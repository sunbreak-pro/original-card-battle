# Survival System Comprehensive Design Document V3.0

## Revision History

* **V3.0: Introduction of the Lives System**
* Unified Teleport Stones (100% rewards).
* Added Abyss Escape Route.
* 100% Soul Remnant retention on death.
* Removed exploration count limits.


* **V2.0:** Changed Soul Remnants to an EXP-based system; added exploration limits.

---

# 1. System Overview

## 1.1 Core Concept

The Survival System presents a trade-off between **Risk Management** and **Resource Preservation**.

* **Design Philosophy:**
* Rewards increase with depth, but so does the risk.
* Tests the player's ability to make tactical retreats.
* Provides "Strategic Withdrawal" as an alternative to "Total Annihilation."
* Closely integrates with Equipment Durability management.
* Adds weight to death via the Lives System.



## 1.2 Survival vs. Death (Penalty Comparison)

| Item | Survival (Return) | Death |
| --- | --- | --- |
| **Equipment** | **Keep All** | **Lose All** (including items brought from base) |
| **Items** | **Keep All** | **Lose All** |
| **Durability** | Persists | - |
| **Mastery** | Recorded | Recorded |
| **Gold** | **100% Carry-back** | Zero |
| **Magic Stones** | **100% Carry-back** | Zero |
| **Soul Remnants** | **100% Added to Total** | **100% Added to Total** (V3.0 Change) |
| **Lives** | **No Change** | **-1 Life** (V3.0 Change) |

> **Nuance Note:** In English gaming terms, "Survival" often sounds like a genre. Here, we use **"Return"** or **"Extraction"** to emphasize the act of safely leaving the dungeon. **"Lose All"** is used to clearly signal the "Permadeath-lite" nature of the penalty.

---

# 2. Methods of Return

## 2.1 Return Methods Overview

1. **Teleport Stone (Instant Return):** Consumes an item. Zero risk. 100% rewards. (Cannot be used in Depth 5).
2. **Return Route (Phased Withdrawal):** Requires time and combat. High risk due to enemies. 100% rewards. (Cannot be used in Depth 5).

## 2.2 Comparison

| Method | Requirement | Immediacy | Reward Multiplier | Risk | Best Used When... |
| --- | --- | --- | --- | --- | --- |
| **Teleport Stone** | Item in hand | **Instant** | 100% | None | Prioritizing safety |
| **Return Route** | Combat-ready | Slow | 100% | Enemy encounters | Seeking extra rewards |

**Critical Rule:** All return methods are disabled in **The Abyss (Depth 5)**. Once you enter, it is "Slay the boss or die." However, an **Escape Route** appears only after the boss is defeated.

---

# 3. Teleport Stone System (Unified)

## 3.1 Specifications

* **V3.0 Change:** Simplified 3 types into **1 Unified Teleport Stone** with 100% reward retention.
* **Usage Conditions:** Cannot be used during battle. Only usable on the Map screen. Disabled in Depth 5.
* **Rarity:** Uncommon. Recommended carrying 1-2 per run.

## 3.2 Strategy: When to Use

* **Critical Durability:** Equipment < 20% (High risk of breaking in the next fight).
* **Low HP/Shield:** HP < 40% with no healing items.
* **High-Value Loot:** After finding Rare/Epic gear you cannot afford to lose.

---

# 4. Return Route System

## 4.1 Concept

Backtracking through the path you cleared. No reward penalty, but random encounters occur.

* **V3.0 Change:** Even if you die *during* the return route, Soul Remnants earned up to that point are kept 100%. However, you still lose 1 Life and all items/equipment.

## 4.2 Encounter Rates

The encounter rate drops as you get closer to the Base (Base = Safety).

* **Formula:** `Current Rate = Initial Rate - (Progress% × Reduction Coefficient)`
* **Enemy Status (Return Route):** HP/ATK are reduced to **70%**; Rewards are reduced to **50%**.

---

# 5. The Abyss (Depth 5) Special Rules

## 5.1 Design Philosophy: "No Way Back"

The Abyss is the ultimate trial. It is designed to maximize tension and force the player to commit.

* **V3.0 Addition:** The **Escape Route** appears only after the boss's death, allowing a safe, 100% reward return.

---

# 6. Reward Calculations

### 6.2.3 Death Processing (V3.0 Change)

```typescript
function handleDeath(player: Character, soulsEarnedThisRun: number): void {
  // V3.0: 100% Souls retained even on death
  player.totalSouls += soulsEarnedThisRun;

  // Penalty: Lose 1 Life
  player.lives.current--;

  // Penalty: Full Inventory/Equipment Wipe
  player.inventory = getEmptyInventory();
  player.equippedItems = {};
  player.dungeonGold = 0;
  
  if (player.lives.current <= 0) {
    triggerGameOver(player);
  }
}

```

---

# 7. Lives System & Game Over

* **Life Cap:** Hard (2), Normal/Easy (3).
* **Recovery:** None.
* **Game Over:** Death at 0 Lives results in a **Hard Reset** (losing all gold, equipment, soul progress, and sanctuary unlocks). Only Achievements persist.

---

# 8. Summary of Tactical Balance

| Method | Rewards | Risk | Cost | Nuance |
| --- | --- | --- | --- | --- |
| **Teleport** | 100% | Zero | Item | The "Safe Bet" for preserving rare loot. |
| **Return Route** | 100% + Bonus | Combat | Time | The "Greedy Play" for extra Mastery and Souls. |
| **Abyss** | 100% | Absolute | Lives | The "Ultimate Gamble." |
