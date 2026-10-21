// Port of unity-project-kit/Assets/View/Depiction/Script/EnemyTurnShadeLayout.cs
// What the screen does on the enemy's turn below the throw line (battle-visual-v1 §5.5, #455). Pure C#,
// so `dotnet test` holds the same numbers the screen draws.
//
// Port note: C# `float` constants are their float32 values (Math.fround), so BottomAlpha is
// fround(0.72) and EndTurnAlpha fround(0.4), as in the C#.
const f = Math.fround;

export const EnemyTurnShadeLayout = {
  /** §5.5: from y 790 down, the screen sinks under black. */
  Top: f(790),

  /** §5.5 黒 72% のグラデーション: clear at y 790, black 72% at the bottom edge. */
  TopAlpha: f(0),
  BottomAlpha: f(0.72),

  /** §4.9 ターン終了: the plate stands at 40% while the enemy acts. */
  EndTurnAlpha: f(0.4),

  /** How tall the shade is on the 1080 screen. */
  get Height() {
    return f(1080 - EnemyTurnShadeLayout.Top);
  },
};
