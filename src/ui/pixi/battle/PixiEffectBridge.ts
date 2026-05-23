/**
 * Imperative effect API surface for the Pixi battle overlay.
 *
 * Phase 1 only fixes the *signatures* (so callers / the Phase 2 wiring have a
 * stable contract) and implements a single concrete effect: `playTestParticle`
 * (rendered inside `EffectLayer`). The damage/heal/shield methods are declared
 * but intentionally unimplemented until Phase 2, when this bridge is wired to
 * `useCardAnimation`.
 *
 * Keeping this as a plain command-style interface (no React coupling) is the
 * intended escape hatch: if @pixi/react proves unstable we can back this with
 * vanilla pixi.js without changing call sites.
 */
export interface PixiEffectBridge {
  // --- Phase 2 (signatures locked, no implementation yet) ---
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
