/**
 * Card Derivation Hydrator
 *
 * Applies derivation registry data to card definitions, setting
 * derivesInto/derivedFrom/unlockMasteryLevel fields automatically.
 *
 * This is called at card data load time so that card definitions
 * always have their derivation relationships populated.
 */

import type { Card } from "@/types/cardTypes";
import type { DerivationEntry } from "@/constants/data/cards/cardDerivationRegistry";

/**
 * Apply derivation registry entries to a set of card definitions.
 * Mutates the card records in-place for efficiency (called once at module load).
 *
 * For each DerivationEntry:
 * - Sets parent card's `derivesInto` to include the derived card's typeId
 * - Sets derived card's `derivedFrom` to the parent's typeId
 * - Sets derived card's `unlockMasteryLevel` to the required mastery
 *
 * @param cards - Card definitions record (cardTypeId -> Card)
 * @param derivations - Array of derivation entries to apply
 * @returns The same cards record with derivation fields populated
 */
export function hydrateDerivationData(
  cards: Record<string, Card>,
  derivations: DerivationEntry[],
): Record<string, Card> {
  for (const entry of derivations) {
    const parentCard = cards[entry.parentCardTypeId];
    const derivedCard = cards[entry.derivedCardTypeId];

    if (!parentCard || !derivedCard) {
      console.warn(
        `[cardDerivationHydrator] Skipping invalid derivation: ` +
          `${entry.parentCardTypeId} -> ${entry.derivedCardTypeId} ` +
          `(parent: ${!!parentCard}, derived: ${!!derivedCard})`,
      );
      continue;
    }

    // Set parent's derivesInto
    if (!parentCard.derivesInto) {
      parentCard.derivesInto = [];
    }
    if (!parentCard.derivesInto.includes(entry.derivedCardTypeId)) {
      parentCard.derivesInto.push(entry.derivedCardTypeId);
    }

    // Set derived card's derivedFrom and unlockMasteryLevel
    derivedCard.derivedFrom = entry.parentCardTypeId;
    derivedCard.unlockMasteryLevel = entry.requiredMastery;
  }

  return cards;
}
