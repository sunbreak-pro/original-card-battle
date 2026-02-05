# Complete Design: New Character System

## Overall Concept

### Highly Flexible Build System

* Playstyles change based on card combinations.
* Allows for diverse builds such as High-Speed, Counter-based, or Heavy-Tank types.
* Differentiation through unique specifications and abilities for each character.

### Proficiency and Evolution System

```
Proficiency increases based on the number of times a card is used.
Develop from Lv0 → Lv4.
Unlock Talent Cards at Lv4 (Acquire new cards).

【Proficiency Levels and Thresholds】
Lv0: 0 uses (Initial state)
Lv1: 8 uses
Lv2: 16 uses
Lv3: 24 uses
Lv4: 30 uses → Talent Card Unlocked

【Proficiency Bonus (Damage Multiplier)】
Lv0: 1.0x
Lv1: 1.2x
Lv2: 1.4x
Lv3: 2.0x
Lv4: 2.5x

```

### Title System

```
Titles change based on the number of cards acquired.

【Swordsman Class】
0 cards: Apprentice Swordsman
5 cards: Swordsman
15 cards: Swordmaster
30 cards: Blade Saint
50 cards: Sword God

【Sorcerer Class】
0 cards: Apprentice Sorcerer
5 cards: Sorcerer
15 cards: Mage
30 cards: Archmage
50 cards: Demon God

【Coming Soon】
...

```

---

## Character 1: Blade Master

### Basic Concept

**"A frontline warrior who overwhelms with multi-hits and momentum."**

* Simple build.
* Deals massive damage by consuming Sword Energy.

### Base Stats

```
HP: 100
Attack (ATK): +30%
Defense (DEF): +10%
Magic Correction: +0%
Initial Energy: 3

```

### Unique Ability: 【Sword Energy】

#### Basic Specifications

```
【Sword Energy Gauge】
Max Value: 10
Initial Value: 0

【Turn Start Effect】
- +1 Sword Energy at the start of each turn (applies from turn 1)
- Does not exceed maximum (10)

【Accumulating Sword Energy】
Sword Energy accumulates when using Physical Attack cards:
- 0 Cost: +1 Sword Energy
- 1 Cost: +1 Sword Energy
- 2 Cost: +2 or +3 Sword Energy
- 3+ Cost: +3 Sword Energy
- Dedicated Accumulation Card (1 Cost): +4 Sword Energy

Special Effects:
- 5+ Energy: Critical Rate +20%
- 8+ Energy: Physical attacks gain Piercing +30%
- 10 Energy (Max): Next physical attack is a Guaranteed Critical + Piercing 50%

Consumption Effects:
【Standard Consumption】
- Consumes 3 Energy: Mid-power technique
- Consumes 5 Energy: High-power technique

【Full Consumption】 (Example)
- Consumes all Energy (10): Ultra-high damage + Self Buffs + Enemy Debuffs

```

## Character 2: Spell Caster

### Basic Concept

**"A backline mage who toy with enemies using diverse magic."**

* Synergy through elemental combinations.
* Fights using status ailments.
* Strategic and calculated playstyle.

### Base Stats

```
HP: 80
Attack (ATK): +0%
Defense (DEF): +5%
Magic Correction: +40%
Initial Energy: 4

```

### Unique Ability: 【Mana Resonance】

#### Basic Specifications

```
【Mana Resonance System】
Resonance accumulates when using magic of the same element consecutively.
Resonance Level: 0 → 1 → 2 (Max) *Level 2 effects apply for any resonance beyond level 2.
Resonance resets upon using a different element.

【Resonance Accumulation】
- 1st use of same element: Resonance Level 0
- 2nd use of same element: Resonance Level 1
- 3rd use and beyond: Resonance Level 2 (Max)
- 4th use and beyond: Level 2 effects are triggered

【Resonance Level Effects】
Independent status ailments and power bonuses for each element.

Fire:
- Level 0: Base effects only
- Level 1: Burn Stack +1 / Fire Magic Power +15%
- Level 2: Burn Stack +2 / Fire Magic Power +30%

Ice:
- Level 0: Base effects only
- Level 1: Freeze (2 turns) / Ice Magic Power +15%
- Level 2: Freeze (3 turns) + Ice Field / Ice Magic Power +30%

Lightning:
- Level 0: Base effects only
- Level 1: (None) / Lightning Magic Power +15%
- Level 2: Stun (1 turn) + Electro Field / Lightning Magic Power +30%

Dark:
- Level 0: Base effects only
- Level 1: Lifesteal 30% / Dark Magic Power +15%
- Level 2: Lifesteal 40% + Weakness (3 turns) + Dark Field / Dark Magic Power +30%

Light:
- Level 0: Base effects only
- Level 1: Removes 1 Debuff / Light Magic Power +15%
- Level 2: Removes 2 Debuffs + HP 10 Recovery + Light Field / Light Magic Power +30%

```

## Unique Ability Comparison Table

| Item | Blade Master | Spell Caster |
| --- | --- | --- |
| Unique System | Sword Energy (Value) | Mana Resonance (2 Stages) |
| Max Value | 10 Sword Energy | Level 2 x 5 Elements |
| Accumulation Method | Use Physical Cards | Consecutive Same Element Use |
| Effect | Direct Damage Addition | Status Ailments + Power Up |
| Consumption | Full consumption on select cards | Reset upon changing elements |
| Strategy Level | Medium | High |
| Damage Calculation | Clear (Additive) | Complex (Multiplier + Status) |

---

## Next Steps

Once the unique abilities are finalized, we move to the exclusive card design for each character:

1. **Blade Master Exclusive Cards (40)**
* Accumulation types
* Consumption types
* Physical attack types
* Defense types


2. **Spell Caster Exclusive Cards (40)**
* Elemental Magic (8 per element)
* Resonance enhancement types
* Elemental combo types

Each class has approximately 40-60 exclusive cards.

*Current Implementation Status: Blade Master 60 cards, Spell Caster 40 cards = 100 cards total.*
