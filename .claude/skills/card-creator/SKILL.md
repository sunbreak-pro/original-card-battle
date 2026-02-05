---
name: card-creator
description: Add new cards to the card battle game. Covers card data creation, type definition compliance, class-specific card file updates, and initial deck registration. Use for "add card", "create new card", "implement X card" requests. 使用時: 「card-creatorを使用します」
---

# Card Creator Skill

Workflow for adding new cards to the game.

## Card Creation Workflow

1. **Confirm card specifications**
   - Class: swordsman / mage / summoner / common
   - Category: atk / def / buff / debuff / heal / swordEnergy
   - Rarity: common / rare / epic / legend
   - Tags: attack / guard / skill / stance

2. **Reference type definitions** (`src/domain/cards/type/cardType.ts`)

3. **Add to class-specific card file**
   - `src/domain/cards/data/SwordmanCards.ts`
   - `src/domain/cards/data/mageCards.ts`
   - `src/domain/cards/data/summonerCards.ts`

4. **(Optional) Register in initial deck** (`src/domain/battles/data/initialDeckConfig.ts`)

## Card Interface (Required Fields)

```typescript
{
  id: string,              // Unique ID (no duplicates in file)
  cardTypeId: string,      // Card type ID (same as id)
  name: string,            // Display name (Japanese)
  description: string,     // Effect description (Japanese)
  characterClass: CardCharacterClass,
  cost: number,            // Energy cost (0-5)
  category: CardCategory,
  rarity: Rarity,
  useCount: 0,
  masteryLevel: 0,
  gemLevel: 0,
  tags: CardTag[],
  // Effects (optional)
  baseDamage?: number,
  hitCount?: number,
  penetration?: number,
  healAmount?: number,
  guardAmount?: number,
  drawCards?: number,
  energyGain?: number,
  applyEnemyDebuff?: CardBuffSpec[],
  applyPlayerBuff?: CardBuffSpec[],
  // Class-specific (optional)
  swordEnergyGain?: number,      // Swordsman
  swordEnergyConsume?: number,   // Swordsman
  element?: ElementType,          // Mage
  summonId?: string,             // Summoner
}
```

## Tag Assignment Rules

- `attack`: baseDamage > 0
- `guard`: guardAmount > 0 AND baseDamage === 0
- `skill`: Buff/debuff/utility (draw, energy, heal)
- `stance`: duration >= 99 OR isPermanent: true

## Example: New Swordsman Card

```typescript
// Add to SwordmanCards.ts
export const SWORDSMAN_CARDS: Record<string, Card> = {
  // ... existing cards

  "sword_new_attack": {
    id: "sword_new_attack",
    cardTypeId: "sword_new_attack",
    name: "新しい斬撃",
    description: "敵に15ダメージを与える。剣気+1",
    characterClass: "swordsman",
    cost: 1,
    category: "atk",
    rarity: "common",
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
    tags: ["attack"],
    baseDamage: 15,
    swordEnergyGain: 1,
  },
};
```

## BuffSpec Format

```typescript
applyEnemyDebuff: [
  {
    type: "vulnerable",  // Buff/debuff type
    value: 25,           // Effect value
    duration: 2,         // Turn count
  }
],
```

## Balance Guidelines

| Rarity | Cost Range | Damage Range | Effects |
|--------|------------|--------------|---------|
| common | 0-2 | 8-15 | Simple single effect |
| rare | 1-3 | 15-25 | Compound effects |
| epic | 2-4 | 25-40 | Powerful + conditional |
| legend | 3-5 | 40+ | Game-changing |

## Design Doc Reference

Card design docs: `.claude/docs/card_document/`
- `SWORDSMAN_CARDS_40.md`
- `MAGE_CARDS_40.md`
- `SUMMONER_CARDS_40.md`
- `COMMON_CARDS_20.md`
