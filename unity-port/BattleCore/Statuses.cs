using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// §5: the statuses one combatant carries, as word → stack count. Immutable: every change hands
    /// back a new set, so a battle state can be compared with the one before it.
    ///
    /// A word with zero stacks is not held at all, which is what keeps the kind count honest. A
    /// ターンで減る型 word never holds more than <see cref="Constants.TurnDecayStackMax"/> (§5, #205):
    /// both ways in (<see cref="Of"/> and <see cref="Add"/>) stop it there, and the others only take
    /// stacks away.
    /// </summary>
    public sealed class StatusSet : IEquatable<StatusSet>
    {
        public static readonly StatusSet Empty = new StatusSet(new Dictionary<StatusKind, int>());

        private readonly IReadOnlyDictionary<StatusKind, int> _stacks;

        private StatusSet(IReadOnlyDictionary<StatusKind, int> stacks)
        {
            _stacks = stacks;
        }

        public static StatusSet Of(params (StatusKind Kind, int Stacks)[] entries)
        {
            var map = new Dictionary<StatusKind, int>();
            foreach (var entry in entries)
            {
                if (entry.Stacks > 0) map[entry.Kind] = Clamp(entry.Kind, entry.Stacks);
            }
            return new StatusSet(map);
        }

        /// <summary>The words held, in enum order, so a screen lists them the same way twice.</summary>
        public IReadOnlyList<StatusKind> Kinds =>
            _stacks.Keys.OrderBy(k => (int)k).ToList();

        public int KindCount => _stacks.Count;

        public bool Has(StatusKind kind) => _stacks.ContainsKey(kind);

        public int Stacks(StatusKind kind) => _stacks.TryGetValue(kind, out int n) ? n : 0;

        /// <summary>
        /// §3.1 / §5: add stacks of one word. Stacking an existing word is always allowed; taking on
        /// a word that is not held yet is refused once the holder is at its kind limit, and the set
        /// comes back unchanged. The player limit is 6 kinds (§5); enemies pass no limit.
        ///
        /// Which word gets pushed out when a seventh arrives is not settled in the canon, so nothing
        /// is pushed out here. That question returns with #47.
        ///
        /// §5 (#205): a ターンで減る型 word stops at <see cref="Constants.TurnDecayStackMax"/> and the
        /// rest is dropped, on either side; a word already there comes back unchanged. The 使うと減る型
        /// is not counted (§19.5 S14).
        /// </summary>
        public StatusSet Add(StatusKind kind, int stacks = Constants.StatusApplyDefault, int? kindLimit = null)
        {
            if (stacks <= 0) return this;
            bool isNewKind = !_stacks.ContainsKey(kind);
            if (isNewKind && kindLimit.HasValue && _stacks.Count >= kindLimit.Value) return this;

            int next = Clamp(kind, Stacks(kind) + stacks);
            if (next == Stacks(kind)) return this;

            var map = ToDictionary();
            map[kind] = next;
            return new StatusSet(map);
        }

        /// <summary>§5 (#205): the ターンで減る型 stops at the cap; the 使うと減る型 is not counted.</summary>
        private static int Clamp(StatusKind kind, int stacks) =>
            Statuses.DecayOf(kind) == StatusDecay.OnTurn ? Math.Min(stacks, Constants.TurnDecayStackMax) : stacks;

        /// <summary>
        /// §5: the ターンで減る型 loses one stack at the holder's turn start. The 使うと減る型 is
        /// untouched here — it is spent by <see cref="Consume"/> when its effect fires.
        /// </summary>
        public StatusSet TickTurnStart()
        {
            var map = new Dictionary<StatusKind, int>();
            foreach (var pair in _stacks)
            {
                int next = Statuses.DecayOf(pair.Key) == StatusDecay.OnTurn ? pair.Value - 1 : pair.Value;
                if (next > 0) map[pair.Key] = next;
            }
            return new StatusSet(map);
        }

        /// <summary>§5: the 使うと減る型 loses one stack each time its effect lands.</summary>
        public StatusSet Consume(StatusKind kind)
        {
            if (!_stacks.ContainsKey(kind)) return this;
            var map = ToDictionary();
            int next = map[kind] - 1;
            if (next > 0) map[kind] = next;
            else map.Remove(kind);
            return new StatusSet(map);
        }

        private Dictionary<StatusKind, int> ToDictionary() =>
            _stacks.ToDictionary(p => p.Key, p => p.Value);

        public bool Equals(StatusSet? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (_stacks.Count != other._stacks.Count) return false;
            foreach (var pair in _stacks)
            {
                if (other.Stacks(pair.Key) != pair.Value) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as StatusSet);

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (var kind in Kinds)
            {
                hash = (hash * 31) + (int)kind;
                hash = (hash * 31) + Stacks(kind);
            }
            return hash;
        }

        public override string ToString() =>
            _stacks.Count == 0 ? "—" : string.Join(" / ", Kinds.Select(k => $"{k.ToLabel()} {Stacks(k)}"));
    }

    /// <summary>
    /// The rules each status word follows. The turn loop applies them (#188): the turn-start words
    /// in <see cref="TurnLoop"/>'s OpenTurnFor, the on-use words where their effect lands.
    /// </summary>
    public static class Statuses
    {
        /// <summary>§5: which of the two decay types a word follows.</summary>
        public static StatusDecay DecayOf(StatusKind kind) => kind switch
        {
            StatusKind.Slow => StatusDecay.OnTurn,
            StatusKind.Swift => StatusDecay.OnTurn,
            StatusKind.Bleed => StatusDecay.OnTurn,
            StatusKind.Fatigue => StatusDecay.OnTurn,
            StatusKind.Regen => StatusDecay.OnTurn,
            StatusKind.Fragile => StatusDecay.OnUse,
            StatusKind.Intimidate => StatusDecay.OnUse,
            StatusKind.Empower => StatusDecay.OnUse,
            StatusKind.Focus => StatusDecay.OnUse,
            StatusKind.Parry => StatusDecay.OnUse,
            // Boss-only words (#51): roster §4.1 / §5.1 / §6.2.
            StatusKind.MiasmaShroud => StatusDecay.Lasting,
            StatusKind.Binding => StatusDecay.OnTurn,
            StatusKind.Hook => StatusDecay.OnUse,
            StatusKind.Depths => StatusDecay.Lasting,
            StatusKind.Rooting => StatusDecay.Lasting,
            StatusKind.Withering => StatusDecay.Lasting,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown status."),
        };

        /// <summary>§5 向き: whether the word is one a side puts on itself (俊敏 / 強化 / 集中 / 見切り / 再生).</summary>
        public static bool IsOwn(StatusKind kind) => kind switch
        {
            StatusKind.Swift => true,
            StatusKind.Empower => true,
            StatusKind.Focus => true,
            StatusKind.Parry => true,
            StatusKind.Regen => true,
            _ => false,
        };

        /// <summary>
        /// §5 鈍足 (v4.3 reading, §21.3): every cell change the holder causes — its own move face and
        /// the push / pull it lands — is one cell shorter, floor 0. A one-cell move still stops dead,
        /// as it did in v4.2; a two-cell one becomes one. What the other side does to the holder is
        /// not reduced.
        /// </summary>
        public static int MoveReduction(StatusSet statuses)
        {
            if (statuses == null) throw new ArgumentNullException(nameof(statuses));
            return statuses.Has(StatusKind.Slow) ? 1 : 0;
        }

        // ---- The boss-only words (#51) ----

        /// <summary>roster §6.2 根張り: HP lost per stack at the turn start after two turns ended on one cell.</summary>
        public const int RootingHpPerStack = 4;

        /// <summary>roster §4.1 / §5.1 / §6.2: the most stacks a boss puts of its word (瘴気纏い 2 → −2, 深み 2 → −2, 根張り 2 → −8, 枯らし 2 → −2).</summary>
        public const int BossWordStackMax = 2;

        /// <summary>Whether the word is one of the six a boss alone gives (battle_core_v4 §5 ボス専用の状態).</summary>
        public static bool IsBossOnly(StatusKind kind) => kind switch
        {
            StatusKind.MiasmaShroud => true,
            StatusKind.Binding => true,
            StatusKind.Hook => true,
            StatusKind.Depths => true,
            StatusKind.Rooting => true,
            StatusKind.Withering => true,
            _ => false,
        };

        /// <summary>
        /// roster §5.1 深み and §6.2 枯らし: what the turn-start recovery loses. 枯らし takes 1 a stack
        /// always; 深み 1 a stack when the holder starts the turn adjacent (N 0) to the nearest enemy.
        /// 疲労 is not counted here (battle_core_v4 §5: −1 while held). The floor 0 is the caller's.
        /// </summary>
        public static int RecoveryLoss(StatusSet statuses, bool adjacent)
        {
            if (statuses == null) throw new ArgumentNullException(nameof(statuses));
            return statuses.Stacks(StatusKind.Withering) + (adjacent ? statuses.Stacks(StatusKind.Depths) : 0);
        }

        /// <summary>
        /// roster §6.2 根張り, read at the holder's turn start from the last two turn ends: the same cell
        /// both times, and the holder did not move itself in the later turn. A push or a pull that left
        /// the holder elsewhere shows as another cell; one that the holder walked back from shows as a
        /// move (数え直し).
        /// </summary>
        public static bool StayedTwoTurns(IReadOnlyList<PlayerTurnEnd> turnEnds)
        {
            if (turnEnds == null) throw new ArgumentNullException(nameof(turnEnds));
            if (turnEnds.Count < 2) return false;
            var last = turnEnds[turnEnds.Count - 1];
            var before = turnEnds[turnEnds.Count - 2];
            return last.Cell > 0 && last.Cell == before.Cell && !last.Moved;
        }
    }
}
