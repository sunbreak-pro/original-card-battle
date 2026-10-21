// Helper for the dungeon port (no C# counterpart): builds the .NET argument exceptions with the
// exact Message text .NET 10 gives them, so a thrown error reads the same as in the C# build.
//   new ArgumentException(msg, param)               → "msg (Parameter 'param')"
//   new ArgumentNullException(param)                → "Value cannot be null. (Parameter 'param')"
//   new ArgumentOutOfRangeException(param)          → "Specified argument was out of the range of valid values. (Parameter 'param')"
//   new ArgumentOutOfRangeException(param, v, msg)  → "msg (Parameter 'param')\nActual value was v."  (msg null → the default text)
// Environment.NewLine is "\n" here (the oracle runs on Linux; Unity on Windows would print "\r\n").
import {
  ArgumentException,
  ArgumentNullException,
  ArgumentOutOfRangeException,
} from '../../shared/cs.js';

const OUT_OF_RANGE = 'Specified argument was out of the range of valid values.';

/** new ArgumentException(message, paramName) */
export function argException(message, paramName) {
  return new ArgumentException(paramName ? `${message} (Parameter '${paramName}')` : message);
}

/** new ArgumentNullException(paramName) */
export function argNull(paramName) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${paramName}')`);
}

/**
 * new ArgumentOutOfRangeException(paramName) when called with one argument;
 * new ArgumentOutOfRangeException(paramName, actualValue, message) otherwise.
 */
export function argOutOfRange(paramName, ...rest) {
  if (rest.length === 0) return new ArgumentOutOfRangeException(`${OUT_OF_RANGE} (Parameter '${paramName}')`);
  const [actualValue, message] = rest;
  let text = `${message ?? OUT_OF_RANGE} (Parameter '${paramName}')`;
  if (actualValue !== null && actualValue !== undefined) text += `\nActual value was ${actualValue}.`;
  return new ArgumentOutOfRangeException(text);
}

/** string.IsNullOrEmpty */
export function isNullOrEmpty(s) {
  return s === null || s === undefined || s === '';
}

// char.IsWhiteSpace: U+0009..U+000D, U+0020, U+0085, U+00A0, U+1680, U+2000..U+200A, U+2028, U+2029,
// U+202F, U+205F, U+3000. Built from code points so no raw separator sits in the source.
const WHITE_SPACE = new RegExp('^[' + [0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x20, 0x85, 0xa0, 0x1680]
  .concat(Array.from({ length: 11 }, (_, i) => 0x2000 + i), [0x2028, 0x2029, 0x202f, 0x205f, 0x3000])
  .map((c) => '\\u' + c.toString(16).padStart(4, '0')).join('') + ']*$');

/** string.IsNullOrWhiteSpace — char.IsWhiteSpace's set (U+0085 yes, U+FEFF no, unlike JS \s). */
export function isNullOrWhiteSpace(s) {
  if (s === null || s === undefined) return true;
  return WHITE_SPACE.test(s);
}

/** ulong → BigInt in [0, 2^64). Accepts a number or a BigInt, wraps like an unchecked cast. */
export function toUlong(v) {
  return BigInt.asUintN(64, typeof v === 'bigint' ? v : BigInt(v));
}
