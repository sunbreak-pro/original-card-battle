> **WEB VERSION RECORD（2026-09-12）**: 魔術師の属性共鳴は Tier 2 R2-7 で投入量スタミナへ再設計するまで無効。本書は Web 版の記録。

# Element System Specification

## 1. Design Philosophy

The Element System assigns an element tag to every card in the game. Elements are a **universal card property independent of character class** — any class can use cards of any element. However, each class's unique ability is **gated by specific element groups**, creating strategic depth in deck building.

### Core Principles

1. **Universal Property, Class-Gated Abilities.** Every card has an element. Any class can play any element's card and receive its base effects (damage, buffs, debuffs). However, class-specific mechanics only activate on their associated element group:
   - Mage Resonance → Magic elements only
   - Swordsman Sword Energy gain → Slash element only

2. **No Weakness/Resistance.** There are no rock-paper-scissors relationships between elements. Fire is not strong against ice. Dark does not resist light. Combat depth comes from card synergy and sequencing, not matchup luck.

3. **Depth Through Sequencing.** The Mage's skill ceiling comes from planning card order within a turn to build resonance chains. The Swordsman's skill ceiling comes from balancing slash-element energy generation with other element utility cards.

4. **Specialization via Sanctuary.** Element-specific Sanctuary skill tree nodes allow Mages to deepen their preferred element's resonance effects, creating distinct build paths.

---

## 2. Element Types

There are 8 element types organized into 2 groups.

### 2.1 Complete Element Table

| Element     | Group     | Icon | Label (JP) | Color     | Description                   |
|-------------|-----------|------|------------|-----------|-------------------------------|
| `fire`      | Magic     | 🔥   | 火         | `#ff4500` | Offensive, burn DoT           |
| `ice`       | Magic     | ❄️   | 氷         | `#00bfff` | Control, freeze               |
| `lightning` | Magic     | ⚡   | 雷         | `#ffd700` | Burst, stun                   |
| `dark`      | Magic     | 🌑   | 闇         | `#6a0dad` | Sustain, lifesteal            |
| `light`     | Magic     | ✨   | 光         | `#fffacd` | Support, cleanse/heal         |
| `slash`     | Physical  | ⚔️   | 斬撃       | `#c0c0c0` | Cutting melee attacks         |
| `impact`    | Physical  | 💥   | 衝撃       | `#ff8c00` | Blunt/impact attacks          |
| `guard`     | Physical  | 🛡️   | 防御       | `#4682b4` | Defensive stance              |

> **Note:** `impact` replaces the former `shock` naming. Code migration is tracked separately.

### 2.2 Element Groups

| Group     | Elements                                    | Associated Class Ability |
|-----------|---------------------------------------------|--------------------------|
| Magic     | fire, ice, lightning, dark, light           | Mage Resonance           |
| Physical  | slash, impact, guard                        | Swordsman Sword Energy (slash only) |

---

## 3. Element × Class Ability Interaction

### 3.1 Universal Rule

Any class can play any element's card. The card's base effects (damage, buffs, debuffs, healing) always apply regardless of the player's class.

### 3.2 Class Ability Activation by Element

| Class     | Ability        | Activating Element(s) | Non-Activating Elements                     |
|-----------|----------------|----------------------|---------------------------------------------|
| Mage      | Resonance      | fire, ice, lightning, dark, light | slash, impact, guard |
| Swordsman | Sword Energy gain | slash only          | impact, guard, fire, ice, lightning, dark, light |

### 3.3 Examples

- **Swordsman plays a fire card:** The card's damage and any burn effects apply normally. However, no Sword Energy is gained because the element is not `slash`.
- **Swordsman plays a slash card:** The card's damage applies AND Sword Energy is gained (per `swordEnergyGain` property).
- **Swordsman plays a guard card:** The card's defensive effects apply. No Sword Energy is gained (guard is not slash).
- **Mage plays a fire card, then another fire card:** Resonance level increases to 1, granting +15% damage and burn effect.
- **Mage plays a slash card:** The card's damage applies normally. No resonance is triggered (slash is not a magic element). If a resonance chain was active, it breaks.

---

## 4. Mage Resonance System

### 4.1 Core Mechanic

Resonance is built by playing cards of the **same magic element consecutively** within a single turn. Each consecutive same-element card increases the resonance level by 1, up to a maximum of 2.

```
State Transitions:
  (empty)         → Play fire card → lastElement=fire, resonanceLevel=0
  lastElement=fire → Play fire card → resonanceLevel=1 (Resonance!)
  resonanceLevel=1 → Play fire card → resonanceLevel=2 (Great Resonance!)
  resonanceLevel=2 → Play ice card  → lastElement=ice, resonanceLevel=0 (chain broken)
  any state       → Play slash card → lastElement=null, resonanceLevel=0 (chain broken)
```

Non-magic elements (slash, impact, guard) break the resonance chain entirely.

### 4.2 Resonance Levels

| Level | Name             | Damage Multiplier | Crit Bonus | Element Effect | Field Buff |
|-------|------------------|-------------------|------------|----------------|------------|
| 0     | —                | 1.00x             | 0%         | None           | None       |
| 1     | 共鳴 (Resonance) | 1.15x             | 0%         | Minor          | None       |
| 2     | 大共鳴 (Great Resonance) | 1.30x      | +10%       | Major          | Yes        |

### 4.3 Resonance Lifecycle

1. **Turn Start:** No change to resonance state.
2. **Card Play:** If the card's element is a magic element matching `lastElement`, increment `resonanceLevel` (max 2). If it's a different magic element, reset to 0 and update `lastElement`. If it's a non-magic element, reset entirely (`lastElement=null`, `resonanceLevel=0`).
3. **Damage Calculation:** Resonance multiplier applies only when the card's element matches `lastElement`.
4. **Turn End:** `resonanceLevel` resets to 0.

### 4.4 State Definition

```typescript
interface ElementalState {
  type: "elemental";
  lastElement: ElementType | null;
  resonanceLevel: ResonanceLevel; // 0 | 1 | 2
}
```

### 4.5 Damage Modifier

```typescript
{
  flatBonus: 0,
  percentMultiplier: RESONANCE_MULTIPLIER[resonanceLevel], // 1.0 | 1.15 | 1.30
  critBonus: resonanceLevel === 2 ? 0.10 : 0,
  penetration: 0,
}
```

---

## 5. Element-Specific Resonance Effects

When resonance reaches level 1 or 2, each magic element triggers unique secondary effects.

### 5.1 Fire

| Level | Effect                     |
|-------|----------------------------|
| 1     | Burn: 1 stack, 2 turns     |
| 2     | Burn: 2 stacks, 3 turns + `fireField` buff |

**Design Intent:** Pure offensive. Burn is a stacking DoT that rewards sustained fire chains.

### 5.2 Ice

| Level | Effect                     |
|-------|----------------------------|
| 1     | Freeze: 2 turns            |
| 2     | Freeze: 3 turns + `iceField` buff |

**Design Intent:** Control element. Freeze disables enemy actions, providing defensive value and setup time.

### 5.3 Lightning

| Level | Effect                     |
|-------|----------------------------|
| 1     | (none)                     |
| 2     | Stun: 1 turn + `electroField` buff |

**Design Intent:** High-risk, high-reward. No level 1 bonus, but level 2 grants stun. Synergizes with multi-hit cards (e.g., mg_018 "連鎖雷" with 4 hits).

### 5.4 Dark

| Level | Effect                     |
|-------|----------------------------|
| 1     | Lifesteal: 30%             |
| 2     | Lifesteal: 40% + Weakness: 3 turns + `darkField` buff |

**Design Intent:** Sustain element. Lifesteal provides self-healing proportional to damage dealt. Weakness at level 2 reduces enemy effectiveness.

### 5.5 Light

| Level | Effect                     |
|-------|----------------------------|
| 1     | Cleanse: remove 1 debuff   |
| 2     | Cleanse: remove 2 debuffs + Heal: 10 HP + `lightField` buff |

**Design Intent:** Support/defensive. The only element providing direct healing and debuff removal through resonance.

### 5.6 Field Buffs

| Element   | Field Buff      |
|-----------|-----------------|
| Fire      | `fireField`     |
| Ice       | `iceField`      |
| Lightning | `electroField`  |
| Dark      | `darkField`     |
| Light     | `lightField`    |

Field buffs are applied at resonance level 2 and persist beyond the turn they were activated.

---

## 6. Swordsman Sword Energy × Element

### 6.1 Rule

Sword Energy gain (the `swordEnergyGain` property on cards) is only activated when the card's element is `slash`. Cards with `impact` or `guard` elements do NOT generate Sword Energy, even when played by a Swordsman.

### 6.2 Design Rationale

This creates a deck-building tension for Swordsmen similar to the Mage's element concentration trade-off:
- **Slash-heavy deck:** Maximizes Sword Energy generation, enabling powerful energy-consuming finishers.
- **Mixed element deck:** Includes impact/guard/magic element utility cards for versatility, but at the cost of reduced Sword Energy flow.

### 6.3 Impact on Card Design

When designing Swordsman cards:
- `slash` element cards should be the primary Energy generators
- `impact` element cards provide raw power or special effects without Energy gain
- `guard` element cards provide defense without Energy gain
- Magic element cards usable by Swordsman offer utility (burn, freeze, etc.) but no Energy

---

## 7. Card Design Integration

### 7.1 Mage Card Distribution

40 Mage cards, 8 per magic element:

| Element   | Card IDs      | Key Feature                        |
|-----------|---------------|------------------------------------|
| Fire      | mg_001–mg_008 | Burn stacking, offensive           |
| Ice       | mg_009–mg_016 | Freeze, slow, defensive            |
| Lightning | mg_017–mg_024 | Multi-hit, penetration, stun       |
| Dark      | mg_025–mg_032 | Lifesteal, curse, weakness         |
| Light     | mg_033–mg_040 | Healing, cleanse, immunity         |

Each element set includes:
- **One 0-cost card:** Enables resonance building without energy cost
- **Attack cards:** Varying cost/power ratios
- **One guard card:** Defensive card that maintains resonance chain
- **Utility cards:** Buffs, debuffs, healing carrying the element tag

### 7.2 Deck Building Implications

**For Mage:**
- **Element Concentration (1–2 elements):** More reliable resonance, higher damage output. Limited utility range.
- **Element Diversity (3–5 elements):** Versatile toolkit (freeze + lifesteal + cleanse). Resonance rarely exceeds level 1.
- **0-cost cards as bridges:** Critical for maintaining resonance chains without burning energy.

**For Swordsman:**
- **Slash-focused:** Maximum Sword Energy generation. Consistent finisher access.
- **Mixed with magic/impact:** Access to burn, freeze, raw impact power. Reduced Energy generation.

### 7.3 Card Property: `elementalChainBonus`

The `Card` interface includes an optional `elementalChainBonus` field reserved for cards that provide extra bonus damage during active resonance chains. This allows individual cards to benefit more or less from resonance without changing system-wide multipliers.

---

## 8. Sanctuary Element Enhancement Nodes

### 8.1 Overview

Five new Mage-exclusive Tier 2 Sanctuary nodes, one per magic element, allow Mages to specialize in their preferred element's resonance effects.

### 8.2 Node Specifications

All nodes share:
- **Category:** `class`
- **Tier:** 2
- **Class Restriction:** `mage`
- **Prerequisite:** `mage_insight`
- **Cost:** 60 souls each

| Node ID                  | Name              | Icon | Effect                                        |
|--------------------------|-------------------|------|-----------------------------------------------|
| `fire_enhancement`       | Fire Mastery      | 🔥   | +1 burn stack at each resonance level          |
| `ice_enhancement`        | Ice Mastery       | ❄️   | +1 freeze duration at each resonance level     |
| `lightning_enhancement`  | Lightning Mastery | ⚡   | Adds stun effect at resonance level 1          |
| `dark_enhancement`       | Dark Mastery      | 🌑   | +10% lifesteal at each resonance level         |
| `light_enhancement`      | Light Mastery     | ✨   | +1 cleanse and +5 heal at each resonance level |

### 8.3 Enhanced vs Base Resonance Effects

**Fire:**

| Level | Base                       | Enhanced (with Fire Mastery)        |
|-------|----------------------------|-------------------------------------|
| 1     | Burn 1 stack, 2 turns      | Burn 2 stacks, 2 turns             |
| 2     | Burn 2 stacks, 3 turns + field | Burn 3 stacks, 3 turns + field |

**Ice:**

| Level | Base                  | Enhanced (with Ice Mastery)   |
|-------|-----------------------|-------------------------------|
| 1     | Freeze 2 turns        | Freeze 3 turns               |
| 2     | Freeze 3 turns + field | Freeze 4 turns + field       |

**Lightning:**

| Level | Base                  | Enhanced (with Lightning Mastery) |
|-------|-----------------------|-----------------------------------|
| 1     | (none)                | Stun 1 turn                      |
| 2     | Stun 1 turn + field   | Stun 1 turn + field (unchanged)  |

> Lightning Mastery fills the empty level 1 slot, making lightning chains valuable earlier. The level 2 effect remains unchanged since it already grants stun.

**Dark:**

| Level | Base                              | Enhanced (with Dark Mastery)           |
|-------|-----------------------------------|----------------------------------------|
| 1     | Lifesteal 30%                     | Lifesteal 40%                          |
| 2     | Lifesteal 40% + weakness + field  | Lifesteal 50% + weakness + field       |

**Light:**

| Level | Base                           | Enhanced (with Light Mastery)           |
|-------|--------------------------------|-----------------------------------------|
| 1     | Cleanse 1                      | Cleanse 2 + Heal 5                     |
| 2     | Cleanse 2 + Heal 10 + field   | Cleanse 3 + Heal 15 + field            |

### 8.4 Skill Tree Layout

The 5 element nodes fan out from `mage_insight` in the class branch (240–300 degrees):

```
                    mage_insight (Tier 1)
                         |
          +------+-------+-------+------+
          |      |       |       |      |
        fire   ice   lightning  dark  light
       (Tier 2) ...    ...     ...   (Tier 2)
```

### 8.5 Implementation Status

> **Implemented:** All 5 element enhancement nodes exist in `src/domain/camps/data/SanctuaryData.ts` as Tier 2 nodes (IDs: `fire_enhancement`, `ice_enhancement`, `lightning_enhancement`, `dark_enhancement`, `light_enhancement`). Each costs 60 souls and requires `mage_insight`.

### 8.6 Soul Cost Analysis

Total cost for one enhanced element: `mage_insight` (30) + element node (60) = **90 souls**.
This positions element specialization as a mid-game investment.

---

## 9. What Elements Do NOT Do

1. **No Weakness/Resistance Relationships.** No element has an advantage or disadvantage against any other element or enemy type.
2. **No Enemy Elemental Typing.** Enemies do not have element attributes that interact with card elements.
3. **No Elemental Resistance Stats.** Neither players nor enemies have per-element damage reduction.
4. **No Cross-Element Reactions.** Playing fire then ice does not trigger special combination effects.
5. **No Resonance for Non-Mage Classes.** Swordsman class abilities are independent of the resonance system. Even if they play consecutive same-magic-element cards, no resonance effects occur.

---

## 10. Architecture Summary

### 10.1 Data Flow

```
Card.element (universal tag on every card)
    │
    ├── Always → Card base effects apply (damage, buffs, debuffs)
    │
    ├── If Mage + Magic element → ElementalSystem.onCardPlay()
    │       → Updates ElementalState (lastElement, resonanceLevel)
    │       → getDamageModifier() returns resonance multiplier
    │       → getResonanceEffects() returns element-specific effects
    │           (optionally enhanced by Sanctuary nodes)
    │       → Effects applied as BuffDebuffState entries
    │
    └── If Swordsman + Slash element → swordEnergyGain activates
            → Sword Energy state updated
```

### 10.2 Key Files

| File | Purpose |
|------|---------|
| `src/types/characterTypes.ts` | `ElementType`, `ElementalState`, `ResonanceLevel` |
| `src/types/cardTypes.ts` | `Card.element` field |
| `src/types/battleTypes.ts` | `BuffDebuffType` including field buffs |
| `src/types/campTypes.ts` | `SanctuaryEffects`, `SkillNode`, `NodeEffectType` |
| `src/constants/characterConstants.ts` | `RESONANCE_MULTIPLIER`, `RESONANCE_EFFECTS` |
| `src/constants/cardConstants.ts` | `ELEMENT_ICON_MAP`, `ELEMENT_LABEL_MAP`, `ELEMENT_COLOR_MAP` |
| `src/domain/characters/player/logic/elementalSystem.ts` | `ElementalSystem` core logic |
| `src/domain/battles/managements/useElementalChain.ts` | React hook for battle integration |
| `src/domain/camps/data/SanctuaryData.ts` | Skill tree node definitions |
| `src/domain/camps/logic/sanctuaryLogic.ts` | `calculateTotalEffects()` |

### 10.3 Sanctuary Integration

To support element enhancement nodes, `SanctuaryEffects` needs an `enhancedElements` field tracking which magic elements have been enhanced. The `getResonanceEffects()` function accepts this as a parameter to return upgraded effect configs.

```typescript
// Proposed extension to SanctuaryEffects
interface SanctuaryEffects {
  // ... existing fields ...
  enhancedElements: Set<ElementType>;
}
```
