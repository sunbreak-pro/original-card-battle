// JS extension — 俊敏's free cell through the depiction source (battle_core_v4.md §5 #48, battle_ui_ux_v2 §3.8).
// The C# CoreBattleSource has no way to take it; CoreBattleSource.CanTakeFreeStep(dir) / TakeFreeStep(dir)
// (src/bridge/depiction/CoreBattleSource.js) add one. These tests hold the extension to the core: a card that
// puts 俊敏 on the player opens the step at the next turn start, the step is one screen event written by the
// same writer as every other move, and the frame it settles on says what the core's state says.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { CoreBattleSource } from '../../src/bridge/depiction/CoreBattleSource.js';
import { CoreText } from '../../src/bridge/depiction/CoreText.js';
import { CueKind, DepictionText, PlayVerdict } from '../../src/bridge/depictionScript/DepictionTypes.js';
import { EffectId, EffectPlan } from '../../src/bridge/depictionScript/Effects.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { BattleSetup, TurnLoop } from '../../src/core/battle/TurnLoop.js';
import { PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { CellsMoved, FreeStepTaken, MoveBlocked } from '../../src/core/battle/BattleEvents.js';
import {
  Actor, BattleAttribute, CardDef, CardInstance, Face, StatusGrant, StatusKind, TargetKind,
} from '../../src/core/battle/Types.js';

/** A card of the test's own: 俊敏 2 on the player (no card of the catalog puts 俊敏 on the player yet). */
const Quicken = new CardDef('quicken', '身軽', BattleAttribute.Skill, 1,
  new Face({ Statuses: [new StatusGrant(StatusKind.Swift, 2, true)] }), null, TargetKind.Self, '');

/** 20 cards: six 身軽 and the prototype deck's kinds; seed 7 deals two 身軽 into the first hand. */
function DeckWithQuicken() {
  const deck = [];
  for (let i = 0; i < 6; i++) deck.push(new CardInstance('quicken-' + i, Quicken));
  const kinds = PrototypeDeck.Kinds;
  for (let i = 0; deck.length < 20; i++) deck.push(new CardInstance('p-' + i, kinds[i % kinds.length]));
  return deck;
}

function Source({ more = null, cells = 7, seed = 7 } = {}) {
  const setup = new BattleSetup(Enemies.PolearmWarped, DeckWithQuicken(), cells, { MoreEnemies: more });
  return new CoreBattleSource(setup, seed);
}

function ToPlayer(source) {
  let guard = 0;
  while (!source.WaitingForPlayer && !source.Finished && guard++ < 200) source.AdvanceAuto();
  assert.equal(source.WaitingForPlayer, true, 'the battle waits for the player');
}

/** Turn 1: play 身軽 (俊敏 2), end the turn, play the enemy's turn up to the player's turn 2. */
function ToSwiftTurn(source) {
  ToPlayer(source);
  const face = source.Frame.Hand.find((f) => f.Id.startsWith('quicken'));
  assert.ok(face, 'a 身軽 is in the first hand');
  const r = source.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim));
  assert.equal(r.verdict, PlayVerdict.Accepted);
  source.EndTurn();
  ToPlayer(source);
}

/** The settled frame against the core's state: the floor, every enemy's N, the hand, the 俊敏 chip. */
function AssertFrameMatchesCore(source) {
  const s = source.State;
  const f = source.Frame;
  assert.equal(f.Floor.PlayerCell, s.Player.Cell, 'Floor.PlayerCell');
  assert.equal(f.Floor.Gap, s.GapTo(s.Nearest), 'Floor.Gap is N to the nearest standing enemy');
  for (const uf of f.Enemies) {
    assert.equal(uf.Cell, s.Enemies[uf.Unit].Body.Cell, 'enemy ' + uf.Unit + ' cell');
    if (!uf.Down) assert.equal(uf.Gap, s.GapTo(uf.Unit), 'enemy ' + uf.Unit + ' N');
  }
  assert.deepEqual(f.Hand.map((c) => c.Id), s.Hand.map((c) => c.InstanceId), 'the hand');
  const chip = f.Player.Statuses.find((c) => c.Label === '俊敏');
  const stacks = s.Player.Statuses.Stacks(StatusKind.Swift);
  if (stacks > 0) assert.equal(chip && chip.Stacks, stacks, 'the 俊敏 chip'); else assert.equal(chip, undefined);
}

test('FreeStep_IsClosed_WithoutSwift', () => {
  const source = Source();
  ToPlayer(source);
  assert.equal(TurnLoop.CanTakeFreeStep(source.State), false);
  assert.equal(source.CanTakeFreeStep(), false);
  assert.equal(source.CanTakeFreeStep(1), false);
  assert.equal(source.CanTakeFreeStep(-1), false);
  assert.throws(() => source.TakeFreeStep(1), (e) => e.name === 'InvalidOperationException');
});

test('FreeStep_ASwiftCard_OpensTheStep_AtTheNextTurnStart', () => {
  const source = Source();
  ToPlayer(source);
  const face = source.Frame.Hand.find((f) => f.Id.startsWith('quicken'));
  source.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim));
  // 俊敏 lands, but the turn it lands in has started already (§5): no step this turn.
  assert.equal(source.State.Player.Statuses.Stacks(StatusKind.Swift), 2);
  assert.equal(source.CanTakeFreeStep(), false);
  source.EndTurn();
  // Not while the enemy's turn plays.
  assert.equal(source.CanTakeFreeStep(), false);
  ToPlayer(source);
  assert.equal(TurnLoop.CanTakeFreeStep(source.State), true);
  assert.equal(source.CanTakeFreeStep(), true);
  AssertFrameMatchesCore(source);
});

test('FreeStep_Back_IsOneScreenEvent_WhoseFrameMatchesTheCore', () => {
  const source = Source();
  ToSwiftTurn(source);
  const before = source.State.Player.Cell;
  const historyBefore = source.History.length;
  assert.equal(source.CanTakeFreeStep(-1), true);
  const ev = source.TakeFreeStep(-1);

  assert.equal(ev.Kind, CoreBattleSource.FreeStepKind);
  assert.equal(ev.Title, CoreBattleSource.FreeStepTitle);
  assert.equal(source.State.Player.Cell, before - 1);
  // The core's own events, kept in the history the end screen counts from.
  const added = source.History.slice(historyBefore);
  assert.deepEqual(added.map((e) => e.constructor.name), ['FreeStepTaken', 'CellsMoved']);
  assert.equal(added[0].Direction, -1);
  assert.ok(added[1] instanceof CellsMoved && added[1].Actor === Actor.Player);

  // One walk of one cell and the recount, as a card's move plays (§5.7 順 10 → 順 1〜2).
  const moves = ev.Cues.filter((c) => c.Kind === CueKind.RangeSwitch);
  assert.equal(moves.length, 1);
  assert.equal(ev.Cues.length, 1);
  const cue = moves[0];
  assert.equal(cue.Pushed, false);
  assert.equal(cue.CellsMoved, 1);
  assert.equal(cue.PlayerCellAfter, source.State.Player.Cell);
  assert.equal(cue.GapAfter, source.State.GapTo(source.State.Nearest));
  assert.deepEqual(EffectPlan.StepsOf(ev).map((s) => s.Id + ' x' + s.Count), [EffectId.FloorMove + ' x1', EffectId.GapRecount + ' x1']);

  // The screen settles on the event's frame; the step is spent, the turn still the player's.
  assert.equal(source.Frame, ev.After);
  assert.equal(source.WaitingForPlayer, true);
  assert.equal(source.CanTakeFreeStep(), false);
  assert.throws(() => source.TakeFreeStep(1), (e) => e.name === 'InvalidOperationException');
  AssertFrameMatchesCore(source);
  // 移動後 (§2.3) counts the step: the core says the player moved this turn.
  assert.equal(source.State.Player.Moved, true);
});

test('FreeStep_Forward_MatchesTheCore_AndTheBattlePlaysOn', () => {
  const source = Source();
  ToSwiftTurn(source);
  const s = source.State;
  const way = TurnLoop.TakeFreeStep(s, 1).State; // what the core alone would do (the state is a value)
  // Seed 7 leaves N 2 here, so the way forward is open.
  assert.notEqual(way.Player.Cell, s.Player.Cell);
  assert.equal(source.CanTakeFreeStep(1), true);
  const ev = source.TakeFreeStep(1);
  assert.equal(source.State.Player.Cell, way.Player.Cell);
  assert.equal(ev.After.Floor.PlayerCell, way.Player.Cell);
  AssertFrameMatchesCore(source);
  // The turn goes on from the new cell: a card is still playable and the turn ends as usual.
  const next = source.Frame.Hand.find((f) => source.Inspect(f.Id) === PlayVerdict.Accepted);
  assert.ok(next, 'a card is playable after the step');
  assert.equal(source.TryPlay(next.Id, DepictionText.RequiredZone(next.Aim)).verdict, PlayVerdict.Accepted);
  AssertFrameMatchesCore(source);
  source.EndTurn();
  ToPlayer(source);
  AssertFrameMatchesCore(source);
});

test('FreeStep_TheLine_ClosesAWay_AndTheSecondStackOpensTheNextTurn', () => {
  const source = Source();
  ToSwiftTurn(source);
  source.TakeFreeStep(-1); // cell 2 → 1
  assert.equal(source.State.Player.Cell, 1);
  source.EndTurn();
  ToPlayer(source);
  // 俊敏 2 gives two turns of it (§5: read before the tick).
  assert.equal(source.CanTakeFreeStep(), true);
  // Cell 1 is the end of the line: no way back (no arrow, battle_ui_ux_v2 §3.8).
  assert.equal(source.State.Player.Cell, 1);
  assert.equal(source.CanTakeFreeStep(-1), false);
  assert.equal(source.CanTakeFreeStep(0), false);
  assert.equal(source.CanTakeFreeStep(2), false);
  assert.throws(() => source.TakeFreeStep(0), (e) => e.name === 'ArgumentOutOfRangeException');
  AssertFrameMatchesCore(source);
});

test('FreeStep_PlayingACard_LetsTheStepGo', () => {
  const source = Source();
  ToSwiftTurn(source);
  const next = source.Frame.Hand.find((f) => source.Inspect(f.Id) === PlayVerdict.Accepted);
  assert.ok(next);
  source.TryPlay(next.Id, DepictionText.RequiredZone(next.Aim));
  assert.equal(source.CanTakeFreeStep(), false);
  assert.equal(source.CanTakeFreeStep(-1), false);
});

test('FreeStep_UnderBinding_IsTaken_AndTheLineSaysWhy', () => {
  const source = Source();
  ToSwiftTurn(source);
  // Test set-up: 呪縛 on the player, as a boss word would put it (roster §4.1). The core still takes the step.
  const s = source.State;
  source._state = s.with({ Player: s.Player.with({ Statuses: s.Player.Statuses.Add(StatusKind.Binding, 2) }) });
  assert.equal(source.CanTakeFreeStep(-1), true, 'the arrow stands under 呪縛 too (§3.8)');
  const cell = source.State.Player.Cell;
  const historyBefore = source.History.length;
  const ev = source.TakeFreeStep(-1);
  const added = source.History.slice(historyBefore);
  assert.ok(added[0] instanceof FreeStepTaken);
  assert.ok(added[1] instanceof MoveBlocked && added[1].By === StatusKind.Binding);
  assert.equal(source.State.Player.Cell, cell);
  assert.deepEqual(ev.Cues.map((c) => c.Kind + ':' + c.Text), [CueKind.TraitFire + ':' + CoreText.MoveBlockedLine(StatusKind.Binding)]);
  assert.equal(source.CanTakeFreeStep(), false);
  AssertFrameMatchesCore(source);
});

test('FreeStep_WithTwoEnemies_EveryEnemysNMatchesTheCore', () => {
  const source = Source({ more: [Enemies.CrossbowHunter], cells: 8 });
  ToSwiftTurn(source);
  assert.equal(source.Frame.Enemies.length, 2);
  const way = source.CanTakeFreeStep(1) ? 1 : -1;
  assert.equal(source.CanTakeFreeStep(way), true);
  const ev = source.TakeFreeStep(way);
  const cue = ev.Cues.find((c) => c.Kind === CueKind.RangeSwitch);
  assert.ok(cue);
  assert.equal(cue.PlayerCellAfter, source.State.Player.Cell);
  AssertFrameMatchesCore(source);
});
