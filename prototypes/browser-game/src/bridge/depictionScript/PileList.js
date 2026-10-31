// Port of unity-project-kit/Assets/View/Depiction/Script/PileList.cs
// The words and the share of the pile lists' drawer (battle_ui_ux_v2 §1.1 C4, #455). Pure C#, so the
// EditMode tests hold them; PileListView only prints these.
//
// Port note: WidthShare is a C# float, held as its float32 value (fround(0.30)).
import { PileKind } from './PileLayout.js';

export const PileList = {
  /** C4 左からのドロワー（手記の鏡像）: the journal drawer takes the right 30%, this one the left 30%. */
  WidthShare: Math.fround(0.30),

  /** The rows' text size (battle-visual-v1 §3.1: 18 px for a panel's heading-sized line). */
  RowSize: 18,

  /** The close button: Esc closes the drawer too (battle_ui_ux_v2 §3.4). */
  CloseLabel: '閉じる（Esc）',

  /** What a list with no card in it reads. */
  Empty: 'なし',

  /** C4's tabs: 山札 / 捨札 / 除外. */
  TabLabel(pile) {
    switch (pile) {
      case PileKind.Discard: return '捨札';
      case PileKind.Exile: return '除外';
      default: return '山札';
    }
  },

  /** One row per kind ("突き 2 / 3"), in the order given; Empty for none. (rows: IReadOnlyList<PileCard>) */
  BodyText(rows) {
    if (rows == null || rows.length === 0) return PileList.Empty;
    let text = '';
    for (let i = 0; i < rows.length; i++) {
      if (i > 0) text += '\n';
      const row = rows[i].RowText;
      text += row == null ? '' : row;
    }
    return text;
  },
};
