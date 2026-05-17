# PixiJS統合 Phase 4: ポリッシュ・最適化・拡張判断

## Change History

| Date | Content |
|------|---------|
| 2026-02-22 | Initial creation based on engine integration proposal |

---

## 1. Overview

Phase 1-3 で構築した PixiJS エコシステムの品質仕上げを行う。レガシーDOMアニメーションコードの完全削除、パフォーマンス最適化、他画面への PixiJS 展開、そして Spine スケルタルアニメーション導入の是非判断を実施する。

### Goals

- 既存DOMパーティクル/アニメーションコードの完全削除
- Feature Flag の除去とコード簡素化
- バンドルサイズ最適化（ツリーシェイキング）
- モバイルパフォーマンス最適化
- ダンジョンマップ・キャンプ画面への PixiJS 展開（選択的）
- Spine 導入の判断と PoC（Go/No-Go）
- ドキュメント更新

### Non-Goals

- 新機能追加（Quest System 等は別タスク）
- ゲームバランス調整
- 新キャラクタークラス追加

---

## 2. レガシーコード削除

### 2.1 削除対象のDOMアニメーションコード

Phase 2 で PixiJS に移行済みの以下のコードを完全に削除する。

| ファイル | 削除対象 | 置換先 |
|---------|----------|--------|
| `src/ui/animations/animationEngine.ts` | `createParticles()` 関数 | PixiJS ParticleEmitter |
| `src/ui/animations/animationEngine.ts` | `showDamageText()` 関数 | PixiJS DamageEffect |
| `src/ui/animations/animationEngine.ts` | `shakeElement()` 関数 | PixiJS ScreenShake |
| `src/ui/html/componentsHtml/useCardAnimation.tsx` | `showDamageEffect()` | PixiEffectBridge |
| `src/ui/html/componentsHtml/useCardAnimation.tsx` | `showHealEffect()` | PixiEffectBridge |
| `src/ui/html/componentsHtml/useCardAnimation.tsx` | `showShieldEffect()` | PixiEffectBridge |
| `src/ui/css/animations/*.css` | パーティクル関連keyframes | 不要 |

**残すもの:**
- `animateAsync()` — DOM UI要素のトランジション（カード移動等）に引き続き使用
- `Easing` オブジェクト — PixiJS 側でも参照可能な共通イージング
- カード手札のCSS アニメーション — DOM UI はそのまま

### 2.2 Feature Flag 削除

```typescript
// 削除対象（src/constants/uiConstants.ts）
export const FEATURE_FLAGS = {
  usePixiEffects: true,      // → 削除、PixiJS が唯一の実装に
  usePixiDamageText: true,   // → 削除
  usePixiScreenShake: true,  // → 削除
  usePixiFilters: true,      // → 削除
};
```

Feature Flag に依存する分岐コードをすべてPixiJS版に統一し、DOM版の分岐を削除。

### 2.3 不要になるCSS

```
削除候補:
- @keyframes particle-burst (animationEngine のDOM用)
- @keyframes damage-text-float (DOM用テキストアニメーション)
- @keyframes shield-ring-expand (DOM用リングアニメーション)
- @keyframes heal-glow (DOM用回復グロー)
- パーティクル関連の .particle-* クラス
```

---

## 3. パフォーマンス最適化

### 3.1 バンドルサイズ最適化

```typescript
// vite.config.ts — PixiJS のツリーシェイキング
// @pixi/react の extend() で必要なモジュールのみ登録する設計を活用
import { Application, Sprite, Container, Graphics, TilingSprite } from 'pixi.js';
import { extend } from '@pixi/react';

extend({
  Application, Sprite, Container, Graphics, TilingSprite,
  // 不要: Text, BitmapText, Mesh, NineSlicePlane etc.
});
```

**目標バンドルサイズ:**
| パッケージ | gzip前 | gzip後（目標） |
|-----------|--------|---------------|
| pixi.js (tree-shaken) | ~300KB | ~80KB |
| @pixi/react | ~20KB | ~6KB |
| @pixi/particle-emitter | ~30KB | ~10KB |
| spine-pixi (もし導入) | ~80KB | ~25KB |
| **合計** | ~430KB | ~121KB |

### 3.2 Vite チャンク分割

```typescript
// vite.config.ts
build: {
  rollupOptions: {
    output: {
      manualChunks: {
        'vendor-pixi': ['pixi.js', '@pixi/react', '@pixi/particle-emitter'],
        'vendor-react': ['react', 'react-dom'],
      }
    }
  }
}
```

PixiJS を独立チャンクにすることで、バトル画面以外のページでは読み込まない。

### 3.3 メモリ管理

```typescript
// テクスチャメモリ管理
class TextureManager {
  // Phase 4 追加: Depth変更時の不要テクスチャ解放
  unloadBundle(bundleName: string): void {
    const bundle = this.bundles.get(bundleName);
    bundle?.forEach(texture => texture.destroy(true));
    this.bundles.delete(bundleName);
  }

  // GC対応: PixiJS v8 の GCSystem と連携
  enableGC(maxIdle: number = 60000): void {
    // 60秒使用されていないテクスチャを自動解放
  }
}
```

### 3.4 モバイルパフォーマンス最適化

| 対策 | 内容 |
|------|------|
| 解像度制限 | モバイルでは `resolution: 1`（Retinaの2x を無効化） |
| パーティクル数制限 | `maxParticles` をデバイス性能に応じて動的調整 |
| フィルター制限 | ブルーム等のWebGLフィルターをモバイルでは無効化 |
| フレームレート | `Ticker.maxFPS = 30` のオプション提供 |
| アセット解像度 | モバイル向けに 50% サイズのスプライトシートを用意 |

```typescript
// src/ui/pixi/core/PixiPerformanceConfig.ts
function detectPerformanceTier(): 'high' | 'medium' | 'low' {
  const gl = document.createElement('canvas').getContext('webgl2');
  if (!gl) return 'low';

  const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
  const renderer = debugInfo
    ? gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL)
    : '';

  // GPU ベースの判定ロジック
  if (/Apple GPU|Mali-4/.test(renderer)) return 'low';
  if (/Mali-G|Adreno 6/.test(renderer)) return 'medium';
  return 'high';
}

const PERFORMANCE_PRESETS = {
  high: {
    resolution: window.devicePixelRatio,
    maxParticles: 500,
    enableFilters: true,
    enableParallax: true,
  },
  medium: {
    resolution: 1,
    maxParticles: 200,
    enableFilters: true,
    enableParallax: true,
  },
  low: {
    resolution: 1,
    maxParticles: 50,
    enableFilters: false,
    enableParallax: false,
  },
} as const;
```

---

## 4. 他画面への PixiJS 展開

### 4.1 ダンジョンマップ (ExplorationScreen / NodeMap)

**適用範囲:** ノード間の接続線パーティクル演出

```
現状:  SVG の <line> + CSS glow
目標:  PixiJS で描画する流れるパーティクルライン

実装イメージ:
- ノード間の接続を PixiJS Graphics + Particle trail で描画
- 「available」ノードへの道に光のパーティクルが流れる
- ボスノードからは威圧的なオーラ
- 現在位置ノードに脈動するグロー
```

**優先度:** 中（バトル画面ほど効果が高くないが、探索の没入感に寄与）

### 4.2 ベースキャンプ (BaseCamp)

**適用範囲:** キャンプファイヤーと環境エフェクト

```
現状:  🔥 絵文字 + CSS sparks
目標:  PixiJS パーティクルのキャンプファイヤー

実装イメージ:
- 炎パーティクル（fire_burst プリセットの低強度版）
- 火の粉が舞い上がるアンビエント
- 施設カードのホバー時グローエフェクト
```

**優先度:** 低（視覚的改善だが、ゲームプレイへの影響なし）

### 4.3 画面遷移エフェクト

**適用範囲:** 全画面共通のトランジション

```
現在:  CSSの opacity transition
目標:  PixiJS による映画的トランジション

候補エフェクト:
- フェードスルーブラック（標準）
- 属性色のワイプ（バトル開始時）
- パーティクル散布 → 収束（ダンジョン突入時）
- 円形マスク縮小/拡大（勝利/敗北時）
```

**優先度:** 中（全画面に影響するため費用対効果が高い）

---

## 5. Spine スケルタルアニメーション — Go/No-Go 判断

### 5.1 判断基準

Phase 3 のスプライトシートアニメーション結果を踏まえ、以下の基準で判断する。

| 基準 | Go 条件 | No-Go 条件 |
|------|---------|------------|
| スプライトアニメーション品質 | 滑らかさに限界を感じる | 十分に満足できるクオリティ |
| 制作コスト | Spine Essential $69 の価値がある | 現行で十分 |
| 学習コスト | Spineツールの習得に投資可能 | 時間的に余裕がない |
| パーツ分割の手間 | 許容範囲内 | AI画像のパーツ分割が困難 |
| ファイルサイズ | +25KB(gzip) が許容範囲 | サイズ増加を避けたい |

### 5.2 Go の場合の計画

```
Step 1: Spine Essential ライセンス購入 ($69)
Step 2: spine-pixi-v8 パッケージ追加
Step 3: プレイヤーキャラクター 2体の Spine 化
  - AI画像をパーツ分割（頭、胴体、腕×2、脚×2、武器）
  - Spine Editor でボーンセットアップ
  - 待機、攻撃、被ダメージ、勝利の4アニメーション
Step 4: 主要ボス敵（5体）の Spine 化
Step 5: CharacterLayer の Spine 対応
```

**導入パッケージ:**
```bash
npm install @esotericsoftware/spine-pixi-v8
```

### 5.3 No-Go の場合の代替強化

```
- フレームアニメーションの品質向上（8-12フレーム/アクション）
- トゥイーンベースの疑似アニメーション強化
  - スケール変化（攻撃時に膨張、被ダメージ時に縮小）
  - 回転（斬撃時の傾き）
  - 色調変化（被ダメージの赤フラッシュ）
  - PixiJS の Sprite.anchor 操作でのスクオッシュ＆ストレッチ
- DragonBones (無料) での試行
```

### 5.4 PoC スケジュール

```
Day 1:  Spine Essential トライアルをダウンロード
Day 2:  プレイヤー剣士の画像をパーツ分割
Day 3:  Spine Editor でボーンセットアップ + 待機アニメーション
Day 4:  spine-pixi-v8 で PixiJS に読み込み描画テスト
Day 5:  品質・手間・パフォーマンスを評価し Go/No-Go 判断
```

---

## 6. ドキュメント更新

### 6.1 更新対象

| ドキュメント | 更新内容 |
|-------------|----------|
| `CLAUDE.md` | Architecture Overview にPixiJSレイヤー構成を追記 |
| `CLAUDE.md` | Development Commands にPixiJS関連コマンドを追記 |
| `CLAUDE.md` | Skills Quick Reference に PixiJS スキルを追記 |
| `README.md` | Development History に Phase 1-4 の完了を記録 |
| `TODO.md` | PixiJS 関連タスクの完了・削除 |
| `.claude/docs/battle_design.md` | PixiJS エフェクト仕様を追記 |

### 6.2 新規ドキュメント

| ドキュメント | 内容 |
|-------------|------|
| `.claude/docs/pixijs_architecture.md` | PixiJS統合アーキテクチャ詳細 |
| `.claude/docs/style_guide.md` | ビジュアルスタイルガイド（Phase 3 で作成） |
| `.claude/docs/asset_pipeline.md` | アセット制作ガイドライン |
| `.claude/skills/pixijs-effects/` | PixiJSエフェクト開発スキル |

### 6.3 開発スキル作成

```
.claude/skills/pixijs-effects/
├── SKILL.md              # PixiJSエフェクト開発ガイド
├── particle_template.md  # パーティクルプリセット作成手順
└── effect_template.md    # 新規エフェクト追加手順
```

---

## 7. Implementation Steps

### Step 1: Feature Flag 削除とコード統一
- FEATURE_FLAGS オブジェクトの削除
- DOM版分岐コードの削除
- PixiJS版を唯一の実装に

### Step 2: レガシーDOMアニメーションコード削除
- animationEngine.ts からパーティクル/ダメージ関数を削除
- 不要なCSS keyframes を削除
- useCardAnimation のDOM版エフェクトコードを削除

### Step 3: バンドルサイズ最適化
- PixiJS ツリーシェイキング設定
- Vite チャンク分割設定
- `npm run build` でサイズ確認

### Step 4: メモリ管理最適化
- TextureManager のアンロード実装
- GCSystem 連携
- Depth切替時のテクスチャ解放テスト

### Step 5: モバイルパフォーマンス対応
- detectPerformanceTier 実装
- パフォーマンスプリセットの適用
- 低スペックデバイスでの動作検証

### Step 6: ダンジョンマップ PixiJS 展開（任意）
- NodeMap への PixiJS オーバーレイ追加
- ノード間パーティクルライン
- 動作確認

### Step 7: 画面遷移エフェクト（任意）
- 共通トランジションシステム設計
- バトル開始/終了のトランジション
- ダンジョン突入のトランジション

### Step 8: Spine PoC と判断
- 5日間の PoC 実施
- 結果に基づく Go/No-Go 判断
- 判断結果をドキュメントに記録

### Step 9: ドキュメント・スキル更新
- 全ドキュメントの更新
- pixijs-effects スキル作成
- README.md の Development History 更新
- TODO.md の完了タスク整理

---

## 8. Affected Files

### Deleted/Reduced Files
| File | Change |
|------|--------|
| `src/ui/animations/animationEngine.ts` | createParticles, showDamageText, shakeElement 削除 |
| `src/ui/css/animations/particle-*.css` | パーティクル関連CSS削除 |
| `src/constants/uiConstants.ts` | FEATURE_FLAGS 削除 |

### New Files
| File | Purpose |
|------|---------|
| `src/ui/pixi/core/PixiPerformanceConfig.ts` | パフォーマンス検出と設定 |
| `src/ui/pixi/dungeon/DungeonMapCanvas.tsx` | ダンジョンマップ PixiJS（任意） |
| `src/ui/pixi/transitions/TransitionManager.ts` | 画面遷移エフェクト（任意） |
| `.claude/docs/pixijs_architecture.md` | アーキテクチャドキュメント |
| `.claude/skills/pixijs-effects/SKILL.md` | 開発スキル |

### Modified Files
| File | Change |
|------|--------|
| `vite.config.ts` | チャンク分割設定追加 |
| `CLAUDE.md` | アーキテクチャ情報更新 |
| `README.md` | 開発履歴追記 |
| `TODO.md` | 完了タスク整理 |

---

## 9. Acceptance Criteria

- [ ] `animationEngine.ts` から DOM パーティクル系コードが完全削除されている
- [ ] Feature Flag が全て削除され、PixiJS が唯一のエフェクト実装
- [ ] `npm run build` のバンドルサイズが PixiJS 追加前 +150KB(gzip) 以内
- [ ] モバイル（中スペック）で 30fps 以上を維持
- [ ] Spine の Go/No-Go 判断が記録されている
- [ ] CLAUDE.md にPixiJSアーキテクチャが反映されている
- [ ] `npm run test:run` で全テストがパスする
- [ ] 既存のゲームフロー（キャラ選択→キャンプ→ダンジョン→バトル→報酬）が正常動作

---

## 10. Dependencies

- **Blocked by**: Phase 1, Phase 2, Phase 3
- **Blocks**: 将来の機能拡張（Spine 導入決定の場合）

## 11. Success Metrics

| Metric | Before (Phase 0) | After (Phase 4) |
|--------|------------------|-----------------|
| 同時パーティクル上限 | 90 (DOM) | 500+ (GPU) |
| エフェクト種類 | 4種 (damage/heal/shield/discard) | 20種+ |
| 敵画像カバー率 | 0% (50体未作成) | 100% (50体完成) |
| スプライトシート化 | なし (38 HTTP req) | 完了 (5-8 req) |
| バンドルサイズ増加 | — | +121KB (gzip) |
| 属性別エフェクト | なし | 6属性完全対応 |
| 背景表現 | 静止画 4枚 | パララックス 4Depth |

## 12. References

- PixiJS Tree Shaking: https://pixijs.com/guides/advanced/tree-shaking
- PixiJS GCSystem: https://pixijs.com/blog/v8-15
- Spine Essential: https://esotericsoftware.com/spine-purchase
- spine-pixi-v8: https://github.com/EsotericSoftware/spine-runtimes
- 既存バトル設計: `.claude/docs/battle_design.md`
- Phase 1 計画: `.claude/docs/vision/plans/pixijs_phase1_foundation.md`
- Phase 2 計画: `.claude/docs/vision/plans/pixijs_phase2_battle_effects.md`
- Phase 3 計画: `.claude/docs/vision/plans/pixijs_phase3_asset_pipeline.md`
