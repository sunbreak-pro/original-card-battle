# HISTORY.md - 変更履歴

> セッション単位の変更履歴（降順）。各エントリは「概要」+「変更点」。要約は `README.md` の Development History、進行状況は `MEMORY.md`。古いエントリは肥大化したら `HISTORY-archive.md` へ退避。

### 2026-06-27 - 戦闘プロトタイプ（間合い×スタミナ最小検証台）実装・マージ

#### 概要

「リアル性コンセプト v2」戦闘主柱（間合いの読み合い × スタミナの消耗）が遊びとして成立するかを本実装前に検証する throwaway プロトタイプを `src/ui/prototype/` に隔離実装し、PR #14 を origin/main へマージ。当初タスクが参照した計画書が実在しなかったため concept §1 / tier1 R1-5・R1-6 / タスク本文から計画書を再構成して実装。役割分担（role-engineer 実装 → session-verifier → role-qa 独立監査）で進め、role-qa PASS-with-fixes。ユーザー実機プレイで「ゲーム性はかなり面白い」と評価。

#### 変更点

- **隔離実装（既存無改変）**: `src/ui/prototype/` に engine（types/constants/combat/cards/enemy/battleReducer）+ UI（PrototypeBattle/DistanceTrack/CombatantPanel/HandView/BattleLog/ResultOverlay）+ `prototype-battle.css`（`.prototype-battle` スコープ）+ ルート `prototype.html` 起動口。既存トラッキングファイルの差分ゼロ、`useBattleOrchestrator`/`BattleScreen`/不可侵 deck は無改変・非 import。デッキ操作は自前 Fisher-Yates
- **メカニクス**: 間合い 近/中/遠（相性ベース・固定強弱なし、diff 0/1/2→×1.0/0.5/0.15）、スタミナ MAX20・回復 近+1/中+2/遠+3・閾値8未満で確定威力減衰（floor 0.4、確率ミスではない）、剣士6カード、リーチ型の敵1体（中=キルゾーン、矯正技 shove）。敵フェーズは純 reducer の END_TURN 内で同期解決（StrictMode 安全）
- **計画書再構成**: `.claude/docs/vision/plans/2026-06-27-battle-prototype-range-stamina.md` を新規作成（Steps/Files/Verification + チューニング所見）。当初参照のファイルが不在だったためユーザー承認のうえ復元
- **検証ゲート**: `npm run build` 緑 / `test:run` 156件緑（プロトタイプ純関数 33件）/ `npx eslint src/ui/prototype` 0 エラー（全体 lint の16エラーは origin/main 既存・本変更外）
- **balance 調整（sim 由来）**: ヘッドレスで4戦略を検証し初期値（敵HP42・穂先6）は勝ち筋ほぼ無しと判明 → 設計意図を保ち `reach_thrust` 6→3（遠を回復の逃げ場に）・`ENEMY_MAX_HP` 42→38。ゴリ押し/カイトは負け・賢い立ち回りで勝てる帯を維持。最終バランスは実機プレイで詰める前提
- **Git**: PR #14 を origin/main へマージ（merge `9b88536` / feat `c77907c`）、マージ済 feat ブランチをローカル・リモート削除。プロトタイプは throwaway（本番非流用）

### 2026-05-23 - PixiJS Phase 1 基盤実装（ハイブリッド描画レイヤー導入）

#### 概要

既存 DOM/CSS バトル演出を温存したまま、`.battle-field` 上に透過 PixiJS キャンバスを重ねる「ハイブリッド描画」基盤を導入。lead-pipeline 重ティアのフルチェーン（session-manager START → role-pm → role-engineer → session-verifier → role-qa + security-reviewer 並列）で実施。Step 0 では @pixi/react v8 の未解決 issue #602（StrictMode 二重マウント時の WebGL context stale）を A 案（React.lazy + mountedRef post-commit ゲート）で構造的回避。QA 判定 PASS-with-fixes（Blocker0 / 実害ある Major0）。実機検証で起動・オーバーレイ表示・クリック貫通・StrictMode 往復・リサイズ追従を全て OK 確認。

#### 変更点

- **計画書（コードレベル版）作成**: `docs/vision/plans/2026-05-17-pixijs-phase1-code-level.md` を新規作成し旧抽象版 `pixijs_phase1_foundation.md` を超越。§0B で旧計画を 6 観点監査し致命的誤り 3（particle-emitter 死亡・z-index:5 誤り・StrictMode #602 未考慮）+ 要修正 4 を是正。実装完了でファイルを `.claude/archive/` へ移動・Status=COMPLETED
- **依存追加**: `pixi.js@^8.18.1` + `@pixi/react@^8.0.5` の 2 本のみ。`@pixi/particle-emitter` は v8 メンテ停止のため**意図的に未導入**。`vite.config.ts` も無変更（pixi v8 は native ESM で optimizeDeps 不要）
- **新規 `src/ui/pixi/` ツリー**: `core/PixiStage.tsx`（`extend({Container,Graphics,Sprite,Text})` + `<Application>` 透過設定 + `preference:'webgl'` 固定）、`core/usePixiApp.ts`（`useApplication` 再エクスポート + `usePixiEventGuard` で `renderer.events.features.move=false` を 1 箇所に確定）、`battle/BattleCanvas.tsx`（Step 0-A の React.lazy + mountedRef マウントガード）、`battle/layers/`（Background/Character は空コンテナ、Effect はテスト粒子）、`battle/PixiEffectBridge.ts`（命令型 API シグネチャ骨格）、`types/pixiTypes.ts`（`BattlePixiProps`）
- **画面統合**: `BattleScreen.tsx` と `GuildBattleScreen.tsx` の両 `.battle-field` 直後に `<BattleCanvas>` を挿入（後者は前者を再利用せず構造複製のため両ファイル個別修正）
- **CSS レイヤリング**: `battle-layout.css` に `.battle-screen .battle-pixi-host { z-index: 15; pointer-events: none; ... }` 追加（field=10 と hand=100 の間）。CSS 共通のため両画面でスコープ機能
- **Step 0 採用**: A 案（React.lazy + useRef マウントガード）。`main.tsx` の `<StrictMode>` は無変更で維持。B 案（dev のみ StrictMode 外し）は不要だったため不採用
- **テスト**: 新規 4 件（`pixiFoundation.test.ts`）+ 既存全 pass（123/123、回帰ゼロ）。WebGL/reconciler 依存テストは jsdom 制約により意図的に契約・layer に限定（コメントで理由明示）
- **意図的 lint disable 3 箇所**: `BattleCanvas.tsx`(set-state-in-effect=Step 0-A post-commit ゲート)、`usePixiApp.ts`(immutability=PixiJS 命令型 API 設定)、`EffectLayer.tsx`(exhaustive-deps=rAF ループ再起動防止)。いずれも該当行限定 + 理由コメント付き
- **独立監査**: role-qa（PASS-with-fixes）+ security-reviewer 並列。security は 9 脆弱性が**全て dev/build 専用ツールチェーン由来・非 PixiJS 由来**と確定、リリースブロックなし。ただし既存の `package.json` の rollup ネイティブバイナリ・ハードコード（time bomb）を発見 → MEMORY 予定 #5 として独立タスク起票（Phase 1 コミットには混ぜない）
- **方針合意**: rollup ハードコード除去は別タスク・別コミット。Phase 1 は実機検証後に commit/PR（ユーザー方針）

### 2026-05-17 - バトルロジック脆弱性修正（V-CHAIN-01 / V-ENM-02 + 回帰テスト + 脆弱性ガイド正本化）

#### 概要

README と脆弱性ガイドの不整合（V-EXEC/V-PHASE 系の完了表記）を実コードで決着。lead-pipeline 重ティアのフルチェーン（session-manager START → general-purpose 事実調査 → role-engineer 実装 → role-qa 別コンテキスト独立監査）で実施。**ground truth: README が正・ガイドが陳腐化**。当初13件と見えた修正は実体2件（V-CHAIN-01 / V-ENM-02）に縮小。QA 判定 PASS-with-fixes（コードブロッカー0、フォローアップ1件起票）。

#### 変更点

- **事実確定（調査）**: V-EXEC-01/02/03/04・V-PHASE-01/02・V-DMG-MANAGE-01 は全て修正済み（README 通り、ガイドが古い）。Phase 4 もガイドの「1/7」は陳腐化で実態 6/7。真に未修正は V-CHAIN-01・V-ENM-02 の2件のみと file:line 証拠付きで確定
- **V-CHAIN-01 修正（魔術師共鳴1枚遅れ）**: `elementalSystem.ts` に純粋関数 `getDamageModifierIncludingCard`（`onCardPlay` 後の仮想 state で modifier 算出）追加。`useElementalChain` に `getDamageModifierForPlay(card)`、`useClassAbility` にオプショナル戻り値追加（swordsman 非影響）。`useBattleOrchestrator` の damage modifier 経路を play-aware に差し替え + `useCallback` 切り出し
- **V-ENM-02 修正（敵AI preview/execute 乖離）**: `enemyAI.ts` に `resolveEnemyAction`/`clearResolvedActionCache` 新設。`(id,hp,maxHp,turn,callIndex)` でメモ化し execute/preview/EnemyFrame レンダーの3経路が同一結果を共有。確率分布維持。`initializeBattle` 冒頭で cache クリア（バトル間リーク防止）
- **回帰テスト追加（4ファイル44件）**: `elementalSystem.test.ts`(13)・`bleedDamage.test.ts`(8)・`phaseLogic.test.ts`(15)・`enemyAI.test.ts`(8)。既修正項目（V-DMG-06/10・V-CLASS-13/04・V-PHASE-01/02・V-DMG-01）の固定化 + 新規修正検証
- **脆弱性ガイド正本化**: `vulnerability-remediation-guide.md` を実態へ更新（Phase 4=7/7、Phase 5 全 ✅FIXED、証拠 file:line 併記、冒頭カウント 35 fixed / 71 remaining）。README は元から正のため変更なし
- **QA 独立監査**: 別コンテキスト role-qa が型/lint/test 再実行（全 PASS、119テスト）、engineer 委譲懸念4点を裁定。Major1件 = `getResonanceEffects` の resonance-debuff 1-card-lag 非対称が残存（damage 側のみ修正で非対称化）→ known-issue 001 として起票
- **フォローアップ起票**: `docs/known-issues/001-resonance-debuff-card-lag.md` 新規 + INDEX 更新 + MEMORY 予定 #2

#### 次

known-issue 001（resonance debuff 1-card-lag）の修正は別タスク。本変更は未コミット（main ブランチ + .claude リファクタ塊と分離コミット必要、ユーザー承認待ち）。

### 2026-05-17 - .claude ハーネス構造を life-editor 準拠へリファクタ（Phase A-E 完了）

#### 概要

グローバル CLAUDE.md 標準構造との乖離を解消するため、`.claude/` を life-editor 運用に合わせて全面再編。ユーザー確認で「フル移行（git mv 標準化）」「MEMORY/HISTORY へ移行」「プロジェクト固有エージェント作成」の 3 方針を決定。Phase A（ディレクトリ移行）〜 Phase E（課題整理）を 1 セッションで完遂。

#### 変更点

- **Phase A — ディレクトリ移行**: `git mv` で `.claude/code_overview/` → `.claude/docs/code-explanation/`（15 ファイル + サブディレクトリ）、`feature_plans/` の tracked 4 ファイル → `docs/vision/plans/`、untracked PixiJS 4 ファイルは `mv`、`memories/LESSONS_LEARNED.md` → `docs/known-issues/`、`memories/basecamp_consolidation_completed.md` → `archive/`。空ディレクトリ削除
- **クロスリンク一括更新**: 非 worktree の .md 11 ファイルで `.claude/code_overview/`→`.claude/docs/code-explanation/` 等を sed 置換。残存ゼロ確認。`worktrees/` 配下は別 git worktree のため非対象
- **Phase B — 標準インフラ新設**: `docs/vision/core.md`（Vision・設計原則）、`docs/known-issues/INDEX.md`（8 知見カタログ + 脆弱性ガイドへのポインタ）、`MEMORY.md`（進行中/直近完了/予定、旧 TODO バックログを移管）、`HISTORY.md`（本ファイル）を作成。`docs/requirements/` ディレクトリも用意。`docs/INDEX.md` を標準構造 + ゲーム設計書の二層構成に書き換え
- **Phase C — CLAUDE.md 再構成**: §0 Meta（役割/更新規則/タスク運用/関連ドキュメント表）追加、Task Completion Rule を TODO/README → MEMORY/HISTORY モデルへ書き換え、Document System 節新設、References 表を更新。223 行（400 行以下目標を維持）。`TODO.md` を MEMORY.md への薄いポインタへ縮小
- **Phase D — プロジェクト固有エージェント**: `agents-lib/projects/original-card-battle/` に分析特化 3 体作成（`card-battle-balance-auditor` = データ vs 設計書整合 / `card-battle-state-invariant-checker` = Context・React19・不可侵コード・セーブ網羅 / `card-battle-battle-logic-validator` = バトルフェーズ・ダメージ・バフ整合）。`.claude/agents/` にシンボリックリンク、`AGENT_INDEX.md` に節 + 最終更新追記
- **Phase E — 課題整理**: 脆弱性ガイドを精読し MEMORY.md「予定」を優先度付きで具体化。**ドキュメント不整合を発見**: README は V-EXEC/V-PHASE 系を「2026-02-05 完了」と記載するが、vulnerability-remediation-guide.md の Phase 5 では未修正扱い。最優先で実コード確認・寄せ先決定が必要（MEMORY.md 課題 #1）
- **方針**: タスク管理は TODO.md/README 履歴 → MEMORY.md/HISTORY.md へ移行（task-tracker 経由運用）。README の Development History は完了履歴の要約として継続

#### 次

MEMORY.md 課題 #1（README vs 脆弱性ガイドの V-EXEC/V-PHASE 不整合）を `card-battle-battle-logic-validator` で検証し寄せ先決定。未コミット（ユーザー確認後に commit 予定）。
