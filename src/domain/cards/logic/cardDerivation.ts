/**
 * Card Derivation System
 *
 * Handles card evolution/derivation based on mastery levels.
 * When a card reaches a certain mastery level, it can unlock derived cards.
 */

import type { Card, MasteryLevel } from "@/types/cardTypes";

/**
 * Result of checking derivation unlocks after a battle
 */
export interface DerivationUnlock {
  /** The parent card that triggered the unlock */
  parentCard: Card;
  /** The newly unlocked derived card */
  derivedCard: Card;
}

/**
 * Check if a card has reached the mastery level to unlock any derivations
 * @param card - The card to check
 * @param allCards - All available card definitions for the class
 * @param unlockedCardTypeIds - Set of already unlocked card type IDs
 * @returns Array of newly unlocked derived cards
 */
export function checkDerivationUnlocks(
  card: Card,
  allCards: Record<string, Card>,
  unlockedCardTypeIds: Set<string>
): DerivationUnlock[] {
  if (!card.derivesInto || card.derivesInto.length === 0) {
    return [];
  }

  const unlocks: DerivationUnlock[] = [];

  for (const derivedCardTypeId of card.derivesInto) {
    // Skip already unlocked cards
    if (unlockedCardTypeIds.has(derivedCardTypeId)) continue;

    const derivedCard = allCards[derivedCardTypeId];
    if (!derivedCard) continue;

    // Check if parent card meets the mastery requirement
    const requiredMastery = derivedCard.unlockMasteryLevel ?? 2;
    if (card.masteryLevel >= requiredMastery) {
      unlocks.push({ parentCard: card, derivedCard });
    }
  }

  return unlocks;
}

/**
 * Check all cards in deck for new derivation unlocks
 * @param deckCards - Cards in the player's deck (with current mastery)
 * @param allCards - All available card definitions for the class
 * @param unlockedCardTypeIds - Set of already unlocked card type IDs
 * @returns Array of all newly unlocked derivations
 */
export function checkAllDerivationUnlocks(
  deckCards: Card[],
  allCards: Record<string, Card>,
  unlockedCardTypeIds: Set<string>
): DerivationUnlock[] {
  const allUnlocks: DerivationUnlock[] = [];
  const checkedTypeIds = new Set<string>();

  for (const card of deckCards) {
    // Only check each card type once
    if (checkedTypeIds.has(card.cardTypeId)) continue;
    checkedTypeIds.add(card.cardTypeId);

    const unlocks = checkDerivationUnlocks(card, allCards, unlockedCardTypeIds);
    allUnlocks.push(...unlocks);
  }

  return allUnlocks;
}

/**
 * Get the derivation chain for a card (ancestors and descendants)
 * @param cardTypeId - The card type ID to trace
 * @param allCards - All available card definitions
 * @returns Array of card type IDs in the derivation chain
 */
export function getDerivationChain(
  cardTypeId: string,
  allCards: Record<string, Card>
): string[] {
  const chain: string[] = [];
  const card = allCards[cardTypeId];
  if (!card) return chain;

  // Trace ancestors
  let current = card;
  while (current.derivedFrom) {
    chain.unshift(current.derivedFrom);
    const parent = allCards[current.derivedFrom];
    if (!parent) break;
    current = parent;
  }

  // Add self
  chain.push(cardTypeId);

  // Add descendants (breadth-first)
  const queue = [cardTypeId];
  while (queue.length > 0) {
    const currentId = queue.shift()!;
    const currentCard = allCards[currentId];
    if (currentCard?.derivesInto) {
      for (const childId of currentCard.derivesInto) {
        if (!chain.includes(childId)) {
          chain.push(childId);
          queue.push(childId);
        }
      }
    }
  }

  return chain;
}

/**
 * Get the mastery level required to unlock a derived card
 */
export function getRequiredMasteryForDerivation(
  derivedCard: Card
): MasteryLevel {
  return derivedCard.unlockMasteryLevel ?? 2;
}
