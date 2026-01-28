// EnemyBattleContext (B1.3)
// Provides enemy-related battle state: enemies, HP, AP, Guard, Buffs, AI

import React, { createContext, useContext, type ReactNode } from "react";
import type { EnemyBattleContextValue } from "./battleContextTypes";

const EnemyBattleContext = createContext<EnemyBattleContextValue | undefined>(
  undefined
);

/**
 * EnemyBattleProvider
 * Receives value from BattleProviderStack which extracts enemy slice from orchestrator
 */
export const EnemyBattleProvider: React.FC<{
  value: EnemyBattleContextValue;
  children: ReactNode;
}> = ({ value, children }) => {
  return (
    <EnemyBattleContext.Provider value={value}>
      {children}
    </EnemyBattleContext.Provider>
  );
};

/**
 * Hook to access enemy battle context
 */
export function useEnemyBattle(): EnemyBattleContextValue {
  const context = useContext(EnemyBattleContext);
  if (!context) {
    throw new Error(
      "useEnemyBattle must be used within EnemyBattleProvider"
    );
  }
  return context;
}
