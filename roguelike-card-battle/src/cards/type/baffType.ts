export type BuffDebuffType =
  // デバフ - 持続ダメージ系
  | "burn" // 火傷
  | "bleed" // 出血
  | "poison" // 毒
  | "curse" // 呪い
  // デバフ - 状態異常系
  | "slow" // スロウ（エナジー減少）
  | "freeze" // 凍結（行動不可）
  | "paralyze" // 麻痺（攻撃力減少）
  | "stun" // 気絶（行動不可）
  | "weak" // 弱体化（攻撃力減少）
  // デバフ - 能力減少系
  | "defDown" // 防御力低下
  | "atkDown" // 攻撃力低下
  | "healingDown" // 回復効果減少
  // バフ - 能力上昇系
  | "atkUp" // 攻撃力上昇
  | "defUp" // 防御力上昇
  | "magicUp" // 魔力上昇
  | "physicalUp" // 物理攻撃力上昇
  | "penetrationUp" // 貫通力上昇
  | "critical" // クリティカル率上昇
  // バフ - 回復・防御系
  | "regeneration" // 再生（毎ターン回復）
  | "shieldRegen" // シールド再生
  | "reflect" // 反撃
  | "evasion" // 回避率上昇
  | "immunity" // デバフ無効
  // バフ - リソース管理系
  | "energyRegen" // エナジー再生
  | "drawPower" // ドロー強化
  | "costReduction" // コスト軽減
  // バフ - 戦闘スタイル変化系
  | "thorns" // 棘の鎧
  | "lifesteal" // 吸血
  | "doubleStrike" // 連撃
  | "splash" // 範囲拡大
  // バフ - キャラクター固有系（剣士）
  | "swordEnergyGain" // 剣気増幅
  | "swordEnergyEfficiency" // 剣気効率
  // バフ - キャラクター固有系（魔術士）
  | "resonanceExtension" // 共鳴延長
  | "elementalMastery" // 属性熟練
  // バフ - キャラクター固有系（召喚士）
  | "summonDuration" // 召喚延長
  | "summonPower" // 召喚強化
  | "sacrificeBonus" // 犠牲強化
  // バフ - 特殊効果系
  | "barrier" // バリア
  | "damageReduction" // ダメージ軽減
  | "focus" // 集中
  | "momentum" // 勢い
  | "cleanse" // 自動浄化
  | "tenacity" // 不屈
  | "lastStand"; // 背水の陣

export interface BuffDebuff {
  type: BuffDebuffType;
  stacks: number; // スタック数
  duration: number; // 残りターン数（-1で永続）
  value: number; // 効果値（ダメージ量や倍率など）
  isPermanent: boolean; // 永続かどうか
  source?: string; // 発生源（カードIDなど）
}

// バフ/デバフマップ
export type BuffDebuffMap = Map<BuffDebuffType, BuffDebuff>;