// The omen plate and the reserve omen (vendor/ds/bundle.js:300-338, bundle.css:54-68). The plate is the one DS piece
// with a CSS transition (`transition: box-shadow .12s`, the .is-active glow); it runs on the document clock through
// CssStates. The battle switches it off (battle.css:49-52): States.SetTransitions("none").
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>OmenPlate's d (bundle.js:302): { kind, icon, value, angle, hit: true | false | null, statusIcons, active }.</summary>
    public sealed class OcbOmenData
    {
        /// <summary>攻撃 / 防御 / 移動 / 構え / 技 / 状態 / 休み.</summary>
        public string Kind = "技";
        /// <summary>d.icon: an OMEN_KIND key used in place of the kind for the icon and colour (null = the kind's).</summary>
        public string Icon;
        /// <summary>String(d.value), or null for none.</summary>
        public string Value;
        /// <summary>The swatch's hatch angle, or null for none.</summary>
        public float? Angle;
        /// <summary>The hit mark: true hit, false miss, null none.</summary>
        public bool? Hit;
        public List<string> StatusIcons = new List<string>();
        public bool Active;
    }

    /// <summary>A built omen plate (200×98) with el.update.</summary>
    public sealed class OcbOmenPlate : OcbElement
    {
        /// <summary>The plate's classes (.is-active, .is-rest) and its box-shadow transition.</summary>
        public CssStates States { get; internal set; }
        public OcbElement Row1 { get; internal set; }
        public OcbElement Row2 { get; internal set; }
        public PathGraphic KindIcon { get; internal set; }
        /// <summary>The value (null without one).</summary>
        public TextMeshProUGUI ValueLabel { get; internal set; }
        public TextMeshProUGUI KindLabel { get; internal set; }
        /// <summary>The hit mark (null without one).</summary>
        public OcbElement HitMark { get; internal set; }
        /// <summary>The swatch (null without one).</summary>
        public OcbElement Swatch { get; internal set; }

        // js: vendor/ds/bundle.js:315
        /// <summary>el.update(d): the rows built again, .is-rest and .is-active set (the glow transitions .12s).</summary>
        public void Update(OcbOmenData d) => Ocb.UpdateOmen(this, d ?? new OcbOmenData());
    }

    public static partial class Ocb
    {
        // js: vendor/ds/bundle.js:313
        /// <summary>
        /// OmenPlate(d): .ocb-omen 200×98 (panel-solid, 2px violet, radius 6, padding 8 12, column, space-between).
        /// Row 1 (52 tall, gap 8): the kind's icon 34, the value (700 48/52 mono in the kind's colour), the status icons
        /// 28, the swatch 22 at margin-left auto (not for 休み). Row 2 (26 tall, 400 16/22): the kind's word (状態 reads
        /// 技) and the hit mark (30×26: hit = 2px phosphor + person 18; miss = 2px dashed --miss + person 18 + the
        /// slash). .is-active: phosphor border + --shadow-omen-active; .is-rest: 2px dashed steel. At (0, 0) of parent.
        /// </summary>
        public static OcbOmenPlate OmenPlate(RectTransform parent, OcbOmenData d)
        {
            OcbOmenPlate el = Make<OcbOmenPlate>(parent, "ocb-omen", OmenCss);
            el.States = CssStates.Of(el.Root);
            el.States.Base(OmenCss).State(".is-active", OmenActiveCss).State(".is-rest", OmenRestCss);
            // The JS's first update runs on a detached element: its classes are set before it is ever styled, so an
            // active plate is lit from the start (no .12s glow on creation). Only later updates transition.
            UpdateOmen(el, d ?? new OcbOmenData(), instant: true);
            return el;
        }

        internal static void UpdateOmen(OcbOmenPlate el, OcbOmenData d, bool instant = false)
        {
            if (!el.Root) return;
            // el.innerHTML = ''
            Ui.Clear(el.Root);
            OcbOmenKind k = OmenKindOf(d.Icon, d.Kind);
            Rgba color = k.Color;
            bool changed = el.States.SetState(".is-rest", d.Kind == "休み", false);
            changed |= el.States.SetState(".is-active", d.Active, false);
            if (changed || instant) el.States.Apply(instant);

            BoxStyle bs = BoxOf(el.States.Effective());
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = el.W - bw.Horizontal - pad.Horizontal;

            el.Row1 = Make<OcbElement>(el.Root, "ocb-omen-row1", OmenRow1Css);
            el.KindIcon = Icon(el.Row1.Root, k.Icon, 34f, color);
            el.ValueLabel = null;
            if (d.Value != null) el.ValueLabel = Text(el.Row1.Root, d.Value, TextOf(OmenCss + OmenRow1Css + OmenRow1BCss).WithColor(color), "b");
            foreach (string s in d.StatusIcons ?? new List<string>()) Icon(el.Row1.Root, s, 28f, DsTokens.Ink);
            el.Swatch = null;
            if (d.Angle.HasValue && d.Kind != "休み")
            {
                el.Swatch = Swatch(el.Row1.Root, d.Angle.Value, 22f);
                AutoMarginLeft(el.Swatch.Root);   // .ocb-omen-row1 .ocb-swatch { margin-left: auto }
            }
            Flow(el.Row1.Root, OmenRow1Css, null, innerW);

            el.Row2 = Make<OcbElement>(el.Root, "ocb-omen-row2", OmenRow2Css);
            el.KindLabel = Text(el.Row2.Root, d.Kind == "状態" ? "技" : d.Kind, TextOf(OmenCss + OmenRow2Css), "span");
            el.HitMark = null;
            if (d.Hit.HasValue) el.HitMark = HitMark(el.Row2.Root, d.Hit.Value);
            Flow(el.Row2.Root, OmenRow2Css, null, innerW);

            // the column: space-between, the rows stretched to the content width
            Flow(el.Root, el.States.Effective());
            el.SetPart("row1", el.Row1.Root);
            el.SetPart("row2", el.Row2.Root);
        }

        // js: vendor/ds/bundle.js:326
        /// <summary>
        /// .ocb-hitmark (30×26, radius 4, the person icon 18 centred): is-hit = 2px solid --phosphor; is-miss = 2px dashed
        /// --miss with ::after, a 2px --miss line from left 2 to right 2 at top 50%, rotated −24°.
        /// </summary>
        public static OcbElement HitMark(RectTransform parent, bool hit)
        {
            string css = HitmarkCss + (hit ? HitmarkHitCss : HitmarkMissCss);
            OcbElement mark = Make<OcbElement>(parent, "ocb-hitmark", css);
            Fixed(mark.Root);
            Icon(mark.Root, "person", 18f, hit ? DsTokens.Phosphor : DsTokens.Miss);
            if (!hit)
            {
                // ::after in the padding box: left 2, right 2, top 50%, height 2, rotate(-24deg) about its centre
                Insets bw = mark.Box.Style.BorderWidths;
                float pw = mark.W - bw.Horizontal, ph = mark.H - bw.Vertical;
                RectTransform after = El(mark.Root, "after", HitmarkMissAfterCss);
                Absolute(after);
                UiNode an = UiNode.Of(after);
                an.Place(bw.Left + 2f, bw.Top + ph * 0.5f, pw - 4f, 2f);
                an.SetStyleTransform(rot: -24f);
                mark.SetPart("after", after);
            }
            Flow(mark.Root, css);
            return mark;
        }

        // js: vendor/ds/bundle.js:333
        /// <summary>
        /// ReserveOmen(d): .ocb-omen-reserve 170×66 at opacity .62 (2px dashed ink-muted, panel-solid, radius 6,
        /// padding 6 10, flex centred, gap 8): the kind's icon 24, the value (700 28/1 mono in the kind's colour), the
        /// kind (16 px), and 「予定」 (400 14/16, 1px dashed ink-muted, radius 3, padding 0 4) at right 6, bottom 6.
        /// </summary>
        public static OcbElement ReserveOmen(RectTransform parent, OcbOmenData d)
        {
            d = d ?? new OcbOmenData();
            OcbOmenKind k = OmenKindOf(d.Icon, d.Kind);
            OcbElement el = Make<OcbElement>(parent, "ocb-omen-reserve", OmenReserveCss);
            Icon(el.Root, k.Icon, 24f, k.Color);
            if (d.Value != null) el.SetPart("b", Text(el.Root, d.Value, TextOf(OmenReserveCss + OmenReserveBCss).WithColor(k.Color), "b"));
            el.SetPart("kind", Text(el.Root, d.Kind, TextOf(OmenReserveCss + "font-size:16px"), "span"));
            // .ocb-plan: position absolute in the plate's padding box
            OcbElement plan = Make<OcbElement>(el.Root, "ocb-plan", OmenReservePlanCss);
            Absolute(plan.Root);
            BoxStyle ps = plan.Box.Style;
            Insets pb = ps.BorderWidths, pp = ps.Padding;
            TextStyle pts = TextOf(OmenReserveCss + OmenReservePlanCss);
            TextMeshProUGUI pl = Text(plan.Root, "予定", pts);
            Ui.Place(pl.rectTransform, pb.Left + pp.Left, pb.Top + pp.Top);
            float w = TextWidth("予定", pts) + pp.Horizontal + pb.Horizontal, h = pts.LineHeightPx + pp.Vertical + pb.Vertical;
            Insets bw = el.Box.Style.BorderWidths;
            plan.Node.Place(el.W - bw.Right - 6f - w, el.H - bw.Bottom - 6f - h, w, h);   // right 6, bottom 6
            el.SetPart("plan", plan.Root);
            Flow(el.Root, OmenReserveCss);
            return el;
        }
    }
}
