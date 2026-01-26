import { type BuffDebuffType, type BuffDebuffState, type BuffDebuffMap, type BuffOwner } from "../type/baffType";
import { BUFF_EFFECTS } from "../data/buffData";
export const addOrUpdateBuffDebuff = (
  map: BuffDebuffMap,
  name: BuffDebuffState["name"],
  duration: BuffDebuffState["duration"],
  value: BuffDebuffState["value"],
  stacks: BuffDebuffState["stacks"],
  isPermanent: BuffDebuffState["isPermanent"] = false,
  source?: BuffDebuffState["source"],
  appliedBy: BuffOwner = 'environment',
): BuffDebuffMap => {
  const newMap = new Map(map);
  const existing = newMap.get(name);

  if (existing) {
    // When updating existing buff, preserve original appliedBy
    newMap.set(name, {
      ...existing,
      stacks: existing.stacks + stacks,
      duration: Math.max(existing.duration, duration),
      value: Math.max(existing.value, value),
    });
  } else {
    newMap.set(name, {
      name,
      stacks,
      duration,
      value,
      isPermanent,
      source,
      appliedBy,
    });
  }
  return newMap;
};

export const removeBuffDebuff = (
  map: BuffDebuffMap,
  type: BuffDebuffType
): BuffDebuffMap => {
  const newMap = new Map(map);
  newMap.delete(type);
  return newMap;
};

export const removeAllDebuffs = (map: BuffDebuffMap): BuffDebuffMap => {
  const newMap = new Map<BuffDebuffState["name"], BuffDebuffState>();
  map.forEach((buff, type) => {
    if (!BUFF_EFFECTS[type].isDebuff) {
      newMap.set(type, buff);
    }
  });
  return newMap;
};

/**
 * @deprecated Use decreaseBuffDebuffDurationForPhase instead.
 * This function decreases all buff durations regardless of who applied them.
 */
export const decreaseBuffDebuffDuration = (
  map: BuffDebuffMap
): BuffDebuffMap => {
  const newMap = new Map<BuffDebuffType, BuffDebuffState>();

  map.forEach((buff, type) => {
    if (buff.isPermanent) {
      newMap.set(type, buff);
    } else if (buff.duration > 1) {
      newMap.set(type, {
        ...buff,
        duration: buff.duration - 1,
      });
    }
  });
  return newMap;
};

/**
 * Decrease buff/debuff durations only for buffs applied by the current actor.
 * This fixes the timing issue where enemy-applied debuffs were decreasing
 * at the wrong phase.
 *
 * @param map - Current buff/debuff map
 * @param currentActor - The actor whose phase is starting ('player' or 'enemy')
 * @returns New buff/debuff map with appropriate durations decreased
 */
export const decreaseBuffDebuffDurationForPhase = (
  map: BuffDebuffMap,
  currentActor: BuffOwner
): BuffDebuffMap => {
  const newMap = new Map<BuffDebuffType, BuffDebuffState>();

  map.forEach((buff, type) => {
    // Permanent buffs never decrease
    if (buff.isPermanent) {
      newMap.set(type, buff);
      return;
    }

    // Get appliedBy, defaulting to 'environment' for legacy data
    const owner = buff.appliedBy ?? 'environment';

    // Only decrease duration if this buff was applied by the current actor
    if (owner === currentActor) {
      if (buff.duration > 1) {
        newMap.set(type, {
          ...buff,
          duration: buff.duration - 1,
        });
      }
      // If duration <= 1, buff expires (not added to newMap)
    } else {
      // Buff was applied by someone else, keep it unchanged
      newMap.set(type, buff);
    }
  });

  return newMap;
};
