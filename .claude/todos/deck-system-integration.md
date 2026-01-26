# Card Deck System Integration Improvement Plan

Date Created: 2026-01-26
Status: Awaiting Plan Approval
Original Plan: Card Deck System Improvement Plan (2026-01-25)

---

## 1. Current Status Analysis Summary

### 1.1 Identified Issues

| Issue                            | Impact | Confirmed Files                                                         |
| -------------------------------- | ------ | ----------------------------------------------------------------------- |
| Duplicate Data Source Definition | High   | `CharacterClassData.ts` (5 cards) vs. `initialDeckConfig.ts` (20 cards) |
| Unused PlayerContext.deck        | High   | Completely Ignoring `useBattleOrchestrator.ts`                          |
| Hard-Coding in BattleScreen      | Medium | Direct Reference to `Swordman_Status`                                   |
| Lack of Class Support            | Medium | Always Generates a Fixed Swordsman Deck                                 |

### 1.2 Current Data Flow (Problem)

```
CharacterSelect.tsx
↓ initializeWithClass(classType)
PlayerContext
↓ classInfo.starterDeck → playerState.deck (5 cards)
↓ Stored in playerData.persistent.deckCardIds
BattleScreen.tsx
↓ deckCardIds not loaded ← [Disconnect Point]
↓ Create initialPlayerState (hardcode Swordman_Status)
useBattleOrchestrator.ts
↓ Use INITIAL_DECK_COUNTS (20 cards, fixed for Swordsman)
createInitialDeck()
↓ Always the same 20-card deck
```

---

## 2. Integration Policy

### 2.1 Decisions

| Item                    | Decision                            | Reason                                         |
| ----------------------- | ----------------------------------- | ---------------------------------------------- |
| Initial Deck Size       | **15 cards**                        | Phase Eliminates the need for adjustments in 2 |
| Data source integration | **initialDeckConfig.ts** is correct | Structure suitable for managing deck count     |
| Data flow corrections   | **BattleScreen side corrections**   | Utilizes PlayerContext.deck                    |

### 2.2 Target data flow (after improvements)

```
CharacterSelect.tsx
↓ initializeWithClass(classType)
PlayerContext
↓ Reference INITIAL_DECK_BY_CLASS[classType]
↓ Store in playerData.persistent.deckCardIds
BattleScreen.tsx
↓ Get deckCardIds from playerData ← [Modifications]
↓ Include in initialPlayerState
useBattleOrchestrator.ts
↓ Use initialPlayerState.deckCardIds ← [Modifications]
createInitialDeck()
↓ Correct Decks for Each Class
```

---

## 3. Phase 1: Immediate Actions (Unified Version)

### 3.1 List of Changed Files

| File                       | Change Type  | Summary                                                                        |
| -------------------------- | ------------ | ------------------------------------------------------------------------------ |
| `initialDeckConfig.ts`     | Expansion    | Character-specific settings changed to 15 cards                                |
| `CharacterClassData.ts`    | Modification | Removed starterDeck and changed to referencing initialDeckCounts               |
| `useBattleState.ts`        | Modification | Added deckConfig to InitialPlayerState type                                    |
| `useBattleOrchestrator.ts` | Modification | Receive deck settings from initialPlayerState                                  |
| `BattleScreen.tsx`         | Modification | Retrieve deck information from playerData and include it in initialPlayerState |
| `PlayerContext.tsx`        | Modification | Use INITIAL_DECK_BY_CLASS with initializeWithClass                             |

### 3.2 initialDeckConfig.ts Design Changes

**Changes:**

- Added `INITIAL_DECK_BY_CLASS` (character-specific settings)
- Adjusted Swordsman's deck from 20 to 15
- Maintained `INITIAL_DECK_COUNTS` as an alias for backward compatibility

**15-card deck (Swordsman):**

```
sw_001: 3 // Quick Slash (4 → 3)
sw_003: 2 // Combo Strike (3 → 2)
sw_007: 2 // Slash (3 → 2)
sw_013: 2 // Sword Focus
sw_037: 2 // Sword Barrier
sw_038: 2 // Counter Stance
sw_014: 2 // Meditation
-----------
Total: 15 cards
```

**Cards Removed:**

- `sw_027` (Sword Energy Release): Too strong early on. Added in the Phase 3 learning system.

### 3.3 CharacterClassData.ts Design Changes

**Changes:**

- Removed `starterDeck: Card[]`
- Added `initialDeckCounts: Record<string, number>`
- Added `totalCards: number` (for UI display)
- Added `getStarterDeckStacks()` function (for stack display)

**Newly Added Functions:**

- `getStarterDeckStacks(classType)`: Returns card information in stack format for the UI.
- `getCardDataByClass(classType)`: Returns an array of card data by class.

### 3.4 BattleScreen.tsx Design Changes

**Changes:**

- Obtained `playerData.persistent.deckCardIds`
- Added `deckCardIds` or `deckConfig` to `initialPlayerState`
- Changed hard-coding of `Swordman_Status` to a reference to `playerData`

**Modifications (around lines 88-106):**

```typescript
// Current: Hard-coding Swordman_Status
// After: Retrieving from playerData
const initialPlayerState = useMemo<InitialPlayerState>(() => ({
currentHp: runtimeState.currentHp,
currentAp: runtimeState.currentAp,
maxHp: playerData.persistent.baseMaxHp,
maxAp: playerData.persistent.baseMaxAp,
name: playerData.persistent.name,
playerClass: playerData.persistent.playerClass,
classGrade: playerData.persistent.classGrade,
speed: playerData.persistent.baseSpeed,
cardActEnergy: 3, // TODO: Add to PlayerData
cardMasteryStore: runtimeState.cardMasteryStore,
deckConfig: getInitialDeckCounts(playerData.persistent.playerClass),
}), [...]);
```

### 3.5 useBattleOrchestrator.ts Design Changes

**Changes:**

- Added `deckConfig` to the `InitialPlayerState` type
- Fixed the `initialDeckState` generation logic

**Changes (around lines 169-178):**

```typescript
// Current: Fixed INITIAL_DECK_COUNTS
// After: Use initialPlayerState.deckConfig
const initialDeckState = useMemo(() => {
  const deckCounts = initialPlayerState?.deckConfig ?? INITIAL_DECK_COUNTS;
  const cardData = getCardDataByClass(
    initialPlayerState?.playerClass ?? "swordsman",
  );
  let initialDeck = createInitialDeck(deckCounts, cardData);

  if (
    initialPlayerState?.cardMasteryStore &&
    initialPlayerState.cardMasteryStore.size > 0
  ) {
    initialDeck = applyMasteryToCards(
      initialDeck,
      initialPlayerState.cardMasteryStore,
    );
  }
  return { hand: [], drawPile: initialDeck, discardPile: [] };
}, [initialPlayerState]);
```

### 3.6 PlayerContext.tsx Design Changes

**Changes:**

- Use INITIAL_DECK_BY_CLASS in `initializeWithClass`
- Fixed the generation logic for `playerData.persistent.deckCardIds`

**Changes (around lines 313-342):**

```typescript
// Generate a deck from INITIAL_DECK_BY_CLASS
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

### 4.1 Proposal 1: Converting cardActEnergy to PlayerData

**Current Status** `Swordman_Status.cardActEnergy` is hard-coded
**Proposal** Add to `PlayerData.persistent.cardActEnergy`

**Affected Files**

- `playerTypes.ts`: PlayerData type definition
- `PlayerContext.tsx`: playerData generation
