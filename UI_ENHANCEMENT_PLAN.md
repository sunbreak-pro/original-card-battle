# UI強化 学習・実装プラン
## 期間: 1-2週間 | 対象: ダークファンタジー × モダンUI

---

## Phase 1: 基礎知識習得 (Day 1-3)

### Day 1: デザイン原則の理解
**目標**: ゲームUIの基本原則を学ぶ

#### 学習内容
1. **視覚階層 (Visual Hierarchy)**
   - 重要な情報ほど目立たせる
   - サイズ、色、コントラストの使い分け
   - 現状のカードUIは良い例: コスト(大)→名前→効果(小)

2. **カラー理論 (ダークファンタジー向け)**
   - 主要色: 深い紫(#2a1a3e)、暗い赤(#4a1a2a)、金(#c9a227)
   - アクセント: 炎のオレンジ、毒の緑、氷の青
   - 背景は暗く、UIは光る要素で浮き上がらせる

3. **参考ゲームのUI分析**
   - Slay the Spire（カード型ローグライク）
   - Darkest Dungeon（ダークファンタジー）
   - Hades（モダン＋ダーク）

#### 実践課題
- 現在のカードデザインのスクリーンショットを撮り、良い点・改善点をメモ

---

### Day 2: Figma基礎
**目標**: UIモックアップを作れるようになる

#### 学習内容 (2-3時間)
1. **基本操作**
   - フレーム、シェイプ、テキストの配置
   - カラーとグラデーション
   - Auto Layoutの基本

2. **コンポーネント作成**
   - カードのモックアップを作成
   - ボタン、バー、バッジのデザイン

#### 実践課題
- 現在のカードデザインをFigmaで再現
- 改良版を1つ作成

#### 推奨リソース
- Figma公式: https://help.figma.com/hc/en-us/sections/4403935997847-Getting-started
- YouTube: "Figma UI Design Tutorial" (1時間程度のもの)

---

### Day 3: 画像生成AI (nanobananaPro) の活用法
**目標**: ゲームアセット生成のプロンプト技術を習得

#### 学習内容
1. **効果的なプロンプト構造**
   ```
   [アートスタイル], [被写体], [雰囲気/照明], [背景], [品質指定]

   例: "dark fantasy card illustration,
        flaming sword weapon,
        dramatic lighting with ember particles,
        black void background,
        game asset, high detail, transparent PNG"
   ```

2. **ゲームアセット用プロンプトテンプレート**
   - カードイラスト: 武器、魔法、防具
   - UI要素: ボタンテクスチャ、フレーム装飾
   - 背景: バトルフィールド、ダンジョン

3. **一貫したスタイル維持**
   - シードの固定（可能な場合）
   - スタイル記述の統一
   - 同シリーズは同じプロンプトベースを使用

#### 実践課題
- 3種類のカードカテゴリ（physical/magic/defense）のアイコン生成
- バトル画面の背景テクスチャ生成

---

## Phase 2: アセット制作 (Day 4-7)

### Day 4-5: カードビジュアル強化

#### 制作するアセット
1. **カードフレーム** (4種類: physical/magic/defense/heal)
   - Figmaでフレームデザイン
   - グラデーション、装飾、枠線

2. **カードアートワーク**
   - 画像生成AIで基本イラスト作成
   - Kritaで調整（透過、色補正、サイズ調整）

3. **カテゴリアイコン**
   - 剣(physical)、盾(defense)、杖(magic)、薬(heal)

#### 技術的な実装方法
```tsx
// カードに背景画像を追加
<div
  className="card"
  style={{
    backgroundImage: `url('/assets/cards/frame_${card.category}.png')`,
    backgroundSize: 'cover'
  }}
>
  <img
    className="card-art"
    src={`/assets/cards/art/${card.id}.png`}
    alt={card.name}
  />
  {/* 既存のUI要素 */}
</div>
```

---

### Day 6-7: バトル画面強化

#### 制作するアセット
1. **背景テクスチャ** (深度別5種類)
   - D1: 森の入り口（緑系ダーク）
   - D2: 洞窟（岩のテクスチャ）
   - D3: 地下神殿（紫＋金）
   - D4: 溶岩地帯（赤＋オレンジ）
   - D5: 虚空（黒＋宇宙的）

2. **UIパネル装飾**
   - ステータスバーの装飾フレーム
   - ボタンのホバー/アクティブ状態

3. **エネミービジュアル用フレーム**
   - 敵の周りの装飾枠
   - ボスは特別なフレーム

---

## Phase 3: 実装とポリッシュ (Day 8-12)

### Day 8-9: アセット統合

#### ディレクトリ構造
```
public/
└── assets/
    ├── cards/
    │   ├── frames/
    │   │   ├── frame_physical.png
    │   │   ├── frame_magic.png
    │   │   └── ...
    │   └── art/
    │       ├── slash.png
    │       └── ...
    ├── ui/
    │   ├── buttons/
    │   ├── panels/
    │   └── icons/
    └── backgrounds/
        ├── depth_1.jpg
        └── ...
```

#### 実装タスク
1. CardComponentに画像表示を追加
2. BattleScreenの背景をdepthに応じて切り替え
3. ボタンのスタイル更新

---

### Day 10-11: アニメーション強化

#### 追加するアニメーション
1. **カードプレイ時のエフェクト**
   - 発光、パーティクル風CSS
   - 攻撃カード: 赤い閃光
   - 防御カード: 青いシールド展開

2. **ダメージ表示の改善**
   - 数字がバウンドしながらフェードアウト
   - クリティカル時は大きく金色に

3. **ターン切り替えの演出**
   - より劇的なスライドイン

#### 実装例
```css
/* カードプレイ時のグロー効果 */
@keyframes cardPlayGlow {
  0% {
    filter: drop-shadow(0 0 0 transparent);
    transform: scale(1);
  }
  50% {
    filter: drop-shadow(0 0 20px var(--category-color));
    transform: scale(1.1);
  }
  100% {
    filter: drop-shadow(0 0 0 transparent);
    transform: scale(1) translateY(-100vh);
  }
}

.card-playing {
  animation: cardPlayGlow 0.6s ease-out forwards;
}
```

---

### Day 12: 最終調整とドキュメント

#### チェックリスト
- [ ] 全カテゴリのカードフレーム適用
- [ ] 5深度の背景画像
- [ ] ボタン・パネルの統一デザイン
- [ ] アニメーションの動作確認
- [ ] パフォーマンス確認（画像サイズ最適化）

---

## 参考リソース集

### デザイン学習
- **Game UI Database**: https://gameuidatabase.com/
  - 実際のゲームUIを分類して閲覧可能
- **Dribbble - Game UI**: https://dribbble.com/tags/game_ui
  - インスピレーション用

### ツール別
- **Figma**: https://www.figma.com/community/
  - 無料のゲームUIキットを探す
- **Krita**: https://docs.krita.org/
  - 透過PNG作成、色調整に使用

### カラーパレット
- **ダークファンタジー向け**
  ```
  主背景: #0a0a12
  パネル: #1a1a2e
  アクセント: #c9a227 (金), #d94a4a (赤), #4a8ed9 (青)
  テキスト: #e8e8e8
  サブテキスト: #888888
  ```

---

## 現状のコード分析メモ

### 良い点（維持すべき）
1. CSS変数によるテーマシステム
2. vh/vw単位でのレスポンシブ対応
3. カテゴリ別カラーシステム

### 改善ポイント
1. 絵文字(⚔️🎴)→画像アセットへ置換
2. 単色グラデーション→テクスチャ画像追加
3. シンプルなバー→装飾的なフレーム追加

---

## 次のアクション

**すぐにできること:**
1. 参考ゲームのスクリーンショット収集
2. カラーパレットの確定
3. Figmaアカウント準備（無料版で十分）

**週末の目標:**
- カードフレーム1種類完成
- 深度1の背景画像完成
