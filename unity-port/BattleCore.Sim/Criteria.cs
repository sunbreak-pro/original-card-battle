using System.Globalization;

namespace BattleCore.Sim;

public enum Verdict
{
    Pass,
    Fail,

    /// <summary>A bot cannot measure it (a person's judgment time, a system the core does not carry yet).</summary>
    NotMeasurable,

    /// <summary>Not run this time (--only first3).</summary>
    Skipped,
}

/// <summary>One row of the §13 table: the canon's words, what the bench measured, and the verdict.</summary>
public sealed record CriterionRow(string Id, string Criterion, string Target, string Measured, Verdict Verdict, string Note, double? Value = null);

/// <summary>§13 / §18.4 「最初に見る 3 つ」.</summary>
public sealed record FirstThree(
    double CostOneWinRate,
    double TypeAverageWinRate,
    double PlaysPerTurnMedian,
    int ObservedMaxBlow,
    string ObservedMaxBlowDeck,
    DeskBlow DeskCanon,
    DeskBlow DeskWithTrait,
    int NormalMaxHp,
    int SingleHitLimit)
{
    public double CostOneLead => CostOneWinRate - TypeAverageWinRate;

    public bool CostOnePasses => CostOneLead < Criteria.CostOneLeadPt;

    public bool PlaysPass => PlaysPerTurnMedian >= Criteria.PlaysPerTurnMin;

    public bool BlowPasses => ObservedMaxBlow <= SingleHitLimit;
}

/// <summary>
/// The 19 criteria of battle_core_v4 §13: the first table (14 rows, numbered 1〜14 here) and 13〜17
/// (S13〜S17 here, the canon's own numbers). Pure functions over the records, so a tiny fixture can
/// check them without a battle.
/// </summary>
public static class Criteria
{
    // The lines of §13, one name per row (pt = percentage points, % = a win rate or a share). The
    // same number stands for different rows (15 is 7, 10 and 13), so no row borrows another's name.

    /// <summary>Criterion 1: the normal battles' median turns, 6〜10.</summary>
    public const int NormalTurnsMin = 6;
    public const int NormalTurnsMax = 10;

    /// <summary>Criterion 2: the boss battles' median turns, 12〜18.</summary>
    public const int BossTurnsMin = 12;
    public const int BossTurnsMax = 18;

    /// <summary>Criterion 3: the cost-1 deck leads the types' average by less than 10 pt.</summary>
    public const int CostOneLeadPt = 10;

    /// <summary>Criterion 4: 2 plays a turn or more (median).</summary>
    public const int PlaysPerTurnMin = 2;

    /// <summary>Criterion 6: the strongest type − the weakest, 25 pt or less.</summary>
    public const int TypeSpreadPt = 25;

    /// <summary>Criterion 7: the best no-stance deck at the strongest − 15 pt or more; no twin 15 pt or more below its type.</summary>
    public const int NoStanceGapPt = 15;

    /// <summary>Criterion 8: the normal battles' win rate at max stamina 6, 50% or more.</summary>
    public const int LowStaminaWinRatePct = 50;

    /// <summary>Criterion 9: the win rate of two battles with HP carried, 40% or more.</summary>
    public const int PairWinRatePct = 40;

    /// <summary>Criterion 10: an enemy counts when the three policies move its win rate 15 pt or more.</summary>
    public const int PolicySwingPt = 15;

    /// <summary>Criterion 11: no movement card in the opening hand, 25% or less.</summary>
    public const int NoMoverHandPct = 25;

    /// <summary>Criterion 12: turns with nothing in reach, 1 a battle or fewer (median).</summary>
    public const int NoReachTurnsMax = 1;

    /// <summary>Criterion 13: staying at 2 or more leads the other policies' average by less than 15 pt.</summary>
    public const int KeepTwoLeadPt = 15;

    /// <summary>Criterion S13: turns of 3 plays or more, 2 a battle or more (median).</summary>
    public const int ThreePlayTurnsMin = 2;

    /// <summary>Criterion S14: a battle with 2 movement plays or more ...</summary>
    public const int ManyMovementPlays = 2;

    /// <summary>Criterion S14: ... wins less than 10 pt below one with 1 or fewer.</summary>
    public const int MovementDeficitPt = 10;

    /// <summary>Criterion S16: each stance type's median stance plays a battle, 1〜2.</summary>
    public const int StancePlaysMin = 1;
    public const int StancePlaysMax = 2;

    private const string SkippedNote = "--only first3 では回していない";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Pct(double value) => value.ToString("0.0", Inv) + "%";

    public static string Pt(double value) => (value >= 0 ? "+" : "") + value.ToString("0.0", Inv) + " pt";

    public static string Num(double value) => value.ToString("0.##", Inv);

    /// <summary>Wins over battles, in percent (0 for none).</summary>
    public static double WinRate(IEnumerable<BattleRecord> records)
    {
        int n = 0, won = 0;
        foreach (var r in records)
        {
            n++;
            if (r.Won) won++;
        }
        return n == 0 ? 0 : 100.0 * won / n;
    }

    /// <summary>The median; the mean of the two middle values for an even count. 0 for none.</summary>
    public static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    public static double Median(IEnumerable<int> values) => Median(values.Select(v => (double)v));

    /// <summary>Each deck's win rate over every enemy (every enemy fights the same number of runs, so pooled is the mean of the enemies).</summary>
    public static IReadOnlyDictionary<string, double> RatesByDeck(IEnumerable<BattleRecord> records) =>
        records.GroupBy(r => r.DeckId).ToDictionary(g => g.Key, g => WinRate(g));

    /// <summary>
    /// A deck's win rate from <see cref="RatesByDeck"/>. A deck id with no battles throws: a renamed or
    /// mistyped id must not enter the verdicts as a 0% deck (#574).
    /// </summary>
    public static double RateOf(IReadOnlyDictionary<string, double> rates, string deckId)
    {
        if (rates.TryGetValue(deckId, out double rate)) return rate;
        throw new KeyNotFoundException(
            $"No battles for the deck id '{deckId}'. Known ids: {string.Join(", ", rates.Keys.OrderBy(k => k, StringComparer.Ordinal))}");
    }

    public static FirstThree ComputeFirstThree(
        IReadOnlyList<BattleRecord> main, IReadOnlyList<SimDeck> archetypes, string costOneId,
        IEnumerable<CardDef> catalog, IEnumerable<EnemyDef> roster)
    {
        var typeIds = archetypes.Select(d => d.Id).ToHashSet();
        var rates = RatesByDeck(main);
        double typeAverage = archetypes.Average(d => RateOf(rates, d.Id));
        double costOne = RateOf(rates, costOneId);
        double plays = Median(main.Where(r => typeIds.Contains(r.DeckId)).SelectMany(r => r.PlaysPerTurn));
        var top = main.OrderByDescending(r => r.MaxBlow).FirstOrDefault();
        var cards = catalog.ToList();
        var (hp, limit) = DeskCalc.SingleHitLimit(roster);
        return new FirstThree(
            costOne, typeAverage, plays, top?.MaxBlow ?? 0, top?.DeckId ?? "",
            DeskCalc.Strongest(cards), DeskCalc.StrongestWithTrait(cards), hp, limit);
    }

    public static IReadOnlyList<CriterionRow> Compute(BenchResults results)
    {
        var archetypes = Sim.Decks.Archetypes;
        var typeIds = archetypes.Select(d => d.Id).ToHashSet();
        var costOne = Sim.Decks.CostOneOnly();
        var first = ComputeFirstThree(results.Main, archetypes, costOne.Id, CardCatalog.All, Enemies.All);
        var run = new Run(
            results, archetypes, results.Main.Where(r => typeIds.Contains(r.DeckId)).ToList(), RatesByDeck(results.Main),
            costOne, first, DeckName(results.Decks, first.ObservedMaxBlowDeck));

        return new List<CriterionRow>
        {
            NormalTurns(run),
            BossTurns(run),
            CostOneDeck(run),
            PlaysPerTurn(run),
            StackedBlow(run),
            TypeSpread(run),
            NoStance(run.Rates, archetypes), // main battles, so --only first3 has them too
            LowStamina(run),
            CarriedPair(run),
            PolicySwing(run),
            NoMoverInHand(run),
            NoReach(run),
            KeepTwo(run),
            JudgmentTime(),
            ThreePlayTurns(run),
            ManyMoves(run),
            SingleBlow(run),
            StancePlays(run.Typed, archetypes),
            MasteryTicks(),
        };
    }

    /// <summary>
    /// What the rows of <see cref="Compute"/> share: the six types' main battles, every main deck's win
    /// rate, the first three and the name of the deck that dealt the largest blow.
    /// </summary>
    private sealed record Run(
        BenchResults Results, IReadOnlyList<SimDeck> Archetypes, IReadOnlyList<BattleRecord> Typed,
        IReadOnlyDictionary<string, double> Rates, SimDeck CostOne, FirstThree First, string BlowDeck)
    {
        /// <summary>Not --only first3: 8〜11 and 13 have their battles.</summary>
        public bool Full => !Results.Options.OnlyFirstThree;

        public int NormalCount => Enemies.All.Count(e => e.Rank == EnemyRank.Normal);
    }

    /// <summary>Criterion 1: battle length against the normal enemies.</summary>
    private static CriterionRow NormalTurns(Run run)
    {
        var typed = run.Typed;
        double median = Median(typed.Where(r => r.Rank == EnemyRank.Normal).Select(r => r.Turns));
        return new CriterionRow("1", "通常戦のターン数（中央値）", "6〜10", Num(median),
            median >= NormalTurnsMin && median <= NormalTurnsMax ? Verdict.Pass : Verdict.Fail,
            $"型 6 種 × 通常 {run.NormalCount} 体、勝ち負けを問わず終わったターン{StallText(typed.Where(r => r.Rank == EnemyRank.Normal))}。"
            + $"精鋭は {Num(Median(typed.Where(r => r.Rank == EnemyRank.Elite).Select(r => r.Turns)))}",
            median);
    }

    /// <summary>Criterion 2: battle length against the bosses.</summary>
    private static CriterionRow BossTurns(Run run)
    {
        var bossRuns = run.Typed.Where(r => r.Rank == EnemyRank.Boss).ToList();
        double median = Median(bossRuns.Select(r => r.Turns));
        return new CriterionRow("2", "ボス戦のターン数", "12〜18", Num(median),
            median >= BossTurnsMin && median <= BossTurnsMax ? Verdict.Pass : Verdict.Fail,
            $"型 6 種 × ボス {Enemies.All.Count(e => e.Rank == EnemyRank.Boss)} 体の中央値（勝ち負けを問わない{StallText(bossRuns)}）", median);
    }

    /// <summary>Criterion 3 (first three): the cost-1 deck against the types' average.</summary>
    private static CriterionRow CostOneDeck(Run run)
    {
        var first = run.First;
        return new CriterionRow("3", "コスト 1 だけで組んだデッキの勝率", "型 6 種の平均を 10 pt 以上上回らない",
            $"{Pct(first.CostOneWinRate)}（平均 {Pct(first.TypeAverageWinRate)}、{Pt(first.CostOneLead)}）",
            first.CostOnePasses ? Verdict.Pass : Verdict.Fail,
            $"{run.CostOne.Basis}。全 {Enemies.All.Count} 体の平均。精鋭とボスだけなら {Pct(WinRate(run.Results.Main.Where(r => r.DeckId == run.CostOne.Id && r.Rank != EnemyRank.Normal)))}"
            + $" / 型の平均 {Pct(run.Archetypes.Average(d => WinRate(run.Typed.Where(r => r.DeckId == d.Id && r.Rank != EnemyRank.Normal))))}",
            first.CostOneLead);
    }

    /// <summary>Criterion 4 (first three): plays a turn.</summary>
    private static CriterionRow PlaysPerTurn(Run run)
    {
        var first = run.First;
        return new CriterionRow("4", "1 ターンのプレイ枚数（中央値）", "2 以上", Num(first.PlaysPerTurnMedian),
            first.PlaysPass ? Verdict.Pass : Verdict.Fail,
            $"型 6 種 × 全敵の全ターン。平均 {Num(run.Typed.SelectMany(r => r.PlaysPerTurn).DefaultIfEmpty(0).Average())}。{CapText(run.Results.Options.MaxPlays)}",
            first.PlaysPerTurnMedian);
    }

    /// <summary>Criterion 5 (first three): the largest stacked blow.</summary>
    private static CriterionRow StackedBlow(Run run)
    {
        var first = run.First;
        string note = $"上限 = 通常敵の最大 HP {first.NormalMaxHp} × 0.6 = {first.SingleHitLimit}。"
            + $"試験台の値は主な戦闘の全てのデッキ（{run.Results.Decks.Count} 種、--only first3 でも同じ）の全試行の最大で、最大を出したのは「{run.BlowDeck}」。"
            + $"机上: {first.DeskCanon.CardName} {first.DeskCanon.Formula}"
            + $"（{(first.DeskCanon.Canon <= first.SingleHitLimit ? "超えない" : "超える")}）、特性込み {first.DeskWithTrait.CardName} {first.DeskWithTrait.WithTrait}";
        return new CriterionRow("5", "重ねた 1 撃の最大値", "通常敵の HP の 6 割を超えない",
            $"試験台 {first.ObservedMaxBlow}（{run.BlowDeck}）/ 机上 {first.DeskCanon.Canon}",
            first.BlowPasses ? Verdict.Pass : Verdict.Fail, note, first.ObservedMaxBlow);
    }

    /// <summary>Criterion 6: spread of the six types.</summary>
    private static CriterionRow TypeSpread(Run run)
    {
        var typeRates = run.Archetypes.Select(d => (d.Name, Rate: RateOf(run.Rates, d.Id))).ToList();
        var strongest = typeRates.OrderByDescending(t => t.Rate).First();
        var weakest = typeRates.OrderBy(t => t.Rate).First();
        double spread = strongest.Rate - weakest.Rate;
        return new CriterionRow("6", "デッキの型 6 種の勝率差（最強 − 最弱）", "25 pt 以下",
            $"{spread.ToString("0.0", Inv)} pt（{strongest.Name} {Pct(strongest.Rate)} − {weakest.Name} {Pct(weakest.Rate)}）",
            spread <= TypeSpreadPt ? Verdict.Pass : Verdict.Fail, "§19.8 の 6 型（§13 の「型 5 種」を 6 型と読む）", spread);
    }

    /// <summary>Criterion 8: max stamina 6.</summary>
    private static CriterionRow LowStamina(Run run)
    {
        if (!run.Full) return Skipped("8", "最大スタミナ 6（瘴気 80%）での通常戦勝率", "50% 以上", SkippedNote);
        var results = run.Results;
        double low = WinRate(results.LowStamina);
        return new CriterionRow("8", "最大スタミナ 6（瘴気 80%）での通常戦勝率", "50% 以上", Pct(low),
            low >= LowStaminaWinRatePct ? Verdict.Pass : Verdict.Fail,
            $"BattleSetup.PlayerMaxStamina = {LowMaxStaminaText}。型 6 種 × 通常 {run.NormalCount} 体（満タンでは {Pct(WinRate(run.Typed.Where(r => r.Rank == EnemyRank.Normal)))}）{StallText(results.LowStamina, "は勝てなかった戦闘に数える")}", low);
    }

    /// <summary>Criterion 9: two battles with HP carried.</summary>
    private static CriterionRow CarriedPair(Run run)
    {
        if (!run.Full) return Skipped("9", "HP 持ち越しの 2 連戦の勝率", "40% 以上", SkippedNote);
        var pairs = run.Results.Pairs;
        double pair = pairs.Count == 0 ? 0 : 100.0 * pairs.Count(p => p.Won) / pairs.Count;
        return new CriterionRow("9", "HP 持ち越しの 2 連戦の勝率", "40% 以上", Pct(pair),
            pair >= PairWinRatePct ? Verdict.Pass : Verdict.Fail,
            $"型 6 種 × 通常 {run.NormalCount} 体を次の 1 体と組んだ {Bench.NormalPairs().Count} 組。休憩なしで HP とスタミナを持ち越す"
            + (pairs.Any(p => p.Outcome == Outcome.Stalled) ? $"。詰まり {pairs.Count(p => p.Outcome == Outcome.Stalled)} 組は勝ちに数えない" : ""), pair);
    }

    /// <summary>Criterion 10: positioning policies.</summary>
    private static CriterionRow PolicySwing(Run run)
    {
        if (!run.Full) return Skipped("10", "間合いの方針の差", "勝率が 15 pt 以上動く敵が、敵の 3 分の 2 以上", SkippedNote);
        var policies = run.Results.Policies;
        var three = new[] { Positioning.Close, Positioning.Away, Positioning.InOut };
        int moved = 0;
        foreach (var enemy in Enemies.All)
        {
            var per = three.Select(p => WinRate(policies[p].Where(r => r.EnemyId == enemy.Id))).ToList();
            if (per.Max() - per.Min() >= PolicySwingPt) moved++;
        }
        int total = Enemies.All.Count;
        int needed = (total * 2 + 2) / 3; // two thirds, rounded up (§13, #403)
        return new CriterionRow("10", "間合いの方針の差", "詰め続ける / 離れ続ける / 出入りする で勝率が 15 pt 以上動く敵が、敵の 3 分の 2 以上",
            $"{moved} / {total} 体（合格は {needed} 体以上）", moved >= needed ? Verdict.Pass : Verdict.Fail,
            $"合格の線は敵の数の 3 分の 2 以上（端数は切り上げ）で、コアの敵は今 {total} 体なので {needed} 体以上。"
            + "敵ごとに型 6 種をまとめた勝率の最大 − 最小で数える。全敵の勝率は "
            + string.Join("、", three.Select(p => $"{PolicyName(p)} {Pct(WinRate(policies[p]))}"))
            + StallText(three.SelectMany(p => policies[p]), "は勝てなかった戦闘に数える"), moved);
    }

    /// <summary>Criterion 11: no movement card in the hand.</summary>
    private static CriterionRow NoMoverInHand(Run run)
    {
        var results = run.Results;
        if (!run.Full || results.OpeningHands.Count == 0)
            return Skipped("11", "移動の効果を持つ札が手札に来ない率", $"{Constants.HandDraw} 枚に 1 枚も無い確率が 25% を超えない（初期デッキで実測）", SkippedNote);
        var forty = Sim.Decks.InitialForty();
        var proto = Sim.Decks.Initial();
        var fortyDeal = results.OpeningHands.Single(h => h.DeckId == forty.Id);
        var protoDeal = results.OpeningHands.Single(h => h.DeckId == proto.Id);
        var (fortyCards, fortyMovers) = Movers(forty);
        var (protoCards, protoMovers) = Movers(proto);
        double fortyDesk = 100 * DeskCalc.NoneInHand(fortyCards, fortyMovers, Constants.HandDraw);
        double protoDesk = 100 * DeskCalc.NoneInHand(protoCards, protoMovers, Constants.HandDraw);
        double none = fortyDeal.Rate;
        return new CriterionRow("11", "移動の効果を持つ札が手札に来ない率", $"{Constants.HandDraw} 枚に 1 枚も無い確率が 25% を超えない（初期デッキで実測）",
            $"{Pct(none)}（初期 40 種 × 1、最初の手札 {fortyDeal.Hands} 回）", none <= NoMoverHandPct ? Verdict.Pass : Verdict.Fail,
            $"{forty.Basis}。{fortyCards} 枚中 {fortyMovers} 枚が移動の効果を持つ。試験台の種で TurnLoop.Start と BeginPlayerTurn に最初の手札（{Constants.HandDraw} 枚 = Constants.HandDraw）を配らせて数えた。"
            + $"机上 C({fortyCards - fortyMovers},{Constants.HandDraw}) / C({fortyCards},{Constants.HandDraw}) = {Pct(fortyDesk)}。"
            + $"戦闘中の全ての手札では {Pct(InBattle(results.Main, forty.Id))}。"
            + $"試運転のデッキ（PrototypeDeck、{protoCards} 枚中 {protoMovers} 枚）は最初の手札 {Pct(protoDeal.Rate)}、戦闘中の全ての手札 {Pct(InBattle(results.Main, proto.Id))}、机上 {Pct(protoDesk)}",
            none);
    }

    /// <summary>Criterion 12: turns with nothing in reach.</summary>
    private static CriterionRow NoReach(Run run)
    {
        double noReach = Median(run.Typed.Select(r => r.NoReachTurns));
        return new CriterionRow("12", "届く札が無くて何も当てられないターン", "1 戦に 1 回以下（中央値）", Num(noReach),
            noReach <= NoReachTurnsMax ? Verdict.Pass : Verdict.Fail,
            $"相手向きの札を持つのにターン開始で 1 枚も届かず、そのターンに相手向きの札を 1 枚も出せなかったターン。平均 {Num(run.Typed.Select(r => r.NoReachTurns).DefaultIfEmpty(0).Average())}",
            noReach);
    }

    /// <summary>Criterion 13: staying at 2 or more.</summary>
    private static CriterionRow KeepTwo(Run run)
    {
        if (!run.Full) return Skipped("13", "離れて削る型の勝率", "間合い 2 以上に居続ける方針が、ほかの方針の平均を 15 pt 以上上回らない", SkippedNote);
        var policies = run.Results.Policies;
        double keep = WinRate(policies[Positioning.KeepTwo]);
        double others = new[] { Positioning.Close, Positioning.Away, Positioning.InOut }.Average(p => WinRate(policies[p]));
        return new CriterionRow("13", "離れて削る型の勝率", "間合い 2 以上に居続ける方針が、ほかの方針の平均を 15 pt 以上上回らない",
            $"{Pct(keep)}（ほか {Pct(others)}、{Pt(keep - others)}）", keep - others < KeepTwoLeadPt ? Verdict.Pass : Verdict.Fail,
            "ほかの方針 = 詰め続ける / 離れ続ける / 出入りする。型 6 種 × 全敵"
            + StallText(policies[Positioning.KeepTwo], "（間合い 2 以上に居続ける方針）は勝てなかった戦闘に数える"), keep - others);
    }

    /// <summary>Criterion 14: a person's judgment time, which a bot cannot measure.</summary>
    private static CriterionRow JudgmentTime() =>
        new CriterionRow("14", "1 ターンの判断時間（人手、中央値）", "20 秒以下", "—", Verdict.NotMeasurable,
            "人が遊んで測る値で、打ち手では測れない");

    /// <summary>Criterion S13: turns of 3 plays or more.</summary>
    private static CriterionRow ThreePlayTurns(Run run)
    {
        int maxPlays = run.Results.Options.MaxPlays;
        double three = Median(run.Typed.Select(r => r.TurnsOfThreeOrMore));
        return new CriterionRow("S13", "1 戦のうち 3 枚以上出したターンの回数", "2 回以上。0 回なら回復 3 か手薄の定義を見直す", Num(three),
            three >= ThreePlayTurnsMin ? Verdict.Pass : Verdict.Fail,
            $"1 戦ごとに数えた中央値。平均 {Num(run.Typed.Select(r => r.TurnsOfThreeOrMore).DefaultIfEmpty(0).Average())}。{CapText(maxPlays)}"
            + (maxPlays is > 0 and < 3 ? "上限が 3 未満なので、3 枚以上のターンは仕組みの上で起きない" : ""), three);
    }

    /// <summary>Criterion S14: battles with many movement plays against those with few.</summary>
    private static CriterionRow ManyMoves(Run run)
    {
        var many = run.Typed.Where(r => r.MovementPlays >= ManyMovementPlays).ToList();
        var few = run.Typed.Where(r => r.MovementPlays < ManyMovementPlays).ToList();
        double manyRate = WinRate(many), fewRate = WinRate(few);
        bool movesKnown = many.Count > 0 && few.Count > 0;
        return new CriterionRow("S14", "移動の効果を 2 回以上出した戦いの勝率", "1 回以下の勝率を 10 pt 以上下回らない",
            $"{Pct(manyRate)}（{many.Count} 戦）/ 1 回以下 {Pct(fewRate)}（{few.Count} 戦）",
            !movesKnown ? Verdict.NotMeasurable : manyRate - fewRate > -MovementDeficitPt ? Verdict.Pass : Verdict.Fail,
            "移動の効果 = 前へ / 後ろへ / 押す / 引く を持つ札（俊敏の無料の 1 マスは数えない）", manyRate - fewRate);
    }

    /// <summary>Criterion S15: 5's blow, read by the one-multiplier rule.</summary>
    private static CriterionRow SingleBlow(Run run)
    {
        var first = run.First;
        return new CriterionRow("S15", "1 撃の最大値", $"通常敵 HP の 6 割（{first.SingleHitLimit}）以下",
            $"試験台 {first.ObservedMaxBlow}（{run.BlowDeck}）/ 机上 {first.DeskCanon.Canon}", first.BlowPasses ? Verdict.Pass : Verdict.Fail,
            "5 と同じ値（倍率 1 つの規則で測る。主な戦闘の全てのデッキの全試行の最大）", first.ObservedMaxBlow);
    }

    /// <summary>Criterion S17: the mastery ticks, which BattleCore does not carry yet.</summary>
    private static CriterionRow MasteryTicks() =>
        new CriterionRow("S17", "1 戦の刻み（中央値）", "才能の札で 4 以上、ほかの札で 2 以上", "—", Verdict.NotMeasurable,
            "習熟（刻み）は BattleCore にまだ無い");

    /// <summary>
    /// 7, read as how much the stances add, not as how weak a type is. Both readings must pass:
    /// (a) §13's words: the best deck without a stance card (the no-stance twins and the types that
    /// hold none) is at least the strongest type − 15 pt; (b) swordsman_cards_v4 §5-7: no twin loses
    /// 15 pt or more to the type it was made from (exactly 15 below fails, as 3 / 13 / S14 read their
    /// bounds). battle_core_v4 §19.6 calls 7 the cap on stances becoming a must, so a type that is
    /// weak with or without its stances is 6's business, not 7's. (b) passing implies (a) (the
    /// strongest type's own twin, or the strongest type itself when it holds no stance, is then
    /// within 15 pt), so (b) decides in practice; (a) is kept as the canon's literal number. The value
    /// is (b)'s smallest twin − type.
    /// </summary>
    public static CriterionRow NoStance(IReadOnlyDictionary<string, double> rates, IReadOnlyList<SimDeck> archetypes)
    {
        const string Criterion = "スタンス無しデッキの勝率";
        const string Target = "最強 − 15 pt 以上（差し替え版は元の型に 15 pt 以上負けない）";
        double Rate(string id) => RateOf(rates, id);
        var gaps = archetypes
            .Select(d => (Type: d, Twin: Sim.Decks.WithoutStances(d)))
            .Where(p => p.Twin != null)
            .Select(p => (p.Twin!.Name, TypeName: p.Type.Name, Twin: Rate(p.Twin.Id), Gap: Rate(p.Twin.Id) - Rate(p.Type.Id)))
            .ToList();
        if (gaps.Count == 0) return new CriterionRow("7", Criterion, Target, "—", Verdict.NotMeasurable, "スタンスの札を持つ型が無い");

        var strongest = archetypes.Select(d => (d.Name, Rate: Rate(d.Id))).OrderByDescending(t => t.Rate).First();
        var bestNoStance = gaps.Select(g => (g.Name, Rate: g.Twin))
            .Concat(archetypes.Where(d => !d.HasStance).Select(d => (d.Name, Rate: Rate(d.Id))))
            .OrderByDescending(t => t.Rate).First();
        double floor = strongest.Rate - NoStanceGapPt;
        bool literal = bestNoStance.Rate >= floor;
        var worstGap = gaps.OrderBy(g => g.Gap).First();
        bool perType = worstGap.Gap > -NoStanceGapPt;
        return new CriterionRow("7", Criterion, Target,
            $"スタンス無しの最良 {Pct(bestNoStance.Rate)}（{bestNoStance.Name}）/ 下限 {Pct(floor)}（最強 {strongest.Name} {Pct(strongest.Rate)} − 15 pt）。"
            + $"元の型との差の最小 {Pt(worstGap.Gap)}（{worstGap.TypeName}）",
            literal && perType ? Verdict.Pass : Verdict.Fail,
            "スタンスがどれだけ勝率を足しているかで読み、次の 2 つがどちらも通れば合格。"
            + $"(a) スタンスの札を持たないデッキ（差し替え版と、もともとスタンスを持たない型）で最もよいものが最強 − 15 pt 以上: {VerdictText(literal ? Verdict.Pass : Verdict.Fail)}。"
            + $"(b) 差し替え版のどれも元の型に 15 pt 以上負けない（swordsman_cards_v4 §5-7）: {VerdictText(perType ? Verdict.Pass : Verdict.Fail)}。"
            + "差し替え版 − 元の型は "
            + string.Join("、", gaps.Select(g => $"{g.TypeName} {Pt(g.Gap)}（{Pct(g.Twin)}）")),
            worstGap.Gap);
    }

    /// <summary>
    /// S16, read per type: each type that holds a stance card must have its own median in 1〜2 (the
    /// canon reads 16 over the battles of a deck, and §19 asks whether a type that lays many stances is
    /// too strong). Pooling the types would let a type with one stance card (at most 1 a battle, since
    /// a played stance is removed) pull a type at 3 or more back into range. The value is the highest
    /// of the medians.
    /// </summary>
    public static CriterionRow StancePlays(IReadOnlyList<BattleRecord> records, IReadOnlyList<SimDeck> archetypes)
    {
        const string Criterion = "1 戦のスタンスのプレイ回数（中央値）";
        const string Target = "1〜2。3 以上なら 1 枚が軽すぎるか弱すぎる";
        var holders = archetypes.Where(d => d.HasStance).ToList();
        var medians = holders
            .Select(d => (d.Name, Median: Median(records.Where(r => r.DeckId == d.Id).Select(r => r.StancePlays))))
            .ToList();
        if (medians.Count == 0) return new CriterionRow("S16", Criterion, Target, "—", Verdict.NotMeasurable, "スタンスの札を持つ型が無い");
        var ids = holders.Select(d => d.Id).ToHashSet();
        double pooled = Median(records.Where(r => ids.Contains(r.DeckId)).Select(r => r.StancePlays));
        var outside = medians.Where(m => m.Median < StancePlaysMin || m.Median > StancePlaysMax).ToList();
        return new CriterionRow("S16", Criterion, Target,
            string.Join(" / ", medians.Select(m => $"{m.Name} {Num(m.Median)}")),
            outside.Count == 0 ? Verdict.Pass : Verdict.Fail,
            "スタンスの札を持つ型ごとの中央値で、全ての型が 1〜2 に入れば合格"
            + (outside.Count == 0 ? "" : $"（外れたのは{string.Join("、", outside.Select(m => m.Name))}）")
            + $"。型をまとめた中央値は {Num(pooled)}",
            medians.Max(m => m.Median));
    }

    /// <summary>A main deck's name for the table (the JSON keeps the id); the id itself when the deck is not known.</summary>
    public static string DeckName(IEnumerable<SimDeck> decks, string deckId) =>
        decks.FirstOrDefault(d => d.Id == deckId)?.Name ?? deckId;

    private static string LowMaxStaminaText => Bench.LowMaxStamina.ToString(Inv);

    /// <summary>The player's per-turn cap, in words for the notes of 4 and S13.</summary>
    public static string CapText(int maxPlays) =>
        maxPlays > 0
            ? $"打ち手は 1 ターン {maxPlays} 枚まで（--max-plays {maxPlays}。§13 の打ち手は上限を置かない）。"
            : "打ち手に 1 ターンの枚数の上限は無い（値が 0 を超える札を出し続ける。--max-plays で上限を付けられる）。";

    /// <summary>
    /// 「、詰まり n 戦<paramref name="counted"/>」 when any of the records stalled, else empty. By default
    /// it says the stalls sit in a turn median at the turn limit.
    /// </summary>
    private static string StallText(IEnumerable<BattleRecord> records, string? counted = null)
    {
        int stalls = records.Count(r => r.Outcome == Outcome.Stalled);
        return stalls == 0 ? "" : $"、詰まり {stalls} 戦{counted ?? $"を {Runner.TurnLimit} ターンとして含む"}";
    }

    /// <summary>The cards of a deck and how many of them carry a movement effect.</summary>
    private static (int Cards, int Movers) Movers(SimDeck deck)
    {
        var built = deck.Build();
        return (built.Count, built.Count(c => AttributeRule.HasMovement(c.Def.Face)));
    }

    /// <summary>The share of the hands dealt in a deck's main battles with no movement card.</summary>
    private static double InBattle(IEnumerable<BattleRecord> main, string deckId)
    {
        var runs = main.Where(r => r.DeckId == deckId).ToList();
        int hands = runs.Sum(r => r.Hands);
        return hands == 0 ? 0 : 100.0 * runs.Sum(r => r.HandsWithoutMovement) / hands;
    }

    /// <summary>One deck × enemy × variant that stalled at least once.</summary>
    public sealed record StallCount(string Variant, Positioning Policy, string DeckId, string EnemyId, int Runs, int Stalls);

    /// <summary>
    /// Every deck × enemy (× policy) with a stall, in run order: main, the policies, max stamina 6,
    /// then the pairs (a pair stalls when either battle does; its enemy is "first+second").
    /// </summary>
    public static IReadOnlyList<StallCount> Stalls(BenchResults results)
    {
        IEnumerable<StallCount> Of(IEnumerable<BattleRecord> records, string variant, Positioning policy) =>
            records.GroupBy(r => (r.DeckId, r.EnemyId))
                .Select(g => new StallCount(variant, policy, g.Key.DeckId, g.Key.EnemyId, g.Count(), g.Count(r => r.Outcome == Outcome.Stalled)))
                .Where(c => c.Stalls > 0);

        var pairs = results.Pairs.GroupBy(p => (p.DeckId, p.FirstId, p.SecondId))
            .Select(g => new StallCount("pair", Positioning.Neutral, g.Key.DeckId, g.Key.FirstId + "+" + g.Key.SecondId, g.Count(), g.Count(p => p.Outcome == Outcome.Stalled)))
            .Where(c => c.Stalls > 0);

        return Of(results.Main, "main", Positioning.Neutral)
            .Concat(results.Policies.OrderBy(p => p.Key).SelectMany(p => Of(p.Value, "policy", p.Key)))
            .Concat(Of(results.LowStamina, "stamina6", Positioning.Neutral))
            .Concat(pairs)
            .ToList();
    }

    private static CriterionRow Skipped(string id, string criterion, string target, string note) =>
        new CriterionRow(id, criterion, target, "—", Verdict.Skipped, note);

    public static string PolicyName(Positioning policy) => policy switch
    {
        Positioning.Neutral => "位置の偏りなし",
        Positioning.Close => "詰め続ける",
        Positioning.Away => "離れ続ける",
        Positioning.InOut => "出入りする",
        Positioning.KeepTwo => "間合い 2 以上に居続ける",
        _ => policy.ToString(),
    };

    public static string VerdictText(Verdict verdict) => verdict switch
    {
        Verdict.Pass => "pass",
        Verdict.Fail => "fail",
        Verdict.NotMeasurable => "not measurable",
        Verdict.Skipped => "skipped",
        _ => verdict.ToString(),
    };
}
