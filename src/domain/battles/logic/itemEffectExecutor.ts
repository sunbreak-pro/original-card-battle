// itemEffectExecutor.ts - Executes consumable item effects in battle

import type { Item } from '@/types/itemTypes';
import type { BuffDebuffMap } from '@/types/battleTypes';
import type { ItemEffectResult, ConsumableEffect } from '@/types/itemTypes';
import { getConsumableData, isUsableInBattle } from "@/constants/data/items/ConsumableItemData";
import { BUFF_EFFECTS } from "@/constants/data/battles/buffData";

/**
 * Execute a consumable item's effects in battle
 * @param item - The item being used
 * @param currentHp - Player's current HP
 * @param maxHp - Player's maximum HP
 * @param currentBuffs - Player's current buffs/debuffs
 * @param currentEnergy - Player's current card energy
 * @param maxEnergy - Player's maximum card energy
 * @returns ItemEffectResult describing what happened
 */
export function executeItemEffect(
  item: Item,
  currentHp: number,
  maxHp: number,
  currentBuffs: BuffDebuffMap,
  currentEnergy: number,
  maxEnergy: number,
): ItemEffectResult {
  // Validate item is consumable
  if (item.itemType !== 'consumable') {
    return {
      success: false,
      message: `${item.name}は消耗品ではありません。`,
    };
  }

  // Check if item is usable in battle
  if (!isUsableInBattle(item.typeId)) {
    return {
      success: false,
      message: `${item.name}は戦闘中に使用できません。`,
    };
  }

  // Get consumable data for effects
  const consumableData = getConsumableData(item.typeId);

  // If no specific data found, try to infer from item properties
  if (!consumableData) {
    // Fallback: basic healing potion behavior for unregistered items
    return executeFallbackEffect(item, currentHp, maxHp);
  }

  // Execute all effects
  const result: ItemEffectResult = {
    success: true,
    message: '',
    buffsApplied: [],
  };

  const messages: string[] = [];

  for (const effect of consumableData.effects) {
    const effectResult = executeSingleEffect(
      effect,
      currentHp,
      maxHp,
      currentBuffs,
      currentEnergy,
      maxEnergy
    );

    // Accumulate results
    if (effectResult.hpChange) {
      result.hpChange = (result.hpChange ?? 0) + effectResult.hpChange;
    }
    if (effectResult.guardChange) {
      result.guardChange = (result.guardChange ?? 0) + effectResult.guardChange;
    }
    if (effectResult.energyChange) {
      result.energyChange = (result.energyChange ?? 0) + effectResult.energyChange;
    }
    if (effectResult.cardsDrawn) {
      result.cardsDrawn = (result.cardsDrawn ?? 0) + effectResult.cardsDrawn;
    }
    if (effectResult.buffsApplied) {
      result.buffsApplied!.push(...effectResult.buffsApplied);
    }
    if (effectResult.debuffsCleared) {
      result.debuffsCleared = true;
    }
    if (effectResult.damageDealt) {
      result.damageDealt = (result.damageDealt ?? 0) + effectResult.damageDealt;
    }
    if (effectResult.skipEnemyTurn) {
      result.skipEnemyTurn = true;
    }

    if (effectResult.message) {
      messages.push(effectResult.message);
    }
  }

  result.message = `${consumableData.nameJa}を使用した！ ${messages.join(' ')}`;

  return result;
}

/**
 * Execute a single effect from a consumable item
 */
function executeSingleEffect(
  effect: ConsumableEffect,
  currentHp: number,
  maxHp: number,
  _currentBuffs: BuffDebuffMap,
  currentEnergy: number,
  maxEnergy: number
): Partial<ItemEffectResult> {
  switch (effect.type) {
    case 'heal': {
      const healAmount = Math.min(effect.value ?? 0, maxHp - currentHp);
      return {
        hpChange: healAmount,
        message: `HPが${healAmount}回復！`,
      };
    }

    case 'fullHeal': {
      const healAmount = maxHp - currentHp;
      return {
        hpChange: healAmount,
        message: `HPが全回復！`,
      };
    }

    case 'shield': {
      return {
        guardChange: effect.value ?? 0,
        message: `ガードが${effect.value}上昇！`,
      };
    }

    case 'energy': {
      const energyGain = Math.min(effect.value ?? 0, maxEnergy - currentEnergy);
      return {
        energyChange: energyGain,
        message: `エネルギーが${energyGain}回復！`,
      };
    }

    case 'buff': {
      if (!effect.buffType) {
        return { message: 'バフの種類が指定されていません。' };
      }
      const buffDef = BUFF_EFFECTS[effect.buffType];
      return {
        buffsApplied: [{
          type: effect.buffType,
          duration: effect.duration ?? 3,
        }],
        message: `${buffDef?.nameJa ?? effect.buffType}を獲得！`,
      };
    }

    case 'debuffClear': {
      return {
        debuffsCleared: true,
        message: 'すべてのデバフを解除！',
      };
    }

    case 'damage': {
      return {
        damageDealt: effect.value ?? 0,
        message: effect.targetAll
          ? `全ての敵に${effect.value}ダメージ！`
          : `敵に${effect.value}ダメージ！`,
      };
    }

    case 'draw': {
      return {
        cardsDrawn: effect.value ?? 1,
        message: `カードを${effect.value}枚ドロー！`,
      };
    }

    case 'skipEnemyTurn': {
      return {
        skipEnemyTurn: true,
        message: '敵の行動をスキップ！',
      };
    }

    default:
      return { message: '不明な効果。' };
  }
}

/**
 * Fallback effect execution for unregistered consumable items
 * Tries to infer basic behavior from item type/name
 */
function executeFallbackEffect(
  item: Item,
  currentHp: number,
  maxHp: number
): ItemEffectResult {
  const itemName = item.name.toLowerCase();
  const typeId = item.typeId.toLowerCase();

  // Basic healing inference
  if (itemName.includes('potion') || itemName.includes('heal') ||
    typeId.includes('potion') || typeId.includes('heal')) {
    // Lesser = 15, normal = 30, greater = 60
    let healAmount = 30;
    if (itemName.includes('lesser') || typeId.includes('lesser')) {
      healAmount = 15;
    } else if (itemName.includes('greater') || typeId.includes('greater')) {
      healAmount = 60;
    }

    const actualHeal = Math.min(healAmount, maxHp - currentHp);
    return {
      success: true,
      hpChange: actualHeal,
      message: `${item.name}を使用！ HPが${actualHeal}回復！`,
    };
  }

  // Unknown item - generic success
  return {
    success: true,
    message: `${item.name}を使用した！`,
  };
}
