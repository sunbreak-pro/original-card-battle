---
name: debugging-error-prevention
description: Debug issues and prevent errors in the card battle game. Covers error boundaries, type safety, async error handling, state debugging, and common pitfall prevention. Use for "debug X", "fix error", "why is X failing", "prevent bugs" requests. 使用時: 「debugging-error-preventionを使用します」
---

# Debugging & Error Prevention Skill

Workflow for debugging issues and implementing error prevention patterns.

## Quick Debugging Checklist

```
□ Check browser console for errors
□ Verify TypeScript compilation (npm run build)
□ Check React DevTools component state
□ Verify context provider hierarchy
□ Check network requests (if applicable)
□ Review recent code changes
```

## Error Categories & Solutions

### 1. Type Errors (Compile Time)

```typescript
// Problem: Property 'x' does not exist
// Solution: Check interface definition or add optional chaining
const value = obj?.property ?? defaultValue;

// Problem: Type 'X' is not assignable to type 'Y'
// Solution: Use type assertion or fix the source type
const typed = data as ExpectedType;
// Or better: fix the data source
```

### 2. Null/Undefined Errors (Runtime)

```typescript
// Common causes in this codebase:
// - Array index out of bounds (enemy access)
// - Missing context provider
// - Uninitialized state

// Safe array access
const enemy = enemies[index];
if (!enemy) {
  console.error(`Enemy not found at index ${index}`);
  return;
}

// Safe context access (already implemented)
const context = useContext(XXXContext);
if (!context) {
  throw new Error("useXXX must be used within XXXProvider");
}
```

### 3. State Update Errors

```typescript
// Problem: State not updating
// Causes:
// 1. Direct mutation instead of new reference
// 2. Stale closure in useEffect
// 3. Missing dependency in useEffect

// Wrong: mutating array
enemies[0].hp = newHp;  // React won't detect change

// Correct: new array reference
setEnemies(prev => prev.map((e, i) =>
  i === 0 ? { ...e, hp: newHp } : e
));
```

### 4. Async Errors

```typescript
// Async errors don't propagate to error boundaries
// Use this pattern to catch them:

const [error, setError] = useState<Error | null>(null);

useEffect(() => {
  const loadData = async () => {
    try {
      const result = await fetchData();
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err : new Error(String(err)));
      console.error("Load failed:", err);
    }
  };
  loadData();
}, []);

if (error) return <ErrorDisplay error={error} />;
```

## Project-Specific Debug Points

### Context Provider Hierarchy

```
GameStateProvider      ← Screen routing, depth, battleMode
  → ResourceProvider   ← Gold, magic stones
  → PlayerProvider     ← PlayerData + RuntimeBattleState
  → InventoryProvider  ← Items, equipment, cards
  → DungeonRunProvider ← Dungeon state persistence
```

**Debug tip:** If state isn't accessible, verify component is wrapped by correct provider.

### Battle System Debug

```typescript
// Key files to check:
// src/domain/battles/managements/useBattleOrchestrator.ts
// src/domain/battles/managements/useBattleState.ts

// Common issues:
// 1. Buff duration not decreasing → check appliedBy ownership
// 2. Damage calculation wrong → check damageCalculation.ts flow
// 3. Phase not advancing → check useBattlePhase conditions

// Debug buff ownership:
console.log("Buff owner:", buff.appliedBy);
// Duration decreases only during applier's phase
```

### Save/Load Debug

```typescript
// src/domain/save/logic/saveManager.ts

// Check save data:
const metadata = saveManager.getMetadata();
console.log("Save exists:", metadata.exists);
console.log("Save version:", metadata.version);

// Inspect raw save:
const raw = localStorage.getItem("card_battle_save");
console.log(JSON.parse(raw || "{}"));
```

### Card/Deck Debug

```typescript
// Verify initial deck creation:
// 1. Check INITIAL_DECK_BY_CLASS in initialDeckConfig.ts
// 2. Check getCardDataByClass() returns correct data
// 3. Check createStarterDeckFromCounts() output

// Debug deck state:
console.log("Deck size:", deck.length);
console.log("Hand:", hand.map(c => c.name));
console.log("Discard:", discardPile.length);
```

## Error Prevention Patterns

### 1. Exhaustive Type Checking

```typescript
// Ensure all cases handled in switch statements
function getClassColor(playerClass: CharacterClass): string {
  switch (playerClass) {
    case "swordsman": return "#ef4444";
    case "mage": return "#3b82f6";
    case "summoner": return "#8b5cf6";
    default:
      const _exhaustive: never = playerClass;
      throw new Error(`Unknown class: ${playerClass}`);
  }
}
```

### 2. Type Guard Functions

```typescript
// Validate runtime data matches expected type
function isValidCard(data: unknown): data is Card {
  return (
    typeof data === "object" &&
    data !== null &&
    "id" in data &&
    "name" in data &&
    "cost" in data
  );
}

// Usage
const parsed = JSON.parse(jsonString);
if (!isValidCard(parsed)) {
  throw new Error("Invalid card data");
}
```

### 3. Safe Array Operations

```typescript
// Instead of direct index access:
const enemy = enemies[targetIndex];  // May be undefined

// Use find with ID:
const enemy = enemies.find(e => e.id === targetId);
if (!enemy) {
  throw new Error(`Enemy ${targetId} not found`);
}

// Or with bounds checking:
if (targetIndex < 0 || targetIndex >= enemies.length) {
  throw new Error(`Invalid enemy index: ${targetIndex}`);
}
```

### 4. Result Pattern for Operations

```typescript
// Pattern used in saveManager - return result object
interface OperationResult<T> {
  success: boolean;
  data?: T;
  error?: string;
}

function performOperation(): OperationResult<Data> {
  try {
    const data = doSomething();
    return { success: true, data };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : "Unknown error"
    };
  }
}
```

## React DevTools Usage

### Component Inspection

1. Open React DevTools → Components tab
2. Select component to inspect state/props
3. Use search to find specific component
4. Check "Rendered by" to trace parent hierarchy

### Profiler (Performance)

1. Open React DevTools → Profiler tab
2. Click record, perform action, stop
3. Look for:
   - Components with high render time
   - Unnecessary re-renders
   - State updates causing cascades

## Common Pitfalls in This Codebase

### 1. Context Synchronization

```typescript
// PlayerContext syncs with ResourceContext
// If gold updates fail, check both contexts
const { gold } = useResource();
const { playerData } = usePlayer();
// These should stay synchronized
```

### 2. Inventory Multi-Step Operations

```typescript
// InventoryContext.moveItem() has 11 directions
// Each direction validates capacity before move
// Check: source has item, destination has space
```

### 3. Battle State Arrays

```typescript
// Enemy state uses array indices
// If enemy targeting fails, verify:
// 1. Index is within bounds
// 2. Enemy at index is alive
// 3. State hasn't updated mid-operation
```

### 4. Map Serialization

```typescript
// Card mastery uses Map<string, number>
// Maps don't serialize to JSON directly
// Convert before save:
const masteryObj = Object.fromEntries(masteryMap);
// Restore after load:
const masteryMap = new Map(Object.entries(masteryObj));
```

## Error Boundary Implementation

```typescript
// src/components/common/ErrorBoundary.tsx
import { Component, ErrorInfo, ReactNode } from "react";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("Error caught by boundary:", error);
    console.error("Component stack:", errorInfo.componentStack);
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback ?? (
        <div className="error-boundary">
          <h2>エラーが発生しました</h2>
          <p>{this.state.error?.message}</p>
          <button onClick={() => this.setState({ hasError: false, error: null })}>
            再試行
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}

// Usage: wrap error-prone sections
<ErrorBoundary fallback={<BattleErrorFallback />}>
  <BattleScreen />
</ErrorBoundary>
```

## Console Debugging Strategy

```typescript
// Development-only logging
const DEBUG = import.meta.env.DEV;

function debugLog(category: string, message: string, data?: unknown) {
  if (DEBUG) {
    console.log(`[${category}]`, message, data ?? "");
  }
}

// Usage
debugLog("Battle", "Phase changed", { from: prev, to: next });
debugLog("Inventory", "Item moved", { item, from, to });
```

## Quick Reference: File Locations

| Issue Type | Primary Files |
|------------|---------------|
| Battle state | `useBattleState.ts`, `useBattleOrchestrator.ts` |
| Damage calc | `damageCalculation.ts`, `buffCalculation.ts` |
| Card effects | `cardPlayLogic.ts`, `cardEffectExecution.ts` |
| Inventory | `InventoryContext.tsx`, `inventoryReducer.ts` |
| Save/Load | `saveManager.ts`, `saveTypes.ts` |
| Player state | `PlayerContext.tsx`, `typeConverters.ts` |
| Dungeon | `dungeonLogic.ts`, `DungeonRunProvider.tsx` |

## Testing Without Framework

Since no test framework is configured, verify manually:

```bash
# 1. Build check (catches type errors)
npm run build

# 2. Lint check
npm run lint

# 3. Manual browser testing
npm run dev
# Then test the specific feature in browser
```

### Manual Test Checklist

```
□ Feature works on first try
□ Feature works after page refresh
□ Feature works after save/load cycle
□ Edge cases handled (empty, max, boundary values)
□ Error states display correctly
□ No console errors during operation
```
