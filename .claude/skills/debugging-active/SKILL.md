---
name: debugging-active
description: Hands-on debugging workflows for investigating live bugs in the card battle game. Covers debug instrumentation, state inspection, diagnostic trees, and step-by-step domain-specific debugging procedures. Use for "debug why X happens", "investigate bug", "trace issue", "find root cause" requests. 使用時: 「debugging-activeを使用します」
---

# Active Debugging Skill

Hands-on investigation workflows for tracing live bugs to their root cause. Complements `debugging-error-prevention` (which covers prevention patterns) by providing operational debugging procedures.

## 1. Debug Instrumentation Framework

### Temporary Debug Logger

Create `src/utils/debugLogger.ts` during investigation, **remove after fix is confirmed**.

```typescript
type DebugCategory =
  | 'battle' | 'buff' | 'damage' | 'phase'
  | 'inventory' | 'dungeon' | 'save'
  | 'context' | 'element' | 'summon';

const ENABLED: Set<DebugCategory> = new Set(['battle', 'buff']); // Toggle per investigation

export function dbg(category: DebugCategory, label: string, data?: unknown) {
  if (import.meta.env.DEV && ENABLED.has(category)) {
    console.log(
      `%c[${category.toUpperCase()}]%c ${label}`,
      'color: #f59e0b; font-weight: bold',
      'color: inherit',
      data ?? ''
    );
  }
}

export function dbgTable(category: DebugCategory, label: string, rows: Record<string, unknown>[]) {
  if (import.meta.env.DEV && ENABLED.has(category)) {
    console.log(`%c[${category.toUpperCase()}]%c ${label}`, 'color: #f59e0b; font-weight: bold', 'color: inherit');
    console.table(rows);
  }
}
```

### Strategic Logging Points

| Category    | File                              | Function / Hook                  | What to Log                                    |
| ----------- | --------------------------------- | -------------------------------- | ---------------------------------------------- |
| `battle`    | `useBattleOrchestrator.ts`        | Top-level orchestrator return    | Composed state snapshot                        |
| `phase`     | `useBattlePhase.ts`               | `advancePhase()`                 | Phase queue before/after advance               |
| `phase`     | `phaseCalculation.ts`             | `generatePhaseQueue()`           | Actor speeds, randomness, resulting order      |
| `damage`    | `damageCalculation.ts`            | `calculateDamage()`              | Attacker/defender stats, modifiers, result     |
| `buff`      | `buffCalculation.ts`              | `attackBuffDebuff()`             | Active buffs, computed multiplier              |
| `buff`      | `executeCharacterManage.ts`       | Phase start/end buff tick        | Buff map before/after duration decrement       |
| `element`   | `elementalSystem.ts`              | `onCardPlay()`                   | Resonance level change, triggered effect       |
| `summon`    | `useSummonSystem.ts`              | `onCardPlayed()` / `onTurnStart` | Active summons, decay, expiry                  |
| `inventory` | `InventoryContext.tsx`            | `moveItem()`                     | Direction, source/dest counts, capacity check  |
| `dungeon`   | `dungeonLogic.ts`                 | `completeNode()`                 | Node result, run state update                  |
| `save`      | `saveManager.ts`                  | `save()` / `load()`             | Data size, version, success/error              |
| `context`   | `PlayerContext.tsx`               | `decreaseLives()`               | Lives before/after, isGameOver                 |
| `context`   | `ResourceContext.tsx`             | `useGold()` / `addGold()`       | Gold before/after, transaction amount          |

## 2. State Inspection Techniques

### Context Provider State Dump

Paste into browser console to inspect live state:

```javascript
// React DevTools hook — works in dev builds
const fiber = document.getElementById('root')?._reactRootContainer?._internalRoot?.current;

// Alternative: use React DevTools Components tab
// Select a component → check hooks panel for context values
```

### Battle State Snapshot Checklist

When debugging a battle issue, collect ALL of the following from `useBattleOrchestrator`:

```
[ ] playerHp, playerMaxHp, playerAp, playerMaxAp
[ ] playerGuard value
[ ] playerBuffDebuffs (Map — use inspectBuffMap below)
[ ] enemies array — each enemy's hp, maxHp, buffDebuffs
[ ] currentPhase, phaseQueue contents
[ ] deck count, hand cards, discard pile count
[ ] classAbilityState (energy / resonance / summons)
[ ] battleResult (null during battle, set at end)
```

### Helper: Inspect Buff Map

Buff state uses `Map<string, BuffDebuffState>`. Console shows `Map(N)` unhelpfully. Use:

```typescript
function inspectBuffMap(buffs: Map<string, BuffDebuffState>): void {
  const rows = Array.from(buffs.entries()).map(([key, b]) => ({
    key,
    type: b.type,
    value: b.value,
    duration: b.duration,
    appliedBy: b.appliedBy,
  }));
  console.table(rows);
}
```

### Helper: Check Inventory Integrity

```typescript
function checkInventoryIntegrity(state: InventoryState): string[] {
  const issues: string[] = [];
  if (state.items.length > state.itemCapacity) {
    issues.push(`Items overflow: ${state.items.length}/${state.itemCapacity}`);
  }
  if (state.equipment.length > state.equipmentCapacity) {
    issues.push(`Equipment overflow: ${state.equipment.length}/${state.equipmentCapacity}`);
  }
  // Check for duplicate IDs
  const ids = state.items.map(i => i.id);
  const dupes = ids.filter((id, i) => ids.indexOf(id) !== i);
  if (dupes.length > 0) {
    issues.push(`Duplicate item IDs: ${dupes.join(', ')}`);
  }
  return issues;
}
```

## 3. Symptom-to-Root-Cause Diagnostic Trees

### Battle Symptoms

**Buff duration not decreasing:**
1. Check `buff.appliedBy` — duration only ticks during the applier's phase
2. Check `executeCharacterManage.ts` → `calculatePlayerPhaseStart/End` or `calculateEnemyPhaseStart/End` — is the decrement logic running?
3. Check if the buff `type` matches the decrement filter (some buff types may be excluded)
4. Log buff map at phase start and phase end to see if values change

**Wrong damage output:**
1. Log inside `damageCalculation.ts` → `calculateDamage()` — check base ATK, DEF values
2. Check `buffCalculation.ts` → `attackBuffDebuff()` and `defenseBuffDebuff()` — verify multipliers
3. Check if critical hit triggered (`criticalRateBuff()`)
4. Check reflect (`reflectBuff()`) — damage might be redirected
5. Check elemental modifier from `elementalSystem.ts` if mage class

**Phase order wrong / stuck:**
1. Log `phaseCalculation.ts` → `generatePhaseQueue()` — check actor speeds
2. Check `applySpeedRandomness()` — mean-reversion variance may produce unexpected order
3. Verify `advancePhase()` is called — check if awaiting animation completion
4. Check if battle result was already set (battle may have ended)

**Enemy targeting wrong target:**
1. Check `enemyAI.ts` — `selectTarget()` logic and targeting priority
2. Verify enemies array indices match expected targets
3. Check if dead enemies are being filtered from target pool

**Battle stuck (no phase advancing):**
1. Check if `battleResult` is already set (ended state)
2. Check if animation promise is unresolved (await never completing)
3. Verify `advancePhase()` callback is wired to phase completion
4. Check browser console for unhandled promise rejections

### Inventory / Shop Symptoms

**Item disappears after purchase:**
1. Check `shopLogic.ts` → `generateConsumableFromData()` — is item created correctly?
2. Check `InventoryContext.tsx` — is `addItem()` called with the generated item?
3. Check capacity — if at max, item may be silently rejected
4. Check if gold deduction happened but item addition failed (partial transaction)

**Can't buy item (button disabled or no effect):**
1. Check `canAfford(playerGold, price)` — gold comparison
2. Check `hasInventorySpace()` — capacity check
3. Check if shop listing `available` flag is false
4. Verify `useGold()` in `ResourceContext` returns success

### Save / Load Symptoms

**Data lost after load:**
1. Check `localStorage.getItem('card_battle_save')` in console — is data present?
2. Check save version vs expected version — migration may have dropped fields
3. Check `saveManager.load()` return — is `success` true?
4. Check if Map fields (mastery, buffs) survived serialization (Maps don't JSON.stringify)

**Save version mismatch:**
1. Check `saveTypes.ts` for current `SAVE_VERSION`
2. Check if migration function exists for the version gap
3. Inspect raw localStorage data version field

### Visual / CSS Symptoms

**Style bleeding between screens:**
1. Check if class names are scoped: `.battle-screen .my-class` not just `.my-class`
2. Check `LESSONS_LEARNED.md` for known CSS collision patterns
3. Search for unscoped class name in all CSS files

**Element sizing wrong:**
1. Verify using `vh/vw` not `px` (project convention)
2. Check parent container sizing chain
3. Browser DevTools → Elements → Computed tab to trace inherited styles

## 4. Domain-Specific Debugging Workflows

### Workflow: Battle System General

```
1. Reproduce the bug (see Section 6)
2. Open React DevTools → find BattleScreen component
3. Drill into useBattleOrchestrator hooks
4. Collect battle state snapshot (Section 2 checklist)
5. Identify which subsystem is wrong (damage? buffs? phase? targeting?)
6. Follow the relevant diagnostic tree (Section 3)
7. Add targeted dbg() calls (Section 1)
8. Reproduce again with logging, read console output
9. Fix the root cause
10. Remove all dbg() calls and debugLogger.ts
```

### Workflow: Buff/Debuff Duration (5 Steps)

```
Step 1: Add logging in executeCharacterManage.ts at phase start
        dbg('buff', 'Phase start buffs', inspectBuffMap(buffs))

Step 2: Add logging at the buff duration decrement point
        dbg('buff', 'Decrementing', { key, appliedBy: buff.appliedBy, currentPhase })

Step 3: Add logging at phase end
        dbg('buff', 'Phase end buffs', inspectBuffMap(buffs))

Step 4: Play one full turn, compare phase-start vs phase-end logs
        → If no change: decrement condition is wrong (check appliedBy match)
        → If buff removed too early: duration was set wrong at application time

Step 5: Trace buff creation back to the card effect or enemy action that applied it
        → Check initial duration value at source
```

### Workflow: Damage Calculation Pipeline (3 Steps)

```
Step 1: Log at entry of calculateDamage()
        dbg('damage', 'Input', { attackerAtk, defenderDef, card, element })

Step 2: Log each modifier stage
        dbg('damage', 'After buff mod', { atkMultiplier, defMultiplier })
        dbg('damage', 'After element mod', { elementMultiplier })
        dbg('damage', 'After critical', { isCritical, critMultiplier })

Step 3: Log final result
        dbg('damage', 'Result', { rawDamage, finalDamage, reflected, healed })
        → Compare expected vs actual at each stage to find divergence
```

### Workflow: Phase Queue Debugging

```
Step 1: Log generatePhaseQueue() input (actor speeds)
Step 2: Log applySpeedRandomness() output for each actor
Step 3: Log final sorted queue
Step 4: Compare with expected order
        → If speeds look correct but order wrong: check sort comparator
        → If speeds wrong: check buff modifiers on speed stat
```

### Workflow: Inventory Movement (4 Steps)

```
Step 1: Log moveItem() call with direction and item details
Step 2: Log capacity check result (source count, dest count, limits)
Step 3: Log state before and after the move operation
Step 4: Verify UI reflects the new state
        → If state correct but UI wrong: React re-render issue (check key props)
        → If state wrong: check reducer case for the direction
```

### Workflow: Save/Load (4 Steps)

```
Step 1: Before save — log the data object being saved
        dbg('save', 'Saving', { keys: Object.keys(data), version })

Step 2: After save — check localStorage directly
        JSON.parse(localStorage.getItem('card_battle_save'))

Step 3: On load — log the parsed data
        dbg('save', 'Loaded raw', parsedData)

Step 4: After load processing — log the hydrated state
        dbg('save', 'Hydrated', { playerData, inventory, resources })
        → Compare Step 1 output with Step 3 to find data loss
        → Check Map fields specifically (mastery, buffs)
```

## 5. Browser DevTools Workflows (Codebase-Specific)

### React DevTools: Battle State Drill-Down

```
Components tab → search "BattleScreen"
  → Expand hooks panel
  → Find useBattleOrchestrator return values
  → Drill into each sub-hook:
    useBattleState → playerHp, playerAp, buffs
    useBattlePhase → currentPhase, phaseQueue
    useCardExecution → hand, playedCards
    useClassAbility → energy/resonance/summons (class-dependent)
```

### Console: Runtime State Extraction

```javascript
// Quick state checks — paste in console during battle

// Check all enemy HP
document.querySelectorAll('[class*="enemy"]') // Find enemy elements
// Then use React DevTools $r to access selected component's props/state

// Check localStorage save data
JSON.parse(localStorage.getItem('card_battle_save') || '{}')

// Check current screen (from GameStateContext)
// Select any component in React DevTools, then:
// $r.props or hooks panel → find gameState context value
```

### Sources Panel: Breakpoint Locations by Bug Type

| Bug Type            | File to Open                    | Where to Set Breakpoint                    |
| ------------------- | ------------------------------- | ------------------------------------------ |
| Wrong damage        | `damageCalculation.ts`          | `calculateDamage()` entry + return         |
| Buff not applied    | `executeCharacterManage.ts`     | Buff application block                     |
| Phase stuck         | `useBattlePhase.ts`             | `advancePhase()` entry                     |
| Card not playing    | `useBattleOrchestrator.ts`      | Card play handler                          |
| Item lost           | `InventoryContext.tsx`           | `moveItem()` entry                         |
| Gold wrong          | `ResourceContext.tsx`            | `useGold()` / `addGold()`                  |
| Enemy AI wrong      | `enemyAI.ts`                    | `selectTarget()` / action selection        |
| Save fails          | `saveManager.ts`                | `save()` try/catch block                   |
| Elemental bug       | `elementalSystem.ts`            | `onCardPlay()` resonance update            |
| Summon bug          | `useSummonSystem.ts`            | `onCardPlayed()` spawn logic               |

### Application Panel: localStorage Inspection

```
DevTools → Application → Local Storage → localhost:5173
Key: card_battle_save
→ Right-click value → Copy → paste into console: JSON.parse('...')
→ Check: version, playerData, inventory, resources, dungeonState
```

## 6. Reproduction Strategies

### Fastest Path to Battle State

```
1. Start dev server: npm run dev
2. Select character (swordsman is simplest)
3. Enter dungeon at depth 1
4. Click first battle node
→ You're now in battle state for testing
```

### State Desync Reproduction

```
1. Enter battle, play several cards
2. Mid-battle, use browser back button
3. Navigate back to dungeon map
4. Re-enter the same battle node
→ If state desyncs, DungeonRunContext persistence is the issue
```

### Save Corruption Reproduction

```
1. Play through several battles, buy items
2. Save game
3. Modify code to change a type definition (add/remove field)
4. Load game
→ If fields are missing/wrong, save migration is needed
```

### Buff Edge Case Reproduction

```
1. Enter battle with an enemy that applies debuffs
2. Let the enemy apply a debuff to player
3. Observe debuff duration over multiple turns
4. Check: does duration decrease on correct phase? (applier's phase only)
```

## 7. Debug Cleanup Checklist

After the bug is fixed, **before committing**:

```
[ ] Remove src/utils/debugLogger.ts (if created)
[ ] Remove all dbg() / inspectBuffMap() / console.log calls added for debugging
[ ] Remove any temporary helper functions
[ ] Run npm run build — verify no unused imports/variables
[ ] Run npm run lint -- --fix
[ ] Test the fix in browser
[ ] Test that the fix survives page refresh
[ ] Test that save/load still works if state was modified
```
