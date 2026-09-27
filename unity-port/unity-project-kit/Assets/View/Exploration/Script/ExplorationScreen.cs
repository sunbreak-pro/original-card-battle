// What the exploration screen shows, as plain values (dungeon_exploration_v4.md §6). The View puts
// these on screen and computes nothing: every number and line of text here was read from the
// exploration core by ExplorationScreenBuilder. Positions are fractions of the map area, so the
// View only scales them.
using System.Collections.Generic;
using DungeonCore;
using Journal;

namespace Exploration
{
    /// <summary>Where a node stands relative to the player (dungeon_exploration_v4.md §6.3).</summary>
    public enum NodeStanding
    {
        /// <summary>The player stands here.</summary>
        Current,

        /// <summary>Next door and not yet resolved, with 刻限 left: entering costs one.</summary>
        Reachable,

        /// <summary>Next door and already resolved: walking back is free.</summary>
        Revisit,

        /// <summary>Resolved, not next door.</summary>
        Resolved,

        /// <summary>Not resolved, not next door. Only the kind shows.</summary>
        Far,
    }

    /// <summary>What a button on the screen asks the session to do.</summary>
    public enum ScreenAction
    {
        /// <summary>The stand-in battle: report it won, HP and stamina unchanged (§6.8).</summary>
        WinBattle,

        /// <summary>The stand-in battle: report it lost, HP 0 (§6.8).</summary>
        FallInBattle,

        GoToInterlude,
        Descend,

        /// <summary>Opens the confirmation for 「この生を終える」 (§6.6).</summary>
        AskEndLife,

        ConfirmEndLife,

        /// <summary>Takes the step that was waiting on a confirmation (瘴気死 ahead).</summary>
        ConfirmStep,

        /// <summary>Descends although the next layer's entry ends the life by 瘴気.</summary>
        ConfirmDescend,

        /// <summary>Closes a confirmation without doing anything.</summary>
        Cancel,

        /// <summary>Starts a fresh run from layer one with a new seed (§6.9).</summary>
        NewLife,
    }

    /// <summary>Which card sits over the map, if any.</summary>
    public enum CardKind
    {
        None,
        Battle,
        ConfirmStep,
        ConfirmDescend,
        ConfirmEndLife,
        LayerCleared,
        PushedOut,
        Interlude,
        MiasmaDeath,
        Fallen,
        Completed,
        Survived,
    }

    /// <summary>The run's numbers before and after entering one node, straight from ExplorationReducer.Step.</summary>
    public sealed class StepForecast
    {
        public StepForecast(ExplorationState before, ExplorationState after, IReadOnlyList<string> lines, IReadOnlyList<string> warnings)
        {
            TimeBefore = before.TimeLeft;
            TimeAfter = after.TimeLeft;
            MiasmaBefore = before.MiasmaPercent;
            MiasmaAfter = after.MiasmaPercent;
            MaxStaminaBefore = before.MaxStamina;
            MaxStaminaAfter = after.MaxStamina;
            StaminaBefore = before.Stamina;
            StaminaAfter = after.Stamina;
            HpBefore = before.Hp;
            HpAfter = after.Hp;
            PhaseAfter = after.Phase;
            Lines = lines;
            Warnings = warnings;
        }

        public int TimeBefore { get; }
        public int TimeAfter { get; }
        public int MiasmaBefore { get; }
        public int MiasmaAfter { get; }
        public int MaxStaminaBefore { get; }
        public int MaxStaminaAfter { get; }
        public int StaminaBefore { get; }
        public int StaminaAfter { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public RunPhase PhaseAfter { get; }

        /// <summary>「刻限 7 → 6」 and the like, one per value that changes.</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>A 20% line crossed, 瘴気死, or being pushed out (§6.4).</summary>
        public IReadOnlyList<string> Warnings { get; }

        public bool IsLethal => PhaseAfter == RunPhase.MiasmaDeath;
    }

    /// <summary>One way into a node: 「進む」, 「休む」, 「訓練する」 or 「移る」.</summary>
    public sealed class StepOption
    {
        public StepOption(string label, RestChoice choice, StepForecast forecast)
        {
            Label = label;
            Choice = choice;
            Forecast = forecast;
        }

        public string Label { get; }

        public RestChoice Choice { get; }

        /// <summary>Null for walking back over a resolved node: nothing changes but where the player stands.</summary>
        public StepForecast Forecast { get; }

        /// <summary>Stepping here ends the life, so the button asks first.</summary>
        public bool NeedsConfirm => Forecast != null && Forecast.IsLethal;
    }

    /// <summary>One node on the map.</summary>
    public sealed class NodeCard
    {
        public NodeCard(
            MapNode node,
            float x,
            float y,
            string glyph,
            string kindLabel,
            NodeStanding standing,
            IReadOnlyList<string> lines,
            IReadOnlyList<StepOption> options,
            string note,
            string title,
            IReadOnlyList<string> detail)
        {
            Id = node.Id;
            Row = node.Row;
            Column = node.Column;
            Kind = node.Kind;
            X = x;
            Y = y;
            Glyph = glyph;
            KindLabel = kindLabel;
            Standing = standing;
            Lines = lines;
            Options = options;
            Note = note;
            Title = title;
            Detail = detail;
        }

        public int Id { get; }
        public int Row { get; }
        public int Column { get; }
        public NodeKind Kind { get; }

        /// <summary>0 = left edge of the map area, 1 = right edge.</summary>
        public float X { get; }

        /// <summary>0 = bottom of the map area, 1 = top. The entry sits on top, the boss at the bottom.</summary>
        public float Y { get; }

        /// <summary>The one-character stand-in for the node's icon (戦・精・主・休・調・事・痕・彫).</summary>
        public string Glyph { get; }

        public string KindLabel { get; }

        public NodeStanding Standing { get; }

        /// <summary>A far node shows its kind only; the rest show what is inside (§6.3).</summary>
        public bool ShowsContents => Standing != NodeStanding.Far;

        /// <summary>What is inside. Empty for a far node.</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>The buttons the detail panel offers. Empty unless the node can be walked to now.</summary>
        public IReadOnlyList<StepOption> Options { get; }

        /// <summary>A short line on where the node stands (「入ると刻限を 1 使う」 and the like).</summary>
        public string Note { get; }

        /// <summary>The detail panel's heading: the glyph and the kind.</summary>
        public string Title { get; }

        /// <summary>
        /// The detail panel's text, line by line: the note, what is inside, then one line per way in
        /// with its forecast (「【休む】刻限 9 → 8　瘴気 1% → 2%」) and its warnings.
        /// </summary>
        public IReadOnlyList<string> Detail { get; }
    }

    /// <summary>A link between two nodes. Lit when it leads from where the player stands to somewhere they can go.</summary>
    public sealed class EdgeLine
    {
        public EdgeLine(int from, int to, bool lit)
        {
            From = from;
            To = to;
            Lit = lit;
        }

        public int From { get; }
        public int To { get; }
        public bool Lit { get; }
    }

    /// <summary>The 瘴気 gauge and what the next new node adds to it.</summary>
    public sealed class MiasmaGauge
    {
        /// <summary>The gauge is drawn as ten cells (concept-v3.md §6: ■■■■■■□□□□ 60%).</summary>
        public const int Cells = 10;

        public MiasmaGauge(int percent, int nextPercent, string label, string forecastLabel, IReadOnlyList<int> stepLines)
        {
            Percent = percent;
            NextPercent = nextPercent;
            Label = label;
            ForecastLabel = forecastLabel;
            StepLines = stepLines;
        }

        public int Percent { get; }

        /// <summary>The reading after one more new node. Equal to Percent when no step is possible.</summary>
        public int NextPercent { get; }

        public float Fill => Percent / 100f;

        public float NextFill => NextPercent / 100f;

        public int FilledCells => Percent * Cells / 100;

        public string Label { get; }

        /// <summary>「次の 1 歩で +3%」, or "" when no new node can be entered.</summary>
        public string ForecastLabel { get; }

        /// <summary>The readings where max stamina drops by one (20, 40, 60, 80).</summary>
        public IReadOnlyList<int> StepLines { get; }
    }

    /// <summary>One consumable slot.</summary>
    public sealed class SlotCard
    {
        public SlotCard(int index, string name, string note, bool canUse)
        {
            Index = index;
            Name = name;
            Note = note;
            CanUse = canUse;
        }

        public int Index { get; }

        /// <summary>The item's name, or 「（空き）」.</summary>
        public string Name { get; }

        public string Note { get; }

        public bool CanUse { get; }
    }

    public sealed class CardButton
    {
        public CardButton(string label, ScreenAction action)
        {
            Label = label;
            Action = action;
        }

        public string Label { get; }
        public ScreenAction Action { get; }
    }

    /// <summary>The card over the map: a stand-in battle, a confirmation, a layer's end or a life's end.</summary>
    public sealed class ScreenCard
    {
        public static readonly ScreenCard None =
            new ScreenCard(CardKind.None, "", new string[0], new CardButton[0]);

        public ScreenCard(CardKind kind, string title, IReadOnlyList<string> lines, IReadOnlyList<CardButton> buttons)
        {
            Kind = kind;
            Title = title;
            Lines = lines;
            Buttons = buttons;
        }

        public CardKind Kind { get; }
        public string Title { get; }
        public IReadOnlyList<string> Lines { get; }
        public IReadOnlyList<CardButton> Buttons { get; }

        public bool IsShown => Kind != CardKind.None;
    }

    /// <summary>One frame of the exploration screen.</summary>
    public sealed class ExplorationScreen
    {
        public ExplorationScreen(
            string layerTitle,
            string timeLabel,
            int timeLeft,
            int timeLimit,
            MiasmaGauge miasma,
            string hpLabel,
            string staminaLabel,
            string maxStaminaLabel,
            string marksLabel,
            IReadOnlyList<NodeCard> nodes,
            IReadOnlyList<EdgeLine> edges,
            int currentNodeId,
            string hint,
            string toolsLabel,
            IReadOnlyList<SlotCard> consumables,
            bool canEndLife,
            bool showsEndLife,
            ScreenCard card,
            JournalBook journal)
        {
            LayerTitle = layerTitle;
            TimeLabel = timeLabel;
            TimeLeft = timeLeft;
            TimeLimit = timeLimit;
            Miasma = miasma;
            HpLabel = hpLabel;
            StaminaLabel = staminaLabel;
            MaxStaminaLabel = maxStaminaLabel;
            MarksLabel = marksLabel;
            Nodes = nodes;
            Edges = edges;
            CurrentNodeId = currentNodeId;
            Hint = hint;
            ToolsLabel = toolsLabel;
            Consumables = consumables;
            CanEndLife = canEndLife;
            ShowsEndLife = showsEndLife;
            Card = card;
            Journal = journal;
        }

        /// <summary>「第 2 層　■■の秘跡」.</summary>
        public string LayerTitle { get; }

        /// <summary>「刻限 7 / 10」.</summary>
        public string TimeLabel { get; }

        /// <summary>How many of the TimeLimit pips are still lit.</summary>
        public int TimeLeft { get; }

        public int TimeLimit { get; }

        public MiasmaGauge Miasma { get; }

        public string HpLabel { get; }

        public string StaminaLabel { get; }

        /// <summary>「最大 9 = 10 − 瘴気 1」, with a temporary modifier added when there is one.</summary>
        public string MaxStaminaLabel { get; }

        public string MarksLabel { get; }

        public IReadOnlyList<NodeCard> Nodes { get; }

        public IReadOnlyList<EdgeLine> Edges { get; }

        public int CurrentNodeId { get; }

        /// <summary>One line under the map on how to read it.</summary>
        public string Hint { get; }

        /// <summary>「ツール: 防瘴の面（1 / 3）」.</summary>
        public string ToolsLabel { get; }

        public IReadOnlyList<SlotCard> Consumables { get; }

        /// <summary>The 「この生を終える」 button is live (§6.6).</summary>
        public bool CanEndLife { get; }

        /// <summary>The button is on screen at all. It goes once the life has ended (§6.2).</summary>
        public bool ShowsEndLife { get; }

        public ScreenCard Card { get; }

        public JournalBook Journal { get; }

        /// <summary>
        /// The map takes input only while no card covers it. The carried items, the band and
        /// 「この生を終える」 stay above a card: whether they work is CanUse / CanEndLife.
        /// </summary>
        public bool MapIsLive => !Card.IsShown;

        public NodeCard Node(int id)
        {
            foreach (var node in Nodes)
            {
                if (node.Id == id) return node;
            }
            return null;
        }
    }
}
