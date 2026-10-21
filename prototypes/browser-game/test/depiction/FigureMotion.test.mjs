// Tests for src/bridge/depictionScript/FigureMotion.js (port of View/Depiction/Script/FigureMotion.cs).
// Ported cases: Assets/View/Depiction/Tests/FigureMotionTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CharacterArt, DefeatShape, EnemyMotionShape, FigureFacing, FigureFrame, FigurePose } from '../../src/bridge/depictionScript/FigureMotion.js';
import { StrikeSystem, UnitSide } from '../../src/bridge/depictionScript/DepictionTypes.js';
import { enumValues } from '../../src/shared/cs.js';

const f = Math.fround;
const near = (a, b, tol, msg) => assert.ok(Math.abs(a - b) <= tol, `${msg || ''} expected ${b} ± ${tol}, got ${a}`);
const Systems = enumValues(StrikeSystem);

// ---- the art's paths ----

test('TheArt_IsFiledUnderTheEnemysId_AsTheIntakeRulesNameIt', () => {
  assert.equal(CharacterArt.PathOf('polearm_warped', FigurePose.Idle), 'Assets/Art/Characters/polearm_warped/chr_polearm_warped_idle_stand.png');
  assert.equal(CharacterArt.PathOf('polearm_warped', FigurePose.Hit), 'Assets/Art/Characters/polearm_warped/chr_polearm_warped_react_hit.png');
  assert.equal(CharacterArt.PathOf('polearm_warped', FigurePose.Down), 'Assets/Art/Characters/polearm_warped/chr_polearm_warped_react_down.png');
  assert.equal(CharacterArt.FileName('polearm_warped', FigurePose.Act), 'chr_polearm_warped_act_', 'any act_* picture');
  assert.throws(() => CharacterArt.Folder(''), { name: 'ArgumentException' });
});

// ---- the frame's width (#302) ----

const RestWidth = 210;
const FrameHeight = 450;

for (const [artWidth, expected, name] of [[640, 210, 'Art640_KeepsTheRestFrame'], [800, 234.375, 'Art800_WidensTo234'], [900, 263.671875, 'Art900_WidensTo264']]) {
  test(`TheFrame_WidensToTheArtsShape_AtTheFrameHeight(${name})`, () => {
    near(FigureFrame.Width(RestWidth, FrameHeight, artWidth, 1536), expected, 1e-3);
  });
}

for (const artWidth of [640, 800, 900]) {
  test(`EveryPicture_StandsOnTheFloorLine_AtFullHeight_InItsFrame(${artWidth})`, () => {
    const width = FigureFrame.Width(RestWidth, FrameHeight, artWidth, 1536);
    near(FigureFrame.Lift(width, FrameHeight, artWidth, 1536), 0, 1e-3);
  });
}

test('WithoutTheWidening_TheWideSpearmenWouldFloat_AsTheIssueMeasured', () => {
  assert.equal(FigureFrame.Lift(RestWidth, FrameHeight, 640, 1536), 0);
  near(FigureFrame.Lift(RestWidth, FrameHeight, 800, 1536), 23.4, 1e-3);
  near(FigureFrame.Lift(RestWidth, FrameHeight, 900, 1536), 45.8, 1e-3);
});

test('APictureWithNoSize_KeepsTheRestFrame', () => {
  assert.equal(FigureFrame.Width(RestWidth, FrameHeight, 0, 1536), RestWidth);
  assert.equal(FigureFrame.Width(RestWidth, FrameHeight, 800, 0), RestWidth);
});

// ---- the height on screen (battle-visual-v1 §4.3, #455) ----

test('AFigure_Stands226Tall_AndATwoCellElite264', () => {
  assert.equal(FigureFrame.HeightFor(1), 226);
  assert.equal(FigureFrame.HeightFor(2), 264);
  near(f(FigureFrame.StandardHeight / 300), 0.75, 0.01, "about 75% of battle_ui_ux_v2's 300 px");
});

test('The450Frame_IsDrawnAtTheHeightOnScreen', () => {
  const scale = FigureFrame.ScaleFor(FigureFrame.StandardHeight, FrameHeight);
  near(f(FrameHeight * scale), 226, 1e-3);
  assert.equal(FigureFrame.ScaleFor(226, 0), 1, 'a frame with no height is left as it is');
});

for (const figureScale of [f(0.5022), f(0.5867), 1]) {
  test(`ACountedChild_KeepsItsScreenPixels_UnderAScaledFigure(${figureScale})`, () => {
    // The target brackets: a 26 px hook drawn inside a figure at figureScale is still 26 px on screen.
    near(f(f(26 * FigureFrame.Counter(figureScale)) * figureScale), 26, 1e-3);
    assert.equal(FigureFrame.Counter(-figureScale), FigureFrame.Counter(figureScale), 'a mirrored parent counts the same');
  });
}

test('ACountedChild_UnderNoScale_IsLeftAsItIs', () => {
  assert.equal(FigureFrame.Counter(0), 1);
});

for (const height of [226, 264]) {
  test(`TheSilhouettesEdgeLight_Is2PxOnScreen_AtEitherHeight(${height})`, () => {
    // battle-visual-v1 §4.3: 縁に 2 px の光, whatever the figure is scaled to.
    const scale = FigureFrame.ScaleFor(height, FrameHeight);
    near(f(FigureFrame.EdgeLightOffset(scale) * scale), 2, 1e-3);
    assert.equal(FigureFrame.EdgeLightOffset(-scale), FigureFrame.EdgeLightOffset(scale), 'the mirrored enemy counts the same');
  });
}

// ---- facing ----

test('Art_IsNeverMirrored_OnlyTheEnemysPlaceholderIs', () => {
  assert.equal(FigureFacing.HomeScaleX(UnitSide.Enemy, true), 1, 'enemy art is drawn facing left');
  assert.equal(FigureFacing.HomeScaleX(UnitSide.Player, true), 1, 'player art is drawn facing right');
  assert.equal(FigureFacing.HomeScaleX(UnitSide.Enemy, false), -1, 'the silhouette is drawn facing right');
  assert.equal(FigureFacing.HomeScaleX(UnitSide.Player, false), 1);
});

test('TheEnemysMove_KeepsItsFacing_ThroughTheMove_AndEndsBackAtRest', () => {
  // The bug #288 names: the move set the scale to plain positive numbers and ended at
  // Vector3.one, so a mirrored enemy turned its back after every action.
  for (const home of [-1, 1]) {
    for (const system of Systems) {
      for (let i = 0; i <= 20; i++) {
        const bell = f(Math.sin((i / 20.0) * Math.PI));
        const { X: x, Y: y } = FigureFacing.During(home, system, bell);
        assert.equal(Math.sign(x), Math.sign(home), system + ' at step ' + i);
        assert.ok(y > 0, system + ' at step ' + i);
      }
      const { X: endX, Y: endY } = FigureFacing.During(home, system, 0);
      assert.equal(endX, home, system + ': back at rest');
      assert.equal(endY, 1, system + ': back at rest');
    }
  }
});

test('TheFourActions_ReadApart_ByHowFarTheyReach', () => {
  assert.ok(EnemyMotionShape.Reach(StrikeSystem.Thrust) > EnemyMotionShape.Reach(StrikeSystem.Strike));
  assert.ok(EnemyMotionShape.Reach(StrikeSystem.Strike) > EnemyMotionShape.Reach(StrikeSystem.Sweep));
  assert.equal(EnemyMotionShape.Reach(StrikeSystem.Shield), 0, 'a guard stands its ground');
});

// ---- the fall ----

test('TheFall_TiltsAwayFromTheOtherSide_Sinks_Darkens_ThenFadesOut', () => {
  // -0 in the C# too (−24 × 0); NUnit's Is.EqualTo(0f) takes it as 0, as === does.
  assert.ok(DefeatShape.Tilt(UnitSide.Enemy, 0) === 0);
  assert.ok(DefeatShape.Tilt(UnitSide.Enemy, 1) < 0, 'the enemy falls back to the right');
  assert.ok(DefeatShape.Tilt(UnitSide.Player, 1) > 0, 'the player falls back to the left');
  assert.ok(DefeatShape.Sink(1) < 0);
  assert.equal(DefeatShape.Brightness(0), 1);
  near(DefeatShape.Brightness(1), DefeatShape.DarkTo, 1e-5);

  assert.equal(DefeatShape.Alpha(0), 1);
  assert.equal(DefeatShape.Alpha(DefeatShape.FadeFrom), 1, 'it falls first, then fades');
  assert.equal(DefeatShape.Alpha(1), 0, 'gone at the end, so the cell is empty');

  let last = 2;
  for (let i = 0; i <= 20; i++) {
    const alpha = DefeatShape.Alpha(f(i / 20));
    assert.ok(alpha <= last);
    last = alpha;
  }
});

// ---- port-specific (checked against the C#) ----

test('Port_TheShapesAreFloat32_AsTheCsComputesThem', () => {
  assert.deepEqual(EnemyMotionShape.Stretch(StrikeSystem.Thrust, 0.5), { X: f(1 + f(f(0.14) * 0.5)), Y: f(1 - f(f(0.05) * 0.5)) });
  assert.deepEqual(EnemyMotionShape.Stretch(StrikeSystem.Slash, 0.5), { X: 1, Y: 1 });
  assert.equal(EnemyMotionShape.Reach(StrikeSystem.Step), 70);
  assert.equal(DefeatShape.Brightness(1), f(1 - f(1 - f(0.35))));
  assert.equal(FigureFrame.Width(210, 450, 900, 1536), 263.671875);
});

test('Port_BadArtIdsAndPoses_ThrowWithDotNetsMessages', () => {
  assert.throws(() => CharacterArt.Folder(null), { name: 'ArgumentException', message: "An art id is required. (Parameter 'id')" });
  assert.throws(() => CharacterArt.FileName('x', 9), { name: 'ArgumentOutOfRangeException', message: "No file is named for this pose. (Parameter 'pose')\nActual value was 9." });
});
