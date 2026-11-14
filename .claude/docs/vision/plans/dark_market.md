# Dark Market - Future Feature

> **Status**: ON HOLD。一部を実装しました。珍しい装備を高い値で売るショップのタブは、2026-02-05 に入りました（記録は `.claude/archive/dark_market_implementation_2026-02-05.md`）。隠れた欠点のある品、信用の仕組み、入場の条件は実装していません。ショップは凍結していて、Web 版 `src/` は旧ループのまま止まっているためです。

## Change History

| Date | Content |
|---|---|
| 2026-02-04 | Extracted from basecamp_consolidation.md |

---

## 1. Overview

A high-risk, high-reward trading system offering rare items and special deals not available in the regular Shop.

## 2. Planned Features

### 2.1 Unique Inventory

- Rare equipment not available in regular Shop
- Limited-time offers
- High-risk items with unknown effects

### 2.2 Risk Elements

- Some items may have hidden drawbacks
- Higher prices but better quality
- Trust/reputation system possible

### 2.3 Access Conditions

- May require specific progression milestones
- Could be unlocked via NPC interactions
- Possible random availability

## 3. Integration Points

- **Facility**: Extension of Shop, or separate facility
- **Context**: ShopContext extension or DarkMarketContext
- **UI**: Distinct visual style (darker, more mysterious)

## 4. Gameplay Balance

- Should not overshadow regular Shop
- Provides alternative progression path
- Risk-reward balance is crucial

## 5. Implementation Priority

**Priority**: Low
**Phase**: Phase 3+ (Post-completion expansion)
**Dependencies**:
- Core Shop functionality
- Item rarity system
- Economy balance established

## 6. References

- `basecamp_consolidation.md` - Mentioned as Guild future expansion
- `shop_design.md` - Base Shop system
