# Roguelike Card Battle Game - Development Constitution

## Project Identity

**Genre:** Roguelike Deck-Building RPG  
**Theme:** Dark Fantasy - "Those who don the corruption will dwell in madness, know chaos, be tormented by emptiness, and gaze into the abyss"  
**Platform:** PC (Web Browser)  
**Target:** Casual yet strategic players inspired by Slay the Spire

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
- ❌ Inline styles - use Tailwind CSS classes

---

## Project Structure (DDD-Inspired)

**Core Principle:** Domain logic separate from UI

```
src/
├── domain/         # Game logic (cards, battles, equipment, enemies)
├── ui/             # UI components (battle, dungeon, base, common)
├── assets/         # Static files (images, sounds, JSON data)
└── utils/          # Shared utilities
```

**Detailed structure:** See `/docs/DIRECTORY_STRUCTURE.md`

### Directory Placement Rules

**CRITICAL RULES:**

1. **Card logic** → `src/domain/cards/state/`
2. **Battle UI** → `src/domain/battles/battleUI/`
3. **Game data (JSON)** → `src/assets/data/` or `src/domain/*/data/`
4. **Type definitions** → `src/domain/*/type/`
5. **Shared utilities** → `src/utils/`

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
  const [hp, setHp] = useState(100);
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

1. Read `/blueprint/` docs → 2. Define types → 3. Implement logic → 4. Connect UI

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

**Emoji Usage (STRICTLY LIMITED):**

- ✅ Allowed ONLY in: Code examples (✅ GOOD / ❌ BAD), critical warnings (🔒)
- ❌ Forbidden in: Prose, explanations, casual responses, headings
- Reason: Claude tends to overuse emojis. Maintain professional clarity.

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

## Project Status Reference

**Current progress:** See `/docs/PROJECT_STATUS.md` for:

- Completed systems
- In-progress features
- Pending tasks
- Implementation priorities

**Why separate?** This document changes frequently. Keeping it in CLAUDE.md would require constant updates and increase token usage.

---

## Quick Reference

**Blocked?** Check blueprints → Search existing code → Ask with rationale

**Game design:** `/blueprint/INTEGRATED_GAME_SYSTEM_DESIGN.md`  
**Battle logic:** `/blueprint/battle/battle_logic.md`  
**Card mechanics:** `/blueprint/card/NEW_CHARACTER_SYSTEM_DESIGN.md`

---

## Priorities

1. **Correctness** over cleverness
2. **Clarity** over brevity
3. **Type safety** over convenience
4. **Game feel** over technical purity

**This is a game first, code second. Maintainable code enables long-term fun.**

---

**Version:** 2.0 | **Updated:** 2024-12-07 | **Maintainer:** こうだい
