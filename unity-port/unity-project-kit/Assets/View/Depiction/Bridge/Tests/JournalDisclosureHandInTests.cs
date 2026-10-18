// #500: the journal's 開示度 reaches the omen once, as the battle starts (battle_ui_ux_v2 §7), through
// BattleLaunch (the Inspector's battle) and DemoSession (the demo's runs). Pins the omen card of a
// real enemy — 獄竜 ガルド at gap 3: 鉤縄を打つ, then 手繰り寄せる planned — at 開示度 1 and 2, with the
// numbers showing from 2 only (#496). Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in
// Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class JournalDisclosureHandInTests
    {
        private static BattleLaunch Gard(int disclosure) => new BattleLaunch
        {
            EnemyId = "abyss_angler",
            FieldCells = DemoSession.FieldCellsFor(Enemies.ById("abyss_angler")),
            Disclosure = disclosure,
        };

        /// <summary>The omen the player first reads: the frame once the source waits for the first card.</summary>
        private static OmenFrame FirstOmen(CoreBattleSource source)
        {
            int guard = 0;
            while (!source.WaitingForPlayer && !source.Finished && guard++ < 50) source.AdvanceAuto();
            Assert.That(source.WaitingForPlayer, Is.True);
            return source.Frame.Omen;
        }

        [Test]
        public void TheLaunch_IsFullyDisclosed_UntilTheJournalHandsALevelIn()
        {
            var launch = new BattleLaunch();
            Assert.That(launch.Disclosure, Is.EqualTo(OmenDisclosure.Full));
            Assert.That(launch.CreateSource().Frame.Omen.Disclosure, Is.EqualTo(2));
        }

        [TestCase(-2)]
        [TestCase(3)]
        public void ALevelOutsideTheJournalsRange_IsRefused(int level)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleLaunch { Disclosure = level }.CreateSource());
        }

        [Test]
        public void MinusOne_HasNoPartsYet_AndReadsAsZero()
        {
            OmenFrame omen = FirstOmen(Gard(-1).CreateSource());
            Assert.That(omen.Disclosure, Is.EqualTo(0));
            Assert.That(omen.PlanVisible, Is.False);
        }

        [Test]
        public void ReadToOne_GardsOmen_ShowsBothKinds_AndNoNumber()
        {
            OmenFrame omen = FirstOmen(Gard(1).CreateSource());
            Assert.Multiple(() =>
            {
                Assert.That(omen.Disclosure, Is.EqualTo(1));
                Assert.That(omen.Visible, Is.True);
                Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Attack));
                Assert.That(omen.KindLabel, Is.EqualTo("攻撃"));
                Assert.That(omen.ValueText, Is.Empty);
                Assert.That(omen.Statuses, Is.Empty);
                Assert.That(omen.PlanVisible, Is.True);
                Assert.That(omen.PlanIcon, Is.EqualTo(OmenIcon.Seal));
                Assert.That(omen.PlanKindLabel, Is.EqualTo("技"));
                Assert.That(omen.PlanValueText, Is.Empty);
                Assert.That(omen.PlanStatuses, Is.Empty);
            });
        }

        [Test]
        public void ReadToTwo_GardsOmen_ShowsTheNumber_AndThePlansStatus()
        {
            OmenFrame omen = FirstOmen(Gard(2).CreateSource());
            Assert.Multiple(() =>
            {
                Assert.That(omen.Disclosure, Is.EqualTo(2));
                Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Attack));
                Assert.That(omen.KindLabel, Is.EqualTo("攻撃"));
                Assert.That(omen.ValueText, Is.EqualTo("12"), "鉤縄を打つ: 威力 9, +3 at gap 3 or more");
                Assert.That(omen.PlanVisible, Is.True);
                Assert.That(omen.PlanIcon, Is.EqualTo(OmenIcon.Seal));
                Assert.That(omen.PlanKindLabel, Is.EqualTo("技"));
                Assert.That(omen.PlanValueText, Is.Empty, "手繰り寄せる has no attack or guard");
                Assert.That(omen.PlanStatuses.Select(s => s.Glyph), Is.EqualTo(new[] { StatusGlyph.Slow }));
            });
        }

        [Test]
        public void TheLevel_DoesNotMoveDuringTheBattle()
        {
            // battle_ui_ux_v2 §7: 戦闘中に開示度は動きません.
            BattleLaunch launch = Gard(1);
            launch.AutoPlay = true;
            launch.StopAfterTurns = 4;
            CoreBattleSource source = launch.CreateSource();
            var omens = new List<OmenFrame> { source.Frame.Omen };
            int guard = 0;
            while (!source.Finished && guard++ < 400)
            {
                if (!source.WaitingForPlayer)
                {
                    omens.Add(source.AdvanceAuto().After.Omen);
                    continue;
                }
                string card = source.SuggestedCardId;
                if (card.Length == 0)
                {
                    omens.Add(source.EndTurn().After.Omen);
                    continue;
                }
                CardFace face = DepictionText.Find(source.Frame.Hand, card);
                Assert.That(source.TryPlay(card, DepictionText.RequiredZone(face.Aim), out DepictionEvent played), Is.EqualTo(PlayVerdict.Accepted));
                omens.Add(played.After.Omen);
            }
            Assert.That(source.Finished, Is.True);
            Assert.That(omens.Select(o => o.Disclosure).Distinct(), Is.EqualTo(new[] { 1 }));
            Assert.That(omens.Where(o => o.ValueText.Length > 0 || o.PlanValueText.Length > 0), Is.Empty);
        }

        [Test]
        public void ARunWithoutAJournal_IsFullyDisclosed()
        {
            DemoSession run = DemoSession.Single(DeckBuilder.Random(9).Build(), "abyss_angler", 3);
            Assert.That(run.DisclosureOf(run.Current), Is.EqualTo(2));
            Assert.That(run.StartBattle().Frame.Omen.Disclosure, Is.EqualTo(2));
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        public void ASingleBattle_StartsAtTheJournalsLevel_ForItsEnemy(int journal, int shown)
        {
            var journalLevels = new Dictionary<string, int> { ["abyss_angler"] = journal };
            DemoSession run = DemoSession.Single(DeckBuilder.Random(9).Build(), "abyss_angler", 3, 0, id => journalLevels[id]);
            Assert.That(run.DisclosureOf(run.Current), Is.EqualTo(shown));
            Assert.That(run.StartBattle().Frame.Omen.Disclosure, Is.EqualTo(shown));
        }

        [Test]
        public void AChain_ReadsEachEnemysOwnLevel()
        {
            var journalLevels = new Dictionary<string, int> { ["polearm_warped"] = 1, ["shadow_hound"] = 2, ["armored_warden"] = 0 };
            DemoSession run = DemoSession.Chain(DeckBuilder.Random(9).Build(), 3, null, 0, id => journalLevels[id]);
            Assert.That(run.Order.Select(run.DisclosureOf), Is.EqualTo(new[] { 1, 2, 0 }));
            Assert.That(run.StartBattle().Frame.Omen.Disclosure, Is.EqualTo(1), "the first battle is polearm_warped's");
        }
    }
}
