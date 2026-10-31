// Port of unity-port/BattleCore/Traits.cs
//
// Records here take C# named arguments the same way Types.js's do: a trailing plain object whose
// keys are all parameter names (`new TraitContext({ Gap: 2 })`, `new TraitOutcome(true, { PowerBonus: 3 })`).
// Omitted parameters take their C# default; `with({...})` is the C# `with` expression.
//
// Import-cycle note: this module reads only cs.js at module-evaluation time (TraitOutcome.None is
// built from literals); Constants / Types are read inside functions.

import {
  defineRecord,
  INT_MAX,
  ArgumentNullException,
  ArgumentOutOfRangeException,
} from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { TraitCondition, TraitEffect } from './Types.js';

// ------------------------------------------------------------------ local helpers (not C# types)

/** Array.Empty<T>(): one shared, frozen empty list. */
const EMPTY = Object.freeze([]);

function isPlainObject(x) {
  return x !== null && typeof x === 'object' && Object.getPrototypeOf(x) === Object.prototype;
}

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function argumentOutOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

/** defineRecord + C# named arguments (same convention as Types.js). */
function record(name, fields) {
  const R = defineRecord(name, fields);
  const C = class extends R {
    constructor(...args) {
      let named = null;
      if (args.length > 0 && isPlainObject(args[args.length - 1])) {
        const last = args[args.length - 1];
        const keys = Object.keys(last);
        const known = keys.filter((k) => C.$fields.includes(k));
        if (keys.length > 0 && known.length === keys.length) {
          named = last;
          args = args.slice(0, -1);
        } else if (known.length > 0) {
          const unknown = keys.filter((k) => !C.$fields.includes(k));
          throw new Error(`${name}: unknown named argument(s) ${unknown.join(', ')}`);
        }
      }
      super(...args);
      if (named) {
        for (const k of Object.keys(named)) {
          if (named[k] !== undefined) this[k] = named[k];
        }
      }
    }
  };
  return C;
}

// ------------------------------------------------------------------ TraitContext

/**
 * What the board looked like when a card or action was played, as far as a trait can see it.
 *
 * Gap is the distance N to the opponent before the card resolves (§2.3: 出す前の N).
 * StaminaAfterUse is the stamina left once the cost is paid, which is what 温存 reads;
 * StaminaBefore is the stamina before paying, which is what 死力 reads.
 *
 * #188 widened it to the twelve conditions (v4.4: thirteen, with 移動後): Played is what the one playing has played earlier
 * this turn, in order (連動 / 初手 / 締め / 連打); HandAfterPlay is the hand left once the card
 * has left it (手薄); OpponentOmen is the standing omen of the enemy the card reads (予兆);
 * the two status sets are for 相手の状態 / 自分の状態; Attributes is the one attribute the card counts as (folded, §2.1), which 連打
 * holds against the card before it. The defaults of the fields #188 added make their conditions
 * fail (死力, 崩し後, 手薄, 予兆, the statuses, 連動, 連打); the first three keep the slice's
 * zeros, so an empty context still reads gap 0 (間合い n 以下), Guard 0 (無防備) and nothing
 * played yet (初手).
 *
 * BattleAttribute.None is 0 (a [Flags] value), written as a literal so nothing from Types.js is
 * read at module-evaluation time.
 */
export class TraitContext extends record('TraitContext', [
  ['Gap', 0],
  ['OpponentGuard', 0],
  ['StaminaAfterUse', 0],
  ['StaminaBefore', INT_MAX],
  ['OpponentStamina', INT_MAX],
  ['Played', null],
  ['HandAfterPlay', INT_MAX],
  ['OpponentOmen', null],
  ['SelfStatuses', null],
  ['OpponentStatuses', null],
  ['Attributes', 0 /* BattleAttribute.None */],
  ['Moved', false],
]) {}

// ------------------------------------------------------------------ TraitOutcome

/**
 * The bonuses the traits of one card or action contributed. All zero when no condition held.
 * Grants are the statuses the Status effect gives; Convert is 転換 (worked out by the turn loop,
 * which knows the Guard face).
 */
export class TraitOutcome extends record('TraitOutcome', [
  'Triggered',
  ['PowerBonus', 0],
  ['GuardBonus', 0],
  ['NextTurnRecoveryBonus', 0],
  ['StaminaGain', 0],
  ['Draw', 0],
  ['Grants', null],
  ['CostDown', 0],
  ['Convert', false],
  ['FollowUp', 0],
  ['BreakBonus', 0],
]) {
  get GrantList() {
    return this.Grants ?? EMPTY;
  }

  /** Two outcomes added up (背水の陣 carries two traits, both judged at once). */
  Plus(other) {
    if (other == null) throw argumentNull('other');
    const grants = [...this.GrantList];
    grants.push(...other.GrantList);
    return new TraitOutcome(
      this.Triggered || other.Triggered,
      this.PowerBonus + other.PowerBonus,
      this.GuardBonus + other.GuardBonus,
      this.NextTurnRecoveryBonus + other.NextTurnRecoveryBonus,
      this.StaminaGain + other.StaminaGain,
      this.Draw + other.Draw,
      grants.length === 0 ? null : grants,
      this.CostDown + other.CostDown,
      this.Convert || other.Convert,
      this.FollowUp + other.FollowUp,
      this.BreakBonus + other.BreakBonus,
    );
  }
}
/** public static readonly TraitOutcome None = new TraitOutcome(false); */
TraitOutcome.None = new TraitOutcome(false);

// ------------------------------------------------------------------ Traits

/** C# `private static TraitOutcome Apply(Trait trait)` of Traits. */
function apply(trait) {
  switch (trait.Effect) {
    case TraitEffect.PowerBonus: return new TraitOutcome(true, { PowerBonus: trait.Amount });
    case TraitEffect.GuardBonus: return new TraitOutcome(true, { GuardBonus: trait.Amount });
    case TraitEffect.NextTurnRecovery: return new TraitOutcome(true, { NextTurnRecoveryBonus: trait.Amount });
    case TraitEffect.HeavyBlow: return new TraitOutcome(true, {
      PowerBonus: Constants.HeavyBlowPower,
      NextTurnRecoveryBonus: -Constants.HeavyBlowRecoveryPenalty,
    });
    case TraitEffect.StaminaGain: return new TraitOutcome(true, { StaminaGain: trait.Amount });
    case TraitEffect.Draw: return new TraitOutcome(true, { Draw: trait.Amount });
    case TraitEffect.Status: return new TraitOutcome(true, { Grants: trait.Grant != null ? [trait.Grant] : null });
    case TraitEffect.CostDown: return new TraitOutcome(true, { CostDown: trait.Amount });
    case TraitEffect.Convert: return new TraitOutcome(true, { Convert: true });
    case TraitEffect.FollowUp: return new TraitOutcome(true, { FollowUp: trait.Amount });
    case TraitEffect.BreakBonus: return new TraitOutcome(true, { BreakBonus: trait.Amount });
    default: throw argumentOutOfRange('trait', trait.Effect, 'Unknown trait effect.');
  }
}

/**
 * §2.3 / §17.6 F5: the trait is judged once, before any face resolves, because the card tables
 * and the predicted numbers on screen are written for that order.
 *
 * The twelve conditions and ten effects (#188) are rows of the two switches below; the demo
 * carries them at the precision a playable battle needs, and #48 keeps the tests that pin every
 * pairing.
 */
export const Traits = Object.freeze({
  Evaluate(trait, context) {
    if (context == null) throw argumentNull('context');
    if (trait == null) return TraitOutcome.None;
    return Traits.Holds(trait, context) ? apply(trait) : TraitOutcome.None;
  },

  /** Every trait of one card, judged on the same board; the outcomes add up. */
  EvaluateAll(traits, context) {
    if (traits == null) throw argumentNull('traits');
    let total = TraitOutcome.None;
    for (const trait of traits) total = total.Plus(Traits.Evaluate(trait, context));
    return total;
  },

  /** §2.3: a gap condition reads N from before the card is played (before any move face). */
  Holds(trait, context) {
    if (trait == null) throw argumentNull('trait');
    if (context == null) throw argumentNull('context');
    const played = context.Played ?? EMPTY;
    switch (trait.Condition) {
      case TraitCondition.GapAtMost: return context.Gap <= trait.Threshold;
      case TraitCondition.GapAtLeast: return context.Gap >= trait.Threshold;
      case TraitCondition.Unguarded: return context.OpponentGuard === 0;
      case TraitCondition.Reserve: return context.StaminaAfterUse >= trait.Threshold;
      case TraitCondition.Desperate: return context.StaminaBefore <= Constants.DesperateThreshold;
      case TraitCondition.FirstPlay: return played.length === 0;
      case TraitCondition.Finisher: return played.length + 1 >= Constants.FinisherPlayNumber;
      case TraitCondition.Broken: return context.OpponentStamina < Constants.BrokenBelow;
      case TraitCondition.Thin: return context.HandAfterPlay <= Constants.ThinHandMax;
      // C# `trait.Omen.HasValue && context.OpponentOmen == trait.Omen` (lifted nullable equality).
      case TraitCondition.OmenIs: return trait.Omen != null && context.OpponentOmen === trait.Omen;
      case TraitCondition.FoeHas:
        return trait.Watch != null && context.OpponentStatuses != null && context.OpponentStatuses.Has(trait.Watch);
      case TraitCondition.SelfHas:
        return trait.Watch != null && context.SelfStatuses != null && context.SelfStatuses.Has(trait.Watch);
      case TraitCondition.Moved: return context.Moved;
      case TraitCondition.Combo:
        for (const attributes of played) {
          if ((attributes & trait.Attribute) !== 0) return true;
        }
        return false;
      case TraitCondition.Chain:
        return played.length > 0 && (played[played.length - 1] & context.Attributes) !== 0;
      default:
        throw argumentOutOfRange('trait', trait.Condition, 'Unknown trait condition.');
    }
  },
});
