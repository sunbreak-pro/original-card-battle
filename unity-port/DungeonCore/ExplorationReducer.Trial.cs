// Trial extension of the exploration reducer (port of the browser trial's src/core/dungeon/ExplorationReducer.js;
// docs/EXTENSIONS.md 「探索の持ち物とイベント」 and 「探索の結果と再開」). Everything here acts only on a state whose
// Trial flag is on (ExplorationReducer.Enter(..., trial: true)); with it off the reducer is the C# as it was.
using System;
using System.Linq;

namespace DungeonCore
{
    public static partial class ExplorationReducer
    {
        /// <summary>
        /// Trial extension (tools_and_consumables_v4.md §3): every consumable acts — 薬草 gives back Amount% of the HP
        /// maximum, 濃い干し肉 raises max stamina by Amount inside the ±4 of the temporary changes, 忘れ水 fills
        /// current stamina, 浄化の香 pulls the gauge back as in the C#. 標の石 is spent by Teleport, which needs the
        /// node it jumps to.
        /// </summary>
        // js: core/dungeon/ExplorationReducer.js:112
        private static ExplorationState UseTrialConsumable(ExplorationState state, RunLoadout loadout, int relief, ReliefItem item)
        {
            switch (item.Kind)
            {
                case ReliefKind.Heal:
                    return state.With(loadout: loadout, hp: Math.Min(state.MaxHp, state.Hp + Share(state.MaxHp, item.Amount)));
                case ReliefKind.MaxStamina:
                    // 「最大スタミナ +1（±4 の枠内）」: the same temporary change a rest makes (miasma_and_battle_v4.md §5).
                    return state.With(loadout: loadout, tempMaxStaminaMod: Clamp(state.TempMaxStaminaMod + item.Amount));
                case ReliefKind.Refill:
                    return state.With(loadout: loadout, stamina: state.MaxStamina);
                case ReliefKind.Teleport:
                    throw new ExplorationRuleException("標の石 jumps to a node: use Teleport");
                default:
                    return state.With(loadout: loadout, miasmaPercent: Miasma.Relieve(state.MiasmaPercent, relief));
            }
        }

        /// <summary>
        /// Trial extension: 標の石 — 「同じ層の未踏ノードへ 1 度だけ跳ぶ」, and 「跳んだ先のノードは新しいノードなので、
        /// 普通に刻限 1 と瘴気がかかります」 (tools_and_consumables_v4.md §3). The item in <paramref name="index"/> is spent
        /// and <paramref name="nodeId"/> is resolved exactly as a step into it would be (a rest takes <paramref name="choice"/>).
        /// </summary>
        // js: core/dungeon/ExplorationReducer.js:132
        public static ExplorationState Teleport(ExplorationState state, int index, int nodeId, RestChoice choice = RestChoice.Rest)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Trial) throw new ExplorationRuleException("標の石 acts in trial mode only");
            var carried = state.Loadout.Consumables;
            if (index < 0 || index >= carried.Count || carried[index].Kind != ReliefKind.Teleport)
                throw new ExplorationRuleException("no 標の石 in that slot");
            if (!CanTeleport(state, nodeId))
                throw new ExplorationRuleException($"no jump to node {nodeId}");
            var loadout = state.Loadout.Spend(index, out _);
            return Resolve(state.With(loadout: loadout), nodeId, choice);
        }

        /// <summary>
        /// Trial extension: whether 標の石 can land on this node — the layer is being explored, a 刻限 is left to
        /// pay, and the node is an unresolved node of this layer (any of them, the boss included: §3 names no
        /// exception — 試作で決めたこと).
        /// </summary>
        // js: core/dungeon/ExplorationReducer.js:149
        public static bool CanTeleport(ExplorationState state, int nodeId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Trial
                && state.Phase == RunPhase.Exploring
                && state.TimeLeft > 0
                && state.Map.Nodes.Any(n => n.Id == nodeId)
                && !state.HasResolved(nodeId);
        }

        /// <summary>
        /// Trial extension: an イベント node's effect on the run (encounters_and_rewards_v4.md §5). The node's 刻限
        /// and 瘴気 were paid by the step; this applies what the event does once the player has answered
        /// (泉 always 「受ける」):
        /// 泉: HP back by the layer's share of the maximum.
        /// 重い宝箱 受ける: max stamina −2 as a temporary change (inside ±4); current stamina follows the lower
        /// maximum. What it holds is the bridge's (cards, consumables, pages).
        /// 特殊な食事 受ける: max stamina +1 as a temporary change; HP −10% of the maximum, never below 1
        /// (試作で決めたこと: §5 does not say a meal may kill).
        /// 受けない: nothing.
        /// </summary>
        // js: core/dungeon/ExplorationReducer.js:169
        public static ExplorationState ResolveEvent(ExplorationState state, EventKind kind, bool accept)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Trial) throw new ExplorationRuleException("events resolve in trial mode only");
            if (state.IsOver) throw new ExplorationRuleException("the run is over");
            if (!accept) return state;
            switch (kind)
            {
                case EventKind.Spring:
                    return state.With(hp: Math.Min(state.MaxHp, state.Hp + SpringHeal(state)));
                case EventKind.HeavyChest:
                {
                    int temp = Clamp(state.TempMaxStaminaMod - LayerEvents.ChestStaminaCost);
                    int max = Miasma.MaxStamina(temp, state.MiasmaPercent);
                    return state.With(tempMaxStaminaMod: temp, stamina: Math.Min(state.Stamina, max));
                }
                case EventKind.StrangeMeal:
                {
                    int temp = Clamp(state.TempMaxStaminaMod + LayerEvents.MealStaminaGain);
                    int hp = Math.Max(1, state.Hp - MealCost(state));
                    return state.With(tempMaxStaminaMod: temp, hp: Math.Min(state.Hp, hp));
                }
                default:
                    throw new ArgumentException($"no such event {kind}", nameof(kind));
            }
        }

        /// <summary>
        /// Trial extension: HP 階層間の休憩 gives back on this state, before the maximum clamps it — 30%, or 45% with
        /// 灯芯の欠片 in trial mode (InterludePercent). Interlude adds exactly this, up to the maximum.
        /// </summary>
        // js: core/dungeon/ExplorationReducer.js:196
        public static int InterludeHealOf(ExplorationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Trial ? Share(state.MaxHp, InterludePercent(state)) : InterludeHeal(state.MaxHp);
        }

        /// <summary>Trial extension: HP a 泉 on this state's layer gives back, before the maximum clamps it.</summary>
        // js: core/dungeon/ExplorationReducer.js:202
        public static int SpringHeal(ExplorationState state) =>
            Share(state.MaxHp, LayerEvents.SpringPercent(state.Profile.Layer));

        /// <summary>Trial extension: HP a 特殊な食事 takes, before the floor of 1.</summary>
        // js: core/dungeon/ExplorationReducer.js:205
        public static int MealCost(ExplorationState state) => Share(state.MaxHp, LayerEvents.MealHpPercent);

        /// <summary>
        /// Trial extension: a consumable found on the way goes into the slots — after the others, or in place of
        /// <paramref name="replaceSlot"/> when the three are full (tools_and_consumables_v4.md §3). Costs no 刻限.
        /// </summary>
        // js: core/dungeon/ExplorationReducer.js:211
        public static ExplorationState TakeConsumable(ExplorationState state, ReliefItem item, int replaceSlot = -1)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsOver) throw new ExplorationRuleException("the run is over");
            return state.With(loadout: state.Loadout.Take(item, replaceSlot));
        }

        /// <summary>
        /// Trial extension: the share of the HP maximum 階層間の休憩 gives back on this state — 30%, and 45% with
        /// 灯芯の欠片 in trial mode (tools_and_consumables_v4.md §2; copies stack — 試作で決めたこと).
        /// </summary>
        // js: core/dungeon/ExplorationReducer.js:221
        public static int InterludePercent(ExplorationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return InterludeHpPercent + (state.Trial ? state.Loadout.InterludeBonus : 0);
        }
    }
}
