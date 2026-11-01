// The stance slot and its tooltip (vendor/ds/bundle.js:408-427, bundle.css:116-121).
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>StanceSlot's d: { name, count, foe, empty, more, swatchAngle }.</summary>
    public sealed class OcbStanceData
    {
        public string Name = "";
        public int Count = 1;
        public bool Foe;
        public bool Empty;
        /// <summary>d.more: 「+n」 in place of the name (0 = none).</summary>
        public int More;
        public float? SwatchAngle;
    }

    /// <summary>A built stance slot and its .is-hover.</summary>
    public sealed class OcbStanceSlot : OcbElement
    {
        public bool Foe { get; internal set; }
        public bool Empty { get; internal set; }
        public PathGraphic Flag { get; internal set; }
        /// <summary>The name span (null for an empty slot).</summary>
        public TextMeshProUGUI NameLabel { get; internal set; }
        /// <summary>The 「×n」 span (null when not shown).</summary>
        public TextMeshProUGUI CountLabel { get; internal set; }

        private bool _hover;

        /// <summary>.is-hover: the slot's ground rgba(40,44,58,.92).</summary>
        public bool Hover
        {
            get => _hover;
            set
            {
                _hover = value;
                Ocb.Restyle(Root, Ocb.StanceSlotCss(Foe, Empty, _hover));
            }
        }
    }

    public static partial class Ocb
    {
        /// <summary>The slot's declarations: .ocb-stance, .is-foe, .is-empty, .is-hover (stylesheet order).</summary>
        public static string StanceSlotCss(bool foe, bool empty, bool hover) =>
            StanceCss + (foe ? StanceFoeCss : "") + (empty ? StanceEmptyCss : "") + (hover ? StanceHoverCss : "");

        // js: vendor/ds/bundle.js:411
        /// <summary>
        /// StanceSlot(d): .ocb-stance 250×52 (padding 0 12, flex centred, gap 8, --panel, 2px --hp-text, radius 6, 700 20/26
        /// display; .is-foe: violet). Empty: no ground, 2px dashed steel, only the flag 24 in steel. Otherwise: the swatch 16
        /// (optional), the flag 24 (phosphor for a foe, hp-text for self), the name (or 「+n」), and 「×n」 (count > 1;
        /// margin-left auto, 40 wide, right-aligned, 600 16/1 mono). At (0, 0) of parent.
        /// </summary>
        public static OcbStanceSlot StanceSlot(RectTransform parent, OcbStanceData d)
        {
            d = d ?? new OcbStanceData();
            string css = StanceSlotCss(d.Foe, d.Empty, false);
            var el = Make<OcbStanceSlot>(parent, "ocb-stance", css);
            el.Foe = d.Foe;
            el.Empty = d.Empty;
            if (d.Empty)
            {
                el.Flag = Icon(el.Root, "flag", 24f, DsTokens.Steel);
                Flow(el.Root, css);
                return el;
            }
            if (d.SwatchAngle.HasValue) el.SetPart("swatch", Swatch(el.Root, d.SwatchAngle.Value, 16f).Root);
            el.Flag = Icon(el.Root, "flag", 24f, d.Foe ? DsTokens.Phosphor : DsTokens.HpText);
            el.NameLabel = Text(el.Root, d.More != 0 ? "+" + d.More.ToString(System.Globalization.CultureInfo.InvariantCulture) : d.Name, TextOf(css), "span");
            el.SetPart("name", el.NameLabel);
            if (d.More == 0 && d.Count > 1)
            {
                TextStyle xs = TextOf(css + StanceXCss);
                float w = Px(Rule(StanceXCss).Layout.Width) ?? 40f;
                // width 40, text-align right: a fixed box
                el.CountLabel = Label.Make(el.Root, "×" + d.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), xs, 0f, 0f, w, xs.LineHeightPx, "ocb-x");
                FlexAttrs a = FlexBox.Attrs(el.CountLabel.rectTransform);
                a.AutoMarginL = true;
                el.SetPart("x", el.CountLabel);
            }
            Flow(el.Root, css);
            el.SetPart("flag", el.Flag);
            return el;
        }

        // js: vendor/ds/bundle.js:421
        /// <summary>
        /// StanceTip(d): the .ocb-tip with the flag 20 in hp-text, 「name ×n」, 「永続」, and the rows 効き方 (how, plus
        /// 「（重ねた n つぶん効く）」 when stacked) and 減り方 「減らない（永続）」.
        /// </summary>
        public static OcbElement StanceTip(RectTransform parent, string name, int count, string how)
        {
            string c = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Tip(parent, "flag", DsTokens.HpText, name + (count > 1 ? " ×" + c : ""), "永続",
                new KeyValuePair<string, string>("効き方", how + (count > 1 ? "（重ねた " + c + " つぶん効く）" : "")),
                new KeyValuePair<string, string>("減り方", "減らない（永続）"));
        }
    }
}
