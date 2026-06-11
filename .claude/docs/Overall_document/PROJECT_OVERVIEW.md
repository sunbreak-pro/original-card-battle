# プロジェクト総覧 — Original Card Battle（最上流資料 + 現状ステータス）

> **作成: 2026-06-07 / スナップショット文書。** 最上流の構想・ジャンル・差別化・成長設計を 1 枚に集約し、**設計書 vs 実装の差分・未完了タスク・ドキュメント不整合**を横断的に棚卸しする。
> 本ファイルは「ある時点の俯瞰スナップショット」であり、SSOT ではない。正本は以下:
> - 構想・設計原則 = `vision/core.md`
> - 実装規約・アーキ = `.claude/CLAUDE.md`
> - マスター設計 = `Overall_document/game_design_master.md` (V3.1)
> - タスク = `.claude/MEMORY.md` / 履歴 = `.claude/HISTORY.md`
> - 技術的負債 = `code-explanation/vulnerability-remediation-guide.md`

---

## 0. 30 秒サマリ

- **ジャンル**: Extraction Dungeon RPG × Card Battle（生還を最終目標とする計画的探索＋ローグライク成長）。
- **実装規模**: `src/` 約 **41,600 行 / 234 ファイル**（TS/TSX）。**主要システムはほぼ全て実装済み**（バトル・カード・ダンジョン・5 施設・セーブ・2 クラス・5 深度の敵）。
- **未完了の核**: ライフ制 **Phase 4-5**（テレポート石統一・アビス脱出ルート・ゲームオーバー全リセット）、**PixiJS Phase 2-4**、脆弱性 **71 件残**、テストは中核ロジックのみ。
- **最大のドキュメント負債**: `README.md` が**旧 Python 版のまま**（現行 React/TS 版と無関係）。

---

## 1. 最上流：構想・ジャンル・差別化

### 1.1 1 行コンセプトと Core Value（`vision/core.md`）

> オクトパストラベラー / Slay the Spire 系のターン制カードバトル RPG。TypeScript + React の学習を兼ねた **個人開発（N=1）** 作品。ブラウザ単一プレイヤー向けローグライク。

| | Core Value |
|---|---|
| **V1 戦術的カードバトル** | フェーズキュー（速度順）+ クラス固有リソース（剣気 / 元素共鳴）による読み合い |
| **V2 ローグライク進行** | 5 深度 × 5 フロアの手続き生成ダンジョン。死亡でランリソース喪失・ソウルは保存 |
| **V3 ラン間成長** | ベースキャンプ 5 施設でデッキ・装備・スキルツリーを構築 |
| **V4 熟練度システム** | カード使用回数で派生カードを解放（masteryManager） |

### 1.2 ジャンル定義（`game_design_master.md` V3.1 §1）

> **「Extraction Dungeon RPG × Card Battle」** — 計画的探索と成長、最終目標は「生還」。
> V2.0 でローグライト要素を撤廃し **エクストラクション型ダンジョン RPG** へピボット、V3.0 で **ライフ制** 導入。

### 1.3 他ゲームとの差別化（参照タイトルと固有要素）

**参照タイトル（差別化の軸）:**

| 参照 | 借用した軸 |
|---|---|
| **Slay the Spire** | カードバトル / デッキ構築 |
| **風来のシレン（不思議のダンジョン系）** | エクストラクション（持ち込み / 持ち帰りのリスク） |
| **Dark Souls** | 死亡ペナルティの重さ・緊張感 |

**StS 系カードゲームに対する固有の差別化:**

1. **エクストラクション型リスク/リワード** — 死亡で装備・アイテム・Gold を全ロスト。ただし **ソウル（経験値）は死んでも 100% 獲得**（V3.0 変更）。「完全な無駄死には存在しない」設計で、成長は止めず緊張感だけを最大化。
2. **ライフ制（リトライ制限）** — Hard:2 / Normal・Easy:3、回復手段なし、0 で死亡＝ゲームオーバー（ハードリセット）。無限試行を封じ、一回一回の死に重みを与える。
3. **「持ち込み装備のジレンマ」** — 高レベル装備＝高リスク高リターン。撤退タイミングの読み合い。
4. **クラス固有リソースの読み合い** — 剣士＝剣気 / 魔術師＝元素共鳴。速度順フェーズキューで行動順を駆け引き。
5. **熟練度 → 派生カード** — カード使用回数で進化・派生（StS の永続アップグレードより継続的）。
6. **ベースキャンプのラン間メタ進行** — 5 施設（ショップ / ギルド / 鍛冶屋 / サンクチュアリ / ダンジョンゲート）＋ Journal。

### 1.4 成長の 3 本柱（master §3.1）

| 柱 | 永続性 | 死亡時 |
|---|---|---|
| **① 装備成長**（Lv0-3 / 品質 / レアリティ） | ラン内 + 恒久 | **喪失**（持ち込み分も含め全ロスト） |
| **② カード成長**（熟練度 Lv1-5 / 進化） | 恒久 | 保持（図鑑・デッキ構成は失われない） |
| **③ ソウル（経験値）** | 恒久 | **100% 獲得**（サンクチュアリでスキルツリー解放） |

### 1.5 Non-Goals（`vision/core.md`）

マルチプレイヤー / オンライン同期 / 課金・サーバーサイド / 3 クラス目以降（剣士・魔術師の 2 クラス確定、召喚士は削除済）/ モバイルネイティブ化。

### 1.6 設計原則（`vision/core.md`）

データとロジックの分離 / 状態の単一所有 / 不可侵コード（`deck.ts`・`deckReducer.ts`）/ React 19 パターン厳守 / CSS スコープ必須 / 設計書駆動。

---

## 2. 実装計画の階層（粒度マップ）

計画は 4 層構造。**ただし「要件層」が運用上欠落**（§6 参照）。

```
Vision（構想）            vision/core.md ─────────────── 不変の North Star
   ↓
マスター設計（戦略）       game_design_master.md §8 ──── MVP / Phase 1 / 2 / 3（箇条書き粒度）
   ↓
〔要件層〕               docs/requirements/ ─────────── ❌ 未作成（規約のみ存在）
   ↓
機能プラン（中粒度）       vision/plans/ ──────────────── PixiJS Phase1-4（詳細・各13-17KB）
                                                       quest/title/npc/dark_market（構想メモ・各1-2KB）
   ↓
タスク（細粒度）          MEMORY.md（優先度付きバックログ）
                         vulnerability-remediation-guide.md（Tier別 個別脆弱性）
```

**master §8 のロードマップ:**

- **Phase 1 (MVP)**: ベースキャンプ基本 / Depth 1-3 / 生還・死亡 / ライフ制 / ソウル XP / 基本スキルツリー → **概ね達成**
- **Phase 2 (拡張)**: 鍛冶屋品質ガチャ / 図鑑・デッキ構築 / 熟練度 / スキルツリー拡張 / Depth 4-5 / アビス脱出ルート → **大半達成（脱出ルートのみ未完）**
- **Phase 3 (完成)**: 難易度・ライフ調整 / 演出・アニメ（= PixiJS）/ UI/UX 最適化 / エンディング → **進行中（PixiJS Phase1 のみ・エンディング未確認）**

---

## 3. 現状ステータス（実装実態）

### 3.1 メトリクス（2026-06-07 計測）

| 指標 | 値 |
|---|---|
| `src/` 総行数 | 約 41,606 行 |
| TS/TSX ファイル数 | 234 |
| ディレクトリ数 | 93 |
| テストファイル | 8（`__tests__` 6 ディレクトリ） |
| 最大ファイル | `EquipmentData.ts`(1,988) / `swordsmanCards.ts`(1,054) / `useBattleOrchestrator.ts`(1,006) |

### 3.2 システム別 完成度

| システム | 完成度 | 実態 |
|---|---|---|
| バトル（orchestrator + 8 サブフック） | ✅ 100% | `useBattleOrchestrator`(1,006行) + 全サブフック実装 |
| カード（2 クラス） | ✅ ~100% | **剣士 45 / 魔術師 40** のカード定義（id 基準）+ 派生/才能レジストリ |
| ダンジョン | ✅ 100% | 5 深度 × 行ベース手続き生成、6 ノード種（battle/elite/boss/event/rest/treasure） |
| キャンプ 5 施設 + Storage | ✅ 100% | Guild/Shop/Blacksmith/Sanctuary/DungeonGate すべて専用 UI ツリーで実装 |
| 敵 | ✅ 100% | depth1-5 の 5 ファイル（深度 20 体 ≒ 計 100 体、Depth5 ボス CHRONOS_GUARDIAN 含む） |
| Context（9 プロバイダ） | ✅ 100% | 全 9 が `src/contexts/` に集約 |
| セーブ/ロード | ✅ 機能 | `saveManager.ts` localStorage + バージョン管理（migration はスタブ） |
| PixiJS | ⏳ ~Phase1 | `src/ui/pixi/` 11 ファイル、Phase 1 基盤のみ。Phase 2-4 未着手 |
| テスト | ⚠️ 部分 | 中核ロジック（damage/buff/phase/AI/element/deck/pixi）は有。UI・施設・ダンジョン・inventory・save は無 |

> 補足: `code-explanation/overall-summary.md` は一部陳腐化（contexts を「3 + DungeonRun in ui」と記載、カードを「81」と記載）。**現行の実態は本 §3 を優先**（9 contexts は `src/contexts/` 集約済、カードは剣士45/魔術師40）。

---

## 4. 設計書 vs 実装の差分（未完了 / 負債）

### 4.1 機能ギャップ（設計書にあるが未完）

| 項目 | 設計書 | 実装実態 | 根拠 |
|---|---|---|---|
| **テレポート石** | ライフ制の中核（統一テレポート石） | **未実装**（`teleportStoneCount = 0` のプレースホルダ + TODO） | `ExplorationScreen.tsx:41-45` / DESIGN_CHANGE Phase 4 |
| **アビス脱出ルート** | Depth5 ボス撃破後の脱出＝ゲームクリア条件 | 未確認（Phase 4-5 未完） | DESIGN_CHANGE_PLAN「Pending」 |
| **ゲームオーバー全リセット** | ライフ 0 で Hard Reset 処理 | 未完（Phase 5） | DESIGN_CHANGE_PLAN「Pending」 |
| **装備耐久度（AP）劣化** | 戦闘で AP 消耗 → 修理が施設価値 | 型・ステ計算はあるが**戦闘中の劣化なし** | overall-summary「Partial」 |
| **称号 / 実績の接続** | 実績ベース称号（Title System） | `AchievementList` UI はあるが解放日時未取得 TODO、`cardTypeCount` 未追跡 | `AchievementList.tsx:186` |

> ⚠️ **逆方向の差分**: 剣士カードは実装 **45** 枚で設計書 `SWORDSMAN_CARDS_40`（40 枚想定）を**超過**。設計書側か実装側どちらに寄せるか要判断（設計書駆動原則）。魔術師は 40 で一致。

### 4.2 stub / TODO（コード内マーカー）

- `ExplorationScreen.tsx:45` — テレポート石使用ロジック未実装（上記）。
- `AchievementList.tsx:186` — 実績解放日時のプレースホルダ。
- セーブ `migrate()` がスタブ — フォーマット変更でセーブ破損リスク。
- デイリーショップのローテーション永続化が未確認（`dayCount` 追跡なし）。

### 4.3 技術的負債（SSOT: `vulnerability-remediation-guide.md`）

| 負債 | 状態 |
|---|---|
| 脆弱性トラッカー | **35 fixed / 71 remaining**（Tier 0-3） |
| known-issue **001**（resonance debuff の 1-card-lag 非対称） | **Open**。修正案あり（`getResonanceEffectsForPlay`） |
| **rollup ネイティブバイナリのハードコード**（time bomb / High） | `package.json` から除去すべき独立タスク。darwin で `npm install` 失敗の原因 |
| バトル/Context のフック統合テスト不足 | 純粋関数は前進、統合は手薄（`testing_analysis.md`） |
| 巨大ファイル | `useBattleOrchestrator`(1,006) / `InventoryContext`(799) / `PlayerContext`(680) は単一責務肥大 |

---

## 5. 未完了タスク一覧（横断・優先度付き）

`MEMORY.md` の「進行中」は **なし**。バックログ + 横断調査で判明した未完を統合:

| # | タスク | 優先度 | 出典 / 備考 |
|---|---|---|---|
| 1 | rollup ハードコード除去 | **High** | MEMORY #5。独立コミット必須 |
| 2 | ライフ制 Phase 4-5（テレポート石 / 脱出ルート / 全リセット） | **High** | DESIGN_CHANGE_PLAN。ゲーム成立条件に直結 |
| 3 | known-issue 001（resonance 1-card-lag） | Medium | 修正案あり |
| 4 | AoE Cards（全敵同時ダメージ） | Medium | MEMORY |
| 5 | Quest System | Medium（要再評価） | ⚠️ MEMORY は「予定」だが **UI(`QuestsTab`)・GuildContext は実装済**。残作業の切り分けが必要 |
| 6 | PixiJS Phase 2-4（演出移行・アセット・最適化） | — | Phase 1 完了 |
| 7 | テスト拡充（統合 / UI / save） | Medium | `testing_analysis.md` |
| 8 | Title System / 統計追跡 | Low | 実績 UI は部分実装 |
| 9 | NPC 会話 / Dark Market 拡張 | Low | ⚠️ Dark Market は基本タブ(`DarkMarketTab`)実装済、「拡張」のみ未 |
| 10 | 敵 PNG 50 体 / フレーム SVG アイコン | Low | imagePath 設定済 |
| 11 | 装備耐久度の戦闘中劣化 | Low-Med | 設計意図の未実現 |

---

## 6. ドキュメント不整合の棚卸し（要是正）

横断調査で見つかった資料側の問題。**実害の大きい順**:

| # | 不整合 | 深刻度 | 詳細 |
|---|---|---|---|
| 1 | **README.md が旧 Python 版** | 🔴 高 | 「Python 3.7 以上」「`python main.py`」と記載。現行 React/TS 版と完全に無関係。新規参加者・将来の自分が誤解する最大の負債 |
| 2 | **Python 残骸の同居** | 🟡 中 | ルートに `main.py` / `game/` が React アプリと併存。削除 or `legacy/` 退避が望ましい |
| 3 | **`docs/requirements/` 不在** | 🟡 中 | CLAUDE.md・INDEX.md・HISTORY が「用意した」と参照するが**実在しない**。要件層が運用されていない |
| 4 | **`feature_plans/` の重複** | 🟡 中 | PixiJS 4 ファイルが `feature_plans/` と `docs/vision/plans/` に**完全同一内容で重複**（phase2 diff = identical）。HISTORY 上は移行済のはずが旧ディレクトリに残存 |
| 5 | **脆弱性カウント不一致** | 🟡 中 | guide 冒頭「106 / 35 fixed / 71 remaining」 vs known-issues INDEX「106 / 約 21 修正 / 87 残」。SSOT を guide に一本化し INDEX を更新すべき |
| 6 | **MEMORY と実装の乖離** | 🟡 中 | Quest System・Dark Market が MEMORY「予定」のまま実装が先行。完了/残作業を task-tracker で再分類すべき |
| 7 | **参照タイトル不一致** | 🟢 低 | `vision/core.md`「オクトパストラベラー」 vs master「風来のシレン / Dark Souls」。差別化の核なので統一が望ましい |
| 8 | **master コアループ図に旧称 "Library"** | 🟢 低 | V3.1 で Journal へ統合済だが §2.1・§7 図に "Library" が残存 |
| 9 | **overall-summary.md の陳腐化** | 🟢 低 | contexts 構成・カード枚数が古い（§3.2 補足参照） |

---

## 7. 推奨アクション（優先度順）

1. **README.md の全面刷新**（#1）— 現行 React/TS 版の概要・起動手順（`npm run dev`）・アーキ要約へ。Python 残骸（#2）の扱いを併せて決定。
2. **ライフ制 Phase 4-5 の完遂**（タスク #2）— テレポート石・脱出ルート・全リセットはゲーム成立条件。`dungeon-system` / `battle-system` スキル。
3. **rollup ハードコード除去**（タスク #1）— 独立コミット。
4. **ドキュメント整合化の一括対応**（#3-#6）— `requirements/` 新設 or 規約から削除 / `feature_plans/` 重複削除 / 脆弱性カウント統一 / Quest・Dark Market を MEMORY で再分類。task-tracker 経由。
5. **設計書 vs 実装の数値寄せ**（§4.1）— 剣士カード 45 vs 設計 40 を `design-research` で確認し寄せる。

---

## 8. 参照

| 種別 | パス |
|---|---|
| 構想・原則 | `vision/core.md` |
| マスター設計 | `Overall_document/game_design_master.md`(V3.1) / `DESIGN_CHANGE_PLAN_lives_system.md` |
| 実装規約 | `.claude/CLAUDE.md` |
| 領域設計書 | `battle_document/` `camp_document/` `card_document/` `danjeon_document/` `enemy_document/` `item_document/` `journal_document/` |
| コード解析 | `code-explanation/`（overall-summary / vulnerability-remediation-guide / testing_analysis） |
| タスク・履歴 | `.claude/MEMORY.md` / `.claude/HISTORY.md` |
| 将来計画 | `vision/plans/`（PixiJS Phase1-4 / quest / title / npc / dark_market） |
| 既知の問題 | `known-issues/INDEX.md` / `LESSONS_LEARNED.md`（8 知見） |
