// Swordsman card definitions + self-contained deck operations.
// Deck shuffle/draw/discard is implemented here on purpose: the battle-lab core
// must NOT import the immutable production deck (src/domain/cards/decks/deck.ts).

import type { CardDefId, CardType, PrototypeCard, RangeBand } from "./types";

interface CardDef {
  readonly defId: CardDefId;
  readonly name: string;
  readonly type: CardType;
  readonly cost: number;
  readonly effectiveRange: RangeBand | null;
  readonly basePower: number;
  readonly shift: number;
  readonly guard: number;
  readonly description: string;
}

export const CARD_DEFS: readonly CardDef[] = [
  {
    defId: "thrust",
    name: "突き",
    type: "attack",
    cost: 4,
    effectiveRange: "close",
    basePower: 9,
    shift: 0,
    guard: 0,
    description: "近接最大火力。",
  },
  {
    defId: "lunge",
    name: "踏み込み斬り",
    type: "attack",
    cost: 5,
    effectiveRange: "close",
    basePower: 7,
    shift: -1,
    guard: 0,
    description: "攻撃しつつ間合いを詰める。",
  },
  {
    defId: "feint",
    name: "牽制",
    type: "attack",
    cost: 3,
    effectiveRange: "mid",
    basePower: 4,
    shift: 1,
    guard: 0,
    description: "削りつつ後退する。",
  },
  {
    defId: "step_in",
    name: "足捌き・前",
    type: "move",
    cost: 2,
    effectiveRange: null,
    basePower: 0,
    shift: -1,
    guard: 0,
    description: "間合いを詰める。",
  },
  {
    defId: "step_out",
    name: "足捌き・後",
    type: "move",
    cost: 1,
    effectiveRange: null,
    basePower: 0,
    shift: 1,
    guard: 0,
    description: "間合いを離す。安い。",
  },
  {
    defId: "brace",
    name: "呼吸を整える",
    type: "guard",
    cost: 2,
    effectiveRange: null,
    basePower: 0,
    shift: 0,
    guard: 6,
    description: "受けを固める。次の被弾を軽減。",
  },
];

const DECK_COPIES = 2;

/** 12-card starting deck: 6 definitions x 2 copies, each with a unique id. */
export function createInitialDeck(): PrototypeCard[] {
  const deck: PrototypeCard[] = [];
  for (const def of CARD_DEFS) {
    for (let copy = 0; copy < DECK_COPIES; copy++) {
      deck.push({ ...def, instanceId: `${def.defId}-${copy}` });
    }
  }
  return deck;
}

/** Fisher-Yates shuffle. Returns a new array; never mutates the input. */
export function shuffle<T>(input: readonly T[]): T[] {
  const arr = [...input];
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

export interface DrawResult {
  readonly hand: PrototypeCard[];
  readonly drawPile: PrototypeCard[];
  readonly discardPile: PrototypeCard[];
}

/**
 * Draw until the hand reaches `target`, reshuffling the discard pile into the
 * draw pile when the draw pile runs dry. Stops early if no cards remain.
 */
export function drawToHandSize(
  drawPile: readonly PrototypeCard[],
  discardPile: readonly PrototypeCard[],
  hand: readonly PrototypeCard[],
  target: number,
): DrawResult {
  let draw = [...drawPile];
  let discard = [...discardPile];
  const newHand = [...hand];
  while (newHand.length < target) {
    if (draw.length === 0) {
      if (discard.length === 0) break;
      draw = shuffle(discard);
      discard = [];
    }
    const next = draw.shift();
    if (next) newHand.push(next);
  }
  return { hand: newHand, drawPile: draw, discardPile: discard };
}
