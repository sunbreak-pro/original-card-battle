# ギルド施設詳細設計書 (GUILD_DESIGN_V2.1)

## 更新履歴
- V2.1: Item型システム統合、文字列グレード対応、Context API統合

---

## 1. 施設の概要

ギルドは冒険者の社会的地位を高め、情報を得て、日々の糧（クエスト報酬）を得る場所です。
従来の「カード収集数による自動昇級」を廃止し、**「昇級試験（Promotion Exam）」**システムを導入します。

### 主な機能

1. **噂話 (Rumors)**: 魔石を支払い、次回の探索に有利な効果を付与
2. **依頼 (Quests)**: デイリー/ウィークリーの討伐・収集クエスト
3. **昇級試験 (Promotion Exams)**: [NEW] クラスグレードを上げるための特別な戦闘イベント

---

## 2. 昇級試験 (Promotion Exams)

### 2.1 基本仕様

プレイヤーの「格（Class Grade）」を上げるための試験。

**受験条件:**
- 現在のグレードに応じた「カード収集数」または「特定の実績」を満たしていること
- 例: 「見習い剣士」→「剣士」への昇格条件 = 所持カード5枚以上

**試験内容:**
- ギルドマスターまたは試験官との模擬戦
- 指定された討伐対象（アリーナ形式）との戦闘
- 敗北してもゲームオーバーにはならず、HP1でキャンプに戻される（再挑戦可能）

**合格報酬:**
- 称号昇格: classGrade が上昇（文字列更新）
- ステータス永続強化: 称号に応じたパッシブ効果（HP+、ATK+など）
- 特別報酬: レアリティの高い装備

---

### 2.2 昇級ランク定義

#### 2.2.1 剣士系（Swordsman）

| グレード | 称号名 | 受験条件 | 試験相手 | 合格恩恵 |
|---------|--------|----------|----------|----------|
| Grade 0 | 見習い剣士 | 初期状態 | - | - |
| Grade 1 | 剣士 | カード 5枚 | 訓練用人形 (Lv5) | maxHP+10, 依頼枠+1 |
| Grade 2 | 剣豪 | カード 15枚 | ギルド教官 (Lv15) | ATK+5%, 報酬ボーナス |
| Grade 3 | 剣聖 | カード 30枚 | 歴戦の勇士 (Lv30) | 全ステータス+5% |
| Grade 4 | 剣神 | カード 50枚 | 剣聖の幻影 (Boss級) | 固有レジェンド装備 |

#### 2.2.2 魔術士系（Mage）

| グレード | 称号名 | 受験条件 | 試験相手 | 合格恩恵 |
|---------|--------|----------|----------|----------|
| Grade 0 | 見習い魔術士 | 初期状態 | - | - |
| Grade 1 | 魔術士 | カード 5枚 | 魔法の傀儡 (Lv5) | maxHP+8, maxAP+5 |
| Grade 2 | 魔導師 | カード 15枚 | 宮廷魔導士 (Lv15) | 魔法ダメージ+5% |
| Grade 3 | 大魔導師 | カード 30枚 | 古代の賢者 (Lv30) | 全ステータス+5% |
| Grade 4 | 魔神 | カード 50枚 | 魔導王の影 (Boss級) | 固有レジェンド装備 |

#### 2.2.3 召喚士系（Summoner）

| グレード | 称号名 | 受験条件 | 試験相手 | 合格恩恵 |
|---------|--------|----------|----------|----------|
| Grade 0 | 見習い召喚士 | 初期状態 | - | - |
| Grade 1 | 召喚士 | カード 5枚 | 霊体の番人 (Lv5) | 召喚コスト-1 |
| Grade 2 | 上級召喚士 | カード 15枚 | 契約の守護者 (Lv15) | 召喚物HP+10% |
| Grade 3 | 召喚師 | カード 30枚 | 次元の門番 (Lv30) | 全ステータス+5% |
| Grade 4 | 召喚神 | カード 50枚 | 原初の召喚獣 (Boss級) | 固有レジェンド装備 |

---

### 2.3 報酬ボーナス解放

**Grade 2以降の特典:**
- ギルドでの報酬装備にレアリティボーナス
- クエスト報酬の金額増加
- 高難易度依頼の解放

---

## 3. 噂話 (Rumors)

### 3.1 基本仕様

**コスト:** 魔石（Magic Stone）を消費
- Gold（金貨）とは明確に区別
- UI表現: 紫色の怪しい輝きを持つアイコン

**効果:** 次回の探索にバフを付与（1回限り）

### 3.2 噂の種類（例）

```typescript
interface Rumor {
  id: string;
  name: string;
  description: string;
  cost: number;              // 魔石コスト
  effect: RumorEffect;
  rarity: 'common' | 'rare' | 'epic';
}

type RumorEffect = 
  | { type: 'elite_rate', value: number }      // エリート敵出現率UP
  | { type: 'shop_discount', value: number }   // ショップ割引
  | { type: 'treasure_rate', value: number }   // 宝箱出現率UP
  | { type: 'start_bonus', bonus: string };    // 開始時ボーナス
```

**噂の例:**
1. **「魔物の巣の噂」** (10魔石)
   - エリート敵の出現率UP（ハイリスク・ハイリターン）
   
2. **「幸運の商人の噂」** (20魔石)
   - ダンジョン内ショップの割引率UP
   
3. **「古の宝の噂」** (15魔石)
   - 宝箱部屋の出現率+20%

---

## 4. 依頼 (Quests)

### 4.1 基本仕様

**クエストタイプ:**
- デイリークエスト: 毎日更新
- ウィークリークエスト: 週1回更新

**報酬:**
- Gold（金貨）
- 魔石（Magic Stone）
- 消耗品アイテム

**解放条件:**
- 昇級すると高難易度・高報酬の依頼が解放

### 4.2 クエストデータ構造

```typescript
interface Quest {
  id: string;
  title: string;
  description: string;
  type: 'daily' | 'weekly';
  requiredGrade: string;         // "剣士", "魔術士"等
  
  objectives: QuestObjective[];
  rewards: QuestReward;
  
  isActive: boolean;
  isCompleted: boolean;
  progress: number;
}

interface QuestObjective {
  type: 'defeat' | 'collect' | 'explore';
  target: string;                // 敵ID、アイテムID、深度等
  required: number;
  current: number;
}

interface QuestReward {
  gold?: number;
  magicStones?: number;
  items?: string[];              // アイテムID配列
  experience?: number;
}
```

**クエスト例:**
```typescript
{
  id: "daily_001",
  title: "腐敗の野犬を討伐せよ",
  description: "深度1に出現する腐敗の野犬を3体討伐する",
  type: "daily",
  requiredGrade: "見習い剣士",
  objectives: [
    {
      type: "defeat",
      target: "corrupted_hound",
      required: 3,
      current: 0
    }
  ],
  rewards: {
    gold: 50,
    magicStones: 3,
    items: ["potion_001"]
  }
}
```

---

## 5. UI/UX デザイン

### 5.1 画面レイアウト

```
┌────────────────────────────────────────────┐
│  🍺 ギルド - 酒場                          │
├────────────────────────────────────────────┤
│                                            │
│  [噂話] [依頼] [昇級試験] ← タブ切り替え  │
│  ═════  ────  ────────                     │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │                                      │  │
│  │    選択中のタブの内容                │  │
│  │                                      │  │
│  │                                      │  │
│  │                                      │  │
│  └──────────────────────────────────────┘  │
│                                            │
│  [キャンプに戻る]                          │
│                                            │
└────────────────────────────────────────────┘
```

### 5.2 昇級試験タブのUI

```
┌────────────────────────────────────────────┐
│  昇級試験                                   │
├────────────────────────────────────────────┤
│                                            │
│  ┌─────────┐         ┌─────────┐          │
│  │ 現在    │   →    │ 次の    │          │
│  │  剣士   │         │  剣豪   │          │
│  └─────────┘         └─────────┘          │
│                                            │
│  ◆ 受験条件                                │
│   [✓] カード所持数: 15/15枚                │
│   [✓] Gold所持: 500/500G                   │
│                                            │
│  ◆ 試験内容                                │
│   対戦相手: ギルド教官 (Lv15)              │
│   推奨HP: 60以上                           │
│   推奨AP: 50以上                           │
│                                            │
│  ◆ 合格報酬                                │
│   - 称号: 剣豪                             │
│   - ATK +5%                                │
│   - レア装備 x1                            │
│                                            │
│  ⚠️ 試験を開始すると戦闘になります。        │
│     装備を整えてください！                  │
│                                            │
│  [試験を開始する]  [戻る]                  │
│                                            │
└────────────────────────────────────────────┘
```

### 5.3 戦闘への遷移演出

**試験開始時:**
1. 「試験開始」ボタンを押す
2. 画面が暗転
3. サイレンやドラのような音が鳴る
4. 通常ダンジョンとは異なる背景（闘技場や道場）で戦闘開始

**勝利時:**
1. ファンファーレと共に「合格」の文字
2. 報酬画面表示
3. キャンプへ戻ると昇格演出

**敗北時:**
1. 「まだまだ修行が足りないようだ...」
2. HP1の状態でキャンプに帰還
3. 再挑戦可能

---

## 6. データ構造定義

### 6.1 GuildTypes.ts

```typescript
// src/types/GuildTypes.ts (新規作成)

/**
 * 昇級試験データ
 */
export interface PromotionExam {
  currentGrade: string;          // "見習い剣士", "剣士" etc
  nextGrade: string;             // "剣士", "剣豪" etc
  requiredCardCount: number;     // 必要カード数
  requiredGold?: number;         // 必要Gold（オプション）
  enemyId: string;               // 試験相手の敵ID
  description: string;
  recommendations: {
    hp: number;
    ap: number;
  };
  rewards: {
    statBonus: string;           // "maxHP+10", "ATK+5%" etc
    items?: string[];            // 報酬アイテムID
  };
}

/**
 * 噂データ
 */
export interface Rumor {
  id: string;
  name: string;
  description: string;
  cost: number;                  // 魔石コスト
  effect: RumorEffect;
  rarity: 'common' | 'rare' | 'epic';
  icon: string;
}

export type RumorEffect = 
  | { type: 'elite_rate'; value: number }
  | { type: 'shop_discount'; value: number }
  | { type: 'treasure_rate'; value: number }
  | { type: 'start_bonus'; bonus: string };

/**
 * クエストデータ
 */
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

/**
 * ギルド状態
 */
export interface GuildState {
  activeRumors: string[];        // 適用中の噂IDリスト
  acceptedQuests: string[];      // 受注中のクエストID
  completedQuests: string[];     // 完了済みクエスト履歴
  availableExam: PromotionExam | null;  // 受験可能な試験
}
```

---

## 7. 実装手順書

### Phase 1: データと型の準備

**1.1 型定義作成**
```
□ src/types/GuildTypes.ts 作成
  □ PromotionExam型
  □ Rumor型
  □ Quest型
  □ GuildState型
```

**1.2 試験データ作成**
```
□ src/camps/facilities/Guild/data/PromotionData.ts 作成
  □ 剣士系の試験4段階
  □ 魔術士系の試験4段階
  □ 召喚士系の試験4段階
```

**1.3 試験用敵データ**
```
□ src/domain/characters/enemy/data/GuildEnemyData.ts 作成
  □ 訓練用人形 (Lv5)
  □ ギルド教官 (Lv15)
  □ 歴戦の勇士 (Lv30)
  □ 剣聖の幻影 (Boss級)
  □ 各クラス用の敵（魔法の傀儡、霊体の番人等）
```

---

### Phase 2: Context統合

**2.1 GuildContextの作成**
```typescript
// src/contexts/GuildContext.tsx

interface GuildContextValue {
  guildState: GuildState;
  
  // 噂関連
  activeRumors: Rumor[];
  activateRumor: (rumorId: string) => boolean;
  clearRumors: () => void;
  
  // クエスト関連
  acceptedQuests: Quest[];
  acceptQuest: (questId: string) => boolean;
  updateQuestProgress: (questId: string, progress: Partial<QuestObjective>) => void;
  completeQuest: (questId: string) => void;
  
  // 昇級試験関連
  availableExam: PromotionExam | null;
  checkExamEligibility: () => PromotionExam | null;
  startExam: (exam: PromotionExam) => void;
}
```

**2.2 PlayerContextとの連携**
```typescript
// 昇級試験合格時の処理
const handleExamPassed = (exam: PromotionExam) => {
  // PlayerContextのclassGradeを更新
  updatePlayer({ 
    classGrade: exam.nextGrade 
  });
  
  // ステータスボーナス適用
  applyStatBonus(exam.rewards.statBonus);
  
  // アイテム報酬付与
  if (exam.rewards.items) {
    exam.rewards.items.forEach(itemId => {
      addItem(createItemFromId(itemId));
    });
  }
};
```

---

### Phase 3: ギルドUIコンポーネントの実装

**3.1 Guild.tsx の骨組み**
```typescript
// src/camps/facilities/Guild/Guild.tsx

type GuildTab = 'rumors' | 'quests' | 'promotion';

const Guild: React.FC = () => {
  const [selectedTab, setSelectedTab] = useState<GuildTab>('promotion');
  const { guildState } = useGuild();
  const { player } = usePlayer();
  
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
      
      <button className="back-button" onClick={onBack}>
        キャンプに戻る
      </button>
    </div>
  );
};
```

**3.2 PromotionTab.tsx の実装**
```typescript
// src/camps/facilities/Guild/PromotionTab.tsx

const PromotionTab: React.FC = () => {
  const { player, updatePlayer } = usePlayer();
  const { availableExam, checkExamEligibility, startExam } = useGuild();
  const { items } = useInventory();
  const { setGameState } = useGameState();
  
  const exam = checkExamEligibility();
  
  if (!exam) {
    return (
      <div className="promotion-unavailable">
        <p>現在受験可能な試験はありません</p>
        <p>カードを集めて次の昇級を目指しましょう</p>
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
    
    // GameStateContextを更新して試験戦闘へ遷移
    setGameState({
      currentScreen: 'battle',
      battleMode: 'exam',
      depth: 1,
      encounterCount: 0,
      battleConfig: {
        enemyIds: [exam.enemyId],
        backgroundType: 'arena',
        onWin: () => handleExamPassed(exam),
        onLose: () => handleExamFailed(),
      }
    });
  };
  
  return (
    <div className="promotion-tab">
      {/* 現在のグレードと次のグレードの表示 */}
      <div className="grade-display">
        <div className="current-grade">
          <span className="grade-label">現在</span>
          <span className="grade-name">{exam.currentGrade}</span>
        </div>
        <div className="arrow">→</div>
        <div className="next-grade">
          <span className="grade-label">次の</span>
          <span className="grade-name">{exam.nextGrade}</span>
        </div>
      </div>
      
      {/* 受験条件 */}
      <section className="exam-requirements">
        <h3>◆ 受験条件</h3>
        <div className={`requirement ${meetsCardRequirement ? 'met' : 'unmet'}`}>
          [{meetsCardRequirement ? '✓' : '✗'}] カード所持数: {cardCount}/{exam.requiredCardCount}枚
        </div>
        {exam.requiredGold && (
          <div className={`requirement ${meetsGoldRequirement ? 'met' : 'unmet'}`}>
            [{meetsGoldRequirement ? '✓' : '✗'}] Gold所持: {player.gold}/{exam.requiredGold}G
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
```

**3.3 RumorsTab.tsx と QuestsTab.tsx**
```typescript
// 簡易実装（Phase 3では基本UIのみ）

const RumorsTab: React.FC = () => {
  return (
    <div className="rumors-tab">
      <p className="coming-soon">Coming Soon...</p>
    </div>
  );
};

const QuestsTab: React.FC = () => {
  return (
    <div className="quests-tab">
      <p className="coming-soon">Coming Soon...</p>
    </div>
  );
};
```

---

### Phase 4: 戦闘システムとの統合

**4.1 BattleScreen.tsxの拡張**
```typescript
// src/battles/battleUI/BattleScreen.tsx

interface BattleScreenProps {
  depth: Depth;
  onDepthChange: (depth: Depth) => void;
  battleMode?: 'normal' | 'exam' | 'return_route';  // ✨ 追加
  enemyIds?: string[];                              // ✨ 追加
  onBattleEnd?: (result: 'victory' | 'defeat') => void;  // ✨ 追加
}

const BattleScreen: React.FC<BattleScreenProps> = ({
  depth,
  onDepthChange,
  battleMode = 'normal',
  enemyIds,
  onBattleEnd
}) => {
  // battleModeによって処理を分岐
  
  if (battleMode === 'exam') {
    // 昇級試験モード
    // - enemyIdsから敵を生成
    // - 深度進行なし
    // - 勝利時: onBattleEnd('victory')
    // - 敗北時: HP1でonBattleEnd('defeat')
  }
  
  // ... 既存の実装
};
```

**4.2 勝敗判定とリザルト処理**
```typescript
// 昇級試験の勝利処理
if (battleMode === 'exam' && battleResult === 'victory') {
  return (
    <ExamVictoryScreen
      onContinue={() => {
        // GameStateContextを更新してキャンプへ
        setGameState(prev => ({
          ...prev,
          currentScreen: 'camp',
          battleMode: null
        }));
        
        // 合格処理（Context経由で実行済み）
      }}
      exam={currentExam}
    />
  );
}

// 昇級試験の敗北処理
if (battleMode === 'exam' && battleResult === 'defeat') {
  return (
    <ExamDefeatScreen
      onRetry={() => {
        // 試験を再開
        resetBattle();
      }}
      onReturn={() => {
        // HP1でキャンプへ
        updatePlayer({ hp: 1 });
        setGameState(prev => ({
          ...prev,
          currentScreen: 'camp',
          battleMode: null
        }));
      }}
    />
  );
}
```

---

### Phase 5: 魔石システムの実装（噂話用）

**5.1 MagicStoneData.ts**
```typescript
// src/items/data/MagicStoneData.ts

import type { Item } from '../../types/ItemTypes';

export const MAGIC_STONE_ITEMS: Item[] = [
  {
    id: 'magic_stone_small',
    typeId: 'magic_stone_small',
    name: '魔石（極小）',
    description: 'わずかな魔力を帯びた小さな石',
    itemType: 'magicStone',
    icon: '💎',
    magicStoneValue: 1,
    rarity: 'common',
    sellPrice: 10,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1
  },
  {
    id: 'magic_stone_medium',
    typeId: 'magic_stone_medium',
    name: '魔石（小）',
    description: 'ほのかに光る魔石',
    itemType: 'magicStone',
    icon: '💎',
    magicStoneValue: 5,
    rarity: 'uncommon',
    sellPrice: 40,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1
  },
  // ... 他のサイズの魔石
];
```

---

## 8. CSS設計

### 8.1 Guild.css

```css
/* src/camps/facilities/Guild/Guild.css */

.guild-screen {
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #1a0f1a 0%, #2a1a2a 100%);
  display: flex;
  flex-direction: column;
  padding: 2rem;
  color: #e0d0f0;
}

.guild-header {
  text-align: center;
  margin-bottom: 2rem;
}

.guild-header h1 {
  font-size: 3rem;
  text-shadow: 0 0 20px rgba(138, 98, 158, 0.8);
}

.guild-tabs {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-bottom: 2rem;
}

.guild-tabs button {
  padding: 1rem 2rem;
  background: rgba(138, 98, 158, 0.2);
  border: 2px solid rgba(138, 98, 158, 0.5);
  border-radius: 8px;
  color: #e0d0f0;
  font-size: 1.2rem;
  cursor: pointer;
  transition: all 0.3s ease;
}

.guild-tabs button.active {
  background: rgba(138, 98, 158, 0.8);
  border-color: rgba(138, 98, 158, 1);
}

.guild-content {
  flex: 1;
  background: rgba(0, 0, 0, 0.3);
  border: 2px solid rgba(138, 98, 158, 0.3);
  border-radius: 12px;
  padding: 2rem;
  overflow-y: auto;
}

/* PromotionTab specific styles */
.promotion-tab {
  max-width: 800px;
  margin: 0 auto;
}

.grade-display {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 2rem;
  margin-bottom: 3rem;
}

.current-grade, .next-grade {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 2rem;
  background: rgba(138, 98, 158, 0.2);
  border: 2px solid rgba(138, 98, 158, 0.5);
  border-radius: 12px;
}

.grade-name {
  font-size: 2rem;
  font-weight: bold;
  margin-top: 0.5rem;
}

.exam-requirements,
.exam-details,
.exam-rewards {
  margin-bottom: 2rem;
}

.requirement {
  margin: 0.5rem 0;
  font-size: 1.1rem;
}

.requirement.met {
  color: #4ade80;
}

.requirement.unmet {
  color: #ef4444;
}

.exam-warning {
  background: rgba(255, 100, 100, 0.2);
  border: 2px solid rgba(255, 100, 100, 0.5);
  border-radius: 8px;
  padding: 1rem;
  text-align: center;
  margin: 2rem 0;
  color: #fca5a5;
}

.start-exam-button {
  width: 100%;
  padding: 1.5rem;
  font-size: 1.3rem;
  font-weight: bold;
  background: linear-gradient(135deg, #9a4ad9 0%, #6a2a9a 100%);
  border: 3px solid #c084fc;
  border-radius: 12px;
  color: white;
  cursor: pointer;
  transition: all 0.3s ease;
}

.start-exam-button:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(154, 74, 217, 0.6);
}

.start-exam-button:disabled {
  background: rgba(100, 100, 100, 0.3);
  border-color: rgba(100, 100, 100, 0.5);
  color: rgba(200, 200, 200, 0.5);
  cursor: not-allowed;
}
```

---

## 9. テスト項目

### 9.1 昇級試験システム
```
□ 受験条件の判定
  □ カード枚数チェック
  □ Gold所持チェック
  □ 条件未達時のボタン無効化

□ 試験戦闘の開始
  □ 正しい敵が出現
  □ アリーナ背景の表示
  □ 深度が進行しない

□ 合格処理
  □ classGradeの更新
  □ ステータスボーナスの適用
  □ 報酬アイテムの付与
  □ キャンプへの帰還

□ 不合格処理
  □ HP1での帰還
  □ 再挑戦可能
  □ classGradeは変わらない
```

### 9.2 Context統合
```
□ GuildContextの動作
  □ 試験受験資格の判定
  □ 状態の永続化

□ PlayerContextとの連携
  □ classGrade更新
  □ ステータス変更

□ InventoryContextとの連携
  □ 魔石の計算
  □ アイテム報酬の付与
```

---

## 10. 参照ドキュメント

```
BASE_CAMP_DESIGN_V1
└── GUILD_DESIGN_V2.1 [本文書]
    ├── GuildEnemyData.ts [試験用敵データ]
    ├── PromotionData.ts [昇級試験データ]
    └── battle_logic.md [戦闘システム]
```

---

**次のステップ:** 実装手順書の詳細版を作成
