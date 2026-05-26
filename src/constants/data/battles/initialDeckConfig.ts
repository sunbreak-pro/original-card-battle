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
        // Attack cards (6 total) - consume sword energy
        sw_001: 2,  // 迅雷斬 (Swift Slash) - cost 0, consume 0, dmg 8
        sw_007: 2,  // 斬撃 (Slash) - cost 1, consume 1, dmg 14
        sw_009: 1,  // 回転斬り (Spin Slash) - cost 1, consume 1, dmg 12 AoE
        sw_003: 1,  // 連撃 (Combo Strike) - cost 2, consume 2, 8×3
        // Skill cards (6 total) - gain sword energy
        sw_013: 2,  // 剣気集中 (Sword Energy Focus) - +5 energy, draw 1
        sw_014: 2,  // 瞑想 (Meditation) - +4 energy, heal 10
        sw_016: 2,  // 気合 (Fighting Spirit) - +3 energy, +1 energy point
        // Guard cards (3 total) - gain sword energy
        sw_037: 1,  // 剣気円盾 (Sword Energy Shield) - +2 energy, guard = energy×8
        sw_042: 2,  // 堅守 (Stalwart Defense) - +3 energy, guard +20
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
