// Port of unity-project-kit/Assets/View/Exploration/Tests/ExplorationSessionTests.cs (layer ends, the
// interlude, 「この生を終える」, the stand-in battle, a life's end, the sockets for the carving and the
// survey) and ExplorationOverlayTests.cs (the top band's three screens and Esc).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ArgumentOutOfRangeException } from '../../src/shared/cs.js';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { LayerEncounters } from '../../src/core/dungeon/LayerEncounters.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { JournalPage, JournalRow, JournalTab } from '../../src/bridge/journal/JournalBook.js';
import { NodeIntel } from '../../src/bridge/explorationScript/EnemyIntel.js';
import { ExplorationSession, RunStart } from '../../src/bridge/explorationScript/ExplorationSession.js';
import { ExplorationText } from '../../src/bridge/explorationScript/ExplorationText.js';
import { CardKind, NodeStanding, Overlay, ScreenAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { Explorable, RouteTo, Step, WalkToBoss, WalkToLayer } from './_explore.mjs';

const sameEncounter = (a, b) => a.IsStandIn === b.IsStandIn
  && a.Fights.length === b.Fights.length
  && a.Fights.every((f, i) => f.EnemyIds.join('+') === b.Fights[i].EnemyIds.join('+'));

// ---- ExplorationSessionTests ----

test('TheBossLeadsThroughTheInterludeToTheNextLayer', () => {
  const session = new ExplorationSession();
  Explorable(session);
  WalkToBoss(session);

  const cleared = session.Screen().Card;
  assert.equal(cleared.Kind, CardKind.LayerCleared);
  assert.equal(cleared.Title, '階層ボスを倒した');
  assert.equal(cleared.Lines[0], '第 1 層を抜けます。');
  assert.ok(cleared.Lines[1].startsWith('階層間の休憩: HP +'));
  assert.ok(cleared.Lines[1].endsWith('（最大の 30%）・スタミナ全回復・瘴気はたまらない'));
  assert.deepEqual(cleared.Buttons.map((b) => b.Action), [ScreenAction.GoToInterlude]);
  assert.equal(cleared.Buttons[0].Label, '階層間の休憩へ');
  assert.equal(session.Screen().CanEndLife, true, 'the life may be closed at the layer\'s end too');

  assert.equal(session.Act(ScreenAction.GoToInterlude), true);
  const interlude = session.Screen();
  assert.equal(interlude.Card.Kind, CardKind.Interlude);
  assert.equal(interlude.Card.Title, '階層間の休憩');
  assert.deepEqual(interlude.Card.Buttons.map((b) => b.Action), [ScreenAction.Descend, ScreenAction.AskEndLife]);
  assert.deepEqual(interlude.Card.Buttons.map((b) => b.Label), ['次の層へ降りる', 'この生を終える']);
  assert.equal(interlude.Card.Lines.at(-1), '次は第 2 層　■■の秘跡: 1 歩ごとに瘴気 +1%、刻限 10');
  assert.equal(interlude.Miasma.ForecastLabel, '降りると +1%');

  const miasma = session.State.MiasmaPercent;
  assert.equal(session.Act(ScreenAction.Descend), true);
  const below = session.Screen();
  assert.equal(below.LayerTitle, '第 2 層　■■の秘跡');
  assert.equal(session.State.MiasmaPercent, interlude.Miasma.NextPercent, 'the interlude\'s forecast is the descent');
  assert.equal(session.State.MiasmaPercent, miasma + 1);
  assert.equal(below.Card.Kind, CardKind.Battle, 'every layer opens on its entry battle');
});

test('BeingPushedOutOfTheLastLayerLeavesOnlyTheSurvivorRoute', () => {
  const session = new ExplorationSession();
  WalkToLayer(session, 7);
  const entry = session.State.CurrentNodeId;
  const middle = session.State.Map.Row(1).map((n) => n.Id);
  for (const id of middle) {
    Step(session, id);
    if (session.State.Phase !== RunPhase.Exploring) break;
    assert.equal(session.Step(entry), true);
  }

  assert.equal(session.State.Phase, RunPhase.PushedOut);
  const pushed = session.Screen().Card;
  assert.equal(pushed.Kind, CardKind.PushedOut);
  assert.equal(pushed.Title, '刻限が尽きた');
  assert.equal(pushed.Lines[0], 'この層の行動は終わりです。これより下の層はありません。');
  assert.equal(session.Act(ScreenAction.GoToInterlude), true);

  const card = session.Screen().Card;
  assert.equal(card.Kind, CardKind.Interlude);
  assert.deepEqual(card.Buttons.map((b) => b.Action), [ScreenAction.AskEndLife]);
  assert.ok(card.Lines.includes('これより下の層はありません。'));
  assert.equal(session.Act(ScreenAction.Descend), false);
  assert.equal(session.TryDescend(), null);
});

test('EndingTheLifeAsksFirst', () => {
  const session = Explorable(new ExplorationSession());
  const before = session.State;

  assert.equal(session.Act(ScreenAction.AskEndLife), true);
  const ask = session.Screen();
  assert.equal(ask.Card.Kind, CardKind.ConfirmEndLife);
  assert.equal(ask.Card.Title, 'この生を終えますか');
  assert.deepEqual(ask.Card.Lines, ['生きたまま次の生へ移ります。手記は丸ごと次の生へ渡ります。', 'いまの到達: 第 1 層、瘴気 1%']);
  assert.equal(ask.MapIsLive, false);
  assert.equal(ask.CanEndLife, false);

  assert.equal(session.Act(ScreenAction.Cancel), true);
  assert.equal(session.Screen().Card.IsShown, false);
  assert.equal(session.State, before, 'cancelling changes nothing');

  session.Act(ScreenAction.AskEndLife);
  assert.equal(session.Act(ScreenAction.ConfirmEndLife), true);
  assert.equal(session.State.Phase, RunPhase.Survived);

  const end = session.Screen();
  assert.equal(end.Card.Kind, CardKind.Survived);
  assert.equal(end.Card.Title, '生存ルート');
  assert.equal(end.Card.Lines[0], '生きたまま、第 1 層でこの生を閉じた。');
  assert.equal(end.Card.Lines[1], '瘴気 1% ／ HP 50 / 50');
  assert.equal(end.CanEndLife, false);
  assert.equal(end.ShowsEndLife, false, 'the button goes once the life has ended');
  assert.ok(end.Consumables.every((s) => !s.CanUse));
});

test('TheLifeCanBeClosedFromTheInterlude', () => {
  const session = new ExplorationSession();
  WalkToBoss(session);
  session.Act(ScreenAction.GoToInterlude);

  assert.equal(session.Act(ScreenAction.AskEndLife), true);
  assert.equal(session.Screen().Card.Kind, CardKind.ConfirmEndLife);
  assert.equal(session.Act(ScreenAction.Cancel), true);
  assert.equal(session.Screen().Card.Kind, CardKind.Interlude, 'cancelling returns to the interlude');

  session.Act(ScreenAction.AskEndLife);
  session.Act(ScreenAction.ConfirmEndLife);
  assert.equal(session.State.Phase, RunPhase.Survived);
});

test('TheInterludeTellsTheHpItReallyGivesBack', () => {
  const session = new ExplorationSession(new RunStart({ Hp: 20 }));
  WalkToBoss(session);
  const hp = session.State.Hp;
  const expected = Math.min(session.State.MaxHp - hp, ExplorationReducer.InterludeHeal(session.State.MaxHp));

  assert.ok(session.Screen().Card.Lines.some((l) => l.startsWith(`階層間の休憩: HP +${expected}（最大の 30%）`)));
  session.Act(ScreenAction.GoToInterlude);

  assert.equal(session.InterludeHealed, expected);
  const lines = session.Screen().Card.Lines;
  assert.equal(lines[0], `HP +${expected}（最大の 30%）で HP ${hp + expected} / 50`);
  assert.equal(lines[1], `スタミナが全回復した（${session.State.Stamina} / ${session.State.MaxStamina}）`);
  assert.equal(lines[2], `瘴気はたまらない（${session.State.MiasmaPercent}%）`);
});

test('ADescentThatEndsTheLifeIsWarnedAndAsksFirst', () => {
  // 94% + the five nodes of layer one's straight route = 99%; layer two's entry tips it to 100%.
  const session = new ExplorationSession(new RunStart({ MiasmaPercent: 94 }));
  WalkToBoss(session);
  assert.equal(session.State.MiasmaPercent, 99);
  session.Act(ScreenAction.GoToInterlude);

  const interlude = session.Screen();
  assert.ok(interlude.Card.Lines.includes('！ 降りると入口で瘴気が 100% に達し、この生が終わります'));
  assert.equal(interlude.Miasma.NextPercent, 100);

  assert.equal(session.Act(ScreenAction.Descend), true);
  const confirm = session.Screen().Card;
  assert.equal(confirm.Kind, CardKind.ConfirmDescend);
  assert.deepEqual(confirm.Lines, ['次の層へ降りると、入口で瘴気が 100% に達し、この生が終わります。']);
  assert.deepEqual(confirm.Buttons.map((b) => b.Label), ['それでも降りる', 'やめる']);
  assert.equal(session.State.Phase, RunPhase.Interlude, 'nothing is taken before the confirmation');

  assert.equal(session.Act(ScreenAction.Cancel), true);
  assert.equal(session.Screen().Card.Kind, CardKind.Interlude);

  session.Act(ScreenAction.Descend);
  assert.equal(session.Act(ScreenAction.ConfirmDescend), true);
  assert.equal(session.State.Phase, RunPhase.MiasmaDeath);
  assert.equal(session.State.Profile.Layer, 2);
  assert.equal(session.Screen().Card.Kind, CardKind.MiasmaDeath, '瘴気死 comes before the entry battle');
  assert.equal(session.Screen().Card.Lines[0], '瘴気が 100% に達し、第 2 層でこの生が終わった。');
});

test('AConsumableAtTheInterludeCanSaveTheDescent', () => {
  const session = new ExplorationSession(new RunStart({ MiasmaPercent: 94, Loadout: RunStart.TrialLoadout() }));
  WalkToBoss(session);
  session.Act(ScreenAction.GoToInterlude);

  const interlude = session.Screen();
  assert.equal(interlude.Consumables[0].CanUse, true, 'items stay usable while the interlude card is up');
  assert.equal(interlude.CanEndLife, true);

  assert.equal(session.UseConsumable(0), true);
  assert.equal(session.State.MiasmaPercent, 89);
  assert.ok(!session.Screen().Card.Lines.some((l) => l.startsWith('！')));

  assert.equal(session.Act(ScreenAction.Descend), true);
  assert.equal(session.State.Profile.Layer, 2);
  assert.equal(session.State.Phase, RunPhase.Exploring);
});

test('FallingInTheStandInBattleEndsTheLife', () => {
  const session = new ExplorationSession();
  assert.equal(session.Act(ScreenAction.FallInBattle), true);

  assert.equal(session.State.Phase, RunPhase.Fallen);
  assert.equal(session.State.Hp, 0);
  const card = session.Screen().Card;
  assert.equal(card.Kind, CardKind.Fallen);
  assert.equal(card.Title, '倒れた');
  assert.equal(card.Lines[0], '第 1 層の戦いで倒れた。');
});

test('WinningTheStandInBattleChangesNothingButTheCard', () => {
  const session = new ExplorationSession();
  const before = session.State;
  session.Act(ScreenAction.WinBattle);

  assert.equal(session.State.Hp, before.Hp);
  assert.equal(session.State.Stamina, before.Stamina);
  assert.equal(session.State.TimeLeft, before.TimeLeft);
  assert.equal(session.State.MiasmaPercent, before.MiasmaPercent);
});

test('TheEntryBattle_DrawsWhoIsFought_FromTheLayersTable', () => {
  const session = new ExplorationSession(new RunStart({ Seed: 7n }));
  const kind = session.State.Map.Node(session.PendingBattleNode).Kind;

  assert.notEqual(session.PendingEncounter, null);
  assert.ok(LayerEncounters.Of(1).PoolFor(kind).some((e) => sameEncounter(e, session.PendingEncounter)));
  assert.ok(sameEncounter(new ExplorationSession(new RunStart({ Seed: 7n })).PendingEncounter, session.PendingEncounter),
    'the same life meets the same enemy on the same node');
});

test('TheBattlesResult_ComesBackAsHpAndStamina', () => {
  const session = new ExplorationSession();
  assert.equal(session.ReportBattle(31, 4), true);

  assert.equal(session.State.Hp, 31);
  assert.equal(session.State.Stamina, 4);
  assert.equal(session.State.Phase, RunPhase.Exploring);
  assert.equal(session.HasPendingBattle, false);
  assert.equal(session.PendingEncounter, null);
  assert.equal(session.ReportBattle(31, 4), false, 'no battle is waiting');
});

test('HpZero_FromTheBattle_EndsTheLife', () => {
  const session = new ExplorationSession();
  assert.equal(session.ReportBattle(0, 6), true);
  assert.equal(session.State.Phase, RunPhase.Fallen);
});

test('ANewLifeStartsOverFromLayerOneWithTheNextSeed', () => {
  const session = new ExplorationSession(new RunStart({ Seed: 5n }));
  session.Act(ScreenAction.FallInBattle);

  assert.equal(session.Act(ScreenAction.NewLife), true);
  assert.equal(session.Life, 2);
  assert.equal(session.RunSeed, 6n);
  assert.equal(session.State.Profile.Layer, 1);
  assert.equal(session.State.Phase, RunPhase.Exploring);
  assert.equal(session.State.Hp, RunStart.DefaultMaxHp);
  assert.equal(session.State.Map.Fingerprint(),
    SevenLayers.Of(1).Map(ExplorationSession.LayerSeed(6n, 1)).Fingerprint());
});

test('ActionsThatDoNotApplyNowAreRefused', () => {
  const session = new ExplorationSession();

  // Under the entry battle's card only the battle's own buttons work.
  assert.equal(session.Act(ScreenAction.AskEndLife), false);
  assert.equal(session.Act(ScreenAction.GoToInterlude), false);
  assert.equal(session.Act(ScreenAction.NewLife), false);
  assert.equal(session.Act(ScreenAction.ConfirmStep), false);
  assert.equal(session.UseConsumable(0), false);

  session.Act(ScreenAction.WinBattle);
  assert.equal(session.Act(ScreenAction.WinBattle), false, 'no battle is waiting');
  assert.equal(session.Act(ScreenAction.Descend), false, 'descending needs the interlude');
  assert.equal(session.Act(ScreenAction.Cancel), false, 'nothing to cancel');
  assert.equal(session.UseConsumable(0), false, 'the empty loadout has no slot 0');

  const map = session.State.Map;
  const far = map.Nodes.map((n) => n.Id).find((id) => !map.Neighbours(session.State.CurrentNodeId).includes(id) && id !== session.State.CurrentNodeId);
  assert.equal(session.Step(far), false, 'only a neighbour can be walked to');
  assert.throws(() => session.Act('Nope'), ArgumentOutOfRangeException);
});

test('TheCarvingRevealsLayerTwosName', () => {
  const session = new ExplorationSession();
  WalkToLayer(session, SevenLayers.SacramentLayer);
  const map = session.State.Map;
  const carvings = map.Nodes.filter((n) => n.Kind === NodeKind.Carving);
  assert.equal(carvings.length, 1);
  const carving = carvings[0].Id;

  assert.equal(session.Screen().LayerTitle, '第 2 層　■■の秘跡');
  for (const id of RouteTo(map, session.State.CurrentNodeId, carving)) Step(session, id);

  assert.equal(session.CarvingTaken, true);
  assert.equal(session.Screen().LayerTitle, '第 2 層　竜神の秘跡');
  assert.ok(session.Screen().Node(carving).Lines.includes('読んだ。この層の名前が分かった'));
  assert.ok(session.Screen().Node(carving).Lines.includes('戦闘には効かない'));
});

/** An enemy source that knows one enemy and reports the survey it was told about. */
class RecordingIntel {
  ForNode(node, layer, surveyed) {
    return new NodeIntel('歪み兵', surveyed ? 2 : 1, surveyed ? ['傾向: 間合い 1〜2 で突く'] : []);
  }

  Pages(layer, surveyed) {
    return [
      new JournalPage('warped', JournalTab.Enemy, '歪み兵',
        [new JournalRow('傾向', '間合い 1〜2 で突く', 2)],
        surveyed ? 2 : 1),
    ];
  }
}

test('TheSurveyIsHandedToTheEnemySocket', () => {
  const intel = new RecordingIntel();
  const session = new ExplorationSession(null, intel);
  Explorable(session);
  const map = session.State.Map;
  const surveys = map.Nodes.filter((n) => n.Kind === NodeKind.Survey);
  assert.equal(surveys.length, 1);
  const survey = surveys[0].Id;

  const combat = session.Screen().Nodes.find((n) => n.Standing !== NodeStanding.Far && ExplorationText.IsCombat(n.Kind));
  assert.ok(combat.Lines.includes('敵: 歪み兵（開示度 1 / 2）'));

  for (const id of RouteTo(map, session.State.CurrentNodeId, survey)) Step(session, id);
  assert.equal(session.Surveyed, true);

  const screen = session.Screen();
  const anyCombat = screen.Nodes.find((n) => n.Standing !== NodeStanding.Far && ExplorationText.IsCombat(n.Kind));
  assert.ok(anyCombat.Lines.includes('敵: 歪み兵（開示度 2 / 2）'));
  assert.ok(anyCombat.Lines.includes('傾向: 間合い 1〜2 で突く'));
  assert.equal(screen.Journal.Pages[0].Title, '歪み兵');
  const dungeon = screen.Journal.Pages.filter((p) => p.Tab === JournalTab.Dungeon);
  assert.equal(dungeon.length, 1);
  assert.equal(dungeon[0].Rows.at(-1).Text, '済ませた。この層の敵 1 種が開示度 2 になった');
  assert.ok(screen.Node(survey).Lines.includes('この層では済ませた'));
});

test('WithoutEnemyDataTheEnemyIsAQuestionMark', () => {
  const session = new ExplorationSession();
  const entry = session.Screen().Node(session.State.CurrentNodeId);

  assert.ok(entry.Lines.includes('敵: ？'));
  assert.deepEqual(entry.Lines, ['脅威度: 通常', '敵: ？']);
  const enemyPage = session.Screen().Journal.Pages[0];
  assert.equal(enemyPage.Tab, JournalTab.Enemy);
  assert.equal(enemyPage.Disclosure, 0);
  assert.equal(enemyPage.Body, '敵の頁はまだありません。遭遇すると開示度 1 の頁ができます。');
});

test('TheSeedsAreTheCSharpUlongArithmetic', () => {
  // unchecked(runSeed * 0x9E3779B97F4A7C15 + layer), then × 0xBF58476D1CE4E5B9 + (node + 1), mod 2^64.
  const M = (1n << 64n) - 1n;
  assert.equal(ExplorationSession.LayerSeed(1n, 1), (0x9E3779B97F4A7C15n + 1n) & M);
  assert.equal(ExplorationSession.LayerSeed(3n, 2), (3n * 0x9E3779B97F4A7C15n + 2n) & M);
  assert.equal(ExplorationSession.EncounterSeed(3n, 2, 4), ((((3n * 0x9E3779B97F4A7C15n + 2n) & M) * 0xBF58476D1CE4E5B9n) + 5n) & M);
  assert.equal(ExplorationSession.LayerSeed(3, 2), ExplorationSession.LayerSeed(3n, 2), 'a whole number seed reads as the same ulong');
  const wrapped = new ExplorationSession(new RunStart({ Seed: (1n << 64n) - 1n }));
  wrapped.Act(ScreenAction.FallInBattle);
  wrapped.Act(ScreenAction.NewLife);
  assert.equal(wrapped.RunSeed, 0n, 'ulong.MaxValue + 1 wraps to 0');
});

test('TrainingMarks_AreSpentOnlyWhileIdle', () => {
  const session = new ExplorationSession();
  assert.equal(session.SpendTrainingMark(), false, 'a battle waits, and there is no mark');
  Explorable(session);
  assert.equal(session.SpendTrainingMark(), false, 'no mark yet');
});

// ---- ExplorationOverlayTests (session side) ----

test('EachButtonOpensItsScreenAndClosesIt', () => {
  const session = Explorable(new ExplorationSession());
  for (const overlay of [Overlay.Journal, Overlay.Cards, Overlay.Settings]) {
    assert.equal(session.Toggle(overlay), true);
    assert.equal(session.Screen().OpenOverlay, overlay);
    assert.equal(session.Toggle(overlay), true);
    assert.equal(session.Screen().OpenOverlay, Overlay.None);
  }
  assert.equal(session.Toggle(Overlay.None), false);
});

test('OpeningOneScreenClosesTheOther', () => {
  const session = Explorable(new ExplorationSession());
  session.Toggle(Overlay.Journal);
  session.Toggle(Overlay.Cards);
  assert.equal(session.OpenOverlay, Overlay.Cards);
  session.Toggle(Overlay.Settings);
  assert.equal(session.OpenOverlay, Overlay.Settings);
});

test('CardsAndSettingsCoverTheMapButTheJournalDoesNot', () => {
  const session = Explorable(new ExplorationSession());
  const target = session.State.ReachableUnresolved()[0];

  session.Toggle(Overlay.Journal);
  assert.equal(session.MapIsLive, true, 'the drawer sits beside the map');
  assert.ok(session.Screen().Node(target).Options.length > 0);

  for (const overlay of [Overlay.Cards, Overlay.Settings]) {
    session.Toggle(overlay);
    assert.equal(session.MapIsLive, false);
    assert.deepEqual(session.Screen().Node(target).Options, []);
    assert.equal(session.Step(target), false, 'no step is taken under ' + overlay);
  }
});

test('TheStandInBattleShutsCardsAndSettingsButNotTheJournal', () => {
  const session = new ExplorationSession();
  const screen = session.Screen();
  assert.equal(screen.Card.Kind, CardKind.Battle);

  assert.equal(screen.CanOpenCards, false);
  assert.equal(screen.CanOpenSettings, false);
  assert.equal(session.Toggle(Overlay.Cards), false);
  assert.equal(session.Toggle(Overlay.Settings), false);
  assert.equal(session.Escape(), false, 'Esc opens no settings in battle');
  assert.equal(session.Toggle(Overlay.Journal), true);

  session.Act(ScreenAction.WinBattle);
  assert.equal(session.Screen().CanOpenCards, true);
  assert.equal(session.Screen().CanOpenSettings, true);
});

test('EscAnswersAConfirmationFirst', () => {
  const session = Explorable(new ExplorationSession(new RunStart({ MiasmaPercent: 98 })));
  const target = session.State.ReachableUnresolved()[0];
  session.Toggle(Overlay.Journal);
  session.Step(target);
  assert.equal(session.Screen().Card.IsConfirmation, true);

  assert.equal(session.Escape(), true);
  assert.equal(session.Screen().Card.IsShown, false, '「やめる」');
  assert.notEqual(session.State.CurrentNodeId, target);
  assert.equal(session.OpenOverlay, Overlay.Journal, 'the drawer waits for the next Esc');

  assert.equal(session.Escape(), true);
  assert.equal(session.OpenOverlay, Overlay.None);
});

test('EscClosesTheJournalOrTheCardsBeforeItOpensSettings', () => {
  const session = Explorable(new ExplorationSession());
  for (const overlay of [Overlay.Journal, Overlay.Cards]) {
    session.Toggle(overlay);
    assert.equal(session.Escape(), true);
    assert.equal(session.OpenOverlay, Overlay.None, overlay + ' closes, settings stay shut');
  }
  assert.equal(session.Escape(), true);
  assert.equal(session.OpenOverlay, Overlay.Settings);
  assert.equal(session.Escape(), true);
  assert.equal(session.OpenOverlay, Overlay.None);
});

test('TheEndLifeConfirmationIsAnsweredByEscToo', () => {
  const session = Explorable(new ExplorationSession());
  session.Act(ScreenAction.AskEndLife);
  assert.equal(session.Screen().Card.Kind, CardKind.ConfirmEndLife);
  assert.equal(session.Escape(), true);
  assert.equal(session.ConfirmingEndLife, false);
  assert.equal(session.State.Phase, RunPhase.Exploring, 'the life goes on');
});

test('TheInterludeKeepsAllThreeButtons', () => {
  const session = new ExplorationSession();
  WalkToBoss(session);
  session.Act(ScreenAction.GoToInterlude);
  const screen = session.Screen();
  assert.equal(screen.Card.Kind, CardKind.Interlude);
  assert.equal(screen.CanOpenCards, true);
  assert.equal(screen.CanOpenSettings, true);
  assert.equal(session.Escape(), true);
  assert.equal(session.OpenOverlay, Overlay.Settings, 'the interlude is no confirmation');
});

test('RunStart_HasTheCSharpDefaults_AndTheTrialLoadout', () => {
  const start = new RunStart();
  assert.equal(start.Seed, 1n);
  assert.equal(start.MaxHp, 50);
  assert.equal(start.Hp, 50);
  assert.equal(start.Stamina, 10);
  assert.equal(start.MiasmaPercent, 0);
  assert.equal(start.Loadout.Tools.length, 0);
  const trial = RunStart.TrialLoadout();
  assert.deepEqual(trial.Tools.map((t) => [t.Id, t.Kind, t.Amount]), [['bosho_no_men', 'Density', 1]]);
  assert.deepEqual(trial.Consumables.map((t) => [t.Id, t.Kind, t.Amount]), [['joka_no_ko', 'Gauge', 10], ['joka_no_ko', 'Gauge', 10]]);
});
