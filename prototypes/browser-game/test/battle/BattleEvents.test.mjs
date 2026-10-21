// Tests for src/core/battle/BattleEvents.js (port of unity-port/BattleCore/BattleEvents.cs).
// The C# tests compare events by record equality, init-only Unit included
// (MultiEnemyTests: `new CardPlayed(Actor.Player, card, 0) { Unit = 1 }`, `new WallHit(...) { Unit = 0 }`),
// and read StatusApplied.Landed (DepartureToolTests: a warded rite lands 0).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { toParityJson } from '../../src/shared/cs.js';
import * as E from '../../src/core/battle/BattleEvents.js';

const {
  BattleEvent, TurnStarted, CellsMoved, WallHit, PushRefused, StatusApplied, CardPlayed, DamageDealt, StepResult,
  EnemyDefeated, MoveBlocked, StanceStopped,
} = E;

const NAMES = ['BattleEvent', 'TurnStarted', 'GuardCleared', 'StaminaRecovered', 'StatusTicked', 'Drawn', 'DeckReshuffled',
  'OmenSet', 'PlanSet', 'PlanChanged', 'TreeSwitched', 'ReserveChecked', 'HandDiscarded', 'TurnEnded', 'CardPlayed',
  'ActionExecuted', 'ActionWhiffed', 'Rested', 'StaminaSpent', 'TraitEvaluated', 'FaceResolved', 'DamageDealt',
  'GuardGained', 'CellsMoved', 'WallHit', 'PushRefused', 'FreeStepTaken', 'MoveBlocked', 'StatusApplied',
  'StaminaGained', 'StatusConsumed', 'StatusHpChanged', 'Healed', 'StaminaBroken', 'Reflected', 'StanceSet',
  'StanceFired', 'StanceStopped', 'StanceResumed', 'CardExiled', 'EnemyDefeated', 'DefeatChecked', 'BattleEnded',
  'MaxStaminaChanged', 'StatusCleared', 'StepResult'];

/** C# positional parameters (after Actor) of every event. */
const FIELDS = {
  TurnStarted: ['Turn'], GuardCleared: ['Cleared'], StaminaRecovered: ['Amount', 'StaminaAfter', 'MaxStamina'],
  StatusTicked: ['Kind', 'StacksAfter'], Drawn: ['Card', 'HandCountAfter'], DeckReshuffled: ['DrawPileCount'],
  OmenSet: ['Omen', 'Decided'], PlanSet: ['Plan', 'Decided'], PlanChanged: ['Was', 'Now'], TreeSwitched: ['From', 'To'],
  ReserveChecked: ['StaminaLeft', 'GuardGained', 'GuardAfter'], HandDiscarded: ['Count'], TurnEnded: ['Turn'],
  CardPlayed: ['Card', 'GapBefore'], ActionExecuted: ['Action', 'GapBefore'], ActionWhiffed: ['SourceId', 'Gap', 'Reach'],
  Rested: ['Declared'], StaminaSpent: ['Amount', 'StaminaAfter'], TraitEvaluated: ['SourceId', 'Trait', 'Outcome'],
  FaceResolved: ['SourceId', 'Face'],
  DamageDealt: ['Target', 'Raw', 'Absorbed', 'Damage', 'TargetGuardAfter', 'TargetHpAfter'],
  GuardGained: ['Amount', 'GuardAfter'], CellsMoved: ['From', 'To', 'Pushed'],
  WallHit: ['BlockedCells', 'Raw', 'Absorbed', 'Damage', 'GuardAfter', 'HpAfter'], PushRefused: ['Size'],
  FreeStepTaken: ['Direction'], MoveBlocked: ['By'], StatusApplied: ['Target', 'Kind', 'Stacks', 'StacksAfter', 'Refused'],
  StaminaGained: ['Amount', 'StaminaAfter'], StatusConsumed: ['Kind', 'StacksAfter'],
  StatusHpChanged: ['Kind', 'Amount', 'HpAfter'], Healed: ['Amount', 'HpAfter'],
  StaminaBroken: ['Target', 'Amount', 'StaminaAfter'],
  Reflected: ['Target', 'Raw', 'Absorbed', 'Damage', 'TargetGuardAfter', 'TargetHpAfter'],
  StanceSet: ['SourceId', 'Name', 'Stance'], StanceFired: ['SourceId', 'Hook'], StanceStopped: ['SourceId', 'Index', 'Through'],
  StanceResumed: ['SourceId', 'Index'], CardExiled: ['Card'], EnemyDefeated: [], DefeatChecked: ['Result'],
  BattleEnded: ['Result'], MaxStaminaChanged: ['Amount', 'MaxAfter', 'StaminaAfter'], StatusCleared: ['Kind', 'Stacks'],
};

test('every type TYPE_MAP lists for BattleEvents.cs is exported', () => {
  for (const name of NAMES) assert.equal(typeof E[name], 'function', name);
  assert.equal(Object.keys(FIELDS).length, 44);
});

test('every event is a BattleEvent with Actor first, then its own fields in C# order, and Unit 0', () => {
  for (const [name, fields] of Object.entries(FIELDS)) {
    const C = E[name];
    const args = fields.map((_, i) => i + 10);
    const ev = new C('Enemy', ...args);
    assert.ok(ev instanceof BattleEvent, name);
    assert.ok(ev instanceof C, name);
    assert.equal(ev.Actor, 'Enemy', name);
    fields.forEach((f, i) => assert.equal(ev[f], i + 10, `${name}.${f}`));
    assert.equal(ev.Unit, 0, `${name}.Unit`);
    assert.deepEqual(C.$fields, ['Actor', ...fields], name);
    assert.equal(C.name, name);
  }
  assert.ok(!(new StepResult(null, []) instanceof BattleEvent));
});

test('object initializer { Unit = u }: trailing named object or with(); Unit takes part in equality', () => {
  const card = { InstanceId: 'brace-0' };
  const a = new CardPlayed('Player', card, 0, { Unit: 1 });
  const b = new CardPlayed('Player', card, 0).with({ Unit: 1 });
  assert.equal(a.Unit, 1);
  assert.deepEqual(a, b);
  assert.ok(a.equals(b));
  assert.ok(!a.equals(new CardPlayed('Player', card, 0)), 'Unit 1 ≠ Unit 0');
  assert.deepEqual(new CellsMoved('Enemy', 3, 4, true, { Unit: 0 }), new CellsMoved('Enemy', 3, 4, true));
  assert.deepEqual(new WallHit('Enemy', 1, 3, 0, 3, 0, 57, { Unit: 0 }), new WallHit('Enemy', 1, 3, 0, 3, 0, 57));
  // C# named arguments for the positional fields too: new CellsMoved(Actor.Enemy, 5, 6, Pushed: true)
  assert.deepEqual(new CellsMoved('Enemy', 5, 6, { Pushed: true }), new CellsMoved('Enemy', 5, 6, true));
  assert.throws(() => new TurnStarted('Player', 1, { Unti: 2, Unit: 1 }), /unknown named argument/);
});

test('ev with { Unit = u } copies; the original keeps its Unit', () => {
  const hit = new DamageDealt('Player', 'Enemy', 6, 2, 4, 0, 56);
  const second = hit.with({ Unit: 2 });
  assert.equal(hit.Unit, 0);
  assert.equal(second.Unit, 2);
  assert.ok(second instanceof DamageDealt);
  assert.equal(second.Raw - second.Absorbed, second.Damage);
});

test('PushRefused.ByStance defaults to false and is set like Unit', () => {
  assert.equal(new PushRefused('Enemy', 2).ByStance, false);
  const byStance = new PushRefused('Player', 1, { Unit: 1, ByStance: true });
  assert.equal(byStance.ByStance, true);
  assert.equal(byStance.Unit, 1);
  assert.ok(!byStance.equals(new PushRefused('Player', 1, { Unit: 1 })));
});

test('StatusApplied.Landed: refused 0, otherwise Stacks − Dropped − Warded', () => {
  assert.equal(new StatusApplied('Player', 'Enemy', 'Bleed', 2, 2, false).Landed, 2);
  assert.equal(new StatusApplied('Player', 'Enemy', 'Bleed', 2, 4, false, { Dropped: 1 }).Landed, 1);
  assert.equal(new StatusApplied('Player', 'Enemy', 'Bleed', 2, 4, false, { Dropped: 2 }).Landed, 0, 'all of it dropped is not a refusal');
  assert.equal(new StatusApplied('Player', 'Enemy', 'Fragile', 2, 0, true).Landed, 0);
  // DepartureToolTests.Charm_KeepsTheFirstStackOfMiasmaShroudOff: the ward kept the one stack off.
  const rite = new StatusApplied('Enemy', 'Player', 'MiasmaShroud', 1, 0, false, { Unit: 0, Dropped: 0, Warded: 1 });
  assert.equal(rite.Warded, 1);
  assert.equal(rite.Landed, 0);
  assert.equal(rite.Refused, false);
  const copy = rite.with({ Warded: 0 });
  assert.equal(copy.Landed, 1);
});

test('events read in pattern-matching style (instanceof) and carry their own kind', () => {
  const events = [new TurnStarted('Player', 1), new EnemyDefeated('Enemy', { Unit: 1 }), new MoveBlocked('Player', 'Hook'),
    new StanceStopped('Enemy', 'root', 0, 4)];
  assert.deepEqual(events.filter((e) => e instanceof EnemyDefeated).map((e) => e.Unit), [1]);
  assert.equal(events.filter((e) => e instanceof BattleEvent).length, 4);
});

test('JSON shape: $type, the C# member names, and the init-only properties', () => {
  const json = toParityJson(new StatusApplied('Player', 'Enemy', 'Bleed', 3, 4, false, { Unit: 2, Dropped: 1 }));
  assert.deepEqual(json, {
    $type: 'StatusApplied', Actor: 'Player', Target: 'Enemy', Kind: 'Bleed', Stacks: 3, StacksAfter: 4, Refused: false,
    Unit: 2, Dropped: 1, Warded: 0,
  });
  assert.deepEqual(toParityJson(new PushRefused('Enemy', 2)), { $type: 'PushRefused', Actor: 'Enemy', Size: 2, Unit: 0, ByStance: false });
  const step = toParityJson(new StepResult({ Turn: 1 }, [new TurnStarted('Player', 1)]));
  assert.deepEqual(step, { $type: 'StepResult', State: { Turn: 1 }, Events: [{ $type: 'TurnStarted', Actor: 'Player', Turn: 1, Unit: 0 }] });
});
