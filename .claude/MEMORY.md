# MEMORY.md - タスクトラッカー

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `HISTORY.md`、要約は `README.md` の Development History。

## 進行中

### 🔧 リアル性コンセプト v2 — ギャップ分析 + 戦闘 UI/UX モックアップ（着手日: 2026-06-11）

**対象**: `.claude/docs/vision/concept-v2.md`（承認済コンセプト）/ `src/domain/battles/` / `src/domain/dungeon/` / `.claude/docs/*_document/`
**計画書**: `.claude/docs/vision/2026-06-11-realism-concept-kickoff.md`（入力資料）/ `.claude/docs/vision/concept-v2.md`（APPROVED 2026-06-11）

- 前回: concept-v2.md 承認 → UI モックアップ作成 → ギャップ分析完了（130件 + 横断8件 + 修正15件、`docs/vision/2026-06-11-gap-analysis.md`）。**TD-0 発見: main が tsc 17エラーでコンパイル不能（検証済み）**、.gitignore も Python 版に regression
- 現在: ギャップ分析のユーザーレビュー待ち（PR #12）。§6 の6論点（エネルギー vs スタミナ / 魔術師移行期 / 手記のフルリセット扱い / Difficulty 処遇 / 試験敵 / Depth 型）が要件定義の入力
- 次: §6 論点の確定 → 要件定義（Tier 1-3）→ 設計書更新（DOC-2 新戦闘コア仕様が最優先）→ Phase 0 負債返済（TD-0 から）

## 直近の完了

- PixiJS Phase 1 基盤実装 ✅（2026-05-23）— pixi.js@8.18.1 + @pixi/react@8.0.5 導入、`src/ui/pixi/` 配下に PixiStage/BattleCanvas/3レイヤー/EffectBridge/型 を新設、BattleScreen + GuildBattleScreen に `<BattleCanvas>` 挿入。Step0=A 案（React.lazy + mountedRef ガード）で StrictMode #602 構造的回避。particle-emitter 未導入・vite.config 無変更・受け入れ基準 9/9 構造充足。QA PASS-with-fixes（Blocker0 / 実害ある Major0）。実機検証 OK。計画書: `archive/2026-05-17-pixijs-phase1-code-level.md`
- バトルロジック脆弱性修正（V-CHAIN-01 / V-ENM-02 + 回帰テスト + ガイド正本化）✅（2026-05-17）— 調査で README が正・ガイド陳腐化を確定。実装が必要だったのは2件のみ。QA PASS-with-fixes（Blocker0）。**コミット済**（commit 60cc0f3, push 済）
- .claude ハーネス構造の life-editor 準拠リファクタ（Phase A-E）✅（2026-05-17）— git mv 標準化、MEMORY/HISTORY 新設、CLAUDE.md 再構成、固有エージェント3体作成。**コミット済**（commit 960aeea, push 済）

> 完了履歴の全量は `README.md` の Development History を参照。

## 予定

### バックログ機能（旧 TODO.md より移管）

| タスク                                               | 優先度 | 仕様                                                                   |
| ---------------------------------------------------- | ------ | ---------------------------------------------------------------------- |
| AoE Cards（全敵同時ダメージ）                        | Medium | —                                                                      |
| Quest System（デイリー/ウィークリー）                | Medium | `docs/vision/plans/quest_system.md`                                    |
| Enemy Image Assets（50体PNG, imagePath設定済）       | Low    | —                                                                      |
| Enemy Frame SVG Icons（絵文字→SVG）                  | Low    | —                                                                      |
| Title System（実績ベース称号）                       | Low    | `docs/vision/plans/title_system.md`                                    |
| NPC Conversation（ギルド酒場対話）                   | Low    | `docs/vision/plans/npc_conversation.md`                                |
| Dark Market Expansion（信頼度/隠れデメリット）       | Low    | `docs/vision/plans/dark_market.md`                                     |
| PixiJS Phase 2-4（バトル演出移行・アセット・最適化） | —      | `docs/vision/plans/pixijs_phase2_battle_effects.md` 他（Phase 1 完了） |

### 技術的負債・課題（`docs/code-explanation/vulnerability-remediation-guide.md` が SSOT）

**SSOT**: `docs/code-explanation/vulnerability-remediation-guide.md`（2026-05-17 正本化済: Phase 1-3 + Phase 4(7/7) + Phase 5 完了。冒頭カウント 35 fixed / 71 remaining）

1. ~~[解消済] ドキュメント不整合~~ ✅ 2026-05-17 — 調査で README が正・ガイド陳腐化と確定。ガイドを実態へ更新（Phase 4=7/7、Phase 5 全 ✅FIXED、証拠 file:line 併記）。V-EXEC/V-PHASE/V-DMG-MANAGE は全て修正済みだった
2. **[新規・要対応] V-CHAIN-02 相当（resonance debuff の 1-card-lag 非対称）**: V-CHAIN-01 で damage modifier 経路は play-aware 化したが、`getResonanceEffects`（burn/freeze/stun 等の敵付与）は依然プレイ前 state を読む。「ダメージは現在カードの共鳴を勘定するが、付与デバフは1枚遅れる」非対称が顕在化。修正案: `getResonanceEffectsForPlay(card)` を `useElementalChain` に追加し `onCardPlay` 後の仮想 state から導出（純粋関数追加のみ、副作用面積小）。詳細: `docs/known-issues/001-resonance-debuff-card-lag.md`
3. **[テスト負債]** バトルオーケストレーター / Context 系のフック統合テストが依然手薄（純粋関数は今回 elementalSystem/bleedDamage/phaseLogic/enemyAI で前進）。`docs/code-explanation/testing_analysis.md` 参照
4. **[再発防止・常時]** 頻出バグパターン: CSS クラス名衝突 / リソース state 二重化 / React 19 ref 参照（`docs/known-issues/LESSONS_LEARNED.md` 8 知見）。新規 Context/hook/battle 変更時は固有エージェント（`card-battle-state-invariant-checker` / `card-battle-battle-logic-validator`）を commit 前ゲートに使う。**注: 固有エージェントは Claude Code 再起動後に有効化される（本セッションでは general/role-\* で代替実施）**
5. **[新規・High] package.json の rollup ネイティブバイナリ・ハードコード除去** — `package.json` の `dependencies` に `@rollup/rollup-linux-arm64-gnu@^4.57.1` がハードコードされており既存の依存破壊（time bomb）。症状: (1) darwin で `npm install`（非 force）と `npm audit fix` が EBADPLATFORM 失敗、(2) `--force` 常用を強いられ peer/platform 不整合を握り潰す、(3) lockfile 不整合（rollup 本体 4.53.3 と衝突）。対処方針: 該当行削除 → `rm -rf node_modules package-lock.json && npm install`（--force なし）→ `npm audit fix`。これで dev/build 専用の脆弱性9件も大半が semver-major なしで解消見込み。**重要: PixiJS Phase 1 とは無関係な既存問題。Phase 1 のコミットには混ぜず、独立タスク・独立コミットで対応すること。** 出典: security-reviewer 監査（2026-05-19）。脆弱性9件は全て dev/build 専用・非 PixiJS 由来でリリースブロックはしないと判明済み
