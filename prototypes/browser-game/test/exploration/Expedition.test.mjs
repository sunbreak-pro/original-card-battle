// Port of unity-project-kit/Assets/View/Exploration/Bridge/Tests/ExpeditionTests.cs (#99, #625): the
// exploration hands a combat node's battle in and takes its result back — HP, stamina, mastery ticks,
// what the journal learned, the unique cards, the rest of the reward and death.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { load } from './_needs.mjs';

// Every module but node's is loaded through load(): while a battle module is still missing,
// the tests below are skipped as 'needs <module>' and run once it lands.
const { skip, m } = await load({
  m0: '../../src/shared/cs.js',
  m1: '../../src/core/battle/CardCatalog.js',
  m2: '../../src/core/battle/Constants.js',
  m3: '../../src/core/battle/Mastery.js',
  m4: '../../src/core/battle/Types.js',
  m5: '../../src/core/dungeonContent/ItemCatalogue.js',
  m6: '../../src/core/dungeon/BattleField.js',
  m7: '../../src/core/dungeon/ExplorationState.js',
  m8: '../../src/core/dungeon/NodeKind.js',
  m9: '../../src/bridge/explorationScript/ExplorationSession.js',
  m10: '../../src/bridge/explorationScript/ExplorationScreen.js',
  m11: '../../src/bridge/exploration/BattleRewards.js',
  m12: '../../src/bridge/exploration/Expedition.js',
  m13: '../../src/bridge/exploration/UniqueCards.js',
  m14: './_battle.mjs',
}, import.meta.url);
const { ArgumentException, InvalidOperationException } = m.m0 ?? {};
const { CardCatalog, PrototypeDeck } = m.m1 ?? {};
const { Constants } = m.m2 ?? {};
const { MasteryContext, MasteryPlay } = m.m3 ?? {};
const { GameResult } = m.m4 ?? {};
const { ItemCatalogue } = m.m5 ?? {};
const { BattleField } = m.m6 ?? {};
const { RestChoice, RunPhase } = m.m7 ?? {};
const { NodeKind } = m.m8 ?? {};
const { RunStart, ExplorationSession } = m.m9 ?? {};
const { ScreenAction } = m.m10 ?? {};
const { CardPile } = m.m11 ?? {};
const { Expedition } = m.m12 ?? {};
const { UniqueCards } = m.m13 ?? {};
const { Drive, EndEveryFight } = m.m14 ?? {};

const start = (o) => new RunStart(o);

const sameEncounter = (a, b) => a.IsStandIn === b.IsStandIn
  && a.Fights.length === b.Fights.length
  && a.Fights.every((f, i) => f.EnemyIds.join('+') === b.Fights[i].EnemyIds.join('+'));

/** Wins whatever battle waits, keeping full HP so the walk never falls. */
function WinPending(expedition) {
  const battle = expedition.StartEncounter();
  assert.notEqual(battle, null);
  EndEveryFight(battle, GameResult.Won, { hp: Constants.PlayerMaxHp, stamina: 5 });
  return expedition.Return();
}

/** Breadth-first over the map: the nodes from where the player stands to the nearest unresolved node of this kind, that node last. */
function PathTo(state, kind) {
  const map = state.Map;
  const from = new Map([[state.CurrentNodeId, -1]]);
  const queue = [state.CurrentNodeId];
  let target = -1;
  while (queue.length > 0) {
    const node = queue.shift();
    if (map.Node(node).Kind === kind && !state.HasResolved(node)) {
      target = node;
      break;
    }
    for (const next of map.Neighbours(node)) {
      if (from.has(next)) continue;
      from.set(next, node);
      queue.push(next);
    }
  }
  assert.notEqual(target, -1, `a ${kind} node is reachable`);
  const path = [];
  for (let node = target; node !== state.CurrentNodeId; node = from.get(node)) path.push(node);
  path.reverse();
  return path;
}

/** Walks the shortest way to the layer's boss and wins every battle on the way. Returns the boss's return. */
function WalkToBossAndWin(expedition) {
  const session = expedition.Session;
  if (session.HasPendingBattle) WinPending(expedition);
  let atBoss = null;
  for (const node of PathTo(session.State, NodeKind.Boss)) {
    assert.equal(session.Step(node), true, `step to ${node}`);
    if (session.HasPendingStep) assert.equal(session.Act(ScreenAction.ConfirmStep), true);
    if (!session.HasPendingBattle) continue;
    const back = WinPending(expedition);
    if (session.State.Map.Node(node).Kind === NodeKind.Boss) atBoss = back;
  }
  assert.notEqual(atBoss, null, 'the walk reached the boss');
  return atBoss;
}

function WalkToLayer(expedition, layer) {
  const session = expedition.Session;
  while (session.State.Profile.Layer < layer) {
    WalkToBossAndWin(expedition);
    assert.equal(session.Act(ScreenAction.GoToInterlude), true);
    assert.equal(session.Act(ScreenAction.Descend), true);
    assert.equal(session.ConfirmingDescend, false, 'the descent does not end the life');
  }
}

/** The first seed whose entry battle, won, lays out the card pile to pick from. */
function WinUntilACardIsOffered() {
  for (let seed = 1n; seed < 100n; seed++) {
    const expedition = new Expedition(start({ Seed: seed }));
    if (WinPending(expedition).Reward.CardChoices.length > 0) return expedition;
  }
  assert.fail('no seed in 1〜99 dealt a card at the entry battle');
}

function WalkToAndWin(expedition, kind, choice = RestChoice.Rest) {
  const session = expedition.Session;
  if (session.HasPendingBattle) WinPending(expedition);
  const path = PathTo(session.State, kind);
  let there = null;
  for (const node of path) {
    const last = node === path[path.length - 1];
    assert.equal(session.Step(node, last ? choice : RestChoice.Rest), true, `step to ${node}`);
    if (session.HasPendingStep) assert.equal(session.Act(ScreenAction.ConfirmStep), true);
    if (!session.HasPendingBattle) continue;
    const back = WinPending(expedition);
    if (last) there = back;
  }
  return there;
}

// ---- handing in ----

test('TheEntryBattle_HandsInTheExplorationsState', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n, Hp: 41, Stamina: 7 }));
  const session = expedition.Session;
  assert.equal(session.HasPendingBattle, true, 'the entry node is always a battle');

  const handIn = expedition.HandIn();
  assert.equal(handIn.Layer, 1);
  assert.equal(handIn.Kind, NodeKind.Battle);
  assert.ok(sameEncounter(handIn.Encounter, session.PendingEncounter));
  assert.equal(handIn.Hp, 41);
  assert.equal(handIn.MaxHp, Constants.PlayerMaxHp);
  assert.equal(handIn.Stamina, session.State.Stamina, 'the entry node\'s 刻限 has already given stamina back');
  assert.equal(handIn.MaxStamina, session.State.MaxStamina);
  assert.equal(handIn.Deck.length, PrototypeDeck.Kinds.length * PrototypeDeck.Copies);
  assert.deepEqual(handIn.FieldCells, [BattleField.Cells(1, NodeKind.Battle)]);
  assert.deepEqual([...new Set(handIn.Disclosure.values())], [0], 'no enemy has been met yet');
  const seed = Number(BigInt.asIntN(32, ExplorationSession.EncounterSeed(3n, 1, session.PendingBattleNode)));
  assert.equal(handIn.Seed, seed, 'unchecked((int)EncounterSeed)');
});

test('TheLaunch_CarriesHpStaminaAndTheLine_IntoTheBattle', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n, Hp: 41, Stamina: 7 }));
  const battle = expedition.StartEncounter();
  const source = battle.StartNext();

  const player = source.State.Player;
  assert.equal(player.Hp, 41);
  assert.equal(player.MaxHp, Constants.PlayerMaxHp);
  assert.equal(player.MaxStamina, expedition.Session.State.MaxStamina);
  assert.equal(player.Stamina, Math.min(expedition.Session.State.Stamina, player.MaxStamina));
  assert.equal(source.State.FieldCells, 6);
  assert.equal(source.State.EnemyDef.Id, battle.HandIn.Encounter.Fights[0].EnemyIds[0]);
});

test('Miasma_LowersTheMaxStamina_TheBattleStartsFrom', { skip }, () => {
  // concept-v3 §6: one point per full 20%. 45% is two points down from 10.
  const expedition = new Expedition(start({ Seed: 3n, MiasmaPercent: 45 }));
  const launch = expedition.StartEncounter().NextLaunch();
  assert.equal(launch.MaxStamina, 8);
  assert.equal(launch.CreateSource().State.Player.MaxStamina, 8);
});

test('TheInBattleTools_ReachTheBattle_AsAmounts', { skip }, () => {
  const tools = ['maai_no_kutsu', 'ikitsugi_no_fue', 'ryurin_no_kakera'];
  const expedition = new Expedition(start({ Seed: 3n }), null, tools);
  const launch = expedition.StartEncounter().NextLaunch();
  assert.equal(launch.PlayerStartCell, Constants.PlayerStartCell - ItemCatalogue.Tool('maai_no_kutsu').Amount);
  assert.equal(launch.StartGap, Constants.StartGap + ItemCatalogue.Tool('maai_no_kutsu').Amount);
  assert.equal(launch.StartStaminaBonus, ItemCatalogue.Tool('ikitsugi_no_fue').Amount);
  assert.equal(launch.FirstHitGuard, ItemCatalogue.Tool('ryurin_no_kakera').Amount);
  assert.equal(launch.BossWordWard, 0);
  assert.equal(launch.CreateSource().State.Player.Cell, 1);
  assert.deepEqual(expedition.Tools.Tools, tools);
  assert.deepEqual(expedition.Tools.Consumables, []);
});

test('AToolThatActsOnTheExploration_IsNotABattleTool', { skip }, () => {
  assert.throws(() => new Expedition(null, null, ['bosho_no_men']), ArgumentException);
  assert.throws(() => new Expedition(null, null, ['bosho_no_men']),
    { message: "Expedition: 防瘴の面 acts on the exploration; it goes in RunStart.Loadout (Parameter 'toolIds')" });
});

test('TheJournalsLevel_ReachesTheOmen_ThroughTheLaunchsDisclosure', { skip }, () => {
  const probe = new Expedition(start({ Seed: 3n }));
  const enemy = probe.Session.PendingEncounter.Fights[0].EnemyIds[0];

  const expedition = new Expedition(start({ Seed: 3n }), null, null, null, null, new Map([[enemy, 1]]));
  const launch = expedition.StartEncounter().NextLaunch();
  assert.equal(launch.Disclosure, 1);
  assert.equal(launch.CreateSource().Frame.Omen.Disclosure, 1);
});

// ---- bringing back ----

test('AWonBattle_BringsHpAndStaminaBack_AndTheJournalReadsOne', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  const battle = expedition.StartEncounter();
  const enemy = battle.HandIn.Encounter.Fights[0].EnemyIds[0];

  EndEveryFight(battle, GameResult.Won, { hp: 33, stamina: 4 });
  const back = expedition.Return();

  assert.equal(back.Won, true);
  assert.equal(back.Died, false);
  assert.equal(expedition.Session.State.Hp, 33);
  assert.equal(expedition.Session.State.Stamina, 4);
  assert.equal(expedition.Session.HasPendingBattle, false);
  assert.equal(expedition.Session.PendingEncounter, null);
  assert.equal(expedition.Session.State.Phase, RunPhase.Exploring);
  assert.equal(expedition.DisclosureOf(enemy), 1);
  assert.equal(back.Disclosure.get(enemy), 1);
  assert.deepEqual(back.Observed, []);
  assert.equal(expedition.Battle, null);
});

test('AnObservedEnemy_ReadsTwo_OnceTheBattleEnds', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  const battle = expedition.StartEncounter();
  const enemy = battle.HandIn.Encounter.Fights[0].EnemyIds[0];

  EndEveryFight(battle, GameResult.Won, { observe: true });
  const back = expedition.Return();

  assert.equal(expedition.DisclosureOf(enemy), 2);
  assert.deepEqual(back.Observed, [enemy]);
});

test('TheMasteryTicks_GoToTheBook_WhenTheBattleEnds', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }), null, null, 'thrust');
  const battle = expedition.StartEncounter();
  const plays = [
    new MasteryPlay('thrust', MasteryContext.Gap | MasteryContext.Omen),
    new MasteryPlay('brace', MasteryContext.Trait),
  ];

  EndEveryFight(battle, GameResult.Won, { plays });
  const back = expedition.Return();

  assert.equal(back.Ticks.TicksOf('thrust'), 2);
  assert.equal(back.Ticks.TicksOf('brace'), 1);
  assert.equal(expedition.Mastery.TicksOf('thrust'), 2 * Constants.TalentTickMultiplier, '才能 doubles the talent card');
  assert.equal(expedition.Mastery.TicksOf('brace'), 1);
});

test('ALostBattle_EndsTheLife', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  const battle = expedition.StartEncounter();

  EndEveryFight(battle, GameResult.Lost, { hp: 0 });
  const back = expedition.Return();

  assert.equal(back.Died, true);
  assert.equal(back.Won, false);
  assert.equal(expedition.Session.State.Phase, RunPhase.Fallen);
  assert.equal(expedition.Session.State.Hp, 0);
});

// Drive reads the card faces' aims through the Depiction script module.
const { skip: skipDrive } = await load({ d: '../../src/bridge/depictionScript/DepictionTypes.js' }, import.meta.url);

test('ARealBattle_FoughtToTheEnd_ComesBack', { skip: skip || skipDrive }, async () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  const battle = expedition.StartEncounter();
  while (!battle.Over) {
    const source = battle.StartNext(true);
    await Drive(source);
    battle.Finish(source);
  }
  const back = expedition.Return();

  assert.equal(back.Hp, expedition.Session.State.Hp);
  assert.equal(back.Died, expedition.Session.State.Phase === RunPhase.Fallen);
  assert.ok([...back.Ticks.Ticks.values()].reduce((s, v) => s + v, 0) > 0, 'playing cards gives ticks');
});

test('ANewLife_StartsFromWhatTheExpeditionSetOutWith', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  const battle = expedition.StartEncounter();
  const enemy = battle.HandIn.Encounter.Fights[0].EnemyIds[0];
  EndEveryFight(battle, GameResult.Lost, { hp: 0, plays: [new MasteryPlay('thrust', MasteryContext.Gap)] });
  expedition.Return();

  assert.equal(expedition.Session.Act(ScreenAction.NewLife), true);
  assert.equal(expedition.DisclosureOf(enemy), 0);
  assert.equal(expedition.Mastery.TicksOf('thrust'), 0);
  assert.notEqual(expedition.HandIn(), null, 'the new life\'s entry battle waits');
});

test('TheHandOver_IsRefusedWhenItDoesNotApply', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  assert.throws(() => expedition.Return(), InvalidOperationException, 'no battle is being fought');
  const battle = expedition.StartEncounter();
  assert.throws(() => expedition.StartEncounter(), InvalidOperationException, 'a battle is already being fought');
  assert.throws(() => expedition.Return(), InvalidOperationException, 'the encounter has fights left');
  expedition.Abandon();
  assert.equal(expedition.Battle, null);
  assert.equal(expedition.Session.HasPendingBattle, true, 'the node still waits on a result');
  assert.notEqual(battle, null);
  EndEveryFight(expedition.StartEncounter(), GameResult.Won);
  expedition.Return();
  assert.equal(expedition.HandIn(), null, 'no battle waits');
  assert.equal(expedition.StartEncounter(), null);
});

// ---- unique cards ----

test('ARealBoss_TeachesItsThreeCards_IntoTheDeck', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 5n }));
  WalkToLayer(expedition, 3);
  const back = WalkToBossAndWin(expedition);

  const cards = UniqueCards.OfBoss('miasma_priest');
  assert.deepEqual(back.Learned, cards);
  assert.ok(cards.every((id) => expedition.Owned.has(id)));
  assert.ok(cards.every((id) => expedition.Deck.some((c) => c.Def.Id === id)), 'a deck of 20 has room');
  assert.equal(expedition.Session.State.Phase, RunPhase.LayerCleared);
  assert.ok(expedition.Deck.some((c) => c.InstanceId === `${cards[0]}-learned-1`));
});

test('ANormalBattle_TeachesNoUniqueCard', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  EndEveryFight(expedition.StartEncounter(), GameResult.Won);
  const back = expedition.Return();
  assert.deepEqual(back.Learned, []);
  assert.deepEqual(back.Offered, []);
});

// ---- the rest of the reward and the picks (#625) ----

test('AWonNormalBattle_BringsOneOfCardConsumableOrPage_Back', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  const back = WinPending(expedition);
  const reward = back.Reward;
  const parts = (reward.CardChoices.length > 0 ? 1 : 0) + reward.Consumables.length + reward.Pages;
  assert.equal(parts, 1, '§4.2: one of the three');
  assert.deepEqual(expedition.Offer, reward.CardChoices);
  assert.deepEqual(expedition.Consumables, reward.Consumables);
  assert.equal(expedition.Pages, reward.Pages);
});

test('TheReward_IsDrawnFromTheExplorationsSeed', { skip }, () => {
  const a = WinPending(new Expedition(start({ Seed: 8n })));
  const b = WinPending(new Expedition(start({ Seed: 8n })));
  assert.deepEqual(b.Reward.CardChoices, a.Reward.CardChoices);
  assert.deepEqual(b.Reward.Consumables, a.Reward.Consumables);
  assert.equal(b.Reward.Pages, a.Reward.Pages);
});

test('TheRewardSeed_IsTheEncounterSeedSalted', { skip }, () => {
  assert.equal(Expedition.RewardSeed(3n, 1, 0), ExplorationSession.EncounterSeed(3n, 1, 0) ^ 0xD1B54A32D192ED03n);
});

test('ALostBattle_BringsNoReward', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  EndEveryFight(expedition.StartEncounter(), GameResult.Lost, { hp: 0 });
  const back = expedition.Return();
  assert.equal(back.Reward.IsEmpty, true);
  assert.deepEqual(expedition.Offer, []);
});

test('APileCard_PickedWithLearn_IsOwnedAndInTheDeck', { skip }, () => {
  const expedition = WinUntilACardIsOffered();
  const offer = expedition.Offer;
  const card = offer[0];
  assert.ok(CardPile.Of(1).includes(card), 'layer 1 deals its own pile');
  assert.equal(expedition.Learn(card), true);
  assert.ok(expedition.Owned.has(card));
  assert.ok(expedition.Deck.some((c) => c.Def.Id === card));
  assert.deepEqual(expedition.Offer, [], 'one card of the three');
  assert.deepEqual(offer, [], 'List.Clear empties the list Offer handed out');
  assert.equal(expedition.Learn(null), false);
});

test('ARealElitesCard_PickedWithLearn_IsOwnedAndInTheDeck', { skip }, () => {
  // Layer 7's elite is always 統牙の長竜, and no boss before it teaches its cards.
  const expedition = new Expedition(start({ Seed: 5n }));
  WalkToLayer(expedition, 7);
  const back = WalkToAndWin(expedition, NodeKind.Elite);

  assert.deepEqual(back.Offered, UniqueCards.OfElite('pack_alpha'), 'a real elite offers its two cards');
  assert.deepEqual(expedition.Offer, back.Offered);
  assert.deepEqual(back.Reward.CardChoices, [], 'the elite\'s cards are the card');
  assert.equal(back.Reward.Consumables.length, 1, '§4.2: an elite always drops a consumable');

  const card = back.Offered[1];
  assert.equal(expedition.Learn(card), true);
  assert.ok(expedition.Owned.has(card));
  assert.ok(expedition.Deck.some((c) => c.Def.Id === card));
  assert.equal(expedition.Owned.has(back.Offered[0]), false, 'one of the two');
  assert.equal(expedition.Learn(back.Offered[0]), false, 'the offer is spent');
});

test('AnEliteWhoseCardsAreOwned_DealsThreeCardsFromThePileInstead', { skip }, () => {
  // Seed 5 draws 鉄壁の門竜 for layer 5's elite. Layer 4's stand-in boss is the same 門竜, and
  // beating it taught both of its cards (§4.2), so the elite has nothing left to offer.
  const expedition = new Expedition(start({ Seed: 5n }));
  WalkToLayer(expedition, 5);
  assert.ok(UniqueCards.OfElite('armored_warden').every((id) => expedition.Owned.has(id)));
  const back = WalkToAndWin(expedition, NodeKind.Elite);

  assert.deepEqual(back.Offered, []);
  assert.equal(back.Reward.CardChoices.length, CardPile.Choices);
  const piles = CardPile.Of(5).concat(CardPile.Of(4));
  assert.ok(back.Reward.CardChoices.every((id) => piles.includes(id)), 'layer 5\'s pile first, then layer 4\'s');
  assert.deepEqual(expedition.Offer, back.Reward.CardChoices);
});

test('ATrainingMark_SpentOnACard_GoesThroughTrainIntoTheMasteryBook', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  const session = expedition.Session;
  WinPending(expedition);
  WalkToAndWin(expedition, NodeKind.Rest, RestChoice.Train);
  assert.equal(session.State.TrainingMarks, 1);

  const card = CardCatalog.Thrust.Id;
  const before = expedition.Mastery.TicksOf(card);
  assert.equal(expedition.Train(card), true);
  assert.equal(expedition.Mastery.TicksOf(card), before + Constants.TrainingTicks);
  assert.equal(session.State.TrainingMarks, 0, 'the mark is spent');
  assert.equal(expedition.Train(card), false, 'no mark left');
});

test('ATrainingMark_IsNotSpentOnACardNotOwned', { skip }, () => {
  const expedition = new Expedition(start({ Seed: 3n }));
  WinPending(expedition);
  WalkToAndWin(expedition, NodeKind.Rest, RestChoice.Train);
  assert.equal(expedition.Train(CardCatalog.LastStand.Id), false, '#80 is not owned at the start');
  assert.equal(expedition.Session.State.TrainingMarks, 1);
});

test('TheCharacterOwnsTheFirstFortyKinds_AndTheDeckIsValidated', { skip }, () => {
  assert.deepEqual(Expedition.InitialKinds, CardCatalog.All.slice(0, 40).map((d) => d.Id));
  const expedition = new Expedition();
  assert.ok(Expedition.InitialKinds.every((id) => expedition.Owned.has(id)));
  assert.equal(expedition.Deck.length, PrototypeDeck.Kinds.length * PrototypeDeck.Copies);
  assert.equal(expedition.Pages, 0);
  assert.deepEqual(expedition.Consumables, []);
  assert.throws(() => new Expedition(null, PrototypeDeck.Build().slice(0, 3)), ArgumentException, 'a deck under §8\'s minimum is refused');
});
