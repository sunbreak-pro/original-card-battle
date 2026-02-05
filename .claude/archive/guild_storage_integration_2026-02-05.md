# 計画：Storage機能のGuild統合

**実装日:** 2026-02-05
**ステータス:** 完了

## 概要

Storage（倉庫）を独立施設からGuild（ギルド）のタブに統合し、施設数を削減。

## 変更内容

### 新規作成
- `src/ui/html/campsHtml/Guild/StorageTab.tsx` - Storage機能をタブ化

### 修正
- `src/types/campTypes.ts` - `GuildTab`に`"storage"`追加
- `src/constants/campConstants.ts` - `GUILD_TABS`に倉庫タブ追加、`FACILITY_NAV_ITEMS`からStorage削除、`NavFacilityType`型追加
- `src/ui/html/campsHtml/Guild/Guild.tsx` - StorageTabインポート・表示追加
- `src/ui/css/camps/Guild.css` - Storage.cssのスタイルを`.guild-storage-tab`スコープで統合
- `src/App.tsx` - Storageインポート・ルーティング削除

### 削除
- `src/ui/html/campsHtml/Storage/Storage.tsx`

### 保持（共有コンポーネント）
- `src/ui/html/campsHtml/Storage/ItemCard.tsx`
- `src/ui/html/campsHtml/Storage/ItemDetailPanel.tsx`
- `src/ui/css/camps/Storage.css` - 未使用だが他から参照される可能性あり

## Guild 4タブ構成

1. **昇格試験** (Exam.tsx) - 昇格試験バトル
2. **噂** (RumorsTab.tsx) - 探索バフ購入
3. **依頼** (QuestsTab.tsx) - クエスト受注
4. **倉庫** (StorageTab.tsx) - アイテム・装備管理

## 結果

- ビルド成功確認済み
- 施設数: 6 → 5 に削減
- 倉庫機能はギルドタブから利用可能
