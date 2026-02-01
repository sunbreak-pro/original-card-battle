# Overall Codebase Summary

## Overview

This is a React 19 + TypeScript card battle roguelike game with dungeon exploration. Players select from three classes (Swordsman, Mage, Summoner), navigate procedural dungeon maps, engage in turn-based card battles against depth-tiered enemies, and manage resources/equipment between runs at a base camp.

## Analysis File Index

### Session 1: Battle Core

| File                                     | Scope                                         |
| ---------------------------------------- | --------------------------------------------- |
| `.claude/code/battle/orchestration.md`   | useBattleOrchestrator, phase queue, turn flow |
| `.claude/code/battle/damage-and-buff.md` | Damage calculation, buff/debuff system, DoT   |

### Session 2: Battle Execution

| File                                     | Scope                                                    |
| ---------------------------------------- | -------------------------------------------------------- |
| `.claude/code/battle/card-execution.md`  | Card play resolution, multi-hit, energy                  |
| `.claude/code/battle/class-abilities.md` | useSwordEnergy, useElementalChain, useSummonSystem hooks |

### Session 3: State & Economy

| File                                   | Scope                                                     |
| -------------------------------------- | --------------------------------------------------------- |
| `.claude/code/state/context-system.md` | Context hierarchy, save system, GameState routing         |
| `.claude/code/resource/economy.md`     | Gold dual-pool, magic stones, shop, blacksmith, sanctuary |

### Session 4: Characters

| File                               | Scope                                                |
| ---------------------------------- | ---------------------------------------------------- |
| `.claude/code/character/player.md` | PlayerContext, class abilities, title system         |
| `.claude/code/character/enemy.md`  | Enemy definitions, AI patterns, energy-based actions |

### Session 5: Cards & Inventory

| File                                            | Scope                                                   |
| ----------------------------------------------- | ------------------------------------------------------- |
| `.claude/code/cards/deck-and-mastery.md`        | 120 cards, deck reducer, mastery levels                 |
| `.claude/code/inventory/equipment-and-items.md` | Equipment AP, durability, consumables, InventoryContext |

### Session 6: Dungeon & Summary

| File                                     | Scope                                                      |
| ---------------------------------------- | ---------------------------------------------------------- |
| `.claude/code/dungeon/dungeon-system.md` | Map generation, node navigation, events, DungeonRunContext |
| `.claude/code/overall-summary.md`        | This file — cross-system overview                          |

## Architecture Overview

### Context Provider Hierarchy

```
GameStateProvider (screen routing, depth, battleMode)
  → ResourceProvider (gold dual-pool, magic stones, exploration limit)
    → PlayerProvider (persistent data, runtime battle state, deck, lives)
      → InventoryProvider (items, equipment, storage, movement)
        → DungeonRunProvider (dungeon state, floor maps, node progression)
```

### System Interaction Map

```
                        ┌─────────────┐
                        │ GameState   │
                        │ (routing)   │
                        └──────┬──────┘
                               │
           ┌───────────────────┼───────────────────┐
           ▼                   ▼                   ▼
    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
    │ Camp System │    │   Dungeon   │    │   Battle    │
    │             │    │   System    │    │   System    │
    ├─────────────┤    ├─────────────┤    ├─────────────┤
    │ Shop        │    │ Map Gen     │    │ Orchestrator│
    │ Blacksmith  │◄──►│ Node Events │───►│ Phase Queue │
    │ Sanctuary   │    │ Floor Prog. │    │ Card Exec.  │
    └──────┬──────┘    └─────────────┘    │ Class Ability│
           │                              │ Enemy AI    │
           ▼                              └──────┬──────┘
    ┌─────────────┐                               │
    │ Resources   │◄──────────────────────────────┘
    │ Inventory   │     (battle rewards, loot)
    │ Equipment   │
    └─────────────┘
```

### Data Flow: Full Game Loop

```
1. Character Select → PlayerContext initialized with class base stats
2. Camp → Shop/Blacksmith/Sanctuary spend resources
3. Dungeon Entry → DungeonRunContext creates floor map
4. Node Selection → Navigate forward through map
5. Battle Node → useBattleOrchestrator manages full battle:
   a. Deck created from player config + mastery
   b. Phase queue generated from speed comparison
   c. Player plays cards → damage calc → class ability modifiers
   d. Enemy AI selects actions → energy-based execution
   e. Buff/debuff durations tick per phase
   f. Battle result → rewards/defeat
6. Non-Battle Node → Rest (HP%), Treasure (loot table), Event (random)
7. Boss Defeat → Floor complete → advance or return
8. Dungeon Exit → Transfer exploration resources to baseCamp (multiplier)
9. Death → Lose exploration resources, keep baseCamp
```

### Key Type Hierarchies

```
Character Types:
  CharacterClass: "swordsman" | "mage" | "summoner"
  ClassAbilityState: SwordEnergyState | ElementalState | SummonState
  BattleStats: { hp, maxHp, ap, maxAp, guard, speed, buffDebuffs }

Card Types:
  Card: { id, cardTypeId, name, cost, category, baseDamage, element[], ... }
  DeckState: { drawPile, discardPile, hand }

Battle Types:
  BuffDebuffMap: Map<string, BuffDebuffState>
  PhaseQueue: { phases, entries, currentIndex }
  DamageModifier: { flatBonus, percentMultiplier, critBonus, penetration }

Item Types:
  Item: { id, name, itemType, rarity, stats?, effect?, ... }
  EquipmentSlot: "weapon" | "armor" | "helmet" | "boots" | "accessory1" | "accessory2"

Dungeon Types:
  DungeonRun: { runId, currentFloor, floorNumber, isActive }
  DungeonNode: { id, type, row, column, connections, status }
```

## Cross-System Issues Summary

### Critical Bug Risks

| ID                     | Location                           | Issue                                                       |
| ---------------------- | ---------------------------------- | ----------------------------------------------------------- |
| V-CS04                 | `PlayerContext.tsx:772`            | Player ID regenerates on every useMemo recomputation        |
| V-CS06                 | `saveManager.ts:193-232`           | Save system misses inventory items and equipmentInventory   |
| V-CS11                 | `InventoryContext.tsx:48`          | Stale playerData in rapid successive operations             |
| V-EC01                 | `shopLogic.ts:83-124`              | Magic stone exchange can overpay without warning            |
| V-EC03                 | `blacksmithLogic.ts:224-285`       | Quality upgrade consumes resources on failure               |
| Orchestrator stale ref | `useBattleOrchestrator.ts:550-608` | Stale closure in executeNextPhaseImpl ref update gap        |
| Enemy preview mismatch | `enemyActionExecution.ts:81-114`   | Preview uses different random results than actual execution |

### Architectural Concerns

| Area                       | Issue                                                            |
| -------------------------- | ---------------------------------------------------------------- |
| PlayerContext (938 lines)  | Single-responsibility violation — handles 6+ concerns            |
| Orchestrator (882 lines)   | Largest file; all battle features require modifying it           |
| DungeonRunContext location | Lives in `src/ui/` instead of `src/contexts/`                    |
| Summoner system            | Entirely STUB — class is playable but ability has minimal effect |
| Test data in production    | TestItemsData and hardcoded resources loaded as initial state    |
| Save migration             | `migrate()` is a stub — format changes corrupt saves             |

### Incomplete/Stub Systems

| System               | Status         | Details                                               |
| -------------------- | -------------- | ----------------------------------------------------- |
| Summoner abilities   | STUB           | Only 3 hardcoded summons; no real summon actions      |
| Equipment durability | Partial        | Types and stat calc exist; no degradation in battle   |
| Title system         | Disconnected   | Functions exist but `cardTypeCount` not tracked       |
| Save migration       | Stub           | Version stamped but no actual migration logic         |
| useEnemyAI hook      | Wrapper only   | Stateless; pure function wrapper for React convention |
| Daily shop rotation  | No persistence | `dayCount` tracking mechanism not visible             |

### Performance Observations

| Area                  | Detail                                                         |
| --------------------- | -------------------------------------------------------------- |
| Sanctuary effects     | Recalculated from scratch on every call (25 nodes, negligible) |
| Inventory operations  | Full playerData object spread per operation                    |
| Phase queue expansion | Rebuilds per-enemy entries each phase check                    |
| Card derivation       | Per-card effective power calculated on demand                  |

## File Count Summary

| Directory                    | Files Analyzed | Purpose                                    |
| ---------------------------- | -------------- | ------------------------------------------ |
| `src/types/`                 | 5              | Type definitions                           |
| `src/contexts/`              | 3              | React context providers                    |
| `src/domain/battles/`        | 12             | Battle logic, execution, calculators       |
| `src/domain/characters/`     | 8              | Player/enemy logic, class abilities        |
| `src/domain/cards/`          | 7              | Deck, mastery, card utilities              |
| `src/domain/item_equipment/` | 3              | Item generation, equipment stats           |
| `src/domain/dungeon/`        | 3              | Map generation, events                     |
| `src/domain/camps/`          | 4              | Shop, blacksmith, sanctuary                |
| `src/constants/data/`        | 14             | Static data (cards, enemies, items, camps) |
| `src/ui/dungeonHtml/`        | 1              | DungeonRunContext                          |
| **Total**                    | **~60 files**  |                                            |

## Naming Conventions Observed

| Pattern                | Examples                                                          |
| ---------------------- | ----------------------------------------------------------------- |
| Types: PascalCase      | `BattleStats`, `DungeonNode`, `CardCategory`                      |
| Functions: camelCase   | `calculateDamage`, `generateFloorMap`, `addSwordEnergy`           |
| Constants: UPPER_SNAKE | `SWORD_ENERGY_MAX`, `GUARD_INIT_MULTIPLIER`                       |
| UI text: Japanese      | "剣気 MAX", "基本攻撃", "休憩地点"                                |
| Code/comments: English | All variable names, JSDoc, inline comments                        |
| Known typos            | `deckReducter` (reducer), `tittle` (title), `deptManager` (depth) |
