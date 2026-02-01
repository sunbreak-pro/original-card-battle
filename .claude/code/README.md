# Code Structure Analysis

コードベース全体の構造・ロジックフロー・脆弱箇所を体系的にまとめたドキュメント群。

## Index

### Battle System
- [battle/orchestration.md](battle/orchestration.md) — オーケストレーター構造、フェーズフロー、初期化 ✅
- [battle/damage-and-buff.md](battle/damage-and-buff.md) — ダメージ計算式、Guard/AP/HP配分、バフ/デバフライフサイクル ✅
- [battle/card-execution.md](battle/card-execution.md) — カード実行フロー、マルチヒット、エネルギー消費 ✅
- [battle/class-abilities.md](battle/class-abilities.md) — 剣気、共鳴、召喚の各システム ✅

### State Management
- [state/context-system.md](state/context-system.md) — 5つのContext、状態形状、相互依存、Save/Load ✅

### Characters
- character/player.md — プレイヤー初期化、クラス別ステータス、成長 (PENDING)
- character/enemy.md — 敵AI、行動決定、Depth別定義 (PENDING)

### Cards
- cards/deck-and-mastery.md — デッキ構築、ドロー、マスタリー (PENDING)

### Inventory
- inventory/equipment-and-items.md — 装備スロット、AP計算、耐久度 (PENDING)

### Resource
- [resource/economy.md](resource/economy.md) — Gold二重プール、魔石、Shop/Blacksmith/Sanctuary経済 ✅

### Dungeon
- dungeon/dungeon-system.md — マップ生成、ノードイベント、Depth管理 (PENDING)

### AI Reference
- ai-reference/ARCHITECTURE.md — 全体依存関係図、脆弱箇所一覧 (PENDING)

## Conventions

Each document follows this structure:
1. **Overview** — 1-2 line summary
2. **File Map** — Related file paths with line counts
3. **Data Structures** — Key State/type shapes
4. **Logic Flow** — Processing flow with diagrams
5. **Dependencies** — Connections to other modules
6. **Vulnerability Analysis** — Issues tagged with:
   - `[BUG-RISK]` — Likely to cause bugs
   - `[EXTENSIBILITY]` — Blocks future extension
   - `[QUALITY]` — Maintenance/readability issues

## Progress

See [todos/code-analysis-sessions.md](../todos/code-analysis-sessions.md) for session tracking.
