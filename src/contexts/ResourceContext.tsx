// ResourceContext: Manages gold, magic stones, and exploration limit resources
// Separated from PlayerContext as part of Phase 3 state refactoring

import React, {
  createContext,
  useContext,
  useState,
  useCallback,
  type ReactNode,
} from "react";
import type { MagicStones } from "@/types/itemTypes";
import { calculateMagicStoneValue } from "../domain/item_equipment/logic/itemUtils";
import { calculateStonesToConsume } from "../domain/camps/logic/shopLogic";
import type { ExplorationLimit } from "@/types/campTypes";

/**
 * Resource state structure
 * Separates baseCamp (permanent) from exploration (at-risk) resources
 */
export interface ResourceState {
  gold: {
    baseCamp: number;
    exploration: number;
  };
  magicStones: {
    baseCamp: MagicStones;
    exploration: MagicStones;
  };
  explorationLimit: ExplorationLimit;
}

/**
 * Resource context value interface
 */
interface ResourceContextValue {
  resources: ResourceState;

  // Gold operations
  addGold: (amount: number, toBaseCamp?: boolean) => void;
  spendGold: (amount: number) => boolean;
  getTotalGold: () => number;

  // Magic stones operations
  addMagicStones: (stones: Partial<MagicStones>, toBaseCamp?: boolean) => void;
  setBaseCampMagicStones: (newStones: MagicStones) => void;
  spendBaseCampMagicStones: (goldValueCost: number) => boolean;
  getTotalMagicStonesValue: () => number;
  getBaseCampMagicStones: () => MagicStones;
  getExplorationMagicStones: () => MagicStones;

  // Exploration limit operations
  useExplorationPoint: () => boolean;
  resetExplorationLimit: () => void;
  getExplorationLimit: () => ExplorationLimit;

  // Transfer operations (on survival/death)
  transferExplorationToBaseCamp: (survivalMultiplier: number) => void;
  resetExplorationResources: () => void;
}

const ResourceContext = createContext<ResourceContextValue | undefined>(
  undefined,
);

/**
 * Create initial resource state with test values
 */
function createInitialResourceState(): ResourceState {
  return {
    gold: {
      baseCamp: 1250, // Test value for UI display
      exploration: 0,
    },
    magicStones: {
      baseCamp: { small: 5, medium: 3, large: 1, huge: 0 }, // Test values (450 total)
      exploration: { small: 0, medium: 0, large: 0, huge: 0 },
    },
    explorationLimit: {
      current: 0,
      max: 10,
    },
  };
}

/**
 * ResourceProvider Component
 * Manages all resource-related state
 */
export const ResourceProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [resources, setResources] = useState<ResourceState>(
    createInitialResourceState(),
  );

  /**
   * Add gold to player
   * @param amount - Amount of gold to add
   * @param toBaseCamp - If true, add to baseCamp; otherwise to exploration
   */
  const addGold = useCallback((amount: number, toBaseCamp = false) => {
    setResources((prev) => ({
      ...prev,
      gold: {
        ...prev.gold,
        baseCamp: toBaseCamp ? prev.gold.baseCamp + amount : prev.gold.baseCamp,
        exploration: toBaseCamp
          ? prev.gold.exploration
          : prev.gold.exploration + amount,
      },
    }));
  }, []);

  /**
   * Use gold (deduct from total)
   * Deducts from baseCamp first, then exploration
   * @returns true if successful, false if insufficient gold
   */
  const spendGold = useCallback((amount: number): boolean => {
    const result = { success: false };
    setResources((prev) => {
      const totalGold = prev.gold.baseCamp + prev.gold.exploration;
      if (totalGold < amount) return prev;

      result.success = true;
      let newBaseCampGold = prev.gold.baseCamp;
      let newExplorationGold = prev.gold.exploration;

      if (newBaseCampGold >= amount) {
        newBaseCampGold -= amount;
      } else {
        const remaining = amount - newBaseCampGold;
        newBaseCampGold = 0;
        newExplorationGold -= remaining;
      }

      return {
        ...prev,
        gold: {
          baseCamp: Math.max(0, newBaseCampGold),
          exploration: Math.max(0, newExplorationGold),
        },
      };
    });
    return result.success;
  }, []);

  /**
   * Get total gold (baseCamp + exploration)
   */
  const getTotalGold = useCallback((): number => {
    return resources.gold.baseCamp + resources.gold.exploration;
  }, [resources.gold.baseCamp, resources.gold.exploration]);

  /**
   * Add magic stones
   * @param stones - Partial MagicStones to add
   * @param toBaseCamp - If true, add to baseCamp; otherwise to exploration
   */
  const addMagicStones = useCallback(
    (stones: Partial<MagicStones>, toBaseCamp = false) => {
      setResources((prev) => {
        const target = toBaseCamp ? "baseCamp" : "exploration";
        return {
          ...prev,
          magicStones: {
            ...prev.magicStones,
            [target]: {
              small: prev.magicStones[target].small + (stones.small || 0),
              medium: prev.magicStones[target].medium + (stones.medium || 0),
              large: prev.magicStones[target].large + (stones.large || 0),
              huge: prev.magicStones[target].huge + (stones.huge || 0),
            },
          },
        };
      });
    },
    [],
  );

  /**
   * Set baseCamp magic stones to a specific value (for exchange operations)
   */
  const setBaseCampMagicStones = useCallback((newStones: MagicStones) => {
    setResources((prev) => ({
      ...prev,
      magicStones: {
        ...prev.magicStones,
        baseCamp: newStones,
      },
    }));
  }, []);

  /**
   * Spend baseCamp magic stones worth a given gold value.
   * Uses calculateStonesToConsume to consume smallest stones first.
   * @param goldValueCost - The gold-equivalent value of stones to consume
   * @returns true if successful, false if insufficient stones
   */
  const spendBaseCampMagicStones = useCallback(
    (goldValueCost: number): boolean => {
      if (goldValueCost <= 0) return true;

      const result = { success: false };
      setResources((prev) => {
        const consumeResult = calculateStonesToConsume(
          prev.magicStones.baseCamp,
          goldValueCost,
        );
        if (!consumeResult) return prev;

        result.success = true;
        return {
          ...prev,
          magicStones: {
            ...prev.magicStones,
            baseCamp: consumeResult.newStones,
          },
        };
      });
      return result.success;
    },
    [],
  );

  /**
   * Get total magic stones value
   */
  const getTotalMagicStonesValue = useCallback((): number => {
    const baseCampValue = calculateMagicStoneValue(
      resources.magicStones.baseCamp,
    );
    const explorationValue = calculateMagicStoneValue(
      resources.magicStones.exploration,
    );
    return baseCampValue + explorationValue;
  }, [resources.magicStones.baseCamp, resources.magicStones.exploration]);

  /**
   * Get baseCamp magic stones
   */
  const getBaseCampMagicStones = useCallback((): MagicStones => {
    return resources.magicStones.baseCamp;
  }, [resources.magicStones.baseCamp]);

  /**
   * Get exploration magic stones
   */
  const getExplorationMagicStones = useCallback((): MagicStones => {
    return resources.magicStones.exploration;
  }, [resources.magicStones.exploration]);

  /**
   * Use one exploration point
   * @returns true if successful, false if limit exceeded
   */
  const useExplorationPoint = useCallback((): boolean => {
    const result = { success: false };
    setResources((prev) => {
      if (prev.explorationLimit.current >= prev.explorationLimit.max) {
        return prev;
      }

      result.success = true;
      return {
        ...prev,
        explorationLimit: {
          ...prev.explorationLimit,
          current: prev.explorationLimit.current + 1,
        },
      };
    });
    return result.success;
  }, []);

  /**
   * Reset exploration limit (e.g., on new day or resting)
   */
  const resetExplorationLimit = useCallback(() => {
    setResources((prev) => ({
      ...prev,
      explorationLimit: {
        ...prev.explorationLimit,
        current: 0,
      },
    }));
  }, []);

  /**
   * Get current exploration limit
   */
  const getExplorationLimit = useCallback((): ExplorationLimit => {
    return resources.explorationLimit;
  }, [resources.explorationLimit]);

  /**
   * Transfer exploration resources to BaseCamp (on survival)
   * @param survivalMultiplier - Multiplier based on survival method (0.6-1.0)
   */
  const transferExplorationToBaseCamp = useCallback(
    (survivalMultiplier: number) => {
      setResources((prev) => {
        const transferredGold = Math.floor(
          prev.gold.exploration * survivalMultiplier,
        );

        return {
          ...prev,
          gold: {
            baseCamp: prev.gold.baseCamp + transferredGold,
            exploration: 0,
          },
          magicStones: {
            baseCamp: {
              small:
                prev.magicStones.baseCamp.small +
                Math.floor(
                  prev.magicStones.exploration.small * survivalMultiplier,
                ),
              medium:
                prev.magicStones.baseCamp.medium +
                Math.floor(
                  prev.magicStones.exploration.medium * survivalMultiplier,
                ),
              large:
                prev.magicStones.baseCamp.large +
                Math.floor(
                  prev.magicStones.exploration.large * survivalMultiplier,
                ),
              huge:
                prev.magicStones.baseCamp.huge +
                Math.floor(
                  prev.magicStones.exploration.huge * survivalMultiplier,
                ),
            },
            exploration: { small: 0, medium: 0, large: 0, huge: 0 },
          },
        };
      });
    },
    [setResources],
  );

  /**
   * Reset exploration resources (on death)
   * Exploration resources are lost, baseCamp resources are kept
   */
  const resetExplorationResources = useCallback(() => {
    setResources((prev) => ({
      ...prev,
      gold: {
        ...prev.gold,
        exploration: 0,
      },
      magicStones: {
        ...prev.magicStones,
        exploration: { small: 0, medium: 0, large: 0, huge: 0 },
      },
    }));
  }, []);

  return (
    <ResourceContext.Provider
      value={{
        resources,
        addGold,
        spendGold,
        getTotalGold,
        addMagicStones,
        setBaseCampMagicStones,
        spendBaseCampMagicStones,
        getTotalMagicStonesValue,
        getBaseCampMagicStones,
        getExplorationMagicStones,
        useExplorationPoint,
        resetExplorationLimit,
        getExplorationLimit,
        transferExplorationToBaseCamp,
        resetExplorationResources,
      }}
    >
      {children}
    </ResourceContext.Provider>
  );
};

/**
 * Hook to use Resource context
 */
// eslint-disable-next-line react-refresh/only-export-components
export const useResources = () => {
  const context = useContext(ResourceContext);
  if (!context) {
    throw new Error("useResources must be used within ResourceProvider");
  }
  return context;
};
