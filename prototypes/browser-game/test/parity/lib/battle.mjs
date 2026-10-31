// The battle every battle-based suite replays (oracle/src/BattleRun.cs, docs/PARITY_SHAPES.md "Suites").
import { arr } from './cs.mjs';

export const BATTLE_MODULES = [
  'src/core/battle/TurnLoop.js',
  'src/core/battle/IRng.js',
  'src/core/battle/Enemies.js',
  'src/core/battle/Constants.js',
  'src/core/battle/Types.js',
  'src/sim/Decks.js',
  'src/sim/GreedyPlayer.js',
];

/** The core's battle API, by C# name. Throws Missing when a module is not there yet. */
export function battleApi(use) {
  return {
    ...use('src/core/battle/TurnLoop.js', 'TurnLoop', 'BattleSetup', 'PlayRefusal'),
    ...use('src/core/battle/IRng.js', 'SeededRng'),
    ...use('src/core/battle/Enemies.js', 'Enemies'),
    ...use('src/core/battle/Constants.js', 'Constants'),
    ...use('src/core/battle/Types.js', 'GameResult', 'BattlePhase'),
    ...use('src/sim/Decks.js', 'Decks'),
  };
}

export function greedyApi(use) {
  return use('src/sim/GreedyPlayer.js', 'GreedyPlayer', 'Positioning');
}

/** A deck of BattleRun.DeckList (or any Decks deck) by id. */
export function deckById(T, id) {
  const { Decks } = T;
  for (const d of arr(Decks.Archetypes)) if (d.Id === id) return d;
  for (const f of ['Initial', 'CostOneOnly', 'InitialForty', 'Plain']) {
    const d = Decks[f]();
    if (d.Id === id) return d;
  }
  for (const d of arr(Decks.NoStanceTwins())) if (d.Id === id) return d;
  throw new Error(`harness: no deck "${id}"`);
}

/** BattleSpec.Setup(): Runner.Setup with every argument spelled out. */
export function setupFor(T, input) {
  const enemies = input.Enemies.map((id) => T.Enemies.ById(id));
  const deck = deckById(T, input.Deck);
  return new T.BattleSetup(
    enemies[0], deck.Build(), input.FieldCells,
    T.Constants.StartGap, T.Constants.PlayerStartCell, T.Constants.PlayerMaxHp, T.Constants.BaseMaxStamina,
    enemies.length > 1 ? enemies.slice(1) : null,
    null, null, 0, 0, 0);
}

/** BattleRun.Fight: the greedy battle, every call with its StepResult. Returns { steps, final, error }. */
export function fightGreedy(T, G, input) {
  const steps = [];
  const decisions = [];
  let final = null;
  let error = null;
  try {
    const ongoing = T.GameResult.Ongoing;
    const playerAction = T.BattlePhase.PlayerAction;
    const player = new G.GreedyPlayer(G.Positioning.Neutral, 0);
    const rng = new T.SeededRng(input.Seed);
    let r = T.TurnLoop.Start(setupFor(T, input), rng);
    steps.push({ Call: 'Start', Result: r });
    let state = r.State;
    while (state.Result === ongoing && state.Turn < input.TurnLimit) {
      r = T.TurnLoop.BeginPlayerTurn(state, rng);
      steps.push({ Call: 'BeginPlayerTurn', Result: r });
      state = r.State;
      if (state.Result !== ongoing) break;
      decisions.push(steps.length - 1);

      const step = player.ChooseFreeStep(state);
      if (step !== 0) {
        r = T.TurnLoop.TakeFreeStep(state, step);
        steps.push({ Call: 'TakeFreeStep', Direction: step, Result: r });
        state = r.State;
        if (state.Result === ongoing && state.Phase === playerAction) decisions.push(steps.length - 1);
      }

      let played = 0;
      while (state.Result === ongoing) {
        const choice = player.Choose(state, played);
        if (choice == null) break;
        const refusal = T.TurnLoop.CanPlay(state, choice.InstanceId, choice.Target);
        if (refusal !== T.PlayRefusal.None) {
          const e = new Error(`The greedy player chose an illegal play: ${choice.InstanceId} at ${choice.Target} (${refusal}).`);
          e.name = 'InvalidOperationException';
          throw e;
        }
        r = T.TurnLoop.PlayCard(state, choice.InstanceId, rng, choice.Target);
        steps.push({ Call: 'PlayCard', InstanceId: choice.InstanceId, Target: choice.Target, Value: choice.Value, Result: r });
        state = r.State;
        played++;
        if (state.Result === ongoing && state.Phase === playerAction) decisions.push(steps.length - 1);
      }

      if (state.Result !== ongoing) break;
      r = T.TurnLoop.EndTurn(state, rng);
      steps.push({ Call: 'EndTurn', Result: r });
      state = r.State;
    }
    final = state;
  } catch (e) {
    if (e && e.name === 'Missing') throw e;
    error = e;
  }
  return { steps, final, error, decisions };
}

/** Replays a recorded call list (previews / scripts) on the port. Returns { steps, error, decisions }. */
export function replayCalls(T, input) {
  const steps = [];
  const decisions = [];
  let error = null;
  try {
    const ongoing = T.GameResult.Ongoing;
    const playerAction = T.BattlePhase.PlayerAction;
    const rng = new T.SeededRng(input.Seed);
    let state = null;
    for (const call of input.Calls) {
      let r;
      switch (call.Call) {
        case 'Start': r = T.TurnLoop.Start(setupFor(T, input), rng); break;
        case 'BeginPlayerTurn': r = T.TurnLoop.BeginPlayerTurn(state, rng); break;
        case 'TakeFreeStep': r = T.TurnLoop.TakeFreeStep(state, call.Direction); break;
        case 'PlayCard': r = T.TurnLoop.PlayCard(state, call.InstanceId, rng, call.Target); break;
        case 'EndTurn': r = T.TurnLoop.EndTurn(state, rng); break;
        default: throw new Error(`harness: unknown call ${call.Call}`);
      }
      steps.push({ call, result: r });
      state = r.State;
      const ok = state.Result === ongoing;
      if (call.Call === 'BeginPlayerTurn' && ok) decisions.push(steps.length - 1);
      if ((call.Call === 'TakeFreeStep' || call.Call === 'PlayCard') && ok && state.Phase === playerAction) decisions.push(steps.length - 1);
    }
  } catch (e) {
    if (e && e.name === 'Missing') throw e;
    error = e;
  }
  return { steps, error, decisions };
}

/** BattleStep.CallOnly(). */
export function callOnly(call) {
  const m = { Call: call.Call };
  if (call.InstanceId !== undefined) m.InstanceId = call.InstanceId;
  if (call.Target !== undefined) m.Target = call.Target;
  if (call.Direction !== undefined) m.Direction = call.Direction;
  return m;
}
