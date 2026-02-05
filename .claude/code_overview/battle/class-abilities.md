# Class Ability System

## Overview

Polymorphic class ability framework using `ClassAbilitySystem<T>` interface, with two implementations: Swordsman (sword energy gauge + bleed) and Mage (elemental resonance chain + field buffs). Each integrates via React hooks into the battle orchestrator's damage pipeline through `DamageModifier`.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/domain/characters/player/classAbility/classAbilitySystem.ts` | ~137 | Interface definition, DamageModifier type, combine/apply helpers |
| `src/domain/characters/player/logic/swordEnergySystem.ts` | ~261 | Swordsman: energy gauge, bleed chance, consume/add functions |
| `src/domain/characters/player/logic/elementalSystem.ts` | ~250 | Mage: resonance chain, element-specific effects, field buffs |
| `src/domain/characters/player/classAbility/classAbilityUtils.ts` | ~89 | Factory functions, type guards, initial state creators |
| `src/domain/battles/managements/useClassAbility.ts` | ~280 | React hooks: useSwordEnergy, useClassAbility<T>, factory |
| `src/domain/battles/managements/useElementalChain.ts` | ~86 | React hook wrapper for ElementalSystem |
| `src/constants/characterConstants.ts` | ~126 | SWORD_ENERGY_MAX, RESONANCE_MULTIPLIER, RESONANCE_EFFECTS, etc. |
| `src/constants/battleConstants.ts` | ~97 | SWORD_ENERGY_BLEED_CHANCE_*, bleed duration/stacks |
| `src/types/characterTypes.ts` | ~344 | SwordEnergyState, ElementalState, ClassAbilityState union |

## Data Structures

### ClassAbilitySystem<T> Interface

```typescript
interface ClassAbilitySystem<T extends ClassAbilityState> {
  initialize(): T;
  onCardPlay(state: T, card: Card): T;
  onTurnStart(state: T): T;
  onTurnEnd(state: T): T;
  getDamageModifier(state: T, card?: Card): DamageModifier;
  canPerformAction(state: T, actionId: string): boolean;
  getStateDescription(state: T): string;
}
```

### DamageModifier

```typescript
interface DamageModifier {
  flatBonus: number;         // Added to base damage
  percentMultiplier: number; // 1.0 = 100%
  critBonus: number;         // 0.0 - 1.0
  penetration: number;       // 0.0 - 1.0 (bypasses armor)
}

DEFAULT_DAMAGE_MODIFIER = { flatBonus: 0, percentMultiplier: 1.0, critBonus: 0, penetration: 0 }
```

### SwordEnergyState

```typescript
interface SwordEnergyState {
  type: "swordEnergy";
  current: number;  // 0-10
  max: number;      // 10
}
```

### ElementalState

```typescript
interface ElementalState {
  type: "elemental";
  lastElement: ElementType | null;   // Current chain element
  resonanceLevel: ResonanceLevel;    // 0 | 1 | 2
}
```


### ResonanceEffectConfig

```typescript
interface ResonanceEffectConfig {
  burn?: { stacks: number; duration: number };
  freeze?: { duration: number };
  stun?: { duration: number };
  lifesteal?: number;
  cleanse?: number;
  heal?: number;
  weakness?: { duration: number };
  fieldBuff?: BuffDebuffType;
}
```

### Key Constants

```
SWORD_ENERGY_MAX = 10
SWORD_ENERGY_BLEED_CHANCE_5  = 0.2  (energy ≥ 5)
SWORD_ENERGY_BLEED_CHANCE_8  = 0.4  (energy ≥ 8)
SWORD_ENERGY_BLEED_CHANCE_10 = 0.6  (energy = 10)
SWORD_ENERGY_BLEED_DURATION  = 3
SWORD_ENERGY_BLEED_STACKS    = 1

MAX_RESONANCE_LEVEL = 2
RESONANCE_MULTIPLIER = { 0: 1.0, 1: 1.15, 2: 1.30 }
MAGIC_ELEMENTS = { fire, ice, lightning, dark, light }

```

## Logic Flow

### Swordsman: Sword Energy System

```
INITIALIZATION:
  { type: "swordEnergy", current: 0, max: 10 }

ON CARD PLAY:
  IF class !== "swordsman" AND class !== "common" → no change
  ├─ swordEnergyConsume > 0 → current -= min(current, consume)
  ├─ swordEnergyConsume === 0 → current = 0 (consume all)
  └─ ELSE: energyGain = card.swordEnergyGain ?? calculateGain(cost)
       calculateGain:
         swordEnergy category → +4
         cost 0 → +1
         cost 1 → +1
         cost 2 → +2
         cost 3+ → +3
       current = min(max, current + energyGain)

ON TURN START: no change (energy persists)
ON TURN END: no change (energy persists)

GET DAMAGE MODIFIER:
  { flatBonus: current, percentMultiplier: 1.0, critBonus: 0, penetration: 0 }
  (Every 1 sword energy = +1 flat damage)

BLEED CHANCE (checked in useCardExecution after damage):
  energy ≥ 10: 60% chance
  energy ≥ 8:  40% chance
  energy ≥ 5:  20% chance
  energy < 5:  0%
  → Applied: bleed(duration=3, stacks=1, appliedBy='player')
```

### Mage: Elemental Resonance System

```
INITIALIZATION:
  { type: "elemental", lastElement: null, resonanceLevel: 0 }

ON CARD PLAY:
  magicElement = card.element.find(e => MAGIC_ELEMENTS.has(e))
  IF no magic element → reset { lastElement: null, resonanceLevel: 0 }
  IF magicElement === lastElement → resonanceLevel = min(2, level + 1)
  IF magicElement !== lastElement → { lastElement: magicElement, resonanceLevel: 0 }

ON TURN START: no change
ON TURN END: resonanceLevel = 0 (reset each turn)

GET DAMAGE MODIFIER:
  IF card doesn't match current element → DEFAULT (1.0×)
  Level 0: { percentMultiplier: 1.0 }
  Level 1: { percentMultiplier: 1.15 }
  Level 2: { percentMultiplier: 1.30, critBonus: 0.10 }

RESONANCE EFFECTS (from RESONANCE_EFFECTS table):
  Fire:
    Lv1: burn(stacks=1, dur=2)
    Lv2: burn(stacks=2, dur=3) + fireField
  Ice:
    Lv1: freeze(dur=2)
    Lv2: freeze(dur=3) + iceField
  Lightning:
    Lv1: (none)
    Lv2: stun(dur=1) + electroField
  Dark:
    Lv1: lifesteal 30
    Lv2: weakness(dur=3), lifesteal 40 + darkField
  Light:
    Lv1: cleanse 1
    Lv2: cleanse 2, heal 10 + lightField
  Physics/Guard/Summon/Enhance/Sacrifice/Buff/Debuff/Heal:
    Lv1: (none)
    Lv2: (none)

SANCTUARY ENHANCEMENT (via enhancedElements parameter):
  Fire: burn stacks +1
  Ice: freeze duration +1
  Lightning: adds stun(dur=1) at Lv1
  Dark: lifesteal +10
  Light: cleanse +1, heal +5
```


### Integration with Battle Orchestrator

```
useBattleOrchestrator
  ├─ useSwordEnergy()     — always called (React hooks rules)
  └─ useElementalChain()  — always called

  Based on player.playerClass:
    "swordsman" → use swordEnergy.getDamageModifier
    "mage"      → use elementalChain.getDamageModifier

  getDamageModifier → passed to useCardExecution via setters.getElementalDamageModifier
    → Applied in executeCard: adjustedBase = round(base × modifier.percentMultiplier)
```

### React Hook Pattern

```
useSwordEnergy / useElementalChain
  ├─ useState(System.initialize())
  ├─ onCardPlayed(card) → setState(prev => System.onCardPlay(prev, card))
  ├─ onTurnStart()      → setState(prev => System.onTurnStart(prev))
  ├─ onTurnEnd()        → setState(prev => System.onTurnEnd(prev))
  ├─ getDamageModifier(card) → System.getDamageModifier(state, card)
  ├─ canPerformAction(id)    → System.canPerformAction(state, id)
  ├─ getAbilityUI()          → { label, current, max, level, description }
  └─ resetAbility()          → setState(System.initialize())

useClassAbility<T>(system, getLabel, getLevel)
  Generic version that works with any ClassAbilitySystem<T>

createClassAbilityHook(characterClass)
  Factory → returns appropriate typed hook function
```

## Dependencies

```
classAbilitySystem.ts (interface + helpers)
  └─ standalone (only Card type import)

swordEnergySystem.ts
  ├─ classAbilitySystem.ts (ClassAbilitySystem, DamageModifier)
  ├─ classAbilityUtils.ts (SWORD_ENERGY_MAX, createInitialSwordEnergy)
  └─ battleConstants.ts (SWORD_ENERGY_BLEED_CHANCE_*)

elementalSystem.ts
  ├─ classAbilitySystem.ts (ClassAbilitySystem, DamageModifier, DEFAULT_DAMAGE_MODIFIER)
  ├─ classAbilityUtils.ts (createInitialElemental)
  ├─ characterConstants.ts (MAX_RESONANCE_LEVEL, RESONANCE_MULTIPLIER, RESONANCE_EFFECTS)
  └─ cardConstants.ts (MAGIC_ELEMENTS)

useClassAbility.ts
  ├─ swordEnergySystem.ts (SwordEnergySystem, createInitialSwordEnergy)
  ├─ useElementalChain.ts
  └─ classAbilitySystem.ts (DEFAULT_DAMAGE_MODIFIER)

useElementalChain.ts
  └─ elementalSystem.ts (ElementalSystem)

classAbilityUtils.ts
  └─ characterTypes.ts (all state types)

Integration:
  useCardExecution ← setters.getElementalDamageModifier (from active class hook)
  useBattleOrchestrator ← both class hooks called unconditionally
```

## Vulnerability Analysis

### `[BUG-RISK]` Sword Energy Flat Bonus Applied Before Consumption

**Location:** `useCardExecution.ts:320` + `swordEnergySystem.ts:111-117`

The sword energy system's `getDamageModifier` returns `flatBonus: current` (the full current energy). In `executeCard`, the sword energy consumption (lines 287-312) happens via `setSwordEnergy` (async state update), but the damage bonus reads from the closure value `swordEnergy.current` (line 320). A card that consumes sword energy AND deals damage uses the pre-consumption energy as a damage bonus. This is functionally correct (consume and unleash), but the ordering dependency is implicit and fragile.

### `[BUG-RISK]` Elemental Resonance Resets on Non-Magic Cards

**Location:** `elementalSystem.ts:47-57`

```typescript
const magicElement = card.element.find(e => MAGIC_ELEMENTS.has(e));
if (!magicElement) {
  return { ...state, lastElement: null, resonanceLevel: 0 };
}
```

Playing any non-magic card (physics, guard, buff, heal, etc.) completely resets the resonance chain. In a Mage deck, playing a healing or buff card mid-chain breaks the resonance. This makes it very difficult to maintain chains in mixed decks and incentivizes pure-element decks, which may be intentional but feels punishing for utility cards.

### `[BUG-RISK]` Resonance Effects Not Actually Applied

**Location:** `elementalSystem.ts:170-218` and `useCardExecution.ts:326-329`

The `getResonanceEffects` function returns burn, freeze, stun, lifesteal, heal, and field buff configs based on resonance level. However, in `useCardExecution`, only `getDamageModifier` is called (which returns just the multiplier). The resonance effects (debuff application, field buff creation) are never applied during card execution. The `getResonanceEffects` function exists but has no caller in the battle execution path.

### `[BUG-RISK]` DamageModifier.critBonus and penetration Are Never Used

**Location:** `classAbilitySystem.ts:22-30`

```typescript
interface DamageModifier {
  flatBonus: number;
  percentMultiplier: number;
  critBonus: number;      // Set by ElementalSystem at Lv2 (0.10)
  penetration: number;    // Never set > 0 by any system
}
```

The `DamageModifier` interface includes `critBonus` and `penetration` fields. `ElementalSystem.getDamageModifier` sets `critBonus: 0.10` at resonance level 2. However, `useCardExecution` only reads `percentMultiplier` from the modifier (line 328). Neither `critBonus` nor `penetration` is checked anywhere in the damage pipeline. The `isCritical` field in `DamageResult` is always `false`.

### `[BUG-RISK]` combineDamageModifiers Multiplies percentMultiplier

**Location:** `classAbilitySystem.ts:119-129`

```typescript
percentMultiplier: combined.percentMultiplier * modifier.percentMultiplier,
```

`combineDamageModifiers` multiplies percent multipliers together. If a Swordsman with `percentMultiplier: 1.0` and equipment buff of `1.2` are combined: `1.0 × 1.2 = 1.2` (correct). But if two +30% bonuses combine: `1.3 × 1.3 = 1.69` (+69% total, not +60%). This is standard multiplicative stacking, but may surprise designers expecting additive stacking. Currently only one modifier source is used per execution.


### `[EXTENSIBILITY]` Resonance Effects Table Has Empty Entries

**Location:** `characterConstants.ts:93-124`

```typescript
physics: { 1: {}, 2: {} },
guard: { 1: {}, 2: {} },
summon: { 1: {}, 2: {} },
// ... 7 more empty entries
```

The `RESONANCE_EFFECTS` table defines entries for all 13 `ElementType` values, but only 5 (fire, ice, lightning, dark, light) have actual effects. The remaining 8 have empty objects. These placeholders exist for type safety but create maintenance noise. A smaller `Partial<Record<ElementType, ...>>` would be cleaner.

### `[EXTENSIBILITY]` Two Identical Hook Wrappers

**Location:** `useClassAbility.ts`, `useElementalChain.ts`

Both class-specific hooks (`useSwordEnergy`, `useElementalChain`) follow an identical pattern: `useState` + 7 `useCallback` wrappers around the system interface. The generic `useClassAbility<T>` function exists but isn't used by any of them. Each hook is ~80 lines of boilerplate. A single generic hook used consistently would reduce duplication.

### `[QUALITY]` SWORD_ENERGY_MAX Defined in Two Places

**Location:** `classAbilityUtils.ts:15` and `characterConstants.ts:30`

```typescript
// classAbilityUtils.ts
export const SWORD_ENERGY_MAX = 10;

// characterConstants.ts
export const SWORD_ENERGY_MAX = 10;
```

`SWORD_ENERGY_MAX` is exported from both `classAbilityUtils.ts` and `characterConstants.ts`. `swordEnergySystem.ts` re-exports from `classAbilityUtils`. Consumers import from different sources — a value change in one file won't propagate to the other. Both are currently `10` but this is a maintenance hazard.

### `[QUALITY]` DEFAULT_DAMAGE_MODIFIER Also Defined in Two Places

**Location:** `classAbilitySystem.ts:35-40` and `characterConstants.ts:37-42`

```typescript
// classAbilitySystem.ts
export const DEFAULT_DAMAGE_MODIFIER: DamageModifier = { ... };

// characterConstants.ts
export const DEFAULT_DAMAGE_MODIFIER: DamageModifier = { ... };
```

Same duplication issue as `SWORD_ENERGY_MAX`. Both define identical default modifiers. `useClassAbility.ts` re-exports from `classAbilitySystem.ts`. If either changes independently, subtle damage calculation differences will occur.
