---
name: design-research
description: Quickly search and reference game design documents. Use when checking specifications before implementation or researching design details. Triggers on "check X specs", "search design docs", "how is X designed?" requests. 使用時: 「design-researchを使用します」
---

# Design Research Skill

Provides shortest path to design docs. Load only what's needed, not everything.

## Quick Index

### Core Systems
| Topic | File | Grep Pattern |
|-------|------|--------------|
| Overall game design | `Overall_document/game_design_master.md` | - |
| Lives system | `Overall_document/DESIGN_CHANGE_PLAN_lives_system.md` | `lives\|残機\|探索回数` |
| Battle flow | `battle_document/battle_logic.md` | `phase\|フェーズ\|ターン` |
| Buff/Debuff | `battle_document/buff_debuff_system.md` | `buff\|debuff\|duration` |

### Character Classes
| Topic | File | Grep Pattern |
|-------|------|--------------|
| Swordsman cards | `card_document/SWORDSMAN_CARDS_40.md` | `剣気\|sword_` |
| Mage cards | `card_document/MAGE_CARDS_40.md` | `共鳴\|element\|火\|水\|雷` |
| Summoner cards | `card_document/SUMMONER_CARDS_40.md` | `召喚\|summon` |
| Common cards | `card_document/COMMON_CARDS_20.md` | `common_` |
| Build system | `card_document/NEW_CHARACTER_SYSTEM_DESIGN.md` | `ビルド\|build` |

### Enemies by Depth
| Depth | File | Grep Pattern |
|-------|------|--------------|
| Depth 1 | `enemy_document/depth1_enemy_database.md` | enemy name |
| Depth 2 | `enemy_document/depth2_enemy_database.md` | enemy name |
| Depth 3 | `enemy_document/depth3_enemy_database.md` | enemy name |
| Depth 4 | `enemy_document/depth4_enemy_database.md` | enemy name |
| Depth 5 (Boss) | `enemy_document/depth5_boss_database.md` | boss name |
| Boss system | `enemy_document/boss_system_redesign.md` | `ボス\|boss` |

### Camp Facilities
| Facility | Design | Implementation |
|----------|--------|----------------|
| Shop | `shop_design.md` | `shop_implementation_guide.md` |
| Blacksmith | `blacksmith_design.md` | `blacksmith_implementation_guide.md` |
| Guild | `guild_design.md` | `guild_implementation_guide.md` |
| Library | `library_design.md` | - |
| Sanctuary | `sanctuary_design.md` | - |
| Storage | `storage_design.md` | - |
| Overview | `camp_facilities_design.md` | - |

### Other Systems
| Topic | File | Grep Pattern |
|-------|------|--------------|
| Equipment/Items | `item_document/EQUIPMENT_AND_ITEMS_DESIGN.md` | `装備\|equipment\|item` |
| Dungeon UI | `danjeon_document/dungeon_exploration_ui_design_v3.0.md` | `node\|ノード\|map` |
| Return system | `danjeon_document/return_system_design.md` | `帰還\|return\|teleport` |
| Inventory | `util_doument/inventory_design.md` | `inventory\|所持品` |

## Search Commands

```bash
# Search specific topic
grep -r "keyword" .claude/docs/ --include="*.md"

# Search within specific file
grep -n "keyword" .claude/docs/battle_document/battle_logic.md

# List files
ls -la .claude/docs/*/
```

## Research Workflow

1. **Quick Lookup**: Find relevant file from index above
2. **Targeted Read**: Read only the specific section needed
3. **Deep Dive**: Load additional files only if necessary

## File Size Reference

| Category | Total Size | Files |
|----------|------------|-------|
| battle_document | ~48K | 2 |
| card_document | ~77K | 5 |
| camp_document | ~286K | 10 |
| enemy_document | ~111K | 6 |
| item_document | ~42K | 1 |
| danjeon_document | ~49K | 2 |
| Overall_document | ~36K | 2 |

**Tip**: camp_document is large due to implementation guides. For overview only, read `*_design.md` files.

## Common Queries

| Question | Reference |
|----------|-----------|
| Damage formula? | `battle_logic.md` → "ダメージ計算" |
| Card cost balance? | Each card file → "コスト" |
| Enemy stat guidelines? | `depth*_enemy_database.md` |
| When do lives decrease? | `DESIGN_CHANGE_PLAN_lives_system.md` |
| How many equipment slots? | `EQUIPMENT_AND_ITEMS_DESIGN.md` → "スロット" |
| Soul acquisition conditions? | `sanctuary_design.md` or `return_system_design.md` |

## Base Path

```
.claude/docs/
├── Overall_document/
├── battle_document/
├── card_document/
├── camp_document/
├── enemy_document/
├── item_document/
├── danjeon_document/
└── util_doument/
```
