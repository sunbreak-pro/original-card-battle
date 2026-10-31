// Holds one run of the exploration screen: the core's state, the few flags the screen needs on top
// of it (the carving, the survey, the battle waiting on a combat node and who is fought there, the
// confirmations, what the top band has open), and the inputs the View forwards. Every rule is the core's: this class only chooses which ExplorationReducer call a click
// becomes, and refuses clicks the screen should not have offered.
using System;
using System.Linq;
using DungeonContent;
using DungeonCore;

namespace Exploration
{
    /// <summary>How a run starts. The exploration screen's own stand-ins until 出立 (#104) hands a real one in.</summary>
    public sealed class RunStart
    {
        /// <summary>The battle core's PLAYER_MAX_HP (BattleCore.Constants). #99 hands it to every battle as the maximum.</summary>
        public const int DefaultMaxHp = 50;

        public ulong Seed { get; set; } = 1UL;

        public int MaxHp { get; set; } = DefaultMaxHp;

        public int Hp { get; set; } = DefaultMaxHp;

        public int Stamina { get; set; } = Miasma.BaseMaxStamina;

        /// <summary>The gauge the life starts at. 0 for a fresh life; higher to try the deep end of the gauge.</summary>
        public int MiasmaPercent { get; set; }

        public RunLoadout Loadout { get; set; } = RunLoadout.Empty;

        /// <summary>The traces earlier lives left and nobody collected (#106). Empty for a first life; a save (#109) hands its own in.</summary>
        public TraceLedger Traces { get; set; } = TraceLedger.Empty;

        /// <summary>
        /// A loadout for trying the screen: 防瘴の面 and two 浄化の香 — the carried items whose
        /// effect the exploration core already honours. Amounts come from the catalogue.
        /// </summary>
        public static RunLoadout TrialLoadout()
        {
            var mask = ItemCatalogue.Tool("bosho_no_men");
            return new RunLoadout(
                new[] { ReliefItem.Density(mask.Id, mask.Amount) },
                new[] { Consumable("joka_no_ko"), Consumable("joka_no_ko") });
        }

        /// <summary>
        /// A catalogue consumable (tools_and_consumables_v4.md §3) as the exploration core carries it:
        /// the effect and its amount, from the catalogue.
        /// </summary>
        public static ReliefItem Consumable(string id)
        {
            var item = ItemCatalogue.Consumable(id);
            return item.Effect switch
            {
                ItemEffect.MiasmaGauge => ReliefItem.Gauge(item.Id, item.Amount),
                ItemEffect.HealHp => ReliefItem.HealHp(item.Id, item.Amount),
                ItemEffect.MaxStamina => ReliefItem.MaxStamina(item.Id, item.Amount),
                ItemEffect.RefillStamina => ReliefItem.RefillStamina(item.Id),
                ItemEffect.Teleport => ReliefItem.Teleport(item.Id),
                _ => throw new ArgumentOutOfRangeException(nameof(id), id, $"the exploration core has no slot effect for {item.Effect}"),
            };
        }
    }

    public sealed class ExplorationSession
    {
        private const int None = -1;

        private readonly RunStart _start;

        // The layers whose 情報収集 node this life has resolved (#107: a survey's 開示度 lasts the life).
        private readonly System.Collections.Generic.HashSet<int> _surveyedLayers = new System.Collections.Generic.HashSet<int>();

        public ExplorationSession(RunStart start = null, IEnemyIntel intel = null)
        {
            _start = start ?? new RunStart();
            Intel = intel ?? new NoEnemyIntel();
            Traces = _start.Traces ?? TraceLedger.Empty;
            Begin(_start.Seed);
        }

        public IEnemyIntel Intel { get; }

        /// <summary>
        /// The traces left by every life of this session and the ones handed in at the start, and what the
        /// life now in the lair has done about them (#106). It outlives 「新しい生」.
        /// </summary>
        public TraceLedger Traces { get; private set; }

        /// <summary>Who says what a dying life leaves (Exploration.Bridge.Expedition). Null: a trace with no candidates.</summary>
        public ILegacySource LegacySource { get; set; }

        /// <summary>The trace this life stepped on first and has not chosen from yet, or null (#106).</summary>
        public DeathTrace PendingTrace => Traces.Pending;

        // A life leaves one trace: the death is settled once, on the state that ended it.
        private bool _traceLeft;

        /// <summary>The seed this life was started from. Each layer's map is drawn from it.</summary>
        public ulong RunSeed { get; private set; }

        /// <summary>How many lives this session has started, counting the first.</summary>
        public int Life { get; private set; }

        public ExplorationState State { get; private set; }

        /// <summary>The layer-two carving has been read, so that layer shows its true name.</summary>
        public bool CarvingTaken { get; private set; }

        /// <summary>This layer's 情報収集 node has been resolved.</summary>
        public bool Surveyed { get; private set; }

        /// <summary>
        /// That layer's 情報収集 node has been resolved in this life. Unlike <see cref="Surveyed"/>, it stays
        /// true after the descent: the journal keeps what the survey taught for the rest of the life (#107).
        /// </summary>
        public bool HasSurveyed(int layer) => _surveyedLayers.Contains(layer);

        /// <summary>The combat node waiting on its battle's result, or −1.</summary>
        public int PendingBattleNode { get; private set; } = None;

        /// <summary>
        /// Who is fought on <see cref="PendingBattleNode"/> (encounters_and_rewards_v4.md §2), drawn
        /// from the layer's table as the node is reached; null while no battle waits. #99 hands it to
        /// the battle.
        /// </summary>
        public Encounter PendingEncounter { get; private set; }

        /// <summary>The node whose step waits on a confirmation because it ends the life, or −1.</summary>
        public int PendingStepNode { get; private set; } = None;

        public RestChoice PendingStepChoice { get; private set; }

        public bool ConfirmingEndLife { get; private set; }

        /// <summary>A descent waits on a confirmation: the next layer's entry would end the life by 瘴気.</summary>
        public bool ConfirmingDescend { get; private set; }

        /// <summary>HP the last 階層間の休憩 actually gave back, after the maximum clamped it.</summary>
        public int InterludeHealed { get; private set; }

        public bool HasPendingBattle => PendingBattleNode != None;

        public bool HasPendingStep => PendingStepNode != None;

        /// <summary>Nothing waits on the player but the map.</summary>
        public bool IsIdle => !HasPendingBattle && !HasPendingStep && !ConfirmingEndLife && !ConfirmingDescend;

        /// <summary>
        /// The map takes steps: the layer is being explored, and neither a card nor a screen opened
        /// from the band covers it. The journal drawer sits beside the map and leaves it live.
        /// </summary>
        public bool MapIsLive => State.Phase == RunPhase.Exploring && IsIdle && !CoversMap;

        /// <summary>What the top band has open over the screen (dungeon_exploration_v4.md §6.2).</summary>
        public Overlay OpenOverlay { get; private set; }

        private bool CoversMap => OpenOverlay == Overlay.Cards || OpenOverlay == Overlay.Settings;

        public bool CanEndLife => !State.IsOver && IsIdle;

        public bool CanUseConsumables => !State.IsOver && IsIdle;

        /// <summary>
        /// The item in this slot can be used now. 標の石 never can yet: the screen that picks the node
        /// to jump to does not exist.
        /// </summary>
        public bool CanUseConsumable(int index) => CanUseConsumables && ExplorationReducer.CanUseConsumable(State, index);

        /// <summary>This frame of the screen.</summary>
        public ExplorationScreen Screen() => ExplorationScreenBuilder.Build(this);

        /// <summary>
        /// Walks to a neighbouring node. A step that would end the life by 瘴気 waits for
        /// ScreenAction.ConfirmStep instead of being taken. False when the screen should not
        /// have offered the step.
        /// </summary>
        public bool Step(int nodeId, RestChoice choice = RestChoice.Rest)
        {
            if (!MapIsLive) return false;
            if (!ExplorationReducer.CanStep(State, nodeId)) return false;

            bool fresh = !State.HasResolved(nodeId);
            if (fresh &&ExplorationReducer.Step(State, nodeId, choice).Phase == RunPhase.MiasmaDeath)
            {
                PendingStepNode = nodeId;
                PendingStepChoice = choice;
                return true;
            }

            Apply(nodeId, choice);
            return true;
        }

        /// <summary>Burns the consumable in this slot. Costs no 刻限. False for an empty slot and for 標の石.</summary>
        public bool UseConsumable(int index)
        {
            if (!CanUseConsumable(index)) return false;

            State = ExplorationReducer.UseConsumable(State, index);
            return true;
        }

        /// <summary>
        /// Puts a consumable found on the way into the slots (ExplorationReducer.TakeConsumable): a free
        /// slot, or the one named in <paramref name="replaceSlot"/> when every slot is full. A full
        /// loadout with no slot named throws. False once the life is over.
        /// </summary>
        public bool TakeConsumable(ReliefItem item, int replaceSlot = -1)
        {
            if (State.IsOver) return false;

            State = ExplorationReducer.TakeConsumable(State, item, replaceSlot);
            return true;
        }

        /// <summary>
        /// Spends one 習熟訓練 mark (#625). The card it goes to is named outside the exploration
        /// (Exploration.Bridge.Expedition.Train). False when no mark is left or something else waits.
        /// </summary>
        public bool SpendTrainingMark()
        {
            if (State.IsOver || !IsIdle || State.TrainingMarks <= 0) return false;
            State = ExplorationReducer.SpendTrainingMark(State);
            return true;
        }

        /// <summary>
        /// Chooses from <see cref="PendingTrace"/> (#106): the trace is collected, and what it hands on
        /// comes back for the layer that holds the cards and the journal to take in. Null when no trace
        /// waits, the life is over, or the choice breaks the rules (DungeonCore.Heritage.CanChoose).
        /// </summary>
        public Bequest TakeLegacy(System.Collections.Generic.IReadOnlyCollection<string> keys)
        {
            DeathTrace trace = PendingTrace;
            if (trace == null || State.IsOver || !Heritage.CanChoose(trace, keys)) return null;
            Traces = Traces.Collect(keys, out Bequest bequest);
            return bequest;
        }

        /// <summary>A button on a card, or 「この生を終える」. False when the action does not apply now.</summary>
        public bool Act(ScreenAction action)
        {
            switch (action)
            {
                case ScreenAction.WinBattle:
                    return ReportBattle(State.Hp, State.Stamina);

                case ScreenAction.FallInBattle:
                    return ReportBattle(0, State.Stamina);

                case ScreenAction.GoToInterlude:
                    if (!IsIdle) return false;
                    if (!ExplorationReducer.CanInterlude(State)) return false;
                    int hpBefore = State.Hp;
                    State = ExplorationReducer.Interlude(State);
                    InterludeHealed = State.Hp - hpBefore;
                    return true;

                case ScreenAction.Descend:
                    if (!IsIdle) return false;
                    var below = TryDescend();
                    if (below == null) return false;
                    // Like a step, a descent that ends the life asks first (dungeon_exploration_v4.md §6.5).
                    if (below.Phase == RunPhase.MiasmaDeath)
                    {
                        ConfirmingDescend = true;
                        return true;
                    }
                    Descended(below);
                    return true;

                case ScreenAction.ConfirmDescend:
                    if (!ConfirmingDescend) return false;
                    ConfirmingDescend = false;
                    Descended(TryDescend());
                    return true;

                case ScreenAction.AskEndLife:
                    if (!CanEndLife) return false;
                    ConfirmingEndLife = true;
                    return true;

                case ScreenAction.ConfirmEndLife:
                    if (!ConfirmingEndLife) return false;
                    ConfirmingEndLife = false;
                    State = ExplorationReducer.EndLife(State);
                    return true;

                case ScreenAction.ConfirmStep:
                    if (!HasPendingStep) return false;
                    int node = PendingStepNode;
                    PendingStepNode = None;
                    Apply(node, PendingStepChoice);
                    return true;

                case ScreenAction.Cancel:
                    if (!ConfirmingEndLife && !HasPendingStep && !ConfirmingDescend) return false;
                    ConfirmingEndLife = false;
                    ConfirmingDescend = false;
                    PendingStepNode = None;
                    return true;

                case ScreenAction.NewLife:
                    if (!State.IsOver || !IsIdle) return false;
                    Begin(RunSeed + 1UL);
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        /// <summary>
        /// The battle on the pending node is over (#99): HP and current stamina come back through the
        /// core's ReportBattle, and HP 0 is a fall. The stand-in card's two buttons come here as well.
        /// False when no battle waits.
        /// </summary>
        public bool ReportBattle(int hpAfter, int staminaAfter)
        {
            if (!HasPendingBattle) return false;
            State = ExplorationReducer.ReportBattle(State, hpAfter, staminaAfter);
            PendingBattleNode = None;
            PendingEncounter = null;
            LeaveTraceIfDead();
            return true;
        }

        /// <summary>
        /// 所持カード and 設定・セーブ stay shut while the stand-in battle waits, which counts as being in
        /// battle (§6.2): a battle plays its deck only, and its state is not part of a save. The journal
        /// opens everywhere.
        /// </summary>
        public bool CanOpen(Overlay overlay) =>
            overlay == Overlay.Journal || (overlay != Overlay.None && !HasPendingBattle);

        /// <summary>
        /// A band button, J or C: opens that screen in place of whichever was open, or closes it when it
        /// is the one open. False when it may not open now.
        /// </summary>
        public bool Toggle(Overlay overlay)
        {
            if (overlay == Overlay.None) return false;
            if (OpenOverlay == overlay)
            {
                OpenOverlay = Overlay.None;
                return true;
            }
            if (!CanOpen(overlay)) return false;
            OpenOverlay = overlay;
            return true;
        }

        /// <summary>
        /// Esc (§6.2): an open confirmation is answered 「やめる」 first; failing that, the journal or
        /// 所持カード closes; only when neither is open does 設定・セーブ open or close.
        /// </summary>
        public bool Escape()
        {
            if (HasPendingStep || ConfirmingDescend || ConfirmingEndLife) return Act(ScreenAction.Cancel);
            if (OpenOverlay == Overlay.Journal || OpenOverlay == Overlay.Cards)
            {
                OpenOverlay = Overlay.None;
                return true;
            }
            return Toggle(Overlay.Settings);
        }

        /// <summary>
        /// The state the next layer's entry would leave, tried without taking it: the core's own
        /// Descend on the immutable state. Null outside the interlude and below the last layer.
        /// </summary>
        public ExplorationState TryDescend()
        {
            if (State.Phase != RunPhase.Interlude || State.Profile.IsLast) return null;
            var next = SevenLayers.Of(State.Profile.Layer + 1);
            return ExplorationReducer.Descend(State, next, Traces.MapFor(next, LayerSeed(RunSeed, next.Layer)));
        }

        /// <summary>A layer's map seed, drawn from the life's seed so a life replays from one number.</summary>
        public static ulong LayerSeed(ulong runSeed, int layer) =>
            unchecked(runSeed * 0x9E3779B97F4A7C15UL + (ulong)layer);

        /// <summary>
        /// The seed a combat node's encounter is drawn from: the layer's map seed and the node, so the
        /// same life meets the same enemies on the same nodes.
        /// </summary>
        public static ulong EncounterSeed(ulong runSeed, int layer, int nodeId) =>
            unchecked(LayerSeed(runSeed, layer) * 0xBF58476D1CE4E5B9UL + (ulong)(nodeId + 1));

        private void Begin(ulong seed)
        {
            RunSeed = seed;
            Life += 1;
            CarvingTaken = false;
            Surveyed = false;
            _surveyedLayers.Clear();
            PendingBattleNode = None;
            PendingEncounter = null;
            PendingStepNode = None;
            ConfirmingEndLife = false;
            ConfirmingDescend = false;
            InterludeHealed = 0;
            Traces = Traces.BeginLife();
            _traceLeft = false;

            var first = SevenLayers.Of(1);
            State = ExplorationReducer.Enter(
                first,
                Traces.MapFor(first, LayerSeed(seed, first.Layer)),
                _start.Loadout ?? RunLoadout.Empty,
                _start.Hp,
                _start.MaxHp,
                _start.Stamina,
                _start.MiasmaPercent);

            // The entry node is resolved on arrival, and it is always a battle (dungeon_exploration_v4.md §4.1).
            Arrived(State.CurrentNodeId);
            LeaveTraceIfDead();
        }

        private void Descended(ExplorationState below)
        {
            State = below;
            Surveyed = false;
            Arrived(State.CurrentNodeId);
            LeaveTraceIfDead();
        }

        private void Apply(int nodeId, RestChoice choice)
        {
            bool fresh = !State.HasResolved(nodeId);
            State = ExplorationReducer.Step(State, nodeId, choice);
            if (fresh) Arrived(nodeId);
            LeaveTraceIfDead();
        }

        /// <summary>
        /// Settles a death the moment the state reaches one, by either cause (#106): the trace goes into
        /// the ledger with what <see cref="LegacySource"/> says the life leaves. Once per life.
        /// </summary>
        private void LeaveTraceIfDead()
        {
            if (_traceLeft || !Heritage.IsDeath(State)) return;
            _traceLeft = true;
            ILegacySource source = LegacySource;
            Traces = Traces.Leave(State, Life, source?.Name, source?.Cards, source?.Pages);
        }

        /// <summary>What resolving a node means for the screen, on top of what the core already did.</summary>
        private void Arrived(int nodeId)
        {
            var kind = State.Map.Node(nodeId).Kind;
            if (kind == NodeKind.Carving) CarvingTaken = true;
            if (kind == NodeKind.Survey)
            {
                Surveyed = true;
                _surveyedLayers.Add(State.Profile.Layer);
            }
            // The first trace a life steps on is the one it receives from (#106). A death on the step claims nothing.
            if (kind == NodeKind.Trace && !State.IsOver) Traces = Traces.Visit(State.Profile.Layer);
            // 瘴気死 ends the life on the node before any fight starts.
            if (kind.IsCombat() && State.Phase != RunPhase.MiasmaDeath)
            {
                PendingBattleNode = nodeId;
                int layer = State.Profile.Layer;
                PendingEncounter = LayerEncounters.Pick(kind, layer, new SeededRng(EncounterSeed(RunSeed, layer, nodeId)));
            }
        }
    }
}
