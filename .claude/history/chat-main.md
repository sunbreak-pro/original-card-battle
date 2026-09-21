# HISTORY (chat-main)

### 2026-09-20 - 戦闘シーン本番化の地ならし（正本 v4.2 改訂 / Issue 17 件 / 実装計画書）

#### 概要

戦闘の設計書は「本文が古く、後ろの節に新しい決定が積まれている」形で止まっていた。決定を本文へ入れ込んで正本を v4.2 にし、本番の戦闘シーンに要る作業を Issue 17 件に分け、実装計画書を置いた。コードは 1 行も変えていない。

#### 変更点

- **`battle_document/battle_core_v4.md`**: §0〜§14 を全面改訂。投入量 → 1 枚ごとの固定コスト、T0〜T3 → 列 1〜4（単属性アタック 6 / 13 / 21 / 30）、共有の距離 → 個体ごとの近間 / 遠間、状態 → スタック制（プレイヤーは種類 6）、スタミナ回復 → 毎ターン 3 固定、デッキ → 20〜40 枚、強化 → ×1.5。`moveFirst` / `shift` / 空振り回避 / 最適間合いの列を廃止し、`push` を敵だけの項目にした。§13 の基準を 9 → 12 項目。§16〜§18 は決定の記録へ降格し、本文が正であることを各節に明記
- **`card_document/swordsman_cards_v4.md`**: 80 枚にコストを割り当て（1 / 2 / 3 = 25 / 30 / 25 枚、列 4 は初期値 0 枚）。位置を条件にする特性を 6 → 16 枚（攻撃 / 防御 × 近間 / 遠間 の 4 マス × 4）。`min` 列と間合い列を廃止。ムーブ面 22 枚を「近間へ / 遠間へ / 反転」に。#80 背水の型を背水の陣へ置換
- **`enemy_document/enemy_roster_v4.md`**: 位置を持つ敵をボスと精鋭 5 体に限定し、通常 6 体と取り巻きから外した。行動表 58 行を列 = コストへ。決定木を 2 枝（位置なし）と 4 枝（位置あり）の 2 形に。通常敵 4 体の位置条件の特性を振り替え、精鋭・ボス 3 体に位置条件を追加。深淵の釣り人と歪みの根を 2 値で再設計。敵の回復値（通常 2 / 精鋭 3 / ボス 4）と敵の目盛り（プレイヤーの約 6 割）を新設
- **周辺 10 文書**: `game_design_master.md` / `tier1-core.md` / `tier2-support.md` / `CAMP_FACILITIES_DESIGN.md` / `INDEX.md` / `art_document/briefs/polearm_warped.md` を追従させ、履歴文書 4 件（`battle_core_v3.md` / `battle_ui_ux_v1.md` / `battle_logic.md` / `buff_debuff_system.md`）に SUPERSEDED 注記を置いた
- **Issue**: #46〜#62 の 17 件を `lane:battle` で起票。依存順に 4 つの束（コアの骨 #47〜#50 / データと検証 #51〜#53 / 画面 #46・#54〜#57 / 繰り返す理由 #58・#59・#62）と人手 2 件（#60 #61）。#53 に「ターン数が目安を下回る見込み」と「敵の回復値は仮置き」をコメントで残した
- **計画書**: `.claude/docs/vision/plans/2026-09-20-battle-scene-production.md` を新設。着手の前提（決まっていること / 止まる未確定 / 環境 / いま赤いもの / 人手の工程）、Scope、Steps、Files、Verification、冒頭に貼るプロンプトを持つ。旧プラン `2026-09-13-battle-v4-implementation.md`（ON HOLD）は本プランが引き継ぐ
- **検証**: `cd unity-port && dotnet test` 54 件 緑。`npm run build` は #44 のとおり赤（`parityFixture.test.ts` の `@types/node` 不足で 4 エラー。本作業とは無関係で、docs のみの変更では動かない）
- **レポート**: `docs/reports/2026-09-20-battle-scene-production-prep.html`（Artifact `https://claude.ai/artifact/2opwQcHTqfW4mkH7E7C283`）
- **未コミット**: 14 文書 + 計画書 + レポートを作業ツリーに置いたまま。ブランチは `docs/boss-roster-and-extra-boss` で、concept-v3 §16（襲名）と Krita 引き継ぎ書の前セッション分も未コミットのまま残っている
