// Small helpers the suite runners use to mirror the oracle's C# (oracle/src/Suites/*.cs).

/** Tables.Try: the value, or the error itself (written as { $error, Message }). */
export function Try(f) {
  try {
    return f();
  } catch (e) {
    if (e && e.name === 'Missing') throw e;
    return e instanceof Error ? e : new Error(String(e));
  }
}

/** Plain enum values in declaration order (defineEnum keeps $order). */
export function enumValues(E) {
  if (E && Array.isArray(E.$order)) return E.$order.slice();
  return Object.values(E);
}

/** Array from an IEnumerable port (array, Set, iterator, generator). */
export function arr(v) {
  if (v == null) return [];
  if (Array.isArray(v)) return v;
  if (typeof v[Symbol.iterator] === 'function') return [...v];
  return v;
}

/** Dictionary entries from a Map or a plain object. */
export function entries(d) {
  if (d == null) return [];
  if (d instanceof Map) return [...d.entries()];
  return Object.entries(d);
}

/** Dictionary lookup on a Map or a plain object; undefined when absent. */
export function lookup(d, key) {
  if (d instanceof Map) return d.get(key);
  return d ? d[key] : undefined;
}

/**
 * A C# method with an `out` parameter, as ported. Two spellings are in use in src/: { ok, value }
 * (ok = the C# return value, value = the out parameter; CONVENTIONS.md) and { value, <outName> }
 * (value = the return value, the out parameter under its C# name, e.g. Loadout.Use → { value, used }).
 * Also accepts [ret, out] and { result, value }. Returns [ret, out].
 */
export function outPair(r, outName) {
  if (Array.isArray(r)) return [r[0], r[1]];
  if (r && typeof r === 'object') {
    if ('ok' in r && 'value' in r) return [r.ok, r.value];
    if (outName && outName in r && 'value' in r) return [r.value, r[outName]];
    if ('result' in r && 'value' in r) return [r.result, r.value];
  }
  throw new Error(`out parameter not in a known form: ${Object.keys(r || {}).join(',')}`);
}

/** Two out parameters (BackdropMotion.MoteAt's x / y): { x, y } / { X, Y } / [x, y] / { ok, value: {...} }. */
export function outXY(r) {
  if (Array.isArray(r)) return [r[0], r[1]];
  if (r && typeof r === 'object') {
    if ('x' in r && 'y' in r) return [r.x, r.y];
    if ('X' in r && 'Y' in r) return [r.X, r.Y];
    if ('value' in r) return outXY(r.value);
  }
  throw new Error('MoteAt result not in a known form');
}

/** Tables.Static: the port's static members the oracle listed (keys taken from the oracle's own output). */
export function readStatic(obj, keys) {
  const o = {};
  for (const k of keys) {
    let v;
    try {
      v = obj[k];
    } catch (e) {
      v = e;
    }
    o[k] = v;
  }
  return o;
}

export function range(start, count) {
  return Array.from({ length: count }, (_, i) => start + i);
}
