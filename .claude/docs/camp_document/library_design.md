# 図書館（書庫）詳細設計書 (LIBRARY_DESIGN_V1)

## 更新履歴

- V1.0: 初版作成（デッキ編成、図鑑、戦績、セーブ/ロード）

---

## 1. 概要

図書館（The Grand Library）は、**知識と記録の殿堂**です。

デッキ編成、図鑑、戦績、セーブデータを管理する、プレイヤーの「メタゲーム」を支える施設です。

### 主な役割

1. **編成管理 (Build Management)**: デッキ・装備・アイテムの組み合わせ
2. **知識の蓄積 (Encyclopedia)**: カード・装備・魔物の記録
3. **進歩の記録 (Achievements)**: 称号、達成度
4. **記憶の保管 (Save/Load)**: セーブデータ管理

---

## 2. 詳細機能仕様

### 2.1 4 つの本棚（メインメニュー）

```
┌────────────────────────────────────────────┐
│  📚 図書館 - The Grand Library            │
├────────────────────────────────────────────┤
│                                            │
│      [本棚から本を選んでください]          │
│                                            │
│  ┌──────────┐  ┌──────────┐              │
│  │  📖      │  │  📕      │              │
│  │ 編成の書  │  │ 図鑑の書  │              │
│  │          │  │          │              │
│  │デッキ編成 │  │カード図鑑 │              │
│  │装備選択   │  │装備図鑑   │              │
│  │          │  │魔物図鑑   │              │
│  └──────────┘  └──────────┘              │
│                                            │
│  ┌──────────┐  ┌──────────┐              │
│  │  📘      │  │  📗      │              │
│  │ 戦績の書  │  │記憶の間   │              │
│  │          │  │          │              │
│  │称号一覧   │  │セーブ     │              │
│  │達成度     │  │ロード     │              │
│  │          │  │          │              │
│  └──────────┘  └──────────┘              │
│                                            │
│  [キャンプに戻る]                          │
└────────────────────────────────────────────┘
```

---

## 3. 編成の書 (Build Management)

### 3.1 デッキ編成画面

```
┌────────────────────────────────────────────────────────┐
│  📖 編成の書 - デッキ編成                              │
├────────────────────────────────────────────────────────┤
│  現在のキャラクター: 剣士 (Swordsman)                   │
│                                                        │
│  ┌──────────────────┐  ┌────────────────────────────┐ │
│  │ 現在のデッキ (40) │  │ カードプール               │ │
│  │                  │  │ [全て][攻撃][防御][特殊]   │ │
│  │ [攻撃カード]     │  │                            │ │
│  │ ⚔️ 斬撃 x4       │  │ ⚔️ 斬撃 (所持: 4/4)       │ │
│  │ 🔥 烈火斬り x2   │  │ 🔥 烈火斬り (所持: 2/3)   │ │
│  │                  │  │ ⚡ 雷撃 (所持: 0/2) 🔒    │ │
│  │ [防御カード]     │  │                            │ │
│  │ 🛡️ 防御 x3       │  │ 🛡️ 防御 (所持: 3/4)       │ │
│  │                  │  │ ...                        │ │
│  │ [特殊カード]     │  │                            │ │
│  │ 💊 回復 x2       │  │                            │ │
│  │                  │  │                            │ │
│  └──────────────────┘  └────────────────────────────┘ │
│                                                        │
│  マナカーブ: [0|1███|2████|3██|4█|5]                  │
│                                                        │
│  [ロードアウト保存]  [セット1] [セット2] [セット3]     │
│  [リセット]  [戻る]                                    │
└────────────────────────────────────────────────────────┘
```

### 3.2 デッキ編成のルール

**基本ルール:**

- デッキ枚数: 40 枚（固定）
- 同じカードは最大 4 枚まで
- クラス専用カードのみ使用可能

**カードの状態:**

- **所持済み**: 編成可能
- **未所持**: グレーアウト、🔒 マーク
- **熟練度不足**: 一部のカードは熟練度で解禁

### 3.3 装備・アイテム編成

```
┌────────────────────────────────────────────┐
│  装備選択                                  │
├────────────────────────────────────────────┤
│  装備スロット:                             │
│  [Weapon]  🗡️ 鉄の剣 (Lv2, normal)       │
│  [Armor]   🛡️ 騎士の鎧 (Lv1, good)       │
│  [Helmet]  👑 (未装備)                    │
│  [Boots]   👢 革のブーツ (Lv0, poor)      │
│  [Accessory1] 💍 力の指輪 (Lv0, master)  │
│  [Accessory2] (未装備)                    │
│                                            │
│  初期アイテム (最大3個):                   │
│  [1] 🧪 小回復ポーション                  │
│  [2] 🔮 転移石（通常）                     │
│  [3] (未選択)                             │
└────────────────────────────────────────────┘
```

### 3.4 ロードアウト保存

**3 つのセット:**

- セット 1: 「バランス型」
- セット 2: 「攻撃特化」
- セット 3: 「防御重視」

**機能:**

- 現在の編成を保存
- ワンクリックで切り替え
- 名前変更可能

---

## 4. 図鑑の書 (Encyclopedia)

### 4.1 カード図鑑

```
┌────────────────────────────────────────────────────────┐
│  📕 図鑑の書 - カード図鑑                              │
├────────────────────────────────────────────────────────┤
│  [全て][剣士][魔術師][召喚師][共通]                     │
│  発見度: 45 / 140 (32%)                                │
│                                                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ ⚔️       │  │ 🔥       │  │ ⚡       │          │
│  │ 斬撃     │  │ 烈火斬り  │  │ ？？？    │          │
│  │          │  │          │  │          │          │
│  │熟練度: 3 │  │熟練度: 1 │  │ 未発見    │          │
│  └──────────┘  └──────────┘  └──────────┘          │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ [選択中のカード詳細]                            │   │
│  │ ⚔️ 斬撃                                        │   │
│  │                                                │   │
│  │ コスト: 1                                      │   │
│  │ タイプ: 攻撃                                   │   │
│  │ 効果: 敵に8ダメージを与える。                  │   │
│  │                                                │   │
│  │ 熟練度: Lv3 (使用回数: 245 / 250)             │   │
│  │ 次のレベル: Lv4 (進化解禁)                     │   │
│  │                                                │   │
│  │ 進化先:                                        │   │
│  │ - 【力】烈火斬り: ダメージ12、バーン付与       │   │
│  │ - 【技】連斬: ダメージ6x2                      │   │
│  │                                                │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘
```

### 4.2 装備図鑑

```
┌────────────────────────────────────────────────────────┐
│  📕 図鑑の書 - 装備図鑑                                │
├────────────────────────────────────────────────────────┤
│  [全て][武器][防具][兜][靴][装飾品]                     │
│  発見度: 28 / 73 (38%)                                 │
│                                                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ 🗡️       │  │ ⚔️       │  │ 🔱       │          │
│  │ 鉄の剣   │  │ 鋼の剣   │  │ ？？？    │          │
│  │          │  │          │  │          │          │
│  │Common    │  │Rare      │  │ 未発見    │          │
│  └──────────┘  └──────────┘  └──────────┘          │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ [選択中の装備詳細]                              │   │
│  │ 🗡️ 鉄の剣                                      │   │
│  │                                                │   │
│  │ レアリティ: Common                             │   │
│  │ スロット: 武器                                  │   │
│  │                                                │   │
│  │ 基礎ステータス:                                │   │
│  │ ATK: +10                                       │   │
│  │ AP: 50                                         │   │
│  │                                                │   │
│  │ 入手方法:                                      │   │
│  │ - Shop: コモン装備パック                       │   │
│  │ - ドロップ: 深度1-2の敵                        │   │
│  │                                                │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘
```

### 4.3 魔物図鑑

```
┌────────────────────────────────────────────────────────┐
│  📕 図鑑の書 - 魔物図鑑                                │
├────────────────────────────────────────────────────────┤
│  [全て][深度1][深度2][深度3][深度4][深度5][ボス]      │
│  発見度: 18 / 45 (40%)                                 │
│                                                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ 👻       │  │ 🧟       │  │ 🐺       │          │
│  │ ゴブリン  │  │ スケルトン│  │ ？？？    │          │
│  │          │  │          │  │          │          │
│  │撃破: 32  │  │撃破: 18  │  │ 未遭遇    │          │
│  └──────────┘  └──────────┘  └──────────┘          │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ [選択中の魔物詳細]                              │   │
│  │ 👻 ゴブリン                                    │   │
│  │                                                │   │
│  │ HP: 30                                         │   │
│  │ 出現深度: 1-2                                  │   │
│  │                                                │   │
│  │ 行動パターン:                                  │   │
│  │ - 通常攻撃: 5ダメージ                          │   │
│  │ - 防御: Guard +3                               │   │
│  │                                                │   │
│  │ ドロップ:                                      │   │
│  │ - Gold: 10-15                                  │   │
│  │ - 魔石（小）: 10%                              │   │
│  │ - 装備: 5% (Common)                            │   │
│  │                                                │   │
│  │ 撃破回数: 32回                                 │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘
```

---

## 5. 戦績の書 (Achievements)

### 5.1 称号システム

```
┌────────────────────────────────────────────────────────┐
│  📘 戦績の書 - 称号                                    │
├────────────────────────────────────────────────────────┤
│  現在の称号: 🏆 ゴブリンスレイヤー                     │
│                                                        │
│  獲得済み称号: 12 / 50                                │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ 🏆 ゴブリンスレイヤー                           │   │
│  │ 獲得日: 2026/01/05                              │   │
│  │ 条件: ゴブリンを100体撃破                       │   │
│  │ 効果: ゴブリンからGold +10%                     │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ 🔥 炎の使い手                                   │   │
│  │ 獲得日: 2026/01/08                              │   │
│  │ 条件: 炎属性カードを100回使用                   │   │
│  │ 効果: なし（コレクション）                      │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ ？？？ 未獲得                                   │   │
│  │ 条件: ？？？                                    │   │
│  │ ヒント: 深度5に到達する                         │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘
```

### 5.2 達成度・統計

```
┌────────────────────────────────────────────┐
│  📘 戦績の書 - 達成度                      │
├────────────────────────────────────────────┤
│  総プレイ時間: 45時間32分                  │
│  総ラン回数: 87回                          │
│                                            │
│  【探索記録】                              │
│  最深到達: 深度4 (ボス前)                  │
│  クリア回数: 0回                           │
│  死亡回数: 87回                            │
│                                            │
│  【戦闘記録】                              │
│  総撃破数: 1,234体                         │
│  最大ダメージ: 156                         │
│  最長コンボ: 8ターン生存                   │
│                                            │
│  【経済記録】                              │
│  累計獲得Gold: 45,600                      │
│  累計獲得魂: 850                           │
│  最大所持Gold: 3,200                       │
│                                            │
│  【コレクション】                          │
│  カード発見率: 45/140 (32%)                │
│  装備発見率: 28/73 (38%)                   │
│  魔物遭遇率: 18/45 (40%)                   │
└────────────────────────────────────────────┘
```

---

## 6. 記憶の間 (Save/Load)

### 6.1 セーブデータ管理

```
┌────────────────────────────────────────────────────────┐
│  📗 記憶の間 - セーブ/ロード                           │
├────────────────────────────────────────────────────────┤
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ スロット1: [使用中]                             │   │
│  │ キャラクター: 剣士 (Lv.15)                      │   │
│  │ 魂の残滓: 150                                   │   │
│  │ 到達深度: 深度3                                 │   │
│  │ プレイ時間: 12時間45分                          │   │
│  │ 最終セーブ: 2026/01/09 14:30                   │   │
│  │                                                │   │
│  │ [ロード] [上書き保存] [削除]                   │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ スロット2: [使用中]                             │   │
│  │ キャラクター: 魔術師 (Lv.8)                     │   │
│  │ 魂の残滓: 80                                    │   │
│  │ 到達深度: 深度2                                 │   │
│  │ プレイ時間: 5時間20分                           │   │
│  │ 最終セーブ: 2026/01/08 20:15                   │   │
│  │                                                │   │
│  │ [ロード] [上書き保存] [削除]                   │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ スロット3: [空き]                               │   │
│  │                                                │   │
│  │ [新規作成]                                     │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  [エクスポート] [インポート] [戻る]                    │
└────────────────────────────────────────────────────────┘
```

### 6.2 オートセーブ

**自動保存タイミング:**

- BaseCamp 帰還時
- 施設利用後
- 探索開始前
- 5 分ごと（バックグラウンド）

**保存内容:**

```typescript
{
  player: {
    character: 'swordsman',
    gold: 1250,
    soulRemnants: 150,
    hp: 100,
    maxHp: 100,
    sanctuaryProgress: {...},
    // ...
  },
  inventory: [...],
  deck: [...],
  equipment: {...},
  library: {
    encyclopedia: {...},
    achievements: [...],
    statistics: {...}
  },
  timestamp: '2026-01-09T14:30:00Z'
}
```

### 6.3 エクスポート/インポート

**エクスポート:**

- JSON 形式でダウンロード
- ファイル名: `roguelike_save_20260109_143000.json`
- バックアップ用

**インポート:**

- JSON ファイルをアップロード
- データ検証
- 上書き確認

---

## 7. データ構造定義

### 7.1 LibraryTypes.ts

```typescript
// src/types/LibraryTypes.ts (新規作成)

/**
 * ロードアウト（デッキ編成セット）
 */
export interface Loadout {
  id: string;
  name: string;
  deck: string[]; // カードIDの配列
  equipment: {
    weapon?: string;
    armor?: string;
    helmet?: string;
    boots?: string;
    accessory1?: string;
    accessory2?: string;
  };
  initialItems: string[]; // アイテムID（最大3個）
}

/**
 * 図鑑データ
 */
export interface Encyclopedia {
  cards: {
    discovered: Set<string>;
    mastery: Map<string, number>; // cardId -> 熟練度レベル
    useCount: Map<string, number>; // cardId -> 使用回数
  };
  equipment: {
    discovered: Set<string>;
  };
  monsters: {
    encountered: Set<string>;
    defeatCount: Map<string, number>; // monsterId -> 撃破回数
  };
}

/**
 * 称号
 */
export interface Title {
  id: string;
  name: string;
  description: string;
  icon: string;
  condition: string;
  effect?: string;
  unlockedAt?: Date;
}

/**
 * 統計情報
 */
export interface Statistics {
  totalPlayTime: number; // 秒
  totalRuns: number;
  deepestDepth: number;
  clearCount: number;
  deathCount: number;
  totalDefeats: number;
  maxDamage: number;
  longestCombo: number;
  totalGoldEarned: number;
  totalSoulEarned: number;
  maxGoldHeld: number;
}

/**
 * Libraryの状態
 */
export interface LibraryState {
  loadouts: Loadout[];
  currentLoadout: string; // loadout id
  encyclopedia: Encyclopedia;
  unlockedTitles: Set<string>;
  currentTitle: string | null;
  statistics: Statistics;
}
```

---

## 8. 図鑑の更新ロジック

### 8.1 カード図鑑の更新

```typescript
// src/camps/facilities/Library/logic/updateEncyclopedia.ts

import type { Encyclopedia } from "../../../../types/LibraryTypes";

/**
 * カード使用時に図鑑を更新
 */
export function recordCardUse(
  encyclopedia: Encyclopedia,
  cardId: string
): Encyclopedia {
  const updated = { ...encyclopedia };

  // 発見に追加
  updated.cards.discovered.add(cardId);

  // 使用回数カウント
  const currentCount = updated.cards.useCount.get(cardId) || 0;
  updated.cards.useCount.set(cardId, currentCount + 1);

  // 熟練度レベルアップ判定
  const newCount = currentCount + 1;
  const currentMastery = updated.cards.mastery.get(cardId) || 0;

  // 熟練度閾値: 50, 150, 250, 400, 600...
  const thresholds = [50, 150, 250, 400, 600];
  const newMastery = thresholds.findIndex((t) => newCount < t) + 1;

  if (newMastery > currentMastery) {
    updated.cards.mastery.set(cardId, newMastery);
    // レベルアップイベント発火
  }

  return updated;
}

/**
 * 装備入手時に図鑑を更新
 */
export function recordEquipmentAcquired(
  encyclopedia: Encyclopedia,
  equipmentTypeId: string
): Encyclopedia {
  const updated = { ...encyclopedia };
  updated.equipment.discovered.add(equipmentTypeId);
  return updated;
}

/**
 * 魔物遭遇時に図鑑を更新
 */
export function recordMonsterEncounter(
  encyclopedia: Encyclopedia,
  monsterId: string,
  defeated: boolean
): Encyclopedia {
  const updated = { ...encyclopedia };

  updated.monsters.encountered.add(monsterId);

  if (defeated) {
    const currentCount = updated.monsters.defeatCount.get(monsterId) || 0;
    updated.monsters.defeatCount.set(monsterId, currentCount + 1);
  }

  return updated;
}
```

---

## 9. Context API への統合

### 9.1 LibraryContext の作成

```typescript
// src/contexts/LibraryContext.tsx (新規作成)

import { createContext, useContext, useState, ReactNode } from "react";
import type {
  LibraryState,
  Loadout,
  Encyclopedia,
  Statistics,
} from "../types/LibraryTypes";

interface LibraryContextValue {
  libraryState: LibraryState;

  // ロードアウト
  saveLoadout: (loadout: Loadout) => void;
  loadLoadout: (loadoutId: string) => void;
  deleteLoadout: (loadoutId: string) => void;

  // 図鑑
  updateEncyclopedia: (update: Partial<Encyclopedia>) => void;

  // 称号
  unlockTitle: (titleId: string) => void;
  setCurrentTitle: (titleId: string | null) => void;

  // 統計
  updateStatistics: (update: Partial<Statistics>) => void;
}

const LibraryContext = createContext<LibraryContextValue | undefined>(
  undefined
);

export const LibraryProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [libraryState, setLibraryState] = useState<LibraryState>(() => {
    // localStorage から読み込み
    const saved = localStorage.getItem("library");
    if (saved) {
      return JSON.parse(saved);
    }

    return {
      loadouts: [],
      currentLoadout: "",
      encyclopedia: {
        cards: {
          discovered: new Set(),
          mastery: new Map(),
          useCount: new Map(),
        },
        equipment: {
          discovered: new Set(),
        },
        monsters: {
          encountered: new Set(),
          defeatCount: new Map(),
        },
      },
      unlockedTitles: new Set(),
      currentTitle: null,
      statistics: {
        totalPlayTime: 0,
        totalRuns: 0,
        deepestDepth: 0,
        clearCount: 0,
        deathCount: 0,
        totalDefeats: 0,
        maxDamage: 0,
        longestCombo: 0,
        totalGoldEarned: 0,
        totalSoulEarned: 0,
        maxGoldHeld: 0,
      },
    };
  });

  // ロードアウト保存
  const saveLoadout = (loadout: Loadout) => {
    setLibraryState((prev) => ({
      ...prev,
      loadouts: [...prev.loadouts.filter((l) => l.id !== loadout.id), loadout],
    }));
  };

  // ... 他のメソッド

  return (
    <LibraryContext.Provider
      value={{
        libraryState,
        saveLoadout,
        loadLoadout,
        deleteLoadout,
        updateEncyclopedia,
        unlockTitle,
        setCurrentTitle,
        updateStatistics,
      }}
    >
      {children}
    </LibraryContext.Provider>
  );
};

export const useLibrary = () => {
  const context = useContext(LibraryContext);
  if (!context) {
    throw new Error("useLibrary must be used within LibraryProvider");
  }
  return context;
};
```

---

## 10. 実装手順（概要）

### Phase 1: データ構造（Week 1: Day 1-2）

```
□ LibraryTypes.ts 作成
□ LibraryContext 作成
□ 図鑑データの初期化
```

### Phase 2: 編成の書（Week 1: Day 3 - Week 2: Day 1）

```
□ DeckBuilder.tsx（デッキ編成UI）
□ EquipmentSelector.tsx（装備選択）
□ LoadoutManager.tsx（ロードアウト管理）
```

### Phase 3: 図鑑の書（Week 2: Day 2-3）

```
□ Encyclopedia.tsx（図鑑メイン）
□ CardEncyclopedia.tsx（カード図鑑）
□ EquipmentEncyclopedia.tsx（装備図鑑）
□ MonsterEncyclopedia.tsx（魔物図鑑）
```

### Phase 4: 戦績の書（Week 2: Day 4）

```
□ Achievements.tsx（称号UI）
□ Statistics.tsx（統計UI）
```

### Phase 5: 記憶の間（Week 3: Day 1-2）

```
□ SaveLoad.tsx（セーブ/ロードUI）
□ セーブデータの検証
□ エクスポート/インポート機能
```

---

## 11. 注意事項

### 11.1 データ永続化

**重要:** LibraryState は完全に永続化が必要

- localStorage への保存
- 定期的なオートセーブ
- データ破損時のバックアップ

### 11.2 図鑑の更新タイミング

**バトル中:**

- カード使用 → recordCardUse
- 魔物遭遇 → recordMonsterEncounter

**アイテム獲得時:**

- 装備入手 → recordEquipmentAcquired

**統計更新:**

- ラン終了時に一括更新

---

## 12. 参照ドキュメント

```
BASE_CAMP_DESIGN_V2
└── LIBRARY_DESIGN_V1 [本文書]
    ├── LibraryContext.tsx
    ├── updateEncyclopedia.ts
    └── SaveLoadManager.ts
```

---

**次のステップ:** 実装手順書の詳細版を作成

## まとめ

図書館の設計が完成しました：

**主な機能:**

- ✅ デッキ・装備・アイテム編成
- ✅ 図鑑（カード・装備・魔物）
- ✅ 称号・達成度システム
- ✅ セーブ/ロード管理

**設計意図:**

- ビルド研究の場
- プレイヤーの進歩を可視化
- 知識の蓄積と活用
