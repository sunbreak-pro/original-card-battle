# Title System - Future Feature

## Change History

| Date | Content |
|------|---------|
| 2026-02-04 | Extracted from library_design.md and guild_design.md |

---

## 1. Overview

Achievement-based title system that provides cosmetic rewards and optional gameplay bonuses.

## 2. Planned Features

### 2.1 Title Acquisition

- Unlocked by completing specific achievements
- Some titles provide gameplay effects
- Some titles are cosmetic only

### 2.2 Title Examples

| Title | Condition | Effect |
|-------|-----------|--------|
| Goblin Slayer | Defeat 100 Goblins | Gold from Goblins +10% |
| Flame Wielder | Use Fire cards 100 times | None (Cosmetic) |
| Depth Master | Reach Depth 5 | None (Cosmetic) |

### 2.3 Title Display

- Current title shown in player profile
- Title selection from unlocked titles
- Title unlock notifications

## 3. Data Structure

```typescript
interface Title {
  id: string;
  name: string;
  description: string;
  icon: string;
  condition: string;
  effect?: string; // Optional gameplay effect
  unlockedAt?: Date;
}
```

## 4. Integration Points

- **Location**: Journal (Chapter 2 "Memory") or dedicated Achievements section
- **Context**: May require LibraryContext or dedicated TitleContext
- **Display**: Player profile, Guild status

## 5. Statistics Tracking

Titles require tracking various player statistics:

```typescript
interface Statistics {
  totalPlayTime: number;
  totalRuns: number;
  deepestDepth: number;
  clearCount: number;
  deathCount: number;
  totalDefeats: number;
  maxDamage: number;
  longestCombo: number;
  totalGoldEarned: number;
  totalSoulEarned: number;
  maxGoldHeld: number;
}
```

## 6. Implementation Priority

**Priority**: Low
**Phase**: Phase 2-3 (Expansion/Completion)
**Dependencies**:
- Statistics tracking system
- Encyclopedia system (for discovery-based titles)
- Journal system (for UI display)

## 7. References

- `library_design.md` Section 5 - Book of Records (Achievements)
- `guild_design.md` Section 2.3 - Reward Bonus Unlocks
- `camp_facilities_design.md` Section 7.2 Phase 2
