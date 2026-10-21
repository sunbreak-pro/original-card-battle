// Port of unity-port/BattleCore/DeckRules.cs
//
// Departure takes C# named arguments the way Types.js's records do: a trailing plain object whose
// keys are all parameter names (`new Departure(deck, tools, consumables, 0, talent, { StartStaminaBonus: 3 })`).
//
// IReadOnlyCollection<string> ownedKinds: any iterable of ids (an array or a Set).
//
// Import-cycle note: only Constants (a leaf) is read at module-evaluation time (the
// StartCellsBackMax constant); BattleSetup (TurnLoop.js) and Cards are read inside functions.
//
// JS extension (not in the C#), docs/EXTENSIONS.md 「出立の枠（DeckLimits）」: DeckLimits, at the end of
// this file, carries the four numbers a departure is checked against. Every check below takes it as a
// trailing optional argument; left out (or null) it is DeckLimits.Default, the C# constants, so a
// call written like the C# behaves exactly like the C#.

import {
  defineRecord,
  compareOrdinal,
  ArgumentNullException,
  ArgumentOutOfRangeException,
} from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { Cards, DeckValidation } from './Cards.js';
import { BattleSetup } from './TurnLoop.js';

// ------------------------------------------------------------------ local helpers (not C# types)

/** Array.Empty<string>(): one shared, frozen empty list. */
const EMPTY = Object.freeze([]);

function isPlainObject(x) {
  return x !== null && typeof x === 'object' && Object.getPrototypeOf(x) === Object.prototype;
}

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentNullException(paramName, message).Message. */
function argumentNullWith(param, message) {
  return new ArgumentNullException(`${message} (Parameter '${param}')`);
}

/** IReadOnlyCollection<string>.Contains (LINQ Enumerable.Contains on any collection). */
function contains(collection, id) {
  if (collection instanceof Set) return collection.has(id);
  if (Array.isArray(collection)) return collection.includes(id);
  for (const x of collection) if (x === id) return true;
  return false;
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

// ------------------------------------------------------------------ Departure

/**
 * What 出立 (#58) hands the battles of one life: the deck, the ids in the tool and consumable
 * slots, how many cells a tool moves the player's start back, the talent card, and what the
 * other battle tools add (#452).
 *
 * The core reads the deck, the start, the talent and the amounts. It does not read the tool and
 * consumable ids: what each one does is the dungeon side's catalogue (tools_and_consumables_v4.md,
 * unity-port/DungeonContent), and the four battle effects reach the core as amounts summed over
 * the slots (the same tool may fill two): 間合いの履 as StartCellsBack, 息継ぎの笛
 * as StartStaminaBonus, 竜鱗の欠片 as FirstHitGuard and 竜語の符 as
 * BossWordWard (§9.1). The ids are kept so the slots can be checked and shown.
 */
export class Departure extends record('Departure', [
  'Deck',
  'Tools',
  'Consumables',
  'StartCellsBack',
  'TalentCardId',
  ['StartStaminaBonus', 0],
  ['FirstHitGuard', 0],
  ['BossWordWard', 0],
]) {
  /** A deck and nothing else: no tools, no consumables, the default start, no talent yet. */
  static Plain(deck) {
    return new Departure(deck, EMPTY, EMPTY, 0, null);
  }

  /** §7.3: the cell the player starts every battle of this life on. */
  get PlayerStartCell() {
    return DeckRules.PlayerStartCell(this.StartCellsBack);
  }

  /** §7.3: the opening gap. The enemy keeps its cell, so a cell back is a cell more of gap. */
  get StartGap() {
    return DeckRules.StartGap(this.StartCellsBack);
  }

  /**
   * One battle of this life: this deck (shuffled by TurnLoop.Start), this start,
   * the tools' amounts (§9.1), and what a chain carries in (§12).
   * C# `SetupFor(EnemyDef enemy, int fieldCells, int? playerStartHp = null, int? playerStartStamina = null)`.
   */
  SetupFor(enemy, fieldCells, playerStartHp = null, playerStartStamina = null) {
    if (enemy == null) throw argumentNull('enemy');
    return new BattleSetup(enemy, this.Deck, fieldCells, this.StartGap, this.PlayerStartCell, {
      PlayerStartHp: playerStartHp ?? null,
      PlayerStartStamina: playerStartStamina ?? null,
      PlayerStartStaminaBonus: this.StartStaminaBonus,
      FirstHitGuard: this.FirstHitGuard,
      BossWordWard: this.BossWordWard,
    });
  }
}

// ------------------------------------------------------------------ DeckRules

/**
 * §8 and the 出立 step (#58): the checks a departure has to pass before the first battle. Pure
 * rules, no state; the screens ask them and show the answer.
 *
 * - The deck: 20〜40 cards, 3 of a kind at most, every kind one the character owns, and the
 *   card-level rules Cards.Validate already holds.
 * - The slots: 3 tools and 3 consumables (concept-v3.md §14, tools_and_consumables_v4.md §2 / §3).
 *   The same tool may fill two slots (tools_and_consumables_v4.md §6 の 1).
 * - The start: the player stands on cell 2 (§7.3); a tool moves it back, never past cell 1
 *   (間合いの履: cell 1, gap 4). The enemy's cell does not move, so the gap grows by the same.
 * - The talent (§17.8): one card id, of a kind in the deck. Only recorded here; the doubled
 *   mastery steps are #59's.
 */
export const DeckRules = Object.freeze({
  /** concept-v3.md §14: three tool slots, shared by exploration and battle. */
  ToolSlots: 3,

  /** concept-v3.md §14: three consumable slots, swapped on the spot, never carried home. */
  ConsumableSlots: 3,

  /** §7.3: the cells behind the default start. A start cannot go further back than this. */
  StartCellsBackMax: Constants.PlayerStartCell - 1,

  /** The player's start cell for `cellsBack` cells back, kept on the line (cell 1 at the furthest). */
  PlayerStartCell(cellsBack) {
    return Constants.PlayerStartCell - Math.max(0, Math.min(DeckRules.StartCellsBackMax, cellsBack));
  },

  /** The opening gap for `cellsBack` cells back: START_GAP plus the cells the player actually moved. */
  StartGap(cellsBack) {
    return Constants.StartGap + (Constants.PlayerStartCell - DeckRules.PlayerStartCell(cellsBack));
  },

  /**
   * §8: Cards.Validate, and every kind owned. `ownedKinds` are
   * the card ids the character owns (at most 80 kinds: 40 to start, 40 learned).
   * JS extension: `limits` (DeckLimits; the C# constants when left out) sets the deck's bounds.
   */
  ValidateDeck(deck, ownedKinds, limits = DeckLimits.Default) {
    if (deck == null) throw argumentNull('deck');
    if (ownedKinds == null) throw argumentNull('ownedKinds');

    const errors = [...Cards.Validate(deck, DeckLimits.From(limits)).Errors];
    const owned = new Set(ownedKinds);
    const notOwned = [...new Set(deck.map((c) => c.Def.Id).filter((id) => !owned.has(id)))].sort(compareOrdinal);
    for (const id of notOwned) {
      errors.push(`Card "${id}" is not owned.`);
    }
    return new DeckValidation(errors.length === 0, errors);
  },

  /**
   * Whether one more copy of `id` may go into `deck`: an owned
   * kind, under three copies, and room under 40. The deck screen greys ＋ out on false.
   * JS extension: `limits` (DeckLimits) moves the room to its DeckMax (45 with the survivor's bonus).
   */
  CanAdd(deck, id, ownedKinds, limits = DeckLimits.Default) {
    if (deck == null) throw argumentNull('deck');
    if (ownedKinds == null) throw argumentNull('ownedKinds');
    if (id == null || id === '' || !contains(ownedKinds, id)) return false;
    return deck.length < DeckLimits.From(limits).DeckMax && deck.filter((c) => c.Def.Id === id).length < Constants.CopiesMax;
  },

  /**
   * The whole departure: the deck (ValidateDeck), the two kinds of slots, the
   * start, and a talent among the deck's kinds. Empty errors when it may set out.
   * JS extension: `limits` (DeckLimits) sets the deck's bounds and the two slot counts
   * (4 tools with the survivor's bonus).
   */
  Validate(departure, ownedKinds, limits = DeckLimits.Default) {
    if (departure == null) throw argumentNull('departure');
    if (departure.Tools == null) throw argumentNullWith('departure', 'Tools is null.');
    if (departure.Consumables == null) throw argumentNullWith('departure', 'Consumables is null.');

    const L = DeckLimits.From(limits);
    const errors = [...DeckRules.ValidateDeck(departure.Deck, ownedKinds, L).Errors];

    if (departure.Tools.length > L.ToolSlots) {
      errors.push(`${departure.Tools.length} tools do not fit ${L.ToolSlots} slots.`);
    }
    if (departure.Consumables.length > L.ConsumableSlots) {
      errors.push(`${departure.Consumables.length} consumables do not fit ${L.ConsumableSlots} slots.`);
    }
    if (departure.StartCellsBack < 0) {
      errors.push(`The start moves ${departure.StartCellsBack} cells back; it never moves forward.`);
    }
    if (departure.StartStaminaBonus < 0 || departure.FirstHitGuard < 0 || departure.BossWordWard < 0) {
      errors.push("A tool's amount is never negative.");
    }

    if (departure.TalentCardId == null || departure.TalentCardId === '') {
      errors.push('No talent card is chosen.');
    } else if (!departure.Deck.some((c) => c.Def.Id === departure.TalentCardId)) {
      errors.push(`The talent card "${departure.TalentCardId}" is not in the deck.`);
    }

    return new DeckValidation(errors.length === 0, errors);
  },
});

// ------------------------------------------------------------------ DeckLimits (JS extension)

/** The four numbers, in the order the constructor reads them. */
const LIMIT_KEYS = Object.freeze(['DeckMin', 'DeckMax', 'ToolSlots', 'ConsumableSlots']);

let defaultLimits = null;

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function limitOutOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

/**
 * **JS extension, not in the C#** (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」): the numbers one
 * departure is checked against — the deck's bounds and the two kinds of slots. The C# has them as
 * constants only (Constants.DeckMin / DeckMax, DeckRules.ToolSlots / ConsumableSlots); the design
 * raises two of them for one life, which the C# does not do yet (#104 / #108).
 *
 * Sources (under the repo's .claude/docs/):
 * - vision/concept-v3.md §14 デッキ枚数 「下限 20・上限 40」, ツールの枠 「共通 3 枠」, 資源・アイテム
 *   「消耗品 3 枠」: the defaults, equal to the C# constants (DeckLimits.Default).
 * - vision/concept-v3.md §8.2, §12-12 and §14 生存者ボーナス 「出立の枠を増やす。ツール枠 +1、デッキ上限
 *   +5 を次の 1 生だけ」: a survivor's heir sets out with 4 tool slots and a deck of up to 45.
 *   Which life has the bonus is the lineage's (src/core/inheritance, docs/INHERITANCE.md R-14 / R-17);
 *   Inheritance.startLife hands the numbers over as DeckMin / DeckMax / ToolSlots / ConsumableSlots,
 *   and DeckLimits.From(lifeStart) reads them.
 * - The consumables keep 3 slots and the deck keeps its floor of 20 and 3 of a kind: the bonus names
 *   only the tools and the cap (concept-v3.md §14).
 *
 * Immutable. `new DeckLimits({ DeckMin, DeckMax, ToolSlots, ConsumableSlots })`: a key left out (or
 * null) takes the default's number. Whole numbers only; DeckMin 1 or more, DeckMax not under DeckMin,
 * slots 0 or more.
 */
export class DeckLimits {
  constructor(init = null) {
    const source = init ?? {};
    const defaults = {
      DeckMin: Constants.DeckMin,
      DeckMax: Constants.DeckMax,
      ToolSlots: DeckRules.ToolSlots,
      ConsumableSlots: DeckRules.ConsumableSlots,
    };
    for (const key of LIMIT_KEYS) {
      const value = source[key] == null ? defaults[key] : source[key];
      if (!Number.isInteger(value) || value < (key === 'DeckMin' ? 1 : 0)) {
        throw limitOutOfRange(key, value, key === 'DeckMin' ? 'A whole number, 1 or more.' : 'A whole number, 0 or more.');
      }
      this[key] = value;
    }
    if (this.DeckMax < this.DeckMin) {
      throw limitOutOfRange('DeckMax', this.DeckMax, `Not under DeckMin (${this.DeckMin}).`);
    }
    Object.freeze(this);
  }

  /** The C# constants: a deck of 20〜40, 3 tool slots, 3 consumable slots. What every check uses when no limits are given. */
  static get Default() {
    return (defaultLimits ??= new DeckLimits());
  }

  /**
   * The limits an object describes: a DeckLimits as it is, null as Default, anything else read by its
   * four keys (Inheritance.startLife's LifeStart carries them; a key it lacks is the default's).
   */
  static From(source) {
    if (source == null) return DeckLimits.Default;
    if (source instanceof DeckLimits) return source;
    return new DeckLimits(source);
  }

  /** True when every number is the C#'s: the departure is checked exactly like the C#. */
  get IsDefault() {
    const d = DeckLimits.Default;
    return LIMIT_KEYS.every((key) => this[key] === d[key]);
  }

  /** A copy with some numbers changed (C#'s `with`). */
  With(changes) {
    return new DeckLimits({ ...this, ...(changes ?? {}) });
  }

  /** Same four numbers. */
  Equals(other) {
    return other instanceof DeckLimits && LIMIT_KEYS.every((key) => this[key] === other[key]);
  }
}
