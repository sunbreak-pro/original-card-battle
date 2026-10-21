// Port of unity-port/BattleCore/Statuses.cs
//
// Import-cycle note: Types.js imports this module (StatusSet.Empty, read lazily) and this module
// imports Types.js back (StatusKind, StatusDecay, EnumTokens). When Types.js is the first module
// of the cycle to load, this module's body runs BEFORE Types.js's body, so nothing here may read a
// Types.js export at module-evaluation time: StatusSet.Empty only builds an empty Map, and every
// read of StatusKind / StatusDecay happens inside a function.

import {
  enumIndex,
  ArgumentNullException,
  ArgumentOutOfRangeException,
} from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { StatusKind, StatusDecay, EnumTokens } from './Types.js';

// ------------------------------------------------------------------ local helpers (not C# types)

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function argumentOutOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

function isPlainObject(x) {
  return x !== null && typeof x === 'object' && Object.getPrototypeOf(x) === Object.prototype;
}

/**
 * One C# tuple `(StatusKind Kind, int Stacks)` as a caller may write it in JS: `{ Kind, Stacks }`
 * (the named tuple), `{ Item1, Item2 }` (C# erases tuple names) or `[kind, stacks]`.
 */
function tupleOf(entry) {
  if (Array.isArray(entry)) return { Kind: entry[0], Stacks: entry[1] };
  if (entry != null && typeof entry === 'object') {
    if ('Kind' in entry || 'Stacks' in entry) return { Kind: entry.Kind, Stacks: entry.Stacks };
    if ('Item1' in entry || 'Item2' in entry) return { Kind: entry.Item1, Stacks: entry.Item2 };
  }
  throw new ArgumentOutOfRangeException(`Not a (StatusKind Kind, int Stacks) tuple: ${entry}`);
}

/** Whether `x` is one tuple written as an array (`[kind, stacks]`), not an array of tuples. */
function isArrayTuple(x) {
  return Array.isArray(x) && x.length === 2 && typeof x[0] === 'string' && typeof x[1] === 'number';
}

// ------------------------------------------------------------------ StatusSet

/**
 * C# `private static int Clamp(StatusKind kind, int stacks)` of StatusSet.
 * §5 (#205): the ターンで減る型 stops at the cap; the 使うと減る型 is not counted.
 */
function clamp(kind, stacks) {
  return Statuses.DecayOf(kind) === StatusDecay.OnTurn ? Math.min(stacks, Constants.TurnDecayStackMax) : stacks;
}

/** C# `private Dictionary<StatusKind, int> ToDictionary()` of StatusSet: a fresh copy, in enumeration order. */
function toDictionary(set) {
  return new Map(set._stacks);
}

/**
 * §5: the statuses one combatant carries, as word → stack count. Immutable: every change hands
 * back a new set, so a battle state can be compared with the one before it.
 *
 * A word with zero stacks is not held at all, which is what keeps the kind count honest. A
 * ターンで減る型 word never holds more than Constants.TurnDecayStackMax (§5, #205):
 * both ways in (Of and Add) stop it there, and the others only take
 * stacks away.
 *
 * JS port: the C# `IReadOnlyDictionary<StatusKind, int>` is a Map held in `_stacks` (the only own
 * field, never mutated after construction). C# `Equals` / `GetHashCode` / `ToString` are kept
 * under their C# names, with `equals` / `toString` aliases for JS; record equality in cs.js
 * (csEquals) compares two sets by their `_stacks` entries, which is the same rule.
 */
export class StatusSet {
  constructor(stacks) {
    // C# `private StatusSet(IReadOnlyDictionary<StatusKind, int> stacks)`. Only this module builds
    // sets; everyone else goes through Empty / Of / Add.
    this._stacks = stacks;
  }

  /**
   * C# `public static StatusSet Of(params (StatusKind Kind, int Stacks)[] entries)`.
   * Tuples as `{ Kind, Stacks }`, `{ Item1, Item2 }` or `[kind, stacks]`; the params array may
   * also be passed whole (`StatusSet.Of(words)`, C# `StatusSet.Of(words.ToArray())`).
   */
  static Of(...entries) {
    if (entries.length === 1 && Array.isArray(entries[0]) && !isArrayTuple(entries[0])) {
      entries = entries[0];
    }
    const map = new Map();
    for (const raw of entries) {
      const entry = tupleOf(raw);
      if (entry.Stacks > 0) map.set(entry.Kind, clamp(entry.Kind, entry.Stacks));
    }
    return new StatusSet(map);
  }

  /** The words held, in enum order, so a screen lists them the same way twice. */
  get Kinds() {
    // Keys are distinct, so the order is total (OrderBy's stability never matters).
    return [...this._stacks.keys()].sort((a, b) => enumIndex(StatusKind, a) - enumIndex(StatusKind, b));
  }

  get KindCount() {
    return this._stacks.size;
  }

  Has(kind) {
    return this._stacks.has(kind);
  }

  Stacks(kind) {
    return this._stacks.has(kind) ? this._stacks.get(kind) : 0;
  }

  /**
   * §3.1 / §5: add stacks of one word. Stacking an existing word is always allowed; taking on
   * a word that is not held yet is refused once the holder is at its kind limit, and the set
   * comes back unchanged. The player limit is 6 kinds (§5); enemies pass no limit.
   *
   * Which word gets pushed out when a seventh arrives is not settled in the canon, so nothing
   * is pushed out here. That question returns with #47.
   *
   * §5 (#205): a ターンで減る型 word stops at Constants.TurnDecayStackMax and the
   * rest is dropped, on either side; a word already there comes back unchanged. The 使うと減る型
   * is not counted (§19.5 S14).
   *
   * C# `Add(StatusKind kind, int stacks = Constants.StatusApplyDefault, int? kindLimit = null)`.
   * JS: positional; a trailing plain object `{ stacks, kindLimit }` is read as C# named arguments
   * (`Add(kind, 2, { kindLimit: 1 })` or `Add(kind, { kindLimit: 1 })`). null / undefined kindLimit
   * is no limit.
   */
  Add(kind, stacks, kindLimit) {
    if (isPlainObject(kindLimit)) {
      const named = kindLimit;
      kindLimit = named.kindLimit;
      if (named.stacks !== undefined) stacks = named.stacks;
    } else if (isPlainObject(stacks)) {
      const named = stacks;
      stacks = named.stacks;
      kindLimit = named.kindLimit;
    }
    if (stacks === undefined) stacks = Constants.StatusApplyDefault;
    if (kindLimit === undefined) kindLimit = null;

    if (stacks <= 0) return this;
    const isNewKind = !this._stacks.has(kind);
    if (isNewKind && kindLimit != null && this._stacks.size >= kindLimit) return this;

    const next = clamp(kind, this.Stacks(kind) + stacks);
    if (next === this.Stacks(kind)) return this;

    const map = toDictionary(this);
    map.set(kind, next);
    return new StatusSet(map);
  }

  /**
   * §5: the ターンで減る型 loses one stack at the holder's turn start. The 使うと減る型 is
   * untouched here — it is spent by Consume when its effect fires.
   */
  TickTurnStart() {
    const map = new Map();
    for (const [key, value] of this._stacks) {
      const next = Statuses.DecayOf(key) === StatusDecay.OnTurn ? value - 1 : value;
      if (next > 0) map.set(key, next);
    }
    return new StatusSet(map);
  }

  /** §5: the 使うと減る型 loses one stack each time its effect lands. */
  Consume(kind) {
    if (!this._stacks.has(kind)) return this;
    const map = toDictionary(this);
    const next = map.get(kind) - 1;
    if (next > 0) map.set(kind, next);
    else map.delete(kind);
    return new StatusSet(map);
  }

  /** C# `Equals(StatusSet? other)` (IEquatable) and `Equals(object? obj)`. */
  Equals(other) {
    if (other == null || !(other instanceof StatusSet)) return false;
    if (this === other) return true;
    if (this._stacks.size !== other._stacks.size) return false;
    for (const [key, value] of this._stacks) {
      if (other.Stacks(key) !== value) return false;
    }
    return true;
  }

  equals(other) {
    return this.Equals(other);
  }

  /** C# GetHashCode, with C# unchecked int arithmetic. */
  GetHashCode() {
    let hash = 17;
    for (const kind of this.Kinds) {
      hash = (Math.imul(hash, 31) + enumIndex(StatusKind, kind)) | 0;
      hash = (Math.imul(hash, 31) + this.Stacks(kind)) | 0;
    }
    return hash;
  }

  ToString() {
    return this._stacks.size === 0
      ? '—'
      : this.Kinds.map((k) => `${EnumTokens.ToLabel(k)} ${this.Stacks(k)}`).join(' / ');
  }

  toString() {
    return this.ToString();
  }

  /** Parity JSON (docs/PARITY_SHAPES.md custom shape): `{ $type, Stacks: { <kind>: n } }` in Kinds order. */
  toJSON() {
    const stacks = {};
    for (const k of this.Kinds) stacks[k] = this.Stacks(k);
    return { $type: 'StatusSet', Stacks: stacks };
  }
}
StatusSet.$name = 'StatusSet';
/** public static readonly StatusSet Empty = new StatusSet(new Dictionary<StatusKind, int>()); */
StatusSet.Empty = new StatusSet(new Map());

// ------------------------------------------------------------------ Statuses

/**
 * The rules each status word follows. The turn loop applies them (#188): the turn-start words
 * in TurnLoop's OpenTurnFor, the on-use words where their effect lands.
 */
export const Statuses = Object.freeze({
  /** §5: which of the two decay types a word follows. */
  DecayOf(kind) {
    switch (kind) {
      case StatusKind.Slow: return StatusDecay.OnTurn;
      case StatusKind.Swift: return StatusDecay.OnTurn;
      case StatusKind.Bleed: return StatusDecay.OnTurn;
      case StatusKind.Fatigue: return StatusDecay.OnTurn;
      case StatusKind.Regen: return StatusDecay.OnTurn;
      case StatusKind.Fragile: return StatusDecay.OnUse;
      case StatusKind.Intimidate: return StatusDecay.OnUse;
      case StatusKind.Empower: return StatusDecay.OnUse;
      case StatusKind.Focus: return StatusDecay.OnUse;
      case StatusKind.Parry: return StatusDecay.OnUse;
      // Boss-only words (#51): roster §4.1 / §5.1 / §6.2.
      case StatusKind.MiasmaShroud: return StatusDecay.Lasting;
      case StatusKind.Binding: return StatusDecay.OnTurn;
      case StatusKind.Hook: return StatusDecay.OnUse;
      case StatusKind.Depths: return StatusDecay.Lasting;
      case StatusKind.Rooting: return StatusDecay.Lasting;
      case StatusKind.Withering: return StatusDecay.Lasting;
      default: throw argumentOutOfRange('kind', kind, 'Unknown status.');
    }
  },

  /** §5 向き: whether the word is one a side puts on itself (俊敏 / 強化 / 集中 / 見切り / 再生). */
  IsOwn(kind) {
    switch (kind) {
      case StatusKind.Swift: return true;
      case StatusKind.Empower: return true;
      case StatusKind.Focus: return true;
      case StatusKind.Parry: return true;
      case StatusKind.Regen: return true;
      default: return false;
    }
  },

  /**
   * §5 鈍足 (v4.3 reading, §21.3): every cell change the holder causes — its own move face and
   * the push / pull it lands — is one cell shorter, floor 0. A one-cell move still stops dead,
   * as it did in v4.2; a two-cell one becomes one. What the other side does to the holder is
   * not reduced.
   */
  MoveReduction(statuses) {
    if (statuses == null) throw argumentNull('statuses');
    return statuses.Has(StatusKind.Slow) ? 1 : 0;
  },

  // ---- The boss-only words (#51) ----

  /** roster §6.2 根張り: HP lost per stack at the turn start after two turns ended on one cell. */
  RootingHpPerStack: 4,

  /** roster §4.1 / §5.1 / §6.2: the most stacks a boss puts of its word (瘴気纏い 2 → −2, 深み 2 → −2, 根張り 2 → −8, 枯らし 2 → −2). */
  BossWordStackMax: 2,

  /** Whether the word is one of the six a boss alone gives (battle_core_v4 §5 ボス専用の状態). */
  IsBossOnly(kind) {
    switch (kind) {
      case StatusKind.MiasmaShroud: return true;
      case StatusKind.Binding: return true;
      case StatusKind.Hook: return true;
      case StatusKind.Depths: return true;
      case StatusKind.Rooting: return true;
      case StatusKind.Withering: return true;
      default: return false;
    }
  },

  /** roster §6.2 枯らし (v4.14, #399 / #400): the recovery 枯らし leaves at the least, 疲労 included. */
  WitheringRecoveryFloor: 1,

  /**
   * roster §5.1 深み and §6.2 枯らし: what the turn-start recovery loses. 枯らし takes 1 a stack
   * always; 深み 1 a stack when the holder starts the turn adjacent (N 0) to the nearest enemy.
   * 疲労 is not counted here (battle_core_v4 §5: −1 while held); it is already out of
   * `recoveryBefore`, the recovery the turn would give without these two words.
   *
   * 枯らし takes only as much as brings `recoveryBefore` down to
   * WitheringRecoveryFloor (roster §6.2 「疲労と重なっても、回復は 1 より下がらない
   * （1 が下限）」), and nothing when it is already 1 or less. The floor is a 枯らし rule only:
   * 深み keeps its 「0 が下限」 (roster §5.1, and the v4.14 history: 深み is not changed), so it
   * comes after 枯らし and may still take the recovery below 1. The floor 0 is the caller's.
   */
  RecoveryLoss(statuses, adjacent, recoveryBefore) {
    if (statuses == null) throw argumentNull('statuses');
    const withering = Math.min(
      statuses.Stacks(StatusKind.Withering),
      Math.max(0, recoveryBefore - Statuses.WitheringRecoveryFloor),
    );
    return withering + (adjacent ? statuses.Stacks(StatusKind.Depths) : 0);
  },

  /**
   * roster §6.2 根張り, read at the holder's turn start from the last two turn ends: the same cell
   * both times, and the holder did not move itself in the later turn. A push or a pull that left
   * the holder elsewhere shows as another cell; one that the holder walked back from shows as a
   * move (数え直し).
   */
  StayedTwoTurns(turnEnds) {
    if (turnEnds == null) throw argumentNull('turnEnds');
    if (turnEnds.length < 2) return false;
    const last = turnEnds[turnEnds.length - 1];
    const before = turnEnds[turnEnds.length - 2];
    return last.Cell > 0 && last.Cell === before.Cell && !last.Moved;
  },
});
