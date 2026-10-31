// Suite 3 (previews): at each player decision point of the first 2 battles per enemy setup, every
// read-only question the screen and the greedy player ask the core.
using BattleCore;
using BattleCore.Sim;
using Depiction.Bridge;

namespace Oracle;

public static class PreviewsSuite
{
    public static int Run(string outDir)
    {
        using var file = new SuiteFile(outDir, "previews", BattlesSuite.Intern);
        foreach (var spec in BattleRun.FirstTwoPerEnemy())
        {
            var trace = BattleRun.Fight(spec);
            file.Add(spec.Id, ReplayInput(trace), () => trace.DecisionSteps.Select(i => DecisionPoint(i, trace.Steps[i].Result.State)).ToList());
        }
        return file.Count;
    }

    /// <summary>The battle's input plus its recorded calls, so the harness replays the same states without the greedy player.</summary>
    public static OrderedMap ReplayInput(BattleTrace trace)
    {
        var m = trace.Spec.Input();
        m["Calls"] = trace.Steps.Select(s => s.CallOnly()).ToList();
        if (trace.Error != null) m["Error"] = trace.Error;
        return m;
    }

    public static OrderedMap DecisionPoint(int step, BattleState state)
    {
        var cards = new List<object>();
        foreach (var card in state.Hand)
        {
            for (int target = 0; target < state.Enemies.Count; target++)
            {
                int t = target;
                cards.Add(new OrderedMap
                {
                    ["InstanceId"] = card.InstanceId,
                    ["Target"] = t,
                    ["CanPlay"] = Tables.Try(() => TurnLoop.CanPlay(state, card.InstanceId, t)),
                    ["CostNow"] = Tables.Try(() => TurnLoop.CostNow(state, card.Def, t)),
                    ["Preview"] = Tables.Try(() => TurnLoop.Preview(state, card.InstanceId, t)),
                });
            }
        }
        var omens = new List<object>();
        for (int unit = 0; unit < state.Enemies.Count; unit++)
        {
            int u = unit;
            omens.Add(new OrderedMap
            {
                ["Unit"] = u,
                ["PreviewOmen"] = Tables.Try(() => TurnLoop.PreviewOmen(state, u)),
                ["OmenBoundaryCells"] = Tables.Try(() => TurnLoop.OmenBoundaryCells(state, u)),
                ["OmenMoveCell"] = Tables.Try(() => TurnLoop.OmenMoveCell(state, u)),
            });
        }
        var texts = new List<object>();
        foreach (var card in state.Hand)
        {
            texts.Add(new OrderedMap
            {
                ["InstanceId"] = card.InstanceId,
                ["ReachesNobody"] = Tables.Try(() => CoreText.ReachesNobody(state, card.InstanceId)),
                ["Lamps"] = Tables.Try(() =>
                {
                    bool lit = CardFaceText.Lamps(state, card.InstanceId, out var perEnemy);
                    return new OrderedMap { ["Result"] = lit, ["PerEnemy"] = perEnemy };
                }),
                ["HpLoss"] = Tables.Try(() => CardFaceText.HpLoss(state, card.InstanceId)),
            });
        }
        return new OrderedMap
        {
            ["Step"] = step,
            ["Cards"] = cards,
            ["Omens"] = omens,
            ["PreviewRecovery"] = Tables.Try(() => TurnLoop.PreviewRecovery(state)),
            ["CanTakeFreeStep"] = TurnLoop.CanTakeFreeStep(state),
            ["GreedyValue"] = GreedyPlayer.Value(state),
            ["GreedyIncoming"] = GreedyPlayer.Incoming(state),
            ["Texts"] = texts,
        };
    }
}
