using System.Collections.Generic;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class TokensTests
    {
        private static IEnumerable<TestCaseData> ColourTokens()
        {
            yield return new TestCaseData("--ground", DsTokens.Ground);
            yield return new TestCaseData("--panel", DsTokens.Panel);
            yield return new TestCaseData("--panel-solid", DsTokens.PanelSolid);
            yield return new TestCaseData("--card", DsTokens.Card);
            yield return new TestCaseData("--ink", DsTokens.Ink);
            yield return new TestCaseData("--ink-muted", DsTokens.InkMuted);
            yield return new TestCaseData("--rule", DsTokens.Rule);
            yield return new TestCaseData("--steel", DsTokens.Steel);
            yield return new TestCaseData("--amber", DsTokens.Amber);
            yield return new TestCaseData("--wick", DsTokens.Wick);
            yield return new TestCaseData("--crimson", DsTokens.Crimson);
            yield return new TestCaseData("--hp-text", DsTokens.HpText);
            yield return new TestCaseData("--violet", DsTokens.Violet);
            yield return new TestCaseData("--reach-red", DsTokens.ReachRed);
            yield return new TestCaseData("--phosphor", DsTokens.Phosphor);
            yield return new TestCaseData("--guard", DsTokens.Guard);
            yield return new TestCaseData("--miss", DsTokens.Miss);
            yield return new TestCaseData("--caution", DsTokens.Caution);
            yield return new TestCaseData("--boss", DsTokens.Boss);
            yield return new TestCaseData("--washi", DsTokens.Washi);
            yield return new TestCaseData("--washi-ink", DsTokens.WashiInk);
            yield return new TestCaseData("--figure-self", DsTokens.FigureSelf);
            yield return new TestCaseData("--figure-enemy", DsTokens.FigureEnemy);
            yield return new TestCaseData("--sky-top", DsTokens.SkyTop());
            yield return new TestCaseData("--floor-tile", DsTokens.FloorTile());
            yield return new TestCaseData("--keyline", DsTokens.Keyline());
        }

        [TestCaseSource(nameof(ColourTokens))]
        public void TypedColour_EqualsTheTokenText(string name, Rgba typed)
        {
            Assert.That(DsTokens.TryResolve(name, DsTheme.Dusk, out string css), Is.True);
            Assert.That(CssColor.Parse("var(" + name + ")").Approximately(typed, 1e-6f), Is.True, name + " = " + css);
        }

        [Test]
        public void Themes_OnlyTheTenThemedTokensDiffer()
        {
            Assert.That(DsTokens.SkyTop(DsTheme.WakakusaA), Is.EqualTo(Rgba.Hex(0xf4f2df)));
            Assert.That(DsTokens.Keyline(DsTheme.WakakusaB), Is.EqualTo(Rgba.Bytes(16, 20, 30, 0.85f)));
            Assert.That(DsTokens.Keyline(DsTheme.Dusk).A, Is.EqualTo(0f));
            Assert.That(CssColor.Parse("var(--sun)", DsTheme.WakakusaA).Approximately(Rgba.Bytes(255, 255, 255, 0.55f), 1e-6f), Is.True);
            // an unthemed token reads the same in every theme
            Assert.That(CssColor.Parse("var(--amber)", DsTheme.WakakusaB), Is.EqualTo(DsTokens.Amber));
            Assert.That(DsTokens.TryResolve("--nope", DsTheme.Dusk, out _), Is.False);
            Assert.That(DsTokens.TryResolve("ink", DsTheme.Dusk, out string ink) && ink == "#ecebf2", Is.True);
        }

        [Test]
        public void ShadowTokens_ParseToTheTypedLayers()
        {
            Assert.That(Css.Shadows("var(--shadow-card)"), Is.EqualTo(DsTokens.ShadowCard));
            Assert.That(Css.Shadows("var(--shadow-lamp)"), Is.EqualTo(DsTokens.ShadowLamp));
            Assert.That(Css.Shadows("var(--shadow-threat)"), Is.EqualTo(DsTokens.ShadowThreat));
            Assert.That(Css.Shadows("var(--shadow-reach)"), Is.EqualTo(DsTokens.ShadowReach));
            Assert.That(Css.Shadows("var(--shadow-omen-active)"), Is.EqualTo(DsTokens.ShadowOmenActive));
            Assert.That(Css.Shadows("var(--shadow-tip)"), Is.EqualTo(DsTokens.ShadowTip));
            Assert.That(DsTokens.ShadowLamp[1].Blur, Is.EqualTo(20f));
            Assert.That(DsTokens.ShadowLamp[1].Spread, Is.EqualTo(5f));
        }

        [Test]
        public void SizeTokens_MatchTheirText()
        {
            Assert.That(Css.Length("var(--radius-panel)").Px, Is.EqualTo(DsTokens.RadiusPanel));
            Assert.That(Css.Length("var(--radius-chip-self)").Px, Is.EqualTo(DsTokens.RadiusChipSelf));
            Assert.That(Css.Length("var(--radius-pip)").Percent, Is.EqualTo(DsTokens.RadiusPipPercent));
            Assert.That(Css.Length("var(--stroke-icon)").Px, Is.EqualTo(DsTokens.StrokeIcon));
            Assert.That(Css.Length("var(--hatch-gap)").Px, Is.EqualTo(DsTokens.HatchGap));
            Assert.That(Css.Length("var(--card-w)").Px, Is.EqualTo(DsTokens.CardW));
            Assert.That(Css.Length("var(--figure-h)").Px, Is.EqualTo(DsTokens.FigureH));
            Assert.That(Css.Parse("opacity: var(--opacity-unpayable)").Opacity, Is.EqualTo(DsTokens.OpacityUnpayable));
            Assert.That(TextMath.FamilyOf(DsTokensFont("--font-display")), Is.EqualTo(Family.Display));
            Assert.That(TextMath.FamilyOf(DsTokensFont("--font-body")), Is.EqualTo(Family.Body));
            Assert.That(TextMath.FamilyOf(DsTokensFont("--font-mono")), Is.EqualTo(Family.Mono));
        }

        private static string DsTokensFont(string name)
        {
            DsTokens.TryResolve(name, DsTheme.Dusk, out string css);
            return css;
        }

        [Test]
        public void Layout_IsOcbLayout()
        {
            // bundle.js:1310-1320
            Assert.That((DsLayout.W, DsLayout.H, DsLayout.FloorX0, DsLayout.FloorX1, DsLayout.FloorY), Is.EqualTo((1920f, 1080f, 300f, 1620f, 560f)));
            Assert.That((DsLayout.CellH, DsLayout.CellGap, DsLayout.FootY, DsLayout.OmenGap, DsLayout.OmenW, DsLayout.OmenH), Is.EqualTo((40f, 3f, 570f, 14f, 200f, 98f)));
            Assert.That((DsLayout.PanelY, DsLayout.PanelSelfW, DsLayout.PanelFoeW, DsLayout.Edge), Is.EqualTo((614f, 300f, 210f, 24f)));
            Assert.That((DsLayout.StanceY, DsLayout.StanceW, DsLayout.StanceH, DsLayout.StanceGap, DsLayout.StanceMax), Is.EqualTo((470f, 250f, 52f, 6f, 3)));
            Assert.That((DsLayout.DistY, DsLayout.ThrowX0, DsLayout.ThrowX1), Is.EqualTo((563f, 400f, 1520f)));
            Assert.That((DsLayout.Stamina.X, DsLayout.Stamina.Y), Is.EqualTo((32f, 846f)));
            Assert.That((DsLayout.Piles.X, DsLayout.Piles.Y, DsLayout.Piles.W, DsLayout.Piles.H, DsLayout.Piles.Gap), Is.EqualTo((32f, 948f, 64f, 84f, 22f)));
            Assert.That((DsLayout.Attrs.X, DsLayout.Attrs.Y, DsLayout.GuardPre.X, DsLayout.GuardPre.Y), Is.EqualTo((1718f, 896f, 1560f, 984f)));
            Assert.That((DsLayout.EndTurn.X, DsLayout.EndTurn.Y, DsLayout.Corner.X, DsLayout.Corner.Y), Is.EqualTo((1672f, 976f, 24f, 20f)));
            Assert.That((DsLayout.Journal.X, DsLayout.Journal.Y, DsLayout.Speed.X, DsLayout.Speed.Y), Is.EqualTo((1840f, 20f, 1708f, 20f)));
            Assert.That((DsLayout.Hand.Cx, DsLayout.RefusalBottom, DsLayout.BannerY), Is.EqualTo((960f, 884f, 400f)));
        }

        [Test]
        public void EveryTokenText_Parses()
        {
            foreach (string name in DsTokens.Names)
            {
                DsTokens.TryResolve(name, DsTheme.Dusk, out string css);
                bool ok = CssColor.TryParse("var(" + name + ")", out _) || CssLength.TryParse(Resolve(css), out _) ||
                          Css.TryShadows(css, DsTheme.Dusk, null, out _) || float.TryParse(css, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ||
                          name.StartsWith("--font-");
                Assert.That(ok, Is.True, name + ": " + css);
            }
        }

        private static string Resolve(string css) => css.StartsWith("var(") ? "0" : css;
    }
}
