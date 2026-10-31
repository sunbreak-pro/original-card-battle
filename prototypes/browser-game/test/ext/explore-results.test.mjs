// Tests for the exploration's result cards and lineage knowledge in trial mode (docs/EXTENSIONS.md 「探索の持ち物と
// イベント」, docs/INHERITANCE.md R-20): a result card stays pending until it is closed, so a save's replay brings it
// back (tier1 R1-16); what the card names (the 開示度 that rose, a spent card pile, learned cards a full deck kept
// out); the carving known from an earlier life; the interlude's own lines; the journal's density floor.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { DeckLimits } from '../../src/core/battle/DeckRules.js';
import { CardInstance, GameResult } from '../../src/core/battle/Types.js';
import { Loadout } from '../../src/core/dungeonContent/Loadout.js';
import { RestChoice } from '../../src/core/dungeon/ExplorationState.js';
import { MapGenerator } from '../../src/core/dungeon/MapGenerator.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { CardPile } from '../../src/bridge/exploration/BattleRewards.js';
import { Expedition } from '../../src/bridge/exploration/Expedition.js';
import { toRunLoadout } from '../../src/bridge/exploration/Loadouts.js';
import { RunStart } from '../../src/bridge/explorationScript/ExplorationSession.js';
import { CardKind, ScreenAction, TrialAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { ExplorationScreenBuilder } from '../../src/bridge/explorationScript/ExplorationScreenBuilder.js';
import { ExplorationText } from '../../src/bridge/explorationScript/ExplorationText.js';
import { EventKind } from '../../src/core/dungeon/LayerEvents.js';
import { recordExpedition, replay } from '../../src/app/recorder.js';
import { EndEveryFight } from '../exploration/_battle.mjs';

const MASK = 'bosho_no_men';

function trial(seed, { tools = [], consumables = [], deck = null, limits = null, journal = null, mapFor = null, carvingKnown } = {}) {
  const slots = Math.max(3, tools.length);
  const loadout = toRunLoadout(Loadout.Of(tools, consumables, slots), slots);
  const start = new RunStart({ Seed: BigInt(seed), Loadout: loadout, MapFor: mapFor });
  const options = { trial: true, limits };
  if (carvingKnown !== undefined) options.carvingKnown = carvingKnown;
  return new Expedition(start, deck, null, null, null, journal, options);
}

function win(ex, hp = 50, stamina = 5) {
  const battle = ex.StartEncounter();
  assert.notEqual(battle, null);
  EndEveryFight(battle, GameResult.Won, { hp, stamina });
  return ex.Return();
}

function clearChoices(session) {
  while (session.HasPendingFound) session.Act(TrialAction.LeaveFound);
  if (session.HasPendingEvent) session.Act(TrialAction.DeclineEvent);
  if (session.HasPendingSurvey) session.Act(TrialAction.ChooseSurvey, session.SurveyChoices[0]);
}

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

/** Walks to the nearest matching node, winning (and closing) every result on the way. */
function walkTo(ex, want) {
  const s = ex.Session;
  if (s.HasPendingBattle) { win(ex); ex.CloseResult(); }
  clearChoices(s);
  ex.CloseResult();
  const path = pathTo(s.State, want);
  for (const node of path) {
    assert.equal(s.Step(node, RestChoice.Rest), true, `step to ${node}`);
    if (s.HasPendingStep) s.Act(ScreenAction.ConfirmStep);
    if (node !== path[path.length - 1]) {
      if (s.HasPendingBattle) { win(ex); ex.CloseResult(); }
      clearChoices(s);
      ex.CloseResult();
    }
  }
}

function walkToLayer(ex, layer) {
  const s = ex.Session;
  while (s.State.Profile.Layer < layer) {
    walkTo(ex, (n) => n.Kind === NodeKind.Boss);
    win(ex);
    ex.CloseResult();
    clearChoices(s);
    assert.equal(s.Act(ScreenAction.GoToInterlude), true);
    assert.equal(s.Act(ScreenAction.Descend), true);
    if (s.HasPendingBattle) { win(ex); ex.CloseResult(); }
    clearChoices(s);
  }
}

/** A prototype deck whose last cards are replaced with these kinds (so they are owned from the start). */
function deckWith(ids) {
  const deck = PrototypeDeck.Build();
  ids.forEach((id, i) => { deck[deck.length - 1 - i] = new CardInstance(`given-${i}`, CardCatalog.ById(id)); });
  return deck;
}

// ---------------------------------------------------------------- the pending result

test('AWonBattlesResult_WaitsUntilClosed_AndAReplayBringsItBackWithItsPick', () => {
  const raw = trial(4);
  const { expedition: ex, log } = recordExpedition(raw);
  const battle = ex.StartEncounter();
  EndEveryFight(battle, GameResult.Won, { hp: 40, stamina: 5 });
  const back = ex.Return();
  const pending = raw.PendingResult;
  assert.equal(pending.Kind, 'battle');
  assert.equal(pending.Back, back);
  const offer = [...raw.Offer];

  // a reload: the same calls on a fresh expedition give the same pending card and the same pick
  const again = trial(4);
  replay(again, log);
  assert.equal(again.PendingResult.Kind, 'battle');
  assert.deepEqual([...again.Offer], offer);
  assert.deepEqual(again.PendingResult.Raised, pending.Raised);

  // closing it (「受け取らない」) gives the pick up and is replayed too
  assert.equal(ex.CloseResult(), true);
  assert.equal(raw.PendingResult, null);
  assert.deepEqual([...raw.Offer], []);
  const third = trial(4);
  replay(third, log);
  assert.equal(third.PendingResult, null);
  assert.deepEqual([...third.Offer], []);
});

test('LearningThePick_ClosesTheResult', () => {
  for (let seed = 1; seed <= 40; seed++) {
    const ex = trial(seed);
    win(ex);
    if (ex.Offer.length === 0) continue;
    assert.equal(ex.Learn(ex.Offer[0]), true);
    assert.equal(ex.PendingResult, null);
    return;
  }
  assert.fail('no seed dealt a card on the entry fight');
});

test('AResult_IsDroppedOnceTheExplorationMovesOn', () => {
  // a save from before CloseResult existed: the result is not shown again once the state changed
  const ex = trial(4);
  win(ex);
  assert.notEqual(ex.PendingResult, null);
  clearChoices(ex.Session);
  const next = pathTo(ex.Session.State, () => true)[0];
  assert.equal(ex.Session.Step(next), true);
  assert.equal(ex.PendingResult, null);
});

test('AnAcceptedEvent_WaitsUntilClosed_ADeclinedOneShowsNothing', () => {
  let checked = 0;
  for (let seed = 1; seed <= 60 && checked < 2; seed++) {
    const ex = trial(seed);
    const s = ex.Session;
    const node = s.State.Map.Nodes.find((n) => n.Kind === NodeKind.Event);
    if (!node) continue;
    walkTo(ex, (n) => n.Id === node.Id);
    if (s.HasPendingBattle || s.State.IsOver) continue;
    if (s.HasPendingEvent) {
      const accept = checked === 0;
      s.Act(accept ? TrialAction.AcceptEvent : TrialAction.DeclineEvent);
      if (accept) {
        assert.equal(ex.PendingResult.Kind, 'event');
        assert.equal(ex.PendingResult.Outcome, s.Events[s.Events.length - 1]);
        ex.CloseResult();
        assert.equal(ex.PendingResult, null);
      } else assert.equal(ex.PendingResult, null);
      checked++;
    } else {
      // a spring resolves on arrival
      assert.equal(ex.PendingResult.Kind, 'event');
      assert.equal(ex.PendingResult.Outcome.Accepted, true);
    }
  }
  assert.ok(checked > 0, 'some seed had an event with a choice');
});

test('OutsideTrial_NothingIsPending', () => {
  const ex = new Expedition(new RunStart({ Seed: 4n }));
  win(ex);
  assert.equal(ex.PendingResult, null);
  assert.equal(ex.CloseResult(), false);
});

// ---------------------------------------------------------------- what the result names

test('Raised_ListsOnlyTheEnemiesWhoseDisclosureRose', () => {
  const probe = trial(4);
  const enemies = [...probe.Session.PendingEncounter.EnemyIds];
  const fresh = trial(4);
  win(fresh);
  assert.deepEqual(fresh.PendingResult.Raised.map(([id]) => id).sort(), [...new Set(enemies)].sort());
  const known = trial(4, { journal: Object.fromEntries(enemies.map((id) => [id, 1])) });
  win(known);
  assert.deepEqual(known.PendingResult.Raised, []);
});

test('PileSpent_WhenTheRewardWantedACardAndThePilesHadNone', () => {
  // the layer-1 stand-in boss always draws from the pile (encounters_and_rewards_v4 §4.2); owning the pile spends it
  const spent = trial(2, { deck: deckWith(CardPile.Of(1)) });
  walkTo(spent, (n) => n.Kind === NodeKind.Boss);
  win(spent);
  assert.equal(spent.PendingResult.PileSpent, true);
  assert.deepEqual([...spent.Offer], []);
  assert.ok(spent.PendingResult.Back.Reward.Consumables.length >= 2, 'the card came as a consumable');

  const full = trial(2);
  walkTo(full, (n) => n.Kind === NodeKind.Boss);
  win(full);
  assert.equal(full.PendingResult.PileSpent, false);
  assert.equal(full.Offer.length, 3);
});

test('AFullDeck_KeepsLearnedCardsOut_AndTheResultSaysWhich', () => {
  // a deck at its cap (20 of 20): the pile's offer does not fit, a real boss's cards are owned only (§4.3)
  const limits = new DeckLimits({ DeckMax: 20 });
  const ex = trial(2, { limits });
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex);
  assert.equal(ex.DeckMax, 20);
  assert.deepEqual(ex.OfferFits, [false, false, false]);
  assert.equal(ex.Learn(ex.Offer[0]), true);
  assert.equal(ex.Deck.length, 20);

  const deep = trial(2, { limits });
  walkToLayer(deep, 3);
  walkTo(deep, (n) => n.Kind === NodeKind.Boss);
  const back = win(deep);
  assert.ok(back.Learned.length > 0, 'the layer-3 boss teaches its cards');
  assert.deepEqual(deep.PendingResult.Benched, back.Learned);
  assert.equal(deep.Deck.length, 20);
  for (const id of back.Learned) assert.equal(deep.Owned.has(id), true);
});

// ---------------------------------------------------------------- the carving known from an earlier life (R-20)

/** The lineage's map seam (src/app/life.js): the carving is taken out once the lineage knows it. */
function mapForKnown(profile, seed) {
  let spec = profile.MapSpec;
  if (spec.Quotas.has(NodeKind.Carving)) spec = spec.With(NodeKind.Carving, 0);
  return MapGenerator.Generate(spec, seed);
}

test('ACarvingKnownToTheLineage_NamesLayerTwoFromTheLifesStart', () => {
  const ex = trial(5, { mapFor: mapForKnown });
  assert.equal(ex.Session.CarvingTaken, true);
  walkToLayer(ex, 2);
  const screen = ex.Session.Screen();
  assert.equal(screen.LayerTitle, `第 2 層　${SevenLayers.Of(2).TrueName}`);
  assert.equal(ex.Session.State.Map.CountOf(NodeKind.Carving), 0);
  assert.match(screen.Journal.Pages.find((p) => p.Id === 'dungeon-2').Title, new RegExp(SevenLayers.Of(2).TrueName));
});

test('TheInterludeBeforeLayerTwo_NamesItAsTheLifeKnowsIt', () => {
  for (const [opts, name] of [[{ mapFor: mapForKnown }, SevenLayers.Of(2).TrueName], [{}, SevenLayers.Of(2).Name]]) {
    const ex = trial(5, opts);
    walkTo(ex, (n) => n.Kind === NodeKind.Boss);
    win(ex);
    ex.CloseResult();
    clearChoices(ex.Session);
    assert.equal(ex.Session.Act(ScreenAction.GoToInterlude), true);
    assert.ok(ex.Session.Screen().Card.Lines.some((l) => l.startsWith(`次は第 2 層　${name}:`)), name);
  }
});

test('CarvingKnown_TheOptionWins_AndOutsideTrialEachLifeReadsItAgain', () => {
  assert.equal(trial(5, { carvingKnown: true }).Session.CarvingTaken, true);
  assert.equal(trial(5, { mapFor: mapForKnown, carvingKnown: false }).Session.CarvingTaken, false);
  assert.equal(trial(5).Session.CarvingTaken, false);
  const plain = new Expedition(new RunStart({ Seed: 5n, MapFor: mapForKnown }));
  assert.equal(plain.Session.CarvingTaken, false);
});

// ---------------------------------------------------------------- the interlude and the journal

test('TheInterludesLines_KeepWhatTheRestGave_WhenAConsumableIsUsedThere', () => {
  const ex = trial(2, { consumables: ['koi_hoshiniku', 'yakuso'] });
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex, 14, 2);
  ex.CloseResult();
  clearChoices(ex.Session);
  const s = ex.Session;
  assert.equal(s.Act(ScreenAction.GoToInterlude), true);
  const rest = s.Screen().Card.Lines.slice(0, 3);
  const meat = s.State.Loadout.Consumables.findIndex((c) => c.Id === 'koi_hoshiniku');
  const herb = s.State.Loadout.Consumables.findIndex((c) => c.Id === 'yakuso');
  assert.equal(s.UseConsumable(meat), true);
  assert.equal(s.UseConsumable(s.State.Loadout.Consumables.findIndex((c) => c.Id === 'yakuso')), herb >= 0);
  assert.deepEqual(s.Screen().Card.Lines.slice(0, 3), rest);
  assert.match(rest[0], new RegExp(`で HP ${s.InterludeState.Hp} / 50`));
});

test('TheInterludeBand_SaysWhatAStepBelowAdds_NotTheGapToTheCap', () => {
  const ex = trial(2);
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex);
  ex.CloseResult();
  clearChoices(ex.Session);
  const s = ex.Session;
  s.Act(ScreenAction.GoToInterlude);
  const below = s.TryDescend();
  assert.equal(s.Screen().Miasma.ForecastLabel, `降りると +${below.EffectiveDensity}%`);
});

test('TheJournalsDensityRow_StopsAtTheFloor', () => {
  const two = trial(1, { tools: [MASK, MASK] });
  const row = (ex) => ex.Session.Screen().Journal.Pages.find((p) => p.Id.startsWith('dungeon-')).Rows.find((r) => r.Label === '瘴気の濃さ').Text;
  assert.equal(row(two), '1 刻限ごとに +1%（層の濃さ 1 − ツール 2、下限 1）');
  const plain = new Expedition(new RunStart({ Seed: 1n, Loadout: RunStart.TrialLoadout() }));
  assert.equal(row(plain), '1 刻限ごとに +1%（層の濃さ 1 − ツール 1）');
});

// ---------------------------------------------------------------- X-9: the event's record says what was kept

/** A trial life whose layer 1 holds a 重い宝箱 dealing a card, with the three slots full, walked onto the chest. */
function atAChest({ record = false } = {}) {
  for (let seed = 1; seed <= 200; seed++) {
    const raw = trial(seed, { consumables: ['yakuso', 'yakuso', 'yakuso'] });
    const node = raw.Session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Event && raw.Session.EventAt(n.Id) === EventKind.HeavyChest);
    if (!node) continue;
    const { expedition: ex, log } = record ? recordExpedition(raw) : { expedition: raw, log: null };
    walkTo(ex, (n) => n.Id === node.Id);
    const s = ex.Session;
    if (!s.HasPendingEvent || !s.State.Loadout.Consumables.every((c) => c.Id === 'yakuso')) continue;
    s.Act(TrialAction.AcceptEvent);
    const last = s.Events[s.Events.length - 1];
    if (last.Cards.length === 0 || !s.HasPendingFound) continue;
    return { seed, raw, ex, s, log };
  }
  assert.fail('no seed walks onto a chest that deals a card with full slots');
}
const lastRecord = (s) => s.Events[s.Events.length - 1].Record;

test('X-9_AChestsCardAndConsumableTurnedDown_LeaveTheRecord', () => {
  const { s, ex } = atAChest();
  const found = ExplorationText.FoundConsumable(ExplorationText.ItemName(s.PendingFound[0]));
  assert.deepEqual(lastRecord(s).slice(1), [ExplorationText.RecordCards(1), found], 'while undecided the finds are listed');
  ex.CloseResult(); // 「受け取らない」
  assert.deepEqual(lastRecord(s).slice(1), [found]);
  s.Act(TrialAction.LeaveFound); // 「諦める」
  assert.match(lastRecord(s)[0], /^最大スタミナ \d+ → \d+$/);
  assert.equal(lastRecord(s).length, 1);
  const row = s.Screen().Journal.Pages.find((p) => p.Id === 'dungeon-1').Rows.find((r) => r.Label === '出来事');
  assert.ok(row.Text.endsWith('重い宝箱: ' + lastRecord(s)[0]), row.Text);
  assert.ok(!s.Events[s.Events.length - 1].Lines.every((l) => lastRecord(s).includes(l)), 'the result card still says what the chest held');
});

test('X-9_AChestsCardLearnedAndConsumableSwappedIn_StayInTheRecord_AndAReplayAgrees', () => {
  const { seed, s, ex, log } = atAChest({ record: true });
  const found = ExplorationText.FoundConsumable(ExplorationText.ItemName(s.PendingFound[0]));
  assert.equal(ex.Learn(ex.Offer[0]), true);
  ex.CloseResult();
  s.Act(TrialAction.TakeFound, 0);
  assert.deepEqual(lastRecord(s).slice(1), [ExplorationText.RecordCards(1), found]);
  const again = trial(seed, { consumables: ['yakuso', 'yakuso', 'yakuso'] });
  replay(again, log);
  assert.deepEqual(lastRecord(again.Session), lastRecord(s));

  // the other way round, replayed: turned down
  const other = atAChest({ record: true });
  other.ex.CloseResult();
  other.s.Act(TrialAction.LeaveFound);
  const third = trial(other.seed, { consumables: ['yakuso', 'yakuso', 'yakuso'] });
  replay(third, other.log);
  assert.deepEqual(lastRecord(third.Session), lastRecord(other.s));
  assert.equal(lastRecord(third.Session).length, 1);
});

test('X-9_ABattlesFindGivenUp_TouchesNoEventRecord', () => {
  const { s, ex } = atAChest();
  ex.CloseResult();
  s.Act(TrialAction.LeaveFound);
  const before = s.Events.map((e) => e.Summary);
  s.Receive(['yakuso']); // a battle's find with the slots full waits on a swap
  assert.equal(s.HasPendingFound, true);
  s.Act(TrialAction.LeaveFound);
  assert.deepEqual(s.Events.map((e) => e.Summary), before);
});

// ---------------------------------------------------------------- X-10: a training mark waits on its card

/** Walks to the nearest rest node and trains there (the mark is earned; the card would open). */
function trainAtARest(ex) {
  const s = ex.Session;
  if (s.HasPendingBattle) { win(ex); ex.CloseResult(); }
  clearChoices(s);
  ex.CloseResult();
  const path = pathTo(s.State, (n) => n.Kind === NodeKind.Rest);
  for (const node of path) {
    const last = node === path[path.length - 1];
    assert.equal(s.Step(node, last ? RestChoice.Train : RestChoice.Rest), true);
    if (s.HasPendingStep) s.Act(ScreenAction.ConfirmStep);
    if (!last) {
      if (s.HasPendingBattle) { win(ex); ex.CloseResult(); }
      clearChoices(s);
      ex.CloseResult();
    }
  }
}

test('X-10_ARestsTraining_WaitsOnItsCard_UntilPutOffOrSpent_AndAReplayAgrees', () => {
  const raw = trial(3);
  const { expedition: ex, log } = recordExpedition(raw);
  const s = ex.Session;
  assert.equal(raw.Session.TrainingWaiting, false);
  trainAtARest(ex);
  assert.equal(raw.Session.State.TrainingMarks, 1);
  assert.equal(raw.Session.TrainingWaiting, true, 'the card opens on arrival');
  const again = trial(3);
  replay(again, log);
  assert.equal(again.Session.TrainingWaiting, true, 'a reload brings the card back');

  assert.equal(s.PutOffTraining(), true); // 「あとで」
  assert.equal(raw.Session.TrainingWaiting, false);
  assert.equal(raw.Session.State.TrainingMarks, 1, 'the mark stays on the band');
  assert.equal(s.PutOffTraining(), false);
  const third = trial(3);
  replay(third, log);
  assert.equal(third.Session.TrainingWaiting, false, 'put off, it stays put off after a reload');

  assert.equal(s.OpenTraining(), true); // the band's 「訓練の刻み」
  assert.equal(raw.Session.TrainingWaiting, true);
  const id = raw.Deck[0].Def.Id;
  assert.equal(ex.Train(id), true);
  assert.equal(raw.Session.State.TrainingMarks, 0);
  assert.equal(raw.Session.TrainingWaiting, false, 'no mark left to wait on');
  assert.equal(s.OpenTraining(), false, 'nothing to open with no mark');
  const fourth = trial(3);
  replay(fourth, log);
  assert.equal(fourth.Session.TrainingWaiting, false);
  assert.equal(fourth.Mastery.TicksOf(id), raw.Mastery.TicksOf(id));
});

test('X-10_LeavingTheLayer_EndsTheWait_AndOutsideTrialNothingWaits', () => {
  const ex = trial(3);
  const s = ex.Session;
  trainAtARest(ex);
  assert.equal(s.TrainingWaiting, true);
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex);
  ex.CloseResult();
  clearChoices(s);
  assert.equal(s.Act(ScreenAction.GoToInterlude), true);
  assert.equal(s.State.TrainingMarks, 1, 'the mark is still this layer’s at the interlude');
  assert.equal(s.Act(ScreenAction.Descend), true);
  assert.equal(s.State.TrainingMarks, 0, 'the next layer starts with none (ExplorationReducer.Enter)');
  assert.equal(s.TrainingWaiting, false);

  const plain = new Expedition(new RunStart({ Seed: 3n, Loadout: RunStart.TrialLoadout() }));
  trainAtARest(plain);
  assert.equal(plain.Session.State.TrainingMarks, 1);
  assert.equal(plain.Session.TrainingWaiting, false);
  assert.equal(plain.Session.OpenTraining(), false);
});

// ---------------------------------------------------------------- X-8: the interlude under its confirmation

test('X-8_TheInterludeCard_IsThereUnderItsConfirmation', () => {
  const ex = trial(2);
  const s = ex.Session;
  assert.equal(ExplorationScreenBuilder.InterludeCard(s), null, 'none outside the interlude');
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex);
  ex.CloseResult();
  clearChoices(s);
  s.Act(ScreenAction.GoToInterlude);
  const rest = s.Screen().Card;
  assert.equal(s.Act(ScreenAction.AskEndLife), true);
  assert.equal(s.Screen().Card.Kind, CardKind.ConfirmEndLife);
  const under = ExplorationScreenBuilder.InterludeCard(s);
  assert.equal(under.Kind, CardKind.Interlude);
  assert.deepEqual(under.Lines, rest.Lines);
  assert.deepEqual(under.Buttons.map((b) => b.Label), rest.Buttons.map((b) => b.Label));
});

// ---------------------------------------------------------------- X-11: what a trace's card gives this heir

import { CardMastery } from '../../src/core/battle/Mastery.js';
import { traceGains, candidateCard, candidatePage, deckIsFull, inheritPreview, pagePreview, cardNameAt, TraceGainText } from '../../src/bridge/exploration/TraceGain.js';
import '../../src/app/life.js'; // Expedition.prototype.Inherit (the game's take)

const cardRow = (id, rank) => ({ Id: `card:${id}`, Kind: 'Card', KindLabel: '技術', Name: id, Detail: `${rank} 段目` });
const withRanks = (ex, ranks) => {
  ex.Mastery = ex.Mastery.with({ Records: new Map(Object.entries(ranks).map(([id, r]) => [id, new CardMastery(id, [0, 3, 8, 15][r], r)])) });
  return ex;
};

test('X-11_TheInheritPreview_IsWhatTheTakeDoes', () => {
  // a 3 段 already held elsewhere caps the inherited 3 段 at 2; a card held at its 段 adds none; the deck has room
  const cards = [{ CardId: 'feint', Rank: 3 }, { CardId: 'brace', Rank: 2 }, { CardId: 'kesa_cut', Rank: 1 }];
  const ex = withRanks(trial(3), { thrust: 3, brace: 2 });
  const preview = inheritPreview(ex, cards);
  assert.deepEqual(preview.map((r) => [r.CardId, r.From, r.To, r.Joins]), [['feint', 0, 2, true], ['brace', 2, 2, true], ['kesa_cut', 0, 1, true]]);
  const deck = ex.Deck.length;
  ex.Inherit({ TraceId: 'trace-1', Cards: cards.map((c) => ({ ...c, Ticks: 0, Name: c.CardId })), Journal: {} });
  assert.equal(ex.Deck.length, deck + 3);
  for (const r of preview) assert.equal(ex.Mastery.RankOf(r.CardId), r.To, r.CardId);
});

test('X-11_AFullDeck_TakesNoCopy_AndTheCardsSaySo', () => {
  const ex = withRanks(trial(3, { limits: new DeckLimits({ DeckMax: 20 }) }), { boar_rush: 2 });
  assert.equal(deckIsFull(ex), true);
  const rows = [cardRow('boar_rush', 2), cardRow('feint', 1), { Id: 'page:page-1', Kind: 'DungeonPage', Detail: '手記の頁' }];
  assert.deepEqual(candidateCard(rows[0]), { CardId: 'boar_rush', Rank: 2 });
  assert.equal(candidateCard(rows[2]), null);
  const before = traceGains(ex, rows, []);
  assert.equal(before.get('card:boar_rush').Nothing, true, 'held at that 段 with no room: nothing at all');
  assert.deepEqual(before.get('card:boar_rush').Notes, [TraceGainText.Nothing(TraceGainText.Now(2), false)]);
  assert.deepEqual(before.get('card:feint').Notes, [TraceGainText.OwnedOnly(null, false)], 'the deck cap is the card lead\'s');
  assert.deepEqual(before.get('page:page-1').Notes, [], 'a dungeon page always adds one: nothing to say');
  assert.equal(before.get('page:page-1').Nothing, false);
  const after = traceGains(ex, rows, ['card:feint', 'card:boar_rush'], { done: true });
  assert.deepEqual(after.get('card:feint').Notes, [`${TraceGainText.RankUp(0, 1)}・${TraceGainText.OwnedOnly(TraceGainText.DeckCap(20), true)}`]);
  ex.Inherit({ TraceId: 'trace-1', Cards: [{ CardId: 'feint', Rank: 1, Ticks: 3, Name: 'feint' }], Journal: {} });
  assert.equal(ex.Deck.length, 20, 'the take agrees: owned only');
  assert.equal(ex.Owned.has('feint'), true);
});

test('X-11_TheOneCardRuleOnThreeDan_IsNamed_BeforeAndAfter', () => {
  const ex = withRanks(trial(3), { thrust: 3, feint: 2 });
  const rows = [cardRow('feint', 3)];
  const before = traceGains(ex, rows, []);
  // the row is printed at this heir's 段 and name; a first line says what the trace held
  assert.equal(before.get('card:feint').Name, cardNameAt('feint', 2));
  assert.equal(before.get('card:feint').Detail, '2 段目');
  assert.deepEqual(before.get('card:feint').Notes, [TraceGainText.InTrace('feint', 3), `${TraceGainText.Now(2)}（${TraceGainText.Capped(2)}）・${TraceGainText.Joins}`]);
  const after = traceGains(ex, rows, ['card:feint'], { done: true });
  assert.deepEqual(after.get('card:feint').Notes, [TraceGainText.InTrace('feint', 3), `${TraceGainText.Already(2)}（${TraceGainText.Capped(2)}）・${TraceGainText.Joined}`]);
});

// ---------------------------------------------------------------- X-3: the received card survives a reload

test('X-3_TheReceivedCard_WaitsUntilClosed_AndAReplayBringsItBack', () => {
  const raw = trial(5);
  const { expedition: ex, log } = recordExpedition(raw);
  // the trace card's notes read the recorded expedition and log nothing
  const logged = log.length;
  traceGains(ex, [cardRow('feint', 1)], ['card:feint']);
  assert.equal(log.length, logged);
  const card = { PlaceLine: '第 1 層', HeirLine: '第 1 代 踏破者 カリナの痕跡', Rows: [{ Kind: 'Card', KindLabel: '技術', Name: '牽制', Detail: '1 段目', Notes: ['習熟 0 → 1 段目・デッキに 1 枚入った'], Nothing: false }], Lost: 4 };
  assert.equal(ex.ShowTaken(card), false, 'nothing was taken yet');
  ex.Inherit({ TraceId: 'trace-1', Cards: [{ CardId: 'feint', Rank: 1, Ticks: 3, Name: '牽制' }], Journal: {} });
  assert.equal(ex.ShowTaken(card), true);
  assert.deepEqual(raw.PendingTaken, card);
  const again = trial(5);
  replay(again, log);
  assert.deepEqual(again.PendingTaken, card);
  assert.equal(ex.CloseTaken(), true);
  assert.equal(raw.PendingTaken, null);
  const third = trial(5);
  replay(third, log);
  assert.equal(third.PendingTaken, null);
  const plain = new Expedition(new RunStart({ Seed: 5n, Loadout: RunStart.TrialLoadout() }));
  plain.InheritedTraceId = 'trace-1';
  assert.equal(plain.ShowTaken(card), false, 'outside trial nothing waits');
  assert.equal(plain.PendingTaken, null);
});

// ---------------------------------------------------------------- the band, the notes, the HP lines, the carving page

test('X-6_TheMasksNote_AtTheInterlude_IsTheNextLayers', () => {
  const ex = trial(2, { tools: [MASK] });
  const s = ex.Session;
  const names = ExplorationText.ItemName(MASK);
  assert.equal(s.Screen().ReliefLabel, ExplorationText.Relief(names, s.State.Profile.Density, s.State.EffectiveDensity));
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex);
  ex.CloseResult();
  clearChoices(s);
  s.Act(ScreenAction.GoToInterlude);
  const below = s.TryDescend();
  assert.equal(s.Screen().ReliefLabel, ExplorationText.Relief(names, below.Profile.Density, below.EffectiveDensity));
  assert.equal(s.Screen().Miasma.ForecastLabel, `降りると +${below.EffectiveDensity}%`);
});

test('PlayerNote_DropsTheDesignStatusRemark_WhereThePlayerReadsIt', () => {
  assert.equal(ExplorationText.PlayerNote('ryurin_no_kakera'), '1 戦につき最初の 1 回だけ Guard 5');
  assert.equal(ExplorationText.PlayerNote('bosho_no_men'), ExplorationText.ItemNote('bosho_no_men'));
  const ex = trial(1, { tools: ['ryurin_no_kakera'] });
  assert.equal(ex.Session.Screen().ToolCards.find((t) => t.Id === 'ryurin_no_kakera').Note, '1 戦につき最初の 1 回だけ Guard 5');
});

test('TheHpLines_ReadTheRealGain_MarkedWhenTheMaximumStopsIt', () => {
  const ex = trial(2);
  const s = ex.Session;
  walkTo(ex, (n) => n.Kind === NodeKind.Boss);
  win(ex, 45);
  ex.CloseResult();
  clearChoices(s);
  const line = s.Screen().Card.Lines.find((l) => l.startsWith('階層間の休憩'));
  assert.equal(line, `階層間の休憩: HP +5（最大の 30%${ExplorationText.CappedMark}）・スタミナ全回復・瘴気はたまらない`);
  s.Act(ScreenAction.GoToInterlude);
  assert.equal(s.Screen().Card.Lines[0], `HP +5（最大の 30%${ExplorationText.CappedMark}）で HP 50 / 50`);
  // a rest node at full HP reads the same way (the first seed whose rest is in sight from the entry)
  const restLine = [1, 2, 3, 4, 5, 6, 7, 8].map((seed) => trial(seed).Session.Screen().Nodes.find((n) => n.Kind === NodeKind.Rest && n.Lines.length))
    .find(Boolean).Lines[0];
  assert.match(restLine, /^休む: HP \+0（最大の 15%・上限まで）・/);
});

test('X-12_ReadingTheCarving_AddsAJournalPage_InTrialOnly', () => {
  const page = (ex) => ex.Session.Screen().Journal.Pages.find((p) => p.Id === 'carving') ?? null;
  for (let seed = 1; seed <= 30; seed++) {
    const ex = trial(seed);
    walkToLayer(ex, 2);
    const s = ex.Session;
    if (!s.State.Map.Nodes.some((n) => n.Kind === NodeKind.Carving)) continue;
    assert.equal(page(ex), null, 'not before it is read');
    walkTo(ex, (n) => n.Kind === NodeKind.Carving);
    if (s.State.IsOver) continue;
    assert.equal(s.CarvingTaken, true);
    const p = page(ex);
    assert.notEqual(p, null);
    assert.equal(p.Title, ExplorationText.CarvingPageTitle);
    const known = trial(seed, { carvingKnown: true });
    assert.notEqual(page(known), null, 'a lineage that read it knows the page too (R-20)');
    const plain = new Expedition(new RunStart({ Seed: BigInt(seed), Loadout: RunStart.TrialLoadout() }));
    plain.Session.CarvingTaken = true;
    assert.equal(page(plain), null, 'outside trial the C#\'s journal');
    return;
  }
  assert.fail('no seed reached the layer-two carving');
});
