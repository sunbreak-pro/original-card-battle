// ExploreScene's state (port of mountExploreScene's closure, ExploreScene.js:181-398) and the helpers every part of
// the scene shares. ExploreScene is one partial class over the files that mirror ExploreScene.js's sections:
//
//   ExploreScene.State.cs    fields, the small shared types, dur / sfx / persist / timers, rich / put / splitRow,
//                            RollLabel, forecastIcon, the root's classes, Tab focus (keysWithFocus, ask)    — scene core
//   ExploreScene.cs          mount / destroy / refresh, the DOM, the clock, render, the key legend and the hint,
//                            the scene's keys, the overlays, lifeEnd                                          — scene core
//   ExploreScene.Map.cs      the map, the walker, steps (nodePos … leap)                                     — map
//   ExploreScene.Band.cs     the band (drawBand, paintGauge, syncMarks, syncEndLife)                          — panels
//   ExploreScene.Detail.cs   the node detail (drawDetail)                                                      — panels
//   ExploreScene.Carried.cs  carried items (drawCarried, fitCarried, useConsumable)                            — panels
//   ExploreScene.Plates.cs   cards over the map, banners, the flow after a step, the interlude               — plates
//   ExploreScene.Results.cs  battle / event results, the trace, the taken card, training                     — results
//
// The JS reads `screen`, `busy`, `plate`… as closure variables; here they are fields of the scene, named as the JS
// names them (PascalCase). Every part reads and writes them directly.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using TMPro;
using Trial.Audio;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    public sealed partial class ExploreScene : IScene
    {
        // ------------------------------------------------------------------ ctx (ExploreScene.js:182-188)

        /// <summary>The ctx the router mounted the scene with.</summary>
        private readonly ExploreSceneCtx Ctx;

        /// <summary>ctx.expedition: the recorded Expedition (state-changing calls are logged; Raw for the rest).</summary>
        private readonly RecordedExpedition Expedition;

        /// <summary>expedition.Session (recorded).</summary>
        private readonly RecordedSession Session;

        /// <summary>ctx.audio || ctx.sound || null.</summary>
        private readonly ITrialAudio Audio;

        /// <summary>ctx.settings || {}.</summary>
        private readonly TrialSettings Settings;

        /// <summary>matchMedia('(prefers-reduced-motion: reduce)').matches, read at mount.</summary>
        private readonly bool Reduce;

        // ------------------------------------------------------------------ DOM (ExploreScene.js:249-283)

        /// <summary>The host the router gave (a stage layer).</summary>
        private RectTransform Host;

        /// <summary>.ocbg-explore: the scene root (1920 × 1080, --ground). Confirm cards open under it (z 900).</summary>
        private RectTransform Root;

        /// <summary>.ocbg-explore-bdhost (z 0): the backdrop's host.</summary>
        private RectTransform BdHost;

        /// <summary>.ocbg-explore-map (z 2): pool, edges, nodes, selection frame, walker (built by BuildMapLayer).</summary>
        private RectTransform MapLayer;

        /// <summary>.ocbg-explore-banner (z 4; built by BuildBanner).</summary>
        private RectTransform Banner;

        /// <summary>.ocbg-explore-detail at DETAIL (z 3; styled by BuildDetail, filled by DrawDetail).</summary>
        private RectTransform Detail;

        /// <summary>.ocbg-explore-lullview (z 4): the interlude's plates (DrawInterlude).</summary>
        private RectTransform Lull;

        /// <summary>.ocbg-explore-cards (z 5): the dim and the plate of a card over the map.</summary>
        private RectTransform CardLayer;

        /// <summary>.ocbg-explore-carried at CARRIED (z 6; styled by BuildCarried, filled by DrawCarried).</summary>
        private RectTransform Carried;

        /// <summary>「この生を終える」 (z 6).</summary>
        private OcbgButton EndLifeBtn;

        /// <summary>.ocbg-explore-hint (z 6).</summary>
        private RectTransform Hint;

        /// <summary>.ocbg-explore-legend (z 6).</summary>
        private RectTransform Legend;

        /// <summary>.ocbg-explore-band (z 7; built by BuildBand).</summary>
        private RectTransform Band;

        /// <summary>.ocbg-explore-overlays (z 8): the fallback overlays.</summary>
        private RectTransform OverlayLayer;

        /// <summary>createBackdrop(bdHost).</summary>
        private ExploreBackdrop Backdrop;

        /// <summary>The band's roll labels, ticked by the clock (ExploreScene.js:323 rollers; BuildBand adds them).</summary>
        private readonly List<RollLabel> Rollers = new List<RollLabel>();

        // ------------------------------------------------------------------ state (ExploreScene.js:368-398)

        private bool Alive = true;

        /// <summary>The screen of the last render (session.Screen()).</summary>
        private ExplorationScreen Screen;

        private int Pinned = -1;
        private int Hovered = -1;
        private int LastCurrent = -1;

        /// <summary>state.Map the map was built from.</summary>
        private LayerMap MapRef;

        private int LastLayer;

        /// <summary>The ambience last set: the layer (int) or 'interlude'.</summary>
        private object LastAmbience;

        /// <summary>The screen and node of a step waiting on its confirmation.</summary>
        private ExploreStepInfo StepInfo;

        /// <summary>A step, battle or plate is in progress: the map takes no input.</summary>
        private bool Busy;

        /// <summary>
        /// The open card over the map (ours), or null. openPlate / closePlate also toggle the carried panel's `is-held`
        /// (ExploreScene.js:850, :874) right before they call syncMarks: SyncMarks syncs it (the carried part's SyncHeld).
        /// </summary>
        private ExplorePlate Plate;

        private bool ConfirmOpen;

        /// <summary>The overlay kind open from here, or null.</summary>
        private string OverlayOpen;

        private bool FlowRunning;
        private ExploreTraceRef PendingTrace;
        private bool PendingTraining;
        private bool BattleFallback;

        /// <summary>The walk's per-frame step (now), or null.</summary>
        private Action<float> WalkerAnim;

        /// <summary>The gauge's rise (float: a rise interrupted mid-way starts from where it stood).</summary>
        private float GaugeFrom;
        private float GaugeTo;
        private float GaugeAt;
        private float GaugeNext;

        private bool FirstRender = true;
        private string HintOverride;

        /// <summary>The app's battle scene is up: the backdrop rests.</summary>
        private bool InBattle;

        /// <summary>Trial: the pending result (PendingResult, or its event's Outcome) whose card this mount opened last.</summary>
        private object ShownResult;

        /// <summary>'layer:node' of the trace card this mount opened (a reload opens it again: R1-16).</summary>
        private string TraceOffered = "";

        /// <summary>Trial: the pending 受け取った遺産 card (PendingTaken) this mount opened last.</summary>
        private LegacyReceipt ShownTaken;

        /// <summary>Closes the fallback overlay (destroy).</summary>
        private Action OverlayClose;

        /// <summary>The map's nodes by id (BuildMap).</summary>
        private readonly Dictionary<int, ExploreNodeView> NodeEls = new Dictionary<int, ExploreNodeView>();

        /// <summary>The map's edges (BuildMap).</summary>
        private readonly List<ExploreEdgeView> EdgeEls = new List<ExploreEdgeView>();

        /// <summary>drawDetail's cache key (ExploreScene.js:598): reset to "" to force a redraw.</summary>
        private string DetailKey = "";

        /// <summary>The screen drawDetail drew last (ExploreScene.js:599).</summary>
        private ExplorationScreen DetailScreen;

        /// <summary>drawLegend's node (ExploreScene.js:326 legendNode).</summary>
        private NodeCard LegendNodeCard;

        /// <summary>The scene clock (createClock): backdrop, roll labels, gauge, walker; and the JS timers (Timeout).</summary>
        private TrialClock Clock;

        // ------------------------------------------------------------------ dur, sfx, ambience, persist (ExploreScene.js:186-195)

        /// <summary>performance.now() on the scene's time base (the clock's; every `now` the scene passes around).</summary>
        private float Now => Clock != null ? Clock.Now : 0f;

        // js: scenes/explore/ExploreScene.js:187
        /// <summary>演出短縮 or reduced motion.</summary>
        private bool Fast() => (Settings != null && Settings.ShortFx) || Reduce;

        // js: scenes/explore/ExploreScene.js:188
        /// <summary>dur(ms): JS timers and JS-driven tweens shrink to 35 % (Math.round) when Fast(); CSS ones never do.</summary>
        private float Dur(float ms) => Fast() ? (float)Math.Floor(ms * ExploreStyle.ShortFxFactor + 0.5) : ms;

        // js: scenes/explore/ExploreScene.js:190
        /// <summary>sfx(name, delay, mult, opts): sound is optional, a throw is swallowed.</summary>
        private void Sfx(string name, double delay = 0, double? mult = null, PlayOptions opts = null)
        {
            try { if (Audio != null) Audio.Play(name, delay, mult, opts); }
            catch (Exception) { /* sound is optional */ }
        }

        // js: scenes/explore/ExploreScene.js:193
        private void Ambience(object id)
        {
            try { if (Audio != null) Audio.SetAmbience(id); }
            catch (Exception) { /* optional */ }
        }

        // js: scenes/explore/ExploreScene.js:195
        /// <summary>The app saves the life after every change (save by replay; ARCH 「Save」).</summary>
        private void Persist()
        {
            try { Ctx.OnPersist?.Invoke(); }
            catch (Exception e) { Debug.LogError("[explore] persist " + e); }
        }

        // ------------------------------------------------------------------ timers (setTimeout)

        /// <summary>
        /// new Promise(r => setTimeout(r, ms)): resolves after ms on the scene clock. A destroyed scene's timers never fire
        /// (the JS ones fire on removed elements; Unity's are destroyed), so flows waiting on them stop where they stand.
        /// </summary>
        private Promise Delay(float ms) => Clock != null && Clock.Alive ? Clock.Wait(ms) : Promise.Never();

        /// <summary>setTimeout(fn, ms) on the scene clock (see Delay); a throw is logged.</summary>
        private void Timeout(float ms, Action fn)
        {
            if (fn == null) return;
            Delay(ms).Then(() =>
            {
                try { fn(); }
                catch (Exception e) { Debug.LogException(e); }
            });
        }

        // ------------------------------------------------------------------ the root's classes (.ocbg-explore.is-…)

        private readonly HashSet<string> RootClasses = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, Action<bool>>> RootWatchers = new List<KeyValuePair<string, Action<bool>>>();

        /// <summary>root.classList.contains(cls): ".is-busy", ".is-interlude", ".is-engaging", ".is-returning".</summary>
        private bool HasRootClass(string cls) => RootClasses.Contains(cls);

        /// <summary>
        /// root.classList.toggle(cls, on). The stylesheet's `.ocbg-explore.is-x .y` rules are the watchers: each part
        /// registers its element's reaction with WatchRootClass. Nothing happens when the class does not change.
        /// </summary>
        private void SetRootClass(string cls, bool on)
        {
            bool had = RootClasses.Contains(cls);
            if (had == on) return;
            if (on) RootClasses.Add(cls); else RootClasses.Remove(cls);
            foreach (KeyValuePair<string, Action<bool>> w in RootWatchers.ToArray())
            {
                if (w.Key != cls) continue;
                try { w.Value(on); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>A rule `.ocbg-explore{cls} …` on some element: onChange(on) runs when the class turns on or off.</summary>
        private void WatchRootClass(string cls, Action<bool> onChange)
        {
            if (onChange == null) return;
            RootWatchers.Add(new KeyValuePair<string, Action<bool>>(cls, onChange));
            if (RootClasses.Contains(cls)) onChange(true);
        }

        /// <summary>
        /// A rule `.ocbg-explore{cls} .el { css }` as a state of the element's CssStates (registered now, so it comes after
        /// the element's own states in the cascade: call it after setting them up).
        /// </summary>
        private void WatchRootClass(string cls, CssStates states, string css)
        {
            if (states == null) return;
            string state = "root" + cls;
            states.State(state, css, RootClasses.Contains(cls));
            states.Apply(instant: true);
            WatchRootClass(cls, on => states.SetState(state, on));
        }

        // ------------------------------------------------------------------ rich, put, splitRow (ExploreScene.js:84-109)

        /// <summary>
        /// rich(text): a bridge line as TMP rich text, its digit runs set in the number face (IBM Plex Mono 600,
        /// letter-spacing 0: .ocbg-explore-num) and its hidden-name runs (「■■の秘跡」) as sealed blocks (ink at 34 %,
        /// .06em; on washi: washi-ink at 38 %). `style` is the label's (its letter-spacing is undone on the runs that set
        /// their own); numsInFace keeps the digits in the label's face (the layer title, the interlude's layer name:
        /// explore.css:30, :364). The label needs style.RichText (RichStyle).
        /// </summary>
        // js: scenes/explore/ExploreScene.js:88
        internal static string Rich(string text, TextStyle style = null, bool onWashi = false, bool numsInFace = false)
        {
            string s = text ?? "";
            float baseLs = style != null ? style.LetterSpacingEm : 0f;
            var sb = new StringBuilder(s.Length + 32);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                int j = i + 1;
                if (IsDigit(c))
                {
                    while (j < s.Length && IsDigit(s[j])) j++;
                    AppendNum(sb, s.Substring(i, j - i), baseLs, numsInFace);
                }
                else if (c == '■')
                {
                    while (j < s.Length && s[j] == '■') j++;
                    AppendSealed(sb, s.Substring(i, j - i), baseLs, onWashi);
                }
                else
                {
                    while (j < s.Length && !IsDigit(s[j]) && s[j] != '■') j++;
                    AppendPlain(sb, s.Substring(i, j - i));
                }
                i = j;
            }
            return sb.ToString();
        }

        /// <summary>A copy of the style with TMP rich text on (for labels that show Rich / RollLabel markup).</summary>
        internal static TextStyle RichStyle(TextStyle style)
        {
            TextStyle s = (style ?? new TextStyle()).Clone();
            s.RichText = true;
            return s;
        }

        /// <summary>JS \d: an ASCII digit.</summary>
        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static string Em(float v) => v.ToString("0.####", CultureInfo.InvariantCulture) + "em";

        /// <summary>A digit run in .ocbg-explore-num.</summary>
        internal static void AppendNum(StringBuilder sb, string digits, float baseLs, bool numsInFace = false)
        {
            bool spacing = Math.Abs(baseLs) > 1e-5f;
            // the face must exist (and be registered) for <font="Mono-600"> to resolve
            if (!numsInFace) TrialFonts.Get(Family.Mono, 600);
            if (spacing) sb.Append("<cspace=").Append(Em(-baseLs)).Append('>');
            if (!numsInFace) sb.Append("<font=\"").Append(TrialFonts.Tag(Family.Mono, 600)).Append("\">");
            sb.Append(digits);
            if (!numsInFace) sb.Append("</font>");
            if (spacing) sb.Append("</cspace>");
        }

        private static void AppendSealed(StringBuilder sb, string run, float baseLs, bool onWashi)
        {
            Color32 c = Materials.Vertex(onWashi ? ExploreStyle.SealedOnWashi : ExploreStyle.Sealed);
            sb.Append("<color=#").Append(c.r.ToString("x2")).Append(c.g.ToString("x2")).Append(c.b.ToString("x2")).Append(c.a.ToString("x2")).Append('>');
            sb.Append("<cspace=").Append(Em(ExploreStyle.SealedSpacingEm - baseLs)).Append('>');
            sb.Append(run);
            sb.Append("</cspace></color>");
        }

        private static void AppendPlain(StringBuilder sb, string run)
        {
            if (run.IndexOf('<') >= 0) sb.Append("<noparse>").Append(run).Append("</noparse>");
            else sb.Append(run);
        }

        // js: scenes/explore/ExploreScene.js:100
        /// <summary>put(el, ...kids): parents the kids that are not null (in order, after what el holds); returns el.</summary>
        internal static RectTransform Put(RectTransform el, params Component[] kids)
        {
            if (el == null || kids == null) return el;
            foreach (Component k in kids)
            {
                if (!k) continue;
                k.transform.SetParent(el, false);
                k.transform.SetAsLastSibling();
            }
            return el;
        }

        /// <summary>put(el, kids[]) (the JS flattens arrays).</summary>
        internal static RectTransform Put(RectTransform el, IEnumerable<Component> kids)
        {
            if (kids == null) return el;
            var list = new List<Component>(kids);
            return Put(el, list.ToArray());
        }

        // js: scenes/explore/ExploreScene.js:106
        /// <summary>"key: value" rows (脅威度: 通常, 休む: HP +8…) split at the first ": " (at 1…8) for a two-column read; key null otherwise.</summary>
        internal static (string Key, string Value) SplitRow(string line)
        {
            string l = line ?? "";
            int at = l.IndexOf(": ", StringComparison.Ordinal);
            return at > 0 && at <= 8 ? (l.Substring(0, at), l.Substring(at + 2)) : ((string)null, l);
        }

        // js: scenes/explore/ExploreScene.js:75
        /// <summary>forecastIcon(line): which icon heads a forecast line, by the word it starts with; null = a blank (fc-dot).</summary>
        internal static IconSpec ForecastIcon(string line)
        {
            string l = line ?? "";
            if (l.StartsWith("刻限", StringComparison.Ordinal)) return new IconSpec("hourglass", ExploreStyle.Ink);
            if (l.StartsWith("瘴気", StringComparison.Ordinal)) return new IconSpec("miasma", ExploreStyle.Phosphor);
            if (l.StartsWith("最大スタミナ", StringComparison.Ordinal)) return new IconSpec("pip", ExploreStyle.Amber, outline: true);
            if (l.StartsWith("スタミナ", StringComparison.Ordinal)) return new IconSpec("pip", ExploreStyle.Amber);
            if (l.StartsWith("HP", StringComparison.Ordinal)) return new IconSpec("heart", ExploreStyle.Crimson);
            return null;
        }

        // ------------------------------------------------------------------ Tab focus (ExploreScene.js:197-246)
        // The kit's Focus is stage.js's Tab-focus rule (only Tab focuses; a pointer press clears it). The scene keeps one
        // Tab scope (its buttons in hierarchy order ≈ DOM order); a confirm card pushes its own.

        /// <summary>The scene's Tab scope (pushed at mount, disposed at destroy).</summary>
        private FocusScope Scope;

        /// <summary>buttonLabel(b) for each focusable the scene added (what Enter presses, for the legend).</summary>
        private readonly Dictionary<Focusable, Func<string>> FocusLabels = new Dictionary<Focusable, Func<string>>();

        /// <summary>
        /// Adds a button to the scene's Tab scope in hierarchy order (DOM order), dropping the ones destroyed since.
        /// `label` is buttonLabel(b): what the legend names for Enter (null = no name).
        /// </summary>
        private Focusable AddFocus(RectTransform target, Action press, Action<bool> ring = null, Func<bool> canFocus = null, Func<string> label = null)
        {
            var f = new Focusable { Target = target, Press = press, Ring = ring, CanFocus = canFocus };
            InsertFocus(f, label);
            return f;
        }

        /// <summary>
        /// An .ocbg-btn in the scene's Tab scope (its own label names it unless `label` says otherwise; a disabled button
        /// is skipped). `ring` replaces the button's own :focus-visible ring (2px --wick, offset 2) where the stylesheet
        /// draws another (the washi plates: 3px washi-ink, offset 3).
        /// </summary>
        private Focusable AddFocus(OcbgButton b, Func<string> label = null, Action<bool> ring = null)
        {
            if (b == null) return null;
            var f = new Focusable { Target = b.Root, Press = b.Press, Ring = ring ?? b.SetFocusRing, CanFocus = () => !b.Disabled };
            InsertFocus(f, label ?? (() => FirstLine(b.Text)));
            return f;
        }

        private void InsertFocus(Focusable f, Func<string> label)
        {
            if (Scope == null || f == null) return;
            // drop what was destroyed (the detail and the plates are rebuilt often)
            var dead = new List<Focusable>();
            foreach (Focusable x in Scope.Items) if (x.Target == null || !x.Target) dead.Add(x);
            foreach (Focusable x in dead) { Scope.Remove(x); FocusLabels.Remove(x); }
            int at = Scope.Items.Count;
            for (int i = 0; i < Scope.Items.Count; i++)
            {
                if (HierarchyBefore(f.Target, Scope.Items[i].Target)) { at = i; break; }
            }
            Scope.Insert(at, f);
            if (label != null) FocusLabels[f] = label;
        }

        /// <summary>button.disabled = v (a disabled button leaves the Tab order: AddFocus reads it).</summary>
        private static void SetDisabled(OcbgButton b, bool v)
        {
            if (b == null || b.Disabled == v) return;
            b.Disabled = v;
        }

        /// <summary>Whether a comes before b in the hierarchy (pre-order: the DOM order of the scene's elements).</summary>
        private static bool HierarchyBefore(Transform a, Transform b)
        {
            if (!a || !b) return false;
            List<int> pa = PathOf(a), pb = PathOf(b);
            int n = Math.Min(pa.Count, pb.Count);
            for (int i = 0; i < n; i++)
                if (pa[i] != pb[i]) return pa[i] < pb[i];
            return pa.Count < pb.Count;
        }

        private static List<int> PathOf(Transform t)
        {
            var p = new List<int>();
            for (Transform x = t; x != null; x = x.parent) p.Add(x.GetSiblingIndex());
            p.Reverse();
            return p;
        }

        private static string FirstLine(string s) => (s ?? "").Trim().Split('\n')[0];

        // js: scenes/explore/ExploreScene.js:211
        /// <summary>buttonLabel(b): a button's own words (what Enter presses); a trace candidate says 付ける・外す.</summary>
        private string ButtonLabel(Focusable b)
        {
            if (b == null) return "";
            if (FocusLabels.TryGetValue(b, out Func<string> fn) && fn != null)
            {
                try { return FirstLine(fn()); }
                catch (Exception e) { Debug.LogException(e); }
            }
            return "";
        }

        // js: scenes/explore/ExploreScene.js:221
        /// <summary>focusedButton(scope): the enabled button inside `scope` that Tab moved the focus to, or null.</summary>
        private Focusable FocusedButton(Transform scope)
        {
            Focusable el = Focus.Current;
            if (el == null || !scope || el.Target == null || !el.Target || !el.Target.IsChildOf(scope)) return null;
            // under a confirmation only its own buttons answer (what is behind it waits)
            ConfirmCardView confirm = Root ? global::Trial.Kit.Stage.LastConfirmIn(Root) : null;
            if (confirm != null && confirm.Shade && !el.Target.IsChildOf(confirm.Shade)) return null;
            return el.IsFocusable ? el : null;
        }

        // js: scenes/explore/ExploreScene.js:230
        /// <summary>pressFocused(e, scope): Enter / Space on a focused button presses it (and nothing else); true when it did.</summary>
        private bool PressFocused(KeyEvent e, Transform scope)
        {
            if ((e.Key != "Enter" && e.Key != " ") || e.CtrlKey || e.MetaKey || e.AltKey) return false;
            Focusable b = FocusedButton(scope);
            if (b == null) return false;
            Focus.Press(b);
            return true;
        }

        // js: scenes/explore/ExploreScene.js:238
        /// <summary>keysWithFocus(handler): pushKeys, with a focused button's Enter / Space first. Returns the pop.</summary>
        private Action KeysWithFocus(Func<KeyEvent, bool> handler) =>
            KeyStack.Push(e => PressFocused(e, Root) || (handler != null && handler(e)));

        // js: scenes/explore/ExploreScene.js:240
        /// <summary>
        /// ask(opts): confirmCard (stage.js) over the scene, whose own Enter confirms: a focused button of the card is
        /// pressed instead. In this scene the body keeps its newlines and the card comes in as a plate (explore.css:397-398).
        /// </summary>
        private Promise<bool> Ask(ConfirmOpts opts)
        {
            opts = opts ?? new ConfirmOpts();
            opts.BodyWhiteSpace = WhiteSpace.PreLine;
            ConfirmCardView v = global::Trial.Kit.Stage.OpenConfirm(Root, opts);
            CssAnimator.Play(v.Card, ExploreStyle.ConfirmCardAnim);
            RectTransform shade = v.Shade;
            Action pop = KeyStack.Push(e => PressFocused(e, shade));
            return v.Answer.Then(yes => { pop(); return yes; });
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Small shared types
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>One item of the key legend: [keys, label] (a single key or several, e.g. [Shift, Enter]).</summary>
    public sealed class LegendEntry
    {
        public LegendEntry(string label, params string[] keys)
        {
            Label = label ?? "";
            Keys = keys ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Keys { get; }
        public string Label { get; }

        public static LegendEntry Of(string key, string label) => new LegendEntry(label, key);
        public static LegendEntry Of(string[] keys, string label) => new LegendEntry(label, keys);
    }

    /// <summary>openPlate's options ({ cls, w, ht, keys, kind, x, y, legend }, ExploreScene.js:867).</summary>
    public sealed class PlateOpts
    {
        /// <summary>The plate's classes ("is-washi is-break", "is-end is-fallen", "is-solid is-train" …).</summary>
        public string Cls;
        public float W;
        /// <summary>null: the plate takes the height of what it holds (call FitPlate once it is filled).</summary>
        public float? Ht;
        /// <summary>The plate's own keys (pushed with keysWithFocus), or null.</summary>
        public Func<KeyEvent, bool> Keys;
        /// <summary>ExploreStyle.PlateScreen / PlateChoice / PlateResult / PlateTrace / PlateTrain.</summary>
        public string Kind;
        public float? X;
        public float? Y;
        /// <summary>The card's keys in the key legend, or null.</summary>
        public IReadOnlyList<LegendEntry> Legend;
    }

    /// <summary>The open card over the map: plate = { el, dim, kind, legend, pop, key } (ExploreScene.js:873).</summary>
    public sealed class ExplorePlate
    {
        public RectTransform El;
        public RectTransform Dim;
        public string Kind;
        public IReadOnlyList<LegendEntry> Legend;
        /// <summary>Pops the plate's keys (null when it has none).</summary>
        public Action Pop;
        /// <summary>What the card shows (drawScreenCard / drawChoiceCard redraw only when it changes).</summary>
        public string Key;
    }

    /// <summary>stepInfo: a step waiting on its confirmation ({ before, node, choice, jump }).</summary>
    public sealed class ExploreStepInfo
    {
        public ExplorationScreen Before;
        public NodeCard Node;
        public RestChoice Choice;
        public bool Jump;
    }

    /// <summary>A trace to offer: { layer, nodeId }.</summary>
    public sealed class ExploreTraceRef
    {
        public ExploreTraceRef(int layer, int nodeId)
        {
            Layer = layer;
            NodeId = nodeId;
        }

        public int Layer { get; }
        public int NodeId { get; }

        public string Key => Layer.ToString(CultureInfo.InvariantCulture) + ":" + NodeId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>An icon as the JS names it: [name, colour, { outline }] (forecastIcon, the end card's mark).</summary>
    public sealed class IconSpec
    {
        public IconSpec(string name, Rgba color, bool outline = false)
        {
            Name = name;
            Color = color;
            Outline = outline;
        }

        public string Name { get; }
        public Rgba Color { get; }
        public bool Outline { get; }
    }

    // js: scenes/explore/ExploreScene.js:112
    /// <summary>
    /// A label whose numbers roll to their new value when only the numbers change: 520 ms, EASE.out, rounded each
    /// frame, and the label glows once (ocbg-explore-roll 900ms ease-out: text-shadow 0 → 0 0 12px ink 70 % → 0). No
    /// animation on the first text or when `animate` is false. The digits are set in the number face (.ocbg-explore-num).
    /// </summary>
    public sealed class RollLabel
    {
        private readonly TMP_Text _el;
        private readonly float _ms;
        private string _text;
        private string[] _parts = Array.Empty<string>();
        private Anim _anim;
        private CssAnimator _rolled;

        private sealed class Anim
        {
            public long[] From;
            public long[] To;
            public float T0;
        }

        // js: scenes/explore/ExploreScene.js:113
        public RollLabel(TMP_Text el, float ms = 520f)
        {
            _el = el;
            _ms = ms;
        }

        public TMP_Text El => _el;

        /// <summary>The text last set (null before the first).</summary>
        public string Text => _text;

        // js: scenes/explore/ExploreScene.js:121
        public void Set(string text, float now, bool animate)
        {
            if (text == _text) return;
            string old = _text;
            _text = text;
            string[] parts = SplitDigits(text ?? "");
            long[] from = null;
            if (animate && old != null)
            {
                string[] op = SplitDigits(old);
                bool same = op.Length == parts.Length;
                for (int i = 0; same && i < op.Length; i++) if (i % 2 == 0 && op[i] != parts[i]) same = false;
                if (same) from = Numbers(op);
            }
            _parts = parts;
            long[] to = Numbers(parts);
            bool differs = false;
            if (from != null) for (int i = 0; i < from.Length && i < to.Length; i++) if (from[i] != to[i]) differs = true;
            if (from != null && differs)
            {
                _anim = new Anim { From = from, To = to, T0 = now };
                Paint(from);
                // classList.remove / reflow / add: the glow starts over
                if (_rolled != null) _rolled.Stop(true);
                if (_el) _rolled = CssAnimator.Play(_el, "ocbg-explore-roll 900ms ease-out");
            }
            else
            {
                // the spans keep the text's own digit runs (ExploreScene.js:133-139: a leading zero stays)
                _anim = null;
                Paint(null);
            }
        }

        // js: scenes/explore/ExploreScene.js:150
        /// <summary>paint(values): the digit runs show these numbers; null = the text's own runs (as set() builds them).</summary>
        private void Paint(long[] values)
        {
            if (!_el) return;
            float ls = Label.StyleOf(_el)?.LetterSpacingEm ?? 0f;
            var sb = new StringBuilder();
            int k = 0;
            for (int i = 0; i < _parts.Length; i++)
            {
                if (i % 2 == 1)
                {
                    string digits = values != null && k < values.Length ? values[k].ToString(CultureInfo.InvariantCulture) : _parts[i];
                    k++;
                    ExploreScene.AppendNum(sb, digits, ls);
                }
                else if (_parts[i].Length > 0)
                {
                    if (_parts[i].IndexOf('<') >= 0) sb.Append("<noparse>").Append(_parts[i]).Append("</noparse>");
                    else sb.Append(_parts[i]);
                }
            }
            Label.SetText(_el, sb.ToString());
        }

        // js: scenes/explore/ExploreScene.js:152
        public void Tick(float now)
        {
            if (_anim == null) return;
            float t = Math.Min(1f, (now - _anim.T0) / _ms);
            float e = Ease.Out(t);
            var v = new long[_anim.From.Length];
            for (int i = 0; i < v.Length; i++) v[i] = (long)Math.Floor(_anim.From[i] + (_anim.To[i] - _anim.From[i]) * (double)e + 0.5);
            Paint(v);
            if (t >= 1f) _anim = null;
        }

        /// <summary>String(text).split(/(\d+)/): text, digits, text, …, text (always odd in length).</summary>
        private static string[] SplitDigits(string s)
        {
            var o = new List<string>();
            var cur = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] >= '0' && s[i] <= '9')
                {
                    int j = i;
                    while (j < s.Length && s[j] >= '0' && s[j] <= '9') j++;
                    o.Add(cur.ToString());
                    cur.Clear();
                    o.Add(s.Substring(i, j - i));
                    i = j;
                }
                else cur.Append(s[i++]);
            }
            o.Add(cur.ToString());
            return o.ToArray();
        }

        private static long[] Numbers(string[] parts)
        {
            var o = new List<long>();
            for (int i = 1; i < parts.Length; i += 2)
                o.Add(long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out long v) ? v : 0);
            return o.ToArray();
        }
    }
}
