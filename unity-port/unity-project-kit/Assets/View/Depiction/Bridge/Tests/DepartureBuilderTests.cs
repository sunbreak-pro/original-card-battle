// The departure screen of the demo (#58): the slots, the start a tool moves back, the talent, and
// the run that sets out with them — single battles and the chain alike. The options here stand in
// for the dungeon side's catalogue, which this assembly does not reference; the real catalogue's
// mapping is held by DepartureSuppliesTests (dotnet test only).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class DepartureBuilderTests
    {
        private const string Shoes = "maai_no_kutsu";

        private static DepartureBuilder Builder()
        {
            var tools = new[]
            {
                new SupplyOption("bosho_no_men", "防瘴の面", "層の濃度を 1 下げる"),
                new SupplyOption(Shoes, "間合いの履", "開始のマスが 2 から 1 になる", 1),
                new SupplyOption("ikitsugi_no_fue", "息継ぎの笛", "戦闘開始時のスタミナ +3"),
            };
            var consumables = new[]
            {
                new SupplyOption("yakuso", "薬草", "HP が最大の 20% 戻る"),
                new SupplyOption("wasuremizu", "忘れ水", "いまのスタミナが全回復"),
            };
            return new DepartureBuilder(tools, consumables);
        }

        private static DeckBuilder Deck() => DeckBuilder.Prototype();

        /// <summary>A builder over the prototype deck with its first kind as the talent: ready to set out.</summary>
        private static DepartureBuilder Ready(DeckBuilder deck)
        {
            DepartureBuilder builder = Builder();
            Assert.That(builder.ChooseTalent(deck, PrototypeDeck.Kinds[0].Id), Is.True);
            return builder;
        }

        // ---- the slots ----

        [Test]
        public void ThreeToolsFit_TheSameOneTwice_AFourthDoesNot()
        {
            DepartureBuilder builder = Builder();
            Assert.That(builder.AddTool("bosho_no_men"), Is.True);
            Assert.That(builder.AddTool("bosho_no_men"), Is.True, "the same tool may fill two slots");
            Assert.That(builder.AddTool(Shoes), Is.True);
            Assert.That(builder.CanAddTool("ikitsugi_no_fue"), Is.False);
            Assert.That(builder.AddTool("ikitsugi_no_fue"), Is.False);
            Assert.That(builder.ToolCount("bosho_no_men"), Is.EqualTo(2));
            Assert.That(builder.ToolsLine, Is.EqualTo("ツール 3 / 3: 防瘴の面、防瘴の面、間合いの履"));

            Assert.That(builder.RemoveTool("bosho_no_men"), Is.True);
            Assert.That(builder.Tools, Is.EqualTo(new[] { "bosho_no_men", Shoes }));
        }

        [Test]
        public void ThreeConsumablesFit_AFourthDoesNot_AndAnUnknownIdNever()
        {
            DepartureBuilder builder = Builder();
            Assert.That(builder.AddConsumable("nothing"), Is.False);
            Assert.That(builder.AddTool("yakuso"), Is.False, "a consumable does not go in a tool slot");
            for (int i = 0; i < DeckRules.ConsumableSlots; i++) Assert.That(builder.AddConsumable("yakuso"), Is.True);
            Assert.That(builder.AddConsumable("wasuremizu"), Is.False);
            Assert.That(builder.ConsumablesLine, Is.EqualTo("消耗品 3 / 3: 薬草、薬草、薬草"));
            Assert.That(Builder().ConsumablesLine, Is.EqualTo("消耗品 0 / 3: なし"));
        }

        // ---- the start ----

        [Test]
        public void TheShoesMoveTheStartToCellOneAtGapFour_ASecondPairDoesNothingMore()
        {
            DepartureBuilder builder = Builder();
            Assert.That(builder.StartLine, Is.EqualTo("開始のマス 2・開始の間合い 3"));

            builder.AddTool(Shoes);
            Assert.That(builder.PlayerStartCell, Is.EqualTo(1));
            Assert.That(builder.StartGap, Is.EqualTo(4));
            Assert.That(builder.StartLine, Is.EqualTo("開始のマス 1・開始の間合い 4（間合いの履）"));

            builder.AddTool(Shoes);
            Assert.That(builder.PlayerStartCell, Is.EqualTo(1));
            Assert.That(builder.StartGap, Is.EqualTo(4));
        }

        [TestCase(false, 2, 3)]
        [TestCase(true, 1, 4)]
        public void TheStartReachesTheBattle_InASingleBattleAndInTheChain(bool shoes, int cell, int gap)
        {
            DeckBuilder deck = Deck();
            DepartureBuilder builder = Ready(deck);
            if (shoes) builder.AddTool(Shoes);
            Departure departure = builder.Build(deck);

            BattleState single = DemoSession.Single(departure, "polearm_warped", 1).StartBattle().State;
            BattleState chain = DemoSession.Chain(departure, 1).StartBattle().State;
            foreach (BattleState state in new[] { single, chain })
            {
                Assert.That(state.Player.Cell, Is.EqualTo(cell));
                Assert.That(state.Gap, Is.EqualTo(gap));
            }
        }

        [Test]
        public void TheEnemyKeepsItsCell_AndTheLineItsWidth_WhenTheStartMovesBack()
        {
            DeckBuilder deck = Deck();
            DepartureBuilder plain = Ready(deck);
            DepartureBuilder shod = Ready(deck);
            shod.AddTool(Shoes);
            foreach (EnemyDef enemy in Enemies.All)
            {
                BattleSetup a = DemoSession.Single(plain.Build(deck), enemy.Id, 1).NextSetup();
                BattleSetup b = DemoSession.Single(shod.Build(deck), enemy.Id, 1).NextSetup();
                Assert.That(b.FieldCells, Is.EqualTo(a.FieldCells), enemy.Id);
                Assert.That(b.EnemyStartCells, Is.EqualTo(a.EnemyStartCells), enemy.Id);
                Assert.That(b.PlayerStartCell, Is.EqualTo(1), enemy.Id);
            }
        }

        // ---- the deck reaches the battle ----

        [Test]
        public void TheDrawPileIsTheChosenDeck()
        {
            DeckBuilder deck = DeckBuilder.Random(17, 33);
            DepartureBuilder builder = Builder();
            builder.ChooseTalent(deck, DepartureBuilder.TalentChoices(deck)[0].Id);
            Departure departure = builder.Build(deck);

            foreach (DemoSession session in new[] { DemoSession.Single(departure, "shadow_hound", 4), DemoSession.Chain(departure, 4) })
            {
                BattleState state = session.StartBattle().State;
                var expected = deck.Build().Select(c => c.InstanceId).OrderBy(id => id, StringComparer.Ordinal).ToList();
                var drawn = state.DrawPile.Concat(state.Hand).Concat(state.DiscardPile).Select(c => c.InstanceId)
                    .OrderBy(id => id, StringComparer.Ordinal).ToList();
                Assert.That(drawn, Is.EqualTo(expected));
            }
        }

        // ---- the talent ----

        [Test]
        public void TheTalentIsRecorded_AndReachesTheRun()
        {
            DeckBuilder deck = Deck();
            DepartureBuilder builder = Builder();
            string id = PrototypeDeck.Kinds[3].Id;

            Assert.That(builder.ChooseTalent(deck, id), Is.True);
            Departure departure = builder.Build(deck);

            Assert.That(departure.TalentCardId, Is.EqualTo(id));
            Assert.That(DemoSession.Single(departure, "polearm_warped", 1).Departure.TalentCardId, Is.EqualTo(id));
            Assert.That(DemoSession.Chain(departure, 1).Departure.TalentCardId, Is.EqualTo(id));
            Assert.That(builder.TalentLine(deck), Is.EqualTo("才能: " + PrototypeDeck.Kinds[3].Name));
        }

        [Test]
        public void OnlyAKindOfTheDeckCanBeTheTalent_AndOneThatLeavesIsDropped()
        {
            DeckBuilder deck = Deck();
            DepartureBuilder builder = Builder();
            string outside = CardCatalog.All.First(d => deck.CountOf(d.Id) == 0).Id;
            Assert.That(builder.ChooseTalent(deck, outside), Is.False);
            Assert.That(builder.TalentLine(deck), Is.EqualTo("才能: まだ選んでいません"));

            string id = PrototypeDeck.Kinds[0].Id;
            builder.ChooseTalent(deck, id);
            deck.Remove(id);
            deck.Remove(id);
            Assert.That(builder.TalentIn(deck), Is.Null, "the talent left with its last copy");
            Assert.That(builder.Build(deck).TalentCardId, Is.Null);
        }

        [Test]
        public void TheTalentChoices_AreTheDecksKindsInCanonOrder_PagedAndClamped()
        {
            DeckBuilder deck = Deck();
            Assert.That(DepartureBuilder.TalentChoices(deck).Select(d => d.Id),
                Is.EqualTo(CardCatalog.All.Where(d => deck.CountOf(d.Id) > 0).Select(d => d.Id)));

            int kinds = DepartureBuilder.TalentChoices(deck).Count;
            DeckPage last = DepartureBuilder.TalentPage(deck, 99, 4);
            Assert.That(last.Index, Is.EqualTo((kinds - 1) / 4));
            Assert.That(last.Cards.Count, Is.EqualTo(kinds - last.Index * 4));
        }

        // ---- setting out ----

        [Test]
        public void ItSetsOutOnlyWithALegalDeckAndATalent()
        {
            DeckBuilder deck = Deck();
            DepartureBuilder builder = Builder();
            Assert.That(builder.CanSetOut(deck), Is.False);
            Assert.That(builder.StatusText(deck), Is.EqualTo("才能にする札を 1 枚選ぶと出立できます"));

            builder.ChooseTalent(deck, PrototypeDeck.Kinds[0].Id);
            Assert.That(builder.CanSetOut(deck), Is.True);
            Assert.That(builder.StatusText(deck), Is.EqualTo("出立できます"));

            // Nineteen cards: the deck screen would not have let it through, and neither does this one.
            deck.Remove(PrototypeDeck.Kinds[5].Id);
            Assert.That(deck.Total, Is.EqualTo(19));
            Assert.That(builder.CanSetOut(deck), Is.False);
            Assert.That(builder.StatusText(deck), Is.EqualTo("デッキが決まりに合っていません。デッキに戻って直してください"));
        }

        [Test]
        public void TheDeckScreen_RefusesAKindNotOwned()
        {
            var owned = CardCatalog.All.Skip(1).Select(d => d.Id).ToList();
            var deck = new DeckBuilder(owned);
            string notOwned = CardCatalog.All[0].Id;

            Assert.That(deck.CanAdd(notOwned), Is.False);
            Assert.That(deck.Add(notOwned), Is.False);
            Assert.That(deck.CanAdd(CardCatalog.All[1].Id), Is.True);
            Assert.That(new DeckBuilder().Owned.Count, Is.EqualTo(CardCatalog.All.Count), "the demo owns the whole catalog");
        }

        [Test]
        public void TheSlotsAreTheCoresThree()
        {
            Assert.That(DeckRules.ToolSlots, Is.EqualTo(3));
            Assert.That(DeckRules.ConsumableSlots, Is.EqualTo(3));
            Assert.That(DepartureBuilder.Title, Is.EqualTo("出立の支度（ツール 3 枠・消耗品 3 枠・才能 1 枚）"));
        }

        [Test]
        public void ASupplyTheBattleDoesNotFeel_SaysSo()
        {
            DepartureBuilder builder = Builder();
            Assert.That(DepartureBuilder.LineOf(builder.ToolOptions[1]), Is.EqualTo("間合いの履　開始のマスが 2 から 1 になる"));
            Assert.That(DepartureBuilder.LineOf(builder.ToolOptions[2]), Does.EndWith("（この試験の戦闘では効果が出ません）"));
        }
    }
}
