# 生還システム完全設計書

## 目次

```
1. 生還システムの概要
2. 生還方法の種類
3. 転移石システム
4. 帰還ルートシステム
5. 深淵（深度5）の特別ルール
6. 生還時の報酬計算
7. 戦略的意義とバランス
8. 実装仕様
9. まとめ
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
```

## 1.2 生還と死亡の違い

| 項目 | 生還 | 死亡 |
|---|---|---|
| 装備 | **全て持ち越し** | 全喪失（レジェンド以外） |
| 耐久度 | **そのまま** | - |
| 熟練度 | **使用回数記録** | 使用回数記録 |
| 魔石 | **方法により減少** | 0 |
| 探索記録 | 記録される | 記録される |
| 魂の残滓 | **方法により減少** | 通常獲得 |
| 装備の痕跡 | **獲得なし** | 獲得 |

**生還の価値:**
- 装備を確実に保持できる
- 熟練度の使用回数が保存される
- 魔石・魂の一部を持ち帰れる

**死亡のペナルティ:**
- 装備の完全喪失（レジェンド以外）
- 収集した魔石・資源が全て失われる
- 魂の残滓は獲得できるが、それ以外は失う

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

| 方法 | 使用条件 | 即時性 | 報酬倍率 | リスク | 推奨場面 |
|---|---|---|---|---|---|
| 通常転移石 | アイテム所持 | **即時** | 70% | なし | 早期撤退 |
| 祝福転移石 | アイテム所持 | **即時** | 80% | なし | 報酬確保 |
| 緊急転移石 | アイテム所持<br>戦闘中可 | **即時** | 60% | なし | 戦闘中脱出 |
| 帰還ルート | 戦闘可能 | 遅い | **100%** | 道中戦闘 | 最大報酬 |

**重要:** 深度5（深淵）では全ての生還手段が無効化される。
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
  defeatedEnemies: number
): ReturnResult {
  
  // 深度5（深淵）チェック
  if (currentDepth === 5) {
    return { 
      success: false, 
      message: "深淵では転移石が無効化されている..." 
    };
  }

  // 戦闘中チェック
  if (isInBattle() && !stone.canUseInBattle) {
    return { 
      success: false, 
      message: "戦闘中は使用できません" 
    };
  }

  // 報酬計算
  const baseReward = calculateBaseReward(currentDepth, defeatedEnemies);
  const finalReward = {
    stones: Math.floor(baseReward.stones * stone.rewardMultiplier),
    souls: Math.floor(baseReward.souls * stone.rewardMultiplier * 0.5) // 魂は更に半減
  };

  // 装備・熟練度は全保持
  savePlayerState(player);

  // 拠点へ帰還
  return {
    success: true,
    type: "teleport",
    rewards: finalReward,
    message: "拠点へ帰還しました"
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

4. 魔石・資金が潤沢
   - 目標リソース達成
   → これ以上のリスク不要

5. ボス戦前の準備不足
   - 消耗品不足
   → 拠点で補給してから再挑戦
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

【デメリット】
- 時間がかかる
- 道中の戦闘リスク
- 装備耐久度の更なる消耗
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
報酬: 50%（魔石のみ）
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
  currentProgress: number;        // 進行度 0〜100%
  currentEncounterRate: number;   // 現在の遭遇率 %
  passedRooms: number;            // 通過済み部屋数
  encountersCount: number;        // 遭遇した戦闘数
}

/**
 * 深度別の遭遇率設定
 */
interface EncounterRateConfig {
  initialRate: number;  // 初期遭遇率
  finalRate: number;    // 最終遭遇率
  decreaseCoef: number; // 減少係数
}

const ENCOUNTER_RATES: Record<number, EncounterRateConfig> = {
  1: { initialRate: 70, finalRate: 20, decreaseCoef: 0.5 },
  2: { initialRate: 75, finalRate: 25, decreaseCoef: 0.5 },
  3: { initialRate: 80, finalRate: 30, decreaseCoef: 0.5 },
  4: { initialRate: 85, finalRate: 35, decreaseCoef: 0.5 }
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
    encountersCount: 0
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
function checkEncounter(
  depth: number,
  progressPercent: number
): boolean {
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
  route: ReturnRoute, 
  encounterOccurred: boolean 
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
    rewardStones: Math.floor(originalEnemy.rewardStones * 0.5),
    canLearnSkill: false // 技習得なし
  };
}

/**
 * 帰還完了時の報酬計算
 */
function completeReturnRoute(
  player: Character,
  currentDepth: number,
  defeatedEnemies: number
): ReturnResult {
  // 報酬は100%獲得
  const fullReward = calculateBaseReward(currentDepth, defeatedEnemies);

  // 装備・熟練度は全保持
  savePlayerState(player);

  return {
    success: true,
    type: "return_route",
    rewards: fullReward,
    message: "無事に拠点へ帰還しました"
  };
}
```

---

## 4.4 帰還ルートのUI表示

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
│                         │
│ ※深淵（深度5）では      │
│   帰還できません         │
│                         │
│ [帰還開始] [キャンセル]  │
│                         │
└─────────────────────────┘
```

---

# 5. 深淵（深度5）の特別ルール

## 5.1 生還不可能の設計思想

```
【コンセプト】
深淵は最終試練
→ 「ボスを倒すか死ぬか」の二択のみ

【意図】
- 最高の緊張感を提供
- 真の覚悟を試す
- 報酬のリスクを最大化
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
 本当に進みますか？」

[はい - 覚悟を決めた] [いいえ - まだ早い]
```

## 5.3 深淵突入時の処理

```typescript
/**
 * 深淵突入確認
 */
function confirmEnterAbyss(): boolean {
  const warning = `
    ⚠️ 警告 ⚠️
    
    深淵へ踏み込むと、生還の手段は全て失われます。
    
    - 帰還ルート: 使用不可
    - 転移石（全種）: 無効化
    - 途中撤退: 不可能
    
    ボスを倒すか、死ぬか――選択肢は二つのみです。
    
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
      reason: "深淵では全ての生還手段が無効化されています"
    };
  }
  
  return {
    canUseTeleport: true,
    canUseReturnRoute: true
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
```

---

# 6. 生還時の報酬計算

## 6.1 基礎報酬の計算式

```typescript
/**
 * 基礎報酬計算
 */
interface BaseReward {
  stones: MagicStones;
  souls: number;
}

interface MagicStones {
  tiny: number;    // 極小
  small: number;   // 小
  medium: number;  // 中
  large: number;   // 大
  huge: number;    // 極大
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
    5: 7.0
  };

  const depthBonus = depthMultiplier[currentDepth] || 1.0;

  // 魔石計算（深度により種類が変化）
  const stones = calculateStoneRewards(currentDepth, defeatedEnemies);

  // 魂の残滓計算
  const souls = Math.floor(currentDepth * 2 + Math.floor(defeatedEnemies / 10));

  return { stones, souls };
}

function calculateStoneRewards(depth: number, enemies: number): MagicStones {
  const base = Math.floor(enemies / 3);
  
  const distribution: Record<number, MagicStones> = {
    1: { tiny: base * 4, small: 0, medium: 0, large: 0, huge: 0 },
    2: { tiny: base, small: base * 3, medium: 0, large: 0, huge: 0 },
    3: { tiny: base, small: base, medium: base * 2, large: 0, huge: 0 },
    4: { tiny: 0, small: base, medium: base * 2, large: base, huge: 0 },
    5: { tiny: 0, small: 0, medium: base, large: base * 2, huge: 1 }
  };

  return distribution[depth] || distribution[1];
}
```

---

## 6.2 生還方法別の報酬倍率

```
【最終報酬の計算式】

最終報酬 = 基礎報酬 × 生還方法倍率

【生還方法倍率】
帰還ルート: 1.0（100%）
通常転移石: 0.7（70%）
祝福転移石: 0.8（80%）
緊急転移石: 0.6（60%）

【魂の残滓の特別ルール】
帰還ルート: 通常獲得（100%）
転移石系: 0.5倍（基礎報酬の半減後、更に倍率適用）

計算例:
基礎魂の残滓: 10個

帰還ルート: 10個（100%）
通常転移石: 10 × 0.5 × 0.7 = 3.5 → 3個
祝福転移石: 10 × 0.5 × 0.8 = 4個
緊急転移石: 10 × 0.5 × 0.6 = 3個
```

---

# 7. 戦略的意義とバランス

## 7.1 各方法の使い分け

```
【帰還ルート】
推奨状況:
- 装備耐久度 > 40%
- HP > 60%
- 消耗品に余裕あり
- 深度1〜3での探索中
→ 最大報酬を狙える

【通常転移石（70%）】
推奨状況:
- 装備耐久度 20〜40%
- HP 40〜60%
- レア装備を入手
- 時間を節約したい
→ 安全に資源確保

【祝福転移石（80%）】
推奨状況:
- 高価値な装備/魔石を所持
- 報酬を多く持ち帰りたい
- 深度3〜4での探索後
→ バランス型

【緊急転移石（60%）】
推奨状況:
- 戦闘中にHP危機
- ボス戦で形勢不利
- 即座の脱出が必要
→ 最後の逃げ道

【深淵での戦略】
深度5に入る前:
- 万全の準備を整える
- 全ての転移石が無効化
- 覚悟を決めてから突入
→ 勝利か死か
```

---

## 7.2 リスク・リターンのバランス

```
【報酬とリスクのトレードオフ】

帰還ルート:
- 報酬: 100%（最大）
- リスク: 道中戦闘3〜7回
- 追加利益: 熟練度+15〜25回分
→ 時間とリスクを取って最大報酬

通常転移石:
- 報酬: 70%
- リスク: なし
- 損失: 魔石30%、魂65%
→ 安全重視、時間節約

祝福転移石:
- 報酬: 80%
- リスク: なし
- 損失: 魔石20%、魂60%
→ バランス型、比較的高い保持率

緊急転移石:
- 報酬: 60%
- リスク: なし
- 損失: 魔石40%、魂70%
→ 緊急時の保険、大幅な損失

【深度別の推奨戦略】

深度1〜2:
- 帰還ルート推奨
- 遭遇率70〜75%だが敵は弱い
- 熟練度稼ぎのチャンス

深度3:
- 状況判断
- 装備良好 → 帰還ルート
- 消耗激しい → 祝福転移石

深度4:
- 転移石推奨
- 遭遇率85%は高リスク
- エリート敵の可能性

深度5（深淵）:
- 生還手段なし
- 事前に深度4で撤退を検討
- 突入は覚悟が必要
```

---

## 7.3 リスク管理の学習曲線

```
【序盤（1〜5ラン）】
学習目標: 撤退タイミングの把握
→ 転移石を使って安全に帰還

【中盤（10〜20ラン）】
学習目標: 帰還ルートの活用
→ 弱体化敵を倒しつつ撤退

【後半（30ラン〜）】
学習目標: ギリギリまで粘る
→ 緊急転移石のみ所持
→ 深層・深淵での高効率周回
```

---

# 8. 実装仕様

## 8.1 データ構造

```typescript
/**
 * 生還システムのデータ型
 */
enum ReturnMethod {
  RETURN_ROUTE = "return_route",          // 帰還ルート
  TELEPORT_NORMAL = "teleport_normal",     // 通常転移石
  TELEPORT_BLESSED = "teleport_blessed",   // 祝福転移石
  TELEPORT_EMERGENCY = "teleport_emergency" // 緊急転移石
}

interface ReturnResult {
  success: boolean;
  type: ReturnMethod;
  rewards: {
    stones: MagicStones;
    souls: number;
  };
  message: string;
  penalties?: {
    stoneLoss: number;
    soulLoss: number;
  };
}

interface ReturnOption {
  method: ReturnMethod;
  available: boolean;
  rewardMultiplier: number;
  risk: "none" | "low" | "medium" | "high" | "dynamic";
  currentEncounterRate?: number; // 帰還ルート用
  finalEncounterRate?: number;   // 帰還ルート用
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
```

---

## 8.2 UI設計要件

```
【生還メニュー画面】

┌─────────────────────────────┐
│  生還方法を選択              │
├─────────────────────────────┤
│                             │
│ 現在地: 下層（深度3）        │
│                             │
│ 1. 帰還ルート               │
│    報酬: 100%               │
│    現在遭遇率: 80%          │
│    最終遭遇率: 30%          │
│    リスク: 中               │
│    [選択]                   │
│                             │
│ 2. 転移石（通常）           │
│    報酬: 70%                │
│    リスク: なし             │
│    所持: 2個                │
│    [使用]                   │
│                             │
│ 3. 転移石（祝福）           │
│    報酬: 80%                │
│    リスク: なし             │
│    所持: 1個                │
│    [使用]                   │
│                             │
│ 4. 転移石（緊急）           │
│    報酬: 60%                │
│    戦闘中でも使用可能       │
│    所持: 0個                │
│    [所持なし]               │
│                             │
│ ⚠️ 深淵（深度5）では        │
│   全ての生還手段が無効      │
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
│ 本当に進みますか？           │
│                             │
│ [覚悟を決めた]              │
│ [まだ早い]                  │
│                             │
└─────────────────────────────┘
```

---

## 8.3 確認ダイアログ

```
【転移石使用確認】

┌─────────────────────────────┐
│  確認                        │
├─────────────────────────────┤
│                             │
│ 通常転移石を使用しますか？   │
│                             │
│ 現在の報酬:                 │
│ - 魔石: 15個 → 10個 (-5)   │
│ - 魂: 8個 → 2個 (-6)       │
│                             │
│ 報酬倍率: 70%               │
│ 装備・熟練度: 全て保持      │
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
│                             │
│ [開始する] [キャンセル]     │
│                             │
└─────────────────────────────┘
```

---

## 8.4 帰還ルート進行中のUI

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
│                             │
│ ※帰還戦闘のため             │
│   敵の強さは通常の70%       │
│                             │
│ [戦闘開始]                  │
│                             │
└─────────────────────────────┘
```

---

## 8.5 実装チェックリスト

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
  □ 基礎報酬算出（魔石・魂）
  □ 倍率適用（70% / 80% / 60% / 100%）
  □ 魂の残滓特別処理（転移石系は半減）

□ UI実装
  □ 生還メニュー
  □ 深淵突入警告
  □ 転移石確認ダイアログ
  □ 帰還ルート確認ダイアログ
  □ 帰還ルート進行画面
  □ 遭遇率の可視化
  □ 報酬減少の可視化

□ 統合テスト
  □ 各生還方法の動作確認
  □ 深度5での制限動作確認
  □ 報酬計算の正確性
  □ 装備・熟練度の保持確認
  □ 遭遇率計算の正確性
  □ エッジケースの処理
```

---

# 9. まとめ

## 9.1 設計の重要ポイント

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
   - 魂の残滓の特別ルール

5. 装備耐久度との連携
   - 耐久度管理が生還判断に影響
   - 戦略的深みの向上
   - リソース管理の重要性
```

## 9.2 プレイヤー体験の設計

```
【初心者（1〜10ラン）】
- 深度1〜2で帰還ルート練習
- 転移石を安全網として使用
- 遭遇率システムの理解

【中級者（11〜30ラン）】
- 深度3〜4まで探索
- 状況判断による生還方法選択
- 装備耐久度との兼ね合い

【上級者（31ラン〜）】
- 深淵への挑戦
- 転移石なしでのギリギリ攻略
- 完璧なリソース管理

【深淵の位置づけ】
- 最終試練
- 真の覚悟が試される場所
- 最高の報酬とリスク
- プレイヤースキルの証明
```

## 9.3 他システムとの統合

```
【装備システム】
- 耐久度が生還判断に直結
- 修繕アイテムの戦略的使用
- レジェンド装備の価値向上

【熟練度システム】
- 帰還ルートで追加経験値
- 転移石使用時の機会損失
- リスク vs 成長のバランス

【報酬システム】
- 深度ボーナス
- 生還方法による倍率
- 魂の残滓の特別計算

【戦闘システム】
- シールド持ち越しが帰還に影響
- 装備破損リスクの管理
- HP管理の重要性
```

---

**END OF DOCUMENT**

このドキュメントは以下の修正を反映しています：
- 緊急脱出システムの完全削除
- 転移石の報酬倍率変更（70% / 80% / 60%）
- ゴールド（資金）関連の記述削除
- 動的遭遇率システムの実装
- 深淵（深度5）の生還不可能設定
- UI表示の更新（遭遇率％表示）

既存のシステム設計と完全に統合され、実装可能な仕様となっています。
