// A dev-only page of every kit primitive at fixed stage positions (kit.md §4.14), to be put side by side with a Chrome
// screenshot of the same CSS: the colour-space probe of §4.12, boxes (borders, radii, dashes, dots, doubles, per-side
// borders, outlines, outer and inset shadows), gradients and glows, hatches and stripes, the common pieces (buttons,
// the confirm card, the notice), every icon and figure, the text faces and their calibrations (letter-spacing, line
// height, underlays, glows, decorations, vertical text), and the CSS animations and transitions.
//
// Put it on an empty GameObject in a scene and press Play. Keys: 1–4 or PageUp / PageDown change the page, C opens a
// confirm card, N shows a notice for 4 s, P logs the probe pixels read back from the gamma target.
// Every position is in stage px (top-left origin); the CSS each piece copies is in the code beside it.
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    [AddComponentMenu("Trial/Dev/Kit Gallery")]
    public sealed class KitGallery : MonoBehaviour
    {
        public const int PageCount = 4;

        [Tooltip("The page shown first (0 boxes and colour, 1 paths, 2 text, 3 motion).")]
        public int StartPage;

        [Tooltip("Draw through the gamma target (kit.md §4.12); off = the Linear default, for comparison.")]
        public bool UseGammaTarget = true;

        private TrialStage _stage;
        private RectTransform _layer;
        private readonly List<RectTransform> _pages = new List<RectTransform>();
        private int _page;
        private Action _popKeys;
        private TextMeshProUGUI _title;
        private readonly List<(string Name, Vector2 At, Color32 Browser)> _probes = new List<(string, Vector2, Color32)>();

        private static readonly string[] PageNames = { "boxes, colour, gradients, hatches, common pieces", "paths: icons and figures", "text", "motion" };

        private void Start()
        {
            _stage = TrialStage.Current ? TrialStage.Current : TrialStage.Create("TrialStage (gallery)", UseGammaTarget);
            _layer = _stage.Layer("gallery", 0);
            BoxGraphic.Make(_layer, "background: var(--ground)", 0f, 0f, TrialStage.W, TrialStage.H, "ground");
            _title = Label.Make(_layer, "", TextStyle.FromCss("font: 600 16px/20px var(--font-mono); color: var(--ink-muted)"), 24f, 8f, 1872f, 20f, "page");
            for (int i = 0; i < PageCount; i++)
            {
                RectTransform p = Ui.Div(_layer, "page " + i, 0f, 0f, TrialStage.W, TrialStage.H);
                _pages.Add(p);
            }
            BuildBoxes(_pages[0]);
            BuildPaths(_pages[1]);
            BuildText(_pages[2]);
            BuildMotion(_pages[3]);
            ShowPage(Mathf.Clamp(StartPage, 0, PageCount - 1));
            _popKeys = KeyStack.Push(OnKey);
            CssTime.Clock.WaitReal(1000f).Then(() => { if (this) LogProbes(); });
        }

        private void OnDestroy()
        {
            _popKeys?.Invoke();
            _popKeys = null;
        }

        private bool OnKey(KeyEvent e)
        {
            if (e.Repeat) return false;
            if (e.Key.Length == 1 && e.Key[0] >= '1' && e.Key[0] <= '0' + PageCount) { ShowPage(e.Key[0] - '1'); return true; }
            switch (e.Key)
            {
                case "PageDown": ShowPage((_page + 1) % PageCount); return true;
                case "PageUp": ShowPage((_page + PageCount - 1) % PageCount); return true;
                case "c":
                    Stage.ConfirmCard(_layer, new ConfirmOpts { Title = "新しい系譜を始める", Body = "これまでの生と痕跡の記録は消えます。", Ok = "はじめから", Cancel = "やめる", Danger = true })
                        .Then(ok => Debug.Log("[KitGallery] confirm answered " + ok));
                    return true;
                case "n":
                    Stage.NoticeLine(_layer, "保存できませんでした。\nこの画面を閉じると、ここまでの記録は失われます。", 560f, 104f, 800f, 4000f);
                    return true;
                case "p": LogProbes(); return true;
            }
            return false;
        }

        private void ShowPage(int i)
        {
            _page = i;
            for (int k = 0; k < _pages.Count; k++) _pages[k].gameObject.SetActive(k == i);
            Label.SetText(_title, "KitGallery " + (i + 1) + "/" + PageCount + " — " + PageNames[i] + "   (1–4 / PgUp PgDn: page · C confirm · N notice · P probes)");
        }

        // ---- helpers ------------------------------------------------------------------------------------------

        private static readonly TextStyle Caption = TextStyle.FromCss("font: 400 14px/18px var(--font-mono); color: var(--ink-muted)");

        private static void Note(RectTransform parent, string text, float x, float y, float w = 220f) =>
            Label.Make(parent, text, Caption, x, y, w, null, "note");

        private static BoxGraphic Box(RectTransform parent, string css, float x, float y, float w, float h, string note = null)
        {
            BoxGraphic b = BoxGraphic.Make(parent, css, x, y, w, h);
            if (note != null) Note(parent, note, x, y + h + 6f, Math.Max(w, 180f));
            return b;
        }

        // ---- page 1: colour probe, boxes, gradients, hatches, common pieces -----------------------------------

        private void BuildBoxes(RectTransform pg)
        {
            // §4.12 colour-space probe: browser values by formula (sRGB blending)
            float x = 24f, y = 40f;
            Note(pg, "§4.12 colour probe (browser rgb under each)", x, y, 600f);
            y += 22f;
            Probe(pg, "scrim .62 over #808080", x, y, "background: #808080", "background: rgba(5,6,10,.62)", new Color32(52, 52, 55, 255));
            Probe(pg, "rule border on #141925", x + 140f, y, "background: #141925", "border: 1px solid rgba(157,166,186,.26); background: #141925", new Color32(56, 62, 76, 255), probeInset: 0.5f);
            Probe(pg, "black .45 over #808080", x + 280f, y, "background: #808080", "background: rgba(0,0,0,.45)", new Color32(70, 70, 70, 255));
            Probe(pg, "black .225 on #141925", x + 420f, y, "background: #141925", "background: rgba(0,0,0,.225)", new Color32(16, 19, 29, 255));
            Probe(pg, "wick .5 on #10141d", x + 560f, y, "background: #10141d", "background: rgba(255,216,154,.5)", new Color32(136, 118, 92, 255));
            Probe(pg, "amber opaque", x + 700f, y, "background: #10141d", "background: #e8a84c", new Color32(232, 168, 76, 255));
            {
                // mix-blend-mode: screen, crimson .7 over #808080
                BoxGraphic.Make(pg, "background: #808080", x + 840f, y, 120f, 80f);
                GradientGraphic s = GradientGraphic.Make(pg, x + 860f, y + 20f, 80f, 40f);
                s.SetPaint(new SolidPaint(DsTokens.Crimson.WithAlpha(0.7f)));
                s.ScreenBlend = true;
                Note(pg, "screen crimson .7\n196,153,150", x + 840f, y + 86f, 130f);
                _probes.Add(("screen crimson .7 over #808080", new Vector2(x + 900f, y + 40f), new Color32(196, 153, 150, 255)));
            }
            {
                // the card shadow token over grey
                BoxGraphic.Make(pg, "background: #808080", x + 980f, y, 140f, 80f);
                BoxGraphic.Make(pg, "background: #141925; box-shadow: 0 8px 18px rgba(0,0,0,.45)", x + 1000f, y + 10f, 100f, 40f);
                Note(pg, "shadow-tip on grey", x + 980f, y + 86f, 140f);
            }
            {
                // a TMP glyph in a translucent colour
                BoxGraphic.Make(pg, "background: #10141d", x + 1140f, y, 120f, 80f);
                Label.Make(pg, "■", TextStyle.FromCss("font: 700 64px/80px var(--font-body); color: rgba(255,216,154,.5)"), x + 1140f, y, 120f, 80f)
                    .horizontalAlignment = HorizontalAlignmentOptions.Center;
                Note(pg, "TMP wick .5\n136,118,92", x + 1140f, y + 86f, 130f);
            }

            // borders and radii
            y = 220f;
            Note(pg, "borders (kit.md §3.2 measured patterns)", x, y - 22f, 600f);
            Box(pg, "background: var(--panel); border: 1px solid var(--rule); border-radius: 6px", x, y, 160f, 60f, ".ocb-panel");
            Box(pg, "background: var(--panel-solid); border: 1px solid var(--steel); border-radius: 6px", x + 180f, y, 160f, 60f, ".ocb-solid");
            Box(pg, "border: 1px dashed #fff", x + 360f, y, 160f, 60f, "1px dashed");
            Box(pg, "border: 2px dashed #fff", x + 540f, y, 160f, 60f, "2px dashed");
            Box(pg, "border: 2px dashed #fff; border-radius: 6px", x + 720f, y, 160f, 60f, "2px dashed r6");
            Box(pg, "border: 3px double var(--boss)", x + 900f, y, 160f, 60f, "3px double");
            Box(pg, "border: 1px dotted #fff", x + 1080f, y, 160f, 60f, "1px dotted");
            Box(pg, "border: 2px dashed var(--amber); border-radius: 50%", x + 1260f, y + 22f, 16f, 16f, "16px circle dashed");
            Box(pg, "background: var(--panel-solid); border: 1px solid var(--rule); border-left: 3px solid var(--caution); border-radius: 6px", x + 1420f, y, 200f, 60f, "per-side (notice)");
            Box(pg, "background: var(--panel-solid); border: 1px solid var(--steel); border-radius: 6px; outline: 2px solid var(--wick); outline-offset: 2px", x + 1660f, y, 160f, 60f, "outline 2px off 2");

            // shadows
            y = 340f;
            Note(pg, "box-shadow (σ = blur/2), inset shadows", x, y - 22f, 600f);
            Box(pg, "background: var(--card); border-radius: 6px; box-shadow: var(--shadow-card)", x, y, 120f, 80f, "shadow-card");
            Box(pg, "background: var(--card); border-radius: 6px; box-shadow: var(--shadow-card), var(--shadow-lamp)", x + 160f, y, 120f, 80f, "card + lamp");
            Box(pg, "background: var(--panel-solid); border-radius: 6px; box-shadow: var(--shadow-tip)", x + 320f, y, 120f, 80f, "shadow-tip");
            Box(pg, "border: 2px solid var(--reach-red); border-radius: 6px; box-shadow: var(--shadow-threat)", x + 480f, y, 120f, 40f, "shadow-threat");
            Box(pg, "background: linear-gradient(rgba(232,168,76,.55), rgba(232,168,76,.18)); border-radius: 6px; box-shadow: var(--shadow-reach)", x + 640f, y, 120f, 40f, "tile reach");
            Box(pg, "background: var(--panel-solid); border: 2px solid var(--phosphor); border-radius: 6px; box-shadow: var(--shadow-omen-active)", x + 800f, y, 120f, 80f, "omen active");
            Box(pg, "background: var(--panel-solid); border-radius: 4px; box-shadow: inset 3px 0 0 var(--amber)", x + 960f, y, 160f, 40f, "inset 3px 0 0");
            Box(pg, "background: var(--washi); border-radius: 4px; box-shadow: inset 0 0 40px rgba(0,0,0,.35)", x + 1160f, y, 160f, 80f, "inset 0 0 40px");
            Box(pg, "background: var(--panel-solid); border-radius: 6px; box-shadow: 0 0 0 2px var(--panel-solid), inset 0 0 0 2px var(--amber)", x + 1360f, y, 120f, 40f, "spread rings");
            Box(pg, "background: var(--amber); border-radius: 50%; box-shadow: 0 0 0 8px rgba(255,216,154,.45)", x + 1540f, y + 12f, 16f, 16f, "pip + ring");

            // gradients and glows
            y = 480f;
            Note(pg, "gradients, glows, vignettes", x, y - 22f, 600f);
            GradientGraphic.Css(pg, "linear-gradient(90deg, rgba(10,12,18,0), rgba(10,12,18,.8) 28%, rgba(10,12,18,.8) 72%, rgba(10,12,18,0))", x, y, 400f, 80f);
            Box(pg, "background: var(--amber)", x, y + 30f, 400f, 2f);
            Note(pg, "banner 90deg (over an amber line)", x, y + 86f, 400f);
            GradientGraphic.Css(pg, "radial-gradient(closest-side, rgba(232,168,76,.55), rgba(0,0,0,0))", x + 430f, y, 200f, 60f, ellipse: true);
            Note(pg, "foot light self", x + 430f, y + 66f);
            GradientGraphic.Css(pg, "radial-gradient(closest-side, rgba(139,111,208,.35), rgba(0,0,0,0))", x + 650f, y, 200f, 60f, ellipse: true);
            Note(pg, "foot light foe", x + 650f, y + 66f);
            GradientGraphic.Glow(pg, x + 870f, y - 10f, 120f, 120f, DsTokens.Amber, 0.6f);
            Note(pg, "glowGradient amber .6", x + 870f, y + 116f);
            BoxGraphic.Make(pg, "background: #808080", x + 1010f, y, 220f, 100f);
            GradientGraphic.Css(pg, "radial-gradient(ellipse 72% 66% at 50% 50%, rgba(0,0,0,0) 60%, rgba(0,0,0,.55))", x + 1010f, y, 220f, 100f);
            Note(pg, "vignette 72% 66%", x + 1010f, y + 106f);
            GradientGraphic.Css(pg, "linear-gradient(135deg, var(--violet), var(--amber))", x + 1250f, y, 100f, 100f).Shape = GradientGraphic.ShapeKind.RoundRect;
            Note(pg, "135deg", x + 1250f, y + 106f, 100f);

            // hatches and stripes
            y = 650f;
            Note(pg, "hatch (bars 3/gap 11, 6 on a boundary), swatches, stripes", x, y - 22f, 800f);
            float[] angles = { 45f, -45f, 90f };
            for (int i = 0; i < 3; i++)
            {
                float tx = x + i * 150f;
                BoxGraphic.Make(pg, "background: var(--floor-tile); border: 1px solid var(--rule); border-radius: 6px", tx, y, 130f, 40f);
                BoxGraphic.Make(pg, "border: 2px solid var(--reach-red); border-radius: 6px; box-shadow: var(--shadow-threat)", tx - 1f, y - 1f, 132f, 42f);
                HatchGraphic h = HatchGraphic.Bars(pg, tx + 1f, y + 1f, 128f, 38f, angles[i], i == 2 ? 6f : 11f);
                h.ClipRadius = 4f;
                Note(pg, angles[i] + "° gap " + (i == 2 ? 6 : 11), tx, y + 46f, 130f);
            }
            {
                // swatch(angle, 22): 2px reach-red border, radius 3, hatch gap 6 width 2 clipped by the padding box
                float sx = x + 470f;
                BoxGraphic.Make(pg, "border: 2px solid var(--reach-red); border-radius: 3px", sx, y + 9f, 22f, 22f);
                HatchGraphic s = HatchGraphic.Bars(pg, sx + 2f, y + 11f, 18f, 18f, 45f, 6f, null, 2f);
                s.ClipRadius = 1f;
                BoxGraphic.Make(pg, "border: 2px solid var(--reach-red); border-radius: 3px", sx + 40f, y + 12f, 16f, 16f);
                HatchGraphic s2 = HatchGraphic.Bars(pg, sx + 42f, y + 14f, 12f, 12f, -45f, 6f, null, 2f);
                s2.ClipRadius = 1f;
                Note(pg, "swatch 22 / 16", sx, y + 46f, 120f);
            }
            {
                float sx = x + 620f;
                BoxGraphic.Make(pg, "background: rgba(0,0,0,.5); border: 1px solid var(--rule); border-radius: 3px", sx, y + 12f, 160f, 14f);
                HatchGraphic st = HatchGraphic.Stripes(pg, sx + 1f, y + 13f, 158f, 12f, 135f, 0f, 2f, 5f, DsTokens.Wick, 2f);
                Note(pg, "HP loss 135° wick 0 2 / 5", sx, y + 46f, 180f);
                HatchGraphic tl = HatchGraphic.Stripes(pg, sx + 200f, y + 18f, 300f, 3f, 90f, 0f, 9f, 15f, DsTokens.Amber);
                Note(pg, "throw line 90° 9 on 6 off", sx + 200f, y + 46f, 200f);
                st.name = "hp loss";
                tl.name = "throw line";
            }

            // the common pieces
            y = 800f;
            Note(pg, "common.css: .ocbg-btn states, the notice (N: a timed one, C: the confirm card)", x, y - 22f, 900f);
            OcbgButton b1 = OcbgButton.Make(pg, "やめる");
            b1.Place(x, y);
            OcbgButton b2 = OcbgButton.Make(pg, "進む", primary: true);
            b2.Place(x + 160f, y);
            OcbgButton b3 = OcbgButton.Make(pg, "はじめから", primary: true, danger: true);
            b3.Place(x + 300f, y);
            OcbgButton b4 = OcbgButton.Make(pg, "使えない");
            b4.Disabled = true;
            b4.Place(x + 500f, y);
            OcbgButton b5 = OcbgButton.Make(pg, "フォーカス");
            b5.SetFocusRing(true);
            b5.Place(x + 680f, y);
            Note(pg, "plain · primary · danger · [disabled] · :focus-visible (hover them)", x, y + 66f, 800f);
            Stage.NoticeLine(pg, "このブラウザでは記録を保存できません。\n閉じると、ここまでの生は失われます。", 900f, y, 560f);
        }

        private void Probe(RectTransform pg, string name, float x, float y, string under, string over, Color32 browser, float probeInset = 40f)
        {
            BoxGraphic.Make(pg, under, x, y, 120f, 80f);
            BoxGraphic.Make(pg, over, x + 20f, y + 20f, 80f, 40f);
            Note(pg, name + "\n" + browser.r + "," + browser.g + "," + browser.b, x, y + 86f, 130f);
            // the sample point: the middle of the overlay, or (for a border) its first pixel
            Vector2 at = probeInset < 1f ? new Vector2(x + 20f + 40f, y + 20f + probeInset) : new Vector2(x + 60f, y + 40f);
            _probes.Add((name, at, browser));
        }

        /// <summary>Reads the probe pixels back from the gamma target and logs them next to the browser's values.</summary>
        public void LogProbes()
        {
            GammaTarget g = _stage ? _stage.Gamma : null;
            if (!g || !g.Target)
            {
                Debug.Log("[KitGallery] probes: no gamma target (UseGammaTarget off): compare a screenshot by eye.");
                return;
            }
            RenderTexture rt = g.Target;
            RenderTexture was = RenderTexture.active;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var sb = new System.Text.StringBuilder("[KitGallery] colour probes (gamma target bytes vs the browser):\n");
            try
            {
                RenderTexture.active = rt;
                foreach (var p in _probes)
                {
                    Vector2 s = _stage.ToScreen(p.At);
                    int px = Mathf.Clamp(Mathf.FloorToInt(s.x), 0, rt.width - 1), py = Mathf.Clamp(Mathf.FloorToInt(s.y), 0, rt.height - 1);
                    tex.ReadPixels(new Rect(px, py, 1, 1), 0, 0, false);
                    tex.Apply(false);
                    Color32 c = tex.GetPixel(0, 0);
                    int d = Math.Max(Math.Abs(c.r - p.Browser.r), Math.Max(Math.Abs(c.g - p.Browser.g), Math.Abs(c.b - p.Browser.b)));
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "  {0,-34} got {1,3},{2,3},{3,3}  browser {4,3},{5,3},{6,3}  Δ{7}{8}\n",
                        p.Name, c.r, c.g, c.b, p.Browser.r, p.Browser.g, p.Browser.b, d, d <= 2 ? "" : "  <-- off"));
                }
            }
            finally
            {
                RenderTexture.active = was;
                Destroy(tex);
            }
            Debug.Log(sb.ToString());
        }

        // ---- page 2: paths ------------------------------------------------------------------------------------

        private void BuildPaths(RectTransform pg)
        {
            float x0 = 24f, y = 40f;
            Note(pg, "DS icons (bundle.js:64-101) at 24 px ink and 48 px amber; stroke 2.2 viewBox units, round caps", x0, y, 1200f);
            y += 24f;
            int i = 0;
            foreach (string name in DsIcons.Names)
            {
                float x = x0 + (i % 19) * 98f, yy = y + (i / 19) * 110f;
                RectTransform cell = Ui.Div(pg, name, x, yy, 90f, 104f);
                PathGraphic a = PathGraphic.Icon(cell, name, 24f, DsTokens.Ink);
                Ui.Place(a.rectTransform, 0f, 0f);
                PathGraphic b = PathGraphic.Icon(cell, name, 48f, DsTokens.Amber);
                Ui.Place(b.rectTransform, 30f, 0f);
                Note(cell, name, 0f, 52f, 90f);
                i++;
            }
            y += ((DsIcons.Names.Count + 18) / 19) * 110f + 10f;
            Note(pg, "explore icons (icons.js:8-41) 32 px; extra icons; dashed, filled, fill .2", x0, y, 1200f);
            y += 24f;
            i = 0;
            foreach (string name in ExploreIcons.Names)
            {
                float x = x0 + i * 66f;
                RectTransform cell = Ui.Div(pg, name, x, y, 60f, 70f);
                PathGraphic a = PathGraphic.Icon(cell, ExploreIcons.Get(name), 32f, DsTokens.Ink);
                Ui.Place(a.rectTransform, 0f, 0f);
                Label.Make(cell, name, TextStyle.FromCss("font: 400 10px/12px var(--font-mono); color: var(--ink-muted)"), 0f, 38f, 60f, 24f);
                i++;
            }
            y += 80f;
            float ex = x0;
            foreach (string name in ExtraIcons.Names)
            {
                IconDef d = ExtraIcons.Get(name);
                bool tall = d.ViewBox.H > 24f;
                PathGraphic g = PathGraphic.Icon(pg, d, tall ? 72f : 36f, name == "shield" ? DsTokens.Guard.WithAlpha(0.2f) : DsTokens.Ink, null, name == "shield-crack" ? 2f : (float?)null);
                Ui.Place(g.rectTransform, ex, y);
                Note(pg, name, ex, y + (tall ? 76f : 40f), 90f);
                ex += tall ? 100f : 70f;
            }
            {
                PathGraphic d1 = PathGraphic.Icon(pg, "shield", 36f, DsTokens.Steel, null, null, dashed: true);
                Ui.Place(d1.rectTransform, ex + 20f, y);
                Note(pg, "dashed", ex + 20f, y + 40f, 80f);
                PathGraphic d2 = PathGraphic.Icon(pg, "shield", 36f, DsTokens.Guard, DsTokens.Guard.WithAlpha(0.2f));
                Ui.Place(d2.rectTransform, ex + 100f, y);
                Note(pg, "fill .2", ex + 100f, y + 40f, 80f);
                PathGraphic d3 = PathGraphic.Icon(pg, "heart", 36f, DsTokens.HpText);
                Ui.Place(d3.rectTransform, ex + 180f, y);
                Note(pg, "filled", ex + 180f, y + 40f, 80f);
            }

            // figures, the target frame, a gradient-filled figure, a flowing dashed edge
            y = 720f;
            Note(pg, "DS figures (fill + 4-unit rim), 226 px tall", x0, y - 22f, 600f);
            float fx = x0;
            foreach (FigureDef f in DsFigures.Figures)
            {
                PathGraphic g = PathGraphic.Figure(pg, f.Name, 226f);
                Ui.Place(g.rectTransform, fx, y);
                Note(pg, f.Name, fx, y + 230f, 120f);
                fx += g.rectTransform.sizeDelta.x + 30f;
            }
            {
                // TargetFrame(w, h, { hook 26, width 3, glow 10, glowAlpha .35 }): four corner hooks, square caps, drop-shadow
                float w = 160f, h = 200f, L = 26f;
                PathGraphic t = PathGraphic.Make(pg, fx + 20f, y + 10f, w, h, "target frame");
                string d = string.Format(CultureInfo.InvariantCulture, "M0 {0} V0 H{0} M{1} 0 H{2} V{0} M{2} {3} V{4} H{1} M{0} {4} H0 V{3}", L, w - L, w, h - L, h);
                t.Add(new PathLayer(d).WithStroke(DsTokens.Wick, 3f, Cap.Square).WithShadow(0f, 0f, 10f, Rgba.Bytes(232, 168, 76, 0.35f)));
                Note(pg, "TargetFrame + drop-shadow 10", fx + 20f, y + 216f, 200f);
                fx += 220f;
            }
            {
                // the stance body light: a figure filled with an SVG radial gradient (userSpaceOnUse) and screen-blended
                FigureDef f = DsFigures.Get("swordsman");
                PathGraphic g = PathGraphic.Make(pg, fx + 20f, y, f.W, f.H, "body light");
                g.ViewBox = f.ViewBox;
                var layer = new PathLayer(new List<string>(f.Paths).ToArray());
                layer.Fill = RadialGradient.Points(new Vec2(f.Cx, f.H * (1f - 0.6f)), 0f, f.H * 0.46f,
                    GradientStop.At(DsTokens.Crimson.WithAlpha(1f), 0f), GradientStop.At(DsTokens.Crimson.WithAlpha(0.7f), 0.35f),
                    GradientStop.At(DsTokens.Crimson.WithAlpha(0.22f), 0.7f), GradientStop.At(DsTokens.Crimson.WithAlpha(0f), 1f));
                g.Add(layer);
                g.ScreenBlend = true;
                Note(pg, "radial fill, screen", fx + 20f, y + 230f, 200f);
            }
        }

        // ---- page 3: text --------------------------------------------------------------------------------------

        private void BuildText(RectTransform pg)
        {
            float x = 24f, y = 40f;
            const string sample = "継承の間 第1層 HP 50/50 ×1.25 あいう、。";
            (string Css, string Name)[] faces =
            {
                ("font: 400 20px/28px var(--font-display)", "display 400 20/28"),
                ("font: 400 34px/48px var(--font-display)", "display 400 34/48"),
                ("font: 700 22px/28px var(--font-display); letter-spacing: .02em", "display 700 22/28 .02em"),
                ("font: 700 30px/40px var(--font-display)", "display 700 30/40"),
                ("font: 800 48px/56px var(--font-display); letter-spacing: .12em", "display 800 48/56 .12em"),
                ("font: 800 64px/72px var(--font-display); letter-spacing: .12em", "display 800 64/72 .12em"),
                ("font: 400 16px/22px var(--font-body)", "body 400 16/22"),
                ("font: 400 20px/32px var(--font-body)", "body 400 20/32"),
                ("font: 700 22px/1 var(--font-body); letter-spacing: .06em", "body 700 22/1 .06em"),
                ("font: 400 16px/20px var(--font-mono)", "mono 400 16/20"),
                ("font: 600 20px/1 var(--font-mono)", "mono 600 20/1"),
                ("font: 700 48px/52px var(--font-mono)", "mono 700 48/52"),
            };
            foreach (var (css, name) in faces)
            {
                TextStyle s = TextStyle.FromCss(css + "; color: var(--ink); white-space: nowrap");
                float lh = s.LineHeightPx;
                // the line box, so the baseline placement can be compared with Chrome's
                BoxGraphic.Make(pg, "background: rgba(109,119,135,.18)", x + 260f, y, 1100f, lh);
                Note(pg, name, x, y + Math.Max(0f, (lh - 18f) * 0.5f), 250f);
                Label.Make(pg, sample, s, x + 260f, y, null, null, name);
                y += lh + 10f;
            }

            // calibrations
            float cx = 1420f, cy = 40f;
            Note(pg, "letter-spacing: あいう at 0 / .1em (20 px): widths differ by 6 px in Chrome", cx, cy, 480f);
            cy += 40f;
            TextMeshProUGUI a0 = Label.Make(pg, "あいう", TextStyle.FromCss("font: 400 20px/28px var(--font-body); color: var(--ink)"), cx, cy);
            TextMeshProUGUI a1 = Label.Make(pg, "あいう", TextStyle.FromCss("font: 400 20px/28px var(--font-body); letter-spacing: .1em; color: var(--ink)"), cx, cy + 32f);
            BoxGraphic.Make(pg, "background: rgba(232,168,76,.25)", cx, cy, a0.rectTransform.sizeDelta.x, 28f);
            BoxGraphic.Make(pg, "background: rgba(232,168,76,.25)", cx, cy + 32f, a1.rectTransform.sizeDelta.x, 28f);
            Note(pg, "measured: " + a0.rectTransform.sizeDelta.x.ToString("0.##", CultureInfo.InvariantCulture) + " / " + a1.rectTransform.sizeDelta.x.ToString("0.##", CultureInfo.InvariantCulture), cx + 120f, cy + 8f, 300f);

            cy += 90f;
            Note(pg, "text-shadow: underlay (≤ 3 px) and TextGlow (> 3 px)", cx, cy, 480f);
            cy += 24f;
            BoxGraphic.Make(pg, "background: var(--washi)", cx, cy, 460f, 220f);
            Label.Make(pg, "0 1px 3px 地の影", TextStyle.FromCss("font: 400 22px/28px var(--font-body); color: var(--ink); text-shadow: 0 1px 3px rgba(0,0,0,.7)"), cx + 16f, cy + 12f);
            TextMeshProUGUI halo = Label.Make(pg, "0 0 8px 地の暈", TextStyle.FromCss("font: 400 22px/28px var(--font-body); color: var(--ink); text-shadow: 0 1px 3px var(--ground), 0 0 8px var(--ground)"), cx + 16f, cy + 60f);
            TextGlow.For(halo);
            TextMeshProUGUI glow = Label.Make(pg, "0 0 22px 灯の光", TextStyle.FromCss("font: 700 30px/40px var(--font-display); color: var(--wick); text-shadow: 0 0 22px rgba(255,216,154,.6)"), cx + 16f, cy + 108f);
            TextGlow.For(glow);
            TextMeshProUGUI ink = Label.Make(pg, "0 0 12px ink 70%", TextStyle.FromCss("font: 600 20px/24px var(--font-mono); color: var(--washi-ink); text-shadow: 0 0 12px color-mix(in srgb, var(--ink) 70%, transparent)"), cx + 16f, cy + 168f);
            TextGlow.For(ink);

            cy += 250f;
            Note(pg, "text-decoration: dotted underline 2px offset 4; line-through 2 / 3 px", cx, cy, 480f);
            cy += 28f;
            TextMeshProUGUI term = Label.Make(pg, "鈍足（にぶあし）", TextStyle.FromCss("font: 400 16px/22px var(--font-body); color: var(--ink)"), cx, cy);
            TextDecor.Underline(term, dotted: true, color: DsTokens.InkMuted);
            TextMeshProUGUI miss = Label.Make(pg, "ミス", TextStyle.FromCss("font: 400 24px/32px var(--font-body); color: var(--ink)"), cx + 200f, cy - 6f);
            TextDecor.LineThrough(miss, thickness: 2f, color: DsTokens.Miss);
            TextMeshProUGUI miss3 = Label.Make(pg, "ミス", TextStyle.FromCss("font: 400 24px/32px var(--font-body); color: var(--ink)"), cx + 280f, cy - 6f);
            TextDecor.LineThrough(miss3, thickness: 3f, color: DsTokens.Miss);

            cy += 60f;
            Note(pg, "vertical-rl (title tagline: display 400 46px, .22em, columns 40 apart)", cx, cy, 480f);
            cy += 24f;
            VerticalText vt = VerticalText.Make(pg, new[] { "生きて、", "継ぐ。" },
                TextStyle.FromCss("font: 400 46px/1.22 var(--font-display); letter-spacing: .22em; color: var(--ink)"), 200f, 40f, new[] { 0f, 96f });
            Ui.Place(vt.Root, cx + 120f, cy);

            cy += 0f;
            Note(pg, "ellipsis / clamp", x, 1000f, 200f);
            Label.Make(pg, "とても長い名前のカードは末尾が省略されて表示される", TextStyle.FromCss("font: 700 22px/28px var(--font-display); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; color: var(--ink)"), x + 220f, 1000f, 188f, 28f);
            Label.Make(pg, "二行で切れる説明文。長い文章は二行目の終わりで省略記号になり、それ以上は出ない。", TextStyle.FromCss("font: 400 16px/22px var(--font-body); -webkit-line-clamp: 2; color: var(--ink)"), x + 440f, 990f, 260f, 44f);
        }

        // ---- page 4: motion ------------------------------------------------------------------------------------

        private void BuildMotion(RectTransform pg)
        {
            float x = 60f, y = 80f;
            Note(pg, "CSS @keyframes on the document clock (CssTime): explore and meta animations", 24f, 40f, 1200f);

            // ocbg-explore-beckon on a node face (box-shadow: card, 0 0 18px 2px amber 38%)
            BoxGraphic face = BoxGraphic.Make(pg, "background: var(--card); border: 2px solid var(--amber); border-radius: 6px; box-shadow: var(--shadow-card)", x, y, 88f, 88f, "face");
            CssAnimator.Play(face, "ocbg-explore-beckon 2.4s ease-in-out infinite");
            PathGraphic sw = PathGraphic.Icon(face.rectTransform, "sword", 48f, DsTokens.Ink);
            Ui.Place(sw.rectTransform, 20f, 20f);
            Note(pg, "beckon 2.4s infinite", x, y + 100f, 160f);

            // ocbg-explore-ripple on the face's ::after (inset −3, 2px amber, radius 6), repeated by the gallery
            BoxGraphic face2 = BoxGraphic.Make(pg, "background: var(--card); border: 2px solid var(--steel); border-radius: 6px", x + 200f, y, 88f, 88f, "face2");
            BoxGraphic ripple = BoxGraphic.Make(pg, "border: 2px solid var(--amber); border-radius: 6px", x + 197f, y - 3f, 94f, 94f, "ripple");
            Loop(ripple, "ocbg-explore-ripple 820ms cubic-bezier(.22,.61,.36,1)", 1400f);
            Note(pg, "ripple 820ms (every 1.4 s)", x + 200f, y + 100f, 180f);
            _ = face2;

            // ocbg-explore-breathe: the walker glow; ocbg-explore-land: the walker disc
            GradientGraphic wg = GradientGraphic.Glow(pg, x + 400f, y, 88f, 88f, DsTokens.Wick, 0.8f);
            CssAnimator.Play(wg, "ocbg-explore-breathe 2.6s ease-in-out infinite");
            BoxGraphic disc = BoxGraphic.Make(pg, "background: var(--wick); border-radius: 50%", x + 432f, y + 32f, 24f, 24f, "disc");
            Loop(disc, "ocbg-explore-land 320ms cubic-bezier(.22,.61,.36,1)", 1200f);
            Note(pg, "breathe 2.6s / land 320ms", x + 400f, y + 100f, 180f);

            // ocbg-explore-pulse
            BoxGraphic warn = BoxGraphic.Make(pg, "background: var(--caution); border-radius: 3px", x + 600f, y + 30f, 120f, 24f, "warn");
            CssAnimator.Play(warn, "ocbg-explore-pulse 1.6s ease-in-out infinite");
            Note(pg, "pulse 1.6s", x + 600f, y + 100f, 160f);

            // ocbg-explore-flow on a lit edge (dash 2 22, width 4, round caps)
            PathGraphic edge = PathGraphic.Make(pg, x + 780f, y, 360f, 88f, "edge");
            edge.Add(new PathLayer("M8 80 L352 8").WithStroke(DsTokens.Amber, 4f, Cap.Round, Join.Round, new[] { 2f, 22f }));
            CssAnimator.Play(edge, "ocbg-explore-flow 1.2s linear infinite");
            Note(pg, "flow 1.2s (stroke-dashoffset 24 → 0)", x + 780f, y + 100f, 300f);

            // ocbg-explore-window (alternate)
            RectTransform wHost = Ui.Div(pg, "window host", x + 1200f, y - 10f, 160f, 110f);
            UnityEngine.UI.RectMask2D unused = wHost.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            GradientGraphic win = GradientGraphic.Css(wHost, "linear-gradient(180deg, #1d3340, #13212b)", 0f, 0f, 160f, 110f);
            CssAnimator.Play(win, "ocbg-explore-window 9s ease-in-out infinite alternate");
            Note(pg, "window 9s alternate", x + 1200f, y + 106f, 200f);
            _ = unused;

            // ocbg-meta-stamp / pulse (box-shadow spread rings)
            BoxGraphic seal = BoxGraphic.Make(pg, "background: var(--crimson); border-radius: 6px", x + 1440f, y + 14f, 60f, 60f, "seal");
            Loop(seal, "ocbg-meta-stamp .7s ease-out", 1500f);
            Note(pg, "meta stamp .7s", x + 1420f, y + 100f, 160f);

            // ocbg-explore-roll on a label (text-shadow 0 → 12px ink 70% → 0)
            y = 300f;
            TextMeshProUGUI rolled = Label.Make(pg, "刻限 9 / 10", TextStyle.FromCss("font: 700 28px/36px var(--font-mono); color: var(--ink)"), x, y);
            Loop(rolled, "ocbg-explore-roll 900ms ease-out", 1600f);
            Note(pg, "roll 900ms (TextGlow)", x, y + 44f, 200f);

            // plate in / out, deal
            BoxGraphic plate = BoxGraphic.Make(pg, "background: var(--panel-solid); border: 1px solid var(--steel); border-radius: 6px; box-shadow: var(--shadow-tip)", x + 300f, y, 240f, 120f, "plate");
            LoopPair(plate, "ocbg-explore-plate-in 300ms cubic-bezier(.22,.61,.36,1) both", "ocbg-explore-plate-out 220ms ease-in both", 1600f);
            Note(pg, "plate-in 300 / plate-out 220", x + 300f, y + 130f, 240f);
            for (int i = 0; i < 3; i++)
            {
                BoxGraphic card = BoxGraphic.Make(pg, "background: var(--card); border-radius: 6px; box-shadow: var(--shadow-card)", x + 600f + i * 130f, y, 108f, 152f, "deal " + i);
                Loop(card, "ocbg-explore-deal 460ms cubic-bezier(.22,.61,.36,1) " + (120 + i * 110) + "ms both", 2200f);
            }
            Note(pg, "deal 460ms, delay 120 + i·110", x + 600f, y + 160f, 300f);

            // transitions: hover the buttons (.ocbg-btn has none: instant) and the transition demo (border-color .14s, background .14s; transform 90ms)
            y = 560f;
            Note(pg, "CSS transitions: hover the boxes (border-color .14s, background .14s / transform 90ms / opacity .6s)", 24f, y - 30f, 1200f);
            BoxGraphic t1 = BoxGraphic.Make(pg, "", x, y, 200f, 64f, "t1");
            ClickArea a1 = ClickArea.On(t1);
            CssStates s1 = CssStates.Of(t1);
            s1.Base("background: var(--panel-solid); border: 1px solid var(--steel); border-radius: 6px; transition: border-color .14s, background .14s")
              .State(":hover", "border-color: var(--ink); background: rgba(40,44,58,.92)");
            s1.Apply(instant: true);
            a1.HoverChanged += on => s1.SetState(":hover", on);

            BoxGraphic t2 = BoxGraphic.Make(pg, "", x + 260f, y, 200f, 64f, "t2");
            ClickArea a2 = ClickArea.On(t2);
            CssStates s2 = CssStates.Of(t2);
            s2.Base("background: var(--card); border: 2px solid var(--amber); border-radius: 6px; transition: transform 90ms cubic-bezier(.4,0,.2,1), box-shadow .2s")
              .State(":hover", "transform: translateY(-6px) scale(1.04); box-shadow: 0 0 18px 2px rgba(232,168,76,.38)");
            s2.Apply(instant: true);
            a2.HoverChanged += on => s2.SetState(":hover", on);

            BoxGraphic t3 = BoxGraphic.Make(pg, "", x + 520f, y, 200f, 64f, "t3");
            ClickArea a3 = ClickArea.On(t3);
            CssStates s3 = CssStates.Of(t3);
            s3.Base("background: var(--violet); border-radius: 6px; transition: opacity .6s").State(":hover", "opacity: .2");
            s3.Apply(instant: true);
            a3.HoverChanged += on => s3.SetState(":hover", on);

            TextMeshProUGUI menu = Label.Make(pg, "はじめから", TextStyle.FromCss("font: 700 32px/40px var(--font-display); color: var(--ink-muted)"), x + 800f, y + 10f);
            ClickArea am = ClickArea.On(menu);
            CssStates sm = CssStates.Of(menu);
            sm.Base("color: var(--ink-muted); letter-spacing: .1em; transition: letter-spacing .2s, text-shadow .2s, color .2s")
              .State(":hover", "color: var(--ink); letter-spacing: .2em; text-shadow: 0 0 18px rgba(255,216,154,.55)");
            sm.Apply(instant: true);
            am.HoverChanged += on => sm.SetState(":hover", on);
            Note(pg, "bg .14s · lift 90ms · opacity .6s · menu item: letter-spacing .2s, text-shadow .2s", x, y + 80f, 1000f);
        }

        /// <summary>Plays a finite animation again every `everyMs` (the gallery's own loop: CSS restarts it when the class comes back).</summary>
        private static void Loop(Component el, string anim, float everyMs)
        {
            CssAnimator a = CssAnimator.Play(el, anim);
            TrialClock c = CssTime.Clock;
            void Again() => c.WaitReal(everyMs).Then(() => { if (el) { a.Restart(); Again(); } });
            Again();
        }

        private static void LoopPair(Component el, string animIn, string animOut, float everyMs)
        {
            TrialClock c = CssTime.Clock;
            bool shown = false;
            void Next()
            {
                if (!el) return;
                shown = !shown;
                CssAnimator.Set(el, shown ? animIn : animOut);
                c.WaitReal(everyMs).Then(Next);
            }
            Next();
        }
    }
}
