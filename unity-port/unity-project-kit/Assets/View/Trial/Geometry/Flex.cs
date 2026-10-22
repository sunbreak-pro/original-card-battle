// A CSS flexbox solver for the subset the DS and the scenes use (kit.md §4.8): row / column (and reverse), gap and
// row-gap, padding, align-items / align-self (start, center, end, stretch, baseline), justify-content (start, center,
// end, space-between, space-around, space-evenly), wrap with align-content, flex-grow / flex-shrink with the CSS
// freeze loop, flex-basis (px or %), auto margins on both axes, order. min-width: auto keeps an item at least its
// content size (the kit gives every item's natural size), so only an item with an explicit min (min-width: 0)
// shrinks below it, as in the browser. Sizes are px; positions are relative to the container's border box.
using System;
using System.Collections.Generic;

namespace Trial.Geometry
{
    public enum Align { Start, Center, End, Stretch, Baseline }

    public enum Justify { Start, Center, End, SpaceBetween, SpaceAround, SpaceEvenly }

    public enum AlignContent { Stretch, Start, Center, End, SpaceBetween, SpaceAround }

    public sealed class FlexSpec
    {
        public bool Column;
        /// <summary>row-reverse / column-reverse.</summary>
        public bool Reverse;
        public bool Wrap;
        /// <summary>Gap between rows (between lines in a row container, between items in a column container).</summary>
        public float RowGap;
        /// <summary>Gap between columns (between items in a row container, between lines in a column container).</summary>
        public float ColumnGap;
        /// <summary>Padding plus border widths: what lies between the border box and the content box.</summary>
        public Insets Padding = Insets.Zero;
        public Align AlignItems = Align.Stretch;
        public Justify Justify = Justify.Start;
        public AlignContent AlignContent = AlignContent.Stretch;
        /// <summary>The container's border-box size; null = fit the content (an absolutely placed box without a width).</summary>
        public float? Width, Height;

        /// <summary>gap: one value for both directions.</summary>
        public FlexSpec SetGap(float gap) { RowGap = gap; ColumnGap = gap; return this; }
    }

    public sealed class FlexItem
    {
        /// <summary>The item's natural border-box size (fixed size, or the content's size for text).</summary>
        public float W, H;
        public float Grow;
        public float Shrink = 1f;
        /// <summary>flex-basis in px (null = auto: the main size).</summary>
        public float? Basis;
        /// <summary>flex-basis in % of the container's inner main size.</summary>
        public float? BasisPercent;
        public float MarginL, MarginR, MarginT, MarginB;
        public bool AutoMarginL, AutoMarginR, AutoMarginT, AutoMarginB;
        /// <summary>min-width / min-height on the main axis; null = auto (the natural size).</summary>
        public float? MinMain;
        public float? MaxMain;
        /// <summary>The first baseline below the border-box top; NaN = none (synthesised from the bottom edge).</summary>
        public float Baseline = float.NaN;
        public Align? Self;
        /// <summary>The cross size is set (width / height given): stretch does not change it.</summary>
        public bool FixedCross;
        public int Order;

        public static FlexItem Of(float w, float h) => new FlexItem { W = w, H = h };

        /// <summary>flex: 1 (= 1 1 0%).</summary>
        public FlexItem Fill() { Grow = 1f; Shrink = 1f; BasisPercent = 0f; return this; }
    }

    public sealed class FlexResult
    {
        /// <summary>Each item's border box, relative to the container's border-box top-left, in the input order.</summary>
        public RectF[] Items;
        /// <summary>The line each item landed on.</summary>
        public int[] Line;
        /// <summary>The container's border-box size (the given one, or the content's).</summary>
        public float Width, Height;
    }

    public static class Flex
    {
        public static FlexResult Solve(FlexSpec spec, IReadOnlyList<FlexItem> items)
        {
            int n = items.Count;
            bool col = spec.Column;
            float padMainStart = col ? spec.Padding.Top : spec.Padding.Left;
            float padMainEnd = col ? spec.Padding.Bottom : spec.Padding.Right;
            float padCrossStart = col ? spec.Padding.Left : spec.Padding.Top;
            float padCrossEnd = col ? spec.Padding.Right : spec.Padding.Bottom;
            float? outerMain = col ? spec.Height : spec.Width;
            float? outerCross = col ? spec.Width : spec.Height;
            float? innerMain = outerMain.HasValue ? Math.Max(0f, outerMain.Value - padMainStart - padMainEnd) : (float?)null;
            float? innerCross = outerCross.HasValue ? Math.Max(0f, outerCross.Value - padCrossStart - padCrossEnd) : (float?)null;
            float mainGap = col ? spec.RowGap : spec.ColumnGap;
            float crossGap = col ? spec.ColumnGap : spec.RowGap;
            // main-start is the right (row-reverse) or bottom (column-reverse) edge: the margins on that side lead
            bool rev = spec.Reverse;
            float MarginMainStart(FlexItem it) => col ? (rev ? it.MarginB : it.MarginT) : (rev ? it.MarginR : it.MarginL);
            float MarginMainEnd(FlexItem it) => col ? (rev ? it.MarginT : it.MarginB) : (rev ? it.MarginL : it.MarginR);
            bool AutoMainStart(FlexItem it) => col ? (rev ? it.AutoMarginB : it.AutoMarginT) : (rev ? it.AutoMarginR : it.AutoMarginL);
            bool AutoMainEnd(FlexItem it) => col ? (rev ? it.AutoMarginT : it.AutoMarginB) : (rev ? it.AutoMarginL : it.AutoMarginR);

            // order (stable)
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => items[a].Order != items[b].Order ? items[a].Order.CompareTo(items[b].Order) : a.CompareTo(b));

            var mainSize = new float[n];
            var flexBase = new float[n];
            var hyp = new float[n];
            var minM = new float[n];
            var maxM = new float[n];
            var mStart = new float[n];
            var mEnd = new float[n];
            for (int i = 0; i < n; i++)
            {
                FlexItem it = items[i];
                float natural = col ? it.H : it.W;
                // flex base size: the basis (px, or % of a definite container), else the natural size
                float basis = it.Basis ?? (it.BasisPercent.HasValue && innerMain.HasValue ? it.BasisPercent.Value * innerMain.Value / 100f : natural);
                // min-width: auto = the content size; an explicit min (min-width: 0) lets it shrink further
                minM[i] = it.MinMain ?? natural;
                maxM[i] = it.MaxMain ?? float.PositiveInfinity;
                flexBase[i] = basis;
                hyp[i] = Clamp(basis, minM[i], maxM[i]);
                mStart[i] = AutoMainStart(it) ? 0f : MarginMainStart(it);
                mEnd[i] = AutoMainEnd(it) ? 0f : MarginMainEnd(it);
            }

            // lines
            var lines = new List<List<int>>();
            var cur = new List<int>();
            float used = 0f;
            foreach (int i in order)
            {
                float outer = hyp[i] + mStart[i] + mEnd[i];
                if (spec.Wrap && innerMain.HasValue && cur.Count > 0 && used + mainGap + outer > innerMain.Value + 1e-3f)
                {
                    lines.Add(cur);
                    cur = new List<int>();
                    used = 0f;
                }
                used += (cur.Count > 0 ? mainGap : 0f) + outer;
                cur.Add(i);
            }
            if (cur.Count > 0 || lines.Count == 0) lines.Add(cur);

            // flexible lengths, per line
            float contentMain = 0f;
            foreach (List<int> line in lines)
            {
                ResolveFlexible(line, items, flexBase, hyp, minM, maxM, mStart, mEnd, mainGap, innerMain, mainSize);
                float sum = 0f;
                for (int k = 0; k < line.Count; k++) sum += mainSize[line[k]] + mStart[line[k]] + mEnd[line[k]] + (k > 0 ? mainGap : 0f);
                contentMain = Math.Max(contentMain, sum);
            }
            float mainAvail = innerMain ?? contentMain;

            // cross sizes (natural), line cross sizes
            var crossSize = new float[n];
            var cStart = new float[n];
            var cEnd = new float[n];
            for (int i = 0; i < n; i++)
            {
                FlexItem it = items[i];
                crossSize[i] = col ? it.W : it.H;
                cStart[i] = col ? (it.AutoMarginL ? 0f : it.MarginL) : (it.AutoMarginT ? 0f : it.MarginT);
                cEnd[i] = col ? (it.AutoMarginR ? 0f : it.MarginR) : (it.AutoMarginB ? 0f : it.MarginB);
            }
            var lineCross = new float[lines.Count];
            var lineBase = new float[lines.Count];
            for (int l = 0; l < lines.Count; l++)
            {
                float maxOuter = 0f, maxAbove = 0f, maxBelow = 0f;
                bool anyBase = false;
                foreach (int i in lines[l])
                {
                    float outer = crossSize[i] + cStart[i] + cEnd[i];
                    if (!col && AlignOf(spec, items[i]) == Align.Baseline && !HasCrossAutoMargin(items[i], col))
                    {
                        float b = BaselineOf(items[i]) + cStart[i];
                        maxAbove = Math.Max(maxAbove, b);
                        maxBelow = Math.Max(maxBelow, outer - b);
                        anyBase = true;
                    }
                    else maxOuter = Math.Max(maxOuter, outer);
                }
                lineBase[l] = maxAbove;
                lineCross[l] = Math.Max(maxOuter, anyBase ? maxAbove + maxBelow : 0f);
            }
            // §9.4 step 15: a single-line container (nowrap) with a definite cross size is one line of that size. A
            // multi-line container (wrap) keeps its lines' own sizes, even with one line, and align-content places them.
            bool multiLine = spec.Wrap;
            if (!multiLine && innerCross.HasValue) lineCross[0] = innerCross.Value;

            // align-content (§9.4 step 16 / CSS Box Alignment fallbacks)
            float totalCross = 0f;
            for (int l = 0; l < lines.Count; l++) totalCross += lineCross[l] + (l > 0 ? crossGap : 0f);
            var lineOffset = new float[lines.Count];
            float crossAvail = innerCross ?? totalCross;
            float extra = crossAvail - totalCross;
            float lead = 0f, between = 0f;
            if (multiLine && innerCross.HasValue)
            {
                switch (spec.AlignContent)
                {
                    case AlignContent.Stretch:
                        if (extra > 0f) for (int l = 0; l < lines.Count; l++) lineCross[l] += extra / lines.Count;
                        break;
                    case AlignContent.Center: lead = extra / 2f; break;
                    case AlignContent.End: lead = extra; break;
                    // negative space or a single line: flex-start
                    case AlignContent.SpaceBetween: if (extra > 0f && lines.Count > 1) between = extra / (lines.Count - 1); break;
                    // negative space: center
                    case AlignContent.SpaceAround:
                        if (extra > 0f) { between = extra / lines.Count; lead = between / 2f; }
                        else lead = extra / 2f;
                        break;
                }
            }
            float y = lead;
            for (int l = 0; l < lines.Count; l++)
            {
                lineOffset[l] = y;
                y += lineCross[l] + crossGap + between;
            }

            // place
            var rects = new RectF[n];
            var lineOf = new int[n];
            for (int l = 0; l < lines.Count; l++)
            {
                List<int> line = lines[l];
                float sum = 0f;
                int autoCount = 0;
                for (int k = 0; k < line.Count; k++)
                {
                    int i = line[k];
                    sum += mainSize[i] + mStart[i] + mEnd[i] + (k > 0 ? mainGap : 0f);
                    FlexItem it = items[i];
                    if (AutoMainStart(it)) autoCount++;
                    if (AutoMainEnd(it)) autoCount++;
                }
                float free = mainAvail - sum;
                float start = 0f, gapExtra = 0f, autoShare = 0f;
                if (free > 0f && autoCount > 0) autoShare = free / autoCount;
                else
                {
                    // CSS Box Alignment fallbacks: space-between → flex-start, space-around / space-evenly → center
                    // when the free space is negative
                    switch (spec.Justify)
                    {
                        case Justify.Center: start = free / 2f; break;
                        case Justify.End: start = free; break;
                        case Justify.SpaceBetween: if (free > 0f && line.Count > 1) gapExtra = free / (line.Count - 1); break;
                        case Justify.SpaceAround:
                            if (free > 0f) { gapExtra = free / line.Count; start = gapExtra / 2f; }
                            else start = free / 2f;
                            break;
                        case Justify.SpaceEvenly:
                            if (free > 0f) { gapExtra = free / (line.Count + 1); start = gapExtra; }
                            else start = free / 2f;
                            break;
                    }
                }
                float pos = start;
                for (int k = 0; k < line.Count; k++)
                {
                    int i = line[k];
                    FlexItem it = items[i];
                    if (k > 0) pos += mainGap + gapExtra;
                    if (AutoMainStart(it)) pos += autoShare;
                    pos += mStart[i];
                    float mainPos = pos;
                    pos += mainSize[i] + mEnd[i];
                    if (AutoMainEnd(it)) pos += autoShare;

                    // cross
                    float lc = lineCross[l];
                    float cs = crossSize[i];
                    float cpos;
                    bool autoS = col ? it.AutoMarginL : it.AutoMarginT, autoE = col ? it.AutoMarginR : it.AutoMarginB;
                    if (autoS || autoE)
                    {
                        float room = lc - cs - cStart[i] - cEnd[i];
                        float share = room > 0f ? room / ((autoS ? 1 : 0) + (autoE ? 1 : 0)) : 0f;
                        cpos = cStart[i] + (autoS ? share : 0f);
                    }
                    else
                    {
                        Align a = AlignOf(spec, it);
                        if (col && a == Align.Baseline) a = Align.Start;
                        switch (a)
                        {
                            case Align.Stretch:
                                if (!it.FixedCross) cs = Math.Max(0f, lc - cStart[i] - cEnd[i]);
                                cpos = cStart[i];
                                break;
                            case Align.Center: cpos = cStart[i] + (lc - cs - cStart[i] - cEnd[i]) / 2f; break;
                            case Align.End: cpos = lc - cEnd[i] - cs; break;
                            case Align.Baseline: cpos = lineBase[l] - BaselineOf(it); break;
                            default: cpos = cStart[i]; break;
                        }
                    }
                    float m = spec.Reverse ? mainAvail - mainPos - mainSize[i] : mainPos;
                    float c = lineOffset[l] + cpos;
                    rects[i] = col
                        ? new RectF(padCrossStart + c, padMainStart + m, cs, mainSize[i])
                        : new RectF(padMainStart + m, padCrossStart + c, mainSize[i], cs);
                    lineOf[i] = l;
                }
            }
            float boxMain = outerMain ?? contentMain + padMainStart + padMainEnd;
            float boxCross = outerCross ?? (lines.Count == 1 && innerCross.HasValue ? innerCross.Value : totalCross) + padCrossStart + padCrossEnd;
            return new FlexResult
            {
                Items = rects,
                Line = lineOf,
                Width = col ? boxCross : boxMain,
                Height = col ? boxMain : boxCross,
            };
        }

        private static Align AlignOf(FlexSpec spec, FlexItem it) => it.Self ?? spec.AlignItems;

        private static bool HasCrossAutoMargin(FlexItem it, bool col) => col ? (it.AutoMarginL || it.AutoMarginR) : (it.AutoMarginT || it.AutoMarginB);

        private static float BaselineOf(FlexItem it) => float.IsNaN(it.Baseline) ? it.H : it.Baseline;

        private static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));

        /// <summary>
        /// CSS Flexbox §9.7 "Resolving Flexible Lengths": grow when the hypothetical sizes leave room, else shrink
        /// (weighted by flex-shrink × base size); items whose target breaks their min / max are frozen there and the
        /// rest is shared again. Without a definite container size nothing flexes.
        /// </summary>
        private static void ResolveFlexible(List<int> line, IReadOnlyList<FlexItem> items, float[] flexBase, float[] hyp, float[] minM,
            float[] maxM, float[] mStart, float[] mEnd, float gap, float? innerMain, float[] result)
        {
            foreach (int i in line) result[i] = hyp[i];
            if (!innerMain.HasValue || line.Count == 0) return;
            float gaps = gap * (line.Count - 1);
            float sumHyp = gaps;
            foreach (int i in line) sumHyp += hyp[i] + mStart[i] + mEnd[i];
            bool growing = sumHyp < innerMain.Value;
            var frozen = new Dictionary<int, bool>();
            foreach (int i in line)
            {
                FlexItem it = items[i];
                float factor = growing ? it.Grow : it.Shrink;
                bool freeze = factor <= 0f || (growing ? flexBase[i] > hyp[i] : flexBase[i] < hyp[i]);
                frozen[i] = freeze;
                result[i] = freeze ? hyp[i] : flexBase[i];
            }
            float Remaining()
            {
                float sum = gaps;
                foreach (int i in line) sum += (frozen[i] ? result[i] : flexBase[i]) + mStart[i] + mEnd[i];
                return innerMain.Value - sum;
            }
            float initialFree = Remaining();
            for (int guard = 0; guard <= line.Count; guard++)
            {
                float sumFactor = 0f, sumScaled = 0f;
                bool any = false;
                foreach (int i in line)
                {
                    if (frozen[i]) continue;
                    any = true;
                    sumFactor += growing ? items[i].Grow : items[i].Shrink;
                    sumScaled += items[i].Shrink * flexBase[i];
                }
                if (!any) break;
                float free = Remaining();
                if (sumFactor < 1f)
                {
                    float scaled = initialFree * sumFactor;
                    if (Math.Abs(scaled) < Math.Abs(free)) free = scaled;
                }
                float totalViolation = 0f;
                var violation = new Dictionary<int, float>();
                foreach (int i in line)
                {
                    if (frozen[i]) continue;
                    FlexItem it = items[i];
                    float target;
                    if (growing) target = flexBase[i] + (sumFactor > 0f ? free * it.Grow / sumFactor : 0f);
                    else target = flexBase[i] + (sumScaled > 0f ? free * it.Shrink * flexBase[i] / sumScaled : 0f);
                    float clamped = Clamp(target, minM[i], maxM[i]);
                    violation[i] = clamped - target;
                    totalViolation += clamped - target;
                    result[i] = clamped;
                }
                if (Math.Abs(totalViolation) < 1e-4f) break;
                foreach (KeyValuePair<int, float> kv in violation)
                    if ((totalViolation > 0f && kv.Value > 0f) || (totalViolation < 0f && kv.Value < 0f)) frozen[kv.Key] = true;
            }
        }
    }
}
