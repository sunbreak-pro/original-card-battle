# BaseCamp Integrated Design Specification V4.0

## Update History

| Date | Content |
|------|---------|
| 2026-02-04 | V4.0: Facility consolidation (7 → 5). Library → Journal (header UI). Storage → Guild (tab). |
| - | V3.0: Complete Transition to New Design - Shift to "Extraction Dungeon RPG", Soul Remnants as XP, Exploration Limit added. |
| - | V2.0: Revamped to focus on design philosophy. Added Sanctuary/Library. Removed Church/Training. |
| - | V3.2: Inn Removed - Facility removed to simplify implementation scope. |

---

## 1. Design Philosophy

### 1.1 Role of BaseCamp

BaseCamp is the "Place of Rest and Preparation" visited between extraction dungeon explorations.

**3 Core Concepts:**

1. **Preparation**: Strengthening forces for the next exploration.
2. **Progression**: Permanent advancement between explorations.
3. **Management**: Organizing resources, equipment, and decks.

**V4.0 Consolidation Changes:**

```
Old: 7 facilities (fragmented responsibilities)
New: 5 facilities + Journal (header UI)

Removed as standalone facilities:
- Library → Migrated to Journal system (header UI)
- Storage → Integrated into Guild as a tab
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
- **Information Accumulation:** Knowledge building via Encyclopedia (Journal).
- **Lives System:** Adding weight and consequence to death (limited retries).

---

## 2. Facility Composition

### 2.1 Facility List (5 Facilities)

| Facility | English Name | Main Role | Progression Type | Status |
|----------|--------------|-----------|------------------|--------|
| 酒場 | **Guild** | Character selection, Status, **Storage management** | Start Run | ✅ |
| 取引所 | **Shop** | Buying/Selling equipment & items | In-Run | ✅ |
| 鍛冶屋 | **Blacksmith** | Enhance, Repair, Dismantle equipment | In-Run | ✅ |
| 神殿 | **Sanctuary** | Permanent upgrades via Soul Remnants | Inter-Run | ✅ |
| ダンジョン | **Dungeon Gate** | Entrance to the Abyss | Start Exploration | ✅ |

### 2.2 Consolidated Systems (Not Standalone Facilities)

| System | Location | Role |
|--------|----------|------|
| **Journal (手記)** | Header UI | Deck building, Encyclopedia, Settings |
| **Storage (倉庫)** | Guild (tab) | Item storage, Inventory management |

**References:**
- Journal: See `journal_document/journal_system_implementation_plan.md`
- Storage in Guild: See `guild_design.md` Section 10

### 2.3 Relationships Between Facilities

```
[Exploration Flow]
Guild -> Dungeon Gate -> Combat/Rewards -> Survival or Death
  │                                              │
  ├─ Select Character                            │
  ├─ Check Lives                                 │
  ├─ Manage Storage (Storage tab)                │
  └─ Promotion Exam                              │
                                                 │
                                          Storage (Guild tab)
                                                 ↓
                                          Shop/Blacksmith (Gear Up)
                                                 ↓
                                          Journal (Adjust Deck) ← Header UI
                                                 ↓
                                          Sanctuary (Soul Upgrades)
                                                 ↓
                                          Next Exploration or End

[Difference Between Survival & Death]
Survival: Bring back Gold, Magic Stones, Gear, Souls -> Enhance
Death:    LOSE Inventory & Equipped Slots, -1 Life
          KEEP items inside Storage (Guild tab) & Transfer Souls (100%)

[Lives System (V3.0)]
Max Lives: Hard=2, Normal/Easy=3
Death: -1 Life, 100% Soul transfer, All items lost
Lives = 0: GAME OVER (Full Reset except achievements)
```

---

## 3. Facility Details

### 3.1 Guild (酒場)

**Concept:** The Starting Point of Adventure + Storage Management

**Tab Structure:**
```
Guild
├── Headquarters (本部)
│   ├── Character Selection
│   ├── Status Check
│   ├── Exploration Count Display
│   └── Promotion Exams
│
└── Storage (倉庫)
    ├── Item Storage (100 slots, retained on death)
    ├── Inventory (20 slots, lost on death)
    └── Equipment Management
```

**Main Functions:**

*Headquarters Tab:*
- Character Selection (Swordsman / Mage)
- Status Check
- **Check Exploration Count**
- **Promotion Exams** (Does not consume Exploration Count)
- Rumors (Pay Magic Stones for next-run buffs)

*Storage Tab:*
- Long-term item storage (safe on death)
- Inventory management (risky on death)
- Equipment slots and equipment inventory management

**Details:** See `guild_design.md`

---

### 3.2 Shop (取引所)

**Concept:** Center of Economy

**Main Functions:**

- Purchase Consumables/Teleport Stones (Pay with Gold or Magic Stones)
- Equipment Packs (Gacha element)
- Sell Equipment

**Resource Flow:**
```
Exploration Rewards (Gold/Stones) -> Shop Purchase -> Power Up
Unwanted Gear -> Sell/Dismantle -> Gold/Stones -> Re-invest
```

**Details:** See `shop_design.md`

---

### 3.3 Blacksmith (鍛冶屋)

**Concept:** Extreme Equipment Enhancement

**Main Functions:**

- Equipment Level Up (Lv0-3)
- Quality System (Poor → Normal → Good → Master)
- Magic Stone Quality (Gacha element)
- Equipment Repair (Restores AP)
- Equipment Dismantle (Returns Magic Stones)

**Growth Formula:**
```
Equipment Stats = BaseStats × Quality Mod × Level Mod
```

**Strategy:**

- **Normal Upgrade:** Low cost, low success rate.
- **Quality Focus:** Medium cost, high success rate, minimum guarantee.
- **Max Quality:** High cost, highest success rate, high-tier guarantee.

**Details:** See `blacksmith_design.md`

---

### 3.4 Sanctuary (神殿)

**Concept:** Permanent Growth via Souls

**Soul Remnants System (V3.0):**
```
[Acquisition]
On Monster Kill:
- Mob: 5 Souls
- Elite: 15 Souls
- Boss: 50 Souls

[Upon Survival]
Souls gained this run × Survival Method Multiplier -> Added to Total
- Return Route: 0.6x/0.8x/1.0x (early/normal/full clear)
- Teleport Stone: 100% (unified type)

[Upon Death]
Souls gained this run -> 100% TRANSFERRED to Total
Lives -> -1
All items/equipment -> Lost

[Upon Game Over (Lives = 0)]
Total Accumulated Souls -> RESET to 0
All Sanctuary Progress -> LOST
```

**Skill Tree Structure:**
```
Center Node (Unlocked for free)
↓
Tier 1: Basic Upgrades (HP+10, Gold+10%, etc.)
↓
Tier 2: Specialization (Class specs, Special effects, Soul Resonance I)
↓
Tier 3: Ultimate Upgrades (Massive bonuses, Soul Resonance II)
```

**Details:** See `sanctuary_design.md`

---

### 3.5 Dungeon Gate (ダンジョンゲート)

**Concept:** Doorway to Exploration

**Main Functions:**

- Select Depth (1-5)
- Check Difficulty
- **Check Lives remaining**
- Start Exploration

**Confirmation Screen:**
```
"Start Exploration?"
Lives: ❤️❤️❤️ (3 remaining)

Warning for Depth 5 (Abyss):
"Survival methods are disabled in the Abyss."
"Lives: 3 remaining."
"Are you sure you want to proceed?"
```

**UI Features:**

- Eerie and alluring visuals.
- Color changes by depth (1: Grey-Green → 5: Deep Purple).
- Lives warning display.

---

## 4. Journal System (Header UI)

> **Note:** Journal is NOT a facility. It is a header UI accessible from any screen.

**Concept:** The player's personal notebook for tactics and knowledge.

**Access:** Click journal icon in header (always visible).

**Pages:**
```
Journal (手記)
├── Chapter 1「戦術」 — Deck building
├── Chapter 2「記憶」 — Encyclopedia (Cards/Equipment/Monsters)
├── Chapter 3「思考」 — Player notes (free text)
└── Appendix「設定」 — Save/Load, Settings
```

**Dungeon Restrictions:**
- Deck editing: View only (no changes)
- Encyclopedia: Full access + real-time updates
- Notes: Full access
- Save/Load: Save limited, Load disabled

**Details:** See `journal_document/journal_system_implementation_plan.md`

---

## 5. Resource Economy

### 5.1 Resource Types and Flow

```
[In-Run Resources (Temporary)]
Gold (Currency):
    - explorationGold: Gold gained during current run (temporary)
    - baseCampGold: Gold stored at BaseCamp (permanent)
    Survival -> explorationGold transferred to baseCampGold
    Death -> explorationGold → ZERO (baseCampGold is kept)

Magic Stones (Currency):
    Types: Small (Value 30), Medium (100), Large (350)
    Same structure as Gold (exploration vs baseCamp)

Equipment:
    Survival -> Bring back
    Death -> All in Inventory & Equipped Slots LOST (Storage is kept)

[Inter-Run Resources (Permanent)]
Soul Remnants:
    Acquisition: On Monster Kill (Like XP)
    Survival -> Add run's souls to Total (Reduced by method)
    Death -> Run's souls are ZERO (Total is kept)
    Usage: Sanctuary Permanent Upgrades

[Information Resources]
Encyclopedia: Discovery -> Journal Record -> Strategy Formulation
```

### 5.2 Economic Balance Design

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

## 6. UI/UX Integration Policy

### 6.1 Screen Transitions

```
BaseCamp (Main Screen)
├─ Guild
│  ├─ Headquarters Tab (Character, Exams, Rumors)
│  └─ Storage Tab (Items, Equipment)
├─ Shop
├─ Blacksmith
├─ Sanctuary (Total Souls / Run Souls display)
└─ Dungeon Gate (Check Count) → Battle Screen

Header (Always Visible)
├─ Journal Icon → Opens overlay
│  ├─ Tactics (Deck Builder)
│  ├─ Memory (Encyclopedia)
│  ├─ Thoughts (Notes)
│  └─ Settings (Save/Load)
└─ Resource Display (Gold, Magic Stones, Lives)
```

### 6.2 Common UI Patterns

**Header:**

- Always Displayed: Gold, Magic Stones, Exploration Count, Journal Icon
- Facility Name
- Back Button

**Resource Display:**

- Shop: Gold, Magic Stones
- Blacksmith: Gold, Magic Stones
- Guild: Exploration Count, Magic Stones (For Rumors)
- Sanctuary: Total Souls, Souls from this Run

**Color Scheme:**

- BaseCamp: Warm Brown/Orange.
- Guild: Lively Tavern atmosphere.
- Shop: Shine of Gold coins.
- Blacksmith: Red of Fire and Iron.
- Sanctuary: Holy White/Gold.
- Journal: Dark parchment, ink tones.

---

## 7. Scalability Considerations

### 7.1 Phase Policy

**Phase 1 (MVP):**

- Guild: Character Select, Count Display, Storage Tab
- Shop: Basic Buy/Sell
- Blacksmith: Upgrade/Repair (Simple quality)
- Sanctuary: Basic Skill Tree (10-15 nodes, Count +1)
- Journal: Deck Building, Basic Encyclopedia
- Dungeon: Depths 1-3
- Exploration Limit System

**Phase 2 (Expansion):**

- Guild: Promotion Exams, Rumors
- Blacksmith: Refined Quality Gacha elements
- Sanctuary: Tier 2 Skills (Count +2)
- Journal: Full Encyclopedia, Notes
- Dungeon: Depths 4-5

**Phase 3 (Completion):**

- Final adjustments for all facilities
- Balance tuning
- End Content
- Sanctuary: Tier 3 Skills (Ultimate Upgrades)

### 7.2 Future Features

Planned features are tracked in `.claude/feature_plans/`:

- `quest_system.md` - Daily/Weekly quests for Guild
- `npc_conversation.md` - NPC dialogue in Guild
- `title_system.md` - Achievement titles
- `dark_market.md` - High-risk trading extension for Shop

**Addition Principles:**

- Must not encroach on existing facility functions.
- Provide clear new value.
- Align with BaseCamp design philosophy.

---

## 8. Technical Considerations

### 8.1 Context API Design

**Shared Context:**

- `GameStateContext`: Screen transitions, global game state.
- `PlayerContext`: Gold, Status, Soul Remnants, Exploration Count.
- `InventoryContext`: Equipment, Items, Magic Stones, Storage.

**Facility Local State:**

- Managed via unique local state for each facility.
- No Context needed between facilities (Avoid over-complexity).

### 8.2 Data Persistence

**Save Data Structure:**

```typescript
{
  player: {
    gold: number,
    sanctuaryProgress: {
      currentRunSouls: number,  // Temporary
      totalSouls: number,       // Permanent
      unlockedNodes: string[],
      explorationLimitBonus: number
    },
    explorationLimit: {
      max: number,
      current: number
    }
  },
  storage: Item[],       // Guild Storage tab
  inventory: Item[],     // Guild Storage tab
  equipment: EquipmentSlots,
  journal: {
    encyclopedia: {
      cards: Set<cardId>,
      equipment: Set<equipmentId>,
      monsters: Set<monsterId>
    },
    notes: string[]      // Player notes
  }
}
```

---

## 9. Summary

### 9.1 Core of Design (V4.0)

BaseCamp is composed of 3 pillars:

1. **Tactical Preparation** (Guild, Shop, Blacksmith, Journal)
   - Immediate prep for the next run.
   - In-run resource management.
   - **Management of Exploration Count.**

2. **Strategic Growth** (Sanctuary)
   - Permanent progression between runs.
   - **Enhancement via Soul Remnants (XP).**
   - **Extension of Exploration Count.**

3. **Accumulation of Knowledge** (Journal)
   - Recording and visualizing information.
   - Supporting player skill improvement.

### 9.2 V4.0 Consolidation Summary

**Facility Changes:**
```
7 Facilities → 5 Facilities + Journal (Header UI)

Removed:
- Library (standalone) → Journal (header UI)
- Storage (standalone) → Guild (Storage tab)

Remaining:
- Guild (with integrated Storage)
- Shop
- Blacksmith
- Sanctuary
- Dungeon Gate
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
CAMP_FACILITIES_DESIGN_V4 [This Document]
├── guild_design.md (V3.0 - includes Storage tab)
├── shop_design.md
├── blacksmith_design.md
├── sanctuary_design.md
└── journal_document/journal_system_implementation_plan.md
```

**High-Level Design:**

```
Overall_document/game_design_master.md (Overall Design)
└── return_system_v2.md (Extraction System)
```

**Future Features:**

```
.claude/feature_plans/
├── quest_system.md
├── npc_conversation.md
├── title_system.md
└── dark_market.md
```
