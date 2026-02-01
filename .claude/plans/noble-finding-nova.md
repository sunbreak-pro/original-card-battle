# ショップ在庫システム構築 + カードレアリティ削除

## 変更概要

1. **ショップ在庫システム**: 在庫数・売り切れ・リストック（戦闘回数ベース）・通知機能
2. **消耗品ラインナップ拡張**: 常設品と日替わり品の2層構造
3. **カードレアリティ完全削除**: `Rarity`型・全カードデータ・UI表示を撤去
4. **カード図鑑UI刷新**: カテゴリ/エレメント/コストによるフィルタ + 未取得カード非公開表示

---

## Session 1: ショップ在庫コアシステム

### 1-1. 在庫状態の型定義・永続化

**ファイル:** `src/types/campTypes.ts`, `src/types/saveTypes.ts`

新規型:
```typescript
interface ShopStockState {
  consumableStock: Record<string, number>;  // itemTypeId → remaining stock
  equipmentStock: Record<string, number>;   // "slot_rarity_idx" → remaining (0 or 1)
  lastRestockEncounterCount: number;        // restock時のencounterCount
  restockThreshold: number;                 // 次回restockまでの戦闘数 (7-10)
  dailySeed: number;                        // 日替わりアイテム用seed
}
```

SaveDataに`shopState: ShopStockState`を追加。

### 1-2. 在庫管理ロジック

**ファイル:** `src/domain/camps/logic/shopLogic.ts`

新規関数:
- `initializeShopStock()` — 初回or restock時に在庫を生成
- `decrementStock(stockState, itemKey)` — 購入時に在庫-1
- `isInStock(stockState, itemKey)` — 在庫確認
- `shouldRestock(encounterCount, lastRestockCount, threshold)` — リストック判定
- `performRestock(encounterCount)` — 在庫リセット+次回閾値設定(7-10のランダム)

### 1-3. 消耗品の在庫ティア設計

**常設品 (毎リストック補充):**

| アイテム | typeId | 在庫数 | 備考 |
|---------|--------|-------|------|
| 回復薬 | healing_potion | 5 | 基本回復 |
| 上級回復薬 | greater_healing_potion | 3 | 中級回復 |
| 完全回復薬 | full_elixir | 1 | 高級回復 |
| 修理キット | repair_kit | 5 | 装備修繕用 |
| テレポストーン | teleport_stone | 2 | 帰還用 |
| 解毒剤 | antidote | 3 | 基本ユーティリティ |

**日替わり品 (リストック時にランダム2-3種を選出):**

| アイテム | typeId | 出現時在庫 | 出現率 |
|---------|--------|-----------|--------|
| エナジーポーション | energy_potion | 3 | 高 |
| シールドポーション | shield_potion | 3 | 高 |
| ヘイストポーション | haste_potion | 2 | 中 |
| ドロースクロール | draw_scroll | 2 | 中 |
| 力のエリクサー | strength_elixir | 1 | 低 |
| 鉄壁のエリクサー | iron_skin_elixir | 1 | 低 |
| 魔力結晶 | magic_burst_crystal | 1 | 低 |

**エピック品 (低確率で日替わり枠に1種のみ出現):**

| アイテム | typeId | 在庫 | 出現率 |
|---------|--------|------|--------|
| 時止めの砂時計 | time_stop_hourglass | 1 | 10% |
| コンボエリクサー | combo_elixir | 1 | 10% |

**レジェンダリー以上:** ショップには一切並ばない

### 1-4. 装備品在庫

**ファイル:** `src/constants/data/camps/ShopData.ts`

- 日替わり装備8品 → 各1個限定
- `generateDailyEquipmentInventory()` のレアリティ上限を `epic` に制限（legendary除外）
- 購入済み装備はsold out表示

### 1-5. ShopData拡張

**ファイル:** `src/constants/data/camps/ShopData.ts`

- 常設消耗品リスト定数: `PERMANENT_CONSUMABLE_LISTINGS`
- 日替わり候補リスト定数: `ROTATING_CONSUMABLE_POOL`
- エピック候補リスト定数: `EPIC_CONSUMABLE_POOL`
- `generateRotatingConsumables(seed)` — シードベースで日替わり品を選出
- 未設定のshopPriceを全消耗品に設定

### 1-6. Context/Provider統合

**ファイル:** `src/contexts/PlayerContext.tsx` or 新規 `src/contexts/ShopContext.tsx`

- `ShopStockState`をPlayerDataまたは独立コンテキストで管理
- `purchaseFromShop(itemKey)` — 在庫減算 + ゴールド消費 + アイテム追加
- `checkAndRestock(encounterCount)` — DungeonRunのencounterCount変更時に自動チェック

**変更ファイル一覧:**
- `src/types/campTypes.ts` — ShopStockState型追加
- `src/types/saveTypes.ts` — shopState追加
- `src/constants/data/camps/ShopData.ts` — 常設/日替わりリスト、価格設定
- `src/constants/data/items/ConsumableItemData.ts` — 全アイテムにshopPrice設定
- `src/domain/camps/logic/shopLogic.ts` — 在庫管理ロジック
- `src/contexts/PlayerContext.tsx` — shopState統合

---

## Session 2: ショップUI + リストック通知

### 2-1. BuyTab在庫表示

**ファイル:** `src/ui/html/campsHtml/Shop/BuyTab.tsx`

- 各アイテムに在庫数バッジ表示 (`残り: X`)
- 在庫0のアイテムは「売り切れ」状態（グレーアウト、購入ボタン無効化）
- 購入時に在庫数がリアルタイム減少
- 消耗品セクションを「常設品」と「本日の特売」に分離表示

### 2-2. 装備品sold out表示

**ファイル:** `src/ui/html/campsHtml/Shop/BuyTab.tsx`

- 購入済み装備にsold outオーバーレイ
- 装備品セクションタイトル: 「装備品 (日替わり)」維持

### 2-3. リストック通知システム

**新規ファイル:** `src/ui/html/componentsHtml/RestockNotification.tsx`
**新規CSS:** `src/ui/css/components/RestockNotification.css`

リストック発生時:
- ショップアイコンにピンバッジ（赤丸+数字）アニメーション
- BaseCampのショップボタンにバッジ表示
- ショップ画面を開いた時に「品揃えが更新されました！」バナー（2秒後自動消去）
- バッジは未読状態をPlayerDataで管理（ショップを開いたらクリア）

### 2-4. CSS更新

**ファイル:** `src/ui/css/camps/Shop.css`

- `.stock-badge` — 在庫数表示スタイル
- `.sold-out` — 売り切れグレーアウト
- `.sold-out-overlay` — 売り切れテキストオーバーレイ
- `.restock-banner` — リストック通知バナー
- `.pin-badge` — ショップボタン上のピンバッジ
- `.pin-badge-animate` — ピンバッジ出現アニメーション

**変更ファイル一覧:**
- `src/ui/html/campsHtml/Shop/BuyTab.tsx` — 在庫UI、セクション分離
- `src/ui/html/campsHtml/BaseCamp.tsx` — ピンバッジ表示
- `src/ui/html/componentsHtml/RestockNotification.tsx` — 新規
- `src/ui/css/camps/Shop.css` — 在庫・sold out・通知スタイル
- `src/ui/css/components/RestockNotification.css` — 新規

---

## Session 3: カードレアリティ完全削除

### 3-1. 型定義からRarity削除

**ファイル:** `src/types/cardTypes.ts`
- `Rarity` type定義を削除 (L32)
- `Card`インターフェースから`rarity`フィールド削除 (L64-65)
- `CardCategory`はそのまま維持

**ファイル:** `src/types/campTypes.ts`
- `CardFilterOptions.rarity` → `CardFilterOptions.category` + `CardFilterOptions.element` に変更 (L381-385)

**ファイル:** `src/types/index.ts`
- barrel exportから`Rarity`が消えることを確認（`export * from './cardTypes'`なので自動）

### 3-2. カード定数からレアリティ関連を削除

**ファイル:** `src/constants/cardConstants.ts`
- `RARITY_COLORS`定数を削除 (L58)

### 3-3. 全カードデータからrarity削除

**ファイル:**
- `src/constants/data/cards/swordmanCards.ts` — 42枚から`rarity`行を削除
- `src/constants/data/cards/mageCards.ts` — 40枚から`rarity`行を削除
- `src/constants/data/cards/summonerCards.ts` — 40枚から`rarity`行を削除

合計122枚のカード定義から`rarity: "xxx",`行を除去。

### 3-4. ドメインロジックのrarity参照除去

**ファイル:**
- `src/domain/cards/type/cardType.ts` — Rarityのre-export削除 (L5)
- `src/constants/data/camps/CardEncyclopediaData.ts` — `getCardsByRarity()`削除、ソートロジックからrarity除去
- `src/ui/html/campsHtml/Library/CardDerivationTree.tsx` — rarity表示・色分け削除

### 3-5. バトル/報酬からrarity除去

**ファイル:**
- `src/ui/html/battleHtml/VictoryScreen.tsx` — カードrarity表示削除 (L117)

**変更ファイル一覧:**
- `src/types/cardTypes.ts` — Rarity型・フィールド削除
- `src/types/campTypes.ts` — CardFilterOptions変更
- `src/constants/cardConstants.ts` — RARITY_COLORS削除
- `src/constants/data/cards/swordmanCards.ts` — 42箇所rarity行削除
- `src/constants/data/cards/mageCards.ts` — 40箇所rarity行削除
- `src/constants/data/cards/summonerCards.ts` — 40箇所rarity行削除
- `src/domain/cards/type/cardType.ts` — re-export削除
- `src/constants/data/camps/CardEncyclopediaData.ts` — rarity関連関数削除
- `src/ui/html/campsHtml/Library/CardDerivationTree.tsx` — rarity表示削除
- `src/ui/html/battleHtml/VictoryScreen.tsx` — rarity表示削除

---

## Session 4: カード図鑑UI刷新

### 4-1. 新フィルタリングシステム

**ファイル:** `src/ui/html/campsHtml/Library/CardEncyclopediaTab.tsx`

レアリティフィルタを以下に置換:
- **カテゴリフィルタ**: atk / def / buff / debuff / heal / swordEnergy
- **エレメントフィルタ**: physics / guard / fire / ice / lightning / dark / light / summon / enhance / sacrifice
- **コストフィルタ**: AP 0-1 / 2-3 / 4-5 / 6+
- **クラスフィルタ**: swordsman / mage / summoner（既存維持）
- **検索テキスト**: 既存維持

### 4-2. 未取得カード非公開表示

**ファイル:** `src/ui/html/campsHtml/Library/CardEncyclopediaTab.tsx`

未取得（isUnlocked === false）のカード:
- カード名: 「???」表示
- カードアイコン/画像: 黒塗りシルエット
- 説明文: 「このカードの情報は未解禁です」
- ステータス（ダメージ、コスト等）: 全て「???」
- カテゴリ/エレメント: 非表示（フィルタには含めない or 含める — 要検討）

取得済みのカード:
- 全情報表示
- 熟練度レベル・使用回数の表示を目立たせる

### 4-3. カードリスト表示の色分け変更

**ファイル:** `src/ui/css/camps/Library.css`

レアリティベースの色分けを廃止し、以下に変更:
- カテゴリごとのアイコン/色帯（atk=赤、def=青、buff=緑、debuff=紫、heal=白）
- エレメントバッジ（小さいアイコン or 色ドット）
- 熟練度レベル表示（星マーク等）

### 4-4. CardDerivationTree更新

**ファイル:** `src/ui/html/campsHtml/Library/CardDerivationTree.tsx`

- レアリティ色分け → カテゴリ色分けに変更
- ノード表示にエレメントアイコン追加
- 未取得ノードは黒塗り+「???」

**変更ファイル一覧:**
- `src/types/campTypes.ts` — CardFilterOptions更新
- `src/ui/html/campsHtml/Library/CardEncyclopediaTab.tsx` — フィルタUI全面刷新
- `src/ui/html/campsHtml/Library/CardDerivationTree.tsx` — 色分け変更
- `src/ui/css/camps/Library.css` — レアリティCSS→カテゴリCSS
- `src/constants/data/camps/CardEncyclopediaData.ts` — フィルタ/ソートロジック更新

---

## 各Session検証方法

### Session 1 検証
1. `npm run build` — TypeScriptエラーなし
2. 新しいShopStockState型がsaveデータに含まれること確認
3. shopLogicの単体動作（在庫初期化、減算、リストック判定）

### Session 2 検証
1. `npm run build` — TypeScriptエラーなし
2. ブラウザでショップ画面を開き:
   - 常設品と日替わり品が分離表示されること
   - 在庫数が表示されること
   - 購入後に在庫が減ること
   - 在庫0で「売り切れ」表示になること
   - 装備品が各1個購入後にsold out表示になること
3. encounterCountが閾値に達した後にショップを開き、リストック通知が表示されること

### Session 3 検証
1. `npm run build` — TypeScriptエラーなし
2. `grep -r "rarity" src/types/cardTypes.ts` → ヒットなし
3. `grep -r "RARITY_COLORS" src/` → ヒットなし
4. 全カードデータにrarityプロパティがないこと確認

### Session 4 検証
1. `npm run build` — TypeScriptエラーなし
2. ブラウザで図鑑画面を開き:
   - カテゴリ/エレメント/コストでフィルタリングできること
   - 未取得カードが黒塗り+「???」で表示されること
   - 既存の検索・クラスフィルタが正常動作すること
   - CardDerivationTreeがカテゴリ色分けで表示されること
