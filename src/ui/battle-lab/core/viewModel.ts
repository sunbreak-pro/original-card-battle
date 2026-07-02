// Engine-agnostic presentation derivation for the battle-lab core.
//
// The validated prototype computed "predicted damage / playable / reason /
// labels" inline inside its React components (HandView.tsx, PrototypeBattle.tsx).
// Both bake-off adapters (Pixi, Phaser) need the exact same derivation, so it is
// hoisted here as pure functions that return plain view data — no React, no JSX,
// no DOM. Adapters render this data however their engine prefers.

import type {
  BattleState,
  CardType,
  GameResult,
  PrototypeCard,
  RangeBand,
} from "./types";
import {
  computeAttackDamage,
  indexToRange,
  rangeMultiplier,
  staminaDamageMultiplier,
} from "./combat";
import { RANGE_LABEL } from "./constants";
import { ENEMY_ACTIONS } from "./enemy";

/** Predicted-damage breakdown for an attack card at the current state. */
export interface CardDamageView {
  readonly basePower: number;
  /** Damage after range + fatigue multipliers, rounded and clamped at 0. */
  readonly predicted: number;
  /** True when the current distance is off the card's effective range. */
  readonly offRange: boolean;
  /** True when low stamina is decaying the damage. */
  readonly fatigued: boolean;
  /** Localized qualifier: "最適" / "間合い不適" / "疲労" / "間合い不適・疲労". */
  readonly note: string;
  /** Pre-formatted line, e.g. "威力 9 → 5（間合い不適）". */
  readonly text: string;
}

/** Everything an adapter needs to render one hand card, fully derived. */
export interface CardView {
  readonly instanceId: string;
  readonly name: string;
  readonly type: CardType;
  readonly cost: number;
  readonly description: string;
  /** True when the card is affordable and the battle is still ongoing. */
  readonly playable: boolean;
  /** Localized reason the card is unplayable, or "" when playable. */
  readonly disabledReason: string;
  /** Effective-range label ("近"/"中"/"遠") for attacks, null otherwise. */
  readonly effectiveRangeLabel: string | null;
  /** Predicted-damage breakdown for attacks, null for move/guard cards. */
  readonly damage: CardDamageView | null;
  /** Guard granted by guard cards, 0 otherwise. */
  readonly guard: number;
  /** Distance shift: < 0 closes, > 0 opens, 0 = none. */
  readonly shift: number;
  /** Localized shift hint, or null when the card does not move. */
  readonly shiftLabel: string | null;
}

const OPTIMAL_NOTE = "最適";
const OFF_RANGE_NOTE = "間合い不適";
const FATIGUE_NOTE = "疲労";

/** Derive the predicted-damage breakdown for an attack card. */
function describeDamage(
  basePower: number,
  effectiveRange: RangeBand,
  playerStamina: number,
  distanceIndex: number,
): CardDamageView {
  const predicted = computeAttackDamage(
    basePower,
    effectiveRange,
    playerStamina,
    distanceIndex,
  );
  const offRange = rangeMultiplier(distanceIndex, effectiveRange) < 1;
  const fatigued = staminaDamageMultiplier(playerStamina) < 1;

  const tags: string[] = [];
  if (offRange) tags.push(OFF_RANGE_NOTE);
  if (fatigued) tags.push(FATIGUE_NOTE);
  const note = tags.length > 0 ? tags.join("・") : OPTIMAL_NOTE;

  return {
    basePower,
    predicted,
    offRange,
    fatigued,
    note,
    text: `威力 ${basePower} → ${predicted}（${note}）`,
  };
}

/** Localized shift hint matching the prototype's hand rendering. */
function describeShift(shift: number): string | null {
  if (shift === 0) return null;
  return shift < 0 ? "間合い −1（詰める）" : "間合い +1（退く）";
}

/**
 * Derive a single card's view from the current pools. `battleOver` disables
 * every card once the result is decided (mirrors the prototype's `disabled`).
 */
export function describeCard(
  card: PrototypeCard,
  playerStamina: number,
  distanceIndex: number,
  battleOver: boolean,
): CardView {
  const affordable = playerStamina >= card.cost;
  const playable = affordable && !battleOver;
  const disabledReason = !affordable ? `気力不足（必要 ${card.cost}）` : "";

  const damage =
    card.type === "attack" && card.effectiveRange
      ? describeDamage(
          card.basePower,
          card.effectiveRange,
          playerStamina,
          distanceIndex,
        )
      : null;

  return {
    instanceId: card.instanceId,
    name: card.name,
    type: card.type,
    cost: card.cost,
    description: card.description,
    playable,
    disabledReason,
    effectiveRangeLabel: card.effectiveRange
      ? RANGE_LABEL[card.effectiveRange]
      : null,
    damage,
    guard: card.guard,
    shift: card.shift,
    shiftLabel: describeShift(card.shift),
  };
}

/** Derive the full hand view from a battle state. */
export function describeHand(state: BattleState): readonly CardView[] {
  const battleOver = isBattleOver(state.result);
  return state.hand.map((card) =>
    describeCard(card, state.playerStamina, state.distanceIndex, battleOver),
  );
}

/** Localized label for a distance index ("近"/"中"/"遠"). */
export function distanceLabel(distanceIndex: number): string {
  return RANGE_LABEL[indexToRange(distanceIndex)];
}

/** True once the battle has resolved either way. */
export function isBattleOver(result: GameResult): boolean {
  return result !== "ongoing";
}

/**
 * Enemy effective-range hint for the player panel, derived from ENEMY_ACTIONS.
 * Lists the ranged kill-zone threats (mid/far). The close-range shove is
 * deliberately excluded — it is a weak pushback, surfaced as the caveat tail.
 */
export function enemyRangeHint(): string {
  const threats = Object.values(ENEMY_ACTIONS).filter(
    (a) =>
      a.type === "attack" &&
      a.effectiveRange !== null &&
      a.effectiveRange !== "close",
  );
  const parts = threats.map(
    (a) => `${RANGE_LABEL[a.effectiveRange as RangeBand]}（${a.name}）`,
  );
  return `有効間合い: ${parts.join(" / ")}。近に詰めると弱い押し戻ししかできない。`;
}
