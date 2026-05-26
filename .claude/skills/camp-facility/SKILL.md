---
name: camp-facility
description: Add or modify camp facilities in the card battle game. Covers shop, guild, blacksmith, sanctuary, and library functionality. Use for "add shop item", "create new facility", "modify guild feature" requests. 使用時: 「camp-facilityを使用します」
---

# Camp Facility Skill

Workflow for adding or modifying camp facilities.

## Camp Architecture

```
CampScreen
  ├── ShopFacility
  ├── GuildFacility
  ├── BlacksmithFacility
  ├── SanctuaryFacility
  └── LibraryFacility
```

## Key Files

| Facility | Types | Data | Logic |
|----------|-------|------|-------|
| Shop | `ShopTypes.ts` | `ShopData.ts` | `shopLogic.ts` |
| Guild | `GuildTypes.ts` | `GuildEnemyData.ts` | - |
| Blacksmith | `BlacksmithTypes.ts` | `BlacksmithData.ts` | `blacksmithLogic.ts` |
| Sanctuary | `SanctuaryTypes.ts` | `SanctuaryData.ts` | `sanctuaryLogic.ts` |
| Library | `LibraryTypes.ts` | `*EncyclopediaData.ts` | - |

All files in: `src/domain/camps/`

## Adding Shop Items

### Step 1: Define Item Type

```typescript
// src/domain/camps/types/ShopTypes.ts
export interface ShopItem {
  id: string;
  name: string;
  description: string;
  price: number;
  currency: "gold" | "magicStone";
  category: "card" | "item" | "equipment";
  stock: number;  // -1 for unlimited
  requiredDepth?: number;

  // Type-specific references
  cardId?: string;
  itemId?: string;
  equipmentId?: string;
}
```

### Step 2: Add to Shop Data

```typescript
// src/domain/camps/data/ShopData.ts
export const SHOP_ITEMS: ShopItem[] = [
  // ... existing items
  {
    id: "shop_new_item",
    name: "新アイテム",
    description: "効果の説明",
    price: 100,
    currency: "gold",
    category: "item",
    stock: 3,
    itemId: "item_id",
  },
];
```

## Guild System

```typescript
// src/domain/camps/types/GuildTypes.ts
export interface GuildMission {
  id: string;
  name: string;
  description: string;
  targetEnemyId: string;
  requiredKills: number;
  reward: {
    gold?: number;
    magicStone?: number;
    itemId?: string;
  };
  depth: number;
}
```

## Blacksmith System

```typescript
// src/domain/camps/types/BlacksmithTypes.ts
export interface UpgradeRecipe {
  id: string;
  name: string;
  inputEquipmentId: string;
  outputEquipmentId: string;
  cost: {
    gold: number;
    materials: { itemId: string; count: number }[];
  };
  successRate: number;  // 0-1
}
```

### Add Upgrade Recipe

```typescript
// src/domain/camps/data/BlacksmithData.ts
export const UPGRADE_RECIPES: UpgradeRecipe[] = [
  {
    id: "upgrade_sword_1",
    name: "剣の強化",
    inputEquipmentId: "basic_sword",
    outputEquipmentId: "enhanced_sword",
    cost: {
      gold: 500,
      materials: [{ itemId: "iron_ore", count: 3 }],
    },
    successRate: 0.8,
  },
];
```

## Sanctuary System

```typescript
// src/domain/camps/types/SanctuaryTypes.ts
export interface SanctuaryService {
  id: string;
  name: string;
  description: string;
  effect: "heal" | "remove_debuff" | "blessing";
  value: number;
  cost: {
    gold?: number;
    magicStone?: number;
  };
}
```

## Library System

```typescript
// Card Encyclopedia: src/domain/camps/data/CardEncyclopediaData.ts
// Enemy Encyclopedia: src/domain/camps/data/EnemyEncyclopediaData.ts
// Game Tips: src/domain/camps/data/GameTipsData.ts
```

## Resource Context

Resources managed in `ResourceContext`:

```typescript
interface ResourceState {
  gold: number;
  magicStone: number;
}

// Usage
const { gold, magicStone, spendGold, addGold } = useResource();
```

## Adding New Facility

1. **Create Types** - `src/domain/camps/types/NewFacilityTypes.ts`
2. **Create Data** - `src/domain/camps/data/NewFacilityData.ts`
3. **Create Component** - `src/components/camp/NewFacility.tsx`
4. **Add to CampScreen** - Add facility button and routing

## Design Doc Reference

Camp design docs: `.claude/docs/camp_document/`
- `camp_facilities_design.md`
- `shop_design.md` / `shop_implementation_guide.md`
- `guild_design.md` / `guild_implementation_guide.md`
- `blacksmith_design.md` / `blacksmith_implementation_guide.md`
- `sanctuary_design.md`
- `library_design.md`
- `storage_design.md`
