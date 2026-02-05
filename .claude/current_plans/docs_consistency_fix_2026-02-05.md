# Design Document Consistency Fix - 2026-02-05

## Summary

Fixed inconsistencies between design documents (.claude/docs/) and implementation code, as well as internal inconsistencies within design documents.

## Changes Made

### Phase 1: Soul System Critical Fix (Code + Document)

**Code Fix:**
- File: `src/domain/camps/logic/soulSystem.ts`
- Change: `handleDeath()` now transfers 100% of souls to totalSouls (V3.0 design)
- Was: Losing all currentRunSouls on death (V2.0 behavior)

**Document Fix:**
- File: `.claude/docs/camp_document/sanctuary_design.md`
- Removed V3.1 implementation notes that contradicted V3.0 design
- Unified document to V3.0 specification (100% soul retention on death)

### Phase 2: Teleport Stone Documentation Update

- File: `.claude/docs/camp_document/shop_design.md`
- Updated version from V1.1 to V3.0
- Changed Teleport Stone section from 3 types (70%/80%/60%) to 1 unified type (100%)
- Matches actual implementation in `ShopData.ts`

### Phase 3: Journal Document Replacement

- File: `.claude/docs/journal_document/journal_system_implementation_plan.md`
- Complete rewrite from scratch
- Was: Incorrectly contained Lives System content
- Now: Proper Journal specifications (Tactics, Memories, Thoughts, Settings)

### Phase 4: Exploration Limit Cleanup

- Files:
  - `.claude/docs/camp_document/camp_facilities_design.md`
  - `.claude/docs/camp_document/sanctuary_design.md`
- Updated outdated "exploration_limit" terminology to "Lives System"
- Updated UI mockups to show Lives (❤️) instead of exploration counts
- Updated skill tree descriptions (Soul Resonance instead of Exploration Extension)

## Verification

- `npm run build`: ✅ Success
- `npm run test:run`: ✅ 62 tests passed

## Files Modified

### Code (1 file)
| File | Lines Changed |
|------|---------------|
| `src/domain/camps/logic/soulSystem.ts` | ~15 |

### Documentation (4 files)
| File | Changes |
|------|---------|
| `sanctuary_design.md` | V3.1 notes removed, V3.0 unified |
| `shop_design.md` | Teleport Stone 3→1 type |
| `journal_system_implementation_plan.md` | Complete rewrite (~380 lines) |
| `camp_facilities_design.md` | exploration_limit→Lives terminology |

## Related Documents

- `deathHandler.ts` already implemented correct 100% soul transfer
- `ShopData.ts` already had single teleport_stone type
