---
name: character-class-creator
description: Add new character classes to the card battle game. Covers class data definition, initial deck configuration, and class-specific mechanic implementation. Use for "add new class", "implement character" requests.
---

# Character Class Creator Skill

Workflow for adding new character classes to the game.

## Class Creation Workflow

1. **Confirm class design**
   - Class name (English/Japanese)
   - Unique mechanic
   - Base stats
   - Theme color

2. **Update the following files**
   - `src/domain/characters/type/baseTypes.ts` - Add to CharacterClass type
   - `src/domain/cards/data/{className}Cards.ts` - Create class-specific cards
   - `src/domain/battles/data/initialDeckConfig.ts` - Initial deck setup
   - `src/domain/characters/player/data/CharacterClassData.ts` - Class info
   - `src/domain/battles/managements/useBattleOrchestrator.ts` - getCardDataByClass

3. **(If unique mechanic exists)**
   - `src/domain/characters/player/logic/{mechanic}System.ts`
   - `src/domain/characters/type/classAbilityTypes.ts`

## Step-by-Step Guide

### Step 1: Add to CharacterClass Type

```typescript
// src/domain/characters/type/baseTypes.ts
export type CharacterClass = "swordsman" | "mage" | "summoner" | "newclass";
```

### Step 2: Create Card Data File

```typescript
// src/domain/cards/data/newclassCards.ts
import type { Card } from "../type/cardType";

export const NEWCLASS_CARDS: Record<string, Card> = {
  "newclass_basic_attack": {
    id: "newclass_basic_attack",
    cardTypeId: "newclass_basic_attack",
    name: "基本攻撃",
    description: "敵に10ダメージを与える",
    characterClass: "newclass", // Also update CardCharacterClass type
    cost: 1,
    category: "atk",
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    tags: ["attack"],
    baseDamage: 10,
  },
  // ... more cards (minimum 15 for starter deck)
};
```

### Step 3: Configure Initial Deck

```typescript
// src/domain/battles/data/initialDeckConfig.ts
export const INITIAL_DECK_BY_CLASS: Record<CharacterClass, Record<string, number>> = {
  // ... existing classes
  newclass: {
    "newclass_basic_attack": 4,
    "newclass_basic_defend": 4,
    // ... total ~15 cards
  },
};
```

### Step 4: Add Class Info

```typescript
// src/domain/characters/player/data/CharacterClassData.ts

// Import card data
import { NEWCLASS_CARDS } from "../../../cards/data/newclassCards";

// Add starter deck creator
function createNewclassStarterDeck(): Card[] {
  return createStarterDeckFromCounts(
    INITIAL_DECK_BY_CLASS.newclass,
    NEWCLASS_CARDS
  );
}

// Add to CHARACTER_CLASS_DATA
export const CHARACTER_CLASS_DATA: Record<CharacterClass, CharacterClassInfo> = {
  // ... existing classes
  newclass: {
    class: "newclass",
    name: "NewClass",
    japaneseName: "新クラス",
    description: "クラスの説明文...",
    uniqueMechanic: "Mechanic Name",
    mechanicDescription: "メカニクスの説明...",
    stats: {
      hp: 80,
      ap: 25,
      speed: 50,
      cardActEnergy: 3,
    },
    starterDeck: createNewclassStarterDeck(),
    isAvailable: true,  // false to hide during development
    themeColor: "#8b5cf6",
    icon: "icon-name",
  },
};
```

### Step 5: Update getCardDataByClass

```typescript
// src/domain/battles/managements/useBattleOrchestrator.ts
function getCardDataByClass(classType: CharacterClass): Record<string, Card> {
  switch (classType) {
    // ... existing cases
    case "newclass":
      return NEWCLASS_CARDS;
    default:
      return SWORDSMAN_CARDS;
  }
}
```

## Existing Class Mechanics Reference

| Class | Mechanic | System File |
|-------|----------|-------------|
| Swordsman | Sword Energy | `swordEnergySystem.ts` |
| Mage | Elemental Resonance | `elementalSystem.ts` |
| Summoner | Summon System | `summonSystem.ts` |

## Stat Guidelines

| Stat | Low | Medium | High |
|------|-----|--------|------|
| HP | 60 | 80 | 100 |
| AP | 20 | 25 | 30 |
| Speed | 40 | 50 | 60 |
| cardActEnergy | 3 | 3 | 3 |

## Important Notes

- **DO NOT MODIFY** `deck.ts` and `deckReducter.ts` (immutable)
- `CardCharacterClass` type also needs updating
- Use `isAvailable: false` to hide during testing
- Create minimum 15 cards for initial deck

## Design Doc Reference

Character design: `.claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md`
