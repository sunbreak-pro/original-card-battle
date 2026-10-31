// Suite 2 (battles): the greedy player's battles, every call with its full StepResult.
using BattleCore;

namespace Oracle;

public static class BattlesSuite
{
    public static readonly string[] Intern = { "CardDef", "EnemyDef", "CardInstance", "EnemyActionDef", "Omen", "StatusSet", "Trait", "StanceDef", "BattleHistory", "PlayerTurnEnd", "MasteryPlay", "CombatantState", "EnemyUnit", "StanceEntry", "CarriedTools", "BattleEvent" };

    public static int Run(string outDir, int seeds)
    {
        using var file = new SuiteFile(outDir, "battles", Intern);
        foreach (var spec in BattleRun.AllSpecs(seeds))
        {
            var trace = BattleRun.Fight(spec);
            file.Add(spec.Id, spec.Input(), () => Output(trace));
        }
        return file.Count;
    }

    public static OrderedMap Output(BattleTrace trace)
    {
        var m = new OrderedMap();
        m["Steps"] = trace.Steps.Select(s => s.ToJson()).ToList();
        if (trace.Error != null) m["Error"] = trace.Error;
        if (trace.Final != null)
        {
            m["Result"] = trace.Final.Result;
            m["Turns"] = trace.Final.Turn;
        }
        return m;
    }
}
