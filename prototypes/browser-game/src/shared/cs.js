// C# semantics helpers shared by every ported module. Keep this file small and exact:
// a mismatch here shows up as a parity failure everywhere.

// ---------------------------------------------------------------- numbers
/** C# int division: truncates toward zero. */
export function idiv(a, b) {
  if (b === 0) throw new Error('Attempted to divide by zero.');
  return Math.trunc(a / b);
}
/** C# Math.Round(x) default: MidpointRounding.ToEven (banker's rounding). */
export function roundEven(x, digits = 0) {
  const m = Math.pow(10, digits);
  const v = x * m;
  const r = Math.round(v);
  // exact .5 midpoint → round to even
  if (Math.abs(v % 1) === 0.5) {
    const f = Math.floor(v);
    return (f % 2 === 0 ? f : f + 1) / m;
  }
  return r / m;
}
/** Math.Round(x, MidpointRounding.AwayFromZero). */
export function roundAway(x, digits = 0) {
  const m = Math.pow(10, digits);
  const v = x * m;
  return (v < 0 ? -Math.round(-v) : Math.round(v)) / m;
}
/** Math.Clamp(value, min, max) — throws like C# when min > max. */
export function clamp(v, min, max) {
  if (min > max) throw new Error(`'${min}' cannot be greater than ${max}.`);
  return v < min ? min : v > max ? max : v;
}
/** (int)double cast. */
export function toInt(x) { return Math.trunc(x); }
/** int.MaxValue / int.MinValue */
export const INT_MAX = 2147483647;
export const INT_MIN = -2147483648;

// ---------------------------------------------------------------- enums
/**
 * Plain C# enum → frozen object of Name → 'Name'. Declaration order is kept in `.$order`
 * so `(int)value` casts can be ported with enumIndex().
 */
export function defineEnum(names) {
  const o = {};
  names.forEach((n) => { o[n] = n; });
  Object.defineProperty(o, '$order', { value: names.slice(), enumerable: false });
  return Object.freeze(o);
}
/** (int)value for an enum made by defineEnum (declaration order, C# default underlying values). */
export function enumIndex(E, value) {
  const i = E.$order.indexOf(value);
  if (i < 0) throw new Error(`Unknown enum value ${value}`);
  return i;
}
/** (E)i for an enum made by defineEnum. */
export function enumAt(E, i) { return E.$order[i]; }
/** Enum.GetValues(typeof(E)) */
export function enumValues(E) { return E.$order.slice(); }
/** [Flags] enum → frozen object Name → int. Use bit ops on the values exactly like C#. */
export function defineFlags(map) { return Object.freeze(Object.assign({}, map)); }
/** HasFlag */
export function hasFlag(value, flag) { return (value & flag) === flag; }

// ---------------------------------------------------------------- records
/**
 * defineRecord('Name', ['A', 'B', ['C', defaultValue]]) → class with a positional constructor,
 * `with(changes)`, `equals(other)` (structural) and `toJSON()`. Records are never mutated
 * after construction: copy with `with` as C# does. Add getters / methods on the returned class.
 *
 *   export class Omen extends defineRecord('Omen', ['Kind', 'Power', ['Plan', null]]) {
 *     get IsAttack() { return this.Kind === OmenKind.Attack; }
 *   }
 */
export function defineRecord(name, fields, base) {
  const names = fields.map((f) => (Array.isArray(f) ? f[0] : f));
  const defaults = fields.map((f) => (Array.isArray(f) ? f[1] : undefined));
  const Base = base || Object;
  const C = class extends Base {
    constructor(...args) {
      super(...(base ? args.slice(0, base.$fields ? base.$fields.length : 0) : []));
      const off = base && base.$fields ? base.$fields.length : 0;
      for (let i = 0; i < names.length; i++) {
        const v = args[off + i];
        this[names[i]] = v === undefined ? (typeof defaults[i] === 'function' ? defaults[i]() : defaults[i] === undefined ? null : defaults[i]) : v;
      }
    }
    with(changes) {
      const copy = Object.create(Object.getPrototypeOf(this));
      Object.assign(copy, this, changes);
      return copy;
    }
    equals(other) { return csEquals(this, other); }
    toJSON() {
      const o = { $type: this.constructor.$name };
      for (const k of this.constructor.$fields) o[k] = this[k];
      return o;
    }
  };
  Object.defineProperty(C, 'name', { value: name });
  C.$name = name;
  C.$fields = (base && base.$fields ? base.$fields : []).concat(names);
  return C;
}

/** Structural equality like C# record / value equality (arrays compared element-wise, Maps by entries). */
export function csEquals(a, b) {
  if (a === b) return true;
  if (a == null || b == null) return false;
  if (typeof a !== 'object' || typeof b !== 'object') return Number.isNaN(a) && Number.isNaN(b);
  if (Object.getPrototypeOf(a) !== Object.getPrototypeOf(b)) return false;
  if (Array.isArray(a)) {
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) if (!csEquals(a[i], b[i])) return false;
    return true;
  }
  if (a instanceof Map) {
    if (a.size !== b.size) return false;
    for (const [k, v] of a) if (!b.has(k) || !csEquals(v, b.get(k))) return false;
    return true;
  }
  if (a instanceof Set) {
    if (a.size !== b.size) return false;
    for (const v of a) if (!b.has(v)) return false;
    return true;
  }
  const ka = Object.keys(a), kb = Object.keys(b);
  if (ka.length !== kb.length) return false;
  for (const k of ka) if (!csEquals(a[k], b[k])) return false;
  return true;
}

// ---------------------------------------------------------------- collections / LINQ
/** Stable OrderBy / ThenBy: keys = [fn, fn, ...], each returning a number or string; prefix '-' via {desc:true}. */
export function stableSortBy(arr, ...keys) {
  const ks = keys.map((k) => (typeof k === 'function' ? { f: k, desc: false } : k));
  return arr
    .map((v, i) => ({ v, i }))
    .sort((x, y) => {
      for (const { f, desc } of ks) {
        const a = f(x.v), b = f(y.v);
        const c = typeof a === 'string' ? compareOrdinal(a, b) : a < b ? -1 : a > b ? 1 : 0;
        if (c !== 0) return desc ? -c : c;
      }
      return x.i - y.i;
    })
    .map((p) => p.v);
}
/** string.CompareOrdinal sign. */
export function compareOrdinal(a, b) { return a < b ? -1 : a > b ? 1 : 0; }
/** Enumerable.Range(start, count) */
export function range(start, count) { return Array.from({ length: count }, (_, i) => start + i); }
/** Sum with an optional selector. */
export function sum(arr, f) { let s = 0; for (const v of arr) s += f ? f(v) : v; return s; }
/** First / FirstOrDefault with predicate. */
export function first(arr, pred) { for (const v of arr) if (!pred || pred(v)) return v; throw new Error('Sequence contains no matching element'); }
export function firstOrDefault(arr, pred, dflt = null) { for (const v of arr) if (!pred || pred(v)) return v; return dflt; }
/** Max/Min with selector (throws on empty, like C#). */
export function max(arr, f) { if (!arr.length) throw new Error('Sequence contains no elements'); let m = f ? f(arr[0]) : arr[0]; for (const v of arr) { const x = f ? f(v) : v; if (x > m) m = x; } return m; }
export function min(arr, f) { if (!arr.length) throw new Error('Sequence contains no elements'); let m = f ? f(arr[0]) : arr[0]; for (const v of arr) { const x = f ? f(v) : v; if (x < m) m = x; } return m; }
/** Dictionary.TryGetValue → returns [found, value]. */
export function tryGet(map, key) { return map.has(key) ? [true, map.get(key)] : [false, undefined]; }

// ---------------------------------------------------------------- errors
export class InvalidOperationException extends Error { constructor(m) { super(m); this.name = 'InvalidOperationException'; } }
export class ArgumentException extends Error { constructor(m) { super(m); this.name = 'ArgumentException'; } }
export class ArgumentOutOfRangeException extends Error { constructor(m) { super(m); this.name = 'ArgumentOutOfRangeException'; } }
export class ArgumentNullException extends Error { constructor(m) { super(m); this.name = 'ArgumentNullException'; } }
export class KeyNotFoundException extends Error { constructor(m) { super(m); this.name = 'KeyNotFoundException'; } }

// ---------------------------------------------------------------- JSON for parity
/** JSON form used by the parity harness: Maps → objects, Sets → arrays, records → {$type,...}. Must match the C# oracle's serializer. */
export function toParityJson(v) {
  return JSON.parse(JSON.stringify(v, (k, x) => {
    if (x instanceof Map) return Object.fromEntries([...x.entries()].map(([kk, vv]) => [String(kk), vv]));
    if (x instanceof Set) return [...x];
    if (typeof x === 'bigint') return x.toString();
    return x;
  }));
}
