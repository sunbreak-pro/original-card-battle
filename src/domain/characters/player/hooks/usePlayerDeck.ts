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

    setPlayerState((prev) => ({
      ...prev,
      deck: newDeck,
    }));
  };

  return { updateDeck };
}
