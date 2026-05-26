# PixiJS統合 Phase 2: バトルエフェクト移行

## Change History

| Date | Content |
|------|---------|
| 2026-02-22 | Initial creation based on engine integration proposal |

---

## 1. Overview

Phase 1 で構築した PixiJS 基盤の上に、既存のDOMベースアニメーションを PixiJS パーティクル・エフェクトに段階的に置換する。最優先は**カード発動演出**の強化で、属性ごとの派手なパーティクルエフェクト、画面シェイク、WebGLフィルター（ブルーム、色収差）を実装する。

### Goals

- カード発動時の属性別パーティクルエフェクト実装（火・氷・雷・闇・光・物理）
- ダメージ/回復/シールドエフェクトの PixiJS 化
- クリティカルヒット演出の強化（画面全体のシェイク + ホワイトアウト）
- バフ/デバフの視覚エフェクト追加
- WebGLフィルター（ブルーム、ショックウェーブ）の導入
- 既存DOM パーティクルコードの段階的廃止

### Non-Goals

- キャラクタースプライトの描画（Phase 3）
- 背景のパララックス化（Phase 3）
- カード自体のPixiJS描画（DOMのまま維持）

---

## 2. カード発動演出 — 属性別エフェクト設計

### 2.1 エフェクトトリガーフロー

```
カードプレイ (handleCardPlay)
  → useCardExecution.executeCard()
    → PixiEffectBridge.playCardEffect(card)
      → 属性判定 → エフェクト選択
        → ParticleEmitter 生成
        → WebGLフィルター適用（任意）
        → ダメージ表示（連動）
```

### 2.2 属性別パーティクル仕様

各属性のパーティクルは `@pixi/particle-emitter` のJSON設定で管理する。

#### 火属性 (fire)
| Parameter | Value |
|-----------|-------|
| パーティクル形状 | 炎の粒子（オレンジ→赤のグラデーション） |
| 動き | 下→上に揺らぎながら上昇、拡散 |
| 色 | #ff4400 → #ff8800 → #ffcc00 |
| パーティクル数 | 60-120（ダメージ依存） |
| 持続時間 | 800ms |
| 追加効果 | ブルームフィルター（強度: 2.0） |
| サウンドイメージ | 燃焼音（将来対応） |

#### 氷属性 (ice)
| Parameter | Value |
|-----------|-------|
| パーティクル形状 | 結晶片（六角形スプライト or 菱形） |
| 動き | ターゲットに向かって収束 → 放射状に弾ける |
| 色 | #00ccff → #aaeeff → #ffffff |
| パーティクル数 | 40-80 |
| 持続時間 | 1000ms |
| 追加効果 | ターゲットに氷結エフェクト（青グロー 500ms） |

#### 雷属性 (lightning)
| Parameter | Value |
|-----------|-------|
| パーティクル形状 | 電撃スパーク（小さな光点 + ライン） |
| 動き | ランダムに瞬間移動、チェーン状に連鎖 |
| 色 | #ffcc00 → #ffffff |
| パーティクル数 | 30-50（高速で点滅） |
| 持続時間 | 600ms（短く鋭い） |
| 追加効果 | 画面フラッシュ（白 50ms） + ショックウェーブ |

#### 闇属性 (dark)
| Parameter | Value |
|-----------|-------|
| パーティクル形状 | 黒い霧・渦巻き |
| 動き | ターゲットを中心に渦巻き状に収束 |
| 色 | #8800ff → #440088 → #000000 |
| パーティクル数 | 50-100 |
| 持続時間 | 1200ms（ゆっくり） |
| 追加効果 | ターゲット周囲の暗転（アルファマスク） |

#### 光属性 (light)
| Parameter | Value |
|-----------|-------|
| パーティクル形状 | 光の粒子（星形スプライト） |
| 動き | ターゲットから放射状に拡散、浮遊 |
| 色 | #ffffaa → #ffffff → #ffffcc |
| パーティクル数 | 80-150 |
| 持続時間 | 1000ms |
| 追加効果 | ブルームフィルター（強度: 3.0） + レンズフレア風 |

#### 物理属性 (physics / slash / impact)
| Parameter | Value |
|-----------|-------|
| パーティクル形状 | 衝撃波リング + 破片 |
| 動き | インパクト点から放射 + 重力落下 |
| 色 | #cccccc → #ffffff |
| パーティクル数 | 20-40 |
| 持続時間 | 500ms（高速） |
| 追加効果 | 画面シェイク（振幅: ダメージ比例） |

#### 斬撃 (slash) 特殊演出
- 剣士専用: 斬撃ラインエフェクト（PixiJS Graphics で描画）
- ソードエナジー消費量に応じて斬撃数が増加
- 色: ソードエナジーレベルに応じて白→金→赤

### 2.3 カード発動シーケンス

```
Frame 0:    カードがDOM上で中央に移動（既存アニメーション）
Frame 100:  カード拡大完了 → PixiJS エフェクト開始
Frame 100:  属性パーティクル開始（EffectLayer）
Frame 300:  パーティクルがターゲットに到達
Frame 300:  ダメージ計算結果を表示
Frame 300:  画面シェイク開始（クリティカル時）
Frame 600:  パーティクル収束・消滅
Frame 800:  全エフェクト完了 → 次のアクションへ
```

---

## 3. ダメージ/回復/シールドエフェクト

### 3.1 ダメージエフェクト（PixiJS版）

**現状からの改善点:**
- DOM `<div>` パーティクル → PixiJS GPU パーティクル
- 90個上限 → 数百個でも60fps維持
- 固定色 → 属性カラー連動

```typescript
interface DamageEffectConfig {
  target: 'player' | 'enemy';
  damage: number;
  isCritical: boolean;
  element: ElementType[];
  hitCount: number;           // 多段ヒット対応
}
```

**クリティカルヒット強化演出:**
1. 画面全体のホワイトフラッシュ（50ms）
2. スローモーション効果（200ms, timeScale: 0.3）
3. 放射状パーティクル爆発（200個+）
4. 画面シェイク（振幅30px, 持続400ms）
5. ダメージ数値の特大表示（スケール: 2.0 → 1.5 のバウンス）
6. ブルームフィルター適用（強度: 4.0, 300ms）

### 3.2 回復エフェクト（PixiJS版）

**現状からの改善点:**
- 緑パーティクルの上昇 → 光の柱 + 癒しのオーラ

```
1. ターゲット足元から光の柱エフェクト（200ms立ち上がり）
2. 緑色のパーティクルが柱に沿って上昇
3. ターゲットに緑のグローオーバーレイ（600ms fade out）
4. 回復数値の浮遊表示（+XX, 緑色）
```

### 3.3 シールドエフェクト（PixiJS版）

**現状からの改善点:**
- 拡大するリング → 六角形バリア展開

```
1. ターゲット前方に半透明の六角形バリア描画（PixiJS Graphics）
2. バリア展開アニメーション（0 → 100%, 300ms, easeOutBack）
3. 青いパーティクルがバリア表面を流れる
4. ガード数値表示 + 青グロー
```

---

## 4. バフ/デバフ視覚エフェクト

### 4.1 バフ適用時エフェクト

| バフカテゴリ | 視覚表現 |
|-------------|----------|
| 攻撃力UP | 赤いオーラ上昇 + キャラクター周囲に赤い粒子 |
| 防御力UP | 青いシールドリング展開 |
| haste/superFast | 緑のスピードライン + 残像 |
| regeneration | 緑の持続パーティクル（ターン開始時） |
| criticalUp | 金色のスパーク |
| reflect | 鏡面反射エフェクト（シルバー） |

### 4.2 デバフ適用時エフェクト

| デバフカテゴリ | 視覚表現 |
|---------------|----------|
| bleed | 赤い滴パーティクル（下方向） |
| poison | 紫の泡パーティクル（上方向） |
| burn | 小さな炎パーティクル（キャラ周囲） |
| freeze | 氷結晶オーバーレイ + 動作速度低下表現 |
| stun | 星マーク回転（キャラ頭上） |
| weakness | 灰色のオーラ（活力低下表現） |

### 4.3 フィールドエフェクト

| フィールド | 視覚表現 |
|-----------|----------|
| fireField | 画面下部に揺らめく炎 + 熱波エフェクト |
| electroField | 画面端にスパーク + 稲妻がランダムに走る |
| iceField | 画面下部に霜 + 冷気パーティクル |
| darkField | 画面全体を暗転 + 紫の霧 |
| lightField | 画面全体が明るく + 光の柱 |

---

## 5. WebGLフィルター設計

### 5.1 導入するフィルター

```typescript
// PixiJS v8 Built-in Filters
import { BlurFilter, ColorMatrixFilter } from 'pixi.js';

// 用途別フィルター設定
const FILTERS = {
  bloom: {
    // BlurFilter + 加算合成で疑似ブルーム
    blur: 4,
    quality: 2,
  },
  shockwave: {
    // カスタムシェーダー（DisplacementFilter ベース）
    amplitude: 30,
    wavelength: 160,
    speed: 300,
  },
  colorShift: {
    // ColorMatrixFilter で色相シフト
    // 闇属性: 彩度低下, 光属性: 明度上昇
  },
  screenFlash: {
    // Graphics overlay + alpha animation
    color: 0xffffff,
    duration: 50,
  },
} as const;
```

### 5.2 フィルター適用タイミング

| トリガー | フィルター | 対象 | 持続 |
|---------|-----------|------|------|
| クリティカルヒット | bloom + screenFlash | EffectLayer全体 | 300ms |
| 雷属性攻撃 | screenFlash(白) | 画面全体 | 50ms |
| 闇属性攻撃 | colorShift(暗転) | BackgroundLayer | 1200ms |
| 光属性攻撃 | bloom(強) | EffectLayer | 1000ms |
| 大ダメージ | shockwave | EffectLayer | 500ms |

---

## 6. Implementation Steps

### Step 1: パーティクルプリセット作成
- `ParticlePresets.ts` に6属性分のEmitter設定JSONを定義
- @pixi/particle-emitter のビヘイビアシステムで設定
- pixiparticles.com エディタで視覚調整

### Step 2: EffectLayer 本実装
- ParticleEmitter の生成・管理ロジック
- エフェクトキュー（同時発動の管理）
- エフェクト完了コールバック

### Step 3: PixiEffectBridge 本実装
- playCardEffect: 属性判定 → プリセット選択 → Emitter起動
- playDamageEffect: ダメージ数値表示 + パーティクル
- playHealEffect / playShieldEffect

### Step 4: 既存 useCardAnimation との統合
- `showDamageEffect` を PixiEffectBridge 経由に切り替え
- `showHealEffect` を PixiEffectBridge 経由に切り替え
- `showShieldEffect` を PixiEffectBridge 経由に切り替え
- 切り替えは Feature Flag で制御（`usePixiEffects: boolean`）

### Step 5: カード発動演出の実装
- カードプレイ時の属性パーティクル発射
- ターゲットへの着弾エフェクト
- 多段ヒットの連続エフェクト対応

### Step 6: クリティカルヒット強化演出
- ホワイトフラッシュ実装
- スローモーション効果（Ticker timeScale制御）
- ブルームフィルター適用

### Step 7: バフ/デバフ/フィールドエフェクト
- バフ適用時のオーラエフェクト
- DoTダメージの持続パーティクル
- フィールドエフェクト描画

### Step 8: 既存DOMパーティクルの段階的削除
- Feature Flag で PixiJS 版が安定確認後
- animationEngine.ts の createParticles を PixiJS 版に置換
- 不要になったDOM生成コードを削除

---

## 7. Data Structure

### 7.1 パーティクルプリセット型

```typescript
// src/ui/pixi/types/pixiTypes.ts
interface ParticlePreset {
  id: string;
  emitterConfig: EmitterConfigV3;  // @pixi/particle-emitter形式
  duration: number;
  filters?: FilterConfig[];
  screenShake?: { amplitude: number; duration: number };
  screenFlash?: { color: number; duration: number };
}

interface FilterConfig {
  type: 'bloom' | 'colorShift' | 'shockwave';
  params: Record<string, number>;
  duration: number;
  target: 'effect' | 'background' | 'screen';
}

// 属性→プリセットマッピング
const ELEMENT_EFFECT_MAP: Record<ElementType, string> = {
  fire: 'fire_burst',
  ice: 'ice_crystal',
  lightning: 'lightning_spark',
  dark: 'dark_vortex',
  light: 'light_radiance',
  physics: 'impact_wave',
  slash: 'slash_line',
  impact: 'impact_wave',
  // ...
};
```

### 7.2 エフェクトキュー

```typescript
interface EffectQueueItem {
  id: string;
  preset: ParticlePreset;
  position: { x: number; y: number };
  target?: { x: number; y: number };
  onComplete?: () => void;
  priority: number;  // 同時発動時の処理順
}
```

---

## 8. Affected Files

### New Files
| File | Purpose |
|------|---------|
| `src/ui/pixi/shared/particles/ParticlePresets.ts` | 属性別パーティクル設定 |
| `src/ui/pixi/shared/particles/effectQueue.ts` | エフェクトキュー管理 |
| `src/ui/pixi/shared/filters/FilterPresets.ts` | WebGLフィルター設定 |
| `src/ui/pixi/battle/effects/DamageEffect.tsx` | ダメージエフェクト |
| `src/ui/pixi/battle/effects/HealEffect.tsx` | 回復エフェクト |
| `src/ui/pixi/battle/effects/ShieldEffect.tsx` | シールドエフェクト |
| `src/ui/pixi/battle/effects/BuffEffect.tsx` | バフ/デバフエフェクト |
| `src/ui/pixi/battle/effects/FieldEffect.tsx` | フィールドエフェクト |
| `src/ui/pixi/battle/effects/CardPlayEffect.tsx` | カード発動演出 |
| `src/ui/pixi/battle/effects/CriticalEffect.tsx` | クリティカルヒット演出 |
| `src/ui/pixi/battle/effects/SlashEffect.tsx` | 剣士斬撃エフェクト |

### Modified Files
| File | Change |
|------|--------|
| `src/ui/pixi/battle/PixiEffectBridge.ts` | 全エフェクトメソッド実装 |
| `src/ui/pixi/battle/layers/EffectLayer.tsx` | エフェクト描画ロジック |
| `src/ui/html/componentsHtml/useCardAnimation.tsx` | PixiJS版エフェクト呼び出しに切替 |
| `src/ui/animations/animationEngine.ts` | Feature Flag 分岐追加 |
| `src/constants/uiConstants.ts` | PixiJS用アニメーション定数追加 |

---

## 9. Feature Flag 設計

```typescript
// src/constants/uiConstants.ts に追加
export const FEATURE_FLAGS = {
  usePixiEffects: true,      // true: PixiJS, false: DOM fallback
  usePixiDamageText: true,   // ダメージ数値表示
  usePixiScreenShake: true,  // 画面シェイク
  usePixiFilters: true,      // WebGLフィルター
} as const;
```

移行期間中は Feature Flag で DOM 版とPixiJS版を切り替え可能にする。Phase 4 で DOM 版を完全削除し、Feature Flag も除去する。

---

## 10. Performance Budget

| Metric | Target | 現状(DOM) |
|--------|--------|-----------|
| 同時パーティクル数 | 500+ | 90上限 |
| フレームレート | 60fps維持 | 60fps（パーティクル少数時） |
| メモリ増加 | +20MB以下 | ベースライン |
| Emitter 同時起動数 | 5以下 | N/A |

---

## 11. Acceptance Criteria

- [ ] カード発動時に属性に応じたパーティクルエフェクトが表示される
- [ ] クリティカルヒット時に画面フラッシュ + シェイク + ブルームが発動する
- [ ] 回復時に光の柱エフェクトが表示される
- [ ] シールド時に六角形バリアが展開される
- [ ] バフ/デバフ適用時にオーラエフェクトが表示される
- [ ] Feature Flag で DOM 版にフォールバック可能
- [ ] 500パーティクル同時表示で 60fps 維持
- [ ] 既存の全バトルフローが正常動作する

---

## 12. Dependencies

- **Blocked by**: Phase 1（基盤構築）
- **Blocks**: Phase 4（ポリッシュ）

## 13. References

- @pixi/particle-emitter ビヘイビア: https://particle-emitter.pixijs.io/docs/
- PixiJS Filters: https://pixijs.com/guides/components/filters
- 既存アニメーション定数: `src/constants/uiConstants.ts`
- 属性定義: `src/types/cardTypes.ts` ElementType
- バフ/デバフ定義: `src/types/battleTypes.ts`
