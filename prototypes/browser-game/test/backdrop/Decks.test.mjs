// Port of the DeckTests of unity-port/BattleCore.Sim.Tests/SimTests.cs (#53: the bench fights with legal
// decks), plus the oracle's `table:Decks` case (oracle/out/catalog.json) diffed value for value.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { loadModules, needExports, MainDecks, oracleCases, oracleShapes } from './_sim.mjs';

const { mods, missing: loadMissing } = await loadModules([
  'sim/Decks.js',
  'core/battle/CardCatalog.js',
  'core/battle/Cards.js',
  'core/battle/Constants.js',
  'core/battle/Attributes.js',
  'core/battle/Types.js',
]);
const missing = loadMissing
  || needExports(mods, 'core/battle/CardCatalog.js', ['CardCatalog', 'PrototypeDeck'])
  || needExports(mods, 'core/battle/Cards.js', ['Cards']);
const skip = missing || false;

const { Decks, SimDeck } = mods['sim/Decks.js'] ?? {};
const { CardCatalog, PrototypeDeck } = mods['core/battle/CardCatalog.js'] ?? {};
const { Cards } = mods['core/battle/Cards.js'] ?? {};
const { Constants } = mods['core/battle/Constants.js'] ?? {};
const { AttributeRule } = mods['core/battle/Attributes.js'] ?? {};
const { BattleAttribute } = mods['core/battle/Types.js'] ?? {};

test('EveryDeck_PassesTheDeckRules', { skip }, () => {
  for (const deck of MainDecks(Decks)) {
    const built = deck.Build();
    const check = Cards.Validate(built);
    assert.ok(check.Ok, `${deck.Id}: ${check.Errors.join('; ')}`);
    assert.ok(built.length >= Constants.DeckMin && built.length <= Constants.DeckMax, deck.Id);
    assert.ok(Math.max(...deck.Rows.map((r) => r.Copies)) <= Constants.CopiesMax, deck.Id);
    const ids = deck.Rows.map((r) => r.CardId);
    assert.equal(new Set(ids).size, ids.length, `${deck.Id}: unique rows`);
  }
});

test('TheDeckTypes_AreTheSixOfSection19_8_TwentyCardsEach', { skip }, () => {
  assert.deepEqual(Decks.Archetypes.map((d) => d.Name), ['溜め放ち', '振り子', '研ぎ', '血路', '一型', '背水']);
  for (const d of Decks.Archetypes) assert.equal(d.Count, Constants.DeckMin, d.Id);
});

test('TheCostOneDeck_HoldsOnlyCostOneCards_EachOnceAtLeast', { skip }, () => {
  const deck = Decks.CostOneOnly();
  for (const c of deck.Build()) assert.equal(c.Def.Cost, 1, c.InstanceId);
  const want = CardCatalog.All.filter((c) => c.Cost === 1).map((c) => c.Id);
  assert.deepEqual([...deck.Rows.map((r) => r.CardId)].sort(), [...want].sort());
});

test('ANoStanceTwin_HoldsNoStanceCard_AndKeepsTheCount', { skip }, () => {
  for (const deck of Decks.Archetypes) {
    const twin = Decks.WithoutStances(deck);
    if (!deck.HasStance) {
      assert.equal(twin, null, deck.Id);
      continue;
    }
    assert.equal(twin.HasStance, false, deck.Id);
    assert.equal(twin.Count, deck.Count, deck.Id);
  }
  assert.ok(Decks.NoStanceTwins().length > 0);
});

test('TheInitialDeck_IsThePrototypeDeck', { skip }, () => {
  assert.deepEqual(Decks.Initial().Build().map((c) => c.InstanceId), PrototypeDeck.Build().map((c) => c.InstanceId));
});

test('TheInitialForty_IsFortyKindsOnceEach_TenOfThemMoving', { skip }, () => {
  const deck = Decks.InitialForty();
  const built = deck.Build();
  assert.equal(new Set(Decks.InitialFortyIds).size, Decks.InitialFortyIds.length);
  assert.equal(built.length, 40);
  for (const r of deck.Rows) assert.equal(r.Copies, 1);
  // battle_core_v4 §14-4: 「移動の効果は初期 40 種中 10 種」.
  assert.equal(built.filter((c) => AttributeRule.HasMovement(c.Def.Face)).length, 10);
});

test('OneStance_HoldsEnoughGuardCards_ForIronWallsDiscount', { skip }, () => {
  const deck = Decks.Archetypes.find((d) => d.Id === 'one_stance');
  assert.ok(deck.Rows.map((r) => r.CardId).includes('iron_wall'));
  const guards = deck.Build().filter((c) => c.Def.Attribute === BattleAttribute.Guard).length;
  assert.ok(guards >= 6);
});

// ---- port-specific ----

test('Build_NamesTheCopiesInRowOrder', { skip }, () => {
  const deck = Decks.Archetypes[0];
  const ids = deck.Build().map((c) => c.InstanceId);
  assert.deepEqual(ids.slice(0, 7), ['brace-0', 'brace-1', 'brace-2', 'wrist_cut-0', 'wrist_cut-1', 'wrist_cut-2', 'crescent_cut-0']);
  assert.equal(ids[ids.length - 1], 'break_off-0');
  assert.equal(deck.Build()[0].Def, CardCatalog.ById('brace'));
});

test('WithoutStances_ThrowsWhenTheSubstitutesAreFull', { skip }, () => {
  const stance = Decks.Archetypes.find((d) => d.HasStance).Rows.find((r) => Cards.IsStanceCard(CardCatalog.ById(r.CardId))).CardId;
  const full = new SimDeck('x', 'x', [
    { CardId: 'thrust', Copies: 3 }, { CardId: 'iron_block', Copies: 3 }, { CardId: 'side_sweep', Copies: 3 },
    { CardId: 'deep_breath', Copies: 3 }, { CardId: stance, Copies: 1 },
  ], 'b');
  assert.throws(() => Decks.WithoutStances(full), { name: 'InvalidOperationException', message: 'No room to swap the stance cards out of "x".' });
});

test('TheTwins_AreNamedAfterTheirType', { skip }, () => {
  for (const twin of Decks.NoStanceTwins()) {
    assert.ok(twin.Id.endsWith('_no_stance'));
    assert.ok(twin.Name.endsWith('（スタンス無し）'));
    assert.equal(twin.Basis, 'スタンスの札を素直な札へ差し替え');
  }
});

// ---- the C# oracle ----

const oracle = skip ? null : oracleCases('catalog', (id) => id === 'table:Decks');
const shapes = oracle ? oracleShapes() : null;
test('Oracle_TableDecks_MatchesTheCSharp', { skip: skip || (oracle && oracle.length && shapes ? false : 'oracle/out/catalog.json not written') }, async () => {
  const { toOracleJson } = await import('../parity/lib/serialize.mjs');
  const { expandRefs, firstDiff } = await import('../parity/lib/compare.mjs');
  const c = oracle[0];
  const all = [...Decks.Archetypes, Decks.CostOneOnly(), Decks.Initial(), Decks.InitialForty(), Decks.Plain(), ...Decks.NoStanceTwins()];
  const js = toOracleJson(all.map((d) => ({ Deck: d, Build: d.Build().map((x) => x.InstanceId), Validate: Cards.Validate(d.Build()) })), shapes);
  const diff = firstDiff(expandRefs(c.output, c.defs), js, { shapes, floats: new Set(c.floats || []) });
  assert.equal(diff, null, JSON.stringify(diff));
});
