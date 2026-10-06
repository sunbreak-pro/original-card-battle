using BattleCore;
using BattleCore.Sim;

namespace BattleCore.Sim.Tests;

/// <summary>#53: the bench replays, plays only legal cards, fights with legal decks, and reads the criteria right.</summary>
public class ReplayTests
{
    [Test]
    public void TheSameSeed_WritesTheSameJson_WhateverTheThreadCount()
    {
        string Json(int threads)
        {
            var results = Bench.Run(new BenchOptions(Seed: 7, Runs: 1, OnlyFirstThree: false, Threads: threads));
            var rows = Criteria.Compute(results);
            var first = Criteria.ComputeFirstThree(results.Main, Decks.Archetypes, Decks.CostOneOnly().Id, CardCatalog.All, Enemies.All);
            return Report.Json(results, rows, first);
        }

        string once = Json(1);
        Assert.That(Json(4), Is.EqualTo(once));
        Assert.That(Json(1), Is.EqualTo(once));
    }

    [Test]
    public void ADifferentSeed_DealsDifferentBattles()
    {
        var a = Bench.Run(new BenchOptions(Seed: 1, Runs: 2, OnlyFirstThree: true));
        var b = Bench.Run(new BenchOptions(Seed: 2, Runs: 2, OnlyFirstThree: true));
        Assert.That(a.Main.Select(r => r.Turns + "/" + r.HpLeft), Is.Not.EqualTo(b.Main.Select(r => r.Turns + "/" + r.HpLeft)));
    }

    [Test]
    public void TheSeedHash_IsFnv1a_AndMovesWithTheRunAndTheBaseSeed()
    {
        // FNV-1a of "a" is 0xE40C292C; the top bit is dropped so a seed is never negative.
        Assert.That(Runner.StableHash("a"), Is.EqualTo(0x640C292C));
        Assert.That(Runner.SeedFor(1, "main", "shadow_hound", 3), Is.Not.EqualTo(Runner.SeedFor(1, "main", "shadow_hound", 4)));
        Assert.That(Runner.SeedFor(1, "main", "shadow_hound", 3), Is.Not.EqualTo(Runner.SeedFor(2, "main", "shadow_hound", 3)));
    }

    /// <summary>
    /// Common random numbers: in a real run, every deck of the main battles and every positioning
    /// policy meets an enemy with the same seed for its n-th battle, and the seeds differ from run to run.
    /// </summary>
    [Test]
    public void EveryDeckAndPolicy_MeetsAnEnemyWithTheSameSeeds()
    {
        var results = Bench.Run(new BenchOptions(Seed: 1, Runs: 2));
        // Each (deck, enemy) group holds its runs in job order; key it by enemy and compare across decks and policies.
        IEnumerable<(string Source, string Enemy, int[] Seeds)> Groups(string source, IEnumerable<BattleRecord> records) =>
            records.GroupBy(r => (r.DeckId, r.EnemyId)).Select(g => ($"{source}/{g.Key.DeckId}", g.Key.EnemyId, g.Select(r => r.Seed).ToArray()));

        var groups = Groups("main", results.Main)
            .Concat(results.Policies.SelectMany(p => Groups(p.Key.ToString(), p.Value)))
            .ToList();
        Assert.That(groups.Select(g => g.Source).Distinct().Count(),
            Is.EqualTo(results.Decks.Count + Bench.PolicyRuns.Count * Decks.Archetypes.Count));

        foreach (var enemy in groups.GroupBy(g => g.Enemy))
        {
            var expected = enemy.First().Seeds;
            Assert.That(expected, Has.Length.EqualTo(2));
            Assert.That(expected.Distinct().Count(), Is.EqualTo(2), $"{enemy.Key}: the runs have different seeds");
            foreach (var g in enemy) Assert.That(g.Seeds, Is.EqualTo(expected), $"{g.Source} vs {enemy.Key}");
        }
    }

    /// <summary>--only first3 fights the same main battles as a full run, so 基準 5 / S15 (the max over every run) read the same.</summary>
    [Test]
    public void OnlyFirstThree_FightsTheSameMainBattles_AsAFullRun()
    {
        var full = Bench.Run(new BenchOptions(Seed: 4, Runs: 1));
        var first3 = Bench.Run(new BenchOptions(Seed: 4, Runs: 1, OnlyFirstThree: true));
        Assert.That(first3.Decks.Select(d => d.Id), Is.EqualTo(full.Decks.Select(d => d.Id)));
        Assert.That(first3.Main, Is.EqualTo(full.Main).Using<BattleRecord>((a, b) => a.DeckId == b.DeckId && a.EnemyId == b.EnemyId && a.Seed == b.Seed && a.MaxBlow == b.MaxBlow && a.Outcome == b.Outcome));

        var fullRows = Criteria.Compute(full);
        var first3Rows = Criteria.Compute(first3);
        foreach (string id in new[] { "3", "4", "5", "7", "S15", "S16" })
        {
            var a = fullRows.Single(r => r.Id == id);
            var b = first3Rows.Single(r => r.Id == id);
            Assert.That((b.Measured, b.Verdict), Is.EqualTo((a.Measured, a.Verdict)), id);
        }
    }
}

public class LegalPlayTests
{
    private static IEnumerable<TestCaseData> EveryDeckAndPolicy()
    {
        foreach (var deck in Bench.MainDecks())
        foreach (Positioning policy in Enum.GetValues<Positioning>())
        {
            yield return new TestCaseData(deck.Id, policy).SetName($"{deck.Id} {policy}");
        }
    }

    /// <summary>
    /// Walks battles by hand: every play the player chooses is one CanPlay allows, and every free step
    /// one TakeFreeStep takes. (Runner.Fight throws on an illegal choice too; this asserts it outright.)
    /// </summary>
    [TestCaseSource(nameof(EveryDeckAndPolicy))]
    public void TheGreedyPlayer_NeverMakesAnIllegalPlay(string deckId, Positioning policy)
    {
        var deck = Bench.MainDecks().Single(d => d.Id == deckId);
        var player = new GreedyPlayer(policy);
        int plays = 0;
        foreach (var enemy in Enemies.All)
        {
            var rng = new SeededRng(Runner.SeedFor(3, "legal", enemy.Id, 0));
            var state = TurnLoop.Start(Runner.Setup(deck, enemy), rng).State;
            while (state.Result == GameResult.Ongoing && state.Turn < Runner.TurnLimit)
            {
                state = TurnLoop.BeginPlayerTurn(state, rng).State;
                if (state.Result != GameResult.Ongoing) break;
                int step = player.ChooseFreeStep(state);
                if (step != 0)
                {
                    Assert.That(TurnLoop.CanTakeFreeStep(state), Is.True);
                    state = TurnLoop.TakeFreeStep(state, step).State;
                }
                Choice? choice;
                while (state.Result == GameResult.Ongoing && (choice = player.Choose(state)) != null)
                {
                    Assert.That(TurnLoop.CanPlay(state, choice.InstanceId, choice.Target), Is.EqualTo(PlayRefusal.None),
                        $"{deckId} vs {enemy.Id} turn {state.Turn}: {choice.InstanceId}");
                    Assert.That(choice.Value, Is.GreaterThan(0));
                    state = TurnLoop.PlayCard(state, choice.InstanceId, rng, choice.Target).State;
                    plays++;
                }
                if (state.Result != GameResult.Ongoing) break;
                state = TurnLoop.EndTurn(state, rng).State;
            }
        }
        Assert.That(plays, Is.GreaterThan(0));
    }

    [Test]
    public void ChooseReturnsNothing_OutsideThePlayersTurn()
    {
        var state = TurnLoop.Start(Runner.Setup(Decks.Archetypes[0], Enemies.PolearmWarped), new SeededRng(1)).State;
        Assert.That(new GreedyPlayer().Choose(state), Is.Null, "before BeginPlayerTurn");
    }

    /// <summary>
    /// The look-ahead copy draws the real next cards, so a value that read them would see the future.
    /// Two boards that differ only in the order of the draw pile must give a drawing card the same
    /// score: here 足運び draws either 投げ刃 (an attack in reach from 2) or 鉄の受け.
    /// </summary>
    [Test]
    public void AScore_DoesNotDependOnTheOrderOfTheDrawPile()
    {
        var deck = Decks.Archetypes[0];
        var rng = new SeededRng(5);
        var start = TurnLoop.Start(Runner.Setup(deck, Enemies.PolearmWarped), rng).State;
        start = TurnLoop.BeginPlayerTurn(start, rng).State;

        CardInstance Card(string id, int copy = 0) => new CardInstance($"{id}-x{copy}", CardCatalog.ById(id));
        var footwork = Card("footwork");
        var hand = new[] { footwork, Card("thrust"), Card("brace") };
        var pile = new[] { Card("throw_blade"), Card("iron_block"), Card("iron_block", 1), Card("deep_breath") };
        var one = start with { Hand = hand, DrawPile = pile, DiscardPile = Array.Empty<CardInstance>() };
        var two = one with { DrawPile = pile.Reverse().ToArray() };
        Assert.That(TurnLoop.CanPlay(one, footwork.InstanceId, one.Nearest), Is.EqualTo(PlayRefusal.None));

        foreach (Positioning policy in Enum.GetValues<Positioning>())
        {
            var player = new GreedyPlayer(policy);
            double a = player.Score(one, footwork, one.Nearest, GreedyPlayer.Value(one));
            double b = player.Score(two, footwork, two.Nearest, GreedyPlayer.Value(two));
            Assert.That(b, Is.EqualTo(a), policy.ToString());
        }
    }

    [Test]
    public void MaxPlays_EndsTheTurnAtTheCap()
    {
        var deck = Decks.CostOneOnly();
        var rng = new SeededRng(11);
        var state = TurnLoop.Start(Runner.Setup(deck, Enemies.PolearmWarped), rng).State;
        state = TurnLoop.BeginPlayerTurn(state, rng).State;
        var capped = new GreedyPlayer(Positioning.Neutral, maxPlays: 2);
        Assert.That(capped.Choose(state, playedThisTurn: 2), Is.Null);
        Assert.That(new GreedyPlayer().Choose(state, playedThisTurn: 2), Is.EqualTo(new GreedyPlayer().Choose(state)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new GreedyPlayer(Positioning.Neutral, maxPlays: -1));

        var results = Bench.Run(new BenchOptions(Seed: 3, Runs: 2, OnlyFirstThree: true, MaxPlays: 2));
        Assert.That(results.Main.SelectMany(r => r.PlaysPerTurn).Max(), Is.LessThanOrEqualTo(2));
        Assert.That(results.Main.Sum(r => r.TurnsOfThreeOrMore), Is.Zero);
    }
}

public class DeckTests
{
    private static IEnumerable<TestCaseData> EveryDeck() =>
        Bench.MainDecks().Select(d => new TestCaseData(d.Id).SetName(d.Id));

    [TestCaseSource(nameof(EveryDeck))]
    public void EveryDeck_PassesTheDeckRules(string deckId)
    {
        var deck = Bench.MainDecks().Single(d => d.Id == deckId);
        var built = deck.Build();
        var check = Cards.Validate(built);
        Assert.That(check.Ok, Is.True, string.Join("; ", check.Errors));
        Assert.That(built.Count, Is.InRange(Constants.DeckMin, Constants.DeckMax));
        Assert.That(deck.Rows.Max(r => r.Copies), Is.LessThanOrEqualTo(Constants.CopiesMax));
        Assert.That(deck.Rows.Select(r => r.CardId), Is.Unique);
    }

    [Test]
    public void TheDeckTypes_AreTheSixOfSection19_8_TwentyCardsEach()
    {
        Assert.That(Decks.Archetypes.Select(d => d.Name), Is.EqualTo(new[] { "溜め放ち", "振り子", "研ぎ", "血路", "一型", "背水" }));
        Assert.That(Decks.Archetypes.Select(d => d.Count), Is.All.EqualTo(Constants.DeckMin));
    }

    [Test]
    public void TheCostOneDeck_HoldsOnlyCostOneCards_EachOnceAtLeast()
    {
        var deck = Decks.CostOneOnly();
        Assert.That(deck.Build().Select(c => c.Def.Cost), Is.All.EqualTo(1));
        Assert.That(deck.Rows.Select(r => r.CardId), Is.EquivalentTo(CardCatalog.All.Where(c => c.Cost == 1).Select(c => c.Id)));
    }

    [Test]
    public void ANoStanceTwin_HoldsNoStanceCard_AndKeepsTheCount()
    {
        foreach (var deck in Decks.Archetypes)
        {
            var twin = Decks.WithoutStances(deck);
            if (!deck.HasStance)
            {
                Assert.That(twin, Is.Null, deck.Id);
                continue;
            }
            Assert.That(twin!.HasStance, Is.False, deck.Id);
            Assert.That(twin.Count, Is.EqualTo(deck.Count), deck.Id);
        }
        Assert.That(Decks.NoStanceTwins(), Is.Not.Empty);
    }

    [Test]
    public void TheInitialDeck_IsThePrototypeDeck()
    {
        Assert.That(Decks.Initial().Build().Select(c => c.InstanceId), Is.EqualTo(PrototypeDeck.Build().Select(c => c.InstanceId)));
    }

    [Test]
    public void TheInitialForty_IsFortyKindsOnceEach_TenOfThemMoving()
    {
        var deck = Decks.InitialForty();
        var built = deck.Build();
        Assert.That(Decks.InitialFortyIds, Is.Unique);
        Assert.That(built, Has.Count.EqualTo(40));
        Assert.That(deck.Rows.Select(r => r.Copies), Is.All.EqualTo(1));
        // battle_core_v4 §14-4: 「移動の効果は初期 40 種中 10 種」.
        Assert.That(built.Count(c => AttributeRule.HasMovement(c.Def.Face)), Is.EqualTo(10));
    }

    [Test]
    public void OpeningHands_AreTheCoresHandSize_AndNearTheDeskValue()
    {
        var deal = Bench.DealOpeningHands(Decks.InitialForty(), baseSeed: 1, hands: 4000);
        Assert.That(deal.Hands, Is.EqualTo(4000));
        // C(30,6) / C(40,6) ≈ 15.5%; 4,000 hands sit within ±2 pt of it nearly always.
        Assert.That(deal.Rate, Is.EqualTo(100 * DeskCalc.NoneInHand(40, 10, Constants.HandDraw)).Within(2.0));

        var rng = new SeededRng(Runner.SeedFor(1, "hand", "initial_forty", 0));
        var state = TurnLoop.Start(Runner.Setup(Decks.InitialForty(), Enemies.All[0]), rng).State;
        state = TurnLoop.BeginPlayerTurn(state, rng).State;
        Assert.That(state.Hand, Has.Count.EqualTo(Constants.HandDraw));
    }

    [Test]
    public void OneStance_HoldsEnoughGuardCards_ForIronWallsDiscount()
    {
        var deck = Decks.Archetypes.Single(d => d.Id == "one_stance");
        Assert.That(deck.Rows.Select(r => r.CardId), Does.Contain("iron_wall"));
        int guards = deck.Build().Count(c => c.Def.Attribute == BattleAttribute.Guard);
        Assert.That(guards, Is.GreaterThanOrEqualTo(6));
    }
}

/// <summary>The criteria that need no battle, on tiny fixtures.</summary>
public class CriteriaTests
{
    private static BattleRecord Rec(string deck, bool won, int turns, int[] plays, int maxBlow = 0, EnemyRank rank = EnemyRank.Normal,
        int moves = 0, int stances = 0, int noReach = 0) =>
        new BattleRecord(deck, "e", rank, won ? Outcome.Won : Outcome.Lost, turns, plays, maxBlow, moves, stances, noReach, 0, plays.Length, 10);

    [Test]
    public void Median_TakesTheMiddle_OrTheMeanOfTheTwoMiddles()
    {
        Assert.That(Criteria.Median(new[] { 3, 1, 2 }), Is.EqualTo(2));
        Assert.That(Criteria.Median(new[] { 4, 1, 2, 3 }), Is.EqualTo(2.5));
        Assert.That(Criteria.Median(Array.Empty<int>()), Is.EqualTo(0));
    }

    [Test]
    public void WinRate_IsWinsOverBattles()
    {
        var records = new[] { Rec("a", true, 5, new[] { 2 }), Rec("a", false, 5, new[] { 2 }), Rec("a", true, 5, new[] { 2 }), Rec("a", true, 5, new[] { 2 }) };
        Assert.That(Criteria.WinRate(records), Is.EqualTo(75));
        Assert.That(Criteria.WinRate(Array.Empty<BattleRecord>()), Is.EqualTo(0));
        Assert.That(Rec("a", false, 3, new[] { 3, 1, 4 }).TurnsOfThreeOrMore, Is.EqualTo(2));
    }

    [Test]
    public void TheFirstThree_ReadTheFixture()
    {
        var types = new[]
        {
            new SimDeck("t1", "型 1", new[] { ("thrust", 1) }, ""),
            new SimDeck("t2", "型 2", new[] { ("thrust", 1) }, ""),
        };
        var main = new List<BattleRecord>
        {
            // t1 wins 1 of 2, t2 wins 2 of 2: the types average 75%.
            Rec("t1", true, 6, new[] { 1, 2, 3 }, maxBlow: 30),
            Rec("t1", false, 6, new[] { 2, 2 }, maxBlow: 20),
            Rec("t2", true, 6, new[] { 1, 1 }, maxBlow: 58),
            Rec("t2", true, 6, new[] { 2 }, maxBlow: 10),
            // The cost-1 deck wins all: 100%, 25 pt over the average. Its plays do not count for the median.
            Rec("ones", true, 6, new[] { 9, 9, 9 }, maxBlow: 12),
            Rec("ones", true, 6, new[] { 9 }, maxBlow: 12),
        };
        var card = new CardDef("big", "大きい札", BattleAttribute.Attack, 3, new Face(Power: 26));
        var roster = new[]
        {
            Enemy("n1", EnemyRank.Normal, 80),
            Enemy("n2", EnemyRank.Normal, 96),
            Enemy("boss", EnemyRank.Boss, 200),
        };

        var first = Criteria.ComputeFirstThree(main, types, "ones", new[] { card }, roster);

        Assert.Multiple(() =>
        {
            Assert.That(first.TypeAverageWinRate, Is.EqualTo(75));
            Assert.That(first.CostOneWinRate, Is.EqualTo(100));
            Assert.That(first.CostOneLead, Is.EqualTo(25));
            Assert.That(first.CostOnePasses, Is.False);
            // The types' turns: 1 2 3 2 2 1 1 2 → median 2.
            Assert.That(first.PlaysPerTurnMedian, Is.EqualTo(2));
            Assert.That(first.PlaysPass, Is.True);
            Assert.That(first.ObservedMaxBlow, Is.EqualTo(58));
            Assert.That(first.ObservedMaxBlowDeck, Is.EqualTo("t2"));
            Assert.That(first.NormalMaxHp, Is.EqualTo(96), "the boss's 200 is not a normal enemy");
            Assert.That(first.SingleHitLimit, Is.EqualTo(57));
            Assert.That(first.BlowPasses, Is.False, "58 is over 57");
            // swordsman_cards_v4 §5-15: a column-3 single attack of 26 stacks to (26 + 9 + 5) × 1.5 = 60.
            Assert.That(first.DeskCanon.Canon, Is.EqualTo(60));
            Assert.That(first.DeskCanon.Formula, Is.EqualTo("(26 + 9 + 5) × 1.5 = 60"));
        });
    }

    [Test]
    public void TheDeskBlow_CountsTheCardsOwnPowerTrait_Separately()
    {
        var heavy = new CardDef("h", "重い札", BattleAttribute.Attack, 2, new Face(Power: 13), new Trait(TraitCondition.FirstPlay, TraitEffect.HeavyBlow));
        var blow = DeskCalc.Of(heavy)!;
        // Column 2 → 3 on the single scale is +8: (13 + 8 + 5) × 1.5 = 39; with 重撃 +6, 48.
        Assert.That(blow.Canon, Is.EqualTo(39));
        Assert.That(blow.WithTrait, Is.EqualTo(48));
        Assert.That(DeskCalc.Of(new CardDef("g", "受け", BattleAttribute.Guard, 2, new Face(Guard: 9))), Is.Null);
    }

    [Test]
    public void TheSingleHitLimit_ComesFromTheRosterNormalEnemies()
    {
        var (hp, limit) = DeskCalc.SingleHitLimit(Enemies.All);
        Assert.That(hp, Is.EqualTo(Enemies.All.Where(e => e.Rank == EnemyRank.Normal).Max(e => e.MaxHp)));
        Assert.That(limit, Is.EqualTo((int)Math.Floor(hp * 0.6)));
    }

    [Test]
    public void NoneInHand_IsTheHypergeometricZero()
    {
        // battle_core_v4 §14-4: the initial forty one each, ten of them moving: C(30,6) / C(40,6) ≈ 15.5%.
        Assert.That(DeskCalc.NoneInHand(40, 10, 6), Is.EqualTo(593775.0 / 3838380.0).Within(1e-12));
        Assert.That(DeskCalc.NoneInHand(20, 0, 6), Is.EqualTo(1));
        Assert.That(DeskCalc.NoneInHand(20, 15, 6), Is.EqualTo(0));
    }

    [Test]
    public void TheTable_HasNineteenRows_AndMarksWhatABotCannotMeasure()
    {
        var results = Bench.Run(new BenchOptions(Seed: 1, Runs: 1, OnlyFirstThree: true));
        var rows = Criteria.Compute(results);
        Assert.That(rows.Select(r => r.Id), Is.EqualTo(new[]
        {
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "S13", "S14", "S15", "S16", "S17",
        }));
        Assert.That(rows.Single(r => r.Id == "14").Verdict, Is.EqualTo(Verdict.NotMeasurable));
        Assert.That(rows.Single(r => r.Id == "S17").Verdict, Is.EqualTo(Verdict.NotMeasurable));
        Assert.That(rows.Where(r => r.Verdict == Verdict.Skipped).Select(r => r.Id), Is.EquivalentTo(new[] { "8", "9", "10", "11", "13" }));
        Assert.That(rows.Single(r => r.Id == "3").Verdict, Is.AnyOf(Verdict.Pass, Verdict.Fail));
    }

    private static BenchResults Fixture(IReadOnlyList<BattleRecord> main, IReadOnlyList<PairRecord>? pairs = null) => new BenchResults
    {
        Options = new BenchOptions(Runs: 1),
        Decks = Bench.MainDecks(),
        Main = main,
        Policies = Bench.PolicyRuns.ToDictionary(p => p, _ => (IReadOnlyList<BattleRecord>)Array.Empty<BattleRecord>()),
        LowStamina = Array.Empty<BattleRecord>(),
        Pairs = pairs ?? Array.Empty<PairRecord>(),
        OpeningHands = Array.Empty<HandDeal>(),
    };

    /// <summary>n battles of a deck, the first <paramref name="won"/> of them won.</summary>
    private static IEnumerable<BattleRecord> Runs(string deck, int n, int won) =>
        Enumerable.Range(0, n).Select(i => Rec(deck, i < won, 5, new[] { 2 }));

    /// <summary>
    /// 7 reads what the stances add: a type that is weak with or without its stances does not fail it
    /// (that is 6's spread), but a twin that loses 15 pt or more to its own type does.
    /// </summary>
    [Test]
    public void Criterion7_ReadsWhatTheStancesAdd_NotHowWeakAType()
    {
        var holders = Decks.Archetypes.Where(d => d.HasStance).ToList();
        Assert.That(holders, Has.Count.EqualTo(3));
        var twins = holders.Select(d => Decks.WithoutStances(d)!).ToList();
        CriterionRow Row(int[] types, int[] withoutStances, int others)
        {
            var main = new List<BattleRecord>();
            foreach (var deck in Decks.Archetypes.Where(d => !d.HasStance)) main.AddRange(Runs(deck.Id, 20, others));
            for (int i = 0; i < holders.Count; i++)
            {
                main.AddRange(Runs(holders[i].Id, 20, types[i]));
                main.AddRange(Runs(twins[i].Id, 20, withoutStances[i]));
            }
            return Criteria.Compute(Fixture(main)).Single(r => r.Id == "7");
        }

        // Seed 1's shape: the strongest type at 100%, the others far below it with or without their
        // stances. The old reading (every twin ≥ strongest − 15) failed this.
        var weakTypes = Row(new[] { 20, 14, 14 }, new[] { 18, 16, 14 }, others: 14); // twin − type: −10 / +10 / 0
        Assert.That(weakTypes.Verdict, Is.EqualTo(Verdict.Pass), weakTypes.Measured);
        Assert.That(weakTypes.Value, Is.EqualTo(-10));
        Assert.That(weakTypes.Measured, Does.Contain($"最良 90.0%（{twins[0].Name}）").And.Contain("下限 85.0%"));
        foreach (var deck in holders) Assert.That(weakTypes.Note, Does.Contain(deck.Name));

        // Exactly 15 pt below its own type fails (b), though (a) holds at the floor.
        var fifteen = Row(new[] { 20, 14, 14 }, new[] { 17, 14, 14 }, others: 14);
        Assert.That(fifteen.Verdict, Is.EqualTo(Verdict.Fail), fifteen.Measured);
        Assert.That(fifteen.Value, Is.EqualTo(-15));
        Assert.That(fifteen.Note, Does.Contain("(a)").And.Contain(": pass。(b)").And.Contain(": fail。"));

        // A weak type that needs its stances fails too, though the best no-stance deck is the strongest.
        var weakNeeds = Row(new[] { 20, 14, 14 }, new[] { 20, 10, 14 }, others: 14);
        Assert.That(weakNeeds.Verdict, Is.EqualTo(Verdict.Fail), weakNeeds.Measured);
        Assert.That(weakNeeds.Value, Is.EqualTo(-20));
        Assert.That(weakNeeds.Measured, Does.Contain($"-20.0 pt（{holders[1].Name}）"));
    }

    /// <summary>S14 「1 回以下の勝率を 10 pt 以上下回らない」: exactly 10 pt below is a fail, like 3 and 13 read their bounds.</summary>
    [Test]
    public void CriterionS14_FailsAtExactlyTenPointsBelow()
    {
        string type = Decks.Archetypes[0].Id;
        IEnumerable<BattleRecord> Battles(int n, int won, int moves) =>
            Enumerable.Range(0, n).Select(i => Rec(type, i < won, 5, new[] { 2 }, moves: moves));

        var tenBelow = Battles(10, 8, moves: 2).Concat(Battles(10, 9, moves: 0)).ToList(); // 80% vs 90%
        var row = Criteria.Compute(Fixture(tenBelow)).Single(r => r.Id == "S14");
        Assert.That(row.Value, Is.EqualTo(-10));
        Assert.That(row.Verdict, Is.EqualTo(Verdict.Fail), row.Measured);

        var fiveBelow = Battles(20, 19, moves: 2).Concat(Battles(20, 20, moves: 0)).ToList(); // 95% vs 100%
        Assert.That(Criteria.Compute(Fixture(fiveBelow)).Single(r => r.Id == "S14").Verdict, Is.EqualTo(Verdict.Pass));
    }

    /// <summary>
    /// S16 is judged per type: a type at 4 fails the row even when a one-stance-card type at 1 pulls the
    /// pooled median down to 2.
    /// </summary>
    [Test]
    public void CriterionS16_IsJudgedPerType_NotOnThePooledMedian()
    {
        var holders = Decks.Archetypes.Where(d => d.HasStance).ToList();
        Assert.That(holders, Has.Count.EqualTo(3));
        List<BattleRecord> Records(params int[] perType) =>
            holders.SelectMany((d, i) => Enumerable.Range(0, 3).Select(_ => Rec(d.Id, true, 5, new[] { 2 }, stances: perType[i]))).ToList();

        var row = Criteria.StancePlays(Records(1, 2, 4), Decks.Archetypes);
        Assert.That(row.Verdict, Is.EqualTo(Verdict.Fail), row.Measured);
        Assert.That(row.Value, Is.EqualTo(4));
        Assert.That(row.Measured, Is.EqualTo($"{holders[0].Name} 1 / {holders[1].Name} 2 / {holders[2].Name} 4"));
        Assert.That(row.Note, Does.Contain(holders[2].Name).And.Contain("まとめた中央値は 2"));

        Assert.That(Criteria.StancePlays(Records(1, 2, 2), Decks.Archetypes).Verdict, Is.EqualTo(Verdict.Pass));
        Assert.That(Criteria.StancePlays(Records(0, 2, 2), Decks.Archetypes).Verdict, Is.EqualTo(Verdict.Fail), "0 is below 1");
    }

    [Test]
    public void TheMaxBlow_IsShownByTheDecksName_AndKeptAsTheIdInTheJson()
    {
        var forty = Decks.InitialForty();
        var main = new List<BattleRecord> { Rec(Decks.Archetypes[0].Id, true, 5, new[] { 2 }, maxBlow: 40), Rec(forty.Id, true, 5, new[] { 2 }, maxBlow: 60) };
        var results = Fixture(main);
        var rows = Criteria.Compute(results);
        foreach (string id in new[] { "5", "S15" })
        {
            string measured = rows.Single(r => r.Id == id).Measured;
            Assert.That(measured, Does.Contain($"60（{forty.Name}）"), id);
            Assert.That(measured, Does.Not.Contain(forty.Id), id);
        }
        var first = Criteria.ComputeFirstThree(main, Decks.Archetypes, Decks.CostOneOnly().Id, CardCatalog.All, Enemies.All);
        Assert.That(Report.Markdown(results, rows, first), Does.Contain($"試験台 60（{forty.Name}）"));
        Assert.That(Report.Json(results, rows, first), Does.Contain($"\"observedMaxSingleHitDeck\": \"{forty.Id}\""));
    }

    [Test]
    public void Stalls_AreCountedByDeckAndEnemy_AndPrintedUnderTheTable()
    {
        var stalled = new BattleRecord("whetstone", "distortion_root", EnemyRank.Boss, Outcome.Stalled, Runner.TurnLimit, new[] { 1 }, 0, 0, 0, 0, 0, 1, 5);
        var main = new List<BattleRecord> { stalled, stalled, Rec("whetstone", true, 5, new[] { 2 }) with { EnemyId = "distortion_root" } };
        var pairs = new[] { new PairRecord("whetstone", "a", "b", Outcome.Stalled), new PairRecord("whetstone", "a", "b", Outcome.Won) };
        var results = Fixture(main, pairs);

        var stalls = Criteria.Stalls(results);
        Assert.That(stalls, Has.Count.EqualTo(2));
        Assert.That(stalls[0], Is.EqualTo(new Criteria.StallCount("main", Positioning.Neutral, "whetstone", "distortion_root", 3, 2)));
        Assert.That(stalls[1].Variant, Is.EqualTo("pair"));
        Assert.That(stalls[1].Stalls, Is.EqualTo(1));
        Assert.That(Criteria.WinRate(main), Is.EqualTo(100.0 / 3), "a stall is a battle that was not won");

        string line = Report.StallLine(results);
        Assert.That(line, Does.Contain("主な戦闘 2 戦"));
        Assert.That(line, Does.Contain("2 連戦 1 組"));
        Assert.That(line, Does.Contain("研ぎ × " + Enemies.ById("distortion_root").Name + " 2/3"));
    }

    private static EnemyDef Enemy(string id, EnemyRank rank, int hp)
    {
        var rest = new EnemyActionDef("wait", "待つ", BattleAttribute.Guard, 1, new Face(Guard: 1), Targets: TargetKind.Self);
        var tree = new[] { "wait" };
        return new EnemyDef(id, id, hp, 10, 2, 1, tree, tree, tree, new Dictionary<string, EnemyActionDef> { ["wait"] = rest }, rank);
    }
}

public class ArgumentTests
{
    [Test]
    public void TheCommandLine_ReadsEveryOption()
    {
        Assert.That(Program.TryParse(new[] { "--seed", "5", "--runs", "12", "--json", "out.json", "--only", "first3", "--threads", "2" },
            out var options, out string? json, out string? trace, out bool help, out string? error), Is.True, error);
        Assert.That(options, Is.EqualTo(new BenchOptions(5, 12, true, 2)));
        Assert.That(json, Is.EqualTo("out.json"));
        Assert.That(trace, Is.Null);
        Assert.That(help, Is.False);

        Assert.That(Program.TryParse(Array.Empty<string>(), out var defaults, out _, out _, out _, out _), Is.True);
        Assert.That(defaults, Is.EqualTo(new BenchOptions()));

        Assert.That(Program.TryParse(new[] { "--only", "everything" }, out _, out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParse(new[] { "--runs", "0" }, out _, out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParse(new[] { "--bogus" }, out _, out _, out _, out _, out _), Is.False);

        Assert.That(Program.TryParse(new[] { "--max-plays", "2" }, out var capped, out _, out _, out _, out _), Is.True);
        Assert.That(capped.MaxPlays, Is.EqualTo(2));
        Assert.That(defaults.MaxPlays, Is.Zero, "no cap unless asked");
        Assert.That(Program.TryParse(new[] { "--max-plays", "0" }, out _, out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParse(new[] { "--max-plays" }, out _, out _, out _, out _, out _), Is.False);
    }

    [Test]
    public void TheTraceSpec_RefusesAnUnknownDeckEnemyOrPolicy()
    {
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound:inout", out var deck, out var enemy, out var policy, out _), Is.True);
        Assert.That((deck!.Id, enemy!.Id, policy), Is.EqualTo(("one_stance", "shadow_hound", Positioning.InOut)));
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound", out _, out _, out policy, out _), Is.True);
        Assert.That(policy, Is.EqualTo(Positioning.Neutral));

        Assert.That(Program.TryParseTrace("one_stance:shadow_hound:Bogus", out _, out _, out _, out string? error), Is.False);
        Assert.That(error, Does.Contain("Bogus"));
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound:7", out _, out _, out _, out _), Is.False, "a number is not a policy");
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound:-1", out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound:close,away", out _, out _, out _, out _), Is.False, "not Close | Away = InOut");
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound:Neutral,KeepTwo", out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound: close", out _, out _, out _, out _), Is.False, "no padding");
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound:", out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParseTrace("one_stance:shadow_hound:KEEPTWO", out _, out _, out policy, out _), Is.True);
        Assert.That(policy, Is.EqualTo(Positioning.KeepTwo));
        Assert.That(Program.TryParseTrace("one_stance:nobody", out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParseTrace("nothing:shadow_hound", out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParseTrace("one_stance", out _, out _, out _, out _), Is.False);
    }

    /// <summary>Runs Main with stdout and stderr captured.</summary>
    private static (int Exit, string Out, string Err) RunMain(params string[] args)
    {
        var savedOut = Console.Out;
        var savedErr = Console.Error;
        var output = new StringWriter();
        var err = new StringWriter();
        Console.SetOut(output);
        Console.SetError(err);
        try
        {
            int exit = Program.Main(args);
            return (exit, output.ToString(), err.ToString());
        }
        finally
        {
            Console.SetOut(savedOut);
            Console.SetError(savedErr);
        }
    }

    [Test]
    public void Help_PrintsTheUsageToStdout_AndExitsZero()
    {
        foreach (string flag in new[] { "-h", "--help" })
        {
            Assert.That(Program.TryParse(new[] { flag }, out _, out _, out _, out bool help, out string? error), Is.True, flag);
            Assert.That((help, error), Is.EqualTo((true, (string?)null)), flag);

            var (exit, output, err) = RunMain(flag);
            Assert.That(exit, Is.Zero, flag);
            Assert.That(output.Trim(), Is.EqualTo(Program.Usage), flag);
            Assert.That(err, Is.Empty, flag);
        }
    }

    /// <summary>A --json path whose folder cannot be made stops with exit 2 before any battle is fought.</summary>
    [Test]
    public void AJsonPathThatCannotBeWritten_ExitsTwo_BeforeTheBattles()
    {
        string file = Path.GetTempFileName();
        try
        {
            // A folder cannot be made under a file.
            string bad = Path.Combine(file, "sub", "out.json");
            Assert.That(Program.TryPrepareJsonPath(bad, out _, out string? error), Is.False);
            Assert.That(error, Does.Contain("--json"));

            var clock = System.Diagnostics.Stopwatch.StartNew();
            var (exit, output, err) = RunMain("--only", "first3", "--runs", "200", "--json", bad);
            Assert.That(exit, Is.EqualTo(2));
            Assert.That(output, Is.Empty, "no table: the run never started");
            Assert.That(err, Does.Contain("--json").And.Contain(Program.Usage), "the error, then the usage like any bad argument");
            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(5));

            Assert.That(Program.TryPrepareJsonPath(Path.GetDirectoryName(file)!, out _, out error), Is.False, "a folder is not a file path");
            Assert.That(Program.TryPrepareJsonPath(Path.Combine(Path.GetDirectoryName(file)!, "sim-ok.json"), out string full, out _), Is.True);
            Assert.That(Path.IsPathFullyQualified(full), Is.True);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Test]
    public void ABadTracePolicy_PrintsTheUsage_AndExitsTwo()
    {
        var saved = Console.Error;
        var err = new StringWriter();
        Console.SetError(err);
        try
        {
            Assert.That(Program.Main(new[] { "--trace", "one_stance:shadow_hound:Bogus" }), Is.EqualTo(2));
        }
        finally
        {
            Console.SetError(saved);
        }
        Assert.That(err.ToString(), Does.Contain(Program.Usage));
    }
}
