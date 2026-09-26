using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// §5: the statuses one combatant carries, as word → stack count. Immutable: every change hands
    /// back a new set, so a battle state can be compared with the one before it.
    ///
    /// A word with zero stacks is not held at all, which is what keeps the kind count honest.
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
                if (entry.Stacks > 0) map[entry.Kind] = entry.Stacks;
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
        /// </summary>
        public StatusSet Add(StatusKind kind, int stacks = Constants.StatusApplyDefault, int? kindLimit = null)
        {
            if (stacks <= 0) return this;
            bool isNewKind = !_stacks.ContainsKey(kind);
            if (isNewKind && kindLimit.HasValue && _stacks.Count >= kindLimit.Value) return this;

            var map = ToDictionary();
            map[kind] = Stacks(kind) + stacks;
            return new StatusSet(map);
        }

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
            StatusKind.Bleed => StatusDecay.OnTurn,
            StatusKind.Fatigue => StatusDecay.OnTurn,
            StatusKind.Regen => StatusDecay.OnTurn,
            StatusKind.Fragile => StatusDecay.OnUse,
            StatusKind.Intimidate => StatusDecay.OnUse,
            StatusKind.Empower => StatusDecay.OnUse,
            StatusKind.Focus => StatusDecay.OnUse,
            StatusKind.Parry => StatusDecay.OnUse,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown status."),
        };

        /// <summary>§5 向き: whether the word is one a side puts on itself (強化 / 集中 / 見切り / 再生).</summary>
        public static bool IsOwn(StatusKind kind) => kind switch
        {
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
    }
}
