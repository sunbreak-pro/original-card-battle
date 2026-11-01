using System;
using NUnit.Framework;

namespace Trial.Geometry.Tests
{
    public class CssColorTests
    {
        private static void Near(Rgba actual, Rgba expected, float eps = 1f / 512f) =>
            Assert.That(actual.Approximately(expected, eps), Is.True, "got " + actual + ", expected " + expected);

        [Test]
        public void HexForms()
        {
            Assert.That(CssColor.Parse("#10141d"), Is.EqualTo(Rgba.Hex(0x10141d)));
            Assert.That(CssColor.Parse("#000"), Is.EqualTo(Rgba.Black));
            Assert.That(CssColor.Parse("#fff"), Is.EqualTo(Rgba.White));
            Near(CssColor.Parse("#ffd89a80"), Rgba.Bytes(255, 216, 154, 128f / 255f));
            Near(CssColor.Parse("#f008"), Rgba.Bytes(255, 0, 0, 136f / 255f));
            Assert.That(CssColor.TryParse("#12345", out _), Is.False);
            Assert.That(CssColor.TryParse("#ggg", out _), Is.False);
        }

        [Test]
        public void RgbAndHsl()
        {
            Assert.That(CssColor.Parse("rgba(16,20,30,.88)"), Is.EqualTo(Rgba.Bytes(16, 20, 30, 0.88f)));
            Assert.That(CssColor.Parse("rgb(232, 168, 76)"), Is.EqualTo(Rgba.Bytes(232, 168, 76)));
            Assert.That(CssColor.Parse("rgb(232 168 76 / 50%)"), Is.EqualTo(Rgba.Bytes(232, 168, 76, 0.5f)));
            Near(CssColor.Parse("rgb(100%, 0%, 50%)"), new Rgba(1f, 0f, 0.5f));
            Near(CssColor.Parse("hsl(0, 100%, 50%)"), Rgba.Hex(0xff0000));
            Near(CssColor.Parse("hsl(120deg 100% 25%)"), new Rgba(0f, 0.5f, 0f));
            Near(CssColor.Parse("hsla(240, 100%, 50%, .5)"), Rgba.Hex(0x0000ff, 0.5f));
            Assert.That(CssColor.Parse("transparent"), Is.EqualTo(Rgba.Transparent));
            Assert.That(CssColor.Parse("#000"), Is.EqualTo(CssColor.Parse("black")));
            Assert.Throws<FormatException>(() => CssColor.Parse("12px"));
        }

        [Test]
        public void Vars_WithFallbacksAndNesting()
        {
            Assert.That(CssColor.Parse("var(--miss)"), Is.EqualTo(DsTokens.Steel)); // --miss: var(--steel)
            Assert.That(CssColor.Parse("var(--flood, #06090c)"), Is.EqualTo(DsTokens.Flood));
            var vars = new CssVars().Set("--attr", "var(--violet)").Set("attr-ink", "var(--phosphor)");
            Assert.That(CssColor.Parse("var(--val-ink, var(--attr-ink))", DsTheme.Dusk, vars), Is.EqualTo(DsTokens.Phosphor));
            Assert.That(CssColor.Parse("var(--attr)", DsTheme.Dusk, vars), Is.EqualTo(DsTokens.Violet));
            Assert.That(CssColor.TryParse("var(--undefined)", out _), Is.False);
            var withColour = new CssVars().Set("color", "var(--amber)");
            Assert.That(CssColor.Parse("currentColor", DsTheme.Dusk, withColour), Is.EqualTo(DsTokens.Amber));
        }

        [Test]
        public void Mix_TwoColoursSummingBelow100_ScalesTheAlpha()
        {
            // hand.css:63 .ocbg-battle-mod: color-mix(in srgb, var(--card) 70%, var(--ink) 18%) = rgba(67,70,81,.88) (battle-views.md)
            Rgba c = CssColor.Parse("color-mix(in srgb, var(--card) 70%, var(--ink) 18%)");
            Assert.That((c.R8, c.G8, c.B8), Is.EqualTo((67, 70, 81)));
            Assert.That(c.A, Is.EqualTo(0.88f).Within(1e-5f));
        }

        [Test]
        public void Mix_WithTransparent_IsTheColourAtThePercentage()
        {
            Rgba c = CssColor.Parse("color-mix(in srgb, var(--amber) 16%, transparent)");
            Near(c, DsTokens.Amber.WithAlpha(0.16f), 1e-5f);
            Rgba ground = CssColor.Parse("color-mix(in srgb, var(--ground) 0%, transparent)");
            Assert.That(ground.A, Is.EqualTo(0f));
            Near(CssColor.Parse("color-mix(in srgb, var(--ink) 55%, transparent)"), DsTokens.Ink.WithAlpha(0.55f), 1e-5f);
        }

        [Test]
        public void Mix_OmittedPercentages()
        {
            // both omitted: 50 / 50
            Near(CssColor.Parse("color-mix(in srgb, #000, #fff)"), new Rgba(0.5f, 0.5f, 0.5f));
            // one omitted: the rest of 100
            Near(CssColor.Parse("color-mix(in srgb, var(--crimson) 75%, var(--flood, #06090c))"),
                CssColor.Mix(DsTokens.Crimson, 75f, DsTokens.Flood, 25f));
            Near(CssColor.Mix(Rgba.Black, float.NaN, Rgba.White, 25f), new Rgba(0.25f, 0.25f, 0.25f));
            // percentage written first
            Near(CssColor.Parse("color-mix(in srgb, 30% #fff, #000)"), new Rgba(0.3f, 0.3f, 0.3f));
        }

        [Test]
        public void Mix_IsPremultiplied_WithATranslucentSecondColour()
        {
            // color-mix(in srgb, var(--wick) 10%, var(--panel)): panel is rgba(16,20,30,.88)
            Rgba c = CssColor.Parse("color-mix(in srgb, var(--wick) 10%, var(--panel))");
            float a = 0.1f * 1f + 0.9f * 0.88f;
            Assert.That(c.A, Is.EqualTo(a).Within(1e-5f));
            float r = (1f * 0.1f + (16f / 255f) * 0.88f * 0.9f) / a;
            Assert.That(c.R, Is.EqualTo(r).Within(1e-5f));
        }

        [Test]
        public void Mix_SumAbove100_Normalises()
        {
            Near(CssColor.Mix(Rgba.Black, 100f, Rgba.White, 100f), new Rgba(0.5f, 0.5f, 0.5f));
            Assert.That(CssColor.TryParse("color-mix(in srgb, #000 0%, #fff 0%)", out _), Is.False);
            Assert.That(CssColor.TryParse("color-mix(in oklab, #000, #fff)", out _), Is.False);
        }

        [Test]
        public void ToCss_RoundTrips()
        {
            foreach (string s in new[] { "rgba(16,20,30,0.88)", "rgba(232,168,76,1)", "rgba(0,0,0,0)" })
                Assert.That(CssColor.ToCss(CssColor.Parse(s)), Is.EqualTo(s));
            Rgba mix = CssColor.Parse("color-mix(in srgb, var(--card) 70%, var(--ink) 18%)");
            Near(CssColor.Parse(CssColor.ToCss(mix)), mix, 1f / 255f);
        }
    }
}
