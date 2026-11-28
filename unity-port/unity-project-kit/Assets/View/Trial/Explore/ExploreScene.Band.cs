// ExploreScene — the band at the top (ExploreScene.js:285-323, :751-791 drawBand, :897-906 syncMarks / syncEndLife).
// The JS's band is replaced by the header the four screens share (#1074, Trial.Header's HeaderBar), so the
// exploration's top edge reads like the base's, the table's and the battle's. What it shows is
// HeaderContent.ForExplore, built from the screen on every render; only the values that changed go through the bar's
// setters, because each setter lays the whole left row out again.
//
// Unlike the JS band: the numbers do not roll (the shared bar has no RollLabel), stamina is 「8 / 12」 against its real
// maximum, and the 訓練の刻み button is gone (decided on #1074). The marks themselves stay: a rest's 「訓練する」
// opens their card, and 次の層へ降りる asks for any left (TrainBeforeDescend), so a mark put off with 「あとで」 waits
// for one of those two.
//
// This file also holds the helpers the panel files share (the node detail, the carried items): ExPanelButton (a
// <button> that is not an .ocbg-btn), the text-style cache, icons and the flex-wrap rows.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Exploration;
using TMPro;
using Trial.Bridge;
using Trial.Geometry;
using Trial.Header;
using Trial.Kit;
using UnityEngine;

namespace Trial.Explore
{
    public sealed partial class ExploreScene
    {
        // ------------------------------------------------------------------ the header

        /// <summary>explore.css:57 :focus-visible of the scene's own buttons (the carried slots').</summary>
        private const string PanelFocusCss = "outline: 2px solid var(--wick); outline-offset: 2px;";

        /// <summary>The shared header (z 7, where the JS's band stood).</summary>
        private HeaderBar TopHeader;

        /// <summary>The header's buttons that open something from here (the speed is the battle's).</summary>
        private static readonly HeaderButtonKind[] HeaderButtons =
        {
            HeaderButtonKind.OwnedCards, HeaderButtonKind.Journal, HeaderButtonKind.Settings,
        };

        /// <summary>
        /// The words the key legend names for Enter on the header's Tab stops, in HeaderBar.Focusables' order (the chip
        /// back, the speed, the owned cards, the journal, the settings): the bar keeps no label of its own for them.
        /// </summary>
        private static readonly string[] HeaderStopWords =
        {
            HeaderBand.BackWord, HeaderBand.SpeedWord, HeaderBand.OwnedCardsWord, HeaderBand.JournalWord, HeaderBand.SettingsWord,
        };

        /// <summary>
        /// The header, built once from the screen as it stands (Mount): its buttons open what the JS band's did
        /// (ExploreScene.js:2270-2278), and its Tab stops join the scene's scope in hierarchy order.
        /// </summary>
        private void BuildHeader()
        {
            TopHeader = HeaderBar.Create(Root, HeaderOf(Session.Screen()), new HeaderActions
            {
                OnOwnedCards = () => OpenOverlay(ExploreOverlayKind.Cards).Forget(),
                OnJournal = () => OpenOverlay(ExploreOverlayKind.Journal).Forget(),
                OnSettings = () => OpenOverlay(ExploreOverlayKind.Settings).Forget(),
            });
            UiNode.Of(TopHeader.Root).Z = ExploreStyle.ZBand;
            IReadOnlyList<Focusable> stops = TopHeader.Focusables;
            for (int i = 0; i < stops.Count; i++)
            {
                string word = i < HeaderStopWords.Length ? HeaderStopWords[i] : null;
                InsertFocus(stops[i], word != null ? () => word : (Func<string>)null);
            }
        }

        /// <summary>What the header shows for a screen (the bridge decides each button's state and every word).</summary>
        private HeaderContent HeaderOf(ExplorationScreen s) => HeaderContent.ForExplore(s.LayerTitle,
            HeaderCalendar.Of(s.DaysLeft, s.CalendarFrozen, s.CalendarNote), s.Hp, s.MaxHp, s.Stamina, s.MaxStamina,
            HeaderDeckCount(), s.CanOpenCards, s.CanOpenSettings);

        /// <summary>The deck's count: the router's when it hands one in, else the deck the next battle is shuffled from.</summary>
        private int HeaderDeckCount() => Ctx.DeckCount != null ? Ctx.DeckCount() : Expedition.Deck.Count;

        // js: scenes/explore/ExploreScene.js:752
        /// <summary>drawBand(): the header again from Screen, through the setters of what changed.</summary>
        private void DrawHeader()
        {
            if (Screen == null || TopHeader == null || !TopHeader.Root) return;
            HeaderContent want = HeaderOf(Screen), shown = TopHeader.Content;
            if (want.PlaceName != shown.PlaceName || want.PlaceArt != shown.PlaceArt) TopHeader.SetPlace(want.PlaceName, want.PlaceArt);
            if (!SameCalendar(want.Calendar, shown.Calendar)) TopHeader.SetCalendar(want.Calendar);
            if (want.Hp != shown.Hp || want.MaxHp != shown.MaxHp) TopHeader.SetHp(want.Hp, want.MaxHp);
            if (want.Stamina != shown.Stamina || want.MaxStamina != shown.MaxStamina) TopHeader.SetStamina(want.Stamina, want.MaxStamina);
            if (want.DeckCount != shown.DeckCount) TopHeader.SetDeckCount(want.DeckCount);
            foreach (HeaderButtonKind kind in HeaderButtons)
            {
                HeaderButtonState state = want.StateOf(kind);
                if (!state.Equals(shown.StateOf(kind))) TopHeader.SetButton(kind, state);
            }
            SyncMarks();
        }

        private static bool SameCalendar(HeaderCalendar a, HeaderCalendar b) =>
            a != null && b != null && a.DaysLeft == b.DaysLeft && a.Frozen == b.Frozen && a.ShownNote == b.ShownNote;

        // js: scenes/explore/ExploreScene.js:897
        /// <summary>
        /// syncMarks(): the carried panel's is-held follows the plate at the moments openPlate and closePlate call this,
        /// and render keeps it. The JS also enabled the 訓練の刻み button here; the header has none.
        /// </summary>
        private void SyncMarks() => SyncHeld();

        // js: scenes/explore/ExploreScene.js:903
        /// <summary>
        /// 「この生を終える」 can be pressed at any time while exploring — except under a card that waits on the player or a
        /// confirmation: there it is held, and looks held (disabled).
        /// </summary>
        private void SyncEndLife()
        {
            if (Screen == null || EndLifeBtn == null) return;
            SetDisabled(EndLifeBtn, !Screen.CanEndLife || Busy || ConfirmOpen || (Plate != null && Plate.Kind != ExploreStyle.PlateScreen));
        }

        // ------------------------------------------------------------------ helpers shared by the detail and the carried items

        /// <summary>The scene's text styles by their CSS (parsed once per scene); rich = TMP rich text on (Rich markup).</summary>
        private readonly Dictionary<string, TextStyle> PanelStyleCache = new Dictionary<string, TextStyle>(StringComparer.Ordinal);

        /// <summary>TextStyle.FromCss(css), cached; rich turns TMP rich text on (Rich); align overrides text-align.</summary>
        private TextStyle PanelText(string css, bool rich = false, TextAlign? align = null)
        {
            string key = (rich ? "R" : "P") + (align.HasValue ? ((int)align.Value).ToString(CultureInfo.InvariantCulture) : "-") + "|" + css;
            if (PanelStyleCache.TryGetValue(key, out TextStyle s)) return s;
            s = TextStyle.FromCss(css);
            if (rich) s.RichText = true;
            if (align.HasValue) s.Align = align.Value;
            PanelStyleCache[key] = s;
            return s;
        }

        /// <summary>One CSS line box: its height and its baseline below its top.</summary>
        private readonly struct PanelLine
        {
            public readonly float H, Base;

            public PanelLine(float h, float baseline)
            {
                H = h;
                Base = baseline;
            }
        }

        /// <summary>
        /// One line of `s` as Blink lays it out: each face's ascent and descent rounded to whole px (SkScalarRound), the
        /// half-leading floored onto the ascent (descent = line-height − ascent), the line box from the highest ascent to
        /// the lowest descent of the faces on it. With `digits` the line also holds rich()'s digit runs
        /// (.ocbg-explore-num: IBM Plex Mono 600 at the same size and line-height), whose face reaches deeper below the
        /// baseline: such a line is taller than its line-height — 22px/28px 30 (the band's 刻限 7 / 10), 20px/28px 29 (a
        /// detail value), 18px/26px 28 (a forecast line), 17px/22px and 16px/22px 23 (a tool chip's effect, a slot's
        /// forecast). Measured on the reference shots: the band's pips (top 62) and gauge (59), the option boxes (256 tall
        /// at a rest node), the tool chips (29). The faces' table metrics (TextMath.Metrics) are what Blink reads.
        /// </summary>
        private static PanelLine PanelLineOf(TextStyle s, bool digits)
        {
            float lh = s.LineHeightPx;
            BlinkFace(s.Family, s.Weight, s.Size, lh, out float a, out float d);
            if (digits)
            {
                BlinkFace(Family.Mono, 600, s.Size, lh, out float ma, out float md);
                a = Math.Max(a, ma);
                d = Math.Max(d, md);
            }
            return new PanelLine(a + d, a);
        }

        private static void BlinkFace(Family family, int weight, float size, float lineHeight, out float ascent, out float descent)
        {
            FontMetrics m = TextMath.Metrics(family, weight);
            float a = (float)Math.Floor(m.AscentEm * size + 0.5), d = (float)Math.Floor(Math.Abs(m.DescentEm) * size + 0.5);
            ascent = a + (float)Math.Floor((lineHeight - (a + d)) / 2f);
            descent = lineHeight - ascent;
        }

        /// <summary>Whether a line holds a digit run (rich() sets it in the mono face).</summary>
        private static bool HasDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
                if (IsDigit(c)) return true;
            return false;
        }

        /// <summary>
        /// A rich() text that may wrap, at a width: its height (lines × its line box: a text with numbers is taken to carry
        /// them on each line it wraps to, as the bridge's wrapped lines do) and the style to draw it with — the line box as
        /// the line pitch when it differs from the line-height — with the y shift that keeps the first baseline where `s`
        /// puts it (the extra height of a line with numbers lies under its baseline).
        /// </summary>
        private static float PanelRichBlock(string markup, TextStyle s, float width, bool digits, out TextStyle draw, out float yShift)
        {
            int lines = TextMeasure.Lines(markup, s, width);
            float lineH = PanelLineOf(s, digits).H;
            draw = s;
            yShift = 0f;
            if (lines > 1 && Math.Abs(lineH - s.LineHeightPx) > 0.001f)
            {
                draw = s.Clone();
                draw.LineHeight = lineH;
                yShift = -(lineH - s.LineHeightPx) / 2f;
            }
            return lines * lineH;
        }

        /// <summary>ico(name, size, colour, opts) placed at (x, y) of parent.</summary>
        private static PathGraphic PanelIcon(RectTransform parent, string name, float size, Rgba color, float x, float y, IcoOpts opts = null)
        {
            PathGraphic g = ExploreIcons.Ico(parent, name, size, color, opts);
            Ui.Place(g.rectTransform, x, y);
            return g;
        }

        /// <summary>One flex item of a wrapping row: its element (placed at (0, 0) of the row) and its border-box size.</summary>
        private struct PanelItem
        {
            public RectTransform El;
            public float W, H;

            public PanelItem(RectTransform el, float w, float h)
            {
                El = el;
                W = w;
                H = h;
            }
        }

        /// <summary>
        /// display: flex; flex-wrap: wrap; align-items: center — the items of a row placed left to right, a line broken
        /// before an item that would pass `width`, each item centred in its line. Returns the rows' height.
        /// </summary>
        private static float PanelWrap(List<PanelItem> items, float width, float columnGap, float rowGap)
        {
            float y = 0f;
            int start = 0;
            while (start < items.Count)
            {
                float x = 0f, lineH = 0f;
                int end = start;
                while (end < items.Count)
                {
                    PanelItem it = items[end];
                    if (end > start && x + it.W > width + 0.01f) break;
                    x += it.W + columnGap;
                    lineH = Math.Max(lineH, it.H);
                    end++;
                }
                x = 0f;
                for (int i = start; i < end; i++)
                {
                    PanelItem it = items[i];
                    if (it.El) Ui.Place(it.El, x, y + (lineH - it.H) / 2f);
                    x += it.W + columnGap;
                }
                y += lineH;
                start = end;
                if (start < items.Count) y += rowGap;
            }
            return y;
        }

        /// <summary>
        /// A declaration block without its width / height: the panels lay their boxes out here (the flex and grid boxes),
        /// and CssStates would otherwise write the rule's size into the element (`height: auto` resolves to 0 there).
        /// </summary>
        private static string PanelBoxCss(string css)
        {
            var sb = new StringBuilder(css.Length);
            int depth = 0, start = 0;
            for (int i = 0; i <= css.Length; i++)
            {
                char c = i < css.Length ? css[i] : ';';
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ';' && depth <= 0)
                {
                    string d = css.Substring(start, i - start).Trim();
                    int colon = d.IndexOf(':');
                    string prop = colon > 0 ? d.Substring(0, colon).Trim().ToLowerInvariant() : "";
                    if (d.Length > 0 && prop != "width" && prop != "height") sb.Append(d).Append("; ");
                    start = i + 1;
                }
            }
            return sb.ToString();
        }

        /// <summary>Removes the panel's focusables from the scene's Tab scope (their elements are about to be rebuilt).</summary>
        private void DropPanelFocus(List<Focusable> list)
        {
            foreach (Focusable f in list.ToArray())
            {
                Scope?.Remove(f);
                FocusLabels.Remove(f);
            }
            list.Clear();
        }

        /// <summary>
        /// A scene button that is not an .ocbg-btn (the consumable slots): its box (the
        /// element's rules through CssStates, with their transitions, on the document clock), pointer listeners, and the
        /// flags the stylesheet reads (:hover from the pointer, [disabled], :focus-visible from Tab). Restyle turns the
        /// element's states on or off from the flags (SetState(…, apply: false)); Restate applies them. Click is onclick
        /// (nothing while disabled); Press() is b.click().
        /// </summary>
        private sealed class ExPanelButton
        {
            private bool _disabled, _hover, _focus;

            public RectTransform Root { get; private set; }
            public UiNode Node { get; private set; }
            public BoxGraphic Box { get; private set; }
            public ClickArea Area { get; private set; }
            public CssStates States { get; private set; }

            /// <summary>The button's own words (what Enter presses).</summary>
            public string Text { get; set; } = "";

            /// <summary>onclick.</summary>
            public event Action Click;

            /// <summary>Sets the states that follow the flags (and any class of the owner) without applying them.</summary>
            public Action<ExPanelButton> Restyle;

            public static ExPanelButton Make(RectTransform parent, string name, string baseCss, float x, float y, float w, float h)
            {
                var b = new ExPanelButton();
                b.Root = Ui.Div(parent, name, x, y, w, h);
                b.Node = UiNode.Of(b.Root);
                b.Box = BoxGraphic.On(b.Root);
                b.Area = ClickArea.On(b.Box);
                b.Area.Click += _ => b.Press();
                b.Area.HoverChanged += on =>
                {
                    b._hover = on;
                    b.Restate();
                };
                b.States = CssStates.Of(b.Root);
                b.States.Base(baseCss);
                return b;
            }

            /// <summary>[disabled]: no clicks, Tab skips it.</summary>
            public bool Disabled
            {
                get => _disabled;
                set
                {
                    if (_disabled == value) return;
                    _disabled = value;
                    Restate();
                }
            }

            /// <summary>The pointer is over it (CSS :hover).</summary>
            public bool Hovered => _hover;

            /// <summary>Tab moved the focus to it (CSS :focus-visible).</summary>
            public bool FocusRing => _focus;

            public void SetFocusRing(bool on)
            {
                _focus = on;
                Restate();
            }

            /// <summary>The states again from the flags; instant = no transitions.</summary>
            public void Restate(bool instant = false)
            {
                if (!Root) return;
                Restyle?.Invoke(this);
                States.Apply(instant);
            }

            /// <summary>b.click(): onclick runs unless the button is disabled; the microtasks run after it.</summary>
            public void Press()
            {
                if (_disabled || !Root) return;
                try { Click?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                Microtasks.Drain();
            }
        }
    }
}
