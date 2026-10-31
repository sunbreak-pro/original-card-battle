// Port of unity-port/BattleCore/Chain.cs
//
// BattleTally.Attributes is a Map<BattleAttribute, int> (C# IReadOnlyDictionary; [Flags] values are
// numbers). The C# tuples `(int Hp, int Stamina)` are returned as `{ Hp, Stamina }`.
//
// Import-cycle note: BattleTally.Order reads BattleAttribute's numbers as literals (Attack 1, Guard 2,
// Skill 4, Stance 8) so nothing from Types.js is read at module-evaluation time; the Chain functions
// read Types / BattleEvents / Constants only when called.

import {
  defineRecord,
  idiv,
  roundAway,
  ArgumentNullException,
  ArgumentException,
} from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { Actor, BattleAttribute } from './Types.js';
import { CardPlayed, TraitEvaluated } from './BattleEvents.js';

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentException(message, paramName).Message. */
function argument(message, param) {
  return new ArgumentException(`${message} (Parameter '${param}')`);
}

// ------------------------------------------------------------------ BattleTally

/**
 * What one finished battle adds up to (§12 の結果画面, #191), counted from its event stream: who
 * won, in how many turns, the HP left, the attributes of the cards played and how often a card's
 * trait held. A card counts once, as the one attribute it folds to (battle_core_v4 §2.1 / §24.1,
 * v4.4: 攻撃 ＞ 防御 ＞ スキル, AttributeRule.Fold), so the attribute counts add up
 * to CardsPlayed — an 攻撃 + 防御 card is one 攻撃, not one of each.
 */
export class BattleTally extends defineRecord('BattleTally', [
  'EnemyId',
  'Result',
  'Turns',
  'HpLeft',
  'MaxHp',
  'CardsPlayed',
  'Attributes',
  'TraitsFired',
]) {
  /** C# `Attributes.TryGetValue(attribute, out int n) ? n : 0`. */
  CountOf(attribute) {
    return this.Attributes.has(attribute) ? this.Attributes.get(attribute) : 0;
  }
}

/**
 * The four attributes (§2.1), for a breakdown printed the same way every time.
 * BattleAttribute.Attack, .Guard, .Skill, .Stance ([Flags]: 1, 2, 4, 8).
 */
BattleTally.Order = Object.freeze([1, 2, 4, 8]);

// ------------------------------------------------------------------ Chain

/**
 * §12 連戦モード, the demo's first shape (#191): what carries from one battle to the next, the
 * rest that may come between, and the tally of each battle. The order of the battles and the
 * choices are the demo flow's (Depiction.Bridge.DemoSession); this holds the rules.
 *
 * Carried: HP and current stamina. Not carried (2026-09-23): the order of the draw and discard
 * piles — the deck is shuffled afresh each battle — nor statuses, Guard or the stance slot.
 */
export const Chain = Object.freeze({
  /** The HP and current stamina a finished battle hands the next one (BattleSetup.PlayerStartHp / PlayerStartStamina). */
  Carry(finished) {
    if (finished == null) throw argumentNull('finished');
    return { Hp: finished.Player.Hp, Stamina: finished.Player.Stamina };
  },

  /**
   * §12 / §3.1 階層間の休憩: HP back by 30% of the maximum (rounded away from zero) up to the
   * maximum, and stamina full.
   */
  Rest(hp, maxHp, maxStamina) {
    // C#: maxHp * ChainRestHpPercent is an unchecked int product, then / 100.0 in double.
    const gain = Math.trunc(roundAway(Math.imul(maxHp, Constants.ChainRestHpPercent) / 100.0));
    return { Hp: Math.min(maxHp, (hp + gain) | 0), Stamina: maxStamina };
  },

  /**
   * §12's result screen for a whole chain: the cards, their attributes and the traits that
   * held, added up over every battle; turns summed; the result and the HP of the last battle.
   */
  Total(tallies) {
    if (tallies == null) throw argumentNull('tallies');
    if (tallies.length === 0) throw argument('A chain total needs at least one battle.', 'tallies');
    const attributes = new Map();
    let turns = 0;
    let played = 0;
    let fired = 0;
    for (const tally of tallies) {
      turns += tally.Turns;
      played += tally.CardsPlayed;
      fired += tally.TraitsFired;
      for (const attribute of BattleTally.Order) {
        const n = tally.CountOf(attribute);
        if (n === 0) continue;
        const sum = attributes.has(attribute) ? attributes.get(attribute) : 0;
        attributes.set(attribute, sum + n);
      }
    }
    const last = tallies[tallies.length - 1];
    return new BattleTally('', last.Result, turns, last.HpLeft, last.MaxHp, played, attributes, fired);
  },

  // ---- 瘴気 and the max stamina (concept-v3 §6) ----

  /** concept-v3 §6: 100% is 瘴気死, the end of the life. The gauge never reads more. */
  MiasmaMax: 100,

  /** concept-v3 §6: one point of max stamina per full 20%. */
  MiasmaStepPercent: 20,

  /** battle_core_v4 §13: the penalty stops at −4 (max stamina 6 at 80%). */
  MiasmaMaxPenalty: 4,

  /**
   * concept-v3 §6 / battle_core_v4 §1: the max stamina a battle starts from at this gauge —
   * BASE_MAX_STAMINA less 1 per full 20% (at most 4), kept to 3〜14. The same rule as the
   * exploration side's (DungeonCore.Miasma.MaxStamina with no temporary modifier).
   */
  MaxStaminaAt(miasmaPercent) {
    const clamped = Math.max(0, Math.min(Chain.MiasmaMax - 1, miasmaPercent));
    const penalty = Math.min(Chain.MiasmaMaxPenalty, idiv(clamped, Chain.MiasmaStepPercent));
    return Math.max(Constants.MaxStaminaFloor, Math.min(Constants.MaxStaminaCeil, Constants.BaseMaxStamina - penalty));
  },

  /** The tally of one battle, from its final state and every event it emitted (any iterable). */
  Tally(finished, events) {
    if (finished == null) throw argumentNull('finished');
    if (events == null) throw argumentNull('events');

    const attributes = new Map();
    let played = 0;
    let fired = 0;
    for (const e of events) {
      if (e instanceof CardPlayed && e.Actor === Actor.Player) {
        played++;
        // §2.1 (v4.4): a card is counted once, as the one attribute it folds to.
        const counted = e.Card.Def.Attribute;
        if (counted !== BattleAttribute.None) {
          const n = attributes.has(counted) ? attributes.get(counted) : 0;
          attributes.set(counted, n + 1);
        }
      } else if (e instanceof TraitEvaluated && e.Actor === Actor.Player && e.Outcome.Triggered) {
        fired++;
      }
    }
    return new BattleTally(
      finished.EnemyDef.Id, finished.Result, finished.Turn, finished.Player.Hp, finished.Player.MaxHp,
      played, attributes, fired);
  },
});
