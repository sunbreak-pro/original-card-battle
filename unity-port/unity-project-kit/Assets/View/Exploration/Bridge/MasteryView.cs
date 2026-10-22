// Trial extension (port of the browser trial's src/bridge/exploration/MasteryView.js; docs/EXTENSIONS.md X-15; the C#
// screens have no such reading): a card's 習熟 as the exploration's cards print it. 習熟 renames a card (突き → 鋭い突き
// …), so a line that says 「0 → 1 段目」 next to the new name alone names a card the player never held; and the training
// card needs the 刻み toward the next 段 to be a choice at all. Every number comes from the MasteryBook and the card's
// MasteryProfile (BattleCore/Mastery.cs); nothing is decided here.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Exploration.Bridge
{
    /// <summary>
    /// Where a card stands in its 習熟 (js: MasteryView.js:34 <c>{ Rank, Ticks, Need, Grows, Top, Capped, Name, BaseName,
    /// NextName }</c>).
    /// </summary>
    public sealed class MasteryProgressInfo
    {
        public MasteryProgressInfo(int rank, int ticks, int? need, bool grows, bool top, bool capped, string name, string baseName, string nextName)
        {
            Rank = rank;
            Ticks = ticks;
            Need = need;
            Grows = grows;
            Top = top;
            Capped = capped;
            Name = name;
            BaseName = baseName;
            NextName = nextName;
        }

        /// <summary>The card's 段 in this life.</summary>
        public int Rank { get; }

        /// <summary>The 刻み the card has.</summary>
        public int Ticks { get; }

        /// <summary>
        /// The 刻み the next 段 asks (the profile's threshold), null when no 段 can come: a card with no profile (Grows
        /// false), the 3rd 段 (Top), or the 2nd 段 while another card holds the life's one 3rd 段 (Capped).
        /// </summary>
        public int? Need { get; }

        /// <summary>The card has a MasteryProfile (it renames and grows).</summary>
        public bool Grows { get; }

        /// <summary>The card is at the 3rd 段.</summary>
        public bool Top { get; }

        /// <summary>The card is at the 2nd 段 and another card holds the life's 3rd 段 (Constants.MasteryTopRankCards).</summary>
        public bool Capped { get; }

        /// <summary>The name at the card's 段.</summary>
        public string Name { get; }

        /// <summary>The catalogue's name (段 0).</summary>
        public string BaseName { get; }

        /// <summary>The name the next 段 brings, null when the name stays (or no 段 can come).</summary>
        public string NextName { get; }
    }

    /// <summary>js: bridge/exploration/MasteryView.js — a card's 習熟 as the exploration's cards print it.</summary>
    public static class MasteryView
    {
        /// <summary>The card's name at <paramref name="rank"/> in this life's book (the catalogue's name at 0, or for a card that never grows); the id for a card the catalogue does not know.</summary>
        // js: bridge/exploration/MasteryView.js:9
        public static string NameAtRank(MasteryBook book, string cardId, int rank)
        {
            CardDef def;
            try
            {
                def = CardCatalog.ById(cardId);
            }
            catch (ArgumentNullException)
            {
                return cardId;
            }
            catch (KeyNotFoundException)
            {
                return cardId;
            }
            MasteryProfile profile = book?.ProfileOf(cardId);
            if (profile == null || !(rank > 0)) return def.Name;
            return Mastery.Master(def, rank, profile).Name;
        }

        /// <summary>
        /// Where a card stands in its 習熟. Need is the 刻み the next 段 asks (the profile's threshold), null when no 段
        /// can come: a card with no profile (Grows false), the 3rd 段 (Top), or the 2nd 段 while another card holds the
        /// life's one 3rd 段 (Capped, Constants.MasteryTopRankCards). NextName is the name the next 段 brings, null when
        /// the name stays.
        /// </summary>
        // js: bridge/exploration/MasteryView.js:23
        public static MasteryProgressInfo MasteryProgress(MasteryBook book, string cardId)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            int rank = book.RankOf(cardId);
            int ticks = book.TicksOf(cardId);
            MasteryProfile profile = book.ProfileOf(cardId);
            string name = NameAtRank(book, cardId, rank);
            string baseName = NameAtRank(book, cardId, 0);
            bool top = rank >= Mastery.TopRank;
            string holder = book.TopRankHolder;
            bool capped = profile != null && !top && rank == Mastery.TopRank - 1 && holder != null && holder != cardId;
            bool open = profile != null && !top && !capped;
            string next = open ? NameAtRank(book, cardId, rank + 1) : null;
            return new MasteryProgressInfo(
                rank,
                ticks,
                open ? profile.Thresholds[rank] : (int?)null,
                profile != null,
                top,
                capped,
                name,
                baseName,
                next != null && next != name ? next : null);
        }
    }
}
