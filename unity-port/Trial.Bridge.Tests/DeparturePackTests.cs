// The way from the base to the exploration (#1052): the base's way down (「降り口の前」) shows the pack the table
// (「支度の卓」) opens with and sets out with it; the table's draft brings a rebuilt deck and the supplies back to the base,
// and holding 「降りる」 there sets out with them. The pack is read the same way the table reads it (DeparturePack.Open).
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    public sealed class DeparturePackTests
    {
        /// <summary>What the router hands 出立 (Main.ToDeparture): the starter's kinds plus what earlier descents learned.</summary>
        private static List<string> OwnedOf(LifeStart ls) => DeckBuilder.WithStarterKinds(Expedition.InitialKinds
            .Concat(ls.Carryover?.Owned ?? (IEnumerable<string>)System.Array.Empty<string>())
            .Distinct()
            .ToList());

        private static Lineage NewCarryOver() => Inheritance.NewLineage(7, carryOver: true);

        [Test]
        public void TheFirstDescent_OpensTheStarterDeck_AndSetsOutFromTheBase()
        {
            Lineage lineage = NewCarryOver();
            LifeStart ls = Inheritance.StartLife(lineage);
            DeparturePack pack = DeparturePack.Open(ls, OwnedOf(ls), false, null, null);

            Assert.That((pack.Deck.Total, pack.CanSetOut), Is.EqualTo((20, true)), "the starter deck, legal as it opens");
            Assert.That(pack.Supplies.Tools, Is.Empty);
            Assert.That(pack.Supplies.Consumables, Is.Empty);
            Assert.That((pack.Limits.ToolSlots, pack.Limits.ConsumableSlots), Is.EqualTo((3, 3)), "no survivor's bonus under the carry-over");

            // holding 降りる sets out with it: the start the router makes carries the deck into the exploration
            DepartureChoice choice = pack.Choice();
            LifeStartData start = Life.MakeStart(ls, choice);
            Assert.That(start.Choice.Deck.Count, Is.EqualTo(20));
            Assert.That(choice.DeckSave, Is.EqualTo(pack.Deck.Save()));
            Assert.That(Life.LifeSetup(start).Expedition, Is.Not.Null, "the exploration starts");
        }

        [Test]
        public void TheTablesDraft_ComesBackToTheBase_AndTheBaseSetsOutWithIt()
        {
            Lineage lineage = NewCarryOver();
            var store = new MemoryKeyValueStore();

            // the table: one card out, two tools and a consumable in, then back to the base (the draft is kept)
            LifeStart atTable = Inheritance.StartLife(lineage);
            DeparturePack table = DeparturePack.Open(atTable, OwnedOf(atTable), false, null, DepartureDraft.Read(store, DepartureDraft.IdOf(atTable)));
            Assert.That(table.Deck.Remove(CardCatalog.KesaCut.Id), Is.True);
            Assert.That(table.Supplies.AddTool("hiyomi_no_suna"), Is.True);
            Assert.That(table.Supplies.AddTool("ikitsugi_no_fue"), Is.True);
            Assert.That(table.Supplies.AddConsumable("yakuso"), Is.True);
            table.Draft().Write(store);

            // the base again: the same life is started again from the same lineage, and reads the same draft
            LifeStart atBase = Inheritance.StartLife(lineage);
            Assert.That(DepartureDraft.IdOf(atBase), Is.EqualTo(DepartureDraft.IdOf(atTable)), "starting the life again names the same draft");
            DeparturePack gate = DeparturePack.Open(atBase, OwnedOf(atBase), false, null, DepartureDraft.Read(store, DepartureDraft.IdOf(atBase)));
            Assert.That(gate.Deck.Total, Is.EqualTo(19));
            Assert.That(gate.Supplies.Tools, Is.EqualTo(new[] { "hiyomi_no_suna", "ikitsugi_no_fue" }));
            Assert.That(gate.Supplies.Consumables, Is.EqualTo(new[] { "yakuso" }));
            Assert.That(gate.CanSetOut, Is.False, "19 cards: under the minimum, so the base does not set out");
            Assert.That(() => gate.Choice(), Throws.InvalidOperationException);

            // the table once more puts a card back; the base sets out with the deck and the pack as the table left them
            Assert.That(gate.Deck.Add(CardCatalog.KesaCut.Id), Is.True);
            gate.Draft().Write(store);
            DeparturePack again = DeparturePack.Open(atBase, OwnedOf(atBase), false, null, DepartureDraft.Read(store, DepartureDraft.IdOf(atBase)));
            Assert.That(again.CanSetOut, Is.True);
            DepartureChoice choice = again.Choice();
            Assert.That(choice.Deck.Count, Is.EqualTo(20));
            Assert.That(choice.BattleToolIds, Is.EqualTo(new[] { "ikitsugi_no_fue" }), "the tools that act in battle go to the battles");
            Assert.That(choice.ConsumableIds, Is.EqualTo(new[] { "yakuso" }));
            LifeStartData start = Life.MakeStart(atBase, choice);
            Assert.That(Life.LifeSetup(start).Expedition.Session.State.Loadout.Tools.Count, Is.EqualTo(2), "the pack goes down with the explorer");
        }

        [Test]
        public void ADraftOfAnotherLife_IsReadPast_AndTheSavedDeckOpens()
        {
            Lineage lineage = NewCarryOver();
            LifeStart ls = Inheritance.StartLife(lineage);
            var store = new MemoryKeyValueStore();
            new DepartureDraft { Id = "another|1|2", Deck = "", Tools = new List<string> { "hiyomi_no_suna" } }.Write(store);
            Assert.That(DepartureDraft.Read(store, DepartureDraft.IdOf(ls)), Is.Null);

            DeckBuilder saved = DeckBuilder.Starter();
            Assert.That(saved.Remove(CardCatalog.KesaCut.Id), Is.True);
            DeparturePack pack = DeparturePack.Open(ls, OwnedOf(ls), false, saved.Save(),
                new DepartureDraft { Id = "another|1|2", Deck = DeckBuilder.Starter().Save() });
            Assert.That(pack.Deck.Total, Is.EqualTo(19), "the deck the last descent set out with, not another life's draft");
            Assert.That(pack.Supplies.Tools, Is.Empty);

            DepartureDraft.Clear(store);
            Assert.That(store.Read(DepartureDraft.Key), Is.Null);
            Assert.That(DepartureDraft.Key, Is.EqualTo(Save.DepartureDraftKey));
        }

        [Test]
        public void NoLife_OpensTheDefaultLimits_AndNoDraft()
        {
            DeparturePack pack = DeparturePack.Open(null, null, true, null, null);
            Assert.That(pack.DraftId, Is.Null);
            Assert.That(pack.Draft(), Is.Null);
            Assert.That(pack.Owned, Is.Null, "the whole catalogue");
            Assert.That(pack.Limits.DeckMax, Is.EqualTo(DeckLimits.Default.DeckMax));
        }
    }
}
