using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #504: the sixty mastery profiles against swordsman_cards_v4.md §4.6 (v4.7, #449) — each card's
    /// kind, its names at steps 1-3, and for 型替え the condition at each step along battle_core_v4
    /// §19.3 S7 as #504 reads it — and 伸び on the five movement-centred cards (§3.2).
    ///
    /// The table test reads §4.6 out of the canon itself, so a row changed on either side alone fails
    /// here. The copy Unity runs (Assets/Tests) has no canon beside it, so there that test is skipped.
    /// </summary>
    public class MasteryProfilesTests
    {
        private const string CanonPath = ".claude/docs/card_document/swordsman_cards_v4.md";

        // ---- The sixty, without the table ----

        [Test]
        public void TheProfiles_AreSixtyCardsOfTheCatalog_InCanonOrder()
        {
            var ids = MasteryProfiles.All.Select(p => p.CardId).ToList();
            var order = ids.Select(id => IndexOf(id)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ids.Count, Is.EqualTo(60));
                Assert.That(ids.Distinct().Count(), Is.EqualTo(60));
                Assert.That(order, Is.All.GreaterThanOrEqualTo(0), "every profile is a catalog card");
                Assert.That(order, Is.Ordered, "canon order");
                Assert.That(MasteryProfiles.Find("observe"), Is.SameAs(MasteryProfiles.Observe));
            });
        }

        /// <summary>§4.6's tally: the kinds by column (軽さ never on column 1, §19.3 S5).</summary>
        [TestCase(MasteryKind.Extend, 10, 12, 8)]
        [TestCase(MasteryKind.Lighten, 0, 3, 11)]
        [TestCase(MasteryKind.Reform, 8, 6, 2)]
        public void TheKinds_ByColumn_MatchTheCanonTally(MasteryKind kind, int one, int two, int three)
        {
            var columns = MasteryProfiles.All.Where(p => p.Kind == kind).Select(p => CardCatalog.ById(p.CardId).Column).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(columns.Count(c => c == 1), Is.EqualTo(one));
                Assert.That(columns.Count(c => c == 2), Is.EqualTo(two));
                Assert.That(columns.Count(c => c == 3), Is.EqualTo(three));
            });
        }

        [Test]
        public void EveryReformCard_KeepsItsEffect_AndEndsAtFirstPlay()
        {
            // §19.3 S6: the effect stays, only the condition loosens; #504: every path ends at 初手.
            var reforms = MasteryProfiles.All.Where(p => p.Kind == MasteryKind.Reform).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(reforms.Count, Is.EqualTo(16));
                foreach (var profile in reforms)
                {
                    var own = CardCatalog.ById(profile.CardId).Trait!;
                    Assert.That(profile.RankTraits, Has.Count.EqualTo(3), profile.CardId);
                    foreach (var trait in profile.RankTraits!)
                    {
                        Assert.That(trait.Effect, Is.EqualTo(own.Effect), profile.CardId);
                        Assert.That(trait.Amount, Is.EqualTo(own.Amount), profile.CardId);
                        Assert.That(trait.Grant, Is.EqualTo(own.Grant), profile.CardId);
                        Assert.That(trait.Watch, Is.Null, profile.CardId);
                    }
                    Assert.That(profile.RankTraits![2].Condition, Is.EqualTo(TraitCondition.FirstPlay), profile.CardId);
                }
                foreach (var profile in MasteryProfiles.All.Where(p => p.Kind != MasteryKind.Reform))
                {
                    Assert.That(profile.RankTraits, Is.Null, profile.CardId + " keeps its trait");
                }
            });
        }

        // ---- Against §4.6 ----

        [Test]
        public void TheProfiles_MatchTheTableOfSection46()
        {
            string? path = FindCanon();
            if (path == null) Assert.Ignore("swordsman_cards_v4.md is not beside this copy of the tests (Unity).");

            var rows = ReadTable(path!);
            Assert.That(rows.Count, Is.EqualTo(60), "§4.6 has sixty rows");
            Assert.Multiple(() =>
            {
                foreach (var row in rows)
                {
                    var card = CardCatalog.All[row.Number - 1];
                    string at = $"#{row.Number} {row.Name}";
                    var profile = MasteryProfiles.Find(card.Id);

                    Assert.That(card.Name, Is.EqualTo(row.Name), at);
                    Assert.That(card.Column, Is.EqualTo(row.Column), at);
                    Assert.That(profile, Is.Not.Null, at + " has a profile");
                    if (profile == null) continue;

                    Assert.That(KindWord(profile.Kind), Is.EqualTo(row.Kind), at);
                    Assert.That(profile.RankNames, Is.EqualTo(row.Names), at);
                    string conditions = profile.RankTraits == null
                        ? "—"
                        : string.Join(" → ", profile.RankTraits.Select(ConditionWord));
                    Assert.That(conditions, Is.EqualTo(row.Conditions), at);
                }

                var listed = new HashSet<string>(rows.Select(r => CardCatalog.All[r.Number - 1].Id));
                foreach (var profile in MasteryProfiles.All)
                {
                    Assert.That(listed.Contains(profile.CardId), Is.True, $"{profile.CardId} has a profile, §4.6 has no row for it");
                }
            });
        }

        // ---- 伸び on the movement-centred cards (§3.2, #504) ----

        [TestCase("back_leap")]
        [TestCase("slide_step")]
        public void Extend_OnAMovementCardWithGuard_Grows4_6_9_9(string id)
        {
            AssertGrowth(id, c => c.Face.Guard, new[] { 4, 6, 9, 9 });
        }

        [TestCase("footwork")]
        [TestCase("leap_back")]
        public void Extend_OnAMovementCardWithADraw_DrawsOneMoreFromStep2(string id)
        {
            AssertGrowth(id, c => c.Face.Draw, new[] { 1, 1, 2, 2 });
        }

        [Test]
        public void Extend_OnTheDash_GivesOneMoreStaminaFromStep2()
        {
            AssertGrowth("dash_in", c => c.Face.StaminaGain, new[] { 1, 1, 2, 2 });
        }

        [Test]
        public void TheFiveMovementCards_AreTheExtendOnesOfTheSix()
        {
            // §2.1: six movement-centred cards; 間合い切り (#28) is 型替え, the other five are 伸び.
            var movement = CardCatalog.All.Where(c => AttributeRule.IsMovementCard(c.Attributes, c.Face)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(movement.Count, Is.EqualTo(6));
                Assert.That(movement.Where(c => MasteryProfiles.Find(c.Id)!.Kind == MasteryKind.Extend).Select(c => c.Id),
                    Is.EqualTo(new[] { "footwork", "back_leap", "slide_step", "leap_back", "dash_in" }));
                Assert.That(MasteryProfiles.Find("break_off")!.Kind, Is.EqualTo(MasteryKind.Reform));
            });
        }

        // ---- Helpers ----

        private static void AssertGrowth(string id, Func<CardDef, int> read, int[] byRank)
        {
            var card = CardCatalog.ById(id);
            var profile = MasteryProfiles.Find(id)!;
            var steps = Enumerable.Range(0, 4).Select(rank => Mastery.Master(card, rank, profile)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(profile.Kind, Is.EqualTo(MasteryKind.Extend));
                Assert.That(steps.Select(read), Is.EqualTo(byRank));
                foreach (var step in steps.Skip(1))
                {
                    // Only the extra grows: the move, the column, the cost and the trait stay.
                    Assert.That(step.Face.Move, Is.EqualTo(card.Face.Move), step.Name);
                    Assert.That(step.Column, Is.EqualTo(card.Column), step.Name);
                    Assert.That(step.Cost, Is.EqualTo(1), step.Name);
                    Assert.That(step.Trait, Is.EqualTo(card.Trait), step.Name);
                    Assert.That(step.Face.Power, Is.EqualTo(0), step.Name);
                    Assert.That(FocusStep.Of(step.Attributes, step.Column, step.Face), Is.EqualTo(FocusStep.None),
                        "集中 still does not grow a movement card");
                }
                // The other extras stay where they were.
                Assert.That(steps[3].Face.Guard + steps[3].Face.Draw + steps[3].Face.StaminaGain - read(steps[3]),
                    Is.EqualTo(card.Face.Guard + card.Face.Draw + card.Face.StaminaGain - read(card)));
                Assert.That(steps.Skip(1).Select(c => c.Name), Is.EqualTo(profile.RankNames));
            });
        }

        private static int IndexOf(string id)
        {
            for (int i = 0; i < CardCatalog.All.Count; i++)
            {
                if (CardCatalog.All[i].Id == id) return i;
            }
            return -1;
        }

        private static string KindWord(MasteryKind kind) => kind switch
        {
            MasteryKind.Extend => "伸び",
            MasteryKind.Lighten => "軽さ",
            MasteryKind.Reform => "型替え",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        /// <summary>A condition as §4.6 writes it (§1.2's words).</summary>
        private static string ConditionWord(Trait trait) => trait.Condition switch
        {
            TraitCondition.Broken => "崩し後",
            TraitCondition.FirstPlay => "初手",
            TraitCondition.Finisher => "締め",
            TraitCondition.Chain => "連打",
            TraitCondition.Reserve => $"温存(残 ≥ {trait.Threshold})",
            TraitCondition.OmenIs => $"予兆({OmenWord(trait.Omen)})",
            TraitCondition.Combo => $"連動({AttributeWord(trait.Attribute)})",
            _ => throw new ArgumentOutOfRangeException(nameof(trait), trait.Condition, "§4.6 does not loosen to this condition."),
        };

        private static string OmenWord(OmenKind? omen) => omen switch
        {
            OmenKind.Attack => "攻撃",
            OmenKind.Guard => "防御",
            OmenKind.Move => "移動",
            _ => throw new ArgumentOutOfRangeException(nameof(omen), omen, null),
        };

        private static string AttributeWord(BattleAttribute attribute) => attribute switch
        {
            BattleAttribute.Attack => "A",
            BattleAttribute.Guard => "G",
            BattleAttribute.Skill => "Sk",
            BattleAttribute.Stance => "St",
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
        };

        private sealed class Row
        {
            public int Number;
            public string Name = "";
            public int Column;
            public string Kind = "";
            public string[] Names = Array.Empty<string>();
            public string Conditions = "";
        }

        private static string? FindCanon()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, CanonPath);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>The rows of §4.6's first table: # / 名前 / 列 / 型 / 段 1 / 段 2 / 段 3 / 型替えの条件.</summary>
        private static List<Row> ReadTable(string path)
        {
            var rows = new List<Row>();
            bool inside = false;
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith("### 4.6", StringComparison.Ordinal)) { inside = true; continue; }
                if (!inside) continue;
                if (line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("### ", StringComparison.Ordinal)) break;
                if (!line.StartsWith("|", StringComparison.Ordinal)) continue;

                string[] cells = line.Split('|').Select(c => c.Trim()).ToArray();
                if (cells.Length < 10 || !int.TryParse(cells[1], out int number)) continue;
                rows.Add(new Row
                {
                    Number = number,
                    Name = cells[2],
                    Column = int.Parse(cells[3]),
                    Kind = cells[4],
                    Names = new[] { cells[5], cells[6], cells[7] },
                    Conditions = cells[8],
                });
            }
            return rows;
        }
    }
}
