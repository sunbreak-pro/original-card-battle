#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DungeonCore;
using static System.FormattableString;

namespace InheritanceCore
{
    /// <summary>
    /// Every sentence the hall, the 痕跡 card and the ending card print (Japanese UI text, data not logic).
    /// The JS's arrow functions are methods here; its lookup objects (KindLabel, EndingTitle, NodeKindLabel)
    /// are methods taking the enum.
    /// </summary>
    // js: core/inheritance/Inheritance.js:99
    public static class InheritanceText
    {
        public const string HallTitle = "継承の間";
        public const string HallLead = "前の生から次の生へ渡すものを見る部屋。";
        public const string HallEmpty = "最初の踏破者。継ぐものはまだ無い。";
        public const string RoleName = "踏破者";
        public const string StartButton = "出立の支度へ";
        public const string ToHallButton = "継承の間へ";
        public const string TraceHeading = "未回収の痕跡";
        public const string NoTraces = "未回収の痕跡は無い。";
        public const string PastLivesHeading = "これまでの踏破者";
        public const string SurvivorHeading = "前回の生存者";
        public const string NextHeirHeading = "次の踏破者";
        public const string AttireHeading = "装束";

        public static IReadOnlyList<string> TraceRules { get; } = new ReadOnlyCollection<string>(new[]
        {
            "遺産の受け取りは、死亡地点を訪れたときに行う。ここでは候補を見るだけ。",
            "1 つの生で受け取れる痕跡は 1 件（最初に訪れた痕跡）。",
            "痕跡は回収されるまで残る。",
            "習熟 2 段目以上の札は、第 3 層より浅い死では遺産に残らない。",
        });

        public const string TraceNodeTitle = "死亡地点の痕跡";
        public const string TakeButton = "受け取る";
        public const string LeaveButton = "何も受け取らない";
        public const string AlreadyTaken = "この生ではもう遺産を受け取った。この痕跡は残り、後の生で受け取れる。";
        public const string Appears = "この層の通常戦闘 1 つと入れ替わって現れる";
        public const string Waits = "同じ層の古い痕跡を回収すると現れる";
        public const string ClearedTraceNote = "歪みの根を倒した生の痕跡。札だけを残し、手記は生存者として渡った";
        public const string WithheldHeading = "浅い層の死では残らなかった札";

        public static string KindLabel(CandidateKind kind) => kind switch
        {
            CandidateKind.Card => "技術",
            CandidateKind.EnemyPage => "敵の頁",
            CandidateKind.DungeonPage => "ダンジョンの頁",
            CandidateKind.Scribble => "走り書き",
            _ => throw new InheritanceRuleException($"unknown candidate kind {kind}"),
        };

        public static string EndingTitle(LifeEnding ending) => ending switch
        {
            LifeEnding.MiasmaDeath => "瘴気死",
            LifeEnding.Fallen => "倒れた",
            LifeEnding.Completed => "踏破",
            LifeEnding.Survived => "生存ルート",
            _ => throw new InheritanceRuleException($"unknown ending {ending}"),
        };

        /// <summary>The node kinds by name, as ExplorationText.KindLabel prints them.</summary>
        public static string NodeKindLabel(NodeKind kind) => kind switch
        {
            NodeKind.Battle => "通常戦闘",
            NodeKind.Elite => "精鋭",
            NodeKind.Boss => "階層ボス",
            NodeKind.Rest => "休息",
            NodeKind.Survey => "情報収集",
            NodeKind.Event => "イベント",
            NodeKind.Trace => "死亡地点の痕跡",
            NodeKind.Carving => "秘跡の彫り",
            _ => throw new InheritanceRuleException($"unknown node kind {kind}"),
        };

        public static string GenerationLabel(int generation) => Invariant($"第 {generation} 代");

        public static string HeirTitle(string name) => $"踏破者 {name}";

        public static string HeirLine(int generation, string? name) => Invariant($"第 {generation} 代 踏破者 {name}");

        /// <summary>Same format as ExplorationText.LayerTitle (full-width space).</summary>
        public static string LayerTitle(int layer, string name) => Invariant($"第 {layer} 層　{name}");

        public static string RankDetail(int rank) => Invariant($"{rank} 段目");

        public static string DisclosureDetail(int level) => Invariant($"開示度 {level} / {InheritanceRules.MaxDisclosure}");

        public const string DungeonPageDetail = "手記の頁";

        public static string ScribbleName(int generation, string? name) => Invariant($"走り書き（第 {generation} 代 {name}）");

        public static string RarePageTitle(int index) => Invariant($"希少な頁 {index}");

        public static string WithheldReason(int rank) => Invariant($"{rank} 段目。第 {InheritanceRules.DeepLayer} 層より浅い死では残らない");

        public static string PickLead(int max) => Invariant($"遺産を {max} つまで選んで受け取る。選ばなかったものはここで失われる。");

        public static string PickCount(int picked, int max) => Invariant($"{picked} / {max}");

        public static string TraceOf(int generation, string name) => Invariant($"第 {generation} 代 踏破者 {name}の痕跡");

        public static string CandidateCount(int cards, int pages, int scribbles) =>
            Invariant($"遺産の候補：技術 {cards} ／ 頁 {pages} ／ 走り書き {scribbles}");

        public const string TierFirst = "最初の踏破者。支給品は縁なし";

        public static string TierReason(int generation, string name, int? layer) =>
            Invariant($"前の生（第 {generation} 代 {name}）が第 {JsNullable(layer)} 層まで届いたため");

        public static string TierCleared(int generation, string name) => Invariant($"前の生（第 {generation} 代 {name}）が歪みの根を倒したため");

        /// <summary>The four end sentences, the same words as ExplorationScreenBuilder's end cards.</summary>
        public static string EndingSentence(LifeEnding ending, int? layer)
        {
            string l = JsNullable(layer);
            switch (ending)
            {
                case LifeEnding.MiasmaDeath: return $"瘴気が 100% に達し、第 {l} 層でこの生が終わった。";
                case LifeEnding.Fallen: return $"第 {l} 層の戦いで倒れた。";
                case LifeEnding.Completed: return "歪みの根を倒した。";
                case LifeEnding.Survived: return $"生きたまま、第 {l} 層でこの生を閉じた。";
                default: throw new InheritanceRuleException($"unknown ending {ending}");
            }
        }

        public static string TraceLeft(string layerTitle) => $"{layerTitle}に痕跡と手記が残った";

        public static string TraceVisit(int layer, int max) => Invariant($"次の踏破者が第 {layer} 層を訪れると、{max} つまで選んで受け取れる");

        public static string WithheldLine(IEnumerable<string> names) => $"浅い層の死では残らない札：{string.Join("、", names)}";

        public const string NothingLeft = "遺産になるものは残らなかった";
        public const string JournalPasses = "手記は丸ごと次の生へ渡る";

        public static string ClearedTraceLeft(string layerTitle) => $"{layerTitle}に札の痕跡が残った（歪みの根の札を含む）";

        public static string SurvivorBonusLine(int lives) =>
            $"生存者ボーナス：ツール枠 +1 ／ デッキ上限 +5（{(lives == 1 ? "次の 1 生" : Invariant($"次の {lives} 生"))}）";

        public static string SurvivorReplaced(int generation, string name) => Invariant($"生存者は一人まで。第 {generation} 代 {name}の分は退いた");

        public static string NextHeirLine(int generation, string name) => Invariant($"次の踏破者：第 {generation} 代 {name}");

        public const string SurvivorJournal = "手記を丸ごと受け継ぐ";

        public static string SurvivorTools(int from, int to) => Invariant($"ツール枠 +1（{from} → {to}）");

        public static string SurvivorDeck(int from, int to) => Invariant($"デッキ上限 +{to - from}（{from} → {to}）");

        public static string SurvivorDuration(int lives) => lives == 1 ? "次の 1 生だけ" : Invariant($"あと {lives} 生");

        /// <summary>The hall after the life that used the bonus closed without a new survivor (R-14: 次の 1 生だけ).</summary>
        public static string SurvivorSpent(int generation, string name) => Invariant($"第 {generation} 代 {name}の生でボーナスを使い終えた");

        public static string SurvivorSpentLimits(int tools, int deck) => Invariant($"ツール枠 {tools} ／ デッキ上限 {deck} に戻った");

        /// <summary>The past life's trace line in the hall.</summary>
        public static class TraceStatus
        {
            public const string None = "痕跡なし（生きて閉じた）";
            public const string Untaken = "痕跡：未回収";

            public static string Taken(int generation, string name) => Invariant($"痕跡：第 {generation} 代 {name}が受け取った");
        }

        // A JS template prints a null number as "null".
        private static string JsNullable(int? value) =>
            value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
    }
}
