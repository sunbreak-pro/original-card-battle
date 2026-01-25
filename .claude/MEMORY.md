# Project Memory

## Operational Rules

**When updating MEMORY.md, always update README.md**

| Timing                                  | Response                                         |
| --------------------------------------- | ------------------------------------------------ |
| When completing a task                  | Add the date and summary of changes to README.md |
| When adding or modifying major features | Record the changes in README.md                  |

---

## Quick Reference

- **Current Phase:** Phase 6 Complete (Lives System, Character Select, Library)
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-01-25

---

## Active Tasks

### Bugs / Issues
- **FacilityHeader unused props** - ESLint errors for `subtitle`, `icon`, `playerData`
  - Decision needed: (A) Simplify - remove unused props, or (B) Implement all variants
  - Callers passing ignored props: Storage, Shop, Blacksmith

### Deferred Features
- Mage/Summoner classes - cards not yet implemented (shows "Coming Soon")
- Sanctuary effects - not connected to battle/dungeon systems
- Guild RumorsTab/QuestsTab - placeholders only
- Shop daily sales system, pack opening animation
- Dungeon event/rest/treasure nodes - complete immediately (no UI)

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
