import { describe, it, expect, beforeEach, vi } from "vitest";
import { battleReducer, initState } from "../battleReducer";
import { chooseEnemyAction, resolveEnemyTurn } from "../enemy";
import type { BattleState, PrototypeCard } from "../types";

// Deterministic shuffles for any draw triggered during a transition.
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
    description: "",
    ...overrides,
  };
}

function makeState(overrides: Partial<BattleState>): BattleState {
  return { ...initState(), ...overrides };
}

describe("initState", () => {
  it("starts at mid with full pools and a 3-card hand from a 12-card deck", () => {
    const s = initState();
    expect(s.turn).toBe(1);
    expect(s.distanceIndex).toBe(1); // mid
    expect(s.playerHp).toBe(30);
    expect(s.enemyHp).toBe(38);
    expect(s.playerStamina).toBe(20);
    expect(s.enemyStamina).toBe(20);
    expect(s.result).toBe("ongoing");
    expect(s.hand).toHaveLength(3);
    expect(s.drawPile).toHaveLength(9);
    expect(s.hand.length + s.drawPile.length + s.discardPile.length).toBe(12);
    expect(s.log.length).toBeGreaterThan(0);
  });
});

describe("chooseEnemyAction", () => {
  it("close: shove when affordable, else reposition, else rest", () => {
    expect(chooseEnemyAction(0, 20)?.id).toBe("shove");
    expect(chooseEnemyAction(0, 2)?.id).toBe("reposition");
    expect(chooseEnemyAction(0, 1)).toBeNull();
  });

  it("mid: sweep, else reach_thrust, else rest", () => {
    expect(chooseEnemyAction(1, 20)?.id).toBe("sweep");
    expect(chooseEnemyAction(1, 4)?.id).toBe("reach_thrust");
    expect(chooseEnemyAction(1, 3)).toBeNull();
  });

  it("far: reach_thrust, else reposition, else rest", () => {
    expect(chooseEnemyAction(2, 20)?.id).toBe("reach_thrust");
    expect(chooseEnemyAction(2, 3)?.id).toBe("reposition");
    expect(chooseEnemyAction(2, 1)).toBeNull();
  });
});

describe("resolveEnemyTurn", () => {
  it("sweep at mid deals full damage and holds the band", () => {
    const out = resolveEnemyTurn(1, 20, 0);
    expect(out.action?.id).toBe("sweep");
    expect(out.damage).toBe(8);
    expect(out.newDistanceIndex).toBe(1);
    expect(out.staminaSpent).toBe(5);
  });

  it("shove at close pushes the player back to mid", () => {
    const out = resolveEnemyTurn(0, 20, 0);
    expect(out.action?.id).toBe("shove");
    expect(out.damage).toBe(2);
    expect(out.newDistanceIndex).toBe(1); // pushed close -> mid
  });

  it("guard absorbs damage and is consumed", () => {
    const out = resolveEnemyTurn(1, 20, 6); // sweep raw 8 vs guard 6
    expect(out.rawDamage).toBe(8);
    expect(out.damage).toBe(2);
    expect(out.newGuard).toBe(0);
  });

  it("rests when no action is affordable", () => {
    const out = resolveEnemyTurn(0, 1, 0);
    expect(out.action).toBeNull();
    expect(out.damage).toBe(0);
    expect(out.newDistanceIndex).toBe(0);
  });
});

describe("battleReducer PLAY_CARD", () => {
  it("applies attack damage, spends stamina, and discards the card", () => {
    const card = makeCard({ instanceId: "thrust-x" });
    const s = makeState({
      distanceIndex: 0,
      hand: [card],
      playerStamina: 20,
      enemyHp: 42,
    });
    const next = battleReducer(s, {
      type: "PLAY_CARD",
      instanceId: "thrust-x",
    });

    expect(next.enemyHp).toBe(33); // 42 - 9
    expect(next.playerStamina).toBe(16); // 20 - 4
    expect(next.hand).toHaveLength(0);
    expect(next.discardPile).toContain(card);
  });

  it("moves distance for cards with a shift", () => {
    const stepIn = makeCard({
      instanceId: "step",
      type: "move",
      cost: 2,
      effectiveRange: null,
      basePower: 0,
      shift: -1,
    });
    const s = makeState({
      distanceIndex: 1,
      hand: [stepIn],
      playerStamina: 20,
    });
    const next = battleReducer(s, { type: "PLAY_CARD", instanceId: "step" });
    expect(next.distanceIndex).toBe(0);
  });

  it("grants guard for guard cards", () => {
    const brace = makeCard({
      instanceId: "brace",
      type: "guard",
      cost: 2,
      effectiveRange: null,
      basePower: 0,
      guard: 6,
    });
    const s = makeState({ hand: [brace], playerStamina: 20, playerGuard: 0 });
    const next = battleReducer(s, { type: "PLAY_CARD", instanceId: "brace" });
    expect(next.playerGuard).toBe(6);
  });

  it("rejects a card the player cannot afford", () => {
    const card = makeCard({ instanceId: "thrust-x", cost: 4 });
    const s = makeState({ hand: [card], playerStamina: 3 });
    const next = battleReducer(s, {
      type: "PLAY_CARD",
      instanceId: "thrust-x",
    });
    expect(next).toBe(s); // unchanged reference
  });

  it("wins when the enemy is reduced to zero", () => {
    const card = makeCard({ instanceId: "thrust-x" });
    const s = makeState({
      distanceIndex: 0,
      hand: [card],
      playerStamina: 20,
      enemyHp: 5,
    });
    const next = battleReducer(s, {
      type: "PLAY_CARD",
      instanceId: "thrust-x",
    });
    expect(next.enemyHp).toBe(0);
    expect(next.result).toBe("won");
  });

  it("ignores card plays once the battle is over", () => {
    const card = makeCard({ instanceId: "thrust-x" });
    const s = makeState({ hand: [card], result: "won" });
    const next = battleReducer(s, {
      type: "PLAY_CARD",
      instanceId: "thrust-x",
    });
    expect(next).toBe(s);
  });
});

describe("battleReducer END_TURN", () => {
  it("resolves the enemy sweep at mid and opens the next player turn", () => {
    const s = makeState({
      distanceIndex: 1,
      enemyStamina: 20,
      playerHp: 30,
      playerStamina: 10,
      playerGuard: 0,
      hand: [],
    });
    const next = battleReducer(s, { type: "END_TURN" });

    expect(next.playerHp).toBe(22); // sweep 8
    expect(next.enemyStamina).toBe(15); // 20 capped, then -5
    expect(next.distanceIndex).toBe(1);
    expect(next.playerStamina).toBe(12); // 10 + mid recovery 2
    expect(next.turn).toBe(2);
    expect(next.result).toBe("ongoing");
  });

  it("lets the enemy shove the player out of close range", () => {
    const s = makeState({
      distanceIndex: 0,
      enemyStamina: 20,
      playerHp: 30,
      playerStamina: 10,
      hand: [],
    });
    const next = battleReducer(s, { type: "END_TURN" });

    expect(next.distanceIndex).toBe(1); // pushed to mid
    expect(next.playerHp).toBe(28); // shove 2
    expect(next.enemyStamina).toBe(17); // 20 capped, then -3
  });

  it("consumes leftover guard before clearing it next turn", () => {
    const s = makeState({
      distanceIndex: 1,
      enemyStamina: 20,
      playerHp: 30,
      playerGuard: 6,
      hand: [],
    });
    const next = battleReducer(s, { type: "END_TURN" });

    expect(next.playerHp).toBe(28); // 8 sweep - 6 guard = 2
    expect(next.playerGuard).toBe(0); // cleared at player turn start
  });

  it("ends in defeat when the enemy lands a lethal blow", () => {
    const s = makeState({
      distanceIndex: 1,
      enemyStamina: 20,
      playerHp: 5,
      playerGuard: 0,
      hand: [],
    });
    const next = battleReducer(s, { type: "END_TURN" });

    expect(next.playerHp).toBe(0);
    expect(next.result).toBe("lost");
  });

  it("is a no-op once the battle is over", () => {
    const s = makeState({ result: "lost" });
    const next = battleReducer(s, { type: "END_TURN" });
    expect(next).toBe(s);
  });
});

describe("battleReducer RESTART", () => {
  it("returns a fresh battle", () => {
    const s = makeState({ playerHp: 1, enemyHp: 1, turn: 9, result: "lost" });
    const next = battleReducer(s, { type: "RESTART" });
    expect(next.playerHp).toBe(30);
    expect(next.enemyHp).toBe(38);
    expect(next.turn).toBe(1);
    expect(next.result).toBe("ongoing");
  });
});
