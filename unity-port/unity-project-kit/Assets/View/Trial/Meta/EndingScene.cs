// Port of scenes/meta/ending.js: the end of a life (dungeon_exploration_v4.md §6.9, concept-v3 §8) — a sheet of washi
// laid over the place it ended, reading Inheritance.EndingView: the end sentence, what stays behind (the trace and its
// candidates, the cards the shallow rule kept out) or what passes on (the whole journal, the survivor's bonus), the
// next heir, and 「継承の間へ」. withWaitingTrace (ending.js:23) is Trial.Script.MetaLines.WithWaitingTrace; the times
// are MetaTimelines; every word comes from the view.
//
// The sheet is laid out as its flex column lays it (padding 64 92 52, gap 16, min-height 560, the footer pushed to the
// bottom by margin-top: auto), takes the height of what it says and stands centred on the screen.
//
// As in the JS (meta-a.md §4.3 names it a quirk; TRIAL-PORT.md: the JS look wins, and the reference shot
// 862-ending-death-layer1-skipped shows it): showAll() drops the sheet's inline transform, so the tilt the entrance lays
// it down with (−0.4° for a death, +0.3° for a life closed alive) goes back to 0° when the entrance is skipped, and
// toHall() lifts the sheet away from 0° (its fresh styler, styler(sheet, { o: 1 })).
using System;
using System.Collections.Generic;
using DungeonCore;
using InheritanceCore;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using Trial.Script;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Trial.Meta
{
    public sealed class EndingScene : IScene
    {
        // ---- meta.css: the ending card, verbatim ------------------------------------------------------------------

        /// <summary>meta.css:276 .ocbg-meta-ending-glow.</summary>
        public const string GlowCss = "position: absolute; pointer-events: none; border-radius: 50%; background: radial-gradient(closest-side, color-mix(in srgb, var(--wick) 26%, transparent), color-mix(in srgb, var(--wick) 0%, transparent));";

        /// <summary>meta.css:277 .ocbg-meta-ending.is-death .ocbg-meta-ending-glow.</summary>
        public const string GlowDeathCss = "background: radial-gradient(closest-side, color-mix(in srgb, var(--violet) 22%, transparent), color-mix(in srgb, var(--violet) 0%, transparent));";

        /// <summary>meta.css:278 .ocbg-meta-ending-sheet (over .ocbg-meta-washi, MetaStyle.Washi).</summary>
        public const string SheetCss = "display: flex; flex-direction: column; gap: var(--space-16); min-height: 560px; padding: 64px 92px 52px; border-radius: 3px; box-shadow: 0 34px 80px color-mix(in srgb, var(--ground) 85%, transparent), inset 0 0 0 1px color-mix(in srgb, var(--washi-ink) 10%, transparent), inset 0 0 60px color-mix(in srgb, var(--washi-ink) 7%, transparent); transform-origin: 50% 40%;";

        /// <summary>meta.css:279 .ocbg-meta-ending-sheet header.</summary>
        public const string HeaderCss = "display: flex; align-items: center; gap: var(--space-24);";

        /// <summary>meta.css:280 .ocbg-meta-ending-mark.</summary>
        public const string MarkCss = "width: 72px; height: 72px; flex: none; display: flex; align-items: center; justify-content: center; border: var(--stroke-frame) solid var(--washi-ink); border-radius: 50%;";

        /// <summary>meta.css:281 .ocbg-meta-ending-sheet h1.</summary>
        public const string H1Css = "font: 800 72px/1 var(--font-display); letter-spacing: .32em;";

        /// <summary>meta.css:282 .ocbg-meta-ending-sheet h1 span, .ocbg-meta-ending-sentence span.</summary>
        public const string CharSpanCss = "display: inline-block;";

        /// <summary>meta.css:401 the same spans (the later rule).</summary>
        public const string CharSpanPreCss = "white-space: pre;";

        /// <summary>meta.css:283 .ocbg-meta-ending-heir.</summary>
        public const string HeirCss = "font: 700 26px/1.3 var(--font-display); letter-spacing: .1em; margin-top: var(--space-8);";

        /// <summary>meta.css:284 .ocbg-meta-ending-sentence.</summary>
        public const string SentenceCss = "font: 400 34px/1.6 var(--font-display); letter-spacing: .06em;";

        /// <summary>meta.css:285 .ocbg-meta-ending-rule.</summary>
        public const string RuleCss = "display: block; height: 2px; background: color-mix(in srgb, var(--washi-ink) 28%, transparent); margin: var(--space-8) 0;";

        /// <summary>meta.css:286 .ocbg-meta-ending-lines.</summary>
        public const string LinesCss = "display: flex; flex-direction: column; gap: var(--space-12);";

        /// <summary>meta.css:287 .ocbg-meta-ending-lines li.</summary>
        public const string LiCss = "display: flex; align-items: baseline; gap: var(--space-16); font: 400 22px/1.5 var(--font-body);";

        /// <summary>meta.css:288 .ocbg-meta-ending-dot.</summary>
        public const string DotCss = "width: 8px; height: 8px; flex: none; border-radius: 50%; background: var(--washi-ink); transform: translateY(-4px);";

        /// <summary>meta.css:289 .ocbg-meta-ending-sheet footer.</summary>
        public const string FooterCss = "margin-top: auto; display: flex; align-items: center; justify-content: space-between; gap: var(--space-24); padding-top: var(--space-24); border-top: var(--stroke-rule) solid color-mix(in srgb, var(--washi-ink) 22%, transparent);";

        /// <summary>meta.css:290 .ocbg-meta-ending-next.</summary>
        public const string NextCss = "font: 700 26px/1.3 var(--font-display); letter-spacing: .08em;";

        /// <summary>meta.css:404 .ocbg-meta-ending-place (where it ended).</summary>
        public const string PlaceCss = "display: flex; align-items: center; justify-content: center; gap: var(--space-24); font: 700 26px/1 var(--font-display); letter-spacing: .2em; color: var(--ink); text-shadow: 0 2px 14px color-mix(in srgb, var(--ground) 90%, transparent);";

        /// <summary>meta.css:405 .ocbg-meta-ending-place i (the two hairlines).</summary>
        public const string PlaceLineCss = "width: 120px; height: 1px; background: linear-gradient(90deg, color-mix(in srgb, var(--ink) 0%, transparent), color-mix(in srgb, var(--ink) 55%, transparent));";

        /// <summary>meta.css:406 .ocbg-meta-ending-place i:last-child.</summary>
        public const string PlaceLineLastCss = "transform: scaleX(-1);";

        // ---- layout numbers of ending.js (stage px) and the gaps of its rules ---------------------------------------

        // js: scenes/meta/ending.js:62 the glow: 960 - 620, 540 - 470, 1240 × 940
        private const float GlowX = 960f - 620f, GlowY = 540f - 470f, GlowW = 1240f, GlowH = 940f;

        // js: scenes/meta/ending.js:67 the place's line: 0, 64, 1920 wide
        private const float PlaceY = 64f, PlaceW = 1920f;

        // js: scenes/meta/ending.js:71 the sheet: 960 - 520, 1040 wide; ending.js:92 its top max(40, round((1080 - h) / 2))
        private const float SheetX = 960f - 520f, SheetW = 1040f, SheetMinTop = 40f;

        /// <summary>meta.css:278 min-height: 560px.</summary>
        private const float SheetMinH = 560f;

        /// <summary>var(--space-8 / 12 / 16 / 24): the rules' gaps and margins.</summary>
        private const float Space8 = 8f, Space12 = 12f, Space16 = 16f, Space24 = 24f;

        /// <summary>meta.css:405 the place's hairlines, 120 × 1.</summary>
        private const float PlaceLineW = 120f, PlaceLineH = 1f;

        /// <summary>meta.css:280 the mark, 72 × 72; ending.js:73 its icon at 40.</summary>
        private const float MarkSize = 72f, MarkIcon = 40f;

        /// <summary>meta.css:288 the dot, 8 × 8, lifted 4.</summary>
        private const float DotSize = 8f, DotLift = -4f;

        private static readonly LifeEnding[] Deaths = { LifeEnding.Fallen, LifeEnding.MiasmaDeath };

        // ---- the scene --------------------------------------------------------------------------------------------

        private readonly EndingCtx _ctx;
        private readonly MetaSound _sound;
        private readonly EndingView _view;
        private readonly bool _died;
        private readonly RectTransform _root;
        private readonly MetaBackdrop _backdrop;
        private readonly SceneClock _clock;
        private SceneClock _live;
        private readonly BoxGraphic _glow;
        private readonly RectTransform _caption;
        private readonly WashiBox _sheet;
        private readonly RectTransform _mark;
        private readonly List<TextMeshProUGUI> _titleChars = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI _heirLine;
        private readonly List<TextMeshProUGUI> _sentenceChars = new List<TextMeshProUGUI>();
        private readonly RectTransform _rule;
        private readonly List<RectTransform> _lineEls = new List<RectTransform>();
        private readonly TextMeshProUGUI _next;
        private readonly MetaButton _button;
        private readonly Styler _fade;
        private readonly Styler _ps;
        private readonly Styler _pb;
        private readonly float _tiltTo;
        private readonly Action _popKeys;
        private bool _alive = true;
        private bool _busy;
        private bool _shown;

        // js: scenes/meta/ending.js:47
        private EndingScene(RectTransform host, EndingCtx ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _sound = MetaUtil.SoundOf(ctx);
            Lineage lineage = ctx.Lineage;
            _view = MetaLines.WithWaitingTrace(ctx.Ending ?? Inheritance.EndingView(lineage), lineage);
            LifeRecord lastLife = lineage != null && lineage.Lives.Count > 0 ? lineage.Lives[lineage.Lives.Count - 1] : null;
            // ctx.layer || (lastLife ? lastLife.Layer : 7)
            int layer = ctx.Layer.HasValue && ctx.Layer.Value != 0 ? ctx.Layer.Value : (lastLife != null ? lastLife.Layer : 7) ?? 7;
            _died = Array.IndexOf(Deaths, _view.Ending) >= 0;
            _root = MetaUtil.SceneRoot(host, "ending" + (_died ? " is-death" : " is-alive"));

            // The place the life ended, under its exploration veil and a little more night.
            var place0 = global::Backdrop.ExplorationBackdropSpec.For(layer);
            _backdrop = MetaBackdrop.Create(_root, MetaBackdrop.LayerBackdrop(layer),
                new MetaBackdrop.Options { Veil = place0.Veil, Vignette = 0.75f, MoteSpeed = _died ? 0.35f : 0.8f });
            _clock = MetaUtil.SceneClock((dt, now) => { _backdrop.Paint(now); GlowBreath(now); }, ctx.Stage);
            _live = _clock;

            _glow = BoxGraphic.Make(_root, GlowCss + (_died ? GlowDeathCss : ""), GlowX, GlowY, GlowW, GlowH, "ocbg-meta-ending-glow");

            // ---- where it ended (ending.js:65-68) ----
            string placeName = InheritanceText.LayerTitle(layer, SevenLayers.Of(layer).DisplayName(lineage != null && lineage.Player.CarvingTaken));
            _caption = MakePlace(placeName);

            // ---- the sheet (ending.js:70-92) ----
            _sheet = MetaStyle.Washi(_root, SheetX, 120f, SheetW, SheetMinH, SheetCss, "ocbg-meta-washi ocbg-meta-ending-sheet");
            BoxStyle sheetBox = global::Trial.Geometry.Css.Box(SheetCss);
            Insets pad = sheetBox.Padding;
            float contentW = SheetW - pad.Horizontal - sheetBox.BorderWidths.Horizontal;
            float left = sheetBox.BorderWidths.Left + pad.Left;
            float y = sheetBox.BorderWidths.Top + pad.Top;
            RectTransform sheet = _sheet.Root;

            // header: the mark and the title, centred on each other (gap 24)
            float headerH = MarkSize;
            TextStyle h1Style = SheetText(H1Css + CharSpanCss + CharSpanPreCss);
            headerH = Math.Max(headerH, h1Style.LineHeightPx);
            RectTransform header = Ui.Div(sheet, "header", left, y, contentW, headerH);
            UiNode.Of(header);
            BoxGraphic mark = BoxGraphic.Make(header, MarkCss, 0f, (headerH - MarkSize) / 2f, MarkSize, MarkSize, "ocbg-meta-ending-mark");
            _mark = mark.rectTransform;
            string iconName = _view.Ending == LifeEnding.MiasmaDeath ? "miasma" : _view.Ending == LifeEnding.Completed ? "star" : "person";
            PathGraphic icon = MetaUtil.Icon(_mark, iconName, MarkIcon, DsTokens.WashiInk);
            Ui.Place(icon.rectTransform, (MarkSize - MarkIcon) / 2f, (MarkSize - MarkIcon) / 2f);
            RectTransform h1 = Ui.Div(header, "h1", MarkSize + Space24, (headerH - h1Style.LineHeightPx) / 2f, 0f, h1Style.LineHeightPx);
            float hx = 0f;
            foreach (string c in MetaTimelines.Chars(_view.Title))
            {
                float w = CharBoxWidth(c, h1Style);
                _titleChars.Add(Label.Make(h1, c, h1Style, hx, 0f, w, h1Style.LineHeightPx, "span"));
                hx += w;
            }
            UiNode.Of(h1).W = hx;
            y += headerH + Space16;

            // the heir's line (margin-top 8)
            y += Space8;
            _heirLine = Label.Make(sheet, _view.HeirLine, SheetP(HeirCss), left, y, contentW, null, "ocbg-meta-ending-heir");
            y += _heirLine.rectTransform.sizeDelta.y + Space16;

            // the sentence, one box per letter (spaces included); a line breaks between any two
            TextStyle sStyle = SheetText(SentenceCss + CharSpanCss + CharSpanPreCss);
            float slh = sStyle.LineHeightPx;
            RectTransform sentence = Ui.Div(sheet, "ocbg-meta-ending-sentence", left, y, contentW, 0f);
            UiNode.Of(sentence);
            float sx = 0f, sy = 0f;
            foreach (string c in MetaTimelines.Chars(_view.Sentence))
            {
                float w = CharBoxWidth(c, sStyle);
                if (sx > 0f && sx + w > contentW + 0.01f)
                {
                    sx = 0f;
                    sy += slh;
                }
                _sentenceChars.Add(Label.Make(sentence, c, sStyle, sx, sy, w, slh, "span"));
                sx += w;
            }
            float sentenceH = _sentenceChars.Count > 0 ? sy + slh : 0f;
            UiNode.Of(sentence).H = sentenceH;
            y += sentenceH + Space16;

            // the rule (margin 8 0)
            y += Space8;
            BoxGraphic rule = BoxGraphic.Make(sheet, RuleCss, left, y, contentW, 2f, "ocbg-meta-ending-rule");
            _rule = rule.rectTransform;
            y += 2f + Space8 + Space16;

            // the lines: a dot lifted 4 px off each line's first baseline, then the words (gap 16), 12 apart
            RectTransform list = Ui.Div(sheet, "ocbg-meta-ending-lines", left, y, contentW, 0f);
            UiNode.Of(list);
            TextStyle liStyle = SheetP(LiCss);
            float ly = 0f;
            for (int i = 0; i < _view.Lines.Count; i++)
            {
                if (i > 0) ly += Space12;
                RectTransform li = Ui.Div(list, "li", 0f, ly, contentW, 0f);
                UiNode liNode = UiNode.Of(li);
                BoxGraphic dot = BoxGraphic.Make(li, DotCss, 0f, 0f, DotSize, DotSize, "ocbg-meta-ending-dot");
                TextMeshProUGUI text = Label.Make(li, _view.Lines[i], liStyle, DotSize + Space16, 0f, contentW - DotSize - Space16, null, "span");
                LabelState st = Label.StateOf(text);
                float baseline = st.HalfLeading + st.AscentPx;
                // align-items: baseline — the empty dot's baseline is its bottom edge
                UiNode dotNode = UiNode.Of(dot.rectTransform);
                dotNode.Place(0f, baseline - DotSize);
                dotNode.SetStyleTransform(ty: DotLift);
                float liH = text.rectTransform.sizeDelta.y;
                liNode.H = liH;
                _lineEls.Add(li);
                ly += liH;
            }
            UiNode.Of(list).H = ly;
            y += ly + Space16;

            // the footer: the next heir on the left, 「継承の間へ」 on the right, centred on each other; pushed to the bottom
            BoxStyle footBox = global::Trial.Geometry.Css.Box(FooterCss);
            float footTop = footBox.BorderWidths.Top + Space24;   // border-top 1 + padding-top var(--space-24)
            RectTransform footer = BoxGraphic.Make(sheet, FooterCss, left, y, contentW, 0f, "footer").rectTransform;
            _button = MetaButton.Washi(footer, _view.Button, "Enter");   // scene text: ending.js:83 h('small', null, 'Enter')
            _button.Click += () => ToHall().Forget();
            float bw = _button.Node.W, bh = _button.Node.H;
            TextStyle nextStyle = SheetP(NextCss);
            float nextAvail = Math.Max(0f, contentW - Space24 - bw);
            float nextW = TextMeasure.Width(_view.NextLine ?? "", nextStyle);
            _next = nextW > nextAvail
                ? Label.Make(footer, _view.NextLine, nextStyle, 0f, 0f, nextAvail, null, "ocbg-meta-ending-next")
                : Label.Make(footer, _view.NextLine, nextStyle, 0f, 0f, null, null, "ocbg-meta-ending-next");
            float nh = _next.rectTransform.sizeDelta.y;
            float rowH = Math.Max(nh, bh);
            Ui.Place(_next.rectTransform, 0f, footTop + (rowH - nh) / 2f);
            _button.Place(contentW - bw, footTop + (rowH - bh) / 2f);
            float footH = footTop + rowH;
            float bottom = y + footH + pad.Bottom + sheetBox.BorderWidths.Bottom;

            // The sheet takes the height of what it says (min-height 560: the footer goes to the bottom), centred:
            // sheet.style.top = max(40, round((1080 - sheet.offsetHeight) / 2)), offsetHeight being the height snapped to
            // whole px (the sheet's top, 120, is whole).
            float sheetH = Math.Max(SheetMinH, bottom);
            float footY = sheetH - pad.Bottom - sheetBox.BorderWidths.Bottom - footH;
            UiNode.Of(footer).Place(left, footY, contentW, footH);
            _sheet.Resize(SheetW, sheetH);
            int offsetHeight = MetaUtil.JsRound(sheetH);
            _sheet.Node.Place(SheetX, Math.Max(SheetMinTop, MetaUtil.JsRound((1080f - offsetHeight) / 2f)));
            _sheet.Node.SetOrigin(0.5f, 0.4f);   // transform-origin: 50% 40%

            // ---- motion: the sheet is laid down, the words are written, what stays appears line by line ----
            _fade = Styler.Of(MetaStyle.Fade(_root), o: 1f);
            _clock.Tween(_fade, "o", 0f, 900f, Ease.Out);
            Styler pc = Styler.Of(_caption, o: 0f);
            _clock.Tween(pc, "o", 1f, 1400f, Ease.Out, 200f);
            _tiltTo = _died ? -0.4f : 0.3f;
            _ps = Styler.Of(_sheet.Node, o: 0f, y: 26f, s: 0.985f, r: _died ? -0.8f : 0.6f);
            _clock.Tween(_ps, new[] { ("o", 1f), ("y", 0f), ("s", 1f), ("r", _tiltTo) }, 900f, Ease.Out, 300f);
            if (_view.Ending == LifeEnding.Fallen) _sound.Play("lifeEndDeath", 300);
            else if (!_died) _sound.Play("survived", 300);
            Styler pm = Styler.Of(_mark, o: 0f, s: 0.8f);
            _clock.Tween(pm, new[] { ("o", 1f), ("s", 1f) }, 700f, Ease.Out, 700f);
            for (int i = 0; i < _titleChars.Count; i++)
            {
                Styler p = Styler.Of(_titleChars[i].rectTransform, o: 0f, y: 10f);
                _clock.Tween(p, new[] { ("o", 1f), ("y", 0f) }, 520f, Ease.Out, MetaTimelines.EndingTitleCharAt(i));
            }
            EndingTimeline t = MetaTimelines.Ending(_titleChars.Count, _sentenceChars.Count, _lineEls.Count);
            Styler ph = Styler.Of(_heirLine.rectTransform, o: 0f);
            _clock.Tween(ph, "o", 1f, 600f, Ease.Out, t.T1);
            for (int i = 0; i < _sentenceChars.Count; i++)
            {
                Styler p = Styler.Of(_sentenceChars[i].rectTransform, o: 0f);
                _clock.Tween(p, "o", 1f, 260f, Ease.Out, MetaTimelines.EndingSentenceCharAt(t.T1, i));
            }
            UiNode.Of(_rule).SetOrigin(0f, 0.5f);   // rule.style.transformOrigin = '0 50%'
            Styler pr = Styler.Of(_rule, o: 0f, s: 0.2f);
            _clock.Tween(pr, new[] { ("o", 1f), ("s", 1f) }, 700f, Ease.Out, t.T2);
            for (int i = 0; i < _lineEls.Count; i++)
            {
                Styler p = Styler.Of(_lineEls[i], o: 0f, x: -10f);
                _clock.Tween(p, new[] { ("o", 1f), ("x", 0f) }, 480f, Ease.Out, MetaTimelines.EndingLineAt(t.T2, i));
            }
            Styler pn = Styler.Of(_next.rectTransform, o: 0f);
            _clock.Tween(pn, "o", 1f, 600f, Ease.Out, t.T3);
            _pb = _button.Styler(o: 0f, y: 8f);
            _clock.Tween(_pb, new[] { ("o", 1f), ("y", 0f) }, 500f, Ease.Out, t.ButtonAt);

            GlowBreath(MetaUtil.PageNow);

            // A press anywhere while the words are still being written lays the whole sheet down at once (ending.js:150).
            // A press on the button stays with the button (its own pointer target), as button.contains(e.target) returns.
            ClickArea.On(_root).Down += OnPointerDown;
            _popKeys = KeyStack.Push(OnKey);
        }

        // js: scenes/meta/ending.js:47
        /// <summary>mountEnding(host, ctx) → { destroy() }.</summary>
        public static IScene Mount(RectTransform host, EndingCtx ctx) => new EndingScene(host, ctx);

        /// <summary>A text style inside the sheet: .ocbg-meta-washi's colour (washi-ink), the sheet's rule, then css.</summary>
        private static TextStyle SheetText(string css) => MetaStyle.Text(MetaStyle.WashiPaintCss + SheetCss + (css ?? ""));

        /// <summary>A p / li inside the sheet (word-break: auto-phrase, meta.css:438).</summary>
        private static TextStyle SheetP(string css) => MetaStyle.P(MetaStyle.WashiPaintCss + SheetCss + (css ?? ""));

        /// <summary>
        /// The width of one letter's inline-block span (white-space: pre): its advance plus the letter-spacing after it. A
        /// lone space is measured between two ideographs, as TMP may leave a trailing space out of a preferred width.
        /// </summary>
        private static float CharBoxWidth(string ch, TextStyle s)
        {
            if (string.IsNullOrEmpty(ch)) return 0f;
            if (!string.IsNullOrWhiteSpace(ch)) return TextMeasure.Width(ch, s);
            return Math.Max(0f, TextMeasure.Width("一" + ch + "一", s) - 2f * TextMeasure.Width("一", s));
        }

        // js: scenes/meta/ending.js:67
        /// <summary>
        /// .ocbg-meta-ending-place at (0, 64), 1920 wide: two 120 × 1 hairlines (the right one mirrored) 24 either side of
        /// the place's name, the three centred on the screen and on each other.
        /// </summary>
        private RectTransform MakePlace(string placeName)
        {
            TextStyle style = MetaStyle.Text(PlaceCss);
            float h = style.LineHeightPx;
            RectTransform caption = Ui.Div(_root, "ocbg-meta-ending-place", 0f, PlaceY, PlaceW, h);
            UiNode.Of(caption);
            BoxGraphic l = BoxGraphic.Make(caption, PlaceLineCss, 0f, (h - PlaceLineH) / 2f, PlaceLineW, PlaceLineH, "i");
            TextMeshProUGUI text = Label.Make(caption, placeName, style, 0f, 0f, null, null, "span");
            BoxGraphic r = BoxGraphic.Make(caption, PlaceLineCss, 0f, (h - PlaceLineH) / 2f, PlaceLineW, PlaceLineH, "i");
            UiNode.Of(r.rectTransform).SetStyleTransform(sx: -1f);   // i:last-child { transform: scaleX(-1) }
            float tw = text.rectTransform.sizeDelta.x;
            float x = (PlaceW - (PlaceLineW + Space24 + tw + Space24 + PlaceLineW)) / 2f;
            UiNode.Of(l.rectTransform).X = x;
            Ui.Place(text.rectTransform, x + PlaceLineW + Space24, (h - text.rectTransform.sizeDelta.y) / 2f);
            UiNode.Of(r.rectTransform).X = x + PlaceLineW + Space24 + tw + Space24;
            TextGlow.For(text);   // text-shadow 0 2px 14px ground 90%
            return caption;
        }

        // js: scenes/meta/ending.js:123
        /// <summary>
        /// glowBreath(now): a death leaves a cold breath of the place (0.35〜0.60 every 6200 ms); a life closed alive keeps
        /// the warm light of the heirs (0.60〜0.85 every 3800 ms).
        /// </summary>
        private void GlowBreath(double now)
        {
            if (_glow) _glow.Opacity = (float)MetaTimelines.EndingGlow(now, _died);
        }

        /// <summary>Number(button.style.opacity): the button has come out fully once this reaches 1.</summary>
        private float ButtonOpacity => _button != null && _button.Node ? _button.Node.Opacity : 0f;

        // js: scenes/meta/ending.js:132
        /// <summary>
        /// showAll(): the first key (or a press) shows everything at once: the clock stops, every element is opaque and
        /// untransformed (the sheet too: its tilt goes, as in the JS), the curtain is open, and a new clock moves only the
        /// backdrop and the glow.
        /// </summary>
        private void ShowAll()
        {
            _shown = true;
            _live.Stop();
            Show(_caption);
            Show(_sheet.Root);
            Show(_mark);
            foreach (TextMeshProUGUI c in _titleChars) Show(c.rectTransform);
            Show(_heirLine.rectTransform);
            foreach (TextMeshProUGUI c in _sentenceChars) Show(c.rectTransform);
            Show(_rule);
            foreach (RectTransform li in _lineEls) Show(li);
            Show(_next.rectTransform);
            if (_button.Node)
            {
                _button.Node.Opacity = 1f;
                _button.ClearTransform();   // el.style.transform = '': the :hover lift applies again
            }
            _fade.O = 0f;
            _live = MetaUtil.SceneClock((dt, now) => { _backdrop.Paint(now); GlowBreath(now); }, _ctx.Stage);
        }

        /// <summary>el.style.opacity = '1'; el.style.transform = ''.</summary>
        private static void Show(RectTransform el)
        {
            if (!el) return;
            UiNode n = UiNode.Of(el);
            n.Opacity = 1f;
            n.ResetTransform();
        }

        // js: scenes/meta/ending.js:139
        /// <summary>toHall(): uiConfirm, the sheet lifts away (420 ms in), the curtain closes (600 ms in, 150 later), onHall.</summary>
        private async Promise ToHall()
        {
            if (_busy) return;
            _busy = true;
            _sound.Play("uiConfirm");
            Styler pf = _fade.Set(o: 0f);   // styler(fade.el, { o: 0 })
            // styler(sheet, { o: 1 }): a fresh styler (o 1, x 0, y 0, s 1, r 0) written at once. Its tween is the newer one,
            // so it wins over the entrance's (still running when the button is clicked before it has come out): the
            // entrance's tween on the sheet goes.
            _live.Kill(_ps);
            Styler pz = _ps.Set(o: 1f, x: 0f, y: 0f, s: 1f, r: 0f);
            _live.Tween(pz, new[] { ("o", 0f), ("y", -16f) }, 420f, Ease.In).Forget();
            await _live.Tween(pf, "o", 1f, 600f, Ease.In, 150f);
            if (_alive) _ctx.OnHall?.Invoke();
        }

        // js: scenes/meta/ending.js:150
        /// <summary>root's pointerdown: while the words are still being written, a press lays the whole sheet down.</summary>
        private void OnPointerDown(PointerEventData e)
        {
            if (_busy || _shown || ButtonOpacity >= 1f) return;
            ShowAll();
        }

        // js: scenes/meta/ending.js:154
        /// <summary>The first Enter / Space / Esc shows everything at once; the next Enter / Space goes on to the hall.</summary>
        private bool OnKey(KeyEvent e)
        {
            if (_busy) return false;
            if (e.Key == "Enter" || e.Key == " " || e.Key == "Escape")
            {
                if (!_shown && ButtonOpacity < 1f)
                {
                    ShowAll();
                    return true;
                }
                if (e.Key != "Escape")
                {
                    ToHall().Forget();
                    return true;
                }
            }
            return false;
        }

        // js: scenes/meta/ending.js:164
        public void Destroy()
        {
            _alive = false;
            _popKeys?.Invoke();
            _live.Stop();
            _clock.Stop();
            _backdrop.Destroy();
            if (_root) Ui.Remove(_root);
        }

        /// <summary>The JS scene has no refresh().</summary>
        public void Refresh() { }
    }
}
