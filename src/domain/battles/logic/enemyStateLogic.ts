import { createRef } from "react";
import type { EnemyBattleState, EnemyDefinition } from "../../characters/type/enemyType";
import { generateEnemyInstanceId } from "../../characters/type/enemyType";
import { createEmptyBuffDebuffMap } from "../../characters/type/baseTypes";

/**
 * Create initial battle state for an enemy from EnemyDefinition
 *
 * This is the preferred method for creating EnemyBattleState.
 */
export function createEnemyStateFromDefinition(
    definition: EnemyDefinition,
    ref?: React.RefObject<HTMLDivElement | null>
): EnemyBattleState {
    const buffDebuffs = createEmptyBuffDebuffMap();

    return {
        // New architecture fields
        instanceId: generateEnemyInstanceId(definition.id),
        definitionId: definition.id,
        definition,

        // BattleStats base
        hp: definition.baseMaxHp,
        maxHp: definition.baseMaxHp,
        ap: definition.baseMaxAp,
        maxAp: definition.baseMaxAp,
        guard: definition.startingGuard ? Math.floor(definition.baseMaxAp * 0.5) : 0,
        speed: definition.baseSpeed,
        buffDebuffs,

        // Enemy-specific battle state
        energy: definition.actEnergy,
        phaseCount: 0,
        turnCount: 0,

        // DOM reference
        ref: ref ?? createRef<HTMLDivElement>(),
    };
}
