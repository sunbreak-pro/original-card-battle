import { describe, it, expect } from 'vitest';
import {
  addOrUpdateBuffDebuff,
  removeBuffDebuff,
  decreaseBuffDebuffDurationForPhase,
  getBuffValue,
  isStackable,
  removeNDebuffs,
} from '../buffLogic';
import type { BuffDebuffMap } from '@/types/battleTypes';

describe('buffLogic', () => {
  // Helper to create empty buff map
  function createEmptyBuffMap(): BuffDebuffMap {
    return new Map();
  }

  describe('addOrUpdateBuffDebuff', () => {
    it('should add a new buff to an empty map', () => {
      const map = createEmptyBuffMap();
      const result = addOrUpdateBuffDebuff(map, 'atkUpMinor', 3, 10, 1, false, undefined, 'player');

      expect(result.has('atkUpMinor')).toBe(true);
      const buff = result.get('atkUpMinor');
      expect(buff?.duration).toBe(3);
      expect(buff?.stacks).toBe(1);
      expect(buff?.appliedBy).toBe('player');
    });

    it('should stack existing buff when adding same type', () => {
      const map = createEmptyBuffMap();
      const map1 = addOrUpdateBuffDebuff(map, 'atkUpMinor', 3, 10, 1, false, undefined, 'player');
      const map2 = addOrUpdateBuffDebuff(map1, 'atkUpMinor', 2, 10, 2, false, undefined, 'player');

      const buff = map2.get('atkUpMinor');
      expect(buff?.stacks).toBe(3); // 1 + 2
      expect(buff?.duration).toBe(3); // Max of 3 and 2
    });

    it('should block debuff application when target has immunity', () => {
      const map = createEmptyBuffMap();
      // Add immunity buff
      const mapWithImmunity = addOrUpdateBuffDebuff(map, 'immunity', 3, 0, 1, false, undefined, 'player');
      // Try to add a debuff (e.g., 'bleed')
      const result = addOrUpdateBuffDebuff(mapWithImmunity, 'bleed', 3, 5, 1, false, undefined, 'enemy');

      expect(result.has('immunity')).toBe(true);
      expect(result.has('bleed')).toBe(false);
    });
  });

  describe('removeBuffDebuff', () => {
    it('should remove an existing buff', () => {
      const map = createEmptyBuffMap();
      const mapWithBuff = addOrUpdateBuffDebuff(map, 'atkUpMinor', 3, 10, 1, false, undefined, 'player');

      expect(mapWithBuff.has('atkUpMinor')).toBe(true);

      const result = removeBuffDebuff(mapWithBuff, 'atkUpMinor');
      expect(result.has('atkUpMinor')).toBe(false);
    });

    it('should return same map content when removing non-existent buff', () => {
      const map = createEmptyBuffMap();
      const result = removeBuffDebuff(map, 'atkUpMinor');

      expect(result.size).toBe(0);
    });
  });

  describe('decreaseBuffDebuffDurationForPhase', () => {
    it('should reduce durations and remove expired buffs', () => {
      let map = createEmptyBuffMap();
      map = addOrUpdateBuffDebuff(map, 'atkUpMinor', 1, 10, 1, false, undefined, 'player');
      map = addOrUpdateBuffDebuff(map, 'defUpMinor', 3, 10, 1, false, undefined, 'player');

      const result = decreaseBuffDebuffDurationForPhase(map, 'player');

      // atkUpMinor had duration 1, should be removed after decrement
      expect(result.has('atkUpMinor')).toBe(false);
      // defUpMinor had duration 3, should now be 2
      expect(result.get('defUpMinor')?.duration).toBe(2);
    });

    it('should not remove permanent buffs', () => {
      let map = createEmptyBuffMap();
      map = addOrUpdateBuffDebuff(map, 'atkUpMinor', 1, 10, 1, true, undefined, 'player');

      const result = decreaseBuffDebuffDurationForPhase(map, 'player');

      expect(result.has('atkUpMinor')).toBe(true);
    });
  });

  describe('getBuffValue', () => {
    it('should return the correct value for a buff type', () => {
      // atkUpMinor should have value 15 defined in BUFF_EFFECTS
      const value = getBuffValue('atkUpMinor');
      expect(value).toBe(15);
    });

    it('should return the correct value for bleed', () => {
      const value = getBuffValue('bleed');
      expect(value).toBe(3);
    });
  });

  describe('isStackable', () => {
    it('should return true for stackable debuffs like bleed', () => {
      const result = isStackable('bleed');
      expect(result).toBe(true);
    });

    it('should return false for non-stackable buffs like atkUpMinor', () => {
      const result = isStackable('atkUpMinor');
      expect(result).toBe(false);
    });
  });

  describe('removeNDebuffs', () => {
    it('should remove specified number of debuffs', () => {
      let map = createEmptyBuffMap();
      // Add some buffs and debuffs
      map = addOrUpdateBuffDebuff(map, 'atkUpMinor', 3, 10, 1, false, undefined, 'player');
      map = addOrUpdateBuffDebuff(map, 'bleed', 3, 5, 1, false, undefined, 'enemy');
      map = addOrUpdateBuffDebuff(map, 'poison', 3, 5, 1, false, undefined, 'enemy');

      const result = removeNDebuffs(map, 1);

      // Should have removed 1 debuff
      expect(result.has('atkUpMinor')).toBe(true); // Buff should remain
      // Either bleed or poison should be removed, not both
      const debuffCount = (result.has('bleed') ? 1 : 0) + (result.has('poison') ? 1 : 0);
      expect(debuffCount).toBe(1);
    });

    it('should not remove buffs', () => {
      let map = createEmptyBuffMap();
      map = addOrUpdateBuffDebuff(map, 'atkUpMinor', 3, 10, 1, false, undefined, 'player');
      map = addOrUpdateBuffDebuff(map, 'defUpMinor', 3, 10, 1, false, undefined, 'player');

      const result = removeNDebuffs(map, 2);

      expect(result.has('atkUpMinor')).toBe(true);
      expect(result.has('defUpMinor')).toBe(true);
    });
  });
});
