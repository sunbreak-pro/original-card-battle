// Port of unity-project-kit/Assets/View/Depiction/Script/BattleSpeed.cs
// The battle speed the player picks (#348): 1.0, 1.25 or 1.5 times. Pure C#, like the rest of
// Script/, so `dotnet test` holds the same factors the screen plays.
//
// Every length in EffectCatalog and EffectFlow is the length at 1.25 times (today's pace, the
// default). At a multiplier k an effect takes "written ms x 1.25 / k": 1.0 times is slower than
// today, 1.5 times faster. The View never rescales a length: it hands UiTween the written ms and
// sets UiTween.Speed to TweenSpeed, and UiTween.Run and UiTween.Wait both divide by that speed. So
// one number stretches the tweens, the effect waits and the gaps between events alike.
//
// Port notes:
// - Every C# `float` is a single: constants are held as their float32 values and each float
//   operation is rounded with Math.fround, as .NET does single-precision arithmetic. So
//   TweenSpeed(Slow) is fround(0.8) = 0.800000011920929, as in the C#.
// - C#'s two TweenSpeed overloads are one function: TweenSpeed(step) and TweenSpeed(step, halted).
// - Load trims with C#'s string.Trim() (char.IsWhiteSpace), not JS's trim().
import { ArgumentOutOfRangeException, defineEnum } from '../../shared/cs.js';

const f = Math.fround;

/** new ArgumentOutOfRangeException(paramName, actualValue, message) with .NET's message. */
function argRangeValue(name, value, message) {
  const e = new ArgumentOutOfRangeException(`${message} (Parameter '${name}')\nActual value was ${value}.`);
  e.ParamName = name;
  e.ActualValue = value;
  return e;
}

/** C# char.IsWhiteSpace for one UTF-16 code unit. */
function isCsWhiteSpace(c) {
  const u = c.charCodeAt(0);
  return (u >= 0x09 && u <= 0x0d) || u === 0x20 || u === 0x85 || u === 0xa0 || u === 0x1680
    || (u >= 0x2000 && u <= 0x200a) || u === 0x2028 || u === 0x2029 || u === 0x202f || u === 0x205f || u === 0x3000;
}

/** C# string.Trim(). */
function csTrim(s) {
  let a = 0;
  let b = s.length;
  while (a < b && isCsWhiteSpace(s[a])) a++;
  while (b > a && isCsWhiteSpace(s[b - 1])) b--;
  return s.substring(a, b);
}

/** The three steps of the battle speed (battle_ui_ux_v2 §5.0). */
export const BattleSpeedStep = defineEnum([
  /** 1.0 times: the written lengths stretched by 1.25. */
  'Slow',
  /** 1.25 times: today's pace and the default. The written lengths as they are. */
  'Normal',
  /** 1.5 times: the written lengths times 1.25 / 1.5. */
  'Fast',
]);

/**
 * The one place the battle speed's factors live. The View reads TweenSpeed(BattleSpeedStep, bool)
 * into UiTween.Speed and shows Label, Chevrons and Tooltip; the measurement reads WallMs and
 * EventCapMsAt.
 */
export const BattleSpeed = {
  Default: BattleSpeedStep.Normal,

  /** The multiplier the lengths in EffectCatalog and EffectFlow are written at. */
  BaseMultiplier: f(1.25),

  /** battle_ui_ux_v2 §5.0: one action within 2.0 s. */
  EventCapMs: f(2000),

  /** UiTween's reduced-motion factor: what is left of a halted event settles within a few frames. */
  HaltedTweenSpeed: f(1000),

  Steps: [BattleSpeedStep.Slow, BattleSpeedStep.Normal, BattleSpeedStep.Fast],

  /** The factor the player picks: 1.0, 1.25 or 1.5. */
  Multiplier(step) {
    switch (step) {
      case BattleSpeedStep.Slow: return f(1);
      case BattleSpeedStep.Normal: return f(1.25);
      case BattleSpeedStep.Fast: return f(1.5);
      default: throw argRangeValue('step', step, 'No multiplier is written for this speed.');
    }
  },

  /**
   * What UiTween.Speed is set to: 0.8, 1 or 1.2. The default plays the written lengths unchanged.
   * With `halted` (C#'s second overload): a halted battle (降参) settles at reduced-motion speed
   * whatever was picked.
   */
  TweenSpeed(step, halted) {
    if (halted !== undefined) return halted ? BattleSpeed.HaltedTweenSpeed : BattleSpeed.TweenSpeed(step);
    return f(BattleSpeed.Multiplier(step) / BattleSpeed.BaseMultiplier);
  },

  /** How long a length written at 1.25 times really lasts at `step`. */
  WallMs(nominalMs, step) {
    return f(f(f(nominalMs) * BattleSpeed.BaseMultiplier) / BattleSpeed.Multiplier(step));
  },

  /**
   * The cap one action is held to at this speed: 2.0 s at 1.25 and 1.5 times, and the same
   * 2.0 s stretched by the slow step's factor (2.5 s) at 1.0 times (battle_ui_ux_v2 §5.0).
   * Test-only (#495): the screen never checks a cap; the measurement holds EffectPlan's waits to it.
   */
  EventCapMsAt(step) {
    return BattleSpeed.Multiplier(step) >= BattleSpeed.BaseMultiplier
      ? BattleSpeed.EventCapMs
      : BattleSpeed.WallMs(BattleSpeed.EventCapMs, step);
  },

  /** The switch's next step: 1.0 → 1.25 → 1.5 → 1.0. */
  Next(step) {
    switch (step) {
      case BattleSpeedStep.Slow: return BattleSpeedStep.Normal;
      case BattleSpeedStep.Normal: return BattleSpeedStep.Fast;
      case BattleSpeedStep.Fast: return BattleSpeedStep.Slow;
      default: throw argRangeValue('step', step, 'No next speed is written for this speed.');
    }
  },

  /**
   * The number on the switch (battle-visual-v1 §4.1): "×1.0", "×1.25" or "×1.5", with no word.
   * The View shows it as it is, next to Chevrons chevrons.
   */
  Label(step) {
    switch (step) {
      case BattleSpeedStep.Slow: return '×1.0';
      case BattleSpeedStep.Normal: return '×1.25';
      case BattleSpeedStep.Fast: return '×1.5';
      default: throw argRangeValue('step', step, 'No label is written for this speed.');
    }
  },

  /**
   * How many right-pointing chevrons the switch's icon stacks (battle-visual-v1 §4.1 and
   * 付録 A 「速さ」): one at 1.0 times, two at 1.25, three at 1.5.
   */
  Chevrons(step) {
    switch (step) {
      case BattleSpeedStep.Slow: return 1;
      case BattleSpeedStep.Normal: return 2;
      case BattleSpeedStep.Fast: return 3;
      default: throw argRangeValue('step', step, 'No icon is written for this speed.');
    }
  },

  /** The line the switch shows under itself while hovered (battle-visual-v1 §4.1). */
  Tooltip: '戦闘の速さ。押すたびに ×1.0 → ×1.25 → ×1.5 と巡ります',

  /** What the screen stores (PlayerPrefs): fixed strings, so no culture's decimal mark gets in. */
  Save(step) {
    switch (step) {
      case BattleSpeedStep.Slow: return '1.0';
      case BattleSpeedStep.Normal: return '1.25';
      case BattleSpeedStep.Fast: return '1.5';
      default: throw argRangeValue('step', step, 'No saved form is written for this speed.');
    }
  },

  /**
   * The speed the screen stored, read back. Nothing stored, or anything this build does not
   * write, is the default (1.25 times).
   */
  Load(has, saved) {
    if (!has || saved == null) return BattleSpeed.Default;
    const text = csTrim(saved);
    for (const step of BattleSpeed.Steps) {
      if (BattleSpeed.Save(step) === text) return step;
    }
    return BattleSpeed.Default;
  },
};
