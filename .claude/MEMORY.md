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

- 三者評価を受けた 5 決定の反映（瘴気 / 手記 / HP 回復 / 遺産の残存 / セーブ前倒し / C# 正本化）✅（2026-09-12）— 衰弱 → **瘴気**（階層ごとの濃度で蓄積、防瘴の面・浄化の香・階層間休憩で和らげる）。図鑑 → **手記**（敵の頁 / ダンジョンの頁 / メモ。どこでも読めるが戦闘中は読むだけ。死亡地点に残り、遺産で選んだ頁だけ継ぐ）。HP は持ち越し、回復は戦闘中カード（応急処置 heal 型）/ 階層間休憩 30% / 探索イベント。痕跡は回収まで残り 1 つの生で 1 件。tier1 R1-16 セーブを Phase 4 の頭へ前倒し（死亡確定は即時保存）。**C# が正**、TS battle-lab は凍結、パリティ打ち切り（`unity-port/README.md` にバナー）。concept-v3 §13 に決定表、§12 の 2 / 20 / 21 を決定
- 戦闘コア v3 設計 + 単一ダンジョン化 + 施設設計書 archive + 三者評価 ✅（2026-09-12）— `battle_document/battle_core_v3.md` v1 を新設（R1-2 成果物: 持つ値 5 つ、間合い相性 1.0/0.5/0.15、スタミナ基礎 10・回復 近1/中2/遠3・投入 0〜3、構え、崩し = スタミナ削り、予兆コミット式 + 空振り回避、EnemyDef 決定木、C# 写像表）。concept-v3 §12 の 3/7/8(基礎)/16/18 を決定、§12-14 は「一つのダンジョンを深く潜る・ノード式継続」に決定、§12-20〜23 を追加。旧個別施設設計 4 本を `.claude/archive/camp_document/` へ。独立エージェント 3 体（デザイン / 実装 / 反対弁護人）で旧設計と比較評価: 3 体とも「方向は改善、完成度は旧を下回る（6 → 5）」。転記ミス（遺産の受け取り場所の不一致、「経済の記述なし」の言い過ぎ、バナー未付与 11 本）を修正。レポート `docs/reports/2026-09-12-concept-v3-evaluation.html`
- 企画書 v3（生と継承ループ）を設計ルールへ上書き + アーマー凍結 ✅（2026-09-12）— life-editor Note「ゲーム設計(chat GPT)」を正本 `docs/vision/concept-v3.md` に再編（§10 旧ルール対応表・§12 未確定 19 項目）。`vision/core.md` / `requirements/tier1〜3`（v6 / v3 / v2）/ `game_design_master.md` V4.0 / `CAMP_FACILITIES_DESIGN.md` V5.0 を全面改稿。旧ルール 14 ファイルに SUPERSEDED / FROZEN バナー。CLAUDE.md Game Loop 更新。**凍結**: アーマー（AP / 耐久 / 鍛冶屋）・経済（ショップ / サンクチュアリ）。**廃止**: ライフ制・帰還・手記の死越え。コード未変更（C# コアにアーマーは元々無い）。レポート `docs/reports/2026-09-12-concept-v3-overwrite.html`

> 完了履歴の全量は `README.md` の Development History を参照。

## 予定

### 次のアクティブタスク

- 🔜 **戦闘 UI / UX の完成度を 80% に上げる（設計・ビジュアル・アニメーション仕様。実装は先）** — 次セッションのプロンプトは `docs/prompts/2026-09-12-next-session-battle-uiux.md`。成果物: `battle_document/battle_ui_ux_v1.md` + モックアップ HTML + HTML レポート。R2-5 の内容を更新
- 🔜 **Unity 上で Tier1 v6 本実装 — 次プラン策定（C# が正）** — Phase 3（最小戦闘画面）完了済（2026-09-06）。`battle_core_v3.md` v1.1 §13 の写像表に従い Phase 1（R1-3 スタミナ投入量・HP 持ち越し / R1-4 間合い補正・予兆）から。TS battle-lab は凍結、`dotnet test` を正にする。残る未確定は concept-v3 §12 の 1 / 4 / 5 / 6 / 9 / 10 / 11 / 12 / 13 / 15 / 17 / 19 / 22 / 23。アート/Live2D（`2026-06-28-unity-migration-character-art.md`）と Unity リポ（sunbreak-pro/RPG-by-card、初回コミット未）の運用も決める。実プレイの手触り確認は人手（Editor 前面・乱数「実戦」）

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
