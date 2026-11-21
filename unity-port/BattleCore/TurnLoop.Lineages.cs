using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// The lineages' rules (#962, battle_core_v4 §7.5, roster §1.9): a parasite's host lets its
    /// parasite out where it falls and the crawling roots thin the player's recovery; a ghost leaps
    /// at its phase end and acts on its omens one phase late. The dragon lineage has no rule of its
    /// own, and an enemy whose data asks for none of these fights as before.
    /// </summary>
    public static partial class TurnLoop
    {
        // ---- 寄生: 宿主と寄生体 ----

        /// <summary>
        /// §7.5: every host that fell and is waiting (<see cref="EnemyUnit.EmergePending"/>) lets its
        /// parasite out, in definition order. The parasite stands on the host's near edge — or, if the
        /// player has since stepped onto it, on the first cell to the player's right it fits — at full
        /// HP and stamina, with none of the host's words, Guard or stances. It shows its omen at once
        /// (a ghost its two) and acts from the next enemy phase. Called once the card or the step of
        /// the phase that felled the host is done, so the rest of that card never lands on it.
        /// </summary>
        private static BattleState EmergeParasites(BattleState state, List<BattleEvent> events)
        {
            for (int unit = 0; unit < state.Enemies.Count; unit++)
            {
                var host = state.Enemies[unit];
                if (!host.EmergePending) continue;
                var parasite = host.Def.Parasite!;
                int cell = EmergeCell(state, unit, parasite.Size);
                var body = new CombatantState(
                    parasite.MaxHp, parasite.MaxHp, parasite.MaxStamina, parasite.MaxStamina,
                    Guard: 0, Cell: cell, Size: parasite.Size, StatusSet.Empty);
                state = state.WithUnit(unit, new EnemyUnit(parasite, body, Omen: null, Host: host.Def));
                events.Add(new ParasiteEmerged(Actor.Enemy, host.Def.Id, parasite.Id, cell, body.Hp, body.MaxHp) { Unit = unit });

                state = DecideNextOmen(state, unit, events);
                if (parasite.DelaysOmen) state = DecideLaterOmen(state, unit, events);
            }
            return state;
        }

        /// <summary>The cell the parasite of host <paramref name="unit"/> comes out on (see <see cref="EmergeParasites"/>).</summary>
        private static int EmergeCell(BattleState state, int unit, int size)
        {
            int near = state.Enemies[unit].Body.Cell;
            if (Field.CanStand(state, unit, near, size)) return near;
            for (int cell = state.Player.FarCell + 1; cell <= state.FieldCells; cell++)
            {
                if (Field.CanStand(state, unit, cell, size)) return cell;
            }
            return near;
        }

        /// <summary>Whether any host has fallen and still waits to let its parasite out.</summary>
        private static bool AnyEmergePending(BattleState state)
        {
            foreach (var enemy in state.Enemies)
            {
                if (enemy.EmergePending) return true;
            }
            return false;
        }

        // ---- 寄生: 根の這い ----

        /// <summary>
        /// §7.5 根の這い: the standing enemy whose <see cref="EnemyDef.RootCrawlGap"/> the player's N is
        /// within at a turn start — the nearest of them — or −1 for none. Several crawl as one.
        /// </summary>
        private static int RootCrawler(BattleState state)
        {
            int crawler = -1;
            foreach (int i in state.Living)
            {
                var reach = state.Enemies[i].Def.RootCrawlGap;
                if (!reach.HasValue || state.GapTo(i) > reach.Value) continue;
                if (crawler < 0 || state.Enemies[i].Body.Cell < state.Enemies[crawler].Body.Cell) crawler = i;
            }
            return crawler;
        }

        // ---- The lineage bosses' words on a card ----

        /// <summary>
        /// roster §13.1 芽吹き: a card played while it is held costs <see cref="Statuses.SproutingHpPerCard"/>
        /// HP, past Guard, once the card has resolved. A card that has felled the last enemy has won
        /// the battle first, and costs nothing.
        /// </summary>
        private static BattleState Sprout(BattleState state, List<BattleEvent> events)
        {
            var player = state.Player;
            if (!player.Statuses.Has(StatusKind.Sprouting) || Combat.IsDefeated(player.Hp)) return state;
            if (state.Living.Count == 0 && !AnyEmergePending(state)) return state;
            int hpAfter = Math.Max(0, player.Hp - Statuses.SproutingHpPerCard);
            events.Add(new StatusHpChanged(Actor.Player, StatusKind.Sprouting, hpAfter - player.Hp, hpAfter));
            return state with { Player = player with { Hp = hpAfter } };
        }

        // ---- 亡霊: 跳ぶ ----

        /// <summary>
        /// §7.5 跳ぶ: at the end of its phase a ghost leaps to the cell at gap k of its leap plan, over
        /// the cells between. A k whose cell is off the line, used by another enemy or the one it
        /// stands on is passed over for the next. The plan is the holding switch's (a stage's) or its
        /// own; a holding switch with <see cref="TreeSwitch.LeapOnce"/> sends this one leap to that gap
        /// instead and leaves the plan where it was. Not a move of its own: 鈍足, 呪縛, 鉤爪 and 根縛り
        /// do not touch it, and it is not 移動後.
        /// </summary>
        private static BattleState Leap(BattleState state, int unit, List<BattleEvent> events)
        {
            var enemy = state.Enemies[unit];
            var active = EnemyAi.ActiveSwitch(enemy.Def, AdaptationViewOf(state, unit));
            var plan = active?.LeapPlan ?? enemy.Def.LeapPlan;
            if (plan == null || plan.Count == 0) return state;

            int from = enemy.Body.Cell;
            int to = -1;
            int index = enemy.LeapIndex;
            if (active?.LeapOnce is int once)
            {
                int cell = Field.NearEdgeAt(once, state.Player.FarCell);
                if (cell != from && Field.CanStand(state, unit, cell)) to = cell;
            }
            if (to < 0) (to, index) = LeapTarget(state, unit, plan, index);

            state = state.WithUnit(unit, state.Enemies[unit] with { LeapIndex = index });
            if (to == from) return state;
            state = state.WithEnemy(unit, state.Enemies[unit].Body with { Cell = to });
            events.Add(new CellsMoved(Actor.Enemy, from, to, Pushed: false) { Unit = unit, Leap = true });
            return state;
        }

        /// <summary>
        /// The cell the plan sends enemy <paramref name="unit"/> to from where it stands now, starting at
        /// <paramref name="index"/>, and where the plan stands after. With no cell to take it stays and
        /// the plan moves on one.
        /// </summary>
        private static (int Cell, int Index) LeapTarget(BattleState state, int unit, IReadOnlyList<int> plan, int index)
        {
            int from = state.Enemies[unit].Body.Cell;
            for (int step = 0; step < plan.Count; step++)
            {
                int gap = plan[(index + step) % plan.Count];
                int cell = Field.NearEdgeAt(gap, state.Player.FarCell);
                if (cell != from && Field.CanStand(state, unit, cell)) return (cell, index + step + 1);
            }
            return (from, index + 1);
        }

        /// <summary>
        /// §7.5 枝を選ぶ間合い: the cell a ghost will stand on in the phase after next — where its next
        /// leap sends it from where it stands now, by the plan of <paramref name="active"/>, against
        /// the player's cell now. A leap a switch sends elsewhere cannot be seen this far ahead.
        /// </summary>
        private static int PredictLeapCell(BattleState state, int unit, TreeSwitch? active)
        {
            var enemy = state.Enemies[unit];
            var plan = active?.LeapPlan ?? enemy.Def.LeapPlan;
            if (plan == null || plan.Count == 0) return enemy.Body.Cell;
            return LeapTarget(state, unit, plan, enemy.LeapIndex).Cell;
        }

        // ---- 亡霊: 予兆が 1 フェーズ遅れる ----

        /// <summary>
        /// §7.5: at the end of a ghost's phase the omen it showed for the phase after next becomes the
        /// one its next phase carries out, with the 予定 and the switch it was decided under.
        /// </summary>
        private static BattleState ShiftLaterOmen(BattleState state, int unit, List<BattleEvent> events)
        {
            var enemy = state.Enemies[unit];
            var omen = enemy.Later ?? EnemyAi.OmenOf(null);
            var plan = enemy.LaterPlan;
            state = state.WithUnit(unit, enemy with
            {
                Omen = omen, Plan = plan, ActiveSwitch = enemy.LaterSwitch,
                Later = null, LaterPlan = null, LaterSwitch = null,
            });
            events.Add(new OmenSet(Actor.Enemy, omen, Decided: true) { Unit = unit });
            if (plan != null) events.Add(new PlanSet(Actor.Enemy, plan, Decided: true) { Unit = unit });
            return state;
        }

        /// <summary>
        /// §7.5 / §9 step 12 for a ghost: the omen of the phase after next. The branch is read at the
        /// gap from the player's cell now to the cell the ghost will stand on then
        /// (<see cref="PredictLeapCell"/>). The stamina is what it will hold then: now, plus a recovery,
        /// less what the next phase's omen and 予定 cost (each only if it can be paid), plus another
        /// recovery. What the next phase will have done is read as far as it is shown: its stance is
        /// spent, a word it puts on with stacks to spare remains, and a NotTwiceRunning action in it is
        /// not taken again (<see cref="EnemyAi.BarredLater"/>).
        /// </summary>
        private static BattleState DecideLaterOmen(BattleState state, int unit, List<BattleEvent> events)
        {
            state = JudgeSwitches(state, unit, events, out var active);
            var enemy = state.Enemies[unit];
            var def = enemy.Def;

            var queuedSwitch = EnemyAi.SwitchById(def, enemy.ActiveSwitch);
            var queued = new List<EnemyActionDef>();
            int stamina = EnemyAi.StaminaAtAction(def, enemy.Body);
            foreach (var step in new[] { enemy.Omen, enemy.Plan })
            {
                if (step == null || step.ActionId == EnemyAi.RestActionId) continue;
                var action = EnemyAi.ActionOf(def, step.ActionId, queuedSwitch);
                queued.Add(action);
                if (Combat.CanPay(action.Cost, stamina)) stamina -= action.Cost;
            }
            stamina = Combat.RecoverStamina(stamina, enemy.Body.MaxStamina, def.Recovery, 0);

            var skip = new List<string>(enemy.Spent);
            foreach (var action in queued)
            {
                if (action.Face.Stance != null) skip.Add(action.Id);
            }
            skip.AddRange(EnemyAi.BarredLater(def, state.Player.Statuses, enemy.Body.Statuses, queued, active, state.Tools.WardedStacks));

            int gap = Field.Gap(state.Player.Cell, state.Player.Size, PredictLeapCell(state, unit, active));
            var later = EnemyAi.DecideOmen(def, gap, stamina, skip, active);
            var laterPlan = EnemyAi.DecidePlan(def, gap, stamina, later, skip, active);
            events.Add(new LaterOmenSet(Actor.Enemy, later, laterPlan, Decided: true) { Unit = unit });
            return state.WithUnit(unit, enemy with { Later = later, LaterPlan = laterPlan, LaterSwitch = active?.Id });
        }
    }
}
