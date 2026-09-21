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
            public Position? Position;
            public readonly SortedDictionary<StatusKind, int> Statuses = new SortedDictionary<StatusKind, int>();

            public void CopyFrom(CombatantState unit)
            {
                Hp = unit.Hp;
                HpMax = unit.MaxHp;
                Guard = unit.Guard;
                Stamina = unit.Stamina;
                StaminaMax = unit.MaxStamina;
                Position = unit.Position;
                Statuses.Clear();
                foreach (StatusKind kind in unit.Statuses.Kinds) Statuses[kind] = unit.Statuses.Stacks(kind);
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

        /// <summary>The enemy's 構え, held back so it plays with the next omen and not inside the attack (see <see cref="Write"/>).</summary>
        private ReserveChecked _enemyReserveHeld;

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
            _player.CopyFrom(state.Player);
            _enemy.CopyFrom(state.Enemy);
            _hand.Clear();
            _hand.AddRange(state.Hand);
            _omen = state.Omen;
            _omenVisible = false;
            _playerActs = state.Phase == BattlePhase.PlayerAction;
            return Snapshot(state);
        }

        /// <summary>
        /// One move of the loop → the screen events it plays as. BeginPlayerTurn and PlayCard are one
        /// event each; EndTurn is three (turn end, the enemy's action, the next omen), the same beats
        /// the screen already knows.
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
                DepictionEvent opened = OpenIfBoundary(e);
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

        private DepictionEvent OpenIfBoundary(BattleEvent e)
        {
            switch (e)
            {
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

                case ReserveChecked reserve when reserve.Actor == Actor.Player:
                    _playerActs = false;
                    return NewEvent(DepictionEventKind.TurnEnd, "ターン終了");

                case GuardCleared cleared when cleared.Actor == Actor.Enemy:
                    return NewEvent(DepictionEventKind.EnemyAction, "敵の番");

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
                    if (!applied.Refused) SetStatus(ev, applied.Target, applied.Kind, applied.Stacks, applied.StacksAfter);
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
                    break;

                case ActionExecuted executed:
                    ev.Title = executed.Action.Name;
                    ev.Cues.Add(new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy });
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

                case PositionChanged moved:
                    Unit(moved.Actor).Position = moved.To;
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.RangeSwitch, Target = CoreText.Side(moved.Actor),
                        RangeAfter = CoreText.Side(moved.To), RangeGlyphAfter = moved.To.ToLabel(),
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

            // An enemy blow that reads the player's side and found them on the other one: the screen
            // strikes the bonus off the omen badge, so stepping away visibly paid off.
            if (trait.Actor == Actor.Enemy
                && trait.Trait.Condition == BattleCore.TraitCondition.OpponentPosition
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
                    Kind = CueKind.Slash, Source = source, Target = target,
                    Amount = damage.Raw, Intensity = CoreText.Intensity(damage.Raw), HpAfter = damage.TargetHpAfter,
                });
                return;
            }

            ev.Cues.Add(new Cue
            {
                Kind = CueKind.Slash, Source = source, Target = target,
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
                // Floor, chain and miasma belong to the exploration layer (#99); one battle, no miasma.
                Corner = new CornerFrame { Turn = _turn, Floor = 1, ChainIndex = 1, ChainTotal = 1, MiasmaPercent = 0 },
                Player = UnitOf(_player, showStamina: true),
                Enemy = UnitOf(_enemy, showStamina: false),
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

        private static UnitFrame UnitOf(UnitModel unit, bool showStamina)
        {
            var frame = new UnitFrame
            {
                Hp = unit.Hp, HpMax = unit.HpMax, Guard = unit.Guard,
                ShowStamina = showStamina, Stamina = unit.Stamina, StaminaMax = unit.StaminaMax,
                HasRange = unit.Position.HasValue,
            };
            if (unit.Position.HasValue)
            {
                frame.Range = CoreText.Side(unit.Position.Value);
                frame.RangeGlyph = unit.Position.Value.ToLabel();
            }
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
