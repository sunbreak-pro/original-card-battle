using System.Linq;
using Backdrop;
using NUnit.Framework;

namespace Trial.Script.Tests
{
    /// <summary>
    /// The hall's room (hall.js hallBackdropSpec) against what the JS gives: the floats node printed for strength 1
    /// and 0.5 are compared exactly as float32. The strata's skies follow the layer skies, which depart from the JS
    /// since 2026-11-03 (unity-port/TRIAL-PORT.md, #89): they are written as hall.js skyCss would write those skies.
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

        // hall.js skyCss(layer) over the layer skies of 2026-11-03 (rgbaCss: Math.round(channel × 255), alpha toFixed(3),
        // Math.round(At × 100) percent), as node would print it.
        private static readonly string[] SkyCss =
        {
            "linear-gradient(180deg, rgba(250,253,224,1.000) 0%, rgba(235,248,172,1.000) 22%, rgba(196,232,120,1.000) 48%, rgba(150,206,86,1.000) 75%, rgba(98,162,66,1.000) 100%)",
            "linear-gradient(180deg, rgba(251,248,226,1.000) 0%, rgba(235,229,187,1.000) 35%, rgba(207,201,153,1.000) 70%, rgba(154,146,104,1.000) 100%)",
            "linear-gradient(180deg, rgba(186,252,243,1.000) 0%, rgba(134,242,228,1.000) 25%, rgba(88,216,198,1.000) 50%, rgba(50,176,158,1.000) 75%, rgba(26,130,112,1.000) 100%)",
            "linear-gradient(180deg, rgba(255,255,255,1.000) 0%, rgba(241,236,255,1.000) 25%, rgba(220,212,246,1.000) 50%, rgba(196,228,238,1.000) 70%, rgba(141,156,204,1.000) 100%)",
            "linear-gradient(180deg, rgba(46,13,24,1.000) 0%, rgba(106,31,46,1.000) 40%, rgba(138,48,64,1.000) 62%, rgba(42,10,18,1.000) 100%)",
            "linear-gradient(180deg, rgba(4,6,28,1.000) 0%, rgba(12,20,80,1.000) 35%, rgba(24,26,112,1.000) 50%, rgba(10,14,58,1.000) 70%, rgba(2,3,16,1.000) 100%)",
            "linear-gradient(180deg, rgba(5,6,26,1.000) 0%, rgba(10,11,38,1.000) 50%, rgba(4,4,15,1.000) 100%)",
        };

        // Math.round(At × 100) of each sky stop, by layer (hall.js skyCss).
        private static readonly System.Collections.Generic.Dictionary<int, int[]> StratumPercents = new System.Collections.Generic.Dictionary<int, int[]>
        {
            [1] = new[] { 0, 22, 48, 75, 100 },
            [2] = new[] { 0, 35, 70, 100 },
            [3] = new[] { 0, 25, 50, 75, 100 },
            [4] = new[] { 0, 25, 50, 70, 100 },
            [5] = new[] { 0, 40, 62, 100 },
            [6] = new[] { 0, 35, 50, 70, 100 },
            [7] = new[] { 0, 50, 100 },
        };

        [Test]
        public void TheStrata_Skies_WriteHallsCss_ByteForByte()
        {
            static int Byte(float v) => (int)System.Math.Floor(v * 255.0 + 0.5);
            static string Stop(StratumStop s) => string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "rgba({0},{1},{2},{3:0.000}) {4}%", Byte(s.Color.R), Byte(s.Color.G), Byte(s.Color.B), s.Color.A, s.Percent);
            for (int layer = 1; layer <= 7; layer++)
            {
                string css = "linear-gradient(180deg, " + string.Join(", ", HallBackdropSpec.StratumSky(layer).Select(Stop)) + ")";
                Assert.That(css, Is.EqualTo(SkyCss[layer - 1]), "layer " + layer);
            }
        }

        [Test]
        public void TheStrata_SkyPercents_AreHallsSkyCss()
        {
            foreach (var pair in StratumPercents)
            {
                var stops = HallBackdropSpec.StratumSky(pair.Key);
                Assert.That(stops.Select(s => s.Percent), Is.EqualTo(pair.Value), "layer " + pair.Key);
                Assert.That(stops.Select(s => s.Color), Is.EqualTo(BackdropSpec.For(pair.Key).Sky.Select(s => s.Color)), "layer " + pair.Key);
            }
        }
    }
}
