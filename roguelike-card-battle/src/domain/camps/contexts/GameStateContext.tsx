// GameStateContext: Manages global game state and screen navigation
// Note: encounterCount is now managed by DungeonRunContext

import React, {
  createContext,
  useContext,
  useState,
  type ReactNode,
} from "react";
import type {
  GameScreen,
  BattleMode,
  Depth,
  BattleConfig,
} from "../types/CampTypes";

/**
 * Game state interface
 * Tracks current screen, battle configuration, and navigation state
 * Note: encounterCount has been moved to DungeonRunContext
 */
export interface GameState {
  currentScreen: GameScreen;
  battleMode: BattleMode;
  depth: Depth;
  battleConfig?: BattleConfig;
}

/**
 * GameState Context value
 */
interface GameStateContextValue {
  gameState: GameState;
  setGameState: React.Dispatch<React.SetStateAction<GameState>>;
  navigateTo: (screen: GameScreen) => void;
  startBattle: (config: BattleConfig, mode?: BattleMode) => void;
  returnToCamp: () => void;
  setDepth: (depth: Depth) => void;
}

const GameStateContext = createContext<GameStateContextValue | undefined>(
  undefined
);

/**
 * Initial game state
 */
const initialGameState: GameState = {
  currentScreen: "camp",
  battleMode: null,
  depth: 1,
};

/**
 * GameState Provider Component
 */
export const GameStateProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [gameState, setGameState] = useState<GameState>(initialGameState);

  /**
   * Navigate to a specific screen
   */
  const navigateTo = (screen: GameScreen) => {
    setGameState((prev) => ({
      ...prev,
      currentScreen: screen,
      // Clear battle config when leaving battle
      ...(screen !== "battle" && { battleConfig: undefined, battleMode: null }),
    }));
  };

  /**
   * Start a battle with specific configuration
   */
  const startBattle = (config: BattleConfig, mode: BattleMode = "normal") => {
    setGameState((prev) => ({
      ...prev,
      currentScreen: "battle",
      battleMode: mode,
      battleConfig: config,
    }));
  };

  /**
   * Return to BaseCamp
   */
  const returnToCamp = () => {
    setGameState((prev) => ({
      ...prev,
      currentScreen: "camp",
      battleMode: null,
      battleConfig: undefined,
    }));
  };

  /**
   * Set current dungeon depth
   */
  const setDepth = (depth: Depth) => {
    setGameState((prev) => ({ ...prev, depth }));
  };

  return (
    <GameStateContext.Provider
      value={{
        gameState,
        setGameState,
        navigateTo,
        startBattle,
        returnToCamp,
        setDepth,
      }}
    >
      {children}
    </GameStateContext.Provider>
  );
};

/**
 * Hook to use GameState context
 */
export const useGameState = () => {
  const context = useContext(GameStateContext);
  if (!context) {
    throw new Error("useGameState must be used within GameStateProvider");
  }
  return context;
};
