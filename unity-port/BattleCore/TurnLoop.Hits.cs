using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Moves and hits (#584): a mover's own move (<see cref="MoveSelf"/>), what the one hit does back
    /// (見切り and the OnHit stances), the order a push takes its foes in, and who fell.
    /// </summary>
    public static partial class TurnLoop
    {
        /// <summary>
        /// §7.3: 前へ / 後ろへ n of the holder's own, as far as the line allows — a move face, or the
        /// 俊敏 free cell (#48). 呪縛 stops it outright and 鉤爪 catches a move back (#51); the mover's
        /// own 鈍足 takes one cell off. A cell that changes is 移動後 (§2.3) for the rest of the turn
        /// and records the way it went (LastMove, #51: root_m), and an enemy that moves itself pays
        /// each 根縛り the player holds.
        /// </summary>
        private static BattleState MoveSelf(BattleState state, Actor actor, int unit, int move, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);
            if (self.Statuses.Has(StatusKind.Binding))
            {
                // #51 (roster §4.1 呪縛): the holder's own movement effect does not resolve at all.
                events.Add(new MoveBlocked(actor, StatusKind.Binding) { Unit = unit });
                return state;
            }

            int cells = Combat.CellsAfterSlow(Math.Abs(move), self.Statuses);
            if (cells == 0)
            {
                events.Add(new MoveBlocked(actor, StatusKind.Slow) { Unit = unit });
                return state;
            }

            var shift = Field.Move(state, actor, Math.Sign(move) * cells, unit);
            if (shift.To == shift.From) return state;

            if (move < 0 && self.Statuses.Has(StatusKind.Hook))
            {
                // #51 (roster §5.1 鉤爪): a move back that would go is stopped at 0 cells and spends one
                // stack. One that 鈍足 already stopped, or that the edge of the line would stop anyway,
                // is not caught, and keeps the stack.
                state = Consume(state, actor, unit, StatusKind.Hook, events);
                events.Add(new MoveBlocked(actor, StatusKind.Hook) { Unit = unit });
                if (actor == Actor.Player) state = state with { PlayerHistory = state.History.WithHookSnag(state.Turn) };
                return state;
            }

            // 移動後 (§2.3): the holder's own cell changed, so the rest of this turn reads it.
            state = Set(state, actor, unit, self with { Cell = shift.To, Moved = true, LastMove = Math.Sign(move) });
            events.Add(new CellsMoved(actor, shift.From, shift.To, Pushed: false) { Unit = unit });

            // 根縛り: an enemy that moves itself pays for it for each such stance the player holds.
            if (actor == Actor.Enemy)
            {
                foreach (var bind in state.Player.StanceList)
                {
                    if (bind.Def.Hook != StanceHook.BreakOnFoeMove || bind.Def.Break <= 0 || bind.StoppedIn(state.Turn)) continue;
                    events.Add(new StanceFired(Actor.Player, bind.Source, StanceHook.BreakOnFoeMove) { Unit = unit });
                    state = Break(state, Actor.Player, Actor.Enemy, unit, bind.Def.Break, unit, events);
                }
            }
            return state;
        }

        /// <summary>
        /// What the one hit does back, once the blow has landed and it still stands: 見切り returns
        /// half of what its Guard absorbed (rounded up; spent only when there was something to
        /// return), then an OnHit stance reacts (司祭の祈り, 槍衾).
        /// </summary>
        private static BattleState OnHitReactions(
            BattleState state, Actor attacker, int attackerUnit, Actor victim, int victimUnit, int absorbed, List<BattleEvent> events)
        {
            int enemyUnit = victim == Actor.Enemy ? victimUnit : attackerUnit;

            var hurt = Get(state, victim, victimUnit);
            if (hurt.Statuses.Has(StatusKind.Parry) && absorbed > 0)
            {
                state = Consume(state, victim, victimUnit, StatusKind.Parry, events);
                int raw = Half(absorbed);
                var striker = Get(state, attacker, attackerUnit);
                var (damage, guardAfter, soaked) = Combat.ApplyGuard(raw, striker.Guard);
                striker = striker with { Guard = guardAfter, Hp = Math.Max(0, striker.Hp - damage) };
                state = Set(state, attacker, attackerUnit, striker);
                events.Add(new Reflected(victim, attacker, raw, soaked, damage, striker.Guard, striker.Hp) { Unit = enemyUnit });
            }

            // §4 (v4.4): each OnHit stance on the list reacts on its own; OncePerTurn is counted per entry.
            int entries = Get(state, victim, victimUnit).StanceList.Count;
            for (int i = 0; i < entries; i++)
            {
                hurt = Get(state, victim, victimUnit);
                var entry = hurt.StanceList[i];
                var stance = entry.Def;
                if (stance.Hook != StanceHook.OnHit || entry.StoppedIn(state.Turn)) continue;
                if (stance.OncePerTurn && entry.ReactedTurn == state.Turn) continue;

                events.Add(new StanceFired(victim, entry.Source, StanceHook.OnHit) { Unit = enemyUnit });
                var reacted = new List<StanceEntry>(hurt.StanceList);
                reacted[i] = entry with { ReactedTurn = state.Turn };
                state = Set(state, victim, victimUnit, hurt with { Stances = reacted });
                if (stance.Stamina > 0) state = GainStamina(state, victim, victimUnit, stance.Stamina, events);
                if (stance.Guard > 0)
                {
                    hurt = Get(state, victim, victimUnit);
                    hurt = hurt with { Guard = hurt.Guard + stance.Guard };
                    state = Set(state, victim, victimUnit, hurt);
                    events.Add(new GuardGained(victim, stance.Guard, hurt.Guard) { Unit = enemyUnit });
                }
                if (stance.Status.HasValue && stance.StatusStacks > 0 && !Combat.IsDefeated(Get(state, attacker, attackerUnit).Hp))
                {
                    state = ApplyStatus(state, victim, attacker, attackerUnit, enemyUnit, stance.Status.Value, stance.StatusStacks, events);
                }
            }
            return state;
        }

        /// <summary>A push moves the farthest foe first, a pull the nearest first; the player is a single foe.</summary>
        private static IReadOnlyList<int> PushOrder(BattleState state, Actor foeSide, IReadOnlyList<int> hit, int push)
        {
            if (foeSide == Actor.Player || hit.Count < 2) return hit;
            var order = new List<int>(hit);
            order.Sort((a, b) => state.Enemies[a].Body.Cell.CompareTo(state.Enemies[b].Body.Cell));
            if (push > 0) order.Reverse();
            return order;
        }

        /// <summary>
        /// §17.6 F9: after a hit, see who fell. Over is true when that ended the battle (the player,
        /// or the last standing enemy); the caller stops resolving. An enemy that falls while others
        /// stand leaves its cells and its omen, and the card goes on.
        /// </summary>
        private static BattleState OnFall(BattleState state, Actor side, int unit, List<BattleEvent> events, out bool over)
        {
            over = false;
            if (!Combat.IsDefeated(Get(state, side, unit).Hp)) return state;
            if (side == Actor.Player || state.Living.Count == 0)
            {
                over = true;
                return state;
            }
            events.Add(new EnemyDefeated(Actor.Enemy) { Unit = unit });
            return state.WithOmen(unit, null);
        }

        /// <summary>Half, rounded up: 転換 and 見切り.</summary>
        private static int Half(int value) => value <= 0 ? 0 : (value + 1) / 2;
    }
}
