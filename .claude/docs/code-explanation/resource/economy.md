# Resource & Economy System

## 1. Overview

The economy revolves around **three currencies** (gold, magic stones, souls) flowing through a dual-pool architecture where exploration resources are at-risk and must be transferred to the permanent baseCamp pool on survival. Spending sinks include the **Shop** (consumables, equipment packs, daily rotation), **Blacksmith** (level/quality upgrades, repair, dismantle), and **Sanctuary** (souls-based skill tree). Magic stones serve as both a secondary currency for blacksmith operations and a gold-convertible asset.

## 2. File Map

| File | Lines | Role |
|------|-------|------|
| `src/contexts/ResourceContext.tsx` | 363 | Gold dual-pool, magic stones, exploration limit state management |
| `src/constants/itemConstants.ts` | 45 | Magic stone values, rarity sell/buy prices |
| `src/constants/campConstants.ts` | 126 | Blacksmith modifiers, sanctuary constants, capacity limits |
| `src/constants/data/camps/ShopData.ts` | 244 | Shop listings, equipment packs, daily rotation |
| `src/constants/data/camps/BlacksmithData.ts` | 191 | Upgrade costs, quality rates, repair/dismantle config |
| `src/constants/data/camps/SanctuaryData.ts` | 608 | 25 skill nodes, tier/category layout |
| `src/domain/camps/logic/shopLogic.ts` | 142 | Purchase, pack opening, stone exchange |
| `src/domain/camps/logic/blacksmithLogic.ts` | 466 | Level/quality upgrade, repair, dismantle |
| `src/domain/camps/logic/sanctuaryLogic.ts` | 335 | Node unlock, effect aggregation, stat boosting |
| `src/domain/camps/logic/blacksmithUtils.ts` | 65 | Equipment validation helpers |
| `src/domain/item_equipment/logic/itemUtils.ts` | 19 | Magic stone value calculation |
| `src/types/campTypes.ts` | 510 | Shop, blacksmith, sanctuary, storage types |
| `src/types/itemTypes.ts` | 176 | Item, MagicStones, equipment types |
| `src/types/saveTypes.ts` | 74 | ResourceSaveData (baseCamp only) |

## 3. Data Structures

### 3.1 Gold Dual-Pool

```typescript
gold: {
  baseCamp: number;      // Permanent. Survives death. Saved to localStorage.
  exploration: number;   // At-risk. Lost on death. Transferred on survival.
}
```

**Spending priority:** `useGold()` deducts from baseCamp first, then exploration (ResourceContext.tsx:122-141).

### 3.2 Magic Stones

```typescript
interface MagicStones {
  small: number;    // 30G each
  medium: number;   // 100G each
  large: number;    // 350G each
  huge: number;     // 1000G each
}
```

Same dual-pool structure as gold: `baseCamp` + `exploration`.

### 3.3 Exploration Limit

```typescript
interface ExplorationLimit {
  current: number;   // Incremented per dungeon entry
  max: number;       // Default 10, increased by Sanctuary
}
```

### 3.4 Sanctuary Progress

```typescript
interface SanctuaryProgress {
  currentRunSouls: number;       // Earned during exploration (at-risk)
  totalSouls: number;            // Permanent pool for purchasing nodes
  unlockedNodes: string[];       // Node IDs
  explorationLimitBonus: number; // Aggregated bonus
}
```

### 3.5 Shop Types

```typescript
interface ShopListing {
  itemTypeId: string;       // References ConsumableItemData
  category: ShopCategory;   // "consumable" | "teleport" | "equipmentPack" | ...
  stock?: number;
}

interface EquipmentPackConfig {
  id: string;
  price: number;                      // 300 / 500 / 1000
  packType: EquipmentPackType;        // "common" | "rare" | "epic"
  guaranteedRarity: ItemRarity;
  itemCount: number;                  // Always 6 (one per slot)
  rarityProbabilities: RarityProbability;
}
```

### 3.6 Blacksmith Types

```typescript
interface UpgradeCost {
  gold: number;
  magicStones: number;    // Gold-equivalent value required
}

interface QualityUpConfig {
  option: QualityUpOption;
  costMultiplier: number;
  successRates: {
    poorToNormal: number;
    normalToGood: number;
    goodToMaster: number;
  };
}
```

## 4. Logic Flow

### 4.1 Gold Flow Diagram

```
  EARNING SOURCES                 POOLS                    SPENDING SINKS
  ======================================================================

  Battle rewards ────┐
  Treasure nodes ────┤        exploration gold            Shop purchases ──────┐
  Selling items ─────┤──────► (at-risk pool)              Equipment packs ─────┤
  Dismantle returns ─┘            |                       Blacksmith upgrades ─┤
                                  | survive               Repair costs ────────┤
                                  | (multiplier 0.6-1.0)  Quality upgrades ────┤
                                  v                       Rumor activation ────┘
                            baseCamp gold
                            (permanent pool)
                                  |
                          death = exploration lost
                          baseCamp preserved
```

### 4.2 Magic Stone Exchange Flow

```
Player has MagicStones { small: N, medium: M, large: L, huge: H }
  |
  v
calculateStonesToConsume(stones, targetGoldValue)
  |- Consumes smallest denominations first
  |- small (30G) → medium (100G) → large (350G) → huge (1000G)
  |- May overshoot targetValue (no change given)
  |
  v
Returns { newStones, actualValue }
  |- actualValue >= targetValue (possible overpay)
```

### 4.3 Shop Purchase Flow

```
Player selects consumable/teleport item
  |
  v
resolveShopListing(listing)
  |- Looks up ConsumableItemData by typeId
  |- Returns { listing, data, price: data.shopPrice }
  |
  v
canAfford(playerGold, price) && hasInventorySpace()
  |
  v
purchaseItem(listing)
  |- generateConsumableFromData(typeId) → Item
  |- Deduct gold via ResourceContext.useGold()
  |- Add item to inventory
```

### 4.4 Equipment Pack (Gacha) Flow

```
Player buys pack (300G / 500G / 1000G)
  |
  v
openEquipmentPack(packId)
  |- For each of 6 EQUIPMENT_SLOTS:
  |   |- rollRarity(pack) → cumulative probability distribution
  |   |- generateEquipmentItem(slot, rarity) → Item
  |
  v
Returns Item[6] → added to storage/equipmentInventory
```

**Pack Rarity Distributions:**

| Pack | Price | Common | Uncommon | Rare | Epic | Legendary |
|------|-------|--------|----------|------|------|-----------|
| Common | 300G | 100% | 0% | 0% | 0% | 0% |
| Rare | 500G | 60% | 0% | 35% | 5% | 0% |
| Epic | 1000G | 30% | 0% | 45% | 20% | 5% |

### 4.5 Blacksmith Level Upgrade Flow

```
Player selects equipment item
  |
  v
canLevelUpgrade(item)
  |- itemType === "equipment"
  |- level < MAX_EQUIPMENT_LEVEL (3)
  |
  v
getLevelUpgradeCost(item)
  |- Lookup UPGRADE_COSTS[rarity][currentLevel]
  |- Returns { gold, magicStones }
  |
  v
canAfford(playerGold, magicStoneValue, cost)
  |
  v
performLevelUpgrade(item)
  |- Stats: baseValue / currentMod * nextMod
  |- AP: baseDurability / currentApMod * nextApMod
  |- Durability ratio preserved
  |- level → level + 1
```

### 4.6 Quality Upgrade Flow

```
Player selects equipment + option (normal/qualityFocused/maxQuality)
  |
  v
canQualityUpgrade(item) → quality !== "master"
  |
  v
getQualityUpgradeCost(option)
  |- Base: { gold: 500, magicStones: 300 }
  |- * costMultiplier (1.0 / 1.5 / 2.0)
  |
  v
attemptQualityUpgrade(item, option)
  |- Roll Math.random() < successRate
  |- Success: quality++ , stats *= nextQualityMod / currentQualityMod
  |- Failure: item unchanged, gold/stones still consumed
```

### 4.7 Sanctuary Node Unlock Flow

```
Player long-presses a node (1.5s hold)
  |
  v
canUnlockNode(node, progress, playerClass)
  |- Not already unlocked
  |- Class restriction check
  |- All prerequisites unlocked
  |- totalSouls >= node.cost
  |
  v
unlockNode(node, progress)
  |- totalSouls -= node.cost
  |- unlockedNodes.push(node.id)
  |
  v
calculateTotalEffects(progress)
  |- Iterates all unlocked nodes
  |- Aggregates: initialHpBonus, goldMultiplier, soulMultiplier,
  |              explorationLimitBonus, inventoryBonus, hpRecoveryPercent,
  |              hasAppraisal, hasTrueAppraisal, hasIndomitableWill,
  |              classEnergy, enhancedElements
```

### 4.8 Exploration Resource Transfer

```
Dungeon exit:
  |
  ├── Survived (earlyReturn/normalReturn/fullClear)
  |     |
  |     v
  |   transferExplorationToBaseCamp(multiplier)
  |     |- gold: floor(exploration * mult) → baseCamp, exploration = 0
  |     |- stones: each tier floor(count * mult) → baseCamp, exploration = 0
  |   transferSouls(multiplier)
  |     |- floor(currentRunSouls * mult) → totalSouls, currentRunSouls = 0
  |
  └── Died
        |
        v
      resetExplorationResources()
        |- exploration gold = 0, exploration stones = 0
      resetCurrentRunSouls()
        |- currentRunSouls = 0
```

## 5. Key Details

### Gold Constants

| Item | Price | Source |
|------|-------|--------|
| Healing Potion | shopPrice from ConsumableItemData | `ShopData.ts` |
| Greater Healing Potion | shopPrice from ConsumableItemData | `ShopData.ts` |
| Full Elixir | shopPrice from ConsumableItemData | `ShopData.ts` |
| Teleport Stone | shopPrice from ConsumableItemData | `ShopData.ts` |
| Common Equipment Pack | 300G | `ShopData.ts:68` |
| Rare Equipment Pack | 500G | `ShopData.ts:79` |
| Epic Equipment Pack | 1000G | `ShopData.ts:90` |

### Equipment Buy Prices (Direct Purchase)

| Rarity | Buy Price | Sell Price |
|--------|-----------|------------|
| Common | 120G | 50G |
| Uncommon | 250G | 100G |
| Rare | 400G | 150G |
| Epic | 900G | 400G |
| Legendary | 2500G | 1000G |

### Magic Stone Values

| Size | Gold Equivalent |
|------|-----------------|
| Small | 30G |
| Medium | 100G |
| Large | 350G |
| Huge | 1000G |

### Blacksmith Upgrade Costs (Gold + MagicStone Gold-Equivalent)

| Rarity | Lv0→1 | Lv1→2 | Lv2→3 |
|--------|-------|-------|-------|
| Common | 200G + 150MS | 400G + 300MS | 800G + 600MS |
| Uncommon | 200G + 150MS | 400G + 300MS | 800G + 600MS |
| Rare | 400G + 300MS | 800G + 600MS | 1600G + 1200MS |
| Epic | 800G + 600MS | 1600G + 1200MS | 3200G + 2400MS |
| Legendary | 1600G + 1200MS | 3200G + 2400MS | 6400G + 4800MS |

### Level Stat Modifiers

| Level | Stat Multiplier | AP Multiplier |
|-------|----------------|---------------|
| 0 | 1.0x | 1.0x |
| 1 | 1.1x (+10%) | 1.2x (+20%) |
| 2 | 1.2x (+20%) | 1.4x (+40%) |
| 3 | 1.3x (+30%) | 1.6x (+60%) |

### Quality Modifiers

| Quality | Stat Multiplier |
|---------|----------------|
| Poor | 0.95x (-5%) |
| Normal | 1.0x |
| Good | 1.03x (+3%) |
| Master | 1.05x (+5%) |

### Quality Upgrade Success Rates

| Transition | Normal (1.0x cost) | Quality Focused (1.5x) | Max Quality (2.0x) |
|------------|-------------------|----------------------|-------------------|
| Poor → Normal | 40% | 80% | 100% |
| Normal → Good | 20% | 40% | 60% |
| Good → Master | 10% | 15% | 25% |

### Repair Costs

- Formula: `ceil(durabilityToRestore * 0.5 * rarityMultiplier)`
- Rarity multipliers: Common 1.0, Uncommon 1.2, Rare 1.5, Epic 2.0, Legendary 3.0

### Dismantle Returns

| Rarity | Gold Return % | Bonus Stone Chance |
|--------|--------------|-------------------|
| Common | 10% of sellPrice | 0% |
| Uncommon | 12% | 0% |
| Rare | 15% | 10% |
| Epic | 20% | 30% |
| Legendary | 25% | 50% |

Level bonus to stone chance: Lv1 +5%, Lv2 +10%, Lv3 +15%. Bonus stone value: 100G equivalent (medium stone).

### Sanctuary Skill Tree (25 Nodes)

| Tier | Cost Range | Node Count | Key Effects |
|------|-----------|------------|-------------|
| 1 (Foundation) | 20-30 souls | 6 | +10 HP, +10% gold, class insight, basic appraisal |
| 2 (Advancement) | 40-80 souls | 12 | +20 HP, +20% gold, +1 exploration, element mastery, full appraisal, +5 inventory |
| 3 (Mastery) | 100-150 souls | 7 | +30 HP, +30% gold, +20% souls, death defiance, true appraisal, +2 exploration |

### Soul Values

| Source | Souls |
|--------|-------|
| Normal enemy | 5 |
| Elite enemy | 15 |
| Boss | 50 |
| Return route multiplier | 1.2x |

### Survival Multipliers (for resource transfer)

| Exit Type | Multiplier |
|-----------|-----------|
| Early return | 0.6x |
| Normal return | 0.8x |
| Full clear | 1.0x |

### Daily Equipment Rotation

- 4 items per day, seeded by `dayCount * 7919 + 31`
- No duplicate slots within same day
- Rarity weighted: 40% common, 30% uncommon, 20% rare, 10% epic

## 6. Dependencies

### Import Graph

```
ResourceContext
  ├── calculateMagicStoneValue ← itemUtils.ts ← MAGIC_STONE_VALUES
  └── ExplorationLimit type ← campTypes.ts

shopLogic
  ├── generateConsumableFromData ← generateItem.ts
  ├── generateEquipmentItem ← generateItem.ts
  ├── EQUIPMENT_SLOTS, MAGIC_STONE_VALUES ← itemConstants.ts
  ├── getEquipmentPackById, resolveShopListing ← ShopData.ts
  └── EquipmentPackConfig, ShopListing types ← campTypes.ts

blacksmithLogic
  ├── LEVEL_STAT_MODIFIERS, LEVEL_AP_MODIFIERS, QUALITY_MODIFIERS ← campConstants.ts
  ├── blacksmithUtils (canUpgrade*, needsRepair, canDismantle)
  ├── QUALITY_UP_OPTIONS, DISMANTLE_CONFIG, cost helpers ← BlacksmithData.ts
  └── UpgradeCost, RepairCost, DismantleResult types ← campTypes.ts

sanctuaryLogic
  ├── DEFAULT_SANCTUARY_EFFECTS ← campConstants.ts
  ├── SKILL_TREE_NODES, getNodeById ← SanctuaryData.ts
  └── SkillNode, SanctuaryEffects types ← campTypes.ts

PlayerContext (resource delegation)
  └── ResourceContext (useResources hook)
```

### Data Flow Between Systems

```
Dungeon battles → addGold(amount) → exploration pool
  |
  v
Shop ← useGold(amount) → baseCamp first, then exploration
  |
  v
Blacksmith ← useGold() + magic stones (value check)
  |
  v
Sanctuary ← totalSouls (permanent pool only)
```

## 7. Vulnerability Analysis

### [BUG-RISK] V-EC01: Stone Exchange Overpayment

**Location:** `shopLogic.ts:83-124`
`calculateStonesToConsume()` consumes stones smallest-first to reach a target gold value, but cannot give change. If a player has only 1 huge stone (1000G) and needs 30G, the entire 1000G stone is consumed. No warning or confirmation is shown to the user about the overpayment.

### [BUG-RISK] V-EC02: Equipment Pack Skips Uncommon Rarity

**Location:** `ShopData.ts:33-55`
All three pack probability distributions set `uncommon: 0`. The Rare pack jumps from 60% common to 35% rare, and the Epic pack from 30% common to 45% rare. This means uncommon equipment can only be obtained via daily shop rotation, never from packs. Likely an oversight rather than intentional design.

### [BUG-RISK] V-EC03: Quality Upgrade Consumes Resources on Failure

**Location:** `blacksmithLogic.ts:224-285`
`attemptQualityUpgrade()` returns a `BlacksmithResult` indicating success or failure, but the cost deduction happens in the UI layer before calling this function. On failure, gold and magic stones are permanently lost. While this may be by design (standard gacha pattern), the 10% good-to-master rate at 2x cost (1000G + 600MS) creates a very punishing RNG sink.

### [EXTENSIBILITY] V-EC04: Sanctuary Effects Recalculated on Every Call

**Location:** `sanctuaryLogic.ts:125-201`
`calculateTotalEffects()` iterates all `unlockedNodes` and rebuilds the full `SanctuaryEffects` object from scratch. Functions like `applyGoldMultiplier()` and `applySoulMultiplier()` each call `calculateTotalEffects()` independently. With 25 nodes this is negligible, but the pattern doesn't cache or memoize results.

### [BUG-RISK] V-EC05: Dismantle Gold Return Based on sellPrice Not buyPrice

**Location:** `BlacksmithData.ts:106-135`
Dismantle returns a percentage of `sellPrice` (10-25% by rarity). For a common item with sellPrice=50G, dismantle returns only 5G. This makes dismantling almost never worthwhile for low-rarity items compared to selling, which undermines the feature's purpose.

### [BUG-RISK] V-EC06: ResourceSaveData Excludes Exploration Resources

**Location:** `saveTypes.ts:29-32`
`ResourceSaveData` only saves `baseCampGold` and `baseCampMagicStones`. If the game is saved during an exploration (mid-dungeon), all exploration gold and stones are silently lost on reload. The save system doesn't warn the player about this.

### [QUALITY] V-EC07: Common and Uncommon Share Identical Upgrade Costs

**Location:** `BlacksmithData.ts:12-22`
Common and Uncommon rarities have identical upgrade costs at all levels (200/400/800G). This removes economic differentiation between the two tiers, making uncommon items strictly better than common with no additional cost.

### [BUG-RISK] V-EC08: Daily Rotation Seed Not Persisted

**Location:** `ShopData.ts:185-223`
`generateDailyEquipmentInventory(dayCount)` uses a `dayCount` parameter, but there's no visible mechanism for tracking which "day" it is. If `dayCount` is derived from `Date.now()`, the shop inventory changes on page reload after midnight. If it's not persisted in save data, it's unreliable.

### [EXTENSIBILITY] V-EC09: No Economy Sink for Excess Gold

**Location:** Systemic
Once a player has purchased all consumables, upgraded all equipment to Lv3 master quality, and unlocked all sanctuary nodes, there are no remaining gold sinks. Equipment packs become the only repeatable spend, but the storage limit (100 items) caps inventory. No gold-for-power conversion exists at endgame.

### [BUG-RISK] V-EC10: Sanctuary Node Cost Not Validated Against explorationLimitBonus

**Location:** `sanctuaryLogic.ts:94-120`, `campTypes.ts:64-68`
`unlockNode()` deducts from `totalSouls` and updates `unlockedNodes`, but the `explorationLimitBonus` field in `SanctuaryProgress` is never updated by the unlock function. It's presumably calculated dynamically, but the field exists in both the type and save data, creating a potential inconsistency.

### [QUALITY] V-EC11: Hardcoded Initial Resource Test Values

**Location:** `ResourceContext.tsx:67-81`
Initial baseCamp gold is hardcoded to 1250, magic stones to `{ small:5, medium:3, large:1, huge:0 }` (total value: 5*30 + 3*100 + 1*350 = 800G). These test values ship in production code and bypass the save system's `createDefaultSaveData()` which correctly initializes everything to zero.

### [BUG-RISK] V-EC12: Transfer Multiplier Applied Per-Stone-Tier

**Location:** `ResourceContext.tsx:267-309`
`transferExplorationToBaseCamp()` applies `Math.floor()` independently to each stone tier. With a 0.6 multiplier and 1 stone of each type, the player keeps `floor(0.6)=0` for each tier individually. This means small quantities of high-value stones are completely lost on early return, which may be more punishing than intended.
