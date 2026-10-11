// Where the three piles stand and how thick each looks (battle-visual-v1 §4.7, #455). Pure C#, so
// `dotnet test` holds the same numbers the screen draws.
using System;

namespace Depiction
{
    /// <summary>The three piles, left to right.</summary>
    public enum PileKind
    {
        Draw,
        Discard,
        Exile,
    }

    public static class PileLayout
    {
        /// <summary>§4.7 束 3 つ: from x 32, y 948, each 64×84, 22 px apart.</summary>
        public const float Left = 32f;
        public const float Top = 948f;
        public const float Width = 64f;
        public const float Height = 84f;
        public const float Gap = 22f;

        /// <summary>§4.7: the pile's icon is 28 px.</summary>
        public const float IconSize = 28f;

        /// <summary>§4.7: each sheet behind the front one shows 4 px more of the pile's thickness.</summary>
        public const float SheetStep = 4f;

        /// <summary>
        /// At most this many sheets behind the front one (12 px). The stamina tag above ends at y 930,
        /// 18 px over the piles, so a thicker stack would reach it.
        /// </summary>
        public const int MaxSheetsBehind = 3;

        /// <summary>One more sheet behind the front one for every this many cards past the first.</summary>
        public const int CardsPerSheet = 5;

        /// <summary>The left edge of <paramref name="pile"/> on the 1920×1080 screen.</summary>
        public static float LeftOf(PileKind pile)
        {
            return Left + (int)pile * (Width + Gap);
        }

        /// <summary>The three together span the stamina tag's 236 px, as §4.7 lines them up under it.</summary>
        public static float SpanWidth => 3f * Width + 2f * Gap;

        /// <summary>
        /// How many sheets lie behind the front card of a pile of <paramref name="count"/> cards: none
        /// for 0 or 1, then one more for every 5 cards (2〜6 → 1, 7〜11 → 2, 12 and more → 3).
        /// §4.7 says the thickness follows the count 4 px at a time without saying how many cards a
        /// step holds; this is the battle lane's reading.
        /// </summary>
        public static int SheetsBehind(int count)
        {
            if (count <= 1) return 0;
            return Math.Min(MaxSheetsBehind, 1 + (count - 2) / CardsPerSheet);
        }

        /// <summary>A pile with no card is drawn as an empty slot: a steel dashed frame, no sheet (§3.2 破線 for 空き).</summary>
        public static bool IsEmpty(int count)
        {
            return count <= 0;
        }

        /// <summary>The number of cards in <paramref name="pile"/> on <paramref name="piles"/>.</summary>
        public static int CountOf(PileFrame piles, PileKind pile)
        {
            if (piles == null) return 0;
            switch (pile)
            {
                case PileKind.Draw: return piles.Draw;
                case PileKind.Discard: return piles.Discard;
                case PileKind.Exile: return piles.Exile;
                default: throw new ArgumentOutOfRangeException(nameof(pile), pile, null);
            }
        }
    }
}
