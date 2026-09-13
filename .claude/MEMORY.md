# MEMORY.md - タスクトラッカー

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `HISTORY.md`、要約は `README.md` の Development History。

## 進行中

### 🔧 リアル性コンセプト v2 — 戦闘システム上流確定 + プロトタイプ計画（着手日: 2026-06-11）

**対象**: `.claude/docs/requirements/`（Tier1/2/3 正本確定）/ `.claude/docs/vision/concept-v2.md`（RE-APPROVED 2026-06-27）/ `src/ui/prototype/`（実装済・main マージ）
**計画書**: `.claude/docs/vision/plans/2026-06-28-battle-engine-bakeoff.md`（次ステップ・PLANNED・別セッション実装）/ `2026-06-27-battle-prototype-range-stamina.md`（プロト — 実装・マージ済 PR #14）

- 前回: 戦闘プロト（間合い×スタミナ検証台）を `src/ui/prototype/` に隔離実装→実機プレイで「ゲーム性はかなり面白い」と評価→PR #14 を origin/main へマージ（merge `9b88536`）。詳細は HISTORY 2026-06-27
- 前回: 要件正本化 + Bake-off Phase 0 prep 完了（Tier1/2/3 正本化・umbrella削除・rollup除去・phaser v4 導入）
- 現在: **Bake-off 実装・実機評価まで完了 → Unity フル移行へ方針転換**。共有コア + Pixi/Phaser 2アダプタを PR #16 で main マージ（merge `853226a`）。実機プレイで Phaser は好印象だが低解像度・ボタン重なり・全体にリアル感不足 → エンジン選定は Unity 移行で moot 化。検証済み戦闘コア（`src/ui/battle-lab/core/`）は C# 移植元・パリティ基準として main に保全
- 次: Unity 上で Tier1 本実装へ。剣気・崩し設計、カード・敵ロースター拡張、アート/Live2D は Phase 3 完了後の別プラン（`docs/vision/plans/2026-06-28-unity-migration-character-art.md`）。人手確認: Editor 前面で実プレイ（乱数「実戦」ボタン）

## 直近の完了

- Unity 移行 Phase 3 — UGUI 最小戦闘画面 + Web トレース一致 ✅（2026-09-06）— `BattleScreenView.Render` をコード生成 UGUI で実装（両者パネル・実距離 + 体勢の間合い・手札・ターン終了/リスタート・ログ・結果オーバーレイ・乱数切替）。固定 RNG(0) で fixture 操作列を再生しトレース 18/18 一致、SystemRng 3 戦完走、EditMode 57/57・parity 58/58。冒頭で NUnit 暗黙 using 起因の CS0246 ×57 を修正（known-issue 002）。計画書 `2026-06-28-unity-first-step-core-port.md` は archive へ。branch `feat/unity-foundation`
- Unity 以降のための作業土台（環境地固め・Logic/View 層・パリティ同期・Unity キット・Phase3 手順）✅（2026-07-06）— Phase 3 に先立ち Editor 抜きで用意できる土台を `unity-port/` に整備（branch `feat/unity-foundation`, commit `1769631`）。純 C# の `BattleStore`/`IBattleView` + ヘッドレステスト、TS↔C# ドリフト検出のワンコマンド化（`npm run parity:gen`/`parity:check`、`.gitattributes` で fixture を LF 固定）、Unity drop-in キット（asmdef/Unity gitignore/`BattleScreenView` 雛形）、`PHASE3-KICKOFF.md`（Windows 手順 + MCP 選定 IvanMurzak 第一候補/CoplayDev 代替）。**把握**: リモート/ローカル差異ゼロ・開発マシン=この Windows 11 に確定。**検証**: TS 204/204・build green、Workflow 敵対的レビューで C# 整合 CLEAN（dotnet 未導入で未コンパイル、導入後 `dotnet test` 58/58 想定）、minor 2 件修正済（parity-check の drift 基準を `git diff HEAD` に／docs の 50/50→58 統一）
- Unity 移行 First Step — 戦闘コア C# 移植 + パリティ証明 ✅（2026-07-05）— 計画書のPhase0（0a AI art生成/Live2D・0b Unity Editorスパイク）は画像生成・Live2D・Unity Editor操作の手段を持たないため自動実行不可と判断、ユーザー確認の上コア移植（子プランP0-P2相当）へ直行。`unity-port/`（netstandard2.1 単体ライブラリ）に `src/ui/battle-lab/core/` を無改変移植元として C# 移植（Types/Constants/Combat/Cards/Enemy/BattleReducer/ViewModel + IRng注入）。TS実装を固定RNGで実走させたゴールドデータでパリティテスト作成。dotnet test 50/50 green、role-qa 独立監査 PASS（Blocker0・数値/ログ文言完全一致確認済み）。**残課題**: Phase3（実際のUnityプロジェクト作成 + UGUI最小戦闘画面）はUnity Editor操作が必須のため別途人間作業が必要（予定へ記載）

> 完了履歴の全量は `README.md` の Development History を参照。

## 予定

### 次のアクティブタスク

- 🔜 **Unity 上で Tier1 本実装 — 次プラン策定** — Phase 3（最小戦闘画面）完了済（2026-09-06）。次はカード・敵ロースター拡張、剣気・崩し、アート/Live2D（`2026-06-28-unity-migration-character-art.md`）の順序と Unity リポ（sunbreak-pro/RPG-by-card、初回コミット未）の運用を決める。実プレイの手触り確認は人手（Editor 前面・乱数「実戦」）

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
5. **[新規・High] package.json の rollup ネイティブバイナリ・ハードコード除去** — `package.json` の `dependencies` に `@rollup/rollup-linux-arm64-gnu@^4.57.1` がハードコードされており既存の依存破壊（time bomb）。症状: (1) darwin で `npm install`（非 force）と `npm audit fix` が EBADPLATFORM 失敗、(2) `--force` 常用を強いられ peer/platform 不整合を握り潰す、(3) lockfile 不整合（rollup 本体 4.53.3 と衝突）。対処方針: 該当行削除 → `rm -rf node_modules package-lock.json && npm install`（--force なし）→ `npm audit fix`。これで dev/build 専用の脆弱性9件も大半が semver-major なしで解消見込み。**重要: PixiJS Phase 1 とは無関係な既存問題。Phase 1 のコミットには混ぜず、独立タスク・独立コミットで対応すること。** 出典: security-reviewer 監査（2026-05-19）。脆弱性9件は全て dev/build 専用・非 PixiJS 由来でリリースブロックはしないと判明済み。**bake-off Phase 0（2026-06-28 計画）で対処予定 — phaser 導入の前提**。**→ ✅ 2026-06-28 解消完了: feat `117e8b1` ＋ main cherry-pick `d709c8a`（build・test green・脆弱性0）。両ブランチで rollup ハードコード除去済**
