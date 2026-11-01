// Scene text (port of src/scenes/explore/text.js): the few Japanese lines the exploration screen prints that the ported
// bridge does not write (ExplorationText / ExplorationScreenBuilder / Expedition / Inheritance cover everything else).
// Fixed labels stay within 10 characters. Numbers are passed in, never worked out here. Owned by EX-LEAF.
// Every string is byte-identical to text.js (checked by evaluating both on the same arguments). A JS argument that is
// falsy (`was`, `need`, `extra`) is null / "" / 0 here.
using System.Collections.Generic;
using System.Globalization;
using Exploration;

namespace Trial.Explore
{
    // js: scenes/explore/text.js:5
    public static class ExploreText
    {
        /// <summary>dungeon_exploration_v4 §6.3: the five standings by the names the spec gives them.</summary>
        // js: scenes/explore/text.js:7
        public static readonly IReadOnlyDictionary<NodeStanding, string> Standing = new Dictionary<NodeStanding, string>
        {
            [NodeStanding.Current] = "今いる",
            [NodeStanding.Reachable] = "進める",
            [NodeStanding.Revisit] = "戻れる",
            [NodeStanding.Resolved] = "踏んだ",
            [NodeStanding.Far] = "遠い",
        };

        // after a battle (Expedition.Return)
        public const string BattleWon = "戦いに勝った";
        public const string BattleLost = "戦いに敗れた";
        public const string Learned = "覚えた札";
        public const string RankUps = "習熟が上がった札";

        /// <summary>A 段 gained; a card the 段 renamed names both (「突き → 鋭い突き（0 → 1 段目）」, X-15).</summary>
        // js: scenes/explore/text.js:15
        public static string RankUp(string name, int from, int to, string was = null) =>
            !string.IsNullOrEmpty(was) && was != name ? RankUpNames(name, was) + "（" + RankUpSteps(from, to) + "）" : name + "　" + RankUpSteps(from, to);

        /// <summary>The rank-up line's two parts, for a narrow column that breaks between them (the result card).</summary>
        // js: scenes/explore/text.js:17
        public static string RankUpNames(string name, string was = null) => !string.IsNullOrEmpty(was) && was != name ? was + " → " + name : name;

        // js: scenes/explore/text.js:18
        public static string RankUpSteps(int from, int to) => N(from) + " → " + N(to) + " 段目";

        public const string Journal = "手記に書き足した敵";

        // js: scenes/explore/text.js:20
        public static string JournalLine(string name, int level, int max) => name + "　開示度 " + N(level) + " / " + N(max);

        public const string Found = "拾った物";

        // js: scenes/explore/text.js:22
        public static string Pages(int n) => "希少な頁 " + N(n) + " 枚";

        /// <summary>Under a reward's or an event's 希少な頁 and on the map's page chips: the pages have no content yet (#107).</summary>
        public const string PagesNote = "頁の中身はまだ書かれていない。手記に数が残るだけ";

        /// <summary>The pages handed down (a survivor's journal, a trace): the life's, but not found here (INHERITANCE.md R-27).</summary>
        public const string Inherited = "受け継いだ物";
        public const string InheritedNote = "前の生から受け継いだ頁。手記に入っている";
        public const string PickOne = "1 枚を選んで覚える";
        public const string LearnButton = "この札を覚える";
        public const string SkipButton = "受け取らない";
        public const string BackToMap = "地図へ戻る";
        public const string NothingFound = "拾った物はない";

        /// <summary>encounters_and_rewards_v4 §4.3: a card the reward wanted came as a consumable.</summary>
        public const string PileSpentBattle = "札の山が尽きていたので、札の代わりに消耗品を拾った";

        /// <summary>§4.3 「デッキの上限の中で」: a learned card that did not fit the deck is owned only.</summary>
        // js: scenes/explore/text.js:36
        public static string Benched(string names, int max) => "デッキが上限（" + N(max) + " 枚）のため、" + names + " はデッキに入らず所持カードに入った";

        // js: scenes/explore/text.js:37
        public static string OfferBenched(int max) => "デッキが上限（" + N(max) + " 枚）なので、覚えた札は所持カードにだけ入る";

        // the trace (Inheritance through ctx.inheritance)
        public const string TraceLeaveTitle = "何も受け取らずに去りますか";

        // js: scenes/explore/text.js:41
        public static string TraceLeaveBody(int picked) =>
            "この痕跡は回収され、ここに残った遺産は失われます。" + (picked > 0 ? "選んだ " + N(picked) + " 件も受け取りません。" : "");

        public const string TraceLeaveCancel = "選び直す";
        public const string TakenTitle = "受け取った遺産";

        // js: scenes/explore/text.js:44
        public static string TraceLost(int n) => "選ばなかった " + N(n) + " 件は失われた";

        // the interlude's look ahead
        public const string NextLayer = "次の層";

        // the encounter banner before a fight
        // js: scenes/explore/text.js:50
        public static string FightCount(int n) => N(n) + " 連戦";

        // 習熟訓練 (Expedition.Train)
        public const string TrainTitle = "訓練する札を選ぶ";
        public const string TrainLead = "選んだ札に、習熟の刻みが 1 入る。「あとで」にした刻みは、この層を出るまで使える。";
        public const string TrainButton = "訓練する";
        public const string TrainLater = "あとで";

        /// <summary>次の層へ降りる with marks left: the marks are this layer's (the next layer starts with none).</summary>
        public const string TrainBeforeDescend = "降りる前に訓練する";
        public const string TrainLeadDescend = "訓練の刻みは、次の層へ降りると失われる。降りる前に、刻みを入れる札を選べる。";
        public const string TrainAndDescend = "訓練して降りる";
        public const string TrainDiscard = "刻みを捨てて降りる";
        public const string TrainStay = "やめる";

        // js: scenes/explore/text.js:63
        public static string Rank(int n) => N(n) + " 段目";

        /// <summary>The training card's rows and preview (X-15): the 刻み toward the next 段, from MasteryView.MasteryProgress.</summary>
        // js: scenes/explore/text.js:65
        public static string TrainTicks(int ticks, int need) => "刻み " + N(ticks) + " / " + N(need);

        public const string TrainTicksTop = "最上の段";
        public const string TrainTicksCapped = "2 段で止まる";
        public const string TrainTicksFlat = "段は上がらない";

        // js: scenes/explore/text.js:69
        public static string TrainBaseName(string name) => "元の名：" + name;

        // js: scenes/explore/text.js:70
        public static string TrainNext(int ticks, int need, int rank) =>
            "訓練すると 刻み " + N(ticks) + " / " + N(need) + (ticks >= need ? "・" + N(rank) + " 段目へ" : "");

        // js: scenes/explore/text.js:71
        public static string TrainNextName(string name) => "次の名：" + name;

        // js: scenes/explore/text.js:72
        public static string TrainCappedNote(string holder) => "3 段目は「" + holder + "」が持っているので、この札は 2 段で止まる";

        // what the training did, said on the map once the card closes (X-15)
        public const string TrainedKicker = "訓練した";

        /// <summary>need 0 (JS falsy) leaves the 刻み out.</summary>
        // js: scenes/explore/text.js:75
        public static string Trained(string name, int ticks, int need) =>
            need != 0 ? name + "に習熟の刻み 1　刻み " + N(ticks) + " / " + N(need) : name + "に習熟の刻み 1";

        // the inventory
        public const string Use = "使う";
        public const string FoundNote = "拾った消耗品は、まだ枠に入れられない";
        public const string FoundNoteTrial = "拾った頁は手記に入る";

        // fallback overlay pages (only when the app mounts none)
        public const string Close = "閉じる";
        public const string SaveQuit = "保存して閉じる";

        // trial mode (docs/EXTENSIONS.md 「探索の持ち物とイベント」): presentation labels only; the rules' words are ExplorationText's
        public const string StopAim = "やめる";
        public const string Swap = "入れ替える";
        public const string FoundItem = "拾った物";
        public const string Carried = "持っている物";
        public const string EventKicker = "イベント";
        public const string SurveyKicker = "情報収集";

        // js: scenes/explore/text.js:94
        public static string ToolNote(string name, string side, string note) => name + "（" + side + "）: " + note;

        /// <summary>extra "" (JS falsy) gives the note alone.</summary>
        // js: scenes/explore/text.js:95
        public static string SlotHint(string note, string extra) => !string.IsNullOrEmpty(extra) ? note + "　→ " + extra : note;

        // the key legend
        public const string KeyEnter = "Enter";
        public const string KeyShift = "Shift";
        public const string KeySelect = "選ぶ";
        public const string KeyToggle = "付ける・外す";
        public const string KeyScroll = "一覧を見る";
        public const string KeyItems = "消耗品";

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}
