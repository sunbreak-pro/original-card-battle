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
    /// The exploration's moves. One resource paces them: 崩壊の暦 (seven_layers_v4.md §2). A new node costs one day,
    /// walking back over a resolved node costs none, and the interlude between layers costs none. A king brought
    /// down holds the calendar still until it comes back.
    /// </summary>
    public static partial class ExplorationReducer
    {
        /// <summary>休息: HP back by this share of the maximum (concept-v3.md §7).</summary>
        public const int RestHpPercent = 15;

        /// <summary>階層間の休憩: HP back by this share (concept-v3.md §7).</summary>
        public const int InterludeHpPercent = 30;

        /// <summary>休息 also lifts max stamina (concept-v3.md §7). Clamped with the other temporaries.</summary>
        public const int RestMaxStaminaBonus = 2;

        /// <summary>HP a 休息 gives back at this maximum, before the maximum clamps it. The screen prints this.</summary>
        public static int RestHeal(int maxHp) => Share(maxHp, RestHpPercent);

        /// <summary>HP 階層間の休憩 gives back at this maximum, before the maximum clamps it.</summary>
        public static int InterludeHeal(int maxHp) => Share(maxHp, InterludeHpPercent);

        /// <summary>
        /// Walks into a layer. The entry node is resolved on arrival and costs its day, unless 日読みの砂 is carried
        /// (tools_and_consumables_v4.md §2.4). A calendar that has already run out ends the life on the entry node
        /// (RunPhase.Expired). Trial extension: <paramref name="trial"/> (default false = the C#) turns
        /// the trial rules on for the run, and the state carries the flag to every later call.
        /// </summary>
        // js: core/dungeon/ExplorationReducer.js:45
        public static ExplorationState Enter(
            LayerProfile profile,
            LayerMap map,
            RunLoadout loadout,
            int hp,
            int maxHp,
            int stamina,
            CalendarState calendar,
            int tempMaxStaminaMod = 0,
            bool trial = false) =>
            EnterLayer(profile, map, loadout, hp, maxHp, stamina, calendar, tempMaxStaminaMod, trial, nextStepFree: false);

        private static ExplorationState EnterLayer(
            LayerProfile profile,
            LayerMap map,
            RunLoadout loadout,
            int hp,
            int maxHp,
            int stamina,
            CalendarState calendar,
            int tempMaxStaminaMod,
            bool trial,
            bool nextStepFree)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (calendar == null) throw new ArgumentNullException(nameof(calendar));
            if (profile.Layer != map.Layer)
                throw new ArgumentException($"layer {profile.Layer} was handed the map of layer {map.Layer}", nameof(map));

            var fresh = new ExplorationState(
                profile,
                map,
                map.EntryId,
                new HashSet<int>(),
                calendar,
                hp,
                maxHp,
                stamina,
                tempMaxStaminaMod,
                trainingMarks: 0,
                loadout,
                RunPhase.Exploring,
                trial,
                nextStepFree);

            return Resolve(fresh, map.EntryId, RestChoice.Rest);
        }

        /// <summary>
        /// Moves to a neighbouring node. Stepping back over a node that is already resolved is
        /// free; entering a new one costs one day.
        /// </summary>
        public static ExplorationState Step(ExplorationState state, int nodeId, RestChoice choice = RestChoice.Rest)
        {
            if (!CanStep(state, nodeId)) throw new ExplorationRuleException(WhyNoStep(state, nodeId));
            if (state.HasResolved(nodeId)) return state.With(currentNodeId: nodeId);
            return Resolve(state, nodeId, choice);
        }

        /// <summary>
        /// Whether <see cref="Step"/> takes this node: the layer is being explored and the node touches the one the
        /// player stands on. The screen asks this before it offers a step.
        /// </summary>
        public static bool CanStep(ExplorationState state, int nodeId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Phase == RunPhase.Exploring
                && state.Map.Neighbours(state.CurrentNodeId).Contains(nodeId)
                && (state.HasResolved(nodeId) || !state.Calendar.IsExpired);
        }

        private static string WhyNoStep(ExplorationState state, int nodeId)
        {
            if (state.Phase != RunPhase.Exploring) return $"the layer is no longer being explored ({state.Phase})";
            if (!state.Map.Neighbours(state.CurrentNodeId).Contains(nodeId)) return $"node {nodeId} does not touch node {state.CurrentNodeId}";
            return "the calendar has run out";
        }

        /// <summary>
        /// Burns one consumable (tools_and_consumables_v4.md §3). Costs no day. 早駆けの丸薬 makes the next new node
        /// free, 薬草 gives back a share of the HP maximum, 濃い干し肉 raises max stamina inside the ±4 clamp, 忘れ水
        /// fills stamina to the maximum. 標の石 needs a node to jump to and is refused (<see cref="CanUseConsumable"/>);
        /// in trial mode the map picks that node and <see cref="Teleport"/> spends the stone there (trial extension).
        /// </summary>
        public static ExplorationState UseConsumable(ExplorationState state, int index)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsOver)
                throw new ExplorationRuleException("the run is over");
            var carried = state.Loadout.Consumables;
            if (index >= 0 && index < carried.Count && carried[index].Kind == ReliefKind.Teleport)
                throw new ExplorationRuleException("a jump needs a node to jump to, and none is picked yet");

            var loadout = state.Loadout.Spend(index, out ReliefItem used);
            return used.Kind switch
            {
                ReliefKind.Haste => state.With(loadout: loadout, nextStepFree: true),
                ReliefKind.HealHp => state.With(loadout: loadout, hp: Math.Min(state.MaxHp, state.Hp + Share(state.MaxHp, used.Amount))),
                ReliefKind.MaxStamina => state.With(loadout: loadout, tempMaxStaminaMod: Clamp(state.TempMaxStaminaMod + used.Amount)),
                ReliefKind.RefillStamina => state.With(loadout: loadout, stamina: state.MaxStamina),
                _ => state.With(loadout: loadout),
            };
        }

        /// <summary>
        /// Whether <see cref="UseConsumable"/> takes this slot: the run goes on, the slot holds an
        /// item, and the item is not a jump waiting for a node to jump to.
        /// </summary>
        public static bool CanUseConsumable(ExplorationState state, int index)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var carried = state.Loadout.Consumables;
            return !state.IsOver && index >= 0 && index < carried.Count && carried[index].Kind != ReliefKind.Teleport;
        }

        /// <summary>
        /// Picks a consumable up (tools_and_consumables_v4.md §3): into a free slot, or in place of the
        /// one in <paramref name="replaceSlot"/> when every slot is full (RunLoadout.Take). Costs no day.
        /// </summary>
        public static ExplorationState TakeConsumable(ExplorationState state, ReliefItem item, int replaceSlot = -1)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsOver)
                throw new ExplorationRuleException("the run is over");

            return state.With(loadout: state.Loadout.Take(item, replaceSlot));
        }

        /// <summary>
        /// 階層間の休憩. HP back by 30%, stamina full, and no day passes (seven_layers_v4.md §2.1).
        /// </summary>
        public static ExplorationState Interlude(ExplorationState state)
        {
            if (!CanInterlude(state))
                throw new ExplorationRuleException($"there is no interlude from {state.Phase} on layer {state.Profile.Layer}");

            // Trial extension (js: core/dungeon/ExplorationReducer.js:234): 30% + 灯芯の欠片 in trial mode.
            int heal = state.Trial ? Share(state.MaxHp, InterludePercent(state)) : InterludeHeal(state.MaxHp);
            int healed = Math.Min(state.MaxHp, state.Hp + heal);
            return state.With(
                hp: healed,
                stamina: state.MaxStamina,
                phase: RunPhase.Interlude);
        }

        /// <summary>
        /// Whether <see cref="Interlude"/> applies: the layer boss fell. The king room has no layer below it in the
        /// 80% build, so a life that brought its king down ends with <see cref="EndLife"/> instead (§10).
        /// </summary>
        public static bool CanInterlude(ExplorationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Phase == RunPhase.LayerCleared && !state.Profile.IsKingLayer;
        }

        /// <summary>
        /// Drops into the next layer, carrying HP, stamina, the calendar and an unspent 早駆けの丸薬
        /// (concept-v3.md §7.1).
        /// </summary>
        public static ExplorationState Descend(ExplorationState state, LayerProfile next, LayerMap nextMap)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (state.Phase != RunPhase.Interlude)
                throw new ExplorationRuleException($"a layer is entered from the interlude, not from {state.Phase}");

            return EnterLayer(
                next,
                nextMap,
                state.Loadout,
                state.Hp,
                state.MaxHp,
                state.Stamina,
                state.Calendar,
                state.TempMaxStaminaMod,
                state.Trial,
                state.NextStepFree);
        }

        /// <summary>
        /// Hands a battle's result back to the run. HP and stamina carry across battles
        /// (concept-v3.md §7.1); the battle's own max-stamina swings do not (battle_core_v4.md §6.2). Winning the
        /// king room's boss fight brings the region's king down (CalendarState.KingDown). Wiring this to the battle
        /// core itself is #99.
        /// </summary>
        public static ExplorationState ReportBattle(ExplorationState state, int hpAfter, int staminaAfter)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            int hp = Math.Max(0, Math.Min(state.MaxHp, hpAfter));
            int stamina = Math.Max(0, Math.Min(state.MaxStamina, staminaAfter));
            if (hp == 0) return state.With(hp: hp, stamina: stamina, phase: RunPhase.Fallen);

            if (!IsKingFight(state)) return state.With(hp: hp, stamina: stamina);

            // The king is down only once it is beaten: the calendar stands still until it comes back, and three down
            // at once is the clear (§2.2).
            var calendar = state.Calendar.KingDown(state.Region);
            var phase = calendar.ClearReached ? RunPhase.Completed : RunPhase.LayerCleared;
            return state.With(hp: hp, stamina: stamina, calendar: calendar, phase: phase);
        }

        /// <summary>
        /// 「この生を終える」. The player may close the life alive at any point of the
        /// exploration, the interlude included (tier1-core.md R1-11). Nothing is spent and
        /// nothing is healed: the life simply stops where it stands. The handover days are the calendar's
        /// (CalendarState.Handover), counted by whoever starts the next life.
        /// </summary>
        public static ExplorationState EndLife(ExplorationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsOver)
                throw new ExplorationRuleException($"the life has already ended ({state.Phase})");

            return state.With(phase: RunPhase.Survived);
        }

        /// <summary>
        /// Spends one mark a 習熟訓練 node earned. The card it goes to and the ticks it takes are the
        /// battle core's (Mastery.Train, #625); the exploration core only counts the marks down.
        /// Costs no day: the node already took it.
        /// </summary>
        public static ExplorationState SpendTrainingMark(ExplorationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsOver)
                throw new ExplorationRuleException($"the life has already ended ({state.Phase})");
            if (state.TrainingMarks <= 0)
                throw new ExplorationRuleException("no 習熟訓練 mark left to spend");

            return state.With(trainingMarks: state.TrainingMarks - 1);
        }

        /// <summary>Spends the day for a node and applies what standing on it does.</summary>
        private static ExplorationState Resolve(ExplorationState state, int nodeId, RestChoice choice)
        {
            var node = state.Map.Node(nodeId);

            // 日読みの砂 covers a layer's entry node; 早駆けの丸薬 covers the next new node that would cost a day, so
            // an entry node the tool already pays for leaves the pill for the node after it.
            bool entryFree = nodeId == state.Map.EntryId && state.Loadout.EntryDayFree;
            bool hasteSpent = !entryFree && state.NextStepFree;
            var calendar = entryFree || hasteSpent ? state.Calendar : state.Calendar.Advance(1);

            int temp = state.TempMaxStaminaMod;
            int hp = state.Hp;
            int marks = state.TrainingMarks;

            if (node.Kind == NodeKind.Rest)
            {
                if (choice == RestChoice.Rest)
                {
                    hp = Math.Min(state.MaxHp, hp + RestHeal(state.MaxHp));
                    temp = Clamp(temp + RestMaxStaminaBonus);
                }
                else
                {
                    marks += 1;
                }
            }

            var resolved = new HashSet<int>(state.Resolved) { nodeId };

            // Stamina recovers by 3 for every new node outside battle, up to the max (concept-v3.md §12-16).
            int maxStamina = StaminaRules.MaxStamina(temp);
            int stamina = Math.Min(maxStamina, state.Stamina + StaminaRules.FieldRecovery);
            if (choice == RestChoice.Rest && node.Kind == NodeKind.Rest) stamina = maxStamina;

            // The boss fight is still ahead: a loss turns this into Fallen in ReportBattle. The king's fall waits for
            // the win there, so a lost king fight leaves nothing on the calendar for the next life to inherit.
            var phase = node.Kind == NodeKind.Boss ? RunPhase.LayerCleared : RunPhase.Exploring;

            // 期限切れ outranks everything: the world ends on the node that spent the last day, boss or not.
            if (calendar.IsExpired) phase = RunPhase.Expired;

            return state.With(
                currentNodeId: nodeId,
                resolved: resolved,
                calendar: calendar,
                hp: hp,
                stamina: stamina,
                tempMaxStaminaMod: temp,
                trainingMarks: marks,
                phase: phase,
                nextStepFree: state.NextStepFree && !hasteSpent);
        }

        /// <summary>
        /// The fight being reported is the king's: the player stands on the king room's boss node and the room has not
        /// ended otherwise. An expired calendar starts no fight. A beaten king reported twice gets the same return count
        /// again, because no day passes in between.
        /// </summary>
        private static bool IsKingFight(ExplorationState state) =>
            state.Phase == RunPhase.LayerCleared
            && state.Profile.IsKingLayer
            && state.CurrentNodeId == state.Map.BossId;

        /// <summary>A share of a maximum, rounded half away from zero the way the battle core rounds.</summary>
        private static int Share(int maximum, int percent) =>
            (int)Math.Round(maximum * percent / 100.0, MidpointRounding.AwayFromZero);

        private static int Clamp(int tempMod) => StaminaRules.ClampTemp(tempMod);
    }
}
