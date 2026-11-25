// How big the exploration map draws its nodes for a map of a given row count, in stage px (#1049). The map stays on
// one screen (no scrolling), so the rows share the map area's height and a longer map has less room per row: the node
// face and everything sized after it (icon, edge inset, walker, selection frame) shrink together, while the label keeps
// its 16 px text. The JS's sizes (88 px nodes) are kept for every map of five rows or fewer, so the king room and the
// older five-row shapes look as they did. The view reads these values and computes none of its own.
using System;

namespace Exploration
{
    public sealed class MapLayout
    {
        /// <summary>The map area the node fractions spread over (ExploreScene.js:47 AREA).</summary>
        public const float AreaWidth = 1060f, AreaHeight = 680f;

        /// <summary>The JS's node face (ExploreScene.js:48 NODE).</summary>
        public const float FullNode = 88f;

        /// <summary>Maps up to this many rows keep the JS's sizes exactly.</summary>
        public const int FullSizeRows = 5;

        /// <summary>The label's text and line (explore.css:89: 700 16px/20px). Text never drops below 16 px.</summary>
        public const float LabelFontSize = 16f, LabelLine = 20f;

        /// <summary>
        /// The least room left between a node's label and the walker standing on the node straight below it, the
        /// tightest pair of a map: the walker's disc rises above its node, the label hangs under the one above.
        /// </summary>
        public const float MinClearance = 4f;

        // The JS's values at 88 px (ExploreScene.js:50-53, :258-262, explore.css:89, :107-110), scaled with the face.
        private const float BaseLabelGap = 8f, BaseEdgeGap = 8f, BaseIcon = 44f, BaseWalkerLift = 6f, BaseWalkerBox = 52f,
            BaseWalkerGlowInset = -24f, BaseWalkerDiscInset = 6f, BaseWalkerIcon = 24f, BaseSelSide = 22f,
            BaseSelAbove = 18f, BaseSelBelow = 12f, BaseSelHook = 20f;

        /// <summary>How far the walker's disc rises above its node's top, per px of face (6 lift + 26 half box − 6 inset).</summary>
        private const float DiscReach = (BaseWalkerLift + BaseWalkerBox / 2f - BaseWalkerDiscInset) / FullNode;

        /// <summary>The JS's sizes.</summary>
        public static readonly MapLayout Full = new MapLayout(FullNode);

        private MapLayout(float node)
        {
            Node = node;
            Scale = node / FullNode;
        }

        /// <summary>The sizes for a map of <paramref name="rowCount"/> rows.</summary>
        public static MapLayout ForRows(int rowCount)
        {
            if (rowCount <= FullSizeRows) return Full;
            float spacing = AreaHeight / (rowCount - 1);
            // Straight below a node: its label (gap + line under the face) and the next node's walker (its disc over
            // that face) must leave MinClearance in one row's height.
            float fit = (spacing - LabelLine - MinClearance) / (1f + BaseLabelGap / FullNode + DiscReach);
            // Whole even px keep the face's centre on a whole px; the epsilon keeps an exact fit from rounding down.
            float node = Math.Min(FullNode, 2f * (float)Math.Floor(fit / 2f + 1e-4f));
            return node >= FullNode ? Full : new MapLayout(node);
        }

        /// <summary>The node's centre from the area's centre, x right (NodePanel.X: 0 = left edge, 1 = right).</summary>
        public static float OffsetX(float x) => (x - 0.5f) * AreaWidth;

        /// <summary>The node's centre from the area's centre, y down (NodePanel.Y: 0 = bottom, 1 = top).</summary>
        public static float OffsetY(float y) => (0.5f - y) * AreaHeight;

        /// <summary>The node face's side (.ocbg-explore-node: the pointer target, 88 × 88 in the JS).</summary>
        public float Node { get; }

        /// <summary>Node / 88: what the sizes below are scaled by.</summary>
        public float Scale { get; }

        /// <summary>The label's top from the face's top (explore.css:89 top: 96px).</summary>
        public float LabelTop => Node + BaseLabelGap * Scale;

        /// <summary>The label's bottom from the face's top.</summary>
        public float LabelBottom => LabelTop + LabelLine;

        /// <summary>EDGE_INSET: where an edge line starts and ends, measured from a node's centre.</summary>
        public float EdgeInset => Node / 2f + BaseEdgeGap * Scale;

        /// <summary>The kind's icon in the face (ico(kind, 44)).</summary>
        public float Icon => BaseIcon * Scale;

        /// <summary>WALKER_UP: the walker stands on its node's top edge, this far above the node's centre.</summary>
        public float WalkerUp => Node / 2f + BaseWalkerLift * Scale;

        /// <summary>WALKER_BOX: the walker's box (explore.css:107 52 × 52).</summary>
        public float WalkerBox => BaseWalkerBox * Scale;

        /// <summary>The walker's glow around its box (explore.css:108 inset −24px).</summary>
        public float WalkerGlowInset => BaseWalkerGlowInset * Scale;

        /// <summary>The walker's disc inside its box (explore.css:110 inset 6px).</summary>
        public float WalkerDiscInset => BaseWalkerDiscInset * Scale;

        /// <summary>The person in the walker's disc (ico('person', 24)).</summary>
        public float WalkerIcon => BaseWalkerIcon * Scale;

        /// <summary>SEL.w: the selection frame's width (node and label).</summary>
        public float SelW => Node + 2f * BaseSelSide * Scale;

        /// <summary>SEL.h: the frame runs from SelAbove over the face to a margin under the label.</summary>
        public float SelH => SelAbove + LabelBottom + BaseSelBelow * Scale;

        /// <summary>How far the selection frame starts above the face's top.</summary>
        public float SelAbove => BaseSelAbove * Scale;

        /// <summary>The selection frame's corner hooks (OCB.TargetFrame hook: 20).</summary>
        public float SelHook => BaseSelHook * Scale;

        /// <summary>How far the walker's disc rises above its node's centre (the walker standing on it, at rest).</summary>
        public float WalkerDiscTop => WalkerUp + WalkerBox / 2f - WalkerDiscInset;
    }
}
