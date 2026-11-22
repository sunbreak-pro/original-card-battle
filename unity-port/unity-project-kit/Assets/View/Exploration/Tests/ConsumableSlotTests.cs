// #651: the exploration screen's consumable slots hold every catalogue consumable. 薬草 heals from a
// slot; 標の石 sits in one but cannot be used until a screen picks the node to jump to.
using System.Linq;
using DungeonContent;
using DungeonCore;
using Exploration;
using NUnit.Framework;
using static Exploration.Tests.ExplorationScreenTests;

namespace Exploration.Tests
{
    public class ConsumableSlotTests
    {
        [Test]
        public void EveryCatalogueConsumable_HasAnItemTheCoreCarries()
        {
            foreach (ItemDef item in ItemCatalogue.Consumables)
            {
                ReliefItem carried = RunStart.Consumable(item.Id);
                Assert.That(carried.Id, Is.EqualTo(item.Id));
            }
            Assert.That(RunStart.Consumable("yakuso").Kind, Is.EqualTo(ReliefKind.HealHp));
            Assert.That(RunStart.Consumable("yakuso").Amount, Is.EqualTo(20));
            Assert.That(RunStart.Consumable("shirube_no_ishi").Kind, Is.EqualTo(ReliefKind.Teleport));
            Assert.That(RunStart.Consumable("hayagake_no_gan").Kind, Is.EqualTo(ReliefKind.Haste));
        }

        [Test]
        public void AHasteInASlot_MakesTheNextNewNodeFree()
        {
            // tools_and_consumables_v4.md §2.4: 早駆けの丸薬 — the next new node takes no day.
            var session = Explorable(new ExplorationSession(new RunStart { Loadout = Carrying("hayagake_no_gan") }));
            int days = session.State.DaysLeft;
            Assert.That(session.UseConsumable(0), Is.True);
            Assert.That(session.State.NextStepFree, Is.True);
            Assert.That(session.State.DaysLeft, Is.EqualTo(days), "using it takes no day");

            Step(session, session.State.ReachableUnresolved().First());
            Assert.That(session.State.DaysLeft, Is.EqualTo(days), "the node it was used for takes no day");
            Assert.That(session.State.NextStepFree, Is.False);

            Step(session, session.State.ReachableUnresolved().First());
            Assert.That(session.State.DaysLeft, Is.EqualTo(days - 1), "the node after it does");
        }

        [Test]
        public void AHerbInASlot_HealsFromTheScreen()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Hp = 20, Loadout = Carrying("yakuso") }));
            Assert.That(session.Screen().Consumables[0].CanUse, Is.True);
            Assert.That(session.UseConsumable(0), Is.True);
            Assert.That(session.State.Hp, Is.EqualTo(30));
        }

        [Test]
        public void AStoneInASlot_IsShownButCannotBeUsed()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Loadout = Carrying("shirube_no_ishi") }));
            ExplorationState before = session.State;

            Assert.That(session.Screen().Consumables[0].Name, Is.EqualTo("標の石"));
            Assert.That(session.Screen().Consumables[0].CanUse, Is.False);
            Assert.That(session.UseConsumable(0), Is.False);
            Assert.That(session.State, Is.SameAs(before), "nothing changed");
        }

        [Test]
        public void AConsumableTaken_ShowsInTheNextFreeSlot()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Loadout = Carrying("hayagake_no_gan") }));
            Assert.That(session.TakeConsumable(RunStart.Consumable("wasuremizu")), Is.True);
            Assert.That(session.Screen().Consumables.Select(s => s.Name).Take(2), Is.EqualTo(new[] { "早駆けの丸薬", "忘れ水" }));
        }

        private static RunLoadout Carrying(params string[] ids) => new RunLoadout(null, ids.Select(RunStart.Consumable).ToList());
    }
}
