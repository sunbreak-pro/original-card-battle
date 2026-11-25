// The base as one scene, 「降り口の前」 (#1052, the design canvas's 出立：降り口の前): the way down stands in the middle
// with the first layer's light behind it and the explorer before it, the collapse calendar over it, the explorer's name
// and how far they have been on the left, the seven layers on the right with where the last descent ended, and at the
// foot the three things to make ready (the deck, the tools, the consumables: each opens 「支度の卓」, #1051) and 「降りる」,
// held to set out. The list of past descents and the rules of traces are in the journal since #1053.
//
// It lives here, engine-free, so `dotnet test` holds the layout and the words (Trial.Bridge.Tests/DepartGateTests):
// nothing overlaps, everything stays on the 1920 × 1080 stage, every text is 16 px or more and every text under 24 px is
// in the body colour (battle-visual-v1 §3.1 / §2.3). Trial.Meta's GateScene draws what this says; what the record and the
// layers read comes from the lineage (InheritanceCore), the pack from DeparturePack.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonCore;
using InheritanceCore;

namespace Trial.Bridge
{
    /// <summary>How far the explorer has been (the left of the base): the descents closed, the deepest layer, the last one's end.</summary>
    public sealed class GateRecord
    {
        /// <summary>The descents closed so far (the lineage's lives, those before the carry-over included).</summary>
        public int Descents;

        /// <summary>The deepest layer any descent reached, or null before the first.</summary>
        public int? Deepest;

        /// <summary>The layer the last descent ended on, or null before the first.</summary>
        public int? LastLayer;

        /// <summary>How the last descent ended (InheritanceText.EndingSentence), or "" before the first.</summary>
        public string LastLine = "";
    }

    /// <summary>One of the seven layers on the right of the base.</summary>
    public sealed class GateLayer
    {
        public int Number;

        /// <summary>What the box says under the number: the mark of the last descent's end, else the layer's name, else "".</summary>
        public string Text = "";

        /// <summary>A descent has reached it (its light shows).</summary>
        public bool Reached;

        /// <summary>The last descent ended here (the crimson frame).</summary>
        public bool Marked;

        /// <summary>The kings' rooms (the boss colour's double frame).</summary>
        public bool King;
    }

    public static class DepartGate
    {
        // ---- the stage and its margins ------------------------------------------------------------------------------

        public const float StageW = 1920f, StageH = 1080f;

        public const float Margin = 48f;

        /// <summary>The header row: 拠点 and the lead on the left; 手記, 設定 and タイトルへ on the right.</summary>
        public static readonly TableBox Header = new TableBox(Margin, 22f, StageW - 2f * Margin, 60f);

        public const float HeaderGap = 24f, CornerGap = 12f, CornerH = 56f;

        // ---- the middle: the way down, its light on the floor, the explorer before it --------------------------------

        /// <summary>The floor in front of the way down (the wall above it is the backdrop).</summary>
        public static readonly TableBox Floor = new TableBox(0f, 772f, StageW, StageH - 772f);

        /// <summary>The way down: an arch (its top corners a half circle) through which the first layer's light shows.</summary>
        public static readonly TableBox Arch = new TableBox(760f, 214f, 520f, 560f);

        /// <summary>The arch's rim: a dark ring 14 px wide, then 2 px of steel round it.</summary>
        public const float ArchRing = 14f, ArchRim = 2f;

        /// <summary>The arch with its rim (the rim does not reach under the floor line).</summary>
        public static readonly TableBox ArchOuter = new TableBox(Arch.X - ArchRing - ArchRim, Arch.Y - ArchRing - ArchRim, Arch.W + 2f * (ArchRing + ArchRim), Arch.H + ArchRing + ArchRim);

        /// <summary>
        /// The bands of rock seen through the way down, bottom up (x from the arch's left, y up from its bottom, w, h, the
        /// alpha of the rock's colour <see cref="RockRgb"/>): the layer's shelves fading into its light.
        /// </summary>
        public static readonly IReadOnlyList<(float X, float Bottom, float W, float H, float Alpha)> RockBands = new[]
        {
            (0f, 0f, 520f, 44f, 0.72f),
            (44f, 72f, 432f, 50f, 0.62f),
            (84f, 156f, 352f, 40f, 0.54f),
            (120f, 232f, 280f, 30f, 0.46f),
            (150f, 300f, 220f, 22f, 0.38f),
        };

        /// <summary>The rock's colour (#10261a), drawn at each band's alpha.</summary>
        public const string RockRgb = "16,38,26";

        /// <summary>The way down's light on the floor: a trapezoid (its top inset 16% on each side) fading downwards.</summary>
        public static readonly TableBox FloorLight = new TableBox(640f, 774f, 760f, 60f);

        public const float FloorLightInset = 0.16f;

        /// <summary>The explorer before the way down (a silhouette until the standing art comes, #84); feet on the floor line.</summary>
        public static readonly TableBox Explorer = new TableBox(520f, 446f, 190f, 340f);

        /// <summary>The explorer's foot light (an ellipse under the feet).</summary>
        public static readonly TableBox ExplorerFoot = new TableBox(Explorer.X, Explorer.Y + 314f, Explorer.W, 24f);

        // ---- over the way down: the calendar -----------------------------------------------------------------------

        /// <summary>「崩壊まで 残り」, the days left and 「日」 on one baseline, centred over the way down.</summary>
        public static readonly TableBox CalendarRow = new TableBox(720f, 106f, 600f, 56f);

        /// <summary>The bar of the 100 days: the share left filled, a tick every ten days.</summary>
        public static readonly TableBox CalendarBar = new TableBox(760f, 172f, 520f, 14f);

        public const int CalendarTickDays = 10;

        /// <summary>The line that the calendar stands still while a king is down, right of the bar.</summary>
        public static readonly TableBox FrozenNote = new TableBox(CalendarBar.Right + 16f, CalendarBar.Y + (CalendarBar.H - 22f) / 2f, 380f, 22f);

        /// <summary>The bar's filled width for the days left (0 to the bar's width).</summary>
        public static float CalendarFill(int daysLeft) => CalendarBar.W * Math.Max(0, Math.Min(DungeonCore.Calendar.TotalDays, daysLeft)) / DungeonCore.Calendar.TotalDays;

        // ---- left: the explorer and how far they have been ----------------------------------------------------------

        public const float LeftX = Margin, LeftW = 420f;

        /// <summary>「次の探索　n 回目」 (18 px on a 26 px line).</summary>
        public static readonly TableBox WhoLabel = new TableBox(LeftX, 214f, LeftW, 26f);

        /// <summary>The explorer's name (64 px on a 72 px line).</summary>
        public static readonly TableBox Name = new TableBox(LeftX, WhoLabel.Bottom + 14f, LeftW, 72f);

        /// <summary>The rule over the record.</summary>
        public static readonly TableBox RecordRule = new TableBox(LeftX, Name.Bottom + 14f, LeftW, 1f);

        /// <summary>The record's rows: a label column 150 wide, 16 apart from the value; rows 30 a line, 8 apart.</summary>
        public const float RecordTop = 351f, RecordLabelW = 150f, RecordGap = 16f, RecordLine = 30f, RecordRowGap = 8f;

        /// <summary>The most lines a row's value wraps to (the longest end sentence takes three).</summary>
        public const int RecordMaxLines = 3;

        public static float RecordValueX => LeftX + RecordLabelW + RecordGap;

        public static float RecordValueW => LeftW - RecordLabelW - RecordGap;

        /// <summary>The whole record at its tallest: three rows of three lines.</summary>
        public static readonly TableBox Record = new TableBox(LeftX, RecordTop, LeftW, 3f * RecordMaxLines * RecordLine + 2f * RecordRowGap);

        // ---- right: the seven layers below -------------------------------------------------------------------------

        public const float LayersX = 1700f, LayersW = 172f;

        /// <summary>「この下」 (18 px on a 26 px line).</summary>
        public static readonly TableBox LayersHead = new TableBox(LayersX, 214f, LayersW, 26f);

        public const float LayerH = 74f, LayerGap = 6f;

        /// <summary>Inside a layer's box: the number (24 px) at the top, the words (16 px) at the bottom, 18 in from the left.</summary>
        public const float LayerPadX = 18f, LayerPadRight = 10f, LayerPadY = 6f, LayerNumberLine = 26f, LayerTextLine = 22f;

        /// <summary>The strip of a reached layer's light along the box's left edge.</summary>
        public const float LayerStripX = 5f, LayerStripW = 6f;

        /// <summary>Layer <paramref name="layer"/>'s box (1 at the top).</summary>
        public static TableBox Layer(int layer)
        {
            if (layer < 1 || layer > Lair.LayerCount) throw new ArgumentOutOfRangeException(nameof(layer), layer, "1 to " + Lair.LayerCount + ".");
            return new TableBox(LayersX, LayersHead.Bottom + LayerGap + (layer - 1) * (LayerH + LayerGap), LayersW, LayerH);
        }

        // ---- the foot: the three things to make ready, then the way down itself ------------------------------------

        public static readonly TableBox Foot = new TableBox(Margin, 846f, StageW - 2f * Margin, 210f);

        public const float FootGap = 16f;

        /// <summary>The hold button is 1.25 times as wide as each of the three boxes.</summary>
        public const float HoldFlex = 1.25f;

        public const float BoxPadX = 16f, BoxPadY = 14f, BoxBorder = 1f, BoxGap = 8f;

        private static float BoxUnit => (Foot.W - 3f * FootGap) / (3f + HoldFlex);

        /// <summary>Box <paramref name="index"/> of the foot: 0 the deck, 1 the tools, 2 the consumables.</summary>
        public static TableBox Box(int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index), index, "0 to 2.");
            return new TableBox(Foot.X + index * (BoxUnit + FootGap), Foot.Y, BoxUnit, Foot.H);
        }

        /// <summary>「降りる」, held.</summary>
        public static TableBox Hold => new TableBox(Box(2).Right + FootGap, Foot.Y, Foot.Right - Box(2).Right - FootGap, Foot.H);

        /// <summary>A box's content (its border and padding in).</summary>
        public static TableBox Inner(TableBox box) => new TableBox(box.X + BoxBorder + BoxPadX, box.Y + BoxBorder + BoxPadY, box.W - 2f * (BoxBorder + BoxPadX), box.H - 2f * (BoxBorder + BoxPadY));

        /// <summary>A box's label row: its icon (20) and its word (16 px).</summary>
        public const float LabelH = 24f, LabelIcon = 20f, LabelIconGap = 8f;

        /// <summary>The deck's count (44 px on a 46 px line), 「枚」 (24 px) and the rule (16 px) on one baseline.</summary>
        public const float CountLine = 46f;

        /// <summary>「組み直す」 at the bottom left of the deck's box.</summary>
        public const float GoH = 28f, GoPadX = 12f;

        public const float SlotMaxH = 40f, SlotPadX = 10f;

        /// <summary>Slot <paramref name="index"/> of <paramref name="count"/> under a box's label: 40 tall, less when more slots must fit.</summary>
        public static TableBox Slot(TableBox box, int index, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "1 or more.");
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index), index, "0 to " + (count - 1) + ".");
            TableBox inner = Inner(box);
            float top = inner.Y + LabelH + BoxGap;
            float h = Math.Min(SlotMaxH, (inner.Bottom - top - (count - 1) * BoxGap) / count);
            return new TableBox(inner.X, top + index * (h + BoxGap), inner.W, h);
        }

        /// <summary>Inside the hold button, centred: 「降りる」 (52 px on 60), the hint (18 px on 26) and the bar (240 × 8), 10 apart.</summary>
        public const float HoldWordLine = 60f, HoldHintLine = 26f, HoldBarW = 240f, HoldBarH = 8f, HoldGap = 10f;

        /// <summary>The hint or the reason the pack may not set out wraps to two lines at most.</summary>
        public const int HoldHintLines = 2;

        public static TableBox HoldWord => new TableBox(Hold.X, Hold.Y + (Hold.H - HoldStackH) / 2f, Hold.W, HoldWordLine);

        public static TableBox HoldHint => new TableBox(Inner(Hold).X, HoldWord.Bottom + HoldGap, Inner(Hold).W, HoldHintLine);

        public static TableBox HoldBar => new TableBox(Hold.X + (Hold.W - HoldBarW) / 2f, HoldHint.Bottom + HoldGap, HoldBarW, HoldBarH);

        private static float HoldStackH => HoldWordLine + HoldGap + HoldHintLine + HoldGap + HoldBarH;

        // ---- holding 「降りる」 ------------------------------------------------------------------------------------------

        /// <summary>
        /// How long 「降りる」 is held to set out (Enter or the pointer), so a stray press does not start a descent
        /// (battle-visual-v1 §4.10). Real time: the game's speed setting does not shorten it.
        /// </summary>
        public const float HoldMs = 1000f;

        /// <summary>How far the bar has filled after <paramref name="heldMs"/> of holding (0 to 1).</summary>
        public static float HoldProgress(double heldMs) => (float)Math.Max(0.0, Math.Min(1.0, heldMs / HoldMs));

        // ---- what the base reads off the lineage ---------------------------------------------------------------------

        /// <summary>The record of the explorer's descents so far.</summary>
        public static GateRecord RecordOf(Lineage lineage)
        {
            if (lineage == null) throw new ArgumentNullException(nameof(lineage));
            var r = new GateRecord { Descents = lineage.Lives.Count };
            foreach (LifeRecord life in lineage.Lives)
            {
                if (life.Layer.HasValue && (!r.Deepest.HasValue || life.Layer.Value > r.Deepest.Value)) r.Deepest = life.Layer.Value;
            }
            LifeRecord last = lineage.Lives.Count > 0 ? lineage.Lives[lineage.Lives.Count - 1] : null;
            if (last != null)
            {
                r.LastLayer = last.Layer;
                if (last.Ending.HasValue) r.LastLine = InheritanceText.EndingSentence(last.Ending.Value, last.Layer);
            }
            return r;
        }

        /// <summary>
        /// The seven layers: the two shared ones by their names (the carving read or not), the kings' rooms as 王の間, the
        /// regions' layers by their number alone; a reached layer shows its light, and the last descent's end is marked.
        /// </summary>
        public static IReadOnlyList<GateLayer> LayersOf(Lineage lineage)
        {
            if (lineage == null) throw new ArgumentNullException(nameof(lineage));
            GateRecord r = RecordOf(lineage);
            var rows = new List<GateLayer>();
            for (int layer = 1; layer <= Lair.LayerCount; layer++)
            {
                bool marked = r.LastLayer == layer;
                string name = layer < Lair.FirstRegionLayer ? Lair.LayerLabel(layer, lineage.Player.CarvingTaken)
                    : layer == Lair.KingLayer ? KingRooms : "";
                rows.Add(new GateLayer
                {
                    Number = layer,
                    Text = marked ? LastEndMark : name,
                    Reached = r.Deepest.HasValue && layer <= r.Deepest.Value,
                    Marked = marked,
                    King = layer == Lair.KingLayer,
                });
            }
            return rows;
        }

        // ---- the words the base adds (the canvas's; the rest are InheritanceCore's and the table's) ------------------

        /// <summary>The line beside the title.</summary>
        public const string Lead = "降り口の前で支度を整え、そのまま降ります。";

        /// <summary>The heading over the seven layers.</summary>
        public const string LayersHeading = "この下";

        public const string KingRooms = "王の間";

        /// <summary>The record's labels: the descents closed (HallView.PastLivesHeading's words), the deepest layer, the last one.</summary>
        public const string DescentsLabel = InheritanceText.PastDescentsHeading;

        public const string DeepestLabel = "いちばん深く";

        public const string LastLabel = "前の探索";

        /// <summary>What the layer the last descent ended on says.</summary>
        public const string LastEndMark = "前の探索はここまで";

        public const string DeckLabel = "束";
        public const string Reassemble = "組み直す";
        public const string EmptySlot = "空き";
        public const string Descend = "降りる";
        public const string HoldKey = "Enter";
        public const string HoldHintWords = "を押し続ける";

        /// <summary>「次の探索　7 回目」: the next descent and its count (HallView's words since #1031).</summary>
        public static string WhoLine(string heading, string count) =>
            string.IsNullOrEmpty(count) ? heading ?? "" : (heading ?? "") + "　" + count;

        /// <summary>「6 回」.</summary>
        public static string Times(int n) => N(n) + " 回";

        /// <summary>「第 3 層」.</summary>
        public static string LayerLine(int layer) => "第 " + N(layer) + " 層";

        /// <summary>「消耗品 2 / 3」 (the table's heading without its note: the box is narrow).</summary>
        public static string ConsumablesHeading(int carried, int slots) => "消耗品 " + N(carried) + " / " + N(slots);

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

        // ---- the boxes' line icons (the canvas's, 24 × 24, stroked 2.2 round) --------------------------------------

        /// <summary>The deck: two sheets, one behind the other.</summary>
        public const string DeckIcon = "M8.6 4h8.8a.6.6 0 0 1 .6.6v13.8a.6.6 0 0 1-.6.6H8.6a.6.6 0 0 1-.6-.6V4.6a.6.6 0 0 1 .6-.6zM5 8v11a1.6 1.6 0 0 0 1.6 1.6H14";

        /// <summary>The tools: a spanner.</summary>
        public const string ToolIcon = "M14 6a4 4 0 0 0 4.6 5.4L21 9l-2-.6-.6-2 2.4-2.4A4 4 0 0 0 14 6zM14.6 9.4 4 20";

        /// <summary>The consumables: a flask.</summary>
        public const string ConsumableIcon = "M9 3h6M10 3v4l-3 4v8a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2v-8l-3-4V3";

        // ---- the base's own text rules (the corner chips are meta.css's, MetaStyle) ---------------------------------

        /// <summary>
        /// The CSS of every text the base adds. DepartGateTests reads them all: 16 px or more, and the body colour (no
        /// colour of its own) under 24 px.
        /// </summary>
        public static class Css
        {
            public const string Title = "font: 800 44px/60px var(--font-display); letter-spacing: .24em; white-space: nowrap;";
            public const string Lead = "font: 400 18px/1.4 var(--font-body); white-space: nowrap;";
            public const string CalendarWords = "font: 800 24px/32px var(--font-display); letter-spacing: .12em; white-space: nowrap;";
            public const string CalendarDays = "font: 700 56px/56px var(--font-mono); white-space: nowrap;";
            public const string CalendarBar = "background: #06090c; border: 1px solid rgba(157,166,186,.5); border-radius: 3px;";
            public const string CalendarFill = "background: var(--ink-muted);";
            public const string CalendarFillFrozen = "background: var(--steel);";
            public const string CalendarTick = "background: rgba(6,9,12,.9);";
            public const string FrozenNote = "font: 400 16px/22px var(--font-body); white-space: nowrap;";
            public const string WhoLabel = "font: 700 18px/26px var(--font-body); white-space: nowrap;";
            public const string Name = "font: 800 64px/72px var(--font-display); letter-spacing: .14em; white-space: nowrap;";
            public const string RecordRule = "background: rgba(157,166,186,.4);";
            public const string RecordLabel = "font: 700 20px/30px var(--font-body); white-space: nowrap;";
            public const string RecordValue = "font: 400 20px/30px var(--font-body);";
            public const string RecordNumber = "font: 700 24px/30px var(--font-mono); white-space: nowrap;";
            public const string LayersHeading = "font: 700 18px/26px var(--font-body); white-space: nowrap;";
            public const string Layer = "background: var(--panel-solid); border: 1px dashed var(--steel); border-radius: var(--radius-panel);";
            public const string LayerReached = "border: 1px solid var(--ink);";
            public const string LayerMarked = "border: 2px solid var(--crimson);";
            public const string LayerKing = "border: 2px solid var(--boss); outline: 2px solid var(--boss); outline-offset: 3px;";
            public const string LayerNumber = "font: 700 24px/26px var(--font-mono); white-space: nowrap;";
            public const string LayerText = "font: 700 16px/22px var(--font-body); white-space: nowrap;";
            public const string Box = "background: var(--panel); border: 1px solid rgba(157,166,186,.4); border-radius: var(--radius-panel); transition: border-color .14s, background .14s;";
            public const string BoxHover = "border-color: var(--ink); background: var(--panel-solid);";
            public const string BoxLabel = "font: 700 16px/24px var(--font-body); white-space: nowrap;";
            public const string DeckCount = "font: 700 44px/46px var(--font-mono); color: var(--amber); white-space: nowrap;";
            public const string DeckUnit = "font: 700 24px/1.2 var(--font-body); color: var(--amber); white-space: nowrap;";
            public const string DeckRule = "font: 400 16px/1.2 var(--font-body); white-space: nowrap;";
            public const string Go = "padding: 4px 12px; border: 1px solid var(--steel); border-radius: 4px; font: 700 16px/20px var(--font-body); white-space: nowrap;";
            public const string Slot = "background: var(--panel-solid); border: 2px solid var(--amber); border-radius: var(--radius-panel);";
            public const string SlotEmpty = "background: transparent; border: 2px dashed var(--steel); border-radius: var(--radius-panel);";
            public const string SlotName = "font: 700 18px/1.2 var(--font-display); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;";
            public const string SlotEmptyWord = "font: 400 16px/1.2 var(--font-body); white-space: nowrap;";
            public const string Hold = "background: color-mix(in srgb, var(--amber) 16%, transparent); border: 2px solid var(--amber); border-radius: var(--radius-panel); transition: background .14s;";
            public const string HoldHover = "background: color-mix(in srgb, var(--amber) 24%, transparent);";
            public const string HoldBlocked = "background: var(--panel); border: 2px dashed var(--steel);";
            public const string HoldWord = "font: 800 52px/60px var(--font-display); letter-spacing: .3em; white-space: nowrap;";
            public const string HoldHint = "font: 400 18px/26px var(--font-body); text-align: center;";
            public const string HoldKey = "padding: 2px 8px; border: 1px solid var(--steel); border-radius: 4px; font: 600 16px/20px var(--font-mono); white-space: nowrap;";
            public const string HoldBar = "background: #06090c; border: 1px solid rgba(157,166,186,.5); border-radius: 4px;";
            public const string HoldFill = "background: var(--wick); border-radius: 3px;";
        }
    }
}
