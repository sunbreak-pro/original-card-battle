# PixiJS統合 Phase 1: 基盤構築

## Change History

| Date | Content |
|------|---------|
| 2026-02-22 | Initial creation based on engine integration proposal |

---

## 1. Overview

PixiJS v8 と @pixi/react v8 をプロジェクトに導入し、BattleScreen にキャンバスレイヤーを追加する基盤を構築する。既存のDOMベースUIは維持しつつ、エフェクト描画専用のPixiJSレイヤーを重ねる「ハイブリッドレンダリング」アーキテクチャを確立する。

### Goals

- PixiJS v8 の Vite + React 19 + TypeScript 環境への統合
- BattleScreen へのキャンバスオーバーレイ追加
- React Context → PixiJS のデータブリッジ確立
- 既存アニメーションとの並行動作確認
- 将来の他画面展開を見据えた共通基盤設計

### Non-Goals (Phase 1 では対象外)

- 既存DOMアニメーションの置換（Phase 2）
- 敵画像・スプライトシートの作成（Phase 3）
- パフォーマンス最適化（Phase 4）

---

## 2. Technical Design

### 2.1 パッケージ導入

```bash
npm install pixi.js @pixi/react
npm install @pixi/particle-emitter
```

**バージョン要件:**
- `pixi.js` >= 8.16.0
- `@pixi/react` >= 8.x（React 19対応版）
- `@pixi/particle-emitter` >= 5.x（PixiJS v8対応版）

### 2.2 Vite設定

```typescript
// vite.config.ts への追加
export default defineConfig({
  // ... existing config
  optimizeDeps: {
    include: ['pixi.js', '@pixi/react']
  }
});
```

### 2.3 共通基盤: PixiStage コンポーネント

他画面にも展開可能な共通の Stage ラッパーを設計する。

```
src/ui/pixi/
├── core/
│   ├── PixiStage.tsx           # 共通Stage wrapper (@pixi/react の extend + Application)
│   ├── PixiContextBridge.tsx    # React Context → PixiJS bridge
│   └── usePixiApp.ts           # PixiJS Application access hook
├── battle/
│   ├── BattleCanvas.tsx         # バトル画面専用のPixiJS描画レイヤー
│   ├── layers/
│   │   ├── BackgroundLayer.tsx  # 背景レイヤー（Phase 3 で本格化）
│   │   ├── CharacterLayer.tsx   # キャラクター描画レイヤー（Phase 3 で本格化）
│   │   └── EffectLayer.tsx      # エフェクト描画レイヤー（Phase 2 で本格化）
│   ├── effects/                 # Phase 2 で個別エフェクトコンポーネントを追加
│   │   └── (Phase 2 で DamageEffect.tsx 等を追加)
│   └── PixiEffectBridge.ts     # 既存hooks → PixiJS エフェクト呼び出しのオーケストレーター
├── shared/
│   ├── particles/
│   │   └── ParticlePresets.ts   # パーティクル設定プリセット集
│   └── textures/
│       └── TextureManager.ts    # テクスチャ読み込み・キャッシュ管理
└── types/
    └── pixiTypes.ts             # PixiJS関連の型定義
```

### 2.4 PixiStage 設計

```typescript
// src/ui/pixi/core/PixiStage.tsx
interface PixiStageProps {
  width: number;
  height: number;
  children: React.ReactNode;
  className?: string;
  transparent?: boolean;     // DOM上にオーバーレイするため基本 true
  resolution?: number;       // devicePixelRatio 対応
}
```

**設計方針:**
- `transparent: true` でDOMの上にオーバーレイ
- `resolution: window.devicePixelRatio` で高解像度対応
- `pointer-events: none` でDOM側のクリックイベントを妨げない
- `position: absolute; top: 0; left: 0; z-index: 上位` でバトルフィールド上に配置

### 2.5 Context Bridge パターン

@pixi/react v8 ではReact Contextが直接伝播しないため、ブリッジが必要。

```typescript
// src/ui/pixi/core/PixiContextBridge.tsx
// BattleState を PixiJS 子コンポーネントに渡すブリッジ
interface BattlePixiContext {
  playerHP: number;
  playerMaxHP: number;
  enemyHP: number;
  enemyMaxHP: number;
  currentPhase: string;
  activeBuffs: ActiveBuff[];
  // ... Phase 2 以降で拡張
}
```

**ブリッジ対象のContext:**
- useBattleState の結果（HP, AP, Guard, Buffs）→ エフェクトトリガーに使用
- useCardExecution の結果 → カード発動エフェクト連動

**ブリッジしないContext（DOM側で処理）:**
- ResourceProvider（ゴールド等はUI表示のみ）
- InventoryProvider（アイテムUIのみ）
- SettingsProvider（設定はDOM管理）

### 2.6 BattleScreen への統合

```
BattleScreen (既存JSX)
├── battle-header (React DOM, z-index: 10)
├── battle-field (React DOM, z-index: 1)
│   ├── EnemyFrame (React DOM)
│   └── PlayerFrame (React DOM)
├── BattleCanvas (PixiJS, z-index: 5)  ← NEW
│   ├── BackgroundLayer   (placeholder)
│   ├── CharacterLayer    (placeholder)
│   └── EffectLayer       (placeholder)
├── hand-container (React DOM, z-index: 10)
└── modals (React DOM, z-index: 100)
```

**レイヤーの z-index 設計:**
| Layer | z-index | 内容 |
|-------|---------|------|
| battle-field | 1 | 既存のキャラクターフレーム |
| BattleCanvas | 5 | PixiJS エフェクト描画 |
| battle-header | 10 | ターン情報、クラスアビリティ |
| hand-container | 10 | カード手札 |
| modals | 100 | モーダルオーバーレイ |

### 2.7 PixiEffectBridge 設計

既存の `useCardAnimation` と `animationEngine.ts` の呼び出しを、PixiJS 側でも処理できるようにするブリッジ。Phase 1 では骨格のみ実装し、Phase 2 で中身を実装する。

```typescript
// src/ui/pixi/battle/PixiEffectBridge.ts
interface PixiEffectBridge {
  // Phase 2 で実装するエフェクトAPI
  playDamageEffect(target: 'player' | 'enemy', damage: number, isCritical: boolean): void;
  playHealEffect(target: 'player' | 'enemy', amount: number): void;
  playShieldEffect(target: 'player' | 'enemy', amount: number): void;
  playCardEffect(cardElement: ElementType[], position: { x: number; y: number }): void;
  playBuffEffect(buffType: string, target: 'player' | 'enemy'): void;

  // Phase 1 で実装するテスト用エフェクト
  playTestParticle(x: number, y: number, color: string): void;
}
```

### 2.8 テクスチャマネージャー

```typescript
// src/ui/pixi/shared/textures/TextureManager.ts
// Phase 1: 基本構造のみ。Phase 3 でスプライトシート対応を追加
class TextureManager {
  private cache: Map<string, Texture>;

  async loadTexture(key: string, url: string): Promise<Texture>;
  async loadSpriteSheet(key: string, jsonUrl: string): Promise<Spritesheet>;
  getTexture(key: string): Texture | undefined;
  destroy(): void;
}
```

---

## 3. 他画面展開の設計指針

Phase 1 では BattleScreen のみに導入するが、共通基盤は以下の画面展開を想定して設計する。

### 3.1 将来のPixiJS適用候補画面

| 画面 | 適用範囲 | 優先度 | 想定Phase |
|------|----------|--------|-----------|
| BattleScreen | エフェクト・キャラクター描画 | 最高 | Phase 1-2 |
| ExplorationScreen (NodeMap) | ノード間パーティクル演出 | 中 | Phase 4+ |
| BaseCamp | キャンプファイヤー・環境エフェクト | 低 | Phase 4+ |
| GuildBattleScreen | BattleScreenと共通 | 最高 | Phase 2（自動対応） |

### 3.2 共通テーマ統一方針

全画面で統一すべきビジュアル要素:
- **カラーパレット**: ダークファンタジー基調（#1a1a24 ベース）
- **パーティクルスタイル**: グロー重視、柔らかい光の粒子
- **属性カラー**: 既存の属性色をPixiJSエフェクトにも適用
  - 火: #ff4400, 氷: #00ccff, 雷: #ffcc00, 闇: #8800ff, 光: #ffffaa
  - 物理: #cccccc, 回復: #44ff44
- **トランジション**: フェードイン/アウト統一（300ms, easeOutCubic）

---

## 4. Implementation Steps

### Step 1: パッケージインストールと Vite 設定
- npm install で必要パッケージを追加
- **vite.config.ts** の optimizeDeps 設定（重要: PixiJS の事前バンドル設定）
- tsconfig.app.json のパスエイリアス確認（@/* は既存）
- `public/assets/spritesheets/` ディレクトリ構造の作成（Phase 3 のアセット配置先）

### Step 2: PixiStage 共通コンポーネント作成
- `src/ui/pixi/core/PixiStage.tsx` 作成
- @pixi/react の `extend()` で必要なPixiJSオブジェクトを登録
- Application 初期化設定（transparent, resolution, antialias）

### Step 3: PixiContextBridge 作成
- BattleState を PixiJS 子コンポーネントに伝播するブリッジ実装
- usePixiApp フック作成

### Step 4: BattleCanvas コンポーネント作成
- BattleScreen 内にオーバーレイ配置
- 3つのレイヤー（Background, Character, Effect）の空コンテナ作成
- CSS設定（position: absolute, pointer-events: none, z-index: 5）

### Step 5: BattleScreen への統合
- BattleScreen.tsx に BattleCanvas を追加
- 既存DOMレンダリングとの共存確認
- リサイズ対応（vh/vw → PixiJS viewport同期）

### Step 6: テストパーティクル実装
- EffectLayer に簡単なテストパーティクル表示
- クリックイベントでDOMとPixiJSの動作確認
- パフォーマンスベースライン計測

### Step 7: PixiEffectBridge 骨格実装
- ブリッジインターフェース定義
- playTestParticle のみ実装
- 既存の useCardAnimation から呼び出し可能なことを確認

---

## 5. Affected Files

### New Files
| File | Purpose |
|------|---------|
| `src/ui/pixi/core/PixiStage.tsx` | 共通Stage wrapper |
| `src/ui/pixi/core/PixiContextBridge.tsx` | Context bridge |
| `src/ui/pixi/core/usePixiApp.ts` | Application access hook |
| `src/ui/pixi/battle/BattleCanvas.tsx` | バトル画面PixiJSレイヤー |
| `src/ui/pixi/battle/layers/BackgroundLayer.tsx` | 背景レイヤー（空） |
| `src/ui/pixi/battle/layers/CharacterLayer.tsx` | キャラレイヤー（空） |
| `src/ui/pixi/battle/layers/EffectLayer.tsx` | エフェクトレイヤー（テスト用） |
| `src/ui/pixi/battle/PixiEffectBridge.ts` | エフェクトブリッジ |
| `src/ui/pixi/shared/textures/TextureManager.ts` | テクスチャ管理 |
| `src/ui/pixi/shared/particles/ParticlePresets.ts` | パーティクルプリセット |
| `src/ui/pixi/types/pixiTypes.ts` | 型定義 |

### Modified Files
| File | Change |
|------|--------|
| `package.json` | pixi.js, @pixi/react, @pixi/particle-emitter 追加 |
| `vite.config.ts` | optimizeDeps 追加 |
| `src/ui/html/battleHtml/BattleScreen.tsx` | BattleCanvas コンポーネント追加 |

### Immutable Files (DO NOT MODIFY)
- `src/domain/cards/decks/deck.ts`
- `src/domain/cards/decks/deckReducer.ts`

---

## 6. Testing Strategy

### Unit Tests
- `PixiStage` のレンダリング確認（@testing-library/react）
- `TextureManager` のキャッシュ動作テスト
- `PixiEffectBridge` のインターフェース呼び出しテスト

### Integration Tests
- BattleScreen + BattleCanvas の共存確認
- Context Bridge 経由のデータ伝播テスト
- リサイズ時の座標同期テスト

### Manual Verification
- バトル画面でPixiJS キャンバスが透明オーバーレイとして表示される
- 既存のカード操作（選択、発動）が正常に動作する
- テストパーティクルがキャンバス上に表示される
- ブラウザの開発者ツールで WebGL コンテキスト生成を確認

---

## 7. Acceptance Criteria

- [ ] `npm run build` がエラーなく完了する
- [ ] バトル画面にPixiJSキャンバスがオーバーレイ表示される
- [ ] 既存のDOMベースUI操作（カード選択、ボタン操作）が妨げられない
- [ ] テストパーティクルがキャンバス上に描画される
- [ ] Context Bridge 経由で battleState がPixiJS側に伝播する
- [ ] ブラウザリサイズ時にキャンバスサイズが追従する
- [ ] `npm run test:run` で既存テストが全てパスする

---

## 8. Dependencies

- **Blocks**: Phase 2（エフェクト移行）、Phase 3（アセット）、Phase 4（ポリッシュ）
- **Blocked by**: なし（初期フェーズ）

## 9. Risks

| Risk | Mitigation |
|------|------------|
| @pixi/react v8 の React 19 互換性問題 | 導入初期に小規模PoC で検証 |
| WebGL コンテキスト上限（ブラウザ制限） | 1つの Application を画面間で共有する設計 |
| pointer-events: none が一部ブラウザで不安定 | 条件付きCSS + フォールバック |

## 10. References

- PixiJS v8 公式ドキュメント: https://pixijs.com/
- @pixi/react v8: https://react.pixijs.io/
- @pixi/particle-emitter: https://particle-emitter.pixijs.io/docs/
- 既存アーキテクチャ: `CLAUDE.md` Architecture Overview
- 既存バトルシステム: `.claude/docs/battle_design.md`
