// The words and the share of the pile lists' drawer (battle_ui_ux_v2 §1.1 C4, #455). Pure C#, so the
// EditMode tests hold them; PileListView only prints these.
using System.Collections.Generic;
using System.Text;

namespace Depiction
{
    public static class PileList
    {
        /// <summary>C4 左からのドロワー（手記の鏡像）: the journal drawer takes the right 30%, this one the left 30%.</summary>
        public const float WidthShare = 0.30f;

        /// <summary>The rows' text size (battle-visual-v1 §3.1: 18 px for a panel's heading-sized line).</summary>
        public const int RowSize = 18;

        /// <summary>The close button: Esc closes the drawer too (battle_ui_ux_v2 §3.4).</summary>
        public const string CloseLabel = "閉じる（Esc）";

        /// <summary>What a list with no card in it reads.</summary>
        public const string Empty = "なし";

        /// <summary>C4's tabs: 山札 / 捨札 / 除外. A pile kind with no word stops here rather than reading 山札 (#654).</summary>
        public static string TabLabel(PileKind pile)
        {
            switch (pile)
            {
                case PileKind.Draw: return "山札";
                case PileKind.Discard: return "捨札";
                case PileKind.Exile: return "除外";
                default: throw new System.ArgumentOutOfRangeException(nameof(pile), pile, "No tab word for the pile " + pile);
            }
        }

        /// <summary>One row per kind ("突き 2 / 3"), in the order given; <see cref="Empty"/> for none.</summary>
        public static string BodyText(IReadOnlyList<PileCard> rows)
        {
            if (rows == null || rows.Count == 0) return Empty;
            var text = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append(rows[i].RowText);
            }
            return text.ToString();
        }
    }
}
