# バフ/デバフシステム 統合設計書 (Ver 5.0)

**更新日:** 2026-01-30
**ステータス:** コード実装済み

## 目次

```
1. バフ/デバフシステム概要
2. スタックシステム
3. バフ/デバフデータベース（全42種類）
4. 持続時間管理
5. 計算優先度
6. 実装関数一覧
```

---

# 1. バフ/デバフシステム概要

## 1.1 基本仕様

```typescript
/**
 * バフ/デバフインターフェース (Ver 5.0)
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

## 1.2 Ver 5.0 変更点

```
【Ver 4.0からの変更】
- 種類数: 30 → 42（デバフ19 + バフ23）
- weak/atkDown/speedDown/healingDown → atkDownMinor/atkDownMajor/defDownMinor/defDownMajor/weakness/prostoration
- speedUp/speedDown → haste/superFast/slow/stall
- guardUp/thorns/barrier/damageReduction/splash → 削除
- swordEnergyBoost/swordEnergyEfficiency → swordEnergyGain
- resonanceExtension → 削除、elementalMastery + フィールドバフ5種に変更
- summonDuration → 削除
- tenacity: デバフ効果-value% → HP30%以下で全能力+value%
- lastStand: HP30%以下で全能力+value% → 致死ダメージを1回生存

【追加】
- burn（火傷）、overCurse（重呪い）、stagger（よろめき）、freeze（凍結）
- atkDownMinor/Major、defDownMinor/Major、weakness、prostoration
- stall（失速）
- atkUpMinor/Major、defUpMinor/Major、penetrationUp、hitRateUp、criticalUp
- superFast（高速）
- fireField/iceField/electroField/darkField/lightField

【変更】
- curse: 回復-50% → 回復-20%（overCurseが-50%）
- poison: スタック×2 → 固定5ダメージ/ターン（スタック可能）
- bleed: 最大HP5% → 最大HP3%
- haste: 速度+30（固定） → 速度+15
```

---

# 2. スタックシステム

## 2.1 スタック加算ルール

```typescript
/**
 * バフ/デバフを追加または更新 (Ver 5.0)
 */
export const addOrUpdateBuffDebuff = (
  map: BuffDebuffMap,
  type: BuffDebuffType,
  stacks: number,
  duration: number,
  value: number,
  isPermanent: boolean = false,
  source?: string,
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

# 3. バフ/デバフデータベース（全 42 種類）

> **Source of truth:** `src/constants/data/battles/buffData.ts`

## 3.1 デバフ - 持続ダメージ系（5 種類）

### ID: poison（毒）

| 項目     | 値                                   |
| -------- | ------------------------------------ |
| **名称** | 毒                                   |
| **効果** | ターン終了時、固定5ダメージ（防御無視）|
| **スタック** | 可能                              |
| **色**   | #66cc00                              |

### ID: bleed（出血）

| 項目     | 値                                              |
| -------- | ----------------------------------------------- |
| **名称** | 出血                                            |
| **効果** | カード使用毎/行動毎に最大HPの**3%**ダメージ     |
| **スタック** | 可能                                         |
| **色**   | #cc0000                                         |

### ID: burn（火傷）

| 項目     | 値                                   |
| -------- | ------------------------------------ |
| **名称** | 火傷                                 |
| **効果** | ターン終了時、固定3ダメージ（防御無視）|
| **スタック** | 可能                              |
| **色**   | #ff4500                              |

### ID: curse（呪い）

| 項目     | 値                |
| -------- | ----------------- |
| **名称** | 呪い              |
| **効果** | 回復効果**-20%**  |
| **スタック** | 可能           |
| **色**   | #9900cc           |

### ID: overCurse（重呪い）

| 項目     | 値                |
| -------- | ----------------- |
| **名称** | 重呪い            |
| **効果** | 回復効果**-50%**  |
| **スタック** | 可能           |
| **色**   | #660066           |

---

## 3.2 デバフ - 状態異常系（3 種類）

### ID: stagger（よろめき）

| 項目     | 値                         |
| -------- | -------------------------- |
| **名称** | よろめき                   |
| **効果** | 行動不能                   |
| **スタック** | 可能                    |

### ID: stun（気絶）

| 項目     | 値                         |
| -------- | -------------------------- |
| **名称** | 気絶                       |
| **効果** | 行動不能（ターンスキップ） |
| **スタック** | 可能                    |

### ID: freeze（凍結）

| 項目     | 値                    |
| -------- | --------------------- |
| **名称** | 凍結                  |
| **効果** | 行動不能（氷属性）    |
| **スタック** | 不可              |

---

## 3.3 デバフ - 能力減少系（8 種類）

### ID: atkDownMinor（脱力）

| 項目 | 値 |
|------|-----|
| **効果** | 攻撃力**-15%** |
| **スタック** | 不可 |

### ID: atkDownMajor（無力）

| 項目 | 値 |
|------|-----|
| **効果** | 攻撃力**-30%** |
| **スタック** | 不可 |

### ID: defDownMinor（軟弱）

| 項目 | 値 |
|------|-----|
| **効果** | 防御力**-15%** |
| **スタック** | 不可 |

### ID: defDownMajor（無防備）

| 項目 | 値 |
|------|-----|
| **効果** | 防御力**-30%** |
| **スタック** | 不可 |

### ID: weakness（衰弱）

| 項目 | 値 |
|------|-----|
| **効果** | 全能力**-20%** |
| **スタック** | 可能 |

### ID: prostoration（虚弱）

| 項目 | 値 |
|------|-----|
| **効果** | 全能力**-50%** |
| **スタック** | 可能 |

### ID: slow（鈍足）

| 項目 | 値 |
|------|-----|
| **効果** | 速度**-10**（固定値）/スタック |
| **スタック** | 可能 |

### ID: stall（失速）

| 項目 | 値 |
|------|-----|
| **効果** | 速度**-15** |
| **スタック** | 可能 |

---

## 3.4 バフ - 能力上昇系（9 種類）

### ID: atkUpMinor（剛力）

| 項目 | 値 |
|------|-----|
| **効果** | 攻撃力**+15%** |
| **スタック** | 不可 |

### ID: atkUpMajor（豪力）

| 項目 | 値 |
|------|-----|
| **効果** | 攻撃力**+30%** |
| **スタック** | 不可 |

### ID: defUpMinor（堅牢）

| 項目 | 値 |
|------|-----|
| **効果** | 防御力**+15%** |
| **スタック** | 不可 |

### ID: defUpMajor（金剛）

| 項目 | 値 |
|------|-----|
| **効果** | 防御力**+30%** |
| **スタック** | 不可 |

### ID: penetrationUp（衝撃力アップ）

| 項目 | 値 |
|------|-----|
| **効果** | 貫通**+30%** |
| **スタック** | 可能 |

### ID: hitRateUp（会心率アップ）

| 項目 | 値 |
|------|-----|
| **効果** | 命中率**+15%** |
| **スタック** | 可能 |

### ID: criticalUp（会心力アップ）

| 項目 | 値 |
|------|-----|
| **効果** | クリティカルダメージ**+15%** |
| **スタック** | 可能 |

### ID: haste（加速）

| 項目 | 値 |
|------|-----|
| **効果** | 速度**+15** |
| **スタック** | 可能 |

### ID: superFast（高速）

| 項目 | 値 |
|------|-----|
| **効果** | 速度**+30** |
| **スタック** | 可能 |

---

## 3.5 バフ - 回復・防御系（4 種類）

### ID: regeneration（再生）

| 項目 | 値 |
|------|-----|
| **効果** | 毎ターン開始時、5 HP回復 |
| **スタック** | 可能（value × stacks） |

### ID: shieldRegen（鉄の構え）

| 項目 | 値 |
|------|-----|
| **効果** | 毎ターン開始時、Guard+5 |
| **スタック** | 可能（value × stacks） |

### ID: reflect（流転の構え）

| 項目 | 値 |
|------|-----|
| **効果** | 被ダメージの**30%**を反射 |
| **スタック** | 可能 |

### ID: immunity（免疫）

| 項目 | 値 |
|------|-----|
| **効果** | ダメージ無効化 |
| **スタック** | 可能 |

---

## 3.6 バフ - リソース管理系（3 種類）

### ID: energyRegen（完璧な呼吸）

| 項目 | 値 |
|------|-----|
| **効果** | 毎ターン開始時、+1エナジー |
| **スタック** | 可能 |

### ID: drawPower（冷静な俯瞰）

| 項目 | 値 |
|------|-----|
| **効果** | 毎ターン開始時、+1ドロー |
| **スタック** | 可能 |

### ID: costReduction（体力温存）

| 項目 | 値 |
|------|-----|
| **効果** | カードコスト-1 |
| **スタック** | 可能 |

---

## 3.7 バフ - 戦闘スタイル変化系（2 種類）

### ID: lifesteal（吸血）

| 項目 | 値 |
|------|-----|
| **効果** | 与ダメージの**30%**をHP回復 |
| **スタック** | 可能 |

### ID: doubleStrike（はやぶさの構え）

| 項目 | 値 |
|------|-----|
| **効果** | 次の攻撃カードが2回発動（威力50%） |
| **スタック** | 可能 |

---

## 3.8 バフ - 特殊効果系（4 種類）

### ID: focus（集中）

| 項目 | 値 |
|------|-----|
| **効果** | 次のカードの効果**+50%** |
| **スタック** | 可能 |

### ID: momentum（勢い）

| 項目 | 値 |
|------|-----|
| **効果** | カード使用ごとに攻撃力**+5%** |
| **スタック** | 可能（ターン終了時にスタック+1） |

### ID: tenacity（不屈）

| 項目 | 値 |
|------|-----|
| **効果** | HP30%以下で全能力**+30%** |
| **スタック** | 可能 |

### ID: lastStand（再誕）

| 項目 | 値 |
|------|-----|
| **効果** | 致死ダメージを1回生存 |
| **スタック** | 可能 |

### ID: cleanse（浄化）

| 項目 | 値 |
|------|-----|
| **効果** | ターン終了時にデバフ1つ解除 |
| **スタック** | 可能 |

---

## 3.9 バフ - キャラクター固有系（7 種類）

### 剣士用（1 種類）

| ID | 名称 | 効果 |
|----|------|------|
| `swordEnergyGain` | 剣気精錬 | 攻撃時の剣気獲得量+3% |

### 魔術士用（6 種類）

| ID | 名称 | 効果 |
|----|------|------|
| `elementalMastery` | 元素熟達 | 属性ダメージ+30% |
| `fireField` | 爆焔界 | 火属性カード効果+50% |
| `iceField` | 冷静界 | 氷属性ダメージ+50% |
| `electroField` | 雷鳴界 | 雷カード使用時+10ダメージ |
| `darkField` | 闇界 | 闇属性ダメージ+50% |
| `lightField` | 光界 | 光属性ダメージ+50% |

---

# 4. 持続時間管理

```typescript
/**
 * ターン経過による持続時間減少
 */
export const decreaseBuffDebuffDuration = (
  map: BuffDebuffMap,
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
 * 攻撃力の倍率計算 (Ver 5.0)
 * コードの実際のバフ/デバフ名を使用
 */
function calculateAttackMultiplier(buffDebuffs: BuffDebuffMap): number {
  let multiplier = 1.0;

  // 攻撃力上昇バフ
  if (buffDebuffs.has("atkUpMinor")) {
    multiplier += 0.15; // +15%
  }
  if (buffDebuffs.has("atkUpMajor")) {
    multiplier += 0.30; // +30%
  }

  // 攻撃力低下デバフ
  if (buffDebuffs.has("atkDownMinor")) {
    multiplier *= 0.85; // -15%
  }
  if (buffDebuffs.has("atkDownMajor")) {
    multiplier *= 0.70; // -30%
  }

  // 衰弱デバフ（全能力-20%）
  if (buffDebuffs.has("weakness")) {
    multiplier *= 0.80;
  }

  // 虚弱デバフ（全能力-50%）
  if (buffDebuffs.has("prostoration")) {
    multiplier *= 0.50;
  }

  // 勢いバフ（スタック累積）
  if (buffDebuffs.has("momentum")) {
    const momentum = buffDebuffs.get("momentum")!;
    multiplier += (momentum.value / 100) * momentum.stacks;
  }

  // 不屈バフ（HP30%以下で+30%）
  // ※HP判定はダメージ計算側で行う

  return multiplier;
}
```

## 5.2 速度計算での適用

```typescript
/**
 * 速度計算 (Ver 5.0)
 * haste/superFast/slow/stall を使用
 */
function calculateSpeed(baseSpeed: number, buffDebuffs: BuffDebuffMap): number {
  let speed = baseSpeed;

  // slowデバフ: 速度-10/スタック（固定値）
  if (buffDebuffs.has("slow")) {
    const slow = buffDebuffs.get("slow")!;
    speed -= slow.value * slow.stacks; // value=10
  }

  // stallデバフ: 速度-15
  if (buffDebuffs.has("stall")) {
    const stall = buffDebuffs.get("stall")!;
    speed -= stall.value * stall.stacks; // value=15
  }

  // hasteバフ: 速度+15
  if (buffDebuffs.has("haste")) {
    const haste = buffDebuffs.get("haste")!;
    speed += haste.value * haste.stacks; // value=15
  }

  // superFastバフ: 速度+30
  if (buffDebuffs.has("superFast")) {
    const superFast = buffDebuffs.get("superFast")!;
    speed += superFast.value * superFast.stacks; // value=30
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
calculateEndTurnDamage(map): number  // poison, burn, curse

// 出血ダメージ（特別処理）
calculateBleedDamage(maxHp, map): number  // 3% maxHP

// ターン開始時の回復・再生
calculateStartTurnHealing(map): { hp: number; shield: number }

// 攻撃力の倍率計算
calculateAttackMultiplier(map): number

// 速度計算
calculateSpeed(baseSpeed, map): number
```

## 6.3 状態判定

```typescript
// 行動可能判定（stun, stagger, freeze考慮）
canAct(map): boolean

// エナジー修正値計算（energyRegen考慮）
calculateEnergyModifier(map): number

// ドロー修正値計算（drawPower考慮）
calculateDrawModifier(map): number
```

---

# 参照関係

```
battle_logic.md (Ver 4.0) [マスター文書]
└── buff_debuff_system.md (Ver 5.0) [本文書]
    ├── バフデータ → src/constants/data/battles/buffData.ts
    ├── ダメージ計算 → src/domain/battles/calculators/damageCalculation.ts
    ├── 速度計算 → src/domain/battles/calculators/phaseCalculation.ts
    ├── バフ管理 → src/domain/battles/logic/buffLogic.ts
    └── バフ計算 → src/domain/battles/calculators/buffCalculation.ts
```
