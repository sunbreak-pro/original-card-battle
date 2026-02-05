# Context & State Management System

## 1. Overview

The game uses a **nested React Context hierarchy** for persistent state management, complemented by standalone hooks for transient battle state. Seven providers are nested in `App.tsx` in dependency order: `GameState -> Settings -> Toast -> Resource -> Player -> Inventory -> DungeonRun`. A separate `GuildContext` sits outside the main hierarchy. Battle state lives entirely in `useBattleState` / `useBattleOrchestrator` hooks (no Context). Save/load serializes a subset of state to `localStorage` via `saveManager`.

## 2. File Map

| File | Lines | Role |
|------|-------|------|
| `src/App.tsx` | 119 | Provider nesting, screen routing |
| `src/contexts/GameStateContext.tsx` | 140 | Screen navigation, battle config, depth |
| `src/contexts/ResourceContext.tsx` | 363 | Gold dual-pool, magic stones, exploration limit |
| `src/contexts/PlayerContext.tsx` | ~675 | Player stats, runtime battle state, deck, equipment AP, resource delegation |
| `src/contexts/InventoryContext.tsx` | 774 | Item add/remove/move/equip operations |
| `src/contexts/GuildContext.tsx` | 240 | Rumors, quests (standalone, not in main hierarchy) |
| `src/ui/dungeonHtml/DungeonRunContext.tsx` | 198 | Dungeon run state, floor progression |
| `src/domain/save/logic/saveManager.ts` | 249 | localStorage save/load/export/import/migrate |
| `src/constants/saveConstants.ts` | 12 | `SAVE_VERSION` ("1.0.0"), `SAVE_KEY` |
| `src/domain/battles/managements/useBattleState.ts` | 620 | Per-battle player & enemy state (hook, no Context) |
| `src/types/characterTypes.ts` | 344 | PlayerData, RuntimeBattleState backing types |
| `src/types/campTypes.ts` | 510 | ExplorationLimit, SanctuaryProgress, storage/inventory types |
| `src/types/dungeonTypes.ts` | 88 | DungeonRun, DungeonFloor, DungeonNode |
| `src/types/itemTypes.ts` | 176 | Item, MagicStones, EquipmentData |
| `src/types/saveTypes.ts` | 74 | SaveData, PlayerSaveData, ResourceSaveData |
| `src/types/index.ts` | 100 | Barrel re-export of all types |

## 3. Data Structures

### 3.1 Provider Hierarchy (App.tsx)

```
<GameStateProvider>          ← No dependencies on other contexts
  <SettingsProvider>         ← Settings (volume, etc.)
    <ToastProvider>          ← Toast notifications
      <ResourceProvider>         ← Independent state
        <PlayerProvider>         ← Reads ResourceContext via useResources()
          <InventoryProvider>    ← Reads PlayerContext via usePlayer()
            <DungeonRunProvider> ← Independent state
              <AppContent />
            </DungeonRunProvider>
          </InventoryProvider>
        </PlayerProvider>
      </ResourceProvider>
    </ToastProvider>
  </SettingsProvider>
</GameStateProvider>
```

GuildContext is **not** in the main hierarchy. It is instantiated locally within the Guild facility component.

### 3.2 GameState

```typescript
interface GameState {
  currentScreen: GameScreen;  // 11 possible screens
  battleMode: BattleMode;     // "normal" | "exam" | "return_route" | null
  depth: Depth;               // 1-5
  battleConfig?: BattleConfig; // enemyIds, backgroundType, callbacks
}
```

**API:** `navigateTo(screen)`, `startBattle(config, mode)`, `returnToCamp()`, `setDepth(depth)`, raw `setGameState`.

**Init:** Checks `saveManager.hasSave()` -> `"camp"` or `"character_select"`.

### 3.3 ResourceState

```typescript
interface ResourceState {
  gold: {
    baseCamp: number;      // Permanent (survives death)
    exploration: number;   // At-risk (lost on death)
  };
  magicStones: {
    baseCamp: MagicStones;     // { small, medium, large, huge }
    exploration: MagicStones;
  };
  explorationLimit: ExplorationLimit; // { current, max }
}
```

**Key operations:** `addGold(amount, toBaseCamp?)`, `useGold(amount)` (baseCamp-first deduction), `transferExplorationToBaseCamp(multiplier)`, `resetExplorationResources()`.

### 3.4 InternalPlayerState + RuntimeBattleState

```typescript
// Internal state (PlayerContext internal)
interface InternalPlayerState {
  // Base stats: name, playerClass, classGrade, level, hp, maxHp, ap, maxAp, guard, speed, cardActEnergy, deck
  // Storage: storage, inventory, equipmentInventory, equipmentSlots
  // Resources (synced from ResourceContext): gold, baseCampGold, explorationGold, *MagicStones
  // Progression: explorationLimit, sanctuaryProgress
}

// Exposed as separate state
interface RuntimeBattleState {
  currentHp: number;         // Persists between battles within exploration
  currentAp: number;         // Derived from equipment durability
  cardMasteryStore: Map<string, number>;
  lives: LivesSystem;        // { maxLives, currentLives }
  difficulty: Difficulty;
}
```

**PlayerData** (computed, memoized): Structured into `persistent`, `resources`, `inventory`, `progression` sub-objects.

### 3.5 InventoryContext

No own state -- delegates entirely to `PlayerContext.updatePlayerData()`:

```typescript
interface InventoryContextValue {
  addItemToInventory/Storage/EquipmentInventory: (item: Item) => boolean;
  removeItemFrom*: (itemId: string) => boolean;
  equipItem: (itemId: string, slot: EquipmentSlot) => MoveResult;
  unequipItem: (slot: EquipmentSlot) => MoveResult;
  moveItem: (itemId: string, direction: MoveDirection) => MoveResult;
  // 9 MoveDirection variants handled in switch
}
```

### 3.6 GuildContext (Standalone)

```typescript
interface GuildContextValue {
  availableRumors: Rumor[];        // Filtered from RUMORS constant
  activeRumors: ActiveRumor[];     // { rumor, remainingDuration, activatedAt }
  availableQuests: Quest[];        // Generated daily + weekly
  acceptedQuests: Quest[];         // Max 5
  completedQuests: Quest[];
  // Operations: activateRumor, acceptQuest, updateQuestProgress, claimQuestReward, refreshQuests
}
```

### 3.7 DungeonRunContext

```typescript
interface DungeonRun {
  runId: string;
  selectedDepth: Depth;
  currentFloor: DungeonFloor;  // { depth, nodes[], currentNodeId, isCompleted, totalRows }
  floorNumber: number;         // 1-5
  encounterCount: number;
  isActive: boolean;
  startedAt: number;
}
```

Located in `src/ui/dungeonHtml/` (not `src/contexts/`).

### 3.8 SaveData

```typescript
interface SaveData {
  version: string;           // "1.0.0"
  timestamp: number;
  player: PlayerSaveData;    // name, class, grade, level, hp/maxHp, ap/maxAp, speed, deckCardIds
  resources: ResourceSaveData; // baseCampGold, baseCampMagicStones
  inventory: InventorySaveData; // storageItems[], equipmentSlots
  progression: ProgressionSaveData; // sanctuaryProgress, unlockedDepths
}
```

### 3.9 useBattleState (Hook, not Context)

```typescript
interface UseBattleStateReturn {
  playerState: PlayerState;     // hp, maxHp, ap, maxAp, guard, energy, buffs
  enemies: EnemyBattleState[];  // Array for multi-enemy support
  selectedTargetIndex: number;
  aliveEnemies: EnemyBattleState[];
  isPlayerAlive: boolean;
  areAllEnemiesDead: boolean;
  playerBattleStats: BattleStats;  // For damage calculation
  enemyBattleStats: BattleStats;
  resetForNextEnemy: (nextEnemies) => void;
  // 30+ setter functions for player/enemy state
}
```

Initialized from `InitialPlayerState` (passed from PlayerContext's RuntimeBattleState).

## 4. Logic Flow

### 4.1 App Boot & Initialization

```
Browser loads App.tsx
  |
  v
GameStateProvider initializes
  |- getInitialScreen() checks saveManager.hasSave()
  |- hasSave() ? "camp" : "character_select"
  |
  v
ResourceProvider initializes
  |- createInitialResourceState()
  |- baseCamp gold: 1250 (hardcoded test value)
  |- baseCamp stones: { small:5, medium:3, large:1, huge:0 }
  |- explorationLimit: { current:0, max:10 }
  |
  v
PlayerProvider initializes
  |- createInitialPlayerState(Swordman_Status) ← always Swordsman default
  |- Syncs gold/stones/limit from ResourceContext
  |- RuntimeBattleState: HP from base, AP from calculateEquipmentAP()
  |
  v
InventoryProvider (stateless, delegates to Player)
  |
  v
DungeonRunProvider (initialRun = null)
  |
  v
AppContent renders based on gameState.currentScreen
```

### 4.2 Character Selection Flow

```
CharacterSelect screen
  |
  v
User picks class (swordsman | mage)
  |
  v
PlayerContext.initializeWithClass(classType)
  |- getBasePlayerByClass(classType)    → Swordsman/Mage_Status
  |- getCharacterClassInfo(classType)   → starterDeck
  |- createInitialPlayerState()
  |- Sync with ResourceContext values
  |- Reset sanctuaryProgress to zeros
  |- Reset RuntimeBattleState (preserve difficulty)
  |
  v
GameStateContext.navigateTo("camp")
```

### 4.3 Battle State Flow

```
DungeonMap → selectNode → startBattle(config)
  |
  v
GameStateContext.currentScreen = "battle"
  |
  v
BattleScreen renders → useBattleOrchestrator
  |- Creates useBattleState(depth, enemies, speed, initialPlayerState)
  |  |- Reads PlayerContext.runtimeState for HP/AP/mastery
  |  |- Creates local useState for playerHp, playerAp, guard, energy, buffs
  |  |- Creates local useState for enemies[]
  |
  v
Battle plays out (all state local to hooks)
  |
  v
Battle ends
  |- PlayerContext.updateRuntimeState({ currentHp, currentAp })
  |- PlayerContext.updateCardMastery(...)
  |- DungeonRun.completeCurrentNode(result)
```

### 4.4 Save/Load Flow

```
Save trigger (manual)
  |
  v
saveManager.save({
  player: { name, class, grade, level, hp, maxHp, ap, maxAp, speed, deckCardIds },
  resources: { baseCampGold, baseCampMagicStones },
  inventory: { storageItems, equipmentSlots },
  progression: { sanctuaryProgress, unlockedDepths }
})
  |
  v
JSON.stringify() → localStorage.setItem("roguelike_card_save")

Load:
  localStorage.getItem() → JSON.parse()
  |- Version check (1.0.0)
  |- Returns { success, data, needsMigration? }
```

### 4.5 Resource Transfer (Exploration End)

```
Survival:
  transferExplorationToBaseCamp(multiplier: 0.6-1.0)
    |- gold: Math.floor(exploration * multiplier) → baseCamp
    |- stones: each tier Math.floor(count * multiplier) → baseCamp
    |- exploration pools reset to 0
  transferSouls(multiplier)
    |- currentRunSouls * multiplier → totalSouls

Death:
  resetExplorationResources()
    |- exploration gold → 0 (baseCamp preserved)
    |- exploration stones → 0 (baseCamp preserved)
  resetCurrentRunSouls()
    |- currentRunSouls → 0 (totalSouls preserved)
```

## 5. Key Details

### Constants

| Constant | Value | Location |
|----------|-------|----------|
| `SAVE_VERSION` | `"1.0.0"` | `saveConstants.ts:8` |
| `SAVE_KEY` | `"roguelike_card_save"` | `saveConstants.ts:11` |
| `STORAGE_MAX_CAPACITY` | 100 | `campConstants.ts:116` |
| `INVENTORY_MAX_CAPACITY` | 20 | `campConstants.ts:119` |
| `EQUIPMENT_INVENTORY_MAX` | 3 | `campConstants.ts:122` |
| `DEFAULT_EXPLORATION_LIMIT` | 10 | `campConstants.ts:125` |
| Initial baseCamp gold | 1250 | `ResourceContext.tsx:69` (test value) |
| Survival multiplier range | 0.6 - 1.0 | `campConstants.ts:97-99` |
| Max accepted quests | 5 | `GuildContext.tsx:133` |

### State Persistence Model

| Data | Survives Battle | Survives Death | Saved to localStorage |
|------|----------------|----------------|----------------------|
| baseCamp gold | Yes | Yes | Yes |
| exploration gold | Yes | **No** | No |
| baseCamp stones | Yes | Yes | Yes |
| exploration stones | Yes | **No** | No |
| Player HP/AP (runtime) | Between battles | No (reset) | No |
| Card mastery | Between battles | No | No |
| Lives | Between battles | N/A (decremented) | No |
| Equipment slots | Yes | Yes | Yes |
| Sanctuary progress | Yes | Yes | Yes |
| Dungeon run state | During run | No | No |
| Guild quests | During session | During session | No |

## 6. Dependencies

### Cross-Context Dependencies

```
GameStateContext ← (none, reads saveManager at init)
  |
ResourceContext ← (none, standalone)
  |
PlayerContext ← ResourceContext (via useResources())
  |   Delegates: addGold, useGold, addMagicStones, transfer, reset
  |   Syncs: gold/stones/limit from ResourceContext to internal state
  |
InventoryContext ← PlayerContext (via usePlayer())
  |   Delegates: all operations via updatePlayerData()
  |
DungeonRunContext ← (none, standalone)
  |
GuildContext ← (none, standalone, NOT in main hierarchy)
```

### Hook Dependencies

```
useBattleState ← Swordman_Status (fallback defaults)
               ← selectRandomEnemy (enemy init)
               ← createEnemyStateFromDefinition

useBattleOrchestrator ← useBattleState
                      ← PlayerContext (for InitialPlayerState)
                      ← DungeonRunContext (for encounter tracking)
```

### Save System Dependencies

```
saveManager ← SaveData types
            ← SAVE_KEY, SAVE_VERSION constants

GameStateContext.getInitialScreen() ← saveManager.hasSave()
```

## 7. Vulnerability Analysis

### [BUG-RISK] V-CS01: Dual Resource State Synchronization

**Location:** `PlayerContext.tsx:531-667`
PlayerContext maintains **duplicate copies** of gold/magicStones/explorationLimit alongside ResourceContext. Each resource mutation calls ResourceContext then manually syncs PlayerContext with inline calculations. The `addGold` function (line 537) uses `resourceContext.getTotalGold() + amount` which reads **stale** state (pre-update ResourceContext value) then adds the amount again, potentially causing drift between the two contexts.

### [BUG-RISK] V-CS02: useGold Race Condition

**Location:** `ResourceContext.tsx:117-145`
`useGold` checks `resources.gold.baseCamp + resources.gold.exploration` against `amount` **outside** the `setResources` updater, using closure-captured values. If two rapid calls occur, the second call could pass the balance check with stale values while the first is still in React's batched update queue.

### [BUG-RISK] V-CS03: useExplorationPoint Return Value

**Location:** `ResourceContext.tsx:224-241`
`useExplorationPoint` sets `success = true` inside `setResources` callback but returns `success` synchronously. Because React batches state updates, `success` may still be `false` when returned if the updater hasn't executed yet. The `let success = false` pattern with mutation inside a state updater is unreliable.

### [BUG-RISK] V-CS04: PlayerData.persistent.id Uses Date.now()

**Location:** `PlayerContext.tsx:772`
`id: \`player_${Date.now()}\`` regenerates the player ID on every `useMemo` recomputation. This means the player's identity changes every time playerState or equipmentAP changes, which could break any system that caches or compares player IDs.

### [QUALITY] V-CS05: Large PlayerContext (~675 lines)

**Location:** `PlayerContext.tsx`
PlayerContext contains runtime battle state, equipment AP system, deck management, lives system, souls, and the entire `updatePlayerData` mapper. This violates single-responsibility and makes modifications risky.

### [BUG-RISK] V-CS06: Save System Missing Coverage

**Location:** `saveManager.ts:193-232`
`createDefaultSaveData()` saves: player stats, baseCamp resources, storage items, equipment slots, sanctuary, unlocked depths. **NOT saved:** inventory items (separate from storage), equipmentInventory, guild state (rumors/quests), runtime battle state, dungeon run state, exploration resources. Some of these are intentionally transient, but inventory items and equipmentInventory are persistent data that could be lost.

### [EXTENSIBILITY] V-CS07: No Save Migration Implementation

**Location:** `saveManager.ts:172-186`
`migrate()` is a stub that simply stamps the current version. No actual migration logic exists. When save format changes, all existing saves will return `needsMigration: true` but cannot actually be migrated, effectively corrupting them.

### [QUALITY] V-CS08: Hardcoded Test Values in Production Init

**Location:** `ResourceContext.tsx:67-81`, `PlayerContext.tsx:228-244`
Initial resource state uses hardcoded test values (1250 gold, specific magic stones). `STORAGE_TEST_ITEMS`, `INVENTORY_TEST_ITEMS`, `EQUIPPED_TEST_ITEMS` are loaded as initial data. These should come from save data or be zero for new games.

### [BUG-RISK] V-CS09: GuildContext Quest Expiry Uses JavaScript Date

**Location:** `campTypes.ts:237` (`expiresAt?: Date`)
Quest expiry uses `Date` objects which won't survive JSON serialization if guild state is ever saved. The `isQuestExpired()` check works in-memory but guild state isn't persisted, so all quests reset on page reload.

### [EXTENSIBILITY] V-CS10: DungeonRunContext in UI Directory

**Location:** `src/ui/dungeonHtml/DungeonRunContext.tsx`
DungeonRunContext is a state management context placed in a UI directory. This breaks the architectural convention that contexts live in `src/contexts/`. While the CLAUDE.md documents this as intentional (for persistence across battle transitions), it creates a discoverability issue and prevents other non-dungeon components from easily accessing dungeon state.

### [BUG-RISK] V-CS11: InventoryContext Reads Stale playerData

**Location:** `InventoryContext.tsx:48`
All inventory operations read `playerData` from closure scope (via `usePlayer()`) and call `updatePlayerData()`. If multiple operations fire in quick succession (e.g., opening a 6-item equipment pack), each reads the same stale `playerData` snapshot, potentially overwriting the previous operation's results.

### [QUALITY] V-CS12: Battle State Dual API Surface

**Location:** `useBattleState.ts:460-494`
Both "targeted" setters (`setTargetedEnemyHp`) and "legacy" setters (`setEnemyHp`) exist and do the **same thing** (route through `selectedEnemyArrayIndex`). This 35-line duplication increases API surface without adding functionality, and callers may be confused about which to use.
