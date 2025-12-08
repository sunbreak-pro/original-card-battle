import { BuffDebuffEffects } from "../data/BuffData";
import type { BuffDebuff, BuffDebuffMap } from "../type/baffType";
import type { BuffDebuffType } from "../type/baffType";
// バフ/デバフの種類

// バフ/デバフの効果

/**
 * バフ/デバフを追加または更新
 */
export const addOrUpdateBuffDebuff = (
  map: BuffDebuffMap,
  type: BuffDebuffType,
  stacks: number,
  duration: number,
  value: number,
  isPermanent: boolean = false,
  source?: string
): BuffDebuffMap => {
  const newMap = new Map(map);
  const existing = newMap.get(type);

  if (existing) {
    // 既存のバフ/デバフがある場合、スタックを加算
    newMap.set(type, {
      ...existing,
      stacks: existing.stacks + stacks,
      duration: Math.max(existing.duration, duration), // 長い方を採用
      value: Math.max(existing.value, value), // 大きい方を採用
    });
  } else {
    // 新規追加
    newMap.set(type, {
      type,
      stacks,
      duration,
      value,
      isPermanent,
      source,
    });
  }

  return newMap;
};

/**
 * バフ/デバフを削除
 */
export const removeBuffDebuff = (
  map: BuffDebuffMap,
  type: BuffDebuffType
): BuffDebuffMap => {
  const newMap = new Map(map);
  newMap.delete(type);
  return newMap;
};

/**
 * 全てのデバフを削除
 */
export const removeAllDebuffs = (map: BuffDebuffMap): BuffDebuffMap => {
  const newMap = new Map<BuffDebuffType, BuffDebuff>();
  map.forEach((buff, type) => {
    if (!BuffDebuffEffects[type].isDebuff) {
      newMap.set(type, buff);
    }
  });
  return newMap;
};

/**
 * ターン経過による持続時間減少
 */
export const decreaseBuffDebuffDuration = (
  map: BuffDebuffMap
): BuffDebuffMap => {
  const newMap = new Map<BuffDebuffType, BuffDebuff>();

  map.forEach((buff, type) => {
    if (buff.isPermanent) {
      // 永続は変更なし
      newMap.set(type, buff);
    } else if (buff.duration > 1) {
      // 持続時間を減少
      newMap.set(type, {
        ...buff,
        duration: buff.duration - 1,
      });
    }
    // duration === 1 の場合は削除（新Mapに追加しない）
  });

  return newMap;
};

/**
 * ターン終了時の持続ダメージ計算
 */
export const calculateEndTurnDamage = (map: BuffDebuffMap): number => {
  let totalDamage = 0;

  map.forEach((buff) => {
    switch (buff.type) {
      case "burn":
        totalDamage += buff.stacks * 3;
        break;
      case "bleed":
        totalDamage += buff.stacks * 2;
        break;
      case "poison":
        totalDamage += buff.stacks * 2;
        break;
      case "curse":
        totalDamage += buff.stacks * 2;
        break;
    }
  });

  return totalDamage;
};

/**
 * ターン開始時の回復・再生計算
 */
export const calculateStartTurnHealing = (
  map: BuffDebuffMap
): { hp: number; shield: number } => {
  let hp = 0;
  let shield = 0;

  map.forEach((buff) => {
    switch (buff.type) {
      case "regeneration":
        hp += buff.value * buff.stacks;
        break;
      case "shieldRegen":
        shield += buff.value * buff.stacks;
        break;
    }
  });

  // 呪いによる回復効果減少
  if (map.has("curse")) {
    hp = Math.floor(hp * 0.5);
  }

  // 回復効果減少デバフ
  if (map.has("healingDown")) {
    const healingDown = map.get("healingDown")!;
    hp = Math.floor(hp * (1 - healingDown.value / 100));
  }

  return { hp, shield };
};

/**
 * 攻撃力の倍率計算
 */
export const calculateAttackMultiplier = (map: BuffDebuffMap): number => {
  let multiplier = 1.0;

  map.forEach((buff) => {
    switch (buff.type) {
      case "atkUp":
        multiplier *= 1 + buff.value / 100;
        break;
      case "physicalUp":
        multiplier *= 1 + buff.value / 100;
        break;
      case "magicUp":
        multiplier *= 1 + buff.value / 100;
        break;
      case "atkDown":
        multiplier *= 1 - buff.value / 100;
        break;
      case "paralyze":
        multiplier *= 0.5;
        break;
      case "weak":
        multiplier *= 0.7;
        break;
    }
  });

  return multiplier;
};

/**
 * 防御力の倍率計算
 */
export const calculateDefenseMultiplier = (map: BuffDebuffMap): number => {
  let multiplier = 1.0;

  map.forEach((buff) => {
    switch (buff.type) {
      case "defUp":
        multiplier *= 1 + buff.value / 100;
        break;
      case "defDown":
        multiplier *= 1 - buff.value / 100;
        break;
    }
  });

  return multiplier;
};

/**
 * 行動可能かどうか判定
 */
export const canAct = (map: BuffDebuffMap): boolean => {
  return !map.has("freeze") && !map.has("stun");
};

/**
 * エナジー修正値を計算
 */
export const calculateEnergyModifier = (map: BuffDebuffMap): number => {
  let modifier = 0;

  map.forEach((buff) => {
    if (buff.type === "slow") {
      modifier -= 1;
    }
    if (buff.type === "energyRegen") {
      modifier += buff.value * buff.stacks;
    }
  });

  return modifier;
};

/**
 * ドロー枚数の修正値を計算
 */
export const calculateDrawModifier = (map: BuffDebuffMap): number => {
  let modifier = 0;

  map.forEach((buff) => {
    if (buff.type === "drawPower") {
      modifier += buff.value * buff.stacks;
    }
  });

  return modifier;
};

/**
 * 指定数のデバフをランダムに解除
 */
export const removeDebuffs = (map: BuffDebuffMap, count: number): BuffDebuffMap => {
  const debuffs: BuffDebuffType[] = [];

  map.forEach((_, type) => {
    if (BuffDebuffEffects[type].isDebuff) {
      debuffs.push(type);
    }
  });

  // ランダムにシャッフル
  const shuffled = debuffs.sort(() => Math.random() - 0.5);
  const toRemove = shuffled.slice(0, Math.min(count, shuffled.length));

  const newMap = new Map(map);
  toRemove.forEach(type => newMap.delete(type));

  return newMap;
};

/**
 * デバフが付与可能かチェック（immunity判定）
 */
export const canApplyDebuff = (map: BuffDebuffMap, debuffType: BuffDebuffType): boolean => {
  // immunityバフがある場合、デバフ無効
  if (map.has("immunity")) {
    return !BuffDebuffEffects[debuffType].isDebuff;
  }
  return true;
};
