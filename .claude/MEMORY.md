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

- 戦闘 UI / UX v2（core v4.1 対応）✅（2026-09-14）— `battle_document/battle_ui_ux_v2.md` を新設（情報設計 26 項目、L1 v2「背骨とレーン」を 3 案の採点で採用、ドラッグ主操作、演出 21 節と時間予算、Unity 写像は UiTween 継続、ルールへの要望 25 件）。モックアップ `docs/mockups/2026-09-13-battle-uiux-v4-mockup.html`（6 画面、Artifact あり）、レポート `docs/reports/2026-09-13-battle-uiux-v4-80.html`。R2-5 を受け入れ基準 16 項目に。実画面の目視で予測札が矢印の先を隠す問題などを見つけて直した。**判断待ち**: 対案 A の実測、要望 5 件（2 段予兆 / 重撃 / 威圧 / 呪縛 / ダメージ式）、正本の誤記 2 件、演出上限の緩和 3 か所
- 戦闘 100% の棚卸 ✅（2026-09-13）— こうだいさんの 7 条件（5 枚ドロー / タイプ 4 種と二面 / 同種 3 枚 / 80 種のうち 40 種は習得 / カード感の操作 / カードごとのエフェクト）を core v3 に突き合わせ、8 領域 58 項目の棚卸表と現在地（約 20%）、80% までの 6 段階を `docs/reports/2026-09-13-battle-100-inventory.html` にまとめた。叩き台: 状態 10 語、スタンス枠 1、T0 は威力 0、移動は属性、初期 40 / 習得 40 の配分、敵 19 体（80% は 9 体）、試験台の基準 9 項目、ドラッグの高さで投入量。**判断待ち**: 7 点（移動を属性 / 投入はドラッグ高さ / T0 威力 0 / スタンス枠 1 / 状態 10 語固定 / 敵 9 体 / v4 着手）
- 戦闘 UI v1.1 + 戦闘コア v3 + UGUI View の Unity 実装 ✅（2026-09-12）— 見た目 **A 採用**（A+ トークンと描き込み、敗北画面、手記ドロワーを設計書 v1.1 とモックアップへ）。`unity-port/BattleCore/` を battle_core_v3 に全面改修（投入量 / 予兆 / 敵 Guard / 構え / 崩し / 瘴気 / `BattleInit` / `BattleEvent`）、`unity-project-kit/Assets/View/` 11 ファイル。`dotnet test` 54 / 54、Unity EditMode 54 / 54、Windows ビルドの実画面で配置を確認。残り: Editor 前面での手触り確認、本物の立ち絵 / 書体 / 効果音

> 完了履歴の全量は `README.md` の Development History を参照。

## 予定

### 次のアクティブタスク

- 🔜 **戦闘 v4 の C# 実装 + 試験台 + 連戦モード（次セッション）** — 設計は 2026-09-13 に確定（`battle_core_v4.md` / `swordsman_cards_v4.md` / `enemy_roster_v4.md`）。順 3〜4（ドラッグと HUD、演出の文法）の UI 正本は `battle_ui_ux_v2.md`（2026-09-14）。プランと冒頭プロンプトは `vision/plans/2026-09-13-battle-v4-implementation.md`。完了条件: dotnet test 緑 / `BattleCore.Sim` が基準 9 項目を出す / 初期 40 種と敵 9 体がデータに入る / 連戦 3 戦が Windows ビルドで通る / View v1.1 が壊れない。ブランチ `feat/battle-core-v4`。その後は順 3 ドラッグ操作と HUD → 順 4 演出の文法 → 順 5 習得 20 種 + ボス適応 → 順 6 プレイテスト 3 巡で 80%。目盛りは T0 = 5（2026-09-13 決定、全体 1.6 倍、HP 50）。特性は条件 12 × 効果 10、初期 32 + 習得 40 に付与
- 🔜 **Unity 上で Tier1 v6 本実装 — 次プラン策定（C# が正）** — Phase 3（最小戦闘画面）完了済（2026-09-06）。**戦闘コア v3（R1-3 / R1-4 相当）と戦闘画面 v1.1 は 2026-09-12 に実装済**（`unity-port/`、`dotnet test` 54）。次は探索側（刻限 / 瘴気 / ノード）と `BattleInit` の接続、手記の実データ、開示度（R2-3）、ボスの 2 段階予兆。TS battle-lab は凍結、`dotnet test` を正にする。残る未確定は concept-v3 §12 の 1 / 4 / 5 / 6 / 9 / 10 / 11 / 12 / 13 / 15 / 17 / 19 / 22 / 23。アート/Live2D（`2026-06-28-unity-migration-character-art.md`）と Unity リポ（sunbreak-pro/RPG-by-card、初回コミット未）の運用も決める。実プレイの手触り確認は人手（Editor 前面・乱数「実戦」）

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
