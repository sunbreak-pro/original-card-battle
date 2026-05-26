# Battle Orchestration System

## Overview

`useBattleOrchestrator` is the main entry point for all battle logic. It composes 8+ hooks into a unified interface that manages player/enemy state, phase queue, card execution, class abilities, animations, and battle flow.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/domain/battles/managements/useBattleOrchestrator.ts` | 882 | Main orchestrator — composes all hooks |
| `src/domain/battles/managements/useBattleState.ts` | 620 | Player/enemy/target state management |
| `src/domain/battles/managements/useBattlePhase.ts` | 212 | Phase queue, speed, turn order |
| `src/domain/battles/managements/executeCharacterManage.ts` | 200+ | Player/enemy phase execution bridge |
| `src/domain/battles/managements/battleFlowManage.ts` | — | Battle flow utilities |
| `src/domain/battles/execution/playerPhaseExecution.ts` | 150 | Pure functions: player phase start/end |
| `src/domain/battles/execution/enemyPhaseExecution.ts` | 248 | Pure functions: enemy phase start/end + attack |
| `src/domain/battles/calculators/speedCalculation.ts` | 80 | Speed formulas + mean-reversion randomness |
| `src/domain/battles/calculators/phaseCalculation.ts` | 167 | Phase queue generation from speed diff |
| `src/domain/battles/logic/phaseLogic.ts` | 156 | Phase transition helpers (partially deprecated) |

## Data Structures

### PhaseState (useBattlePhase)

```typescript
interface PhaseState {
  phaseQueue: PhaseQueue | null;   // Generated turn order
  currentPhaseIndex: number;        // Current position in queue
  phaseCount: number;               // Total phases elapsed (1-based)
  isPlayerPhase: boolean;           // Player's turn active
  isEnemyPhase: boolean;            // Enemy's turn active
  playerSpeed: number;              // Current (with randomness)
  enemySpeed: number;               // Current (with randomness)
}
```

### PhaseQueue / PhaseEntry

```typescript
interface PhaseQueue {
  phases: PhaseActor[];     // ["player", "enemy", "player", ...]
  entries: PhaseEntry[];    // Expanded with enemyIndex for multi-enemy
  currentIndex: number;
}

interface PhaseEntry {
  actor: PhaseActor;        // "player" | "enemy"
  enemyIndex?: number;      // Which enemy acts (multi-enemy only)
}
```

### PlayerState (useBattleState)

```typescript
interface PlayerState {
  hp: number; maxHp: number;
  ap: number; maxAp: number;
  guard: number;
  energy: number; maxEnergy: number;
  buffs: BuffDebuffMap;
  name: string;
  playerClass: CharacterClass;
  classGrade: string;
}
```

### EnemyBattleState (per enemy)

```typescript
interface EnemyBattleState {
  definition: EnemyDefinition;
  hp: number; maxHp: number;
  ap: number; maxAp: number;
  guard: number;
  energy: number;
  buffDebuffs: BuffDebuffMap;
  ref: RefObject<HTMLDivElement | null>;
}
```

## Logic Flow

### Battle Initialization

```
useBattleOrchestrator mounted
  ↓
useEffect detects currentEnemy exists + !battleInitializedRef
  ↓
initializeBattle()
  ↓
phaseState.generatePhaseQueueFromSpeeds(playerBuffs, enemy, enemyBuffs)
  ├─ calculatePlayerSpeed(playerBuffs)    → base 50 ± buff modifiers
  ├─ calculateEnemySpeed(enemy, buffs)    → enemy.baseSpeed ± modifiers
  ├─ applySpeedRandomness(base, history)  → ±5% mean-reversion variance
  └─ generatePhaseQueue(pSpeed, eSpeed)   → PhaseQueue
  ↓
executeNextPhase(queue, 0)
```

### Phase Execution Loop

```
executeNextPhaseImpl(queue, index)
  ↓
expandPhaseEntriesForMultipleEnemies(queue, enemies)
  │  Single enemy: entries unchanged
  │  Multi enemy: each "enemy" slot → N entries (one per alive enemy)
  ↓
if index >= expandedEntries.length:
  │  Round complete → generatePhaseQueueFromSpeeds() → restart at index 0
  ↓
currentEntry = expandedEntries[index]
  ↓
if "player":
  │  executePlayerPhase() → waits for user handleEndPhase()
  ↓
if "enemy":
  │  executeEnemyPhaseForIndex(enemyIndex)
  │  ↓ (auto-advance)
  └─ executeNextPhaseRef.current(queue, index + 1)
```

### Player Phase Flow

```
executePlayerPhase()
  ↓
calculatePlayerPhaseStart(playerBuffs, drawPile, discardPile)
  ├─ decreaseBuffDebuffDurationForPhase(buffs, 'player')  ← only player-applied tick
  ├─ calculateStartPhaseHealing(buffs) → {hp, shield}
  ├─ cleanse check → removeAllDebuffs()
  └─ drawCards(drawCount, drawPile, discardPile)
  ↓
Apply healing/shield → setPlayerHp, setPlayerGuard
Show animations
  ↓
Player plays cards (handleCardPlay) ...
  ↓
handleEndPhase() ← user clicks "End Turn"
  ├─ calculatePlayerPhaseEnd(buffs)
  │   ├─ calculateEndPhaseDamage() → DoT (burn/poison)
  │   └─ momentum stacking (+1 stack)
  ├─ Apply DoT damage to player
  ├─ discard hand with animation
  └─ executeNextPhase(queue, nextIndex)
```

### Enemy Phase Flow

```
executeEnemyPhaseForIndex(enemyIndex)
  ↓
buildEnemyBattleStats(enemy) → BattleStats
  ↓
executeEnemyPhaseImpl(context)
  ├─ calculateEnemyPhaseStart(enemy, enemyBuffs)
  │   ├─ decreaseBuffDebuffDurationForPhase(buffs, 'enemy')
  │   ├─ calculateStartPhaseHealing(buffs)
  │   ├─ guard reset (if startingGuard) → baseMaxAp * 0.5
  │   ├─ energy reset → enemy.actEnergy
  │   └─ canAct check (stun)
  ├─ AI selects action → processEnemyAction()
  │   ├─ attack → calculateDamage + applyDamageAllocation
  │   ├─ guard gain
  │   └─ debuffs to apply
  ├─ Apply damage to player (Guard → AP → HP)
  ├─ Apply debuffs with appliedBy: 'enemy'
  └─ calculateEnemyPhaseEnd → DoT damage to enemy
```

### Speed & Phase Queue Generation

```
speedDiff = |playerSpeed - enemySpeed|

Consecutive phases per actor:
  diff <  15 → 1 (alternating: P, E, P, E, ...)
  diff >= 15 → 2 (P, P, E, P, P, E, ...)
  diff >= 25 → 3
  each +10   → +1 more

Speed randomness:
  variance = baseSpeed * VARIANCE_PERCENT(5) / 100
  meanReversion adjusts based on last 5 rolls
  finalSpeed = base + adjustedVariance (clamped ±5%)
```

## Dependencies

```
useBattleOrchestrator
  ├─ useBattleState        → player/enemy state + derived values
  ├─ useBattlePhase        → phase queue + speed management
  ├─ useCharacterPhaseExecution → player/enemy phase bridges
  ├─ useCardExecution      → card play resolution
  ├─ useSwordEnergy        → Swordsman class ability
  ├─ useElementalChain     → Mage class ability
  ├─ useEnemyAI            → (initialized, not yet wired)
  ├─ useCardAnimation      → draw/discard/play/damage animations
  ├─ useTurnTransition     → turn message display
  ├─ deckReducer           → (IMMUTABLE) deck state management
  ├─ createInitialDeck     → deck creation from config
  └─ applyMasteryToCards   → mastery stat application
```

External dependencies:
- `PlayerContext` provides `InitialPlayerState` (HP/AP/speed/deck/mastery)
- `DungeonRunProvider` provides enemy definitions
- `BattleScreen` UI consumes the returned interface

## Vulnerability Analysis

### `[BUG-RISK]` Stale Closure in executeNextPhaseImpl

**Location:** `useBattleOrchestrator.ts:550-608`

The `executeNextPhaseRef` pattern is used to avoid stale closures, but the ref update happens in a separate `useEffect`. Between the time `executeNextPhaseImpl` is recreated and the `useEffect` runs, `executeNextPhaseRef.current` still points to the old function. If `executeNextPhase` is called during this gap (e.g. rapid enemy phase auto-advance), it will use stale state.

**Trigger:** Fast consecutive enemy turns in multi-enemy battles where state changes between renders haven't propagated to the ref yet.

### `[BUG-RISK]` Phase Queue Expansion Uses Stale enemies Array

**Location:** `useBattleOrchestrator.ts:562`

```typescript
const expandedEntries = expandPhaseEntriesForMultipleEnemies(queue, enemies);
```

`enemies` is captured at callback creation time. During a round, if an enemy dies mid-sequence, the expansion still uses the snapshot from when `executeNextPhaseImpl` was last recreated. The dead-enemy skip at line 586 mitigates this, but the expansion itself may produce extra unnecessary entries.

### `[BUG-RISK]` Healing Calculated from Pre-Duration-Decrease Buffs

**Location:** `playerPhaseExecution.ts:74`

```typescript
const { hp: healAmount, shield: shieldAmount } = calculateStartPhaseHealing(playerBuffs);
```

Healing is calculated from the original `playerBuffs` (before duration decrease), not from `newBuffs`. This means a regeneration buff on its last turn (duration=1) will still heal, even though it was removed by `decreaseBuffDebuffDurationForPhase`. This could be intentional (heal on final tick) but is inconsistent with DoT damage at phase end, which uses post-momentum buffs.

### `[BUG-RISK]` handleEndPhase Reads phaseState.currentPhaseIndex

**Location:** `useBattleOrchestrator.ts:648`

```typescript
const nextPhaseIndex = phaseState.currentPhaseIndex + 1;
```

This reads `currentPhaseIndex` which is React state from `useBattlePhase`. The value is captured at render time. If `setPhaseIndex` was called earlier in the same render cycle (line 576), this read may not reflect the latest value due to batched state updates.

### `[BUG-RISK]` generatePhaseQueueFromSpeeds Closure Over Random State

**Location:** `useBattlePhase.ts:82-111`

```typescript
const generatePhaseQueueFromSpeeds = useCallback((...) => {
  const playerResult = applySpeedRandomness(basePlayerSpeed, playerRandomState);
  // ...
  setPlayerRandomState(playerResult.newRandomState);
}, [playerRandomState, enemyRandomState]);
```

The `useCallback` depends on `playerRandomState` and `enemyRandomState`. If this function is called twice in quick succession (e.g. battle reset + reinitialize), the second call uses the same random state because `setPlayerRandomState` hasn't been flushed yet. This could produce identical speed values for two consecutive rounds.

### `[EXTENSIBILITY]` 882-Line Orchestrator

**Location:** `useBattleOrchestrator.ts`

The orchestrator is the largest single file in the codebase. It handles initialization, deck setup, phase flow, card play, animations, modals, battle result, enemy preview, and reset. Adding new features (e.g. AoE, new class abilities) requires modifying this file.

### `[EXTENSIBILITY]` Multi-Enemy Support is Bolted On

**Location:** `expandPhaseEntriesForMultipleEnemies` + scoped setters

Multi-enemy was added by expanding phase entries and creating scoped setters per enemy index. The core phase queue still generates a single-enemy pattern. This means enemy-vs-enemy interactions, ally targeting, or conditional turn ordering cannot be expressed in the current model.

### `[EXTENSIBILITY]` useEnemyAI Initialized But Unused

**Location:** `useBattleOrchestrator.ts:405`

```typescript
useEnemyAI();
```

Called unconditionally to satisfy React hooks rules, but the return value is discarded. Enemy AI is actually invoked through `executeCharacterManage` → `enemyPhaseExecution` → `enemyAI.enemyAction()`. This creates confusion about where AI logic lives.

### `[QUALITY]` Legacy Compatibility Surface

**Location:** `useBattleOrchestrator.ts:800-882`

The return object exposes both modern (`enemies`, `aliveEnemies`) and legacy (`enemyHp`, `enemyMaxHp`, `enemyGuard`) interfaces. Several deprecated aliases exist (`startBattleRound`, `cardEnergy`, `onDepthChange`). This increases the API surface and makes it unclear which interface consumers should use.

### `[QUALITY]` Phase Count vs Phase Index Confusion

Two different concepts track progress: `phaseCount` (monotonically increasing total, used for enemy AI pattern selection) and `currentPhaseIndex` (position within current round's queue, resets each round). Both are exposed in the return value, and consumers need to know which to use.
