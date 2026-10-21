// #649: the hand-over's tables and checks that would otherwise let a mistake through in silence. A
// real boss or elite with no row in the unique-card table teaches nothing and no check stops it; a
// hand-in missing an enemy's 開示度 used to read 0; a battle that never ended used to be read by the
// View itself. Runs under `dotnet test` (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonCore;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class UniqueCardsTests
    {
        // ---- the unique-card table ----

        [Test]
        public void EveryRealBoss_HasARowInTheUniqueCardTable()
        {
            var missing = RealEnemies(table => table.Boss).Where(id => UniqueCards.OfBoss(id).Count == 0).ToList();
            Assert.That(missing, Is.Empty, "a real boss in LayerEncounters with no row in UniqueCards.BossCards");
        }

        [Test]
        public void EveryRealElite_HasARowInTheUniqueCardTable()
        {
            var missing = RealEnemies(table => table.Elite).Where(id => UniqueCards.OfElite(id).Count == 0).ToList();
            Assert.That(missing, Is.Empty, "a real elite in LayerEncounters with no row in UniqueCards.EliteCards");
        }

        [Test]
        public void TheLayerTables_HoldRealBossesAndElites_ToCheck()
        {
            // The two tests above pass on an empty list; this keeps them from passing for that reason.
            Assert.Multiple(() =>
            {
                Assert.That(RealEnemies(table => table.Boss).Count, Is.EqualTo(3), "セルク / ガルド / 歪みの根");
                Assert.That(RealEnemies(table => table.Elite).Count, Is.EqualTo(3), "門竜 / 統牙の長竜 / 不退の槍竜");
            });
        }

        // ---- the hand-in ----

        [Test]
        public void AHandIn_MissingAnEnemysDisclosure_Throws()
        {
            var encounter = new Encounter(new[] { new Fight(new[] { "polearm_warped", "crossbow_hunter" }) }, isStandIn: true);
            var partial = new Dictionary<string, int> { ["polearm_warped"] = 1 };
            Assert.Throws<ArgumentException>(() => HandIn(encounter, partial));
        }

        [Test]
        public void AHandIn_WithEveryEnemysDisclosure_IsMade()
        {
            var encounter = new Encounter(new[] { new Fight(new[] { "polearm_warped", "crossbow_hunter" }) }, isStandIn: true);
            var full = new Dictionary<string, int> { ["polearm_warped"] = 1, ["crossbow_hunter"] = 2 };
            Assert.That(new EncounterBattle(HandIn(encounter, full)).NextLaunch().Disclosure, Is.EqualTo(1), "the lower of the two");
        }

        // ---- the end of a fight ----

        [Test]
        public void AFightThatNeverEnded_IsNotFinished()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EncounterBattle battle = expedition.StartEncounter();
            CoreBattleSource source = battle.StartNext();

            Assert.Multiple(() =>
            {
                Assert.That(battle.TryFinish(source), Is.False);
                Assert.That(battle.Fought.Count, Is.EqualTo(0));
                Assert.That(battle.Over, Is.False);
            });
        }

        [Test]
        public void AFightThatEnded_IsFinished()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            EncounterBattle battle = expedition.StartEncounter();
            CoreBattleSource source = battle.StartNext(suggestCards: true);
            int guard = 0;
            while (!source.Finished && guard++ < 2000)
            {
                if (!source.WaitingForPlayer)
                {
                    source.AdvanceAuto();
                    continue;
                }
                string card = source.SuggestedCardId;
                if (card.Length == 0)
                {
                    source.EndTurn();
                    continue;
                }
                CardFace face = DepictionText.Find(source.Frame.Hand, card);
                Assert.That(source.TryPlay(card, DepictionText.RequiredZone(face.Aim), out _), Is.EqualTo(PlayVerdict.Accepted));
            }
            Assert.That(source.Finished, Is.True, "the suggested cards play the fight out");

            Assert.Multiple(() =>
            {
                Assert.That(battle.TryFinish(source), Is.True);
                Assert.That(battle.Fought.Count, Is.EqualTo(1));
            });
        }

        // ---- helpers ----

        /// <summary>The single enemy of every encounter the layer tables do not flag as a stand-in, in the pools picked.</summary>
        private static List<string> RealEnemies(Func<LayerEncounterTable, IReadOnlyList<Encounter>> pool) =>
            LayerEncounters.All
                .SelectMany(pool)
                .Where(encounter => !encounter.IsStandIn)
                .SelectMany(encounter => encounter.EnemyIds)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        private static BattleHandIn HandIn(Encounter encounter, IReadOnlyDictionary<string, int> disclosure)
        {
            var deck = PrototypeDeck.Build();
            return new BattleHandIn(
                2, NodeKind.Elite, encounter, encounter.Fights.Select(_ => 7).ToList(),
                Constants.PlayerMaxHp, Constants.PlayerMaxHp, Constants.BaseMaxStamina, Constants.BaseMaxStamina, 0,
                disclosure, deck, Departure.Plain(deck), 11);
        }
    }
}
