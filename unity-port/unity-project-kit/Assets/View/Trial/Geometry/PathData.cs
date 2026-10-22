// SVG path data ("d"), parsed into subpaths of absolute segments (SVG 1.1 §8.3 / SVG 2 path grammar).
//
// Grammar notes (what the shipped strings use, kit.md §2.2 and §4.6):
//  - commands MmLlHhVvCcSsQqTtAaZz, relative and absolute;
//  - numbers: optional sign, leading dot, exponent; a second dot starts a new number (".7.5" = .7 .5), a sign
//    starts a new number ("1.4-1.1");
//  - the arc flags are single characters 0/1 and may be glued to what follows ("a3.5 3.5 0 110 7");
//  - implicit repeats: extra pairs after M are L, after m are l; other commands repeat themselves;
//  - Z returns to the subpath start; a drawing command after Z without M starts a new subpath there;
//  - S/s reflect the previous cubic's second control point (or use the current point), T/t the previous quad's.
// Like a browser, an error stops the parse and keeps what came before it (Error / ErrorAt say where).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Trial.Geometry
{
    public enum SegmentKind { Line, Cubic, Quad, Arc }

    /// <summary>One absolute segment. Arcs keep their SVG endpoint parameters; Flatten converts them.</summary>
    public readonly struct PathSegment
    {
        public readonly SegmentKind Kind;
        public readonly Vec2 From, C1, C2, To;
        public readonly float Rx, Ry, XAxisRotation;
        public readonly bool LargeArc, Sweep;

        private PathSegment(SegmentKind kind, Vec2 from, Vec2 c1, Vec2 c2, Vec2 to, float rx, float ry, float rot, bool large, bool sweep)
        {
            Kind = kind; From = from; C1 = c1; C2 = c2; To = to; Rx = rx; Ry = ry; XAxisRotation = rot; LargeArc = large; Sweep = sweep;
        }

        public static PathSegment Line(Vec2 from, Vec2 to) => new PathSegment(SegmentKind.Line, from, from, to, to, 0f, 0f, 0f, false, false);
        public static PathSegment Cubic(Vec2 from, Vec2 c1, Vec2 c2, Vec2 to) => new PathSegment(SegmentKind.Cubic, from, c1, c2, to, 0f, 0f, 0f, false, false);
        public static PathSegment Quad(Vec2 from, Vec2 c, Vec2 to) => new PathSegment(SegmentKind.Quad, from, c, c, to, 0f, 0f, 0f, false, false);
        public static PathSegment Arc(Vec2 from, float rx, float ry, float rotDeg, bool large, bool sweep, Vec2 to) =>
            new PathSegment(SegmentKind.Arc, from, from, to, to, rx, ry, rotDeg, large, sweep);
    }

    public sealed class SubPath
    {
        public readonly Vec2 Start;
        internal readonly List<PathSegment> SegmentList = new List<PathSegment>();
        public IReadOnlyList<PathSegment> Segments => SegmentList;
        public bool Closed { get; internal set; }

        internal SubPath(Vec2 start) { Start = start; }

        public Vec2 End => SegmentList.Count == 0 ? Start : SegmentList[SegmentList.Count - 1].To;
    }

    public sealed class PathData
    {
        private readonly List<SubPath> _subPaths = new List<SubPath>();

        public IReadOnlyList<SubPath> SubPaths => _subPaths;
        /// <summary>The source string.</summary>
        public string Source { get; private set; }
        /// <summary>Null when the whole string parsed; otherwise what stopped the parse (everything before it is kept).</summary>
        public string Error { get; private set; }
        /// <summary>Index into Source where the parse stopped, or −1.</summary>
        public int ErrorAt { get; private set; } = -1;

        // Engine-free, so it cannot hook Unity's SubsystemRegistration reset (TRIAL-PORT.md §8); it holds only
        // immutable parse results keyed by their string, so surviving a play session changes nothing. It is bounded
        // so that a caller passing per-frame strings (an animated path) cannot grow it without limit.
        private static readonly Dictionary<string, PathData> Cache = new Dictionary<string, PathData>(StringComparer.Ordinal);
        internal const int CacheLimit = 1024;

        /// <summary>
        /// Parse once per distinct string (the result is immutable, so it is shared). For the fixed strings of the
        /// data tables; a path built anew every frame should use <see cref="Parse"/>.
        /// </summary>
        public static PathData Cached(string d)
        {
            d = d ?? string.Empty;
            lock (Cache)
            {
                if (Cache.TryGetValue(d, out PathData p)) return p;
                p = Parse(d);
                if (Cache.Count >= CacheLimit) Cache.Clear();
                Cache[d] = p;
                return p;
            }
        }

        public static PathData Parse(string d)
        {
            var pd = new PathData { Source = d ?? string.Empty };
            new Parser(pd.Source, pd).Run();
            return pd;
        }

        /// <summary>Bounds of every endpoint and control point (arcs: their endpoints only).</summary>
        public RectF ControlBounds()
        {
            var pts = new List<Vec2>();
            foreach (SubPath sp in _subPaths)
            {
                pts.Add(sp.Start);
                foreach (PathSegment s in sp.SegmentList)
                {
                    if (s.Kind == SegmentKind.Cubic || s.Kind == SegmentKind.Quad) { pts.Add(s.C1); pts.Add(s.C2); }
                    pts.Add(s.To);
                }
            }
            return RectF.Bounds(pts);
        }

        private sealed class Parser
        {
            private readonly string _s;
            private readonly PathData _pd;
            private int _i;

            public Parser(string s, PathData pd) { _s = s; _pd = pd; _i = 0; }

            public void Run()
            {
                Vec2 cur = Vec2.Zero, start = Vec2.Zero;
                Vec2 lastC2 = Vec2.Zero, lastQ = Vec2.Zero;
                char prev = '\0';          // upper-case kind of the previous segment command (C/S/Q/T matter)
                char cmd = '\0';
                bool first = true;
                SubPath sp = null;
                bool needSubPath = false;   // after Z: the next drawing command starts a subpath at `start`

                while (true)
                {
                    SkipWsp();
                    if (_i >= _s.Length) return;
                    char c = _s[_i];
                    if (IsCommand(c))
                    {
                        cmd = c;
                        _i++;
                    }
                    else
                    {
                        if (cmd == '\0') { Fail("path must start with M/m"); return; }
                        if (cmd == 'Z' || cmd == 'z') { Fail("number after Z"); return; }
                        if (c == ',') { _i++; SkipWsp(); }
                        if (!StartsNumber()) { Fail("unexpected '" + c + "'"); return; }
                        // implicit repeat: M → L, m → l, others repeat themselves
                        if (cmd == 'M') cmd = 'L';
                        else if (cmd == 'm') cmd = 'l';
                    }

                    if (first && cmd != 'M' && cmd != 'm') { Fail("path must start with M/m"); return; }

                    bool rel = char.IsLower(cmd);
                    char up = char.ToUpperInvariant(cmd);
                    Vec2 origin = rel ? cur : Vec2.Zero;

                    if (up == 'Z')
                    {
                        if (sp != null) sp.Closed = true;
                        cur = start;
                        needSubPath = true;
                        prev = 'Z';
                        continue;
                    }

                    if (up == 'M')
                    {
                        if (!ReadPair(out Vec2 p)) { Fail("M needs x y"); return; }
                        // a relative m at the very start is absolute (relative to 0,0)
                        p = first ? p : origin + p;
                        first = false;
                        sp = new SubPath(p);
                        _pd._subPaths.Add(sp);
                        cur = start = p;
                        needSubPath = false;
                        prev = 'M';
                        continue;
                    }

                    if (needSubPath || sp == null)
                    {
                        sp = new SubPath(cur);
                        _pd._subPaths.Add(sp);
                        start = cur;
                        needSubPath = false;
                    }

                    switch (up)
                    {
                        case 'L':
                        {
                            if (!ReadPair(out Vec2 p)) { Fail("L needs x y"); return; }
                            p = origin + p;
                            sp.SegmentList.Add(PathSegment.Line(cur, p));
                            cur = p;
                            break;
                        }
                        case 'H':
                        {
                            if (!ReadNumber(out float x)) { Fail("H needs x"); return; }
                            var p = new Vec2(rel ? cur.X + x : x, cur.Y);
                            sp.SegmentList.Add(PathSegment.Line(cur, p));
                            cur = p;
                            break;
                        }
                        case 'V':
                        {
                            if (!ReadNumber(out float y)) { Fail("V needs y"); return; }
                            var p = new Vec2(cur.X, rel ? cur.Y + y : y);
                            sp.SegmentList.Add(PathSegment.Line(cur, p));
                            cur = p;
                            break;
                        }
                        case 'C':
                        {
                            if (!ReadPair(out Vec2 c1) || !ReadPair(out Vec2 c2) || !ReadPair(out Vec2 p)) { Fail("C needs 6 numbers"); return; }
                            c1 = origin + c1; c2 = origin + c2; p = origin + p;
                            sp.SegmentList.Add(PathSegment.Cubic(cur, c1, c2, p));
                            lastC2 = c2;
                            cur = p;
                            break;
                        }
                        case 'S':
                        {
                            if (!ReadPair(out Vec2 c2) || !ReadPair(out Vec2 p)) { Fail("S needs 4 numbers"); return; }
                            Vec2 c1 = prev == 'C' || prev == 'S' ? cur * 2f - lastC2 : cur;
                            c2 = origin + c2; p = origin + p;
                            sp.SegmentList.Add(PathSegment.Cubic(cur, c1, c2, p));
                            lastC2 = c2;
                            cur = p;
                            break;
                        }
                        case 'Q':
                        {
                            if (!ReadPair(out Vec2 q) || !ReadPair(out Vec2 p)) { Fail("Q needs 4 numbers"); return; }
                            q = origin + q; p = origin + p;
                            sp.SegmentList.Add(PathSegment.Quad(cur, q, p));
                            lastQ = q;
                            cur = p;
                            break;
                        }
                        case 'T':
                        {
                            if (!ReadPair(out Vec2 p)) { Fail("T needs x y"); return; }
                            Vec2 q = prev == 'Q' || prev == 'T' ? cur * 2f - lastQ : cur;
                            p = origin + p;
                            sp.SegmentList.Add(PathSegment.Quad(cur, q, p));
                            lastQ = q;
                            cur = p;
                            break;
                        }
                        case 'A':
                        {
                            if (!ReadNumber(out float rx) || !ReadNumber(out float ry) || !ReadNumber(out float rot)
                                || !ReadFlag(out bool large) || !ReadFlag(out bool sweep) || !ReadPair(out Vec2 p))
                            {
                                Fail("A needs rx ry rot large sweep x y");
                                return;
                            }
                            p = origin + p;
                            sp.SegmentList.Add(PathSegment.Arc(cur, rx, ry, rot, large, sweep, p));
                            cur = p;
                            break;
                        }
                    }
                    prev = up;
                }
            }

            private void Fail(string why)
            {
                _pd.Error = why + " at " + _i.ToString(CultureInfo.InvariantCulture);
                _pd.ErrorAt = _i;
            }

            private static bool IsCommand(char c)
            {
                switch (c)
                {
                    case 'M': case 'm': case 'L': case 'l': case 'H': case 'h': case 'V': case 'v':
                    case 'C': case 'c': case 'S': case 's': case 'Q': case 'q': case 'T': case 't':
                    case 'A': case 'a': case 'Z': case 'z':
                        return true;
                    default:
                        return false;
                }
            }

            private static bool IsWsp(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f';

            private void SkipWsp()
            {
                while (_i < _s.Length && IsWsp(_s[_i])) _i++;
            }

            /// <summary>Whitespace and at most one comma between arguments.</summary>
            private void SkipSeparator()
            {
                SkipWsp();
                if (_i < _s.Length && _s[_i] == ',') { _i++; SkipWsp(); }
            }

            private bool StartsNumber()
            {
                if (_i >= _s.Length) return false;
                char c = _s[_i];
                return char.IsDigit(c) || c == '.' || c == '-' || c == '+';
            }

            private bool ReadPair(out Vec2 p)
            {
                p = Vec2.Zero;
                if (!ReadNumber(out float x) || !ReadNumber(out float y)) return false;
                p = new Vec2(x, y);
                return true;
            }

            private bool ReadNumber(out float v)
            {
                v = 0f;
                SkipSeparator();
                int st = _i, n = _s.Length;
                int i = _i;
                if (i < n && (_s[i] == '+' || _s[i] == '-')) i++;
                int intDigits = 0, fracDigits = 0;
                while (i < n && _s[i] >= '0' && _s[i] <= '9') { i++; intDigits++; }
                if (i < n && _s[i] == '.')
                {
                    int dot = i;
                    i++;
                    while (i < n && _s[i] >= '0' && _s[i] <= '9') { i++; fracDigits++; }
                    if (fracDigits == 0 && intDigits == 0) { i = dot; }
                }
                if (intDigits + fracDigits == 0) return false;
                if (i < n && (_s[i] == 'e' || _s[i] == 'E'))
                {
                    int j = i + 1;
                    if (j < n && (_s[j] == '+' || _s[j] == '-')) j++;
                    int expDigits = 0;
                    while (j < n && _s[j] >= '0' && _s[j] <= '9') { j++; expDigits++; }
                    if (expDigits > 0) i = j;
                }
                string tok = _s.Substring(st, i - st);
                if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv)) return false;
                v = (float)dv;
                _i = i;
                return true;
            }

            private bool ReadFlag(out bool f)
            {
                f = false;
                SkipSeparator();
                if (_i >= _s.Length) return false;
                char c = _s[_i];
                if (c != '0' && c != '1') return false;
                f = c == '1';
                _i++;
                return true;
            }
        }
    }
}
