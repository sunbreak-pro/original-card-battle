// Helpers shared by the exploration tests: the ones ExplorationScreenTests.cs exposes as internal
// static (Explorable, Step, SeedWhere, WalkToBoss, WalkToLayer) and ExplorationSessionTests' RouteTo.
import assert from 'node:assert/strict';
import { RestChoice, RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { ExplorationSession, RunStart } from '../../src/bridge/explorationScript/ExplorationSession.js';
import { ScreenAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';

/** Reports the entry battle won, so the map takes steps. */
export function Explorable(session) {
  if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
  assert.equal(session.MapIsLive, true);
  return session;
}

/** Steps, and reports any battle the step opened as won. */
export function Step(session, nodeId, choice = RestChoice.Rest) {
  assert.equal(session.Step(nodeId, choice), true, `step to ${nodeId}`);
  if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
}

/** The first seed whose explorable opening frame passes the test. */
export function SeedWhere(check) {
  for (let seed = 1n; seed <= 200n; seed++) {
    const session = Explorable(new ExplorationSession(new RunStart({ Seed: seed })));
    if (check(session)) return session;
  }
  assert.fail('no seed in 1..200 passes');
}

/** Walks every node on the way to the boss straight down, winning each battle. */
export function WalkToBoss(session) {
  Explorable(session);
  while (session.State.Phase === RunPhase.Exploring) {
    const map = session.State.Map;
    const row = map.Node(session.State.CurrentNodeId).Row;
    const next = map.Successors(session.State.CurrentNodeId).find((id) => map.Node(id).Row === row + 1);
    Step(session, next);
  }
}

/** Clears layers straight down until the given layer's entry is reported. */
export function WalkToLayer(session, layer) {
  Explorable(session);
  while (session.State.Profile.Layer < layer) {
    WalkToBoss(session);
    assert.equal(session.State.Phase, RunPhase.LayerCleared);
    assert.equal(session.Act(ScreenAction.GoToInterlude), true);
    assert.equal(session.Act(ScreenAction.Descend), true);
    Explorable(session);
  }
}

/** A breadth-first route over the map's links, excluding the start (ExplorationSessionTests.RouteTo). */
export function RouteTo(map, from, to) {
  const previous = new Map();
  const queue = [from];
  const seen = new Set([from]);
  while (queue.length > 0) {
    const current = queue.shift();
    if (current === to) break;
    for (const next of map.Neighbours(current)) {
      if (map.Node(next).Kind === NodeKind.Boss && next !== to) continue;
      if (seen.has(next)) continue;
      seen.add(next);
      previous.set(next, current);
      queue.push(next);
    }
  }
  const route = [];
  for (let at = to; at !== from; at = previous.get(at)) route.push(at);
  route.reverse();
  return route;
}
