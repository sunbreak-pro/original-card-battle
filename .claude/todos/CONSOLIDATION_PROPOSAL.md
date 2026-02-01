# 施設UI共通化 提案書

## 概要

施設画面に繰り返されているUIパターンを分析し、共通化の候補と推奨アプローチをまとめる。

---

## 現状分析: 重複パターン一覧

### パターンA: 施設タブデータのハードコード

**該当**: Shop.tsx, Blacksmith.tsx, Guild.tsx

各施設がタブ（icon + label）をJSX内に個別にハードコードしている。配列化されておらず、タブの追加・変更時にJSX構造を直接編集する必要がある。

```tsx
// Shop.tsx (lines 22-43) — 3タブをJSX内に直書き
<button className={`shop-tab ${selectedTab === "buy" ? "active" : ""}`}>
  <span className="tab-icon">🛒</span>
  <span className="tab-label">Buy</span>
</button>
// ... sell, exchange も同様
```

**問題**: タブ追加時にJSX構造の変更が必要。タブメタデータが散在。

---

### パターンB: Library タブデータ（配列化済・未抽出）

**該当**: Library.tsx (lines 23-27)

```tsx
const tabs: { id: LibraryTab; label: string }[] = [
  { id: "cards", label: "Card Encyclopedia" },
  { id: "enemies", label: "Enemy Bestiary" },
  { id: "tips", label: "Game Tips" },
];
```

配列化済みだが定数ファイルに未抽出。コンポーネント内に閉じている。

---

### パターンC: DungeonGate タブデータ（配列化済・未抽出）

**該当**: DungeonGate.tsx (lines 53-57)

```tsx
const tabs: { key: PreparationTab; label: string }[] = [
  { key: "deck", label: "デッキ" },
  { key: "inventory", label: "持ち物" },
  { key: "equipment", label: "装備" },
];
```

Libraryと同様、配列化済みだが定数ファイルに未抽出。key/id命名も不統一。

---

### パターンD: タブUIパターンの重複

**該当**: 全5施設画面（Shop, Blacksmith, Guild, Library, DungeonGate）

同じ「タブナビ + コンテンツ切替」パターンを各画面で個別実装。

```
<nav className="{facility}-tabs">
  {tabs.map → <button className="{facility}-tab active?">}
</nav>
<div className="{facility}-content">
  {selectedTab === "xxx" && <XxxTab />}
</div>
```

CSSも `tabs.css` 内に施設別テーマ変数を定義しつつ、各施設CSSで重複スタイル記述。

---

### パターンE: 戻るボタンの重複

**該当**: 全施設画面

| ファイル | クラス名 | テキスト |
|---------|----------|---------|
| Shop.tsx | `shop-back-button` | ← Back to Camp |
| Blacksmith.tsx | `blacksmith-back-button` | ← Back to Camp |
| Guild.tsx | `guild-back-button` | ← Back to Camp |
| Library.tsx | `library-back-button` | Return to Camp |
| DungeonGate.tsx | `dungeon-gate-back-button` | ← キャンプに戻る |

テキストも英語/日本語混在。CSS定義も各施設のCSSファイルで重複（`display: block; margin: 1vh auto; padding: 1.2vh 2vw; ...`）。

---

## 推奨アプローチ

### 優先度1: タブデータの定数抽出（低リスク・高効果）

Shop/Blacksmith/Guild のハードコードタブを配列化し、Library/DungeonGateパターンに統一。
全タブ定義を `campConstants.ts` に集約。

```ts
// campConstants.ts
export const SHOP_TABS = [
  { id: "buy", label: "Buy", icon: "🛒" },
  { id: "sell", label: "Sell", icon: "💰" },
  { id: "exchange", label: "Exchange", icon: "🔄" },
] as const;

export const LIBRARY_TABS = [
  { id: "cards", label: "Card Encyclopedia" },
  { id: "enemies", label: "Enemy Bestiary" },
  { id: "tips", label: "Game Tips" },
] as const;

// ... 他施設も同様
```

**影響範囲**: 各施設のtabs配列を定数importに置換するだけ。JSX構造の変更不要。

---

### 優先度2: 戻るボタンの共通コンポーネント化（低リスク）

```tsx
// componentsHtml/BackToCampButton.tsx
const BackToCampButton = () => {
  const { navigateTo } = useGameState();
  return (
    <button className="back-to-camp-button" onClick={() => navigateTo("camp")}>
      ← キャンプに戻る
    </button>
  );
};
```

- テキストを日本語に統一
- CSS を1箇所に集約
- 各施設から `{facility}-back-button` を削除

---

### 優先度3: タブナビコンポーネント（中リスク・将来検討）

汎用 `FacilityTabNav` コンポーネントの作成。

```tsx
interface FacilityTabNavProps<T extends string> {
  tabs: ReadonlyArray<{ id: T; label: string; icon?: string }>;
  activeTab: T;
  onTabChange: (tab: T) => void;
  facility: string; // CSS テーマ用
}
```

**注意**: 各施設のタブ構造はほぼ同一だが、コンテンツ切替部分は施設固有ロジックが多い。ナビ部分のみの共通化が現実的。CSS変数テーマ（`tabs.css`）は既に存在するため、クラス名の統一で大部分解決。

---

## 実装順序の推奨

1. **タブデータ定数抽出**（パターンA/B/C） — 各施設のタブ配列を `campConstants.ts` に移動
2. **戻るボタン共通化**（パターンE） — `BackToCampButton` コンポーネント作成
3. **タブナビコンポーネント**（パターンD） — 上記2つの成果を踏まえて検討

各ステップは独立して実施可能。段階的に進められる。
