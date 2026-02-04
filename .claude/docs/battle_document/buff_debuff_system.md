# Buff/Debuff System Integrated Design Document (Ver 5.0)

**Date:** 2026-01-30

**Status:** Code Implemented

## Table of Contents

```
1. System Overview
2. Stacking System
3. Buff/Debuff Database (42 Types)
4. Duration Management
5. Calculation Priority
6. Implementation Functions

```

---

# 1. System Overview

## 1.1 Basic Specifications

```typescript
/**
 * Buff/Debuff Interface (Ver 5.0)
 */
interface BuffDebuff {
  type: BuffDebuffType; // Type of buff/debuff
  stacks: number; // Number of stacks
  duration: number; // Remaining turns
  value: number; // Effect value (multiplier or damage amount)
  isPermanent: boolean; // Permanent flag
  source?: string; // Origin (Card ID, Equipment ID, etc.)
}

type BuffDebuffMap = Map<BuffDebuffType, BuffDebuff>;

```

## 1.2 Ver 5.0 Key Changes

```
【Changes from Ver 4.0】
- Total Count: 30 → 42 (19 Debuffs + 23 Buffs)
- Refined Reductions: weak/atkDown/speedDown/healingDown split into Minor/Major tiers, Weakness, and Prostration.
- Speed Logic: speedUp/speedDown replaced by Haste/Super Fast/Slow/Stall.
- Removals: guardUp, thorns, barrier, damageReduction, splash (removed for simplification).
- Class Specifics: swordEnergyBoost/Efficiency unified to swordEnergyGain; resonanceExtension replaced by Elemental Mastery + 5 Field Buffs.
- Tenacity: Changed from "Debuff Resist" to "+Value% to all stats when HP ≤ 30%."
- Last Stand: Changed from a stat buff to "Survive lethal damage once."

【Additions】
- Status: Burn, Over-Curse, Stagger, Freeze.
- Stat Tiers: atkDownMinor/Major, defDownMinor/Major, weakness, prostration.
- Speed: Stall (severe speed drop).
- Offensive Buffs: atkUpMinor/Major, defUpMinor/Major, penetrationUp, hitRateUp, criticalUp.
- Fields: fireField, iceField, electroField, darkField, lightField.

【Adjustments】
- Curse: Healing -50% → -20% (Over-Curse takes the -50% slot).
- Poison: Stacks × 2 → Fixed 5 dmg/turn (Stackable duration/instances).
- Bleed: 5% Max HP → 3% Max HP.
- Haste: Speed +30 (Fixed) → Speed +15.

```

---

# 2. Stacking System

## 2.1 Stack Addition Rules

```typescript
/**
 * Add or Update Buff/Debuff (Ver 5.0)
 */
export const addOrUpdateBuffDebuff = (
  map: BuffDebuffMap,
  type: BuffDebuffType,
  stacks: number,
  duration: number,
  value: number,
  isPermanent: boolean = false,
  source?: string,
): BuffDebuffMap => {
  const newMap = new Map(map);
  const existing = newMap.get(type);

  if (existing) {
    // If exists: Add stacks, use the maximum of durations and values
    newMap.set(type, {
      ...existing,
      stacks: existing.stacks + stacks,
      duration: Math.max(existing.duration, duration),
      value: Math.max(existing.value, value),
    });
  } else {
    // New entry
    newMap.set(type, {
      type,
      stacks,
      duration,
      value,
      isPermanent,
      source,
    });
  }

  return newMap;
};

```

---

# 3. Buff/Debuff Database (42 Types Total)

> **Source of truth:** `src/constants/data/battles/buffData.ts`

## 3.1 Debuffs - Damage over Time (5 Types)

### ID: poison

| Item | Value |
| --- | --- |
| **Name** | Poison |
| **Effect** | Fixed 5 damage at turn end (Bypasses Defense) |
| **Stacking** | Possible |
| **Color** | #66cc00 |

### ID: bleed

| Item | Value |
| --- | --- |
| **Name** | Bleed |
| **Effect** | **3%** Max HP damage per card play / enemy action |
| **Stacking** | Possible |
| **Color** | #cc0000 |

### ID: burn

| Item | Value |
| --- | --- |
| **Name** | Burn |
| **Effect** | Fixed 3 damage at turn end (Bypasses Defense) |
| **Stacking** | Possible |
| **Color** | #ff4500 |

### ID: curse

| Item | Value |
| --- | --- |
| **Name** | Curse |
| **Effect** | Healing received **-20%** |
| **Stacking** | Possible |
| **Color** | #9900cc |

### ID: overCurse

| Item | Value |
| --- | --- |
| **Name** | Over-Curse |
| **Effect** | Healing received **-50%** |
| **Stacking** | Possible |
| **Color** | #660066 |

---

## 3.2 Debuffs - Status Ailments (3 Types)

### ID: stagger

| Item | Value |
| --- | --- |
| **Name** | Stagger |
| **Effect** | Unable to act (Temporary) |
| **Stacking** | Possible |

### ID: stun

| Item | Value |
| --- | --- |
| **Name** | Stun |
| **Effect** | Unable to act (Turn skip) |
| **Stacking** | Possible |

### ID: freeze

| Item | Value |
| --- | --- |
| **Name** | Freeze |
| **Effect** | Unable to act (Ice attribute) |
| **Stacking** | Not Possible |

---

## 3.3 Debuffs - Stat Reduction (8 Types)

### ID: atkDownMinor (Enfeeble)

* **Effect:** ATK **-15%**
* **Stacking:** Not Possible

### ID: atkDownMajor (Powerless)

* **Effect:** ATK **-30%**
* **Stacking:** Not Possible

### ID: defDownMinor (Frail)

* **Effect:** DEF **-15%**
* **Stacking:** Not Possible

### ID: defDownMajor (Vulnerable)

* **Effect:** DEF **-30%**
* **Stacking:** Not Possible

### ID: weakness

* **Effect:** All Stats **-20%**
* **Stacking:** Possible

### ID: prostration

* **Effect:** All Stats **-50%**
* **Stacking:** Possible

### ID: slow

* **Effect:** Speed **-10** (Fixed) per stack
* **Stacking:** Possible

### ID: stall

* **Effect:** Speed **-15** (Major penalty)
* **Stacking:** Possible

---

## 3.4 Buffs - Stat Enhancement (9 Types)

### ID: atkUpMinor (Might)

* **Effect:** ATK **+15%**
* **Stacking:** Not Possible

### ID: atkUpMajor (Great Might)

* **Effect:** ATK **+30%**
* **Stacking:** Not Possible

### ID: defUpMinor (Fortify)

* **Effect:** DEF **+15%**
* **Stacking:** Not Possible

### ID: defUpMajor (Adamantine)

* **Effect:** DEF **+30%**
* **Stacking:** Not Possible

### ID: penetrationUp

* **Effect:** Penetration **+30%**
* **Stacking:** Possible

### ID: hitRateUp

* **Effect:** Hit Rate **+15%**
* **Stacking:** Possible

### ID: criticalUp

* **Effect:** Critical Damage **+15%**
* **Stacking:** Possible

### ID: haste

* **Effect:** Speed **+15**
* **Stacking:** Possible

### ID: superFast

* **Effect:** Speed **+30**
* **Stacking:** Possible

---

## 3.5 Buffs - Recovery & Defense (4 Types)

### ID: regeneration

* **Effect:** Heal 5 HP at turn start
* **Stacking:** Possible (Value × Stacks)

### ID: shieldRegen

* **Effect:** Gain 5 Guard at turn start
* **Stacking:** Possible (Value × Stacks)

### ID: reflect

* **Effect:** Reflect **30%** of damage taken
* **Stacking:** Possible

### ID: immunity

* **Effect:** Negate all damage
* **Stacking:** Possible

---

## 3.6 Buffs - Resource Management (3 Types)

### ID: energyRegen

* **Effect:** +1 Energy at turn start
* **Stacking:** Possible

### ID: drawPower

* **Effect:** +1 Draw at turn start
* **Stacking:** Possible

### ID: costReduction

* **Effect:** Card Cost -1
* **Stacking:** Possible

---

## 3.7 Buffs - Combat Styles (2 Types)

### ID: lifesteal

* **Effect:** Heal HP for **30%** of damage dealt
* **Stacking:** Possible

### ID: doubleStrike

* **Effect:** Next attack card triggers twice (50% power each)
* **Stacking:** Possible

---

## 3.8 Buffs - Special Effects (5 Types)

### ID: focus

* **Effect:** Next card effect **+50%**
* **Stacking:** Possible

### ID: momentum

* **Effect:** ATK **+5%** per card played
* **Stacking:** Possible (Increments stack at turn end)

### ID: tenacity

* **Effect:** All stats **+30%** when HP ≤ 30%
* **Stacking:** Possible

### ID: lastStand

* **Effect:** Survive lethal damage once
* **Stacking:** Possible

### ID: cleanse

* **Effect:** Remove 1 debuff at turn end
* **Stacking:** Possible

---

## 3.9 Buffs - Class Specific (7 Types)

### Warrior Type

| ID | Name | Effect |
| --- | --- | --- |
| `swordEnergyGain` | Sword Qi Refinement | Sword Qi gain +3% on attack |

### Mage Type

| ID | Name | Effect |
| --- | --- | --- |
| `elementalMastery` | Elemental Mastery | Attribute damage +30% |
| `fireField` | Blazing Field | Fire card effects +50% |
| `iceField` | Frozen Field | Ice damage +50% |
| `electroField` | Thunder Field | +10 damage on Lightning card play |
| `darkField` | Abyssal Field | Dark damage +50% |
| `lightField` | Radiant Field | Light damage +50% |

---

# 4. Duration Management

```typescript
/**
 * Decrease duration by turn progression
 */
export const decreaseBuffDebuffDuration = (
  map: BuffDebuffMap,
): BuffDebuffMap => {
  const newMap = new Map<BuffDebuffType, BuffDebuff>();

  map.forEach((buff, type) => {
    if (buff.isPermanent) {
      // No change for permanent buffs
      newMap.set(type, buff);
    } else if (buff.duration > 1) {
      // Decrease duration
      newMap.set(type, {
        ...buff,
        duration: buff.duration - 1,
      });
    }
    // Items with duration === 1 are removed (not added to new map)
  });

  return newMap;
};

```

---

# 5. Calculation Priority

## 5.1 Damage Calculation

```typescript
function calculateAttackMultiplier(buffDebuffs: BuffDebuffMap): number {
  let multiplier = 1.0;

  // Attack Up Buffs
  if (buffDebuffs.has("atkUpMinor")) multiplier += 0.15;
  if (buffDebuffs.has("atkUpMajor")) multiplier += 0.30;

  // Attack Down Debuffs
  if (buffDebuffs.has("atkDownMinor")) multiplier *= 0.85;
  if (buffDebuffs.has("atkDownMajor")) multiplier *= 0.70;

  // Broad Reductions
  if (buffDebuffs.has("weakness")) multiplier *= 0.80;
  if (buffDebuffs.has("prostration")) multiplier *= 0.50;

  // Momentum (Stack accumulation)
  if (buffDebuffs.has("momentum")) {
    const momentum = buffDebuffs.get("momentum")!;
    multiplier += (momentum.value / 100) * momentum.stacks;
  }

  return multiplier;
}

```

## 5.2 Speed Calculation

```typescript
function calculateSpeed(baseSpeed: number, buffDebuffs: BuffDebuffMap): number {
  let speed = baseSpeed;

  if (buffDebuffs.has("slow")) {
    const slow = buffDebuffs.get("slow")!;
    speed -= slow.value * slow.stacks; // value=10
  }
  if (buffDebuffs.has("stall")) {
    const stall = buffDebuffs.get("stall")!;
    speed -= stall.value * stall.stacks; // value=15
  }
  if (buffDebuffs.has("haste")) {
    const haste = buffDebuffs.get("haste")!;
    speed += haste.value * haste.stacks; // value=15
  }
  if (buffDebuffs.has("superFast")) {
    const superFast = buffDebuffs.get("superFast")!;
    speed += superFast.value * superFast.stacks; // value=30
  }

  return Math.max(0, speed);
}

```

---

# 6. Implementation Functions

## 6.1 Management

* `addOrUpdateBuffDebuff`: Upsert logic for buffs.
* `removeBuffDebuff`: Direct removal by ID.
* `removeAllDebuffs`: Clears all harmful status effects.
* `decreaseBuffDebuffDuration`: Turn-end duration tick.

## 6.2 Combat Logic

* `calculateEndTurnDamage`: Sum of Poison, Burn, etc.
* `calculateBleedDamage`: 3% Max HP per action.
* `calculateStartTurnHealing`: HP/Guard regeneration sum.
* `calculateAttackMultiplier`: Cumulative ATK modifiers.
* `calculateSpeed`: Final speed stat calculation.

## 6.3 State Verification

* `canAct`: Returns false if Stun/Stagger/Freeze are present.
* `calculateEnergyModifier`: Adjusts energy gain based on `energyRegen`.
* `calculateDrawModifier`: Adjusts hand replenishment via `drawPower`.

---

# Reference Map

```
battle_logic.md (Ver 4.0) [Master Document]
└── buff_debuff_system.md (Ver 5.0) [This Document]
    ├── Data → src/constants/data/battles/buffData.ts
    ├── Damage → src/domain/battles/calculators/damageCalculation.ts
    ├── Speed → src/domain/battles/calculators/phaseCalculation.ts
    └── Management → src/domain/battles/logic/buffLogic.ts

```