// Tests for src/bridge/depiction/CoreText.js (port of View/Depiction/Bridge/CoreText.cs).
// Golden: every one of the eighty cards, every enemy action's omen, the stance clauses, the chips
// and the reach hints of real hands, compared byte for byte with what the C# printed
// (scratch-csharp/texts → texts.golden.json). Ported cases: CoreVocabularyTests (the card text),
// CoreOmenIconTests, ReachHintTests, BossWordTextTests (the lines), UnknownValueTests (CoreText's),
// PreviewNumberTests (a face and an omen sum their blows).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  golden, load, needs, attempt, faceJson, chipJson, omenJson, omenStatusJson, traitTexts, syntheticStance,
  battleState, handOf,
} from './_texts.mjs';

const { m, missing } = await load();
const opt = needs(missing);

// ---- golden: the eighty cards ----

test('golden: every card face (FaceOf) is the C#\'s, field for field', opt, () => {
  const { CardCatalog, CoreText } = m;
  assert.equal(golden.Cards.length, CardCatalog.All.length);
  for (let i = 0; i < CardCatalog.All.length; i++) {
    const def = CardCatalog.All[i];
    const row = golden.Cards[i];
    assert.equal(def.Id, row.Id, 'canon order');
    assert.deepStrictEqual(faceJson(CoreText.FaceOf(def)), row.Face, def.Id);
  }
});

test('golden: a dealt card without a preview (Face(card, null)) is the C#\'s', opt, () => {
  const { CardCatalog, CoreText, CardInstance } = m;
  for (const row of golden.Cards) {
    const def = CardCatalog.ById(row.Id);
    assert.deepStrictEqual(faceJson(CoreText.Face(new CardInstance(def.Id + '-7', def), null)), row.Instance, def.Id);
  }
});

test('golden: Describe / ValueOf / FacePower / TraitLines / reach / system / kind / stance of every card', opt, () => {
  const { CardCatalog, CoreText } = m;
  for (const row of golden.Cards) {
    const def = CardCatalog.ById(row.Id);
    const got = {
      Describe: CoreText.Describe(def.Face, def.Attributes),
      ValueOf: CoreText.ValueOf(def.Face, def.Attributes),
      FacePower: CoreText.FacePower(def.Face),
      TraitLines: CoreText.TraitLines(def),
      ReachText: CoreText.ReachText(def.Attributes, def.Face, def.Targets),
      ReachHint: CoreText.ReachHint(def.Attributes, def.Face, def.Targets),
      SystemOf: CoreText.SystemOf(def),
      KindOf: CoreText.KindOf(def.Attribute),
      KindOfDeclared: CoreText.KindOf(def.Attributes),
      StanceText: def.Face.Stance == null ? null : CoreText.StanceText(def.Face.Stance),
    };
    const want = {};
    for (const k of Object.keys(got)) want[k] = row[k];
    assert.deepStrictEqual(got, want, def.Id);
  }
});

test('golden: each trait of every card reads the C#\'s lamp words (TraitLine / ConditionWord / EffectWord)', opt, () => {
  const { CardCatalog } = m;
  let traits = 0;
  for (const row of golden.Cards) {
    const def = CardCatalog.ById(row.Id);
    assert.deepStrictEqual(def.AllTraits.map((t) => traitTexts(m, t, def)), row.Traits, def.Id);
    traits += def.AllTraits.length;
  }
  assert.ok(traits >= 60, `${traits} traits compared`);
});

// ---- golden: omens ----

test('golden: every enemy action\'s omen frame, icon and system, with and without the core preview', opt, () => {
  const { Enemies, EnemyAi, CoreText, Omen, OmenPreview } = m;
  let n = 0;
  for (const row of golden.Omens) {
    if (row.Null) {
      assert.deepStrictEqual(omenJson(CoreText.OmenOf(null, Enemies.PolearmWarped)), row.Frame);
      continue;
    }
    const enemy = Enemies.ById(row.Enemy);
    if (row.Rest) {
      assert.deepStrictEqual(omenJson(CoreText.OmenOf(new Omen(EnemyAi.RestActionId, EnemyAi.RestLabel), enemy)), row.Frame, row.Enemy + ' rest');
      continue;
    }
    if (row.Override) {
      const sw = enemy.Switches.find((s) => s.Id === row.Switch);
      const action = sw.Overrides.find((a) => a.Id === row.Override);
      const kind = EnemyAi.LabelOf(action).Kind;
      assert.deepStrictEqual({
        SystemOf: CoreText.SystemOf(action),
        Icon: CoreText.IconOf(kind, action),
        Value: CoreText.OmenValueOf(kind, action, null),
        Statuses: CoreText.OmenStatusesOf(kind, action).map(omenStatusJson),
      }, { SystemOf: row.SystemOf, Icon: row.Icon, Value: row.Value, Statuses: row.Statuses }, `${row.Enemy} ${row.Switch} ${row.Override}`);
      continue;
    }
    const action = enemy.Actions.get(row.Action);
    const omen = new Omen(row.Action, EnemyAi.LabelOf(action));
    const at = `${row.Enemy} ${row.Action}`;
    assert.deepStrictEqual(omenJson(CoreText.OmenOf(omen, enemy)), row.Frame, at);
    assert.deepStrictEqual(omenJson(CoreText.OmenOf(omen, enemy, new OmenPreview(17, 9, true, false))), row.FrameWithPreview, at + ' preview');
    assert.equal(CoreText.SystemOf(action), row.SystemOf, at);
    assert.equal(CoreText.IconOf(omen.Label.Kind, action), row.Icon, at);
    n++;
  }
  assert.ok(n >= 60, `${n} actions compared`);
});

test('golden: every omen kind has its word and icon', opt, () => {
  const { CoreText } = m;
  for (const row of golden.OmenKinds) {
    assert.equal(CoreText.OmenKindWord(row.Kind), row.Word, row.Kind);
    assert.equal(CoreText.IconOf(row.Kind), row.Icon, row.Kind);
  }
});

// ---- golden: stances, chips, misc ----

test('golden: every stance hook × when × amounts reads the C#\'s clause', opt, () => {
  const { CoreText } = m;
  for (const row of golden.Stances) {
    if (row.Null) {
      assert.equal(CoreText.StanceText(null), row.Text);
      continue;
    }
    const stance = syntheticStance(m, row.Hook, row.When, row.Params);
    assert.equal(attempt(() => CoreText.StanceText(stance)), row.Text, `${row.Hook} ${row.When} ${row.Params}`);
  }
});

test('golden: the chips of a unit (CoreText.Chips, both overloads)', opt, () => {
  const { CoreText, StatusSet } = m;
  for (const row of golden.Chips) {
    const entries = [];
    for (let i = 0; i < row.Statuses.length; i += 2) entries.push({ Kind: row.Statuses[i], Stacks: row.Statuses[i + 1] });
    const set = StatusSet.Of(...entries);
    const chips = 'Stances' in row ? CoreText.Chips(set, row.Stances) : row.Stance === '' ? CoreText.Chips(set) : CoreText.Chips(set, row.Stance);
    assert.deepStrictEqual(chips.map(chipJson), row.Chips, JSON.stringify(row.Statuses) + ' ' + (row.Stances ?? row.Stance));
  }
});

test('golden: gap glyph, side, kind words, sides, the boss lines and the errors', opt, () => {
  const { CoreText, Actor, StanceDef } = m;
  const g = golden.Misc;
  for (const row of g.Gaps) {
    assert.equal(CoreText.GapGlyph(row.Gap), row.Glyph);
    assert.equal(CoreText.SideOf(row.Gap), row.Side);
  }
  for (const row of g.Kinds) assert.equal(CoreText.KindWord(row.Kind), row.Word, row.Kind);
  for (const row of g.Attributes) assert.equal(CoreText.KindOf(row.Attr), row.KindOf, String(row.Attr));
  assert.deepStrictEqual([CoreText.Side(Actor.Player), CoreText.Side(Actor.Enemy)], g.Sides);
  assert.equal(CoreText.BoundRefusal('足運び'), g.BoundRefusal);
  assert.equal(CoreText.StanceStoppedLine('岩の構え'), g.StanceStopped);
  assert.equal(CoreText.StanceResumedLine('岩の構え'), g.StanceResumed);
  assert.equal(attempt(() => CoreText.OmenKindWord(999)), g.Errors[0]);
  assert.equal(attempt(() => CoreText.StanceText(new StanceDef(999))), g.Errors[1]);
});

test('golden: the status glyphs and the stopped-move lines of every status word', opt, () => {
  const { CoreText } = m;
  for (const row of golden.Statuses) {
    if (!('Glyph' in row)) continue;
    assert.equal(CoreText.GlyphOf(row.Kind), row.Glyph, row.Kind);
    assert.equal(CoreText.MoveBlockedLine(row.Kind), row.MoveBlocked, row.Kind);
  }
});

// ---- golden: real hands (TurnLoop.Preview) ----

test('golden: the hands of real battles — Face with the preview, ReachesNobody, the two-argument Face', opt, () => {
  let cards = 0;
  for (const row of golden.Battles) {
    const state = battleState(m, row.Setup, row.Deck, row.Gap);
    const got = handOf(m, state);
    const at = `${row.Setup} ${row.Deck} gap ${row.Gap}`;
    assert.deepStrictEqual(got.map((c) => c.Id), row.Hand.map((c) => c.Id), at + ' hand');
    for (let i = 0; i < got.length; i++) {
      assert.equal(got[i].Nobody, row.Hand[i].Nobody, `${at} ${got[i].Id} ReachesNobody`);
      assert.deepStrictEqual(got[i].Face, row.Hand[i].Face, `${at} ${got[i].Id} Face`);
      assert.deepStrictEqual(got[i].FaceTwoArgs, row.Hand[i].FaceTwoArgs, `${at} ${got[i].Id} Face(card, preview)`);
      cards++;
    }
  }
  assert.ok(cards >= 200, `${cards} dealt cards compared`);
});

// ---- CoreVocabularyTests: the eighty, as printed ----

test('TheCardText_ReadsTheNewWords', opt, () => {
  const { CoreText, CardCatalog, StanceDef, StanceHook, StatusKind } = m;
  assert.equal(CoreText.TraitLines(CardCatalog.LastStand), '間合い2以上 +5 ／ 死力 +3');
  assert.equal(CoreText.TraitLines(CardCatalog.WaterStance), '移動後 コスト-1');
  assert.equal(CoreText.TraitLines(CardCatalog.SpiritRoar), '相手出血 脆化+2');
  assert.equal(CoreText.TraitLines(CardCatalog.Resolve), '死力 自分再生+1');
  assert.equal(CoreText.TraitLines(CardCatalog.PurgeFlash), '予兆攻撃 追撃');
  assert.equal(CoreText.TraitLines(CardCatalog.Thrust), '', '素直');
  assert.equal(CoreText.Describe(CardCatalog.Focus.Face, CardCatalog.Focus.Attributes),
    '自分に集中を 2 付与する。自分に強化を 2 付与する。カードを 1 枚引く。');
  assert.equal(CoreText.Describe(CardCatalog.FlatStrike.Face, CardCatalog.FlatStrike.Attributes), '敵に 7 ダメージ。崩し 1。');
  assert.equal(CoreText.Describe(CardCatalog.FirstAid.Face, CardCatalog.FirstAid.Attributes), 'HP を 15 回復する。');
  assert.equal(CoreText.Describe(CardCatalog.RockStance.Face, CardCatalog.RockStance.Attributes), '構え: 毎ターン開始に Guard +3。');
  assert.equal(CoreText.StanceText(CardCatalog.WaterStance.Face.Stance), '間合い 2 以上で始まるターンに回復 +2');
  assert.equal(CoreText.StanceText(CardCatalog.MistStep.Face.Stance), '間合い 2 以上で終えたターンに Guard +3、次の回復 +2');
  assert.equal(CoreText.StanceText(CardCatalog.PriestPrayer.Face.Stance), '被弾のたび（ターン 1 回）スタミナ +1、Guard +3');
  assert.equal(CoreText.StanceText(CardCatalog.IronWall.Face.Stance), '毎ターン 1 回、防御のカードのコスト −1', '#333: 鉄壁の構え');
  assert.equal(CoreText.Describe(CardCatalog.IronWall.Face, CardCatalog.IronWall.Attributes), '構え: 毎ターン 1 回、防御のカードのコスト −1。');
  assert.equal(CoreText.StanceText(new StanceDef(StanceHook.StatusOnAttack, { Status: StatusKind.Bleed, StatusStacks: 1 })),
    'アタックのたび敵に出血 1', '#253: no card carries one yet');
  assert.equal(CoreText.ValueOf(CardCatalog.FirstAid.Face, CardCatalog.FirstAid.Attributes), '15');
});

test('EveryCard_HasTextForEveryFace_AndATraitLine', opt, () => {
  const { CoreText, CardCatalog } = m;
  for (const def of CardCatalog.All) {
    const text = CoreText.Describe(def.Face, def.Attributes);
    assert.notEqual(text, '', def.Id);
    if (def.Face.Stance != null) assert.ok(text.includes('構え: '), def.Id);
    for (const trait of def.AllTraits) {
      const line = CoreText.TraitLine(trait);
      assert.notEqual(line, '', def.Id);
      assert.ok(line.split(' ').length <= 2, def.Id + ': 「条件 効果」');
    }
  }
});

test('AHandCard_PrintsTheCostTheCoreWouldTake', opt, () => {
  // 水の構え (cost 2, 移動後: コスト −1) after a move card: the card prints 1.
  const { CoreText, CardCatalog, CardInstance, TurnLoop, BattleSetup, Enemies, FixedRng } = m;
  const noShuffle = new FixedRng(0.9999999);
  const deck = [new CardInstance('footwork-0', CardCatalog.Footwork), new CardInstance('water_stance-0', CardCatalog.WaterStance)];
  for (let i = 0; i < 18; i++) deck.push(new CardInstance('filler-' + i, CardCatalog.Brace));
  let state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells), noShuffle).State;
  state = TurnLoop.BeginPlayerTurn(state, noShuffle).State;
  const water = state.Hand.find((c) => c.Def === CardCatalog.WaterStance);

  assert.equal(CoreText.Face(water, TurnLoop.Preview(state, water.InstanceId)).Cost, 2);
  state = TurnLoop.PlayCard(state, 'footwork-0', noShuffle).State;
  assert.equal(CoreText.Face(water, TurnLoop.Preview(state, water.InstanceId)).Cost, 1);
});

// ---- CoreOmenIconTests ----

function omenFor(enemy, id) {
  return new m.Omen(id, m.EnemyAi.LabelOf(enemy.Actions.get(id)));
}

test('AnAttackOmen_CarriesTheSword_AndTheCoresNumber', opt, () => {
  const { CoreText, Enemies, OmenPreview, OmenIcon } = m;
  const sweep = omenFor(Enemies.PolearmWarped, 'sweep');
  // 17 is a number no rule produces from the sweep: the writer copies the core's preview.
  const frame = CoreText.OmenOf(sweep, Enemies.PolearmWarped, new OmenPreview(17, 9, true, false));
  assert.equal(frame.Visible, true);
  assert.equal(frame.Icon, OmenIcon.Attack);
  assert.equal(frame.KindLabel, '攻撃');
  assert.equal(frame.ValueText, '17');
  assert.equal(CoreText.OmenOf(sweep, Enemies.PolearmWarped).ValueText, '9', 'the face alone without a preview');
});

test('AGuardOmen_CarriesTheShield_AndTheFaceGuard', opt, () => {
  const { CoreText, Enemies, OmenPreview, OmenIcon } = m;
  const guard = omenFor(Enemies.PolearmWarped, 'guard_up');
  const frame = CoreText.OmenOf(guard, Enemies.PolearmWarped);
  assert.equal(frame.Icon, OmenIcon.Guard);
  assert.equal(frame.KindLabel, '防御');
  assert.equal(frame.ValueText, '3');
  assert.equal(CoreText.OmenOf(guard, Enemies.PolearmWarped, new OmenPreview(0, 0, false, false)).ValueText, '3',
    'an attack preview does not touch a guard\'s number');
});

test('ASkillThatPutsAStatusOnThePlayer_CarriesTheStamp_AndTheStatusInPlaceOfANumber', opt, () => {
  const { CoreText, Enemies, OmenIcon, StatusGlyph } = m;
  const frame = CoreText.OmenOf(omenFor(Enemies.PackAlpha, 'herd'), Enemies.PackAlpha);
  assert.equal(frame.Icon, OmenIcon.Seal);
  assert.equal(frame.KindLabel, '技');
  assert.equal(frame.ValueText, '');
  assert.deepStrictEqual(frame.Statuses.map((s) => s.Label), ['鈍足']);
  assert.deepStrictEqual(frame.Statuses.map((s) => s.Glyph), [StatusGlyph.Slow]);
});

test('ASkillThatPutsNoStatusOnThePlayer_CarriesTheStar_AndNothingInPlaceOfANumber', opt, () => {
  const { CoreText, Enemies, OmenIcon } = m;
  const frame = CoreText.OmenOf(omenFor(Enemies.PackAlpha, 'howl'), Enemies.PackAlpha);
  assert.equal(frame.Icon, OmenIcon.Status);
  assert.equal(frame.KindLabel, '技');
  assert.equal(frame.ValueText, '');
  assert.deepStrictEqual(frame.Statuses, []);
});

test('AStatusWithNoIcon_IsCarriedByName_AndTheSelfStatusIsLeftOut', opt, () => {
  const { CoreText, Enemies, OmenIcon, StatusGlyph, StatusKind, EnumTokens } = m;
  const frame = CoreText.OmenOf(omenFor(Enemies.MiasmaPriest, 'binding_word'), Enemies.MiasmaPriest);
  assert.equal(frame.Icon, OmenIcon.Seal);
  assert.deepStrictEqual(frame.Statuses.map((s) => s.Label), [EnumTokens.ToLabel(StatusKind.Binding), EnumTokens.ToLabel(StatusKind.Intimidate)]);
  assert.deepStrictEqual(frame.Statuses.map((s) => s.Glyph), [StatusGlyph.None, StatusGlyph.Intimidate]);
});

test('AnAttackThatAlsoPutsAStatus_KeepsTheSword_AndItsNumber', opt, () => {
  const { CoreText, Enemies, OmenIcon } = m;
  const frame = CoreText.OmenOf(omenFor(Enemies.ArmoredWarden, 'shield_bash'), Enemies.ArmoredWarden);
  assert.equal(frame.Icon, OmenIcon.Attack);
  assert.notEqual(frame.ValueText, '');
  assert.deepStrictEqual(frame.Statuses, []);
});

test('AtMostTwoStatuses_AreCarried', opt, () => {
  const { CoreText, EnemyActionDef, Face, Reach, StatusGrant, StatusKind, BattleAttribute, OmenKind, StatusGlyph } = m;
  const many = new EnemyActionDef('many', 'many', BattleAttribute.Skill, 1, new Face({
    Reach: Reach.Only(0),
    Statuses: [
      new StatusGrant(StatusKind.Bleed, 1), new StatusGrant(StatusKind.Regen, 1, true),
      new StatusGrant(StatusKind.Slow, 1), new StatusGrant(StatusKind.Fatigue, 1),
    ],
  }));
  assert.deepStrictEqual(CoreText.OmenStatusesOf(OmenKind.Skill, many).map((s) => s.Glyph), [StatusGlyph.Bleed, StatusGlyph.Slow],
    'the self status is skipped; the third foe status is not shown');
});

test('AMoveOmen_ReadsMove_AndShowsNoNumber_EvenWithAGuardFace', opt, () => {
  const { CoreText, Enemies, OmenIcon } = m;
  const frame = CoreText.OmenOf(omenFor(Enemies.PolearmWarped, 'step_forward'), Enemies.PolearmWarped);
  assert.equal(frame.Icon, OmenIcon.Move);
  assert.equal(frame.KindLabel, '移動');
  assert.equal(frame.ValueText, '');
});

test('AStanceOmen_CarriesTheFlag_AndNoNumber', opt, () => {
  const { CoreText, Enemies, OmenIcon } = m;
  const frame = CoreText.OmenOf(omenFor(Enemies.PackAlpha, 'hunt_stance'), Enemies.PackAlpha);
  assert.equal(frame.Icon, OmenIcon.Stance);
  assert.equal(frame.KindLabel, '構え');
  assert.equal(frame.ValueText, '');
});

test('ARestOmen_CarriesTheRestIcon_AndNoNumber', opt, () => {
  const { CoreText, Enemies, EnemyAi, Omen, OmenIcon } = m;
  const frame = CoreText.OmenOf(new Omen(EnemyAi.RestActionId, EnemyAi.RestLabel), Enemies.PolearmWarped);
  assert.equal(frame.Visible, true);
  assert.equal(frame.Icon, OmenIcon.Rest);
  assert.equal(frame.KindLabel, '休み');
  assert.equal(frame.ValueText, '');
});

test('EveryOmenKind_HasAnIconAndAWord', opt, () => {
  const { CoreText, OmenKind, OmenIcon } = m;
  for (const kind of OmenKind.$order) {
    assert.notEqual(CoreText.IconOf(kind), OmenIcon.None, kind);
    assert.notEqual(CoreText.OmenKindWord(kind), '', kind);
  }
  const icons = [CoreText.IconOf(OmenKind.Attack), CoreText.IconOf(OmenKind.Guard), CoreText.IconOf(OmenKind.Skill)];
  assert.equal(new Set(icons).size, 3, 'attack, guard and the status kind are told apart by their icons');
});

// ---- PreviewNumberTests: a face and an omen without the preview sum their blows ----

test('ACardFace_AndAnOmenWithoutThePreview_SumTheBlows', opt, () => {
  const { CoreText, CardDef, CardInstance, Face, StatusGrant, StatusKind, BattleAttribute, Enemies } = m;
  const twinCut = new CardDef('twin_cut', '二段', BattleAttribute.Attack, 1,
    new Face({ Power: 6, Hits: 2, Statuses: [new StatusGrant(StatusKind.Fragile, 1)] }));
  const face = CoreText.Face(new CardInstance('twin_cut-0', twinCut), null);
  const twin = omenFor(Enemies.TwinBladeWarped, 'twin_slash');
  assert.equal(face.ValueText, '12');
  assert.equal(CoreText.OmenOf(twin, Enemies.TwinBladeWarped).ValueText, '16');
  assert.ok(!(face.ValueText + CoreText.OmenOf(twin, Enemies.TwinBladeWarped).ValueText).includes('×'));
});

// ---- ReachHintTests ----

const fiveCards = () => [m.CardCatalog.KesaCut, m.CardCatalog.ReachThrust, m.CardCatalog.GuardThrust, m.CardCatalog.Brace, m.CardCatalog.Feint];

function faceIn(state, def) {
  const { CoreText, TurnLoop } = m;
  const card = state.Hand.find((c) => c.Def === def);
  return CoreText.Face(card, TurnLoop.Preview(state, card.InstanceId), CoreText.ReachesNobody(state, card.InstanceId));
}

function polearmHand(gap) {
  const { BattleSetup, Enemies, Cards, TurnLoop, FixedRng } = m;
  const noShuffle = new FixedRng(0.9999999);
  const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(fiveCards(), 1), BattleSetup.SliceFieldCells, { StartGap: gap });
  return TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, noShuffle).State, noShuffle).State;
}

function twoHounds(gap) {
  const { BattleSetup, Enemies, Cards, TurnLoop, FixedRng } = m;
  const noShuffle = new FixedRng(0.9999999);
  const setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(fiveCards(), 1), 8, { StartGap: gap, MoreEnemies: [Enemies.ShadowHound] });
  return TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, noShuffle).State, noShuffle).State;
}

test('ACardInReach_HasNoHint', opt, () => {
  const { CardCatalog } = m;
  const state = polearmHand(0);
  assert.equal(faceIn(state, CardCatalog.KesaCut).ReachHint, '', '0〜1 reaches a gap of 0');
  assert.equal(faceIn(state, CardCatalog.GuardThrust).ReachHint, '', '0 reaches a gap of 0');
});

test('ACardOutOfReach_SaysTheGapItNeeds', opt, () => {
  const { CardCatalog } = m;
  assert.equal(faceIn(polearmHand(0), CardCatalog.ReachThrust).ReachHint, '相手との間合いが 1〜2 のとき使用可能', '1〜2 cannot reach a gap of 0');
  const far = polearmHand(3);
  assert.equal(faceIn(far, CardCatalog.KesaCut).ReachHint, '相手との間合いが 0〜1 のとき使用可能');
  assert.equal(faceIn(far, CardCatalog.GuardThrust).ReachHint, '相手との間合いが 0 のとき使用可能', 'one gap only reads as Reach.ToText does');
});

test('ASelfCard_HasNoHint_AtAnyGap', opt, () => {
  assert.equal(faceIn(polearmHand(3), m.CardCatalog.Brace).ReachHint, '');
});

test('ACardShortOfStaminaOnly_HasNoHint', opt, () => {
  const { CardCatalog, CoreText, TurnLoop, PlayRefusal } = m;
  let state = twoHounds(0);
  state = state.with({ Player: state.Player.with({ Stamina: 0 }) });
  const kesa = state.Hand.find((c) => c.Def === CardCatalog.KesaCut);
  assert.equal(TurnLoop.CanPlay(state, kesa.InstanceId), PlayRefusal.NotEnoughStamina, 'the card cannot be played');
  assert.equal(CoreText.ReachesNobody(state, kesa.InstanceId), false, 'the reach looks at the gap only');
  assert.equal(faceIn(state, CardCatalog.KesaCut).ReachHint, '');
});

test('TwoEnemies_TheHintShows_OnlyWhenNeitherIsInReach', opt, () => {
  const { CardCatalog, TurnLoop } = m;
  const close = twoHounds(0);
  assert.deepStrictEqual([close.GapTo(0), close.GapTo(1)], [0, 1]);
  const thrust = close.Hand.find((c) => c.Def === CardCatalog.ReachThrust);
  assert.equal(TurnLoop.Preview(close, thrust.InstanceId).InReach, false, 'the first enemy alone is out of 1〜2');
  assert.equal(faceIn(close, CardCatalog.ReachThrust).ReachHint, '', 'the second one stands at 1');
  const far = twoHounds(3);
  assert.deepStrictEqual([far.GapTo(0), far.GapTo(1)], [3, 4]);
  assert.equal(faceIn(far, CardCatalog.KesaCut).ReachHint, '相手との間合いが 0〜1 のとき使用可能');
});

test('TwoEnemies_AFallenOneIsNotAsked', opt, () => {
  const { CardCatalog } = m;
  let state = twoHounds(0);
  state = state.WithEnemy(0, state.Enemies[0].Body.with({ Hp: 0 }));
  assert.equal(faceIn(state, CardCatalog.KesaCut).ReachHint, '');
  assert.equal(faceIn(state, CardCatalog.GuardThrust).ReachHint, '相手との間合いが 0 のとき使用可能');
});

// ---- BossWordTextTests: the lines ----

test('TheBossWords_WriteTheirLines_AndSlowKeepsItsSilence', opt, () => {
  const { CoreText, StatusKind } = m;
  assert.equal(CoreText.MoveBlockedLine(StatusKind.Binding), '呪縛で動けない');
  assert.equal(CoreText.MoveBlockedLine(StatusKind.Hook), '鉤爪で下がれない');
  assert.equal(CoreText.MoveBlockedLine(StatusKind.Slow), '');
  assert.equal(CoreText.BoundRefusal('足運び'), '「足運び」は移動が中心の札です。呪縛が付いている間は出せません');
});

// ---- UnknownValueTests (CoreText's part) ----

test('EveryTraitCondition_AndEffect_HasItsLampWords', opt, () => {
  const { CoreText, Trait, StatusGrant, TraitCondition, TraitEffect, BattleAttribute, OmenKind, StatusKind } = m;
  const full = (c, e) => new Trait(c, e, 2, 1, BattleAttribute.Attack, OmenKind.Attack, StatusKind.Bleed, new StatusGrant(StatusKind.Bleed, 2));
  for (const c of TraitCondition.$order) assert.notEqual(CoreText.ConditionWord(full(c, TraitEffect.PowerBonus)), '', c);
  for (const e of TraitEffect.$order) assert.notEqual(CoreText.EffectWord(full(TraitCondition.GapAtMost, e)), '', e);
});

test('EveryStanceHook_HasAClause', opt, () => {
  const { CoreText, StanceDef, StanceHook, StanceWhen, StatusKind, BattleAttribute } = m;
  for (const hook of StanceHook.$order) {
    const stance = new StanceDef(hook, StanceWhen.Always, 1, 2, 1, 1, 2, 1, StatusKind.Bleed, 1, 1, false, BattleAttribute.Guard);
    assert.notEqual(CoreText.StanceText(stance), '', hook);
  }
});

test('AValueOutsideTheEnums_Throws_NamingTheValue', opt, () => {
  const { CoreText, Trait, TraitCondition, TraitEffect, StanceDef } = m;
  const isRange = (e) => e.name === 'ArgumentOutOfRangeException' && e.message.includes('999');
  assert.throws(() => CoreText.ConditionWord(new Trait(999, TraitEffect.PowerBonus)), isRange);
  assert.throws(() => CoreText.EffectWord(new Trait(TraitCondition.GapAtMost, 999)), isRange);
  assert.throws(() => CoreText.StanceText(new StanceDef(999)), isRange);
  assert.throws(() => CoreText.OmenKindWord(999), isRange);
});
