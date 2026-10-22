// The app's own words (src/app/main.js): the notices about storage and the save, and the three confirm cards
// the router asks (新しい系譜を始める, 生を再開できません, 戦闘を続けられません). Byte-identical to the JS.
// The router (Trial.Boot) shows them; where and for how long is written next to each, as main.js places them.
//
// Engine-free (no UnityEngine).
using System;
using System.Collections.Generic;

namespace Trial.Bridge
{
    /// <summary>What one confirm card says (stage.js confirmCard's opts: title, body, ok, cancel, danger).</summary>
    public sealed class ConfirmText
    {
        public ConfirmText(string title, string body, string ok, string cancel, bool danger)
        {
            Title = title;
            Body = body;
            Ok = ok;
            Cancel = cancel;
            Danger = danger;
        }

        public string Title { get; }
        public string Body { get; }
        public string Ok { get; }
        public string Cancel { get; }

        /// <summary>The OK button is drawn as the dangerous one (border --hp-text).</summary>
        public bool Danger { get; }
    }

    public static class AppText
    {
        // js: app/main.js:46
        /// <summary>Storage is blocked: on the title (one line each) and over the settings panel (joined).</summary>
        public static readonly IReadOnlyList<string> NoStorageLines = new[]
        {
            "この環境では保存できません。",
            "ページを閉じると、ここまでの記録は失われます。",
        };

        // js: app/main.js:47
        /// <summary>A write failed after storage had worked: once, over whatever is on screen.</summary>
        public const string SaveFailedText = "保存できませんでした。ページを閉じると、ここまでの記録は失われます。";

        // js: app/main.js:48
        /// <summary>The stored save did not load (the title's notice). <paramref name="kept"/>: the copy aside was written.</summary>
        public static IReadOnlyList<string> UnreadableLines(bool kept) => new[]
        {
            "保存された記録を読み込めませんでした。",
            kept ? "記録は消さずに控えとして残しています。" : "控えを残せないため、はじめからを選ぶと失われます。",
        };

        // js: app/main.js:50
        /// <summary>Why something failed, as the confirm cards end on it: 「（message）」, the message cut at 80 UTF-16 units.</summary>
        public static string ReasonOf(Exception e) => "（" + MessageOf(e) + "）";

        // js: app/main.js:51
        /// <summary>Another instance moved the save on: this one reloaded it and went back to the title.</summary>
        public const string StaleText = "別のタブでこの記録が先に進みました。最新の記録を読み込み直しました。";

        // ---- where the router puts the notices (main.js; stage px, noticeLine(text, x, y, width, ms)) ----

        /// <summary>SAVE_FAILED_TEXT and STALE_TEXT: under the top bars (main.js:140, :185).</summary>
        public const int StageNoticeX = 560;
        public const int StageNoticeY = 104;
        public const int StageNoticeWidth = 800;

        /// <summary>How long SAVE_FAILED_TEXT stays (ms, main.js:140).</summary>
        public const int SaveFailedMs = 8000;

        /// <summary>How long STALE_TEXT stays (ms, main.js:185).</summary>
        public const int StaleMs = 10000;

        /// <summary>The title's notice (main.js:251): x 152, y 840 − max(0, lines − 2)·25, width 560, z 40, no timeout.</summary>
        public const int TitleNoticeX = 152;
        public const int TitleNoticeWidth = 560;

        // js: app/main.js:251
        public static int TitleNoticeY(int lineCount) => 840 - Math.Max(0, lineCount - 2) * 25;

        /// <summary>The lines the title's notice shows (main.js:246): the unreadable save's, then the blocked storage's.</summary>
        // js: app/main.js:246
        public static IReadOnlyList<string> TitleNoticeLines(bool unreadable, bool unreadableKept, bool storageOk)
        {
            var lines = new List<string>();
            if (unreadable) lines.AddRange(UnreadableLines(unreadableKept));
            if (!storageOk) lines.AddRange(NoStorageLines);
            return lines;
        }

        /// <summary>The blocked-storage line over the settings panel (main.js:91-92): x 540, y max(16, panel top − 76), width 840.</summary>
        public const int SettingsNoticeX = 540;
        public const int SettingsNoticeWidth = 840;

        // js: app/main.js:91
        public static int SettingsNoticeY(int panelTop) => Math.Max(16, panelTop - 76);

        /// <summary>The same line's y when the settings panel is not found (main.js:91: `panel ? … : 40`).</summary>
        public const int SettingsNoticeFallbackY = 40;

        // ---- the confirm cards ----

        // js: app/main.js:227
        /// <summary>はじめから over a lineage with lives or a life in progress.</summary>
        public static readonly ConfirmText NewLineage = new ConfirmText(
            "新しい系譜を始める",
            "これまでの生と痕跡の記録は消えます。",
            "はじめから", "やめる", danger: true);

        // js: app/main.js:235
        /// <summary>はじめから over a stored save that did not load.</summary>
        public static ConfirmText NewOverUnreadable(bool kept, Exception error) => new ConfirmText(
            "新しい系譜を始める",
            "読み込めなかった記録に、新しい系譜を上書きします。" +
                (kept ? "控えは残ります。" : "控えを残せなかったため、記録は失われます。") + ReasonOf(error),
            "はじめから", "やめる", danger: true);

        // js: app/main.js:345
        /// <summary>The saved life does not replay. OK (あきらめる) drops the life; cancel goes to the title.</summary>
        public static ConfirmText CannotResume(bool kept, Exception error) => new ConfirmText(
            "生を再開できません",
            "保存された生を読み込めませんでした。" + (kept ? "記録は消さずに控えとして残しています。" : "") +
                "あきらめると、この生は無かったことになり、出立の前から出直します。（" + MessageOf(error) + "）",
            "あきらめる", "タイトルへ", danger: true);

        // js: app/main.js:478
        /// <summary>The battle scene could not go on. OK (やり直す) loads the life again; cancel goes to the title.</summary>
        public static ConfirmText BattleFailed(Exception error) => new ConfirmText(
            "戦闘を続けられません",
            "この戦闘をはじめからやり直します。（" + MessageOf(error) + "）",
            "やり直す", "タイトルへ", danger: false);

        /// <summary>
        /// JS String((e &amp;&amp; e.message) || e).slice(0, 80): the message, or (none) the exception's type name, or
        /// "null"; cut at 80 UTF-16 units.
        /// </summary>
        public static string MessageOf(Exception e)
        {
            string text = e == null ? "null" : string.IsNullOrEmpty(e.Message) ? e.GetType().Name : e.Message;
            return text.Length <= 80 ? text : text.Substring(0, 80);
        }
    }
}
