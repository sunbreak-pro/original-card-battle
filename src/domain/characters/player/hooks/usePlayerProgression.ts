import type React from "react";
import type { InternalPlayerState } from "@/contexts/PlayerContext";

type SetPlayerState = React.Dispatch<
  React.SetStateAction<InternalPlayerState>
>;

export function usePlayerProgression(setPlayerState: SetPlayerState) {
  const updateClassGrade = (newGrade: string) => {
    setPlayerState((prev) => ({ ...prev, classGrade: newGrade }));
  };

  const updateBaseMaxHp = (delta: number) => {
    setPlayerState((prev) => ({
      ...prev,
      maxHp: prev.maxHp + delta,
      hp: prev.hp + delta,
    }));
  };

  const updateBaseMaxAp = (delta: number) => {
    setPlayerState((prev) => ({
      ...prev,
      maxAp: prev.maxAp + delta,
      ap: prev.ap + delta,
    }));
  };

  const updateHp = (newHp: number) => {
    setPlayerState((prev) => ({
      ...prev,
      hp: Math.max(0, Math.min(newHp, prev.maxHp)),
    }));
  };

  const updateAp = (newAp: number) => {
    setPlayerState((prev) => ({
      ...prev,
      ap: Math.max(0, Math.min(newAp, prev.maxAp)),
    }));
  };

  const addSouls = (amount: number) => {
    setPlayerState((prev) => ({
      ...prev,
      sanctuaryProgress: {
        ...prev.sanctuaryProgress,
        currentRunSouls: prev.sanctuaryProgress.currentRunSouls + amount,
      },
    }));
  };

  const transferSouls = (survivalMultiplier: number) => {
    setPlayerState((prev) => {
      const transferredSouls = Math.floor(
        prev.sanctuaryProgress.currentRunSouls * survivalMultiplier,
      );
      return {
        ...prev,
        sanctuaryProgress: {
          ...prev.sanctuaryProgress,
          totalSouls: prev.sanctuaryProgress.totalSouls + transferredSouls,
          currentRunSouls: 0,
        },
      };
    });
  };

  const resetCurrentRunSouls = () => {
    setPlayerState((prev) => ({
      ...prev,
      sanctuaryProgress: {
        ...prev.sanctuaryProgress,
        currentRunSouls: 0,
      },
    }));
  };

  return {
    updateClassGrade,
    updateBaseMaxHp,
    updateBaseMaxAp,
    updateHp,
    updateAp,
    addSouls,
    transferSouls,
    resetCurrentRunSouls,
  };
}
