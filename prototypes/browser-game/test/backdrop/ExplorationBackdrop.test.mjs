// Port of unity-project-kit/Assets/View/Backdrop/Tests/ExplorationBackdropTests.cs (#90: the exploration
// screen's backdrop, battle-visual-v1 §2.5). The oracle's layer cases (which carry ExplorationBackdropSpec's
// values too) are diffed in BackdropSpec.test.mjs; the table case's ExplorationStatic is re-checked here.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { ExplorationBackdrop, ExplorationBackdropSpec } from '../../src/bridge/backdrop/ExplorationBackdrop.js';
import { BackdropSpec } from '../../src/bridge/backdrop/BackdropSpec.js';
import { oracleMissing, diffCase } from './_oracle.mjs';

test('EveryLayer_StandsInTheBattlesPlaceOfTheSameLayer', () => {
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    const backdrop = ExplorationBackdropSpec.For(layer);
    assert.equal(backdrop.Layer, layer);
    assert.equal(backdrop.Place, BackdropSpec.For(layer), `layer ${layer}`);
  }
});

for (const [asked, drawn] of [[0, 1], [8, 7]]) {
  test(`ALayerOutsideTheLair_IsHeldToTheNearestEnd(${asked}, ${drawn})`, () => {
    assert.equal(ExplorationBackdropSpec.For(asked).Layer, drawn);
    assert.equal(ExplorationBackdropSpec.For(asked).Place.Layer, drawn);
  });
}

test('TheVeil_IsTheGroundColour', () => {
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    const veil = ExplorationBackdropSpec.For(layer).Veil;
    const ground = ExplorationBackdropSpec.Ground;
    assert.deepEqual([veil.R, veil.G, veil.B], [ground.R, ground.G, ground.B], `layer ${layer}`);
    assert.ok(veil.A >= Math.fround(0.3) && veil.A <= Math.fround(0.75), `layer ${layer}`);
  }
});

test('TheBodyText_ReadsOnEveryLayer', () => {
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    const contrast = ExplorationBackdropSpec.LowestContrast(ExplorationBackdropSpec.For(layer), ExplorationBackdropSpec.Ink);
    assert.ok(contrast >= ExplorationBackdropSpec.InkMinContrast, `layer ${layer}: ${contrast}`);
  }
});

test('TheAmberAccent_ReadsOnEveryLayer', () => {
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    const contrast = ExplorationBackdropSpec.LowestContrast(ExplorationBackdropSpec.For(layer), ExplorationBackdropSpec.Amber);
    assert.ok(contrast >= ExplorationBackdropSpec.AmberMinContrast, `layer ${layer}: ${contrast}`);
  }
});

test('TheBrightLayers_NeedTheThickVeil', () => {
  for (const layer of [1, 2, 4]) {
    const bare = new ExplorationBackdrop({ Layer: layer, Place: BackdropSpec.For(layer), Veil: ExplorationBackdropSpec.Ground.WithAlpha(0) });
    assert.ok(ExplorationBackdropSpec.LowestContrast(bare, ExplorationBackdropSpec.Ink) < ExplorationBackdropSpec.InkMinContrast, `layer ${layer}`);
    assert.ok(ExplorationBackdropSpec.For(layer).Veil.A > ExplorationBackdropSpec.For(3).Veil.A, `layer ${layer}`);
  }
});

test('Layer4_StaysTheBrightest_UnderTheVeil', () => {
  const four = ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(4));
  for (let layer = 1; layer <= BackdropSpec.Layers; layer++) {
    if (layer === 4) continue;
    assert.ok(ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(layer)) < four, `layer ${layer}`);
  }
});

test('Layer3_StaysDarkerThan1_2And4_UnderTheVeil', () => {
  const three = ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(3));
  assert.ok(three < ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(1)));
  assert.ok(three < ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(2)));
  assert.ok(three < ExplorationBackdropSpec.PeakLuminance(ExplorationBackdropSpec.For(4)));
});

// ---- port-specific ----

test('TheVeilAlphas_AreTheCSharpFloats', () => {
  const alphas = [1, 2, 3, 4, 5, 6, 7].map((l) => ExplorationBackdropSpec.For(l).Veil.A);
  assert.deepEqual(alphas, [0.72, 0.68, 0.35, 0.70, 0.35, 0.35, 0.35].map(Math.fround));
  assert.deepEqual(Object.keys(ExplorationBackdropSpec).filter((k) => typeof ExplorationBackdropSpec[k] !== 'function'),
    ['Ground', 'Ink', 'InkMinContrast', 'Amber', 'AmberMinContrast']);
});

test('Oracle_LayerCases_CarryTheExplorationValues', { skip: oracleMissing ?? false }, () => {
  for (const id of ['layer:1', 'layer:4', 'layer:7']) assert.equal(diffCase(id), null, id);
});
