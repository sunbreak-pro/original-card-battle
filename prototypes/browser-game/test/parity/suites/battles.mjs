// Mirrors oracle/src/Suites/Battles.cs: the greedy battle (src/sim/GreedyPlayer.js), every call and StepResult.
import { BATTLE_MODULES, battleApi, greedyApi, fightGreedy } from '../lib/battle.mjs';

export const imports = BATTLE_MODULES;

export function run(c, { use }) {
  const T = battleApi(use);
  const G = greedyApi(use);
  const { steps, final, error } = fightGreedy(T, G, c.input);
  const out = { Steps: steps };
  if (error) out.Error = error;
  if (final) {
    out.Result = final.Result;
    out.Turns = final.Turn;
  }
  return out;
}
