# PixiJS統合 Phase 3: アセットパイプライン構築

## Change History

| Date | Content |
|------|---------|
| 2026-02-22 | Initial creation based on engine integration proposal |

---

## 1. Overview

AI生成画像を本格的なゲームアセットに変換するパイプラインを構築し、アニメ調ダークファンタジーの統一されたビジュアルスタイルを確立する。敵キャラクター50体の画像生成、スプライトシート化、キャラクターアニメーション、カードイラスト、背景のパララックス化を実施する。

### Goals

- アニメ調ダークファンタジーのスタイルガイド確立
- 敵キャラクター50体のAI画像バッチ生成
- 全画像アセットのスプライトシート最適化
- CharacterLayer への敵/プレイヤースプライト描画
- 背景のパララックススクロール実装
- カードイラストのAI生成とスタイル統一

### Non-Goals

- Spineスケルタルアニメーション（Phase 4 で判断）
- 新規キャラクタークラスの追加
- ゲームバランス調整

---

## 2. スタイルガイド — アニメ調ダークファンタジー

### 2.1 ビジュアルディレクション

**全体方針:** 日本のダークファンタジーゲーム（例: ダークソウル系 × アニメ調キャラデザ）をベースに、カードゲームとしての視認性を重視したスタイル。

**キーワード:**
- ダーク: 暗い色調ベース、光と影のコントラスト強調
- ファンタジー: 魔法的な発光、非現実的なプロポーション
- アニメ調: デフォルメされた特徴、大きな目、鮮やかなハイライト
- カードゲーム: 背景透過、正面～やや斜めのポーズ、明確なシルエット

### 2.2 カラーパレット

```
【ベース色（背景・環境）】
- 深淵ブラック:  #0a0a14  (最深部背景)
- ダーク基調:    #1a1a24  (既存の primary)
- 暗灰:         #2a2a3a  (UIパネル背景)
- 石壁:         #3a3a4a  (テクスチャベース)

【属性アクセント色】
- 炎:   #ff4400 / #ff8800 / #ffcc00
- 氷:   #00ccff / #aaeeff / #ffffff
- 雷:   #ffcc00 / #ffee66 / #ffffff
- 闇:   #8800ff / #440088 / #220044
- 光:   #ffffaa / #ffffcc / #ffffff
- 物理: #cccccc / #888888 / #ffffff

【キャラクター配色ルール】
- 主色:  キャラの属性 or 種族を表す1色
- 副色:  主色の補色 or 類似色
- 発光:  目・魔法エフェクト部分に属性色のグロー
- 影:    #1a0a2a (紫寄りの暗色で統一)
```

### 2.3 AI画像生成プロンプトテンプレート

#### 敵キャラクター用 共通プロンプト

```
[Base prompt - 全敵共通]
dark fantasy anime style, single character, transparent background,
facing slightly left, battle-ready pose, detailed illustration,
dramatic lighting from above, glowing eyes,
dark purple shadows, high contrast,
game character art, card game style,
clean lines, no text, no watermark

[Depth 1 - 森林迷宮 enemies]
{enemy_name}, forest creature, mossy textures, green-brown tones,
dim forest lighting, {specific_features}

[Depth 2 - 水晶洞窟 enemies]
{enemy_name}, crystal cave dweller, crystalline features, blue-purple tones,
bioluminescent glow, {specific_features}

[Depth 3 - 溶岩神殿 enemies]
{enemy_name}, volcanic creature, molten features, red-orange tones,
magma glow, obsidian textures, {specific_features}

[Depth 4 - 虚空回廊 enemies]
{enemy_name}, void entity, ethereal features, dark purple tones,
cosmic energy, distorted form, {specific_features}

[Depth 5 - 深淵 enemies]
{enemy_name}, abyssal horror, eldritch features, pitch black with highlights,
otherworldly glow, incomprehensible form, {specific_features}
```

#### プレイヤーキャラクター用

```
[Swordsman]
dark fantasy anime style, male swordsman warrior,
heroic pose holding sword, silver armor with blue accents,
determined expression, short dark hair,
dramatic lighting, transparent background,
full body, facing right, game character art

[Mage]
dark fantasy anime style, female mage sorceress,
casting spell pose, dark robes with purple accents,
mystical expression, long silver hair,
magical energy swirling, transparent background,
full body, facing right, game character art
```

### 2.4 品質基準

| 基準 | 要件 |
|------|------|
| 解像度 | 最低 512x512 px（敵）、768x1024 px（プレイヤー） |
| 背景 | 完全透過（PNG alpha） |
| ポーズ | 正面～30度斜め、バトルレディ |
| シルエット | 3m離れても識別可能な明確な輪郭 |
| 色数 | 主色+副色+アクセント色の3色構成 |
| 統一感 | 全キャラで影色 #1a0a2a、ライティング方向統一 |

---

## 3. 敵キャラクター50体のバッチ生成計画

### 3.1 既存の敵データとの対応

既存の `src/constants/data/characters/enemies/` に定義された50体に対応するPNGを生成する。各敵にはすでに `imagePath` が設定されている。

### 3.2 Depth別バッチ生成計画

| Depth | 敵数 | テーマ | 優先度 |
|-------|------|--------|--------|
| Depth 1 (森林迷宮) | 10体 | 森の生物、ゴブリン、植物系 | 最高 |
| Depth 2 (水晶洞窟) | 10体 | 結晶生物、洞窟モンスター | 高 |
| Depth 3 (溶岩神殿) | 10体 | 溶岩・炎系、ゴーレム | 中 |
| Depth 4 (虚空回廊) | 10体 | 虚空の存在、幽霊系 | 中 |
| Depth 5 (深淵) | 10体 | エルドリッチホラー、ボス | 高 |

### 3.3 生成ワークフロー

```
1. 敵データファイルから名前・特徴を抽出
2. Depth別プロンプトテンプレートを適用
3. AI画像生成ツールでバッチ生成（候補2-3枚/体）
4. 品質チェック（シルエット、色調、透過）
5. 必要に応じてリタッチ（Aseprite or GIMP）
6. 512x512 にリサイズ・最適化
7. public/images/enemies/ に配置
8. TexturePacker でスプライトシート化
```

### 3.4 AI生成ツール選定

| ツール | 用途 | コスト |
|--------|------|--------|
| Stable Diffusion (ローカル) | メイン生成。スタイル統一にLoRA活用 | 無料（GPU必要） |
| PixelLab | キャラスプライト + アニメーション | 無料プランあり |
| Ludo.ai | スプライトシート直接生成 | 無料プランあり |
| Midjourney / DALL-E | 高品質コンセプト画 | 有料 |

**推奨フロー:** Stable Diffusion でベース生成 → 品質チェック → PixelLab でアニメーション化

---

## 4. スプライトシート最適化

### 4.1 スプライトシート構成

```
public/assets/spritesheets/
├── enemies/
│   ├── depth1_enemies.json    + depth1_enemies.png
│   ├── depth2_enemies.json    + depth2_enemies.png
│   ├── depth3_enemies.json    + depth3_enemies.png
│   ├── depth4_enemies.json    + depth4_enemies.png
│   └── depth5_enemies.json    + depth5_enemies.png
├── players/
│   ├── swordsman.json         + swordsman.png
│   └── mage.json              + mage.png
├── cards/
│   └── card_illustrations.json + card_illustrations.png
├── effects/
│   ├── particles.json         + particles.png
│   └── elements.json          + elements.png
└── backgrounds/
    ├── depth1_bg_layers.json  + depth1_bg_layers.png
    ├── depth2_bg_layers.json  + depth2_bg_layers.png
    ├── depth3_bg_layers.json  + depth3_bg_layers.png
    └── depth4_bg_layers.json  + depth4_bg_layers.png
```

### 4.2 TexturePacker 設定

```json
{
  "algorithm": "MaxRects",
  "maxWidth": 2048,
  "maxHeight": 2048,
  "padding": 2,
  "trimMode": "Trim",
  "extrude": 1,
  "format": "pixijs",
  "pngOptimizationLevel": 3
}
```

### 4.3 PixiJS でのスプライトシート読み込み

```typescript
// TextureManager の拡張（Phase 1 で骨格作成済み）
class TextureManager {
  async loadSpriteSheet(key: string, jsonUrl: string): Promise<Spritesheet> {
    const sheet = await Assets.load(jsonUrl);
    this.sheets.set(key, sheet);
    return sheet;
  }

  getFrame(sheetKey: string, frameName: string): Texture {
    return this.sheets.get(sheetKey)!.textures[frameName];
  }
}
```

---

## 5. CharacterLayer 実装

### 5.1 敵キャラクター描画

```typescript
// src/ui/pixi/battle/layers/CharacterLayer.tsx
interface CharacterLayerProps {
  enemy: {
    id: string;
    spriteKey: string;   // スプライトシート内のフレーム名
    depth: number;
    hp: number;
    maxHp: number;
    activeBuffs: ActiveBuff[];
  };
  player: {
    characterClass: string;
    spriteKey: string;
    hp: number;
    maxHp: number;
    activeBuffs: ActiveBuff[];
  };
}
```

**描画仕様:**
- 敵: 画面上部中央に配置（既存EnemyFrameの位置に重ねる）
- プレイヤー: 画面下部左に配置（既存PlayerFrameの位置に重ねる）
- 待機アニメーション: 軽い上下浮遊（sine wave, ±5px, 2秒周期）
- 被ダメージ: 赤フラッシュ + 後方ノックバック（100ms）
- 攻撃: 前方ステップ（150ms） → 元の位置に戻る（200ms）

### 5.2 段階的移行

```
Phase 3a: PixiJSスプライトと既存DOMフレームの共存
  - PixiJS側: スプライト描画（CharacterLayer）
  - DOM側: ステータス表示（HP/AP/Guard バー）は維持
  - 切り替え: Feature Flag で DOM画像 ↔ PixiJSスプライト

Phase 3b: DOM画像の置換
  - EnemyFrame の <img> タグを非表示
  - PlayerFrame の <img> タグを非表示
  - ステータスバーはDOMのまま残す（視認性・アクセシビリティ確保）
```

---

## 6. 背景パララックス実装

### 6.1 レイヤー構成

各Depth背景を3-4レイヤーに分割し、奥行き感を演出する。

```
BackgroundLayer (PixiJS TilingSprite)
├── Layer 0: 空/天井 (最遠景、移動速度 0.1x)
├── Layer 1: 遠景    (壁・遠くの構造物、移動速度 0.3x)
├── Layer 2: 中景    (柱・近い構造物、移動速度 0.6x)
└── Layer 3: 前景    (霧・パーティクル、移動速度 1.0x)
```

### 6.2 AI背景レイヤー生成

```
[共通プロンプト]
dark fantasy environment, {depth_theme}, horizontal tileable,
atmospheric perspective, game background layer,
{layer_description}, no characters, no text

[Layer 0 - 最遠景] faded, misty, very low detail, gradient sky
[Layer 1 - 遠景]   medium detail, architectural elements
[Layer 2 - 中景]   detailed structures, depth_specific features
[Layer 3 - 前景]   fog particles, floating dust, ambient glow
```

### 6.3 パララックス動作

- ターン進行に応じて微動（戦闘の緊張感演出）
- カード発動時に一時的な加速スクロール
- 敵撃破時に振動 + ズームアウト
- ボス戦突入時に高速スクロール → 新背景

---

## 7. カードイラスト

### 7.1 カードイラスト仕様

| 項目 | 仕様 |
|------|------|
| サイズ | 256x256 px（カード内イラストエリア） |
| スタイル | アニメ調、属性色を強調 |
| 背景 | 属性グラデーション（透過なし） |
| 内容 | スキル/技のイメージイラスト |

### 7.2 生成プロンプトカテゴリ

```
[攻撃カード] action scene, weapon slash/impact, dynamic pose,
  energy trails, {element_color} dominant

[防御カード] shield barrier, protective stance, blue energy,
  hexagonal patterns, defensive aura

[スキルカード] magical energy, mystical symbols, swirling aura,
  {element_color} glow, enchantment visual

[回復カード] healing light, green energy, soothing glow,
  nature elements, restoration visual
```

### 7.3 カードへの統合

既存の `CardComponent.tsx` は CSS グラデーション背景。Phase 3 ではイラスト画像を追加：

```typescript
// CardComponent の拡張
interface CardProps {
  // ... existing props
  illustrationUrl?: string;  // スプライトシートからの参照 or 個別画像
}
```

---

## 8. Implementation Steps

### Step 1: スタイルガイド確定とテンプレート作成
- 上記スタイルガイドをチーム（自分）で確定
- プロンプトテンプレートの微調整
- 3-5体のテスト生成でスタイル検証

### Step 2: 敵キャラクター Depth 1 バッチ生成
- 10体のプロンプト作成
- AI生成 → 品質チェック → リタッチ
- public/images/enemies/ に配置
- 動作確認（既存の imagePath との接続）

### Step 3: 残りの敵キャラクター生成 (Depth 2-5)
- 40体の残りをバッチ生成
- Depth テーマに沿った品質チェック
- 統一感の最終確認

### Step 4: TexturePacker でスプライトシート化
- 全敵画像をDepth別にパック
- PixiJS JSON形式で出力
- TextureManager への読み込みテスト

### Step 5: CharacterLayer 実装
- 敵スプライト描画ロジック
- 待機アニメーション（浮遊）
- 被ダメージ/攻撃モーション
- Feature Flag による DOM ↔ PixiJS 切替

### Step 6: 背景レイヤー生成と実装
- Depth 別に 3-4 レイヤーの背景画像生成
- BackgroundLayer のパララックス実装
- ターン進行連動の微動実装

### Step 7: カードイラスト生成
- 全カード分のイラスト生成（約30種）
- スプライトシート化
- CardComponent への統合

### Step 8: プレイヤーキャラクター刷新
- 剣士/魔法使いの高品質イラスト生成
- 複数ポーズ（待機、攻撃、被ダメージ、勝利）
- スプライトシート化

---

## 9. Asset Management Architecture

```typescript
// src/ui/pixi/shared/textures/AssetManifest.ts
// PixiJS v8 の Assets API を活用した宣言的アセット管理

const ASSET_MANIFEST = {
  bundles: [
    {
      name: 'battle-common',
      assets: [
        { alias: 'particles', src: '/assets/spritesheets/effects/particles.json' },
        { alias: 'elements', src: '/assets/spritesheets/effects/elements.json' },
      ]
    },
    {
      name: 'battle-depth1',
      assets: [
        { alias: 'enemies-d1', src: '/assets/spritesheets/enemies/depth1_enemies.json' },
        { alias: 'bg-d1', src: '/assets/spritesheets/backgrounds/depth1_bg_layers.json' },
      ]
    },
    // ... depth 2-5
  ]
};
```

**読み込み戦略:**
- `battle-common`: アプリ起動時にプリロード
- `battle-depth{N}`: ダンジョン突入時に該当Depthのみロード
- 遅延読み込みでメモリ使用量を最適化

---

## 10. Affected Files

### New Files
| File | Purpose |
|------|---------|
| `src/ui/pixi/shared/textures/AssetManifest.ts` | アセットマニフェスト |
| `src/ui/pixi/battle/layers/CharacterLayer.tsx` | キャラクター描画（本実装） |
| `src/ui/pixi/battle/layers/BackgroundLayer.tsx` | パララックス背景（本実装） |
| `public/assets/spritesheets/**` | スプライトシートアセット |
| `.claude/docs/style_guide.md` | ビジュアルスタイルガイド |
| `.claude/docs/ai_prompt_templates.md` | AI画像生成プロンプト集 |

### Modified Files
| File | Change |
|------|--------|
| `src/ui/pixi/shared/textures/TextureManager.ts` | スプライトシート読み込み実装 |
| `src/ui/html/battleHtml/EnemyFrame.tsx` | Feature Flag による画像切替 |
| `src/ui/html/battleHtml/PlayerFrame.tsx` | Feature Flag による画像切替 |
| `src/ui/html/cardHtml/CardComponent.tsx` | イラスト画像表示追加 |

---

## 11. Acceptance Criteria

- [ ] 50体の敵キャラクター画像がすべて生成・配置されている
- [ ] 全画像がアニメ調ダークファンタジースタイルで統一されている
- [ ] スプライトシート化により HTTP リクエスト数が 10 以下に削減
- [ ] CharacterLayer で敵/プレイヤーが描画される
- [ ] 待機、被ダメージ、攻撃のモーションアニメーションが動作する
- [ ] パララックス背景が Depth に応じて変化する
- [ ] カードにイラストが表示される
- [ ] アセットの遅延読み込みが機能する

---

## 12. Dependencies

- **Blocked by**: Phase 1（PixiJS基盤）、Phase 2（EffectLayer）
- **Blocks**: Phase 4（Spine判断材料としてのスプライトアニメーション品質評価）

## 13. References

- TexturePacker: https://www.codeandweb.com/texturepacker
- PixiJS Assets API: https://pixijs.com/guides/components/assets
- PixelLab: https://www.pixellab.ai/
- Ludo.ai: https://ludo.ai/features/sprite-generator
- 既存敵データ: `src/constants/data/characters/enemies/`
- 既存カードデータ: `src/constants/data/cards/`
