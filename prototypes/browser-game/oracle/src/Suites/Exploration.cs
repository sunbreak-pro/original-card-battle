// Suite 7 (exploration): Expedition runs through layers 1〜3 with stubbed battle outcomes. Each move
// records the ExplorationScreenBuilder screen (its JournalBook included) and the expedition's state;
// battles record the hand-in, each launch and the BattleReturn (BattleRewards / UniqueCards).
using BattleCore;
using DungeonCore;
using Exploration;
using Exploration.Bridge;

namespace Oracle;

public static class ExplorationSuite
{
    public const int Seeds = 3;
    public const int LastLayer = 3;
    public const int MaxMoves = 400;
    public const int StubHpLoss = 10;

    public static int Run(string outDir)
    {
        using var file = new SuiteFile(outDir, "exploration", "LayerMap", "LayerProfile", "CardDef", "EnemyDef", "CardInstance", "MasteryProfile");
        for (int seed = 1; seed <= Seeds; seed++)
        {
            int s = seed;
            file.Add($"expedition:s{s}", Input(s), () => Expedition(s));
        }
        return file.Count;
    }

    /// <summary>Seed 1: a plain start. Seed 2: battle tools (息継ぎの笛, 竜鱗の欠片) and a talent. Seed 3: the trial loadout.</summary>
    public static OrderedMap Input(int seed) => new OrderedMap
    {
        ["Seed"] = (ulong)seed,
        ["Loadout"] = seed == 3 ? "trial" : "empty",
        ["BattleToolIds"] = seed == 2 ? new List<string> { "ikitsugi_no_fue", "ryurin_no_kakera" } : null,
        ["Talent"] = seed == 2 ? "thrust" : null,
        ["LastLayer"] = LastLayer,
        ["StubHpLoss"] = StubHpLoss,
        ["MaxMoves"] = MaxMoves,
    };

    /// <summary>
    /// The stub for a fight: the battle the launch starts, taken as won at once with the player's HP
    /// 10 lower (at least 1) — `state with { Result = Won, Phase = Finished, Player = Player with { Hp } }`.
    /// </summary>
    public static BattleState Stub(BattleState started) =>
        started with
        {
            Result = GameResult.Won,
            Phase = BattlePhase.Finished,
            Player = started.Player with { Hp = Math.Max(1, started.Player.Hp - StubHpLoss) },
        };

    public static object Expedition(int seed)
    {
        var start = new RunStart { Seed = (ulong)seed, Loadout = seed == 3 ? RunStart.TrialLoadout() : RunLoadout.Empty };
        var exp = new Expedition(
            start,
            deck: null,
            battleToolIds: seed == 2 ? new[] { "ikitsugi_no_fue", "ryurin_no_kakera" } : null,
            talent: seed == 2 ? "thrust" : null);
        var moves = new List<object>();

        void Record(string op, object args, object result)
        {
            var m = new OrderedMap { ["Op"] = op };
            if (args != null) m["Args"] = args;
            m["Result"] = result;
            m["Screen"] = Tables.Try(() => exp.Session.Screen());
            m["Expedition"] = Summary(exp);
            moves.Add(m);
        }

        Record("Begin", null, new OrderedMap { ["Tools"] = exp.Tools, ["InitialKinds"] = Exploration.Bridge.Expedition.InitialKinds });
        Record("Toggle", "Journal", exp.Session.Toggle(Overlay.Journal));
        Record("Toggle", "Journal", exp.Session.Toggle(Overlay.Journal));
        Record("Escape", null, exp.Session.Escape());
        Record("Escape", null, exp.Session.Escape());

        bool trained = false, usedConsumable = false;
        int guard = 0;
        while (guard++ < MaxMoves)
        {
            var session = exp.Session;
            var state = session.State;
            if (state.IsOver || state.Profile.Layer > LastLayer) break;

            if (session.HasPendingBattle)
            {
                var battle = exp.StartEncounter();
                Record("StartEncounter", null, battle?.HandIn);
                if (battle == null) break;
                while (!battle.Over)
                {
                    var launch = battle.NextLaunch();
                    var source = battle.StartNext();
                    var finished = Stub(source.State);
                    battle.Finish(finished, source.History);
                    Record("Fight", null, new OrderedMap
                    {
                        ["Launch"] = launch,
                        ["StartState"] = source.State,
                        ["Opening"] = source.Frame,
                        ["HpAfter"] = battle.HpAfter,
                        ["StaminaAfter"] = battle.StaminaAfter,
                        ["Index"] = battle.Index,
                        ["Count"] = battle.Count,
                        ["Won"] = battle.Won,
                    });
                }
                var ret = Tables.Try(() => exp.Return());
                Record("Return", null, ret);
                if (exp.Offer.Count > 0)
                {
                    string pick = exp.Offer[0];
                    Record("Learn", pick, exp.Learn(pick));
                }
                continue;
            }
            if (session.HasPendingStep)
            {
                Record("Act", ScreenAction.ConfirmStep, session.Act(ScreenAction.ConfirmStep));
                continue;
            }
            if (session.ConfirmingDescend)
            {
                Record("Act", ScreenAction.ConfirmDescend, session.Act(ScreenAction.ConfirmDescend));
                continue;
            }
            if (state.Phase == RunPhase.LayerCleared || state.Phase == RunPhase.PushedOut)
            {
                Record("Act", ScreenAction.GoToInterlude, session.Act(ScreenAction.GoToInterlude));
                continue;
            }
            if (state.Phase == RunPhase.Interlude)
            {
                if (state.Profile.Layer >= LastLayer) break;
                Record("Act", ScreenAction.Descend, session.Act(ScreenAction.Descend));
                continue;
            }
            if (state.TrainingMarks > 0)
            {
                string card = exp.Deck[0].Def.Id;
                Record("Train", card, exp.Train(card));
                continue;
            }
            if (!usedConsumable && session.CanUseConsumables && state.Loadout.Consumables.Count > 0 && state.MiasmaPercent > 0)
            {
                usedConsumable = true;
                Record("UseConsumable", 0, session.UseConsumable(0));
                continue;
            }
            var open = state.ReachableUnresolved().Select(state.Map.Node).ToList();
            if (open.Count == 0)
            {
                Record("Act", ScreenAction.AskEndLife, session.Act(ScreenAction.AskEndLife));
                Record("Act", ScreenAction.ConfirmEndLife, session.Act(ScreenAction.ConfirmEndLife));
                continue;
            }
            var pickNode = open.OrderBy(n => n.Column).ThenBy(n => n.Id).First();
            var choice = RestChoice.Rest;
            if (pickNode.Kind == NodeKind.Rest)
            {
                choice = trained ? RestChoice.Rest : RestChoice.Train;
                trained = true;
            }
            Record("Step", new OrderedMap { ["Node"] = pickNode.Id, ["Choice"] = choice }, session.Step(pickNode.Id, choice));
        }

        return new OrderedMap { ["Moves"] = moves };
    }

    public static OrderedMap Summary(Expedition exp)
    {
        var s = exp.Session;
        var disclosure = new OrderedMap();
        foreach (var e in Enemies.All) disclosure.Add(e.Id, exp.DisclosureOf(e.Id));
        return new OrderedMap
        {
            ["Life"] = s.Life,
            ["RunSeed"] = s.RunSeed,
            ["State"] = s.State,
            ["CarvingTaken"] = s.CarvingTaken,
            ["Surveyed"] = s.Surveyed,
            ["PendingBattleNode"] = s.PendingBattleNode,
            ["PendingEncounter"] = s.PendingEncounter,
            ["PendingStepNode"] = s.PendingStepNode,
            ["ConfirmingEndLife"] = s.ConfirmingEndLife,
            ["ConfirmingDescend"] = s.ConfirmingDescend,
            ["InterludeHealed"] = s.InterludeHealed,
            ["OpenOverlay"] = s.OpenOverlay,
            ["MapIsLive"] = s.MapIsLive,
            ["CanEndLife"] = s.CanEndLife,
            ["Deck"] = exp.Deck.Select(c => c.InstanceId).ToList(),
            ["Owned"] = exp.Owned.ToList(),
            ["Offer"] = exp.Offer,
            ["Consumables"] = exp.Consumables,
            ["Pages"] = exp.Pages,
            ["Mastery"] = exp.Mastery,
            ["Disclosure"] = disclosure,
        };
    }
}
