# BaseCamp Integrated Design Specification V3.0

## Update History

- **V3.0: Complete Transition to New Design** - Shift to "Extraction Dungeon RPG" (Extraction-based), Soul Remnants converted to XP system, Exploration Limit added.
- **V2.0:** Revamped to focus on design philosophy. Added Sanctuary/Library. Removed Church/Training.

---

## 1. Design Philosophy

### 1.1 Role of BaseCamp

BaseCamp is the "Place of Rest and Preparation" visited between extraction dungeon explorations.

**3 Core Concepts:**

1. **Preparation**: Strengthening forces for the next exploration.
2. **Progression**: Permanent advancement between explorations.
3. **Management**: Organizing resources, equipment, and decks.

**Major Changes in V3.0:**

```
Old: Roguelite Elements (Die to get stronger)
New: Extraction Type (Survive to grow)

- Clarification of Death Penalties
- Soul Remnants = Experience Point (XP) System
- Addition of Exploration Limits

```

### 1.2 Design Principles

**Clear Functional Separation:**

- Each facility has a distinct, independent role.
- Avoid functional overlap; design for mutual complementation.
- Intuitive layout so players do not get lost.

**Gradual Complexity:**

- **Early Game:** Simple operations (Buy/Enhance).
- **Mid Game:** Strategy (Deck building/Quality improvement).
- **Late Game:** Optimization (Permanent upgrades/Build research).

**Integration of Extraction Elements:**

- **In-Run Progression:** Gold, Equipment Levels, Card Acquisition.
- **Inter-Run Progression:** Permanent strengthening via Soul Remnants (XP).
- **Information Accumulation:** Knowledge building via Encyclopedia and Records.
- **Exploration Limit:** Adding weight and consequence to each exploration.

---

## 2. Facility Composition

### 2.1 Facility List

BaseCamp consists of the following 7 facilities:

| Facility   | English Name     | Main Role                                                     | Progression Type  |
| ---------- | ---------------- | ------------------------------------------------------------- | ----------------- |
| 酒場       | **Guild**        | Character selection, Check exploration count, Promotion exams | Start Run         |
| 取引所     | **Shop**         | Buying/Selling equipment & items                              | In-Run            |
| 鍛冶屋     | **Blacksmith**   | Enhance, Repair, Dismantle equipment                          | In-Run            |
| 神殿       | **Sanctuary**    | Permanent upgrades via Soul Remnants                          | Inter-Run         |
| 図書館     | **Library**      | Deck building, Encyclopedia, Records                          | Management        |
| 倉庫       | **Storage**      | Item storage & organization                                   | Management        |
| ダンジョン | **Dungeon Gate** | Entrance to the Abyss                                         | Start Exploration |

### 2.2 Relationships Between Facilities

```
[Exploration Flow]
Guild -> Dungeon -> Combat/Rewards -> Survival or Death
  ↓                                          ↓
Select Character                        Storage (Organize Items)
Check Exploration Count                      ↓
Promotion Exam                          Shop/Blacksmith (Gear Up)
                                             ↓
                                        Library (Adjust Deck)
                                             ↓
                                        Sanctuary (Soul Upgrades)
                                             ↓
                                        Next Exploration or End

[Difference Between Survival & Death]
Survival: Bring back Gold, Magic Stones, Gear, Souls -> Enhance
Death:    LOSE Inventory & Equipped Slots
          KEEP items inside Storage & Accumulated Souls

[Exploration Limit]
Must reach the deep layers within 10 runs.
Normal Exploration: +1 Count (regardless of Survival/Death)
Promotion Exam:     Does NOT consume Exploration Count
Limit Exceeded ->   GAME OVER

```

---

## 3. Function Overview

### 3.1 Guild

**Concept:** The Starting Point of Adventure

**Main Functions:**

- Character Selection (Swordsman / Mage / Summoner)
- Status Check
- **Check Exploration Count (V3.0 - NEW)**
- **Promotion Exams (V3.1 - NEW)**
- Does not consume Exploration Count.
- Reward is Promotion only (No equipment rewards).

- Quest Acceptance (Future Expansion)

**Changes in V3.0:**

```
Exploration Count Display:
"Exploration Count: 7 / 13 (6 remaining)"
*13 = Default 10 + Sanctuary Upgrades +3

Warning:
"Remaining attempts are low!"

Promotion Exams:
- Does not consume count (Challenge as many times as needed).
- On Defeat: Return to camp with 1 HP.
- Pass Reward: Title promotion, Permanent status boost.

```

**Details:** See `GUILD_DESIGN_V2.1.md` (Needs Revision)

---

### 3.2 Shop

**Concept:** Center of Economy

**Main Functions:**

- Purchase Consumables/Teleport Stones (Pay with Gold or Magic Stones)
- Equipment Packs (Gacha element)
- Sell Equipment

**Changes in V3.0:**
Emphasis on:

- The importance of purchasing equipment.
- Recovery means after death.

```
Resource Flow:
Exploration Rewards (Gold/Stones) -> Shop Purchase -> Power Up
Unwanted Gear -> Sell/Dismantle -> Gold/Stones -> Re-invest

```

**Details:** See `SHOP_DESIGN_V1.md` (Needs Minor Revision)

---

### 3.3 Blacksmith

**Concept:** Extreme Equipment Enhancement

**Main Functions:**

- Equipment Level Up (Lv0-3)
- Quality System (Poor → Normal → Good → Master)
- Magic Stone Quality (Gacha element)
- Equipment Repair (Restores AP)
- Equipment Dismantle (Returns Magic Stones)

**Changes in V3.0:**

```
No functional changes (Maintain existing design).

However, change in strategic value:
- High-level equipment is LOST upon death.
- Risk management becomes crucial.

```

**Growth Formula:**

```
Equipment Stats = BaseStats × Quality Mod × Level Mod

```

**Strategy:**

- **Normal Upgrade:** Low cost, low success rate.
- **Quality Focus:** Medium cost, high success rate, minimum guarantee.
- **Max Quality:** High cost, highest success rate, high-tier guarantee.

**Details:** See `BLACKSMITH_DESIGN_V1.md` (No Changes)

---

### 3.4 Sanctuary

**Concept:** Permanent Growth via Souls

**Major Changes in V3.0:**

```
Old: Gain Souls on Death -> Permanent Upgrade (Roguelite)
New: Gain Souls on Kill (XP) -> Add to Total upon Survival -> Permanent Upgrade

```

**Main Functions:**

- Unlock Skill Tree using Soul Remnants.
- Permanent upgrades persisting between runs.
- Improve Player Base Stats.

**Soul Remnants System (V3.0 - XP Conversion):**

```
[Acquisition]
On Monster Kill:
- Mob: 5 Souls
- Elite: 15 Souls
- Boss: 50 Souls

[Upon Survival]
Souls gained this run × Survival Method Multiplier -> Added to Total
- Return Route: 100%
- Normal Teleport Stone: 70%
- Blessed Teleport Stone: 80%
- Emergency Teleport Stone: 60%

[Upon Death]
Souls gained this run -> ZERO
Total Accumulated Souls -> KEPT (No change)

```

**Skill Tree Structure:**

```
Center Node (Unlocked for free)
↓
Tier 1: Basic Upgrades (HP+10, Gold+10%, etc.)
↓
Tier 2: Specialization (Class specs, Special effects, Exploration Count +1)
↓
Tier 3: Ultimate Upgrades (Massive bonuses, Exploration Count +2)

```

**Important New Skills (V3.0 - NEW):**

- **Extended Exploration I**: Exploration Count +1 (Cost: 80 Souls)
- **Extended Exploration II**: Exploration Count +2 (Total +3, Cost: 150 Souls)

**Design Intent:**

- Motivation to defeat monsters.
- Massively increases the value of "Survival".
- Death is painful, but accumulated progress is kept (Not a full reset).
- Expanding exploration limits broadens strategic options.

**Details:** See `SANCTUARY_DESIGN_V2.md`

---

### 3.5 Library

**Concept:** Hall of Knowledge and Records

**Main Functions:**

1. **Deck Builder**: Combine Cards, Equipment, Items.
2. **Encyclopedia**: Records of Cards, Equipment, Items, Monsters.
3. **Records**: Titles, Achievements.
4. **Memory Room**: Save/Load.

**The 4 Bookshelves:**

**📖 Book of Composition:**

- Deck Construction (Card Selection)
- Equipment Set Selection
- Starting Item Selection
- Loadout Save/Load (Set 1, 2, 3)

**📕 Book of Knowledge (Encyclopedia):**

- Card Compendium (Mastery, Evolution info)
- Equipment Compendium (Rarity, Effects)
- Item Compendium (Consumables, Stones)
- Monster Compendium (Spawn Depth, Drops)

**📘 Book of Records:**

- Title System
- Achievements (Clear count, Deepest reach, etc.)
- Statistics (**Exploration Count recorded - V3.0**)

**📗 Room of Memory:**

- Save Data Management (Multiple slots)
- Cloud Sync (Future expansion)
- Data Export

**Changes in V3.0:**

```
Added to Statistics:
- Total Exploration Count
- Survival Count
- Death Count
- Average Souls Gained

```

**Design Intent:**

- Place for build research.
- Visualize player progress.
- Encourage trial and error.

**Details:** See `LIBRARY_DESIGN_V1.md` (Needs Minor Revision)

---

### 3.6 Dungeon Gate

**Concept:** Doorway to Exploration

**Main Functions:**

- Select Depth (1-5)
- Check Difficulty
- **Check Exploration Count (V3.0 - NEW)**
- Start Exploration

**Changes in V3.0:**

```
Confirmation Screen before Start:
"Start Exploration?"
Exploration Count: 7 / 13 (6 remaining)

Warning for Depth 5 (Abyss):
"Survival methods are disabled in the Abyss."
"Exploration Count: 6 remaining."
"Are you sure you want to proceed?"

```

**UI Features:**

- Eerie and alluring visuals.
- Color changes by depth (1: Grey-Green → 5: Deep Purple).
- Exploration count warning display.

---

## 4. Resource Economy

### 4.1 Resource Types and Flow (V3.0)

```
[In-Run Resources (Temporary)]
Gold (Currency): Exploration Reward -> Shop Buy / Blacksmith Upgrade & Repair
    Survival -> Bring back (Reduced by method)
    Death -> ZERO (BaseCamp storage is kept)

Magic Stones (Currency): Enemy Drops -> Blacksmith / Guild Rumors / Shop
    Types: Small (Value 30), Medium (100), Large (350)
    Does not occupy Inventory (Currency type)
    Survival -> Bring back (Reduced by method)
    Death -> ZERO (BaseCamp storage is kept)

Equipment: Shop/Drops -> Blacksmith Upgrade -> Dismantle
    Survival -> Bring back
    Death -> All in Inventory & Equipped Slots LOST (Storage is kept)

AP: Combat consumption -> Blacksmith Repair
    Survival -> Remains as is
    Death -> -

[Inter-Run Resources (Permanent)]
Soul Remnants:
    Acquisition: On Monster Kill (Like XP)
    Survival -> Add run's souls to Total (Reduced by method)
    Death -> Run's souls are ZERO (Total is kept)
    Usage: Sanctuary Permanent Upgrades

[Information Resources (Record Type)]
Encyclopedia: Discovery -> Library Record -> Strategy Formulation

```

### 4.2 Economic Balance Design (V3.0)

**Early Game (Explorations 1-3):**

- Gold is scarce, Magic Stones are rare.
- Equipment procurement mainly via Shop.
- Avoid death to ensure survival.
- Accumulate Souls.

**Mid Game (Explorations 4-7):**

- Souls start to increase.
- Sanctuary upgrades become important.
- Blacksmith upgrades become essential.
- Judgment of Risk vs. Return.

**Late Game (Explorations 8-10):**

- Resources are plentiful, but Exploration Count is low.
- Value of survival is extremely high.
- Final upgrades in Sanctuary.
- Challenge to the Deep Layers.

---

## 5. Progression Integration (V3.0)

### 5.1 In-Run Progression (Temporary)

**Flow:**

```
1. Guild: Select Character, Check Count
2. Dungeon: Start Exploration
3. Combat: Gain Rewards (Gold, Stones, Gear, Souls)
4. Survival Decision: Teleport Stone OR Return Route OR Go Deeper
5. Survival -> Return to BaseCamp
   - Shop: Buy/Sell Gear
   - Blacksmith: Upgrade/Repair
   - Library: Adjust Deck
   - Sanctuary: Permanent Upgrade with Souls
6. Next Exploration OR Clear/Death

```

### 5.2 Inter-Run Progression (Permanent - V3.0)

**Upon Survival:**

```
1. Acquire: Gold, Magic Stones, Equipment, Soul Remnants.
2. Sanctuary: Unlock Skill Tree.
3. Exploration Count: +1.
4. Guild: To next exploration (With enhanced status).

```

**Upon Death:**

```
1. LOST: All items in Inventory, All equipped Gear,
   Gold/Stones/Souls gained during the run.
2. KEPT: Items in Storage, BaseCamp stored Gold/Stones,
   Total Accumulated Souls, Cards, Sanctuary Nodes.
3. Exploration Count: +1 (Except Promotion Exams).
4. Guild: To next exploration (Retry with gear from Storage).

```

**Exploration Limit:**

```
Default: 10 Runs
Sanctuary Upgrade: +1, +2 (Max +3)

Low remaining runs -> Value of Survival skyrockets.
Limit Exceeded -> GAME OVER.

```

### 5.3 Knowledge Accumulation (Metaprogression)

**Library Encyclopedia:**

- Card Mastery: Unlocked by usage count.
- Equipment: Unlocked by acquisition.
- Monsters: Unlocked by encounter.

**Strategic Value:**

- Reduce unknown elements.
- Research effective builds.
- Improve player skill.

---

## 6. UI/UX Integration Policy

### 6.1 Screen Transitions

```
BaseCamp (Main Screen)
├─ Guild (Exploration Count, Promotion Exams)
├─ Shop
├─ Blacksmith
├─ Sanctuary (Total Souls / Run Souls display)
├─ Library
│ ├─ Deck Builder
│ ├─ Encyclopedia
│ ├─ Achievements (Exploration Stats)
│ └─ Save/Load
├─ Storage
│ ├─ Storage Tab (Items in Warehouse)
│ └─ Inventory Tab (Items on Hand)
└─ Dungeon Gate (Check Count) → Battle Screen

```

### 6.2 Common UI Patterns

**Header:**

- Always Displayed: Gold, **Magic Stones (Currency)**, **Exploration Count (V3.0 - NEW)**.
- Facility Name.
- Back Button.

**Resource Display:**

- Emphasize relevant resources per facility.
- Shop: Gold, Magic Stones (S/M/L qty & total value).
- Blacksmith: Gold, Magic Stones (S/M/L qty & total value).
- Guild: Exploration Count, Magic Stones (For Rumors).
- Storage: Inventory Capacity, Storage Capacity.
- Sanctuary: **Total Souls, Souls from this Run (V3.0)**.
- Library: None (Info only).

**Color Scheme:**

- BaseCamp: Warm Brown/Orange.
- Guild: Lively Tavern atmosphere.
- Shop: Shine of Gold coins.
- Blacksmith: Red of Fire and Iron.
- Sanctuary: Holy White/Gold.
- Library: Calm Blue/Purple.

---

## 7. Scalability Considerations

### 7.1 Phase Policy

**Phase 1 (MVP):**

- Guild: Character Select, Count Display.
- Shop: Basic Buy/Sell.
- Blacksmith: Upgrade/Repair (Simple quality).
- Sanctuary: Basic Skill Tree (10-15 nodes, Count +1).
- Library: Deck Building, Basic Encyclopedia.
- Dungeon: Depths 1-3.
- Exploration Limit System.

**Phase 2 (Expansion):**

- Guild: Quest System.
- Blacksmith: Refined Quality Gacha elements.
- Sanctuary: Tier 2 Skills (Count +2).
- Library: Title System.
- Dungeon: Depths 4-5.

**Phase 3 (Completion):**

- Final adjustments for all facilities.
- Balance tuning.
- End Content.
- Sanctuary: Tier 3 Skills (Ultimate Upgrades).

### 7.2 Potential Facility Additions

**Future Extensions:**

- Arena (PvP / Challenge Mode).
- Gallery (Art / Story).
- Workshop (Customization).

**Addition Principles:**

- Must not encroach on existing facility functions.
- Provide clear new value.
- Align with BaseCamp design philosophy.

---

## 8. Technical Considerations

### 8.1 Context API Design

**Shared Context:**

- `GameStateContext`: Screen transitions, global game state.
- `PlayerContext`: Gold, Status, Soul Remnants (V3.0), **Exploration Count (V3.0 - NEW)**.
- `InventoryContext`: Equipment, Items, Magic Stones.

**Facility Local State:**

- Managed via unique local state for each facility.
- No Context needed between facilities (Avoid over-complexity).

### 8.2 Data Persistence

**Save Data Structure (Example):**

```typescript
{
  player: {
    gold: number,
    sanctuaryProgress: {
      currentRunSouls: number,  // V3.0: Temporary
      totalSouls: number,       // V3.0: Permanent
      unlockedNodes: string[],
      explorationLimitBonus: number // V3.0: NEW
    },
    explorationLimit: {          // V3.0: NEW
      max: number,
      current: number
    }
    // ...
  },
  inventory: Item[],
  library: {
    cardMastery: Map<cardId, level>,
    encyclopedia: {
      cards: Set<cardId>,
      equipment: Set<equipmentId>,
      monsters: Set<monsterId>
    },
    achievements: Set<achievementId>,
    statistics: {
      totalExplorations: number,  // V3.0: NEW
      survivalCount: number,      // V3.0: NEW
      deathCount: number,         // V3.0: NEW
      avgSoulsGained: number      // V3.0: NEW
    }
  },
  // ...
}

```

---

## 9. Summary

### 9.1 Core of Design (V3.0)

BaseCamp is composed of 3 pillars:

1. **Tactical Preparation** (Guild, Shop, Blacksmith, Library)

- Immediate prep for the next run.
- In-run resource management.
- **Management of Exploration Count.**

2. **Strategic Growth** (Sanctuary)

- Permanent progression between runs.
- **Enhancement via Soul Remnants (XP).**
- **Extension of Exploration Count.**

3. **Accumulation of Knowledge** (Library)

- Recording and visualizing information.
- Supporting player skill improvement.

### 9.2 Fundamental Changes in V3.0

**Genre Shift:**

```
Roguelike → Extraction Dungeon RPG

```

**Soul Remnants System:**

```
Gain on Death → Gain on Kill (XP)
Survival adds to Total, Death zeroes run gains.

```

**Exploration Limit:**

```
Reach Deep Layers within 10 Runs.
Expandable via Sanctuary (+1, +2).
Increased weight of Survival and Death.

```

**Clarification of Survival/Death:**

```
Survival: Bring back EVERYTHING (Return).
Death: Run progress LOST, Accumulation KEPT (Risk Management).

```

### 9.3 Success Metrics

**Evidence of Good Design:**

- Players immediately understand the role of each facility.
- Growth is felt between runs.
- **The timing of "Survival" (Extraction) becomes a strategic decision.**
- **Exploration Limits create tension.**
- The loop of Exploration → Return → Enhance feels satisfying.
- BaseCamp itself becomes a fun "Playground."

---

## 10. Reference Documents

**Detailed Facility Design:**

```
BASE_CAMP_DESIGN_V3 [This Document]
├── GUILD_DESIGN_V2.1.md (Needs Revision)
├── SHOP_DESIGN_V1.md (Needs Minor Revision)
├── BLACKSMITH_DESIGN_V1.md (No Changes)
├── SANCTUARY_DESIGN_V2.md (V3.0 Updated)
└── LIBRARY_DESIGN_V1.md (Needs Minor Revision)

```

**High-Level Design:**

```
GAME_DESIGN_MASTER_V2.md (Overall Design)
└── return_system_v2.md (Extraction System)

```

**Related Systems:**

- `battle_logic.md` (Combat System)
- `card_system.md` (Card System)
- `dungeon_system.md` (Dungeon System - To Be Created)
