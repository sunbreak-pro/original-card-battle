# Player & Character System

## Overview

The player/character system defines two playable classes (Swordsman, Mage) with shared base stats and class-specific ability systems. `PlayerContext` is the central ~675-line provider that manages persistent player data, runtime battle state, deck cards, lives, mastery, equipment AP, and resource delegation. Each class has a dedicated ability system implementing the `ClassAbilitySystem<T>` generic interface, producing `DamageModifier` objects that feed into damage calculation.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/types/characterTypes.ts` | 344 | All character types: BattleStats, class abilities, enemy, player |
| `src/constants/data/characters/PlayerData.tsx` | 82 | Base stats per class (HP, AP, speed, energy, deck config) |
| `src/constants/data/characters/CharacterClassData.ts` | 226 | Class display info, descriptions, ability descriptions |
| `src/contexts/PlayerContext.tsx` | ~675 | Player state provider — persistent + runtime + deck + lives |
| `src/domain/characters/logic/playerUtils.ts` | 96 | Player stat helpers (getInitialPlayerState, buildBattleStats) |
| `src/domain/characters/logic/characterUtils.ts` | 34 | createEmptyBuffDebuffMap, shared utils |
| `src/domain/characters/logic/classAbilityUtils.ts` | 88 | Factory functions for initial class ability states |
| `src/domain/characters/classAbility/classAbilitySystem.ts` | 137 | Generic ClassAbilitySystem interface + DamageModifier |
| `src/domain/characters/player/logic/swordEnergySystem.ts` | 261 | Swordsman ability: sword energy accumulation + bleed |
| `src/domain/characters/player/logic/elementalSystem.ts` | 249 | Mage ability: elemental resonance chain |
| `src/domain/characters/player/logic/title.ts` | ~23 | Title/grade strings by card type count |

## Data Structures

### BattleStats (shared)

```typescript
interface BattleStats {
  hp: number; maxHp: number;
  ap: number; maxAp: number;
  guard: number;
  speed: number;
  buffDebuffs: BuffDebuffMap;
}
```

### ClassAbilitySystem<T> (generic interface)

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
  flatBonus: number;          // Added to base damage
  percentMultiplier: number;  // 1.0 = 100%
  critBonus: number;          // 0.0 - 1.0
  penetration: number;        // 0.0 - 1.0
}
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
  lastElement: ElementType | null;
  resonanceLevel: ResonanceLevel;  // 0 | 1 | 2
}
```


### PlayerData (persistent)

```typescript
interface PlayerData {
  id: string;
  name: string;
  characterClass: CharacterClass;
  classGrade: string;
  stats: { hp, maxHp, ap, maxAp, speed, energy, maxEnergy };
  deck: { deckCards, drawCount, handSize };
  mastery: MasteryMap;
  lives: LivesSystem;
  souls: number;
  inventory: { inventory, storage, equipmentSlots, equipmentInventory };
}
```

## Logic Flow

### Character Selection → Battle Initialization

```
Character select screen
  → setPlayerClass(class)
  → PlayerContext loads base stats from PlayerData[class]
  → getInitialPlayerState(class, equipmentSlots, sanctuary)
      ├─ Base HP/AP/speed from PlayerData
      ├─ + equipmentAP bonus (from InventoryContext)
      ├─ + sanctuary HP bonus
      └─ Returns InitialPlayerState for battle
  ↓
useBattleOrchestrator receives InitialPlayerState
  → buildBattleStats(playerState) → BattleStats
  → class ability initialized via ClassAbilitySystem.initialize()
```

### Sword Energy System (Swordsman)

```
Card played → SwordEnergySystem.onCardPlay(state, card)
  ├─ calculateSwordEnergyGain(cost, isSwordEnergyCard, customGain)
  │   ├─ customGain → use directly
  │   ├─ isSwordEnergyCard → +4
  │   └─ cost 0/1 → +1, cost 2 → +2, cost 3+ → +3
  ├─ addSwordEnergy(state, gain) → clamped to max (10)
  ↓
getDamageModifier(state)
  ├─ flatBonus: current * 0.5 (floor)
  ├─ percentMultiplier: 1.0 (no percent bonus)
  └─ Bleed chance thresholds:
      ├─ energy >= 5 → 20%
      ├─ energy >= 8 → 40%
      └─ energy >= 10 (MAX) → 60%

Turn end → onTurnEnd(state)
  └─ energy = max(0, current - 3)  ← decays by 3 each turn
```

### Elemental Resonance System (Mage)

```
Card played → ElementalSystem.onCardPlay(state, card)
  ├─ Find magic element in card.element (fire/ice/lightning/dark/light)
  │   ├─ No magic element → reset resonance to 0, lastElement = null
  │   ├─ Same element as lastElement → resonanceLevel + 1 (max 2)
  │   └─ Different element → reset to 0, set lastElement = new element
  ↓
getDamageModifier(state, card)
  ├─ Only applies if card element matches lastElement
  ├─ percentMultiplier: RESONANCE_MULTIPLIER[level]  (1.0 / 1.15 / 1.30)
  └─ critBonus: level 2 → +0.10, else 0

getResonanceEffects(element, level, enhancedElements?)
  ├─ Fire: burn stacks
  ├─ Ice: freeze duration
  ├─ Lightning: stun at Lv2
  ├─ Dark: lifesteal, weakness
  └─ Light: cleanse, heal
  + Sanctuary enhancement bonuses (extra stacks/duration)

Turn end → onTurnEnd(state)
  └─ resonanceLevel = 0  ← resets every turn
```


### Title System

```
getSwordsmanTitle(cardTypeCount)   getMageTitle(count)
  ├─ >= 50 → "剣神"                ├─ >= 50 → "魔神"
  ├─ >= 30 → "剣聖"                ├─ >= 30 → "大魔導師"
  ├─ >= 15 → "剣豪"                ├─ >= 15 → "魔導師"
  ├─ >= 5  → "剣士"                ├─ >= 5  → "魔術士"
  └─ else  → "見習い剣士"           └─ else  → "見習い魔術士"
```

### PlayerContext API Surface

```
PlayerContext provides:
  ├─ playerData (persistent PlayerData)
  ├─ updatePlayerData(partial) → merges into playerData
  ├─ runtimeBattleState → HP/AP/lives/mastery for current battle
  ├─ deckCards → custom deck configuration
  ├─ equipmentAP → computed from equipped items
  ├─ Resource delegation:
  │   ├─ gold, magicStones (delegates to ResourceContext)
  │   ├─ addGold, useGold, addMagicStones, useMagicStones
  │   └─ sanctuary progress, exploration resources
  └─ Lives system: remaining, max, lose/gain/reset
```

## Key Details

- `ClassAbilitySystem` is a generic interface — all three class systems implement the same 7 methods
- `combineDamageModifiers()` multiplies percentMultiplier values together, adds flat bonuses, caps crit/penetration at 1.0
- `applyDamageModifier()`: `floor((baseDamage + flatBonus) * percentMultiplier)`
- Sword energy decays by 3 per turn end, not fully reset
- Mage resonance fully resets at turn end — must chain within a single turn
- PlayerContext generates player ID as `player_${Date.now()}` which changes on every `useMemo` recomputation

## Dependencies

```
PlayerContext
  ├─ ResourceContext (gold, magicStones delegation)
  ├─ PlayerData (base stats)
  ├─ CharacterClassData (display info)
  ├─ classAbilityUtils → createInitialSwordEnergy / createInitialElemental / createInitialSummon
  └─ equipmentStats (AP calculation)

ClassAbilitySystem implementations
  ├─ SwordEnergySystem ← constants (SWORD_ENERGY_MAX, bleed chances)
  └─ ElementalSystem ← constants (RESONANCE_MULTIPLIER, RESONANCE_EFFECTS, MAGIC_ELEMENTS)

Battle integration
  └─ useBattleOrchestrator
      ├─ useSwordEnergy() hook
      └─ useElementalChain() hook (both called unconditionally per React rules)
```

## Vulnerability Analysis

### `[BUG-RISK]` Player ID Regenerated on Recomputation

**Location:** `PlayerContext.tsx:772`

`id: \`player_${Date.now()}\`` is inside a `useMemo` computation. Every time dependencies change (playerState, equipmentAP), the player ID changes. Any system caching or comparing player IDs will break.

### `[BUG-RISK]` Sword Energy _card Parameter Unused

**Location:** `swordEnergySystem.ts:110`

`getDamageModifier(state: SwordEnergyState, _card?: Card)` ignores the card parameter entirely. The flat damage bonus applies to all cards equally regardless of card type. This means defensive cards also get the sword energy damage bonus if they happen to deal damage.


### `[BUG-RISK]` Elemental Resonance Uses card.element Array Inconsistently

**Location:** `elementalSystem.ts:48-77`

`onCardPlay` finds the first magic element via `card.element.find()` but checks chain continuation with `card.element.includes(state.lastElement)`. A card with `["fire", "ice"]` could continue a fire chain (includes fire) but set lastElement to fire (first magic element found). If the player then plays a pure ice card, the chain breaks despite the previous card containing ice.

### `[EXTENSIBILITY]` PlayerContext at ~675 Lines

**Location:** `PlayerContext.tsx`

PlayerContext handles persistent data, runtime battle state, deck management, lives system, equipment AP, resource delegation, and sanctuary progress. This violates single-responsibility.

### `[BUG-RISK]` Title Functions Disconnected from Gameplay

**Location:** `title.ts`

Title functions (`getSwordsmanTitle`, `getMageTitle`) take a `cardTypeCount` parameter but there's no visible mechanism tracking unique card types used. The functions are exported but may not be called anywhere, making the title system non-functional.
