---
name: dark-fantasy-cardgame-ui
description: ダークファンタジー×カードバトルゲーム特化のUI/UXスキル。心理的プレッシャー、戦略的フィードバック、深度表現に焦点を当てた視覚デザインガイド。
extends: frontend-design
category: game-ui
---

# Dark Fantasy Card Game UI/UX Skill

このスキルは、ダークファンタジー美学を持つカードバトルゲーム（特にローグライク要素を含む）のUI/UX設計と実装をガイドします。frontend-designスキルを拡張し、ゲーム特有の要素に特化しています。

## Core Philosophy

### Game Context
- **Genre**: 持ち帰り型ダンジョンRPG × カードバトル
- **Theme**: ダークファンタジー、深度探索、リスク/リターン
- **Player Psychology**: 緊張感、戦略的判断、探索回数制限によるプレッシャー
- **Victory Condition**: 深く潜り、生きて帰還すること

### Design Principles
1. **Clarity over Decoration**: 情報は明確に、装飾は目的を持って
2. **Pressure through Simplicity**: シンプルな表現で心理的圧力を生む
3. **Depth Visualization**: 深度進行を視覚的に感じさせる
4. **Strategic Feedback**: プレイヤーの判断を支援する即座のフィードバック

---

## 1. Dark Fantasy Aesthetics

### Color Palette Philosophy
ダークファンタジーの雰囲気を保ちつつ、戦略ゲームとしての視認性を確保。

```css
/* CSS Variables - Dark Fantasy Theme */
:root {
  /* Base Colors - 深度レベル0（地上） */
  --bg-surface: #1a1a1f;
  --bg-elevated: #25252e;
  --text-primary: #e8e8ea;
  --text-secondary: #a8a8b2;
  
  /* Depth Progression Colors */
  --depth-1-5: #2a2535;    /* 薄紫の暗さ */
  --depth-6-10: #252a35;   /* 深い青暗さ */
  --depth-11-15: #2a2525;  /* 赤黒い暗さ */
  --depth-16-20: #1a1a1a;  /* 最深部の闇 */
  
  /* Accent Colors - 機能別 */
  --accent-danger: #d32f2f;    /* HP減少、危険 */
  --accent-warning: #f57c00;   /* 注意、コスト */
  --accent-success: #388e3c;   /* 回復、成功 */
  --accent-info: #1976d2;      /* 情報、魔法 */
  
  /* Status Colors */
  --status-buff: #4caf50;      /* バフ効果 */
  --status-debuff: #e64a19;    /* デバフ効果 */
  --status-neutral: #757575;   /* 中立効果 */
  
  /* Card Rarity Colors */
  --rarity-common: #78909c;    /* コモン */
  --rarity-rare: #5e35b1;      /* レア */
  --rarity-epic: #d84315;      /* エピック */
  
  /* UI Elements */
  --border-subtle: rgba(255, 255, 255, 0.08);
  --border-emphasis: rgba(255, 255, 255, 0.16);
  --shadow-depth: 0 4px 12px rgba(0, 0, 0, 0.4);
}
```

### Typography
**読みやすさを最優先**しつつ、ダークファンタジーの雰囲気を演出。

```css
/* Typography System */
@import url('https://fonts.googleapis.com/css2?family=Cinzel:wght@600;700&family=Noto+Sans+JP:wght@400;500;700&display=swap');

:root {
  /* Display Font - ゲームタイトル、重要な見出し */
  --font-display: 'Cinzel', serif;
  
  /* Body Font - UI、カード説明、ステータス */
  --font-body: 'Noto Sans JP', sans-serif;
  
  /* Mono Font - 数値、HP、ダメージ */
  --font-mono: 'SF Mono', 'Consolas', monospace;
  
  /* Font Sizes */
  --text-xs: 0.75rem;   /* 12px - 補足情報 */
  --text-sm: 0.875rem;  /* 14px - カード説明 */
  --text-base: 1rem;    /* 16px - 標準UI */
  --text-lg: 1.125rem;  /* 18px - カード名 */
  --text-xl: 1.25rem;   /* 20px - セクション見出し */
  --text-2xl: 1.5rem;   /* 24px - 大きな数値 */
  --text-3xl: 2rem;     /* 32px - タイトル */
}

/* Usage Examples */
.card-title {
  font-family: var(--font-body);
  font-size: var(--text-lg);
  font-weight: 700;
  letter-spacing: 0.02em;
}

.damage-number {
  font-family: var(--font-mono);
  font-size: var(--text-2xl);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.section-header {
  font-family: var(--font-display);
  font-size: var(--text-xl);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.1em;
}
```

### Visual Texture & Atmosphere
控えめだが効果的な質感表現。

```css
/* Subtle Background Noise - 全体の質感 */
.game-container {
  background-image: 
    radial-gradient(circle at 20% 80%, rgba(138, 43, 226, 0.03) 0%, transparent 50%),
    url("data:image/svg+xml,%3Csvg viewBox='0 0 400 400' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noise'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' /%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noise)' opacity='0.03'/%3E%3C/svg%3E");
  background-color: var(--bg-surface);
}

/* Card Border Glow - カード選択時 */
.card:hover {
  box-shadow: 
    0 0 20px rgba(138, 43, 226, 0.3),
    inset 0 0 20px rgba(138, 43, 226, 0.05);
  border: 1px solid rgba(138, 43, 226, 0.5);
}

/* Depth Vignette - 深度が深いほど暗く */
.dungeon-view[data-depth="deep"] {
  box-shadow: inset 0 0 120px rgba(0, 0, 0, 0.7);
}
```

---

## 2. Psychological Pressure Design

### Exploration Count Display (残機表示)
探索回数制限（最大10回）を強調し、プレイヤーに緊張感を与える。

**Design Rationale**:
- 大きな数字で残機を強調
- 色の変化で危機感を段階的に伝達
- ドット表示で直感的な残量把握
- アニメーションは危険時のみ（パルス）

### Depth Indicator (深度表示)
現在の深度を明確に表示し、進行度と危険度を可視化。

### Risk/Return Visualization
選択肢のリスクとリターンを視覚的に明示。

---

## 3. Battle Feedback Patterns

### Damage Display
ダメージ表示は明確で即座に理解できること。

```css
/* Damage Float Animation */
@keyframes damage-float {
  0% {
    opacity: 1;
    transform: translateY(0) scale(0.8);
  }
  20% {
    transform: translateY(-10px) scale(1.2);
  }
  100% {
    opacity: 0;
    transform: translateY(-50px) scale(1);
  }
}

.animate-damage-float {
  animation: damage-float 1s ease-out forwards;
}
```

### Status Effect Indicators
バフ/デバフの視覚表現。アイコンと持続ターン数を明示。

### Card Play Feedback
カードプレイ時の視覚フィードバック。

```css
/* Card Play Animation - シンプルかつ効果的 */
@keyframes card-play {
  0% {
    transform: translateY(0) scale(1);
    opacity: 1;
  }
  50% {
    transform: translateY(-20px) scale(1.05);
  }
  100% {
    transform: translateY(-100px) scale(0.8);
    opacity: 0;
  }
}

.card-playing {
  animation: card-play 0.5s ease-out forwards;
  pointer-events: none;
}

/* Card Selection Glow */
.card-selected {
  box-shadow: 
    0 0 20px rgba(138, 43, 226, 0.6),
    0 0 40px rgba(138, 43, 226, 0.3);
  border-color: rgba(138, 43, 226, 0.8);
  transform: translateY(-4px);
}
```

---

## 4. Dungeon Exploration UI

### Room Transition
部屋移動時のシンプルな遷移効果。

```css
/* Fade Transition for Room Changes */
.room-transition-enter {
  opacity: 0;
  transform: scale(0.95);
}

.room-transition-enter-active {
  opacity: 1;
  transform: scale(1);
  transition: opacity 300ms ease-out, transform 300ms ease-out;
}

.room-transition-exit {
  opacity: 1;
}

.room-transition-exit-active {
  opacity: 0;
  transition: opacity 200ms ease-in;
}
```

### Carry-Back Progress Indicator
持ち帰り要素の進行状況表示。死亡時に失うリスクを明示。

---

## 5. Card Game Specifics

### Card Layout & Interaction
Slay the Spireスタイルを参考にした、明確で使いやすいカード表示。

**Key Elements**:
- コストバッジ（左上）
- カード画像（上部）
- カード名と説明（中央）
- タイプラベル（右下）
- レアリティによる枠線の色分け

### Hand Layout
手札の配置と管理。

```css
/* Card Hand Container */
.card-hand {
  display: flex;
  justify-content: center;
  align-items: flex-end;
  gap: -2rem; /* Overlap cards slightly */
  padding: 1rem;
  perspective: 1000px;
}

.card-hand .game-card {
  transition: transform 0.2s ease-out;
}

/* Fan out effect on hover */
.card-hand:hover .game-card {
  margin: 0 0.5rem;
}

.card-hand .game-card:hover {
  transform: translateY(-2rem) scale(1.05);
  z-index: 10;
}
```

---

## 6. Accessibility & Clarity

### Readability First
すべてのテキストは明瞭で読みやすく。

```css
/* Text Readability Standards */
.text-readable {
  /* Minimum contrast ratio: 4.5:1 for normal text, 3:1 for large text */
  color: var(--text-primary);
  background: var(--bg-surface);
  
  /* Optimal line length */
  max-width: 65ch;
  
  /* Comfortable line height */
  line-height: 1.6;
  
  /* Prevent orphans */
  text-wrap: pretty;
}

/* Large Numbers - Always High Contrast */
.number-display {
  color: #ffffff;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.8);
  font-variant-numeric: tabular-nums;
}
```

### Color Blindness Consideration
色だけに依存しない情報伝達。

**Best Practices**:
- HPバーには数値表示を併記
- 危険状態は色+アイコン+テキストで表現
- ステータス効果はアイコンを必ず使用

### Focus States
キーボードナビゲーションのサポート。

```css
/* Clear focus indicators */
*:focus-visible {
  outline: 2px solid var(--accent-info);
  outline-offset: 2px;
  border-radius: 4px;
}

/* Interactive elements */
button:focus-visible,
.card:focus-visible {
  outline: 3px solid rgba(138, 43, 226, 0.8);
  outline-offset: 4px;
}
```

---

## 7. Strategic Animation Use

### Animation Principles
アニメーションは**意味のある瞬間**にのみ使用。

1. **State Changes**: HP変化、ステータス付与時
2. **User Feedback**: クリック、ホバー、カードプレイ
3. **Attention Direction**: 重要な情報、危険な状態
4. **Transitions**: 画面遷移、部屋移動

### Performance-First Animations
CSS-onlyアニメーション、transform/opacityの使用。

```css
/* Good: GPU-accelerated */
.smooth-animation {
  transition: transform 0.3s ease-out, opacity 0.3s ease-out;
}

.smooth-animation:hover {
  transform: translateY(-4px) scale(1.05);
}

/* Avoid: Causes reflow */
.avoid-this {
  transition: width 0.3s, height 0.3s, top 0.3s;
}
```

### Limited Animation Examples

```css
/* Pulse for critical warnings only */
@keyframes critical-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}

.critical-warning {
  animation: critical-pulse 1.5s ease-in-out infinite;
}

/* Subtle hover lift */
.card-hover {
  transition: transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.card-hover:hover {
  transform: translateY(-8px);
}

/* Quick fade for state changes */
.fade-in {
  animation: fadeIn 0.3s ease-out;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}
```

---

## Implementation Checklist

### Before Starting
- [ ] 現在の深度レベルを確認
- [ ] プレイヤーの残機状況を把握
- [ ] 実装するUIコンポーネントの優先度を決定

### During Implementation
- [ ] CSS変数を使用して一貫したテーマを適用
- [ ] 数値表示は必ずモノスペースフォント
- [ ] 色だけでなくアイコン/テキストで情報を補完
- [ ] アニメーションは必要最小限（3秒以内で完了）
- [ ] 全ての重要情報が視認可能（コントラスト比4.5:1以上）

### After Implementation
- [ ] 深度1, 10, 20でのビジュアル確認
- [ ] 残機が少ない状態（1-2回）での表示確認
- [ ] カラーブラインドシミュレーターでチェック
- [ ] キーボードのみでの操作性確認
- [ ] パフォーマンス測定（60fps維持）

---

## Related Resources

- **Parent Skill**: frontend-design
- **Project Docs**: `/docs/Overall_document/`, `/docs/battle_document/`
- **Design References**: Slay the Spire, Darkest Dungeon, Inscryption

---

## Notes

このスキルは**実装ガイド**であり、**デザインシステム**です。各セクションのコード例は実際に動作するよう設計されていますが、プロジェクトの具体的な要件に応じて調整してください。

重要なのは、**プレイヤーの戦略的判断を支援するUI**を作ることです。装飾は雰囲気作りのためですが、情報の明確さを犠牲にしてはいけません。
