/**
 * Battle Phase State Management Hook
 * Manages phase queue, phase transitions, and speed calculations.
 */

import { useState, useCallback } from "react";
import type { PhaseQueue, PhaseActor, SpeedRandomState } from '@/types/battleTypes';
import type { BuffDebuffMap } from '@/types/battleTypes';
import type { EnemyDefinition } from '@/types/characterTypes';
import {
    calculatePlayerSpeed,
    calculateEnemySpeed,
    applySpeedRandomness,
    generatePhaseQueue,
    createInitialSpeedRandomState,
} from "../calculators/speedCalculation";

export interface PhaseState {
    phaseQueue: PhaseQueue | null;
    currentPhaseIndex: number;
    phaseCount: number;
    isPlayerPhase: boolean;
    isEnemyPhase: boolean;
    playerSpeed: number;
    enemySpeed: number;
}

export interface UseBattlePhaseReturn {
    // State
    phaseQueue: PhaseQueue | null;
    currentPhaseIndex: number;
    phaseCount: number;
    isPlayerPhase: boolean;
    isEnemyPhase: boolean;
    playerSpeed: number;
    enemySpeed: number;

    // Actions
    generatePhaseQueueFromSpeeds: (
        playerBuffs: BuffDebuffMap,
        enemy: EnemyDefinition | null,
        enemyBuffs: BuffDebuffMap
    ) => PhaseQueue;
    advancePhaseIndex: () => void;
    setPhaseIndex: (index: number) => void;
    setPlayerPhaseActive: () => void;
    setEnemyPhaseActive: () => void;
    clearActivePhase: () => void;
    incrementPhaseCount: () => void;
    resetPhaseState: () => void;
    isRoundComplete: () => boolean;
    getCurrentActor: () => PhaseActor | null;
}

export function useBattlePhase(initialPlayerSpeed: number): UseBattlePhaseReturn {
    // Phase queue state
    const [phaseQueue, setPhaseQueue] = useState<PhaseQueue | null>(null);
    const [currentPhaseIndex, setCurrentPhaseIndex] = useState(0);

    // Phase count
    const [phaseCount, setPhaseCount] = useState(1);

    // Active phase flags
    const [isPlayerPhase, setIsPlayerPhase] = useState(false);
    const [isEnemyPhase, setIsEnemyPhase] = useState(false);

    // Speed state
    const [playerSpeed, setPlayerSpeed] = useState(initialPlayerSpeed);
    const [enemySpeed, setEnemySpeed] = useState(0);

    // Speed random states for mean-reversion
    const [playerRandomState, setPlayerRandomState] = useState<SpeedRandomState>(
        createInitialSpeedRandomState()
    );
    const [enemyRandomState, setEnemyRandomState] = useState<SpeedRandomState>(
        createInitialSpeedRandomState()
    );

    /**
     * Calculate speeds and generate phase queue
     */
    const generatePhaseQueueFromSpeeds = useCallback((
        playerBuffs: BuffDebuffMap,
        enemy: EnemyDefinition | null,
        enemyBuffs: BuffDebuffMap
    ): PhaseQueue => {
        // Calculate base speeds
        const basePlayerSpeed = calculatePlayerSpeed(playerBuffs);
        const baseEnemySpeed = enemy ? calculateEnemySpeed(enemy, enemyBuffs) : 0;

        // Apply randomness with mean-reversion
        const playerResult = applySpeedRandomness(basePlayerSpeed, playerRandomState);
        const enemyResult = applySpeedRandomness(baseEnemySpeed, enemyRandomState);

        // Update random states
        setPlayerRandomState(playerResult.newRandomState);
        setEnemyRandomState(enemyResult.newRandomState);

        // Update speed values
        setPlayerSpeed(playerResult.speed);
        setEnemySpeed(enemyResult.speed);

        // Generate phase queue
        const result = generatePhaseQueue(playerResult.speed, enemyResult.speed);

        // Update queue state
        setPhaseQueue(result.queue);
        setCurrentPhaseIndex(0);

        return result.queue;
    }, [playerRandomState, enemyRandomState]);

    /**
     * Advance to next phase index
     */
    const advancePhaseIndex = useCallback(() => {
        setCurrentPhaseIndex(prev => prev + 1);
    }, []);

    /**
     * Set phase index to specific value
     */
    const setPhaseIndex = useCallback((index: number) => {
        setCurrentPhaseIndex(index);
    }, []);

    /**
     * Set player phase as active
     */
    const setPlayerPhaseActive = useCallback(() => {
        setIsPlayerPhase(true);
        setIsEnemyPhase(false);
    }, []);

    /**
     * Set enemy phase as active
     */
    const setEnemyPhaseActive = useCallback(() => {
        setIsPlayerPhase(false);
        setIsEnemyPhase(true);
    }, []);

    /**
     * Clear active phase (transition state)
     */
    const clearActivePhase = useCallback(() => {
        setIsPlayerPhase(false);
        setIsEnemyPhase(false);
    }, []);

    /**
     * Increment phase count
     */
    const incrementPhaseCount = useCallback(() => {
        setPhaseCount(c => c + 1);
    }, []);

    /**
     * Reset all phase state for new battle
     */
    const resetPhaseState = useCallback(() => {
        setPhaseQueue(null);
        setCurrentPhaseIndex(0);
        setPhaseCount(1);
        setIsPlayerPhase(false);
        setIsEnemyPhase(false);
        setPlayerRandomState(createInitialSpeedRandomState());
        setEnemyRandomState(createInitialSpeedRandomState());
    }, []);

    /**
     * Check if current round is complete
     */
    const isRoundComplete = useCallback((): boolean => {
        if (!phaseQueue) return true;
        return currentPhaseIndex >= phaseQueue.phases.length;
    }, [phaseQueue, currentPhaseIndex]);

    /**
     * Get current actor from phase queue
     */
    const getCurrentActor = useCallback((): PhaseActor | null => {
        if (!phaseQueue || currentPhaseIndex >= phaseQueue.phases.length) {
            return null;
        }
        return phaseQueue.phases[currentPhaseIndex];
    }, [phaseQueue, currentPhaseIndex]);

    return {
        // State
        phaseQueue,
        currentPhaseIndex,
        phaseCount,
        isPlayerPhase,
        isEnemyPhase,
        playerSpeed,
        enemySpeed,

        // Actions
        generatePhaseQueueFromSpeeds,
        advancePhaseIndex,
        setPhaseIndex,
        setPlayerPhaseActive,
        setEnemyPhaseActive,
        clearActivePhase,
        incrementPhaseCount,
        resetPhaseState,
        isRoundComplete,
        getCurrentActor,
    };
}
