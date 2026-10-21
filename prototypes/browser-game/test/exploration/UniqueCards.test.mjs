// Port of the unique-card cases in unity-project-kit/Assets/View/Exploration/Bridge/Tests/ExpeditionTests.cs
// (TheRewardTable_FollowsTheNodeAndWhetherTheEnemyIsAStandIn, ACardAlreadyOwned_IsNotTaughtAgain,
// EveryCardTheTableNames_IsALearnedCard).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { load } from './_needs.mjs';

// Every module but node's is loaded through load(): while a battle module is still missing,
// the tests below are skipped as 'needs <module>' and run once it lands.
const { skip, m } = await load({
  m0: '../../src/shared/cs.js',
  m1: '../../src/core/battle/CardCatalog.js',
  m2: '../../src/core/dungeon/LayerEncounters.js',
  m3: '../../src/core/dungeon/NodeKind.js',
  m4: '../../src/bridge/exploration/UniqueCards.js',
}, import.meta.url);
const { ArgumentNullException } = m.m0 ?? {};
const { CardCatalog } = m.m1 ?? {};
const { Encounter, Fight } = m.m2 ?? {};
const { NodeKind } = m.m3 ?? {};
const { UniqueCardReward, UniqueCards } = m.m4 ?? {};

const Single = (id, standIn) => new Encounter([new Fight([id])], standIn);

test('TheRewardTable_FollowsTheNodeAndWhetherTheEnemyIsAStandIn', { skip }, () => {
  const none = new Set();
  assert.deepEqual(UniqueCards.For(NodeKind.Boss, Single('abyss_angler', false), none).Learned, UniqueCards.OfBoss('abyss_angler'));
  assert.deepEqual(UniqueCards.For(NodeKind.Boss, Single('armored_warden', true), none).Learned, UniqueCards.OfElite('armored_warden'),
    'layer 4\'s stand-in boss teaches both of its cards');
  assert.equal(UniqueCards.For(NodeKind.Boss, Single('shadow_hound', true), none).IsEmpty, true, 'layer 1\'s stand-in boss draws from the pile');
  assert.deepEqual(UniqueCards.For(NodeKind.Elite, Single('pack_alpha', false), none).Offered, UniqueCards.OfElite('pack_alpha'));
  assert.deepEqual(UniqueCards.For(NodeKind.Elite, Single('pack_alpha', false), none).Learned, []);
  assert.equal(UniqueCards.For(NodeKind.Battle, Single('polearm_warped', false), none).IsEmpty, true);
  assert.equal(UniqueCards.For(NodeKind.Elite, Single('pack_alpha', true), none).IsEmpty, true, 'a stand-in elite teaches nothing');
  const pair = new Encounter([new Fight(['polearm_warped', 'crossbow_hunter'])], true);
  assert.equal(UniqueCards.For(NodeKind.Boss, pair, none), UniqueCardReward.None, 'pairs and chains are all stand-ins');
});

test('ACardAlreadyOwned_IsNotTaughtAgain', { skip }, () => {
  const owned = new Set([CardCatalog.FangRush.Id]);
  const reward = UniqueCards.For(NodeKind.Elite, Single('pack_alpha', false), owned);
  assert.deepEqual(reward.Offered, [CardCatalog.PackHowl.Id]);
  // An owned list (IReadOnlyCollection as an array) reads the same.
  assert.deepEqual(UniqueCards.For(NodeKind.Elite, Single('pack_alpha', false), [CardCatalog.FangRush.Id]).Offered, [CardCatalog.PackHowl.Id]);
});

test('EveryCardTheTableNames_IsALearnedCard', { skip }, () => {
  const learned = CardCatalog.All.slice(40).map((d) => d.Id);
  const named = ['miasma_priest', 'abyss_angler', 'distortion_root'].flatMap((id) => UniqueCards.OfBoss(id))
    .concat(['armored_warden', 'pack_alpha', 'polearm_unyielding'].flatMap((id) => UniqueCards.OfElite(id)));
  assert.equal(named.length, 15);
  assert.ok(named.every((id) => learned.includes(id)));
});

test('TheOwnersAreTheCardTables', { skip }, () => {
  assert.deepEqual(UniqueCards.OfBoss('miasma_priest'), [CardCatalog.MiasmaBlade.Id, CardCatalog.PriestPrayer.Id, CardCatalog.PurgeFlash.Id]);
  assert.deepEqual(UniqueCards.OfBoss('distortion_root'), [CardCatalog.RootStride.Id, CardCatalog.ThornShot.Id, CardCatalog.RootBind.Id]);
  assert.deepEqual(UniqueCards.OfElite('polearm_unyielding'), [CardCatalog.RiposteStance.Id, CardCatalog.SpearWall.Id]);
  assert.deepEqual(UniqueCards.OfBoss('shadow_hound'), []);
  assert.deepEqual(UniqueCards.OfElite(null), []);
  assert.deepEqual(UniqueCards.OfBoss(null), []);
});

test('NullsAreRefused_AndTheRewardTurnsNullsIntoEmpties', { skip }, () => {
  assert.throws(() => UniqueCards.For(NodeKind.Boss, null, new Set()), ArgumentNullException);
  assert.throws(() => UniqueCards.For(NodeKind.Boss, Single('abyss_angler', false), null), ArgumentNullException);
  const r = new UniqueCardReward(null, null);
  assert.deepEqual(r.Learned, []);
  assert.deepEqual(r.Offered, []);
  assert.equal(r.IsEmpty, true);
  assert.equal(UniqueCardReward.None.IsEmpty, true);
});
