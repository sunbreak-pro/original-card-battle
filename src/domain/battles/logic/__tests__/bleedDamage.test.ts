import { describe, it, expect } from "vitest";
import { calculateBleedDamage, hasBleed } from "../bleedDamage";
import { calculateEndPhaseDamage } from "../../calculators/buffCalculation";
import type {
  BuffDebuffMap,
  BuffDebuffState,
  BuffDebuffType,
} from "@/types/battleTypes";
import {
  BLEED_DAMAGE_RATE,
  FIRE_FIELD_BONUS_MULTIPLIER,
} from "../../../../constants";

function buff(
  name: BuffDebuffType,
  stacks: number,
  value: number,
): BuffDebuffState {
  return { name, stacks, duration: 3, value, isPermanent: false };
}

function makeMap(
  entries: Array<[BuffDebuffType, BuffDebuffState]>,
): BuffDebuffMap {
  return new Map(entries);
}

describe("calculateBleedDamage", () => {
  it("returns 0 when there is no bleed", () => {
    expect(calculateBleedDamage(100, makeMap([]))).toBe(0);
  });

  // V-DMG-06 fix固定化: bleed damage must scale by stacks.
  it("scales bleed damage proportionally with stacks (V-DMG-06)", () => {
    const perStack = Math.floor(100 * BLEED_DAMAGE_RATE); // floor(5) = 5
    const oneStack = makeMap([["bleed", buff("bleed", 1, 0)]]);
    const threeStacks = makeMap([["bleed", buff("bleed", 3, 0)]]);
    expect(calculateBleedDamage(100, oneStack)).toBe(perStack);
    expect(calculateBleedDamage(100, threeStacks)).toBe(perStack * 3);
  });

  it("floors per-tick damage before multiplying by stacks", () => {
    // maxHp 30 → floor(30 * 0.05) = floor(1.5) = 1 per stack.
    const map = makeMap([["bleed", buff("bleed", 4, 0)]]);
    expect(calculateBleedDamage(30, map)).toBe(1 * 4);
  });
});

describe("hasBleed", () => {
  it("detects presence/absence of bleed", () => {
    expect(hasBleed(makeMap([["bleed", buff("bleed", 1, 0)]]))).toBe(true);
    expect(hasBleed(makeMap([]))).toBe(false);
  });
});

describe("calculateEndPhaseDamage (DoT)", () => {
  it("returns 0 with no DoT buffs", () => {
    expect(calculateEndPhaseDamage(makeMap([]))).toBe(0);
  });

  // V-DMG-10 fix固定化: burn/poison DoT scales by value * stacks.
  it("scales burn damage by value * stacks (V-DMG-10)", () => {
    const map = makeMap([["burn", buff("burn", 3, 4)]]); // 4 * 3 = 12
    expect(calculateEndPhaseDamage(map)).toBe(12);
  });

  it("sums burn and poison independently", () => {
    const map = makeMap([
      ["burn", buff("burn", 2, 5)], // 10
      ["poison", buff("poison", 3, 2)], // 6
    ]);
    expect(calculateEndPhaseDamage(map)).toBe(16);
  });

  it("applies fire field bonus only to burn damage", () => {
    const map = makeMap([
      ["burn", buff("burn", 2, 5)], // base 10
      ["poison", buff("poison", 1, 4)], // base 4, no bonus
      ["fireField", buff("fireField", 1, 0)],
    ]);
    // burn: 10 + 10 * FIRE_FIELD_BONUS_MULTIPLIER, poison: 4
    const expected = 10 + 10 * FIRE_FIELD_BONUS_MULTIPLIER + 4;
    expect(calculateEndPhaseDamage(map)).toBe(expected);
  });
});
