// dev only: which seeds let the unattended auto-play walk layer 1 to its boss and win (for the screenshots).
import { Expedition } from '../src/bridge/exploration/Expedition.js';
import { RunStart } from '../src/bridge/explorationScript/ExplorationSession.js';
import { DepictionText } from '../src/bridge/depictionScript/DepictionTypes.js';
import { RunPhase } from '../src/core/dungeon/ExplorationState.js';

function fight(exp) {
  const b = exp.StartEncounter();
  if (!b) return;
  while (!b.Over) {
    const s = b.StartNext(true);
    let n = 0;
    while (!s.Finished && n++ < 6000) {
      if (!s.WaitingForPlayer) { s.AdvanceAuto(); continue; }
      const id = s.SuggestedCardId;
      if (!id) { s.EndTurn(); continue; }
      const face = DepictionText.Find(s.Frame.Hand, id);
      s.TryPlay(id, DepictionText.RequiredZone(face.Aim));
    }
    if (!s.Finished) { exp.Abandon(); return 'stuck'; }
    b.Finish(s);
  }
  exp.Return();
  if (exp.Offer.length) exp.Learn(exp.Offer[0]);
}
for (let seed = 1; seed <= 12; seed++) {
  const exp = new Expedition(new RunStart({ Seed: BigInt(seed), Loadout: RunStart.TrialLoadout() }));
  const s = exp.Session;
  const log = [];
  let guard = 0;
  let err = '';
  while (guard++ < 30) {
    if (s.HasPendingBattle) {
      try { if (fight(exp) === 'stuck') { log.push('stuck'); break; } } catch (e) { err = e.message + ' @layer ' + s.State.Profile.Layer + ' ' + (s.PendingEncounter ? s.PendingEncounter.EnemyIds.join('+') : ''); break; }
      continue;
    }
    const st = s.State;
    if (st.IsOver) break;
    if (st.Phase === RunPhase.LayerCleared || st.Phase === RunPhase.PushedOut) { s.Act('GoToInterlude'); continue; }
    if (st.Phase === RunPhase.Interlude) { if (st.Profile.Layer >= 2) break; s.Act('Descend'); continue; }
    const scr = s.Screen();
    const ok = scr.Nodes.filter((n) => n.Standing === 'Reachable');
    const rest = ok.find((n) => n.Kind === 'Rest');
    const pick = rest || ok.sort((a, b) => b.Row - a.Row)[0];
    if (!pick) break;
    s.Step(pick.Id, 'Rest');
    log.push(pick.Kind[0] + st.Hp);
  }
  const st = s.State;
  console.log(seed, st.Profile.Layer, st.Phase, 'HP', st.Hp, log.join(' '), err);
}
