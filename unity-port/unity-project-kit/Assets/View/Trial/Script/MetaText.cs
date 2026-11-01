// Scene text: the few words the meta screens print that no bridge module holds yet. Everything else
// (every name, number, rule line and sentence) comes from the ported bridge or core: Inheritance
// (InheritanceText, HallView, EndingView), DeckBuilder, DepartureBuilder, CardFaceText / CoreText,
// JournalBook / JournalTerms, BattleSpeed, ItemCatalogue. Fixed labels stay within 10 characters.
//
// Port of scenes/meta/text.js (the whole file, in its order). The JS arrow functions are methods here;
// the frozen arrays are read-only lists. Every string is byte-identical to the JS (full-width spaces and
// punctuation included); numbers print as a JS template prints an integer.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Trial.Script
{
    // js: scenes/meta/text.js:6
    public static class MetaText
    {
        // ---- title (concept-v3.md §1: 短縮形) ----
        public const string GameName = "Original Card Battle";

        public static IReadOnlyList<string> TaglineLines { get; } =
            new ReadOnlyCollection<string>(new[] { "死ぬたびに人は変わる。", "だが、技と記憶は次の生へ。" });

        public const string NewGame = "はじめから";
        public const string Continue = "つづきから";
        public const string Settings = "設定";
        public const string NoSave = "保存された生は無い";

        /// <summary>つづきから when a save is stored but could not be read (the router's notice under the menu says more).</summary>
        public const string Unreadable = "記録を読み込めない";

        public const string TitleKeys = "↑↓ 選ぶ　Enter 決める";

        // ---- 継承の間 ----
        public static IReadOnlyList<string> NameSeal { get; } = new ReadOnlyCollection<string>(new[] { "襲", "名" });

        public const string HallKeys = "←→ 痕跡を選ぶ　Enter 出立　Esc 設定";

        // the layer column's marks (review: three looks, no key): the hover line and the key under the column
        // js: scenes/meta/text.js:22
        public static string MarkTitle(string heirLine, string ending, string trace) => $"{heirLine}　{ending}・{trace}";

        public const string MarkUntaken = "未回収の痕跡";
        public const string MarkTaken = "回収済み";
        public const string MarkAlive = "生きて閉じた";
        public const string HallKeysNoTrace = "Enter 出立　Esc 設定";

        /// <summary>
        /// The key line under the hall: ←→ only with two or more traces, ↑↓ only while the trace's list runs past the
        /// panel.
        /// </summary>
        // js: scenes/meta/text.js:28
        public static string HallKeysFor(bool traces, bool scrolls) =>
            string.Join("　", new[] { traces ? "←→ 痕跡を選ぶ" : null, scrolls ? "↑↓ 候補を読む" : null, "Enter 出立", "Esc 設定" }
                .Where(s => !string.IsNullOrEmpty(s)));

        public const string JournalKey = "J";
        public const string BackToTitle = "タイトルへ";

        // A past life's trace once someone visited it: visiting collects it even with 「何も受け取らない」
        // (INHERITANCE.md R-10, 試作で決めたこと 5), and the lineage keeps who collected it, not what was picked.
        // js: scenes/meta/text.js:34
        public static string TraceCollected(int generation, string name) => $"痕跡：第 {N(generation)} 代 {name}が回収した";

        // ---- the ending card ----
        // A trace left behind an older one on the same layer (INHERITANCE.md 試作で決めたこと 4; the hall's
        // 「同じ層の古い痕跡を回収すると現れる」).
        // js: scenes/meta/text.js:39
        public static string TraceWaits(int layer, int generation, string name) =>
            $"第 {N(layer)} 層には第 {N(generation)} 代 {name}の古い痕跡がある。それが回収されると現れる";

        // js: scenes/meta/text.js:40
        public static string TraceWaitsMany(int layer, int count) =>
            $"第 {N(layer)} 層には古い痕跡が {N(count)} 件ある。それらが回収されると現れる";

        // ---- 出立 (Unity DepartureScreen.cs / DeckSelectScreen.cs literals) ----
        public const string StepDeck = "デッキを組む";
        public const string StepSupplies = "出立の支度";
        public const string ToSupplies = "出立の支度へ";
        public const string PrevPage = "前のページ";
        public const string NextPage = "次のページ";
        public const string Prototype = "試作デッキ";
        public const string Random = "ランダム";
        public const string Clear = "空にする";
        public const string PickCardHint = "カードを押すと、効果の文が出ます。";

        // js: scenes/meta/text.js:52
        public static string DeckCount(int n) => $"デッキ: {N(n)} 枚";

        public const string CardsUnit = "枚";
        public const string NotOwned = "未所持";
        public const string InDeck = "デッキの札";
        public const string SideExplore = "探索";
        public const string SideBattle = "戦闘";
        public const string EmptySlot = "空き";
        public const string DeckListHeading = "いまのデッキ";

        // Beside the heading while the deck list runs past its three rows (the deck step's arrows move the card cursor).
        public const string DeckListMore = "Shift+↑↓ 続きを読む";

        public const string StartHeading = "開始の位置";
        public const string DeckKeys = "←↑↓→ 札を選ぶ　＋ − 枚数　PgUp PgDn ページ";
        public const string SupplyKeys = "Tab 項目を移る　Space 押す";
        public const string EscBack = "Esc 戻る";
        public const string EscSettings = "Esc 設定";

        // ---- 所持カード (CAMP_FACILITIES_DESIGN.md §3.4) ----
        public const string OwnedTitle = "所持カード";

        // js: scenes/meta/text.js:70
        public static string OwnedCount(int n) => $"{N(n)} 種";

        public const string OwnedKeys = "←↑↓→ 選ぶ　PgUp PgDn ページ　C か Esc で閉じる";
        public const string OwnedNone = "持っている札は無い。";
        public const string Mastery = "習熟";

        // js: scenes/meta/text.js:74
        public static string RankLine(int rank) => $"{N(rank)} 段";

        public const string NextRank = "次の段まで";

        // js: scenes/meta/text.js:76
        public static string TicksLine(int ticks, int need) => $"刻み {N(ticks)} / {N(need)}";

        public const string TopRank = "最上の段";
        public const string NoGrowth = "習熟で名は変わらない";

        // js: scenes/meta/text.js:79
        public static string NextName(string name) => $"次の名：{name}";

        // concept-v3 §14 1 つの生の段数: 「3 段目を持てるのは 1 枚だけ。ほかは 2 段目まで」.
        // js: scenes/meta/text.js:81
        public static string TopTaken(string holder) =>
            $"3 段目を持てるのは 1 つの生で 1 枚だけ。この生では「{holder}」が持っているので、この札は 2 段で止まる。";

        // js: scenes/meta/text.js:82
        public static string TicksSoFar(int ticks) => $"刻み {N(ticks)}";

        // js: scenes/meta/text.js:83
        public static string BaseName(string name) => $"元の名：{name}";

        public const string Lasting = "永続能力";
        public const string NoLasting = "なし";
        public const string Close = "閉じる";

        // ---- 設定・セーブ (CAMP_FACILITIES_DESIGN.md §3.5) ----
        public const string SettingsTitle = "設定・セーブ";
        public const string Speed = "戦闘の速さ";
        public const string ShortFx = "演出短縮";
        public const string ShortFxOn = "入れる";
        public const string ShortFxOff = "切る";
        public const string ShortFxNote = "演出短縮を入れると、速さに関わらず演出が縮みます。";
        public const string Volume = "音の大きさ";
        public const string Master = "全体";
        public const string Effects = "効果音";
        public const string Ambience = "環境音";
        public const string Mute = "消音";
        public const string MuteOn = "消す";
        public const string MuteOff = "鳴らす";
        public const string SaveQuit = "保存して閉じる";
        public const string SaveNote = "今の状態を保存してゲームを閉じます。生は終わりません。";
        public const string SaveToTitle = "保存してタイトルへ";

        /// <summary>The button by where it is pressed: a life (the map, a fight) or 継承の間 / 出立 ("lineage").</summary>
        // js: scenes/meta/text.js:106
        public static string SaveQuitOf(string kind) => kind == "lineage" ? "保存してタイトルへ" : "保存して閉じる";

        // js: scenes/meta/text.js:107
        public static string SaveNoteOf(string kind) =>
            kind == "battle" ? "戦闘の途中を保存して閉じます。つづきからで、この場面に戻ります。"
            : kind == "lineage" ? "ここまでの記録は保存されています。つづきからで、ここへ戻ります。"
            : "今の状態を保存してゲームを閉じます。生は終わりません。";

        public const string SettingsKeys = "↑↓ 項目　←→ 変える　Esc 閉じる";

        // ---- 手記 (dungeon_exploration_v4.md §6.7) ----
        public const string EnemyPagePending = "頁の中身は、まだ書かれていない。";

        /// <summary>The 用語 page's stance row (X-17): the attribute 「スタンス」 said apart from the rule 「構え」.</summary>
        public const string StanceTerm = "スタンス";

        public const string StanceTermText = "置くと戦闘のあいだ効き続ける札の属性（水の構え・岩の構え など）。上の「構え」の規則とは別";
        public const string JournalKeys = "←→ 頁　J か Esc で閉じる";
        public const string MemoMine = "自分のメモ";
        public const string MemoInherited = "受け継いだ走り書き";

        // the ダンジョン tab's 希少な頁 (INHERITANCE.md R-27): handed down or found in this life
        public const string RarePagesTitle = "希少な頁";

        // js: scenes/meta/text.js:122
        public static string LayerShort(int n) => $"第 {N(n)} 層";

        public const string RarePagesInherited = "受け継いだ頁";
        public const string RarePagesFound = "この生で拾った頁";

        // js: scenes/meta/text.js:125
        public static string RarePagesCount(int n) => $"{N(n)} 枚";

        public const string RarePagesPending = "頁の中身は、まだ書かれていない。数に入るだけで、戦いにも探索にも効き目は無い。";

        /// <summary>Where the pages go when the life ends (INHERITANCE.md R-13, 試作で決めたこと 9).</summary>
        public const string RarePagesFate = "生きてこの生を閉じれば、手記と一緒に次の生へ渡る。中身の無い頁は痕跡の遺産の候補にならない。";

        // An integer as a JS template prints it (no culture: the minus is "-", no grouping).
        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
