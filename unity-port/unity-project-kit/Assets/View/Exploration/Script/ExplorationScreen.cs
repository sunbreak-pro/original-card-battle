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

        // Trial extension (js: ExplorationScreen.js:52 TrialAction; docs/EXTENSIONS.md 「探索の持ち物とイベント」): the
        // buttons of the trial cards, answered by ExplorationSession.Act(action, arg). The browser trial keeps them in a
        // separate enum; here they follow the C#'s members, whose values and order stay as they were.

        /// <summary>「受ける」 on a 重い宝箱 or 特殊な食事 (encounters_and_rewards_v4.md §5).</summary>
        AcceptEvent,

        /// <summary>「受けない」 on a 重い宝箱 or 特殊な食事.</summary>
        DeclineEvent,

        /// <summary>A found consumable takes the slot named by the argument (0〜2) when the three are full (§4.3).</summary>
        TakeFound,

        /// <summary>「諦める」: the found consumable is left behind (tools_and_consumables_v4.md §3).</summary>
        LeaveFound,

        /// <summary>写しの帳: the enemy id the argument names is the 情報収集 target (tools_and_consumables_v4.md §2.2).</summary>
        ChooseSurvey,
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

        // Trial extension (js: ExplorationScreen.js:75 TrialCardKind): the cards over the map that wait on a choice.

        /// <summary>受ける / 受けない on a 重い宝箱 or 特殊な食事.</summary>
        EventChoice,

        /// <summary>A found consumable and full slots: swap or give up.</summary>
        Swap,

        /// <summary>写しの帳's pick of the 情報収集 target.</summary>
        SurveyChoice,
    }

    /// <summary>
    /// What the top band's buttons open over the screen (dungeon_exploration_v4.md §6.2), one at a
    /// time. The journal is a drawer beside the map; the other two cover it.
    /// </summary>
    public enum Overlay
    {
        None,

        /// <summary>The journal drawer: the band's button or J.</summary>
        Journal,

        /// <summary>The cards the character holds, read only: the band's button or C.</summary>
        Cards,

        /// <summary>Settings and 「保存してゲームを閉じる」: the band's gear or Esc.</summary>
        Settings,
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
        public StepOption(string label, RestChoice choice, StepForecast forecast, bool jump = false)
        {
            Label = label;
            Choice = choice;
            Forecast = forecast;
            Jump = jump;
        }

        public string Label { get; }

        public RestChoice Choice { get; }

        /// <summary>Null for walking back over a resolved node: nothing changes but where the player stands.</summary>
        public StepForecast Forecast { get; }

        /// <summary>
        /// Trial extension (js: ExplorationScreen.js:140): the way in is 標の石's jump (ExplorationSession.Jump),
        /// not a step (tools_and_consumables_v4.md §3).
        /// </summary>
        public bool Jump { get; }

        /// <summary>Stepping here ends the life, so the button asks first.</summary>
        public bool NeedsConfirm => Forecast != null && Forecast.IsLethal;
    }

    /// <summary>
    /// One node on the map. The node itself fixes where it sits on the grid; everything else is named
    /// in an object initializer, so two of its strings cannot trade places unnoticed (#597). A panel, not
    /// a card: a card in this game is one of the deck's, and the battle's script has its own CardKind (#598).
    /// </summary>
    public sealed class NodePanel
    {
        public NodePanel(MapNode node)
        {
            Id = node.Id;
            Row = node.Row;
            Column = node.Column;
            Kind = node.Kind;
        }

        public int Id { get; }
        public int Row { get; }
        public int Column { get; }
        public NodeKind Kind { get; }

        /// <summary>0 = left edge of the map area, 1 = right edge.</summary>
        public float X { get; init; }

        /// <summary>0 = bottom of the map area, 1 = top. The entry sits on top, the boss at the bottom.</summary>
        public float Y { get; init; }

        /// <summary>The one-character stand-in for the node's icon (戦・精・主・休・調・事・痕・彫).</summary>
        public string Glyph { get; init; }

        public string KindLabel { get; init; }

        public NodeStanding Standing { get; init; }

        /// <summary>What is inside. Empty for a far node, which shows its kind only (§6.3).</summary>
        public IReadOnlyList<string> Lines { get; init; }

        /// <summary>The buttons the detail panel offers. Empty unless the node can be walked to now.</summary>
        public IReadOnlyList<StepOption> Options { get; init; }

        /// <summary>A short line on where the node stands (「入ると刻限を 1 使う」 and the like).</summary>
        public string Note { get; init; }

        /// <summary>The detail panel's heading: the glyph and the kind.</summary>
        public string Title { get; init; }

        /// <summary>
        /// The detail panel's text, line by line: the note, what is inside, then one line per way in
        /// with its forecast (「【休む】刻限 9 → 8　瘴気 1% → 2%」) and its warnings.
        /// </summary>
        public IReadOnlyList<string> Detail { get; init; }
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
        public MiasmaGauge(int percent, int nextPercent, string label, string forecastLabel, IReadOnlyList<int> stepLines)
        {
            Percent = percent;
            NextPercent = nextPercent;
            Label = label;
            ForecastLabel = forecastLabel;
            StepLines = stepLines;
            Fill = FillOf(percent);
            NextFill = FillOf(nextPercent);
            var fills = new float[stepLines.Count];
            for (int i = 0; i < fills.Length; i++) fills[i] = FillOf(stepLines[i]);
            StepLineFills = fills;
        }

        /// <summary>A reading as a fraction of the whole gauge, which ends at 瘴気死 (Miasma.DeathAt).</summary>
        private static float FillOf(int percent) => (float)percent / Miasma.DeathAt;

        public int Percent { get; }

        /// <summary>The reading after one more new node. Equal to Percent when no step is possible.</summary>
        public int NextPercent { get; }

        /// <summary>Percent as 0..1 of the gauge.</summary>
        public float Fill { get; }

        /// <summary>NextPercent as 0..1 of the gauge.</summary>
        public float NextFill { get; }

        public string Label { get; }

        /// <summary>「次の 1 歩で +3%」, or "" when no new node can be entered.</summary>
        public string ForecastLabel { get; }

        /// <summary>The readings where max stamina drops by one (20, 40, 60, 80).</summary>
        public IReadOnlyList<int> StepLines { get; }

        /// <summary>Each of StepLines as 0..1 of the gauge, in the same order.</summary>
        public IReadOnlyList<float> StepLineFills { get; }
    }

    /// <summary>One consumable slot. A panel, not a card, for the same reason as NodePanel (#598).</summary>
    public sealed class SlotPanel
    {
        public SlotPanel(int index, string name, string note, bool canUse,
            IReadOnlyList<string> forecast = null, string reason = "", bool aiming = false, string id = null)
        {
            Index = index;
            Name = name;
            Note = note;
            CanUse = canUse;
            Forecast = forecast;
            Reason = reason ?? "";
            Aiming = aiming;
            Id = id;
        }

        public int Index { get; }

        /// <summary>The item's name, or 「（空き）」.</summary>
        public string Name { get; }

        public string Note { get; }

        public bool CanUse { get; }

        /// <summary>Trial extension (js: ExplorationScreen.js:257): what using it changes, tried on the state (「HP 30 → 40」), or null.</summary>
        public IReadOnlyList<string> Forecast { get; }

        /// <summary>Trial extension: why it cannot be used now, or "".</summary>
        public string Reason { get; }

        /// <summary>Trial extension: this 標の石 is being aimed — the map picks where it lands.</summary>
        public bool Aiming { get; }

        /// <summary>Trial extension: the catalogue id of the item in the slot, or null when empty.</summary>
        public string Id { get; }
    }

    public sealed class CardButton
    {
        public CardButton(string label, ScreenAction action, string arg = null, string note = "")
        {
            Label = label;
            Action = action;
            Arg = arg;
            Note = note ?? "";
        }

        public string Label { get; }
        public ScreenAction Action { get; }

        /// <summary>
        /// Trial extension (js: ExplorationScreen.js:275): the argument ExplorationSession.Act takes with the action —
        /// a slot (an invariant-culture integer string) or an enemy id — or null.
        /// </summary>
        public string Arg { get; }

        /// <summary>Trial extension: one line under the button (what the choice changes), or "".</summary>
        public string Note { get; }
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

        /// <summary>A card that waits for a yes or a no. Esc answers no before it does anything else (§6.2).</summary>
        public bool IsConfirmation =>
            Kind == CardKind.ConfirmStep || Kind == CardKind.ConfirmDescend || Kind == CardKind.ConfirmEndLife;

        /// <summary>Trial extension (js: ExplorationScreen.js:305): a card that waits for one of its buttons (an event, a swap, a survey pick).</summary>
        public bool IsChoice =>
            Kind == CardKind.EventChoice || Kind == CardKind.Swap || Kind == CardKind.SurveyChoice;
    }

    /// <summary>
    /// Trial extension (js: ExplorationScreen.js:326): one carried tool as the inventory shows it — its name, the
    /// catalogue's note, and whether it acts on the exploration or in battle (tools_and_consumables_v4.md §2).
    /// </summary>
    public sealed class ToolCard
    {
        public ToolCard(string id, string name, string note, string side, string effect)
        {
            Id = id;
            Name = name;
            Note = note;
            Side = side;
            Effect = effect;
        }

        public string Id { get; }
        public string Name { get; }
        public string Note { get; }

        /// <summary>「探索」 or 「戦闘」.</summary>
        public string Side { get; }

        /// <summary>What it does on this layer, in numbers (「刻限 +1」), or "" for a battle tool.</summary>
        public string Effect { get; }
    }

    /// <summary>
    /// Trial extension (js: ExplorationScreen.js:342): what one イベント did (encounters_and_rewards_v4.md §5), kept
    /// for the result card and the journal's dungeon page (「起きたイベントは、手記のダンジョンの頁に…残します」).
    /// </summary>
    public sealed class EventOutcome
    {
        public EventOutcome(int layer, int nodeId, EventKind kind, string name, bool accepted, IReadOnlyList<string> lines,
            IReadOnlyList<string> cards, IReadOnlyList<string> consumables, int pages, IReadOnlyList<string> record = null)
        {
            Layer = layer;
            NodeId = nodeId;
            Kind = kind;
            Name = name;
            Accepted = accepted;
            Lines = lines;
            Cards = cards ?? new string[0];
            Consumables = consumables ?? new string[0];
            Pages = pages;
            Record = record ?? lines;
        }

        public int Layer { get; }
        public int NodeId { get; }

        /// <summary>LayerEvents' EventKind.</summary>
        public EventKind Kind { get; }

        /// <summary>「泉」 and the like.</summary>
        public string Name { get; }

        /// <summary>The player took it (a 泉 always does).</summary>
        public bool Accepted { get; }

        /// <summary>What changed, line by line (「HP 30 → 40」).</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>A chest's cards laid out to pick one from (Expedition.Learn), as ids.</summary>
        public IReadOnlyList<string> Cards { get; }

        /// <summary>A chest's consumables, as catalogue ids.</summary>
        public IReadOnlyList<string> Consumables { get; }

        /// <summary>A chest's rare pages.</summary>
        public int Pages { get; }

        /// <summary>The short lines the journal keeps (what changed and what was found), or the result lines.</summary>
        public IReadOnlyList<string> Record { get; }

        /// <summary>The one-line summary the journal's dungeon page shows.</summary>
        public string Summary => string.Join("・", Record);
    }

    /// <summary>A screen the top band opens over the map: 所持カード or 設定・セーブ (§6.2).</summary>
    public sealed class OverlayPage
    {
        public OverlayPage(string title, IReadOnlyList<string> lines, string closeLabel)
        {
            Title = title;
            Lines = lines;
            CloseLabel = closeLabel;
        }

        public string Title { get; }
        public IReadOnlyList<string> Lines { get; }

        /// <summary>「閉じる（C）」 and the like: the button and the key that close it.</summary>
        public string CloseLabel { get; }
    }

    /// <summary>
    /// One frame of the exploration screen. It has no constructor: ExplorationScreenBuilder names every
    /// member in an object initializer, so the band's labels — eight strings side by side — cannot trade
    /// places unnoticed the way positional arguments could (#597). The trial's members default to empty.
    /// </summary>
    public sealed class ExplorationScreen
    {
        private readonly IReadOnlyList<ToolCard> _toolCards = new ToolCard[0];
        private readonly string _timeBonusLabel = "";
        private readonly string _reliefLabel = "";
        private readonly IReadOnlyList<EventOutcome> _events = new EventOutcome[0];

        /// <summary>「第 2 層　■■の秘跡」.</summary>
        public string LayerTitle { get; init; }

        /// <summary>The layer the map is on (1〜7): the view lays that layer's backdrop behind the map (battle-visual-v1 §2.5).</summary>
        public int Layer { get; init; }

        /// <summary>「刻限 7 / 10」.</summary>
        public string TimeLabel { get; init; }

        /// <summary>How many of the TimeLimit pips are still lit.</summary>
        public int TimeLeft { get; init; }

        public int TimeLimit { get; init; }

        public MiasmaGauge Miasma { get; init; }

        public string HpLabel { get; init; }

        public string StaminaLabel { get; init; }

        /// <summary>「最大 9 = 10 − 瘴気 1」, with a temporary modifier added when there is one.</summary>
        public string MaxStaminaLabel { get; init; }

        public string MarksLabel { get; init; }

        public IReadOnlyList<NodePanel> Nodes { get; init; }

        public IReadOnlyList<EdgeLine> Edges { get; init; }

        public int CurrentNodeId { get; init; }

        /// <summary>One line under the map on how to read it.</summary>
        public string Hint { get; init; }

        /// <summary>「ツール: 防瘴の面（1 / 3）」.</summary>
        public string ToolsLabel { get; init; }

        public IReadOnlyList<SlotPanel> Consumables { get; init; }

        /// <summary>The 「この生を終える」 button is live (§6.6).</summary>
        public bool CanEndLife { get; init; }

        /// <summary>The button is on screen at all. It goes once the life has ended (§6.2).</summary>
        public bool ShowsEndLife { get; init; }

        public ScreenCard Card { get; init; }

        public JournalBook Journal { get; init; }

        /// <summary>What is open over the screen from the top band, if anything.</summary>
        public Overlay OpenOverlay { get; init; }

        /// <summary>
        /// The band's 所持カード button is live. Like the gear, it goes dead while the stand-in battle
        /// card is up, which counts as being in battle (§6.2). The journal's button never does.
        /// </summary>
        public bool CanOpenCards { get; init; }

        public bool CanOpenSettings { get; init; }

        /// <summary>The 所持カード screen. Its contents wait for #244; until then it is a stand-in.</summary>
        public OverlayPage Cards { get; init; }

        /// <summary>The 設定・セーブ screen. Settings and saving are not wired yet; it is a stand-in.</summary>
        public OverlayPage Settings { get; init; }

        /// <summary>Trial extension (js: ExplorationScreen.js:455): every carried tool with its note. Empty outside trial.</summary>
        public IReadOnlyList<ToolCard> ToolCards { get => _toolCards; init => _toolCards = value ?? new ToolCard[0]; }

        /// <summary>Trial extension: 「刻限の砂 +1」 beside the 刻限 in the band, or "".</summary>
        public string TimeBonusLabel { get => _timeBonusLabel; init => _timeBonusLabel = value ?? ""; }

        /// <summary>Trial extension: 「防瘴の面 濃さ 2 → 1」 beside the gauge, or "".</summary>
        public string ReliefLabel { get => _reliefLabel; init => _reliefLabel = value ?? ""; }

        /// <summary>Trial extension: a 標の石 is being aimed; the nodes it can land on carry jump options.</summary>
        public bool Aiming { get; init; }

        /// <summary>Trial extension: every event of this life so far, oldest first.</summary>
        public IReadOnlyList<EventOutcome> Events { get => _events; init => _events = value ?? new EventOutcome[0]; }

        /// <summary>
        /// The map takes input only while no card covers it. The carried items, the band and
        /// 「この生を終える」 stay above a card: whether they work is CanUse / CanEndLife.
        /// </summary>
        public bool MapIsLive => !Card.IsShown;

        public NodePanel Node(int id)
        {
            foreach (var node in Nodes)
            {
                if (node.Id == id) return node;
            }
            return null;
        }
    }
}
