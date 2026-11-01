using System;

namespace BattleCore
{
    /// <summary>
    /// Trial extension (port of the browser trial's DeckRules.js:240-325, docs/EXTENSIONS.md 「出立の枠
    /// （DeckLimits）」 L-1〜L-9): the numbers one departure is checked against — the deck's bounds and the
    /// two kinds of slots. Without it the core has them as constants only (<see cref="Constants.DeckMin"/> /
    /// <see cref="Constants.DeckMax"/>, <see cref="DeckRules.ToolSlots"/> / <see cref="DeckRules.ConsumableSlots"/>);
    /// the design raises two of them for one life (#104 / #108).
    ///
    /// Opt-in: every check that takes one (<see cref="Cards.Validate"/>, <see cref="DeckRules.ValidateDeck"/>,
    /// <see cref="DeckRules.CanAdd"/>, <see cref="DeckRules.Validate"/>) reads a null as <see cref="Default"/>,
    /// the constants, so a call that leaves it out behaves exactly as before.
    ///
    /// Sources (under the repo's .claude/docs/):
    /// - vision/concept-v3.md §14 デッキ枚数 「下限 20・上限 40」, ツールの枠 「共通 3 枠」, 資源・アイテム
    ///   「消耗品 3 枠」: the defaults, equal to the constants (<see cref="Default"/>).
    /// - vision/concept-v3.md §8.2, §12-12 and §14 生存者ボーナス 「出立の枠を増やす。ツール枠 +1、デッキ上限
    ///   +5 を次の 1 生だけ」: a survivor's heir sets out with 4 tool slots and a deck of up to 45. Which life
    ///   has the bonus is the lineage's (InheritanceCore); its LifeStart hands the four numbers over.
    /// - The consumables keep 3 slots and the deck keeps its floor of 20 and 3 of a kind: the bonus names
    ///   only the tools and the cap (concept-v3.md §14).
    ///
    /// Immutable. A number left out (null) takes the default's. DeckMin 1 or more, DeckMax not under
    /// DeckMin, slots 0 or more; anything else is an <see cref="ArgumentOutOfRangeException"/> (L-9).
    /// </summary>
    public sealed class DeckLimits : IEquatable<DeckLimits>
    {
        // js: core/battle/DeckRules.js:274
        public DeckLimits(int? deckMin = null, int? deckMax = null, int? toolSlots = null, int? consumableSlots = null)
        {
            // The JS reads its four keys in this order and refuses the first bad one; the parameter names
            // in the messages are the JS's keys (DeckRules.js:248 limitOutOfRange).
            DeckMin = Checked("DeckMin", deckMin ?? Constants.DeckMin, 1);
            DeckMax = Checked("DeckMax", deckMax ?? Constants.DeckMax, 0);
            ToolSlots = Checked("ToolSlots", toolSlots ?? DeckRules.ToolSlots, 0);
            ConsumableSlots = Checked("ConsumableSlots", consumableSlots ?? DeckRules.ConsumableSlots, 0);
            if (DeckMax < DeckMin)
            {
                throw new ArgumentOutOfRangeException("DeckMax", DeckMax, "Not under DeckMin (" + DeckMin + ").");
            }
        }

        /// <summary>The fewest cards a deck may hold (§8: 20).</summary>
        public int DeckMin { get; }

        /// <summary>The most cards a deck may hold (§8: 40; 45 with the survivor's bonus).</summary>
        public int DeckMax { get; }

        /// <summary>Tool slots (concept-v3.md §14: 3; 4 with the survivor's bonus).</summary>
        public int ToolSlots { get; }

        /// <summary>Consumable slots (concept-v3.md §14: 3).</summary>
        public int ConsumableSlots { get; }

        /// <summary>
        /// The constants: a deck of 20〜40, 3 tool slots, 3 consumable slots. What every check uses when no
        /// limits are given. One shared instance.
        /// </summary>
        // js: core/battle/DeckRules.js:296
        public static DeckLimits Default { get; } = new DeckLimits();

        /// <summary>
        /// The limits to check against: <paramref name="limits"/> as it is, null as <see cref="Default"/>.
        /// (The JS also reads any object by its four keys, such as Inheritance.startLife's LifeStart; in C#
        /// the caller builds a DeckLimits from those numbers with the constructor.)
        /// </summary>
        // js: core/battle/DeckRules.js:304
        public static DeckLimits From(DeckLimits? limits)
        {
            return limits ?? Default;
        }

        /// <summary>True when every number is the constants': the departure is checked exactly as without limits.</summary>
        // js: core/battle/DeckRules.js:311
        public bool IsDefault => Equals(Default);

        /// <summary>A copy with some numbers changed (a null keeps this one's).</summary>
        // js: core/battle/DeckRules.js:317
        public DeckLimits With(int? deckMin = null, int? deckMax = null, int? toolSlots = null, int? consumableSlots = null)
        {
            return new DeckLimits(deckMin ?? DeckMin, deckMax ?? DeckMax, toolSlots ?? ToolSlots, consumableSlots ?? ConsumableSlots);
        }

        /// <summary>Same four numbers.</summary>
        // js: core/battle/DeckRules.js:322
        public bool Equals(DeckLimits? other)
        {
            return other is not null
                && DeckMin == other.DeckMin
                && DeckMax == other.DeckMax
                && ToolSlots == other.ToolSlots
                && ConsumableSlots == other.ConsumableSlots;
        }

        public override bool Equals(object? obj) => Equals(obj as DeckLimits);

        public override int GetHashCode() => HashCode.Combine(DeckMin, DeckMax, ToolSlots, ConsumableSlots);

        public override string ToString() =>
            "DeckLimits { DeckMin = " + DeckMin + ", DeckMax = " + DeckMax + ", ToolSlots = " + ToolSlots
            + ", ConsumableSlots = " + ConsumableSlots + " }";

        // js: core/battle/DeckRules.js:283-286 (the check in the constructor's loop) and :248 (limitOutOfRange)
        private static int Checked(string key, int value, int least)
        {
            if (value < least)
            {
                throw new ArgumentOutOfRangeException(key, value,
                    least == 1 ? "A whole number, 1 or more." : "A whole number, 0 or more.");
            }
            return value;
        }
    }
}
