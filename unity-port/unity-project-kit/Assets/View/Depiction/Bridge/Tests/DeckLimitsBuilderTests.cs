// Trial extension 「出立の枠（DeckLimits）」 on the deck screen and the supplies step (port of the builder part of the
// browser trial's test/ext/limits.test.mjs, and the X-17 test of test/ext/last-round.test.mjs):
//   L-1  A builder made without limits prints what it printed before.
//   L-6  DeckBuilder: Add / CanAdd / Validate / StatusText / LifeTitle / Random / Prototype / Load read the limits;
//        ReadOverCap / OverCapNotice / WithOwned open a survivor's 45-card save after the bonus ended (試作 7).
//   L-7  DepartureBuilder: the slots, the lines and the headings read the limits; the 4th slot is the bonus's.
//   L-8  A new heir's first 出立 opens on the full prototype deck (owned = the forty ∪ the prototype's kinds).
//   X-17 The deck builder prints the battle's attribute words; its own words stay as they are.
//   BattleLaunch.Limits holds the battle's deck to the life's limits (null: 20〜40, as before).
// The lineage (L-2's startLife) and the hand-off through DepartureSupplies / Expedition / EncounterBattle are
// tested where those live (InheritanceCore, Trial.Bridge, Exploration.Bridge): this assembly sees none of them.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class DeckLimitsBuilderTests
    {
        private static readonly DeckLimits Survivor = new DeckLimits(deckMax: 45, toolSlots: 4);

        /// <summary>The first forty kinds of the catalog: what Exploration.Bridge's Expedition.InitialKinds holds.</summary>
        private static readonly IReadOnlyList<string> InitialKinds = CardCatalog.All.Take(40).Select(d => d.Id).ToList();

        /// <summary>A builder holding <paramref name="n"/> cards (three of a kind, canon order).</summary>
        private static DeckBuilder Filled(int n, DeckLimits limits = null)
        {
            var b = new DeckBuilder(null, limits);
            foreach (CardDef def in CardCatalog.All)
            {
                for (int i = 0; i < 3 && b.Total < n; i++) Assert.That(b.Add(def.Id), Is.True, "card " + (b.Total + 1));
                if (b.Total >= n) break;
            }
            return b;
        }

        private static DepartureBuilder Supplies(DeckLimits limits = null)
        {
            var tools = new[]
            {
                new SupplyOption("bosho_no_men", "防瘴の面", "層の濃度を 1 下げる"),
                new SupplyOption("maai_no_kutsu", "間合いの履", "開始のマスが 2 から 1 になる", 1),
                new SupplyOption("ikitsugi_no_fue", "息継ぎの笛", "戦闘開始時のスタミナ +3", 0, 3),
            };
            var consumables = new[] { new SupplyOption("yakuso", "薬草", "HP が最大の 20% 戻る") };
            return limits == null ? new DepartureBuilder(tools, consumables) : new DepartureBuilder(tools, consumables, limits);
        }

        /// <summary>The deck a saved string reads to without the over-cap reading: the JS's private Read.</summary>
        private static DeckBuilder Read(string saved, DeckLimits limits, out string notice) =>
            DeckBuilder.LoadOrPrototype(true, saved, out notice, limits);

        // ---- L-1 what it was by default ----

        [Test]
        public void L1_ABuilderMadeWithoutLimits_PrintsWhatItPrintedBefore()
        {
            var b = new DeckBuilder();
            Assert.That(b.Limits, Is.SameAs(DeckLimits.Default));
            Assert.That(b.LifeTitle, Is.EqualTo(DeckBuilder.Title));
            Assert.That(DeckBuilder.Title, Is.EqualTo("デッキを組む（80 種から 20〜40 枚、1 種 3 枚まで）"));
            Assert.That(DeckBuilder.TitleFor(null), Is.EqualTo(DeckBuilder.Title));
            Assert.That(Filled(40).StatusText, Is.EqualTo("40 枚です。20〜40 枚、1 種 3 枚を満たしています"));
            Assert.That(Filled(40).CanAdd("kesa_cut"), Is.False, "the 41st");
            Assert.That(b.RaisedLine, Is.EqualTo(""));
            DepartureBuilder d = Supplies();
            Assert.That(d.Limits, Is.SameAs(DeckLimits.Default));
            Assert.That(d.LifeTitle, Is.EqualTo(DepartureBuilder.Title));
            Assert.That(d.LifeToolHeading, Is.EqualTo(DepartureBuilder.ToolHeading));
            Assert.That(d.LifeConsumableHeading, Is.EqualTo(DepartureBuilder.ConsumableHeading));
            Assert.That(DepartureBuilder.Title, Is.EqualTo("出立の支度（ツール 3 枠・消耗品 3 枠・才能 1 枚）"));
            Assert.That(DepartureBuilder.ToolHeading, Is.EqualTo("ツール（探索と戦闘で共通の 3 枠。同じものを 2 つ持てます）"));
            Assert.That(DepartureBuilder.ConsumableHeading, Is.EqualTo("消耗品（3 枠。拠点へは持ち帰りません）"));
            Assert.That(d.RaisedLine, Is.EqualTo(""));
            Assert.That(d.IsBonusToolSlot(3), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(d.AddTool("bosho_no_men"), Is.True);
            Assert.That(d.AddTool("bosho_no_men"), Is.False, "the 4th tool");
            Assert.That(d.ToolsLine, Is.EqualTo("ツール 3 / 3: 防瘴の面、防瘴の面、防瘴の面"));
            Assert.That(DeckBuilder.Random(7).Save(), Is.EqualTo(DeckBuilder.Random(7, DeckBuilder.RandomDefaultSize, DeckLimits.Default).Save()));
        }

        // ---- L-6 DeckBuilder ----

        [Test]
        public void L6_DeckBuilderWithTheBonus_UpToFortyFive_TheLinesPrintFortyFive()
        {
            DeckBuilder b = Filled(45, Survivor);
            Assert.That(b.Total, Is.EqualTo(45));
            Assert.That(b.CanAdd("observe"), Is.False, "the 46th");
            Assert.That(b.Add("observe"), Is.False);
            Assert.That(b.IsValid, Is.True);
            Assert.That(b.StatusText, Is.EqualTo("45 枚です。20〜45 枚、1 種 3 枚を満たしています"));
            Assert.That(Filled(19, Survivor).StatusText, Is.EqualTo("19 枚です。あと 1 枚入れると戦えます"));
            Assert.That(b.LifeTitle, Is.EqualTo("デッキを組む（80 種から 20〜45 枚、1 種 3 枚まで）"));
            Assert.That(DeckBuilder.TitleFor(Survivor), Is.EqualTo(b.LifeTitle));
            Assert.That(DeckBuilder.Title, Is.EqualTo("デッキを組む（80 種から 20〜40 枚、1 種 3 枚まで）"), "the static Title stays");
            Assert.That(b.RaisedLine, Is.EqualTo("デッキ上限 +5（40 → 45）"));
            Assert.That(Filled(41, Survivor).Validate().Ok, Is.True);
        }

        [Test]
        public void L6_RandomDrawsUpToTheLimitsCap_FixedByTheSeed()
        {
            foreach (int seed in new[] { 1, 7, 17, 42 })
            {
                DeckBuilder r = DeckBuilder.Random(seed, 45, Survivor);
                Assert.That(r.Total, Is.EqualTo(45), "seed " + seed);
                Assert.That(r.IsValid, Is.True);
                Assert.That(r.Limits, Is.SameAs(Survivor));
                Assert.That(r.Save(), Is.EqualTo(DeckBuilder.Random(seed, 45, Survivor).Save()), "deterministic");
                Assert.That(DeckBuilder.Random(seed, 99, Survivor).Total, Is.EqualTo(45), "clamped to 45");
                Assert.That(DeckBuilder.Random(seed, 45).Total, Is.EqualTo(40), "without limits it clamps to 40");
                // 30 is inside both: the same deck either way
                Assert.That(DeckBuilder.Random(seed, 30, Survivor).Save(), Is.EqualTo(DeckBuilder.Random(seed, 30).Save()));
            }
        }

        [Test]
        public void L6_PrototypeAndTheSavedStringCarryTheLimits()
        {
            DeckBuilder p = DeckBuilder.Prototype(Survivor);
            Assert.That(p.Limits, Is.SameAs(Survivor));
            Assert.That(p.Total, Is.EqualTo(PrototypeDeck.Kinds.Count * PrototypeDeck.Copies));
            string saved = Filled(45, Survivor).Save();
            DeckBuilder read = DeckBuilder.Load(saved, Survivor);
            Assert.That(read.Total, Is.EqualTo(45));
            Assert.That(read.Limits, Is.SameAs(Survivor));
            Assert.That(DeckBuilder.Load(saved).Total, Is.EqualTo(0), "the constants' 40 refuses a 45-card string");
            DeckBuilder withNotice = DeckBuilder.LoadOrPrototype(true, saved, out string notice, Survivor);
            Assert.That(withNotice.Total, Is.EqualTo(45));
            Assert.That(notice, Is.EqualTo(""));
            DeckBuilder refused = DeckBuilder.LoadOrPrototype(true, saved, out string refusal);
            Assert.That(refused.Total, Is.EqualTo(0));
            Assert.That(refusal, Is.EqualTo("保存したデッキがデッキの決まりに合わないため、空のデッキから始めます"));
            DeckBuilder plain = DeckBuilder.LoadOrPrototype(false, null, Survivor);
            Assert.That(plain.Limits, Is.SameAs(Survivor));
            Assert.That(plain.Total, Is.EqualTo(20));
            Assert.That(DeckBuilder.LoadOrPrototype(false, null).Limits, Is.SameAs(DeckLimits.Default));
            Assert.That(Read("thrust:2,???", Survivor, out _).Limits, Is.SameAs(Survivor), "an empty deck from a broken string keeps the limits");
        }

        [Test]
        public void L6_ASurvivorsFortyFiveCardSave_OpensWholeAfterTheBonusEnded_AndAsksForFiveCardsOut()
        {
            string saved = Filled(45, Survivor).Save();
            DeckBuilder r = DeckBuilder.ReadOverCap(saved, out string notice);
            Assert.That(r.Total, Is.EqualTo(45), "every card kept");
            Assert.That(r.Limits, Is.SameAs(DeckLimits.Default), "held to the life's limits");
            Assert.That(r.Save(), Is.EqualTo(saved));
            Assert.That(notice, Is.EqualTo("生存者ボーナスが終わり、デッキ上限が 40 枚に戻りました。保存したデッキ 45 枚から、外す 5 枚を選んでください"));
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.StatusText, Is.EqualTo("45 枚です。あと 5 枚外すと戦えます"));
            Assert.That(r.CanAdd("thrust"), Is.False, "＋ stays greyed out over the cap");
            // the cards come out one by one; at the cap the deck is what it was without limits
            List<string> ids = r.Build().Select(c => c.Def.Id).Reverse().ToList();
            for (int i = 0; i < 4; i++) Assert.That(r.Remove(ids[i]), Is.True);
            Assert.That(r.StatusText, Is.EqualTo("41 枚です。あと 1 枚外すと戦えます"));
            Assert.That(r.IsValid, Is.False);
            r.Remove(ids[4]);
            Assert.That(r.IsValid, Is.True);
            Assert.That(r.StatusText, Is.EqualTo("40 枚です。20〜40 枚、1 種 3 枚を満たしています"));
            Assert.That(r.CanAdd("thrust"), Is.False, "thrust already holds 3");
        }

        [Test]
        public void L6_ReadOverCap_ReadsEveryOtherStringAsTheSavedStringReads()
        {
            var cases = new List<KeyValuePair<string, DeckLimits>>
            {
                new KeyValuePair<string, DeckLimits>("", DeckLimits.Default),
                new KeyValuePair<string, DeckLimits>(null, Survivor),
                new KeyValuePair<string, DeckLimits>("thrust:2,kesa_cut:3", DeckLimits.Default),
                new KeyValuePair<string, DeckLimits>(Filled(45, Survivor).Save(), Survivor),
                new KeyValuePair<string, DeckLimits>(Filled(40).Save(), DeckLimits.Default),
                new KeyValuePair<string, DeckLimits>("thrust:4", DeckLimits.Default),
                new KeyValuePair<string, DeckLimits>("thrust:2,???", Survivor),
                new KeyValuePair<string, DeckLimits>("no_such_card:1", DeckLimits.Default),
                new KeyValuePair<string, DeckLimits>("thrust:2,thrust:1", DeckLimits.Default),
            };
            foreach (KeyValuePair<string, DeckLimits> each in cases)
            {
                DeckBuilder a = DeckBuilder.ReadOverCap(each.Key, out string overNotice, each.Value);
                DeckBuilder b = Read(each.Key, each.Value, out string readNotice);
                Assert.That(overNotice, Is.EqualTo(readNotice), each.Key ?? "null");
                Assert.That(a.Save(), Is.EqualTo(b.Save()), each.Key ?? "null");
                Assert.That(a.Limits, Is.SameAs(b.Limits));
            }
            // past the cap and broken in another way too (a 4th copy): the empty deck and its notice
            string lastKind = CardCatalog.All[CardCatalog.All.Count - 1].Id;
            string pastAndBroken = Filled(42, Survivor).Save() + "," + lastKind + ":4";
            DeckBuilder broken = DeckBuilder.ReadOverCap(pastAndBroken, out string brokenNotice);
            Assert.That(broken.Total, Is.EqualTo(0));
            Read(pastAndBroken, null, out string readBroken);
            Assert.That(brokenNotice, Is.EqualTo(readBroken));
            Assert.That(DeckBuilder.ReadOverCap(Filled(42, Survivor).Save(), out _).Total, Is.EqualTo(42), "the same deck without the 4th copy opens whole");
        }

        [Test]
        public void L6_OverCapNotice_NamesTheBonusOnlyWhenTheCapIsBackAtForty()
        {
            Assert.That(DeckBuilder.OverCapNotice(43),
                Is.EqualTo("生存者ボーナスが終わり、デッキ上限が 40 枚に戻りました。保存したデッキ 43 枚から、外す 3 枚を選んでください"));
            Assert.That(DeckBuilder.OverCapNotice(46, Survivor),
                Is.EqualTo("この生のデッキ上限は 45 枚です。保存したデッキ 46 枚から、外す 1 枚を選んでください"));
        }

        [Test]
        public void L6_ReadOverCap_UnderACapAboveForty_NamesTheLifesCap()
        {
            // (Not in the JS tests.) DeckBuilder.js:502 / :518: the over-cap reading under a cap still above 40 takes
            // OverCapNotice's other branch, through ReadOverCap rather than called directly.
            var raised = new DeckLimits(deckMax: 42);
            string saved = Filled(45, Survivor).Save();
            DeckBuilder r = DeckBuilder.ReadOverCap(saved, out string notice, raised);
            Assert.That(r.Total, Is.EqualTo(45));
            Assert.That(r.Limits, Is.SameAs(raised));
            Assert.That(notice, Is.EqualTo("この生のデッキ上限は 42 枚です。保存したデッキ 45 枚から、外す 3 枚を選んでください"));
            Assert.That(r.StatusText, Is.EqualTo("45 枚です。あと 3 枚外すと戦えます"));
        }

        [Test]
        public void L6_WithOwned_KeepsTheOwnedKindsAndADeckOverTheCap_AKindNotOwnedIsLeftOut()
        {
            DeckBuilder over = DeckBuilder.ReadOverCap(Filled(45, Survivor).Save(), out _);
            List<string> owned = DeckBuilder.WithPrototypeKinds(InitialKinds);
            DeckBuilder kept = over.WithOwned(owned);
            Assert.That(kept.Limits, Is.SameAs(over.Limits));
            Assert.That(kept.Owned, Is.EqualTo(owned));
            var ownedSet = new HashSet<string>(owned);
            int expect = over.Build().Count(c => ownedSet.Contains(c.Def.Id));
            Assert.That(kept.Total, Is.EqualTo(expect));
            Assert.That(over.WithOwned(null).Total, Is.EqualTo(45));
            DeckBuilder proto = DeckBuilder.Prototype().WithOwned(owned);
            Assert.That(proto.Total, Is.EqualTo(20));
            Assert.That(proto.IsValid, Is.True);
        }

        // ---- L-7 DepartureBuilder ----

        [Test]
        public void L7_DepartureBuilderWithTheBonus_FourToolSlots_TheFourthIsTheBonuss()
        {
            DepartureBuilder d = Supplies(Survivor);
            Assert.That(d.LifeTitle, Is.EqualTo("出立の支度（ツール 4 枠・消耗品 3 枠・才能 1 枚）"));
            Assert.That(d.LifeToolHeading, Is.EqualTo("ツール（探索と戦闘で共通の 4 枠。同じものを 2 つ持てます）"));
            Assert.That(d.LifeConsumableHeading, Is.EqualTo("消耗品（3 枠。拠点へは持ち帰りません）"));
            Assert.That(DepartureBuilder.TitleFor(Survivor), Is.EqualTo(d.LifeTitle));
            Assert.That(DepartureBuilder.ToolHeadingFor(Survivor), Is.EqualTo(d.LifeToolHeading));
            Assert.That(DepartureBuilder.ConsumableHeadingFor(Survivor), Is.EqualTo(d.LifeConsumableHeading));
            Assert.That(DepartureBuilder.Title, Is.EqualTo("出立の支度（ツール 3 枠・消耗品 3 枠・才能 1 枚）"), "the static Title stays");
            Assert.That(d.RaisedLine, Is.EqualTo("ツール枠 +1（3 → 4）"));
            Assert.That(DepartureBuilder.BonusLabel, Is.EqualTo("生存者ボーナス"));
            Assert.That(new[] { 0, 1, 2, 3, 4 }.Select(i => d.IsBonusToolSlot(i)).ToArray(), Is.EqualTo(new[] { false, false, false, true, false }));
            Assert.That(d.AddTool("bosho_no_men"), Is.True);
            Assert.That(d.AddTool("bosho_no_men"), Is.True);
            Assert.That(d.AddTool("maai_no_kutsu"), Is.True);
            Assert.That(d.CanAddTool("ikitsugi_no_fue"), Is.True, "the 4th slot");
            Assert.That(d.AddTool("ikitsugi_no_fue"), Is.True);
            Assert.That(d.AddTool("bosho_no_men"), Is.False, "no 5th");
            Assert.That(d.ToolsLine, Is.EqualTo("ツール 4 / 4: 防瘴の面、防瘴の面、間合いの履、息継ぎの笛"));
            for (int i = 0; i < 3; i++) Assert.That(d.AddConsumable("yakuso"), Is.True);
            Assert.That(d.AddConsumable("yakuso"), Is.False, "the consumables stay 3");
            Assert.That(d.ConsumablesLine, Is.EqualTo("消耗品 3 / 3: 薬草、薬草、薬草"));
            // set out with a 45-card deck and four tools
            DeckBuilder deck = Filled(45, Survivor);
            Assert.That(d.ChooseTalent(deck, "thrust"), Is.True);
            Assert.That(d.Validate(deck).Errors, Is.Empty);
            Assert.That(d.CanSetOut(deck), Is.True);
            Assert.That(d.StatusText(deck), Is.EqualTo("出立できます"));
            Departure departure = d.Build(deck);
            Assert.That(departure.Deck.Count, Is.EqualTo(45));
            Assert.That(departure.Tools.Count, Is.EqualTo(4));
            Assert.That(departure.StartStaminaBonus, Is.EqualTo(3));
            Assert.That(departure.PlayerStartCell, Is.EqualTo(1));
        }

        [Test]
        public void L7_TheDecksBoundsComeFromTheDeckBuilder_TheSlotsFromTheDepartureBuilder()
        {
            // A survivor's deck under a departure builder made without limits: the deck passes (as its IsValid
            // says), the 4th tool is still refused.
            DeckBuilder deck = Filled(45, Survivor);
            DepartureBuilder d = Supplies();
            d.ChooseTalent(deck, "thrust");
            Assert.That(deck.IsValid, Is.True);
            Assert.That(d.CanSetOut(deck), Is.True);
            Assert.That(d.StatusText(deck), Is.EqualTo("出立できます"));
            Assert.That(d.CanAddTool("bosho_no_men"), Is.True);
            for (int i = 0; i < 3; i++) d.AddTool("bosho_no_men");
            Assert.That(d.AddTool("bosho_no_men"), Is.False);
            // and the other way: a deck builder without limits under a survivor's departure is held to 40
            DeckBuilder plainDeck = Filled(40);
            DepartureBuilder s = Supplies(Survivor);
            s.ChooseTalent(plainDeck, "thrust");
            Assert.That(s.CanSetOut(plainDeck), Is.True);
            Assert.That(plainDeck.CanAdd("observe"), Is.False);
        }

        [Test]
        public void L7_TheDecksFloorComesFromTheDeckBuilderToo()
        {
            // (Not in the JS tests.) DepartureBuilder.js:206 LimitsWith takes both of the deck's bounds: a deck held to
            // 1〜5 sets out with 3 cards under a departure builder made without limits, as its own IsValid says.
            var tight = new DeckLimits(deckMin: 1, deckMax: 5);
            var deck = new DeckBuilder(null, tight);
            for (int i = 0; i < 3; i++) Assert.That(deck.Add("thrust"), Is.True);
            Assert.That(deck.IsValid, Is.True);
            DepartureBuilder d = Supplies();
            Assert.That(d.ChooseTalent(deck, "thrust"), Is.True);
            Assert.That(d.Validate(deck).Errors, Is.Empty);
            Assert.That(d.StatusText(deck), Is.EqualTo("出立できます"));
            // The same 3 cards on a builder without limits: the 20 floor, in the deck and in the departure.
            var plain = new DeckBuilder();
            for (int i = 0; i < 3; i++) plain.Add("thrust");
            Assert.That(d.Validate(plain).Errors, Is.EqualTo(new[] { "Deck has 3 cards; the minimum is 20." }));
        }

        // ---- BattleLaunch: the life's limits reach the battle's deck check ----

        [Test]
        public void BattleLaunch_HoldsTheDeckToItsLimits_NullIsTwentyToForty()
        {
            // BattleLaunch.js:87 / :132 (docs/EXTENSIONS.md 「出立の枠」 For the router: without it a survivor's 41〜45
            // cards throw at the first fight). The hand-off that sets it (EncounterBattle.NextLaunch) is tested with
            // Exploration.Bridge.
            List<CardInstance> fortyFive = Filled(45, Survivor).Build();
            var launch = new BattleLaunch { Deck = fortyFive };
            Assert.That(launch.Limits, Is.Null, "null by default");
            ArgumentException refused = Assert.Throws<ArgumentException>(() => launch.BuildDeck());
            Assert.That(refused.Message, Is.EqualTo("BattleLaunch: the deck breaks §8 — Deck has 45 cards; the maximum is 40."));

            launch.Limits = Survivor;
            List<CardInstance> built = launch.BuildDeck();
            Assert.That(built.Select(c => c.InstanceId).ToList(), Is.EqualTo(fortyFive.Select(c => c.InstanceId).ToList()));
            Assert.That(built, Is.Not.SameAs(fortyFive), "a copy, as without limits");
            Assert.That(launch.BuildSetup().Deck, Has.Count.EqualTo(45));

            // The 46th is still refused, with the life's cap in the line.
            List<CardInstance> fortySix = Filled(45, Survivor).Build();
            CardDef next = CardCatalog.All[15];
            Assert.That(fortySix.Any(c => c.Def.Id == next.Id), Is.False);
            fortySix.Add(new CardInstance(next.Id + "-0", next));
            var over = new BattleLaunch { Deck = fortySix, Limits = Survivor };
            ArgumentException past = Assert.Throws<ArgumentException>(() => over.BuildDeck());
            Assert.That(past.Message, Is.EqualTo("BattleLaunch: the deck breaks §8 — Deck has 46 cards; the maximum is 45."));

            // No deck: the prototype deck, limits or not.
            Assert.That(new BattleLaunch { Limits = Survivor }.BuildDeck().Select(c => c.InstanceId).ToList(),
                Is.EqualTo(new BattleLaunch().BuildDeck().Select(c => c.InstanceId).ToList()));
        }

        // ---- L-8 the first 出立 ----

        [Test]
        public void L8_ThePrototypeDeckOpensWhole_WhenThePrototypesKindsJoinTheOwnedForty()
        {
            Func<DeckBuilder, IReadOnlyCollection<string>, DeckLimits, DeckBuilder> adopt = (src, kinds, limits) =>
            {
                var b = new DeckBuilder(kinds, limits);
                foreach (CardInstance card in src.Build()) b.Add(card.Def.Id);
                return b;
            };
            IReadOnlyList<string> forty = InitialKinds;
            List<string> outside = PrototypeDeck.Kinds.Where(d => !forty.Contains(d.Id)).Select(d => d.Id).ToList();
            Assert.That(outside, Is.EqualTo(new[] { "shield_bash" }), "盾打ち (#51) is the prototype's one kind outside the forty");
            Assert.That(adopt(DeckBuilder.Prototype(), forty, null).Total, Is.EqualTo(18), "the bug: two 盾打ち refused");
            List<string> owned = DeckBuilder.WithPrototypeKinds(forty);
            Assert.That(owned.Count, Is.EqualTo(41));
            Assert.That(owned.Take(40).ToList(), Is.EqualTo(forty), "the forty first, in their order");
            Assert.That(owned[40], Is.EqualTo("shield_bash"));
            Assert.That(DeckBuilder.WithPrototypeKinds(owned), Is.EqualTo(owned), "idempotent");
            Assert.That(DeckBuilder.WithPrototypeKinds(null), Is.EqualTo(PrototypeDeck.Kinds.Select(d => d.Id).ToList()));
            foreach (DeckLimits limits in new[] { null, Survivor })
            {
                DeckBuilder deck = adopt(DeckBuilder.Prototype(limits), owned, limits);
                Assert.That(deck.Total, Is.EqualTo(20));
                Assert.That(deck.IsValid, Is.True);
                Assert.That(deck.Save(), Is.EqualTo(DeckBuilder.Prototype().Save()));
            }
        }

        // ---- X-17 the attribute words the screens print ----

        [Test]
        public void X17_TheDeckBuilderPrintsTheBattlesAttributeWords_ItsOwnWordsStay()
        {
            Assert.That(DeckBuilder.ShownAttributeOptions.Select(o => o.Key).ToArray(), Is.EqualTo(new[] { "全て", "攻撃", "防御", "スキル", "スタンス" }));
            Assert.That(DeckBuilder.ShownAttributeOptions.Select(o => o.Value).ToArray(), Is.EqualTo(DeckBuilder.AttributeOptions.Select(o => o.Value).ToArray()));
            Assert.That(DeckBuilder.AttributeOptions.Select(o => o.Key).ToArray(), Is.EqualTo(new[] { "全て", "攻撃", "防御", "技", "構え" }));
            foreach (BattleAttribute flag in new[] { BattleAttribute.Attack, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance })
            {
                Assert.That(DeckBuilder.ShownAttributeWords(flag), Is.EqualTo(CardFaceText.AttributeWord(flag)), "the battle's word");
            }
            foreach (CardDef def in CardCatalog.All)
            {
                List<string> shown = DeckBuilder.ShownDetailOf(def);
                List<string> plain = DeckBuilder.DetailOf(def);
                string shownWords = DeckBuilder.ShownAttributeWords(def.Attribute);
                Assert.That(shown.Count, Is.EqualTo(plain.Count));
                Assert.That(shown[1], Does.StartWith("コスト " + def.Cost + "　" + shownWords), def.Id);
                Assert.That(shown.Skip(2).ToList(), Is.EqualTo(plain.Skip(2).ToList()));
                // The JS's String.replace: the first match only.
                int at = shown[1].IndexOf(shownWords, StringComparison.Ordinal);
                string back = shown[1].Substring(0, at) + DeckBuilder.AttributeWords(def.Attribute) + shown[1].Substring(at + shownWords.Length);
                Assert.That(back, Is.EqualTo(plain[1]));
            }
        }
    }
}
