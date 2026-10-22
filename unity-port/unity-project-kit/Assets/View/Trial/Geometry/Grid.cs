// A CSS grid solver for the templates the scenes write (kit.md §4.8): px, %, fr, auto, minmax(min, max) and
// repeat(n, …) columns ("24px auto", "186px 1fr", "repeat(3, minmax(0, 1fr))", "28px 150px 1fr auto",
// "136px minmax(0, 1fr) auto"), explicit px rows or auto rows, column / row gaps, line-based placement
// (grid-column: 2 / 3, 1 / -1, span n) and sparse auto-placement, justify / align per item.
//
// Track sizing follows CSS Grid §12 in its simple form: fixed tracks take their size; intrinsic (auto) tracks start
// at their items' min-content and grow up to their max-content while there is room ("maximize tracks"); fr tracks
// then share what is left, a `1fr` (= minmax(auto, 1fr)) never below its min-content, a minmax(0, 1fr) from 0.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Trial.Geometry
{
    public enum TrackSize { Px, Percent, Fr, Auto, MinContent, MaxContent }

    /// <summary>One track: its min and max sizing functions (a plain "24px" has both 24px; "1fr" is minmax(auto, 1fr)).</summary>
    public readonly struct Track : IEquatable<Track>
    {
        public readonly TrackSize MinKind, MaxKind;
        public readonly float MinValue, MaxValue;

        public Track(TrackSize minKind, float minValue, TrackSize maxKind, float maxValue)
        {
            MinKind = minKind; MinValue = minValue; MaxKind = maxKind; MaxValue = maxValue;
        }

        public static Track Px(float px) => new Track(TrackSize.Px, px, TrackSize.Px, px);
        public static Track Fr(float fr = 1f) => new Track(TrackSize.Auto, 0f, TrackSize.Fr, fr);
        public static readonly Track Auto = new Track(TrackSize.Auto, 0f, TrackSize.Auto, 0f);
        public static Track MinMax(Track min, Track max) => new Track(min.MinKind, min.MinValue, max.MaxKind, max.MaxValue);

        public bool IsFlexible => MaxKind == TrackSize.Fr;

        public bool Equals(Track o) => MinKind == o.MinKind && MaxKind == o.MaxKind && MinValue == o.MinValue && MaxValue == o.MaxValue;
        public override bool Equals(object obj) => obj is Track t && Equals(t);
        public override int GetHashCode() => (int)MinKind ^ (int)MaxKind * 7 ^ MinValue.GetHashCode() ^ MaxValue.GetHashCode() * 3;

        public override string ToString()
        {
            string F(TrackSize k, float v) => k == TrackSize.Px ? v.ToString("0.###", CultureInfo.InvariantCulture) + "px"
                : k == TrackSize.Percent ? v.ToString("0.###", CultureInfo.InvariantCulture) + "%"
                : k == TrackSize.Fr ? v.ToString("0.###", CultureInfo.InvariantCulture) + "fr"
                : k == TrackSize.MinContent ? "min-content" : k == TrackSize.MaxContent ? "max-content" : "auto";
            if (MinKind == MaxKind && MinValue == MaxValue) return F(MinKind, MinValue);
            if (MinKind == TrackSize.Auto && MaxKind == TrackSize.Fr) return F(MaxKind, MaxValue);
            return "minmax(" + F(MinKind, MinValue) + ", " + F(MaxKind, MaxValue) + ")";
        }
    }

    public sealed class GridItem
    {
        /// <summary>max-content size (border box).</summary>
        public float W, H;
        /// <summary>min-content width; NaN = W (text that cannot wrap).</summary>
        public float MinW = float.NaN;
        /// <summary>1-based start / end lines (negative counts from the end: −1 = the last line); 0 = auto.</summary>
        public int ColumnStart, ColumnEnd, RowStart, RowEnd;
        /// <summary>span n when the end is auto.</summary>
        public int ColumnSpan = 1, RowSpan = 1;
        /// <summary>justify-self / align-self; null = the container's (stretch by default).</summary>
        public Align? JustifySelf, AlignSelf;
        /// <summary>The width / height is set: stretch does not change it.</summary>
        public bool FixedW, FixedH;
        public float MarginL, MarginR, MarginT, MarginB;
        /// <summary>The first baseline below the border-box top (align-items: baseline); NaN = the bottom edge.</summary>
        public float Baseline = float.NaN;

        public static GridItem Of(float w, float h) => new GridItem { W = w, H = h };
    }

    public sealed class GridSpec
    {
        public List<Track> Columns = new List<Track>();
        /// <summary>Explicit rows (grid-template-rows); rows beyond them are auto.</summary>
        public List<Track> Rows = new List<Track>();
        public float ColumnGap, RowGap;
        /// <summary>
        /// The content-box width the columns share (fr needs it). NaN = the grid sizes to its content (an inline or
        /// shrink-to-fit grid such as the card's `.ocb-val`): auto and fr columns take their max-content.
        /// </summary>
        public float Width = float.NaN;
        /// <summary>CSS §12.8: with justify-content normal / stretch, leftover width goes to the auto columns.</summary>
        public bool StretchAutoTracks = true;
        /// <summary>The content-box height (for % / fr rows); null = rows fit their items.</summary>
        public float? Height;
        public Align JustifyItems = Align.Stretch, AlignItems = Align.Stretch;
    }

    public sealed class GridResult
    {
        /// <summary>Each item's border box relative to the grid's content box, in the input order.</summary>
        public RectF[] Items;
        public float[] ColX, ColW, RowY, RowH;
        public float Width, Height;
        /// <summary>The cell (0-based column, row) each item starts in.</summary>
        public (int col, int row)[] Cells;
    }

    public static class Grid
    {
        /// <summary>grid-template-columns / -rows text → tracks ("repeat(3, minmax(0, 1fr))" expands; "none" → empty).</summary>
        public static List<Track> ParseTracks(string css)
        {
            var list = new List<Track>();
            if (string.IsNullOrWhiteSpace(css) || css.Trim() == "none") return list;
            foreach (string tok in CssLex.SplitSpaces(css.Trim()))
            {
                string t = tok.Trim().ToLowerInvariant();
                if (t.StartsWith("repeat(", StringComparison.Ordinal) && t.EndsWith(")", StringComparison.Ordinal))
                {
                    string inner = t.Substring(7, t.Length - 8);
                    int comma = CssLex.IndexOfTopLevel(inner, ',');
                    if (comma < 0) throw new FormatException("Bad repeat(): " + css);
                    string count = inner.Substring(0, comma).Trim();
                    List<Track> body = ParseTracks(inner.Substring(comma + 1));
                    // auto-fill / auto-fit need the container width; they repeat once here (the trial's scenes do not use them)
                    int n = int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) ? c : 1;
                    for (int k = 0; k < n; k++) list.AddRange(body);
                    continue;
                }
                list.Add(ParseTrack(t));
            }
            return list;
        }

        public static Track ParseTrack(string t)
        {
            t = t.Trim().ToLowerInvariant();
            if (t.StartsWith("minmax(", StringComparison.Ordinal) && t.EndsWith(")", StringComparison.Ordinal))
            {
                List<string> args = CssLex.SplitTopLevel(t.Substring(7, t.Length - 8), ',');
                if (args.Count != 2) throw new FormatException("Bad minmax(): " + t);
                Track min = ParseTrack(args[0]), max = ParseTrack(args[1]);
                TrackSize minKind = min.MinKind == TrackSize.Auto && min.MaxKind == TrackSize.Fr ? TrackSize.Auto : min.MinKind;
                return new Track(minKind, min.MinValue, max.MaxKind, max.MaxValue);
            }
            switch (t)
            {
                case "auto": return Track.Auto;
                case "min-content": return new Track(TrackSize.MinContent, 0f, TrackSize.MinContent, 0f);
                case "max-content": return new Track(TrackSize.MaxContent, 0f, TrackSize.MaxContent, 0f);
            }
            if (!CssLex.TryNumber(t, out float v, out string unit)) throw new FormatException("Bad grid track: " + t);
            switch (unit)
            {
                case "px":
                case "": return Track.Px(v);
                case "%": return new Track(TrackSize.Percent, v, TrackSize.Percent, v);
                case "fr": return Track.Fr(v);
                default: throw new FormatException("Bad grid track unit: " + t);
            }
        }

        /// <summary>The kit.md signature: columns over a width, auto rows.</summary>
        public static GridResult Solve(IReadOnlyList<Track> cols, float colGap, float rowGap, IReadOnlyList<GridItem> items, float width)
        {
            var spec = new GridSpec { ColumnGap = colGap, RowGap = rowGap, Width = width };
            spec.Columns.AddRange(cols);
            return Solve(spec, items);
        }

        public static GridResult Solve(GridSpec spec, IReadOnlyList<GridItem> items)
        {
            int n = items.Count;
            int nCols = Math.Max(1, spec.Columns.Count);
            var cells = new (int col, int span, int row, int rspan)[n];
            var occupied = new List<bool[]>();

            bool Fits(int r, int c, int span, int rspan)
            {
                if (c < 0 || c + span > nCols) return false;
                for (int rr = r; rr < r + rspan; rr++)
                {
                    while (occupied.Count <= rr) occupied.Add(new bool[nCols]);
                    for (int cc = c; cc < c + span; cc++) if (occupied[rr][cc]) return false;
                }
                return true;
            }

            void Occupy(int r, int c, int span, int rspan)
            {
                for (int rr = r; rr < r + rspan; rr++)
                {
                    while (occupied.Count <= rr) occupied.Add(new bool[nCols]);
                    for (int cc = c; cc < Math.Min(nCols, c + span); cc++) occupied[rr][cc] = true;
                }
            }

            // definite rows and columns first, then auto placement in order (sparse)
            var done = new bool[n];
            for (int i = 0; i < n; i++)
            {
                GridItem it = items[i];
                if (it.RowStart == 0 || it.ColumnStart == 0) continue;
                (int c, int span) = Lines(it.ColumnStart, it.ColumnEnd, it.ColumnSpan, nCols);
                (int r, int rspan) = Lines(it.RowStart, it.RowEnd, it.RowSpan, Math.Max(spec.Rows.Count, 1));
                cells[i] = (c, span, r, rspan);
                Occupy(r, c, span, rspan);
                done[i] = true;
            }
            int curRow = 0, curCol = 0;
            for (int i = 0; i < n; i++)
            {
                if (done[i]) continue;
                GridItem it = items[i];
                int rspan = Math.Max(1, it.RowSpan);
                if (it.ColumnStart != 0)
                {
                    (int c, int span) = Lines(it.ColumnStart, it.ColumnEnd, it.ColumnSpan, nCols);
                    if (c < curCol) curRow++;
                    while (!Fits(curRow, c, span, rspan)) curRow++;
                    cells[i] = (c, span, curRow, rspan);
                    Occupy(curRow, c, span, rspan);
                    curCol = c + span;
                }
                else
                {
                    int span = Math.Max(1, Math.Min(nCols, it.ColumnSpan));
                    while (true)
                    {
                        if (curCol + span > nCols) { curRow++; curCol = 0; }
                        if (Fits(curRow, curCol, span, rspan)) break;
                        curCol++;
                    }
                    cells[i] = (curCol, span, curRow, rspan);
                    Occupy(curRow, curCol, span, rspan);
                    curCol += span;
                }
            }
            int nRows = Math.Max(spec.Rows.Count, 0);
            foreach (var c in cells) nRows = Math.Max(nRows, c.row + c.rspan);

            float[] colW = SizeColumns(spec, items, cells, nCols);
            var rowH = new float[nRows];
            for (int r = 0; r < nRows; r++)
            {
                if (r < spec.Rows.Count && spec.Rows[r].MinKind == TrackSize.Px) { rowH[r] = spec.Rows[r].MinValue; continue; }
                if (r < spec.Rows.Count && spec.Rows[r].MinKind == TrackSize.Percent && spec.Height.HasValue) { rowH[r] = spec.Rows[r].MinValue * spec.Height.Value / 100f; continue; }
                float h = 0f;
                for (int i = 0; i < n; i++)
                    if (cells[i].row == r && cells[i].rspan == 1) h = Math.Max(h, items[i].H + items[i].MarginT + items[i].MarginB);
                rowH[r] = h;
            }
            var colX = new float[nCols];
            float x = 0f;
            for (int c = 0; c < nCols; c++) { colX[c] = x; x += colW[c] + spec.ColumnGap; }
            var rowY = new float[nRows];
            float y = 0f;
            for (int r = 0; r < nRows; r++) { rowY[r] = y; y += rowH[r] + spec.RowGap; }
            float totalW = nCols > 0 ? colX[nCols - 1] + colW[nCols - 1] : 0f;
            float totalH = nRows > 0 ? rowY[nRows - 1] + rowH[nRows - 1] : 0f;

            // row baselines
            var rowBase = new float[nRows];
            for (int i = 0; i < n; i++)
            {
                if ((items[i].AlignSelf ?? spec.AlignItems) != Align.Baseline) continue;
                rowBase[cells[i].row] = Math.Max(rowBase[cells[i].row], BaselineOf(items[i]) + items[i].MarginT);
            }

            var rects = new RectF[n];
            var starts = new (int, int)[n];
            for (int i = 0; i < n; i++)
            {
                GridItem it = items[i];
                (int c, int span, int r, int rspan) = cells[i];
                float cx = colX[c];
                float cw = colX[Math.Min(nCols - 1, c + span - 1)] + colW[Math.Min(nCols - 1, c + span - 1)] - cx;
                float cy = rowY[r];
                float ch = rowY[Math.Min(nRows - 1, r + rspan - 1)] + rowH[Math.Min(nRows - 1, r + rspan - 1)] - cy;
                float w = it.W, h = it.H, ix, iy;
                switch (it.JustifySelf ?? spec.JustifyItems)
                {
                    case Align.Center: ix = cx + it.MarginL + (cw - it.MarginL - it.MarginR - w) / 2f; break;
                    case Align.End: ix = cx + cw - it.MarginR - w; break;
                    case Align.Stretch:
                        if (!it.FixedW) w = Math.Max(0f, cw - it.MarginL - it.MarginR);
                        ix = cx + it.MarginL;
                        break;
                    default: ix = cx + it.MarginL; break;
                }
                switch (it.AlignSelf ?? spec.AlignItems)
                {
                    case Align.Center: iy = cy + it.MarginT + (ch - it.MarginT - it.MarginB - h) / 2f; break;
                    case Align.End: iy = cy + ch - it.MarginB - h; break;
                    case Align.Baseline: iy = cy + rowBase[r] - BaselineOf(it); break;
                    case Align.Stretch:
                        if (!it.FixedH) h = Math.Max(0f, ch - it.MarginT - it.MarginB);
                        iy = cy + it.MarginT;
                        break;
                    default: iy = cy + it.MarginT; break;
                }
                rects[i] = new RectF(ix, iy, w, h);
                starts[i] = (c, r);
            }
            return new GridResult { Items = rects, ColX = colX, ColW = colW, RowY = rowY, RowH = rowH, Width = totalW, Height = totalH, Cells = starts };
        }

        private static float BaselineOf(GridItem it) => float.IsNaN(it.Baseline) ? it.H : it.Baseline;

        /// <summary>1-based lines (negative from the end) → 0-based start column and span.</summary>
        private static (int start, int span) Lines(int start, int end, int span, int count)
        {
            int lines = count + 1;
            int s = start > 0 ? start - 1 : start < 0 ? lines + start : 0;
            int e;
            if (end > 0) e = end - 1;
            else if (end < 0) e = lines + end;
            else e = s + Math.Max(1, span);
            if (e <= s) e = s + 1;
            s = Math.Max(0, Math.Min(count - 1, s));
            return (s, Math.Max(1, Math.Min(count - s, e - s)));
        }

        private static float[] SizeColumns(GridSpec spec, IReadOnlyList<GridItem> items, (int col, int span, int row, int rspan)[] cells, int nCols)
        {
            var baseSize = new float[nCols];
            var limit = new float[nCols];
            var flex = new float[nCols];
            var maxC = new float[nCols];
            bool intrinsic = float.IsNaN(spec.Width);
            float width = intrinsic ? 0f : spec.Width;
            float avail = width - spec.ColumnGap * (nCols - 1);
            for (int c = 0; c < nCols; c++)
            {
                Track t = c < spec.Columns.Count ? spec.Columns[c] : Track.Auto;
                float minContent = 0f, maxContent = 0f;
                for (int i = 0; i < items.Count; i++)
                {
                    if (cells[i].col != c || cells[i].span != 1) continue;
                    GridItem it = items[i];
                    float margins = it.MarginL + it.MarginR;
                    minContent = Math.Max(minContent, (float.IsNaN(it.MinW) ? it.W : it.MinW) + margins);
                    maxContent = Math.Max(maxContent, it.W + margins);
                }
                maxC[c] = maxContent;
                switch (t.MinKind)
                {
                    case TrackSize.Px: baseSize[c] = t.MinValue; break;
                    case TrackSize.Percent: baseSize[c] = t.MinValue * width / 100f; break;
                    case TrackSize.MaxContent: baseSize[c] = maxContent; break;
                    default: baseSize[c] = minContent; break; // auto, min-content
                }
                switch (t.MaxKind)
                {
                    case TrackSize.Px: limit[c] = Math.Max(baseSize[c], t.MaxValue); break;
                    case TrackSize.Percent: limit[c] = Math.Max(baseSize[c], t.MaxValue * width / 100f); break;
                    case TrackSize.Fr: limit[c] = baseSize[c]; flex[c] = t.MaxValue; break;
                    case TrackSize.MinContent: limit[c] = Math.Max(baseSize[c], minContent); break;
                    default: limit[c] = Math.Max(baseSize[c], maxContent); break; // auto, max-content
                }
            }
            // items spanning several columns: grow the spanned intrinsic columns to fit their min-content
            for (int i = 0; i < items.Count; i++)
            {
                if (cells[i].span <= 1) continue;
                GridItem it = items[i];
                float need = (float.IsNaN(it.MinW) ? it.W : it.MinW) + it.MarginL + it.MarginR - spec.ColumnGap * (cells[i].span - 1);
                float have = 0f;
                var grow = new List<int>();
                for (int c = cells[i].col; c < cells[i].col + cells[i].span && c < nCols; c++)
                {
                    have += baseSize[c];
                    Track t = c < spec.Columns.Count ? spec.Columns[c] : Track.Auto;
                    if (t.MinKind == TrackSize.Auto && t.MaxKind != TrackSize.Fr) grow.Add(c);
                }
                if (need > have && grow.Count > 0)
                    foreach (int c in grow) { baseSize[c] += (need - have) / grow.Count; limit[c] = Math.Max(limit[c], baseSize[c]); }
            }
            if (intrinsic)
            {
                // max-content sizing: every intrinsic column at its max-content; fr columns at the used flex fraction
                // (§12.7.1 "find the size of an fr", indefinite space): the largest max-content ÷ flex, a flex factor
                // of one or less counting as one
                float fr = 0f;
                for (int c = 0; c < nCols; c++) if (flex[c] > 0f) fr = Math.Max(fr, flex[c] > 1f ? maxC[c] / flex[c] : maxC[c]);
                for (int c = 0; c < nCols; c++)
                    baseSize[c] = flex[c] > 0f ? Math.Max(baseSize[c], flex[c] * fr) : Math.Max(baseSize[c], limit[c]);
                return baseSize;
            }
            // maximize: free space to the tracks below their growth limits, equally
            for (int guard = 0; guard < nCols + 1; guard++)
            {
                float free = avail;
                for (int c = 0; c < nCols; c++) free -= baseSize[c];
                if (free <= 1e-4f) break;
                int open = 0;
                for (int c = 0; c < nCols; c++) if (baseSize[c] < limit[c] - 1e-4f) open++;
                if (open == 0) break;
                float share = free / open;
                for (int c = 0; c < nCols; c++)
                    if (baseSize[c] < limit[c] - 1e-4f) baseSize[c] = Math.Min(limit[c], baseSize[c] + share);
            }
            // expand flexible tracks (§12.7.1 "find the size of an fr")
            float sumFlex = 0f;
            for (int c = 0; c < nCols; c++) sumFlex += flex[c];
            if (sumFlex > 0f)
            {
                var inflexible = new bool[nCols];
                for (int c = 0; c < nCols; c++) inflexible[c] = flex[c] <= 0f;
                float frSize = 0f;
                for (int guard = 0; guard <= nCols; guard++)
                {
                    float left = avail, flexSum = 0f;
                    for (int c = 0; c < nCols; c++)
                    {
                        if (inflexible[c]) left -= baseSize[c];
                        else flexSum += flex[c];
                    }
                    frSize = left / Math.Max(1f, flexSum);
                    bool changed = false;
                    for (int c = 0; c < nCols; c++)
                        if (!inflexible[c] && flex[c] * frSize < baseSize[c]) { inflexible[c] = true; changed = true; }
                    if (!changed) break;
                }
                for (int c = 0; c < nCols; c++)
                    if (flex[c] > 0f) baseSize[c] = Math.Max(baseSize[c], flex[c] * Math.Max(0f, frSize));
            }
            else if (spec.StretchAutoTracks)
            {
                // §12.8 stretch auto tracks: what is left goes equally to the columns whose max is auto
                float free = avail;
                int autos = 0;
                for (int c = 0; c < nCols; c++)
                {
                    free -= baseSize[c];
                    Track t = c < spec.Columns.Count ? spec.Columns[c] : Track.Auto;
                    if (t.MaxKind == TrackSize.Auto) autos++;
                }
                if (free > 0f && autos > 0)
                    for (int c = 0; c < nCols; c++)
                    {
                        Track t = c < spec.Columns.Count ? spec.Columns[c] : Track.Auto;
                        if (t.MaxKind == TrackSize.Auto) baseSize[c] += free / autos;
                    }
            }
            return baseSize;
        }
    }
}
