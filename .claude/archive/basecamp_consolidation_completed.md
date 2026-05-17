# BaseCamp 統合リファクタリング 実装ガイド

## 変更履歴

| 日付       | バージョン | 内容           |
| ---------- | ---------- | -------------- |
| 2026-02-04 | v1.0       | 初版作成       |

---

## 1. 概要

### 1.1 目的

BaseCamp の施設数を 7 → 5 に削減し、過剰な施設・機能を整理する。

### 1.2 変更サマリー

```
【旧構成】7施設
  ダンジョンゲート / 取引所 / 鍛冶屋 / 聖域 / 図書館 / 酒場 / 倉庫

【新構成】5施設 + 手記（ヘッダー常駐UI）
  ダンジョンゲート / 取引所 / 鍛冶屋 / 聖域 / 酒場

【廃止】
  図書館 → 手記（Journal）に移行済み（別計画書で対応）
  倉庫   → 酒場（Guild）に吸収
```

### 1.3 前提条件

- 手記（Journal）システムは別計画書に基づき実装する。本ガイドのスコープ外。
- 本ガイドは **倉庫の酒場への統合** と **図書館・倉庫の施設としての廃止** を対象とする。
- 既存の戦闘システム、デッキ、セーブ等のロジックには一切触れない。

---

## 2. 新しい施設構成と責務

### 2.1 施設一覧

| 施設 | 英名 | 一言での役割 | リソースの流れ |
|------|------|-------------|---------------|
| ダンジョンゲート | Dungeon Gate | 深淵に潜る | — |
| 取引所 | Shop | Goldで物を買う | Gold → 装備・アイテム |
| 鍛冶屋 | Blacksmith | 魔石で装備を鍛える | 魔石 → 装備強化 |
| 聖域 | Sanctuary | 魂で自分を鍛える | 魂の残滓 → 恒久スキル |
| 酒場 | Guild | 人と会い、荷物を整理する | — |

### 2.2 各施設の詳細

#### ダンジョンゲート（変更なし）

- 深度選択・出発
- 準備画面（デッキ/装備/インベントリ確認）は実装済み
- 本リファクタリングでは変更しない

#### 取引所（Shop）（変更なし）

- 装備・消耗品の購入
- 不要品の売却
- 魔石→Gold換金
- 将来拡張：闇市（レア品・高リスク取引）

#### 鍛冶屋（Blacksmith）（変更なし）

- 装備レベルアップ（魔石消費）
- 品質上昇
- 修理（AP回復）
- 解体（魔石還元）

#### 聖域（Sanctuary）（変更なし）

- 魂の残滓によるスキルツリー解放
- 基礎ステータス強化
- 特殊能力解放

#### 酒場（Guild）（**倉庫機能を吸収**）

**現行の機能（維持）：**
- キャラクター選択
- 残機確認・状況サマリー
- 昇格試験

**倉庫から吸収する機能：**
- アイテム保管庫（Storage）の閲覧・操作
- インベントリ ↔ 倉庫間のアイテム移動
- 装備の着脱・管理

**将来の拡張枠（本リファクタリングでは実装しない）：**
- NPC会話・情報収集
- 称号の獲得・閲覧
- クエスト受注
- 社会的評価システム

---

## 3. 実装タスク

### 3.1 タスク一覧

| # | タスク | 優先度 | 影響範囲 |
|---|--------|--------|----------|
| 1 | 型定義の更新（FacilityType, GameScreen） | 高 | 型システム全体 |
| 2 | BaseCamp.tsx から図書館・倉庫カードを削除 | 高 | BaseCamp画面 |
| 3 | App.tsx のルーティングから library・storage を削除 | 高 | アプリ全体 |
| 4 | Guild コンポーネントに倉庫タブを追加 | 高 | Guild画面 |
| 5 | 不要ファイルの削除・整理 | 中 | ファイルシステム |
| 6 | 施設グリッドのレイアウト調整（7→5カード） | 中 | CSS |

---

### 3.2 タスク詳細

#### タスク 1: 型定義の更新

**対象ファイル:** `src/types/campTypes.ts`

**変更内容:**

```typescript
// 旧
export type FacilityType =
  | "guild"
  | "shop"
  | "blacksmith"
  | "sanctuary"
  | "library"    // 削除
  | "storage"    // 削除
  | "dungeon";

// 新
export type FacilityType =
  | "guild"
  | "shop"
  | "blacksmith"
  | "sanctuary"
  | "dungeon";
```

```typescript
// 旧
export type GameScreen =
  | "character_select"
  | "camp"
  | "battle"
  | "guild"
  | "shop"
  | "blacksmith"
  | "sanctuary"
  | "library"      // 削除
  | "storage"      // 削除
  | "dungeon"
  | "dungeon_map";

// 新
export type GameScreen =
  | "character_select"
  | "camp"
  | "battle"
  | "guild"
  | "shop"
  | "blacksmith"
  | "sanctuary"
  | "dungeon"
  | "dungeon_map";
```

**注意:**
- `library` や `storage` を参照している箇所がコンパイルエラーになるため、TypeScriptコンパイラで全てのエラーを検出してから次のタスクに進むこと。
- StorageState, InventoryState, MoveDirection 等の**型定義自体は削除しない**。これらは酒場内の倉庫タブで引き続き使用する。

---

#### タスク 2: BaseCamp.tsx の施設カード削除

**対象ファイル:** `src/ui/campsHtml/BaseCamp.tsx`

**変更内容:**

facilities 配列から `library` と `storage` のエントリを削除する。

```typescript
// 削除対象 1
{
  type: "library",
  name: "図書館",
  description: "Build your deck and browse the encyclopedia",
  icon: "📚",
  isUnlocked: true,
  onEnter: () => navigateTo("library"),
},

// 削除対象 2
{
  type: "storage",
  name: "倉庫",
  description: "Store and manage your items safely",
  icon: "📦",
  isUnlocked: true,
  onEnter: () => navigateTo("storage"),
},
```

変更後、facilities 配列には 5 つのエントリのみ残る:
1. dungeon（ダンジョンゲート）
2. shop（取引所）
3. blacksmith（鍛冶屋）
4. sanctuary（聖域）
5. guild（酒場）

**酒場の description を更新:**

```typescript
{
  type: "guild",
  name: "酒場",
  description: "Rest, manage your storage, and hear rumors",
  icon: "🍺",
  isUnlocked: true,
  onEnter: () => navigateTo("guild"),
},
```

---

#### タスク 3: App.tsx のルーティング削除

**対象ファイル:** `src/App.tsx`

**変更内容:**

1. import文から Library と Storage を削除:

```typescript
// 削除
import { Library } from "./ui/campsHtml/Library/Library.tsx";
import Storage from "./ui/campsHtml/Storage/Storage.tsx";
```

2. AppContent 内のルーティングを削除:

```tsx
// 削除
{currentScreen === "library" && <Library />}
{currentScreen === "storage" && <Storage />}
```

---

#### タスク 4: Guild に倉庫タブを統合

**対象ファイル:** `src/ui/campsHtml/Guild/Guild.tsx`（及び関連ファイル）

**方針:**

Guild コンポーネントにタブUIを導入し、既存機能と倉庫機能を切り替えられるようにする。

**タブ構成:**

```
酒場（Guild）
├── [本部]    — キャラ選択・残機確認・昇格試験（既存機能）
└── [倉庫]    — アイテム保管・インベントリ整理（Storageから移植）
```

**実装方針:**

- Guild の既存UIを「本部」タブとして維持する。内部構造の変更は最小限にする。
- Storage コンポーネントの**UIロジック**を Guild 内の「倉庫」タブとして再利用する。
- Storage コンポーネントが使用していた Context（InventoryContext 等）はそのまま利用する。
- Storage の独自スタイルは Guild 内のタブコンテンツとして適用されるよう調整する。

**イメージ（Guild.tsx の構造変更）:**

```tsx
// Guild.tsx 変更後の概要構造

type GuildTab = "headquarters" | "storage";

export const Guild = () => {
  const [activeTab, setActiveTab] = useState<GuildTab>("headquarters");

  return (
    <div className="guild">
      <FacilityHeader title="guild" variant="facility" />

      {/* タブ切り替え */}
      <div className="guild-tabs">
        <button
          className={activeTab === "headquarters" ? "active" : ""}
          onClick={() => setActiveTab("headquarters")}
        >
          本部
        </button>
        <button
          className={activeTab === "storage" ? "active" : ""}
          onClick={() => setActiveTab("storage")}
        >
          倉庫
        </button>
      </div>

      {/* タブコンテンツ */}
      {activeTab === "headquarters" && (
        <GuildHeadquarters />  {/* 既存のGuild UIをここに抽出 */}
      )}
      {activeTab === "storage" && (
        <GuildStorage />  {/* Storageの機能を移植 */}
      )}

      <button onClick={returnToCamp}>← キャンプに戻る</button>
    </div>
  );
};
```

**GuildStorage コンポーネント:**

既存の `Storage.tsx` の中身をベースに、以下の方針で作成する:

- ファイルパス: `src/ui/campsHtml/Guild/GuildStorage.tsx`
- `InventoryContext` を使用したアイテム移動ロジックはそのまま流用
- ヘッダーやナビゲーション部分は不要（Guild のタブ内に収まるため）
- CSS は Guild のスタイルに合わせて調整

**GuildHeadquarters コンポーネント:**

既存の Guild.tsx の内部コンテンツを抽出する:

- ファイルパス: `src/ui/campsHtml/Guild/GuildHeadquarters.tsx`
- キャラクター選択、残機確認、昇格試験の UI
- 既存の Guild 内部ロジックをそのまま移動

---

#### タスク 5: 不要ファイルの整理

**削除候補ファイル:**

```
src/ui/campsHtml/Library/          -- ディレクトリごと削除
  └── Library.tsx
  └── Library.css（存在する場合）

src/ui/campsHtml/Storage/          -- ディレクトリごと削除
  └── Storage.tsx
  └── Storage.css（存在する場合）
```

**削除前の確認事項:**

- Library 内に手記（Journal）で再利用するロジックがないか確認する。
  手記の実装計画書に移行先が明記されている機能は手記側で新規実装するため、Library コンポーネント自体は削除して問題ない。
- Storage 内のロジックは GuildStorage に移植が完了してから削除する。

**削除しないファイル:**

以下は他の場所で引き続き使用するため削除しない:

```
src/types/campTypes.ts
  → StorageState, InventoryState, MoveDirection, MoveResult,
    EquipmentSet, ItemFilter, ItemSortCriteria 等の型定義は維持

src/contexts/InventoryContext.tsx
  → 酒場の倉庫タブで使用するため維持

src/domain/camps/data/ 配下の図鑑データ
  → 手記の「記憶」ページで使用するため維持
    CardEncyclopediaData.ts
    EnemyEncyclopediaData.ts
    GameTipsData.ts
```

---

#### タスク 6: 施設グリッドのレイアウト調整

**対象ファイル:** `src/ui/css/camps/BaseCamp.css`

**変更内容:**

7施設→5施設になるため、`facilities-grid` のレイアウトを調整する。

**方針:**

- 5カードが均等に配置されるグリッドに変更
- 推奨レイアウト: 上段3枚 + 下段2枚（中央寄せ）、または5枚横並び
- 具体的なレイアウトは実装時に画面サイズとバランスを見て決定

```css
/* 変更例: 上段3 + 下段2 */
.facilities-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  /* 下段2枚が中央に来るよう justify で調整 */
  justify-items: center;
  gap: 1.5rem;
}
```

---

## 4. 実装順序

```
Phase 1: 型の整理（安全に壊す）
  1. campTypes.ts から library / storage を削除
  2. TypeScriptコンパイラでエラー箇所を全て検出
  3. エラー箇所を修正（ルーティング、import 等）

Phase 2: 施設カード・ルーティングの削除
  4. BaseCamp.tsx から図書館・倉庫カードを削除
  5. App.tsx からルーティングを削除
  6. import文の整理

Phase 3: 倉庫機能の酒場への統合
  7. GuildHeadquarters.tsx の作成（既存Guild UIの抽出）
  8. GuildStorage.tsx の作成（Storage UIの移植）
  9. Guild.tsx のタブUI実装
  10. スタイル調整

Phase 4: クリーンアップ
  11. Library/ ディレクトリの削除
  12. Storage/ ディレクトリの削除（移植完了後）
  13. グリッドレイアウトの調整
  14. 全体コンパイル確認・動作確認
```

---

## 5. 影響を受けるファイル一覧

### 5.1 変更するファイル

| ファイル | 変更内容 |
|----------|----------|
| `src/types/campTypes.ts` | FacilityType, GameScreen から library/storage 削除 |
| `src/ui/campsHtml/BaseCamp.tsx` | facilities配列から2施設削除、guild description更新 |
| `src/App.tsx` | Library/Storage の import とルーティング削除 |
| `src/ui/campsHtml/Guild/Guild.tsx` | タブUI導入、既存UIをHeadquartersに抽出 |
| `src/ui/css/camps/BaseCamp.css` | 5施設グリッドレイアウト調整 |
| `src/ui/css/camps/Guild.css`（存在する場合） | タブUIのスタイル追加 |

### 5.2 新規作成するファイル

| ファイル | 内容 |
|----------|------|
| `src/ui/campsHtml/Guild/GuildHeadquarters.tsx` | 既存Guild UIの抽出先 |
| `src/ui/campsHtml/Guild/GuildStorage.tsx` | 倉庫機能の移植先 |

### 5.3 削除するファイル

| ファイル | 理由 |
|----------|------|
| `src/ui/campsHtml/Library/` ディレクトリ | 手記に移行済み |
| `src/ui/campsHtml/Storage/` ディレクトリ | 酒場に統合済み |

### 5.4 変更しないファイル

| ファイル | 理由 |
|----------|------|
| `src/contexts/InventoryContext.tsx` | 酒場の倉庫タブで引き続き使用 |
| `src/contexts/GameStateContext.tsx` | GameScreen型は campTypes.ts から import しているため自動反映 |
| `src/types/campTypes.ts` 内のStorage系型定義 | 酒場の倉庫タブで引き続き使用 |
| `src/domain/camps/data/*EncyclopediaData.ts` | 手記の「記憶」ページで使用 |
| ダンジョン関連ファイル全て | スコープ外 |
| 戦闘関連ファイル全て | スコープ外 |

---

## 6. 検証チェックリスト

### 6.1 コンパイル確認

- [ ] `npx tsc --noEmit` がエラー 0 で通ること
- [ ] `library` `storage` の文字列リテラルが FacilityType / GameScreen に残っていないこと
- [ ] 削除したコンポーネントへの import が残っていないこと

### 6.2 画面遷移確認

- [ ] BaseCamp 画面に 5 施設のカードのみ表示されること
- [ ] 各施設カードをクリックして正しい画面に遷移すること
- [ ] 酒場画面でタブが切り替わること（本部 / 倉庫）
- [ ] 「倉庫」タブでアイテムの移動が動作すること
- [ ] 各画面から「キャンプに戻る」で BaseCamp に戻れること

### 6.3 機能確認

- [ ] 酒場 > 本部: キャラクター選択が動作すること
- [ ] 酒場 > 本部: 昇格試験が開始・完了できること
- [ ] 酒場 > 倉庫: Storage ↔ Inventory のアイテム移動が動作すること
- [ ] 酒場 > 倉庫: 装備の着脱が動作すること
- [ ] 取引所: 購入・売却が動作すること（変更なし確認）
- [ ] 鍛冶屋: 強化・修理・解体が動作すること（変更なし確認）
- [ ] 聖域: スキルツリーが動作すること（変更なし確認）
- [ ] ダンジョンゲート: 準備画面・出発が動作すること（変更なし確認）

### 6.4 レイアウト確認

- [ ] 5施設のグリッドが画面内に収まり、バランスが良いこと
- [ ] 酒場のタブUIが他の施設のUIトーンと統一されていること

---

## 7. 設計原則（実装時の判断基準）

1. **既存ロジックの再利用を最大化する。** Storage のロジックは新規実装せず移植する。
2. **型安全性を最優先する。** 型定義を先に変更し、コンパイラにエラー箇所を検出させる。
3. **変更しないファイルには触らない。** スコープ外の変更は行わない。
4. **施設の役割が一言で説明できること。** 機能が曖昧な施設を作らない。
5. **ダークファンタジーのトーンを維持する。** タブUIも酒場の世界観に合わせる。

---

## 8. 関連ドキュメント

| ドキュメント | 関係 |
|-------------|------|
| 手記（Journal）システム実装計画書 v1.0 | 図書館の機能移行先。本ガイドとは独立して実装。 |
| CAMP_FACILITIES_DESIGN.md | BaseCamp 設計の上位文書。本リファクタリング後に更新が必要。 |
| game_design_master.md | ゲーム全体設計。施設構成の記述を更新が必要。 |
| guild_implementation_guide.md | Guild の既存実装ガイド。タブ追加後に更新が必要。 |

---

## 9. 未決定事項

| 項目 | 内容 | 判断タイミング |
|------|------|---------------|
| 酒場タブのUI配置 | タブを上部に置くか、左サイドに置くか | タスク4実装時 |
| 5施設のグリッドレイアウト | 3+2配置か、5横並びか、他の配置か | タスク6実装時 |
| 施設カードの並び順 | ダンジョンを最上位に置くか、酒場を最上位に置くか | タスク2実装時 |
| 倉庫タブ内の装備セット機能 | 手記側に移すか、倉庫タブに残すか | 手記実装後に判断 |
