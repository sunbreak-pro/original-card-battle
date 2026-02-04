# Blacksmith Detailed Design Document (BLACKSMITH_DESIGN_V1)

## Update History

- V1.0: Initial Draft (Includes Quality Upgrade System, Magic Stone Quality Gacha Elements)
- V1.1: Implementation Status Verified - 3-pattern quality upgrade system fully implemented

---

## 1. Overview

The Blacksmith is a facility for maintaining and enhancing "Equipment," the adventurer's lifeline.

Equipment performance is determined not just by simple numerical increases, but by two axes: **"Quality" and "Upgrade Level"**. Notably, there is a **chance for Quality to increase during upgrades**, featuring a "Gacha element" (RNG mechanic) where the success rate changes based on the type of Magic Stones used.

### Primary Roles

1. **Upgrade**: Increases the equipment's level, improving base stats and AP (Durability). Unlocks special skills at Lv3. **There is a chance for Quality to increase during upgrades.**
2. **Repair**: Restores decreased AP (Durability).
3. **Dismantle**: Destroys unnecessary equipment to recover Magic Stones and materials.

---

## 2. Detailed Functional Specifications

### 2.1 Equipment Data Extension (Quality and Level)

The following properties will be added to all equipment items.

#### 2.1.1 Upgrade Level (Level)

**Range:** Lv0 - Lv3 (MAX)

**Effect:** Base stats (ATK/DEF/Magic) and Max AP increase with each level.

| Level | Stat Mod | AP Mod | Special Effect             |
| ----- | -------- | ------ | -------------------------- |
| Lv0   | ±0%      | ±0%    | None                       |
| Lv1   | +10%     | +20%   | None                       |
| Lv2   | +20%     | +40%   | None                       |
| Lv3   | +30%     | +60%   | Unlockable Skill Activated |

#### 2.1.2 Quality

A system that realizes "different performance even with the same equipment name."

**🎲 New Quality Upgrade System:**

- Equipment is basically generated as `normal`.
- **There is a chance for Quality to rise when upgrading at the Blacksmith.**
- The probability changes based on the type of Magic Stones used (Gacha element).

**Quality Types:**

| Quality Name | English ID | Modifier   | Initial Spawn Rate | Name Display Example |
| ------------ | ---------- | ---------- | ------------------ | -------------------- |
| Poor         | poor       | 0.95 (-5%) | 10%                | Rusty [Item]         |
| Normal       | normal     | 1.00 (±0%) | 70%                | [Item]               |
| Good         | good       | 1.03 (+3%) | 15%                | Tempered [Item]      |
| Master       | master     | 1.05 (+5%) | 5%                 | Masterwork [Item]    |

**Modification Targets:** Numerical parameters like ATK, DEF, Magic, HP (AP is excluded).

---

### 2.2 Upgrade System

Consumes Gold and Magic Stones (Magic Stone Value) to increase the level.

#### 2.2.1 Base Upgrade Costs

Upgrade costs comply with `EQUIPMENT_AND_ITEMS_DESIGN.md`. **Balance adjustments are planned after Phase 1 implementation.**

**Reference Cost Table:**

| Rarity    | Lv0→1            | Lv1→2            | Lv2→3             |
| --------- | ---------------- | ---------------- | ----------------- |
| Common    | 200G + Stone 5   | 400G + Stone 10  | 800G + Stone 20   |
| Rare      | 400G + Stone 10  | 800G + Stone 20  | 1600G + Stone 40  |
| Epic      | 800G + Stone 20  | 1600G + Stone 40 | 3200G + Stone 80  |
| Legendary | 1600G + Stone 40 | 3200G + Stone 80 | 6400G + Stone 160 |

#### 2.2.2 Magic Stone Consumption

Automatically calculates "Magic Stone Items" in the inventory and consumes them starting from the lowest value items (using `InventoryContext.useMagicStones`).

If insufficient, the upgrade is not possible.

---

### 2.3 Quality Upgrade System (New Feature)

#### 2.3.1 Quality Upgrade Mechanism

**Trigger:** Chance to increase quality when upgrading equipment (leveling up).

**Timing:** Quality upgrade check occurs immediately after a successful level up.

**Upgrade Rules:**

- Increases by 1 stage in the order of poor → normal → good → master.
- Does not increase if already `master`.
- Max 1 stage increase per upgrade.

#### 2.3.2 Effects of Magic Stone Types

The **total value** and **type** of Magic Stones used change the Quality Upgrade Probability and Guaranteed Minimum.

**Magic Stone Priority Settings:**

| Upgrade Option  | Stones Used           | Quality Up Rate | Minimum Guarantee  | Extra Cost |
| --------------- | --------------------- | --------------- | ------------------ | ---------- |
| Normal Upgrade  | Auto (Small→Med→Lrg)  | Base Rate       | None               | None       |
| Quality Focused | Medium/Large Priority | High Rate       | `normal` Guarantee | +50% Gold  |
| Max Quality Aim | Large Only            | Highest Rate    | `good` Guarantee   | +100% Gold |

**Base Quality Upgrade Probability (Normal Upgrade):**

| Current Quality | Next Quality | Upgrade Chance |
| --------------- | ------------ | -------------- |
| poor            | normal       | 40%            |
| normal          | good         | 20%            |
| good            | master       | 10%            |

**Quality Focused Upgrade (Priority on Medium+ Stones):**

| Current Quality | Next Quality | Upgrade Chance |
| --------------- | ------------ | -------------- |
| poor            | normal       | 80%            |
| normal          | good         | 40%            |
| good            | master       | 15%            |

**Guarantee:** Post-upgrade quality will be at least `normal`.

**Max Quality Aim (Large Stones Only):**

| Current Quality | Next Quality | Upgrade Chance |
| --------------- | ------------ | -------------- |
| poor            | normal       | 100%           |
| normal          | good         | 60%            |
| good            | master       | 25%            |

**Guarantee:** Post-upgrade quality will be at least `good`.

#### 2.3.3 Magic Stone Value Calculation

```typescript
interface MagicStoneUsage {
  small: number; // Count of Magic Stone (Small)
  medium: number; // Count of Magic Stone (Medium)
  large: number; // Count of Magic Stone (Large)
  totalValue: number; // Total Value
}

// Determine Magic Stone Quality Priority
function getMagicStoneQuality(
  usage: MagicStoneUsage
): "low" | "medium" | "high" {
  const totalValue = usage.totalValue;
  const largeRatio = (usage.large * 350) / totalValue;
  const mediumRatio = (usage.medium * 100) / totalValue;

  if (largeRatio >= 0.5) return "high"; // Large is 50% or more
  if (mediumRatio >= 0.5) return "medium"; // Medium is 50% or more
  return "low"; // Otherwise
}
```

#### 2.3.4 Visuals

**Success:**

- Equipment glows with rainbow colors.
- Sound effect: "Shing!" (Sparkle sound).
- Animation of the quality name changing.
- Message: "Quality Upgraded! poor → normal"

**Failure:**

- Standard upgrade visual only.
- Message: "Quality did not change."

---

### 2.4 Repair System

Recovers AP (Durability) consumed during exploration.

#### 2.4.1 Repair Cost

**Formula:**

```typescript
const REPAIR_COST_PER_AP = 0.5;

const repairCost = (maxAP - currentAP) * REPAIR_COST_PER_AP * rarityMultiplier;
```

**Rarity Multiplier:**

- Common: x1.0
- Rare: x1.5
- Epic: x2.0
- Legendary: x3.0

**Example:**

- Common Equipment (AP 150) reduced to 50:
- `(150 - 50) × 0.5 × 1.0 = 50 G`

- Legendary Equipment (AP 450) reduced to 100:
- `(450 - 100) × 0.5 × 3.0 = 525 G`

#### 2.4.2 Repair UI

**Phase 1: Simple Implementation**

- "Repair All (XXX G)" button only.
- No sliders.

**Phase 2+ Extensions:**

- Partial Repair (e.g., Recover 50%).
- Batch Repair (Repair all equipment in inventory).

---

### 2.5 Dismantle System

Destroys equipment to extract "Magic Stones."

#### 2.5.1 Return Rate

**Base Return Rate (Fixed by Rarity):**

| Rarity    | Return Rate |
| --------- | ----------- |
| Common    | 10%         |
| Rare      | 15%         |
| Epic      | 20%         |
| Legendary | 25%         |

**Calculation Example:**

```typescript
const baseReturn = item.sellPrice * returnRate;

// Example: Rare Equipment (Sell Price 1000G)
// 1000 × 0.15 = 150G worth of Magic Stones

// Convert to Magic Stones
// Magic Stone (Medium) 100G x 1 + Magic Stone (Small) 30G x 1 + Remaining 20G worth
```

#### 2.5.2 Magic Stone Conversion Logic

```typescript
function convertToMagicStones(value: number): MagicStoneReward[] {
  const stones: MagicStoneReward[] = [];
  let remaining = value;

  // Magic Stone (Large): 350G
  while (remaining >= 350) {
    stones.push({ typeId: "magic_stone_large", count: 1 });
    remaining -= 350;
  }

  // Magic Stone (Medium): 100G
  while (remaining >= 100) {
    stones.push({ typeId: "magic_stone_medium", count: 1 });
    remaining -= 100;
  }

  // Magic Stone (Small): 30G
  while (remaining >= 30) {
    stones.push({ typeId: "magic_stone_small", count: 1 });
    remaining -= 30;
  }

  // Fractions returned as Gold (Optional)
  if (remaining > 0) {
    // Round down or return as Gold
  }

  return stones;
}
```

#### 2.5.3 Rarity Bonus

**When dismantling Epic or higher:**

- Low chance (20%) to add 1 Magic Stone (Large).
- Bonus probability increases with Upgrade Level.
- Lv1: +5%
- Lv2: +10%
- Lv3: +15%

**Example:**

```typescript
// Dismantling Epic Lv3 Equipment
const bonusChance = 0.2 + 0.15; // 35%

if (Math.random() < bonusChance) {
  // Acquire 1 additional Magic Stone (Large)
}
```

#### 2.5.4 Dismantle Warning

**Show confirmation dialog under the following conditions:**

- Rarity is Rare or higher
- Upgrade Level is Lv1 or higher
- Quality is Good or higher

```
┌─────────────────────────────────────────────┐
│  ⚠️ WARNING                                 │
│                                             │
│  You are about to dismantle:                │
│  Masterwork Sword God's Blade (Lv3, master) │
│                                             │
│  This action cannot be undone.              │
│  Are you sure?                              │
│                                             │
│  Rewards: Magic Stone (L) x5, (M) x2        │
│                                             │
│  [Dismantle]  [Cancel]                      │
└─────────────────────────────────────────────┘

```

---

## 3. UI/UX Design

### 3.1 Screen Layout

Background is a workshop with a glowing furnace and anvil. A muscular blacksmith is swinging a hammer.

```
┌────────────────────────────────────────────────────────┐
│  ⚒️ The Blacksmith                                     │
├────────────────────────────────────────────────────────┤
│  Gold: 2,400 G      Magic Stone Value: 480 G          │
│                                                        │
│  [Upgrade/Repair] [Dismantle]                          │
│  ═══════════════  ────────                             │
│                                                        │
│  ┌──────────────────┐  ┌────────────────────────────┐ │
│  │ Equipment List    │  │ Selected Equipment          │ │
│  │ [All][Wpn][Armor] │  │ 🗡️ Rusty Iron Sword         │ │
│  │                  │  │ Quality: Poor (-5%)        │ │
│  │ 🗡️ Iron Sword    │  │ Level: 0                   │ │
│  │    Lv0 / Poor   │  │ AP: 45 / 50 (90%)          │ │
│  │    AP: 45/50    │  │                            │ │
│  │                  │  │ 【UPGRADE】Lv0 → Lv1       │ │
│  │ 🛡️ Knight Armor  │  │ ATK: 10 → 11 (+10%)        │ │
│  │    Lv2 / Normal │  │ AP:  50 → 60 (+20%)        │ │
│  │    AP: 10/150⚠️ │  │                            │ │
│  │                  │  │ Cost: 200 G + Stone 5      │ │
│  │ 💍 Ring of Power │  │                            │ │
│  │    Lv0 / Good   │  │ Upgrade Options:            │ │
│  │                  │  │ ○ Normal Upgrade           │ │
│  │ ...              │  │ ○ Quality Focused (+50% G) │ │
│  └──────────────────┘  │ ○ Max Quality Aim (+100% G)│ │
│                        │                            │ │
│                        │ [Upgrade]   [Repair: 25 G] │ │
│                        └────────────────────────────┘ │
│                                                        │
│  [Return to Camp]                                      │
└────────────────────────────────────────────────────────┘

```

### 3.2 Upgrade Options UI

```
┌────────────────────────────────────────┐
│  Select Upgrade Option                   │
├────────────────────────────────────────┤
│                                        │
│  ○ Normal Upgrade                      │
│     Cost: 200 G + Stone 5              │
│     Quality Up Chance: Base            │
│     Guaranteed Min: None               │
│                                        │
│  ○ Quality Focused Upgrade             │
│     Cost: 300 G + Stone 5 (Med Prio)   │
│     Quality Up Chance: High            │
│     Guaranteed Min: Normal             │
│                                        │
│  ○ Max Quality Aim                     │
│     Cost: 400 G + Stone 5 (Lrg Only)   │
│     Quality Up Chance: Highest         │
│     Guaranteed Min: Good               │
│                                        │
│  Owned Stones:                         │
│  Small: 10 (300G)                      │
│  Medium: 3 (300G)                      │
│  Large: 0 (0G)   ← Insufficient!       │
│                                        │
│  [Confirm]  [Cancel]                   │
└────────────────────────────────────────┘

```

### 3.3 Dismantle Tab

```
┌────────────────────────────────────────────────────────┐
│  [Upgrade/Repair] [Dismantle]                          │
│  ────────────────  ═════════                           │
│                                                        │
│  ┌──────────────────┐  ┌────────────────────────────┐ │
│  │ Equipment List    │  │ Dismantle Preview           │ │
│  │ [All][Wpn][Armor] │  │ 🗡️ Iron Sword (Lv0, poor)  │ │
│  │                  │  │                            │ │
│  │ 🗡️ Iron Sword    │  │ Receive:                   │ │
│  │    Lv0 / Poor   │  │ Magic Stone (S) x 1 (30G)  │ │
│  │    Sell: 50G    │  │                            │ │
│  │                  │  │ *10% of Sell Price         │ │
│  │ 🛡️ Knight Armor  │  │                            │ │
│  │    Lv2 / Normal │  │ [Dismantle]                │ │
│  │    Sell: 300G   │  │                            │ │
│  │                  │  │                            │ │
│  └──────────────────┘  └────────────────────────────┘ │
│                                                        │
└────────────────────────────────────────────────────────┘

```

### 3.4 Feedback (Visuals)

#### Upgrade Success

```
Sound: Clang! Clang! (Anvil)
Animation:
  1. Flame effects
  2. Sparks flying
  3. Stats numbers increase in green text

```

#### Quality Upgrade Success (Additional)

```
Sound: Shing! (Special sparkle sound)
Animation:
  1. Equipment glows with rainbow light
  2. Text "Quality Upgraded!" appears
  3. Stage indicator: poor → normal
  4. Screen flashes momentarily

```

#### Lv3 Reached (Skill Unlock)

```
Sound: Rumble... BOOM!
Animation:
  1. Equipment glows intensely
  2. Text "Unlockable Skill Activated!" appears
  3. Skill name is displayed

```

#### Repair

```
Sound: Sizzle... (Quenching)
Animation:
  1. AP bar fills up
  2. Steam effect

```

#### Dismantle

```
Sound: Crunch! Crash!
Animation:
  1. Equipment shatters
  2. Magic Stone icons pop out
  3. Pop-up display in inventory

```

---

## 4. Data Structure Definition

### 4.1 Extension of ItemTypes.ts

```typescript
// Append to src/types/ItemTypes.ts

export type QualityType = "poor" | "normal" | "good" | "master";

export interface EquipmentItem extends Item {
  itemType: "equipment";

  // Blacksmith Extension Properties
  level: 0 | 1 | 2 | 3;
  quality: QualityType;

  // Calculated Stats (Base x Quality x Level Mod)
  stats: {
    atk?: number;
    def?: number;
    magic?: number;
    maxHp?: number;
  };

  // Unlocked Skills (Epic/Legendary Equipment Only)
  unlockedSkills?: EquipmentEffect[];

  // Protection Flag (Phase 2)
  isLocked?: boolean;
}

// Quality Data Definitions
export const QUALITY_MODIFIERS: Record<QualityType, number> = {
  poor: 0.95,
  normal: 1.0,
  good: 1.03,
  master: 1.05,
};

export const QUALITY_NAMES: Record<QualityType, string> = {
  poor: "Rusty",
  normal: "",
  good: "Tempered",
  master: "Masterwork",
};

export const QUALITY_COLORS: Record<QualityType, string> = {
  poor: "#888888",
  normal: "#ffffff",
  good: "#4ade80",
  master: "#fbbf24",
};
```

### 4.2 BlacksmithTypes.ts

```typescript
// src/types/BlacksmithTypes.ts (New File)

import type { EquipmentItem, QualityType } from "./ItemTypes";

/**
 * Upgrade Options
 */
export type UpgradeOption = "normal" | "quality_focused" | "max_quality";

export interface UpgradeConfig {
  option: UpgradeOption;
  goldCost: number;
  magicStoneCost: number;
  qualityUpgradeChances: QualityUpgradeChances;
  guaranteedMinQuality: QualityType | null;
}

/**
 * Quality Upgrade Chances
 */
export interface QualityUpgradeChances {
  poor_to_normal: number;
  normal_to_good: number;
  good_to_master: number;
}

/**
 * Settings per Upgrade Option
 */
export const UPGRADE_OPTIONS: Record<
  UpgradeOption,
  {
    name: string;
    description: string;
    goldMultiplier: number;
    magicStonePreference: "auto" | "medium_preferred" | "large_only";
    qualityUpgradeChances: QualityUpgradeChances;
    guaranteedMinQuality: QualityType | null;
  }
> = {
  normal: {
    name: "Normal Upgrade",
    description: "Standard upgrade. Quality upgrade chances are standard.",
    goldMultiplier: 1.0,
    magicStonePreference: "auto",
    qualityUpgradeChances: {
      poor_to_normal: 0.4,
      normal_to_good: 0.2,
      good_to_master: 0.1,
    },
    guaranteedMinQuality: null,
  },
  quality_focused: {
    name: "Quality Focused",
    description:
      "Prioritizes Magic Stone (M) or higher. High quality upgrade chance.",
    goldMultiplier: 1.5,
    magicStonePreference: "medium_preferred",
    qualityUpgradeChances: {
      poor_to_normal: 0.8,
      normal_to_good: 0.4,
      good_to_master: 0.15,
    },
    guaranteedMinQuality: "normal",
  },
  max_quality: {
    name: "Max Quality Aim",
    description: "Uses Magic Stone (L) only. Highest quality upgrade chance.",
    goldMultiplier: 2.0,
    magicStonePreference: "large_only",
    qualityUpgradeChances: {
      poor_to_normal: 1.0,
      normal_to_good: 0.6,
      good_to_master: 0.25,
    },
    guaranteedMinQuality: "good",
  },
};

/**
 * Repair Cost Settings
 */
export const REPAIR_COST_PER_AP = 0.5;

export const REPAIR_RARITY_MULTIPLIER: Record<string, number> = {
  common: 1.0,
  rare: 1.5,
  epic: 2.0,
  legendary: 3.0,
};

/**
 * Dismantle Return Rates
 */
export const DISMANTLE_RETURN_RATES: Record<string, number> = {
  common: 0.1,
  rare: 0.15,
  epic: 0.2,
  legendary: 0.25,
};

/**
 * Dismantle Bonus (Epic+)
 */
export const DISMANTLE_BONUS_CHANCE = 0.2; // Base 20%
export const DISMANTLE_LEVEL_BONUS = {
  1: 0.05,
  2: 0.1,
  3: 0.15,
};

/**
 * Upgrade Result
 */
export interface UpgradeResult {
  success: boolean;
  newLevel: number;
  oldQuality: QualityType;
  newQuality: QualityType;
  qualityUpgraded: boolean;
  skillUnlocked: boolean;
  newStats: EquipmentItem["stats"];
}

/**
 * Dismantle Result
 */
export interface DismantleResult {
  magicStones: {
    typeId: string;
    count: number;
  }[];
  bonusReceived: boolean;
}
```

---

## 5. Implementation Plan (Overview)

### Phase 1: Introduction of Quality System (Week 1: Day 1-3)

**Task 1.1: Type Definition Extension**

```
□ Add QualityType to ItemTypes.ts
□ Extend EquipmentItem Type
□ Create BlacksmithTypes.ts

```

**Task 1.2: Update Equipment Generation Logic**

```
□ Update equipmentGenerator.ts
  □ Set initial Quality (default: normal)
  □ Set initial Level (Lv0)
  □ Stat calculation logic

```

**Task 1.3: Quality Assignment to Existing Equipment**

```
□ Migration Logic
  □ Assign quality: 'normal' to existing equipment
  □ Assign level: 0
  □ Recalculate stats

```

---

### Phase 2: Upgrade System Implementation (Week 1: Day 4 - Week 2: Day 2)

**Task 2.1: Upgrade Logic**

```
□ src/camps/facilities/Blacksmith/logic/upgradeEquipment.ts
  □ Level up process
  □ Stat recalculation
  □ Quality upgrade check
  □ Skill unlock check

```

**Task 2.2: Quality Upgrade Logic**

```
□ qualityUpgrade.ts
  □ Calculate quality upgrade probability
  □ Determine Magic Stone type
  □ Apply minimum guarantee

```

**Task 2.3: Cost Calculation**

```
□ calculateUpgradeCost.ts
  □ Get base cost
  □ Apply upgrade option multiplier
  □ Magic Stone sufficiency check

```

---

### Phase 3: UI Component Implementation (Week 2: Day 3-5)

**Task 3.1: Blacksmith.tsx**

```
□ Main container
□ Tab switching (Upgrade & Repair / Dismantle)
□ Resource display

```

**Task 3.2: UpgradeTab.tsx**

```
□ Equipment list display
□ Equipment detail panel
□ Upgrade option selection UI
□ Execute Upgrade

```

**Task 3.3: DismantleTab.tsx**

```
□ Dismantleable equipment list
□ Dismantle preview display
□ Confirmation dialog

```

---

### Phase 4: Repair & Dismantle Implementation (Week 3: Day 1-3)

**Task 4.1: Repair System**

```
□ repairEquipment.ts
  □ Cost calculation
  □ AP recovery process

```

**Task 4.2: Dismantle System**

```
□ dismantleEquipment.ts
  □ Magic Stone conversion logic
  □ Bonus check
  □ Item removal

```

---

### Phase 5: Effects and Animation (Week 3: Day 4-5)

**Task 5.1: CSS Implementation**

```
□ Blacksmith.css
□ UpgradeTab.css
□ Animation definitions

```

**Task 5.2: Effect Implementation**

```
□ Upgrade visual effects
□ Quality upgrade visual effects (Special)
□ Skill unlock visual effects
□ Repair/Dismantle visual effects

```

---

## 6. Context API Integration

### 6.1 PlayerContext

```typescript
const { player, useGold, addGold } = usePlayer();

// On Upgrade
if (useGold(upgradeCost.gold)) {
  // Execute Upgrade
}

// On Repair
if (useGold(repairCost)) {
  // Execute Repair
}
```

### 6.2 InventoryContext

```typescript
const { items, updateItem, removeItem, addItem, useMagicStones } =
  useInventory();

// On Upgrade (Consume Magic Stones)
if (useMagicStones(requiredMagicStoneValue, option.magicStonePreference)) {
  // Execute Upgrade
  const upgradedEquipment = upgradeEquipment(equipment);
  updateItem(equipment.id, upgradedEquipment);
}

// On Dismantle
removeItem(equipment.id);
dismantleResult.magicStones.forEach((stone) => {
  addItem(createMagicStoneItem(stone.typeId, stone.count));
});
```

### 6.3 GameStateContext

```typescript
const { returnToCamp } = useGameState();

// Return to Camp
<button onClick={returnToCamp}>Return to Camp</button>;
```

---

## 7. Notes

### 7.1 Phased Introduction of Quality System

**During Shop Implementation (Phase 1):**

- Equipment is generated without quality (or all as normal).
- Level is Lv0.

**During Blacksmith Implementation:**

- Apply Quality system to all equipment.
- Retroactively assign `quality: 'normal'` to existing equipment.
- Update Shop's equipment pack generation logic.

### 7.2 Reflection in Battle

**Important:** Battle calculation logic must be modified to reference the equipment's `stats` property (with quality/level applied) instead of base stats.

```typescript
// ❌ Old Logic
const totalAtk = baseAtk + equipment.baseAtk;

// ✅ New Logic
const totalAtk = baseAtk + (equipment.stats.atk || 0);
```

### 7.3 Balancing Upgrade Costs

**Phase 1:** Use the cost table in the design document.

**After Phase 1 Implementation:** Adjust balance after playtesting.

- Gold cost adjustment.
- Magic Stone cost adjustment.
- Quality upgrade probability adjustment.

### 7.4 Data Persistence

Equipment `level` and `quality` must be persistent:

- Include in save data.
- Manage within InventoryContext.

---

## 8. Reference Documents

```
BASE_CAMP_DESIGN_V1
├── GUILD_DESIGN_V2.1
├── SHOP_DESIGN_V1
└── BLACKSMITH_DESIGN_V1 [This Document]
    ├── equipmentGenerator.ts [Equipment Generation]
    ├── upgradeEquipment.ts [Upgrade Logic]
    ├── qualityUpgrade.ts [Quality Upgrade]
    └── EQUIPMENT_AND_ITEMS_DESIGN.md [Equipment Data]

```

---

---

## 9. Implementation Status (V1.1)

> **Status: FULLY IMPLEMENTED**

The Blacksmith facility has been implemented with all designed features:

### 9.1 Implemented Features

| Feature | Status | Implementation File |
|---------|--------|-------------------|
| Level Upgrade (Lv0-3) | ✅ Complete | `blacksmithLogic.ts` |
| Quality Upgrade (3 options) | ✅ Complete | `blacksmithLogic.ts`, `BlacksmithData.ts` |
| Repair System | ✅ Complete | `blacksmithLogic.ts` |
| Dismantle System | ✅ Complete | `blacksmithLogic.ts` |
| Batch Operations | ✅ Complete | `blacksmithLogic.ts` |

### 9.2 Quality Upgrade Options (Implemented)

All 3 upgrade patterns from the design are implemented:

```typescript
QUALITY_UP_OPTIONS = {
  normal: {
    label: "通常強化",
    costMultiplier: 1.0,
    successRates: { poorToNormal: 0.40, normalToGood: 0.20, goodToMaster: 0.10 }
  },
  qualityFocused: {
    label: "品質重視",
    costMultiplier: 1.5,
    successRates: { poorToNormal: 0.80, normalToGood: 0.40, goodToMaster: 0.15 }
  },
  maxQuality: {
    label: "最高品質狙い",
    costMultiplier: 2.0,
    successRates: { poorToNormal: 1.00, normalToGood: 0.60, goodToMaster: 0.25 }
  }
}
```

### 9.3 Dismantle Returns

Implementation returns **Gold** (not magic stones as primary return):

```typescript
calculateDismantleGold(sellPrice, rarity) // Returns gold based on sell price × rarity rate
```

Bonus magic stones are awarded probabilistically based on rarity and level.

---

**Next Step:** Create detailed Implementation Plan

## Summary

The Blacksmith design is complete:

**Key Features:**

- ✅ Equipment Upgrade (Level 0-3)
- ✅ **Quality Upgrade System (Gacha Element)**
- ✅ **Probability fluctuation based on Magic Stone Quality**
- ✅ Repair System
- ✅ Dismantle System

**New Quality Mechanics:**

1. Chance for Quality to increase during upgrades.
2. Probability and minimum guarantee change based on Magic Stone type.
3. Three options: Normal, Quality Focused, Max Quality Aim.
4. Enhances player anticipation with Gacha elements.
