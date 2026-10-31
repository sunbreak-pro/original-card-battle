// Tests for the world extension 「探索の持ち物とイベント」 (docs/EXTENSIONS.md): trial mode
// (RunStart.Trial / Expedition options.trial) — every tool's and consumable's exploration effect
// (tools_and_consumables_v4.md §2〜§3), battle-won consumables entering the slots with the swap
// (encounters_and_rewards_v4.md §4.3), events with 受ける / 受けない (§5), and that all of it is off by default.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { DeckLimits } from '../../src/core/battle/DeckRules.js';
import { CardInstance, GameResult } from '../../src/core/battle/Types.js';
import { Loadout } from '../../src/core/dungeonContent/Loadout.js';
import { ExplorationReducer, ExplorationRuleException } from '../../src/core/dungeon/ExplorationReducer.js';
import { RestChoice, RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { EventKind, LayerEvents } from '../../src/core/dungeon/LayerEvents.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { ReliefItem, ReliefKind, RunLoadout } from '../../src/core/dungeon/RunLoadout.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { MapGenerator } from '../../src/core/dungeon/MapGenerator.js';
import { Expedition } from '../../src/bridge/exploration/Expedition.js';
import { runLoadoutFromData, runLoadoutToData, toRunLoadout } from '../../src/bridge/exploration/Loadouts.js';
import { ExplorationSession, RunStart } from '../../src/bridge/explorationScript/ExplorationSession.js';
import { CardKind, NodeStanding, ScreenAction, TrialAction, TrialCardKind } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { ExplorationText } from '../../src/bridge/explorationScript/ExplorationText.js';
import { DepictionText } from '../../src/bridge/depictionScript/DepictionTypes.js';
import { recordExpedition, replay } from '../../src/app/recorder.js';
import { toParityJson } from '../../src/shared/cs.js';
import { EndEveryFight } from '../exploration/_battle.mjs';

// ---------------------------------------------------------------- helpers

const SAND = 'kokugen_no_suna';
const BOOK = 'utsushi_no_tobari';
const WICK = 'toshin_no_kakera';
const MASK = 'bosho_no_men';
const FLUTE = 'ikitsugi_no_fue';

/** A trial expedition from a departure's choice, the way the game builds one. */
function trial(seed, tools = [], consumables = [], opts = {}) {
  const loadout = toRunLoadout(Loadout.Of(tools, consumables, Math.max(3, tools.length)), Math.max(3, tools.length));
  const start = new RunStart({ Seed: BigInt(seed), Loadout: loadout, Hp: opts.hp ?? 50, MiasmaPercent: opts.miasma ?? 0 });
  return new Expedition(start, null, null, null, null, null, { trial: true });
}

/** Wins the waiting battle with this HP left (no fight played: the core's end is reported). */
function win(ex, hp = 50, stamina = 5) {
  const battle = ex.StartEncounter();
  assert.notEqual(battle, null);
  EndEveryFight(battle, GameResult.Won, { hp, stamina });
  return ex.Return();
}

/** Clears whatever trial choice waits: swaps leave, events decline, survey picks the first. */
function clearChoices(session) {
  while (session.HasPendingFound) session.Act(TrialAction.LeaveFound);
  if (session.HasPendingEvent) session.Act(TrialAction.DeclineEvent);
  if (session.HasPendingSurvey) session.Act(TrialAction.ChooseSurvey, session.SurveyChoices[0]);
}

/** The nodes from where the player stands to the nearest unresolved node matching `want`, that node last. */
function pathTo(state, want) {
  const map = state.Map;
  const from = new Map([[state.CurrentNodeId, -1]]);
  const queue = [state.CurrentNodeId];
  let target = -1;
  while (queue.length > 0) {
    const node = queue.shift();
    if (want(map.Node(node)) && !state.HasResolved(node)) { target = node; break; }
    for (const next of map.Neighbours(node)) if (!from.has(next)) { from.set(next, node); queue.push(next); }
  }
  assert.notEqual(target, -1, 'a matching node is reachable');
  const path = [];
  for (let node = target; node !== state.CurrentNodeId; node = from.get(node)) path.push(node);
  return path.reverse();
}

/**
 * Walks to the nearest node matching `want`, winning battles and clearing choices on the way (not at the
 * end). `beforeLast(session)` runs right before the last step.
 */
function walkTo(ex, want, choice = RestChoice.Rest, beforeLast = null) {
  const s = ex.Session;
  if (s.HasPendingBattle) win(ex);
  clearChoices(s);
  const path = pathTo(s.State, want);
  for (const node of path) {
    const last = node === path[path.length - 1];
    if (last && beforeLast) beforeLast(s);
    assert.equal(s.Step(node, last ? choice : RestChoice.Rest), true, `step to ${node}`);
    if (s.HasPendingStep) s.Act(ScreenAction.ConfirmStep);
    if (!last) {
      if (s.HasPendingBattle) win(ex);
      clearChoices(s);
    }
  }
  return path[path.length - 1];
}

/** The first seed (1〜200) whose layer-1 map has an event of this kind. */
function seedWithEvent(kind, extra = () => true) {
  for (let seed = 1; seed <= 200; seed++) {
    const ex = trial(seed);
    const s = ex.Session;
    const node = s.State.Map.Nodes.find((n) => n.Kind === NodeKind.Event && s.EventAt(n.Id) === kind);
    if (node && extra(s, node)) return { seed, node: node.Id };
  }
  assert.fail(`no seed has a ${kind}`);
}

// ---------------------------------------------------------------- off by default

test('ByDefault_NothingIsOn_AndTheRunPlaysTheCSharp', () => {
  assert.equal(new RunStart().Trial, false);
  const ex = new Expedition(new RunStart({ Seed: 1n, Loadout: RunStart.TrialLoadout() }));
  assert.equal(ex.Trial, false);
  assert.equal(ex.Session.Trial, false);
  assert.equal(ex.Session.Keeper, null);
  assert.equal(ex.Session.State.Trial, false);
  assert.equal(ex.Session.State.TimeLimit, SevenLayers.Of(1).TimeLimit);
  const back = win(ex);
  // the battle's consumables stay in Expedition.Consumables, the slots are the C#'s
  assert.deepEqual(ex.Session.State.Loadout.Consumables.map((c) => c.Id), ['joka_no_ko', 'joka_no_ko']);
  assert.deepEqual([...ex.Consumables], back.Reward.Consumables);
  assert.equal(ex.Session.HasPendingFound, false);
  // an event node only says what may happen, and stepping on it does nothing
  const screen = ex.Session.Screen();
  const event = screen.Nodes.find((n) => n.Kind === NodeKind.Event && n.Lines.length > 0);
  if (event) assert.deepEqual(event.Lines, ['泉 / 重い宝箱 / 特殊な食事 のどれかが起きる']);
  assert.deepEqual(screen.ToolCards, []);
  assert.equal(screen.TimeBonusLabel, '');
  assert.equal(screen.Aiming, false);
});

test('ACSharpRunLoadout_StillRefusesANonDensityTool_ATrialOneTakesEveryTool', () => {
  assert.throws(() => new RunLoadout([new ReliefItem(SAND, ReliefKind.Time, 1)]), { name: 'ArgumentException' });
  const lo = new RunLoadout([new ReliefItem(SAND, ReliefKind.Time, 1), ReliefItem.Density(MASK, 1), new ReliefItem(FLUTE, ReliefKind.Battle, 3)], null, 3, true);
  assert.equal(lo.Trial, true);
  assert.equal(lo.DensityRelief, 1, 'only the density tools lower the density');
  assert.equal(lo.TimeBonus, 1);
  assert.equal(lo.InterludeBonus, 0);
  assert.equal(lo.ChoosesSurvey, false);
  assert.throws(() => new RunLoadout([ReliefItem.Gauge('joka_no_ko', 10)], null, 3, true), { name: 'ArgumentException' });
});

test('ACSharpSession_BurnsANonGaugeConsumable_WithoutEffect_AsTheCSharpDoes', () => {
  const lo = toRunLoadout(Loadout.Of([], ['yakuso']), 3);
  const s = new ExplorationSession(new RunStart({ Seed: 1n, Loadout: lo, Hp: 20 }));
  s.Act(ScreenAction.WinBattle);
  assert.equal(s.UseConsumable(0), true);
  assert.equal(s.State.Hp, 20);
  assert.equal(s.State.Loadout.Consumables.length, 0);
});

// ---------------------------------------------------------------- the departure's loadout

test('ToRunLoadout_CarriesEveryToolAndConsumable_AsTheKindTheCoreReads', () => {
  const lo = Loadout.Of([MASK, SAND, BOOK, WICK], ['joka_no_ko', 'yakuso', 'shirube_no_ishi'], 4);
  const run = toRunLoadout(lo, 4);
  assert.deepEqual(run.Tools.map((t) => [t.Id, t.Kind, t.Amount]),
    [[MASK, 'Density', 1], [SAND, 'Time', 1], [BOOK, 'Survey', 1], [WICK, 'Interlude', 15]]);
  assert.deepEqual(run.Consumables.map((t) => [t.Id, t.Kind, t.Amount]),
    [['joka_no_ko', 'Gauge', 10], ['yakuso', 'Heal', 20], ['shirube_no_ishi', 'Teleport', 1]]);
  assert.equal(run.ToolSlotCount, 4);
  const battle = toRunLoadout(Loadout.Of(['maai_no_kutsu', 'ikitsugi_no_fue', 'ryurin_no_kakera', 'ryugo_no_fu'], [], 4), 4);
  assert.ok(battle.Tools.every((t) => t.Kind === ReliefKind.Battle));
  assert.equal(battle.DensityRelief, 0);
  // and back through a save
  assert.deepEqual(toParityJson(runLoadoutFromData(runLoadoutToData(run))), toParityJson(run));
  assert.equal(runLoadoutFromData(runLoadoutToData(run)).Trial, true);
});

// ---------------------------------------------------------------- tools (§2)

test('刻限の砂_AddsItsTimeToEveryLayer_AndTheBandSaysSo', () => {
  const ex = trial(1, [SAND]);
  const s = ex.Session;
  assert.equal(s.State.TimeLimit, 11);
  assert.equal(s.State.TimeLeft, 10, 'the entry node took one');
  const screen = s.Screen();
  assert.equal(screen.TimeLabel, '刻限 10 / 11');
  assert.equal(screen.TimeLimit, 11);
  assert.equal(screen.TimeBonusLabel, '刻限の砂 +1');
  // two copies stack (tools_and_consumables_v4.md §6.1, Loadout.TimeLimitBonus)
  assert.equal(trial(1, [SAND, SAND]).Session.State.TimeLimit, 12);
  // the same tool in a C# session does nothing
  const off = new ExplorationSession(new RunStart({ Seed: 1n, Loadout: toRunLoadout(Loadout.Of([SAND]), 3) }));
  assert.equal(off.State.TimeLimit, 10);
});

test('刻限の砂_ReachesTheNextLayer_ThroughTheDescent', () => {
  const ex = trial(2, [SAND]);
  const s = ex.Session;
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex);
  clearChoices(s);
  assert.equal(s.Act(ScreenAction.GoToInterlude), true);
  assert.ok(s.Screen().Card.Lines.some((l) => l.endsWith('刻限 11（刻限の砂 +1）')));
  assert.equal(s.Act(ScreenAction.Descend), true);
  assert.equal(s.State.Profile.Layer, 2);
  assert.equal(s.State.TimeLimit, 11);
  assert.equal(s.State.TimeLeft, 10);
  assert.equal(s.State.Trial, true);
});

test('灯芯の欠片_RaisesTheInterludeHealTo45Percent', () => {
  const ex = trial(2, [WICK]);
  const s = ex.Session;
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex, 10);
  clearChoices(s);
  assert.equal(ExplorationReducer.InterludePercent(s.State), 45);
  assert.ok(s.Screen().Card.Lines.some((l) => l.includes('HP +23（最大の 45%・灯芯の欠片 +15%）')));
  s.Act(ScreenAction.GoToInterlude);
  assert.equal(s.InterludeHealed, 23, '45% of 50, rounded half away from zero');
  assert.equal(s.State.Hp, 33);
  // without the wick, 30%
  const plain = trial(2);
  walkTo(plain, (n) => n.Kind === NodeKind.Boss);
  win(plain, 10);
  clearChoices(plain.Session);
  plain.Session.Act(ScreenAction.GoToInterlude);
  assert.equal(plain.Session.InterludeHealed, 15);
});

test('写しの帳_LetsThePlayerPickTheSurveyTarget_AndTheJournalRaisesIt', () => {
  const ex = trial(3, [BOOK]);
  const s = ex.Session;
  const survey = walkTo(ex, (n) => n.Kind === NodeKind.Survey);
  assert.equal(s.State.Map.Node(survey).Kind, NodeKind.Survey);
  assert.equal(s.HasPendingSurvey, true);
  assert.equal(s.IsIdle, false);
  assert.equal(s.MapIsLive, false);
  const card = s.Screen().Card;
  assert.equal(card.Kind, TrialCardKind.SurveyChoice);
  assert.equal(card.IsChoice, true);
  assert.deepEqual(s.SurveyChoices, ExplorationSession.LayerEnemies(1).filter((id) => ex.DisclosureOf(id) < 2));
  const target = s.SurveyChoices[s.SurveyChoices.length - 1];
  assert.equal(card.Buttons.find((b) => b.Arg === target).Label, `${ex.EnemyName(target)}（開示度 ${ex.DisclosureOf(target)} → 2）`);
  assert.equal(s.Act(TrialAction.ChooseSurvey, 'no_such_enemy'), false);
  assert.equal(s.Act(TrialAction.ChooseSurvey, target), true);
  assert.equal(ex.DisclosureOf(target), 2);
  assert.equal(s.SurveyTarget, target);
  assert.equal(s.MapIsLive, true);
  const row = s.Screen().Journal.Pages.find((p) => p.Id === 'dungeon-1').Rows.find((r) => r.Label === '情報収集');
  assert.equal(row.Text, `済ませた。${ex.EnemyName(target)}が開示度 2 になった（写しの帳）`);
});

test('WithoutTheBook_TheSurveyAsksNothing_TheLayerDecides', () => {
  const ex = trial(3);
  walkTo(ex, (n) => n.Kind === NodeKind.Survey);
  assert.equal(ex.Session.HasPendingSurvey, false);
  assert.equal(ex.Session.Surveyed, true);
  assert.equal(ex.Session.SurveyTarget, null);
});

test('WithoutTheBook_TheIntelsLayerTarget_IsRaisedInTheJournal_InTrialOnly', () => {
  // an intel that names the layer's target (the journal job's JournalIntel.SurveyTarget)
  const intel = { ForNode: () => null, Pages: () => [], SurveyTarget: () => 'shadow_hound' };
  const make = (trialOn) => new Expedition(new RunStart({ Seed: 3n }), null, null, null, intel, null, trialOn ? { trial: true } : null);
  const ex = make(true);
  walkTo(ex, (n) => n.Kind === NodeKind.Survey);
  assert.equal(ex.Session.SurveyTarget, 'shadow_hound');
  assert.equal(ex.Session.SurveyPicked, false);
  assert.equal(ex.DisclosureOf('shadow_hound'), 2);
  const row = ex.Session.Screen().Journal.Pages.find((p) => p.Id === 'dungeon-1').Rows.find((r) => r.Label === '情報収集');
  assert.equal(row.Text, '済ませた。瘴牙の走竜が開示度 2 になった');
  // the C# session leaves the journal to the intel's reports
  const off = make(false);
  if (off.Session.HasPendingBattle) win(off);
  for (const node of pathTo(off.Session.State, (n) => n.Kind === NodeKind.Survey)) {
    off.Session.Step(node);
    if (off.Session.HasPendingBattle) win(off);
  }
  assert.equal(off.Session.SurveyTarget, null);
  assert.ok(off.DisclosureOf('shadow_hound') < 2);
});

test('TheLimits_RideTheHandIn_ToTheLaunch', () => {
  const limits = { DeckMin: 20, DeckMax: 45, ToolSlots: 4, ConsumableSlots: 3 };
  const ex = new Expedition(new RunStart({ Seed: 1n }), null, null, null, null, null, { limits });
  assert.equal(ex.HandIn().Limits, limits);
  assert.equal(ex.StartEncounter().NextLaunch().Limits, limits);
  const plain = new Expedition(new RunStart({ Seed: 1n }));
  assert.equal(plain.HandIn().Limits, null);
  assert.equal('Limits' in plain.StartEncounter().NextLaunch() && plain.Battle.NextLaunch().Limits != null, false);
});

test('ASurvivorsDeckPastForty_ReachesTheBattle_UnderTheLifesLimits', () => {
  // concept-v3.md §14 生存者ボーナス: a survivor's deck may hold 45; W-19 carries the limits to the launch.
  const limits = new DeckLimits({ DeckMin: 20, DeckMax: 45, ToolSlots: 4, ConsumableSlots: 3 });
  const deck = [...PrototypeDeck.Build()];
  let i = 0;
  for (const def of CardCatalog.All) {
    while (deck.length < 41 && deck.filter((c) => c.Def.Id === def.Id).length < 2) deck.push(new CardInstance(`extra-${i++}`, def));
  }
  assert.equal(deck.length, 41);
  const ex = new Expedition(new RunStart({ Seed: 1n }), deck, null, null, null, null, { limits });
  assert.equal(ex.StartEncounter().NextLaunch().BuildDeck().length, 41);
});

test('TheBattleTools_StayBattleOnly_ButShowInTheInventory', () => {
  const ex = trial(1, [FLUTE, MASK]);
  const tools = ex.Session.Screen().ToolCards;
  assert.deepEqual(tools.map((t) => [t.Name, t.Side, t.Effect]), [['息継ぎの笛', '戦闘', ''], ['防瘴の面', '探索', '濃さ −1']]);
  assert.equal(ex.Session.Screen().ToolsLabel, 'ツール: 息継ぎの笛・防瘴の面（2 / 3）');
  assert.equal(ex.Session.State.Loadout.DensityRelief, 1);
  assert.equal(ex.Session.Screen().ReliefLabel, '防瘴の面 濃さは下限 1 のまま');
  const deep = trial(1, [MASK]);
  WalkToLayer(deep, 3);
  assert.equal(deep.Session.Screen().ReliefLabel, '防瘴の面 濃さ 2 → 1');
});

// ---------------------------------------------------------------- consumables (§3)

test('薬草_GivesBackTwentyPercent_AndCannotBeWastedAtFullHp', () => {
  const ex = trial(1, [], ['yakuso']);
  const s = ex.Session;
  win(ex, 50);
  clearChoices(s);
  assert.equal(s.CanUseSlot(0), false, 'full HP: nothing would change');
  assert.equal(s.Screen().Consumables[0].Reason, ExplorationText.NoEffectNow);
  assert.equal(s.UseConsumable(0), false);
  const low = trial(1, [], ['yakuso']);
  win(low, 25);
  clearChoices(low.Session);
  const slot = low.Session.Screen().Consumables[0];
  assert.equal(slot.CanUse, true);
  assert.deepEqual(slot.Forecast, ['HP 25 → 35']);
  assert.equal(low.Session.UseConsumable(0), true);
  assert.equal(low.Session.State.Hp, 35);
  assert.equal(low.Session.State.Loadout.Consumables.length, 0);
});

test('濃い干し肉_RaisesMaxStaminaByOne_InsideTheTemporaryFour', () => {
  const ex = trial(1, [], ['koi_hoshiniku', 'koi_hoshiniku']);
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  const before = s.State.MaxStamina;
  assert.equal(s.UseConsumable(0), true);
  assert.equal(s.State.MaxStamina, before + 1);
  assert.equal(s.State.TempMaxStaminaMod, 1);
  // at +4 already, a second one would change nothing
  s.State = s.State.With({ tempMaxStaminaMod: 4 });
  assert.equal(s.CanUseSlot(0), false);
  assert.equal(ExplorationReducer.UseConsumable(s.State, 0).TempMaxStaminaMod, 4);
});

test('濃い干し肉_OnTheFloorOfThree_ChangesNothingNow_SoItsSlotIsNotLit', () => {
  // temporary −4 (two 重い宝箱) and 瘴気 ≥ 80%: 10 − 4 − 4 = 2 sits on the floor of 3, and +1 still shows 3.
  const ex = trial(3, [], ['koi_hoshiniku'], { miasma: 80 });
  const s = ex.Session;
  win(ex, 50, 2);
  clearChoices(s);
  s.State = s.State.With({ tempMaxStaminaMod: -4 });
  assert.equal(s.State.MaxStamina, 3);
  const slot = s.Screen().Consumables[0];
  assert.equal(slot.CanUse, false);
  assert.equal(slot.Forecast, null);
  assert.equal(slot.Reason, ExplorationText.NoEffectNow);
  assert.equal(s.UseConsumable(0), false);
  assert.equal(s.State.TempMaxStaminaMod, -4, 'nothing spent');
  // one up from there, +1 lifts the floor: lit, with its forecast
  s.State = s.State.With({ tempMaxStaminaMod: -3 });
  const lit = s.Screen().Consumables[0];
  assert.equal(lit.CanUse, true);
  assert.deepEqual(lit.Forecast, [ExplorationText.MaxStaminaChange(3, 4)]);
});

test('ASlot_IsLit_ExactlyWhenItHasAForecast', () => {
  // 試作で決めたこと 1: one definition of 「changes something now」 for CanUseSlot and the slot's forecast.
  let checked = 0;
  for (const carried of [['koi_hoshiniku', 'yakuso', 'wasuremizu'], ['joka_no_ko']]) {
  const ex = trial(3, [], carried);
  const s = ex.Session;
  win(ex, 50, 2);
  clearChoices(s);
  const base = s.State;
  assert.equal(base.Loadout.Consumables.length, carried.length);
  for (const miasma of [0, 5, 20, 59, 80, 95]) {
    for (let temp = -4; temp <= 4; temp++) {
      for (const hp of [1, 45, 50]) {
        for (const stamina of [0, 3, 14]) {
          s.State = base.With({ miasmaPercent: miasma, tempMaxStaminaMod: temp, hp, stamina: Math.min(stamina, base.With({ miasmaPercent: miasma, tempMaxStaminaMod: temp }).MaxStamina) });
          for (const slot of s.Screen().Consumables) {
            assert.equal(slot.CanUse, slot.Forecast !== null, `${slot.Name} at 瘴気 ${miasma}% temp ${temp} HP ${hp}: lit ⇔ forecast`);
            checked++;
          }
        }
      }
    }
  }
  }
  assert.ok(checked > 0);
});

test('忘れ水_FillsCurrentStamina', () => {
  const ex = trial(1, [], ['wasuremizu']);
  const s = ex.Session;
  win(ex, 50, 2);
  clearChoices(s);
  assert.deepEqual(s.Screen().Consumables[0].Forecast, [`スタミナ 2 → ${s.State.MaxStamina}`]);
  assert.equal(s.UseConsumable(0), true);
  assert.equal(s.State.Stamina, s.State.MaxStamina);
});

test('浄化の香_PullsTheGaugeBackTen_AsInTheCSharp', () => {
  const ex = trial(1, [], ['joka_no_ko'], { miasma: 30 });
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  assert.equal(s.UseConsumable(0), true);
  assert.equal(s.State.MiasmaPercent, 21);
});

test('TheConsumables_StayUsableOnTheLayerBreakCard_ButNotDuringABattle', () => {
  const ex = trial(2, [], ['yakuso']);
  const s = ex.Session;
  assert.equal(s.HasPendingBattle, true);
  assert.equal(s.CanUseSlot(0), false, 'a battle waits');
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex, 20);
  clearChoices(s);
  assert.equal(s.State.Phase, RunPhase.LayerCleared);
  assert.equal(s.UseConsumable(0), true);
  assert.equal(s.State.Hp, 30);
});

test('標の石_AimsFirst_ThenJumpsToAnyUnresolvedNode_PayingItsTime', () => {
  const ex = trial(4, [], ['shirube_no_ishi']);
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  const far = s.State.Map.Nodes.find((n) => n.Kind === NodeKind.Rest && !s.State.Map.Neighbours(s.State.CurrentNodeId).includes(n.Id) && !s.State.HasResolved(n.Id))
    ?? s.State.Map.Nodes.find((n) => !s.State.HasResolved(n.Id) && !s.State.Map.Neighbours(s.State.CurrentNodeId).includes(n.Id));
  assert.ok(far, 'a far node');
  const carried = s.State.Loadout.Consumables.length;
  assert.equal(s.UseConsumable(0), true);
  assert.equal(s.IsAiming, true);
  assert.equal(s.State.Loadout.Consumables.length, carried, 'aiming spends nothing yet');
  const screen = s.Screen();
  assert.equal(screen.Aiming, true);
  assert.equal(screen.Hint, ExplorationText.AimHint);
  assert.equal(screen.Consumables[0].Aiming, true);
  const node = screen.Node(far.Id);
  assert.ok(node.Options.length > 0 && node.Options.every((o) => o.Jump));
  assert.equal(node.Note, ExplorationText.JumpNote);
  // the boss is a landing place too (§3 names no exception)
  assert.ok(screen.Node(s.State.Map.BossId).Options.length > 0);
  // a step is refused while aiming; Esc stops aiming
  const near = s.State.Map.Neighbours(s.State.CurrentNodeId).find((id) => !s.State.HasResolved(id));
  assert.equal(s.Step(near), false);
  assert.equal(s.Escape(), true);
  assert.equal(s.IsAiming, false);
  // aim again and land
  s.UseConsumable(0);
  const before = s.State;
  assert.equal(s.Jump(far.Id), true);
  assert.equal(s.State.CurrentNodeId, far.Id);
  assert.equal(s.State.TimeLeft, before.TimeLeft - 1);
  assert.equal(s.State.MiasmaPercent, before.MiasmaPercent + before.EffectiveDensity);
  assert.equal(s.State.Loadout.Consumables.length, carried - 1);
  assert.equal(s.IsAiming, false);
  assert.equal(s.State.HasResolved(far.Id), true);
});

test('標の石_LandingOnABattle_StartsIt_AndALethalJumpAsksFirst', () => {
  const ex = trial(4, [], ['shirube_no_ishi'], { miasma: 98 });
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  const battle = s.State.Map.Nodes.find((n) => n.Kind === NodeKind.Battle && !s.State.HasResolved(n.Id) && n.Id !== s.State.CurrentNodeId);
  s.UseConsumable(0);
  assert.equal(s.Jump(battle.Id), true);
  assert.equal(s.HasPendingStep, true, '99% + 1 reaches 100%');
  assert.equal(s.Screen().Card.Kind, CardKind.ConfirmStep);
  assert.equal(s.Act(ScreenAction.ConfirmStep), true);
  assert.equal(s.State.Phase, RunPhase.MiasmaDeath);
  assert.equal(s.HasPendingBattle, false);

  const ok = trial(4, [], ['shirube_no_ishi']);
  win(ok);
  clearChoices(ok.Session);
  ok.Session.UseConsumable(0);
  assert.equal(ok.Session.Jump(battle.Id), true);
  assert.equal(ok.Session.HasPendingBattle, true);
  assert.equal(ok.Session.PendingBattleNode, battle.Id);
});

test('標の石_CannotJumpOnTheLayerBreak_OrWithNoTimeLeft', () => {
  const ex = trial(2, [], ['shirube_no_ishi']);
  const s = ex.Session;
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex);
  clearChoices(s);
  assert.equal(s.State.Phase, RunPhase.LayerCleared);
  assert.equal(s.CanUseSlot(0), false);
  assert.equal(s.Screen().Consumables[0].Reason, ExplorationText.NoJumpNow);
  assert.throws(() => ExplorationReducer.Teleport(s.State, 0, 1), ExplorationRuleException);
});

test('標の石_AimedAtAFarEvent_ForecastsABareStep_ItsContentsStayHidden', () => {
  // encounters §5 「中身は隣接したときに見えます」 (W-12): the jump's forecast must not tell a far 泉 from a far
  // 重い宝箱 or 特殊な食事 — it counts the 泉's heal only once the node is next door.
  let farSprings = 0;
  let nearSprings = 0;
  for (let seed = 1; seed <= 60; seed++) {
    const ex = trial(seed, [], ['shirube_no_ishi'], { hp: 30 });
    win(ex, 30);
    clearChoices(ex.Session);
    const s = ex.Session;
    assert.equal(s.UseConsumable(0), true);
    const screen = s.Screen();
    for (const node of s.State.Map.Nodes) {
      if (node.Kind !== NodeKind.Event || s.State.HasResolved(node.Id)) continue;
      const card = screen.Node(node.Id);
      const spring = s.EventAt(node.Id) === EventKind.Spring;
      const hpLine = card.Options.some((o) => o.Forecast.Lines.some((l) => l.startsWith('HP ')));
      if (card.Standing === NodeStanding.Far) {
        assert.deepEqual(card.Lines, [], `seed ${seed} node ${node.Id}: a far event shows nothing`);
        assert.equal(hpLine, false, `seed ${seed} node ${node.Id}: a far ${s.EventAt(node.Id)} forecasts as a bare step`);
        assert.equal(card.Detail.some((l) => l.includes('HP ')), false);
        if (spring) farSprings++;
      } else if (spring) {
        assert.equal(hpLine, true, `seed ${seed} node ${node.Id}: a 泉 next door counts its heal`);
        nearSprings++;
      }
    }
  }
  assert.ok(farSprings > 0, 'some far 泉 was aimed at');
  assert.ok(nearSprings > 0, 'some 泉 next door was aimed at');
});

test('標の石_StaysAimed_WhenAnotherSlotIsRefused_AndAUsableOneStopsIt', () => {
  const ex = trial(2, [], ['shirube_no_ishi', 'yakuso']);
  const s = ex.Session;
  win(ex, 50);
  clearChoices(s);
  assert.equal(s.UseConsumable(0), true);
  // 薬草 at full HP changes nothing: refused, and the refusal changes nothing — the stone stays aimed
  assert.equal(s.CanUseSlot(1), false);
  assert.equal(s.UseConsumable(1), false);
  assert.equal(s.IsAiming, true);
  const screen = s.Screen();
  assert.equal(screen.Aiming, true);
  assert.equal(screen.Consumables[0].Aiming, true);
  assert.ok(screen.Nodes.some((n) => n.Options.some((o) => o.Jump)), 'the map still offers the jumps');
  // a slot that can be used stops the aim and acts
  s.State = s.State.With({ hp: 30 });
  const carried = s.State.Loadout.Consumables.length;
  assert.equal(s.UseConsumable(1), true);
  assert.equal(s.IsAiming, false);
  assert.equal(s.State.Hp, 40);
  assert.equal(s.State.Loadout.Consumables.length, carried - 1);
  assert.equal(s.State.Loadout.Consumables[0].Kind, ReliefKind.Teleport, 'the stone is still carried');
});

test('標の石_EndingTheLife_StopsTheAim', () => {
  const ex = trial(2, [], ['shirube_no_ishi']);
  const s = ex.Session;
  win(ex, 50);
  clearChoices(s);
  const carried = s.State.Loadout.Consumables.length;
  assert.equal(s.UseConsumable(0), true);
  assert.equal(s.Act(ScreenAction.AskEndLife), true);
  assert.equal(s.IsAiming, false, 'the confirmation covers the map, so the aim stops');
  assert.equal(s.Act(ScreenAction.Cancel), true);
  assert.equal(s.IsAiming, false);
  assert.equal(s.UseConsumable(0), true);
  assert.equal(s.Act(ScreenAction.AskEndLife), true);
  assert.equal(s.Act(ScreenAction.ConfirmEndLife), true);
  assert.equal(s.State.Phase, RunPhase.Survived);
  const screen = s.Screen();
  assert.equal(s.IsAiming, false);
  assert.equal(screen.Aiming, false);
  assert.equal(screen.Consumables[0].Aiming, false);
  assert.equal(screen.Consumables[0].CanUse, false);
  assert.equal(s.State.Loadout.Consumables.length, carried, 'nothing was spent');
});

// ---------------------------------------------------------------- battle rewards into the slots (§4.3)

/** A seed whose entry battle drops a consumable. */
function seedWithConsumableReward() {
  for (let seed = 1; seed <= 200; seed++) {
    const ex = trial(seed);
    if (win(ex).Reward.Consumables.length > 0) return seed;
  }
  assert.fail('no seed drops a consumable at the entry');
}

test('ABattlesConsumable_GoesIntoAFreeSlot', () => {
  const seed = seedWithConsumableReward();
  const ex = trial(seed, [], ['yakuso']);
  const back = win(ex);
  assert.deepEqual(ex.Session.State.Loadout.Consumables.map((c) => c.Id), ['yakuso', ...back.Reward.Consumables]);
  assert.equal(ex.Session.HasPendingFound, false);
  assert.deepEqual([...ex.Consumables], back.Reward.Consumables, 'the C# record of finds is kept');
});

test('WithTheSlotsFull_TheFindWaitsOnASwapOrOnGivingUp', () => {
  const seed = seedWithConsumableReward();
  const ex = trial(seed, [], ['yakuso', 'wasuremizu', 'koi_hoshiniku']);
  const s = ex.Session;
  const back = win(ex);
  const found = back.Reward.Consumables[0];
  assert.deepEqual(s.PendingFound, back.Reward.Consumables);
  assert.equal(s.IsIdle, false);
  assert.equal(s.MapIsLive, false);
  const card = s.Screen().Card;
  assert.equal(card.Kind, TrialCardKind.Swap);
  assert.deepEqual(card.Buttons.map((b) => [b.Label, b.Action, b.Arg]), [
    ['薬草と入れ替える', TrialAction.TakeFound, 0],
    ['忘れ水と入れ替える', TrialAction.TakeFound, 1],
    ['濃い干し肉と入れ替える', TrialAction.TakeFound, 2],
    ['諦める', TrialAction.LeaveFound, null],
  ]);
  assert.equal(s.Act(TrialAction.TakeFound, 7), false, 'no such slot');
  assert.equal(s.Act(TrialAction.TakeFound, 1), true);
  assert.deepEqual(s.State.Loadout.Consumables.map((c) => c.Id), ['yakuso', found, 'koi_hoshiniku']);
  assert.equal(s.HasPendingFound, false);
  assert.equal(s.MapIsLive, true);

  const other = trial(seed, [], ['yakuso', 'wasuremizu', 'koi_hoshiniku']);
  win(other);
  assert.equal(other.Session.Act(TrialAction.LeaveFound), true);
  assert.deepEqual(other.Session.State.Loadout.Consumables.map((c) => c.Id), ['yakuso', 'wasuremizu', 'koi_hoshiniku']);
  assert.equal(other.Session.Act(TrialAction.LeaveFound), false, 'nothing waits any more');
});

// ---------------------------------------------------------------- events (§5)

test('TheEventTable_KeepsTheDocumentsWeightsAndContents', () => {
  assert.deepEqual(LayerEvents.Weights(1), [50, 20, 30]);
  assert.deepEqual(LayerEvents.Weights(2), [40, 30, 30]);
  assert.deepEqual(LayerEvents.Weights(3), [30, 40, 30]);
  assert.deepEqual(LayerEvents.Weights(7), [0, 70, 30]);
  for (let layer = 1; layer <= 7; layer++) assert.equal(LayerEvents.Weights(layer).reduce((a, b) => a + b), 100);
  assert.deepEqual([1, 2, 3, 7].map((l) => LayerEvents.SpringPercent(l)), [20, 25, 30, 30]);
  assert.deepEqual(LayerEvents.ChestContents(1), { Cards: 1, Consumables: 1, Pages: 0 });
  assert.deepEqual(LayerEvents.ChestContents(2), { Cards: 1, Consumables: 0, Pages: 1 });
  assert.deepEqual(LayerEvents.ChestContents(3), { Cards: 1, Consumables: 1, Pages: 1 });
  assert.deepEqual(LayerEvents.ChestContents(7), LayerEvents.ChestContents(3));
  assert.equal(LayerEvents.HasChoice(EventKind.Spring), false);
  assert.equal(LayerEvents.HasChoice(EventKind.HeavyChest), true);
  assert.equal(LayerEvents.HasChoice(EventKind.StrangeMeal), true);
});

test('TheTwoEventsOfALayer_NeverRepeatAKind_AndLayerSevenNeverDrawsASpring', () => {
  const counts = { Spring: 0, HeavyChest: 0, StrangeMeal: 0 };
  for (let seed = 1; seed <= 300; seed++) {
    for (const layer of [1, 2, 3]) {
      const map = SevenLayers.Of(layer).Map(BigInt(seed));
      const kinds = [...LayerEvents.Draw(map, layer, (id) => ExplorationSession.EventSeed(BigInt(seed), layer, id)).values()];
      assert.equal(kinds.length, 2);
      assert.notEqual(kinds[0], kinds[1], `layer ${layer} seed ${seed}`);
      if (layer === 1) for (const k of kinds) counts[k]++;
    }
    const map7 = SevenLayers.Of(7).Map(BigInt(seed));
    const k7 = [...LayerEvents.Draw(map7, 7, (id) => ExplorationSession.EventSeed(BigInt(seed), 7, id)).values()];
    assert.equal(k7.length, 1);
    assert.notEqual(k7[0], EventKind.Spring);
  }
  // with layer one's weights, the spring is the commonest
  assert.ok(counts.Spring > counts.StrangeMeal && counts.StrangeMeal > counts.HeavyChest, JSON.stringify(counts));
});

test('TheDraw_IsSeededFromTheNode_SoALifeReplaysTheSameEvents', () => {
  const a = trial(9).Session;
  const b = trial(9).Session;
  const ids = a.State.Map.Nodes.filter((n) => n.Kind === NodeKind.Event).map((n) => n.Id);
  assert.deepEqual(ids.map((id) => a.EventAt(id)), ids.map((id) => b.EventAt(id)));
  assert.equal(a.EventAt(a.State.Map.EntryId), null);
  assert.equal(ExplorationSession.EventSeed(9n, 1, 4), ExplorationSession.EncounterSeed(9n, 1, 4) ^ 0x9E6C63D0676A9A99n);
});

test('AnEventsContents_ShowOnceItIsNextDoor_NotWhileFar', () => {
  // a chest that is not next to the entry
  const { seed, node } = seedWithEvent(EventKind.HeavyChest, (s, n) => !s.State.Map.Neighbours(s.State.CurrentNodeId).includes(n.Id));
  const ex = trial(seed);
  win(ex);
  clearChoices(ex.Session);
  const screen = ex.Session.Screen();
  const card = screen.Node(node);
  assert.equal(card.Standing, 'Far');
  assert.deepEqual(card.Lines, []);
  walkTo(ex, (n) => n.Id !== node && ex.Session.State.Map.Neighbours(n.Id).includes(node));
  clearChoices(ex.Session);
  if (ex.Session.HasPendingBattle) win(ex);
  clearChoices(ex.Session);
  const near = ex.Session.Screen().Node(node);
  assert.deepEqual(near.Lines, ['重い宝箱: 最大スタミナ −2（一時的な変化）', '中身: カード 1 枚 + 消耗品 1 個', ExplorationText.EventChoiceNote]);
});

test('泉_HealsOnArrival_ByTheLayersShare_AndIsRecorded', () => {
  const { seed, node } = seedWithEvent(EventKind.Spring);
  const ex = trial(seed);
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  walkTo(ex, (n) => n.Id === node, RestChoice.Rest, (session) => { session.State = session.State.With({ hp: 20 }); });
  assert.equal(s.HasPendingEvent, false, 'a spring asks nothing');
  assert.equal(s.State.Hp, 30, '20% of 50');
  const last = s.Events[s.Events.length - 1];
  assert.deepEqual([last.Kind, last.Name, last.Accepted, last.Lines], [EventKind.Spring, '泉', true, ['HP 20 → 30']]);
  const row = s.Screen().Journal.Pages.find((p) => p.Id === 'dungeon-1').Rows.find((r) => r.Label === '出来事');
  assert.equal(row.Text, '泉: HP 20 → 30');
});

test('重い宝箱_Accepted_CostsTwoMaxStamina_AndGivesACardAConsumableAndNoPageOnLayerOne', () => {
  const { seed, node } = seedWithEvent(EventKind.HeavyChest);
  const ex = trial(seed);
  const s = ex.Session;
  win(ex, 50, 9);
  clearChoices(s);
  walkTo(ex, (n) => n.Id === node);
  assert.equal(s.HasPendingEvent, true);
  assert.equal(s.IsIdle, false);
  const card = s.Screen().Card;
  assert.equal(card.Kind, TrialCardKind.EventChoice);
  assert.deepEqual(card.Buttons.map((b) => [b.Label, b.Action]), [['受ける', TrialAction.AcceptEvent], ['受けない', TrialAction.DeclineEvent]]);
  const before = s.State;
  const pages = ex.Pages;
  assert.equal(s.Act(TrialAction.AcceptEvent), true);
  assert.equal(s.State.TempMaxStaminaMod, before.TempMaxStaminaMod - 2);
  assert.equal(s.State.MaxStamina, before.MaxStamina - 2);
  assert.ok(s.State.Stamina <= s.State.MaxStamina, 'current stamina follows the lower maximum');
  assert.equal(s.State.TimeLeft, before.TimeLeft, 'the 刻限 was paid by the step');
  assert.equal(ex.Offer.length, 3, 'the card pile lays out three');
  assert.equal(ex.Pages, pages);
  const last = s.Events[s.Events.length - 1];
  assert.equal(last.Consumables.length, 1);
  assert.ok(s.State.Loadout.Consumables.some((c) => c.Id === last.Consumables[0]), 'the consumable is in a slot');
  const pick = ex.Offer[1];
  assert.equal(ex.Learn(pick), true);
  assert.ok(ex.Owned.has(pick));
});

test('重い宝箱_OnLayerTwo_HoldsACardAndAPage', () => {
  // a seed whose layer 2 draws a chest
  let seed = 1;
  for (; seed <= 200; seed++) {
    const map = SevenLayers.Of(2).Map(ExplorationSession.LayerSeed(BigInt(seed), 2));
    if ([...LayerEvents.Draw(map, 2, (id) => ExplorationSession.EventSeed(BigInt(seed), 2, id)).values()].includes(EventKind.HeavyChest)) break;
  }
  assert.ok(seed <= 200);
  const ex = trial(seed);
  WalkToLayer(ex, 2);
  const s = ex.Session;
  const node = s.State.Map.Nodes.find((n) => n.Kind === NodeKind.Event && s.EventAt(n.Id) === EventKind.HeavyChest);
  assert.ok(node, 'the chest is on the map the session walks');
  walkTo(ex, (n) => n.Id === node.Id);
  const pages = ex.Pages;
  s.Act(TrialAction.AcceptEvent);
  assert.equal(ex.Pages, pages + 1);
  const last = s.Events[s.Events.length - 1];
  assert.equal(last.Consumables.length, 0);
  assert.equal(last.Cards.length, 3);
  assert.match(last.Record[0], /^最大スタミナ \d+ → \d+$/);
  assert.deepEqual(last.Record.slice(1), ['カード 1 枚', '希少な頁 1 枚']);
});

test('特殊な食事_Accepted_AddsOneMaxStamina_AndTakesTenPercentHp_NeverBelowOne', () => {
  const { seed, node } = seedWithEvent(EventKind.StrangeMeal);
  const ex = trial(seed);
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  walkTo(ex, (n) => n.Id === node, RestChoice.Rest, (session) => { session.State = session.State.With({ hp: 3 }); });
  assert.equal(s.HasPendingEvent, true);
  const before = s.State;
  s.Act(TrialAction.AcceptEvent);
  assert.equal(s.State.MaxStamina, Math.min(before.MaxStamina + 1, 14));
  assert.equal(s.State.Hp, 1, '3 − 5 stops at 1');
  assert.equal(s.State.Phase, RunPhase.Exploring, 'a meal never kills');
  const floor = ExplorationReducer.ResolveEvent(before.With({ hp: 2 }), EventKind.StrangeMeal, true);
  assert.equal(floor.Hp, 1);
});

test('受けない_ChangesNothing_ButTheStepAlreadyPaidItsTime', () => {
  const { seed, node } = seedWithEvent(EventKind.StrangeMeal);
  const ex = trial(seed);
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  const t0 = s.State.TimeLeft;
  walkTo(ex, (n) => n.Id === node);
  const before = s.State;
  assert.equal(before.TimeLeft < t0, true);
  assert.equal(s.Act(TrialAction.DeclineEvent), true);
  assert.equal(s.State, before);
  assert.deepEqual(s.Events[s.Events.length - 1].Lines, ['受けずに通り過ぎた']);
  assert.equal(s.Act(TrialAction.DeclineEvent), false, 'nothing waits any more');
  const row = s.Screen().Journal.Pages.find((p) => p.Id === 'dungeon-1').Rows.find((r) => r.Label === '出来事');
  assert.ok(row.Text.endsWith('特殊な食事: 受けなかった'));
});

test('AnEventOnTheLastTime_StillWaits_BeforeTheLayerBreakCard', () => {
  const { seed, node } = seedWithEvent(EventKind.HeavyChest);
  const ex = trial(seed);
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  walkTo(ex, (n) => n.Id === node);
  s.State = s.State.With({ phase: RunPhase.PushedOut });
  assert.equal(s.Screen().Card.Kind, TrialCardKind.EventChoice);
  assert.equal(s.Act(ScreenAction.GoToInterlude), false, 'the event is answered first');
  s.Act(TrialAction.DeclineEvent);
  assert.equal(s.Screen().Card.Kind, CardKind.PushedOut);
});

test('TheNewLife_ForgetsTheEventsAndTheChoices', () => {
  const { seed, node } = seedWithEvent(EventKind.HeavyChest);
  const ex = trial(seed);
  const s = ex.Session;
  win(ex);
  clearChoices(s);
  walkTo(ex, (n) => n.Id === node);
  s.Act(TrialAction.DeclineEvent);
  assert.equal(s.Events.length > 0, true);
  s.State = ExplorationReducer.EndLife(s.State);
  assert.equal(s.Act(ScreenAction.NewLife), true);
  assert.deepEqual(s.Events, []);
  assert.equal(s.HasPendingEvent || s.HasPendingFound || s.HasPendingSurvey || s.IsAiming, false);
  assert.equal(s.State.Trial, true);
});

// ---------------------------------------------------------------- options and saves

test('TheLimitsOption_ReachesTheDeckRules', async () => {
  const { DeckLimits } = await import('../../src/core/battle/DeckRules.js');
  if (!DeckLimits) return; // the limits extension is another job's; nothing to pass yet
  const deck = PrototypeDeck.Build();
  const tight = new DeckLimits({ DeckMin: 1, DeckMax: deck.length, ToolSlots: 3, ConsumableSlots: 3 });
  const ex = new Expedition(new RunStart({ Seed: 1n }), deck, null, null, null, null, { limits: tight });
  assert.equal(ex.Trial, false);
  const before = ex.Deck.length;
  ex.Take('shield_push');
  assert.equal(ex.Deck.length, before, 'a full deck by the given limits takes no copy');
  assert.ok(ex.Owned.has('shield_push'));
});

test('ATrialOption_OrATrialRunStart_TurnTheSameRulesOn', () => {
  const byOption = new Expedition(new RunStart({ Seed: 1n }), null, null, null, null, null, { trial: true });
  const byStart = new Expedition(new RunStart({ Seed: 1n, Trial: true }));
  for (const ex of [byOption, byStart]) {
    assert.equal(ex.Trial, true);
    assert.equal(ex.Session.Trial, true);
    assert.equal(ex.Session.Keeper, ex);
  }
});

function fight(source) {
  let guard = 0;
  while (!source.Finished) {
    if (++guard > 20000) throw new Error('fight did not end');
    if (source.WaitingForPlayer) {
      const id = source.SuggestedCardId;
      if (id) {
        const face = DepictionText.Find(source.Frame.Hand, id);
        source.TryPlay(id, DepictionText.RequiredZone(face.Aim));
      } else source.EndTurn();
    } else {
      try { source.Next(); } catch (e) { source.AdvanceAuto(); }
    }
  }
}

function WalkToLayer(ex, layer) {
  const s = ex.Session;
  while (s.State.Profile.Layer < layer) {
    walkTo(ex, (n) => n.Kind === NodeKind.Boss);
    win(ex);
    clearChoices(s);
    assert.equal(s.Act(ScreenAction.GoToInterlude), true);
    assert.equal(s.Act(ScreenAction.Descend), true);
    if (s.HasPendingBattle) win(ex);
    clearChoices(s);
  }
}

test('EveryTrialCall_IsLoggedWithPlainArguments_AndALifeReplaysToTheSameState', () => {
  const make = () => trial(3, [SAND, BOOK, WICK], ['yakuso', 'shirube_no_ishi', 'koi_hoshiniku']);
  const raw = make();
  const { expedition: ex, log } = recordExpedition(raw);
  const s = ex.Session;
  let guard = 0;
  const fightPending = () => {
    const battle = ex.Battle || ex.StartEncounter();
    while (!battle.Over) { const src = battle.StartNext(true); fight(src); battle.Finish(src); }
    ex.Return();
    if (ex.Offer.length) ex.Learn(ex.Offer[0]);
  };
  // the life: fight, swap or take finds, answer events, pick the survey, jump once, use the rest
  let jumped = false;
  while (++guard < 120 && !s.State.IsOver && s.State.Profile.Layer === 1) {
    if (s.HasPendingBattle) { fightPending(); continue; }
    if (s.HasPendingFound) { s.Act(TrialAction.TakeFound, 0); continue; }
    if (s.HasPendingEvent) { s.Act(TrialAction.AcceptEvent); if (ex.Offer.length) ex.Learn(ex.Offer[ex.Offer.length - 1]); continue; }
    if (s.HasPendingSurvey) { s.Act(TrialAction.ChooseSurvey, s.SurveyChoices[s.SurveyChoices.length - 1]); continue; }
    if (!s.MapIsLive) break;
    const slots = s.State.Loadout.Consumables;
    const stone = slots.findIndex((c) => c.Kind === ReliefKind.Teleport);
    if (!jumped && stone >= 0 && s.CanUseSlot(stone)) {
      s.UseConsumable(stone);
      const target = s.State.Map.Nodes.find((n) => n.Kind === NodeKind.Event && ExplorationReducer.CanTeleport(s.State, n.Id))
        ?? s.State.Map.Nodes.find((n) => ExplorationReducer.CanTeleport(s.State, n.Id) && n.Kind !== NodeKind.Boss);
      if (target) { s.Jump(target.Id); jumped = true; continue; }
      s.Escape();
    }
    for (let i = 0; i < slots.length; i++) if (slots[i].Kind !== ReliefKind.Teleport && s.CanUseSlot(i)) { s.UseConsumable(i); break; }
    const st = s.State;
    const open = st.Map.Nodes.filter((n) => ExplorationReducer.CanStep(st, n.Id) && !st.HasResolved(n.Id));
    if (open.length === 0) break;
    const next = open.find((n) => n.Kind === NodeKind.Event || n.Kind === NodeKind.Survey) ?? open.find((n) => n.Kind !== NodeKind.Boss) ?? open[0];
    s.Step(next.Id, RestChoice.Rest);
  }
  assert.ok(raw.Session.Events.length > 0 || jumped, 'the walk met an event or jumped');
  for (const [, , args] of log) {
    for (const a of args) assert.ok(a === null || ['string', 'number', 'boolean'].includes(typeof a) || a === '$source', `plain argument ${typeof a}`);
  }
  const again = make();
  replay(again, JSON.parse(JSON.stringify(log)));
  const snap = (e) => JSON.stringify(toParityJson({
    state: e.Session.State, events: e.Session.Events.map((x) => [x.Kind, x.Accepted, x.Lines]), found: e.Session.PendingFound,
    target: e.Session.SurveyTarget, deck: e.Deck.map((c) => c.Def.Id), owned: [...e.Owned].sort(), journal: [...e._journal], pages: e.Pages,
  }));
  assert.equal(snap(again), snap(raw));
});

test('TheMapGenerator_IsUntouched_TheEventsLiveBesideIt', () => {
  const spec = SevenLayers.Of(1).MapSpec;
  const a = MapGenerator.Generate(spec, 11n);
  const b = SevenLayers.Of(1).Map(11n);
  assert.deepEqual(a.Nodes.map((n) => n.Kind), b.Nodes.map((n) => n.Kind));
});

// X-16: the survey says which enemy it raised — on the node, in the journal's row, and (the scene) on arrival.
test('X-16 a finished 情報収集 names the enemy it raised on the node too, and says when nothing was left', async () => {
  const { ExplorationScreenBuilder } = await import('../../src/bridge/explorationScript/ExplorationScreenBuilder.js');
  const intel = { ForNode: () => null, Pages: () => [], SurveyTarget: () => 'shadow_hound' };
  const ex = new Expedition(new RunStart({ Seed: 3n }), null, null, null, intel, null, { trial: true });
  const node = walkTo(ex, (n) => n.Kind === NodeKind.Survey);
  const line = ExplorationScreenBuilder.SurveyLine(ex.Session);
  assert.equal(line, ExplorationText.SurveyDone('瘴牙の走竜'));
  const detail = ex.Session.Screen().Nodes.find((n) => n.Id === node).Detail;
  assert.ok(detail.includes(line), 'the node says it');
  assert.ok(!detail.includes('この層では済ませた'), 'instead of only that it was done');
  // an intel with nothing below 2 left: the survey says so
  const none = new Expedition(new RunStart({ Seed: 3n }), null, null, null, { ...intel, SurveyTarget: () => null }, null, { trial: true });
  walkTo(none, (n) => n.Kind === NodeKind.Survey);
  assert.equal(ExplorationScreenBuilder.SurveyLine(none.Session), ExplorationText.SurveyNothing);
  // 写しの帳: nothing to say while the pick waits
  const book = trial(3, [BOOK]);
  walkTo(book, (n) => n.Kind === NodeKind.Survey);
  assert.equal(ExplorationScreenBuilder.SurveyLine(book.Session), null);
  const target = book.Session.SurveyChoices[0];
  book.Session.Act(TrialAction.ChooseSurvey, target);
  assert.equal(ExplorationScreenBuilder.SurveyLine(book.Session), ExplorationText.SurveyChosen(book.EnemyName(target)));
  // outside trial the C#'s words stay
  const off = new Expedition(new RunStart({ Seed: 3n }), null, null, null, intel, null, null);
  assert.equal(ExplorationScreenBuilder.SurveyLine(off.Session), null);
});
