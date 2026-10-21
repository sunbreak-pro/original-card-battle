// Tests for src/bridge/depiction/DeckBuilder.js (port of View/Depiction/Bridge/DeckBuilder.cs).
// Golden: the random preset for 156 seeds × sizes (the SeededRng draw order), the saved strings and
// the notices they give, the filtered pages, the card lines and details, the titles — byte for byte
// what the C# printed (scratch-csharp/texts → texts.golden.json). Ported cases: DeckBuilderTests.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { golden, load, loadOptional, needs, attempt } from './_texts.mjs';

const { m, missing } = await load();
const opt = needs(missing);
const launchLoad = missing ? { mod: null, skip: opt } : await loadOptional('../../src/bridge/depiction/BattleLaunch.js');
const launchModule = launchLoad.mod;
const launchOpt = launchLoad.skip;

const g = golden.Deck;

// ---- golden ----

test('golden: the title and the filter choices', opt, () => {
  const { DeckBuilder } = m;
  assert.equal(DeckBuilder.Title, g.Title);
  assert.deepStrictEqual(DeckBuilder.AttributeOptions, g.AttributeOptions);
  assert.deepStrictEqual(DeckBuilder.CostOptions, g.CostOptions);
});

test('golden: the line and the detail of every card', opt, () => {
  const { DeckBuilder, CardCatalog } = m;
  for (const row of golden.Cards) {
    const def = CardCatalog.ById(row.Id);
    assert.equal(DeckBuilder.LineOf(def), row.DeckLine, def.Id);
    assert.deepStrictEqual(DeckBuilder.DetailOf(def), row.DeckDetail, def.Id);
    assert.equal(DeckBuilder.AttributeWords(def.Attributes), row.AttributeWords, def.Id);
    assert.equal(DeckBuilder.AttributeWords(def.Attribute), row.AttributeWordsFolded, def.Id);
  }
  for (const row of golden.Misc.Attributes) assert.equal(DeckBuilder.AttributeWords(row.Attr), row.Words, String(row.Attr));
});

test('golden: the random preset, seed by seed and size by size', opt, () => {
  const { DeckBuilder } = m;
  for (const row of g.Randoms) {
    const deck = DeckBuilder.Random(row.Seed, row.Size);
    const at = `seed ${row.Seed} size ${row.Size}`;
    assert.equal(deck.Save(), row.Save, at);
    assert.equal(deck.StanceCount, row.StanceCount, at);
    assert.equal(deck.StatusText, row.Status, at);
  }
  assert.ok(g.Randoms.length >= 150);
});

test('golden: the saved strings — what they load to and the line they print', opt, () => {
  const { DeckBuilder } = m;
  for (const row of g.Loads) {
    const r = DeckBuilder.LoadOrPrototype(true, row.Saved, true);
    const at = JSON.stringify(row.Saved);
    assert.equal(r.ok.Total, row.Total, at);
    assert.equal(r.ok.Save(), row.Save, at);
    assert.equal(r.value, row.Notice, at);
    assert.equal(DeckBuilder.Load(row.Saved).Save(), row.Load, at);
  }
  assert.equal(DeckBuilder.LoadOrPrototype(false, 'garbage').Save(), g.FirstPlay);
});

test('golden: the pages of every filter', opt, () => {
  const { DeckBuilder, DeckFilter } = m;
  for (const row of g.Pages) {
    const page = new DeckBuilder().Page(new DeckFilter({ Attribute: row.Attr, Cost: row.Cost }), row.Page, row.PerPage);
    assert.deepStrictEqual({ Text: page.Text, Index: page.Index, Count: page.Count, Kept: page.Kept, Cards: page.Cards.map((c) => c.Id) },
      { Text: row.Text, Index: row.Index, Count: row.Count, Kept: row.Kept, Cards: row.Cards }, `${row.Attr} ${row.Cost} ${row.Page}`);
  }
});

test('golden: the prototype, a built deck, the counts, forward and closers', opt, () => {
  const { DeckBuilder, CardCatalog } = m;
  const proto = DeckBuilder.Prototype();
  assert.equal(proto.Save(), g.Prototype);
  assert.deepStrictEqual(proto.DeckLines(), g.PrototypeLines);
  const built = new DeckBuilder();
  built.Add('kesa_cut');
  built.Add('thrust');
  built.Add('kesa_cut');
  assert.deepStrictEqual(built.Build().map((c) => c.InstanceId), g.Built);
  assert.deepStrictEqual(built.DeckLines(), g.BuiltLines);
  assert.deepStrictEqual([built.CountText('kesa_cut'), built.CountText('thrust'), built.CountText('brace')], g.BuiltCount);
  assert.deepStrictEqual(DeckBuilder.Forward.map((c) => c.Id), g.Forward);
  assert.deepStrictEqual(DeckBuilder.Closers.map((c) => c.Id), g.Closers);
  for (const row of g.Counts) {
    const deck = new DeckBuilder();
    let i = 0;
    while (deck.Total < row.N) { deck.Add(CardCatalog.All[i % CardCatalog.All.length].Id); i++; }
    assert.deepStrictEqual({ Status: deck.StatusText, Valid: deck.IsValid, Errors: deck.Validate().Errors, Lines: deck.DeckLines() },
      { Status: row.Status, Valid: row.Valid, Errors: row.Errors, Lines: row.Lines }, String(row.N));
  }
  assert.equal(attempt(() => new DeckBuilder().Page(m.DeckFilter.All, 0, 0).Text), golden.Misc.Errors[4]);
});

// ---- DeckBuilderTests ----

function filled(count) {
  // Two copies of each kind in canon order, so any count up to 135 is reachable (the stance cards stop at three).
  const { DeckBuilder, CardCatalog } = m;
  const deck = new DeckBuilder();
  let i = 0;
  while (deck.Total < count) {
    deck.Add(CardCatalog.All[i % CardCatalog.All.length].Id);
    i++;
  }
  return deck;
}

test('NineteenCards_CannotFight_TwentyAndFortyCan', opt, () => {
  assert.equal(filled(19).IsValid, false);
  const errors = filled(19).Validate().Errors;
  assert.equal(errors.length, 1);
  assert.ok(errors[0].includes('minimum is 20'));
  assert.equal(filled(19).StatusText, '19 枚です。あと 1 枚入れると戦えます');
  assert.equal(filled(20).IsValid, true);
  assert.equal(filled(40).IsValid, true);
  assert.equal(filled(40).StatusText, '40 枚です。20〜40 枚、1 種 3 枚を満たしています');
});

test('TheFortyFirstCard_IsRefused', opt, () => {
  const { CardCatalog, Cards } = m;
  const deck = filled(40);
  const next = CardCatalog.All.find((c) => deck.CountOf(c.Id) === 0 && !Cards.IsStanceCard(c)).Id;
  assert.equal(deck.CanAdd(next), false);
  assert.equal(deck.Add(next), false);
  assert.equal(deck.Total, 40);
});

test('TheFourthCopy_IsRefused_AndAnUnknownIdToo', opt, () => {
  const deck = new m.DeckBuilder();
  for (let i = 0; i < 3; i++) assert.equal(deck.Add('thrust'), true);
  assert.equal(deck.Add('thrust'), false);
  assert.equal(deck.CountOf('thrust'), 3);
  assert.equal(deck.CountText('thrust'), '×3');
  assert.equal(deck.Add('nothing'), false);
  assert.equal(deck.Add(null), false);
});

test('StanceCards_HaveNoCap_OnlyTheThreeOfAKindHolds', opt, () => {
  const deck = new m.DeckBuilder();
  for (const id of ['water_stance', 'water_stance', 'water_stance', 'rock_stance', 'rock_stance', 'rock_stance', 'flow_stance', 'flow_stance', 'flow_stance', 'iron_wall']) {
    assert.equal(deck.Add(id), true, id);
  }
  assert.equal(deck.StanceCount, 10);
  assert.equal(deck.CanAdd('water_stance'), false, 'a fourth copy of one kind');
  assert.equal(deck.CanAdd('priest_prayer'), true, 'another stance kind');
  assert.equal(deck.CanAdd('thrust'), true);
});

test('TheRandomPreset_IsAlwaysALegalDeck_AtEverySize', opt, () => {
  const { DeckBuilder, Constants, Cards } = m;
  for (const size of [Constants.DeckMin, DeckBuilder.RandomDefaultSize, Constants.DeckMax]) {
    for (let seed = 0; seed < 200; seed++) {
      const deck = DeckBuilder.Random(seed, size);
      const at = 'seed ' + seed + ' size ' + size;
      assert.equal(deck.Total, size, at);
      assert.equal(deck.IsValid, true, at);
      assert.equal(deck.StanceCount, deck.Build().filter((c) => Cards.IsStanceCard(c.Def)).length, at);
    }
  }
});

test('ASavedDeckWithManyStanceCards_LoadsWholeNow', opt, () => {
  assert.equal(m.DeckBuilder.Load('water_stance:2,rock_stance:2').Total, 4);
});

test('Removing_TakesOneCopy_AndNothingFromAnEmptyKind', opt, () => {
  const deck = new m.DeckBuilder();
  deck.Add('thrust');
  deck.Add('thrust');
  assert.equal(deck.Remove('thrust'), true);
  assert.equal(deck.CountOf('thrust'), 1);
  assert.equal(deck.Remove('kesa_cut'), false);
  assert.equal(deck.Remove('thrust'), true);
  assert.equal(deck.CountText('thrust'), '');
});

test('TheBuiltDeck_IsInCanonOrder_WithUniqueInstanceIds', opt, () => {
  const deck = new m.DeckBuilder();
  deck.Add('kesa_cut');
  deck.Add('thrust');
  deck.Add('kesa_cut');
  assert.deepStrictEqual(deck.Build().map((c) => c.InstanceId), ['thrust-0', 'kesa_cut-0', 'kesa_cut-1']);
  assert.deepStrictEqual(deck.DeckLines(), ['突き ×1', '袈裟斬り ×2']);
});

const filterCases = [
  ['None', 0, 80], ['Attack', 0, 36], ['Guard', 0, 19], ['Skill', 0, 11], ['Stance', 0, 14],
  ['None', 1, 25], ['None', 2, 31], ['None', 3, 24],
];
for (const [attr, cost, kept] of filterCases) {
  test(`AFilter_KeepsTheCardsCountedAsTheAttribute_AndTheCost(${attr}, ${cost}, ${kept})`, opt, () => {
    const { DeckBuilder, DeckFilter, BattleAttribute } = m;
    const filter = new DeckFilter({ Attribute: BattleAttribute[attr], Cost: cost });
    const cards = new DeckBuilder().Filter(filter);
    assert.equal(cards.length, kept);
    assert.ok(cards.every((c) => filter.Keeps(c)));
  });
}

test('ThePages_CoverTheFilteredList_AndClampTheIndex', opt, () => {
  const { DeckBuilder, DeckFilter, CardCatalog, BattleAttribute } = m;
  const deck = new DeckBuilder();
  const first = deck.Page(DeckFilter.All, 0, 20);
  const last = deck.Page(DeckFilter.All, 99, 20);
  const before = deck.Page(DeckFilter.All, -3, 20);
  const all = [0, 1, 2, 3].flatMap((p) => deck.Page(DeckFilter.All, p, 20).Cards);
  assert.equal(first.Count, 4);
  assert.equal(first.Text, '1 / 4 ページ（80 種）');
  assert.equal(last.Index, 3);
  assert.equal(before.Index, 0);
  assert.deepStrictEqual(all, [...CardCatalog.All]);
  assert.equal(deck.Page(new DeckFilter({ Attribute: BattleAttribute.Stance }), 0, 20).Text, '1 / 1 ページ（14 種）');
});

test('TheCardText_IsTheBridgesWords', opt, () => {
  const { DeckBuilder, CardCatalog } = m;
  assert.equal(DeckBuilder.LineOf(CardCatalog.BoarRush), '猪突猛進　コスト 3　攻撃');
  assert.deepStrictEqual(DeckBuilder.DetailOf(CardCatalog.KesaCut), [
    '袈裟斬り', 'コスト 2　攻撃　届く間合い 0〜1', '敵に 15 ダメージ。', '特性: 間合い0 +5', '「肩口から斬り下ろす」',
  ]);
  assert.equal(DeckBuilder.DetailOf(CardCatalog.Thrust)[3], '特性: なし（素直な札）');
  assert.equal(DeckBuilder.DetailOf(CardCatalog.Brace)[1], 'コスト 2　防御　自分向き');
  assert.ok(CardCatalog.All.every((c) => DeckBuilder.DetailOf(c).every((l) => l.length > 0)));
});

test('ThePrototypePreset_IsTheSlicesTenKindsTimesTwo', opt, () => {
  const { DeckBuilder, PrototypeDeck } = m;
  const deck = DeckBuilder.Prototype();
  assert.equal(deck.Total, 20);
  assert.equal(deck.IsValid, true);
  assert.deepStrictEqual([...new Set(deck.Build().map((c) => c.Def.Id))].sort(), PrototypeDeck.Kinds.map((k) => k.Id).sort());
});

test('TheRandomPreset_AlwaysPasses_AndCanAlwaysClose', opt, () => {
  const { DeckBuilder } = m;
  for (let seed = 0; seed < 300; seed++) {
    const deck = DeckBuilder.Random(seed);
    const at = 'seed ' + seed;
    assert.equal(deck.Total, DeckBuilder.RandomDefaultSize, at);
    assert.equal(deck.IsValid, true, at + ': ' + deck.Validate().Errors.join(' / '));
    assert.ok(deck.CountIn(DeckBuilder.Closers) >= 1, at);
    assert.ok(deck.CountIn(DeckBuilder.Forward) >= DeckBuilder.RandomForwardMin, at);
  }
  assert.equal(DeckBuilder.Random(7).Save(), DeckBuilder.Random(7).Save(), 'the seed fixes it');
  assert.equal(DeckBuilder.Random(1, 5).Total, 20, 'clamped to §8');
  assert.equal(DeckBuilder.Random(1, 99).Total, 40);
  assert.deepStrictEqual(DeckBuilder.Closers.map((c) => c.Id).sort(), ['boar_rush', 'dash_in', 'gale_thrust']);
});

test('TheSavedString_RoundTrips', opt, () => {
  const { DeckBuilder } = m;
  const deck = DeckBuilder.Random(42, 33);
  const saved = deck.Save();
  const loaded = DeckBuilder.Load(saved);
  assert.equal(loaded.Save(), saved);
  assert.deepStrictEqual(loaded.Build().map((c) => c.InstanceId), deck.Build().map((c) => c.InstanceId));
  assert.ok(DeckBuilder.Prototype().Save().startsWith('thrust:2,kesa_cut:2,'));
});

for (const saved of [null, '', 'thrust:2,nothing:1', 'thrust:4', 'thrust:0', 'thrust:-1', 'thrust', 'thrust:2:1', 'thrust:2,thrust:1',
  'thrust:two', '<garbage>', 'thrust:2,,kesa_cut:1']) {
  test(`ABrokenString_GivesAnEmptyDeck(${JSON.stringify(saved)})`, opt, () => {
    assert.equal(m.DeckBuilder.Load(saved).Total, 0);
  });
}

test('TheScreenOpensOnThePrototype_OnlyWhenNothingWasSaved', opt, () => {
  const { DeckBuilder } = m;
  assert.equal(DeckBuilder.LoadOrPrototype(false, '').Save(), DeckBuilder.Prototype().Save(), 'the first Play');
  assert.equal(DeckBuilder.LoadOrPrototype(true, '').Total, 0, 'the player emptied it');
  assert.equal(DeckBuilder.LoadOrPrototype(true, 'thrust:9').Total, 0, 'a broken string starts empty');
  assert.equal(DeckBuilder.LoadOrPrototype(true, 'thrust:2').CountOf('thrust'), 2);
});

for (const saved of ['thrust', 'thrust:0', 'thrust:two', 'thrust:2:1', 'thrust:2,thrust:1', 'thrust:2,,kesa_cut:1', '<garbage>', ':2',
  '<garbage>:1', 'old_thrust:abc', '{"thrust":2}']) {
  test(`AnUnreadableSavedString_IsSaidInOneLine(${JSON.stringify(saved)})`, opt, () => {
    const r = m.DeckBuilder.LoadOrPrototype(true, saved, true);
    assert.equal(r.ok.Total, 0);
    assert.equal(r.value, '保存したデッキを読めなかったため、空のデッキから始めます');
  });
}

test('ARenamedCard_IsNamedInTheLine', opt, () => {
  const r = m.DeckBuilder.LoadOrPrototype(true, 'thrust:2,old_thrust:1', true);
  assert.equal(r.ok.Total, 0);
  assert.equal(r.value, '保存したデッキに知らないカード「old_thrust」があったため、空のデッキから始めます');
});

for (const saved of ['thrust:4', 'thrust:9',
  'thrust:3,kesa_cut:3,overhead:3,wrist_cut:3,side_sweep:3,flat_strike:3,probe_thrust:3,'
  + 'pierce:3,throw_blade:3,reach_thrust:3,brace:3,iron_block:3,low_guard:3,riposte_guard:3']) {
  test(`ASavedDeckPastTheRules_IsSaidWithoutNamingTheRule(${saved.slice(0, 20)})`, opt, () => {
    const r = m.DeckBuilder.LoadOrPrototype(true, saved, true);
    assert.equal(r.ok.Total, 0);
    assert.equal(r.value, '保存したデッキがデッキの決まりに合わないため、空のデッキから始めます');
  });
}

test('NothingIsSaid_WhenNothingWasSaved_TheDeckWasEmptied_OrTheStringReads', opt, () => {
  const { DeckBuilder } = m;
  const firstPlay = DeckBuilder.LoadOrPrototype(false, '', true).value;
  const emptied = DeckBuilder.LoadOrPrototype(true, '', true).value;
  const clean = DeckBuilder.LoadOrPrototype(true, DeckBuilder.Random(3).Save(), true);
  const building = DeckBuilder.LoadOrPrototype(true, 'thrust:2', true);
  assert.equal(firstPlay, '', 'the first Play');
  assert.equal(emptied, '', 'the player emptied it');
  assert.equal(clean.value, '', 'a clean string');
  assert.equal(clean.ok.Save(), DeckBuilder.Random(3).Save());
  assert.equal(building.value, '', 'a deck short of 20 reads: it is being built, not broken');
  assert.equal(building.ok.Total, 2);
});

test('TheTitleAndTheFilterChoices_AreTheBridges', opt, () => {
  const { DeckBuilder, BattleAttribute } = m;
  assert.equal(DeckBuilder.Title, 'デッキを組む（80 種から 20〜40 枚、1 種 3 枚まで）');
  assert.deepStrictEqual(DeckBuilder.AttributeOptions.map((o) => o.Key), ['全て', '攻撃', '防御', '技', '構え']);
  assert.equal(DeckBuilder.AttributeOptions[0].Value, BattleAttribute.None);
  assert.deepStrictEqual(DeckBuilder.CostOptions.map((o) => o.Key), ['全コスト', 'コスト 1', 'コスト 2', 'コスト 3']);
  assert.deepStrictEqual(DeckBuilder.CostOptions.map((o) => o.Value), [0, 1, 2, 3]);
});

test('AStringPastFortyCards_GivesAnEmptyDeck', opt, () => {
  const fortyTwo = m.CardCatalog.All.slice(0, 14).map((c) => c.Id + ':3').join(',');
  assert.equal(m.DeckBuilder.Load(fortyTwo).Total, 0);
});

test('TheDeckScreen_RefusesAKindNotOwned', opt, () => {
  const { DeckBuilder, CardCatalog } = m;
  const owned = CardCatalog.All.slice(1).map((d) => d.Id);
  const deck = new DeckBuilder(owned);
  const notOwned = CardCatalog.All[0].Id;
  assert.equal(deck.CanAdd(notOwned), false);
  assert.equal(deck.Add(notOwned), false);
  assert.equal(deck.CanAdd(CardCatalog.All[1].Id), true);
  assert.equal(new DeckBuilder().Owned.length, CardCatalog.All.length, 'the demo owns the whole catalog');
});

// ---- into the battle (BattleLaunch) ----

test('ABuiltDeck_IsTheDeckTheBattleIsFoughtWith', launchOpt, () => {
  const { BattleLaunch } = launchModule;
  const deck = m.DeckBuilder.Random(5);
  const launch = new BattleLaunch();
  launch.Deck = deck.Build();
  const setup = launch.BuildSetup();
  const ids = deck.Build().map((c) => c.InstanceId).sort();
  assert.deepStrictEqual(setup.Deck.map((c) => c.InstanceId).sort(), ids);
  const source = launch.CreateSource();
  const state = source.State;
  assert.deepStrictEqual([...state.Hand, ...state.DrawPile, ...state.DiscardPile].map((c) => c.InstanceId).sort(), ids);
  source.AdvanceAuto();
  assert.equal(source.Frame.Hand.length, 6);
  assert.ok(source.Frame.Hand.every((f) => ids.includes(f.Id)), 'the hand is dealt from the built deck');
});

test('ADeckThatBreaksTheRules_IsRefusedAtLaunch', launchOpt, () => {
  const { BattleLaunch } = launchModule;
  const launch = new BattleLaunch();
  launch.Deck = filled(19).Build();
  assert.throws(() => launch.BuildSetup(), (e) => e.name === 'ArgumentException' && e.message.includes('minimum is 20'));
});
