// 出立's one screen, 「支度の卓」 (#1051): the boxes stay on the stage and apart, the small card's words come from the
// face, every tool and consumable has its line art, the table's texts are 16 px or more and in the body colour under
// 24 px, and one screen still builds a deck and a pack that set out.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonContent;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Bridge.Tests
{
    public sealed class DepartTableTests
    {
        private static readonly TableBox Stage = new TableBox(0f, 0f, DepartTable.StageW, DepartTable.StageH);
        private static readonly int Tools = ItemCatalogue.Tools.Count, Consumables = ItemCatalogue.Consumables.Count;

        private static IEnumerable<(string Name, TableBox Box)> Regions()
        {
            yield return ("header band", DepartTable.Band);
            yield return ("filters", DepartTable.FilterRow);
            for (int i = 0; i < DepartTable.PerPage; i++) yield return ("cell " + i, DepartTable.Cell(i));
            yield return ("page nav", DepartTable.PageNav);
            yield return ("shelf", DepartTable.Shelf);
            yield return ("pack", DepartTable.Pack);
        }

        private static void AssertApart(IList<(string Name, TableBox Box)> boxes)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    Assert.That(boxes[i].Box.Overlaps(boxes[j].Box), Is.False, boxes[i].Name + " " + boxes[i].Box + " overlaps " + boxes[j].Name + " " + boxes[j].Box);
                }
            }
        }

        [Test]
        public void TheRegions_StayOnTheStage_AndApart()
        {
            List<(string Name, TableBox Box)> all = Regions().ToList();
            foreach ((string name, TableBox box) in all) Assert.That(box.Inside(Stage), Is.True, name + " " + box);
            AssertApart(all);
        }

        [Test]
        public void TheBand_IsTheBasesWithTheTablesTag_AndTheChipBack()
        {
            Assert.That(DepartTable.Band, Is.EqualTo(HeaderBand.Band));
            HeaderContent c = DepartTable.BandOf(new DungeonCore.CalendarState(87), 20);
            Assert.That(c.Screen, Is.EqualTo(HeaderScreen.Table));
            Assert.That(c.Tags, Is.EqualTo(new[] { HeaderBand.TableTag }));
            Assert.That(c.BackChip, Is.True, "the chip back to the base stands in the band");
            Assert.That(c.OwnedCards, Is.EqualTo(HeaderButtonState.On), "the owned cards stay lit: this is their screen");
            Assert.That((c.Hp, c.MaxHp, c.Stamina, c.MaxStamina), Is.EqualTo((50, 50, 10, 10)));
            Assert.That((c.Calendar.DaysLeft, c.DeckCount), Is.EqualTo((87, 20)));
            HeaderContent gate = DepartGate.BandOf(new DungeonCore.CalendarState(87), 20);
            Assert.That((c.Calendar.DaysLeft, c.MaxHp, c.MaxStamina), Is.EqualTo((gate.Calendar.DaysLeft, gate.MaxHp, gate.MaxStamina)), "the base's values");
        }

        [Test]
        public void TheBonusTag_StandsAtTheToolHeadingsRight_LeavingTheHeadingItsRoom()
        {
            TableBox small = DepartTable.BonusTag(200f);
            Assert.That(small.Right, Is.EqualTo(DepartTable.ToolHead.Right).Within(0.01f));
            Assert.That((small.Y, small.H), Is.EqualTo((DepartTable.ToolHead.Y, DepartTable.ToolHead.H)));
            Assert.That(small.W, Is.EqualTo(200f + 2f * (DepartTable.BonusPadX + 1f)).Within(0.01f));
            TableBox wide = DepartTable.BonusTag(5000f);
            Assert.That(wide.W, Is.EqualTo(DepartTable.BonusMaxW).Within(0.01f), "a long line is cut, not run over the heading");
            Assert.That(wide.X, Is.EqualTo(DepartTable.ToolHead.X + DepartTable.BonusRoomLeft).Within(0.01f));
            Assert.That(wide.Inside(DepartTable.PackContent), Is.True);
        }

        [Test]
        public void TheGrid_IsSixByThree_FillingTheCardsWidth_AboveThePageTurns()
        {
            Assert.That(DepartTable.PerPage, Is.EqualTo(18));
            Assert.That(DepartTable.Cell(0).X, Is.EqualTo(DepartTable.CardsX));
            Assert.That(DepartTable.Cell(5).Right, Is.EqualTo(DepartTable.CardsX + DepartTable.CardsW).Within(0.01f));
            Assert.That(DepartTable.Cell(0).Y, Is.GreaterThan(DepartTable.FilterRow.Bottom));
            Assert.That(DepartTable.Cell(17).Bottom, Is.LessThan(DepartTable.PageNav.Y));
            Assert.That(DepartTable.PageNav.Bottom, Is.LessThan(DepartTable.Shelf.Y));
            Assert.That(DepartTable.Pack.X, Is.GreaterThan(DepartTable.CardsX + DepartTable.CardsW));
            Assert.Throws<ArgumentOutOfRangeException>(() => DepartTable.Cell(18));
        }

        [Test]
        public void TheSmallCardsParts_StandInTheCell_TopToBottom()
        {
            var cell = new TableBox(0f, 0f, DepartTable.CellW, DepartTable.CellH);
            TableBox[] parts = { DepartTable.CardTop, DepartTable.CardName, DepartTable.CardValue, DepartTable.CardFoot };
            foreach (TableBox p in parts) Assert.That(p.Inside(cell), Is.True, p.ToString());
            for (int i = 1; i < parts.Length; i++) Assert.That(parts[i].Y, Is.GreaterThanOrEqualTo(parts[i - 1].Bottom), parts[i].ToString());
            // − ×n ＋: two 44 px steps and the count between them
            Assert.That(DepartTable.CardFoot.W, Is.GreaterThanOrEqualTo(44f * 2f + 44f));
        }

        [Test]
        public void TheShelf_HoldsEveryToolAndConsumable_InOneRow_Apart()
        {
            Assert.That((Tools, Consumables), Is.EqualTo((8, 5)));
            var boxes = new List<(string, TableBox)>();
            for (int i = 0; i < Tools + Consumables; i++) boxes.Add(("item " + i, DepartTable.ShelfItem(i, Tools, Consumables)));
            boxes.Add(("divider", DepartTable.ShelfDivider(Tools, Consumables)));
            AssertApart(boxes);
            foreach ((string name, TableBox box) in boxes) Assert.That(box.Inside(DepartTable.ShelfRow), Is.True, name + " " + box);
            Assert.That(DepartTable.ShelfItem(Tools + Consumables - 1, Tools, Consumables).Right, Is.EqualTo(DepartTable.ShelfRow.Right).Within(0.01f));
            Assert.That(DepartTable.ShelfDivider(Tools, Consumables).X, Is.GreaterThan(DepartTable.ShelfItem(Tools - 1, Tools, Consumables).Right));
            Assert.That(DepartTable.ShelfDivider(Tools, Consumables).Right, Is.LessThan(DepartTable.ShelfItem(Tools, Tools, Consumables).X));
            Assert.That(DepartTable.ShelfHead.Bottom, Is.LessThanOrEqualTo(DepartTable.ShelfRow.Y));
            Assert.That(DepartTable.ShelfRow.Bottom, Is.LessThanOrEqualTo(DepartTable.Shelf.Bottom - 1f - DepartTable.ShelfPadY));
        }

        [Test]
        public void ThePack_StacksItsParts_InsideItself_WithoutOverlap()
        {
            var parts = new List<(string Name, TableBox Box)>
            {
                ("pile row", DepartTable.PileRow), ("tool heading", DepartTable.ToolHead), ("tool slots", DepartTable.ToolSlots),
                ("consumable heading", DepartTable.ConsumableHead), ("consumable slots", DepartTable.ConsumableSlots),
                ("reader", DepartTable.Reader), ("presets", DepartTable.Presets), ("status", DepartTable.Status), ("set out", DepartTable.SetOut),
            };
            foreach ((string name, TableBox box) in parts) Assert.That(box.Inside(DepartTable.PackContent), Is.True, name + " " + box);
            for (int i = 1; i < parts.Count; i++) Assert.That(parts[i].Box.Y, Is.GreaterThanOrEqualTo(parts[i - 1].Box.Bottom), parts[i].Name);
            Assert.That(DepartTable.SetOut.Bottom, Is.EqualTo(DepartTable.PackContent.Bottom).Within(0.01f));
            Assert.That(DepartTable.PackContent.Inside(DepartTable.Pack), Is.True);

            // beside the pile: 束 n 枚, the gauge, the deck's line, all in the pile row
            foreach (TableBox box in new[] { DepartTable.Pile, DepartTable.PileText, DepartTable.Gauge, DepartTable.DeckLine })
                Assert.That(box.Inside(DepartTable.PileRow), Is.True, box.ToString());
            Assert.That(DepartTable.Pile.Overlaps(DepartTable.PileText), Is.False);
            Assert.That(DepartTable.Gauge.Bottom, Is.LessThanOrEqualTo(DepartTable.DeckLine.Y));
        }

        [Test]
        public void TheReader_HoldsTheStartDiagram_UnderAToolsLines()
        {
            // 間合いの履: its name, two lines of its note, the start's heading and the lane
            float need = 34f + 8f + 2f * 30f + 12f + 22f + DepartTable.LaneH;
            Assert.That(DepartTable.ReaderContent.H, Is.GreaterThanOrEqualTo(need));
            Assert.That(DepartTable.ReaderContent.Inside(DepartTable.Reader), Is.True);
            // the widest lane: the start moved back as far as the rules let it
            var deck = new DepartureBuilder(DepartureCarry.Tools, DepartureCarry.Consumables, DeckLimits.Default);
            int widest = 0;
            for (int back = 0; back <= DeckRules.StartCellsBackMax + 1; back++)
                widest = Math.Max(widest, DepartTable.LaneCells(DeckRules.PlayerStartCell(back), DeckRules.StartGap(back)));
            Assert.That(widest * DepartTable.LaneCellW, Is.LessThanOrEqualTo(DepartTable.ReaderContent.W));
            Assert.That(DepartTable.LaneCells(deck.PlayerStartCell, deck.StartGap), Is.GreaterThanOrEqualTo(6));
        }

        [TestCase(3)]
        [TestCase(4)]
        public void TheSlots_ShareTheirRow(int count)
        {
            var boxes = new List<(string, TableBox)>();
            for (int i = 0; i < count; i++) boxes.Add(("slot " + i, DepartTable.Slot(DepartTable.ToolSlots, i, count)));
            AssertApart(boxes);
            foreach ((string name, TableBox box) in boxes) Assert.That(box.Inside(DepartTable.ToolSlots), Is.True, name);
            Assert.That(boxes[count - 1].Item2.Right, Is.EqualTo(DepartTable.ToolSlots.Right).Within(0.01f));
        }

        [Test]
        public void ThePresets_ShareTheirRow()
        {
            var boxes = Enumerable.Range(0, 3).Select(i => ("preset " + i, DepartTable.Preset(i))).ToList();
            AssertApart(boxes);
            foreach ((string name, TableBox box) in boxes) Assert.That(box.Inside(DepartTable.Presets), Is.True, name);
        }

        [Test]
        public void ThePile_GrowsWithTheDeck_UpToItsCap()
        {
            Assert.That(DepartTable.PileSheets(0, 40), Is.Zero);
            Assert.That(DepartTable.PileSheets(1, 40), Is.EqualTo(1));
            Assert.That(DepartTable.PileSheets(20, 40), Is.EqualTo(5));
            Assert.That(DepartTable.PileSheets(40, 40), Is.EqualTo(DepartTable.MaxSheets));
            Assert.That(DepartTable.PileSheets(45, 40), Is.EqualTo(DepartTable.MaxSheets), "a deck over the cap");
            Assert.That(DepartTable.PileSheets(45, 45), Is.EqualTo(DepartTable.MaxSheets));
            int last = 0;
            for (int n = 0; n <= 45; n++)
            {
                int s = DepartTable.PileSheets(n, 45);
                Assert.That(s, Is.GreaterThanOrEqualTo(last), n.ToString());
                last = s;
            }
            for (int k = 0; k < DepartTable.MaxSheets; k++) Assert.That(DepartTable.Sheet(k, DepartTable.MaxSheets).Inside(DepartTable.Pile), Is.True, k.ToString());
            Assert.That(DepartTable.Sheet(0, 5).Y, Is.GreaterThan(DepartTable.Sheet(4, 5).Y), "the sheets behind stand higher");
        }

        [Test]
        public void TheRows_LayTheirItems()
        {
            Assert.That(DepartTable.RowFrom(10f, 5f, 20f, 30f, 40f), Is.EqualTo(new[] { 10f, 35f, 70f }));
            Assert.That(DepartTable.RowTo(100f, 5f, 20f, 30f), Is.EqualTo(new[] { 45f, 70f }));
        }

        [Test]
        public void TheSmallCard_PrintsTheFacesCostReachNameAndFirstValue()
        {
            foreach (CardDef def in CardCatalog.All)
            {
                CardFace face = CoreText.FaceOf(def);
                SmallCard small = DepartTable.SmallCardOf(face);
                Assert.That(small.Name, Is.EqualTo(face.Name), def.Id);
                Assert.That(small.Cost, Is.EqualTo(face.PrintedCost != 0 ? face.PrintedCost : face.Cost), def.Id);
                Assert.That(small.Kind, Is.EqualTo(face.Kind), def.Id);
                Assert.That(small.Reach, Is.EqualTo(face.RequiredRangeGlyph.Length > 0 ? face.RequiredRangeGlyph : "自分"), def.Id);
                if (face.Values.Count > 0)
                {
                    Assert.That((small.Number, small.Word, small.ValueKind), Is.EqualTo((face.Values[0].Number, face.Values[0].Word, (CardValueKind?)face.Values[0].Kind)), def.Id);
                }
                else
                {
                    Assert.That(small.Number, Is.Empty, def.Id);
                    Assert.That(small.ValueKind, Is.Null, def.Id);
                    Assert.That(small.Word, Is.Not.Empty, def.Id + ": a card without a value prints its first row");
                }
            }
            SmallCard water = DepartTable.SmallCardOf(CoreText.FaceOf(CardCatalog.WaterStance));
            Assert.That(water.Word, Is.EqualTo(CoreText.FaceOf(CardCatalog.WaterStance).TextLines[0].Trim()));
        }

        [Test]
        public void EveryToolAndConsumable_HasLineArt_InsideItsBox()
        {
            foreach (ItemDef item in ItemCatalogue.Tools.Concat(ItemCatalogue.Consumables))
            {
                string d = DepartTable.LineArtOf(item.Id);
                Assert.That(d, Is.Not.Null.And.Not.Empty, item.Id);
                RectF b = PathData.Parse(d).ControlBounds();
                Assert.That(b.Left >= 0f && b.Top >= 0f && b.Right <= 24f && b.Bottom <= 24f, Is.True, item.Id + " " + b);
            }
            Assert.That(DepartTable.LineArtOf("no_such_item"), Is.Null);
        }

        private static readonly Regex FontSize = new Regex(@"(?:font:\s*(?:\d+\s+)?(?:italic\s+)?([\d.]+)px|font-size:\s*([\d.]+)px)", RegexOptions.CultureInvariant);

        [Test]
        public void TheTablesTexts_AreSixteenPxOrMore_InTheBodyColourUnderTwentyFour()
        {
            int texts = 0;
            foreach (FieldInfo f in typeof(DepartTable.Css).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
            {
                string css = (string)f.GetRawConstantValue();
                foreach (Match m in FontSize.Matches(css))
                {
                    texts++;
                    float px = float.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    Assert.That(px, Is.GreaterThanOrEqualTo(16f), f.Name);
                    if (px < 24f) Assert.That(Regex.IsMatch(css, @"(^|;)\s*color\s*:"), Is.False, f.Name + ": under 24 px the text keeps the body colour");
                }
            }
            Assert.That(texts, Is.GreaterThan(20), "the table's texts were read");
        }

        [Test]
        public void TheTablesWords_ComeFromTheCatalogueAndTheLimits()
        {
            Assert.That(DepartTable.ShelfLead, Is.EqualTo("左の 8 つが道具（探索の 4 つ、戦闘の 4 つ）、右の 5 つが消耗品です。押すと荷に入ります。"));
            Assert.That(DepartTable.RuleLine(null), Is.EqualTo("20〜40 枚、1 種 3 枚まで"));
            Assert.That(DepartTable.RuleLine(new DeckLimits(deckMax: 45, toolSlots: 4)), Is.EqualTo("20〜45 枚、1 種 3 枚まで"));
            Assert.That(DepartTable.ToolsHeading(2, 3), Is.EqualTo("道具 2 / 3"));
            Assert.That(DepartTable.ConsumablesHeading(0, 3), Is.EqualTo("消耗品 0 / 3（拠点へは持ち帰りません）"));
        }

        [Test]
        public void OneScreen_BuildsTheDeckAndThePack_AndSetsOut()
        {
            // the starter deck opens whole and legal; the pack fills its slots and refuses past them
            DeckBuilder deck = DeckBuilder.Starter().WithOwned(DeckBuilder.WithStarterKinds(CardCatalog.Starter.Select(d => d.Id)));
            Assert.That((deck.Total, deck.IsValid), Is.EqualTo((20, true)));
            DepartureBuilder pack = DepartureCarry.NewBuilder();
            foreach (string id in new[] { "maai_no_kutsu", "maai_no_kutsu", "hiyomi_no_suna" }) Assert.That(pack.AddTool(id), Is.True, id);
            Assert.That(pack.AddTool("ikitsugi_no_fue"), Is.False, "three tool slots");
            foreach (string id in new[] { "yakuso", "yakuso", "wasuremizu" }) Assert.That(pack.AddConsumable(id), Is.True, id);
            Assert.That(pack.AddConsumable("yakuso"), Is.False, "three consumable slots");
            Assert.That(pack.CanSetOut(deck), Is.True);

            // the second starter stance is refused with the reason the deck's line prints
            Assert.That(deck.Add(CardCatalog.IronWall.Id), Is.False);
            Assert.That(deck.RefusalOf(CardCatalog.IronWall.Id), Is.EqualTo("スタンスは 1 種だけ入れられます。「水の構え」を外すと入れられます"));

            Departure departure = pack.Build(deck);
            CarriedSupplies carry = DepartureCarry.Carry(departure, pack.Limits);
            Assert.That(departure.Deck.Count, Is.EqualTo(20));
            Assert.That(carry.Loadout.Tools.Count, Is.EqualTo(3));
            Assert.That(carry.ConsumableIds, Is.EqualTo(new[] { "yakuso", "yakuso", "wasuremizu" }));

            // a deck under the minimum keeps the pack from setting out
            var thin = new DeckBuilder(null);
            thin.Add(CardCatalog.KesaCut.Id);
            Assert.That(pack.CanSetOut(thin), Is.False);
        }
    }
}
