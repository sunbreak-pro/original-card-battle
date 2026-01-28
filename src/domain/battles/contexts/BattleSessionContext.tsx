// BattleSessionContext (B1.4)
// Provides session-level battle state: phase flow, turn order, battle result

import React, { createContext, useContext, type ReactNode } from "react";
import type { BattleSessionContextValue } from "./battleContextTypes";

const BattleSessionContext = createContext<
  BattleSessionContextValue | undefined
>(undefined);

/**
 * BattleSessionProvider
 * Receives value from BattleProviderStack which extracts session slice from orchestrator
 */
export const BattleSessionProvider: React.FC<{
  value: BattleSessionContextValue;
  children: ReactNode;
}> = ({ value, children }) => {
  return (
    <BattleSessionContext.Provider value={value}>
      {children}
    </BattleSessionContext.Provider>
  );
};

/**
 * Hook to access battle session context
 */
export function useBattleSession(): BattleSessionContextValue {
  const context = useContext(BattleSessionContext);
  if (!context) {
    throw new Error(
      "useBattleSession must be used within BattleSessionProvider"
    );
  }
  return context;
}
