// dotnet test only: not under the Unity kit, so `unity:sync` never copies it. Backdrop.Script is plain C#
// and cannot reach BattleTheme (Unity only), the exploration core or the font list, so it writes their
// values again. This holds each copy to its source, so changing one side turns a test red (#652).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Backdrop;
using NUnit.Framework;

namespace Backdrop.Script.Tests
{
    public class CopiedValuesTests
    {
        private const string ViewRoot = "unity-port/unity-project-kit/Assets/View";

        // `public static readonly Color Violet = Hex("#8b6fd0");` or with an alpha: `Hex("#10141e", 0.88f)`.
        private static readonly Regex ThemeColour = new Regex(
            @"public static readonly Color (\w+) = Hex\(""(#[0-9a-fA-F]{6})""(?:,\s*([0-9.]+)f)?\)", RegexOptions.Compiled);

        private static readonly Regex UiKitFace = new Regex(@"Face\(ref \w+, ""([^""]+)""\)", RegexOptions.Compiled);

        [Test]
        public void MiasmaLookColours_MatchBattleTheme()
        {
            var theme = ThemeColours();
            AssertSame(theme["Violet"], MiasmaLook.FillThick, "FillThick");
            AssertSame(theme["Violet"], MiasmaLook.SeepColor, "SeepColor");
            AssertSame(theme["Phosphor"], MiasmaLook.FillThin, "FillThin");
            AssertSame(theme["PanelOpaque"], MiasmaLook.Track, "Track");
            AssertSame(theme["Caution"], MiasmaLook.Caution, "Caution");
            AssertSame(theme["Ground"].WithAlpha(0.9f), MiasmaLook.Tick, "Tick");
        }

        [Test]
        public void ExplorationBackdropColours_MatchBattleTheme()
        {
            var theme = ThemeColours();
            AssertSame(theme["Ground"], ExplorationBackdropSpec.Ground, "Ground");
            AssertSame(theme["Ink"], ExplorationBackdropSpec.Ink, "Ink");
            AssertSame(theme["Amber"], ExplorationBackdropSpec.Amber, "Amber");
        }

        [Test]
        public void MiasmaLookReadings_MatchTheExplorationCore()
        {
            Assert.That(MiasmaLook.DeathAt, Is.EqualTo(DungeonCore.Miasma.DeathAt));
            Assert.That(MiasmaLook.StepPercent, Is.EqualTo(DungeonCore.Miasma.StepPercent));
            Assert.That(MiasmaLook.LastStepLine, Is.EqualTo(DungeonCore.Miasma.MaxPenalty * DungeonCore.Miasma.StepPercent));
        }

        [Test]
        public void EveryFontUiKitReads_IsFetchedByFetchFonts()
        {
            string root = FindRepoRoot();
            string uiKit = File.ReadAllText(Path.Combine(root, ViewRoot, "UiKit.cs"));
            string fetch = File.ReadAllText(Path.Combine(root, "unity-port/tools/fetch-fonts.mjs"));

            var read = new List<string>();
            foreach (Match m in UiKitFace.Matches(uiKit)) read.Add(m.Groups[1].Value);

            Assert.That(read, Has.Count.EqualTo(5), "UiKit.cs reads five faces; update this test with a sixth");
            foreach (string face in read)
            {
                Assert.That(fetch, Does.Contain("\"" + face + ".ttf\""), face + " is read by UiKit.cs but not fetched");
            }
        }

        [Test]
        public void TheThemeReader_SeesTheAlphaAndTheTokens()
        {
            var theme = ThemeColours();
            Assert.That(theme["Panel"].A, Is.EqualTo(0.88f).Within(1e-6));
            Assert.That(theme["Violet"].A, Is.EqualTo(1f));
            Assert.That(theme, Does.ContainKey("PanelOpaque"));
        }

        private static void AssertSame(Rgba expected, Rgba actual, string name)
        {
            Assert.That(new[] { actual.R, actual.G, actual.B, actual.A },
                Is.EqualTo(new[] { expected.R, expected.G, expected.B, expected.A }).Within(1e-6),
                name + " is not BattleTheme's colour any more");
        }

        private static Dictionary<string, Rgba> ThemeColours()
        {
            string text = File.ReadAllText(Path.Combine(FindRepoRoot(), ViewRoot, "BattleTheme.cs"));
            var colours = new Dictionary<string, Rgba>();
            foreach (Match m in ThemeColour.Matches(text))
            {
                float alpha = m.Groups[3].Success ? float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 1f;
                colours[m.Groups[1].Value] = Rgba.Hex(m.Groups[2].Value, alpha);
            }
            Assert.That(colours, Is.Not.Empty, "no colours read from BattleTheme.cs");
            return colours;
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ViewRoot))) return dir.FullName;
                dir = dir.Parent;
            }
            Assert.Fail("Assets/View not found above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
