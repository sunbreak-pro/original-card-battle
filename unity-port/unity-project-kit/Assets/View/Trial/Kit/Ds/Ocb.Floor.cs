// The floor pieces (vendor/ds/bundle.js:466-502, bundle.css:70-79): FloorTile with its reach light, threat frames
// and hatches, and the DistanceChip.
using System;
using System.Collections.Generic;
using TMPro;
using Trial.Geometry;
using UnityEngine;

namespace Trial.Kit
{
    /// <summary>One threat on a tile: { angle, boundary } (boundary: the hatch gap is 6 instead of 11).</summary>
    public struct OcbThreat
    {
        public float Angle;
        public bool Boundary;

        public OcbThreat(float angle, bool boundary = false)
        {
            Angle = angle;
            Boundary = boundary;
        }
    }

    /// <summary>A built floor tile (el.reach, el.reachLine, el.threatBox, el.setThreat).</summary>
    public sealed class OcbFloorTile : OcbElement
    {
        /// <summary>.ocb-tile-reach: the amber light (opacity 0 until the scene turns it on).</summary>
        public OcbElement Reach { get; internal set; }
        /// <summary>.ocb-tile-reach-line: the 3px amber bar on top (opacity 0 until the scene turns it on).</summary>
        public OcbElement ReachLine { get; internal set; }
        /// <summary>The threat host (inset 0): the scenes fade it (opacity) and wipe it in (SetThreatClip).</summary>
        public RectTransform ThreatBox { get; internal set; }
        /// <summary>.ocb-tile-threat (null without threats).</summary>
        public OcbElement ThreatFrame { get; internal set; }
        /// <summary>.ocb-tile-move (null when not a move destination).</summary>
        public OcbElement MoveFrame { get; internal set; }
        /// <summary>One hatch per threat, in order.</summary>
        public List<HatchGraphic> Hatches { get; } = new List<HatchGraphic>();

        private float _clipHidden = -1f;

        /// <summary>The reach light and its bar together (floor.js drawReach sets both).</summary>
        public float ReachOpacity
        {
            set
            {
                if (Reach != null) Reach.Node.Opacity = value;
                if (ReachLine != null) ReachLine.Node.Opacity = value;
            }
        }

        // js: vendor/ds/bundle.js:477
        /// <summary>el.setThreat(threats, move): the threat host emptied and filled again.</summary>
        public void SetThreat(IReadOnlyList<OcbThreat> threats, bool move) => Ocb.SetTileThreat(this, threats, move);

        /// <summary>
        /// clip-path: inset(0 x% 0 0) on the threat host (floor.js's left-to-right wipe): everything in it (frames, their
        /// shadows, the hatches) is cut to the host's box with `hiddenRight` (0..1) of its width taken off the right.
        /// </summary>
        public void SetThreatClip(float hiddenRight)
        {
            _clipHidden = Math.Max(0f, Math.Min(1f, hiddenRight));
            Ocb.ApplyThreatClip(this, _clipHidden);
        }

        /// <summary>clip-path: '' (no clip: the frame's shadow reaches outside the host again).</summary>
        public void ClearThreatClip()
        {
            _clipHidden = -1f;
            Ocb.ApplyThreatClip(this, -1f);
        }

        internal float ClipHidden => _clipHidden;
    }

    public static partial class Ocb
    {
        // js: vendor/ds/bundle.js:469
        /// <summary>
        /// FloorTile(w): .ocb-tile w × 40 (--floor-tile, 1px --rule, radius 6) holding the reach light (inset 0, amber
        /// .55 → .18, --shadow-reach, opacity 0), the threat host (inset 0) and the reach bar (3px amber on top, radius
        /// 3 3 0 0, opacity 0), each in the tile's padding box (1 px in from its edge). At (0, 0) of parent.
        /// </summary>
        public static OcbFloorTile FloorTile(RectTransform parent, float w)
        {
            var el = Make<OcbFloorTile>(parent, "ocb-tile", TileCss);
            el.Node.W = w;
            float h = el.H;
            // The tile is position: absolute, so its absolutely placed children sit in its padding box, inside the 1px
            // border: inset 0 = (1, 1, w − 2, 38) (measured in Chrome: reach, host and bar all start at (1, 1)).
            Insets tbw = el.Box.Style.BorderWidths;
            float px = tbw.Left, py = tbw.Top, pw = Math.Max(0f, w - tbw.Horizontal), ph = Math.Max(0f, h - tbw.Vertical);
            el.Reach = Make<OcbElement>(el.Root, "ocb-tile-reach", TileReachCss);
            el.Reach.Node.Place(px, py, pw, ph);   // inset 0
            el.ThreatBox = El(el.Root, "threat-box");
            UiNode tb = UiNode.Of(el.ThreatBox);
            tb.Place(px, py, pw, ph);   // class ocb-pe-none, style position:absolute; inset:0 (its graphics take no pointer)
            el.ReachLine = Make<OcbElement>(el.Root, "ocb-tile-reach-line", TileReachLineCss);
            el.ReachLine.Node.Place(px, py, pw, el.ReachLine.H);   // left 0, right 0, top 0, height 3
            el.SetPart("reach", el.Reach.Root);
            el.SetPart("threat-box", el.ThreatBox);
            el.SetPart("reach-line", el.ReachLine.Root);
            return el;
        }

        internal static void SetTileThreat(OcbFloorTile el, IReadOnlyList<OcbThreat> threats, bool move)
        {
            if (!el.Root) return;
            Ui.Clear(el.ThreatBox);
            el.ThreatFrame = null;
            el.MoveFrame = null;
            el.Hatches.Clear();
            float w = el.W;   // the hatch svg is w × 40 (the closure's w)
            // inset: -1px of the host (the tile's padding box): the frames cover the tile's border box exactly
            UiNode host = UiNode.Of(el.ThreatBox);
            float hw = host.W, hh = host.H;
            if (move)
            {
                el.MoveFrame = Make<OcbElement>(el.ThreatBox, "ocb-tile-move", TileMoveCss);
                el.MoveFrame.Node.Place(-1f, -1f, hw + 2f, hh + 2f);   // inset: -1px
            }
            if (threats != null && threats.Count > 0)
            {
                OcbElement box = Make<OcbElement>(el.ThreatBox, "ocb-tile-threat", TileThreatCss);
                box.Node.Place(-1f, -1f, hw + 2f, hh + 2f);   // inset: -1px
                Insets bw = box.Box.Style.BorderWidths;
                float r = Math.Max(0f, DsTokens.RadiusPanel - bw.Top);   // overflow: hidden: the padding box, inner radius 4
                foreach (OcbThreat t in threats)
                {
                    // .ocb-tile-threat svg { position: absolute; inset: 0 }: an svg of w × 40 at the padding box's corner
                    HatchGraphic hatch = Hatch(box.Root, bw.Left, bw.Top, w, 40f, t.Angle, t.Boundary ? 6f : 11f);
                    hatch.ClipRect = new RectF(0f, 0f, box.W - bw.Horizontal, box.H - bw.Vertical);
                    hatch.ClipRadius = r;
                    el.Hatches.Add(hatch);
                }
                el.ThreatFrame = box;
            }
            if (el.ClipHidden >= 0f) ApplyThreatClip(el, el.ClipHidden);
        }

        internal static void ApplyThreatClip(OcbFloorTile el, float hidden)
        {
            if (!el.Root) return;
            // inset(0 x% 0 0) of the host's border box (the tile's padding box: the frames' outer px and their shadows
            // fall outside it while the clip is on), in each graphic's own box px
            UiNode host = UiNode.Of(el.ThreatBox);
            float w = host.W, h = host.H;
            foreach (VectorGraphic g in el.ThreatBox.GetComponentsInChildren<VectorGraphic>(true))
            {
                if (hidden < 0f) { g.ClipConvex = null; continue; }
                Vec2 o = OffsetIn(el.ThreatBox, g.rectTransform);   // the graphic's top-left in host px
                float ox = o.X, oy = o.Y;
                float right = w * (1f - hidden);
                g.ClipConvex = new List<Vec2>
                {
                    new Vec2(-ox, -oy), new Vec2(right - ox, -oy), new Vec2(right - ox, h - oy), new Vec2(-ox, h - oy),
                };
            }
        }

        /// <summary>The bracket lines' inline styles (bundle.js:493-497).</summary>
        public const string DistLineCss = "position:absolute;background:rgba(236,235,242,.55);";

        // js: vendor/ds/bundle.js:487
        /// <summary>
        /// DistanceChip(n, span): .ocb-dist max(100, span) wide, 34 tall, centred: with span > 0, thin ink .55 brackets
        /// (horizontals at top 17 from 8 px in to the middle, ticks 17 tall at top 9 on both ends) under the chip (34 tall,
        /// padding 0 10, gap 6, panel-solid, 1px steel, radius 6, 700 28/1 mono; `range` 22 ink and n). At (0, 0) of parent.
        /// </summary>
        public static OcbElement DistanceChip(RectTransform parent, string n, float span)
        {
            float w = Math.Max(100f, span > 0f || span < 0f ? span : 0f);   // Math.max(100, span || 0): NaN counts as 0
            OcbElement el = Make<OcbElement>(parent, "ocb-dist", DistCss);
            el.Node.W = w;
            float h = el.H;
            if (span > 0f)
            {
                Line(el.Root, "left", 8f, 17f, w * 0.5f - 8f, 1f);          // left:8px;right:50%;top:17px;height:1px
                Line(el.Root, "right", w * 0.5f, 17f, w * 0.5f - 8f, 1f);   // right:8px;left:50%;top:17px;height:1px
                Line(el.Root, "tick-left", 8f, 9f, 1f, 17f);               // left:8px;top:9px;width:1px;height:17px
                Line(el.Root, "tick-right", w - 8f - 1f, 9f, 1f, 17f);     // right:8px;top:9px;width:1px;height:17px
            }
            OcbElement chip = Make<OcbElement>(el.Root, "ocb-dist-chip", DistChipCss);
            Icon(chip.Root, "range", 22f, DsTokens.Ink);
            chip.SetPart("n", Text(chip.Root, n ?? "", TextOf(DistCss + DistChipCss), "n"));
            Flow(chip.Root, DistChipCss);
            chip.Place((w - chip.W) * 0.5f, (h - chip.H) * 0.5f);   // .ocb-dist: flex, centred
            el.SetPart("chip", chip.Root);
            return el;
        }

        private static void Line(RectTransform parent, string name, float x, float y, float w, float h)
        {
            RectTransform l = El(parent, name, DistLineCss);
            UiNode.Of(l).Place(x, y, w, h);
        }
    }
}
