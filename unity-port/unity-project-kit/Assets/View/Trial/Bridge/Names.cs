// What the inheritance module needs from a finished life that the ported C# does not carry as one value (port of the
// browser trial's src/app/names.js): the journal (開示度 by enemy, the rare pages, the memo and inherited 走り書き, the
// carving read) and the display names stored in a trace, so the hall can draw it without the catalogues; and, for a
// cleared life, 歪みの根's cards (docs/INHERITANCE.md R-18 / 試作 10).
//
// Engine-free (no UnityEngine).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonCore;
using Exploration.Bridge;
using InheritanceCore;
using LifeJournalData = InheritanceCore.Journal;

namespace Trial.Bridge
{
    public static class Names
    {
        /// <summary>
        /// The journal a finished life leaves (js: names.js lifeJournal(expedition, live)): the Expedition's 開示度 above 0
        /// in the order the life learned them, its rare pages as a count (Expedition.Pages, the inherited ones among them),
        /// the heir's memo and the 走り書き the life carries (the router's <c>live</c>), and whether the carving was read.
        /// Inheritance.RecordEnd normalises it.
        /// </summary>
        // js: app/names.js:10
        public static LifeJournalData LifeJournal(Expedition expedition, string memo = null, IEnumerable<Scribble> scribbles = null)
        {
            if (expedition == null) throw new ArgumentNullException(nameof(expedition));
            var disclosure = new List<KeyValuePair<string, int>>();
            foreach (KeyValuePair<string, int> pair in expedition.JournalLevels)
            {
                if (pair.Value > 0) disclosure.Add(pair);
            }
            return LifeJournalData.WithPageCount(
                expedition.Pages,
                disclosure,
                memo ?? "",
                (scribbles ?? Array.Empty<Scribble>()).ToList(),
                expedition.Session.CarvingTaken);
        }

        /// <summary>
        /// <see cref="LifeJournal(Expedition, string, IEnumerable{Scribble})"/> with the router's live life: its memo and
        /// its 走り書き as the save holds them (ILiveLife.ScribblesJson, read leniently as Life.ScribblesFromJson).
        /// </summary>
        // js: app/names.js:10
        public static LifeJournalData LifeJournal(Expedition expedition, ILiveLife live) =>
            LifeJournal(expedition, live?.Memo, live == null ? null : Life.ScribblesFromJson(live.ScribblesJson));

        /// <summary>The card's name at its 段 (習熟 renames a card: 突き → 伸び突き …), else its catalogue name, else its id.</summary>
        // js: app/names.js:23
        public static string CardName(string cardId, int rank = 0)
        {
            MasteryProfile profile = cardId == null ? null : MasteryProfiles.Find(MasteryProfiles.All, cardId);
            string grown = profile?.NameAt(rank);
            if (!string.IsNullOrEmpty(grown)) return grown;
            try
            {
                return CardCatalog.ById(cardId).Name;
            }
            catch (KeyNotFoundException)
            {
                return cardId;
            }
            catch (ArgumentException)
            {
                return cardId;
            }
        }

        /// <summary>The enemy's name (the battle core's), else its id.</summary>
        // js: app/names.js:30
        public static string EnemyName(string enemyId)
        {
            try
            {
                return Enemies.ById(enemyId).Name;
            }
            catch (KeyNotFoundException)
            {
                return enemyId;
            }
            catch (ArgumentException)
            {
                return enemyId;
            }
        }

        /// <summary>歪みの根's three cards: they pass only as legacy (roster §6.6 / §7.4, INHERITANCE.md 試作 10).</summary>
        // js: app/names.js:35
        private static List<CardEntry> RootCards() =>
            UniqueCards.OfBoss("distortion_root").Select(id => new CardEntry(id)).ToList();

        /// <summary>
        /// The options Inheritance.RecordEnd takes for this life: the display names (CardName, EnemyName), and 歪みの根's
        /// cards when the life cleared the dungeon (<paramref name="state"/>.Phase is Completed).
        /// </summary>
        // js: app/names.js:39
        public static EndOptions EndOptions(Expedition expedition, ExplorationState state)
        {
            var opts = new EndOptions
            {
                CardName = (id, rank) => CardName(id, rank),
                EnemyName = EnemyName,
            };
            if (state != null && state.Phase == RunPhase.Completed) opts.RootCards = RootCards();
            return opts;
        }
    }
}
