// The browser trial's test helpers for a trial Expedition (test/ext/explore-results.test.mjs:28-103,
// test/ext/world.test.mjs:37-106, test/exploration/_battle.mjs): build a trial life the way the game does, win the
// waiting battle without playing it out, clear the trial choices, walk to a node. Runs under `dotnet test`
// (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonContent;
using DungeonCore;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    internal static class TrialTestKit
    {
        public const string Mask = "bosho_no_men";
        public const string Sand = "kokugen_no_suna";
        public const string Book = "utsushi_no_tobari";
        public const string Wick = "toshin_no_kakera";
        public const string Flute = "ikitsugi_no_fue";

        /// <summary>
        /// A trial expedition from a departure's choice, the way the game builds one (js: explore-results.test.mjs:28).
        /// </summary>
        public static Expedition Trial(
            ulong seed,
            IReadOnlyList<string> tools = null,
            IReadOnlyList<string> consumables = null,
            IReadOnlyList<CardInstance> deck = null,
            DeckLimits limits = null,
            IReadOnlyDictionary<string, int> journal = null,
            Func<LayerProfile, ulong, LayerMap> mapFor = null,
            bool? carvingKnown = null,
            int startPages = 0)
        {
            tools = tools ?? Array.Empty<string>();
            int slots = Math.Max(3, tools.Count);
            RunLoadout loadout = Loadouts.ToRunLoadout(Loadout.Of(tools, consumables ?? Array.Empty<string>(), slots), slots);
            var start = new RunStart { Seed = seed, Loadout = loadout, MapFor = mapFor };
            var options = new ExpeditionOptions { Trial = true, Limits = limits, CarvingKnown = carvingKnown, StartPages = startPages };
            return new Expedition(start, deck, null, null, null, journal, options);
        }

        /// <summary>Wins the waiting battle with this HP and stamina left (no fight played: the core's end is reported).</summary>
        public static BattleReturn Win(Expedition ex, int hp = 50, int stamina = 5)
        {
            EncounterBattle battle = ex.StartEncounter();
            Assert.That(battle, Is.Not.Null, "a battle waits");
            EndEveryFight(battle, GameResult.Won, hp, stamina);
            return ex.Return();
        }

        /// <summary>Clears whatever trial choice waits: swaps leave, events decline, survey picks the first.</summary>
        public static void ClearChoices(ExplorationSession session)
        {
            while (session.HasPendingFound) session.Act(ScreenAction.LeaveFound);
            if (session.HasPendingEvent) session.Act(ScreenAction.DeclineEvent);
            if (session.HasPendingSurvey) session.Act(ScreenAction.ChooseSurvey, session.SurveyChoices[0]);
        }

        /// <summary>The nodes from where the player stands to the nearest unresolved node matching <paramref name="want"/>, that node last.</summary>
        public static IReadOnlyList<int> PathTo(ExplorationState state, Func<MapNode, bool> want)
        {
            LayerMap map = state.Map;
            var from = new Dictionary<int, int> { [state.CurrentNodeId] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(state.CurrentNodeId);
            int target = -1;
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                if (want(map.Node(node)) && !state.HasResolved(node))
                {
                    target = node;
                    break;
                }
                foreach (int next in map.Neighbours(node))
                {
                    if (from.ContainsKey(next)) continue;
                    from[next] = node;
                    queue.Enqueue(next);
                }
            }
            Assert.That(target, Is.Not.EqualTo(-1), "a matching node is reachable");
            var path = new List<int>();
            for (int node = target; node != state.CurrentNodeId; node = from[node]) path.Add(node);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Walks to the nearest matching node, winning (and closing) every result on the way (js:
        /// explore-results.test.mjs:67). The last step is taken with <paramref name="choice"/>; nothing is cleared there.
        /// </summary>
        public static int WalkTo(Expedition ex, Func<MapNode, bool> want, RestChoice choice = RestChoice.Rest)
        {
            ExplorationSession s = ex.Session;
            if (s.HasPendingBattle)
            {
                Win(ex);
                ex.CloseResult();
            }
            ClearChoices(s);
            ex.CloseResult();
            IReadOnlyList<int> path = PathTo(s.State, want);
            foreach (int node in path)
            {
                bool last = node == path[path.Count - 1];
                Assert.That(s.Step(node, last ? choice : RestChoice.Rest), Is.True, $"step to {node}");
                if (s.HasPendingStep) s.Act(ScreenAction.ConfirmStep);
                if (last) continue;
                if (s.HasPendingBattle)
                {
                    Win(ex);
                    ex.CloseResult();
                }
                ClearChoices(s);
                ex.CloseResult();
            }
            return path[path.Count - 1];
        }

        /// <summary>Walks down to <paramref name="layer"/>, beating each layer's boss (js: explore-results.test.mjs:84).</summary>
        public static void WalkToLayer(Expedition ex, int layer)
        {
            ExplorationSession s = ex.Session;
            while (s.State.Profile.Layer < layer)
            {
                WalkTo(ex, n => n.Kind == NodeKind.Boss);
                Win(ex);
                ex.CloseResult();
                ClearChoices(s);
                Assert.That(s.Act(ScreenAction.GoToInterlude), Is.True);
                Assert.That(s.Act(ScreenAction.Descend), Is.True);
                if (s.HasPendingBattle)
                {
                    Win(ex);
                    ex.CloseResult();
                }
                ClearChoices(s);
            }
        }

        /// <summary>A prototype deck whose last cards are replaced with these kinds, so they are owned from the start (js: explore-results.test.mjs:99).</summary>
        public static List<CardInstance> DeckWith(IReadOnlyList<string> ids)
        {
            var deck = PrototypeDeck.Build().ToList();
            for (int i = 0; i < ids.Count; i++) deck[deck.Count - 1 - i] = new CardInstance($"given-{i}", CardCatalog.ById(ids[i]));
            return deck;
        }

        /// <summary>The prototype deck grown with two copies of each catalogue card (in canon order) up to <paramref name="count"/> (js: world.test.mjs:289).</summary>
        public static List<CardInstance> DeckOf(int count)
        {
            var deck = PrototypeDeck.Build().ToList();
            int i = 0;
            foreach (CardDef def in CardCatalog.All)
            {
                while (deck.Count < count && deck.Count(c => c.Def.Id == def.Id) < 2) deck.Add(new CardInstance($"extra-{i++}", def));
            }
            return deck;
        }

        /// <summary>The source's state as a battle that ended this way: the end the core would report, without playing it out.</summary>
        public static BattleState Ended(Depiction.Bridge.CoreBattleSource source, GameResult result, int hp, int stamina)
        {
            BattleState state = source.State;
            return state with { Result = result, Player = state.Player with { Hp = hp, Stamina = stamina } };
        }

        /// <summary>Ends every fight of the encounter this way. −1 keeps the HP and stamina the fight started with (js: _battle.mjs:33).</summary>
        public static void EndEveryFight(EncounterBattle battle, GameResult result, int hp = -1, int stamina = -1)
        {
            while (!battle.Over)
            {
                Depiction.Bridge.CoreBattleSource source = battle.StartNext();
                CombatantState player = source.State.Player;
                var events = new List<BattleEvent>(source.History);
                battle.Finish(Ended(source, result, hp < 0 ? player.Hp : hp, stamina < 0 ? player.Stamina : stamina), events);
            }
        }

        /// <summary>The first seed (1〜200) whose layer-1 map holds an event of this kind, and that node (js: world.test.mjs:98).</summary>
        public static (ulong Seed, int Node) SeedWithEvent(EventKind kind, Func<ExplorationSession, MapNode, bool> extra = null)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                ExplorationSession s = Trial(seed).Session;
                MapNode node = s.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event && s.EventAt(n.Id) == kind);
                if (node != null && (extra == null || extra(s, node))) return (seed, node.Id);
            }
            Assert.Fail($"no seed has a {kind}");
            return (0, -1);
        }
    }
}
