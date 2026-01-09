# バフ/デバフシステム 統合設計書 (Ver 4.0)

**更新日:** 2025-12-31
**ステータス:** 設計完了

## 目次

```
1. バフ/デバフシステム概要
2. スタックシステム
3. バフ/デバフデータベース（全30種類）
4. 持続時間管理
5. 計算優先度
6. 実装関数一覧
```

---

# 1. バフ/デバフシステム概要

## 1.1 基本仕様

```typescript
/**
 * バフ/デバフインターフェース (Ver 4.0)
 * ※ 重症度システムは廃止されました
 */
interface BuffDebuff {
  type: BuffDebuffType; // バフ/デバフの種類
  stacks: number; // スタック数（重ね掛け）
  duration: number; // 残りターン数
  value: number; // 効果値（倍率やダメージ量）
  isPermanent: boolean; // 永続フラグ
  source?: string; // 発生源（カードID、装備IDなど）
}

type BuffDebuffMap = Map<BuffDebuffType, BuffDebuff>;
```

## 1.2 Ver 4.0 変更点

```
【廃止】
- 重症度システム（Light → Heavy進化）
- burn, freeze, paralyze, defDown, defUp, physicalUp, magicUp, oxidize, feeble

【新規】
- 加速（速度上昇）
- 失速（速度低下）

【変更】
- bleed: カード使用/行動毎に最大HP5%ダメージに変更
- 全てのバフ/デバフがシンプルなスタック制に統一
```

---

# 2. スタックシステム

## 2.1 スタック加算ルール

```typescript
/**
 * バフ/デバフを追加または更新 (Ver 4.0)
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
    // 既存のバフ/デバフがある場合：スタック加算、期間は最大値
    newMap.set(type, {
      ...existing,
      stacks: existing.stacks + stacks,
      duration: Math.max(existing.duration, duration),
      value: Math.max(existing.value, value),
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
```

---

# 3. バフ/デバフデータベース（全 30 種類）

## 3.1 デバフ - 持続ダメージ系（3 種類）

### ID: poison（毒）

| 項目     | 値                                              |
| -------- | ----------------------------------------------- |
| **名称** | 毒                                              |
| **対象** | HP                                              |
| **効果** | ターン終了時、スタック × 2 ダメージ（防御無視） |
| **持続** | カード依存                                      |
| **色**   | #66cc00                                         |

```typescript
{
  name: "Poison",
  nameJa: "毒",
  description: "毎ターン終了時、スタック×2のダメージ（防御無視）",
  color: "#66cc00",
  isDebuff: true,
}
```

---

### ID: bleed（出血）

| 項目     | 値                                                           |
| -------- | ------------------------------------------------------------ |
| **名称** | 出血                                                         |
| **対象** | HP                                                           |
| **効果** | プレイヤー: カード使用毎に最大 HP5%、敵: 1 行動毎に最大 HP5% |
| **持続** | カード依存                                                   |
| **色**   | #cc0000                                                      |

```typescript
{
  name: "Bleed",
  nameJa: "出血",
  description: "プレイヤー: カード使用毎に最大HPの5%ダメージ、敵: 1行動毎に最大HPの5%ダメージ（防御無視）",
  color: "#cc0000",
  isDebuff: true,
}
```

**特殊実装:** `bleedDamage.ts` で個別処理

---

### ID: curse（呪い）

| 項目     | 値                                              |
| -------- | ----------------------------------------------- |
| **名称** | 呪い                                            |
| **対象** | HP + 回復                                       |
| **効果** | 回復効果-50%、ターン終了時スタック × 2 ダメージ |
| **持続** | カード依存                                      |
| **色**   | #9900cc                                         |

```typescript
{
  name: "Curse",
  nameJa: "呪い",
  description: "回復効果-50%、毎ターン終了時スタック×2のダメージ",
  color: "#9900cc",
  isDebuff: true,
}
```

---

## 3.2 デバフ - 状態異常系（3 種類）

### ID: slow（スロウ）

| 項目     | 値                                             |
| -------- | ---------------------------------------------- |
| **名称** | スロウ                                         |
| **対象** | Energy + Speed                                 |
| **効果** | プレイヤー: エナジー-1、両者: 速度-10/スタック |
| **持続** | カード依存                                     |
| **色**   | #4488ff                                        |

```typescript
{
  name: "Slow",
  nameJa: "スロウ",
  description: "エナジー-1（プレイヤーのみ）、速度-10/スタック（両者）",
  color: "#4488ff",
  isDebuff: true,
}
```

---

### ID: stun（気絶）

| 項目     | 値                         |
| -------- | -------------------------- |
| **名称** | 気絶                       |
| **対象** | Act                        |
| **効果** | 行動不能（ターンスキップ） |
| **持続** | 1 ターン                   |
| **色**   | #ff6600                    |

```typescript
{
  name: "Stun",
  nameJa: "気絶",
  description: "行動不能（ターンスキップ）",
  color: "#ff6600",
  isDebuff: true,
}
```

---

### ID: weak（弱体化）

| 項目     | 値              |
| -------- | --------------- |
| **名称** | 弱体化          |
| **対象** | Atk             |
| **効果** | 与ダメージ -30% |
| **持続** | カード依存      |
| **色**   | #888888         |

```typescript
{
  name: "Weak",
  nameJa: "弱体化",
  description: "与ダメージ-30%",
  color: "#888888",
  isDebuff: true,
}
```

---

## 3.3 デバフ - 能力減少系（3 種類）

### ID: atkDown（攻撃力低下）

```typescript
{
  name: "Attack Down",
  nameJa: "攻撃力低下",
  description: "攻撃力がvalue%低下",
  color: "#ff4444",
  isDebuff: true,
}
```

### ID: speedDown（速度低下）

```typescript
{
  name: "Speed Down",
  nameJa: "速度低下",
  description: "速度がvalue低下",
  color: "#6688aa",
  isDebuff: true,
}
```

### ID: healingDown（回復効果減少）

```typescript
{
  name: "Healing Down",
  nameJa: "回復効果減少",
  description: "回復効果がvalue%減少",
  color: "#cc6666",
  isDebuff: true,
}
```

---

## 3.4 バフ - 能力上昇系（4 種類）

### ID: atkUp（攻撃力上昇）

```typescript
{
  name: "Attack Up",
  nameJa: "攻撃力上昇",
  description: "攻撃力がvalue%上昇",
  color: "#ff6666",
  isDebuff: false,
}
```

### ID: critical（クリティカル率上昇）

```typescript
{
  name: "Critical",
  nameJa: "クリティカル率上昇",
  description: "クリティカル率+value%（最大80%）",
  color: "#ff6600",
  isDebuff: false,
}
```

### ID: speedUp（速度上昇）【Ver 4.0 新規】

```typescript
{
  name: "Speed Up",
  nameJa: "速度上昇",
  description: "速度 += value × stacks",
  color: "#66ccff",
  isDebuff: false,
}
```

### ID: haste（加速）【Ver 4.0 新規】

```typescript
{
  name: "Haste",
  nameJa: "加速",
  description: "速度 +30（固定値）",
  color: "#00ccff",
  isDebuff: false,
}
```

---

## 3.5 バフ - 回復・防御系（6 種類）

### ID: regeneration（再生）

```typescript
{
  name: "Regeneration",
  nameJa: "再生",
  description: "毎ターン開始時、value × stacks HP回復",
  color: "#44ff44",
  isDebuff: false,
}
```

### ID: guardUp（防御強化）

```typescript
{
  name: "Guard Up",
  nameJa: "防御強化",
  description: "Guard獲得量+value%",
  color: "#6666ff",
  isDebuff: false,
}
```

### ID: shieldRegen（シールド再生）

```typescript
{
  name: "Shield Regeneration",
  nameJa: "シールド再生",
  description: "毎ターン開始時、value × stacks Guard付与",
  color: "#4488ff",
  isDebuff: false,
}
```

### ID: reflect（反撃）

```typescript
{
  name: "Reflect",
  nameJa: "反撃",
  description: "被ダメージのvalue%を反撃",
  color: "#ffaa00",
  isDebuff: false,
}
```

### ID: evasion（回避率上昇）

```typescript
{
  name: "Evasion",
  nameJa: "回避率上昇",
  description: "回避率+value%",
  color: "#66ffcc",
  isDebuff: false,
}
```

### ID: immunity（デバフ無効）

```typescript
{
  name: "Immunity",
  nameJa: "デバフ無効",
  description: "デバフを無効化",
  color: "#ffffff",
  isDebuff: false,
}
```

---

## 3.6 バフ - リソース管理系（3 種類）

### ID: energyRegen（エナジー再生）

```typescript
{
  name: "Energy Regeneration",
  nameJa: "エナジー再生",
  description: "毎ターン開始時、valueエナジー回復",
  color: "#ffdd44",
  isDebuff: false,
}
```

### ID: drawPower（ドロー強化）

```typescript
{
  name: "Draw Power",
  nameJa: "ドロー強化",
  description: "毎ターン開始時、value枚追加ドロー",
  color: "#44ddff",
  isDebuff: false,
}
```

### ID: costReduction（コスト軽減）

```typescript
{
  name: "Cost Reduction",
  nameJa: "コスト軽減",
  description: "カードコスト-value",
  color: "#ffaa44",
  isDebuff: false,
}
```

---

## 3.7 バフ - 戦闘スタイル変化系（4 種類）

### ID: thorns（棘の鎧）

```typescript
{
  name: "Thorns",
  nameJa: "棘の鎧",
  description: "物理攻撃を受けた時、攻撃者にvalueダメージ",
  color: "#996633",
  isDebuff: false,
}
```

### ID: lifesteal（吸血）

```typescript
{
  name: "Lifesteal",
  nameJa: "吸血",
  description: "与ダメージのvalue%をHP回復",
  color: "#cc3366",
  isDebuff: false,
}
```

### ID: doubleStrike（連撃）

```typescript
{
  name: "Double Strike",
  nameJa: "連撃",
  description: "攻撃カードが2回発動（威力value%）",
  color: "#ff9933",
  isDebuff: false,
}
```

### ID: splash（範囲拡大）

```typescript
{
  name: "Splash Damage",
  nameJa: "範囲拡大",
  description: "単体攻撃が隣接敵にもvalue%ダメージ",
  color: "#6699ff",
  isDebuff: false,
}
```

---

## 3.8 バフ - 特殊効果系（7 種類）

### ID: barrier（バリア）

```typescript
{
  name: "Barrier",
  nameJa: "バリア",
  description: "valueダメージまで無効化する障壁",
  color: "#44ccff",
  isDebuff: false,
}
```

### ID: damageReduction（ダメージ軽減）

```typescript
{
  name: "Damage Reduction",
  nameJa: "ダメージ軽減",
  description: "全ダメージ-value%",
  color: "#6666ff",
  isDebuff: false,
}
```

### ID: focus（集中）

```typescript
{
  name: "Focus",
  nameJa: "集中",
  description: "次のカードの効果+value%",
  color: "#ffcc66",
  isDebuff: false,
}
```

### ID: momentum（勢い）

```typescript
{
  name: "Momentum",
  nameJa: "勢い",
  description: "ターン終了ごとにスタック+1、攻撃力+value%/スタック",
  color: "#ff8844",
  isDebuff: false,
}
```

### ID: cleanse（自動浄化）

```typescript
{
  name: "Auto Cleanse",
  nameJa: "自動浄化",
  description: "毎ターン開始時、デバフをvalue × stacks個解除",
  color: "#ffffff",
  isDebuff: false,
}
```

### ID: tenacity（不屈）

```typescript
{
  name: "Tenacity",
  nameJa: "不屈",
  description: "デバフの効果-value%",
  color: "#ffaa66",
  isDebuff: false,
}
```

### ID: lastStand（背水の陣）

```typescript
{
  name: "Last Stand",
  nameJa: "背水の陣",
  description: "HP30%以下で全能力+value%",
  color: "#cc4444",
  isDebuff: false,
}
```

---

## 3.9 バフ - キャラクター固有系（7 種類）

### 剣士用（2 種類）

```typescript
{
  name: "Sword Energy Boost",
  nameJa: "剣気増幅",
  description: "攻撃時の剣気獲得量+value",
  color: "#ff4444",
  isDebuff: false,
}

{
  name: "Sword Energy Efficiency",
  nameJa: "剣気効率",
  description: "剣気ダメージ+value%",
  color: "#ff6666",
  isDebuff: false,
}
```

### 魔術士用（2 種類）

```typescript
{
  name: "Resonance Extension",
  nameJa: "共鳴延長",
  description: "属性共鳴の持続+valueターン",
  color: "#9966ff",
  isDebuff: false,
}

{
  name: "Elemental Mastery",
  nameJa: "属性熟練",
  description: "共鳴ボーナス+value%",
  color: "#cc66ff",
  isDebuff: false,
}
```

### 召喚士用（3 種類）

```typescript
{
  name: "Summon Duration",
  nameJa: "召喚延長",
  description: "召喚獣の持続+valueターン",
  color: "#66cc99",
  isDebuff: false,
}

{
  name: "Summon Power",
  nameJa: "召喚強化",
  description: "召喚獣の能力+value%",
  color: "#66ff99",
  isDebuff: false,
}

{
  name: "Sacrifice Bonus",
  nameJa: "犠牲強化",
  description: "犠牲効果+value%",
  color: "#993366",
  isDebuff: false,
}
```

---

# 4. 持続時間管理

```typescript
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
```

---

# 5. 計算優先度

## 5.1 ダメージ計算での適用

```typescript
/**
 * 攻撃力の倍率計算 (Ver 4.0)
 */
function calculateAttackMultiplier(buffDebuffs: BuffDebuffMap): number {
  let multiplier = 1.0;

  // 弱体化デバフ
  if (buffDebuffs.has("weak")) {
    multiplier *= 0.7; // -30%
  }

  // 攻撃力低下デバフ
  if (buffDebuffs.has("atkDown")) {
    const atkDown = buffDebuffs.get("atkDown")!;
    multiplier *= 1 - atkDown.value / 100;
  }

  // 攻撃力上昇バフ
  if (buffDebuffs.has("atkUp")) {
    const atkUp = buffDebuffs.get("atkUp")!;
    multiplier += atkUp.value / 100;
  }

  // 勢いバフ（スタック累積）
  if (buffDebuffs.has("momentum")) {
    const momentum = buffDebuffs.get("momentum")!;
    multiplier += (momentum.value / 100) * momentum.stacks;
  }

  return multiplier;
}
```

## 5.2 速度計算での適用

```typescript
/**
 * 速度計算 (Ver 4.0)
 */
function calculateSpeed(baseSpeed: number, buffDebuffs: BuffDebuffMap): number {
  let speed = baseSpeed;

  // slowデバフ: 速度-10/スタック
  if (buffDebuffs.has("slow")) {
    const slow = buffDebuffs.get("slow")!;
    speed -= 10 * slow.stacks;
  }

  // speedDownデバフ: 速度-value
  if (buffDebuffs.has("speedDown")) {
    const speedDown = buffDebuffs.get("speedDown")!;
    speed -= speedDown.value;
  }

  // speedUpバフ: 速度 += value × stacks
  if (buffDebuffs.has("speedUp")) {
    const speedUp = buffDebuffs.get("speedUp")!;
    speed += speedUp.value * speedUp.stacks;
  }

  // hasteバフ: 速度+30（固定）
  if (buffDebuffs.has("haste")) {
    speed += 30;
  }

  return Math.max(0, speed);
}
```

---

# 6. 実装関数一覧

## 6.1 バフ/デバフ管理

```typescript
// バフ/デバフの追加・更新
addOrUpdateBuffDebuff(map, type, stacks, duration, value, isPermanent, source);

// バフ/デバフの削除
removeBuffDebuff(map, type);

// 全デバフの削除
removeAllDebuffs(map);

// ランダムにデバフを削除
removeDebuffs(map, count);

// 持続時間の減少
decreaseBuffDebuffDuration(map);
```

## 6.2 ダメージ・回復計算

```typescript
// ターン終了時の持続ダメージ
calculateEndTurnDamage(map): number  // poison, curse

// 出血ダメージ（特別処理）
calculateBleedDamage(maxHp, map): number

// ターン開始時の回復・再生
calculateStartTurnHealing(map): { hp: number; shield: number }

// 攻撃力の倍率計算
calculateAttackMultiplier(map): number

// 速度計算
calculateSpeed(baseSpeed, map): number
```

## 6.3 状態判定

```typescript
// 行動可能判定（stun, freeze考慮）
canAct(map): boolean

// エナジー修正値計算（slow, energyRegen考慮）
calculateEnergyModifier(map): number

// ドロー修正値計算（drawPower考慮）
calculateDrawModifier(map): number
```

---

# 参照関係

```
battle_logic.md (Ver 4.0) [マスター文書]
└── buff_debuff_system.md (Ver 4.0) [本文書]
    ├── ダメージ計算 → damageCalculation.ts
    ├── 速度計算 → speedCalculation.ts
    ├── 出血ダメージ → bleedDamage.ts
    └── バフ管理 → buff.ts
```
