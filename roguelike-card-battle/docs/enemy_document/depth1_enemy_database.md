# 深度1（腐食 - Corruption）敵データベース (Ver 4.0)

**更新日:** 2025-12-31
**ステータス:** 設計完了

## 設計コンセプト

```
【難易度方針】
- 初見プレイヤーは最後のボスまで辿り着けない想定
- 数ターンでじわじわAPを削る火力
- 7〜10回のバトルで装備耐久度が危険域に
- フロアボスで初心者はギリギリ負ける難易度

【世界観：腐食】
「腐食を身に纏えば」
肉体の崩壊が始まる階層
ダークファンタジー、硬派な名称と見た目
```

---

## Ver 4.0 敵データ仕様

### 必須フィールド
```typescript
interface Enemy {
  id: string;           // 敵ID
  nameJa: string;       // 日本語名称
  maxHp: number;        // 最大HP
  maxAp: number;        // 最大AP（敵は通常0）
  startingGuard: number; // 開始時Guard

  // Ver 4.0 新規フィールド
  baseEnemyEnergy: number;  // 行動回数（1ターン当たり）
  speed: number;            // 行動速度（プレイヤー基本50）

  aiPatterns: EnemyAIPattern[];  // 行動パターン
}

interface EnemyAction {
  name: string;
  type: "attack" | "buff" | "debuff" | "special";
  baseDamage: number;

  // Ver 4.0 新規フィールド
  displayIcon: string;   // UI表示用アイコン
  priority: number;      // 行動優先度（0-10）
  energyCost: number;    // エナジーコスト（デフォルト1）

  hitCount?: number;     // 連撃回数
  guardGain?: number;    // Guard付与量
  applyDebuffs?: BuffDebuff[];  // 付与するデバフ
}
```

---

## 想定バランス値

### プレイヤー初期ステータス（推定）
```
HP: 80
AP: 60〜80（コモン装備想定）
Guard: 0（戦闘開始時）
基本速度: 50
初期デッキ: 物理攻撃・防御・回復の基本構成
```

### 敵の火力目標
```
【通常敵】
1戦あたりAPダメージ: 8〜15
（Guard無しの場合、3〜4ターンでAP削り切る）

【複数敵】
総ダメージ: 12〜20
（分散攻撃で防御が難しい）

【フロアボス】
APダメージ: 15〜25/ターン
総ダメージ: 60〜100（4〜6ターン想定）
```

---

## 通常敵（単体）

### 1. 腐敗の野犬 (CORRUPTED_HOUND)
```
【基本情報】
ID: depth1_hound
HP: 28
AP: 0
Guard: 0
baseEnemyEnergy: 1
speed: 40

【外見】
腐肉が露出した痩せこけた黒い野犬。
骨が透けて見え、緑色の瘴気を纏う。

【行動パターン】
ターン1: 噛みつき
  - name: "噛みつき"
  - type: "attack"
  - baseDamage: 7
  - displayIcon: "⚔️"
  - priority: 0
  - energyCost: 1

ターン2: 腐肉の牙
  - name: "腐肉の牙"
  - type: "attack"
  - baseDamage: 7
  - displayIcon: "🦷"
  - priority: 1
  - energyCost: 1
  - applyDebuffs: [{ type: "poison", stacks: 1, duration: 2 }]

ターン3〜: ターン1,2をランダム（各50%）

【戦闘想定】
3〜4ターンで撃破可能
合計被ダメージ: 14〜21
毒込み: 20〜27

【ドロップ】
魔石（極小）: 100%
資金: 5〜7G
カード獲得率: 40%
```

### 2. 変異した腐食鴉 (MUTATED_CROW)
```
【基本情報】
ID: depth1_crow
HP: 22
AP: 0
Guard: 0
baseEnemyEnergy: 2  ※2回行動
speed: 55

【外見】
羽が抜け落ちた灰色の鴉。
目は白濁し、嘴から酸性の唾液が滴る。

【行動パターン】
メイン行動: 連続啄み
  - name: "連続啄み"
  - type: "attack"
  - baseDamage: 5
  - hitCount: 1
  - displayIcon: "🐦"
  - priority: 0
  - energyCost: 1

サブ行動（20%確率）: 弱体化の唾液
  - name: "弱体化の唾液"
  - type: "debuff"
  - baseDamage: 3
  - displayIcon: "💧"
  - priority: 1
  - energyCost: 1
  - applyDebuffs: [{ type: "weak", stacks: 1, duration: 2 }]

【戦闘想定】
2〜3ターンで撃破
合計被ダメージ: 15〜25
弱体化発動時: 与ダメージ-30%

【特徴】
低HPだが2回行動で手数が多い
速度55でプレイヤーより先に行動しやすい

【ドロップ】
魔石（極小）: 100%
資金: 5〜7G
カード獲得率: 35%
```

### 3. 骨の徘徊者 (BONE_WANDERER)
```
【基本情報】
ID: depth1_skeleton
HP: 32
AP: 0
Guard: 0
baseEnemyEnergy: 1
speed: 35
特性: 出血無効

【外見】
白骨化した人型の骸骨。
錆びた剣を引きずり、無言で歩む。

【行動パターン】
ターン1-2: 骨の剣
  - name: "骨の剣"
  - type: "attack"
  - baseDamage: 6
  - displayIcon: "⚔️"
  - priority: 0
  - energyCost: 1

ターン3: 骨砕き
  - name: "骨砕き"
  - type: "attack"
  - baseDamage: 10
  - displayIcon: "💀"
  - priority: 2
  - energyCost: 1
  - applyDebuffs: [{ type: "slow", stacks: 1, duration: 1 }]

ターン4〜: ターン1-3をループ

【戦闘想定】
4〜5ターンで撃破
合計被ダメージ: 18〜30
スロウ込み: エナジー-1で手札事故を誘発

【特徴】
骨砕きのタイミングが読みやすい
速度35で遅い（プレイヤー先攻取りやすい）

【ドロップ】
魔石（極小）: 100%
資金: 6〜8G
カード獲得率: 40%
```

### 4. 影の這いずり (SHADOW_CRAWLER)
```
【基本情報】
ID: depth1_shadow
HP: 25
AP: 0
Guard: 0
baseEnemyEnergy: 1
speed: 50
特性: 回避率15%

【外見】
黒い霧状の人型生物。
実体が曖昧で、地を這うように移動する。

【行動パターン】
ターン1: 影の触手
  - name: "影の触手"
  - type: "attack"
  - baseDamage: 8
  - displayIcon: "👤"
  - priority: 0
  - energyCost: 1

ターン2: 闇の侵食
  - name: "闇の侵食"
  - type: "attack"
  - baseDamage: 6
  - displayIcon: "🌑"
  - priority: 1
  - energyCost: 1
  - applyDebuffs: [{ type: "weak", stacks: 1, duration: 3 }]

ターン3〜: ターン1,2をランダム

【戦闘想定】
3〜4ターンで撃破
合計被ダメージ: 16〜24
回避運が悪いと長期化

【特徴】
弱体化で火力が落ち、戦闘が長引く
速度50でプレイヤーと同速（同速時プレイヤー先攻）
回避率が地味に厄介

【ドロップ】
魔石（極小）: 100%
資金: 6〜8G
カード獲得率: 40%
```

---

## 複数敵バトル

### 5. 腐肉喰らい×3 (FLESH_EATER)
```
【基本情報（1体あたり）】
ID: depth1_flesh_eater
HP: 18
AP: 0
Guard: 0
baseEnemyEnergy: 1
speed: 45

【外見】
腐敗した肉塊から無数の触手が生えた小型の生物。
群れで行動し、死肉を漁る。

【行動パターン（各個体）】
通常: 触手攻撃
  - name: "触手攻撃"
  - type: "attack"
  - baseDamage: 5
  - displayIcon: "🦑"
  - priority: 0
  - energyCost: 1

HP50%以下: 狂乱
  - name: "狂乱"
  - type: "attack"
  - baseDamage: 7
  - displayIcon: "💢"
  - priority: 2
  - energyCost: 1

【戦闘想定】
総HP: 54
総ダメージ/ターン: 15（通常時）
総ダメージ/ターン: 21（狂乱時）

【戦略】
範囲攻撃が有効
1体ずつ確実に倒す
HP50%以下で火力上昇に注意

【ドロップ】
魔石（極小）: 3個
資金: 10〜15G
カード獲得率: 50%
```

### 6. 錆びた剣士×2 (RUSTY_SWORDSMAN)
```
【基本情報（1体あたり）】
ID: depth1_rusty_knight
HP: 30
AP: 0
Guard: 0
baseEnemyEnergy: 1
speed: 40

【外見】
錆びた鎧を纏った亡霊騎士。
剣を構え、機械的に斬りかかってくる。

【行動パターン（各個体）】
ターン1-2: 斬撃
  - name: "斬撃"
  - type: "attack"
  - baseDamage: 8
  - displayIcon: "⚔️"
  - priority: 0
  - energyCost: 1

ターン3: 二段斬り
  - name: "二段斬り"
  - type: "attack"
  - baseDamage: 6
  - hitCount: 2
  - displayIcon: "⚔️⚔️"
  - priority: 1
  - energyCost: 1

ターン4〜: ループ

【連携行動】
2体生存時: 片方が二段斬りすると、もう片方も次ターン二段斬り確定

【戦闘想定】
総HP: 60
総ダメージ/ターン: 16（通常時）
総ダメージ/ターン: 24（連携時）

【戦略】
連携を防ぐため1体を集中攻撃
二段斬りターンはGuard必須

【ドロップ】
魔石（極小）: 2個
魔石（小）: 30%
資金: 12〜18G
カード獲得率: 55%
```

### 7. 毒蜘蛛の群れ×4 (POISON_SPIDER)
```
【基本情報（1体あたり）】
ID: depth1_spider
HP: 12
AP: 0
Guard: 0
baseEnemyEnergy: 1
speed: 50

【外見】
人の頭ほどの大きさの紫色の蜘蛛。
体液は毒々しく光り、糸を吐く。

【行動パターン（各個体）】
80%: 毒牙
  - name: "毒牙"
  - type: "attack"
  - baseDamage: 4
  - displayIcon: "🕷️"
  - priority: 0
  - energyCost: 1
  - applyDebuffs: [{ type: "poison", stacks: 1, duration: 2 }]

20%: 糸縛り
  - name: "糸縛り"
  - type: "debuff"
  - baseDamage: 2
  - displayIcon: "🕸️"
  - priority: 1
  - energyCost: 1
  - applyDebuffs: [{ type: "slow", stacks: 1, duration: 1 }]

【戦闘想定】
総HP: 48
総ダメージ/ターン: 16
毒込み総ダメージ: 25〜40（スタック次第）

【戦略】
毒スタックが致命的
範囲攻撃で早期殲滅が理想
回復カード必須

【ドロップ】
魔石（極小）: 4個
資金: 15〜20G
カード獲得率: 60%
```

---

## フロアボス：堕ちた番人 (FALLEN_GUARDIAN)

### 基本情報
```
ID: depth1_boss_guardian
名称: 堕ちた番人
HP: 120
AP: 0
Guard: 15（ターン開始時に自動付与）
baseEnemyEnergy: 1 (P1-2) → 2 (P3)
speed: 55

【外見】
かつて上層を守護していた重装の騎士。
漆黒の鎧は腐食し、兜の奥から赤い光が漏れる。
巨大な戦斧を片手で振るう。
```

### フェーズ1（HP100%〜66%）：試練の始まり
```
【行動パターン】
ターン1: 重斬撃
  - name: "重斬撃"
  - type: "attack"
  - baseDamage: 12
  - displayIcon: "⚔️"
  - priority: 0
  - energyCost: 1

ターン2: 防御固め
  - name: "防御固め"
  - type: "buff"
  - baseDamage: 0
  - guardGain: 20
  - displayIcon: "🛡️"
  - priority: 1
  - energyCost: 1

ターン3: 戦斧の一振り
  - name: "戦斧の一振り"
  - type: "attack"
  - baseDamage: 15
  - displayIcon: "🪓"
  - priority: 2
  - energyCost: 1

ターン4〜: ループ

【特徴】
比較的読みやすいパターン
Guard付与で耐久力が高い
ダメージは控えめ
```

### フェーズ2（HP65%〜34%）：腐敗の力
```
【フェーズ移行時】
咆哮: 全体ダメージ10 + 弱体化付与（2ターン）

【行動パターン】
ターン1: 腐敗の斬撃
  - name: "腐敗の斬撃"
  - type: "attack"
  - baseDamage: 12
  - displayIcon: "⚔️"
  - priority: 0
  - energyCost: 1
  - applyDebuffs: [{ type: "bleed", stacks: 1, duration: 2 }]

ターン2: 漆黒の波動
  - name: "漆黒の波動"
  - type: "attack"
  - baseDamage: 8
  - displayIcon: "🌊"
  - priority: 1
  - energyCost: 1
  - applyDebuffs: [
      { type: "poison", stacks: 1, duration: 2 },
      { type: "weak", stacks: 1, duration: 2 }
    ]

ターン3: 戦斧の連撃
  - name: "戦斧の連撃"
  - type: "attack"
  - baseDamage: 10
  - hitCount: 2
  - displayIcon: "🪓🪓"
  - priority: 2
  - energyCost: 1

ターン4〜: ループ

【特徴】
デバフが重なると危険
出血でカード使用がHP損失に
弱体化でGuard戦術が弱体化
毒で持久戦が不利に
```

### フェーズ3（HP33%〜0%）：番人の最期
```
【フェーズ移行時】
絶望の咆哮: 全体ダメージ15 + スロウ付与（2ターン） + Guard-30

【行動回数変更】
baseEnemyEnergy: 2（2回行動）

【行動パターン（1回目）】
50%: 狂乱の斬撃
  - name: "狂乱の斬撃"
  - type: "attack"
  - baseDamage: 18
  - displayIcon: "⚔️"
  - priority: 0
  - energyCost: 1

30%: 腐敗の波動
  - name: "腐敗の波動"
  - type: "attack"
  - baseDamage: 10
  - displayIcon: "🌊"
  - priority: 1
  - energyCost: 1
  - applyDebuffs: [
      { type: "poison", stacks: 1, duration: 2 },
      { type: "bleed", stacks: 1, duration: 2 }
    ]

20%: Guard回復
  - name: "Guard回復"
  - type: "buff"
  - baseDamage: 0
  - guardGain: 25
  - displayIcon: "🛡️"
  - priority: 2
  - energyCost: 1

【行動パターン（2回目）】
70%: 追撃
  - name: "追撃"
  - type: "attack"
  - baseDamage: 12
  - displayIcon: "⚔️"
  - priority: 0
  - energyCost: 1

30%: 戦斧の回転斬り
  - name: "戦斧の回転斬り"
  - type: "attack"
  - baseDamage: 8
  - displayIcon: "🔄"
  - priority: 1
  - energyCost: 1

【特徴】
2回行動で圧倒的火力
スロウでエナジー管理が困難
最も危険なフェーズ
```

### 戦闘総評
```
【想定戦闘ターン数】
6〜10ターン

【合計被ダメージ】
AP: 60〜90（初心者はアーマーブレイク確実）
HP: 20〜40（フェーズ移行 + 貫通ダメージ + デバフ）

【初見難易度】
装備が弱いと勝てない設計
デバフ管理ができないと詰む
APが0になると貫通ダメージで急速にHPが減る

【攻略のカギ】
- フェーズ2で回復カードを温存
- 弱体化中は火力より回復優先
- フェーズ3開始前にHP満タン推奨
- デバフ解除カードがあると有利
```

### ドロップ報酬
```
魔石（極小）: 3個
魔石（小）: 2個
魔石（中）: 1個（50%）
資金: 50〜80G
カード獲得: 確定（レア以上）
装備獲得: 30%（レア装備）
```

---

## バトル回数設計（深度1全体）

### 想定バトル構成（7〜10回）
```
【パターンA（最短7回）】
1. 通常敵×3回
2. 複数敵×2回
3. 休憩/宝箱×1回
4. 通常敵×1回
5. フロアボス×1回

【パターンB（標準9回）】
1. 通常敵×4回
2. 複数敵×2回
3. 休憩/宝箱×2回
4. 通常敵×1回
5. フロアボス×1回

【パターンC（最長10回）】
1. 通常敵×5回
2. 複数敵×2回
3. 休憩/宝箱×2回
4. 通常敵×1回
5. フロアボス×1回
```

### AP損耗計算（装備AP70想定）
```
【戦闘ごとのAP減少】
通常敵（単体）: 10〜15
複数敵: 12〜20
フロアボス: 60〜90

【標準9回パターン】
通常敵5回: 50〜75
複数敵2回: 24〜40
フロアボス: 60〜90
合計: 134〜205

【休憩所2回活用の場合】
装備修繕(+10×6スロット=+60) × 2回 = +120
実質損耗: 14〜85

【結論】
- 休憩所を活用しないとボス前にAP危険域
- 初心者は休憩所の重要性を学ぶ
- 装備選択とリソース管理が重要
```

---

## 敵出現率

### 部屋タイプ別出現率
```
通常敵（単体）: 40%
複数敵: 20%
休憩所: 15%
宝箱: 10%
イベント: 10%
ショップ: 5%
```

### 通常敵出現率（単体）
```
腐敗の野犬: 30%
変異した腐食鴉: 25%
骨の徘徊者: 25%
影の這いずり: 20%
```

### 複数敵出現率
```
腐肉喰らい×3: 40%
錆びた剣士×2: 35%
毒蜘蛛の群れ×4: 25%
```

---

## まとめ

### 深度1の設計意図
```
【学習要素】
1. 基本的な戦闘フロー
2. Guard/AP/HPの3層防御の理解
3. 状態異常への対処
4. リソース管理（HP/AP/カード）
5. 休憩所の重要性

【難易度カーブ】
通常敵: 慣れるための練習
複数敵: 戦略を考える必要性
フロアボス: 初見では勝てない壁

【初心者体験】
1回目: ボス前で死亡（装備喪失）
2回目: 休憩所を活用してボス到達
3回目: デバフ対策を学んでボス撃破
```

---

## 参照関係

```
battle_logic.md (Ver 4.0) [マスター文書]
├── buff_debuff_system.md (Ver 4.0) [バフ/デバフ定義]
└── depth1_enemy_database.md (Ver 4.0) [本文書]
    └── 実装: src/Character/data/EnemyData.ts
```
