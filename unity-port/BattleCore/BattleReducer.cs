using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>Turn flow of battle_core_v3.md §9. Pure: (state, action, rng) → state.</summary>
    public static class BattleReducer
    {
        private static BattleState WithLogs(BattleState state, IReadOnlyList<string> texts, IReadOnlyList<BattleEvent> events)
        {
            var newLog = new List<LogEntry>(state.Log);
            for (int i = 0; i < texts.Count; i++)
            {
                newLog.Add(new LogEntry(state.LogSeq + i, texts[i]));
            }
            return state with { Log = newLog, LogSeq = state.LogSeq + texts.Count, Events = events };
        }

        public static BattleState InitState(IRng rng) => InitState(rng, new BattleInit());

        public static BattleState InitState(IRng rng, BattleInit init)
        {
            int maxStamina = Math.Max(Constants.MaxStaminaFloor, Math.Min(Constants.MaxStaminaCeil, init.PlayerMaxStamina));
            int stamina = Math.Max(0, Math.Min(maxStamina, init.PlayerStamina ?? maxStamina));
            int distance = Combat.ClampDistance(init.InitialDistanceIndex);

            var shuffled = Cards.Shuffle(Cards.CreateInitialDeck(), rng);
            var drawn = Cards.DrawToHandSize(
                shuffled, new List<CardInstance>(), new List<CardInstance>(), Constants.HandSize, rng);
            var omen = Enemy.ChooseOmen(distance, Enemy.Def.MaxStamina);

            var state = new BattleState(
                Turn: 1,
                DistanceIndex: distance,
                PlayerHp: Math.Max(1, Math.Min(init.PlayerMaxHp, init.PlayerHp)),
                PlayerMaxHp: init.PlayerMaxHp,
                PlayerStamina: stamina,
                PlayerMaxStamina: maxStamina,
                PlayerGuard: 0,
                PendingBonusRecovery: 0,
                EnemyHp: Enemy.Def.MaxHp,
                EnemyMaxHp: Enemy.Def.MaxHp,
                EnemyStamina: Enemy.Def.MaxStamina,
                EnemyMaxStamina: Enemy.Def.MaxStamina,
                EnemyGuard: 0,
                Omen: omen,
                Hand: drawn.Hand,
                DrawPile: drawn.DrawPile,
                DiscardPile: drawn.DiscardPile,
                Log: new List<LogEntry>(),
                LogSeq: 0,
                Events: new List<BattleEvent>(),
                Result: GameResult.Ongoing,
                Init: init);

            var events = new List<BattleEvent>
            {
                new TurnStartedEvent(1, Combat.IndexToRange(distance), 0, 0),
                new CardsDrawnEvent(drawn.Hand.Count),
                new OmenDeclaredEvent(omen),
            };
            return WithLogs(state, new[]
            {
                $"戦闘開始。間合いは「{Constants.RangeLabel[Combat.IndexToRange(distance)]}」。",
                OmenLog(omen),
            }, events);
        }

        private static string OmenLog(Omen omen)
        {
            var action = Enemy.Actions[omen.ActionId];
            string range = omen.TargetRange.HasValue ? $"・狙い {Constants.RangeLabel[omen.TargetRange.Value]}" : "";
            return $"予兆: {action.Name}{range}。";
        }

        // ---- §9 step 5: play a card with an invest ----

        private static BattleState PlayCard(BattleState state, string instanceId, int invest)
        {
            if (state.Result != GameResult.Ongoing) return state;
            var card = state.Hand.FirstOrDefault(c => c.InstanceId == instanceId);
            if (card == null) return state;
            var def = card.Def;
            if (invest < def.MinInvest || invest > Constants.MaxInvest) return state;
            if (state.PlayerStamina < invest) return state;

            var tier = def.Tiers[invest];
            var events = new List<BattleEvent> { new CardPlayedEvent(instanceId, def.Id, def.Name, invest) };
            var logs = new List<string>();

            int enemyHp = state.EnemyHp;
            int enemyGuard = state.EnemyGuard;
            int enemyStamina = state.EnemyStamina;
            int playerHp = state.PlayerHp;
            int playerGuard = state.PlayerGuard;
            int distance = state.DistanceIndex;
            int diff = -1;
            string investText = $"（投入 {invest}）";

            // 1. damage
            if (def.Type == CardType.Attack && def.EffectiveRange.HasValue)
            {
                var eff = def.EffectiveRange.Value;
                diff = Combat.RangeDiff(distance, eff);
                double mult = Combat.RangeMultiplier(distance, eff);
                bool desperate = Combat.IsDesperate(def, state.PlayerStamina);
                int raw = Combat.ComputeAttackDamage(tier.Power, eff, distance, desperate);
                var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw, enemyGuard);
                enemyHp = Math.Max(0, enemyHp - damage);
                enemyGuard = guardAfter;
                events.Add(new AttackResolvedEvent(Actor.Player, def.Name, tier.Power, mult, diff, desperate, raw, absorbed, damage, enemyHp));
                string log = $"「{def.Name}」{investText}で {damage} ダメージ";
                if (absorbed > 0) log += $"（敵の Guard で {absorbed} 軽減）";
                if (diff == 1) log += "（間合い不適 ×0.5）";
                else if (diff >= Constants.WhiffDiff) log += "（空振り ×0.15）";
                log += "。";
                logs.Add(log);
            }
            else if (def.Type == CardType.Guard)
            {
                playerGuard += tier.Guard;
                events.Add(new GuardGainedEvent(Actor.Player, tier.Guard, playerGuard, def.Name));
                logs.Add($"「{def.Name}」{investText}で受けを固めた（Guard +{tier.Guard}）。");
            }
            else if (def.Type == CardType.Heal)
            {
                int heal = Math.Min(tier.Heal, state.PlayerMaxHp - playerHp);
                playerHp += heal;
                events.Add(new HealedEvent(Actor.Player, heal, playerHp));
                logs.Add($"「{def.Name}」{investText}で HP {heal} 回復。");
            }
            else
            {
                logs.Add($"「{def.Name}」{investText}。");
            }

            // 2. move
            if (tier.Shift != 0)
            {
                int after = Combat.ShiftDistance(distance, tier.Shift);
                bool clamped = after == distance;
                events.Add(new MovedEvent(Actor.Player, distance, after, clamped));
                if (clamped) logs.Add(tier.Shift < 0 ? "これ以上詰められない。" : "これ以上退けない。");
                else logs.Add($"間合いが「{Constants.RangeLabel[Combat.IndexToRange(after)]}」に。");
                distance = after;
            }

            // 3. additional effects: always for move / heal, diff 0 only for attacks (§2.3)
            bool extrasApply = def.Type != CardType.Attack || diff == 0;
            if (extrasApply && def.Type != CardType.Guard && tier.Guard > 0)
            {
                playerGuard += tier.Guard;
                events.Add(new GuardGainedEvent(Actor.Player, tier.Guard, playerGuard, def.Name));
                logs.Add($"Guard +{tier.Guard}。");
            }
            if (extrasApply && tier.BreakStamina > 0)
            {
                enemyStamina = Math.Max(0, enemyStamina - tier.BreakStamina);
                events.Add(new StaminaBrokenEvent(Actor.Enemy, tier.BreakStamina, enemyStamina));
                logs.Add($"敵を崩した（スタミナ −{tier.BreakStamina}）。");
            }

            // stamina, calm
            int playerStamina = state.PlayerStamina - invest;
            int pendingBonus = state.PendingBonusRecovery;
            if (Combat.CalmTriggers(def, playerStamina))
            {
                pendingBonus += def.Reserve!.Bonus;
                events.Add(new CalmTriggeredEvent(def.Name, def.Reserve.Bonus));
                logs.Add($"冷静: 次ターンの回復 +{def.Reserve.Bonus}。");
            }

            var hand = state.Hand.Where(c => c.InstanceId != instanceId).ToList();
            var discard = new List<CardInstance>(state.DiscardPile) { card };

            var result = state.Result;
            if (enemyHp <= 0)
            {
                result = GameResult.Won;
                events.Add(new BattleEndedEvent(GameResult.Won));
                logs.Add($"敵「{Enemy.Def.Name}」を打ち倒した。");
            }

            var next = state with
            {
                EnemyHp = enemyHp,
                EnemyGuard = enemyGuard,
                EnemyStamina = enemyStamina,
                PlayerHp = playerHp,
                PlayerGuard = playerGuard,
                PlayerStamina = playerStamina,
                PendingBonusRecovery = pendingBonus,
                DistanceIndex = distance,
                Hand = hand,
                DiscardPile = discard,
                Result = result,
            };
            return WithLogs(next, logs, events);
        }

        // ---- §9 steps 6–11 + next turn start ----

        private static BattleState EndTurn(BattleState state, IRng rng)
        {
            if (state.Result != GameResult.Ongoing) return state;
            var events = new List<BattleEvent>();
            var logs = new List<string>();

            // 6. reserve (構え)
            int playerGuard = state.PlayerGuard;
            int reserve = Combat.ReserveGuard(state.PlayerStamina);
            if (reserve > 0)
            {
                playerGuard += reserve;
                events.Add(new ReserveGuardEvent(Actor.Player, state.PlayerStamina, reserve));
                logs.Add($"構え: 残 {state.PlayerStamina} で Guard +{reserve}。");
            }

            // 7. discard hand
            var discard = new List<CardInstance>(state.DiscardPile);
            discard.AddRange(state.Hand);
            if (state.Hand.Count > 0) events.Add(new HandDiscardedEvent(state.Hand.Count));

            // 8. enemy phase: guard to 0, recovery
            var band = Combat.IndexToRange(state.DistanceIndex);
            int enemyRecovery = Combat.StaminaRecovery(band);
            int enemyStamina = Math.Min(state.EnemyMaxStamina, state.EnemyStamina + enemyRecovery);
            events.Add(new EnemyPhaseStartedEvent(band, enemyRecovery));
            logs.Add($"敵の番。間合い「{Constants.RangeLabel[band]}」でスタミナ回復（+{enemyRecovery} → {enemyStamina}）。");

            // 9. execute the omen
            var omen = state.Omen ?? Enemy.ChooseOmen(state.DistanceIndex, enemyStamina);
            var outcome = Enemy.ResolveOmen(omen, state.DistanceIndex, enemyStamina, 0, playerGuard, state.PlayerStamina);
            int playerHp = Math.Max(0, state.PlayerHp - outcome.Damage);
            foreach (var ev in outcome.Events)
            {
                events.Add(ev is AttackResolvedEvent hit ? hit with { TargetHpAfter = playerHp } : ev);
            }
            logs.AddRange(outcome.Logs);
            enemyStamina -= outcome.Invest;
            int enemyGuard = outcome.EnemyGuardAfter;
            int playerStamina = outcome.PlayerStaminaAfter;
            int distance = outcome.DistanceAfter;

            // 10. defeat
            if (playerHp <= 0)
            {
                events.Add(new BattleEndedEvent(GameResult.Lost));
                logs.Add("力尽きた。");
                var lost = state with
                {
                    Hand = new List<CardInstance>(),
                    DiscardPile = discard,
                    EnemyStamina = enemyStamina,
                    EnemyGuard = enemyGuard,
                    PlayerHp = 0,
                    PlayerGuard = outcome.PlayerGuardAfter,
                    PlayerStamina = playerStamina,
                    DistanceIndex = distance,
                    Result = GameResult.Lost,
                };
                return WithLogs(lost, logs, events);
            }

            // enemy reserve (symmetric 構え)
            int enemyReserve = Combat.ReserveGuard(enemyStamina);
            if (enemyReserve > 0)
            {
                enemyGuard += enemyReserve;
                events.Add(new ReserveGuardEvent(Actor.Enemy, enemyStamina, enemyReserve));
                logs.Add($"敵の構え: Guard +{enemyReserve}。");
            }

            // 11. next omen
            var nextOmen = Enemy.ChooseOmen(distance, enemyStamina);
            events.Add(new OmenDeclaredEvent(nextOmen));
            logs.Add(OmenLog(nextOmen));

            // player turn start (§9 steps 1–4)
            int turn = state.Turn + 1;
            var playerBand = Combat.IndexToRange(distance);
            int recovery = Combat.StaminaRecovery(playerBand);
            int bonus = state.PendingBonusRecovery;
            int playerStaminaNext = Math.Min(state.PlayerMaxStamina, playerStamina + recovery + bonus);
            var drawn = Cards.DrawToHandSize(
                state.DrawPile, discard, new List<CardInstance>(), Constants.HandSize, rng);
            events.Add(new TurnStartedEvent(turn, playerBand, recovery, bonus));
            events.Add(new CardsDrawnEvent(drawn.Hand.Count));
            string bonusText = bonus > 0 ? $" +{bonus}（冷静）" : "";
            logs.Add($"ターン {turn}。間合い「{Constants.RangeLabel[playerBand]}」で回復（+{recovery}{bonusText} → {playerStaminaNext}）。");

            var next = state with
            {
                Turn = turn,
                DistanceIndex = distance,
                PlayerHp = playerHp,
                PlayerStamina = playerStaminaNext,
                PlayerGuard = 0,
                PendingBonusRecovery = 0,
                EnemyStamina = enemyStamina,
                EnemyGuard = enemyGuard,
                Omen = nextOmen,
                Hand = drawn.Hand,
                DrawPile = drawn.DrawPile,
                DiscardPile = drawn.DiscardPile,
                Result = GameResult.Ongoing,
            };
            return WithLogs(next, logs, events);
        }

        public static BattleState Reduce(BattleState state, BattleAction action, IRng rng)
        {
            return action switch
            {
                PlayCardAction play => PlayCard(state, play.InstanceId, play.Invest),
                EndTurnAction => EndTurn(state, rng),
                RestartAction => InitState(rng, state.Init),
                _ => state,
            };
        }
    }
}
