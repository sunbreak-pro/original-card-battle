# 生還システム完全設計書 V2.0

## 更新履歴

- V2.0: **設計の根本的変更** - 魂の残滓を経験値システムに変更、探索回数制限追加

## 目次

```
1. 生還システムの概要
2. 生還方法の種類
3. 転移石システム
4. 帰還ルートシステム
5. 深淵（深度5）の特別ルール
6. 生還時の報酬計算
7. 探索回数制限との関係（NEW）
8. 戦略的意義とバランス
9. 実装仕様
10. まとめ
```

---

# 1. 生還システムの概要

## 1.1 基本コンセプト

```
生還システムは「リスク管理」と「リソース保全」のトレードオフを提供する。

【設計思想】
- 深く潜るほど報酬は大きいが、リスクも増大
- 適切なタイミングで撤退する判断力を試す
- 完全な死亡ではなく「戦略的撤退」の選択肢を提供
- 装備の耐久度管理と密接に連携
- 探索回数制限により、各探索に重みを持たせる
```

## 1.2 生還と死亡の違い

| 項目     | 生還                                         | 死亡                             |
| -------- | -------------------------------------------- | -------------------------------- |
| 装備     | **全て持ち越し**                             | 全喪失                           |
| 耐久度   | **そのまま**                                 | -                                |
| 熟練度   | **使用回数記録**                             | 使用回数記録                     |
| Gold     | **全額持ち帰り（方法により減少）**           | ゼロ                             |
| 魔石     | **方法により減少**                           | ゼロ                             |
| 魂の残滓 | **その探索分を累計に加算（方法により減少）** | **その探索分ゼロ（累計は保持）** |
| 探索記録 | 記録される                                   | 記録される                       |
| 探索回数 | **+1**                                       | **+1**                           |

**生還の価値:**

- 装備を確実に保持できる
- 熟練度の使用回数が保存される
- Gold・魔石・魂を持ち帰れる
- 探索回数を有効活用できる

**死亡のペナルティ:**

- 装備の完全喪失
- 収集した Gold・魔石・資源が全て失われる
- **その探索で獲得した魂の残滓もゼロ**
- ただし、**過去に蓄積した魂の残滓（累計）は保持**
- **探索回数は消費される**

**重要な変更点（V2.0）:**

```
旧設計:
死亡時 → 魂の残滓を「通常獲得」（ローグライト要素）

新設計:
魔物撃破時 → 魂の残滓を獲得（経験値的）
生還 → 累計に加算
死亡 → その探索分はゼロ（累計は保持）
```

---

# 2. 生還方法の種類

## 2.1 生還方法の一覧

```
【2つの生還方法】

1. 転移石（即時帰還）
   - コスト: アイテム消費
   - リスク: なし
   - 報酬: 減少あり
   - 制限: 深度5（深淵）では使用不可

2. 帰還ルート（段階的撤退）
   - コスト: 時間と戦闘
   - リスク: 道中の敵
   - 報酬: 減少なし
   - 制限: 深度5（深淵）では使用不可
```

## 2.2 各方法の比較表

| 方法       | 使用条件                 | 即時性   | 報酬倍率 | リスク   | 推奨場面   |
| ---------- | ------------------------ | -------- | -------- | -------- | ---------- |
| 通常転移石 | アイテム所持             | **即時** | 70%      | なし     | 早期撤退   |
| 祝福転移石 | アイテム所持             | **即時** | 80%      | なし     | 報酬確保   |
| 緊急転移石 | アイテム所持<br>戦闘中可 | **即時** | 60%      | なし     | 戦闘中脱出 |
| 帰還ルート | 戦闘可能                 | 遅い     | **100%** | 道中戦闘 | 最大報酬   |

**重要:** 深度 5（深淵）では全ての生還手段が無効化される。
深淵に入ったら「ボスを倒すか死ぬか」の二択のみ。

---

# 3. 転移石システム

## 3.1 転移石の種類

### 3.1.1 通常転移石

```
【基本仕様】
名称: 帰還の転移石
効果: 即座に拠点へ帰還 / 報酬倍率70%
入手: ショップ / イベント / 宝箱

【使用条件】
- 戦闘中は使用不可
- マップ画面でのみ使用可能
- 深度1〜4でのみ使用可能（深度5では無効）

【特徴】
希少性: コモン
1ランあたり推奨所持数: 1〜2個
```

### 3.1.2 祝福転移石

```
【基本仕様】
名称: 祝福の転移石
効果: 帰還 + 報酬倍率80%（通常より高い）
入手: イベント / ボス報酬 / 隠し部屋

【使用条件】
- 戦闘中は使用不可
- マップ画面でのみ使用可能
- 深度1〜4でのみ使用可能（深度5では無効）

【特徴】
希少性: レア
報酬保持率が高く、より多くの資源を持ち帰れる
```

### 3.1.3 緊急転移石

```
【基本仕様】
名称: 緊急転移石
効果: 戦闘中でも使用可能 / 報酬倍率60%
入手: ショップ / 特別イベント

【使用条件】
- いつでも使用可能
- 戦闘中でも発動
- 深度1〜4でのみ使用可能（深度5では無効）

【特徴】
希少性: エピック
戦略的価値: 戦闘中の最後の保険
報酬は少ないが命と装備を守れる
```

---

## 3.2 転移石使用時の処理フロー

```typescript
/**
 * 転移石使用処理
 */
interface TeleportStone {
  type: "normal" | "blessed" | "emergency";
  rewardMultiplier: number; // 0.7 / 0.8 / 0.6
  canUseInBattle: boolean;
}

function useTeleportStone(
  player: Character,
  stone: TeleportStone,
  currentDepth: number,
  defeatedEnemies: number,
  soulsEarnedThisRun: number // NEW: この探索で獲得した魂
): ReturnResult {
  // 深度5（深淵）チェック
  if (currentDepth === 5) {
    return {
      success: false,
      message: "深淵では転移石が無効化されている...",
    };
  }

  // 戦闘中チェック
  if (isInBattle() && !stone.canUseInBattle) {
    return {
      success: false,
      message: "戦闘中は使用できません",
    };
  }

  // 報酬計算
  const baseReward = calculateBaseReward(currentDepth, defeatedEnemies);
  const finalReward = {
    gold: Math.floor(baseReward.gold * stone.rewardMultiplier),
    stones: Math.floor(baseReward.stones * stone.rewardMultiplier),
    souls: Math.floor(soulsEarnedThisRun * stone.rewardMultiplier), // NEW: 獲得した魂に倍率適用
  };

  // 装備・熟練度は全保持
  savePlayerState(player);

  // 魂の残滓を累計に加算 (NEW)
  addToTotalSouls(finalReward.souls);

  // 拠点へ帰還
  return {
    success: true,
    type: "teleport",
    rewards: finalReward,
    message: "拠点へ帰還しました",
  };
}
```

---

## 3.3 転移石の戦略的使用タイミング

```
【推奨使用タイミング】

1. 装備耐久度が限界
   - 全装備耐久度 < 20%
   → 次戦闘で全破損の危険

2. HP/シールドが低い
   - HP < 40% かつ 回復手段なし
   → 次戦闘で死亡リスク高

3. 強力な装備を獲得
   - レア/エピック装備入手
   → 死亡による喪失を避ける

4. Gold・魔石・魂が潤沢
   - 目標リソース達成
   → これ以上のリスク不要

5. ボス戦前の準備不足
   - 消耗品不足
   → 拠点で補給してから再挑戦

6. 探索回数が残り少ない（NEW）
   - 残り2〜3回で深層到達必須
   → 確実に魂を持ち帰る
```

---

# 4. 帰還ルートシステム

## 4.1 基本仕様

```
【コンセプト】
来た道を逆走して拠点へ戻る
報酬減少なし、ただし道中の戦闘が発生

【メリット】
- 報酬100%獲得
- 追加の戦闘経験
- 熟練度をさらに稼げる
- 魂の残滓も追加で獲得可能（NEW）

【デメリット】
- 時間がかかる
- 道中の戦闘リスク
- 装備耐久度の更なる消耗
- 死亡すると全ての魂がゼロに（NEW）
```

## 4.2 帰還ルートの敵

### 4.2.1 敵の出現ルール

```
【遭遇率の変動システム】
帰還を進めるごとに遭遇率が減少
→ 拠点に近づくほど安全になる

【深度別の初期遭遇率】
上層（深度1）: 開始70% → 終了20%
中層（深度2）: 開始75% → 終了25%
下層（深度3）: 開始80% → 終了30%
深層（深度4）: 開始85% → 終了35%
深淵（深度5）: 帰還不可能

【遭遇率の減少式】
現在遭遇率 = 初期遭遇率 - (進行度% × 減少係数)

減少係数:
- 深度1: 0.5  (70% → 20% で50%減少)
- 深度2: 0.5  (75% → 25% で50%減少)
- 深度3: 0.5  (80% → 30% で50%減少)
- 深度4: 0.5  (85% → 35% で50%減少)

例: 深度3で帰還を50%進めた場合
現在遭遇率 = 80% - (50% × 0.5) = 55%
```

### 4.2.2 帰還戦闘の特徴

```
【弱体化された敵】
HP: 70%
攻撃力: 70%
報酬Gold: 50%
報酬魔石: 50%
報酬魂: 50%（NEW）
技習得: 発生しない

【特別ルール】
- エリート敵は再出現しない
- ボスは再出現しない
- 倒した敵と同じ種類が出る
- 遭遇判定は部屋ごとに個別実行
```

---

## 4.3 帰還ルート選択時の処理

```typescript
/**
 * 帰還ルート開始処理
 */
interface ReturnRoute {
  totalRooms: number;
  currentProgress: number; // 進行度 0〜100%
  currentEncounterRate: number; // 現在の遭遇率 %
  passedRooms: number; // 通過済み部屋数
  encountersCount: number; // 遭遇した戦闘数
  additionalSoulsEarned: number; // NEW: 帰還中に追加で獲得した魂
}

/**
 * 深度別の遭遇率設定
 */
interface EncounterRateConfig {
  initialRate: number; // 初期遭遇率
  finalRate: number; // 最終遭遇率
  decreaseCoef: number; // 減少係数
}

const ENCOUNTER_RATES: Record<number, EncounterRateConfig> = {
  1: { initialRate: 70, finalRate: 20, decreaseCoef: 0.5 },
  2: { initialRate: 75, finalRate: 25, decreaseCoef: 0.5 },
  3: { initialRate: 80, finalRate: 30, decreaseCoef: 0.5 },
  4: { initialRate: 85, finalRate: 35, decreaseCoef: 0.5 },
};

/**
 * 帰還ルート開始
 */
function startReturnRoute(
  currentDepth: number,
  roomsPassed: number
): ReturnRoute | null {
  // 深度5（深淵）チェック
  if (currentDepth === 5) {
    return null; // 帰還不可能
  }

  const config = ENCOUNTER_RATES[currentDepth];

  return {
    totalRooms: roomsPassed,
    currentProgress: 0,
    currentEncounterRate: config.initialRate,
    passedRooms: 0,
    encountersCount: 0,
    additionalSoulsEarned: 0, // NEW
  };
}

/**
 * 現在の遭遇率を計算
 */
function calculateCurrentEncounterRate(
  depth: number,
  progressPercent: number
): number {
  const config = ENCOUNTER_RATES[depth];

  // 現在遭遇率 = 初期遭遇率 - (進行度% × 減少係数)
  const reduction = progressPercent * config.decreaseCoef;
  const currentRate = config.initialRate - reduction;

  // 最終遭遇率以下にはならない
  return Math.max(currentRate, config.finalRate);
}

/**
 * 部屋ごとの遭遇判定
 */
function checkEncounter(depth: number, progressPercent: number): boolean {
  const encounterRate = calculateCurrentEncounterRate(depth, progressPercent);
  const roll = Math.random() * 100;

  return roll < encounterRate;
}

/**
 * 帰還ルート進行処理
 */
function advanceReturnRoute(
  route: ReturnRoute,
  depth: number
): {
  route: ReturnRoute;
  encounterOccurred: boolean;
} {
  route.passedRooms++;
  route.currentProgress = (route.passedRooms / route.totalRooms) * 100;
  route.currentEncounterRate = calculateCurrentEncounterRate(
    depth,
    route.currentProgress
  );

  // 遭遇判定
  const encounterOccurred = checkEncounter(depth, route.currentProgress);

  if (encounterOccurred) {
    route.encountersCount++;
  }

  return { route, encounterOccurred };
}

/**
 * 帰還戦闘の敵生成
 */
function generateReturnEnemy(originalEnemy: Character): Character {
  return {
    ...originalEnemy,
    hp: Math.floor(originalEnemy.hp * 0.7),
    maxHp: Math.floor(originalEnemy.maxHp * 0.7),
    attack: Math.floor(originalEnemy.attack * 0.7),
    rewardGold: Math.floor(originalEnemy.rewardGold * 0.5),
    rewardStones: Math.floor(originalEnemy.rewardStones * 0.5),
    rewardSouls: Math.floor(originalEnemy.rewardSouls * 0.5), // NEW
    canLearnSkill: false, // 技習得なし
  };
}

/**
 * 帰還完了時の報酬計算
 */
function completeReturnRoute(
  player: Character,
  currentDepth: number,
  defeatedEnemies: number,
  soulsEarnedThisRun: number, // NEW: この探索で獲得した魂
  additionalSoulsFromReturn: number // NEW: 帰還中に追加獲得した魂
): ReturnResult {
  // 報酬は100%獲得
  const fullReward = calculateBaseReward(currentDepth, defeatedEnemies);

  // 魂の残滓の合計 (NEW)
  const totalSouls = soulsEarnedThisRun + additionalSoulsFromReturn;

  // 装備・熟練度は全保持
  savePlayerState(player);

  // 魂の残滓を累計に加算 (NEW)
  addToTotalSouls(totalSouls);

  return {
    success: true,
    type: "return_route",
    rewards: {
      ...fullReward,
      souls: totalSouls, // 探索分 + 帰還戦闘分
    },
    message: "無事に拠点へ帰還しました",
  };
}
```

---

## 4.4 帰還ルートの UI 表示

```
【マップ画面での表示】

┌─────────────────────────┐
│  帰還ルート選択          │
├─────────────────────────┤
│                         │
│ 現在地: 下層 8部屋目     │
│ 拠点まで: 15部屋         │
│                         │
│ 現在の遭遇率: 80%        │
│ 最終的な遭遇率: 30%      │
│                         │
│ ▼遭遇率の推移            │
│ 開始時 ████████ 80%     │
│ 中盤頃 █████░░░ 55%     │
│ 終盤頃 ███░░░░░ 30%     │
│                         │
│ 報酬倍率: 100%          │
│ 装備保持: あり          │
│ 追加の魂: 獲得可能      │  ← NEW
│                         │
│ ※深淵（深度5）では      │
│   帰還できません         │
│                         │
│ [帰還開始] [キャンセル]  │
│                         │
└─────────────────────────┘
```

---

# 5. 深淵（深度 5）の特別ルール

## 5.1 生還不可能の設計思想

```
【コンセプト】
深淵は最終試練
→ 「ボスを倒すか死ぬか」の二択のみ

【意図】
- 最高の緊張感を提供
- 真の覚悟を試す
- 報酬のリスクを最大化
- 探索回数制限との相乗効果（NEW）
```

## 5.2 深淵での制限

```
【禁止事項】
✗ 帰還ルート使用不可
✗ 全種類の転移石無効
✗ 途中撤退不可能

【深淵突入前の警告】
「深淵へ踏み込むと、生還の手段は全て失われます。
 ボスを倒すか、死ぬか――選択肢は二つのみです。

 探索回数: 残り X回

 本当に進みますか？」

[はい - 覚悟を決めた] [いいえ - まだ早い]
```

## 5.3 深淵突入時の処理

```typescript
/**
 * 深淵突入確認
 */
function confirmEnterAbyss(remainingExplorations: number): boolean {
  const warning = `
    ⚠️ 警告 ⚠️
    
    深淵へ踏み込むと、生還の手段は全て失われます。
    
    - 帰還ルート: 使用不可
    - 転移石（全種）: 無効化
    - 途中撤退: 不可能
    
    ボスを倒すか、死ぬか――選択肢は二つのみです。
    
    探索回数: 残り ${remainingExplorations}回
    
    本当に進みますか？
  `;

  return confirm(warning);
}

/**
 * 転移石/帰還の可否チェック
 */
function canReturn(currentDepth: number): {
  canUseTeleport: boolean;
  canUseReturnRoute: boolean;
  reason?: string;
} {
  if (currentDepth === 5) {
    return {
      canUseTeleport: false,
      canUseReturnRoute: false,
      reason: "深淵では全ての生還手段が無効化されています",
    };
  }

  return {
    canUseTeleport: true,
    canUseReturnRoute: true,
  };
}

/**
 * 深淵での転移石使用試行
 */
function attemptTeleportInAbyss(): void {
  showMessage(`
    転移石が砕け散った...
    深淵の魔力が転移魔法を無効化している。
    もはや戻る道はない。
  `);

  // 転移石は消費されない（使用不可のため）
}
```

## 5.4 深淵のゲームデザイン意義

```
【戦略的意義】
1. リスク・リターンの極限
   - 最高報酬だが逃げ場なし
   - 完全な準備が必要

2. プレイヤースキルの試練
   - 装備管理の完璧さ
   - 戦闘技術の熟練
   - リソース配分の正確さ

3. 段階的な挑戦
   - 初心者: 深度3までで撤退
   - 中級者: 深度4で転移石使用
   - 上級者: 深淵へ突入

4. 達成感の最大化
   - 深淵クリアは真の栄誉
   - 称号・特別報酬

5. 探索回数制限との統合（NEW）
   - 残り回数が少ないと深淵突入は賭け
   - 失敗すると次のチャンスがない
   - 慎重な判断が必要
```

---

# 6. 生還時の報酬計算

## 6.1 基礎報酬の計算式

```typescript
/**
 * 基礎報酬計算
 */
interface BaseReward {
  gold: number;
  stones: MagicStones;
}

interface MagicStones {
  tiny: number; // 極小
  small: number; // 小
  medium: number; // 中
  large: number; // 大
  huge: number; // 極大
}

function calculateBaseReward(
  currentDepth: number,
  defeatedEnemies: number
): BaseReward {
  // 深度ボーナス
  const depthMultiplier: Record<number, number> = {
    1: 1.0,
    2: 1.5,
    3: 2.5,
    4: 4.0,
    5: 7.0,
  };

  const depthBonus = depthMultiplier[currentDepth] || 1.0;

  // Gold計算
  const gold = Math.floor(currentDepth * 50 + defeatedEnemies * 10);

  // 魔石計算（深度により種類が変化）
  const stones = calculateStoneRewards(currentDepth, defeatedEnemies);

  return { gold, stones };
}

function calculateStoneRewards(depth: number, enemies: number): MagicStones {
  const base = Math.floor(enemies / 3);

  const distribution: Record<number, MagicStones> = {
    1: { tiny: base * 4, small: 0, medium: 0, large: 0, huge: 0 },
    2: { tiny: base, small: base * 3, medium: 0, large: 0, huge: 0 },
    3: { tiny: base, small: base, medium: base * 2, large: 0, huge: 0 },
    4: { tiny: 0, small: base, medium: base * 2, large: base, huge: 0 },
    5: { tiny: 0, small: 0, medium: base, large: base * 2, huge: 1 },
  };

  return distribution[depth] || distribution[1];
}
```

---

## 6.2 魂の残滓の計算（V2.0 - 大幅変更）

**重要な変更点:**

```
旧設計:
- 死亡時に魂を獲得
- 生還時は方法により減少

新設計:
- 魔物撃破時に魂を獲得（経験値的）
- 生還時に累計に加算（方法により減少）
- 死亡時はその探索分ゼロ（累計は保持）
```

### 6.2.1 魂の獲得タイミング

```typescript
/**
 * 魔物撃破時に魂を獲得
 */
interface SoulGainSystem {
  // 戦闘中に加算される「この探索での獲得魂」
  currentRunSouls: number;

  // 過去の探索で蓄積した「累計魂」
  totalSouls: number;
}

/**
 * 魔物撃破時の魂獲得
 */
function defeatEnemy(enemy: Character): number {
  // 敵の種類により魂の量が異なる
  const soulsByType: Record<EnemyType, number> = {
    minion: 5, // 雑魚
    elite: 15, // 強敵
    boss: 50, // ボス
  };

  const soulsGained = soulsByType[enemy.type] || 5;

  // 「この探索での獲得魂」に加算
  currentRunSouls += soulsGained;

  return soulsGained;
}

/**
 * 帰還戦闘での魂獲得（50%）
 */
function defeatReturnEnemy(enemy: Character): number {
  const baseSouls = enemy.rewardSouls;
  const reducedSouls = Math.floor(baseSouls * 0.5);

  // 「この探索での獲得魂」に加算
  currentRunSouls += reducedSouls;

  return reducedSouls;
}
```

### 6.2.2 生還時の魂の処理

```typescript
/**
 * 生還時の魂の残滓計算
 */
function calculateSoulReward(
  soulsEarnedThisRun: number,
  returnMethod: ReturnMethod
): number {
  const multipliers: Record<ReturnMethod, number> = {
    return_route: 1.0, // 100%
    teleport_normal: 0.7, // 70%
    teleport_blessed: 0.8, // 80%
    teleport_emergency: 0.6, // 60%
  };

  const multiplier = multipliers[returnMethod];
  const finalSouls = Math.floor(soulsEarnedThisRun * multiplier);

  return finalSouls;
}

/**
 * 生還完了処理
 */
function completeReturn(
  player: Character,
  soulsEarnedThisRun: number,
  returnMethod: ReturnMethod
): void {
  // 生還方法による魂の減少
  const finalSouls = calculateSoulReward(soulsEarnedThisRun, returnMethod);

  // 累計魂に加算
  player.totalSouls += finalSouls;

  // この探索での獲得魂をリセット
  currentRunSouls = 0;

  // Sanctuaryで使用可能なスキルポイントを更新
  updateAvailableSkillPoints(player.totalSouls);
}
```

### 6.2.3 死亡時の魂の処理

```typescript
/**
 * 死亡時の処理
 */
function handleDeath(player: Character, soulsEarnedThisRun: number): void {
  // この探索で獲得した魂はゼロに
  currentRunSouls = 0;

  // ただし、累計魂は保持される
  // player.totalSouls はそのまま

  // 装備・Gold・魔石は全ロスト
  player.equipment = [];
  player.gold = 0;
  player.magicStones = { tiny: 0, small: 0, medium: 0, large: 0, huge: 0 };

  // BaseCampに保管している装備・カード・過去の累計魂は保持
}
```

---

## 6.3 生還方法別の報酬倍率

```
【最終報酬の計算式】

最終報酬 = 基礎報酬 × 生還方法倍率

【生還方法倍率】
帰還ルート: 1.0（100%）
通常転移石: 0.7（70%）
祝福転移石: 0.8（80%）
緊急転移石: 0.6（60%）

【魂の残滓の計算（V2.0）】
生還時:
- 帰還ルート: この探索で獲得した魂 × 1.0 → 累計に加算
- 通常転移石: この探索で獲得した魂 × 0.7 → 累計に加算
- 祝福転移石: この探索で獲得した魂 × 0.8 → 累計に加算
- 緊急転移石: この探索で獲得した魂 × 0.6 → 累計に加算

死亡時:
- この探索で獲得した魂 → ゼロ
- 累計魂 → 保持（変化なし）

計算例:
この探索で獲得した魂: 100個
過去の累計魂: 500個

【生還の場合】
帰還ルート: 100個加算 → 累計600個
通常転移石: 70個加算 → 累計570個
祝福転移石: 80個加算 → 累計580個
緊急転移石: 60個加算 → 累計560個

【死亡の場合】
この探索分: 0個
累計: 500個（変化なし）
```

---

# 7. 探索回数制限との関係（NEW）

## 7.1 探索回数制限の基本ルール

```
【制限の仕組み】
- 探索回数制限: 10回（デフォルト）
- 生還・死亡にかかわらず、探索1回 = カウント +1
- 10回以内に深度5のボスを撃破 → 成功
- 10回使い果たす → ゲームオーバー

【探索1回のカウント】
Dungeonに入る → 生還 or 死亡 → 探索回数 +1
```

## 7.2 生還システムと探索回数の関係

```
【生還の戦略的価値の向上】

探索回数が残り少ない状況では:
- 死亡のリスクが極めて高い
- 確実に魂を持ち帰る必要がある
- 転移石の価値が上がる

例: 残り探索回数が2回の場合
選択肢A: 深層まで進んで大量の魂を狙う
→ 死亡すると魂ゼロ、残り1回で深層到達困難

選択肢B: 中層で確実に生還し魂を蓄積
→ Sanctuaryで強化、残り1回で深層に挑む
```

## 7.3 生還方法の選択と探索回数

```
【残り回数別の推奨戦略】

残り7〜10回:
- 帰還ルート推奨（最大報酬）
- 死亡してもリカバリー可能
- 追加の魂獲得を狙う

残り4〜6回:
- 状況判断
- 深層なら転移石使用
- 浅層なら帰還ルート

残り1〜3回:
- 転移石必須（祝福 or 緊急）
- 絶対に死亡を避ける
- 確実に魂を持ち帰る
- Sanctuaryで最終強化
```

## 7.4 深淵突入の判断

```
【深淵突入の条件】

推奨条件:
- 探索回数: 残り2回以上
- 装備: 最高レベル（Lv3）
- Sanctuary: 主要スキル解放済み
- 消耗品: 十分な在庫

残り1回での深淵:
- 最後の挑戦
- 失敗 = ゲームオーバー
- 成功 = 完全クリア

警告表示:
「深淵へ突入します。
 探索回数: 残り 1回

 失敗すると次のチャンスはありません。
 本当に進みますか？」
```

---

# 8. 戦略的意義とバランス

## 8.1 各方法の使い分け

```
【帰還ルート】
推奨状況:
- 装備耐久度 > 40%
- HP > 60%
- 消耗品に余裕あり
- 深度1〜3での探索中
- 探索回数に余裕あり（残り5回以上）
→ 最大報酬を狙える

【通常転移石（70%）】
推奨状況:
- 装備耐久度 20〜40%
- HP 40〜60%
- レア装備を入手
- 時間を節約したい
- 探索回数が中盤（残り4〜6回）
→ 安全に資源確保

【祝福転移石（80%）】
推奨状況:
- 高価値な装備/魔石を所持
- 報酬を多く持ち帰りたい
- 深度3〜4での探索後
- 探索回数が少ない（残り2〜3回）
→ バランス型

【緊急転移石（60%）】
推奨状況:
- 戦闘中にHP危機
- ボス戦で形勢不利
- 即座の脱出が必要
- 探索回数が残り1回
→ 最後の逃げ道

【深淵での戦略】
深度5に入る前:
- 万全の準備を整える
- 全ての転移石が無効化
- 探索回数を確認
- 覚悟を決めてから突入
→ 勝利か死か
```

---

## 8.2 リスク・リターンのバランス

```
【報酬とリスクのトレードオフ】

帰還ルート:
- 報酬: 100%（最大）
- リスク: 道中戦闘3〜7回
- 追加利益: 熟練度+15〜25回分、追加の魂
- 探索回数の価値: 最大化
→ 時間とリスクを取って最大報酬

通常転移石:
- 報酬: 70%
- リスク: なし
- 損失: Gold30%、魔石30%、魂30%
- 探索回数の価値: 中程度
→ 安全重視、時間節約

祝福転移石:
- 報酬: 80%
- リスク: なし
- 損失: Gold20%、魔石20%、魂20%
- 探索回数の価値: 高い
→ バランス型、比較的高い保持率

緊急転移石:
- 報酬: 60%
- リスク: なし
- 損失: Gold40%、魔石40%、魂40%
- 探索回数の価値: 命と装備を守る
→ 緊急時の保険、大幅な損失

【深度別の推奨戦略】

深度1〜2:
- 帰還ルート推奨
- 遭遇率70〜75%だが敵は弱い
- 熟練度・魂稼ぎのチャンス

深度3:
- 状況判断
- 装備良好 + 探索回数余裕 → 帰還ルート
- 消耗激しい or 探索回数少ない → 祝福転移石

深度4:
- 転移石推奨
- 遭遇率85%は高リスク
- エリート敵の可能性
- 探索回数が少ないなら必須

深度5（深淵）:
- 生還手段なし
- 事前に深度4で撤退を検討
- 探索回数を確認
- 突入は覚悟が必要
```

---

## 8.3 リスク管理の学習曲線

```
【序盤（1〜5ラン）】
学習目標: 撤退タイミングの把握
→ 転移石を使って安全に帰還
→ 探索回数の感覚を掴む

【中盤（10〜20ラン）】
学習目標: 帰還ルートの活用
→ 弱体化敵を倒しつつ撤退
→ 魂の獲得を最大化

【後半（30ラン〜）】
学習目標: ギリギリまで粘る
→ 緊急転移石のみ所持
→ 深層・深淵での高効率周回
→ 探索回数を完璧に管理
```

---

# 9. 実装仕様

## 9.1 データ構造

```typescript
/**
 * 生還システムのデータ型
 */
enum ReturnMethod {
  RETURN_ROUTE = "return_route", // 帰還ルート
  TELEPORT_NORMAL = "teleport_normal", // 通常転移石
  TELEPORT_BLESSED = "teleport_blessed", // 祝福転移石
  TELEPORT_EMERGENCY = "teleport_emergency", // 緊急転移石
}

interface ReturnResult {
  success: boolean;
  type: ReturnMethod;
  rewards: {
    gold: number;
    stones: MagicStones;
    souls: number; // この探索で獲得し、累計に加算される魂
  };
  message: string;
  penalties?: {
    goldLoss: number;
    stoneLoss: number;
    soulLoss: number; // 生還方法による魂の減少
  };
}

interface ReturnOption {
  method: ReturnMethod;
  available: boolean;
  rewardMultiplier: number;
  risk: "none" | "low" | "medium" | "high" | "dynamic";
  currentEncounterRate?: number; // 帰還ルート用
  finalEncounterRate?: number; // 帰還ルート用
  reason?: string; // 使用不可の理由
}

/**
 * 深淵チェック
 */
interface AbyssRestriction {
  canUseTeleport: boolean;
  canUseReturnRoute: boolean;
  reason?: string;
}

/**
 * 魂の残滓システム（V2.0）
 */
interface SoulSystem {
  currentRunSouls: number; // この探索で獲得した魂
  totalSouls: number; // 累計魂（過去の探索分）
}

/**
 * 探索回数制限（NEW）
 */
interface ExplorationLimit {
  maxExplorations: number; // 最大探索回数（デフォルト10）
  currentExplorations: number; // 現在の探索回数
  remainingExplorations: number; // 残り探索回数
}
```

---

## 9.2 UI 設計要件

```
【生還メニュー画面】

┌─────────────────────────────┐
│  生還方法を選択              │
├─────────────────────────────┤
│                             │
│ 現在地: 下層（深度3）        │
│ 探索回数: 7 / 10 (残り3回)  │  ← NEW
│                             │
│ この探索での獲得魂: 85個    │  ← NEW
│                             │
│ 1. 帰還ルート               │
│    報酬: 100%               │
│    魂: 85個 → 累計に加算    │  ← NEW
│    現在遭遇率: 80%          │
│    最終遭遇率: 30%          │
│    リスク: 中               │
│    [選択]                   │
│                             │
│ 2. 転移石（通常）           │
│    報酬: 70%                │
│    魂: 59個 → 累計に加算    │  ← NEW (85 × 0.7)
│    リスク: なし             │
│    所持: 2個                │
│    [使用]                   │
│                             │
│ 3. 転移石（祝福）           │
│    報酬: 80%                │
│    魂: 68個 → 累計に加算    │  ← NEW (85 × 0.8)
│    リスク: なし             │
│    所持: 1個                │
│    [使用]                   │
│                             │
│ 4. 転移石（緊急）           │
│    報酬: 60%                │
│    魂: 51個 → 累計に加算    │  ← NEW (85 × 0.6)
│    戦闘中でも使用可能       │
│    所持: 0個                │
│    [所持なし]               │
│                             │
│ ⚠️ 深淵（深度5）では        │
│   全ての生還手段が無効      │
│                             │
│ ⚠️ 死亡すると獲得した魂85個 │  ← NEW
│   はゼロになります          │  ← NEW
│                             │
│ [キャンセル]                │
│                             │
└─────────────────────────────┘

【深淵突入前の警告画面】

┌─────────────────────────────┐
│  ⚠️ 深淵への突入 ⚠️          │
├─────────────────────────────┤
│                             │
│ これより先は深淵です。       │
│                             │
│ 深淵では以下が発生します:    │
│                             │
│ ✗ 帰還ルート使用不可         │
│ ✗ 全ての転移石が無効化       │
│ ✗ 途中撤退不可能             │
│                             │
│ ボスを倒すか、死ぬか。       │
│ 選択肢は二つのみです。       │
│                             │
│ 探索回数: 8 / 10 (残り2回)  │  ← NEW
│                             │
│ この探索での獲得魂: 120個   │  ← NEW
│ ※死亡すると全てゼロに       │  ← NEW
│                             │
│ 本当に進みますか？           │
│                             │
│ [覚悟を決めた]              │
│ [まだ早い]                  │
│                             │
└─────────────────────────────┘
```

---

## 9.3 確認ダイアログ

```
【転移石使用確認】

┌─────────────────────────────┐
│  確認                        │
├─────────────────────────────┤
│                             │
│ 通常転移石を使用しますか？   │
│                             │
│ 現在の報酬:                 │
│ - Gold: 500 → 350 (-150)   │
│ - 魔石: 15個 → 10個 (-5)   │
│ - 魂: 85個 → 59個 (-26)    │  ← NEW
│                             │
│ 報酬倍率: 70%               │
│ 装備・熟練度: 全て保持      │
│                             │
│ 探索回数: 7 → 8 (+1)        │  ← NEW
│ 残り回数: 3回 → 2回         │  ← NEW
│                             │
│ [使用する] [キャンセル]     │
│                             │
└─────────────────────────────┘

【帰還ルート確認】

┌─────────────────────────────┐
│  確認                        │
├─────────────────────────────┤
│                             │
│ 帰還ルートで撤退しますか？   │
│                             │
│ 拠点まで: 15部屋            │
│ 現在遭遇率: 80%             │
│ 最終遭遇率: 30%             │
│                             │
│ 報酬: 100%（減少なし）      │
│ 予想戦闘: 8〜10回           │
│                             │
│ 装備・熟練度: 全て保持      │
│ 追加熟練度: 約20回分        │
│ 追加の魂: 約40個            │  ← NEW
│                             │
│ 探索回数: 7 → 8 (+1)        │  ← NEW
│ 残り回数: 3回 → 2回         │  ← NEW
│                             │
│ [開始する] [キャンセル]     │
│                             │
└─────────────────────────────┘
```

---

## 9.4 帰還ルート進行中の UI

```
【帰還ルート画面】

┌─────────────────────────────┐
│  拠点へ帰還中...             │
├─────────────────────────────┤
│                             │
│ 進行度: ████████░░░ 72%     │
│                             │
│ 残り部屋: 4 / 15            │
│ 遭遇戦闘: 6回               │
│                             │
│ 現在地: 中層 3部屋目         │
│                             │
│ 次の部屋の遭遇率: 39%       │
│ ▼▼▼░░░░░░░ 低リスク        │
│                             │
│ この探索での獲得魂: 105個   │  ← NEW
│ （帰還戦闘で +20個）        │  ← NEW
│                             │
│ [次の部屋へ]                │
│                             │
└─────────────────────────────┘

【遭遇発生時】

┌─────────────────────────────┐
│  敵と遭遇！                  │
├─────────────────────────────┤
│                             │
│ 腐敗狼（弱体化）            │
│ HP: 21 / 30 (70%)           │
│ 攻撃: 5 (通常: 7)           │
│ 魂: 2個 (通常: 5個)         │  ← NEW
│                             │
│ ※帰還戦闘のため             │
│   敵の強さは通常の70%       │
│   報酬は50%                 │
│                             │
│ [戦闘開始]                  │
│                             │
└─────────────────────────────┘
```

---

## 9.5 実装チェックリスト

```
□ 転移石アイテムの実装
  □ 通常転移石（70%）
  □ 祝福転移石（80%）
  □ 緊急転移石（60% / 戦闘中可）

□ 深度チェックシステム
  □ 深度5での転移石無効化
  □ 深度5での帰還ルート無効化
  □ 深淵突入前の警告表示

□ 帰還ルートシステム
  □ 動的遭遇率計算
  □ 進行度による遭遇率減少
  □ 部屋ごとの遭遇判定
  □ 敵の弱体化処理

□ 報酬計算システム
  □ 基礎報酬算出（Gold・魔石）
  □ 倍率適用（70% / 80% / 60% / 100%）
  □ 魂の残滓計算（V2.0）
    □ 魔物撃破時に獲得
    □ 生還時に累計に加算
    □ 死亡時はゼロ

□ 探索回数制限システム（NEW）
  □ 探索回数のカウント
  □ 残り回数の表示
  □ ゲームオーバー条件
  □ UI表示の統合

□ UI実装
  □ 生還メニュー
  □ 深淵突入警告
  □ 転移石確認ダイアログ
  □ 帰還ルート確認ダイアログ
  □ 帰還ルート進行画面
  □ 遭遇率の可視化
  □ 報酬減少の可視化
  □ 探索回数の表示（NEW）
  □ 魂の獲得状況の表示（NEW）

□ 統合テスト
  □ 各生還方法の動作確認
  □ 深度5での制限動作確認
  □ 報酬計算の正確性
  □ 装備・熟練度の保持確認
  □ 遭遇率計算の正確性
  □ 魂の残滓システムの動作（V2.0）
  □ 探索回数制限の動作（NEW）
  □ エッジケースの処理
```

---

# 10. まとめ

## 10.1 設計の重要ポイント（V2.0）

```
1. 2段階の生還方法
   - 帰還ルート（リスク変動・報酬最大）
   - 転移石3種（安全・報酬減少）

2. 深淵の絶対制約
   - 深度5では全ての生還手段が無効
   - 「ボスを倒すか死ぬか」の緊張感
   - 事前の準備と覚悟が必須

3. 動的遭遇率システム
   - 帰還を進めるほど安全に
   - 深度が深いほど初期遭遇率が高い
   - プレイヤーに進行状況を可視化

4. 報酬のトレードオフ
   - 安全 vs 報酬のバランス
   - 転移石の種類による選択肢

5. 装備耐久度との連携
   - 耐久度管理が生還判断に影響
   - 戦略的深みの向上
   - リソース管理の重要性

6. 魂の残滓システム（V2.0 - 大幅変更）
   - 魔物撃破時に獲得（経験値的）
   - 生還時に累計に加算（方法により減少）
   - 死亡時はその探索分ゼロ（累計は保持）
   - ローグライト要素を削除

7. 探索回数制限との統合（NEW）
   - 生還・死亡にかかわらずカウント
   - 残り回数が少ないと生還の価値が急上昇
   - 深淵突入の判断に影響
   - 各探索に重みを持たせる
```

## 10.2 プレイヤー体験の設計

```
【初心者（1〜10ラン）】
- 深度1〜2で帰還ルート練習
- 転移石を安全網として使用
- 遭遇率システムの理解
- 魂の獲得方法を理解（NEW）
- 探索回数の感覚を掴む（NEW）

【中級者（11〜30ラン）】
- 深度3〜4まで探索
- 状況判断による生還方法選択
- 装備耐久度との兼ね合い
- 魂の蓄積戦略（NEW）
- 探索回数の効率的管理（NEW）

【上級者（31ラン〜）】
- 深淵への挑戦
- 転移石なしでのギリギリ攻略
- 完璧なリソース管理
- 魂の最大化（NEW）
- 探索回数の完璧な管理（NEW）

【深淵の位置づけ】
- 最終試練
- 真の覚悟が試される場所
- 最高の報酬とリスク
- プレイヤースキルの証明
- 探索回数制限との相乗効果（NEW）
```

## 10.3 他システムとの統合

```
【装備システム】
- 耐久度が生還判断に直結
- 修繕アイテムの戦略的使用

【熟練度システム】
- 帰還ルートで追加経験値
- 転移石使用時の機会損失
- リスク vs 成長のバランス

【報酬システム】
- 深度ボーナス
- 生還方法による倍率
- 魂の残滓の計算（V2.0）

【戦闘システム】
- シールド持ち越しが帰還に影響
- 装備破損リスクの管理
- HP管理の重要性

【Sanctuaryシステム（NEW）】
- 魂の残滓で恒久強化
- 探索回数 +1 のスキル解放
- 生還の価値をさらに高める

【探索回数制限システム（NEW）】
- 生還・死亡の戦略的重要性
- 各探索の重み
- ゲーム全体の緊張感
```

---

**END OF DOCUMENT - V2.0**

このドキュメントは以下の大幅な変更を反映しています：

- 魂の残滓獲得タイミングを「死亡時」→「魔物撃破時（経験値）」に変更
- 死亡時の魂の処理を「通常獲得」→「その探索分ゼロ（累計は保持）」に変更
- 探索回数制限システムの追加
- UI の更新（探索回数表示、魂の獲得状況表示）
- ローグライト要素の完全削除

新しいゲームデザイン（持ち帰り型ダンジョン RPG）に完全に統合され、実装可能な仕様となっています。
