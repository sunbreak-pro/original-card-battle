import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  determineEnemyAction,
  resolveEnemyAction,
  clearResolvedActionCache,
} from "../enemyAI";
import { previewEnemyActions } from "../enemyActionExecution";
import type {
  EnemyAIPattern,
  EnemyAction,
  EnemyDefinition,
} from "@/types/characterTypes";

function attack(name: string, baseDamage: number, energyCost = 1): EnemyAction {
  return { name, type: "attack", baseDamage, energyCost };
}

// Two equally-probable patterns on the same phase → outcome depends on RNG.
const TWO_PATTERN_AI: EnemyAIPattern[] = [
  { phaseNumber: 0, action: attack("Strike A", 10), probability: 0.5 },
  { phaseNumber: 0, action: attack("Strike B", 20), probability: 0.5 },
];

function makeEnemy(id: string, actEnergy = 3): EnemyDefinition {
  return {
    id,
    name: id,
    nameJa: id,
    description: "",
    baseMaxHp: 50,
    baseMaxAp: 20,
    baseSpeed: 5,
    startingGuard: false,
    actEnergy,
    aiPatterns: TWO_PATTERN_AI,
  };
}

describe("determineEnemyAction", () => {
  beforeEach(() => clearResolvedActionCache());
  afterEach(() => vi.restoreAllMocks());

  it("falls back to a basic attack when no pattern matches the phase", () => {
    const enemy = {
      aiPatterns: [{ phaseNumber: 5, action: attack("Late", 9) }],
    };
    const action = determineEnemyAction(enemy, 50, 50, 1);
    expect(action.name).toBe("基本攻撃");
  });

  it("selects the lower-cumulative pattern when RNG is low", () => {
    vi.spyOn(Math, "random").mockReturnValue(0); // random*total = 0 → first
    const action = determineEnemyAction(makeEnemy("e1"), 50, 50, 1);
    expect(action.name).toBe("Strike A");
  });

  it("selects the second pattern when RNG is high", () => {
    vi.spyOn(Math, "random").mockReturnValue(0.99); // → second
    const action = determineEnemyAction(makeEnemy("e1"), 50, 50, 1);
    expect(action.name).toBe("Strike B");
  });
});

// V-ENM-02 fix: preview and execute must resolve the SAME action even when
// patterns are probabilistic. resolveEnemyAction memoizes per
// (id, hp, maxHp, turn, callIndex) so both paths read the same slot.
describe("resolveEnemyAction (V-ENM-02)", () => {
  beforeEach(() => clearResolvedActionCache());
  afterEach(() => vi.restoreAllMocks());

  it("returns the cached action on subsequent calls (RNG only runs once)", () => {
    const rng = vi.spyOn(Math, "random").mockReturnValue(0.99); // → Strike B
    const enemy = makeEnemy("e1");

    const first = resolveEnemyAction(enemy, 50, 50, 1, 0);
    // Even if RNG would now return something else, the slot is cached.
    rng.mockReturnValue(0);
    const second = resolveEnemyAction(enemy, 50, 50, 1, 0);

    expect(first.name).toBe("Strike B");
    expect(second.name).toBe("Strike B");
    expect(rng).toHaveBeenCalledTimes(1);
  });

  it("different callIndex resolves independently (varied multi-action sequence)", () => {
    const rng = vi.spyOn(Math, "random");
    rng.mockReturnValueOnce(0); // callIndex 0 → Strike A
    rng.mockReturnValueOnce(0.99); // callIndex 1 → Strike B
    const enemy = makeEnemy("e1");

    expect(resolveEnemyAction(enemy, 50, 50, 1, 0).name).toBe("Strike A");
    expect(resolveEnemyAction(enemy, 50, 50, 1, 1).name).toBe("Strike B");
  });

  it("different enemy id does not collide in the cache", () => {
    const rng = vi.spyOn(Math, "random");
    rng.mockReturnValueOnce(0); // enemy A → Strike A
    rng.mockReturnValueOnce(0.99); // enemy B → Strike B

    expect(resolveEnemyAction(makeEnemy("A"), 50, 50, 1, 0).name).toBe(
      "Strike A",
    );
    expect(resolveEnemyAction(makeEnemy("B"), 50, 50, 1, 0).name).toBe(
      "Strike B",
    );
  });

  it("clearResolvedActionCache drops cached slots", () => {
    const rng = vi.spyOn(Math, "random").mockReturnValue(0.99);
    const enemy = makeEnemy("e1");
    resolveEnemyAction(enemy, 50, 50, 1, 0);
    clearResolvedActionCache();
    rng.mockReturnValue(0); // after clear, RNG runs again → Strike A
    expect(resolveEnemyAction(enemy, 50, 50, 1, 0).name).toBe("Strike A");
  });
});

describe("preview matches execute resolution (V-ENM-02)", () => {
  beforeEach(() => clearResolvedActionCache());
  afterEach(() => vi.restoreAllMocks());

  it("previewEnemyActions resolves the same actions a re-preview would", () => {
    // actEnergy 3, each action cost 1 → 3 actions. Alternate RNG so we get a
    // mixed A/B/A sequence; the cache must make it stable across calls.
    const rng = vi.spyOn(Math, "random");
    rng.mockReturnValueOnce(0); // idx 0 → A
    rng.mockReturnValueOnce(0.99); // idx 1 → B
    rng.mockReturnValueOnce(0); // idx 2 → A
    const enemy = makeEnemy("seq", 3);

    const firstPreview = previewEnemyActions(enemy, 50, 50, 1).map(
      (a) => a.name,
    );
    // A second preview for the SAME slot must not re-roll: cached.
    const secondPreview = previewEnemyActions(enemy, 50, 50, 1).map(
      (a) => a.name,
    );

    expect(firstPreview).toEqual(["Strike A", "Strike B", "Strike A"]);
    expect(secondPreview).toEqual(firstPreview);
    // RNG ran exactly 3 times (once per distinct callIndex slot).
    expect(rng).toHaveBeenCalledTimes(3);
  });
});
