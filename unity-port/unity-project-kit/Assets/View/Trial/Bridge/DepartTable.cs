// 出立's one screen, 「支度の卓」 (#1051, the design canvas's 出立：支度の卓): where every box of the screen stands, the
// small card's few words, the shelf's line art and the words the screen adds. The two steps of the JS's 出立 (デッキを組む,
// 出立の支度) become one table: the owned cards on the left, the shelf of tools and consumables under them, the pack on the
// right (the deck as a pile, the tool and consumable slots, one reading panel, 出立する), the shared header band on top
// (#1074: the collapse calendar, HP and stamina, the chip back to the base).
//
// It lives here, engine-free, so `dotnet test` holds the layout (Trial.Bridge.Tests/DepartTableTests): nothing overlaps,
// everything stays on the 1920 × 1080 stage, every text is 16 px or more and every text under 24 px is in the body
// colour (battle-visual-v1 §3.1 / §2.3). Trial.Meta's DepartureScene draws what this says; the rules stay the
// builders' (DeckBuilder, DepartureBuilder).
using System;
using System.Collections.Generic;
using System.Globalization;
using BattleCore;
using Depiction;
using DungeonContent;

namespace Trial.Bridge
{
    /// <summary>A box in stage px (x right, y down), as the screen places it.</summary>
    public readonly struct TableBox : IEquatable<TableBox>
    {
        public TableBox(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float X { get; }
        public float Y { get; }
        public float W { get; }
        public float H { get; }
        public float Right => X + W;
        public float Bottom => Y + H;

        /// <summary>Whether the two boxes share any area (touching edges do not).</summary>
        public bool Overlaps(TableBox o) => X < o.Right - Eps && o.X < Right - Eps && Y < o.Bottom - Eps && o.Y < Bottom - Eps;

        /// <summary>Whether this box lies inside <paramref name="o"/> (edges included).</summary>
        public bool Inside(TableBox o) => X >= o.X - Eps && Y >= o.Y - Eps && Right <= o.Right + Eps && Bottom <= o.Bottom + Eps;

        private const float Eps = 0.01f;

        public bool Equals(TableBox o) => X == o.X && Y == o.Y && W == o.W && H == o.H;

        public override bool Equals(object obj) => obj is TableBox o && Equals(o);

        public override int GetHashCode() => (X, Y, W, H).GetHashCode();

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2} × {3})", X, Y, W, H);
    }

    /// <summary>What a small card on the table prints (#1051): cost, attribute, reach, name and one value, nothing else.</summary>
    public sealed class SmallCard
    {
        public string Name = "";

        /// <summary>The printed cost (before a discount, as the full card's circle).</summary>
        public int Cost;

        /// <summary>The face's kind: the frame colour and the attribute icon.</summary>
        public CardKind Kind;

        /// <summary>「0〜1」, or 「自分」 for a card that reads no reach (the full card's reach tag says the same).</summary>
        public string Reach = "";

        /// <summary>The first value column's number (「15」), or "" when the card has none.</summary>
        public string Number = "";

        /// <summary>The first value column's word (「威力」), or the card's first row of text when it has no value.</summary>
        public string Word = "";

        /// <summary>The first value column's kind (its number's colour); null without a value.</summary>
        public CardValueKind? ValueKind;
    }

    public static class DepartTable
    {
        // ---- the stage and its margins ------------------------------------------------------------------------------

        public const float StageW = 1920f, StageH = 1080f;

        /// <summary>The left and right margin of the table.</summary>
        public const float Margin = 48f;

        /// <summary>
        /// The shared header band over the top (#1074: 「拠点」 with 「支度の卓」 and the chip back, the calendar, HP, stamina
        /// and the buttons). Its height is HeaderBand's; nothing of the table reaches into it.
        /// </summary>
        public static TableBox Band => HeaderBand.Band;

        /// <summary>The table's band: the base's, with 「支度の卓」, the chip back, and the owned cards lit.</summary>
        public static HeaderContent BandOf(DungeonCore.CalendarState calendar, int deckCount) =>
            HeaderContent.ForTable(DepartGate.BandCalendar(calendar), DepartGate.StartMaxHp, DepartGate.StartMaxStamina, deckCount);

        // ---- left: the owned cards ----------------------------------------------------------------------------------

        public const float CardsX = Margin, CardsW = 1126f;

        /// <summary>The row of 持ち札 and the attribute and cost filters.</summary>
        public static readonly TableBox FilterRow = new TableBox(CardsX, 106f, CardsW, 44f);

        /// <summary>The space between two filter buttons.</summary>
        public const float FilterGap = 10f;

        // The grid keeps the JS's 6 × 3 page (departure.js:30), each card now drawn small instead of the full face at 72%.
        public const int GridColumns = 6, GridRows = 3;
        public const int PerPage = GridColumns * GridRows;
        public const float CellW = 176f, CellH = 200f, CellGap = 14f;
        public const float GridY = 164f;

        /// <summary>The page turns under the grid, with the page words and the keys.</summary>
        public static readonly TableBox PageNav = new TableBox(CardsX, 806f, CardsW, 44f);

        /// <summary>Grid cell <paramref name="index"/> of a page (0 at the top left, row by row).</summary>
        public static TableBox Cell(int index)
        {
            if (index < 0 || index >= PerPage) throw new ArgumentOutOfRangeException(nameof(index), index, "0 to " + (PerPage - 1) + ".");
            int col = index % GridColumns, row = index / GridColumns;
            return new TableBox(CardsX + col * (CellW + CellGap), GridY + row * (CellH + CellGap), CellW, CellH);
        }

        // The small card inside its cell (stage px from the cell's top left; the left border is the attribute's 6 px).
        public const float CardPadTop = 10f, CardPadRight = 10f, CardPadBottom = 10f, CardPadLeft = 12f;
        public const float CardBorderLeft = 6f, CardBorder = 1f, CardBorderIn = 2f;
        public const float CostSize = 34f, AttrIconSize = 24f;

        /// <summary>The cost circle, the attribute icon and the reach tag.</summary>
        public static readonly TableBox CardTop = new TableBox(CardBorderLeft + CardPadLeft, CardBorder + CardPadTop, CellW - CardBorderLeft - CardPadLeft - CardPadRight - CardBorder, CostSize);

        /// <summary>The name (20 px on a 26 px line).</summary>
        public static readonly TableBox CardName = new TableBox(CardTop.X, CardTop.Bottom + 6f, CardTop.W, 26f);

        /// <summary>The one value: its number (30 px) and word, or one row of the card's text.</summary>
        public static readonly TableBox CardValue = new TableBox(CardTop.X, CardName.Bottom + 6f, CardTop.W, 32f);

        /// <summary>The − ×n ＋ row at the bottom (the steps are 44 × 36).</summary>
        public static readonly TableBox CardFoot = new TableBox(CardTop.X, CellH - CardBorder - CardPadBottom - 36f, CardTop.W, 36f);

        // ---- left, below: the shelf of tools and consumables -------------------------------------------------------

        public static readonly TableBox Shelf = new TableBox(CardsX, 869f, CardsW, 183f);

        public const float ShelfPadX = 12f, ShelfPadY = 14f;

        /// <summary>The heading row of the shelf (品の棚 and what to do with it).</summary>
        public static readonly TableBox ShelfHead = new TableBox(Shelf.X + 1f + ShelfPadX, Shelf.Y + 1f + ShelfPadY, Shelf.W - 2f - 2f * ShelfPadX, 32f);

        /// <summary>One thing on the shelf: the socket (62) for its art and its name under it in two lines at most.</summary>
        public const float ItemW = 84f, SocketSize = 62f, ItemNameGap = 6f, ItemNameLine = 20f, ItemNameLines = 2f;

        public const float ItemH = SocketSize + ItemNameGap + ItemNameLine * ItemNameLines;

        public const float ShelfDividerW = 1f;

        /// <summary>The row the things stand in, under the heading.</summary>
        public static readonly TableBox ShelfRow = new TableBox(ShelfHead.X, ShelfHead.Bottom + 12f, ShelfHead.W, ItemH);

        /// <summary>
        /// Thing <paramref name="index"/> of the shelf: the tools first, then a 1 px divider, then the consumables, spread
        /// over the row with equal gaps (the canvas's space-between).
        /// </summary>
        public static TableBox ShelfItem(int index, int tools, int consumables)
        {
            int n = tools + consumables;
            if (index < 0 || index >= n) throw new ArgumentOutOfRangeException(nameof(index), index, "0 to " + (n - 1) + ".");
            float step = ItemW + ShelfGap(tools, consumables);
            float x = index < tools ? index * step : tools * step + ShelfDividerW + ShelfGap(tools, consumables) + (index - tools) * step;
            return new TableBox(ShelfRow.X + x, ShelfRow.Y, ItemW, ItemH);
        }

        /// <summary>The divider between the tools and the consumables.</summary>
        public static TableBox ShelfDivider(int tools, int consumables) =>
            new TableBox(ShelfRow.X + tools * (ItemW + ShelfGap(tools, consumables)), ShelfRow.Y, ShelfDividerW, SocketSize);

        private static float ShelfGap(int tools, int consumables)
        {
            int n = tools + consumables;
            return n == 0 ? 0f : (ShelfRow.W - n * ItemW - ShelfDividerW) / n;
        }

        // ---- right: the pack ----------------------------------------------------------------------------------------

        public static readonly TableBox Pack = new TableBox(1206f, 106f, 666f, 946f);

        public const float PackPadX = 24f, PackPadY = 20f, PackGap = 16f;

        /// <summary>The pack's content box (its border 1 and padding in).</summary>
        public static readonly TableBox PackContent = new TableBox(Pack.X + 1f + PackPadX, Pack.Y + 1f + PackPadY, Pack.W - 2f - 2f * PackPadX, Pack.H - 2f - 2f * PackPadY);

        /// <summary>The deck as a pile: the pile on the left, how many and the gauge and the deck's line on the right.</summary>
        public static readonly TableBox PileRow = new TableBox(PackContent.X, PackContent.Y, PackContent.W, 176f);

        /// <summary>The pile of sheets (its thickness is the count: <see cref="PileSheets"/>).</summary>
        public static readonly TableBox Pile = new TableBox(PileRow.X, PileRow.Y + (PileRow.H - 150f) / 2f, 112f, 150f);

        /// <summary>One sheet of the pile.</summary>
        public const float SheetW = 92f, SheetH = 130f;

        /// <summary>The most sheets the pile draws (the deck at its cap).</summary>
        public const int MaxSheets = 9;

        /// <summary>The column beside the pile: 束 n 枚 and the rule, the gauge, then the deck's line (three lines at most).</summary>
        public static readonly TableBox PileText = new TableBox(Pile.Right + 22f, PileRow.Y, PileRow.Right - Pile.Right - 22f, PileRow.H);

        public const float TotalLine = 64f, GaugeH = 16f, DeckLineLine = 26f, DeckLineLines = 3f;

        public static readonly TableBox Gauge = new TableBox(PileText.X, PileText.Y + TotalLine + 8f, PileText.W, GaugeH);

        /// <summary>The deck's line: its status, or why a card was refused, or why a saved deck opened over the cap.</summary>
        public static readonly TableBox DeckLine = new TableBox(PileText.X, Gauge.Bottom + 10f, PileText.W, DeckLineLine * DeckLineLines);

        public static readonly TableBox ToolHead = new TableBox(PackContent.X, PileRow.Bottom + PackGap, PackContent.W, 26f);

        /// <summary>
        /// The survivor's bonus (what the life's limits raise) at the right end of the tool heading's row, since the header
        /// it stood in became the shared band (#1074). Room is kept for the longest heading, 「道具 4 / 4」, and a gap.
        /// </summary>
        public const float BonusRoomLeft = 100f, BonusPadX = 10f, BonusDot = 10f, BonusDotGap = 8f, BonusWordsGap = 8f;

        /// <summary>The widest the bonus tag may stand (its words cut with an ellipsis past it).</summary>
        public static float BonusMaxW => ToolHead.W - BonusRoomLeft;

        /// <summary>The bonus tag for its content width (the dot, the label, the line), right-aligned on the row, at most <see cref="BonusMaxW"/>.</summary>
        public static TableBox BonusTag(float contentW)
        {
            float w = Math.Min(BonusMaxW, contentW + 2f * (BonusPadX + 1f));
            return new TableBox(ToolHead.Right - w, ToolHead.Y, w, ToolHead.H);
        }
        public static readonly TableBox ToolSlots = new TableBox(PackContent.X, ToolHead.Bottom + 8f, PackContent.W, 60f);
        public static readonly TableBox ConsumableHead = new TableBox(PackContent.X, ToolSlots.Bottom + PackGap, PackContent.W, 26f);
        public static readonly TableBox ConsumableSlots = new TableBox(PackContent.X, ConsumableHead.Bottom + 8f, PackContent.W, 60f);

        // From the bottom up: 出立する, the status line, the three preset decks.
        public static readonly TableBox SetOut = new TableBox(PackContent.X, PackContent.Bottom - 80f, PackContent.W, 80f);
        public static readonly TableBox Status = new TableBox(PackContent.X, SetOut.Y - PackGap - 29f, PackContent.W, 29f);
        public static readonly TableBox Presets = new TableBox(PackContent.X, Status.Y - PackGap - 48f, PackContent.W, 48f);
        public const float PresetGap = 12f;

        /// <summary>The one reading panel for whatever is under the pointer (a card or a thing), between the slots and the presets.</summary>
        public static readonly TableBox Reader = new TableBox(PackContent.X, ConsumableSlots.Bottom + PackGap, PackContent.W, Presets.Y - PackGap - ConsumableSlots.Bottom - PackGap);

        public const float ReaderPadX = 16f, ReaderPadY = 14f;

        /// <summary>The reading panel's content box.</summary>
        public static readonly TableBox ReaderContent = new TableBox(Reader.X + 1f + ReaderPadX, Reader.Y + 1f + ReaderPadY, Reader.W - 2f - 2f * ReaderPadX, Reader.H - 2f - 2f * ReaderPadY);

        // The start-position diagram (the JS's 開始の位置, departure.js:616): floor tiles 88 apart on a lane 112 tall.
        public const float LaneCellW = 88f, LaneH = 112f;

        /// <summary>The floor cells the diagram draws: at least six, and always the enemy's.</summary>
        public static int LaneCells(int playerCell, int gap) => Math.Max(6, playerCell + gap + 1);

        /// <summary>Preset button <paramref name="index"/> of three.</summary>
        public static TableBox Preset(int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index), index, "0 to 2.");
            float w = (Presets.W - 2f * PresetGap) / 3f;
            return new TableBox(Presets.X + index * (w + PresetGap), Presets.Y, w, Presets.H);
        }

        /// <summary>Slot <paramref name="index"/> of a row of <paramref name="count"/> (3, or 4 with the survivor's bonus), 10 apart.</summary>
        public static TableBox Slot(TableBox row, int index, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "1 or more.");
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index), index, "0 to " + (count - 1) + ".");
            const float gap = 10f;
            float w = (row.W - gap * (count - 1)) / count;
            return new TableBox(row.X + index * (w + gap), row.Y, w, row.H);
        }

        /// <summary>
        /// How many sheets the pile shows for <paramref name="total"/> cards of a deck capped at <paramref name="cap"/>: none
        /// for an empty deck, then one more for every ninth of the cap, <see cref="MaxSheets"/> at the cap and past it.
        /// </summary>
        public static int PileSheets(int total, int cap)
        {
            if (total <= 0) return 0;
            if (cap < 1) cap = 1;
            int n = (int)Math.Ceiling(total * (double)MaxSheets / cap);
            return Math.Max(1, Math.Min(MaxSheets, n));
        }

        /// <summary>
        /// Sheet <paramref name="k"/> of <paramref name="sheets"/> inside <see cref="Pile"/> (0 is the front one, at the
        /// bottom left): every sheet behind sits 2 px right and 2.5 px up, so the full pile spans the box.
        /// </summary>
        public static TableBox Sheet(int k, int sheets)
        {
            if (k < 0 || k >= Math.Max(1, sheets)) throw new ArgumentOutOfRangeException(nameof(k), k, "0 to " + (sheets - 1) + ".");
            float stepX = (Pile.W - SheetW) / (MaxSheets - 1);
            float stepY = (Pile.H - SheetH) / (MaxSheets - 1);
            return new TableBox(Pile.X + k * stepX, Pile.Y + Pile.H - SheetH - k * stepY, SheetW, SheetH);
        }

        /// <summary>A row of items laid from <paramref name="x"/> rightwards with <paramref name="gap"/> between: each one's x.</summary>
        public static float[] RowFrom(float x, float gap, params float[] widths)
        {
            var xs = new float[widths?.Length ?? 0];
            for (int i = 0; i < xs.Length; i++)
            {
                xs[i] = x;
                x += widths[i] + gap;
            }
            return xs;
        }

        /// <summary>A row of items laid leftwards so the last one ends at <paramref name="right"/>: each one's x, in the given order.</summary>
        public static float[] RowTo(float right, float gap, params float[] widths)
        {
            var xs = new float[widths?.Length ?? 0];
            for (int i = xs.Length - 1; i >= 0; i--)
            {
                xs[i] = right - widths[i];
                right = xs[i] - gap;
            }
            return xs;
        }

        // ---- the small card's words ---------------------------------------------------------------------------------

        /// <summary>The small card of a face: cost, kind, reach, name and the first value (or its first row of text).</summary>
        public static SmallCard SmallCardOf(CardFace face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            var card = new SmallCard
            {
                Name = face.Name ?? "",
                Cost = face.PrintedCost != 0 ? face.PrintedCost : face.Cost,
                Kind = face.Kind,
                Reach = string.IsNullOrEmpty(face.RequiredRangeGlyph) ? SelfReach : face.RequiredRangeGlyph,
            };
            CardValue first = face.Values != null && face.Values.Count > 0 ? face.Values[0] : null;
            if (first != null)
            {
                card.Number = first.Number ?? "";
                card.Word = first.Word ?? "";
                card.ValueKind = first.Kind;
            }
            else
            {
                card.Word = FirstRow(face);
            }
            return card;
        }

        /// <summary>The reach tag of a card that reads no reach (the full card's 「自分」).</summary>
        public const string SelfReach = "自分";

        // The face's first printed row: its TextLines, else its Description's first sentence (MetaCardFace.DescriptionRows).
        private static string FirstRow(CardFace face)
        {
            if (face.TextLines != null)
            {
                foreach (string row in face.TextLines)
                {
                    if (!string.IsNullOrWhiteSpace(row)) return row.Trim();
                }
            }
            foreach (string row in (face.Description ?? "").Replace("。", "。\n").Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(row)) return row.Trim();
            }
            return "";
        }

        // ---- the words the table adds (the canvas's; every number comes from the limits or the catalogue) ---------

        public const string CardsHeading = "持ち札";
        public const string ShelfHeading = "品の棚";
        public const string PileHeading = "束";

        /// <summary>
        /// The preset that opens the starter deck (#1038). The JS's 「試作デッキ」 (MetaText.Prototype) named the prototype
        /// deck, which the C# no longer hands a first expedition.
        /// </summary>
        public const string StarterPreset = "最初期の束";

        /// <summary>「左の 8 つが道具（探索の 4 つ、戦闘の 4 つ）、右の 5 つが消耗品です。押すと荷に入ります。」 from the catalogue.</summary>
        public static string ShelfLead
        {
            get
            {
                int explore = 0, battle = 0;
                foreach (ItemDef item in ItemCatalogue.Tools)
                {
                    if (item.Side == EffectSide.Battle) battle++;
                    else explore++;
                }
                return "左の " + N(ItemCatalogue.Tools.Count) + " つが道具（探索の " + N(explore) + " つ、戦闘の " + N(battle) + " つ）、右の "
                    + N(ItemCatalogue.Consumables.Count) + " つが消耗品です。押すと荷に入ります。";
            }
        }

        /// <summary>「20〜40 枚、1 種 3 枚まで」 for the life's limits.</summary>
        public static string RuleLine(DeckLimits limits)
        {
            DeckLimits l = DeckLimits.From(limits);
            return N(l.DeckMin) + "〜" + N(l.DeckMax) + " 枚、1 種 " + N(Constants.CopiesMax) + " 枚まで";
        }

        /// <summary>「道具 2 / 3」.</summary>
        public static string ToolsHeading(int carried, int slots) => "道具 " + N(carried) + " / " + N(slots);

        /// <summary>「消耗品 2 / 3（拠点へは持ち帰りません）」.</summary>
        public static string ConsumablesHeading(int carried, int slots) => "消耗品 " + N(carried) + " / " + N(slots) + "（拠点へは持ち帰りません）";

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

        // ---- the shelf's line art (the canvas's, 24 × 24, stroked 2.2 round) until the painted art comes -------------

        private static readonly Dictionary<string, string> Art = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kaerimichi_no_ito"] = "M5 19c4 0 3-6 7-6s3-6 7-6M6.6 19a1.6 1.6 0 1 1-3.2 0a1.6 1.6 0 1 1 3.2 0zM20.6 7a1.6 1.6 0 1 1-3.2 0a1.6 1.6 0 1 1 3.2 0z",
            ["hiyomi_no_suna"] = "M7 4h10M7 20h10M8 4c0 4 4 5 4 8s-4 4-4 8M16 4c0 4-4 5-4 8s4 4 4 8",
            ["utsushi_no_tobari"] = "M4 5c3-1 6-1 8 1 2-2 5-2 8-1v13c-3-1-6-1-8 1-2-2-5-2-8-1zM12 6v13",
            ["toshin_no_kakera"] = "M12 3c1 3 4 5 4 9a4 4 0 0 1-8 0c0-1.6.8-2.8 1.6-3.8.2 1.2.8 1.8 1.4 2 .2-2.4-.2-4.8 1-7.2zM9.5 20.5h5",
            ["maai_no_kutsu"] = "M4 17h9l7-2v3H4zM6 17V9l5 1 2 7",
            ["ikitsugi_no_fue"] = "M4 16 16 4l4 4L8 20zM13 7l4 4M10 10l4 4",
            ["ryurin_no_kakera"] = "M12 3 5 9l2 9 5 3 5-3 2-9zM12 3v18M5 9l7 3 7-3",
            ["ryugo_no_fu"] = "M8.6 3h6.8a1.6 1.6 0 0 1 1.6 1.6v14.8a1.6 1.6 0 0 1-1.6 1.6H8.6A1.6 1.6 0 0 1 7 19.4V4.6A1.6 1.6 0 0 1 8.6 3zM10 8h4M10 12h4M10 16h2",
            ["hayagake_no_gan"] = "M13 14a4 4 0 1 1-8 0a4 4 0 1 1 8 0zM19 9a3 3 0 1 1-6 0a3 3 0 1 1 6 0z",
            ["yakuso"] = "M12 21V10M12 14c-4 0-6-3-6-7 4 0 6 3 6 7zM12 12c0-4 2-6 6-6 0 4-2 6-6 6z",
            ["koi_hoshiniku"] = "M5 8c4-3 10-3 14 0v8c-4 3-10 3-14 0zM9 9v6M14 9v6",
            ["wasuremizu"] = "M9 3h6M10 3v4l-3 4v8a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2v-8l-3-4V3",
            ["shirube_no_ishi"] = "M12 3 7 13h10zM9 13l-2 8h10l-2-8",
        };

        /// <summary>The line art of a tool or consumable (a 24 × 24 path), or null for an id without one.</summary>
        public static string LineArtOf(string id) => id != null && Art.TryGetValue(id, out string d) ? d : null;

        // ---- the table's own text rules (the other texts are meta.css's, MetaStyle and DepartureScene) -------------

        /// <summary>
        /// The CSS of every text the table adds. DepartTableTests reads them all: 16 px or more, and the body colour
        /// (no colour of its own) under 24 px.
        /// </summary>
        public static class Css
        {
            public const string Heading = "font: 800 26px/1.2 var(--font-display); white-space: nowrap;";
            public const string ShelfHeading = "font: 800 24px/32px var(--font-display); white-space: nowrap;";
            public const string ShelfLead = "font: 400 16px/1.4 var(--font-body); white-space: nowrap;";
            public const string Filter = "height: 44px; padding: 0 16px; display: inline-flex; align-items: center; justify-content: center; gap: 8px; background: var(--panel-solid); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel); font: 700 18px/1 var(--font-body); white-space: nowrap; transition: border-color .14s, background .14s;";
            public const string FilterHover = "border-color: var(--ink);";
            public const string FilterOn = "border: var(--stroke-frame) solid var(--amber); background: color-mix(in srgb, var(--amber) 16%, var(--panel-solid));";
            public const string CostDot = "width: 26px; height: 26px; display: inline-flex; align-items: center; justify-content: center; border: var(--stroke-frame) solid var(--amber); border-radius: 50%; font: 700 16px/1 var(--font-mono); font-style: normal;";
            public const string PageText = "font: 400 18px/1 var(--font-body); white-space: nowrap;";
            public const string Keys = "font: 400 16px/22px var(--font-body); letter-spacing: .06em; white-space: nowrap;";
            public const string Card = "background: var(--card); border: 1px solid var(--rule); border-left-width: 6px; border-radius: var(--radius-panel); transition: transform .14s ease-out, box-shadow .14s;";
            public const string CardIn = "border-top-width: 2px; border-right-width: 2px; border-bottom-width: 2px; box-shadow: 0 6px 14px rgba(0,0,0,.45);";
            public const string CardHover = "transform: translateY(-4px);";
            public const string CardSel = "box-shadow: 0 0 0 3px var(--amber), 0 0 20px color-mix(in srgb, var(--amber) 40%, transparent);";
            public const string CardUnowned = "opacity: .5;";
            public const string CardCost = "width: 34px; height: 34px; display: inline-flex; align-items: center; justify-content: center; border: var(--stroke-frame) solid var(--amber); border-radius: 50%; font: 700 20px/1 var(--font-mono);";
            public const string CardReach = "padding: 1px 6px; border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-tag); font: 600 16px/1.2 var(--font-mono); white-space: nowrap;";
            public const string CardName = "font: 700 20px/26px var(--font-display); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;";
            public const string CardNumber = "font: 700 30px/32px var(--font-mono); white-space: nowrap;";
            public const string CardWord = "font: 400 16px/32px var(--font-body); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;";
            public const string CardCount = "font: 700 26px/1 var(--font-mono); text-align: center; white-space: nowrap;";
            public const string Unowned = "padding: 4px 14px; border: var(--stroke-rule) dashed var(--steel); border-radius: var(--radius-tag); font: 700 16px/1.2 var(--font-body); white-space: nowrap;";
            public const string Shelf = "background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel);";
            public const string Socket = "background: var(--panel-solid); border: var(--stroke-rule) solid var(--steel); border-radius: 50%; transition: border-color .14s, box-shadow .14s;";
            public const string SocketHover = "border-color: var(--ink);";
            public const string SocketIn = "border: 3px solid var(--amber);";
            public const string ItemName = "font: 400 16px/20px var(--font-body); text-align: center;";
            public const string ItemCount = "font: 700 16px/1.2 var(--font-mono); white-space: nowrap;";
            public const string Pack = "background: var(--panel); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel);";
            public const string Sheet = "background: var(--card); border: 1.5px solid var(--steel); border-radius: var(--radius-panel);";
            public const string SheetFront = "background: var(--card); border: var(--stroke-frame) solid var(--steel); border-radius: var(--radius-panel);";
            public const string SheetFrontOk = "border-color: var(--amber);";
            public const string PileTotal = "font: 700 64px/64px var(--font-mono); color: var(--amber); white-space: nowrap;";
            public const string PileUnit = "font: 700 24px/1.2 var(--font-body); color: var(--amber); white-space: nowrap;";
            public const string PileRule = "font: 400 18px/1.2 var(--font-body); white-space: nowrap;";
            public const string Gauge = "background: #06090c; border: var(--stroke-rule) solid var(--steel); border-radius: 3px;";
            public const string GaugeFill = "background: var(--steel); border-radius: 2px; transition: width .26s ease-out, background .2s;";
            public const string GaugeFillOk = "background: var(--amber);";
            public const string GaugeMin = "background: var(--ink);";
            public const string DeckLine = "font: 400 18px/26px var(--font-body);";
            public const string SlotHead = "font: 700 18px/26px var(--font-body); white-space: nowrap;";
            public const string Bonus = "background: color-mix(in srgb, var(--wick) 10%, var(--panel)); border: var(--stroke-rule) solid var(--wick); border-radius: var(--radius-tag);";
            public const string BonusDot = "background: var(--wick); border-radius: 50%; box-shadow: 0 0 8px var(--wick);";
            public const string BonusLabel = "font: 700 16px/22px var(--font-body); white-space: nowrap;";
            public const string BonusLine = "font: 400 16px/22px var(--font-body); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;";
            public const string Slot = "background: transparent; border: var(--stroke-frame) dashed var(--steel); border-radius: var(--radius-panel); transition: border-color .14s, background .14s;";
            public const string SlotFull = "border-style: solid; border-color: var(--amber); background: var(--panel-solid);";
            public const string SlotFullHover = "background: color-mix(in srgb, var(--amber) 12%, var(--panel-solid));";
            public const string SlotBonus = "border-color: var(--wick);";
            public const string SlotNo = "width: 30px; height: 30px; display: inline-flex; align-items: center; justify-content: center; border: var(--stroke-rule) solid var(--steel); border-radius: 50%; font: 700 16px/1 var(--font-mono);";
            public const string SlotName = "font: 700 20px/1.2 var(--font-display); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;";
            public const string SlotEmpty = "font: 400 18px/1.2 var(--font-body); white-space: nowrap;";
            public const string Reader = "background: var(--panel-solid); border: var(--stroke-rule) solid var(--rule); border-radius: var(--radius-panel);";
            public const string ReaderName = "font: 800 26px/34px var(--font-display); white-space: nowrap;";
            public const string ReaderReading = "font: 400 16px/1.2 var(--font-body); white-space: nowrap;";
            public const string ReaderSide = "padding: 3px 10px; border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-tag); font: 700 16px/1.2 var(--font-body); white-space: nowrap;";
            public const string ReaderText = "font: 400 20px/30px var(--font-body);";
            public const string ReaderLine = "font: 400 18px/27px var(--font-body);";
            public const string ReaderFlavour = "font: 400 18px/27px var(--font-display);";
            public const string ReaderStart = "font: 700 16px/22px var(--font-body); white-space: nowrap;";
        }
    }
}
