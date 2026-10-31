// The journal in one line per page (#107), for the list of legacy candidates: concept-v3.md §14 lays
// out every filled page beside every card of mastery 1 or more, and asks the journal to be compact and
// orderly so that list stays readable. Each line is 「種類｜題｜中身」 with the same three fields for
// every kind, so the lines read as a table even in plain text (dungeon_exploration_v4.md §6.11).
//
// A filled page is: an enemy page at 開示度 1 or more, a layer's dungeon page with one entry or more at
// 1 or more, and a memo note with text in it. The page of terms and statuses is never a candidate
// (CAMP_FACILITIES_DESIGN.md §3.3): it is the same in every life.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Journal
{
    /// <summary>One page in one line: which page, its kind, and the line.</summary>
    public sealed class JournalLine
    {
        public JournalLine(string pageId, JournalTab tab, string title, string summary)
        {
            if (string.IsNullOrEmpty(pageId)) throw new ArgumentException("a line needs the page's id", nameof(pageId));
            PageId = pageId;
            Tab = tab;
            Title = title ?? "";
            Summary = summary ?? "";
        }

        /// <summary>The page the line stands for: an enemy page, a layer's dungeon page or one memo note.</summary>
        public string PageId { get; }

        public JournalTab Tab { get; }

        public string Title { get; }

        public string Summary { get; }

        /// <summary>「敵｜錆槍の竜兵｜開示度 2 / 2（弱点まで）」.</summary>
        public string Text => $"{JournalBook.TabLabel(Tab)}{JournalLines.Separator}{Title}{JournalLines.Separator}{Summary}";

        public override string ToString() => Text;
    }

    public static class JournalLines
    {
        public const string Separator = "｜";

        /// <summary>How many characters of a note's first line the list shows before 「…」.</summary>
        public const int MemoPreviewLength = 20;

        /// <summary>An enemy page at 開示度 1 or more as one line; null at 0, which is no page yet.</summary>
        public static JournalLine Enemy(string enemyId, int disclosure)
        {
            int level = EnemyJournal.Clamp(disclosure);
            if (level < EnemyJournal.MovesDisclosure) return null;
            EnemyJournalEntry entry = EnemyJournal.Of(enemyId);
            string reach = level >= EnemyJournal.WeaknessDisclosure ? "弱点まで" : "間合いごとの動きまで";
            return new JournalLine(EnemyJournal.PageId(entry.Id), JournalTab.Enemy, entry.Name,
                $"開示度 {level} / {JournalPage.MaxDisclosure}（{reach}）");
        }

        /// <summary>
        /// A layer's dungeon page as one line: how many of its entries are known, and how many in full.
        /// Null when none is known.
        /// </summary>
        public static JournalLine Dungeon(int layer, Func<string, int> levelOf)
        {
            if (levelOf == null) throw new ArgumentNullException(nameof(levelOf));
            var levels = DungeonJournal.OfLayer(layer).Select(e => levelOf(e.Id)).ToList();
            int known = levels.Count(l => l >= DungeonJournal.MetDisclosure);
            if (known == 0) return null;
            int full = levels.Count(l => l >= DungeonJournal.FullDisclosure);
            return new JournalLine(DungeonJournal.PageId(layer), JournalTab.Dungeon, $"第 {layer} 層",
                $"分かったこと {known} 件（うち中身まで {full} 件）");
        }

        /// <summary>A memo note as one line: its first line, cut at 20 characters. Null for a note with no text.</summary>
        public static JournalLine Memo(MemoNote note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            if (string.IsNullOrWhiteSpace(note.Text)) return null;
            string first = note.Text.Split('\n')[0].TrimEnd('\r');
            string preview = first.Length > MemoPreviewLength ? first.Substring(0, MemoPreviewLength) + "…" : first;
            return new JournalLine(note.Id, JournalTab.Memo, note.Title, preview);
        }

        /// <summary>
        /// Every filled page of one life as lines, in the drawer's order: the enemies in the roster's
        /// order, the layers from the top, then the notes as written. The terms are never listed.
        /// </summary>
        public static IReadOnlyList<JournalLine> Candidates(Func<string, int> enemyLevel, Func<string, int> entryLevel, JournalMemo memo)
        {
            if (enemyLevel == null) throw new ArgumentNullException(nameof(enemyLevel));
            if (entryLevel == null) throw new ArgumentNullException(nameof(entryLevel));
            var lines = new List<JournalLine>();
            lines.AddRange(EnemyJournal.All.Select(e => Enemy(e.Id, enemyLevel(e.Id))).Where(l => l != null));
            lines.AddRange(DungeonJournal.All.Select(e => e.Layer).Distinct().OrderBy(l => l)
                .Select(layer => Dungeon(layer, entryLevel)).Where(l => l != null));
            if (memo != null) lines.AddRange(memo.Notes.Select(Memo).Where(l => l != null));
            return lines;
        }
    }
}
