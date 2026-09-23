// The turn the player actually fights. Pure C#: no UnityEngine, no BattleCore.
//
// This is the "script generator" of issue #36: instead of replaying a written order, it takes the
// card the player released and writes that one move's script — the same DepictionEvent / Cue /
// DepictionFrame the View already knows how to play. The View therefore still computes nothing;
// it only stopped being told which card it is allowed to receive.
//
// The rules here are the §18 draft scale, the same numbers TurnSliceScript prints by hand. When the
// v4.2 core (unity-port/BattleCore) grows the stamina-and-range model, it replaces the body of this
// class behind IDepictionSource; nothing in the View has to move (issue #29).
using System;
using System.Collections.Generic;

namespace Depiction
{
    public sealed class LiveTurn : IDepictionSource
    {
        public const int PlayerMaxHp = 50;
        public const int EnemyMaxHp = 60;
        public const int MaxStamina = 10;
        /// <summary>Stamina returned at the start of every turn.</summary>
        public const int Recovery = 3;
        public const int HandSize = 5;
        /// <summary>Stamina left at the turn's end that still earns the stance shield.</summary>
        public const int StanceThreshold = 3;
        public const int StanceGuard = 3;
        public const int EnemyAttackPower = 13;
        /// <summary>Extra damage when the attack catches the player on the side the omen names.</summary>
        public const int EnemySideBonus = 5;
        public const int EnemyGuardPower = 9;
        /// <summary>The side the enemy's attack punishes. A polearm soldier wants the player close.</summary>
        public const RangeSide OmenSide = RangeSide.Near;

        private enum Phase
        {
            TurnStart,
            PlayerTurn,
            TurnEnd,
            EnemyAction,
            NextOmen,
            Over,
        }

        private sealed class Dealt
        {
            public string InstanceId = "";
            public DemoCard Def;
        }

        private readonly Random _rng;
        private readonly List<Dealt> _drawPile = new List<Dealt>();
        private readonly List<Dealt> _discardPile = new List<Dealt>();
        private readonly List<Dealt> _hand = new List<Dealt>();

        private int _playerHp = PlayerMaxHp;
        private int _playerGuard;
        private int _stamina = MaxStamina;
        private RangeSide _range = RangeSide.Near;
        private int _enemyHp = EnemyMaxHp;
        private int _enemyGuard;
        private bool _omenAttacks = true;
        private bool _omenVisible;
        private int _turn = 1;
        private int _playedThisTurn;
        private int _order;
        private Phase _phase = Phase.TurnStart;

        /// <param name="seed">Fixes the shuffle, so a test or a capture can replay the same fight.</param>
        public LiveTurn(int seed = 20260920)
        {
            _rng = new Random(seed);
            foreach (DemoCard def in DemoDeck.All)
            {
                for (int copy = 0; copy < DemoDeck.Copies; copy++)
                {
                    _drawPile.Add(new Dealt { InstanceId = def.DefId + "#" + copy, Def = def });
                }
            }
            Shuffle(_drawPile);
            Frame = Snapshot();
        }

        public DepictionFrame Frame { get; private set; }
        public bool Finished => _phase == Phase.Over;
        public bool WaitingForPlayer => _phase == Phase.PlayerTurn;
        public bool CanEndTurn => _phase == Phase.PlayerTurn;

        /// <summary>True when the enemy fell; false when the player did. Only meaningful once finished.</summary>
        public bool PlayerWon => _enemyHp <= 0;

        /// <summary>A live turn has no opinion about which card to drag; the player decides.</summary>
        public string SuggestedCardId => "";

        public string GuideText
        {
            get
            {
                if (_phase == Phase.Over) return PlayerWon ? "敵を討ち取りました" : "力尽きました";
                if (_phase != Phase.PlayerTurn) return "";
                return HasPlayableCard()
                    ? "出したい札を離してください。手が済んだら「ターン終了」"
                    : "出せる札がありません。「ターン終了」を押してください";
            }
        }

        public DepictionEvent AdvanceAuto()
        {
            switch (_phase)
            {
                case Phase.TurnStart: return BuildTurnStart();
                case Phase.EnemyAction: return BuildEnemyAction();
                case Phase.NextOmen: return BuildNextOmen();
                case Phase.Over: throw new InvalidOperationException("The fight is over.");
                default: throw new InvalidOperationException("The turn waits for the player.");
            }
        }

        public DepictionEvent EndTurn()
        {
            if (!CanEndTurn) throw new InvalidOperationException("The turn cannot be ended right now.");
            return BuildTurnEnd();
        }

        public PlayVerdict Inspect(string cardId)
        {
            if (!WaitingForPlayer) return PlayVerdict.NotWaiting;
            Dealt card = InHand(cardId);
            if (card == null) return PlayVerdict.NotInHand;
            if (card.Def.Cost > _stamina) return PlayVerdict.NotEnoughStamina;
            if (card.Def.RequiredRange.HasValue && card.Def.RequiredRange.Value != _range) return PlayVerdict.OutOfRange;
            return PlayVerdict.Accepted;
        }

        public PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)
        {
            played = null;
            PlayVerdict verdict = Inspect(cardId);
            if (verdict != PlayVerdict.Accepted) return verdict;
            Dealt card = InHand(cardId);
            if (zone != DepictionText.RequiredZone(card.Def.Aim)) return PlayVerdict.WrongZone;
            played = BuildPlay(card);
            return PlayVerdict.Accepted;
        }

        public string PreviewFor(string cardId)
        {
            Dealt card = InHand(cardId);
            if (card == null || Inspect(cardId) != PlayVerdict.Accepted) return "";
            DemoCard def = card.Def;
            int bonus = TraitBonus(def);
            if (def.Damage > 0) return (def.Damage + bonus).ToString();
            if (def.Guard > 0) return (def.Guard + bonus).ToString();
            return "";
        }

        public string RefusalText(string cardId, PlayVerdict verdict)
        {
            Dealt card = InHand(cardId);
            if (card == null) return "";
            DemoCard def = card.Def;
            switch (verdict)
            {
                case PlayVerdict.NotEnoughStamina:
                    return "「" + def.Name + "」はスタミナ " + def.Cost + " が要ります。残りは " + _stamina + " です";
                case PlayVerdict.OutOfRange:
                    return "「" + def.Name + "」は" + DemoDeck.Glyph(def.RequiredRange.Value) + "間でしか出せません。いまは"
                        + DemoDeck.Glyph(_range) + "間です";
                case PlayVerdict.WrongZone:
                    return "「" + def.Name + "」は" + DepictionText.ZoneName(def.Aim) + "で離すと出せます";
                default:
                    return "";
            }
        }

        // ---- events -------------------------------------------------------------------------

        private DepictionEvent BuildTurnStart()
        {
            DepictionEvent ev = NewEvent(DepictionEventKind.TurnStart, "ターン開始");
            _playedThisTurn = 0;

            if (_playerGuard != 0)
            {
                _playerGuard = 0;
                ev.Cues.Add(new Cue { Kind = CueKind.GuardReset, Target = UnitSide.Player, GuardAfter = 0 });
            }

            int before = _stamina;
            _stamina = Math.Min(MaxStamina, _stamina + Recovery);
            if (_stamina != before)
            {
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.StaminaChange, Target = UnitSide.Player,
                    Amount = _stamina - before, StaminaAfter = _stamina, StaminaMax = MaxStamina,
                });
            }

            int drawn = DrawUpTo(HandSize);
            if (drawn > 0) ev.Cues.Add(new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = drawn });

            _omenVisible = true;
            ev.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy });

            _phase = Phase.PlayerTurn;
            return Settle(ev);
        }

        private DepictionEvent BuildPlay(Dealt card)
        {
            DemoCard def = card.Def;
            DepictionEvent ev = NewEvent(DepictionEventKind.PlayCard, def.Name);
            ev.CardId = card.InstanceId;
            ev.Aim = def.Aim;
            ev.PreviewText = PreviewFor(card.InstanceId);

            int bonus = TraitBonus(def);
            _hand.Remove(card);
            _discardPile.Add(card);
            _stamina -= def.Cost;
            _playedThisTurn += 1;
            ev.Cues.Add(new Cue
            {
                Kind = CueKind.StaminaChange, Target = UnitSide.Player,
                Amount = -def.Cost, StaminaAfter = _stamina, StaminaMax = MaxStamina,
            });

            if (def.MovesTo.HasValue && def.MovesTo.Value != _range)
            {
                _range = def.MovesTo.Value;
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.RangeSwitch, Target = UnitSide.Player,
                    RangeAfter = _range, RangeGlyphAfter = DemoDeck.Glyph(_range),
                });
            }

            if (def.Guard > 0)
            {
                _playerGuard += def.Guard;
                ev.Cues.Add(GuardGain(UnitSide.Player, def.Guard, _playerGuard));
                if (bonus > 0)
                {
                    ev.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = UnitSide.Player, Text = def.TraitText });
                    _playerGuard += bonus;
                    ev.Cues.Add(GuardGain(UnitSide.Player, bonus, _playerGuard));
                }
            }

            if (def.Damage > 0)
            {
                if (bonus > 0) ev.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = UnitSide.Player, Text = def.TraitText });
                AddAttack(ev, UnitSide.Player, UnitSide.Enemy, def.Damage + bonus);
            }

            if (def.Draw > 0)
            {
                int drawn = DrawUpTo(_hand.Count + def.Draw);
                if (drawn > 0) ev.Cues.Add(new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = drawn });
            }

            if (_enemyHp <= 0) _phase = Phase.Over;
            return Settle(ev);
        }

        private DepictionEvent BuildTurnEnd()
        {
            DepictionEvent ev = NewEvent(DepictionEventKind.TurnEnd, "ターン終了");

            if (_stamina >= StanceThreshold)
            {
                _playerGuard += StanceGuard;
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.StanceCue, Target = UnitSide.Player,
                    Amount = StanceGuard, Text = "+" + StanceGuard, GuardAfter = _playerGuard,
                });
            }

            if (_hand.Count > 0)
            {
                int discarded = _hand.Count;
                _discardPile.AddRange(_hand);
                _hand.Clear();
                ev.Cues.Add(new Cue { Kind = CueKind.DiscardHand, Target = UnitSide.Player, Amount = discarded });
            }

            _phase = Phase.EnemyAction;
            return Settle(ev);
        }

        private DepictionEvent BuildEnemyAction()
        {
            DepictionEvent ev = NewEvent(DepictionEventKind.EnemyAction, _omenAttacks ? "敵の攻撃" : "敵の構え");

            // The shield the enemy raised last turn held through the player's turn; it drops as it moves again.
            if (_enemyGuard != 0)
            {
                _enemyGuard = 0;
                ev.Cues.Add(new Cue { Kind = CueKind.GuardReset, Target = UnitSide.Enemy, GuardAfter = 0 });
            }

            if (_omenAttacks)
            {
                bool caught = _range == OmenSide;
                ev.Cues.Add(new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy });
                if (!caught)
                {
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.SideBonusMiss, Target = UnitSide.Enemy,
                        Amount = EnemySideBonus, Text = "+" + EnemySideBonus,
                    });
                }
                AddAttack(ev, UnitSide.Enemy, UnitSide.Player, EnemyAttackPower + (caught ? EnemySideBonus : 0));
                if (!caught)
                {
                    // A polearm that missed its reach steps in, so the player can back away again next turn.
                    _range = OmenSide;
                    ev.Cues.Add(new Cue
                    {
                        Kind = CueKind.RangeSwitch, Target = UnitSide.Player,
                        RangeAfter = _range, RangeGlyphAfter = DemoDeck.Glyph(_range),
                    });
                }
            }
            else
            {
                _enemyGuard += EnemyGuardPower;
                ev.Cues.Add(GuardGain(UnitSide.Enemy, EnemyGuardPower, _enemyGuard));
            }

            _omenVisible = false;
            _phase = _playerHp <= 0 ? Phase.Over : Phase.NextOmen;
            return Settle(ev);
        }

        private DepictionEvent BuildNextOmen()
        {
            DepictionEvent ev = NewEvent(DepictionEventKind.NextOmen, "次の予兆");
            _omenAttacks = !_omenAttacks;
            _omenVisible = true;
            ev.Cues.Add(new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy });
            _turn += 1;
            _phase = Phase.TurnStart;
            return Settle(ev);
        }

        // ---- rules --------------------------------------------------------------------------

        /// <summary>
        /// One attack, settled. An attack that Guard swallows part of splits into three beats, so the
        /// shield and the wound each carry their own number; an unguarded one stays a single slash.
        /// </summary>
        private void AddAttack(DepictionEvent ev, UnitSide source, UnitSide target, int raw)
        {
            int guard = target == UnitSide.Enemy ? _enemyGuard : _playerGuard;
            int absorbed = Math.Min(guard, raw);
            int through = raw - absorbed;
            bool splits = absorbed > 0 || source == UnitSide.Enemy;

            if (!splits)
            {
                TakeHp(target, through);
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Slash, Source = source, Target = target,
                    Amount = raw, Intensity = Intensity(raw), HpAfter = HpOf(target),
                });
                return;
            }

            // A slash with no settled HP is the swing alone; the cues below carry the numbers.
            ev.Cues.Add(new Cue { Kind = CueKind.Slash, Source = source, Target = target, Amount = raw, Intensity = Intensity(raw) });
            if (absorbed > 0)
            {
                SetGuard(target, guard - absorbed);
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.GuardBlock, Target = target,
                    Amount = absorbed, GuardAfter = guard - absorbed,
                });
            }
            if (through > 0)
            {
                TakeHp(target, through);
                ev.Cues.Add(new Cue
                {
                    Kind = CueKind.Hit, Target = target,
                    Amount = through, Intensity = Intensity(through), HpAfter = HpOf(target),
                });
            }
        }

        private int TraitBonus(DemoCard def)
        {
            switch (def.Trait)
            {
                case TraitCondition.FirstCardOfTurn: return _playedThisTurn == 0 ? DemoDeck.TraitBonus : 0;
                case TraitCondition.AgainstAttackOmen: return _omenAttacks ? DemoDeck.TraitBonus : 0;
                default: return 0;
            }
        }

        /// <summary>How hard the beat looks, 1..4. The View never derives it from the amount itself.</summary>
        private static int Intensity(int amount)
        {
            if (amount <= 9) return 1;
            if (amount <= 16) return 2;
            if (amount <= 24) return 3;
            return 4;
        }

        private static Cue GuardGain(UnitSide target, int amount, int guardAfter)
        {
            return new Cue
            {
                Kind = CueKind.GuardGain, Target = target,
                Amount = amount, Intensity = Intensity(amount), GuardAfter = guardAfter,
            };
        }

        private void TakeHp(UnitSide target, int amount)
        {
            if (target == UnitSide.Enemy) _enemyHp = Math.Max(0, _enemyHp - amount);
            else _playerHp = Math.Max(0, _playerHp - amount);
        }

        private int HpOf(UnitSide target)
        {
            return target == UnitSide.Enemy ? _enemyHp : _playerHp;
        }

        private void SetGuard(UnitSide target, int guard)
        {
            if (target == UnitSide.Enemy) _enemyGuard = guard;
            else _playerGuard = guard;
        }

        // ---- hand ---------------------------------------------------------------------------

        private Dealt InHand(string cardId)
        {
            foreach (Dealt card in _hand)
            {
                if (card.InstanceId == cardId) return card;
            }
            return null;
        }

        private bool HasPlayableCard()
        {
            foreach (Dealt card in _hand)
            {
                if (Inspect(card.InstanceId) == PlayVerdict.Accepted) return true;
            }
            return false;
        }

        /// <returns>How many cards were actually drawn.</returns>
        private int DrawUpTo(int target)
        {
            int drawn = 0;
            while (_hand.Count < target)
            {
                if (_drawPile.Count == 0)
                {
                    if (_discardPile.Count == 0) break;
                    _drawPile.AddRange(_discardPile);
                    _discardPile.Clear();
                    Shuffle(_drawPile);
                }
                _hand.Add(_drawPile[0]);
                _drawPile.RemoveAt(0);
                drawn += 1;
            }
            return drawn;
        }

        private void Shuffle(List<Dealt> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                Dealt swap = cards[i];
                cards[i] = cards[j];
                cards[j] = swap;
            }
        }

        // ---- frames -------------------------------------------------------------------------

        private DepictionEvent NewEvent(DepictionEventKind kind, string title)
        {
            _order += 1;
            return new DepictionEvent { Order = _order, Kind = kind, Title = title };
        }

        private DepictionEvent Settle(DepictionEvent ev)
        {
            ev.After = Snapshot();
            Frame = ev.After;
            return ev;
        }

        private DepictionFrame Snapshot()
        {
            return new DepictionFrame
            {
                Outcome = _phase != Phase.Over ? BattleOutcome.Ongoing : PlayerWon ? BattleOutcome.Won : BattleOutcome.Lost,
                Corner = new CornerFrame { Turn = _turn, Floor = 1, ChainIndex = 1, ChainTotal = 3, MiasmaPercent = 0 },
                Player = new UnitFrame
                {
                    Hp = _playerHp, HpMax = PlayerMaxHp, Guard = _playerGuard,
                    ShowStamina = true, Stamina = _stamina, StaminaMax = MaxStamina,
                    HasRange = true, Range = _range, RangeGlyph = DemoDeck.Glyph(_range),
                },
                Enemy = new UnitFrame
                {
                    Hp = _enemyHp, HpMax = EnemyMaxHp, Guard = _enemyGuard,
                    ShowStamina = false, HasRange = false,
                },
                Omen = OmenNow(),
                Hand = Faces(),
                StanceHint = _phase == Phase.PlayerTurn && _stamina >= StanceThreshold ? "+" + StanceGuard : "",
            };
        }

        private OmenFrame OmenNow()
        {
            if (!_omenVisible) return new OmenFrame { Visible = false };
            return _omenAttacks
                ? new OmenFrame
                {
                    Visible = true, KindLabel = "攻撃",
                    SideGlyph = DemoDeck.Glyph(OmenSide), ValueText = EnemyAttackPower.ToString(),
                }
                : new OmenFrame { Visible = true, KindLabel = "防御" };
        }

        private List<CardFace> Faces()
        {
            var faces = new List<CardFace>();
            foreach (Dealt card in _hand)
            {
                faces.Add(DemoDeck.Face(card.Def, card.InstanceId, TraitBonus(card.Def) > 0));
            }
            return faces;
        }
    }
}
