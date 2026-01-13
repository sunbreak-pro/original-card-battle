# Roguelike Card Battle Game - Development Constitution

## CRITICAL: Working Directory

**Current Working Directory:**
```
/Users/newlife/Downloads/Desktop/roguelike-card/
```

**Project structure:**
```
roguelike-card/                    # Git root & Workspace root (CURRENT DIRECTORY)
├── package.json                   # Workspace package.json (npm commands work here!)
├── .claude/
└── roguelike-card-battle/         # Actual project root
    ├── package.json
    ├── src/
    ├── public/
    └── ...
```

**NPM Commands:**
You can now run npm commands from EITHER directory:
- ✅ `npm run dev` (from roguelike-card/) - runs workspace script
- ✅ `cd roguelike-card-battle && npm run dev` - runs directly

**When referencing file paths:**
- ✅ CORRECT: `roguelike-card-battle/src/domain/camps/types/ItemTypes.ts`
- ❌ WRONG: `src/domain/camps/types/ItemTypes.ts` (this would be git root)

---

## Project Identity

**Genre:** Deck-Building RPG
**Theme:** Dark Fantasy - "Those who don the corruption will dwell in madness, know chaos, be tormented by emptiness, and gaze into the abyss"
**Platform:** PC (Web Browser)
**Target:** Casual yet strategic players inspired

---

## Tech Stack (STRICT)

### Core Technologies

```
Language: TypeScript (strict mode)
Framework: React 18+
Build Tool: Vite
UI Framework: Tailwind CSS (ONLY - consult before alternatives)
State Management: React Hooks (useState, useReducer, Context API)
Linter: ESLint with auto-fix
Package Manager: npm
```

### Version Requirements

- **Node.js**: 18+ recommended
- **TypeScript**: 5.0+
- **React**: 18.2+

### Forbidden Technologies

- ❌ jQuery (completely banned)
- ❌ Global state libraries (Redux, MobX, Zustand) - use React hooks instead
- ❌ Class components - use functional components only
- ❌ Inline styles

### CSS Unit Standards

**Viewport-Relative Units (REQUIRED for UI sizing):**

To ensure consistent responsive layouts across different screen sizes, always use viewport units for UI element sizing:

- **vh (Viewport Height)**: Use for vertical spacing, padding, margins, and font sizes
- **vw (Viewport Width)**: Use for horizontal spacing, padding, margins, and widths
- **Rationale**: Fixed units (rem, px) can cause layout overflow issues when designing full-screen UIs

**Examples:**
```css
/* ✅ GOOD - Viewport units scale with screen size */
.screen-container {
  height: 100vh;
  padding: 5vh 3vw;
}

.title {
  font-size: 5vh;
  margin-bottom: 2vh;
}

.content {
  max-width: 90vw;
  gap: 2vw;
}

/* ❌ BAD - Fixed units can cause overflow */
.screen-container {
  padding: 2rem;  /* May overflow on smaller screens */
}

.title {
  font-size: 2rem;  /* Doesn't scale with viewport */
}
```

**When to use fixed units:**
- Border widths (2px borders are acceptable)
- Border radius (8px, 12px)
- Very small spacing where precision matters (< 0.5rem equivalent)

**Conversion Guidelines:**
- `1rem` → `1.8vh` (for font sizes)
- `1rem` → `1.5vh` (for padding/margins vertically)
- `1rem` → `1vw` (for padding/margins horizontally)
- Adjust multipliers based on context and testing

---

## Project Structure (DDD-Inspired)

**Core Principle:** Domain logic separate from UI

```
src/
├── domain/         # Game logic (cards, battles, equipments, characters, camp, dungeon)
├── ui/             # UI components (battleUI, dungeonUI, campUI, commonUI, enemyUI, css)
├── assets/         # Static files (images, sounds, JSON data)
└── utils/          # Shared utilities(User)
```

**Detailed structure:** See `/docs/DIRECTORY_STRUCTURE.md`

### Directory Placement Rules

**CRITICAL RULES:**

1. **Card logic** -> `src/domain/cards/logic/`
2. **Battle logic** ->`src/domain/battles/logic`
3. **Battle UI** -> `src/ui/battleUI/`
4. **Game Save data (JSON)** -> `src/assets/data/`
5. **Type definitions** -> `src/domain/*/type/`
6. **Shared utilities** -> `src/utils/`

**WHY DDD-inspired?**

- Clear separation of concerns
- Game logic independent of UI
- Easier testing and maintenance

**Token cost consideration:**

- If directory structure becomes too deep, we will consolidate
- Prioritize clarity over strict DDD dogma

---

## Coding Standards

### TypeScript Strictness

```typescript
// Use type guards, explicit types, avoid 'any'
function isCard(value: unknown): value is Card {
  return typeof value === "object" && value !== null && "id" in value;
}
```

### Naming Conventions

- Types/Interfaces: `PascalCase`
- Functions: `camelCase` (verb-first: `calculateDamage`)
- Constants: `UPPER_SNAKE_CASE`
- Files: `kebab-case.ts`

### Component Pattern

```tsx
// Functional components only, hooks at top, clear prop types
interface BattleProps {
  depth: Depth;
  playerDeck: Card[];
}

export const Battle: React.FC<BattleProps> = ({ depth, playerDeck }) => {
  const [hp, setHp] = useState(Swordsman_status.hp);
  // ... hooks, effects, handlers, return
};
```

---

## CRITICAL: Immutable Code Zones

### 🔒 DO NOT MODIFY - Deck System

**Location:** `src/domain/battles/decks/`

**Files:**

- `deck.ts` - Card drawing and shuffling logic
- `deckReducter.ts` - Deck state management

**Reason:** These systems are battle-tested and stable. Modifying shuffle logic can break game balance and introduce subtle bugs.

**If changes are needed:**

1. Consult with the developer first
2. Create a new version alongside the old one
3. Test extensively before replacing

---

## Game Design Principles

### Dark Fantasy Aesthetics

**Visual Reference:** Slay the Spire's clarity + darker tone  
**Core Palette:** Deep purples, blacks, corrupted greens

**Depth Themes:** See `/blueprint/dungeon_theme_design.md` for full color palettes

### Gameplay Philosophy

- **Clarity over realism** - Always show exact numbers
- **Predict outcomes** before player actions
- **Clear visual feedback** for all state changes
- **Minimal hidden information** (unless intentional design)

---

## Data Management

**Format:** JSON files in `src/assets/data/`

```typescript
// Import and parse with type safety
import CARDS_DATA from "@/assets/data/cards.json";
const cards: Card[] = CARDS_DATA.map((data) => parseCard(data));
```

**Schema examples:** See individual JSON files for structure

---

## Development Workflow

### Process

1. Read docs → 1. Define types → 2. Implement logic → 3. Connect UI

### Testing

Unit test game logic with clear assertions. Example:

```typescript
expect(calculateDamage(attacker, defender, magicCard, 3)).toBe(basePower * 4);
```

---

## Documentation References

**Blueprint Docs:** `/blueprint/` contains all detailed game specs

- `INTEGRATED_GAME_SYSTEM_DESIGN.md` - Full system overview
- `battle/battle_logic.md` - Combat calculations
- `card/NEW_CHARACTER_SYSTEM_DESIGN.md` - Character/card mechanics
- `equipment/equipment_system.md` - Equipment rules
- `return_system.md` - Survival mechanics

**When to check:** Before implementing any game system

---

## ESLint Configuration

**Auto-fix enabled.** Do NOT manually format code or explain linting rules.  
Use: `npm run lint --fix`

---

## Communication Style

### Output Format Rules

**1. Please write comments in plain English**

**2. Emoji Usage (STRICTLY LIMITED):**

- ✅ Allowed ONLY in: Code examples (✅ GOOD / ❌ BAD), critical warnings (🔒)
- ❌ Forbidden in: Prose, explanations, casual responses, headings

**Example:**

```typescript
// ✅ GOOD: Allowed in code comments
const value = parseValue(data);

// Response to user (NO emoji):
("The function successfully parsed the data.");
```

### Language & Tone

- **日本語で対応** (code examples in English)
- Explain game mechanics in Japanese
- **Always ask before:** Modifying deck logic, changing battle calculations, restructuring directories

### Error Handling

Fail fast with descriptive errors:

```typescript
throw new Error(`Invalid depth: ${depth}. Must be 1-5.`);
```

---

## Performance Guidelines

Memoize expensive calculations, use stable callbacks. Avoid premature optimization.

```tsx
const damage = useMemo(() => calculateDamage(...), [deps]);
const handlePlay = useCallback((card) => {...}, []);
```

---

## Common Pitfalls

1. **State mutation** - Use immutable updates: `setPlayer(prev => ({...prev, hp: prev.hp - dmg}))`
2. **Missing deps** - Complete dependency arrays in useEffect/useMemo
3. **Unsafe assertions** - Use type guards before casting

---

## Memory Management (Active)

**Location:** `.claude/MEMORY.md`

**Core Principle:** This file is the "Project Diary" and single source of truth for current status.

**Workflow Rules (STRICT):**

1.  **Session Start:** ALWAYS read `.claude/MEMORY.md` first to load context.
2.  **Session End:** ALWAYS update `.claude/MEMORY.md` with:
    - Current progress status
    - Next immediate actions (specific tasks)
    - Unresolved blockers or bugs
3.  **Maintenance:**
    - Keep concise (bullet points)
    - Auto-archive completed tasks (remove to save tokens)
    - Record specific technical pitfalls/lessons (e.g., "v5.0 causes bug X")

## Quick Reference

**Blocked?** Check docs → Search existing code → Ask with rationale

**Game design:** `.claude/docs/`  
**Battle logic:** `.claude/docs/battle_document/battle_logic.md`  
**Card mechanics:** `.claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md`

---

## Priorities

1. **Correctness** over cleverness
2. **Clarity** over brevity
3. **Type safety** over convenience
4. **Game feel** over technical purity

**This is a game first, code second. Maintainable code enables long-term fun.**
