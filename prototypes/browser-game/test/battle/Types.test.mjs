// Tests for src/core/battle/Types.js (port of unity-port/BattleCore/Types.cs).
// Ports the record / enum rows of FieldTests (Reach, bands), BossStatusTests (tokens and labels),
// ColumnTests (CardDef / EnemyActionDef cost), FloorReadTests (GapBoundary) and MultiEnemyTests
// (Nearest, Living), plus the members every other module leans on.
import { test as nodeTest } from 'node:test';
import assert from 'node:assert/strict';
import { enumIndex, toParityJson } from '../../src/shared/cs.js';
import { loadDeps } from './_foundation_deps.mjs';

// Types.js imports Statuses.js (StatusSet.Empty); skip with a reason while that module is missing.
const { mods: T, skip: SKIP } = await loadDeps(import.meta.url, ['../../src/core/battle/Types.js', '../../src/core/battle/Statuses.js']);
const test = (name, fn) => nodeTest(name, { skip: SKIP }, fn);
const { StatusSet } = T ?? {};

const {
  BattleAttribute, FaceKind, Actor, GameResult, BattlePhase, TargetKind, OmenKind, StatusKind, StatusGrant,
  StatusDecay, GapBand, TraitCondition, TraitEffect, Trait, StanceHook, StanceWhen, StanceEntry, StanceDef,
  Reach, GapBoundary, Face, CardDef, CardInstance, OmenLabel, Omen, EnemyActionDef, PlayerTurnEnd,
  BattleHistory, AdaptationView, TreeSwitch, EnemyRank, EnemyDef, CombatantState, EnemyUnit, CarriedTools,
  BattleState, EnumTokens,
} = T ?? {};

const TYPE_MAP_NAMES = ['BattleAttribute', 'FaceKind', 'Actor', 'GameResult', 'BattlePhase', 'TargetKind', 'OmenKind',
  'StatusKind', 'StatusGrant', 'StatusDecay', 'GapBand', 'TraitCondition', 'TraitEffect', 'Trait', 'StanceHook',
  'StanceWhen', 'StanceEntry', 'StanceDef', 'Reach', 'GapBoundary', 'Face', 'CardDef', 'CardInstance', 'OmenLabel',
  'Omen', 'EnemyActionDef', 'PlayerTurnEnd', 'BattleHistory', 'AdaptationView', 'TreeSwitch', 'EnemyRank', 'EnemyDef',
  'CombatantState', 'EnemyUnit', 'CarriedTools', 'BattleState', 'EnumTokens'];

test('every type TYPE_MAP lists for Types.cs is exported', () => {
  for (const name of TYPE_MAP_NAMES) assert.ok(T[name] !== undefined, name);
});

// ---- enums ----

test('BattleAttribute keeps its explicit [Flags] values', () => {
  assert.deepEqual({ ...BattleAttribute }, { None: 0, Attack: 1, Guard: 2, Skill: 4, Stance: 8 });
  assert.equal(BattleAttribute.Attack | BattleAttribute.Guard, 3);
});

test('plain enums keep the C# declaration order ((int) casts read it)', () => {
  assert.deepEqual(FaceKind.$order, ['Attack', 'Move', 'Guard', 'Skill', 'Stance']);
  assert.deepEqual(Actor.$order, ['Player', 'Enemy']);
  assert.deepEqual(GameResult.$order, ['Ongoing', 'Won', 'Lost']);
  assert.deepEqual(BattlePhase.$order, ['AwaitingTurnStart', 'PlayerAction', 'Finished']);
  assert.deepEqual(TargetKind.$order, ['One', 'Self', 'All']);
  assert.deepEqual(OmenKind.$order, ['Attack', 'Guard', 'Move', 'Skill', 'Stance', 'Rest']);
  assert.deepEqual(StatusKind.$order, ['Slow', 'Swift', 'Bleed', 'Fragile', 'Intimidate', 'Fatigue', 'Empower', 'Focus',
    'Parry', 'Regen', 'MiasmaShroud', 'Binding', 'Hook', 'Depths', 'Rooting', 'Withering']);
  assert.equal(enumIndex(StatusKind, StatusKind.Withering), 15);
  assert.deepEqual(StatusDecay.$order, ['OnUse', 'OnTurn', 'Lasting']);
  assert.deepEqual(GapBand.$order, ['Zero', 'OneToTwo', 'ThreePlus']);
  assert.equal(TraitCondition.$order.length, 15);
  assert.equal(TraitCondition.$order[14], 'SelfHas');
  assert.equal(TraitEffect.$order.length, 11);
  assert.equal(TraitEffect.$order[10], 'BreakBonus');
  assert.deepEqual(StanceHook.$order, ['TurnStart', 'TurnEnd', 'AttackBonus', 'OnHit', 'PushImmune', 'BreakOnFoeMove', 'StatusOnAttack', 'CostDiscount']);
  assert.deepEqual(StanceWhen.$order, ['Always', 'GapAtLeast', 'GapAtMost', 'OmenAttack', 'MovedThisTurn', 'TargetHasStatus']);
  assert.deepEqual(EnemyRank.$order, ['Normal', 'Elite', 'Boss']);
});

// ---- EnumTokens ----

// (enum values are their member names, so the rows are written as strings)
for (const [gap, band] of [[0, 'Zero'], [1, 'OneToTwo'], [2, 'OneToTwo'], [3, 'ThreePlus'], [6, 'ThreePlus']]) {
  test(`TheBand_SplitsAtOneAndThree(${gap}, ${band})`, () => {
    assert.equal(EnumTokens.ToBand(gap), band);
  });
}

test('ToBand refuses a negative gap with the .NET message', () => {
  assert.throws(() => EnumTokens.ToBand(-1), (e) =>
    e.name === 'ArgumentOutOfRangeException' && e.message === "A gap is never negative. (Parameter 'gap')\nActual value was -1.");
});

const WORDS = [
  ['Slow', 'slow', '鈍足'], ['Swift', 'swift', '俊敏'], ['Bleed', 'bleed', '出血'],
  ['Fragile', 'fragile', '脆化'], ['Intimidate', 'intimidate', '威圧'], ['Fatigue', 'fatigue', '疲労'],
  ['Empower', 'empower', '強化'], ['Focus', 'focus', '集中'], ['Parry', 'parry', '見切り'],
  ['Regen', 'regen', '再生'],
  // BossStatusTests.EachBossWord_HasItsDecay_ItsSide_AndItsName (token / label rows)
  ['MiasmaShroud', 'miasma_shroud', '瘴気纏い'], ['Binding', 'binding', '呪縛'],
  ['Hook', 'hook', '鉤爪'], ['Depths', 'depths', '深み'], ['Rooting', 'rooting', '根張り'],
  ['Withering', 'withering', '枯らし'],
];

test('every status word has its token and its label', () => {
  assert.equal(WORDS.length, StatusKind.$order.length);
  for (const [kind, token, label] of WORDS) {
    assert.equal(EnumTokens.ToToken(kind), token, kind);
    assert.equal(EnumTokens.ToLabel(kind), label, kind);
  }
  assert.throws(() => EnumTokens.ToToken('Nope'), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message === "Specified argument was out of the range of valid values. (Parameter 'kind')\nActual value was Nope.");
  assert.throws(() => EnumTokens.ToLabel(99), (e) => e.name === 'ArgumentOutOfRangeException');
});

// ---- Reach / GapBoundary ----

test('Reach_IsInclusive_AndPrintsAsARange', () => {
  assert.ok(Reach.Default.equals(new Reach(0, 1)));
  assert.deepEqual(Reach.Default, new Reach(0, 1));
  assert.equal(new Reach(1, 2).Contains(1), true);
  assert.equal(new Reach(1, 2).Contains(2), true);
  assert.equal(new Reach(1, 2).Contains(0), false);
  assert.equal(new Reach(1, 2).Contains(3), false);
  assert.equal(new Reach(1, 2).ToText(), '1〜2');
  assert.equal(Reach.Only(0).ToText(), '0');
  assert.deepEqual(new Face().ReachOrDefault, Reach.Default);
  assert.equal(Reach.Default.ToText(), '0〜1');
});

test('GapBoundary.Holds: at least / at most (FloorReadTests)', () => {
  const atLeast2 = new GapBoundary(true, 2);
  const atMost0 = new GapBoundary(false, 0);
  assert.deepEqual([0, 1, 2, 3].map((g) => atLeast2.Holds(g)), [false, false, true, true]);
  assert.deepEqual([0, 1, 2].map((g) => atMost0.Holds(g)), [true, false, false]);
  assert.ok(new GapBoundary({ AtLeast: true, Threshold: 2 }).equals(atLeast2));
});

// ---- Records and their defaults ----

test('record defaults are the C# defaults', () => {
  assert.deepEqual({ ...new StatusGrant(StatusKind.Bleed) }, { Kind: 'Bleed', Stacks: 2, OnSelf: false, Cap: 0 });
  assert.deepEqual({ ...new Trait(TraitCondition.Combo, TraitEffect.PowerBonus) },
    { Condition: 'Combo', Effect: 'PowerBonus', Amount: 0, Threshold: 0, Attribute: 0, Omen: null, Watch: null, Grant: null });
  assert.deepEqual({ ...new StanceDef(StanceHook.OnHit) }, {
    Hook: 'OnHit', When: 'Always', Threshold: 0, Guard: 0, Recovery: 0, NextRecovery: 0, Power: 0, Stamina: 0,
    Status: null, StatusStacks: 0, Break: 0, OncePerTurn: false, Attribute: null,
  });
  assert.deepEqual({ ...new Face() }, {
    Power: 0, Guard: 0, Move: 0, Push: 0, Draw: 0, StaminaGain: 0, Reach: null, Statuses: null, Heal: 0, Break: 0,
    Stance: null, Hits: 1, StopStance: false,
  });
  const card = new CardDef('c', 'C', BattleAttribute.Attack, 2, new Face());
  assert.equal(card.Trait, null);
  assert.equal(card.Targets, TargetKind.One);
  assert.equal(card.Description, '');
  assert.equal(card.ExtraTrait, null);
  assert.equal(card.FixedCost, null);
  const action = new EnemyActionDef('a', 'A', BattleAttribute.Attack, 1, new Face());
  assert.deepEqual([action.Omen, action.NotWhileFoeHas, action.PullBeyond, action.NotAtFoeCap], [null, null, null, false]);
  assert.deepEqual({ ...new PlayerTurnEnd(3, 1, true) }, { Guard: 3, Gap: 1, Moved: true, Cell: 0, Played: 0, LastMove: 0 });
  assert.deepEqual({ ...new OmenLabel(OmenKind.Rest) }, { Kind: 'Rest', Reach: null });
  assert.deepEqual({ ...new StanceEntry(new StanceDef(StanceHook.TurnEnd), 'src') }.ReactedTurn, 0);
  assert.deepEqual({ ...new CarriedTools() }, { ScaleGuard: 0, BossWordWard: 0, Warded: null });
});

test('named arguments: { ... } alone, and positional followed by { ... }', () => {
  assert.deepEqual(new Face({ Power: 7, Reach: new Reach(0, 2) }), new Face(7, 0, 0, 0, 0, 0, new Reach(0, 2)));
  assert.deepEqual(new Trait(TraitCondition.GapAtMost, TraitEffect.PowerBonus, 3, { Threshold: 1 }),
    new Trait(TraitCondition.GapAtMost, TraitEffect.PowerBonus, 3, 1));
  assert.deepEqual(new StatusGrant(StatusKind.Regen, 1, { OnSelf: true }), new StatusGrant(StatusKind.Regen, 1, true, 0));
  assert.throws(() => new Face({ Power: 7, Pwer: 3 }), /unknown named argument/);
  // A Map (C# dictionary) or an array is never read as named arguments.
  const def = new EnemyDef('e', 'E', 10, 10, 2, 1, [], [], [], new Map());
  assert.ok(def.Actions instanceof Map);
});

test('with() copies and changes, like the C# with expression; equals is structural', () => {
  const a = new Face({ Power: 5 });
  const b = a.with({ Guard: 3 });
  assert.equal(a.Guard, 0);
  assert.equal(b.Guard, 3);
  assert.equal(b.Power, 5);
  assert.ok(b instanceof Face);
  assert.ok(a.equals(new Face({ Power: 5 })));
  assert.ok(!a.equals(b));
});

test('Face: StatusList is empty for none; GivesFoeStatus reads OnSelf', () => {
  assert.deepEqual([...new Face().StatusList], []);
  assert.equal(new Face().GivesFoeStatus, false);
  assert.equal(new Face({ Statuses: [new StatusGrant(StatusKind.Regen, 1, true)] }).GivesFoeStatus, false);
  assert.equal(new Face({ Statuses: [new StatusGrant(StatusKind.Regen, 1, true), new StatusGrant(StatusKind.Bleed)] }).GivesFoeStatus, true);
});

test('CardDef: cost from the column (or FixedCost), the folded attribute, traits in judging order', () => {
  const card = (column, fixed = null) => new CardDef('c', 'C', BattleAttribute.Attack | BattleAttribute.Guard, column,
    new Face({ Power: 5, Guard: 3 }), null, TargetKind.One, '', null, fixed);
  assert.deepEqual([1, 2, 3, 4].map((c) => card(c).Cost), [1, 2, 3, 3]);
  assert.equal(card(4, 0).Cost, 0, '軽さ takes the cost to as low as 0');
  assert.equal(card(1).Attribute, BattleAttribute.Attack);
  const t1 = new Trait(TraitCondition.Desperate, TraitEffect.PowerBonus, 4);
  const t2 = new Trait(TraitCondition.Thin, TraitEffect.Draw, 1);
  const both = new CardDef('b', 'B', BattleAttribute.Attack, 2, new Face({ Power: 9 }), t1, TargetKind.One, '', t2);
  assert.deepEqual(both.AllTraits, [t1, t2]);
  assert.deepEqual(new CardDef('n', 'N', BattleAttribute.Skill, 1, new Face()).AllTraits, []);
  assert.deepEqual(new CardDef('x', 'X', BattleAttribute.Skill, 1, new Face(), null, TargetKind.One, '', t2).AllTraits, [t2]);
  assert.throws(() => card(5).Cost, (e) => e.name === 'ArgumentOutOfRangeException');
});

test('EnemyActionDef: cost from the column, attribute folded like a card', () => {
  const a = new EnemyActionDef('step', 'Step', BattleAttribute.None, 1, new Face({ Move: 1 }));
  assert.equal(a.Cost, 1);
  assert.equal(a.Attribute, BattleAttribute.Skill);
  assert.equal(new EnemyActionDef('big', 'Big', BattleAttribute.Attack, 4, new Face({ Power: 30 })).Cost, 3);
});

test('StanceEntry.StoppedIn: through the turn it names', () => {
  const entry = new StanceEntry(new StanceDef(StanceHook.TurnStart), 'iron', 0, 3);
  assert.deepEqual([2, 3, 4].map((t) => entry.StoppedIn(t)), [true, true, false]);
  assert.equal(new StanceEntry(new StanceDef(StanceHook.TurnStart), 'iron').StoppedIn(1), false);
});

test('BattleHistory: empty lists for none, the With* copies, LastTurnCards', () => {
  const empty = new BattleHistory();
  assert.deepEqual([empty.Cards, empty.TurnEnds, empty.HookSnags, empty.Inflictions, empty.MasteryPlays].map((l) => l.length), [0, 0, 0, 0, 0]);
  assert.deepEqual([...empty.LastTurnCards], []);

  let h = empty.WithCard(BattleAttribute.Attack).WithCard(BattleAttribute.Guard).WithTurnEnd(new PlayerTurnEnd(3, 1, false, 2, 2));
  h = h.WithCard(BattleAttribute.Skill).WithCard(BattleAttribute.Attack).WithCard(BattleAttribute.Stance)
    .WithTurnEnd(new PlayerTurnEnd(0, 0, true, 3, 3, 1));
  assert.deepEqual([...h.Cards], [1, 2, 4, 1, 8]);
  assert.equal(h.TurnEnds.length, 2);
  assert.deepEqual(h.LastTurnCards, [4, 1, 8]);
  assert.equal(empty.Cards.length, 0, 'the original is not touched');

  // Played larger than what was recorded is capped by Cards.Count.
  const odd = new BattleHistory([BattleAttribute.Attack], [new PlayerTurnEnd(0, 0, false, 0, 5)]);
  assert.deepEqual(odd.LastTurnCards, [1]);

  const snags = empty.WithHookSnag(2).WithHookSnag(4).WithInfliction(1);
  assert.deepEqual([...snags.HookSnags], [2, 4]);
  assert.deepEqual([...snags.Inflictions], [1]);
  const play = { Card: 'thrust' };
  assert.deepEqual([...empty.WithMasteryPlay(play).MasteryPlays], [play]);
  assert.throws(() => empty.WithMasteryPlay(null), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'play')");
});

test('AdaptationView.HasHeld reads the held switch ids, ordinally', () => {
  const view = new AdaptationView(3, 40, 60, ['hit'], new BattleHistory(), 1, ['second_stage']);
  assert.equal(view.HasHeld('second_stage'), true);
  assert.equal(view.HasHeld('Second_Stage'), false);
  assert.equal(new AdaptationView(1, 60, 60, [], new BattleHistory()).HasHeld('x'), false);
  assert.deepEqual([...new AdaptationView(1, 60, 60, [], new BattleHistory()).Held], []);
});

test('TreeSwitch: the branch per band, the override by id; Holds is the condition', () => {
  const override = new EnemyActionDef('hook_cast', 'Hook', BattleAttribute.Attack, 2, new Face({ Power: 3 }));
  const sw = new TreeSwitch('second_stage', (v) => v.Hp <= 30, ['a'], null, ['c'], [override], 2, false, true);
  assert.deepEqual(sw.Branch(GapBand.Zero), ['a']);
  assert.equal(sw.Branch(GapBand.OneToTwo), null);
  assert.deepEqual(sw.Branch(GapBand.ThreePlus), ['c']);
  assert.equal(sw.Override('hook_cast'), override);
  assert.equal(sw.Override('other'), null);
  assert.equal(new TreeSwitch('s', () => true).Override('x'), null);
  assert.equal(sw.Holds(new AdaptationView(2, 25, 60, [], new BattleHistory())), true);
  assert.equal(sw.Holds(new AdaptationView(2, 45, 60, [], new BattleHistory())), false);
  assert.throws(() => sw.Branch('Nope'), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message === "Specified argument was out of the range of valid values. (Parameter 'band')\nActual value was Nope.");
});

test('EnemyDef: defaults and the branch per band', () => {
  const def = new EnemyDef('e', 'E', 60, 10, 2, 1, ['z'], ['o'], ['t'], new Map());
  assert.equal(def.Rank, EnemyRank.Normal);
  assert.equal(def.ActionsPerPhase, 1);
  assert.equal(def.Switches, null);
  assert.deepEqual([GapBand.Zero, GapBand.OneToTwo, GapBand.ThreePlus].map((b) => def.Branch(b)), [['z'], ['o'], ['t']]);
  assert.throws(() => def.Branch(7), (e) => e.message === "Specified argument was out of the range of valid values. (Parameter 'band')\nActual value was 7.");
});

// ---- CombatantState / EnemyUnit / BattleState ----

const body = (cell, size = 1, hp = 60) => new CombatantState(hp, hp, 10, 10, 0, cell, size, StatusSet.Empty);
const enemyDef = (id) => new EnemyDef(id, id, 60, 10, 2, 1, [], [], [], new Map());

test('CombatantState: FarCell, the stance list, the played list', () => {
  const dragon = body(4, 3, 200);
  assert.equal(dragon.FarCell, 6);
  assert.equal(body(2).FarCell, 2);
  assert.deepEqual([...dragon.StanceList], []);
  assert.deepEqual([...dragon.PlayedThisTurn], []);
  const iron = new StanceDef(StanceHook.CostDiscount, { Attribute: BattleAttribute.Guard });
  const twice = dragon.WithStance(iron, 'iron-0').WithStance(iron, 'iron-1');
  assert.deepEqual(twice.StanceList.map((e) => e.Source), ['iron-0', 'iron-1'], 'the same stance twice is two entries');
  assert.ok(twice.StanceList[0] instanceof StanceEntry);
  assert.equal(twice.StanceList[0].ReactedTurn, 0);
  assert.equal(dragon.StanceList.length, 0);
  // defaults
  assert.deepEqual([dragon.NextTurnRecoveryBonus, dragon.Stances, dragon.FollowUp, dragon.Played, dragon.Moved, dragon.FreeStep, dragon.LastMove],
    [0, null, 0, null, false, false, 0]);
});

test('EnemyUnit: Alive until HP 0; the id lists are empty for none', () => {
  const unit = new EnemyUnit(enemyDef('a'), body(5), null);
  assert.equal(unit.Alive, true);
  assert.equal(unit.with({ Body: unit.Body.with({ Hp: 0 }) }).Alive, false);
  assert.equal(unit.with({ Body: unit.Body.with({ Hp: -3 }) }).Alive, false);
  assert.deepEqual([unit.Held, unit.Spent, unit.ExecutedActions].map((l) => l.length), [0, 0, 0]);
  assert.deepEqual([unit.Plan, unit.ActiveSwitch], [null, null]);
});

test('CarriedTools: None and the warded stacks default', () => {
  assert.deepEqual({ ...CarriedTools.None }, { ScaleGuard: 0, BossWordWard: 0, Warded: null });
  assert.equal(CarriedTools.None.WardedStacks, StatusSet.Empty);
});

function battle(enemyCells, fieldCells = 8, playerCell = 2) {
  const units = enemyCells.map((c, i) => new EnemyUnit(enemyDef(String.fromCharCode(97 + i)), body(c), new Omen(`act_${i}`, new OmenLabel(OmenKind.Attack))));
  return new BattleState(1, fieldCells, body(playerCell, 1, 50), units, [], [], []);
}

test('BattleState: defaults and the first-enemy shorthands', () => {
  const s = battle([5]);
  assert.equal(s.Result, GameResult.Ongoing);
  assert.equal(s.Phase, BattlePhase.AwaitingTurnStart);
  assert.equal(s.Tools, CarriedTools.None);
  assert.deepEqual([...s.Exiled], []);
  assert.ok(s.History instanceof BattleHistory);
  assert.equal(s.Enemy, s.Enemies[0].Body);
  assert.equal(s.EnemyDef, s.Enemies[0].Def);
  assert.equal(s.Omen.ActionId, 'act_0');
  assert.equal(s.Gap, 2);
  assert.equal(s.GapTo(0), 2);
});

test('BattleState: Living and Nearest (MultiEnemyTests)', () => {
  const s = battle([6, 3, 4]);
  assert.deepEqual(s.Living, [0, 1, 2]);
  assert.equal(s.Nearest, 1);
  assert.deepEqual([0, 1, 2].map((u) => s.GapTo(u)), [3, 0, 1]);
  const bDown = s.WithEnemy(1, s.Enemies[1].Body.with({ Hp: 0 }));
  assert.deepEqual(bDown.Living, [0, 2]);
  assert.equal(bDown.Nearest, 2);
  const allDown = bDown.WithEnemy(0, bDown.Enemies[0].Body.with({ Hp: 0 })).WithEnemy(2, bDown.Enemies[2].Body.with({ Hp: 0 }));
  assert.deepEqual(allDown.Living, []);
  assert.equal(allDown.Nearest, -1);
  // Equal cells: the first in definition order stays nearest (strict <).
  assert.equal(battle([4, 4]).Nearest, 0);
});

test('BattleState: WithEnemy (both overloads), WithOmen, WithUnit copy and never mutate', () => {
  const s = battle([5, 6]);
  const moved = s.WithEnemy(s.Enemy.with({ Cell: 4 }));
  assert.equal(moved.Enemies[0].Body.Cell, 4);
  assert.equal(s.Enemies[0].Body.Cell, 5, 'the original is untouched');
  const second = s.WithEnemy(1, s.Enemies[1].Body.with({ Guard: 3 }));
  assert.equal(second.Enemies[1].Body.Guard, 3);
  assert.equal(second.Enemies[0], s.Enemies[0]);
  const noOmen = s.WithOmen(1, null);
  assert.equal(noOmen.Enemies[1].Omen, null);
  assert.equal(s.Enemies[1].Omen.ActionId, 'act_1');
  const replaced = s.WithUnit(0, s.Enemies[1]);
  assert.equal(replaced.Enemies[0], s.Enemies[1]);
  assert.ok(replaced instanceof BattleState);
  assert.notEqual(replaced.Enemies, s.Enemies);
});

test('JSON shape: records carry $type and their C# member names', () => {
  const json = toParityJson(new CardInstance('thrust-0', new CardDef('thrust', '突き', BattleAttribute.Attack, 3, new Face({ Power: 23 }))));
  assert.equal(json.$type, 'CardInstance');
  assert.equal(json.Def.$type, 'CardDef');
  assert.equal(json.Def.Face.$type, 'Face');
  assert.equal(json.Def.Face.Power, 23);
  assert.equal(json.Def.Attributes, 1, '[Flags] enums as their integer value');
});
