// バフ/デバフ効果のデータ定義
import type { BuffDebuffType } from "../type/baffType";
export const BuffDebuffEffects: Record<
  BuffDebuffType,
  {
    name: string;
    nameJa: string;
    description: string;
    color: string;
    isDebuff: boolean;
  }
> = {
  // デバフ - 持続ダメージ系
  burn: {
    name: "Burn",
    nameJa: "火傷",
    description: "毎ターン終了時、スタック×3のダメージ（シールド無視）",
    color: "#ff6600",
    isDebuff: true,
  },
  bleed: {
    name: "Bleed",
    nameJa: "出血",
    description: "毎ターン終了時、スタック×2のダメージ",
    color: "#cc0000",
    isDebuff: true,
  },
  poison: {
    name: "Poison",
    nameJa: "毒",
    description: "毎ターン終了時、スタック×2のダメージ（防御無視）",
    color: "#66cc00",
    isDebuff: true,
  },
  curse: {
    name: "Curse",
    nameJa: "呪い",
    description: "回復効果-50%、毎ターン終了時スタック×2のダメージ",
    color: "#9900cc",
    isDebuff: true,
  },
  // デバフ - 状態異常系
  slow: {
    name: "Slow",
    nameJa: "スロウ",
    description: "エナジー-1",
    color: "#4488ff",
    isDebuff: true,
  },
  freeze: {
    name: "Freeze",
    nameJa: "凍結",
    description: "行動不可",
    color: "#00ccff",
    isDebuff: true,
  },
  paralyze: {
    name: "Paralyze",
    nameJa: "麻痺",
    description: "攻撃力-50%",
    color: "#ffcc00",
    isDebuff: true,
  },
  stun: {
    name: "Stun",
    nameJa: "気絶",
    description: "行動不可",
    color: "#ffaa00",
    isDebuff: true,
  },
  weak: {
    name: "Weak",
    nameJa: "弱体化",
    description: "攻撃力-30%",
    color: "#888888",
    isDebuff: true,
  },
  // デバフ - 能力減少系
  defDown: {
    name: "Defense Down",
    nameJa: "防御力低下",
    description: "防御力がvalue%低下",
    color: "#ff8800",
    isDebuff: true,
  },
  atkDown: {
    name: "Attack Down",
    nameJa: "攻撃力低下",
    description: "攻撃力がvalue%低下",
    color: "#ff4444",
    isDebuff: true,
  },
  healingDown: {
    name: "Healing Down",
    nameJa: "回復効果減少",
    description: "回復効果がvalue%減少",
    color: "#cc6666",
    isDebuff: true,
  },
  // バフ - 能力上昇系
  atkUp: {
    name: "Attack Up",
    nameJa: "攻撃力上昇",
    description: "攻撃力がvalue%上昇",
    color: "#ff6666",
    isDebuff: false,
  },
  defUp: {
    name: "Defense Up",
    nameJa: "防御力上昇",
    description: "防御力がvalue%上昇",
    color: "#6666ff",
    isDebuff: false,
  },
  magicUp: {
    name: "Magic Up",
    nameJa: "魔力上昇",
    description: "魔力がvalue%上昇",
    color: "#cc66ff",
    isDebuff: false,
  },
  physicalUp: {
    name: "Physical Up",
    nameJa: "物理攻撃力上昇",
    description: "物理攻撃力がvalue%上昇",
    color: "#ff9966",
    isDebuff: false,
  },
  penetrationUp: {
    name: "Penetration Up",
    nameJa: "貫通力上昇",
    description: "貫通力がvalue%上昇",
    color: "#ffcc66",
    isDebuff: false,
  },
  // バフ - 特殊
  regeneration: {
    name: "Regeneration",
    nameJa: "再生",
    description: "毎ターン開始時、value HP回復",
    color: "#44ff44",
    isDebuff: false,
  },
  shieldRegen: {
    name: "Shield Regeneration",
    nameJa: "シールド再生",
    description: "毎ターン開始時、valueシールド付与",
    color: "#4488ff",
    isDebuff: false,
  },
  reflect: {
    name: "Reflect",
    nameJa: "反撃",
    description: "被ダメージのvalue%を反撃",
    color: "#ffaa00",
    isDebuff: false,
  },
  evasion: {
    name: "Evasion",
    nameJa: "回避率上昇",
    description: "回避率+value%",
    color: "#66ffcc",
    isDebuff: false,
  },
  critical: {
    name: "Critical",
    nameJa: "クリティカル率上昇",
    description: "クリティカル率+value%",
    color: "#ff6600",
    isDebuff: false,
  },
  immunity: {
    name: "Immunity",
    nameJa: "デバフ無効",
    description: "デバフを無効化",
    color: "#ffffff",
    isDebuff: false,
  },
};
