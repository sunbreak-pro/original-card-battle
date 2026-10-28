// #106: the exploration session settles a death into a trace by either cause, puts the trace back on its
// layer's map in the next life, opens the first trace a life steps on and collects it on the choice.
// Runs under `dotnet test` (Exploration.Script.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Exploration;
using NUnit.Framework;
using static Exploration.Tests.ExplorationScreenTests;

namespace Exploration.Tests
{
    public class TraceSessionTests
    {
        [Test]
        public void FallingInBattle_LeavesOneTrace_AndTheNextLifeMeetsItOnThatLayer()
        {
            var session = new ExplorationSession(new RunStart { Seed = 4 });
            Assert.That(session.Act(ScreenAction.FallInBattle), Is.True);
            Assert.That(session.Traces.Traces.Count, Is.EqualTo(1));
            DeathTrace trace = session.Traces.Traces[0];
            Assert.That(trace.Place, Is.EqualTo(new DeathPlace(1, NodeKind.Battle)));
            Assert.That(trace.Cause, Is.EqualTo(DeathCause.Fallen));
            Assert.That(trace.Life, Is.EqualTo(1));
            Assert.That(trace.Name, Is.EqualTo(Heritage.StandInName(1)));
            Assert.That(trace.Candidates, Is.Empty, "no source: the trace offers nothing");

            Assert.That(session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(session.Traces.Traces.Count, Is.EqualTo(1), "a new life leaves no trace by starting");
            Assert.That(session.State.Map.CountOf(NodeKind.Trace), Is.EqualTo(1), "the trace is back on layer 1");
            Assert.That(session.PendingTrace, Is.Null);
        }

        [Test]
        public void TheGaugeKillingOnArrival_LeavesATrace_AndALaterDeathDoesNotOverwriteIt()
        {
            var session = new ExplorationSession(new RunStart { Seed = 4, MiasmaPercent = Miasma.DeathAt - 1 });
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.MiasmaDeath));
            Assert.That(session.Traces.Traces.Single().Cause, Is.EqualTo(DeathCause.Miasma));

            Assert.That(session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(session.Traces.Traces.Select(t => t.Life), Is.EqualTo(new[] { 1, 2 }), "the first trace stays");
            Assert.That(session.State.Map.CountOf(NodeKind.Trace), Is.EqualTo(1), "a layer shows one trace at a time");
        }

        [Test]
        public void TheSurvivorRoute_LeavesNoTrace()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Seed = 4 }));
            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(session.Traces.Traces, Is.Empty);
        }

        [Test]
        public void ADeathAsksTheSourceOnce_ForWhatTheLifeLeaves()
        {
            var source = new CountingSource();
            var session = new ExplorationSession(new RunStart { Seed = 4 }) { LegacySource = source };
            Assert.That(session.Act(ScreenAction.FallInBattle), Is.True);

            DeathTrace trace = session.Traces.Traces.Single();
            Assert.That(source.Asked, Is.EqualTo(1));
            Assert.That(trace.Name, Is.EqualTo("ソラ"));
            Assert.That(trace.Candidates.Select(c => c.Key), Is.EqualTo(new[] { "card:thrust", "page:p1" }),
                "a death on layer 1 keeps step 1 only");
        }

        [Test]
        public void SteppingOnTheTrace_OpensIt_AndTheChoiceCollectsIt()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Seed = 4, Traces = Ledger(1) }));
            Assert.That(session.Traces.MayReceive, Is.True);
            Assert.That(session.TakeLegacy(Array.Empty<string>()), Is.Null, "nothing waits before the trace is reached");

            WalkTo(session, NodeKind.Trace);
            Assert.That(session.PendingTrace?.Id, Is.EqualTo(1));
            Assert.That(session.MapIsLive, Is.True, "the choice waits beside the map, like a card offer");

            var four = new[] { "card:thrust", "card:kesa_cut", "page:p1", "page:p2" };
            Assert.That(session.TakeLegacy(four), Is.Null, "more than three is refused");
            Assert.That(session.PendingTrace?.Id, Is.EqualTo(1), "and the trace still waits");

            Bequest bequest = session.TakeLegacy(new[] { "page:p2", "card:thrust" });
            Assert.That(bequest, Is.Not.Null);
            Assert.That(bequest.Cards.Select(c => c.CardId), Is.EqualTo(new[] { "thrust" }));
            Assert.That(bequest.Pages.Select(p => p.PageId), Is.EqualTo(new[] { "p2" }));
            Assert.That(session.Traces.Traces, Is.Empty, "collected");
            Assert.That(session.PendingTrace, Is.Null);
        }

        [Test]
        public void ASecondTraceInTheSameLife_OpensNothing_AndStays()
        {
            var session = Explorable(new ExplorationSession(new RunStart { Seed = 4, Traces = Ledger(1, 2) }));
            WalkTo(session, NodeKind.Trace);
            Assert.That(session.TakeLegacy(new[] { "card:thrust" }), Is.Not.Null);

            WalkToLayer(session, 2);
            Assert.That(session.State.Map.CountOf(NodeKind.Trace), Is.EqualTo(1), "layer 2 still shows its trace");
            if (session.State.Map.Nodes.Any(n => n.Kind == NodeKind.Trace && !session.State.HasResolved(n.Id))) WalkTo(session, NodeKind.Trace);
            Assert.That(session.PendingTrace, Is.Null, "one trace per life");
            Assert.That(session.Traces.Traces.Select(t => t.Place.Layer), Is.EqualTo(new[] { 2 }), "the other trace stays for a later life");

            Assert.That(session.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(session.Act(ScreenAction.NewLife), Is.True);
            WalkToLayer(session, 2);
            if (session.State.Map.Nodes.Any(n => n.Kind == NodeKind.Trace && !session.State.HasResolved(n.Id))) WalkTo(session, NodeKind.Trace);
            Assert.That(session.PendingTrace?.Place.Layer, Is.EqualTo(2), "the next life may take it");
        }

        // ---- helpers ----

        /// <summary>A ledger with one trace on each of the layers given, ids from 1, each offering two cards and two pages.</summary>
        internal static TraceLedger Ledger(params int[] layers)
        {
            TraceLedger ledger = TraceLedger.Empty;
            for (int i = 0; i < layers.Length; i++)
            {
                var candidates = Heritage.Candidates(layers[i],
                    new[] { new LegacyCard("thrust", 1, "鋭い突き"), new LegacyCard("kesa_cut", 1, "深袈裟") },
                    new[] { new LegacyPage("p1", "頁 1"), new LegacyPage("p2", "頁 2") });
                ledger = ledger.Add(new DeathTrace(i + 1, i + 1, Heritage.StandInName(i + 1),
                    new DeathPlace(layers[i], NodeKind.Battle), DeathCause.Fallen, candidates));
            }
            return ledger;
        }

        /// <summary>Walks the shortest way to the nearest unresolved node of this kind, winning every battle on the way.</summary>
        internal static void WalkTo(ExplorationSession session, NodeKind kind)
        {
            ExplorationState state = session.State;
            LayerMap map = state.Map;
            var from = new Dictionary<int, int> { [state.CurrentNodeId] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(state.CurrentNodeId);
            int target = -1;
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                if (map.Node(node).Kind == kind && !state.HasResolved(node))
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
            Assert.That(target, Is.Not.EqualTo(-1), $"a {kind} node is reachable");
            var path = new List<int>();
            for (int node = target; node != state.CurrentNodeId; node = from[node]) path.Add(node);
            path.Reverse();
            foreach (int node in path) Step(session, node);
        }

        private sealed class CountingSource : ILegacySource
        {
            public int Asked { get; private set; }

            public string Name => "ソラ";

            public IReadOnlyList<LegacyCard> Cards
            {
                get
                {
                    Asked++;
                    return new[] { new LegacyCard("thrust", 1, "鋭い突き"), new LegacyCard("overhead", 2, "雷落とし") };
                }
            }

            public IReadOnlyList<LegacyPage> Pages => new[] { new LegacyPage("p1", "頁 1") };
        }
    }
}
