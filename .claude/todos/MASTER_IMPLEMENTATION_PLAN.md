# Master Implementation Plan

> Unified task management document — only remaining/future work listed here.
> Last Updated: 2026-01-30

---

## Current Status

All three development phases (A, B, C) are nearly complete.

| Category | Status | Notes |
|----------|--------|-------|
| Battle System | 95% | Core, multi-enemy, escape, element system done. AoE pending |
| Camp Facilities | 95% | Shop, Guild, Library, Blacksmith, Storage, Sanctuary all working |
| Dungeon System | 90% | Map, nodes, events, 5-floor progression, depth 1-5 |
| Progression System | 95% | Lives, souls, sanctuary, equipment, mastery, card derivation |
| Save System | Implemented | `src/domain/save/logic/saveManager.ts` |

---

## Remaining Tasks

### Priority: LOW (Code Quality)

| ID | Task | Location | Details |
|----|------|----------|---------|
| Q1 | Replace EnemyFrame emoji icons with SVG | `src/ui/battleHtml/EnemyFrame.tsx:22-35` | Action type icons use emoji (TODO comment exists in code) |
| Q2 | Remove unused `variant` prop from FacilityHeader | `src/ui/componentsHtml/FacilityHeader.tsx:23` | Prop accepted but never used; always renders "basecamp-variant" |

### Priority: LOW (Future Features)

| ID | Task | Details |
|----|------|---------|
| F1 | AoE card support | Cards that damage all enemies simultaneously. No logic exists yet. Requires: card type flag, damage loop over all enemies, UI feedback |
| F2 | Teleport stone item | A4 noted "teleport stone item not yet implemented" — item for instant dungeon return |

---

## Architecture Reference

### Context Provider Hierarchy

```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```

Battle-specific contexts (created per battle):
```
PlayerBattleContext + EnemyBattleContext + BattleSessionContext
(managed by BattleProviderStack.tsx)
```

### State Management

```
Permanent State (survives battles):
├── PlayerContext: persistent data, resources, progression
├── InventoryContext: items, equipment
└── ResourceContext: gold, magic stones

Battle State (reset each battle):
├── PlayerBattleContext: HP, AP, buffs
├── EnemyBattleContext: enemy state (multi-enemy array)
└── BattleSessionContext: flow control

Run State (reset each exploration):
└── DungeonRunProvider: map, progress, temp items
```

### Core Game Loop

```
BaseCamp (Safe Zone)
    ↓ Start Exploration
Dungeon (Danger Zone)
    ↓ Battle → Gain Souls/Items
    ↓ Decision: Continue or Return
    ├── Survive → Keep Everything + Souls
    └── Death → Lose Items + Souls Saved + Lives -1
         ↓
    Lives > 0 → Return to BaseCamp
    Lives = 0 → Game Over (Full Reset)
```

---

## Archived Design Decisions

### Why No Character Interface

- Player and Enemy are not interchangeable (violates LSP)
- Common properties handled by `BattleStats`
- Avoids `type: 'player' | 'enemy'` branching

### Deck System

- Initial deck: 15 cards per class
- `initialDeckConfig.ts` is source of truth
- `CharacterClassData.ts` references, not duplicates

### Soul System V3.0

- Souls are saved 100% on BOTH survival AND death
- Death penalty = item loss + life loss (already significant)
- Game Over is the true reset point

---

## Revision History

| Date | Changes |
|------|---------|
| 2026-01-26 | Initial plan created, Phase A completed |
| 2026-01-28 | Type refactoring, shop refactoring completed |
| 2026-01-29 | Phase B completed, Phase C nearly completed (C1-C5 + B4 done) |
| 2026-01-30 | **Full cleanup:** Removed all completed tasks. C3 (Multi-Enemy) confirmed complete. B1 (Context Separation) confirmed complete. FIX-1/2/3 confirmed resolved. Only remaining: AoE cards, EnemyFrame SVG, FacilityHeader prop, teleport stone |
