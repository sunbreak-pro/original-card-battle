// Tests for src/core/battle/Enemies.js — ported from unity-port/BattleCore.Tests:
// - EnemyRosterTests (all of it: the thirteen, their numbers, trees, traits, texts, the fights to the end),
// - EnemyTreeReachTests (every action is chosen in some scene),
// - BossSwitchTests (the adaptations and stages the roster builds: conditions read off a hand-built
//   AdaptationView, the reshaped trees and overrides, and a few runs through the turn loop).

import { describe, test } from 'node:test';
import assert from 'node:assert/strict';

import { csEquals, compareOrdinal, toParityJson, KeyNotFoundException, ArgumentNullException } from '../../src/shared/cs.js';
import {
  BattleAttribute, TargetKind, OmenKind, StatusKind, TraitCondition, TraitEffect, GapBand, EnemyRank, GameResult, Actor,
  Face, Reach, Trait, StatusGrant, StanceDef, StanceHook, CardDef, CardInstance, EnemyActionDef, CombatantState,
  BattleHistory, PlayerTurnEnd, AdaptationView,
} from '../../src/core/battle/Types.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { Statuses, StatusSet } from '../../src/core/battle/Statuses.js';
import { AttributeRule } from '../../src/core/battle/Attributes.js';
import { EnemyAi } from '../../src/core/battle/EnemyAi.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { FixedRng, SeededRng } from '../../src/core/battle/IRng.js';
import { TurnLoop, BattleSetup, PlayRefusal } from '../../src/core/battle/TurnLoop.js';
import {
  ActionExecuted, StanceFired, GuardGained, DamageDealt, StatusApplied, StatusConsumed, StaminaBroken, CellsMoved,
  TreeSwitched, StatusCleared,
} from '../../src/core/battle/BattleEvents.js';
import { Enemies } from '../../src/core/battle/Enemies.js';

// ------------------------------------------------------------------ helpers

const A = BattleAttribute;
const NoShuffle = new FixedRng(0.9999999);

/** C# record equality (Is.EqualTo on records); shows a JSON diff on failure. */
function eq(actual, expected, msg) {
  if (csEquals(actual, expected)) return;
  assert.deepStrictEqual(toParityJson(actual), toParityJson(expected), msg);
  assert.fail(`${msg ?? 'not equal'} (same JSON, different record types)`);
}

const ofType = (events, Type) => events.filter((e) => e instanceof Type);
const ids = (end) => ofType(end.Events, ActionExecuted).map((e) => e.Action.Id);
const sameSet = (actual, expected, msg) => assert.deepEqual([...actual].sort(compareOrdinal), [...expected].sort(compareOrdinal), msg);

/** Fixtures.Card(id, column = 1, face = null, attributes = Attack, trait = null, targets = One). */
function Card(id, { column = 1, face = null, attributes = A.Attack, trait = null, targets = TargetKind.One } = {}) {
  return new CardDef(id, id, attributes, column, face ?? new Face(), trait, targets, '');
}

/** Fixtures.EnemyAction(id, column = 1, face = null, attributes = Attack, trait = null, targets = One). */
function EnemyAction(id, { column = 1, face = null, attributes = A.Attack, trait = null, targets = TargetKind.One } = {}) {
  return new EnemyActionDef(id, id, attributes, column, face ?? new Face(), trait, targets, '');
}

/** C# StatusSet.Of((kind, n), ...). */
const SS = (...pairs) => StatusSet.Of(...pairs.map(([Kind, Stacks]) => ({ Kind, Stacks })));

/** The line an enemy needs to start at gap 3: the player's cell 2 + 3 + its size (roster §0). */
const CellsFor = (enemy) => Constants.PlayerStartCell + Constants.StartGap + enemy.Size;

const Filler = Card('filler', { face: new Face({ Guard: 1 }), attributes: A.Guard, targets: TargetKind.Self });

function Fillers(count) {
  const deck = [];
  for (let i = 0; i < count; i++) deck.push(new CardInstance('filler-' + i, Filler));
  return deck;
}

/** BossSwitchTests.Deck: the given cards first, then fillers up to DeckMin. */
function Deck(...first) {
  const deck = [];
  for (let i = 0; i < first.length; i++) deck.push(new CardInstance(first[i].Id + '-' + i, first[i]));
  for (let i = deck.length; i < Constants.DeckMin; i++) deck.push(new CardInstance('filler-' + i, Filler));
  return deck;
}

const AllIds = () => Enemies.All.map((e) => e.Id);
const Bands = [GapBand.Zero, GapBand.OneToTwo, GapBand.ThreePlus];

// ======================================================================== EnemyRosterTests

describe('EnemyRosterTests: the roster', () => {
  test('TheRoster_HoldsThirteenEnemies_InRosterOrder', () => {
    // roster §1.8 (v4.6, #286): seven normal (§2.1〜§2.7), three elite (§3.1〜§3.3), three bosses.
    assert.deepEqual(AllIds(), [
      'polearm_warped', 'shadow_hound', 'rusted_revenant', 'crossbow_hunter', 'mist_archer', 'twin_blade_warped', 'polearm_crystal',
      'armored_warden', 'pack_alpha', 'polearm_unyielding',
      'miasma_priest', 'abyss_angler', 'distortion_root',
    ]);
    for (const enemy of Enemies.All) assert.equal(Enemies.ById(enemy.Id), enemy);
    assert.equal(Enemies.PolearmWarped.Name, '錆槍の竜兵', '#179');
    assert.equal(Enemies.PolearmWarpedId, 'polearm_warped');
    assert.equal(Enemies.PolearmWarped, Enemies.PolearmWarped, 'built once (static readonly)');
  });

  test('ById: an unknown id and null throw as the C# does', () => {
    assert.throws(() => Enemies.ById('nobody'), (e) => e instanceof KeyNotFoundException && e.message === 'Unknown enemy id "nobody".');
    assert.throws(() => Enemies.ById(null), (e) => e instanceof ArgumentNullException && e.message === "Value cannot be null. (Parameter 'id')");
  });

  for (const [rank, count, hpMin, hpMax, stamina, recovery, actions] of [
    [EnemyRank.Normal, 7, 60, 100, 10, 2, 1],
    [EnemyRank.Elite, 3, 110, 120, 12, 3, 2],
    [EnemyRank.Boss, 3, 140, 200, 14, 3, 2],
  ]) {
    test(`EachRank_KeepsToTheRosterScale(${rank})`, () => {
      // roster §0 (v4.4, #204): HP, max stamina and recovery by rank; §1.3: elites and bosses act twice.
      const ofRank = Enemies.All.filter((e) => e.Rank === rank);
      assert.equal(ofRank.length, count);
      for (const enemy of ofRank) {
        assert.ok(enemy.MaxHp >= hpMin && enemy.MaxHp <= hpMax, enemy.Id);
        assert.equal(enemy.MaxStamina, stamina, enemy.Id);
        assert.equal(enemy.Recovery, recovery, enemy.Id);
        assert.equal(enemy.ActionsPerPhase, actions, enemy.Id);
      }
    });
  }

  test('the exact HP, rank and size of each of the thirteen (roster §2〜§6)', () => {
    const rows = Enemies.All.map((e) => `${e.Id}:${e.MaxHp}/${e.MaxStamina}/${e.Recovery}/${e.Size}/${e.Rank}/${e.ActionsPerPhase}`);
    assert.deepEqual(rows, [
      'polearm_warped:60/10/2/1/Normal/1', 'shadow_hound:80/10/2/1/Normal/1', 'rusted_revenant:87/10/2/1/Normal/1',
      'crossbow_hunter:81/10/2/1/Normal/1', 'mist_archer:72/10/2/1/Normal/1', 'twin_blade_warped:96/10/2/1/Normal/1',
      'polearm_crystal:90/10/2/1/Normal/1',
      'armored_warden:110/12/3/2/Elite/2', 'pack_alpha:120/12/3/1/Elite/2', 'polearm_unyielding:120/12/3/1/Elite/2',
      'miasma_priest:140/14/3/2/Boss/2', 'abyss_angler:180/14/3/2/Boss/2', 'distortion_root:200/14/3/2/Boss/2',
    ]);
  });

  test('TheLargeOnes_AreTheWardenAndTheBosses', () => {
    // roster §1.8: size 2 for 鉄壁の門竜 and the three bosses; everyone else 1.
    sameSet(Enemies.All.filter((e) => e.Size === 2).map((e) => e.Id), ['armored_warden', 'miasma_priest', 'abyss_angler', 'distortion_root']);
  });

  test('EveryTree_EndsCheap_AndItsFarBranchCanClose', () => {
    // roster §1.2 / battle_core_v4 §6.1: each branch ends on a column 1〜2 action, and the 3+
    // branch holds a forward move or something that reaches 3.
    for (const enemy of Enemies.All) {
      for (const band of Bands) {
        const branch = enemy.Branch(band);
        assert.ok(enemy.Actions.get(branch[branch.length - 1]).Column <= 2, enemy.Id + ' ' + band);
      }
      const closes = enemy.BranchAtGapThreePlus.map((id) => enemy.Actions.get(id)).some((a) =>
        a.Face.Move > 0
        || (EnemyAi.IsOpponentDirected(a.Attributes, a.Face, a.Targets) && a.Face.ReachOrDefault.Contains(3)));
      assert.equal(closes, true, enemy.Id);
    }
  });

  test('EveryAction_KeepsToTheCellLimits_AndTheSelfOnesReadNoReach', () => {
    for (const enemy of Enemies.All) {
      for (const action of enemy.Actions.values()) {
        const at = enemy.Id + '.' + action.Id;
        assert.ok(Math.abs(action.Face.Move) <= Constants.MoveStepMax, at);
        assert.ok(Math.abs(action.Face.Push) <= Constants.MoveStepMax, at);
        const aims = (action.Attributes & A.Attack) === A.Attack || action.Face.GivesFoeStatus
          || action.Face.Push !== 0 || action.Face.Break > 0;
        assert.equal(action.Targets === TargetKind.Self, !aims, at + ': Self exactly when nothing aims at the player');
        assert.equal(action.Face.Stance != null, (action.Attributes & A.Stance) === A.Stance, at);
      }
    }
  });

  for (const [enemy, action, kind] of [
    ['mist_archer', 'fade', OmenKind.Guard],
    ['armored_warden', 'advance_guard', OmenKind.Move],
    ['abyss_angler', 'slack_line', OmenKind.Move],
    ['distortion_root', 'creeping_root', OmenKind.Guard],
    ['pack_alpha', 'herd', OmenKind.Skill],
    ['abyss_angler', 'reel_in', OmenKind.Skill],
    ['polearm_warped', 'step_forward', OmenKind.Move],
    ['rusted_revenant', 'trudge', OmenKind.Move],
  ]) {
    test(`TheOmenKind_IsTheRostersColumn(${enemy}, ${action})`, () => {
      // roster §2〜§6 予兆: the same faces read 防御 or 移動 by the roster's column, not by attribute order.
      assert.equal(EnemyAi.LabelOf(Enemies.ById(enemy).Actions.get(action)).Kind, kind);
    });
  }

  test('TheSlowAnEnemyPuts_LastsIntoThePlayersTurn', () => {
    // #197: 鈍足 put on in the enemy phase loses a stack at the player's turn start, so it is
    // put on with 2 (battle_core_v4 §5) where the roster writes 1.
    let seen = 0;
    for (const enemy of Enemies.All) {
      for (const action of enemy.Actions.values()) {
        for (const grant of action.Face.StatusList.filter((g) => g.Kind === StatusKind.Slow && !g.OnSelf)) {
          assert.ok(grant.Stacks >= 2, enemy.Id + '.' + action.Id);
          seen++;
        }
      }
    }
    assert.equal(seen, 3, 'shield_bash, herd, reel_in');
  });
});

// ---- Two actions a phase (§1.3) ----

describe('EnemyRosterTests: two actions a phase, stances once', () => {
  test('TheWarden_SetsItsWallThenAdvances_InOnePhase', () => {
    // roster §3.1 典型: at gap 3, 鉄壁 (column 2) then 盾を掲げて前進 (column 1) — cost 3, gap 2 after.
    const enemy = Enemies.ArmoredWarden;
    const setup = new BattleSetup(enemy, Fillers(20), CellsFor(enemy));
    const state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;
    const end = TurnLoop.EndTurn(state, NoShuffle);

    assert.deepEqual(ids(end), ['iron_wall', 'advance_guard']);
    assert.equal(end.State.Enemy.StanceList.length, 1);
    eq(end.State.Enemy.StanceList[0].Def, enemy.Actions.get('iron_wall').Face.Stance);
    assert.deepEqual([...end.State.Enemies[0].Spent], ['iron_wall']);
    assert.equal(end.State.Gap, 2);
    assert.equal(end.State.Enemy.Stamina, 12 - 3);
  });

  test('TheSecondAction_IsNeverTheFirstAgain', () => {
    // roster §1.3: the tree is read again after the first action and the same one is skipped.
    const enemy = Enemies.PackAlpha;
    const setup = new BattleSetup(enemy, Fillers(40), CellsFor(enemy));
    let state = TurnLoop.Start(setup, NoShuffle).State;
    for (let turn = 0; turn < 12 && state.Result === GameResult.Ongoing; turn++) {
      state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
      const end = TurnLoop.EndTurn(state, NoShuffle);
      const taken = ids(end);
      assert.ok(taken.length <= 2);
      assert.equal(new Set(taken).size, taken.length, 'turn ' + turn + ': ' + taken.join(', '));
      state = end.State;
    }
  });

  test('AStanceAction_IsUsedOnceABattle_AndLeavesTheTree', () => {
    // roster §1.2: 瘴甲の竜兵 opens with 鉄の身 at gap 3+, and never again.
    const enemy = Enemies.RustedRevenant;
    const setup = new BattleSetup(enemy, Fillers(40), BattleSetup.SliceFieldCells, { StartGap: 3 });
    let state = TurnLoop.Start(setup, NoShuffle).State;
    assert.equal(state.Omen.ActionId, 'iron_body');

    const used = [];
    for (let turn = 0; turn < 10 && state.Result === GameResult.Ongoing; turn++) {
      state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
      // Keep the gap at 3+ by backing off: the player walks to cell 1 whenever it can.
      state = state.with({ Player: state.Player.with({ Cell: 1 }) });
      const end = TurnLoop.EndTurn(state, NoShuffle);
      used.push(...ids(end));
      state = end.State;
    }
    assert.equal(used.filter((id) => id === 'iron_body').length, 1, used.join(', '));
    assert.equal(EnemyAi.ChooseAction(enemy, 3, 10, state.Enemies[0].Spent).Id, 'trudge');
  });

  test('TheIronBody_RaisesGuardAtEachEnemyTurnStart', () => {
    // roster §2.3: 鉄の身 is +3 Guard at the start of every enemy turn once it stands.
    const enemy = Enemies.RustedRevenant;
    const setup = new BattleSetup(enemy, Fillers(40), BattleSetup.SliceFieldCells, { StartGap: 3 });
    let state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;
    state = TurnLoop.EndTurn(state, NoShuffle).State; // sets 鉄の身
    state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
    const next = TurnLoop.EndTurn(state, NoShuffle);

    assert.ok(ofType(next.Events, StanceFired).some((e) => e.Actor === Actor.Enemy && e.Hook === StanceHook.TurnStart));
    assert.equal(ofType(next.Events, GuardGained).find((e) => e.Actor === Actor.Enemy).Amount, 3);
  });
});

// ---- Faces the roster brought in ----

describe('EnemyRosterTests: faces', () => {
  test('TheTwinSlash_StrikesEightTwice_AndPutsNothingOnThePlayer', () => {
    // roster §2.6 (v4.5, #257; v4.19, #498): 8 × 2 = 16, and no 脆化 between the blows (the multi-hit rule, #253).
    const enemy = Enemies.TwinBladeWarped;
    const setup = new BattleSetup(enemy, Fillers(20), BattleSetup.SliceFieldCells, { StartGap: 0 });
    let state = TurnLoop.Start(setup, NoShuffle).State;
    assert.equal(state.Omen.ActionId, 'twin_slash');
    state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
    state = state.with({ Player: state.Player.with({ Guard: 0 }) });
    const end = TurnLoop.EndTurn(state.with({ Player: state.Player.with({ Stamina: 0 }) }), NoShuffle);

    const blows = ofType(end.Events, DamageDealt).filter((d) => d.Target === Actor.Player);
    assert.deepEqual(blows.map((b) => b.Raw), [8, 8]);
    assert.equal(end.State.Player.Hp, Constants.PlayerMaxHp - 16);
    assert.deepEqual(ofType(end.Events, StatusApplied).filter((a) => a.Target === Actor.Player), []);
    assert.deepEqual([...enemy.Actions.get('twin_slash').Face.StatusList], []);
  });

  test('TheCrossGuard_IsTheTwinBladesOnlyChoiceFromThreeAway_AndNeverCloser', () => {
    // roster §2.6 (v4.13, #328): 0 → twin_slash / retreat_cut, 1〜2 → step_slash / retreat_cut,
    // 3+ → cross_guard. Under v4.12 the cost-1 退き斬り and 間を詰める came first and it was never chosen.
    const enemy = Enemies.TwinBladeWarped;
    for (let gap = 0; gap <= 6; gap++) {
      for (let stamina = 0; stamina <= enemy.MaxStamina; stamina++) {
        const chosen = EnemyAi.ChooseAction(enemy, gap, stamina)?.Id ?? null;
        if (gap >= 3) assert.equal(chosen, stamina >= 1 ? 'cross_guard' : null, `gap ${gap}, stamina ${stamina}`);
        else assert.notEqual(chosen, 'cross_guard', `gap ${gap}, stamina ${stamina}`);
      }
    }
    sameSet([...enemy.Actions.keys()], ['twin_slash', 'step_slash', 'retreat_cut', 'cross_guard'], '間を詰める is gone');
  });

  test('TheTwinBlade_OpensWithTheCrossGuard_ClosingTwoAndTakingOneParry', () => {
    // roster §2.6 (v4.13, #328): the battle opens at gap 3, so the first omen is 十字受け, read
    // as 移動 (§1.2). It closes to gap 1; Guard 2 + the opening stance's 3 = 5; 残 9 keeps the 温存
    // and grants 見切り 1.
    const enemy = Enemies.TwinBladeWarped;
    const setup = new BattleSetup(enemy, Fillers(20), CellsFor(enemy), { StartGap: 3 });
    let state = TurnLoop.Start(setup, NoShuffle).State;
    assert.equal(state.Omen.ActionId, 'cross_guard');
    assert.equal(EnemyAi.LabelOf(enemy.Actions.get('cross_guard')).Kind, OmenKind.Move);

    state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
    const end = TurnLoop.EndTurn(state.with({ Player: state.Player.with({ Stamina: 0 }) }), NoShuffle);

    assert.ok(ids(end).includes('cross_guard'));
    assert.equal(end.State.Gap, 1);
    assert.equal(end.State.Enemy.Guard, 5);
    assert.equal(end.State.Enemy.Statuses.Stacks(StatusKind.Parry), 1);
  });

  test('TheTwinSlash_TakesOneMultiplierABlow_AndSpendsEmpowerOnceOrNotAtAll', () => {
    // battle_core §5.1 (§19.5 S13): one multiplier a blow, 脆化 first. 強化 rides the blows
    // 脆化 does not take and is spent once for the face; when 脆化 takes every blow it stays.
    const enemy = Enemies.TwinBladeWarped;
    const setup = new BattleSetup(enemy, Fillers(20), BattleSetup.SliceFieldCells, { StartGap: 0 });
    let state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;
    state = state.WithEnemy(state.Enemy.with({ Statuses: SS([StatusKind.Empower, 1]) }));
    state = state.with({ Player: state.Player.with({ Guard: 0, Stamina: 0 }) });

    // No 脆化 on the player: 強化 takes both blows (8 × 1.5 → 12) and is spent once.
    const bare = TurnLoop.EndTurn(state, NoShuffle);
    // 脆化 2 on the player: 脆化 takes both blows, and 強化 waits for the next attack.
    const fragile = TurnLoop.EndTurn(state.with({ Player: state.Player.with({ Statuses: SS([StatusKind.Fragile, 2]) }) }), NoShuffle);

    assert.deepEqual(ofType(bare.Events, DamageDealt).filter((d) => d.Target === Actor.Player).map((b) => b.Raw), [12, 12]);
    assert.equal(ofType(bare.Events, StatusConsumed).filter((c) => c.Kind === StatusKind.Empower).length, 1);
    assert.equal(bare.State.Enemy.Statuses.Has(StatusKind.Empower), false);

    assert.deepEqual(ofType(fragile.Events, DamageDealt).filter((d) => d.Target === Actor.Player).map((b) => b.Raw), [12, 12]);
    assert.equal(ofType(fragile.Events, StatusConsumed).some((c) => c.Kind === StatusKind.Empower), false);
    assert.equal(fragile.State.Enemy.Statuses.Stacks(StatusKind.Empower), 1);
    assert.equal(fragile.State.Player.Statuses.Has(StatusKind.Fragile), false, '2 − 1 − 1');
  });

  test('TheHelmSplitter_BreaksTwoWhenAdjacent', () => {
    // roster §3.1: 崩し 1, +1 at gap 0.
    const enemy = Enemies.ArmoredWarden;
    const setup = new BattleSetup(enemy, Fillers(20), CellsFor(enemy), { StartGap: 0 });
    let state = TurnLoop.Start(setup, NoShuffle).State;
    assert.equal(state.Omen.ActionId, 'helm_splitter');
    state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
    const end = TurnLoop.EndTurn(state, NoShuffle);

    const broken = ofType(end.Events, StaminaBroken)[0];
    assert.equal(broken.Amount, 2);
    assert.equal(broken.Target, Actor.Player);
  });

  test('TheBosses_PutTheirOwnWords_AndNoStandInIsLeft', () => {
    // roster §4.1 / §5.1 / §6.2: each boss gives its two words, not the common word that stood in for them (#189).
    const kinds = (action) => action.Face.StatusList.map((g) => g.Kind);
    assert.deepEqual(kinds(Enemies.MiasmaPriest.Actions.get('miasma_rite')), [StatusKind.MiasmaShroud, StatusKind.Regen]);
    eq([...Enemies.MiasmaPriest.Actions.get('binding_word').Face.StatusList], [new StatusGrant(StatusKind.Binding, 2), new StatusGrant(StatusKind.Intimidate, 1)]);
    assert.deepEqual(kinds(Enemies.AbyssAngler.Actions.get('hook_cast')), [StatusKind.Hook]);
    assert.deepEqual(kinds(Enemies.AbyssAngler.Actions.get('fathom_call')), [StatusKind.Depths, StatusKind.Regen]);
    assert.equal(Enemies.AbyssAngler.Actions.get('drown').Trait.Watch, StatusKind.Hook);
    assert.deepEqual(kinds(Enemies.DistortionRoot.Actions.get('root_grip')), [StatusKind.Rooting, StatusKind.Regen]);
    assert.deepEqual(Enemies.DistortionRoot.Actions.get('wither_breath').Face.StatusList.map((g) => [g.Kind, g.Stacks]),
      [[StatusKind.Withering, 1], [StatusKind.Fatigue, 1]]);
    assert.equal(Enemies.DistortionRoot.Actions.get('sweep').Trait.Watch, StatusKind.Rooting);

    // Every boss word comes from a boss, and every boss gives exactly two of them (BOSS_STATUS_KINDS).
    for (const enemy of Enemies.All) {
      const words = new Set([...enemy.Actions.values()].flatMap((a) => a.Face.StatusList).filter((g) => Statuses.IsBossOnly(g.Kind)).map((g) => g.Kind));
      assert.equal(words.size, enemy.Rank === EnemyRank.Boss ? Constants.BossStatusKinds : 0, enemy.Id);
    }
  });

  test('the boss words carry the roster caps: 瘴気纏い 1, 深み 2, 根張り 1, 枯らし 1 (2 from the stages)', () => {
    const cap = (enemy, action, kind) => enemy.Actions.get(action).Face.StatusList.find((g) => g.Kind === kind).Cap;
    assert.equal(cap(Enemies.MiasmaPriest, 'miasma_rite', StatusKind.MiasmaShroud), 1);
    assert.equal(Enemies.MiasmaPriest.Actions.get('miasma_rite').NotAtFoeCap, true);
    assert.equal(Enemies.MiasmaPriest.Actions.get('binding_word').NotWhileFoeHas, StatusKind.Binding);
    assert.equal(cap(Enemies.AbyssAngler, 'fathom_call', StatusKind.Depths), Statuses.BossWordStackMax);
    assert.equal(cap(Enemies.AbyssAngler, 'hook_cast', StatusKind.Hook), 0, '鉤爪 has no stack limit');
    assert.equal(cap(Enemies.DistortionRoot, 'root_grip', StatusKind.Rooting), 1);
    assert.equal(cap(Enemies.DistortionRoot, 'wither_breath', StatusKind.Withering), 1);
    eq(Enemies.DistortionRoot.Actions.get('root_grip').Face.Reach, Reach.Default, 'the base 根を張る reaches 0〜1 (written out, not null)');
    const creeping = Enemies.DistortionRoot.Actions.get('creeping_root');
    assert.equal(creeping.PullBeyond, 1);
    assert.equal(creeping.Omen, OmenKind.Guard);
    assert.equal(creeping.Face.Push, 1);
  });
});

// ---- v4.5 (#257): one attribute and one omen kind per action ----

/** Each enemy's actions as the roster's tables write them: id → 属性 (A / G / Sk / St) and 予兆. */
const RosterColumns = {
  polearm_warped: 'sweep:A:攻 shove:A:攻 reach_thrust:A:攻 guard_up:G:防 step_forward:G:移',
  shadow_hound: 'bite:A:攻 lunge_in:A:攻 dash:Sk:移 crouch:G:防',
  rusted_revenant: 'iron_body:St:構 heavy_swing:A:攻 press:A:攻 trudge:G:移',
  crossbow_hunter: 'bolt:A:攻 backstep:A:攻 kick_off:A:攻 brace:G:防',
  mist_archer: 'mist_arrow:A:攻 fade:G:防 scatter:A:攻',
  twin_blade_warped: 'twin_slash:A:攻 step_slash:A:攻 retreat_cut:A:攻 cross_guard:G:移',
  polearm_crystal: 'great_thrust:A:攻 crystal_rush:A:攻 recoil_thrust:A:攻 short_jab:A:攻 haft_guard:G:防',
  armored_warden: 'iron_wall:St:構 helm_splitter:A:攻 push_shield:A:攻 shield_bash:A:攻 advance_guard:G:移 brace:G:防',
  pack_alpha: 'hunt_stance:St:構 rend:A:攻 pounce:A:攻 herd:Sk:技 howl:Sk:技 crouch:G:防',
  polearm_unyielding: 'set_spear:St:構 long_thrust:A:攻 haft_shove:Sk:技 hook_in:Sk:技 plate_guard:G:防 clank_on:G:移',
  miasma_priest: 'ward:St:構 miasma_rite:Sk:技 staff_strike:A:攻 miasma_bolt:A:攻 push_back:A:攻 binding_word:Sk:技 coil:G:防',
  abyss_angler: 'hook_cast:A:攻 drown:A:攻 fathom_call:Sk:技 line_whip:A:攻 deep_water:St:構 reel_in:Sk:技 slack_line:G:移',
  distortion_root: 'sweep:A:攻 thorn_volley:A:攻 wither_breath:Sk:技 root_grip:Sk:技 twist:A:攻 bark:St:構 creeping_root:G:防',
};

/** roster §2〜§6's 決定木, "gap 0 | 1〜2 | 3 以上", for all thirteen. */
const RosterTrees = {
  polearm_warped: 'shove guard_up reach_thrust | sweep reach_thrust guard_up | step_forward guard_up',
  shadow_hound: 'bite crouch | lunge_in crouch | dash crouch',
  rusted_revenant: 'heavy_swing iron_body trudge | press trudge | iron_body trudge',
  crossbow_hunter: 'kick_off brace | backstep brace | bolt brace',
  mist_archer: 'fade scatter | fade scatter | mist_arrow scatter',
  twin_blade_warped: 'twin_slash retreat_cut | step_slash retreat_cut | cross_guard',
  polearm_crystal: 'recoil_thrust short_jab haft_guard | great_thrust short_jab haft_guard | crystal_rush haft_guard',
  armored_warden: 'helm_splitter shield_bash brace | push_shield advance_guard brace | iron_wall advance_guard brace',
  pack_alpha: 'rend herd crouch | pounce crouch howl | hunt_stance pounce howl',
  polearm_unyielding: 'haft_shove plate_guard | long_thrust plate_guard | set_spear hook_in clank_on',
  miasma_priest: 'staff_strike coil | miasma_bolt binding_word coil | miasma_rite ward miasma_bolt coil',
  abyss_angler: 'drown deep_water fathom_call line_whip | reel_in line_whip slack_line | hook_cast reel_in',
  distortion_root: 'twist root_grip sweep creeping_root | sweep root_grip thorn_volley creeping_root | wither_breath thorn_volley creeping_root',
};

/**
 * roster §2〜§6's 特性 column, "action:trait" with "—" for none. Written short:
 * condition / effect / amount, then @threshold, (watched word), [combo attribute] or {granted word}.
 */
const RosterTraits = {
  polearm_warped: 'sweep:GapAtLeast/PowerBonus/3@2 shove:Unguarded/PowerBonus/3 reach_thrust:— guard_up:— step_forward:—',
  shadow_hound: 'bite:— lunge_in:GapAtLeast/PowerBonus/3@2 dash:— crouch:—',
  rusted_revenant: 'iron_body:— heavy_swing:Broken/PowerBonus/3 press:— trudge:—',
  crossbow_hunter: 'bolt:— backstep:Reserve/GuardBonus/3@4 kick_off:— brace:—',
  mist_archer: 'mist_arrow:GapAtLeast/PowerBonus/5@3 fade:Unguarded/NextTurnRecovery/1 scatter:—',
  twin_blade_warped: 'twin_slash:— step_slash:— retreat_cut:— cross_guard:Reserve/Status/0@4{Parry,1,self}',
  polearm_crystal: 'great_thrust:— crystal_rush:GapAtLeast/PowerBonus/3@3 recoil_thrust:Unguarded/PowerBonus/3 short_jab:— haft_guard:—',
  armored_warden: 'iron_wall:— helm_splitter:GapAtMost/BreakBonus/1 push_shield:— shield_bash:Combo/PowerBonus/3[Attack] advance_guard:— brace:—',
  pack_alpha: 'hunt_stance:— rend:— pounce:GapAtLeast/PowerBonus/5@2 herd:— howl:— crouch:Reserve/GuardBonus/3@5',
  polearm_unyielding: 'set_spear:— long_thrust:Combo/PowerBonus/3[Skill] haft_shove:— hook_in:— plate_guard:Reserve/GuardBonus/3@5 clank_on:—',
  miasma_priest: 'ward:— miasma_rite:— staff_strike:GapAtMost/PowerBonus/5 miasma_bolt:GapAtLeast/PowerBonus/7@3 push_back:Unguarded/PowerBonus/5 binding_word:— coil:—',
  abyss_angler: 'hook_cast:GapAtLeast/PowerBonus/3@3 drown:FoeHas/PowerBonus/3(Hook) fathom_call:— line_whip:FoeHas/PowerBonus/3(Slow) deep_water:— reel_in:— slack_line:—',
  distortion_root: 'sweep:FoeHas/PowerBonus/5(Rooting) thorn_volley:GapAtLeast/PowerBonus/5@3 wither_breath:— root_grip:— twist:Broken/PowerBonus/3 bark:— creeping_root:—',
};

/** C# `BattleAttribute.ToString()` for a single flag (the roster's combo attributes). */
function attributeName(value) {
  for (const [name, bit] of Object.entries(A)) if (bit === value) return name;
  return String(value);
}

function Short(trait) {
  if (trait == null) return '—';
  let text = trait.Condition + '/' + trait.Effect + '/' + trait.Amount;
  if (trait.Threshold !== 0) text += '@' + trait.Threshold;
  if (trait.Watch != null) text += '(' + trait.Watch + ')';
  if (trait.Attribute !== A.None) text += '[' + attributeName(trait.Attribute) + ']';
  if (trait.Grant != null) text += '{' + trait.Grant.Kind + ',' + trait.Grant.Stacks + (trait.Grant.OnSelf ? ',self' : '') + '}';
  return text;
}

describe('EnemyRosterTests: every action, tree and trait as the roster writes them', () => {
  for (const id of AllIds()) {
    test(`EveryAction_CountsAsTheRostersAttribute_AndShowsItsOmenKind(${id})`, () => {
      const enemy = Enemies.ById(id);
      const rows = RosterColumns[id].split(' ').map((row) => row.split(':'));
      sameSet([...enemy.Actions.keys()], rows.map((r) => r[0]), id + ": the roster's actions, no more and no fewer");
      for (const row of rows) {
        const action = enemy.Actions.get(row[0]);
        const attribute = { A: A.Attack, G: A.Guard, Sk: A.Skill }[row[1]] ?? A.Stance;
        const omen = { 攻: OmenKind.Attack, 防: OmenKind.Guard, 移: OmenKind.Move, 構: OmenKind.Stance }[row[2]] ?? OmenKind.Skill;
        assert.equal(action.Attribute, attribute, id + '.' + row[0] + ' 属性');
        assert.equal(EnemyAi.LabelOf(action).Kind, omen, id + '.' + row[0] + ' 予兆');
      }
    });

    test(`TheBaseTree_IsTheRosters(${id})`, () => {
      const def = Enemies.ById(id);
      const tree = [def.BranchAtGapZero, def.BranchAtGapOneToTwo, def.BranchAtGapThreePlus].map((branch) => branch.join(' ')).join(' | ');
      assert.equal(tree, RosterTrees[id]);
    });

    test(`EveryTrait_IsTheRosters(${id})`, () => {
      const def = Enemies.ById(id);
      const actionIds = RosterTraits[id].split(' ').map((pair) => pair.substring(0, pair.indexOf(':')));
      const traits = actionIds.map((action) => action + ':' + Short(def.Actions.get(action).Trait)).join(' ');
      sameSet([...def.Actions.keys()], actionIds);
      assert.equal(traits, RosterTraits[id]);
    });
  }

  test('TheCrystalSpear_MovesItselfWithEveryBlow', () => {
    // roster §2.7: HP 90; the rush closes 2 from 2〜4 (+3 at 3+), the recoil thrust backs off 2 at 0.
    const def = Enemies.PolearmCrystal;
    assert.deepEqual([def.MaxHp, def.Rank, def.Size], [90, EnemyRank.Normal, 1]);
    assert.deepEqual([...def.BranchAtGapZero], ['recoil_thrust', 'short_jab', 'haft_guard']);
    assert.deepEqual([...def.BranchAtGapOneToTwo], ['great_thrust', 'short_jab', 'haft_guard']);
    assert.deepEqual([...def.BranchAtGapThreePlus], ['crystal_rush', 'haft_guard']);
    const great = def.Actions.get('great_thrust');
    assert.equal(great.Column, 3);
    assert.equal(great.Face.Power, 15);
    eq(great.Face.ReachOrDefault, new Reach(1, 2));
    assert.equal(def.Actions.get('crystal_rush').Face.Move, 2);
    eq(def.Actions.get('crystal_rush').Face.ReachOrDefault, new Reach(2, 4));
    assert.equal(def.Actions.get('recoil_thrust').Face.Move, -2);
    eq(def.Actions.get('recoil_thrust').Face.ReachOrDefault, Reach.Only(0));
    assert.ok([...def.Actions.values()].filter((a) => a.Face.Move !== 0).every((a) => a.Attributes === A.Attack), 'every move rides an attack');
    assert.ok([...def.Actions.values()].every((a) => a.Face.Push === 0), 'it never moves the player');
  });

  test('TheUnyieldingSpear_MovesThePlayer_ThenThrustsWithTheSkillsCombo', () => {
    // roster §3.3 典型: at gap 3, 穂先を据える → 穂で掛け寄せる (pull 2) leaves gap 1; the next
    // phase, 長穂の突き 8 + 構え 3 = 11, then 甲で受ける.
    const def = Enemies.PolearmUnyielding;
    let state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(def, Fillers(40), CellsFor(def)), NoShuffle).State, NoShuffle).State;
    const first = TurnLoop.EndTurn(state, NoShuffle);
    state = TurnLoop.BeginPlayerTurn(first.State, NoShuffle).State;
    state = state.with({ Player: state.Player.with({ Guard: 0 }) });
    const second = TurnLoop.EndTurn(state, NoShuffle);

    assert.deepEqual([def.MaxHp, def.Rank, def.ActionsPerPhase], [120, EnemyRank.Elite, 2]);
    assert.deepEqual(ids(first), ['set_spear', 'hook_in']);
    assert.equal(first.State.Gap, 1);
    assert.deepEqual(ids(second), ['long_thrust', 'plate_guard']);
    assert.equal(ofType(second.Events, DamageDealt).find((d) => d.Target === Actor.Player).Raw, 11);
    assert.equal(EnemyAi.LabelOf(def.Actions.get('haft_shove')).Kind, OmenKind.Skill, '予兆 技');
    assert.equal(EnemyAi.LabelOf(def.Actions.get('hook_in')).Kind, OmenKind.Skill);
    assert.equal(EnemyAi.LabelOf(def.Actions.get('clank_on')).Kind, OmenKind.Move, '予兆 移動');
  });

  for (const [enemy, action, min, max] of [
    ['crossbow_hunter', 'bolt', 2, 5],
    ['mist_archer', 'mist_arrow', 2, 4],
    ['miasma_priest', 'miasma_bolt', 1, 4],
  ]) {
    test(`TheFarShot_ReachesWhatTheRosterSays(${enemy}.${action})`, () => {
      // roster §2.4 / §2.5 / §4.2 (#196): 弩の一射 2〜5, 靄の矢 2〜4, 瘴気の矢 1〜4.
      const def = Enemies.ById(enemy).Actions.get(action);
      eq(def.Face.ReachOrDefault, new Reach(min, max));
      eq(EnemyAi.LabelOf(def).Reach, new Reach(min, max), 'the omen aims at those cells');
    });
  }

  for (const [enemy, zero, oneToTwo, threePlus] of [
    ['crossbow_hunter', ['kick_off', 'brace'], ['backstep', 'brace'], ['bolt', 'brace']],
    ['mist_archer', ['fade', 'scatter'], ['fade', 'scatter'], ['mist_arrow', 'scatter']],
    ['miasma_priest', ['staff_strike', 'coil'], ['miasma_bolt', 'binding_word', 'coil'], ['miasma_rite', 'ward', 'miasma_bolt', 'coil']],
  ]) {
    test(`TheBranches_AreTheRostersThree(${enemy})`, () => {
      const def = Enemies.ById(enemy);
      assert.deepEqual([...def.BranchAtGapZero], zero);
      assert.deepEqual([...def.BranchAtGapOneToTwo], oneToTwo);
      assert.deepEqual([...def.BranchAtGapThreePlus], threePlus);
    });
  }

  for (const [enemy, action, description] of [
    ['polearm_warped', 'guard_up', 'Guard 3'],
    ['shadow_hound', 'crouch', 'Guard 3'],
    ['polearm_crystal', 'haft_guard', 'Guard 3'],
    ['twin_blade_warped', 'step_slash', '間合い 1〜2 に届く。当ててから前へ 2'],
    ['pack_alpha', 'rend', '間合い 0 に届く。出血を 1 付与する'],
    ['armored_warden', 'brace', 'Guard 3'],
  ]) {
    test(`TheSevenTraitsV415TookOff_AreGone_FromTheDataAndTheText(${enemy}.${action})`, () => {
      const def = Enemies.ById(enemy).Actions.get(action);
      assert.equal(def.Trait, null);
      assert.equal(def.Description, description);
    });
  }

  test('every description, byte for byte (roster §2〜§6, the text the omen and the journal show)', () => {
    const texts = Object.fromEntries(Enemies.All.flatMap((e) => [...e.Actions.values()].map((a) => [`${e.Id}.${a.Id}`, `${a.Name}|${a.Column}|${a.Description}`])));
    assert.deepEqual(texts, {
      'polearm_warped.sweep': '払い斬り|2|間合い 1〜2 に届く。間合い 2 以上: 威力 +3',
      'polearm_warped.shove': '柄尻の押し込み|2|間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +3',
      'polearm_warped.reach_thrust': '穂先の突き|1|間合い 0〜2 に届く',
      'polearm_warped.guard_up': '柄で受ける|1|Guard 3',
      'polearm_warped.step_forward': '踏み込み|1|前へ 1 動き、Guard 2 を得る',
      'shadow_hound.bite': '噛みつき|2|間合い 0 に届く。出血を 2 付与する',
      'shadow_hound.lunge_in': '跳びかかり|2|間合い 1〜2 に届く。当ててから前へ 2。間合い 2 以上: 威力 +3',
      'shadow_hound.dash': '駆け寄る|1|前へ 2',
      'shadow_hound.crouch': '身を低くする|1|Guard 3',
      'rusted_revenant.iron_body': '鉄の身|2|構え: 毎ターン開始に Guard +3',
      'rusted_revenant.heavy_swing': '大振り|3|間合い 0 に届く。崩し後: 相手のスタミナ 3 未満で威力 +3',
      'rusted_revenant.press': '押し込み|1|間合い 1〜2 に届く。当ててから前へ 1',
      'rusted_revenant.trudge': 'のし歩く|1|前へ 1、Guard 2',
      'crossbow_hunter.bolt': '弩の一射|3|間合い 2〜5 に届く。威圧を 1 付与する',
      'crossbow_hunter.backstep': '退き撃ち|2|間合い 1〜2 に届く。撃ってから後ろへ 1。温存: 残 4 以上で Guard +3',
      'crossbow_hunter.kick_off': '蹴り離し|2|間合い 0 に届く。相手を 2 マス押す',
      'crossbow_hunter.brace': '盾を構える|1|Guard 3',
      'mist_archer.mist_arrow': '靄の矢|3|間合い 2〜4 に届く。疲労を 1 付与する。間合い 3 以上: 威力 +5',
      'mist_archer.fade': '靄に溶ける|2|間合い 0〜1 に届く。Guard 4、相手を 2 マス押す。無防備: 次の回復 +1',
      'mist_archer.scatter': '散り矢|1|間合い 0〜2 に届く',
      'twin_blade_warped.twin_slash': '二段斬り|3|間合い 0 に届く。威力 8 × 2',
      'twin_blade_warped.step_slash': '踏み込み斬り|2|間合い 1〜2 に届く。当ててから前へ 2',
      'twin_blade_warped.retreat_cut': '退き斬り|1|間合い 0〜1 に届く',
      'twin_blade_warped.cross_guard': '十字受け|1|前へ 2、Guard 2。温存: 残 4 以上で見切りを 1 付与する',
      'polearm_crystal.great_thrust': '大穂の突き|3|間合い 1〜2 に届く。威力 15',
      'polearm_crystal.crystal_rush': '晶穂の突進|2|間合い 2〜4 に届く。当ててから前へ 2。間合い 3 以上: 威力 +3',
      'polearm_crystal.recoil_thrust': '引き突き|2|間合い 0 に届く。当ててから後ろへ 2。無防備: 相手の Guard が 0 なら威力 +3',
      'polearm_crystal.short_jab': '短い突き|1|間合い 0〜2 に届く',
      'polearm_crystal.haft_guard': '柄を立てる|1|Guard 3',
      'armored_warden.iron_wall': '鉄壁|2|構え: 毎ターン開始に Guard +3',
      'armored_warden.helm_splitter': '打ち下ろし|2|間合い 0〜1 に届く。崩し 1。間合い 0: 崩し +1',
      'armored_warden.push_shield': '盾で押し出る|2|間合い 1〜2 に届く。当ててから前へ 1',
      'armored_warden.shield_bash': '盾打ち|1|間合い 0 に届く。鈍足を 2 付与する。連動: 同じフェーズにアタックを出していれば威力 +3',
      'armored_warden.advance_guard': '盾を掲げて前進|1|Guard 2、前へ 1',
      'armored_warden.brace': '盾を構える|1|Guard 3',
      'pack_alpha.hunt_stance': '狩りの構え|2|構え: 相手との間合いが 0 のときアタック威力 +3',
      'pack_alpha.rend': '引き裂き|2|間合い 0 に届く。出血を 1 付与する',
      'pack_alpha.pounce': '飛びかかり|2|間合い 1〜3 に届く。当ててから前へ 2。間合い 2 以上: 威力 +5',
      'pack_alpha.herd': '追い立てる|1|間合い 0 に届く。相手を 1 マス押し、鈍足を 2 付与する',
      'pack_alpha.howl': '遠吠え|1|自分に強化を 1 付与する',
      'pack_alpha.crouch': '身を伏せる|1|Guard 3。温存: 残 5 以上で Guard +3',
      'polearm_unyielding.set_spear': '穂先を据える|2|構え: 相手との間合いが 1 以上のとき攻撃威力 +3',
      'polearm_unyielding.long_thrust': '長穂の突き|2|間合い 1〜3 に届く。連動: 同じフェーズにスキルを出していれば威力 +3',
      'polearm_unyielding.haft_shove': '柄で突き放す|1|間合い 0 に届く。相手を 2 マス押す',
      'polearm_unyielding.hook_in': '穂で掛け寄せる|1|間合い 3〜5 に届く。相手を 2 マス引く',
      'polearm_unyielding.plate_guard': '甲で受ける|1|Guard 3。温存: 残 5 以上で Guard +3',
      'polearm_unyielding.clank_on': '甲を鳴らして進む|1|Guard 2、前へ 1',
      'miasma_priest.ward': '瘴気の帳|2|構え: 相手との間合いが 2 以上のターン開始に Guard +5',
      'miasma_priest.miasma_rite': '瘴気の儀|3|間合い 2〜4 に届く。瘴気纏いを 1 付与する。自分に再生を 1 付与する。瘴気纏いが上限のあいだは使わない',
      'miasma_priest.staff_strike': '鎌首の打ち込み|3|間合い 0〜1 に届く。間合い 0: 威力 +5',
      'miasma_priest.miasma_bolt': '瘴気の矢|2|間合い 1〜4 に届く。疲労を 1 付与する。間合い 3 以上: 威力 +7',
      'miasma_priest.push_back': '払いのけ|2|間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +5',
      'miasma_priest.binding_word': '縛りの言葉|1|間合い 2〜4 に届く。呪縛を 2 付与する。威圧を 1 付与する。呪縛が残っているあいだは使わない',
      'miasma_priest.coil': 'とぐろを締める|1|Guard 3',
      'abyss_angler.hook_cast': '鉤縄を打つ|3|間合い 2〜4 に届く。鉤爪を 1 付与する。間合い 3 以上: 威力 +3',
      'abyss_angler.drown': '引きずり込む|2|間合い 0〜1 に届く。相手の状態（鉤爪）: 威力 +3',
      'abyss_angler.fathom_call': '深みへの呼び声|2|間合い 0〜1 に届く。深みを 1 付与する。自分に再生を 1 付与する',
      'abyss_angler.line_whip': '糸の一打|2|間合い 0〜2 に届く。相手の状態（鈍足）: 威力 +3',
      'abyss_angler.deep_water': '淀みを張る|2|構え: 相手との間合いが 1 以下のターン開始に Guard +5',
      'abyss_angler.reel_in': '手繰り寄せる|1|間合い 1〜4 に届く。相手を 2 マス引き、鈍足を 2 付与する',
      'abyss_angler.slack_line': '糸を緩める|1|Guard 2、後ろへ 1',
      'distortion_root.sweep': '根の横払い|2|間合い 0〜2 に届く。相手の状態（根張り）: 威力 +5',
      'distortion_root.thorn_volley': '棘を飛ばす|2|間合い 1〜4 に届く。出血を 1 付与する。間合い 3 以上: 威力 +5',
      'distortion_root.wither_breath': '枯らしの息|2|間合い 2〜4 に届く。枯らしを 1 付与する。疲労を 1 付与する',
      'distortion_root.root_grip': '根を張る|2|間合い 0〜1 に届く。根張りを 1 付与する。自分に再生を 1 付与する',
      'distortion_root.twist': '捻じる|2|間合い 0〜1 に届く。崩し 1。崩し後: 相手のスタミナ 3 未満で威力 +3',
      'distortion_root.bark': '樹皮|2|構え: 相手との間合いが 2 以上のターン開始に Guard +5',
      'distortion_root.creeping_root': '伸びる根|1|間合い 0〜4 に届く。Guard 2。相手が間合い 1 以下なら 1 マス押し、2 以上なら 1 マス引く',
    });
  });

  for (const [enemy, gap, first] of [
    ['crossbow_hunter', 4, 'bolt'],
    ['crossbow_hunter', 5, 'bolt'],
    ['mist_archer', 4, 'mist_arrow'],
    ['miasma_priest', 4, 'miasma_rite'],
  ]) {
    test(`FromTheFarthestGap_TheFarBranchStillLands(${enemy}, ${gap})`, () => {
      // roster §1.2 (#196): an enemy that never steps forward has something in its 3+ branch that
      // reaches the farthest gap of its fields, so a player backed to cell 1 is still hit.
      const def = Enemies.ById(enemy);
      const action = EnemyAi.ChooseAction(def, gap, def.MaxStamina);
      assert.equal(action.Id, first);
      assert.ok(def.BranchAtGapThreePlus.map((id) => def.Actions.get(id))
        .some((a) => (a.Attributes & A.Attack) === A.Attack && a.Face.ReachOrDefault.Contains(gap)), 'an attack lands at ' + gap);
    });
  }

  test('TheBindingWord_IsNotTakenWhileBindingRemains_AndComesBackEveryOtherPhase', () => {
    // roster §4.3: 「縛りの言葉は、相手に呪縛が残っているフェーズには取りません。その枝の次の行動へ進みます」.
    // At gap 2 the 1〜2 branch is 瘴気の矢 → 縛りの言葉. The player stays at gap 2 and does nothing.
    const enemy = Enemies.MiasmaPriest;
    const setup = new BattleSetup(enemy, Fillers(40), CellsFor(enemy) + 1, { StartGap: 2 });
    let state = TurnLoop.Start(setup, NoShuffle).State;
    const taken = [];
    for (let turn = 0; turn < 4; turn++) {
      state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
      state = state.with({ Player: state.Player.with({ Cell: state.Enemy.Cell - 3 }) });
      const end = TurnLoop.EndTurn(state, NoShuffle);
      taken.push(ids(end));
      state = end.State;
    }

    assert.deepEqual(taken[0], ['miasma_bolt', 'binding_word'], 'no 呪縛 yet');
    assert.ok(!taken[1].includes('binding_word'), '呪縛 2 → 1 remains through the next phase');
    assert.ok(taken[2].includes('binding_word'), 'gone by then, so it binds again');
    assert.ok(!taken[3].includes('binding_word'));
  });
});

// ---- Every enemy, fought to the end ----

/** The test's player keeps its gap: a card that steps back is not played once the gap is 2 or more. */
const Fights = (state, def) => def.Face.Move >= 0 || state.Gap < 2;

/**
 * A legal random deck (§8) that holds at least three cards stepping forward, one of them a card
 * that closes from gap 3 (駆け込み or 疾風突き).
 */
function RandomDeck(rng, size) {
  const counts = new Map();
  const deck = [];
  const forward = CardCatalog.All.filter((c) => c.Face.Move > 0);
  // The two cards that close from gap 3 even under 鈍足: 駆け込み and 疾風突き.
  const leap = CardCatalog.All.filter((c) => c.Face.Move >= 2
    && (c.Targets === TargetKind.Self || c.Face.ReachOrDefault.Contains(3)));
  while (deck.length < size) {
    const pool = deck.filter((c) => leap.includes(c.Def)).length < 1 ? leap
      : deck.filter((c) => c.Def.Face.Move > 0).length < 3 ? forward
        : CardCatalog.All;
    const def = pool[Math.trunc(rng.NextDouble() * pool.length) % pool.length];
    const held = counts.get(def.Id) ?? 0;
    if (held >= Constants.CopiesMax) continue;
    counts.set(def.Id, held + 1);
    deck.push(new CardInstance(def.Id + '-' + held, def));
  }
  return deck;
}

describe('EnemyRosterTests: EachEnemy_FightsToTheEnd_WithAFixedSeed', () => {
  // #189 の完了条件: every enemy, fought with a fixed seed to the end without an exception. The
  // player plays the leftmost card it can pay for, with the prototype deck and with a random deck
  // of the eighty — except that it does not step further back once 2 or more cells part it from
  // the enemy (Fights).
  for (const id of AllIds()) {
    test(id, () => {
      const enemy = Enemies.ById(id);
      for (let seed = 1; seed <= 6; seed++) {
        const rng = new SeededRng(seed * 104729);
        const deck = seed % 2 === 0 ? PrototypeDeck.Build() : RandomDeck(rng, 20 + seed * 3);
        let state = TurnLoop.Start(new BattleSetup(enemy, deck, CellsFor(enemy)), rng).State;
        let turns = 0;
        assert.doesNotThrow(() => {
          while (state.Result === GameResult.Ongoing && turns < 200) {
            state = TurnLoop.BeginPlayerTurn(state, rng).State;
            turns++;
            while (state.Result === GameResult.Ongoing) {
              const s = state;
              const next = s.Hand.find((c) => TurnLoop.CanPlay(s, c.InstanceId) === PlayRefusal.None && Fights(s, c.Def));
              if (next == null) break;
              state = TurnLoop.PlayCard(state, next.InstanceId, rng).State;
            }
            if (state.Result === GameResult.Ongoing) state = TurnLoop.EndTurn(state, rng).State;
          }
        }, id + ' seed ' + seed);
        assert.notEqual(state.Result, GameResult.Ongoing, id + ' seed ' + seed + ' did not end in 200 turns');
      }
    });
  }
});

describe('EnemyRosterTests: the action check', () => {
  test('EveryRosterAction_PassesTheActionCheck', () => {
    // v4.4 (#256 の 3): the multi-hit rule binds enemy actions too, and a stance stands alone.
    // 二段斬り was the one placeholder; #51 put it on v4.5's 7 × 2, so nothing is let through,
    // the bosses' overrides included.
    const actions = [
      ...Enemies.All.flatMap((e) => [...e.Actions.values()]),
      ...Enemies.All.flatMap((e) => e.Switches ?? []).flatMap((s) => s.Overrides ?? []),
    ];
    const refused = [...new Set(actions.filter((a) => Cards.ValidateEnemyAction(a).length > 0).map((a) => a.Id))];
    assert.deepEqual(refused, []);
    assert.equal(Enemies.MultiHitRedesignPending.size, 0);
    assert.ok(Enemies.MultiHitRedesignPending instanceof Set);
  });

  test('TheActionCheck_RefusesAMultiHitActionThatPutsAStatusOnThePlayer_AndAStanceThatAlsoStrikes', () => {
    const bleeding = EnemyAction('bleeding', { face: new Face({ Power: 4, Hits: 2, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }) });
    const viaTrait = new EnemyActionDef('via_trait', 'via_trait', A.Attack, 1, new Face({ Power: 4, Hits: 2 }),
      new Trait(TraitCondition.FirstPlay, TraitEffect.Status, { Grant: new StatusGrant(StatusKind.Fragile, 1) }));
    const selfOnly = EnemyAction('self_only', { face: new Face({ Power: 4, Hits: 2, Statuses: [new StatusGrant(StatusKind.Empower, 1, { OnSelf: true })] }) });
    const single = EnemyAction('single', { face: new Face({ Power: 6, Statuses: [new StatusGrant(StatusKind.Bleed, 1)] }) });
    const strikingStance = EnemyAction('striking_stance', {
      face: new Face({ Power: 5, Stance: new StanceDef(StanceHook.TurnStart, { Guard: 1 }) }),
      attributes: A.Attack | A.Stance,
    });

    assert.deepEqual([...Cards.ValidateEnemyAction(bleeding)], ['Action "bleeding" strikes more than once and gives the opponent a status.']);
    assert.deepEqual([...Cards.ValidateEnemyAction(viaTrait)], ['Action "via_trait" strikes more than once and gives the opponent a status.']);
    assert.deepEqual([...Cards.ValidateEnemyAction(selfOnly)], [], 'a status on itself is fine');
    assert.deepEqual([...Cards.ValidateEnemyAction(single)], [], 'one blow may carry a status');
    assert.deepEqual([...Cards.ValidateEnemyAction(strikingStance)], ['Action "striking_stance" is a stance and also declares another attribute or moves.']);
  });
});

// ======================================================================== EnemyTreeReachTests

describe('EnemyTreeReachTests', () => {
  const GapMax = 8;

  /** The actions no scene chooses today, "enemy.action" → why. Empty since #446. */
  const NeverChosen = new Map();

  function Body(def, stamina, fatigue, bonus) {
    return new CombatantState(def.MaxHp, def.MaxHp, stamina, def.MaxStamina, 0, 6, def.Size,
      fatigue ? SS([StatusKind.Fatigue, 1]) : StatusSet.Empty, bonus);
  }

  /** Every stamina the omen can be decided on: StaminaAtAction from every stamina 0..max, with and without 疲労 and a 温存 bonus of 1. */
  function StaminasAtAction(def) {
    const staminas = new Set();
    for (let now = 0; now <= def.MaxStamina; now++) {
      for (const fatigue of [false, true]) {
        for (const bonus of [0, 1]) staminas.add(EnemyAi.StaminaAtAction(def, Body(def, now, fatigue, bonus)));
      }
    }
    return [...staminas].sort((a, b) => a - b);
  }

  /** roster §1.2: every set of stance actions the enemy may have used up, the empty one first. */
  function* SpentStanceSets(def) {
    const stances = [...def.Actions.values()].filter((a) => a.Face.Stance != null).map((a) => a.Id).sort(compareOrdinal);
    for (let mask = 0; mask < 1 << stances.length; mask++) {
      yield stances.filter((_, i) => (mask & (1 << i)) !== 0);
    }
  }

  /** roster §4.3: the player holding none, or 1 or 2 stacks of each word an action waits out or stops at the cap of. */
  function* FoeWords(def) {
    yield StatusSet.Empty;
    const waited = [...def.Actions.values()].filter((a) => a.NotWhileFoeHas != null).map((a) => a.NotWhileFoeHas);
    const capped = [...def.Actions.values()].filter((a) => a.NotAtFoeCap)
      .flatMap((a) => a.Face.StatusList.filter((g) => !g.OnSelf && g.Cap > 0).map((g) => g.Kind));
    for (const word of new Set([...waited, ...capped])) {
      yield SS([word, 1]);
      yield SS([word, 2]);
    }
  }

  /** Every action some scene chooses, with the first scene that did. */
  function ChosenSomewhere(def) {
    const chosen = new Map();
    const Saw = (action, scene) => {
      if (action != null && !chosen.has(action.Id)) chosen.set(action.Id, scene);
    };

    const switches = [null, ...(def.Switches ?? [])];
    for (const active of switches) {
      for (const spent of SpentStanceSets(def)) {
        for (const foe of FoeWords(def)) {
          for (let gap = 0; gap <= GapMax; gap++) {
            for (const stamina of StaminasAtAction(def)) {
              const scene = `switch ${active?.Id ?? 'base'}, gap ${gap}, stamina ${stamina}, spent [${spent.join(',')}]`;

              // §9 step 12: the omen, decided as DecideNextOmen decides it.
              const skip = [...spent, ...EnemyAi.Barred(def, foe, true, active)];
              const omen = EnemyAi.DecideOmen(def, gap, stamina, skip, active);
              if (omen.ActionId === EnemyAi.RestActionId) continue;
              const first = EnemyAi.ActionOf(def, omen.ActionId, active);
              Saw(first, scene + ', first');
              if (def.ActionsPerPhase < 2) continue;

              // §17.6 F11: the 予定 shown beside the omen.
              const plan = EnemyAi.DecidePlan(def, gap, stamina, omen, skip, active);
              if (plan != null && plan.ActionId !== EnemyAi.RestActionId) Saw(EnemyAi.ActionOf(def, plan.ActionId, active), scene + ', plan');

              // roster §1.3: the tree read again after the first action, as step 10 reads it.
              const again = [...spent, first.Id, ...EnemyAi.Barred(def, foe, false, active)];
              Saw(EnemyAi.ChooseAction(def, gap, stamina - first.Cost, again, active), scene + ', second');
            }
          }
        }
      }
    }
    return chosen;
  }

  for (const enemyId of AllIds()) {
    test(`EveryAction_IsChosenInSomeScene(${enemyId})`, () => {
      const def = Enemies.ById(enemyId);
      const chosen = ChosenSomewhere(def);
      const missing = [...def.Actions.keys()].filter((id) => !chosen.has(id) && !NeverChosen.has(def.Id + '.' + id)).sort(compareOrdinal);
      const stale = [...def.Actions.keys()].filter((id) => chosen.has(id) && NeverChosen.has(def.Id + '.' + id)).sort(compareOrdinal);
      assert.deepEqual(missing, [], def.Id + ': no gap, stamina, spent stance, word or switch chooses these actions.');
      assert.deepEqual(stale, [], def.Id + ': these are listed in NeverChosen but are chosen now.');
    });
  }

  test('TheWalk_SeesTheFatiguedStaminaOfOne', () => {
    // 錆槍の竜兵's 柄で受ける is chosen only at stamina 1 — 疲労 held, recovery 2 − 1 from 0.
    const def = Enemies.PolearmWarped;
    assert.equal(EnemyAi.StaminaAtAction(def, Body(def, 0, true, 0)), 1);
    assert.equal(EnemyAi.StaminaAtAction(def, Body(def, 0, false, 0)), 2);
    assert.ok(ChosenSomewhere(def).has('guard_up'));
  });

  test('TheTwoActionsV415MovedUp_AreChosenWhereTheRosterSays', () => {
    // roster §3.2 (v4.15, #396): after 狩りの構え, a 飛びかかり from 3〜4 lands at 1〜2 and the
    // redrawn second action is 身を伏せる, ahead of 遠吠え.
    const alpha = Enemies.PackAlpha;
    const alphaSecond = EnemyAi.ChooseAction(alpha, 1, alpha.MaxStamina - 2 - 2, ['hunt_stance', 'pounce']);
    // roster §5.3 (v4.15, #396): the first phase that hauls the player in is 引きずり込む → 淀みを張る.
    const angler = Enemies.AbyssAngler;
    const anglerSecond = EnemyAi.ChooseAction(angler, 0, 4 - 2, ['drown']);

    assert.equal(alphaSecond?.Id, 'crouch');
    assert.equal(anglerSecond?.Id, 'deep_water');
    assert.equal(NeverChosen.size, 0, 'nothing is left unreached since #446');
  });
});

// ======================================================================== BossSwitchTests

const G = A.Guard;
const St = A.Stance;
const None = [];

/** One finished player turn: the cards it played (folded), and how it ended. */
const Turn = (Cards, { Guard = 0, Gap = 3, Moved = false, Cell = 2, LastMove = 0 } = {}) => ({ Cards, Guard, Gap, Moved, Cell, LastMove });

function History(...turns) {
  let history = new BattleHistory();
  for (const turn of turns) {
    for (const card of turn.Cards) history = history.WithCard(card);
    history = history.WithTurnEnd(new PlayerTurnEnd(turn.Guard, turn.Gap, turn.Moved, turn.Cell, turn.Cards.length, turn.LastMove));
  }
  return history;
}

function View(boss, history, { hp = null, gap = 3, turn = null, held = [] } = {}) {
  return new AdaptationView(turn ?? history.TurnEnds.length, hp ?? boss.MaxHp, boss.MaxHp, [], history, gap, held);
}

const Active = (boss, view) => EnemyAi.ActiveSwitch(boss, view)?.Id ?? null;

function SwitchOf(boss, id) {
  const found = boss.Switches.filter((s) => s.Id === id);
  assert.equal(found.length, 1, id);
  return found[0];
}

describe('BossSwitchTests: 大黒蛇 セルク (§4.4)', () => {
  const priest = () => Enemies.MiasmaPriest;

  test('the switch order: each adaptation, then the stage and each adaptation over it', () => {
    assert.deepEqual(priest().Switches.map((s) => s.Id),
      ['turtle', 'breaker', 'pusher', 'second_stage', 'second_stage+turtle', 'second_stage+breaker', 'second_stage+pusher']);
    assert.deepEqual(Enemies.AbyssAngler.Switches.map((s) => s.Id),
      ['bait', 'snap', 'pin', 'second_stage', 'second_stage+bait', 'second_stage+snap', 'second_stage+pin']);
  });

  test('Priest_Turtle_AfterThreeAttacksInARow_CoilsThenWards_ForOnePhase', () => {
    const p = priest();
    const three = History(Turn([A.Attack, A.Attack]), Turn([A.Attack]));
    const after = History(Turn([A.Attack, A.Attack]), Turn([A.Attack]), Turn(None));
    const two = History(Turn([A.Attack, G, A.Attack]), Turn([A.Attack]));

    assert.equal(Active(p, View(p, three)), 'turtle', 'the third attack in a row came in the last turn');
    assert.deepEqual([...SwitchOf(p, 'turtle').BranchAtGapZero], ['coil', 'ward', 'miasma_bolt']);
    assert.deepEqual([...SwitchOf(p, 'turtle').BranchAtGapThreePlus], ['coil', 'ward', 'miasma_bolt']);
    assert.equal(Active(p, View(p, after)), null, '1 フェーズで戻る');
    assert.equal(Active(p, View(p, two)), null, 'a guard broke the run');
  });

  test('Priest_Turtle_CountsAgainFromZero_OnceTheRunReachesThree', () => {
    const p = priest();
    const AAA = [A.Attack, A.Attack, A.Attack];
    assert.equal(Active(p, View(p, History(Turn(AAA)))), 'turtle', '3 in turn 1');
    assert.equal(Active(p, View(p, History(Turn(AAA), Turn([A.Attack])))), null, 'the 4th attack is the 1st of a new run');
    assert.equal(Active(p, View(p, History(Turn(AAA), Turn([A.Attack, A.Attack])))), null, 'the 5th is the 2nd');
    assert.equal(Active(p, View(p, History(Turn(AAA), Turn(AAA)))), 'turtle', 'the 6th completes the second run');
  });

  test('Priest_Turtle_SixAttacksInOneTurn_SwitchesForOnePhaseOnly', () => {
    const p = priest();
    const six = Turn([A.Attack, A.Attack, A.Attack, A.Attack, A.Attack, A.Attack]);
    assert.equal(Active(p, View(p, History(six))), 'turtle', 'the phase after the six');
    assert.equal(Active(p, View(p, History(six, Turn(None)))), null, '1 フェーズで戻る');
    assert.equal(Active(p, View(p, History(six, Turn([A.Attack])))), null, 'the second run reset the count to 0');
    assert.equal(Active(p, View(p, History(six, Turn([A.Attack, A.Attack])))), null, '2 is not 3');
  });

  test('Priest_Turtle_AStanceCardOrAMoveOnlyCard_ResetsTheCount', () => {
    const p = priest();
    const moveOnly = AttributeRule.Fold(A.None, new Face({ Move: 1 }));
    assert.equal(moveOnly, A.Skill, 'a move-only card is counted as スキル');
    assert.equal(Active(p, View(p, History(Turn([A.Attack, A.Attack]), Turn([A.Attack])))), 'turtle', 'the same history without the break');
    assert.equal(Active(p, View(p, History(Turn([A.Attack, A.Attack, St]), Turn([A.Attack])))), null, 'a stance card broke the run');
    assert.equal(Active(p, View(p, History(Turn([A.Attack, A.Attack, moveOnly]), Turn([A.Attack])))), null, 'a move-only card broke the run');
    assert.equal(Active(p, View(p, History(Turn([A.Attack, St]), Turn([A.Attack, A.Attack, A.Attack])))), 'turtle', 'counted from 0 after the stance card');
  });

  test('Priest_Breaker_AfterTwoTurnsOnGuardNine_PutsTheBreakingStaffOnTop_UntilGuardFive', () => {
    const p = priest();
    const on = History(Turn(None, { Guard: 9 }), Turn(None, { Guard: 12 }));
    const kept = History(Turn(None, { Guard: 9 }), Turn(None, { Guard: 12 }), Turn(None, { Guard: 6 }));
    const off = History(Turn(None, { Guard: 9 }), Turn(None, { Guard: 12 }), Turn(None, { Guard: 5 }));
    const breaker = SwitchOf(p, 'breaker');

    assert.equal(Active(p, View(p, on)), 'breaker');
    assert.equal(Active(p, View(p, kept)), 'breaker', '6 is not yet 5 or less');
    assert.equal(Active(p, View(p, off)), null);
    assert.equal(breaker.BranchAtGapOneToTwo[0], 'staff_strike', '決定木の先頭へ出す');
    assert.deepEqual([...breaker.BranchAtGapThreePlus], ['miasma_rite', 'ward', 'miasma_bolt', 'coil'], 'v4.18 #497: 3+ stays');
    assert.equal(EnemyAi.ActionOf(p, 'staff_strike', breaker).Face.Break, 2, '崩しを 2 付与する');
    assert.equal(EnemyAi.ActionOf(p, 'staff_strike', breaker).Description, '間合い 0〜1 に届く。崩しを 2 付与する。間合い 0: 威力 +5');
    assert.equal(EnemyAi.ActionOf(p, 'staff_strike').Face.Break, 0, 'the base staff does not break');
  });

  test('Priest_Pusher_AfterTwoTurnsAdjacent_PushesBack_UntilTheGapOpens', () => {
    const p = priest();
    const close = History(Turn(None, { Gap: 0 }), Turn(None, { Gap: 0 }));
    assert.equal(Active(p, View(p, close, { gap: 0 })), 'pusher');
    assert.equal(Active(p, View(p, close, { gap: 2 })), null, '「間合いが 1 以上になる」で戻る');
    assert.equal(Active(p, View(p, History(Turn(None, { Gap: 0 })), { gap: 0 })), null, 'one turn is not two');
    assert.equal(SwitchOf(p, 'pusher').BranchAtGapOneToTwo[0], 'push_back', '全ての枝の先頭');
    assert.ok(!p.BranchAtGapZero.includes('push_back'));
    assert.deepEqual([...SwitchOf(p, 'pusher').BranchAtGapZero], ['push_back', 'staff_strike', 'coil']);
    assert.equal(EnemyAi.ChooseAction(p, 0, p.MaxStamina).Id, 'staff_strike', 'the base tree strikes at 0');
    assert.equal(EnemyAi.ChooseAction(p, 0, p.MaxStamina, null, SwitchOf(p, 'pusher')).Id, 'push_back');
  });

  test('Priest_SecondStage_FromHpSeventy_RitesEveryPhase_AndNeverGoesBack', () => {
    const p = priest();
    const quiet = History(Turn(None));
    const stage = SwitchOf(p, 'second_stage');
    assert.equal(Active(p, View(p, quiet, { hp: 71 })), null);
    assert.equal(Active(p, View(p, quiet, { hp: 70 })), 'second_stage');
    assert.equal(Active(p, View(p, quiet, { hp: 74, held: ['second_stage'] })), 'second_stage', '戻らない, 再生 or not');
    assert.equal(stage.BranchAtGapZero[0], 'miasma_rite');
    assert.equal(stage.BranchAtGapThreePlus[0], 'miasma_rite');
    const rite = stage.Override('miasma_rite');
    assert.equal(rite.Face.StatusList.find((g) => g.Kind === StatusKind.MiasmaShroud).Cap, 2);
    assert.equal(rite.Description, '間合い 2〜4 に届く。瘴気纏いを 1 付与する（2 まで重なる）。自分に再生を 1 付与する。瘴気纏いが上限のあいだは使わない');
    assert.equal(stage.EnterMove, 0);
    assert.equal(stage.Sway, false);
  });

  test('Priest_AnAdaptationOverTheStage_KeepsBoth_AndTheAdaptationLeads', () => {
    const p = priest();
    const close = History(Turn(None, { Gap: 0 }), Turn(None, { Gap: 0 }));
    const active = EnemyAi.ActiveSwitch(p, View(p, close, { hp: 60, gap: 0 }));
    assert.equal(active.Id, 'second_stage+pusher');
    assert.deepEqual(active.BranchAtGapZero.slice(0, 2), ['push_back', 'miasma_rite']);
    assert.notEqual(active.Override('miasma_rite'), null);
    // breaker over the stage carries both overrides: the stage's rite and its own staff.
    const breaker = SwitchOf(p, 'second_stage+breaker');
    assert.deepEqual(breaker.Overrides.map((o) => o.Id), ['miasma_rite', 'staff_strike']);
    assert.equal(SwitchOf(p, 'turtle').Overrides, null, 'an adaptation with no overrides over the base tree carries null');
  });

  test('Priest_TheBreakingStaff_BreaksTwoInPlay', () => {
    // Two turns on Guard 10: the next omen is the override's staff, which takes 2 stamina.
    const p = priest();
    let s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(p, Deck(), 8, { StartGap: 1 }), NoShuffle).State, NoShuffle).State;
    s = s.with({ PlayerHistory: History(Turn(None, { Guard: 10, Gap: 1 })), Player: s.Player.with({ Guard: 10, Stamina: 0 }) });
    const decided = TurnLoop.EndTurn(s, NoShuffle);
    assert.equal(decided.State.Enemies[0].ActiveSwitch, 'breaker');
    assert.equal(decided.State.Omen.ActionId, 'staff_strike');

    s = TurnLoop.BeginPlayerTurn(decided.State, NoShuffle).State;
    s = s.with({ Player: s.Player.with({ Guard: 10, Cell: s.Enemy.Cell - 2 }) });
    const struck = TurnLoop.EndTurn(s, NoShuffle);
    assert.equal(ofType(struck.Events, StaminaBroken).find((b) => b.Target === Actor.Player).Amount, 2);
  });
});

describe('BossSwitchTests: 獄竜 ガルド (§5.4)', () => {
  const angler = () => Enemies.AbyssAngler;

  test('Angler_Bait_AfterTwoTurnsAtTwoOrMore_HooksAndReels_UntilAdjacent', () => {
    const g = angler();
    const far = History(Turn(None, { Gap: 2 }), Turn(None, { Gap: 4 }));
    const bait = SwitchOf(g, 'bait');
    assert.equal(Active(g, View(g, far, { gap: 3 })), 'bait');
    assert.equal(Active(g, View(g, far, { gap: 0 })), null, '「間合いが 0 になる」で戻る');
    assert.deepEqual([...bait.BranchAtGapOneToTwo], ['hook_cast', 'reel_in']);
    assert.deepEqual([...bait.BranchAtGapThreePlus], ['hook_cast', 'reel_in']);
    assert.deepEqual([...bait.BranchAtGapZero], [...g.BranchAtGapZero], '0 is left as it is');
  });

  test('Angler_Snap_EverySecondHookTheyShake_SlacksThenCalls_ForOnePhase', () => {
    const g = angler();
    const After = (...snags) => {
      // At gap 1 throughout, so bait (2 or more) does not hold beside it.
      let history = History(Turn(None, { Gap: 1 }), Turn(None, { Gap: 1 }), Turn(None, { Gap: 1 }), Turn(None, { Gap: 1 }));
      for (const turn of snags) history = history.WithHookSnag(turn);
      return View(g, history, { turn: 4, gap: 1 });
    };
    assert.equal(Active(g, After(3, 4)), 'snap', 'the 2nd shaken off in the last turn');
    assert.equal(Active(g, After(4)), null, 'one is not two');
    assert.equal(Active(g, After(2, 3)), null, 'the 2nd came a turn earlier: that phase has passed');
    assert.equal(Active(g, After(1, 2, 3, 4)), 'snap', 'the 4th in the last turn');
    assert.equal(Active(g, After(1, 2, 3)), null, 'the 3rd is odd');
    assert.deepEqual([...SwitchOf(g, 'snap').BranchAtGapZero], ['slack_line', 'fathom_call']);
  });

  test('Angler_Pin_AfterTwoTurnsAdjacent_DrownsThenWhips', () => {
    const g = angler();
    const close = History(Turn(None, { Gap: 0 }), Turn(None, { Gap: 0 }));
    assert.equal(Active(g, View(g, close, { gap: 0 })), 'pin');
    assert.deepEqual([...SwitchOf(g, 'pin').BranchAtGapZero], ['drown', 'line_whip']);
  });

  test('Angler_SecondStage_StepsOutTwo_DropsTheSlackLine_AndHooksTwo', () => {
    // roster §5.4: 「ガルドが前へ 2 出る（出られるだけ）。以後は糸を緩めるを使わない。鉤縄が 1 回で鉤爪 2 スタックを付ける」.
    const g = angler();
    const stage = SwitchOf(g, 'second_stage');
    let s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(g, Deck(), 8, { StartGap: 4 }), NoShuffle).State, NoShuffle).State;
    s = s.WithEnemy(s.Enemy.with({ Hp: 80 }));
    const cellBefore = s.Enemy.Cell;
    const end = TurnLoop.EndTurn(s, NoShuffle);
    const enemyMoves = ofType(end.Events, CellsMoved).filter((m) => m.Actor === Actor.Enemy);
    const stepped = enemyMoves[enemyMoves.length - 1];

    assert.equal(stage.EnterMove, 2);
    const switched = ofType(end.Events, TreeSwitched);
    assert.equal(switched.length, 1);
    assert.equal(switched[0].To, 'second_stage');
    assert.ok(stepped.To < stepped.From, '前へ');
    assert.ok(end.State.Enemy.Cell < cellBefore);
    for (const each of g.Switches.filter((sw) => sw.Id.startsWith('second_stage'))) {
      for (const band of [each.BranchAtGapZero, each.BranchAtGapOneToTwo, each.BranchAtGapThreePlus]) {
        assert.ok(!(band ?? g.BranchAtGapZero).includes('slack_line'), each.Id);
      }
      assert.equal(each.Id === 'second_stage' ? each.EnterMove : 0, each.EnterMove, 'only the stage itself moves');
    }
    const hook = stage.Override('hook_cast');
    assert.equal(hook.Face.StatusList.length, 1);
    assert.equal(hook.Face.StatusList[0].Stacks, 2);
    assert.equal(hook.Description, '間合い 2〜4 に届く。鉤爪を 2 付与する。間合い 3 以上: 威力 +3');

    // The stage is entered once: the next decision does not step again.
    const again = TurnLoop.EndTurn(TurnLoop.BeginPlayerTurn(end.State, NoShuffle).State, NoShuffle);
    assert.deepEqual(ofType(again.Events, TreeSwitched), []);
  });
});

describe('BossSwitchTests: 歪みの根 (§6.1 / §6.4)', () => {
  const root = () => Enemies.DistortionRoot;

  test('Root_TheFiveAdaptations_HoldOnWhatTheyCount', () => {
    const r = root();
    const On = (history) => Active(r, View(r, history));

    const fourAttacks = History(Turn([A.Attack, A.Attack]), Turn([A.Attack, A.Attack]));
    const threeAttacks = History(Turn([A.Attack, A.Attack, A.Attack]));
    const guarded = History(Turn(None, { Guard: 9 }), Turn(None, { Guard: 10 }));
    const stance = History(Turn([St]));
    const twoWords = History(Turn(None), Turn(None)).WithInfliction(1).WithInfliction(2);
    const steppedIn = History(Turn(None, { Moved: true, Cell: 2, LastMove: 1 }), Turn(None, { Moved: true, Cell: 3, LastMove: 1 }));
    const steppedOut = History(Turn(None, { Moved: true, Cell: 3, LastMove: -1 }), Turn(None, { Moved: true, Cell: 2, LastMove: -1 }));
    const inOnTheSameCell = History(Turn(None, { Moved: true, Cell: 3, LastMove: 1 }), Turn(None, { Moved: true, Cell: 3, LastMove: 1 }));
    const outOnTheSameCell = History(Turn(None, { Moved: true, Cell: 3, LastMove: 1 }), Turn(None, { Moved: true, Cell: 3, LastMove: -1 }));
    const once = History(Turn(None, { Moved: false }), Turn(None, { Moved: true }));

    assert.equal(On(fourAttacks), 'root_a', '4 attacks');
    assert.equal(On(threeAttacks), null, '3 is not 4');
    assert.equal(On(guarded), 'root_g');
    assert.equal(On(stance), 'root_st');
    assert.equal(On(twoWords), 'root_sk');
    assert.equal(On(steppedIn), 'root_m_in');
    assert.equal(On(steppedOut), 'root_m_out');
    assert.equal(On(inOnTheSameCell), 'root_m_in', 'same turn-end cells, last move forward');
    assert.equal(On(outOnTheSameCell), 'root_m_out', 'same turn-end cells, last move back');
    assert.equal(On(once), null);
  });

  test('Root_RootA_CountsAgainFromZero_OnceTheRunReachesFour', () => {
    const r = root();
    const On = (history) => Active(r, View(r, history));
    const four = Turn([A.Attack, A.Attack, A.Attack, A.Attack]);
    const eight = Turn(Array(8).fill(A.Attack));
    assert.equal(On(History(four)), 'root_a');
    assert.equal(On(History(four, Turn([A.Attack, A.Attack, A.Attack]))), null, 'the 5th to 7th are the 1st to 3rd of a new run');
    assert.equal(On(History(four, Turn([A.Attack, A.Attack, A.Attack, A.Attack]))), 'root_a', 'the 8th completes the second run');
    assert.equal(On(History(eight)), 'root_a', 'two runs in one turn: one phase');
    assert.equal(On(History(eight, Turn([A.Attack, A.Attack, A.Attack]))), null, '3 after two runs');
  });

  test('Root_TheAdaptations_ReshapeTheTree_AsTheRosterSays', () => {
    const r = root();
    assert.deepEqual(SwitchOf(r, 'root_a').BranchAtGapOneToTwo.slice(0, 2), ['root_grip', 'twist'], '根を張る → 捻じる');
    assert.equal(SwitchOf(r, 'root_g').BranchAtGapThreePlus[0], 'twist');
    assert.equal(SwitchOf(r, 'root_g').Override('twist').Face.Break, 2, '捻じるの崩しを常に 2 にし');
    assert.equal(SwitchOf(r, 'root_st').BranchAtGapZero[0], 'wither_breath');
    assert.deepEqual(SwitchOf(r, 'root_sk').BranchAtGapZero.slice(0, 2), ['bark', 'sweep']);
    assert.equal(SwitchOf(r, 'root_sk').Cleanse, true);
    // roster §6.5 v4.15 (#396, #446): 樹皮 is taken only by root_sk, never by the base tree.
    assert.ok(![...r.BranchAtGapZero, ...r.BranchAtGapOneToTwo, ...r.BranchAtGapThreePlus].includes('bark'));
    assert.equal(SwitchOf(r, 'root_m_in').BranchAtGapZero[0], 'creeping_root');
    assert.equal(EnemyAi.FaceAt(SwitchOf(r, 'root_m_in').Override('creeping_root'), 3).Push, 1, 'moved in: pushed back out');
    assert.equal(EnemyAi.FaceAt(SwitchOf(r, 'root_m_out').Override('creeping_root'), 0).Push, -1, 'moved out: pulled back in');
    assert.equal(SwitchOf(r, 'root_m_in').Override('creeping_root').PullBeyond, null);
    assert.equal(SwitchOf(r, 'root_m_in').Override('creeping_root').Description, '間合い 0〜4 に届く。Guard 2。相手を 1 マス押す');
    assert.equal(SwitchOf(r, 'root_m_out').Override('creeping_root').Description, '間合い 0〜4 に届く。Guard 2。相手を 1 マス引く');
  });

  test('root_st: the breath that stops a stance, over the base tree and over each stage (#334)', () => {
    const r = root();
    const base = SwitchOf(r, 'root_st').Override('wither_breath');
    assert.equal(base.Face.StopStance, true);
    eq(base.Face.ReachOrDefault, new Reach(2, 4));
    assert.equal(base.Description, '間合い 2〜4 に届く。枯らしを 1 付与する。疲労を 1 付与する。当たると、相手がいちばん新しく置いた永続の効果 1 つが 1 ターン働かない');
    assert.equal(r.Actions.get('wither_breath').Face.StopStance, false, 'the root\'s own breath does not stop');

    for (const id of ['stage_2+root_st', 'stage_3+root_st']) {
      const breath = SwitchOf(r, id).Override('wither_breath');
      assert.equal(breath.Face.StopStance, true, id);
      eq(breath.Face.ReachOrDefault, new Reach(0, 4), id + ': the stage\'s reach is kept');
      assert.equal(breath.Face.StatusList[0].Cap, Statuses.BossWordStackMax, id);
      assert.equal(breath.Description, '間合い 0〜4 に届く。枯らしを 1 付与する（2 まで重なる）。疲労を 1 付与する。当たると、相手がいちばん新しく置いた永続の効果 1 つが 1 ターン働かない', id);
    }
    // Over stage 3 the stage's 根を張る stays beside the stopping breath, the breath last (removed and added again).
    assert.deepEqual(SwitchOf(r, 'stage_3+root_st').Overrides.map((o) => o.Id), ['root_grip', 'wither_breath']);
  });

  test('Root_TheStages_RiseAtTwoThirds_AndSwayAtOneThird', () => {
    const r = root();
    const quiet = History(Turn(None));
    const stage2 = SwitchOf(r, 'stage_2');
    const stage3 = SwitchOf(r, 'stage_3');
    assert.equal(Active(r, View(r, quiet, { hp: 134 })), null);
    assert.equal(Active(r, View(r, quiet, { hp: 133 })), 'stage_2');
    assert.equal(Active(r, View(r, quiet, { hp: 66 })), 'stage_3', 'the later stage wins');
    assert.deepEqual([stage2.EnterMove, stage2.Sway], [1, false], 'せり上がる 前へ 1');
    assert.deepEqual([stage3.EnterMove, stage3.Sway], [0, true], '前へ 1 と後ろへ 1 を交互に');
    assert.equal(stage2.BranchAtGapZero[0], 'wither_breath');
    assert.deepEqual(stage3.BranchAtGapZero.slice(0, 2), ['root_grip', 'sweep']);
    assert.deepEqual([...stage3.BranchAtGapZero], ['root_grip', 'sweep', 'wither_breath', 'creeping_root']);
    assert.deepEqual([...stage3.BranchAtGapOneToTwo], ['root_grip', 'sweep', 'wither_breath', 'creeping_root']);
    assert.deepEqual([...stage3.BranchAtGapThreePlus], ['root_grip', 'wither_breath', 'creeping_root']);
    assert.equal(EnemyAi.ChooseAction(r, 3, 14 - 2, ['root_grip'], stage3).Id, 'wither_breath', '2 手目 at 3+');
    assert.equal(EnemyAi.ChooseAction(r, 2, 14 - 2, ['root_grip'], stage3).Id, 'sweep', '2 手目 at 0〜2');
    eq(stage3.Override('root_grip').Face.ReachOrDefault, new Reach(0, 4), '根を張るの届く間合いも 0〜4');
    eq(stage3.Override('wither_breath').Face.ReachOrDefault, new Reach(0, 4), "stage 2's reach carries on");
    assert.equal(stage3.Override('root_grip').Description, '間合い 0〜4 に届く。根張りを 1 付与する（2 まで重なる）。自分に再生を 1 付与する');
    assert.equal(Active(r, View(r, History(Turn([St])), { hp: 60 })), 'stage_3+root_st', 'the adaptations work through the stages');
  });

  test('Root_StageThree_SwaysForwardAndBack_PhaseByPhase', () => {
    const r = root();
    let s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(r, Deck(), 8, { StartGap: 3 }), NoShuffle).State, NoShuffle).State;
    s = s.WithEnemy(s.Enemy.with({ Hp: 60 }));
    s = TurnLoop.EndTurn(s, NoShuffle).State; // stage 2 and 3 are held from this step 12
    const moves = [];
    for (let turn = 0; turn < 3; turn++) {
      s = TurnLoop.BeginPlayerTurn(s, NoShuffle).State;
      s = s.with({ Player: s.Player.with({ Cell: 1 }) });
      const end = TurnLoop.EndTurn(s, NoShuffle);
      const sway = ofType(end.Events, CellsMoved).find((m) => m.Actor === Actor.Enemy && !m.Pushed);
      moves.push(sway == null ? 0 : sway.From - sway.To);
      s = end.State;
      if (s.Result !== GameResult.Ongoing) break;
    }
    const firstTwo = moves.slice(0, 2);
    assert.ok(csEquals(firstTwo, [-1, 1]) || csEquals(firstTwo, [1, -1]), 'one step each phase, the way alternating: ' + firstTwo);
  });

  test('Root_ShakesOffItsWords_WhenTwoStatusesHaveLanded', () => {
    const r = root();
    let s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(r, Deck(), 8, { StartGap: 3 }), NoShuffle).State, NoShuffle).State;
    s = s.WithEnemy(s.Enemy.with({ Statuses: SS([StatusKind.Bleed, 2], [StatusKind.Fragile, 1]) }));
    s = s.with({ PlayerHistory: new BattleHistory().WithInfliction(s.Turn).WithInfliction(s.Turn) });
    const end = TurnLoop.EndTurn(s, NoShuffle);

    assert.equal(end.State.Enemies[0].ActiveSwitch, 'root_sk');
    const cleared = ofType(end.Events, StatusCleared);
    sameSet(cleared.map((c) => `${c.Actor}/${c.Kind}/${c.Stacks}/${c.Unit}`), ['Enemy/Bleed/1/0', 'Enemy/Fragile/1/0'],
      '出血 2 ticked to 1 at the root\'s turn start; what is left is shaken off');
    assert.equal(end.State.Enemy.Statuses.KindCount, 0);
    assert.equal(end.State.Omen.ActionId, 'bark');
  });

  test('EveryBossSwitch_NamesOnlyTheBossesOwnActions_AndEndsEachBranchOnSomethingCheap', () => {
    for (const boss of Enemies.All.filter((e) => e.Rank === EnemyRank.Boss)) {
      assert.ok(boss.Switches != null && boss.Switches.length > 0, boss.Id);
      for (const each of boss.Switches) {
        for (const band of Bands) {
          const branch = EnemyAi.BranchFor(boss, band === GapBand.Zero ? 0 : band === GapBand.OneToTwo ? 1 : 3, each);
          assert.ok(branch.length > 0, boss.Id + '/' + each.Id);
          assert.ok(branch.every((id) => boss.Actions.has(id)), boss.Id + '/' + each.Id);
          assert.ok(EnemyAi.ActionOf(boss, branch[branch.length - 1], each).Column <= 2, boss.Id + '/' + each.Id + ' ' + band);
        }
      }
    }
    for (const other of Enemies.All.filter((e) => e.Rank !== EnemyRank.Boss)) {
      assert.equal(other.Switches, null, other.Id + ' fights on its base tree');
    }
  });
});
