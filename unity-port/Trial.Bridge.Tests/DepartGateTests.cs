// The base as one scene, 「降り口の前」 (#1052): the regions stay on the stage, apart and under the shared header band
// (#1074), the band shows the calendar and the strength a descent sets out with, the foot's boxes hold their slots, the
// hold to set out fills in its length, the record and the seven layers read the lineage, and every text the base adds
// is 16 px or more and in the body colour under 24 px.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    public sealed class DepartGateTests
    {
        private static readonly TableBox Stage = new TableBox(0f, 0f, DepartGate.StageW, DepartGate.StageH);

        private static IEnumerable<(string Name, TableBox Box)> Regions()
        {
            yield return ("header band", DepartGate.Band);
            yield return ("way down", DepartGate.ArchOuter);
            yield return ("explorer", DepartGate.Explorer);
            yield return ("who", DepartGate.WhoLabel);
            yield return ("name", DepartGate.Name);
            yield return ("record", DepartGate.Record);
            yield return ("layers heading", DepartGate.LayersHead);
            for (int layer = 1; layer <= Lair.LayerCount; layer++) yield return ("layer " + layer, DepartGate.Layer(layer));
            for (int i = 0; i < 3; i++) yield return ("box " + i, DepartGate.Box(i));
            yield return ("hold", DepartGate.Hold);
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
            Assert.That(DepartGate.Band, Is.EqualTo(HeaderBand.Band), "the band's height is HeaderBand's, not the base's own");
        }

        [Test]
        public void TheWayDown_StandsInTheMiddle_OnTheFloor_WithTheExplorerBeforeIt()
        {
            Assert.That(DepartGate.Arch.X + DepartGate.Arch.W / 2f, Is.EqualTo(DepartGate.StageW / 2f + 60f).Within(0.01f), "the arch's centre");
            Assert.That(DepartGate.Arch.Bottom, Is.GreaterThanOrEqualTo(DepartGate.Floor.Y), "the arch reaches the floor");
            Assert.That(DepartGate.Explorer.Right, Is.LessThan(DepartGate.ArchOuter.X), "the explorer stands before it, on its left");
            Assert.That(DepartGate.ExplorerFoot.Y + DepartGate.ExplorerFoot.H / 2f, Is.EqualTo(DepartGate.Floor.Y).Within(0.01f), "the feet are on the floor line");
            Assert.That(DepartGate.FloorLight.Y, Is.GreaterThanOrEqualTo(DepartGate.Floor.Y));
            Assert.That(DepartGate.FloorLight.Bottom, Is.LessThan(DepartGate.Foot.Y));
            var arch = new TableBox(0f, 0f, DepartGate.Arch.W, DepartGate.Arch.H);
            foreach ((float x, float bottom, float w, float h, float a) in DepartGate.RockBands)
            {
                Assert.That(new TableBox(x, DepartGate.Arch.H - bottom - h, w, h).Inside(arch), Is.True, "a band of rock " + bottom);
                Assert.That(a, Is.InRange(0f, 1f));
            }
            Assert.That(DepartGate.Band.Bottom, Is.LessThan(DepartGate.ArchOuter.Y), "the way down stands under the band");
        }

        [Test]
        public void TheFoot_LaysThreeBoxesAndTheHold_AcrossTheStage()
        {
            Assert.That(DepartGate.Box(0).X, Is.EqualTo(DepartGate.Margin));
            Assert.That(DepartGate.Hold.Right, Is.EqualTo(DepartGate.StageW - DepartGate.Margin).Within(0.01f));
            Assert.That(DepartGate.Hold.W, Is.EqualTo(DepartGate.Box(0).W * DepartGate.HoldFlex).Within(0.01f));
            Assert.That(DepartGate.Box(1).X - DepartGate.Box(0).Right, Is.EqualTo(DepartGate.FootGap).Within(0.01f));
            foreach (TableBox part in new[] { DepartGate.HoldWord, DepartGate.HoldHint, DepartGate.HoldBar })
                Assert.That(part.Inside(DepartGate.Hold), Is.True, part.ToString());
            Assert.That(DepartGate.HoldHint.Y, Is.GreaterThanOrEqualTo(DepartGate.HoldWord.Bottom));
            Assert.That(DepartGate.HoldBar.Y, Is.GreaterThanOrEqualTo(DepartGate.HoldHint.Bottom));
            Assert.That(DepartGate.Layer(Lair.LayerCount).Bottom, Is.LessThan(DepartGate.Foot.Y));
            Assert.That(DepartGate.Record.Bottom, Is.LessThan(DepartGate.Foot.Y));
            Assert.That(DepartGate.Record.Y, Is.GreaterThanOrEqualTo(DepartGate.RecordRule.Bottom));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DepartGate.Box(3));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DepartGate.Layer(0));
        }

        [TestCase(3)]
        [TestCase(4)]
        public void TheSlots_StackInTheirBox_UnderTheLabel(int count)
        {
            TableBox box = DepartGate.Box(1);
            var slots = new List<(string, TableBox)>();
            for (int i = 0; i < count; i++) slots.Add(("slot " + i, DepartGate.Slot(box, i, count)));
            AssertApart(slots);
            foreach ((string name, TableBox slot) in slots)
            {
                Assert.That(slot.Inside(DepartGate.Inner(box)), Is.True, name + " " + slot);
                Assert.That(slot.H, Is.GreaterThanOrEqualTo(30f), name + ": an 18 px name still fits");
            }
            Assert.That(slots[0].Item2.Y, Is.GreaterThanOrEqualTo(DepartGate.Inner(box).Y + DepartGate.LabelH));
            if (count == 3) Assert.That(slots[0].Item2.H, Is.EqualTo(DepartGate.SlotMaxH), "three slots keep the canvas's 40");
        }

        [Test]
        public void TheHold_FillsInItsLength()
        {
            Assert.That(DepartGate.HoldMs, Is.EqualTo(1000f));
            Assert.That(DepartGate.HoldProgress(0), Is.Zero);
            Assert.That(DepartGate.HoldProgress(-5), Is.Zero);
            Assert.That(DepartGate.HoldProgress(500), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(DepartGate.HoldProgress(DepartGate.HoldMs), Is.EqualTo(1f));
            Assert.That(DepartGate.HoldProgress(5000), Is.EqualTo(1f));
        }

        [Test]
        public void TheBand_ShowsTheCalendar_AndTheStrengthADescentSetsOutWith()
        {
            HeaderContent c = DepartGate.BandOf(new CalendarState(87), 22);
            Assert.That(c.Screen, Is.EqualTo(HeaderScreen.Base));
            Assert.That(c.PlaceName, Is.EqualTo(HeaderBand.BaseName));
            Assert.That((c.Calendar.DaysLeft, c.Calendar.Frozen, c.Calendar.ShownNote), Is.EqualTo((87, false, "")));
            Assert.That((c.Hp, c.MaxHp), Is.EqualTo((50, 50)), "a descent sets out at full HP");
            Assert.That((c.Stamina, c.MaxStamina), Is.EqualTo((10, 10)), "and at full stamina");
            Assert.That((DepartGate.StartMaxHp, DepartGate.StartMaxStamina), Is.EqualTo((global::Exploration.RunStart.DefaultMaxHp, StaminaRules.BaseMaxStamina)));
            Assert.That(c.DeckCount, Is.EqualTo(22));
            Assert.That(c.OwnedCards, Is.EqualTo(HeaderButtonState.Ready), "the owned cards open the table");
            Assert.That(c.BackChip, Is.False);

            Assert.That(DepartGate.BandCalendar(null).DaysLeft, Is.EqualTo(CalendarState.Fresh.DaysLeft), "no calendar reads as fresh");
        }

        [Test]
        public void TheBand_SaysTheCalendarStandsStill_WhileAKingIsDown()
        {
            var frozen = new CalendarState(41, new Dictionary<Region, int> { [Region.Dragon] = 12 });
            Assert.That(frozen.IsFrozen, Is.True);
            HeaderCalendar cal = DepartGate.BandCalendar(frozen);
            Assert.That((cal.DaysLeft, cal.Frozen, cal.ShownNote), Is.EqualTo((41, true, DepartGate.CalendarFrozenNote)));
        }

        private static Lineage WithLives(params (int Layer, LifeEnding Ending)[] lives)
        {
            Lineage lineage = Inheritance.NewLineage(7, carryOver: true);
            var records = new List<LifeRecord>();
            for (int i = 0; i < lives.Length; i++)
            {
                LifeRecord life = Inheritance.StartLife(lineage).Life;
                records.Add(new LifeRecord(i + 1, life.Name, life.Tier, life.Seed, false, null, false, null, lives[i].Ending, lives[i].Layer, null, null, null, null, null));
            }
            return lineage.WithLives(records);
        }

        [Test]
        public void TheRecord_CountsTheDescents_TheDeepest_AndHowTheLastEnded()
        {
            GateRecord none = DepartGate.RecordOf(Inheritance.NewLineage(7, carryOver: true));
            Assert.That((none.Descents, none.Deepest, none.LastLayer, none.LastLine), Is.EqualTo((0, (int?)null, (int?)null, "")));

            GateRecord r = DepartGate.RecordOf(WithLives((2, LifeEnding.Fallen), (4, LifeEnding.Survived), (3, LifeEnding.Fallen)));
            Assert.That(r.Descents, Is.EqualTo(3));
            Assert.That(r.Deepest, Is.EqualTo(4));
            Assert.That(r.LastLayer, Is.EqualTo(3));
            Assert.That(r.LastLine, Is.EqualTo("第 3 層の戦いで倒れた。"));
            Assert.That(DepartGate.Times(r.Descents), Is.EqualTo("3 回"));
            Assert.That(DepartGate.LayerLine(4), Is.EqualTo("第 4 層"));
        }

        [Test]
        public void TheSevenLayers_LightWhatWasReached_AndMarkWhereTheLastDescentEnded()
        {
            IReadOnlyList<GateLayer> first = DepartGate.LayersOf(Inheritance.NewLineage(7, carryOver: true));
            Assert.That(first.Select(l => l.Number), Is.EqualTo(Enumerable.Range(1, 7)));
            Assert.That(first.Any(l => l.Reached || l.Marked), Is.False, "before the first descent nothing is lit or marked");
            Assert.That(first[0].Text, Is.EqualTo(Lair.LayerLabel(1, false)));
            Assert.That(first[1].Text, Is.EqualTo(Lair.LayerLabel(2, false)), "the sacrament's name as the carving leaves it");
            Assert.That(first.Skip(2).Take(4).All(l => l.Text.Length == 0), Is.True, "the regions' layers show their number alone");
            Assert.That((first[6].Text, first[6].King), Is.EqualTo((DepartGate.KingRooms, true)));

            IReadOnlyList<GateLayer> after = DepartGate.LayersOf(WithLives((2, LifeEnding.Fallen), (4, LifeEnding.Survived), (3, LifeEnding.Fallen)));
            Assert.That(after.Where(l => l.Reached).Select(l => l.Number), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(after.Single(l => l.Marked).Number, Is.EqualTo(3));
            Assert.That(after[2].Text, Is.EqualTo(DepartGate.LastEndMark));
            Assert.That(after.Where(l => l.King).Select(l => l.Number), Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void TheWords_FollowTheCarryOversWords()
        {
            Assert.That(DepartGate.DescentsLabel, Is.EqualTo("これまでの探索"));
            Assert.That(DepartGate.WhoLine(InheritanceText.NextDescentHeading, InheritanceText.DescentLabel(7)), Is.EqualTo("次の探索　7 回目"));
            Assert.That(DepartGate.WhoLine("次の探索", ""), Is.EqualTo("次の探索"));
            Assert.That(DepartGate.ConsumablesHeading(2, 3), Is.EqualTo("消耗品 2 / 3"));
        }

        [Test]
        public void TheBoxesIcons_StayInTheirBox()
        {
            foreach (string d in new[] { DepartGate.DeckIcon, DepartGate.ToolIcon, DepartGate.ConsumableIcon })
            {
                Trial.Geometry.RectF b = Trial.Geometry.PathData.Parse(d).ControlBounds();
                Assert.That(b.Left >= 0f && b.Top >= 0f && b.Right <= 24f && b.Bottom <= 24f, Is.True, d + " " + b);
            }
        }

        private static readonly Regex FontSize = new Regex(@"(?:font:\s*(?:\d+\s+)?(?:italic\s+)?([\d.]+)px|font-size:\s*([\d.]+)px)", RegexOptions.CultureInvariant);

        [Test]
        public void TheBasesTexts_AreSixteenPxOrMore_InTheBodyColourUnderTwentyFour()
        {
            int texts = 0;
            foreach (FieldInfo f in typeof(DepartGate.Css).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
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
            Assert.That(texts, Is.GreaterThan(15), "the base's texts were read");
        }

        [Test]
        public void Amber_StaysOnThePlayersSide()
        {
            // the deck's count, the filled slots and the hold to set out; nothing of the layers or the record
            string[] amber = typeof(DepartGate.Css).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && ((string)f.GetRawConstantValue()).Contains("--amber"))
                .Select(f => f.Name).OrderBy(n => n).ToArray();
            Assert.That(amber, Is.EqualTo(new[] { "DeckCount", "DeckUnit", "Hold", "HoldHover", "Slot" }));
        }
    }
}
