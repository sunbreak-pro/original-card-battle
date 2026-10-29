using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// The permanent effects (§4, #584): stopping and resuming one, the turn-end stance, and the
    /// conditions and bonuses a stance reads, on its own or on the foe a blow lands on.
    /// </summary>
    public static partial class TurnLoop
    {
        /// <summary>
        /// #334 (roster §6.4 root_st, battle_core_v4 §4): the holder's newest permanent effect — the
        /// last entry on its list, which is kept in the order the entries were put on — does nothing
        /// for the rest of this turn and the whole next one. One already stopped is stopped again,
        /// to the later turn. A holder with no permanent effect loses nothing, and nothing is said.
        /// </summary>
        private static BattleState StopNewestStance(BattleState state, Actor holder, int unit, int eventUnit, List<BattleEvent> events)
        {
            var self = Get(state, holder, unit);
            var list = self.StanceList;
            if (list.Count == 0) return state;

            int newest = list.Count - 1;
            int through = state.Turn + 1;
            var stopped = new List<StanceEntry>(list);
            stopped[newest] = list[newest] with { StoppedThrough = through };
            events.Add(new StanceStopped(holder, list[newest].Source, newest, through) { Unit = eventUnit });
            return Set(state, holder, unit, self with { Stances = stopped });
        }

        /// <summary>
        /// #334: at the start of a turn, every permanent effect (the player's and each enemy's) whose
        /// stop ran through the turn before works again, and says so.
        /// </summary>
        private static BattleState ResumeStances(BattleState state, List<BattleEvent> events)
        {
            state = ResumeStancesOf(state, Actor.Player, 0, 0, events);
            for (int i = 0; i < state.Enemies.Count; i++)
            {
                if (state.Enemies[i].Alive) state = ResumeStancesOf(state, Actor.Enemy, i, i, events);
            }
            return state;
        }

        private static BattleState ResumeStancesOf(BattleState state, Actor holder, int unit, int eventUnit, List<BattleEvent> events)
        {
            var self = Get(state, holder, unit);
            var list = self.StanceList;
            List<StanceEntry>? resumed = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].StoppedThrough == 0 || list[i].StoppedIn(state.Turn)) continue;
                resumed ??= new List<StanceEntry>(list);
                resumed[i] = list[i] with { StoppedThrough = 0 };
                events.Add(new StanceResumed(holder, list[i].Source, i) { Unit = eventUnit });
            }
            return resumed == null ? state : Set(state, holder, unit, self with { Stances = resumed });
        }

        /// <summary>§4: the turn-end stance (根渡り, 霞み足), before 構え.</summary>
        private static BattleState StanceAtTurnEnd(BattleState state, Actor actor, int unit, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);
            foreach (var entry in self.StanceList)
            {
                var stance = entry.Def;
                if (stance.Hook != StanceHook.TurnEnd || entry.StoppedIn(state.Turn) || !StanceHolds(state, actor, unit, stance)) continue;

                events.Add(new StanceFired(actor, entry.Source, StanceHook.TurnEnd) { Unit = unit });
                if (stance.Guard > 0)
                {
                    self = self with { Guard = self.Guard + stance.Guard };
                    events.Add(new GuardGained(actor, stance.Guard, self.Guard) { Unit = unit });
                }
                if (stance.NextRecovery > 0) self = self with { NextTurnRecoveryBonus = self.NextTurnRecoveryBonus + stance.NextRecovery };
            }
            return Set(state, actor, unit, self);
        }

        /// <summary>
        /// §4 `when` for the stances that do not look at one foe: the gap is the nearest enemy's for
        /// the player and the player's for an enemy.
        /// </summary>
        private static bool StanceHolds(BattleState state, Actor actor, int unit, StanceDef stance)
        {
            switch (stance.When)
            {
                case StanceWhen.Always: return true;
                case StanceWhen.GapAtLeast: return OwnGap(state, actor, unit) >= stance.Threshold;
                case StanceWhen.GapAtMost: return OwnGap(state, actor, unit) <= stance.Threshold;
                case StanceWhen.MovedThisTurn: return Get(state, actor, unit).Moved;
                case StanceWhen.OmenAttack:
                    if (actor != Actor.Player) return false;
                    foreach (int i in state.Living)
                    {
                        var omen = state.Enemies[i].Omen;
                        if (omen != null && omen.Label.Kind == OmenKind.Attack) return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>§4 条件付き加算: the power an AttackBonus stance adds to a blow on this foe. 0 when it does not hold.</summary>
        private static int StanceAttackBonus(BattleState state, Actor actor, int unit, Actor foeSide, int foe, List<BattleEvent>? events = null)
        {
            int total = 0;
            foreach (var entry in Get(state, actor, unit).StanceList)
            {
                var stance = entry.Def;
                if (stance.Hook != StanceHook.AttackBonus || entry.StoppedIn(state.Turn) || !StanceHoldsOn(state, actor, unit, stance, foeSide, foe)) continue;
                total += stance.Power;
                if (stance.Power > 0 && events != null)
                {
                    events.Add(new StanceFired(actor, entry.Source, StanceHook.AttackBonus) { Unit = foeSide == Actor.Enemy ? foe : unit });
                }
            }
            return total;
        }

        /// <summary>
        /// §4 攻撃ごとの状態付与 (#253): a StatusOnAttack stance puts its word on the foe a blow has
        /// just landed on, when the condition holds for that foe. The caller has checked the foe stands.
        /// </summary>
        private static BattleState StanceStatusOnAttack(
            BattleState state, Actor actor, int unit, Actor foeSide, int foe, List<BattleEvent> events)
        {
            int eventUnit = foeSide == Actor.Enemy ? foe : unit;
            foreach (var entry in Get(state, actor, unit).StanceList)
            {
                var stance = entry.Def;
                if (stance.Hook != StanceHook.StatusOnAttack || !stance.Status.HasValue || stance.StatusStacks <= 0) continue;
                if (entry.StoppedIn(state.Turn)) continue;
                if (!StanceHoldsOn(state, actor, unit, stance, foeSide, foe)) continue;
                if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) break;

                events.Add(new StanceFired(actor, entry.Source, StanceHook.StatusOnAttack) { Unit = eventUnit });
                state = ApplyStatus(state, actor, foeSide, foe, eventUnit, stance.Status.Value, stance.StatusStacks, events);
            }
            return state;
        }

        /// <summary>§4 `when` for the stances that look at the one foe hit (AttackBonus, StatusOnAttack).</summary>
        private static bool StanceHoldsOn(BattleState state, Actor actor, int unit, StanceDef stance, Actor foeSide, int foe)
        {
            int gap = actor == Actor.Player ? state.GapTo(foe) : state.GapTo(unit);
            return stance.When switch
            {
                StanceWhen.Always => true,
                StanceWhen.GapAtMost => gap <= stance.Threshold,
                StanceWhen.GapAtLeast => gap >= stance.Threshold,
                StanceWhen.MovedThisTurn => Get(state, actor, unit).Moved,
                StanceWhen.TargetHasStatus => stance.Status.HasValue && Get(state, foeSide, foe).Statuses.Has(stance.Status.Value),
                _ => false,
            };
        }

        private static int OwnGap(BattleState state, Actor actor, int unit)
        {
            if (actor == Actor.Enemy) return state.GapTo(unit);
            int nearest = state.Nearest;
            return nearest < 0 ? 0 : state.GapTo(nearest);
        }
    }
}
