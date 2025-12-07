# バフ/デバフシステム 統合設計書 + データベース (Ver 2.0)

## 目次

```
1. バフ/デバフシステム概要
2. 重症度システム
3. バフ/デバフデータベース（全44種類）
4. スタックシステム
5. 持続時間管理
6. 計算優先度
7. 実装関数一覧
```

---

# 1. バフ/デバフシステム概要

## 1.1 基本仕様

```typescript
/**
 * バフ/デバフの重症度
 */
enum BuffDebuffSeverity {
  LIGHT = "Light", // 軽度
  HEAVY = "Heavy", // 重度
  NONE = "None", // 重症度なし（一部のバフ用）
}

/**
 * バフ/デバフインターフェース
 */
interface BuffDebuff {
  type: BuffDebuffType; // バフ/デバフの種類
  severity: BuffDebuffSeverity; // 重症度（Light/Heavy/None）
  stacks: number; // スタック数（重ね掛け）
  duration: number; // 残りターン数
  value: number; // 効果値（倍率やダメージ量）
  isPermanent: boolean; // 永続フラグ
  source?: string; // 発生源（カードID、装備IDなど）
}

type BuffDebuffMap = Map<BuffDebuffType, BuffDebuff>;
```

## 1.2 重症度の適用範囲

```
【重症度あり（Light → Heavy進化）】
- 持続ダメージ系デバフ
- 状態異常系デバフ
- 一部の能力減少系デバフ

【重症度なし（Severity = None）】
- リソース管理系バフ
- 戦闘スタイル変化系バフ
- キャラクター固有バフ
- 一部の能力上昇系バフ
```

---

# 2. 重症度システム

## 2.1 進化ルール

```typescript
/**
 * バフ/デバフ付与時の重症度進化ロジック
 */
function applyBuffDebuff(
  map: BuffDebuffMap,
  type: BuffDebuffType,
  duration: number,
  stack: number = 1,
  value: number = 0
): BuffDebuffMap {
  const newMap = new Map(map);
  const existing = newMap.get(type);

  if (existing) {
    // 既存のバフ/デバフがある場合
    if (existing.severity === BuffDebuffSeverity.LIGHT) {
      // 軽度 → 重度に進化
      newMap.set(type, {
        ...existing,
        severity: BuffDebuffSeverity.HEAVY,
        duration: Math.max(existing.duration, duration),
        stack: existing.stack + stack,
        value: Math.max(existing.value, value),
      });
    } else if (existing.severity === BuffDebuffSeverity.HEAVY) {
      // 重度の場合は持続ターンとスタックのみ延長
      newMap.set(type, {
        ...existing,
        duration: Math.max(existing.duration, duration),
        stack: existing.stack + stack,
        value: Math.max(existing.value, value),
      });
    } else {
      // 重症度なし（NONE）の場合は通常のスタック加算
      newMap.set(type, {
        ...existing,
        stacks: existing.stacks + stack,
        duration: Math.max(existing.duration, duration),
        value: Math.max(existing.value, value),
      });
    }
  } else {
    // 新規付与（重症度ありは軽度から開始）
    const hasSeverity = hasBuffDebuffSeverity(type);
    newMap.set(type, {
      type,
      severity: hasSeverity
        ? BuffDebuffSeverity.LIGHT
        : BuffDebuffSeverity.NONE,
      stack,
      duration,
      value,
      isPermanent: false,
    });
  }

  return newMap;
}

/**
 * 指定のバフ/デバフタイプが重症度システムを持つか判定
 */
function hasBuffDebuffSeverity(type: BuffDebuffType): boolean {
  const severityTypes = [
    "burn",
    "bleed",
    "poison",
    "curse", // 持続ダメージ系
    "slow",
    "freeze",
    "paralyze",
    "stun",
    "weak", // 状態異常系
    "oxidize",
    "feeble", // 特殊デバフ
  ];
  return severityTypes.includes(type);
}
```

## 2.2 重症度進化の例

```
【例1: 火傷（burn）】
ターン1: 火傷付与 → 火傷(Light, 2ターン, スタック1)
ターン2: 再度火傷付与 → 火傷(Heavy, 3ターン, スタック2)に進化
ターン3: さらに火傷付与 → 火傷(Heavy, 5ターン, スタック3)のまま

【例2: 麻痺（paralyze）】
ターン1: 麻痺付与 → 麻痺(Light, 2ターン) 攻撃力-30%
ターン2: 再度麻痺付与 → 麻痺(Heavy, 3ターン)に進化 攻撃力-50%

【例3: 攻撃力上昇（atkUp）- 重症度なし】
ターン1: 攻撃力+30%付与 → atkUp(None, 3ターン, value=30)
ターン2: 攻撃力+20%付与 → atkUp(None, 4ターン, value=30) ※大きい方を維持
```

---

# 3. バフ/デバフデータベース（全 44 種類）

## 3.1 デバフ - 持続ダメージ系（4 種類）

### ID: burn（火傷）

| 項目       | 軽度(Light)                                       | 重度(Heavy)                                                |
| ---------- | ------------------------------------------------- | ---------------------------------------------------------- |
| **名称**   | 火傷                                              | 大火傷                                                     |
| **対象**   | HP                                                | HP                                                         |
| **効果**   | ターン終了時、スタック ×3 ダメージ（Shield 無視） | ターン開始時 + 終了時、スタック ×3 ダメージ（Shield 無視） |
| **持続**   | 2 ターン                                          | 3 ターン                                                   |
| **減衰**   | Duration -1（End Turn）                           | Duration -1（End Turn）                                    |
| **色**     | #ff6600                                           | #ff3300                                                    |
| **発生源** | 火属性魔法カード                                  | 強力な火属性魔法                                           |

```typescript
{
  name: "Burn",
  nameJa: "火傷",
  nameJaHeavy: "大火傷",
  description: {
    light: "毎ターン終了時、スタック×3のダメージ（シールド無視）",
    heavy: "毎ターン開始時と終了時、スタック×3のダメージ（シールド無視）"
  },
  color: "#ff6600",
  colorHeavy: "#ff3300",
  isDebuff: true,
  hasSeverity: true,
}
```

---

### ID: bleed（出血）

| 項目       | 軽度(Light)                        | 重度(Heavy)                              |
| ---------- | ---------------------------------- | ---------------------------------------- |
| **名称**   | 出血                               | 大出血                                   |
| **対象**   | HP                                 | HP                                       |
| **効果**   | カード使用時、スタック ×2 ダメージ | 全行動時、スタック ×3 ダメージ（1.5 倍） |
| **持続**   | 2 ターン                           | 3 ターン                                 |
| **減衰**   | Duration -1（3 Turn）              | Duration -1（3 Turn）                    |
| **色**     | #cc0000                            | #990000                                  |
| **発生源** | 斬撃系物理カード                   | 強力な斬撃攻撃                           |

```typescript
{
  name: "Bleed",
  nameJa: "出血",
  nameJaHeavy: "大出血",
  description: {
    light: "カード使用時、スタック×2のダメージ",
    heavy: "全行動時、スタック×3のダメージ（1.5倍）"
  },
  color: "#cc0000",
  colorHeavy: "#990000",
  isDebuff: true,
  hasSeverity: true,
}
```

---

### ID: poison（毒）

| 項目       | 軽度(Light)                                    | 重度(Heavy)                                    |
| ---------- | ---------------------------------------------- | ---------------------------------------------- |
| **名称**   | 毒                                             | 猛毒                                           |
| **対象**   | HP                                             | HP                                             |
| **効果**   | ターン終了時、スタック ×2 ダメージ（防御無視） | ターン終了時、スタック ×4 ダメージ（防御無視） |
| **持続**   | 2 ターン                                       | 3 ターン                                       |
| **減衰**   | Stack -1（End Turn）                           | Stack -1（End Turn）                           |
| **色**     | #66cc00                                        | #339900                                        |
| **発生源** | 毒属性カード、罠                               | 強力な毒攻撃                                   |

```typescript
{
  name: "Poison",
  nameJa: "毒",
  nameJaHeavy: "猛毒",
  description: {
    light: "毎ターン終了時、スタック×2のダメージ（防御無視）",
    heavy: "毎ターン終了時、スタック×4のダメージ（防御無視）"
  },
  color: "#66cc00",
  colorHeavy: "#339900",
  isDebuff: true,
  hasSeverity: true,
}
```

---

### ID: curse（呪い）

| 項目       | 軽度(Light)                                    | 重度(Heavy)                                                   |
| ---------- | ---------------------------------------------- | ------------------------------------------------------------- |
| **名称**   | 呪い                                           | 重呪                                                          |
| **対象**   | HP + 回復                                      | HP + 回復                                                     |
| **効果**   | 回復効果-50%、ターン終了時スタック ×2 ダメージ | 回復効果-75%、ターン終了時スタック ×3 ダメージ、HP 最大値-10% |
| **持続**   | 2 ターン                                       | 3 ターン                                                      |
| **減衰**   | Duration -1（2 Turn）                          | Duration -1（3 Turn）                                         |
| **色**     | #9900cc                                        | #660099                                                       |
| **発生源** | 闇属性魔法                                     | 強力な闇属性魔法                                              |

```typescript
{
  name: "Curse",
  nameJa: "呪い",
  nameJaHeavy: "重呪",
  description: {
    light: "回復効果-50%、毎ターン終了時スタック×2のダメージ",
    heavy: "回復効果-75%、毎ターン終了時スタック×3のダメージ、HP最大値-10%"
  },
  color: "#9900cc",
  colorHeavy: "#660099",
  isDebuff: true,
  hasSeverity: true,
}
```

---

## 3.2 デバフ - 状態異常系（5 種類）

### ID: slow（スロウ）

| 項目       | 軽度(Light)           | 重度(Heavy)           |
| ---------- | --------------------- | --------------------- |
| **名称**   | スロウ                | 重スロウ              |
| **対象**   | Energy                | Energy                |
| **効果**   | エナジー-1            | エナジー-2            |
| **持続**   | 2 ターン              | 3 ターン              |
| **減衰**   | Duration -1（1 Turn） | Duration -1（1 Turn） |
| **色**     | #4488ff               | #2266dd               |
| **発生源** | 氷属性魔法            | 強力な氷属性魔法      |

```typescript
{
  name: "Slow",
  nameJa: "スロウ",
  nameJaHeavy: "重スロウ",
  description: {
    light: "エナジー-1",
    heavy: "エナジー-2"
  },
  color: "#4488ff",
  colorHeavy: "#2266dd",
  isDebuff: true,
  hasSeverity: true,
}
```

---

### ID: freeze（凍結）

| 項目       | 軽度(Light)           | 重度(Heavy)              |
| ---------- | --------------------- | ------------------------ |
| **名称**   | 凍結                  | 氷獄                     |
| **対象**   | Hand                  | Hand                     |
| **効果**   | 手札 1 枚禁止         | 手札全て禁止（行動不可） |
| **持続**   | 1 ターン              | 2 ターン                 |
| **減衰**   | Duration -1（1 Turn） | Duration -1（1 Turn）    |
| **色**     | #00ccff               | #0099cc                  |
| **発生源** | 氷属性魔法            | 強力な氷属性魔法         |

```typescript
{
  name: "Freeze",
  nameJa: "凍結",
  nameJaHeavy: "氷獄",
  description: {
    light: "手札1枚禁止",
    heavy: "手札全て禁止（行動不可）"
  },
  color: "#00ccff",
  colorHeavy: "#0099cc",
  isDebuff: true,
  hasSeverity: true,
}
```

---

### ID: paralyze（麻痺）

| 項目       | 軽度(Light)           | 重度(Heavy)           |
| ---------- | --------------------- | --------------------- |
| **名称**   | 麻痺                  | 完全麻痺              |
| **対象**   | Atk                   | Atk                   |
| **効果**   | 攻撃力-30%            | 攻撃力-50%            |
| **持続**   | 2 ターン              | 3 ターン              |
| **減衰**   | Duration -1（2 Turn） | Duration -1（2 Turn） |
| **色**     | #ffcc00               | #ff9900               |
| **発生源** | 雷属性魔法            | 強力な雷属性魔法      |

```typescript
{
  name: "Paralyze",
  nameJa: "麻痺",
  nameJaHeavy: "完全麻痺",
  description: {
    light: "攻撃力-30%",
    heavy: "攻撃力-50%"
  },
  color: "#ffcc00",
  colorHeavy: "#ff9900",
  isDebuff: true,
  hasSeverity: true,
}
```

---

### ID: stun（気絶）

| 項目       | 軽度(Light)              | 重度(Heavy)           |
| ---------- | ------------------------ | --------------------- |
| **名称**   | よろめき                 | 気絶                  |
| **対象**   | Act                      | Act                   |
| **効果**   | エナジー-1（行動は可能） | 行動不能（Skip Turn） |
| **持続**   | 1 ターン                 | 1 ターン              |
| **減衰**   | Duration -1（1 Turn）    | Duration -1（1 Turn） |
| **色**     | #ffaa00                  | #ff6600               |
| **発生源** | 物理打撃系カード         | 強力な打撃攻撃        |

```typescript
{
  name: "Stun",
  nameJa: "よろめき",
  nameJaHeavy: "気絶",
  description: {
    light: "エナジー-1（行動は可能）",
    heavy: "行動不能（Skip Turn）"
  },
  color: "#ffaa00",
  colorHeavy: "#ff6600",
  isDebuff: true,
  hasSeverity: true,
}
```

---

### ID: weak（弱体化）

| 項目       | 軽度(Light)           | 重度(Heavy)                   |
| ---------- | --------------------- | ----------------------------- |
| **名称**   | 弱体化                | 脆弱                          |
| **対象**   | Def                   | Def                           |
| **効果**   | 被ダメージ ×1.25      | 被ダメージ ×1.50 + Guard 不可 |
| **持続**   | 2 ターン              | 3 ターン                      |
| **減衰**   | Duration -1（2 Turn） | Duration -1（2 Turn）         |
| **色**     | #888888               | #555555                       |
| **発生源** | 弱体化カード          | 強力な弱体化攻撃              |

```typescript
{
  name: "Weak",
  nameJa: "弱体化",
  nameJaHeavy: "脆弱",
  description: {
    light: "被ダメージ×1.25",
    heavy: "被ダメージ×1.50 + Guard不可"
  },
  color: "#888888",
  colorHeavy: "#555555",
  isDebuff: true,
  hasSeverity: true,
}
```

---

## 3.3 デバフ - 能力減少系（3 種類）

### ID: defDown（防御力低下）

| 項目       | 値                      |
| ---------- | ----------------------- |
| **名称**   | 防御力低下              |
| **重症度** | なし（Severity = None） |
| **効果**   | 防御力が value%低下     |
| **持続**   | カード依存              |
| **色**     | #ff8800                 |
| **発生源** | 防御力低下カード        |

```typescript
{
  name: "Defense Down",
  nameJa: "防御力低下",
  description: "防御力がvalue%低下",
  color: "#ff8800",
  isDebuff: true,
  hasSeverity: false,
}
```

---

### ID: atkDown（攻撃力低下）

| 項目       | 値                      |
| ---------- | ----------------------- |
| **名称**   | 攻撃力低下              |
| **重症度** | なし（Severity = None） |
| **効果**   | 攻撃力が value%低下     |
| **持続**   | カード依存              |
| **色**     | #ff4444                 |
| **発生源** | 攻撃力低下カード        |

```typescript
{
  name: "Attack Down",
  nameJa: "攻撃力低下",
  description: "攻撃力がvalue%低下",
  color: "#ff4444",
  isDebuff: true,
  hasSeverity: false,
}
```

---

### ID: healingDown（回復効果減少）

| 項目       | 値                      |
| ---------- | ----------------------- |
| **名称**   | 回復効果減少            |
| **重症度** | なし（Severity = None） |
| **効果**   | 回復効果が value%減少   |
| **持続**   | カード依存              |
| **色**     | #cc6666                 |
| **発生源** | 特殊攻撃カード          |

```typescript
{
  name: "Healing Down",
  nameJa: "回復効果減少",
  description: "回復効果がvalue%減少",
  color: "#cc6666",
  isDebuff: true,
  hasSeverity: false,
}
```

---

## 3.4 デバフ - 特殊系（2 種類）

### ID: oxidize（酸化）

| 項目       | 軽度(Light)             | 重度(Heavy)                              |
| ---------- | ----------------------- | ---------------------------------------- |
| **名称**   | 酸化                    | 溶解                                     |
| **対象**   | AP                      | AP                                       |
| **効果**   | ターン終了時、AP10%削り | ターン終了時、AP15%削り + Guard 効果半減 |
| **持続**   | 2 ターン                | 3 ターン                                 |
| **減衰**   | Duration -1（3 Turn）   | Duration -1（3 Turn）                    |
| **色**     | #996633                 | #663300                                  |
| **発生源** | 酸属性攻撃              | 強力な酸攻撃                             |

```typescript
{
  name: "Oxidize",
  nameJa: "酸化",
  nameJaHeavy: "溶解",
  description: {
    light: "ターン終了時、AP10%削り",
    heavy: "ターン終了時、AP15%削り + Guard効果半減"
  },
  color: "#996633",
  colorHeavy: "#663300",
  isDebuff: true,
  hasSeverity: true,
}
```

---

### ID: feeble（脱力）

| 項目       | 軽度(Light)           | 重度(Heavy)                      |
| ---------- | --------------------- | -------------------------------- |
| **名称**   | 脱力                  | 無力                             |
| **対象**   | Atk                   | Atk                              |
| **効果**   | 与ダメージ ×0.75      | 与ダメージ ×0.50 + Critical 不可 |
| **持続**   | 2 ターン              | 3 ターン                         |
| **減衰**   | Duration -1（2 Turn） | Duration -1（2 Turn）            |
| **色**     | #aa8866               | #775533                          |
| **発生源** | 脱力カード            | 強力な脱力攻撃                   |

```typescript
{
  name: "Feeble",
  nameJa: "脱力",
  nameJaHeavy: "無力",
  description: {
    light: "与ダメージ×0.75",
    heavy: "与ダメージ×0.50 + Critical不可"
  },
  color: "#aa8866",
  colorHeavy: "#775533",
  isDebuff: true,
  hasSeverity: true,
}
```

---

## 3.5 バフ - 能力上昇系（6 種類）

すべて **重症度なし（Severity = None）**

### ID: atkUp（攻撃力上昇）

```typescript
{
  name: "Attack Up",
  nameJa: "攻撃力上昇",
  description: "攻撃力がvalue%上昇",
  color: "#ff6666",
  isDebuff: false,
  hasSeverity: false,
}
```

### ID: defUp（防御力上昇）

```typescript
{
  name: "Defense Up",
  nameJa: "防御力上昇",
  description: "防御力がvalue%上昇",
  color: "#6666ff",
  isDebuff: false,
  hasSeverity: false,
}
```

### ID: magicUp（魔力上昇）

```typescript
{
  name: "Magic Up",
  nameJa: "魔力上昇",
  description: "魔力がvalue%上昇",
  color: "#cc66ff",
  isDebuff: false,
  hasSeverity: false,
}
```

### ID: physicalUp（物理攻撃力上昇）

```typescript
{
  name: "Physical Up",
  nameJa: "物理攻撃力上昇",
  description: "物理攻撃力がvalue%上昇",
  color: "#ff9966",
  isDebuff: false,
  hasSeverity: false,
}
```

### ID: penetrationUp（貫通力上昇）

```typescript
{
  name: "Penetration Up",
  nameJa: "貫通力上昇",
  description: "貫通力がvalue%上昇",
  color: "#ffcc66",
  isDebuff: false,
  hasSeverity: false,
}
```

### ID: critical（クリティカル率上昇）

```typescript
{
  name: "Critical",
  nameJa: "クリティカル率上昇",
  description: "クリティカル率+value%",
  color: "#ff6600",
  isDebuff: false,
  hasSeverity: false,
}
```

---

## 3.6 バフ - 回復・防御系（5 種類）

すべて **重症度なし（Severity = None）**

### ID: regeneration（再生）

```typescript
{
  name: "Regeneration",
  nameJa: "再生",
  description: "毎ターン開始時、value HP回復",
  color: "#44ff44",
  isDebuff: false,
  hasSeverity: false,
}
```

### ID: shieldRegen（シールド再生）

```typescript
{
  name: "Shield Regeneration",
  nameJa: "シールド再生",
  description: "毎ターン開始時、valueシールド付与",
  color: "#4488ff",
  isDebuff: false,
  hasSeverity: false,
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
  hasSeverity: false,
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
  hasSeverity: false,
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
  hasSeverity: false,
}
```

---

## 3.7 バフ - リソース管理系（3 種類）

すべて **重症度なし（Severity = None）**

### ID: energyRegen（エナジー再生）

```typescript
{
  name: "Energy Regeneration",
  nameJa: "エナジー再生",
  description: "毎ターン開始時、valueエナジー回復",
  color: "#ffdd44",
  isDebuff: false,
  hasSeverity: false,
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
  hasSeverity: false,
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
  hasSeverity: false,
}
```

---

## 3.8 バフ - 戦闘スタイル変化系（4 種類）

すべて **重症度なし（Severity = None）**

### ID: thorns（棘の鎧）

```typescript
{
  name: "Thorns",
  nameJa: "棘の鎧",
  description: "物理攻撃を受けた時、攻撃者にvalueダメージ",
  color: "#996633",
  isDebuff: false,
  hasSeverity: false,
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
  hasSeverity: false,
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
  hasSeverity: false,
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
  hasSeverity: false,
}
```

---

## 3.9 バフ - キャラクター固有系（7 種類）

すべて **重症度なし（Severity = None）**

### 剣士用（2 種類）

```typescript
{
  name: "Sword Energy Boost",
  nameJa: "剣気増幅",
  description: "攻撃時の剣気獲得量+value",
  color: "#ff4444",
  isDebuff: false,
  hasSeverity: false,
}

{
  name: "Sword Energy Efficiency",
  nameJa: "剣気効率",
  description: "剣気ダメージ+value%",
  color: "#ff6666",
  isDebuff: false,
  hasSeverity: false,
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
  hasSeverity: false,
}

{
  name: "Elemental Mastery",
  nameJa: "属性熟練",
  description: "共鳴ボーナス+value%",
  color: "#cc66ff",
  isDebuff: false,
  hasSeverity: false,
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
  hasSeverity: false,
}

{
  name: "Summon Power",
  nameJa: "召喚強化",
  description: "召喚獣の能力+value%",
  color: "#66ff99",
  isDebuff: false,
  hasSeverity: false,
}

{
  name: "Sacrifice Bonus",
  nameJa: "犠牲強化",
  description: "犠牲効果+value%",
  color: "#993366",
  isDebuff: false,
  hasSeverity: false,
}
```

---

## 3.10 バフ - 特殊効果系（7 種類）

すべて **重症度なし（Severity = None）**

```typescript
{
  name: "Barrier",
  nameJa: "バリア",
  description: "valueダメージまで無効化する障壁",
  color: "#44ccff",
  isDebuff: false,
  hasSeverity: false,
}

{
  name: "Damage Reduction",
  nameJa: "ダメージ軽減",
  description: "全ダメージ-value%",
  color: "#6666ff",
  isDebuff: false,
  hasSeverity: false,
}

{
  name: "Focus",
  nameJa: "集中",
  description: "次のカードの効果+value%",
  color: "#ffcc66",
  isDebuff: false,
  hasSeverity: false,
}

{
  name: "Momentum",
  nameJa: "勢い",
  description: "カード使用ごとに攻撃力+value%（累積）",
  color: "#ff8844",
  isDebuff: false,
  hasSeverity: false,
}

{
  name: "Auto Cleanse",
  nameJa: "自動浄化",
  description: "毎ターン開始時、デバフをvalue個解除",
  color: "#ffffff",
  isDebuff: false,
  hasSeverity: false,
}

{
  name: "Tenacity",
  nameJa: "不屈",
  description: "デバフの効果-value%",
  color: "#ffaa66",
  isDebuff: false,
  hasSeverity: false,
}

{
  name: "Last Stand",
  nameJa: "背水の陣",
  description: "HP30%以下で全能力+value%",
  color: "#cc4444",
  isDebuff: false,
  hasSeverity: false,
}
```

---

# 4. スタックシステム

## 4.1 スタック加算ルール（重症度統合版）

```typescript
/**
 * バフ/デバフを追加または更新（重症度対応版）
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
  const hasSeverity = hasBuffDebuffSeverity(type);

  if (existing) {
    // 既存のバフ/デバフがある場合
    if (hasSeverity) {
      // 重症度システムあり
      if (existing.severity === BuffDebuffSeverity.LIGHT) {
        // 軽度 → 重度に進化
        newMap.set(type, {
          ...existing,
          severity: BuffDebuffSeverity.HEAVY,
          stacks: existing.stacks + stacks,
          duration: Math.max(existing.duration, duration),
          value: Math.max(existing.value, value),
        });
      } else {
        // 重度の場合は持続ターンとスタックのみ延長
        newMap.set(type, {
          ...existing,
          stacks: existing.stacks + stacks,
          duration: Math.max(existing.duration, duration),
          value: Math.max(existing.value, value),
        });
      }
    } else {
      // 重症度システムなし（通常のスタック加算）
      newMap.set(type, {
        ...existing,
        stacks: existing.stacks + stacks,
        duration: Math.max(existing.duration, duration),
        value: Math.max(existing.value, value),
      });
    }
  } else {
    // 新規追加
    newMap.set(type, {
      type,
      severity: hasSeverity
        ? BuffDebuffSeverity.LIGHT
        : BuffDebuffSeverity.NONE,
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

# 5. 持続時間管理

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

# 6. 計算優先度

## 6.1 ダメージ計算での重症度適用

```typescript
/**
 * 攻撃力の倍率計算（重症度考慮版）
 */
function calculateAttackMultiplier(buffDebuffs: BuffDebuffMap): number {
  let multiplier = 1.0;

  // 脱力デバフ
  if (buffDebuffs.has("feeble")) {
    const feeble = buffDebuffs.get("feeble")!;
    if (feeble.severity === BuffDebuffSeverity.LIGHT) {
      multiplier *= 0.75; // 軽度: ×0.75
    } else if (feeble.severity === BuffDebuffSeverity.HEAVY) {
      multiplier *= 0.5; // 重度: ×0.50
    }
  }

  // 麻痺デバフ
  if (buffDebuffs.has("paralyze")) {
    const paralyze = buffDebuffs.get("paralyze")!;
    if (paralyze.severity === BuffDebuffSeverity.LIGHT) {
      multiplier *= 0.7; // 軽度: -30%
    } else if (paralyze.severity === BuffDebuffSeverity.HEAVY) {
      multiplier *= 0.5; // 重度: -50%
    }
  }

  // 攻撃力上昇バフ（重症度なし）
  if (buffDebuffs.has("atkUp")) {
    const atkUp = buffDebuffs.get("atkUp")!;
    multiplier += atkUp.value / 100;
  }

  return multiplier;
}
```

---

# 7. 実装関数一覧

## 7.1 バフ/デバフ管理

```typescript
// バフ/デバフの追加・更新（重症度対応）
addOrUpdateBuffDebuff(map, type, stacks, duration, value, isPermanent, source)

// バフ/デバフの削除
removeBuffDebuff(map, type)

// 全デバフの削除
removeAllDebuffs(map)

// 持続時間の減少
decreaseBuffDebuffDuration(map)

// 重症度判定
hasBuffDebuffSeverity(type): boolean

// 重症度取得
getSeverity(map, type): BuffDebuffSeverity
```

## 7.2 ダメージ・回復計算

```typescript
// ターン終了時の持続ダメージ（重症度考慮）
calculateEndTurnDamage(map): number

// ターン開始時の回復・再生
calculateStartTurnHealing(map): { hp: number; shield: number }

// 攻撃力の倍率計算（重症度考慮）
calculateAttackMultiplier(map): number

// 防御力の倍率計算（重症度考慮）
calculateDefenseMultiplier(map): number
```
