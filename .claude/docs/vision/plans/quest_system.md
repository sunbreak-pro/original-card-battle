# Quest System - Future Feature

## Change History

| Date | Content |
|------|---------|
| 2026-02-04 | Extracted from guild_design.md and camp_facilities_design.md |

---

## 1. Overview

Daily and weekly quest system to provide structured goals and rewards beyond normal dungeon exploration.

## 2. Planned Features

### 2.1 Quest Types

- **Daily Quests**: Reset every day, smaller rewards
- **Weekly Quests**: Reset weekly, larger rewards

### 2.2 Quest Objectives

```typescript
interface QuestObjective {
  type: "defeat" | "collect" | "explore";
  target: string; // EnemyID, ItemID, Depth, etc.
  required: number;
  current: number;
}
```

### 2.3 Quest Rewards

- Gold
- Magic Stones
- Consumable Items
- Experience (Soul Remnants)

### 2.4 Unlock Conditions

- Higher difficulty quests unlock with rank promotions
- Quest variety increases with game progression

## 3. Data Structure

```typescript
interface Quest {
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

interface QuestReward {
  gold?: number;
  magicStones?: number;
  items?: string[];
  experience?: number;
}
```

## 4. UI Design (Placeholder)

The Guild design currently shows:
```
Quest Tab: "Coming Soon..."
```

Full UI to be designed when implementation begins.

## 5. Integration Points

- **Facility**: Guild (Quests sub-tab under Headquarters)
- **Context**: GuildContext (already has quest-related interfaces)
- **Triggers**: Quest progress updates during battle/exploration

## 6. Implementation Priority

**Priority**: Medium
**Phase**: Phase 2 (Expansion)
**Dependencies**:
- Guild facility core functionality
- Battle system for objective tracking
- Time-based reset system

## 7. References

- `guild_design.md` Section 4 - Quest specifications
- `camp_facilities_design.md` Section 7.1 Phase 2
