// Port of unity-project-kit/Assets/View/Backdrop/Tests/BackdropSpecTests.cs (#89: the seven layers'
// backdrops, battle-visual-v1 §2.4), plus the oracle's `table:BackdropSpec` and `layer:0..8` cases
// (oracle/out/backdrop.json) diffed value for value.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { BackdropSpec, Rgba, Haze, HazeShape, SkyStop, Flicker, Motes, LayerBackdrop } from '../../src/bridge/backdrop/BackdropSpec.js';
import { oracleMissing, oracleIds, diffCase } from './_oracle.mjs';

const f = Math.fround;

function within(actual, expected, tol, msg) {
  assert.ok(Math.abs(actual - expected) <= tol, `${msg ?? ''} expected ${expected} ± ${tol}, got ${actual}`);
}

function AssertHex(color, hex) {
  const want = Rgba.Hex(hex);
  within(color.R, want.R, 1e-4, hex);
  within(color.G, want.G, 1e-4, hex);
  within(color.B, want.B, 1e-4, hex);
}

function MeanLuminance(backdrop) {
  let s = 0;
  for (let step = 0; step <= 20; step++) s += BackdropSpec.Luminance(BackdropSpec.SkyAt(backdrop, f(step / 20)));
  return s / 21;
}

test('EveryLayer_HasItsOwnBackdrop', () => {
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    const backdrop = BackdropSpec.For(layer);
    assert.equal(backdrop.Layer, layer);
    assert.ok(backdrop.Sky.length >= 2, `layer ${layer}`);
    assert.equal(backdrop.Sky[0].At, 0);
    assert.equal(backdrop.Sky[backdrop.Sky.length - 1].At, 1);
    assert.ok(backdrop.Hazes.length > 0, `layer ${layer}`);
  }
});

for (const [asked, drawn] of [[0, 1], [-3, 1], [8, 7], [99, 7]]) {
  test(`ALayerOutsideTheLair_IsHeldToTheNearestEnd(${asked}, ${drawn})`, () => {
    assert.equal(BackdropSpec.For(asked).Layer, drawn);
  });
}

test('Layers1_4_5_KeepTheValuesSection24Fixes', () => {
  AssertHex(BackdropSpec.For(1).Sky[0].Color, '#d6e8a4');
  AssertHex(BackdropSpec.For(1).Sky[1].Color, '#7cab4a');
  AssertHex(BackdropSpec.For(1).Sky[2].Color, '#22391c');
  AssertHex(BackdropSpec.For(4).Sky[0].Color, '#e4e6f2');
  AssertHex(BackdropSpec.For(4).Sky[1].Color, '#b7bbd6');
  AssertHex(BackdropSpec.For(4).Sky[2].Color, '#7d82a8');
  AssertHex(BackdropSpec.For(5).Sky[0].Color, '#1a0e12');
  AssertHex(BackdropSpec.For(5).Sky[1].Color, '#070507');
  assert.equal(BackdropSpec.For(1).Tile.A, f(0.82));
  assert.equal(BackdropSpec.For(4).Tile.A, f(0.82));
  assert.equal(BackdropSpec.For(5).Tile.A, f(0.80));
});

test('TheReachRed_KeepsAContrastOf3OnEveryLayersFloor', () => {
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    const contrast = BackdropSpec.ReachRedContrast(BackdropSpec.For(layer));
    assert.ok(contrast >= BackdropSpec.ReachRedMinContrast, `layer ${layer}: ${contrast}`);
  }
});

test('Layer4_IsTheLowestForTheRed_AsSection23Measured', () => {
  const four = BackdropSpec.ReachRedContrast(BackdropSpec.For(4));
  assert.ok(four < 4.0);
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    if (layer === 4) continue;
    assert.ok(BackdropSpec.ReachRedContrast(BackdropSpec.For(layer)) > four, `layer ${layer}`);
  }
});

test('TheBrightness_FollowsWorldRule3', () => {
  const l = new Array(BackdropSpec.Layers + 1).fill(0);
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) l[layer] = MeanLuminance(BackdropSpec.For(layer));
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    if (layer !== 4) assert.ok(l[4] > l[layer], `4 is the brightest, above ${layer}`);
  }
  assert.ok(l[1] > l[3], '1 is bright, above 3');
  assert.ok(l[2] > l[3], '3 drops after 2');
  assert.ok(l[5] < l[4]);
  assert.ok(l[6] < l[5], '5 onward goes darker');
  assert.ok(l[7] < l[6], '7 is the darkest');
  for (let layer = 1; layer < BackdropSpec.Layers; layer++) assert.ok(l[7] < l[layer], `7 is darker than ${layer}`);
});

test('TheSky_RunsStraightBetweenItsStops', () => {
  const four = BackdropSpec.For(4);
  AssertHex(BackdropSpec.SkyAt(four, 0), '#e4e6f2');
  AssertHex(BackdropSpec.SkyAt(four, 0.5), '#b7bbd6');
  AssertHex(BackdropSpec.SkyAt(four, 1), '#7d82a8');
  const quarter = BackdropSpec.SkyAt(four, 0.25);
  within(quarter.R, (0xe4 + 0xb7) / 2 / 255, 1e-4);
});

test('AHaze_LaysItsAlphaOnlyWhereItReaches', () => {
  const top = new Haze({ Shape: HazeShape.TopGlow, Color: new Rgba(1, 1, 1, 0.6), Size: 0.5 });
  within(BackdropSpec.HazeAlphaAt(top, 0), 0.6, 1e-6);
  within(BackdropSpec.HazeAlphaAt(top, 0.25), 0.3, 1e-6);
  assert.equal(BackdropSpec.HazeAlphaAt(top, 0.6), 0);

  const band = new Haze({ Shape: HazeShape.Band, Color: new Rgba(1, 1, 1, 0.1), Y: 0.5, Size: 0.2 });
  within(BackdropSpec.HazeAlphaAt(band, 0.5), 0.1, 1e-6);
  assert.equal(BackdropSpec.HazeAlphaAt(band, 0.65), 0);
});

test('TheContrast_IsWcag2s', () => {
  within(BackdropSpec.Contrast(new Rgba(1, 1, 1), new Rgba(0, 0, 0)), 21.0, 1e-9);
  within(BackdropSpec.Contrast(new Rgba(0.5, 0.5, 0.5), new Rgba(0.5, 0.5, 0.5)), 1.0, 1e-9);
});

// ---- port-specific: C# float and struct semantics ----

test('Floats_AreHeldAsTheirFloat32Values', () => {
  assert.equal(BackdropSpec.FloorTop, 0.5185185074806213);
  assert.equal(BackdropSpec.FloorBottom, 0.5555555820465088);
  const c = Rgba.Hex('#ff1f4f');
  assert.deepEqual([c.R, c.G, c.B, c.A], [1, 0.12156862765550613, 0.30980393290519714, 1]);
  assert.equal(new Haze({ X: 0.42 }).X, f(0.42));
  assert.equal(new Flicker(6000, 0.25).Depth, 0.25);
  assert.equal(new SkyStop(0.4, c).At, f(0.4));
});

test('Defaults_AreTheCSharpOnes', () => {
  // default(Rgba) is all zeros (alpha 0 too); Motes.None and Haze defaults as declared.
  assert.deepEqual({ ...Motes.None.Color }, { R: 0, G: 0, B: 0, A: 0 });
  assert.equal(Motes.None.SizePx, 4);
  const h = new Haze();
  assert.equal(h.Shape, HazeShape.TopGlow);
  assert.equal(h.X, 0.5);
  assert.equal(h.Count, 1);
  assert.equal(h.Flicker, Flicker.Still);
  assert.equal(Flicker.Still.Moves, false);
  assert.equal(new LayerBackdrop().Motes, Motes.None);
  assert.equal(BackdropSpec.For(6).Motes, Motes.None);
  assert.throws(() => new Haze({ Colour: c0() }), /no member 'Colour'/);
  function c0() {
    return new Rgba(0, 0, 0);
  }
});

test('Hex_TrimsEveryLeadingHash_AndRefusesBadDigits', () => {
  assert.ok(Rgba.Hex('##10141d', 0.5).equals(Rgba.Hex('10141d', 0.5)));
  assert.throws(() => Rgba.Hex('#zz0000'), { name: 'FormatException' });
  assert.throws(() => Rgba.Hex('#fff'), { name: 'ArgumentOutOfRangeException' });
});

test('Rgba_HasValueEquality_LikeACSharpStruct', () => {
  assert.ok(Rgba.Bytes(157, 166, 186, 0.26).equals(new Rgba(157 / 255, 166 / 255, 186 / 255, 0.26)));
  assert.ok(!Rgba.Hex('#ffffff').equals(Rgba.Hex('#ffffff', 0.5)));
});

// ---- the C# oracle ----

const ids = oracleMissing ? [] : oracleIds().filter((id) => id === 'table:BackdropSpec' || id.startsWith('layer:'));
test('Oracle_TableAndLayers_MatchTheCSharp', { skip: oracleMissing ?? false }, () => {
  assert.equal(ids.length, 10);
  for (const id of ids) assert.equal(diffCase(id), null, id);
});
