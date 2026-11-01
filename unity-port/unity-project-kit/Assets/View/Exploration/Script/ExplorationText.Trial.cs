// Trial extension of ExplorationText (port of the browser trial's src/bridge/explorationScript/ExplorationText.js:92
// and :106-312; docs/EXTENSIONS.md 「探索の持ち物とイベント」 and 「探索の結果と再開」). Same style as the C# half: the
// numbers are passed in. Sources: encounters_and_rewards_v4.md §4〜§5, tools_and_consumables_v4.md §2〜§3,
// dungeon_exploration_v4.md §6. Every string is byte-identical to the JS.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DungeonCore;

namespace Exploration
{
    public static partial class ExplorationText
    {
        // A sentence carrying a design-status remark (「。防御は Guard だけ（アーマーは凍結）」).
        private static readonly Regex DesignStatus = new Regex(@"。?[^。（）]*（[^（）]*凍結[^（）]*）");

        /// <summary>
        /// The note a player reads (the slots, the tool chips, the swap card) — ItemNote without the design-status
        /// remarks it carries for the team too (「防御は Guard だけ（アーマーは凍結）」: what the design has frozen),
        /// the same cut 出立 makes: the sentence goes with its remark.
        /// </summary>
        // js: bridge/explorationScript/ExplorationText.js:97
        public static string PlayerNote(string id) => DesignStatus.Replace(ItemNote(id), "").Trim();

        /// <summary>encounters_and_rewards_v4.md §5: the three events by name.</summary>
        // js: bridge/explorationScript/ExplorationText.js:111
        public static string EventName(EventKind kind) => kind switch
        {
            EventKind.Spring => "泉",
            EventKind.HeavyChest => "重い宝箱",
            EventKind.StrangeMeal => "特殊な食事",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        /// <summary>
        /// 「泉: HP +10（最大の 20%）」. <paramref name="room"/> (the HP the maximum still lets in, or null): a heal the
        /// maximum would stop prints what it gives, marked 「上限まで」 — the rest's and the interlude's reading.
        /// </summary>
        // js: bridge/explorationScript/ExplorationText.js:124
        public static string SpringLine(int heal, int percent, int? room = null)
        {
            int gain = room == null ? heal : Math.Min(heal, room.Value);
            return $"泉: HP +{gain}（最大の {percent}%{(gain < heal ? CappedMark : "")}）";
        }

        /// <summary>A share of the HP maximum that the maximum stopped short: 「（最大の 30%・上限まで）」.</summary>
        public const string CappedMark = "・上限まで";

        /// <summary>§5 重い宝箱: the cost line.</summary>
        // js: bridge/explorationScript/ExplorationText.js:133
        public static string ChestLine(int cost) => $"重い宝箱: 最大スタミナ −{cost}（一時的な変化）";

        /// <summary>§5 重い宝箱's contents in the table's order: 「カード 1 枚 + 頁 1 枚 + 消耗品 1 個」.</summary>
        // js: bridge/explorationScript/ExplorationText.js:138
        public static string ChestContents(ChestContents contents)
        {
            var parts = new List<string>();
            if (contents.Cards > 0) parts.Add($"カード {contents.Cards} 枚");
            if (contents.Pages > 0) parts.Add($"頁 {contents.Pages} 枚");
            if (contents.Consumables > 0) parts.Add($"消耗品 {contents.Consumables} 個");
            return $"中身: {string.Join(" + ", parts)}";
        }

        /// <summary>§5 特殊な食事: the gain and the cost.</summary>
        // js: bridge/explorationScript/ExplorationText.js:147
        public static string MealLine(int gain, int cost, int percent) =>
            $"特殊な食事: 最大スタミナ +{gain}（一時的な変化）・HP −{cost}（最大の {percent}%）";

        /// <summary>§5: 「どちらを選んでも刻限は 1 減ります」.</summary>
        public const string EventChoiceNote = "受ける / 受けない を選べる。どちらでも刻限は 1 減る";

        /// <summary>miasma_and_battle_v4.md §5: the temporary changes share ±4.</summary>
        public const string TempClampNote = "一時的な変化は、休息の +2 と合わせて ±4 まで";

        /// <summary>§5: 「中身は隣接したときに見えます」 — a far event node keeps its kind only.</summary>
        public const string EventHidden = "隣に来ると、何が起きるかが分かる";

        public const string AcceptLabel = "受ける";

        public const string DeclineLabel = "受けない";

        /// <summary>The event card's lead, after the step paid its 刻限.</summary>
        // js: bridge/explorationScript/ExplorationText.js:165
        public static string EventLead(string name) => $"{name}がある。受けるか、受けずに通り過ぎるかを選ぶ。";

        /// <summary>The result line of a declined event.</summary>
        public const string EventDeclined = "受けずに通り過ぎた";

        /// <summary>Under the 受けない button: the 刻限 was paid on the way in.</summary>
        public const string DeclineNote = "何も変わらない（刻限はもう払った）";

        /// <summary>A chest's card on the journal's dungeon page.</summary>
        // js: bridge/explorationScript/ExplorationText.js:176
        public static string RecordCards(int n) => $"カード {n} 枚";

        /// <summary>「HP 30 → 40」 and the like, for an event's result and a consumable's forecast.</summary>
        // js: bridge/explorationScript/ExplorationText.js:179
        public static string HpChange(int before, int after) => $"HP {before} → {after}";

        // js: bridge/explorationScript/ExplorationText.js:181
        public static string MaxStaminaChange(int before, int after) => $"最大スタミナ {before} → {after}";

        // js: bridge/explorationScript/ExplorationText.js:183
        public static string StaminaChange(int before, int after) => $"スタミナ {before} → {after}";

        // js: bridge/explorationScript/ExplorationText.js:185
        public static string MiasmaChange(int before, int after) => $"瘴気 {before}% → {after}%";

        /// <summary>A chest's finds on the result card.</summary>
        // js: bridge/explorationScript/ExplorationText.js:188
        public static string FoundCards(int n) => $"カード {n} 枚から 1 枚を選べる";

        // js: bridge/explorationScript/ExplorationText.js:190
        public static string FoundPages(int n) => $"希少な頁 {n} 枚";

        // js: bridge/explorationScript/ExplorationText.js:192
        public static string FoundConsumable(string name) => $"消耗品: {name}";

        /// <summary>§4.3: 「すべての山が尽きたときは、札の代わりに消耗品を出します」.</summary>
        public const string PileSpent = "札の山が尽きていたので、札の代わりに消耗品が入っていた";

        /// <summary>The journal's dungeon page row of this layer's events.</summary>
        public const string EventsRow = "出来事";

        /// <summary>One event on the journal's dungeon page.</summary>
        // js: bridge/explorationScript/ExplorationText.js:201
        public static string EventRecord(string name, bool accepted, string summary) =>
            accepted ? $"{name}: {summary}" : $"{name}: 受けなかった";

        /// <summary>What an event did, on its node once walked.</summary>
        // js: bridge/explorationScript/ExplorationText.js:206
        public static string EventDone(bool accepted, string summary) =>
            accepted ? $"結果: {summary}" : "結果: 受けなかった";

        public const string NoEvents = "まだ何も起きていない";

        // ---- the slots (tools_and_consumables_v4.md §3, encounters_and_rewards_v4.md §4.3)

        /// <summary>A slot whose item would change nothing now.</summary>
        public const string NoEffectNow = "いま使っても何も変わらない";

        /// <summary>標の石 with nowhere to land, or no 刻限 to pay.</summary>
        public const string NoJumpNow = "跳べるのは、刻限が残る探索中だけ";

        /// <summary>The hint while a 標の石 is aimed.</summary>
        public const string AimHint = "標の石: 跳ぶ先の、まだ踏んでいないノードを選ぶ（刻限 1 を使う。Esc でやめる）";

        /// <summary>A node 標の石 can land on.</summary>
        public const string JumpNote = "標の石で跳べる。入ると刻限を 1 使う";

        public const string JumpLabel = "跳ぶ";

        public const string JumpRestLabel = "跳んで休む";

        public const string JumpTrainLabel = "跳んで訓練する";

        public const string SwapTitle = "消耗品の枠が埋まっている";

        /// <summary>The swap card's lead.</summary>
        // js: bridge/explorationScript/ExplorationText.js:235
        public static string SwapLead(string name) =>
            $"{name}を拾った。入れ替える枠を選ぶか、諦める。消耗品は拠点へ持ち帰らない。";

        // js: bridge/explorationScript/ExplorationText.js:239
        public static string SwapButton(string name) => $"{name}と入れ替える";

        public const string LeaveFoundLabel = "諦める";

        // ---- the tools (tools_and_consumables_v4.md §2)

        public const string SideExploration = "探索";

        public const string SideBattle = "戦闘";

        /// <summary>刻限の砂 in the band: 「刻限の砂 +1」.</summary>
        // js: bridge/explorationScript/ExplorationText.js:250
        public static string TimeBonus(string name, int amount) => $"{name} +{amount}";

        /// <summary>防瘴の面 beside the gauge: 「防瘴の面 濃さ 2 → 1」, or 「防瘴の面 濃さは下限 1 のまま」 (seven_layers_v4.md §3.2).</summary>
        // js: bridge/explorationScript/ExplorationText.js:253
        public static string Relief(string names, int density, int effective) =>
            effective < density ? $"{names} 濃さ {density} → {effective}" : $"{names} 濃さは下限 {effective} のまま";

        /// <summary>
        /// The journal's density row when the tools would take the density below its floor (seven_layers_v4.md §3.2
        /// 「濃度は 1 より下がりません」): 「層の濃さ 2 − ツール 2、下限 1」.
        /// </summary>
        // js: bridge/explorationScript/ExplorationText.js:261
        public static string ReliefFloor(int density, int relief, int floor) =>
            $"層の濃さ {density} − ツール {relief}、下限 {floor}";

        /// <summary>
        /// The journal's page of the layer-two carving (seven_layers_v4.md §5 「拾うと起きる … 手記に頁が 1 つ増える」;
        /// what it shows: world-v1.md §5, 「二層の柱の彫りが竜の形をしている」; no battle effect, §5.2).
        /// </summary>
        public const string CarvingPageTitle = "秘跡の彫り";

        /// <summary>
        /// The carving page's rows, label and text (js: ExplorationText.js:270). Read-only like the JS's frozen table:
        /// the array cannot be cast back and written, which would outlive the play session with domain reload off.
        /// </summary>
        public static readonly IReadOnlyList<(string Label, string Text)> CarvingPageRows = Array.AsReadOnly(new[]
        {
            ("", "第 2 層の柱に、竜の形をした彫りがあった。"),
            ("層の名", "■■の秘跡 は、竜神の秘跡 と読めた"),
            ("戦闘", "効かない"),
        });

        /// <summary>A 死亡地点の痕跡 node once this life collected it (docs/INHERITANCE.md R-09 / R-10).</summary>
        public const string TraceCollected = "この痕跡は回収した";

        /// <summary>灯芯の欠片 on the interlude line: 「（最大の 45%・灯芯の欠片 +15%）」.</summary>
        // js: bridge/explorationScript/ExplorationText.js:280
        public static string InterludeShare(int percent, string name, int bonus, bool capped = false)
        {
            string mark = capped ? CappedMark : "";
            return bonus > 0 ? $"（最大の {percent}%・{name} +{bonus}%{mark}）" : $"（最大の {percent}%{mark}）";
        }

        /// <summary>
        /// A tool's effect in numbers on the inventory (js: ExplorationText.js:286, a table keyed by the kind):
        /// "" for a kind the table does not hold (the battle tools).
        /// </summary>
        public static string ToolEffect(ReliefKind kind, int amount) => kind switch
        {
            ReliefKind.Density => $"濃さ −{amount}",
            ReliefKind.Time => $"刻限 +{amount}",
            ReliefKind.Survey => "相手を選ぶ",
            ReliefKind.Interlude => $"休憩 +{amount}%",
            _ => "",
        };

        // ---- 写しの帳 (tools_and_consumables_v4.md §2.2)

        /// <summary>On a 情報収集 node when 写しの帳 is carried.</summary>
        public const string SurveyChooseLine = "写しの帳: 開示度を上げる相手を自分で選べる";

        public const string SurveyTitle = "情報収集の相手を選ぶ";

        public const string SurveyLead = "写しの帳: この層の敵から 1 種を選び、手記の開示度を 2 段目へ上げる。";

        /// <summary>One enemy on the survey card.</summary>
        // js: bridge/explorationScript/ExplorationText.js:303
        public static string SurveyButton(string name, int disclosure) => $"{name}（開示度 {disclosure} → 2）";

        /// <summary>The journal's survey row once the player chose.</summary>
        // js: bridge/explorationScript/ExplorationText.js:306
        public static string SurveyChosen(string name) => $"済ませた。{name}が開示度 2 になった（写しの帳）";

        /// <summary>The journal's survey row once the layer chose.</summary>
        // js: bridge/explorationScript/ExplorationText.js:309
        public static string SurveyDone(string name) => $"済ませた。{name}が開示度 2 になった";

        /// <summary>The survey found every enemy behind this map at 開示度 2 already (the intel's SurveyTarget gave none).</summary>
        public const string SurveyNothing = "済ませた。この層の敵は、みな開示度 2 だった";
    }
}
