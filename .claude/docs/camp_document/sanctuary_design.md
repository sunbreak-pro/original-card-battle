# 神殿（聖域）詳細設計書 V2.0 (SANCTUARY_DESIGN_V2)

## 更新履歴

- V2.0: **設計の根本的変更** - 魂の残滓を経験値システムに変更、探索回数+1 スキル追加、ローグライト要素削除

---

## 1. 概要

神殿（The Sanctuary）は、**魂の残滓（Soul Remnants）**を消費して恒久的な強化を解放する施設です。

**V2.0 での大幅変更:**

```
旧設計:
死亡時に魂を獲得 → 恒久強化（ローグライト要素）

新設計:
魔物撃破で魂を獲得（経験値的） → 生還で累計に加算 → 恒久強化
探索回数制限との統合
```

### 主な役割

1. **恒久強化 (Permanent Upgrades)**: ラン間で永続するプレイヤー基礎能力の向上
2. **成長の選択 (Build Diversity)**: スキルツリーで成長方向を選択
3. **進歩の可視化 (Progress Visualization)**: 解放済みノードで成長を実感
4. **探索回数の拡張 (Exploration Expansion)**: 探索回数 +1 スキル（NEW）

---

## 2. 詳細機能仕様

### 2.1 魂の残滓（Soul Remnants）- V2.0 大幅変更

#### 2.1.1 獲得方法（経験値システム）

**V2.0 での変更:**

```
旧: 死亡時に獲得
新: 魔物撃破時に獲得（経験値的）
```

**獲得タイミング:**

| 獲得タイミング       | 獲得量     | 備考             |
| -------------------- | ---------- | ---------------- |
| 雑魚敵撃破           | 5 魂       | 戦闘中に即時加算 |
| 強敵撃破             | 15 魂      | エリート・中ボス |
| ボス撃破             | 50 魂      | 各深度のボス     |
| 帰還戦闘（弱体化敵） | 通常の 50% | 帰還ルート使用時 |

**重要な仕組み:**

```typescript
// この探索での獲得魂（一時的）
currentRunSouls: number;

// 累計魂（恒久的）
totalSouls: number;

// 生還時
totalSouls += currentRunSouls × 生還方法倍率;
currentRunSouls = 0;

// 死亡時
currentRunSouls = 0;  // この探索分はゼロ
// totalSouls は保持（変化なし）
```

#### 2.1.2 生還・死亡時の処理

**生還の場合:**

```
魔物を撃破 → 魂を獲得 (currentRunSouls)
  ↓
生還（転移石 or 帰還ルート）
  ↓
獲得した魂 × 生還方法倍率 → 累計に加算

生還方法倍率:
- 帰還ルート: 100%
- 通常転移石: 70%
- 祝福転移石: 80%
- 緊急転移石: 60%

例: この探索で100魂獲得、通常転移石で生還
→ 100 × 0.7 = 70魂が累計に加算
```

**死亡の場合:**

```
魔物を撃破 → 魂を獲得 (currentRunSouls)
  ↓
死亡
  ↓
この探索で獲得した魂 → ゼロ
累計魂（totalSouls）→ 保持（変化なし）

例: この探索で100魂獲得、死亡
→ 100魂はゼロに
→ ただし過去の累計500魂は保持
```

**性質:**

- 過去に蓄積した魂は**永続保持**
- この探索で獲得した魂は生還しないと加算されない
- Sanctuary でのみ使用可能

**初期所持:**

- 新規プレイヤー: 50 魂（チュートリアル用）

---

### 2.2 スキルツリー構造

#### 2.2.1 ツリーの形状

**放射状デザイン（推奨）:**

```
                  [中心]
                    │
        ┌───────────┼───────────┐
        │           │           │
     [HP系]      [Gold系]    [Utility系]
        │           │           │
    ┌───┼───┐   ┌───┼───┐   ┌───┼───┐
  [+10] [+20] [+10%][+20%] [探索][拡張]
    │           │         回数+1    │
  [+30]       [+30%]        │     [上位]
                         [回数+2]
```

**特徴:**

- 中心から 4 方向に伸びる
- 各方向はテーマ別（HP/Gold/戦闘/Utility）
- 上位ノードほど強力だが高コスト
- **探索回数+1 が戦略的に重要（NEW）**

#### 2.2.2 ノードの種類

**第 1 層（基礎強化）: コスト 20-30 魂**

| ノード名     | アイコン | 効果                           | コスト |
| ------------ | -------- | ------------------------------ | ------ |
| 生命の加護 I | ❤️       | 初期 HP +10                    | 20     |
| 富の祝福 I   | 💰       | 初期 Gold +10%                 | 25     |
| 剣士の心得   | ⚔️       | 剣士: 剣エネルギー +1 スタート | 30     |
| 魔術師の心得 | 🔮       | 魔術師: 初期共鳴レベル +1      | 30     |
| 召喚師の心得 | 👻       | 召喚師: 初期召喚枠 +1          | 30     |

**第 2 層（専門化）: コスト 40-80 魂**

| ノード名         | アイコン | 効果               | コスト | 前提         |
| ---------------- | -------- | ------------------ | ------ | ------------ |
| 生命の加護 II    | ❤️❤️     | 初期 HP +20        | 50     | 生命の加護 I |
| 富の祝福 II      | 💰💰     | 初期 Gold +20%     | 60     | 富の祝福 I   |
| 鑑定の目         | 👁️       | 装備の詳細情報表示 | 40     | -            |
| 拡張の鞄         | 🎒       | インベントリ +5    | 50     | -            |
| 回復の恩恵       | 💊       | 戦闘後 HP +5%回復  | 60     | 生命の加護 I |
| **探索の延長 I** | ⏰       | **探索回数 +1**    | 80     | -            |

**第 3 層（究極強化）: コスト 100-150 魂**

| ノード名          | アイコン | 効果                               | コスト | 前提          |
| ----------------- | -------- | ---------------------------------- | ------ | ------------- |
| 生命の加護 III    | ❤️❤️❤️   | 初期 HP +30                        | 100    | 生命の加護 II |
| 富の祝福 III      | 💰💰💰   | 初期 Gold +30%                     | 100    | 富の祝福 II   |
| 不屈の意志        | 🛡️       | 1 探索に 1 回、HP0 で 1 で生き残る | 120    | 生命の加護 II |
| 魂の共鳴          | ✨       | 魂の残滓獲得 +20%                  | 80     | -             |
| 真なる鑑定        | 👁️‍🗨️       | 装備の隠し効果も表示               | 90     | 鑑定の目      |
| **探索の延長 II** | ⏰⏰     | **探索回数 +2（累計+3）**          | 150    | 探索の延長 I  |

#### 2.2.3 探索回数拡張スキル（NEW - 重要）

**探索の延長スキル:**

```
【第2層】探索の延長 I
コスト: 80魂
効果: 探索回数の上限を +1 増やす
前提: なし

【第3層】探索の延長 II
コスト: 150魂
効果: 探索回数の上限を +2 増やす（累計+3）
前提: 探索の延長 I

【効果の例】
デフォルト: 10回探索
探索の延長 I 解放後: 11回探索
探索の延長 II 解放後: 13回探索
```

**戦略的価値:**

- 探索回数制限を緩和
- より多くの試行錯誤が可能
- 深層到達の確率が上がる
- 高コストだが価値は非常に高い

#### 2.2.4 クラス特化ノード

**剣士専用:**

```
剣士の心得 I (30魂) → 剣士の心得 II (60魂) → 剣士の極意 (100魂)
効果: 剣エネルギー +1/+2/+3 スタート
```

**魔術師専用:**

```
魔術師の心得 I (30魂) → 魔術師の心得 II (60魂) → 魔術師の極意 (100魂)
効果: 初期共鳴レベル +1/+2/+3
```

**召喚師専用:**

```
召喚師の心得 I (30魂) → 召喚師の心得 II (60魂) → 召喚師の極意 (100魂)
効果: 初期召喚枠 +1/+2/+3
```

---

### 2.3 ノードの状態

**3 つの状態:**

1. **解放済み（Unlocked）**

   - ビジュアル: 点灯、金色に輝く
   - 効果: 有効化
   - 操作: 不可

2. **解放可能（Available）**

   - ビジュアル: 点滅、白色
   - 効果: 未適用
   - 操作: 長押しで解放
   - 条件: 前提ノード解放 + 魂の残滓充足

3. **ロック（Locked）**
   - ビジュアル: グレーアウト、シルエット
   - 効果: なし
   - 操作: 不可
   - 理由: 前提未解放 or 魂の残滓不足

---

## 3. UI/UX デザイン

### 3.1 画面レイアウト

```
┌────────────────────────────────────────────────────────┐
│  ✨ 神殿 - The Sanctuary                              │
├────────────────────────────────────────────────────────┤
│  魂の残滓: 累計 650魂 / この探索 +85魂                 │  ← NEW
│  探索回数上限: 10回 (+0)                               │  ← NEW
│                                                        │
│              [スキルツリー表示エリア]                   │
│                                                        │
│                      [中心]                            │
│                    (解放済み)                          │
│                        │                               │
│        ┌───────────────┼───────────────┐               │
│        │               │               │               │
│   [生命の加護I]   [富の祝福I]    [探索の延長I]        │  ← NEW
│   (解放済み✨)   (解放可能💫)   (解放可能💫)            │
│        │               │           80魂               │
│   [生命の加護II]  [富の祝福II] [探索の延長II]         │  ← NEW
│   (ロック🔒)     (ロック🔒)    (ロック🔒)              │
│                                  150魂                │
│                                                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │ [選択中のノード詳細]                              │  │
│  │ ⏰ 探索の延長 I                                  │  │  ← NEW
│  │                                                  │  │
│  │ 効果: 探索回数の上限を +1 増やす                 │  │
│  │       (現在 10回 → 11回に)                       │  │
│  │                                                  │  │
│  │ コスト: 80 魂の残滓                               │  │
│  │ 現在の所持: 650魂（充足）                         │  │
│  │                                                  │  │
│  │ 前提条件: なし                                    │  │
│  │                                                  │  │
│  │ ※探索回数制限を緩和し、                          │  │
│  │   より多くの挑戦が可能になります                  │  │
│  │                                                  │  │
│  │ [長押しで解放]                                    │  │
│  └──────────────────────────────────────────────────┘  │
│                                                        │
│  [キャンプに戻る]                                      │
└────────────────────────────────────────────────────────┘
```

### 3.2 ノードのビジュアル

**解放済み:**

```css
border: 3px solid gold;
background: radial-gradient(circle, #ffd700, #ffa500);
box-shadow: 0 0 20px gold;
animation: pulse 2s infinite;
```

**解放可能:**

```css
border: 3px solid white;
background: radial-gradient(circle, #ffffff, #cccccc);
box-shadow: 0 0 10px white;
animation: blink 1.5s infinite;
```

**ロック:**

```css
border: 2px solid #444;
background: #222;
opacity: 0.5;
filter: grayscale(100%);
```

### 3.3 解放インタラクション

**長押し解放（1.5 秒）:**

```
タップ開始
  ↓
プログレスリング表示（0%）
  ↓
長押し継続（0.5秒）
  ↓
プログレスリング（33%）
効果音: 「シュゥゥ...」
  ↓
長押し継続（1.0秒）
  ↓
プログレスリング（66%）
効果音: 「シュゥゥ...」（大きく）
  ↓
長押し完了（1.5秒）
  ↓
解放エフェクト
効果音: 「キラーン！」
  ↓
ノードが点灯
聖なる光が広がる
  ↓
解放完了メッセージ
「探索の延長 I を解放しました！」
「探索回数上限が 10回 → 11回 になりました！」  ← NEW
```

**キャンセル:**

- 長押し途中で指を離す → キャンセル
- プログレスリング消失
- 魂の残滓は消費されない

### 3.4 演出

**解放成功:**

```
効果音: 重厚な鐘の音「ゴーーーン」
ビジュアル:
  1. ノードから聖なる光が爆発
  2. 光が画面全体に広がる
  3. ステンドグラスが一瞬輝く
  4. ノードが金色に点灯
  5. 探索回数が増えた場合、特別なエフェクト ← NEW
```

**魂の残滓獲得（V2.0）:**

```
魔物撃破時:
  1. 魂のオーブが敵から出現
  2. プレイヤーに吸収される
  3. 「+5 魂の残滓」表示
  4. 画面右上の「この探索」カウンターが更新

生還時:
  1. 大きな魂のオーブが出現
  2. 数値カウントアップ
  3. 「+85 魂の残滓（累計 650 → 735）」表示
  4. キラキラエフェクト

死亡時:
  1. 魂のオーブが砕け散る
  2. 「この探索の魂 85個は失われた...」
  3. 「累計 650魂は保持されています」
  4. 暗い演出
```

---

## 4. データ構造定義

### 4.1 SanctuaryTypes.ts

```typescript
// src/types/SanctuaryTypes.ts (更新)

/**
 * スキルノード
 */
export interface SkillNode {
  id: string;
  name: string;
  description: string;
  icon: string;
  cost: number;
  category: "hp" | "gold" | "combat" | "utility" | "class" | "exploration"; // NEW: exploration追加
  tier: 1 | 2 | 3;
  prerequisites: string[]; // 前提ノードのID
  effects: NodeEffect[];
  classRestriction?: "swordsman" | "mage" | "summoner";
}

/**
 * ノードの効果
 */
export interface NodeEffect {
  type:
    | "stat_boost"
    | "special_ability"
    | "resource_increase"
    | "exploration_limit"; // NEW
  target: string; // 'initial_hp', 'initial_gold', 'exploration_limit', etc.
  value: number | string;
}

/**
 * ノードの状態
 */
export type NodeStatus = "unlocked" | "available" | "locked";

/**
 * プレイヤーのSanctuaryデータ（V2.0）
 */
export interface SanctuaryProgress {
  unlockedNodes: Set<string>;

  // V2.0: 魂の残滓システム
  currentRunSouls: number; // この探索で獲得した魂
  totalSouls: number; // 累計魂（過去の探索分）

  // NEW: 探索回数拡張
  explorationLimitBonus: number; // 探索回数の追加分（デフォルト0）
}
```

### 4.2 スキルノードデータ

```typescript
// src/camps/facilities/Sanctuary/data/SkillTreeData.ts (更新)

import type { SkillNode } from "../../../../types/SanctuaryTypes";

/**
 * 第1層: 基礎強化
 */
export const TIER1_NODES: SkillNode[] = [
  {
    id: "hp_blessing_1",
    name: "生命の加護 I",
    description: "初期HPが10増加する",
    icon: "❤️",
    cost: 20,
    category: "hp",
    tier: 1,
    prerequisites: [],
    effects: [{ type: "stat_boost", target: "initial_hp", value: 10 }],
  },
  {
    id: "gold_blessing_1",
    name: "富の祝福 I",
    description: "初期Goldが10%増加する",
    icon: "💰",
    cost: 25,
    category: "gold",
    tier: 1,
    prerequisites: [],
    effects: [
      { type: "stat_boost", target: "initial_gold_multiplier", value: 1.1 },
    ],
  },
  // ... 他のノード
];

/**
 * 第2層: 専門化（探索回数拡張を含む）
 */
export const TIER2_NODES: SkillNode[] = [
  {
    id: "hp_blessing_2",
    name: "生命の加護 II",
    description: "初期HPがさらに20増加する",
    icon: "❤️❤️",
    cost: 50,
    category: "hp",
    tier: 2,
    prerequisites: ["hp_blessing_1"],
    effects: [{ type: "stat_boost", target: "initial_hp", value: 20 }],
  },
  // NEW: 探索回数拡張
  {
    id: "exploration_extension_1",
    name: "探索の延長 I",
    description: "探索回数の上限を +1 増やす",
    icon: "⏰",
    cost: 80,
    category: "exploration",
    tier: 2,
    prerequisites: [],
    effects: [
      { type: "exploration_limit", target: "max_explorations", value: 1 },
    ],
  },
  // ... 他のノード
];

/**
 * 第3層: 究極強化（探索回数拡張 IIを含む）
 */
export const TIER3_NODES: SkillNode[] = [
  {
    id: "hp_blessing_3",
    name: "生命の加護 III",
    description: "初期HPがさらに30増加する",
    icon: "❤️❤️❤️",
    cost: 100,
    category: "hp",
    tier: 3,
    prerequisites: ["hp_blessing_2"],
    effects: [{ type: "stat_boost", target: "initial_hp", value: 30 }],
  },
  // NEW: 探索回数拡張 II
  {
    id: "exploration_extension_2",
    name: "探索の延長 II",
    description: "探索回数の上限をさらに +2 増やす（累計+3）",
    icon: "⏰⏰",
    cost: 150,
    category: "exploration",
    tier: 3,
    prerequisites: ["exploration_extension_1"],
    effects: [
      { type: "exploration_limit", target: "max_explorations", value: 2 },
    ],
  },
  // ... 他のノード
];

export const ALL_SKILL_NODES = [...TIER1_NODES, ...TIER2_NODES, ...TIER3_NODES];
```

---

## 5. ロジック実装

### 5.1 ノード状態の判定

```typescript
// src/camps/facilities/Sanctuary/logic/nodeStatus.ts (更新)

import type {
  SkillNode,
  NodeStatus,
  SanctuaryProgress,
} from "../../../../types/SanctuaryTypes";

/**
 * ノードの状態を判定
 */
export function getNodeStatus(
  node: SkillNode,
  progress: SanctuaryProgress
): NodeStatus {
  // すでに解放済み
  if (progress.unlockedNodes.has(node.id)) {
    return "unlocked";
  }

  // 前提条件チェック
  const prerequisitesMet = node.prerequisites.every((prereqId) =>
    progress.unlockedNodes.has(prereqId)
  );

  if (!prerequisitesMet) {
    return "locked";
  }

  // 魂の残滓充足チェック（V2.0: totalSouls使用）
  if (progress.totalSouls < node.cost) {
    return "locked";
  }

  return "available";
}

/**
 * ノードを解放
 */
export function unlockNode(
  node: SkillNode,
  progress: SanctuaryProgress
): SanctuaryProgress {
  const status = getNodeStatus(node, progress);

  if (status !== "available") {
    throw new Error(`Cannot unlock node ${node.id}: status is ${status}`);
  }

  return {
    ...progress,
    unlockedNodes: new Set([...progress.unlockedNodes, node.id]),
    totalSouls: progress.totalSouls - node.cost,
  };
}
```

### 5.2 効果の適用

```typescript
// src/camps/facilities/Sanctuary/logic/applyEffects.ts (更新)

import type { SanctuaryProgress } from "../../../../types/SanctuaryTypes";
import { ALL_SKILL_NODES } from "../data/SkillTreeData";

/**
 * 解放済みノードの効果を全て適用
 */
export function calculateTotalEffects(progress: SanctuaryProgress) {
  const effects = {
    initial_hp: 0,
    initial_gold_multiplier: 1.0,
    initial_sword_energy: 0,
    initial_resonance_level: 0,
    initial_summon_slots: 0,
    inventory_size: 0,
    exploration_limit_bonus: 0, // NEW
    special_abilities: new Set<string>(),
  };

  // 解放済みノードの効果を累積
  progress.unlockedNodes.forEach((nodeId) => {
    const node = ALL_SKILL_NODES.find((n) => n.id === nodeId);
    if (!node) return;

    node.effects.forEach((effect) => {
      switch (effect.type) {
        case "stat_boost":
          if (effect.target === "initial_hp") {
            effects.initial_hp += effect.value as number;
          } else if (effect.target === "initial_gold_multiplier") {
            effects.initial_gold_multiplier *= effect.value as number;
          }
          // ... 他のステータス
          break;

        // NEW: 探索回数拡張
        case "exploration_limit":
          if (effect.target === "max_explorations") {
            effects.exploration_limit_bonus += effect.value as number;
          }
          break;

        case "special_ability":
          effects.special_abilities.add(effect.value as string);
          break;

        case "resource_increase":
          if (effect.target === "inventory_size") {
            effects.inventory_size += effect.value as number;
          }
          break;
      }
    });
  });

  return effects;
}
```

### 5.3 魂の残滓システム（V2.0 - 新規）

```typescript
// src/camps/facilities/Sanctuary/logic/soulSystem.ts (新規作成)

import type { SanctuaryProgress } from "../../../../types/SanctuaryTypes";

/**
 * 魔物撃破時に魂を獲得
 */
export function gainSoulFromEnemy(
  progress: SanctuaryProgress,
  enemyType: "minion" | "elite" | "boss",
  isReturnBattle: boolean = false
): SanctuaryProgress {
  const baseSouls: Record<typeof enemyType, number> = {
    minion: 5,
    elite: 15,
    boss: 50,
  };

  let soulsGained = baseSouls[enemyType];

  // 帰還戦闘は50%
  if (isReturnBattle) {
    soulsGained = Math.floor(soulsGained * 0.5);
  }

  return {
    ...progress,
    currentRunSouls: progress.currentRunSouls + soulsGained,
  };
}

/**
 * 生還時に累計に加算
 */
export function completeSurvival(
  progress: SanctuaryProgress,
  returnMethod:
    | "return_route"
    | "teleport_normal"
    | "teleport_blessed"
    | "teleport_emergency"
): SanctuaryProgress {
  const multipliers = {
    return_route: 1.0,
    teleport_normal: 0.7,
    teleport_blessed: 0.8,
    teleport_emergency: 0.6,
  };

  const multiplier = multipliers[returnMethod];
  const soulsToAdd = Math.floor(progress.currentRunSouls * multiplier);

  return {
    ...progress,
    totalSouls: progress.totalSouls + soulsToAdd,
    currentRunSouls: 0, // リセット
  };
}

/**
 * 死亡時の処理
 */
export function handleDeath(progress: SanctuaryProgress): SanctuaryProgress {
  return {
    ...progress,
    currentRunSouls: 0, // この探索分はゼロ
    // totalSoulsは保持（変化なし）
  };
}
```

---

## 6. PlayerContext への統合

```typescript
// src/contexts/PlayerContext.tsx (大幅修正)

import type { SanctuaryProgress } from "../types/SanctuaryTypes";
import { calculateTotalEffects } from "../camps/facilities/Sanctuary/logic/applyEffects";
import {
  gainSoulFromEnemy,
  completeSurvival,
  handleDeath,
} from "../camps/facilities/Sanctuary/logic/soulSystem";

export interface Player {
  // ... 既存のフィールド

  // V2.0: Sanctuary更新
  sanctuaryProgress: SanctuaryProgress;

  // NEW: 探索回数制限
  explorationLimit: {
    max: number; // 最大探索回数（デフォルト10 + ボーナス）
    current: number; // 現在の探索回数
  };
}

export const PlayerProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [player, setPlayer] = useState<Player>(() => {
    // セーブデータから読み込み
    const saved = localStorage.getItem("player");
    if (saved) {
      return JSON.parse(saved);
    }

    return {
      // ... 既存の初期値
      sanctuaryProgress: {
        unlockedNodes: new Set(),
        currentRunSouls: 0,
        totalSouls: 50, // 初期50魂
        explorationLimitBonus: 0,
      },
      explorationLimit: {
        max: 10,
        current: 0,
      },
    };
  });

  // Sanctuary効果を適用した初期ステータス計算
  const getSanctuaryBoostedStats = () => {
    const effects = calculateTotalEffects(player.sanctuaryProgress);

    return {
      initialHp: player.baseHp + effects.initial_hp,
      initialGold: Math.floor(
        player.baseGold * effects.initial_gold_multiplier
      ),
      explorationLimitMax: 10 + effects.exploration_limit_bonus, // NEW
      // ... 他のステータス
    };
  };

  // 魔物撃破時に魂を獲得（V2.0 - NEW）
  const gainSouls = (
    enemyType: "minion" | "elite" | "boss",
    isReturnBattle: boolean = false
  ) => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: gainSoulFromEnemy(
        prev.sanctuaryProgress,
        enemyType,
        isReturnBattle
      ),
    }));
  };

  // 生還時の処理（V2.0 - NEW）
  const handleSurvival = (
    returnMethod:
      | "return_route"
      | "teleport_normal"
      | "teleport_blessed"
      | "teleport_emergency"
  ) => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: completeSurvival(prev.sanctuaryProgress, returnMethod),
      explorationLimit: {
        ...prev.explorationLimit,
        current: prev.explorationLimit.current + 1,
      },
    }));
  };

  // 死亡時の処理（V2.0 - NEW）
  const handlePlayerDeath = () => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: handleDeath(prev.sanctuaryProgress),
      explorationLimit: {
        ...prev.explorationLimit,
        current: prev.explorationLimit.current + 1,
      },
      // 装備・Gold・魔石はロスト
      equipment: [],
      gold: 0,
      magicStones: { tiny: 0, small: 0, medium: 0, large: 0, huge: 0 },
    }));
  };

  return (
    <PlayerContext.Provider
      value={{
        player,
        setPlayer,
        gainSouls, // NEW
        handleSurvival, // NEW
        handlePlayerDeath, // NEW
        getSanctuaryBoostedStats,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};
```

---

## 7. 実装手順（概要）

### Phase 1: データ構造（Week 1: Day 1-2）

```
□ SanctuaryTypes.ts 更新（exploration_limit追加）
□ SkillTreeData.ts 更新（探索回数拡張スキル追加）
□ PlayerContext に currentRunSouls / totalSouls 追加
□ PlayerContext に explorationLimit 追加
```

### Phase 2: ロジック実装（Week 1: Day 3-4）

```
□ nodeStatus.ts（状態判定）
□ applyEffects.ts（効果適用・探索回数拡張）
□ soulSystem.ts（V2.0 - 新規作成）
  □ gainSoulFromEnemy（魔物撃破時）
  □ completeSurvival（生還時）
  □ handleDeath（死亡時）
□ PlayerContext に統合
```

### Phase 3: UI 実装（Week 2: Day 1-3）

```
□ Sanctuary.tsx（メインコンテナ）
  □ 累計魂と探索魂の表示
  □ 探索回数上限の表示
□ SkillTree.tsx（ツリー表示）
□ SkillNode.tsx（個別ノード）
□ NodeDetailPanel.tsx（詳細パネル・探索回数表示）
```

### Phase 4: インタラクション（Week 2: Day 4-5）

```
□ 長押し解放の実装
□ プログレスリングUI
□ 解放エフェクト
□ 探索回数拡張の特別演出
□ 効果音
```

### Phase 5: バトルシステムとの統合（Week 3）

```
□ 魔物撃破時に gainSouls 呼び出し
□ 生還時に handleSurvival 呼び出し
□ 死亡時に handlePlayerDeath 呼び出し
□ 探索回数のカウント
```

---

## 8. 注意事項

### 8.1 バランス調整

**Phase 1 実装後:**

- 魂の残滓獲得量の調整
- ノードコストの調整
- 効果量の調整
- 探索回数拡張の価値調整

**目標:**

- 5-10 回の探索で第 1 層を全解放
- 30-50 回の探索で第 2 層を大部分解放
- 探索の延長 I は重要な選択肢
- 探索の延長 II は高難度プレイヤー向け

### 8.2 セーブデータへの影響

**重要:** sanctuaryProgress は永続保存が必須

- localStorage への保存
- クラウド同期（将来）
- データ破損時のバックアップ
- currentRunSouls は一時的（探索中のみ）
- totalSouls は恒久的（常に保存）

---

## 9. 参照ドキュメント

```
GAME_DESIGN_MASTER_V2
├── return_system_v2.md（生還システム）
└── SANCTUARY_DESIGN_V2 [本文書]
    ├── SkillTreeData.ts
    ├── nodeStatus.ts
    ├── applyEffects.ts
    └── soulSystem.ts（NEW）
```

---

## まとめ

神殿の設計が V2.0 に更新されました：

**V2.0 での主な変更:**

- ✅ 魂の残滓を経験値システムに変更
- ✅ 魔物撃破時に獲得、生還で累計加算
- ✅ 死亡時は探索分ゼロ、累計は保持
- ✅ 探索回数+1 スキルの追加
- ✅ ローグライト要素の完全削除

**設計意図:**

- リスク・リターンの明確化
- 生還の価値向上
- 探索回数制限との統合
- プレイヤーの成長を可視化
