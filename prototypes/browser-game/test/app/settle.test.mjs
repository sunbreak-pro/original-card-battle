// The app saves a fight the moment the core decides it (recorder.settleWhenDecided), not after the screen
// has played the fall and held the result card: a reload in between keeps a death and a won fight
// (tier1 R1-16 「死亡と生の終了は確定と同時に保存し…」, INHERITANCE.md R-21). Also the save module's
// storage probe and the copy kept of a save whose life would not replay.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { Loadout } from '../../src/core/dungeonContent/Loadout.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';
import { ScreenAction, TrialAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { toParityJson } from '../../src/shared/cs.js';
import { DepictionText } from '../../src/bridge/depictionScript/DepictionTypes.js';
import * as Inh from '../../src/core/inheritance/Inheritance.js';
import { recordExpedition, replay, replayLife, settleWhenDecided, trimUnfinished } from '../../src/app/recorder.js';
import { lifeSetup, makeStart } from '../../src/app/life.js';

function newLife(seed) {
  const ls = Inh.startLife(Inh.newLineage(seed));
  const start = makeStart(ls, { deck: PrototypeDeck.Build(), battleToolIds: [], loadout: Loadout.Of([], [], ls.ToolSlots), talent: null });
  const { expedition: raw } = lifeSetup(JSON.parse(JSON.stringify(start)));
  const rec = recordExpedition(raw);
  return { start, raw, rec, ex: rec.expedition };
}

/** One step of the screen: the player's move when it waits for one, else the next script event. */
function step(src, play) {
  if (src.WaitingForPlayer) {
    const id = play ? src.SuggestedCardId : null;
    const face = id ? DepictionText.Find(src.Frame.Hand, id) : null;
    if (face) src.TryPlay(id, DepictionText.RequiredZone(face.Aim));
    else src.EndTurn();
  } else src.AdvanceAuto();
}

const saved = (rec) => JSON.parse(JSON.stringify(rec.log));
const finishes = (log) => log.filter(([t, m]) => t === 'B' && m === 'Finish').length;

test('a lost fight is finished and saved when the core decides it; a reload then keeps the death', () => {
  const { start, rec, ex } = newLife(11);
  assert.ok(ex.Session.HasPendingBattle, 'the entry node is a battle');
  const battle = ex.StartEncounter();
  const saves = [];
  const fight = settleWhenDecided(battle, battle.StartNext(false), () => saves.push(saved(rec)));
  // never play a card: only end turns until the heir falls
  for (let guard = 0; guard < 20000 && !fight.settled; guard++) step(fight.source, false);
  assert.ok(fight.settled, 'the fight was decided');
  assert.equal(saves.length, 1, 'saved once, at the decision');
  assert.ok(battle.Lost, 'the encounter holds the lost fight');
  // (the save is written inside the call that decided it: the screen has yet to play that event, the fall
  // and the result card)
  const atDefeat = saves[0];
  assert.deepEqual(atDefeat[atDefeat.length - 1], ['B', 'Finish', ['$source']], 'the save ends on the finished fight');
  assert.equal(trimUnfinished(atDefeat).length, atDefeat.length, 'nothing of the decided fight is dropped on load');

  // "reload while the 負け card is up": the fight does not start over, the encounter comes back lost
  const { expedition: again } = lifeSetup(JSON.parse(JSON.stringify(start)));
  replay(again, atDefeat);
  assert.ok(again.Battle && again.Battle.Over && again.Battle.Lost, 'the encounter is over and lost after the reload');
  const back = again.Return();
  assert.ok(back.Died);
  assert.equal(again.Session.State.Phase, RunPhase.Fallen, 'the heir is dead');

  // the screen plays the rest; the app's finish after onFinished changes nothing
  for (let guard = 0; guard < 20000 && !fight.source.Finished; guard++) fight.source.AdvanceAuto();
  assert.equal(fight.finish(), false, 'finish after the decision is a no-op');
  assert.equal(finishes(rec.log), 1, 'Finish is logged once');
  const { expedition: full } = lifeSetup(JSON.parse(JSON.stringify(start)));
  replay(full, saved(rec));
  assert.ok(full.Battle.Lost);
  full.Return();
  assert.equal(full.Session.State.Phase, RunPhase.Fallen, 'the whole log replays to the same death');
  assert.equal(full.Session.State.Hp, again.Session.State.Hp);
});

test('a won fight is kept by a reload during its result card', () => {
  const { start, rec, ex } = newLife(5);
  const battle = ex.StartEncounter();
  const saves = [];
  const fight = settleWhenDecided(battle, battle.StartNext(true), () => saves.push(saved(rec)));
  for (let guard = 0; guard < 20000 && !fight.settled; guard++) step(fight.source, true);
  assert.equal(saves.length, 1);
  const { expedition: again } = lifeSetup(JSON.parse(JSON.stringify(start)));
  replay(again, saves[0]);
  assert.equal(again.Battle.Fought.length, 1, 'the fight is in the save');
  assert.equal(again.Battle.Fought[0].State.Result, battle.Fought[0].State.Result, 'with the same outcome');
  assert.equal(again.Battle.HpAfter, battle.HpAfter, 'and the same HP');
});

/**
 * The player's side of one screen step without the source's suggestion (a played battle has none): the
 * leftmost card the source would take, else the turn's end; the next script event while it is not waiting.
 */
function playerStep(src) {
  if (src.WaitingForPlayer) {
    const card = src.Frame.Hand.find((f) => src.Inspect(f.Id) === 'Accepted');
    if (card) src.TryPlay(card.Id, DepictionText.RequiredZone(card.Aim));
    else src.EndTurn();
  } else src.AdvanceAuto();
}
const battleJson = (src) => JSON.stringify(toParityJson({ state: src.State, frame: src.Frame, finished: src.Finished, waiting: src.WaitingForPlayer }));

// tier1 R1-16 (2026-10-12, #462): 「戦闘中にゲームを閉じたら、閉じた場面から再開する」, its criterion
// 「戦闘中にプロセスを落として再開すると、閉じた場面から続き、続きの結果が閉じる前と同じになる」.
test('a fight in progress comes back on load where it stood, and goes on to the same end', () => {
  for (const [seed, cut] of [[11, 7], [5, 23], [8, 2]]) {
    const { start, rec, ex } = newLife(seed);
    const battle = ex.StartEncounter();
    const fight = settleWhenDecided(battle, battle.StartNext(false), null);
    for (let i = 0; i < cut && !fight.settled; i++) playerStep(fight.source);
    assert.ok(!fight.settled, `seed ${seed}: still undecided at the cut`);
    const log = saved(rec);

    const { expedition: again } = lifeSetup(JSON.parse(JSON.stringify(start)));
    const { kept, open } = replayLife(again, log);
    assert.equal(kept.length, log.length, 'nothing of the fight is dropped');
    assert.ok(open && open.source && open.battle === again.Battle, 'the fight in progress comes back');
    assert.equal(battleJson(open.source), battleJson(fight.source), 'hand, piles, HP, stamina, Guard, statuses, omen, turn and phase as they were');

    // go on in both: the reloaded one through a recorder like the app's, the same moves
    const rec2 = recordExpedition(again, kept);
    const back = settleWhenDecided(rec2.expedition.Battle, rec2.wrapSource(open.source), null);
    for (let guard = 0; guard < 20000 && !(fight.settled && back.settled); guard++) {
      assert.equal(battleJson(back.source), battleJson(fight.source), 'every step lands on the same state (the random state came back too)');
      if (!fight.settled) playerStep(fight.source);
      if (!back.settled) playerStep(back.source);
    }
    assert.ok(fight.settled && back.settled, 'both fights were decided');
    assert.equal(again.Battle.Fought[0].State.Result, battle.Fought[0].State.Result, 'the same outcome');
    assert.equal(again.Battle.HpAfter, battle.HpAfter, 'the same HP');
    // and the reloaded life's log, saved again, replays to the same decided fight
    const { expedition: third } = lifeSetup(JSON.parse(JSON.stringify(start)));
    const r3 = replayLife(third, saved(rec2));
    assert.equal(r3.open, null, 'decided: nothing in progress');
    assert.equal(third.Battle.HpAfter, battle.HpAfter);
  }
});

test('a fight brought back already decided is finished at once; trimUnfinished still drops a fight that must start over', () => {
  const { start, rec, ex } = newLife(11);
  const battle = ex.StartEncounter();
  const raw = battle.StartNext(false);
  // the source is driven without settling (as if the Finish never reached the save)
  for (let guard = 0; guard < 20000 && raw.State.Result === 'Ongoing'; guard++) { if (raw.WaitingForPlayer) raw.EndTurn(); else raw.AdvanceAuto(); }
  const log = saved(rec);
  const { expedition: again } = lifeSetup(JSON.parse(JSON.stringify(start)));
  const { open } = replayLife(again, log);
  assert.ok(open, 'the fight is open in the log');
  const saves = [];
  const fight = settleWhenDecided(again.Battle, open.source, () => saves.push(1));
  assert.ok(fight.settled && saves.length === 1, 'finished and saved as it is handed back');
  assert.ok(again.Battle.Over && again.Battle.Lost);

  // the battle scene failed on a fight: the app drops it and the node waits on it again
  const { expedition: fresh } = lifeSetup(JSON.parse(JSON.stringify(start)));
  const trimmed = trimUnfinished(log);
  assert.ok(trimmed.every(([t]) => t !== 'C'), 'no call of the dropped fight is kept');
  const r = replayLife(fresh, trimmed);
  assert.equal(r.open, null);
  assert.ok(fresh.Battle && fresh.Battle.Index === 0, 'the encounter waits at its first fight');
  // read-only calls and properties pass straight through the settling view
  const view = settleWhenDecided(fresh.Battle, fresh.Battle.StartNext(false), null);
  assert.equal(typeof view.source.Frame, 'object');
  assert.equal(view.source.Finished, false);
  assert.equal(typeof view.source.Inspect, 'function');
  assert.equal(view.settled, false, 'a new fight is not decided');
});

/** A whole life walked like the app does it: every fight through settleWhenDecided, then drained to its end. */
function walkSettled(ex, rec, saves) {
  let chains = 0;
  for (let guard = 0; guard < 600; guard++) {
    const s = ex.Session;
    const st = s.State;
    if (s.HasPendingBattle) {
      const battle = ex.Battle || ex.StartEncounter();
      if (battle.Count > 1) chains++;
      while (!battle.Over) {
        const fight = settleWhenDecided(battle, battle.StartNext(true), () => saves.push(saved(rec)));
        for (let g = 0; g < 20000 && !fight.source.Finished; g++) step(fight.source, true);
        assert.ok(fight.settled, 'every fight is finished at its decision');
        fight.finish();
      }
      ex.Return();
      if (ex.Offer.length) ex.Learn(ex.Offer[0]);
      continue;
    }
    if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); continue; }
    if (s.HasPendingEvent) { s.Act(TrialAction.AcceptEvent); continue; }
    if (s.HasPendingFound) { if (!s.Act(TrialAction.TakeFound, 0)) s.Act(TrialAction.LeaveFound); continue; }
    if (s.HasPendingSurvey) { s.Act(TrialAction.ChooseSurvey, s.SurveyChoices[0]); continue; }
    if (s.ConfirmingDescend) { s.Act(ScreenAction.ConfirmDescend); continue; }
    if (st.IsOver) return { end: st, chains };
    if (st.Phase === RunPhase.Interlude) { s.Act(ScreenAction.Descend); continue; }
    if (ExplorationReducer.CanInterlude(st)) { s.Act(ScreenAction.GoToInterlude); continue; }
    const open = st.Map.Nodes.filter((n) => ExplorationReducer.CanStep(st, n.Id));
    const fresh = open.filter((n) => !st.HasResolved(n.Id));
    const pick = (fresh.length ? fresh : open);
    const node = pick.find((n) => n.Kind === 'Elite' || n.Kind === 'Boss') || pick[0];
    assert.ok(s.Step(node.Id), 'step refused');
  }
  throw new Error('the life did not end');
}

const snap = (ex) => JSON.stringify(toParityJson({ state: ex.Session.State, mastery: ex.Mastery, deck: ex.Deck, owned: [...ex.Owned].sort(), journal: [...ex._journal], pages: ex.Pages }));

test('whole lives settled fight by fight (chains included) replay from every save to the same fights', () => {
  let chains = 0;
  for (const seed of [2, 4, 6]) {
    const { start, raw, rec, ex } = newLife(seed);
    const saves = [];
    const { end, chains: c } = walkSettled(ex, rec, saves);
    chains += c;
    assert.ok(end.IsOver);
    // the last save of the life replays to the same state once its encounter is brought back
    const { expedition: again } = lifeSetup(JSON.parse(JSON.stringify(start)));
    replay(again, saved(rec));
    assert.equal(snap(again), snap(raw), `life ${seed} replays to the same state`);
    // and every save made at a decision keeps every fight decided so far
    for (const log of saves) {
      const { expedition: e } = lifeSetup(JSON.parse(JSON.stringify(start)));
      replay(e, log);
      assert.ok(e.Battle && e.Battle.Fought.length >= 1, 'the decided fight is in the save');
    }
  }
  assert.ok(chains >= 1, 'some encounter held several fights');
});

// ---- save.js: storage probe and the copy of a save that would not replay ----

function fakeStorage({ broken = false } = {}) {
  const m = new Map();
  return {
    getItem(k) { if (broken) throw new Error('SecurityError'); return m.has(k) ? m.get(k) : null; },
    setItem(k, v) { if (broken) throw new Error('SecurityError'); m.set(k, String(v)); },
    removeItem(k) { if (broken) throw new Error('SecurityError'); m.delete(k); },
    keys: () => [...m.keys()],
  };
}

test('storageWorks tells blocked storage apart and leaves nothing behind; keepBrokenSave copies the save aside', async () => {
  const save = await import('../../src/app/save.js');
  const had = Object.getOwnPropertyDescriptor(globalThis, 'window');
  try {
    const ok = fakeStorage();
    globalThis.window = { localStorage: ok };
    assert.equal(save.storageWorks(), true);
    assert.deepEqual(ok.keys(), [], 'the probe is removed');
    const lineage = Inh.newLineage(9);
    assert.equal(save.writeSave({ lineage, life: { start: { seed: '1' }, log: [['S', 'Step', [3]]] } }), true);
    assert.equal(save.readBrokenSave(), null);
    assert.equal(save.keepBrokenSave(), true);
    assert.equal(save.readBrokenSave(), ok.getItem('ocb.save.v1'), 'the copy is the save as stored');

    globalThis.window = { localStorage: fakeStorage({ broken: true }) };
    assert.equal(save.storageWorks(), false);
    assert.equal(save.writeSave({ lineage, life: null }), false, 'a write reports the failure');
    assert.equal(save.keepBrokenSave(), false);

    globalThis.window = {}; // no storage at all
    assert.equal(save.storageWorks(), false);
  } finally {
    if (had) Object.defineProperty(globalThis, 'window', had); else delete globalThis.window;
  }
});

// A stored lineage that does not load (another build's version, a write cut off) is never dropped without
// a word: openSave says so and copies the text aside, and leaves it under its own key (tier1 R1-16; no
// manual load slot, CAMP_FACILITIES_DESIGN.md §3.5, so the player has no other way back to it).
test('openSave: nothing stored, a good save, and an unreadable one (kept aside, left in place)', async () => {
  const save = await import('../../src/app/save.js');
  const had = Object.getOwnPropertyDescriptor(globalThis, 'window');
  const warn = console.warn;
  console.warn = () => {};
  try {
    const st = fakeStorage();
    globalThis.window = { localStorage: st };
    assert.deepEqual(save.openSave(), { save: null, unreadable: null, stamp: null }, 'nothing stored is not an error');

    const lineage = Inh.newLineage(11);
    save.writeSave({ lineage, life: null, deckSave: 'D' });
    const good = st.getItem('ocb.save.v1');
    const r = save.openSave();
    assert.equal(r.unreadable, null);
    assert.equal(Inh.serialize(r.save.lineage), Inh.serialize(lineage));
    assert.equal(r.save.deckSave, 'D');
    assert.equal(save.readBrokenSave(), null, 'a save that loads is not copied');

    const future = (() => { const s = JSON.parse(good); s.lineage.Version = (s.lineage.Version || 1) + 1; return JSON.stringify(s); })();
    const variants = { future, truncated: good.slice(0, Math.floor(good.length * 0.7)), noLineage: JSON.stringify({ v: 1, lineage: null }), nullText: 'null' };
    for (const [name, text] of Object.entries(variants)) {
      st.setItem('ocb.save.v1', text);
      st.removeItem('ocb.save.v1.broken');
      const b = save.openSave();
      assert.equal(b.save, null, name);
      assert.ok(b.unreadable && b.unreadable.error, name + ': says why');
      assert.equal(b.unreadable.kept, true, name);
      assert.equal(save.readBrokenSave(), text, name + ': the copy is the text as stored');
      assert.equal(st.getItem('ocb.save.v1'), text, name + ': nothing is overwritten until the player starts anew');
      assert.equal(save.loadSave(), null, name + ': loadSave stays null');
    }

    // Storage that reads but refuses writes (full): the save is still reported, the copy is not claimed.
    const full = fakeStorage();
    full.setItem('ocb.save.v1', variants.truncated);
    full.setItem = () => { throw new Error('QuotaExceededError'); };
    globalThis.window = { localStorage: full };
    const f = save.openSave();
    assert.equal(f.save, null);
    assert.equal(f.unreadable.kept, false);
  } finally {
    console.warn = warn;
    if (had) Object.defineProperty(globalThis, 'window', had); else delete globalThis.window;
  }
});
