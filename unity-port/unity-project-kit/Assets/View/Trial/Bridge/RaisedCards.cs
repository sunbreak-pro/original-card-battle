// The cards whose 習熟の段 a won fight raised, as the win result card lays them out (battle-visual-v1 §5.6, #910): each
// card's face at its new 段, the 段 it rose from and to, and its name before and after. The 段 come from the core
// (Expedition.RankUpsOfLastFight, worked out the way Return writes them); the result card only lays these out.
//
// Engine-free (no UnityEngine).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using Exploration.Bridge;

namespace Trial.Bridge
{
    /// <summary>One card on the win result card's 「習熟が上がった札」 (battle-visual-v1 §5.6).</summary>
    public sealed class RaisedCard
    {
        public RaisedCard(string cardId, int from, int to, string name, string was, CardFace face)
        {
            CardId = cardId;
            From = from;
            To = to;
            Name = name;
            Was = was;
            Face = face;
        }

        public string CardId { get; }

        /// <summary>The 段 before the fight.</summary>
        public int From { get; }

        /// <summary>The 段 the fight raised it to.</summary>
        public int To { get; }

        /// <summary>The name at the new 段.</summary>
        public string Name { get; }

        /// <summary>The name at the old 段, or null when the 段 kept the name.</summary>
        public string Was { get; }

        /// <summary>The face at the new 段: 段 go up only at a battle's end, so the card is shown as it will be dealt next.</summary>
        public CardFace Face { get; }
    }

    public static class RaisedCards
    {
        /// <summary>
        /// §5.6 「多いとき」: the cards drawn as faces; from the next one on the card is named only. 3 is provisional (the
        /// design reviews it after a run), and fits the paper's inner width (3 × 173 + 2 × 16 = 551 of 608).
        /// </summary>
        public const int FacesMax = 3;

        /// <summary>The cards the fight that ended last raised, in the order the result card shows them; empty when none.</summary>
        public static IReadOnlyList<RaisedCard> OfLastFight(Expedition expedition)
        {
            if (expedition == null) return Array.Empty<RaisedCard>();
            return Of(expedition.RankUpsOfLastFight(), expedition.Mastery);
        }

        /// <summary>
        /// <paramref name="ups"/> as the result card shows them: the higher new 段 first and, on a tie, the core's order
        /// (§5.6 「並べる順」). <paramref name="book"/> gives the profiles that name each 段.
        /// </summary>
        public static IReadOnlyList<RaisedCard> Of(IReadOnlyList<RankUp> ups, MasteryBook book)
        {
            if (ups == null || ups.Count == 0) return Array.Empty<RaisedCard>();
            var cards = new List<RaisedCard>();
            // OrderByDescending is stable, so equal 段 keep the order the core returned them in.
            foreach (RankUp up in ups.OrderByDescending(u => u.To))
            {
                CardDef def = CardCatalog.ById(up.CardId);
                MasteryProfile profile = book?.ProfileOf(up.CardId);
                CardDef at = profile != null && up.To > 0 ? Mastery.Master(def, up.To, profile) : def;
                string was = MasteryView.NameAtRank(book, up.CardId, up.From);
                cards.Add(new RaisedCard(up.CardId, up.From, up.To, at.Name, was != at.Name ? was : null, CoreText.FaceOf(at)));
            }
            return cards;
        }
    }
}
