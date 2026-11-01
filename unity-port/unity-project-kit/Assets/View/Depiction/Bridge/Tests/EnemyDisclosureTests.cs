// #589: the journal's 開示度 per enemy. Each enemy's omen shows what the journal has read of that enemy
// (battle-visual-v1 §4.4, battle_ui_ux_v2 §7), and a battle nobody hands a list to keeps its one level
// for every enemy. Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode
// runner from the same file.
using System;
using System.Collections.Generic;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class EnemyDisclosureTests
    {
        /// <summary>roster §9's pair, the one that fights together: 錆槍の竜兵 in front, 灰弩の竜兵 behind.</summary>
        private static BattleLaunch ThePair() => new BattleLaunch
        {
            EnemyId = Enemies.PolearmWarped.Id,
            MoreEnemyIds = new List<string> { Enemies.CrossbowHunter.Id },
            FieldCells = 8,
            Seed = 7,
        };

        /// <summary>Plays on until a frame shows both omens with a number at full disclosure.</summary>
        private static DepictionFrame FirstFrameWithBothOmens(CoreBattleSource source)
        {
            for (int guard = 0; guard < 40 && !source.Finished; guard++)
            {
                if (source.WaitingForPlayer) return source.Frame;
                source.AdvanceAuto();
            }
            throw new InvalidOperationException("the battle never came to the player");
        }

        [Test]
        public void TwoEnemies_WithDifferentLevels_ShowDifferentOmenDetail()
        {
            DepictionFrame full = FirstFrameWithBothOmens(ThePair().CreateSource());
            int numbered = full.Enemies.FindIndex(e => e.Omen.ValueText.Length > 0);
            Assert.That(numbered, Is.GreaterThanOrEqualTo(0), "at full disclosure an omen on the first turn shows its number");
            int other = 1 - numbered;

            BattleLaunch readToNothing = ThePair();
            readToNothing.Disclosures = new List<int> { OmenDisclosure.Full, OmenDisclosure.Full };
            readToNothing.Disclosures[numbered] = OmenDisclosure.Unread;
            DepictionFrame hidden = FirstFrameWithBothOmens(readToNothing.CreateSource());

            BattleLaunch readWhole = ThePair();
            readWhole.Disclosures = new List<int> { OmenDisclosure.Unread, OmenDisclosure.Unread };
            readWhole.Disclosures[numbered] = OmenDisclosure.Full;
            DepictionFrame shown = FirstFrameWithBothOmens(readWhole.CreateSource());

            Assert.Multiple(() =>
            {
                Assert.That(hidden.Enemies[numbered].Omen.Disclosure, Is.EqualTo(OmenDisclosure.Unread));
                Assert.That(hidden.Enemies[other].Omen.Disclosure, Is.EqualTo(OmenDisclosure.Full));
                Assert.That(hidden.Enemies[numbered].Omen.ValueText, Is.Empty, "read to 0: no number");
                Assert.That(hidden.Enemies[numbered].Omen.Statuses, Is.Empty, "read to 0: no statuses");
                Assert.That(hidden.Enemies[numbered].Omen.KindLabel, Is.EqualTo(full.Enemies[numbered].Omen.KindLabel), "the kind shows at every level");
                Assert.That(hidden.Enemies[other].Omen.ValueText, Is.EqualTo(full.Enemies[other].Omen.ValueText), "the other enemy, read to 2, is as at full disclosure");

                Assert.That(shown.Enemies[numbered].Omen.Disclosure, Is.EqualTo(OmenDisclosure.Full));
                Assert.That(shown.Enemies[numbered].Omen.ValueText, Is.EqualTo(full.Enemies[numbered].Omen.ValueText), "read to 2: its number");
                Assert.That(shown.Enemies[other].Omen.Disclosure, Is.EqualTo(OmenDisclosure.Unread));
                Assert.That(shown.Enemies[other].Omen.ValueText, Is.Empty, "the other enemy, read to 0, shows none");
            });
        }

        [Test]
        public void AnElitesPlan_IsKept_ForTheEnemyReadFarEnough_AndOffTheOtherOne()
        {
            var launch = new BattleLaunch
            {
                EnemyId = Enemies.ArmoredWarden.Id,
                MoreEnemyIds = new List<string> { Enemies.ArmoredWarden.Id },
                FieldCells = 8,
                StartGap = 0, // two elites of two cells each: a gap of 3 leaves the second no room on the line
                Seed = 7,
                Disclosures = new List<int> { OmenDisclosure.Partial, OmenDisclosure.Unread },
            };
            DepictionFrame frame = FirstFrameWithBothOmens(launch.CreateSource());

            Assert.Multiple(() =>
            {
                Assert.That(frame.Enemies[0].Omen.PlanVisible, Is.True, "read to 1: the plan shows");
                Assert.That(frame.Enemies[0].Omen.ValueText, Is.Empty, "read to 1: no number");
                Assert.That(frame.Enemies[1].Omen.PlanVisible, Is.False, "read to 0: no plan");
            });
        }

        [TestCase(OmenDisclosure.Unread)]
        [TestCase(OmenDisclosure.Partial)]
        [TestCase(OmenDisclosure.Full)]
        public void NoList_ReadsTheOneLevel_ForEveryEnemy(int level)
        {
            BattleLaunch launch = ThePair();
            launch.Disclosure = level;
            DepictionFrame frame = FirstFrameWithBothOmens(launch.CreateSource());

            Assert.That(frame.Enemies[0].Omen.Disclosure, Is.EqualTo(level));
            Assert.That(frame.Enemies[1].Omen.Disclosure, Is.EqualTo(level));
        }

        [Test]
        public void AShortList_LeavesTheEnemyPastItsEnd_OnTheOneLevel()
        {
            BattleLaunch launch = ThePair();
            launch.Disclosure = OmenDisclosure.Partial;
            launch.Disclosures = new List<int> { OmenDisclosure.Full };
            DepictionFrame frame = FirstFrameWithBothOmens(launch.CreateSource());

            Assert.That(frame.Enemies[0].Omen.Disclosure, Is.EqualTo(OmenDisclosure.Full));
            Assert.That(frame.Enemies[1].Omen.Disclosure, Is.EqualTo(OmenDisclosure.Partial));
        }

        [TestCase(-2)]
        [TestCase(3)]
        public void ALevelOutsideTheJournalsRange_IsRefused_InTheList(int level)
        {
            BattleLaunch launch = ThePair();
            launch.Disclosures = new List<int> { OmenDisclosure.Full, level };
            Assert.Throws<ArgumentOutOfRangeException>(() => launch.CreateSource());
        }

        [Test]
        public void MinusOne_InTheList_ReadsAsZero()
        {
            BattleLaunch launch = ThePair();
            launch.Disclosures = new List<int> { BattleLaunch.LowestDisclosure, OmenDisclosure.Full };
            DepictionFrame frame = FirstFrameWithBothOmens(launch.CreateSource());

            Assert.That(frame.Enemies[0].Omen.Disclosure, Is.EqualTo(OmenDisclosure.Unread));
        }
    }
}
