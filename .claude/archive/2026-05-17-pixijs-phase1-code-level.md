# Plan: PixiJS Phase 1 — Foundation (Code-Level)

| Field      | Value                                                                                                                                       |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Status     | COMPLETED（2026-05-23）— 実装完了・実機検証 OK・archive 済。Step 0 採用案 = **A 案**（React.lazy + useRef マウントガード、main.tsx 無変更） |
| Created    | 2026-05-17                                                                                                                                  |
| Task       | MEMORY.md → 予定「PixiJS 描画基盤 Phase 1」                                                                                                 |
| Project    | /Users/newlife/dev/apps/original-card-battle                                                                                                |
| Supersedes | `.claude/docs/vision/plans/pixijs_phase1_foundation.md`（抽象版・2026-02-22。本書を正とし旧版は archive 推奨）                              |
| Verified   | PixiJS / @pixi/react / Vite7 の最新情報を 2026-05-17 に web 検証済（§0B）                                                                   |

---

## 0A. Context（なぜやるか / 制約 / Non-Goals）

**動機:** バトル演出を GPU 描画へ移行する基盤を作る。既存 DOM/CSS アニメは温存し、PixiJS 透過キャンバスを重ねる「ハイブリッド」構成。Phase 1 は基盤のみで、既存演出の置換（Phase 2）・アセット（Phase 3）・最適化（Phase 4）はやらない。

**Non-Goals（Phase 1 で触らない）:**

- 既存 `animationEngine.ts` / `useCardAnimation.tsx` の DOM パーティクル置換
- 敵スプライト・スプライトシート
- パフォーマンス最適化
- WebGPU 有効化（§0B-2 の通り production 非推奨）

**不可侵（DO NOT MODIFY）:** `src/domain/cards/decks/deck.ts`, `deckReducer.ts`

---

## 0B. 旧計画への批判的評価（多視点・最新情報反映）

旧 `pixijs_phase1_foundation.md` を 6 観点で監査。**致命的な誤り 3 / 要修正 4** を検出。

| #   | 旧計画の記述                                                | 検証結果                                                                                                                                                                                                              | 判定                       | 本書での対応                                                                                          |
| --- | ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | ----------------------------------------------------------------------------------------------------- |
| 1   | `@pixi/react v8` は「React 19 専用設計」                    | パッケージ名は正（改名なし）。peerDep は `react>=18`。**ただし StrictMode で WebGL コンテキスト stale → クラッシュする未解決 issue #602 が OPEN**。本リポは `main.tsx:7` で全 App を `<StrictMode>` ラップ → **直撃** | ⚠ NUANCED / **ブロッカー** | §3 Step 0 で StrictMode 回避策を必須化                                                                |
| 2   | `pixi.js >= 8.16.0`（最新）                                 | 8.16.0 は 2026-02 時点。**現行最新は v8.18.1（2026-04）**。WebGPU は公式が「production 非推奨」明言                                                                                                                   | ❌ OUTDATED                | バージョン更新 + `preference:'webgl'` 固定                                                            |
| 3   | `npm install @pixi/particle-emitter`                        | **v8 非対応。最新 v5.0.8(2022) は pixi v6 向け。v8 issue #211 はメンテ無反応＝事実上死亡**                                                                                                                            | ❌ FALSE / 高リスク        | **Phase 1 では一切入れない**。粒子は Phase 2 で v8 内蔵 `ParticleContainer` か community 版を別途評価 |
| 4   | `vite.config.ts` に `optimizeDeps.include:['pixi.js']` 追加 | pixi.js v8 は native ESM。Vite7 で原則不要。**真のリスクは top-level await**（`Application.init()` を top-level await すると prod build で hang。issue #10456）                                                       | ⚠ NUANCED                  | optimizeDeps は入れない。§2.2 で await 罠を明記                                                       |
| 5   | キャンバスを z-index:5、`pointer-events:none` で重ねる      | 実コードは `.battle-field` z-index **10**、hand **100**、particles **9999**。**z-index:5 だとフレーム背面に隠れる**。さらに `pointer-events:none` だけでは v8 の `pointermove` が document capture phase で漏れる     | ❌ FALSE（z）/ ⚠（events） | §2.6 で z-index **15** に修正 + `renderer.events.features.move=false` 明記                            |
| 6   | 独自 `PixiContextBridge.tsx` で Context 伝播                | Context が reconciler を越えない事実は CONFIRMED。だが @pixi/react v8 は `its-fine` を内蔵済。**独自ブリッジは過剰設計**。props 渡し or `useApplication` で足りる                                                     | ⚠ 過剰設計                 | §2.5 で独自 Bridge を廃し props 注入に簡素化                                                          |

**その他の実装現実とのズレ:**

- 旧計画は `useCardAnimation` の場所を未特定。実体は `src/ui/html/componentsHtml/useCardAnimation.tsx`（402行）。クリティカル 90 粒子は `src/constants/uiConstants.ts:125 CRIT_PARTICLE_COUNT`。
- 旧計画「GuildBattleScreen は Phase2 で自動対応」→ 実際は `GuildBattleScreen.tsx` が `BattleScreen` を再利用せず**構造を複製**（同一 CSS）。`BattleCanvas` は **2 ファイルに個別追加が必要**（CSS は共通なので z-index は 1 箇所で済む）。
- 旧計画の `@pixi/react extend()` / `<Application>` 記述は CONFIRMED（v8 で `<Stage>` は廃止、`extend()` 必須）。

**結論:** 旧計画の方向性（PixiJS v8 / ハイブリッド / 段階移行）は妥当。ただし **particle-emitter 依存・z-index・StrictMode 未考慮**は実装前に必ず潰す。本書はこれらを反映した実装可能版。

---

## 1. Steps（各ステップ 1 セッションで完了可能）

- [ ] **Step 0 — StrictMode ブロッカー対処（最優先）**
- [ ] **Step 1 — パッケージ導入（particle-emitter なし）**
- [ ] **Step 2 — 型定義 `pixiTypes.ts`**
- [ ] **Step 3 — `PixiStage` 共通 wrapper**
- [ ] **Step 4 — `BattleCanvas` + 3 レイヤー骨格**
- [ ] **Step 5 — CSS レイヤリング（z-index 15）**
- [ ] **Step 6 — `BattleScreen` / `GuildBattleScreen` 統合**
- [ ] **Step 7 — `PixiEffectBridge` 骨格 + テスト粒子**
- [ ] **Step 8 — テスト・受け入れ確認**

---

## 2. 技術設計（コードレベル）

### 2.1 ディレクトリ構成（新規）

```
src/ui/pixi/
├── core/
│   ├── PixiStage.tsx          # <Application> ラッパー + extend()
│   └── usePixiApp.ts          # useApplication 再エクスポート + 型付け
├── battle/
│   ├── BattleCanvas.tsx       # バトル用オーバーレイ（state を props 注入）
│   ├── layers/
│   │   ├── BackgroundLayer.tsx   # 空コンテナ（Phase3）
│   │   ├── CharacterLayer.tsx    # 空コンテナ（Phase3）
│   │   └── EffectLayer.tsx       # テスト粒子のみ（Phase2 で本実装）
│   └── PixiEffectBridge.ts    # 命令型 API 骨格（playTestParticle のみ実装）
└── types/
    └── pixiTypes.ts           # BattlePixiProps 等
```

※ 旧計画の `core/PixiContextBridge.tsx` / `shared/textures/TextureManager.ts` / `shared/particles/ParticlePresets.ts` は **Phase 1 では作らない**（Bridge=過剰、Texture/Particle=Phase3/2 の領分）。YAGNI。

### 2.2 Step 1: パッケージ導入

```bash
npm install pixi.js@^8.18.1 @pixi/react@^8.0.5
```

- **`@pixi/particle-emitter` は入れない**（§0B-3）。
- **`optimizeDeps` 追加なし**（§0B-4）。`vite.config.ts` は変更しない。HMR で pixi の重複初期化が出た場合のみ `optimizeDeps: { exclude: ['@pixi/react'] }` を検討（初手では入れない）。
- top-level await 罠: `@pixi/react` の `<Application>` は内部で `init()` を呼ぶため、アプリ側で `await Application.init()` を top-level で書かない限り問題は出ない。**TextureManager を自前で書かない**（Phase 1 で作らない）ことで本罠を回避。

### 2.3 Step 0: StrictMode 回避（ブロッカー / 最優先）

issue #602: StrictMode の二重マウントで PixiJS が stale WebGL context を掴み `Cannot read properties of null (reading 'split')` でクラッシュ。本リポは `src/main.tsx:7` で全 App を `<StrictMode>` ラップ。

**採用方針（推奨）:** 全体の StrictMode は維持し、**PixiJS サブツリーだけ StrictMode の外に出す**。`<Application>` を含む `BattleCanvas` 内では二重 effect を踏まないよう、マウントガードを併用する。

`src/ui/pixi/core/PixiStage.tsx`:

```tsx
import { Application, extend } from "@pixi/react";
import { Container, Graphics, Sprite, Text } from "pixi.js";
import type { ReactNode, RefObject } from "react";

// v8 必須: 使う pixi クラスを JSX 要素として登録（<pixiContainer> 等）
extend({ Container, Graphics, Sprite, Text });

interface PixiStageProps {
  resizeTo: RefObject<HTMLElement | null>; // .battle-field を渡す
  children: ReactNode;
}

export function PixiStage({ resizeTo, children }: PixiStageProps) {
  return (
    <Application
      resizeTo={resizeTo}
      backgroundAlpha={0} // 透過オーバーレイ
      antialias
      resolution={window.devicePixelRatio} // hi-DPI
      autoDensity // ↑とセット必須（座標ズレ防止）
      preference="webgl" // §0B-2: WebGPU は production 非推奨
      // StrictMode 二重マウント緩和: 開発時のみ HMR で context 再生成
    >
      {children}
    </Application>
  );
}
```

**StrictMode 緩和の具体策（いずれか / Step 0 で決定）:**

- (A 推奨) `main.tsx` の `<StrictMode>` は維持。`BattleCanvas` を `React.lazy` + マウント済みフラグ（`useRef` ガード）で **2 重 init を抑止**。受け入れ基準: dev で battle → camp → battle 往復してクラッシュしないこと。
- (B 代替) dev ビルドのみ `main.tsx` の StrictMode を条件外し（`import.meta.env.PROD ? <StrictMode> : <>`）。影響大なので A で解決しなければ採用。

> Step 0 の成果物 = 「どちらを採るか」を本書に追記 + main.tsx もしくは BattleCanvas のガード実装。
>
> **【実装結果 2026-05-19】採用 = A 案。** `main.tsx` の `<StrictMode>` は無変更で維持。`BattleCanvas` を `React.lazy` で分割し、`useRef`(mountedRef) + post-commit `setReady` ゲートで `<Application>` マウントを StrictMode 二重マウント後の1回に限定。B 案は不要だったため不採用。`features.move=false` は `usePixiApp.ts` の `usePixiEventGuard` 1箇所に確定。意図的 lint disable は **計3箇所**（`BattleCanvas.tsx` set-state-in-effect / `usePixiApp.ts` immutability / `EffectLayer.tsx` exhaustive-deps）— いずれも該当行限定 + 理由コメント付き。QA 判定 PASS-with-fixes（Blocker0 / 実害ある Major0）。

### 2.4 Step 2: 型定義

`src/ui/pixi/types/pixiTypes.ts`:

```ts
import type { BuffDebuffMap } from "@/types"; // useBattleOrchestrator が返す型に合わせる

/** DOM 側 battleState から Pixi に渡す最小サブセット（Phase2 で拡張） */
export interface BattlePixiProps {
  playerHp: number;
  playerMaxHp: number;
  enemyHp: number;
  enemyMaxHp: number;
  isPlayerPhase: boolean;
  phaseCount: number;
}
```

### 2.5 Step 3-4: Context 注入を props に簡素化

@pixi/react v8 は `its-fine` 内蔵だが、Phase 1 で渡すのは数値数個。**独自 Bridge を作らず props で注入**（YAGNI、§0B-6）。

`src/ui/pixi/battle/BattleCanvas.tsx`:

```tsx
import { useRef } from "react";
import { PixiStage } from "../core/PixiStage";
import { EffectLayer } from "./layers/EffectLayer";
import { BackgroundLayer } from "./layers/BackgroundLayer";
import { CharacterLayer } from "./layers/CharacterLayer";
import type { BattlePixiProps } from "../types/pixiTypes";

export function BattleCanvas(props: BattlePixiProps) {
  const hostRef = useRef<HTMLDivElement>(null);
  return (
    <div ref={hostRef} className="battle-pixi-host" aria-hidden>
      <PixiStage resizeTo={hostRef}>
        <pixiContainer>
          <BackgroundLayer />
          <CharacterLayer />
          <EffectLayer {...props} />
        </pixiContainer>
      </PixiStage>
    </div>
  );
}
```

`layers/BackgroundLayer.tsx` / `CharacterLayer.tsx` は `export const X = () => <pixiContainer />;`（空・Phase3）。
`EffectLayer.tsx` は Step 7 のテスト粒子を保持。

### 2.6 Step 5: CSS（z-index 修正済）

実測 z-index: field=10 / hand=100 / particles=9999。**キャンバスは field の上・hand の下＝z-index 15**。
`src/ui/css/pages/battle/battle-layout.css` に追記（共通 CSS なので BattleScreen/GuildBattleScreen 両対応）:

```css
/* PixiJS overlay — sits above battle-field(10), below hand(100) */
.battle-screen .battle-pixi-host {
  position: absolute;
  top: 18vh; /* .battle-field と同座標 */
  left: 5vw;
  width: 90vw;
  height: 51vh;
  z-index: 15;
  pointer-events: none; /* クリックは下の DOM へ通す */
}
.battle-screen .battle-pixi-host canvas {
  pointer-events: none;
}
```

さらに **`pointermove` 漏れ対策**（§0B-5）: `PixiStage` 内 or `usePixiApp` で

```ts
app.renderer.events.features.move = false; // hover 不要な Phase1 では全停止
```

を適用（Phase 2 でホバー演出が要るなら eventMode を個別制御に切替）。

### 2.7 Step 6: 画面統合（2 ファイル）

挿入点（Explore 確認済）:

- `src/ui/html/battleHtml/BattleScreen.tsx` … `.battle-field`（~615行）の直後、`.hand-container`（616行〜）の前に `<BattleCanvas ... />` を挿入。
- `src/ui/html/campsHtml/Guild/GuildBattleScreen.tsx` … 同じ位置（`.battle-field` 直後）に同コンポーネント挿入（構造複製のため手動・CSS は共通で流用）。

props は両画面の `useBattleOrchestrator` 返却値から最小マッピング:

```tsx
<BattleCanvas
  playerHp={playerHp}
  playerMaxHp={playerMaxHp}
  enemyHp={currentEnemy?.hp ?? 0}
  enemyMaxHp={currentEnemy?.maxHp ?? 0}
  isPlayerPhase={isPlayerPhase}
  phaseCount={phaseCount}
/>
```

（フィールド名は `useBattleOrchestrator.ts` 返却 §7 に準拠。実装時に型で検証）

### 2.8 Step 7: PixiEffectBridge 骨格

`src/ui/pixi/battle/PixiEffectBridge.ts`:

```ts
export interface PixiEffectBridge {
  // Phase 2 で実装（シグネチャのみ確定）
  playDamageEffect(
    target: "player" | "enemy",
    damage: number,
    isCritical: boolean,
  ): void;
  playHealEffect(target: "player" | "enemy", amount: number): void;
  playShieldEffect(target: "player" | "enemy", amount: number): void;
  // Phase 1 で実装する唯一の実体
  playTestParticle(x: number, y: number, colorHex: number): void;
}
```

Phase 1 では `EffectLayer` 内に `playTestParticle` のみ実装（`<pixiGraphics>` の円を rAF で 1 個フェードアウト）。**既存 `useCardAnimation` への配線はしない**（Phase 2）。クリック等で 1 個描画できれば Step 8 合格。

---

## 3. Files（影響一覧）

| File                                                | Operation       | Notes                                                                 |
| --------------------------------------------------- | --------------- | --------------------------------------------------------------------- |
| `package.json`                                      | Modify          | `pixi.js@^8.18.1`, `@pixi/react@^8.0.5` 追加（particle-emitter 無し） |
| `src/main.tsx`                                      | Modify (条件付) | Step 0-B 採用時のみ。A 採用なら変更なし                               |
| `src/ui/pixi/core/PixiStage.tsx`                    | New             | Application wrapper + extend()                                        |
| `src/ui/pixi/core/usePixiApp.ts`                    | New             | useApplication ラッパ + features.move=false                           |
| `src/ui/pixi/types/pixiTypes.ts`                    | New             | BattlePixiProps                                                       |
| `src/ui/pixi/battle/BattleCanvas.tsx`               | New             | オーバーレイ host                                                     |
| `src/ui/pixi/battle/layers/BackgroundLayer.tsx`     | New             | 空                                                                    |
| `src/ui/pixi/battle/layers/CharacterLayer.tsx`      | New             | 空                                                                    |
| `src/ui/pixi/battle/layers/EffectLayer.tsx`         | New             | テスト粒子                                                            |
| `src/ui/pixi/battle/PixiEffectBridge.ts`            | New             | 骨格                                                                  |
| `src/ui/css/pages/battle/battle-layout.css`         | Modify          | `.battle-pixi-host` z-index:15 追加                                   |
| `src/ui/html/battleHtml/BattleScreen.tsx`           | Modify          | `<BattleCanvas>` 挿入                                                 |
| `src/ui/html/campsHtml/Guild/GuildBattleScreen.tsx` | Modify          | `<BattleCanvas>` 挿入（複製構造）                                     |
| `src/ui/pixi/**/__tests__/*`                        | New             | §4 テスト                                                             |
| `vite.config.ts`                                    | **変更しない**  | optimizeDeps 不要（§0B-4）                                            |

**不可侵:** `src/domain/cards/decks/deck.ts`, `deckReducer.ts`（触れない）

---

## 4. Testing Strategy

- **Unit:** `PixiStage` が `@testing-library/react` でクラッシュせず描画開始（WebGL は jsdom 非対応 → `app.init` をモック or `happy-dom` + canvas モック。最低限 import/レンダリングが throw しないこと）。
- **型:** `BattlePixiProps` が `useBattleOrchestrator` 返却値で型エラーにならない（`npm run build` の tsc で担保）。
- **回帰:** `npm run test:run` 既存全 pass（バトルロジックに副作用ゼロ）。
- **Manual:**
  1. battle 画面で透過キャンバスが `.battle-field` 上にオーバーレイ表示。
  2. カード選択/発動/ボタンが従来通り操作可能（クリック貫通 OK）。
  3. テスト粒子が 1 個描画される。
  4. **battle → camp → battle を 3 往復してクラッシュしない**（StrictMode #602 検証＝Step 0 の合否）。
  5. ブラウザリサイズでキャンバスが `.battle-field` に追従。
  6. DevTools で WebGL context 生成を確認（WebGPU でないこと）。

---

## 5. Verification（受け入れ基準・pass/fail）

- [ ] `npm run build` がエラーなく完了（tsc 含む）
- [ ] `npm run test:run` 既存テスト全 pass
- [ ] battle/guild-battle 両画面でキャンバスがオーバーレイ表示
- [ ] 既存 DOM 操作（カード・ボタン）が阻害されない
- [ ] テスト粒子が描画される
- [ ] **StrictMode 下で画面往復してもクラッシュしない（issue #602 回避確認）** — 採用案: **A 案**（React.lazy + mountedRef ゲート）。実機 battle↔camp 3往復で最終合否判定
- [ ] リサイズでキャンバス追従
- [ ] WebGL（WebGPU でない）で動作
- [ ] `vite.config.ts` 無変更・`@pixi/particle-emitter` 未導入

---

## 6. Dependencies / Risks

| Risk                                                       | 対策                                                                                                                           |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| @pixi/react #602（StrictMode crash, OPEN・未修正）         | Step 0 で A/B いずれか確定。受け入れ基準に往復テスト明記                                                                       |
| @pixi/react 開発停滞（最新 v8.0.5 が 2024-12、issue 滞留） | バージョン固定（`^8.0.5`）。重大化したら Phase 2 で vanilla pixi.js 直叩きへ退避可能な構成（Bridge を命令型 API に寄せてある） |
| @pixi/particle-emitter v8 死亡                             | Phase 1 は粒子ライブラリ非依存。Phase 2 で v8 内蔵 `ParticleContainer` を第一候補に再評価（別タスク化）                        |
| Vite top-level await hang                                  | TextureManager を Phase1 で作らない＝罠を踏まない                                                                              |
| pointermove 漏れ                                           | `renderer.events.features.move=false` を Step 5 で適用                                                                         |

- **Blocks:** Phase 2(エフェクト移行)・3(アセット)・4(最適化)
- **Blocked by:** なし（最初のフェーズ）

## 7. References

- 旧計画（抽象・要 archive）: `.claude/docs/vision/plans/pixijs_phase1_foundation.md`
- 元提案: `ゲームエンジン統合提案書.docx`（リポジトリ管理外）
- @pixi/react v8 docs: https://react.pixijs.io/ / extend: https://react.pixijs.io/extend/
- PixiJS v8 renderers（WebGPU 非推奨根拠）: https://pixijs.com/8.x/guides/components/renderers
- #602 StrictMode: https://github.com/pixijs/pixi-react/issues/602
- particle-emitter v8 死亡: https://github.com/pixijs-userland/particle-emitter
- 既存実装: `BattleScreen.tsx` / `useCardAnimation.tsx`(componentsHtml) / `uiConstants.ts:125` / `useBattleOrchestrator.ts`
