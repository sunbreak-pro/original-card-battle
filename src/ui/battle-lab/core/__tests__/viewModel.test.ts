import { describe, it, expect, beforeEach, vi } from "vitest";
import {
  describeCard,
  describeHand,
  distanceLabel,
  enemyRangeHint,
  isBattleOver,
} from "../viewModel";
import { initState } from "../battleReducer";
import type { BattleState, PrototypeCard } from "../types";

// Deterministic shuffles for any draw triggered while building a state.
beforeEach(() => {
  vi.spyOn(Math, "random").mockReturnValue(0);
});

function makeCard(
  overrides: Partial<PrototypeCard> & Pick<PrototypeCard, "instanceId">,
): PrototypeCard {
  return {
    defId: "thrust",
    name: "突き",
    type: "attack",
    cost: 4,
    effectiveRange: "close",
    basePower: 9,
    shift: 0,
    guard: 0,
    description: "近接最大火力。",
    ...overrides,
  };
}

function makeState(overrides: Partial<BattleState>): BattleState {
  return { ...initState(), ...overrides };
}

describe("describeCard — attack damage derivation", () => {
  it("is optimal at the effective range with full stamina", () => {
    const card = makeCard({ instanceId: "t" });
    const view = describeCard(card, 20, 0, false); // close range, full stamina
    expect(view.damage).not.toBeNull();
    expect(view.damage?.predicted).toBe(9);
    expect(view.damage?.offRange).toBe(false);
    expect(view.damage?.fatigued).toBe(false);
    expect(view.damage?.note).toBe("最適");
    expect(view.damage?.text).toBe("威力 9 → 9（最適）");
    expect(view.effectiveRangeLabel).toBe("近");
  });

  it("flags off-range when distance is wrong", () => {
    const card = makeCard({ instanceId: "t" });
    const view = describeCard(card, 20, 1, false); // mid vs close effRange
    expect(view.damage?.predicted).toBe(5); // 9 * 0.5 -> 4.5 -> 5
    expect(view.damage?.offRange).toBe(true);
    expect(view.damage?.fatigued).toBe(false);
    expect(view.damage?.note).toBe("間合い不適");
  });

  it("flags fatigue when stamina is below the threshold", () => {
    const card = makeCard({
      instanceId: "f",
      effectiveRange: "mid",
      basePower: 8,
    });
    const view = describeCard(card, 4, 1, false); // optimal range, low stamina
    expect(view.damage?.predicted).toBe(4); // 8 * 0.5
    expect(view.damage?.offRange).toBe(false);
    expect(view.damage?.fatigued).toBe(true);
    expect(view.damage?.note).toBe("疲労");
  });

  it("joins both penalties when off-range and fatigued", () => {
    const card = makeCard({ instanceId: "t" });
    const view = describeCard(card, 4, 1, false); // mid distance, low stamina
    expect(view.damage?.predicted).toBe(2); // 9 * 0.5 * 0.5 -> 2.25 -> 2
    expect(view.damage?.note).toBe("間合い不適・疲労");
  });
});

describe("describeCard — playability", () => {
  it("is playable when affordable and the battle is ongoing", () => {
    const card = makeCard({ instanceId: "t", cost: 4 });
    const view = describeCard(card, 4, 0, false);
    expect(view.playable).toBe(true);
    expect(view.disabledReason).toBe("");
  });

  it("is unplayable with a reason when stamina is short", () => {
    const card = makeCard({ instanceId: "t", cost: 5 });
    const view = describeCard(card, 4, 0, false);
    expect(view.playable).toBe(false);
    expect(view.disabledReason).toBe("気力不足（必要 5）");
  });

  it("is unplayable once the battle is over even if affordable", () => {
    const card = makeCard({ instanceId: "t", cost: 4 });
    const view = describeCard(card, 20, 0, true);
    expect(view.playable).toBe(false);
    expect(view.disabledReason).toBe(""); // affordable, just battle over
  });
});

describe("describeCard — move and guard cards", () => {
  it("has no damage and a closing shift label for a move card", () => {
    const card = makeCard({
      instanceId: "in",
      type: "move",
      cost: 2,
      effectiveRange: null,
      basePower: 0,
      shift: -1,
    });
    const view = describeCard(card, 20, 1, false);
    expect(view.damage).toBeNull();
    expect(view.effectiveRangeLabel).toBeNull();
    expect(view.shiftLabel).toBe("間合い −1（詰める）");
  });

  it("reports an opening shift label for a retreat card", () => {
    const card = makeCard({
      instanceId: "out",
      type: "move",
      cost: 1,
      effectiveRange: null,
      basePower: 0,
      shift: 1,
    });
    expect(describeCard(card, 20, 1, false).shiftLabel).toBe(
      "間合い +1（退く）",
    );
  });

  it("exposes guard amount and no shift for a guard card", () => {
    const card = makeCard({
      instanceId: "g",
      type: "guard",
      cost: 2,
      effectiveRange: null,
      basePower: 0,
      guard: 6,
      shift: 0,
    });
    const view = describeCard(card, 20, 1, false);
    expect(view.damage).toBeNull();
    expect(view.guard).toBe(6);
    expect(view.shiftLabel).toBeNull();
  });
});

describe("describeHand", () => {
  it("derives a view per hand card and disables all when the battle is over", () => {
    const hand = [
      makeCard({ instanceId: "a" }),
      makeCard({ instanceId: "b", cost: 1 }),
    ];
    const ongoing = makeState({ hand, playerStamina: 20, distanceIndex: 0 });
    expect(describeHand(ongoing)).toHaveLength(2);
    expect(describeHand(ongoing).every((c) => c.playable)).toBe(true);

    const won = makeState({
      hand,
      playerStamina: 20,
      distanceIndex: 0,
      result: "won",
    });
    expect(describeHand(won).every((c) => !c.playable)).toBe(true);
  });
});

describe("distanceLabel / isBattleOver / enemyRangeHint", () => {
  it("labels distance indices", () => {
    expect(distanceLabel(0)).toBe("近");
    expect(distanceLabel(1)).toBe("中");
    expect(distanceLabel(2)).toBe("遠");
  });

  it("treats only 'ongoing' as not over", () => {
    expect(isBattleOver("ongoing")).toBe(false);
    expect(isBattleOver("won")).toBe(true);
    expect(isBattleOver("lost")).toBe(true);
  });

  it("lists the mid/far threats and excludes the close shove", () => {
    const hint = enemyRangeHint();
    expect(hint).toContain("中（薙ぎ払い）");
    expect(hint).toContain("遠（穂先の突き）");
    expect(hint).not.toContain("石突き"); // close-range shove omitted
    expect(hint).toContain("近に詰めると弱い押し戻ししかできない");
  });
});
