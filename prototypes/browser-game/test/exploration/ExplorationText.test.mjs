// ExplorationText: every word the exploration screen prints about a node kind, an item or a number.
// The C# has no test file of its own for it; these pin its strings (ExplorationScreenTests uses them).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ArgumentOutOfRangeException, enumValues } from '../../src/shared/cs.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { ExplorationText } from '../../src/bridge/explorationScript/ExplorationText.js';

test('EveryKindHasAGlyphALabelAndAThreat', () => {
  assert.deepEqual(enumValues(NodeKind).map((k) => ExplorationText.Glyph(k)), ['戦', '精', '主', '休', '調', '事', '痕', '彫']);
  assert.deepEqual(enumValues(NodeKind).map((k) => ExplorationText.KindLabel(k)),
    ['通常戦闘', '精鋭', '階層ボス', '休息', '情報収集', 'イベント', '死亡地点の痕跡', '秘跡の彫り']);
  assert.deepEqual(enumValues(NodeKind).map((k) => ExplorationText.Threat(k)), ['通常', '精鋭', '階層の主', '', '', '', '', '']);
  assert.deepEqual(enumValues(NodeKind).map((k) => ExplorationText.IsCombat(k)), [true, true, true, false, false, false, false, false]);
  assert.throws(() => ExplorationText.Glyph('Nope'), ArgumentOutOfRangeException);
  assert.throws(() => ExplorationText.KindLabel('Nope'), ArgumentOutOfRangeException);
});

test('TheFixedWords', () => {
  assert.equal(ExplorationText.LayerTitle(2, '■■の秘跡'), '第 2 層　■■の秘跡');
  assert.equal(ExplorationText.Change(7, 6), '7 → 6');
  assert.equal(ExplorationText.UnknownEnemy, '？');
  assert.equal(ExplorationText.EmptySlot, '（空き）');
  assert.equal(ExplorationText.JournalButton, '手記（J）');
  assert.equal(ExplorationText.CardsButton, '所持カード（C）');
  assert.equal(ExplorationText.SettingsButton, '設定（Esc）');
  assert.equal(ExplorationText.EndLifeButton, 'この生を終える');
  assert.equal(ExplorationText.FarNode, '遠いノードは種別しか分かりません');
});

test('ItemNamesAndNotes_ComeFromTheCatalogue_WithoutTheDocumentPointers', () => {
  assert.equal(ExplorationText.ItemName('bosho_no_men'), '防瘴の面');
  assert.equal(ExplorationText.ItemName('joka_no_ko'), '浄化の香');
  assert.equal(ExplorationText.ItemName('no_such_item'), 'no_such_item', 'an unknown id prints as itself');
  assert.equal(ExplorationText.ItemNote('joka_no_ko'), '瘴気の蓄積が 10% 戻る');
  assert.equal(ExplorationText.ItemNote('bosho_no_men'), 'この生の間、層の濃度を 1 下げる。下限は 1');
  assert.equal(ExplorationText.ItemNote('ryugo_no_fu'), 'ボス専用の状態が 1 スタック低く乗る');
  assert.equal(ExplorationText.ItemNote('ryurin_no_kakera'), '1 戦につき最初の 1 回だけ Guard 5。防御は Guard だけ（アーマーは凍結）',
    'a bracket without .md stays');
  assert.equal(ExplorationText.ItemNote('no_such_item'), '');
});
