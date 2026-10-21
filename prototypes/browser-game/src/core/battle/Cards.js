// Port of unity-port/BattleCore/Cards.cs
//
// Import-cycle note: nothing from another module is read at module-evaluation time; Types.js,
// Constants, Columns and AttributeRule are read inside the functions.
//
// JS extension (not in the C#): Validate takes a trailing optional DeckLimits (docs/EXTENSIONS.md
// 「出立の枠（DeckLimits）」); without it, it is the C#'s Validate.

import {
  defineRecord,
  compareOrdinal,
  hasFlag,
  ArgumentNullException,
  ArgumentOutOfRangeException,
} from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { Columns } from './Columns.js';
import { AttributeRule } from './Attributes.js';
import { BattleAttribute, CardDef, CardInstance, EnemyActionDef } from './Types.js';

// ------------------------------------------------------------------ local helpers (not C# types)

/** Array.Empty<T>(): one shared, frozen empty list. */
const EMPTY = Object.freeze([]);

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function argumentOutOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

/** `.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)` over a list of strings. */
function distinctOrdinalSorted(ids) {
  return [...new Set(ids)].sort(compareOrdinal);
}

/** `GroupBy(key)` (groups in first-appearance order) → Map key → count. */
function countBy(items, key) {
  const counts = new Map();
  for (const item of items) {
    const k = key(item);
    counts.set(k, (counts.get(k) ?? 0) + 1);
  }
  return counts;
}

// ------------------------------------------------------------------ records

/** Where every card sits after a draw. Nothing is lost: the three piles plus the hand always add up. */
export class DrawResult extends defineRecord('DrawResult', [
  'Hand',
  'DrawPile',
  'DiscardPile',
  'Drawn',
  'OverflowToDiscard',
  'Reshuffled',
]) {}

/** Why a deck was refused, or nothing if it passes. Checked at 出立 (#58), not mid-battle. */
export class DeckValidation extends defineRecord('DeckValidation', ['Ok', 'Errors']) {}

// ------------------------------------------------------------------ Cards

/**
 * §2.4 / §4 multi-hit check over the declared attributes, the face and the traits:
 * C# `MultiHitCarriesNoFoeStatus(BattleAttribute attributes, Face face, IReadOnlyList<Trait> traits)`.
 */
function multiHitRule(attributes, face, traits) {
  if (face == null) throw argumentNull('face');
  if (traits == null) throw argumentNull('traits');
  if (!hasFlag(attributes, BattleAttribute.Attack) || face.Hits < 2) return true;
  if (face.GivesFoeStatus) return false;
  for (const trait of traits) {
    if (trait.Grant != null && !trait.Grant.OnSelf) return false;
  }
  return true;
}

/**
 * §8: building a deck, shuffling it, drawing the turn's hand, and throwing the hand away.
 *
 * Every shuffle goes through IRng, so the same seed replays the same battle. There
 * is no other source of randomness in the core.
 */
export const Cards = Object.freeze({
  /**
   * Lays out copies of each definition in order, with instance ids of the form "thrust-0".
   * Order is deterministic; shuffling is a separate step so a test can skip it.
   */
  BuildDeck(defs, copies) {
    if (defs == null) throw argumentNull('defs');
    if (copies < 1) throw argumentOutOfRange('copies', copies, 'At least one copy.');

    const deck = [];
    for (const def of defs) {
      for (let copy = 0; copy < copies; copy++) {
        deck.push(new CardInstance(`${def.Id}-${copy}`, def));
      }
    }
    return deck;
  },

  /** Fisher-Yates, driven only by the injected RNG. */
  Shuffle(input, rng) {
    if (input == null) throw argumentNull('input');
    if (rng == null) throw argumentNull('rng');

    const arr = [...input];
    for (let i = arr.length - 1; i > 0; i--) {
      let j = Math.floor(rng.NextDouble() * (i + 1));
      if (j > i) j = i;
      const t = arr[i];
      arr[i] = arr[j];
      arr[j] = t;
    }
    return arr;
  },

  /**
   * §9 step 4: draw the turn's cards. An empty draw pile is refilled by reshuffling the
   * discard pile (§8); with nothing left in either, the draw simply comes up short.
   *
   * §17.6 F8: anything that would push the hand past 8 goes straight to the discard pile.
   * With a flat 5 and an empty hand that never fires, but the rule is in place for when
   * draw bonuses arrive.
   */
  Draw(drawPile, discardPile, hand, count, rng) {
    if (drawPile == null) throw argumentNull('drawPile');
    if (discardPile == null) throw argumentNull('discardPile');
    if (hand == null) throw argumentNull('hand');
    if (rng == null) throw argumentNull('rng');

    let draw = [...drawPile];
    let discard = [...discardPile];
    const newHand = [...hand];
    let drawn = 0;
    let overflow = 0;
    let reshuffled = false;

    for (let i = 0; i < count; i++) {
      if (draw.length === 0) {
        if (discard.length === 0) break;
        draw = Cards.Shuffle(discard, rng);
        discard = [];
        reshuffled = true;
      }

      const next = draw[0];
      draw.splice(0, 1);
      drawn++;

      if (newHand.length >= Constants.HandLimit) {
        discard.push(next);
        overflow++;
      } else {
        newHand.push(next);
      }
    }

    return new DrawResult(newHand, draw, discard, drawn, overflow, reshuffled);
  },

  /**
   * §9 step 8: the whole hand goes to the discard pile. Nothing is kept for next turn.
   * C# returns the tuple `(Hand, DiscardPile)`; JS returns `{ Hand, DiscardPile }`.
   */
  DiscardHand(hand, discardPile) {
    if (hand == null) throw argumentNull('hand');
    if (discardPile == null) throw argumentNull('discardPile');

    const discard = [...discardPile];
    discard.push(...hand);
    return { Hand: [], DiscardPile: discard };
  },

  /**
   * §8: whether a card is a stance card — the attribute it counts as (§2.1). The same test
   * sends a played stance card to the exile pile (§4).
   */
  IsStanceCard(def) {
    if (def == null) throw argumentNull('def');
    return def.Attribute === BattleAttribute.Stance;
  },

  /**
   * Three C# overloads in one JS method:
   *
   * - `MultiHitCarriesNoFoeStatus(CardDef def)` — §2.4 hits (#253, and for enemy actions v4.4):
   *   a face that strikes two or more times carries no status for the opponent (出血 / 脆化 / 鈍足 /
   *   威圧 / 疲労), neither on the face nor through a trait. Statuses on the one playing are
   *   allowed, and so is a stance that gives one on every blow (StanceHook.StatusOnAttack).
   * - `MultiHitCarriesNoFoeStatus(EnemyActionDef action)` — the same rule for an enemy action
   *   (v4.4, #256 の 3).
   * - `MultiHitCarriesNoFoeStatus(BattleAttribute attributes, Face face, IReadOnlyList<Trait> traits)`.
   *
   * Dispatch: two or more arguments (or a number first) is the third; otherwise an EnemyActionDef
   * is the second and anything else the first. A lone null throws on `def`, the CardDef
   * overload's parameter.
   */
  MultiHitCarriesNoFoeStatus(...args) {
    if (args.length >= 2 || typeof args[0] === 'number') {
      return multiHitRule(args[0], args[1], args[2]);
    }
    const one = args[0];
    if (one instanceof EnemyActionDef) {
      const action = one;
      return multiHitRule(action.Attributes, action.Face, action.Trait == null ? EMPTY : [action.Trait]);
    }
    if (one == null) throw argumentNull('def');
    if (!(one instanceof CardDef) && !('AllTraits' in one)) {
      // Something shaped like an enemy action (no AllTraits): read it as one.
      return multiHitRule(one.Attributes, one.Face, one.Trait == null ? EMPTY : [one.Trait]);
    }
    const def = one;
    return multiHitRule(def.Attributes, def.Face, def.AllTraits);
  },

  /**
   * §4 (v4.4): a stance stands alone. A card that declares スタンス declares no other attribute
   * and does not move, and a card that carries a stance face declares スタンス. All fourteen
   * stance cards of the catalog pass since #333 wrote the ten #257 rewrote as stance only.
   */
  StanceStandsAlone(def) {
    if (def == null) throw argumentNull('def');
    return AttributeRule.StanceStandsAlone(def.Attributes, def.Face);
  },

  /**
   * §2.4 / §4 for an enemy action (v4.4): the multi-hit rule, the stance standing alone, and a
   * column that has a cost. Returns the reasons it is refused, empty when it passes. Nothing
   * in the core calls it during a battle; the roster test does.
   */
  ValidateEnemyAction(action) {
    if (action == null) throw argumentNull('action');
    const errors = [];
    if (!Columns.IsValid(action.Column)) {
      errors.push(`Action "${action.Id}" sits outside columns ${Columns.Min}..${Columns.Max}.`);
    }
    if (!AttributeRule.StanceStandsAlone(action.Attributes, action.Face)) {
      errors.push(`Action "${action.Id}" is a stance and also declares another attribute or moves.`);
    }
    if (!multiHitRule(action.Attributes, action.Face, action.Trait == null ? EMPTY : [action.Trait])) {
      errors.push(`Action "${action.Id}" strikes more than once and gives the opponent a status.`);
    }
    return errors;
  },

  /**
   * §8: a deck holds 20 to 40 cards and at most 3 of any one kind. There is no limit on stance
   * cards (v4.4; §19.6 S15's three-card cap is gone). Columns are checked too, because a card
   * outside 1..4 has no cost, and so is the multi-hit rule (#253) and the stance standing alone (§4).
   *
   * JS extension (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」): `limits` is a DeckLimits (DeckRules.js)
   * whose DeckMin / DeckMax replace the 20 and 40 — concept-v3.md §14 生存者ボーナス 「デッキ上限 +5」
   * lets a survivor's heir set out with up to 45. Left out (or null) it is DeckLimits.Default, the C#
   * constants. Read by its two numbers so this module does not import DeckRules.js (DeckRules imports
   * Cards; the cycle stays one way).
   */
  Validate(deck, limits = null) {
    if (deck == null) throw argumentNull('deck');

    const errors = [];
    const deckMin = limits?.DeckMin ?? Constants.DeckMin;
    const deckMax = limits?.DeckMax ?? Constants.DeckMax;

    if (deck.length < deckMin) {
      errors.push(`Deck has ${deck.length} cards; the minimum is ${deckMin}.`);
    } else if (deck.length > deckMax) {
      errors.push(`Deck has ${deck.length} cards; the maximum is ${deckMax}.`);
    }

    // deck.GroupBy(c => c.Def.Id).OrderBy(g => g.Key, StringComparer.Ordinal)
    const byKind = countBy(deck, (c) => c.Def.Id);
    for (const key of [...byKind.keys()].sort(compareOrdinal)) {
      const count = byKind.get(key);
      if (count > Constants.CopiesMax) {
        errors.push(`Deck holds ${count} copies of "${key}"; the maximum is ${Constants.CopiesMax}.`);
      }
    }

    const defs = deck.map((c) => c.Def);

    for (const id of distinctOrdinalSorted(defs.filter((d) => !Cards.StanceStandsAlone(d)).map((d) => d.Id))) {
      errors.push(`Card "${id}" is a stance and also declares another attribute or moves; a stance stands alone.`);
    }

    for (const id of distinctOrdinalSorted(defs.filter((d) => !Columns.IsValid(d.Column)).map((d) => d.Id))) {
      errors.push(`Card "${id}" sits outside columns ${Columns.Min}..${Columns.Max}.`);
    }

    for (const id of distinctOrdinalSorted(defs.filter((d) => !Cards.MultiHitCarriesNoFoeStatus(d)).map((d) => d.Id))) {
      errors.push(`Card "${id}" strikes more than once and gives the opponent a status.`);
    }

    // deck.GroupBy(c => c.InstanceId, Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).OrderBy(id => id, Ordinal)
    const byInstance = countBy(deck, (c) => c.InstanceId);
    const duplicateIds = [...byInstance.keys()].filter((k) => byInstance.get(k) > 1).sort(compareOrdinal);
    for (const instanceId of duplicateIds) {
      errors.push(`Instance id "${instanceId}" appears more than once.`);
    }

    return new DeckValidation(errors.length === 0, errors);
  },
});
