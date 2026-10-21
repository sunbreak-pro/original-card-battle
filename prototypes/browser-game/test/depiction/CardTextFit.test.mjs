// Tests for src/bridge/depictionScript/CardTextFit.js (port of View/Depiction/Script/CardTextFit.cs).
// Ported cases: Assets/View/Depiction/Tests/CardTextFitTests.cs (all) and
// Assets/View/Depiction/Bridge/Tests/CardFaceFitTests.cs (all: the eighty cards' names, trait lines and
// descriptions in the prefab's boxes), which run on CardCatalog and CoreText and are skipped, naming
// the module, while one is missing.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CardTextFit, TextFit } from '../../src/bridge/depictionScript/CardTextFit.js';
import { CardFace } from '../../src/bridge/depictionScript/DepictionTypes.js';

const f = Math.fround;

async function load(rel) {
  try { return await import(new URL('../../src/' + rel, import.meta.url).href); } catch { return null; }
}
const Catalog = await load('core/battle/CardCatalog.js');
const Text = await load('bridge/depiction/CoreText.js');
const needCards = !Catalog ? 'needs core/battle/CardCatalog.js' : !Text ? 'needs bridge/depiction/CoreText.js' : false;

// Every character one em wide and every line one em high: the arithmetic, not a font.
function Ems(text, size) {
  const lines = text.split('\n');
  let widest = 0;
  for (const line of lines) widest = Math.max(widest, line.length);
  return { width: widest * size, height: lines.length * size };
}

test('AName_TakesTheLargestSizeThatStaysInItsBox', () => {
  const fit = CardTextFit.Name('呼吸を整える', 24, 12, 100, 40, Ems);
  assert.equal(fit.Text, '呼吸を整える', 'a name never breaks');
  assert.equal(fit.Size, 16, '6 × 16 = 96 fits 100, 6 × 17 does not');
  assert.equal(fit.Fits, true);
});

test('AShortName_KeepsThePrefabSize', () => {
  assert.equal(CardTextFit.Name('突き', 24, 12, 100, 40, Ems).Size, 24);
});

test('ANameTooLongForTheSmallestSize_SaysSo_AtTheSmallestSize', () => {
  const fit = CardTextFit.Name('あいうえおかきくけこ', 24, 12, 100, 40, Ems);
  assert.equal(fit.Fits, false);
  assert.equal(fit.Size, 12);
});

test('TwoTraits_BreakBetweenThem_AndOneTrait_BetweenItsConditionAndEffect', () => {
  assert.equal(CardTextFit.TwoLines('間合い2以上 +5 ／ 死力 +3'), '間合い2以上 +5\n死力 +3');
  assert.equal(CardTextFit.TwoLines('間合い0 スタミナ+1'), '間合い0\nスタミナ+1');
  assert.equal(CardTextFit.TwoLines('転換'), null);
});

test('ATraitLine_GoesOnTwoLines_OnlyWhenTheyPrintLarger', () => {
  // One line of 11 fits 110 at 10; two lines of 5 fit 110 × 36 at 18.
  const two = CardTextFit.Trait('あいうえお かきくけこ', 22, 8, 110, 36, Ems);
  assert.equal(two.Text, 'あいうえお\nかきくけこ');
  assert.equal(two.Size, 18);

  // One line of 5 fits at 22 already.
  const one = CardTextFit.Trait('あい うえ', 22, 8, 110, 36, Ems);
  assert.equal(one.Text, 'あい うえ');
  assert.equal(one.Size, 22);
});

test('ATie_KeepsOneLine', () => {
  // One line of 6 fits 108 at 18; two lines of 3 and 2 fit 108 × 36 at 18 as well.
  const fit = CardTextFit.Trait('あいう えお', 22, 8, 108, 36, Ems);
  assert.equal(fit.Text, 'あいう えお');
  assert.equal(fit.Size, 18);
});

// ---- the description (#567) ---------------------------------------------------------------

test('DescriptionRows_ThatFit_KeepTheLargestSize_OneRowPerLine', () => {
  const fit = CardTextFit.Description(['あいう', 'えお'], 16, 12, 100, 40, Ems);
  assert.equal(fit.Text, 'あいう\nえお');
  assert.equal(fit.Size, 16);
  assert.equal(fit.Fits, true);
});

test('ADescriptionRowTooWide_ShrinksBeforeItWraps', () => {
  // 7 × 16 = 112 overflows 100; 7 × 14 = 98 fits on one line.
  const fit = CardTextFit.Description(['あいうえおかき'], 16, 12, 100, 40, Ems);
  assert.equal(fit.Text, 'あいうえおかき');
  assert.equal(fit.Size, 14);
  assert.equal(fit.Fits, true);
});

test('ADescriptionRowTooWideAtTheSmallestSize_Wraps_AtTheLargestSizeThatHoldsTheLines', () => {
  // 10 × 12 = 120 overflows 100 on one line; at 16 six fit a line, and two lines are 32 high.
  const fit = CardTextFit.Description(['あいうえおかきくけこ'], 16, 12, 100, 40, Ems);
  assert.equal(fit.Text, 'あいうえおか\nきくけこ');
  assert.equal(fit.Size, 16);
  assert.equal(fit.Fits, true);
});

test('AWrappedLine_NeverStartsWithPunctuation', () => {
  // Six fit a line at 16; the seventh is 、 so か goes down with it.
  assert.deepEqual(CardTextFit.Wrap(['あいうえおか、きく'], 16, 100, Ems), ['あいうえお', 'か、きく']);
  assert.deepEqual(CardTextFit.Wrap(['あいうえおか。'], 16, 100, Ems), ['あいうえお', 'か。']);
});

test('AWrappedLine_KeepsANumberWhole', () => {
  assert.deepEqual(CardTextFit.Wrap(['あいうえお+12'], 16, 100, Ems), ['あいうえお', '+12']);
});

test('AWrappedLine_KeepsAKatakanaWordWhole', () => {
  assert.deepEqual(CardTextFit.Wrap(['あいうえアタック'], 16, 100, Ems), ['あいうえ', 'アタック']);
});

test('AWrappedLine_DropsTheSpaceItBreaksAt', () => {
  assert.deepEqual(CardTextFit.Wrap(['あいうえお かき'], 16, 100, Ems), ['あいうえお', 'かき']);
});

test('ADescriptionTooLongForTheBox_IsClippedAtItsFoot_AndSaysSo', () => {
  // At 12 eight fit a line and the 15 make two lines, 24 high: only the first fits 20.
  const fit = CardTextFit.Description(['あいうえおかきくけこさしすせそ'], 16, 12, 100, 20, Ems);
  assert.equal(fit.Text, 'あいうえおかきく');
  assert.equal(fit.Size, 12);
  assert.equal(fit.Fits, false);
});

test('DescriptionRows_PutTheLastingMarkAtTheHeadOfEachLastingRow_AndSplitAV42DescriptionBySentence', () => {
  const stance = new CardFace({ Lasting: true, TextLines: ['被ダメージ -2'] });
  assert.deepEqual(CardTextFit.DescriptionRows(stance), ['∞ 被ダメージ -2']);
  assert.equal(CardTextFit.PrintsLasting(stance), true);

  const old = new CardFace({ Description: '敵に 6 ダメージ。自分に 強化 1。' });
  assert.deepEqual(CardTextFit.DescriptionRows(old), ['敵に 6 ダメージ。', '自分に 強化 1。']);
  assert.equal(CardTextFit.PrintsLasting(old), false);
});

// ---- CardFaceFitTests.cs (#210, #567): the eighty cards in the Card prefab's boxes ----

// The Card prefab (DepictionPrefabBuilder): the name box and its size, the trait text's box
// and its size. The trait text starts 40 px in and CardView.traitVisibleRight cuts it at 150.
const NameSize = 24;
const NameWidth = 100;
const NameHeight = 40;
const TraitSize = 22;
const TraitWidth = 150 - 40;
const TraitHeight = 36;
const MinSize = 12;

// A stand-in wider than the prefab's font: a Japanese letter one em, a space 0.3 em, any other
// letter 0.6 em, a line 1.2 em high. C#: `line.Sum(...)` (Enumerable.Sum over floats adds in double
// and casts the total to float), `Math.Max(widest, ems * size)`, `lines.Length * 1.2f * size`.
function StandIn(text, size) {
  const lines = text.split('\n');
  let widest = f(0);
  for (const line of lines) {
    let sum = 0;
    for (let i = 0; i < line.length; i++) {
      const c = line.charCodeAt(i);
      sum += c >= 0x2e80 ? f(1) : c === 0x20 ? f(0.3) : f(0.6);
    }
    const ems = f(sum);
    widest = Math.max(widest, f(ems * size));
  }
  return { width: widest, height: f(f(lines.length * f(1.2)) * size) };
}

test('EveryName_FitsOnOneLine_AboveTheTypeLine', { skip: needCards }, () => {
  const all = Catalog.CardCatalog.All;
  assert.equal(all.length, 80);
  const misfits = [];
  for (const def of all) {
    const fit = CardTextFit.Name(def.Name, NameSize, MinSize, NameWidth, NameHeight, StandIn);
    if (!fit.Fits || fit.Text.includes('\n') || fit.Size < 16) misfits.push(def.Name + ' at ' + fit.Size);
  }
  assert.deepEqual(misfits, []);
});

test('TheLongestNames_ShrinkButStayReadable', { skip: needCards }, () => {
  const all = Catalog.CardCatalog.All;
  const longest = Math.max(...all.map((c) => c.Name.length));
  for (const def of all.filter((c) => c.Name.length === longest)) {
    assert.equal(CardTextFit.Name(def.Name, NameSize, MinSize, NameWidth, NameHeight, StandIn).Size, 16, def.Name);
  }
});

test('EveryTraitLine_FitsBeforeTheRightNeighbour', { skip: needCards }, () => {
  const misfits = [];
  for (const def of Catalog.CardCatalog.All) {
    const line = Text.CoreText.TraitLines(def);
    if (line.length === 0) continue;
    const fit = CardTextFit.Trait(line, TraitSize, MinSize, TraitWidth, TraitHeight, StandIn);
    if (!fit.Fits || fit.Size < 15) misfits.push(def.Name + ' 「' + line + '」 at ' + fit.Size);
  }
  assert.deepEqual(misfits, []);
});

test('LastStand_PrintsItsTwoTraits_OnePerLine', { skip: needCards }, () => {
  const fit = CardTextFit.Trait(Text.CoreText.TraitLines(Catalog.CardCatalog.ById('last_stand')), TraitSize, MinSize, TraitWidth, TraitHeight, StandIn);
  assert.equal(fit.Text, '間合い2以上 +5\n死力 +3');
});

test('BodyCheck_KeepsItsHeavyBlowOnOneLine', { skip: needCards }, () => {
  const fit = CardTextFit.Trait(Text.CoreText.TraitLines(Catalog.CardCatalog.ById('body_check')), TraitSize, MinSize, TraitWidth, TraitHeight, StandIn);
  assert.equal(fit.Text, '間合い0 重撃');
  assert.equal(fit.Size, 18);
});

// #567: the description box (CardView): 188 px wide, three 22 px lines at 16 px (a lasting
// card's four). A line is 22 / 16 of the size high, as the Text's lineSpacing makes it.
const TextSize = 16;
const TextWidth = 216 - 16 - 12;
const TextHeight = 22 * 3;
const LastingHeight = 22 * 4;

function DescriptionStandIn(text, size) {
  const width = StandIn(text.split(CardTextFit.LastingMark).join('　'), size).width;
  // text.Split('\n').Length * (22f / 16f) * size; (22f / 16f) is a float constant.
  const height = f(f(text.split('\n').length * f(22 / 16)) * size);
  return { width, height };
}

function DescriptionOf(def) {
  const face = Text.CoreText.FaceOf(def);
  const height = CardTextFit.PrintsLasting(face) ? LastingHeight : TextHeight;
  return CardTextFit.Description(CardTextFit.DescriptionRows(face), TextSize, MinSize, TextWidth, height, DescriptionStandIn);
}

test('EveryDescription_FitsItsBox_WithNoLineOpeningOnPunctuation', { skip: needCards }, () => {
  const all = Catalog.CardCatalog.All;
  assert.equal(all.length, 80);
  const misfits = [];
  for (const def of all) {
    const fit = DescriptionOf(def);
    if (!fit.Fits) misfits.push(def.Id + ' ' + def.Name + ' 「' + fit.Text.split('\n').join(' / ') + '」');
    for (const line of fit.Text.split('\n')) {
      if (line.length > 0 && CardTextFit.NoLineStart.indexOf(line[0]) >= 0) misfits.push(def.Id + ' opens a line with 「' + line + '」');
    }
  }
  assert.deepEqual(misfits, []);
});

test('TheLongDescriptions_ShrinkOrWrap_AsPinned', { skip: needCards }, () => {
  const ById = (id) => Catalog.CardCatalog.ById(id);
  // Shrunk on one line.
  assert.equal(DescriptionOf(ById('flow_stance')).Size, 12);
  assert.equal(DescriptionOf(ById('wolf_stance')).Size, 13);
  assert.equal(DescriptionOf(ById('twist_away')).Size, 14);
  // Too long for one line even at 12: wrapped at 16 inside a lasting card's four lines.
  const abyss = DescriptionOf(ById('abyss_stance'));
  assert.equal(abyss.Text, '∞ 間合い 0 の相手への\nアタック +5');
  assert.equal(abyss.Size, 16);
  const mist = DescriptionOf(ById('mist_step'));
  assert.equal(mist.Text.split('\n').length, 3);
  assert.equal(mist.Size, 16);
});

// The deck screen prints the catalog with the hand's face (no dealt instance behind it).
test('EveryCatalogCard_HasAFaceForTheDeckScreen_WithItsOwnIdNameAndPrintedCost', { skip: needCards }, () => {
  for (const def of Catalog.CardCatalog.All) {
    const face = Text.CoreText.FaceOf(def);
    assert.equal(face.Id, def.Id, def.Id);
    assert.equal(face.Name, def.Name, def.Id);
    assert.equal(face.Cost, def.Cost, def.Id);
    assert.notEqual(face.TypeLabel, '', def.Id);
    assert.equal(face.TraitLit, false, def.Id);
  }
});

// ---- port-specific (checked against the C# with a float stand-in measure over 120,000 cases) ----

test('Port_TextFitIsAReadonlyStruct', () => {
  const fit = new TextFit('a', 12, true);
  assert.deepEqual([fit.Text, fit.Size, fit.Fits], ['a', 12, true]);
  assert.ok(Object.isFrozen(fit));
});

test('Port_TheMeasureMayAnswerAnArrayOrCsNames', () => {
  const arr = (t, s) => { const m = Ems(t, s); return [m.width, m.height]; };
  const caps = (t, s) => { const m = Ems(t, s); return { Width: m.width, Height: m.height }; };
  assert.equal(CardTextFit.Name('呼吸を整える', 24, 12, 100, 40, arr).Size, 16);
  assert.equal(CardTextFit.Name('呼吸を整える', 24, 12, 100, 40, caps).Size, 16);
});

test('Port_CsTrimAndNullRows', () => {
  // A null lasting row prints the mark alone (C#: "∞" + " " + null).
  assert.deepEqual(CardTextFit.DescriptionRows(new CardFace({ Lasting: true, TextLines: [null, 'x'] })), ['∞ ', '∞ x']);
  // Wrap trims with C#'s Trim (U+3000 is white space in both).
  assert.deepEqual(CardTextFit.Wrap(['　あ　', null], 16, 100, Ems), ['あ', '']);
  // A blank sentence is dropped; the others keep their spaces.
  assert.deepEqual(CardTextFit.DescriptionRows(new CardFace({ Description: '敵に 6 ダメージ。 。。自分に 強化 1。後' })),
    ['敵に 6 ダメージ。', ' 。', '。', '自分に 強化 1。', '後']);
  assert.deepEqual(CardTextFit.DescriptionRows(new CardFace({ Description: null })), []);
});

test('Port_CanBreakBefore_TheRules', () => {
  assert.equal(CardTextFit.CanBreakBefore('あい', 0), false);
  assert.equal(CardTextFit.CanBreakBefore('あい', 2), false);
  assert.equal(CardTextFit.CanBreakBefore('あい', 1), true);
  assert.equal(CardTextFit.CanBreakBefore('あ、', 1), false, '、 never opens a line');
  assert.equal(CardTextFit.CanBreakBefore('（あ', 1), false, '（ never closes a line');
  assert.equal(CardTextFit.CanBreakBefore('アタ', 1), false, 'katakana stays whole');
  assert.equal(CardTextFit.CanBreakBefore('+1', 1), false, 'a number stays whole');
  assert.equal(CardTextFit.CanBreakBefore('ａ1', 1), true, 'a full-width letter is no word char');
  assert.equal(CardTextFit.CanBreakBefore('×−', 1), false);
});
