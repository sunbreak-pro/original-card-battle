using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// The turn start and the statuses (#584): §9 steps 1-3 and 9 for either side
    /// (<see cref="OpenTurnFor"/>), and the pieces that give, spend and ward a status or move stamina.
    /// </summary>
    public static partial class TurnLoop
    {
        /// <summary>
        /// §9 steps 1-3 for the player, step 9 for an enemy: the same things in the same order.
        /// Step 2 is the recovery, less 1 for 疲労 and plus a turn-start stance's recovery (水の構え);
        /// step 3 is the stance's Guard, then 再生, then 出血, then the ターンで減る型 ticks, then 俊敏
        /// (#48). The holder may fall to 出血 here; the caller looks.
        ///
        /// 俊敏 reads the stacks held before the tick, as 再生 and 出血 do, so n stacks give n turns
        /// of it. The player's step is opened (<see cref="CombatantState.FreeStep"/>) and taken in
        /// <see cref="TakeFreeStep"/>; an enemy takes its step here, toward the reach of the omen it is
        /// about to carry out (<see cref="EnemyAi.FreeStepDirection"/>). One that fell to 出血 does not.
        /// </summary>
        private static BattleState OpenTurnFor(BattleState state, Actor actor, int unit, int recovery, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);

            events.Add(new GuardCleared(actor, self.Guard) { Unit = unit });
            self = self with { Guard = 0, Played = null, Moved = false, LastMove = 0, FreeStep = false };

            // §4 (v4.4): every turn-start stance on the list fires on its own, so two of one work twice.
            var firing = new List<StanceEntry>();
            foreach (var entry in self.StanceList)
            {
                if (entry.Def.Hook == StanceHook.TurnStart && !entry.StoppedIn(state.Turn) && StanceHolds(state, actor, unit, entry.Def)) firing.Add(entry);
            }
            foreach (var entry in firing) events.Add(new StanceFired(actor, entry.Source, StanceHook.TurnStart) { Unit = unit });

            int fatigue = self.Statuses.Has(StatusKind.Fatigue) ? Constants.FatiguePenalty : 0;
            int stanceRecovery = firing.Sum(entry => entry.Def.Recovery);
            // #51 (roster §5.1 深み, §6.2 枯らし): the boss words that thin the recovery, floor 0. The
            // 温存 bonus is deliberately inside that floor, as 疲労 is: RecoverStamina floors
            // recovery + bonus together, so a bonus first fills what the loss took. #400: 枯らし stops
            // at a recovery of 1 counted the same way (疲労 and the bonus in), 深み does not.
            int withered = Statuses.RecoveryLoss(
                self.Statuses,
                adjacent: OwnGap(state, actor, unit) == 0 && state.Nearest >= 0,
                recoveryBefore: recovery + stanceRecovery - fatigue + self.NextTurnRecoveryBonus);
            int staminaAfter = Combat.RecoverStamina(
                self.Stamina, self.MaxStamina, recovery + stanceRecovery - fatigue - withered, self.NextTurnRecoveryBonus);
            events.Add(new StaminaRecovered(actor, staminaAfter - self.Stamina, staminaAfter, self.MaxStamina) { Unit = unit });
            self = self with { Stamina = staminaAfter, NextTurnRecoveryBonus = 0 };

            foreach (var entry in firing)
            {
                if (entry.Def.Guard <= 0) continue;
                self = self with { Guard = self.Guard + entry.Def.Guard };
                events.Add(new GuardGained(actor, entry.Def.Guard, self.Guard) { Unit = unit });
            }

            int regen = self.Statuses.Stacks(StatusKind.Regen);
            if (regen > 0)
            {
                int hpAfter = Math.Min(self.MaxHp, self.Hp + (regen * Constants.RegenPerStack));
                events.Add(new StatusHpChanged(actor, StatusKind.Regen, hpAfter - self.Hp, hpAfter) { Unit = unit });
                self = self with { Hp = hpAfter };
            }
            int bleed = self.Statuses.Stacks(StatusKind.Bleed);
            if (bleed > 0)
            {
                int hpAfter = Math.Max(0, self.Hp - (bleed * Constants.BleedPerStack));
                events.Add(new StatusHpChanged(actor, StatusKind.Bleed, hpAfter - self.Hp, hpAfter) { Unit = unit });
                self = self with { Hp = hpAfter };
            }

            // #51 (roster §6.2 根張り): the player ended the last two turns on the same cell. Only the
            // player's turns are kept in the history, and only a boss puts the word on the player.
            int rooted = self.Statuses.Stacks(StatusKind.Rooting);
            if (rooted > 0 && actor == Actor.Player && self.Hp > 0 && Statuses.StayedTwoTurns(state.History.TurnEnds))
            {
                int hpAfter = Math.Max(0, self.Hp - (rooted * Statuses.RootingHpPerStack));
                events.Add(new StatusHpChanged(actor, StatusKind.Rooting, hpAfter - self.Hp, hpAfter) { Unit = unit });
                self = self with { Hp = hpAfter };
            }

            bool swift = self.Statuses.Has(StatusKind.Swift);
            var ticked = self.Statuses.TickTurnStart();
            foreach (var kind in self.Statuses.Kinds)
            {
                if (Statuses.DecayOf(kind) != StatusDecay.OnTurn) continue;
                events.Add(new StatusTicked(actor, kind, ticked.Stacks(kind)) { Unit = unit });
            }
            self = self with { Statuses = ticked };
            state = Set(state, actor, unit, self);

            if (!swift || Combat.IsDefeated(self.Hp)) return state;
            if (actor == Actor.Player) return Set(state, actor, unit, self with { FreeStep = true });

            var enemy = state.Enemies[unit];
            var omened = enemy.Omen == null ? null : EnemyAi.ActionToExecute(enemy.Def, enemy.Omen, self.Stamina);
            int direction = EnemyAi.FreeStepDirection(omened, state.GapTo(unit));
            if (direction == 0) return state;
            events.Add(new FreeStepTaken(actor, direction) { Unit = unit });
            return MoveSelf(state, actor, unit, direction, events);
        }

        private static BattleState GainStamina(BattleState state, Actor actor, int unit, int amount, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);
            // §9.1 (#452): stamina 息継ぎの笛 took past the maximum is kept, but a gain adds nothing to it.
            int staminaAfter = self.Stamina >= self.MaxStamina ? self.Stamina : Math.Min(self.MaxStamina, self.Stamina + amount);
            events.Add(new StaminaGained(actor, staminaAfter - self.Stamina, staminaAfter) { Unit = unit });
            return Set(state, actor, unit, self with { Stamina = staminaAfter });
        }

        /// <summary>崩し: <paramref name="target"/> loses stamina (floor 0).</summary>
        private static BattleState Break(
            BattleState state, Actor breaker, Actor target, int targetUnit, int amount, int eventUnit, List<BattleEvent> events)
        {
            var other = Get(state, target, targetUnit);
            int staminaAfter = Math.Max(0, other.Stamina - amount);
            events.Add(new StaminaBroken(breaker, target, other.Stamina - staminaAfter, staminaAfter) { Unit = eventUnit });
            return Set(state, target, targetUnit, other with { Stamina = staminaAfter });
        }

        /// <summary>§5 使うと減る型: one stack spent.</summary>
        private static BattleState Consume(BattleState state, Actor holder, int unit, StatusKind kind, List<BattleEvent> events)
        {
            var self = Get(state, holder, unit);
            var after = self.Statuses.Consume(kind);
            events.Add(new StatusConsumed(holder, kind, after.Stacks(kind)) { Unit = unit });
            return Set(state, holder, unit, self with { Statuses = after });
        }

        /// <summary>
        /// §5: stacks on one side; the player takes on at most six kinds, an enemy any number. A
        /// ターンで減る型 word stops at four on either side and the rest is dropped (#205): that is
        /// not a refusal, so the event says how much was dropped instead.
        /// </summary>
        private static BattleState ApplyStatus(
            BattleState state, Actor by, Actor target, int targetUnit, int eventUnit, StatusKind kind, int stacks, List<BattleEvent> events,
            int cap = 0)
        {
            int? limit = target == Actor.Player ? Constants.StatusKindsPlayer : (int?)null;
            var other = Get(state, target, targetUnit);
            int before = other.Statuses.Stacks(kind);

            // §9.1 竜語の符 (#452): a boss-only word sits the ward's stacks lower on the player.
            int warded = 0;
            if (target == Actor.Player && Statuses.IsBossOnly(kind) && state.Tools.BossWordWard > 0)
            {
                state = Ward(state, kind, stacks, cap, before, out warded, out cap);
            }

            // #51: a grant's own cap (the boss words, roster §4.1 / §5.1 / §6.2) drops what would go past
            // it, the same way the turn-decay cap does; a word already at it takes nothing.
            // giving is 0 only when the word is already held at the cap or the ward kept it all off,
            // so neither is a refusal.
            int offered = stacks - warded;
            int giving = cap > 0 ? Math.Min(offered, Math.Max(0, cap - before)) : offered;
            var after = giving > 0 ? other.Statuses.Add(kind, giving, limit) : other.Statuses;
            bool refused = giving > 0 && !after.Has(kind);
            int dropped = refused ? 0 : offered - (after.Stacks(kind) - before);
            events.Add(new StatusApplied(by, target, kind, stacks, after.Stacks(kind), refused) { Unit = eventUnit, Dropped = dropped, Warded = warded });
            state = Set(state, target, targetUnit, other with { Statuses = after });

            // #51 (roster §6.4 root_sk): what the player has put on an enemy, counted by turn.
            if (by == Actor.Player && target == Actor.Enemy && after.Stacks(kind) > before)
            {
                state = state with { PlayerHistory = state.History.WithInfliction(state.Turn) };
            }

            // #51 (battle_core_v4 §6.2 瘴気纏い): each stack lowers the holder's maximum stamina by Statuses.MiasmaShroudMaxStaminaPerStack, and
            // what it holds is cut to the new maximum.
            if (kind == StatusKind.MiasmaShroud && after.Stacks(kind) > before)
            {
                var holder = Get(state, target, targetUnit);
                int lowered = (after.Stacks(kind) - before) * Statuses.MiasmaShroudMaxStaminaPerStack;
                int maxAfter = Math.Max(Constants.MaxStaminaFloor, holder.MaxStamina - lowered);
                int staminaAfter = Math.Min(holder.Stamina, maxAfter);
                events.Add(new MaxStaminaChanged(target, maxAfter - holder.MaxStamina, maxAfter, staminaAfter) { Unit = eventUnit });
                state = Set(state, target, targetUnit, holder with { MaxStamina = maxAfter, Stamina = staminaAfter });
            }
            return state;
        }

        /// <summary>
        /// §9.1 竜語の符 (#452): how many of <paramref name="stacks"/> the ward keeps off a boss-only
        /// word headed for the player (<paramref name="warded"/>), and the cap the rest then meets
        /// (<paramref name="wardedCap"/>, 0 for none).
        ///
        /// A 戦闘終了まで word (瘴気纏い / 深み / 根張り / 枯らし) never leaves, so it sits exactly the
        /// ward lower than it would: the first ward-many stacks given this battle are kept off
        /// (counted in <see cref="CarriedTools.Warded"/>) and its cap is the ward lower; a cap the ward
        /// takes to 0 lets nothing on. What the lowered cap stops is counted as warded, not dropped.
        /// A word that wears off (呪縛 by the turn, 鉤爪 by use) comes and goes, so every grant of it
        /// is the ward lower, floor 0.
        /// </summary>
        private static BattleState Ward(BattleState state, StatusKind kind, int stacks, int cap, int before, out int warded, out int wardedCap)
        {
            var tools = state.Tools;
            int ward = tools.BossWordWard;
            wardedCap = cap;
            if (Statuses.DecayOf(kind) != StatusDecay.Lasting)
            {
                warded = Math.Min(stacks, ward);
                return state;
            }

            int keptOff = Math.Min(stacks, Math.Max(0, ward - tools.WardedStacks.Stacks(kind)));
            warded = keptOff;
            if (cap > 0)
            {
                int lowered = cap - ward;
                if (lowered <= 0)
                {
                    warded = stacks;
                }
                else
                {
                    int offered = stacks - keptOff;
                    int underCap = Math.Min(offered, Math.Max(0, cap - before));
                    int underLowered = Math.Min(offered, Math.Max(0, lowered - before));
                    warded = keptOff + (underCap - underLowered);
                    wardedCap = lowered;
                }
            }
            if (keptOff > 0)
            {
                state = state with { PlayerTools = tools with { Warded = tools.WardedStacks.Add(kind, keptOff) } };
            }
            return state;
        }

        /// <summary>
        /// §9.1 竜鱗の欠片 (#452): the first enemy blow or wall damage that reaches the player this
        /// battle meets the scale's Guard first. The Guard is gained just before it resolves (a
        /// <see cref="GuardGained"/> on the player) and then behaves as any Guard: what the blow does
        /// not take stays until the player's next turn start (§9 step 1).
        /// </summary>
        private static BattleState RaiseScale(BattleState state, Actor victim, List<BattleEvent> events)
        {
            if (victim != Actor.Player) return state;
            int scale = state.Tools.ScaleGuard;
            if (scale <= 0) return state;
            var player = state.Player with { Guard = state.Player.Guard + scale };
            events.Add(new GuardGained(Actor.Player, scale, player.Guard));
            return state with { Player = player, PlayerTools = state.Tools with { ScaleGuard = 0 } };
        }
    }
}
