# 深度2（狂乱 - Madness）敵データベース Ver 4.0

> **Ver 4.0 変更履歴**
> - 狂気システム（insanity, confusion, terror）を削除
> - 標準デバフ（weak, slow, stun, poison等）に統一
> - Ver 4.0 敵データフィールド追加（baseEnemyEnergy, speed, displayIcon, priority, energyCost）
> - 重症度システム（Light/Heavy）を廃止、スタック制に統一

## 設計コンセプト

```
【難易度方針】
- 深度1の1.2倍スケーリング
- デバフ累積による圧力
- 精神攻撃の本格化（weak, slow中心）
- 予測困難な行動パターン
- 8〜12回のバトルを想定
- フロアボスはデバフ管理が最重要

【世界観：狂乱】
「腐食を身に纏えば、狂乱に身を置き」
肉体の崩壊の次は、精神の崩壊
正気と狂気の境界が消え、混沌が支配する
```

---

## 想定バランス値

### プレイヤー想定ステータス（深度2到達時）
```
HP: 80（変化なし）
AP: 50〜70（深度1で消耗）
装備: コモン〜レア混在
カード: 初期デッキ + 3〜5枚追加
熟練度: 主力カードLv1〜2
```

### 敵の火力目標（1.2倍スケーリング）
```
【通常敵】
HP: 34〜48（深度1の1.2倍）
攻撃: 8〜12
1戦あたりAPダメージ: 10〜18

【複数敵】
総HP: 65〜85
総ダメージ: 15〜25/ターン

【フロアボス】
HP: 160〜180
APダメージ: 20〜35/ターン
総ダメージ: 80〜140（5〜8ターン想定）
```

---

## 通常敵（単体）

### 1. 狂乱の囚人
```
【基本情報】
id: "depth2_prisoner"
name: "狂乱の囚人"
HP: 40
攻撃力: 10
baseEnemyEnergy: 1
speed: 35

【外見】
ボロボロの囚人服を着た痩せこけた人型。
目は血走り、口から泡を吹きながら笑い続ける。

【行動パターン】
ターン1: 狂乱の爪
  - baseDamage: 10
  - displayIcon: "🔪"
  - priority: 0
  - energyCost: 1

ターン2: 狂った叫び
  - baseDamage: 8
  - debuff: { type: "slow", stacks: 2, duration: 2 }
  - displayIcon: "😱"
  - priority: 0
  - energyCost: 1

ターン3: 自傷攻撃
  - baseDamage: 15
  - selfDamage: 5
  - displayIcon: "💢"
  - priority: 0
  - energyCost: 1

ターン4〜: ターン1-3をランダムループ

【特徴】
行動パターンがランダム
自傷攻撃で火力上昇だが自滅も
slow付与で行動順操作

【戦闘想定】
3〜4ターンで撃破
合計被ダメージ: 18〜33

【ドロップ】
魔石（極小）: 100%
魔石（小）: 30%
資金: 10〜13G
カード獲得率: 40%
```

### 2. 恐怖の影
```
【基本情報】
id: "depth2_shadow"
name: "恐怖の影"
HP: 35
攻撃力: 7
baseEnemyEnergy: 1
speed: 50
特性: 回避率20%

【外見】
黒い霧のような人型で、時々恐ろしい顔が浮かび上がる。
トラウマの具現化であり、見る者に恐怖を与える。

【行動パターン】
ターン1: 恐怖の囁き
  - baseDamage: 7
  - debuff: { type: "weak", stacks: 2, duration: 2 }
  - displayIcon: "👻"
  - priority: 0
  - energyCost: 1

ターン2: 悪夢の幻影
  - baseDamage: 5
  - debuff: { type: "weak", stacks: 3, duration: 3 }
  - displayIcon: "🌑"
  - priority: 0
  - energyCost: 1

ターン3: 闇の触手
  - baseDamage: 12
  - displayIcon: "🦑"
  - priority: 0
  - energyCost: 1

ターン4〜: ターン1-3をループ

【特徴】
weak付与で攻撃力低下
回避率が地味に厄介
長期戦になりやすい

【戦闘想定】
4〜5ターンで撃破（回避率のため）
合計被ダメージ: 15〜29

【ドロップ】
魔石（極小）: 100%
魔石（小）: 35%
資金: 10〜13G
カード獲得率: 40%
```

### 3. 精神崩壊者
```
【基本情報】
id: "depth2_broken_mind"
name: "精神崩壊者"
HP: 45
攻撃力: 9
baseEnemyEnergy: 1〜2（ランダム）
speed: 40

【外見】
かつては探索者だった人間の成れの果て。
自我が崩壊し、混沌に支配されている。

【行動パターン】
完全ランダム（以下から毎ターン選択）:
- 精神攻撃
  - baseDamage: 9
  - debuff: { type: "atkDown", stacks: 1, duration: 2 }
  - displayIcon: "🧠"
  - priority: 0
  - energyCost: 1

- 狂乱の連撃
  - baseDamage: 6
  - hitCount: 2
  - displayIcon: "⚔️"
  - priority: 0
  - energyCost: 1

- 絶叫
  - baseDamage: 8
  - isAoe: true
  - debuff: { type: "slow", stacks: 2, duration: 2 }
  - displayIcon: "📢"
  - priority: 0
  - energyCost: 1

- 自己強化
  - buff: { type: "atkUp", stacks: 2, duration: 1 }
  - displayIcon: "💪"
  - priority: 0
  - energyCost: 1

- 何もしない（行動不能）
  - displayIcon: "💤"
  - priority: 0
  - energyCost: 0

【特徴】
完全に予測不能
「何もしない」ターンは回復チャンス

【戦闘想定】
3〜5ターンで撃破（行動次第）
合計被ダメージ: 18〜40

【ドロップ】
魔石（極小）: 100%
魔石（小）: 40%
資金: 11〜14G
カード獲得率: 45%
```

### 4. 呪いの伝道者
```
【基本情報】
id: "depth2_cultist"
name: "呪いの伝道者"
HP: 42
攻撃力: 8
baseEnemyEnergy: 1
speed: 45

【外見】
黒いローブを纏い、狂気を説く異端の司祭。
目は虚ろで、口から意味不明な言葉を吐き続ける。

【行動パターン】
ターン1: 呪いの説法
  - baseDamage: 8
  - debuff: { type: "curse", stacks: 2, duration: 3 }
  - displayIcon: "📖"
  - priority: 0
  - energyCost: 1

ターン2: 精神侵食
  - baseDamage: 6
  - debuff: { type: "weak", stacks: 2, duration: 2 }
  - displayIcon: "🕯️"
  - priority: 0
  - energyCost: 1

ターン3: 伝道の儀式
  - guard: 20
  - debuff: { type: "slow", stacks: 1, duration: 2 }
  - displayIcon: "🛡️"
  - priority: 0
  - energyCost: 1

ターン4〜: ターン1-3をループ

【特徴】
curse付与で持続ダメージ
Guard付与で耐久力あり
放置するとデバフ累積

【戦闘想定】
3〜4ターンで撃破
合計被ダメージ: 14〜28 + curse持続

【ドロップ】
魔石（極小）: 100%
魔石（小）: 45%
資金: 11〜15G
カード獲得率: 45%
```

### 5. 幻影の獣
```
【基本情報】
id: "depth2_phantom_beast"
name: "幻影の獣"
HP: 38
攻撃力: 11
baseEnemyEnergy: 1
speed: 55
特性: 幻影（攻撃が15%の確率で無効化）

【外見】
実体が曖昧な獣の姿。
幻覚なのか現実なのか判別不能。

【行動パターン】
ターン1: 幻影の爪
  - baseDamage: 11
  - displayIcon: "🐺"
  - priority: 0
  - energyCost: 1

ターン2: 現実歪曲
  - baseDamage: 8
  - debuff: { type: "slow", stacks: 2, duration: 2 }
  - selfBuff: { type: "evasion", stacks: 1, duration: 2 }
  - displayIcon: "🌀"
  - priority: 0
  - energyCost: 1

ターン3: 猛襲
  - baseDamage: 15
  - displayIcon: "💥"
  - priority: 0
  - energyCost: 1

ターン4〜: ターン1-3をループ

【幻影効果】
基本15% → 最大35%まで上昇
攻撃が無効化される

【戦闘想定】
3〜4ターンで撃破
合計被ダメージ: 19〜34
幻影で攻撃が空振りすると長期化

【特徴】
高火力だが幻影で不安定
現実歪曲でさらに回避率上昇
運が悪いと長期戦

【ドロップ】
魔石（極小）: 100%
魔石（小）: 40%
資金: 11〜15G
カード獲得率: 45%
```

---

## 複数敵バトル

### 6. 狂った看守×2 + 囚人×2
```
【狂った看守（1体あたり）】
id: "depth2_guard"
name: "狂った看守"
HP: 35
攻撃力: 9
baseEnemyEnergy: 1
speed: 40

【外見】
かつては秩序を守る看守だったが、今は狂気に堕ちている。
囚人を虐待し、自らも狂気に蝕まれている。

【行動パターン】
ターン1-2: 鞭打ち
  - baseDamage: 9
  - ally_buff: { type: "atkUp", stacks: 1, duration: 1 }
  - displayIcon: "🔗"
  - priority: 0
  - energyCost: 1

ターン3: 処刑命令
  - ally_buff: { type: "atkUp", stacks: 3, duration: 1 }
  - displayIcon: "⚰️"
  - priority: 0
  - energyCost: 1

【囚人（1体あたり）】
id: "depth2_prisoner_minion"
name: "囚人"
HP: 20
攻撃力: 6
baseEnemyEnergy: 1
speed: 30

【行動パターン】
通常: 殴打
  - baseDamage: 6
  - displayIcon: "👊"
  - priority: 0
  - energyCost: 1

看守撃破後: 狂乱化
  - baseDamage: 12
  - 行動がランダムに
  - displayIcon: "💢"

【戦闘想定】
総HP: 110（看守70 + 囚人40）
総ダメージ/ターン: 18〜30

【戦略】
看守を先に倒すか判断
処刑命令のタイミングに注意
囚人の狂乱化は予測不能

【ドロップ】
魔石（極小）: 2個
魔石（小）: 2個
資金: 22〜30G
カード獲得率: 60%
```

### 7. 恐怖の三体×3
```
【基本情報（1体あたり）】
id: "depth2_fear_trio"
name: "恐怖の影"
HP: 28
攻撃力: 8
baseEnemyEnergy: 1
speed: 45

【外見】
黒い影のような三つ子。
それぞれ異なる効果を体現している。

【個体特性】
第一体: 過去の恐怖
  - baseDamage: 8
  - debuff: { type: "weak", stacks: 2, duration: 2 }
  - displayIcon: "👻"
  - priority: 0
  - energyCost: 1

第二体: 現在の恐怖
  - baseDamage: 8
  - debuff: { type: "slow", stacks: 2, duration: 2 }
  - displayIcon: "🌑"
  - priority: 0
  - energyCost: 1

第三体: 未来の恐怖
  - baseDamage: 8
  - debuff: { type: "atkDown", stacks: 1, duration: 2 }
  - displayIcon: "🔮"
  - priority: 0
  - energyCost: 1

【連携行動】
3体生存時: 三重恐怖
  - baseDamage: 10
  - isAoe: true
  - debuff: [weak, slow, atkDown] 各1スタック
  - displayIcon: "💀"
  - priority: 10
  - energyCost: 1

2体生存時: 二重恐怖
  - baseDamage: 8
  - isAoe: true
  - debuff: ランダム2種
  - displayIcon: "👥"

1体生存時: 単独行動（通常攻撃のみ）

【戦闘想定】
総HP: 84
総ダメージ/ターン: 24（通常時）
三重恐怖: 30 + 全デバフ

【戦略】
素早く数を減らす
三重恐怖を避ける
範囲攻撃が有効

【ドロップ】
魔石（極小）: 3個
魔石（小）: 60%
資金: 18〜25G
カード獲得率: 55%
```

### 8. 呪いの感染者×4
```
【基本情報（1体あたり）】
id: "depth2_infected"
name: "呪いの感染者"
HP: 22
攻撃力: 6
baseEnemyEnergy: 1
speed: 35

【外見】
呪いに感染した民衆。
笑いながら、泣きながら、叫びながら襲い掛かる。

【行動パターン（各個体）】
80%: 呪いの攻撃
  - baseDamage: 6
  - debuff: { type: "curse", stacks: 1, duration: 3 }
  - displayIcon: "🦠"
  - priority: 0
  - energyCost: 1

20%: 呪いの伝播
  - baseDamage: 4
  - isAoe: true
  - debuff: { type: "curse", stacks: 1, duration: 2 }
  - displayIcon: "☠️"
  - priority: 0
  - energyCost: 1

【感染システム】
1体撃破ごとに残りの個体の攻撃力+2
curse累積で持続ダメージ増加

【戦闘想定】
総HP: 88
総ダメージ/ターン: 24
curse累積: スタック4〜8

【戦略】
curse管理が最優先
範囲攻撃で同時撃破
1体ずつ倒すと後半火力急増

【ドロップ】
魔石（極小）: 4個
魔石（小）: 50%
資金: 20〜28G
カード獲得率: 60%
```

---

## フロアボス：混沌の化身

### 基本情報
```
id: "depth2_boss"
name: "混沌の化身"
englishName: "Avatar of Chaos"
HP: 175
攻撃力: 12（基本値）
baseEnemyEnergy: 1（P1）→ 2（P2, P3）
speed: 50
特性: 精神体（物理ダメージ-20%）

【外見】
無数の目と口を持つ不定形の黒い塊。
形が定まらず、見る者の精神を侵食する。
混沌そのものが形を成した存在。
```

### フェーズ1（HP100%〜66%）：恐怖の始まり
```
【フェーズ特性】
精神体: 物理ダメージ-20%

【行動パターン】
ターン1: 恐怖の眼光
  - baseDamage: 12
  - debuff: { type: "weak", stacks: 3, duration: 3 }
  - displayIcon: "👁️"
  - priority: 0
  - energyCost: 1

ターン2: 混沌の囁き
  - baseDamage: 10
  - debuff: { type: "atkDown", stacks: 2, duration: 2 }
  - displayIcon: "👄"
  - priority: 0
  - energyCost: 1

ターン3: 精神侵食
  - baseDamage: 8
  - debuff: { type: "slow", stacks: 2, duration: 2 }
  - debuff2: { type: "curse", stacks: 1, duration: 3 }
  - displayIcon: "🧠"
  - priority: 0
  - energyCost: 1

ターン4: 幻覚発生
  - baseDamage: 10
  - isAoe: true
  - debuff: { type: "weak", stacks: 2, duration: 2 }
  - debuff2: { type: "slow", stacks: 2, duration: 2 }
  - displayIcon: "🌀"
  - priority: 0
  - energyCost: 1

ターン5〜: ループ

【特徴】
デバフ累積が脅威
物理ダメージ軽減で長期化
curse管理に注意
```

### フェーズ2（HP65%〜34%）：混沌の深化
```
【フェーズ移行時】
精神崩壊:
  - baseDamage: 20
  - isAoe: true
  - debuff: { type: "stun", stacks: 1, duration: 1 }
  - displayIcon: "💥"

【フェーズ特性】
精神体: 物理ダメージ-20%
混沌増幅: 全てのデバフ付与+1スタック

【行動パターン（2回/ターン）】
1回目:
- 40%: 混沌の一撃
  - baseDamage: 15
  - debuff: { type: "atkDown", stacks: 2, duration: 2 }
  - displayIcon: "👊"
  - priority: 0
  - energyCost: 1

- 30%: 恐怖波動
  - baseDamage: 12
  - isAoe: true
  - debuff: { type: "weak", stacks: 3, duration: 3 }
  - displayIcon: "🌊"
  - priority: 0
  - energyCost: 1

- 30%: 混乱の嵐
  - baseDamage: 10
  - isAoe: true
  - debuff: { type: "slow", stacks: 3, duration: 3 }
  - displayIcon: "🌪️"
  - priority: 0
  - energyCost: 1

2回目:
- 50%: 精神攻撃
  - baseDamage: 10
  - debuff: { type: "curse", stacks: 2, duration: 3 }
  - displayIcon: "🎭"
  - priority: 0
  - energyCost: 1

- 30%: 幻影召喚
  - summon: "depth2_phantom_beast"
  - summonHp: 20
  - displayIcon: "🐺"
  - priority: 0
  - energyCost: 1

- 20%: 自己強化
  - buff: { type: "atkUp", stacks: 3, duration: 2 }
  - displayIcon: "💪"
  - priority: 0
  - energyCost: 1

【特徴】
2回行動で圧力増大
幻影召喚で数的不利
デバフ管理が最重要
```

### フェーズ3（HP33%〜0%）：完全混沌
```
【フェーズ移行時】
混沌爆発:
  - baseDamage: 25
  - isAoe: true
  - debuff: { type: "stun", stacks: 1, duration: 1 }
  - debuff2: { type: "weak", stacks: 5, duration: 3 }
  - displayIcon: "💥"

【フェーズ特性】
精神体: 物理ダメージ-20%
混沌の具現化: 攻撃力+50%

【行動パターン（2回/ターン）】
1回目:
- 50%: 絶対混沌
  - baseDamage: 20
  - debuff: { type: "atkDown", stacks: 3, duration: 2 }
  - debuff2: { type: "weak", stacks: 3, duration: 2 }
  - displayIcon: "☠️"
  - priority: 0
  - energyCost: 1

- 30%: 全体精神破壊
  - baseDamage: 15
  - isAoe: true
  - debuff: [weak, slow, curse] 各2スタック
  - displayIcon: "💀"
  - priority: 0
  - energyCost: 1

- 20%: 幻影軍団
  - summon: "depth2_phantom_beast" × 2
  - displayIcon: "🐺🐺"
  - priority: 0
  - energyCost: 1

2回目:
- 60%: 追撃
  - baseDamage: 15
  - displayIcon: "⚡"
  - priority: 0
  - energyCost: 1

- 40%: 混沌の連鎖
  - baseDamage: 12
  - bonusDamagePerDebuff: 3（プレイヤーのデバフ数×3追加ダメージ）
  - displayIcon: "🔗"
  - priority: 0
  - energyCost: 1

【特殊】
HP10%以下: 最終混沌
  - 次ターン baseDamage: 80
  - isAoe: true
  - debuff: { type: "stun", stacks: 2, duration: 2 }
  - displayIcon: "🔥"
  - priority: 10
  - energyCost: 2

【特徴】
stun + weak で1ターン無防備
高火力で一気に削る必要
混沌の連鎖がデバフ累積時致命的
最終混沌前に倒す必要
```

### 戦闘総評
```
【想定戦闘ターン数】
8〜12ターン（stunのターンスキップ含む）

【合計被ダメージ】
AP: 90〜130（デバフで実質防御力低下）
HP: 40〜70（フェーズ移行 + デバフ累積）

【初見難易度】
デバフ管理が非常に複雑
stunで1ターン無駄に
物理軽減で魔法カードが有利
デバフが3種類重なる

【攻略のカギ】
- フェーズ1: デバフ解除を温存
- フェーズ2: stun後の立て直し準備
- フェーズ3: stun直後に全力攻撃
- デバフ解除カード必須
- 魔法攻撃が有効
- 最終混沌前に倒す（HP10%ライン）
```

### ドロップ報酬
```
魔石（小）: 5個
魔石（中）: 3個
魔石（大）: 1個（70%）
資金: 90〜130G
カード獲得: 確定（レア以上、デバフ解除系）
装備獲得: 45%（レア装備）
```

---

## バトル回数設計（深度2全体）

### 想定バトル構成（8〜12回）
```
【パターンA（最短8回）】
1. 通常敵×4回
2. 複数敵×2回
3. 休憩/宝箱×1回
4. 通常敵×1回
5. フロアボス×1回

【パターンB（標準10回）】
1. 通常敵×5回
2. 複数敵×2回
3. 休憩/宝箱×2回
4. 通常敵×1回
5. フロアボス×1回
```

### AP損耗計算
```
【戦闘ごとのAP減少】
通常敵（単体）: 12〜18
複数敵: 15〜25
フロアボス: 90〜130

【標準10回パターン】
通常敵6回: 72〜108
複数敵2回: 30〜50
フロアボス: 90〜130
合計: 192〜288

【結論】
- デバフで実質防御力低下
- weak/atkDownで火力低下、戦闘長期化
- 修繕アイテム必須
- デバフ解除カードの重要性
```

---

## 敵出現率

### 通常敵出現率（単体）
```
狂乱の囚人: 25%
恐怖の影: 25%
精神崩壊者: 20%
呪いの伝道者: 20%
幻影の獣: 10%
```

### 複数敵出現率
```
狂った看守×2 + 囚人×2: 35%
恐怖の三体×3: 35%
呪いの感染者×4: 30%
```

---

## まとめ

### 深度2（狂乱）の設計意図
```
【主要要素】
1. デバフ累積システム（weak, slow, curse, atkDown）
2. 予測不能な行動パターン
3. 精神攻撃特化

【学習要素】
1. デバフ管理の重要性
2. デバフ解除の重要性
3. stunからの立て直し
4. 魔法攻撃の有効性（物理軽減対策）

【難易度カーブ】
深度1より1.2倍強化
デバフ管理が複雑化
curse累積が致命的
フロアボスはデバフとの戦い
```

### プレイヤー体験
```
1回目: デバフ管理失敗、累積ダメージで全滅
2回目: デバフ解除カード用意、curse管理を意識
3回目: 魔法カード活用、ボス撃破
```

「腐食を身に纏えば、狂乱に身を置き」
- 肉体の崩壊から精神の崩壊へ

---

## 設計書間の参照関係

```
battle_logic.md (Ver 4.0) [マスター文書]
├── バフ/デバフ定義 → buff_debuff_system.md (Ver 4.0)
├── depth2敵データ定義 → depth2_enemy_database.md (Ver 4.0) [本文書]
└── キャラクター固有効果 → NEW_CHARACTER_SYSTEM_DESIGN.md
```

※ Ver 4.0 では狂気システム（insanity, confusion, terror）は設計から削除されました
※ 深度2の敵はVer 4.0標準のデバフ（weak, slow, stun, curse, atkDown等）のみを使用します
