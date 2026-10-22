// The stamina plate and the piles (vendor/ds/bundle.js:429-464, bundle.css:123-142). A pip with .is-lit plays
// `animation: ocb-pip-lit .32s ease-out` (scale 1.35 → 1, a wick ring 0 → 8px fading out) through CssAnimator on the
// document clock; the battle re-runs the same keyframes on its own clock (hud/stamina.js), which CssAnimator also takes.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>StaminaPlate's d: { st, max, pay, next, showPay, lit: [indexes] }.</summary>
    public sealed class OcbStaminaData
    {
        public int St, Max, Pay, Next;
        public bool ShowPay;
        public List<int> Lit = new List<int>();
    }

    /// <summary>A built stamina plate with el.update.</summary>
    public sealed class OcbStaminaPlate : OcbElement
    {
        public OcbElement Row1 { get; internal set; }
        public TextMeshProUGUI Number { get; internal set; }
        public TextMeshProUGUI PayLabel { get; internal set; }
        public OcbElement Pips { get; internal set; }
        /// <summary>The pips of the last update, in order (the divider is not among them).</summary>
        public List<OcbElement> PipEls { get; } = new List<OcbElement>();
        /// <summary>The pip-lit animations started by the last update.</summary>
        public List<CssAnimator> LitAnimations { get; } = new List<CssAnimator>();

        // js: vendor/ds/bundle.js:437
        /// <summary>el.update(d): the number, 「▸ n」 (shown with .is-on), and the pips built again.</summary>
        public void Update(OcbStaminaData d) => Ocb.UpdateStamina(this, d ?? new OcbStaminaData());
    }

    /// <summary>A built pile: its sheets, its face, its :hover.</summary>
    public sealed class OcbPile : OcbElement
    {
        public string Kind { get; internal set; }
        public int Count { get; internal set; }
        /// <summary>The element's title (the browser's tooltip, 「山札 n 枚」); not drawn.</summary>
        public string Title { get; internal set; }
        public OcbElement Face { get; internal set; }
        public PathGraphic FaceIcon { get; internal set; }
        public List<OcbElement> Sheets { get; } = new List<OcbElement>();
        /// <summary>The pointer listener over the pile's box (cursor: pointer; click to open the list).</summary>
        public ClickArea Area { get; internal set; }

        private bool _hover;

        /// <summary>:hover: the face's border in --ink.</summary>
        public bool Hover
        {
            get => _hover;
            set
            {
                _hover = value;
                if (Face != null) Ocb.Restyle(Face.Root, Ocb.PileFaceStyle(Count == 0, _hover));
            }
        }
    }

    public static partial class Ocb
    {
        // js: vendor/ds/bundle.js:432
        /// <summary>
        /// StaminaPlate(d): .ocb-stamina 236×84 (padding 10 12, --card, 1px steel, radius 5). Row 1 (flex, centred, gap 8,
        /// 36 tall): pip 18 amber, the number (700 34/36 mono amber), 「▸ n」 at margin-left auto (700 24/1 mono wick,
        /// opacity 0; .is-on 1). The pips (flex, centred, gap 4, 20 tall, margin-top 8): 16×16 circles — have (amber),
        /// pay (3px amber ring), next (2px dashed amber), empty (2px steel ring) — with a 2×20 divider after the third.
        /// </summary>
        public static OcbStaminaPlate StaminaPlate(RectTransform parent, OcbStaminaData d)
        {
            var el = Make<OcbStaminaPlate>(parent, "ocb-stamina", StaminaCss);
            el.Row1 = Make<OcbElement>(el.Root, "ocb-st-row1", StRow1Css);
            Icon(el.Row1.Root, "pip", 18f, DsTokens.Amber);
            el.Number = Text(el.Row1.Root, "", TextOf(StaminaCss + StRow1Css + StRow1BCss), "b");
            el.PayLabel = Text(el.Row1.Root, "", TextOf(StaminaCss + StRow1Css + StPayCss), "ocb-st-pay");
            AutoMarginLeft(el.PayLabel);
            el.Pips = Make<OcbElement>(el.Root, "ocb-pips", PipsCss);
            el.SetPart("row1", el.Row1.Root);
            el.SetPart("pips", el.Pips.Root);
            el.Update(d ?? new OcbStaminaData());
            return el;
        }

        internal static void UpdateStamina(OcbStaminaPlate el, OcbStaminaData d)
        {
            if (!el.Root) return;
            BoxStyle bs = el.Box.Style;
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = el.W - bw.Horizontal - pad.Horizontal;
            string inv(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

            Label.SetText(el.Number, inv(d.St));
            Label.SetText(el.PayLabel, "▸ " + inv(d.Pay));
            UiNode.Of(el.PayLabel.rectTransform).Opacity = d.ShowPay ? 1f : 0f;   // .ocb-st-pay opacity 0, .is-on 1
            Flow(el.Row1.Root, StRow1Css, null, innerW);
            el.Row1.Place(bw.Left + pad.Left, bw.Top + pad.Top);

            // pips.innerHTML = ''
            foreach (CssAnimator a in el.LitAnimations) a.Stop(false);
            el.LitAnimations.Clear();
            Ui.Clear(el.Pips.Root);
            el.PipEls.Clear();
            int pay0 = d.ShowPay ? d.Pay : 0;
            var lit = new List<OcbElement>();
            for (int i = 0; i < d.Max; i++)
            {
                string cls = i < d.St - pay0 ? PipHaveCss : i < d.St ? PipPayCss : i < d.St + d.Next ? PipNextCss : PipEmptyCss;
                OcbElement pip = Make<OcbElement>(el.Pips.Root, "ocb-pip", PipCss + cls);
                NoShrink(pip.Root);
                el.PipEls.Add(pip);
                if (d.Lit != null && d.Lit.IndexOf(i) >= 0) lit.Add(pip);
                if (i == 2) NoShrink(El(el.Pips.Root, "ocb-pip-div", PipDivCss));
            }
            Flow(el.Pips.Root, PipsCss);
            float marginTop = Px(Rule(PipsCss).Layout.MarginTop) ?? 8f;
            el.Pips.Place(bw.Left + pad.Left, bw.Top + pad.Top + el.Row1.H + marginTop);
            // .ocb-pip.is-lit { animation: ocb-pip-lit .32s ease-out } (the class adds no paint of its own)
            foreach (OcbElement pip in lit) el.LitAnimations.Add(CssAnimator.Play(pip.Root, "ocb-pip-lit .32s ease-out"));
        }

        /// <summary>A pip-lit animation on any element (the battle's stamina tag re-runs it on its own clock).</summary>
        public static CssAnimator PipLit(Component pip, TrialClock clock = null, bool sceneTime = false) =>
            CssAnimator.Play(pip, CssAnimation.Parse("ocb-pip-lit .32s ease-out"), null, clock, sceneTime);

        // js: vendor/ds/bundle.js:456
        /// <summary>
        /// Pile(kind, count): .ocb-pile 64×84: min(3, ceil(count / 3)) sheets behind the face (the farthest first, each 4 px
        /// higher, opacity 1 − .18 i), and the face (--card, 1px steel, radius 5) with the pile's icon 28 (ink; steel when
        /// empty: no ground, dashed border). :hover: the face's border in --ink.
        /// </summary>
        public static OcbPile Pile(RectTransform parent, string kind, int count)
        {
            var el = Make<OcbPile>(parent, "ocb-pile", PileCss);
            el.Kind = kind;
            el.Count = count;
            PileName.TryGetValue(kind ?? "", out string name);
            el.Title = name + " " + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 枚";
            int layers = count != 0 ? Math.Min(3, (int)Math.Ceiling(count / 3.0)) : 0;
            for (int i = layers; i >= 1; i--)
            {
                OcbElement sheet = Make<OcbElement>(el.Root, "ocb-pile-sheet", PileSheetCss);
                // style: { top: -4i px, opacity: 1 − i·.18 }
                sheet.Node.Place(0f, -4f * i);
                sheet.Node.Opacity = 1f - i * 0.18f;
                el.Sheets.Add(sheet);
            }
            el.Face = Make<OcbElement>(el.Root, "ocb-pile-face", PileFaceStyle(count == 0, false));
            el.Face.Node.Place(0f, 0f, el.W, el.H);   // inset 0
            PileIcon.TryGetValue(kind ?? "", out string icon);
            el.FaceIcon = Icon(el.Face.Root, icon, 28f, count != 0 ? DsTokens.Ink : DsTokens.Steel);
            Flow(el.Face.Root, PileFaceStyle(count == 0, false), null, el.W, el.H);
            el.Area = ClickArea.On(el.Root);
            el.Area.HoverChanged += on => el.Hover = on;
            el.SetPart("face", el.Face.Root);
            return el;
        }

        /// <summary>The face's declarations: .ocb-pile-face, the empty pile's, :hover.</summary>
        public static string PileFaceStyle(bool empty, bool hover) =>
            PileFaceCss + (empty ? PileEmptyFaceCss : "") + (hover ? PileHoverFaceCss : "");
    }
}
