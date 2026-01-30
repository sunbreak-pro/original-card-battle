/**
 * Initial deck configuration for battle start
 * Maps card type IDs to their initial counts
 */

import type { CharacterClass } from "@/types/characterTypes";

/**
 * Initial deck configuration by class
 * Each class has a 15-card starter deck optimized for their playstyle
 */
export const INITIAL_DECK_BY_CLASS: Record<CharacterClass, Record<string, number>> = {
    swordsman: {
        sw_001: 3,  // 迅雷斬 (Swift Slash) - 0 cost basic attack
        sw_003: 2,  // 連撃 (Combo Strike) - 3-hit attack
        sw_007: 2,  // 斬りつける (Slash) - basic damage
        sw_013: 2,  // 剣気集中 (Sword Energy Focus) - energy gain
        sw_037: 2,  // 剣気円盾 (Sword Energy Barrier) - defense
        sw_039: 2,  // 不屈の精神 (Unyielding Spirit) - heal + guard
        sw_014: 2,  // 瞑想 (Meditation) - heal + energy
    },
    mage: {
        mg_001: 3,  // 火球 - Fire basic attack
        mg_008: 2,  // 炎の矢 - 0-cost fire attack
        mg_009: 2,  // 氷結 - Ice basic attack
        mg_017: 2,  // 雷撃 - Lightning basic attack
        mg_033: 2,  // 光の槍 - Light basic attack
        mg_007: 2,  // 炎の壁 - Fire guard
        mg_037: 2,  // 癒しの光 - Light heal
    },
    summoner: {
        sm_001: 2,  // 火精霊召喚 - Fire spirit summon
        sm_002: 2,  // 水精霊召喚 - Water spirit summon
        sm_003: 2,  // 風精霊召喚 - Wind spirit summon
        sm_013: 2,  // 召喚獣強化 - Summon enhancement
        sm_025: 2,  // 精霊弾 - Spirit shot
        sm_026: 2,  // 使い魔の一撃 - Familiar strike
        sm_033: 1,  // 生命の供物 - Life offering (sacrifice/heal)
        sm_034: 2,  // エーテル還元 - Ether return (sacrifice/AP)
    },
};

/**
 * Get initial deck counts for a specific class
 * @param classType - The character class
 * @returns Deck configuration mapping card type IDs to counts
 */
export function getInitialDeckCounts(classType: CharacterClass): Record<string, number> {
    return INITIAL_DECK_BY_CLASS[classType] ?? INITIAL_DECK_BY_CLASS.swordsman;
}

/**
 * Legacy alias for backward compatibility
 * Uses swordsman deck as default
 * @deprecated Use getInitialDeckCounts(classType) instead
 */
export const INITIAL_DECK_COUNTS: Record<string, number> = INITIAL_DECK_BY_CLASS.swordsman;
