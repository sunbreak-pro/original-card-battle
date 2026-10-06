namespace BattleCore.Sim;

/// <summary>
/// How one battle ended. Stalled is still going at <see cref="Runner.TurnLimit"/>: neither a win nor a
/// loss. A win rate counts it in its battles (it is not a win), and the report counts it on its own.
/// </summary>
public enum Outcome
{
    Won,
    Lost,
    Stalled,
}

/// <summary>
/// What one battle adds up to, counted from its event stream. Everything the §13 criteria read is
/// here, so the criteria can be computed (and tested) without running a battle. Seed is the seed the
/// battle was fought with (0 in a hand-made record).
/// </summary>
public sealed record BattleRecord(
    string DeckId,
    string EnemyId,
    EnemyRank Rank,
    Outcome Outcome,
    int Turns,
    IReadOnlyList<int> PlaysPerTurn,
    int MaxBlow,
    int MovementPlays,
    int StancePlays,
    int NoReachTurns,
    int HandsWithoutMovement,
    int Hands,
    int HpLeft,
    int Seed = 0)
{
    public bool Won => Outcome == Outcome.Won;

    /// <summary>§13 基準 13: the turns in which 3 or more cards were played.</summary>
    public int TurnsOfThreeOrMore => PlaysPerTurn.Count(p => p >= 3);
}

/// <summary>Runs battles with the greedy player and records them. Pure apart from the time it takes.</summary>
public static class Runner
{
    /// <summary>A battle not decided by this turn is a stall (the turn limit #192's sweep used).</summary>
    public const int TurnLimit = 60;

    /// <summary>
    /// §12 / roster §0 「開始に要るマス数」: the player's cell 2 + START_GAP + the enemies' sizes, at
    /// least the slice's 6 — the line <see cref="ChainBattle.FieldCells"/> picks.
    /// </summary>
    public static int FieldCellsFor(IReadOnlyList<EnemyDef> enemies) =>
        Math.Max(BattleSetup.SliceFieldCells, Constants.PlayerStartCell + Constants.StartGap + enemies.Sum(e => e.Size));

    public static BattleSetup Setup(SimDeck deck, EnemyDef enemy, int? maxStamina = null, int? startHp = null, int? startStamina = null) =>
        new BattleSetup(
            enemy, deck.Build(), FieldCellsFor(new[] { enemy }),
            PlayerMaxStamina: maxStamina ?? Constants.BaseMaxStamina,
            PlayerStartHp: startHp,
            PlayerStartStamina: startStamina);

    /// <summary>One battle to its end (or the turn limit). Returns the record and the final state.</summary>
    public static (BattleRecord Record, BattleState Final) Fight(SimDeck deck, BattleSetup setup, GreedyPlayer player, int seed, Action<string>? trace = null)
    {
        var rng = new SeededRng(seed);
        var state = TurnLoop.Start(setup, rng).State;

        var plays = new List<int>();
        int maxBlow = 0, movementPlays = 0, stancePlays = 0, noReachTurns = 0, handsWithoutMovement = 0, hands = 0;

        void Read(IReadOnlyList<BattleEvent> events)
        {
            foreach (var e in events)
            {
                if (e is DamageDealt blow && blow.Actor == Actor.Player && blow.Target == Actor.Enemy)
                {
                    maxBlow = Math.Max(maxBlow, blow.Raw);
                }
            }
        }

        while (state.Result == GameResult.Ongoing && state.Turn < TurnLimit)
        {
            var begin = TurnLoop.BeginPlayerTurn(state, rng);
            Read(begin.Events);
            state = begin.State;
            if (state.Result != GameResult.Ongoing) break;

            // The hand as dealt: §13 「移動の効果を持つ札が手札に来ない率」 and 「届く札が無くて何も当てられないターン」.
            hands++;
            if (!state.Hand.Any(c => AttributeRule.HasMovement(c.Def.Face))) handsWithoutMovement++;
            bool aimsAtAll = state.Hand.Any(c => EnemyAi.IsOpponentDirected(c.Def.Attributes, c.Def.Face, c.Def.Targets));
            bool reachesAtStart = state.Hand.Any(c => GreedyPlayer.AimsAndReaches(state, c.Def));
            bool struck = false;
            trace?.Invoke($"T{state.Turn} gap {Gap(state)} hp {state.Player.Hp} st {state.Player.Stamina} | enemy {Foe(state)} | omen {OmenText(state)} incoming {GreedyPlayer.Incoming(state)} | hand {string.Join(",", state.Hand.Select(c => c.Def.Id))}");

            int step = player.ChooseFreeStep(state);
            if (step != 0)
            {
                var free = TurnLoop.TakeFreeStep(state, step);
                Read(free.Events);
                state = free.State;
            }

            int played = 0;
            while (state.Result == GameResult.Ongoing)
            {
                var choice = player.Choose(state, played);
                if (choice == null) break;
                var refusal = TurnLoop.CanPlay(state, choice.InstanceId, choice.Target);
                if (refusal != PlayRefusal.None)
                {
                    throw new InvalidOperationException($"The greedy player chose an illegal play: {choice.InstanceId} at {choice.Target} ({refusal}).");
                }
                var def = state.Hand.First(c => c.InstanceId == choice.InstanceId).Def;
                if (AttributeRule.HasMovement(def.Face)) movementPlays++;
                if (Cards.IsStanceCard(def)) stancePlays++;
                if (EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) struck = true;
                trace?.Invoke($"  play {choice.InstanceId} at {choice.Target}, value {choice.Value:0.0}");
                var result = TurnLoop.PlayCard(state, choice.InstanceId, rng, choice.Target);
                Read(result.Events);
                state = result.State;
                played++;
            }
            plays.Add(played);
            trace?.Invoke($"  end: gap {Gap(state)} st {state.Player.Stamina} guard {state.Player.Guard} incoming {GreedyPlayer.Incoming(state)}");
            if (aimsAtAll && !reachesAtStart && !struck) noReachTurns++;

            if (state.Result != GameResult.Ongoing) break;
            var end = TurnLoop.EndTurn(state, rng);
            Read(end.Events);
            state = end.State;
        }

        var outcome = state.Result switch
        {
            GameResult.Won => Outcome.Won,
            GameResult.Lost => Outcome.Lost,
            _ => Outcome.Stalled,
        };
        var record = new BattleRecord(
            deck.Id, setup.Enemy.Id, setup.Enemy.Rank, outcome, state.Turn, plays, maxBlow,
            movementPlays, stancePlays, noReachTurns, handsWithoutMovement, hands, Math.Max(0, state.Player.Hp), seed);
        return (record, state);
    }

    private static int Gap(BattleState state) => state.Nearest < 0 ? -1 : state.GapTo(state.Nearest);

    private static string Foe(BattleState state) =>
        string.Join(" ", state.Living.Select(i => $"{state.Enemies[i].Body.Hp}hp/{state.Enemies[i].Body.Stamina}st/c{state.Enemies[i].Body.Cell}"));

    private static string OmenText(BattleState state) =>
        string.Join(" ", state.Living.Select(i => state.Enemies[i].Omen is { } omen ? omen.ActionId + ":" + omen.Label.ToText() : "-"));

    /// <summary>
    /// §13 「HP 持ち越しの 2 連戦」: the first battle, then — if it was won — the second with the HP and
    /// stamina it left (<see cref="Chain.Carry"/>; no rest between). Won when both are won; Stalled
    /// when either hit the turn limit; Lost otherwise.
    /// </summary>
    public static Outcome FightPair(SimDeck deck, EnemyDef first, EnemyDef second, GreedyPlayer player, int seed)
    {
        var (one, final) = Fight(deck, Setup(deck, first), player, seed);
        if (!one.Won) return one.Outcome;
        var (hp, stamina) = Chain.Carry(final);
        var (two, _) = Fight(deck, Setup(deck, second, startHp: hp, startStamina: stamina), player, unchecked(seed * 31 + 7));
        return two.Outcome;
    }

    /// <summary>A stable 32-bit FNV-1a hash, so a seed derived from a name is the same on every run and runtime.</summary>
    public static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    /// <summary>
    /// The seed of run <paramref name="run"/> of a battle kind against an enemy, from the base seed.
    /// The deck and the policy are left out on purpose: every deck meets the same seeds (common random
    /// numbers), which makes the differences between decks less noisy.
    /// </summary>
    public static int SeedFor(int baseSeed, string kind, string enemyId, int run) =>
        StableHash($"{baseSeed}|{kind}|{enemyId}|{run}");
}
