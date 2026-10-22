// Tests for src/bridge/depiction/DepartureBuilder.js (port of View/Depiction/Bridge/DepartureBuilder.cs).
// Golden: a departure screen driven step by step (slots, start, talent, the lines it prints, the
// Departure it builds and the errors it validates to), the option lines and the talent pages —
// byte for byte what the C# printed (scratch-csharp/texts → texts.golden.json). Ported cases:
// DepartureBuilderTests (the DemoSession ones run when src/bridge/depiction/DemoSession.js exists).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { golden, load, loadOptional, needs, attempt } from './_texts.mjs';

const { m, missing } = await load();
const opt = needs(missing);
const demoLoad = missing ? { mod: null, skip: opt } : await loadOptional('../../src/bridge/depiction/DemoSession.js');
const demoModule = demoLoad.mod;
const demoOpt = demoLoad.skip;

const g = golden.Departure;
const Shoes = 'maai_no_kutsu';
const RyugoNote = 'ボス専用の状態が 1 スタック低く乗る';

/** The C# test's Builder(), plus the golden program's nameless option (null name and note, a negative start). */
function builder(withNameless = false) {
  const { SupplyOption, DepartureBuilder } = m;
  const tools = [
    new SupplyOption('bosho_no_men', '防瘴の面', '層の濃度を 1 下げる'),
    new SupplyOption(Shoes, '間合いの履', '開始のマスが 2 から 1 になる', 1),
    new SupplyOption('ikitsugi_no_fue', '息継ぎの笛', '戦闘開始時のスタミナ +3', 0, 3),
    new SupplyOption('ryurin_no_kakera', '竜鱗の欠片', '1 戦につき最初の 1 回だけ Guard 5', 0, 0, 5),
    new SupplyOption('ryugo_no_fu', '竜語の符', RyugoNote, 0, 0, 0, 1),
  ];
  if (withNameless) tools.push(new SupplyOption('nameless', null, null, -2));
  const consumables = [
    new SupplyOption('yakuso', '薬草', 'HP が最大の 20% 戻る'),
    new SupplyOption('wasuremizu', '忘れ水', 'いまのスタミナが全回復'),
  ];
  return new DepartureBuilder(tools, consumables);
}

function snapshot(b, deck) {
  const d = b.Build(deck);
  return {
    ToolsLine: b.ToolsLine, ConsumablesLine: b.ConsumablesLine, StartLine: b.StartLine,
    PlayerStartCell: b.PlayerStartCell, StartGap: b.StartGap, StartCellsBack: b.StartCellsBack,
    StartStaminaBonus: b.StartStaminaBonus, FirstHitGuard: b.FirstHitGuard, BossWordWard: b.BossWordWard,
    TalentLine: b.TalentLine(deck), StatusText: b.StatusText(deck), CanSetOut: b.CanSetOut(deck),
    Errors: b.Validate(deck).Errors, Tools: [...b.Tools], Consumables: [...b.Consumables],
    Departure: {
      Deck: d.Deck.map((c) => c.InstanceId), Tools: d.Tools, Consumables: d.Consumables,
      StartCellsBack: d.StartCellsBack, TalentCardId: d.TalentCardId, StartStaminaBonus: d.StartStaminaBonus,
      FirstHitGuard: d.FirstHitGuard, BossWordWard: d.BossWordWard,
    },
  };
}

// ---- golden ----

test('golden: the headings, the labels and the option lines', opt, () => {
  const { DepartureBuilder } = m;
  assert.deepStrictEqual({
    Title: DepartureBuilder.Title, ToolHeading: DepartureBuilder.ToolHeading, ConsumableHeading: DepartureBuilder.ConsumableHeading,
    TalentHeading: DepartureBuilder.TalentHeading, SetOutLabel: DepartureBuilder.SetOutLabel, BackLabel: DepartureBuilder.BackLabel,
  }, {
    Title: g.Title, ToolHeading: g.ToolHeading, ConsumableHeading: g.ConsumableHeading, TalentHeading: g.TalentHeading,
    SetOutLabel: g.SetOutLabel, BackLabel: g.BackLabel,
  });
  const b = builder(true);
  assert.deepStrictEqual(b.ToolOptions.map((o) => DepartureBuilder.LineOf(o)), g.ToolLines);
  assert.deepStrictEqual(b.ConsumableOptions.map((o) => DepartureBuilder.LineOf(o)), g.ConsumableLines);
  assert.deepStrictEqual(b.ToolOptions.map((o) => ({
    Id: o.Id, Name: o.Name, Note: o.Note, StartCellsBack: o.StartCellsBack, StartStaminaBonus: o.StartStaminaBonus,
    FirstHitGuard: o.FirstHitGuard, BossWordWard: o.BossWordWard, WorksInBattle: o.WorksInBattle,
  })), g.Options);
  assert.equal(attempt(() => new m.SupplyOption('', 'x', 'y').Name), golden.Misc.Errors[5]);
});

test('golden: a departure screen driven step by step', opt, () => {
  const { DeckBuilder } = m;
  const steps = new Map(g.Steps.map((s) => [s.Step, s]));
  const check = (name, b, deck, ok) => {
    const want = steps.get(name);
    if ('Ok' in want) assert.equal(ok, want.Ok, name + ' ok');
    assert.deepStrictEqual(snapshot(b, deck), want.State, name);
  };
  const deck = DeckBuilder.Prototype();
  const b = builder(true);
  check('fresh', b, deck);
  check('talent outside', b, deck, b.ChooseTalent(deck, 'overhead'));
  check('talent', b, deck, b.ChooseTalent(deck, 'kesa_cut'));
  check('men', b, deck, b.AddTool('bosho_no_men'));
  check('men2', b, deck, b.AddTool('bosho_no_men'));
  check('shoes', b, deck, b.AddTool(Shoes));
  check('fourth', b, deck, b.AddTool('ikitsugi_no_fue'));
  check('remove men', b, deck, b.RemoveTool('bosho_no_men'));
  check('shoes2', b, deck, b.AddTool(Shoes));
  check('remove absent', b, deck, b.RemoveTool('ryugo_no_fu'));
  check('yakuso x3', b, deck, b.AddConsumable('yakuso') && b.AddConsumable('yakuso') && b.AddConsumable('wasuremizu'));
  check('consumable 4', b, deck, b.AddConsumable('yakuso'));
  check('consumable unknown', b, deck, b.AddConsumable('nothing'));
  check('remove consumable', b, deck, b.RemoveConsumable('yakuso'));
  deck.Remove('kesa_cut');
  check('deck 19', b, deck);
  deck.Remove('kesa_cut');
  check('talent left', b, deck);

  const c = builder(true);
  const deck2 = DeckBuilder.Prototype();
  c.ChooseTalent(deck2, 'shield_bash');
  c.AddTool('ikitsugi_no_fue');
  c.AddTool('ikitsugi_no_fue');
  c.AddTool('ryugo_no_fu');
  check('battle tools', c, deck2);

  const e = builder(true);
  e.AddTool('nameless');
  e.AddTool('ryurin_no_kakera');
  check('nameless', e, deck2);
});

test('golden: the talent choices and pages of a 33-card random deck', opt, () => {
  const { DeckBuilder, DepartureBuilder } = m;
  const big = DeckBuilder.Random(17, 33);
  assert.deepStrictEqual(DepartureBuilder.TalentChoices(big).map((x) => x.Id), g.TalentChoices);
  for (const row of g.TalentPages) {
    const p = DepartureBuilder.TalentPage(big, row.Page, 4);
    assert.deepStrictEqual({ Text: p.Text, Index: p.Index, Count: p.Count, Kept: p.Kept, Cards: p.Cards.map((x) => x.Id) },
      { Text: row.Text, Index: row.Index, Count: row.Count, Kept: row.Kept, Cards: row.Cards }, String(row.Page));
  }
});

// ---- DepartureBuilderTests ----

const deckOf = () => m.DeckBuilder.Prototype();

function ready(deck) {
  const b = builder();
  assert.equal(b.ChooseTalent(deck, m.PrototypeDeck.Kinds[0].Id), true);
  return b;
}

test('ThreeToolsFit_TheSameOneTwice_AFourthDoesNot', opt, () => {
  const b = builder();
  assert.equal(b.AddTool('bosho_no_men'), true);
  assert.equal(b.AddTool('bosho_no_men'), true, 'the same tool may fill two slots');
  assert.equal(b.AddTool(Shoes), true);
  assert.equal(b.CanAddTool('ikitsugi_no_fue'), false);
  assert.equal(b.AddTool('ikitsugi_no_fue'), false);
  assert.equal(b.ToolCount('bosho_no_men'), 2);
  assert.equal(b.ToolsLine, 'ツール 3 / 3: 防瘴の面、防瘴の面、間合いの履');
  assert.equal(b.RemoveTool('bosho_no_men'), true);
  assert.deepStrictEqual([...b.Tools], ['bosho_no_men', Shoes]);
});

test('ThreeConsumablesFit_AFourthDoesNot_AndAnUnknownIdNever', opt, () => {
  const { DeckRules } = m;
  const b = builder();
  assert.equal(b.AddConsumable('nothing'), false);
  assert.equal(b.AddTool('yakuso'), false, 'a consumable does not go in a tool slot');
  for (let i = 0; i < DeckRules.ConsumableSlots; i++) assert.equal(b.AddConsumable('yakuso'), true);
  assert.equal(b.AddConsumable('wasuremizu'), false);
  assert.equal(b.ConsumableCount('yakuso'), 3);
  assert.equal(b.ConsumablesLine, '消耗品 3 / 3: 薬草、薬草、薬草');
  assert.equal(builder().ConsumablesLine, '消耗品 0 / 3: なし');
});

test('TheShoesMoveTheStartToCellOneAtGapFour_ASecondPairDoesNothingMore', opt, () => {
  const b = builder();
  assert.equal(b.StartLine, '開始のマス 2・開始の間合い 3');
  b.AddTool(Shoes);
  assert.equal(b.PlayerStartCell, 1);
  assert.equal(b.StartGap, 4);
  assert.equal(b.StartLine, '開始のマス 1・開始の間合い 4（間合いの履）');
  b.AddTool(Shoes);
  assert.equal(b.PlayerStartCell, 1);
  assert.equal(b.StartGap, 4);
  assert.equal(b.StartLine, '開始のマス 1・開始の間合い 4（間合いの履）', 'the tool is named once');
});

for (const [shoes, cell, gap] of [[false, 2, 3], [true, 1, 4]]) {
  test(`TheStartReachesTheBattle_InASingleBattleAndInTheChain(${shoes}, ${cell}, ${gap})`, demoOpt, () => {
    const { DemoSession } = demoModule;
    const deck = deckOf();
    const b = ready(deck);
    if (shoes) b.AddTool(Shoes);
    const departure = b.Build(deck);
    const single = DemoSession.Single(departure, 'polearm_warped', 1).StartBattle().State;
    const chain = DemoSession.Chain(departure, 1).StartBattle().State;
    for (const state of [single, chain]) {
      assert.equal(state.Player.Cell, cell);
      assert.equal(state.Gap, gap);
    }
  });
}

test('TheTalentIsRecorded_AndReachesTheBuild', opt, () => {
  const { PrototypeDeck } = m;
  const deck = deckOf();
  const b = builder();
  const id = PrototypeDeck.Kinds[3].Id;
  assert.equal(b.ChooseTalent(deck, id), true);
  const departure = b.Build(deck);
  assert.equal(departure.TalentCardId, id);
  assert.equal(b.TalentLine(deck), '才能: ' + PrototypeDeck.Kinds[3].Name);
});

test('TheTalentReachesTheRun', demoOpt, () => {
  const { DemoSession } = demoModule;
  const deck = deckOf();
  const b = builder();
  const id = m.PrototypeDeck.Kinds[3].Id;
  b.ChooseTalent(deck, id);
  const departure = b.Build(deck);
  assert.equal(DemoSession.Single(departure, 'polearm_warped', 1).Departure.TalentCardId, id);
  assert.equal(DemoSession.Chain(departure, 1).Departure.TalentCardId, id);
});

test('OnlyAKindOfTheDeckCanBeTheTalent_AndOneThatLeavesIsDropped', opt, () => {
  const { CardCatalog, PrototypeDeck } = m;
  const deck = deckOf();
  const b = builder();
  const outside = CardCatalog.All.find((d) => deck.CountOf(d.Id) === 0).Id;
  assert.equal(b.ChooseTalent(deck, outside), false);
  assert.equal(b.TalentLine(deck), '才能: まだ選んでいません');
  const id = PrototypeDeck.Kinds[0].Id;
  b.ChooseTalent(deck, id);
  deck.Remove(id);
  deck.Remove(id);
  assert.equal(b.TalentIn(deck), null, 'the talent left with its last copy');
  assert.equal(b.Build(deck).TalentCardId, null);
});

test('TheTalentChoices_AreTheDecksKindsInCanonOrder_PagedAndClamped', opt, () => {
  const { CardCatalog, DepartureBuilder } = m;
  const deck = deckOf();
  assert.deepStrictEqual(DepartureBuilder.TalentChoices(deck).map((d) => d.Id),
    CardCatalog.All.filter((d) => deck.CountOf(d.Id) > 0).map((d) => d.Id));
  const kinds = DepartureBuilder.TalentChoices(deck).length;
  const last = DepartureBuilder.TalentPage(deck, 99, 4);
  assert.equal(last.Index, Math.trunc((kinds - 1) / 4));
  assert.equal(last.Cards.length, kinds - last.Index * 4);
});

test('ItSetsOutOnlyWithALegalDeckAndATalent', opt, () => {
  const { PrototypeDeck } = m;
  const deck = deckOf();
  const b = builder();
  assert.equal(b.CanSetOut(deck), false);
  assert.equal(b.StatusText(deck), '才能にする札を 1 枚選ぶと出立できます');
  b.ChooseTalent(deck, PrototypeDeck.Kinds[0].Id);
  assert.equal(b.CanSetOut(deck), true);
  assert.equal(b.StatusText(deck), '出立できます');
  deck.Remove(PrototypeDeck.Kinds[5].Id);
  assert.equal(deck.Total, 19);
  assert.equal(b.CanSetOut(deck), false);
  assert.equal(b.StatusText(deck), 'デッキが決まりに合っていません。デッキに戻って直してください');
});

test('TheSlotsAreTheCoresThree', opt, () => {
  const { DeckRules, DepartureBuilder } = m;
  assert.equal(DeckRules.ToolSlots, 3);
  assert.equal(DeckRules.ConsumableSlots, 3);
  assert.equal(DepartureBuilder.Title, '出立の支度（ツール 3 枠・消耗品 3 枠・才能 1 枚）');
});

test('ASupplyTheBattleDoesNotFeel_SaysSo', opt, () => {
  const { DepartureBuilder } = m;
  const b = builder();
  assert.equal(DepartureBuilder.LineOf(b.ToolOptions[0]), '防瘴の面　層の濃度を 1 下げる（この試験の戦闘では効果が出ません）');
  assert.equal(DepartureBuilder.LineOf(b.ToolOptions[1]), '間合いの履　開始のマスが 2 から 1 になる');
});

test('TheOtherThreeBattleTools_CarryNoSuffix_AndReachTheDeparture', opt, () => {
  const { DepartureBuilder, PrototypeDeck } = m;
  const b = builder();
  assert.equal(DepartureBuilder.LineOf(b.ToolOptions[2]), '息継ぎの笛　戦闘開始時のスタミナ +3');
  assert.equal(DepartureBuilder.LineOf(b.ToolOptions[3]), '竜鱗の欠片　1 戦につき最初の 1 回だけ Guard 5');
  assert.equal(DepartureBuilder.LineOf(b.ToolOptions[4]), '竜語の符　' + RyugoNote);
  const deck = deckOf();
  assert.equal(b.ChooseTalent(deck, PrototypeDeck.Kinds[0].Id), true);
  b.AddTool('ikitsugi_no_fue');
  b.AddTool('ryurin_no_kakera');
  b.AddTool('ryugo_no_fu');
  const departure = b.Build(deck);
  assert.equal(departure.StartStaminaBonus, 3);
  assert.equal(departure.FirstHitGuard, 5);
  assert.equal(departure.BossWordWard, 1);
  assert.equal(b.CanSetOut(deck), true);
});

test('TheSameToolTwice_AddsItsAmountTwice', opt, () => {
  const b = builder();
  b.AddTool('ikitsugi_no_fue');
  b.AddTool('ikitsugi_no_fue');
  b.AddTool('ryugo_no_fu');
  assert.equal(b.StartStaminaBonus, 6);
  assert.equal(b.FirstHitGuard, 0);
  assert.equal(b.BossWordWard, 1);
});

test('TheArguments_AreChecked_LikeTheCs', opt, () => {
  const { DepartureBuilder, SupplyOption } = m;
  const isNull = (e) => e.name === 'ArgumentNullException';
  assert.throws(() => new DepartureBuilder(null, []), (e) => isNull(e) && e.message.includes("'tools'"));
  assert.throws(() => new DepartureBuilder([], null), (e) => isNull(e) && e.message.includes("'consumables'"));
  assert.throws(() => builder().ChooseTalent(null, 'thrust'), isNull);
  assert.throws(() => DepartureBuilder.LineOf(null), isNull);
  assert.throws(() => new SupplyOption(null, 'x', 'y'), (e) => e.name === 'ArgumentException');
  assert.throws(() => DepartureBuilder.TalentPage(deckOf(), 0, 0), (e) => e.name === 'ArgumentOutOfRangeException');
  const o = new SupplyOption('id', null, undefined, -3, -1, -2, -5);
  assert.deepStrictEqual([o.Name, o.Note, o.StartCellsBack, o.StartStaminaBonus, o.FirstHitGuard, o.BossWordWard, o.WorksInBattle],
    ['id', '', 0, 0, 0, 0, false]);
});
