# Card Deck System Integration Improvement Plan v2

Created: 2026-01-26
Status: Awaiting Approval
Original Plan: Card Deck System Improvement Plan (2026-01-25)

---

## 1. Current Status Analysis Summary

### 1.1 Identified Issues

| Issue | Impact | Confirmed Files |
|-------|--------|-----------------|
| Duplicate Data Source Definition | High | `CharacterClassData.ts` (5 cards) vs `initialDeckConfig.ts` (20 cards) |
| Unused PlayerContext.deck | High | `useBattleOrchestrator.ts` completely ignores it |
| Hard-coding in BattleScreen | Medium | Direct reference to `Swordman_Status` |
| Lack of Class-specific Support | Medium | Always generates fixed Swordsman deck |

### 1.2 Current Data Flow (Problematic)

```
CharacterSelect.tsx
    | initializeWithClass(classType)
PlayerContext
    | classInfo.starterDeck -> playerState.deck (5 cards)
    | Stored in playerData.persistent.deckCardIds
BattleScreen.tsx
    | deckCardIds NOT loaded <- [DISCONNECT POINT]
    | Creates initialPlayerState (hardcoded Swordman_Status)
useBattleOrchestrator.ts
    | Uses INITIAL_DECK_COUNTS (20 cards, Swordsman only)
createInitialDeck()
    | Always same 20-card deck
```

---

## 2. Integration Policy

### 2.1 Decisions

| Item | Decision | Reason |
|------|----------|--------|
| Initial Deck Size | **15 cards** | Eliminates need for Phase 2 adjustments |
| Data Source Integration | **initialDeckConfig.ts** as source of truth | Structure suitable for card count management |
| Data Flow Fix | **Fix BattleScreen side** | Utilize PlayerContext.deck |

### 2.2 Target Data Flow (After Improvement)

```
CharacterSelect.tsx
    | initializeWithClass(classType)
PlayerContext
    | Reference INITIAL_DECK_BY_CLASS[classType]
    | Store in playerData.persistent.deckCardIds
BattleScreen.tsx
    | Get deckCardIds from playerData <- [FIX POINT]
    | Include in initialPlayerState
useBattleOrchestrator.ts
    | Use initialPlayerState.deckConfig <- [FIX POINT]
createInitialDeck()
    | Correct deck for each class
```

---

## 3. Phase 1: Immediate Actions (Integrated Version)

### 3.1 Files to Modify

| File | Change Type | Summary |
|------|-------------|---------|
| `initialDeckConfig.ts` | Extend | Character-specific settings, change to 15 cards |
| `CharacterClassData.ts` | Modify | Remove starterDeck, reference initialDeckCounts |
| `useBattleState.ts` | Modify | Add deckConfig to InitialPlayerState type |
| `useBattleOrchestrator.ts` | Modify | Receive deck settings from initialPlayerState |
| `BattleScreen.tsx` | Modify | Get deck info from playerData, include in initialPlayerState |
| `PlayerContext.tsx` | Modify | Use INITIAL_DECK_BY_CLASS in initializeWithClass |

### 3.2 initialDeckConfig.ts Design Changes

**Changes:**
- Add new `INITIAL_DECK_BY_CLASS` (character-specific settings)
- Adjust Swordsman from 20 to 15 cards
- Maintain `INITIAL_DECK_COUNTS` as alias for backward compatibility

**15-card Configuration (Swordsman):**
```
sw_001: 3  // Quick Slash (4->3)
sw_003: 2  // Combo Strike (3->2)
sw_007: 2  // Slash (3->2)
sw_013: 2  // Sword Energy Focus
sw_037: 2  // Sword Barrier
sw_038: 2  // Counter Stance
sw_014: 2  // Meditation
-----------
Total: 15 cards
```

**Removed Cards:**
- `sw_027` (Sword Energy Release): Too powerful for early game, add via Phase 3 mastery system

### 3.3 CharacterClassData.ts Design Changes

**Changes:**
- Remove `starterDeck: Card[]`
- Add `initialDeckCounts: Record<string, number>`
- Add `totalCards: number` (for UI display)
- Add `getStarterDeckStacks()` function (for stack display)

**New Functions:**
- `getStarterDeckStacks(classType)`: Returns card info in stack format for UI
- `getCardDataByClass(classType)`: Returns card data array by class

### 3.4 BattleScreen.tsx Design Changes

**Changes:**
- Retrieve `playerData.persistent.deckCardIds`
- Add `deckConfig` to `initialPlayerState`
- Change hardcoded `Swordman_Status` to reference `playerData`

**Modified Section (around lines 88-106):**
```typescript
// Before: Hardcoded Swordman_Status
// After: Retrieved from playerData
const initialPlayerState = useMemo<InitialPlayerState>(() => ({
  currentHp: runtimeState.currentHp,
  currentAp: runtimeState.currentAp,
  maxHp: playerData.persistent.baseMaxHp,
  maxAp: playerData.persistent.baseMaxAp,
  name: playerData.persistent.name,
  playerClass: playerData.persistent.playerClass,
  classGrade: playerData.persistent.classGrade,
  speed: playerData.persistent.baseSpeed,
  cardActEnergy: playerData.persistent.cardActEnergy,
  cardMasteryStore: runtimeState.cardMasteryStore,
  deckConfig: getInitialDeckCounts(playerData.persistent.playerClass),
}), [...]);
```

### 3.5 useBattleOrchestrator.ts Design Changes

**Changes:**
- Add `deckConfig` to `InitialPlayerState` type
- Fix `initialDeckState` generation logic
- Add `getCardDataByClass()` helper function

**Modified Section (around lines 169-178):**
```typescript
// Before: Fixed INITIAL_DECK_COUNTS
// After: Use initialPlayerState.deckConfig
const initialDeckState = useMemo(() => {
  const deckCounts = initialPlayerState?.deckConfig
    ?? getInitialDeckCounts(initialPlayerState?.playerClass ?? 'swordsman');
  const cardData = getCardDataByClass(initialPlayerState?.playerClass ?? 'swordsman');
  let initialDeck = createInitialDeck(deckCounts, cardData);

  if (initialPlayerState?.cardMasteryStore && initialPlayerState.cardMasteryStore.size > 0) {
    initialDeck = applyMasteryToCards(initialDeck, initialPlayerState.cardMasteryStore);
  }
  return { hand: [], drawPile: initialDeck, discardPile: [] };
}, [initialPlayerState]);
```

### 3.6 PlayerContext.tsx Design Changes

**Changes:**
- Use INITIAL_DECK_BY_CLASS in `initializeWithClass`
- Fix `playerData.persistent.deckCardIds` generation logic
- Add `cardActEnergy` to PlayerData

**Modified Section (around lines 313-342):**
```typescript
// Generate deck from INITIAL_DECK_BY_CLASS
const deckCounts = INITIAL_DECK_BY_CLASS[classType];
const cardData = getCardDataByClass(classType);
const deck = createDeckFromCounts(deckCounts, cardData);

const playerWithStarterDeck: Player = {
  ...basePlayer,
  deck: deck,
};
```

---

## 4. Additional Proposals

### 4.1 Proposal 1: Add cardActEnergy to PlayerData

**Current:** `Swordman_Status.cardActEnergy` is hardcoded
**Proposal:** Add to `PlayerData.persistent.cardActEnergy` (implemented)

**Affected Files:**
- `playerTypes.ts`: PlayerData type definition
- `PlayerContext.tsx`: playerData generation
- `BattleScreen.tsx`: Reference locations

### 4.2 Proposal 2: Stack Display for Character Selection Screen

**Current:** starterDeck displayed as individual cards (5 cards)
**Proposal:** Stack display (same cards shown as "x3" format)

**New Component:**
```typescript
// StarterDeckPreview.tsx
interface DeckCardStack {
  card: Card;
  count: number;
}

function StarterDeckPreview({ characterClass }: { characterClass: CharacterClass }) {
  const deckStacks = getStarterDeckStacks(characterClass);
  // Display cards in stack format
}
```

### 4.3 Proposal 3: Deck Validation Utility

**Purpose:** Detect deck configuration errors during development

```typescript
// deckValidator.ts
export function validateDeckConfig(
  deckCounts: Record<string, number>,
  availableCards: Card[]
): { valid: boolean; errors: string[] } {
  // Verify cardTypeId existence
  // Verify total card count
  // Verify required cards
}
```

---

## 5. Implementation Priority (Phase 1)

### Phase 1A (Required - Immediate)
1. `initialDeckConfig.ts` extension
2. `useBattleOrchestrator.ts` modification
3. `BattleScreen.tsx` modification

### Phase 1B (Recommended - Immediate)
4. `CharacterClassData.ts` modification
5. `PlayerContext.tsx` modification

### Phase 1C (Optional - Later)
6. Character selection screen stack display
7. Deck validation utility

---

## 6. Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Existing save data compatibility | Deck count change causes inconsistency | Version check and migration processing |
| Card type ID generation | Unique ID generation needed in createInitialDeck | Maintain existing logic, manage by cardTypeId |
| Lack of tests | Risk of regression bugs | Create manual test checklist |

---

## 7. Test Checklist

### Functional Tests
- [ ] Character selection -> Battle start generates correct deck
- [ ] Hand correctly draws 5 cards
- [ ] Deck total is 15 cards
- [ ] Card mastery correctly saved after card use

### Regression Tests
- [ ] Existing battle flow works
- [ ] Reward screen displays after enemy defeat
- [ ] Defeat processing works correctly
- [ ] Save/Load functions normally

---

## 8. Related File References

### Modification Targets
```
src/domain/battles/data/initialDeckConfig.ts
src/domain/characters/player/data/CharacterClassData.ts
src/domain/battles/managements/useBattleOrchestrator.ts
src/domain/battles/managements/useBattleState.ts
src/ui/battleHtml/BattleScreen.tsx
src/domain/camps/contexts/PlayerContext.tsx
```

### Reference Only (DO NOT MODIFY)
```
src/domain/cards/decks/deck.ts (IMMUTABLE)
src/domain/cards/decks/deckReducter.ts (IMMUTABLE)
```

### Related References
```
src/domain/cards/data/SwordmanCards.ts
src/domain/characters/player/data/PlayerData.ts
src/ui/characterSelectHtml/CharacterSelect.tsx
src/ui/characterSelectHtml/StarterDeckPreview.tsx
```

---

## 9. Phase 3: Card Derivation Mastery System Design

### 9.1 New Concept: Card Derivation System

**Differences from Existing Design:**

| Item | Existing Design (library_design.md etc.) | New Addition (Phase 3) |
|------|------------------------------------------|------------------------|
| Mastery Increase | Usage count based (thresholds: 50, 150, 250...) | Same (maintained) |
| Card Acquisition | "Talent Card" unlocked at Lv5 (details undefined) | **Source card + Required mastery Lv** unlocks new card |
| Acquisition Timing | Undefined | Immediately available upon reaching mastery Lv |

**New Concept: Card Derivation Tree**
```
Base Card (initial deck possession)
    | Mastery Lv2
Derived Card 1 (acquired)
    | Mastery Lv4
Derived Card 2 (acquired)
```

### 9.2 Card Data Extension Design

**Additional Properties for Card Type:**
```typescript
interface Card {
  // Existing properties
  id: string;
  cardTypeId: string;
  name: string;
  // ...

  // New: Card acquisition system
  unlockSource?: string;       // cardTypeId of source card (null = initial possession)
  unlockMasteryLevel?: number; // Required mastery Lv (1-5)
  unlocksCards?: CardUnlock[]; // List of cards derived from this card
}

interface CardUnlock {
  cardTypeId: string;    // ID of card to be acquired
  requiredLevel: number; // Required mastery Lv
}
```

### 9.3 Swordsman Card Derivation Tree (Draft)

```
[Initial Deck (15 cards)]
+-- sw_001 Quick Slash x3
|   +-- Lv2 -> sw_005 Double Slash
|   +-- Lv3 -> sw_019 Whirlwind Blade
|   +-- Lv5 -> sw_020 Flash Blade
|
+-- sw_003 Combo Strike x2
|   +-- Lv2 -> sw_006 Triple Thrust
|   +-- Lv4 -> sw_026 Lion's Fury
|   +-- Lv5 -> sw_036 Annihilation Blade
|
+-- sw_007 Slash x2
|   +-- Lv2 -> sw_017 Sky Splitter
|   +-- Lv4 -> sw_023 Dimension Rend
|
+-- sw_013 Sword Energy Focus x2
|   +-- Lv2 -> sw_015 Fighting Spirit Release
|   +-- Lv4 -> sw_033 Sword God Descent
|
+-- sw_014 Meditation x2
|   +-- Lv2 -> sw_016 Spirit Charge
|   +-- Lv4 -> sw_029 Mind's Eye
|
+-- sw_037 Sword Barrier x2
|   +-- Lv3 -> sw_038 Counter Stance (*also in initial deck)
|
+-- sw_038 Counter Stance x2
    +-- Lv4 -> sw_039 Iron Wall Stance
```

### 9.4 Acquisition UI Requirements

1. **Card Acquisition Notification (End of Battle)**
   - Popup display when mastery Lv increases
   - Select acquirable card (when multiple derivations exist)

2. **Card Tree Display (Library Facility)**
   - Add "Evolution Tree" tab in Encyclopedia
   - Visually display acquired/not acquired/acquirable status

3. **Impact on Deck Edit Screen**
   - Only acquired cards can be added to deck
   - Display each card's mastery and derivation targets

---

## 10. Design Document Update Plan

### 10.1 Target Files

| Document | Path | Update Content |
|----------|------|----------------|
| NEW_CHARACTER_SYSTEM_DESIGN.md | `.claude/docs/card_document/` | Add card derivation system details |
| SWORDSMAN_CARDS_40.md | `.claude/docs/card_document/` | Add unlockSource/unlocksCards to each card |
| library_design.md | `.claude/docs/camp_document/` | Add card derivation tree display to Encyclopedia |
| game_design_master.md | `.claude/docs/Overall_document/` | Document card derivation in growth system |

### 10.2 Additions to NEW_CHARACTER_SYSTEM_DESIGN.md

**New Section:**
```markdown
### Card Derivation System via Mastery (New)

New cards can be acquired through mastery level increases from card usage.

[Basic Rules]
- Each card has "derived cards" and "required mastery Lv" settings
- Derived cards become acquirable when mastery Lv is reached
- Player selects when multiple derivations exist from same card

[Data Structure]
- unlockSource: cardTypeId of source card
- unlockMasteryLevel: Required mastery Lv (1-5)
- unlocksCards: List of cards derived from this card

[Integration with Existing "Talent Card Unlock"]
- Lv5 "Talent Card Unlock" positioned as final stage of derivation tree
- Special talent cards only acquirable at Lv5
```

### 10.3 Additions to SWORDSMAN_CARDS_40.md

**Addition to Each Card Definition:**
```markdown
## sw_001 Quick Slash

[Existing Items]
- Cost: 1
- Effect: Power 15, Sword Energy +1

[New: Derivation Info]
- Source: None (initial possession)
- Derivations:
  - sw_005 Double Slash (Lv2)
  - sw_019 Whirlwind Blade (Lv3)
  - sw_020 Flash Blade (Lv5)

---

## sw_005 Double Slash

[Existing Items]
- Cost: 2
- Effect: Power 12x2, Sword Energy +2

[New: Derivation Info]
- Source: sw_001 (Quick Slash) Lv2
- Derivations: (Under Design)
```

### 10.4 Additions to library_design.md

**Addition to Encyclopedia Section:**
```markdown
### 4.4 Card Derivation Tree Display (New)

+------------------------------------------------------------+
|  Book of Encyclopedia - Card Evolution Tree                 |
+------------------------------------------------------------+
|  [Swordsman][Mage][Summoner]                               |
|                                                            |
|  +-----------------------------------------------------+   |
|  |  Quick Slash [Lv3/5]                                |   |
|  |       |                                             |   |
|  |       +-- Double Slash [Acquired] ---+              |   |
|  |       |                              |              |   |
|  |       +-- Whirlwind Blade [Unlock@Lv3]              |   |
|  |       |                              |              |   |
|  |       +-- Flash Blade [Unlock@Lv5]   |              |   |
|  |                                      v              |   |
|  |                          (Further derivations)      |   |
|  +-----------------------------------------------------+   |
|                                                            |
|  Legend: Acquired  Locked  Available                       |
+------------------------------------------------------------+
```

### 10.5 Additions to game_design_master.md

**Addition to Growth System Section:**
```markdown
### Card Growth (Details)

1. **Mastery System**
   - Mastery Lv increases with card usage count (Lv1->5)
   - Required usage counts based on card cost

2. **Card Derivation System** (New)
   - Acquire new cards upon reaching mastery Lv
   - Derivation tree: Initial deck cards -> Intermediate cards -> Advanced cards
   - View derivation status in Library Encyclopedia

3. **Impact on Deck Building**
   - Only acquired cards can be added to deck
   - No same-card limit (free within 15-50 card deck range)
```

---

## 11. Deck Building System Revision

### 11.1 New Deck Rules

**Changes from Existing Design:**

| Item | Existing Design | New Design |
|------|-----------------|------------|
| Deck Size | Fixed 40 cards | **15-50 cards** (flexible) |
| Same Card Limit | Max 4 copies | **No limit** (any number if acquired) |
| Edit Location | Library only | **Library + Exploration Prep Screen** |

**Constraints:**
- Minimum cards: 15 (same as initial deck)
- Maximum cards: 50
- Only acquired cards can be added

### 11.2 Deck Building Strategy

**Small Deck (15-25 cards):**
- Higher chance to draw key cards
- Fast deck cycling
- Risk: Hand depletion, deck exhaustion

**Large Deck (35-50 cards):**
- Adaptable to various situations
- Suited for long battles
- Risk: Difficulty drawing key cards

### 11.3 UI Requirements

**Library Deck Edit (Detailed):**
- Full edit functionality
- Card list (filter/sort)
- Mana curve display
- Deck statistics (average cost, card type ratio)
- Multiple Loadout saves (3 sets as per existing design)

**Exploration Prep Screen Simple Edit:**
- Selection from saved Loadouts
- Minor adjustments like swapping a few cards
- "Edit in Library" button -> Navigate to Library screen

---

## 12. Exploration Preparation Screen (New Design) [DETAILED]

### 12.1 Overview

**Complete replacement of existing Dungeon Gate screen**

Abolish depth selection function, focus on following features:
1. Deck selection/simple edit
2. Equipment selection
3. Item preparation (Storage <-> Inventory)
4. Start exploration

**Design Philosophy:**
- Complete all pre-exploration preparation on single screen
- Navigate to Library when detailed editing needed
- Change depth selection to auto-progression (eliminate manual selection hassle)

### 12.2 Depth Progression System

**Auto-Progression Method:**
```
First exploration -> Depth 1
    | Clear
Next exploration -> Depth 2
    | Clear
Next exploration -> Depth 3
    ...
    | Clear
Depth 5 (Final) -> Game Clear
```

**Progression Rules:**
- Auto-start from next Depth after cleared Depth
- Retry same Depth on defeat
- Maximum reached Depth is saved
- (Future consideration) Feature to return to any Depth

**Data Management:**
```typescript
interface PlayerProgression {
  currentDepth: number;      // Currently challenging Depth (1-5)
  maxClearedDepth: number;   // Maximum cleared Depth (0-5)
  depthAttempts: number;     // Attempt count for current Depth
}
```

### 12.3 Screen Layout (Detailed)

```
+--------------------------------------------------------------------+
|  Exploration Preparation                        Next: Depth 3      |
|  ------------------------------------------------------------------|
|                                                                    |
|  +---------------------------+  +-------------------------------+  |
|  | DECK                      |  | ITEM PREPARATION              |  |
|  |                           |  |                               |  |
|  | Current Loadout:          |  | [Storage]        [Inventory]  |  |
|  | +---------------------+   |  | +-----------+  +-----------+  |  |
|  | | "Attack Build"      |   |  | |Potion     |  |           |  |  |
|  | | Cards: 32           |   |  | |Heal Herb x3| |           |  |  |
|  | | Avg Cost: 1.8       |   |  | |M.Stone(S)x5|->|           |  |  |
|  | +---------------------+   |  | |Return Stone|  |           |  |  |
|  |                           |  | |...        |  |           |  |  |
|  | Other Loadouts:           |  | +-----------+  +-----------+  |  |
|  | o Balanced                |  |                               |  |
|  | o Defense Focus           |  | Inventory: 0/20               |  |
|  |                           |  |                               |  |
|  | [Change Loadout]          |  | [Move All] [Reset]            |  |
|  | [Edit in Library ->]      |  |                               |  |
|  +---------------------------+  +-------------------------------+  |
|                                                                    |
|  +---------------------------+  +-------------------------------+  |
|  | EQUIPMENT                 |  | STATUS OVERVIEW               |  |
|  |                           |  |                               |  |
|  | Weapon: Iron Sword (+10ATK)|  | HP: 100                       |  |
|  | Armor: Leather (+5 DEF)   |  | ATK: 30 (+10)                 |  |
|  | Accessory1: Power Ring    |  | DEF: 10 (+5)                  |  |
|  | Accessory2: (None)        |  | Speed: 50                     |  |
|  |                           |  |                               |  |
|  | [Change Equipment]        |  | Deck: 32 cards                |  |
|  +---------------------------+  | Items: 0/20                   |  |
|                                 +-------------------------------+  |
|                                                                    |
|  +------------------------------------------------------------+   |
|  |                                                            |   |
|  |        START EXPLORATION (To Depth 3)                      |   |
|  |                                                            |   |
|  +------------------------------------------------------------+   |
|                                                                    |
|  [<- Return to Camp]                                               |
+--------------------------------------------------------------------+
```

### 12.4 Section Details

#### 12.4.1 Deck Section

**Display Content:**
- Currently selected Loadout name
- Total card count
- Average cost
- Other Loadout list (radio button style)

**Operations:**
| Button | Action |
|--------|--------|
| [Change Loadout] | Display Loadout selection modal |
| [Edit in Library ->] | Navigate to Library screen (preserve state for return) |

**Loadout Selection Modal:**
```
+-------------------------------------+
| Select Loadout                      |
+-------------------------------------+
| * Attack Build (32 cards)           |
| o Balanced (40 cards)               |
| o Defense Focus (28 cards)          |
+-------------------------------------+
| [Select] [Cancel]                   |
+-------------------------------------+
```

#### 12.4.2 Item Preparation Section

**Display Content:**
- Storage (warehouse) item list (left side)
- Inventory (belongings) item list (right side)
- Inventory capacity display

**Operations:**
| Operation | Action |
|-----------|--------|
| Item click | Select item |
| [->] button | Move selected item to Inventory |
| [<-] button | Return selected item to Storage |
| Drag & Drop | Directly move item |
| [Move All] | Move all Storage items to Inventory (within capacity) |
| [Reset] | Empty Inventory, return all to Storage |

**Constraints:**
- Cannot exceed Inventory capacity
- Equipped items cannot be moved (must unequip first)
- Items consumed during exploration are not returned

#### 12.4.3 Equipment Section

**Display Content:**
- Current equipment in each slot
- Stat bonuses from equipment

**Equipment Slots:**
```
Weapon: ATK modifier
Armor: DEF modifier
Accessory1: Special effect
Accessory2: Special effect
```

**Operations:**
| Button | Action |
|--------|--------|
| [Change Equipment] | Display equipment selection modal |

**Equipment Selection Modal:**
```
+---------------------------------------------+
| Select Equipment - Weapon                   |
+---------------------------------------------+
| Current: Iron Sword (+10 ATK)               |
+---------------------------------------------+
| [Owned Equipment]                           |
| o Copper Sword (+5 ATK)                     |
| * Iron Sword (+10 ATK) [Equipped]           |
| o Steel Sword (+15 ATK)                     |
| o (No Equipment)                            |
+---------------------------------------------+
| [Equip] [Cancel]                            |
+---------------------------------------------+
```

#### 12.4.4 Status Overview Section

**Display Content:**
- Base stats (HP, ATK, DEF, Speed)
- Bonus values from equipment
- Deck card count
- Inventory usage status

**Purpose:**
- Final confirmation before starting exploration
- Immediate confirmation of equipment change effects

### 12.5 Screen Transition Flow

```
BaseCamp (Main Screen)
    |
    +-- [To Exploration] -----> Exploration Prep Screen (This Screen)
    |                               |
    |                               +-- [Start Exploration] -----> Battle Screen
    |                               |                                  |
    |                               |                                  v
    |                               |                            (Victory/Defeat)
    |                               |                                  |
    |                               |                                  v
    |                               |                            Result Screen
    |                               |                                  |
    |                               |                                  v
    |                               |                              BaseCamp
    |                               |
    |                               +-- [Edit in Library] -----> Library Screen
    |                               |                              |
    |                               |                              v
    |                               |                           [Return]
    |                               |                              |
    |                               +------------------------------+
    |                               |
    |                               +-- [Return to Camp] -----> BaseCamp
    |
    +-- [Library] -----> Library Screen
```

### 12.6 State Preservation

**When transitioning Exploration Prep -> Library -> Exploration Prep:**
- Preserve Exploration Prep screen state
- Immediately reflect Library changes
- Return to Exploration Prep with "Back" button

**Implementation Method:**
```typescript
// Exploration Prep screen state
interface ExplorationPrepState {
  selectedLoadoutId: string;
  inventoryItems: string[];  // Item ID array
  equipmentSlots: EquipmentSlots;
  returnTo: 'preparation' | 'baseCamp';  // Navigation source
}

// Manage via Context or URL state
```

### 12.7 Screens to be Deprecated

**Dungeon Gate Screen (DungeonGate):**
- Depth selection function -> Changed to auto-progression
- Screen itself deprecated
- Related files:
  - `src/ui/dungeonGate/` directory
  - Related routing configuration

### 12.8 New Files to Create

```
src/ui/explorationPrep/
+-- ExplorationPrepScreen.tsx    // Main screen
+-- DeckSection.tsx              // Deck section
+-- ItemPrepSection.tsx          // Item preparation section
+-- EquipmentSection.tsx         // Equipment section
+-- StatusOverview.tsx           // Status overview
+-- LoadoutSelectModal.tsx       // Loadout selection modal
+-- EquipmentSelectModal.tsx     // Equipment selection modal
+-- hooks/
    +-- useExplorationPrep.ts    // State management hook
```

### 12.9 Design Documents to Update

| Document | Path | Update Content |
|----------|------|----------------|
| game_design_master.md | `.claude/docs/Overall_document/` | Exploration flow change, depth auto-progression |
| library_design.md | `.claude/docs/camp_document/` | Deck rule changes (15-50 cards, remove same-card limit) |
| danjeon_document | `.claude/docs/danjeon_document/` | Dungeon Gate screen deprecated, Exploration Prep screen added |
| storage_design.md | `.claude/docs/camp_document/` | Storage<->Inventory integration details |

---

## 13. Implementation Priority (Full Revision)

### Phase 1 (Immediate): Deck System Integration
1. `initialDeckConfig.ts` extension
2. `useBattleOrchestrator.ts` modification - IMPLEMENTED
3. `BattleScreen.tsx` modification
4. `CharacterClassData.ts` modification
5. `PlayerContext.tsx` modification - IMPLEMENTED

### Phase 2 (Mid-term): Design Document Updates
6. Add derivation system to `NEW_CHARACTER_SYSTEM_DESIGN.md`
7. Add derivation info to `SWORDSMAN_CARDS_40.md`
8. Add derivation tree UI + deck rule changes to `library_design.md`
9. Add growth system + exploration flow changes to `game_design_master.md`

### Phase 3 (Long-term): Card Derivation System Implementation
10. Add derivation properties to Card type
11. Implement mastery Lv-up acquisition logic
12. Implement acquisition notification UI
13. Implement Library Encyclopedia derivation tree UI

### Phase 4 (Long-term): Exploration Prep Screen Implementation
14. Deprecate Dungeon Gate screen
15. Create Exploration Prep screen (ExplorationPrepScreen.tsx)
16. Implement depth auto-progression logic
17. Implement Storage<->Inventory integration UI
18. Implement Library screen navigation with state preservation
19. Implement modals (Loadout selection, Equipment selection)

---

## Revision History

| Date | Content |
|------|---------|
| 2026-01-25 | Original plan created |
| 2026-01-26 | Integration plan created, decided on 15-card structure & BattleScreen-side fix approach |
| 2026-01-26 | Added Phase 3 card derivation system and design document update plan |
| 2026-01-26 | Added deck building system revision (15-50 cards, remove same-card limit), Exploration Prep screen design |
| 2026-01-26 | v2 created: Integrated all sections, significantly expanded Exploration Prep screen details |
| 2026-01-26 | Translated entire document to English for code compatibility |
