// Tests for src/bridge/depiction/StatusChipText.js (port of View/Depiction/Bridge/StatusChipText.cs).
// Golden: every status word at 1〜3 stacks, every stance chip (the catalog's fourteen once and three
// times, unknown names, repeats, blanks), the families, the groups and the errors — byte for byte
// what the C# printed (scratch-csharp/texts → texts.golden.json). Ported cases: CardFaceVisualTests
// (the chips' panel text) and UnknownValueTests (the families).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { golden, load, needs, attempt, chipJson } from './_texts.mjs';

const { m, missing } = await load();
const opt = needs(missing);

test('golden: every status word\'s chip at 1, 2 and 3 stacks', opt, () => {
  const { StatusChipText } = m;
  let n = 0;
  for (const row of golden.Statuses) {
    if (!('Chip' in row)) continue;
    assert.deepStrictEqual(chipJson(StatusChipText.Chip(row.Kind, row.Stacks)), row.Chip, `${row.Kind} ${row.Stacks}`);
    n++;
  }
  assert.equal(n, 16 * 3);
});

test('golden: every status word\'s family and decay', opt, () => {
  const { StatusChipText } = m;
  for (const row of golden.Statuses) {
    if (!('Family' in row)) continue;
    assert.equal(StatusChipText.KindOf(row.Kind), row.Family, row.Kind);
    assert.equal(StatusChipText.Decay(row.Kind), row.Decay, row.Kind);
  }
});

test('golden: the stance chips (StanceChips) of every list', opt, () => {
  const { StatusChipText } = m;
  for (const row of golden.StanceChips) {
    assert.deepStrictEqual(StatusChipText.StanceChips(row.Names).map(chipJson), row.Chips, JSON.stringify(row.Names));
  }
});

test('golden: the groups, the group words and the errors', opt, () => {
  const { StatusChipText, ChipKind } = m;
  assert.deepStrictEqual([StatusChipText.OnFoeGroup, StatusChipText.OnSelfGroup, StatusChipText.LastingGroup, StatusChipText.BossGroup],
    golden.Misc.Groups);
  assert.deepStrictEqual(ChipKind.$order.map((k) => StatusChipText.GroupWord(k)), golden.Misc.GroupWords);
  assert.equal(attempt(() => StatusChipText.KindOf(999)), golden.Misc.Errors[2]);
});

test('golden: a unit\'s chips (Chips): the stances first, then the words in their fixed order', opt, () => {
  const { StatusChipText, StatusSet } = m;
  for (const row of golden.Chips) {
    if (!('Stances' in row)) continue;
    const entries = [];
    for (let i = 0; i < row.Statuses.length; i += 2) entries.push({ Kind: row.Statuses[i], Stacks: row.Statuses[i + 1] });
    assert.deepStrictEqual(StatusChipText.Chips(StatusSet.Of(...entries), row.Stances).map(chipJson), row.Chips, JSON.stringify(row.Statuses));
  }
});

// ---- CardFaceVisualTests: the status chips ----

test('AWordPutOnTheOpponent_IsASquareChip_WithItsEffectAndDecay', opt, () => {
  const { StatusChipText, StatusKind, ChipKind } = m;
  const bleed = StatusChipText.Chip(StatusKind.Bleed, 2);
  assert.equal(bleed.Kind, ChipKind.OnFoe);
  assert.equal(bleed.Title, '出血 2');
  assert.equal(bleed.Group, '相手に付ける系');
  assert.equal(bleed.Effect, 'ターン開始に HP −2×スタック（いまは −4）');
  assert.equal(bleed.Decay, 'ターンごとに 1 減る');
});

test('TheStatusTable_WritesEachPanel', opt, () => {
  const { StatusChipText, StatusKind, ChipKind } = m;
  assert.equal(StatusChipText.Chip(StatusKind.Fragile, 1).Effect, '次に受ける攻撃の威力が 1.5 倍（Guard で引く前の値）');
  assert.equal(StatusChipText.Chip(StatusKind.Fragile, 1).Decay, '効くと 1 減る');
  assert.equal(StatusChipText.Chip(StatusKind.Intimidate, 2).Effect, '次の行動の威力と Guard が −3');
  assert.equal(StatusChipText.Chip(StatusKind.Fatigue, 2).Effect, '次のターン開始のスタミナ回復が −1');
  assert.equal(StatusChipText.Chip(StatusKind.Empower, 2).Effect, '次の攻撃の威力が 1.5 倍');
  assert.equal(StatusChipText.Chip(StatusKind.Regen, 3).Effect, 'ターン開始に HP +2×スタック（いまは +6）');
  assert.equal(StatusChipText.Chip(StatusKind.Parry, 1).Decay, '効くと 1 減る（Guard 0 で受けたときは減らない）');
  assert.equal(StatusChipText.Chip(StatusKind.Regen, 3).Kind, ChipKind.OnSelf);
  assert.equal(StatusChipText.Chip(StatusKind.Regen, 3).Group, '自分に付ける系');
  assert.equal(StatusChipText.Chip(StatusKind.Binding, 1).Kind, ChipKind.Boss);
});

test('EveryStatusWord_HasAPanel', opt, () => {
  const { StatusChipText, StatusKind } = m;
  for (const kind of StatusKind.$order) {
    const chip = StatusChipText.Chip(kind, 1);
    assert.notEqual(chip.Effect, '', kind);
    assert.notEqual(chip.Decay, '', kind);
    assert.notEqual(chip.Group, '', kind);
  }
});

test('TheSameStanceTwice_IsOneLastingChip_ThatSaysItWorksTwice', opt, () => {
  const { StatusChipText, ChipKind } = m;
  const chips = StatusChipText.StanceChips(['岩の構え', '岩の構え']);
  assert.equal(chips.length, 1);
  assert.equal(chips[0].Kind, ChipKind.Lasting);
  assert.equal(chips[0].Title, '岩の構え ×2');
  assert.ok(chips[0].Effect.startsWith('毎ターン開始に Guard +3'));
  assert.equal(chips[0].Decay, '減らない（永続）');
  assert.equal(StatusChipText.StanceChips(['岩の構え'])[0].Title, '岩の構え', 'once prints no ×1');
});

test('AStanceTheCatalogDoesNotKnow_ReadsTheGeneralClause', opt, () => {
  const { StatusChipText } = m;
  const chip = StatusChipText.StanceChip('知らない構え', 1);
  assert.equal(chip.Label, '構え・知らない構え');
  assert.equal(chip.Stacks, 0);
  assert.equal(chip.Effect, '置いたスタンスの効果が、戦闘のあいだ効き続ける');
  assert.equal(StatusChipText.StanceChip('知らない構え', 3).Effect, '置いたスタンスの効果が、戦闘のあいだ効き続ける（3 枚ぶん、3 倍効く）');
});

// ---- UnknownValueTests: the chip families, word by word ----

test('EveryStatusWord_IsInTheFamilyTheTableGivesIt', opt, () => {
  const { StatusChipText, StatusKind, ChipKind } = m;
  const families = new Map([
    [StatusKind.Slow, ChipKind.OnFoe], [StatusKind.Swift, ChipKind.OnSelf], [StatusKind.Bleed, ChipKind.OnFoe],
    [StatusKind.Fragile, ChipKind.OnFoe], [StatusKind.Intimidate, ChipKind.OnFoe], [StatusKind.Fatigue, ChipKind.OnFoe],
    [StatusKind.Empower, ChipKind.OnSelf], [StatusKind.Focus, ChipKind.OnSelf], [StatusKind.Parry, ChipKind.OnSelf],
    [StatusKind.Regen, ChipKind.OnSelf], [StatusKind.MiasmaShroud, ChipKind.Boss], [StatusKind.Binding, ChipKind.Boss],
    [StatusKind.Hook, ChipKind.Boss], [StatusKind.Depths, ChipKind.Boss], [StatusKind.Rooting, ChipKind.Boss],
    [StatusKind.Withering, ChipKind.Boss],
  ]);
  for (const kind of StatusKind.$order) {
    assert.ok(families.has(kind), kind + ' has no row in this table');
    assert.equal(StatusChipText.KindOf(kind), families.get(kind), kind);
    assert.equal(StatusChipText.Chip(kind, 1).Group, StatusChipText.GroupWord(families.get(kind)), kind);
  }
});

test('AValueOutsideTheEnums_Throws', opt, () => {
  const { StatusChipText } = m;
  const isRange = (e) => e.name === 'ArgumentOutOfRangeException' && e.message.includes('999');
  assert.throws(() => StatusChipText.KindOf(999), isRange);
  assert.throws(() => StatusChipText.GroupWord(999), isRange);
  assert.throws(() => StatusChipText.Effect(999, 1), isRange);
});
