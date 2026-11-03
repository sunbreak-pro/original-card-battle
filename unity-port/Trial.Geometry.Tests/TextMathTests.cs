using System;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class TextMathTests
    {
        [TestCase("700 22px/28px var(--font-display)", Family.Display, 700, 22f, 28f)]
        [TestCase("400 18px/1.4 var(--font-body)", Family.Body, 400, 18f, 25.2f)]
        [TestCase("600 20px/1 var(--font-mono)", Family.Mono, 600, 20f, 20f)]
        [TestCase("800 64px/72px var(--font-display)", Family.Display, 800, 64f, 72f)]
        [TestCase("700 36px/1.2 var(--font-display)", Family.Display, 700, 36f, 43.2f)]
        [TestCase("400 46px/1 var(--font-display)", Family.Display, 400, 46f, 46f)]
        [TestCase("12px/1.5 var(--font-mono)", Family.Mono, 400, 12f, 18f)]
        [TestCase("400 14px/1.45 var(--font-body)", Family.Body, 400, 14f, 20.3f)]
        [TestCase("bold 16px/20px \"Zen Kaku Gothic New\", sans-serif", Family.Body, 700, 16f, 20f)]
        public void ParseFont_Shorthands(string css, Family family, int weight, float size, float lh)
        {
            FontSpec f = TextMath.ParseFont(css);
            Assert.That((f.Family, f.Weight, f.Size), Is.EqualTo((family, weight, size)));
            Assert.That(f.LineHeightPx, Is.EqualTo(lh).Within(1e-4f));
        }

        [Test]
        public void ParseFont_NormalLineHeight_IsTheContentArea()
        {
            FontSpec f = TextMath.ParseFont("700 16px var(--font-body)");
            Assert.That(f.LineHeightPx, Is.EqualTo(1.448f * 16f).Within(1e-4f));
            FontSpec m = TextMath.ParseFont("600 20px var(--font-mono)");
            Assert.That(m.LineHeightPx, Is.EqualTo(1.3f * 20f).Within(1e-4f));
            Assert.That(TextMath.TryParseFont("italic 700 16px/20px serif", out FontSpec it, out bool italic), Is.True);
            Assert.That((it.Family, italic), Is.EqualTo((Family.Display, true)));
            Assert.Throws<FormatException>(() => TextMath.ParseFont("inherit"));
        }

        [Test]
        public void TmpLineSpacing_FromCssPitch()
        {
            // kit.md §4.9: body 16/22 → −7.3; display 22/28 → −17.5; mono 48/52 → −21.7
            Assert.That(TextMath.TmpLineSpacing(TextMath.ParseFont("400 16px/22px var(--font-body)")), Is.EqualTo(-7.3f).Within(0.01f));
            Assert.That(TextMath.TmpLineSpacing(TextMath.ParseFont("700 22px/28px var(--font-display)")), Is.EqualTo(-17.53f).Within(0.01f));
            Assert.That(TextMath.TmpLineSpacing(TextMath.ParseFont("700 48px/52px var(--font-mono)")), Is.EqualTo(-21.67f).Within(0.01f));
            Assert.That(TextMath.TmpLineSpacing(22f, 16f, 1.16f, -0.288f), Is.EqualTo(TextMath.TmpLineSpacing(22f, 16f, 1.16f, 0.288f)));
        }

        [Test]
        public void Baseline_HalfLeadingModel()
        {
            FontSpec body = TextMath.ParseFont("400 16px/22px var(--font-body)");
            Assert.That(TextMath.HalfLeading(body), Is.EqualTo(-0.584f).Within(1e-3f));
            Assert.That(TextMath.CssBaselineFromTop(body), Is.EqualTo(-0.584f + 18.56f).Within(1e-3f));
            Assert.That(TextMath.CssBaselineFromTop(28f, 22f, 1.16f, 0.288f), Is.EqualTo((28f - 1.448f * 22f) / 2f + 1.16f * 22f).Within(1e-4f));
        }

        // TMP's height test for a clipped label: the glyph boxes of n lines (pitch L) against the box less the margins.
        private static bool TmpKeepsLines(FontSpec f, int lines, float boxH, float room, float tallestEm = 0f)
        {
            FontMetrics m = f.Metrics;
            float glyphs = (lines - 1) * f.LineHeightPx + Math.Max(tallestEm, m.ContentEm) * f.Size;
            float marginH = boxH - TextMath.HalfLeading(f) + room;
            return glyphs <= marginH + 1e-4f;
        }

        [Test]
        public void ClipRoom_CardDescriptionModeC()
        {
            // Ocb.Card 'c' mode at 16 px: body 16/22 in a 22 px box; the glyphs (23.168) overflow the box less the
            // half-leading (22.584), so TMP's Ellipsis dropped the line
            FontSpec f = TextMath.ParseFont("400 16px/22px var(--font-body)");
            Assert.That(TmpKeepsLines(f, 1, 22f, 0f), Is.False);
            float room = TextMath.ClipRoomPx(f.LineHeightPx, f.Size, f.Metrics.ContentEm);
            Assert.That(room, Is.EqualTo(23.168f - 22.584f + 0.05f * 16f).Within(1e-3f));
            Assert.That(TmpKeepsLines(f, 1, 22f, room), Is.True);
            Assert.That(TmpKeepsLines(f, 2, 22f, room), Is.False, "the room must not fit a second line");
        }

        [Test]
        public void ClipRoom_KitGalleryEllipsisSample()
        {
            // KitGallery's sample: Shippori 700 22/28 in a 28 px box; glyphs 31.856 against 29.928
            FontSpec f = TextMath.ParseFont("700 22px/28px var(--font-display)");
            Assert.That(TmpKeepsLines(f, 1, 28f, 0f), Is.False);
            float room = TextMath.ClipRoomPx(f.LineHeightPx, f.Size, f.Metrics.ContentEm);
            Assert.That(room, Is.EqualTo(31.856f - 29.928f + 0.05f * 22f).Within(1e-3f));
            Assert.That(TmpKeepsLines(f, 1, 28f, room), Is.True);
            Assert.That(TmpKeepsLines(f, 2, 28f, room), Is.False);
        }

        [Test]
        public void ClipRoom_LineClampKeepsItsLines()
        {
            // -webkit-line-clamp: 2 on body 16/22 (box 44): both lines show, the third still ellipsises
            FontSpec f = TextMath.ParseFont("400 16px/22px var(--font-body)");
            float room = TextMath.ClipRoomPx(f.LineHeightPx, f.Size, f.Metrics.ContentEm);
            Assert.That(TmpKeepsLines(f, 2, 44f, 0f), Is.False);
            Assert.That(TmpKeepsLines(f, 2, 44f, room), Is.True);
            Assert.That(TmpKeepsLines(f, 3, 44f, room), Is.False);
        }

        [Test]
        public void ClipRoom_CoversTheFallbackFace()
        {
            // 「×n」 (Plex 600 16/1): Plex alone needs (20.8 − 16)/2, a Zen Kaku fallback glyph (1.448 em) more
            FontSpec f = TextMath.ParseFont("600 16px/1 var(--font-mono)");
            float own = TextMath.ClipRoomPx(f.LineHeightPx, f.Size, f.Metrics.ContentEm);
            Assert.That(own, Is.EqualTo(2.4f + 0.8f).Within(1e-3f));
            float withFallback = TextMath.ClipRoomPx(f.LineHeightPx, f.Size, f.Metrics.ContentEm, 1.448f);
            Assert.That(TmpKeepsLines(f, 1, 16f, own, 1.448f), Is.False);
            Assert.That(TmpKeepsLines(f, 1, 16f, withFallback, 1.448f), Is.True);
            Assert.That(TmpKeepsLines(f, 2, 16f, withFallback), Is.False);
            // a fallback shorter than the own face changes nothing
            Assert.That(TextMath.ClipRoomPx(f.LineHeightPx, f.Size, f.Metrics.ContentEm, 1f), Is.EqualTo(own));
        }

        [TestCase("400 16px/24px var(--font-body)")]
        [TestCase("400 15px/22px var(--font-body)")] // the 'c' mode once it shrinks to 15 px
        [TestCase("700 22px/32px var(--font-display)")]
        [TestCase("600 20px/30px var(--font-mono)")]
        [TestCase("400 16px var(--font-body)")] // line-height normal: the glyph box is the line box
        public void ClipRoom_IsZeroWhenTheLineFits(string css)
        {
            FontSpec f = TextMath.ParseFont(css);
            Assert.That(TextMath.ClipRoomPx(f.LineHeightPx, f.Size, f.Metrics.ContentEm), Is.EqualTo(0f));
            Assert.That(TextMath.ClipRoomPx(f.LineHeightPx, 0f, f.Metrics.ContentEm), Is.EqualTo(0f));
        }

        [Test]
        public void LetterSpacing_IsHundredthsOfAnEm()
        {
            Assert.That(TextMath.TmpCharacterSpacing(0.02f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(TextMath.TmpCharacterSpacing(-0.04f), Is.EqualTo(-4f).Within(1e-5f));
            Assert.That(TextMath.TmpCharacterSpacingPx(2.2f, 22f), Is.EqualTo(10f).Within(1e-4f));
        }

        private static void Near(Vec2 actual, Vec2 expected, float eps = 1e-3f) =>
            Assert.That(Math.Abs(actual.X - expected.X) <= eps && Math.Abs(actual.Y - expected.Y) <= eps, Is.True, "got " + actual.X + ", " + actual.Y);

        [Test]
        public void VerticalPunctuation_IsTheFontsVertForm()
        {
            // the tagline (meta.css:48, 400 46px Shippori Mincho; 'だが、技と記憶は次の生へ。'): ShipporiMincho-Regular's
            // uni3001 (59, −69)…(296, 155) → uniFE11 (707, 592)…(944, 816); uni3002 (62, −60) → uniFE12 (677, 560)
            Near(TextMath.VerticalPunctuationOffset('、', 46f), new Vec2(0.648f * 46f, -0.661f * 46f));
            Near(TextMath.VerticalPunctuationOffset('。', 46f), new Vec2(0.615f * 46f, -0.620f * 46f));
            // Zen Kaku Gothic New Regular: uni3001 (77, −47) → uniFE11 (692, 575)
            Near(TextMath.Vertical('、', Family.Body).OffsetEm, new Vec2(0.615f, -0.622f));
            Near(TextMath.Vertical('、', Family.Display, 800).OffsetEm, new Vec2(0.657f, -0.699f));
            Near(TextMath.Vertical('っ').OffsetEm, new Vec2(0.119f, -0.138f));
            Near(TextMath.Vertical('ッ', Family.Mono, 600).OffsetEm, new Vec2(0.11f, -0.11f));
            Assert.That(TextMath.Vertical('ー').RotateDeg, Is.EqualTo(90f));
            Assert.That(TextMath.Vertical('「').RotateDeg, Is.EqualTo(90f));
            Assert.That(TextMath.Vertical('「').OffsetEm, Is.EqualTo(Vec2.Zero));
            // Zen Kaku's … sits on the baseline: turned, it also moves to the column's middle
            VerticalGlyph dots = TextMath.Vertical('…', Family.Body);
            Assert.That(dots.RotateDeg, Is.EqualTo(90f));
            Near(dots.OffsetEm, new Vec2(0.335f, 0.039f));
            Assert.That(TextMath.Vertical('…').OffsetEm, Is.EqualTo(Vec2.Zero));
            Assert.That(TextMath.Vertical('継').RotateDeg, Is.EqualTo(0f));
            Assert.That(TextMath.VerticalPunctuationOffset('継', 46f), Is.EqualTo(Vec2.Zero));
        }

        [Test]
        public void Metrics_PerFamily()
        {
            Assert.That(TextMath.Metrics(Family.Display, 800).ContentEm, Is.EqualTo(1.448f).Within(1e-5f));
            Assert.That(TextMath.Metrics(Family.Mono, 700).UnderlinePosEm, Is.EqualTo(-0.1f));
            Assert.That(TextMath.Metrics(Family.Body, 400).UnderlinePosEm, Is.EqualTo(-0.133f));
            // IBMPlexMono-Regular.ttf: underline −140 / 60 (not the SemiBold's −112 / 88); weights pick faces as CSS does
            Assert.That(TextMath.Metrics(Family.Mono, 400).UnderlinePosEm, Is.EqualTo(-0.14f));
            Assert.That(TextMath.Metrics(Family.Mono, 400).UnderlineThicknessEm, Is.EqualTo(0.06f));
            Assert.That(TextMath.Metrics(Family.Mono, 500).UnderlinePosEm, Is.EqualTo(-0.14f));
            Assert.That(TextMath.Metrics(Family.Mono, 600).UnderlinePosEm, Is.EqualTo(-0.112f));
            Assert.That(TextMath.Metrics(Family.Mono, 650).UnderlinePosEm, Is.EqualTo(-0.1f));
            Assert.That(TextMath.Metrics(Family.Mono, 400).ContentEm, Is.EqualTo(1.3f).Within(1e-5f));
        }

        [Test]
        public void TextStyle_InheritsAndMerges()
        {
            TextStyleData root = Css.Text("font-family: var(--font-body); color: var(--ink); line-height: 1.375");
            TextStyleData card = Css.Text("font: 700 22px/28px var(--font-display); letter-spacing: .02em; white-space: nowrap").Inherit(root);
            Assert.That(card.Font, Is.EqualTo(new FontSpec(Family.Display, 700, 22f, 28f)));
            Assert.That(card.Color, Is.EqualTo(DsTokens.Ink));
            Assert.That(card.LetterSpacingEmResolved, Is.EqualTo(0.02f));
            TextStyleData child = new TextStyleData { Size = 11f }.Inherit(card);
            // letter-spacing inherits as a length: .02em of 22px
            Assert.That(child.LetterSpacingPx.Value, Is.EqualTo(0.44f).Within(1e-5f));
            TextStyleData ratio = new TextStyleData { Size = 20f }.Inherit(root);
            Assert.That(ratio.Font.LineHeightPx, Is.EqualTo(27.5f).Within(1e-5f));
            TextStyleData small = Css.Text("font: 400 16px/22px var(--font-body)").Merge(Css.Text("font-size: 17px"));
            Assert.That((small.Font.Size, small.Font.LineHeightPx), Is.EqualTo((17f, 22f)));
            // the font shorthand resets font-variant-numeric (written after it, it holds: hud.css's mono numbers)
            Assert.That(Css.Text("font-variant-numeric: tabular-nums; font: 700 24px/30px var(--font-mono)").TabularNums, Is.EqualTo((bool?)false));
            Assert.That(Css.Text("font: 700 24px/30px var(--font-mono); font-variant-numeric: tabular-nums").TabularNums, Is.EqualTo((bool?)true));
            Assert.That(Css.Text("letter-spacing: .1em; font: 700 24px/30px var(--font-mono)").LetterSpacingEm, Is.EqualTo((float?)0.1f));
        }
    }
}
