import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  resolveDamageType,
  calculateDamage,
  applyDamageAllocation,
} from '../damageCalculation';
import type { Card } from '@/types/cardTypes';
import type { BattleStats, ElementType } from '@/types/characterTypes';
import type { BuffDebuffMap } from '@/types/battleTypes';

// Constants for tests
const GUARD_BLEED_THROUGH_MULTIPLIER = 0.50;

// Helper to create a minimal Card for testing
function createTestCard(
  baseDamage: number,
  elements: ElementType[] = ['physics']
): Card {
  return {
    id: 'test-card-1',
    cardTypeId: 'test-type',
    name: 'Test Card',
    description: 'A test card',
    characterClass: 'swordsman',
    baseDamage,
    cost: 1,
    element: elements,
    tags: ['attack'],
    useCount: 0,
    masteryLevel: 0,
    gemLevel: 0,
  };
}

// Helper to create minimal BattleStats
function createBattleStats(
  overrides: Partial<BattleStats> = {}
): BattleStats {
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

describe('resolveDamageType', () => {
  describe('priority: true > magical > physical', () => {
    it('returns "physical" for empty elements array', () => {
      expect(resolveDamageType([])).toBe('physical');
    });

    it('returns "physical" for physical element', () => {
      expect(resolveDamageType(['physics'])).toBe('physical');
    });

    it('returns "magical" for fire element', () => {
      expect(resolveDamageType(['fire'])).toBe('magical');
    });

    it('returns "magical" for ice element', () => {
      expect(resolveDamageType(['ice'])).toBe('magical');
    });

    it('returns "magical" for lightning element', () => {
      expect(resolveDamageType(['lightning'])).toBe('magical');
    });

    it('returns "magical" for dark element', () => {
      expect(resolveDamageType(['dark'])).toBe('magical');
    });

    it('returns "magical" for light element', () => {
      expect(resolveDamageType(['light'])).toBe('magical');
    });

    it('prioritizes "magical" over "physical" when both present', () => {
      expect(resolveDamageType(['physics', 'fire'])).toBe('magical');
      expect(resolveDamageType(['fire', 'physics'])).toBe('magical');
    });

    it('handles multiple magical elements', () => {
      expect(resolveDamageType(['fire', 'ice', 'lightning'])).toBe('magical');
    });
  });
});

describe('calculateDamage', () => {
  beforeEach(() => {
    // Mock Math.random for critical hit tests
    vi.spyOn(Math, 'random');
  });

  describe('basic damage calculation', () => {
    it('calculates base damage without buffs/debuffs', () => {
      const attacker = createBattleStats();
      const defender = createBattleStats();
      const card = createTestCard(10);

      const result = calculateDamage(attacker, defender, card);

      expect(result.finalDamage).toBe(10);
      expect(result.damageType).toBe('physical');
      expect(result.isCritical).toBe(false);
    });

    it('returns 0 damage for card with baseDamage 0', () => {
      const attacker = createBattleStats();
      const defender = createBattleStats();
      const card = createTestCard(0);

      const result = calculateDamage(attacker, defender, card);

      expect(result.finalDamage).toBe(0);
    });

    it('handles card without baseDamage property', () => {
      const attacker = createBattleStats();
      const defender = createBattleStats();
      const card = createTestCard(0);
      delete card.baseDamage;

      const result = calculateDamage(attacker, defender, card);

      expect(result.finalDamage).toBe(0);
    });
  });

  describe('critical hits', () => {
    it('applies 1.5x multiplier on critical hit', () => {
      vi.mocked(Math.random).mockReturnValue(0.1); // Ensures crit
      const attacker = createBattleStats();
      const defender = createBattleStats();
      const card = createTestCard(10);

      const result = calculateDamage(attacker, defender, card, 0.5); // 50% crit chance

      expect(result.isCritical).toBe(true);
      expect(result.finalDamage).toBe(15); // 10 * 1.5
    });

    it('does not crit when critBonus is 0', () => {
      vi.mocked(Math.random).mockReturnValue(0.1);
      const attacker = createBattleStats();
      const defender = createBattleStats();
      const card = createTestCard(10);

      const result = calculateDamage(attacker, defender, card, 0);

      expect(result.isCritical).toBe(false);
      expect(result.finalDamage).toBe(10);
    });

    it('does not crit when random exceeds critBonus', () => {
      vi.mocked(Math.random).mockReturnValue(0.8);
      const attacker = createBattleStats();
      const defender = createBattleStats();
      const card = createTestCard(10);

      const result = calculateDamage(attacker, defender, card, 0.5);

      expect(result.isCritical).toBe(false);
      expect(result.finalDamage).toBe(10);
    });
  });

  describe('damage types', () => {
    it('resolves physical damage type correctly', () => {
      const attacker = createBattleStats();
      const defender = createBattleStats();
      const card = createTestCard(10, ['physics']);

      const result = calculateDamage(attacker, defender, card);

      expect(result.damageType).toBe('physical');
    });

    it('resolves magical damage type correctly', () => {
      const attacker = createBattleStats();
      const defender = createBattleStats();
      const card = createTestCard(10, ['fire']);

      const result = calculateDamage(attacker, defender, card);

      expect(result.damageType).toBe('magical');
    });
  });

  describe('null/undefined buffDebuffs handling', () => {
    it('handles attacker with undefined buffDebuffs', () => {
      const attacker = createBattleStats({ buffDebuffs: undefined as unknown as BuffDebuffMap });
      const defender = createBattleStats();
      const card = createTestCard(10);

      const result = calculateDamage(attacker, defender, card);

      expect(result.finalDamage).toBe(10);
    });

    it('handles defender with undefined buffDebuffs', () => {
      const attacker = createBattleStats();
      const defender = createBattleStats({ buffDebuffs: undefined as unknown as BuffDebuffMap });
      const card = createTestCard(10);

      const result = calculateDamage(attacker, defender, card);

      expect(result.finalDamage).toBe(10);
    });
  });
});

describe('applyDamageAllocation', () => {
  describe('guard only (no AP)', () => {
    it('guard fully absorbs damage with bleed-through when no AP', () => {
      // Armor Break: 50% bypasses guard and goes to HP, remaining 50% absorbed by guard
      const defender = createBattleStats({ guard: 20, ap: 0 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      const bypassDamage = Math.floor(damage * GUARD_BLEED_THROUGH_MULTIPLIER); // 5
      const guardedDamage = damage - bypassDamage; // 5
      expect(result.guardDamage).toBe(guardedDamage);
      expect(result.apDamage).toBe(0);
      expect(result.hpDamage).toBe(bypassDamage);
    });

    it('guard partially absorbs, remainder goes to HP when no AP', () => {
      const defender = createBattleStats({ guard: 5, ap: 0 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(5);
      expect(result.apDamage).toBe(0);
      expect(result.hpDamage).toBe(5); // 10 - 5 = 5 remaining
    });
  });

  describe('guard with AP backup', () => {
    it('guard fully absorbs damage without bleed-through when AP exists', () => {
      const defender = createBattleStats({ guard: 20, ap: 10 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(10);
      expect(result.apDamage).toBe(0);
      expect(result.hpDamage).toBe(0);
    });

    it('guard partially absorbs, AP takes overflow', () => {
      const defender = createBattleStats({ guard: 5, ap: 20 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(5);
      expect(result.apDamage).toBe(5);
      expect(result.hpDamage).toBe(0);
    });

    it('guard and AP partially absorb, HP takes remainder', () => {
      const defender = createBattleStats({ guard: 5, ap: 3 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(5);
      expect(result.apDamage).toBe(3);
      expect(result.hpDamage).toBe(2); // 10 - 5 - 3 = 2
    });
  });

  describe('no guard', () => {
    it('AP fully absorbs damage when no guard', () => {
      const defender = createBattleStats({ guard: 0, ap: 20 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(0);
      expect(result.apDamage).toBe(10);
      expect(result.hpDamage).toBe(0);
    });

    it('AP partially absorbs, HP takes remainder', () => {
      const defender = createBattleStats({ guard: 0, ap: 5 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(0);
      expect(result.apDamage).toBe(5);
      expect(result.hpDamage).toBe(5);
    });

    it('HP takes all damage when no guard and no AP', () => {
      const defender = createBattleStats({ guard: 0, ap: 0 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(0);
      expect(result.apDamage).toBe(0);
      expect(result.hpDamage).toBe(10);
    });
  });

  describe('edge cases', () => {
    it('handles 0 damage', () => {
      const defender = createBattleStats({ guard: 10, ap: 10 });
      const damage = 0;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(0);
      expect(result.apDamage).toBe(0);
      expect(result.hpDamage).toBe(0);
    });

    it('handles exact guard match with AP backup', () => {
      const defender = createBattleStats({ guard: 10, ap: 5 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(10);
      expect(result.apDamage).toBe(0);
      expect(result.hpDamage).toBe(0);
    });

    it('handles exact AP match after guard depleted', () => {
      const defender = createBattleStats({ guard: 5, ap: 5 });
      const damage = 10;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(5);
      expect(result.apDamage).toBe(5);
      expect(result.hpDamage).toBe(0);
    });

    it('handles large damage values', () => {
      const defender = createBattleStats({ guard: 10, ap: 10 });
      const damage = 1000;

      const result = applyDamageAllocation(defender, damage);

      expect(result.guardDamage).toBe(10);
      expect(result.apDamage).toBe(10);
      expect(result.hpDamage).toBe(980);
    });
  });
});
