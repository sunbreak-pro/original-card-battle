// dotnet test only: not under the Unity kit, so `unity:sync` never copies it. The View's MonoBehaviours
// live in Assembly-CSharp, which sees all of BattleCore, and never reach `dotnet test`; nothing stops a
// View file from calling a rule. This reads the View's sources as text and holds the line (#572):
// a View file may hold BattleCore's definitions (CardDef, EnemyDef, Departure, BattleAttribute …) to
// pass them along without computing, and only Bridge/ calls BattleCore's rules.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class ViewRulesBoundaryTests
    {
        private const string ViewRoot = "unity-port/unity-project-kit/Assets/View";

        /// <summary>The View files outside Bridge/ and Tests/ allowed to say `using BattleCore;`.</summary>
        private static readonly string[] AllowedUsers =
        {
            "Depiction/DeckSelectScreen.cs",
            "Depiction/DemoFlow.cs",
            "Depiction/DemoScreens.cs",
            "Depiction/DepartureScreen.cs",
            "Exploration/ExplorationBootstrap.cs",
        };

        /// <summary>BattleCore's rule classes. A View file calling one of them is computing.</summary>
        private static readonly string[] RuleClasses =
        {
            "TurnLoop", "EnemyAi", "Combat", "Field", "Traits", "Statuses",
            "Columns", "Mastery", "Cards", "Chain", "DeckRules", "Constants",
        };

        private static readonly Regex UsingBattleCore =
            new Regex(@"^\s*using\s+(static\s+)?(\w+\s*=\s*)?BattleCore\b", RegexOptions.Compiled);

        // A member access on a rule class, bare or qualified with `BattleCore.`. One with a `.` or a
        // letter right before it (page.Cards.Count, MyField.X) is someone else's member and not counted.
        private static readonly Regex RuleCall = new Regex(
            @"(?<![.\w])(?:BattleCore\.)?(" + string.Join("|", RuleClasses) + @")\.", RegexOptions.Compiled);

        [Test]
        public void OnlyTheKnownViewFiles_UseBattleCore()
        {
            Assert.That(FilesUsingBattleCore(ViewSources()), Is.EquivalentTo(AllowedUsers));
        }

        [Test]
        public void NoViewFile_CallsABattleCoreRule()
        {
            Assert.That(RuleCalls(ViewSources()), Is.Empty);
        }

        [Test]
        public void TheUsingCheck_CatchesANewFile()
        {
            var sources = new Dictionary<string, string>
            {
                ["Depiction/DemoFlow.cs"] = "using System;\nusing BattleCore;\n",
                ["Depiction/HpBar.cs"] = "using UnityEngine;\n    using BattleCore;\n",
                ["Depiction/Alias.cs"] = "using Rules = BattleCore.TurnLoop;\n",
                ["Depiction/Static.cs"] = "using static BattleCore.Traits;\n",
                ["Depiction/Clean.cs"] = "using Depiction.Script;\n// using BattleCore; in a comment\n",
            };

            Assert.That(FilesUsingBattleCore(sources),
                Is.EquivalentTo(new[] { "Depiction/DemoFlow.cs", "Depiction/HpBar.cs", "Depiction/Alias.cs", "Depiction/Static.cs" }));
        }

        [Test]
        public void TheRuleCheck_CatchesACall_AndLeavesOtherMembersAlone()
        {
            var sources = new Dictionary<string, string>
            {
                ["Depiction/Preview.cs"] = "var p = TurnLoop.Preview(state, card);\n",
                ["Depiction/Qualified.cs"] = "int n = BattleCore.Field.GapBetween(a, b);\n",
                ["Depiction/Pass.cs"] = "int n = page.Cards.Count;\nvar f = this.Field.Width;\nint k = MyConstants.X;\n",
                ["Depiction/Comment.cs"] = "// TurnLoop.Preview is the Bridge's to call\n",
            };

            Assert.That(RuleCalls(sources), Is.EqualTo(new[]
            {
                "Depiction/Preview.cs:1: var p = TurnLoop.Preview(state, card);",
                "Depiction/Qualified.cs:1: int n = BattleCore.Field.GapBetween(a, b);",
            }));
        }

        private static List<string> FilesUsingBattleCore(IReadOnlyDictionary<string, string> sources) =>
            sources.Where(s => Lines(s.Value).Any(l => UsingBattleCore.IsMatch(l)))
                .Select(s => s.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

        private static List<string> RuleCalls(IReadOnlyDictionary<string, string> sources)
        {
            var hits = new List<string>();
            foreach (var source in sources.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                string[] lines = Lines(source.Value);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (IsComment(lines[i])) continue;
                    if (RuleCall.IsMatch(lines[i])) hits.Add(source.Key + ":" + (i + 1) + ": " + lines[i].Trim());
                }
            }
            return hits;
        }

        private static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n');

        private static bool IsComment(string line)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal);
        }

        /// <summary>Every .cs under Assets/View outside Bridge/ and Tests/, keyed by its path below View/.</summary>
        private static Dictionary<string, string> ViewSources()
        {
            string root = FindViewRoot();
            var sources = new Dictionary<string, string>();
            foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                string[] folders = relative.Split('/');
                if (folders.Take(folders.Length - 1).Any(f => f == "Bridge" || f == "Tests")) continue;
                sources[relative] = File.ReadAllText(path);
            }
            Assert.That(sources, Is.Not.Empty, "no View sources found under " + root);
            return sources;
        }

        private static string FindViewRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, ViewRoot);
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            Assert.Fail("Assets/View not found above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
