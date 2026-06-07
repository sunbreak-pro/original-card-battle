# Documentation Index

`.claude/docs/` 配下のドキュメント索引。標準ハーネス構造（`vision` / `requirements` / `known-issues` / `code-explanation`）とゲーム設計書（`*_document/`）が同居する。

## Update History

| Date       | Content                                                                                                                                                    |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-05-17 | life-editor 準拠リファクタ: `code_overview`→`code-explanation`、`feature_plans`→`vision/plans`、`memories`→`known-issues` へ移行。標準サブディレクトリ追加 |
| 2026-02-04 | Updated for facility consolidation (7 → 5). Removed deprecated files.                                                                                      |

## 標準ハーネス構造

| Directory           | Description                                                                                                              |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `vision/`           | 抽象構想・設計原則（`core.md`）。`vision/plans/` はアクティブ実装プラン + 将来機能（quest/title/NPC/dark market/PixiJS） |
| `requirements/`     | 機能要件定義（Tier 別。現状空、新機能追加時に使用）                                                                      |
| `known-issues/`     | Root Cause + 再発防止知見（`INDEX.md` で索引、`LESSONS_LEARNED.md` に 8 知見）                                           |
| `code-explanation/` | コード解析・脆弱性トラッカー（`vulnerability-remediation-guide.md`）・テスト分析                                         |

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

- `battle_document/` - Combat flow, phase execution, buff/debuff mechanics

### Card System

- `card_document/` - Card types, mastery progression, derivation unlocks

### Camp Facilities (5 Facilities)

- `camp_document/camp_facilities_design.md` - Master design (V4.0)
- `camp_document/guild_design.md` - Guild with integrated Storage tab (V3.0)
- `camp_document/shop_design.md` - Shop economy
- `camp_document/blacksmith_design.md` - Equipment enhancement
- `camp_document/sanctuary_design.md` - Soul remnant skill tree

### Journal System (Header UI)

- `journal_document/journal_system_implementation_plan.md` - Deck building, encyclopedia, settings

### Equipment

- `ap-equipment-system.md` - AP (Armor Point) system, equipment durability

## Related References

- **`.claude/CLAUDE.md`** - 現状の実装規約・アーキテクチャ・規約の SSOT
- **`.claude/MEMORY.md`** - タスクトラッカー（進行中 / 直近完了 / 予定）
- **`.claude/HISTORY.md`** - セッション単位の変更履歴（降順）
- **`README.md`** - プロジェクト概要・Development History（完了履歴の要約）
- **`.claude/docs/vision/core.md`** - Vision・設計原則
