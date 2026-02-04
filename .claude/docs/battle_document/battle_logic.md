# Battle System Logic Specification (Ver 4.0)

## 1. Overview

This document defines the core logic for the "Roguelite Card RPG." It covers the following key systems:

1. **Asymmetrical Battle Flow:** Distinct energy systems for players and enemies.
2. **Action Speed System:** Turn order determination and initiative bonuses.
3. **Enemy Multi-Action System:** Action frequency controlled by Enemy Energy.
4. **Hybrid Defense System:** Separation of **AP** (Equipment Durability) and **Guard** (Temporary).
5. **Buff/Debuff System:** Simplified status effects and stack management.
6. **Damage Calculation:** Comprehensive formulas including modifiers.
7. **Condition Management:** Handling duration, stacks, and effect values.
8. **Depth System:** Enemy variety based on dungeon depth.

---

## 2. Battle Flow System (New in V4.0)

### 2.1 Asymmetrical Energy System

Players and enemies utilize different energy concepts.

#### **Player Side**

* **Energy:** Resource for playing cards (standard).
* **Base Energy:** 3 per turn.
* The player can play any number of cards as long as Energy permits.

#### **Enemy Side**

* **Enemy Energy = Action Count:** * 1 Energy = 1 Action (Attack, Buff, Defense, etc.).
* Energy varies based on the enemy type and dungeon depth.

### 2.2 Enemy Energy Calculation

```typescript
function calculateEnemyEnergy(enemy: Enemy, addEnergy: number): number {
  const baseEnergy = enemy.actEnergy;
  // Energy modifiers may fluctuate based on specific enemy skills
  return Math.floor(baseEnergy * addEnergy);
}

```

---

## 3. Action Speed System (Consecutive Phase System)

> **Source of truth:** `src/domain/battles/calculators/phaseCalculation.ts`

### 3.1 Speed Parameters

Both player and enemy possess a "Speed" stat.

* **Base Speed:** Player (Fixed: 50), Enemy (Unique per type).
* **Randomness:** ±5% variance with "Mean Reversion" to prevent extreme streaks.

### 3.2 Speed Modifiers

Speed is influenced by four primary buffs/debuffs:

* **Haste:** +15 Speed per stack.
* **Super Fast:** +30 Speed per stack.
* **Slow:** -10 Speed per stack.
* **Stall:** -15 Speed per stack.

### 3.3 Consecutive Phase System (Turn Order)

Instead of a simple "Player then Enemy" loop, turn order is determined by speed differences.

| Speed Difference | Result |
| --- | --- |
| **Diff < 15** | **Alternating Phases** (P → E → P → E) |
| **Diff ≥ 15** | **2 Consecutive Phases** for the faster unit (P → P → E) |
| **Diff ≥ 25** | **3 Consecutive Phases** for the faster unit (P → P → P → E) |
| **Every +10 Diff** | **+1 Additional Phase** |

---

## 4. Turn Flow (Ver 4.0 Updated)

### 4.2 Player Phase

1. **Phase Start:** * Reset Player Guard to 0.
* Tick down Buff/Debuff durations.
* Apply Regeneration/Shield effects.
* Gain Energy and Draw Cards.


2. **Speed Bonus:** If Player Speed > Enemy Speed, apply a temporary damage/effect bonus.
3. **Action Wait:** Wait for player to play cards and press "End Turn."
4. **Phase End:** * Apply DoT (Damage over Time) from Poison/Curse.
* Update "Momentum" stacks.



### 4.3 Enemy Phase

1. **Phase Start:** Reset Enemy Guard to 0. Apply Regeneration.
2. **Action Execution:** Enemy performs actions based on their **Action Energy**.
3. **Fallback Logic:** If an enemy has remaining energy but cannot afford a high-cost skill, they default to a **Basic Attack** (1 Cost) or **Wait** (0 Cost).

---

## 5. Defense System (Hybrid Defense)

Defensive mechanics are split into "Persistent Durability" and "Turn-based Guard."

### 5.1 Definitions

* **HP (Health Points):** Vitality. Death at 0.
* **AP (Armor Points):** Equipment durability. **Carries over between battles.** Requires repair cards/items to recover.
* **GP (Guard Points):** Temporary shield from cards. **Resets to 0 at the start of every turn.**

### 5.2 Damage Priority

Damage is subtracted in this specific order:

1. **Guard** (Block with shield)
2. **AP** (Absorb with armor)
3. **HP** (Take physical damage)

### 5.3 Armor Break

* **Trigger:** When AP reaches 0.
* **Penalty:** **50% of incoming damage bypasses Guard** and hits HP directly. The remaining 50% is mitigated by Guard as usual. (This reflects the vulnerability of having no armor).

[Image showing a damage flow diagram: Incoming Attack -> Guard -> AP -> HP, with a special arrow for Armor Break piercing through to HP]

---

## 6. Damage Formula

### 7.1 Calculation Flow

1. **Base Power:** Card power.
2. **Attack Modifiers:** Apply `atkUp` (+%) and `weak` (-30%).
3. **Critical Check:** Base rate 10% (max 80%). Base Multiplier 1.5x.
4. **Mitigation:** Reduce damage by Defender’s Equipment DEF%.
5. **Allocation:** Apply damage to Guard → AP → HP (considering Armor Break).
6. **Post-Process:** Calculate Lifesteal (Attacker) and Reflect/Thorns (Defender).

---

## 7. Special Status Effects

* **Bleed:** * **Player:** Takes 5% Max HP damage **every time they play a card**.
* **Enemy:** Takes 5% Max HP damage **every time they take an action**.


* **Poison/Curse:** Fixed damage applied at the end of the turn (Stacks × 2).
* **Regeneration:** Heals HP at the start of the turn. (Effect is halved if **Cursed**).

---

## 8. Ver 4.0 Major Changes Summary

1. **Removed Depth Scaling:** Enemies no longer receive automatic stat multipliers based on depth. Stats (HP, ATK, Energy) are now **hard-coded** into each unique enemy definition.
2. **Depth Role:** Depth now strictly determines the **Enemy Pool** (which enemies can appear).
3. **Speed Logic:** Replaced "Energy -1" for Slow with "Speed -10." Turn order is now dynamic based on the Phase Queue.
4. **Guard Reset:** Guard now resets to 0 at the **start** of a unit's own turn, preventing infinite stacking across turn cycles.

---