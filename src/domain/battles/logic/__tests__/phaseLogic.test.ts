import { describe, it, expect } from "vitest";
import {
  calculatePlayerPhaseStart,
  calculatePlayerPhaseEnd,
  calculateEnemyPhaseStart,
  checkBattleEndCondition,
} from "../phaseLogic";
import { calculateStartPhaseHealing } from "../../calculators/buffCalculation";
import type {
  BuffDebuffMap,
  BuffDebuffState,
  BuffDebuffType,
} from "@/types/battleTypes";
import type { EnemyDefinition } from "@/types/characterTypes";

function buff(
  name: BuffDebuffType,
  opts: Partial<BuffDebuffState> = {},
): BuffDebuffState {
  return {
    name,
    stacks: opts.stacks ?? 1,
    duration: opts.duration ?? 3,
    value: opts.value ?? 0,
    isPermanent: opts.isPermanent ?? false,
    appliedBy: opts.appliedBy ?? "player",
  };
}

function makeMap(
  entries: Array<[BuffDebuffType, BuffDebuffState]>,
): BuffDebuffMap {
  return new Map(entries);
}

describe("calculateStartPhaseHealing", () => {
  it("returns zero healing/shield with no relevant buffs", () => {
    expect(calculateStartPhaseHealing(makeMap([]))).toEqual({
      hp: 0,
      shield: 0,
    });
  });

  it("scales regeneration healing by value * stacks", () => {
    const map = makeMap([
      ["regeneration", buff("regeneration", { value: 5, stacks: 3 })],
    ]);
    expect(calculateStartPhaseHealing(map)).toEqual({ hp: 15, shield: 0 });
  });

  it("scales shieldRegen by value * stacks", () => {
    const map = makeMap([
      ["shieldRegen", buff("shieldRegen", { value: 4, stacks: 2 })],
    ]);
    expect(calculateStartPhaseHealing(map)).toEqual({ hp: 0, shield: 8 });
  });

  it("applies curse healing reduction", () => {
    const map = makeMap([
      ["regeneration", buff("regeneration", { value: 10, stacks: 2 })], // 20
      ["curse", buff("curse")],
    ]);
    // 20 * 0.2 = 4
    expect(calculateStartPhaseHealing(map).hp).toBe(4);
  });
});

// V-PHASE-01 / V-PHASE-02 / V-DMG-01 fix固定化:
// healing must be computed from the buff state BEFORE the per-phase duration
// decrease, so a 1-duration regeneration still heals on its final phase.
describe("calculatePlayerPhaseStart (pre-decrease healing)", () => {
  it("heals from the original buffs even when regeneration has duration 1 (V-PHASE-01)", () => {
    const map = makeMap([
      [
        "regeneration",
        buff("regeneration", { value: 7, stacks: 1, duration: 1 }),
      ],
    ]);
    const result = calculatePlayerPhaseStart(map);
    // Healing applied this phase from the pre-decrease state.
    expect(result.healAmount).toBe(7);
    // Duration-1 player buff expires after the decrease (not carried over).
    expect(result.newBuffs.has("regeneration")).toBe(false);
  });

  it("keeps a multi-duration regeneration but still heals this phase", () => {
    const map = makeMap([
      [
        "regeneration",
        buff("regeneration", { value: 5, stacks: 1, duration: 3 }),
      ],
    ]);
    const result = calculatePlayerPhaseStart(map);
    expect(result.healAmount).toBe(5);
    expect(result.newBuffs.get("regeneration")?.duration).toBe(2);
  });

  it("cleanses debuffs when a cleanse buff is present", () => {
    const map = makeMap([
      ["cleanse", buff("cleanse")],
      ["poison", buff("poison", { value: 3, appliedBy: "enemy" })],
    ]);
    const result = calculatePlayerPhaseStart(map);
    expect(result.shouldCleanse).toBe(true);
    expect(result.newBuffs.has("poison")).toBe(false);
  });

  it("adds drawPower modifier (value * stacks) on top of base draw count", () => {
    const baseline = calculatePlayerPhaseStart(makeMap([])).drawCount;
    const map = makeMap([
      ["drawPower", buff("drawPower", { value: 1, stacks: 2 })],
    ]);
    const withBuff = calculatePlayerPhaseStart(map).drawCount;
    expect(withBuff - baseline).toBe(2);
  });
});

describe("calculatePlayerPhaseEnd", () => {
  it("reports DoT damage from end-phase damage buffs", () => {
    const map = makeMap([["burn", buff("burn", { value: 4, stacks: 2 })]]);
    const result = calculatePlayerPhaseEnd(map);
    expect(result.dotDamage).toBe(8);
  });

  it("increments momentum stacks at phase end", () => {
    const map = makeMap([["momentum", buff("momentum", { stacks: 1 })]]);
    const result = calculatePlayerPhaseEnd(map);
    expect(result.newBuffs.get("momentum")?.stacks).toBe(2);
  });
});

describe("calculateEnemyPhaseStart", () => {
  const enemyDef: EnemyDefinition = {
    id: "test-enemy",
    name: "Test",
    nameJa: "テスト",
    description: "",
    baseMaxHp: 50,
    baseMaxAp: 20,
    baseSpeed: 5,
    startingGuard: true,
    actEnergy: 2,
    aiPatterns: [],
  };

  it("blocks action while stunned", () => {
    const map = makeMap([["stun", buff("stun", { appliedBy: "player" })]]);
    const result = calculateEnemyPhaseStart(enemyDef, map);
    expect(result.canPerformAction).toBe(false);
  });

  it("allows action with no disabling debuffs and sets guard reset for startingGuard", () => {
    const result = calculateEnemyPhaseStart(enemyDef, makeMap([]));
    expect(result.canPerformAction).toBe(true);
    expect(result.guardReset).toBeGreaterThan(0);
  });
});

describe("checkBattleEndCondition", () => {
  it("victory when all enemies are dead", () => {
    expect(checkBattleEndCondition(30, [0, 0])).toEqual({
      shouldEnd: true,
      result: "victory",
    });
  });

  it("defeat when player hp is 0 and an enemy is alive", () => {
    expect(checkBattleEndCondition(0, [10])).toEqual({
      shouldEnd: true,
      result: "defeat",
    });
  });

  it("ongoing otherwise", () => {
    expect(checkBattleEndCondition(20, [10, 0])).toEqual({
      shouldEnd: false,
      result: "ongoing",
    });
  });
});
