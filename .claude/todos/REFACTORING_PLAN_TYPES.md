# 型定義リファクタリング設計案

## ステータス: ✅ 完了 (2026-01-28)

---

## 実装完了サマリー

### Phase 0: 事前準備 ✅
- [x] tsconfig.app.json に paths 設定追加 (`@/*` → `src/*`)
- [x] vite.config.ts に alias 設定追加
- [x] 設定変更後、ビルド確認

### Phase 1: src/types ディレクトリ作成 ✅
- [x] `src/types/` ディレクトリ作成
- [x] 8ファイル作成:
  - `cardTypes.ts` (Depth, Rarity, CardCategory, Card 等)
  - `characterTypes.ts` (CharacterClass, CardCharacterClass, Player, Enemy 等)
  - `itemTypes.ts` (Item, EquipmentSlot, MagicStones 等)
  - `battleTypes.ts` (BuffDebuffType, DamageResult 等)
  - `campTypes.ts` (施設型, NodeStatus 統合版)
  - `dungeonTypes.ts` (DungeonNode, DungeonFloor 等)
  - `saveTypes.ts` (SaveData, SaveResult 等)
  - `index.ts` (barrel export)

### Phase 2: 関数の移動 ✅
- [x] 型ファイルに混在していた関数を `domain/*/logic/` に移動
- [x] 新規ロジックファイル作成:
  - `domain/battles/logic/buffLogic.ts`
  - `domain/battles/logic/cardExecutionLogic.ts`
  - `domain/cards/logic/cardUtils.ts`
  - `domain/characters/logic/characterUtils.ts`
  - `domain/characters/logic/playerUtils.ts`
  - `domain/characters/logic/enemyUtils.ts`
  - `domain/camps/logic/campUtils.ts`
  - `domain/camps/logic/blacksmithUtils.ts`
  - `domain/camps/logic/storageLogic.ts`
  - `domain/item_equipment/logic/itemUtils.ts`

### Phase 3: 旧型ファイルをshimに変換 ✅
- [x] 22個の旧型ファイルをre-export shimに変換

### Phase 4: 定数の重複解消 ✅
- [x] `campConstants.ts` の `RARITY_SELL_PRICES` 削除 → `itemConstants.ts` に一本化
- [x] `characterConstants.ts` の `ResonanceEffectConfig` 削除 → `types/characterTypes.ts` に移動
- [x] `constants/index.ts` に `itemConstants` のexport追加

### Phase 5: consumer の import パス更新 ✅
- [x] ~95+ ファイルの import を `@/types/` パスに変更
- [x] 4つの並列エージェントで更新:
  - Battle module (domain/battles/)
  - Cards + Characters (domain/cards/, domain/characters/)
  - Camps + Dungeon + Save (domain/camps/, domain/dungeon/, domain/save/)
  - Constants + Contexts + UI

### Phase 6: クリーンアップ ✅
- [x] 21個のshimファイル削除
- [x] 空のtype(s)ディレクトリ削除
- [x] **例外**: `domain/cards/type/cardType.ts` は最小限のshimとして残存
  - 理由: `deck.ts`, `deckReducter.ts` が immutable（変更不可）でこのパスからimport

### Phase 7: 検証 ✅
- [x] TypeScript コンパイル確認
- [x] ビルド成功確認 (`npm run build`)
- [x] 開発サーバー起動確認 (`npm run dev`)

---

## 最終的なディレクトリ構造

```
src/
├── types/                    # 全型定義を集約（8ファイル）
│   ├── battleTypes.ts
│   ├── campTypes.ts
│   ├── cardTypes.ts
│   ├── characterTypes.ts
│   ├── dungeonTypes.ts
│   ├── itemTypes.ts
│   ├── saveTypes.ts
│   └── index.ts
│
├── constants/               # 定数のみ（型定義なし）
│   ├── ...Constants.ts
│   ├── itemConstants.ts     # RARITY_SELL_PRICES はここのみ
│   └── index.ts
│
└── domain/
    ├── cards/
    │   ├── type/
    │   │   └── cardType.ts  # 最小shim（immutable deck用）
    │   └── logic/
    │       └── cardUtils.ts # 移動した関数群
    └── .../logic/           # 各domainに移動した関数群
```

---

## 残存項目（意図的）

| ファイル | 理由 |
|---------|------|
| `domain/cards/type/cardType.ts` | `deck.ts`, `deckReducter.ts` が immutable で変更不可。これらは `../type/cardType` からimportするため、shimを残す必要がある |

## 追加修正（2026-01-28 最終確認時）

Phase完了後の最終確認で発見した追加のインポートエラー:

| ファイル | 旧パス | 新パス |
|---------|--------|--------|
| `domain/camps/data/ShopData.ts` | `../../item_equipment/data/ConsumableItemData` | `@/constants/data/items/ConsumableItemData` |
| `domain/item_equipment/logic/generateItem.ts` | `../data/ConsumableItemData` | `@/constants/data/items/ConsumableItemData` |
| `domain/battles/managements/useDeckManage.ts` | `../data/initialDeckConfig` | `@/constants/data/battles/initialDeckConfig` |
| `ui/characterSelectHtml/CharacterCard.tsx` | `../../domain/characters/player/data/CharacterClassData` | `@/constants/data/characters/CharacterClassData` |

すべて修正済み、ビルド確認済み。

---

## 変更統計

| 項目 | 数 |
|------|-----|
| 新規作成（types/） | 8ファイル |
| 新規作成（logic/） | ~10ファイル |
| import更新 | ~95ファイル |
| shim削除 | 21ファイル |
| shim残存 | 1ファイル |

---

*完了日: 2026-01-28*
