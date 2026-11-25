// #648: the hand-over reads the values it shares with the battle from where they are defined, not
// from its own copies: the starter's kinds from BattleCore.CardCatalog.Starter (#1038) and the in-battle tools'
// numbers from DungeonContent.BattleToolAmounts. Runs under `dotnet test`
// (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System.Linq;
using BattleCore;
using DungeonContent;
using DungeonCore;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class ExpeditionSourcesTests
    {
        [Test]
        public void TheInitialKinds_AreTheCatalogsStarter()
        {
            Assert.That(Expedition.InitialKinds, Is.EqualTo(CardCatalog.Starter.Select(d => d.Id)));
            Assert.That(Expedition.InitialKinds.Count, Is.EqualTo(11), "swordsman_cards_v4 v4.15 §2.1 (#1035, #1038)");
        }

        [Test]
        public void ANewExpedition_OwnsTheStartersElevenKinds_AndSetsOutWithTheStarterDeck()
        {
            // #1038: the first expedition owns the eleven kinds of swordsman_cards_v4 §2.1 and nothing else.
            var expedition = new Expedition(new RunStart { Seed = 3 });
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Owned, Is.EquivalentTo(new[]
                {
                    "wrist_cut", "kesa_cut", "side_sweep", "observe", "brace", "iron_block",
                    "footwork", "leap_back", "dash_in", "water_stance", "iron_wall",
                }));
                Assert.That(expedition.Deck.Select(c => c.InstanceId), Is.EqualTo(StarterDeck.Build().Select(c => c.InstanceId)));
                Assert.That(Cards.Validate(expedition.Deck).Ok, Is.True);
                Assert.That(DeckRules.ValidateDeck(expedition.Deck, expedition.Owned.ToList()).Ok, Is.True);
            });
        }

        [Test]
        public void TheInBattleTools_AreTheSharedMappingSummedOverTheSlots()
        {
            var tools = new[] { "ryugo_no_fu", "ikitsugi_no_fue", "ryugo_no_fu" };
            BattleToolAmounts expected = tools
                .Select(id => BattleToolAmounts.Of(ItemCatalogue.Tool(id)))
                .Aggregate(BattleToolAmounts.None, (sum, a) => sum.Plus(a));

            Departure departure = new Expedition(new RunStart { Seed = 3 }, battleToolIds: tools).Tools;
            Assert.Multiple(() =>
            {
                Assert.That(departure.StartCellsBack, Is.EqualTo(expected.StartCellsBack));
                Assert.That(departure.StartStaminaBonus, Is.EqualTo(expected.StartStaminaBonus));
                Assert.That(departure.FirstHitGuard, Is.EqualTo(expected.FirstHitGuard));
                Assert.That(departure.BossWordWard, Is.EqualTo(expected.BossWordWard));
                Assert.That(departure.BossWordWard, Is.EqualTo(2), "the same tool in two slots counts twice");
            });
        }
    }
}
