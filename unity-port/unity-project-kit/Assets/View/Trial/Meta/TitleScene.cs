// Port of scenes/meta/title.js: the title — the lair at its deepest (layer 7's backdrop values from the bridge: the
// violet spring, the miasma rising slowly), one heir standing at its edge, the line from concept-v3 §1 written top to
// bottom on the right, and はじめから / つづきから / 設定. The router mounts it; it only calls back.
//
// The menu's rules (which items open, where the cursor starts, how ↑↓ skip the shut ones, what a choice asks for) are
// Trial.Script.TitleMenu; the texts are MetaText; the times are MetaTimelines. This file keeps the looks (meta.css's
// title rules, verbatim below), the sounds and the walk into the dark.
//
// Deviation (meta-a.md §1.3): the JS's leave() makes fresh stylers for the figure and the name, so a leave during the
// entrance runs two tweens on one element; here the leave tweens the entrance's own stylers, so its tween replaces the
// entrance's on the same keys (the look of a leave after the entrance is the same).
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Trial.Meta
{
    public sealed class TitleScene : IScene
    {
        // ---- meta.css: the title, verbatim ------------------------------------------------------------------------

        /// <summary>meta.css:41 .ocbg-meta-title-ground.</summary>
        public const string GroundCss = "position: absolute; pointer-events: none; background: linear-gradient(180deg, color-mix(in srgb, var(--ground) 0%, transparent), color-mix(in srgb, var(--ground) 92%, transparent) 70%);";

        /// <summary>meta.css:408 .ocbg-meta-title-ground (the later rule: its background wins over :41's).</summary>
        public const string GroundLaterCss = "background: linear-gradient(180deg, color-mix(in srgb, var(--ground) 0%, transparent), color-mix(in srgb, var(--ground) 45%, transparent) 80%);";

        /// <summary>meta.css:409 .ocbg-meta-title-ground::before (the horizon line).</summary>
        public const string GroundBeforeCss = "content: \"\"; position: absolute; left: 22%; right: 22%; top: 0; height: 1px; background: linear-gradient(90deg, color-mix(in srgb, var(--violet) 0%, transparent), color-mix(in srgb, var(--violet) 55%, transparent), color-mix(in srgb, var(--violet) 0%, transparent));";

        /// <summary>meta.css:42 .ocbg-meta-title-figure.</summary>
        public const string FigureCss = "position: absolute; transform-origin: 50% 100%;";

        /// <summary>meta.css:45 .ocbg-meta-title-name.</summary>
        public const string NameCss = "font: 700 30px/1 var(--font-display); letter-spacing: .34em; color: var(--ink); white-space: nowrap; text-shadow: 0 2px 12px color-mix(in srgb, var(--ground) 80%, transparent);";

        /// <summary>meta.css:46 .ocbg-meta-title-rule.</summary>
        public const string RuleCss = "position: absolute; display: block; background: linear-gradient(90deg, var(--amber), color-mix(in srgb, var(--amber) 0%, transparent));";

        /// <summary>meta.css:47 .ocbg-meta-title-tagline.</summary>
        public const string TaglineCss = "display: flex; flex-direction: row-reverse; justify-content: flex-start; gap: 40px;";

        /// <summary>meta.css:48 .ocbg-meta-title-col.</summary>
        public const string ColCss = "writing-mode: vertical-rl; font: 400 46px/1 var(--font-display); letter-spacing: .22em; color: var(--ink); text-shadow: 0 0 18px color-mix(in srgb, var(--violet) 40%, transparent);";

        /// <summary>meta.css:49 .ocbg-meta-title-col:last-child.</summary>
        public const string ColLastCss = "margin-top: 96px;";

        /// <summary>meta.css:50 .ocbg-meta-title-col span.</summary>
        public const string ColSpanCss = "display: inline-block;";

        /// <summary>meta.css:51 .ocbg-meta-title-menu.</summary>
        public const string MenuCss = "display: flex; flex-direction: column; gap: var(--space-12);";

        /// <summary>meta.css:52 .ocbg-meta-title-item.</summary>
        public const string ItemCss = "position: relative; display: flex; align-items: baseline; gap: var(--space-16); padding: 10px 0 10px 48px; text-align: left; font: 700 36px/1.2 var(--font-display); letter-spacing: .14em; color: var(--ink); transition: letter-spacing .2s, text-shadow .2s;";

        /// <summary>meta.css:53 .ocbg-meta-title-item small.</summary>
        public const string ItemSmallCss = "font: 400 16px/1 var(--font-body); letter-spacing: .04em;";

        /// <summary>meta.css:54 .ocbg-meta-title-mark (the selection line).</summary>
        public const string MarkCss = "position: absolute; left: 0; top: 50%; width: 32px; height: 2px; margin-top: -1px; background: var(--amber); transform: scaleX(0); transform-origin: 0 50%; transition: transform .22s;";

        /// <summary>meta.css:55 .ocbg-meta-title-mark::after (the lamp at the line's tip).</summary>
        public const string MarkAfterCss = "content: \"\"; position: absolute; right: -4px; top: -3px; width: 8px; height: 8px; border-radius: 50%; background: var(--wick); box-shadow: 0 0 10px var(--wick); opacity: 0; transition: opacity .22s;";

        /// <summary>meta.css:56 .ocbg-meta-title-item.is-on.</summary>
        public const string ItemOnCss = "letter-spacing: .2em; text-shadow: 0 0 22px color-mix(in srgb, var(--amber) 45%, transparent);";

        /// <summary>meta.css:57 .ocbg-meta-title-item.is-on .ocbg-meta-title-mark.</summary>
        public const string MarkOnCss = "transform: scaleX(1);";

        /// <summary>meta.css:58 .ocbg-meta-title-item.is-on .ocbg-meta-title-mark::after.</summary>
        public const string MarkAfterOnCss = "opacity: 1;";

        /// <summary>meta.css:59 .ocbg-meta-title-item.is-off.</summary>
        public const string ItemOffCss = "cursor: default;";

        /// <summary>meta.css:60 .ocbg-meta-title-item.is-off > span.</summary>
        public const string ItemOffSpanCss = "opacity: .4;";

        /// <summary>meta.css:61 .ocbg-meta-title-item.is-off small.</summary>
        public const string ItemOffSmallCss = "color: var(--ink); opacity: .85;";

        // ---- layout numbers of title.js (stage px) ----------------------------------------------------------------

        // js: scenes/meta/title.js:23 the figure's frame: 960 - 70, 600, 140 × 226 (its feet on the horizon, y 826)
        private const float FigureX = 960f - 70f, FigureY = 600f, FigureW = 140f, FigureH = 226f;

        // js: scenes/meta/title.js:24-25 O.FootLight(200, false) at (-30, 196) of the frame
        private const float FootW = 200f, FootX = -30f, FootY = 196f;

        // js: scenes/meta/title.js:33 the tagline's box
        private const float TaglineX = 1440f, TaglineY = 104f, TaglineW = 280f, TaglineH = 880f;

        /// <summary>meta.css:47 gap: 40px between the columns; :49 margin-top: 96px on the last.</summary>
        private const float ColumnGap = 40f, LastColumnTop = 96f;

        // js: scenes/meta/title.js:47-48, :66, :81
        private const float NameX = 152f, NameY = 236f;
        private const float RuleX = 152f, RuleY = 300f, RuleW = 360f, RuleH = 2f;
        private const float MenuX = 152f, MenuY = 560f, MenuW = 520f;
        private const float KeysX = 152f, KeysY = 1004f;

        /// <summary>meta.css:51 gap: var(--space-12) between the items; :52 gap: var(--space-16) inside one.</summary>
        private const float ItemGap = 12f, ItemInnerGap = 16f;

        /// <summary>meta.css:54-55: the line 32 × 2 (margin-top −1 at top 50%), the lamp 8 × 8 at right −4, top −3.</summary>
        private const float MarkW = 32f, MarkH = 2f, LampSize = 8f, LampRight = -4f, LampTop = -3f;

        // ---- the scene --------------------------------------------------------------------------------------------

        private sealed class Row
        {
            public RectTransform El;
            public UiNode Node;
            public TextMeshProUGUI Span;
            public CssStates SpanStates;
            public BoxGraphic Mark;
            public CssStates MarkStates;
            public BoxGraphic Lamp;
            public CssStates LampStates;
            public TextMeshProUGUI Small;
        }

        private readonly TitleCtx _ctx;
        private readonly MetaSound _sound;
        private readonly RectTransform _root;
        private readonly MetaBackdrop _backdrop;
        private readonly SceneClock _clock;
        private readonly RectTransform _figureWrap;
        private readonly GradientGraphic _foot;
        private readonly RectTransform _ground;
        private readonly RectTransform _tagline;
        private readonly VerticalText _taglineText;
        private readonly TextMeshProUGUI _name;
        private readonly RectTransform _rule;
        private readonly RectTransform _menu;
        private readonly TitleMenu _items;
        private readonly List<Row> _rows = new List<Row>();
        private readonly TextMeshProUGUI _keys;
        private readonly Styler _fade;
        private readonly Styler _pf0;
        private readonly Styler _pn0;
        private readonly Action _popKeys;
        private bool _busy;

        // js: scenes/meta/title.js:15
        private TitleScene(RectTransform host, TitleCtx ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _sound = MetaUtil.SoundOf(ctx);
            _root = MetaUtil.SceneRoot(host, "title");
            _backdrop = MetaBackdrop.Create(_root, MetaBackdrop.LayerBackdrop(7), new MetaBackdrop.Options { Vignette = 0.55f, MoteSpeed = 0.7f });
            _clock = MetaUtil.SceneClock((dt, now) => { _backdrop.Paint(now); Breathe(now); }, ctx.Stage);

            // ---- the heir at the edge of the spring (title.js:22-30): the ground, then the figure over it ----
            BoxGraphic ground = BoxGraphic.Make(_root, GroundCss + GroundLaterCss, 0f, 826f, 1920f, 254f, "ocbg-meta-title-ground");
            _ground = ground.rectTransform;
            // ::before: left 22%, right 22%, top 0, height 1px
            BoxGraphic.Make(_ground, GroundBeforeCss, 1920f * 0.22f, 0f, 1920f * (1f - 0.22f - 0.22f), 1f, "before");

            _figureWrap = Ui.Div(_root, "ocbg-meta-title-figure", FigureX, FigureY, FigureW, FigureH);
            UiNode.Of(_figureWrap).SetOrigin(0.5f, 1f);   // transform-origin: 50% 100%
            _foot = Ocb.FootLight(_figureWrap, FootW, false);
            Ui.Place(_foot.rectTransform, FootX, FootY);
            Ocb.Figure(_figureWrap, "swordsman");

            // ---- the line, written top to bottom on the right (title.js:32-44) ----
            _tagline = Ui.Div(_root, "ocbg-meta-title-tagline", TaglineX, TaglineY, TaglineW, TaglineH);
            UiNode.Of(_tagline);
            IReadOnlyList<string> lines = MetaText.TaglineLines;
            var tops = new List<float>();
            for (int i = 0; i < lines.Count; i++) tops.Add(i == lines.Count - 1 ? LastColumnTop : 0f);
            _taglineText = VerticalText.Make(_tagline, lines, MetaStyle.Text(ColCss + ColSpanCss), TaglineW, ColumnGap, tops, "ocbg-meta-title-col");
            foreach (TextMeshProUGUI ch in _taglineText.Chars) TextGlow.For(ch);   // text-shadow 0 0 18px violet 40%

            // ---- name and menu on the left (title.js:46-50) ----
            _name = Label.Make(_root, MetaText.GameName, MetaStyle.Text(NameCss), NameX, NameY, null, null, "ocbg-meta-title-name");
            TextGlow.For(_name);   // text-shadow 0 2px 12px ground 80%
            BoxGraphic rule = BoxGraphic.Make(_root, RuleCss, RuleX, RuleY, RuleW, RuleH, "ocbg-meta-title-rule");
            _rule = rule.rectTransform;

            // js: scenes/meta/title.js:61 the items (TitleMenu: enabled, the note beside a shut つづきから, the first cursor)
            _items = new TitleMenu(ctx.HasSave, ctx.Unreadable, ctx.OnSettings != null);
            _menu = Ui.Div(_root, "ocbg-meta-title-menu", MenuX, MenuY, MenuW, 0f);
            UiNode.Of(_menu);
            float y = 0f;
            for (int i = 0; i < _items.Items.Count; i++)
            {
                if (i > 0) y += ItemGap;
                Row row = MakeRow(i, _items.Items[i], y);
                _rows.Add(row);
                y += row.Node.H;
            }
            UiNode.Of(_menu).H = y;
            _keys = MetaStyle.Keys(_root, MetaText.TitleKeys, KeysX, KeysY);

            // js: scenes/meta/title.js:93 the first cursor's row is lit at once
            for (int k = 0; k < _rows.Count; k++) SetOn(_rows[k], k == _items.Lit, instant: true);

            // ---- entrance (title.js:122-143) ----
            _fade = Styler.Of(MetaStyle.Fade(_root), o: 1f);
            _clock.Tween(_fade, "o", 0f, 1600f, Ease.InOut);
            _pf0 = Styler.Of(_figureWrap, o: 0f, y: 12f);
            _clock.Tween(_pf0, new[] { ("o", 1f), ("y", 0f) }, 1400f, Ease.Out, 500f);
            _pn0 = Styler.Of(_name.rectTransform, o: 0f, y: 8f);
            _clock.Tween(_pn0, new[] { ("o", 1f), ("y", 0f) }, 900f, Ease.Out, 900f);
            UiNode.Of(_rule).SetOrigin(0f, 0.5f);   // rule.style.transformOrigin = '0 50%'
            Styler pr0 = Styler.Of(_rule, o: 0f, s: 0.4f);
            _clock.Tween(pr0, new[] { ("o", 1f), ("s", 1f) }, 1100f, Ease.Out, 1000f);
            for (int i = 0; i < _taglineText.Cells.Count; i++)
            {
                Styler p = Styler.Of(_taglineText.Cells[i], o: 0f, y: -10f);
                _clock.Tween(p, new[] { ("o", 1f), ("y", 0f) }, 700f, Ease.Out, MetaTimelines.TitleTaglineCharAt(i));
            }
            var menuIn = new List<Promise>();
            for (int i = 0; i < _rows.Count; i++)
            {
                Styler p = Styler.Of(_rows[i].Node, o: 0f, x: -16f);
                menuIn.Add(_clock.Tween(p, new[] { ("o", 1f), ("x", 0f) }, 500f, Ease.Out, MetaTimelines.TitleMenuItemAt(i)));
            }
            // r.style.transform = ''; r.style.opacity = ''
            Promise.All(menuIn).Then(() =>
            {
                foreach (Row r in _rows)
                {
                    if (!r.Node) continue;
                    r.Node.ResetTransform();
                    r.Node.Opacity = 1f;
                }
            }).Forget();
            Styler pk = Styler.Of(_keys.rectTransform, o: 0f);
            _clock.Tween(pk, "o", 1f, 700f, Ease.Out, 2400f);

            Breathe(MetaUtil.PageNow);
            _popKeys = KeyStack.Push(OnKey);
        }

        // js: scenes/meta/title.js:15
        /// <summary>mountTitle(host, ctx) → { destroy() }.</summary>
        public static IScene Mount(RectTransform host, TitleCtx ctx) => new TitleScene(host, ctx);

        // js: scenes/meta/title.js:67
        /// <summary>
        /// One item (button.ocbg-meta-title-item): the selection line with its lamp, the label, and the note of a shut
        /// つづきから (small), laid out as its flex row lays them (padding 10 0 10 48, baseline aligned, gap 16).
        /// </summary>
        private Row MakeRow(int i, TitleMenuItem it, float y)
        {
            var row = new Row();
            row.El = Ui.Div(_menu, "ocbg-meta-title-item" + (it.Enabled ? "" : " is-off"), 0f, y, MenuW, 0f);
            row.Node = UiNode.Of(row.El);
            Insets pad = global::Trial.Geometry.Css.Box(ItemCss).Padding;

            // the line (the button's first child): absolute, left 0, top 50% − 1, 32 × 2, scaleX 0 → 1 about its left
            // end; its lamp (::after) at right −4, top −3. Its top is set once the row's height is known.
            row.Mark = BoxGraphic.Make(row.El, MarkCss, 0f, 0f, MarkW, MarkH, "ocbg-meta-title-mark");
            UiNode.Of(row.Mark.rectTransform).SetOrigin(0f, 0.5f);   // transform-origin: 0 50%
            row.Lamp = BoxGraphic.Make(row.Mark.rectTransform, MarkAfterCss, MarkW - LampRight - LampSize, LampTop, LampSize, LampSize, "after");

            // the label (span) and the note (small): baseline aligned
            TextStyle spanStyle = MetaStyle.Text(ItemCss);
            row.Span = Label.Make(row.El, it.Label, spanStyle, pad.Left, pad.Top, null, null, "span");
            float spanBase = BaselineOf(row.Span), spanH = spanStyle.LineHeightPx;
            float above = spanBase, below = spanH - spanBase;
            float smallBase = 0f;
            if (!it.Enabled && it.Key == TitleMenu.KeyContinue && it.Note != null)
            {
                TextStyle smallStyle = MetaStyle.Text(ItemCss + ItemSmallCss + ItemOffSmallCss);
                row.Small = Label.Make(row.El, it.Note, smallStyle, 0f, 0f, null, null, "small");
                UiNode.Of(row.Small.rectTransform).Opacity = 0.85f;   // .is-off small { opacity: .85 }
                smallBase = BaselineOf(row.Small);
                above = Math.Max(above, smallBase);
                below = Math.Max(below, smallStyle.LineHeightPx - smallBase);
            }
            float rowH = pad.Top + above + below + pad.Bottom;
            row.Node.H = rowH;
            Ui.Place(row.Span.rectTransform, pad.Left, pad.Top + above - spanBase);
            if (row.Small)
            {
                float spanW = row.Span.rectTransform.sizeDelta.x;
                Ui.Place(row.Small.rectTransform, pad.Left + spanW + ItemInnerGap, pad.Top + above - smallBase);
            }

            UiNode.Of(row.Mark.rectTransform).Place(0f, rowH / 2f - MarkH / 2f);   // top: 50%; margin-top: -1px

            // the states: .is-on (letter-spacing .2s, text-shadow .2s on the label; the line's scaleX and the lamp's
            // opacity .22s), .is-off > span (opacity .4)
            row.SpanStates = CssStates.Of(row.Span).Base(ItemCss).State(".is-on", ItemOnCss).State(".is-off > span", ItemOffSpanCss, !it.Enabled);
            row.MarkStates = CssStates.Of(row.Mark).Base(MarkCss).State(".is-on", MarkOnCss);
            row.LampStates = CssStates.Of(row.Lamp).Base(MarkAfterCss).State(".is-on", MarkAfterOnCss);

            // onmouseenter: focus(i, true); onclick: choose(i)
            ClickArea area = ClickArea.On(row.El);
            int index = i;
            area.Enter += (PointerEventData _) => Focus(index, true);
            area.Click += (PointerEventData _) => Choose(index);
            return row;
        }

        /// <summary>The first baseline below a label's top (CSS: half-leading + ascent).</summary>
        private static float BaselineOf(TMP_Text t)
        {
            LabelState st = Label.StateOf(t);
            return st.HalfLeading + st.AscentPx;
        }

        /// <summary>classList.toggle('is-on', on) on an item: its label, line and lamp transition (the first time at once).</summary>
        private static void SetOn(Row r, bool on, bool instant = false)
        {
            if (r == null || !r.El) return;
            r.SpanStates.SetState(".is-on", on, false);
            r.MarkStates.SetState(".is-on", on, false);
            r.LampStates.SetState(".is-on", on, false);
            r.SpanStates.Apply(instant);
            r.MarkStates.Apply(instant);
            r.LampStates.Apply(instant);
        }

        // js: scenes/meta/title.js:56
        /// <summary>
        /// startNew: with no save (and no unreadable one) the title walks out as usual; over a save the router first asks
        /// whether to wipe it, over the title as it stands (uiClick, then onNew; its 「はじめから」 moves on to the hall).
        /// </summary>
        private void StartNew(TitleChoice choice)
        {
            if (choice == TitleChoice.LeaveNew)
            {
                Leave(_ctx.OnNew, true).Forget();
                return;
            }
            _sound.Play("uiClick");
            _ctx.OnNew?.Invoke();
        }

        // js: scenes/meta/title.js:87
        /// <summary>focus(i, fromPointer): the cursor to item i (not a shut one): its row lit, nodeHover by pointer, uiClick by key.</summary>
        private void Focus(int i, bool fromPointer)
        {
            string sound = _items.Focus(i, fromPointer);
            if (sound == null) return;
            Light();
            _sound.Play(sound);
        }

        private void Light()
        {
            for (int k = 0; k < _rows.Count; k++) SetOn(_rows[k], k == _items.Lit);
        }

        // js: scenes/meta/title.js:94
        /// <summary>move(by): ↑ (−1) / ↓ (+1) to the next item that opens, round the ends.</summary>
        private void Move(int by)
        {
            string sound = _items.Move(by);
            if (sound == null) return;
            Light();
            _sound.Play(sound);
        }

        // js: scenes/meta/title.js:101
        /// <summary>choose(i): nothing while leaving or on a shut item; else the item's run.</summary>
        private void Choose(int i)
        {
            TitleChoice choice = _items.Choose(i);
            switch (choice)
            {
                case TitleChoice.LeaveNew:
                case TitleChoice.AskNew:
                    StartNew(choice);
                    break;
                case TitleChoice.LeaveContinue:
                    Leave(_ctx.OnContinue, true).Forget();
                    break;
                case TitleChoice.Settings:
                    // js: scenes/meta/title.js:64
                    _sound.Play("uiClick");
                    _ctx.OnSettings?.Invoke();
                    break;
            }
        }

        // js: scenes/meta/title.js:108
        /// <summary>
        /// leave(cb, enter): leaving into the den — the menu lets go, the heir walks into the dark, then the router moves
        /// on (cb once the curtain has closed, 1100 ms on).
        /// </summary>
        private async Promise Leave(Action cb, bool enter)
        {
            if (_busy) return;
            _busy = true;
            _items.Busy = true;
            if (enter) _sound.Play("titleEnter");
            _sound.Play("uiConfirm");
            Styler pm = Styler.Of(_menu), pt = Styler.Of(_tagline);
            Styler pf = _pf0, pn = _pn0;   // the entrance's channels (see the head of the file)
            _clock.Tween(pm, new[] { ("o", 0f), ("x", -24f) }, 360f, Ease.In).Forget();
            _clock.Tween(pn, "o", 0f, 420f, Ease.In).Forget();
            _clock.Tween(pt, "o", 0f, 700f, Ease.In).Forget();
            _clock.Tween(pf, new[] { ("o", 0f), ("y", 26f), ("s", 0.92f) }, 900f, Ease.In).Forget();
            await _clock.Tween(_fade, "o", 1f, 900f, Ease.InOut, 200f);
            cb?.Invoke();
        }

        // js: scenes/meta/title.js:146
        /// <summary>breathe(now): the heir's foot light breathes with the spring (0.55〜0.90 every 4200 ms).</summary>
        private void Breathe(double now)
        {
            if (_foot) _foot.Opacity = (float)MetaTimelines.TitleFootLight(now);
        }

        // js: scenes/meta/title.js:151
        /// <summary>
        /// The title's keys: nothing while leaving or while the router's confirm card is open over the title; ↑ / w, ↓ / s,
        /// Enter / Space, and Esc (a fresh press only) for the settings when they are offered.
        /// </summary>
        private bool OnKey(KeyEvent e)
        {
            if (_busy) return false;
            if (Stage.AnyConfirmOpen()) return false;   // the router's question is open over the title
            switch (TitleMenu.KeyOf(e.Key))
            {
                case TitleKey.Up:
                    Move(-1);
                    return true;
                case TitleKey.Down:
                    Move(1);
                    return true;
                case TitleKey.Choose:
                    Choose(_items.Cur);
                    return true;
                case TitleKey.Settings:
                    // a held Esc repeats: only a fresh press opens the settings (the panel closes on a fresh press only, too)
                    if (_ctx.OnSettings == null) return false;
                    if (!e.Repeat)
                    {
                        _sound.Play("uiClick");
                        _ctx.OnSettings();
                    }
                    return true;
                default:
                    return false;
            }
        }

        // js: scenes/meta/title.js:163
        public void Destroy()
        {
            _popKeys?.Invoke();
            _clock.Stop();
            _backdrop.Destroy();
            if (_root) Ui.Remove(_root);
        }

        /// <summary>The JS scene has no refresh().</summary>
        public void Refresh() { }
    }
}
