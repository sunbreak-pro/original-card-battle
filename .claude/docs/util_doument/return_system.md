Here is the English translation of the design document.

# Survival System Complete Design Document V2.0

## Revision History

- V2.0: **Fundamental Design Overhaul** - Changed Soul Remnants to an experience point system, added Exploration Count Limit.

## Table of Contents

```
1. Overview of Survival System
2. Types of Survival Methods
3. Teleport Stone System
4. Return Route System
5. Special Rules for The Abyss (Depth 5)
6. Reward Calculation upon Survival
7. Relationship with Exploration Count Limit (NEW)
8. Strategic Significance and Balance
9. Implementation Specifications
10. Summary

```

---

# 1. Overview of Survival System

## 1.1 Basic Concept

```
The Survival System offers a trade-off between "Risk Management" and "Resource Preservation".

[Design Philosophy]
- Deeper levels yield greater rewards but increased risks.
- Tests the ability to judge when to retreat appropriately.
- Offers "Strategic Retreat" as an option instead of total death.
- Closely integrated with Equipment Durability management.
- Adds weight to each run through the Exploration Count Limit.

```

## 1.2 Difference between Survival and Death

| Item              | Survival                               | Death                                  |
| ----------------- | -------------------------------------- | -------------------------------------- |
| Equipment         | **All Retained**                       | All Lost                               |
| Durability        | **As Is**                              | -                                      |
| Mastery           | **Usage Count Recorded**               | Usage Count Recorded                   |
| Gold              | **Full Amount (Reduced by method)**    | Zero                                   |
| Magic Stones      | **Reduced by method**                  | Zero                                   |
| Soul Remnants     | **Added to Total (Reduced by method)** | **Zero for this run (Total retained)** |
| Exploration Log   | Recorded                               | Recorded                               |
| Exploration Count | **+1**                                 | **+1**                                 |

**Value of Survival:**

- Reliably keep equipment.
- Mastery usage counts are saved.
- Bring back Gold, Magic Stones, and Souls.
- Effective use of Exploration Count.

**Penalty of Death:**

- Complete loss of equipment.
- All collected Gold, Magic Stones, and resources are lost.
- **Soul Remnants gained in that run are also zeroed.**
- However, **accumulated Soul Remnants (Total) from the past are retained.**
- **Exploration Count is consumed.**

**Important Changes (V2.0):**

```
Old Design:
On Death → Gain Soul Remnants "Normally" (Roguelite element)

New Design:
On Enemy Kill → Gain Soul Remnants (Like EXP)
Survival → Add to Total
Death → Zero for that run (Total is retained)

```

---

# 2. Types of Survival Methods

## 2.1 List of Survival Methods

```
[Two Methods of Survival]

1. Teleport Stone (Immediate Return)
   - Cost: Consumes Item
   - Risk: None
   - Reward: Reduced
   - Restriction: Unusable in Depth 5 (The Abyss)

2. Return Route (Gradual Retreat)
   - Cost: Time and Combat
   - Risk: Enemies along the way
   - Reward: No Reduction
   - Restriction: Unusable in Depth 5 (The Abyss)

```

## 2.2 Comparison of Methods

| Method             | Condition    | Immediacy     | Reward Multiplier | Risk | Recommended Scenario |
| ------------------ | ------------ | ------------- | ----------------- | ---- | -------------------- |
| Normal Teleport    | Has Item     | **Immediate** | 70%               | None | Early Retreat        |
| Blessed Teleport   | Has Item     | **Immediate** | 80%               | None | Securing Rewards     |
| Emergency Teleport | Has Item<br> |

<br>Can use in battle | **Immediate** | 60% | None | Escape during Combat |
| Return Route | Can Fight | Slow | **100%** | Combat | Max Rewards |

**Important:** In Depth 5 (The Abyss), all survival methods are disabled.
Once you enter The Abyss, the only choices are "Defeat the Boss or Die".

---

# 3. Teleport Stone System

## 3.1 Types of Teleport Stones

### 3.1.1 Normal Teleport Stone

```
[Basic Specs]
Name: Teleport Stone of Return
Effect: Immediately return to base / Reward Multiplier 70%
Acquisition: Shop / Event / Chest

[Usage Conditions]
- Cannot use during combat
- Usable only on map screen
- Usable only in Depth 1-4 (Invalid in Depth 5)

[Features]
Rarity: Common
Recommended amount per run: 1-2

```

### 3.1.2 Blessed Teleport Stone

```
[Basic Specs]
Name: Teleport Stone of Blessing
Effect: Return + Reward Multiplier 80% (Higher than normal)
Acquisition: Event / Boss Reward / Hidden Room

[Usage Conditions]
- Cannot use during combat
- Usable only on map screen
- Usable only in Depth 1-4 (Invalid in Depth 5)

[Features]
Rarity: Rare
High reward retention, bring back more resources.

```

### 3.1.3 Emergency Teleport Stone

```
[Basic Specs]
Name: Emergency Teleport Stone
Effect: Usable even during combat / Reward Multiplier 60%
Acquisition: Shop / Special Event

[Usage Conditions]
- Usable anytime
- Activates even during combat
- Usable only in Depth 1-4 (Invalid in Depth 5)

[Features]
Rarity: Epic
Strategic Value: Last resort insurance during combat.
Low rewards, but protects life and equipment.

```

---

## 3.2 Process Flow for Teleport Stone Usage

```typescript
/**
 * Teleport Stone Usage Process
 */
interface TeleportStone {
  type: "normal" | "blessed" | "emergency";
  rewardMultiplier: number; // 0.7 / 0.8 / 0.6
  canUseInBattle: boolean;
}

function useTeleportStone(
  player: Character,
  stone: TeleportStone,
  currentDepth: number,
  defeatedEnemies: number,
  soulsEarnedThisRun: number // NEW: Souls earned in this run
): ReturnResult {
  // Check Depth 5 (Abyss)
  if (currentDepth === 5) {
    return {
      success: false,
      message: "Teleport stones are disabled in the Abyss...",
    };
  }

  // Check Combat Status
  if (isInBattle() && !stone.canUseInBattle) {
    return {
      success: false,
      message: "Cannot be used during combat.",
    };
  }

  // Calculate Rewards
  const baseReward = calculateBaseReward(currentDepth, defeatedEnemies);
  const finalReward = {
    gold: Math.floor(baseReward.gold * stone.rewardMultiplier),
    stones: Math.floor(baseReward.stones * stone.rewardMultiplier),
    souls: Math.floor(soulsEarnedThisRun * stone.rewardMultiplier), // NEW: Apply multiplier to earned souls
  };

  // Equipment/Mastery are fully retained
  savePlayerState(player);

  // Add Soul Remnants to Total (NEW)
  addToTotalSouls(finalReward.souls);

  // Return to Base
  return {
    success: true,
    type: "teleport",
    rewards: finalReward,
    message: "Returned to base.",
  };
}
```

---

## 3.3 Strategic Timing for Teleport Stone

```
[Recommended Usage Timing]

1. Equipment Durability Critical
   - All equipment durability < 20%
   → Risk of total breakage in next battle.

2. Low HP/Shield
   - HP < 40% and no recovery means.
   → High risk of death in next battle.

3. Acquired Powerful Equipment
   - Obtained Rare/Epic gear.
   → Avoid loss due to death.

4. Sufficient Gold/Stones/Souls
   - Resource goals met.
   → No need for further risk.

5. Unprepared for Boss
   - Lack of consumables.
   → Restock at base and retry.

6. Low Remaining Exploration Count (NEW)
   - Remaining 2-3 runs, deep progression required.
   → Securely bring back souls.

```

---

# 4. Return Route System

## 4.1 Basic Specs

```
[Concept]
Run back the way you came to return to base.
No reward reduction, but encounters occur along the way.

[Pros]
- 100% Rewards obtained.
- Additional combat experience.
- Farm more mastery.
- Can earn additional Soul Remnants (NEW).

[Cons]
- Takes time.
- Risk of combat along the way.
- Further equipment durability consumption.
- If you die, ALL souls become zero (NEW).

```

## 4.2 Enemies on Return Route

### 4.2.1 Enemy Encounter Rules

```
[Variable Encounter Rate System]
Encounter rate decreases as you progress back.
→ Becomes safer closer to base.

[Initial Encounter Rates by Depth]
Upper Layer (Depth 1): Start 70% → End 20%
Middle Layer (Depth 2): Start 75% → End 25%
Lower Layer (Depth 3): Start 80% → End 30%
Deep Layer (Depth 4): Start 85% → End 35%
The Abyss (Depth 5): Return impossible.

[Encounter Rate Formula]
Current Rate = Initial Rate - (Progress% × Decrease Coeff)

Decrease Coeff:
- Depth 1: 0.5 (70% → 20% is 50% reduction)
- Depth 2: 0.5
- Depth 3: 0.5
- Depth 4: 0.5

Example: 50% progress in Depth 3
Current Rate = 80% - (50% × 0.5) = 55%

```

### 4.2.2 Features of Return Combat

```
[Weakened Enemies]
HP: 70%
Attack: 70%
Reward Gold: 50%
Reward Stones: 50%
Reward Souls: 50% (NEW)
Skill Learning: None

[Special Rules]
- Elite enemies do not respawn.
- Bosses do not respawn.
- Same enemy types as defeated appear.
- Encounter check performed per room.

```

---

## 4.3 Process when Selecting Return Route

```typescript
/**
 * Start Return Route Process
 */
interface ReturnRoute {
  totalRooms: number;
  currentProgress: number; // Progress 0-100%
  currentEncounterRate: number; // Current Encounter Rate %
  passedRooms: number; // Rooms passed
  encountersCount: number; // Number of encounters
  additionalSoulsEarned: number; // NEW: Additional souls earned during return
}

/**
 * Encounter Rate Config by Depth
 */
interface EncounterRateConfig {
  initialRate: number;
  finalRate: number;
  decreaseCoef: number;
}

const ENCOUNTER_RATES: Record<number, EncounterRateConfig> = {
  1: { initialRate: 70, finalRate: 20, decreaseCoef: 0.5 },
  2: { initialRate: 75, finalRate: 25, decreaseCoef: 0.5 },
  3: { initialRate: 80, finalRate: 30, decreaseCoef: 0.5 },
  4: { initialRate: 85, finalRate: 35, decreaseCoef: 0.5 },
};

/**
 * Start Return Route
 */
function startReturnRoute(
  currentDepth: number,
  roomsPassed: number
): ReturnRoute | null {
  // Check Depth 5 (Abyss)
  if (currentDepth === 5) {
    return null; // Return impossible
  }

  const config = ENCOUNTER_RATES[currentDepth];

  return {
    totalRooms: roomsPassed,
    currentProgress: 0,
    currentEncounterRate: config.initialRate,
    passedRooms: 0,
    encountersCount: 0,
    additionalSoulsEarned: 0, // NEW
  };
}

/**
 * Calculate Current Encounter Rate
 */
function calculateCurrentEncounterRate(
  depth: number,
  progressPercent: number
): number {
  const config = ENCOUNTER_RATES[depth];

  // Current Rate = Initial - (Progress% * Coeff)
  const reduction = progressPercent * config.decreaseCoef;
  const currentRate = config.initialRate - reduction;

  // Does not go below Final Rate
  return Math.max(currentRate, config.finalRate);
}

/**
 * Encounter Check per Room
 */
function checkEncounter(depth: number, progressPercent: number): boolean {
  const encounterRate = calculateCurrentEncounterRate(depth, progressPercent);
  const roll = Math.random() * 100;

  return roll < encounterRate;
}

/**
 * Advance Return Route Process
 */
function advanceReturnRoute(
  route: ReturnRoute,
  depth: number
): {
  route: ReturnRoute;
  encounterOccurred: boolean;
} {
  route.passedRooms++;
  route.currentProgress = (route.passedRooms / route.totalRooms) * 100;
  route.currentEncounterRate = calculateCurrentEncounterRate(
    depth,
    route.currentProgress
  );

  // Encounter Check
  const encounterOccurred = checkEncounter(depth, route.currentProgress);

  if (encounterOccurred) {
    route.encountersCount++;
  }

  return { route, encounterOccurred };
}

/**
 * Generate Return Combat Enemy
 */
function generateReturnEnemy(originalEnemy: Character): Character {
  return {
    ...originalEnemy,
    hp: Math.floor(originalEnemy.hp * 0.7),
    maxHp: Math.floor(originalEnemy.maxHp * 0.7),
    attack: Math.floor(originalEnemy.attack * 0.7),
    rewardGold: Math.floor(originalEnemy.rewardGold * 0.5),
    rewardStones: Math.floor(originalEnemy.rewardStones * 0.5),
    rewardSouls: Math.floor(originalEnemy.rewardSouls * 0.5), // NEW
    canLearnSkill: false, // No skill learning
  };
}

/**
 * Calculate Rewards on Return Completion
 */
function completeReturnRoute(
  player: Character,
  currentDepth: number,
  defeatedEnemies: number,
  soulsEarnedThisRun: number, // NEW: Souls from this run
  additionalSoulsFromReturn: number // NEW: Additional souls from return
): ReturnResult {
  // 100% Rewards
  const fullReward = calculateBaseReward(currentDepth, defeatedEnemies);

  // Total Souls (NEW)
  const totalSouls = soulsEarnedThisRun + additionalSoulsFromReturn;

  // Keep Equipment/Mastery
  savePlayerState(player);

  // Add to Total Souls (NEW)
  addToTotalSouls(totalSouls);

  return {
    success: true,
    type: "return_route",
    rewards: {
      ...fullReward,
      souls: totalSouls, // Run + Return Combat
    },
    message: "Safely returned to base.",
  };
}
```

---

## 4.4 Return Route UI Display

```
[Map Screen Display]

┌─────────────────────────┐
│  Select Return Route    │
├─────────────────────────┤
│                         │
│ Location: Lower 8th Rm  │
│ To Base: 15 Rooms       │
│                         │
│ Current Enc. Rate: 80%  │
│ Final Enc. Rate: 30%    │
│                         │
│ ▼ Rate Trend            │
│ Start ████████ 80%      │
│ Mid   █████░░░ 55%      │
│ End   ███░░░░░ 30%      │
│                         │
│ Reward Mult: 100%       │
│ Keep Equip: Yes         │
│ Add. Souls: Available   │  ← NEW
│                         │
│ * Cannot return from    │
│   The Abyss (Depth 5)   │
│                         │
│ [Start Return] [Cancel] │
│                         │
└─────────────────────────┘

```

---

# 5. Special Rules for The Abyss (Depth 5)

## 5.1 Design Philosophy of No Survival

```
[Concept]
The Abyss is the Final Trial.
→ Only two choices: "Defeat the Boss or Die".

[Intent]
- Provide maximum tension.
- Test true resolve.
- Maximize reward risk.
- Synergy with Exploration Count Limit (NEW).

```

## 5.2 Restrictions in The Abyss

```
[Prohibited]
✗ Return Route unavailable
✗ All Teleport Stones disabled
✗ Mid-way retreat impossible

[Warning Before Entering Abyss]
"Entering the Abyss will forfeit all means of survival.
 Only two choices remain: Defeat the Boss or Die.

 Exploration Count: X Remaining

 Do you really want to proceed?"

[Yes - I am resolved] [No - Not yet]

```

## 5.3 Process Upon Entering Abyss

```typescript
/**
 * Confirm Enter Abyss
 */
function confirmEnterAbyss(remainingExplorations: number): boolean {
  const warning = `
    ⚠️ WARNING ⚠️
    
    Entering the Abyss will forfeit all means of survival.
    
    - Return Route: Unavailable
    - Teleport Stones (All): Disabled
    - Mid-way Retreat: Impossible
    
    Only two choices remain: Defeat the Boss or Die.
    
    Exploration Count: ${remainingExplorations} Remaining
    
    Do you really want to proceed?
  `;

  return confirm(warning);
}

/**
 * Check Teleport/Return Availability
 */
function canReturn(currentDepth: number): {
  canUseTeleport: boolean;
  canUseReturnRoute: boolean;
  reason?: string;
} {
  if (currentDepth === 5) {
    return {
      canUseTeleport: false,
      canUseReturnRoute: false,
      reason: "All survival methods are disabled in the Abyss.",
    };
  }

  return {
    canUseTeleport: true,
    canUseReturnRoute: true,
  };
}

/**
 * Attempt Teleport in Abyss
 */
function attemptTeleportInAbyss(): void {
  showMessage(`
    The Teleport Stone shattered...
    The magic of the Abyss nullifies teleportation.
    There is no way back now.
  `);

  // Stone is not consumed (since use failed)
}
```

## 5.4 Game Design Significance of The Abyss

```
[Strategic Significance]
1. Extreme Risk/Reward
   - Highest rewards but no escape.
   - Complete preparation required.

2. Trial of Player Skill
   - Perfect equipment management.
   - Mastery of combat techniques.
   - Precise resource allocation.

3. Gradual Challenge
   - Beginner: Retreat by Depth 3.
   - Intermediate: Use Teleport at Depth 4.
   - Advanced: Enter The Abyss.

4. Maximize Sense of Accomplishment
   - Clearing the Abyss is true glory.
   - Titles / Special Rewards.

5. Integration with Exploration Count (NEW)
   - Entering Abyss with few runs left is a gamble.
   - Failure means no next chance.
   - Requires careful judgment.

```

---

# 6. Reward Calculation upon Survival

## 6.1 Base Reward Formula

```typescript
/**
 * Base Reward Calculation
 */
interface BaseReward {
  gold: number;
  stones: MagicStones;
}

interface MagicStones {
  tiny: number;
  small: number;
  medium: number;
  large: number;
  huge: number;
}

function calculateBaseReward(
  currentDepth: number,
  defeatedEnemies: number
): BaseReward {
  // Depth Bonus
  const depthMultiplier: Record<number, number> = {
    1: 1.0,
    2: 1.5,
    3: 2.5,
    4: 4.0,
    5: 7.0,
  };

  const depthBonus = depthMultiplier[currentDepth] || 1.0;

  // Gold Calculation
  const gold = Math.floor(currentDepth * 50 + defeatedEnemies * 10);

  // Stone Calculation (Types change by depth)
  const stones = calculateStoneRewards(currentDepth, defeatedEnemies);

  return { gold, stones };
}

function calculateStoneRewards(depth: number, enemies: number): MagicStones {
  const base = Math.floor(enemies / 3);

  const distribution: Record<number, MagicStones> = {
    1: { tiny: base * 4, small: 0, medium: 0, large: 0, huge: 0 },
    2: { tiny: base, small: base * 3, medium: 0, large: 0, huge: 0 },
    3: { tiny: base, small: base, medium: base * 2, large: 0, huge: 0 },
    4: { tiny: 0, small: base, medium: base * 2, large: base, huge: 0 },
    5: { tiny: 0, small: 0, medium: base, large: base * 2, huge: 1 },
  };

  return distribution[depth] || distribution[1];
}
```

---

## 6.2 Soul Remnants Calculation (V2.0 - Major Change)

**Important Changes:**

```
Old Design:
- Gain Souls on Death.
- Reduced on Survival by method.

New Design:
- Gain Souls on Enemy Kill (Like EXP).
- Add to Total on Survival (Reduced by method).
- Zero for that run on Death (Total is retained).

```

### 6.2.1 Soul Acquisition Timing

```typescript
/**
 * Gain Souls on Enemy Kill
 */
interface SoulGainSystem {
  // Souls added during combat "Souls earned this run"
  currentRunSouls: number;

  // "Total Souls" accumulated from past runs
  totalSouls: number;
}

/**
 * Soul Gain on Enemy Defeat
 */
function defeatEnemy(enemy: Character): number {
  // Soul amount differs by enemy type
  const soulsByType: Record<EnemyType, number> = {
    minion: 5,
    elite: 15,
    boss: 50,
  };

  const soulsGained = soulsByType[enemy.type] || 5;

  // Add to "Souls earned this run"
  currentRunSouls += soulsGained;

  return soulsGained;
}

/**
 * Soul Gain in Return Combat (50%)
 */
function defeatReturnEnemy(enemy: Character): number {
  const baseSouls = enemy.rewardSouls;
  const reducedSouls = Math.floor(baseSouls * 0.5);

  // Add to "Souls earned this run"
  currentRunSouls += reducedSouls;

  return reducedSouls;
}
```

### 6.2.2 Soul Processing on Survival

```typescript
/**
 * Calculate Soul Remnants on Survival
 */
function calculateSoulReward(
  soulsEarnedThisRun: number,
  returnMethod: ReturnMethod
): number {
  const multipliers: Record<ReturnMethod, number> = {
    return_route: 1.0, // 100%
    teleport_normal: 0.7, // 70%
    teleport_blessed: 0.8, // 80%
    teleport_emergency: 0.6, // 60%
  };

  const multiplier = multipliers[returnMethod];
  const finalSouls = Math.floor(soulsEarnedThisRun * multiplier);

  return finalSouls;
}

/**
 * Survival Completion Process
 */
function completeReturn(
  player: Character,
  soulsEarnedThisRun: number,
  returnMethod: ReturnMethod
): void {
  // Soul reduction by method
  const finalSouls = calculateSoulReward(soulsEarnedThisRun, returnMethod);

  // Add to Total Souls
  player.totalSouls += finalSouls;

  // Reset souls for this run
  currentRunSouls = 0;

  // Update available skill points in Sanctuary
  updateAvailableSkillPoints(player.totalSouls);
}
```

### 6.2.3 Soul Processing on Death

```typescript
/**
 * Death Processing
 */
function handleDeath(player: Character, soulsEarnedThisRun: number): void {
  // Souls earned this run become zero
  currentRunSouls = 0;

  // However, Total Souls are retained
  // player.totalSouls remains as is

  // Equipment, Gold, Magic Stones are all lost
  player.equipment = [];
  player.gold = 0;
  player.magicStones = { tiny: 0, small: 0, medium: 0, large: 0, huge: 0 };

  // Equipment, Cards, Past Total Souls stored at BaseCamp are retained
}
```

---

## 6.3 Reward Multiplier by Survival Method

```
[Final Reward Formula]

Final Reward = Base Reward × Survival Method Multiplier

[Survival Method Multipliers]
Return Route: 1.0 (100%)
Normal Teleport: 0.7 (70%)
Blessed Teleport: 0.8 (80%)
Emergency Teleport: 0.6 (60%)

[Soul Remnants Calculation (V2.0)]
On Survival:
- Return Route: Souls this run × 1.0 → Add to Total
- Normal Teleport: Souls this run × 0.7 → Add to Total
- Blessed Teleport: Souls this run × 0.8 → Add to Total
- Emergency Teleport: Souls this run × 0.6 → Add to Total

On Death:
- Souls this run → Zero
- Total Souls → Retained (No change)

Calculation Example:
Souls earned this run: 100
Past Total Souls: 500

[If Survived]
Return Route: Add 100 → Total 600
Normal Teleport: Add 70 → Total 570
Blessed Teleport: Add 80 → Total 580
Emergency Teleport: Add 60 → Total 560

[If Died]
This run: 0
Total: 500 (No change)

```

---

# 7. Relationship with Exploration Count Limit (NEW)

## 7.1 Basic Rules of Exploration Count Limit

```
[Limit Mechanism]
- Exploration Limit: 10 runs (Default)
- Regardless of survival or death, 1 run = Count +1
- Defeat Depth 5 Boss within 10 runs → Success
- Use up 10 runs → Game Over

[Count for 1 Run]
Enter Dungeon → Survive or Die → Exploration Count +1

```

## 7.2 Relationship between Survival System and Exploration Count

```
[Increased Strategic Value of Survival]

When remaining explorations are low:
- Risk of death is extremely high.
- Need to reliably bring back Souls.
- Value of Teleport Stones increases.

Example: 2 remaining runs
Option A: Go deep for massive Souls
→ If you die, 0 Souls, hard to reach deep with 1 run left.

Option B: Reliably survive at mid-layer, accumulate Souls
→ Upgrade at Sanctuary, challenge deep layer with 1 run left.

```

## 7.3 Choosing Survival Method and Exploration Count

```
[Recommended Strategy by Remaining Count]

Remaining 7-10:
- Return Route recommended (Max rewards)
- Can recover even if you die
- Aim for additional Souls

Remaining 4-6:
- Judgement call
- Use Teleport if deep
- Return Route if shallow

Remaining 1-3:
- Teleport Stone mandatory (Blessed or Emergency)
- Must avoid death
- Reliably bring back Souls
- Final upgrades at Sanctuary

```

## 7.4 Judgment to Enter Abyss

```
[Conditions to Enter Abyss]

Recommended:
- Exploration Count: 2+ remaining
- Equipment: Max Level (Lv3)
- Sanctuary: Key skills unlocked
- Consumables: Sufficient stock

Abyss with 1 run left:
- Final challenge
- Failure = Game Over
- Success = Full Clear

Warning Display:
"Entering the Abyss.
 Exploration Count: 1 Remaining

 Failure means no next chance.
 Do you really want to proceed?"

```

---

# 8. Strategic Significance and Balance

## 8.1 Usage of Each Method

```
[Return Route]
Recommended:
- Equipment Durability > 40%
- HP > 60%
- Consumables available
- Exploring Depth 1-3
- Plenty of Exploration Count (5+ left)
→ Aim for Max Rewards

[Normal Teleport (70%)]
Recommended:
- Equipment Durability 20-40%
- HP 40-60%
- Acquired Rare Equipment
- Want to save time
- Mid-game Exploration Count (4-6 left)
→ Safe resource securing

[Blessed Teleport (80%)]
Recommended:
- Possess High Value Equip/Stones
- Want to bring back more rewards
- After exploring Depth 3-4
- Low Exploration Count (2-3 left)
→ Balanced type

[Emergency Teleport (60%)]
Recommended:
- HP Critical during battle
- Disadvantageous Boss fight
- Need immediate escape
- 1 Exploration Count left
→ Last resort escape

[Strategy in Abyss]
Before entering Depth 5:
- Complete preparations
- All Teleport Stones disabled
- Check Exploration Count
- Enter only with resolve
→ Victory or Death

```

---

## 8.2 Risk/Reward Balance

```
[Trade-off]

Return Route:
- Reward: 100% (Max)
- Risk: 3-7 battles en route
- Additional Profit: Mastery +15-25 uses, Additional Souls
- Exploration Count Value: Maximized
→ Take time and risk for max reward

Normal Teleport:
- Reward: 70%
- Risk: None
- Loss: 30% Gold/Stones/Souls
- Exploration Count Value: Moderate
→ Safety first, time saving

Blessed Teleport:
- Reward: 80%
- Risk: None
- Loss: 20% Gold/Stones/Souls
- Exploration Count Value: High
→ Balanced, relatively high retention

Emergency Teleport:
- Reward: 60%
- Risk: None
- Loss: 40% Gold/Stones/Souls
- Exploration Count Value: Protects life/gear
→ Emergency insurance, significant loss

[Strategy by Depth]

Depth 1-2:
- Return Route recommended
- 70-75% encounter rate but enemies are weak
- Chance for Mastery/Soul farming

Depth 3:
- Judgment call
- Good Equip + Count to spare → Return Route
- High wear or Low Count → Blessed Teleport

Depth 4:
- Teleport recommended
- 85% encounter rate is high risk
- Potential Elite enemies
- Mandatory if Count is low

Depth 5 (Abyss):
- No survival means
- Consider retreating at Depth 4 beforehand
- Check Exploration Count
- Entry requires resolve

```

---

## 8.3 Risk Management Learning Curve

```
[Early Game (1-5 Runs)]
Goal: Understanding retreat timing
→ Use Teleport for safe return
→ Grasp the feel of Exploration Count

[Mid Game (10-20 Runs)]
Goal: Utilizing Return Route
→ Defeat weakened enemies and retreat
→ Maximize Soul acquisition

[Late Game (30+ Runs)]
Goal: Push to the limit
→ Carry only Emergency Teleport
→ High efficiency runs in Deep/Abyss
→ Perfect management of Exploration Count

```

---

# 9. Implementation Specifications

## 9.1 Data Structure

```typescript
/**
 * Survival System Data Types
 */
enum ReturnMethod {
  RETURN_ROUTE = "return_route",
  TELEPORT_NORMAL = "teleport_normal",
  TELEPORT_BLESSED = "teleport_blessed",
  TELEPORT_EMERGENCY = "teleport_emergency",
}

interface ReturnResult {
  success: boolean;
  type: ReturnMethod;
  rewards: {
    gold: number;
    stones: MagicStones;
    souls: number; // Souls earned this run and added to total
  };
  message: string;
  penalties?: {
    goldLoss: number;
    stoneLoss: number;
    soulLoss: number; // Soul reduction by method
  };
}

interface ReturnOption {
  method: ReturnMethod;
  available: boolean;
  rewardMultiplier: number;
  risk: "none" | "low" | "medium" | "high" | "dynamic";
  currentEncounterRate?: number; // For return route
  finalEncounterRate?: number; // For return route
  reason?: string; // Reason for unavailability
}

/**
 * Abyss Check
 */
interface AbyssRestriction {
  canUseTeleport: boolean;
  canUseReturnRoute: boolean;
  reason?: string;
}

/**
 * Soul Remnants System (V2.0)
 */
interface SoulSystem {
  currentRunSouls: number; // Souls earned this run
  totalSouls: number; // Total accumulated souls
}

/**
 * Exploration Count Limit (NEW)
 */
interface ExplorationLimit {
  maxExplorations: number; // Max counts (Default 10)
  currentExplorations: number; // Current counts
  remainingExplorations: number; // Remaining counts
}
```

---

## 9.2 UI Design Requirements

```
[Survival Menu Screen]

┌─────────────────────────────┐
│  Select Survival Method     │
├─────────────────────────────┤
│                             │
│ Location: Lower (Depth 3)   │
│ Exploration: 7/10 (3 left)  │  ← NEW
│                             │
│ Souls earned this run: 85   │  ← NEW
│                             │
│ 1. Return Route             │
│    Reward: 100%             │
│    Souls: 85 → Add to Total │  ← NEW
│    Curr Enc Rate: 80%       │
│    Final Enc Rate: 30%      │
│    Risk: Medium             │
│    [Select]                 │
│                             │
│ 2. Teleport (Normal)        │
│    Reward: 70%              │
│    Souls: 59 → Add to Total │  ← NEW (85 × 0.7)
│    Risk: None               │
│    Held: 2                  │
│    [Use]                    │
│                             │
│ 3. Teleport (Blessed)       │
│    Reward: 80%              │
│    Souls: 68 → Add to Total │  ← NEW (85 × 0.8)
│    Risk: None               │
│    Held: 1                  │
│    [Use]                    │
│                             │
│ 4. Teleport (Emergency)     │
│    Reward: 60%              │
│    Souls: 51 → Add to Total │  ← NEW (85 × 0.6)
│    Usable in Combat         │
│    Held: 0                  │
│    [None Held]              │
│                             │
│ ⚠️ In Abyss (Depth 5)       │
│   All methods disabled      │
│                             │
│ ⚠️ Upon Death, the 85 Souls │  ← NEW
│   will become zero          │  ← NEW
│                             │
│ [Cancel]                    │
│                             │
└─────────────────────────────┘

[Abyss Entry Warning Screen]

┌─────────────────────────────┐
│  ⚠️ Entering The Abyss ⚠️   │
├─────────────────────────────┤
│                             │
│ Ahead lies The Abyss.       │
│                             │
│ In The Abyss:               │
│                             │
│ ✗ Return Route Unavailable  │
│ ✗ All Teleport Stones Void  │
│ ✗ No Retreat                │
│                             │
│ Defeat the Boss or Die.     │
│ Only two choices.           │
│                             │
│ Exploration: 8/10 (2 left)  │  ← NEW
│                             │
│ Souls earned this run: 120  │  ← NEW
│ * Zeroed upon death         │  ← NEW
│                             │
│ Do you really want to proceed?│
│                             │
│ [I am resolved]             │
│ [Not yet]                   │
│                             │
└─────────────────────────────┘

```

---

## 9.3 Confirmation Dialogs

```
[Teleport Stone Confirmation]

┌─────────────────────────────┐
│  Confirm                    │
├─────────────────────────────┤
│                             │
│ Use Normal Teleport Stone?  │
│                             │
│ Current Reward:             │
│ - Gold: 500 → 350 (-150)    │
│ - Stones: 15 → 10 (-5)      │
│ - Souls: 85 → 59 (-26)      │  ← NEW
│                             │
│ Reward Mult: 70%            │
│ Equip/Mastery: All Kept     │
│                             │
│ Exploration: 7 → 8 (+1)     │  ← NEW
│ Remaining: 3 → 2            │  ← NEW
│                             │
│ [Use] [Cancel]              │
│                             │
└─────────────────────────────┘

[Return Route Confirmation]

┌─────────────────────────────┐
│  Confirm                    │
├─────────────────────────────┤
│                             │
│ Retreat via Return Route?   │
│                             │
│ To Base: 15 Rooms           │
│ Curr Enc Rate: 80%          │
│ Final Enc Rate: 30%         │
│                             │
│ Reward: 100% (No reduction) │
│ Est. Combat: 8-10           │
│                             │
│ Equip/Mastery: All Kept     │
│ Add. Mastery: ~20 uses      │
│ Add. Souls: ~40             │  ← NEW
│                             │
│ Exploration: 7 → 8 (+1)     │  ← NEW
│ Remaining: 3 → 2            │  ← NEW
│                             │
│ [Start] [Cancel]            │
│                             │
└─────────────────────────────┘

```

---

## 9.4 Return Route Progress UI

```
[Return Route Screen]

┌─────────────────────────────┐
│  Returning to Base...       │
├─────────────────────────────┤
│                             │
│ Progress: ████████░░░ 72%   │
│                             │
│ Rooms Left: 4 / 15          │
│ Encounters: 6               │
│                             │
│ Location: Mid 3rd Room      │
│                             │
│ Next Room Enc Rate: 39%     │
│ ▼▼▼░░░░░░░ Low Risk         │
│                             │
│ Souls earned this run: 105  │  ← NEW
│ (Return Combat: +20)        │  ← NEW
│                             │
│ [To Next Room]              │
│                             │
└─────────────────────────────┘

[Encounter Occurred]

┌─────────────────────────────┐
│  Enemy Encountered!         │
├─────────────────────────────┤
│                             │
│ Corrupt Wolf (Weakened)     │
│ HP: 21 / 30 (70%)           │
│ Atk: 5 (Normal: 7)          │
│ Souls: 2 (Normal: 5)        │  ← NEW
│                             │
│ * Return Combat             │
│   Enemy strength 70%        │
│   Reward 50%                │
│                             │
│ [Start Battle]              │
│                             │
└─────────────────────────────┘

```

---

## 9.5 Implementation Checklist

```
□ Teleport Stone Item Implementation
  □ Normal (70%)
  □ Blessed (80%)
  □ Emergency (60% / In-combat)

□ Depth Check System
  □ Disable Teleport at Depth 5
  □ Disable Return Route at Depth 5
  □ Warning before entering Abyss

□ Return Route System
  □ Dynamic Encounter Rate Calculation
  □ Rate reduction by progress
  □ Encounter check per room
  □ Enemy weakening logic

□ Reward Calculation System
  □ Base reward calc (Gold/Stones)
  □ Multiplier application (70% / 80% / 60% / 100%)
  □ Soul Remnants calc (V2.0)
    □ Gain on Enemy Kill
    □ Add to Total on Survival
    □ Zero on Death

□ Exploration Count Limit System (NEW)
  □ Count exploration
  □ Display remaining count
  □ Game Over condition
  □ UI Integration

□ UI Implementation
  □ Survival Menu
  □ Abyss Warning
  □ Teleport Confirmation Dialog
  □ Return Route Confirmation Dialog
  □ Return Route Progress Screen
  □ Visualization of Encounter Rate
  □ Visualization of Reward Reduction
  □ Display Exploration Count (NEW)
  □ Display Soul Gain Status (NEW)

□ Integration Testing
  □ Verify each survival method
  □ Verify Depth 5 restrictions
  □ Accuracy of reward calc
  □ Verify Equipment/Mastery retention
  □ Accuracy of encounter rate calc
  □ Soul Remnants System operation (V2.0)
  □ Exploration Count Limit operation (NEW)
  □ Edge case handling

```

---

# 10. Summary

## 10.1 Key Design Points (V2.0)

```
1. Two-Tier Survival Methods
   - Return Route (Variable Risk/Max Reward)
   - 3 Teleport Stones (Safe/Reduced Reward)

2. Absolute Constraints of The Abyss
   - All survival means disabled in Depth 5.
   - Tension of "Defeat the Boss or Die".
   - Preparation and resolve required.

3. Dynamic Encounter Rate System
   - Safer as you progress back.
   - Higher initial rate at deeper depths.
   - Visualize progress for player.

4. Reward Trade-off
   - Safety vs Reward Balance.
   - Choices based on stone type.

5. Integration with Equipment Durability
   - Durability directly affects survival decision.
   - Strategic depth.
   - Importance of resource management.

6. Soul Remnants System (V2.0 - Major Change)
   - Gain on Kill (EXP style).
   - Add to Total on Survival (Reduced by method).
   - Zero for run on Death (Total retained).
   - Removed Roguelite element.

7. Integration with Exploration Count Limit (NEW)
   - Counted regardless of survival/death.
   - Value of survival skyrockets when count is low.
   - Affects decision to enter Abyss.
   - Adds weight to each run.

```

## 10.2 Player Experience Design

```
[Beginner (1-10 Runs)]
- Practice Return Route in Depth 1-2.
- Use Teleport Stones as safety net.
- Understand Encounter Rate system.
- Understand Soul acquisition (NEW).
- Grasp Exploration Count (NEW).

[Intermediate (11-30 Runs)]
- Explore up to Depth 3-4.
- Choose survival based on situation.
- Balance with Equipment Durability.
- Soul accumulation strategy (NEW).
- Efficient Exploration Count management (NEW).

[Advanced (31+ Runs)]
- Challenge The Abyss.
- Barely scraping by without Stones.
- Perfect resource management.
- Maximize Souls (NEW).
- Perfect Exploration Count management (NEW).

[Positioning of The Abyss]
- Final Trial.
- Place where true resolve is tested.
- Highest Rewards and Risks.
- Proof of Player Skill.
- Synergy with Exploration Count Limit (NEW).

```

## 10.3 Integration with Other Systems

```
[Equipment System]
- Durability directly links to survival decision.
- Strategic use of repair items.

[Mastery System]
- Additional EXP in Return Route.
- Opportunity cost of using Teleport Stones.
- Risk vs Growth balance.

[Reward System]
- Depth Bonus.
- Multiplier by survival method.
- Soul Remnants calculation (V2.0).

[Combat System]
- Shield carry-over affects return.
- Equipment breakage risk management.
- Importance of HP management.

[Sanctuary System (NEW)]
- Permanent upgrades with Soul Remnants.
- Unlock Exploration Count +1 skill.
- Further enhances value of survival.

[Exploration Count Limit System (NEW)]
- Strategic importance of Survival/Death.
- Weight of each run.
- Tension of the entire game.

```
