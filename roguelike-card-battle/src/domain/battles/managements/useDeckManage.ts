/**
 * Battle Deck State Management Hook
 * Manages deck state, card drawing, and discarding with animations.
 * Note: Core deck logic (shuffle, draw) is in domain/cards/decks/deck.ts (IMMUTABLE)
 */

import { useReducer, useRef, useEffect, useState, useCallback } from "react";
import type { Card } from "../../cards/type/cardType";
import { deckReducer, type DeckState } from "../../cards/decks/deckReducter";
import { createInitialDeck, drawCards, shuffleArray } from "../../cards/decks/deck";
import { useCardAnimation } from "../../../ui/commonUI/useCardAnimation";
import { SWORDSMAN_CARDS_ARRAY } from "../../cards/data/SwordmanCards";
import { INITIAL_DECK_COUNTS } from "../data/initialDeckConfig";

// Get the return type of useCardAnimation
type CardAnimationReturn = ReturnType<typeof useCardAnimation>;

export interface UseBattleDeckReturn {
    // State
    hand: Card[];
    drawPile: Card[];
    discardPile: Card[];
    isShuffling: boolean;
    isDrawingAnimation: boolean;

    // Animation helpers
    isNewCard: (cardId: string) => boolean;
    getDiscardingCards: () => Card[];

    // Actions
    drawCardsToHand: (count: number) => Card[];
    playCard: (card: Card) => void;
    discardHand: (onComplete?: () => void) => void;
    addCardsToHand: (cards: Card[]) => void;
    resetDeck: () => void;
    resetDeckForNextEnemy: () => void;

    // Refs for external access
    deckStateRef: React.RefObject<DeckState>;
    drawnCardsRef: React.RefObject<Card[]>;

    // Dispatch for direct reducer access
    dispatch: React.Dispatch<import("../../cards/decks/deckReducter").DeckAction>;

    // Animation hooks pass-through
    drawCardsWithAnimation: CardAnimationReturn["drawCardsWithAnimation"];
    discardCardsWithAnimation: CardAnimationReturn["discardCardsWithAnimation"];
    playCardWithAnimation: CardAnimationReturn["playCardWithAnimation"];
}

export function useBattleDeck(): UseBattleDeckReturn {
    // Animation hooks
    const {
        drawCardsWithAnimation,
        discardCardsWithAnimation,
        playCardWithAnimation,
        isNewCard,
        getDiscardingCards,
    } = useCardAnimation();

    // Refs
    const drawnCardsRef = useRef<Card[]>([]);

    // Animation state
    const [isShuffling, setIsShuffling] = useState(false);
    const [isDrawingAnimation, setIsDrawingAnimation] = useState(false);

    // Initial deck state
    const initialDeckState = (() => {
        const initialDeck = createInitialDeck(INITIAL_DECK_COUNTS, SWORDSMAN_CARDS_ARRAY);
        const { drawnCards, newDrawPile, newDiscardPile } = drawCards(5, initialDeck, []);
        return { hand: drawnCards, drawPile: newDrawPile, discardPile: newDiscardPile };
    })();

    const [deckState, dispatch] = useReducer(deckReducer, initialDeckState);
    const deckStateRef = useRef(deckState);

    // Keep ref in sync
    useEffect(() => {
        deckStateRef.current = deckState;
    }, [deckState]);

    /**
     * Draw cards and trigger animation
     */
    const drawCardsToHand = useCallback((count: number): Card[] => {
        const currentDeck = deckStateRef.current;
        const { drawnCards: newCards, newDrawPile, newDiscardPile } = drawCards(
            count,
            currentDeck.drawPile,
            currentDeck.discardPile
        );

        drawnCardsRef.current = newCards;
        dispatch({ type: "SET_PILES", newDrawPile, newDiscardPile });

        // Check if shuffle occurred
        if (newDiscardPile.length === 0 && currentDeck.discardPile.length > 0) {
            setIsShuffling(true);
        } else if (newCards.length > 0) {
            setIsDrawingAnimation(true);
        }

        return newCards;
    }, []);

    /**
     * Play a card (move to discard)
     */
    const playCard = useCallback((card: Card) => {
        dispatch({ type: "CARD_PLAY", card });
    }, []);

    /**
     * Discard entire hand with animation
     */
    const discardHand = useCallback((onComplete?: () => void) => {
        const cardsToDiscard = [...deckStateRef.current.hand];
        discardCardsWithAnimation(cardsToDiscard, 250, () => {
            dispatch({ type: "END_TURN", cardsToDiscard });
            onComplete?.();
        });
    }, [discardCardsWithAnimation]);

    /**
     * Add cards to hand (from draw effects)
     */
    const addCardsToHand = useCallback((cards: Card[]) => {
        dispatch({ type: "ADD_TO_HAND", cards });
    }, []);

    /**
     * Reset deck to initial state
     */
    const resetDeck = useCallback(() => {
        const initialDeck = createInitialDeck(INITIAL_DECK_COUNTS, SWORDSMAN_CARDS_ARRAY);
        const { drawnCards, newDrawPile, newDiscardPile } = drawCards(5, initialDeck, []);
        dispatch({
            type: "RESET_DECK",
            hand: drawnCards,
            drawPile: newDrawPile,
            discardPile: newDiscardPile,
        });
    }, []);

    /**
     * Reset deck for next enemy (shuffle all cards)
     */
    const resetDeckForNextEnemy = useCallback(() => {
        const allCards = [
            ...deckStateRef.current.hand,
            ...deckStateRef.current.drawPile,
            ...deckStateRef.current.discardPile,
        ];
        const shuffledDeck = shuffleArray(allCards);
        const newHand = shuffledDeck.slice(0, 5);
        const newDrawPile = shuffledDeck.slice(5);
        dispatch({
            type: "RESET_DECK",
            hand: newHand,
            drawPile: newDrawPile,
            discardPile: [],
        });
    }, []);

    // Handle shuffle animation
    useEffect(() => {
        if (isShuffling) {
            const timer = setTimeout(() => {
                setIsShuffling(false);
                setIsDrawingAnimation(true);
            }, 1500);
            return () => clearTimeout(timer);
        }
    }, [isShuffling]);

    // Handle draw animation
    useEffect(() => {
        if (isDrawingAnimation && drawnCardsRef.current.length > 0) {
            drawCardsWithAnimation(drawnCardsRef.current, (cards) => {
                dispatch({ type: "ADD_TO_HAND", cards });
                drawnCardsRef.current = [];
                setIsDrawingAnimation(false);
            }, 250);
        }
    }, [isDrawingAnimation, drawCardsWithAnimation]);

    return {
        // State
        hand: deckState.hand,
        drawPile: deckState.drawPile,
        discardPile: deckState.discardPile,
        isShuffling,
        isDrawingAnimation,

        // Animation helpers
        isNewCard,
        getDiscardingCards,

        // Actions
        drawCardsToHand,
        playCard,
        discardHand,
        addCardsToHand,
        resetDeck,
        resetDeckForNextEnemy,

        // Refs
        deckStateRef,
        drawnCardsRef,

        // Direct access
        dispatch,

        // Animation hooks
        drawCardsWithAnimation,
        discardCardsWithAnimation,
        playCardWithAnimation,
    };
}
