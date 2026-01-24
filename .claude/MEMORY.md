# Project Memory

## Quick Reference

- **Current Phase:** Phase 6 Complete + Lives System + Bug Fixes
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-01-23

---

## Active Tasks

- Test character selection flow (clear localStorage, verify flow works)
- Test Library tabs (Cards, Enemies, Tips)
- Mage/Summoner classes show "Coming Soon" - cards not yet implemented
- Connect Sanctuary effects to battle/dungeon systems
- Test Lives System (death -> lives decrease, game over at 0 lives)
- Test HP/AP persistence between battles

---

## Recent Changes (Lives System + Bug Fixes)

### Lives System Implementation (Completed)

**New Feature:**

- Replaced `explorationLimit` with `LivesSystem` (残機システム)
- Lives based on difficulty: easy/normal = 3 lives, hard = 2 lives
- On death: lives decrease by 1, 100% of souls transferred to permanent pool
- Game Over when lives reach 0

**Files Changed:**

- `playerTypes.ts`: Added `Difficulty`, `LivesSystem`, `LIVES_BY_DIFFICULTY`
- `PlayerContext.tsx`: Added `RuntimeBattleState` with lives, `decreaseLives()`, `isGameOver()`, `setDifficulty()`
- `deathHandler.ts`: Updated to transfer 100% souls on death, added `handlePlayerDeathWithDetails()`
- `DefeatScreen.tsx`: Shows remaining lives (hearts), souls transferred, game over state

### Battle State Persistence Bug Fixes (Completed)

**Bugs Fixed:**

1. **HP/AP not persisting between battles**
   - Root cause: `useBattleState.ts` always used `Swordman_Status.hp/ap` constants
   - Fix: Added `InitialPlayerState` interface, pass runtime HP/AP from PlayerContext

2. **Card mastery not persisting**
   - Root cause: `createInitialDeck()` always created cards with `useCount: 0`
   - Fix: Added `cardMasteryStore` to `RuntimeBattleState`, apply mastery from store on deck init

3. **Same card type mastery not shared**
   - Root cause: `incrementUseCount()` only updated individual card instance
   - Fix: On victory, collect mastery from all cards by `cardTypeId` and save to store

**Files Changed:**

- `PlayerContext.tsx`: Added `RuntimeBattleState` with `currentHp`, `currentAp`, `cardMasteryStore`
- `useBattleState.ts`: Added `InitialPlayerState` parameter
- `useBattleOrchestrator.ts`: Pass initial state, apply mastery to initial deck
- `BattleScreen.tsx`: Save HP/AP/mastery on victory, pass runtime state to battle

---

## Recent Changes (CSS Refactoring)

### CSS Architecture Overhaul (Completed)

**Problem Solved:**

- BattleScreen.css was 1,243 lines with duplicate `@keyframes energyConsume`
- Facility CSS files had ~400 lines of duplicate patterns each
- No centralized variables or shared components

**New Structure:**

```
src/ui/css/
├── core/                    # Base layer
│   ├── variables.css        # CSS variables (colors, spacing, themes)
│   └── reset.css            # Global reset, scrollbar styling
│
├── components/              # Reusable components
│   ├── notifications.css    # Toast notifications
│   ├── tabs.css             # Tab navigation
│   ├── buttons.css          # Back/action buttons
│   ├── badges.css           # Quality/rarity badges
│   ├── bars.css             # Progress bars
│   ├── panels.css           # Detail panels
│   ├── modals.css           # Modal overlays
│   └── index.css            # Aggregate import
│
├── animations/              # Shared animations
│   └── keyframes.css        # All @keyframes (no duplicates)
│
├── pages/battle/            # Battle screen modules
│   ├── battle-layout.css    # Container, header, field
│   ├── battle-status.css    # HP, AP, guard, energy bars
│   ├── battle-hand.css      # Hand, cards, pile icons
│   ├── battle-enemy.css     # Enemy cards, tooltips
│   ├── battle-sword-energy.css
│   ├── battle-modals.css
│   └── index.css            # Aggregate import
│
└── cards/
    └── CardUnified.css      # Card styling (unchanged)
```

**Files Updated:**

- `index.css`: Now imports core + animations + components
- `BattleScreen.css`: Slim import of `pages/battle/index.css`
- All facility CSS (Shop, Guild, Library, Blacksmith, Sanctuary, Storage): Added shared imports

**Build Result:** Passes, CSS size essentially unchanged (shared styles consolidated)

---

## Recent Changes (Card Tag & Theme Simplification)

### Card Tag System (Completed)

- **New CardTag type:** `"attack" | "guard" | "skill" | "stance"`
- **Classification rules:**
  - `attack`: Cards with `baseDamage > 0` (deals damage)
  - `guard`: Cards with `guardAmount > 0` or `category === "def"` (no damage)
  - `skill`: Temporary buffs/debuffs, utility (draw, energy, heal)
  - `stance`: Semi-permanent battle effects (multi-turn buffs like `sw_033`, `sw_035`)
- **Files updated:**
  - `cardType.ts`: Added `CardTag` type, updated `Card.tags` from `string[]` to `CardTag[]`
  - `SwordmanCards.ts`: All 43 cards updated with new tag system
  - `enemyAI.ts`: Enemy action cards now use `CardTag[]`

### Depth Theme Removal (Completed)

- **neutralTheme:** Unified purple/dark theme for all UI
- **All depths now use same colors** - depth indicated via text only
- **Files updated:**
  - `deptManager.ts`: Added `neutralTheme`, all `depthThemes` point to it
  - `BattleScreen.tsx`: Uses `neutralTheme` instead of `depthThemes[depth]`
  - `DungeonGate.tsx`: `getThemeColors()` returns `neutralTheme`
  - `NodeMap.tsx`: Theme memo now returns `neutralTheme`

---

## Recent Changes (Phase 6)

- **Character Selection Screen:** New players choose class before starting
- **Library Facility:** Card encyclopedia, enemy bestiary, game tips
- **Save Integration:** No save = character select, existing save = camp

---

## Pending Items

- Guild: RumorsTab and QuestsTab are placeholders
- Shop: Daily sales system (deferred), pack opening animation (deferred)
- Dungeon: Event/rest/treasure nodes complete immediately (no UI)
- Sanctuary effects not applied to game mechanics

---

## See Also

- **Detailed Progress:** `.claude/todos/REFACTORING_TODO.md`
- **Game Design:** `blueprint/` directory
- **Battle Logic:** `blueprint/battle/battle_logic.md`

---

## Critical Lessons Learned

**Language Consistency:**
Player grades use Japanese ("見習い剣士") but data files may use English. Always verify language consistency across PlayerData, game data, and type definitions.

**Context Provider Scope:**
When state needs to persist across screen transitions (dungeon run → battle → dungeon), place the Context Provider high enough in the tree. Test full navigation flow (A → B → A).

**React Hooks Rules:**
All hooks must be called at the top level, before any conditional returns, in the same order every render.

```tsx
// BAD - hook after early return
if (condition) return <Early />;
useEffect(() => {...});

// GOOD - hook before early return
useEffect(() => {...});
if (condition) return <Early />;
```

**CSS Class Name Collision (Global CSS Conflict):**
Plain CSS has no native scoping. When the same class name is defined in multiple files, unintended styles may be applied.

Example: `.enemy-card` defined in both `Library.css` and `battle-status.css` → Library's background color appears on battle screen.

**Solutions:**
1. **Scope with parent element (Recommended)**
   ```css
   /* Library only */
   .library-screen .enemy-card { background: ...; }

   /* Battle only */
   .battle-screen .enemy-card { background: ...; }
   ```

2. **Use BEM naming convention**
   ```css
   .library__enemy-card { ... }
   .battle__enemy-card { ... }
   ```

3. **Always search for existing class names before creating new CSS**
   ```bash
   grep -r "\.enemy-card" src/ui/css/
   ```

**Rule:** Generic class names (`.card`, `.item`, `.enemy-card`, etc.) must always be scoped with a parent element or prefix.
