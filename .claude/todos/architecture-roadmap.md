# Architecture Refactoring Roadmap

## Design Principles

This refactoring is based on the following principles:

1. **SOLID Principle Compliance**
2. **Separation of Concerns**: Battle-time vs Camp-time responsibility separation
3. **Loose Coupling**: Minimize dependencies between Contexts
4. **Gradual Migration**: Progress without breaking existing code

## Important Design Decisions

### Do NOT Create Character Interface

**Reasons:**
- Player and Enemy are not "interchangeable" (would violate LSP)
- Common points are sufficient with `BattleStats`
- Creating Character would require `type: 'player' | 'enemy'` branching, which is a design mistake

**Structure to Maintain:**
```
BattleStats (common battle stats interface)
├── PlayerBattleState extends BattleStats
└── EnemyBattleState extends BattleStats
```

### Player and ExtendedPlayer Integration

**Policy: Delete ExtendedPlayer and fully migrate to PlayerData**

```
To Be Deleted:
- Player (legacy interface)
- ExtendedPlayer (legacy interface)

To Maintain/Evolve:
- PlayerData (new architecture)
  ├── persistent: PlayerPersistentData
  ├── resources: PlayerResources
  ├── inventory: PlayerInventory
  └── progression: PlayerProgression
- PlayerBattleState (battle-time only)
```

---

## Phase 1: Buff/Debuff Ownership System ✅ Design Complete

**Purpose:** Fix buff/debuff duration reduction timing

**Details:** See `phase1-buff-debuff-ownership.md`

**Status:** Design Complete - Awaiting Implementation

---

## Phase 2: Context Separation (Gradual Implementation)

### Phase 2.1: PlayerBattleContext Separation

**Purpose:** Separate battle-time state management from PlayerContext

**Current State:**
```
PlayerContext
├── persistent data (camp)
├── resources (camp)
├── inventory (camp)
├── progression (camp)
└── runtimeBattleState (battle) ← Separate this
```

**Goal:**
```
PlayerContext (camp-only)
├── persistent data
├── resources
├── inventory
└── progression

PlayerBattleContext (battle-only) ← NEW
├── hp, ap, guard
├── buffDebuffs
├── cardEnergy
├── deck state
└── classAbility state
```

**Impact Scope:**
- `domain/camps/contexts/PlayerContext.tsx`
- `domain/battles/contexts/PlayerBattleContext.tsx` (new file)
- `ui/battleHtml/BattleScreen.tsx`
- `domain/battles/managements/useBattleState.ts`

### Phase 2.2: EnemyBattleContext Creation

**Purpose:** Enemy state management with multi-enemy support in mind

**Goal:**
```
EnemyBattleContext (battle-only)
├── enemies: EnemyBattleState[]
├── activeEnemyIndex: number
├── getActiveEnemy(): EnemyBattleState
├── updateEnemy(instanceId, updates): void
└── removeEnemy(instanceId): void
```

**Multi-enemy Extensibility:**
- Manage multiple enemies with `enemies` array
- Target selection with `activeEnemyIndex`
- Process AoE attacks with `enemies.forEach()`

### Phase 2.3: BattleSessionContext Creation

**Purpose:** Centralize battle flow control

**Goal:**
```
BattleSessionContext
├── battleId: string
├── phaseQueue: PhaseQueue
├── currentPhaseIndex: number
├── battleStatus: 'active' | 'victory' | 'defeat'
├── advancePhase(): void
├── endBattle(result): void
└── restartBattle(): void
```

---

## Phase 3: Legacy Interface Deletion

### Phase 3.1: Delete Player Interface

**Steps:**
1. Confirm full migration to `PlayerData`
2. Replace all `Player` usages with `PlayerData.persistent`
3. Delete `Player` interface

### Phase 3.2: Delete ExtendedPlayer Interface

**Steps:**
1. Change `PlayerContext` internal implementation to `PlayerData` based
2. Update all `ExtendedPlayer` usages
3. Delete `ExtendedPlayer` interface

### Phase 3.3: Type File Cleanup

**Goal Structure:**
```
domain/characters/type/
├── baseTypes.ts          - BattleStats, CharacterClass
├── playerTypes.ts        - PlayerData, PlayerBattleState only
└── enemyType.ts          - EnemyDefinition, EnemyBattleState only

To Delete:
- characterType.ts (re-export file)
```

---

## Phase 4: Multi-Enemy Battle System

**Implement after Phase 2 completion**

### Design Policy

```
EnemyBattleContext
├── enemies: EnemyBattleState[]
│   ├── [0] Goblin (hp: 30, instanceId: 'enemy_0')
│   ├── [1] Orc (hp: 50, instanceId: 'enemy_1')
│   └── [2] Skeleton (hp: 20, instanceId: 'enemy_2')
├── targetingMode: 'single' | 'all' | 'random'
└── currentTarget: number (index)
```

### UI Support

```
BattleScreen
├── EnemyRow (horizontal display)
│   ├── EnemyDisplay [0] ← Target selectable
│   ├── EnemyDisplay [1]
│   └── EnemyDisplay [2]
└── TargetSelector (target selection during card selection)
```

### Phase Queue Extension

```
Current: PhaseActor = 'player' | 'enemy'
Future: PhaseActor = 'player' | `enemy_${number}` | 'all_enemies'

Phase Queue Example:
[player, enemy_0, enemy_1, player, enemy_2, player, ...]
```

---

## Implementation Priority

| Phase | Content | Priority | Dependencies |
|-------|---------|----------|--------------|
| 1 | Buff/Debuff Ownership | High | None |
| 2.1 | PlayerBattleContext Separation | Medium | Phase 1 |
| 2.2 | EnemyBattleContext Creation | Medium | Phase 1 |
| 2.3 | BattleSessionContext Creation | Medium | Phase 2.1, 2.2 |
| 3.1 | Delete Player | Low | Phase 2 Complete |
| 3.2 | Delete ExtendedPlayer | Low | Phase 3.1 |
| 3.3 | Type File Cleanup | Low | Phase 3.2 |
| 4 | Multi-Enemy Battle | Medium | Phase 2 Complete |

---

## Risk Management

### High Risk Work

1. **State Synchronization During Context Separation**
   - Mitigation: Gradual migration, don't delete old Context immediately
   - Run both Contexts in parallel and test

2. **Oversights During Legacy Interface Deletion**
   - Mitigation: TypeScript compiler unused detection
   - Mark gradual deprecation with `// @deprecated` comments

### Low Risk Work

1. **Phase 1 (Buff/Debuff Ownership)**
   - Limited impact scope
   - Backward compatibility maintainable
   - Easy rollback

---

## Completion Criteria

### Phase 1
- [ ] Buff/Debuff Ownership System implementation complete
- [ ] Verified working through tests

### Phase 2
- [ ] PlayerBattleContext separation complete
- [ ] EnemyBattleContext creation complete
- [ ] BattleSessionContext creation complete
- [ ] Existing battles function normally

### Phase 3
- [ ] Player, ExtendedPlayer fully deleted
- [ ] Only PlayerData in use
- [ ] No TypeScript compilation errors

### Phase 4
- [ ] Multi-enemy battle implementation complete
- [ ] Target selection UI implemented
- [ ] AoE attack support complete

---

Created: 2025-01-26
Last Updated: 2025-01-26
