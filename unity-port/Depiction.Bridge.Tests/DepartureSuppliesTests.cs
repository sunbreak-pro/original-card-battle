// dotnet test only: not under the Unity kit, so `unity:sync` never copies it. Unity's
// Depiction.Bridge.Tests assembly does not reference DungeonContent, and the mapping it checks
// (Assets/View/Depiction/DepartureSupplies.cs) lives in Assembly-CSharp, which a test asmdef cannot
// reference. This project compiles that one file against DungeonContent instead (#58).
using System.Linq;
using BattleCore;
using Depiction.View;
using DungeonContent;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class DepartureSuppliesTests
    {
        [Test]
        public void TheScreenOffersTheWholeCatalogue_InItsOrder()
        {
            Assert.That(DepartureSupplies.Tools.Select(o => o.Id), Is.EqualTo(ItemCatalogue.Tools.Select(i => i.Id)));
            Assert.That(DepartureSupplies.Consumables.Select(o => o.Id), Is.EqualTo(ItemCatalogue.Consumables.Select(i => i.Id)));
            Assert.That(DepartureSupplies.Tools.Select(o => o.Name), Is.EqualTo(ItemCatalogue.Tools.Select(i => i.Name)));
            Assert.That(DepartureSupplies.Tools.Select(o => o.Note), Is.EqualTo(ItemCatalogue.Tools.Select(i => i.Note)));
        }

        [Test]
        public void TheShoesAreTheFifthTool()
        {
            // The PlayMode demo test presses Tools/Row4 to pick 間合いの履; this keeps that row honest.
            Assert.That(DepartureSupplies.Tools.Select(o => o.Id).ToList().IndexOf("maai_no_kutsu"), Is.EqualTo(4));
        }

        [Test]
        public void TheSlotsAreTheSameOnBothSides()
        {
            Assert.That(DeckRules.ToolSlots, Is.EqualTo(Loadout.ToolSlots));
            Assert.That(DeckRules.ConsumableSlots, Is.EqualTo(Loadout.ConsumableSlots));
        }

        [Test]
        public void OnlyTheShoesMoveTheStart_OneCellBack()
        {
            var movers = DepartureSupplies.Tools.Where(o => o.StartCellsBack > 0).ToList();
            Assert.That(movers.Select(o => o.Id), Is.EqualTo(new[] { "maai_no_kutsu" }));
            Assert.That(movers[0].StartCellsBack, Is.EqualTo(1));
            Assert.That(DepartureSupplies.Consumables.All(o => o.StartCellsBack == 0), Is.True);
        }

        [Test]
        public void TheOtherThreeBattleTools_CarryTheirAmounts_AndOnlyTheExplorationOnesKeepTheSuffix()
        {
            // #452: each battle tool reaches the core with the catalogue's Amount; nothing else does.
            var byId = DepartureSupplies.Tools.ToDictionary(o => o.Id);
            Assert.That(byId["ikitsugi_no_fue"].StartStaminaBonus, Is.EqualTo(3));
            Assert.That(byId["ryurin_no_kakera"].FirstHitGuard, Is.EqualTo(5));
            Assert.That(byId["ryugo_no_fu"].BossWordWard, Is.EqualTo(1));
            Assert.That(DepartureSupplies.Tools.Where(o => o.StartStaminaBonus > 0).Select(o => o.Id), Is.EqualTo(new[] { "ikitsugi_no_fue" }));
            Assert.That(DepartureSupplies.Tools.Where(o => o.FirstHitGuard > 0).Select(o => o.Id), Is.EqualTo(new[] { "ryurin_no_kakera" }));
            Assert.That(DepartureSupplies.Tools.Where(o => o.BossWordWard > 0).Select(o => o.Id), Is.EqualTo(new[] { "ryugo_no_fu" }));
            Assert.That(DepartureSupplies.Consumables.All(o => !o.WorksInBattle), Is.True);

            // The screen's suffix stays on exactly the tools the dungeon side reads.
            foreach (ItemDef item in ItemCatalogue.Tools)
            {
                string line = DepartureBuilder.LineOf(DepartureSupplies.Of(item));
                bool battle = item.Effect.Side() == EffectSide.Battle;
                Assert.That(line.EndsWith("（この試験の戦闘では効果が出ません）"), Is.EqualTo(!battle), item.Id);
            }
        }

        [Test]
        public void TheFluteFromTheCatalogue_OpensTheBattleThreePastTheMaximum()
        {
            DeckBuilder deck = DeckBuilder.Prototype();
            DepartureBuilder builder = DepartureSupplies.NewBuilder();
            builder.ChooseTalent(deck, PrototypeDeck.Kinds[0].Id);
            Assert.That(builder.AddTool("ikitsugi_no_fue"), Is.True);

            BattleState state = DemoSession.Chain(builder.Build(deck), 2).StartBattle().State;
            Assert.That(state.Player.Stamina, Is.EqualTo(Constants.BaseMaxStamina + 3));
            Assert.That(state.Player.MaxStamina, Is.EqualTo(Constants.BaseMaxStamina));
        }

        [Test]
        public void TheShoesFromTheCatalogue_StartTheBattleOnCellOneAtGapFour()
        {
            DeckBuilder deck = DeckBuilder.Prototype();
            DepartureBuilder builder = DepartureSupplies.NewBuilder();
            builder.ChooseTalent(deck, PrototypeDeck.Kinds[0].Id);
            Assert.That(builder.AddTool("maai_no_kutsu"), Is.True);
            Assert.That(builder.CanSetOut(deck), Is.True);

            BattleState state = DemoSession.Chain(builder.Build(deck), 2).StartBattle().State;
            Assert.That(state.Player.Cell, Is.EqualTo(1));
            Assert.That(state.Gap, Is.EqualTo(4));
        }

        [Test]
        public void EveryOptionReadsAsALoadout()
        {
            // What the screen sets out with is something the dungeon side's Loadout takes as it is.
            DepartureBuilder builder = DepartureSupplies.NewBuilder();
            builder.AddTool("maai_no_kutsu");
            builder.AddTool("bosho_no_men");
            builder.AddTool("bosho_no_men");
            builder.AddConsumable("yakuso");
            Loadout loadout = Loadout.Of(builder.Tools, builder.Consumables);
            Assert.That(loadout.Has(ItemEffect.WiderStart), Is.True);
            Assert.That(loadout.MiasmaDensityRelief, Is.EqualTo(2));
        }
    }
}
