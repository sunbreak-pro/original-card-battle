import { discardCards } from "./deck";
import type { Card } from "../type/cardType";

export interface DeckState {
  drawPile: Card[];
  hand: Card[];
  discardPile: Card[];
}

export type DeckAction =
  | { type: "END_TURN"; cardsToDiscard: Card[] }
  | { type: "CARD_PLAY"; card: Card }
  | { type: "SET_PILES"; newDrawPile: Card[]; newDiscardPile: Card[] }
  | { type: "ADD_TO_HAND"; cards: Card[] }
  | { type: "RESET_DECK"; hand: Card[]; drawPile: Card[]; discardPile: Card[] };

export const deckReducer = (
  state: DeckState,
  action: DeckAction
): DeckState => {
  switch (action.type) {
    case "END_TURN": {
      const newDiscard = discardCards(action.cardsToDiscard, state.discardPile);
      return {
        ...state,
        discardPile: newDiscard,
        hand: [],
      };
    }

    case "CARD_PLAY": {
      const newHand = state.hand.filter((c) => c.id !== action.card.id);
      const newDiscard = discardCards([action.card], state.discardPile);
      return {
        ...state,
        hand: newHand,
        discardPile: newDiscard,
      };
    }

    case "SET_PILES": {
      return {
        ...state,
        drawPile: action.newDrawPile,
        discardPile: action.newDiscardPile,
      };
    }

    case "ADD_TO_HAND": {
      return {
        ...state,
        hand: [...state.hand, ...action.cards],
      };
    }

    case "RESET_DECK": {
      return {
        hand: action.hand,
        drawPile: action.drawPile,
        discardPile: action.discardPile,
      };
    }

    default:
      return state;
  }
};