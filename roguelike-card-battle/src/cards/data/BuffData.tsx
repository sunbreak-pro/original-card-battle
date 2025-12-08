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
  critical: {
    name: "Critical",
    nameJa: "クリティカル率上昇",
    description: "クリティカル率+value%",
    color: "#ff6600",
    isDebuff: false,
  },
  // バフ - 回復・防御系
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
  immunity: {
    name: "Immunity",
    nameJa: "デバフ無効",
    description: "デバフを無効化",
    color: "#ffffff",
    isDebuff: false,
  },
  // バフ - リソース管理系
  energyRegen: {
    name: "Energy Regeneration",
    nameJa: "エナジー再生",
    description: "毎ターン開始時、valueエナジー回復",
    color: "#ffdd44",
    isDebuff: false,
  },
  drawPower: {
    name: "Draw Power",
    nameJa: "ドロー強化",
    description: "毎ターン開始時、value枚追加ドロー",
    color: "#88ccff",
    isDebuff: false,
  },
  costReduction: {
    name: "Cost Reduction",
    nameJa: "コスト軽減",
    description: "カードコスト-value",
    color: "#ffbb66",
    isDebuff: false,
  },
  // バフ - 戦闘スタイル変化系
  thorns: {
    name: "Thorns",
    nameJa: "棘の鎧",
    description: "物理攻撃を受けた時、攻撃者にvalueダメージ",
    color: "#998866",
    isDebuff: false,
  },
  lifesteal: {
    name: "Lifesteal",
    nameJa: "吸血",
    description: "与ダメージのvalue%をHP回復",
    color: "#cc4444",
    isDebuff: false,
  },
  doubleStrike: {
    name: "Double Strike",
    nameJa: "連撃",
    description: "攻撃カードが2回発動（威力value%）",
    color: "#ff8844",
    isDebuff: false,
  },
  splash: {
    name: "Splash",
    nameJa: "範囲拡大",
    description: "単体攻撃が隣接敵にもvalue%ダメージ",
    color: "#6699ff",
    isDebuff: false,
  },
  // バフ - キャラクター固有系（剣士）
  swordEnergyGain: {
    name: "Sword Energy Gain",
    nameJa: "剣気増幅",
    description: "攻撃時の剣気獲得量+value",
    color: "#aaddff",
    isDebuff: false,
  },
  swordEnergyEfficiency: {
    name: "Sword Energy Efficiency",
    nameJa: "剣気効率",
    description: "剣気ダメージ+value%",
    color: "#88bbff",
    isDebuff: false,
  },
  // バフ - キャラクター固有系（魔術士）
  resonanceExtension: {
    name: "Resonance Extension",
    nameJa: "共鳴延長",
    description: "属性共鳴の持続+valueターン",
    color: "#cc88ff",
    isDebuff: false,
  },
  elementalMastery: {
    name: "Elemental Mastery",
    nameJa: "属性熟練",
    description: "共鳴ボーナス+value%",
    color: "#aa66ff",
    isDebuff: false,
  },
  // バフ - キャラクター固有系（召喚士）
  summonDuration: {
    name: "Summon Duration",
    nameJa: "召喚延長",
    description: "召喚獣の持続+valueターン",
    color: "#66ddaa",
    isDebuff: false,
  },
  summonPower: {
    name: "Summon Power",
    nameJa: "召喚強化",
    description: "召喚獣の能力+value%",
    color: "#44cc88",
    isDebuff: false,
  },
  sacrificeBonus: {
    name: "Sacrifice Bonus",
    nameJa: "犠牲強化",
    description: "犠牲効果+value%",
    color: "#aa4466",
    isDebuff: false,
  },
  // バフ - 特殊効果系
  barrier: {
    name: "Barrier",
    nameJa: "バリア",
    description: "valueダメージまで無効化する障壁",
    color: "#ccddff",
    isDebuff: false,
  },
  damageReduction: {
    name: "Damage Reduction",
    nameJa: "ダメージ軽減",
    description: "全ダメージ-value%",
    color: "#8899aa",
    isDebuff: false,
  },
  focus: {
    name: "Focus",
    nameJa: "集中",
    description: "次のカードの効果+value%",
    color: "#ffee66",
    isDebuff: false,
  },
  momentum: {
    name: "Momentum",
    nameJa: "勢い",
    description: "カード使用ごとに攻撃力+value%（累積）",
    color: "#ff9944",
    isDebuff: false,
  },
  cleanse: {
    name: "Cleanse",
    nameJa: "自動浄化",
    description: "毎ターン開始時、デバフをvalue個解除",
    color: "#ddffee",
    isDebuff: false,
  },
  tenacity: {
    name: "Tenacity",
    nameJa: "不屈",
    description: "デバフの効果-value%",
    color: "#ccaa88",
    isDebuff: false,
  },
  lastStand: {
    name: "Last Stand",
    nameJa: "背水の陣",
    description: "HP30%以下で全能力+value%",
    color: "#ff4422",
    isDebuff: false,
  },
};
