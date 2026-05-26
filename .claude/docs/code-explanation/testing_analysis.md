# Testing Analysis

This document provides a comprehensive analysis of the testing infrastructure, current coverage, and gaps in the original-card-battle codebase.

---

## 1. Testing Fundamentals

### Types of Testing

| Type | Description | Example in This Codebase |
|------|-------------|--------------------------|
| **Unit Testing** | Tests individual functions or modules in isolation | `deck.test.ts` testing `shuffleArray()` |
| **Integration Testing** | Tests how multiple units work together | Testing `useCardExecution` with `damageCalculation` |
| **End-to-End (E2E) Testing** | Tests complete user flows through the application | Full battle from start to victory (not implemented) |

### Testing Pyramid

```
        /\
       /  \  E2E Tests (few, slow, expensive)
      /----\
     /      \  Integration Tests (moderate number)
    /--------\
   /          \  Unit Tests (many, fast, cheap)
  /------------\
```

The pyramid suggests writing **many unit tests**, **some integration tests**, and **few E2E tests**. This codebase benefits from unit testing because ~90% of domain logic consists of **pure functions** (functions with no side effects that return the same output for the same input).

### Common Testing Terminology

| Term | Definition |
|------|------------|
| **Test Suite** | A collection of related tests, created with `describe()` |
| **Test Case** | A single test, created with `it()` or `test()` |
| **Assertion** | A statement that checks expected behavior, e.g., `expect(value).toBe(5)` |
| **Mock** | A fake implementation that replaces real dependencies |
| **Stub** | A simplified mock that returns predetermined values |
| **Fixture** | Reusable test data or setup code |
| **Factory Function** | A helper that creates test objects with sensible defaults |
| **Coverage** | Percentage of code executed during tests |

### Why Testing Matters for Game Development

1. **Battle calculations must be deterministic** - Damage formulas, buff stacking, and turn order affect game balance
2. **Card interactions are complex** - Combinatorial effects between cards, buffs, and enemy actions
3. **State management is critical** - Deck shuffling, resource management, and progression systems
4. **Refactoring safety** - Tests allow confident changes to core systems
5. **Regression prevention** - Ensures bug fixes don't reintroduce old issues

---

## 2. Current Infrastructure Analysis

### Framework Configuration

| Component | Value |
|-----------|-------|
| Framework | Vitest 4.0.18 |
| Environment | jsdom |
| Globals | Enabled (`describe`, `it`, `expect` available globally) |
| Setup File | `src/test/setup.ts` |

### Dependencies

```json
{
  "@testing-library/jest-dom": "^6.9.1",  // DOM assertions
  "@testing-library/react": "^16.3.2",     // React component testing
  "jsdom": "^28.0.0",                       // DOM simulation
  "vitest": "^4.0.18"                       // Test runner
}
```

### Available NPM Scripts

| Script | Purpose |
|--------|---------|
| `npm run test` | Run tests in watch mode |
| `npm run test:run` | Single test run (CI mode) |
| `npm run test:coverage` | Run with coverage report |

### Vitest Configuration (`vitest.config.ts`)

```typescript
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',           // Browser-like environment
    globals: true,                   // No import needed for describe/it/expect
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{js,mjs,cjs,ts,mts,cts,jsx,tsx}'],
  },
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') }  // Path alias support
  },
});
```

### Setup File (`src/test/setup.ts`)

```typescript
import '@testing-library/jest-dom';  // Adds DOM matchers to expect()

// Mock import.meta.env for tests
Object.defineProperty(import.meta, 'env', {
  value: {
    DEV: true,
    PROD: false,
    MODE: 'test',
  },
});
```

---

## 3. Existing Test Coverage

### Test File 1: `src/domain/cards/decks/__tests__/deck.test.ts`

**31 test cases** covering the core deck manipulation functions:

| Function | Tests | Key Patterns |
|----------|-------|--------------|
| `shuffleArray()` | 5 | Math.random mocking, immutability verification |
| `shuffleDiscardIntoDraw()` | 4 | Pile manipulation, edge cases |
| `drawCards()` | 9 | Recycle logic, boundary conditions |
| `discardCards()` | 6 | Array concatenation, order preservation |
| `createInitialDeck()` | 7 | Card instantiation, ID generation |

**Notable Testing Patterns:**

1. **Factory Function** for creating test cards:
```typescript
function createTestCard(id: string, cardTypeId?: string): Card {
  return {
    id,
    cardTypeId: cardTypeId ?? id,
    name: `Card ${id}`,
    description: 'Test card',
    characterClass: 'swordsman',
    baseDamage: 5,
    cost: 1,
    element: ['physics'],
    tags: ['attack'],
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
  };
}
```

2. **Math.random Mocking** for deterministic shuffle tests:
```typescript
beforeEach(() => {
  vi.spyOn(Math, 'random');
});

it('shuffles array (not always same order)', () => {
  vi.mocked(Math.random)
    .mockReturnValueOnce(0.1)
    .mockReturnValueOnce(0.9)
    .mockReturnValueOnce(0.5);
  // ...
});
```

3. **Immutability Verification**:
```typescript
it('does not mutate original array', () => {
  const input = [1, 2, 3, 4, 5];
  const original = [...input];
  shuffleArray(input);
  expect(input).toEqual(original);
});
```

### Test File 2: `src/domain/battles/calculators/__tests__/damageCalculation.test.ts`

**35 test cases** covering damage calculation logic:

| Function | Tests | Key Patterns |
|----------|-------|--------------|
| `resolveDamageType()` | 9 | Element → damage type mapping |
| `calculateDamage()` | 13 | Critical hits, buff handling, null safety |
| `applyDamageAllocation()` | 13 | Guard/AP/HP damage distribution |

**Notable Testing Patterns:**

1. **Stats Factory with Overrides**:
```typescript
function createBattleStats(overrides: Partial<BattleStats> = {}): BattleStats {
  return {
    hp: 100,
    maxHp: 100,
    ap: 20,
    maxAp: 20,
    guard: 0,
    speed: 40,
    buffDebuffs: new Map(),
    ...overrides,
  };
}
```

2. **Grouped Test Cases** with nested `describe`:
```typescript
describe('applyDamageAllocation', () => {
  describe('guard only (no AP)', () => { /* ... */ });
  describe('guard with AP backup', () => { /* ... */ });
  describe('no guard', () => { /* ... */ });
  describe('edge cases', () => { /* ... */ });
});
```

3. **Edge Case Coverage**:
```typescript
it('handles 0 damage', () => { /* ... */ });
it('handles large damage values', () => { /* ... */ });
it('handles card without baseDamage property', () => { /* ... */ });
```

---

## 4. Gap Analysis

### Critical Priority (Battle-Affecting)

These systems directly affect game balance and player experience during combat:

| File | Functions to Test | Complexity |
|------|-------------------|------------|
| `buffLogic.ts` | `addBuffDebuff()`, `removeBuffDebuff()`, `tickBuffDurations()`, `hasImmunity()` | High - CRUD operations, duration tracking, immunity checks |
| `buffCalculation.ts` | `calculateAttackModifier()`, `calculateDefenseModifier()`, `calculateDotDamage()`, `calculateHealAmount()` | High - Buff stacking, percentage calculations |
| `equipmentStats.ts` | `calculateEquipmentAP()`, `applyDurabilityDamage()` | Medium - Multi-slot aggregation, degradation |
| `masteryManager.ts` | `calculateMasteryLevel()`, `getMasteryBonuses()` | Medium - Level thresholds, bonus curves |
| `speedCalculation.ts` | `calculateTurnOrder()`, `compareSpeed()` | Medium - Sort stability, tie-breaking |

### High Priority (Core Gameplay)

These systems affect dungeon exploration and progression:

| File | Functions to Test | Complexity |
|------|-------------------|------------|
| `dungeonLogic.ts` | `generateMap()`, `isNodeSelectable()`, `getAdjacentNodes()` | High - Procedural generation, graph traversal |
| `enemyAI.ts` | `selectAction()`, `evaluateTarget()`, `getPatternWeight()` | High - Decision trees, energy management |
| `blacksmithLogic.ts` | `calculateUpgradeCost()`, `upgradeEquipment()`, `repairEquipment()`, `dismantleEquipment()` | Medium - Cost formulas, success rates |
| `shopLogic.ts` | `calculatePrice()`, `canAfford()`, `purchaseItem()` | Low-Medium - Price modifiers, stock management |

### Medium Priority (Supporting Systems)

| File | Functions to Test | Complexity |
|------|-------------------|------------|
| `cardDerivation.ts` | `checkUnlockConditions()`, `deriveCard()` | Medium - Condition evaluation |
| `elementalSystem.ts` | `buildResonanceStack()`, `consumeResonance()`, `calculateBonusDamage()` | Medium - Mage class mechanics |
| `swordEnergySystem.ts` | `gainEnergy()`, `consumeEnergy()`, `calculateEnergyBonus()` | Medium - Swordsman class mechanics |
| `sanctuaryLogic.ts` | `calculateHealingCost()`, `restoreHealth()`, `purchaseBuff()` | Low - Simple calculations |
| `generateItem.ts` | `generateRandomItem()`, `rollRarity()`, `selectAffixes()` | Medium - RNG with weights |

### Lower Priority (UI/Integration)

| File | Type | Notes |
|------|------|-------|
| `useEquipmentAP.ts` | Hook | Requires `renderHook` from testing-library |
| `useCardExecution.ts` | Hook | Complex dependencies, needs integration approach |
| `useBattleOrchestrator.ts` | Hook | Orchestration layer, best tested via integration |
| `usePlayerDeck.ts` | Hook | State management integration |
| Context Providers | Component | Full component testing needed |

---

## 5. Missing Infrastructure

### Not Configured

| Missing Item | Purpose | Recommendation |
|--------------|---------|----------------|
| Coverage Thresholds | Enforce minimum coverage | Add to `vitest.config.ts` |
| Mock Directory (`__mocks__/`) | Centralized mock modules | Create for frequently mocked modules |
| Test Fixtures Directory | Reusable test data | Create `src/test/fixtures/` |
| Factory Utilities | Centralized object creation | Create `src/test/factories/` |

### Recommended Configuration Additions

```typescript
// vitest.config.ts additions
test: {
  coverage: {
    provider: 'v8',
    reporter: ['text', 'json', 'html'],
    thresholds: {
      branches: 70,
      functions: 70,
      lines: 70,
      statements: 70,
    },
  },
}
```

### Missing Test Utilities

1. **Centralized Factory Functions**
```typescript
// src/test/factories/card.ts
export function createTestCard(overrides?: Partial<Card>): Card { /* ... */ }

// src/test/factories/enemy.ts
export function createTestEnemy(overrides?: Partial<Enemy>): Enemy { /* ... */ }
```

2. **Hook Testing Setup** (no examples exist in codebase)
```typescript
import { renderHook, act } from '@testing-library/react';

const { result } = renderHook(() => useEquipmentAP());
act(() => {
  result.current.equip(helmet);
});
expect(result.current.totalAP).toBe(15);
```

---

## 6. Testing Approach Recommendations

### Highest ROI: Pure Function Testing

~90% of domain logic is pure functions. These are ideal testing targets:

| Characteristic | Benefit |
|----------------|---------|
| No side effects | No mocking required |
| Deterministic | Same input → same output |
| Isolated | No external dependencies |
| Fast | Millisecond execution |

**Example Pure Functions:**
- `calculateDamage()` in `damageCalculation.ts`
- `shuffleArray()` in `deck.ts`
- `calculateTurnOrder()` in `speedCalculation.ts`
- `calculateUpgradeCost()` in `blacksmithLogic.ts`

### Hook Testing with renderHook

For React hooks that manage state:

```typescript
import { renderHook, act } from '@testing-library/react';

describe('useEquipmentAP', () => {
  it('calculates total AP from equipped items', () => {
    const { result } = renderHook(() => useEquipmentAP());

    act(() => {
      result.current.equipItem('helmet', { ap: 10 });
      result.current.equipItem('armor', { ap: 20 });
    });

    expect(result.current.totalAP).toBe(30);
  });
});
```

### Component Testing Patterns

For UI components with user interactions:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';

describe('BattleCard', () => {
  it('displays card cost and damage', () => {
    render(<BattleCard card={mockCard} />);

    expect(screen.getByText('Cost: 2')).toBeInTheDocument();
    expect(screen.getByText('Damage: 10')).toBeInTheDocument();
  });
});
```

### Integration Testing Between Systems

For testing system interactions (e.g., buff + damage calculation):

```typescript
describe('Buff + Damage Integration', () => {
  it('attack buff increases final damage', () => {
    const attacker = createBattleStats();
    addBuffDebuff(attacker.buffDebuffs, 'attack_up', { power: 1.5 });

    const result = calculateDamage(attacker, defender, card);

    expect(result.finalDamage).toBe(15); // 10 base × 1.5 buff
  });
});
```

### Mocking Strategies

| Scenario | Approach |
|----------|----------|
| Random values | `vi.spyOn(Math, 'random').mockReturnValue(0.5)` |
| Date/Time | `vi.useFakeTimers()` |
| External modules | `vi.mock('@/module')` |
| Context values | Wrap with custom Provider |

---

## Summary

| Metric | Current State |
|--------|---------------|
| Test Files | 2 |
| Total Test Cases | 66 |
| Tested Systems | Deck manipulation, Damage calculation |
| Untested Systems | ~60 modules |
| Test Framework | Vitest 4.0.18 (modern, fast) |
| Infrastructure | Basic setup, missing factories/fixtures |

The testing foundation is solid with Vitest properly configured. The existing tests demonstrate good patterns (factory functions, mocking, edge cases). The primary gap is coverage breadth - most domain logic modules lack tests entirely. Priority should be given to battle-affecting calculations and core gameplay systems where bugs have the highest impact on player experience.
