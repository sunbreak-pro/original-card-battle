// Sanctuary facility type definitions

/**
 * Skill node categories in the sanctuary skill tree
 */
export type SkillCategory =
  | "hp" // HP/survivability boosts
  | "gold" // Gold acquisition bonuses
  | "combat" // Combat-related abilities
  | "utility" // General utility (inventory, appraisal)
  | "class" // Class-specific bonuses
  | "exploration"; // Exploration limit extensions

/**
 * Skill tree tier levels
 */
export type SkillTier = 1 | 2 | 3;

/**
 * Node effect types
 */
export type NodeEffectType =
  | "stat_boost" // Direct stat increase (HP, etc.)
  | "special_ability" // Special passive ability
  | "resource_increase" // Resource capacity increase
  | "exploration_limit" // Exploration count increase
  | "gold_multiplier" // Gold acquisition multiplier
  | "soul_multiplier"; // Soul acquisition multiplier

/**
 * Character class types for class-restricted nodes
 */
export type CharacterClass = "swordsman" | "mage" | "summoner";

/**
 * Node effect definition
 */
export interface NodeEffect {
  type: NodeEffectType;
  target: string; // e.g., 'initial_hp', 'gold_acquisition', 'exploration_limit'
  value: number; // Effect value (can be flat amount or percentage as decimal)
  description: string; // Human-readable description
}

/**
 * Skill node definition
 */
export interface SkillNode {
  id: string;
  name: string;
  description: string;
  icon: string;
  cost: number; // Soul cost to unlock
  category: SkillCategory;
  tier: SkillTier;
  prerequisites: string[]; // IDs of required nodes
  effects: NodeEffect[];
  classRestriction?: CharacterClass; // If set, only this class can unlock
  position: {
    angle: number; // Angle in degrees from center (0-360)
    radius: number; // Distance from center (1-3 for tiers)
  };
}

/**
 * Node status for UI display
 */
export type NodeStatus = "unlocked" | "available" | "locked";

/**
 * Unlock result
 */
export interface UnlockResult {
  success: boolean;
  message: string;
  remainingSouls?: number;
}

/**
 * Calculated effects from all unlocked nodes
 */
export interface SanctuaryEffects {
  initialHpBonus: number; // Flat HP bonus
  goldMultiplier: number; // Gold acquisition multiplier (1.0 = no bonus)
  soulMultiplier: number; // Soul acquisition multiplier (1.0 = no bonus)
  explorationLimitBonus: number; // Extra exploration count
  inventoryBonus: number; // Extra inventory slots
  hpRecoveryPercent: number; // HP recovery percentage on rest
  hasAppraisal: boolean; // Can see equipment details
  hasTrueAppraisal: boolean; // Can see hidden effects
  hasIndomitableWill: boolean; // Survive with 1 HP once per run
  classEnergy: {
    swordsman: number; // Extra sword energy
    mage: number; // Extra resonance
    summoner: number; // Extra summon slots
  };
}

/**
 * Default sanctuary effects (no nodes unlocked)
 */
export const DEFAULT_SANCTUARY_EFFECTS: SanctuaryEffects = {
  initialHpBonus: 0,
  goldMultiplier: 1.0,
  soulMultiplier: 1.0,
  explorationLimitBonus: 0,
  inventoryBonus: 0,
  hpRecoveryPercent: 0,
  hasAppraisal: false,
  hasTrueAppraisal: false,
  hasIndomitableWill: false,
  classEnergy: {
    swordsman: 0,
    mage: 0,
    summoner: 0,
  },
};

/**
 * Soul values for different enemy types
 */
export interface EnemySoulValues {
  normal: number;
  elite: number;
  boss: number;
  returnRoute: number; // Multiplier for return route encounters
}

/**
 * Survival multipliers based on return method
 */
export interface SurvivalMultipliers {
  earlyReturn: number; // 60%
  normalReturn: number; // 80%
  fullClear: number; // 100%
}

/**
 * Sanctuary constants
 */
export const SANCTUARY_CONSTANTS = {
  // Soul values
  SOUL_VALUES: {
    normal: 5,
    elite: 15,
    boss: 50,
    returnRouteMultiplier: 1.2,
  } as const,

  // Survival multipliers
  SURVIVAL_MULTIPLIERS: {
    earlyReturn: 0.6,
    normalReturn: 0.8,
    fullClear: 1.0,
  } as const,

  // UI constants
  UNLOCK_HOLD_DURATION: 1500, // 1.5 seconds for long press unlock
  TIER_RADIUS: {
    1: 1,
    2: 2,
    3: 3,
  } as const,
} as const;
