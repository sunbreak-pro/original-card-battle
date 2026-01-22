import type { BuffDebuffMap } from "../../battles/type/baffType";

// Re-export CharacterClass from baseTypes for backward compatibility
export { type CharacterClass, type CardCharacterClass } from "./baseTypes";

/**
 * @deprecated Use BattleStats from baseTypes.ts instead.
 * This interface is kept for backward compatibility.
 */
export interface Character {
    hp: number;
    maxHp: number;
    ap: number;
    maxAp: number;
    guard: number;
    startingGuard?: boolean;
    speed: number;
    buffDebuffs?: BuffDebuffMap;
    imagePath?: string;
}