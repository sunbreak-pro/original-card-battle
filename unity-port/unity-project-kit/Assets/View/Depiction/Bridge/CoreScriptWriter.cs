// Turns the core's event stream (BattleCore.BattleEvent) into the script the screen plays
// (DepictionEvent / Cue / DepictionFrame). Pure C#: BattleCore + Depiction.Script, no UnityEngine.
//
// Nothing here works a rule out. Every number on a cue is copied from an event's settled value
// (StaminaAfter, TargetHpAfter, GuardAfter …), and what a held card would do comes from the core's
// own TurnLoop.Preview. What this class does decide is presentation: where one screen event ends
// and the next begins, and which beat shows which number.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed class CoreScriptWriter
    {
        /// <summary>The numbers one unit shows, copied forward from events as they are read.</summary>
        private sealed class UnitModel
        {
            public int Hp;
            public int HpMax;
            public int Guard;
            public int Stamina;
            public int StaminaMax;
            public int Cell;
            public int Size;
            public readonly SortedDictionary<StatusKind, int> Statuses = new SortedDictionary<StatusKind, int>();

            /// <summary>§4: the name of the stance in the slot (#188), or empty.</summary>
            public string Stance = "";

            public void CopyFrom(CombatantState unit, string stanceName)
            {
                Hp = unit.Hp;
                HpMax = unit.MaxHp;
                Guard = unit.Guard;
                Stamina = unit.Stamina;
                StaminaMax = unit.MaxStamina;
                Cell = unit.Cell;
                Size = unit.Size;
                Statuses.Clear();
                foreach (StatusKind kind in unit.Statuses.Kinds) Statuses[kind] = unit.Statuses.Stacks(kind);
                Stance = stanceName ?? "";
            }
        }

        private readonly EnemyDef _enemyDef;
        private readonly UnitModel _player = new UnitModel();
        private readonly UnitModel _enemy = new UnitModel();
        private readonly List<CardInstance> _hand = new List<CardInstance>();
        private int _turn;
        private Omen _omen;
        private bool _omenVisible;
        private bool _playerActs;
        private int _order;

        /// <summary>The enemy action being written whiffed (§6): its trait miss has nothing to strike off.</summary>
        private bool _whiffed;

        /// <summary>The system (§5.3) of the card or action whose blows are being written.</summary>
        private StrikeSystem _strike = StrikeSystem.Slash;

        /// <summary>The enemy has carried out an action in this phase already (#189: an elite's second one opens a beat of its own).</summary>
        private bool _enemyActed;

        /// <summary>The enemy's 構え, held back so it plays with the next omen and not inside the attack (see <see cref="Write"/>).</summary>
        private ReserveChecked _enemyReserveHeld;

        /// <summary>§7.2: N as the two models stand now.</summary>
        private int Gap => _enemy.Cell - (_player.Cell + _player.Size - 1) - 1;

        /// <summary>§12 連戦 (#191): which battle of the chain this is, and how many there are; 1 / 1 alone.</summary>
        public int ChainIndex { get; set; } = 1;

        public int ChainTotal { get; set; } = 1;

        public CoreScriptWriter(EnemyDef enemyDef)
        {
            _enemyDef = enemyDef ?? throw new ArgumentNullException(nameof(enemyDef));
        }

        /// <summary>
        /// The screen before turn 1. The first omen is already decided (TurnLoop.Start) but stays
        /// hidden: step 5 of the first turn is what shows it.
        /// </summary>
        public DepictionFrame Opening(BattleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _turn = Math.Max(1, state.Turn);
            _player.CopyFrom(state.Player, StanceName(state.Player.StanceSource, null));
            _enemy.CopyFrom(state.Enemy, StanceName(state.Enemy.StanceSource, _enemyDef));
            _hand.Clear();
            _hand.AddRange(state.Hand);
            _omen = state.Omen;
            _omenVisible = false;
            _playerActs = state.Phase == BattlePhase.PlayerAction;
            return Snapshot(state);
        }

        /// <summary>
        /// One move of the loop → the screen events it plays as. BeginPlayerTurn and PlayCard are one
        /// event each; EndTurn is three for the slice (turn end, the enemy's action, the next omen),
        /// the same beats the screen already knows. A few moments take a beat of their own so each
        /// event stays inside the 2.0 s it may take: the enemy's push driving the player into the
        /// wall, and (#189) an enemy turn start that did something, an elite's second action, the
        /// second blow of 二段斬り and the step an enemy takes after its blow.
        ///
        /// <paramref name="after"/> is the state the move left. It is only asked what the cards still
        /// in the hand would do (the lamp), never for a number an event already carries.
        /// </summary>
        public List<DepictionEvent> Write(IReadOnlyList<BattleEvent> events, BattleState after)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var written = new List<DepictionEvent>();
            DepictionEvent current = null;

            foreach (BattleEvent e in events)
            {
                DepictionEvent opened = OpenIfBoundary(e, current);
                if (opened != null)
                {
                    Close(current, after);
                    current = opened;
                    written.Add(current);
                }
                if (current == null) continue; // TurnLoop.Start's first omen: folded into Opening, not played.
                Apply(e, current);
            }

            // A held enemy 構え plays at the head of the last event: the next omen, or — when the
            // battle ended and no omen follows — the enemy's own action.
            if (current != null) FlushHeldReserve(current);
            Close(current, after);
            return written;
        }

        // ---- where a screen event begins ----------------------------------------------------

        private DepictionEvent OpenIfBoundary(BattleEvent e, DepictionEvent current)
        {
            switch (e)
            {
                case WallHit wall when current != null && current.Kind == DepictionEventKind.EnemyAction:
                    // §7.3: the player, shoved into the end of the line. Inside a player's card the wall
                    // is the enemy's and fits in the card's own event.
                    return NewEvent(DepictionEventKind.EnemyAction, "壁に当たる");

                case TurnStarted started when started.Actor == Actor.Player:
                    _turn = started.Turn;
                    _playerActs = true;
                    return NewEvent(DepictionEventKind.TurnStart, "ターン開始");

                case CardPlayed played:
                {
                    DepictionEvent ev = NewEvent(DepictionEventKind.PlayCard, played.Card.Def.Name);
                    ev.CardId = played.Card.InstanceId;
                    ev.Aim = played.Card.Def.Targets == TargetKind.One ? CardAim.Single : CardAim.Self;
                    return ev;
                }

                // Step 7 opens the turn end: a turn-end stance (根渡り, 霞み足, #188) fires before 構え
                // and belongs to the same event, so 構え does not open a second one.
                case StanceFired fired when fired.Actor == Actor.Player && fired.Hook == StanceHook.TurnEnd:
                    _playerActs = false;
                    return NewEvent(DepictionEventKind.TurnEnd, "ターン終了");

                case ReserveChecked reserve when reserve.Actor == Actor.Player
                    && (current == null || current.Kind != DepictionEventKind.TurnEnd):
                    _playerActs = false;
                    return NewEvent(DepictionEventKind.TurnEnd, "ターン終了");

                case GuardCleared cleared when cleared.Actor == Actor.Enemy:
                    _enemyActed = false;
                    return NewEvent(DepictionEventKind.EnemyAction, "敵の番");

                // roster §1.3 (#189): an elite's or a boss's second action is a beat of its own, so
                // neither blow is squeezed into the 2.0 s one event may take.
                case ActionExecuted second when current != null && current.Kind == DepictionEventKind.EnemyAction
                    && _enemyActed:
                    return NewEvent(DepictionEventKind.EnemyAction, second.Action.Name);

                // #189: an enemy turn start that did something of its own (a stance's Guard, 再生 or
                // 出血) is its beat; the action follows as the next one.
                case ActionExecuted first when current != null && current.Kind == DepictionEventKind.EnemyAction
                    && current.Cues.Exists(c => c.Kind == CueKind.GuardGain || c.Kind == CueKind.HpChange):
                    return NewEvent(DepictionEventKind.EnemyAction, first.Action.Name);

                // §2.2 (#189): an enemy that strikes and then steps in (飛びかかり, 当ててから前へ 2) takes
                // the step as its own beat once the blow has landed.
                case CellsMoved step when step.Actor == Actor.Enemy && !step.Pushed && current != null
                    && current.Kind == DepictionEventKind.EnemyAction
                    && current.Cues.Exists(c => c.Kind == CueKind.Hit || c.Kind == CueKind.GuardBlock):
                    return NewEvent(DepictionEventKind.EnemyAction, current.Title + (step.To < step.From ? "（詰める）" : "（下がる）"));

                // §2.4 hits (#189): the second blow of an enemy face is a beat of its own (二段斬り),
                // opened where the 脆化 its first blow left is spent for it.
                case StatusConsumed used when used.Actor == Actor.Player && used.Kind == StatusKind.Fragile
                    && current != null && current.Kind == DepictionEventKind.EnemyAction
                    && current.Cues.Exists(c => c.Kind == CueKind.Slash && c.Source == UnitSide.Enemy):
                    return NewEvent(DepictionEventKind.EnemyAction, current.Title + "（2 撃目）");

                case DamageDealt blow when blow.Actor == Actor.Enemy && current != null
                    && current.Kind == DepictionEventKind.EnemyAction
                    && current.Cues.Exists(c => c.Kind == CueKind.Slash && c.Source == UnitSide.Enemy):
                    return NewEvent(DepictionEventKind.EnemyAction, current.Title + "（2 撃目）");

                case OmenSet omen when omen.Decided && _order > 0:
                    return NewEvent(DepictionEventKind.NextOmen, "次の予兆");

                default:
                    return null;
            }
        }

        // ---- one core event → its beats -----------------------------------------------------

        private void Apply(BattleEvent e, DepictionEvent ev)
        {
            switch (e)
            {
                case GuardCleared cleared:
                {
                    UnitModel unit = Unit(cleared.Actor);
                    unit.Guard = 0;
                    if (cleared.Cleared > 0)
                    {
                        ev.Cues.Add(new Cue { Kind = CueKind.GuardReset, Target = CoreText.Side(cleared.Actor), GuardAfter = 0 });
                    }
                    break;
                }

                case StaminaRecovered recovered:
                    SetStamina(ev, recovered.Actor, recovered.Amount, recovered.StaminaAfter);
                    break;

                case StaminaSpent spent:
                    SetStamina(ev, spent.Actor, -spent.Amount, spent.StaminaAfter);
                    break;

                case StaminaGained gained:
                    SetStamina(ev, gained.Actor, gained.Amount, gained.StaminaAfter);
                    break;

                case StatusTicked ticked:
                    SetStatus(ev, ticked.Actor, ticked.Kind, -1, ticked.StacksAfter);
                    break;

                case StatusApplied applied:
                    // #205: a ターンで減る型 word stops at its cap, so what landed can be less than what
                    // was given, or nothing; the core says how much.
                    if (applied.Landed > 0) SetStatus(ev, applied.Target, applied.Kind, applied.Landed, applied.StacksAfter);
                    break;

                case Drawn drawn:
                {
                    _hand.Add(drawn.Card);
                    // A hand of five is five core events and one beat on screen.
                    Cue last = ev.Cues.Count > 0 ? ev.Cues[ev.Cues.Count - 1] : null;
                    if (last != null && last.Kind == CueKind.DrawHand) last.Amount += 1;
                    else ev.Cues.Add(new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 1 });
                    break;
                }

                case OmenSet omen:
                {
                    bool alreadyShown = _omenVisible && Equals(_omen, omen.Omen);
                    _omen = omen.Omen;
                    _omenVisible = true;
                    // Step 5 re-announces the omen step 12 already put up; the screen shows it once.
                    if (!alreadyShown) ev.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy });
                    break;
                }

                case CardPlayed played:
                    _hand.Remove(played.Card);
                    _strike = CoreText.SystemOf(played.Card.Def);
                    break;

                case ActionExecuted executed:
                    _enemyActed = true;
                    ev.Title = executed.Action.Name;
                    _strike = CoreText.SystemOf(executed.Action);
                    ev.Cues.Add(new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy, System = _strike });
                    break;

                case Rested _:
                    ev.Title = "敵は動けない";
                    break;

                case TraitEvaluated trait:
                    ApplyTrait(ev, trait);
                    break;

                case DamageDealt damage:
                    ApplyDamage(ev, damage);
                    break;

                case GuardGained guard:
                    Unit(guard.Actor).Guard = guard.GuardAfter;
                    ev.Cues.Add(GuardGain(guard.Actor, guard.Amount, guard.GuardAfter));
                    break;

                case CellsMoved moved:
                    // Whichever side moved, the screen has one thing to show for it: the new N on the
                    // player's tag, and the player's figure in the slot N maps to (CoreText.SideOf).
                    // Moving the enemy figure waits for the floor cells (#163).
                    Unit(moved.Actor).Cell = moved.To;
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.RangeSwitch, Target = UnitSide.Player, Pushed = moved.Pushed,
                        RangeAfter = CoreText.SideOf(Gap), RangeGlyphAfter = CoreText.GapGlyph(Gap),
                    });
                    break;

                case WallHit wall:
                    ApplyWallHit(ev, wall);
                    break;

                case ActionWhiffed whiff:
                    // The blow finds nobody: the omen's number is struck off, the way a missed side bonus is.
                    _whiffed = true;
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.SideBonusMiss, Target = UnitSide.Enemy,
                        Amount = _enemyDef.Actions[whiff.SourceId].Face.Power * Math.Max(1, _enemyDef.Actions[whiff.SourceId].Face.Hits), Text = "空振り",
                    });
                    break;

                case ReserveChecked reserve:
                    ApplyReserve(ev, reserve);
                    break;

                case HandDiscarded discarded:
                    _hand.Clear();
                    if (discarded.Count > 0)
                    {
                        ev.Cues.Add(new Cue { Kind = CueKind.DiscardHand, Target = UnitSide.Player, Amount = discarded.Count });
                    }
                    break;

                // ---- the demo vocabulary (#188) ----

                case StatusConsumed consumed:
                    SetStatus(ev, consumed.Actor, consumed.Kind, -1, consumed.StacksAfter);
                    break;

                case StatusHpChanged changed:
                    SetHp(ev, changed.Actor, changed.Amount, changed.HpAfter, changed.Kind.ToLabel());
                    break;

                case Healed healed:
                    SetHp(ev, healed.Actor, healed.Amount, healed.HpAfter, "回復");
                    break;

                case StaminaBroken broken:
                    SetStamina(ev, broken.Target, -broken.Amount, broken.StaminaAfter);
                    break;

                case Reflected reflected:
                {
                    UnitModel struck = Unit(reflected.Target);
                    struck.Guard = reflected.TargetGuardAfter;
                    if (reflected.Absorbed > 0)
                    {
                        ev.Cues.Add(new Cue
                        {
                            Kind = CueKind.GuardBlock, Target = CoreText.Side(reflected.Target),
                            Amount = reflected.Absorbed, GuardAfter = reflected.TargetGuardAfter,
                        });
                    }
                    SetHp(ev, reflected.Target, -reflected.Damage, reflected.TargetHpAfter, "見切り");
                    break;
                }

                case StanceSet set:
                    Unit(set.Actor).Stance = set.Name;
                    ev.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = CoreText.Side(set.Actor), Text = "構え・" + set.Name });
                    break;
            }

            // The omen is spent once the enemy has acted on it (or rested through it).
            if (e is ActionExecuted || e is Rested) _omenVisible = false;
        }

        private void ApplyTrait(DepictionEvent ev, TraitEvaluated trait)
        {
            UnitSide side = CoreText.Side(trait.Actor);
            if (trait.Outcome.Triggered)
            {
                ev.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = side, Text = CoreText.TraitLine(trait.Trait) });
                return;
            }

            // An enemy blow that reads the gap and found the player outside its threshold: the screen
            // strikes the bonus off the omen badge, so stepping away (or in) visibly paid off. A blow
            // that whiffed has already had its whole number struck off.
            if (!_whiffed
                && trait.Actor == Actor.Enemy
                && (trait.Trait.Condition == BattleCore.TraitCondition.GapAtLeast
                    || trait.Trait.Condition == BattleCore.TraitCondition.GapAtMost)
                && trait.Trait.Effect == TraitEffect.PowerBonus)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.SideBonusMiss, Target = UnitSide.Enemy,
                    Amount = trait.Trait.Amount, Text = "+" + trait.Trait.Amount,
                });
            }
        }

        /// <summary>
        /// One attack, settled. When Guard swallows part of it — and for every enemy blow — it plays as
        /// three beats (swing, shield, wound) so each number has its own moment; otherwise one slash.
        /// </summary>
        private void ApplyDamage(DepictionEvent ev, DamageDealt damage)
        {
            UnitSide source = CoreText.Side(damage.Actor);
            UnitSide target = CoreText.Side(damage.Target);
            UnitModel unit = Unit(damage.Target);
            unit.Guard = damage.TargetGuardAfter;
            unit.Hp = damage.TargetHpAfter;

            if (ev.Kind == DepictionEventKind.PlayCard) ev.PreviewText = damage.Damage.ToString();

            bool splits = damage.Absorbed > 0 || damage.Actor == Actor.Enemy;
            if (!splits)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Slash, Source = source, Target = target, System = _strike,
                    Amount = damage.Raw, Intensity = CoreText.Intensity(damage.Raw), HpAfter = damage.TargetHpAfter,
                });
                return;
            }

            ev.Cues.Add(new Cue
            {
                Kind = CueKind.Slash, Source = source, Target = target, System = _strike,
                Amount = damage.Raw, Intensity = CoreText.Intensity(damage.Raw),
            });
            if (damage.Absorbed > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.GuardBlock, Target = target,
                    Amount = damage.Absorbed, GuardAfter = damage.TargetGuardAfter,
                });
            }
            if (damage.Damage > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Hit, Target = target,
                    Amount = damage.Damage, Intensity = CoreText.Intensity(damage.Damage), HpAfter = damage.TargetHpAfter,
                });
            }
        }

        /// <summary>§7.3 壁のダメージ: the shield and the wound, like an enemy blow without its swing.</summary>
        private void ApplyWallHit(DepictionEvent ev, WallHit wall)
        {
            UnitSide target = CoreText.Side(wall.Actor);
            UnitModel unit = Unit(wall.Actor);
            unit.Guard = wall.GuardAfter;
            unit.Hp = wall.HpAfter;
            if (wall.Absorbed > 0)
            {
                ev.Cues.Add(new Cue { Kind = CueKind.GuardBlock, Target = target, Amount = wall.Absorbed, GuardAfter = wall.GuardAfter });
            }
            if (wall.Damage > 0)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Hit, Target = target,
                    Amount = wall.Damage, Intensity = CoreText.Intensity(wall.Damage), HpAfter = wall.HpAfter,
                });
            }
        }

        private void ApplyReserve(DepictionEvent ev, ReserveChecked reserve)
        {
            if (reserve.Actor == Actor.Player)
            {
                _player.Guard = reserve.GuardAfter;
                if (reserve.GuardGained > 0)
                {
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.StanceCue, Target = UnitSide.Player,
                        Amount = reserve.GuardGained, Text = "+" + reserve.GuardGained, GuardAfter = reserve.GuardAfter,
                    });
                }
                return;
            }

            // The enemy's 構え would push its own action past the 2.0 s an event may take (a shove is
            // already windup + swing + shield + wound + the push). It plays with the next omen instead,
            // and the Guard number on screen waits with it.
            if (reserve.GuardGained > 0) _enemyReserveHeld = reserve;
        }

        private void FlushHeldReserve(DepictionEvent ev)
        {
            if (_enemyReserveHeld == null) return;
            ReserveChecked reserve = _enemyReserveHeld;
            _enemyReserveHeld = null;
            _enemy.Guard = reserve.GuardAfter;
            // First in the event: the shield goes up, then the omen it stands behind appears.
            ev.Cues.Insert(0, GuardGain(Actor.Enemy, reserve.GuardGained, reserve.GuardAfter));
        }

        private void SetStamina(DepictionEvent ev, Actor actor, int amount, int staminaAfter)
        {
            UnitModel unit = Unit(actor);
            unit.Stamina = staminaAfter;
            // Only the player's pips are on screen (the enemy's stamina is not disclosed).
            if (actor != Actor.Player || amount == 0) return;
            ev.Cues.Add(new Cue
            {
                Kind = CueKind.StaminaChange, Target = UnitSide.Player,
                Amount = amount, StaminaAfter = staminaAfter, StaminaMax = unit.StaminaMax,
            });
        }

        private void SetStatus(DepictionEvent ev, Actor holder, StatusKind kind, int change, int stacksAfter)
        {
            UnitModel unit = Unit(holder);
            if (stacksAfter > 0) unit.Statuses[kind] = stacksAfter;
            else unit.Statuses.Remove(kind);
            ev.Cues.Add(new Cue
            {
                Kind = CueKind.StatusChange, Target = CoreText.Side(holder),
                Amount = change, Text = kind.ToLabel(), StacksAfter = stacksAfter,
            });
        }

        /// <summary>HP moved without a blow: 出血 / 再生, a heal, a 見切り return. The bar follows the settled value.</summary>
        private void SetHp(DepictionEvent ev, Actor actor, int amount, int hpAfter, string cause)
        {
            Unit(actor).Hp = hpAfter;
            if (amount == 0) return;
            ev.Cues.Add(new Cue
            {
                Kind = CueKind.HpChange, Target = CoreText.Side(actor),
                Amount = amount, HpAfter = hpAfter, Text = cause,
            });
        }

        /// <summary>The display name of a stance's source: a card for the player, an action of the enemy's.</summary>
        private static string StanceName(string sourceId, EnemyDef enemy)
        {
            if (string.IsNullOrEmpty(sourceId)) return "";
            EnemyActionDef action;
            if (enemy != null && enemy.Actions.TryGetValue(sourceId, out action)) return action.Name;
            foreach (CardDef def in CardCatalog.All)
            {
                if (def.Id == sourceId) return def.Name;
            }
            return sourceId;
        }

        private static Cue GuardGain(Actor actor, int amount, int guardAfter)
        {
            return new Cue
            {
                Kind = CueKind.GuardGain, Target = CoreText.Side(actor),
                Amount = amount, Intensity = CoreText.Intensity(amount), GuardAfter = guardAfter,
            };
        }

        // ---- frames -------------------------------------------------------------------------

        private DepictionEvent NewEvent(DepictionEventKind kind, string title)
        {
            _order += 1;
            _whiffed = false;
            return new DepictionEvent { Order = _order, Kind = kind, Title = title };
        }

        private void Close(DepictionEvent ev, BattleState after)
        {
            if (ev == null) return;
            ev.After = Snapshot(after);
        }

        private DepictionFrame Snapshot(BattleState after)
        {
            var frame = new DepictionFrame
            {
                Outcome = after.Result == GameResult.Won ? BattleOutcome.Won
                    : after.Result == GameResult.Lost ? BattleOutcome.Lost
                    : BattleOutcome.Ongoing,
                // Floor and miasma belong to the exploration layer (#99); the chain position is the demo's (#191).
                Corner = new CornerFrame { Turn = _turn, Floor = 1, ChainIndex = ChainIndex, ChainTotal = ChainTotal, MiasmaPercent = 0 },
                Player = UnitOf(_player, showStamina: true, gap: Gap),
                Enemy = UnitOf(_enemy, showStamina: false, gap: null),
                Omen = _omenVisible ? CoreText.OmenOf(_omen, _enemyDef) : new OmenFrame { Visible = false },
            };
            foreach (CardInstance card in _hand)
            {
                frame.Hand.Add(CoreText.Face(card, TurnLoop.Preview(after, card.InstanceId)));
            }
            bool waiting = _playerActs && after.Result == GameResult.Ongoing;
            int reserve = waiting ? Combat.ReserveGuard(_player.Stamina) : 0;
            frame.StanceHint = reserve > 0 ? "+" + reserve : "";
            return frame;
        }

        private static UnitFrame UnitOf(UnitModel unit, bool showStamina, int? gap)
        {
            var frame = new UnitFrame
            {
                Hp = unit.Hp, HpMax = unit.HpMax, Guard = unit.Guard,
                ShowStamina = showStamina, Stamina = unit.Stamina, StaminaMax = unit.StaminaMax,
                HasRange = gap.HasValue,
            };
            if (gap.HasValue)
            {
                frame.Range = CoreText.SideOf(gap.Value);
                frame.RangeGlyph = CoreText.GapGlyph(gap.Value);
            }
            // §4: the stance in the slot leads the chips; it has no stacks to count.
            if (unit.Stance.Length > 0) frame.Statuses.Add(new StatusChip { Label = "構え・" + unit.Stance, Stacks = 0 });
            foreach (KeyValuePair<StatusKind, int> pair in unit.Statuses)
            {
                frame.Statuses.Add(new StatusChip { Label = pair.Key.ToLabel(), Stacks = pair.Value });
            }
            return frame;
        }

        private UnitModel Unit(Actor actor)
        {
            return actor == Actor.Player ? _player : _enemy;
        }
    }
}
