// Tests for src/bridge/depictionScript/Effects.js (port of View/Depiction/Script/Effects.cs).
// Ported cases: Assets/View/Depiction/Tests/EffectTests.cs (every case but the PileList one, which is
// PileList's; StrikeSystems.Of is held by DepictionTypes.test.mjs), plus the number formats of
// EffectTrace.ToCsv checked against .NET 10's output and the C# Trim of EffectSwitches.WithOff.
//
// The module's own logic runs on the real modules when DepictionTypes' and Effects' siblings are
// ported, else on the two modules with the other agents' imports swapped for stubs holding C#
// constants (BattleSpeed's stub answers the default speed only). The cases that read other agents'
// modules (TurnSliceScript, BattleSpeed's factors) run on the real modules only and are skipped,
// naming the module, while it is missing.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { enumValues } from '../../src/shared/cs.js';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const srcFile = (rel) => resolve(root, 'src', rel);
const dataUrl = (source) => 'data:text/javascript;charset=utf-8,' + encodeURIComponent(source).replace(/'/g, '%27');

async function tryImport(rel) {
  try {
    return { module: await import(pathToFileURL(srcFile(rel)).href), missing: null };
  } catch (e) {
    const why = String(e && e.message ? e.message : e).split('\n')[0];
    const notFound = /Cannot find module '([^']+)'/.exec(why);
    return { module: null, missing: notFound ? relative(root, notFound[1]) : `${rel} (${why})` };
  }
}

/** Imports src/<rel> with some imports swapped: stubs maps a specifier to a source or { url }. */
async function isolated(rel, stubs) {
  const path = srcFile(rel);
  const source = readFileSync(path, 'utf8').replace(/(\bfrom\s+)'([^']+)'/g, (m, head, spec) => {
    if (stubs[spec]) return head + JSON.stringify(typeof stubs[spec] === 'string' ? dataUrl(stubs[spec]) : stubs[spec].url);
    if (spec.startsWith('.')) return head + JSON.stringify(pathToFileURL(resolve(dirname(path), spec)).href);
    return m;
  });
  const url = dataUrl(source + `\n// isolated ${rel} ${Math.random()}\n`);
  return { module: await import(url), url };
}

async function isolatedModules() {
  const cs = pathToFileURL(srcFile('shared/cs.js')).href;
  const types = await isolated('bridge/depictionScript/DepictionTypes.js', {
    './OmenDisclosure.js': 'export const OmenDisclosure = { Unread: 0, Partial: 1, Full: 2 };',
    './PileLayout.js': `import { defineEnum } from '${cs}'; export const PileKind = defineEnum(['Draw', 'Discard', 'Exile']);`,
    './EffectGrammar.js': "export const EffectGrammar = { BlowOf() { throw new Error('stub'); }, ToStrike() { throw new Error('stub'); } };",
  });
  const effects = await isolated('bridge/depictionScript/Effects.js', {
    './DepictionTypes.js': { url: types.url },
    // The written lengths are the lengths at the default speed (BattleSpeed.Default, 1.25 times).
    './BattleSpeed.js': "export const BattleSpeed = { Default: 'Normal', WallMs(ms, step) { if (step !== 'Normal') throw new Error('stub: default speed only'); return ms; } };",
  });
  return { ...types.module, ...effects.module };
}

const realTypes = await tryImport('bridge/depictionScript/DepictionTypes.js');
const realEffects = realTypes.module ? await tryImport('bridge/depictionScript/Effects.js') : realTypes;
const useReal = !!(realTypes.module && realEffects.module);
const M = useReal ? { ...realTypes.module, ...realEffects.module } : await isolatedModules();
const layer = useReal ? 'real' : 'isolated';
const realOnly = useReal ? {} : { skip: `needs ${realEffects.missing}` };
const slice = await tryImport('bridge/depictionScript/TurnSliceScript.js');
const withSlice = !useReal ? realOnly : slice.module ? {} : { skip: `needs ${slice.missing}` };
const speedMod = await tryImport('bridge/depictionScript/BattleSpeed.js');
const withSpeed = !useReal ? realOnly : speedMod.module ? {} : { skip: `needs ${speedMod.missing}` };

const {
  EffectId, EffectSpec, EffectCatalog, EffectSwitches, EffectStep, StatusBeat, EffectStrength, EffectPlan,
  EffectTrace, Entry, _netCustomFixed,
  Cue, CueKind, DepictionEvent, DepictionEventKind, DepictionFrame, UnitSide, RangeSide, StrikeSystem,
} = M;

const ev = (kind, cues = []) => {
  const e = new DepictionEvent({ Kind: kind, After: new DepictionFrame() });
  for (const c of cues) e.Cues.push(c);
  return e;
};
const cue = (fields) => new Cue(fields);
const steps = (e) => EffectPlan.StepsOf(e).map((s) => [s.Id, s.Count]);
const on = () => EffectSwitches.AllOn();

// ---- The catalog ----

test(`[${layer}] EveryEffect_HasALength`, () => {
  const ids = enumValues(EffectId);
  assert.equal(ids.length, 60);
  for (const id of ids) {
    const spec = EffectCatalog.Of(id);
    assert.equal(spec.Id, id);
    assert.ok(spec.Ms > 0, id + ' plays for some time when it is on');
    assert.ok(spec.StaggerMs >= 0, id);
    assert.ok(spec.Source.length > 0, id + ' says where its length comes from');
  }
  assert.equal(EffectCatalog.All.length, ids.length);
  assert.deepEqual(EffectCatalog.All.map((s) => s.Id), ids, 'written in the order of the enum');
  assert.throws(() => EffectCatalog.Of('Nope'), (e) => e.name === 'KeyNotFoundException' && e.message === 'No length is written for effect Nope.');
});

test(`[${layer}] the catalog's sources are the C#'s, character for character`, () => {
  assert.equal(EffectCatalog.Of(EffectId.CardDraw).Source, 'battle_ui_ux_v2 §3.2 ドロー');
  assert.equal(EffectCatalog.Of(EffectId.CardToExile).Source, 'battle_ui_ux_v2 §5.11 順 4 除外の束へ飛ぶ 220 in（拍 600。面の拍と並行）');
  assert.equal(EffectCatalog.Of(EffectId.ReservePips).Source, 'battle_ui_ux_v2 §5.13 順 1 構え ピップ 1〜3 が光る（息）250');
  assert.equal(EffectCatalog.Of(EffectId.Defeat).Source, 'battle_ui_ux_v2 §2.2 の 6 / §5.18 倒れ（800。薄れて消えるまでを含む）');
});

const cardMotion = [
  ['CardDraw', 260, 60, true], ['HandFan', 120, 0, false], ['CardHover', 90, 0, false], ['CardGrab', 90, 0, false],
  ['ReceiverShow', 150, 0, false], ['ThrowLineShow', 150, 0, false], ['ReceiverSnap', 80, 0, false],
  ['CardRelease', 160, 0, true], ['CardToDiscard', 200, 0, false], ['CardToExile', 220, 0, false],
  ['PileRebuild', 200, 0, true], ['CardReturn', 200, 0, false], ['RefusalShake', 160, 0, false],
  ['HandDiscard', 240, 60, true], ['UnpayableDim', 120, 0, false],
];
test(`[${layer}] TheCardMotion_PlaysForTheLengthsTheCanonWrites`, () => {
  for (const [id, ms, stagger, blocking] of cardMotion) {
    const spec = EffectCatalog.Of(id);
    assert.equal(spec.Ms, ms, id);
    assert.equal(spec.StaggerMs, stagger, id);
    assert.equal(spec.Blocking, blocking, id);
  }
});

const attackAndDefence = [
  ['AttackLunge', 100, true], ['StrikeShape', 120, true], ['HitStop', 30, true], ['HitStopStrong', 90, true],
  ['HitFlash', 260, false], ['TargetRecoil', 260, false], ['DamageNumber', 1000, false], ['HpDrain', 300, true],
  ['HpTrail', 500, false], ['ScreenShake', 180, false], ['AttackReturn', 120, true], ['EnemyMotion', 220, true],
  ['OmenSpend', 200, true], ['EnemyReturn', 140, true], ['GuardBlock', 320, true], ['GuardNumber', 1000, false],
  ['GuardBreak', 200, false], ['GuardGain', 260, true],
];
test(`[${layer}] TheAttackAndDefence_PlayForTheirLengths`, () => {
  for (const [id, ms, blocking] of attackAndDefence) {
    assert.equal(EffectCatalog.Of(id).Ms, ms, id);
    assert.equal(EffectCatalog.Of(id).Blocking, blocking, id);
  }
});

test(`[${layer}] TheStrength_IsTwoStepsForNow`, () => {
  assert.equal(EffectStrength.IsStrong(1), false);
  assert.equal(EffectStrength.IsStrong(2), false);
  assert.equal(EffectStrength.IsStrong(3), true);
  assert.equal(EffectStrength.IsStrong(4), true);
  assert.equal(EffectStrength.Uniform, 2);
});

test(`[${layer}] ASlash_WaitsForTheLunge_TheShape_TheStop_TheDrain_AndTheReturn`, () => {
  const direct = ev(DepictionEventKind.PlayCard, [
    cue({ Kind: CueKind.Slash, Source: UnitSide.Player, Target: UnitSide.Enemy, Intensity: 3, HpAfter: 40 }),
  ]);
  assert.deepEqual(steps(direct), [
    ['CardRelease', 1], ['AttackLunge', 1], ['StrikeShape', 1], ['HitStopStrong', 1], ['HpDrain', 1], ['AttackReturn', 1],
  ]);
  assert.equal(EffectPlan.BlockingMs(direct, on()), 160 + 100 + 120 + 90 + 300 + 120);

  // An enemy blow: it steps in (#288), swings, then the shield and the wound, then it draws back.
  const enemy = ev(DepictionEventKind.EnemyAction, [
    cue({ Kind: CueKind.EnemyWindup, Target: UnitSide.Enemy, System: StrikeSystem.Strike }),
    cue({ Kind: CueKind.Slash, Source: UnitSide.Enemy, Target: UnitSide.Player, Intensity: 1 }),
    cue({ Kind: CueKind.GuardBlock, Target: UnitSide.Player, Amount: 3, GuardAfter: 0 }),
    cue({ Kind: CueKind.Hit, Target: UnitSide.Player, Amount: 5, Intensity: 1, HpAfter: 45 }),
  ]);
  assert.deepEqual(steps(enemy), [
    ['EnemyMotion', 1], ['EnemyLunge', 1], ['StrikeShape', 1], ['HitStop', 1],
    ['GuardBlock', 1], ['HpDrain', 1], ['OmenSpend', 1], ['EnemyReturn', 1],
  ]);
  assert.equal(EffectPlan.BlockingMs(enemy, on()), 220 + 100 + 120 + 30 + 320 + 300 + 200 + 140);
});

test(`[${layer}] ABlowAMoveFollows_LeavesOutTheReturn_TheMoveCarriesThePlayerOn`, () => {
  // §5.4 順 2 / §5.5 順 8 (#499): 牽制 hits, then steps back one cell; the step back is the move.
  const blow = cue({ Kind: CueKind.Slash, Source: UnitSide.Player, Target: UnitSide.Enemy, Intensity: 2, HpAfter: 40 });
  const feint = ev(DepictionEventKind.PlayCard, [
    blow,
    cue({ Kind: CueKind.RangeSwitch, Target: UnitSide.Player, PlayerCellAfter: 1, EnemyCellAfter: 4, GapAfter: 2, CellsMoved: 1 }),
  ]);
  assert.equal(EffectPlan.MoveFollows(feint, blow), true);
  assert.deepEqual(steps(feint), [
    ['CardRelease', 1], ['AttackLunge', 1], ['StrikeShape', 1], ['HitStop', 1], ['HpDrain', 1], ['FloorMove', 1], ['GapRecount', 1],
  ]);
  // §5.4 順 2: the move starts on the number + 80; the bar drains on beside it.
  const drains = EffectPlan.StepsOf(feint).filter((s) => s.Id === EffectId.HpDrain);
  assert.equal(drains.length, 1);
  assert.equal(drains[0].LeadMs, EffectPlan.NextFaceLeadMs);
  assert.equal(EffectPlan.BlockingMs(feint, on()), 160 + 100 + 120 + 30 + 80 + 160 + 150);

  // A blow Guard takes part of is three beats; the move starts on the wound's number the same way.
  const wound = cue({ Kind: CueKind.Hit, Target: UnitSide.Enemy, Amount: 5, Intensity: 2, HpAfter: 45 });
  const split = ev(DepictionEventKind.PlayCard, [
    cue({ Kind: CueKind.Slash, Source: UnitSide.Player, Target: UnitSide.Enemy, Intensity: 2 }),
    cue({ Kind: CueKind.GuardBlock, Target: UnitSide.Enemy, Amount: 3, GuardAfter: 0 }),
    wound,
    cue({ Kind: CueKind.RangeSwitch, Target: UnitSide.Player, PlayerCellAfter: 1, EnemyCellAfter: 4, GapAfter: 2, CellsMoved: 1 }),
  ]);
  assert.equal(EffectPlan.MoveFollows(split, wound), true);
  assert.equal(EffectPlan.BlockingMs(split, on()), 160 + 100 + 120 + 30 + 320 + 80 + 160 + 150);

  // Switched off, the drain holds nothing, lead or not.
  const noDrain = EffectSwitches.AllOn();
  noDrain.Set(EffectId.HpDrain, false);
  assert.equal(EffectPlan.BlockingMs(feint, noDrain), 160 + 100 + 120 + 30 + 160 + 150);

  // Anything between the blow and the move (here a status) and the player steps back as usual.
  const first = cue({ Kind: CueKind.Slash, Source: UnitSide.Player, Target: UnitSide.Enemy, Intensity: 2, HpAfter: 40 });
  const apart = ev(DepictionEventKind.PlayCard, [
    first,
    cue({ Kind: CueKind.StatusChange, Target: UnitSide.Enemy, Text: '出血', Amount: 1, StacksAfter: 1 }),
    cue({ Kind: CueKind.RangeSwitch, Target: UnitSide.Player, PlayerCellAfter: 1, EnemyCellAfter: 4, GapAfter: 2, CellsMoved: 1 }),
  ]);
  assert.equal(EffectPlan.MoveFollows(apart, first), false);
  assert.ok(steps(apart).some(([id, n]) => id === 'AttackReturn' && n === 1));
  assert.equal(EffectPlan.MoveFollows(apart, cue({ Kind: CueKind.Slash })), false, 'a cue not in the event');
});

test(`[${layer}] ATraitBeforeTheFirstFace_ShowsBesideTheRelease`, () => {
  // §5.4 順 0 / §5.9 順 1 (#499): the trait's cue starts as the card is released.
  const traited = ev(DepictionEventKind.PlayCard, [
    cue({ Kind: CueKind.StaminaChange, Target: UnitSide.Player, Amount: -1, StaminaAfter: 2, StaminaMax: 3 }),
    cue({ Kind: CueKind.TraitFire, Target: UnitSide.Player, Text: '間合い +3' }),
    cue({ Kind: CueKind.Slash, Source: UnitSide.Player, Target: UnitSide.Enemy, Intensity: 2, HpAfter: 40 }),
  ]);
  const s = EffectPlan.StepsOf(traited);
  assert.equal(s[0].Id, EffectId.CardRelease);
  assert.equal(s[0].LeadMs, 0);
  assert.equal(s[1].Id, EffectId.TraitFire);
  assert.equal(EffectPlan.BlockingMs(traited, on()), 200 + 100 + 120 + 30 + 300 + 120);

  // A trait cue after the first face (a stance set, a bonus on the Guard) is its own beat as before.
  const later = ev(DepictionEventKind.PlayCard, [
    cue({ Kind: CueKind.GuardGain, Target: UnitSide.Player, Amount: 3, GuardAfter: 3 }),
    cue({ Kind: CueKind.TraitFire, Target: UnitSide.Player, Text: '連動 +2' }),
  ]);
  assert.equal(EffectPlan.StepsOf(later)[0].LeadMs, null);
  assert.equal(EffectPlan.BlockingMs(later, on()), 160 + 260 + 200);
});

test(`[${layer}] AFall_IsItsOwnBeat_OfEightHundredMs`, () => {
  const fall = ev(DepictionEventKind.Defeat, [cue({ Kind: CueKind.Defeat, Target: UnitSide.Enemy })]);
  assert.deepEqual(steps(fall), [['Defeat', 1]]);
  assert.equal(EffectPlan.BlockingMs(fall, on()), 800);
  assert.equal(EffectPlan.BlockingMs(fall, EffectSwitches.AllOff()), 0, 'switched off, the figure is gone at once');
});

// ---- Status and turn (#77) ----

const statusAndTurn = [
  ['StatusApply', 200, true], ['StatusStack', 150, true], ['StatusTick', 120, true], ['StatusVanish', 150, true],
  ['OmenBlink', 250, false], ['OmenExecute', 200, false], ['PushMark', 1000, false], ['TurnBanner', 300, true],
  ['EnemyTurnBanner', 400, true], ['StaminaRecover', 80, true], ['ResultCard', 400, false],
];
test(`[${layer}] TheStatusAndTurnBeats_PlayForTheirLengths`, () => {
  for (const [id, ms, blocking] of statusAndTurn) {
    assert.equal(EffectCatalog.Of(id).Ms, ms, id);
    assert.equal(EffectCatalog.Of(id).Blocking, blocking, id);
  }
});

test(`[${layer}] AStatusChange_IsOneOfTheFourChipBeats`, () => {
  for (const [change, stacksAfter, beat] of [
    [2, 2, 'StatusApply'], // a new word: 0 → 2
    [1, 3, 'StatusStack'], // more of a word already there
    [-1, 1, 'StatusTick'], // one stack less at the turn start
    [-1, 0, 'StatusVanish'], // the last one gone
  ]) {
    const c = cue({ Kind: CueKind.StatusChange, Target: UnitSide.Enemy, Text: '鈍足', Amount: change, StacksAfter: stacksAfter });
    assert.equal(StatusBeat.Of(c), beat, `${change} → ${stacksAfter}`);
  }
  assert.throws(() => StatusBeat.Of(null), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'cue')");
});

test(`[${layer}] TheTurn_OpensWithItsBanner_AndHandsOverWithTheEnemysBanner`, () => {
  const start = ev(DepictionEventKind.TurnStart, [
    cue({ Kind: CueKind.StaminaChange, Target: UnitSide.Player, Amount: 3, StaminaAfter: 10, StaminaMax: 10 }),
    cue({ Kind: CueKind.DrawHand, Target: UnitSide.Player, Amount: 5 }),
  ]);
  assert.deepEqual(steps(start), [['TurnBanner', 1], ['StaminaRecover', 3], ['CardDraw', 5]]);
  assert.equal(EffectPlan.BlockingMs(start, on()), 300 + 3 * 80 + 500, 'the pips light 80 ms apart');

  const end = ev(DepictionEventKind.TurnEnd, [cue({ Kind: CueKind.DiscardHand, Target: UnitSide.Player, Amount: 2 })]);
  assert.deepEqual(steps(end).at(-1), ['EnemyTurnBanner', 1]);
});

test(`[${layer}] APerCardEffect_StartsEachNextCardOneStaggerLater`, () => {
  const draw = EffectCatalog.Of(EffectId.CardDraw);
  assert.equal(draw.TotalMs(5), 260 + 4 * 60, 'five cards: the last leaves after four staggers');
  assert.equal(draw.TotalMs(1), 260);
  assert.equal(draw.TotalMs(0), 0, 'nothing drawn, nothing waited for');
  assert.equal(draw.TotalMs(-3), 0);
  assert.equal(EffectCatalog.Of(EffectId.HandDiscard).TotalMs(8), 240 + 7 * 60);
});

test(`[${layer}] a spec keeps C# floats: lengths and sums are singles`, () => {
  const odd = new EffectSpec(EffectId.CardDraw, 0.1, 0.1, true, 'x');
  assert.equal(odd.Ms, Math.fround(0.1));
  assert.equal(odd.TotalMs(3), Math.fround(Math.fround(0.1) + Math.fround(Math.fround(0.1) * 2)));
  assert.equal(EffectCatalog.CardToExileDelayMs, 600);
  assert.equal(EffectPlan.NextFaceLeadMs, 80);
});

// ---- Switches ----

test(`[${layer}] EveryEffect_IsOnUntilSwitchedOff_AndOffWaitsForNothing`, () => {
  const switches = EffectSwitches.AllOn();
  for (const id of enumValues(EffectId)) {
    assert.equal(switches.IsOn(id), true, id);
    assert.equal(switches.WaitMs(id, 3), EffectCatalog.Of(id).TotalMs(3), id);
    assert.equal(switches.WaitMs(id), EffectCatalog.Of(id).TotalMs(1), id + ': one item by default');
    switches.Set(id, false);
    assert.equal(switches.IsOn(id), false, id);
    assert.equal(switches.Ms(id), 0, id + ': off animates for 0 ms');
    assert.equal(switches.StaggerMs(id), 0, id);
    assert.equal(switches.WaitMs(id, 3), 0, id + ': off holds nothing up');
    switches.Set(id, true);
    assert.equal(switches.IsOn(id), true, id + ' can be switched back on');
  }
  const off = EffectSwitches.AllOff();
  for (const id of enumValues(EffectId)) assert.equal(off.IsOn(id), false, id);
});

test(`[${layer}] TheInspectorList_SwitchesOffByName_AndHandsBackWhatItDoesNotKnow`, () => {
  const r = EffectSwitches.WithOff(['CardDraw', ' HandDiscard ', '', 'CardDrw', '7', null]);
  const switches = r.ok;
  assert.equal(r.switches, switches);
  assert.equal(r.unknown, r.value);
  assert.equal(switches.IsOn(EffectId.CardDraw), false);
  assert.equal(switches.IsOn(EffectId.HandDiscard), false, 'surrounding spaces are forgiven');
  assert.equal(switches.IsOn(EffectId.CardRelease), true, '"7" is not a name, so nothing at 7 is switched off');
  assert.deepEqual(r.value, ['CardDrw', '7']);

  const none = EffectSwitches.WithOff(null);
  assert.equal(none.ok.IsOn(EffectId.CardDraw), true);
  assert.deepEqual(none.value, []);
});

test(`[${layer}] the Inspector list trims as C# does: U+0085 is white space, U+FEFF is not`, () => {
  const r = EffectSwitches.WithOff(['\u0085HpDrain　', '﻿HpTrail', '\tcardDraw\n']);
  assert.equal(r.ok.IsOn(EffectId.HpDrain), false);
  assert.equal(r.ok.IsOn(EffectId.HpTrail), true);
  assert.deepEqual(r.value, ['﻿HpTrail', 'cardDraw'], 'names are matched case by case');
});

// ---- What an event waits for ----

test(`[${layer}] ADrawThatRanThePileOut_HoldsTheRebuildBeforeTheCards_AndKeepsOneStagger`, () => {
  const e = new DepictionEvent({ Kind: DepictionEventKind.TurnStart });
  e.Cues.push(cue({ Kind: CueKind.DrawHand, Target: UnitSide.Player, Amount: 8, RebuildAfter: 2 }));
  const s = EffectPlan.StepsOf(e);
  assert.deepEqual(s.map((x) => [x.Id, x.Count]), [['TurnBanner', 1], ['PileRebuild', 1], ['CardDraw', 8]]);
  const draw = s.slice(1).reduce((acc, x) => Math.fround(acc + EffectPlan.WaitMs(x, on())), 0);
  assert.equal(draw, 260 + 7 * 60 + 200, '§5.1: the worst draw is 880 ms');

  const plain = new DepictionEvent({ Kind: DepictionEventKind.TurnStart });
  plain.Cues.push(cue({ Kind: CueKind.DrawHand, Target: UnitSide.Player, Amount: 6 }));
  assert.ok(!EffectPlan.StepsOf(plain).some((x) => x.Id === EffectId.PileRebuild));
});

test(`[${layer}] TheRebuildSlide_ComesWhenTheOldPileIsEmpty_AndTheDrawEndsOnThePlan`, () => {
  for (const rebuildAfter of [0, 2, 7]) {
    const sw = EffectSwitches.AllOn();
    const n = 8;
    const slide = EffectPlan.RebuildStartMs(rebuildAfter, sw);
    assert.equal(slide, rebuildAfter * 60, 'one stagger after the old pile\'s last card');
    for (let i = 0; i < rebuildAfter; i++) assert.ok(EffectPlan.DrawCardStartMs(i, rebuildAfter, sw) < slide, 'card ' + i);
    assert.equal(EffectPlan.DrawCardStartMs(rebuildAfter, rebuildAfter, sw), slide + 200, 'the new pile\'s first card waits for the slide');
    const lastEnds = EffectPlan.DrawCardStartMs(n - 1, rebuildAfter, sw) + EffectCatalog.Of(EffectId.CardDraw).Ms;
    assert.equal(lastEnds, 260 + (n - 1) * 60 + 200);
    assert.equal(lastEnds, sw.WaitMs(EffectId.PileRebuild) + sw.WaitMs(EffectId.CardDraw, n));
    assert.equal(EffectPlan.DrawCardStartMs(n - 1, Cue.Unchanged, sw), (n - 1) * 60, 'a plain draw keeps one stagger');
  }
  assert.equal(EffectPlan.RebuildStartMs(-1, on()), 0);
  assert.equal(EffectPlan.DrawCardStartMs(-2, -1, on()), 0);
  assert.throws(() => EffectPlan.RebuildStartMs(0, null), (e) => e.name === 'ArgumentNullException');
  assert.throws(() => EffectPlan.DrawCardStartMs(0, 0, null), (e) => e.name === 'ArgumentNullException');
});

test(`[${layer}] AStanceCard_LeavesForTheExilePile_OnTheBeat600`, () => {
  assert.equal(EffectCatalog.CardToExileDelayMs, 300 + 300);
  assert.equal(EffectCatalog.Of(EffectId.CardToExile).Blocking, false);
  assert.equal(new DepictionEvent().PlayedTo, 'Discard', 'a card goes to the discard pile unless the core exiles it');
});

test(`[${layer}] AStepsWait_IsItsCatalogLengthForItsCount_AndNothingWhenSwitchedOff`, () => {
  assert.equal(EffectPlan.WaitMs(new EffectStep(EffectId.CardDraw, 5), on()), 260 + 4 * 60);
  assert.equal(EffectPlan.WaitMs(new EffectStep(EffectId.StaminaRecover, 3), on()), 3 * 80);
  assert.equal(EffectPlan.WaitMs(new EffectStep(EffectId.GuardBlock, 1), on()), 320);
  assert.equal(EffectPlan.WaitMs(new EffectStep(EffectId.CardDraw, 5), EffectSwitches.AllOff()), 0);
  // A lead holds the flow for the lead only, or the whole effect if that is shorter.
  assert.equal(EffectPlan.WaitMs(new EffectStep(EffectId.HpDrain, 1, 80), on()), 80);
  assert.equal(EffectPlan.WaitMs(new EffectStep(EffectId.HitStop, 1, 80), on()), 30);
  assert.equal(new EffectStep(EffectId.HpDrain, 1).LeadMs, null);
  assert.throws(() => new EffectStep(EffectId.HpDrain, 1, -1), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message === "Specified argument was out of the range of valid values. (Parameter 'leadMs')");
  assert.throws(() => EffectPlan.WaitMs(null, on()), (e) => e.message === "Value cannot be null. (Parameter 'step')");
  assert.throws(() => EffectPlan.WaitMs(new EffectStep(EffectId.HpDrain, 1), null), (e) => e.message === "Value cannot be null. (Parameter 'switches')");
});

test(`[${layer}] AMoveWithoutAFloor_Waits320_AndFlipsTheTagFor150`, () => {
  assert.equal(EffectCatalog.Of(EffectId.RangeSwitch).Ms, 320);
  assert.equal(EffectCatalog.Of(EffectId.RangeTagFlip).Ms, 150);
  assert.equal(EffectCatalog.Of(EffectId.RangeTagFlip).Blocking, true);
  const slot = ev(DepictionEventKind.PlayCard, [
    cue({ Kind: CueKind.RangeSwitch, Target: UnitSide.Player, RangeAfter: RangeSide.Far, RangeGlyphAfter: '遠' }),
  ]);
  assert.equal(EffectPlan.MovesOnFloor(slot.Cues[0]), false);
  assert.deepEqual(steps(slot), [['CardRelease', 1], ['RangeSwitch', 1], ['RangeTagFlip', 1]]);
  assert.equal(EffectPlan.BlockingMs(slot, on()), 160 + 320 + 150);
});

test(`[${layer}] AMoveOnTheFloor_Walks160ACell_ThenCountsTheGapAgainFor150`, () => {
  assert.equal(EffectCatalog.Of(EffectId.FloorMove).TotalMs(1), 160);
  assert.equal(EffectCatalog.Of(EffectId.FloorMove).TotalMs(2), 320);
  assert.equal(EffectCatalog.Of(EffectId.GapRecount).Ms, 150);
  assert.equal(EffectCatalog.Of(EffectId.FloorMove).Blocking, true);
  assert.equal(EffectCatalog.Of(EffectId.GapRecount).Blocking, true);
  for (const [cells, ms] of [[1, 310], [2, 470]]) {
    const move = ev(DepictionEventKind.EnemyAction, [
      cue({ Kind: CueKind.RangeSwitch, Target: UnitSide.Player, PlayerCellAfter: 2, EnemyCellAfter: 5, GapAfter: 2, CellsMoved: cells }),
    ]);
    assert.equal(EffectPlan.MovesOnFloor(move.Cues[0]), true);
    assert.deepEqual(steps(move), [['FloorMove', cells], ['GapRecount', 1]]);
    assert.equal(EffectPlan.StepsOf(move).reduce((a, x) => Math.fround(a + EffectPlan.WaitMs(x, on())), 0), ms, cells + ' cells');
  }
});

test(`[${layer}] AMoveOnTheFloor_ThatDoesNotSayHowFar_Stops`, () => {
  const move = ev(DepictionEventKind.EnemyAction, [
    cue({ Kind: CueKind.RangeSwitch, Target: UnitSide.Player, PlayerCellAfter: 2, EnemyCellAfter: 5, GapAfter: 2 }),
  ]);
  assert.throws(() => EffectPlan.StepsOf(move), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message === "A move on the floor must say how many cells it walks (Cue.CellsMoved). (Parameter 'cue')\nActual value was 0.");
  assert.throws(() => EffectPlan.CellsOf(cue({ Kind: CueKind.RangeSwitch })), (e) => e.name === 'ArgumentException'
    && e.message === "A move without a floor walks no cells. (Parameter 'cue')");
});

test(`[${layer}] NoCardMoving_IsNoBeat`, () => {
  const e = ev(DepictionEventKind.TurnEnd, [
    cue({ Kind: CueKind.DrawHand, Target: UnitSide.Player, Amount: 0 }),
    cue({ Kind: CueKind.DiscardHand, Target: UnitSide.Player, Amount: 0 }),
  ]);
  assert.deepEqual(steps(e), [['EnemyTurnBanner', 1]], 'the screen flies no card and waits for none');
});

test(`[${layer}] ASlashWithNoSource_IsSwungByTheSideOppositeItsTarget_OnScreenAndInThePlan`, () => {
  const atEnemy = cue({ Kind: CueKind.Slash, Target: UnitSide.Enemy, Intensity: 2, HpAfter: 40 });
  const atPlayer = cue({ Kind: CueKind.Slash, Target: UnitSide.Player, Intensity: 2 });
  assert.equal(EffectPlan.AttackerOf(atEnemy), UnitSide.Player);
  assert.equal(EffectPlan.AttackerOf(atPlayer), UnitSide.Enemy);
  assert.equal(EffectPlan.AttackerOf(cue({ Kind: CueKind.Slash, Source: UnitSide.Enemy, Target: UnitSide.Enemy })), UnitSide.Enemy, 'a named Source wins');
  const e = ev(DepictionEventKind.EnemyAction, [atEnemy]);
  assert.deepEqual(steps(e), [['AttackLunge', 1], ['StrikeShape', 1], ['HitStop', 1], ['HpDrain', 1], ['AttackReturn', 1]]);
});

test(`[${layer}] EveryCueKind_HasAPlan`, () => {
  for (const kind of enumValues(CueKind)) {
    const e = ev(DepictionEventKind.EnemyAction, [cue({ Kind: kind, Source: UnitSide.Enemy, Target: UnitSide.Player, Amount: 1, Intensity: 1 })]);
    assert.doesNotThrow(() => EffectPlan.StepsOf(e), kind);
  }
  const bad = ev(DepictionEventKind.EnemyAction, [cue({ Kind: 'Wobble' })]);
  assert.throws(() => EffectPlan.StepsOf(bad), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message === "No effect is planned for this cue. (Parameter 'cue')\nActual value was Wobble.");
  assert.throws(() => EffectPlan.StepsOf(null), (e) => e.message === "Value cannot be null. (Parameter 'ev')");
  assert.throws(() => EffectPlan.BlockingMs(null, null), (e) => e.message === "Value cannot be null. (Parameter 'switches')", 'switches is checked first');
});

// ---- The fixed slice (TurnSliceScript) ----

const cardMotionIds = new Set(['CardDraw', 'HandFan', 'CardHover', 'CardGrab', 'ReceiverShow', 'ThrowLineShow', 'ReceiverSnap',
  'CardRelease', 'CardToDiscard', 'CardReturn', 'RefusalShake', 'HandDiscard', 'UnpayableDim']);
const cardSteps = (e) => steps(e).filter(([id]) => cardMotionIds.has(id));

test('TheFixedSlice_WaitsForTheDrawsTheReleasesAndTheDiscard', withSlice, () => {
  const events = slice.module.TurnSliceScript.Build().Events;
  assert.deepEqual(cardSteps(events[0]), [['CardDraw', 5]]);
  for (const p of events.filter((e) => e.Kind === DepictionEventKind.PlayCard)) {
    assert.deepEqual(cardSteps(p), [['CardRelease', 1]], 'event ' + p.Order);
    assert.deepEqual(steps(p)[0], ['CardRelease', 1], 'the release comes before the card\'s beats');
  }
  const ends = events.filter((e) => e.Kind === DepictionEventKind.TurnEnd);
  assert.equal(ends.length, 1);
  assert.deepEqual(cardSteps(ends[0]), [['HandDiscard', 2]]);
});

test('AnEventsLength_IsTheSumOfThePlansStepWaits', withSlice, () => {
  const oneOff = EffectSwitches.AllOn();
  oneOff.Set(EffectId.HpDrain, false);
  for (const switches of [EffectSwitches.AllOn(), oneOff, EffectSwitches.AllOff()]) {
    for (const e of slice.module.TurnSliceScript.Build().Events) {
      const held = EffectPlan.StepsOf(e).reduce((a, x) => Math.fround(a + EffectPlan.WaitMs(x, switches)), 0);
      assert.equal(EffectPlan.BlockingMs(e, switches), held, 'event ' + e.Order + ' ' + e.Title);
    }
  }
});

test('SwitchingAnyEffectOff_NeverLengthensAnEvent_AndAllOff_TheTurnStillPlaysToTheEnd', withSlice, () => {
  const events = slice.module.TurnSliceScript.Build().Events;
  assert.ok(events.length > 3);
  for (const id of enumValues(EffectId)) {
    const oneOff = EffectSwitches.AllOn();
    oneOff.Set(id, false);
    for (const e of events) {
      const full = EffectPlan.BlockingMs(e, EffectSwitches.AllOn());
      const off = EffectPlan.BlockingMs(e, oneOff);
      assert.ok(off >= 0 && off <= full, id + ' off, event ' + e.Order);
    }
  }
  const allOff = EffectSwitches.AllOff();
  for (const e of events) {
    assert.equal(EffectPlan.BlockingMs(e, allOff), 0, 'event ' + e.Order);
    assert.notEqual(e.After, null, 'event ' + e.Order);
  }
});

test('EveryEvent_FitsInsideTheTwoSecondBudget', withSlice, () => {
  for (const e of slice.module.TurnSliceScript.Build().Events) {
    assert.ok(EffectPlan.BlockingMs(e, on()) < 2000, 'event ' + e.Order + ' ' + e.Title);
  }
});

test('BlockingMs at a battle speed is the wall time of the plain one (#348)', withSpeed, () => {
  const { BattleSpeed, BattleSpeedStep } = speedMod.module;
  const fall = ev(DepictionEventKind.Defeat, [cue({ Kind: CueKind.Defeat, Target: UnitSide.Enemy })]);
  assert.equal(EffectPlan.BlockingMs(fall, on(), BattleSpeedStep.Normal), 800);
  assert.equal(EffectPlan.BlockingMs(fall, on(), BattleSpeedStep.Slow), BattleSpeed.WallMs(800, BattleSpeedStep.Slow));
  assert.equal(EffectPlan.BlockingMs(fall, on(), BattleSpeedStep.Slow), 1000);
});

// ---- The trace #78 reads ----

test(`[${layer}] TheTrace_TimesWhatPlayed_AndMarksWhatWasSwitchedOff`, () => {
  let now = 10.0;
  const trace = new EffectTrace(() => now);
  const draw = trace.Begin(EffectId.CardDraw, 1, 5);
  now += 0.52;
  trace.End(draw);
  trace.End(draw); // a second End changes nothing
  trace.Skip(EffectId.CardRelease, 2);
  const open = trace.Begin(EffectId.HandFan, 2);

  assert.equal(trace.Entries.length, 3);
  const first = trace.Entries[0];
  assert.ok(first instanceof Entry && first instanceof EffectTrace.Entry);
  assert.equal(first.Id, EffectId.CardDraw);
  assert.equal(first.EventOrder, 1);
  assert.ok(Math.abs(first.Seconds - 0.52) < 1e-9);
  assert.equal(first.NominalMs, 500);
  assert.equal(first.Finished, true);
  assert.equal(trace.Entries[1].Skipped, true);
  assert.equal(trace.Entries[1].Seconds, 0);
  assert.equal(trace.Entries[open].Finished, false, 'still playing');
  assert.equal(trace.Entries[open].NominalMs, 120);

  const lines = trace.ToCsv().replace(/\n+$/, '').split('\n');
  assert.equal(lines[0], 'effect,event,start_s,measured_ms,nominal_ms,skipped');
  assert.equal(lines[1], 'CardDraw,1,10.000,520,500,0');
  assert.ok(lines[2].startsWith('CardRelease,2,') && lines[2].endsWith(',1'));
  assert.equal(lines[2], 'CardRelease,2,10.520,0,0,1');
  assert.equal(lines[3], 'HandFan,2,10.520,0,120,0');
  assert.throws(() => trace.End(9), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message === "Specified argument was out of the range of valid values. (Parameter 'handle')");
  assert.throws(() => trace.End(-1), (e) => e.name === 'ArgumentOutOfRangeException');
  assert.throws(() => new EffectTrace(null), (e) => e.message === "Value cannot be null. (Parameter 'clock')");
  assert.equal(new Entry().Id, EffectId.CardDraw, 'C#: default(EffectId)');
});

test('TheTrace_RecordsTheNominalLength_AtTheSpeedInForce', withSpeed, () => {
  const { BattleSpeed, BattleSpeedStep } = speedMod.module;
  const trace = new EffectTrace(() => 0.0);
  assert.equal(trace.Speed, BattleSpeed.Default);
  trace.Begin(EffectId.CardDraw, 1, 5);
  trace.Speed = BattleSpeedStep.Slow;
  trace.Begin(EffectId.CardDraw, 1, 5);
  trace.Speed = BattleSpeedStep.Fast;
  trace.Begin(EffectId.CardDraw, 1, 5);
  assert.equal(trace.Entries[0].NominalMs, 500, '1.25 times is the written length');
  assert.ok(Math.abs(trace.Entries[1].NominalMs - 625) <= 0.01, '1.0 times: 500 x 1.25');
  assert.ok(Math.abs(trace.Entries[2].NominalMs - 416.67) <= 0.01, '1.5 times: 500 x 1.25 / 1.5');
  assert.ok(trace.ToCsv().split('\n')[3].endsWith(',417,0'), 'a float nominal is written to 7 digits, then rounded');
});

test(`[${layer}] ToCsv writes numbers as .NET's "0.000" / "0" custom formats do (checked against .NET 10)`, () => {
  // [value, "0.000" (double), "0" (double)] as `dotnet` printed them.
  const doubles = [
    [-0.0001, '-0.000', '-0'], [-0, '-0.000', '-0'], [0, '0.000', '0'], [1.0005, '1.001', '1'], [0.0005, '0.001', '0'],
    [2.5, '2.500', '3'], [3.5, '3.500', '4'], [-2.5, '-2.500', '-3'], [1234567890123455, '1234567890123460.000', '1234567890123460'],
    [10, '10.000', '10'], [520, '520.000', '520'], [1e-7, '0.000', '0'], [1e21, '1000000000000000000000.000', '1000000000000000000000'],
    [123456789.0005, '123456789.001', '123456789'], [0.0015, '0.002', '0'], [NaN, 'NaN', 'NaN'], [Infinity, 'Infinity', 'Infinity'],
    [-Infinity, '-Infinity', '-Infinity'], [4.999999999999999, '5.000', '5'], [1.2345678901234568e-5, '0.000', '0'],
    [1234567890123465, null, '1234567890123460'], [1234567890123475, '1234567890123480.000', null], [2.0, '2.000', null],
  ];
  for (const [x, three, zero] of doubles) {
    if (three != null) assert.equal(_netCustomFixed(x, 3, 15), three, `${x} "0.000"`);
    if (zero != null) assert.equal(_netCustomFixed(x, 0, 15), zero, `${x} "0"`);
  }
  assert.equal(_netCustomFixed(0.125, 2, 15), '0.13');
  assert.equal(_netCustomFixed(0.375, 2, 15), '0.38');
  assert.equal(_netCustomFixed(-1.5, 0, 15), '-2');
  // Floats: 7 significant digits first (ties to even), then "0".
  const floats = [
    [416.66666, '417'], [0.5, '1'], [1.5, '2'], [2.5, '3'], [-0.5, '-1'], [1234567.5, '1234568'], [1234568.5, '1234568'],
    [16777215, '16777220'], [625, '625'], [33554432, '33554430'], [-0, '-0'], [0.00001, '0'],
  ];
  for (const [x, zero] of floats) assert.equal(_netCustomFixed(Math.fround(x), 0, 7), zero, `${x}f "0"`);
});
