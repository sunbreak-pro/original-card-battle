using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattleCore.Tests
{
    /// <summary>
    /// Trial extension 「出立の枠（DeckLimits）」 (the browser trial's docs/EXTENSIONS.md; port of the core part of
    /// test/ext/limits.test.mjs): the survivor's bonus at 出立 (concept-v3.md §8.2 / §12-12 / §14 生存者ボーナス
    /// 「ツール枠 +1、デッキ上限 +5 を次の 1 生だけ」). One test or more per rule, named after it:
    ///   L-1  DeckLimits.Default is the constants, and every check without limits is what it was.
    ///   L-2  DeckLimits.From (the lineage half lives with InheritanceCore / Trial.Bridge).
    ///   L-3  Cards.Validate / DeckRules.ValidateDeck hold the deck to DeckMin..DeckMax.
    ///   L-4  DeckRules.CanAdd refuses the card past DeckMax (the 41st by default, the 46th with the bonus).
    ///   L-5  DeckRules.Validate holds the tools to ToolSlots and the consumables to ConsumableSlots.
    ///   L-9  Bad limits are refused.
    /// The builder rules (L-6 / L-7 / L-8, X-17) are Depiction.Bridge.Tests' DeckLimitsBuilderTests.
    /// </summary>
    public class DeckLimitsTests
    {
        private static readonly DeckLimits Survivor = new DeckLimits(deckMax: 45, toolSlots: 4);

        private static readonly IReadOnlyCollection<string> AllKinds = CardCatalog.All.Select(d => d.Id).ToList();

        /// <summary><paramref name="n"/> cards over the catalogue in canon order, three of a kind, instance ids "id-copy".</summary>
        private static List<CardInstance> DeckOf(int n)
        {
            var deck = new List<CardInstance>();
            foreach (CardDef def in CardCatalog.All)
            {
                for (int copy = 0; copy < 3 && deck.Count < n; copy++) deck.Add(new CardInstance(def.Id + "-" + copy, def));
                if (deck.Count >= n) break;
            }
            return deck;
        }

        private static int[] Numbers(DeckLimits l) => new[] { l.DeckMin, l.DeckMax, l.ToolSlots, l.ConsumableSlots };

        // ---- L-1 the constants by default ----

        [Test]
        public void L1_DefaultIsTheConstants_Shared()
        {
            DeckLimits d = DeckLimits.Default;
            Assert.Multiple(() =>
            {
                Assert.That(Numbers(d), Is.EqualTo(new[] { Constants.DeckMin, Constants.DeckMax, DeckRules.ToolSlots, DeckRules.ConsumableSlots }));
                Assert.That(Numbers(d), Is.EqualTo(new[] { 20, 40, 3, 3 }));
                Assert.That(DeckLimits.Default, Is.SameAs(d));
                Assert.That(d.IsDefault, Is.True);
                Assert.That(new DeckLimits().Equals(d), Is.True);
                Assert.That(new DeckLimits().IsDefault, Is.True);
                Assert.That(Survivor.IsDefault, Is.False);
                Assert.That(DeckRules.ToolSlots, Is.EqualTo(3), "the constants stay");
                Assert.That(DeckRules.ConsumableSlots, Is.EqualTo(3));
            });
        }

        [Test]
        public void L1_EveryCheckWithoutLimits_GivesWhatTheExplicitDefaultAndNullGive()
        {
            foreach (int n in new[] { 19, 20, 33, 40, 41, 45, 46 })
            {
                List<CardInstance> deck = DeckOf(n);
                DeckValidation plain = Cards.Validate(deck);
                Assert.That(Cards.Validate(deck, DeckLimits.Default).Ok, Is.EqualTo(plain.Ok), "Validate " + n);
                Assert.That(Cards.Validate(deck, DeckLimits.Default).Errors, Is.EqualTo(plain.Errors), "Validate " + n);
                Assert.That(Cards.Validate(deck, null).Errors, Is.EqualTo(plain.Errors));
                Assert.That(DeckRules.ValidateDeck(deck, AllKinds, DeckLimits.Default).Errors, Is.EqualTo(DeckRules.ValidateDeck(deck, AllKinds).Errors));
                Assert.That(DeckRules.CanAdd(deck, "thrust", AllKinds, DeckLimits.Default), Is.EqualTo(DeckRules.CanAdd(deck, "thrust", AllKinds)));
                Assert.That(DeckRules.CanAdd(deck, "thrust", AllKinds, null), Is.EqualTo(DeckRules.CanAdd(deck, "thrust", AllKinds)));
                foreach (string[] tools in new[] { new string[0], new[] { "a", "b", "c" }, new[] { "a", "b", "c", "d" } })
                {
                    var departure = new Departure(deck, tools, new[] { "x", "y", "z", "w" }.Take(tools.Length).ToArray(), 0, "thrust");
                    DeckValidation explicitDefault = DeckRules.Validate(departure, AllKinds, DeckLimits.Default);
                    DeckValidation left = DeckRules.Validate(departure, AllKinds);
                    Assert.That(explicitDefault.Ok, Is.EqualTo(left.Ok));
                    Assert.That(explicitDefault.Errors, Is.EqualTo(left.Errors));
                }
            }
            Assert.That(Cards.Validate(DeckOf(41)).Errors, Is.EqualTo(new[] { "Deck has 41 cards; the maximum is 40." }));
        }

        // ---- L-2 From ----

        [Test]
        public void L2_FromReadsNullAsDefault_AndANumberLeftOutIsTheDefaults()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DeckLimits.From(null), Is.SameAs(DeckLimits.Default));
                Assert.That(DeckLimits.From(Survivor), Is.SameAs(Survivor));
                Assert.That(Numbers(new DeckLimits(deckMax: 45)), Is.EqualTo(new[] { 20, 45, 3, 3 }), "a missing number is the default's");
                Assert.That(Numbers(Survivor), Is.EqualTo(new[] { 20, 45, 4, 3 }));
                Assert.That(Survivor.Equals(new DeckLimits(20, 45, 4, 3)), Is.True);
                Assert.That(Survivor.GetHashCode(), Is.EqualTo(new DeckLimits(20, 45, 4, 3).GetHashCode()));
                Assert.That(Survivor.Equals(DeckLimits.Default), Is.False);
                Assert.That(Survivor.Equals(null), Is.False);
            });
        }

        // ---- L-3 the deck's bounds ----

        [Test]
        public void L3_CardsValidate_HoldsTheDeckToTheLimitsMinAndMax()
        {
            foreach (int n in new[] { 41, 42, 43, 44, 45 })
            {
                Assert.That(Cards.Validate(DeckOf(n), Survivor).Ok, Is.True, n + " cards with the bonus");
                Assert.That(Cards.Validate(DeckOf(n)).Ok, Is.False, n + " cards without");
            }
            Assert.That(Cards.Validate(DeckOf(46), Survivor).Errors, Is.EqualTo(new[] { "Deck has 46 cards; the maximum is 45." }));
            Assert.That(Cards.Validate(DeckOf(19), Survivor).Errors, Is.EqualTo(new[] { "Deck has 19 cards; the minimum is 20." }), "the floor stays 20");
            Assert.That(DeckRules.ValidateDeck(DeckOf(45), AllKinds, Survivor).Ok, Is.True);
            Assert.That(DeckRules.ValidateDeck(DeckOf(45), AllKinds).Errors, Is.EqualTo(new[] { "Deck has 45 cards; the maximum is 40." }));
            // the other §8 rules do not move: three of a kind
            var four = DeckOf(40).Concat(new[] { new CardInstance("thrust-9", CardCatalog.ById("thrust")) }).ToList();
            Assert.That(Cards.Validate(four, Survivor).Errors, Is.EqualTo(new[] { "Deck holds 4 copies of \"thrust\"; the maximum is 3." }));
        }

        // ---- L-4 CanAdd ----

        [Test]
        public void L4_CanAdd_TheFortyFirstWithTheBonus_NeverTheFortySixth()
        {
            List<CardInstance> forty = DeckOf(40);
            Assert.Multiple(() =>
            {
                Assert.That(DeckRules.CanAdd(forty, "observe", AllKinds), Is.False);
                Assert.That(DeckRules.CanAdd(forty, "observe", AllKinds, Survivor), Is.True);
                Assert.That(DeckRules.CanAdd(DeckOf(44), "observe", AllKinds, Survivor), Is.True);
                Assert.That(DeckRules.CanAdd(DeckOf(45), "observe", AllKinds, Survivor), Is.False);
                Assert.That(DeckRules.CanAdd(forty, "thrust", AllKinds, Survivor), Is.False, "still three of a kind");
                Assert.That(DeckRules.CanAdd(forty, "observe", new[] { "thrust" }, Survivor), Is.False, "still owned kinds only");
            });
        }

        // ---- L-5 the slots ----

        [Test]
        public void L5_Validate_AFourthToolFitsOnlyWithTheBonus_TheConsumablesStayThree()
        {
            List<CardInstance> deck = DeckOf(45);
            var four = new Departure(deck, new[] { "a", "b", "c", "d" }, new[] { "x", "y", "z" }, 0, "thrust");
            Assert.That(DeckRules.Validate(four, AllKinds, Survivor).Errors, Is.Empty);
            Assert.That(DeckRules.Validate(four, AllKinds).Errors,
                Is.EqualTo(new[] { "Deck has 45 cards; the maximum is 40.", "4 tools do not fit 3 slots." }));
            var five = new Departure(deck, new[] { "a", "b", "c", "d", "e" }, new[] { "x", "y", "z", "w" }, 0, "thrust");
            Assert.That(DeckRules.Validate(five, AllKinds, Survivor).Errors,
                Is.EqualTo(new[] { "5 tools do not fit 4 slots.", "4 consumables do not fit 3 slots." }));
        }

        // ---- L-9 bad limits ----

        [Test]
        public void L9_BadLimitsAreRefused()
        {
            // The JS also refuses a number that is not whole (2.5) or not a number ('45'); a C# int is always whole.
            ArgumentOutOfRangeException min = Assert.Throws<ArgumentOutOfRangeException>(() => new DeckLimits(deckMin: 0))!;
            Assert.That(min.ParamName, Is.EqualTo("DeckMin"));
            Assert.That(min.Message, Does.StartWith("A whole number, 1 or more."));
            ArgumentOutOfRangeException max = Assert.Throws<ArgumentOutOfRangeException>(() => new DeckLimits(deckMax: 19))!;
            Assert.That(max.ParamName, Is.EqualTo("DeckMax"));
            Assert.That(max.Message, Does.StartWith("Not under DeckMin (20)."));
            ArgumentOutOfRangeException tools = Assert.Throws<ArgumentOutOfRangeException>(() => new DeckLimits(toolSlots: -1))!;
            Assert.That(tools.ParamName, Is.EqualTo("ToolSlots"));
            Assert.That(tools.Message, Does.StartWith("A whole number, 0 or more."));
            ArgumentOutOfRangeException consumables = Assert.Throws<ArgumentOutOfRangeException>(() => new DeckLimits(consumableSlots: -1))!;
            Assert.That(consumables.ParamName, Is.EqualTo("ConsumableSlots"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Survivor.With(deckMin: 46));
            Assert.That(Numbers(Survivor.With(toolSlots: 5)), Is.EqualTo(new[] { 20, 45, 5, 3 }));
            Assert.That(Numbers(Survivor), Is.EqualTo(new[] { 20, 45, 4, 3 }), "With leaves the original as it was");
            Assert.That(Numbers(new DeckLimits(toolSlots: 0, consumableSlots: 0)), Is.EqualTo(new[] { 20, 40, 0, 0 }), "slots may be 0");
            Assert.That(Numbers(new DeckLimits(deckMin: 1, deckMax: 1)), Is.EqualTo(new[] { 1, 1, 3, 3 }));
        }
    }
}
