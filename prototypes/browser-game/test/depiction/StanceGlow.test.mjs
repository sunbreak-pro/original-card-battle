// Tests for src/bridge/depictionScript/StanceGlow.js (port of View/Depiction/Script/StanceGlow.cs).
// Ported cases: Assets/View/Depiction/Tests/StanceGlowTests.cs (all; the 構え pip cases read Effects and
// TurnSliceScript) and Assets/View/Depiction/Bridge/Tests/StanceGlowReadoutTests.cs (all: the light
// followed through a real fight's frames from CoreScriptWriter). Cases that read another agent's
// module are skipped, naming it, while it is missing.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { StanceGlow, StanceGlowChange } from '../../src/bridge/depictionScript/StanceGlow.js';
import { StanceList } from '../../src/bridge/depictionScript/StanceList.js';
import { BattleSpeed } from '../../src/bridge/depictionScript/BattleSpeed.js';
import { Cue, CueKind, DepictionEvent, DepictionEventKind, DepictionFrame, UnitFrame, UnitSide } from '../../src/bridge/depictionScript/DepictionTypes.js';

async function load(rel) {
  try { return await import(new URL('../../src/' + rel, import.meta.url).href); } catch { return null; }
}
const Effects = await load('bridge/depictionScript/Effects.js');
const Slice = await load('bridge/depictionScript/TurnSliceScript.js');
const Grammar = await load('bridge/depictionScript/EffectGrammar.js');
const Writer = await load('bridge/depiction/CoreScriptWriter.js');
const Loop = await load('core/battle/TurnLoop.js');
const Rng = await load('core/battle/IRng.js');
const Catalog = await load('core/battle/CardCatalog.js');
const Foes = await load('core/battle/Enemies.js');
const Types = await load('core/battle/Types.js');
const Consts = await load('core/battle/Constants.js');
const needEffects = Effects ? false : 'needs bridge/depictionScript/Effects.js';
const needSlice = !Effects ? 'needs bridge/depictionScript/Effects.js' : !Slice ? 'needs bridge/depictionScript/TurnSliceScript.js' : false;
const readoutMissing = [['bridge/depictionScript/Effects.js', Effects], ['bridge/depictionScript/EffectGrammar.js', Grammar], ['bridge/depiction/CoreScriptWriter.js', Writer],
  ['core/battle/TurnLoop.js', Loop], ['core/battle/IRng.js', Rng], ['core/battle/CardCatalog.js', Catalog], ['core/battle/Enemies.js', Foes], ['core/battle/Types.js', Types], ['core/battle/Constants.js', Consts]]
  .filter(([, m]) => !m).map(([n]) => n);
const needReadout = readoutMissing.length ? 'needs ' + readoutMissing.join(', ') : false;

const Holding = (...placed) => new UnitFrame({ Stances: StanceList.Rows(placed) });

// ---- §5.11 順 2: the light kept while a stance is held ----

test('TheLight_IsOffWhileTheListIsEmpty', () => {
  assert.equal(StanceGlow.IsOn(new UnitFrame()), false);
  assert.equal(StanceGlow.IsOn(Holding()), false);
  assert.equal(StanceGlow.IsOn(new UnitFrame({ Stances: null })), false, 'a source with no list draws no light');
  assert.equal(StanceGlow.IsOn(null), false);
});

test('TheLight_IsOnWhileTheListHoldsAStance', () => {
  assert.equal(StanceGlow.IsOn(Holding('岩の構え')), true);
  assert.equal(StanceGlow.IsOn(Holding('岩の構え', '水の構え', '岩の構え')), true);
});

test('TheLight_ComesOnWithTheFirstStance_StaysUntilTheListEmpties_AndGoesOutThen', () => {
  // The frames a unit goes through: nothing, a stance put down, a second, a repeat, a turn
  // later, and the list emptied. The last is the rule only: no frame within a battle empties the
  // list (battle_core_v4 §4), and a new battle snaps the light off instead of fading it.
  const frames = [
    [Holding(), StanceGlowChange.Keep, false],
    [Holding('岩の構え'), StanceGlowChange.Light, true],
    [Holding('岩の構え', '水の構え'), StanceGlowChange.Keep, true],
    [Holding('岩の構え', '水の構え', '岩の構え'), StanceGlowChange.Keep, true],
    [Holding('岩の構え', '水の構え', '岩の構え'), StanceGlowChange.Keep, true],
    [Holding(), StanceGlowChange.Fade, false],
    [Holding(), StanceGlowChange.Keep, false],
  ];
  let shown = false;
  frames.forEach(([frame, expected, onAfter], i) => {
    const change = StanceGlow.Change(shown, frame);
    assert.equal(change, expected, 'frame ' + i);
    if (change !== StanceGlowChange.Keep) shown = change === StanceGlowChange.Light;
    assert.equal(shown, onAfter, 'frame ' + i);
    assert.equal(shown, StanceGlow.IsOn(frame), 'the light follows the list, frame ' + i);
  });
});

test('ASecondStance_AddsNoSecondLight', () => {
  // §5.11 順 2: 2 つ目からは光を重ねず、紋だけを出し直す.
  assert.equal(StanceGlow.Change(true, Holding('岩の構え', '水の構え')), StanceGlowChange.Keep);
  // The crest drawn for it comes without the crest's own body light, so nothing is laid over the kept light.
  assert.equal(StanceGlow.CrestCarriesLight(true), false);
});

test('TheFirstStance_CrestBringsItsBodyLight', () => {
  // §5.11 順 2 足元に紋、身体に薄い光: the first crest brings the §5.3 型 light as the kept light comes on.
  assert.equal(StanceGlow.CrestCarriesLight(false), true);
  assert.equal(StanceGlow.Change(false, Holding('岩の構え')), StanceGlowChange.Light);
});

test('TheLight_IsAQuarterStrong_AndFadesBesideTheFlowIn300Ms', { skip: needEffects }, () => {
  assert.equal(StanceGlow.Alpha, 0.25);
  const spec = Effects.EffectCatalog.Of(Effects.EffectId.StanceGlow);
  assert.equal(spec.Ms, 300, '§5.11 順 2 300 out');
  assert.equal(spec.Blocking, false, 'the light never lengthens an event');
});

// ---- §5.13 順 1: 構え's pips ----

const TurnEndWithReserve = () => new DepictionEvent({
  Kind: DepictionEventKind.TurnEnd,
  After: new DepictionFrame(),
  Cues: [
    new Cue({ Kind: CueKind.StanceCue, Target: UnitSide.Player, Amount: 3, Text: '+3', GuardAfter: 3 }),
    new Cue({ Kind: CueKind.DiscardHand, Amount: 5 }),
  ],
});

test('TheReserveBeat_GlowsThePipsFirst_ThenTheShieldAndThePlus3', { skip: needEffects }, () => {
  const steps = Effects.EffectPlan.StepsOf(TurnEndWithReserve()).map((s) => s.Id);
  assert.ok(steps.includes(Effects.EffectId.ReservePips));
  const pips = steps.indexOf(Effects.EffectId.ReservePips);
  assert.equal(steps[pips + 1], Effects.EffectId.StanceCue, '順 1 the pips, then 順 2 the shield and +3');
});

test('ThePipGlow_IsWaitedFor_250Ms', { skip: needEffects }, () => {
  const spec = Effects.EffectCatalog.Of(Effects.EffectId.ReservePips);
  assert.equal(spec.Ms, 250, '§5.13 順 1 250');
  assert.equal(spec.StaggerMs, 0, 'the three pips glow together');
  assert.equal(spec.Blocking, true);
  const on = Effects.EffectSwitches.AllOn();
  const pipsOff = Effects.EffectSwitches.AllOn();
  pipsOff.Set(Effects.EffectId.ReservePips, false);
  assert.equal(Effects.EffectPlan.BlockingMs(TurnEndWithReserve(), on) - Effects.EffectPlan.BlockingMs(TurnEndWithReserve(), pipsOff), 250);
});

test('NoReserve_NoPipGlow', { skip: needEffects }, () => {
  // §5.13: under 3 left there is no 構え cue, so no glow and no wait.
  const ev = new DepictionEvent({ Kind: DepictionEventKind.TurnEnd, After: new DepictionFrame(), Cues: [new Cue({ Kind: CueKind.DiscardHand, Amount: 5 })] });
  assert.ok(!Effects.EffectPlan.StepsOf(ev).map((s) => s.Id).includes(Effects.EffectId.ReservePips));
});

test('TheTurnEnd_WithThePipGlowAndAFullHandThrownAway_StillFitsTwoSeconds', { skip: needSlice }, () => {
  const ms = Effects.EffectPlan.BlockingMs(TurnEndWithReserve(), Effects.EffectSwitches.AllOn());
  // 250 (pips) + 460 (shield, +3) + 240 + 4 × 60 (five cards thrown away) + 400 (the enemy's banner).
  assert.equal(ms, 250 + 460 + 480 + 400);
  assert.ok(ms < BattleSpeed.EventCapMs);

  const ends = Slice.TurnSliceScript.Build().Events.filter((e) => e.Kind === DepictionEventKind.TurnEnd);
  assert.equal(ends.length, 1);
  const sliceEnd = ends[0];
  assert.ok(Effects.EffectPlan.StepsOf(sliceEnd).map((s) => s.Id).includes(Effects.EffectId.ReservePips), 'the fixed slice keeps 3 and glows');
  assert.ok(Effects.EffectPlan.BlockingMs(sliceEnd, Effects.EffectSwitches.AllOn()) < BattleSpeed.EventCapMs);
});

// ---- StanceGlowReadoutTests.cs (#501): the light through a real fight's frames ----

const NoShuffle = () => new Rng.FixedRng(0.9999999);

/** Dealt from the front (NoShuffle): 岩の構え, 水の構え, then cards to fill the hand. */
function Deck() {
  const C = Catalog.CardCatalog;
  const I = Types.CardInstance;
  return [
    new I('rock-0', C.RockStance),
    new I('water-0', C.WaterStance),
    new I('kesa-0', C.KesaCut),
    new I('feint-0', C.Feint),
    new I('brace-0', C.Brace),
    new I('kesa-1', C.KesaCut),
    new I('brace-1', C.Brace),
  ];
}

const Setup = () => new Loop.BattleSetup(Foes.Enemies.PolearmWarped, Deck(), Loop.BattleSetup.SliceFieldCells, { StartGap: 2 });

/** The light as the screen keeps it: each landing frame moves it as StanceGlow.Change says. */
class Light {
  constructor() { this.Shown = false; }
  Land(unit) {
    const change = StanceGlow.Change(this.Shown, unit);
    if (change !== StanceGlowChange.Keep) this.Shown = change === StanceGlowChange.Light;
    return change;
  }
}

test('TheBodyLight_IsOnFromTheFirstStanceToTheEndOfTheTurn_AndOffBefore', { skip: needReadout }, () => {
  const { TurnLoop } = Loop;
  const { CoreScriptWriter } = Writer;
  const { FxSystem } = Grammar;
  const state = TurnLoop.Start(Setup(), NoShuffle()).State;
  const writer = new CoreScriptWriter(Foes.Enemies.PolearmWarped);
  const player = new Light();
  const enemy = new Light();

  const opening = writer.Opening(state);
  assert.equal(player.Land(opening.Player), StanceGlowChange.Keep, 'no stance, no light');
  assert.equal(player.Shown, false);

  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  for (const ev of writer.Write(begin.Events, begin.State)) {
    player.Land(ev.After.Player);
    assert.equal(player.Shown, false, 'the turn start puts down no stance: ' + ev.Title);
  }

  // The first stance: the 型 crest is drawn on the player, and the frame after it lights the body.
  const rock = TurnLoop.PlayCard(begin.State, 'rock-0', NoShuffle());
  const placed = writer.Write(rock.Events, rock.State).at(-1);
  assert.ok(placed.Cues.some((c) => c.Target === UnitSide.Player && c.Fx === FxSystem.Form), 'the 型 beat the light opens with (§5.11 順 2 足元に紋、身体に薄い光)');
  assert.equal(StanceGlow.CrestCarriesLight(player.Shown), true, 'the first crest brings its body light');
  assert.equal(player.Land(placed.After.Player), StanceGlowChange.Light);
  assert.equal(enemy.Land(placed.After.Enemy), StanceGlowChange.Keep, "the player's stance lights the player only");

  // A second stance adds no second light: its crest is drawn alone, and the kept light stays as it is.
  const water = TurnLoop.PlayCard(rock.State, 'water-0', NoShuffle());
  const second = writer.Write(water.Events, water.State).at(-1);
  assert.ok(second.Cues.some((c) => c.Target === UnitSide.Player && c.Fx === FxSystem.Form), 'the crest is drawn again');
  assert.equal(StanceGlow.CrestCarriesLight(player.Shown), false, '2 つ目からは光を重ねず、紋だけを出し直す');
  assert.equal(player.Land(second.After.Player), StanceGlowChange.Keep);
  assert.equal(player.Shown, true);

  // The stances last the battle (battle_core_v4 §4): through the turn end and the enemy's turn the light stays.
  const end = TurnLoop.EndTurn(water.State, NoShuffle());
  const enemyTurn = writer.Write(end.Events, end.State);
  assert.ok(enemyTurn.length > 0);
  for (const ev of enemyTurn) {
    assert.equal(player.Land(ev.After.Player), StanceGlowChange.Keep, ev.Title);
    assert.equal(player.Shown, true, 'the light stays while the list holds a stance: ' + ev.Title);
  }

  // The list empties only when a new battle opens: the screen snaps each light to the opening frame.
  const next = new CoreScriptWriter(Foes.Enemies.PolearmWarped).Opening(TurnLoop.Start(Setup(), NoShuffle()).State);
  assert.equal(StanceGlow.IsOn(next.Player), false, 'a new battle snaps the light off');
  assert.equal(StanceGlow.IsOn(next.Enemy), false);
});

test('TheReserveAtTurnEnd_GlowsThePipsBeforeTheShield_AndTheTurnEndFitsTwoSeconds', { skip: needReadout }, () => {
  const { TurnLoop } = Loop;
  const state = TurnLoop.Start(Setup(), NoShuffle()).State;
  const writer = new Writer.CoreScriptWriter(Foes.Enemies.PolearmWarped);
  writer.Opening(state);
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);
  assert.ok(begin.State.Player.Stamina >= Consts.Constants.ReserveThreshold, 'nothing played: 構え holds');

  const end = TurnLoop.EndTurn(begin.State, NoShuffle());
  const turnEnd = writer.Write(end.Events, end.State).find((e) => e.Kind === DepictionEventKind.TurnEnd);
  assert.ok(turnEnd.Cues.some((c) => c.Kind === CueKind.StanceCue));

  const steps = Effects.EffectPlan.StepsOf(turnEnd).map((s) => s.Id);
  const pips = steps.indexOf(Effects.EffectId.ReservePips);
  assert.ok(pips >= 0, '§5.13 順 1: the reserve beat glows pips 1-3');
  assert.equal(steps[pips + 1], Effects.EffectId.StanceCue);
  const ms = Effects.EffectPlan.BlockingMs(turnEnd, Effects.EffectSwitches.AllOn());
  assert.ok(ms < BattleSpeed.EventCapMs);
});
