// CSS transitions on state changes (kit.md §3.4, §4.11): an element's declarations are its base rule plus the rules of
// its states (`:hover`, `.is-primary`, `[disabled]`…, registered in cascade order); when a state turns on or off, every
// property a `transition` covers runs from its current value to the new one over the transition's duration, delay and
// timing function, as CSS Transitions does (an interrupted transition starts from where it stood; going back to where a
// running one started shortens it by the reversing factor). Properties no transition covers change at once.
//
// What is written where (the element's own pieces, found on its GameObject or assigned):
//   opacity, transform (+ translate / scale / rotate), width, height, filter, display → UiNode
//   background-color, border-color, box-shadow (and at once: background images, border widths / styles, radius, outline) → BoxGraphic
//   color, letter-spacing, text-shadow (and at once: font, line-height, white-space, text-align) → the TMP label / its TextGlow
//   stroke, stroke-width, stroke-dashoffset → PathGraphic
//
//   var st = Transitions.Set(btn, "border-color .14s, background .14s");     // or a `transition:` in the base rule
//   st.Base(CommonStyles.BtnCss).State(":hover", CommonStyles.BtnHoverCss);
//   st.SetState(":hover", true);
//
// Time: CssTime (the document clock, real time) unless a scene clock is given. Inside the battle the JS declares no
// transitions (battle.css:47-52), so nothing animates there unless a rule says so.
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>transition: … on an element (the static entry points).</summary>
    public static class Transitions
    {
        /// <summary>The element's state machine with its `transition` property set (inline: wins over the rules' own).</summary>
        public static CssStates Set(Component element, string transitionCss)
        {
            CssStates s = CssStates.Of(element);
            s.SetTransitions(transitionCss);
            return s;
        }

        /// <summary>Turns a state of the element on or off (its transitions run).</summary>
        public static void SetState(Component element, string state, bool on) => CssStates.Of(element).SetState(state, on);
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class CssStates : MonoBehaviour
    {
        private sealed class Rule
        {
            public string Name;
            public string Css;
            public bool On;
        }

        private sealed class Track
        {
            public object Start, End, ReversingStart;
            public float T0, Delay, Duration, Factor = 1f;
            public EaseFn Ease;

            public float Portion(float now)
            {
                if (Duration <= 0f) return now - T0 >= Delay ? 1f : 0f;
                float p = (now - T0 - Delay) / Duration;
                if (p <= 0f) return 0f;
                if (p >= 1f) return 1f;
                return (Ease ?? Trial.Ease.Css)(p);
            }

            public bool Done(float now) => now - T0 - Delay >= Duration;
        }

        // the managed properties
        private const string POpacity = "opacity", PTransform = "transform", PWidth = "width", PHeight = "height", PFilter = "filter",
            PBg = "background-color", PBorder = "border-color", PShadow = "box-shadow", PColor = "color", PSpacing = "letter-spacing",
            PTextShadow = "text-shadow", PStroke = "stroke", PStrokeWidth = "stroke-width", PDash = "stroke-dashoffset";

        private static readonly string[] Interpolated =
            { POpacity, PTransform, PWidth, PHeight, PFilter, PBg, PBorder, PShadow, PColor, PSpacing, PTextShadow, PStroke, PStrokeWidth, PDash };

        private string _base = "";
        private readonly List<Rule> _rules = new List<Rule>();
        private List<TransitionSpec> _override;
        private readonly HashSet<string> _managed = new HashSet<string>(StringComparer.Ordinal);
        private bool _manageBox, _manageText, _manageDisplay;
        private readonly Dictionary<string, object> _current = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, Track> _tracks = new Dictionary<string, Track>(StringComparer.Ordinal);
        private CssBlock _block;
        private bool _applied;
        private IDisposable _frame;
        private TrialClock _frameClock;

        // the original look of the pieces (what an undeclared property falls back to)
        private TextStyle _origText;
        private float _origW = float.NaN, _origH = float.NaN;
        private float _origStrokeWidth = float.NaN;
        private Rgba? _origStroke;

        /// <summary>The scene clock to run on (null = CssTime, the document clock).</summary>
        public TrialClock Clock;

        /// <summary>With a Clock: follow its scene time (else its real time).</summary>
        public bool SceneTime;

        public UiNode Node;
        public BoxGraphic Box;
        public TMP_Text Text;
        public PathGraphic Path;

        /// <summary>The state machine of an element (added when missing; the node, box, label and path on it are bound).</summary>
        public static CssStates Of(Component element)
        {
            if (!element) throw new ArgumentNullException(nameof(element));
            CssStates s = element.GetComponent<CssStates>();
            if (!s) s = element.gameObject.AddComponent<CssStates>();
            return s;
        }

        private void Awake() => Bind();

        // Finds the pieces not assigned yet. Apply calls it too: Awake waits for the first activation, so a state machine
        // added under an inactive parent (a button built hidden) would otherwise style nothing.
        private void Bind()
        {
            var rt = transform as RectTransform;
            if (rt && !Node) Node = UiNode.Of(rt);
            if (!Box) Box = GetComponent<BoxGraphic>();
            if (!Text) Text = GetComponent<TMP_Text>();
            if (!Path) Path = GetComponent<PathGraphic>();
        }

        // ---- the rules ---------------------------------------------------------------------------------------

        /// <summary>The element's base declarations (its class rule; may hold `transition:`).</summary>
        public CssStates Base(string css)
        {
            _base = css ?? "";
            Reindex();
            return this;
        }

        /// <summary>
        /// A state's declarations (":hover", ".is-primary", "[disabled]"), registered in cascade order: a later rule wins
        /// over an earlier one (order them by specificity, then by their place in the stylesheet).
        /// </summary>
        public CssStates State(string name, string css, bool on = false)
        {
            Rule r = _rules.Find(x => x.Name == name);
            if (r == null) _rules.Add(r = new Rule { Name = name });
            r.Css = css ?? "";
            r.On = on;
            Reindex();
            return this;
        }

        /// <summary>The element's `transition` property (inline: it wins over the rules'); null = the rules' own.</summary>
        public CssStates SetTransitions(string css)
        {
            _override = css == null ? null : TransitionSpec.ParseList(css);
            return this;
        }

        /// <summary>Turns a state on or off; the change transitions. Returns whether anything changed.</summary>
        public bool SetState(string name, bool on) => SetState(name, on, true);

        /// <summary>Turns a state on or off; apply false only records it (several changes, then one Apply()).</summary>
        public bool SetState(string name, bool on, bool apply)
        {
            Rule r = _rules.Find(x => x.Name == name);
            if (r == null)
            {
                if (!on) return false;
                _rules.Add(r = new Rule { Name = name, Css = "" });
            }
            if (r.On == on && _applied) return false;
            r.On = on;
            if (apply) Apply();
            return true;
        }

        public bool Has(string name)
        {
            Rule r = _rules.Find(x => x.Name == name);
            return r != null && r.On;
        }

        /// <summary>The declarations in force (base + the states that are on).</summary>
        public string Effective()
        {
            var sb = new System.Text.StringBuilder(_base);
            foreach (Rule r in _rules)
                if (r.On && !string.IsNullOrEmpty(r.Css))
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ';') sb.Append(';');
                    sb.Append(r.Css);
                }
            return sb.ToString();
        }

        // Which properties any rule declares: only those are managed (an undeclared property is left to the scene).
        private void Reindex()
        {
            _managed.Clear();
            _manageBox = _manageText = _manageDisplay = false;
            Index(_base);
            foreach (Rule r in _rules) Index(r.Css);
            if (_applied) Apply();
        }

        private void Index(string css)
        {
            foreach (string prop in PropertyNames(css))
            {
                switch (prop)
                {
                    case "opacity": _managed.Add(POpacity); break;
                    case "transform":
                    case "translate":
                    case "scale":
                    case "rotate": _managed.Add(PTransform); break;
                    case "width": _managed.Add(PWidth); break;
                    case "height": _managed.Add(PHeight); break;
                    case "filter": _managed.Add(PFilter); break;
                    case "display": _manageDisplay = true; break;
                    case "box-shadow": _managed.Add(PShadow); _manageBox = true; break;
                    case "color": _managed.Add(PColor); break;
                    case "letter-spacing": _managed.Add(PSpacing); break;
                    case "text-shadow": _managed.Add(PTextShadow); break;
                    case "stroke": _managed.Add(PStroke); break;
                    case "stroke-width": _managed.Add(PStrokeWidth); break;
                    case "stroke-dashoffset": _managed.Add(PDash); break;
                    default:
                        if (prop.StartsWith("background", StringComparison.Ordinal)) { _managed.Add(PBg); _manageBox = true; }
                        else if (prop.StartsWith("border", StringComparison.Ordinal) || prop.StartsWith("outline", StringComparison.Ordinal))
                        {
                            _manageBox = true;
                            if (prop != "border-radius" && !prop.StartsWith("outline", StringComparison.Ordinal)) _managed.Add(PBorder);
                        }
                        else if (prop.StartsWith("font", StringComparison.Ordinal) || prop == "line-height" || prop == "white-space" || prop == "text-align")
                            _manageText = true;
                        break;
                }
            }
        }

        /// <summary>The property names of a declaration block (lower case, in order).</summary>
        public static List<string> PropertyNames(string css)
        {
            var o = new List<string>();
            if (string.IsNullOrEmpty(css)) return o;
            int depth = 0, start = 0;
            for (int i = 0; i <= css.Length; i++)
            {
                char c = i < css.Length ? css[i] : ';';
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ';' && depth <= 0)
                {
                    string d = css.Substring(start, i - start);
                    int colon = d.IndexOf(':');
                    if (colon > 0) o.Add(d.Substring(0, colon).Trim().ToLowerInvariant());
                    start = i + 1;
                }
            }
            return o;
        }

        // ---- applying -------------------------------------------------------------------------------------------

        private float Now
        {
            get
            {
                TrialClock c = Clock ?? CssTime.Clock;
                return Clock != null && SceneTime ? c.Now : c.RealNow;
            }
        }

        /// <summary>Applies the declarations in force; instant = no transitions (the first apply is always instant).</summary>
        public void Apply(bool instant = false)
        {
            Bind();
            CaptureOriginals();
            _block = global::Trial.Geometry.Css.Parse(Effective());
            List<TransitionSpec> specs = _override ?? _block.Transitions ?? new List<TransitionSpec>();
            bool first = !_applied;
            _applied = true;
            float now = Now;
            foreach (string p in Interpolated)
            {
                if (!_managed.Contains(p) || !Bound(p)) continue;
                object target = Read(p, _block);
                if (target == null) continue;
                if (first || instant || !_current.ContainsKey(p))
                {
                    _tracks.Remove(p);
                    _current[p] = target;
                    continue;
                }
                StartTransition(p, target, Spec(specs, p), now);
            }
            if (_manageDisplay && Node) Node.Visible = _block.Layout.Display != "none";
            if (_manageText && Text) ApplyTextImmediate();
            Flush(all: true);
            EnsureTicking();
        }

        private bool Bound(string p)
        {
            switch (p)
            {
                case POpacity:
                case PTransform:
                case PWidth:
                case PHeight:
                case PFilter: return Node;
                case PBg:
                case PBorder:
                case PShadow: return Box;
                case PColor:
                case PSpacing:
                case PTextShadow: return Text;
                default: return Path;
            }
        }

        private void CaptureOriginals()
        {
            if (Text && _origText == null) _origText = global::Trial.Kit.Label.StyleOf(Text)?.Clone() ?? new TextStyle();
            if (Node && float.IsNaN(_origW)) { _origW = Node.W; _origH = Node.H; }
            if (Path && float.IsNaN(_origStrokeWidth))
            {
                foreach (PathLayer l in Path.Layers)
                    if (l.Stroke != null)
                    {
                        _origStrokeWidth = l.StrokeStyle?.Width ?? 1f;
                        if (l.Stroke is SolidPaint sp) _origStroke = sp.Color;
                        break;
                    }
            }
        }

        private static TransitionSpec Spec(List<TransitionSpec> specs, string property)
        {
            // the last transition that names the property wins (CSS: later list items override earlier ones)
            TransitionSpec found = null;
            foreach (TransitionSpec s in specs) if (s.Covers(property)) found = s;
            return found;
        }

        private void StartTransition(string p, object end, TransitionSpec spec, float now)
        {
            object cur = _current[p];
            _tracks.TryGetValue(p, out Track old);
            if (old != null && Same(end, old.End)) return;
            float dur = spec != null ? Math.Max(0f, spec.DurationMs) : 0f;
            float delay = spec != null ? spec.DelayMs : 0f;
            if (CssTime.ReducedMotion && spec != null) dur = Math.Min(dur, 1f) > 0f ? 1f : 0f;
            if (spec == null || dur + delay <= 0f || Same(cur, end))
            {
                _tracks.Remove(p);
                _current[p] = end;
                return;
            }
            float factor = 1f;
            object revStart = cur;
            if (old != null && Same(end, old.ReversingStart))
            {
                // CSS Transitions §3: the reversing shortening factor
                float portion = old.Portion(now);
                factor = Math.Abs(portion * old.Factor + 1f - old.Factor);
                if (factor > 1f) factor = 1f;
                dur *= factor;
                if (delay < 0f) delay *= factor;
                revStart = old.End;
            }
            _tracks[p] = new Track { Start = cur, End = end, ReversingStart = revStart, T0 = now, Delay = delay, Duration = dur, Factor = factor, Ease = spec.Easing };
        }

        private void EnsureTicking()
        {
            TrialClock c = Clock ?? CssTime.Clock;
            if (_tracks.Count == 0)
            {
                _frame?.Dispose();
                _frame = null;
                return;
            }
            if (_frame != null && _frameClock == c && c.Alive) return;
            _frame?.Dispose();
            _frameClock = c;
            _frame = c.OnFrame((dt, real) => Tick());
        }

        private void Tick()
        {
            if (!this) { _frame?.Dispose(); _frame = null; return; }
            float now = Now;
            var done = new List<string>();
            foreach (KeyValuePair<string, Track> kv in _tracks)
            {
                Track t = kv.Value;
                _current[kv.Key] = Lerp(t.Start, t.End, t.Portion(now));
                if (t.Done(now)) done.Add(kv.Key);
            }
            _lastDone.Clear();
            foreach (string k in done)
            {
                _current[k] = _tracks[k].End;
                _tracks.Remove(k);
                _lastDone.Add(k);
            }
            Flush(all: false);
            _lastDone.Clear();
            if (_tracks.Count == 0)
            {
                _frame?.Dispose();
                _frame = null;
            }
        }

        private readonly HashSet<string> _written = new HashSet<string>(StringComparer.Ordinal);

        // Writes the current values into the pieces (all = after a rule change: every managed value; else the running ones).
        private void Flush(bool all)
        {
            _written.Clear();
            foreach (string p in Interpolated)
            {
                if (!_current.ContainsKey(p)) continue;
                if (!all && !_tracks.ContainsKey(p) && !JustDone(p)) continue;
                _written.Add(p);
            }
            if (_written.Count == 0 && !all) return;
            UiNode n = Node;
            if (n)
            {
                if (_written.Contains(POpacity)) n.Opacity = (float)_current[POpacity];
                if (_written.Contains(PTransform))
                {
                    TransformParts tp = ((TransformSpec)_current[PTransform]).Compose();
                    n.SetTransform(tp.Tx, tp.Ty, tp.Sx, tp.Sy, tp.Rot, tp.TxPercent / 100f, tp.TyPercent / 100f);
                }
                if (_written.Contains(PWidth)) n.W = (float)_current[PWidth];
                if (_written.Contains(PHeight)) n.H = (float)_current[PHeight];
                if (_written.Contains(PFilter)) n.Filter = (ColorFilter)_current[PFilter];
            }
            if (Box && (_manageBox && all || _written.Contains(PBg) || _written.Contains(PBorder) || _written.Contains(PShadow)))
            {
                BoxStyle s = (_block?.Box ?? new BoxStyle()).Clone();
                if (_current.TryGetValue(PBg, out object bg)) s.BackgroundColor = (Rgba)bg;
                if (_current.TryGetValue(PBorder, out object bc))
                {
                    var c = (Rgba[])bc;
                    s.Top = s.Top.WithColor(c[0]); s.Right = s.Right.WithColor(c[1]); s.Bottom = s.Bottom.WithColor(c[2]); s.Left = s.Left.WithColor(c[3]);
                }
                if (_current.TryGetValue(PShadow, out object sh)) s.Shadows = new List<BoxShadow>((BoxShadow[])sh);
                Box.Style = s;
            }
            if (Text)
            {
                if (_written.Contains(PSpacing))
                {
                    TextStyle st = (global::Trial.Kit.Label.StyleOf(Text) ?? _origText ?? new TextStyle()).Clone();
                    float em = (float)_current[PSpacing];
                    if (Math.Abs(st.LetterSpacingEm - em) > 1e-5f)
                    {
                        st.LetterSpacingEm = em;
                        global::Trial.Kit.Label.Apply(Text, st);
                    }
                }
                if (_written.Contains(PColor)) global::Trial.Kit.Label.SetColor(Text, (Rgba)_current[PColor]);
                if (_written.Contains(PTextShadow))
                {
                    var ts = (TextShadow[])_current[PTextShadow];
                    TextGlow g = TextGlow.Of(Text);
                    if (g) g.SetShadows(ts);
                    else if (ts.Length > 0) TextGlow.For(Text, ts);
                }
            }
            if (Path)
            {
                bool changed = false;
                if (_written.Contains(PStroke) || _written.Contains(PStrokeWidth))
                {
                    foreach (PathLayer l in Path.Layers)
                    {
                        if (l.Stroke == null) continue;
                        if (_current.TryGetValue(PStroke, out object sc)) l.Stroke = new SolidPaint((Rgba)sc);
                        if (_current.TryGetValue(PStrokeWidth, out object sw))
                        {
                            l.StrokeStyle = (l.StrokeStyle ?? new StrokeStyle()).Clone();
                            l.StrokeStyle.Width = (float)sw;
                        }
                        changed = true;
                    }
                }
                if (changed) Path.LayersChanged();
                if (_written.Contains(PDash)) Path.DashOffset = (float)_current[PDash];
            }
        }

        // A track that finished this frame was removed before Flush: its end value still has to be written.
        private bool JustDone(string p) => _lastDone.Contains(p);
        private readonly HashSet<string> _lastDone = new HashSet<string>(StringComparer.Ordinal);

        private void ApplyTextImmediate()
        {
            TextStyle cur = global::Trial.Kit.Label.StyleOf(Text) ?? _origText ?? new TextStyle();
            TextStyle merged = Merge(_origText ?? cur, _block.Text);
            // keep the interpolated values
            merged.Color = cur.Color;
            merged.LetterSpacingEm = cur.LetterSpacingEm;
            merged.Shadows = cur.Shadows;
            if (merged.LayoutKey() != cur.LayoutKey() || merged.Align != cur.Align) global::Trial.Kit.Label.Apply(Text, merged);
        }

        private static TextStyle Merge(TextStyle orig, TextStyleData d)
        {
            TextStyle s = orig.Clone();
            if (d == null) return s;
            if (d.Family.HasValue) s.Family = d.Family.Value;
            if (d.Weight.HasValue) s.Weight = d.Weight.Value;
            if (d.Size.HasValue) s.Size = d.Size.Value;
            if (d.LineHeightPx.HasValue) s.LineHeight = d.LineHeightPx.Value;
            else if (d.LineHeightRatio.HasValue) s.LineHeight = d.LineHeightRatio.Value * s.Size;
            if (d.WhiteSpace.HasValue) s.WhiteSpace = d.WhiteSpace.Value;
            if (d.Align.HasValue) s.Align = d.Align.Value;
            return s;
        }

        // ---- values ----------------------------------------------------------------------------------------------

        private object Read(string p, CssBlock b)
        {
            switch (p)
            {
                case POpacity: return b.Opacity ?? 1f;
                case PTransform: return b.CombinedTransform();
                case PWidth: return b.Layout.Width.HasValue ? b.Layout.Width.Value.Resolve(ParentSize().x) : _origW;
                case PHeight: return b.Layout.Height.HasValue ? b.Layout.Height.Value.Resolve(ParentSize().y) : _origH;
                case PFilter: return b.Filter?.Color ?? ColorFilter.None;
                case PBg: return b.Box.BackgroundColor;
                case PBorder: return new[] { b.Box.Top.Color, b.Box.Right.Color, b.Box.Bottom.Color, b.Box.Left.Color };
                case PShadow: return b.Box.Shadows.ToArray();
                case PColor: return b.Text.Color ?? _origText?.Color ?? DsTokens.Ink;
                case PSpacing:
                {
                    float size = b.Text.Size ?? _origText?.Size ?? 16f;
                    if (b.Text.LetterSpacingEm.HasValue) return b.Text.LetterSpacingEm.Value;
                    if (b.Text.LetterSpacingPx.HasValue) return size > 0f ? b.Text.LetterSpacingPx.Value / size : 0f;
                    return _origText?.LetterSpacingEm ?? 0f;
                }
                case PTextShadow: return b.Text.Shadows != null ? b.Text.Shadows.ToArray() : Array.Empty<TextShadow>();
                case PStroke: return b.Svg.Stroke ?? _origStroke ?? Rgba.Transparent;
                case PStrokeWidth: return b.Svg.StrokeWidth ?? (float.IsNaN(_origStrokeWidth) ? 1f : _origStrokeWidth);
                case PDash: return b.Svg.DashOffset ?? 0f;
            }
            return null;
        }

        private Vector2 ParentSize()
        {
            var p = transform.parent as RectTransform;
            return p ? p.rect.size : new Vector2(TrialStage.W, TrialStage.H);
        }

        private static object Lerp(object a, object b, float t)
        {
            switch (a)
            {
                case float fa: return fa + ((float)b - fa) * t;
                case Rgba ca: return Rgba.LerpPremul(ca, (Rgba)b, t);
                case Rgba[] ra:
                {
                    var rb = (Rgba[])b;
                    var o = new Rgba[ra.Length];
                    for (int i = 0; i < o.Length; i++) o[i] = Rgba.LerpPremul(ra[i], rb[i], t);
                    return o;
                }
                case BoxShadow[] sa: return CssKeyframes.LerpShadows(sa, (BoxShadow[])b, t);
                case TextShadow[] xa: return CssKeyframes.LerpTextShadows(xa, (TextShadow[])b, t);
                case TransformSpec ta: return TransformSpec.Interpolate(ta, (TransformSpec)b, t);
                case ColorFilter fa2:
                {
                    var fb = (ColorFilter)b;
                    return new ColorFilter(fa2.Brightness + (fb.Brightness - fa2.Brightness) * t, fa2.Grayscale + (fb.Grayscale - fa2.Grayscale) * t);
                }
            }
            return t < 0.5f ? a : b;
        }

        private static bool Same(object a, object b)
        {
            if (a == null || b == null) return a == b;
            switch (a)
            {
                case float fa: return b is float fb && Math.Abs(fa - fb) < 1e-5f;
                case Rgba ca: return b is Rgba cb && ca.Approximately(cb, 1f / 1024f);
                case Rgba[] ra:
                {
                    if (!(b is Rgba[] rb) || rb.Length != ra.Length) return false;
                    for (int i = 0; i < ra.Length; i++) if (!ra[i].Approximately(rb[i], 1f / 1024f)) return false;
                    return true;
                }
                case BoxShadow[] sa:
                {
                    if (!(b is BoxShadow[] sb) || sb.Length != sa.Length) return false;
                    for (int i = 0; i < sa.Length; i++) if (!sa[i].Equals(sb[i])) return false;
                    return true;
                }
                case TextShadow[] xa:
                {
                    if (!(b is TextShadow[] xb) || xb.Length != xa.Length) return false;
                    for (int i = 0; i < xa.Length; i++) if (!xa[i].Equals(xb[i])) return false;
                    return true;
                }
                case TransformSpec ta:
                {
                    if (!(b is TransformSpec tb)) return false;
                    TransformParts p = ta.Compose(), q = tb.Compose();
                    return Math.Abs(p.Tx - q.Tx) < 1e-4f && Math.Abs(p.Ty - q.Ty) < 1e-4f && Math.Abs(p.TxPercent - q.TxPercent) < 1e-4f
                           && Math.Abs(p.TyPercent - q.TyPercent) < 1e-4f && Math.Abs(p.Rot - q.Rot) < 1e-4f
                           && Math.Abs(p.Sx - q.Sx) < 1e-5f && Math.Abs(p.Sy - q.Sy) < 1e-5f;
                }
                case ColorFilter fa2: return b is ColorFilter fb2 && fa2.Equals(fb2);
            }
            return Equals(a, b);
        }

        /// <summary>A running transition's progress for a property (0..1 eased), or −1 when none runs.</summary>
        public float ProgressOf(string property)
        {
            if (!_tracks.TryGetValue(property, out Track t)) return -1f;
            return t.Portion(Now);
        }

        /// <summary>Whether any transition is running.</summary>
        public bool Transitioning => _tracks.Count > 0;

        public override string ToString() =>
            "CssStates(" + string.Join(" ", _rules.ConvertAll(r => (r.On ? "+" : "-") + r.Name)) + ", running " + _tracks.Count.ToString(CultureInfo.InvariantCulture) + ")";

        private void OnDestroy()
        {
            _frame?.Dispose();
            _frame = null;
        }
    }
}
