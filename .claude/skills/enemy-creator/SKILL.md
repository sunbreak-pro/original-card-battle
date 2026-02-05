---
name: enemy-creator
description: Add new enemies to the card battle game. Covers enemy definition data, AI pattern design, and depth-specific data file updates. Use for "add enemy", "create new monster", "implement boss" requests. 使用時: 「enemy-creatorを使用します」
---

# Enemy Creator Skill

Workflow for adding new enemies to the game.

## Enemy Creation Workflow

1. **Confirm enemy specifications**
   - Depth (1-5): Which depth the enemy appears at
   - Type: Regular enemy / Boss
   - AI complexity: Simple pattern / Complex pattern

2. **Reference type definitions** (`src/domain/characters/type/enemyType.ts`)

3. **Add to depth-specific data file**
   - `src/domain/characters/enemy/data/enemyDepth1.ts`
   - `src/domain/characters/enemy/data/enemyDepth2.ts`
   - `src/domain/characters/enemy/data/enemyDepth3.ts`
   - `src/domain/characters/enemy/data/enemyDepth4.ts`
   - `src/domain/characters/enemy/data/enemyDepth5.ts` (Boss)

## EnemyDefinition Interface

```typescript
interface EnemyDefinition {
  id: string,              // Unique ID
  name: string,            // English name
  nameJa: string,          // Japanese name
  description: string,     // Description

  // Base stats
  baseMaxHp: number,
  baseMaxAp: number,       // Defense/armor
  baseSpeed: number,
  startingGuard: boolean,

  // AI
  actEnergy: number,       // Action energy per turn
  aiPatterns: EnemyAIPattern[],

  // Display
  imagePath?: string,
}
```

## EnemyAction Types

```typescript
type EnemyActionType = "attack" | "buff" | "debuff" | "special";

interface EnemyAction {
  name: string,
  type: EnemyActionType,
  baseDamage: number,      // 0 for non-attack actions
  applyDebuffs?: BuffDebuffState[],
  applyBuffs?: BuffDebuffState[],
  guardGain?: number,
  hitCount?: number,       // Default: 1
  displayIcon?: string,
  priority?: number,
  energyCost?: number,
}
```

## AI Pattern Design

```typescript
interface EnemyAIPattern {
  phaseNumber: number,     // Action phase number
  condition?: (hp: number, maxHp: number) => boolean,
  action: EnemyAction,
  probability?: number,    // 0-1 (default: 1)
}
```

### Pattern Examples

**Simple attack loop:**
```typescript
aiPatterns: [
  { phaseNumber: 1, action: attackAction },
  { phaseNumber: 2, action: attackAction },
]
```

**HP-conditional action:**
```typescript
aiPatterns: [
  {
    phaseNumber: 1,
    condition: (hp, maxHp) => hp < maxHp * 0.5,
    action: enrageAction,  // Triggers below 50% HP
  },
  { phaseNumber: 1, action: normalAttack },
]
```

**Probability-based action:**
```typescript
aiPatterns: [
  { phaseNumber: 1, action: heavyAttack, probability: 0.3 },
  { phaseNumber: 1, action: normalAttack, probability: 0.7 },
]
```

## Example: New Depth 2 Enemy

```typescript
// Add to enemyDepth2.ts
export const DEPTH2_ENEMIES: EnemyDefinition[] = [
  // ... existing enemies

  {
    id: "shadow_wolf",
    name: "Shadow Wolf",
    nameJa: "影狼",
    description: "闇から生まれた凶暴な獣。素早い連続攻撃が得意。",
    baseMaxHp: 45,
    baseMaxAp: 8,
    baseSpeed: 70,
    startingGuard: false,
    actEnergy: 2,
    aiPatterns: [
      {
        phaseNumber: 1,
        action: {
          name: "噛みつき",
          type: "attack",
          baseDamage: 8,
          displayIcon: "🐺",
        },
      },
      {
        phaseNumber: 2,
        action: {
          name: "連続噛みつき",
          type: "attack",
          baseDamage: 5,
          hitCount: 2,
          displayIcon: "🐺",
        },
      },
    ],
  },
];
```

## Stat Guidelines by Depth

| Depth | HP | AP | Speed | Characteristics |
|-------|-----|-----|-------|-----------------|
| 1 | 20-40 | 0-5 | 30-50 | Simple AI |
| 2 | 35-60 | 5-10 | 40-60 | Conditional actions |
| 3 | 50-90 | 10-20 | 50-70 | Compound actions |
| 4 | 80-150 | 15-30 | 60-80 | High difficulty AI |
| 5 | 200+ | 30+ | 70+ | Boss only |

## Design Doc Reference

Enemy design docs: `.claude/docs/enemy_document/`
- `depth1_enemy_database.md` ~ `depth4_enemy_database.md`
- `depth5_boss_database.md`
- `boss_system_redesign.md`
