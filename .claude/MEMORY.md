# MEMORY.md - タスクトラッカー

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `HISTORY.md`、要約は `README.md` の Development History。

## 進行中

（なし）

## 直近の完了

- バトルロジック脆弱性修正（V-CHAIN-01 / V-ENM-02 + 回帰テスト + ガイド正本化）✅（2026-05-17）— 調査で README が正・ガイド陳腐化を確定。実装が必要だったのは2件のみ。QA PASS-with-fixes（Blocker0）。**未コミット**（main + .claude リファクタと分離が必要）
- .claude ハーネス構造の life-editor 準拠リファクタ（Phase A-E）✅（2026-05-17）— git mv 標準化、MEMORY/HISTORY 新設、CLAUDE.md 再構成、固有エージェント3体作成。**未コミット**
- Journal 戦術・記憶タブ改善 ✅（2026-02-07）— DeckTab 全4タグ常時表示、CardAddModal count+1、MemoriesPage 3分類

> 完了履歴の全量は `README.md` の Development History を参照。

## 予定

### バックログ機能（旧 TODO.md より移管）

| タスク                                         | 優先度 | 仕様                                               |
| ---------------------------------------------- | ------ | -------------------------------------------------- |
| AoE Cards（全敵同時ダメージ）                  | Medium | —                                                  |
| Quest System（デイリー/ウィークリー）          | Medium | `docs/vision/plans/quest_system.md`                |
| Enemy Image Assets（50体PNG, imagePath設定済） | Low    | —                                                  |
| Enemy Frame SVG Icons（絵文字→SVG）            | Low    | —                                                  |
| Title System（実績ベース称号）                 | Low    | `docs/vision/plans/title_system.md`                |
| NPC Conversation（ギルド酒場対話）             | Low    | `docs/vision/plans/npc_conversation.md`            |
| Dark Market Expansion（信頼度/隠れデメリット） | Low    | `docs/vision/plans/dark_market.md`                 |
| PixiJS 描画基盤（4 Phase 計画）                | —      | `docs/vision/plans/pixijs_phase1_foundation.md` 他 |

### 技術的負債・課題（`docs/code-explanation/vulnerability-remediation-guide.md` が SSOT）

**SSOT**: `docs/code-explanation/vulnerability-remediation-guide.md`（2026-05-17 正本化済: Phase 1-3 + Phase 4(7/7) + Phase 5 完了。冒頭カウント 35 fixed / 71 remaining）

1. ~~[解消済] ドキュメント不整合~~ ✅ 2026-05-17 — 調査で README が正・ガイド陳腐化と確定。ガイドを実態へ更新（Phase 4=7/7、Phase 5 全 ✅FIXED、証拠 file:line 併記）。V-EXEC/V-PHASE/V-DMG-MANAGE は全て修正済みだった
2. **[新規・要対応] V-CHAIN-02 相当（resonance debuff の 1-card-lag 非対称）**: V-CHAIN-01 で damage modifier 経路は play-aware 化したが、`getResonanceEffects`（burn/freeze/stun 等の敵付与）は依然プレイ前 state を読む。「ダメージは現在カードの共鳴を勘定するが、付与デバフは1枚遅れる」非対称が顕在化。修正案: `getResonanceEffectsForPlay(card)` を `useElementalChain` に追加し `onCardPlay` 後の仮想 state から導出（純粋関数追加のみ、副作用面積小）。詳細: `docs/known-issues/001-resonance-debuff-card-lag.md`
3. **[テスト負債]** バトルオーケストレーター / Context 系のフック統合テストが依然手薄（純粋関数は今回 elementalSystem/bleedDamage/phaseLogic/enemyAI で前進）。`docs/code-explanation/testing_analysis.md` 参照
4. **[再発防止・常時]** 頻出バグパターン: CSS クラス名衝突 / リソース state 二重化 / React 19 ref 参照（`docs/known-issues/LESSONS_LEARNED.md` 8 知見）。新規 Context/hook/battle 変更時は固有エージェント（`card-battle-state-invariant-checker` / `card-battle-battle-logic-validator`）を commit 前ゲートに使う。**注: 固有エージェントは Claude Code 再起動後に有効化される（本セッションでは general/role-\* で代替実施）**
