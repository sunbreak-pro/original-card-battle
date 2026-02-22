import type { BuffDebuffMap } from "@/types/battleTypes";
import type { CharacterClass } from "@/types/characterTypes";

/**
 * Battle state subset for PixiJS rendering.
 * Passed as props from BattleScreen (not via Context).
 */
export interface BattlePixiState {
  readonly playerHp: number;
  readonly playerMaxHp: number;
  readonly playerGuard: number;
  readonly enemies: ReadonlyArray<EnemyPixiState>;
  readonly currentPhaseIndex: number;
  readonly isPlayerPhase: boolean;
  readonly playerClass: CharacterClass;
}

export interface EnemyPixiState {
  readonly hp: number;
  readonly maxHp: number;
  readonly guard: number;
  readonly buffDebuffs: BuffDebuffMap;
}

/**
 * Layer z-ordering within PixiJS canvas.
 * sortableChildren on the root container uses these values.
 */
export const PIXI_LAYER_ORDER = {
  BACKGROUND: 0,
  CHARACTER: 1,
  EFFECT: 2,
} as const;

/**
 * Effect trigger types that PixiJS will render.
 * Phase 1: only TEST_PARTICLE is implemented.
 */
export const PIXI_EFFECT_TYPE = {
  TEST_PARTICLE: "test_particle",
  DAMAGE: "damage",
  HEAL: "heal",
  SHIELD: "shield",
  CARD_PLAY: "card_play",
  BUFF: "buff",
} as const;

export type PixiEffectType =
  (typeof PIXI_EFFECT_TYPE)[keyof typeof PIXI_EFFECT_TYPE];

/**
 * Command to trigger an effect on the PixiJS canvas.
 */
export interface PixiEffectCommand {
  readonly type: PixiEffectType;
  readonly x: number;
  readonly y: number;
  readonly color?: string;
  readonly value?: number;
  readonly isCritical?: boolean;
  readonly target?: "player" | "enemy";
}
