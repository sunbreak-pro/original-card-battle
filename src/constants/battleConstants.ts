/**
 * Battle Constants
 *
 * Centralized battle-related constants including damage calculation,
 * buff/debuff rates, phase calculation, and critical hit configuration.
 */

// ============================================================
// Phase Calculation Constants
// ============================================================

/** Speed randomness variance percentage (±5%) */
export const VARIANCE_PERCENT = 5;

/** Mean reversion factor for speed randomness */
export const MEAN_REVERSION_FACTOR = 0.3;

/** Number of variance history samples to keep */
export const HISTORY_LENGTH = 5;

/** Speed difference threshold for consecutive phases */
export const CONSECUTIVE_PHASE_THRESHOLD = 15;

/** Additional speed diff per extra consecutive phase */
export const ADDITIONAL_PHASE_INTERVAL = 10;

/** Minimum phases to generate for UI prediction display */
export const MIN_PHASES_FOR_PREDICTION = 8;

// ============================================================
// Damage Calculation Constants
// ============================================================

/** Guard bleed-through damage multiplier (when guard absorbs all but AP=0) */
export const GUARD_BLEED_THROUGH_MULTIPLIER = 0.75;

// ============================================================
// Buff/Debuff Calculation Constants
// ============================================================

/** Curse healing reduction multiplier */
export const CURSE_HEALING_MULTIPLIER = 0.2;

/** Over-curse healing reduction multiplier */
export const OVER_CURSE_HEALING_MULTIPLIER = 0.5;

/** Fire field bonus damage multiplier */
export const FIRE_FIELD_BONUS_MULTIPLIER = 0.5;

// ============================================================
// Player Base Constants
// ============================================================

/** Base player speed before buffs/debuffs */
export const BASE_PLAYER_SPEED = 50;

/** Number of cards drawn per phase */
export const PLAYER_BASE_DRAW_COUNT = 5;

/** Guard initial value multiplier (guard = baseMaxAp × this) */
export const GUARD_INIT_MULTIPLIER = 0.5;

/** Bleed damage rate (% of maxHP) */
export const BLEED_DAMAGE_RATE = 0.05;

// ============================================================
// Encounter Constants
// ============================================================

/** Boss encounter index */
export const BOSS_ENCOUNTER_INDEX = 7;

/** Group encounter interval */
export const GROUP_ENCOUNTER_INTERVAL = 3;

/** Maximum turn order display count */
export const MAX_TURN_ORDER_DISPLAY = 4;

// ============================================================
// Sword Energy Bleed Constants
// ============================================================

/** Bleed chance at sword energy 5+ */
export const SWORD_ENERGY_BLEED_CHANCE_5 = 0.2;

/** Bleed chance at sword energy 8+ */
export const SWORD_ENERGY_BLEED_CHANCE_8 = 0.4;

/** Bleed chance at sword energy 10 (max) */
export const SWORD_ENERGY_BLEED_CHANCE_10 = 0.6;

/** Duration of bleed applied by sword energy */
export const SWORD_ENERGY_BLEED_DURATION = 3;

/** Stacks of bleed applied by sword energy */
export const SWORD_ENERGY_BLEED_STACKS = 1;
