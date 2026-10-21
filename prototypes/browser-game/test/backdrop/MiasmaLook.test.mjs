// Port of unity-project-kit/Assets/View/Backdrop/Tests/MiasmaLookTests.cs (#90: the miasma gauge and the
// shade, battle-visual-v1 §2.5), plus the oracle's `miasma:0..100` cases.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { MiasmaLook, MiasmaWarning, GaugeTick, MiasmaShade } from '../../src/bridge/backdrop/MiasmaLook.js';
import { BackdropSpec } from '../../src/bridge/backdrop/BackdropSpec.js';
import { ExplorationBackdropSpec } from '../../src/bridge/backdrop/ExplorationBackdrop.js';
import { oracleMissing, oracleIds, diffCase } from './_oracle.mjs';

const f = Math.fround;

function within(actual, expected, tol, msg) {
  assert.ok(Math.abs(actual - expected) <= tol, `${msg ?? ''} expected ${expected} ± ${tol}, got ${actual}`);
}
function AssertSame(actual, expected) {
  within(actual.R, expected.R, 1e-6);
  within(actual.G, expected.G, 1e-6);
  within(actual.B, expected.B, 1e-6);
  within(actual.A, expected.A, 1e-6);
}

test('TheReadings_MatchTheRules', () => {
  assert.equal(MiasmaLook.DeathAt, 100);
  assert.equal(MiasmaLook.StepPercent, 20);
  assert.equal(MiasmaLook.LastStepLine, 80);
});

test('TheFill_DeepensFromPhosphorToViolet', () => {
  AssertSame(MiasmaLook.FillAt(0), MiasmaLook.FillThin);
  AssertSame(MiasmaLook.FillAt(100), MiasmaLook.FillThick);
  assert.ok(BackdropSpec.Luminance(MiasmaLook.FillAt(60)) < BackdropSpec.Luminance(MiasmaLook.FillAt(20)));
});

test('TheFill_StaysApartFromItsTrack_AtEveryReading', () => {
  for (let percent = 0; percent <= 100; percent++) {
    assert.ok(BackdropSpec.Contrast(MiasmaLook.FillAt(percent), MiasmaLook.Track) >= MiasmaLook.FillMinContrast, `${percent}%`);
  }
});

test('TheCaution_ReadsOnTheTrack', () => {
  assert.ok(BackdropSpec.Contrast(MiasmaLook.Caution, MiasmaLook.Track) >= MiasmaLook.FillMinContrast);
});

for (const [percent, next, warning] of [
  [10, 13, MiasmaWarning.None],
  [17, 20, MiasmaWarning.StepLine],
  [38, 41, MiasmaWarning.StepLine],
  [78, 85, MiasmaWarning.StepLine],
  [82, 89, MiasmaWarning.None],
  [93, 100, MiasmaWarning.Death],
  [75, 104, MiasmaWarning.Death],
  [40, 40, MiasmaWarning.None],
  [45, 35, MiasmaWarning.None],
]) {
  test(`TheShareAhead_WarnsOfALineAndOfDeath(${percent}, ${next}, ${warning})`, () => {
    assert.equal(MiasmaLook.Warning(percent, next), warning);
  });
}

test('TheShareAhead_IsTheFaintFill_OrTheCautionWithItsMark', () => {
  const plain = MiasmaLook.AheadColor(10, 13);
  AssertSame(plain.WithAlpha(1), MiasmaLook.FillAt(13));
  assert.equal(plain.A, MiasmaLook.AheadAlpha);
  assert.equal(MiasmaLook.ForecastMark(10, 13), '');

  const line = MiasmaLook.AheadColor(38, 41);
  AssertSame(line.WithAlpha(1), MiasmaLook.Caution);
  assert.equal(line.A, MiasmaLook.AheadStepAlpha);
  assert.equal(MiasmaLook.ForecastMark(38, 41), '△ ');

  const death = MiasmaLook.AheadColor(95, 100);
  AssertSame(death, MiasmaLook.Caution);
  assert.equal(MiasmaLook.ForecastMark(95, 100), '△ ');
});

test('TheGauge_HasTenCells_WithFullLinesEvery20', () => {
  const ticks = MiasmaLook.Ticks();
  assert.equal(ticks.length, 9);
  for (let i = 0; i < ticks.length; i++) {
    assert.ok(ticks[i] instanceof GaugeTick);
    assert.equal(ticks[i].Percent, (i + 1) * 10);
    const line = ticks[i].Percent % 20 === 0;
    assert.equal(ticks[i].IsStepLine, line, `${ticks[i].Percent}%`);
    if (line) assert.equal(ticks[i].Height, 1);
    else assert.ok(ticks[i].Height < 1);
  }
});

test('TheFill_ClimbsToANewReading_Softly', () => {
  assert.equal(MiasmaLook.Rise(38, 41, 0), 38);
  within(MiasmaLook.Rise(38, 41, MiasmaLook.RiseMs / 2.0), 39.5, 0.001);
  assert.equal(MiasmaLook.Rise(38, 41, MiasmaLook.RiseMs), 41);
  assert.equal(MiasmaLook.Rise(50, 40, MiasmaLook.RiseMs * 2.0), 40);
  const early = MiasmaLook.Rise(0, 100, MiasmaLook.RiseMs * 0.1);
  assert.ok(early < 10, 'it starts slower than straight');
});

test('TheBreath_StartsAt80_AndQuickensTowardsDeath', () => {
  assert.equal(MiasmaLook.BreathPeriodMs(79), 0);
  assert.equal(MiasmaLook.Breath(79, 600), 0);
  assert.equal(MiasmaLook.FillAlpha(79, 600), 1);
  assert.equal(MiasmaLook.BreathPeriodMs(80), MiasmaLook.BreathSlowMs);
  assert.equal(MiasmaLook.BreathPeriodMs(99), MiasmaLook.BreathFastMs);
  const p90 = MiasmaLook.BreathPeriodMs(90);
  assert.ok(p90 < MiasmaLook.BreathSlowMs && p90 > MiasmaLook.BreathFastMs);

  const period = MiasmaLook.BreathPeriodMs(80);
  within(MiasmaLook.Breath(80, 0), 0, 1e-6);
  within(MiasmaLook.Breath(80, period / 2.0), 1, 1e-6);
  within(MiasmaLook.FillAlpha(80, period / 2.0), 1 - MiasmaLook.BreathDepth, 1e-6);
  within(MiasmaLook.Breath(80, period), 0, 1e-6);
});

test('TheShade_IsClearAtTheStartOfALife', () => {
  const shade = MiasmaLook.ShadeAt(0);
  assert.equal(shade.Strength, 0);
  assert.equal(MiasmaLook.ShadeAlphaAt(shade, 1), 0);
  assert.equal(MiasmaLook.ShadeAlphaAt(shade, 0), 0);
  assert.equal(shade.SeepAlpha, 0);
});

test('TheShade_ClosesIn_AsTheReadingClimbs', () => {
  const corner = MiasmaLook.CornerDistance(0, 0, 16 / 9);
  within(corner, 1, 1e-5);
  let lastAlpha = -1;
  let lastInner = 2;
  for (let percent = 0; percent <= 100; percent += 10) {
    const shade = MiasmaLook.ShadeAt(percent);
    const alpha = MiasmaLook.ShadeAlphaAt(shade, corner);
    assert.ok(alpha > lastAlpha, `${percent}%`);
    assert.ok(shade.InnerRadius < lastInner, `${percent}%`);
    lastAlpha = alpha;
    lastInner = shade.InnerRadius;
  }
  assert.ok(MiasmaLook.ShadeAlphaAt(MiasmaLook.ShadeAt(20), corner) > 0.05, 'the first line already shows');
  within(MiasmaLook.ShadeAlphaAt(MiasmaLook.ShadeAt(100), corner),
    f(1 - f(f(1 - MiasmaLook.ShadeVeilMax) * f(1 - MiasmaLook.ShadeEdgeMax))), 1e-5);
});

test('TheShade_IsDarkerAtTheCorners_ThanAtTheCentre', () => {
  const shade = MiasmaLook.ShadeAt(70);
  let last = -1;
  for (let step = 0; step <= 10; step++) {
    const alpha = MiasmaLook.ShadeAlphaAt(shade, step / 10);
    assert.ok(alpha >= last, `distance ${step / 10}`);
    last = alpha;
  }
  within(MiasmaLook.ShadeAlphaAt(shade, 0), shade.VeilAlpha, 1e-6);
});

test('TheCentre_IsOnlyEverVeiled_SoTheMapKeepsItsMiddle', () => {
  for (let percent = 0; percent <= 100; percent += 5) {
    const shade = MiasmaLook.ShadeAt(percent);
    assert.ok(shade.InnerRadius >= MiasmaLook.ShadeInnerClosed - 1e-6);
    assert.ok(MiasmaLook.ShadeAlphaAt(shade, 0.3) <= MiasmaLook.ShadeVeilMax + 1e-6, `${percent}%`);
  }
});

test('TheShade_BreathesWithTheGauge_From80', () => {
  assert.equal(MiasmaLook.ShadeAt(70, 600).InnerRadius, MiasmaLook.ShadeAt(70).InnerRadius);
  const period = MiasmaLook.BreathPeriodMs(85);
  const rest = MiasmaLook.ShadeAt(85, 0);
  const deep = MiasmaLook.ShadeAt(85, period / 2.0);
  within(rest.InnerRadius, MiasmaLook.ShadeAt(85).InnerRadius, 1e-6);
  within(deep.InnerRadius, rest.InnerRadius - MiasmaLook.ShadeBreathRadius, 1e-6);
});

test('TheMapsText_ReadsUnderTheShade_OnEveryLayerAndReading', () => {
  const distances = [0, 0.3, 0.5, 0.7, 0.85, 1];
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    const backdrop = ExplorationBackdropSpec.For(layer);
    for (let percent = 0; percent <= 100; percent += 10) {
      const shade = MiasmaLook.ShadeAt(percent);
      for (let step = 0; step <= 10; step++) {
        const under = ExplorationBackdropSpec.BrightestAt(backdrop, step / 10);
        for (const d of distances) {
          const shaded = MiasmaLook.Shaded(under, shade, d);
          assert.ok(BackdropSpec.Contrast(ExplorationBackdropSpec.Ink, shaded) >= ExplorationBackdropSpec.InkMinContrast, `layer ${layer}, ${percent}%`);
          assert.ok(BackdropSpec.Contrast(ExplorationBackdropSpec.Amber, shaded) >= ExplorationBackdropSpec.AmberMinContrast, `layer ${layer}, ${percent}%`);
        }
      }
    }
  }
});

// ---- port-specific ----

test('ShadeAt_TheTwoOverloads', () => {
  // ShadeAt(p) and ShadeAt(p, ms) below 80% are the same shade; a MiasmaShade holds float32 values.
  const still = MiasmaLook.ShadeAt(42);
  assert.ok(still instanceof MiasmaShade);
  assert.ok(still.equals(MiasmaLook.ShadeAt(42, 1234.5)));
  for (const k of ['Strength', 'VeilAlpha', 'EdgeAlpha', 'InnerRadius', 'SeepAlpha']) assert.equal(still[k], f(still[k]), k);
});

test('BreathPeriodMs_RoundsHalfToEven_LikeMathRound', () => {
  // 2400 − 1200 · t lands on whole numbers or between; (int)Math.Round is banker's rounding.
  for (let p = 80; p <= 99; p++) {
    const t = Math.min(1, f(f(p - 80) / 19));
    const exact = f(2400 + f(-1200 * t));
    const r = MiasmaLook.BreathPeriodMs(p);
    assert.ok(Math.abs(r - exact) <= 0.5, `${p}: ${r} vs ${exact}`);
  }
});

const ids = oracleMissing ? [] : oracleIds().filter((id) => id.startsWith('miasma:'));
test('Oracle_Miasma_MatchesTheCSharp', { skip: oracleMissing ?? false }, () => {
  assert.equal(ids.length, 21);
  for (const id of ids) assert.equal(diffCase(id), null, id);
});
