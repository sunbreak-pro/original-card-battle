// Dungeon run state management context

import {
  createContext,
  useContext,
  useState,
  useCallback,
  type ReactNode,
} from "react";
import type { Depth } from '@/types/campTypes';
import type {
  DungeonRun,
  DungeonNode,
  NodeCompletionResult,
} from '@/types/dungeonTypes';
import {
  initializeDungeonRun,
  selectNode,
  completeNode,
  getNodeById,
} from "../../domain/dungeon/logic/dungeonLogic";

/**
 * Context value interface
 */
interface DungeonRunContextValue {
  // Current dungeon run state
  dungeonRun: DungeonRun | null;

  // Initialize a new dungeon run
  initializeRun: (depth: Depth) => void;

  // Select a node to visit
  selectNodeToVisit: (nodeId: string) => void;

  // Complete the current node with a result
  completeCurrentNode: (result: NodeCompletionResult) => void;

  // Retreat from the dungeon (back to camp)
  retreatFromDungeon: () => void;

  // Get current node info
  getCurrentNode: () => DungeonNode | null;

  // Increment encounter count
  incrementEncounter: () => void;
}

// Create the context
const DungeonRunContext = createContext<DungeonRunContextValue | null>(null);

/**
 * Provider props
 */
interface DungeonRunProviderProps {
  children: ReactNode;
  initialRun?: DungeonRun | null;
}

/**
 * DungeonRunProvider component
 * Provides dungeon run state management to child components
 */
export function DungeonRunProvider({
  children,
  initialRun = null,
}: DungeonRunProviderProps) {
  const [dungeonRun, setDungeonRun] = useState<DungeonRun | null>(initialRun);

  // Initialize a new dungeon run
  const initializeRun = useCallback((depth: Depth) => {
    const newRun = initializeDungeonRun(depth);
    setDungeonRun(newRun);
  }, []);

  // Select a node to visit
  const selectNodeToVisit = useCallback((nodeId: string) => {
    setDungeonRun((prev) => {
      if (!prev) return prev;

      const updatedFloor = selectNode(prev.currentFloor, nodeId);
      return {
        ...prev,
        currentFloor: updatedFloor,
      };
    });
  }, []);

  // Complete the current node
  const completeCurrentNode = useCallback((result: NodeCompletionResult) => {
    setDungeonRun((prev) => {
      if (!prev || !prev.currentFloor.currentNodeId) return prev;

      const updatedFloor = completeNode(
        prev.currentFloor,
        prev.currentFloor.currentNodeId,
        result
      );

      // If defeated, end the run
      if (result === "defeat") {
        return {
          ...prev,
          currentFloor: updatedFloor,
          isActive: false,
        };
      }

      return {
        ...prev,
        currentFloor: updatedFloor,
        encounterCount: prev.encounterCount + 1,
      };
    });
  }, []);

  // Retreat from dungeon
  const retreatFromDungeon = useCallback(() => {
    setDungeonRun((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        isActive: false,
      };
    });
  }, []);

  // Get current node
  const getCurrentNode = useCallback((): DungeonNode | null => {
    if (!dungeonRun || !dungeonRun.currentFloor.currentNodeId) {
      return null;
    }
    return (
      getNodeById(dungeonRun.currentFloor, dungeonRun.currentFloor.currentNodeId) ||
      null
    );
  }, [dungeonRun]);

  // Increment encounter count
  const incrementEncounter = useCallback(() => {
    setDungeonRun((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        encounterCount: prev.encounterCount + 1,
      };
    });
  }, []);

  const value: DungeonRunContextValue = {
    dungeonRun,
    initializeRun,
    selectNodeToVisit,
    completeCurrentNode,
    retreatFromDungeon,
    getCurrentNode,
    incrementEncounter,
  };

  return (
    <DungeonRunContext.Provider value={value}>
      {children}
    </DungeonRunContext.Provider>
  );
}

/**
 * Hook to use dungeon run context
 */
export function useDungeonRun(): DungeonRunContextValue {
  const context = useContext(DungeonRunContext);
  if (!context) {
    throw new Error("useDungeonRun must be used within a DungeonRunProvider");
  }
  return context;
}

export default DungeonRunContext;
