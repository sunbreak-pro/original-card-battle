// The deck the demo's deck screen builds (#190): §8's limits, the filters and pages, the presets,
// the saved string, and the battle a built deck starts.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class DeckBuilderTests
    {
        private static DeckBuilder Filled(int count)
        {
            // Two copies of each kind in canon order, so any count up to 135 is reachable (the stance
            // cards stop at three).
            var deck = new DeckBuilder();
            int i = 0;
            while (deck.Total < count)
            {
                deck.Add(CardCatalog.All[i % CardCatalog.All.Count].Id);
                i++;
            }
            return deck;
        }

        // ---- §8 ----

        [Test]
        public void NineteenCards_CannotFight_TwentyAndFortyCan()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Filled(19).IsValid, Is.False);
                Assert.That(Filled(19).Validate().Errors.Single(), Does.Contain("minimum is 20"));
                Assert.That(Filled(19).StatusText, Is.EqualTo("19 枚です。あと 1 枚入れると戦えます"));
                Assert.That(Filled(20).IsValid, Is.True);
                Assert.That(Filled(40).IsValid, Is.True);
                Assert.That(Filled(40).StatusText, Is.EqualTo("40 枚です。20〜40 枚、1 種 3 枚、構え 3 枚までを満たしています"));
            });
        }

        [Test]
        public void TheFortyFirstCard_IsRefused()
        {
            var deck = Filled(40);
            // Not a stance card: Filled(40) already holds three, and that alone would refuse it.
            string next = CardCatalog.All.First(c => deck.CountOf(c.Id) == 0 && !Cards.IsStanceCard(c)).Id;
            Assert.Multiple(() =>
            {
                Assert.That(deck.CanAdd(next), Is.False);
                Assert.That(deck.Add(next), Is.False);
                Assert.That(deck.Total, Is.EqualTo(40));
            });
        }

        [Test]
        public void TheFourthCopy_IsRefused_AndAnUnknownIdToo()
        {
            var deck = new DeckBuilder();
            for (int i = 0; i < 3; i++) Assert.That(deck.Add("thrust"), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(deck.Add("thrust"), Is.False);
                Assert.That(deck.CountOf("thrust"), Is.EqualTo(3));
                Assert.That(deck.CountText("thrust"), Is.EqualTo("×3"));
                Assert.That(deck.Add("nothing"), Is.False);
                Assert.That(deck.Add(null), Is.False);
            });
        }

        [Test]
        public void TheFourthStanceCard_IsRefused_WhicheverKindItIs()
        {
            // §19.6 S15: three cards with a stance face across kinds; the three of a kind is a separate limit.
            var deck = new DeckBuilder();
            deck.Add("water_stance");
            deck.Add("water_stance");
            deck.Add("rock_stance");
            Assert.Multiple(() =>
            {
                Assert.That(deck.StanceCount, Is.EqualTo(3));
                Assert.That(deck.CanAdd("flow_stance"), Is.False);
                Assert.That(deck.Add("iron_wall"), Is.False, "ガード + スタンス counts too");
                Assert.That(deck.Add("water_stance"), Is.False, "a third copy would pass the three of a kind, not the three stances");
                Assert.That(deck.CanAdd("thrust"), Is.True);
                Assert.That(deck.StanceCount, Is.EqualTo(3));
            });
            deck.Remove("rock_stance");
            Assert.That(deck.CanAdd("flow_stance"), Is.True, "one out, one may come in");
        }

        [Test]
        public void TheRandomPreset_NeverHoldsMoreThanThreeStanceCards()
        {
            // §19.6 S15, at every size the preset builds. Before it, most 30-card decks held four or more.
            foreach (int size in new[] { Constants.DeckMin, DeckBuilder.RandomDefaultSize, Constants.DeckMax })
            {
                for (int seed = 0; seed < 1000; seed++)
                {
                    var deck = DeckBuilder.Random(seed, size);
                    int stances = deck.Build().Count(c => Cards.IsStanceCard(c.Def));
                    Assert.That(stances, Is.LessThanOrEqualTo(Constants.StanceCardsMax), "seed " + seed + " size " + size);
                    Assert.That(deck.StanceCount, Is.EqualTo(stances), "seed " + seed + " size " + size);
                }
            }
        }

        [Test]
        public void ASavedDeckWithFourStanceCards_GivesAnEmptyDeck()
        {
            // Each kind is within three, but the stance cards are four (§19.6 S15): Add refuses the fourth.
            Assert.That(DeckBuilder.Load("water_stance:2,rock_stance:2").Total, Is.EqualTo(0));
        }

        [Test]
        public void Removing_TakesOneCopy_AndNothingFromAnEmptyKind()
        {
            var deck = new DeckBuilder();
            deck.Add("thrust");
            deck.Add("thrust");
            Assert.Multiple(() =>
            {
                Assert.That(deck.Remove("thrust"), Is.True);
                Assert.That(deck.CountOf("thrust"), Is.EqualTo(1));
                Assert.That(deck.Remove("kesa_cut"), Is.False);
                Assert.That(deck.Remove("thrust"), Is.True);
                Assert.That(deck.CountText("thrust"), Is.EqualTo(""));
            });
        }

        [Test]
        public void TheBuiltDeck_IsInCanonOrder_WithUniqueInstanceIds()
        {
            var deck = new DeckBuilder();
            deck.Add("kesa_cut");
            deck.Add("thrust");
            deck.Add("kesa_cut");
            Assert.That(deck.Build().Select(c => c.InstanceId), Is.EqualTo(new[] { "thrust-0", "kesa_cut-0", "kesa_cut-1" }));
            Assert.That(deck.DeckLines(), Is.EqualTo(new[] { "突き ×1", "袈裟斬り ×2" }));
        }

        // ---- filters and pages ----

        [TestCase(BattleAttribute.None, 0, 80)]
        [TestCase(BattleAttribute.Attack, 0, 39)]
        [TestCase(BattleAttribute.Stance, 0, 14)]
        [TestCase(BattleAttribute.None, 1, 25)]
        [TestCase(BattleAttribute.None, 2, 30)]
        [TestCase(BattleAttribute.None, 3, 25)]
        [TestCase(BattleAttribute.Move, 1, 8)] // the six single moves (K5: all cost 1), 手繰りの歩み and 狩人の印
        public void AFilter_KeepsTheCardsWithTheAttributeAndCost(BattleAttribute attribute, int cost, int kept)
        {
            // swordsman_cards_v4 §4.1 / §4.4: 39 carry an attack face, 14 a stance, costs 25 / 30 / 25.
            var filter = new DeckFilter { Attribute = attribute, Cost = cost };
            List<CardDef> cards = new DeckBuilder().Filter(filter);
            Assert.That(cards, Has.Count.EqualTo(kept));
            Assert.That(cards.All(filter.Keeps), Is.True);
        }

        [Test]
        public void ThePages_CoverTheFilteredList_AndClampTheIndex()
        {
            var deck = new DeckBuilder();
            DeckPage first = deck.Page(DeckFilter.All, 0, 20);
            DeckPage last = deck.Page(DeckFilter.All, 99, 20);
            DeckPage before = deck.Page(DeckFilter.All, -3, 20);
            var all = Enumerable.Range(0, 4).SelectMany(p => deck.Page(DeckFilter.All, p, 20).Cards).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(first.Count, Is.EqualTo(4));
                Assert.That(first.Text, Is.EqualTo("1 / 4 ページ（80 種）"));
                Assert.That(last.Index, Is.EqualTo(3));
                Assert.That(before.Index, Is.EqualTo(0));
                Assert.That(all, Is.EqualTo(CardCatalog.All));
                Assert.That(deck.Page(new DeckFilter { Attribute = BattleAttribute.Stance }, 0, 20).Text, Is.EqualTo("1 / 1 ページ（14 種）"));
            });
        }

        [Test]
        public void TheCardText_IsTheBridgesWords()
        {
            List<string> lines = DeckBuilder.DetailOf(CardCatalog.KesaCut);
            Assert.Multiple(() =>
            {
                Assert.That(DeckBuilder.LineOf(CardCatalog.BoarRush), Is.EqualTo("猪突猛進　コスト 3　攻撃＋ムーブ"));
                Assert.That(lines, Is.EqualTo(new[]
                {
                    "袈裟斬り", "コスト 2　攻撃　届く間合い 0〜1", "敵に 13 ダメージ。", "特性: 間合い0 +5", "「肩口から斬り下ろす」",
                }));
                Assert.That(DeckBuilder.DetailOf(CardCatalog.Thrust)[3], Is.EqualTo("特性: なし（素直な札）"));
                Assert.That(DeckBuilder.DetailOf(CardCatalog.Brace)[1], Is.EqualTo("コスト 2　防御　自分向き"));
                Assert.That(CardCatalog.All.All(c => DeckBuilder.DetailOf(c).All(l => l.Length > 0)), Is.True);
            });
        }

        // ---- presets ----

        [Test]
        public void ThePrototypePreset_IsTheSlicesTenKindsTimesTwo()
        {
            var deck = DeckBuilder.Prototype();
            Assert.That(deck.Total, Is.EqualTo(20));
            Assert.That(deck.IsValid, Is.True);
            Assert.That(deck.Build().Select(c => c.Def.Id).Distinct(), Is.EquivalentTo(PrototypeDeck.Kinds.Select(k => k.Id)));
        }

        [Test]
        public void TheRandomPreset_AlwaysPasses_AndCanAlwaysClose()
        {
            // #196: every random deck holds a card that closes from gap 3 and three that step forward.
            for (int seed = 0; seed < 300; seed++)
            {
                var deck = DeckBuilder.Random(seed);
                string at = "seed " + seed;
                Assert.That(deck.Total, Is.EqualTo(DeckBuilder.RandomDefaultSize), at);
                Assert.That(deck.IsValid, Is.True, at + ": " + string.Join(" / ", deck.Validate().Errors));
                Assert.That(deck.CountIn(DeckBuilder.Closers), Is.GreaterThanOrEqualTo(1), at);
                Assert.That(deck.CountIn(DeckBuilder.Forward), Is.GreaterThanOrEqualTo(DeckBuilder.RandomForwardMin), at);
            }
            Assert.That(DeckBuilder.Random(7).Save(), Is.EqualTo(DeckBuilder.Random(7).Save()), "the seed fixes it");
            Assert.That(DeckBuilder.Random(1, 5).Total, Is.EqualTo(20), "clamped to §8");
            Assert.That(DeckBuilder.Random(1, 99).Total, Is.EqualTo(40));
            Assert.That(DeckBuilder.Closers.Select(c => c.Id), Is.EquivalentTo(new[] { "dash_in", "gale_thrust" }));
        }

        // ---- the saved string ----

        [Test]
        public void TheSavedString_RoundTrips()
        {
            var deck = DeckBuilder.Random(42, 33);
            string saved = deck.Save();
            var loaded = DeckBuilder.Load(saved);
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Save(), Is.EqualTo(saved));
                Assert.That(loaded.Build().Select(c => c.InstanceId), Is.EqualTo(deck.Build().Select(c => c.InstanceId)));
                Assert.That(DeckBuilder.Prototype().Save(), Does.StartWith("thrust:2,kesa_cut:2,"));
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("thrust:2,nothing:1")]
        [TestCase("thrust:4")]
        [TestCase("thrust:0")]
        [TestCase("thrust:-1")]
        [TestCase("thrust")]
        [TestCase("thrust:2:1")]
        [TestCase("thrust:2,thrust:1")]
        [TestCase("thrust:two")]
        [TestCase("<garbage>")]
        [TestCase("thrust:2,,kesa_cut:1")]
        public void ABrokenString_GivesAnEmptyDeck(string saved)
        {
            Assert.That(DeckBuilder.Load(saved).Total, Is.EqualTo(0));
        }

        [Test]
        public void TheScreenOpensOnThePrototype_OnlyWhenNothingWasSaved()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DeckBuilder.LoadOrPrototype(false, "").Save(), Is.EqualTo(DeckBuilder.Prototype().Save()), "the first Play");
                Assert.That(DeckBuilder.LoadOrPrototype(true, "").Total, Is.EqualTo(0), "the player emptied it");
                Assert.That(DeckBuilder.LoadOrPrototype(true, "thrust:9").Total, Is.EqualTo(0), "a broken string starts empty");
                Assert.That(DeckBuilder.LoadOrPrototype(true, "thrust:2").CountOf("thrust"), Is.EqualTo(2));
            });
        }

        [Test]
        public void TheTitleAndTheFilterChoices_AreTheBridges()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DeckBuilder.Title, Is.EqualTo("デッキを組む（80 種から 20〜40 枚、1 種 3 枚、構え 3 枚まで）"));
                Assert.That(DeckBuilder.AttributeOptions.Select(o => o.Key), Is.EqualTo(new[] { "全て", "攻撃", "ムーブ", "防御", "技", "構え" }));
                Assert.That(DeckBuilder.AttributeOptions.First().Value, Is.EqualTo(BattleAttribute.None));
                Assert.That(DeckBuilder.CostOptions.Select(o => o.Key), Is.EqualTo(new[] { "全コスト", "コスト 1", "コスト 2", "コスト 3" }));
                Assert.That(DeckBuilder.CostOptions.Select(o => o.Value), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            });
        }

        [Test]
        public void AStringPastFortyCards_GivesAnEmptyDeck()
        {
            string fortyTwo = string.Join(",", CardCatalog.All.Take(14).Select(c => c.Id + ":3"));
            Assert.That(DeckBuilder.Load(fortyTwo).Total, Is.EqualTo(0));
        }

        // ---- into the battle ----

        [Test]
        public void ABuiltDeck_IsTheDeckTheBattleIsFoughtWith()
        {
            var deck = DeckBuilder.Random(5);
            var launch = new BattleLaunch { Deck = deck.Build() };
            BattleSetup setup = launch.BuildSetup();
            CoreBattleSource source = launch.CreateSource();

            Assert.Multiple(() =>
            {
                Assert.That(setup.Deck.Select(c => c.InstanceId), Is.EquivalentTo(deck.Build().Select(c => c.InstanceId)));
                BattleState state = source.State;
                var all = state.Hand.Concat(state.DrawPile).Concat(state.DiscardPile).Select(c => c.InstanceId);
                Assert.That(all, Is.EquivalentTo(deck.Build().Select(c => c.InstanceId)));
            });

            source.AdvanceAuto();
            Assert.That(source.Frame.Hand, Has.Count.EqualTo(5));
            Assert.That(source.Frame.Hand.All(f => deck.Build().Any(c => c.InstanceId == f.Id)), Is.True, "the hand is dealt from the built deck");
        }

        [Test]
        public void ADeckThatBreaksTheRules_IsRefusedAtLaunch()
        {
            var launch = new BattleLaunch { Deck = Filled(19).Build() };
            var error = Assert.Throws<ArgumentException>(() => launch.BuildSetup());
            Assert.That(error.Message, Does.Contain("minimum is 20"));
        }

        [Test]
        public void ADeckWithFourStanceCards_IsRefusedAtLaunch()
        {
            // The deck screen cannot build one; a list handed in directly still meets Cards.Validate.
            var plain = CardCatalog.All.Where(c => !Cards.IsStanceCard(c)).Take(8).ToList();
            var deck = Cards.BuildDeck(plain, 2).Concat(Cards.BuildDeck(new[] { CardCatalog.WaterStance, CardCatalog.RockStance }, 2)).ToList();
            var error = Assert.Throws<ArgumentException>(() => new BattleLaunch { Deck = deck }.BuildSetup());
            Assert.That(error.Message, Does.Contain("4 stance cards"));
        }

        [Test]
        public void NoDeck_StillMeansThePrototypeDeck()
        {
            Assert.That(new BattleLaunch().BuildSetup().Deck.Select(c => c.InstanceId), Is.EqualTo(PrototypeDeck.Build().Select(c => c.InstanceId)));
        }
    }
}
