// The JS half of the oracle's JSON form (docs/PARITY_SHAPES.md). toOracleJson turns whatever the port
// returns into the same JSON the C# oracle writes, reading each typed object's members from
// oracle/out/shapes.json, so a C# computed property is read through the JS getter of the same name.
import { toParityJson } from '../../../src/shared/cs.js';

export { toParityJson };

/** Name of a typed JS object: the record's $name (defineRecord), else the class name; null for a plain object. */
export function typeName(v) {
  const C = v && v.constructor;
  if (!C || C === Object || C === Array || C === Map || C === Set) return null;
  if (C.$name) return C.$name;
  return C.name && C.name !== 'Object' ? C.name : null;
}

/**
 * The message the C# passed to the exception: a port that copies .NET's full Message (the
 * " (Parameter 'x')" suffix and the "\nActual value was v." line) is cut back the way the oracle
 * cuts its own (ParityJson.cs BaseMessage), so both spellings compare equal.
 */
export function baseMessage(msg) {
  let m = String(msg);
  m = m.replace(/\r?\nActual value was [\s\S]*$/, '');
  m = m.replace(/ \(Parameter '[^']*'\)$/, '');
  return m;
}

function errorJson(e) {
  let name = e && e.name;
  if (!name || name === 'Error') name = (e && e.constructor && e.constructor.name) || 'Error';
  return { $error: name, Message: baseMessage(e && e.message !== undefined ? e.message : e) };
}

function numberJson(x) {
  if (Number.isNaN(x)) return 'NaN';
  if (x === Infinity) return 'Infinity';
  if (x === -Infinity) return '-Infinity';
  return x;
}

function bigintJson(b) {
  const abs = b < 0n ? -b : b;
  return abs <= 9007199254740992n ? Number(b) : b.toString();
}

/**
 * JS value → oracle JSON. `shapes` is oracle/out/shapes.json.
 * - Map → object with String(key) keys; Set / arrays / iterables → arrays.
 * - Error → { $error, Message }.
 * - typed object with a shape → { $type, members... } (getter that throws: left out; undefined: null).
 * - StatusSet / LayerMap → their custom shapes.
 * - typed object without a shape → { $type, own enumerable fields (no functions, no _private) }.
 */
export function toOracleJson(v, shapes, depth = 0) {
  if (depth > 120) throw new Error('toOracleJson: nested too deep (a cycle?)');
  if (v === undefined || v === null) return null;
  switch (typeof v) {
    case 'string':
    case 'boolean':
      return v;
    case 'number':
      return numberJson(v);
    case 'bigint':
      return bigintJson(v);
    case 'function':
    case 'symbol':
      return null;
    default:
      break;
  }
  if (v instanceof Error) return errorJson(v);
  if (Array.isArray(v)) return v.map((x) => toOracleJson(x, shapes, depth + 1));
  if (v instanceof Map) {
    const o = {};
    for (const [k, x] of v) o[String(k)] = toOracleJson(x, shapes, depth + 1);
    return o;
  }
  if (v instanceof Set) return [...v].map((x) => toOracleJson(x, shapes, depth + 1));
  if (ArrayBuffer.isView(v)) return Array.from(v, (x) => toOracleJson(x, shapes, depth + 1));

  const name = typeName(v);
  if (name === null) {
    // A plain object: an ordered map the harness built, or a tuple-like object.
    if (typeof v[Symbol.iterator] === 'function') return [...v].map((x) => toOracleJson(x, shapes, depth + 1));
    const o = {};
    for (const k of Object.keys(v)) {
      if (typeof v[k] === 'function') continue;
      o[k] = toOracleJson(v[k], shapes, depth + 1);
    }
    return o;
  }

  const shape = shapes && shapes[name];
  const o = { $type: name };
  if (shape && shape.custom === 'StatusSet') {
    const kinds = typeof v.Kinds === 'function' ? v.Kinds() : v.Kinds;
    const stacks = {};
    for (const k of kinds || []) stacks[String(k)] = toOracleJson(v.Stacks(k), shapes, depth + 1);
    o.Stacks = stacks;
    return o;
  }
  if (shape) {
    for (const m of shape.members) {
      if (shape.custom === 'LayerMap' && (m === 'Successors' || m === 'Fingerprint')) continue;
      let x;
      try {
        x = v[m];
      } catch {
        continue; // a getter that throws is left out, as the oracle does
      }
      if (typeof x === 'function') x = null; // a C# property ported as a method: shows up as a difference
      o[m] = toOracleJson(x, shapes, depth + 1);
    }
    if (shape.custom === 'LayerMap') {
      const succ = {};
      for (const n of v.Nodes || []) succ[String(n.Id)] = toOracleJson(v.Successors(n.Id), shapes, depth + 1);
      o.Successors = succ;
      o.Fingerprint = v.Fingerprint();
    }
    return o;
  }
  if (typeof v[Symbol.iterator] === 'function' && !(typeof v.toJSON === 'function')) {
    return [...v].map((x) => toOracleJson(x, shapes, depth + 1));
  }
  const fields = v.constructor && v.constructor.$fields ? v.constructor.$fields : Object.keys(v);
  for (const k of fields) {
    if (k.startsWith('_') || typeof v[k] === 'function') continue;
    o[k] = toOracleJson(v[k], shapes, depth + 1);
  }
  return o;
}
