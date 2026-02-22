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
}

/**
 * Create a PixiEffectBridge that pushes commands to an effect queue.
 * The queue is consumed by EffectLayer in the PixiJS canvas.
 */
export function createPixiEffectBridge(
  pushEffect: (command: PixiEffectCommand) => void,
): PixiEffectBridgeAPI {
  return {
    playTestParticle(x: number, y: number, color: string): void {
      pushEffect({
        type: PIXI_EFFECT_TYPE.TEST_PARTICLE,
        x,
        y,
        color,
      });
    },

    // Phase 2 stubs
    playDamageEffect(
      _target: "player" | "enemy",
      _damage: number,
      _isCritical: boolean,
    ): void {
      // Phase 2 implementation
    },

    playHealEffect(_target: "player" | "enemy", _amount: number): void {
      // Phase 2 implementation
    },

    playShieldEffect(_target: "player" | "enemy", _amount: number): void {
      // Phase 2 implementation
    },
  };
}
