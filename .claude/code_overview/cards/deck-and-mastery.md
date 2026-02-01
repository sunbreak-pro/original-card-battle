# Card, Deck & Mastery System

## Overview

The card system defines ~120 cards across three classes (Swordsman 40, Mage 40, Summoner 40) with per-card stats (damage, cost, element, buffs/debuffs). Decks are managed via an immutable `deckReducer` (draw/discard/shuffle) with a `DeckState` tracking drawPile, discardPile, and hand. Card mastery increases with use, providing stat bonuses through `cardDerivation`. The `deck.ts` and `deckReducter.ts` files are marked IMMUTABLE and must not be modified.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/types/cardTypes.ts` | 138 | Card interface, CardCategory, CardTag, Depth, Rarity |
| `src/domain/cards/decks/deck.ts` | 106 | **IMMUTABLE** — createInitialDeck, getCardDataByClass |
| `src/domain/cards/decks/deckReducter.ts` | 66 | **IMMUTABLE** — deckReducer (draw/discard/shuffle/addToHand) |
| `src/domain/cards/logic/cardUtils.ts` | 31 | getCardsByCategory, getCardsByCost helpers |
| `src/domain/cards/logic/cardDerivation.ts` | 133 | Effective power calculation with mastery/gem bonuses |
| `src/domain/cards/state/masteryManager.ts` | 59 | Mastery level increment, stat bonus calculation |
| `src/domain/cards/state/card.ts` | 82 | getCardDataByClass — class→card array lookup |
| `src/domain/cards/state/CardHandle.ts` | 57 | CardHandle class for card manipulation |
| `src/domain/cards/state/cardPlayLogic.ts` | 100 | canPlayCard, playCard, energy/cost validation |
| `src/constants/data/cards/SwordmanCards.ts` | 594 | 40 Swordsman cards (sw_001 - sw_040) |
| `src/constants/data/cards/mageCards.ts` | 706 | 40 Mage cards: fire/ice/lightning/dark/light (mg_001 - mg_040) |
| `src/constants/data/cards/summonerCards.ts` | 786 | 40 Summoner cards: summon/enhance/attack/sacrifice (sm_001 - sm_040) |

## Data Structures

### Card

```typescript
interface Card {
  id: string;
  cardTypeId: string;
  name: string;
  description: string;
  characterClass: CardCharacterClass;
  cost: number;                       // Energy cost to play
  category: CardCategory;             // "atk" | "def" | "heal" | "buff" | "debuff"
  baseDamage?: number;
  guardAmount?: number;
  healAmount?: number;
  element: ElementType[];
  tags: CardTag[];                    // ["attack", "skill", "guard", ...]
  rarity: ItemRarity;
  useCount: number;
  masteryLevel: number;               // 0-5, increases with use
  gemLevel: number;                   // 0-3, upgrade system
  hitCount?: number;                  // Multi-hit (default 1)
  penetration?: number;               // 0.0-1.0 armor bypass
  applyPlayerBuff?: BuffDebuffState[];
  applyEnemyDebuff?: BuffDebuffState[];
  drawCards?: number;
  energyGain?: number;
  nextCardCostReduction?: number;
  // Summoner-specific
  summonId?: string;
  summonEnhancement?: number;
  requiresSummon?: boolean;
}
```

### DeckState (from deckReducer)

```typescript
interface DeckState {
  drawPile: Card[];
  discardPile: Card[];
  hand: Card[];
}
```

### MasteryMap

```typescript
type MasteryMap = Record<string, MasteryData>;

interface MasteryData {
  cardTypeId: string;
  useCount: number;
  masteryLevel: number;  // 0-5
}
```

## Logic Flow

### Deck Creation

```
createInitialDeck(deckConfig, characterClass)
  ├─ getCardDataByClass(characterClass) → full card array for class
  ├─ deckConfig.deckCards: Record<cardTypeId, count>
  │   └─ For each cardTypeId, create `count` copies
  ├─ Apply mastery bonuses from MasteryMap
  └─ Shuffle → DeckState { drawPile, discardPile: [], hand: [] }
```

### Draw/Discard Cycle

```
deckReducer(state, action):
  ├─ "draw":
  │   ├─ If drawPile empty → shuffle discardPile into drawPile
  │   ├─ Take card from top of drawPile
  │   └─ Add to hand
  ├─ "discard":
  │   ├─ Remove card from hand
  │   └─ Add to discardPile
  ├─ "shuffle":
  │   └─ Fisher-Yates shuffle of drawPile
  └─ "addToHand":
      └─ Directly add card to hand (bypass draw)
```

### Card Play Validation

```
canPlayCard(card, playerEnergy, currentCostReduction?)
  ├─ effectiveCost = max(0, card.cost - costReduction)
  └─ Return playerEnergy >= effectiveCost

playCard(card, deckState, playerEnergy)
  ├─ Validate via canPlayCard
  ├─ Remove from hand (deckReducer "discard")
  ├─ Deduct energy
  ├─ Increment useCount
  └─ Return { newDeckState, newEnergy, cardPlayed }
```

### Mastery System

```
incrementMastery(masteryMap, cardTypeId)
  ├─ useCount++
  ├─ Check mastery level thresholds:
  │   ├─ useCount >= 3  → level 1
  │   ├─ useCount >= 8  → level 2
  │   ├─ useCount >= 15 → level 3
  │   ├─ useCount >= 25 → level 4
  │   └─ useCount >= 40 → level 5
  └─ Return updated MasteryMap

getMasteryBonus(masteryLevel)
  ├─ Level 0: +0% damage, +0% guard, +0% heal
  ├─ Level 1: +5%
  ├─ Level 2: +10%
  ├─ Level 3: +15%
  ├─ Level 4: +20%
  └─ Level 5: +25%
```

### Card Derivation (Effective Power)

```
calculateEffectivePower(card)
  ├─ basePower = card.baseDamage || card.guardAmount || card.healAmount
  ├─ masteryMultiplier = 1.0 + getMasteryBonus(card.masteryLevel)
  ├─ gemMultiplier = 1.0 + card.gemLevel * GEM_BONUS_PER_LEVEL
  ├─ effectivePower = floor(basePower * masteryMultiplier * gemMultiplier)
  └─ Multi-hit: effectivePower * hitCount for total
```

### Card Data Organization by Class

```
Swordsman (sw_001 - sw_040):
  ├─ sw_001-010: Basic attacks (physics element)
  ├─ sw_011-020: Guard/defense cards (guard element)
  ├─ sw_021-030: Skill cards (buff/debuff)
  └─ sw_031-040: Special attacks (high cost, high damage)

Mage (mg_001 - mg_040):
  ├─ mg_001-008: Fire cards (burn DoT)
  ├─ mg_009-016: Ice cards (freeze, slow)
  ├─ mg_017-024: Lightning cards (multi-hit, stun)
  ├─ mg_025-032: Dark cards (lifesteal, weakness)
  └─ mg_033-040: Light cards (heal, cleanse, immunity)

Summoner (sm_001 - sm_040):
  ├─ sm_001-012: Summon cards (place creatures via summonId)
  ├─ sm_013-020: Enhancement cards (buff summons, requiresSummon)
  ├─ sm_021-032: Attack cards (dark element, direct damage)
  └─ sm_033-040: Sacrifice cards (destroy summons for power, requiresSummon)
```

## Key Details

- **IMMUTABLE files**: `deck.ts` and `deckReducter.ts` must never be modified
- Cards have both `id` and `cardTypeId` — `id` is unique per card instance, `cardTypeId` identifies the card type (for mastery tracking)
- Cost reduction carries across cards within a turn (from `nextCardCostReduction`)
- Multi-hit cards calculate damage independently per hit with 500ms delay
- Summoner cards use `requiresSummon: true` to indicate cards that need active summons
- Sacrifice cards (sm_033-040) destroy summons for powerful one-shot effects
- Card `element` is an array (can have multiple elements for dual-element cards)
- All mage cards have exactly one magic element; summoner summon cards use "summon" element
- Swordsman cards primarily use "physics" and "guard" elements

## Dependencies

```
Card Data
  ├─ SwordmanCards.ts → SWORDSMAN_CARDS, SWORDSMAN_CARDS_ARRAY
  ├─ mageCards.ts → MAGE_CARDS, MAGE_CARDS_ARRAY, getMageCard
  └─ summonerCards.ts → SUMMONER_CARDS, SUMMONER_CARDS_ARRAY

Deck Management
  ├─ deck.ts (IMMUTABLE) → createInitialDeck, getCardDataByClass
  ├─ deckReducter.ts (IMMUTABLE) → deckReducer, DeckState
  └─ cardPlayLogic.ts → canPlayCard, playCard

Mastery
  └─ masteryManager.ts → incrementMastery, getMasteryBonus

Derivation
  └─ cardDerivation.ts → calculateEffectivePower

Integration points:
  ├─ useBattleOrchestrator → createInitialDeck, deckReducer
  ├─ useCardExecution → card play resolution, mastery increment
  └─ PlayerContext → deckCards config, MasteryMap persistence
```

## Vulnerability Analysis

### `[BUG-RISK]` Draw From Empty Pile Race Condition

**Location:** `deckReducter.ts` (IMMUTABLE — cannot fix)

If `drawPile` is empty and `discardPile` is also empty (all cards in hand), the draw action would attempt to shuffle an empty array and draw from it. The reducer should handle this edge case but being immutable, it cannot be modified if it doesn't.

### `[BUG-RISK]` Card ID vs CardTypeId Confusion

**Location:** `SwordmanCards.ts:10-11` (pattern repeated across all card files)

All cards have `id` equal to `cardTypeId` (e.g., `id: "sw_001", cardTypeId: "sw_001"`). When multiple copies of the same card are in a deck, they share the same `id`, which could cause issues with React keys or card instance tracking.

### `[QUALITY]` Inconsistent File Naming

**Location:** `SwordmanCards.ts` (missing "d" — should be "SwordsmanCards")

The filename "SwordmanCards" drops the "d" from "Swordsman", inconsistent with the type name `CharacterClass = "swordsman"`.

### `[EXTENSIBILITY]` No Card Validation on Data Files

**Location:** All card data files

Card definitions are plain objects with no runtime validation. A card could have `baseDamage: -5` or `cost: -1` without any error. Type checking ensures shape but not value ranges.

### `[BUG-RISK]` Summoner requiresSummon Not Enforced in Play Logic

**Location:** `cardPlayLogic.ts`

`canPlayCard()` checks energy cost but not `requiresSummon`. A summoner could play enhancement/sacrifice cards (sm_013-040) even with no active summons, as the check happens (if at all) in a different layer.

### `[BUG-RISK]` getMasteryBonus Returns Percentage as Decimal

**Location:** `masteryManager.ts`

Mastery bonus values must be interpreted correctly — if `getMasteryBonus(3)` returns `0.15`, the caller must add 1.0 to create a multiplier. Any caller using the value directly as a multiplier would reduce damage to 15% instead of adding 15%.
