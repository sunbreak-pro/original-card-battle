// The rows of the pile lists (battle_ui_ux_v2 §1.1 C4, #455): what the draw, discard and exile piles
// hold, one row per kind of card with how many the pile holds and how many the battle's deck holds
// ("突き 2 / 3"). Pure C#: BattleCore + Depiction.Script, no UnityEngine.
//
// The counting is presentation, like the lamps' "any enemy" fold: the core keeps the piles
// (BattleState.DrawPile / DiscardPile / Exiled) and this only groups them. The rows follow the card
// catalogue's order, never the pile's, so the list does not give away what is drawn next.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public static class PileCards
    {
        private static Dictionary<string, int> _catalogOrder;

        /// <summary>Writes the three lists of <paramref name="state"/> onto <paramref name="piles"/>.</summary>
        public static void Fill(PileFrame piles, BattleState state)
        {
            if (piles == null) throw new ArgumentNullException(nameof(piles));
            if (state == null) throw new ArgumentNullException(nameof(state));
            var deck = new Dictionary<string, int>();
            Count(deck, state.Hand);
            Count(deck, state.DrawPile);
            Count(deck, state.DiscardPile);
            Count(deck, state.Exiled);
            piles.DrawCards = Rows(state.DrawPile, deck);
            piles.DiscardCards = Rows(state.DiscardPile, deck);
            piles.ExileCards = Rows(state.Exiled, deck);
        }

        /// <summary>One row per name in <paramref name="pile"/>, in the catalogue's order (then by name).</summary>
        public static List<PileCard> Rows(IReadOnlyList<CardInstance> pile, IReadOnlyDictionary<string, int> deck)
        {
            var inPile = new Dictionary<string, int>();
            var firstId = new Dictionary<string, string>();
            foreach (CardInstance card in pile)
            {
                string name = card.Def.Name;
                inPile[name] = inPile.TryGetValue(name, out int n) ? n + 1 : 1;
                if (!firstId.ContainsKey(name)) firstId[name] = card.Def.Id;
            }
            var names = new List<string>(inPile.Keys);
            names.Sort((a, b) =>
            {
                int byCatalog = OrderOf(firstId[a]).CompareTo(OrderOf(firstId[b]));
                return byCatalog != 0 ? byCatalog : string.CompareOrdinal(a, b);
            });
            var rows = new List<PileCard>();
            foreach (string name in names)
            {
                rows.Add(new PileCard
                {
                    Name = name,
                    InPile = inPile[name],
                    InDeck = deck != null && deck.TryGetValue(name, out int all) ? all : inPile[name],
                });
            }
            return rows;
        }

        private static void Count(Dictionary<string, int> deck, IReadOnlyList<CardInstance> cards)
        {
            foreach (CardInstance card in cards)
            {
                string name = card.Def.Name;
                deck[name] = deck.TryGetValue(name, out int n) ? n + 1 : 1;
            }
        }

        /// <summary>Where the card stands in CardCatalog.All; a card the catalogue lacks goes after every one it has.</summary>
        private static int OrderOf(string id)
        {
            if (_catalogOrder == null)
            {
                var order = new Dictionary<string, int>();
                int i = 0;
                foreach (CardDef def in CardCatalog.All)
                {
                    if (!order.ContainsKey(def.Id)) order[def.Id] = i;
                    i++;
                }
                _catalogOrder = order;
            }
            return _catalogOrder.TryGetValue(id ?? "", out int index) ? index : int.MaxValue;
        }
    }
}
