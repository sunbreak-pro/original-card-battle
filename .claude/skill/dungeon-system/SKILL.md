---
name: dungeon-system
description: Add or modify dungeon exploration systems in the card battle game. Covers map generation, node types, return system, and depth progression. Use for "add dungeon node", "modify map generation", "implement return system" requests.
---

# Dungeon System Skill

Workflow for adding or modifying dungeon exploration systems.

## Dungeon Architecture

```
DungeonScreen
  └── DungeonRunProvider (state persistence)
        ├── DungeonMap (node navigation)
        ├── NodeEvent (encounter handling)
        └── DepthManager (progression)
```

## Key Files

| Domain | File Path |
|--------|-----------|
| Types | `src/domain/dungeon/types/DungeonTypes.ts` |
| Logic | `src/domain/dungeon/logic/dungeonLogic.ts` |
| Depth Manager | `src/domain/dungeon/depth/deptManager.ts` |

## Dungeon Node Types

```typescript
export type DungeonNodeType =
  | "battle"      // Normal combat
  | "elite"       // Strong enemy combat
  | "boss"        // Boss combat
  | "rest"        // Recovery
  | "event"       // Random event
  | "shop"        // Merchant
  | "treasure"    // Treasure chest
  | "mystery";    // Unknown node
```

## DungeonNode Interface

```typescript
interface DungeonNode {
  id: string;
  type: DungeonNodeType;
  depth: number;
  position: { x: number; y: number };
  connections: string[];  // Connected node IDs
  visited: boolean;
  available: boolean;

  // Type-specific data
  enemyIds?: string[];    // For battle nodes
  eventId?: string;       // For event nodes
  rewards?: Reward[];     // For treasure nodes
}
```

## Map Generation

```typescript
// src/domain/dungeon/logic/dungeonLogic.ts

export function generateDungeonMap(depth: number): DungeonNode[] {
  const nodes: DungeonNode[] = [];

  // Generate layers (rows)
  for (let layer = 0; layer < LAYERS_PER_DEPTH; layer++) {
    const nodeCount = getNodeCountForLayer(layer);

    for (let i = 0; i < nodeCount; i++) {
      nodes.push({
        id: `${depth}_${layer}_${i}`,
        type: selectNodeType(layer, depth),
        depth,
        position: calculatePosition(layer, i, nodeCount),
        connections: [],
        visited: false,
        available: layer === 0,
      });
    }
  }

  connectNodes(nodes);
  return nodes;
}
```

## Node Type Distribution

```typescript
const NODE_TYPE_WEIGHTS: Record<number, Record<DungeonNodeType, number>> = {
  0: { battle: 70, event: 20, treasure: 10 },           // Early layers
  1: { battle: 50, elite: 20, event: 15, rest: 10, shop: 5 },  // Mid layers
  2: { battle: 40, elite: 30, event: 10, rest: 15, shop: 5 },  // Late layers
  3: { boss: 100 },  // Boss layer
};
```

## Adding New Node Type

### Step 1: Add Type

```typescript
// DungeonTypes.ts
export type DungeonNodeType = /* existing */ | "newtype";
```

### Step 2: Add Handler

```typescript
// dungeonLogic.ts
export function handleNodeEncounter(node: DungeonNode): void {
  switch (node.type) {
    // ... existing cases
    case "newtype":
      handleNewTypeNode(node);
      break;
  }
}
```

### Step 3: Add UI Component

```typescript
// src/components/dungeon/NewTypeNode.tsx
export function NewTypeNode({ node }: { node: DungeonNode }) {
  // Render node encounter UI
}
```

## Depth Progression

```typescript
// src/domain/dungeon/depth/deptManager.ts
export const DEPTH_CONFIG = {
  1: { name: "浅層", enemyMultiplier: 1.0, rewardMultiplier: 1.0 },
  2: { name: "中層", enemyMultiplier: 1.5, rewardMultiplier: 1.5 },
  3: { name: "深層", enemyMultiplier: 2.0, rewardMultiplier: 2.0 },
  4: { name: "最深層", enemyMultiplier: 3.0, rewardMultiplier: 3.0 },
  5: { name: "奈落", enemyMultiplier: 5.0, rewardMultiplier: 5.0 },
};
```

## Return System (Lives)

```typescript
interface DungeonRunState {
  currentDepth: number;
  currentNode: string | null;
  visitedNodes: string[];
  collectedLoot: Loot[];

  // Return system
  livesRemaining: number;  // Max 10
  canReturn: boolean;
}

// On death: lose collectedLoot, decrement lives
// On successful return: keep collectedLoot, lives unchanged
```

## DungeonRunProvider

```typescript
// Persists dungeon state across battle transitions
const DungeonRunContext = createContext<DungeonRunState | null>(null);

export function DungeonRunProvider({ children }) {
  const [runState, setRunState] = useState<DungeonRunState>(initialState);
  // State persists when navigating to/from battles
  // Reset on explicit return or death
  return (
    <DungeonRunContext.Provider value={{ runState, setRunState }}>
      {children}
    </DungeonRunContext.Provider>
  );
}
```

## Event System

```typescript
interface DungeonEvent {
  id: string;
  name: string;
  description: string;
  choices: EventChoice[];
}

interface EventChoice {
  text: string;
  effect: () => void;
  requirement?: () => boolean;
}
```

## Design Doc Reference

Dungeon design docs: `.claude/docs/danjeon_document/`
- `dungeon_exploration_ui_design_v3.0.md`
- `return_system_design.md`

Lives system: `.claude/docs/Overall_document/DESIGN_CHANGE_PLAN_lives_system.md`
