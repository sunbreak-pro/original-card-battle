// The title's menu as rules, without the screen (scenes/meta/title.js:52-105, :151-160): はじめから / つづきから
// / 設定, which may be chosen, where the cursor starts, how ↑↓ walk past the shut items, and what choosing one
// asks the scene to do. The scene keeps the looks, the sounds (it plays what Focus / Move return) and the walk
// into the dark (Leave), and sets Busy when it leaves, as the JS's `busy`.
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Trial.Script
{
    /// <summary>What choosing an item asks the scene to do (title.js:56-65).</summary>
    public enum TitleChoice
    {
        /// <summary>Nothing: the scene is leaving, or the item is shut.</summary>
        None,

        /// <summary>はじめから with no save: leave(onNew, true) (titleEnter, uiConfirm, the walk into the dark, then onNew).</summary>
        LeaveNew,

        /// <summary>
        /// はじめから over a save (or an unreadable one): uiClick, then onNew() at once — the router asks over the title as
        /// it stands (its confirm card), and moves on to the hall itself if the player confirms (no walk out).
        /// </summary>
        AskNew,

        /// <summary>つづきから: leave(onContinue, true).</summary>
        LeaveContinue,

        /// <summary>設定: uiClick, then onSettings().</summary>
        Settings,
    }

    /// <summary>A key of the title's handler (title.js:154-158), by the JS KeyboardEvent.key it reads.</summary>
    public enum TitleKey
    {
        /// <summary>Not the title's: the handler returns false.</summary>
        None,
        Up,
        Down,
        Choose,
        Settings,
    }

    /// <summary>One row of the menu.</summary>
    public sealed class TitleMenuItem
    {
        public TitleMenuItem(string key, string label, bool enabled, string note)
        {
            Key = key;
            Label = label;
            Enabled = enabled;
            Note = note;
        }

        /// <summary>"new", "continue" or "settings".</summary>
        public string Key { get; }

        public string Label { get; }

        /// <summary>Shut items read .4 and are skipped by ↑↓ (`is-off`).</summary>
        public bool Enabled { get; }

        /// <summary>The `small` beside a shut つづきから (保存された生は無い / 記録を読み込めない); null elsewhere.</summary>
        public string Note { get; }
    }

    public sealed class TitleMenu
    {
        public const string KeyNew = "new";
        public const string KeyContinue = "continue";
        public const string KeySettings = "settings";

        private readonly bool _hasSave;
        private readonly bool _unreadable;

        /// <param name="hasSave">ctx.hasSave: a lineage is saved (つづきから opens, and the cursor starts on it).</param>
        /// <param name="unreadable">ctx.unreadable: a save is stored but could not be read.</param>
        /// <param name="hasSettings">ctx.onSettings is given (設定 opens).</param>
        // js: scenes/meta/title.js:61
        public TitleMenu(bool hasSave, bool unreadable, bool hasSettings)
        {
            _hasSave = hasSave;
            _unreadable = unreadable;
            Items = new ReadOnlyCollection<TitleMenuItem>(new[]
            {
                new TitleMenuItem(KeyNew, MetaText.NewGame, true, null),
                new TitleMenuItem(KeyContinue, MetaText.Continue, hasSave,
                    hasSave ? null : unreadable ? MetaText.Unreadable : MetaText.NoSave),
                new TitleMenuItem(KeySettings, MetaText.Settings, hasSettings, null),
            });
            // js: scenes/meta/title.js:85 — つづきから with a save, はじめから without
            Cur = IndexOf(hasSave ? KeyContinue : KeyNew);
            Lit = Cur;
        }

        public IReadOnlyList<TitleMenuItem> Items { get; }

        /// <summary>The cursor: what Enter / Space choose.</summary>
        public int Cur { get; private set; }

        /// <summary>
        /// The row drawn on (`is-on`: the amber mark, the wider spacing, the glow). It follows Cur on every focus; a
        /// choice moves Cur alone, as the JS does (choose sets `cur` without redrawing the rows).
        /// </summary>
        public int Lit { get; private set; }

        /// <summary>The scene is leaving (the JS's `busy`): nothing is chosen or moved by keys any more.</summary>
        public bool Busy { get; set; }

        /// <summary>
        /// Puts the cursor on item <paramref name="i"/> (a pointer entering it, or ↑↓). Returns the sound to play —
        /// "nodeHover" by pointer, "uiClick" by key — or null when nothing moved (the same item, or a shut one).
        /// </summary>
        // js: scenes/meta/title.js:87
        public string Focus(int i, bool fromPointer)
        {
            if (i == Cur || i < 0 || i >= Items.Count || !Items[i].Enabled) return null;
            Cur = i;
            Lit = i;
            return fromPointer ? "nodeHover" : "uiClick";
        }

        /// <summary>
        /// ↑ (−1) / ↓ (+1): the next item that may be chosen, round the ends, skipping the shut ones. Returns the
        /// sound <see cref="Focus"/> returns, or null.
        /// </summary>
        // js: scenes/meta/title.js:94
        public string Move(int by)
        {
            int i = Cur;
            for (int n = 0; n < Items.Count; n++)
            {
                i = ((i + by) % Items.Count + Items.Count) % Items.Count;
                if (Items[i].Enabled) return Focus(i, false);
            }
            return null;
        }

        /// <summary>
        /// Chooses item <paramref name="i"/> (a click, or Enter / Space on <see cref="Cur"/>): what the scene does next.
        /// None while <see cref="Busy"/> or for a shut item. はじめから asks first over a save or an unreadable one.
        /// </summary>
        // js: scenes/meta/title.js:101
        public TitleChoice Choose(int i)
        {
            if (Busy || i < 0 || i >= Items.Count || !Items[i].Enabled) return TitleChoice.None;
            Cur = i;
            switch (Items[i].Key)
            {
                case KeyNew:
                    // js: scenes/meta/title.js:56 startNew
                    return !_hasSave && !_unreadable ? TitleChoice.LeaveNew : TitleChoice.AskNew;
                case KeyContinue:
                    return TitleChoice.LeaveContinue;
                default:
                    return TitleChoice.Settings;
            }
        }

        /// <summary>
        /// The title's keys by KeyboardEvent.key: ArrowUp / w, ArrowDown / s, Enter / Space, Escape. The scene's
        /// handler passes everything while busy or while the router's confirm card is open, and opens 設定 on a fresh
        /// Escape only (a held one repeats) when ctx.onSettings is given.
        /// </summary>
        // js: scenes/meta/title.js:151
        public static TitleKey KeyOf(string key)
        {
            switch (key)
            {
                case "ArrowUp":
                case "w":
                    return TitleKey.Up;
                case "ArrowDown":
                case "s":
                    return TitleKey.Down;
                case "Enter":
                case " ":
                    return TitleKey.Choose;
                case "Escape":
                    return TitleKey.Settings;
                default:
                    return TitleKey.None;
            }
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Key == key) return i;
            }
            return -1;
        }
    }
}
