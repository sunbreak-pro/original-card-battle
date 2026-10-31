// Port of unity-project-kit/Assets/View/Depiction/Script/RefusalLineLayout.cs
// Where the reason a card went back stands, and how big it is (battle-visual-v1 §5.4, #455). Pure C#,
// so `dotnet test` holds the same numbers the screen draws.
//
// Port note: every C# `float` is a single (float32 constants, Math.fround on each float operation).
const f = Math.fround;

/**
 * battle-visual-v1 §5.4 出せなかった理由: the one line above the hand that says why a released card
 * went back, for 3.5 s. 48 px tall, centred on the hand's middle (x 960), its bottom 12 px above the
 * resting hand's top (y 896, §4.8).
 */
export const RefusalLineLayout = {
  Height: f(48),
  CenterX: f(960),
  Bottom: f(884),
  get Top() {
    return f(RefusalLineLayout.Bottom - RefusalLineLayout.Height);
  },

  /** §5.4: an amber dot at 16 px on the left and the text at 20 px, 16 px in from either end, 10 px apart. */
  DotSize: f(16),
  TextSize: 20,
  Pad: f(16),
  DotGap: f(10),

  /** §5.4: how long the line stays. */
  Seconds: f(3.5),

  /** The line's width for a text `textWidth` px wide. */
  WidthFor(textWidth) {
    const r = RefusalLineLayout;
    // (Pad + DotSize + DotGap) is a C# constant (42).
    return f(f(f(f(r.Pad + r.DotSize) + r.DotGap) + Math.max(f(0), f(textWidth))) + r.Pad);
  },

  /** The line's left edge on the 1920×1080 screen, centred on the hand. */
  LeftFor(textWidth) {
    return f(RefusalLineLayout.CenterX - f(RefusalLineLayout.WidthFor(textWidth) * 0.5));
  },
};
