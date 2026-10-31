// ExplorationScreen's plain value types: what the computed members say (IsLethal, NeedsConfirm, Fill,
// IsShown, IsConfirmation, MapIsLive, Node). The frames themselves are ExplorationScreenBuilder's tests.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { RunPhase, RestChoice } from '../../src/core/dungeon/ExplorationState.js';
import {
  CardButton, CardKind, EdgeLine, ExplorationScreen, MiasmaGauge, NodeCard, NodeStanding, Overlay, OverlayPage,
  ScreenAction, ScreenCard, SlotCard, StepForecast, StepOption,
} from '../../src/bridge/explorationScript/ExplorationScreen.js';

const fakeState = (o) => ({ TimeLeft: 9, MiasmaPercent: 1, MaxStamina: 10, Stamina: 10, Hp: 50, Phase: RunPhase.Exploring, ...o });

test('TheEnumsKeepTheCSharpOrder', () => {
  assert.deepEqual(NodeStanding.$order, ['Current', 'Reachable', 'Revisit', 'Resolved', 'Far']);
  assert.deepEqual(ScreenAction.$order, ['WinBattle', 'FallInBattle', 'GoToInterlude', 'Descend', 'AskEndLife', 'ConfirmEndLife', 'ConfirmStep', 'ConfirmDescend', 'Cancel', 'NewLife']);
  assert.deepEqual(CardKind.$order, ['None', 'Battle', 'ConfirmStep', 'ConfirmDescend', 'ConfirmEndLife', 'LayerCleared', 'PushedOut', 'Interlude', 'MiasmaDeath', 'Fallen', 'Completed', 'Survived']);
  assert.deepEqual(Overlay.$order, ['None', 'Journal', 'Cards', 'Settings']);
});

test('AForecast_ReadsBothStates_AndIsLethalOnlyOnMiasmaDeath', () => {
  const before = fakeState({});
  const after = fakeState({ TimeLeft: 8, MiasmaPercent: 2, Stamina: 9, Hp: 49, MaxStamina: 9 });
  const f = new StepForecast(before, after, ['刻限 9 → 8'], []);
  assert.deepEqual(
    [f.TimeBefore, f.TimeAfter, f.MiasmaBefore, f.MiasmaAfter, f.MaxStaminaBefore, f.MaxStaminaAfter, f.StaminaBefore, f.StaminaAfter, f.HpBefore, f.HpAfter],
    [9, 8, 1, 2, 10, 9, 10, 9, 50, 49]);
  assert.equal(f.PhaseAfter, RunPhase.Exploring);
  assert.equal(f.IsLethal, false);
  const lethal = new StepForecast(before, fakeState({ Phase: RunPhase.MiasmaDeath }), [], []);
  assert.equal(lethal.IsLethal, true);

  assert.equal(new StepOption('進む', RestChoice.Rest, f).NeedsConfirm, false);
  assert.equal(new StepOption('進む', RestChoice.Rest, lethal).NeedsConfirm, true);
  assert.equal(new StepOption('移る', RestChoice.Rest, null).NeedsConfirm, false, 'walking back asks nothing');
});

test('TheGaugeFills_AreSinglePrecision', () => {
  const g = new MiasmaGauge(33, 35, '瘴気 33%', '次の 1 歩で +2%', [20, 40, 60, 80]);
  assert.equal(g.Fill, Math.fround(0.33));
  assert.equal(g.NextFill, Math.fround(0.35));
  assert.equal(new MiasmaGauge(0, 0, '', '', []).Fill, 0);
});

test('TheCardOverTheMap', () => {
  assert.equal(ScreenCard.None.IsShown, false);
  assert.equal(ScreenCard.None.Title, '');
  assert.deepEqual(ScreenCard.None.Lines, []);
  assert.equal(ScreenCard.None.IsConfirmation, false);
  for (const kind of [CardKind.ConfirmStep, CardKind.ConfirmDescend, CardKind.ConfirmEndLife]) {
    assert.equal(new ScreenCard(kind, 't', [], []).IsConfirmation, true, kind);
  }
  assert.equal(new ScreenCard(CardKind.Interlude, 't', [], []).IsConfirmation, false);
  assert.equal(new ScreenCard(CardKind.Battle, 't', [], [new CardButton('勝ったことにする', ScreenAction.WinBattle)]).IsShown, true);
});

test('TheScreen_FindsANodeById_AndIsLiveOnlyWithoutACard', () => {
  const node = (id) => new NodeCard({ Id: id, Row: 0, Column: id, Kind: 'Battle' }, 0.5, 1, '戦', '通常戦闘', NodeStanding.Current, [], [], '今いるノード', '戦　通常戦闘', ['今いるノード']);
  const screen = (card) => new ExplorationScreen('第 1 層　燦光の樹海', '刻限 9 / 10', 9, 10,
    new MiasmaGauge(1, 2, '瘴気 1%', '', []), 'HP 50 / 50', 'スタミナ 10 / 10', '最大 10 = 10 − 瘴気 0', '訓練の刻み 0',
    [node(0), node(3)], [new EdgeLine(0, 3, true)], 0, '', 'ツール: なし（0 / 3）', [new SlotCard(0, '（空き）', '', false)],
    true, true, card, null, Overlay.None, true, true, new OverlayPage('所持カード（仮）', [], '閉じる（C）'), new OverlayPage('設定・セーブ（仮）', [], '閉じる（Esc）'));
  const live = screen(ScreenCard.None);
  assert.equal(live.MapIsLive, true);
  assert.equal(live.Node(3).Column, 3);
  assert.equal(live.Node(7), null);
  assert.equal(screen(new ScreenCard(CardKind.Battle, '戦闘（仮）', [], [])).MapIsLive, false);
  const json = JSON.parse(JSON.stringify(live));
  assert.equal(json.$type, 'ExplorationScreen');
  assert.equal(json.Nodes[0].$type, 'NodeCard');
  assert.equal(json.Card.$type, 'ScreenCard');
});
