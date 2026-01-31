// Sanctuary facility business logic

import type {
  SkillNode,
  NodeStatus,
  UnlockResult,
  SanctuaryEffects,
  SanctuaryProgress,
} from '@/types/campTypes';
import type { CharacterClass, ElementType } from '@/types/characterTypes';
import { DEFAULT_SANCTUARY_EFFECTS } from '@/constants/campConstants';
import { SKILL_TREE_NODES, getNodeById } from "@/constants/data/camps/SanctuaryData";

/**
 * Determine the status of a skill node
 */
export function getNodeStatus(
  node: SkillNode,
  progress: SanctuaryProgress,
  playerClass?: CharacterClass
): NodeStatus {
  // Check if already unlocked
  if (progress.unlockedNodes.includes(node.id)) {
    return "unlocked";
  }

  // Check class restriction
  if (node.classRestriction && node.classRestriction !== playerClass) {
    return "locked";
  }

  // Check if all prerequisites are met
  const prerequisitesMet = node.prerequisites.every((prereqId) =>
    progress.unlockedNodes.includes(prereqId)
  );

  if (prerequisitesMet) {
    return "available";
  }

  return "locked";
}

/**
 * Check if a node can be unlocked
 */
export function canUnlockNode(
  node: SkillNode,
  progress: SanctuaryProgress,
  playerClass?: CharacterClass
): { canUnlock: boolean; reason?: string } {
  // Check if already unlocked
  if (progress.unlockedNodes.includes(node.id)) {
    return { canUnlock: false, reason: "Already unlocked" };
  }

  // Check class restriction
  if (node.classRestriction && node.classRestriction !== playerClass) {
    return {
      canUnlock: false,
      reason: `Requires ${node.classRestriction} class`,
    };
  }

  // Check prerequisites
  const prerequisitesMet = node.prerequisites.every((prereqId) =>
    progress.unlockedNodes.includes(prereqId)
  );
  if (!prerequisitesMet) {
    const missingPrereqs = node.prerequisites.filter(
      (prereqId) => !progress.unlockedNodes.includes(prereqId)
    );
    const missingNames = missingPrereqs
      .map((id) => getNodeById(id)?.name || id)
      .join(", ");
    return { canUnlock: false, reason: `Requires: ${missingNames}` };
  }

  // Check soul cost
  if (progress.totalSouls < node.cost) {
    return {
      canUnlock: false,
      reason: `Insufficient souls (need ${node.cost}, have ${progress.totalSouls})`,
    };
  }

  return { canUnlock: true };
}

/**
 * Unlock a skill node
 * Returns a new SanctuaryProgress object (immutable update)
 */
export function unlockNode(
  node: SkillNode,
  progress: SanctuaryProgress,
  playerClass?: CharacterClass
): UnlockResult & { newProgress?: SanctuaryProgress } {
  const { canUnlock, reason } = canUnlockNode(node, progress, playerClass);

  if (!canUnlock) {
    return {
      success: false,
      message: reason || "Cannot unlock this node",
    };
  }

  const newProgress: SanctuaryProgress = {
    ...progress,
    totalSouls: progress.totalSouls - node.cost,
    unlockedNodes: [...progress.unlockedNodes, node.id],
  };

  return {
    success: true,
    message: `Unlocked ${node.name}!`,
    remainingSouls: newProgress.totalSouls,
    newProgress,
  };
}

/**
 * Calculate total effects from all unlocked nodes
 */
export function calculateTotalEffects(
  progress: SanctuaryProgress
): SanctuaryEffects {
  const effects: SanctuaryEffects = {
    ...DEFAULT_SANCTUARY_EFFECTS,
    enhancedElements: new Set<ElementType>(),
  };

  for (const nodeId of progress.unlockedNodes) {
    const node = getNodeById(nodeId);
    if (!node) continue;

    for (const effect of node.effects) {
      switch (effect.type) {
        case "stat_boost":
          switch (effect.target) {
            case "initial_hp":
              effects.initialHpBonus += effect.value;
              break;
            case "hp_recovery":
              effects.hpRecoveryPercent += effect.value;
              break;
            case "sword_energy":
              effects.classEnergy.swordsman += effect.value;
              break;
            case "resonance":
              effects.classEnergy.mage += effect.value;
              break;
            case "summon_slot":
              effects.classEnergy.summoner += effect.value;
              break;
          }
          break;

        case "gold_multiplier":
          effects.goldMultiplier += effect.value;
          break;

        case "soul_multiplier":
          effects.soulMultiplier += effect.value;
          break;

        case "exploration_limit":
          effects.explorationLimitBonus += effect.value;
          break;

        case "resource_increase":
          if (effect.target === "inventory_capacity") {
            effects.inventoryBonus += effect.value;
          }
          break;

        case "special_ability":
          switch (effect.target) {
            case "appraisal":
            case "basic_appraisal":
              effects.hasAppraisal = true;
              break;
            case "true_appraisal":
              effects.hasTrueAppraisal = true;
              effects.hasAppraisal = true; // True appraisal includes basic
              break;
            case "death_defiance":
              effects.hasIndomitableWill = true;
              break;
          }
          break;

        case "element_enhancement":
          effects.enhancedElements.add(effect.target as ElementType);
          break;
      }
    }
  }

  return effects;
}

/**
 * Get all available nodes that can be unlocked
 */
export function getAvailableNodes(
  progress: SanctuaryProgress,
  playerClass?: CharacterClass
): SkillNode[] {
  return SKILL_TREE_NODES.filter(
    (node) => getNodeStatus(node, progress, playerClass) === "available"
  );
}

/**
 * Get all unlocked nodes
 */
export function getUnlockedNodes(progress: SanctuaryProgress): SkillNode[] {
  return progress.unlockedNodes
    .map((id) => getNodeById(id))
    .filter((node): node is SkillNode => node !== undefined);
}

/**
 * Get nodes filtered by player class
 * Returns nodes that either have no class restriction or match the player's class
 */
export function getAccessibleNodes(playerClass?: CharacterClass): SkillNode[] {
  return SKILL_TREE_NODES.filter(
    (node) => !node.classRestriction || node.classRestriction === playerClass
  );
}

/**
 * Calculate total souls spent
 */
export function getTotalSoulsSpent(progress: SanctuaryProgress): number {
  return progress.unlockedNodes.reduce((total, nodeId) => {
    const node = getNodeById(nodeId);
    return total + (node?.cost || 0);
  }, 0);
}

/**
 * Get progress statistics
 */
export function getProgressStats(
  progress: SanctuaryProgress,
  playerClass?: CharacterClass
): {
  totalNodes: number;
  unlockedCount: number;
  availableCount: number;
  lockedCount: number;
  soulsSpent: number;
  completionPercent: number;
} {
  const accessibleNodes = getAccessibleNodes(playerClass);
  const totalNodes = accessibleNodes.length;

  let unlockedCount = 0;
  let availableCount = 0;
  let lockedCount = 0;

  for (const node of accessibleNodes) {
    const status = getNodeStatus(node, progress, playerClass);
    switch (status) {
      case "unlocked":
        unlockedCount++;
        break;
      case "available":
        availableCount++;
        break;
      case "locked":
        lockedCount++;
        break;
    }
  }

  const soulsSpent = getTotalSoulsSpent(progress);
  const completionPercent =
    totalNodes > 0 ? Math.round((unlockedCount / totalNodes) * 100) : 0;

  return {
    totalNodes,
    unlockedCount,
    availableCount,
    lockedCount,
    soulsSpent,
    completionPercent,
  };
}

/**
 * Get boosted initial stats based on sanctuary effects
 */
export function getBoostedInitialStats(
  baseMaxHp: number,
  progress: SanctuaryProgress
): {
  maxHp: number;
  explorationLimit: number;
  inventoryBonus: number;
} {
  const effects = calculateTotalEffects(progress);

  return {
    maxHp: baseMaxHp + effects.initialHpBonus,
    explorationLimit: 10 + effects.explorationLimitBonus, // Base 10 + bonuses
    inventoryBonus: effects.inventoryBonus,
  };
}

/**
 * Apply gold multiplier from sanctuary effects
 */
export function applyGoldMultiplier(
  baseGold: number,
  progress: SanctuaryProgress
): number {
  const effects = calculateTotalEffects(progress);
  return Math.floor(baseGold * effects.goldMultiplier);
}

/**
 * Apply soul multiplier from sanctuary effects
 */
export function applySoulMultiplier(
  baseSouls: number,
  progress: SanctuaryProgress
): number {
  const effects = calculateTotalEffects(progress);
  return Math.floor(baseSouls * effects.soulMultiplier);
}
