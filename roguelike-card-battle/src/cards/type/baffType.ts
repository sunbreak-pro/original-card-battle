export type BuffDebuffType =
  // デバフ - 持続ダメージ系
  | "bleed" // 出血（特殊実装: カード使用/行動毎に最大HPの5%）
  | "poison" // 毒（毎ターン終了時、4ダメージ）
  | "burn" // 火傷（毎ターン終了時、スタック×4ダメージ）
  | "curse" // 呪い（回復効果-20%）
  | "overCurse" // 厄呪（回復効果-50%）
  // デバフ - 状態異常系
  | "stun" // 気絶（行動不可）
  // デバフ - 能力減少系
  | "atkDown" // 脱力（攻撃力-10%）
  | "defDown" // 無力（攻撃力-30%）
  | "thinSkinned" // 打弱（防御力-10%）
  | "soft" // 軟化（防御力-30%）
  | "slow" // 減速（速度-15）
  | "stall" // 失速（速度-30）

  // バフ - 能力上昇系
  | "atkUp" // 筋肥大（+30%）
  | "defUp" // 強靭化（+30%）
  | "penetrationUp" // 貫通増強（+30%）
  | "hitRateUp" // 命中増強（+15%）
  | "criticalUp" // 会心増強（+15%）
  | "haste" // 加速（+15）
  | "superFast" // 超速（+30）
  // バフ - 回復・防御系
  | "regeneration" // 再生（毎ターン5HP回復）
  | "shieldRegen" // 鉄壁の構え（毎ターン5Guard）
  | "reflect" // 反撃（被ダメージの30%反射）
  | "immunity" // デバフ無効
  // バフ - リソース管理系
  | "energyRegen" // エナジー再生（+1）
  | "drawPower" // ドロー強化（+1枚）
  | "costReduction" // コスト軽減（-1）
  // バフ - 戦闘スタイル変化系
  | "lifesteal" // 吸血（与ダメージの30%回復）
  | "doubleStrike" // 連撃（攻撃が2回発動、威力50%）
  // バフ - キャラクター固有系（剣士）
  | "swordEnergyGain" // 剣気増幅（+50%）
  // バフ - キャラクター固有系（魔術士）
  | "elementalMastery" // 属性熟練（+30%）
  | "fireField" // 火炎地帯（火属性効果+50%:火傷のダメージなど）
  | "electroField" // 雷鳴地帯（雷属性カード使用毎に+20ダメージ）
  // バフ - キャラクター固有系（召喚士）
  | "summonPower" // 召喚強化（+30%)
  | "sacrificeBonus" // 犠牲強化（+30%)
  // バフ - 特殊効果系
  | "focus" // 集中（次カード効果+50%）
  | "momentum" // 勢い（カード使用ごとに攻撃力+5%累積）
  | "cleanse" // 自動浄化（毎ターン1デバフ解除）
  | "tenacity" // 不屈（HP30%以下で全能力+30%）
  | "lastStand"; // 背水の陣（HP1で耐える、1回）

/**
 * 各buff/debuffの効果値定義
 * カードはこの値を参照し、個別にvalueを指定しない
 */
export interface BuffEffectDefinition {
  name: string;           // 日本語名
  value: number;          // 効果値（%またはフラット値）
  isDebuff: boolean;      // デバフかどうか
  isPercentage: boolean;  // %値かフラット値か
  description(): string;    // 効果説明
}

export const BUFF_EFFECTS: Record<BuffDebuffType, BuffEffectDefinition> = {
  // === デバフ - 持続ダメージ系 ===
  bleed: { name: "出血", value: 3, isDebuff: true, isPercentage: true, description: () => `カード使用毎に最大HPの${BUFF_EFFECTS.bleed.value}%ダメージ` },
  poison: { name: "毒", value: 5, isDebuff: true, isPercentage: false, description: () => `ターン終了時, ${BUFF_EFFECTS.poison.value}ダメージ` },
  burn: { name: "火傷", value: 3, isDebuff: true, isPercentage: false, description: () => `毎ターン終了時, ${BUFF_EFFECTS.burn.value}ダメージ` },
  curse: { name: "呪い", value: 20, isDebuff: true, isPercentage: true, description: () => `回復効果-${BUFF_EFFECTS.curse.value}%` },
  overCurse: { name: "厄呪", value: 50, isDebuff: true, isPercentage: true, description: () => `回復効果-${BUFF_EFFECTS.overCurse.value}%` },
  // === デバフ - 状態異常系 ===
  stun: { name: "気絶", value: 100, isDebuff: true, isPercentage: true, description: () => "行動不可" },

  // === デバフ - 能力減少系（全て30%）===
  atkDown: { name: "脱力化", value: 10, isDebuff: true, isPercentage: true, description: () => `攻撃力-${BUFF_EFFECTS.atkDown.value}%` },
  defDown: { name: "無力化", value: 30, isDebuff: true, isPercentage: true, description: () => `防御力-${BUFF_EFFECTS.defDown.value}%` },
  thinSkinned: { name: "打弱", value: 10, isDebuff: true, isPercentage: true, description: () => "防御力-30%" },
  soft: { name: "軟化", value: 30, isDebuff: true, isPercentage: true, description: () => `防御力-${BUFF_EFFECTS.soft.value}%` },
  slow: { name: "減速", value: 10, isDebuff: true, isPercentage: false, description: () => `速度-${BUFF_EFFECTS.slow.value}` },
  stall: { name: "失速", value: 15, isDebuff: true, isPercentage: true, description: () => `速度-${BUFF_EFFECTS.slow.value}` },

  // === バフ - 能力上昇系（全て30%、特殊除く）===
  atkUp: { name: "筋肥大", value: 30, isDebuff: false, isPercentage: true, description: () => `攻撃力+${BUFF_EFFECTS.atkUp.value}%` },
  defUp: { name: "強靭化", value: 30, isDebuff: false, isPercentage: true, description: () => `防御力+${BUFF_EFFECTS.defUp.value}%` },
  penetrationUp: { name: "貫通増強", value: 30, isDebuff: false, isPercentage: true, description: () => `貫通力+${BUFF_EFFECTS.penetrationUp.value}%` },
  hitRateUp: { name: "命中増強", value: 15, isDebuff: false, isPercentage: true, description: () => `命中率+${BUFF_EFFECTS.hitRateUp.value}%` },
  criticalUp: { name: "会心増強", value: 15, isDebuff: false, isPercentage: true, description: () => `会心発生時のダメージ+${BUFF_EFFECTS.criticalUp.value}%` },
  haste: { name: "加速", value: 15, isDebuff: false, isPercentage: true, description: () => `速度+${BUFF_EFFECTS.haste.value}` },
  superFast: { name: "超速", value: 30, isDebuff: false, isPercentage: true, description: () => `速度+${BUFF_EFFECTS.superFast.value}` },
  // === バフ - 回復・防御系 ===
  regeneration: { name: "再生", value: 5, isDebuff: false, isPercentage: false, description: () => `毎ターン${BUFF_EFFECTS.regeneration.value}HP回復` },
  shieldRegen: { name: "鉄壁の構え", value: 5, isDebuff: false, isPercentage: false, description: () => `毎ターンGuard+${BUFF_EFFECTS.shieldRegen.value}` },
  reflect: { name: "反撃の構え", value: 30, isDebuff: false, isPercentage: true, description: () => `被ダメージの${BUFF_EFFECTS.reflect.value}%を反射` },
  immunity: { name: "流転の構え", value: 1, isDebuff: false, isPercentage: false, description: () => "被ダメージを無効、" },
  // === バフ - リソース管理系 ===
  energyRegen: { name: "気力再生", value: 1, isDebuff: false, isPercentage: false, description: () => `エナジー+${BUFF_EFFECTS.energyRegen.value}` },
  drawPower: { name: "冷静", value: 1, isDebuff: false, isPercentage: false, description: () => "ドロー+1枚" },
  costReduction: { name: "最適化", value: 1, isDebuff: false, isPercentage: false, description: () => "コスト-1" },
  // === バフ - 戦闘スタイル変化系 ===
  lifesteal: { name: "ライフスティール", value: 30, isDebuff: false, isPercentage: true, description: () => `与ダメージの${BUFF_EFFECTS.lifesteal.value}%をHP回復` },
  doubleStrike: { name: "ダブルアクト", value: 50, isDebuff: false, isPercentage: true, description: () => `次に使用するATKカードが2回発動` },

  // === バフ - キャラクター固有系（剣士）===
  swordEnergyGain: { name: "剣気精錬", value: 3, isDebuff: false, isPercentage: true, description: () => `剣気獲得量+${BUFF_EFFECTS.swordEnergyGain.value}%` },
  // === バフ - キャラクター固有系（魔術士）===
  elementalMastery: { name: "元素熟達", value: 30, isDebuff: false, isPercentage: true, description: () => `属性ダメージ+${BUFF_EFFECTS.elementalMastery.value}%` },
  fireField: { name: "焔界", value: 50, isDebuff: false, isPercentage: true, description: () => `あらゆる火属性カードの効果が+${BUFF_EFFECTS.fireField.value}%` },
  electroField: { name: "雷鳴界", value: 10, isDebuff: false, isPercentage: true, description: () => `あらゆる雷属性カードをプレイする度に+${BUFF_EFFECTS.electroField.value}ダメージ` },
  // === バフ - キャラクター固有系（召喚士）===
  summonPower: { name: "召喚獣能力", value: 30, isDebuff: false, isPercentage: true, description: () => `召喚獣能力+${BUFF_EFFECTS.summonPower.value}%` },
  sacrificeBonus: { name: "犠牲効果", value: 30, isDebuff: false, isPercentage: true, description: () => `犠牲効果+${BUFF_EFFECTS.sacrificeBonus.value}%` },

  // === バフ - 特殊効果系 ===
  focus: { name: "集中", value: 50, isDebuff: false, isPercentage: true, description: () => `次カード効果+${BUFF_EFFECTS.focus.value}%` },
  momentum: { name: "激怒", value: 5, isDebuff: false, isPercentage: true, description: () => `カード使用毎に攻撃力+${BUFF_EFFECTS.momentum.value}%累積` },
  cleanse: { name: "浄化", value: 1, isDebuff: false, isPercentage: false, description: () => "毎ターン終了時 1デバフ解除" },
  tenacity: { name: "不屈", value: 30, isDebuff: false, isPercentage: true, description: () => `HP30%以下で全能力+${BUFF_EFFECTS.tenacity.value}%` },
  lastStand: { name: "ラストスタンド", value: 1, isDebuff: false, isPercentage: false, description: () => "致死ダメージを1回耐える" },
};

/**
 * buff/debuffの効果値を取得するヘルパー関数
 */
export function getBuffValue(type: BuffDebuffType): number {
  return BUFF_EFFECTS[type].value;
}

/**
 * カードで指定するbuff/debuff（valueは含まない）
 * 効果値はBUFF_EFFECTSから自動取得される
 */
export interface CardBuffSpec {
  name: BuffDebuffType;
  duration: number;     // 持続ターン数
  stacks: number;       // スタック数
  isPermanent?: boolean; // 永続かどうか（省略時false）
}

/**
 * ランタイムのbuff/debuff状態（valueを含む）
 */
export interface BuffDebuffState {
  name: BuffDebuffType;
  stacks: number;       // スタック数
  duration: number;     // 残りターン数（-1で永続）
  value: number;        // 効果値（BUFF_EFFECTSから取得）
  isPermanent: boolean; // 永続かどうか
  source?: string;      // 発生源（カードIDなど）
}

// バフ/デバフマップ
export type BuffDebuffMap = Map<BuffDebuffType, BuffDebuffState>;

/**
 * CardBuffSpecをBuffDebuffStateに変換
 * valueはBUFF_EFFECTSから自動取得
 */
export function createBuffState(buff: CardBuffSpec, source?: string): BuffDebuffState {
  return {
    name: buff.name,
    stacks: buff.stacks,
    duration: buff.duration,
    value: BUFF_EFFECTS[buff.name].value,
    isPermanent: buff.isPermanent ?? false,
    source,
  };
}