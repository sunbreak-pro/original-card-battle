using System.Collections.Generic;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class GridTests
    {
        [Test]
        public void ParseTracks()
        {
            Assert.That(Grid.ParseTracks("24px auto"), Is.EqualTo(new[] { Track.Px(24f), Track.Auto }));
            Assert.That(Grid.ParseTracks("186px 1fr"), Is.EqualTo(new[] { Track.Px(186f), Track.Fr(1f) }));
            List<Track> rep = Grid.ParseTracks("repeat(3, minmax(0, 1fr))");
            Assert.That(rep.Count, Is.EqualTo(3));
            Assert.That(rep[0], Is.EqualTo(new Track(TrackSize.Px, 0f, TrackSize.Fr, 1f)));
            Assert.That(Grid.ParseTracks("28px 150px 1fr auto").Count, Is.EqualTo(4));
            Assert.That(Grid.ParseTracks("136px minmax(0, 1fr) auto")[1].ToString(), Is.EqualTo("minmax(0px, 1fr)"));
            Assert.That(Grid.ParseTracks("repeat(4, 1fr)").Count, Is.EqualTo(4));
        }

        [Test]
        public void FixedAndFr()
        {
            // explore.css:47 the band body: 186px 1fr, column-gap 16, in 460
            var cols = Grid.ParseTracks("186px 1fr");
            GridResult r = Grid.Solve(cols, 16f, 6f, new List<GridItem> { GridItem.Of(100f, 30f), GridItem.Of(200f, 26f) }, 460f);
            Assert.That((r.ColW[0], r.ColW[1]), Is.EqualTo((186f, 258f)));
            Assert.That(r.Items[1], Is.EqualTo(new RectF(202f, 0f, 258f, 30f)));
        }

        [Test]
        public void MinmaxZero_IgnoresTheContent_ButOneFrDoesNot()
        {
            var even = new GridSpec { Columns = Grid.ParseTracks("repeat(3, minmax(0, 1fr))"), ColumnGap = 12f, Width = 400f };
            GridResult r = Grid.Solve(even, new List<GridItem> { new GridItem { W = 300f, H = 112f, MinW = 300f }, GridItem.Of(10f, 112f), GridItem.Of(10f, 112f) });
            Assert.That(r.ColW[0], Is.EqualTo((400f - 24f) / 3f).Within(1e-3f));
            Assert.That(r.ColW[2], Is.EqualTo((400f - 24f) / 3f).Within(1e-3f));
            var plain = new GridSpec { Columns = Grid.ParseTracks("1fr 1fr"), Width = 400f };
            GridResult p = Grid.Solve(plain, new List<GridItem> { new GridItem { W = 300f, H = 20f, MinW = 300f }, GridItem.Of(10f, 20f) });
            Assert.That((p.ColW[0], p.ColW[1]), Is.EqualTo((300f, 100f)));
        }

        [Test]
        public void AutoColumns_TakeTheirContent_FrTheRest()
        {
            // explore.css:281 the taken rows: 22px 150px 1fr auto, column-gap 12
            var spec = new GridSpec { Columns = Grid.ParseTracks("22px 150px 1fr auto"), ColumnGap = 12f, Width = 600f, AlignItems = Align.Center };
            GridResult r = Grid.Solve(spec, new List<GridItem> { GridItem.Of(22f, 22f), GridItem.Of(120f, 26f), GridItem.Of(200f, 24f), GridItem.Of(60f, 22f) });
            Assert.That(r.ColW[3], Is.EqualTo(60f));
            Assert.That(r.ColW[2], Is.EqualTo(600f - 36f - 22f - 150f - 60f).Within(1e-3f));
            Assert.That(r.Items[0].Y, Is.EqualTo(2f)); // centred in the 26 px row
            // no fr: the auto column stretches over what is left (§12.8)
            var auto = new GridSpec { Columns = Grid.ParseTracks("24px auto"), ColumnGap = 4f, Width = 200f };
            Assert.That(Grid.Solve(auto, new List<GridItem> { GridItem.Of(24f, 24f), GridItem.Of(80f, 40f) }).ColW[1], Is.EqualTo(172f));
        }

        [Test]
        public void IntrinsicWidth_FitsTheContent()
        {
            // bundle.css:34 .ocb-val: grid 24px auto, column-gap 4, inside a flex row (its width is its content)
            var spec = new GridSpec { Columns = Grid.ParseTracks("24px auto"), ColumnGap = 4f, AlignItems = Align.Center };
            var items = new List<GridItem>
            {
                new GridItem { W = 24f, H = 24f, FixedW = true, FixedH = true },
                GridItem.Of(44f, 40f),
                new GridItem { W = 38f, H = 18f, ColumnStart = 2, MarginT = -2f },
            };
            GridResult r = Grid.Solve(spec, items);
            Assert.That(r.Width, Is.EqualTo(24f + 4f + 44f));
            Assert.That(r.Cells[2], Is.EqualTo((1, 1)));
            Assert.That(r.Items[0].Y, Is.EqualTo(8f));
            Assert.That(r.RowY[1], Is.EqualTo(40f));
        }

        [Test]
        public void IntrinsicWidth_FrBelowOne_CountsAsOne()
        {
            // §12.7.1 with indefinite space: the used flex fraction takes a track's max-content as is when its flex
            // factor is one or less (a 0.5fr track holding 80 px does not make the fr 160 px)
            var spec = new GridSpec { Columns = Grid.ParseTracks("0.5fr 0.5fr") };
            GridResult r = Grid.Solve(spec, new List<GridItem> { GridItem.Of(80f, 10f), GridItem.Of(20f, 10f) });
            Assert.That((r.ColW[0], r.ColW[1]), Is.EqualTo((80f, 40f)));
            var two = new GridSpec { Columns = Grid.ParseTracks("2fr 1fr") };
            GridResult t = Grid.Solve(two, new List<GridItem> { GridItem.Of(80f, 10f), GridItem.Of(20f, 10f) });
            Assert.That((t.ColW[0], t.ColW[1]), Is.EqualTo((80f, 40f)));
        }

        [Test]
        public void Placement_SpansAndAutoFlow()
        {
            var spec = new GridSpec { Columns = Grid.ParseTracks("1fr 1fr"), ColumnGap = 40f, RowGap = 14f, Width = 400f };
            var items = new List<GridItem>
            {
                GridItem.Of(50f, 20f),
                GridItem.Of(50f, 20f),
                new GridItem { W = 100f, H = 30f, ColumnStart = 1, ColumnEnd = -1 },
                GridItem.Of(50f, 20f),
            };
            GridResult r = Grid.Solve(spec, items);
            Assert.That(r.Cells, Is.EqualTo(new[] { (0, 0), (1, 0), (0, 1), (0, 2) }));
            Assert.That(r.Items[2].W, Is.EqualTo(400f));
            Assert.That(r.Items[3].Y, Is.EqualTo(20f + 14f + 30f + 14f));
            LayoutStyle l = Css.Layout("grid-column: 2 / 3");
            Assert.That((l.GridColumnStart, l.GridColumnEnd), Is.EqualTo(((int?)2, (int?)3)));
        }
    }
}
