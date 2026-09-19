using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>One invest chip on a selected card (battle_ui_ux_v1.md §3.1–3.3): every number is the settled value at the current distance.</summary>
    public sealed record TierView(
        int Invest,
        bool Affordable,
        int Power,
        int PredictedDamage,
        double Mult,
        int Diff,
        bool Desperate,
        int Guard,
        bool GuardApplies,
        int Heal,
        int Shift,
        int DistanceAfter,
        string? DistanceAfterLabel,
        bool Clamped,
        int BreakStamina,
        int StaminaAfter,
        bool KeepsReserve,
        bool IsDefault,
        string Summary);

    public sealed record CardView(
        string InstanceId,
        CardDefId DefId,
        string Name,
        CardType Type,
        string TypeLabel,
        string? EffectiveRangeLabel,
        int MinInvest,
        string Description,
        bool Playable,
        string DisabledReason,
        IReadOnlyList<TierView> Tiers,
        int? DefaultInvest,
        /// <summary>The one-line prediction shown on the card at rest (the default tier's summary).</summary>
        string RestText);

    /// <summary>The enemy's declared next action as the HUD shows it (UI §1.1 A1/A2, §7 disclosure).</summary>
    public sealed record OmenView(
        EnemyActionId ActionId,
        CardType Kind,
        string KindLabel,
        string? Name,
        RangeBand? TargetRange,
        string? TargetRangeLabel,
        string? PowerSpan,
        int? Diff,
        double? Mult,
        string? BandLabel,
        string BannerText,
        string FloorText);

    public sealed record JournalView(
        string EnemyName,
        int Disclosure,
        int DisclosureMax,
        IReadOnlyList<string> TendencyLines,
        IReadOnlyList<string> SpecialLines,
        string WeaknessLine,
        string AdaptationLine);

    public static class ViewModel
    {
        public static string TypeLabel(CardType type) => type switch
        {
            CardType.Attack => "攻撃",
            CardType.Move => "移動",
            CardType.Guard => "防御",
            CardType.Heal => "回復",
            _ => "",
        };

        public static string BandLabel(int diff) => diff switch
        {
            0 => "的中",
            1 => "半減",
            _ => "空振り",
        };

        public static string MultLabel(double mult) => mult switch
        {
            >= 1.0 => "×1.0",
            >= 0.5 => "×0.5",
            _ => "×0.15",
        };

        public static string DistanceLabel(int distanceIndex) => Constants.RangeLabel[Combat.IndexToRange(distanceIndex)];

        public static bool IsBattleOver(GameResult result) => result != GameResult.Ongoing;

        public static TierView DescribeTier(CardDef def, int invest, BattleState state)
        {
            var tier = def.Tiers[invest];
            bool affordable = state.PlayerStamina >= invest && invest >= def.MinInvest;
            int distance = state.DistanceIndex;
            int diff = -1;
            double mult = 1.0;
            int predicted = 0;
            bool desperate = false;
            if (def.Type == CardType.Attack && def.EffectiveRange.HasValue)
            {
                diff = Combat.RangeDiff(distance, def.EffectiveRange.Value);
                mult = Combat.RangeMultiplier(distance, def.EffectiveRange.Value);
                desperate = Combat.IsDesperate(def, state.PlayerStamina);
                int raw = Combat.ComputeAttackDamage(tier.Power, def.EffectiveRange.Value, distance, desperate);
                predicted = Combat.ApplyGuard(raw, state.EnemyGuard).Damage;
            }
            int after = tier.Shift != 0 ? Combat.ShiftDistance(distance, tier.Shift) : distance;
            bool clamped = tier.Shift != 0 && after == distance;
            bool extras = def.Type != CardType.Attack || diff == 0;
            bool guardApplies = tier.Guard > 0 && (def.Type == CardType.Guard || extras);
            int staminaAfter = state.PlayerStamina - invest;
            bool keeps = staminaAfter >= Constants.ReserveThreshold;

            var parts = new List<string>();
            if (def.Type == CardType.Attack)
            {
                parts.Add(diff == 0 ? $"威力 {predicted}" : $"威力 {tier.Power} → {predicted}（{MultLabel(mult)}）");
                if (desperate) parts.Add("死力");
            }
            if (tier.Heal > 0) parts.Add($"HP +{tier.Heal}");
            if (guardApplies) parts.Add($"Guard +{tier.Guard}");
            if (tier.Shift != 0) parts.Add(clamped ? (tier.Shift < 0 ? "詰められない" : "退けない") : (tier.Shift < 0 ? $"詰め → {DistanceLabel(after)}" : $"退き → {DistanceLabel(after)}"));
            if (tier.BreakStamina > 0) parts.Add(extras ? $"崩し {tier.BreakStamina}" : "崩し（最適間合いのみ）");

            return new TierView(
                Invest: invest,
                Affordable: affordable,
                Power: tier.Power,
                PredictedDamage: predicted,
                Mult: mult,
                Diff: diff,
                Desperate: desperate,
                Guard: tier.Guard,
                GuardApplies: guardApplies,
                Heal: tier.Heal,
                Shift: tier.Shift,
                DistanceAfter: after,
                DistanceAfterLabel: tier.Shift != 0 ? DistanceLabel(after) : null,
                Clamped: clamped,
                BreakStamina: tier.BreakStamina,
                StaminaAfter: staminaAfter,
                KeepsReserve: keeps,
                IsDefault: false,
                Summary: string.Join(" / ", parts));
        }

        /// <summary>UI §3.2: default chip = largest invest that keeps ≥ 3 stamina, else the smallest affordable.</summary>
        public static int? DefaultInvest(CardDef def, int stamina)
        {
            return Combat.ChooseInvest(def.MinInvest, stamina, Constants.ReserveThreshold);
        }

        public static CardView DescribeCard(CardInstance card, BattleState state)
        {
            var def = card.Def;
            bool battleOver = IsBattleOver(state.Result);
            int? def_invest = DefaultInvest(def, state.PlayerStamina);
            bool playable = def_invest.HasValue && !battleOver;
            string disabled = !def_invest.HasValue ? $"スタミナ不足（最低 {def.MinInvest}）" : "";

            var tiers = new List<TierView>();
            for (int invest = def.MinInvest; invest <= Constants.MaxInvest; invest++)
            {
                var t = DescribeTier(def, invest, state);
                tiers.Add(t with { IsDefault = def_invest == invest });
            }
            string rest = def_invest.HasValue
                ? tiers.First(t => t.Invest == def_invest.Value).Summary
                : tiers[0].Summary;

            return new CardView(
                InstanceId: card.InstanceId,
                DefId: def.Id,
                Name: def.Name,
                Type: def.Type,
                TypeLabel: TypeLabel(def.Type),
                EffectiveRangeLabel: def.EffectiveRange.HasValue ? Constants.RangeLabel[def.EffectiveRange.Value] : null,
                MinInvest: def.MinInvest,
                Description: def.Description,
                Playable: playable,
                DisabledReason: disabled,
                Tiers: tiers,
                DefaultInvest: def_invest,
                RestText: rest);
        }

        public static IReadOnlyList<CardView> DescribeHand(BattleState state)
        {
            return state.Hand.Select(c => DescribeCard(c, state)).ToList();
        }

        /// <summary>Range multiplier the omen would land with if the player stood at <paramref name="distanceIndex"/> (floor band colour).</summary>
        public static double? OmenMultiplierAt(Omen omen, int distanceIndex)
        {
            if (!omen.TargetRange.HasValue) return null;
            return Combat.RangeMultiplier(distanceIndex, omen.TargetRange.Value);
        }

        public static int? OmenDiffAt(Omen omen, int distanceIndex)
        {
            if (!omen.TargetRange.HasValue) return null;
            return Combat.RangeDiff(distanceIndex, omen.TargetRange.Value);
        }

        public static OmenView DescribeOmen(Omen omen, int distanceIndex, int disclosure)
        {
            var action = Enemy.Actions[omen.ActionId];
            string kindLabel = TypeLabel(action.Type);
            string? name = disclosure >= 2 ? action.Name : null;
            string? rangeLabel = disclosure >= 1 && omen.TargetRange.HasValue ? Constants.RangeLabel[omen.TargetRange.Value] : null;
            string? span = null;
            if (disclosure >= 2 && action.Type == CardType.Attack)
            {
                int lo = action.Tiers[action.MinInvest].Power;
                int hi = action.Tiers[Constants.MaxInvest].Power;
                span = lo == hi ? $"{hi}" : $"{lo}〜{hi}";
            }
            int? diff = disclosure >= 1 ? OmenDiffAt(omen, distanceIndex) : null;
            double? mult = disclosure >= 1 ? OmenMultiplierAt(omen, distanceIndex) : null;
            string? band = diff.HasValue ? BandLabel(diff.Value) : null;

            var parts = new List<string> { name ?? kindLabel };
            if (rangeLabel != null) parts.Add($"狙い {rangeLabel}");
            if (span != null) parts.Add($"威力 {span}");
            string banner = "予兆: " + string.Join(" ／ ", parts);
            string floor = mult.HasValue
                ? $"このままだと {name ?? kindLabel} {MultLabel(mult.Value)}"
                : (action.Type == CardType.Attack ? "狙いは読めない" : "");
            return new OmenView(omen.ActionId, action.Type, kindLabel, name, omen.TargetRange, rangeLabel, span, diff, mult, band, banner, floor);
        }

        public static JournalView DescribeJournal(int disclosure)
        {
            var def = Enemy.Def;
            var tendencies = new List<string>();
            if (disclosure >= 1)
            {
                foreach (var band in Constants.RangeOrder)
                {
                    var names = def.DecisionTree[band].Select(id => def.Actions[id].Name);
                    tendencies.Add($"{Constants.RangeLabel[band]} → {string.Join(" / ", names)}");
                }
            }
            else
            {
                tendencies.Add("？（まだ読めない）");
            }
            var specials = new List<string>();
            if (disclosure >= 2)
            {
                specials.Add("石突きの押し込み: 近にいる相手を中へ押し戻す");
                specials.Add("薙ぎ払いが届かない間合いでは、攻撃を捨てて中へ取り直す");
            }
            else
            {
                specials.Add("？");
            }
            return new JournalView(
                def.Name, disclosure, Constants.DisclosureMax, tendencies, specials,
                disclosure >= 2 ? "弱点: 近（弱い押し戻ししか無い）" : "弱点: ？",
                "適応: ？（未観測）");
        }

        /// <summary>UI B4: "今終えると 残 7 → Guard +2" or "残 2 → 構えなし".</summary>
        public static string ReservePreview(int stamina)
        {
            int guard = Combat.ReserveGuard(stamina);
            return guard > 0 ? $"今終えると 残 {stamina} → 構え Guard +{guard}" : $"今終えると 残 {stamina} → 構えなし（3 以上で +{Constants.ReserveGuard}）";
        }
    }
}
