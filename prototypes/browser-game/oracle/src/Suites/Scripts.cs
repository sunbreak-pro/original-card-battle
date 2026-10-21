// Suite 5 (scripts): the depiction script of the first battle per enemy setup (seed 1), written two ways:
//   writer:<battle>  CoreScriptWriter fed the recorded greedy battle's StepResults (Opening + Write per call)
//   source:<battle>  CoreBattleSource run unattended (suggestCards, the leftmost payable card), every
//                    event it hands the screen plus what the screen asks at each wait.
using BattleCore;
using Depiction;
using Depiction.Bridge;

namespace Oracle;

public static class ScriptsSuite
{
    public static readonly string[] Intern =
    {
        "CardDef", "EnemyDef", "CardFace", "CardDetail", "UnitFrame", "OmenFrame", "PileFrame", "FloorFrame",
        "CornerFrame", "PlayedFrame", "StatusChip", "TermMark", "CardValue", "TermDefinition", "PileCard", "DepictionFrame",
    };

    /// <summary>The unattended run stops after this many turns (CoreBattleSource's stopAfterTurns).</summary>
    public const int SourceTurns = 30;

    /// <summary>A guard against a source that never finishes.</summary>
    public const int SourceMaxMoves = 3000;

    public static int Run(string outDir)
    {
        using var file = new SuiteFile(outDir, "scripts", Intern);
        foreach (var spec in BattleRun.FirstPerEnemy())
        {
            var trace = BattleRun.Fight(spec);
            file.Add("writer:" + spec.Id, PreviewsSuite.ReplayInput(trace), () => Writer(spec, trace));
        }
        foreach (var spec in BattleRun.FirstPerEnemy())
        {
            var input = spec.Input();
            input["SuggestCards"] = true;
            input["StopAfterTurns"] = SourceTurns;
            file.Add("source:" + spec.Id, input, () => Source(spec));
        }
        return file.Count;
    }

    public static OrderedMap Writer(BattleSpec spec, BattleTrace trace)
    {
        var setup = spec.Setup();
        var writer = new CoreScriptWriter(setup.Enemy) { ChainIndex = 1, ChainTotal = 1, Disclosure = OmenDisclosure.Full };
        var opening = writer.Opening(trace.Steps[0].Result.State);
        var steps = new List<object>();
        for (int i = 1; i < trace.Steps.Count; i++)
        {
            var step = trace.Steps[i];
            var m = step.CallOnly();
            m["Events"] = Tables.Try(() => writer.Write(step.Result.Events, step.Result.State));
            steps.Add(m);
        }
        return new OrderedMap { ["Opening"] = opening, ["Steps"] = steps };
    }

    public static OrderedMap Source(BattleSpec spec)
    {
        var source = new CoreBattleSource(spec.Setup(), spec.Seed, suggestCards: true, stopAfterTurns: SourceTurns);
        var moves = new List<object>();
        var opening = source.Frame;
        int guard = 0;
        while (!source.Finished && guard++ < SourceMaxMoves)
        {
            var move = new OrderedMap();
            if (source.WaitingForPlayer)
            {
                string id = source.SuggestedCardId;
                move["Guide"] = source.GuideText;
                move["CanEndTurn"] = source.CanEndTurn;
                move["Suggested"] = id;
                var hand = new List<object>();
                foreach (var face in source.Frame.Hand)
                {
                    var verdict = source.Inspect(face.Id);
                    hand.Add(new OrderedMap
                    {
                        ["Id"] = face.Id,
                        ["Inspect"] = verdict,
                        ["PreviewFor"] = source.PreviewFor(face.Id),
                        ["RefusalText"] = source.RefusalText(face.Id, verdict),
                        ["WrongZoneText"] = source.RefusalText(face.Id, PlayVerdict.WrongZone),
                    });
                }
                move["Hand"] = hand;
                if (id.Length > 0)
                {
                    var face = DepictionText.Find(source.Frame.Hand, id);
                    var verdict = source.TryPlay(id, DepictionText.RequiredZone(face.Aim), out var played);
                    move["Action"] = "TryPlay";
                    move["Verdict"] = verdict;
                    move["Event"] = played;
                }
                else
                {
                    move["Action"] = "EndTurn";
                    move["Event"] = source.EndTurn();
                }
            }
            else
            {
                move["Action"] = "AdvanceAuto";
                move["Event"] = source.AdvanceAuto();
            }
            moves.Add(move);
        }
        return new OrderedMap
        {
            ["Opening"] = opening,
            ["Moves"] = moves,
            ["Finished"] = source.Finished,
            ["Result"] = source.State.Result,
            ["Turn"] = source.State.Turn,
            ["HistoryCount"] = source.History.Count,
        };
    }
}
