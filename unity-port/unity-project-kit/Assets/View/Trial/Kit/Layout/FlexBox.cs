// CSS flex layout for uGUI (kit.md §4.8): Trial.Geometry's Flex.Solve on the active children of a container, the
// results written back as stage-px boxes. It runs when content changes, never per frame; Unity's LayoutGroups are not
// used (different semantics, rebuild costs).
//
// Each child's natural size: its UiNode / RectTransform size, or for a label sized to its text, the text's
// max-content width and lines × line height (its first baseline feeds align-items: baseline). Item properties
// (flex-grow, margins, align-self, min-width…) live on a FlexAttrs component on the child, or come from a callback.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>The CSS flex-item properties of a child (flex-grow, flex-shrink, flex-basis, margins, align-self, min / max).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class FlexAttrs : MonoBehaviour
    {
        public float Grow;
        public float Shrink = 1f;
        public float? Basis;
        public float? BasisPercent;
        public float MarginL, MarginR, MarginT, MarginB;
        public bool AutoMarginL, AutoMarginR, AutoMarginT, AutoMarginB;
        public float? MinMain;
        public float? MaxMain;
        public Align? Self;
        public int Order;
        /// <summary>A fixed width / height: the item is not stretched on the cross axis.</summary>
        public bool FixedCross;
        /// <summary>Not laid out (position: absolute inside the flex container).</summary>
        public bool Absolute;

        /// <summary>flex: 1.</summary>
        public FlexAttrs Fill() { Grow = 1f; Shrink = 1f; BasisPercent = 0f; return this; }

        public FlexAttrs Margins(float top, float right, float bottom, float left)
        {
            MarginT = top; MarginR = right; MarginB = bottom; MarginL = left;
            return this;
        }
    }

    public static class FlexBox
    {
        /// <summary>The FlexAttrs of a child (added when missing).</summary>
        public static FlexAttrs Attrs(RectTransform child)
        {
            FlexAttrs a = child.GetComponent<FlexAttrs>();
            return a ? a : child.gameObject.AddComponent<FlexAttrs>();
        }

        /// <summary>The natural size and baseline of a child: a label sized to its text measures it; anything else keeps its box.</summary>
        public static FlexItem Measure(RectTransform child)
        {
            UiNode node = child.GetComponent<UiNode>();
            float w = node ? node.W : child.rect.width;
            float h = node ? node.H : child.rect.height;
            var item = new FlexItem { W = w, H = h };
            TMP_Text t = child.GetComponent<TMP_Text>();
            if (t)
            {
                LabelState st = t.GetComponent<LabelState>();
                TextStyle style = st ? st.Style : null;
                if (st && style != null)
                {
                    if (st.AutoW) item.W = TextMeasure.Width(st.Text, style);
                    if (st.AutoH) item.H = TextMeasure.Height(st.Text, style, item.W + 0.01f);
                    item.Baseline = TextMath.CssBaselineFromTop(style.Spec);
                    item.FixedCross = false;
                }
            }
            FlexAttrs a = child.GetComponent<FlexAttrs>();
            if (a)
            {
                item.Grow = a.Grow;
                item.Shrink = a.Shrink;
                item.Basis = a.Basis;
                item.BasisPercent = a.BasisPercent;
                item.MarginL = a.MarginL; item.MarginR = a.MarginR; item.MarginT = a.MarginT; item.MarginB = a.MarginB;
                item.AutoMarginL = a.AutoMarginL; item.AutoMarginR = a.AutoMarginR;
                item.AutoMarginT = a.AutoMarginT; item.AutoMarginB = a.AutoMarginB;
                item.MinMain = a.MinMain;
                item.MaxMain = a.MaxMain;
                item.Self = a.Self;
                item.Order = a.Order;
                item.FixedCross = a.FixedCross;
            }
            return item;
        }

        /// <summary>
        /// Lays out the active children of `container` (sibling order; FlexAttrs.Absolute ones skipped). `item`
        /// overrides the measured FlexItem of a child (return null to keep it). The children get their boxes (sizes too
        /// when stretched, grown or shrunk); the container gets the content size when spec.Width / Height is null and
        /// resizeContainer is true. Returns the solved layout.
        /// </summary>
        public static FlexResult Layout(RectTransform container, FlexSpec spec, Func<RectTransform, FlexItem, FlexItem> item = null, bool resizeContainer = true)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            var children = new List<RectTransform>();
            var items = new List<FlexItem>();
            for (int i = 0; i < container.childCount; i++)
            {
                var c = container.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeSelf) continue;
                FlexAttrs a = c.GetComponent<FlexAttrs>();
                if (a && a.Absolute) continue;
                FlexItem fi = Measure(c);
                if (item != null) fi = item(c, fi) ?? fi;
                children.Add(c);
                items.Add(fi);
            }
            FlexResult r = Flex.Solve(spec, items);
            for (int i = 0; i < children.Count; i++)
            {
                RectF box = r.Items[i];
                RectTransform c = children[i];
                Ui.Place(c, box.X, box.Y, box.W, box.H);
                TMP_Text t = c.GetComponent<TMP_Text>();
                if (t)
                {
                    // A label given its box keeps it (stretched or grown boxes must not snap back to the text's size).
                    LabelState st = t.GetComponent<LabelState>();
                    if (st && (Math.Abs(box.W - items[i].W) > 0.01f)) st.AutoW = false;
                    if (st && (Math.Abs(box.H - items[i].H) > 0.01f)) st.AutoH = false;
                }
            }
            if (resizeContainer && (!spec.Width.HasValue || !spec.Height.HasValue))
            {
                UiNode node = container.GetComponent<UiNode>();
                float w = spec.Width ?? r.Width, h = spec.Height ?? r.Height;
                if (node) { node.W = w; node.H = h; }
                else container.sizeDelta = new Vector2(w, h);
            }
            return r;
        }
    }
}
