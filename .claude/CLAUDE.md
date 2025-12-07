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
UI Framework: Tailwind CSS (primary) OR Bootstrap (fallback)
State Management: React Hooks (useState, useReducer, Context API)
Linter: ESLint with auto-fix
Package Manager: npm
```

### Version Requirements

- **Node.js**: 18+ recommended
- **TypeScript**: 5.0+
- **React**: 18.2+

### Forbidden Technologies

- jQuery (completely banned)
- Global state libraries (Redux, MobX, Zustand) - use React hooks instead
- Class components - use functional components only
- Inline styles - use Tailwind CSS classes
- Don't use Emoji

---

## Project Structure (DDD-Inspired)

```
src/
├── domain/              # ゲームロジックの中核
│   ├── cards/           # カードシステム
│   │   ├── type/        # 型定義
│   │   ├── state/       # 状態管理・計算ロジック
│   │   └── data/        # カードデータ (JSON import)
│   ├── battles/         # 戦闘システム
│   │   ├── logic/       # 戦闘ロジック
│   │   ├── decks/       # デッキ管理
│   │   └── battleUI/    # 戦闘UI
│   ├── equipment/       # 装備システム
│   │   ├── type/
│   │   └── data/        # 装備データ (JSON)
│   └── enemies/         # 敵システム
│       ├── type/
│       └── data/        # 敵データ (JSON)
├── ui/                  # UIコンポーネント
│   ├── battle/          # 戦闘画面
│   ├── dungeon/         # ダンジョン探索
│   ├── base/            # 拠点画面
│   └── common/          # 共通UI
├── assets/              # 静的アセット
│   ├── images/
│   ├── sounds/
│   └── data/            # JSONデータファイル
└── utils/               # ユーティリティ

```

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
// GOOD: Type guards for safety
function isCard(value: unknown): value is Card {
  return typeof value === "object" && value !== null && "id" in value;
}

// GOOD: Explicit types
const damage: number = calculateDamage(attacker, defender, card);

// BAD: any type
const damage: any = calculateDamage(attacker, defender, card);
```

### Naming Conventions

```typescript
// Types/Interfaces: PascalCase
interface Card {}
type Depth = 1 | 2 | 3 | 4 | 5;

// Functions: camelCase with verb
function calculateDamage() {}
function applyStatusEffect() {}

// Constants: UPPER_SNAKE_CASE
const MAX_HAND_SIZE = 10;
const INITIAL_ENERGY = 3;

// Files: kebab-case
card - effect - calculator.ts;
battle - logic.ts;
```

### Component Structure

```tsx
// GOOD: Functional component with TypeScript
interface BattleProps {
  depth: Depth;
  playerDeck: Card[];
}

export const Battle: React.FC<BattleProps> = ({ depth, playerDeck }) => {
  // Hooks at the top
  const [hp, setHp] = useState(100);
  const [energy, setEnergy] = useState(3);

  // Effects
  useEffect(() => {
    // ...
  }, []);

  // Event handlers
  const handleCardPlay = (card: Card) => {
    // ...
  };

  // Render
  return <div className="battle-container">...</div>;
};
```

---

## CRITICAL: Immutable Code Zones

### DO NOT MODIFY - Deck System

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

**Visual Direction:**

- Inspired by Slay the Spire's clarity but darker tone
- Color palette: Deep purples, blacks, corrupted greens
- UI should feel ominous yet readable

**Depth Theme Progression:**

```
Depth 1 (Decay):     Gray, brown, sickly green
Depth 2 (Madness):   Purple, unstable red, yellow
Depth 3 (Chaos):     All colors mixing chaotically
Depth 4 (Void):      Deep purple, pure black, void white
Depth 5 (Abyss):     Absolute darkness with faint light
```

### Gameplay Clarity > Realism

- **Always show exact numbers** (damage, healing, shield)
- **Predict outcomes** before actions
- **Clear visual feedback** for state changes
- **No hidden information** unless intentional game design

---

## Data Management

### Card/Equipment Data Format

**Storage:** JSON files in `src/assets/data/`

```json
{
  "id": "phy_001",
  "name": "斬撃",
  "category": "physical",
  "cost": 0,
  "basePower": 10,
  "depthCurve": "neutral",
  "effects": {
    "damage": 10,
    "swordEnergy": 1
  }
}
```

**Loading Pattern:**

```typescript
// GOOD: Import JSON directly
import CARDS_DATA from "@/assets/data/cards.json";

// GOOD: Type-safe parsing
const cards: Card[] = CARDS_DATA.map((data) => parseCard(data));
```

---

## Development Workflow

### Before Writing Code

1. **Read relevant design docs** in `/blueprint/`
2. **Check existing implementations** in `/src/domain/`
3. **Understand the game system** being modified

### When Creating New Features

1. **Design first** - Write spec in `/blueprint/`
2. **Types second** - Define TypeScript interfaces
3. **Logic third** - Implement pure functions
4. **UI last** - Connect to React components

### Testing Strategy

```typescript
// Unit tests for game logic
describe("calculateDamage", () => {
  it("should apply depth multiplier to magic damage", () => {
    const damage = calculateDamage(attacker, defender, magicCard, 3);
    expect(damage).toBe(basePower * 4); // Depth 3 = 4x multiplier
  });
});
```

---

## Documentation References

### Game System Specs

All detailed specifications are in `/blueprint/`:

```
/blueprint/
├── INTEGRATED_GAME_SYSTEM_DESIGN.md    # 全システム統合設計
├── battle/
│   └── battle_logic.md                  # 戦闘ロジック詳細
├── card/
│   └── NEW_CHARACTER_SYSTEM_DESIGN.md   # キャラ・カードシステム
├── equipment/
│   └── equipment_system.md              # 装備システム
├── return_system.md                     # 生還システム
└── enemy_database/                      # 敵データベース
```

**When to reference:**

- Implementing card effects → `card/`
- Battle calculations → `battle/battle_logic.md`
- Equipment mechanics → `equipment/`
- Dungeon generation → `INTEGRATED_GAME_SYSTEM_DESIGN.md`

---

## ESLint Configuration

**Auto-fix on save:**

```json
{
  "editor.codeActionsOnSave": {
    "source.fixAll.eslint": true
  }
}
```

**Do NOT ask Claude to:**

- Format code manually (ESLint handles this)
- Fix linting errors manually (use `npm run lint --fix`)
- Explain basic formatting rules (trust ESLint)

---

## Communication Style

### When Responding

- **日本語で対応すること** (Respond in Japanese)
- Provide code examples in English (for consistency)
- Explain game mechanics in Japanese
- Ask clarifying questions before major changes

### Before Major Changes

**ALWAYS ask confirmation for:**

1. Modifying deck/shuffle logic
2. Changing core battle calculations
3. Altering game balance numbers
4. Restructuring directories

### Error Handling

```typescript
// GOOD: Descriptive error messages
throw new Error(`Invalid depth: ${depth}. Must be 1-5.`);

// GOOD: Fail fast with clear context
if (!card) {
  throw new Error(`Card not found: ${cardId}`);
}

// BAD: Silent failures
if (!card) return;
```

---

## Performance Guidelines

### React Performance

```tsx
// GOOD: Memoize expensive calculations
const damagePreview = useMemo(
  () => calculateDamage(attacker, defender, card, depth),
  [attacker, defender, card, depth]
);

// GOOD: Callback stability
const handleCardPlay = useCallback((card: Card) => {
  // ...
}, []);
```

### Avoid Premature Optimization

- **Write clear code first**
- **Optimize only when measured performance issues exist**
- **Trust React's rendering optimizations**

---

## Common Pitfalls to Avoid

### 1. State Mutation

```typescript
// BAD: Mutating state directly
player.hp -= damage;

// GOOD: Immutable update
setPlayer((prev) => ({ ...prev, hp: prev.hp - damage }));
```

### 2. Missing Dependencies

```typescript
// BAD: Incomplete dependency array
useEffect(() => {
  updateBattle(player, enemy);
}, []); // Missing player, enemy

// GOOD: Complete dependencies
useEffect(() => {
  updateBattle(player, enemy);
}, [player, enemy]);
```

### 3. Type Assertions Without Guards

```typescript
// BAD: Unsafe type assertion
const card = data as Card;

// GOOD: Type guard
if (isCard(data)) {
  const card = data;
}
```

---

## Project Status

### Completed Systems

- Core battle mechanics (HP, Guard, AP system)
- Deck management (draw, discard, shuffle)
- Basic card effects
- Depth scaling system
- Enemy database (all 5 depths)
- Equipment system design
- Survival system design

### In Progress 🚧

- Card database completion (30/90 cards)
- Equipment data implementation
- Shop system detailed design

### Pending 📋

- Room event design (15 types)
- Progress management system
- UI/UX implementation
- Balance adjustment

---

## Emergency Contacts

### If Unsure About:

- **Game design decisions** → Check `/blueprint/INTEGRATED_GAME_SYSTEM_DESIGN.md`
- **Battle calculations** → Check `/blueprint/battle/battle_logic.md`
- **Card mechanics** → Check `/blueprint/card/NEW_CHARACTER_SYSTEM_DESIGN.md`
- **TypeScript types** → Check `src/domain/*/type/`

### If Blocked:

1. Search existing code for similar implementations
2. Check blueprint documents
3. Ask clarifying questions
4. Propose solution with rationale

---

## Final Notes

**This CLAUDE.md is your constitution - follow it strictly.**

**Priorities:**

1. **Correctness** over cleverness
2. **Clarity** over brevity
3. **Type safety** over convenience
4. **Game feel** over technical purity

**Remember:**

- This is a **game first, code second**
- Players care about **fun**, not clean architecture
- But **maintainable code** enables long-term fun

**When in doubt:**

- Check the blueprints
- Ask questions
- Write clear, typed, tested code

---

**Document Version:** 1.0  
**Last Updated:** 2024-12-07  
**Maintained by:** こうだい
