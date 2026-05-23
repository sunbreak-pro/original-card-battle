import { describe, it, expect } from "vitest";
import type { BattlePixiProps } from "../types/pixiTypes";
import type { PixiEffectBridge } from "../battle/PixiEffectBridge";

/**
 * Phase 1 foundation tests.
 *
 * jsdom has no WebGL context, so we deliberately do NOT mount <Application>
 * here (it would crash regardless of correctness). Additionally, modules that
 * transitively import `@pixi/react`'s reconciler store (`PixiStage`,
 * `usePixiApp`, `BattleCanvas`) are intentionally NOT eagerly imported under
 * vitest: `@pixi/react` uses an extensionless `react-reconciler/constants`
 * specifier that the production Vite build resolves but vitest's jsdom
 * resolver does not. Their correct loading is covered by `npm run build`
 * (which succeeds) and by manual browser verification (plan §4).
 *
 * What this suite asserts:
 *  - the layer components are valid React component functions (no @pixi/react
 *    reconciler import at module load — only JSX intrinsics),
 *  - the typed contracts (BattlePixiProps / PixiEffectBridge) are stable.
 */
describe("PixiJS Phase 1 foundation", () => {
  it("exposes layer components as functions", async () => {
    const [bg, ch, fx] = await Promise.all([
      import("../battle/layers/BackgroundLayer"),
      import("../battle/layers/CharacterLayer"),
      import("../battle/layers/EffectLayer"),
    ]);
    expect(typeof bg.BackgroundLayer).toBe("function");
    expect(typeof ch.CharacterLayer).toBe("function");
    expect(typeof fx.EffectLayer).toBe("function");
  });

  it("imports BattleCanvas without throwing", async () => {
    const mod = await import("../battle/BattleCanvas");
    expect(typeof mod.BattleCanvas).toBe("function");
  });

  it("BattlePixiProps accepts the orchestrator-derived shape", () => {
    const props: BattlePixiProps = {
      playerHp: 30,
      playerMaxHp: 40,
      enemyHp: 12,
      enemyMaxHp: 20,
      isPlayerPhase: true,
      phaseCount: 3,
    };
    expect(props.playerHp).toBe(30);
    expect(props.isPlayerPhase).toBe(true);
  });

  it("PixiEffectBridge contract is structurally satisfiable", () => {
    const calls: string[] = [];
    const bridge: PixiEffectBridge = {
      playDamageEffect: () => calls.push("damage"),
      playHealEffect: () => calls.push("heal"),
      playShieldEffect: () => calls.push("shield"),
      playTestParticle: () => calls.push("test"),
    };
    bridge.playTestParticle(10, 20, 0x66ccff);
    expect(calls).toEqual(["test"]);
  });
});
