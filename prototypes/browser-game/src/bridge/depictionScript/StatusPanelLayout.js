// Port of unity-project-kit/Assets/View/Depiction/Script/StatusPanelLayout.cs
// Where the two number panels sit under the figures (battle-visual-v1 §4.5, #455). Pure arithmetic
// with no engine types, like CornerLayout and FloorSpec, so the EditMode tests hold it directly.
// Boxes are in pixels on the 1920 x 1080 reference canvas, y counted downwards from the top.
//
// §4.5 puts each panel under its own figure, centred on it, 24 px inside the screen's edges. At a
// gap of 0 the two figures stand one cell apart (165 px on an 8-cell line), which is less than half
// of the two panels together, so centring alone would lay them over each other. This spreads the
// pair apart evenly about their middle until there is a gap between them, and keeps both inside
// the edges. The player always stands left of the enemies, so the player's panel is kept on the left.
//
// Port notes:
// - Every C# `float` is a single: float parameters are rounded on entry, constants are float32
//   values and each float operation is rounded with Math.fround.
// - The out parameters are returned as objects: Place → `{ playerLeft, enemyLeft }`,
//   OnFloor → `{ player, enemy }` (two ScreenBox).
import { ArgumentOutOfRangeException } from '../../shared/cs.js';
import { CornerLayout, ScreenBox } from './CornerLayout.js';
import { FloorSpec } from './FloorSpec.js';

const f = Math.fround;

/** new ArgumentOutOfRangeException(paramName, message) with .NET's message. */
function argRangeMessage(name, message) {
  const e = new ArgumentOutOfRangeException(`${message} (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

function Clamp(value, min, max) {
  if (max < min) return min;
  return value < min ? min : value > max ? max : value;
}

const PadTop = f(8);
const HpRowHeight = f(24);
const RowGap = f(8);
const GuardRowHeight = f(28);

export const StatusPanelLayout = {
  /** §4.5: the panels' top edge. */
  Top: f(614),

  /** §4.5: the player's panel is 300 wide. */
  PlayerWidth: f(300),

  /** §4.5: an enemy's panel is 210 wide, a 2-cell enemy's 272. */
  EnemyWidth: f(210),
  LargeEnemyWidth: f(272),

  /** §4.5: an enemy's panel is no wider than the cells it stands on, less 10 px. */
  CellMargin: f(10),

  /** §4.5: kept 24 px inside the screen's left and right edges. */
  EdgeInset: f(24),

  /** The least space left between the two panels when they are pushed apart (§3.2's 8 px step). */
  MinGap: f(8),

  // §4.5 inside the panel: padding 8 above, 10 at the sides and below, 8 between rows.
  PadTop,
  PadSide: f(10),
  PadBottom: f(10),
  RowGap,

  /** 1 行目: the HP number is 24 px, the tallest thing on the row (heart 20, bar 14). */
  HpRowHeight,

  /** 2 行目: the Guard shield is 28 px, the tallest thing on the row. */
  GuardRowHeight,

  /** 3 行目: a status chip is 30 px tall (§7.3), and wrapped rows are 5 px apart. */
  ChipHeight: f(30),
  ChipGap: f(5),

  /**
   * The width of an enemy's panel: 210, or 272 for an enemy that takes two cells or more, and
   * never more than its cells' width on a line of `cells` less 10 px. With no floor (`cells` 0) the
   * cell cap does not apply.
   */
  EnemyPanelWidth(cells, size) {
    const taken = Math.max(1, size);
    const width = taken >= 2 ? StatusPanelLayout.LargeEnemyWidth : StatusPanelLayout.EnemyWidth;
    if (cells <= 0) return width;
    return Math.min(width, f(f(f(taken) * FloorSpec.CellWidth(cells)) - StatusPanelLayout.CellMargin));
  },

  /** The room a panel of `width` leaves for its rows inside the padding. */
  ContentWidth(width) {
    // 2f * PadSide is a C# constant (20).
    return Math.max(f(0), f(f(width) - f(2 * StatusPanelLayout.PadSide)));
  },

  /** The y (from the panel's top) of the chip row. A C# constant (76). */
  ChipRowTop: f(f(f(f(PadTop + HpRowHeight) + RowGap) + GuardRowHeight) + RowGap),

  /**
   * A panel's height with `chipRows` rows of status chips: the HP row and the Guard row always,
   * the chip rows when there are any, inside the padding.
   */
  Height(chipRows) {
    const s = StatusPanelLayout;
    // PadTop + HpRowHeight + RowGap + GuardRowHeight + PadBottom is a C# constant (78).
    let height = f(f(f(f(s.PadTop + s.HpRowHeight) + s.RowGap) + s.GuardRowHeight) + s.PadBottom);
    if (chipRows > 0) height = f(height + f(f(s.RowGap + f(f(chipRows) * s.ChipHeight)) + f(f(chipRows - 1) * s.ChipGap)));
    return height;
  },

  /**
   * The left edges of the two panels, the player's centred on `playerX` and the enemy's on `enemyX`
   * (screen x of each figure's middle) where there is room. Each is kept 24 px inside the edges;
   * when they would come closer than MinGap, both move apart by the same amount, and one stopped by
   * an edge hands the rest to the other. The player's panel stays on the left. Whatever the two x
   * are, the panels never overlap as long as both fit side by side between the edges.
   * Returns `{ playerLeft, enemyLeft }` (C#: two out floats).
   */
  Place(playerX, playerWidth, enemyX, enemyWidth) {
    playerX = f(playerX);
    playerWidth = f(playerWidth);
    enemyX = f(enemyX);
    enemyWidth = f(enemyWidth);
    if (playerWidth < 0 || enemyWidth < 0) throw argRangeMessage('playerWidth', 'A panel has no negative width.');
    const s = StatusPanelLayout;
    const lowest = s.EdgeInset;
    // CornerLayout.CanvasWidth - EdgeInset is a C# constant (1896).
    const inner = f(CornerLayout.CanvasWidth - s.EdgeInset);
    const playerHighest = f(inner - playerWidth);
    const enemyHighest = f(inner - enemyWidth);
    let playerLeft = Clamp(f(playerX - f(playerWidth * 0.5)), lowest, playerHighest);
    let enemyLeft = Clamp(f(enemyX - f(enemyWidth * 0.5)), lowest, enemyHighest);

    const shortBy = f(f(f(playerLeft + playerWidth) + s.MinGap) - enemyLeft);
    if (shortBy <= 0) return { playerLeft, enemyLeft };
    playerLeft = f(playerLeft - f(shortBy * 0.5));
    enemyLeft = f(enemyLeft + f(shortBy * 0.5));
    if (playerLeft < lowest) {
      enemyLeft = f(enemyLeft + f(lowest - playerLeft));
      playerLeft = lowest;
    }
    if (enemyLeft > enemyHighest) {
      playerLeft = f(playerLeft - f(enemyLeft - enemyHighest));
      enemyLeft = enemyHighest;
    }
    return { playerLeft, enemyLeft };
  },

  /**
   * The two panels for figures standing on a line of `cells` (§4.2): each figure stands in the
   * middle of its cells (FloorSpec.FeetX), the enemy's panel is as wide as EnemyPanelWidth allows,
   * and both are `height` tall at y 614. Returns `{ player, enemy }` (C#: two out ScreenBox).
   */
  OnFloor(cells, playerCell, playerSize, enemyCell, enemySize, height) {
    const s = StatusPanelLayout;
    const enemyWidth = s.EnemyPanelWidth(cells, enemySize);
    const { playerLeft, enemyLeft } = s.Place(FloorSpec.FeetX(cells, playerCell, playerSize), s.PlayerWidth,
      FloorSpec.FeetX(cells, enemyCell, enemySize), enemyWidth);
    const player = new ScreenBox(playerLeft, s.Top, s.PlayerWidth, height);
    const enemy = new ScreenBox(enemyLeft, s.Top, enemyWidth, height);
    return { player, enemy };
  },
};
