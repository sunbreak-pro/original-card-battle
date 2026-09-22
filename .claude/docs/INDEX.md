# Documentation Index

`.claude/docs/` 配下のドキュメント索引。標準ハーネス構造（`vision` / `requirements` / `known-issues` / `code-explanation`）とゲーム設計書（`*_document/`）が同居する。

## Update History

| Date       | Content                                                                                                                                                                                                                                                                                            |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-09-12 | 企画書「ゲーム設計(chat GPT)」で設計ルールを上書き。`vision/concept-v3.md` 新設（正本）、`requirements/tier1〜3` 全面改稿、`game_design_master.md` V4.0、`CAMP_FACILITIES_DESIGN.md` V5.0。旧ルール（ライフ制 / エクストラクション / 手記の死越え / AP）に SUPERSEDED・FROZEN バナー。アーマー凍結 |
| 2026-05-17 | life-editor 準拠リファクタ: `code_overview`→`code-explanation`、`feature_plans`→`vision/plans`、`memories`→`known-issues` へ移行。標準サブディレクトリ追加                                                                                                                                         |
| 2026-02-04 | Updated for facility consolidation (7 → 5). Removed deprecated files.                                                                                                                                                                                                                              |

> **どれが正本かは [`SOURCES.md`](./SOURCES.md) が持ちます**（主題 → 正本 → 持ち主のレーン、旧版の一覧、食い違ったときの手順）。本書はフォルダの案内です。

## 標準ハーネス構造

| Directory           | Description                                                                                                                                                                                |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `vision/`           | 抽象構想・設計原則（`core.md` + 正本 `concept-v3.md`「生と継承」。`concept-v2.md` は SUPERSEDED）。`vision/plans/` はアクティブ実装プラン + 将来機能（quest/title/NPC/dark market/PixiJS） |
| `requirements/`     | 機能要件定義（Tier 別）。`tier1-core.md` v6（生と継承ループの必須要件 R1-1〜R1-17）/ `tier2-support.md` v3 / `tier3-experimental.md` v2（実験・凍結・廃棄）                                |
| `known-issues/`     | Root Cause + 再発防止知見（`INDEX.md` で索引、`LESSONS_LEARNED.md` に 8 知見）                                                                                                             |
| `code-explanation/` | コード解析・脆弱性トラッカー（`vulnerability-remediation-guide.md`）・テスト分析                                                                                                           |

## ゲーム設計書（`*_document/`）

| Directory           | Description                                                        |
| ------------------- | ------------------------------------------------------------------ |
| `Overall_document/` | High-level game design and architecture                            |
| `battle_document/`  | Battle system mechanics, phases, damage calculation                |
| `camp_document/`    | Camp facilities (Shop, Guild, Sanctuary, Blacksmith, Dungeon Gate) |
| `card_document/`    | Card system, mastery, derivation mechanics                         |
| `danjeon_document/` | Dungeon exploration, map generation, nodes                         |
| `enemy_document/`   | Enemy definitions, AI patterns, boss design                        |
| `item_document/`    | Items, equipment, consumables                                      |
| `journal_document/` | Journal system (header UI for deck/encyclopedia/settings)          |
| `util_doument/`     | Utility systems and helpers                                        |

## Key Documents

### Project Overview (Snapshot)

- `Overall_document/PROJECT_OVERVIEW.md` - 最上流（構想/差別化/成長設計）+ 現状ステータス + 設計書vs実装の差分・未完了・ドキュメント不整合の横断スナップショット（2026-06-07）

### Battle System

- `battle_document/battle_core_v4.md` - **数値の正本（2026-09-13 起票、v4.2 は §16〜§18、v4.3 は §21 の間合い N）**: 属性 5 つ × 列 1〜4 の固定コスト（投入量は廃止）、特性（属性コンボ）、状態 10 語 + ボス専用 2（スタック制、プレイヤーは種類 6）、スタンス枠 1、手札 5 枚 / 全捨て、デッキ 20〜40 / 同種 3、個体ごとの近間 / 遠間（位置を持つのはボスと精鋭だけ）、連戦モード、試験台の基準 9 項目。カードは `card_document/swordsman_cards_v4.md`、敵は `enemy_document/enemy_roster_v4.md`。実装は `vision/plans/2026-09-13-battle-v4-implementation.md`
- `battle_document/battle_core_v3.md` - 数値の旧正本（2026-09-12）: 間合い × スタミナ投入。投入量と共有の距離（近 / 中 / 遠）は v4.2 で廃止（`battle_core_v4.md` §16〜§18）。予兆・崩し・構えは継続。旧 `battle_logic.md` / `buff_debuff_system.md` / `element_system_spec.md` は Web 版の記録
- `battle_document/battle_ui_ux_v2.md` - **戦闘 UI / UX の正本（v2.1、2026-09-21、core v4.2 対応）**: 情報設計（角の札 / 予兆の一字 / 立ち位置と一字札 / 語の予算 10 字 / 状態チップ / スタンス枠 / 2 体戦 / 精鋭の予兆 2 段 / 連戦の進捗）、ドラッグ主操作（受け皿 = 単体の相手、投げ上げ線 = 自分向き。予測値は 1 つ）、演出の文法（系統 8 × 面の確定値の 4 段 × 固有の飾り）、Unity 写像（戦闘描写の試作 `Assets/View/Depiction/` のプレハブ 7 種と台本の型）とアセット一覧。§10 / §11 は決定の記録。画面の見本は Unity の試作（`docs/reports/2026-09-19-unity-battle-depiction.html`）
- `battle_document/battle_ui_ux_v1.md` - 戦闘 UI / UX の旧正本（2026-09-12、v1.2、core v3 前提。View v1.1 の実装記録）: 情報設計 20 項目・L1 / L2・2 操作のプレイ・見た目 3 案と A 採用・演出 10 節・Unity 写像。モックアップは `docs/mockups/2026-09-12-battle-uiux-mockup.html`

### Card System

- `card_document/` - Card types, mastery progression, derivation unlocks

### Camp Facilities (5 Facilities)

- `camp_document/camp_facilities_design.md` - Master design (V4.0)
- 旧個別施設設計（guild / shop / blacksmith / sanctuary）は `.claude/archive/camp_document/` へ移動（2026-09-12、凍結）

### Journal System (Header UI)

- `journal_document/journal_system_implementation_plan.md` - Deck building, encyclopedia, settings

### Equipment

- `ap-equipment-system.md` - AP (Armor Point) system, equipment durability — **FROZEN（2026-09-12）**。防御は Guard のみ

## Related References

- **`.claude/CLAUDE.md`** - 現状の実装規約・アーキテクチャ・規約の SSOT
- **`.claude/memory/chat-<self>.md`** - タスクトラッカー（進行中 / 直近完了 / 予定。チャットごと）
- **`.claude/history/chat-<self>.md`** - セッション単位の変更履歴（降順。チャットごと）
- **`README.md`** - プロジェクト概要・Development History（完了履歴の要約）
- **`.claude/docs/vision/core.md`** - Vision・設計原則
