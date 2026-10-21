// Port of unity-project-kit/Assets/View/Depiction/Script/StanceList.cs
// The stances each side holds, as the screen lists them at its edge (battle-visual-v1 §4.6 スタンス枠,
// battle_core_v4 §4 v4.4, battle_ui_ux_v2 §3.7, #455). Pure C#, so `dotnet test` holds the same order
// and the same places the screen draws.
//
// v4.4 has no single slot: every stance put down joins a list with no cap, and the same stance put
// down again works twice. battle-visual-v1 §4.6 still draws one 250×52 frame per side, so until
// design settles the list's shape (the conflict handed off as 2026-10-11-battle-conflict-stance-list-
// shape) each stance takes one such frame, stacked upward from the slot §4.6 gives.
//
// Port note: every C# `float` is a single (float32 constants, Math.fround on each float operation).
import { StanceRow, UnitSide } from './DepictionTypes.js';

const f = Math.fround;

const RefWidth = f(1920);

export const StanceList = {
  /** §4.6: each frame is 250×52, 24 px in from its side's edge, its top at y 470. */
  Width: f(250),
  RowHeight: f(52),
  Edge: f(24),
  SlotTop: f(470),

  /** The gap between two stacked frames: 6 px from the §3.2 spacing scale. */
  RowGap: f(6),

  /**
   * At most this many frames per side (the top one at y 354). A longer list folds its oldest
   * stances into one "+n" frame on top, as the status chips fold theirs (§7.3), so the newest
   * stance always shows.
   */
  MaxRows: 3,

  /**
   * The rows of a side's list from the stance names in the order they were put down, a repeat
   * listed again (CombatantState.StanceList). battle_ui_ux_v2 §3.7: in the order put down, the
   * newest last; a stance put down again is not listed twice but moves to the end with its
   * count raised, so the newest stance is always the last row. (List<StanceRow>)
   */
  Rows(placed) {
    const rows = [];
    if (placed == null) return rows;
    for (const name of placed) {
      if (name == null || name.length === 0) continue;
      const at = rows.findIndex((r) => r.Name === name);
      let count = 1;
      if (at >= 0) {
        count = rows[at].Count + 1;
        rows.splice(at, 1);
      }
      rows.push(new StanceRow({ Name: name, Count: count }));
    }
    return rows;
  },

  /** How many of the oldest rows fold into the "+n" frame: none while the list fits. */
  Folded(rows) {
    return rows > StanceList.MaxRows ? rows - (StanceList.MaxRows - 1) : 0;
  },

  /** How many frames a list of `rows` takes: one empty frame for none. */
  Frames(rows) {
    return Math.max(1, Math.min(rows, StanceList.MaxRows));
  },

  /** The left edge of `side`'s frames on the 1920×1080 screen. */
  LeftOf(side) {
    // RefWidth - Edge - Width is a C# constant (1646).
    return side === UnitSide.Player ? StanceList.Edge : f(f(RefWidth - StanceList.Edge) - StanceList.Width);
  },

  /** The top of the frame `fromBottom` places above the §4.6 slot (0 is the slot itself). */
  TopOf(fromBottom) {
    // (RowHeight + RowGap) is a C# constant (58).
    return f(StanceList.SlotTop - f(f(fromBottom) * f(StanceList.RowHeight + StanceList.RowGap)));
  },

  /** The "×2" beside a stance put down more than once; nothing for one (§7.3 永続). */
  CountText(count) {
    return count > 1 ? '×' + count : '';
  },
};
