// Headless lives through the app layer: departure data → Expedition with the lineage's maps → walk,
// fight (unattended, through the recorded proxies), die or survive → record → next life with a trace
// node → take it → the inherited cards and pages reach the Expedition. Every life is replayed from
// its saved start and log and must land on the same state.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { Loadout } from '../../src/core/dungeonContent/Loadout.js';
import { ItemCatalogue } from '../../src/core/dungeonContent/ItemCatalogue.js';
import { ItemEffects, EffectSide } from '../../src/core/dungeonContent/ItemEffect.js';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { ScreenAction, TrialAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { DepictionText } from '../../src/bridge/depictionScript/DepictionTypes.js';
import * as Inh from '../../src/core/inheritance/Inheritance.js';
import { toParityJson } from '../../src/shared/cs.js';
import { recordExpedition, replay, replayLife } from '../../src/app/recorder.js';
import { lifeSetup, makeStart } from '../../src/app/life.js';
import { lifeJournal, endOptions } from '../../src/app/names.js';

function fight(source) {
  let guard = 0;
  while (!source.Finished) {
    if (++guard > 20000) throw new Error('fight did not end');
    if (source.WaitingForPlayer) {
      const id = source.SuggestedCardId;
      if (id) {
        const face = DepictionText.Find(source.Frame.Hand, id);
        const r = source.TryPlay(id, DepictionText.RequiredZone(face.Aim));
        assert.equal(r.verdict, 'Accepted');
      } else source.EndTurn();
    } else {
      try { source.Next(); } catch (e) { source.AdvanceAuto(); }
    }
  }
}

/** Walks one life: `pick(choices)` picks the next node among the open ones. Returns the end state. */
function walk(ex, pick, opts = {}) {
  let guard = 0;
  for (;;) {
    if (++guard > 500) throw new Error(`walk did not end: phase ${ex.Session.State.Phase} layer ${ex.Session.State.Profile.Layer} node ${ex.Session.State.CurrentNodeId} pb ${ex.Session.HasPendingBattle} ps ${ex.Session.HasPendingStep} cd ${ex.Session.ConfirmingDescend} ce ${ex.Session.ConfirmingEndLife} over ${ex.Session.State.IsOver} hp ${ex.Session.State.Hp}`);
    const s = ex.Session;
    const st = s.State;
    if (s.HasPendingBattle) {
      const battle = ex.Battle || ex.StartEncounter();
      while (!battle.Over) { const src = battle.StartNext(true); fight(src); battle.Finish(src); }
      ex.Return();
      if (ex.Offer.length) ex.Learn(ex.Offer[0]);
      continue;
    }
    if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); continue; }
    // trial-mode choices (docs/EXTENSIONS.md): take events, swap found consumables into slot 0, survey the first choice
    if (s.HasPendingEvent) { assert.ok(s.Act(TrialAction.AcceptEvent)); continue; }
    if (s.HasPendingFound) { assert.ok(s.Act(TrialAction.TakeFound, 0) || s.Act(TrialAction.LeaveFound)); continue; }
    if (s.HasPendingSurvey) { assert.ok(s.Act(TrialAction.ChooseSurvey, s.SurveyChoices[0])); continue; }
    if (s.ConfirmingDescend) { s.Act(ScreenAction.ConfirmDescend); continue; }
    if (st.IsOver) return st;
    if (st.Phase === RunPhase.Interlude) {
      if (opts.surviveAt && st.Profile.Layer >= opts.surviveAt) { s.Act(ScreenAction.AskEndLife); s.Act(ScreenAction.ConfirmEndLife); continue; }
      assert.ok(s.Act(ScreenAction.Descend));
      continue;
    }
    if (ExplorationReducer.CanInterlude(st)) { assert.ok(s.Act(ScreenAction.GoToInterlude)); continue; }
    const open = st.Map.Nodes.filter((n) => ExplorationReducer.CanStep(st, n.Id));
    assert.ok(open.length > 0, 'no open node');
    const fresh = open.filter((n) => !st.HasResolved(n.Id));
    const node = pick(fresh.length ? fresh : open, st);
    if (node.Kind === NodeKind.Trace && opts.onTrace) opts.onTrace(st.Profile.Layer);
    assert.ok(s.Step(node.Id), 'step refused');
  }
}

// The departure's payload as the meta scene hands it: a DungeonContent Loadout (catalogue items).
function departure(lifeStart, g = 1) {
  const tools = g % 2 ? ['bosho_no_men', 'kokugen_no_suna', 'ikitsugi_no_fue'] : ['toshin_no_kakera', 'utsushi_no_tobari'];
  const consumables = g % 2 ? ['yakuso', 'joka_no_ko'] : ['koi_hoshiniku', 'shirube_no_ishi', 'wasuremizu'];
  return {
    deck: PrototypeDeck.Build(),
    battleToolIds: tools.filter((id) => ItemEffects.Side(ItemCatalogue.Tool(id).Effect) === EffectSide.Battle),
    loadout: Loadout.Of(tools, consumables, lifeStart.ToolSlots),
    talent: null,
  };
}

const snap = (ex) => JSON.stringify(toParityJson({ state: ex.Session.State, mastery: ex.Mastery, deck: ex.Deck, owned: [...ex.Owned].sort(), journal: [...ex._journal], pages: ex.Pages }));

test('lives die, later lives find the traces, take them, and every life replays exactly', () => {
  let lineage = Inh.newLineage(7);
  let taken = 0;
  for (let g = 1; g <= 12 && taken < 2; g++) {
    const ls = Inh.startLife(lineage);
    lineage = ls.Lineage;
    const start = makeStart(ls, departure(ls, g));
    const { expedition: raw } = lifeSetup(JSON.parse(JSON.stringify(start)));
    const rec = recordExpedition(raw);
    let tookOn = null;
    const preferTrace = (open) => open.find((n) => n.Kind === NodeKind.Trace) || open[0];
    const end = walk(rec.expedition, preferTrace, {
      onTrace: (layer) => {
        const view = Inh.traceNodeView(lineage, layer);
        assert.ok(view && view.CanTake, 'the trace node offers its trace');
        assert.ok(ls.TraceLayers.includes(layer), 'the node stands on a trace layer');
        const picks = view.Candidates.slice(0, 3).map((c) => c.Id);
        const r = Inh.takeTrace(lineage, view.TraceId, picks, { Layer: layer });
        lineage = r.Lineage;
        rec.expedition.Inherit(JSON.parse(JSON.stringify(r.Taken)));
        tookOn = layer;
        taken++;
        for (const c of r.Taken.Cards) {
          assert.ok(raw.Owned.has(c.CardId), 'inherited card is owned');
          assert.ok(raw.Mastery.RankOf(c.CardId) >= Math.min(c.Rank, 2), 'inherited rank kept');
        }
      },
    });
    assert.ok(end.IsOver);
    if (tookOn !== null) assert.ok(end.Map.Nodes.every((n) => n.Kind !== NodeKind.Trace) || end.Profile.Layer === tookOn, 'no trace node after the take');

    const log = JSON.parse(JSON.stringify(rec.log));
    const { expedition: again } = lifeSetup(JSON.parse(JSON.stringify(start)));
    replay(again, log);
    assert.equal(snap(again), snap(raw), `life ${g} replays to the same state`);

    const place = Inh.placeFromState(end);
    lineage = Inh.recordEnd(lineage, null, place, raw.Mastery, lifeJournal(raw, { memo: g === 1 ? 'メモ' : '' }), endOptions(raw, end));
    console.log(`life ${g}: ${place.Ending} on layer ${place.Layer}; traces at start ${JSON.stringify(ls.TraceLayers)}; took ${tookOn}; log ${log.length} entries / ${JSON.stringify(log).length} bytes`);
  }
  assert.ok(taken >= 1, 'some life took a trace');
  const hall = Inh.hallView(lineage);
  assert.ok(hall.PastLives.length >= 2);
});

test('an unfinished fight stays in the log and comes back where it stood (tier1 R1-16, #462)', () => {
  let lineage = Inh.newLineage(11);
  const ls = Inh.startLife(lineage);
  const start = makeStart(ls, departure(ls));
  const { expedition: raw } = lifeSetup(start);
  const rec = recordExpedition(raw);
  const ex = rec.expedition;
  assert.ok(ex.Session.HasPendingBattle, 'the entry node is a battle');
  const battle = ex.StartEncounter();
  const src = battle.StartNext(false);
  // play a few steps, then "close the tab"
  for (let i = 0; i < 5 && !src.Finished; i++) { if (src.WaitingForPlayer) src.EndTurn(); else { try { src.Next(); } catch (e) { src.AdvanceAuto(); } } }
  const log = JSON.parse(JSON.stringify(rec.log));
  const { expedition: again } = lifeSetup(start);
  const { kept, open } = replayLife(again, log);
  assert.ok(kept.some(([t]) => t === 'C'), 'the fight\'s calls are kept');
  assert.ok(open, 'the fight is in progress after the reload');
  assert.equal(open.source.State.Turn, src.State.Turn);
  assert.equal(open.source.State.Player.Hp, src.State.Player.Hp);
  assert.deepEqual(open.source.State.Hand.map((c) => c.InstanceId), src.State.Hand.map((c) => c.InstanceId));
  assert.equal(replay(lifeSetup(start).expedition, log).length, log.length, 'replay keeps every call');
});

test('a survivor passes the bonus: 4 tool slots and deck cap 45 for the next life', () => {
  let lineage = Inh.newLineage(3);
  const ls = Inh.startLife(lineage);
  lineage = ls.Lineage;
  const { expedition: raw } = lifeSetup(makeStart(ls, departure(ls)));
  const end = walk(recordExpedition(raw).expedition, (open) => open[open.length - 1], { surviveAt: 1 });
  if (end.Phase !== RunPhase.Survived) return; // died on layer 1 before the interlude
  lineage = Inh.recordEnd(lineage, null, Inh.placeFromState(end), raw.Mastery, lifeJournal(raw, {}), endOptions(raw, end));
  const next = Inh.startLife(lineage);
  assert.equal(next.ToolSlots, 4);
  assert.equal(next.DeckMax, 45);
});
