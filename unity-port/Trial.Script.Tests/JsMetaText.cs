// Generated from the JS (scenes/meta/text.js, bridge/backdrop/BackdropSpec.js) by node: what the JS gives.
// MetaTextTests and HallBackdropSpecTests compare the C# with it. Regenerate it from the trial's sources when
// text.js changes (import MetaText, print each string / array member, the function names, the sample calls'
// results, and Math.round(At * 100) of BackdropSpec.For(1..7).Sky); do not edit it by hand.
using System.Collections.Generic;

namespace Trial.Script.Tests
{
    internal static class JsMetaText
    {
        /// <summary>Every string member of the JS MetaText, by name.</summary>
        public static readonly Dictionary<string, string> Strings = new Dictionary<string, string>
        {
            ["GameName"] = "Original Card Battle",
            ["NewGame"] = "はじめから",
            ["Continue"] = "つづきから",
            ["Settings"] = "設定",
            ["NoSave"] = "保存された生は無い",
            ["Unreadable"] = "記録を読み込めない",
            ["TitleKeys"] = "↑↓ 選ぶ　Enter 決める",
            ["HallKeys"] = "←→ 痕跡を選ぶ　Enter 出立　Esc 設定",
            ["MarkUntaken"] = "未回収の痕跡",
            ["MarkTaken"] = "回収済み",
            ["MarkAlive"] = "生きて閉じた",
            ["HallKeysNoTrace"] = "Enter 出立　Esc 設定",
            ["JournalKey"] = "J",
            ["BackToTitle"] = "タイトルへ",
            ["StepDeck"] = "デッキを組む",
            ["StepSupplies"] = "出立の支度",
            ["ToSupplies"] = "出立の支度へ",
            ["PrevPage"] = "前のページ",
            ["NextPage"] = "次のページ",
            ["Prototype"] = "試作デッキ",
            ["Random"] = "ランダム",
            ["Clear"] = "空にする",
            ["PickCardHint"] = "カードを押すと、効果の文が出ます。",
            ["CardsUnit"] = "枚",
            ["NotOwned"] = "未所持",
            ["InDeck"] = "デッキの札",
            ["SideExplore"] = "探索",
            ["SideBattle"] = "戦闘",
            ["EmptySlot"] = "空き",
            ["DeckListHeading"] = "いまのデッキ",
            ["DeckListMore"] = "Shift+↑↓ 続きを読む",
            ["StartHeading"] = "開始の位置",
            ["DeckKeys"] = "←↑↓→ 札を選ぶ　＋ − 枚数　PgUp PgDn ページ",
            ["SupplyKeys"] = "Tab 項目を移る　Space 押す",
            ["EscBack"] = "Esc 戻る",
            ["EscSettings"] = "Esc 設定",
            ["OwnedTitle"] = "所持カード",
            ["OwnedKeys"] = "←↑↓→ 選ぶ　PgUp PgDn ページ　C か Esc で閉じる",
            ["OwnedNone"] = "持っている札は無い。",
            ["Mastery"] = "習熟",
            ["NextRank"] = "次の段まで",
            ["TopRank"] = "最上の段",
            ["NoGrowth"] = "習熟で名は変わらない",
            ["Lasting"] = "永続能力",
            ["NoLasting"] = "なし",
            ["Close"] = "閉じる",
            ["SettingsTitle"] = "設定・セーブ",
            ["Speed"] = "戦闘の速さ",
            ["ShortFx"] = "演出短縮",
            ["ShortFxOn"] = "入れる",
            ["ShortFxOff"] = "切る",
            ["ShortFxNote"] = "演出短縮を入れると、速さに関わらず演出が縮みます。",
            ["Volume"] = "音の大きさ",
            ["Master"] = "全体",
            ["Effects"] = "効果音",
            ["Ambience"] = "環境音",
            ["Mute"] = "消音",
            ["MuteOn"] = "消す",
            ["MuteOff"] = "鳴らす",
            ["SaveQuit"] = "保存して閉じる",
            ["SaveNote"] = "今の状態を保存してゲームを閉じます。生は終わりません。",
            ["SaveToTitle"] = "保存してタイトルへ",
            ["SettingsKeys"] = "↑↓ 項目　←→ 変える　Esc 閉じる",
            ["EnemyPagePending"] = "頁の中身は、まだ書かれていない。",
            ["StanceTerm"] = "スタンス",
            ["StanceTermText"] = "置くと戦闘のあいだ効き続ける札の属性（水の構え・岩の構え など）。上の「構え」の規則とは別",
            ["JournalKeys"] = "←→ 頁　J か Esc で閉じる",
            ["MemoMine"] = "自分のメモ",
            ["MemoInherited"] = "受け継いだ走り書き",
            ["RarePagesTitle"] = "希少な頁",
            ["RarePagesInherited"] = "受け継いだ頁",
            ["RarePagesFound"] = "この生で拾った頁",
            ["RarePagesPending"] = "頁の中身は、まだ書かれていない。数に入るだけで、戦いにも探索にも効き目は無い。",
            ["RarePagesFate"] = "生きてこの生を閉じれば、手記と一緒に次の生へ渡る。中身の無い頁は痕跡の遺産の候補にならない。",
        };

        /// <summary>Every frozen array member of the JS MetaText, by name.</summary>
        public static readonly Dictionary<string, string[]> Lists = new Dictionary<string, string[]>
        {
            ["TaglineLines"] = new[] { "死ぬたびに人は変わる。", "だが、技と記憶は次の生へ。" },
            ["NameSeal"] = new[] { "襲", "名" },
        };

        /// <summary>Every function member of the JS MetaText, by name.</summary>
        public static readonly string[] Functions = { "MarkTitle", "HallKeysFor", "TraceCollected", "TraceWaits", "TraceWaitsMany", "DeckCount", "OwnedCount", "RankLine", "TicksLine", "NextName", "TopTaken", "TicksSoFar", "BaseName", "SaveQuitOf", "SaveNoteOf", "LayerShort", "RarePagesCount" };

        /// <summary>Sample calls: (function, its arguments, what the JS returns).</summary>
        public static readonly (string Name, object[] Args, string Result)[] Calls =
        {
            ("MarkTitle", new object[] { "第 1 代 踏破者 灯", "倒れた", "痕跡：未回収" }, "第 1 代 踏破者 灯　倒れた・痕跡：未回収"),
            ("HallKeysFor", new object[] { false, false }, "Enter 出立　Esc 設定"),
            ("HallKeysFor", new object[] { true, false }, "←→ 痕跡を選ぶ　Enter 出立　Esc 設定"),
            ("HallKeysFor", new object[] { false, true }, "↑↓ 候補を読む　Enter 出立　Esc 設定"),
            ("HallKeysFor", new object[] { true, true }, "←→ 痕跡を選ぶ　↑↓ 候補を読む　Enter 出立　Esc 設定"),
            ("TraceCollected", new object[] { 2, "灯" }, "痕跡：第 2 代 灯が回収した"),
            ("TraceWaits", new object[] { 7, 1, "灯" }, "第 7 層には第 1 代 灯の古い痕跡がある。それが回収されると現れる"),
            ("TraceWaitsMany", new object[] { 7, 2 }, "第 7 層には古い痕跡が 2 件ある。それらが回収されると現れる"),
            ("DeckCount", new object[] { 40 }, "デッキ: 40 枚"),
            ("OwnedCount", new object[] { 80 }, "80 種"),
            ("RankLine", new object[] { 2 }, "2 段"),
            ("TicksLine", new object[] { 3, 10 }, "刻み 3 / 10"),
            ("NextName", new object[] { "突き" }, "次の名：突き"),
            ("TopTaken", new object[] { "突き" }, "3 段目を持てるのは 1 つの生で 1 枚だけ。この生では「突き」が持っているので、この札は 2 段で止まる。"),
            ("TicksSoFar", new object[] { 5 }, "刻み 5"),
            ("BaseName", new object[] { "突き" }, "元の名：突き"),
            ("SaveQuitOf", new object[] { "lineage" }, "保存してタイトルへ"),
            ("SaveQuitOf", new object[] { "battle" }, "保存して閉じる"),
            ("SaveQuitOf", new object[] { "life" }, "保存して閉じる"),
            ("SaveNoteOf", new object[] { "lineage" }, "ここまでの記録は保存されています。つづきからで、ここへ戻ります。"),
            ("SaveNoteOf", new object[] { "battle" }, "戦闘の途中を保存して閉じます。つづきからで、この場面に戻ります。"),
            ("SaveNoteOf", new object[] { "life" }, "今の状態を保存してゲームを閉じます。生は終わりません。"),
            ("LayerShort", new object[] { 3 }, "第 3 層"),
            ("RarePagesCount", new object[] { 4 }, "4 枚"),
        };

        /// <summary>hall.js skyCss: Math.round(At × 100) of each sky stop, by layer.</summary>
        public static readonly Dictionary<int, int[]> StratumPercents = new Dictionary<int, int[]>
        {
            [1] = new[] { 0, 40, 100 },
            [2] = new[] { 0, 50, 100 },
            [3] = new[] { 0, 100 },
            [4] = new[] { 0, 50, 100 },
            [5] = new[] { 0, 100 },
            [6] = new[] { 0, 100 },
            [7] = new[] { 0, 100 },
        };
    }
}
