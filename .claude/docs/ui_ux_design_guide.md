# UI/UX Design Guide

UI/UX設計の原則と仕様書。**なぜ**そう設計されているか（Why）と**何を**実装すべきか（What）を定義する。

> **関連リソース:**
> - 実装方法（How）: `.claude/skills/ui-ux-creator/SKILL.md`
> - CSS変数定義: `src/ui/css/core/variables.css`
> - アニメーション定義: `src/ui/css/animations/keyframes.css`

---

## 1. Design Philosophy (設計哲学)

### Core Principles

| Principle | Description | Rationale |
|-----------|-------------|-----------|
| **Clarity over Decoration** | 情報伝達を最優先。装飾は目的を持つ場合のみ | プレイヤーは瞬時に判断する必要がある |
| **Psychological Pressure** | 最小限の要素で心理的緊張を演出 | ローグライクの緊張感を視覚で増幅 |
| **Depth Visualization** | ダンジョン深度を視覚的に認識可能に | 進行感と達成感を提供 |
| **Strategic Feedback** | 即時フィードバックで意思決定を支援 | カードバトルの戦略性を強調 |

### Dark Fantasy Aesthetic

- **カラートーン:** 低彩度、高コントラスト
- **光源:** 点光源（glow効果）を控えめに使用
- **テクスチャ:** なし（フラットデザイン、パフォーマンス優先）
- **境界:** 微妙な透明度のボーダーで奥行き表現

### When to Break Rules

ルールを破る判断基準:

1. **プレイヤーの安全:** 危険状態（HP低下、ライフ1）は例外的に派手な演出OK
2. **達成感:** レア報酬、ボス撃破など特別な瞬間は視覚的強調OK
3. **緊急性:** ターン制限、即死危機など即座に注目を集める必要がある場合

---

## 2. Color System (カラーシステム)

### Base Theme Colors

```css
/* Reference: src/ui/css/core/variables.css */
--theme-primary: #1a1a24;      /* 主背景 */
--theme-secondary: #2a2a3d;    /* サブ背景 */
--theme-accent: #8b7aa8;       /* アクセント（紫系） */
--theme-glow: rgba(139, 122, 168, 0.25);  /* グロー効果 */
--theme-hover: #a090b0;        /* ホバー状態 */
```

**Rationale:** 紫系のダークテーマはファンタジー感を出しつつ、目の疲労を軽減。

### Facility Theme Colors

| Facility | Primary | Secondary | Psychological Effect |
|----------|---------|-----------|---------------------|
| **Blacksmith** | `#ff6b35` | `#ff9f66` | 炎・鍛冶の熱さ、力強さ |
| **Shop** | `#d4af37` | `#ffd700` | 金・富、商取引の活気 |
| **Sanctuary** | `#a855f7` | `#7c3aed` | 神秘・魔法、神聖さ |
| **Storage** | `#ff6f00` | `#ffa000` | 暖かみ、安全な保管 |
| **Guild** | `#f4e4c1` | `#e8d5b7` | 伝統・クリーム、落ち着き |
| **Library** | `#a855f7` | `#9f7aea` | 知識・魔法、探求心 |

**使用例:**
```css
--blacksmith-bg: rgba(45, 31, 26, 0.95);
--blacksmith-border: rgba(255, 107, 53, 0.3);
--blacksmith-text: #e8d5b7;
```

### Semantic Status Colors

| Status | Variable | Color | Usage |
|--------|----------|-------|-------|
| HP | `--color-hp` | `#d94a4a` | HP表示、ダメージ |
| AP | `--color-ap` | `#808080` | 行動力、コスト |
| Guard | `--color-guard` | `#4488cc` | ガード値、防御 |
| Energy | `--color-energy` | `#2b9f0e` | エネルギー、回復 |

**Rationale:** 直感的な色連想（赤=危険/HP、緑=回復/エネルギー、青=防御）

### Notification Colors

| Type | Variable | Color | Usage |
|------|----------|-------|-------|
| Success | `--color-success` | `#4caf50` | 成功、完了 |
| Error | `--color-error` | `#f44336` | エラー、失敗 |
| Warning | `--color-warning` | `#ffc107` | 警告、注意 |

### Mastery Progression Colors

**寒色→暖色グラデーション**で成長を表現:

| Level | Variable | Color | Meaning |
|-------|----------|-------|---------|
| 0 | `--color-mastery-0` | `#343434` | 未熟練（グレー） |
| 1 | `--color-mastery-1` | `#2196f3` | 初級（青） |
| 2 | `--color-mastery-2` | `#9c27b0` | 中級（紫） |
| 3 | `--color-mastery-3` | `#ff9800` | 熟練（金） |

**Rationale:** 寒色（未熟）→暖色（熟練）は「冷たい→熱い」の直感的な成長表現。

### Quality Colors

| Quality | Variable | Color |
|---------|----------|-------|
| Poor | `--color-quality-poor` | `#5b5b5b` |
| Normal | `--color-quality-normal` | `#9ee2e2` |
| Good | `--color-quality-good` | `#05e758` |
| Master | `--color-quality-master` | `#fb2424` |

### Currency Colors

| Currency | Variable | Color |
|----------|----------|-------|
| Gold | `--color-gold` | `#d4af37` |
| Gold Light | `--color-gold-light` | `#ffd54f` |
| Magic Stone | `--color-magic-stone` | `#6495ed` |

---

## 3. Typography System (タイポグラフィ)

### Font Stack

```css
--font-primary: 'Inter', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
--font-weight-normal: 400;
--font-weight-bold: 700;
--line-height-base: 1.5;
```

**Rationale:** Interは読みやすさとUI適性を兼ね備えたモダンフォント。Segoe UIはWindowsフォールバック。

### Size Scale (vh-based)

| Usage | Size | Example |
|-------|------|---------|
| 極小テキスト | `1.2vh` | バッジ、補足情報 |
| 小テキスト | `1.4vh` | カード説明、ステータス |
| 標準 | `1.6vh` | 一般UI、ボタン |
| 見出し | `2.0vh` | セクションタイトル |
| 大見出し | `2.5vh` | 画面タイトル |
| 特大 | `3.0vh+` | 重要な数値、結果表示 |

### Number Display Guidelines

```css
/* 数値は常にtabular-numsで位置を揃える */
font-variant-numeric: tabular-nums;
```

**使用場面:** HP、ダメージ、コスト、カウンター等すべての数値

### Japanese Text Considerations

- **UI text:** 日本語（仕様）
- **Code/comments:** English
- **フォントサイズ:** 日本語は欧文より大きめに見えるため、`line-height: 1.5`で行間確保
- **改行:** 日本語は任意の位置で改行可能だが、単語途中での改行は避ける

---

## 4. Spacing & Layout (スペーシング & レイアウト)

### Spacing Scale

| Token | Value | Usage |
|-------|-------|-------|
| `--spacing-xs` | `0.5vh` | 密接な要素間 |
| `--spacing-sm` | `1vh` | 関連要素間 |
| `--spacing-md` | `1.5vh` | 標準間隔 |
| `--spacing-lg` | `2vh` | セクション間 |
| `--spacing-xl` | `3vh` | 大きな区切り |

### vh/vw Philosophy

| Unit | Usage | Rationale |
|------|-------|-----------|
| `vh/vw` | サイズ、間隔、フォント | 画面サイズに応じたスケーリング |
| `px` | ボーダーのみ | 1px線の視認性確保 |

**例外:** `border-radius`は`px`を使用（小さな値では`vh`は不安定）

### Border Radius

```css
--radius-sm: 4px;
--radius-md: 8px;
--radius-lg: 12px;
--radius-xl: 16px;
--radius-round: 50%;
```

### Common Layout Patterns

**1. Card Grid:**
```css
display: grid;
grid-template-columns: repeat(auto-fill, minmax(var(--card-base-width), 1fr));
gap: var(--spacing-md);
```

**2. Status Bar:**
```css
display: flex;
justify-content: space-between;
align-items: center;
padding: var(--spacing-sm) var(--spacing-md);
```

**3. Modal Overlay:**
```css
position: fixed;
inset: 0;
display: flex;
justify-content: center;
align-items: center;
background: rgba(0, 0, 0, 0.7);
z-index: var(--z-modal);
```

---

## 5. Component Patterns (コンポーネントパターン)

### Card Component

**Structure:**
```
┌─────────────────┐
│ [Cost]          │  ← コストバッジ（左上）
│                 │
│    [Image]      │  ← カード画像
│                 │
├─────────────────┤
│ [Name]          │  ← カード名（熟練度色背景）
│ [Description]   │  ← 説明文
│           [Tag] │  ← タイプタグ（右下）
└─────────────────┘
```

**Dimensions:**
```css
--card-base-width: 13vh;
--card-aspect-ratio: 2 / 3;
```

**Card Tag Colors (by Element):**

| Element | Color | Meaning |
|---------|-------|---------|
| physics | グレー系 | 物理攻撃 |
| fire | 赤橙系 | 火属性 |
| ice | 水色系 | 氷属性 |
| lightning | 黄色系 | 雷属性 |
| dark | 紫暗系 | 闘属性 |
| light | 白金系 | 光属性 |
| heal | 緑系 | 回復 |
| guard | 青系 | 防御 |
| buff | 緑黄系 | バフ |

### Button Hierarchy

| Level | Usage | Style |
|-------|-------|-------|
| Primary | 主要アクション（購入、確定） | 施設テーマカラー、塗りつぶし |
| Secondary | 副次アクション（キャンセル、詳細） | ボーダーのみ、透明背景 |
| Tertiary | 補助アクション（閉じる） | テキストのみ |
| Danger | 破壊的アクション（削除） | `--color-error`使用 |

### Panel Pattern

```css
.panel {
  background: var(--theme-secondary);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
}
```

### Modal Pattern

- **Overlay:** `rgba(0, 0, 0, 0.7)`
- **Animation:** `scaleIn` (0.3s)
- **Close:** ESCキー、オーバーレイクリック、×ボタン

### Badge Pattern

| Type | Background | Text |
|------|------------|------|
| コストバッジ | 半透明黒 | 白 |
| 数量バッジ | `--color-gold` | 黒 |
| ステータスバッジ | セマンティックカラー | 白 |

### Progress Bar Pattern

```css
.progress-bar {
  height: 1vh;
  background: rgba(0, 0, 0, 0.3);
  border-radius: var(--radius-sm);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--color-hp);  /* or semantic color */
  transition: width var(--transition-normal);
}
```

---

## 6. Animation & Motion (アニメーション)

### Animation Principles

| Principle | Description |
|-----------|-------------|
| **Purpose-driven** | 状態変化、フィードバック、注目誘導のみ |
| **Performance** | `transform`と`opacity`のみ（GPU加速） |
| **Duration Limit** | 最大3秒（ユーザーを待たせない） |
| **Reduced Motion** | `prefers-reduced-motion`サポート必須 |

### Timing Tokens

```css
--transition-fast: 0.2s ease;    /* ホバー、小さな変化 */
--transition-normal: 0.3s ease;  /* 標準トランジション */
--transition-slow: 0.4s ease;    /* モーダル、大きな変化 */
```

### Animation Catalog

**Reference:** `src/ui/css/animations/keyframes.css`

| Category | Animation | Duration | Usage |
|----------|-----------|----------|-------|
| **Notification** | `notification-slide` | 3s | トースト通知 |
| **Notification** | `fadeInOut` | 2s | 短い通知 |
| **Modal** | `fadeIn` | 0.3s | オーバーレイ |
| **Modal** | `scaleIn` | 0.3s | モーダルコンテンツ |
| **Battle** | `cardDrawIn` | 0.8s | カードドロー |
| **Battle** | `discardToPile` | 0.5s | カード捨て |
| **Battle** | `turnMessageAppear` | 1.5s | ターン表示 |
| **Battle** | `breakFlash` | 0.6s | ブレイク状態 |
| **Battle** | `energyConsume` | 0.6s | エネルギー消費 |
| **Effects** | `swordEnergyMaxPulse` | 1s | 剣気MAX |
| **Effects** | `maxEnergyPulse` | 0.5s | エネルギーMAX |
| **Tooltip** | `tooltipFadeIn` | 0.2s | ツールチップ |
| **Sanctuary** | `pulse-available` | 2s | 利用可能 |
| **Sanctuary** | `unlock-success` | 0.5s | 解放成功 |
| **Camp** | `twinkle` | 2s | 星のきらめき |
| **Camp** | `fogDrift` | 4s | 霧の動き |
| **Camp** | `titleGlow` | 3s | タイトル発光 |
| **Shop** | `packOpen` | 0.3s | パック開封 |

### Card Animation Constants

**Reference:** `src/constants/uiConstants.ts`

```typescript
CARD_ANIMATION = {
  DRAW_INTERVAL: 150,      // カード間の遅延
  DRAW_DURATION: 800,      // ドローアニメーション
  DISCARD_DURATION: 500,   // 捨てアニメーション
  PLAY_DURATION: 400,      // プレイアニメーション
}
```

### Reduced Motion Support

```css
@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
  }
}
```

---

## 7. Icon & Image System (アイコン)

### Asset Structure

```
/assets/
├── icons/
│   └── header/           # ヘッダーアイコン
│       ├── heart-icon.png
│       ├── gold-icon.png
│       ├── magic-stone-*.png
│       └── dropdown-icon.png
├── images/
│   ├── player-character/ # プレイヤー画像
│   ├── elements/         # 属性アイコン
│   └── depth-backgrounds/# 深度背景
```

### Naming Convention

- **ケバブケース:** `magic-stone-large.png`
- **サイズサフィックス:** `small`, `medium`, `large`, `huge`
- **状態サフィックス:** `-active`, `-disabled`, `-hover`

### Size Guidelines

| Context | Size | Example |
|---------|------|---------|
| ヘッダーアイコン | `2vh` | ライフ、ゴールド |
| カード内アイコン | `1.5vh` | コスト、属性 |
| ステータスバッジ | `1.2vh` | バフ/デバフ |
| 大型表示 | `4-6vh` | キャラクター選択 |

### Icon + Text Pairing

**原則:** 色だけでなく、必ずアイコンまたはテキストを併用

```css
.status-indicator {
  display: flex;
  align-items: center;
  gap: var(--spacing-xs);
}

/* 良い例: アイコン + 数値 */
<span class="hp-indicator">
  <img src="heart-icon.png" />
  <span>25</span>
</span>

/* 悪い例: 色だけ */
<span style="color: red;">25</span>
```

---

## 8. State & Feedback Design (状態 & フィードバック)

### Interactive States

| State | Visual Change | Timing |
|-------|---------------|--------|
| **Default** | 基本スタイル | - |
| **Hover** | 明度上昇、軽い浮き | `0.2s` |
| **Active/Pressed** | 縮小、暗転 | 即時 |
| **Focus** | アウトライン表示 | 即時 |
| **Disabled** | 不透明度50%、カーソル変更 | - |
| **Loading** | スピナー、操作無効 | - |
| **Selected** | ボーダー強調、グロー | - |

### Toast Notification System

| Type | Color | Icon | Duration |
|------|-------|------|----------|
| Success | `--color-success` | ✓ | 3s |
| Error | `--color-error` | ✗ | 5s |
| Warning | `--color-warning` | ⚠ | 4s |
| Info | `--theme-accent` | ℹ | 3s |

**Position:** 画面上部中央、`z-index: 1100`

### Error Handling UI

1. **インラインエラー:** フォーム要素直下に赤テキスト
2. **トーストエラー:** 操作失敗時の即時フィードバック
3. **モーダルエラー:** 重大なエラー、ユーザーアクション必要

### Empty States

| Context | Message | Action |
|---------|---------|--------|
| デッキが空 | 「カードがありません」 | ショップへ誘導 |
| 在庫なし | 「売り切れです」 | 再入荷情報 |
| 検索結果なし | 「該当するカードがありません」 | フィルターリセット |

---

## 9. Accessibility (アクセシビリティ)

### Contrast Requirements

| Element | Minimum Ratio |
|---------|---------------|
| 本文テキスト | 4.5:1 |
| 大きなテキスト (18px+) | 3:1 |
| UIコンポーネント | 3:1 |
| アイコン | 3:1 |

### Color Independence

**原則:** 色だけで情報を伝えない

| Bad | Good |
|-----|------|
| 赤=敵、緑=味方 | 赤+敵アイコン、緑+味方アイコン |
| 色付きHP | 色+数値+バー |

### Focus States

```css
*:focus-visible {
  outline: 2px solid var(--theme-accent);
  outline-offset: 2px;
}
```

### Motion Considerations

- すべてのアニメーションに`prefers-reduced-motion`対応
- 点滅は3回/秒以下
- 自動再生アニメーションは停止可能に

---

## 10. Depth-Based Theming (深度テーマ)

### Depth Color Progression

| Depth | Theme | Atmosphere |
|-------|-------|------------|
| 1 | 紫暗 | 入口、不気味だが明るめ |
| 2 | 青暗 | 中層、冷たさ |
| 3 | 赤暗 | 深層、危険 |
| 4-5 | 漆黒 | 深淵、絶望 |

**Background Images:**
```typescript
DEPTH_BACKGROUND_IMAGES: Record<number, string> = {
  1: "/assets/images/depth-backgrounds/depth_1_background.png",
  2: "/assets/images/depth-backgrounds/depth_2_background.png",
  // ...
}
```

### Life System Visual Feedback

| Lives | Color | Effect |
|-------|-------|--------|
| 7-10 | 通常 | なし |
| 4-6 | 黄色系 | 警告 |
| 1-3 | 赤 | `critical-pulse`アニメーション |

---

## Appendix

### CSS File Reference Map

| File | Purpose |
|------|---------|
| `src/ui/css/core/variables.css` | CSS変数定義（色、間隔、フォント） |
| `src/ui/css/core/reset.css` | ブラウザリセット |
| `src/ui/css/animations/keyframes.css` | @keyframes定義 |
| `src/ui/css/components/` | コンポーネントスタイル |
| `src/ui/css/screens/` | 画面固有スタイル |

### Quick Reference

**Colors:**
- 背景: `#1a1a24` / `#2a2a3d`
- アクセント: `#8b7aa8`
- HP: `#d94a4a` / Guard: `#4488cc` / Energy: `#2b9f0e`

**Spacing:**
- `0.5vh` / `1vh` / `1.5vh` / `2vh` / `3vh`

**Timing:**
- Fast: `0.2s` / Normal: `0.3s` / Slow: `0.4s`

**Z-Index:**
- Base: `1` / Dropdown: `100` / Modal: `1000` / Notification: `1100` / Tooltip: `1200`

### New Component Checklist

- [ ] CSS変数を使用（ハードコード禁止）
- [ ] 数値は`font-variant-numeric: tabular-nums`
- [ ] 色+アイコン/テキストで情報伝達
- [ ] ホバー/フォーカス/無効状態を実装
- [ ] アニメーションは`transform`/`opacity`のみ
- [ ] `prefers-reduced-motion`対応
- [ ] 親クラスでスコープ（`.screen-name .component`）
- [ ] サイズは`vh/vw`（ボーダーのみ`px`）
- [ ] 深度1/3/5でテスト
- [ ] ライフ1-2でテスト
