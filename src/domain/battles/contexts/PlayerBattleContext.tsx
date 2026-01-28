// PlayerBattleContext (B1.2)
// Provides player-related battle state: HP, AP, Guard, Energy, Buffs, Deck, Card execution

import React, { createContext, useContext, type ReactNode } from "react";
import type { PlayerBattleContextValue } from "./battleContextTypes";

const PlayerBattleContext = createContext<PlayerBattleContextValue | undefined>(
  undefined
);

/**
 * PlayerBattleProvider
 * Receives value from BattleProviderStack which extracts player slice from orchestrator
 */
export const PlayerBattleProvider: React.FC<{
  value: PlayerBattleContextValue;
  children: ReactNode;
}> = ({ value, children }) => {
  return (
    <PlayerBattleContext.Provider value={value}>
      {children}
    </PlayerBattleContext.Provider>
  );
};

/**
 * Hook to access player battle context
 */
export function usePlayerBattle(): PlayerBattleContextValue {
  const context = useContext(PlayerBattleContext);
  if (!context) {
    throw new Error(
      "usePlayerBattle must be used within PlayerBattleProvider"
    );
  }
  return context;
}
