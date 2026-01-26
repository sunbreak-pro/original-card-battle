// ConsumableEffectTypes.ts - Types for consumable item effects

import type { BuffDebuffType } from "../../battles/type/baffType";
import type { UsableContext } from "./ItemTypes";

/**
 * Types of effects that consumable items can have
 */
export type ConsumableEffectType =
  | 'heal'           // Restore HP by fixed amount
  | 'fullHeal'       // Restore HP to maximum
  | 'buff'           // Apply a buff to self
  | 'debuffClear'    // Remove all debuffs
  | 'damage'         // Deal damage to enemy
  | 'shield'         // Add guard/shield points
  | 'energy'         // Restore card energy
  | 'draw'           // Draw additional cards
  | 'skipEnemyTurn'; // Skip the next enemy turn

/**
 * Individual effect that a consumable item applies
 */
export interface ConsumableEffect {
  type: ConsumableEffectType;
  value?: number;              // Amount for heal/damage/shield/energy/draw
  buffType?: BuffDebuffType;   // Which buff to apply (for 'buff' type)
  duration?: number;           // Duration of buff in turns
  targetAll?: boolean;         // Whether effect targets all enemies (for damage)
}

/**
 * Complete data definition for a consumable item
 */
export interface ConsumableItemData {
  typeId: string;
  name: string;
  nameJa: string;
  description: string;
  descriptionJa: string;
  effects: ConsumableEffect[];
  usableContext: UsableContext;
  icon: string;                // Emoji or icon identifier
  rarity: 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary';
  sellPrice: number;
  maxStack: number;
}

/**
 * Result of executing an item effect
 */
export interface ItemEffectResult {
  success: boolean;
  hpChange?: number;           // Positive = heal, negative = damage taken
  guardChange?: number;        // Guard points added
  energyChange?: number;       // Energy restored
  cardsDrawn?: number;         // Cards drawn
  buffsApplied?: Array<{ type: BuffDebuffType; duration: number }>;
  debuffsCleared?: boolean;
  damageDealt?: number;        // Damage dealt to enemies
  skipEnemyTurn?: boolean;
  message: string;             // Description of what happened
}
