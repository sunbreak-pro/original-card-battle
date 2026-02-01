import { createRef } from "react";
import type { EnemyBattleState, EnemyDefinition } from '@/types/characterTypes';
import { generateEnemyInstanceId } from '../../characters/enemy/logic/enemyUtils';
import { createEmptyBuffDebuffMap } from '../../characters/characterUtils';
import { GUARD_INIT_MULTIPLIER } from "../../../constants";

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
        guard: definition.startingGuard ? Math.floor(definition.baseMaxAp * GUARD_INIT_MULTIPLIER) : 0,
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
