using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>What one enemy phase did (§9 steps 9–11), before it is folded into the state.</summary>
    public sealed record EnemyOutcome(
        EnemyAction? Action,
        int Invest,
        bool Whiffed,
        int Raw,
        int Damage,
        int GuardAbsorbed,
        int PlayerGuardAfter,
        int PlayerStaminaAfter,
        int EnemyGuardAfter,
        int DistanceAfter,
        bool Clamped,
        IReadOnlyList<BattleEvent> Events,
        IReadOnlyList<string> Logs);

    public static class Enemy
    {
        private static Tier T(int power = 0, int guard = 0) => new Tier(power, guard);

        private static readonly Tier None = new Tier();

        /// <summary>長柄の歪み兵 (§8.2).</summary>
        public static readonly IReadOnlyDictionary<EnemyActionId, EnemyAction> Actions =
            new Dictionary<EnemyActionId, EnemyAction>
            {
                [EnemyActionId.Sweep] = new EnemyAction(
                    EnemyActionId.Sweep, "薙ぎ払い", CardType.Attack, RangeBand.Mid, 1,
                    new[] { None, T(4), T(6), T(8) }, 0, false, "中間合いの主力。"),
                [EnemyActionId.ReachThrust] = new EnemyAction(
                    EnemyActionId.ReachThrust, "穂先の突き", CardType.Attack, RangeBand.Far, 0,
                    new[] { T(1), T(2), T(3), T(4) }, 0, false, "遠間から届くが軽い。"),
                [EnemyActionId.Shove] = new EnemyAction(
                    EnemyActionId.Shove, "石突きの押し込み", CardType.Attack, RangeBand.Close, 0,
                    new[] { T(1), T(2), T(2), T(3) }, 1, false, "懐の相手を中へ押し戻す。"),
                [EnemyActionId.Reposition] = new EnemyAction(
                    EnemyActionId.Reposition, "間合い取り直し", CardType.Move, null, 0,
                    new[] { None, None, None, None }, 0, true, "得意の中へ 1 段階戻る。"),
                [EnemyActionId.GuardUp] = new EnemyAction(
                    EnemyActionId.GuardUp, "柄で受ける", CardType.Guard, null, 0,
                    new[] { T(guard: 2), T(guard: 3), T(guard: 4), T(guard: 5) }, 0, false, "柄で受けを固める。"),
            };

        public static readonly EnemyDef Def = new EnemyDef(
            "長柄の歪み兵",
            38,
            Constants.BaseMaxStamina,
            RangeBand.Mid,
            new Dictionary<RangeBand, IReadOnlyList<EnemyActionId>>
            {
                [RangeBand.Close] = new[] { EnemyActionId.Shove, EnemyActionId.Reposition },
                [RangeBand.Mid] = new[] { EnemyActionId.Sweep, EnemyActionId.ReachThrust, EnemyActionId.GuardUp },
                [RangeBand.Far] = new[] { EnemyActionId.ReachThrust, EnemyActionId.Reposition },
            },
            Actions);

        /// <summary>§6.3: deterministic — first entry of the distance's list whose minimum invest is affordable.</summary>
        public static Omen ChooseOmen(int distanceIndex, int enemyStamina)
        {
            var band = Combat.IndexToRange(distanceIndex);
            foreach (var id in Def.DecisionTree[band])
            {
                var action = Actions[id];
                if (enemyStamina >= action.MinInvest)
                {
                    return new Omen(id, action.Type == CardType.Attack ? action.EffectiveRange : null);
                }
            }
            return new Omen(EnemyActionId.Reposition, null);
        }

        private static int HomeShift(int distanceIndex)
        {
            return Math.Sign(Combat.RangeToIndex(Def.HomeRange) - distanceIndex);
        }

        /// <summary>§9 step 9: execute the declared omen (with whiff-avoidance §6.2 and the invest rule §6.3).</summary>
        public static EnemyOutcome ResolveOmen(
            Omen omen, int distanceIndex, int enemyStamina, int enemyGuard, int playerGuard, int playerStamina)
        {
            var action = Actions[omen.ActionId];
            var events = new List<BattleEvent>();
            var logs = new List<string>();
            string who = $"敵「{Def.Name}」";

            // Whiff avoidance: a declared attack that would land at diff ≥ 2 is dropped for a reposition.
            if (action.Type == CardType.Attack && action.EffectiveRange.HasValue
                && Combat.RangeDiff(distanceIndex, action.EffectiveRange.Value) >= Constants.WhiffDiff)
            {
                events.Add(new OmenWhiffedEvent(action.Id, action.Name));
                logs.Add($"{who}の{action.Name}は届かない。間合いを取り直す。");
                return Reposition(distanceIndex, enemyGuard, playerGuard, playerStamina, events, logs, true);
            }

            int? invest = Combat.ChooseInvest(action.MinInvest, enemyStamina, Constants.EnemyReserve);
            if (invest == null)
            {
                // Cannot pay even the minimum: reposition if away from home, otherwise rest.
                if (HomeShift(distanceIndex) != 0)
                {
                    logs.Add($"{who}は息が上がっている。間合いを取り直す。");
                    return Reposition(distanceIndex, enemyGuard, playerGuard, playerStamina, events, logs, false);
                }
                events.Add(new EnemyRestedEvent());
                logs.Add($"{who}は息を整えた（休む）。");
                return new EnemyOutcome(null, 0, false, 0, 0, 0, playerGuard, playerStamina, enemyGuard,
                    distanceIndex, false, events, logs);
            }

            if (action.Type == CardType.Move)
            {
                events.Add(new OmenExecutedEvent(action.Id, action.Name, 0));
                logs.Add($"{who}の{action.Name}。");
                return Reposition(distanceIndex, enemyGuard, playerGuard, playerStamina, events, logs, false);
            }

            int inv = invest.Value;
            var tier = action.Tiers[inv];
            events.Add(new OmenExecutedEvent(action.Id, action.Name, inv));

            if (action.Type == CardType.Guard)
            {
                int guardAfter = enemyGuard + tier.Guard;
                events.Add(new GuardGainedEvent(Actor.Enemy, tier.Guard, guardAfter, action.Name));
                logs.Add($"{who}の{action.Name}（投入 {inv}、Guard +{tier.Guard}）。");
                return new EnemyOutcome(action, inv, false, 0, 0, 0, playerGuard, playerStamina, guardAfter,
                    distanceIndex, false, events, logs);
            }

            // Attack: damage → move → additional effects (diff 0 only).
            var eff = action.EffectiveRange!.Value;
            int diff = Combat.RangeDiff(distanceIndex, eff);
            double mult = Combat.RangeMultiplier(distanceIndex, eff);
            int raw = Combat.ComputeAttackDamage(tier.Power, eff, distanceIndex);
            var (damage, guardAfterHit, absorbed) = Combat.ApplyGuard(raw, playerGuard);
            events.Add(new AttackResolvedEvent(Actor.Enemy, action.Name, tier.Power, mult, diff, false, raw, absorbed, damage, -1));
            string log = $"{who}の{action.Name}（投入 {inv}）。{damage} ダメージ";
            if (absorbed > 0) log += $"（Guard で {absorbed} 軽減）";
            if (diff == 1) log += "（間合い不適 ×0.5）";
            log += "。";
            logs.Add(log);

            int distanceAfter = distanceIndex;
            bool clamped = false;
            if (action.Shift != 0)
            {
                distanceAfter = Combat.ShiftDistance(distanceIndex, action.Shift);
                clamped = distanceAfter == distanceIndex;
                events.Add(new MovedEvent(Actor.Enemy, distanceIndex, distanceAfter, clamped));
                if (!clamped) logs.Add($"間合いが「{Constants.RangeLabel[Combat.IndexToRange(distanceAfter)]}」に。");
            }

            int playerStaminaAfter = playerStamina;
            int enemyGuardAfter = enemyGuard;
            if (diff == 0)
            {
                if (tier.BreakStamina > 0)
                {
                    playerStaminaAfter = Math.Max(0, playerStamina - tier.BreakStamina);
                    events.Add(new StaminaBrokenEvent(Actor.Player, tier.BreakStamina, playerStaminaAfter));
                    logs.Add($"崩された（スタミナ −{tier.BreakStamina}）。");
                }
                if (tier.Guard > 0)
                {
                    enemyGuardAfter += tier.Guard;
                    events.Add(new GuardGainedEvent(Actor.Enemy, tier.Guard, enemyGuardAfter, action.Name));
                }
            }

            return new EnemyOutcome(action, inv, false, raw, damage, absorbed, guardAfterHit, playerStaminaAfter,
                enemyGuardAfter, distanceAfter, clamped, events, logs);
        }

        private static EnemyOutcome Reposition(
            int distanceIndex, int enemyGuard, int playerGuard, int playerStamina,
            List<BattleEvent> events, List<string> logs, bool whiffed)
        {
            int shift = HomeShift(distanceIndex);
            int after = Combat.ShiftDistance(distanceIndex, shift);
            bool clamped = after == distanceIndex;
            events.Add(new MovedEvent(Actor.Enemy, distanceIndex, after, clamped));
            if (!clamped) logs.Add($"間合いが「{Constants.RangeLabel[Combat.IndexToRange(after)]}」に。");
            return new EnemyOutcome(Actions[EnemyActionId.Reposition], 0, whiffed, 0, 0, 0, playerGuard,
                playerStamina, enemyGuard, after, clamped, events, logs);
        }
    }
}
