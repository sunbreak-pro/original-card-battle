# Dungeon System

## Overview

The dungeon system generates procedural floor maps with node-based navigation. Each floor is a grid of nodes (battle, elite, boss, event, rest, treasure) connected in a forward-progressing web structure. `DungeonRunContext` (located in `src/ui/dungeonHtml/`, not `src/contexts/`) manages run state including floor progression, node selection, and encounter counting. Depths 1-5 map to themed dungeon tiers (腐食→深淵) with all depths sharing a unified neutral visual theme.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/types/dungeonTypes.ts` | 87 | DungeonNode, DungeonFloor, DungeonRun, MapGenerationConfig |
| `src/domain/dungeon/depth/deptManager.ts` | 41 | Depth names, neutral theme constants |
| `src/domain/dungeon/logic/dungeonLogic.ts` | 362 | Map generation, node selection, progression, utilities |
| `src/domain/dungeon/logic/nodeEventLogic.ts` | 243 | Rest/treasure/event node processing with loot tables |
| `src/ui/dungeonHtml/DungeonRunContext.tsx` | 197 | Run state management context provider |

## Data Structures

### DungeonNode

```typescript
interface DungeonNode {
  id: string;              // "node-{row}-{col}"
  type: NodeType;          // "battle" | "elite" | "boss" | "event" | "rest" | "treasure"
  row: number;
  column: number;
  connections: string[];   // IDs of connected nodes in next row
  status: NodeStatus;      // "available" | "locked" | "current" | "completed" | "skipped"
}
```

### DungeonFloor

```typescript
interface DungeonFloor {
  depth: Depth;
  nodes: DungeonNode[];
  currentNodeId: string | null;
  isCompleted: boolean;
  totalRows: number;
}
```

### DungeonRun

```typescript
interface DungeonRun {
  runId: string;           // "run-{timestamp}-{random}"
  selectedDepth: Depth;
  currentFloor: DungeonFloor;
  floorNumber: number;     // 1-5
  encounterCount: number;
  isActive: boolean;
  startedAt: number;
}
```

### MapGenerationConfig

```typescript
interface MapGenerationConfig {
  totalRows: number;
  nodesPerRow: number[];
  eliteChance: number;
  eventChance: number;
  restChance: number;
  treasureChance: number;
}
```

### NodeEventResult

```typescript
interface NodeEventResult {
  type: "rest" | "treasure" | "event";
  title: string;
  description: string;
  rewards: NodeEventReward;
}

interface NodeEventReward {
  hpRestore?: number;
  hpRestorePercent?: number;
  gold?: number;
  magicStones?: { small?: number; medium?: number; large?: number };
  items?: Item[];
}
```

## Logic Flow

### Map Generation

```
generateFloorMap(depth, config)
  ↓
For each row (0 to totalRows-1):
  ├─ nodeCount = config.nodesPerRow[row] || 1
  ├─ For each column (0 to nodeCount-1):
  │   ├─ id = "node-{row}-{col}"
  │   ├─ type = getNodeTypeForRow(row, totalRows, config):
  │   │   ├─ Row 0 → always "battle" (entry point)
  │   │   ├─ Last row → always "boss"
  │   │   └─ Middle rows → weighted random:
  │   │       ├─ eliteChance → "elite"
  │   │       ├─ eventChance → "event"
  │   │       ├─ restChance → "rest"
  │   │       ├─ treasureChance → "treasure"
  │   │       └─ remainder → "battle"
  │   └─ status: row 0 → "available", else → "locked"
  ↓
Generate connections between rows:
  ├─ 1 next-row node → all current nodes connect to it
  ├─ 1 current node → connects to all next-row nodes
  └─ Multiple → direct mapping + 50% diagonal connection
```

### Node Selection & Progression

```
selectNode(floor, nodeId)
  ├─ Validate node exists and status === "available"
  ├─ Mark all other available nodes as "skipped"
  ├─ Mark selected node as "current"
  └─ Set currentNodeId = nodeId

completeNode(floor, nodeId, result)
  ├─ Validate node exists and status === "current"
  ├─ "defeat" | "retreat" → isCompleted = false, no progression
  ├─ "victory":
  │   ├─ Mark node as "completed"
  │   ├─ If boss (last row) → isCompleted = true
  │   └─ Unlock connected nodes (status → "available")
  └─ Clear currentNodeId
```

### Floor Advancement

```
advanceToNextFloor()  (DungeonRunContext)
  ├─ floorNumber >= 5 → no-op
  ├─ floorNumber + 1
  └─ generateFloorMap(selectedDepth) → new floor
```

### Non-Battle Node Events

```
processRestNode(currentHp, maxHp)
  └─ Restore 20-30% HP (random)

processTreasureNode()
  └─ Weighted loot table:
      ├─ 35% → Gold (50-200)
      ├─ 25% → Small magic stones (1-3)
      ├─ 15% → Medium magic stone (1)
      ├─ 15% → Healing potion
      └─ 10% → Gold (100-300) + 2 small stones

processEventNode()
  └─ Weighted random events:
      ├─ 25% → "落とし物" (found potion)
      ├─ 20% → "隠された金庫" (gold 80-200)
      ├─ 20% → "旅の商人" (gold 30-80 + 1 small stone)
      ├─ 15% → "罠！" (take 10-30 damage — negative hpRestore)
      ├─ 10% → "癒しの泉" (15% HP restore)
      └─ 10% → "古代の碑文" (2 small stones)
```

### Depth Theme System

```
DEPTH_TABLE:
  1 → "腐食" (Corrosion)
  2 → "狂乱" (Frenzy)
  3 → "混沌" (Chaos)
  4 → "虚無" (Void)
  5 → "深淵" (Abyss)

All depths share neutralTheme:
  primary: "#1a1a24"
  secondary: "#2a2a3d"
  accent: "#8b7aa8"
  bg: gradient dark
```

### DungeonRunContext API

```
DungeonRunContext provides:
  ├─ dungeonRun: DungeonRun | null
  ├─ initializeRun(depth) → create new DungeonRun
  ├─ selectNodeToVisit(nodeId) → selectNode()
  ├─ completeCurrentNode(result) → completeNode()
  │   └─ "defeat" → isActive = false
  ├─ advanceToNextFloor() → new floor, floorNumber++
  ├─ retreatFromDungeon() → isActive = false
  ├─ getCurrentNode() → DungeonNode | null
  └─ incrementEncounter() → encounterCount++
```

## Key Details

- Row 0 is always a battle node; last row is always a boss node
- Connections only go forward (row N → row N+1), never backward or lateral
- When a node is selected, all other available nodes on that row become "skipped" (no backtracking)
- Up to 5 floors per dungeon run; `advanceToNextFloor` stops at floor 5
- Trap events deal damage via negative `hpRestore` values
- Treasure loot uses weighted random selection with cumulative probability
- DungeonRunContext lives in `src/ui/dungeonHtml/` (not `src/contexts/`) — documented as intentional for persistence across battle transitions
- All state changes use `setDungeonRun` with functional updaters for safety
- `generateRunId()` uses `Date.now()` + random string for uniqueness
- File `deptManager.ts` has a typo in name (should be "depthManager")

## Dependencies

```
dungeonLogic.ts
  ├─ dungeonTypes (DungeonNode, DungeonFloor, DungeonRun, etc.)
  └─ DEFAULT_MAP_CONFIG ← dungeonConstants

nodeEventLogic.ts
  ├─ itemTypes (Item)
  └─ generateConsumableFromData ← generateItem.ts

DungeonRunContext.tsx
  ├─ dungeonLogic (initializeDungeonRun, selectNode, completeNode, getNodeById, generateFloorMap)
  └─ dungeonTypes (DungeonRun, DungeonNode, NodeCompletionResult, Depth)

deptManager.ts
  └─ standalone (no imports beyond types)

External consumers:
  ├─ GameStateContext → triggers dungeon entry via screen routing
  ├─ BattleScreen → receives enemy from dungeon node type
  └─ Camp → return destination after dungeon exit
```

## Vulnerability Analysis

### `[BUG-RISK]` Map Generation Uses Uncontrolled Randomness

**Location:** `dungeonLogic.ts:47-71`

`getNodeTypeForRow()` and `generateConnections()` use `Math.random()` with no seed. The same depth and config produce different maps each time. If a player saves mid-dungeon and reloads, the map would need to be persisted exactly — but `DungeonRun` stores the generated map, so this is mitigated for active runs. However, `advanceToNextFloor()` generates a new map each time.

### `[BUG-RISK]` Floor Advancement Doesn't Track Depth Progression

**Location:** `DungeonRunContext.tsx:122-133`

`advanceToNextFloor()` always uses `prev.selectedDepth` for the new floor. The `floorNumber` increases but the enemy difficulty is determined by `selectedDepth`, not `floorNumber`. All 5 floors of a Depth 1 run face Depth 1 enemies — no escalation within a run.

### `[BUG-RISK]` Trap Damage Uses Negative hpRestore

**Location:** `nodeEventLogic.ts:194`

Trap events set `hpRestore: -(10 + random * 20)`. The consumer must check for negative values and apply damage instead of healing. If the consumer unconditionally adds `hpRestore` to HP, traps would heal the player.

### `[EXTENSIBILITY]` DungeonRunContext In UI Directory

**Location:** `src/ui/dungeonHtml/DungeonRunContext.tsx`

As noted in CLAUDE.md, this context lives in a UI directory rather than `src/contexts/`. This breaks the architectural convention and creates discoverability issues. Other screens (e.g., camp) that need to check dungeon state must import from a UI directory.

### `[BUG-RISK]` Node Connection Generation Can Create Unreachable Nodes

**Location:** `dungeonLogic.ts:90-126`

`generateConnections()` maps current-row nodes to next-row nodes with direct index mapping plus optional 50% diagonal connections. If `currentRowNodes.length > nextRowNodes.length + 1`, some current-row nodes will map to the same next-row node, potentially leaving next-row edge nodes unreachable (no incoming connections from any previous node).

### `[QUALITY]` deptManager.ts Typo in Filename

**Location:** `src/domain/dungeon/depth/deptManager.ts`

Should be "depthManager". The unused `depthThemes` object maps all 5 depths to the same `neutralTheme`, making the per-depth theme structure pointless.

### `[BUG-RISK]` completeCurrentNode Increments encounterCount for All Results

**Location:** `DungeonRunContext.tsx:94-118`

`encounterCount` increments for all non-defeat completions, including event/rest/treasure nodes. This counter may be used for difficulty scaling or rewards, but counting non-battle events inflates it inaccurately.
