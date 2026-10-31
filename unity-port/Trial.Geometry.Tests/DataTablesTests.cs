using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Geometry.Tests
{
    public class DataTablesTests
    {
        [Test]
        public void DsIconTableMatchesTheJs()
        {
            Assert.That(DsIcons.Names.Count, Is.EqualTo(36));
            Assert.That(DsIcons.Names[0], Is.EqualTo("sword"));
            Assert.That(DsIcons.Names[35], Is.EqualTo("speed3"));
            Assert.That(DsIcons.Labels.Count, Is.EqualTo(36));
            Assert.That(DsIcons.Names.All(n => DsIcons.Labels.ContainsKey(n)), Is.True);
            Assert.That(DsIcons.Labels["plus"], Is.EqualTo("「+n」"));
            Assert.That(DsIcons.Labels["speed2"], Is.EqualTo("速さ ×1.25"));
            Assert.That(DsIcons.Paths["heart"], Is.EqualTo("M12 21s-8-5-8-11a4.5 4.5 0 018-2.8A4.5 4.5 0 0120 10c0 6-8 11-8 11z"));
            Assert.That(DsIcons.Filled, Is.EquivalentTo(new[] { "heart", "pip" }));
        }

        [Test]
        public void DsIconGetFallsBackToPlusWithTheRequestedNamesFilledFlag()
        {
            IconDef p = DsIcons.Get("pip");
            Assert.That(p.Filled, Is.True);
            Assert.That(p.ViewBox, Is.EqualTo(new RectF(0, 0, 24, 24)));
            Assert.That(p.StrokeWidth, Is.EqualTo(2.2f));
            Assert.That(p.Label, Is.EqualTo("スタミナの粒"));

            IconDef u = DsIcons.Get("nope");
            Assert.That(u.D, Is.EqualTo(DsIcons.Paths["plus"]));
            Assert.That(u.Filled, Is.False);
            Assert.That(DsIcons.Get(null).D, Is.EqualTo(DsIcons.Paths["plus"]));

            StrokeStyle st = DsIcons.Get("shield").Style(dashed: true);
            Assert.That(st.Cap, Is.EqualTo(Cap.Round));
            Assert.That(st.Join, Is.EqualTo(Join.Round));
            Assert.That(st.Dash, Is.EqualTo(new[] { 2.4f, 2.4f }));
            Assert.That(DsIcons.Get("sword").Style(width: 3f).Width, Is.EqualTo(3f));
        }

        [Test]
        public void ExploreIconsMatchTheJs()
        {
            Assert.That(ExploreIcons.Names.Count, Is.EqualTo(27));
            // the ones shared with the DS set are identical strings
            Assert.That(ExploreIcons.Paths["Battle"], Is.EqualTo(DsIcons.Paths["sword"]));
            Assert.That(ExploreIcons.Paths["cards"], Is.EqualTo(DsIcons.Paths["draw"]));
            foreach (string n in new[] { "journal", "hourglass", "stairs", "miasma", "heart", "pip", "person", "seal" })
                Assert.That(ExploreIcons.Paths[n], Is.EqualTo(DsIcons.Paths[n]), n);
            Assert.That(ExploreIcons.Get("heart").Filled, Is.True);
            Assert.That(ExploreIcons.Get("pip", outline: true).Filled, Is.False);
            Assert.That(ExploreIcons.Get("unknown").D, Is.EqualTo(ExploreIcons.Paths["pouch"]));
            Assert.That(ExploreIcons.ItemIcon("yakuso"), Is.EqualTo("yakuso"));
            Assert.That(ExploreIcons.ItemIcon("no_such_item"), Is.EqualTo("pouch"));
        }

        [Test]
        public void ExtraIconsMatchTheJs()
        {
            Assert.That(ExploreIcons.Paths["gear"], Is.EqualTo(ExtraIcons.GearPath));
            Assert.That(ExtraIcons.Get("stay").D, Is.EqualTo(ExtraIcons.StayPath));
            Assert.That(ExtraIcons.Get("shield").ViewBox, Is.EqualTo(new RectF(0, 0, 64, 72)));
            Assert.That(ExtraIcons.Get("shield").Filled, Is.True);
            Assert.That(ExtraIcons.Get("nothing"), Is.Null);
            StrokeStyle c = ExtraIcons.CutMarkCircleStyle();
            Assert.That(c.Width, Is.EqualTo(2f));
            Assert.That(c.Dash, Is.EqualTo(new[] { 3f, 3f }));
            Assert.That(c.Cap, Is.EqualTo(Cap.Butt));
            // the circle path is the <circle>: r 8 around (12, 12)
            foreach (Polyline l in Flatten.Run(ExtraIcons.CutMarkCirclePath, Affine.Identity))
                foreach (Vec2 v in l.Points) Assert.That(Vec2.Dist(v, new Vec2(12, 12)), Is.EqualTo(8f).Within(1e-3f));
        }

        [Test]
        public void FiguresMatchTheJs()
        {
            Assert.That(DsFigures.Figures.Select(f => f.Name), Is.EqualTo(new[] { "swordsman", "spearman", "hound", "serpent", "warden" }));
            FigureDef s = DsFigures.Get("swordsman");
            Assert.That((s.W, s.H, s.Body, s.Cx, s.Side), Is.EqualTo((140f, 226f, 100f, 62f, "self")));
            Assert.That(s.Paths.Count, Is.EqualTo(12));
            Assert.That(s.IsFoe, Is.False);
            Assert.That(s.DefaultRim, Is.EqualTo(Rgba.Bytes(232, 168, 76, 0.28f)));
            FigureDef w = DsFigures.Get("warden");
            Assert.That((w.W, w.H, w.Body, w.Cx, w.Side), Is.EqualTo((220f, 264f, 180f, 120f, "foe")));
            Assert.That(w.DefaultFillToken, Is.EqualTo("figure-enemy"));
            Assert.That(DsFigures.Get("hound").Paths.Count, Is.EqualTo(9));
            Assert.That(DsFigures.Get("serpent").Paths.Count, Is.EqualTo(5));
            Assert.That(DsFigures.Get("spearman").Paths.Count, Is.EqualTo(11));
            Assert.That(DsFigures.Get("nobody"), Is.SameAs(s));
            Assert.That(s.SizeFor(113f), Is.EqualTo((140f * 113f / 226f, 113f)));
        }

        private static List<float> Numbers(string d)
        {
            var o = new List<float>();
            PathData p = PathData.Parse(d);
            foreach (SubPath sp in p.SubPaths)
            {
                o.Add(sp.Start.X); o.Add(sp.Start.Y);
                foreach (PathSegment s in sp.Segments) { o.Add(s.To.X); o.Add(s.To.Y); }
            }
            return o;
        }

        [Test]
        public void PolyReproducesTheJsStrings()
        {
            (float, float, float, float, float, string)[] cases =
            {
                (100, 92, 136, 22, 5, DsFigures.Get("swordsman").Paths[6]),
                (91, 87, 109, 97, 4, DsFigures.Get("swordsman").Paths[7]),
                (93, 106, 100, 92, 4, DsFigures.Get("swordsman").Paths[8]),
                (20, 164, 152, 30, 5, DsFigures.Get("spearman").Paths[7]),
            };
            foreach (var (x1, y1, x2, y2, w, js) in cases)
            {
                string cs = DsFigures.Poly(x1, y1, x2, y2, w);
                Assert.That(cs, Is.EqualTo(js), "round-trip formatting matches the JS Number → string");
                List<float> a = Numbers(cs), b = Numbers(js);
                Assert.That(a.Count, Is.EqualTo(b.Count));
                for (int i = 0; i < a.Count; i++) Assert.That(a[i], Is.EqualTo(b[i]).Within(1e-4f));
            }
        }

        // ---- every shipped string, pinned to the JS ------------------------------------------------------
        // SHA-256 of the JS tables, computed from the JS sources with node (name TAB value, one per line, JS order):
        // vendor/ds/bundle.js ICONS / ICON_LABELS / FIGURES (w h body cx side, then the paths with poly() evaluated),
        // src/scenes/explore/icons.js EXPLORE_ICONS, and STAY_PATH, GEAR_PATH, SHIELD_PATH, SHIELD_CRACK and the
        // cut mark's slash (freestep.js:48, chrome.js:196, art.js:238, fx.js:55, hand-marks.js:137).
        private const string JsIcons = "b1a52b78ca15fe356d74f68dcaaffd7d07e8024cec9597422eb9a7636421afca";
        private const string JsLabels = "69d44a11884ba8717abad17d1595b5e7a5772711d7f7ff404f971405f3af4c18";
        private const string JsExplore = "660357bcd58d7bfa56419eac5b7d67c508c8f5b94e803f343e527ed1c59532a9";
        private const string JsFigures = "ef9907c8f4796abea0bc9fb0342cfd109e538bbef2ef31a386195f1bd0adc7c7";
        private const string JsExtra = "048f6409ed3b5eb20d44e22c2c984c8e295b288325eea1d35eaf62013a279298";

        private static string Sha(string s)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] h = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
                return string.Concat(h.Select(b => b.ToString("x2")));
            }
        }

        private static string Table(IReadOnlyList<string> names, IReadOnlyDictionary<string, string> map) =>
            string.Join("\n", names.Select(n => n + "\t" + map[n]));

        [Test]
        public void EveryShippedStringIsByteIdenticalToTheJs()
        {
            Assert.That(Sha(Table(DsIcons.Names, DsIcons.Paths)), Is.EqualTo(JsIcons), "ICONS");
            Assert.That(Sha(Table(DsIcons.Names, DsIcons.Labels)), Is.EqualTo(JsLabels), "ICON_LABELS");
            Assert.That(Sha(Table(ExploreIcons.Names, ExploreIcons.Paths)), Is.EqualTo(JsExplore), "EXPLORE_ICONS");
            string figs = string.Join("\n", DsFigures.Figures.Select(f =>
                string.Join("\t", f.Name, N(f.W), N(f.H), N(f.Body), N(f.Cx), f.Side) + "\n" + string.Join("\n", f.Paths)));
            Assert.That(Sha(figs), Is.EqualTo(JsFigures), "FIGURES");
            string extra = string.Join("\n", ExtraIcons.StayPath, ExtraIcons.GearPath, ExtraIcons.ShieldPath,
                ExtraIcons.ShieldCrackPath, ExtraIcons.CutMarkSlashPath);
            Assert.That(Sha(extra), Is.EqualTo(JsExtra), "scene-owned icon paths");
        }

        private static string N(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        [Test]
        public void IconOptionsFollowTheJsFalsyRules()
        {
            // opts.width || 2.2
            Assert.That(DsIcons.Get("sword").Style(width: 0f).Width, Is.EqualTo(2.2f));
            Assert.That(DsIcons.Get("sword").Style(width: float.NaN).Width, Is.EqualTo(2.2f));
            Assert.That(ExploreIcons.Get("Boss").Style(width: 1.2f).Width, Is.EqualTo(1.2f));
            // the fx shield and its crack are stroked with round joins (fx.js:726, 747)
            Assert.That(ExtraIcons.Get("shield").LineJoin, Is.EqualTo(Join.Round));
            Assert.That(ExtraIcons.Get("shield-crack").LineJoin, Is.EqualTo(Join.Round));
            Assert.That(ExtraIcons.Get("shield-crack").LineCap, Is.EqualTo(Cap.Butt));
            // Figure(kind, { scaleTo: 0 }) keeps the natural size
            FigureDef s = DsFigures.Get("swordsman");
            var natural = new MeshBuilder();
            IconMesh.Figure(s, s.H, Rgba.White, s.DefaultRim, 1f, natural);
            var zero = new MeshBuilder();
            IconMesh.Figure(s, 0f, Rgba.White, s.DefaultRim, 1f, zero);
            Assert.That(zero.Bounds(), Is.EqualTo(natural.Bounds()));
        }

        [Test]
        public void TheParseCacheStaysBoundedAndCorrect()
        {
            for (int i = 0; i < 1100; i++) PathData.Cached("M0 0L" + i + " 1");
            PathData a = PathData.Cached("M0 0L7 7");
            Assert.That(PathData.Cached("M0 0L7 7"), Is.SameAs(a));
            Assert.That(a.SubPaths[0].Segments[0].To, Is.EqualTo(new Vec2(7, 7)));
        }

        [Test]
        public void GlowStopsAreAlphaTimesOneMinusRSquared()
        {
            (float Pos, float Alpha)[] g = DsFigures.GlowStops(0.8f);
            Assert.That(g.Length, Is.EqualTo(5));
            Assert.That(g[0], Is.EqualTo((0f, 0.8f)));
            Assert.That(g[1].Alpha, Is.EqualTo(0.8f * 0.5625f).Within(1e-6f));
            Assert.That(g[2].Alpha, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(g[3].Alpha, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(g[4], Is.EqualTo((1f, 0f)));
        }
    }
}
