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

        /// <summary>Next door and not yet resolved: entering costs a day of the calendar.</summary>
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

        /// <summary>Takes the step that was waiting on a confirmation (the calendar runs out ahead).</summary>
        ConfirmStep,

        /// <summary>Descends although the next layer's entry runs the calendar out.</summary>
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

        /// <summary>
        /// The layer-two interlude's way down (dungeon_exploration_v4.md §6.5, seven_layers_v4.md §3.1): the argument is
        /// (int)Region.Dragon / Parasite / Ghost as an invariant-culture integer. Appended last so the values before it stay.
        /// </summary>
        ChooseRegion,
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
        Interlude,

        /// <summary>The calendar ran out: the world ends and the game starts over (§6.9).</summary>
        Expired,

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
            DaysBefore = before.DaysLeft;
            DaysAfter = after.DaysLeft;
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

        /// <summary>Days left on the collapse calendar before the step.</summary>
        public int DaysBefore { get; }

        /// <summary>Days left after it: the same while a king is down or the node costs no day.</summary>
        public int DaysAfter { get; }

        public int MaxStaminaBefore { get; }
        public int MaxStaminaAfter { get; }
        public int StaminaBefore { get; }
        public int StaminaAfter { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public RunPhase PhaseAfter { get; }

        /// <summary>「暦 残り 312 → 311 日」 first, then one line per other value that changes.</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>The calendar running out here (§6.4), or nothing.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Entering here runs the calendar out: the world ends.</summary>
        public bool IsExpiry => PhaseAfter == RunPhase.Expired;
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

        /// <summary>Stepping here runs the calendar out, so the button asks first.</summary>
        public bool NeedsConfirm => Forecast != null && Forecast.IsExpiry;
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

        /// <summary>A short line on where the node stands (「入ると暦が 1 日進む」 and the like).</summary>
        public string Note { get; init; }

        /// <summary>The detail panel's heading: the glyph and the kind.</summary>
        public string Title { get; init; }

        /// <summary>
        /// The detail panel's text, line by line: the note, what is inside, then one line per way in
        /// with its forecast (「【休む】暦 残り 312 → 311 日　HP 30 → 38」) and its warnings.
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

        /// <summary>What it does, in one line (「層の入口で日が進まない」), or "" for a battle tool.</summary>
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
        private readonly string _calendarNote = "";
        private readonly string _daysForecastLabel = "";
        private readonly IReadOnlyList<EventOutcome> _events = new EventOutcome[0];
        private readonly MapLayout _layout = MapLayout.Full;

        /// <summary>「第 2 層　■■の秘跡」, 「第 3 層　大黒蛇の住処（竜の領域）」 or 「竜の領域 第 4 層」 (§6.2).</summary>
        public string LayerTitle { get; init; }

        /// <summary>The depth the map is on (1〜7).</summary>
        public int Layer { get; init; }

        /// <summary>
        /// Which of the seven backdrops lies behind the map (LayerProfile.BackdropLayer, 1〜7). The regions share depths,
        /// so the backdrop is looked up by this slot, not by the depth (seven_layers_v4.md §3.2 / §3.4).
        /// </summary>
        public int BackdropLayer { get; init; }

        /// <summary>Days left on the collapse calendar (seven_layers_v4.md §2).</summary>
        public int DaysLeft { get; init; }

        /// <summary>「残り 312 日」.</summary>
        public string DaysLabel { get; init; }

        /// <summary>A king is down, so no day passes (seven_layers_v4.md §2.2).</summary>
        public bool CalendarFrozen { get; init; }

        /// <summary>"" while the calendar runs; 「竜王が戻るまで 41 日。暦は止まっている」 while a king is down.</summary>
        public string CalendarNote { get => _calendarNote; init => _calendarNote = value ?? ""; }

        /// <summary>
        /// Days left after the next new node, or after the descent at the interlude, tried on the core (§6.4: the band
        /// draws the day the next node takes ahead of time). DaysLeft when no day is ahead.
        /// </summary>
        public int NextDaysLeft { get; init; }

        /// <summary>「次の 1 歩で 1 日進む」, 「降りると 1 日進む」, 「次の 1 日は進まない」, or "".</summary>
        public string DaysForecastLabel { get => _daysForecastLabel; init => _daysForecastLabel = value ?? ""; }

        public string HpLabel { get; init; }

        public string StaminaLabel { get; init; }

        /// <summary>「最大 12 = 10 + 一時 2」, or the maximum alone when no temporary change moves it.</summary>
        public string MaxStaminaLabel { get; init; }

        public string MarksLabel { get; init; }

        public IReadOnlyList<NodePanel> Nodes { get; init; }

        public IReadOnlyList<EdgeLine> Edges { get; init; }

        /// <summary>How big the map draws its nodes: smaller on a map of more than five rows, so the rows fit one screen (#1049).</summary>
        public MapLayout Layout { get => _layout; init => _layout = value ?? MapLayout.Full; }

        public int CurrentNodeId { get; init; }

        /// <summary>One line under the map on how to read it.</summary>
        public string Hint { get; init; }

        /// <summary>「ツール: 帰り道の糸（1 / 3）」.</summary>
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
