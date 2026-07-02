// Pure battle reducer. The enemy phase is resolved synchronously inside
// END_TURN so StrictMode double-invokes and effect races cannot desync it.

import type { BattleAction, BattleState, GameResult, LogEntry } from "./types";
import {
  HAND_SIZE,
  INITIAL_DISTANCE_INDEX,
  MAX_STAMINA,
  PLAYER_MAX_HP,
  RANGE_LABEL,
} from "./constants";
import {
  indexToRange,
  shiftDistance,
  staminaRecovery,
  computeAttackDamage,
} from "./combat";
import { createInitialDeck, drawToHandSize, shuffle } from "./cards";
import { ENEMY_DEF, resolveEnemyTurn } from "./enemy";

function appendLogs(
  source: Pick<BattleState, "log" | "logSeq">,
  texts: readonly string[],
): { log: LogEntry[]; logSeq: number } {
  const added = texts.map((text, i) => ({ id: source.logSeq + i, text }));
  return {
    log: [...source.log, ...added],
    logSeq: source.logSeq + texts.length,
  };
}

export function initState(): BattleState {
  const shuffled = shuffle(createInitialDeck());
  const { hand, drawPile, discardPile } = drawToHandSize(
    shuffled,
    [],
    [],
    HAND_SIZE,
  );
  const base: BattleState = {
    turn: 1,
    distanceIndex: INITIAL_DISTANCE_INDEX,
    playerHp: PLAYER_MAX_HP,
    playerStamina: MAX_STAMINA,
    playerGuard: 0,
    enemyHp: ENEMY_DEF.maxHp,
    enemyStamina: MAX_STAMINA,
    hand,
    drawPile,
    discardPile,
    log: [],
    logSeq: 0,
    result: "ongoing",
  };
  const { log, logSeq } = appendLogs(base, [
    `戦闘開始。間合いは「${RANGE_LABEL[indexToRange(base.distanceIndex)]}」。`,
  ]);
  return { ...base, log, logSeq };
}

function playCard(state: BattleState, instanceId: string): BattleState {
  if (state.result !== "ongoing") return state;
  const card = state.hand.find((c) => c.instanceId === instanceId);
  if (!card) return state;
  if (state.playerStamina < card.cost) return state;

  const logs: string[] = [];
  let enemyHp = state.enemyHp;
  let distanceIndex = state.distanceIndex;
  let playerGuard = state.playerGuard;

  if (card.type === "attack" && card.effectiveRange) {
    // Use stamina before paying the cost so the hand preview matches reality.
    const dmg = computeAttackDamage(
      card.basePower,
      card.effectiveRange,
      state.playerStamina,
      state.distanceIndex,
    );
    enemyHp = Math.max(0, enemyHp - dmg);
    logs.push(`「${card.name}」で ${dmg} ダメージ。`);
  } else if (card.type === "guard") {
    playerGuard += card.guard;
    logs.push(`「${card.name}」で受けを固めた（ガード +${card.guard}）。`);
  } else {
    logs.push(`「${card.name}」を使った。`);
  }

  if (card.shift !== 0) {
    distanceIndex = shiftDistance(distanceIndex, card.shift);
    logs.push(`間合いが「${RANGE_LABEL[indexToRange(distanceIndex)]}」に。`);
  }

  const playerStamina = state.playerStamina - card.cost;
  const hand = state.hand.filter((c) => c.instanceId !== instanceId);
  const discardPile = [...state.discardPile, card];

  let result: GameResult = state.result;
  if (enemyHp <= 0) {
    result = "won";
    logs.push(`敵「${ENEMY_DEF.name}」を打ち倒した。勝利。`);
  }

  const { log, logSeq } = appendLogs(state, logs);
  return {
    ...state,
    enemyHp,
    distanceIndex,
    playerGuard,
    playerStamina,
    hand,
    discardPile,
    result,
    log,
    logSeq,
  };
}

function endTurn(state: BattleState): BattleState {
  if (state.result !== "ongoing") return state;

  const logs: string[] = [];

  // 1. Discard the remaining hand.
  const discardAfterHand = [...state.discardPile, ...state.hand];

  // 2. Enemy recovers stamina for the current band.
  const enemyBand = indexToRange(state.distanceIndex);
  const enemyRecovery = staminaRecovery(enemyBand);
  const enemyStaminaAfterRecovery = Math.min(
    MAX_STAMINA,
    state.enemyStamina + enemyRecovery,
  );
  logs.push(
    `敵が間合い「${RANGE_LABEL[enemyBand]}」でスタミナ回復（+${enemyRecovery} → ${enemyStaminaAfterRecovery}）。`,
  );

  // 3. Enemy acts.
  const outcome = resolveEnemyTurn(
    state.distanceIndex,
    enemyStaminaAfterRecovery,
    state.playerGuard,
  );
  const enemyStamina = enemyStaminaAfterRecovery - outcome.staminaSpent;
  const playerHp = Math.max(0, state.playerHp - outcome.damage);
  const distanceIndex = outcome.newDistanceIndex;
  logs.push(outcome.logText);

  // 4. Defeat check.
  if (playerHp <= 0) {
    logs.push("力尽きた。敗北。");
    const { log, logSeq } = appendLogs(state, logs);
    return {
      ...state,
      hand: [],
      discardPile: discardAfterHand,
      enemyStamina,
      playerHp,
      playerGuard: outcome.newGuard,
      distanceIndex,
      result: "lost",
      log,
      logSeq,
    };
  }

  // 5. Next player turn: clear guard, recover stamina for the (possibly new)
  //    band, draw back up to hand size.
  const turn = state.turn + 1;
  const playerBand = indexToRange(distanceIndex);
  const playerRecovery = staminaRecovery(playerBand);
  const playerStamina = Math.min(
    MAX_STAMINA,
    state.playerStamina + playerRecovery,
  );
  const { hand, drawPile, discardPile } = drawToHandSize(
    state.drawPile,
    discardAfterHand,
    [],
    HAND_SIZE,
  );
  logs.push(
    `ターン${turn}開始。間合い「${RANGE_LABEL[playerBand]}」でスタミナ回復（+${playerRecovery} → ${playerStamina}）。`,
  );

  const { log, logSeq } = appendLogs(state, logs);
  return {
    ...state,
    turn,
    distanceIndex,
    playerHp,
    playerStamina,
    playerGuard: 0,
    enemyStamina,
    hand,
    drawPile,
    discardPile,
    log,
    logSeq,
    result: "ongoing",
  };
}

export function battleReducer(
  state: BattleState,
  action: BattleAction,
): BattleState {
  switch (action.type) {
    case "PLAY_CARD":
      return playCard(state, action.instanceId);
    case "END_TURN":
      return endTurn(state);
    case "RESTART":
      return initState();
    default:
      return state;
  }
}
