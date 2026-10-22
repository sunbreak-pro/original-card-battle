// Trial extension of the exploration session (port of the browser trial's src/bridge/explorationScript/
// ExplorationSession.js; docs/EXTENSIONS.md 「探索の持ち物とイベント」 and 「探索の結果と再開」). With RunStart.Trial off
// (the default) nothing here acts: every field stays empty, every trial call is refused, and the session is the
// C# as it was. ExplorationSession.cs carries the hooks, each marked 「Trial extension」.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DungeonCore;

// The EditMode tests set ExplorationSession.State the way the browser trial's tests do (Unity's assembly, then dotnet's).
[assembly: InternalsVisibleTo("Exploration.Tests")]
[assembly: InternalsVisibleTo("Exploration.Script.Tests")]

namespace Exploration
{
    public sealed partial class RunStart
    {
        /// <summary>
        /// Trial extension (js: ExplorationSession.js:55): the trial rules — every carried item acts
        /// (tools_and_consumables_v4.md §2〜§3), found consumables enter the slots (encounters_and_rewards_v4.md
        /// §4.3) and イベント nodes resolve (§5). False plays exactly the C#.
        /// </summary>
        public bool Trial { get; set; }

        /// <summary>
        /// Trial extension (js: ExplorationSession.js:60): the layer-two carving was read in an earlier life
        /// (docs/INHERITANCE.md R-20), so every life after shows 竜神の秘跡 from its start. Null leaves it to the map
        /// seam (ExplorationSession.CarvingKnownAt).
        /// </summary>
        public bool? CarvingKnown { get; set; }

        /// <summary>
        /// Trial extension (js: ExplorationSession.js:503, <c>RunStart.MapFor</c>): draws a layer's map instead of
        /// <c>profile.Map(seed)</c> — the lineage's trace node and the known carving. Null is exactly the C#.
        /// </summary>
        public Func<LayerProfile, ulong, LayerMap> MapFor { get; set; }
    }

    public sealed partial class ExplorationSession
    {
        private static readonly string[] NoIds = new string[0];

        private IReadOnlyList<int> _foundFrom = new int[0];
        private LayerMap _eventMap;
        private IReadOnlyDictionary<int, EventKind> _eventKinds;

        /// <summary>The trial rules are on (RunStart.Trial). Every trial field stays empty while it is off.</summary>
        public bool Trial { get; }

        /// <summary>
        /// What keeps what the exploration finds outside its core, or null: an Expedition sets itself here in trial
        /// mode. Without one a chest gives its max-stamina change only.
        /// </summary>
        public IExplorationKeeper Keeper { get; set; }

        /// <summary>A 重い宝箱 / 特殊な食事 waiting on 受ける / 受けない, or null.</summary>
        public PendingEventInfo PendingEvent { get; private set; }

        /// <summary>Every event of this life, oldest first — the result cards and the journal read it.</summary>
        public IReadOnlyList<EventOutcome> Events { get; private set; } = new EventOutcome[0];

        /// <summary>Consumables found while the three slots are full, waiting on a swap or 「諦める」 (catalogue ids).</summary>
        public IReadOnlyList<string> PendingFound { get; private set; } = NoIds;

        /// <summary>写しの帳: the enemies the player may pick as this layer's 情報収集 target; empty when none waits.</summary>
        public IReadOnlyList<string> SurveyChoices { get; private set; } = NoIds;

        /// <summary>This layer's 情報収集 target once known: 写しの帳's pick, or the intel's (the layer decides); else null.</summary>
        public string SurveyTarget { get; private set; }

        /// <summary>The target was the player's pick (写しの帳), not the layer's.</summary>
        public bool SurveyPicked { get; private set; }

        /// <summary>The 標の石 slot being aimed (the map picks where it lands), or −1.</summary>
        public int AimingSlot { get; private set; } = None;

        /// <summary>A 標の石 jump waiting on the 瘴気死 confirmation: its slot, or −1.</summary>
        public int PendingJumpSlot { get; private set; } = None;

        /// <summary>The state the last event left (its result card is pending while the state is still this one), or null.</summary>
        public ExplorationState EventState { get; private set; }

        /// <summary>
        /// The state the last 階層間の休憩 left: the interlude card's own lines (HP, stamina, gauge) read it, so a
        /// consumable used at the interlude does not rewrite what the rest gave (§6.5). Null outside trial.
        /// </summary>
        public ExplorationState InterludeState { get; private set; }

        /// <summary>
        /// A 習熟訓練 mark waits on its card (the scene's training card is up): set when a rest's 「訓練する」 earns a mark,
        /// or by OpenTraining; cleared by PutOffTraining (「あとで」), by spending the last mark, and by leaving the
        /// layer. A reload brings the card back while it is set (tier1 R1-16). Always false outside trial.
        /// </summary>
        public bool TrainingWaiting { get; private set; }

        /// <summary>A 重い宝箱 or 特殊な食事 waits on 受ける / 受けない.</summary>
        public bool HasPendingEvent => PendingEvent != null;

        /// <summary>A found consumable waits on a slot to swap or on 「諦める」.</summary>
        public bool HasPendingFound => PendingFound.Count > 0;

        /// <summary>写しの帳's pick of the 情報収集 target waits.</summary>
        public bool HasPendingSurvey => SurveyChoices.Count > 0;

        /// <summary>A 標の石 is being aimed.</summary>
        public bool IsAiming => AimingSlot != None;

        /// <summary>
        /// Whether the item in this slot can be used now. 標の石 needs the map live with a 刻限 left and a node to land
        /// on; any other item must change something (試作で決めたこと: a use that changes nothing is not offered).
        /// 「Changes something」 is the slot's forecast (ExplorationScreenBuilder.ChangeLines) not being empty — one
        /// definition for both, so a lit slot always says what it changes. Outside trial: any carried item, as the C#.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:251
        public bool CanUseSlot(int index)
        {
            if (!CanUseConsumables) return false;
            var carried = State.Loadout.Consumables;
            if (index < 0 || index >= carried.Count) return false;
            var item = carried[index];
            if (!Trial) return true;
            if (item.Kind == ReliefKind.Teleport)
                return MapIsLive && State.Map.Nodes.Any(n => ExplorationReducer.CanTeleport(State, n.Id));
            var before = State;
            return ExplorationScreenBuilder.ChangeLines(before, ExplorationReducer.UseConsumable(before, index)).Count > 0;
        }

        /// <summary>
        /// 標の石 lands on <paramref name="nodeId"/> (tools_and_consumables_v4.md §3: 「同じ層の未踏ノードへ 1 度だけ跳ぶ」,
        /// paying the node's 刻限 and 瘴気). A jump that would end the life by 瘴気 waits for ScreenAction.ConfirmStep,
        /// like a step. False when no 標の石 is aimed or it cannot land there.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:268
        public bool Jump(int nodeId, RestChoice choice = RestChoice.Rest)
        {
            if (!IsAiming || !MapIsLive) return false;
            if (!ExplorationReducer.CanTeleport(State, nodeId)) return false;

            int slot = AimingSlot;
            if (ExplorationReducer.Teleport(State, slot, nodeId, choice).Phase == RunPhase.MiasmaDeath)
            {
                PendingStepNode = nodeId;
                PendingStepChoice = choice;
                PendingJumpSlot = slot;
                return true;
            }
            ApplyJump(slot, nodeId, choice);
            return true;
        }

        /// <summary>
        /// The training card is opened from the band (「訓練の刻み」), so a reload brings it back like the card a rest's
        /// 「訓練する」 opens. False outside trial, with no mark, or while something else waits.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:298
        public bool OpenTraining()
        {
            if (!Trial || State.IsOver || !IsIdle || State.TrainingMarks <= 0) return false;
            TrainingWaiting = true;
            return true;
        }

        /// <summary>
        /// 「あとで」 — the marks stay on the band until the layer is left (the scene asks again before 次の層へ降りる).
        /// False when the card was not waiting.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:308
        public bool PutOffTraining()
        {
            if (!TrainingWaiting) return false;
            TrainingWaiting = false;
            return true;
        }

        /// <summary>The trial cards' buttons (Act's AcceptEvent … ChooseSurvey).</summary>
        // js: bridge/explorationScript/ExplorationSession.js:388
        private bool ActTrial(ScreenAction action, string arg)
        {
            switch (action)
            {
                case ScreenAction.AcceptEvent:
                case ScreenAction.DeclineEvent:
                {
                    if (!HasPendingEvent) return false;
                    var waiting = PendingEvent;
                    PendingEvent = null;
                    ResolveEvent(waiting.NodeId, waiting.Kind, action == ScreenAction.AcceptEvent);
                    return true;
                }

                case ScreenAction.TakeFound:
                {
                    if (!HasPendingFound) return false;
                    bool integer = SlotOf(arg, out int slot);
                    bool full = !State.Loadout.HasFreeSlot;
                    if (full && (!integer || slot < 0 || slot >= State.Loadout.Consumables.Count)) return false;
                    string id = PendingFound[0];
                    PendingFound = PendingFound.Skip(1).ToList();
                    _foundFrom = _foundFrom.Skip(1).ToList();
                    State = ExplorationReducer.TakeConsumable(State, TrialItems.Consumable(id), full ? slot : -1);
                    return true;
                }

                case ScreenAction.LeaveFound:
                {
                    if (!HasPendingFound) return false;
                    string id = PendingFound[0];
                    int from = _foundFrom.Count > 0 ? _foundFrom[0] : None;
                    PendingFound = PendingFound.Skip(1).ToList();
                    _foundFrom = _foundFrom.Skip(1).ToList();
                    // a chest's consumable given up leaves the event's record (X-9: the record says what was kept)
                    if (from != None) TurnDownFind(from, ExplorationText.FoundConsumable(ExplorationText.ItemName(id)));
                    return true;
                }

                case ScreenAction.ChooseSurvey:
                    if (!HasPendingSurvey || arg == null || !SurveyChoices.Contains(arg)) return false;
                    SurveyTarget = arg;
                    SurveyPicked = true;
                    SurveyChoices = NoIds;
                    Keeper?.TakeSurvey(arg);
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        /// <summary>
        /// TakeFound's slot as the browser trial reads it (<c>Number(arg)</c>, then <c>Number.isInteger</c>): null and
        /// a blank string read as 0, as <c>Number(null)</c> and <c>Number('')</c> do (a JS quirk, ported as is).
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:399
        private static bool SlotOf(string arg, out int slot)
        {
            slot = 0;
            double value = JsNumber(arg);
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Floor(value) != value) return false;
            if (value < int.MinValue || value > int.MaxValue) return false;
            slot = (int)value;
            return true;
        }

        // StrDecimalLiteral of ECMA-262 StringToNumber, ASCII digits only (.NET's \d would take other scripts' digits).
        private static readonly Regex JsDecimal =
            new Regex(@"^[+-]?(?:Infinity|(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)$", RegexOptions.CultureInvariant);

        /// <summary>
        /// JS <c>Number(string)</c> (ECMA-262 StringToNumber): the JS white space trimmed, empty is 0, the
        /// <c>0x</c> / <c>0o</c> / <c>0b</c> integers, a decimal literal, and NaN for anything else. Null is 0, as
        /// <c>Number(null)</c> is.
        /// </summary>
        private static double JsNumber(string arg)
        {
            if (arg == null) return 0;
            int start = 0;
            int end = arg.Length;
            while (start < end && IsJsSpace(arg[start])) start++;
            while (end > start && IsJsSpace(arg[end - 1])) end--;
            if (start == end) return 0;
            string s = arg.Substring(start, end - start);
            if (s.Length > 2 && s[0] == '0')
            {
                int radix = s[1] switch
                {
                    'x' or 'X' => 16,
                    'o' or 'O' => 8,
                    'b' or 'B' => 2,
                    _ => 0,
                };
                if (radix != 0) return RadixValue(s, 2, radix);
            }
            if (!JsDecimal.IsMatch(s)) return double.NaN;
            if (s.EndsWith("Infinity", StringComparison.Ordinal)) return s[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
            return double.Parse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture);
        }

        private static double RadixValue(string s, int from, int radix)
        {
            double value = 0;
            for (int i = from; i < s.Length; i++)
            {
                char c = s[i];
                int digit = c >= '0' && c <= '9' ? c - '0'
                    : c >= 'a' && c <= 'z' ? c - 'a' + 10
                    : c >= 'A' && c <= 'Z' ? c - 'A' + 10
                    : 99;
                if (digit >= radix) return double.NaN;
                value = value * radix + digit;
            }
            return value;
        }

        // JS WhiteSpace and LineTerminator (ECMA-262 StrWhiteSpaceChar): not .NET's char.IsWhiteSpace, which also
        // takes U+0085 and leaves U+FEFF.
        private static bool IsJsSpace(char c) =>
            c == '\t' || c == '\v' || c == '\f' || c == ' ' || c == '\u00A0' || c == '\uFEFF' ||
            c == '\n' || c == '\r' || c == '\u2028' || c == '\u2029' ||
            CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;

        /// <summary>
        /// A layer's map. RunStart.MapFor(profile, seed), when the game sets one, draws it instead of
        /// profile.Map(seed) — the lineage's trace node and the known carving (Trial.Bridge's Life,
        /// Inheritance.MapSpecFor). Without it this is exactly the C#.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:503
        public LayerMap MapOf(LayerProfile profile, ulong seed) =>
            _start.MapFor != null ? _start.MapFor(profile, seed) : profile.Map(seed);

        /// <summary>
        /// The seed an イベント node's kind is drawn from — the node's encounter seed, salted so it does not replay the
        /// encounter draw (the same rule as Expedition.RewardSeed).
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:524
        public static ulong EventSeed(ulong runSeed, int layer, int nodeId) =>
            EncounterSeed(runSeed, layer, nodeId) ^ 0x9E6C63D0676A9A99UL;

        /// <summary>
        /// The enemies a layer's table can field (LayerEncounters, normal → elite → boss, each once) — 「対象はこの階層の
        /// 敵 1 種」 (dungeon_exploration_v4.md §2.1), the list 写しの帳 picks from.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:532
        public static IReadOnlyList<string> LayerEnemies(int layer)
        {
            var table = LayerEncounters.Of(layer);
            var ids = new List<string>();
            foreach (var pool in new[] { table.Normal, table.Elite, table.Boss })
            {
                foreach (var encounter in pool)
                {
                    foreach (string id in encounter.EnemyIds)
                    {
                        if (!ids.Contains(id)) ids.Add(id);
                    }
                }
            }
            return ids;
        }

        /// <summary>
        /// Which event an イベント node of this layer holds (LayerEvents.Draw), or null for any other node. 「中身は隣接
        /// したときに見えます」 — the screen shows it once the node is next door.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:545
        public EventKind? EventAt(int nodeId)
        {
            var map = State.Map;
            if (!ReferenceEquals(_eventMap, map))
            {
                int layer = State.Profile.Layer;
                ulong runSeed = RunSeed;
                _eventKinds = LayerEvents.Draw(map, layer, id => EventSeed(runSeed, layer, id));
                _eventMap = map;
            }
            return _eventKinds.TryGetValue(nodeId, out var kind) ? kind : (EventKind?)null;
        }

        /// <summary>
        /// Consumables found on the way (a battle's reward, a chest) go into the free slots; what does not fit waits in
        /// PendingFound for a swap or 「諦める」 (tools_and_consumables_v4.md §3, encounters_and_rewards_v4.md §4.3).
        /// Nothing is taken once the life is over. Called by the Expedition (and by the session for a chest).
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:561
        public void Receive(IEnumerable<string> consumableIds, int fromEvent = None)
        {
            if (!Trial) return;
            foreach (string id in consumableIds ?? NoIds)
            {
                if (State.IsOver) return;
                if (State.Loadout.HasFreeSlot && !HasPendingFound)
                {
                    State = ExplorationReducer.TakeConsumable(State, TrialItems.Consumable(id));
                }
                else
                {
                    PendingFound = PendingFound.Concat(new[] { id }).ToList();
                    _foundFrom = _foundFrom.Concat(new[] { fromEvent }).ToList();
                }
            }
        }

        /// <summary>
        /// A chest's find was turned down after all — its card not learned (Expedition.CloseResult) or its consumable
        /// given up (LeaveFound) — so the event's record drops that row: the walked node's 「結果」 and the journal's
        /// 「出来事」 say what the heir kept (docs/EXTENSIONS.md X-9, W-17). The result card's own lines (what the chest
        /// held) stay. Called by the session and the expedition on themselves, never by the scene.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:580
        public bool TurnDownFind(int eventIndex, string line)
        {
            if (eventIndex < 0 || eventIndex >= Events.Count) return false;
            var e = Events[eventIndex];
            int at = -1;
            for (int i = 0; i < e.Record.Count; i++)
            {
                if (string.Equals(e.Record[i], line, StringComparison.Ordinal))
                {
                    at = i;
                    break;
                }
            }
            if (at < 0) return false;
            var record = e.Record.Where((_, i) => i != at).ToList();
            var next = new EventOutcome(e.Layer, e.NodeId, e.Kind, e.Name, e.Accepted, e.Lines, e.Cards, e.Consumables, e.Pages, record);
            Events = Events.Select((x, i) => i == eventIndex ? next : x).ToList();
            return true;
        }

        /// <summary>The jump itself (Jump, or ConfirmStep after the 瘴気死 warning).</summary>
        // js: bridge/explorationScript/ExplorationSession.js:592
        private void ApplyJump(int slot, int nodeId, RestChoice choice)
        {
            AimingSlot = None;
            PendingJumpSlot = None;
            int marks = State.TrainingMarks;
            State = ExplorationReducer.Teleport(State, slot, nodeId, choice);
            if (Trial && State.TrainingMarks > marks) TrainingWaiting = true; // the training card opens
            Arrived(nodeId);
        }

        /// <summary>An event happens — the core's effect, a chest's finds through the Keeper, the record.</summary>
        // js: bridge/explorationScript/ExplorationSession.js:602
        private void ResolveEvent(int nodeId, EventKind kind, bool accept)
        {
            var before = State;
            int layer = before.Profile.Layer;
            State = ExplorationReducer.ResolveEvent(before, kind, accept);
            var after = State;

            var found = ChestFind.None;
            if (accept && kind == EventKind.HeavyChest && Keeper != null)
                found = Keeper.TakeChest(layer, nodeId, LayerEvents.ChestContents(layer));

            // The result card's lines, and the shorter record the journal's dungeon page keeps.
            var lines = new List<string>();
            var record = new List<string>();
            if (!accept)
            {
                lines.Add(ExplorationText.EventDeclined);
                record.Add(ExplorationText.EventDeclined);
            }
            else
            {
                if (after.Hp != before.Hp || kind == EventKind.Spring)
                {
                    lines.Add(ExplorationText.HpChange(before.Hp, after.Hp));
                    record.Add(ExplorationText.HpChange(before.Hp, after.Hp));
                }
                if (kind != EventKind.Spring)
                {
                    lines.Add(ExplorationText.MaxStaminaChange(before.MaxStamina, after.MaxStamina));
                    record.Add(ExplorationText.MaxStaminaChange(before.MaxStamina, after.MaxStamina));
                    if (after.Stamina != before.Stamina) lines.Add(ExplorationText.StaminaChange(before.Stamina, after.Stamina));
                    if (after.MaxStamina == before.MaxStamina) lines.Add(ExplorationText.TempClampNote);
                }
                if (found.Cards.Count > 0)
                {
                    lines.Add(ExplorationText.FoundCards(found.Cards.Count));
                    record.Add(ExplorationText.RecordCards(1));
                }
                if (found.Pages > 0)
                {
                    lines.Add(ExplorationText.FoundPages(found.Pages));
                    record.Add(ExplorationText.FoundPages(found.Pages));
                }
                foreach (string id in found.Consumables)
                {
                    lines.Add(ExplorationText.FoundConsumable(ExplorationText.ItemName(id)));
                    record.Add(ExplorationText.FoundConsumable(ExplorationText.ItemName(id)));
                }
                if (found.PileSpent) lines.Add(ExplorationText.PileSpent);
            }
            Events = Events.Concat(new[]
            {
                new EventOutcome(layer, nodeId, kind, ExplorationText.EventName(kind), accept,
                    lines, found.Cards, found.Consumables, found.Pages, record),
            }).ToList();
            Receive(found.Consumables, Events.Count - 1);
            EventState = State;
        }

        /// <summary>
        /// Whether the life starts knowing the carving (docs/INHERITANCE.md R-20, seven_layers_v4.md §5: 「生をまたぐ。
        /// 一度拾ったら次の生でも 竜神の秘跡 と表示し、ノードは出ない」). RunStart.CarvingKnown says so when the game passes
        /// it. Otherwise the lineage's map seam does: a layer whose spec holds a carving but whose map (RunStart.MapFor)
        /// holds none had it taken out because the name is known. False outside trial and without a seam: the C#'s
        /// carving is read again each life.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:658
        public bool CarvingKnownAt(ulong seed)
        {
            if (!Trial) return false;
            if (_start.CarvingKnown.HasValue) return _start.CarvingKnown.Value;
            if (_start.MapFor == null) return false;
            for (int layer = 1; layer <= SevenLayers.Count; layer++)
            {
                var profile = SevenLayers.Of(layer);
                if (!(profile.MapSpec.Quotas.TryGetValue(NodeKind.Carving, out int quota) && quota > 0)) continue;
                if (MapOf(profile, LayerSeed(seed, layer)).CountOf(NodeKind.Carving) == 0) return true;
            }
            return false;
        }

        /// <summary>Begin's trial part: a new life starts with nothing waiting and no events behind it.</summary>
        // js: bridge/explorationScript/ExplorationSession.js:681
        private void BeginTrial()
        {
            PendingEvent = null;
            Events = new EventOutcome[0];
            PendingFound = NoIds;
            SurveyChoices = NoIds;
            SurveyTarget = null;
            SurveyPicked = false;
            AimingSlot = None;
            PendingJumpSlot = None;
            EventState = null;
            InterludeState = null;
            TrainingWaiting = false;
            _foundFrom = new int[0];
            _eventMap = null;
            _eventKinds = null;
        }

        /// <summary>Descended's trial part: the survey's target is per layer, like Surveyed; the new layer starts with no mark.</summary>
        // js: bridge/explorationScript/ExplorationSession.js:716
        private void DescendedTrial()
        {
            SurveyTarget = null;
            SurveyPicked = false;
            SurveyChoices = NoIds;
            AimingSlot = None;
            TrainingWaiting = false; // the new layer starts with no mark (ExplorationReducer.Enter)
        }

        /// <summary>
        /// Arrived's trial part: an event happens (泉) or waits on 受ける / 受けない; 写しの帳 asks for the survey's
        /// target, or the intel names it (ISurveyIntel) and the Keeper raises it. Like a fight, nothing happens on the
        /// node that ended the life by 瘴気.
        /// </summary>
        // js: bridge/explorationScript/ExplorationSession.js:745
        private void ArrivedTrial(int nodeId, NodeKind kind)
        {
            if (!Trial || State.Phase == RunPhase.MiasmaDeath) return;
            if (kind == NodeKind.Event)
            {
                var happening = EventAt(nodeId);
                if (happening != null && LayerEvents.HasChoice(happening.Value)) PendingEvent = new PendingEventInfo(nodeId, happening.Value);
                else if (happening != null) ResolveEvent(nodeId, happening.Value, true);
            }
            if (kind == NodeKind.Survey && State.Loadout.ChoosesSurvey)
            {
                var keeper = Keeper;
                SurveyChoices = LayerEnemies(State.Profile.Layer)
                    .Where(id => keeper == null || keeper.DisclosureOf(id) < 2)
                    .ToList();
            }
            else if (kind == NodeKind.Survey && Intel is ISurveyIntel survey)
            {
                // 「既定は階層が決め」 (tools_and_consumables_v4.md §2.2): the intel names the layer's target and the
                // journal is raised to 2 (dungeon_exploration_v4.md §2.1 「手記の開示度を 2 段目へ上げる」), so the battles'
                // omens read it too.
                string target = survey.SurveyTarget(State.Profile.Layer, State.Map);
                if (target != null)
                {
                    SurveyTarget = target;
                    Keeper?.TakeSurvey(target);
                }
            }
        }
    }
}
