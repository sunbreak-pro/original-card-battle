/**
 * Phase Calculation Module
 * Handles phase queue generation and speed randomness for the phase-based battle system.
 */
import type { PhaseActor, SpeedRandomState, PhaseCalculationResult } from "../type/phaseType";

const VARIANCE_PERCENT = 5;
const MEAN_REVERSION_FACTOR = 0.3;
const HISTORY_LENGTH = 5;
const CONSECUTIVE_PHASE_THRESHOLD = 15; // Speed diff >= 15 for 2 phases
const ADDITIONAL_PHASE_INTERVAL = 10; // +1 phase per 10 additional diff

/**
 * Apply speed randomness with mean-reversion
 * Prevents streaky outcomes by pulling variance toward the opposite of history mean
 */
export function applySpeedRandomness(
  baseSpeed: number,
  randomState: SpeedRandomState
): { speed: number; newRandomState: SpeedRandomState } {
  // Calculate mean of variance history
  const historyMean =
    randomState.varianceHistory.length > 0
      ? randomState.varianceHistory.reduce((a, b) => a + b, 0) /
      randomState.varianceHistory.length
      : 0;

  // Generate raw random variance: -5% to +5%
  const rawVariance = (Math.random() - 0.5) * 2 * VARIANCE_PERCENT;

  // Apply mean reversion: pull toward opposite of history mean
  const meanReversionAdjustment = -historyMean * MEAN_REVERSION_FACTOR;
  const finalVariance = rawVariance + meanReversionAdjustment;

  // Clamp to ±5%
  const clampedVariance = Math.max(
    -VARIANCE_PERCENT,
    Math.min(VARIANCE_PERCENT, finalVariance)
  );

  // Apply variance to speed
  const adjustedSpeed = Math.round(baseSpeed * (1 + clampedVariance / 100));

  // Update history (keep last N samples)
  const newHistory = [
    ...randomState.varianceHistory,
    clampedVariance,
  ].slice(-HISTORY_LENGTH);

  return {
    speed: Math.max(0, adjustedSpeed),
    newRandomState: {
      varianceHistory: newHistory,
    },
  };
}

/**
 * Calculate number of consecutive phases based on speed difference
 *
 * Rules:
 * - diff < 15: 1 phase each (alternating)
 * - diff >= 15: 2 consecutive phases
 * - diff >= 25: 3 consecutive phases
 * - Each additional +10 diff adds +1 phase
 */
export function calculateConsecutivePhases(speedDiff: number): number {
  if (speedDiff < CONSECUTIVE_PHASE_THRESHOLD) {
    return 1;
  }
  return (
    2 + Math.floor((speedDiff - CONSECUTIVE_PHASE_THRESHOLD) / ADDITIONAL_PHASE_INTERVAL)
  );
}

// Minimum phases to generate for UI prediction display
const MIN_PHASES_FOR_PREDICTION = 8;

/**
 * Generate a single round pattern based on speed difference
 */
function generateRoundPattern(
  fasterActor: PhaseActor,
  slowerActor: PhaseActor,
  consecutivePhases: number,
  speedDiff: number
): PhaseActor[] {
  const pattern: PhaseActor[] = [];

  if (speedDiff < CONSECUTIVE_PHASE_THRESHOLD) {
    // Alternating phases: faster first, then slower
    pattern.push(fasterActor, slowerActor);
  } else {
    // Faster gets consecutive phases, then slower gets 1
    for (let i = 0; i < consecutivePhases; i++) {
      pattern.push(fasterActor);
    }
    pattern.push(slowerActor);
  }

  return pattern;
}

/**
 * Generate the phase queue for battle with prediction
 *
 * Generates at least MIN_PHASES_FOR_PREDICTION phases by repeating the round pattern.
 * This allows UI to show 4+ phases ahead for turn order prediction.
 *
 * Examples:
 * - Player:50, Enemy:50 (diff=0) → [P, E, P, E, P, E, P, E]
 * - Player:65, Enemy:50 (diff=15) → [P, P, E, P, P, E, P, P, E]
 * - Player:30, Enemy:60 (diff=30) → [E, E, E, P, E, E, E, P]
 */
export function generatePhaseQueue(
  playerSpeed: number,
  enemySpeed: number
): PhaseCalculationResult {
  const speedDiff = Math.abs(playerSpeed - enemySpeed);
  const fasterActor: PhaseActor =
    playerSpeed >= enemySpeed ? "player" : "enemy";
  const slowerActor: PhaseActor = fasterActor === "player" ? "enemy" : "player";

  const consecutivePhases = calculateConsecutivePhases(speedDiff);

  // Generate single round pattern
  const roundPattern = generateRoundPattern(
    fasterActor,
    slowerActor,
    consecutivePhases,
    speedDiff
  );

  // Repeat pattern to reach minimum phases for prediction
  const phases: PhaseActor[] = [];
  while (phases.length < MIN_PHASES_FOR_PREDICTION) {
    phases.push(...roundPattern);
  }

  return {
    queue: {
      phases,
      currentIndex: 0,
    },
    playerSpeed,
    enemySpeed,
    speedDifference: speedDiff,
    consecutivePhases,
    fasterActor,
  };
}

/**
 * Create initial speed random state
 */
export function createInitialSpeedRandomState(): SpeedRandomState {
  return {
    varianceHistory: [],
  };
}
