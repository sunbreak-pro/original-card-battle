// The journal drawer's content, shared by every screen that opens the drawer: the exploration
// screen now, and the battle screen once its drawer lands (battle_ui_ux_v2.md, 手記ドロワー).
// Pure C#: which rows a disclosure level hides and which tabs open where are decided here, so the
// drawer View only lays the text out. The pages' real contents are #107's.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Journal
{
    /// <summary>The drawer's tabs: one per enemy, the dungeon's page, the player's memo.</summary>
    public enum JournalTab
    {
        Enemy,
        Dungeon,
        Memo,
    }

    /// <summary>Where the journal is read. In battle only the enemy pages open; while exploring, all of them.</summary>
    public enum JournalContext
    {
        Exploration,
        Battle,
    }

    /// <summary>One line of a page. A row the reader has not learned yet reads as 「？」.</summary>
    public sealed class JournalRow
    {
        public const string Hidden = "？";

        public JournalRow(string label, string text, int needsDisclosure = 0)
        {
            if (needsDisclosure < 0 || needsDisclosure > JournalPage.MaxDisclosure)
                throw new ArgumentOutOfRangeException(nameof(needsDisclosure), needsDisclosure, "disclosure runs 0..2");

            Label = label ?? "";
            Text = text ?? "";
            NeedsDisclosure = needsDisclosure;
        }

        public string Label { get; }

        public string Text { get; }

        /// <summary>The disclosure the page must have reached before this row is readable.</summary>
        public int NeedsDisclosure { get; }
    }

    /// <summary>One page of the journal: an enemy, the dungeon, or the memo.</summary>
    public sealed class JournalPage
    {
        /// <summary>Encounter gives 1, observation gives 2 (concept-v3.md §14).</summary>
        public const int MaxDisclosure = 2;

        public JournalPage(string id, JournalTab tab, string title, IReadOnlyList<JournalRow> rows, int disclosure = MaxDisclosure)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("a page needs an id", nameof(id));
            if (disclosure < 0 || disclosure > MaxDisclosure)
                throw new ArgumentOutOfRangeException(nameof(disclosure), disclosure, "disclosure runs 0..2");

            Id = id;
            Tab = tab;
            Title = title ?? "";
            Rows = rows ?? Array.Empty<JournalRow>();
            Disclosure = disclosure;
        }

        public string Id { get; }

        public JournalTab Tab { get; }

        public string Title { get; }

        public IReadOnlyList<JournalRow> Rows { get; }

        public int Disclosure { get; }

        /// <summary>
        /// 「開示度 1 / 2」 beside an enemy page's title. Dungeon and memo pages carry none, and neither
        /// does an enemy page at 0: a real page exists only after an encounter, so 0 is a stand-in.
        /// </summary>
        public string DisclosureLabel =>
            Tab == JournalTab.Enemy && Disclosure > 0 ? $"開示度 {Disclosure} / {MaxDisclosure}" : "";

        public bool IsHidden(JournalRow row) => row != null && row.NeedsDisclosure > Disclosure;

        /// <summary>What the drawer prints for a row: its text, or 「？」 when it is not learned yet.</summary>
        public string TextOf(JournalRow row) => row == null ? "" : IsHidden(row) ? JournalRow.Hidden : row.Text;

        /// <summary>The page's text as the drawer prints it: each row's label over its text, a blank line between rows.</summary>
        public string Body => string.Join("\n\n", Rows.Select(row =>
            row.Label.Length == 0 ? TextOf(row) : row.Label + "\n" + TextOf(row)));
    }

    /// <summary>The pages one screen hands the drawer, and which of them may be opened there.</summary>
    public sealed class JournalBook
    {
        /// <summary>The drawer's heading.</summary>
        public const string Heading = "手記";

        /// <summary>The close button. J opens and closes the drawer wherever it is used.</summary>
        public const string CloseLabel = "閉じる（J）";

        public JournalBook(IReadOnlyList<JournalPage> pages, JournalContext context)
        {
            Pages = pages ?? Array.Empty<JournalPage>();
            Context = context;
        }

        public IReadOnlyList<JournalPage> Pages { get; }

        public JournalContext Context { get; }

        /// <summary>A line beside the close button. Only the battle has one: the hand waits while the drawer is open.</summary>
        public string ClosingHint => Context == JournalContext.Battle ? "閉じるとプレイできます" : "";

        public bool CanOpen(JournalPage page) =>
            page != null && (Context == JournalContext.Exploration || page.Tab == JournalTab.Enemy);

        public IReadOnlyList<JournalPage> Openable => Pages.Where(CanOpen).ToList();

        /// <summary>The page the drawer shows first: the first that may be opened here, or null.</summary>
        public JournalPage First => Pages.FirstOrDefault(CanOpen);

        /// <summary>The page with this id if it may be opened here, else the first one that may.</summary>
        public JournalPage Find(string id) =>
            Pages.FirstOrDefault(p => p.Id == id && CanOpen(p)) ?? First;

        /// <summary>
        /// What a page's tab says: an enemy's tab carries its name, the others their kind. Every page
        /// gets a tab; one that may not be opened here is shown but not pressable (battle_ui_ux_v2.md).
        /// </summary>
        public static string TabText(JournalPage page) =>
            page.Tab == JournalTab.Enemy ? page.Title : TabLabel(page.Tab);

        public static string TabLabel(JournalTab tab) => tab switch
        {
            JournalTab.Enemy => "敵",
            JournalTab.Dungeon => "ダンジョン",
            JournalTab.Memo => "メモ",
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
        };
    }
}
