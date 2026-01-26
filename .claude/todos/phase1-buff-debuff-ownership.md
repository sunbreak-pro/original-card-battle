# Phase 1: Buff/Debuff Ownership System Design Document

## Overview

Separate buff/debuff duration reduction timing based on the owner (appliedBy).
This resolves the issue where debuffs applied by enemies are meaninglessly reduced at the start of the Player Phase.

## Current Problem

```
Current Flow:
┌──────────────────────────────────────────────────────────────────┐
│ Player Phase Start                                               │
│   └─→ decreaseBuffDebuffDuration(enemyBuffs)  ← Enemy-applied    │
│                                                  also decreased! │
└──────────────────────────────────────────────────────────────────┘

Example Problem:
- Turn 1: Enemy Phase - Enemy applies "Poison (duration: 3)" to Player
- Turn 2: Player Phase Start - Poison duration decreases 3→2 ← WRONG!
- Effectively only 2 turns of effect
```

## Solution Approach

**Owner-Based Duration Tracking**

1. Add `appliedBy: PhaseActor` field to BuffDebuffState
2. At phase start, only decrease buff/debuffs **applied by the opponent**
3. Self-applied buffs (e.g., Haste from own card) decrease at own phase start

## Detailed Design

### 1. Type Definition Changes

**File: `domain/battles/type/buffType.ts`**

```typescript
// New addition
export type BuffOwner = 'player' | 'enemy' | 'environment';

// Extend existing BuffDebuffState
export interface BuffDebuffState {
  name: BuffDebuffType;
  stacks: number;
  duration: number;
  value: number;
  isPermanent: boolean;
  source?: string;
  appliedBy: BuffOwner;  // NEW: Who applied this buff/debuff
}
```

### 2. buffLogic.ts Changes

**File: `domain/battles/logic/buffLogic.ts`**

```typescript
/**
 * New Duration Reduction Logic
 *
 * Rules:
 * - At own phase start, decrease buff/debuffs applied by opponent
 * - At own phase start, also decrease self-applied buff/debuffs (lasts until next turn)
 *
 * Result:
 * - Duration 3 debuff takes effect for 3 opponent phases after application
 */
export function decreaseBuffDebuffDurationForPhase(
  map: BuffDebuffMap,
  currentActor: PhaseActor
): BuffDebuffMap {
  const newMap = new Map<BuffDebuffType, BuffDebuffState>();

  map.forEach((buff, type) => {
    // Permanent effects do not decrease
    if (buff.isPermanent) {
      newMap.set(type, buff);
      return;
    }

    // Opponent-applied: decrease at own phase start
    // Self-applied: decrease at own phase start
    // → All decrease at own phase start (this is correct behavior)
    //
    // However, do not decrease immediately after application (determined by appliedBy)
    // Enemy Phase: Enemy applies Debuff to Player
    // → Player Phase Start: Do not decrease (effect not yet received)
    // → Enemy Phase Start: Decrease (1 turn elapsed)

    const ownerPhase = buff.appliedBy === 'player' ? 'player' : 'enemy';
    const shouldDecrease = ownerPhase === currentActor;

    if (shouldDecrease) {
      if (buff.duration > 1) {
        newMap.set(type, { ...buff, duration: buff.duration - 1 });
      }
      // If duration is 1 or less, remove (don't add to Map)
    } else {
      // Not target for decrease, maintain as is
      newMap.set(type, buff);
    }
  });

  return newMap;
}

/**
 * Deprecate old function (maintain for backward compatibility)
 * @deprecated Use decreaseBuffDebuffDurationForPhase instead
 */
export function decreaseBuffDebuffDuration(map: BuffDebuffMap): BuffDebuffMap {
  // Maintain existing behavior (decrease all)
  // Keep for gradual migration
  const newMap = new Map<BuffDebuffType, BuffDebuffState>();

  map.forEach((buff, type) => {
    if (buff.isPermanent) {
      newMap.set(type, buff);
    } else if (buff.duration > 1) {
      newMap.set(type, { ...buff, duration: buff.duration - 1 });
    }
  });

  return newMap;
}
```

### 3. addOrUpdateBuffDebuff Changes

**File: `domain/battles/logic/buffLogic.ts`**

```typescript
/**
 * Add or update Buff/Debuff
 * appliedBy parameter is now required
 */
export function addOrUpdateBuffDebuff(
  map: BuffDebuffMap,
  name: BuffDebuffType,
  stacks: number,
  duration: number,
  value: number,
  isPermanent: boolean,
  appliedBy: BuffOwner,  // NEW: Required parameter
  source?: string
): BuffDebuffMap {
  const newMap = new Map(map);
  const existing = newMap.get(name);

  if (existing) {
    // Update existing buff
    newMap.set(name, {
      name,
      stacks: existing.stacks + stacks,
      duration: Math.max(existing.duration, duration),
      value: Math.max(existing.value, value),
      isPermanent: isPermanent || existing.isPermanent,
      appliedBy: existing.appliedBy, // Maintain original applier
      source: source ?? existing.source,
    });
  } else {
    // Add new
    newMap.set(name, {
      name,
      stacks,
      duration,
      value,
      isPermanent,
      appliedBy,
      source,
    });
  }

  return newMap;
}
```

### 4. Phase Execution Logic Changes

**File: `domain/battles/execution/playerPhaseExecution.ts`**

```typescript
export function calculatePlayerPhaseStart(
  input: PlayerPhaseStartInput
): PlayerPhaseStartResult {
  const { playerBuffs, currentActor } = input; // Add currentActor

  // Use new duration reduction logic
  const newBuffs = decreaseBuffDebuffDurationForPhase(playerBuffs, currentActor);

  // Existing logic continues...
}
```

**File: `domain/battles/execution/enemyPhaseExecution.ts`**

```typescript
export function calculateEnemyPhaseStart(
  input: EnemyPhaseStartInput
): EnemyPhaseStartResult {
  const { enemy, enemyBuffs, currentActor } = input; // Add currentActor

  // Use new duration reduction logic
  const newBuffs = decreaseBuffDebuffDurationForPhase(enemyBuffs, currentActor);

  // Existing logic continues...
}
```

### 5. Buff Application on Card Execution

**File: `domain/battles/managements/useCardExecution.ts`**

When applying buff/debuff from cards, set `appliedBy: 'player'`.

```typescript
// Apply buff to Player from card effect
addOrUpdateBuffDebuff(
  playerBuffs,
  'attackBuff',
  1,      // stacks
  3,      // duration
  15,     // value (15% attack increase)
  false,  // isPermanent
  'player', // appliedBy - card user
  cardId    // source
);

// Apply debuff to Enemy from card effect
addOrUpdateBuffDebuff(
  enemyBuffs,
  'poison',
  1,      // stacks
  3,      // duration
  5,      // value (5 damage per turn)
  false,  // isPermanent
  'player', // appliedBy - card user
  cardId    // source
);
```

### 6. Debuff Application on Enemy Attack

**File: `domain/battles/execution/enemyPhaseExecution.ts`**

```typescript
export function applyEnemyDebuffsToPlayer(
  currentBuffs: BuffDebuffMap,
  debuffs: BuffDebuffState[],
  appliedBy: BuffOwner = 'enemy'  // Default to 'enemy'
): BuffDebuffMap {
  if (debuffs.length === 0) {
    return currentBuffs;
  }

  let newBuffs = currentBuffs;
  debuffs.forEach((debuff) => {
    newBuffs = addOrUpdateBuffDebuff(
      newBuffs,
      debuff.name,
      debuff.stacks,
      debuff.duration,
      debuff.value,
      debuff.isPermanent,
      appliedBy,  // Applied by Enemy
      debuff.source
    );
  });

  return newBuffs;
}
```

## Implementation Tasks

### Task 1: Update Type Definitions
- [ ] Add `BuffOwner` type to `buffType.ts`
- [ ] Add `appliedBy` field to `BuffDebuffState`

### Task 2: Update buffLogic.ts
- [ ] Add new `decreaseBuffDebuffDurationForPhase()` function
- [ ] Add `appliedBy` parameter to `addOrUpdateBuffDebuff()`
- [ ] Mark existing `decreaseBuffDebuffDuration()` as `@deprecated`

### Task 3: Update Phase Execution Logic
- [ ] Use new reduction logic in `playerPhaseExecution.ts`
- [ ] Use new reduction logic in `enemyPhaseExecution.ts`
- [ ] Set `appliedBy` in `applyEnemyDebuffsToPlayer()`

### Task 4: Update Card Execution Logic
- [ ] Set `appliedBy: 'player'` when applying buffs in `useCardExecution.ts`
- [ ] Review and update all buff application locations

### Task 5: Backward Compatibility for Existing Data
- [ ] Add fallback when loading existing `BuffDebuffState` without `appliedBy`
- [ ] Set default value for `appliedBy`

### Task 6: Testing and Verification
- [ ] Verify correct timing of duration reduction
- [ ] Test both self-buffs and opponent debuffs
- [ ] Verify permanent effects (isPermanent) do not decrease

## Impact Scope

### Files Requiring Changes
1. `domain/battles/type/buffType.ts`
2. `domain/battles/logic/buffLogic.ts`
3. `domain/battles/execution/playerPhaseExecution.ts`
4. `domain/battles/execution/enemyPhaseExecution.ts`
5. `domain/battles/managements/useCardExecution.ts`
6. `domain/battles/managements/useBattleState.ts`

### Files Not Requiring Changes (Interface-Only Changes)
- `domain/battles/calculators/buffCalculation.ts` - Buff effect calculation unchanged
- `domain/battles/data/buffData.ts` - Buff data definitions unchanged

## Backward Compatibility

1. Maintain `decreaseBuffDebuffDuration()` as `@deprecated`
2. Treat existing data without `appliedBy` as `'environment'`
3. Gradually migrate to new logic

## Future Extensions

### Connection to Phase 2
- Can add `'summon'` to `BuffOwner` in the future
- Can extend to `'enemy_1'`, `'enemy_2'` etc. for multi-enemy support

### Environment Effects
- Use `'environment'` to represent field effects and traps
- Example: Poison automatically applied by poison swamp field

---

## Completion Criteria

1. ✅ All type definitions are updated
2. ✅ New duration reduction logic is implemented
3. ✅ All buff application locations have `appliedBy` set
4. ✅ Backward compatibility with existing code is maintained
5. ✅ Verified working through tests

---

Created: 2025-01-26
Status: Design Complete - Awaiting Implementation
