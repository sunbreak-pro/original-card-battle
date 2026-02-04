# Guild Facility Detailed Design Document (GUILD_DESIGN_V3.0)

## Update History

| Date | Content |
|------|---------|
| 2026-02-04 | V3.0: Restructured with 2 main tabs (Headquarters + Storage). Storage functionality integrated from storage_design.md. |
| - | V2.1: Integrated Item Type System, String Grade support, Context API integration. |

---

## 1. Facility Overview

The Guild is a place where adventurers raise their social status, gather information, manage their belongings, and earn their daily keep.

**V3.0 Changes:**
- Restructured into 2 main tabs: **Headquarters** and **Storage**
- Storage facility (formerly standalone) is now integrated as a tab
- Quest system moved to future features (see `.claude/feature_plans/quest_system.md`)

### Tab Structure

```
Guild (酒場)
├── Tab 1: Headquarters (本部)
│   ├── Promotion Exams - Class grade advancement
│   └── Rumors - Pay magic stones for next-run buffs
│
└── Tab 2: Storage (倉庫)
    ├── Item Storage - Long-term item storage
    ├── Inventory Management - Current carry items
    └── Equipment Management - Equipment slots and inventory
```

### Main Functions

**Headquarters Tab:**
1. **Promotion Exams**: Special battle events to raise Class Grade.
2. **Rumors**: Pay Magic Stones to grant advantageous effects for the next exploration.

**Storage Tab:**
1. **Item Storage**: Long-term safe storage (retained on death).
2. **Inventory Management**: Items carried during exploration (lost on death).
3. **Equipment Management**: Equipment slots and equipment inventory.

---

## 2. Promotion Exams

### 2.1 Basic Specifications

Exams to raise the player's "Class Grade".

**Eligibility Conditions:**

- Must meet the "Card Collection Count" or "Specific Achievements" corresponding to the current grade.
- Example: Promotion from "Apprentice Swordsman" → "Swordsman" requires possessing 5 or more cards.

**Exam Content:**

- Mock battle with the Guild Master or an Examiner.
- Combat against a designated target (Arena format).
- Defeat does not result in Game Over; the player returns to camp with 1 HP (Retry possible).

**Pass Rewards:**

- Title Promotion: `classGrade` increases (string update).
- Permanent Stat Boost: Passive effects based on the title (HP+, ATK+, etc.).
- Special Reward: High rarity equipment.

---

### 2.2 Rank Definitions

#### 2.2.1 Swordsman Class

| Grade   | Title        | Requirement | Exam Opponent               | Pass Benefit           |
| ------- | ------------ | ----------- | --------------------------- | ---------------------- |
| Grade 0 | Apprentice   | Initial     | -                           | -                      |
| Grade 1 | Swordsman    | 5 Cards     | Training Dummy (Lv5)        | maxHP+10, Quest Slot+1 |
| Grade 2 | Sword Master | 15 Cards    | Guild Instructor (Lv15)     | ATK+5%, Reward Bonus   |
| Grade 3 | Sword Saint  | 30 Cards    | Veteran Warrior (Lv30)      | All Stats +5%          |
| Grade 4 | Sword God    | 50 Cards    | Phantom of the Saint (Boss) | Unique Legend Equip    |

#### 2.2.2 Mage Class

| Grade   | Title       | Requirement | Exam Opponent              | Pass Benefit        |
| ------- | ----------- | ----------- | -------------------------- | ------------------- |
| Grade 0 | Apprentice  | Initial     | -                          | -                   |
| Grade 1 | Mage        | 5 Cards     | Magic Puppet (Lv5)         | maxHP+8, maxAP+5    |
| Grade 2 | Wizard      | 15 Cards    | Court Mage (Lv15)          | Magic Dmg +5%       |
| Grade 3 | Archmage    | 30 Cards    | Ancient Sage (Lv30)        | All Stats +5%       |
| Grade 4 | Magic Deity | 50 Cards    | Shadow of the Magus (Boss) | Unique Legend Equip |

#### 2.2.3 Summoner Class

| Grade   | Title          | Requirement | Exam Opponent               | Pass Benefit        |
| ------- | -------------- | ----------- | --------------------------- | ------------------- |
| Grade 0 | Apprentice     | Initial     | -                           | -                   |
| Grade 1 | Summoner       | 5 Cards     | Spirit Keeper (Lv5)         | Summon Cost -1      |
| Grade 2 | High Summoner  | 15 Cards    | Guardian of the Pact (Lv15) | Summon HP +10%      |
| Grade 3 | Grand Summoner | 30 Cards    | Gatekeeper (Lv30)           | All Stats +5%       |
| Grade 4 | Summon God     | 50 Cards    | Primal Beast (Boss)         | Unique Legend Equip |

---

### 2.3 Reward Bonus Unlocks

**Perks from Grade 2 onwards:**

- Rarity bonuses for equipment rewards at the Guild.
- Increased Gold amount for Quest rewards.
- Unlocking of high-difficulty requests.

---

## 3. Rumors

### 3.1 Basic Specifications

**Cost:** Consumes Magic Stones.

- Clearly distinguished from Gold.
- UI Representation: Icon with a suspicious purple glow.

**Effect:** Grants a buff to the _next_ exploration (One-time use).

### 3.2 Rumor Types (Examples)

```typescript
interface Rumor {
  id: string;
  name: string;
  description: string;
  cost: number; // Magic Stone cost
  effect: RumorEffect;
  rarity: "common" | "rare" | "epic";
}

type RumorEffect =
  | { type: "elite_rate"; value: number } // Elite Enemy Spawn Rate UP
  | { type: "shop_discount"; value: number } // Shop Discount
  | { type: "treasure_rate"; value: number } // Treasure Chest Spawn Rate UP
  | { type: "start_bonus"; bonus: string }; // Start of Run Bonus
```

**Examples:**

1. **"Rumor of the Monster's Nest"** (10 Stones)

- Elite enemy spawn rate UP (High Risk, High Return).

2. **"Rumor of the Lucky Merchant"** (20 Stones)

- Dungeon Shop discount rate UP.

3. **"Rumor of Ancient Treasure"** (15 Stones)

- Treasure chest room spawn rate +20%.

---

## 4. Quests

### 4.1 Basic Specifications

**Quest Types:**

- Daily Quest: Updates every day.
- Weekly Quest: Updates once a week.

**Rewards:**

- Gold
- Magic Stones
- Consumable Items

**Unlock Conditions:**

- Promoting ranks unlocks higher difficulty and higher reward requests.

### 4.2 Quest Data Structure

```typescript
interface Quest {
  id: string;
  title: string;
  description: string;
  type: "daily" | "weekly";
  requiredGrade: string; // e.g., "Swordsman", "Mage"

  objectives: QuestObjective[];
  rewards: QuestReward;

  isActive: boolean;
  isCompleted: boolean;
  progress: number;
}

interface QuestObjective {
  type: "defeat" | "collect" | "explore";
  target: string; // EnemyID, ItemID, Depth, etc.
  required: number;
  current: number;
}

interface QuestReward {
  gold?: number;
  magicStones?: number;
  items?: string[]; // Array of Item IDs
  experience?: number;
}
```

**Quest Example:**

```typescript
{
  id: "daily_001",
  title: "Hunt the Corrupted Hounds",
  description: "Defeat 3 Corrupted Hounds appearing in Depth 1.",
  type: "daily",
  requiredGrade: "Apprentice Swordsman",
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

## 5. UI/UX Design

### 5.1 Screen Layout

```
┌────────────────────────────────────────────┐
│  🍺 Guild - Tavern                         │
├────────────────────────────────────────────┤
│                                            │
│  [Rumors] [Quests] [Exams] ← Tab Switch    │
│  ════════  ──────  ───────                 │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │                                      │  │
│  │    Content of Selected Tab           │  │
│  │                                      │  │
│  │                                      │  │
│  │                                      │  │
│  └──────────────────────────────────────┘  │
│                                            │
│  [Return to Camp]                          │
│                                            │
└────────────────────────────────────────────┘

```

### 5.2 Promotion Exam Tab UI

```
┌────────────────────────────────────────────┐
│  Promotion Exams                           │
├────────────────────────────────────────────┤
│                                            │
│  ┌────────────┐      ┌────────────┐        │
│  │ Current    │  →   │ Next       │        │
│  │ Swordsman  │      │ Sword Mstr │        │
│  └────────────┘      └────────────┘        │
│                                            │
│  ◆ Prerequisites                           │
│   [✓] Cards Owned: 15/15                   │
│   [✓] Gold Owned: 500/500 G                │
│                                            │
│  ◆ Exam Details                            │
│   Opponent: Guild Instructor (Lv15)        │
│   Rec. HP: 60+                             │
│   Rec. AP: 50+                             │
│                                            │
│  ◆ Pass Rewards                            │
│   - Title: Sword Master                    │
│   - ATK +5%                                │
│   - Rare Equip x1                          │
│                                            │
│  ⚠️ Starting the exam initiates combat.     │
│     Please prepare your equipment!         │
│                                            │
│  [Start Exam]      [Back]                  │
│                                            │
└────────────────────────────────────────────┘

```

### 5.3 Battle Transition Effects

**On Exam Start:**

1. Click "Start Exam".
2. Screen darkens.
3. Sound effect: Siren or Gong.
4. Battle starts with a different background (Arena or Dojo) than normal dungeons.

**On Victory:**

1. Fanfare with "Passed" text.
2. Reward screen display.
3. Promotion animation upon returning to camp.

**On Defeat:**

1. Message: "It seems you need more training..."
2. Return to camp with 1 HP.
3. Retry is possible.

---

## 6. Data Structure Definition

### 6.1 GuildTypes.ts

```typescript
// src/types/GuildTypes.ts (Create New)

/**
 * Promotion Exam Data
 */
export interface PromotionExam {
  currentGrade: string; // "Apprentice", "Swordsman" etc
  nextGrade: string; // "Swordsman", "Sword Master" etc
  requiredCardCount: number; // Required cards
  requiredGold?: number; // Required Gold (Optional)
  enemyId: string; // Exam Opponent Enemy ID
  description: string;
  recommendations: {
    hp: number;
    ap: number;
  };
  rewards: {
    statBonus: string; // "maxHP+10", "ATK+5%" etc
    items?: string[]; // Reward Item IDs
  };
}

/**
 * Rumor Data
 */
export interface Rumor {
  id: string;
  name: string;
  description: string;
  cost: number; // Magic Stone Cost
  effect: RumorEffect;
  rarity: "common" | "rare" | "epic";
  icon: string;
}

export type RumorEffect =
  | { type: "elite_rate"; value: number }
  | { type: "shop_discount"; value: number }
  | { type: "treasure_rate"; value: number }
  | { type: "start_bonus"; bonus: string };

/**
 * Quest Data
 */
export interface Quest {
  id: string;
  title: string;
  description: string;
  type: "daily" | "weekly";
  requiredGrade: string;

  objectives: QuestObjective[];
  rewards: QuestReward;

  isActive: boolean;
  isCompleted: boolean;
  expiresAt?: Date;
}

export interface QuestObjective {
  type: "defeat" | "collect" | "explore";
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
 * Guild State
 */
export interface GuildState {
  activeRumors: string[]; // List of active rumor IDs
  acceptedQuests: string[]; // Accepted Quest IDs
  completedQuests: string[]; // History of completed quests
  availableExam: PromotionExam | null; // Currently available exam
}
```

---

## 7. Implementation Procedure

### Phase 1: Data and Types Preparation

**1.1 Type Definition Creation**

```
□ Create src/types/GuildTypes.ts
  □ PromotionExam Type
  □ Rumor Type
  □ Quest Type
  □ GuildState Type

```

**1.2 Exam Data Creation**

```
□ Create src/camps/facilities/Guild/data/PromotionData.ts
  □ Swordsman line exams (4 stages)
  □ Mage line exams (4 stages)
  □ Summoner line exams (4 stages)

```

**1.3 Exam Enemy Data**

```
□ Create src/domain/characters/enemy/data/GuildEnemyData.ts
  □ Training Dummy (Lv5)
  □ Guild Instructor (Lv15)
  □ Veteran Warrior (Lv30)
  □ Phantom of the Saint (Boss)
  □ Enemies for other classes (Magic Puppet, Spirit Keeper, etc.)

```

---

### Phase 2: Context Integration

**2.1 GuildContext Creation**

```typescript
// src/contexts/GuildContext.tsx

interface GuildContextValue {
  guildState: GuildState;

  // Rumor related
  activeRumors: Rumor[];
  activateRumor: (rumorId: string) => boolean;
  clearRumors: () => void;

  // Quest related
  acceptedQuests: Quest[];
  acceptQuest: (questId: string) => boolean;
  updateQuestProgress: (
    questId: string,
    progress: Partial<QuestObjective>
  ) => void;
  completeQuest: (questId: string) => void;

  // Promotion Exam related
  availableExam: PromotionExam | null;
  checkExamEligibility: () => PromotionExam | null;
  startExam: (exam: PromotionExam) => void;
}
```

**2.2 Integration with PlayerContext**

```typescript
// Processing when Exam is Passed
const handleExamPassed = (exam: PromotionExam) => {
  // Update PlayerContext classGrade
  updatePlayer({
    classGrade: exam.nextGrade,
  });

  // Apply Stat Bonus
  applyStatBonus(exam.rewards.statBonus);

  // Grant Item Rewards
  if (exam.rewards.items) {
    exam.rewards.items.forEach((itemId) => {
      addItem(createItemFromId(itemId));
    });
  }
};
```

---

### Phase 3: Guild UI Component Implementation

**3.1 Guild.tsx Skeleton**

```typescript
// src/camps/facilities/Guild/Guild.tsx

type GuildTab = "rumors" | "quests" | "promotion";

const Guild: React.FC = () => {
  const [selectedTab, setSelectedTab] = useState<GuildTab>("promotion");
  const { guildState } = useGuild();
  const { player } = usePlayer();

  return (
    <div className="guild-screen">
      <header className="guild-header">
        <h1>🍺 Guild - Tavern</h1>
      </header>

      <nav className="guild-tabs">
        <button
          className={selectedTab === "rumors" ? "active" : ""}
          onClick={() => setSelectedTab("rumors")}
        >
          Rumors
        </button>
        <button
          className={selectedTab === "quests" ? "active" : ""}
          onClick={() => setSelectedTab("quests")}
        >
          Quests
        </button>
        <button
          className={selectedTab === "promotion" ? "active" : ""}
          onClick={() => setSelectedTab("promotion")}
        >
          Exams
        </button>
      </nav>

      <div className="guild-content">
        {selectedTab === "rumors" && <RumorsTab />}
        {selectedTab === "quests" && <QuestsTab />}
        {selectedTab === "promotion" && <PromotionTab />}
      </div>

      <button className="back-button" onClick={onBack}>
        Return to Camp
      </button>
    </div>
  );
};
```

**3.2 PromotionTab.tsx Implementation**

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
        <p>No exams currently available.</p>
        <p>Collect more cards to aim for the next promotion.</p>
      </div>
    );
  }

  // Eligibility Check
  const cardCount = player.deck.length;
  const meetsCardRequirement = cardCount >= exam.requiredCardCount;
  const meetsGoldRequirement = exam.requiredGold
    ? player.gold >= exam.requiredGold
    : true;

  const canTakeExam = meetsCardRequirement && meetsGoldRequirement;

  const handleStartExam = () => {
    if (!canTakeExam) return;

    // Update GameStateContext and transition to Exam Battle
    setGameState({
      currentScreen: "battle",
      battleMode: "exam",
      depth: 1,
      encounterCount: 0,
      battleConfig: {
        enemyIds: [exam.enemyId],
        backgroundType: "arena",
        onWin: () => handleExamPassed(exam),
        onLose: () => handleExamFailed(),
      },
    });
  };

  return (
    <div className="promotion-tab">
      {/* Grade Display */}
      <div className="grade-display">
        <div className="current-grade">
          <span className="grade-label">Current</span>
          <span className="grade-name">{exam.currentGrade}</span>
        </div>
        <div className="arrow">→</div>
        <div className="next-grade">
          <span className="grade-label">Next</span>
          <span className="grade-name">{exam.nextGrade}</span>
        </div>
      </div>

      {/* Prerequisites */}
      <section className="exam-requirements">
        <h3>◆ Prerequisites</h3>
        <div
          className={`requirement ${meetsCardRequirement ? "met" : "unmet"}`}
        >
          [{meetsCardRequirement ? "✓" : "✗"}] Cards Owned: {cardCount}/
          {exam.requiredCardCount}
        </div>
        {exam.requiredGold && (
          <div
            className={`requirement ${meetsGoldRequirement ? "met" : "unmet"}`}
          >
            [{meetsGoldRequirement ? "✓" : "✗"}] Gold Owned: {player.gold}/
            {exam.requiredGold}G
          </div>
        )}
      </section>

      {/* Exam Details */}
      <section className="exam-details">
        <h3>◆ Exam Details</h3>
        <p>{exam.description}</p>
        <div className="recommendations">
          <p>Rec. HP: {exam.recommendations.hp}+</p>
          <p>Rec. AP: {exam.recommendations.ap}+</p>
        </div>
      </section>

      {/* Rewards */}
      <section className="exam-rewards">
        <h3>◆ Pass Rewards</h3>
        <ul>
          <li>Title: {exam.nextGrade}</li>
          <li>{exam.rewards.statBonus}</li>
          {exam.rewards.items && (
            <li>Rare Equip x{exam.rewards.items.length}</li>
          )}
        </ul>
      </section>

      {/* Warning */}
      <div className="exam-warning">
        ⚠️ Starting the exam initiates combat. Please prepare your equipment!
      </div>

      {/* Start Button */}
      <button
        className="start-exam-button"
        disabled={!canTakeExam}
        onClick={handleStartExam}
      >
        {canTakeExam ? "Start Exam" : "Requirements Not Met"}
      </button>
    </div>
  );
};
```

**3.3 RumorsTab.tsx and QuestsTab.tsx**

```typescript
// Simple Implementation (Basic UI for Phase 3)

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

### Phase 4: Battle System Integration

**4.1 BattleScreen.tsx Extension**

```typescript
// src/battles/battleUI/BattleScreen.tsx

interface BattleScreenProps {
  depth: Depth;
  onDepthChange: (depth: Depth) => void;
  battleMode?: "normal" | "exam" | "return_route"; // ✨ Added
  enemyIds?: string[]; // ✨ Added
  onBattleEnd?: (result: "victory" | "defeat") => void; // ✨ Added
}

const BattleScreen: React.FC<BattleScreenProps> = ({
  depth,
  onDepthChange,
  battleMode = "normal",
  enemyIds,
  onBattleEnd,
}) => {
  // Branch logic based on battleMode

  if (battleMode === "exam") {
    // Promotion Exam Mode
    // - Generate enemy from enemyIds
    // - No depth progression
    // - On Win: onBattleEnd('victory')
    // - On Lose: HP 1 and onBattleEnd('defeat')
  }

  // ... Existing implementation
};
```

**4.2 Victory/Defeat Judgment and Result Processing**

```typescript
// Exam Victory Processing
if (battleMode === "exam" && battleResult === "victory") {
  return (
    <ExamVictoryScreen
      onContinue={() => {
        // Update GameStateContext and go to Camp
        setGameState((prev) => ({
          ...prev,
          currentScreen: "camp",
          battleMode: null,
        }));

        // Pass processing (Already executed via Context)
      }}
      exam={currentExam}
    />
  );
}

// Exam Defeat Processing
if (battleMode === "exam" && battleResult === "defeat") {
  return (
    <ExamDefeatScreen
      onRetry={() => {
        // Restart Exam
        resetBattle();
      }}
      onReturn={() => {
        // Return to Camp with 1 HP
        updatePlayer({ hp: 1 });
        setGameState((prev) => ({
          ...prev,
          currentScreen: "camp",
          battleMode: null,
        }));
      }}
    />
  );
}
```

---

### Phase 5: Magic Stone System Implementation (For Rumors)

**5.1 MagicStoneData.ts**

```typescript
// src/items/data/MagicStoneData.ts

import type { Item } from "../../types/ItemTypes";

export const MAGIC_STONE_ITEMS: Item[] = [
  {
    id: "magic_stone_small",
    typeId: "magic_stone_small",
    name: "Magic Stone (Small)",
    description: "A small stone faintly charged with magic.",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 30, // FIXED: Unified with shop/blacksmith designs
    rarity: "common",
    sellPrice: 30, // FIXED: Aligned with magicStoneValue
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
  {
    id: "magic_stone_medium",
    typeId: "magic_stone_medium",
    name: "Magic Stone (Medium)",
    description: "A magic stone glowing dimly.",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 100, // FIXED: Unified with shop/blacksmith designs
    rarity: "uncommon",
    sellPrice: 100, // FIXED: Aligned with magicStoneValue
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
  {
    id: "magic_stone_large",
    typeId: "magic_stone_large",
    name: "Magic Stone (Large)",
    description: "Precious stone emitting strong magic.",
    itemType: "magicStone",
    icon: "💎",
    magicStoneValue: 350, // ADDED: Consistent with shop/blacksmith designs
    rarity: "rare",
    sellPrice: 350,
    canSell: true,
    canDiscard: false,
    stackable: true,
    maxStack: 99,
    stackCount: 1,
  },
];
```

---

## 8. CSS Design

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

.current-grade,
.next-grade {
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

## 9. Test Items

### 9.1 Promotion Exam System

```
□ Exam Eligibility Judgment
  □ Card count check
  □ Gold possession check
  □ Button disabled when conditions unmet

□ Exam Battle Start
  □ Correct enemy appears
  □ Arena background display
  □ No depth progression

□ Pass Processing
  □ classGrade update
  □ Stat bonus application
  □ Reward item granting
  □ Return to camp

□ Fail Processing
  □ Return with 1 HP
  □ Retry available
  □ classGrade remains unchanged

```

### 9.2 Context Integration

```
□ GuildContext Operation
  □ Exam eligibility judgment
  □ State persistence

□ Integration with PlayerContext
  □ classGrade update
  □ Stat changes

□ Integration with InventoryContext
  □ Magic Stone calculation
  □ Item reward granting

```

---

## 10. Storage Tab (Integrated from Storage System)

### 10.1 Storage vs Inventory Distinction

The Storage tab manages the critical distinction between safe storage and risky carry items.

| Feature | Storage (Warehouse) | Inventory (On Hand) | Equipment Inventory | Equipment Slots |
|---------|---------------------|---------------------|---------------------|-----------------|
| **Access** | BaseCamp only | Anywhere | Anywhere (Equip Only) | Equipped |
| **Capacity** | 100 slots | 20 slots | 3 slots | 6 slots |
| **Upon Death** | **RETAINED** | **LOST** | **LOST** | **LOST** |
| **Item Types** | All items | All items | Equipment only | Equipment only |

### 10.2 Death Processing

**Items Lost on Death:**
- All Inventory items
- All Equipment Slots items
- All Equipment Inventory items
- Exploration Gold, Magic Stones, Soul Remnants (current run)

**Items Retained on Death:**
- All Storage items
- BaseCamp resources (Gold, Magic Stones stored at camp)
- Accumulated Soul Remnants (totalSouls)
- Sanctuary unlocks, Card Mastery, Encyclopedia data

### 10.3 Item Movement Operations

| From | To | Operation |
|------|----|-----------|
| Storage | Inventory | Retrieve from warehouse |
| Inventory | Storage | Deposit into warehouse |
| Storage | Equipment Slots | Equip directly |
| Equipment Slots | Storage | Unequip and store |
| Equipment Inventory | Equipment Slots | Equip spare |
| Equipment Slots | Equipment Inventory | Swap to spare |

### 10.4 Equipment Tab Layout

The Equipment tab within Storage has a specialized three-section layout:

```
┌─────────────────────────────────────────────────────────────┐
│  [Items]  [Equipment]  ← Sub-tabs                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─── Equipment List ───┐  ┌───── Right Panel ─────────┐  │
│  │ (Storage Equipment)  │  │                            │  │
│  │                      │  │ ┌── Equipment Slots ────┐  │  │
│  │  ⚔️ 🛡️ 👑 👢 💍      │  │ │ weapon armor helmet   │  │  │
│  │                      │  │ │ boots  acc1  acc2     │  │  │
│  │  Filtered view:      │  │ └───────────────────────┘  │  │
│  │  Equipment items     │  │                            │  │
│  │  from Storage only   │  │ ┌── Equipment Inventory ─┐  │  │
│  │                      │  │ │    (Max 3 slots)       │  │  │
│  │                      │  │ │  ⚔️  🛡️  [empty]       │  │  │
│  │                      │  │ └───────────────────────┘  │  │
│  └──────────────────────┘  └────────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 10.5 Strategic Significance

**Risk Management Core:**
- Store valuable equipment in Storage before dangerous exploration
- Carry only essential gear in Inventory/Equipment
- Use Equipment Inventory (3 slots) for mid-dungeon swaps

**Recovery After Death:**
- Access Storage for spare equipment
- Retry with stored gear
- Purchase new equipment with BaseCamp Gold

---

## 11. Future Features (Moved to feature_plans/)

The following features are planned for future implementation:

- **Quest System**: See `.claude/feature_plans/quest_system.md`
- **NPC Conversation**: See `.claude/feature_plans/npc_conversation.md`
- **Title System**: See `.claude/feature_plans/title_system.md`

---

## 12. Reference Documents

```
CAMP_FACILITIES_DESIGN_V4
└── GUILD_DESIGN_V3.0 [This Document]
    ├── Section 2-9: Headquarters functionality
    ├── Section 10: Storage functionality (integrated)
    ├── GuildEnemyData.ts [Exam Enemy Data]
    ├── PromotionData.ts [Promotion Exam Data]
    └── battle_logic.md [Battle System]
```
