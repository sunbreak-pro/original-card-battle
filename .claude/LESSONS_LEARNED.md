# Lessons Learned - Detailed Documentation

This file contains detailed explanations, code examples, and error messages for critical lessons learned during development.

---

## 1. CSS Class Name Collision

**Problem:** Generic class names like `.card`, `.enemy-card` cause style conflicts across different screens.

**Error Symptoms:**
- Card layouts break unexpectedly
- Styles appear correct in one screen but wrong in another
- CSS specificity battles

**Solution:** Always scope CSS class names with parent element:

```css
/* GOOD - Scoped to parent */
.battle-screen .enemy-card { ... }
.battle-screen .card { ... }

/* BAD - Conflicts with other screens */
.enemy-card { ... }
.card { ... }
```

**Affected Files:**
- `src/ui/css/pages/battle/*.css`
- Any component-specific CSS

---

## 2. Context Provider Scope

**Problem:** State that needs to persist across screen transitions (e.g., dungeon → battle → dungeon) loses data if provider is too low in the component tree.

**Solution:** Place providers high in the App.tsx hierarchy:

```
GameStateProvider → ResourceProvider → PlayerProvider → InventoryProvider → DungeonRunProvider
```

**Affected Files:**
- `src/App.tsx`
- `src/domain/dungeon/DungeonRunProvider.tsx`

---

## 3. React Hooks Rules

**Problem:** Calling hooks conditionally or after early returns causes React errors.

**Error Message:**
```
React has detected a change in the order of Hooks called by Component.
```

**Solution:** All hooks must be called at top level, before any conditional returns:

```typescript
// GOOD
const MyComponent = () => {
  const [state, setState] = useState(0);  // Hook at top
  const value = useContext(MyContext);     // Hook at top

  if (someCondition) return null;  // Early return AFTER hooks

  return <div>{state}</div>;
};

// BAD
const MyComponent = () => {
  if (someCondition) return null;  // Early return BEFORE hooks

  const [state, setState] = useState(0);  // Hook after return - ERROR
  return <div>{state}</div>;
};
```

---

## 4. React 19 Ref Access Rule

**Problem:** In React 19, `useRef().current` cannot be accessed during render (e.g., passing to props or returning in JSX).

**Error Message:**
```
Cannot access refs during render
```

**Root Cause:** React 19 enforces stricter ref access rules. Refs are meant for imperative operations, not render-time values.

**Solution:** Use `useState` for values that need to be rendered:

```typescript
// BAD - React 19 error: "Cannot access refs during render"
const valueRef = useRef(0);
return <Component value={valueRef.current} />;

// GOOD - Use state for values needed during render
const [value, setValue] = useState(0);
return <Component value={value} />;
```

**Fixed Instance:**
- `BattleScreen.tsx`: Changed `soulsTransferredRef` → `soulsTransferred` (useState), moved death handling to useEffect
- `BattleScreen.tsx`: Changed `prevPhaseIndexRef` → `prevPhaseIndex` (useState) for render-time phase tracking

**When to Use Refs vs State:**
| Use Refs | Use State |
|----------|-----------|
| DOM element access | Values displayed in UI |
| Mutable values without re-render | Values that trigger re-render |
| One-time guards (e.g., `deathHandledRef`) | Values passed to child props |
| Values only read in effects/callbacks | Previous value tracking for derived state |

---

## 5. Language Consistency

**Problem:** Mixed languages in UI text cause confusion and inconsistency.

**Rule:**
- **UI text:** Japanese (e.g., "見習い剣士", "攻撃", "防御")
- **Code/comments:** English

**Affected Files:**
- `src/domain/characters/CharacterClassData.ts` - Class names in Japanese
- All React components with user-facing text

---

## 6. React 19 `set-state-in-effect` vs `refs` の衝突

**Problem:** Two React 19 lint rules conflict — moving code from render to useEffect triggers `set-state-in-effect`, while keeping it in render triggers `refs`.

**Pattern A — Render-time derived state (no side effects):**
```typescript
// Use useState to track previous values (NOT useRef)
const [prevValue, setPrevValue] = useState(currentValue);
if (currentValue !== prevValue) {
  setPrevValue(currentValue);
  setDerivedState(newValue); // OK: render-time setState is fine
}
```

**Pattern B — Side effects that need setState:**
```typescript
// Use useEffect + eslint-disable for the setState line
const guardRef = useRef(false);
useEffect(() => {
  if (!guardRef.current) {
    doSideEffect();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- one-time init guarded by ref
    setSomeState(value);
    guardRef.current = true;
  }
}, [deps]);
```

**Decision rule:** If the code has side effects (API calls, context updates, etc.) → Pattern B (useEffect). If it's pure derived state → Pattern A (render-time setState).

**Fixed Instance:**
- `BattleScreen.tsx`: Death handling → Pattern B (useEffect + eslint-disable)
- `BattleScreen.tsx`: Phase index tracking → Pattern A (render-time useState)

---

## Quick Reference

| Issue | One-Line Rule |
|-------|---------------|
| CSS Class Collision | Scope with parent: `.battle-screen .card {}` |
| Context Provider Scope | Persist state across screens → provider high in tree |
| React Hooks | Call at top level, before conditional returns |
| React 19 Refs | No `ref.current` during render → use `useState` |
| React 19 Rules Conflict | Side effects → useEffect + eslint-disable; Derived state → render-time useState |
| Language | UI: Japanese / Code: English |
