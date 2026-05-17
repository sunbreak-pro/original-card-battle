# HISTORY.md - 変更履歴

> セッション単位の変更履歴（降順）。各エントリは「概要」+「変更点」。要約は `README.md` の Development History、進行状況は `MEMORY.md`。古いエントリは肥大化したら `HISTORY-archive.md` へ退避。

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
