# Damage Calculation & Buff/Debuff System

## Overview

Layered damage system with Guard → AP → HP priority and a phase-aware buff duration model where buffs only tick during their applier's phase.

## File Map

| File | Lines | Role |
|------|-------|------|
| `src/domain/battles/calculators/damageCalculation.ts` | 87 | Core damage formula + allocation |
| `src/domain/battles/calculators/buffCalculation.ts` | 167 | Buff effect multipliers, healing, DoT, utility |
| `src/domain/battles/logic/buffLogic.ts` | 153 | Buff CRUD operations + duration management |
| `src/domain/battles/logic/bleedDamage.ts` | 17 | Bleed damage formula |
| `src/domain/battles/managements/damageManage.ts` | 107 | Damage calculation wrappers for card/enemy |
| `src/domain/battles/execution/playerPhaseExecution.ts` | 150 | Phase start/end effects (player) |
| `src/domain/battles/execution/enemyPhaseExecution.ts` | 248 | Phase start/end effects (enemy) |
| `src/constants/data/battles/buffData.ts` | 111 | BUFF_EFFECTS definitions |
| `src/constants/battleConstants.ts` | 97 | All numeric constants |
| `src/types/battleTypes.ts` | 247 | BuffDebuffState, DamageResult, etc. |

## Data Structures

### BuffDebuffState

```typescript
interface BuffDebuffState {
  name: BuffDebuffType;           // e.g. "bleed", "atkUpMinor"
  stacks: number;                 // Quantity (if stackable)
  duration: number;               // Turns remaining
  value: number;                  // Effect magnitude (from BUFF_EFFECTS)
  isPermanent: boolean;           // Never expires
  source?: string;                // Origin identifier
  appliedBy?: BuffOwner;          // 'player' | 'enemy' | 'environment'
}

type BuffDebuffMap = Map<BuffDebuffType, BuffDebuffState>;
```

### DamageResult / DamageAllocation

```typescript
interface DamageResult {
  finalDamage: number;
  isCritical: boolean;            // Always false (placeholder)
  reflectDamage: number;
  lifestealAmount: number;
}

interface DamageAllocation {
  guardDamage: number;
  apDamage: number;
  hpDamage: number;
}
```

### Key Constants

```
GUARD_BLEED_THROUGH_MULTIPLIER = 0.75   // HP leak when guard blocks & AP=0
BLEED_DAMAGE_RATE = 0.05                // 5% maxHP per bleed
GUARD_INIT_MULTIPLIER = 0.5             // Guard = maxAp × 0.5
CURSE_HEALING_MULTIPLIER = 0.2          // Curse reduces healing to 20%
OVER_CURSE_HEALING_MULTIPLIER = 0.5     // OverCurse reduces to 50%
FIRE_FIELD_BONUS_MULTIPLIER = 0.5       // +50% burn damage in fire field
BASE_PLAYER_SPEED = 50
PLAYER_BASE_DRAW_COUNT = 5
```

## Logic Flow

### Damage Calculation Pipeline

```
calculateDamage(attacker, defender, card)
  ↓
1. baseDmg = card.baseDamage || 0
  ↓
2. Attack buff multiplier:
   atkMultiplier = 1.0
   + atkUpMinor.value/100         (e.g. +0.15)
   + atkUpMajor.value/100         (e.g. +0.30)
   + momentum.value/100 * stacks  (e.g. +0.05 per stack)
   × (1 - atkDownMinor.value/100) (e.g. ×0.85)
   × (1 - atkDownMajor.value/100) (e.g. ×0.70)
  ↓
3. finalAtk = floor(baseDmg × atkMultiplier)
  ↓
4. Defense modifiers on defender:
   vulnerabilityMod = 1.0
   × (1 + defDownMinor/100)       (e.g. ×1.15)
   × (1 + defDownMajor/100)       (e.g. ×1.30)
   IF tenacity AND vulnMod > 1:
     excess = vulnMod - 1
     vulnMod = 1 + excess × (1 - tenacity/100)

   damageReductionMod = 1.0
   × (1 - defUpMinor/100)         (e.g. ×0.85)
   × (1 - defUpMajor/100)         (e.g. ×0.70)
  ↓
5. incomingDmg = floor(finalAtk × vulnerabilityMod × damageReductionMod)
  ↓
6. reflectDamage = floor(incomingDmg × reflect.value/100)   (30% default)
   lifestealAmount = floor(incomingDmg × lifesteal.value/100) (30% default)
  ↓
RETURN { finalDamage: incomingDmg, isCritical: false, reflectDamage, lifestealAmount }
```

### Damage Allocation (Guard → AP → HP)

```
applyDamageAllocation(defender, damage)
  ↓
hadGuard = defender.guard > 0

CASE 1: Guard absorbs all + AP empty
  guard >= damage AND ap <= 0
  → guardDmg = damage
  → hpDmg = floor(damage × 0.75)  ← BLEED-THROUGH
  → apDmg = 0

CASE 2: Guard absorbs all + AP available
  guard >= damage AND ap > 0
  → guardDmg = damage
  → apDmg = 0, hpDmg = 0

CASE 3: Guard partially absorbs
  guard < damage
  → guardDmg = guard
  → remaining = damage - guard
  → continue to AP step

AP STEP:
  IF ap >= remaining → apDmg = remaining
  ELSE → apDmg = ap, remaining -= ap → continue to HP

HP STEP:
  hpDmg = remaining
```

### Buff Lifecycle

```
CREATION:
  addOrUpdateBuffDebuff(map, name, duration, value, stacks, isPermanent, source, appliedBy)
  ├─ IF exists: stacks += new, duration = max(old, new), value = max(old, new)
  │             appliedBy preserved from original
  └─ IF new: create with all params

DURATION TICK (phase-aware):
  decreaseBuffDebuffDurationForPhase(map, currentActor)
  FOR EACH buff:
  ├─ isPermanent → keep unchanged
  ├─ appliedBy === currentActor → duration - 1 (remove if ≤ 0)
  └─ appliedBy !== currentActor → keep unchanged

  Example: Enemy applies stun(dur=2) to player
    Player phase: stun stays dur=2  (enemy applied it)
    Enemy phase: stun → dur=1
    Next enemy phase: stun → dur=0, removed

REMOVAL:
  removeBuffDebuff(map, type) → delete single buff
  removeAllDebuffs(map) → keep only non-debuff entries (cleanse)

DEPRECATED:
  decreaseBuffDebuffDuration(map) → decreases ALL regardless of appliedBy
```

### Phase Start/End Effects

**Player Phase Start:**
```
1. decreaseBuffDebuffDurationForPhase(buffs, 'player')
2. calculateStartPhaseHealing(originalBuffs) → {hp, shield}
3. IF "cleanse": removeAllDebuffs(newBuffs)
4. Draw cards: PLAYER_BASE_DRAW_COUNT + drawModifier
```

**Player Phase End:**
```
1. calculateEndPhaseDamage(buffs)
   burn → +value damage
   burn + fireField → +value × 1.5 damage
   poison → +value damage
2. IF "momentum": stacks += 1
```

**Enemy Phase Start:**
```
1. decreaseBuffDebuffDurationForPhase(buffs, 'enemy')
2. calculateStartPhaseHealing(originalBuffs) → {hp, shield}
3. Guard reset (if startingGuard): floor(baseMaxAp × 0.5)
4. Energy reset: enemy.actEnergy
5. canAct check: !stun
```

**Enemy Phase End:**
```
1. calculateEndPhaseDamage(buffs) → DoT to enemy
```

### Bleed System

```
calculateBleedDamage(maxHp, buffMap):
  IF "bleed" exists:
    damage = floor(maxHp × 0.05)  per stack? (code: BLEED_DAMAGE_RATE)
  Triggered: after each card play (not per-hit)

Sword Energy auto-bleed (Swordsman):
  energy >= 5:  20% chance → bleed(dur=3, stacks=1)
  energy >= 8:  40% chance
  energy = 10:  60% chance
  Applied once after all hits complete
```

### Healing Modifiers

```
Base healing from regeneration: value × stacks per phase

IF "curse":      heal = floor(heal × 0.2)    ← 80% reduction
IF "overCurse":  heal = floor(heal × 0.5)    ← 50% reduction

Both curse modifiers apply multiplicatively in sequence:
  curse + overCurse: heal = floor(floor(heal × 0.2) × 0.5)
```

### Utility Buff Functions

```
canAct(map)             → !stun (freeze/stagger not checked here)
energyRegenBuff(map)    → sum of energyRegen buff values
calculateDrawModifier(map) → sum of drawPower.value × stacks
immunityBuff(map, type) → IF "immunity" exists, block debuffs only
```

## Dependencies

```
damageCalculation.ts
  ├─ imports: buffCalculation.ts (attack/defense modifiers, reflect, lifesteal)
  └─ imports: battleConstants (GUARD_BLEED_THROUGH_MULTIPLIER)

buffCalculation.ts
  ├─ imports: buffData.ts (BUFF_EFFECTS definitions)
  └─ imports: battleConstants (CURSE/FIRE_FIELD multipliers)

buffLogic.ts
  └─ imports: buffData.ts (BUFF_EFFECTS for stackable/value lookup)

playerPhaseExecution.ts
  ├─ imports: buffCalculation.ts (healing, DoT, draw modifier)
  ├─ imports: buffLogic.ts (duration decrease, debuff removal)
  └─ imports: deck.ts (drawCards) — IMMUTABLE ZONE

enemyPhaseExecution.ts
  ├─ imports: buffCalculation.ts (healing, DoT, canAct)
  ├─ imports: buffLogic.ts (duration decrease, add buff)
  ├─ imports: damageCalculation.ts (damage + allocation)
  ├─ imports: bleedDamage.ts
  └─ imports: enemyAI.ts (enemyAction converter)
```

## Buff Catalog

### Damage-Dealing Debuffs
| Name | Value | Stackable | Timing |
|------|-------|-----------|--------|
| bleed | 5% maxHP | Yes | After card play |
| poison | 5 flat | Yes | Phase end |
| burn | 3 flat (+50% in fireField) | Yes | Phase end |
| curse | 80% heal reduction | Yes | Passive |
| overCurse | 50% heal reduction | Yes | Passive |

### Control Debuffs
| Name | Effect | Stackable |
|------|--------|-----------|
| stun | Skip turn | Yes |
| freeze | Cannot act | No |
| stagger | Cannot act | Yes |

### Stat Debuffs
| Name | Effect | Stackable |
|------|--------|-----------|
| atkDownMinor | -15% damage | No |
| atkDownMajor | -30% damage | No |
| defDownMinor | +15% incoming | No |
| defDownMajor | +30% incoming | No |
| weakness | -20% all stats | Yes |
| prostoration | -50% all stats | Yes |
| slow | -10 speed | Yes |
| stall | -15% speed | Yes |

### Positive Buffs
| Name | Effect | Stackable |
|------|--------|-----------|
| atkUpMinor | +15% damage | No |
| atkUpMajor | +30% damage | No |
| defUpMinor | -15% incoming | No |
| defUpMajor | -30% incoming | No |
| haste | +15 speed | Yes |
| superFast | +30 speed | Yes |
| regeneration | +5 HP/phase | Yes |
| shieldRegen | +5 guard/phase | Yes |
| reflect | 30% counter-damage | Yes |
| lifesteal | 30% heal from damage | Yes |
| momentum | +5% atk per stack (grows each phase) | Yes |
| tenacity | Reduce excess vulnerability | Yes |
| drawPower | +1 card draw per stack | Yes |
| energyRegen | +energy/phase | Yes |
| immunity | Block debuffs | — |
| cleanse | Remove all debuffs at phase start | — |

### Class Ability Buffs
| Name | Effect | Class |
|------|--------|-------|
| swordEnergyGain | +3% sword energy gain | Swordsman |
| elementalMastery | +30% elemental damage | Mage |
| fireField | +50% burn damage | Mage |
| electroField | +10 per lightning card | Mage |
| iceField | +50% ice damage | Mage |

## Vulnerability Analysis

### `[BUG-RISK]` Healing Uses Pre-Duration-Decrease Buffs

**Location:** `playerPhaseExecution.ts:71-74`, `enemyPhaseExecution.ts:88-91`

```typescript
let newBuffs = decreaseBuffDebuffDurationForPhase(playerBuffs, 'player');
const { hp, shield } = calculateStartPhaseHealing(playerBuffs); // ← original, not newBuffs
```

Healing is computed from the original buff map before duration decrease. A regeneration buff on its final tick (duration=1) still heals because `calculateStartPhaseHealing` sees it before it's removed. This may be intentional ("heal on last tick"), but it's inconsistent: DoT at phase end uses the post-mutation buff map (which includes momentum stacking).

### `[BUG-RISK]` Curse + OverCurse Stacking is Multiplicative

**Location:** `buffCalculation.ts:93-98`

```typescript
if (map.has("curse")) { hp = Math.floor(hp * 0.2); }
if (map.has("overCurse")) { hp = Math.floor(hp * 0.5); }
```

If both curse and overCurse are active, healing is reduced to `floor(floor(heal * 0.2) * 0.5)` = 10% of original. These are applied sequentially with floor rounding at each step, which can reduce small heals to 0 faster than a single 10% multiplier would. Unclear if intended.

### `[BUG-RISK]` Guard Bleed-Through Only When Guard Fully Absorbs

**Location:** `damageCalculation.ts:52-57`

```typescript
if (defender.guard >= remainingDmg && defender.ap <= 0) {
  guardDmg = remainingDmg;
  hpDmg = Math.floor(remainingDmg * GUARD_BLEED_THROUGH_MULTIPLIER);
```

Bleed-through (75% to HP) only triggers when guard **fully absorbs** the damage AND AP is 0. If guard partially absorbs (guard < damage), the remaining damage goes to HP normally at 100%, with no bleed-through multiplier. This means high-guard-low-AP targets take MORE damage from small hits (75% bleed) than from large hits that break guard (100% overflow to HP). This is counter-intuitive.

### `[BUG-RISK]` canAct Only Checks Stun

**Location:** `buffCalculation.ts:135-137`

```typescript
export const canAct = (map: BuffDebuffMap): boolean => {
    return !map.has("stun");
};
```

`freeze` and `stagger` are listed as control debuffs but are not checked by `canAct()`. Enemies/players with freeze or stagger can still act. Either these buffs are handled elsewhere (not found in the codebase), or they are non-functional.

### `[BUG-RISK]` immunityBuff Logic is Inverted

**Location:** `buffCalculation.ts:162-167`

```typescript
export const immunityBuff = (map: BuffDebuffMap, debuffType: BuffDebuffType): boolean => {
    if (map.has("immunity")) {
        return !BUFF_EFFECTS[debuffType].isDebuff;
    }
    return true;
};
```

Returns `true` if the buff should be applied, `false` if blocked. When immunity is active, it returns `!isDebuff` — so debuffs return `false` (blocked) and buffs return `true` (allowed). When no immunity, returns `true` (allow everything). The function name and return semantics are confusing — `true` means "allow", but the name `immunityBuff` suggests "is immune". Callers must understand the inverted semantics.

### `[BUG-RISK]` Bleed Damage Does Not Multiply by Stacks

**Location:** `bleedDamage.ts` vs `buffCalculation.ts`

The bleed damage formula in `bleedDamage.ts` calculates `floor(maxHp * BLEED_DAMAGE_RATE)` without multiplying by stacks. However, the buff catalog says bleed is stackable. Need to verify whether stacks affect bleed damage or only duration/presence.

### `[EXTENSIBILITY]` Attack/Defense Buffs Are Hardcoded If-Chains

**Location:** `buffCalculation.ts:9-62`

Each buff type is checked with individual `if (map.has("..."))` blocks. Adding a new attack or defense buff requires modifying the calculation functions directly. A data-driven approach (iterating over buff effects with a `category: 'atkUp' | 'defDown'` field) would be more extensible.

### `[EXTENSIBILITY]` No Damage Type System

All damage flows through a single `calculateDamage` pipeline. There's no concept of damage types (physical, magical, elemental) at the calculation level. Elemental bonuses (fireField, iceField) are handled as buff effects, not as part of the damage formula. Adding type-based resistances would require reworking the pipeline.

### `[QUALITY]` Deprecated Function Still Exists

**Location:** `buffLogic.ts:94-110`

`decreaseBuffDebuffDuration` (deprecated) coexists with `decreaseBuffDebuffDurationForPhase`. If any code path still calls the deprecated version, buffs will tick at wrong timing.

### `[QUALITY]` burn DoT Ignores Stacks

**Location:** `buffCalculation.ts:120-124`

```typescript
case "burn":
    buffDamage += buff.value;
    if (map.has("fireField")) {
        buffDamage += buff.value * FIRE_FIELD_BONUS_MULTIPLIER;
    }
```

Burn damage uses `buff.value` but not `buff.stacks`. Burn is marked as stackable in BUFF_EFFECTS, but stacking only affects duration management (via addOrUpdateBuffDebuff stacks accumulation), not the damage output. Poison has the same issue. Compare with regeneration which does use `value * stacks`.

### `[QUALITY]` Weakness and Prostoration Unused in Calculations

`weakness` (-20% all stats) and `prostoration` (-50% all stats) are defined in BUFF_EFFECTS but are not checked in `attackBuffDebuff()` or `defenseBuffDebuff()`. They exist as debuff types but have no mechanical effect in the damage pipeline.
