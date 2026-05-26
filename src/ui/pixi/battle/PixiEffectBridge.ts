import type { PixiEffectCommand } from "../types/pixiTypes";
import { PIXI_EFFECT_TYPE } from "../types/pixiTypes";

/**
 * Bridge API between DOM-based battle hooks and PixiJS effects.
 * Phase 1: Only playTestParticle is implemented.
 * Phase 2: Will implement damage, heal, shield, card effects.
 */
export interface PixiEffectBridgeAPI {
  playTestParticle(x: number, y: number, color: string): void;
  playDamageEffect(
    target: "player" | "enemy",
    damage: number,
    isCritical: boolean,
  ): void;
  playHealEffect(target: "player" | "enemy", amount: number): void;
  playShieldEffect(target: "player" | "enemy", amount: number): void;

  // --- Phase 1 (only concrete entry point) ---
  playTestParticle(x: number, y: number, colorHex: number): void;
}
