# Project Memory

## Quick Reference

- **Current Phase:** Phase 6 Complete + CSS Refactoring
- **Dev Server:** http://localhost:5173/
- **Last Updated:** 2026-01-22

---

## Active Tasks

- Test character selection flow (clear localStorage, verify flow works)
- Test Library tabs (Cards, Enemies, Tips)
- Mage/Summoner classes show "Coming Soon" - cards not yet implemented
- Connect Sanctuary effects to battle/dungeon systems

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
