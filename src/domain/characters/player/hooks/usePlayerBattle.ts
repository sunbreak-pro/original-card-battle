import type React from "react";
import type { RuntimeBattleState } from "@/contexts/PlayerContext";
import type { Difficulty } from "@/types/characterTypes";
import {
  createLivesSystem,
  decreaseLives as decreaseLivesHelper,
  isGameOver as isGameOverHelper,
} from "../logic/playerUtils";

type SetRuntimeState = React.Dispatch<
  React.SetStateAction<RuntimeBattleState>
>;

export function usePlayerBattle(
  runtimeState: RuntimeBattleState,
  setRuntimeState: SetRuntimeState,
) {
  const updateRuntimeState = (updates: Partial<RuntimeBattleState>) => {
    setRuntimeState((prev) => ({
      ...prev,
      ...updates,
    }));
  };

  const updateCardMastery = (cardTypeId: string, useCount: number) => {
    setRuntimeState((prev) => {
      const newStore = new Map(prev.cardMasteryStore);
      const currentCount = newStore.get(cardTypeId) || 0;
      newStore.set(cardTypeId, currentCount + useCount);
      return {
        ...prev,
        cardMasteryStore: newStore,
      };
    });
  };

  const decreaseLives = () => {
    setRuntimeState((prev) => ({
      ...prev,
      lives: decreaseLivesHelper(prev.lives),
    }));
  };

  const isGameOver = () => {
    return isGameOverHelper(runtimeState.lives);
  };

  const setDifficulty = (difficulty: Difficulty) => {
    setRuntimeState((prev) => ({
      ...prev,
      difficulty,
      lives: createLivesSystem(difficulty),
    }));
  };

  return {
    updateRuntimeState,
    updateCardMastery,
    decreaseLives,
    isGameOver,
    setDifficulty,
  };
}
