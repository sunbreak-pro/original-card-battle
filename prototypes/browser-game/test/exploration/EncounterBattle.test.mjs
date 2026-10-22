// Port of the "node of two fights, and two enemies on one line" cases in
// unity-project-kit/Assets/View/Exploration/Bridge/Tests/ExpeditionTests.cs, plus the hand-in's and the
// return's own contracts.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { load } from './_needs.mjs';

// Every module but node's is loaded through load(): while a battle module is still missing,
// the tests below are skipped as 'needs <module>' and run once it lands.
const { skip, m } = await load({
  m0: '../../src/shared/cs.js',
  m1: '../../src/core/battle/CardCatalog.js',
  m2: '../../src/core/battle/BattleEvents.js',
  m3: '../../src/core/battle/DeckRules.js',
  m4: '../../src/core/battle/Mastery.js',
  m5: '../../src/core/battle/Types.js',
  m6: '../../src/core/dungeon/LayerEncounters.js',
  m7: '../../src/core/dungeon/NodeKind.js',
  m8: '../../src/bridge/exploration/BattleRewards.js',
  m9: '../../src/bridge/exploration/EncounterBattle.js',
  m10: './_battle.mjs',
}, import.meta.url);
const { ArgumentException, ArgumentNullException, InvalidOperationException } = m.m0 ?? {};
const { CardCatalog, PrototypeDeck } = m.m1 ?? {};
const { CardPlayed } = m.m2 ?? {};
const { Departure } = m.m3 ?? {};
const { MasteryTally } = m.m4 ?? {};
const { Actor, CardInstance, GameResult } = m.m5 ?? {};
const { Encounter, Fight } = m.m6 ?? {};
const { NodeKind } = m.m7 ?? {};
const { BattleReward } = m.m8 ?? {};
const { BattleHandIn, BattleReturn, EncounterBattle, FoughtBattle } = m.m9 ?? {};
const { Ended, HandIn } = m.m10 ?? {};

const twoFights = () => new Encounter([new Fight(['polearm_warped']), new Fight(['shadow_hound'])], true);

test('TwoFightsBackToBack_CarryHpAndStamina', { skip }, () => {
  const battle = new EncounterBattle(HandIn(twoFights(), NodeKind.Elite));

  const first = battle.StartNext();
  battle.Finish(Ended(first, GameResult.Won, 27, 3), first.History);
  assert.equal(battle.Over, false);

  const second = battle.NextLaunch();
  assert.equal(second.EnemyId, 'shadow_hound');
  assert.equal(second.StartHp, 27);
  assert.equal(second.StartStamina, 3);
  assert.equal(second.ChainIndex, 2);
  assert.equal(second.ChainTotal, 2);
  assert.notEqual(second.Seed, battle.HandIn.Seed, 'each fight is dealt afresh');
  assert.equal(second.Seed, 11 + 7919, 'the next fight steps its seed by the prime 7919');
});

test('ALostFirstFight_EndsTheEncounter', { skip }, () => {
  const battle = new EncounterBattle(HandIn(twoFights(), NodeKind.Elite));
  const first = battle.StartNext();
  battle.Finish(Ended(first, GameResult.Lost, 0, 0), first.History);

  assert.equal(battle.Over, true);
  assert.equal(battle.Lost, true);
  assert.equal(battle.Won, false);
  assert.equal(battle.HpAfter, 0);
  assert.throws(() => battle.NextLaunch(), InvalidOperationException);
});

test('ThePair_StandsOnOneLine_AndTheOmenReadsTheLowerLevel', { skip }, () => {
  const encounter = new Encounter([new Fight(['polearm_warped', 'crossbow_hunter'])], true);
  const levels = new Map([['polearm_warped', 2], ['crossbow_hunter', 1]]);
  const battle = new EncounterBattle(HandIn(encounter, NodeKind.Elite, levels, 7));

  const launch = battle.NextLaunch();
  assert.deepEqual(launch.MoreEnemyIds, ['crossbow_hunter']);
  assert.equal(launch.Disclosure, 1);

  const state = launch.CreateSource().State;
  assert.deepEqual(state.Enemies.map((e) => e.Def.Id), ['polearm_warped', 'crossbow_hunter']);
  assert.equal(state.FieldCells, 7);
});

test('TheLaunch_IsWhatTheHandInSays', { skip }, () => {
  const encounter = new Encounter([new Fight(['polearm_warped'])], false);
  const battle = new EncounterBattle(HandIn(encounter, NodeKind.Battle, new Map(), 6));
  const launch = battle.NextLaunch();
  assert.equal(launch.EnemyId, 'polearm_warped');
  assert.deepEqual(launch.MoreEnemyIds, []);
  assert.equal(launch.Seed, 11);
  assert.equal(launch.FieldCells, 6);
  assert.equal(launch.Disclosure, 0, 'an enemy the journal does not list reads 0');
  assert.equal(launch.Deck.length, battle.HandIn.Deck.length);
  assert.notEqual(launch.Deck, battle.HandIn.Deck, 'a copy of the deck');
  assert.equal(launch.StartHp, battle.HandIn.Hp);
  assert.equal(launch.StartStamina, battle.HandIn.Stamina);
  assert.equal(launch.ChainIndex, 1);
  assert.equal(launch.ChainTotal, 1);
  assert.equal(launch.AutoPlay, false);
  assert.equal(battle.Count, 1);
  assert.equal(battle.Index, 0);
  assert.equal(battle.HpAfter, battle.HandIn.Hp, 'before any fight: the HP handed in');
  assert.equal(battle.StaminaAfter, battle.HandIn.Stamina);
});

test('AFight_IsStartedAndFinishedOnce', { skip }, () => {
  const battle = new EncounterBattle(HandIn(twoFights(), NodeKind.Elite));
  assert.throws(() => battle.Finish(Ended(battle.StartNext(), GameResult.Won, 1, 1), null), ArgumentNullException);
  assert.throws(() => battle.StartNext(), InvalidOperationException, 'a fight is already being fought');
  const fresh = new EncounterBattle(HandIn(twoFights(), NodeKind.Elite));
  const source = fresh.StartNext();
  assert.throws(() => fresh.Finish(source.State, source.History), InvalidOperationException, 'the fight has not ended');
  assert.throws(() => fresh.Finish(null), ArgumentNullException);
  fresh.Finish(Ended(source, GameResult.Won, 30, 5), source.History);
  assert.throws(() => fresh.Finish(Ended(source, GameResult.Won, 30, 5), source.History), InvalidOperationException, 'no fight is being fought');
  assert.equal(fresh.Fought.length, 1);
  assert.equal(fresh.Fought[0].Fight.EnemyIds[0], 'polearm_warped');
});

test('FinishWithTheSource_ReadsItsStateAndHistory', { skip }, () => {
  const battle = new EncounterBattle(HandIn(new Encounter([new Fight(['polearm_warped'])], false), NodeKind.Battle));
  const source = battle.StartNext();
  const ended = Ended(source, GameResult.Won, 40, 6);
  battle.Finish({ State: ended, History: source.History });
  assert.equal(battle.Won, true);
  assert.equal(battle.HpAfter, 40);
  assert.equal(battle.StaminaAfter, 6);
});

test('TheHandIn_RefusesNullsAndALineCountThatIsNotTheFights', { skip }, () => {
  const deck = PrototypeDeck.Build();
  const tools = Departure.Plain(deck);
  const enc = twoFights();
  const make = (o) => new BattleHandIn(1, NodeKind.Elite, o.enc === undefined ? enc : o.enc, o.cells === undefined ? [6, 6] : o.cells,
    50, 50, 10, 10, 0, o.disc === undefined ? new Map() : o.disc, o.deck === undefined ? deck : o.deck, o.tools === undefined ? tools : o.tools, 1);
  assert.throws(() => make({ enc: null }), ArgumentNullException);
  assert.throws(() => make({ cells: null }), ArgumentNullException);
  assert.throws(() => make({ disc: null }), ArgumentNullException);
  assert.throws(() => make({ deck: null }), ArgumentNullException);
  assert.throws(() => make({ tools: null }), ArgumentNullException);
  assert.throws(() => make({ cells: [6] }), ArgumentException);
  assert.equal(make({}).Layer, 1);
});

test('TheReturn_FillsWhatWasLeftOut', { skip }, () => {
  assert.throws(() => new BattleReturn(true, 10, 3, null, null, null, null, null, null), ArgumentNullException);
  const back = new BattleReturn(true, 10, 3, MasteryTally.Of([]), null, null, null, null, null);
  assert.deepEqual(back.RankUps, []);
  assert.equal(back.Disclosure.size, 0);
  assert.deepEqual(back.Observed, []);
  assert.deepEqual(back.Learned, []);
  assert.deepEqual(back.Offered, []);
  assert.equal(back.Reward, BattleReward.None);
  assert.equal(back.Died, false);
  assert.equal(new BattleReturn(false, 0, 3, MasteryTally.Of([]), null, null, null, null, null).Died, true);
});

test('AnObservation_IsAPlayersObserveCard', { skip }, () => {
  const observe = new CardPlayed(Actor.Player, new CardInstance('observe-0', CardCatalog.Observe), 3);
  const theirs = new CardPlayed(Actor.Enemy, new CardInstance('observe-1', CardCatalog.Observe), 3);
  const other = new CardPlayed(Actor.Player, new CardInstance('thrust-0', CardCatalog.Thrust), 3);
  const fight = new Fight(['polearm_warped']);
  assert.equal(new FoughtBattle(fight, null, [other, observe]).Observed, true);
  assert.equal(new FoughtBattle(fight, null, [other, theirs]).Observed, false);
  assert.equal(new FoughtBattle(fight, null, []).Observed, false);
});
