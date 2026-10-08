namespace BattleCore.Sim;

/// <summary>
/// What to run. OnlyFirstThree runs the main battles only (every deck of <see cref="Bench.MainDecks"/>,
/// the same decks as a full run, so 基準 5 / S15's maximum over every run reads the same records).
/// MaxPlays caps the cards the player plays a turn in every battle (0: no cap, the default; see
/// <see cref="GreedyPlayer.MaxPlays"/>).
/// </summary>
public sealed record BenchOptions(int Seed = BenchOptions.DefaultSeed, int Runs = BenchOptions.DefaultRuns, bool OnlyFirstThree = false, int Threads = 0, int MaxPlays = 0)
{
    public const int DefaultSeed = 1;

    /// <summary>§13 基準 11: opening hands dealt per run (so the default 200 runs deal 20,000 hands).</summary>
    public const int HandsPerRun = 100;

    /// <summary>
    /// Battles per (deck × enemy × policy). 200 makes a full run about 110,000 battles, under a minute
    /// on 16 threads; a win rate then moves by about 3 pt at most from one seed to another.
    /// </summary>
    public const int DefaultRuns = 200;
}

/// <summary>One pair of §13 「HP 持ち越しの 2 連戦」 for a deck: won when both battles were, stalled when either hit the turn limit.</summary>
public sealed record PairRecord(string DeckId, string FirstId, string SecondId, Outcome Outcome)
{
    public bool Won => Outcome == Outcome.Won;
}

/// <summary>
/// §13 基準 11 on a deck: how many opening hands were dealt (TurnLoop.Start + BeginPlayerTurn, one
/// seed each) and how many of them held no card with a movement effect.
/// </summary>
public sealed record HandDeal(string DeckId, int Hands, int WithoutMovement)
{
    public double Rate => Hands == 0 ? 0 : 100.0 * WithoutMovement / Hands;
}

/// <summary>Every battle the bench ran, sorted by what the criteria read.</summary>
public sealed class BenchResults
{
    public required BenchOptions Options { get; init; }

    /// <summary>The decks that fought the main battles, archetypes first.</summary>
    public required IReadOnlyList<SimDeck> Decks { get; init; }

    /// <summary>Neutral policy, full stamina: every deck of <see cref="Decks"/> × every enemy × runs.</summary>
    public required IReadOnlyList<BattleRecord> Main { get; init; }

    /// <summary>The six types under each positioning policy but Neutral (empty under --only first3).</summary>
    public required IReadOnlyDictionary<Positioning, IReadOnlyList<BattleRecord>> Policies { get; init; }

    /// <summary>The six types with max stamina 6 against the normal enemies (empty under --only first3).</summary>
    public required IReadOnlyList<BattleRecord> LowStamina { get; init; }

    /// <summary>The six types over the pairs of normal enemies (empty under --only first3).</summary>
    public required IReadOnlyList<PairRecord> Pairs { get; init; }

    /// <summary>§13 基準 11's opening hands: the initial forty first, then the initial deck (empty under --only first3).</summary>
    public required IReadOnlyList<HandDeal> OpeningHands { get; init; }
}

/// <summary>Builds the job list, runs it in parallel and puts the records back in job order (so the output never depends on the thread count).</summary>
public static class Bench
{
    /// <summary>§13 「最大スタミナ 6（瘴気 80%）」.</summary>
    public const int LowMaxStamina = 6;

    public static IReadOnlyList<Positioning> PolicyRuns { get; } = new[] { Positioning.Close, Positioning.Away, Positioning.InOut, Positioning.KeepTwo };

    /// <summary>
    /// The decks of the main battles: the six types, the cost-1 deck, the no-stance twins, the initial
    /// deck, the initial forty × 1 and the plain deck. The same under --only first3: 基準 5 / S15 take
    /// the maximum blow over every run of these decks, and a smaller set would change the verdict.
    /// </summary>
    public static IReadOnlyList<SimDeck> MainDecks()
    {
        var decks = new List<SimDeck>(Sim.Decks.Archetypes) { Sim.Decks.CostOneOnly() };
        decks.AddRange(Sim.Decks.NoStanceTwins());
        decks.Add(Sim.Decks.Initial());
        decks.Add(Sim.Decks.InitialForty());
        decks.Add(Sim.Decks.Plain());
        return decks;
    }

    /// <summary>
    /// §13 基準 11: deals <paramref name="hands"/> opening hands of the deck the way a battle does —
    /// TurnLoop.Start shuffles, BeginPlayerTurn draws <see cref="Constants.HandDraw"/> — each from its
    /// own seed, and counts the hands with no card that moves anybody. The enemy only stands there:
    /// nothing it does comes before the first draw.
    /// </summary>
    public static HandDeal DealOpeningHands(SimDeck deck, int baseSeed, int hands)
    {
        var enemy = Enemies.All[0];
        int without = 0;
        for (int i = 0; i < hands; i++)
        {
            var rng = new SeededRng(Runner.SeedFor(baseSeed, "hand", deck.Id, i));
            var state = TurnLoop.Start(Runner.Setup(deck, enemy), rng).State;
            state = TurnLoop.BeginPlayerTurn(state, rng).State;
            if (!state.Hand.Any(c => AttributeRule.HasMovement(c.Def.Face))) without++;
        }
        return new HandDeal(deck.Id, hands, without);
    }

    /// <summary>The normal enemies, in roster order: the pairs of the 2 連戦 are each with the next (the last with the first).</summary>
    public static IReadOnlyList<(EnemyDef First, EnemyDef Second)> NormalPairs()
    {
        var normal = Enemies.All.Where(e => e.Rank == EnemyRank.Normal).ToList();
        return normal.Select((e, i) => (e, normal[(i + 1) % normal.Count])).ToList();
    }

    public static BenchResults Run(BenchOptions options)
    {
        var decks = MainDecks();
        var enemies = Enemies.All;
        var neutral = new GreedyPlayer(Positioning.Neutral, options.MaxPlays);

        var main = new List<Func<BattleRecord>>();
        foreach (var deck in decks)
        foreach (var enemy in enemies)
        for (int run = 0; run < options.Runs; run++)
        {
            int seed = Runner.SeedFor(options.Seed, "main", enemy.Id, run);
            main.Add(() => Runner.Fight(deck, Runner.Setup(deck, enemy), neutral, seed).Record);
        }

        var policyJobs = new List<(Positioning Policy, Func<BattleRecord> Job)>();
        var low = new List<Func<BattleRecord>>();
        var pairs = new List<Func<PairRecord>>();
        var deals = new List<Func<HandDeal>>();
        if (!options.OnlyFirstThree)
        {
            foreach (var policy in PolicyRuns)
            {
                var player = new GreedyPlayer(policy, options.MaxPlays);
                foreach (var deck in Sim.Decks.Archetypes)
                foreach (var enemy in enemies)
                for (int run = 0; run < options.Runs; run++)
                {
                    int seed = Runner.SeedFor(options.Seed, "main", enemy.Id, run);
                    policyJobs.Add((policy, () => Runner.Fight(deck, Runner.Setup(deck, enemy), player, seed).Record));
                }
            }

            foreach (var deck in Sim.Decks.Archetypes)
            foreach (var enemy in enemies.Where(e => e.Rank == EnemyRank.Normal))
            for (int run = 0; run < options.Runs; run++)
            {
                int seed = Runner.SeedFor(options.Seed, "stamina6", enemy.Id, run);
                low.Add(() => Runner.Fight(deck, Runner.Setup(deck, enemy, maxStamina: LowMaxStamina), neutral, seed).Record);
            }

            foreach (var deck in Sim.Decks.Archetypes)
            foreach (var (first, second) in NormalPairs())
            for (int run = 0; run < options.Runs; run++)
            {
                int seed = Runner.SeedFor(options.Seed, "pair", first.Id + "+" + second.Id, run);
                pairs.Add(() => new PairRecord(deck.Id, first.Id, second.Id, Runner.FightPair(deck, first, second, neutral, seed)));
            }

            int hands = options.Runs * BenchOptions.HandsPerRun;
            foreach (var deck in new[] { Sim.Decks.InitialForty(), Sim.Decks.Initial() })
            {
                deals.Add(() => DealOpeningHands(deck, options.Seed, hands));
            }
        }

        var mainOut = RunAll(main, options.Threads);
        var policyOut = RunAll(policyJobs.Select(p => p.Job).ToList(), options.Threads);
        var policies = new Dictionary<Positioning, IReadOnlyList<BattleRecord>>();
        foreach (var policy in PolicyRuns)
        {
            policies[policy] = policyJobs.Select((p, i) => (p.Policy, Record: policyOut[i])).Where(p => p.Policy == policy).Select(p => p.Record).ToList();
        }

        return new BenchResults
        {
            Options = options,
            Decks = decks,
            Main = mainOut,
            Policies = policies,
            LowStamina = RunAll(low, options.Threads),
            Pairs = RunAll(pairs, options.Threads),
            OpeningHands = RunAll(deals, options.Threads),
        };
    }

    /// <summary>Runs every job, results in job order whatever the thread count.</summary>
    public static T[] RunAll<T>(IReadOnlyList<Func<T>> jobs, int threads)
    {
        var results = new T[jobs.Count];
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = threads > 0 ? threads : Environment.ProcessorCount };
        Parallel.For(0, jobs.Count, parallel, i => results[i] = jobs[i]());
        return results;
    }
}
