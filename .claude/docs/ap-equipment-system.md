# AP Equipment System Design

## Overview

AP (Armor Points) is derived entirely from defense equipment durability. Characters have no base AP stat — all AP comes from equipped defense items.

## AP Source

- **Defense slots that contribute AP:** armor, helmet, boots, accessory1, accessory2
- **Weapon does NOT contribute AP** (weapon effects persist regardless of AP state)
- AP bonus per slot is looked up from `EQUIPMENT_STAT_BONUSES[slot][rarity].apBonus`
- Each item's AP contribution scales with durability: `contribution = apBonus * (durability / maxDurability)`
- `maxAP` = sum of all `apBonus` values at full durability
- `currentAP` = sum factoring current durability ratios

## Damage Absorption Flow

```
Incoming Damage → Guard → AP → HP
```

1. Guard absorbs damage first (temporary per-phase shield)
2. Remaining damage hits AP (equipment durability)
3. Any remaining damage hits HP

## AP Damage → Equipment Durability

When AP absorbs X damage:
1. Distribute X across equipped defense items **proportionally** to their AP contribution ratio
2. Each item's durability decreases by its share: `slotDamage = floor(X * slotContribution / totalAP)`
3. Last slot absorbs rounding remainder
4. Durability cannot go below 0

### Key function: `distributeApDamageToEquipment(apDamage, equipmentSlots)`

Returns `Record<EquipmentSlot, number>` — durability reduction per slot.

## Broken Equipment (Durability = 0)

- Defense equipment with durability = 0 **loses all stat bonuses** (defPercent, hpBonus, apBonus, etc.)
- Weapon effects persist regardless of AP state
- Broken state persists after returning to camp
- UI shows "break!" when total AP = 0

### Key function: `getEffectiveEquipmentBonuses(equipmentSlots)`

Skips stat bonuses for broken defense equipment, always includes weapon bonuses.

## Persistence

- Equipment durability changes persist in `PlayerContext.playerState.equipmentSlots`
- `resetRuntimeState()` derives AP from current equipment durability (does NOT reset to max)
- AP persists across battles within the same exploration
- AP persists when returning to camp
- Battle initialization computes AP from equipment state at battle start

## Repair Mechanics

- **Blacksmith repair** restores durability to max (`performRepair` in `blacksmithLogic.ts`)
- **Equipment swap** at storage replaces broken equipment with new items
- These are the only ways to restore AP after equipment is broken

## Integration Points

| Component | Role |
|-----------|------|
| `equipmentStats.ts` | Core AP calculation, durability distribution, broken item checks |
| `PlayerContext.tsx` | `applyEquipmentDurabilityDamage()` — updates equipment + runtime AP |
| `PlayerContext.tsx` | `resetRuntimeState()` — derives AP from equipment durability |
| `executeCharacterManage.ts` | `onApDamage` callback in enemy phase execution |
| `useBattleOrchestrator.ts` | Passes `onApDamage` option to phase execution |
| `BattleScreen.tsx` | Wires `applyEquipmentDurabilityDamage` from PlayerContext to battle |
| `EquipmentTab.tsx` | Shows total AP and per-slot AP contribution |
| `PlayerFrame.tsx` | Shows AP bar and "break!" indicator |
| `blacksmithLogic.ts` | Repair restores durability |

## Data Flow Diagram

```
BattleScreen
  └─ usePlayer().applyEquipmentDurabilityDamage
       └─ distributeApDamageToEquipment(damage, slots)
       └─ applyDurabilityDamage(slots, distribution)
       └─ calculateEquipmentAP(updatedSlots) → new AP
       └─ setPlayerState (equipment) + setRuntimeState (AP)

PlayerContext.resetRuntimeState()
  └─ calculateEquipmentAP(playerState.equipmentSlots) → AP at exploration start

EquipmentTab
  └─ calculateEquipmentAP(slots) → display AP summary
```
