// Port of unity-project-kit/Assets/View/Exploration/Tests/ExplorationScreenTests.cs (the frame: what each
// node shows, what a step forecasts, and that the forecast is the step) and the frame-side cases of
// ExplorationOverlayTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';
import { RestChoice, RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { JournalContext, JournalTab } from '../../src/bridge/journal/JournalBook.js';
import { ExplorationSession, RunStart } from '../../src/bridge/explorationScript/ExplorationSession.js';
import { ExplorationScreenBuilder } from '../../src/bridge/explorationScript/ExplorationScreenBuilder.js';
import { ExplorationText } from '../../src/bridge/explorationScript/ExplorationText.js';
import { CardKind, NodeStanding, Overlay, ScreenAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { Explorable, SeedWhere, Step, WalkToLayer } from './_explore.mjs';

const sameMembers = (a, b) => assert.deepEqual([...a].sort((x, y) => x - y), [...b].sort((x, y) => x - y));

test('TheFirstFrameIsLayerOneWithTheEntrySpent', () => {
  const session = new ExplorationSession();
  const screen = session.Screen();
  const layer = SevenLayers.Of(1);

  assert.equal(screen.LayerTitle, '第 1 層　燦光の樹海');
  assert.equal(screen.TimeLabel, `刻限 ${layer.TimeLimit - 1} / ${layer.TimeLimit}`);
  assert.equal(screen.Miasma.Label, `瘴気 ${layer.Density}%`);
  assert.equal(screen.HpLabel, 'HP 50 / 50');
  assert.equal(screen.Nodes.length, session.State.Map.NodeCount);
  assert.equal(screen.CurrentNodeId, session.State.Map.EntryId);
});

test('TheEntryBattleCoversTheMapUntilItIsReported', () => {
  const session = new ExplorationSession();
  const first = session.Screen();

  assert.equal(first.Card.Kind, CardKind.Battle);
  assert.equal(first.Card.Title, '戦闘（仮）');
  assert.deepEqual(first.Card.Lines, [
    '探索と戦闘はまだつながっていません。結果を選んで探索へ戻ります。',
    '勝ったことにする: HP とスタミナはそのまま',
    '倒れたことにする: HP 0 でこの生が終わる',
  ]);
  assert.equal(first.MapIsLive, false);
  assert.ok(first.Nodes.every((n) => n.Options.length === 0), 'no step is offered under a card');
  assert.equal(first.Hint, '');
  assert.equal(session.Step(session.State.ReachableUnresolved()[0]), false);

  assert.equal(session.Act(ScreenAction.WinBattle), true);
  const after = session.Screen();
  assert.equal(after.Card.IsShown, false);
  assert.equal(after.MapIsLive, true);
  assert.equal(after.Hint, '明るい枠のノードへ進めます。踏んだノードへは刻限を使わずに移れます。');
});

test('FarNodesShowTheirKindOnly', () => {
  const screen = Explorable(new ExplorationSession()).Screen();
  const far = screen.Nodes.filter((n) => n.Standing === NodeStanding.Far);
  assert.ok(far.length > 0);
  for (const node of far) {
    assert.deepEqual(node.Lines, []);
    assert.deepEqual(node.Options, []);
    assert.ok(node.Glyph.length > 0);
    assert.ok(node.KindLabel.length > 0);
  }
});

test('ReachableNodesShowTheirContentsAndAForecast', () => {
  const session = Explorable(new ExplorationSession());
  const screen = session.Screen();
  const reachable = screen.Nodes.filter((n) => n.Standing === NodeStanding.Reachable);
  sameMembers(reachable.map((n) => n.Id), session.State.ReachableUnresolved());
  for (const node of reachable) {
    assert.ok(node.Lines.length > 0);
    assert.ok(node.Options.length > 0);
    assert.ok(node.Options.every((o) => o.Forecast != null));
  }
});

test('EveryForecastIsTheStepItForetells', () => {
  for (let seed = 1n; seed <= 12n; seed++) {
    const probe = Explorable(new ExplorationSession(new RunStart({ Seed: seed })));
    for (const node of probe.Screen().Nodes.filter((n) => n.Standing === NodeStanding.Reachable)) {
      for (const option of node.Options) {
        const session = Explorable(new ExplorationSession(new RunStart({ Seed: seed })));
        assert.equal(session.Step(node.Id, option.Choice), true);
        const taken = session.State;
        assert.equal(taken.TimeLeft, option.Forecast.TimeAfter);
        assert.equal(taken.MiasmaPercent, option.Forecast.MiasmaAfter);
        assert.equal(taken.MaxStamina, option.Forecast.MaxStaminaAfter);
        assert.equal(taken.Stamina, option.Forecast.StaminaAfter);
        assert.equal(taken.Hp, option.Forecast.HpAfter);
      }
    }
  }
});

test('ARestNodeOffersToRestOrToTrain', () => {
  const session = SeedWhere((s) => s.Screen().Nodes.some((n) => n.Standing === NodeStanding.Reachable && n.Kind === NodeKind.Rest));
  const rest = session.Screen().Nodes.find((n) => n.Standing === NodeStanding.Reachable && n.Kind === NodeKind.Rest);

  assert.deepEqual(rest.Options.map((o) => o.Label), ['休む', '訓練する']);
  assert.deepEqual(rest.Options.map((o) => o.Choice), [RestChoice.Rest, RestChoice.Train]);
  assert.ok(rest.Lines.some((l) => l.startsWith('休む: HP +8')), '15% of 50, rounded half away from zero');
  assert.deepEqual(rest.Lines, [
    '休む: HP +8（最大の 15%）・スタミナ全回復・最大スタミナ +2',
    '訓練する: 選んだ 1 枚に習熟の刻み 1',
    'どちらか 1 つだけを選ぶ',
  ]);

  // Resting lifts max stamina by 2; training leaves it.
  const restForecast = rest.Options[0].Forecast;
  const trainForecast = rest.Options[1].Forecast;
  assert.equal(restForecast.MaxStaminaAfter, trainForecast.MaxStaminaAfter + ExplorationReducer.RestMaxStaminaBonus);
});

test('WalkingBackIsOfferedAndCostsNothing', () => {
  const session = Explorable(new ExplorationSession());
  const entry = session.State.CurrentNodeId;
  const forward = session.State.ReachableUnresolved()[0];
  Step(session, forward);

  const screen = session.Screen();
  const back = screen.Node(entry);
  assert.equal(back.Standing, NodeStanding.Revisit);
  assert.equal(back.Options.length, 1);
  assert.equal(back.Options[0].Label, '移る');
  assert.equal(back.Options[0].Forecast, null);
  assert.ok(back.Note.includes('刻限を使わずに'));
  assert.deepEqual(back.Detail.slice(0, 1), ['踏んだノード。刻限を使わずに移れる'], 'a way in without a forecast adds no line');

  const timeBefore = session.State.TimeLeft;
  assert.equal(session.Step(entry), true);
  assert.equal(session.State.TimeLeft, timeBefore);
});

test('OnlyTheLinksAroundThePlayerAreLit', () => {
  const session = Explorable(new ExplorationSession());
  const screen = session.Screen();
  const here = screen.CurrentNodeId;
  for (const edge of screen.Edges) {
    const touches = edge.From === here || edge.To === here;
    if (!touches) assert.equal(edge.Lit, false);
  }
  assert.equal(screen.Edges.filter((e) => e.Lit).length, session.State.Map.Neighbours(here).length);
});

test('NodesAreLaidOutEntryOnTopBossAtTheBottom', () => {
  const screen = new ExplorationSession().Screen();
  const entry = screen.Node(screen.CurrentNodeId);
  const boss = screen.Nodes.filter((n) => n.Kind === NodeKind.Boss);
  assert.equal(boss.length, 1);
  assert.equal(entry.Y, 1);
  assert.equal(boss[0].Y, 0);
  assert.ok(screen.Nodes.every((n) => n.X > 0 && n.X < 1));
  // C# floats: (Column + 0.5f) / rowWidth and 1f − Row / (RowCount − 1), in single precision.
  for (const n of screen.Nodes) {
    assert.equal(n.X, Math.fround(n.X));
    assert.equal(n.Y, Math.fround(n.Y));
  }
  const middle = screen.Nodes.find((n) => n.Row === 1 && n.Column === 0);
  assert.equal(middle.X, Math.fround(1 / 6));
  assert.equal(middle.Y, Math.fround(0.75));
});

test('TheGaugeForetellsTheNextNewNode', () => {
  const session = Explorable(new ExplorationSession());
  const gauge = session.Screen().Miasma;
  assert.equal(gauge.NextPercent, gauge.Percent + SevenLayers.Of(1).Density);
  assert.equal(gauge.ForecastLabel, `次の 1 歩で +${SevenLayers.Of(1).Density}%`);
  assert.deepEqual(gauge.StepLines, [20, 40, 60, 80]);
});

test('CrossingATwentyPercentLineIsWarned', () => {
  const session = Explorable(new ExplorationSession(new RunStart({ MiasmaPercent: 18 })));
  assert.equal(session.State.MiasmaPercent, 19);

  const node = session.Screen().Nodes.find((n) => n.Standing === NodeStanding.Reachable && n.Kind !== NodeKind.Rest);
  assert.equal(node.Options.length, 1);
  const forecast = node.Options[0].Forecast;

  assert.equal(forecast.MiasmaAfter, 20);
  assert.equal(forecast.MaxStaminaAfter, forecast.MaxStaminaBefore - 1);
  assert.ok(forecast.Warnings.includes('瘴気が 20% を越え、最大スタミナが 10 から 9 に下がります'));
  assert.equal(forecast.IsLethal, false);
  assert.ok(forecast.Lines.includes('最大スタミナ 10 → 9'));
});

test('RestingAcrossALineWarnsOnlyWhenTheMaximumReallyFalls', () => {
  let session = null;
  for (let seed = 1n; seed <= 200n && session == null; seed++) {
    const probe = Explorable(new ExplorationSession(new RunStart({ Seed: seed, MiasmaPercent: 18 })));
    if (probe.Screen().Nodes.some((n) => n.Standing === NodeStanding.Reachable && n.Kind === NodeKind.Rest)) session = probe;
  }
  assert.ok(session != null, 'no seed in 1..200 opens next to a rest node');

  const rest = session.Screen().Nodes.find((n) => n.Standing === NodeStanding.Reachable && n.Kind === NodeKind.Rest);
  const restForecast = rest.Options.find((o) => o.Choice === RestChoice.Rest).Forecast;
  const trainForecast = rest.Options.find((o) => o.Choice === RestChoice.Train).Forecast;

  assert.equal(restForecast.MaxStaminaAfter, 11);
  assert.deepEqual(restForecast.Warnings, []);
  assert.equal(trainForecast.MaxStaminaAfter, 9);
  assert.ok(trainForecast.Warnings.includes('瘴気が 20% を越え、最大スタミナが 10 から 9 に下がります'));
});

test('TheDetailPanelTextComesWholeFromTheScreen', () => {
  const screen = Explorable(new ExplorationSession()).Screen();

  const far = screen.Nodes.find((n) => n.Standing === NodeStanding.Far);
  assert.deepEqual(far.Detail, [ExplorationText.FarNode]);
  assert.equal(far.Title, `${far.Glyph}　${far.KindLabel}`);

  const near = screen.Nodes.find((n) => n.Standing === NodeStanding.Reachable && n.Kind !== NodeKind.Rest);
  assert.equal(near.Detail[0], '入ると刻限を 1 使う');
  assert.ok(near.Detail.some((l) => l.startsWith('【進む】刻限 ')));
  const way = near.Detail.find((l) => l.startsWith('【進む】'));
  assert.ok(way.startsWith('【進む】刻限 9 → 8　瘴気 1% → 2%'), way);
});

test('MaxStaminaIsSpeltOut', () => {
  const session = Explorable(new ExplorationSession(new RunStart({ MiasmaPercent: 40 })));
  assert.equal(session.Screen().MaxStaminaLabel, '最大 8 = 10 − 瘴気 2');
});

test('AStepThatEndsTheLifeWaitsForAConfirmation', () => {
  const session = Explorable(new ExplorationSession(new RunStart({ MiasmaPercent: 98 })));
  const target = session.State.ReachableUnresolved()[0];
  const option = session.Screen().Node(target).Options[0];
  assert.equal(option.NeedsConfirm, true);
  assert.ok(option.Forecast.Warnings.includes('ここで瘴気が 100% に達し、この生が終わります'));

  assert.equal(session.Step(target, option.Choice), true);
  const confirm = session.Screen().Card;
  assert.equal(confirm.Kind, CardKind.ConfirmStep);
  assert.equal(confirm.Title, '瘴気が 100% に達します');
  assert.deepEqual(confirm.Lines, ['このノードへ入ると、瘴気死でこの生が終わります。']);
  assert.deepEqual(confirm.Buttons.map((b) => b.Label), ['それでも進む', 'やめる']);
  assert.notEqual(session.State.CurrentNodeId, target, 'nothing is taken before the confirmation');

  assert.equal(session.Act(ScreenAction.Cancel), true);
  assert.equal(session.Screen().Card.IsShown, false);
  assert.equal(session.State.MiasmaPercent, 99);

  session.Step(target, option.Choice);
  assert.equal(session.Act(ScreenAction.ConfirmStep), true);
  assert.equal(session.State.Phase, RunPhase.MiasmaDeath);

  const end = session.Screen().Card;
  assert.equal(end.Kind, CardKind.MiasmaDeath, '瘴気死 comes before any fight');
  assert.equal(end.Title, '瘴気死');
  assert.equal(end.Lines[0], '瘴気が 100% に達し、第 1 層でこの生が終わった。');
  assert.equal(end.Lines[1], `瘴気 100% ／ HP ${session.State.Hp} / 50`);
  assert.equal(end.Buttons.length, 1);
  assert.equal(end.Buttons[0].Action, ScreenAction.NewLife);
  assert.equal(end.Buttons[0].Label, '新しい生で潜り直す');
});

test('TheLastTimeUnitWarnsOfBeingPushedOut', () => {
  // Layer seven: 刻限 4 over rows 1-3-1. Entry, two middles, then the third middle spends the last unit.
  const session = new ExplorationSession();
  WalkToLayer(session, 7);
  const entry = session.State.CurrentNodeId;
  const middle = session.State.Map.Row(1).map((n) => n.Id);

  Step(session, middle[0]);
  assert.equal(session.Step(entry), true);
  Step(session, middle[1]);
  assert.equal(session.Step(entry), true);

  const last = session.Screen().Node(middle[2]);
  assert.ok(last.Options[0].Forecast.Warnings.includes('刻限が尽き、この層の行動が終わります'));
});

test('ConsumablesShowInTheirSlotsAndCostNoTime', () => {
  const session = Explorable(new ExplorationSession(new RunStart({ Loadout: RunStart.TrialLoadout(), MiasmaPercent: 30 })));
  const screen = session.Screen();

  assert.equal(screen.ToolsLabel, 'ツール: 防瘴の面（1 / 3）');
  assert.equal(screen.Consumables[0].Note, '瘴気の蓄積が 10% 戻る', 'the design-document pointer is not for the player');
  assert.deepEqual(screen.Consumables.map((s) => s.Name), ['浄化の香', '浄化の香', ExplorationText.EmptySlot]);
  assert.equal(screen.Consumables[0].CanUse, true);
  assert.equal(screen.Consumables[2].CanUse, false);

  const time = session.State.TimeLeft;
  const miasma = session.State.MiasmaPercent;
  assert.equal(session.UseConsumable(0), true);
  assert.equal(session.State.TimeLeft, time);
  assert.equal(session.State.MiasmaPercent, miasma - 10);
  assert.equal(session.Screen().Consumables.filter((s) => s.Name === '浄化の香').length, 1);
});

test('TheSameSeedDrawsTheSameScreen', () => {
  const a = new ExplorationSession(new RunStart({ Seed: 42n })).Screen();
  const b = new ExplorationSession(new RunStart({ Seed: 42n })).Screen();
  assert.deepEqual(b.Nodes.map((n) => [n.Id, n.Kind, n.X, n.Y]), a.Nodes.map((n) => [n.Id, n.Kind, n.X, n.Y]));
  assert.deepEqual(b.Edges.map((e) => [e.From, e.To]), a.Edges.map((e) => [e.From, e.To]));
});

test('TheDungeonPageDescribesThisLayer', () => {
  const session = Explorable(new ExplorationSession());
  const book = session.Screen().Journal;

  assert.equal(book.Context, JournalContext.Exploration);
  assert.deepEqual(book.Pages.map((p) => p.Tab), [JournalTab.Enemy, JournalTab.Dungeon, JournalTab.Memo, JournalTab.Terms]);
  assert.ok(book.Pages.every((p) => book.CanOpen(p)), 'every tab opens while exploring');

  const dungeon = book.Pages.find((p) => p.Tab === JournalTab.Dungeon);
  assert.equal(dungeon.Id, 'dungeon-1');
  assert.equal(dungeon.Title, '第 1 層　燦光の樹海');
  assert.deepEqual(dungeon.Rows.map((r) => r.Label), ['瘴気の濃さ', '刻限', 'ノード', '情報収集']);
  assert.equal(dungeon.TextOf(dungeon.Rows[0]), '1 刻限ごとに +1%');
  assert.equal(dungeon.TextOf(dungeon.Rows[1]), '10（寄り道なしの踏破は 5）');
  assert.equal(dungeon.TextOf(dungeon.Rows[2]), '通常戦闘 4・精鋭 1・階層ボス 1・休息 2・情報収集 1・イベント 2');
  assert.equal(dungeon.TextOf(dungeon.Rows[3]), 'まだ');
});

test('ToolsThinTheAir_AndTheDungeonPageSaysByHowMuch', () => {
  const session = Explorable(new ExplorationSession(new RunStart({ Loadout: RunStart.TrialLoadout() })));
  const screen = session.Screen();
  const dungeon = screen.Journal.Pages.find((p) => p.Tab === JournalTab.Dungeon);
  assert.equal(dungeon.TextOf(dungeon.Rows[0]), '1 刻限ごとに +1%（層の濃さ 1 − ツール 1）');
});

test('TheTopBandAndTheStandInPages', () => {
  // ExplorationOverlayTests: NothingIsOpenAtFirst / TheCardsAndSettingsScreensSayWhatTheyAreAndHowToClose / TheJournalEndsOnThePageOfTerms
  const screen = Explorable(new ExplorationSession()).Screen();
  assert.equal(screen.OpenOverlay, Overlay.None);
  assert.equal(screen.CanOpenCards, true);
  assert.equal(screen.CanOpenSettings, true);
  assert.ok(screen.Cards.Title.startsWith('所持カード'));
  assert.equal(screen.Cards.Title, '所持カード（仮）');
  assert.equal(screen.Cards.CloseLabel, '閉じる（C）');
  assert.ok(screen.Settings.Title.startsWith('設定・セーブ'));
  assert.ok(screen.Settings.Lines.some((l) => l.startsWith('保存してゲームを閉じる')));
  assert.equal(screen.Settings.CloseLabel, '閉じる（Esc）');
  assert.equal(screen.Settings.Lines[0], '戦闘の速さ: ×1.0 / ×1.25 / ×1.5（既定は ×1.25）');
  assert.equal(screen.Journal.Pages.at(-1).Tab, JournalTab.Terms);
  assert.equal(screen.Journal.CanOpen(screen.Journal.Pages.at(-1)), true);
  assert.equal(screen.MarksLabel, '訓練の刻み 0');
  assert.equal(screen.StaminaLabel, 'スタミナ 10 / 10');
  assert.equal(screen.ShowsEndLife, true);
  assert.equal(screen.CanEndLife, true);
});

test('TheStandInBattleCard_NamesTheNodesKind', () => {
  const session = Explorable(new ExplorationSession());
  // Walk to the nearest elite: its stand-in battle card says so.
  const map = session.State.Map;
  const elite = map.Nodes.find((n) => n.Kind === NodeKind.Elite);
  const route = [];
  // breadth-first from the entry
  const prev = new Map([[session.State.CurrentNodeId, -1]]);
  const queue = [session.State.CurrentNodeId];
  while (queue.length) {
    const at = queue.shift();
    if (at === elite.Id) break;
    for (const next of map.Neighbours(at)) {
      if (prev.has(next) || (map.Node(next).Kind === NodeKind.Boss)) continue;
      prev.set(next, at);
      queue.push(next);
    }
  }
  for (let at = elite.Id; at !== session.State.CurrentNodeId; at = prev.get(at)) route.unshift(at);
  for (const id of route.slice(0, -1)) Step(session, id);
  assert.equal(session.Step(route.at(-1)), true);
  const card = session.Screen().Card;
  assert.equal(card.Kind, CardKind.Battle);
  assert.equal(card.Title, '精鋭との戦闘（仮）');
  const lines = session.Screen().Node(elite.Id).Lines;
  assert.deepEqual(lines, ['脅威度: 精鋭', '避けて通れる寄り道', '敵: ？']);
});

test('Forecast_IsReducerStepToldAsBeforeAndAfter', () => {
  const session = Explorable(new ExplorationSession());
  const target = session.State.ReachableUnresolved()[0];
  const f = ExplorationScreenBuilder.Forecast(session.State, target, RestChoice.Rest);
  const after = ExplorationReducer.Step(session.State, target, RestChoice.Rest);
  assert.equal(f.TimeAfter, after.TimeLeft);
  assert.equal(f.Lines[0], `刻限 ${session.State.TimeLeft} → ${after.TimeLeft}`);
  assert.equal(f.Lines[1], `瘴気 ${session.State.MiasmaPercent}% → ${after.MiasmaPercent}%`);
});
