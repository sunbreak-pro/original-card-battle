// Shared battle-lab core — type definitions.
// Range x Stamina combat model, engine-agnostic (no React, no DOM).
// Promoted verbatim from the validated prototype engine; this is now the
// source of truth that every render adapter (Pixi / Phaser) builds on.

export type RangeBand = "close" | "mid" | "far";
export type CardType = "attack" | "move" | "guard";
export type CardDefId =
  "thrust" | "lunge" | "feint" | "step_in" | "step_out" | "brace";
export type EnemyActionId = "sweep" | "reach_thrust" | "shove" | "reposition";
export type GameResult = "ongoing" | "won" | "lost";

/** A single physical card copy held in a pile. */
export interface PrototypeCard {
  readonly instanceId: string;
  readonly defId: CardDefId;
  readonly name: string;
  readonly type: CardType;
  readonly cost: number;
  /** Optimal range for attacks; null for move/guard cards. */
  readonly effectiveRange: RangeBand | null;
  readonly basePower: number;
  /** Distance index delta applied after the card resolves (- closes, + opens). */
  readonly shift: number;
  readonly guard: number;
  readonly description: string;
}

/** Enemy action definition (deterministic AI picks one per turn). */
export interface EnemyAction {
  readonly id: EnemyActionId;
  readonly name: string;
  readonly type: CardType;
  readonly cost: number;
  readonly effectiveRange: RangeBand | null;
  readonly basePower: number;
  readonly shift: number;
  /** When true, shift is resolved toward the mid band instead of a fixed value. */
  readonly towardMid: boolean;
  readonly description: string;
}

export interface EnemyDef {
  readonly name: string;
  readonly maxHp: number;
}

/** Result of resolving a single enemy turn against the player. */
export interface EnemyOutcome {
  readonly action: EnemyAction | null;
  readonly rawDamage: number;
  readonly damage: number;
  readonly newGuard: number;
  readonly newDistanceIndex: number;
  readonly staminaSpent: number;
  readonly logText: string;
}

export interface LogEntry {
  readonly id: number;
  readonly text: string;
}

export interface BattleState {
  readonly turn: number;
  /** Shared player/enemy distance as an index into RANGE_ORDER (0..2). */
  readonly distanceIndex: number;
  readonly playerHp: number;
  readonly playerStamina: number;
  readonly playerGuard: number;
  readonly enemyHp: number;
  readonly enemyStamina: number;
  readonly hand: readonly PrototypeCard[];
  readonly drawPile: readonly PrototypeCard[];
  readonly discardPile: readonly PrototypeCard[];
  readonly log: readonly LogEntry[];
  readonly logSeq: number;
  readonly result: GameResult;
}

export type BattleAction =
  | { readonly type: "PLAY_CARD"; readonly instanceId: string }
  | { readonly type: "END_TURN" }
  | { readonly type: "RESTART" };
