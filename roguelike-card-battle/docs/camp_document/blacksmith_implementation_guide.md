# Blacksmith Implementation Guide (BLACKSMITH_IMPLEMENTATION_GUIDE_V1)

## 0. Prerequisites

### 0.1 Tasks that must be completed

- ✅ BaseCamp Overall Design (Context API implemented)
- ✅ Item Type System Introduction
- ✅ InventoryContext Implementation
- ✅ Shop Implementation (Equipment generation logic exists)

### 0.2 Dependencies

```
ItemTypes (Equipment Extension)
  ↓
equipmentGenerator (Quality & Level assignment)
  ↓
InventoryContext (Equipment management, Magic Stone consumption)
  ↓
Blacksmith Components (UpgradeTab, DismantleTab)

```

---

## Phase 1: Introducing the Quality System (Week 1: Day 1-3)

### Task 1.1: Extension of ItemTypes.ts

**Priority:** 🔴 Highest

```typescript
// src/types/ItemTypes.ts (Extended)

export type QualityType = "poor" | "normal" | "good" | "master";

export interface EquipmentItem extends Item {
  itemType: "equipment";

  // ✨ Blacksmith Extension Properties
  level: 0 | 1 | 2 | 3;
  quality: QualityType;

  // Calculated Stats (Base x Quality x Level Modifier)
  stats: {
    atk?: number;
    def?: number;
    magic?: number;
    maxHp?: number;
    maxAp?: number;
  };

  // Base Stats (For calculation)
  baseStats: {
    atk?: number;
    def?: number;
    magic?: number;
    maxHp?: number;
    maxAp?: number;
  };

  // Unlocked Skills (Epic/Legendary equipment only)
  unlockedSkills?: EquipmentEffect[];

  // Protection Flag (Phase 2)
  isLocked?: boolean;
}

// Quality Data Definition
export const QUALITY_MODIFIERS: Record<QualityType, number> = {
  poor: 0.95,
  normal: 1.0,
  good: 1.03,
  master: 1.05,
};

export const QUALITY_NAMES: Record<QualityType, string> = {
  poor: "Rusted",
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

// Level Modifiers
export const LEVEL_STAT_MODIFIERS: Record<number, number> = {
  0: 0.0,
  1: 0.1,
  2: 0.2,
  3: 0.3,
};

export const LEVEL_AP_MODIFIERS: Record<number, number> = {
  0: 0.0,
  1: 0.2,
  2: 0.4,
  3: 0.6,
};
```

**✅ Completion Check:**

- [ ] QualityType added
- [ ] EquipmentItem extended
- [ ] Constants defined
- [ ] No compilation errors

---

### Task 1.2: Creation of BlacksmithTypes.ts

```bash
mkdir -p src/types

```

```typescript
// src/types/BlacksmithTypes.ts (New)

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
 * Quality Upgrade Probabilities
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
    description: "Basic upgrade. Standard chance for quality improvement.",
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
      "Prioritizes Medium Magic Stones. High chance for quality improvement.",
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
    name: "Masterwork Attempt",
    description:
      "Uses only Large Magic Stones. Highest chance for quality improvement.",
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
 * Base Upgrade Costs (By Rarity & Level)
 */
export const BASE_UPGRADE_COSTS: Record<
  string,
  Record<number, { gold: number; magicStone: number }>
> = {
  common: {
    1: { gold: 200, magicStone: 5 },
    2: { gold: 400, magicStone: 10 },
    3: { gold: 800, magicStone: 20 },
  },
  rare: {
    1: { gold: 400, magicStone: 10 },
    2: { gold: 800, magicStone: 20 },
    3: { gold: 1600, magicStone: 40 },
  },
  epic: {
    1: { gold: 800, magicStone: 20 },
    2: { gold: 1600, magicStone: 40 },
    3: { gold: 3200, magicStone: 80 },
  },
  legendary: {
    1: { gold: 1600, magicStone: 40 },
    2: { gold: 3200, magicStone: 80 },
    3: { gold: 6400, magicStone: 160 },
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
 * Dismantle Bonus (Epic or higher)
 */
export const DISMANTLE_BONUS_CHANCE = 0.2;
export const DISMANTLE_LEVEL_BONUS: Record<number, number> = {
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

**✅ Completion Check:**

- [ ] BlacksmithTypes.ts created
- [ ] All type definitions completed
- [ ] Constant tables defined

---

### Task 1.3: Update equipmentGenerator.ts

```typescript
// src/items/utils/equipmentGenerator.ts (Update)

import type {
  EquipmentItem,
  EquipmentSlot,
  QualityType,
  QUALITY_MODIFIERS,
} from "../../types/ItemTypes";

/**
 * Roll for Quality
 */
export function rollQuality(): QualityType {
  const roll = Math.random();

  if (roll < 0.1) return "poor"; // 10%
  if (roll < 0.8) return "normal"; // 70%
  if (roll < 0.95) return "good"; // 15%
  return "master"; // 5%
}

/**
 * Calculate Stats (Apply Quality x Level modifiers)
 */
export function calculateEquipmentStats(
  baseStats: EquipmentItem["baseStats"],
  quality: QualityType,
  level: number
): EquipmentItem["stats"] {
  const qualityMod = QUALITY_MODIFIERS[quality];
  const levelStatMod = LEVEL_STAT_MODIFIERS[level];
  const levelApMod = LEVEL_AP_MODIFIERS[level];

  return {
    atk: baseStats.atk
      ? Math.floor(baseStats.atk * qualityMod * (1 + levelStatMod))
      : undefined,
    def: baseStats.def
      ? Math.floor(baseStats.def * qualityMod * (1 + levelStatMod))
      : undefined,
    magic: baseStats.magic
      ? Math.floor(baseStats.magic * qualityMod * (1 + levelStatMod))
      : undefined,
    maxHp: baseStats.maxHp
      ? Math.floor(baseStats.maxHp * qualityMod * (1 + levelStatMod))
      : undefined,
    maxAp: baseStats.maxAp
      ? Math.floor(baseStats.maxAp * (1 + levelApMod)) // AP is not affected by quality
      : undefined,
  };
}

/**
 * Generate Random Equipment (With Quality & Level)
 */
export function createRandomEquipment(
  slot: EquipmentSlot,
  rarity: "common" | "rare" | "epic" | "legendary"
): EquipmentItem {
  const template = getEquipmentTemplate(slot, rarity); // Implement later

  // ✨ Initialize Quality and Level
  const quality = rollQuality();
  const level = 0; // Always generate at Lv0

  // ✨ Calculate Stats
  const stats = calculateEquipmentStats(template.baseStats, quality, level);

  return {
    id: generateUniqueId(),
    typeId: template.id,
    name: template.name,
    description: template.description,
    itemType: "equipment",
    icon: template.icon,
    equipmentSlot: slot,

    // ✨ Blacksmith Extension Properties
    level: level,
    quality: quality,
    baseStats: template.baseStats,
    stats: stats,

    durability: stats.maxAp || 100,
    maxDurability: stats.maxAp || 100,
    effects: template.effects,
    rarity: rarity,
    sellPrice: template.sellPrice,
    canSell: true,
    canDiscard: false,
    unlockedSkills: template.unlockedSkills,
  };
}
```

**✅ Completion Check:**

- [ ] rollQuality implemented
- [ ] calculateEquipmentStats implemented
- [ ] createRandomEquipment updated
- [ ] All equipment is generated with Quality and Level

---

### Task 1.4: Migration for Existing Equipment

```typescript
// src/items/utils/equipmentMigration.ts (New)

import type { Item, EquipmentItem } from "../../types/ItemTypes";
import { calculateEquipmentStats } from "./equipmentGenerator";

/**
 * Add Quality and Level to existing equipment
 */
export function migrateEquipmentToV2(item: Item): EquipmentItem | Item {
  if (item.itemType !== "equipment") {
    return item; // Leave non-equipment as is
  }

  const equipment = item as any;

  // Skip if already migrated
  if ("quality" in equipment && "level" in equipment) {
    return equipment as EquipmentItem;
  }

  // ✨ Add Quality and Level
  const quality: QualityType = "normal"; // Existing equipment defaults to normal
  const level = 0;

  // If baseStats don't exist, treat current stats as baseStats
  const baseStats = equipment.baseStats || {
    atk: equipment.atk,
    def: equipment.def,
    magic: equipment.magic,
    maxHp: equipment.maxHp,
    maxAp: equipment.maxDurability,
  };

  // Recalculate stats
  const stats = calculateEquipmentStats(baseStats, quality, level);

  return {
    ...equipment,
    level: level,
    quality: quality,
    baseStats: baseStats,
    stats: stats,
    itemType: "equipment",
  } as EquipmentItem;
}

/**
 * Migrate entire inventory
 */
export function migrateInventory(items: Item[]): Item[] {
  return items.map((item) => migrateEquipmentToV2(item));
}
```

**Integrate into InventoryContext:**

```typescript
// src/contexts/InventoryContext.tsx (Modification)

import { migrateInventory } from "../items/utils/equipmentMigration";

export const InventoryProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [items, setItems] = useState<Item[]>(() => {
    const stored = localStorage.getItem("inventory");
    if (stored) {
      const parsedItems = JSON.parse(stored);
      // ✨ Execute Migration
      return migrateInventory(parsedItems);
    }
    return [];
  });

  // ... remaining code
};
```

**✅ Completion Check:**

- [ ] migrateEquipmentToV2 implemented
- [ ] Migration executed in InventoryContext
- [ ] `quality` and `level` added to existing equipment

---

## Phase 2: Upgrade System Implementation (Week 1: Day 4 - Week 2: Day 2)

### Task 2.1: Create upgradeEquipment.ts

```bash
mkdir -p src/camps/facilities/Blacksmith/logic

```

```typescript
// src/camps/facilities/Blacksmith/logic/upgradeEquipment.ts (New)

import type { EquipmentItem } from "../../../../types/ItemTypes";
import type {
  UpgradeResult,
  UpgradeOption,
} from "../../../../types/BlacksmithTypes";
import { calculateEquipmentStats } from "../../../../items/utils/equipmentGenerator";
import { rollQualityUpgrade } from "./qualityUpgrade";
import { UPGRADE_OPTIONS } from "../../../../types/BlacksmithTypes";

/**
 * Upgrade Equipment
 */
export function upgradeEquipment(
  equipment: EquipmentItem,
  option: UpgradeOption
): UpgradeResult {
  const currentLevel = equipment.level;
  const newLevel = Math.min(currentLevel + 1, 3) as 0 | 1 | 2 | 3;

  if (newLevel === currentLevel) {
    return {
      success: false,
      newLevel: currentLevel,
      oldQuality: equipment.quality,
      newQuality: equipment.quality,
      qualityUpgraded: false,
      skillUnlocked: false,
      newStats: equipment.stats,
    };
  }

  // ✨ Check Quality Upgrade
  const upgradeConfig = UPGRADE_OPTIONS[option];
  const qualityUpgradeResult = rollQualityUpgrade(
    equipment.quality,
    upgradeConfig.qualityUpgradeChances,
    upgradeConfig.guaranteedMinQuality
  );

  const newQuality = qualityUpgradeResult.newQuality;
  const qualityUpgraded = qualityUpgradeResult.upgraded;

  // Recalculate Stats
  const newStats = calculateEquipmentStats(
    equipment.baseStats,
    newQuality,
    newLevel
  );

  // Check Skill Unlock
  const skillUnlocked = newLevel === 3 && !!equipment.unlockedSkills;

  return {
    success: true,
    newLevel: newLevel,
    oldQuality: equipment.quality,
    newQuality: newQuality,
    qualityUpgraded: qualityUpgraded,
    skillUnlocked: skillUnlocked,
    newStats: newStats,
  };
}

/**
 * Apply Upgrade Result to Equipment
 */
export function applyUpgradeResult(
  equipment: EquipmentItem,
  result: UpgradeResult
): EquipmentItem {
  return {
    ...equipment,
    level: result.newLevel,
    quality: result.newQuality,
    stats: result.newStats,
    durability: result.newStats.maxAp || equipment.durability,
    maxDurability: result.newStats.maxAp || equipment.maxDurability,
  };
}
```

**✅ Completion Check:**

- [ ] upgradeEquipment implemented
- [ ] applyUpgradeResult implemented
- [ ] Level-up logic is correct

---

### Task 2.2: Create qualityUpgrade.ts

```typescript
// src/camps/facilities/Blacksmith/logic/qualityUpgrade.ts (New)

import type { QualityType } from "../../../../types/ItemTypes";
import type { QualityUpgradeChances } from "../../../../types/BlacksmithTypes";

/**
 * Quality Upgrade Result
 */
export interface QualityUpgradeResult {
  upgraded: boolean;
  newQuality: QualityType;
}

/**
 * Quality Order
 */
const QUALITY_ORDER: QualityType[] = ["poor", "normal", "good", "master"];

/**
 * Get Next Quality
 */
function getNextQuality(current: QualityType): QualityType | null {
  const index = QUALITY_ORDER.indexOf(current);
  if (index === -1 || index === QUALITY_ORDER.length - 1) {
    return null; // Already max quality
  }
  return QUALITY_ORDER[index + 1];
}

/**
 * Roll for Quality Upgrade
 */
export function rollQualityUpgrade(
  currentQuality: QualityType,
  chances: QualityUpgradeChances,
  guaranteedMinQuality: QualityType | null
): QualityUpgradeResult {
  const nextQuality = getNextQuality(currentQuality);

  // Already max quality
  if (!nextQuality) {
    return {
      upgraded: false,
      newQuality: currentQuality,
    };
  }

  // Get upgrade chance
  let upgradeChance = 0;
  switch (currentQuality) {
    case "poor":
      upgradeChance = chances.poor_to_normal;
      break;
    case "normal":
      upgradeChance = chances.normal_to_good;
      break;
    case "good":
      upgradeChance = chances.good_to_master;
      break;
  }

  // Roll
  const roll = Math.random();
  let resultQuality = currentQuality;
  let upgraded = false;

  if (roll < upgradeChance) {
    resultQuality = nextQuality;
    upgraded = true;
  }

  // ✨ Apply Guaranteed Minimum
  if (guaranteedMinQuality) {
    const minIndex = QUALITY_ORDER.indexOf(guaranteedMinQuality);
    const currentIndex = QUALITY_ORDER.indexOf(resultQuality);

    if (currentIndex < minIndex) {
      resultQuality = guaranteedMinQuality;
      upgraded = true; // Consider guarantee boost as an upgrade
    }
  }

  return {
    upgraded: upgraded,
    newQuality: resultQuality,
  };
}
```

**✅ Completion Check:**

- [ ] rollQualityUpgrade implemented
- [ ] Guaranteed minimum applied correctly
- [ ] Probability calculation is correct

---

### Task 2.3: Create calculateUpgradeCost.ts

```typescript
// src/camps/facilities/Blacksmith/logic/calculateUpgradeCost.ts (New)

import type { EquipmentItem } from "../../../../types/ItemTypes";
import type {
  UpgradeOption,
  UpgradeConfig,
} from "../../../../types/BlacksmithTypes";
import {
  BASE_UPGRADE_COSTS,
  UPGRADE_OPTIONS,
} from "../../../../types/BlacksmithTypes";

/**
 * Calculate Upgrade Cost
 */
export function calculateUpgradeCost(
  equipment: EquipmentItem,
  option: UpgradeOption
): UpgradeConfig {
  const currentLevel = equipment.level;
  const targetLevel = currentLevel + 1;

  if (targetLevel > 3) {
    throw new Error("Cannot upgrade beyond level 3");
  }

  const rarity = equipment.rarity || "common";
  const baseCost = BASE_UPGRADE_COSTS[rarity][targetLevel];

  if (!baseCost) {
    throw new Error(`No cost data for ${rarity} level ${targetLevel}`);
  }

  const optionConfig = UPGRADE_OPTIONS[option];

  return {
    option: option,
    goldCost: Math.floor(baseCost.gold * optionConfig.goldMultiplier),
    magicStoneCost: baseCost.magicStone,
    qualityUpgradeChances: optionConfig.qualityUpgradeChances,
    guaranteedMinQuality: optionConfig.guaranteedMinQuality,
  };
}

/**
 * Check Magic Stone Availability
 */
export function checkMagicStoneAvailability(
  items: Item[],
  requiredValue: number,
  preference: "auto" | "medium_preferred" | "large_only"
): {
  available: boolean;
  deficit: number;
  breakdown: {
    small: number;
    medium: number;
    large: number;
  };
} {
  const magicStones = items.filter((item) => item.itemType === "magicStone");

  let small = 0,
    medium = 0,
    large = 0;
  magicStones.forEach((stone) => {
    const count = stone.stackCount || 1;
    switch (stone.typeId) {
      case "magic_stone_small":
        small += count;
        break;
      case "magic_stone_medium":
        medium += count;
        break;
      case "magic_stone_large":
        large += count;
        break;
    }
  });

  // Value calculation based on preference
  let totalValue = 0;

  switch (preference) {
    case "large_only":
      totalValue = large * 350;
      break;

    case "medium_preferred":
      totalValue = large * 350 + medium * 100 + small * 30;
      break;

    case "auto":
    default:
      totalValue = large * 350 + medium * 100 + small * 30;
      break;
  }

  const available = totalValue >= requiredValue;
  const deficit = available ? 0 : requiredValue - totalValue;

  return {
    available,
    deficit,
    breakdown: { small, medium, large },
  };
}
```

**✅ Completion Check:**

- [ ] calculateUpgradeCost implemented
- [ ] checkMagicStoneAvailability implemented
- [ ] Upgrade option multipliers applied

---

## Phase 3: UI Component Implementation (Week 2: Day 3-5)

### Task 3.1: Create Blacksmith.tsx

```bash
mkdir -p src/camps/facilities/Blacksmith

```

```typescript
// src/camps/facilities/Blacksmith/Blacksmith.tsx (New)

import { useState } from "react";
import { useGameState } from "../../../contexts/GameStateContext";
import { usePlayer } from "../../../contexts/PlayerContext";
import { useInventory } from "../../../contexts/InventoryContext";
import UpgradeTab from "./UpgradeTab";
import DismantleTab from "./DismantleTab";
import "./Blacksmith.css";

type BlacksmithTab = "upgrade" | "dismantle";

const Blacksmith: React.FC = () => {
  const [activeTab, setActiveTab] = useState<BlacksmithTab>("upgrade");
  const { returnToCamp } = useGameState();
  const { player } = usePlayer();
  const { items } = useInventory();

  // Calculate total Magic Stone value
  const magicStoneValue = items
    .filter((item) => item.itemType === "magicStone")
    .reduce((sum, item) => {
      const value = item.magicStoneValue || 0;
      const count = item.stackCount || 1;
      return sum + value * count;
    }, 0);

  return (
    <div className="blacksmith-screen">
      <header className="blacksmith-header">
        <h1>⚒️ 鍛冶屋 - The Blacksmith</h1>
        <div className="resources">
          <div className="gold">💰 {player.gold} G</div>
          <div className="magic-stones">💎 {magicStoneValue} G Equiv.</div>
        </div>
      </header>

      <nav className="blacksmith-tabs">
        <button
          className={activeTab === "upgrade" ? "active" : ""}
          onClick={() => setActiveTab("upgrade")}
        >
          Upgrade / Repair
        </button>
        <button
          className={activeTab === "dismantle" ? "active" : ""}
          onClick={() => setActiveTab("dismantle")}
        >
          Dismantle
        </button>
      </nav>

      <div className="blacksmith-content">
        {activeTab === "upgrade" && <UpgradeTab />}
        {activeTab === "dismantle" && <DismantleTab />}
      </div>

      <button className="back-button" onClick={returnToCamp}>
        Return to Camp
      </button>
    </div>
  );
};

export default Blacksmith;
```

**✅ Completion Check:**

- [ ] Blacksmith.tsx created
- [ ] Tab switching works
- [ ] Resource display is correct

---

### Task 3.2: Create UpgradeTab.tsx

```typescript
// src/camps/facilities/Blacksmith/UpgradeTab.tsx (New)

import { useState } from "react";
import { usePlayer } from "../../../contexts/PlayerContext";
import { useInventory } from "../../../contexts/InventoryContext";
import type { EquipmentItem } from "../../../types/ItemTypes";
import type { UpgradeOption } from "../../../types/BlacksmithTypes";
import {
  calculateUpgradeCost,
  checkMagicStoneAvailability,
} from "./logic/calculateUpgradeCost";
import { upgradeEquipment, applyUpgradeResult } from "./logic/upgradeEquipment";
import { UPGRADE_OPTIONS } from "../../../types/BlacksmithTypes";
import { QUALITY_NAMES, QUALITY_COLORS } from "../../../types/ItemTypes";
import UpgradeOptionModal from "./components/UpgradeOptionModal";
import "./UpgradeTab.css";

const UpgradeTab: React.FC = () => {
  const [selectedEquipment, setSelectedEquipment] =
    useState<EquipmentItem | null>(null);
  const [showUpgradeModal, setShowUpgradeModal] = useState(false);
  const { player, useGold } = usePlayer();
  const { items, updateItem, useMagicStones } = useInventory();

  // Get equipment list (including equipped items)
  const equipmentList = items.filter(
    (item) => item.itemType === "equipment"
  ) as EquipmentItem[];

  const handleSelectEquipment = (equipment: EquipmentItem) => {
    setSelectedEquipment(equipment);
  };

  const handleUpgradeClick = () => {
    if (!selectedEquipment) return;
    if (selectedEquipment.level >= 3) {
      alert("This equipment is at max level");
      return;
    }
    setShowUpgradeModal(true);
  };

  const handleConfirmUpgrade = (option: UpgradeOption) => {
    if (!selectedEquipment) return;

    try {
      const config = calculateUpgradeCost(selectedEquipment, option);
      const optionData = UPGRADE_OPTIONS[option];

      // Check cost
      if (player.gold < config.goldCost) {
        alert("Not enough Gold!");
        return;
      }

      const magicStoneCheck = checkMagicStoneAvailability(
        items,
        config.magicStoneCost,
        optionData.magicStonePreference
      );

      if (!magicStoneCheck.available) {
        alert(
          `Insufficient Magic Stones! Short by ${magicStoneCheck.deficit}G value.`
        );
        return;
      }

      // Pay Gold
      if (!useGold(config.goldCost)) {
        alert("Failed to pay Gold");
        return;
      }

      // Consume Magic Stones
      if (
        !useMagicStones(config.magicStoneCost, optionData.magicStonePreference)
      ) {
        alert("Failed to consume Magic Stones");
        return;
      }

      // ✨ Execute Upgrade
      const result = upgradeEquipment(selectedEquipment, option);
      const upgradedEquipment = applyUpgradeResult(selectedEquipment, result);

      // Update item
      updateItem(selectedEquipment.id, upgradedEquipment);

      // Effects and Messages
      showUpgradeResult(result);

      // Update selection
      setSelectedEquipment(upgradedEquipment);
      setShowUpgradeModal(false);
    } catch (error) {
      console.error("Upgrade error:", error);
      alert("Upgrade failed");
    }
  };

  const showUpgradeResult = (result: UpgradeResult) => {
    let message = `Upgrade Successful! Reached Lv${result.newLevel}!\n`;

    if (result.qualityUpgraded) {
      message += `\n🎉 Quality Improved!\n${result.oldQuality} → ${result.newQuality}`;
      // TODO: Special visual effect
    }

    if (result.skillUnlocked) {
      message += "\n\n✨ Hidden skill unlocked!";
      // TODO: Skill unlock visual effect
    }

    alert(message);
  };

  const handleRepair = () => {
    if (!selectedEquipment) return;

    const repairCost = calculateRepairCost(selectedEquipment);

    if (repairCost === 0) {
      alert("Repair not needed");
      return;
    }

    if (!confirm(`Repair to full for ${repairCost}G?`)) {
      return;
    }

    if (!useGold(repairCost)) {
      alert("Not enough Gold");
      return;
    }

    const repairedEquipment: EquipmentItem = {
      ...selectedEquipment,
      durability: selectedEquipment.maxDurability,
    };

    updateItem(selectedEquipment.id, repairedEquipment);
    setSelectedEquipment(repairedEquipment);
    alert("Repair complete!");
  };

  const calculateRepairCost = (equipment: EquipmentItem): number => {
    const missing = equipment.maxDurability - equipment.durability;
    if (missing <= 0) return 0;

    const rarity = equipment.rarity || "common";
    const multiplier = REPAIR_RARITY_MULTIPLIER[rarity] || 1.0;

    return Math.floor(missing * REPAIR_COST_PER_AP * multiplier);
  };

  return (
    <div className="upgrade-tab">
      <div className="equipment-list">
        <h3>Equipment List</h3>
        {equipmentList.map((eq) => (
          <div
            key={eq.id}
            className={`equipment-item ${
              selectedEquipment?.id === eq.id ? "selected" : ""
            }`}
            onClick={() => handleSelectEquipment(eq)}
          >
            <span className="icon">{eq.icon}</span>
            <div className="info">
              <div
                className="name"
                style={{ color: QUALITY_COLORS[eq.quality] }}
              >
                {QUALITY_NAMES[eq.quality]}
                {eq.name}
              </div>
              <div className="details">
                Lv{eq.level} / {eq.quality} / AP: {eq.durability}/
                {eq.maxDurability}
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="equipment-panel">
        {selectedEquipment ? (
          <>
            <h3>Selected Equipment</h3>
            <div className="equipment-details">
              <div className="icon-large">{selectedEquipment.icon}</div>
              <div
                className="name-large"
                style={{ color: QUALITY_COLORS[selectedEquipment.quality] }}
              >
                {QUALITY_NAMES[selectedEquipment.quality]}
                {selectedEquipment.name}
              </div>
              <div className="quality">
                Quality: {selectedEquipment.quality}
              </div>
              <div className="level">Level: {selectedEquipment.level} / 3</div>
              <div className="durability">
                AP: {selectedEquipment.durability} /{" "}
                {selectedEquipment.maxDurability}
              </div>

              <div className="stats">
                {selectedEquipment.stats.atk && (
                  <div>ATK: {selectedEquipment.stats.atk}</div>
                )}
                {selectedEquipment.stats.def && (
                  <div>DEF: {selectedEquipment.stats.def}</div>
                )}
                {selectedEquipment.stats.magic && (
                  <div>Magic: {selectedEquipment.stats.magic}</div>
                )}
              </div>

              <div className="actions">
                <button
                  onClick={handleUpgradeClick}
                  disabled={selectedEquipment.level >= 3}
                >
                  {selectedEquipment.level >= 3 ? "Max Level" : "Upgrade"}
                </button>

                <button
                  onClick={handleRepair}
                  disabled={
                    selectedEquipment.durability ===
                    selectedEquipment.maxDurability
                  }
                >
                  Repair ({calculateRepairCost(selectedEquipment)}G)
                </button>
              </div>
            </div>
          </>
        ) : (
          <div className="no-selection">Please select equipment</div>
        )}
      </div>

      {showUpgradeModal && selectedEquipment && (
        <UpgradeOptionModal
          equipment={selectedEquipment}
          onConfirm={handleConfirmUpgrade}
          onCancel={() => setShowUpgradeModal(false)}
        />
      )}
    </div>
  );
};

export default UpgradeTab;
```

**✅ Completion Check:**

- [ ] UpgradeTab.tsx created
- [ ] Equipment list display
- [ ] Upgrade execution
- [ ] Repair execution

---

### Task 3.3: Create UpgradeOptionModal.tsx

```typescript
// src/camps/facilities/Blacksmith/components/UpgradeOptionModal.tsx (New)

import type { EquipmentItem } from "../../../../types/ItemTypes";
import type { UpgradeOption } from "../../../../types/BlacksmithTypes";
import { useState } from "react";
import { useInventory } from "../../../../contexts/InventoryContext";
import {
  calculateUpgradeCost,
  checkMagicStoneAvailability,
} from "../logic/calculateUpgradeCost";
import { UPGRADE_OPTIONS } from "../../../../types/BlacksmithTypes";
import "./UpgradeOptionModal.css";

interface UpgradeOptionModalProps {
  equipment: EquipmentItem;
  onConfirm: (option: UpgradeOption) => void;
  onCancel: () => void;
}

const UpgradeOptionModal: React.FC<UpgradeOptionModalProps> = ({
  equipment,
  onConfirm,
  onCancel,
}) => {
  const [selectedOption, setSelectedOption] = useState<UpgradeOption>("normal");
  const { items } = useInventory();

  const config = calculateUpgradeCost(equipment, selectedOption);
  const optionData = UPGRADE_OPTIONS[selectedOption];

  const magicStoneCheck = checkMagicStoneAvailability(
    items,
    config.magicStoneCost,
    optionData.magicStonePreference
  );

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <h2>Select Upgrade Option</h2>

        <div className="options">
          {(
            ["normal", "quality_focused", "max_quality"] as UpgradeOption[]
          ).map((option) => {
            const opt = UPGRADE_OPTIONS[option];
            const cfg = calculateUpgradeCost(equipment, option);
            const check = checkMagicStoneAvailability(
              items,
              cfg.magicStoneCost,
              opt.magicStonePreference
            );

            return (
              <div
                key={option}
                className={`option-card ${
                  selectedOption === option ? "selected" : ""
                } ${!check.available ? "unavailable" : ""}`}
                onClick={() => check.available && setSelectedOption(option)}
              >
                <div className="option-name">{opt.name}</div>
                <div className="option-description">{opt.description}</div>
                <div className="option-cost">
                  Cost: {cfg.goldCost}G + Stones: {cfg.magicStoneCost}
                </div>
                <div className="option-chances">
                  Quality Upgrade Chance:
                  <ul>
                    <li>
                      poor→normal:{" "}
                      {(opt.qualityUpgradeChances.poor_to_normal * 100).toFixed(
                        0
                      )}
                      %
                    </li>
                    <li>
                      normal→good:{" "}
                      {(opt.qualityUpgradeChances.normal_to_good * 100).toFixed(
                        0
                      )}
                      %
                    </li>
                    <li>
                      good→master:{" "}
                      {(opt.qualityUpgradeChances.good_to_master * 100).toFixed(
                        0
                      )}
                      %
                    </li>
                  </ul>
                </div>
                {opt.guaranteedMinQuality && (
                  <div className="option-guarantee">
                    Minimum Guarantee: {opt.guaranteedMinQuality}
                  </div>
                )}
                {!check.available && (
                  <div className="option-unavailable">
                    Missing Stones: {check.deficit}G
                  </div>
                )}
              </div>
            );
          })}
        </div>

        <div className="magic-stone-info">
          <h3>Owned Magic Stones:</h3>
          <div>
            Small: {magicStoneCheck.breakdown.small} (
            {magicStoneCheck.breakdown.small * 30}G)
          </div>
          <div>
            Medium: {magicStoneCheck.breakdown.medium} (
            {magicStoneCheck.breakdown.medium * 100}G)
          </div>
          <div>
            Large: {magicStoneCheck.breakdown.large} (
            {magicStoneCheck.breakdown.large * 350}G)
          </div>
        </div>

        <div className="modal-actions">
          <button
            onClick={() => onConfirm(selectedOption)}
            disabled={!magicStoneCheck.available}
          >
            Upgrade
          </button>
          <button onClick={onCancel}>Cancel</button>
        </div>
      </div>
    </div>
  );
};

export default UpgradeOptionModal;
```

**✅ Completion Check:**

- [ ] UpgradeOptionModal.tsx created
- [ ] 3 options displayed
- [ ] Magic Stone availability check functional

---

## Phase 4: Dismantle System Implementation (Week 2-3)

### Task 4.1: Create DismantleTab.tsx

```typescript
// src/camps/facilities/Blacksmith/DismantleTab.tsx (New)

import { useState } from "react";
import { useInventory } from "../../../contexts/InventoryContext";
import type { EquipmentItem } from "../../../types/ItemTypes";
import { dismantleEquipment } from "./logic/dismantleEquipment";
import { QUALITY_NAMES, QUALITY_COLORS } from "../../../types/ItemTypes";
import "./DismantleTab.css";

const DismantleTab: React.FC = () => {
  const [selectedEquipment, setSelectedEquipment] =
    useState<EquipmentItem | null>(null);
  const { items, removeItem, addItem } = useInventory();

  const equipmentList = items.filter(
    (item) => item.itemType === "equipment"
  ) as EquipmentItem[];

  const handleDismantle = () => {
    if (!selectedEquipment) return;

    // Warning Check
    const shouldWarn =
      selectedEquipment.rarity === "rare" ||
      selectedEquipment.rarity === "epic" ||
      selectedEquipment.rarity === "legendary" ||
      selectedEquipment.level >= 1 ||
      selectedEquipment.quality === "good" ||
      selectedEquipment.quality === "master";

    if (shouldWarn) {
      const confirmMessage = `
⚠️ WARNING

You are about to dismantle:
${QUALITY_NAMES[selectedEquipment.quality]}${selectedEquipment.name}
(Lv${selectedEquipment.level}, ${selectedEquipment.quality})

This action cannot be undone.
Are you sure?
      `.trim();

      if (!confirm(confirmMessage)) {
        return;
      }
    }

    // Execute Dismantle
    const result = dismantleEquipment(selectedEquipment);

    // Remove Item
    removeItem(selectedEquipment.id);

    // Add Magic Stones
    result.magicStones.forEach((stone) => {
      // TODO: createMagicStoneItem implementation
      // addItem(createMagicStoneItem(stone.typeId, stone.count));
    });

    // Message
    let message = "Dismantle Complete!\n\nAcquired Magic Stones:\n";
    result.magicStones.forEach((stone) => {
      message += `${stone.typeId} x ${stone.count}\n`;
    });

    if (result.bonusReceived) {
      message += "\n🎉 Bonus! Acquired additional Large Magic Stone!";
    }

    alert(message);
    setSelectedEquipment(null);
  };

  // Dismantle Prediction
  const predictDismantle = (equipment: EquipmentItem) => {
    return dismantleEquipment(equipment); // Calculation only
  };

  return (
    <div className="dismantle-tab">
      <div className="equipment-list">
        <h3>Equipment List</h3>
        {equipmentList.map((eq) => (
          <div
            key={eq.id}
            className={`equipment-item ${
              selectedEquipment?.id === eq.id ? "selected" : ""
            }`}
            onClick={() => setSelectedEquipment(eq)}
          >
            <span className="icon">{eq.icon}</span>
            <div className="info">
              <div
                className="name"
                style={{ color: QUALITY_COLORS[eq.quality] }}
              >
                {QUALITY_NAMES[eq.quality]}
                {eq.name}
              </div>
              <div className="details">
                Lv{eq.level} / Sell: {eq.sellPrice}G
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="dismantle-panel">
        {selectedEquipment ? (
          <>
            <h3>Dismantle Prediction</h3>
            <div className="equipment-info">
              <div className="icon-large">{selectedEquipment.icon}</div>
              <div className="name-large">
                {QUALITY_NAMES[selectedEquipment.quality]}
                {selectedEquipment.name}
              </div>
              <div>
                Lv{selectedEquipment.level} / {selectedEquipment.quality}
              </div>
            </div>

            <div className="dismantle-result">
              <h4>Acquire on Dismantle:</h4>
              {predictDismantle(selectedEquipment).magicStones.map(
                (stone, i) => (
                  <div key={i}>
                    {stone.typeId} x {stone.count}
                  </div>
                )
              )}
              <div className="note">
                ※{" "}
                {(
                  (DISMANTLE_RETURN_RATES[
                    selectedEquipment.rarity || "common"
                  ] || 0.1) * 100
                ).toFixed(0)}
                % of sell price
              </div>
            </div>

            <button className="dismantle-button" onClick={handleDismantle}>
              Dismantle
            </button>
          </>
        ) : (
          <div className="no-selection">Please select equipment</div>
        )}
      </div>
    </div>
  );
};

export default DismantleTab;
```

**✅ Completion Check:**

- [ ] DismantleTab.tsx created
- [ ] Dismantle prediction displayed
- [ ] Dismantle execution works

---

### Task 4.2: Create dismantleEquipment.ts

```typescript
// src/camps/facilities/Blacksmith/logic/dismantleEquipment.ts (New)

import type { EquipmentItem } from "../../../../types/ItemTypes";
import type { DismantleResult } from "../../../../types/BlacksmithTypes";
import {
  DISMANTLE_RETURN_RATES,
  DISMANTLE_BONUS_CHANCE,
  DISMANTLE_LEVEL_BONUS,
} from "../../../../types/BlacksmithTypes";

/**
 * Dismantle Equipment
 */
export function dismantleEquipment(equipment: EquipmentItem): DismantleResult {
  const rarity = equipment.rarity || "common";
  const returnRate = DISMANTLE_RETURN_RATES[rarity] || 0.1;
  const baseReturn = Math.floor(equipment.sellPrice * returnRate);

  // Convert to Magic Stones
  const magicStones = convertToMagicStones(baseReturn);

  // Bonus Check (Epic or higher)
  let bonusReceived = false;
  if (rarity === "epic" || rarity === "legendary") {
    const levelBonus = DISMANTLE_LEVEL_BONUS[equipment.level] || 0;
    const bonusChance = DISMANTLE_BONUS_CHANCE + levelBonus;

    if (Math.random() < bonusChance) {
      magicStones.push({ typeId: "magic_stone_large", count: 1 });
      bonusReceived = true;
    }
  }

  return {
    magicStones,
    bonusReceived,
  };
}

/**
 * Convert Value to Magic Stones
 */
function convertToMagicStones(
  value: number
): { typeId: string; count: number }[] {
  const stones: { typeId: string; count: number }[] = [];
  let remaining = value;

  // Large Magic Stone: 350G
  const largeCount = Math.floor(remaining / 350);
  if (largeCount > 0) {
    stones.push({ typeId: "magic_stone_large", count: largeCount });
    remaining -= largeCount * 350;
  }

  // Medium Magic Stone: 100G
  const mediumCount = Math.floor(remaining / 100);
  if (mediumCount > 0) {
    stones.push({ typeId: "magic_stone_medium", count: mediumCount });
    remaining -= mediumCount * 100;
  }

  // Small Magic Stone: 30G
  const smallCount = Math.floor(remaining / 30);
  if (smallCount > 0) {
    stones.push({ typeId: "magic_stone_small", count: smallCount });
    remaining -= smallCount * 30;
  }

  // Remainders are truncated

  return stones;
}
```

**✅ Completion Check:**

- [ ] dismantleEquipment implemented
- [ ] convertToMagicStones implemented
- [ ] Bonus check works

---

## Phase 5: CSS and Animations (Week 3)

### Task 5.1: Blacksmith.css

```css
/* src/camps/facilities/Blacksmith/Blacksmith.css */

.blacksmith-screen {
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #2a1a1a 0%, #3a2a2a 100%);
  display: flex;
  flex-direction: column;
  padding: 2rem;
  color: #f0e0d0;
}

.blacksmith-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.blacksmith-header h1 {
  font-size: 2.5rem;
  text-shadow: 0 0 20px rgba(255, 100, 50, 0.8);
}

.resources {
  display: flex;
  gap: 2rem;
  font-size: 1.5rem;
}

.blacksmith-tabs {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-bottom: 2rem;
}

.blacksmith-tabs button {
  padding: 1rem 2rem;
  background: rgba(255, 100, 50, 0.2);
  border: 2px solid rgba(255, 100, 50, 0.5);
  border-radius: 8px;
  color: #f0e0d0;
  font-size: 1.2rem;
  cursor: pointer;
  transition: all 0.3s ease;
}

.blacksmith-tabs button.active {
  background: rgba(255, 100, 50, 0.8);
  border-color: rgba(255, 100, 50, 1);
}

.blacksmith-content {
  flex: 1;
  background: rgba(0, 0, 0, 0.3);
  border: 2px solid rgba(255, 100, 50, 0.3);
  border-radius: 12px;
  padding: 2rem;
  overflow-y: auto;
}

.back-button {
  margin-top: 1rem;
  padding: 1rem 2rem;
  background: rgba(100, 100, 100, 0.3);
  border: 2px solid rgba(150, 150, 150, 0.5);
  border-radius: 8px;
  color: #f0e0d0;
  font-size: 1.1rem;
  cursor: pointer;
}

/* Quality Upgrade Animation */
@keyframes quality-upgrade {
  0% {
    transform: scale(1);
    filter: brightness(1);
  }
  50% {
    transform: scale(1.2);
    filter: brightness(2) hue-rotate(60deg);
  }
  100% {
    transform: scale(1);
    filter: brightness(1);
  }
}

.quality-upgraded {
  animation: quality-upgrade 1s ease-in-out;
}
```

**✅ Completion Check:**

- [ ] Blacksmith.css created
- [ ] Styles applied
- [ ] Animations defined

---

## Test Items

### Basic Operation Test

```
□ Blacksmith Screen Display
  □ Resource Display (Gold, Magic Stone Value)
  □ Tab Switching

□ Upgrade Function
  □ Equipment List Display
  □ Upgrade Option Selection
  □ Level Up
  □ Quality Upgrade Check
  □ Skill Unlock (Lv3)
  □ Cost Payment

□ Repair Function
  □ Cost Calculation
  □ AP Recovery

□ Dismantle Function
  □ Magic Stone Conversion
  □ Bonus Check
  □ Warning Dialog

```

---

## Troubleshooting

### Common Errors

**1. Quality not found**

```
Cause: Existing equipment does not have 'quality'.
Solution: Run migrateEquipmentToV2.

```

**2. Stats calculation incorrect**

```
Cause: baseStats is undefined.
Solution: Set baseStats in equipmentGenerator.

```

**3. Upgrade cost not found**

```
Cause: No data in BASE_UPGRADE_COSTS.
Solution: Check BlacksmithTypes.ts.
```
