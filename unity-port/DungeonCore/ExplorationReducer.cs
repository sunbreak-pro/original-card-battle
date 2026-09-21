using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>Raised when a caller asks for a move the rules do not allow.</summary>
    public sealed class ExplorationRuleException : Exception
    {
        public ExplorationRuleException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// The two exploration resources, and nothing else. 刻限 says what gets done inside a
    /// layer; 瘴気 says how long the life lasts. They are kept apart on purpose — spending a
    /// 刻限 is the only thing that moves the gauge, and the gauge never gives 刻限 back
    /// (concept-v3.md §6).
    /// </summary>
    public static class ExplorationReducer
    {
        /// <summary>休息: HP back by this share of the maximum (concept-v3.md §7).</summary>
        public const int RestHpPercent = 15;

        /// <summary>階層間の休憩: HP back by this share (concept-v3.md §7).</summary>
        public const int InterludeHpPercent = 30;

        /// <summary>休息 also lifts max stamina (concept-v3.md §7). Clamped with the other temporaries.</summary>
        public const int RestMaxStaminaBonus = 2;

        /// <summary>Walks into a layer. The entry node is resolved on arrival and costs its 刻限.</summary>
        public static ExplorationState Enter(
            LayerProfile profile,
            LayerMap map,
            RunLoadout loadout,
            int hp,
            int maxHp,
            int stamina,
            int miasmaPercent = 0,
            int tempMaxStaminaMod = 0)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (profile.Layer != map.Layer)
                throw new ArgumentException($"layer {profile.Layer} was handed the map of layer {map.Layer}", nameof(map));

            var fresh = new ExplorationState(
                profile,
                map,
                map.EntryId,
                new HashSet<int>(),
                profile.TimeLimit,
                miasmaPercent,
                hp,
                maxHp,
                stamina,
                tempMaxStaminaMod,
                trainingMarks: 0,
                loadout,
                RunPhase.Exploring);

            return Resolve(fresh, map.EntryId, RestChoice.Rest);
        }

        /// <summary>
        /// Moves to a neighbouring node. Stepping back over a node that is already resolved is
        /// free; entering a new one costs one 刻限 and one layer's worth of 瘴気.
        /// </summary>
        public static ExplorationState Step(ExplorationState state, int nodeId, RestChoice choice = RestChoice.Rest)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Phase != RunPhase.Exploring)
                throw new ExplorationRuleException($"the layer is no longer being explored ({state.Phase})");
            if (!state.Map.Neighbours(state.CurrentNodeId).Contains(nodeId))
                throw new ExplorationRuleException($"node {nodeId} does not touch node {state.CurrentNodeId}");

            if (state.HasResolved(nodeId)) return state.With(currentNodeId: nodeId);

            if (state.TimeLeft <= 0)
                throw new ExplorationRuleException("no 刻限 left to spend on a new node");

            return Resolve(state, nodeId, choice);
        }

        /// <summary>Burns one consumable. Costs no 刻限, so it never moves the gauge upward.</summary>
        public static ExplorationState UseConsumable(ExplorationState state, int index)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsOver)
                throw new ExplorationRuleException("the run is over");

            var loadout = state.Loadout.Spend(index, out int relief);
            return state.With(
                loadout: loadout,
                miasmaPercent: Miasma.Relieve(state.MiasmaPercent, relief));
        }

        /// <summary>
        /// 階層間の休憩. HP back by 30%, stamina full, and the gauge does not move — the only
        /// place in a run where time passes for free (concept-v3.md §7).
        /// </summary>
        public static ExplorationState Interlude(ExplorationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Phase != RunPhase.LayerCleared && state.Phase != RunPhase.PushedOut)
                throw new ExplorationRuleException($"there is no interlude from {state.Phase}");

            int healed = Math.Min(state.MaxHp, state.Hp + Share(state.MaxHp, InterludeHpPercent));
            return state.With(
                hp: healed,
                stamina: state.MaxStamina,
                phase: RunPhase.Interlude);
        }

        /// <summary>Drops into the next layer, carrying HP, stamina and the gauge (concept-v3.md §7.1).</summary>
        public static ExplorationState Descend(ExplorationState state, LayerProfile next, LayerMap nextMap)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (state.Phase != RunPhase.Interlude)
                throw new ExplorationRuleException($"a layer is entered from the interlude, not from {state.Phase}");

            return Enter(
                next,
                nextMap,
                state.Loadout,
                state.Hp,
                state.MaxHp,
                state.Stamina,
                state.MiasmaPercent,
                state.TempMaxStaminaMod);
        }

        /// <summary>
        /// Hands a battle's result back to the run. HP and stamina carry across battles
        /// (concept-v3.md §7.1); the battle's own max-stamina swings do not, so nothing about
        /// 瘴気纏い reaches the gauge (battle_core_v4.md §6.2). Wiring this to the battle core
        /// itself is #99.
        /// </summary>
        public static ExplorationState ReportBattle(ExplorationState state, int hpAfter, int staminaAfter)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            int hp = Math.Max(0, Math.Min(state.MaxHp, hpAfter));
            int stamina = Math.Max(0, Math.Min(state.MaxStamina, staminaAfter));
            var phase = hp == 0 ? RunPhase.Fallen : state.Phase;
            return state.With(hp: hp, stamina: stamina, phase: phase);
        }

        /// <summary>Spends the 刻限 for a node and applies what standing on it does.</summary>
        private static ExplorationState Resolve(ExplorationState state, int nodeId, RestChoice choice)
        {
            var node = state.Map.Node(nodeId);

            int miasma = Miasma.Accumulate(state.MiasmaPercent, state.EffectiveDensity);
            int timeLeft = state.TimeLeft - 1;
            int temp = state.TempMaxStaminaMod;
            int hp = state.Hp;
            int marks = state.TrainingMarks;

            if (node.Kind == NodeKind.Rest)
            {
                if (choice == RestChoice.Rest)
                {
                    hp = Math.Min(state.MaxHp, hp + Share(state.MaxHp, RestHpPercent));
                    temp = Clamp(temp + RestMaxStaminaBonus);
                }
                else
                {
                    marks += 1;
                }
            }

            var resolved = new HashSet<int>(state.Resolved) { nodeId };

            // Stamina recovers by 3 for every 刻限 spent outside battle, up to the max that the
            // new gauge reading allows (concept-v3.md §12-16).
            int maxStamina = Miasma.MaxStamina(temp, miasma);
            int stamina = Math.Min(maxStamina, state.Stamina + Miasma.FieldRecovery);
            if (choice == RestChoice.Rest && node.Kind == NodeKind.Rest) stamina = maxStamina;

            // 瘴気死 outranks everything: the life ends on the node that tipped the gauge,
            // even if that node was the layer boss.
            var phase = RunPhase.Exploring;
            if (Miasma.IsLethal(miasma)) phase = RunPhase.MiasmaDeath;
            else if (node.Kind == NodeKind.Boss)
                phase = state.Profile.IsLast ? RunPhase.Completed : RunPhase.LayerCleared;
            else if (timeLeft <= 0) phase = RunPhase.PushedOut;

            return state.With(
                currentNodeId: nodeId,
                resolved: resolved,
                timeLeft: Math.Max(0, timeLeft),
                miasmaPercent: miasma,
                hp: hp,
                stamina: stamina,
                tempMaxStaminaMod: temp,
                trainingMarks: marks,
                phase: phase);
        }

        /// <summary>A share of a maximum, rounded half away from zero the way the battle core rounds.</summary>
        private static int Share(int maximum, int percent) =>
            (int)Math.Round(maximum * percent / 100.0, MidpointRounding.AwayFromZero);

        private static int Clamp(int tempMod) =>
            Math.Max(-Miasma.TempModClamp, Math.Min(Miasma.TempModClamp, tempMod));
    }
}
