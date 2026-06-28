# Plan: 戦闘コア — ゲームエンジン Bake-off（PixiJS版 × Phaser 3版）

> **Status**: PLANNED
> **Created**: 2026-06-28
> **Task**: MEMORY.md 進行中「リアル性コンセプト v2 — 戦闘システム上流確定 + プロトタイプ計画」の次ステップ
> **Project**: `/Users/newlife/dev/apps/original-card-battle`
> **Branch（実装時）**: `feat/battle-engine-bakeoff`（main から分岐）
> **前提プラン**: `2026-06-27-battle-prototype-range-stamina.md`（検証台 — 実装・マージ済 / origin/main）
> **入力仕様**: `docs/vision/concept-v2.md` §1（戦闘主柱）/ `docs/requirements/tier1-core.md` R1-5（スタミナ）・R1-6（間合い）

---

## Context

検証台（React DOM 版）で「間合い × スタミナの押し引き」が**遊びとして面白い**ことは実機確認済み（ユーザー評価「ゲーム性はかなり面白い」）。次は**本番想定**で、ゲームエンジンを用いて作り直す。

ただしエンジンを 1 つに即決せず、**同じ検証済みコアを PixiJS版 と Phaser 3版 の両方に載せ、実際に触って（肌感で）どちらが本作に合うか比較**してから決める。検証で面白さが確定しているのは「描画より下の純ロジック」なので、**ロジックは 1 本に固定し、描画エンジンだけ 2 通り作る**のが要点。これでゲーム性は完全に同一になり、比較は「描画・演出・触り心地・開発体験」だけに絞られる（apples-to-apples）。

### このプランの位置づけ（重要）

- **本気で作る**。品質が高ければ、勝った側のエンジンがそのまま **Tier 1 本実装の描画基盤**になる前提。
- そのため**共有コアは本番品質**（型・テスト・エンジン非依存）で作る。どちらのエンジンが勝っても、コアはそのまま生き残る＝二度手間にならない設計。
- 検証台（`src/ui/prototype/`、DOM版）は**触らず残す**。DOM／Pixi／Phaser の 3 すくみで触り比べられる「対照群」として有用。

### 検証したい問い（このプランのゴール）

1. **どちらのエンジンが「肌感」で気持ちいいか**（最重要・ユーザー判断）
2. 演出（ダメージ・移動・疲労）の作りやすさはどちらが上か
3. カード UI（テキスト主体のリッチ UI）はどちらで素直に作れるか
4. 既存スタック（React / PixiJS Phase 1 資産）との統合コストの差
5. バンドルサイズ・多スプライト時のパフォーマンスの差
6. **本実装へ発展させたときの保守性**はどちらが高いか

### Non-goals（含めない）

剣気・崩し（設計未確定。MEMORY の次々ステップ）／観察・ステージ・不可逆性・セーブ・魔術師／本番 `BattleScreen`・`useBattleOrchestrator` への統合／網羅テスト／バランスの作り直し（検証台の数値をそのまま使う）。

### 制約（厳守）

- **ゲーム性・数値は検証台と完全一致**させる（比較の公平性のため `core/` は検証済み `engine/` を昇格コピー、数値は `constants.ts` のまま）。
- **不可侵コード**（`src/domain/cards/decks/deck.ts` / `deckReducer.ts`）変更禁止。コアは自前デッキ操作を持つ（検証台同様、import しない）。
- 既存の本番資産（`src/domain/` / `src/contexts/` / `BattleScreen` / `useBattleOrchestrator`）を**変更しない**。
- PixiJS Phase 1 資産（`src/ui/pixi/`）は**読み取り専用で再利用可**。Phase 1 ファイルは編集しない。
- 検証台（`src/ui/prototype/`）は**編集しない**（対照群として保全）。
- TS strict / explicit return types（public API）/ named exports / `import type` / `as const`（enum 禁止）。
- CSS は各アダプタ専用クラスでスコープ（`.pixi-bakeoff` / `.phaser-bakeoff`）。サイズは vh/vw（border のみ px）。UI テキストは日本語、コード/コメントは英語。
- **新規依存は `phaser` のみ許可**（本フェーズの目的。検証台の $0 制約はここでは解除）。`npm install` 前に下記「Pre-flight」の rollup 時限爆弾対処を必ず実施。

---

## アーキテクチャ（3 層・コア共有）

```
src/ui/battle-lab/
├── core/                       ← 共有・エンジン非依存・本番品質（検証台 engine/ を昇格）
│   ├── constants.ts            数値の正（検証台と同一）
│   ├── types.ts                型定義
│   ├── combat.ts               純関数（相性・疲労・距離・ダメージ）
│   ├── cards.ts                カード定義 + 自前デッキ操作
│   ├── enemy.ts                敵定義 + 決定的 AI
│   ├── battleReducer.ts        純 reducer（PLAY_CARD / END_TURN〔敵フェーズ内包〕/ RESTART）
│   ├── viewModel.ts            ★新規・エンジン非依存の表示用導出（予測ダメージ・不可理由・ラベル）
│   └── __tests__/              純関数テスト（検証台の 33 件 + viewModel 分）
├── adapters/
│   ├── pixi/                   ← PixiJS 描画アダプタ（@pixi/react + Phase 1 資産再利用）
│   └── phaser/                 ← Phaser 3 描画アダプタ（Phaser.Scene）
└── shared/                     ← 任意・両アダプタ共通の純 UI ヘルパ（色・整形）
```

**設計の肝**: アダプタは「`BattleState` を受け取って描く」「入力を `BattleAction` に変換して dispatch する」だけ。ゲームロジックは一切持たない。`viewModel.ts` を新設し、検証台では React コンポーネント内に埋まっていた「カードごとの予測ダメージ・プレイ不可理由」の導出を純関数として切り出す（両アダプタで重複させないため）。

**起動口**: ルートに 2 つの HTML を新規追加し、横並びで触り比べる。

- `pixi-bakeoff.html` → `adapters/pixi/main.tsx`
- `phaser-bakeoff.html` → `adapters/phaser/main.ts`

---

## エンジン別の実装方針

### Adapter A — PixiJS版（`adapters/pixi/`）

- 状態は React の `useReducer(battleReducer)` で保持し、`@pixi/react` で描画（Phase 1 と同じ React×Pixi 統合パターン）。
- **Phase 1 資産を読み取り再利用**: `PixiStage` / `usePixiApp`（core）、`ParticlePresets` / `TextureManager`（shared）、3 レイヤー構成（Background/Character/Effect）。これが Pixi 側の「既存統合が速い」優位の実証になる。
- 描画対象: 間合いトラック（近/中/遠）／両者パネル（HP・スタミナ・疲労帯・ガード）／手札（cost・有効間合い・予測ダメージ・footwork・不可理由）／ログ／結果オーバーレイ。
- 演出: ダメージ数字ポップ・移動トゥイーン・疲労時のカード減衰表示を Pixi のティッカー/トゥイーンで。
- 入力: カードスプライトの pointer イベント → `dispatch({type:'PLAY_CARD'})` 等。
- カード UI のようなテキスト主体部分は **DOM オーバーレイ併用可**（Pixi の現実的な使い方。比較メモに「どこまで DOM に逃がしたか」を記録）。

### Adapter B — Phaser 3版（`adapters/phaser/`）

- `npm install phaser`（新規依存・本フェーズ唯一の追加）。
- `Phaser.Game` を 1 つ、`BattleScene` 1 シーンで構成。React 非依存（薄い mount のみ）。
- 状態管理: コアは純 reducer なので、シーン側に極小ストア（`let state = initState(); const dispatch = a => { state = battleReducer(state, a); scene.render(state); }`）を置くだけ。React の useReducer と等価。
- 描画対象: 上と同一。Phaser の `Text` / `Rectangle` / `Container` / `Tween` で構築。
- 入力: Phaser のインタラクティブゾーン → dispatch。
- 演出: Phaser Tween/Particle で移動・被弾・疲労を表現。
- カード UI（テキスト主体）の作りにくさ／作りやすさを比較メモに記録（Phaser はテキストレイアウトが弱め、という仮説の検証）。

> **公平性ルール**: 両アダプタは `core/constants.ts` の数値・`battleReducer` を**そのまま**使う。バランス調整・独自ロジックを片方だけに入れない。差が出るのは描画・演出・入力・コード量・触り心地だけにする。

---

## 比較評価（両方完成後に埋める・意思決定の根拠）

| 軸               | 配点観点                        | PixiJS版         | Phaser 3版       |
| ---------------- | ------------------------------- | ---------------- | ---------------- |
| 肌感（最重要）   | 触って気持ちいいか・テンポ      | （ユーザー記入） | （ユーザー記入） |
| 演出のしやすさ   | tween/particle/数字ポップ       |                  |                  |
| カード UI        | テキスト主体リッチ UI の素直さ  |                  |                  |
| 既存統合         | React / Pixi Phase 1 資産の活用 |                  |                  |
| 開発体験(DX)     | 実装時間・コード量・詰まり      |                  |                  |
| バンドルサイズ   | 追加 KB                         |                  |                  |
| パフォーマンス   | 多スプライト/演出時のフレーム   |                  |                  |
| 本実装への発展性 | 保守性・拡張容易さ              |                  |                  |

**意思決定ゲート**: 両方を実機で触り比べ → 上表を埋める → **1 エンジンを選定** → それが Tier 1 本実装の描画基盤になる。コアはエンジン非依存なので、選定結果に関わらずそのまま継承。負けた側のアダプタは archive する（コアは残す）。

---

## Steps

### Phase 0 — Pre-flight（実装着手前・別コミット）

- [ ] 0a. `docs/realism-concept-v2` ブランチ（v2 ドキュメント + 本プラン）を **main へ PR・マージ**（次セッションが「検証台コア + 本プラン」を両方持つ main から分岐できるようにする）
- [ ] 0b. **rollup 時限爆弾の除去**（MEMORY 技術的負債 #5・独立コミット）: `package.json` の `@rollup/rollup-linux-arm64-gnu` ハードコード行を削除 → `rm -rf node_modules package-lock.json && npm install`（`--force` なし）→ `npm audit fix`。これをやらないと darwin で `npm install phaser` が EBADPLATFORM で失敗する
- [ ] 0c. `npm install phaser`（時限爆弾除去後）。`package.json` / lockfile を確認

### Phase 1 — 共有コア昇格（`core/`）

- [ ] 1. `src/ui/battle-lab/core/` に検証台 `src/ui/prototype/engine/` の 6 ファイル（constants/types/combat/cards/enemy/battleReducer）+ `__tests__/` を**コピー昇格**（検証台は残す）。これが今後の正
- [ ] 2. `core/viewModel.ts` 新設 — `BattleState` から「カードごとの予測ダメージ・プレイ可否と不可理由・間合いラベル・敵の有効間合いヒント」をエンジン非依存に導出する純関数群（検証台 `HandView.tsx` に埋まっていたロジックを抽出）
- [ ] 3. `core/__tests__/viewModel.test.ts` — viewModel 純関数の最小テスト（予測ダメージ・不可理由の境界）

### Phase 2 — PixiJS アダプタ（`adapters/pixi/`）

- [ ] 4. `adapters/pixi/PixiBattle.tsx` — `useReducer(battleReducer)` + `@pixi/react`。Phase 1 の `PixiStage`/`usePixiApp` を読み取り再利用
- [ ] 5. Pixi 描画コンポーネント群 — 間合いトラック / 両者パネル / 手札 / ログ / 結果。演出（ダメージポップ・移動トゥイーン・疲労減衰表示）。`ParticlePresets` 再利用
- [ ] 6. `adapters/pixi/main.tsx`（StrictMode mount）+ ルート `pixi-bakeoff.html`（新規）+ `pixi-bakeoff.css`（`.pixi-bakeoff` スコープ）

### Phase 3 — Phaser 3 アダプタ（`adapters/phaser/`）

- [ ] 7. `adapters/phaser/BattleScene.ts` — `Phaser.Scene`。極小ストア（`battleReducer` を手動適用）+ シーン再描画。間合いトラック / 両者パネル / 手札 / ログ / 結果
- [ ] 8. Phaser 演出 — Tween/Particle で移動・被弾・疲労。入力ゾーン → dispatch
- [ ] 9. `adapters/phaser/main.ts`（`Phaser.Game` 構成）+ ルート `phaser-bakeoff.html`（新規）

### Phase 4 — 比較・意思決定

- [ ] 10. 両アダプタを `npm run dev` で起動し**実機で触り比べ**、上記「比較評価」表を埋める
- [ ] 11. 計測: バンドルサイズ（`npm run build` の出力差）・体感パフォーマンス・コード行数を記録
- [ ] 12. **エンジン選定をユーザーと確定**し、本プラン末尾「決定記録」に追記。負けたアダプタは `archive/` 方針を決定

---

## Files

| File                                                | Operation     | Notes                                                                          |
| --------------------------------------------------- | ------------- | ------------------------------------------------------------------------------ |
| `package.json`                                      | Edit          | rollup 時限爆弾行を削除（0b）/ `phaser` 追加（0c）。**Phase 0 の独立コミット** |
| `src/ui/battle-lab/core/*.ts`（6+1）                | Create        | 検証台 engine/ の昇格コピー + `viewModel.ts`                                   |
| `src/ui/battle-lab/core/__tests__/*`                | Create        | 既存 33 件相当 + viewModel テスト                                              |
| `src/ui/battle-lab/adapters/pixi/PixiBattle.tsx`    | Create        | React×Pixi コンテナ                                                            |
| `src/ui/battle-lab/adapters/pixi/components/*`      | Create        | Pixi 描画群                                                                    |
| `src/ui/battle-lab/adapters/pixi/main.tsx`          | Create        | StrictMode mount                                                               |
| `src/ui/battle-lab/adapters/pixi/pixi-bakeoff.css`  | Create        | `.pixi-bakeoff` スコープ                                                       |
| `src/ui/battle-lab/adapters/phaser/BattleScene.ts`  | Create        | Phaser シーン本体                                                              |
| `src/ui/battle-lab/adapters/phaser/main.ts`         | Create        | `Phaser.Game` 構成                                                             |
| `pixi-bakeoff.html`                                 | Create        | ルート起動口（Pixi）                                                           |
| `phaser-bakeoff.html`                               | Create        | ルート起動口（Phaser）                                                         |
| `src/ui/pixi/**`                                    | **Read-only** | Phase 1 資産を再利用（編集禁止）                                               |
| `src/ui/prototype/**`                               | **Untouched** | DOM 対照群として保全                                                           |
| `src/domain/cards/decks/deck.ts` / `deckReducer.ts` | **Untouched** | 不可侵                                                                         |

---

## Verification

- [ ] `npm run build`（`tsc -b && vite build`）通過（型ゲート + 2 つの新 HTML エントリも含めビルド可。`vite.config.ts` の rollup `input` に 2 エントリ追加が必要なら追記）
- [ ] `npm run test:run` 通過（コア 33+件 + viewModel テスト green、既存テストも green）
- [ ] `npm run lint` 通過（スコープ・未使用なし・import type 準拠）
- [ ] PixiJS版: `http://localhost:5173/pixi-bakeoff.html` で 1 戦プレイ可能（勝敗・リスタート動作）
- [ ] Phaser版: `http://localhost:5173/phaser-bakeoff.html` で 1 戦プレイ可能（勝敗・リスタート動作）
- [ ] **公平性**: 同じ初期手札シード（`Math.random` を両者同条件）で両版の挙動・ダメージが一致する
- [ ] 比較評価表が両列とも埋まっている
- [ ] 決定記録（採用エンジン + 理由）が本プランに追記されている

---

## 次セッション キックオフプロンプト（コピペ用）

> 下記をそのまま次セッション冒頭に貼る。Phase 0（Pre-flight）から開始する。

```
戦闘コアのゲームエンジン bake-off を実装する。計画書は
.claude/docs/vision/plans/2026-06-28-battle-engine-bakeoff.md（これが実装契約）。

目的: 検証済みの「間合い×スタミナ」コアを PixiJS版 と Phaser 3版 の両方に載せ、
肌感で触り比べてエンジンを選ぶ。ゲーム性・数値は検証台と完全一致させる（描画だけ2通り）。

厳守:
- 不可侵コード（deck.ts / deckReducer.ts）と本番資産（domain/ contexts/ BattleScreen
  useBattleOrchestrator）は変更しない。検証台 src/ui/prototype/ も編集せず対照群として残す。
- PixiJS Phase 1（src/ui/pixi/）は読み取り再利用のみ。
- 新規依存は phaser のみ。npm install 前に rollup 時限爆弾（MEMORY 技術的負債 #5）を必ず除去。
- TS strict / explicit return types / named exports / import type / as const。
- 共有コア（core/）は本番品質で作る（品質次第で Tier 1 本実装の基盤に昇格するため）。

進め方（重・層横断なので pipeline）:
1. task-tracker START（競合チェック込み）
2. main を pull し feat/battle-engine-bakeoff を切る（検証台コアと本プランが乗った main 前提）
3. Phase 0 Pre-flight: rollup 時限爆弾除去（独立コミット）→ npm install phaser
4. Phase 1 共有コア昇格（engine/ を core/ へコピー + viewModel.ts 抽出）
5. Phase 2/3 の Pixi/Phaser アダプタは独立ユニット → role-engineer を2体並列起動して
   1体ずつ担当（再帰起動はせずメインが Agent ツールで逐次/並列起動）
6. session-verifier → role-qa（別コンテキスト独立レビュー）
7. 両版を実機で触り比べ → 比較表を埋める → エンジン選定 → task-tracker END

検証台の所見: コアは React/DOM 非依存の純ロジックで33テスト付き、そのまま core/ に昇格できる。
バランスは constants.ts のまま（reach_thrust power3 / ENEMY_MAX_HP 38 で調整済み）。
```

---

## Pre-flight チェックリスト（前準備・本セッションで確認済み）

| 項目                 | 状態      | メモ                                                                                                                                                                                                  |
| -------------------- | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 検証台コアの再利用性 | ✅ 確認済 | `src/ui/prototype/engine/` は React/DOM 非依存・33 テスト付き・そのまま core/ 昇格可                                                                                                                  |
| PixiJS 在庫          | ✅ あり   | `pixi.js@8.18.1` / `@pixi/react@8.0.5` 導入済。Phase 1 資産 `src/ui/pixi/`（11 ファイル）再利用可                                                                                                     |
| Phaser 在庫          | ❌ 未導入 | Phase 0c で `npm install phaser`。先に時限爆弾除去が必要                                                                                                                                              |
| rollup 時限爆弾      | ⚠️ 未対処 | `package.json` に `@rollup/rollup-linux-arm64-gnu@^4.57.1` ハードコード残存。darwin で `npm install` 非 force が落ちる。Phase 0b で除去（MEMORY 技術的負債 #5・独立コミット）                         |
| ブランチ状態         | ⚠️ 要整理 | 検証台コア + 前プランは **origin/main(9b88536)**。本プラン + v2 docs は **docs/realism-concept-v2**（main と分岐: main +11 / docs +5）。Phase 0a で docs を main へマージしてから feat ブランチを切る |
| vite ビルド入力      | ℹ️ 要確認 | Vite 既定 input は `index.html` のみ。新 HTML 2 つを `npm run build` 対象にするなら `vite.config.ts` の `build.rollupOptions.input` に追記（dev で開くだけなら不要）                                  |

---

## 決定記録（Phase 4 後に追記）

> 採用エンジン: （未定）
> 理由: （未定）
> 負けたアダプタの扱い: （未定）
