// Port of unity-port/unity-project-kit/Assets/View/Backdrop/Script/MiasmaLook.cs
//
// How the miasma shows on the exploration screen (battle-visual-v1 §2.5, #90): the gauge in the top band
// (dungeon_exploration_v4 §6.2: ten ticks, a line every 20%, the next step drawn faintly ahead) and the
// shade that closes in on the screen as the reading climbs. Pure functions of the reading and the time,
// so the same moment always draws the same picture and `dotnet test` can pin them.
//
// The colours follow world-v1 §3 rule 1 (the thicker the miasma, the deeper it leans to violet) and
// battle-visual-v1 §2.1 (the miasma icon is phosphor; amber stays the player's; 注意 always comes with △).
// The readings the rules hang on (death at 100, a max-stamina line every 20, no further drop past 80) are
// DungeonCore's Miasma; this assembly does not reference it, so they are restated here and the tests pin
// them to concept-v3 §6 and seven_layers_v4 §3.
//
// Floats as in BackdropSpec.js (Math.fround after every C# float operation; float parameters rounded on
// entry). The two C# overloads ShadeAt(float) and ShadeAt(float, double) are one JS method: leave `ms`
// out (undefined) for the first. System.Math.Pow is CrtPow.Pow (glibc's pow, which the oracle calls).
import { defineRecord, defineEnum, roundEven } from '../../shared/cs.js';
import { BackdropSpec, Rgba } from './BackdropSpec.js';
import { Pow } from './CrtPow.js';

const f = Math.fround;

/** What the faint share ahead of the gauge warns of. */
export const MiasmaWarning = defineEnum([
  /** The next step only adds to the reading. */
  'None',
  /** The next step crosses one of the 20% lines, where max stamina drops. */
  'StepLine',
  /** The next step reaches 100% and ends the life. */
  'Death',
]);

/** How one tick of the gauge is drawn. Height is a share of the gauge's height. */
export class GaugeTick extends defineRecord('GaugeTick', ['Percent', 'IsStepLine', 'Height']) {
  constructor(percent, isStepLine, height) {
    super(percent, isStepLine, f(height));
  }
}

/**
 * How far the shade has closed in at one reading. Alphas are of MiasmaLook.ShadeColor
 * except SeepAlpha, which is of MiasmaLook.SeepColor.
 *
 * Strength: 0 at a clear 0% to 1 at 100%. VeilAlpha: laid evenly over the whole screen. EdgeAlpha:
 * added at the corners, falling to nothing at InnerRadius. InnerRadius: where the darkening of the
 * edges starts, as a share of the half diagonal from the centre. SeepAlpha: the violet glow that seeps
 * in at the corners, on the same falloff as the edges.
 */
export class MiasmaShade extends defineRecord('MiasmaShade', ['Strength', 'VeilAlpha', 'EdgeAlpha', 'InnerRadius', 'SeepAlpha']) {
  constructor(strength, veilAlpha, edgeAlpha, innerRadius, seepAlpha) {
    super(f(strength), f(veilAlpha), f(edgeAlpha), f(innerRadius), f(seepAlpha));
  }
}

/** The reading that ends the life (concept-v3 §6). */
const DeathAt = 100;
/** Max stamina drops by one at every multiple of this (concept-v3 §6). */
const StepPercent = 20;
/** The last line that drops max stamina; past it the reading only counts down to death (seven_layers_v4 §3). */
const LastStepLine = 80;

/** 燐光 (§2.1): the fill of a thin reading. */
const FillThin = Rgba.Hex('#b9a3ff');
/** 菫 (§2.1): the fill of a thick reading. */
const FillThick = Rgba.Hex('#8b6fd0');
/** 注意 (§2.1): the share ahead when the next step costs more than miasma. Always with △. */
const Caution = Rgba.Hex('#e6c35c');

/** The share ahead's alpha when it only warns of more miasma. */
const AheadAlpha = f(0.4);
/** The share ahead's alpha when it crosses a line. */
const AheadStepAlpha = f(0.7);

/** The mark the forecast label starts with when the share ahead is 注意 (§2.1: 注意 always with △). */
const CautionMark = '△ ';

/** How long the fill takes to climb to a new reading. */
const RiseMs = 500;

/** From this reading the fill and the shade breathe: only death is left ahead (seven_layers_v4 §3). */
const BreathFrom = LastStepLine;
const BreathSlowMs = 2400;
const BreathFastMs = 1200;
/** How far the fill's alpha dips at the bottom of a breath. */
const BreathDepth = 0.25;

/** 紺 (world-v1 §6), darker than the darkest sky: the shade's colour. */
const ShadeColor = Rgba.Hex('#05060f');
/** 菫 (§2.1): the miasma's own glow seeping in at the corners. */
const SeepColor = Rgba.Hex('#8b6fd0');

const ShadeVeilMax = f(0.20);
const ShadeEdgeMax = f(0.80);
const ShadeInnerClear = f(0.85);
const ShadeInnerClosed = f(0.40);
const ShadeSeepMax = f(0.20);
/** How far the inner edge of the shade moves in and out at the bottom of a breath. */
const ShadeBreathRadius = f(0.03);
/** The curve of the strength: below 1, so the first 20% already shows and the end does not jump. (double) */
const ShadeCurve = 1.4;

/** private static float Clamp01(float v) */
function Clamp01(v) {
  if (v < 0) return 0;
  return v > 1 ? 1 : v;
}

/** Static class: public members in C# declaration order (fields interleaved with methods as declared). */
export const MiasmaLook = Object.freeze({
  DeathAt,
  StepPercent,
  LastStepLine,

  // ---- the gauge ----

  /** パネル（不透明） (§2.1): the gauge's empty track. */
  Track: Rgba.Hex('#141925'),
  /** 罫線 (§2.1): the gauge's frame. */
  Frame: Rgba.Bytes(157, 166, 186, 0.26),
  FillThin,
  FillThick,
  Caution,
  /** 地 (§2.1): the ticks, drawn over the fill. */
  Tick: Rgba.Hex('#10141d', 0.9),
  /** The fill and the track are told apart by colour alone, so they keep a contrast of 3 (WCAG 1.4.11). */
  FillMinContrast: 3,

  AheadAlpha,
  AheadStepAlpha,
  CautionMark,
  RiseMs,
  BreathFrom,
  BreathSlowMs,
  BreathFastMs,
  BreathDepth,

  /** The fill's colour at `percent`: phosphor when thin, deepening to violet as it thickens. */
  FillAt(percent) {
    return BackdropSpec.Mix(FillThin, FillThick, Clamp01(f(percent / DeathAt)));
  },

  /** What the share from `percent` to `nextPercent` warns of. */
  Warning(percent, nextPercent) {
    if (nextPercent <= percent) return MiasmaWarning.None;
    if (nextPercent >= DeathAt) return MiasmaWarning.Death;
    for (let line = StepPercent; line <= LastStepLine; line += StepPercent) {
      if (percent < line && nextPercent >= line) return MiasmaWarning.StepLine;
    }
    return MiasmaWarning.None;
  },

  /** The colour of the share ahead of the fill. Without a warning it is the fill it will become, faint. */
  AheadColor(percent, nextPercent) {
    switch (MiasmaLook.Warning(percent, nextPercent)) {
      case MiasmaWarning.Death:
        return Caution;
      case MiasmaWarning.StepLine:
        return Caution.WithAlpha(AheadStepAlpha);
      default:
        return MiasmaLook.FillAt(nextPercent).WithAlpha(AheadAlpha);
    }
  },

  /** The mark to put before the forecast label: CautionMark when the share ahead is 注意. */
  ForecastMark(percent, nextPercent) {
    return MiasmaLook.Warning(percent, nextPercent) === MiasmaWarning.None ? '' : CautionMark;
  },

  /** The gauge's nine ticks between 0 and 100 (ten cells): full-height lines at 20, 40, 60, 80, short ones between. */
  Ticks() {
    const ticks = new Array(9);
    for (let i = 1; i <= 9; i++) {
      const percent = i * 10;
      const line = percent % StepPercent === 0;
      ticks[i - 1] = new GaugeTick(percent, line, line ? 1 : 0.4);
    }
    return ticks;
  },

  /**
   * The reading the fill shows `elapsedMs` after it moved from `from`
   * to `to`: eased over RiseMs. It goes down the same way (incense).
   */
  Rise(from, to, elapsedMs) {
    if (elapsedMs <= 0) return f(from);
    if (elapsedMs >= RiseMs) return f(to);
    const t = elapsedMs / RiseMs;
    return f(from + ((to - from) | 0) * (t * t * (3.0 - 2.0 * t)));
  },

  /** The period of the breath at `percent`: slow at 80%, quickening to 99%. 0 below 80%. */
  BreathPeriodMs(percent) {
    percent = f(percent);
    if (percent < BreathFrom) return 0;
    const t = Clamp01(f(f(percent - BreathFrom) / (DeathAt - 1 - BreathFrom)));
    // (int)System.Math.Round(BreathSlowMs + (BreathFastMs - BreathSlowMs) * t): float math, Math.Round(double) to even.
    return Math.trunc(roundEven(f(BreathSlowMs + f((BreathFastMs - BreathSlowMs) * t))));
  },

  /** The breath at `ms`: 0 at rest, 1 at the bottom of a breath, on a cosine so it never jumps. */
  Breath(percent, ms) {
    const period = MiasmaLook.BreathPeriodMs(percent);
    if (period <= 0) return 0;
    const phase = ms / period - Math.floor(ms / period);
    return f(0.5 - 0.5 * Math.cos(phase * 2.0 * Math.PI));
  },

  /** The share of the fill's alpha lit at `ms`: 1 below 80%, dipping by BreathDepth from there. */
  FillAlpha(percent, ms) {
    return f(1 - f(BreathDepth * MiasmaLook.Breath(percent, ms)));
  },

  // ---- the shade over the screen ----

  ShadeColor,
  SeepColor,
  ShadeVeilMax,
  ShadeEdgeMax,
  ShadeInnerClear,
  ShadeInnerClosed,
  ShadeSeepMax,
  ShadeBreathRadius,
  ShadeCurve,

  /**
   * C# overloads:
   * - ShadeAt(float percent): the shade at `percent` (a reading easing up with Rise may fall between whole numbers).
   * - ShadeAt(float percent, double ms): the shade at `percent` at `ms`: from 80% its inner edge breathes in step with the gauge.
   */
  ShadeAt(percent, ms) {
    if (ms === undefined) {
      percent = f(percent);
      const s = f(Pow(Clamp01(f(percent / DeathAt)), ShadeCurve)); // System.Math.Pow → CrtPow.Pow
      return new MiasmaShade(
        s,
        f(ShadeVeilMax * s),
        f(ShadeEdgeMax * s),
        f(ShadeInnerClear + f(f(ShadeInnerClosed - ShadeInnerClear) * s)),
        f(ShadeSeepMax * s),
      );
    }
    const still = MiasmaLook.ShadeAt(percent);
    const breath = MiasmaLook.Breath(percent, ms);
    if (breath <= 0) return still;
    return new MiasmaShade(still.Strength, still.VeilAlpha, still.EdgeAlpha,
      f(still.InnerRadius - f(ShadeBreathRadius * breath)), still.SeepAlpha);
  },

  /**
   * How far point (`x`, `y`) — shares of the screen, 0 at the top left —
   * is from the centre, as a share of the half diagonal on a `aspect` (width / height) screen.
   */
  CornerDistance(x, y, aspect) {
    x = f(x);
    y = f(y);
    aspect = f(aspect);
    const dx = (x - 0.5) * aspect;
    const dy = y - 0.5;
    // aspect * aspect is float * float (rounded to float) before + 1.0 widens it.
    const half = Math.sqrt(f(aspect * aspect) + 1.0) / 2.0;
    return f(Math.sqrt(dx * dx + dy * dy) / half);
  },

  /** How much of the edges' darkening reaches a point `distance` from the centre (see CornerDistance). */
  EdgeFalloff(shade, distance) {
    distance = f(distance);
    if (distance <= shade.InnerRadius) return 0;
    if (distance >= 1) return 1;
    const t = f(f(distance - shade.InnerRadius) / f(1 - shade.InnerRadius));
    return f(f(t * t) * f(3 - f(2 * t)));
  },

  /** The shade colour's alpha at a point `distance` from the centre: the even veil, then the edges on top. */
  ShadeAlphaAt(shade, distance) {
    const edge = f(shade.EdgeAlpha * MiasmaLook.EdgeFalloff(shade, distance));
    return f(1 - f(f(1 - shade.VeilAlpha) * f(1 - edge)));
  },

  /** `under` as the shade leaves it at a point `distance` from the centre: darkened, then the seep laid on. */
  Shaded(under, shade, distance) {
    const dark = BackdropSpec.Over(ShadeColor.WithAlpha(MiasmaLook.ShadeAlphaAt(shade, distance)), under);
    return BackdropSpec.Over(SeepColor.WithAlpha(f(shade.SeepAlpha * MiasmaLook.EdgeFalloff(shade, distance))), dark);
  },
});
