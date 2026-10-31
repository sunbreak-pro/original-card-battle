// Expanding the oracle's interned objects and diffing oracle JSON against the port's JSON.

/** Replaces every { "$ref": n } in `value` with the expanded defs[n] (shared, not copied). */
export function expandRefs(value, defs) {
  if (!defs || defs.length === 0) return value;
  const done = new Array(defs.length);
  const busy = new Array(defs.length).fill(false);
  const def = (i) => {
    if (done[i] !== undefined) return done[i];
    if (busy[i]) throw new Error(`$ref cycle at ${i}`);
    busy[i] = true;
    done[i] = walk(defs[i]);
    busy[i] = false;
    return done[i];
  };
  const walk = (v) => {
    if (v === null || typeof v !== 'object') return v;
    if (Array.isArray(v)) {
      let out = null;
      for (let i = 0; i < v.length; i++) {
        const x = walk(v[i]);
        if (x !== v[i]) {
          if (!out) out = v.slice();
          out[i] = x;
        }
      }
      return out || v;
    }
    const keys = Object.keys(v);
    if (keys.length === 1 && keys[0] === '$ref') return def(v.$ref);
    let out = null;
    for (const k of keys) {
      const x = walk(v[k]);
      if (x !== v[k]) {
        if (!out) out = { ...v };
        out[k] = x;
      }
    }
    return out || v;
  };
  return walk(value);
}

function short(v, max = 240) {
  let s;
  try {
    s = JSON.stringify(v);
  } catch {
    s = String(v);
  }
  if (s === undefined) s = 'undefined';
  return s.length > max ? s.slice(0, max) + `… (${s.length} chars)` : s;
}

const isObj = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);

/**
 * First difference between the oracle's value and the port's, or null. `floats` is the case's set of
 * normalized float paths; `shapes` gives each type's float members. Typed objects ({$type}) are
 * compared on the oracle's members (a member the port lacks is a difference); untyped objects
 * (dictionaries, harness-built maps) must have the same key set.
 */
export function firstDiff(oracle, js, ctx, path = 'output', norm = 'output', floatCtx = false) {
  if (oracle === undefined) oracle = null;
  if (js === undefined) js = null;
  if (typeof oracle === 'number' && typeof js === 'number') {
    if (oracle === js) return null;
    const isFloat = floatCtx || (ctx.floats && ctx.floats.has(norm));
    if (isFloat && Math.fround(oracle) === Math.fround(js)) return null;
    // Suites of presentation values only (backdrop) allow a few ulps: .NET's Math.Pow / Sin / Cos and
    // V8's differ in the last bit. Game logic (battle, dungeon) compares exactly.
    if (ctx.ulps && Math.abs(oracle - js) <= ctx.ulps * Number.EPSILON * Math.max(Math.abs(oracle), Math.abs(js))) return null;
    return { path, oracle, js, note: isFloat ? 'float' : undefined };
  }
  if (oracle === null || js === null || typeof oracle !== 'object' || typeof js !== 'object') {
    if (oracle === js) return null;
    return { path, oracle: short(oracle), js: short(js) };
  }
  if (Array.isArray(oracle) || Array.isArray(js)) {
    if (!Array.isArray(oracle) || !Array.isArray(js)) return { path, oracle: short(oracle), js: short(js) };
    const n = Math.min(oracle.length, js.length);
    for (let i = 0; i < n; i++) {
      const d = firstDiff(oracle[i], js[i], ctx, `${path}[${i}]`, `${norm}[*]`, floatCtx);
      if (d) return d;
    }
    if (oracle.length !== js.length) {
      return { path: `${path}.length`, oracle: oracle.length, js: js.length, extra: short(oracle.length > js.length ? oracle[n] : js[n]) };
    }
    return null;
  }
  if (!isObj(oracle) || !isObj(js)) return { path, oracle: short(oracle), js: short(js) };

  if ('$type' in oracle) {
    if (oracle.$type !== js.$type) return { path: `${path}.$type`, oracle: oracle.$type, js: js.$type ?? null };
    const shape = ctx.shapes && ctx.shapes[oracle.$type];
    const floatMembers = shape && shape.floats && shape.floats.length ? new Set(shape.floats) : null;
    for (const k of Object.keys(oracle)) {
      if (k === '$type') continue;
      if (!(k in js)) return { path: `${path}.${k}`, oracle: short(oracle[k]), js: '(missing member)' };
      const d = firstDiff(oracle[k], js[k], ctx, `${path}.${k}`, `${norm}.${k}`, floatCtx || (floatMembers !== null && floatMembers.has(k)));
      if (d) return d;
    }
    return null;
  }
  if ('$error' in oracle || '$error' in js) {
    if (oracle.$error !== js.$error || oracle.Message !== js.Message) {
      return { path, oracle: short(oracle), js: short(js) };
    }
    return null;
  }
  for (const k of Object.keys(oracle)) {
    if (!(k in js)) return { path: `${path}.${k}`, oracle: short(oracle[k]), js: '(missing key)' };
    const d = firstDiff(oracle[k], js[k], ctx, `${path}.${k}`, `${norm}.${k}`, floatCtx);
    if (d) return d;
  }
  for (const k of Object.keys(js)) {
    if (!(k in oracle)) return { path: `${path}.${k}`, oracle: '(no such key)', js: short(js[k]) };
  }
  return null;
}

export { short };
