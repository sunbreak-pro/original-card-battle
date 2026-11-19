// The height each enemy's figure stands on the 1920 x 1080 screen, when it is not the 226 / 264 px its
// cells give (battle-visual-v1 §4.3 大型の敵, #843). The height is the picture's, a look and not a rule,
// so it sits beside the art, keyed by the art id the script names (UnitFrame.ArtId, the roster's
// internal id, the folder under Assets/Art/Characters) and never in BattleCore. Both screens read it:
// the trial's FieldView and Depiction's FigureView stand the figure this tall with its feet on y 570.
// An enemy off this list keeps FigureFrame.HeightFor(cells). The values themselves are #590's to set;
// write the same height in the asset ledger's notes when the art goes in (asset-intake.md §1.2).
using System;
using System.Collections.Generic;

namespace Depiction
{
    public static class FigureHeights
    {
        private static readonly Dictionary<string, float> ByArtId = new Dictionary<string, float>(StringComparer.Ordinal)
        {
        };

        /// <summary>Every art id with a height of its own, and that height in px.</summary>
        public static IReadOnlyDictionary<string, float> All => ByArtId;

        /// <summary>The screen height filed for <paramref name="artId"/>, or 0 when it has none (an empty id has none).</summary>
        public static float Of(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return 0f;
            return ByArtId.TryGetValue(artId, out float height) ? height : 0f;
        }
    }
}
