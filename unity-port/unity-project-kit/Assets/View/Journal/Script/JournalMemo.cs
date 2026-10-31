// The memo of the journal (#107): free text the player writes. The game keeps every note as it was
// written and never reads anything out of it (concept-v3.md §5: 「ゲームは内容を解釈しない」). The drawer
// shows the notes on one memo page; each note is its own unit when the journal is listed as legacy
// candidates (JournalLines), so a dying character can leave one note and not another.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Journal
{
    /// <summary>One note: an id the game gives it and the text exactly as written.</summary>
    public sealed class MemoNote
    {
        public MemoNote(string id, int number, string text)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("a note needs an id", nameof(id));
            Id = id;
            Number = number;
            Text = text ?? "";
        }

        public string Id { get; }

        /// <summary>1 for the first note written in this life, 2 for the next; never reused after an erase.</summary>
        public int Number { get; }

        public string Text { get; }

        /// <summary>「メモ 1」: the name the drawer and the legacy list give the note.</summary>
        public string Title => $"{JournalMemo.Title} {Number}";
    }

    public sealed class JournalMemo
    {
        /// <summary>The drawer's memo page: one page holds every note.</summary>
        public const string PageId = "memo";

        public const string Title = "メモ";

        public const string Empty = "メモはまだありません。";

        private readonly List<MemoNote> _notes = new List<MemoNote>();
        private int _written;

        /// <summary>The notes in the order they were first written.</summary>
        public IReadOnlyList<MemoNote> Notes => _notes;

        /// <summary>Adds a note with this text as written (null is kept as ""). Returns its id.</summary>
        public string Write(string text)
        {
            _written++;
            var note = new MemoNote($"memo-{_written}", _written, text);
            _notes.Add(note);
            return note.Id;
        }

        /// <summary>Replaces a note's text, keeping its place and number. False for an unknown id.</summary>
        public bool Rewrite(string id, string text)
        {
            int index = _notes.FindIndex(n => n.Id == id);
            if (index < 0) return false;
            MemoNote old = _notes[index];
            _notes[index] = new MemoNote(old.Id, old.Number, text);
            return true;
        }

        /// <summary>Removes a note. False for an unknown id.</summary>
        public bool Erase(string id) => _notes.RemoveAll(n => n.Id == id) > 0;

        /// <summary>Empties the memo, as a new life starts with none.</summary>
        public void Clear()
        {
            _notes.Clear();
            _written = 0;
        }

        /// <summary>
        /// The drawer's memo page: each note under its title, or 「メモはまだありません。」 when there is
        /// none. Notes carry no disclosure, so every row reads as written.
        /// </summary>
        public JournalPage Page()
        {
            IReadOnlyList<JournalRow> rows = _notes.Count == 0
                ? new[] { new JournalRow("", Empty) }
                : _notes.Select(n => new JournalRow(n.Title, n.Text)).ToList();
            return new JournalPage(PageId, JournalTab.Memo, Title, rows);
        }
    }
}
