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
  // バフ - 特殊
  | "regeneration" // 再生（毎ターン回復）
  | "shieldRegen" // シールド再生
  | "reflect" // 反撃
  | "evasion" // 回避率上昇
  | "critical" // クリティカル率上昇
  | "immunity"; // デバフ無効

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