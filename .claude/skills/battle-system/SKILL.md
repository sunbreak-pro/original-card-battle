---
name: battle-system
description: Add or modify battle system features in the card battle game. Covers buff/debuff systems, damage calculation, phase execution, and card effect implementation. Use for "add buff", "modify damage calculation", "new battle feature" requests.
---

# Battle System Skill

Workflow for adding or modifying battle system features.

## Battle System Architecture

```
BattleScreen
  └── useBattleOrchestrator
        ├── useBattleState (state management)
        ├── useDeckManage (deck operations)
        ├── useBattlePhase (turn/phase control)
        └── useCardExecution (card play logic)
              ├── damageCalculation
              ├── buffCalculation
              └── phaseExecution
```

## Key Files

| Domain | File Path |
|--------|-----------|
| Buff/Debuff Types | `src/domain/battles/type/baffType.ts` |
| Buff Data | `src/domain/battles/data/buffData.ts` |
| Buff Logic | `src/domain/battles/logic/buffLogic.ts` |
| Buff Calculation | `src/domain/battles/calculators/buffCalculation.ts` |
| Damage Calculation | `src/domain/battles/calculators/damageCalculation.ts` |
| Phase Execution | `src/domain/battles/execution/` |

## Adding a New Buff/Debuff

### Step 1: Add to BuffType

```typescript
// src/domain/battles/type/baffType.ts
export type BuffType =
  | "strength"
  | "vulnerable"
  // ... existing types
  | "newbuff";  // Add here
```

### Step 2: Add Buff Data

```typescript
// src/domain/battles/data/buffData.ts
export const BUFF_DATA: Record<BuffType, BuffDefinition> = {
  // ... existing buffs
  newbuff: {
    id: "newbuff",
    name: "新バフ",
    description: "効果の説明",
    type: "buff",  // "buff" | "debuff"
    stackable: true,
    maxStacks: 5,
    icon: "🔥",
  },
};
```

### Step 3: Implement Effect Logic

```typescript
// src/domain/battles/calculators/buffCalculation.ts
// Add case to applyBuffEffects or relevant calculation
```

## Buff Ownership System

```typescript
interface BuffDebuffState {
  type: BuffType;
  value: number;
  duration: number;
  appliedBy: "player" | "enemy" | "environment";  // IMPORTANT
}
```

**Rule:** Duration decreases only during the applier's phase.
- Player-applied buffs → decrease on player phase end
- Enemy-applied debuffs → decrease on enemy phase end

## Damage Calculation Flow

```typescript
// src/domain/battles/calculators/damageCalculation.ts

1. getBaseDamage(card)
2. applyStrengthBuff(damage, buffs)
3. applyWeaknessDebuff(damage, buffs)
4. applyVulnerability(damage, targetBuffs)
5. applyArmorPenetration(damage, penetration)
6. applyGuard(damage, targetGuard)
7. applyAP(damage, targetAP)
8. finalDamage = max(1, damage)
```

## Phase Execution

```typescript
// Player Phase
playerPhaseExecution.ts:
  1. drawCards()
  2. restoreEnergy()
  3. decreaseBuffDurations("player")
  4. waitForPlayerInput()

// Enemy Phase
enemyPhaseExecution.ts:
  1. selectAction(aiPatterns)
  2. executeAction()
  3. decreaseBuffDurations("enemy")
```

## Adding Card Effect

```typescript
// src/domain/cards/state/cardPlayLogic.ts
export function executeCardEffect(card: Card, state: BattleState): BattleState {
  let newState = { ...state };

  if (card.baseDamage) {
    newState = applyDamage(newState, card.baseDamage);
  }

  if (card.guardAmount) {
    newState = addGuard(newState, card.guardAmount);
  }

  // New effect
  if (card.newEffect) {
    newState = applyNewEffect(newState, card.newEffect);
  }

  return newState;
}
```

## Common Buff Types

| Type | Effect | Stackable |
|------|--------|-----------|
| strength | +damage dealt | Yes |
| vulnerable | +damage taken | Yes |
| weak | -damage dealt | Yes |
| guard | Block damage | No |
| bleed | DoT damage | Yes |
| poison | DoT damage | Yes |
| regeneration | HoT | Yes |

## Testing Checklist

- [ ] Buff applies correctly
- [ ] Duration decreases properly
- [ ] Stacking works as expected
- [ ] Effect calculation is correct
- [ ] UI displays the buff
- [ ] Buff removal on duration 0

## Design Doc Reference

Battle design docs: `.claude/docs/battle_document/`
- `battle_logic.md`
- `buff_debuff_system.md`
