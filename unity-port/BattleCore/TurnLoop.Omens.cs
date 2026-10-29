using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// The enemy's next step (#584): §9 step 12's omen and 予定, the boss switches that move the body
    /// (EnterMove, Sway), and a 予定変更.
    /// </summary>
    public static partial class TurnLoop
    {
        /// <summary>
        /// §9 step 12, for one enemy. It judges what it can pay for with the stamina it will hold when
        /// the omen is carried out — after its next recovery — so an omen only becomes a rest when
        /// something drained it in between. The canon does not fix this moment; see #70. The branch
        /// is this enemy's gap band at this moment (§6.1).
        /// </summary>
        private static BattleState DecideNextOmen(BattleState state, int unit, List<BattleEvent> events)
        {
            var enemy = state.Enemies[unit];

            // #50: the boss's tree is asked first. A switch that starts or stops holding is announced.
            var view = AdaptationViewOf(state, unit);
            var active = EnemyAi.ActiveSwitch(enemy.Def, view);

            // #51: a switch holding for the first time this battle is remembered (a stage that never
            // goes back reads it), and moves the body when it says so (EnterMove).
            if (enemy.Def.Switches != null)
            {
                foreach (var each in enemy.Def.Switches)
                {
                    if (view.HasHeld(each.Id) || !each.Holds(view)) continue;
                    var held = new List<string>(state.Enemies[unit].Held) { each.Id };
                    state = state.WithUnit(unit, state.Enemies[unit] with { HeldSwitches = held });
                    if (each.EnterMove != 0) state = MoveBySwitch(state, unit, each.EnterMove, events);
                }
                enemy = state.Enemies[unit];
            }

            if (!string.Equals(active?.Id, enemy.ActiveSwitch, StringComparison.Ordinal))
            {
                events.Add(new TreeSwitched(Actor.Enemy, enemy.ActiveSwitch, active?.Id) { Unit = unit });
            }

            // #51 (roster §6.4 root_sk): the switch that shakes off every word the enemy holds. It
            // clears at every decision where it holds, not only when it starts holding: root_sk holds
            // only in the decision right after the turn that triggered it, so holding twice in a row
            // means it was triggered twice, and each trigger clears.
            if (active != null && active.Cleanse && enemy.Body.Statuses.Kinds.Count > 0)
            {
                foreach (var kind in enemy.Body.Statuses.Kinds)
                {
                    events.Add(new StatusCleared(Actor.Enemy, kind, enemy.Body.Statuses.Stacks(kind)) { Unit = unit });
                }
                state = state.WithEnemy(unit, enemy.Body with { Statuses = StatusSet.Empty });
                enemy = state.Enemies[unit];
            }

            int gap = state.GapTo(unit);
            int stamina = EnemyAi.StaminaAtAction(enemy.Def, enemy.Body);

            // #51 (roster §4.3): an action the player's word keeps out, as it will stand after the
            // player's next turn start, is passed over like a spent stance — and, #497 (v4.18), one
            // whose word the player already holds at the cap of the switch decided here.
            var skip = new List<string>(enemy.Spent);
            skip.AddRange(EnemyAi.Barred(enemy.Def, state.Player.Statuses, beforeFoeTurn: true, active: active, warded: state.Tools.WardedStacks));
            var omen = EnemyAi.DecideOmen(enemy.Def, gap, stamina, skip, active);
            events.Add(new OmenSet(Actor.Enemy, omen, Decided: true) { Unit = unit });

            // §17.6 F11 (#50): an elite or a boss shows its second step too, as a 予定.
            var plan = EnemyAi.DecidePlan(enemy.Def, gap, stamina, omen, skip, active);
            if (plan != null) events.Add(new PlanSet(Actor.Enemy, plan, Decided: true) { Unit = unit });

            return state.WithUnit(unit, enemy with { Omen = omen, Plan = plan, ActiveSwitch = active?.Id });
        }

        /// <summary>#50: what an adaptation of this enemy reads at this moment. #51 adds N and the switches that have held.</summary>
        private static AdaptationView AdaptationViewOf(BattleState state, int unit)
        {
            var enemy = state.Enemies[unit];
            return new AdaptationView(
                state.Turn, enemy.Body.Hp, enemy.Body.MaxHp, enemy.ExecutedActions, state.History,
                state.GapTo(unit), enemy.Held);
        }

        /// <summary>
        /// #51: the enemy moves itself because a switch says so — a stage's EnterMove, or a step of a
        /// Sway. As far as the line allows, with no 鈍足 taken off (the roster's 「出られるだけ」 /
        /// 「動けるだけ」). It is the enemy moving itself, so 根縛り fires on it as on a move face.
        /// </summary>
        private static BattleState MoveBySwitch(BattleState state, int unit, int cells, List<BattleEvent> events)
        {
            var shift = Field.Move(state, Actor.Enemy, cells, unit);
            if (shift.To == shift.From) return state;
            state = state.WithEnemy(unit, state.Enemies[unit].Body with { Cell = shift.To });
            events.Add(new CellsMoved(Actor.Enemy, shift.From, shift.To, Pushed: false) { Unit = unit });
            foreach (var bind in state.Player.StanceList)
            {
                if (bind.Def.Hook != StanceHook.BreakOnFoeMove || bind.Def.Break <= 0 || bind.StoppedIn(state.Turn)) continue;
                events.Add(new StanceFired(Actor.Player, bind.Source, StanceHook.BreakOnFoeMove) { Unit = unit });
                state = Break(state, Actor.Player, Actor.Enemy, unit, bind.Def.Break, unit, events);
            }
            return state;
        }

        /// <summary>
        /// #51 (roster §6.1 段階 3): once a switch with Sway has held, the body steps at the start of
        /// each of its phases — 前へ 1 on an odd turn, 後ろへ 1 on an even one — as far as it can.
        /// </summary>
        private static BattleState Sway(BattleState state, int unit, List<BattleEvent> events)
        {
            var enemy = state.Enemies[unit];
            if (enemy.Def.Switches == null || enemy.Held.Count == 0) return state;
            foreach (var each in enemy.Def.Switches)
            {
                if (!each.Sway || !enemy.Held.Contains(each.Id)) continue;
                return MoveBySwitch(state, unit, state.Turn % 2 == 1 ? 1 : -1, events);
            }
            return state;
        }

        /// <summary>
        /// #50 予定変更: the second action, read again after the first, is not the one planned. The
        /// plan is replaced by what the tree now says; the event carries both.
        /// </summary>
        private static BattleState RevisePlan(BattleState state, int unit, Omen now, List<BattleEvent> events)
        {
            var planned = state.Enemies[unit].Plan;
            if (planned == null || Equals(planned, now)) return state;
            events.Add(new PlanChanged(Actor.Enemy, planned, now) { Unit = unit });
            return state.WithUnit(unit, state.Enemies[unit] with { Plan = now });
        }
    }
}
