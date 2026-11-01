using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// The traces the lives have left and nobody has collected yet, and what the life now in the lair has
    /// done about them (concept-v3.md §8.3, Issue #106). Immutable, like the exploration state: every
    /// change returns a new ledger.
    ///
    /// - A trace stays until it is collected. A later death never overwrites it.
    /// - A life receives from one trace only: the first one it steps on. Stepping on a second one opens
    ///   nothing, and that trace stays for a later life.
    /// - A layer's map holds one trace at a time: the oldest one left on that layer.
    /// </summary>
    public sealed class TraceLedger
    {
        public static readonly TraceLedger Empty = new TraceLedger(Array.Empty<DeathTrace>(), 1, null, false);

        private TraceLedger(IReadOnlyList<DeathTrace> traces, int nextId, int? claimedTraceId, bool hasReceived)
        {
            Traces = traces;
            NextId = nextId;
            ClaimedTraceId = claimedTraceId;
            HasReceived = hasReceived;
        }

        /// <summary>The uncollected traces, oldest first.</summary>
        public IReadOnlyList<DeathTrace> Traces { get; }

        /// <summary>The id the next trace left gets.</summary>
        public int NextId { get; }

        /// <summary>The trace this life stepped on first, or null while it has stepped on none.</summary>
        public int? ClaimedTraceId { get; }

        /// <summary>This life has chosen from its trace. No other trace opens for it.</summary>
        public bool HasReceived { get; }

        /// <summary>The trace whose choice waits: claimed by this life and not chosen from yet. Null otherwise.</summary>
        public DeathTrace? Pending => !HasReceived && ClaimedTraceId.HasValue ? Find(ClaimedTraceId.Value) : null;

        /// <summary>This life may still receive from a trace: it has stepped on none yet.</summary>
        public bool MayReceive => !ClaimedTraceId.HasValue && !HasReceived;

        public DeathTrace? Find(int traceId) => Traces.FirstOrDefault(t => t.Id == traceId);

        /// <summary>The trace a layer's map shows: the oldest one left on that layer, or null.</summary>
        public DeathTrace? TraceIn(int layer) => Traces.FirstOrDefault(t => t.Place.Layer == layer);

        /// <summary>Takes a trace in as it is (a save, #109, or a test). Its id must be new to the ledger.</summary>
        public TraceLedger Add(DeathTrace trace)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (Find(trace.Id) != null) throw new ArgumentException($"trace {trace.Id} is already in the ledger", nameof(trace));
            var traces = Traces.Concat(new[] { trace }).ToList();
            return new TraceLedger(traces, Math.Max(NextId, trace.Id + 1), ClaimedTraceId, HasReceived);
        }

        /// <summary>A death is settled: the dead state leaves its trace (<see cref="Heritage.Leave"/>).</summary>
        public TraceLedger Leave(
            ExplorationState dead, int life, string? name,
            IEnumerable<LegacyCard>? cards, IEnumerable<LegacyPage>? pages) =>
            Add(Heritage.Leave(dead, NextId, life, name, cards, pages));

        /// <summary>A new life starts: the traces stay, and the new life has stepped on none of them.</summary>
        public TraceLedger BeginLife() => new TraceLedger(Traces, NextId, null, false);

        /// <summary>The map spec a layer is drawn from: its own, with a trace in it when one was left on the layer.</summary>
        public LayerMapSpec SpecFor(LayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return TraceIn(profile.Layer) != null ? Heritage.WithTrace(profile.MapSpec) : profile.MapSpec;
        }

        /// <summary>A layer's map for the seed, with the layer's trace on it when there is one.</summary>
        public LayerMap MapFor(LayerProfile profile, ulong seed) => MapGenerator.Generate(SpecFor(profile), seed);

        /// <summary>
        /// The life steps on the 死亡地点の痕跡 of <paramref name="layer"/>. The first trace a life steps on
        /// is the one it receives from; any later one changes nothing.
        /// </summary>
        public TraceLedger Visit(int layer)
        {
            if (!MayReceive) return this;
            DeathTrace? trace = TraceIn(layer);
            return trace == null ? this : new TraceLedger(Traces, NextId, trace.Id, false);
        }

        /// <summary>
        /// Chooses from the pending trace (<see cref="Heritage.Choose"/>). The trace is collected: it leaves
        /// the ledger with everything not chosen, and this life receives no other.
        /// </summary>
        public TraceLedger Collect(IReadOnlyCollection<string> keys, out Bequest bequest)
        {
            DeathTrace trace = Pending ?? throw new ExplorationRuleException("no trace waits on a choice");
            bequest = Heritage.Choose(trace, keys);
            var traces = Traces.Where(t => t.Id != trace.Id).ToList();
            return new TraceLedger(traces, NextId, trace.Id, true);
        }
    }
}
