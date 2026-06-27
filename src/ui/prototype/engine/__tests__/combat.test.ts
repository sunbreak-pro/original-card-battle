import { describe, it, expect } from "vitest";
import {
  clampDistance,
  computeAttackDamage,
  indexToRange,
  rangeMultiplier,
  rangeToIndex,
  shiftDistance,
  staminaDamageMultiplier,
  staminaRecovery,
} from "../combat";

describe("rangeToIndex / indexToRange", () => {
  it("maps bands to ordered indices", () => {
    expect(rangeToIndex("close")).toBe(0);
    expect(rangeToIndex("mid")).toBe(1);
    expect(rangeToIndex("far")).toBe(2);
  });

  it("maps indices back to bands and clamps out-of-range", () => {
    expect(indexToRange(0)).toBe("close");
    expect(indexToRange(2)).toBe("far");
    expect(indexToRange(-5)).toBe("close");
    expect(indexToRange(99)).toBe("far");
  });
});

describe("clampDistance / shiftDistance", () => {
  it("clamps to [0, 2]", () => {
    expect(clampDistance(-1)).toBe(0);
    expect(clampDistance(0)).toBe(0);
    expect(clampDistance(2)).toBe(2);
    expect(clampDistance(3)).toBe(2);
  });

  it("shifts then clamps", () => {
    expect(shiftDistance(1, -1)).toBe(0);
    expect(shiftDistance(1, 1)).toBe(2);
    expect(shiftDistance(0, -1)).toBe(0); // already closest
    expect(shiftDistance(2, 1)).toBe(2); // already farthest
  });
});

describe("staminaRecovery", () => {
  it("recovers by band: close +1, mid +2, far +3", () => {
    expect(staminaRecovery("close")).toBe(1);
    expect(staminaRecovery("mid")).toBe(2);
    expect(staminaRecovery("far")).toBe(3);
  });
});

describe("staminaDamageMultiplier", () => {
  it("is full at or above the fatigue threshold", () => {
    expect(staminaDamageMultiplier(20)).toBe(1);
    expect(staminaDamageMultiplier(8)).toBe(1);
  });

  it("decays linearly below the threshold down to the floor", () => {
    expect(staminaDamageMultiplier(4)).toBeCloseTo(0.5);
    expect(staminaDamageMultiplier(1)).toBe(0.4); // floor wins over 1/8
    expect(staminaDamageMultiplier(0)).toBe(0.4);
  });
});

describe("rangeMultiplier", () => {
  it("peaks at the effective range and falls off by distance diff", () => {
    expect(rangeMultiplier(0, "close")).toBe(1); // diff 0
    expect(rangeMultiplier(1, "close")).toBe(0.5); // diff 1
    expect(rangeMultiplier(2, "close")).toBe(0.15); // diff 2
    expect(rangeMultiplier(1, "mid")).toBe(1);
    expect(rangeMultiplier(2, "far")).toBe(1);
  });
});

describe("computeAttackDamage", () => {
  it("deals full base power at optimal range and full stamina", () => {
    // thrust: power 9, effRange close, distance close, stamina 20
    expect(computeAttackDamage(9, "close", 20, 0)).toBe(9);
  });

  it("halves (rounded) at one band off", () => {
    // thrust at mid: 9 * 0.5 * 1 = 4.5 -> 5
    expect(computeAttackDamage(9, "close", 20, 1)).toBe(5);
  });

  it("nearly whiffs at two bands off", () => {
    // thrust at far: 9 * 0.15 * 1 = 1.35 -> 1
    expect(computeAttackDamage(9, "close", 20, 2)).toBe(1);
  });

  it("applies fatigue decay on top of range", () => {
    // power 8 at optimal range, stamina 4 -> 8 * 1 * 0.5 = 4
    expect(computeAttackDamage(8, "mid", 4, 1)).toBe(4);
  });

  it("never returns negative damage", () => {
    expect(computeAttackDamage(0, "mid", 4, 1)).toBe(0);
  });
});
