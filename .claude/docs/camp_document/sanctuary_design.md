Here is the English translation of the design document.

# Sanctuary Detailed Design Document V3.0 (SANCTUARY_DESIGN_V3)

## Revision History

- V2.0: **Fundamental Design Overhaul** - Changed Soul Remnants to an experience point system, added +1 Exploration Count skill, removed roguelite elements (permanent death resets).
- V3.0: **Lives System Integration** - Removed exploration count extension skills, updated soul acquisition (100% on both survival AND death), integrated with lives system, game over resets sanctuary progress.

---

## 1. Overview

The Sanctuary is a facility where players consume **Soul Remnants** to unlock permanent upgrades.

**Major Changes in V3.0:**

```
V2.0 Design:
Gain Souls on Enemy Kill (EXP style) → Add to Total upon Survival → Permanent Upgrades
Integrated with the Exploration Count Limit system

V3.0 Design:
Gain Souls on Enemy Kill (EXP style) → Add to Total (100% on BOTH survival AND death)
Integrated with the Lives System (残機システム)
Game Over (Lives = 0) → Complete Sanctuary Reset (only achievements persist)

```

### Key Roles

1. **Permanent Upgrades**: Improving basic player stats that persist across runs (until game over).
2. **Build Diversity**: Selecting growth directions via the skill tree.
3. **Progress Visualization**: Feeling growth through unlocked nodes.
4. **Risk Mitigation**: Souls are always saved (100%), encouraging exploration even in risky situations.

---

## 2. Detailed Functional Specifications

### 2.1 Soul Remnants - V2.0 Major Changes

#### 2.1.1 Acquisition Method (Experience System)

**Change in V3.0:**

```
V2.0: Gained on Enemy Kill, Added to Total only on Survival
V3.0: Gained on Enemy Kill, Added to Total on BOTH Survival AND Death (100%)

```

**Acquisition Timing:**

| Timing        | Amount        | Notes                           |
| ------------- | ------------- | ------------------------------- |
| Minion Kill   | 5 Souls       | Added immediately during combat |
| Elite Kill    | 15 Souls      | Elite enemies / Mid-bosses      |
| Boss Kill     | 50 Souls      | Floor Bosses                    |
| Return Battle | 100% (same)   | V3.0: No reduction on return    |

**Important Mechanism (V3.0):**

```typescript
// Souls gained in this specific run (Temporary)
currentRunSouls: number;

// Accumulated Souls (Permanent until Game Over)
totalSouls: number;

// Upon Survival (V3.0: 100% always)
totalSouls += currentRunSouls;  // 100% acquisition
currentRunSouls = 0;

// Upon Death (V3.0: ALSO 100% - major change!)
totalSouls += currentRunSouls;  // 100% acquisition even on death
currentRunSouls = 0;
lives--;  // Lose 1 life

// Upon Game Over (Lives = 0)
totalSouls = 0;  // Complete reset
unlockedNodes = [];  // All sanctuary progress lost
// Only achievements persist

```

#### 2.1.2 Survival vs. Death Processing (V3.0 - Major Change)

**Case: Survival**

```
Defeat Enemy → Gain Souls (currentRunSouls)
  ↓
Survive (Teleport Stone or Return Route)
  ↓
Acquired Souls → 100% Added to Total (V3.0: No multiplier)

V3.0 Changes:
- Teleport Stone: Unified to 1 type with 100% reward
- Return Route: 100% reward (unchanged)
- No reduction penalties

Example: Gained 100 Souls this run, used Teleport Stone
→ 100 Souls added to Total (100%)

```

**Case: Death (V3.0 - Major Change)**

```
Defeat Enemy → Gain Souls (currentRunSouls)
  ↓
Death
  ↓
V3.0 NEW: Souls gained this run → 100% Added to Total!
Lives → Decrease by 1
All Items/Equipment → Lost

Example: Gained 100 Souls this run, Died
→ 100 Souls ARE added to Total (major change from V2.0!)
→ Lives: 3 → 2
→ All items and equipment lost

```

**Case: Game Over (Lives = 0)**

```
Lives reach 0
  ↓
COMPLETE RESET:
- totalSouls → 0
- unlockedNodes → Empty
- All sanctuary progress → Lost
- Only achievements persist

```

**Properties:**

- Souls are **always saved at 100%** (both survival and death).
- Death penalty is item/equipment loss + life decrease, NOT soul loss.
- Sanctuary progress is lost on game over.
- Can only be used in the Sanctuary.

**Initial Possession:**

- New Player: 50 Souls (For tutorial)
- After Game Over: 50 Souls (Same starting point)

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
  [+10] [+20] [+10%][+20%] [Bag] [Soul+]
    │           │         Expand   │
  [+30]       [+30%]        │     [Soul++]
                         [Adv.]

```

**Features:**

- Extends in 4 directions from the center.
- Each direction has a theme (HP / Gold / Combat / Utility).
- Higher tier nodes are more powerful but cost more.
- **V3.0 Note:** The design intended to remove Exploration Extension skills, but `SanctuaryData.ts` still contains `extended_exploration_1` and `extended_exploration_2` nodes. These remain in code alongside the Lives system.

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
| Soul Resonance I           | ✨   | Soul Remnants Gain +10%          | 50   | -                    |

**Tier 3 (Ultimate Upgrades): Cost 100-150 Souls**

| Node Name                   | Icon   | Effect                              | Cost | Prerequisite          |
| --------------------------- | ------ | ----------------------------------- | ---- | --------------------- |
| Blessing of Life III        | ❤️❤️❤️ | Initial HP +30                      | 100  | Blessing of Life II   |
| Blessing of Wealth III      | 💰💰💰 | Initial Gold +30%                   | 100  | Blessing of Wealth II |
| Indomitable Will            | 🛡️     | Survive with 1 HP once per run      | 120  | Blessing of Life II   |
| Soul Resonance II           | ✨✨   | Soul Remnants Gain +20% (Total +30%)| 100  | Soul Resonance I      |
| True Appraisal              | 👁️‍🗨️     | Displays hidden equipment effects   | 90   | Eye of Appraisal      |
| Fortune's Favor             | 🍀    | +10% chance for rare drops          | 110  | Blessing of Wealth II |

#### 2.2.3 Soul Resonance Skills (V3.0 Replacement)

**Soul Acquisition Enhancement Skills:**

```
[Tier 2] Soul Resonance I
Cost: 50 Souls
Effect: Soul Remnants Gain +10%
Prerequisite: None

[Tier 3] Soul Resonance II
Cost: 100 Souls
Effect: Soul Remnants Gain +20% (Total +30%)
Prerequisite: Soul Resonance I

[Example Effects]
Default: 5 Souls per Minion
After unlocking Soul Resonance I: 5.5 Souls (rounded)
After unlocking Soul Resonance II: 6.5 Souls (rounded)

```

**Strategic Value (V3.0):**

- Accelerates sanctuary upgrade progression.
- Since souls are now saved on death too, this is pure value.
- Helps rebuild faster after game over.
- Moderate cost, steady value accumulation.

> **V3.0 Note:** The design intended to remove Exploration Extension skills in favor of the Lives System. However, `SanctuaryData.ts` still contains `extended_exploration_1` (Tier 2, 80 Souls) and `extended_exploration_2` (Tier 3, 150 Souls) nodes. These coexist with the Lives system in the current codebase.

> **Implementation Note:** `SanctuaryData.ts` contains **25 skill nodes total** (7 Tier 1 + 12 Tier 2 + 6 Tier 3), including 5 Mage element enhancement nodes (`fire_enhancement`, `ice_enhancement`, `lightning_enhancement`, `dark_enhancement`, `light_enhancement`) at Tier 2, 60 Souls each, requiring `mage_insight`.

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
│  Soul Remnants: Total 650 / This Run +85               │
│  Lives: ❤️❤️❤️ (3/3)                                  │  ← V3.0
│                                                        │
│              [Skill Tree Display Area]                 │
│                                                        │
│                      [Center]                          │
│                     (Unlocked)                         │
│                        │                               │
│        ┌───────────────┼───────────────┐               │
│        │               │               │               │
│   [Life Bless I]  [Wealth Bless I] [Soul Reson. I]     │  ← V3.0
│   (Unlocked ✨)   (Available 💫)   (Available 💫)      │
│        │               │           50 Souls            │
│   [Life Bless II] [Wealth Bless II] [Soul Reson. II]   │  ← V3.0
│   (Locked 🔒)     (Locked 🔒)      (Locked 🔒)         │
│                                    100 Souls           │
│                                                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │ [Selected Node Details]                           │  │
│  │ ✨ Soul Resonance I                              │  │  ← V3.0
│  │                                                  │  │
│  │ Effect: Increases Soul Remnants gain by +10%     │  │
│  │         (Current 1.0x -> 1.1x)                   │  │
│  │                                                  │  │
│  │ Cost: 50 Soul Remnants                            │  │
│  │ Currently Held: 650 (Sufficient)                  │  │
│  │                                                  │  │
│  │ Prerequisite: None                                │  │
│  │                                                  │  │
│  │ * Accelerates sanctuary progression.              │  │
│  │   Souls are always saved (even on death).         │  │
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
"Unlocked Soul Resonance I!"
"Soul gain increased 1.0x -> 1.1x!"  ← V3.0

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
  5. Special effect if Soul multiplier increased. ← V3.0

```

**Soul Remnants Gain (V3.0):**

```
On Enemy Kill:
  1. Soul orb appears from enemy.
  2. Absorbed by player.
  3. "+5 Soul Remnants" text (or +5.5 with Soul Resonance).
  4. Top right "This Run" counter updates.

On Survival:
  1. Large Soul Orb appears.
  2. Number counts up.
  3. "+85 Soul Remnants (Total 650 -> 735)" displayed.
  4. Sparkle effect.

On Death (V3.0 Change):
  1. Soul Orb glows warmly (NOT shatters).
  2. "+85 Soul Remnants saved!" ← V3.0: Souls ARE saved!
  3. "Total 650 -> 735 Souls"
  4. "Lives: ❤️❤️❤️ -> ❤️❤️"
  5. Bittersweet visual (loss + gain).

On Game Over (Lives = 0):
  1. All Soul Orbs shatter dramatically.
  2. "All sanctuary progress lost..."
  3. "Starting anew with 50 Souls."
  4. "Achievements preserved."
  5. Dark/Reset visual.

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
    | "soul_multiplier"; // V3.0: Replaced exploration_limit
  target: string; // 'initial_hp', 'initial_gold', 'soul_gain_multiplier', etc.
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

  // V3.0: Soul Remnants System
  currentRunSouls: number; // Souls gained in this run (always added to total)
  totalSouls: number; // Total accumulated souls (reset on game over)

  // V3.0: Soul gain multiplier from upgrades
  soulGainMultiplier: number; // Default 1.0, increased by Soul Resonance skills
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
  // V3.0: Soul Resonance (Replaced Exploration Extension)
  {
    id: "soul_resonance_1",
    name: "Soul Resonance I",
    description: "Increases Soul Remnants gain by +10%",
    icon: "✨",
    cost: 50,
    category: "utility",
    tier: 2,
    prerequisites: [],
    effects: [
      { type: "soul_multiplier", target: "soul_gain_multiplier", value: 0.1 },
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
  // V3.0: Soul Resonance II (Replaced Exploration Extension II)
  {
    id: "soul_resonance_2",
    name: "Soul Resonance II",
    description:
      "Increases Soul Remnants gain by an additional +20% (Total +30%)",
    icon: "✨✨",
    cost: 100,
    category: "utility",
    tier: 3,
    prerequisites: ["soul_resonance_1"],
    effects: [
      { type: "soul_multiplier", target: "soul_gain_multiplier", value: 0.2 },
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
    soul_gain_multiplier: 1.0, // V3.0: Replaced exploration_limit_bonus
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

        // V3.0: Soul Gain Multiplier (Replaced Exploration Extension)
        case "soul_multiplier":
          if (effect.target === "soul_gain_multiplier") {
            effects.soul_gain_multiplier += effect.value as number;
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

### 5.3 Soul Remnants System (V3.0 - Updated)

```typescript
// src/camps/facilities/Sanctuary/logic/soulSystem.ts (V3.0 Updated)

import type { SanctuaryProgress } from "../../../../types/SanctuaryTypes";
import { calculateTotalEffects } from "./applyEffects";

/**
 * Gain souls upon enemy defeat
 * V3.0: Apply soul gain multiplier from upgrades
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

  // V3.0: Apply soul gain multiplier
  const effects = calculateTotalEffects(progress);
  let soulsGained = Math.floor(baseSouls[enemyType] * effects.soul_gain_multiplier);

  // V3.0: Return Battle now yields 100% (same as normal)
  // No reduction for return battles

  return {
    ...progress,
    currentRunSouls: progress.currentRunSouls + soulsGained,
  };
}

/**
 * Add to Total upon Survival
 * V3.0: Always 100% (teleport stone unified)
 */
export function completeSurvival(
  progress: SanctuaryProgress,
  returnMethod: "return_route" | "teleport_stone"  // V3.0: Unified stone type
): SanctuaryProgress {
  // V3.0: All return methods yield 100%
  const soulsToAdd = progress.currentRunSouls;

  return {
    ...progress,
    totalSouls: progress.totalSouls + soulsToAdd,
    currentRunSouls: 0, // Reset
  };
}

/**
 * Process upon Death
 * V3.0 MAJOR CHANGE: Souls are ALSO added to total on death!
 */
export function handleDeath(progress: SanctuaryProgress): SanctuaryProgress {
  // V3.0: Souls gained this run ARE added to total (100%)
  const soulsToAdd = progress.currentRunSouls;

  return {
    ...progress,
    totalSouls: progress.totalSouls + soulsToAdd,
    currentRunSouls: 0,
    // Note: Lives decrement is handled in PlayerContext, not here
  };
}

/**
 * Process Game Over (Lives = 0)
 * V3.0 NEW: Complete sanctuary reset
 */
export function handleGameOver(progress: SanctuaryProgress): SanctuaryProgress {
  return {
    unlockedNodes: new Set(),  // All progress lost
    currentRunSouls: 0,
    totalSouls: 50,  // Reset to starting amount
    soulGainMultiplier: 1.0,  // Reset
  };
}
```

---

## 6. Integration into PlayerContext

```typescript
// src/contexts/PlayerContext.tsx (V3.0 Major Update)

import type { SanctuaryProgress } from "../types/SanctuaryTypes";
import { calculateTotalEffects } from "../camps/facilities/Sanctuary/logic/applyEffects";
import {
  gainSoulFromEnemy,
  completeSurvival,
  handleDeath,
  handleGameOver,
} from "../camps/facilities/Sanctuary/logic/soulSystem";

export interface Player {
  // ... existing fields

  // V3.0: Sanctuary Updates
  sanctuaryProgress: SanctuaryProgress;

  // V3.0: Lives System (Replaced Exploration Limit)
  lives: {
    max: number; // Lives cap (2 for Hard, 3 for Normal/Easy)
    current: number; // Current lives remaining
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
        soulGainMultiplier: 1.0,
      },
      // V3.0: Lives system (default Normal difficulty)
      lives: {
        max: 3,
        current: 3,
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
      soulGainMultiplier: effects.soul_gain_multiplier, // V3.0
      // ... other stats
    };
  };

  // Gain Souls on Enemy Kill (V3.0)
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

  // Handle Survival (V3.0: Simplified)
  const handleSurvival = (
    returnMethod: "return_route" | "teleport_stone"  // V3.0: Unified stone type
  ) => {
    setPlayer((prev) => ({
      ...prev,
      sanctuaryProgress: completeSurvival(prev.sanctuaryProgress, returnMethod),
      // V3.0: Lives do NOT decrease on successful return
    }));
  };

  // Handle Death (V3.0: Major changes)
  const handlePlayerDeath = () => {
    setPlayer((prev) => {
      const newLives = prev.lives.current - 1;

      // Check for game over
      if (newLives <= 0) {
        return handlePlayerGameOver(prev);
      }

      return {
        ...prev,
        // V3.0: Souls ARE added to total on death
        sanctuaryProgress: handleDeath(prev.sanctuaryProgress),
        lives: {
          ...prev.lives,
          current: newLives,
        },
        // V3.0: ALL items/equipment lost (including brought items)
        equipment: [],
        inventory: [],
        gold: 0,
        magicStones: { tiny: 0, small: 0, medium: 0, large: 0, huge: 0 },
      };
    });
  };

  // Handle Game Over (V3.0: NEW)
  const handlePlayerGameOver = (prev: Player): Player => {
    return {
      ...prev,
      // Complete sanctuary reset
      sanctuaryProgress: handleGameOver(prev.sanctuaryProgress),
      // Reset lives
      lives: {
        ...prev.lives,
        current: prev.lives.max,
      },
      // Reset all progress
      equipment: [],  // Initial equipment only
      inventory: [],
      gold: 100,  // Initial gold
      magicStones: { tiny: 0, small: 0, medium: 0, large: 0, huge: 0 },
      deck: getInitialDeck(),  // Reset to initial deck
      // NOTE: Achievements are NOT reset (handled separately)
    };
  };

  return (
    <PlayerContext.Provider
      value={{
        player,
        setPlayer,
        gainSouls,
        handleSurvival,
        handlePlayerDeath,
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
□ Update SanctuaryTypes.ts (Add soul_multiplier, remove exploration_limit)
□ Update SkillTreeData.ts (Replace Exploration Extension with Soul Resonance)
□ Add currentRunSouls / totalSouls to PlayerContext
□ Add lives system to PlayerContext (replace explorationLimit)

```

### Phase 2: Logic Implementation (Week 1: Day 3-4)

```
□ nodeStatus.ts (Status determination)
□ applyEffects.ts (Apply effects, including Soul Resonance)
□ soulSystem.ts (V3.0 - Updated)
  □ gainSoulFromEnemy (On kill, with multiplier)
  □ completeSurvival (On survival, 100%)
  □ handleDeath (On death, V3.0: 100% souls saved!)
  □ handleGameOver (On game over, complete reset)
□ Integrate into PlayerContext

```

### Phase 3: UI Implementation (Week 2: Day 1-3)

```
□ Sanctuary.tsx (Main Container)
  □ Display Total Souls and Run Souls
  □ Display Lives (❤️ icons)
□ SkillTree.tsx (Tree display)
□ SkillNode.tsx (Individual nodes)
□ NodeDetailPanel.tsx (Details panel with Soul Resonance info)

```

### Phase 4: Interaction (Week 2: Day 4-5)

```
□ Implement Long-Press Unlock
□ Progress Ring UI
□ Unlock Effects
□ Special FX for Soul Resonance
□ Sound Effects

```

### Phase 5: Battle System Integration (Week 3)

```
□ Call gainSouls on enemy defeat (with multiplier)
□ Call handleSurvival on return
□ Call handlePlayerDeath on death (V3.0: souls saved + lives decrease)
□ Call handleGameOver on lives=0
□ Lives tracking and display

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
- Soul Resonance I accelerates mid-game progression.
- Soul Resonance II is for players aiming for quick rebuilds.

### 8.2 Save Data Impact

**Important:** `sanctuaryProgress` persistence rules changed in V3.0.

- Save to localStorage (until game over).
- Cloud sync (Future).
- Backup in case of data corruption.
- `currentRunSouls` is temporary (during run only).
- `totalSouls` persists across runs BUT resets on game over.
- V3.0: All sanctuary progress resets on game over (lives=0).

---

## 9. Reference Documents

```
GAME_DESIGN_MASTER_V3
├── return_system_v3.md (Return System with unified teleport stone)
├── dungeon_exploration_ui_v3.md (Lives system UI)
└── SANCTUARY_DESIGN_V3 [This Document]
    ├── SkillTreeData.ts (Soul Resonance skills)
    ├── nodeStatus.ts
    ├── applyEffects.ts (soul_multiplier)
    └── soulSystem.ts (V3.0: 100% souls on death, game over reset)

```
