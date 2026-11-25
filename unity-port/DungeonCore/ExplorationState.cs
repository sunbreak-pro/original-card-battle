using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>Where a run currently stands. Only one of these is ever true at a time.</summary>
    public enum RunPhase
    {
        /// <summary>Inside a layer, with days left on the calendar.</summary>
        Exploring,

        /// <summary>
        /// The layer boss is down. The way on is the interlude. On the king layer there is no way on: the life ends
        /// with EndLife (seven_layers_v4.md §10).
        /// </summary>
        LayerCleared,

        /// <summary>階層間の休憩. It costs no day (seven_layers_v4.md §2.1).</summary>
        Interlude,

        /// <summary>HP reached 0 in a battle. The battle core reports this in.</summary>
        Fallen,

        /// <summary>A king fell while the other two were still down: the clear (seven_layers_v4.md §2.2).</summary>
        Completed,

        /// <summary>
        /// The player closed the life alive: the 生存ルート (concept-v3.md §8.1). What the next
        /// life receives from it is #108's; the run only records that it ended this way.
        /// </summary>
        Survived,

        /// <summary>The calendar ran out: the dragon god wakes and the world ends (seven_layers_v4.md §2.1).</summary>
        Expired,
    }

    /// <summary>
    /// A 休息 node offers one of two things for its single day, never both
    /// (dungeon_exploration_v4.md §3.2).
    /// </summary>
    public enum RestChoice
    {
        /// <summary>HP back, stamina full, max stamina up.</summary>
        Rest,

        /// <summary>One notch of mastery on a chosen card instead.</summary>
        Train,
    }

    /// <summary>
    /// One layer's exploration state. Immutable: every transition returns a new one, so a run
    /// can be replayed from its seed and its inputs, the same way the battle core works.
    /// </summary>
    public sealed class ExplorationState
    {
        public ExplorationState(
            LayerProfile profile,
            LayerMap map,
            int currentNodeId,
            IReadOnlyCollection<int> resolved,
            CalendarState calendar,
            int hp,
            int maxHp,
            int stamina,
            int tempMaxStaminaMod,
            int trainingMarks,
            RunLoadout loadout,
            RunPhase phase,
            bool trial = false,
            bool nextStepFree = false)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Resolved = resolved ?? throw new ArgumentNullException(nameof(resolved));
            Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            CurrentNodeId = currentNodeId;
            Hp = hp;
            MaxHp = maxHp;
            Stamina = stamina;
            TempMaxStaminaMod = tempMaxStaminaMod;
            TrainingMarks = trainingMarks;
            Phase = phase;
            Trial = trial;
            NextStepFree = nextStepFree;
        }

        public LayerProfile Profile { get; }

        public LayerMap Map { get; }

        public int CurrentNodeId { get; }

        /// <summary>Nodes already spent a day on. Walking back over them is free (§4.2).</summary>
        public IReadOnlyCollection<int> Resolved { get; }

        /// <summary>崩壊の暦, shared by every life (seven_layers_v4.md §2).</summary>
        public CalendarState Calendar { get; }

        /// <summary>Days until the world ends.</summary>
        public int DaysLeft => Calendar.DaysLeft;

        /// <summary>早駆けの丸薬 was taken: the next new node costs no day (tools_and_consumables_v4.md §3).</summary>
        public bool NextStepFree { get; }

        /// <summary>The region of the layer the life stands in: Common on layers 1 and 2.</summary>
        public Region Region => Profile.Region;

        public int Hp { get; }

        public int MaxHp { get; }

        public int Stamina { get; }

        /// <summary>Heavy packs, strange meals, a rest. Clamped to ±4 when max stamina is read.</summary>
        public int TempMaxStaminaMod { get; }

        /// <summary>
        /// Notches earned at 習熟訓練 nodes this layer: a running total. Not BattleCore's Constants.TrainingTicks,
        /// which is how many one training gives (1); the two keep different names because they count different
        /// things (#598).
        /// </summary>
        public int TrainingMarks { get; }

        public RunLoadout Loadout { get; }

        public RunPhase Phase { get; }

        /// <summary>
        /// Trial extension (docs/EXTENSIONS.md 「探索の持ち物とイベント」): the run plays the trial rules — every
        /// carried item acts and イベント nodes resolve (ExplorationReducer.Enter's <c>trial</c>). False is the C#.
        /// </summary>
        public bool Trial { get; }

        /// <summary>
        /// The max stamina the next battle starts from. A battle's own swings of the maximum are applied on top of
        /// this inside the battle and never written back (battle_core_v4.md §6.2).
        /// </summary>
        public int MaxStamina => StaminaRules.MaxStamina(TempMaxStaminaMod);

        public bool IsOver =>
            Phase == RunPhase.Fallen || Phase == RunPhase.Completed || Phase == RunPhase.Survived ||
            Phase == RunPhase.Expired;

        public bool HasResolved(int nodeId) => Resolved.Contains(nodeId);

        /// <summary>Nodes the player could still spend a day on from where they stand.</summary>
        public IReadOnlyList<int> ReachableUnresolved() =>
            Map.Neighbours(CurrentNodeId).Where(id => !HasResolved(id)).ToList();

        public ExplorationState With(
            LayerProfile? profile = null,
            LayerMap? map = null,
            int? currentNodeId = null,
            IReadOnlyCollection<int>? resolved = null,
            CalendarState? calendar = null,
            int? hp = null,
            int? maxHp = null,
            int? stamina = null,
            int? tempMaxStaminaMod = null,
            int? trainingMarks = null,
            RunLoadout? loadout = null,
            RunPhase? phase = null,
            bool? trial = null,
            bool? nextStepFree = null) =>
            new ExplorationState(
                profile ?? Profile,
                map ?? Map,
                currentNodeId ?? CurrentNodeId,
                resolved ?? Resolved,
                calendar ?? Calendar,
                hp ?? Hp,
                maxHp ?? MaxHp,
                stamina ?? Stamina,
                tempMaxStaminaMod ?? TempMaxStaminaMod,
                trainingMarks ?? TrainingMarks,
                loadout ?? Loadout,
                phase ?? Phase,
                trial ?? Trial,
                nextStepFree ?? NextStepFree);
    }
}
