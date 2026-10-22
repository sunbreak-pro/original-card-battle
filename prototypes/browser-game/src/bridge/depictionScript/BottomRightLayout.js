// Port of unity-project-kit/Assets/View/Depiction/Script/BottomRightLayout.cs
// The lower right of the battle screen (battle-visual-v1 §4.9, #455): the four frames of the attributes
// played this turn, and the 構えの予告 left of the end-turn plate. Pure C#, so `dotnet test` holds the
// same numbers the screen draws.
//
// Port notes: C# `float` members are float32 values (Math.fround), and each float operation is
// rounded the same way. Static properties are getters.
import { ArgumentOutOfRangeException } from '../../shared/cs.js';
import { CardKind } from './DepictionTypes.js';

const f = Math.fround;

/** new ArgumentOutOfRangeException(paramName) with .NET's message. */
function argRange(name) {
  const e = new ArgumentOutOfRangeException(`Specified argument was out of the range of valid values. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

const RefWidth = f(1920);

export const PlayedLayout = {
  /** §4.9 出した属性: 攻撃・防御・スキル・スタンス, left to right. */
  Order: [CardKind.Attack, CardKind.Guard, CardKind.Skill, CardKind.Stance],

  /** §4.9: 40×40 frames, 6 px apart, the last one 24 px in from the right, their tops at y 896. */
  Size: f(40),
  Gap: f(6),
  Right: f(24),
  Top: f(896),

  /** §4.9: the icon in a frame (§6.1's 属性アイコン size). */
  IconSize: f(24),

  /** §4.9 回数の札: a 20 px circle at the frame's top right, its number at 14 px. */
  BadgeSize: f(20),
  BadgeText: 14,

  /** §4.9: an attribute not played this turn is steel at 55%. */
  UnplayedAlpha: f(0.55),

  /** The span of the four frames. */
  get SpanWidth() {
    const n = PlayedLayout.Order.length;
    return f(f(n * PlayedLayout.Size) + f((n - 1) * PlayedLayout.Gap));
  },

  // ---- battle_ui_ux_v2 §1.1 B6 / §3.6 (f) 追撃のバッジ (#455) ----

  /**
   * B6: while a 追撃 waits, its "+5" stands at the row's left end — one more 40×40 frame, a gap to
   * the left of 攻撃's. It grows to the right of nothing: a wider number widens it leftwards.
   */
  get FollowUpRight() {
    return f(PlayedLayout.LeftOf(0) - PlayedLayout.Gap);
  },

  /** The badge's number: 24 px (§3.1 の HP・Guard・敵のスタミナの段), in phosphor, the colour of power (§8 威力). */
  FollowUpText: 24,

  /** The padding either side of a number wider than the 40 px frame. */
  FollowUpPad: f(6),

  /** §3.6 (f): the badge pops in 150 ms when the 追撃 fires and goes in 150 ms when it is spent or the turn ends. */
  FollowUpPopMs: f(150),
  FollowUpVanishMs: f(150),

  /** "+5" for a 追撃 of 5 waiting; "" when none waits. */
  FollowUpLabel(followUp) {
    return followUp > 0 ? '+' + followUp : '';
  },

  /** The left edge of the `index`-th frame on the 1920×1080 screen. */
  LeftOf(index) {
    if (index < 0 || index >= PlayedLayout.Order.length) throw argRange('index');
    // RefWidth - Right - SpanWidth + index * (Size + Gap); (Size + Gap) is a C# constant (46).
    return f(f(f(RefWidth - PlayedLayout.Right) - PlayedLayout.SpanWidth) + f(index * f(PlayedLayout.Size + PlayedLayout.Gap)));
  },
};

export const ReserveHintLayout = {
  /** §4.9 構えの予告: from x 1560, y 984, 56 tall. */
  Left: f(1560),
  Top: f(984),
  Height: f(56),

  /** The padding either side: 12 px from the §3.2 spacing scale. */
  Pad: f(12),

  /** §4.9: the shield at 30 px and "+3" at 24 px, 8 px apart. */
  ShieldSize: f(30),
  TextGap: f(8),
  TextSize: 24,
  TextWidth: f(36),

  /** Wide enough for the shield and a two-character "+3"; it ends 12 px short of the end-turn plate (x 1672). */
  get Width() {
    const r = ReserveHintLayout;
    return f(f(f(f(r.Pad + r.ShieldSize) + r.TextGap) + r.TextWidth) + r.Pad);
  },
};
