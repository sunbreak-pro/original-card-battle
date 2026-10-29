// #648: the hand-over reads the values it shares with the battle from where they are defined, not
// from its own copies: the initial forty from BattleCore.CardCatalog.Initial and the in-battle tools'
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
        public void TheInitialKinds_AreTheCatalogsInitialForty()
        {
            Assert.That(Expedition.InitialKinds, Is.EqualTo(CardCatalog.Initial.Select(d => d.Id)));
            Assert.That(Expedition.InitialKinds.Count, Is.EqualTo(40));
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
                Assert.That(departure.StartStaminaBonus, Is.EqualTo(expected.StartStamina));
                Assert.That(departure.FirstHitGuard, Is.EqualTo(expected.OpeningGuard));
                Assert.That(departure.BossWordWard, Is.EqualTo(expected.BossWordWard));
                Assert.That(departure.BossWordWard, Is.EqualTo(2), "the same tool in two slots counts twice");
            });
        }
    }
}
