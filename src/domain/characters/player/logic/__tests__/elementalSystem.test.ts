import { describe, it, expect } from "vitest";
import {
  ElementalSystem,
  getDamageModifierIncludingCard,
} from "../elementalSystem";
import type { Card } from "@/types/cardTypes";
import type { ElementType } from "@/types/characterTypes";

// Minimal Card fixture - only fields read by ElementalSystem matter (element).
function createTestCard(elements: ElementType[]): Card {
  return {
    id: "test-card",
    cardTypeId: "test-type",
    name: "Test Card",
    description: "",
    characterClass: "mage",
    baseDamage: 10,
    cost: 1,
    element: elements,
    tags: ["attack"],
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
  };
}

describe("ElementalSystem.onCardPlay", () => {
  it("starts a new chain on first magic card (level 0, element set)", () => {
    const state = ElementalSystem.initialize();
    const next = ElementalSystem.onCardPlay(state, createTestCard(["fire"]));
    expect(next.lastElement).toBe("fire");
    expect(next.resonanceLevel).toBe(0);
  });

  it("increases resonance level when same magic element repeats", () => {
    let state = ElementalSystem.initialize();
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 0
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 1
    expect(state.lastElement).toBe("fire");
    expect(state.resonanceLevel).toBe(1);
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 2
    expect(state.resonanceLevel).toBe(2);
  });

  it("caps resonance level at MAX (2)", () => {
    let state = ElementalSystem.initialize();
    for (let i = 0; i < 6; i++) {
      state = ElementalSystem.onCardPlay(state, createTestCard(["ice"]));
    }
    expect(state.resonanceLevel).toBe(2);
  });

  it("resets chain to level 0 with new element when a different magic element is played", () => {
    let state = ElementalSystem.initialize();
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"]));
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 1
    state = ElementalSystem.onCardPlay(state, createTestCard(["ice"]));
    expect(state.lastElement).toBe("ice");
    expect(state.resonanceLevel).toBe(0);
  });

  // V-CLASS-13 fix固定化: non-magic card fully resets the chain (per design spec).
  it("completely resets resonance on a non-magic card (V-CLASS-13)", () => {
    let state = ElementalSystem.initialize();
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"]));
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 1
    state = ElementalSystem.onCardPlay(state, createTestCard(["physics"]));
    expect(state.lastElement).toBeNull();
    expect(state.resonanceLevel).toBe(0);
  });

  // V-CLASS-04 fix固定化: dual-element card uses the magic element for chaining.
  it("uses the magic element from a dual-element card for chain continuation (V-CLASS-04)", () => {
    let state = ElementalSystem.initialize();
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"]));
    // physics+fire card: should continue the fire chain, not reset on physics.
    state = ElementalSystem.onCardPlay(
      state,
      createTestCard(["physics", "fire"]),
    );
    expect(state.lastElement).toBe("fire");
    expect(state.resonanceLevel).toBe(1);
  });
});

describe("ElementalSystem.onTurnEnd", () => {
  it("resets resonance level to 0 but keeps lastElement", () => {
    let state = ElementalSystem.initialize();
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"]));
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 1
    const ended = ElementalSystem.onTurnEnd(state);
    expect(ended.resonanceLevel).toBe(0);
    expect(ended.lastElement).toBe("fire");
  });
});

describe("ElementalSystem.getDamageModifier (stale-state path)", () => {
  it("returns default modifier when card element does not match chain", () => {
    const state = ElementalSystem.onCardPlay(
      ElementalSystem.initialize(),
      createTestCard(["fire"]),
    );
    const mod = ElementalSystem.getDamageModifier(
      state,
      createTestCard(["ice"]),
    );
    expect(mod.percentMultiplier).toBe(1.0);
  });

  it("applies resonance multiplier when card element matches chain at level 1", () => {
    let state = ElementalSystem.initialize();
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"]));
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 1
    const mod = ElementalSystem.getDamageModifier(
      state,
      createTestCard(["fire"]),
    );
    expect(mod.percentMultiplier).toBeCloseTo(1.15);
  });
});

// V-CHAIN-01 fix検証: the card being played must count toward its own resonance.
describe("getDamageModifierIncludingCard (V-CHAIN-01)", () => {
  it("the FIRST fire card contributes to its own chain (no 1-card lag)", () => {
    // Stale path: empty state + fire card → no match yet → 1.0x.
    const initial = ElementalSystem.initialize();
    expect(
      ElementalSystem.getDamageModifier(initial, createTestCard(["fire"]))
        .percentMultiplier,
    ).toBe(1.0);

    // Play-aware path: the fire card starts the chain (level 0) and now
    // matches its own element → still 1.0x at level 0, but element matches.
    const mod0 = getDamageModifierIncludingCard(
      initial,
      createTestCard(["fire"]),
    );
    expect(mod0.percentMultiplier).toBe(1.0); // level 0 = 1.0x (correct)
  });

  it("SECOND consecutive fire card gets level-1 bonus immediately (was lagged before fix)", () => {
    // After one fire card, lastElement=fire, level=0.
    const afterFirst = ElementalSystem.onCardPlay(
      ElementalSystem.initialize(),
      createTestCard(["fire"]),
    );

    // Stale path would read level 0 → 1.0x (the bug: bonus lags one card).
    expect(
      ElementalSystem.getDamageModifier(afterFirst, createTestCard(["fire"]))
        .percentMultiplier,
    ).toBe(1.0);

    // Play-aware path: playing the second fire card raises level to 1 and
    // applies the +15% to THIS card.
    const mod = getDamageModifierIncludingCard(
      afterFirst,
      createTestCard(["fire"]),
    );
    expect(mod.percentMultiplier).toBeCloseTo(1.15);
  });

  it("THIRD consecutive fire card gets level-2 bonus + crit immediately", () => {
    let state = ElementalSystem.initialize();
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 0
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 1
    const mod = getDamageModifierIncludingCard(state, createTestCard(["fire"]));
    expect(mod.percentMultiplier).toBeCloseTo(1.3);
    expect(mod.critBonus).toBeCloseTo(0.1);
  });

  it("a non-matching card after a chain gets no bonus even on the play-aware path", () => {
    let state = ElementalSystem.initialize();
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"]));
    state = ElementalSystem.onCardPlay(state, createTestCard(["fire"])); // lvl 1
    // Playing ice breaks the fire chain → ice level 0 → 1.0x.
    const mod = getDamageModifierIncludingCard(state, createTestCard(["ice"]));
    expect(mod.percentMultiplier).toBe(1.0);
  });
});
