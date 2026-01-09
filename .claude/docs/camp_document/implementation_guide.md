# BaseCamp & Guild 実装手順書 (IMPLEMENTATION_GUIDE_V1)

## 0. 事前準備

### 0.1 必要な理解
- React Context API の基礎
- TypeScript の型定義
- 既存のBattleScreenの構造
- 既存のPlayerData.tsxの構造

### 0.2 開発環境の確認
```bash
# プロジェクトルートで確認
npm run dev  # 開発サーバーが起動すること
```

---

## Phase 1: 基盤整備（Week 1: Day 1-3）

### タスク 1.1: 型定義の作成

**優先度:** 🔴 最高（他の実装の基礎）

**1.1.1 ItemTypes.ts の作成**

```bash
# ディレクトリ作成
mkdir -p src/types
```

```typescript
// src/types/ItemTypes.ts

export type ItemType = 
  | 'equipment'
  | 'consumable'
  | 'magicStone'
  | 'material'
  | 'quest'
  | 'key';

export type EquipmentSlot = 
  | 'weapon' 
  | 'armor' 
  | 'helmet' 
  | 'boots' 
  | 'accessory1' 
  | 'accessory2';

export interface Item {
  id: string;
  typeId: string;
  name: string;
  description: string;
  itemType: ItemType;
  icon: string;
  
  // 装備専用
  equipmentSlot?: EquipmentSlot;
  durability?: number;
  maxDurability?: number;
  effects?: EquipmentEffect[];
  
  // 消耗品専用
  stackable?: boolean;
  stackCount?: number;
  maxStack?: number;
  
  // 魔石専用
  magicStoneValue?: number;
  
  // 共通
  rarity: 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary';
  sellPrice: number;
  canSell: boolean;
  canDiscard: boolean;
}

export interface EquipmentEffect {
  type: 'stat' | 'skill' | 'passive';
  target: string;
  value: number | string;
  description: string;
}
```

**1.1.2 GuildTypes.ts の作成**

```typescript
// src/types/GuildTypes.ts

export interface PromotionExam {
  currentGrade: string;
  nextGrade: string;
  requiredCardCount: number;
  requiredGold?: number;
  enemyId: string;
  description: string;
  recommendations: {
    hp: number;
    ap: number;
  };
  rewards: {
    statBonus: string;
    items?: string[];
  };
}

export interface Rumor {
  id: string;
  name: string;
  description: string;
  cost: number;
  effect: RumorEffect;
  rarity: 'common' | 'rare' | 'epic';
  icon: string;
}

export type RumorEffect = 
  | { type: 'elite_rate'; value: number }
  | { type: 'shop_discount'; value: number }
  | { type: 'treasure_rate'; value: number }
  | { type: 'start_bonus'; bonus: string };

export interface Quest {
  id: string;
  title: string;
  description: string;
  type: 'daily' | 'weekly';
  requiredGrade: string;
  objectives: QuestObjective[];
  rewards: QuestReward;
  isActive: boolean;
  isCompleted: boolean;
  expiresAt?: Date;
}

export interface QuestObjective {
  type: 'defeat' | 'collect' | 'explore';
  target: string;
  required: number;
  current: number;
  description: string;
}

export interface QuestReward {
  gold?: number;
  magicStones?: number;
  items?: string[];
  experience?: number;
}

export interface GuildState {
  activeRumors: string[];
  acceptedQuests: string[];
  completedQuests: string[];
  availableExam: PromotionExam | null;
}
```

**✅ 完了チェック:**
- [ ] ItemTypes.ts が作成され、コンパイルエラーがない
- [ ] GuildTypes.ts が作成され、コンパイルエラーがない

---

### タスク 1.2: Context APIの実装

**優先度:** 🔴 最高

**1.2.1 ディレクトリ構造**

```bash
mkdir -p src/contexts
```

**1.2.2 GameStateContext.tsx**

```typescript
// src/contexts/GameStateContext.tsx

import React, { createContext, useContext, useState, ReactNode } from 'react';

export type GameScreen = 'camp' | 'battle' | 'shop' | 'blacksmith' | 'guild' | 'dungeon';
export type BattleMode = 'normal' | 'exam' | 'return_route' | null;
export type Depth = 1 | 2 | 3 | 4 | 5;

export interface BattleConfig {
  enemyIds: string[];
  backgroundType: 'dungeon' | 'arena' | 'guild';
  onWin?: () => void;
  onLose?: () => void;
}

export interface GameState {
  currentScreen: GameScreen;
  battleMode: BattleMode;
  depth: Depth;
  encounterCount: number;
  battleConfig?: BattleConfig;
}

interface GameStateContextValue {
  gameState: GameState;
  setGameState: React.Dispatch<React.SetStateAction<GameState>>;
  navigateTo: (screen: GameScreen) => void;
  startBattle: (config: BattleConfig, mode?: BattleMode) => void;
  returnToCamp: () => void;
}

const GameStateContext = createContext<GameStateContextValue | undefined>(undefined);

export const GameStateProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [gameState, setGameState] = useState<GameState>({
    currentScreen: 'camp',
    battleMode: null,
    depth: 1,
    encounterCount: 0,
  });

  const navigateTo = (screen: GameScreen) => {
    setGameState(prev => ({ ...prev, currentScreen: screen }));
  };

  const startBattle = (config: BattleConfig, mode: BattleMode = 'normal') => {
    setGameState(prev => ({
      ...prev,
      currentScreen: 'battle',
      battleMode: mode,
      battleConfig: config,
    }));
  };

  const returnToCamp = () => {
    setGameState(prev => ({
      ...prev,
      currentScreen: 'camp',
      battleMode: null,
      battleConfig: undefined,
    }));
  };

  return (
    <GameStateContext.Provider value={{ 
      gameState, 
      setGameState, 
      navigateTo, 
      startBattle, 
      returnToCamp 
    }}>
      {children}
    </GameStateContext.Provider>
  );
};

export const useGameState = () => {
  const context = useContext(GameStateContext);
  if (!context) {
    throw new Error('useGameState must be used within GameStateProvider');
  }
  return context;
};
```

**1.2.3 PlayerContext.tsx**

```typescript
// src/contexts/PlayerContext.tsx

import React, { createContext, useContext, useState, ReactNode } from 'react';
import { Player, Swordman_Status } from '../Character/data/PlayerData';

interface PlayerContextValue {
  player: Player;
  updatePlayer: (updates: Partial<Player>) => void;
  updateClassGrade: (newGrade: string) => void;
  addGold: (amount: number) => void;
  useGold: (amount: number) => boolean;
  updateHp: (newHp: number) => void;
  updateAp: (newAp: number) => void;
}

const PlayerContext = createContext<PlayerContextValue | undefined>(undefined);

export const PlayerProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  // 初期プレイヤーデータ（後でセーブデータから読み込む）
  const [player, setPlayer] = useState<Player>(Swordman_Status);

  const updatePlayer = (updates: Partial<Player>) => {
    setPlayer(prev => ({ ...prev, ...updates }));
  };

  const updateClassGrade = (newGrade: string) => {
    setPlayer(prev => ({ ...prev, classGrade: newGrade }));
  };

  const addGold = (amount: number) => {
    setPlayer(prev => ({ ...prev, gold: prev.gold + amount }));
  };

  const useGold = (amount: number): boolean => {
    if (player.gold < amount) return false;
    setPlayer(prev => ({ ...prev, gold: prev.gold - amount }));
    return true;
  };

  const updateHp = (newHp: number) => {
    setPlayer(prev => ({ ...prev, hp: Math.max(0, Math.min(newHp, prev.maxHp)) }));
  };

  const updateAp = (newAp: number) => {
    setPlayer(prev => ({ ...prev, ap: Math.max(0, Math.min(newAp, prev.maxAp)) }));
  };

  return (
    <PlayerContext.Provider value={{
      player,
      updatePlayer,
      updateClassGrade,
      addGold,
      useGold,
      updateHp,
      updateAp,
    }}>
      {children}
    </PlayerContext.Provider>
  );
};

export const usePlayer = () => {
  const context = useContext(PlayerContext);
  if (!context) {
    throw new Error('usePlayer must be used within PlayerProvider');
  }
  return context;
};
```

**1.2.4 InventoryContext.tsx**

```typescript
// src/contexts/InventoryContext.tsx

import React, { createContext, useContext, useState, ReactNode } from 'react';
import type { Item, EquipmentSlot } from '../types/ItemTypes';

interface EquippedItems {
  weapon?: string;
  armor?: string;
  helmet?: string;
  boots?: string;
  accessory1?: string;
  accessory2?: string;
}

interface InventoryContextValue {
  items: Item[];
  equipped: EquippedItems;
  addItem: (item: Item) => void;
  removeItem: (itemId: string) => void;
  equipItem: (itemId: string, slot: EquipmentSlot) => void;
  unequipItem: (slot: EquipmentSlot) => void;
  getMagicStones: () => number;
  useMagicStones: (amount: number) => boolean;
  getEquippedIds: () => string[];
}

const InventoryContext = createContext<InventoryContextValue | undefined>(undefined);

export const InventoryProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [items, setItems] = useState<Item[]>([]);
  const [equipped, setEquipped] = useState<EquippedItems>({});

  const addItem = (item: Item) => {
    setItems(prev => {
      // スタック可能アイテムの場合、既存アイテムに加算
      if (item.stackable) {
        const existingIndex = prev.findIndex(i => i.typeId === item.typeId);
        if (existingIndex !== -1) {
          const newItems = [...prev];
          const existing = newItems[existingIndex];
          newItems[existingIndex] = {
            ...existing,
            stackCount: (existing.stackCount || 1) + (item.stackCount || 1),
          };
          return newItems;
        }
      }
      return [...prev, item];
    });
  };

  const removeItem = (itemId: string) => {
    setItems(prev => prev.filter(item => item.id !== itemId));
  };

  const equipItem = (itemId: string, slot: EquipmentSlot) => {
    const item = items.find(i => i.id === itemId);
    if (!item || item.itemType !== 'equipment') return;
    
    setEquipped(prev => ({ ...prev, [slot]: itemId }));
  };

  const unequipItem = (slot: EquipmentSlot) => {
    setEquipped(prev => {
      const newEquipped = { ...prev };
      delete newEquipped[slot];
      return newEquipped;
    });
  };

  const getMagicStones = (): number => {
    return items
      .filter(item => item.itemType === 'magicStone')
      .reduce((sum, item) => {
        const value = item.magicStoneValue || 0;
        const count = item.stackCount || 1;
        return sum + (value * count);
      }, 0);
  };

  const useMagicStones = (amount: number): boolean => {
    const total = getMagicStones();
    if (total < amount) return false;

    // 魔石を消費（一番価値の低いものから）
    let remaining = amount;
    const magicStones = items
      .filter(item => item.itemType === 'magicStone')
      .sort((a, b) => (a.magicStoneValue || 0) - (b.magicStoneValue || 0));

    const newItems = [...items];
    for (const stone of magicStones) {
      if (remaining <= 0) break;

      const stoneValue = stone.magicStoneValue || 0;
      const stoneCount = stone.stackCount || 1;
      const totalValue = stoneValue * stoneCount;

      if (totalValue <= remaining) {
        // この魔石を全て消費
        remaining -= totalValue;
        const index = newItems.findIndex(i => i.id === stone.id);
        if (index !== -1) newItems.splice(index, 1);
      } else {
        // 一部だけ消費
        const needCount = Math.ceil(remaining / stoneValue);
        remaining = 0;
        const index = newItems.findIndex(i => i.id === stone.id);
        if (index !== -1) {
          newItems[index] = {
            ...newItems[index],
            stackCount: stoneCount - needCount,
          };
        }
      }
    }

    setItems(newItems);
    return true;
  };

  const getEquippedIds = (): string[] => {
    return Object.values(equipped).filter((id): id is string => id !== undefined);
  };

  return (
    <InventoryContext.Provider value={{
      items,
      equipped,
      addItem,
      removeItem,
      equipItem,
      unequipItem,
      getMagicStones,
      useMagicStones,
      getEquippedIds,
    }}>
      {children}
    </InventoryContext.Provider>
  );
};

export const useInventory = () => {
  const context = useContext(InventoryContext);
  if (!context) {
    throw new Error('useInventory must be used within InventoryProvider');
  }
  return context;
};
```

**1.2.5 App.tsx の更新**

```typescript
// src/App.tsx

import { GameStateProvider } from "./contexts/GameStateContext";
import { PlayerProvider } from "./contexts/PlayerContext";
import { InventoryProvider } from "./contexts/InventoryContext";
import BattleScreen from "./battles/battleUI/BattleScreen";
import BaseCamp from "./camps/campsUI/BaseCamp";
import { useGameState } from "./contexts/GameStateContext";

function AppContent() {
  const { gameState, setGameState } = useGameState();
  const { currentScreen, depth, battleMode, battleConfig } = gameState;

  return (
    <div style={{ width: "100%", height: "100%" }}>
      {currentScreen === "camp" && <BaseCamp />}
      {currentScreen === "battle" && (
        <BattleScreen 
          depth={depth} 
          onDepthChange={(newDepth) => 
            setGameState(prev => ({ ...prev, depth: newDepth }))
          }
          battleMode={battleMode || 'normal'}
          enemyIds={battleConfig?.enemyIds}
          onBattleEnd={(result) => {
            if (result === 'victory' && battleConfig?.onWin) {
              battleConfig.onWin();
            } else if (result === 'defeat' && battleConfig?.onLose) {
              battleConfig.onLose();
            }
          }}
        />
      )}
    </div>
  );
}

function App() {
  return (
    <GameStateProvider>
      <PlayerProvider>
        <InventoryProvider>
          <AppContent />
        </InventoryProvider>
      </PlayerProvider>
    </GameStateProvider>
  );
}

export default App;
```

**✅ 完了チェック:**
- [ ] 3つのContextが作成された
- [ ] App.tsxがProviderでラップされている
- [ ] コンパイルエラーがない
- [ ] 開発サーバーが正常に起動する

---

### タスク 1.3: Player型の修正

**優先度:** 🟡 中

```typescript
// src/Character/data/PlayerData.tsx (修正)

export interface Player {
  characterClass: CharacterClass;
  classGrade: string;              // ✅ 既存の文字列型を維持
  level: number;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  speed: number;
  initialEnergy: number;
  gold: number;
  
  deck: string[];
  equipped: string[];              // ✨ 新規追加
  
  statusEffects: Record<string, number>;
  title?: string[];
}

// ✅ 既存の初期データを修正
export const Swordman_Status: Player = {
  characterClass: "swordsman",
  classGrade: getSwordsmanTitle(0),
  level: 1,
  hp: 100,
  maxHp: 110,
  ap: 30,
  maxAp: 30,
  guard: 0,
  speed: 50,
  initialEnergy: 3,
  gold: 0,
  deck: [],
  equipped: [],                    // ✨ 新規追加
  statusEffects: {},
};

// Mage_Status, Summon_Status も同様に修正
```

**✅ 完了チェック:**
- [ ] Player型に `equipped: string[]` が追加された
- [ ] 初期データが更新された
- [ ] 既存コードで型エラーがないことを確認

---

## Phase 2: 試験用データの作成（Week 1: Day 4-5）

### タスク 2.1: GuildEnemyData.ts の作成

**優先度:** 🔴 最高

```bash
# ディレクトリ確認
ls src/domain/characters/enemy/data/
```

```typescript
// src/domain/characters/enemy/data/GuildEnemyData.ts

import type { Enemy } from '../../../../Character/data/EnemyData';

/**
 * 昇級試験専用の敵データ
 * 通常のダンジョンには出現しない
 */

// 剣士系の試験敵
export const TRAINING_DUMMY: Enemy = {
  id: 'exam_training_dummy',
  name: '訓練用人形',
  displayName: '訓練用人形',
  maxHp: 50,
  maxAp: 30,
  speed: 40,
  initialEnergy: 2,
  depth: 1,
  type: 'normal',
  pattern: [
    {
      turn: 1,
      actions: [
        {
          probability: 1.0,
          action: {
            name: '木刀での攻撃',
            type: 'attack',
            baseDamage: 8,
            displayIcon: '⚔️',
            priority: 0,
            energyCost: 1,
          }
        }
      ]
    }
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: []
  }
};

export const GUILD_INSTRUCTOR: Enemy = {
  id: 'exam_guild_instructor',
  name: 'ギルド教官',
  displayName: 'ギルド教官',
  maxHp: 120,
  maxAp: 60,
  speed: 55,
  initialEnergy: 3,
  depth: 2,
  type: 'elite',
  pattern: [
    {
      turn: 1,
      actions: [
        {
          probability: 0.7,
          action: {
            name: '教官の一撃',
            type: 'attack',
            baseDamage: 15,
            displayIcon: '⚔️',
            priority: 0,
            energyCost: 2,
          }
        },
        {
          probability: 0.3,
          action: {
            name: '防御の構え',
            type: 'buff',
            baseDamage: 0,
            guardGain: 20,
            displayIcon: '🛡️',
            priority: 1,
            energyCost: 1,
          }
        }
      ]
    }
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: []
  }
};

export const VETERAN_WARRIOR: Enemy = {
  id: 'exam_veteran_warrior',
  name: '歴戦の勇士',
  displayName: '歴戦の勇士',
  maxHp: 200,
  maxAp: 90,
  speed: 60,
  initialEnergy: 4,
  depth: 3,
  type: 'elite',
  pattern: [
    {
      turn: 1,
      actions: [
        {
          probability: 0.5,
          action: {
            name: '熟練の剣技',
            type: 'attack',
            baseDamage: 20,
            displayIcon: '⚔️',
            priority: 0,
            energyCost: 2,
          }
        },
        {
          probability: 0.3,
          action: {
            name: '連撃',
            type: 'attack',
            baseDamage: 12,
            displayIcon: '⚔️⚔️',
            priority: 0,
            energyCost: 3,
            applyDebuffs: [
              { type: 'bleed', stacks: 1, duration: 2 }
            ]
          }
        },
        {
          probability: 0.2,
          action: {
            name: '鉄壁の防御',
            type: 'buff',
            baseDamage: 0,
            guardGain: 30,
            displayIcon: '🛡️',
            priority: 1,
            energyCost: 2,
          }
        }
      ]
    }
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: []
  }
};

export const SWORD_SAINT_PHANTOM: Enemy = {
  id: 'exam_sword_saint_phantom',
  name: '剣聖の幻影',
  displayName: '剣聖の幻影',
  maxHp: 350,
  maxAp: 120,
  speed: 70,
  initialEnergy: 5,
  depth: 4,
  type: 'boss',
  pattern: [
    // フェーズ1 (HP 100%)
    {
      turn: 1,
      actions: [
        {
          probability: 0.6,
          action: {
            name: '神速の斬撃',
            type: 'attack',
            baseDamage: 25,
            displayIcon: '⚡⚔️',
            priority: 0,
            energyCost: 3,
          }
        },
        {
          probability: 0.4,
          action: {
            name: '剣気放出',
            type: 'attack',
            baseDamage: 18,
            displayIcon: '🌊',
            priority: 1,
            energyCost: 2,
            applyDebuffs: [
              { type: 'weakened', stacks: 1, duration: 2 }
            ]
          }
        }
      ]
    },
    // フェーズ2 (HP < 50%)
    {
      turn: 5,
      actions: [
        {
          probability: 1.0,
          action: {
            name: '奥義：無想剣',
            type: 'attack',
            baseDamage: 40,
            displayIcon: '💥',
            priority: 0,
            energyCost: 5,
            applyDebuffs: [
              { type: 'stunned', stacks: 1, duration: 1 }
            ]
          }
        }
      ]
    }
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: []
  }
};

// 魔術士系の試験敵（簡易版）
export const MAGIC_GOLEM: Enemy = {
  id: 'exam_magic_golem',
  name: '魔法の傀儡',
  displayName: '魔法の傀儡',
  maxHp: 45,
  maxAp: 25,
  speed: 35,
  initialEnergy: 2,
  depth: 1,
  type: 'normal',
  pattern: [
    {
      turn: 1,
      actions: [
        {
          probability: 1.0,
          action: {
            name: '魔力弾',
            type: 'attack',
            baseDamage: 10,
            displayIcon: '✨',
            priority: 0,
            energyCost: 1,
          }
        }
      ]
    }
  ],
  rewards: {
    gold: { min: 0, max: 0 },
    magicStones: []
  }
};

// エクスポート
export const GUILD_ENEMIES: Enemy[] = [
  TRAINING_DUMMY,
  GUILD_INSTRUCTOR,
  VETERAN_WARRIOR,
  SWORD_SAINT_PHANTOM,
  MAGIC_GOLEM,
  // ... 他のクラスの敵も追加
];
```

**✅ 完了チェック:**
- [ ] GuildEnemyData.tsが作成された
- [ ] 最低4体の敵データが定義された
- [ ] 既存のEnemy型と互換性がある

---

### タスク 2.2: PromotionData.ts の作成

```bash
mkdir -p src/camps/facilities/Guild/data
```

```typescript
// src/camps/facilities/Guild/data/PromotionData.ts

import type { PromotionExam } from '../../../../types/GuildTypes';

/**
 * 剣士系の昇級試験データ
 */
export const SWORDSMAN_EXAMS: PromotionExam[] = [
  {
    currentGrade: '見習い剣士',
    nextGrade: '剣士',
    requiredCardCount: 5,
    enemyId: 'exam_training_dummy',
    description: '訓練用人形を倒し、基本的な剣技を証明せよ',
    recommendations: {
      hp: 60,
      ap: 40
    },
    rewards: {
      statBonus: 'maxHP+10, 依頼枠+1',
      items: ['weapon_iron_sword']
    }
  },
  {
    currentGrade: '剣士',
    nextGrade: '剣豪',
    requiredCardCount: 15,
    enemyId: 'exam_guild_instructor',
    description: 'ギルド教官との模擬戦で実力を示せ',
    recommendations: {
      hp: 80,
      ap: 60
    },
    rewards: {
      statBonus: 'ATK+5%, 報酬ボーナス',
      items: ['weapon_steel_sword', 'armor_steel_plate']
    }
  },
  {
    currentGrade: '剣豪',
    nextGrade: '剣聖',
    requiredCardCount: 30,
    requiredGold: 500,
    enemyId: 'exam_veteran_warrior',
    description: '歴戦の勇士を打ち破り、剣の道を極めよ',
    recommendations: {
      hp: 120,
      ap: 80
    },
    rewards: {
      statBonus: '全ステータス+5%',
      items: ['weapon_mythril_sword', 'armor_mythril_plate']
    }
  },
  {
    currentGrade: '剣聖',
    nextGrade: '剣神',
    requiredCardCount: 50,
    requiredGold: 1000,
    enemyId: 'exam_sword_saint_phantom',
    description: '剣聖の幻影との死闘を制し、神の領域へ',
    recommendations: {
      hp: 150,
      ap: 100
    },
    rewards: {
      statBonus: '固有レジェンドスキル解放',
      items: ['weapon_legendary_excalibur']
    }
  }
];

/**
 * 魔術士系の昇級試験データ
 */
export const MAGE_EXAMS: PromotionExam[] = [
  {
    currentGrade: '見習い魔術士',
    nextGrade: '魔術士',
    requiredCardCount: 5,
    enemyId: 'exam_magic_golem',
    description: '魔法の傀儡を制御し、魔力の扱いを証明せよ',
    recommendations: {
      hp: 50,
      ap: 35
    },
    rewards: {
      statBonus: 'maxHP+8, maxAP+5',
      items: ['weapon_apprentice_staff']
    }
  },
  // ... 他の階級
];

/**
 * 召喚士系の昇級試験データ
 */
export const SUMMONER_EXAMS: PromotionExam[] = [
  // ... 定義
];

/**
 * クラスに応じた試験データを取得
 */
export function getExamsForClass(characterClass: string): PromotionExam[] {
  switch (characterClass) {
    case 'swordsman':
      return SWORDSMAN_EXAMS;
    case 'mage':
      return MAGE_EXAMS;
    case 'summoner':
      return SUMMONER_EXAMS;
    default:
      return [];
  }
}

/**
 * 現在のグレードから次の試験を取得
 */
export function getNextExam(
  characterClass: string, 
  currentGrade: string
): PromotionExam | null {
  const exams = getExamsForClass(characterClass);
  return exams.find(exam => exam.currentGrade === currentGrade) || null;
}
```

**✅ 完了チェック:**
- [ ] PromotionData.tsが作成された
- [ ] 3クラス × 4段階 = 12個の試験データが定義された
- [ ] ヘルパー関数が実装された

---

## Phase 3: Guild UIの実装（Week 2: Day 1-5）

### タスク 3.1: Guildコンポーネントの骨組み

```bash
mkdir -p src/camps/facilities/Guild
```

```typescript
// src/camps/facilities/Guild/Guild.tsx

import { useState } from 'react';
import { usePlayer } from '../../../contexts/PlayerContext';
import { useGameState } from '../../../contexts/GameStateContext';
import PromotionTab from './PromotionTab';
import RumorsTab from './RumorsTab';
import QuestsTab from './QuestsTab';
import './Guild.css';

type GuildTab = 'rumors' | 'quests' | 'promotion';

const Guild: React.FC = () => {
  const [selectedTab, setSelectedTab] = useState<GuildTab>('promotion');
  const { returnToCamp } = useGameState();

  return (
    <div className="guild-screen">
      <header className="guild-header">
        <h1>🍺 ギルド - 酒場</h1>
      </header>

      <nav className="guild-tabs">
        <button
          className={selectedTab === 'rumors' ? 'active' : ''}
          onClick={() => setSelectedTab('rumors')}
        >
          噂話
        </button>
        <button
          className={selectedTab === 'quests' ? 'active' : ''}
          onClick={() => setSelectedTab('quests')}
        >
          依頼
        </button>
        <button
          className={selectedTab === 'promotion' ? 'active' : ''}
          onClick={() => setSelectedTab('promotion')}
        >
          昇級試験
        </button>
      </nav>

      <div className="guild-content">
        {selectedTab === 'rumors' && <RumorsTab />}
        {selectedTab === 'quests' && <QuestsTab />}
        {selectedTab === 'promotion' && <PromotionTab />}
      </div>

      <button className="back-button" onClick={returnToCamp}>
        キャンプに戻る
      </button>
    </div>
  );
};

export default Guild;
```

**✅ 完了チェック:**
- [ ] Guild.tsxが作成された
- [ ] タブ切り替えが動作する
- [ ] 戻るボタンが動作する

---

### タスク 3.2: PromotionTab の実装

```typescript
// src/camps/facilities/Guild/PromotionTab.tsx

import { usePlayer } from '../../../contexts/PlayerContext';
import { useGameState } from '../../../contexts/GameStateContext';
import { useInventory } from '../../../contexts/InventoryContext';
import { getNextExam } from './data/PromotionData';
import './PromotionTab.css';

const PromotionTab: React.FC = () => {
  const { player, updateClassGrade, updatePlayer } = usePlayer();
  const { startBattle, returnToCamp } = useGameState();
  const { addItem } = useInventory();

  const exam = getNextExam(player.characterClass, player.classGrade);

  if (!exam) {
    return (
      <div className="promotion-unavailable">
        <h2>おめでとうございます！</h2>
        <p>あなたは既に最高位の称号を得ています</p>
        <p className="current-grade">{player.classGrade}</p>
      </div>
    );
  }

  // 受験条件チェック
  const cardCount = player.deck.length;
  const meetsCardRequirement = cardCount >= exam.requiredCardCount;
  const meetsGoldRequirement = exam.requiredGold
    ? player.gold >= exam.requiredGold
    : true;

  const canTakeExam = meetsCardRequirement && meetsGoldRequirement;

  const handleStartExam = () => {
    if (!canTakeExam) return;

    // 試験戦闘を開始
    startBattle(
      {
        enemyIds: [exam.enemyId],
        backgroundType: 'arena',
        onWin: () => handleExamPassed(),
        onLose: () => handleExamFailed(),
      },
      'exam'
    );
  };

  const handleExamPassed = () => {
    // 昇格処理
    updateClassGrade(exam.nextGrade);

    // ステータスボーナス適用（簡易版）
    // TODO: statBonusを解析して適用

    // アイテム報酬
    if (exam.rewards.items) {
      exam.rewards.items.forEach(itemId => {
        // TODO: itemIdからItemオブジェクトを生成
        // addItem(createItemFromId(itemId));
      });
    }

    // キャンプに戻る
    returnToCamp();
  };

  const handleExamFailed = () => {
    // HP1でキャンプに戻る
    updatePlayer({ hp: 1 });
    returnToCamp();
  };

  return (
    <div className="promotion-tab">
      {/* 現在のグレードと次のグレード */}
      <div className="grade-display">
        <div className="current-grade-box">
          <span className="grade-label">現在</span>
          <span className="grade-name">{exam.currentGrade}</span>
        </div>
        <div className="arrow">→</div>
        <div className="next-grade-box">
          <span className="grade-label">次の</span>
          <span className="grade-name">{exam.nextGrade}</span>
        </div>
      </div>

      {/* 受験条件 */}
      <section className="exam-requirements">
        <h3>◆ 受験条件</h3>
        <div className={`requirement ${meetsCardRequirement ? 'met' : 'unmet'}`}>
          [{meetsCardRequirement ? '✓' : '✗'}] カード所持数: {cardCount}/
          {exam.requiredCardCount}枚
        </div>
        {exam.requiredGold && (
          <div className={`requirement ${meetsGoldRequirement ? 'met' : 'unmet'}`}>
            [{meetsGoldRequirement ? '✓' : '✗'}] Gold所持: {player.gold}/
            {exam.requiredGold}G
          </div>
        )}
      </section>

      {/* 試験内容 */}
      <section className="exam-details">
        <h3>◆ 試験内容</h3>
        <p>{exam.description}</p>
        <div className="recommendations">
          <p>推奨HP: {exam.recommendations.hp}以上</p>
          <p>推奨AP: {exam.recommendations.ap}以上</p>
        </div>
      </section>

      {/* 合格報酬 */}
      <section className="exam-rewards">
        <h3>◆ 合格報酬</h3>
        <ul>
          <li>称号: {exam.nextGrade}</li>
          <li>{exam.rewards.statBonus}</li>
          {exam.rewards.items && <li>レア装備 x{exam.rewards.items.length}</li>}
        </ul>
      </section>

      {/* 警告 */}
      <div className="exam-warning">
        ⚠️ 試験を開始すると戦闘になります。装備を整えてください！
      </div>

      {/* 開始ボタン */}
      <button
        className="start-exam-button"
        disabled={!canTakeExam}
        onClick={handleStartExam}
      >
        {canTakeExam ? '試験を開始する' : '条件を満たしていません'}
      </button>
    </div>
  );
};

export default PromotionTab;
```

**✅ 完了チェック:**
- [ ] PromotionTab.tsxが作成された
- [ ] 受験条件のチェックが動作する
- [ ] ボタンの有効/無効が切り替わる

---

### タスク 3.3: プレースホルダータブの作成

```typescript
// src/camps/facilities/Guild/RumorsTab.tsx
const RumorsTab: React.FC = () => {
  return (
    <div className="rumors-tab">
      <h2>噂話</h2>
      <p className="coming-soon">Coming Soon...</p>
      <p>魔石を使って探索にバフを付与できます</p>
    </div>
  );
};

export default RumorsTab;
```

```typescript
// src/camps/facilities/Guild/QuestsTab.tsx
const QuestsTab: React.FC = () => {
  return (
    <div className="quests-tab">
      <h2>依頼</h2>
      <p className="coming-soon">Coming Soon...</p>
      <p>デイリー/ウィークリークエストを受注できます</p>
    </div>
  );
};

export default QuestsTab;
```

---

### タスク 3.4: BaseCampからGuildへの遷移

```typescript
// src/camps/campsUI/BaseCamp.tsx (修正)

import { useGameState } from '../../contexts/GameStateContext';

const BaseCamp = () => {
  const { navigateTo } = useGameState();
  const [selectedFacility, setSelectedFacility] = useState<FacilityType | null>(null);

  const facilities: FacilityCardProps[] = [
    // ... 既存の施設
    {
      type: "tavern",
      name: "酒場",
      description: "噂話、依頼、昇級試験",
      icon: "🍺",
      isUnlocked: true,  // ✅ 解放する
      onEnter: () => navigateTo('guild'),  // ✅ Guildに遷移
    },
  ];

  // ... 残りのコード
};
```

**✅ 完了チェック:**
- [ ] BaseCampから酒場をクリックできる
- [ ] Guildコンポーネントが表示される
- [ ] 戻るボタンでBaseCampに戻れる

---

## Phase 4: 戦闘システム統合（Week 3-4）

### タスク 4.1: BattleScreen の拡張

```typescript
// src/battles/battleUI/BattleScreen.tsx (修正)

interface BattleScreenProps {
  depth: Depth;
  onDepthChange: (depth: Depth) => void;
  battleMode?: 'normal' | 'exam' | 'return_route';
  enemyIds?: string[];
  onBattleEnd?: (result: 'victory' | 'defeat') => void;
}

const BattleScreen: React.FC<BattleScreenProps> = ({
  depth,
  onDepthChange,
  battleMode = 'normal',
  enemyIds,
  onBattleEnd
}) => {
  // ... 既存のロジック

  // 敵の初期化を分岐
  useEffect(() => {
    if (battleMode === 'exam' && enemyIds) {
      // 試験モード：指定された敵を生成
      const examEnemies = enemyIds.map(id => {
        const enemyData = GUILD_ENEMIES.find(e => e.id === id);
        return createEnemyState(enemyData);
      });
      setEnemies(examEnemies);
    } else {
      // 通常モード：既存のロジック
      const { enemies: randomEnemies } = selectRandomEnemy(depth, 'normal');
      setEnemies(randomEnemies);
    }
  }, [battleMode, enemyIds, depth]);

  // 勝敗判定の分岐
  useEffect(() => {
    if (aliveEnemies.length === 0 && !battleResult) {
      setBattleResult('victory');
      if (onBattleEnd) onBattleEnd('victory');
    }
    
    if (playerHp <= 0 && !battleResult) {
      setBattleResult('defeat');
      if (onBattleEnd) onBattleEnd('defeat');
    }
  }, [aliveEnemies, playerHp, battleResult, onBattleEnd]);

  // ... 残りのコード
};
```

**✅ 完了チェック:**
- [ ] battleModeパラメータが追加された
- [ ] examモードで指定敵が出現する
- [ ] 勝敗時にonBattleEndが呼ばれる

---

## テスト手順

### 基本動作テスト

```
□ Context統合テスト
  □ GameStateContextの状態遷移
  □ PlayerContextの更新
  □ InventoryContextの操作

□ 画面遷移テスト
  □ BaseCamp → Guild
  □ Guild → タブ切り替え
  □ Guild → 試験 → 戦闘 → BaseCamp

□ 昇級試験テスト
  □ 条件未達時のボタン無効化
  □ 試験開始
  □ 敵との戦闘
  □ 勝利時の昇格処理
  □ 敗北時のHP1帰還
```

---

## トラブルシューティング

### よくあるエラー

**1. Context is undefined**
```
原因: Providerの外でhookを使用している
解決: App.tsxでProviderが正しく配置されているか確認
```

**2. Type error on Player.equipped**
```
原因: 既存コードがequipment配列を前提としている
解決: 互換性レイヤーを追加するか、該当コードを修正
```

**3. 試験戦闘が開始しない**
```
原因: GameStateContextの更新が反映されていない
解決: startBattle関数が正しく呼ばれているか確認
```

---

## 次のステップ

Phase 4完了後:
1. 噂話システムの実装
2. クエストシステムの実装
3. 装備品質システムの拡張
4. セーブ/ロード機能の統合

---

**END OF IMPLEMENTATION GUIDE**
