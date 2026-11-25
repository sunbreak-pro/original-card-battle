// 出立's pack before it sets out (#1052): the deck, the tools and the consumables of the life about to go down, read
// from 出立's draft (the table, 「支度の卓」, keeps it while the player steps back to the base), else from the deck the
// last life set out with, else the starter deck. The base's way down (「降り口の前」) shows it and sets out with it, and
// the table opens on it, so both read one pack and the choice either one makes is the same (DepartureChoice).
//
// Engine-free, so `dotnet test` holds the flow (Trial.Bridge.Tests/DeparturePackTests). The rules stay the builders'
// (DeckBuilder, DepartureBuilder); the draft's memory copy and the device store are the table's (Trial.Meta).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using Exploration.Bridge;
using InheritanceCore;

namespace Trial.Bridge
{
    /// <summary>
    /// 出立's draft (departure.js:52-90, less the talent since #1045): { id, deck: DeckBuilder.Save, tools, consumables }
    /// of one life, kept under <see cref="Key"/> until the life sets out. A draft stored with a talent is read past it.
    /// </summary>
    public sealed class DepartureDraft
    {
        // js: scenes/meta/departure.js:62
        public const string Key = Save.DepartureDraftKey;

        public string Id = "";
        public string Deck = "";
        public List<string> Tools = new List<string>();
        public List<string> Consumables = new List<string>();

        // js: scenes/meta/departure.js:64
        /// <summary>draftIdOf(lifeStart): `lineageSeed|generation|lifeSeed`, or null without a life.</summary>
        public static string IdOf(LifeStart lifeStart)
        {
            if (lifeStart == null || lifeStart.Life == null) return null;
            string lineageSeed = lifeStart.Lineage != null ? lifeStart.Lineage.Seed : "";
            return string.Join("|", lineageSeed ?? "", lifeStart.Life.Generation.ToString(CultureInfo.InvariantCulture),
                lifeStart.Seed.ToString(CultureInfo.InvariantCulture));
        }

        // js: scenes/meta/departure.js:69
        /// <summary>storedDraft(storage, id): the stored draft of this life, or null (no store, nothing stored, broken, another life).</summary>
        public static DepartureDraft Read(IKeyValueStore store, string id)
        {
            if (store == null || id == null) return null;
            try
            {
                string text = store.Read(Key);
                object d = string.IsNullOrEmpty(text) ? null : Json.Parse(text);
                if (d is JsonObject o && o["id"] is string did && did == id && o["deck"] is string deck)
                {
                    var draft = new DepartureDraft { Id = did, Deck = deck };
                    if (o["tools"] is List<object> tools) foreach (object t in tools) if (t is string s) draft.Tools.Add(s);
                    if (o["consumables"] is List<object> cons) foreach (object c in cons) if (c is string s) draft.Consumables.Add(s);
                    return draft;
                }
            }
            catch (Exception)
            {
                // storage unavailable or the text broken: no draft
            }
            return null;
        }

        // js: scenes/meta/departure.js:81
        /// <summary>The draft written under <see cref="Key"/> (a store that refuses keeps nothing).</summary>
        public void Write(IKeyValueStore store)
        {
            if (store == null) return;
            try
            {
                var o = new JsonObject();
                o.Set("id", Id);
                o.Set("deck", Deck);
                o.Set("tools", Tools.Cast<object>().ToList());
                o.Set("consumables", Consumables.Cast<object>().ToList());
                store.Write(Key, Json.Stringify(o));
            }
            catch (Exception)
            {
                // storage unavailable
            }
        }

        // js: scenes/meta/departure.js:85
        public static void Clear(IKeyValueStore store)
        {
            if (store == null) return;
            try { store.Remove(Key); }
            catch (Exception) { /* storage unavailable */ }
        }
    }

    /// <summary>The pack a life opens 出立 with, and what it sets out with.</summary>
    public sealed class DeparturePack
    {
        private DeparturePack(List<string> owned, DeckLimits limits, DeckBuilder deck, string notice, DepartureBuilder supplies, string draftId)
        {
            Owned = owned;
            Limits = limits;
            Deck = deck;
            Notice = notice ?? "";
            Supplies = supplies;
            DraftId = draftId;
        }

        /// <summary>The kinds the deck may be built from; null: the whole catalogue.</summary>
        public List<string> Owned { get; }

        /// <summary>The life's limits (DeckMin / DeckMax / ToolSlots / ConsumableSlots, the survivor's bonus included).</summary>
        public DeckLimits Limits { get; }

        public DeckBuilder Deck { get; }

        /// <summary>Why a saved deck opened as it did (DeckBuilder.ReadOverCap's notice), or "".</summary>
        public string Notice { get; }

        /// <summary>The saved deck opened past the life's cap (a survivor's 45 after the bonus ended).</summary>
        public bool OverCap => Deck.Total > Limits.DeckMax;

        public DepartureBuilder Supplies { get; }

        /// <summary>The life's draft id (<see cref="DepartureDraft.IdOf"/>), or null without a life.</summary>
        public string DraftId { get; }

        /// <summary>The deck and the pack may set out (the deck's size and copies, the slots).</summary>
        public bool CanSetOut => Supplies.CanSetOut(Deck);

        /// <summary>DeckLimits.From(lifeStart): the life's four numbers (a missing one, or no life, is the C#'s).</summary>
        public static DeckLimits LimitsOf(LifeStart lifeStart)
        {
            if (lifeStart == null) return DeckLimits.Default;
            int? min = lifeStart.DeckMin > 0 ? lifeStart.DeckMin : (int?)null;
            int? max = lifeStart.DeckMax > 0 && lifeStart.DeckMax >= (min ?? 1) ? lifeStart.DeckMax : (int?)null;
            int? tools = lifeStart.ToolSlots >= 0 ? lifeStart.ToolSlots : (int?)null;
            int? cons = lifeStart.ConsumableSlots >= 0 ? lifeStart.ConsumableSlots : (int?)null;
            try { return new DeckLimits(min, max, tools, cons); }
            catch (ArgumentOutOfRangeException) { return DeckLimits.Default; }
        }

        // js: scenes/meta/departure.js:109-157
        /// <summary>
        /// The pack 出立 opens with: the kinds the heir may build from (the router's, else the initial ones, plus the starter
        /// deck's; null with <paramref name="ownsCatalogue"/>), the life's limits, then the draft's deck and supplies when
        /// <paramref name="draft"/> is this life's, else <paramref name="savedDeck"/> (read over the cap with its reason),
        /// else the starter deck with an empty pack.
        /// </summary>
        public static DeparturePack Open(LifeStart lifeStart, IReadOnlyList<string> owned, bool ownsCatalogue, string savedDeck, DepartureDraft draft)
        {
            List<string> kinds = ownsCatalogue ? null
                : DeckBuilder.WithStarterKinds(owned != null && owned.Count > 0 ? owned : Expedition.InitialKinds);
            DeckLimits limits = LimitsOf(lifeStart);
            string draftId = DepartureDraft.IdOf(lifeStart);
            if (draft != null && draft.Id != draftId) draft = null;
            string saved = draft != null ? draft.Deck : savedDeck;
            DeckBuilder deck;
            string notice = "";
            if (draft != null || savedDeck != null)
            {
                deck = DeckBuilder.ReadOverCap(saved, out string value, limits).WithOwned(kinds);
                notice = value ?? "";
            }
            else
            {
                deck = DeckBuilder.Starter(limits).WithOwned(kinds);
            }
            DepartureBuilder supplies = DepartureCarry.NewBuilder(limits);
            if (draft != null)
            {
                foreach (string id in draft.Tools) supplies.AddTool(id);
                foreach (string id in draft.Consumables) supplies.AddConsumable(id);
            }
            return new DeparturePack(kinds, limits, deck, notice, supplies, draftId);
        }

        /// <summary>The draft of this pack as it stands (null without a life to key it).</summary>
        public DepartureDraft Draft() => DraftId == null ? null : new DepartureDraft
        {
            Id = DraftId,
            Deck = Deck.Save(),
            Tools = Supplies.Tools.ToList(),
            Consumables = Supplies.Consumables.ToList(),
        };

        // js: scenes/meta/departure.js:702
        /// <summary>
        /// What the life sets out with (departure.js setOut): the built departure, the tools that act inside a battle for
        /// the battles and the whole loadout for the exploration (DepartureCarry.Carry, #104), and the deck string the save
        /// keeps. Throws when the pack may not set out.
        /// </summary>
        public DepartureChoice Choice()
        {
            if (!CanSetOut) throw new InvalidOperationException("the deck and the pack may not set out: " + Deck.StatusText);
            Departure departure = Supplies.Build(Deck);
            return DepartureChoice.Of(departure, DepartureCarry.Carry(departure, Limits), Deck.Save());
        }
    }
}
