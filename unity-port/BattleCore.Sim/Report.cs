using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BattleCore.Sim;

/// <summary>
/// The printed table and the JSON. Neither holds a time or anything else that changes between two
/// runs with the same seed, so the same seed writes the same bytes.
/// </summary>
public static class Report
{
    public const string Format = "battlecore-sim/2";

    private static string Cell(string text) => text.Replace("|", "\\|");

    public static string Markdown(BenchResults results, IReadOnlyList<CriterionRow> rows, FirstThree first)
    {
        var o = results.Options;
        var text = new StringBuilder();
        text.AppendLine($"## BattleCore.Sim — battle_core_v4 §13（seed {o.Seed}、{o.Runs} 戦 / 組み合わせ{(o.OnlyFirstThree ? "、--only first3" : "")}{(o.MaxPlays > 0 ? $"、1 ターン {o.MaxPlays} 枚まで" : "")}）");
        text.AppendLine();
        text.AppendLine("| # | 基準 | 目標 | 測定値 | 判定 | 備考 |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var row in rows)
        {
            text.AppendLine($"| {row.Id} | {Cell(row.Criterion)} | {Cell(row.Target)} | {Cell(row.Measured)} | {Criteria.VerdictText(row.Verdict)} | {Cell(row.Note)} |");
        }
        text.AppendLine();
        text.AppendLine("### 最初に見る 3 つ（§18.4）");
        text.AppendLine();
        text.AppendLine($"1. コスト 1 だけのデッキ {Criteria.Pct(first.CostOneWinRate)} / 型 6 種の平均 {Criteria.Pct(first.TypeAverageWinRate)}（{Criteria.Pt(first.CostOneLead)}、基準は +10 pt 未満）: {Criteria.VerdictText(first.CostOnePasses ? Verdict.Pass : Verdict.Fail)}");
        text.AppendLine($"2. 1 ターンのプレイ枚数の中央値 {Criteria.Num(first.PlaysPerTurnMedian)}（基準は 2 以上）: {Criteria.VerdictText(first.PlaysPass ? Verdict.Pass : Verdict.Fail)}");
        text.AppendLine($"3. 1 撃の最大値: 試験台 {first.ObservedMaxBlow}（{Criteria.DeckName(results.Decks, first.ObservedMaxBlowDeck)}）、机上 {first.DeskCanon.CardName} {first.DeskCanon.Formula}、"
            + $"特性込み {first.DeskWithTrait.CardName} {first.DeskWithTrait.WithTrait}。上限は通常敵の最大 HP {first.NormalMaxHp} × 0.6 = {first.SingleHitLimit}: "
            + $"{Criteria.VerdictText(first.BlowPasses ? Verdict.Pass : Verdict.Fail)}（机上は {(first.DeskCanon.Canon <= first.SingleHitLimit ? "超えない" : "超える")}）");
        text.AppendLine();
        text.AppendLine($"### デッキ × 敵の勝率（位置の偏りなし、最大スタミナ {Constants.BaseMaxStamina}）");
        text.AppendLine();
        var enemies = Enemies.All;
        text.AppendLine("| デッキ | " + string.Join(" | ", enemies.Select(e => e.Name)) + " | 全体 |");
        text.AppendLine("| --- | " + string.Join(" | ", enemies.Select(_ => "---")) + " | --- |");
        foreach (var deck in results.Decks)
        {
            var mine = results.Main.Where(r => r.DeckId == deck.Id).ToList();
            text.AppendLine($"| {deck.Name} | " + string.Join(" | ", enemies.Select(e => Criteria.Pct(Criteria.WinRate(mine.Where(r => r.EnemyId == e.Id)))))
                + $" | {Criteria.Pct(Criteria.WinRate(mine))} |");
        }
        text.AppendLine();
        text.AppendLine(StallLine(results));
        return text.ToString();
    }

    /// <summary>
    /// The line under the win-rate table: how many battles hit the turn limit, by kind, and the main
    /// battles' stalls by deck × enemy. The win rates count a stall as a battle that was not won.
    /// </summary>
    public static string StallLine(BenchResults results)
    {
        var stalls = Criteria.Stalls(results);
        int Sum(string variant) => stalls.Where(c => c.Variant == variant).Sum(c => c.Stalls);
        int total = stalls.Sum(c => c.Stalls);
        var line = new StringBuilder();
        line.Append($"詰まり（{Runner.TurnLimit} ターンで決着しない戦闘。勝ちにも負けにも数えず、勝率では勝てなかった戦闘として分母に入る）: ");
        if (total == 0) return line.Append("0 戦").ToString();
        line.Append($"主な戦闘 {Sum("main")} 戦、方針別 {Sum("policy")} 戦、最大スタミナ {Bench.LowMaxStamina} で {Sum("stamina6")} 戦、2 連戦 {Sum("pair")} 組。");
        var main = stalls.Where(c => c.Variant == "main").ToList();
        if (main.Count > 0)
        {
            var names = results.Decks.ToDictionary(d => d.Id, d => d.Name);
            line.Append("主な戦闘の内訳: ");
            line.Append(string.Join("、", main.Select(c => $"{names[c.DeckId]} × {Enemies.ById(c.EnemyId).Name} {c.Stalls}/{c.Runs}")));
            line.Append('。');
        }
        // The policy runs, summed over the six types (each deck × enemy × policy is in the JSON).
        var policy = stalls.Where(c => c.Variant == "policy")
            .GroupBy(c => (c.Policy, c.EnemyId))
            .Select(g => (g.Key.Policy, g.Key.EnemyId, Stalls: g.Sum(c => c.Stalls),
                Runs: results.Policies[g.Key.Policy].Count(r => r.EnemyId == g.Key.EnemyId)))
            .ToList();
        if (policy.Count > 0)
        {
            line.Append("方針別の内訳（型 6 種の合計）: ");
            line.Append(string.Join("、", policy.Select(c => $"{Criteria.PolicyName(c.Policy)} × {Enemies.ById(c.EnemyId).Name} {c.Stalls}/{c.Runs}")));
            line.Append('。');
        }
        return line.ToString().TrimEnd('。') + "。";
    }

    public static string Json(BenchResults results, IReadOnlyList<CriterionRow> rows, FirstThree first)
    {
        var o = results.Options;
        var doc = new
        {
            format = Format,
            seed = o.Seed,
            runs = o.Runs,
            only = o.OnlyFirstThree ? "first3" : "all",
            turnLimit = Runner.TurnLimit,
            maxPlays = o.MaxPlays > 0 ? o.MaxPlays : (int?)null,
            policy = o.MaxPlays > 0
                ? $"greedy: play the highest-valued legal card while its value is above 0, at most {o.MaxPlays} a turn (GreedyPlayer)"
                : "greedy: play the highest-valued legal card while its value is above 0, no cap a turn (GreedyPlayer)",
            basis = new
            {
                normalEnemyMaxHp = first.NormalMaxHp,
                singleHitLimit = first.SingleHitLimit,
                handDraw = Constants.HandDraw,
                playerMaxHp = Constants.PlayerMaxHp,
                baseMaxStamina = Constants.BaseMaxStamina,
                lowMaxStamina = Bench.LowMaxStamina,
            },
            firstThree = new
            {
                costOneWinRate = R(first.CostOneWinRate),
                typeAverageWinRate = R(first.TypeAverageWinRate),
                costOneLeadPt = R(first.CostOneLead),
                playsPerTurnMedian = R(first.PlaysPerTurnMedian),
                observedMaxSingleHit = first.ObservedMaxBlow,
                observedMaxSingleHitDeck = first.ObservedMaxBlowDeck,
                deskCalc = Desk(first.DeskCanon),
                deskCalcWithTrait = Desk(first.DeskWithTrait),
            },
            criteria = rows.Select(r => new
            {
                id = r.Id,
                criterion = r.Criterion,
                target = r.Target,
                measured = r.Measured,
                verdict = Criteria.VerdictText(r.Verdict),
                note = r.Note,
                value = r.Value.HasValue ? R(r.Value.Value) : (double?)null,
            }),
            decks = results.Decks.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                basis = d.Basis,
                count = d.Count,
                valid = Cards.Validate(d.Build()).Ok,
                cards = d.Rows.Select(r => new { id = r.CardId, copies = r.Copies }),
            }),
            matchups = Matchups(results.Main, "main", Positioning.Neutral)
                .Concat(results.Policies.OrderBy(p => p.Key).SelectMany(p => Matchups(p.Value, "policy", p.Key)))
                .Concat(Matchups(results.LowStamina, "stamina6", Positioning.Neutral)),
            pairs = results.Pairs
                .GroupBy(p => (p.DeckId, p.FirstId, p.SecondId))
                .Select(g => new
                {
                    deck = g.Key.DeckId, first = g.Key.FirstId, second = g.Key.SecondId, runs = g.Count(), wins = g.Count(p => p.Won),
                    stalls = g.Count(p => p.Outcome == Outcome.Stalled),
                }),
            openingHands = results.OpeningHands.Select(h => new
            {
                deck = h.DeckId,
                hands = h.Hands,
                withoutMovement = h.WithoutMovement,
                rate = R(h.Rate),
            }),
            stalls = new
            {
                total = Criteria.Stalls(results).Sum(c => c.Stalls),
                byMatchup = Criteria.Stalls(results).Select(c => new
                {
                    variant = c.Variant,
                    policy = c.Policy.ToString(),
                    deck = c.DeckId,
                    enemy = c.EnemyId,
                    runs = c.Runs,
                    stalls = c.Stalls,
                }),
            },
        };
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        return JsonSerializer.Serialize(doc, options) + "\n";
    }

    private static object Desk(DeskBlow blow) => new
    {
        card = blow.CardId,
        name = blow.CardName,
        face = blow.Face,
        focus = blow.Focus,
        followUp = blow.FollowUp,
        trait = blow.Trait,
        multiplier = blow.Multiplier,
        canon = blow.Canon,
        withTrait = blow.WithTrait,
        formula = blow.Formula,
    };

    private static IEnumerable<object> Matchups(IEnumerable<BattleRecord> records, string variant, Positioning policy) =>
        records
            .GroupBy(r => (r.DeckId, r.EnemyId))
            .Select(g => (object)new
            {
                variant,
                policy = policy.ToString(),
                deck = g.Key.DeckId,
                enemy = g.Key.EnemyId,
                rank = g.First().Rank.ToString(),
                runs = g.Count(),
                wins = g.Count(r => r.Won),
                stalls = g.Count(r => r.Outcome == Outcome.Stalled),
                winRate = R(Criteria.WinRate(g)),
                turnsMedian = R(Criteria.Median(g.Select(r => r.Turns))),
                turnsMean = R(g.Average(r => r.Turns)),
                playsPerTurnMedian = R(Criteria.Median(g.SelectMany(r => r.PlaysPerTurn))),
                playsPerTurnMean = R(g.SelectMany(r => r.PlaysPerTurn).DefaultIfEmpty(0).Average()),
                maxSingleHit = g.Max(r => r.MaxBlow),
                hpLeftMean = R(g.Average(r => r.HpLeft)),
            });

    private static double R(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
