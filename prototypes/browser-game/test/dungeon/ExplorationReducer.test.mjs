// Tests for src/core/dungeon/ExplorationReducer.js — ports DungeonCore.Tests/ExplorationReducerTests.cs,
// TrainingMarkTests.cs and RuleChecksTests.cs, and replays six seeded lives against the C# oracle
// (fixtures/walks.json).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { enumValues } from '../../src/shared/cs.js';
import { ExplorationReducer, ExplorationRuleException } from '../../src/core/dungeon/ExplorationReducer.js';
import { RunPhase, RestChoice } from '../../src/core/dungeon/ExplorationState.js';
import { SevenLayers } from '../../src/core/dungeon/SevenLayers.js';
import { RunLoadout, ReliefItem } from '../../src/core/dungeon/RunLoadout.js';
import { NodeKind, NodeKindTokens } from '../../src/core/dungeon/NodeKind.js';
import { Miasma } from '../../src/core/dungeon/Miasma.js';
import { SeededRng, RngExtensions } from '../../src/core/dungeon/IRng.js';
import { LayerEncounters } from '../../src/core/dungeon/LayerEncounters.js';
import { fixture, caught, expectedError, layerSeed } from './_helpers.mjs';

const PlayerMaxHp = 50;

function EnterLayer(layer, { seed = 11n, loadout = null, hp = PlayerMaxHp, stamina = 10 } = {}) {
  const profile = SevenLayers.Of(layer);
  return ExplorationReducer.Enter(profile, profile.Map(seed), loadout ?? RunLoadout.Empty, hp, PlayerMaxHp, stamina);
}

/** Always steps to the first node of the next row: a shortest route by construction. */
function WalkToBoss(start) {
  let state = start;
  while (state.Phase === RunPhase.Exploring) {
    const currentRow = state.Map.Node(state.CurrentNodeId).Row;
    const next = state.Map.Successors(state.CurrentNodeId).find((id) => state.Map.Node(id).Row === currentRow + 1);
    state = ExplorationReducer.Step(state, next, RestChoice.Train);
  }
  return state;
}

function WalkStraightDown(profile, seed, loadout, hp, maxHp, stamina) {
  return WalkToBoss(ExplorationReducer.Enter(profile, profile.Map(seed), loadout, hp, maxHp, stamina));
}

function WalkTo(start, target, choice) {
  const previous = new Map();
  const queue = [start.CurrentNodeId];
  const seen = new Set([start.CurrentNodeId]);
  while (queue.length) {
    const current = queue.shift();
    if (current === target) break;
    for (const next of start.Map.Neighbours(current)) {
      if (seen.has(next)) continue;
      seen.add(next);
      previous.set(next, current);
      queue.push(next);
    }
  }
  const route = [];
  for (let at = target; at !== start.CurrentNodeId; at = previous.get(at)) route.push(at);
  route.reverse();
  let state = start;
  for (const id of route) state = ExplorationReducer.Step(state, id, choice);
  return state;
}

function WalkThroughLayerSeven(profile, map, middleId, choice) {
  let state = ExplorationReducer.Enter(profile, map, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, 68);
  state = ExplorationReducer.Step(state, middleId, choice);
  return ExplorationReducer.Step(state, map.BossId, choice);
}

test('entering a layer spends the entry node\'s time unit', () => {
  const state = EnterLayer(3);
  assert.equal(state.TimeLeft, SevenLayers.Of(3).TimeLimit - 1);
  assert.equal(state.MiasmaPercent, SevenLayers.Of(3).Density);
  assert.equal(state.HasResolved(state.Map.EntryId), true);
  assert.equal(state.Phase, RunPhase.Exploring);
});

test('a new node costs one time unit and one layer\'s worth of miasma', () => {
  const before = EnterLayer(5);
  const after = ExplorationReducer.Step(before, before.ReachableUnresolved()[0], RestChoice.Train);
  assert.equal(after.TimeLeft, before.TimeLeft - 1);
  assert.equal(after.MiasmaPercent, before.MiasmaPercent + SevenLayers.Of(5).Density);
});

test('walking back over a resolved node is free; both sides of a fork cost one each', () => {
  const start = EnterLayer(1);
  const entry = start.CurrentNodeId;
  const forward = start.ReachableUnresolved()[0];
  const stepped = ExplorationReducer.Step(start, forward, RestChoice.Train);
  const back = ExplorationReducer.Step(stepped, entry);
  assert.equal(back.CurrentNodeId, entry);
  assert.equal(back.TimeLeft, stepped.TimeLeft);
  assert.equal(back.MiasmaPercent, stepped.MiasmaPercent);

  const fork = start.ReachableUnresolved().slice(0, 2);
  assert.equal(fork.length, 2);
  const left = ExplorationReducer.Step(start, fork[0], RestChoice.Train);
  const home = ExplorationReducer.Step(left, entry);
  const right = ExplorationReducer.Step(home, fork[1], RestChoice.Train);
  assert.equal(right.TimeLeft, start.TimeLeft - 2);
  assert.equal(right.MiasmaPercent, start.MiasmaPercent + 2 * SevenLayers.Of(1).Density);
});

test('stepping somewhere that does not touch the current node throws', () => {
  const state = EnterLayer(1);
  const far = state.Map.Nodes.filter((n) => n.Row === state.Map.RowCount - 1).at(-1).Id;
  assert.throws(() => ExplorationReducer.Step(state, far), ExplorationRuleException);
});

test('stamina comes back by three for every time unit spent, never past the maximum', () => {
  const start = EnterLayer(1, { stamina: 0 });
  assert.equal(start.Stamina, Miasma.FieldRecovery);
  const next = ExplorationReducer.Step(start, start.ReachableUnresolved()[0], RestChoice.Train);
  assert.equal(next.Stamina, Math.min(next.MaxStamina, 2 * Miasma.FieldRecovery));
  const state = EnterLayer(6, { stamina: 10 });
  assert.ok(state.Stamina <= state.MaxStamina);
});

test('running out of time ends the layer, and still leads to the interlude', () => {
  let state = EnterLayer(7, { seed: 4n });
  const entry = state.CurrentNodeId;
  const middle = state.Map.Row(1).map((n) => n.Id);
  assert.equal(middle.length, 4);
  for (const id of middle) {
    state = ExplorationReducer.Step(state, id, RestChoice.Train);
    if (state.Phase !== RunPhase.Exploring) break;
    state = ExplorationReducer.Step(state, entry);
  }
  assert.equal(state.TimeLeft, 0);
  assert.equal(state.Phase, RunPhase.PushedOut);
  assert.equal(state.HasResolved(state.Map.BossId), false);
  assert.doesNotThrow(() => ExplorationReducer.Interlude(state));
});

test('no new node can be taken without time left', () => {
  const zeroed = EnterLayer(7, { seed: 4n }).With({ timeLeft: 0 });
  assert.throws(() => ExplorationReducer.Step(zeroed, zeroed.ReachableUnresolved()[0]), ExplorationRuleException);
});

test('beating the layer boss opens the interlude; the interlude gives back 30%', () => {
  const state = WalkStraightDown(SevenLayers.Of(2), 9n, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
  assert.equal(state.Phase, RunPhase.LayerCleared);
  const rest = ExplorationReducer.Interlude(state);
  assert.equal(rest.Phase, RunPhase.Interlude);
  assert.equal(rest.MiasmaPercent, state.MiasmaPercent);
  assert.equal(rest.Stamina, rest.MaxStamina);

  const cleared = WalkStraightDown(SevenLayers.Of(1), 9n, RunLoadout.Empty, 10, PlayerMaxHp, 10);
  assert.equal(ExplorationReducer.Interlude(cleared).Hp, Math.min(PlayerMaxHp, cleared.Hp + 15));
  assert.equal(ExplorationReducer.InterludeHeal(50), 15);
  assert.equal(ExplorationReducer.RestHeal(50), 8, '7.5 rounds away from zero');
  assert.equal(ExplorationReducer.RestHeal(30), 5, '4.5 rounds away from zero');
});

test('beating the last layer\'s boss completes the run', () => {
  const state = WalkStraightDown(SevenLayers.Of(7), 5n, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
  assert.equal(state.Phase, RunPhase.Completed);
  assert.equal(state.IsOver, true);
});

test('the gauge reaching one hundred ends the life, and outranks the boss', () => {
  const profile = SevenLayers.Of(7);
  const state = ExplorationReducer.Enter(profile, profile.Map(3n), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, 90);
  assert.equal(state.MiasmaPercent, 97);
  assert.equal(state.Phase, RunPhase.Exploring);
  const dead = ExplorationReducer.Step(state, state.ReachableUnresolved()[0], RestChoice.Train);
  assert.equal(dead.MiasmaPercent, 100);
  assert.equal(dead.Phase, RunPhase.MiasmaDeath);
  assert.equal(dead.IsOver, true);

  let s = ExplorationReducer.Enter(profile, profile.Map(3n), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, 79);
  while (s.Phase === RunPhase.Exploring) s = ExplorationReducer.Step(s, s.ReachableUnresolved()[0], RestChoice.Train);
  assert.equal(s.MiasmaPercent, 100);
  assert.equal(s.Phase, RunPhase.MiasmaDeath);
});

test('a rest node heals or trains, never both', () => {
  const profile = SevenLayers.Of(4);
  const map = profile.Map(2n);
  const restNode = map.Nodes.find((n) => n.Kind === NodeKind.Rest);
  const start = ExplorationReducer.Enter(profile, map, RunLoadout.Empty, 10, PlayerMaxHp, 1);
  const atRest = WalkTo(start, restNode.Id, RestChoice.Rest);
  const atTrain = WalkTo(start, restNode.Id, RestChoice.Train);
  assert.equal(atRest.Hp, atTrain.Hp + 8);
  assert.equal(atRest.TempMaxStaminaMod, ExplorationReducer.RestMaxStaminaBonus);
  assert.equal(atRest.Stamina, atRest.MaxStamina);
  assert.equal(atRest.TrainingMarks, 0);
  assert.equal(atTrain.TrainingMarks, 1);
  assert.equal(atTrain.TempMaxStaminaMod, 0);
  assert.equal(atRest.TimeLeft, atTrain.TimeLeft);
  assert.equal(atRest.MiasmaPercent, atTrain.MiasmaPercent);
});

test('a consumable pulls the gauge back without spending time', () => {
  const carried = new RunLoadout(null, [ReliefItem.Gauge('joka_no_ko', 10)]);
  const profile = SevenLayers.Of(6);
  const state = ExplorationReducer.Enter(profile, profile.Map(11n), carried, PlayerMaxHp, PlayerMaxHp, 10, 40);
  assert.equal(state.MiasmaPercent, 45);
  const after = ExplorationReducer.UseConsumable(state, 0);
  assert.equal(after.MiasmaPercent, 35);
  assert.equal(after.TimeLeft, state.TimeLeft);
  assert.equal(after.Loadout.Consumables.length, 0);
});

test('HP and stamina carry across battles but the gauge does not; zero HP falls', () => {
  const state = EnterLayer(3);
  const after = ExplorationReducer.ReportBattle(state, 12, 2);
  assert.equal(after.Hp, 12);
  assert.equal(after.Stamina, 2);
  assert.equal(after.MiasmaPercent, state.MiasmaPercent);
  assert.equal(after.MaxStamina, state.MaxStamina);
  const fallen = ExplorationReducer.ReportBattle(state, 0, 0);
  assert.equal(fallen.Phase, RunPhase.Fallen);
  assert.equal(fallen.IsOver, true);
  const clamped = ExplorationReducer.ReportBattle(state, 999, 999);
  assert.equal(clamped.Hp, PlayerMaxHp);
  assert.equal(clamped.Stamina, state.MaxStamina);
});

test('ending the life stops the run where it stands, also from the interlude; nothing moves after', () => {
  const state = EnterLayer(3);
  const ended = ExplorationReducer.EndLife(state);
  assert.equal(ended.Phase, RunPhase.Survived);
  assert.equal(ended.IsOver, true);
  assert.equal(ended.TimeLeft, state.TimeLeft);
  assert.equal(ended.MiasmaPercent, state.MiasmaPercent);
  assert.equal(ended.Hp, state.Hp);

  const cleared = WalkStraightDown(SevenLayers.Of(1), 9n, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
  assert.equal(ExplorationReducer.EndLife(ExplorationReducer.Interlude(cleared)).Phase, RunPhase.Survived);

  const over = ExplorationReducer.EndLife(EnterLayer(1));
  const next = over.ReachableUnresolved()[0];
  assert.throws(() => ExplorationReducer.EndLife(over), ExplorationRuleException);
  assert.throws(() => ExplorationReducer.Step(over, next), ExplorationRuleException);
  assert.throws(() => ExplorationReducer.Interlude(over), ExplorationRuleException);
  const fallen = ExplorationReducer.ReportBattle(EnterLayer(1), 0, 0);
  assert.throws(() => ExplorationReducer.EndLife(fallen), ExplorationRuleException);
});

test('a straight run down all seven layers ends at 89 percent', () => {
  let state = ExplorationReducer.Enter(SevenLayers.Of(1), SevenLayers.Of(1).Map(1n), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
  state = WalkToBoss(state);
  const expectedAfterLayer = [5, 10, 20, 32, 48, 68, 89];
  const expectedMaxStamina = [10, 10, 9, 9, 8, 7, 6];
  assert.equal(state.MiasmaPercent, expectedAfterLayer[0]);
  for (let layer = 2; layer <= SevenLayers.Count; layer++) {
    const profile = SevenLayers.Of(layer);
    state = ExplorationReducer.Descend(ExplorationReducer.Interlude(state), profile, profile.Map(layer));
    state = WalkToBoss(state);
    assert.equal(state.MiasmaPercent, expectedAfterLayer[layer - 1], `after layer ${layer}`);
    assert.equal(state.MaxStamina, expectedMaxStamina[layer - 1], `after layer ${layer}`);
  }
  assert.equal(state.Phase, RunPhase.Completed);
  assert.equal(state.MiasmaPercent, SevenLayers.BareMinimumMiasma());
});

test('resting on the last layer arrives with more stamina; a detour there costs none', () => {
  const profile = SevenLayers.Of(7);
  const map = profile.Map(5n);
  const rest = map.Nodes.filter((n) => n.Kind === NodeKind.Rest);
  assert.equal(rest.length, 1);
  const trained = WalkThroughLayerSeven(profile, map, rest[0].Id, RestChoice.Train);
  const rested = WalkThroughLayerSeven(profile, map, rest[0].Id, RestChoice.Rest);
  assert.equal(trained.MiasmaPercent, 89);
  assert.equal(rested.MiasmaPercent, 89);
  assert.equal(trained.MaxStamina, 6);
  assert.equal(rested.MaxStamina, 8);
  assert.equal(rested.TrainingMarks, 0);
  assert.equal(trained.TrainingMarks, 1);

  const straight = WalkThroughLayerSeven(profile, map, map.Row(1)[0].Id, RestChoice.Train);
  let detoured = ExplorationReducer.Enter(profile, map, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, 68);
  const middle = map.Row(1).map((n) => n.Id);
  detoured = ExplorationReducer.Step(detoured, middle[0], RestChoice.Train);
  detoured = ExplorationReducer.Step(detoured, map.EntryId);
  detoured = ExplorationReducer.Step(detoured, middle[1], RestChoice.Train);
  detoured = ExplorationReducer.Step(detoured, map.BossId, RestChoice.Train);
  assert.equal(detoured.MiasmaPercent, 96);
  assert.equal(detoured.MaxStamina, straight.MaxStamina);
  assert.equal(detoured.Phase, RunPhase.Completed);
});

test('the same seeds give the same run', () => {
  const first = WalkStraightDown(SevenLayers.Of(4), 77n, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
  const second = WalkStraightDown(SevenLayers.Of(4), 77n, RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
  assert.equal(second.Map.Fingerprint(), first.Map.Fingerprint());
  assert.equal(second.MiasmaPercent, first.MiasmaPercent);
  assert.equal(second.TimeLeft, first.TimeLeft);
  assert.equal(second.Hp, first.Hp);
});

// ---------------------------------------------------------------- TrainingMarkTests

function pathTo(state, kind) {
  const map = state.Map;
  const from = new Map([[state.CurrentNodeId, -1]]);
  const queue = [state.CurrentNodeId];
  let target = -1;
  while (queue.length && target < 0) {
    const node = queue.shift();
    if (map.Node(node).Kind === kind && !state.HasResolved(node)) { target = node; break; }
    for (const next of map.Neighbours(node)) {
      if (from.has(next)) continue;
      from.set(next, node);
      queue.push(next);
    }
  }
  assert.notEqual(target, -1);
  const path = [];
  for (let node = target; node !== state.CurrentNodeId; node = from.get(node)) path.push(node);
  return path.reverse();
}

function trained() {
  const profile = SevenLayers.Of(1);
  let state = ExplorationReducer.Enter(profile, profile.Map(11n), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
  const path = pathTo(state, NodeKind.Rest);
  for (let i = 0; i < path.length; i++) state = ExplorationReducer.Step(state, path[i], i === path.length - 1 ? RestChoice.Train : RestChoice.Rest);
  return state;
}

test('spending a mark counts the marks down and nothing else', () => {
  const t = trained();
  assert.equal(t.TrainingMarks, 1);
  const spent = ExplorationReducer.SpendTrainingMark(t);
  assert.equal(spent.TrainingMarks, 0);
  assert.equal(spent.TimeLeft, t.TimeLeft);
  assert.equal(spent.MiasmaPercent, t.MiasmaPercent);
  assert.equal(spent.Hp, t.Hp);
  assert.equal(spent.CurrentNodeId, t.CurrentNodeId);
  assert.equal(spent.Phase, t.Phase);
});

test('with no mark left, or after the life ends, the spend is refused', () => {
  const fresh = EnterLayer(1);
  assert.equal(fresh.TrainingMarks, 0);
  assert.throws(() => ExplorationReducer.SpendTrainingMark(fresh), ExplorationRuleException);
  assert.throws(() => ExplorationReducer.SpendTrainingMark(ExplorationReducer.EndLife(trained())), ExplorationRuleException);
});

// ---------------------------------------------------------------- RuleChecksTests

function* Walks() {
  for (let layer = 1; layer <= SevenLayers.Count; layer++) {
    for (let seed = 1n; seed <= 4n; seed++) {
      const rng = new SeededRng(seed * 31n + BigInt(layer));
      const profile = SevenLayers.Of(layer);
      let state = ExplorationReducer.Enter(profile, profile.Map(seed), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
      yield state;
      for (let guard = 0; state.Phase === RunPhase.Exploring && guard < 200; guard++) {
        const options = state.Map.Neighbours(state.CurrentNodeId).filter((id) => ExplorationReducer.CanStep(state, id));
        if (options.length === 0) break;
        state = ExplorationReducer.Step(state, options[RngExtensions.NextInt(rng, 0, options.length)]);
        yield state;
      }
    }
  }
}

const takes = (move) => {
  try { move(); return true; } catch (e) { if (e instanceof ExplorationRuleException) return false; throw e; }
};

test('CanStep says yes exactly when Step takes the node', () => {
  for (const state of Walks()) {
    for (const node of state.Map.Nodes) {
      assert.equal(ExplorationReducer.CanStep(state, node.Id), takes(() => ExplorationReducer.Step(state, node.Id)),
        `layer ${state.Profile.Layer}, ${state.Phase}, at ${state.CurrentNodeId}, node ${node.Id}`);
    }
  }
});

test('CanInterlude says yes exactly when Interlude applies', () => {
  const phases = new Set();
  const profile = SevenLayers.Of(1);
  const fallen = ExplorationReducer.ReportBattle(
    ExplorationReducer.Enter(profile, profile.Map(1n), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10), 0, 0);
  for (const state of [...Walks(), fallen]) {
    phases.add(state.Phase);
    assert.equal(ExplorationReducer.CanInterlude(state), takes(() => ExplorationReducer.Interlude(state)));
  }
  for (const p of [RunPhase.Exploring, RunPhase.LayerCleared, RunPhase.PushedOut, RunPhase.Fallen]) assert.ok(phases.has(p), p);
});

test('IsCombat says yes exactly for the kinds that draw a fight', () => {
  for (const kind of enumValues(NodeKind)) {
    for (const table of LayerEncounters.All) {
      assert.equal(table.PoolFor(kind).length > 0, NodeKindTokens.IsCombat(kind), `layer ${table.Layer} ${kind}`);
    }
  }
});

// ---------------------------------------------------------------- C# oracle lives

function snap(action, s) {
  const resolved = [...s.Resolved].sort((a, b) => a - b).join('.');
  return `${action}|L${s.Profile.Layer}|at ${s.CurrentNodeId}|t ${s.TimeLeft}|m ${s.MiasmaPercent}|hp ${s.Hp}/${s.MaxHp}` +
    `|st ${s.Stamina}/${s.MaxStamina}|tmp ${s.TempMaxStaminaMod}|mk ${s.TrainingMarks}|${s.Phase}|r ${resolved}` +
    `|c ${s.Loadout.Consumables.length}|d ${s.EffectiveDensity}|over ${s.IsOver ? 'True' : 'False'}` +
    `|reach ${s.ReachableUnresolved().join('.')}`;
}

/** The oracle's Life() driver, line for line (scratch-csharp/dungeon/Program.cs). */
function life(seed, loadout, startHp, endChance, avoidBoss) {
  const lines = [];
  const policy = new SeededRng(BigInt.asUintN(64, seed * 7919n + 13n));
  const first = SevenLayers.Of(1);
  let state = ExplorationReducer.Enter(first, first.Map(layerSeed(seed, 1)), loadout, startHp, 50, 10);
  lines.push(snap('enter', state));
  for (let guard = 0; guard < 400; guard++) {
    if (state.IsOver) break;
    if (ExplorationReducer.CanInterlude(state)) {
      state = ExplorationReducer.Interlude(state);
      lines.push(snap('interlude', state));
      if (state.Profile.IsLast || RngExtensions.NextChance(policy, endChance)) {
        state = ExplorationReducer.EndLife(state);
        lines.push(snap('endlife', state));
        break;
      }
      const next = SevenLayers.Of(state.Profile.Layer + 1);
      state = ExplorationReducer.Descend(state, next, next.Map(layerSeed(seed, next.Layer)));
      lines.push(snap('descend', state));
      continue;
    }
    if (state.Phase !== RunPhase.Exploring) break;

    if (state.TrainingMarks > 0 && RngExtensions.NextChance(policy, 0.5)) {
      state = ExplorationReducer.SpendTrainingMark(state);
      lines.push(snap('mark', state));
    }
    if (state.Loadout.Consumables.length > 0 && state.MiasmaPercent >= 10 && RngExtensions.NextChance(policy, 0.3)) {
      state = ExplorationReducer.UseConsumable(state, RngExtensions.NextInt(policy, 0, state.Loadout.Consumables.length));
      lines.push(snap('use', state));
    }

    const s0 = state;
    const options = s0.Map.Neighbours(s0.CurrentNodeId).filter((id) => ExplorationReducer.CanStep(s0, id));
    if (options.length === 0) break;
    let fresh = options.filter((id) => !s0.HasResolved(id));
    const nonBoss = fresh.filter((id) => id !== s0.Map.BossId);
    if (avoidBoss && nonBoss.length > 0) fresh = nonBoss;
    const pool = fresh.length > 0 && RngExtensions.NextChance(policy, 0.8) ? fresh : options;
    const target = pool[RngExtensions.NextInt(policy, 0, pool.length)];
    const choice = RngExtensions.NextChance(policy, 0.5) ? RestChoice.Rest : RestChoice.Train;
    const wasNew = !state.HasResolved(target);
    state = ExplorationReducer.Step(state, target, choice);
    lines.push(snap(`step ${target} ${choice}`, state));

    const kind = state.Map.Node(target).Kind;
    if (wasNew && NodeKindTokens.IsCombat(kind) && state.Phase !== RunPhase.MiasmaDeath) {
      const hpLoss = 4 + RngExtensions.NextInt(policy, 0, 9);
      const stLoss = RngExtensions.NextInt(policy, 0, 6);
      state = ExplorationReducer.ReportBattle(state, state.Hp - hpLoss, state.Stamina - stLoss);
      lines.push(snap('battle', state));
    }
  }
  return lines;
}

test('24 seeded lives replay the C# run line for line', () => {
  // Between them they reach every phase: cleared, pushed out, interlude, fallen, miasma death,
  // completed and survived, with consumables used and training marks spent on the way.
  const walks = fixture('walks');
  assert.equal(walks.length, 24);
  const phases = new Set();
  for (const w of walks) {
    const seed = BigInt(w.seed);
    const loadout = seed % 2n === 0n
      ? new RunLoadout([ReliefItem.Density('bosho_no_men', 1)], [ReliefItem.Gauge('joka_no_ko', 10), ReliefItem.Gauge('joka_no_ko', 10)])
      : RunLoadout.Empty;
    const startHp = seed % 3n === 0n ? 22 : 50;
    const endChance = seed <= 6n ? 0.1 : 0.0;
    const avoidBoss = seed % 4n === 1n;
    const lines = life(seed, loadout, startHp, endChance, avoidBoss);
    assert.deepEqual(lines, w.lines, `seed ${w.seed}`);
    for (const l of lines) phases.add(l.split('|')[9]);
  }
  assert.deepEqual([...phases].sort(), enumValues(RunPhase).slice().sort());
});

test('errors carry the C# messages', () => {
  const map = SevenLayers.Of(1).Map(1n);
  const state = ExplorationReducer.Enter(SevenLayers.Of(1), map, RunLoadout.Empty, 50, 50, 10);
  const ended = ExplorationReducer.EndLife(state);
  const zeroed = state.With({ timeLeft: 0 });
  const nextFresh = state.ReachableUnresolved()[0];
  const cases = {
    'reducer.notNeighbour': () => ExplorationReducer.Step(state, map.BossId),
    'reducer.noTime': () => ExplorationReducer.Step(zeroed, nextFresh),
    'reducer.over': () => ExplorationReducer.Step(ended, nextFresh),
    'reducer.interlude': () => ExplorationReducer.Interlude(state),
    'reducer.endTwice': () => ExplorationReducer.EndLife(ended),
    'reducer.noMark': () => ExplorationReducer.SpendTrainingMark(state),
    'reducer.useOver': () => ExplorationReducer.UseConsumable(ended, 0),
    'reducer.useEmpty': () => ExplorationReducer.UseConsumable(state, 0),
    'reducer.descend': () => ExplorationReducer.Descend(state, SevenLayers.Of(2), SevenLayers.Of(2).Map(1)),
    'reducer.enterMismatch': () => ExplorationReducer.Enter(SevenLayers.Of(2), map, RunLoadout.Empty, 50, 50, 10),
  };
  for (const [key, fn] of Object.entries(cases)) assert.equal(caught(fn), expectedError(key), key);
});
