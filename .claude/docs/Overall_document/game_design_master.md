# Original Card Battle RPG Overall Design Document V3.1

## Revision History

| Date | Content |
| --- | --- |
| 2026-02-04 | V3.1: Facility consolidation (7 → 5). Library → Journal (header UI). Storage → Guild (tab). |
| - | V3.0: Introduction of Life System - Exploration limits changed to Lives, Unified Teleportation Stones, 100% Soul retention on death. |
| - | V2.0: Fundamental design change - Removed roguelite elements, pivoted to an Extraction-style Dungeon RPG. |

---

## 1. Game Concept

### 1.1 Genre Redefinition

> **Extraction Dungeon RPG × Card Battle**
> Planned exploration and growth with the ultimate goal of survival.

**Reference Titles:**

* Shiren the Wanderer (Extraction-style)
* Slay the Spire (Card Battle)
* Dark Souls (Severity of death penalty)

---

### 1.2 Design Philosophy Shift

```
【Advantages of the Extraction-style】
Exploration → Survival → Retain Equipment, Items, Gold, & Souls → Growth
       ↓
     Death → Total Loss (Equip/Items/Gold) + 100% Soul Gain + Life -1
       ↓
   Equipment, Cards, and Gold at BaseCamp are retained

Pros:
- Clear Risk/Reward
- Sense of achievement upon survival
- Strategic depth regarding the timing of return
- Tension maintained by the Life System

```

---

## 2. Core Game Mechanics

### 2.1 Core Loop

```
┌─────────────────────────────────────────────┐
│                                             │
│  BaseCamp (Safe Zone)                       │
│  ├─ Equipment Enhancement (Blacksmith)      │
│  ├─ Equipment Purchase (Shop)               │
│  ├─ Deck Building (Library)                 │
│  └─ Level up via Soul Remnants (Sanctuary)  │
│                                             │
└──────────────┬──────────────────────────────┘
               │ Select equipment/deck and depart
               ↓
┌─────────────────────────────────────────────┐
│  Dungeon (Danger Zone)                      │
│  ├─ Battle → Gain Magic Stones, Gear, Card Mastery, & Souls │
│  ├─ Decide to push deeper or return         │
│  └─ Two Options:                            │
│     [A] Survival → Extract everything       │
│     [B] Death → Total Loss + Soul Gain + Life -1│
└──────────────┬──────────────────────────────┘
               │
               ↓
      [A] In case of Survival:
      - Bring back Gear, Gold (from events), and Souls to BaseCamp
      - Strengthen for the next expedition
      - No change to Lives

      [B] In case of Death:
      - Total loss of all carried items and equipment
      - Acquired Gold is reset to zero
      - 100% of Soul Remnants gained (added to cumulative total)
      - Life -1
      - Equipment, Cards, and Gold stored at BaseCamp are safe
               ↓
      Lives > 0 → Return to BaseCamp
      Lives = 0 → Game Over (Hard Reset)

```

---

### 2.2 Risk/Reward Design

#### Benefits of Survival (Return)

| Retainable Assets | Description |
| --- | --- |
| Gold | Total Gold acquired during the Dungeon run |
| Equipment | Equipment acquired within the Dungeon |
| Items | Consumables picked up in the Dungeon |
| Soul Remnants | Experience points earned by defeating monsters |

*Note: Gold in dungeons is only obtained through specific events. Gold is not obtained as a battle reward.*

**Decision points for survival:**

* Remaining HP
* Equipment Durability (AP)
* Quantity of healing items on hand
* Acquisition of high-level weapons or Magic Stones
* Danger level of the next Depth

#### Penalties of Death (Risk)

| Assets Lost | Description |
| --- | --- |
| **All Carried Gear** | All equipment brought in and acquired during the run are lost |
| **All Carried Items** | All consumables brought in and picked up are lost |
| Acquired Gold | Gold acquired within the Dungeon is reset to zero |
| **One Life** | Total Lives decrease by 1 |

**Items gained upon death (Important):**

| Assets Gained | Description |
| --- | --- |
| **100% Souls** | All Souls earned during that run are added to the cumulative total |

**Items retained (Important):**

* Equipment stored at BaseCamp
* Gold balance at BaseCamp
* Soul Remnants (cumulative level) from past explorations
* Permanent upgrades unlocked at the Sanctuary

**Design Intent:**

* Death is very painful, but Souls are guaranteed.
* Risk management of "bring-in" equipment is vital.
* The dilemma of "should I bring this equipment or not?"
* Final tension provided by the Life System.

---

### 2.3 Life System (Retries)

#### Purpose of Limitation

**Why Lives are necessary:**

* To prevent infinite trial-and-error.
* To give weight to each death.
* To encourage cautious play.
* To maintain tension throughout the entire game.

#### Life Mechanics

**Basic Rules:**

```
Max Lives by Difficulty:
- Hard: 2
- Normal: 3
- Easy: 3

Life Decrease Timing: Only upon death.
Life Recovery: None.

Death with 0 Lives → Game Over (Hard Reset)

```

**Life Fluctuations:**

| Situation | Life Change | Remarks |
| --- | --- | --- |
| Start Exploration | No change |  |
| Survive via Return Route | No change |  |
| Survive via Teleport Stone | No change |  |
| Death in Depth 1-4 | **-1** |  |
| Death in The Abyss (Depth 5) | **-1** |  |
| Escape after Abyss Boss | No change | Game Clear |

#### Variations by Difficulty

| Difficulty | Max Lives | Expected Playstyle |
| --- | --- | --- |
| Easy | 3 | Some trial-and-error is possible |
| Normal | 3 | Planned exploration is required |
| Hard | 2 | Failure is not permitted |

---

## 3. Overall Progression System

### 3.1 Pillars of Growth

Growth within the game consists of **three pillars**:

#### (1) Equipment Growth (In-run + Permanent)

**Characteristics:**

* Strengthen/Purchase at BaseCamp.
* Carry into the Dungeon.
* Bring back if you survive; **lose if you die.**

**Growth Elements:**

* Equipment Level (Lv0-3)
* Equipment Quality (poor/normal/good/master)
* Equipment Rarity (Common → Legendary)

**Risk:**

* Bringing high-level gear → **Massive loss upon death.**
* Attempting with low-level gear → Difficult to clear.

#### (2) Card Growth (Permanent)

**Characteristics:**

* Cards themselves are not lost (recorded in the encyclopedia).
* Mastery increases with usage.
* Cards evolve based on mastery levels.

**Growth Elements:**

* Card Mastery (Lv1-5)
* Card Evolution (New effects/branching paths)

**Retention:**

* Card acquisition status is never lost.
* Deck configurations are saved in the Library.

#### (3) Soul Remnants (Experience System - Permanent)

**Characteristics:**

* Earn Souls by defeating monsters.
* **100% added to total if you survive.**
* **100% added to total even if you die.** (V3.0 Change)

**Growth Elements:**

* Unlock skill trees at the Sanctuary.
* Basic stat enhancement (HP/Inventory capacity).
* Unlock special abilities (Under consideration).

**Experience Calculation:**

```typescript
Rarity of Gained Souls = Strength of the enemy
Experience Amount (Cumulative Souls) = Conversion of gained souls
Monster Souls: (Expandable)
- Minion (Low): Small Soul (=Cumulative Souls * 10)
- Minion (Mid): Medium Soul (=Cumulative Souls * 50)
- Minion (High): Large Soul (=Cumulative Souls * 100)
- Elite (Floor Boss): Majestic Onisoul (=Cumulative Souls * 500)
- Boss (Evil God in the Abyss): Raging Godsoul (=Cumulative Souls * 1000)

```

**Leveling Up:**

```
Cumulative Souls  Skill Points
0-99              0
100-299           1
300-599           2
600-999           3
1000+             4
...

```

---

### 3.2 Overall Game Flow

#### Phase 1: Early Game (Until Lives go from 3 to 2)

**Goal:** Establish foundations.

```
- Purchase initial equipment at the Shop.
- Strengthen equipment at the Blacksmith (around Lv1).
- Build a deck in the Library.
- Explore Depth 1-2 to accumulate Souls and Mastery.
- Growth continues even if you die, as Souls are retained.

```

#### Phase 2: Mid Game (Remaining Lives: 2)

**Goal:** Increase combat power.

```
- Upgrade equipment with Gold brought back.
- Attempt Quality Upgrades (Gacha) at the Blacksmith.
- Unlock skill trees at the Sanctuary.
- Challenge Depth 3-4.
- Balance the risk of bringing valuable equipment.

```

#### Phase 3: Late Game (Remaining Life: 1)

**Goal:** Reach the deepest level.

```
- Challenge with the best equipment and deck.
- Reach Depth 5 (The Abyss).
- The challenge at Life 1 is a matter of life and death.
- Escape route opens after defeating the boss.

```

---

### 3.3 Ending Conditions

**Success Condition:**

```
Defeat the Depth 5 boss and survive by returning via the escape route.

```

**Failure Condition:**

```
Death while having 0 Lives → Game Over.

```

**Upon Game Over:**

```
Hard Reset:
- Gold: Resets to initial value.
- Equipment: Initial equipment only.
- Soul Remnants (Cumulative): Resets to 0.
- Sanctuary Unlock Status: Reset.
- Card Deck: Resets to initial deck.
- Encyclopedia: Reset.
- Known Event Information: Reset.

What persists:
- Achievement unlock status only.

```

---

## 4. Role of Each System (Redefined)

### 4.1 BaseCamp Facility Redefinition (V3.1 Updated)

> **V3.1 Changes:** Consolidated facilities from 7 to 5. Library → Journal (Header UI). Storage → Guild (Tab).

#### Guild (The Pub)

**Role:** Starting point of the game + Item management.

**Tab Structure:**

```
Guild
├── Headquarters
│   ├── Character Selection
│   ├── Life Check
│   ├── Promotion Exams
│   └── Rumors
│
└── Storage
    ├── Item Storage (Retained upon death)
    ├── Inventory Management (Lost upon death)
    └── Equipment Management

```

#### Shop (Exchange)

**Role:** Hub for equipment procurement.

* Purchase equipment (Costs Gold).
* Sell equipment (Convert unwanted gear to Gold).
* Exchange Magic Stones for Gold.

#### Blacksmith

**Role:** Ultimate enhancement of equipment.

* Level up (Lv0-3).
* Quality Improvement (Gacha element).
* Repair (Restore AP).
* Dismantle (Convert back to Magic Stones).

**Strategy:**

* High-level gear is risky but powerful.
* Decide whether to gamble for the best quality in the Gacha.

#### Sanctuary (Temple)

**Role:** Permanent enhancement using Soul Remnants.

**Changes in V3.0:**

```
Old: Souls added only on survival; zero for the run on death.
New: 100% Souls added regardless of survival or death.

```

**Skill Tree:**

* Basic stat enhancement (HP/Gold).
* Special abilities (Appraisal/Expansion).
* *Note: Exploration count expansion skill has been removed.*

#### Dungeon Gate (Entrance to the Abyss)

**Role:** Starting the exploration.

* Depth selection.
* Life confirmation.
* **Check possession of Teleport Stones.**

#### Journal (Handwritten Notes) — Header UI

> **Note:** The Journal is not a facility but a UI accessible at all times from the header.

**Role:** Build research and record management.

**Page Structure:**

```
Journal
├── Chapter 1: Tactics — Deck Composition
├── Chapter 2: Memories — Encyclopedia (Cards/Equipment/Monsters)
├── Chapter 3: Thoughts — Strategy Notes
└── Colophon: Settings — Save/Load

```

**Details:** See `journal_document/journal_system_implementation_plan.md`

---

### 4.2 Resource Economy Redesign

#### Flow of Gold

```
【Acquisition】
1. Selling Magic Stones and Equipment at BaseCamp.

2. Dungeon Exploration → Event triggers (Unimplemented)
              ↓
         Survival → Bring back to BaseCamp
              ↓
         Death → Zero (Lost)

【Consumption】
BaseCamp:
- Shop: Purchase equipment
- Blacksmith: Enhancement and Repair

```

#### Flow of Magic Stones

```
【Acquisition】
1. Dungeon Exploration → Monster drops
              ↓
   Event triggers → Obtain Magic Stones
              ↓
         Survival → Bring back to BaseCamp
              ↓
         Death → Zero (Lost)

【Consumption】
- Blacksmith: Equipment enhancement
- Shop: Gold exchange (Emergency)

```

#### Flow of Soul Remnants

```
【Acquisition】
Dungeon Exploration → Defeat Monsters → Gain Souls (Experience points)
              ↓
         Survival → 100% added to cumulative total (Permanent)
              ↓
         Death → 100% added to cumulative total (Permanent) ★V3.0 Change

【Consumption】
- Sanctuary: Skill tree unlock

```

**Crucial Design (V3.0):**

* Soul Remnants are **obtained 100% regardless of survival or death.**
* The penalty for death is "Loss of Life" and "Item Loss."
* Growth elements (Souls) are secured, so there is no such thing as a "completely wasted death."

---

## 5. Difficulty Design Policy

### 5.1 Difficulty Curve

```
Depth 1: Tutorial-level difficulty.
         Minions only; clearable even without equipment.

Depth 2: Basic equipment required.
         Clearable with Gear Lv0-1.

Depth 3: Strategy required.
         Gear Lv1-2 + Optimized deck composition.

Depth 4: High difficulty.
         Gear Lv2-3 + Recommended quality "Good" or higher.

Depth 5: Maximum difficulty.
         Best gear + Optimized deck + Sanctuary upgrades mandatory.

```

### 5.2 Death Penalty Balance

**Design Goal:**

* Death is very painful, but growth does not stop.
* Encourage cautious play.
* Emphasize risk management of "bring-in" equipment.

**Adjustment Points:**

```
Weight of Death Penalty = Value of brought equipment + Acquired resources + 1 Life

Early game: Equipment value is low → Small penalty.
Mid game: Equipment value rises → Medium penalty.
Late game: Bringing the best equipment → Large penalty.

```

### 5.3 Life System Balance

**Goal:**

* Cautious Player: Reach Depth 5 while conserving Lives.
* Average Player: Challenge Depth 5 with 1-2 Lives remaining.
* Reckless Player: Game Over at 0 Lives.

---

## 6. Player Experience Design

### 6.1 Intended Play Experience

**Tension:**

* "Should I bring this piece of equipment?"
* "Should I go one step further, or go home now?"
* "I only have one Life left."

**Sense of Achievement:**

* "I survived safely!"
* "I brought back high-level equipment!"
* "I reached Depth 5!"

**Strategic Depth:**

* Equipment selection and bring-in risk.
* Timing of return.
* Deck composition.
* Skill tree choices.

### 6.2 Player Choices

**Before Exploration:**

```
[1] Which equipment to bring?
    - High-level gear (High risk, High reward)
    - Low-level gear (Low risk, Low reward)
    - No gear (No loss on death, extremely difficult to clear)

[2] Which deck to use?
    - Attack-focused
    - Defense-oriented
    - Balanced type

[3] Which Depth to challenge?
    - Depth 1-2 (Safe, low rewards)
    - Depth 3-4 (Dangerous, medium rewards)
    - Depth 5 (Deadly, high rewards)

```

**During Exploration:**

```
[1] Choice after battle:
    - Proceed to the next room
    - Return via Teleport Stone
    - Return via Return Route

[2] Obtaining new gear:
    - Equip and continue (Increases loss on death)
    - Return immediately to prioritize keeping it

[3] Judging remaining HP:
    - Still can fight → Go deeper
    - Danger zone → Return

```

---

## 7. Overall Game Flow Chart

```
┌─────────────────────────────────────────────┐
│  Game Start                                  │
│  - Lives: 3 (Normal/Easy) or 2 (Hard)        │
│  - Soul Remnants: 0                          │
│  - Gold: 500                                 │
└──────────────┬──────────────────────────────┘
               ↓
┌─────────────────────────────────────────────┐
│  BaseCamp (Preparation)                       │
│  - Shop: Buy initial gear                    │
│  - Library: Build deck                       │
│  - Blacksmith: Enhance gear                  │
│  - Sanctuary: Skill Tree (if Souls available)│
└──────────────┬──────────────────────────────┘
               ↓
         Dungeon Exploration
               ↓
    ┌──────────┴──────────┐
    │                     │
 Survival               Death
    │                     │
    ↓                     ↓
Extract Everything     Total Loss
Lives Unchanged        100% Soul Gain
    │                  Life -1
    │                     │
    └──────────┬──────────┘
               ↓
         Lives > 0?
         │        │
        Yes       No
         │        ↓
         │    Game Over
         │    (Hard Reset)
         ↓
    Return to BaseCamp
         ↓
    Depth 5 Cleared?
         │        │
        No        Yes
         │        ↓
         └──→  Ending
               (Success)

```

---

## 8. Design Priorities

### 8.1 Phase 1 (MVP)

**Goal:** Implementation of the basic loop.

```
□ BaseCamp basic functions (Guild/Shop/Blacksmith/Sanctuary)
□ Dungeon Exploration (Depth 1-3)
□ Survival/Death systems
□ Life system
□ Soul Remnant (XP) system
□ Basic Sanctuary skill tree

```

### 8.2 Phase 2 (Expansion)

**Goal:** Adding strategic depth.

```
□ Blacksmith quality Gacha
□ Library Encyclopedia and Deck Building
□ Card Mastery system
□ Sanctuary skill tree expansion
□ Implementation of Depth 4-5
□ Abyss Escape Route

```

### 8.3 Phase 3 (Completion)

**Goal:** Balance adjustment and presentation.

```
□ Difficulty balancing
□ Life system balance adjustment
□ Presentation and animations
□ UI/UX optimization
□ Ending implementation

```

---

## 9. Reference Documents

```
GAME_DESIGN_MASTER_V3.1 [This Document]
├── CAMP_FACILITIES_DESIGN_V4
│   ├── guild_design.md (V3.0 - includes Storage tab)
│   ├── shop_design.md
│   ├── blacksmith_design.md
│   └── sanctuary_design.md
├── journal_document/journal_system_implementation_plan.md
├── battle_logic.md
├── card_system.md
├── return_system_v3.md
└── dungeon_system.md

```

**Future Features:**

```
.claude/feature_plans/
├── quest_system.md
├── npc_conversation.md
├── title_system.md
└── dark_market.md

```

---

## Summary

### The Heart of the New Game Design (V3.0)

**Genre:**

> Extraction-style Dungeon RPG × Card Battle

**Core Mechanics:**

> Survival or Death Choice + Life System

**Features of the Life System:**

1. Lives decrease only upon death.
2. Lives are conserved upon successful return.
3. Max Life varies by difficulty (Hard: 2, Normal/Easy: 3).
4. No means of recovering Lives.
5. Death with 0 Lives leads to Game Over (Hard Reset).

**Features of the Death Penalty:**

1. Total loss of all owned items and equipment.
2. Total loss of all acquired Gold.
3. **100% of Soul Remnants are gained.**
4. Life -1.

**Three Pillars of Growth:**

1. Equipment Growth (Risk/Reward)
2. Card Growth (Permanent)
3. Soul Remnants (Experience/Permanent)

**Player Experience:**

> Risk assessment of "Should I bring this equipment?"
> Dilemma of "Should I go further or return?"
> Tension provided by the Life System.
> Sense of achievement upon survival.
