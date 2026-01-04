# Battle System Ver 5.0 - Phase-Based Speed System

## Overview

This document outlines the implementation plan for converting the turn-based battle system to a **phase-based system** where speed advantage grants consecutive phases.

**Priority**: High
**Dependencies**: Existing battle logic, speed calculation, UI components
**Status**: Planning

---

## System Design

### Core Concept

Replace the simple alternating turn system (`player → enemy → player`) with a dynamic phase queue where speed determines action order and consecutive phases.

### Phase Allocation Rules

| Speed Difference | Phases for Faster Actor   | Example                                |
| ---------------- | ------------------------- | -------------------------------------- |
| diff < 15        | 1 (alternating)           | Player:50, Enemy:50 → [P, E, P, E...]  |
| diff >= 15       | 2 consecutive             | Player:65, Enemy:50 → [P, P, E]        |
| diff >= 25       | 3 consecutive             | Player:75, Enemy:50 → [P, P, P, E]     |
| diff >= 35       | 4 consecutive             | Player:85, Enemy:50 → [P, P, P, P, E]  |
| No limit         | +1 per additional 10 diff | Formula: `2 + floor((diff - 15) / 10)` |

### Speed Randomness (Mean-Reversion)

- Apply **±5% variance** to speed calculations
- Use **mean-reversion** to prevent streaky outcomes
- Track variance history (last 5 samples)
- Pull new variance toward opposite of history mean

### Energy System

- **Player energy**: Recover to MAX at start of each player phase
- **Enemy energy**: Recover to `actEnergy` at start of each enemy phase
- Energy no longer tied to turns, but to phases

### Removed Systems

- **SpeedBonus** (`先制`, `電光石火`): Abolished
  - Consecutive phases ARE the new speed advantage
  - No attack/crit bonuses from speed difference

---

## Files to Modify

### 1. NEW: `src/battles/logic/phaseCalculation.ts`

Core phase queue generation and speed randomness logic.

**Types:**

```typescript
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
```

**Functions:**

```typescript
// Apply ±5% randomness with mean-reversion
export function applySpeedRandomness(
  baseSpeed: number,
  randomState: SpeedRandomState
): { speed: number; newRandomState: SpeedRandomState };

// Calculate consecutive phases from speed difference
export function calculateConsecutivePhases(speedDiff: number): number;

// Generate full phase queue for a round
export function generatePhaseQueue(
  playerSpeed: number,
  enemySpeed: number
): PhaseCalculationResult;
```

**Algorithm - calculateConsecutivePhases:**

```typescript
export function calculateConsecutivePhases(speedDiff: number): number {
  if (speedDiff < 15) return 1;
  return 2 + Math.floor((speedDiff - 15) / 10);
}
```

**Algorithm - generatePhaseQueue:**

```typescript
export function generatePhaseQueue(
  playerSpeed: number,
  enemySpeed: number
): PhaseCalculationResult {
  const speedDiff = Math.abs(playerSpeed - enemySpeed);
  const fasterActor: PhaseActor =
    playerSpeed >= enemySpeed ? "player" : "enemy";
  const slowerActor: PhaseActor = fasterActor === "player" ? "enemy" : "player";

  const consecutivePhases = calculateConsecutivePhases(speedDiff);
  const phases: PhaseActor[] = [];

  if (speedDiff < 15) {
    // Alternating phases
    phases.push(fasterActor, slowerActor);
  } else {
    // Faster gets consecutive phases, then slower gets 1
    for (let i = 0; i < consecutivePhases; i++) {
      phases.push(fasterActor);
    }
    phases.push(slowerActor);
  }

  return {
    queue: { phases, currentIndex: 0 },
    playerSpeed,
    enemySpeed,
    speedDifference: speedDiff,
    consecutivePhases,
    fasterActor,
  };
}
```

---

### 2. MODIFY: `src/battles/logic/speedCalculation.ts`

**Remove:**

- `SpeedBonus` interface (lines 4-8)
- `calculateSpeedBonus()` function (lines 68-88)

**Keep:**

- `calculatePlayerSpeed()` - unchanged
- `calculateEnemySpeed()` - unchanged
- `determineTurnOrder()` - keep for backwards compatibility

**Add:**

```typescript
// Re-export from phaseCalculation
export {
  generatePhaseQueue,
  applySpeedRandomness,
  calculateConsecutivePhases,
  type PhaseQueue,
  type PhaseActor,
  type SpeedRandomState,
  type PhaseCalculationResult,
} from "./phaseCalculation";
```

---

### 3. MODIFY: `src/battles/logic/useBattleLogic.ts`

**State Changes:**

```typescript
// REMOVE
const [speedBonusPlayer, setSpeedBonusPlayer] = useState<SpeedBonus | null>(
  null
);
const [speedBonusEnemy, setSpeedBonusEnemy] = useState<SpeedBonus | null>(null);
const [turn, setTurn] = useState(1); // rename to phaseCount
const [turnPhase, setTurnPhase] = useState<"player" | "enemy" | "transition">(
  "player"
); // split into two booleans

// ADD
const [phaseQueue, setPhaseQueue] = useState<PhaseQueue | null>(null);
const [currentPhaseIndex, setCurrentPhaseIndex] = useState(0);
const [phaseCount, setPhaseCount] = useState(1); // renamed from turn
const [isPlayerPhase, setIsPlayerPhase] = useState(false); // replaces turnPhase
const [isEnemyPhase, setIsEnemyPhase] = useState(false); // replaces turnPhase
const [playerRandomState, setPlayerRandomState] = useState<SpeedRandomState>({
  varianceHistory: [],
});
const [enemyRandomState, setEnemyRandomState] = useState<SpeedRandomState>({
  varianceHistory: [],
});
```

**State Design Rationale:**

| Old State   | New State                        | Reason                                                     |
| ----------- | -------------------------------- | ---------------------------------------------------------- |
| `turn`      | `phaseCount`                     | More accurate - counts phases, not turns                   |
| `turnPhase` | `isPlayerPhase` + `isEnemyPhase` | Explicit boolean flags improve type safety and readability |

**Phase State Transitions:**

```typescript
// Player phase active
setIsPlayerPhase(true);
setIsEnemyPhase(false);

// Enemy phase active
setIsPlayerPhase(false);
setIsEnemyPhase(true);

// Transition (neither active)
setIsPlayerPhase(false);
setIsEnemyPhase(false);
```

**New Battle Flow:**

```
Battle Start
    │
    ▼
┌─────────────────────────────────┐
│      startBattleRound()         │
│  1. Calculate speeds + random   │
│  2. Generate phase queue        │
│  3. Call executeNextPhase(0)    │
└──────────────┬──────────────────┘
               │
               ▼
┌─────────────────────────────────┐
│      executeNextPhase(index)    │◄───────────────────┐
│  Check: index >= phases.length? │                    │
│  YES → startBattleRound()       │                    │
│  NO  → execute phase            │                    │
└──────────────┬──────────────────┘                    │
               │                                        │
      ┌────────┴────────┐                              │
      ▼                 ▼                              │
┌───────────┐    ┌─────────────┐                       │
│  Player   │    │   Enemy     │                       │
│  Phase    │    │   Phase     │                       │
└─────┬─────┘    └──────┬──────┘                       │
      │                 │                              │
      ▼                 │                              │
  (wait for             │                              │
   end turn)            │                              │
      │                 ▼                              │
      │          executeNextPhase(index+1) ───────────┤
      ▼                                               │
handleEndTurn() → executeNextPhase(index+1) ──────────┘
```

**Key Function Implementations:**

```typescript
// Start a new battle round (generates phase queue)
const startBattleRound = useCallback(async () => {
  // Calculate base speeds
  const basePlayerSpeed = calculatePlayerSpeed(playerBuffs);
  const baseEnemySpeed = currentEnemy
    ? calculateEnemySpeed(currentEnemy, enemyBuffs)
    : 0;

  // Apply randomness with mean-reversion
  const playerResult = applySpeedRandomness(basePlayerSpeed, playerRandomState);
  const enemyResult = applySpeedRandomness(baseEnemySpeed, enemyRandomState);

  setPlayerRandomState(playerResult.newRandomState);
  setEnemyRandomState(enemyResult.newRandomState);
  setPlayerSpeed(playerResult.speed);
  setEnemySpeed(enemyResult.speed);

  // Generate phase queue
  const result = generatePhaseQueue(playerResult.speed, enemyResult.speed);
  setPhaseQueue(result.queue);
  setCurrentPhaseIndex(0);

  // Start first phase
  await executeNextPhase(result.queue, 0);
}, [
  playerBuffs,
  enemyBuffs,
  currentEnemy,
  playerRandomState,
  enemyRandomState,
]);

// Execute next phase in queue
const executeNextPhase = useCallback(
  async (queue: PhaseQueue, index: number) => {
    if (index >= queue.phases.length) {
      // All phases complete - start new round
      await startBattleRound();
      return;
    }

    setCurrentPhaseIndex(index);
    const currentActor = queue.phases[index];

    if (currentActor === "player") {
      await startPlayerPhase();
      // Player phase waits for handleEndTurn to advance
    } else {
      await executeEnemyPhase();
      // Enemy phase auto-advances
      await executeNextPhase(queue, index + 1);
    }
  },
  [startBattleRound]
);

// Player phase start
const startPlayerPhase = useCallback(
  async () => {
    await showMessage("Player Phase", 2500);

    // Set phase state
    setIsPlayerPhase(true);
    setIsEnemyPhase(false);

    // Increment phase count
    setPhaseCount((c) => c + 1);

    // Reset guard
    setPlayerGuard(0);

    // Energy recovery to MAX
    setEnergy(maxEnergy);

    // Process buff/debuff durations
    setPlayerBuffs((b) => decreaseBuffDebuffDuration(b));

    // Start-of-phase healing/shield
    const { hp, shield } = calculateStartTurnHealing(playerBuffs);
    if (hp > 0) setPlayerHp((h) => applyHeal(hp, h, playerMaxHp));
    if (shield > 0) setPlayerGuard((g) => g + shield);

    // Stun check
    if (!canAct(playerBuffs)) {
      await new Promise((r) => setTimeout(r, 1500));
      setIsPlayerPhase(false);
      // Skip to next phase
      const queue = phaseQueue!;
      await executeNextPhase(queue, currentPhaseIndex + 1);
      return;
    }

    // Draw cards
    // ... existing draw logic ...
  },
  [
    /* deps */
  ]
);

// Enemy phase
const executeEnemyPhase = useCallback(
  async () => {
    await showMessage("Enemy Phase", 2500);

    // Set phase state
    setIsPlayerPhase(false);
    setIsEnemyPhase(true);

    // Increment phase count
    setPhaseCount((c) => c + 1);

    // Reset guard
    setEnemyGuard(currentEnemy.startingGuard);

    // Energy recovery to MAX
    setEnemyEnergy(currentEnemy.actEnergy);

    // Process buff/debuff durations
    setEnemyBuffs((b) => decreaseBuffDebuffDuration(b));

    // Start-of-phase healing/shield
    const { hp, shield } = calculateStartTurnHealing(enemyBuffs);
    if (hp > 0) setEnemyHp((h) => applyHeal(hp, h, enemyMaxHp));
    if (shield > 0) setEnemyGuard((g) => g + shield);

    // Stun check
    if (!canAct(enemyBuffs)) {
      await new Promise((r) => setTimeout(r, 1500));
      setIsEnemyPhase(false);
      return;
    }

    // Execute enemy actions
    await executeEnemyActions(/* ... */);

    // End-of-phase DoT damage
    const dotDamage = calculateEndTurnDamage(enemyBuffs);
    if (dotDamage > 0) setEnemyHp((h) => Math.max(0, h - dotDamage));

    // Clear phase state
    setIsEnemyPhase(false);
  },
  [
    /* deps */
  ]
);

// Handle end turn (advances phase queue)
const handleEndTurn = useCallback(
  () => {
    if (!isPlayerPhase) return; // Guard: only during player phase

    // Set transition state
    setIsPlayerPhase(false);
    setIsEnemyPhase(false);

    // End-of-phase DoT damage
    const dotDamage = calculateEndTurnDamage(playerBuffs);
    if (dotDamage > 0) setPlayerHp((h) => Math.max(0, h - dotDamage));

    // Discard hand
    const cardsToDiscard = [...deckState.hand];
    discardCardsWithAnimation(cardsToDiscard, 250, () => {
      dispatch({ type: "END_TURN", cardsToDiscard });

      // Advance to next phase
      const queue = phaseQueue!;
      executeNextPhase(queue, currentPhaseIndex + 1);
    });
  },
  [
    /* deps */
  ]
);
```

**Return Values Update:**

```typescript
return {
  // ... existing
  // REMOVE: speedBonusPlayer, speedBonusEnemy,
  // ADD:
  phaseQueue,
  currentPhaseIndex,
  // ... rest
};
```

---

### 4. MODIFY: `src/battles/battleUI/TurnOrderIndicator.tsx`

**New Props:**

```typescript
interface TurnOrderIndicatorProps {
  playerSpeed: number;
  enemySpeed: number;
  phaseQueue: PhaseQueue | null;
  currentPhaseIndex: number;
}
```

**New UI:**

```tsx
export const TurnOrderIndicator: React.FC<TurnOrderIndicatorProps> = ({
  playerSpeed,
  enemySpeed,
  phaseQueue,
  currentPhaseIndex,
}) => {
  if (!phaseQueue) return null;

  return (
    <div className="turn-order-indicator">
      {/* Speed display */}
      <div className="speed-display">
        <span>Player: {playerSpeed}</span>
        <span>Enemy: {enemySpeed}</span>
      </div>

      {/* Phase queue visualization */}
      <div className="phase-queue flex gap-1">
        {phaseQueue.phases.map((actor, index) => (
          <div
            key={index}
            className={`
              phase-marker w-8 h-8 rounded flex items-center justify-center
              ${actor === "player" ? "bg-blue-600" : "bg-red-600"}
              ${index === currentPhaseIndex ? "ring-2 ring-yellow-400" : ""}
              ${index < currentPhaseIndex ? "opacity-50" : ""}
            `}
          >
            {actor === "player" ? "P" : "E"}
          </div>
        ))}
      </div>
    </div>
  );
};
```

---

### 5. MODIFY: `src/battles/battleUI/BattleScreen.tsx`

**Update Props:**

```typescript
// REMOVE
speedBonusPlayer = { speedBonusPlayer };
speedBonusEnemy = { speedBonusEnemy };

// ADD
phaseQueue = { phaseQueue };
currentPhaseIndex = { currentPhaseIndex };
```

---

## Implementation Steps

### Step 1: Create `phaseCalculation.ts`

1. Define types (`PhaseActor`, `PhaseQueue`, `SpeedRandomState`, `PhaseCalculationResult`)
2. Implement `applySpeedRandomness()` with mean-reversion
3. Implement `calculateConsecutivePhases()`
4. Implement `generatePhaseQueue()`

### Step 2: Update `speedCalculation.ts`

1. Remove `SpeedBonus` interface
2. Remove `calculateSpeedBonus()` function
3. Add re-exports from `phaseCalculation.ts`

### Step 3: Update `useBattleLogic.ts` - State Layer

1. Remove `speedBonusPlayer`, `speedBonusEnemy` state
2. Add `phaseQueue`, `currentPhaseIndex` state
3. Add `playerRandomState`, `enemyRandomState` state

### Step 4: Update `useBattleLogic.ts` - Function Layer

1. Create `startBattleRound()` function
2. Create `executeNextPhase()` function
3. Refactor `startPlayerTurn()` → `startPlayerPhase()`
4. Refactor `executeEnemyTurn()` → `executeEnemyPhase()`
5. Update `handleEndTurn()` to advance phase queue

### Step 5: Update UI Components

1. Update `TurnOrderIndicator` props and rendering
2. Update `BattleScreen` to pass new props

### Step 6: Clean Up

1. Remove unused imports
2. Update type exports
3. Test all edge cases

---

## Edge Cases

### Stun Handling

- Stunned actor skips their phase but does NOT regenerate queue
- Simply advance to next phase in queue

### Battle End Mid-Phase

- Check `playerHp <= 0` or `allEnemiesDead` after each action
- If battle ends, stop queue execution immediately

### Multiple Enemies

- Use **fastest enemy speed** for queue calculation
- All enemies act during single "enemy phase"

### Speed Changes During Round

- Buffs/debuffs applied mid-round take effect on **next round**
- Current round's queue is locked once generated

### Edge Case: Very High Speed Difference

- No upper limit on consecutive phases
- diff=100 → 10+ consecutive phases (may need UI consideration)

---

## Testing Checklist

- [ ] Equal speeds (diff < 15) → alternating phases [P, E, P, E...]
- [ ] Speed diff 15 → 2 consecutive [P, P, E] or [E, E, P]
- [ ] Speed diff 25 → 3 consecutive
- [ ] Speed diff 35 → 4 consecutive
- [ ] Randomness occasionally flips expected order
- [ ] Mean-reversion prevents streaky outcomes
- [ ] Energy recovers to MAX each phase
- [ ] Stun correctly skips phase without regenerating queue
- [ ] Battle ends cleanly mid-queue
- [ ] Multiple enemies work correctly
- [ ] UI displays phase queue correctly
- [ ] Current phase is highlighted

---

## Migration Notes

### Breaking Changes

- `speedBonusPlayer` and `speedBonusEnemy` removed from hook return
- `SpeedBonus` type no longer exported
- `turn` renamed to `phaseCount`
- `turnPhase` replaced with `isPlayerPhase` + `isEnemyPhase` boolean flags
- Turn concept replaced with round/phase concept

### State Naming Convention

| Old                | New             | Type      |
| ------------------ | --------------- | --------- |
| `turn`             | `phaseCount`    | `number`  |
| `turnPhase`        | `isPlayerPhase` | `boolean` |
| `turnPhase`        | `isEnemyPhase`  | `boolean` |
| `speedBonusPlayer` | (removed)       | -         |
| `speedBonusEnemy`  | (removed)       | -         |

### Backwards Compatibility

- `phaseCount` still tracks progression (increments each phase)
- Existing card effects unchanged
- Buff/debuff duration system unchanged
