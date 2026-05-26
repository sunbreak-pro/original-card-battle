import type React from "react";
import type { Card } from "@/types/cardTypes";
import type { CharacterClass } from "@/types/characterTypes";
import type { InternalPlayerState } from "@/contexts/PlayerContext";
import { getCardDataByClass } from "@/constants/data/characters/CharacterClassData";

type SetPlayerState = React.Dispatch<
  React.SetStateAction<InternalPlayerState>
>;

export function usePlayerDeck(
  playerClass: CharacterClass,
  setPlayerState: SetPlayerState,
  onDiscoverCard?: (cardTypeId: string) => void,
) {
  const updateDeck = (cardTypeIds: string[]) => {
    const cardData = getCardDataByClass(playerClass);
    let instanceCounter = 1;
    const newDeck: Card[] = [];

    for (const cardTypeId of cardTypeIds) {
      const cardTemplate = cardData[cardTypeId];
      if (cardTemplate) {
        newDeck.push({
          ...cardTemplate,
          id: `deck_${cardTypeId}_${instanceCounter++}`,
        });
      }
    }

    // Discover new cards added to the deck
    if (onDiscoverCard) {
      setPlayerState((prev) => {
        const prevIds = new Set(prev.deck.map((c) => c.cardTypeId));
        for (const id of cardTypeIds) {
          if (!prevIds.has(id)) {
            onDiscoverCard(id);
          }
        }
        return { ...prev, deck: newDeck };
      });
    } else {
      setPlayerState((prev) => ({
        ...prev,
        deck: newDeck,
      }));
    }
  };

  return { updateDeck };
}
