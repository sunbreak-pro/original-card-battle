# Project Memory

## Operational Rules

**When updating MEMORY.md, always update README.md**

| Timing                                  | Response                                         |
| --------------------------------------- | ------------------------------------------------ |
| When completing a task                  | Add the date and summary of changes to README.md |
| When adding or modifying major features | Record the changes in README.md                  |

---

- Write all design documents and code in English
- Please output only user chat and README.md in Japanese.

## Quick Reference

- **Current Phase:** Phase 6 Complete + Deck System Integration
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-01-26

---

## Active Tasks

### Recently Completed

- **Phase 1: Buff/Debuff Ownership System (2026-01-26)** - Fixed duration timing bug
  - Added `BuffOwner` type (`'player' | 'enemy' | 'environment'`) to `baffType.ts`
  - Added `appliedBy` field to `BuffDebuffState` (optional for backward compatibility)
  - New `decreaseBuffDebuffDurationForPhase(map, currentActor)` in `buffLogic.ts`
  - Deprecated old `decreaseBuffDebuffDuration()` function
  - **Fixed critical bug:** Enemy debuffs now decrease at enemy phase, not player phase
  - **Fixed parameter order bug** in `enemyPhaseExecution.ts:applyEnemyDebuffsToPlayer()`
  - Files modified: `baffType.ts`, `buffLogic.ts`, `playerPhaseExecution.ts`, `enemyPhaseExecution.ts`, `useCardExecution.ts`

- **Mage Character Selection Enabled (2026-01-26)** - Mage class now playable
  - Added `MAGE_CARDS` import and `createMageStarterDeck()` function
  - `getCardDataByClass()` returns `MAGE_CARDS` for mage
  - Updated mage entry: `isAvailable: true`, `uniqueMechanic: "Elemental Resonance"`
  - 15-card starter deck with fire/ice/lightning/light elements
  - File modified: `CharacterClassData.ts`

- **Character Select: Unique Card Display (2026-01-26)** - Show one card of each type in starter deck preview
  - `StarterDeckPreview.tsx` now filters to unique cards by `cardTypeId`
  - Header still shows total deck size ("15 cards")
  - Displays 7 unique card types instead of 15 cards with duplicates

- **Deck System Integration (2026-01-26)** - PlayerContext deck now flows to battle correctly
  - `INITIAL_DECK_BY_CLASS` replaces hardcoded 20-card deck with 15-card class-specific decks
  - `BattleScreen.tsx` uses `playerData.persistent` instead of `Swordman_Status`
  - `useBattleOrchestrator.ts` uses `initialPlayerState.deckConfig`
  - Files modified: `initialDeckConfig.ts`, `useBattleState.ts`, `BattleScreen.tsx`, `useBattleOrchestrator.ts`, `CharacterClassData.ts`

### Bugs / Issues

- **FacilityHeader unused props** - ESLint errors for `subtitle`, `icon`, `playerData`
  - Decision needed: (A) Simplify - remove unused props, or (B) Implement all variants
  - Callers passing ignored props: Storage, Shop, Blacksmith

### Deferred Features

- Summoner class - cards not yet implemented (shows "Coming Soon")
- Sanctuary effects - not connected to battle/dungeon systems
- Guild RumorsTab/QuestsTab - placeholders only
- Shop daily sales system, pack opening animation
- Dungeon event/rest/treasure nodes - complete immediately (no UI)

### Current Refactoring Status

| Phase   | Status      | Description                  |
| ------- | ----------- | ---------------------------- |
| Phase 1 | ✅ Complete | Buff/Debuff Ownership System |
| Phase 2 | Not Started | Context Separation           |
| Phase 3 | Not Started | Legacy Interface Deletion    |
| Phase 4 | Not Started | Multi-Enemy Battle           |

---

## Critical Lessons Learned

**CSS Class Name Collision:**
Generic class names (`.card`, `.enemy-card`) must be scoped with parent element:

```css
.battle-screen .enemy-card { ... }  /* Good */
.enemy-card { ... }                  /* Bad - conflicts */
```

**Context Provider Scope:**
State persisting across screens (dungeon → battle → dungeon) requires provider high in tree.

**React Hooks Rules:**
All hooks must be called at top level, before any conditional returns.

**Language Consistency:**
UI uses Japanese (e.g., "見習い剣士") - verify consistency across PlayerData and type definitions.

---

## See Also

- **Game Design:** `.claude/docs/`
- **Battle Logic:** `.claude/docs/battle_document/battle_logic.md`
