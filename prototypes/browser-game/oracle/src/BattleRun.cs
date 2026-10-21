// The greedy battle every battle-based suite replays: Runner.Fight's loop (BattleCore.Sim/Runner.cs),
// recording each TurnLoop call and its StepResult. The JS harness runs the same loop.
using BattleCore;
using BattleCore.Sim;

namespace Oracle;

/// <summary>One battle of the battles suite: a Decks deck against one or more roster enemies, one seed.</summary>
public sealed class BattleSpec
{
    public string DeckId = "";
    public SimDeck Deck;
    public List<string> EnemyIds = new();
    public int Seed;

    public string Id => $"{DeckId}|{string.Join("+", EnemyIds)}|{Seed}";

    public int FieldCells
    {
        get
        {
            var enemies = EnemyIds.Select(Enemies.ById).ToList();
            return Math.Max(BattleSetup.SliceFieldCells, Constants.PlayerStartCell + Constants.StartGap + enemies.Sum(e => e.Size));
        }
    }

    /// <summary>What the case's "input" holds: enough for the harness to rebuild the battle.</summary>
    public OrderedMap Input()
    {
        var m = new OrderedMap();
        m["Deck"] = DeckId;
        m["Enemies"] = EnemyIds;
        m["Seed"] = Seed;
        m["FieldCells"] = FieldCells;
        m["TurnLimit"] = BattleRun.TurnLimit;
        m["Positioning"] = "Neutral";
        m["MaxPlays"] = 0;
        return m;
    }

    /// <summary>Runner.Setup's battle (BattleCore.Sim/Runner.cs), with every argument spelled out.</summary>
    public BattleSetup Setup()
    {
        var enemies = EnemyIds.Select(Enemies.ById).ToList();
        return new BattleSetup(
            enemies[0], Deck.Build(), FieldCells,
            Constants.StartGap, Constants.PlayerStartCell, Constants.PlayerMaxHp, Constants.BaseMaxStamina,
            enemies.Count > 1 ? enemies.Skip(1).ToList() : null,
            null, null, 0, 0, 0);
    }
}

/// <summary>One recorded TurnLoop call.</summary>
public sealed class BattleStep
{
    public string Call = "";
    public string InstanceId;
    public int? Target;
    public int? Direction;
    public double? Value;
    public StepResult Result;

    public OrderedMap ToJson(bool withResult = true)
    {
        var m = new OrderedMap();
        m["Call"] = Call;
        if (InstanceId != null) m["InstanceId"] = InstanceId;
        if (Target.HasValue) m["Target"] = Target.Value;
        if (Direction.HasValue) m["Direction"] = Direction.Value;
        if (Value.HasValue) m["Value"] = Value.Value;
        if (withResult) m["Result"] = Result;
        return m;
    }

    /// <summary>The call alone, for suites that replay a battle's calls rather than its greedy choices.</summary>
    public OrderedMap CallOnly()
    {
        var m = new OrderedMap();
        m["Call"] = Call;
        if (InstanceId != null) m["InstanceId"] = InstanceId;
        if (Target.HasValue) m["Target"] = Target.Value;
        if (Direction.HasValue) m["Direction"] = Direction.Value;
        return m;
    }
}

public sealed class BattleTrace
{
    public BattleSpec Spec;
    public List<BattleStep> Steps = new();
    public BattleState Final;
    public Exception Error;

    /// <summary>Indices of the steps after which the player decides (the greedy is asked to choose).</summary>
    public List<int> DecisionSteps = new();
}

public static class BattleRun
{
    public const int TurnLimit = 60;

    public static BattleTrace Fight(BattleSpec spec)
    {
        var trace = new BattleTrace { Spec = spec };
        try
        {
            FightInner(spec, trace);
        }
        catch (Exception ex)
        {
            trace.Error = ex;
        }
        return trace;
    }

    private static void FightInner(BattleSpec spec, BattleTrace trace)
    {
        var player = new GreedyPlayer(Positioning.Neutral, 0);
        var rng = new SeededRng(spec.Seed);
        var start = TurnLoop.Start(spec.Setup(), rng);
        trace.Steps.Add(new BattleStep { Call = "Start", Result = start });
        var state = start.State;

        while (state.Result == GameResult.Ongoing && state.Turn < TurnLimit)
        {
            var begin = TurnLoop.BeginPlayerTurn(state, rng);
            trace.Steps.Add(new BattleStep { Call = "BeginPlayerTurn", Result = begin });
            state = begin.State;
            if (state.Result != GameResult.Ongoing) break;
            trace.DecisionSteps.Add(trace.Steps.Count - 1);

            int step = player.ChooseFreeStep(state);
            if (step != 0)
            {
                var free = TurnLoop.TakeFreeStep(state, step);
                trace.Steps.Add(new BattleStep { Call = "TakeFreeStep", Direction = step, Result = free });
                state = free.State;
                if (state.Result == GameResult.Ongoing && state.Phase == BattlePhase.PlayerAction) trace.DecisionSteps.Add(trace.Steps.Count - 1);
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
                var result = TurnLoop.PlayCard(state, choice.InstanceId, rng, choice.Target);
                trace.Steps.Add(new BattleStep { Call = "PlayCard", InstanceId = choice.InstanceId, Target = choice.Target, Value = choice.Value, Result = result });
                state = result.State;
                played++;
                if (state.Result == GameResult.Ongoing && state.Phase == BattlePhase.PlayerAction) trace.DecisionSteps.Add(trace.Steps.Count - 1);
            }

            if (state.Result != GameResult.Ongoing) break;
            var end = TurnLoop.EndTurn(state, rng);
            trace.Steps.Add(new BattleStep { Call = "EndTurn", Result = end });
            state = end.State;
        }
        trace.Final = state;
    }

    /// <summary>The decks the battles suite fights with: §13's six archetypes, then the demo's initial deck.</summary>
    public static List<SimDeck> DeckList()
    {
        var decks = Decks.Archetypes.ToList();
        decks.Add(Decks.Initial());
        return decks;
    }

    /// <summary>
    /// The enemy setups: every roster enemy alone (Enemies.All order), then each multi-enemy fight
    /// LayerEncounters holds (the pair polearm_warped + crossbow_hunter of layers 2〜3).
    /// </summary>
    public static List<List<string>> EnemySetups()
    {
        var setups = Enemies.All.Select(e => new List<string> { e.Id }).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in DungeonCore.LayerEncounters.All)
        {
            foreach (var pool in new[] { table.Normal, table.Elite, table.Boss })
            {
                foreach (var encounter in pool)
                {
                    foreach (var fight in encounter.Fights)
                    {
                        if (fight.EnemyIds.Count < 2) continue;
                        string key = string.Join("+", fight.EnemyIds);
                        if (seen.Add(key)) setups.Add(fight.EnemyIds.ToList());
                    }
                }
            }
        }
        return setups;
    }

    /// <summary>The battles suite, in order: deck-major, then enemy setup, then seed.</summary>
    public static List<BattleSpec> AllSpecs(int seeds)
    {
        var specs = new List<BattleSpec>();
        foreach (var deck in DeckList())
        {
            foreach (var enemies in EnemySetups())
            {
                for (int seed = 1; seed <= seeds; seed++)
                {
                    specs.Add(new BattleSpec { DeckId = deck.Id, Deck = deck, EnemyIds = enemies, Seed = seed });
                }
            }
        }
        return specs;
    }

    /// <summary>
    /// "The first 2 battles per enemy" (previews / texts): the first two decks of <see cref="DeckList"/>
    /// at seed 1, per enemy setup.
    /// </summary>
    public static List<BattleSpec> FirstTwoPerEnemy()
    {
        var decks = DeckList();
        var specs = new List<BattleSpec>();
        foreach (var enemies in EnemySetups())
        {
            for (int d = 0; d < 2; d++) specs.Add(new BattleSpec { DeckId = decks[d].Id, Deck = decks[d], EnemyIds = enemies, Seed = 1 });
        }
        return specs;
    }

    /// <summary>"The first battle per enemy (seed 1)" (scripts): the first deck at seed 1, per enemy setup.</summary>
    public static List<BattleSpec> FirstPerEnemy()
    {
        var deck = DeckList()[0];
        return EnemySetups().Select(e => new BattleSpec { DeckId = deck.Id, Deck = deck, EnemyIds = e, Seed = 1 }).ToList();
    }
}
