using System.Collections.Generic;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class FlexTests
    {
        [Test]
        public void MarginLeftAuto_PushesTheTailToTheEnd()
        {
            // bundle.css:120 the stance slot: padding 0 12, flex centre gap 8, ×n at margin-left auto
            var spec = new FlexSpec { Padding = new Insets(2f, 14f, 2f, 14f), AlignItems = Align.Center, Width = 250f, Height = 52f }.SetGap(8f);
            var items = new List<FlexItem>
            {
                FlexItem.Of(24f, 24f),
                FlexItem.Of(100f, 26f),
                new FlexItem { W = 40f, H = 16f, AutoMarginL = true },
            };
            FlexResult r = Flex.Solve(spec, items);
            Assert.That(r.Items[0], Is.EqualTo(new RectF(14f, 14f, 24f, 24f)));
            Assert.That(r.Items[1].X, Is.EqualTo(46f));
            Assert.That(r.Items[2].X, Is.EqualTo(250f - 14f - 40f));
            Assert.That(r.Items[2].Y, Is.EqualTo(18f));
        }

        [Test]
        public void Wrap_StartsANewLine_WhenTheNextItemDoesNotFit()
        {
            // the chip rows: flex wrap gap 5
            var spec = new FlexSpec { Wrap = true, Width = 200f, AlignContent = AlignContent.Start }.SetGap(5f);
            var items = new List<FlexItem> { FlexItem.Of(60f, 30f), FlexItem.Of(70f, 30f), FlexItem.Of(50f, 30f), FlexItem.Of(80f, 30f), FlexItem.Of(100f, 30f) };
            FlexResult r = Flex.Solve(spec, items);
            Assert.That(r.Line, Is.EqualTo(new[] { 0, 0, 0, 1, 1 }));
            Assert.That(r.Items[2].X, Is.EqualTo(140f));
            Assert.That(r.Items[3], Is.EqualTo(new RectF(0f, 35f, 80f, 30f)));
            Assert.That(r.Items[4].X, Is.EqualTo(85f));
            Assert.That(r.Height, Is.EqualTo(65f));
        }

        [Test]
        public void Grow_AndShrinkStopsAtTheContent()
        {
            var spec = new FlexSpec { Width = 300f }.SetGap(10f);
            FlexResult grow = Flex.Solve(spec, new List<FlexItem> { FlexItem.Of(0f, 10f).Fill(), FlexItem.Of(0f, 10f).Fill() });
            Assert.That((grow.Items[0].W, grow.Items[1].X, grow.Items[1].W), Is.EqualTo((145f, 155f, 145f)));
            // flex: 1 1 0 with min-width auto: an item never goes below its content; the others share the rest
            var filters = new FlexSpec { Width = 300f };
            var f = new List<FlexItem>
            {
                new FlexItem { W = 50f, H = 48f, Grow = 1f, Basis = 0f },
                new FlexItem { W = 120f, H = 48f, Grow = 1f, Basis = 0f },
                new FlexItem { W = 60f, H = 48f, Grow = 1f, Basis = 0f },
            };
            FlexResult fr = Flex.Solve(filters, f);
            Assert.That(fr.Items[0].W, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(fr.Items[1].W, Is.EqualTo(120f).Within(1e-3f));
            Assert.That(fr.Items[2].W, Is.EqualTo(90f).Within(1e-3f));
            // shrink: an item with min-width: 0 gives up its width (the ellipsis case); the content ones keep theirs
            var narrow = new FlexSpec { Width = 200f };
            FlexResult sh = Flex.Solve(narrow, new List<FlexItem> { new FlexItem { W = 150f, H = 20f, MinMain = 0f }, FlexItem.Of(100f, 20f) });
            Assert.That((sh.Items[0].W, sh.Items[1].W), Is.EqualTo((100f, 100f)));
        }

        [Test]
        public void Justify_AndStretch()
        {
            var spec = new FlexSpec { Width = 100f, Height = 40f, Justify = Justify.SpaceBetween };
            FlexResult r = Flex.Solve(spec, new List<FlexItem> { FlexItem.Of(20f, 10f), FlexItem.Of(20f, 10f), FlexItem.Of(20f, 10f) });
            Assert.That((r.Items[0].X, r.Items[1].X, r.Items[2].X), Is.EqualTo((0f, 40f, 80f)));
            Assert.That(r.Items[0].H, Is.EqualTo(40f)); // stretch: no height given
            var centre = new FlexSpec { Width = 100f, Height = 40f, Justify = Justify.Center, AlignItems = Align.Center };
            FlexResult c = Flex.Solve(centre, new List<FlexItem> { new FlexItem { W = 30f, H = 10f, FixedCross = true } });
            Assert.That(c.Items[0], Is.EqualTo(new RectF(35f, 15f, 30f, 10f)));
            var end = new FlexSpec { Width = 100f, Justify = Justify.End };
            Assert.That(Flex.Solve(end, new List<FlexItem> { FlexItem.Of(30f, 10f) }).Items[0].X, Is.EqualTo(70f));
        }

        [Test]
        public void Baseline_AlignsTheFirstBaselines()
        {
            var spec = new FlexSpec { AlignItems = Align.Baseline }.SetGap(12f);
            var items = new List<FlexItem>
            {
                new FlexItem { W = 40f, H = 30f, Baseline = 24f },
                new FlexItem { W = 60f, H = 20f, Baseline = 14f },
            };
            FlexResult r = Flex.Solve(spec, items);
            Assert.That((r.Items[0].Y, r.Items[1].Y), Is.EqualTo((0f, 10f)));
            Assert.That(r.Height, Is.EqualTo(30f));
            Assert.That(r.Width, Is.EqualTo(112f)); // fit content
        }

        [Test]
        public void Column_WithGapAndMarginTopAuto()
        {
            var spec = new FlexSpec { Column = true, Height = 300f, Width = 100f, Padding = Insets.All(10f) }.SetGap(6f);
            var items = new List<FlexItem>
            {
                FlexItem.Of(80f, 40f),
                FlexItem.Of(80f, 30f),
                new FlexItem { W = 50f, H = 20f, AutoMarginT = true, FixedCross = true },
            };
            FlexResult r = Flex.Solve(spec, items);
            Assert.That((r.Items[0].Y, r.Items[1].Y), Is.EqualTo((10f, 56f)));
            Assert.That(r.Items[2].Y, Is.EqualTo(300f - 10f - 20f));
            Assert.That(r.Items[0].W, Is.EqualTo(80f)); // stretched to the inner width 80
            Assert.That(r.Items[2].W, Is.EqualTo(50f));
        }

        [Test]
        public void OrderAndReverse()
        {
            var spec = new FlexSpec { Width = 200f, Reverse = true };
            FlexResult r = Flex.Solve(spec, new List<FlexItem> { FlexItem.Of(20f, 10f), FlexItem.Of(30f, 10f) });
            Assert.That((r.Items[0].X, r.Items[1].X), Is.EqualTo((180f, 150f)));
            var ordered = new FlexSpec { Width = 200f };
            FlexResult o = Flex.Solve(ordered, new List<FlexItem> { new FlexItem { W = 20f, H = 10f, Order = 3 }, FlexItem.Of(30f, 10f) });
            Assert.That((o.Items[0].X, o.Items[1].X), Is.EqualTo((30f, 0f)));
        }

        [Test]
        public void WrapWithOneLine_AlignContentStart_KeepsTheLineAtItsItems()
        {
            // meta.css:226 the deck list: flex-wrap: wrap; height: 84px; align-content: flex-start. One line of chips
            // whose height comes from their text: the line is as tall as the chips, align-items stretches them to it only.
            var wrap = new FlexSpec { Wrap = true, Width = 300f, Height = 84f, AlignContent = AlignContent.Start }.SetGap(6f);
            FlexResult r = Flex.Solve(wrap, new List<FlexItem> { FlexItem.Of(60f, 24f), FlexItem.Of(40f, 20f) });
            Assert.That((r.Items[0].H, r.Items[1].H), Is.EqualTo((24f, 24f)));
            Assert.That((r.Items[0].Y, r.Items[1].Y), Is.EqualTo((0f, 0f)));
            // align-content: normal (stretch) gives the one line the whole height
            var stretch = new FlexSpec { Wrap = true, Width = 300f, Height = 84f };
            Assert.That(Flex.Solve(stretch, new List<FlexItem> { FlexItem.Of(60f, 24f) }).Items[0].H, Is.EqualTo(84f));
            // centre: the line sits in the middle
            var centre = new FlexSpec { Wrap = true, Width = 300f, Height = 84f, AlignContent = AlignContent.Center };
            Assert.That(Flex.Solve(centre, new List<FlexItem> { FlexItem.Of(60f, 24f) }).Items[0].Y, Is.EqualTo(30f));
            // nowrap is single-line: the line is the container whatever align-content says
            var single = new FlexSpec { Width = 300f, Height = 84f, AlignContent = AlignContent.Start };
            Assert.That(Flex.Solve(single, new List<FlexItem> { FlexItem.Of(60f, 24f) }).Items[0].H, Is.EqualTo(84f));
            // space-between with one line: flex-start (no division by zero)
            var between = new FlexSpec { Wrap = true, Width = 300f, Height = 84f, AlignContent = AlignContent.SpaceBetween, AlignItems = Align.Start };
            RectF b = Flex.Solve(between, new List<FlexItem> { FlexItem.Of(60f, 24f) }).Items[0];
            Assert.That((b.Y, b.H), Is.EqualTo((0f, 24f)));
        }

        [Test]
        public void NegativeFreeSpace_SpaceAroundAndEvenly_Centre()
        {
            // CSS Box Alignment: space-around / space-evenly fall back to center, space-between to flex-start
            var items = new List<FlexItem> { new FlexItem { W = 80f, H = 10f, Shrink = 0f }, new FlexItem { W = 80f, H = 10f, Shrink = 0f } };
            Assert.That(Flex.Solve(new FlexSpec { Width = 100f, Justify = Justify.SpaceAround }, items).Items[0].X, Is.EqualTo(-30f));
            Assert.That(Flex.Solve(new FlexSpec { Width = 100f, Justify = Justify.SpaceEvenly }, items).Items[0].X, Is.EqualTo(-30f));
            Assert.That(Flex.Solve(new FlexSpec { Width = 100f, Justify = Justify.SpaceBetween }, items).Items[0].X, Is.EqualTo(0f));
            Assert.That(Flex.Solve(new FlexSpec { Width = 100f, Justify = Justify.Center }, items).Items[0].X, Is.EqualTo(-30f));
            // align-content space-around with lines taller than the container: centred
            var lines = new FlexSpec { Wrap = true, Width = 100f, Height = 30f, AlignContent = AlignContent.SpaceAround };
            FlexResult r = Flex.Solve(lines, new List<FlexItem> { FlexItem.Of(80f, 20f), FlexItem.Of(80f, 20f) });
            Assert.That((r.Items[0].Y, r.Items[1].Y), Is.EqualTo((-5f, 15f)));
        }

        [Test]
        public void RowReverse_KeepsEachMarginOnItsOwnSide()
        {
            // row-reverse: main-start is the right edge; an item's margin-right is the one toward the start
            var spec = new FlexSpec { Width = 200f, Reverse = true };
            FlexResult r = Flex.Solve(spec, new List<FlexItem>
            {
                new FlexItem { W = 20f, H = 10f, MarginR = 10f },
                new FlexItem { W = 30f, H = 10f, MarginL = 5f },
            });
            Assert.That(r.Items[0].X, Is.EqualTo(200f - 10f - 20f));
            Assert.That(r.Items[1].X, Is.EqualTo(170f - 30f));
            // the free space goes to an auto margin on the side it is written: margin-right: auto (the main-start side
            // here) pushes the last item to the left end; margin-left: auto leaves it packed at the right, the space on its left
            FlexResult a = Flex.Solve(spec, new List<FlexItem> { FlexItem.Of(20f, 10f), new FlexItem { W = 30f, H = 10f, AutoMarginR = true } });
            Assert.That((a.Items[0].X, a.Items[1].X), Is.EqualTo((180f, 0f)));
            FlexResult l = Flex.Solve(spec, new List<FlexItem> { FlexItem.Of(20f, 10f), new FlexItem { W = 30f, H = 10f, AutoMarginL = true } });
            Assert.That((l.Items[0].X, l.Items[1].X), Is.EqualTo((180f, 150f)));
            // column-reverse with margin-bottom: the bottom margin leads
            var col = new FlexSpec { Column = true, Reverse = true, Width = 50f, Height = 100f };
            Assert.That(Flex.Solve(col, new List<FlexItem> { new FlexItem { W = 50f, H = 20f, MarginB = 7f } }).Items[0].Y, Is.EqualTo(100f - 7f - 20f));
        }

        [Test]
        public void LayoutStyle_BridgesToTheSolver()
        {
            LayoutStyle l = Css.Layout("display: flex; align-items: center; gap: 4px; flex-wrap: wrap");
            FlexSpec spec = l.ToFlexSpec(Insets.Of(0f, 8f), 200f, null);
            Assert.That((spec.AlignItems, spec.ColumnGap, spec.RowGap, spec.Wrap), Is.EqualTo((Align.Center, 4f, 4f, true)));
            LayoutStyle tail = Css.Layout("margin-left: auto; flex: none");
            FlexItem it = tail.ToFlexItem(40f, 20f, false);
            Assert.That((it.AutoMarginL, it.Grow, it.Shrink), Is.EqualTo((true, 0f, 0f)));
            FlexItem fill = Css.Layout("flex: 1; min-width: 0").ToFlexItem(10f, 10f, false);
            Assert.That((fill.Grow, fill.Shrink, fill.BasisPercent, fill.MinMain), Is.EqualTo((1f, 1f, (float?)0f, (float?)0f)));
            // min-width: auto is the content size, except for a scroll container (overflow hidden / auto), where it is 0
            Assert.That(Css.Layout("flex: 1").ToFlexItem(10f, 10f, false).MinMain, Is.Null);
            Assert.That(Css.Layout("flex: 1; overflow: hidden").ToFlexItem(10f, 10f, false).MinMain, Is.EqualTo((float?)0f));
            Assert.That(Css.Layout("flex: 1; overflow-y: auto").ToFlexItem(10f, 10f, true).MinMain, Is.EqualTo((float?)0f));
            Assert.That(Css.Layout("flex: 1; overflow-y: auto").ToFlexItem(10f, 10f, false).MinMain, Is.Null);
            Assert.That(Css.Layout("flex: 1; overflow: visible").ToFlexItem(10f, 10f, false).MinMain, Is.Null);
        }
    }
}
