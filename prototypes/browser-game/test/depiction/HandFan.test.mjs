// Tests for src/bridge/depictionScript/HandFan.js (port of View/Depiction/Script/HandFan.cs: FanPlace,
// HandFan, HandSpec). Ported cases: Assets/View/Depiction/Tests/HandFanTests.cs and HandSpecTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { FanPlace, HandFan, HandSpec } from '../../src/bridge/depictionScript/HandFan.js';

const f = Math.fround;
const near = (a, b, tol, msg) => assert.ok(Math.abs(a - b) <= tol, `${msg || ''} expected ${b} ± ${tol}, got ${a}`);

// ---- HandFanTests.cs ----

// The values the BattleDepiction scene ships with (DepictionPlayer and the Card prefab).
const Spacing = 204;
const DegreesPerCard = 2.5;
const DropPixels = 4;
const CardWidth = 190;
const CardHeight = 260;
const Tolerance = f(1e-4);

const Place = (count, index) => HandFan.Place(count, index, Spacing, DegreesPerCard, DropPixels, CardWidth);

/** Lowest of the four corners of a centre-pivoted card at `place`. */
function LowestCornerY(place) {
  const radians = place.Degrees * Math.PI / 180.0;
  let lowest = Number.MAX_VALUE;
  for (const cx of [-CardWidth * 0.5, CardWidth * 0.5]) {
    for (const cy of [-CardHeight * 0.5, CardHeight * 0.5]) {
      lowest = Math.min(lowest, place.Y + cx * Math.sin(radians) + cy * Math.cos(radians));
    }
  }
  return f(lowest);
}

/** Compares card centres (pivots), not their top edges: a steep fan's outer top corner may stand higher. */
for (const count of [3, 5, 7]) {
  for (const degreesPerCard of [2.5, 8, 20]) {
    test(`TheMiddleCardRestsHighest(${count}, ${degreesPerCard})`, () => {
      // Each step away from the middle rests lower than the one before it, however steep the tilt.
      const middle = Math.trunc(count / 2);
      for (let i = middle; i < count - 1; i++) {
        const inner = HandFan.Place(count, i, Spacing, degreesPerCard, DropPixels, CardWidth);
        const outer = HandFan.Place(count, i + 1, Spacing, degreesPerCard, DropPixels, CardWidth);
        assert.ok(outer.Y < inner.Y, 'card ' + (i + 1) + ' of ' + count);
      }
    });
  }
}

for (const count of [2, 3, 4, 5, 6]) {
  test(`TheFanIsMirrorSymmetric(${count})`, () => {
    for (let i = 0; i < count; i++) {
      const left = Place(count, i);
      const right = Place(count, count - 1 - i);
      near(left.X, -right.X, Tolerance, 'x of card ' + i + ' of ' + count);
      near(left.Y, right.Y, Tolerance, 'y of card ' + i + ' of ' + count);
      near(left.Degrees, -right.Degrees, Tolerance, 'tilt of card ' + i + ' of ' + count);
    }
  });
}

for (const count of [1, 2, 3, 5, 7]) {
  for (const degreesPerCard of [2.5, 8, 20]) {
    test(`NoLowerCornerDipsBelowTheBaseLine(${count}, ${degreesPerCard})`, () => {
      // The base line is where an upright card resting at y = 0 has its bottom edge.
      const baseLine = -CardHeight * 0.5;
      for (let i = 0; i < count; i++) {
        const place = HandFan.Place(count, i, Spacing, degreesPerCard, DropPixels, CardWidth);
        assert.ok(LowestCornerY(place) >= baseLine - Tolerance, 'card ' + i + ' of ' + count);
      }

      // ...and the outermost card sits on it, not somewhere above: only its own cos(tilt) lifts it.
      const edgeTilt = f(f(f((count - 1) * 0.5) * f(degreesPerCard)));
      const outermost = HandFan.Place(count, 0, Spacing, degreesPerCard, DropPixels, CardWidth);
      const expected = f(-CardHeight * 0.5 * Math.cos(edgeTilt * Math.PI / 180.0));
      near(LowestCornerY(outermost), expected, 1e-3, 'outermost of ' + count);
    });
  }
}

test('OneCardRestsUprightInTheMiddle', () => {
  const only = Place(1, 0);
  near(only.X, 0, Tolerance);
  near(only.Y, 0, Tolerance);
  near(only.Degrees, 0, Tolerance);
});

test('TwoCardsLeanAwayFromEachOtherAtTheSameHeight', () => {
  const left = Place(2, 0);
  const right = Place(2, 1);
  near(left.X, -Spacing * 0.5, Tolerance);
  near(right.X, Spacing * 0.5, Tolerance);
  near(left.Degrees, DegreesPerCard * 0.5, Tolerance);
  near(right.Degrees, -DegreesPerCard * 0.5, Tolerance);
  near(left.Y, right.Y, Tolerance);
  assert.ok(left.Y > 0, 'a tilted pair is raised by its corner dip');
});

for (const [count, expected] of [[1, 160], [5, 160], [6, 150], [7, 140], [8, 126]]) {
  test(`TheSpacing_FollowsTheVisualTable(${count})`, () => {
    // battle-visual-v1.md §4.8, with the 160 px the inspector ships for five cards or fewer.
    near(HandFan.Spacing(count, 160), expected, 1e-3);
  });
}

test('TheSpacing_NeverWidens_AsTheHandGrows', () => {
  for (let count = 1; count < HandFan.MostCards; count++) {
    assert.ok(HandFan.Spacing(count + 1, Spacing) <= HandFan.Spacing(count, Spacing), count + ' to ' + (count + 1));
  }
});

test('EightCards_FitTheHandBand', () => {
  // §4.8: fan width = (count - 1) x spacing + the 216 px card; 1098 px inside the 1100 px band.
  const width = f(f((HandFan.MostCards - 1) * HandFan.Spacing(HandFan.MostCards, 160)) + 216);
  near(width, 1098, 1e-2);
  assert.ok(width <= 1100);
});

test('TheSpacing_ScalesWithTheInspectorValue', () => {
  near(HandFan.Spacing(6, 204), (204 * 150) / 160, 1e-3);
  assert.equal(HandFan.Spacing(3, 204), 204);
});

// ---- HandSpecTests.cs ----

const SpecSpacing = 160;
const Degrees = 2.5;

function Top(place) {
  // Back from the centre to the top edge's middle, undoing the tilt (screen y grows downward).
  const radians = place.Degrees * Math.PI / 180.0;
  return f(place.Y - f(f(HandSpec.CardHeight * 0.5) * f(Math.cos(radians))));
}

test('TheMiddleCard_ShowsItsUpper184Px', () => {
  const middle = HandSpec.Rest(5, 2, SpecSpacing, Degrees);
  near(middle.X, 960, 1e-3);
  assert.ok(middle.Degrees === 0);
  near(Top(middle), 896, 1e-3);
  near(1080 - Top(middle), 184, 1e-3);
});

test('SixCards_SitAt150Apart_AroundTheCentre_AndFitTheHandBand', () => {
  const spacing = HandFan.Spacing(6, SpecSpacing);
  const first = HandSpec.Rest(6, 0, spacing, Degrees);
  const last = HandSpec.Rest(6, 5, spacing, Degrees);
  near(spacing, 150, 1e-3);
  near((first.X + last.X) * 0.5, 960, 1e-3, 'centred on x 960');
  near(first.Degrees, 6.25, 1e-3, '2.5 steps out, leaning left');
  near(last.Degrees, -6.25, 1e-3);
  near(HandSpec.Width(6, spacing), 966, 1e-3);
  assert.ok(HandSpec.Width(8, HandFan.Spacing(8, SpecSpacing)) <= 1100, '§4.8: eight still fit the 1100 px band');
});

test('TheKthCard_SitsKSquaredTimes3Point2Lower_TiltedAboutItsBottomEdge', () => {
  const spacing = HandFan.Spacing(6, SpecSpacing);
  for (let i = 0; i < 6; i++) {
    const place = HandSpec.Rest(6, i, spacing, Degrees);
    const step = i - 2.5;
    const radians = place.Degrees * Math.PI / 180.0;
    // The middle of the bottom edge stays where the untilted card would put it.
    const bottomX = f(place.X + f(f(HandSpec.CardHeight * 0.5) * f(Math.sin(radians))));
    const bottomY = f(place.Y + f(f(HandSpec.CardHeight * 0.5) * f(Math.cos(radians))));
    near(bottomX, 960 + step * spacing, 1e-3, 'card ' + i);
    near(bottomY - HandSpec.CardHeight, 896 + step * step * 3.2, 1e-3, 'card ' + i);
  }
});

test('AHoveredCard_StandsUpright_WithItsTopAtY760_AndItsBottom16PxAboveTheEdge', () => {
  const spacing = HandFan.Spacing(6, SpecSpacing);
  const rest = HandSpec.Rest(6, 1, spacing, Degrees);
  const hover = HandSpec.Hover(6, 1, spacing);
  assert.equal(hover.Degrees, 0);
  near(hover.Y - HandSpec.CardHeight * 0.5, 760, 1e-3);
  near(1080 - (hover.Y + HandSpec.CardHeight * 0.5), 16, 1e-3);
  near(hover.X, 960 - 1.5 * spacing, 1e-3, 'it rises above its own place');
  assert.notEqual(rest.Degrees, 0);
});

// ---- port-specific (checked against the C#) ----

test('Port_FanPlace_IsAReadonlyStructOfFloats', () => {
  const p = new FanPlace(0.1, 2, -0.3);
  assert.deepEqual([p.X, p.Y, p.Degrees], [f(0.1), 2, f(-0.3)]);
  assert.ok(Object.isFrozen(p));
  assert.equal(HandFan.Spacing(8, 160), f(160 * f(126 / 160)));
  assert.equal(HandSpec.Width(0, 160), 0);
  assert.equal(HandSpec.Hover(6, 1, 150).Y, 912);
});
