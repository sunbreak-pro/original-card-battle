using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>Where a run currently stands. Only one of these is ever true at a time.</summary>
    public enum RunPhase
    {
        /// <summary>Inside a layer with 刻限 left.</summary>
        Exploring,

        /// <summary>The layer boss is down. The way on is the interlude.</summary>
        LayerCleared,

        /// <summary>刻限 ran out. The layer's actions are over, boss or not (§4.3).</summary>
        PushedOut,

        /// <summary>階層間の休憩. No 刻限, no 瘴気 (concept-v3.md §7).</summary>
        Interlude,

        /// <summary>The gauge hit 100%.</summary>
        MiasmaDeath,

        /// <summary>HP reached 0 in a battle. The battle core reports this in.</summary>
        Fallen,

        /// <summary>The last layer's boss is down.</summary>
        Completed,
    }

    /// <summary>
    /// A 休息 node offers one of two things for its single 刻限, never both
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
            int timeLeft,
            int miasmaPercent,
            int hp,
            int maxHp,
            int stamina,
            int tempMaxStaminaMod,
            int trainingMarks,
            RunLoadout loadout,
            RunPhase phase)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Resolved = resolved ?? throw new ArgumentNullException(nameof(resolved));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            CurrentNodeId = currentNodeId;
            TimeLeft = timeLeft;
            MiasmaPercent = miasmaPercent;
            Hp = hp;
            MaxHp = maxHp;
            Stamina = stamina;
            TempMaxStaminaMod = tempMaxStaminaMod;
            TrainingMarks = trainingMarks;
            Phase = phase;
        }

        public LayerProfile Profile { get; }

        public LayerMap Map { get; }

        public int CurrentNodeId { get; }

        /// <summary>Nodes already spent a 刻限 on. Walking back over them is free (§4.2).</summary>
        public IReadOnlyCollection<int> Resolved { get; }

        public int TimeLeft { get; }

        public int MiasmaPercent { get; }

        public int Hp { get; }

        public int MaxHp { get; }

        public int Stamina { get; }

        /// <summary>Heavy packs, strange meals, a rest. Clamped to ±4 when max stamina is read.</summary>
        public int TempMaxStaminaMod { get; }

        /// <summary>Notches earned at 習熟訓練 nodes this layer.</summary>
        public int TrainingMarks { get; }

        public RunLoadout Loadout { get; }

        public RunPhase Phase { get; }

        /// <summary>What one 刻限 in this layer costs after the run's tools are counted.</summary>
        public int EffectiveDensity => Miasma.EffectiveDensity(Profile.Density, Loadout.DensityRelief);

        /// <summary>
        /// The max stamina the next battle starts from. The battle's own 瘴気纏い is applied on
        /// top of this inside the battle and never written back (battle_core_v4.md §6.2).
        /// </summary>
        public int MaxStamina => Miasma.MaxStamina(TempMaxStaminaMod, MiasmaPercent);

        public bool IsOver =>
            Phase == RunPhase.MiasmaDeath || Phase == RunPhase.Fallen || Phase == RunPhase.Completed;

        public bool HasResolved(int nodeId) => Resolved.Contains(nodeId);

        /// <summary>Nodes the player could still spend a 刻限 on from where they stand.</summary>
        public IReadOnlyList<int> ReachableUnresolved() =>
            Map.Neighbours(CurrentNodeId).Where(id => !HasResolved(id)).ToList();

        public ExplorationState With(
            LayerProfile? profile = null,
            LayerMap? map = null,
            int? currentNodeId = null,
            IReadOnlyCollection<int>? resolved = null,
            int? timeLeft = null,
            int? miasmaPercent = null,
            int? hp = null,
            int? maxHp = null,
            int? stamina = null,
            int? tempMaxStaminaMod = null,
            int? trainingMarks = null,
            RunLoadout? loadout = null,
            RunPhase? phase = null) =>
            new ExplorationState(
                profile ?? Profile,
                map ?? Map,
                currentNodeId ?? CurrentNodeId,
                resolved ?? Resolved,
                timeLeft ?? TimeLeft,
                miasmaPercent ?? MiasmaPercent,
                hp ?? Hp,
                maxHp ?? MaxHp,
                stamina ?? Stamina,
                tempMaxStaminaMod ?? TempMaxStaminaMod,
                trainingMarks ?? TrainingMarks,
                loadout ?? Loadout,
                phase ?? Phase);
    }
}
