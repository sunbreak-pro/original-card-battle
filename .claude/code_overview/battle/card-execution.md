# Card Execution System

## Overview

Card execution pipeline from click to effect application, covering energy cost, damage/guard/heal resolution, sword energy processing, buff/debuff application, deck management (draw/discard/shuffle), mastery tracking, card derivation, and item effect execution.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/domain/battles/managements/useCardExecution.ts` | 615 | Core execution hook: damage loop, guard, heal, buffs, draw, bleed |
| `src/domain/battles/managements/useDeckManage.ts` | 221 | Battle deck state management with animations |
| `src/domain/cards/state/card.ts` | 83 | Effective power, mastery calc, canPlay, calculateCardEffect |
| `src/domain/cards/state/cardPlayLogic.ts` | 58 | Duplicate of calculateCardEffect + canPlayCard (arrow fn version) |
| `src/domain/cards/state/CardHandle.ts` | 101 | Sword energy processing helpers for card play |
| `src/domain/cards/state/masteryManager.ts` | 60 | MasteryStore (Map), increment/sync mastery across deck |
| `src/domain/cards/logic/cardDerivation.ts` | 134 | Card evolution unlock checks based on mastery levels |
| `src/domain/cards/logic/cardUtils.ts` | 32 | Class-based card filtering utilities |
| `src/domain/battles/logic/cardExecutionLogic.ts` | 30 | Factory for default CardExecutionResult |
| `src/domain/battles/logic/itemEffectExecutor.ts` | 293 | Consumable item effect execution in battle |
| `src/domain/cards/decks/deck.ts` | 107 | Shuffle, draw, discard, createInitialDeck (IMMUTABLE) |
| `src/domain/cards/decks/deckReducter.ts` | 67 | Deck reducer: END_TURN, CARD_PLAY, SET_PILES, ADD_TO_HAND, RESET_DECK |
| `src/types/cardTypes.ts` | 139 | Card interface, CardCategory, MasteryLevel, GemLevel, CardTag |
| `src/types/battleTypes.ts` | 247 | CardExecutionResult, CardEffectPreview, CardExecutionContext |
| `src/constants/cardConstants.ts` | 139 | MASTERY_THRESHOLDS, MASTERY_BONUSES, MAGIC_ELEMENTS |

## Data Structures

### Card (key properties)

```typescript
interface Card {
  id: string;                    // Unique instance ID
  cardTypeId: string;            // Card type (shared across copies)
  characterClass: CardCharacterClass;  // "swordsman" | "mage" | "summoner" | "common"
  cost: number;                  // Energy cost to play
  category: CardCategory;        // "atk" | "def" | "buff" | "debuff" | "heal" | "swordEnergy"
  tags: CardTag[];               // "attack" | "guard" | "skill" | "stance"
  element: ElementType[];        // Element types (fire, ice, physics, etc.)
  baseDamage?: number;
  hitCount?: number;             // Multi-hit (default 1)
  guardAmount?: number;
  healAmount?: number;
  drawCards?: number;
  energyGain?: number;
  swordEnergyGain?: number;
  swordEnergyConsume?: number;   // 0 = consume ALL
  applyEnemyDebuff?: CardBuffSpec[];
  applyPlayerBuff?: CardBuffSpec[];
  useCount: number;
  masteryLevel: MasteryLevel;    // 0-4
  gemLevel: GemLevel;            // 0-2
  derivedFrom?: string;
  derivesInto?: string[];
}
```

### CardExecutionResult

```typescript
interface CardExecutionResult {
  success: boolean;
  damageDealt: number;
  guardGained: number;
  healingDone: number;
  energyConsumed: number;
  swordEnergyChange: number;
  swordEnergyConsumed: number;
  buffsApplied: BuffDebuffState[];
  debuffsApplied: BuffDebuffState[];
  cardsDrawn: number;
  isCritical: boolean;
  lifestealAmount: number;
  reflectDamage: number;
  bleedDamage: number;
}
```

### DeckState / DeckAction

```typescript
interface DeckState {
  drawPile: Card[];
  hand: Card[];
  discardPile: Card[];
}

type DeckAction =
  | { type: "END_TURN"; cardsToDiscard: Card[] }
  | { type: "CARD_PLAY"; card: Card }
  | { type: "SET_PILES"; newDrawPile: Card[]; newDiscardPile: Card[] }
  | { type: "ADD_TO_HAND"; cards: Card[] }
  | { type: "RESET_DECK"; hand: Card[]; drawPile: Card[]; discardPile: Card[] };
```

### MasteryStore

```typescript
type MasteryStore = Map<string, number>;  // cardTypeId → useCount
```

### Key Constants

```
MASTERY_THRESHOLDS = { 0: 0, 1: 8, 2: 16, 3: 24, 4: 30 }
MASTERY_BONUSES    = { 0: 1.0, 1: 1.2, 2: 1.4, 3: 2.0, 4: 2.5 }
effectivePower = round(baseDamage × (1 + masteryLevel × 0.1 + gemLevel × 0.5))
```

## Logic Flow

### Card Play Pipeline (useCardExecution.executeCard)

```
Player clicks card
  ↓
canPlayCard(card, energy, isPlayerPhase)
  ├─ !isPlayerPhase → return false
  └─ card.cost > energy → return false
  ↓
result = createDefaultExecutionResult()
result.energyConsumed = card.cost
setPlayerEnergy(e - card.cost)
  ↓
calculateCardEffect(card)
  ├─ effectivePower = round(baseDamage × (1 + mastery×0.1 + gem×0.5))
  ├─ category=atk → damageToEnemy = effectivePower
  ├─ category=def → shieldGain = effectivePower
  ├─ category=heal → hpGain = effectivePower
  ├─ applyEnemyDebuff → createBuffState for each
  └─ applyPlayerBuff → createBuffState for each
  ↓
ANIMATION: playCardWithAnimation(cardElement, target)
  ↓
SWORD ENERGY PROCESSING:
  ├─ swordEnergyConsume=0 → consumeAllSwordEnergy()
  ├─ swordEnergyConsume>0 → consumeSwordEnergy(amount)
  └─ swordEnergyGain → addSwordEnergy(gain)
  ↓
DAMAGE APPLICATION (if effect.damageToEnemy):
  1. swordEnergyFlatBonus = swordEnergy.current
  2. adjustedBase = baseDamage + swordEnergyFlatBonus
  3. elementalMod = getElementalDamageModifier(card)
  4. adjustedBase = round(adjustedBase × elementalMod.percentMultiplier)
  5. FOR hit = 0..hitCount-1:
     ├─ damageResult = calculateDamage(player, enemy, cardWithBonus)
     ├─ allocation = applyDamageAllocation(enemy, finalDamage)
     ├─ setEnemyGuard/Ap/Hp -= allocation
     ├─ showDamageEffect animation
     ├─ IF lifesteal > 0: heal player
     ├─ IF reflect > 0: damage player
     ├─ updateBattleStats
     └─ delay 500ms (between hits, not after last)
  6. Auto-bleed check (swordsman attack cards only):
     ├─ bleedChance = getSwordEnergyBleedChance(swordEnergy)
     └─ IF random < chance: addOrUpdateBuffDebuff("bleed")
  ↓
GUARD APPLICATION:
  ├─ effect.shieldGain → setPlayerGuard += amount
  ├─ card.guardAmount → setPlayerGuard += amount
  ├─ card sw_037 → guard += swordEnergy × 8
  └─ card sw_039/sw_040 → guard += swordEnergy × 2
  ↓
HEAL APPLICATION:
  ├─ card.healAmount → applyHeal(amount)
  └─ effect.hpGain → applyHeal(amount)
  ↓
ENERGY GAIN: card.energyGain → min(maxEnergy, energy + gain)
  ↓
CARD DRAW: card.drawCards > 0
  ├─ drawCards(count, drawPile, discardPile)
  ├─ dispatch SET_PILES
  └─ drawCardsWithAnimation → dispatch ADD_TO_HAND
  ↓
DEBUFFS TO ENEMY: addOrUpdateBuffDebuff for each
BUFFS TO PLAYER: addOrUpdateBuffDebuff for each
  ↓
CARD DISCARD: incrementUseCount(card) → dispatch CARD_PLAY
  ↓
BLEED DAMAGE: calculateBleedDamage(playerMaxHp, playerBuffs)
  IF > 0: setPlayerHp -= bleedDamage, delay 300ms
  ↓
RETURN result
```

### Deck Draw/Shuffle Cycle

```
drawCards(count, drawPile, discardPile)
  FOR i = 0..count-1:
    IF drawPile empty:
      IF discardPile empty → BREAK (no cards left)
      shuffleDiscardIntoDraw → merge shuffled discard into draw
    card = drawPile.pop()
    drawnCards.push(card)
  RETURN { drawnCards, newDrawPile, newDiscardPile }

CARD_PLAY action:
  hand = hand.filter(c => c.id !== card.id)
  discardPile = [...discardPile, card]

END_TURN action:
  discardPile = [...discardPile, ...cardsToDiscard]
  hand = []
```

### Mastery System

```
incrementUseCount(card):
  newUseCount = card.useCount + 1
  newMasteryLevel = calculateMasteryLevel(newUseCount)
  RETURN { ...card, useCount, masteryLevel }

calculateMasteryLevel(useCount):
  ≥ 30 → 4
  ≥ 24 → 3
  ≥ 16 → 2
  ≥  8 → 1
  else → 0

calculateEffectivePower(card):
  IF no baseDamage → 0
  masteryBonus = 1 + masteryLevel × 0.1
  RETURN round(baseDamage × (masteryBonus + gemLevel × 0.5))
```

### Card Derivation

```
checkDerivationUnlocks(card, allCards, unlockedIds):
  FOR derivedCardTypeId in card.derivesInto:
    IF already unlocked → skip
    requiredMastery = derivedCard.unlockMasteryLevel ?? 2
    IF card.masteryLevel >= required:
      ADD { parentCard, derivedCard } to unlocks
```

### Item Effect Execution (Battle)

```
executeItemEffect(item, hp, maxHp, buffs, energy, maxEnergy)
  ├─ Validate: itemType === 'consumable'
  ├─ Check: isUsableInBattle(typeId)
  ├─ Lookup: getConsumableData(typeId)
  │   └─ IF null: executeFallbackEffect (name-based inference)
  └─ FOR each ConsumableEffect:
       switch effect.type:
         heal → min(value, maxHp - hp)
         fullHeal → maxHp - hp
         shield → guardChange
         energy → min(value, maxEnergy - energy)
         buff → buffsApplied with duration
         debuffClear → debuffsCleared = true
         damage → damageDealt
         draw → cardsDrawn
         skipEnemyTurn → skipEnemyTurn = true
```

## Key Details

### Effective Power Formula

```
effectivePower = round(baseDamage × (1 + masteryLevel × 0.1 + gemLevel × 0.5))

Examples:
  baseDamage=10, mastery=0, gem=0 → round(10 × 1.0) = 10
  baseDamage=10, mastery=2, gem=1 → round(10 × (1.2 + 0.5)) = 17
  baseDamage=10, mastery=4, gem=2 → round(10 × (1.4 + 1.0)) = 24
```

### Damage Bonus Stack in executeCard

```
totalBaseDamage = card.baseDamage
  + swordEnergy.current                        (flat bonus, calculated once)
  × elementalMod.percentMultiplier              (resonance multiplier)
```

### Sword Energy Guard Specials

```
sw_037: guard += swordEnergy.current × 8
sw_039: guard += swordEnergy.current × 2
sw_040: guard += swordEnergy.current × 2
```

### Multi-Hit Timing

```
500ms delay between each hit
300ms delay after bleed damage at end
Animation callbacks are async/await
```

### Fallback Item Effect

```
Unknown consumable → name-based inference:
  "potion"/"heal" → heal 30 (lesser=15, greater=60)
  Unknown → generic success with no effect
```

## Dependencies

```
useCardExecution.ts
  ├─ card.ts (calculateCardEffect, canPlayCard, incrementUseCount)
  ├─ damageCalculation.ts (calculateDamage, applyDamageAllocation)
  ├─ buffLogic.ts (addOrUpdateBuffDebuff)
  ├─ battleLogic.ts (applyHeal)
  ├─ bleedDamage.ts (calculateBleedDamage)
  ├─ swordEnergySystem.ts (addSwordEnergy, consumeSwordEnergy, etc.)
  ├─ cardExecutionLogic.ts (createDefaultExecutionResult)
  ├─ deck.ts (drawCards) — IMMUTABLE
  └─ classAbilitySystem.ts (DamageModifier type)

useDeckManage.ts
  ├─ deckReducter.ts (deckReducer, DeckAction)
  ├─ deck.ts (createInitialDeck, drawCards, shuffleArray) — IMMUTABLE
  ├─ useCardAnimation.ts (draw/discard/play animations)
  └─ SwordmanCards.ts + initialDeckConfig.ts (initial deck data)

cardDerivation.ts
  └─ standalone (only card types)

masteryManager.ts
  └─ cardConstants.ts (MASTERY_THRESHOLDS)

itemEffectExecutor.ts
  ├─ ConsumableItemData.ts (getConsumableData, isUsableInBattle)
  ├─ buffLogic.ts (createBuffState)
  └─ buffData.ts (BUFF_EFFECTS)
```

## Vulnerability Analysis

### `[BUG-RISK]` Sword Energy Bonus Uses Stale Value

**Location:** `useCardExecution.ts:320`

```typescript
const swordEnergyFlatBonus = swordEnergy.current;
```

The sword energy flat bonus is captured from `swordEnergy` (a prop/state) before the sword energy consumption at lines 287-312. If the card consumes sword energy AND deals damage, the damage bonus uses the pre-consumption value. The consumption happens via `setSwordEnergy` (async state update), so `swordEnergy.current` still reflects the old value when damage is calculated. This means a "consume all + deal damage" card gets the full energy bonus before the energy is actually consumed. May be intentional design, but creates a hidden ordering dependency.

### `[BUG-RISK]` Double Guard Application

**Location:** `useCardExecution.ts:429-461`

```typescript
if (effect.shieldGain) { ... }         // From calculateCardEffect
if (card.guardAmount && card.guardAmount > 0) { ... }  // Direct card property
```

Guard is applied from two sources: `effect.shieldGain` (from `calculateCardEffect` which only fires for `category=def`) AND `card.guardAmount` (direct card property). A defense card with `category: "def"` and `guardAmount` set would get guard applied twice — once as effective-power-scaled shield and once as raw guardAmount. Cards must carefully avoid setting both `category: "def"` and `guardAmount`.

### `[BUG-RISK]` Hardcoded Card IDs for Sword Energy Guard

**Location:** `useCardExecution.ts:446-461`

```typescript
if (card.cardTypeId === "sw_037") {
  const guardFromEnergy = swordEnergy.current * 8;
```

Three specific card IDs (`sw_037`, `sw_039`, `sw_040`) have hardcoded special behavior for sword energy → guard conversion. This bypasses the card data system entirely. If these cards are renamed, rebalanced, or new similar cards are added, the hardcoded checks must be manually updated.

### `[BUG-RISK]` Heal Uses Stale HP Value

**Location:** `useCardExecution.ts:468-483`

```typescript
const newHp = applyHeal(card.healAmount, playerHp, playerMaxHp);
setters.setPlayerHp(newHp);
```

`playerHp` is a captured closure value. If the card both deals reflect damage to the player (reducing HP) and heals, the heal calculation uses the pre-reflect HP. Multiple heal sources in the same card also clobber each other — `setPlayerHp(newHp)` is called multiple times with the same base `playerHp`, not the updated value.

### `[BUG-RISK]` Duplicate calculateCardEffect Functions

**Location:** `card.ts:52-82` and `cardPlayLogic.ts:14-43`

Both `card.ts` and `cardPlayLogic.ts` export identical `calculateCardEffect` and `canPlayCard` functions. `useCardExecution` imports from `card.ts`. Any bug fix or behavior change must be applied to both files, or they'll diverge silently. `cardPlayLogic.ts` is likely a leftover duplicate.

### `[BUG-RISK]` Item Effect Damage Ignores Defense

**Location:** `itemEffectExecutor.ts:172-178`

```typescript
case 'damage': {
  return {
    damageDealt: effect.value ?? 0,
```

Item damage effects return raw damage values without going through `calculateDamage` or `applyDamageAllocation`. The caller must separately apply this damage to enemy HP/AP/guard, but the `ItemEffectResult` structure doesn't distinguish between HP/AP/guard allocation. This means item damage either bypasses all defenses or the integration layer must handle allocation separately.

### `[BUG-RISK]` Auto-Bleed Uses Stale Enemy Buffs

**Location:** `useCardExecution.ts:410-421`

```typescript
const newEnemyBuffs = addOrUpdateBuffDebuff(
  enemyBuffs,  // ← closure value, not current state
```

The auto-bleed from sword energy applies bleed using `enemyBuffs` from the closure, not the potentially updated buff map (if the card itself applied debuffs earlier at line 521-538). The `setEnemyBuffs` at line 537 and line 420 operate on different base states, so the last one wins — whichever runs last in React's batch update will overwrite the other's changes.

### `[EXTENSIBILITY]` calculateCardEffect Only Handles 3 Categories

**Location:** `card.ts:58-68`

```typescript
switch (card.category) {
  case "atk": result.damageToEnemy = effectivePower; break;
  case "def": result.shieldGain = effectivePower; break;
  case "heal": result.hpGain = effectivePower; break;
}
```

The `buff`, `debuff`, and `swordEnergy` categories are not handled in the switch statement. Cards with these categories get no `effectivePower`-based effect — they rely entirely on `applyEnemyDebuff`/`applyPlayerBuff` arrays. Adding a new category requires modifying this function. The effective power formula (mastery/gem scaling) doesn't apply to these categories at all.

### `[EXTENSIBILITY]` useDeckManage Hardcodes Swordsman Initial Deck

**Location:** `useDeckManage.ts:70`

```typescript
const initialDeck = createInitialDeck(INITIAL_DECK_COUNTS, SWORDSMAN_CARDS_ARRAY);
```

The deck management hook hardcodes `SWORDSMAN_CARDS_ARRAY` for initial deck creation. Mage and Summoner classes cannot use this hook without modification. The `resetDeck` function (line 136) also hardcodes the same array.

### `[QUALITY]` cardPlayLogic.ts is a Complete Duplicate

**Location:** `src/domain/cards/state/cardPlayLogic.ts` (58 lines)

This file contains exact duplicates of `calculateCardEffect` and `canPlayCard` from `card.ts`, implemented as arrow functions instead of regular functions. It's dead code — `useCardExecution` imports from `card.ts`. Should be deleted to avoid maintenance burden.

### `[QUALITY]` CardHandle.ts processSwordEnergyConsumption Returns Unused damageBonus

**Location:** `CardHandle.ts:49`

```typescript
return {
  damageBonus: 0,  // Always 0
  consumedAmount,
  newState,
};
```

`processSwordEnergyConsumption` always returns `damageBonus: 0`. The actual damage bonus from sword energy is calculated directly in `useCardExecution.ts:320` as `swordEnergy.current`. The `CardHandle.ts` abstraction exists but isn't used by the main execution path — `useCardExecution` directly calls `consumeSwordEnergy`/`addSwordEnergy` instead of going through `CardHandle`.

### `[QUALITY]` Module-Level Mutable Counter in deck.ts

**Location:** `deck.ts:3-7`

```typescript
let cardInstanceCounter = 0;
const generateCardInstanceId = (baseId: string): string => {
  cardInstanceCounter++;
  return `${baseId}_instance_${cardInstanceCounter}`;
};
```

A module-level mutable counter generates card instance IDs. This counter never resets across battles or page reloads (only on full module re-evaluation). IDs will grow indefinitely. In development with HMR, counter resets on file change could cause ID collisions with existing cards in state.
