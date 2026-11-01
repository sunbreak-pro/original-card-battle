// What 出立 offers and hands on (src/scenes/meta/DepartureSupplies.js: Tools / Consumables / Of / NewBuilder(limits) /
// Carry): the DepartureSupplies parts of test/ext/limits.test.mjs (L-1, L-7 and the three hand-off tests), the hand-off
// carried on through the life's saved start (Life.ChoiceToData / LifeSetup).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using DungeonContent;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    public sealed class DepartureCarryTests
    {
        private static readonly DeckLimits Survivor = new DeckLimits(deckMax: 45, toolSlots: 4);

        [Test]
        public void TheSupplies_AreTheCataloguesInItsOrder_EachAsTheBattleReadsIt()
        {
            // js: scenes/meta/DepartureSupplies.js:25 / :28 / :60
            Assert.That(DepartureCarry.Tools.Select(o => o.Id), Is.EqualTo(ItemCatalogue.Tools.Select(d => d.Id)));
            Assert.That(DepartureCarry.Consumables.Select(o => o.Id), Is.EqualTo(ItemCatalogue.Consumables.Select(d => d.Id)));
            foreach (ItemDef item in ItemCatalogue.Tools.Concat(ItemCatalogue.Consumables))
            {
                SupplyOption o = DepartureCarry.Of(item);
                Assert.That((o.Id, o.Name, o.Note), Is.EqualTo((item.Id, item.Name, item.Note)));
                Assert.That(o.StartCellsBack, Is.EqualTo(item.Effect == ItemEffect.WiderStart ? item.Amount : 0), item.Id);
                Assert.That(o.StartStaminaBonus, Is.EqualTo(item.Effect == ItemEffect.OpeningStamina ? item.Amount : 0), item.Id);
                Assert.That(o.FirstHitGuard, Is.EqualTo(item.Effect == ItemEffect.OpeningGuard ? item.Amount : 0), item.Id);
                Assert.That(o.BossWordWard, Is.EqualTo(item.Effect == ItemEffect.BluntBossState ? item.Amount : 0), item.Id);
            }
            Assert.That(DepartureCarry.Tools.Single(o => o.Id == "maai_no_kutsu").StartCellsBack, Is.EqualTo(1));
            Assert.That(DepartureCarry.Tools.Single(o => o.Id == "ikitsugi_no_fue").StartStaminaBonus, Is.EqualTo(3));
        }

        // js: test/ext/limits.test.mjs:133 / :352
        [Test]
        public void NewBuilder_HoldsTheLimitsItIsGiven_TheCSharpsWithout()
        {
            Assert.That(DepartureCarry.NewBuilder().Limits, Is.EqualTo(DeckLimits.Default));
            Assert.That(DepartureCarry.NewBuilder(Survivor).Limits, Is.EqualTo(Survivor));
            DepartureBuilder sup = DepartureCarry.NewBuilder(Survivor);
            foreach (string id in new[] { "bosho_no_men", "bosho_no_men", "maai_no_kutsu", "ikitsugi_no_fue" }) Assert.That(sup.AddTool(id), Is.True, id);
            Assert.That(sup.AddTool("bosho_no_men"), Is.False, "four slots with the bonus");
            DepartureBuilder plain = DepartureCarry.NewBuilder();
            for (int i = 0; i < 3; i++) Assert.That(plain.AddTool("bosho_no_men"), Is.True);
            Assert.That(plain.AddTool("bosho_no_men"), Is.False, "the C#'s 3 slots");
        }

        /// <summary>A lineage whose last life closed alive (js: limits.test.mjs:66 survivorLineage).</summary>
        private static Lineage SurvivorLineage()
        {
            Lineage l = Inheritance.NewLineage(20261020);
            LifeStart s1 = Inheritance.StartLife(l);
            l = Inheritance.RecordDeath(s1.Lineage, null, new LifePlace(3, 14, NodeKind.Boss, RunPhase.Fallen),
                new List<CardEntry> { new CardEntry("thrust", 1) }, InheritanceCore.Journal.WithPageCount(0));
            LifeStart s2 = Inheritance.StartLife(l);
            return Inheritance.RecordSurvival(s2.Lineage, null, new LifePlace(4, 9, NodeKind.Rest, RunPhase.Survived),
                new List<CardEntry> { new CardEntry("reach_thrust", 1) }, InheritanceCore.Journal.WithPageCount(0));
        }

        /// <summary>A survivor's 出立 as the screen builds it: 45 cards, 4 tools (the 4th the bonus's), a consumable, a talent (js: limits.test.mjs:426).</summary>
        private static (LifeStart Ls, DeckLimits Limits, DeckBuilder Deck, Departure Departure) SurvivorDeparture()
        {
            LifeStart ls = Inheritance.StartLife(SurvivorLineage());
            var limits = new DeckLimits(ls.DeckMin, ls.DeckMax, ls.ToolSlots, ls.ConsumableSlots);
            List<string> owned = DeckBuilder.WithPrototypeKinds(Expedition.InitialKinds);
            var deck = new DeckBuilder(owned, limits);
            foreach (CardInstance card in DeckBuilder.Prototype(limits).Build()) deck.Add(card.Def.Id);
            foreach (string id in owned)
            {
                while (deck.Total < 45 && deck.Add(id)) { }
                if (deck.Total >= 45) break;
            }
            DepartureBuilder sup = DepartureCarry.NewBuilder(limits);
            foreach (string id in new[] { "bosho_no_men", "bosho_no_men", "maai_no_kutsu", "ikitsugi_no_fue" }) Assert.That(sup.AddTool(id), Is.True, id);
            Assert.That(sup.AddConsumable("yakuso"), Is.True);
            Assert.That(sup.ChooseTalent(deck, deck.Build()[0].Def.Id), Is.True);
            Assert.That(sup.CanSetOut(deck), Is.True);
            return (ls, limits, deck, sup.Build(deck));
        }

        /// <summary>Steps the life to its first fight (fresh nodes first) and returns the expedition's EncounterBattle (js: limits.test.mjs:410).</summary>
        private static EncounterBattle ToFirstFight(Expedition ex)
        {
            ExplorationSession s = ex.Session;
            for (int guard = 0; guard < 200 && !s.HasPendingBattle; guard++)
            {
                if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); continue; }
                ExplorationState st = s.State;
                List<MapNode> open = st.Map.Nodes.Where(n => ExplorationReducer.CanStep(st, n.Id)).ToList();
                List<MapNode> fresh = open.Where(n => !st.HasResolved(n.Id)).ToList();
                s.Step((fresh.Count > 0 ? fresh : open)[0].Id);
            }
            Assert.That(s.HasPendingBattle, Is.True, "a fight waits");
            return ex.StartEncounter();
        }

        /// <summary>The departure's Loadout as the saved start keeps it: ToRunLoadout, then plain data and back (js: limits.test.mjs:405).</summary>
        private static RunLoadout SavedRunLoadout(Loadout loadout, int toolSlots) =>
            Loadouts.RunLoadoutFromData(Json.Parse(Json.Stringify(Loadouts.RunLoadoutToData(Loadouts.ToRunLoadout(loadout, toolSlots)))));

        // js: test/ext/limits.test.mjs:440
        [Test]
        public void L2_HandOff_ASurvivorsFortyFiveCardsAndFourTools_ReachTheExplorationAndTheBattleLaunch()
        {
            (LifeStart ls, DeckLimits limits, DeckBuilder deckBuilder, Departure departure) = SurvivorDeparture();
            Assert.That(limits, Is.EqualTo(Survivor));
            Assert.That(departure.Deck.Count, Is.EqualTo(45));
            CarriedSupplies carry = DepartureCarry.Carry(departure, limits);
            Assert.That(carry.BattleToolIds, Is.EqualTo(new[] { "maai_no_kutsu", "ikitsugi_no_fue" }));
            Assert.That(carry.ConsumableIds, Is.EqualTo(new[] { "yakuso" }));
            Assert.That(carry.Loadout.ToolSlotCount, Is.EqualTo(4));
            // the exploration's RunLoadout, through a save: 4 tools in 4 slots
            RunLoadout runLoadout = SavedRunLoadout(carry.Loadout, limits.ToolSlots);
            Assert.That(runLoadout.ToolSlotCount, Is.EqualTo(4));
            Assert.That(runLoadout.Tools.Select(t => t.Id), Is.EqualTo(new[] { "bosho_no_men", "bosho_no_men", "maai_no_kutsu", "ikitsugi_no_fue" }));
            Expedition Begin(ExpeditionOptions options) => new Expedition(new RunStart { Seed = 20261020, Loadout = runLoadout }, departure.Deck,
                carry.BattleToolIds, departure.TalentCardId, null, null, options);
            // without the limits the C#'s Expedition refuses the 45 cards: the router must forward them
            var refused = Assert.Throws<ArgumentException>(() => Begin(null));
            Assert.That(refused.Message, Does.Contain("Deck has 45 cards; the maximum is 40."));
            Expedition ex = Begin(new ExpeditionOptions { Limits = limits });
            Assert.That(ex.Deck.Count, Is.EqualTo(45));
            // ... and they ride to the battle: the launch carries them and its deck passes Cards.Validate with them
            BattleLaunch launch = ToFirstFight(ex).NextLaunch();
            Assert.That(launch.Limits, Is.EqualTo(limits));
            Assert.That(launch.Deck.Count, Is.EqualTo(45));
            Assert.That(Cards.Validate(launch.Deck, launch.Limits).Ok, Is.True);
            Assert.That(Cards.Validate(launch.Deck).Errors, Is.EqualTo(new[] { "Deck has 45 cards; the maximum is 40." }));

            // the router's way: the choice saved with the life's start, the Expedition rebuilt from it (Life.LifeSetup)
            DepartureChoice choice = DepartureChoice.Of(departure, carry, deckBuilder.Save());
            Assert.That(choice.DeckSave, Is.EqualTo(deckBuilder.Save()));
            LifeStartData start = LifeTestKit.Clone(Life.MakeStart(ls, choice));
            Assert.That(start.DeckMax, Is.EqualTo(45));
            Assert.That(start.ToolSlots, Is.EqualTo(4));
            Assert.That(start.Choice.BattleToolIds, Is.EqualTo(new[] { "maai_no_kutsu", "ikitsugi_no_fue" }));
            Assert.That(start.Choice.Talent, Is.EqualTo(departure.TalentCardId));
            Expedition life = Life.LifeSetup(start).Expedition;
            Assert.That(life.Deck.Select(c => c.InstanceId), Is.EqualTo(departure.Deck.Select(c => c.InstanceId)));
            Assert.That(life.DeckMax, Is.EqualTo(45));
            Assert.That(life.Session.State.Loadout.Tools.Select(t => t.Id), Is.EqualTo(new[] { "bosho_no_men", "bosho_no_men", "maai_no_kutsu", "ikitsugi_no_fue" }));
            Assert.That(ToFirstFight(life).NextLaunch().Limits, Is.EqualTo(limits));
        }

        // js: test/ext/limits.test.mjs:466
        [Test]
        public void L3_HandOff_ASurvivors45CardDeck_StartsItsFirstFight()
        {
            (LifeStart _, DeckLimits limits, DeckBuilder _, Departure departure) = SurvivorDeparture();
            CarriedSupplies carry = DepartureCarry.Carry(departure, limits);
            var ex = new Expedition(new RunStart { Seed = 20261020, Loadout = SavedRunLoadout(carry.Loadout, limits.ToolSlots) },
                departure.Deck, carry.BattleToolIds, departure.TalentCardId, null, null, new ExpeditionOptions { Limits = limits });
            EncounterBattle battle = ToFirstFight(ex);
            Assert.DoesNotThrow(() => battle.StartNext(true));
        }

        // js: test/ext/limits.test.mjs:475
        [Test]
        public void L5_HandOff_OnAFirstLife_EveryToolInTheCSharpsThreeSlots_ReachesTheExploration()
        {
            DeckBuilder deck = DeckBuilder.Prototype();
            foreach (ItemDef tool in ItemCatalogue.Tools)
            {
                DepartureBuilder sup = DepartureCarry.NewBuilder();
                Assert.That(sup.AddTool(tool.Id), Is.True);
                Assert.That(sup.AddTool(tool.Id), Is.True);
                Assert.That(sup.AddTool("bosho_no_men"), Is.True);
                Assert.That(sup.AddTool("bosho_no_men"), Is.False, "the C#'s 3 slots");
                sup.AddConsumable("joka_no_ko");
                sup.ChooseTalent(deck, "thrust");
                Departure departure = sup.Build(deck);
                CarriedSupplies carry = DepartureCarry.Carry(departure);
                Assert.That(carry.Loadout.ToolSlotCount, Is.EqualTo(3));
                Assert.That(carry.BattleToolIds, Is.EqualTo(tool.Side == EffectSide.Battle ? new[] { tool.Id, tool.Id } : Array.Empty<string>()));
                RunLoadout runLoadout = SavedRunLoadout(carry.Loadout, DeckLimits.Default.ToolSlots);
                Assert.That(runLoadout.Tools.Count, Is.EqualTo(3), tool.Id);
                var ex = new Expedition(new RunStart { Seed = 7, Loadout = runLoadout }, departure.Deck, carry.BattleToolIds, departure.TalentCardId);
                Assert.That(ex.Deck.Count, Is.EqualTo(20), tool.Id);
            }
        }

        [Test]
        public void Carry_NeverHasFewerSlotsThanTools()
        {
            // js: scenes/meta/DepartureSupplies.js:51 (Math.max(slots, tools.length))
            var departure = new Departure(PrototypeDeck.Build(), new[] { "bosho_no_men", "bosho_no_men", "bosho_no_men", "bosho_no_men" }, Array.Empty<string>(), 0, null);
            Assert.That(DepartureCarry.Carry(departure).Loadout.ToolSlotCount, Is.EqualTo(4));
            Assert.That(DepartureCarry.Carry(departure).BattleToolIds, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => DepartureCarry.Carry(null));
        }
    }
}
