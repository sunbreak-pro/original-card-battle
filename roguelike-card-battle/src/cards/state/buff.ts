import type { BuffDebuffState, BuffDebuffMap } from "../type/baffType";
import { type BuffDebuffType, BUFF_EFFECTS } from "../type/baffType";

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

export const calculateEndTurnDamage = (map: BuffDebuffMap): number => {
  let buffDamage = 0;

  map.forEach((buff) => {
    switch (buff.name) {
      case "burn":
        buffDamage += buff.value;
        if (map.has("fireField")) {
          buffDamage += buff.value * 0.5;
        }
        break;
      case "poison":
        buffDamage += buff.value;
        break;
    }
  });

  return buffDamage;
};

export const calculateStartTurnHealing = (
  map: BuffDebuffMap
): { hp: number; shield: number } => {
  let hp = 0;
  let shield = 0;

  map.forEach((buff) => {
    switch (buff.name) {
      case "regeneration":
        hp += buff.value * buff.stacks;
        break;
      case "shieldRegen":
        shield += buff.value * buff.stacks;
        break;
    }
  });
  if (map.has("curse")) {
    hp = Math.floor(hp * 0.2);
  }
  if (map.has("overCurse")) {
    hp = Math.floor(hp * 0.5);
  }

  return { hp, shield };
};

export const calculateAttackMultiplier = (map: BuffDebuffMap): number => {
  let multiplier = 1.0;

  map.forEach((buff) => {
    switch (buff.name) {
      case "atkUpMinor":
      case "atkUpMajor":
        multiplier *= 1 + (buff.value / 100);
        break;
      case "atkDownMinor":
      case "atkDownMajor":
        multiplier *= 1 - (buff.value / 100);
        break;
    }
  });
  return multiplier;
};

export const calculateDefenseMultiplier = (map: BuffDebuffMap): number => {
  let multiplier = 1.0;

  map.forEach((buff) => {
    switch (buff.name) {
      case "defUpMinor":
      case "defUpMajor":
        multiplier *= 1 + (buff.value / 100);
        break;
      case "defDownMinor":
      case "defDownMajor":
        multiplier *= 1 - (buff.value / 100);
        break;
    }
  });
  return multiplier;
};

export const canAct = (map: BuffDebuffMap): boolean => {
  return !map.has("stun");
};

export const calculateEnergyModifier = (map: BuffDebuffMap): number => {
  let modifier = 0;

  map.forEach((buff) => {
    if (buff.name === "slow") {
      modifier -= 1;
    }
    if (buff.name === "energyRegen") {
      modifier += buff.value * buff.stacks;
    }
  });

  return modifier;
};

export const calculateDrawModifier = (map: BuffDebuffMap): number => {
  let modifier = 0;

  map.forEach((buff) => {
    if (buff.name === "drawPower") {
      modifier += buff.value * buff.stacks;
    }
  });

  return modifier;
};
export const removeDebuffs = (map: BuffDebuffMap, count: number): BuffDebuffMap => {
  const debuffs: BuffDebuffType[] = [];

  map.forEach((_, type) => {
    if (BUFF_EFFECTS[type].isDebuff) {
      debuffs.push(type);
    }
  });
  const shuffled = debuffs.sort(() => Math.random() - 0.5);
  const toRemove = shuffled.slice(0, Math.min(count, shuffled.length));

  const newMap = new Map(map);
  toRemove.forEach(type => newMap.delete(type));

  return newMap;
};
export const canApplyDebuff = (map: BuffDebuffMap, debuffType: BuffDebuffType): boolean => {
  if (map.has("immunity")) {
    return !BUFF_EFFECTS[debuffType].isDebuff;
  }
  return true;
};
