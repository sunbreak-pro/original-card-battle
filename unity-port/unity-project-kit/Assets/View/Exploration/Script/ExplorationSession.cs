// Holds one run of the exploration screen: the core's state, the few flags the screen needs on top
// of it (the carving, the survey, the stand-in battle, the confirmations), and the inputs the View
// forwards. Every rule is the core's: this class only chooses which ExplorationReducer call a click
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
        /// <summary>The battle core's PLAYER_MAX_HP (BattleCore.Constants). #99 hands the real one across.</summary>
        public const int DefaultMaxHp = 50;

        public ulong Seed { get; set; } = 1UL;

        public int MaxHp { get; set; } = DefaultMaxHp;

        public int Hp { get; set; } = DefaultMaxHp;

        public int Stamina { get; set; } = Miasma.BaseMaxStamina;

        /// <summary>The gauge the life starts at. 0 for a fresh life; higher to try the deep end of the gauge.</summary>
        public int MiasmaPercent { get; set; }

        public RunLoadout Loadout { get; set; } = RunLoadout.Empty;

        /// <summary>
        /// A loadout for trying the screen: 防瘴の面 and two 浄化の香 — the carried items whose
        /// effect the exploration core already honours. Amounts come from the catalogue.
        /// </summary>
        public static RunLoadout TrialLoadout()
        {
            var mask = ItemCatalogue.Tool("bosho_no_men");
            var incense = ItemCatalogue.Consumable("joka_no_ko");
            return new RunLoadout(
                new[] { ReliefItem.Density(mask.Id, mask.Amount) },
                new[] { ReliefItem.Gauge(incense.Id, incense.Amount), ReliefItem.Gauge(incense.Id, incense.Amount) });
        }
    }

    public sealed class ExplorationSession
    {
        private const int None = -1;

        private readonly RunStart _start;

        public ExplorationSession(RunStart start = null, IEnemyIntel intel = null)
        {
            _start = start ?? new RunStart();
            Intel = intel ?? new NoEnemyIntel();
            Begin(_start.Seed);
        }

        public IEnemyIntel Intel { get; }

        /// <summary>The seed this life was started from. Each layer's map is drawn from it.</summary>
        public ulong RunSeed { get; private set; }

        /// <summary>How many lives this session has started, counting the first.</summary>
        public int Life { get; private set; }

        public ExplorationState State { get; private set; }

        /// <summary>The layer-two carving has been read, so that layer shows its true name.</summary>
        public bool CarvingTaken { get; private set; }

        /// <summary>This layer's 情報収集 node has been resolved.</summary>
        public bool Surveyed { get; private set; }

        /// <summary>The combat node waiting on the stand-in battle's result, or −1.</summary>
        public int PendingBattleNode { get; private set; } = None;

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

        /// <summary>The map takes steps: the layer is being explored and no card covers it.</summary>
        public bool MapIsLive => State.Phase == RunPhase.Exploring && IsIdle;

        public bool CanEndLife => !State.IsOver && IsIdle;

        public bool CanUseConsumables => !State.IsOver && IsIdle;

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
            if (!State.Map.Neighbours(State.CurrentNodeId).Contains(nodeId)) return false;

            bool fresh = !State.HasResolved(nodeId);
            if (fresh && State.TimeLeft <= 0) return false;

            if (fresh && ExplorationReducer.Step(State, nodeId, choice).Phase == RunPhase.MiasmaDeath)
            {
                PendingStepNode = nodeId;
                PendingStepChoice = choice;
                return true;
            }

            Apply(nodeId, choice);
            return true;
        }

        /// <summary>Burns the consumable in this slot. Costs no 刻限.</summary>
        public bool UseConsumable(int index)
        {
            if (!CanUseConsumables) return false;
            if (index < 0 || index >= State.Loadout.Consumables.Count) return false;

            State = ExplorationReducer.UseConsumable(State, index);
            return true;
        }

        /// <summary>A button on a card, or 「この生を終える」. False when the action does not apply now.</summary>
        public bool Act(ScreenAction action)
        {
            switch (action)
            {
                case ScreenAction.WinBattle:
                    if (!HasPendingBattle) return false;
                    State = ExplorationReducer.ReportBattle(State, State.Hp, State.Stamina);
                    PendingBattleNode = None;
                    return true;

                case ScreenAction.FallInBattle:
                    if (!HasPendingBattle) return false;
                    State = ExplorationReducer.ReportBattle(State, 0, State.Stamina);
                    PendingBattleNode = None;
                    return true;

                case ScreenAction.GoToInterlude:
                    if (!IsIdle) return false;
                    if (State.Phase != RunPhase.LayerCleared && State.Phase != RunPhase.PushedOut) return false;
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
        /// The state the next layer's entry would leave, tried without taking it: the core's own
        /// Descend on the immutable state. Null outside the interlude and below the last layer.
        /// </summary>
        public ExplorationState TryDescend()
        {
            if (State.Phase != RunPhase.Interlude || State.Profile.IsLast) return null;
            var next = SevenLayers.Of(State.Profile.Layer + 1);
            return ExplorationReducer.Descend(State, next, next.Map(LayerSeed(RunSeed, next.Layer)));
        }

        /// <summary>A layer's map seed, drawn from the life's seed so a life replays from one number.</summary>
        public static ulong LayerSeed(ulong runSeed, int layer) =>
            unchecked(runSeed * 0x9E3779B97F4A7C15UL + (ulong)layer);

        private void Begin(ulong seed)
        {
            RunSeed = seed;
            Life += 1;
            CarvingTaken = false;
            Surveyed = false;
            PendingBattleNode = None;
            PendingStepNode = None;
            ConfirmingEndLife = false;
            ConfirmingDescend = false;
            InterludeHealed = 0;

            var first = SevenLayers.Of(1);
            State = ExplorationReducer.Enter(
                first,
                first.Map(LayerSeed(seed, first.Layer)),
                _start.Loadout ?? RunLoadout.Empty,
                _start.Hp,
                _start.MaxHp,
                _start.Stamina,
                _start.MiasmaPercent);

            // The entry node is resolved on arrival, and it is always a battle (dungeon_exploration_v4.md §4.1).
            Arrived(State.CurrentNodeId);
        }

        private void Descended(ExplorationState below)
        {
            State = below;
            Surveyed = false;
            Arrived(State.CurrentNodeId);
        }

        private void Apply(int nodeId, RestChoice choice)
        {
            bool fresh = !State.HasResolved(nodeId);
            State = ExplorationReducer.Step(State, nodeId, choice);
            if (fresh) Arrived(nodeId);
        }

        /// <summary>What resolving a node means for the screen, on top of what the core already did.</summary>
        private void Arrived(int nodeId)
        {
            var kind = State.Map.Node(nodeId).Kind;
            if (kind == NodeKind.Carving) CarvingTaken = true;
            if (kind == NodeKind.Survey) Surveyed = true;
            // 瘴気死 ends the life on the node before any fight starts.
            if (ExplorationText.IsCombat(kind) && State.Phase != RunPhase.MiasmaDeath) PendingBattleNode = nodeId;
        }
    }
}
