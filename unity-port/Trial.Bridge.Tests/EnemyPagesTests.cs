// The journal's enemy pages (js: test/ext/journal.test.mjs J-1〜J-6; src/bridge/journal/EnemyPages.js). J-2 reads the
// roster (.claude/docs/enemy_document/enemy_roster_v4.md) and is ignored where it is not on the machine.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BattleCore;
using Journal;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    public sealed class EnemyPagesTests
    {
        private static readonly string[] Bosses = { "miasma_priest", "abyss_angler", "distortion_root" };

        // ------------------------------------------------------------------ J-1

        // js: test/ext/journal.test.mjs:73
        [Test]
        public void J1_EveryEnemyTheCoreKnows_HasAPageUnderItsId_OnTheEnemyTab_TitledWithTheCoreName()
        {
            Assert.That(EnemyPages.Ids, Is.EqualTo(Enemies.All.Select(d => d.Id)));
            foreach (EnemyDef def in Enemies.All)
            {
                Assert.That(EnemyPages.Has(def.Id), Is.True, def.Id);
                JournalPage page = EnemyPages.Page(def.Id, 1);
                Assert.That(page.Id, Is.EqualTo("enemy-" + def.Id));
                Assert.That(page.Tab, Is.EqualTo(JournalTab.Enemy));
                Assert.That(page.Title, Is.EqualTo(def.Name));
                Assert.That(JournalBook.TabText(page), Is.EqualTo(def.Name));
            }
            Assert.That(EnemyPages.Has("dragon_god"), Is.False, "the extra boss is not in the core yet");
            Assert.That(EnemyPages.Has(null), Is.False);
            Assert.Throws<KeyNotFoundException>(() => EnemyPages.Page("nobody", 1));
        }

        // ------------------------------------------------------------------ J-2

        private sealed class RosterSection
        {
            public string FavoredGap;
            public string Gap0, Gap1to2, Gap3Plus, Weakness;
            public readonly List<string> Adaptations = new List<string>();
            public readonly Dictionary<string, string[]> Rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
        }

        /// <summary>The roster beside this checkout (js: journal.test.mjs:47 ROSTER_PATH), or null.</summary>
        private static string RosterPath()
        {
            for (DirectoryInfo dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, ".claude", "docs", "enemy_document", "enemy_roster_v4.md");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        // js: test/ext/journal.test.mjs:92
        private static Dictionary<string, RosterSection> ReadRoster(string path)
        {
            var output = new Dictionary<string, RosterSection>(StringComparer.Ordinal);
            var head = new Regex("^##+ .*?（([a-z_]+)）— 得意な間合い ([^、]+)、");
            var journal = new Regex("^(?:手記: )?間合い 0「(.*?)」。1〜2「(.*?)」。3 以上「(.*?)」。弱点「(.*?)」。$");
            var cond = new Regex("^「.*」$");
            var id = new Regex("^[a-z_0-9]+$");
            RosterSection cur = null;
            foreach (string raw in File.ReadAllText(path).Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                Match h = head.Match(line);
                if (h.Success)
                {
                    cur = new RosterSection { FavoredGap = h.Groups[2].Value };
                    output[h.Groups[1].Value] = cur;
                    continue;
                }
                if (cur == null) continue;
                Match j = journal.Match(line);
                if (j.Success)
                {
                    cur.Gap0 = j.Groups[1].Value;
                    cur.Gap1to2 = j.Groups[2].Value;
                    cur.Gap3Plus = j.Groups[3].Value;
                    cur.Weakness = j.Groups[4].Value;
                }
                if (line.StartsWith("| ", StringComparison.Ordinal))
                {
                    string[] cells = line.Split(new[] { " | " }, StringSplitOptions.None)
                        .Select(c => Regex.Replace(c, @"^\| |\s\|$", "")).ToArray();
                    string c0 = cells.FirstOrDefault(c => cond.IsMatch(c));
                    if (c0 != null && id.IsMatch(cells[0])) cur.Adaptations.Add(c0.Substring(1, c0.Length - 2));
                    cur.Rows[cells[0]] = cells;
                }
            }
            return output;
        }

        // js: test/ext/journal.test.mjs:112
        [Test]
        public void J2_ThePagesTexts_AreTheRostersByteForByte()
        {
            string path = RosterPath();
            if (path == null) Assert.Ignore("the roster is not on this machine");
            Dictionary<string, RosterSection> roster = ReadRoster(path);
            foreach (string id in EnemyPages.Ids)
            {
                EnemyPageEntry e = EnemyPages.Entry(id);
                Assert.That(roster.TryGetValue(id, out RosterSection r), Is.True, $"the roster has a section for {id}");
                Assert.That(e.FavoredGap, Is.EqualTo(r.FavoredGap), $"{id} 得意な間合い");
                Assert.That(e.Gap0, Is.EqualTo(r.Gap0), $"{id} 間合い 0");
                Assert.That(e.Gap1to2, Is.EqualTo(r.Gap1to2), $"{id} 間合い 1〜2");
                Assert.That(e.Gap3Plus, Is.EqualTo(r.Gap3Plus), $"{id} 間合い 3 以上");
                Assert.That(e.Weakness, Is.EqualTo(r.Weakness), $"{id} 弱点");
                Assert.That(e.Adaptations, Is.EqualTo(r.Adaptations), $"{id} 適応条件");
                foreach (EnemySpecial item in e.Special)
                {
                    if (item.ActionId != null)
                    {
                        // | id | 名前 | 属性 | 列 | 届く | 効果 | 状態 | 特性 | 予兆 |
                        Assert.That(r.Rows.TryGetValue(item.ActionId, out string[] row), Is.True, $"{id} {item.ActionId} is in its action table");
                        Assert.That(item.Name, Is.EqualTo(row[1]));
                        Assert.That(item.Effect, Is.EqualTo(row[5]));
                        Assert.That(item.Trait ?? "—", Is.EqualTo(row[7]));
                        Assert.That(item.Omen, Is.EqualTo(row[8]));
                    }
                    else
                    {
                        // | 状態 | 向き | 効き方 | 持続 | 対処 |
                        Assert.That(r.Rows.TryGetValue(item.Status, out string[] row), Is.True, $"{id} {item.Status} is in its 専用の状態 table");
                        Assert.That(row[1], Is.EqualTo("相手に"));
                        Assert.That(item.Effect, Is.EqualTo(row[2]));
                        Assert.That(item.Lasts, Is.EqualTo(row[3]));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ J-3

        // js: test/ext/journal.test.mjs:148
        [Test]
        public void J3_APage_ShowsNameAndHpAt0_TheTendenciesAt1_TheRestAt2()
        {
            JournalPage page0 = EnemyPages.Page("polearm_warped", 0);
            Assert.That(page0.Rows.Select(r => r.Label), Is.EqualTo(new[] { "名前", "HP 最大", "得意な間合い", "間合い 0", "間合い 1〜2", "間合い 3 以上", "弱点", "特殊行動" }));
            Assert.That(page0.Rows.Select(r => r.NeedsDisclosure), Is.EqualTo(new[] { 0, 0, 1, 1, 1, 1, 2, 2 }));
            Assert.That(page0.Rows.Select(page0.TextOf), Is.EqualTo(new[] { "錆槍の竜兵", "60", "？", "？", "？", "？", "？", "？" }));
            Assert.That(page0.DisclosureLabel, Is.EqualTo(""), "no 「開示度」 beside a page at 0 (JournalPage)");

            JournalPage page1 = EnemyPages.Page("polearm_warped", 1);
            Assert.That(page1.DisclosureLabel, Is.EqualTo("開示度 1 / 2"));
            Assert.That(page1.Body, Is.EqualTo(string.Join("\n\n", new[]
            {
                "名前\n錆槍の竜兵",
                "HP 最大\n60",
                "得意な間合い\n1〜2",
                "間合い 0\n柄尻で 2 マス押し返す。Guard が無いと重い。後ろが端なら壁にも当たる",
                "間合い 1〜2\n払い斬り。2 マス空いていると +3",
                "間合い 3 以上\n踏み込んで 1 マス寄ってくる",
                "弱点\n？",
                "特殊行動\n？",
            })));

            JournalPage page2 = EnemyPages.Page("polearm_warped", 2);
            Assert.That(page2.DisclosureLabel, Is.EqualTo("開示度 2 / 2"));
            Assert.That(page2.Rows.All(r => !page2.IsHidden(r)), Is.True, "every row reads at 2");
            Assert.That(page2.TextOf(page2.Rows[6]), Is.EqualTo("隣へ入れば払い斬りは届かないが、1 ターンで押し返される。押し込みの予兆が出たターンに鈍足を付けると、押し返しが 1 マスに減る。Guard を残して受けてもよい"));
            Assert.That(page2.TextOf(page2.Rows[7]), Is.EqualTo("なし"));

            Assert.Throws<ArgumentOutOfRangeException>(() => EnemyPages.Page("polearm_warped", 3), "JournalPage refuses 開示度 past 2");
            JournalPage priest = EnemyPages.Page("miasma_priest", 0);
            Assert.That(priest.TextOf(priest.Rows[1]), Is.EqualTo("140"), "HP 最大 is the core's");
        }

        // ------------------------------------------------------------------ J-4

        // js: test/ext/journal.test.mjs:181
        [Test]
        public void J4_OnlyTheBosses_CarryTheirAdaptations_EveryConditionOfTheirTable_OneALine()
        {
            foreach (string id in EnemyPages.Ids)
            {
                JournalPage page = EnemyPages.Page(id, 2);
                JournalRow row = page.Rows.FirstOrDefault(r => r.Label == "適応条件");
                if (Bosses.Contains(id))
                {
                    Assert.That(row, Is.Not.Null, $"{id} has 適応条件");
                    Assert.That(row.NeedsDisclosure, Is.EqualTo(2), "開示度 2 で全文 (roster §1.5)");
                    Assert.That(row, Is.SameAs(page.Rows[page.Rows.Count - 1]), "適応条件 closes the roster's rows (§2.4 order)");
                }
                else
                {
                    Assert.That(row, Is.Null, $"{id} has none");
                }
            }
            Assert.That(EnemyPages.AdaptationsText("miasma_priest"), Is.EqualTo(string.Join("\n", new[]
            {
                "アタックを 3 回続けて受けると、とぐろを締めて帳を張り直す",
                "Guard を 9 以上で 2 ターン受け続けると、崩しを狙う",
                "隣に 2 ターン居続けると、払いのけで押し返す",
                "HP が半分を切ると、儀式を毎フェーズ行う",
            })));
            Assert.That(EnemyPages.Entry("abyss_angler").Adaptations.Count, Is.EqualTo(4));
            Assert.That(EnemyPages.Entry("distortion_root").Adaptations.Count, Is.EqualTo(7), "root_a / g / st / sk / m and the two stages");
            JournalPage root1 = EnemyPages.Page("distortion_root", 1);
            Assert.That(root1.TextOf(root1.Rows[root1.Rows.Count - 1]), Is.EqualTo(JournalRow.Hidden));
        }

        // ------------------------------------------------------------------ J-5

        // js: test/ext/journal.test.mjs:206
        [Test]
        public void J5_SpecialActions_AreTheCoresStancesAndBreakActions_ThenABosssOwnTwoStatuses()
        {
            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in EnemyPages.Ids)
            {
                EnemyDef def = Enemies.ById(id);
                foreach (EnemySpecial item in EnemyPages.Entry(id).Special)
                {
                    if (item.ActionId == null) continue;
                    Assert.That(def.Actions.TryGetValue(item.ActionId, out EnemyActionDef action), Is.True, $"{id} has {item.ActionId}");
                    Assert.That(action.Name, Is.EqualTo(item.Name));
                    Assert.That(action.Face.Stance != null || action.Face.Break > 0, Is.True, $"{item.ActionId} is a stance or gives 崩し");
                    if (action.Face.Stance != null) Assert.That(item.Omen, Is.EqualTo("構え"));
                    listed.Add(id + "/" + item.ActionId);
                }
                foreach (EnemyActionDef action in def.Actions.Values)
                {
                    if (action.Face.Stance != null || action.Face.Break > 0)
                        Assert.That(listed.Contains(id + "/" + action.Id), Is.True, $"{id}/{action.Id} is listed");
                }
                int statuses = EnemyPages.Entry(id).Special.Count(s => s.Status != null);
                Assert.That(statuses, Is.EqualTo(Bosses.Contains(id) ? 2 : 0), $"{id}'s own statuses");
            }
            Assert.That(EnemyPages.SpecialText("polearm_warped"), Is.EqualTo("なし"));
            Assert.That(EnemyPages.SpecialText("armored_warden"),
                Is.EqualTo("鉄壁（構え）: 毎ターン開始に Guard +3\n打ち下ろし（攻撃）: 威力 8、崩しを 1 付与する。間合い 0: 崩し +1"));
            Assert.That(EnemyPages.SpecialText("miasma_priest").Split('\n')[1],
                Is.EqualTo("瘴気纏い（相手に付ける）: この戦闘中、最大スタミナ −1。2 スタックまで重なり −2（戦闘終了まで）"));
            Assert.That((Enemies.ById("miasma_priest").Actions["ward"].Attributes & BattleAttribute.Stance) != 0, Is.True);
        }

        // ------------------------------------------------------------------ J-6

        // js: test/ext/journal.test.mjs:233
        [Test]
        public void J6_TheMemoRow_IsThePlayersMemo_ReadOnly_InBattleItEndsOnTheLockedLine()
        {
            Assert.That(EnemyPages.MemoRow("間合い 2 で待つ", JournalContext.Exploration).Text, Is.EqualTo("間合い 2 で待つ"));
            Assert.That(EnemyPages.MemoRow("間合い 2 で待つ", JournalContext.Battle).Text, Is.EqualTo("間合い 2 で待つ\n戦闘中は書けない"));
            Assert.That(EnemyPages.MemoRow("", JournalContext.Exploration).Text, Is.EqualTo("メモはまだありません。"));
            Assert.That(EnemyPages.MemoRow(null, JournalContext.Battle).Text, Is.EqualTo("メモはまだありません。\n戦闘中は書けない"));
            Assert.That(EnemyPages.MemoRow("x", JournalContext.Battle).NeedsDisclosure, Is.EqualTo(0), "the memo is the player's, never 「？」");

            JournalPage page = EnemyPages.Page("shadow_hound", 0);
            JournalPage withMemo = EnemyPages.WithMemo(page, "すぐ下がる", JournalContext.Battle);
            Assert.That(withMemo.Rows.Count, Is.EqualTo(page.Rows.Count + 1));
            Assert.That(withMemo.Rows[withMemo.Rows.Count - 1].Label, Is.EqualTo("自分のメモ"));
            Assert.That(withMemo.TextOf(withMemo.Rows[withMemo.Rows.Count - 1]), Is.EqualTo("すぐ下がる\n戦闘中は書けない"), "readable at 開示度 0");
            Assert.That(withMemo.Disclosure, Is.EqualTo(0));
            Assert.That(page.Rows.Count, Is.EqualTo(8), "the page itself is left as it was");
            JournalPage again = EnemyPages.WithMemo(withMemo, "書き直した", JournalContext.Exploration);
            Assert.That(again.Rows.Count(r => r.Label == "自分のメモ"), Is.EqualTo(1), "one memo row, the newest text");
            Assert.That(again.Rows[again.Rows.Count - 1].Text, Is.EqualTo("書き直した"));
            var terms = new JournalPage("memo", JournalTab.Memo, "メモ", new[] { new JournalRow("", "メモはまだありません。") });
            Assert.That(EnemyPages.WithMemo(terms, "x", JournalContext.Exploration), Is.SameAs(terms), "other pages stay as they are");
            // The battle book shuts the メモ tab (JournalBook.CanOpen), so the enemy page is where the memo is read in battle.
            var book = new JournalBook(new[] { withMemo, terms }, JournalContext.Battle);
            Assert.That(book.CanOpen(terms), Is.False);
            Assert.That(book.CanOpen(withMemo), Is.True);
        }

        // ------------------------------------------------------------------ parity with the browser trial

        /// <summary>The fixture EnemyPages.parity.mjs wrote from the JS, beside this test's source.</summary>
        private static string ParityPath()
        {
            for (DirectoryInfo dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, "Trial.Bridge.Tests", "EnemyPages.parity.txt");
                if (File.Exists(path)) return path;
                path = Path.Combine(dir.FullName, "EnemyPages.parity.txt");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        // Every page at every 開示度, its rows, the body the drawer prints, the memo row in battle and while exploring, the
        // 特殊行動 and 適応条件 texts and the 傾向 lines: the same text the JS prints, byte for byte.
        [Test]
        public void EveryPageAndLine_IsTheJsOneByteForByte()
        {
            string path = ParityPath();
            Assert.That(path, Is.Not.Null, "EnemyPages.parity.txt is beside the tests");
            string J(object v) => InheritanceCore.Json.Stringify(v);
            var lines = new List<string> { "ids|" + J(EnemyPages.Ids.Cast<object>().ToList()) };
            foreach (string id in EnemyPages.Ids)
            {
                for (int d = 0; d <= 2; d++)
                {
                    JournalPage page = EnemyPages.Page(id, d);
                    lines.Add($"page|{id}|{d}|" + J(new List<object> { page.Id, page.Tab.ToString(), page.Title, page.Disclosure, page.DisclosureLabel }));
                    lines.Add($"rows|{id}|{d}|" + J(page.Rows.Select(r => (object)new List<object> { r.Label, r.Text, r.NeedsDisclosure, page.TextOf(r) }).ToList()));
                    lines.Add($"body|{id}|{d}|" + J(page.Body));
                    JournalPage memo = EnemyPages.WithMemo(page, d == 1 ? "" : "メモ", d == 2 ? JournalContext.Exploration : JournalContext.Battle);
                    lines.Add($"memo|{id}|{d}|" + J(memo.Body));
                }
                lines.Add($"special|{id}|" + J(EnemyPages.SpecialText(id)));
                lines.Add($"adaptations|{id}|" + J(EnemyPages.AdaptationsText(id)));
                lines.Add($"tendency|{id}|" + J(new List<object> { EnemyPages.TendencyLine(id), EnemyPages.TendencyLine(id, true) }));
            }
            string[] expected = File.ReadAllText(path).Replace("\r", "").TrimEnd('\n').Split('\n');
            Assert.That(lines.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++) Assert.That(lines[i], Is.EqualTo(expected[i]), "line " + (i + 1));
        }

        // ------------------------------------------------------------------ the map's words (J-9's EncounterName, J-7's TendencyLine)

        // js: test/ext/journal.test.mjs:376
        [Test]
        public void EncounterName_JoinsAFightWithPlus_AndFightsWithArrows_TendencyLine_NamesItsEnemyWhenAsked()
        {
            Assert.That(EnemyPages.EncounterName(new[] { new[] { "a", "b" }, new[] { "c" } }), Is.EqualTo("a + b → c"));
            Assert.That(EnemyPages.TendencyLine("shadow_hound"), Is.EqualTo("傾向: 得意な間合い 0"));
            Assert.That(EnemyPages.TendencyLine("polearm_warped", true), Is.EqualTo("傾向（錆槍の竜兵）: 得意な間合い 1〜2"));
            Assert.That(EnemyPages.PageId("x"), Is.EqualTo("enemy-x"));
            Assert.That(EnemyPages.NameOf("abyss_angler"), Is.EqualTo("獄竜 ガルド"));
        }
    }
}
