# Documentation Index

This directory contains game design specifications and implementation details for the card battle game.

## Update History

| Date | Content |
|------|---------|
| 2026-02-04 | Updated for facility consolidation (7 → 5). Removed deprecated files. |

## Directory Structure

| Directory | Description |
|-----------|-------------|
| `Overall_document/` | High-level game design and architecture |
| `battle_document/` | Battle system mechanics, phases, damage calculation |
| `camp_document/` | Camp facilities (Shop, Guild, Sanctuary, Blacksmith, Dungeon Gate) |
| `card_document/` | Card system, mastery, derivation mechanics |
| `danjeon_document/` | Dungeon exploration, map generation, nodes |
| `enemy_document/` | Enemy definitions, AI patterns, boss design |
| `item_document/` | Items, equipment, consumables |
| `journal_document/` | Journal system (header UI for deck/encyclopedia/settings) |
| `util_doument/` | Utility systems and helpers |

## Key Documents

### Battle System
- `battle_document/` - Combat flow, phase execution, buff/debuff mechanics

### Card System
- `card_document/` - Card types, mastery progression, derivation unlocks

### Camp Facilities (5 Facilities)
- `camp_document/camp_facilities_design.md` - Master design (V4.0)
- `camp_document/guild_design.md` - Guild with integrated Storage tab (V3.0)
- `camp_document/shop_design.md` - Shop economy
- `camp_document/blacksmith_design.md` - Equipment enhancement
- `camp_document/sanctuary_design.md` - Soul remnant skill tree

### Journal System (Header UI)
- `journal_document/journal_system_implementation_plan.md` - Deck building, encyclopedia, settings

### Equipment
- `ap-equipment-system.md` - AP (Armor Point) system, equipment durability

## Related References

- **CLAUDE.md** (project root) - Development commands, architecture overview, conventions
- **README.md** - Project overview, development history
- **.claude/feature_plans/** - Future feature plans (quests, titles, NPC, dark market)
- **.claude/memories/** - Lessons learned and completed refactoring guides
