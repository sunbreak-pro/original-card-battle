using System.Linq;
using Backdrop;
using NUnit.Framework;

namespace Trial.Script.Tests
{
    /// <summary>
    /// The hall's room (hall.js hallBackdropSpec) against what the JS gives: the floats node printed for strength 1
    /// and 0.5 are compared exactly as float32, and the strata's sky percents against hall.js skyCss.
    /// </summary>
    public class HallBackdropSpecTests
    {
        private static float F(double js) => (float)js;

        private static void AssertColor(Rgba c, double r, double g, double b, double a, string what)
        {
            Assert.That(new[] { c.R, c.G, c.B, c.A }, Is.EqualTo(new[] { F(r), F(g), F(b), F(a) }), what);
        }

        [Test]
        public void TheRoom_AtFullStrength_IsTheJs()
        {
            LayerBackdrop spec = HallBackdropSpec.For();
            Assert.That(spec.Layer, Is.EqualTo(0));
            Assert.That(spec.Name, Is.EqualTo("継承の間"));
            Assert.That(spec.Sky.Select(s => s.At), Is.EqualTo(new[] { 0f, F(0.550000011920929), 1f }));
            AssertColor(spec.Sky[0].Color, 0.11356863379478455, 0.11325490474700928, 0.12478431314229965, 1, "sky 0");
            AssertColor(spec.Sky[1].Color, 0.062745101749897, 0.0784313753247261, 0.11372549086809158, 1, "sky .55");
            AssertColor(spec.Sky[2].Color, 0.0345098078250885, 0.04313725605607033, 0.06254902482032776, 1, "sky 1");

            Assert.That(spec.Hazes.Length, Is.EqualTo(2));
            Haze amber = spec.Hazes[0];
            Assert.That(amber.Shape, Is.EqualTo(HazeShape.Radial));
            AssertColor(amber.Color, 0.9098039269447327, 0.658823549747467, 0.2980392277240753, 0.1599999964237213, "amber glow");
            Assert.That(new[] { amber.X, amber.Y, amber.Size }, Is.EqualTo(new[] { 0.5f, F(0.019999999552965164), 1.25f }));
            Assert.That(amber.Count, Is.EqualTo(1));
            Assert.That(amber.Angle, Is.EqualTo(0f));
            Assert.That(amber.Flicker.PeriodMs, Is.EqualTo(4200));
            Assert.That(amber.Flicker.Depth, Is.EqualTo(F(0.2800000011920929)));
            Assert.That(amber.Flicker.Flash, Is.False);

            Haze wick = spec.Hazes[1];
            Assert.That(wick.Shape, Is.EqualTo(HazeShape.Radial));
            AssertColor(wick.Color, 1, 0.8470588326454163, 0.6039215922355652, 0.07000000029802322, "wick glow");
            Assert.That(new[] { wick.X, wick.Y, wick.Size }, Is.EqualTo(new[] { F(0.8600000143051147), F(0.30000001192092896), F(0.699999988079071) }));
            Assert.That(wick.Flicker.PeriodMs, Is.EqualTo(5300));
            Assert.That(wick.Flicker.Depth, Is.EqualTo(F(0.3499999940395355)));

            Motes m = spec.Motes;
            Assert.That(m.Count, Is.EqualTo(26));
            AssertColor(m.Color, 1, 0.8470588326454163, 0.6039215922355652, 0.2199999988079071, "motes");
            Assert.That(new[] { m.SizePx, m.SpeedX, m.SpeedY, m.Twinkle }, Is.EqualTo(new[] { 3f, 3f, -5f, F(0.6000000238418579) }));

            AssertColor(spec.Tile, 0, 0, 0, 0, "no tile");
        }

        [Test]
        public void TheRoom_AtHalfStrength_ScalesTheGlowsAndMotes()
        {
            LayerBackdrop half = HallBackdropSpec.For(0.5);
            Assert.That(half.Hazes[0].Color.A, Is.EqualTo(F(0.07999999821186066)));
            Assert.That(half.Hazes[1].Color.A, Is.EqualTo(F(0.03500000014901161)));
            Assert.That(half.Motes.Color.A, Is.EqualTo(F(0.10999999940395355)));
            // the sky and the colours stay
            LayerBackdrop full = HallBackdropSpec.For(1);
            Assert.That(half.Sky.Select(s => s.Color.R), Is.EqualTo(full.Sky.Select(s => s.Color.R)));
            Assert.That(half.Hazes[0].Color.R, Is.EqualTo(full.Hazes[0].Color.R));
        }

        [Test]
        public void TheSky_IsTheAnalysisColours_InBytes()
        {
            // meta-a §1.1: 0% ≈ rgb(29,29,32), 100% ≈ rgb(9,11,16)
            LayerBackdrop spec = HallBackdropSpec.For();
            int Byte(float v) => (int)System.Math.Round(v * 255);
            Assert.That(new[] { Byte(spec.Sky[0].Color.R), Byte(spec.Sky[0].Color.G), Byte(spec.Sky[0].Color.B) }, Is.EqualTo(new[] { 29, 29, 32 }));
            Assert.That(new[] { Byte(spec.Sky[2].Color.R), Byte(spec.Sky[2].Color.G), Byte(spec.Sky[2].Color.B) }, Is.EqualTo(new[] { 9, 11, 16 }));
        }

        [Test]
        public void EachCall_BuildsAFreshRoom()
        {
            LayerBackdrop a = HallBackdropSpec.For();
            LayerBackdrop b = HallBackdropSpec.For();
            Assert.That(a, Is.Not.SameAs(b));
            Assert.That(a.Hazes, Is.Not.SameAs(b.Hazes));
        }

        // hall.js skyCss(layer) as node printed it (rgbaCss: Math.round(channel × 255), alpha toFixed(3)).
        private static readonly string[] JsSkyCss =
        {
            "linear-gradient(180deg, rgba(214,232,164,1.000) 0%, rgba(124,171,74,1.000) 40%, rgba(34,57,28,1.000) 100%)",
            "linear-gradient(180deg, rgba(184,166,132,1.000) 0%, rgba(138,122,94,1.000) 50%, rgba(74,64,52,1.000) 100%)",
            "linear-gradient(180deg, rgba(30,26,58,1.000) 0%, rgba(11,10,24,1.000) 100%)",
            "linear-gradient(180deg, rgba(228,230,242,1.000) 0%, rgba(183,187,214,1.000) 50%, rgba(125,130,168,1.000) 100%)",
            "linear-gradient(180deg, rgba(26,14,18,1.000) 0%, rgba(7,5,7,1.000) 100%)",
            "linear-gradient(180deg, rgba(18,11,28,1.000) 0%, rgba(4,2,8,1.000) 100%)",
            "linear-gradient(180deg, rgba(8,10,26,1.000) 0%, rgba(2,3,9,1.000) 100%)",
        };

        [Test]
        public void TheStrata_Skies_WriteTheJsCss_ByteForByte()
        {
            static int Byte(float v) => (int)System.Math.Floor(v * 255.0 + 0.5);
            static string Stop(StratumStop s) => string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "rgba({0},{1},{2},{3:0.000}) {4}%", Byte(s.Color.R), Byte(s.Color.G), Byte(s.Color.B), s.Color.A, s.Percent);
            for (int layer = 1; layer <= 7; layer++)
            {
                string css = "linear-gradient(180deg, " + string.Join(", ", HallBackdropSpec.StratumSky(layer).Select(Stop)) + ")";
                Assert.That(css, Is.EqualTo(JsSkyCss[layer - 1]), "layer " + layer);
            }
        }

        [Test]
        public void TheStrata_SkyPercents_AreHallJsSkyCss()
        {
            foreach (var pair in JsMetaText.StratumPercents)
            {
                var stops = HallBackdropSpec.StratumSky(pair.Key);
                Assert.That(stops.Select(s => s.Percent), Is.EqualTo(pair.Value), "layer " + pair.Key);
                Assert.That(stops.Select(s => s.Color), Is.EqualTo(BackdropSpec.For(pair.Key).Sky.Select(s => s.Color)), "layer " + pair.Key);
            }
        }
    }
}
