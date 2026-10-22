// Tests for src/bridge/depiction/CardFaceText.js (port of View/Depiction/Bridge/CardFaceText.cs).
// Golden: the battle-visual-v1 fields of every one of the eighty cards (values, rows, trait box,
// hover line, detail, underlined terms), every trait condition × effect on five kinds of card, the
// glossary, and the lamps and HP stripes of real hands — byte for byte what the C# printed
// (scratch-csharp/texts → texts.golden.json). Ported cases: CardFaceVisualTests, TermMarkTests,
// UnknownValueTests (CardFaceText's), HpWickReadoutTests (盾押し's stripe).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { golden, load, needs, attempt, traitTexts, syntheticTrait, battleState, handOf } from './_texts.mjs';

const { m, missing } = await load();
const opt = needs(missing);

// ---- golden ----

test('golden: TermsOf / PrintedForms / KindLine / LastingLines / TextLines / AttributeWord of every card', opt, () => {
  const { CardCatalog, CardFaceText } = m;
  for (const row of golden.Cards) {
    const def = CardCatalog.ById(row.Id);
    assert.deepStrictEqual({
      TermsOf: CardFaceText.TermsOf(def),
      PrintedForms: CardFaceText.TermsOf(def).map((w) => CardFaceText.PrintedForms(w)),
      KindLine: CardFaceText.KindLine(def),
      LastingLines: CardFaceText.LastingLines(def.Face),
      TextLinesRaw: CardFaceText.TextLines(def.Face, def.Attributes),
      CardFaceAttributeWord: CardFaceText.AttributeWord(def.Attribute),
    }, {
      TermsOf: row.TermsOf, PrintedForms: row.PrintedForms, KindLine: row.KindLine, LastingLines: row.LastingLines,
      TextLinesRaw: row.TextLinesRaw, CardFaceAttributeWord: row.CardFaceAttributeWord,
    }, def.Id);
  }
});

test('golden: the face fields Fill writes, for every card', opt, () => {
  const { CardCatalog, CardFaceText, CardFace } = m;
  for (const row of golden.Cards) {
    const def = CardCatalog.ById(row.Id);
    const face = new CardFace();
    CardFaceText.Fill(face, def);
    const want = row.Face;
    assert.equal(face.PrintedCost, want.PrintedCost, def.Id);
    assert.deepStrictEqual(face.Values.map((v) => ({ Kind: v.Kind, Number: v.Number, Word: v.Word })), want.Values, def.Id);
    assert.equal(face.Lasting, want.Lasting, def.Id);
    assert.deepStrictEqual(face.TextLines, want.TextLines, def.Id);
    assert.equal(face.Plain, want.Plain, def.Id);
    assert.deepStrictEqual([face.TraitTop, face.TraitBottom], [want.TraitTop, want.TraitBottom], def.Id);
    assert.equal(face.HoverLine, want.HoverLine, def.Id);
    assert.deepStrictEqual({
      Name: face.Detail.Name, KindLine: face.Detail.KindLine, Body: face.Detail.Body,
      Terms: face.Detail.Terms.map((t) => ({ Word: t.Word, Meaning: t.Meaning })),
    }, want.Detail, def.Id);
    assert.deepStrictEqual(face.Terms.map((t) => ({ Row: t.Row, Start: t.Start, Length: t.Length, Word: t.Word, Meaning: t.Meaning })), want.Terms, def.Id);
  }
});

test('golden: every trait condition × effect × variant on five cards (Condition / Effect / clauses / lamp words)', opt, () => {
  const { CardCatalog } = m;
  for (const row of golden.Traits) {
    const def = CardCatalog.ById(row.Card);
    const trait = syntheticTrait(m, row.Condition, row.Effect, row.Variant);
    assert.deepStrictEqual(traitTexts(m, trait, def), row.Texts, `${row.Condition} ${row.Effect} v${row.Variant} ${row.Card}`);
  }
  assert.ok(golden.Traits.length >= 2000);
});

test('golden: the glossary, in its order, and the plain box', opt, () => {
  const { CardFaceText } = m;
  assert.deepStrictEqual([...CardFaceText.Glossary.entries()], golden.Misc.Glossary);
  assert.equal(CardFaceText.PlainTop, golden.Misc.PlainTop);
  assert.equal(CardFaceText.PlainBottom, golden.Misc.PlainBottom);
  assert.equal(CardFaceText.Glossary, CardFaceText.Glossary, 'one dictionary, built once');
  for (const row of golden.Misc.Attributes) assert.equal(CardFaceText.AttributeWord(row.Attr), row.Word, String(row.Attr));
  assert.equal(attempt(() => { CardFaceText.Fill(null, m.CardCatalog.Thrust); return ''; }), golden.Misc.Errors[3]);
});

test('golden: the lamps (aggregated and per enemy) and the HP stripes of real hands', opt, () => {
  let cards = 0;
  for (const row of golden.Battles) {
    const got = handOf(m, battleState(m, row.Setup, row.Deck, row.Gap));
    const at = `${row.Setup} ${row.Deck} gap ${row.Gap}`;
    assert.equal(got.length, row.Hand.length, at);
    for (let i = 0; i < got.length; i++) {
      assert.deepStrictEqual({ Lit: got[i].Lit, PerEnemy: got[i].PerEnemy, HpLoss: got[i].HpLoss },
        { Lit: row.Hand[i].Lit, PerEnemy: row.Hand[i].PerEnemy, HpLoss: row.Hand[i].HpLoss }, `${at} ${got[i].Id}`);
      cards++;
    }
  }
  assert.ok(cards >= 200, `${cards} dealt cards compared`);
});

// ---- CardFaceVisualTests ----

const hand = () => [m.CardCatalog.KesaCut, m.CardCatalog.ReachThrust, m.CardCatalog.Brace, m.CardCatalog.BackLeap, m.CardCatalog.RockStance];

function twoHounds(gap) {
  const { BattleSetup, Enemies, Cards, TurnLoop, FixedRng } = m;
  const noShuffle = new FixedRng(0.9999999);
  const setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(hand(), 1), 8, { StartGap: gap, MoreEnemies: [Enemies.ShadowHound] });
  return TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, noShuffle).State, noShuffle).State;
}

function polearm(gap) {
  const { BattleSetup, Enemies, Cards, TurnLoop, FixedRng } = m;
  const noShuffle = new FixedRng(0.9999999);
  const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(hand(), 1), BattleSetup.SliceFieldCells, { StartGap: gap });
  return TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, noShuffle).State, noShuffle).State;
}

const idOf = (state, def) => state.Hand.find((c) => c.Def === def).InstanceId;
const lit = (state, def) => m.CardFaceText.Lamps(state, idOf(state, def));

test('ACardAimedAtOne_Lights_WhenAnyEnemyInReachSatisfiesIt_AndKeepsEachEnemysVerdict', opt, () => {
  const state = twoHounds(0);
  assert.deepStrictEqual([state.GapTo(0), state.GapTo(1)], [0, 1]);
  const r = lit(state, m.CardCatalog.KesaCut);
  assert.equal(r.ok, true);
  assert.deepStrictEqual(r.value, [true, false]);
});

test('TheFramedEnemy_ReLightsTheLamp_FromTheVerdictTheScriptCarries', opt, () => {
  const { CardCatalog, CoreText, CardFaceText, TurnLoop } = m;
  const state = twoHounds(1);
  assert.deepStrictEqual([state.GapTo(0), state.GapTo(1)], [1, 2]);
  const card = state.Hand.find((c) => c.Def === CardCatalog.ReachThrust);
  const face = CoreText.Face(card, TurnLoop.Preview(state, card.InstanceId), false);
  const r = CardFaceText.Lamps(state, card.InstanceId);
  face.TraitLit = r.ok;
  face.LitOn = r.value;
  assert.equal(face.TraitLit, true);
  assert.equal(face.LitFor(0), false, 'framing the hound at 1 puts the lamp out');
  assert.equal(face.LitFor(1), true);
});

test('ACardThatHoldsOnlyOutOfReach_StaysDark', opt, () => {
  const { CardCatalog, TurnLoop } = m;
  const state = polearm(3);
  const card = state.Hand.find((c) => c.Def === CardCatalog.ReachThrust);
  assert.equal(TurnLoop.Preview(state, card.InstanceId).TraitHolds, true, 'the condition itself holds');
  const r = lit(state, CardCatalog.ReachThrust);
  assert.equal(r.ok, false);
  assert.deepStrictEqual(r.value, [false]);
});

test('ASelfCard_ReadsTheNearestEnemy_AndCarriesNoVerdictPerEnemy', opt, () => {
  const { CardCatalog, Omen, OmenLabel, OmenKind } = m;
  const state = twoHounds(0);
  const attack = new Omen('bite', new OmenLabel(OmenKind.Attack));
  const guard = new Omen('crouch', new OmenLabel(OmenKind.Guard));
  const nearAttacks = state.WithOmen(0, attack).WithOmen(1, guard);
  const farAttacks = state.WithOmen(0, guard).WithOmen(1, attack);
  const r = lit(nearAttacks, CardCatalog.BackLeap);
  assert.equal(r.ok, true);
  assert.deepStrictEqual(r.value, [], 'a card aimed at nobody has one lamp everywhere');
  assert.equal(lit(farAttacks, CardCatalog.BackLeap).ok, false, 'the far hound\'s attack is not read');
});

test('ACardWithNoTrait_IsNeverLit', opt, () => {
  const { BattleSetup, Enemies, Cards, TurnLoop, FixedRng, CardCatalog } = m;
  const noShuffle = new FixedRng(0.9999999);
  const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck([CardCatalog.Thrust, CardCatalog.KesaCut], 3),
    BattleSetup.SliceFieldCells, { StartGap: 0 });
  const state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, noShuffle).State, noShuffle).State;
  const r = lit(state, CardCatalog.Thrust);
  assert.equal(r.ok, false);
  assert.deepStrictEqual(r.value, []);
});

test('APlainCard_PrintsThePlainBox_AndSaysWhereItsTwoWent', opt, () => {
  const face = m.CoreText.FaceOf(m.CardCatalog.Thrust);
  assert.equal(face.Plain, true);
  assert.deepStrictEqual([face.TraitTop, face.TraitBottom], ['素直な札', '特性なし（+2 込み）']);
  assert.equal(face.HoverLine, '特性なし。そのぶん威力が 2 高い（23 に込み）');
  assert.deepStrictEqual(face.Values.map((v) => v.Word + ' ' + v.Number), ['威力 23']);
});

const section9 = [
  ['kesa_cut', '間合い 0', '→ 威力 +5', '隣り合っていれば、この札の威力 15 → 20'],
  ['reach_thrust', '間合い 2 以上', '→ 威力 +5', '間合い 2 以上なら、この札の威力 15 → 20'],
  ['brace', '温存 残り 6 以上', '→ 次ターン回復 +1', '出した後スタミナが 6 以上残れば、次ターンの回復 3 → 4'],
  ['rock_stance', '置く時 予兆が攻撃', '→ Guard +3（1 回）', '置くときに敵の予兆が攻撃なら、その場で Guard +3（1 回だけ）'],
  ['war_cry', '連打', '→ 敵に脆化 2', 'このターン直前に出した札もスキルなら、敵に脆化 2 も付ける'],
  ['back_leap', '予兆が攻撃', '→ Guard +3', 'いちばん近い敵の予兆が攻撃なら、この札の Guard 4 → 7'],
  ['body_check', '間合い 0', '→ 重撃', '隣り合っていれば、威力 5 → 11。ただし次ターンの回復 3 → 2'],
  ['stone_throw', '間合い 2 以上', '→ コスト −1', '間合い 2 以上なら、この札のコスト 2 → 1'],
  ['observe', '手薄', '→ ドロー +1', '出した直後の手札が 2 枚以下なら、さらに 1 枚引く'],
];

for (const [id, top, bottom, hover] of section9) {
  test(`TheSampleCards_ReadAsSection9Writes(${id})`, opt, () => {
    const face = m.CoreText.FaceOf(m.CardCatalog.ById(id));
    assert.equal(face.TraitTop, top);
    assert.equal(face.TraitBottom, bottom);
    assert.equal(face.HoverLine, hover);
    assert.equal(face.Plain, false);
  });
}

test('TheCardWithTwoTraits_PutsOneTraitOnEachRow', opt, () => {
  const face = m.CoreText.FaceOf(m.CardCatalog.ById('last_stand'));
  assert.deepStrictEqual([face.TraitTop, face.TraitBottom], ['間合い2以上→威力+5', '死力→威力+3']);
  assert.ok(face.HoverLine.includes('。'), 'two sentences, one per trait');
});

test('StatusesLeadTheRows_AndAreNeverAValueColumn', opt, () => {
  const { CoreText, CardCatalog, CardValueKind } = m;
  const body = CoreText.FaceOf(CardCatalog.ById('body_check'));
  const stone = CoreText.FaceOf(CardCatalog.ById('stone_throw'));
  const cry = CoreText.FaceOf(CardCatalog.ById('war_cry'));
  assert.equal(body.TextLines[0], '敵に 鈍足 2');
  assert.equal(stone.TextLines[0], '敵に 脆化 2・威圧 2');
  assert.deepStrictEqual(cry.TextLines, ['敵に 威圧 2', '自分に 強化 2']);
  assert.deepStrictEqual(body.Values.map((v) => v.Kind), [CardValueKind.Power]);
});

test('AMoveIsAValueColumn_WithItsWord', opt, () => {
  const { CoreText, CardCatalog, CardValueKind } = m;
  const leap = CoreText.FaceOf(CardCatalog.BackLeap);
  assert.deepStrictEqual(leap.Values.map((v) => v.Kind), [CardValueKind.Back, CardValueKind.Guard]);
  assert.deepStrictEqual(leap.Values.map((v) => v.Word + ' ' + v.Number), ['離れる 2', 'Guard 4']);
});

test('AStanceCard_HasNoValues_AndItsRowsAreLasting', opt, () => {
  const face = m.CoreText.FaceOf(m.CardCatalog.RockStance);
  assert.deepStrictEqual(face.Values, []);
  assert.equal(face.Lasting, true);
  assert.deepStrictEqual(face.TextLines, ['毎ターン開始に Guard +3']);
});

test('EveryCatalogCard_KeepsToTheFacesLimits', opt, () => {
  const { CoreText, CardCatalog, CardFaceText } = m;
  const misfits = [];
  for (const def of CardCatalog.All) {
    const face = CoreText.FaceOf(def);
    if (face.Values.length > CardFaceText.MaxValues) misfits.push(def.Name + ': ' + face.Values.length + ' values');
    const rows = face.Lasting ? CardFaceText.MaxLastingLines : CardFaceText.MaxTextLines;
    if (face.TextLines.length > rows) misfits.push(def.Name + ': ' + face.TextLines.length + ' rows');
    if (face.TraitTop.length === 0 || face.TraitBottom.length === 0) misfits.push(def.Name + ': an empty trait row');
    if (face.HoverLine.length === 0) misfits.push(def.Name + ': no hover line');
    if (face.PrintedCost !== def.Cost) misfits.push(def.Name + ': printed cost ' + face.PrintedCost);
    if (face.Detail == null || face.Detail.Body.length === 0) misfits.push(def.Name + ': no detail');
  }
  assert.deepStrictEqual(misfits, []);
});

test('EveryTraitRow_FitsTheTraitBoxAt16Px', opt, () => {
  const width = 165;
  const size = 16;
  const misfits = [];
  for (const def of m.CardCatalog.All) {
    const face = m.CoreText.FaceOf(def);
    for (const row of [face.TraitTop, face.TraitBottom]) {
      let ems = 0;
      for (let i = 0; i < row.length; i++) {
        const c = row.charCodeAt(i);
        ems += c >= 0x2E80 ? 1 : c === 0x20 ? 0.3 : 0.6;
      }
      if (ems * size > width) misfits.push(def.Name + ' 「' + row + '」 ' + ems * size + ' px');
    }
  }
  assert.deepStrictEqual(misfits, []);
});

test('TheDiscountedCost_IsMarked', opt, () => {
  const { BattleSetup, Enemies, Cards, TurnLoop, FixedRng, CardCatalog, CoreText } = m;
  const noShuffle = new FixedRng(0.9999999);
  const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck([CardCatalog.ById('stone_throw')], 6),
    BattleSetup.SliceFieldCells, { StartGap: 2 });
  const state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, noShuffle).State, noShuffle).State;
  const card = state.Hand[0];
  const face = CoreText.Face(card, TurnLoop.Preview(state, card.InstanceId), false);
  assert.equal(face.PrintedCost, 2);
  assert.equal(face.Cost, 1, '石礫 costs one less from a gap of 2');
  assert.equal(face.CostDrop, 1);
});

test('TheDetail_NamesOneAttribute_TheTargetAndTheReach', opt, () => {
  const { CoreText, CardCatalog } = m;
  const kesa = CoreText.FaceOf(CardCatalog.KesaCut).Detail;
  const brace = CoreText.FaceOf(CardCatalog.Brace).Detail;
  assert.equal(kesa.Name, '袈裟斬り');
  assert.equal(kesa.KindLine, '攻撃 ・ 敵 1 体 ・ 間合い 0〜1');
  assert.ok(kesa.Body.includes('隣り合っていれば、この札の威力 15 → 20'));
  assert.equal(brace.KindLine, '防御 ・ 自分');
  assert.ok(brace.Terms.map((t) => t.Word).includes('温存'));
  assert.ok(brace.Body.startsWith('Guard 9 を得る。'));
});

test('TheDetail_ListsTheTermsTheCardUses_WithTheirMeaning', opt, () => {
  const body = m.CoreText.FaceOf(m.CardCatalog.ById('body_check')).Detail;
  assert.deepStrictEqual(body.Terms.map((t) => t.Word), ['間合い', '重撃']);
  assert.equal(body.Terms[1].Meaning, '威力 +6。そのかわり次のターン開始のスタミナ回復が 1 減る');
});

// ---- TermMarkTests ----

function printed(face, mark) {
  const row = mark.Row === m.TermRow.TraitTop ? face.TraitTop : face.TraitBottom;
  return row.substring(mark.Start, mark.Start + mark.Length);
}

test('BodyCheck_UnderlinesTheGapInItsCondition_AndTheHeavyBlowInItsEffect', opt, () => {
  const { CoreText, CardCatalog, CardFaceText } = m;
  const face = CoreText.FaceOf(CardCatalog.BodyCheck);
  assert.equal(face.TraitTop, '間合い 0');
  assert.equal(face.TraitBottom, '→ 重撃');
  assert.deepStrictEqual(face.Terms.map((t) => t.Row + ':' + printed(face, t)), ['TraitTop:間合い', 'TraitBottom:重撃']);
  const heavy = face.Terms.filter((t) => t.Word === '重撃');
  assert.equal(heavy.length, 1);
  assert.equal(heavy[0].Start, 2);
  assert.equal(heavy[0].Meaning, CardFaceText.Glossary.get('重撃'));
});

test('Brace_UnderlinesReserve', opt, () => {
  const { CoreText, CardCatalog, TermRow } = m;
  const face = CoreText.FaceOf(CardCatalog.Brace);
  assert.equal(face.Terms.length, 1);
  const reserve = face.Terms[0];
  assert.equal(reserve.Word, '温存');
  assert.equal(reserve.Row, TermRow.TraitTop);
  assert.equal(reserve.Start, 0);
  assert.equal(printed(face, reserve), '温存');
});

test('Lunge_UnderlinesCombo_UnderItsGlossaryWord', opt, () => {
  const face = m.CoreText.FaceOf(m.CardCatalog.Lunge);
  const combo = face.Terms.filter((t) => t.Word === '連動 X');
  assert.equal(combo.length, 1);
  assert.equal(printed(face, combo[0]), '連動', '§7.5 連動 X is printed 連動 スタンス');
  assert.ok(combo[0].Meaning.includes('属性 X'));
});

test('APlainCard_UnderlinesItsName_InTheBox', opt, () => {
  const face = m.CoreText.FaceOf(m.CardCatalog.Thrust);
  assert.equal(face.Plain, true);
  assert.equal(face.Terms.length, 1);
  assert.equal(face.Terms[0].Word, '素直な札');
  assert.equal(printed(face, face.Terms[0]), '素直な札');
});

test('EveryMark_InTheCatalog_PointsAtItsPrintedWord_AndNoTwoOverlap', opt, () => {
  const { CoreText, CardCatalog, CardFaceText } = m;
  for (const def of CardCatalog.All) {
    const face = CoreText.FaceOf(def);
    for (const mark of face.Terms) {
      assert.ok(CardFaceText.PrintedForms(mark.Word).includes(printed(face, mark)), def.Name + ': ' + mark.Word);
      assert.notEqual(mark.Meaning, '', def.Name);
      assert.ok(CardFaceText.Glossary.has(mark.Word));
    }
    for (const a of face.Terms) {
      for (const b of face.Terms) {
        if (a === b || a.Row !== b.Row) continue;
        const overlap = a.Start < b.Start + b.Length && b.Start < a.Start + a.Length;
        assert.equal(overlap, false, def.Name + ': ' + a.Word + ' / ' + b.Word);
      }
    }
  }
});

test('PrintedForms_SplitsAPair_AndDropsTheParameter', opt, () => {
  const { CardFaceText } = m;
  assert.deepStrictEqual(CardFaceText.PrintedForms('詰める / 離れる'), ['詰める', '離れる']);
  assert.deepStrictEqual(CardFaceText.PrintedForms('連動 X'), ['連動']);
  assert.deepStrictEqual(CardFaceText.PrintedForms('崩し n'), ['崩し']);
  assert.deepStrictEqual(CardFaceText.PrintedForms('重撃'), ['重撃']);
});

// ---- HpWickReadoutTests: a Guard card that pushes into the wall has a stripe ----

test('HpLoss_IsEmpty_ForACardAimedAtNobody_OrNotInTheHand', opt, () => {
  const { CardFaceText, CardCatalog } = m;
  const state = polearm(0);
  assert.deepStrictEqual(CardFaceText.HpLoss(state, idOf(state, CardCatalog.Brace)), []);
  assert.deepStrictEqual(CardFaceText.HpLoss(state, 'nothing-0'), []);
  assert.equal(CardFaceText.HpLoss(state, idOf(state, CardCatalog.KesaCut)).length, 1);
  assert.throws(() => CardFaceText.HpLoss(null, 'x'), (e) => e.name === 'ArgumentNullException');
  assert.throws(() => CardFaceText.Lamps(null, 'x'), (e) => e.name === 'ArgumentNullException');
});

// ---- UnknownValueTests (CardFaceText's part) ----

test('EveryTraitCondition_AndEffect_HasItsWords_OnTheFaceAndTheHover', opt, () => {
  const { CardFaceText, CardCatalog, Trait, StatusGrant, TraitCondition, TraitEffect, BattleAttribute, OmenKind, StatusKind, hasFlag } = m;
  const full = (c, e) => new Trait(c, e, 2, 1, BattleAttribute.Attack, OmenKind.Attack, StatusKind.Bleed, new StatusGrant(StatusKind.Bleed, 2));
  const cards = [
    CardCatalog.All.find((c) => hasFlag(c.Attribute, BattleAttribute.Attack)),
    CardCatalog.All.find((c) => hasFlag(c.Attribute, BattleAttribute.Stance)),
  ];
  for (const c of TraitCondition.$order) {
    for (const def of cards) {
      assert.notEqual(CardFaceText.Condition(full(c, TraitEffect.PowerBonus), def), '', c);
      assert.notEqual(CardFaceText.ConditionClause(full(c, TraitEffect.PowerBonus), def), '', c);
    }
  }
  for (const e of TraitEffect.$order) {
    for (const def of cards) {
      assert.notEqual(CardFaceText.Effect(full(TraitCondition.GapAtMost, e), def), '', e);
      assert.notEqual(CardFaceText.EffectClause(full(TraitCondition.GapAtMost, e), def), '', e);
    }
  }
});

test('AValueOutsideTheEnums_Throws_NamingTheValue', opt, () => {
  const { CardFaceText, CardCatalog, Trait, TraitCondition, TraitEffect } = m;
  const def = CardCatalog.Thrust;
  const isRange = (e) => e.name === 'ArgumentOutOfRangeException' && e.message.includes('999');
  assert.throws(() => CardFaceText.Condition(new Trait(999, TraitEffect.PowerBonus), def), isRange);
  assert.throws(() => CardFaceText.ConditionClause(new Trait(999, TraitEffect.PowerBonus), def), isRange);
  assert.throws(() => CardFaceText.Effect(new Trait(TraitCondition.GapAtMost, 999), def), isRange);
  assert.throws(() => CardFaceText.EffectClause(new Trait(TraitCondition.GapAtMost, 999), def), isRange);
});
