# Enemy System

## Overview

The enemy system defines enemy creatures across 5 depth tiers, each with AI patterns that determine actions based on HP thresholds, turn numbers, and probability weights. Enemies operate on an energy system where each turn they spend energy on actions until depleted. The AI system is pure-functional: `determineEnemyAction()` selects actions, `executeEnemyActions()` loops through energy-costed actions, and `enemyPhaseExecution.ts` handles phase start/end effects. Multi-enemy support allows single/double/triple/boss encounters.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/constants/data/characters/enemy/enemyDepth1.ts` | ~200 | Depth 1 enemies (Forest) — slime, goblin, wolf, treant, boss |
| `src/constants/data/characters/enemy/enemyDepth2.ts` | ~200 | Depth 2 enemies (Cave) — bat, skeleton, spider, golem, boss |
| `src/constants/data/characters/enemy/enemyDepth3.ts` | ~200 | Depth 3 enemies (Chaos) — cultist, demon, shadow, wraith, boss |
| `src/constants/data/characters/enemy/enemyDepth4.ts` | ~200 | Depth 4 enemies (Void) — phantom, devourer, aberration, boss |
| `src/constants/data/characters/enemy/enemyDepth5.ts` | ~200 | Depth 5 enemies (Abyss) — abyssal creatures, final boss |
| `src/domain/characters/enemy/logic/enemyAI.ts` | 126 | Core AI: determineEnemyAction, enemyAction card converter, selectRandomEnemy |
| `src/domain/characters/enemy/logic/enemyActionExecution.ts` | 115 | Action execution loop with energy, preview, fallback |
| `src/domain/characters/logic/enemyUtils.ts` | 28 | generateEnemyInstanceId helper |
| `src/domain/battles/logic/enemyStateLogic.ts` | 41 | createEnemyStateFromDefinition factory |
| `src/domain/battles/managements/useEnemyAI.ts` | 300 | React hook wrapping AI + execution + preview |
| `src/domain/battles/execution/enemyPhaseExecution.ts` | 247 | Phase start/end calculations, attack damage, debuff application |

## Data Structures

### EnemyDefinition

```typescript
interface EnemyDefinition {
  id: string;
  name: string;
  baseMaxHp: number;
  baseMaxAp: number;
  baseSpeed: number;
  actEnergy: number;         // Energy per turn (actions cost energy)
  startingGuard: boolean;    // Whether enemy starts with guard
  imagePath?: string;
  aiPatterns: EnemyAIPattern[];
}
```

### EnemyAIPattern

```typescript
interface EnemyAIPattern {
  phaseNumber: number;       // 0 = any turn, N = specific turn
  condition?: (hp: number, maxHp: number) => boolean;  // HP threshold check
  probability?: number;      // Weight for random selection (default 1.0)
  action: EnemyAction;
}
```

### EnemyAction

```typescript
interface EnemyAction {
  name: string;
  type: "attack" | "guard" | "buff" | "debuff" | "special";
  baseDamage?: number;
  guardGain?: number;
  hitCount?: number;
  energyCost?: number;       // Default 1
  penetration?: number;
  applyDebuffs?: BuffDebuffState[];
  displayIcon?: string;
  priority?: number;
}
```

### EnemyBattleState (runtime)

```typescript
interface EnemyBattleState {
  instanceId: string;
  definitionId: string;
  definition: EnemyDefinition;
  hp: number; maxHp: number;
  ap: number; maxAp: number;
  guard: number;
  speed: number;
  buffDebuffs: BuffDebuffMap;
  energy: number;
  phaseCount: number;
  turnCount: number;
  ref: RefObject<HTMLDivElement | null>;
}
```

### DepthEnemyData

```typescript
interface DepthEnemyData {
  single: { enemies: EnemyDefinition[] }[];
  double: { enemies: EnemyDefinition[] }[];
  three: { enemies: EnemyDefinition[] }[];
  boss: EnemyDefinition;
}
```

## Logic Flow

### Enemy Selection

```
selectRandomEnemy(depth, encounterSize)
  ├─ Load DepthEnemyData from DEPTH1-5_ENEMIES
  ├─ encounterSize: "single" | "double" | "three" | "boss"
  │   ├─ single/double/three → random pattern from array → enemies[]
  │   └─ boss → data.boss (single enemy)
  └─ Returns { enemies: EnemyDefinition[], isBoss: boolean }
```

### Enemy State Initialization

```
createEnemyStateFromDefinition(definition, ref?)
  ├─ instanceId: generateEnemyInstanceId(definition.id)
  ├─ hp/maxHp: definition.baseMaxHp
  ├─ ap/maxAp: definition.baseMaxAp
  ├─ guard: startingGuard ? floor(baseMaxAp * GUARD_INIT_MULTIPLIER) : 0
  ├─ energy: definition.actEnergy
  ├─ buffDebuffs: createEmptyBuffDebuffMap()
  └─ phaseCount: 0, turnCount: 0
```

### AI Action Selection

```
determineEnemyAction(enemy, currentHp, maxHp, turnNumber, _remainingEnergy)
  ↓
Filter aiPatterns:
  ├─ phaseNumber === 0 (any turn) OR phaseNumber === turnNumber
  └─ condition(hp, maxHp) passes (or no condition)
  ↓
If no valid patterns → fallback: "基本攻撃" (5 damage)
  ↓
Weighted random selection:
  ├─ Each pattern has probability (default 1.0)
  ├─ Sum all probabilities → totalProbability
  ├─ random * totalProbability → cumulative selection
  └─ Return pattern.action
```

### Action Execution Loop

```
executeEnemyActions(enemy, hp, maxHp, turn, energy, onExecuteAction, checkBattleEnd)
  ↓
while remainingEnergy > 0:
  ├─ determineEnemyAction(enemy, hp, maxHp, turn, remainingEnergy)
  ├─ actionCost = action.energyCost ?? 1
  ├─ if actionCost > remainingEnergy:
  │   └─ getFallbackAction(remaining) → "基本攻撃" if >= 1 energy
  ├─ Push action, subtract energy
  ↓
Execute collected actions sequentially:
  ├─ checkBattleEnd() before each action
  ├─ onExecuteAction(action) → async callback
  ├─ 800ms delay between actions
  └─ checkBattleEnd() after each action
```

### Enemy Phase Flow

```
Enemy phase start:
  calculateEnemyPhaseStart(enemy, enemyBuffs)
    ├─ decreaseBuffDebuffDurationForPhase(buffs, 'enemy')
    ├─ calculateStartPhaseHealing(buffs) → hp/shield amounts
    ├─ guardReset: startingGuard ? floor(baseMaxAp * GUARD_INIT_MULTIPLIER) : 0
    ├─ energyReset: enemy.actEnergy (full refill)
    └─ canPerformAction: canAct(buffs) — stun check

Enemy attack:
  calculateEnemyAttackDamage(attacker, defender, action)
    ├─ Guard-only → all zeros
    ├─ enemyAction(action) → converts to Card format
    ├─ calculateDamage(attacker, defender, card) → finalDamage, isCritical, reflectDamage
    └─ applyDamageAllocation(defender, damage) → guard/ap/hp split

Enemy phase end:
  calculateEnemyPhaseEnd(buffs)
    └─ calculateEndPhaseDamage(buffs) → DoT damage (burn/poison)
```

### Enemy Action → Card Conversion

```
enemyAction(action) → Card
  ├─ id: "enemy_action_{name}"
  ├─ tags: baseDamage > 0 ? ["attack"] : ["skill"]
  ├─ element: ["physics"]  (always physical)
  ├─ baseDamage: action.baseDamage
  └─ applyEnemyDebuff: mapped from action.applyDebuffs
```

## Key Details

- **Energy system**: Each enemy has `actEnergy` (e.g., 2-3). Each action costs `energyCost` (default 1). An enemy with 3 energy can attack 3 times per turn.
- **Fallback action**: If remaining energy can't afford the AI-selected action, a basic 5-damage attack is used (costs 1 energy).
- **Guard reset**: Enemies with `startingGuard: true` get their guard restored to `floor(baseMaxAp * GUARD_INIT_MULTIPLIER)` every phase start.
- **Multi-enemy encounters**: `selectRandomEnemy` returns arrays of 1-3 enemies. Phase queue expands enemy slots per alive enemy.
- **Boss enemies**: One per depth, selected via `"boss"` encounter size. Higher stats and more complex AI patterns.
- **Instance IDs**: Generated as `${definitionId}_${Date.now()}_${random}` for uniqueness in multi-enemy battles.
- **`_remainingEnergy` parameter**: Passed to `determineEnemyAction` but prefixed with underscore (unused in pattern selection). Energy management happens in the execution loop, not the AI selection.

## Dependencies

```
enemyAI.ts
  ├─ DEPTH1-5_ENEMIES (enemy definitions)
  └─ characterTypes (EnemyDefinition, EnemyAIPattern, EnemyAction)

enemyActionExecution.ts
  └─ enemyAI.determineEnemyAction

enemyPhaseExecution.ts
  ├─ buffCalculation (healing, DoT, canAct)
  ├─ buffLogic (duration decrease, add/update)
  ├─ damageCalculation (calculateDamage, applyDamageAllocation)
  ├─ bleedDamage (calculateBleedDamage)
  └─ enemyAI.enemyAction (Card converter)

useEnemyAI.ts (hook)
  ├─ enemyAI (determineEnemyAction)
  ├─ enemyActionExecution (executeEnemyActions, previewEnemyActions)
  ├─ enemyPhaseExecution (calculateEnemyAttackDamage, applyEnemyDebuffsToPlayer)
  └─ bleedDamage (calculateBleedDamage)

enemyStateLogic.ts
  ├─ enemyUtils (generateEnemyInstanceId)
  ├─ characterUtils (createEmptyBuffDebuffMap)
  └─ GUARD_INIT_MULTIPLIER constant
```

## Vulnerability Analysis

### `[BUG-RISK]` _remainingEnergy Ignored in AI Selection

**Location:** `enemyAI.ts:23`

`determineEnemyAction` receives `_remainingEnergy` but ignores it. The AI may select a high-cost action that the execution loop then can't afford, forcing a fallback. The AI doesn't consider remaining energy when selecting patterns, which means expensive actions are selected and then discarded.

### `[BUG-RISK]` previewEnemyActions Uses Random AI Selection

**Location:** `enemyActionExecution.ts:81-114`

`previewEnemyActions()` calls `determineEnemyAction()` which uses `Math.random()` internally. The preview shown in UI may differ from the actual actions executed, since each call produces different random results. Players see one preview but the enemy does something different.

### `[BUG-RISK]` enemyAction Converter Always Sets element to ["physics"]

**Location:** `enemyAI.ts:71`

All enemy actions are converted to cards with `element: ["physics"]`. Enemies cannot deal elemental damage (fire, ice, etc.) through the standard damage pipeline. Enemy debuffs like burn are applied via `applyDebuffs`, not via elemental damage calculation.

### `[BUG-RISK]` Enemy HP Used for Both AI and Bleed in Same Turn

**Location:** `useEnemyAI.ts:233-241`

In `executeAllActions`, bleed damage is calculated using `enemyMaxHp` and `enemyBuffs` from the action context (captured at start of turn). If the enemy's HP changed during multi-hit execution, the bleed calculation still uses the original values. Additionally, bleed is checked after every single action, not once per turn.

### `[EXTENSIBILITY]` useEnemyAI Hook Is Stateless Wrapper

**Location:** `useEnemyAI.ts:133`

`useEnemyAI()` wraps pure functions in `useCallback` but holds no state. All methods receive enemy state as parameters. This hook exists mainly for React convention (called unconditionally in orchestrator) but adds no state management value.

### `[QUALITY]` Duplicate Guard-Only Check

**Location:** `enemyPhaseExecution.ts:141-150` and `useEnemyAI.ts:177-179`

Guard-only action detection (`action.guardGain > 0 && !action.baseDamage`) is checked both in `calculateEnemyAttackDamage()` and in `useEnemyAI.executeAllActions()`. The same logic path is duplicated across two files.

### `[BUG-RISK]` Fallback Action Hardcoded Outside Enemy Data

**Location:** `enemyActionExecution.ts:65-77`

`getFallbackAction()` returns a hardcoded "基本攻撃" with 5 base damage. This isn't defined in any enemy's AI patterns and doesn't scale with depth. A Depth 5 enemy using the fallback deals the same 5 damage as a Depth 1 enemy.
