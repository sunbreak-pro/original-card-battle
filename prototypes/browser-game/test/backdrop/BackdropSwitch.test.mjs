// Port of unity-project-kit/Assets/View/Backdrop/Tests/BackdropSwitchTests.cs (#607: the switching
// BackdropLayers draws), plus the oracle's `switch` case.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { BackdropSwitch, Change } from '../../src/bridge/backdrop/BackdropSwitch.js';
import { BackdropSpec } from '../../src/bridge/backdrop/BackdropSpec.js';
import { oracleMissing, diffCase } from './_oracle.mjs';

test('TheFirstLayer_ShowsAtOnce', () => {
  const sw = new BackdropSwitch();
  assert.equal(sw.Shown, 0);
  assert.equal(sw.Show(2, 1000), BackdropSwitch.Change.First);
  assert.equal(sw.Shown, 2);
  assert.equal(sw.Leaving, 0);
  assert.equal(sw.Tick(1000), 1);
});

test('TheSameLayerAgain_ChangesNothing', () => {
  const sw = new BackdropSwitch();
  sw.Show(4, 0);
  assert.equal(sw.Show(4, 50), Change.None);
  assert.equal(sw.Leaving, 0);
});

test('ANewLayer_FadesInOver400Ms_ThenTheOldOneGoes', () => {
  const sw = new BackdropSwitch();
  sw.Show(1, 0);
  assert.equal(sw.Show(2, 1000), Change.Fade);
  assert.equal(sw.Shown, 2);
  assert.equal(sw.Leaving, 1);

  assert.equal(sw.Tick(1000), 0);
  const half = sw.Tick(1000 + BackdropSpec.CrossfadeMs / 2.0);
  assert.ok(half >= 0.01 && half <= 0.99);
  assert.equal(sw.Leaving, 1, 'still fading');

  assert.equal(sw.Tick(1000 + BackdropSpec.CrossfadeMs), 1);
  assert.equal(sw.Leaving, 0, 'the old layer is done');
});

test('AThirdLayerMidFade_DropsTheOneFadingOut', () => {
  const sw = new BackdropSwitch();
  sw.Show(1, 0);
  sw.Show(2, 1000);
  sw.Tick(1100);
  assert.equal(sw.Show(3, 1200), Change.FadeDroppingOldest);
  assert.equal(sw.Shown, 3);
  assert.equal(sw.Leaving, 2, 'the half-shown layer now fades out');
  assert.equal(sw.Tick(1200), 0, 'the fade starts over');
});

for (const [asked, shown] of [[0, 1], [9, 7]]) {
  test(`ALayerOutsideTheLair_IsHeldToTheNearestEnd(${asked}, ${shown})`, () => {
    const sw = new BackdropSwitch();
    sw.Show(asked, 0);
    assert.equal(sw.Shown, shown);
    assert.equal(sw.Show(shown, 10), Change.None);
  });
}

test('ShownAndLeaving_HaveNoPublicSetter', () => {
  const sw = new BackdropSwitch();
  assert.throws(() => {
    'use strict';
    sw.Shown = 3;
  }, TypeError);
});

test('Oracle_Switch_MatchesTheCSharp', { skip: oracleMissing ?? false }, () => {
  assert.equal(diffCase('switch'), null);
});
