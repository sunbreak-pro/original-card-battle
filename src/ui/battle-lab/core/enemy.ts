// Enemy definition + deterministic AI + turn resolution (pure).

import type {
  EnemyAction,
  EnemyActionId,
  EnemyDef,
  EnemyOutcome,
} from "./types";
import { ENEMY_MAX_HP, MID_INDEX, RANGE_LABEL } from "./constants";
import { computeAttackDamage, indexToRange, shiftDistance } from "./combat";

export const ENEMY_DEF: EnemyDef = {
  name: "長柄の歪み兵",
  maxHp: ENEMY_MAX_HP,
};

export const ENEMY_ACTIONS: Readonly<Record<EnemyActionId, EnemyAction>> = {
  sweep: {
    id: "sweep",
    name: "薙ぎ払い",
    type: "attack",
    cost: 5,
    effectiveRange: "mid",
    basePower: 8,
    shift: 0,
    towardMid: false,
    description: "キルゾーン（中）の主力。",
  },
  reach_thrust: {
    id: "reach_thrust",
    name: "穂先の突き",
    type: "attack",
    cost: 4,
    effectiveRange: "far",
    basePower: 3,
    shift: 0,
    towardMid: false,
    description: "遠間から届くが軽い牽制。",
  },
  shove: {
    id: "shove",
    name: "石突きの押し込み",
    type: "attack",
    cost: 3,
    effectiveRange: "close",
    basePower: 2,
    shift: 1,
    towardMid: false,
    description: "懐の相手を中間合いへ押し戻す。",
  },
  reposition: {
    id: "reposition",
    name: "間合い取り直し",
    type: "move",
    cost: 2,
    effectiveRange: null,
    basePower: 0,
    shift: 0,
    towardMid: true,
    description: "中間合いへ取り直す。",
  },
};

const CLOSE_INDEX = 0;
const FAR_INDEX = 2;

/**
 * Deterministic, one-action-per-turn AI. Returns null when the enemy can only
 * catch its breath (no affordable action). See plan §敵 AI.
 */
export function chooseEnemyAction(
  distanceIndex: number,
  enemyStamina: number,
): EnemyAction | null {
  const can = (a: EnemyAction): boolean => enemyStamina >= a.cost;

  if (distanceIndex <= CLOSE_INDEX) {
    // Close: player is inside the haft. Only the weak shove, then push to mid.
    if (can(ENEMY_ACTIONS.shove)) return ENEMY_ACTIONS.shove;
    if (can(ENEMY_ACTIONS.reposition)) return ENEMY_ACTIONS.reposition;
    return null;
  }
  if (distanceIndex >= FAR_INDEX) {
    // Far: the spear tip still reaches; else step back toward mid.
    if (can(ENEMY_ACTIONS.reach_thrust)) return ENEMY_ACTIONS.reach_thrust;
    if (can(ENEMY_ACTIONS.reposition)) return ENEMY_ACTIONS.reposition;
    return null;
  }
  // Mid: kill zone. Sweep, else the cheaper thrust, else rest.
  if (can(ENEMY_ACTIONS.sweep)) return ENEMY_ACTIONS.sweep;
  if (can(ENEMY_ACTIONS.reach_thrust)) return ENEMY_ACTIONS.reach_thrust;
  return null;
}

function resolveShift(action: EnemyAction, distanceIndex: number): number {
  if (action.towardMid) return Math.sign(MID_INDEX - distanceIndex);
  return action.shift;
}

/** Resolve one enemy turn into a structured outcome the reducer can apply. */
export function resolveEnemyTurn(
  distanceIndex: number,
  enemyStamina: number,
  playerGuard: number,
): EnemyOutcome {
  const action = chooseEnemyAction(distanceIndex, enemyStamina);
  if (!action) {
    return {
      action: null,
      rawDamage: 0,
      damage: 0,
      newGuard: playerGuard,
      newDistanceIndex: distanceIndex,
      staminaSpent: 0,
      logText: `敵「${ENEMY_DEF.name}」は息を整えた（休む）。`,
    };
  }

  let rawDamage = 0;
  if (action.type === "attack" && action.effectiveRange) {
    rawDamage = computeAttackDamage(
      action.basePower,
      action.effectiveRange,
      enemyStamina,
      distanceIndex,
    );
  }
  const damage = Math.max(0, rawDamage - playerGuard);
  const newGuard = Math.max(0, playerGuard - rawDamage);
  const shift = resolveShift(action, distanceIndex);
  const newDistanceIndex = shiftDistance(distanceIndex, shift);

  let logText = `敵「${ENEMY_DEF.name}」の${action.name}。`;
  if (action.type === "attack") {
    logText += ` ${damage} ダメージ`;
    if (rawDamage > damage)
      logText += `（ガードで ${rawDamage - damage} 軽減）`;
    logText += "。";
  }
  if (shift !== 0) {
    logText += ` 間合いが「${RANGE_LABEL[indexToRange(newDistanceIndex)]}」に。`;
  }

  return {
    action,
    rawDamage,
    damage,
    newGuard,
    newDistanceIndex,
    staminaSpent: action.cost,
    logText,
  };
}
