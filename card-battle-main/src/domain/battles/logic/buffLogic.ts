import { type BuffDebuffType, type BuffDebuffState, type BuffDebuffMap } from "../type/baffType";
import { BUFF_EFFECTS } from "../data/buffData";
export const addOrUpdateBuffDebuff = (
  map: BuffDebuffMap,
  name: BuffDebuffState["name"],
  duration: BuffDebuffState["duration"],
  value: BuffDebuffState["value"],
  stacks: BuffDebuffState["stacks"],
  isPermanent: BuffDebuffState["isPermanent"] = false,
  source?: BuffDebuffState["source"],
): BuffDebuffMap => {
  const newMap = new Map(map);
  const existing = newMap.get(name);

  if (existing) {
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
