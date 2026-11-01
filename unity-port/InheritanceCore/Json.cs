#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace InheritanceCore
{
    /// <summary>
    /// A small JSON reader and writer with the browser's semantics, for the lineage and the trial's save
    /// (Unity has neither System.Text.Json nor a dependable Newtonsoft). The values are plain:
    /// <c>null</c>, <see cref="bool"/>, numbers, <see cref="string"/>, <see cref="List{T}"/> of object and
    /// <see cref="JsonObject"/>.
    ///
    /// <see cref="Stringify"/> writes exactly what JavaScript's <c>JSON.stringify</c> writes for the same
    /// value: integers without ".0", other numbers in their shortest round-trip form with JS's exponent rule
    /// (1e21, 1e-7), NaN and Infinity as null, strings escaped as JS escapes them (control characters as
    /// \b \f \n \r \t or \u00xx, lone surrogates as \udxxx, everything else raw). With <c>indent</c> it
    /// writes <c>JSON.stringify(value, null, 2)</c>.
    ///
    /// <see cref="Parse"/> is <c>JSON.parse</c>: strict JSON, last duplicate key wins. A number comes back
    /// as <see cref="int"/> when it is an integer in int range, as <see cref="long"/> when it is an integer
    /// a double holds exactly (|n| &lt;= 2^53), and as <see cref="double"/> otherwise — the value is the
    /// double JS would hold (so 1.0 reads as int 1, and 12345678901234567890 as a double).
    /// </summary>
    public static class Json
    {
        /// <summary>Nesting deeper than this is refused (a guard against a stack overflow, which C# cannot catch).</summary>
        public const int MaxDepth = 1000;

        private const double SafeInteger = 9007199254740992.0; // 2^53
        private const long SafeLong = 9007199254740992L;

        // ------------------------------------------------------------------ writing

        /// <summary>JSON text of <paramref name="value"/>, as <c>JSON.stringify(value)</c> (or <c>(value, null, 2)</c>).</summary>
        public static string Stringify(object? value, bool indent = false)
        {
            var sb = new StringBuilder();
            Write(sb, value, indent, "", 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object? value, bool indent, string gap, int depth)
        {
            if (depth > MaxDepth) throw new ArgumentException($"JSON nested deeper than {MaxDepth}");
            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case string s:
                    Quote(sb, s);
                    return;
                case char c:
                    Quote(sb, c.ToString());
                    return;
                case Enum e:
                    // The JS enums are their names (shared/cs.js defineEnum).
                    Quote(sb, e.ToString());
                    return;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    return;
                case long l:
                    sb.Append(l >= -SafeLong && l <= SafeLong ? l.ToString(CultureInfo.InvariantCulture) : FormatNumber(l));
                    return;
                case short sh:
                    sb.Append(sh.ToString(CultureInfo.InvariantCulture));
                    return;
                case sbyte sb8:
                    sb.Append(sb8.ToString(CultureInfo.InvariantCulture));
                    return;
                case byte u8:
                    sb.Append(u8.ToString(CultureInfo.InvariantCulture));
                    return;
                case ushort u16:
                    sb.Append(u16.ToString(CultureInfo.InvariantCulture));
                    return;
                case uint u32:
                    sb.Append(u32.ToString(CultureInfo.InvariantCulture));
                    return;
                case ulong u64:
                    sb.Append(u64 <= (ulong)SafeLong ? u64.ToString(CultureInfo.InvariantCulture) : FormatNumber(u64));
                    return;
                case double d:
                    sb.Append(FormatNumber(d));
                    return;
                case float f:
                    sb.Append(FormatNumber(f));
                    return;
                case decimal m:
                    sb.Append(FormatNumber((double)m));
                    return;
                case JsonObject o:
                    WriteObject(sb, o, indent, gap, depth);
                    return;
                case IDictionary dict:
                    WriteObject(sb, JsonObject.From(dict), indent, gap, depth);
                    return;
                case IEnumerable list:
                    WriteArray(sb, list, indent, gap, depth);
                    return;
                default:
                    throw new ArgumentException($"Json cannot write a {value.GetType().FullName}");
            }
        }

        private static void WriteObject(StringBuilder sb, JsonObject o, bool indent, string gap, int depth)
        {
            if (o.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            string inner = indent ? gap + "  " : gap;
            sb.Append('{');
            bool first = true;
            foreach (var pair in o)
            {
                if (!first) sb.Append(',');
                first = false;
                if (indent) sb.Append('\n').Append(inner);
                Quote(sb, pair.Key);
                sb.Append(indent ? ": " : ":");
                Write(sb, pair.Value, indent, inner, depth + 1);
            }
            if (indent) sb.Append('\n').Append(gap);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable list, bool indent, string gap, int depth)
        {
            string inner = indent ? gap + "  " : gap;
            bool first = true;
            sb.Append('[');
            foreach (var item in list)
            {
                if (!first) sb.Append(',');
                first = false;
                if (indent) sb.Append('\n').Append(inner);
                Write(sb, item, indent, inner, depth + 1);
            }
            if (indent && !first) sb.Append('\n').Append(gap);
            sb.Append(']');
        }

        /// <summary>A string as JSON.stringify quotes it.</summary>
        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            Quote(sb, s);
            return sb.ToString();
        }

        private static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); continue;
                    case '\\': sb.Append("\\\\"); continue;
                    case '\b': sb.Append("\\b"); continue;
                    case '\f': sb.Append("\\f"); continue;
                    case '\n': sb.Append("\\n"); continue;
                    case '\r': sb.Append("\\r"); continue;
                    case '\t': sb.Append("\\t"); continue;
                }
                if (c < 0x20)
                {
                    AppendUnicodeEscape(sb, c);
                }
                else if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                    {
                        sb.Append(c).Append(s[i + 1]);
                        i++;
                    }
                    else
                    {
                        AppendUnicodeEscape(sb, c); // well-formed JSON.stringify (ES2019)
                    }
                }
                else if (char.IsLowSurrogate(c))
                {
                    AppendUnicodeEscape(sb, c);
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append('"');
        }

        private static void AppendUnicodeEscape(StringBuilder sb, char c)
        {
            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// A number as JS's Number#toString writes it (NaN / ±Infinity as JSON's null): integers in full up to
        /// 1e21, the shortest digits that read back to the same double, exponent form outside 1e-7 .. 1e21.
        /// </summary>
        public static string FormatNumber(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            return NumberToString(v);
        }

        /// <summary>JS's String(number): like <see cref="FormatNumber"/> but "NaN", "Infinity", "-Infinity".</summary>
        public static string NumberToString(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsPositiveInfinity(v)) return "Infinity";
            if (double.IsNegativeInfinity(v)) return "-Infinity";
            if (v == 0) return "0"; // -0 too
            if (Math.Floor(v) == v && Math.Abs(v) < SafeInteger)
                return ((long)v).ToString(CultureInfo.InvariantCulture);

            ShortestDigits(Math.Abs(v), out string digits, out int n);
            int k = digits.Length;
            string body;
            if (k <= n && n <= 21)
            {
                body = digits + new string('0', n - k);
            }
            else if (0 < n && n <= 21)
            {
                body = digits.Substring(0, n) + "." + digits.Substring(n);
            }
            else if (-6 < n && n <= 0)
            {
                body = "0." + new string('0', -n) + digits;
            }
            else
            {
                int e = n - 1;
                string mantissa = k == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
                body = mantissa + "e" + (e >= 0 ? "+" : "-") + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
            }
            return v < 0 ? "-" + body : body;
        }

        /// <summary>
        /// The fewest decimal digits d1..dk that read back as <paramref name="v"/> (positive, finite), with n such
        /// that v = 0.d1..dk × 10^n (ECMA-262 Number::toString: k as small as possible, then the closest, then the
        /// even). Exact integer arithmetic (Burger and Dybvig's free-format algorithm), not the runtime's "E16" and
        /// double.Parse: Unity's Mono rounds both wrongly at 16–17 digits (about 2 % of random doubles printed
        /// otherwise than JS), and its Parse throws on "2E+308".
        /// </summary>
        private static void ShortestDigits(double v, out string digits, out int n)
        {
            long bits = BitConverter.DoubleToInt64Bits(v);
            int biased = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            BigInteger f = biased == 0 ? fraction : fraction | (1L << 52);
            int e = biased == 0 ? -1074 : biased - 1075;
            // v = f × 2^e. The gap below is half the gap above when f is 2^52 and a smaller exponent exists.
            bool asymmetric = fraction == 0 && biased > 1;
            bool even = f.IsEven; // round-half-even reading: a decimal on the boundary reads back as v only then
            BigInteger r, s, mPlus, mMinus;
            if (e >= 0)
            {
                BigInteger be = BigInteger.One << e;
                if (!asymmetric)
                {
                    r = f * be * 2;
                    s = 2;
                    mPlus = be;
                    mMinus = be;
                }
                else
                {
                    r = f * be * 4;
                    s = 4;
                    mPlus = be * 2;
                    mMinus = be;
                }
            }
            else if (!asymmetric)
            {
                r = f * 2;
                s = BigInteger.One << (1 - e);
                mPlus = BigInteger.One;
                mMinus = BigInteger.One;
            }
            else
            {
                r = f * 4;
                s = BigInteger.One << (2 - e);
                mPlus = 2;
                mMinus = BigInteger.One;
            }

            // r / s = v, mPlus / s and mMinus / s = half the gaps to the neighbours. Scale by the estimated 10^k
            // (never too high), then fix the estimate up by one when it was low.
            int k = (int)Math.Ceiling(Math.Log10(v) - 1e-10);
            if (k >= 0)
            {
                s *= BigInteger.Pow(10, k);
            }
            else
            {
                BigInteger scale = BigInteger.Pow(10, -k);
                r *= scale;
                mPlus *= scale;
                mMinus *= scale;
            }
            if (even ? r + mPlus >= s : r + mPlus > s)
            {
                k++;
            }
            else
            {
                r *= 10;
                mPlus *= 10;
                mMinus *= 10;
            }

            var sb = new StringBuilder(17);
            while (true)
            {
                int d = (int)BigInteger.DivRem(r, s, out r);
                bool low = even ? r <= mMinus : r < mMinus;
                bool high = even ? r + mPlus >= s : r + mPlus > s;
                if (!low && !high)
                {
                    sb.Append((char)('0' + d));
                    r *= 10;
                    mPlus *= 10;
                    mMinus *= 10;
                    continue;
                }
                if (low && high)
                {
                    // Both d and d + 1 read back: the closer one, and on a tie the even one.
                    int c = (r * 2).CompareTo(s);
                    if (c > 0 || (c == 0 && d % 2 == 1)) d++;
                }
                else if (high)
                {
                    d++;
                }
                sb.Append((char)('0' + d));
                break;
            }
            string text = sb.ToString().TrimEnd('0');
            digits = text.Length == 0 ? "0" : text;
            n = k;
        }

        private static readonly double[] ExactPowersOf10 =
        {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19,
            1e20, 1e21, 1e22,
        };

        /// <summary>
        /// The double nearest a JSON number literal (ties to even), as JSON.parse reads it. Exact integer arithmetic,
        /// not double.Parse: Unity's Mono reads some 17-digit literals as the neighbouring double.
        /// </summary>
        private static double ParseNumber(string literal)
        {
            bool negative = literal[0] == '-';
            int i = negative ? 1 : 0;
            var digits = new StringBuilder(literal.Length);
            int fractionDigits = 0;
            bool inFraction = false;
            int exponent = 0;
            for (; i < literal.Length; i++)
            {
                char c = literal[i];
                if (c == '.')
                {
                    inFraction = true;
                    continue;
                }
                if (c == 'e' || c == 'E')
                {
                    i++;
                    bool minus = literal[i] == '-';
                    if (literal[i] == '-' || literal[i] == '+') i++;
                    long value = 0;
                    for (; i < literal.Length; i++)
                    {
                        if (value < 100000000) value = value * 10 + (literal[i] - '0'); // past this it is ±Infinity or 0
                    }
                    exponent = (int)(minus ? -value : value);
                    break;
                }
                if (digits.Length > 0 || c != '0') digits.Append(c);
                else if (!inFraction) continue;
                if (inFraction) fractionDigits++;
            }
            double result;
            int length = digits.Length;
            long exp10 = (long)exponent - fractionDigits; // value = digits × 10^exp10
            if (length == 0) result = 0;
            else if (length + exp10 > 310) result = double.PositiveInfinity;
            else if (length + exp10 < -330) result = 0;
            else if (length <= 15 && exp10 >= -22 && exp10 <= 22)
            {
                // Clinger's fast path: both operands are exact, so the one operation is rounded once.
                double d = long.Parse(digits.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
                result = exp10 >= 0 ? d * ExactPowersOf10[exp10] : d / ExactPowersOf10[-exp10];
            }
            else
            {
                BigInteger num = BigInteger.Parse(digits.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
                BigInteger den = BigInteger.One;
                if (exp10 >= 0) num *= BigInteger.Pow(10, (int)exp10);
                else den = BigInteger.Pow(10, (int)-exp10);
                result = Nearest(num, den);
            }
            return negative ? -result : result;
        }

        /// <summary>The double nearest num / den (both positive), ties to even.</summary>
        private static double Nearest(BigInteger num, BigInteger den)
        {
            var twoPow52 = BigInteger.One << 52;
            var twoPow53 = BigInteger.One << 53;
            int shift = BitLength(num) - BitLength(den) - 53; // q = num / (den × 2^shift) has 52..54 bits
            BigInteger q, rem, divisor;
            while (true)
            {
                if (shift < -1074) shift = -1074; // subnormal: the last bit is 2^-1074
                Divide(num, den, shift, out q, out rem, out divisor);
                if (q >= twoPow53)
                {
                    shift++;
                    continue;
                }
                if (q < twoPow52 && shift > -1074)
                {
                    shift--;
                    continue;
                }
                break;
            }
            int c = (rem * 2).CompareTo(divisor);
            if (c > 0 || (c == 0 && !q.IsEven)) q++;
            if (q == twoPow53)
            {
                q = twoPow52;
                shift++;
            }
            if (shift > 971) return double.PositiveInfinity; // the top bit would be past 2^1023
            long mantissa = (long)q;
            long bits = mantissa < (1L << 52) ? mantissa : ((long)(shift + 1075) << 52) | (mantissa - (1L << 52));
            return BitConverter.Int64BitsToDouble(bits);
        }

        private static void Divide(BigInteger num, BigInteger den, int shift, out BigInteger q, out BigInteger rem, out BigInteger divisor)
        {
            if (shift >= 0)
            {
                divisor = den << shift;
                q = BigInteger.DivRem(num, divisor, out rem);
            }
            else
            {
                divisor = den;
                q = BigInteger.DivRem(num << -shift, den, out rem);
            }
        }

        private static int BitLength(BigInteger x)
        {
            byte[] bytes = x.ToByteArray(); // little-endian, positive
            int top = bytes.Length - 1;
            while (top > 0 && bytes[top] == 0) top--;
            int bitsInTop = 0;
            for (int b = bytes[top]; b != 0; b >>= 1) bitsInTop++;
            return top * 8 + bitsInTop;
        }

        // ------------------------------------------------------------------ reading

        /// <summary>The value of JSON text, as <c>JSON.parse(text)</c>. Throws <see cref="JsonException"/> on bad text.</summary>
        public static object? Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var reader = new Reader(text);
            reader.SkipSpace();
            object? value = reader.ReadValue(0);
            reader.SkipSpace();
            if (!reader.AtEnd) throw reader.Fail("Unexpected non-whitespace character after JSON");
            return value;
        }

        /// <summary>True when <paramref name="value"/> is a number with an integer value (JS Number.isInteger).</summary>
        public static bool IsInteger(object? value)
        {
            switch (value)
            {
                case int _:
                case long _:
                case short _:
                case sbyte _:
                case byte _:
                case ushort _:
                case uint _:
                case ulong _:
                    return true;
                case double d:
                    return !double.IsNaN(d) && !double.IsInfinity(d) && Math.Floor(d) == d;
                case float f:
                    return !float.IsNaN(f) && !float.IsInfinity(f) && Math.Floor(f) == f;
                case decimal m:
                    return decimal.Floor(m) == m;
                default:
                    return false;
            }
        }

        /// <summary>True when <paramref name="value"/> is a number (JS typeof === 'number').</summary>
        public static bool IsNumber(object? value) =>
            value is int || value is long || value is double || value is float || value is decimal
            || value is short || value is sbyte || value is byte || value is ushort || value is uint || value is ulong;

        /// <summary>A number value as the double JS would hold. Throws when <paramref name="value"/> is not a number.</summary>
        public static double ToDouble(object? value)
        {
            switch (value)
            {
                case int i: return i;
                case long l: return l;
                case double d: return d;
                case float f: return f;
                case decimal m: return (double)m;
                case short s: return s;
                case sbyte sb: return sb;
                case byte b: return b;
                case ushort us: return us;
                case uint ui: return ui;
                case ulong ul: return ul;
                default: throw new ArgumentException("not a number", nameof(value));
            }
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _pos;

            public Reader(string text)
            {
                _text = text;
            }

            public bool AtEnd => _pos >= _text.Length;

            public JsonException Fail(string message) => new JsonException($"{message} in JSON at position {_pos}", _pos);

            public void SkipSpace()
            {
                while (_pos < _text.Length)
                {
                    char c = _text[_pos];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _pos++;
                    else break;
                }
            }

            public object? ReadValue(int depth)
            {
                if (depth > MaxDepth) throw Fail($"JSON nested deeper than {MaxDepth}");
                if (AtEnd) throw Fail("Unexpected end of JSON input");
                char c = _text[_pos];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': ReadWord("true"); return true;
                    case 'f': ReadWord("false"); return false;
                    case 'n': ReadWord("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Fail($"Unexpected token '{c}'");
                }
            }

            private void ReadWord(string word)
            {
                if (_pos + word.Length > _text.Length || string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
                    throw Fail($"Unexpected token '{_text[_pos]}'");
                _pos += word.Length;
            }

            private JsonObject ReadObject(int depth)
            {
                var o = new JsonObject();
                _pos++; // {
                SkipSpace();
                if (!AtEnd && _text[_pos] == '}')
                {
                    _pos++;
                    return o;
                }
                while (true)
                {
                    SkipSpace();
                    if (AtEnd) throw Fail("Unexpected end of JSON input");
                    if (_text[_pos] != '"') throw Fail("Expected a double-quoted property name");
                    string key = ReadString();
                    SkipSpace();
                    if (AtEnd) throw Fail("Unexpected end of JSON input");
                    if (_text[_pos] != ':') throw Fail("Expected ':' after property name");
                    _pos++;
                    SkipSpace();
                    o[key] = ReadValue(depth + 1);
                    SkipSpace();
                    if (AtEnd) throw Fail("Unexpected end of JSON input");
                    char c = _text[_pos];
                    if (c == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (c == '}')
                    {
                        _pos++;
                        return o;
                    }
                    throw Fail("Expected ',' or '}' after property value");
                }
            }

            private List<object?> ReadArray(int depth)
            {
                var list = new List<object?>();
                _pos++; // [
                SkipSpace();
                if (!AtEnd && _text[_pos] == ']')
                {
                    _pos++;
                    return list;
                }
                while (true)
                {
                    SkipSpace();
                    list.Add(ReadValue(depth + 1));
                    SkipSpace();
                    if (AtEnd) throw Fail("Unexpected end of JSON input");
                    char c = _text[_pos];
                    if (c == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (c == ']')
                    {
                        _pos++;
                        return list;
                    }
                    throw Fail("Expected ',' or ']' after array element");
                }
            }

            private string ReadString()
            {
                _pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Fail("Unterminated string");
                    char c = _text[_pos];
                    if (c == '"')
                    {
                        _pos++;
                        return sb.ToString();
                    }
                    if (c < 0x20) throw Fail("Bad control character in string literal");
                    if (c != '\\')
                    {
                        sb.Append(c);
                        _pos++;
                        continue;
                    }
                    _pos++;
                    if (AtEnd) throw Fail("Unterminated string");
                    char e = _text[_pos];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 >= _text.Length) throw Fail("Bad Unicode escape");
                            int code = 0;
                            for (int i = 1; i <= 4; i++)
                            {
                                int h = HexValue(_text[_pos + i]);
                                if (h < 0) throw Fail("Bad Unicode escape");
                                code = code * 16 + h;
                            }
                            sb.Append((char)code);
                            _pos += 4;
                            break;
                        default:
                            throw Fail("Bad escaped character");
                    }
                    _pos++;
                }
            }

            private static int HexValue(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            private object ReadNumber()
            {
                int start = _pos;
                if (_text[_pos] == '-') _pos++;
                if (AtEnd) throw Fail("No number after minus sign");
                if (_text[_pos] == '0')
                {
                    _pos++;
                }
                else if (_text[_pos] >= '1' && _text[_pos] <= '9')
                {
                    while (!AtEnd && IsDigit(_text[_pos])) _pos++;
                }
                else
                {
                    throw Fail("No number after minus sign");
                }
                if (!AtEnd && _text[_pos] == '.')
                {
                    _pos++;
                    if (AtEnd || !IsDigit(_text[_pos])) throw Fail("Unterminated fractional number");
                    while (!AtEnd && IsDigit(_text[_pos])) _pos++;
                }
                if (!AtEnd && (_text[_pos] == 'e' || _text[_pos] == 'E'))
                {
                    _pos++;
                    if (!AtEnd && (_text[_pos] == '+' || _text[_pos] == '-')) _pos++;
                    if (AtEnd || !IsDigit(_text[_pos])) throw Fail("Exponent part is missing a number");
                    while (!AtEnd && IsDigit(_text[_pos])) _pos++;
                }
                return Narrow(ParseNumber(_text.Substring(start, _pos - start)));
            }

            private static bool IsDigit(char c) => c >= '0' && c <= '9';
        }

        /// <summary>int when the double is an int, long when it is an integer a double holds exactly, else the double.</summary>
        private static object Narrow(double d)
        {
            if (!double.IsInfinity(d) && Math.Floor(d) == d)
            {
                if (d >= int.MinValue && d <= int.MaxValue) return (int)d;
                if (Math.Abs(d) <= SafeInteger) return (long)d;
            }
            return d;
        }
    }

    /// <summary>
    /// A JSON object that keeps JavaScript's property order: keys that are array indices ("0", "1", … up to
    /// 2^32−2, written without leading zeros) first in ascending numeric order, then every other key in the
    /// order it was first set. Setting a key again keeps its place; removing it and setting it again moves it
    /// to the end. The indexer reads a missing key as null; <see cref="TryGet"/> tells missing from null.
    /// </summary>
    public sealed class JsonObject : IEnumerable<KeyValuePair<string, object?>>
    {
        private readonly Dictionary<string, object?> _values = new Dictionary<string, object?>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();
        private int _indexCount;

        public JsonObject()
        {
        }

        public int Count => _values.Count;

        /// <summary>The keys in JS property order.</summary>
        public IReadOnlyList<string> Keys => new ReadOnlyCollection<string>(_order.ToArray());

        /// <summary>The value at <paramref name="key"/>; null when there is none (set to add or replace).</summary>
        public object? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set => Set(key, value);
        }

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public bool TryGet(string key, out object? value) => _values.TryGetValue(key, out value);

        /// <summary>Sets <paramref name="key"/> (collection-initializer form; a repeated key replaces the value).</summary>
        public void Add(string key, object? value) => Set(key, value);

        /// <summary>Sets <paramref name="key"/> and returns this object, for chaining.</summary>
        public JsonObject Set(string key, object? value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!_values.ContainsKey(key))
            {
                if (TryArrayIndex(key, out uint index))
                {
                    int at = 0;
                    while (at < _indexCount && ArrayIndexOf(_order[at]) < index) at++;
                    _order.Insert(at, key);
                    _indexCount++;
                }
                else
                {
                    _order.Add(key);
                }
            }
            _values[key] = value;
            return this;
        }

        public bool Remove(string key)
        {
            if (!_values.Remove(key)) return false;
            int at = _order.IndexOf(key);
            _order.RemoveAt(at);
            if (at < _indexCount) _indexCount--;
            return true;
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            foreach (var key in _order.ToArray()) yield return new KeyValuePair<string, object?>(key, _values[key]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>A JsonObject of a dictionary's string keys, set in its enumeration order.</summary>
        public static JsonObject From(IDictionary dictionary)
        {
            var o = new JsonObject();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!(entry.Key is string key)) throw new ArgumentException("a JSON object's keys are strings", nameof(dictionary));
                o[key] = entry.Value;
            }
            return o;
        }

        /// <summary>True when <paramref name="key"/> is an array index (canonical, below 2^32 − 1).</summary>
        public static bool IsArrayIndex(string key) => TryArrayIndex(key, out _);

        private static uint ArrayIndexOf(string key)
        {
            TryArrayIndex(key, out uint index);
            return index;
        }

        private static bool TryArrayIndex(string key, out uint index)
        {
            index = 0;
            if (string.IsNullOrEmpty(key) || key.Length > 10) return false;
            if (key.Length > 1 && key[0] == '0') return false;
            ulong value = 0;
            foreach (char c in key)
            {
                if (c < '0' || c > '9') return false;
                value = value * 10 + (ulong)(c - '0');
            }
            if (value >= uint.MaxValue) return false; // 2^32 − 1 is not an index
            index = (uint)value;
            return true;
        }
    }

    /// <summary>JSON text that does not read (JS's SyntaxError from JSON.parse).</summary>
    public sealed class JsonException : FormatException
    {
        public JsonException(string message, int position) : base(message)
        {
            Position = position;
        }

        /// <summary>Where in the text the reader stopped.</summary>
        public int Position { get; }
    }
}
