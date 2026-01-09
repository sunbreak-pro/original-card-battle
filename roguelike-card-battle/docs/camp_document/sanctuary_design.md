Here is the English translation of the design document.

# Sanctuary Detailed Design Document V2.0 (SANCTUARY_DESIGN_V2)

## Revision History

- V2.0: **Fundamental Design Overhaul** - Changed Soul Remnants to an experience point system, added +1 Exploration Count skill, removed roguelite elements (permanent death resets).

---

## 1. Overview

The Sanctuary is a facility where players consume **Soul Remnants** to unlock permanent upgrades.

**Major Changes in V2.0:**

```
Old Design:
Gained Souls on Death → Permanent Upgrades (Roguelite element)

New Design:
Gain Souls on Enemy Kill (EXP style) → Add to Total upon Survival → Permanent Upgrades
Integrated with the Exploration Count Limit system

```

### Key Roles

1. **Permanent Upgrades**: Improving basic player stats that persist across runs.
2. **Build Diversity**: Selecting growth directions via the skill tree.
3. **Progress Visualization**: Feeling growth through unlocked nodes.
4. **Exploration Expansion**: Skill to increase Exploration Count +1 (NEW).

---

## 2. Detailed Functional Specifications

### 2.1 Soul Remnants - V2.0 Major Changes

#### 2.1.1 Acquisition Method (Experience System)

**Change in V2.0:**

```
Old: Gained upon Death
New: Gained upon Enemy Kill (Like Experience Points)

```

**Acquisition Timing:**

| Timing        | Amount        | Notes                           |
| ------------- | ------------- | ------------------------------- |
| Minion Kill   | 5 Souls       | Added immediately during combat |
| Elite Kill    | 15 Souls      | Elite enemies / Mid-bosses      |
| Boss Kill     | 50 Souls      | Floor Bosses                    |
| Return Battle | 50% of normal | When using the Return Route     |

**Important Mechanism:**

```typescript
// Souls gained in this specific run (Temporary)
currentRunSouls: number;

// Accumulated Souls (Permanent)
totalSouls: number;

// Upon Survival
totalSouls += currentRunSouls × SurvivalMethodMultiplier;
currentRunSouls = 0;

// Upon Death
currentRunSouls = 0;  // Souls from this run are lost
// totalSouls is retained (No change)

```

#### 2.1.2 Survival vs. Death Processing

**Case: Survival**

```
Defeat Enemy → Gain Souls (currentRunSouls)
  ↓
Survive (Return Stone or Return Route)
  ↓
Acquired Souls × Method Multiplier → Added to Total

Survival Method Multiplier:
- Return Route: 100%
- Normal Return Stone: 70%
- Blessed Return Stone: 80%
- Emergency Return Stone: 60%

Example: Gained 100 Souls this run, used Normal Return Stone
→ 100 × 0.7 = 70 Souls added to Total

```

**Case: Death**

```
Defeat Enemy → Gain Souls (currentRunSouls)
  ↓
Death
  ↓
Souls gained this run → Zero
Accumulated Souls (totalSouls) → Retained (Unchanged)

Example: Gained 100 Souls this run, Died
→ The 100 Souls are reset to 0
→ However, the past accumulated 500 Souls remain safe

```

**Properties:**

- Souls accumulated in the past are **permanently retained**.
- Souls gained in the current run are not added unless you survive.
- Can only be used in the Sanctuary.

**Initial Possession:**

- New Player: 50 Souls (For tutorial)

---

### 2.2 Skill Tree Structure

#### 2.2.1 Tree Shape

**Radial Design (Recommended):**

```
                  [Center]
                    │
        ┌───────────┼───────────┐
        │           │           │
     [HP Branch] [Gold Branch] [Utility Branch]
        │           │           │
    ┌───┼───┐   ┌───┼───┐   ┌───┼───┐
  [+10] [+20] [+10%][+20%] [Expl.] [Expand]
    │           │         Count+1   │
  [+30]       [+30%]        │     [Adv.]
                         [Count+2]

```

**Features:**

- Extends in 4 directions from the center.
- Each direction has a theme (HP / Gold / Combat / Utility).
- Higher tier nodes are more powerful but cost more.
- **Exploration Count +1 is strategically critical (NEW).**

#### 2.2.2 Node Types

**Tier 1 (Basic Upgrades): Cost 20-30 Souls**

| Node Name            | Icon | Effect                                | Cost |
| -------------------- | ---- | ------------------------------------- | ---- |
| Blessing of Life I   | ❤️   | Initial HP +10                        | 20   |
| Blessing of Wealth I | 💰   | Initial Gold +10%                     | 25   |
| Swordsman's Insight  | ⚔️   | Swordsman: Start with +1 Sword Energy | 30   |
| Mage's Insight       | 🔮   | Mage: Start with +1 Resonance Level   | 30   |
| Summoner's Insight   | 👻   | Summoner: Start with +1 Summon Slot   | 30   |

**Tier 2 (Specialization): Cost 40-80 Souls**

| Node Name                  | Icon | Effect                           | Cost | Prerequisite         |
| -------------------------- | ---- | -------------------------------- | ---- | -------------------- |
| Blessing of Life II        | ❤️❤️ | Initial HP +20                   | 50   | Blessing of Life I   |
| Blessing of Wealth II      | 💰💰 | Initial Gold +20%                | 60   | Blessing of Wealth I |
| Eye of Appraisal           | 👁️   | Displays detailed equipment info | 40   | -                    |
| Expanded Bag               | 🎒   | Inventory +5                     | 50   | -                    |
| Boon of Recovery           | 💊   | Recover +5% HP after combat      | 60   | Blessing of Life I   |
| **Extended Exploration I** | ⏰   | **Exploration Count +1**         | 80   | -                    |

**Tier 3 (Ultimate Upgrades): Cost 100-150 Souls**

| Node Name                   | Icon   | Effect                              | Cost | Prerequisite          |
| --------------------------- | ------ | ----------------------------------- | ---- | --------------------- |
| Blessing of Life III        | ❤️❤️❤️ | Initial HP +30                      | 100  | Blessing of Life II   |
| Blessing of Wealth III      | 💰💰💰 | Initial Gold +30%                   | 100  | Blessing of Wealth II |
| Indomitable Will            | 🛡️     | Survive with 1 HP once per run      | 120  | Blessing of Life II   |
| Soul Resonance              | ✨     | Soul Remnants Gain +20%             | 80   | -                     |
| True Appraisal              | 👁️‍🗨️     | Displays hidden equipment effects   | 90   | Eye of Appraisal      |
| **Extended Exploration II** | ⏰⏰   | **Exploration Count +2 (Total +3)** | 150  | Extended Expl. I      |

#### 2.2.3 Exploration Extension Skills (NEW - Critical)

**Extended Exploration Skills:**

```
[Tier 2] Extended Exploration I
Cost: 80 Souls
Effect: Increases Max Exploration Count by +1
Prerequisite: None

[Tier 3] Extended Exploration II
Cost: 150 Souls
Effect: Increases Max Exploration Count by +2 (Total +3)
Prerequisite: Extended Exploration I

[Example Effects]
Default: 10 Explorations
After unlocking Extended Expl. I: 11 Explorations
After unlocking Extended Expl. II: 13 Explorations

```

**Strategic Value:**

- Mitigates the exploration limit.
- Allows for more trial and error.
- Increases probability of reaching deeper floors.
- High cost, but extremely high value.

#### 2.2.4 Class Specialization Nodes

**Swordsman Exclusive:**

```
Swordsman's Insight I (30 Souls) → II (60 Souls) → Mastery (100 Souls)
Effect: Start with +1 / +2 / +3 Sword Energy

```

**Mage Exclusive:**

```
Mage's Insight I (30 Souls) → II (60 Souls) → Mastery (100 Souls)
Effect: Start with +1 / +2 / +3 Resonance Level

```

**Summoner Exclusive:**

```
Summoner's Insight I (30 Souls) → II (60 Souls) → Mastery (100 Souls)
Effect: Start with +1 / +2 / +3 Summon Slots

```

---

### 2.3 Node Status

**3 States:**

1. **Unlocked**

- Visual: Lit up, glowing gold.
- Effect: Active.
- Interaction: None.

2. **Available**

- Visual: Blinking, white border.
- Effect: Not applied yet.
- Interaction: Long press to unlock.
- Condition: Prerequisite unlocked + Sufficient Souls.

3. **Locked**

- Visual: Grayed out, silhouette.
- Effect: None.
- Interaction: None.
- Reason: Prerequisite not met OR Insufficient Souls.

---

## 3. UI/UX Design

### 3.1 Screen Layout

```
┌────────────────────────────────────────────────────────┐
│  ✨ Sanctuary                                         │
├────────────────────────────────────────────────────────┤
│  Soul Remnants: Total 650 / This Run +85               │  ← NEW
│  Exploration Limit: 10 Runs (+0)                       │  ← NEW
│                                                        │
│              [Skill Tree Display Area]                 │
│                                                        │
│                      [Center]                          │
│                     (Unlocked)                         │
│                        │                               │
│        ┌───────────────┼───────────────┐               │
│        │               │               │               │
│   [Life Bless I]  [Wealth Bless I] [Ext. Explor I]     │  ← NEW
│   (Unlocked ✨)   (Available 💫)   (Available 💫)      │
│        │               │           80 Souls            │
│   [Life Bless II] [Wealth Bless II] [Ext. Explor II]   │  ← NEW
│   (Locked 🔒)     (Locked 🔒)      (Locked 🔒)         │
│                                    150 Souls           │
│                                                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │ [Selected Node Details]                           │  │
│  │ ⏰ Extended Exploration I                        │  │  ← NEW
│  │                                                  │  │
│  │ Effect: Increases Max Exploration Count by +1    │  │
│  │         (Current 10 -> 11)                       │  │
│  │                                                  │  │
│  │ Cost: 80 Soul Remnants                            │  │
│  │ Currently Held: 650 (Sufficient)                  │  │
│  │                                                  │  │
│  │ Prerequisite: None                                │  │
│  │                                                  │  │
│  │ * Mitigates exploration limits,                   │  │
│  │   enabling more attempts.                         │  │
│  │                                                  │  │
│  │ [Long Press to Unlock]                            │  │
│  └──────────────────────────────────────────────────┘  │
│                                                        │
│  [Return to Camp]                                      │
└────────────────────────────────────────────────────────┘

```

### 3.2 Visuals

**Unlocked:**

```css
border: 3px solid gold;
background: radial-gradient(circle, #ffd700, #ffa500);
box-shadow: 0 0 20px gold;
animation: pulse 2s infinite;
```

**Available:**

```css
border: 3px solid white;
background: radial-gradient(circle, #ffffff, #cccccc);
box-shadow: 0 0 10px white;
animation: blink 1.5s infinite;
```

**Locked:**

```css
border: 2px solid #444;
background: #222;
opacity: 0.5;
filter: grayscale(100%);
```

### 3.3 Unlock Interaction

**Long Press Unlock (1.5 seconds):**

```
Tap Start
  ↓
Show Progress Ring (0%)
  ↓
Hold (0.5s)
  ↓
Progress Ring (33%)
SFX: "Shhhhuuu..."
  ↓
Hold (1.0s)
  ↓
Progress Ring (66%)
SFX: "Shhhhuuu..." (Louder)
  ↓
Hold Complete (1.5s)
  ↓
Unlock Effect
SFX: "Ching!" (Sparkle sound)
  ↓
Node Lights Up
Holy light spreads
  ↓
Completion Message
"Unlocked Extended Exploration I!"
"Max Exploration Count increased 10 -> 11!"  ← NEW

```

**Cancel:**

- Release finger mid-press → Cancel.
- Progress ring disappears.
- Soul Remnants are not consumed.

### 3.4 Effects (Feedback)

**Unlock Success:**

```
SFX: Heavy bell sound "GONG..."
Visuals:
  1. Holy light explodes from the node.
  2. Light spreads across the screen.
  3. Stained glass background flashes momentarily.
  4. Node lights up gold.
  5. Special effect if Exploration Count increased. ← NEW

```

**Soul Remnants Gain (V2.0):**

```
On Enemy Kill:
  1. Soul orb appears from enemy.
  2. Absorbed by player.
  3. "+5 Soul Remnants" text.
  4. Top right "This Run" counter updates.

On Survival:
  1. Large Soul Orb appears.
  2. Number counts up.
  3. "+85 Soul Remnants (Total 650 -> 735)" displayed.
  4. Sparkle effect.

On Death:
  1. Soul Orb shatters.
  2. "85 Souls from this run were lost..."
  3. "Total 650 Souls remain safe."
  4. Dark/Gloomy visual.

```

---

## 4. Data Structure Definition

### 4.1 SanctuaryTypes.ts

```typescript
// src/types/SanctuaryTypes.ts (Updated)

/**
 * Skill Node
 */
export interface SkillNode {
  id: string;
  name: string;
  description: string;
  icon: string;
  cost: number;
  category: "hp" | "gold" | "combat" | "utility" | "class" | "exploration"; // NEW: exploration added
  tier: 1 | 2 | 3;
  prerequisites: string[]; // IDs of prerequisite nodes
  effects: NodeEffect[];
  classRestriction?: "swordsman" | "mage" | "summoner";
}

/**
 * Node Effects
 */
export interface NodeEffect {
  type:
    | "stat_boost"
    | "special_ability"
    | "resource_increase"
    | "exploration_limit"; // NEW
  target: string; // 'initial_hp', 'initial_gold', 'exploration_limit', etc.
  value: number | string;
}

/**
 * Node Status
 */
export type NodeStatus = "unlocked" | "available" | "locked";

/**
 * Player Sanctuary Data (V2.0)
 */
export interface SanctuaryProgress {
  unlockedNodes: Set<string>;

  // V2.0: Soul Remnants System
  currentRunSouls: number; // Souls gained in this run
  totalSouls: number; // Total accumulated souls (from past runs)

  // NEW: Exploration Extension
  explorationLimitBonus: number; // Additional exploration count (Default 0)
}
```

### 4.2 Skill Node Data

```typescript
// src/camps/facilities/Sanctuary/data/SkillTreeData.ts (Updated)

import type { SkillNode } from "../../../../types/SanctuaryTypes";

/**
 * Tier 1: Basic Upgrades
 */
export const TIER1_NODES: SkillNode[] = [
  {
    id: "hp_blessing_1",
    name: "Blessing of Life I",
    description: "Increases initial HP by 10",
    icon: "❤️",
    cost: 20,
    category: "hp",
    tier: 1,
    prerequisites: [],
    effects: [{ type: "stat_boost", target: "initial_hp", value: 10 }],
  },
  {
    id: "gold_blessing_1",
    name: "Blessing of Wealth I",
    description: "Increases initial Gold by 10%",
    icon: "💰",
    cost: 25,
    category: "gold",
    tier: 1,
    prerequisites: [],
    effects: [
      { type: "stat_boost", target: "initial_gold_multiplier", value: 1.1 },
    ],
  },
  // ... other nodes
];

/**
 * Tier 2: Specialization (Includes Exploration Extension)
 */
export const TIER2_NODES: SkillNode[] = [
  {
    id: "hp_blessing_2",
    name: "Blessing of Life II",
    description: "Increases initial HP by an additional 20",
    icon: "❤️❤️",
    cost: 50,
    category: "hp",
    tier: 2,
    prerequisites: ["hp_blessing_1"],
    effects: [{ type: "stat_boost", target: "initial_hp", value: 20 }],
  },
  // NEW: Exploration Extension
  {
    id: "exploration_extension_1",
    name: "Extended Exploration I",
    description: "Increases Max Exploration Count by +1",
    icon: "⏰",
    cost: 80,
    category: "exploration",
    tier: 2,
    prerequisites: [],
    effects: [
      { type: "exploration_limit", target: "max_explorations", value: 1 },
    ],
  },
  // ... other nodes
];

/**
 * Tier 3: Ultimate (Includes Exploration Extension II)
 */
export const TIER3_NODES: SkillNode[] = [
  {
    id: "hp_blessing_3",
    name: "Blessing of Life III",
    description: "Increases initial HP by an additional 30",
    icon: "❤️❤️❤️",
    cost: 100,
    category: "hp",
    tier: 3,
    prerequisites: ["hp_blessing_2"],
    effects: [{ type: "stat_boost", target: "initial_hp", value: 30 }],
  },
  // NEW: Exploration Extension II
  {
    id: "exploration_extension_2",
    name: "Extended Exploration II",
    description:
      "Increases Max Exploration Count by an additional +2 (Total +3)",
    icon: "⏰⏰",
    cost: 150,
    category: "exploration",
    tier: 3,
    prerequisites: ["exploration_extension_1"],
    effects: [
      { type: "exploration_limit", target: "max_explorations", value: 2 },
    ],
  },
  // ... other nodes
];

export const ALL_SKILL_NODES = [...TIER1_NODES, ...TIER2_NODES, ...TIER3_NODES];
```

---

## 5. Logic Implementation

### 5.1 Node Status Determination

```typescript
// src/camps/facilities/Sanctuary/logic/nodeStatus.ts (Updated)

import type {
  SkillNode,
  NodeStatus,
  SanctuaryProgress,
} from "../../../../types/SanctuaryTypes";

/**
 * Determine node status
 */
export function getNodeStatus(
  node: SkillNode,
  progress: SanctuaryProgress
): NodeStatus {
  // Already unlocked
  if (progress.unlockedNodes.has(node.id)) {
    return "unlocked";
  }

  // Check prerequisites
  const prerequisitesMet = node.prerequisites.every((prereqId) =>
    progress.unlockedNodes.has(prereqId)
  );

  if (!prerequisitesMet) {
    return "locked";
  }

  // Check Soul sufficiency (V2.0: Use totalSouls)
  if (progress.totalSouls < node.cost) {
    return "locked";
  }

  return "available";
}

/**
 * Unlock node
 */
export function unlockNode(
  node: SkillNode,
  progress: SanctuaryProgress
): SanctuaryProgress {
  const status = getNodeStatus(node, progress);

  if (status !== "available") {
    throw new Error(`Cannot unlock node ${node.id}: status is ${status}`);
  }

  return {
    ...progress,
    unlockedNodes: new Set([...progress.unlockedNodes, node.id]),
    totalSouls: progress.totalSouls - node.cost,
  };
}
```

### 5.2 Applying Effects

```typescript
// src/camps/facilities/Sanctuary/logic/applyEffects.ts (Updated)

import type { SanctuaryProgress } from "../../../../types/SanctuaryTypes";
import { ALL_SKILL_NODES } from "../data/SkillTreeData";

/**
 * Calculate total effects of all unlocked nodes
 */
export function calculateTotalEffects(progress: SanctuaryProgress) {
  const effects = {
    initial_hp: 0,
    initial_gold_multiplier: 1.0,
    initial_sword_energy: 0,
    initial_resonance_level: 0,
    initial_summon_slots: 0,
    inventory_size: 0,
    exploration_limit_bonus: 0, // NEW
    special_abilities: new Set<string>(),
  };

  // Accumulate effects of unlocked nodes
  progress.unlockedNodes.forEach((nodeId) => {
    const node = ALL_SKILL_NODES.find((n) => n.id === nodeId);
    if (!node) return;

    node.effects.forEach((effect) => {
      switch (effect.type) {
        case "stat_boost":
          if (effect.target === "initial_hp") {
            effects.initial_hp += effect.value as number;
          } else if (effect.target === "initial_gold_multiplier") {
            effects.initial_gold_multiplier *= effect.value as number;
          }
          // ... other stats
          break;

        // NEW: Exploration Extension
        case "exploration_limit":
          if (effect.target === "max_explorations") {
            effects.exploration_limit_bonus += effect.value as number;
          }
          break;

        case "special_ability":
          effects.special_abilities.add(effect.value as string);
          break;

        case "resource_increase":
          if (effect.target === "inventory_size") {
            effects.inventory_size += effect.value as number;
          }
          break;
      }
    });
  });

  return effects;
}
```

### 5.3 Soul Remnants System (V2.0 - New)

```typescript
// src/camps/facilities/Sanctuary/logic/soulSystem.ts (New)

import type { SanctuaryProgress } from "../../../../types/SanctuaryTypes";

/**
 * Gain souls upon enemy defeat
 */
export function gainSoulFromEnemy(
  progress: SanctuaryProgress,
  enemyType: "minion" | "elite" | "boss",
  isReturnBattle: boolean = false
): SanctuaryProgress {
  const baseSouls: Record<typeof enemyType, number> = {
    minion: 5,
    elite: 15,
    boss: 50,
  };

  let soulsGained = baseSouls[enemyType];

  // Return Battle yields 50%
  if (isReturnBattle) {
    soulsGained = Math.floor(soulsGained * 0.5);
  }

  return {
    ...progress,
    currentRunSouls: progress.currentRunSouls + soulsGained,
  };
}

/**
 * Add to Total upon Survival
 */
export function completeSurvival(
  progress: SanctuaryProgress,
  returnMethod:
    | "return_route"
    | "teleport_normal"
    | "teleport_blessed"
    | "teleport_emergency"
): SanctuaryProgress {
  const multipliers = {
    return_route: 1.0,
    teleport_normal: 0.7,
    teleport_blessed: 0.8,
    teleport_emergency: 0.6,
  };

  const multiplier = multipliers[returnMethod];
  const soulsToAdd = Math.floor(progress.currentRunSouls * multiplier);

  return {
    ...progress,
    totalSouls: progress.totalSouls + soulsToAdd,
    currentRunSouls: 0, // Reset
  };
}

/**
 * Process upon Death
 */
export function handleDeath(progress: SanctuaryProgress): SanctuaryProgress {
  return {
    ...progress,
    currentRunSouls: 0, // Souls for this run are zeroed
    // totalSouls is retained (no change)
  };
}
```

---

## 6. Integration into PlayerContext

```typescript
// src/contexts/PlayerContext.tsx (Major Update)

import type { SanctuaryProgress } from "../types/SanctuaryTypes";
import { calculateTotalEffects } from "../camps/facilities/Sanctuary/logic/applyEffects";
import {
  gainSoulFromEnemy,
  completeSurvival,
  handleDeath,
} from "../camps/facilities/Sanctuary/logic/soulSystem";

export interface Player {
  // ... existing fields

  // V2.0: Sanctuary Updates
  sanctuaryProgress: SanctuaryProgress;

  // NEW: Exploration Limit
  explorationLimit: {
    max: number; // Max exploration count (Default 10 + Bonus)
    current: number; // Current exploration count
  };
}

export const PlayerProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [player, setPlayer] = useState<Player>(() => {
    // Load from save data
    const saved = localStorage.getItem("player");
    if (saved) {
      return JSON.parse(saved);
    }

    return {
      // ... existing defaults
      sanctuaryProgress: {
        unlockedNodes: new Set(),
        currentRunSouls: 0,
        totalSouls: 50, // Initial 50 souls
        explorationLimitBonus: 0,
      },
      explorationLimit: {
        max: 10,
        current: 0,
      },
    };
  });

  // Calculate Initial Stats with Sanctuary Effects
  const getSanctuaryBoostedStats = () => {
    const effects = calculateTotalEffects(player.sanctuaryProgress);

    return {
      initialHp: player.baseHp + effects.initial_hp,
      initialGold: Math.floor(
        player.baseGold * effects.initial_gold_multiplier
      ),
      explorationLimitMax: 10 + effects.exploration_limit_bonus, // NEW
      // ... other stats
    };
  };

  // Gain Souls on Enemy Kill (V2.0 - NEW)
  const gainSouls = (
    enemyType: "minion" | "elite" | "boss",
    isReturnBattle: boolean = false
  ) => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: gainSoulFromEnemy(
        prev.sanctuaryProgress,
        enemyType,
        isReturnBattle
      ),
    }));
  };

  // Handle Survival (V2.0 - NEW)
  const handleSurvival = (
    returnMethod:
      | "return_route"
      | "teleport_normal"
      | "teleport_blessed"
      | "teleport_emergency"
  ) => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: completeSurvival(prev.sanctuaryProgress, returnMethod),
      explorationLimit: {
        ...prev.explorationLimit,
        current: prev.explorationLimit.current + 1,
      },
    }));
  };

  // Handle Death (V2.0 - NEW)
  const handlePlayerDeath = () => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: handleDeath(prev.sanctuaryProgress),
      explorationLimit: {
        ...prev.explorationLimit,
        current: prev.explorationLimit.current + 1,
      },
      // Equipment, Gold, and Magic Stones are lost
      equipment: [],
      gold: 0,
      magicStones: { tiny: 0, small: 0, medium: 0, large: 0, huge: 0 },
    }));
  };

  return (
    <PlayerContext.Provider
      value={{
        player,
        setPlayer,
        gainSouls, // NEW
        handleSurvival, // NEW
        handlePlayerDeath, // NEW
        getSanctuaryBoostedStats,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};
```

---

## 7. Implementation Procedures (Overview)

### Phase 1: Data Structures (Week 1: Day 1-2)

```
□ Update SanctuaryTypes.ts (Add exploration_limit)
□ Update SkillTreeData.ts (Add Exploration Extension skills)
□ Add currentRunSouls / totalSouls to PlayerContext
□ Add explorationLimit to PlayerContext

```

### Phase 2: Logic Implementation (Week 1: Day 3-4)

```
□ nodeStatus.ts (Status determination)
□ applyEffects.ts (Apply effects, including Exploration Ext.)
□ soulSystem.ts (V2.0 - New)
  □ gainSoulFromEnemy (On kill)
  □ completeSurvival (On survival)
  □ handleDeath (On death)
□ Integrate into PlayerContext

```

### Phase 3: UI Implementation (Week 2: Day 1-3)

```
□ Sanctuary.tsx (Main Container)
  □ Display Total Souls and Run Souls
  □ Display Max Exploration Count
□ SkillTree.tsx (Tree display)
□ SkillNode.tsx (Individual nodes)
□ NodeDetailPanel.tsx (Details panel with Exploration count info)

```

### Phase 4: Interaction (Week 2: Day 4-5)

```
□ Implement Long-Press Unlock
□ Progress Ring UI
□ Unlock Effects
□ Special FX for Exploration Extension
□ Sound Effects

```

### Phase 5: Battle System Integration (Week 3)

```
□ Call gainSouls on enemy defeat
□ Call handleSurvival on return
□ Call handlePlayerDeath on death
□ Exploration count tracking

```

---

## 8. Notes

### 8.1 Balance Adjustments

**After Phase 1 Implementation:**

- Adjust Soul acquisition rates.
- Adjust Node costs.
- Adjust Effect magnitudes.
- Adjust value of Exploration Extension.

**Targets:**

- Unlock all Tier 1 in 5-10 runs.
- Unlock most of Tier 2 in 30-50 runs.
- Extended Exploration I is a key strategic choice.
- Extended Exploration II is for high-difficulty players.

### 8.2 Save Data Impact

**Important:** `sanctuaryProgress` must be persistent.

- Save to localStorage.
- Cloud sync (Future).
- Backup in case of data corruption.
- `currentRunSouls` is temporary (during run only).
- `totalSouls` is permanent (always saved).

---

## 9. Reference Documents

```
GAME_DESIGN_MASTER_V2
├── return_system_v2.md (Return System)
└── SANCTUARY_DESIGN_V2 [This Document]
    ├── SkillTreeData.ts
    ├── nodeStatus.ts
    ├── applyEffects.ts
    └── soulSystem.ts (NEW)

```
