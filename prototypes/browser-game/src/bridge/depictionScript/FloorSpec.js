// Port of unity-project-kit/Assets/View/Depiction/Script/FloorSpec.cs
// Where the floor's cells stand on the 1920×1080 screen (battle-visual-v1 §4.2, #242). Pure
// arithmetic with no engine types, so the EditMode tests hold it directly, as they hold HandSpec.
// This is layout, not a rule of the game: which cells exist and who stands where is the script's.
//
// Port notes:
// - Every C# `float` is a single: constants are float32 values and every float operation is rounded
//   with Math.fround (an int operand is converted to float first, as C# does).
// - GapSpan's two out parameters are returned as `{ from, to }`.
const f = Math.fround;

export const FloorSpec = {
  /** §4.2: the band runs x 300〜1620 (1320 px) and is 40 px tall. */
  Left: f(300),
  Right: f(1620),
  Height: f(40),

  /** §4.2: each cell leaves 3 px free on its left and right. */
  Inset: f(3),

  /** §4.2 / §4.3: the floor's top is at y 560 and the feet at y 570. */
  FeetBelowTop: f(10),

  /** §4.2 間合いの数字: at gap 0 the tag is 100 px wide over the border of the two cells. */
  TagAtZero: f(100),

  /** The width the band gives one cell: 220 px for 6, 165 for 8, 264 for 5. */
  CellWidth(cells) {
    // (Right - Left) is a C# constant (1320).
    return f(f(FloorSpec.Right - FloorSpec.Left) / f(Math.max(1, cells)));
  },

  /** The left edge of cell `cell` (1-based) as drawn, its 3 px inset taken off. */
  TileLeft(cells, cell) {
    return f(f(FloorSpec.Left + f(f(cell - 1) * FloorSpec.CellWidth(cells))) + FloorSpec.Inset);
  },

  /** The width of a cell as drawn: its share of the band less 3 px on each side. */
  TileWidth(cells) {
    // Inset * 2f is a C# constant (6).
    return f(FloorSpec.CellWidth(cells) - f(FloorSpec.Inset * 2));
  },

  /** §4.3: a figure stands in the middle of the cells it takes. */
  FeetX(cells, cell, size) {
    const across = f(f(cell - 1) + f(f(Math.max(1, size)) * 0.5));
    return f(FloorSpec.Left + f(across * FloorSpec.CellWidth(cells)));
  },

  /**
   * §4.2 間合いの数字: the empty cells between the player's front edge and the enemy's near edge,
   * as screen x. At gap 0 both are the border the two share. Returns `{ from, to }` (C#: two out floats).
   */
  GapSpan(cells, playerCell, playerSize, enemyCell) {
    const width = FloorSpec.CellWidth(cells);
    const from = f(FloorSpec.Left + f(f(playerCell + Math.max(1, playerSize) - 1) * width));
    const to = f(FloorSpec.Left + f(f(enemyCell - 1) * width));
    return { from, to };
  },
};
