// Which floor cells gain or lose their stripes (battle_ui_ux_v2 §5.8, #766). Pure C#, so `dotnet test`
// holds the same cells FloorView draws in and fades out. The when is EffectPlan's (StripeDrawStartMs,
// StripeDrawShare, StripeFadeMs); this says only which cells, and in which order.
//
// The screen asks with what its cells hold now rather than with the last frame, because an omen can be
// shown in the middle of an event whose end spends another: the cells the new omen takes are drawn in
// at its drop, and the ones only the old omen held still fade at the event's end.
//
// A cell that keeps stripes, even of another direction or density, is not swept: it changes at once, as
// before. Only stripes that come where there were none are drawn in, and only stripes that go leaving
// none are faded, so a frame that only moves an aim (§5.8 敵が動いた / 押された) is not mistaken for a
// new omen.
using System;
using System.Collections.Generic;

namespace Depiction
{
    public static class StripeSweep
    {
        /// <summary>For each cell of <paramref name="frame"/>'s floor, from the left, whether it has stripes (FloorStripes.Of).</summary>
        public static List<bool> Holding(DepictionFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            var holding = new List<bool>();
            int cells = frame.Floor != null ? frame.Floor.Cells : 0;
            for (int cell = 1; cell <= cells; cell++) holding.Add(FloorStripes.Of(frame, cell).Count > 0);
            return holding;
        }

        /// <summary>
        /// The cells (1-based, left to right) that hold no stripes now (<paramref name="holding"/>, one per
        /// cell from the left) and have some in <paramref name="after"/>: the order StripeDrawStartMs staggers.
        /// Nothing when the floor changed its number of cells (a new battle settles at once).
        /// </summary>
        public static List<int> DrawnIn(IReadOnlyList<bool> holding, DepictionFrame after)
        {
            if (after == null) throw new ArgumentNullException(nameof(after));
            var cells = new List<int>();
            if (!SameFloor(holding, after)) return cells;
            for (int cell = 1; cell <= after.Floor.Cells; cell++)
            {
                if (!holding[cell - 1] && FloorStripes.Of(after, cell).Count > 0) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>
        /// The cells (1-based, left to right) that hold stripes now and have none in <paramref name="after"/>:
        /// those fade together over StripeFadeMs. Nothing when the floor changed its number of cells.
        /// </summary>
        public static List<int> FadedOut(IReadOnlyList<bool> holding, DepictionFrame after)
        {
            if (after == null) throw new ArgumentNullException(nameof(after));
            var cells = new List<int>();
            if (!SameFloor(holding, after)) return cells;
            for (int cell = 1; cell <= after.Floor.Cells; cell++)
            {
                if (holding[cell - 1] && FloorStripes.Of(after, cell).Count == 0) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>From one frame to the next: nothing when there is no frame before (the first frame settles at once).</summary>
        public static List<int> DrawnIn(DepictionFrame before, DepictionFrame after) =>
            before == null ? new List<int>() : DrawnIn(Holding(before), after);

        /// <summary>From one frame to the next: nothing when there is no frame before.</summary>
        public static List<int> FadedOut(DepictionFrame before, DepictionFrame after) =>
            before == null ? new List<int>() : FadedOut(Holding(before), after);

        private static bool SameFloor(IReadOnlyList<bool> holding, DepictionFrame after) =>
            holding != null && after.Floor != null && after.Floor.Cells > 0 && holding.Count == after.Floor.Cells;
    }
}
