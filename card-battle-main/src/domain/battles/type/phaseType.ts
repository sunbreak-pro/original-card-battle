export type PhaseActor = "player" | "enemy";

export interface PhaseQueue {
    phases: PhaseActor[];
    currentIndex: number;
}

export interface SpeedRandomState {
    varianceHistory: number[];
}

export interface PhaseCalculationResult {
    queue: PhaseQueue;
    playerSpeed: number;
    enemySpeed: number;
    speedDifference: number;
    consecutivePhases: number;
    fasterActor: PhaseActor;
}