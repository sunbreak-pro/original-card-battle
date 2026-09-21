using System;

namespace DungeonCore
{
    /// <summary>
    /// The node kinds kept for the 80% build, from dungeon_exploration_v4.md §2.1.
    /// 階層間の休憩 is deliberately absent: it sits between layers, costs no 刻限 and
    /// is not a node (§4.1). Everything postponed in §2.2 is absent as well.
    /// </summary>
    public enum NodeKind
    {
        /// <summary>通常戦闘. One battle. The bulk of a layer.</summary>
        Battle,

        /// <summary>精鋭. Thicker reward, heavier cost. Always avoidable (§3.1).</summary>
        Elite,

        /// <summary>階層ボス. The exit. Exactly one per layer, on the last row.</summary>
        Boss,

        /// <summary>休息. Rest or train a card — one of the two, for one 刻限 (§3.2).</summary>
        Rest,

        /// <summary>情報収集. Raises one enemy's journal disclosure to level 2.</summary>
        Survey,

        /// <summary>イベント. Spring / heavy chest / strange meal (§2.1).</summary>
        Event,

        /// <summary>
        /// 死亡地点の痕跡. The previous life's estate. Present only when the run carries
        /// one; a layer holds at most one, and only one is collected per life (concept-v3 §8.3).
        /// </summary>
        Trace,

        /// <summary>
        /// 秘跡の彫り. The layer-two carving that names 竜神の秘跡 (world-v1 §5).
        /// Picking it up has no combat effect (world-v1 §5.2) — it only reveals the layer's name.
        /// </summary>
        Carving,
    }

    public static class NodeKindTokens
    {
        /// <summary>Stable lowercase token. Used by map fingerprints, so it must never drift.</summary>
        public static string ToToken(this NodeKind kind) => kind switch
        {
            NodeKind.Battle => "battle",
            NodeKind.Elite => "elite",
            NodeKind.Boss => "boss",
            NodeKind.Rest => "rest",
            NodeKind.Survey => "survey",
            NodeKind.Event => "event",
            NodeKind.Trace => "trace",
            NodeKind.Carving => "carving",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        /// <summary>
        /// Kinds a player may want to skip. A row is only ever allowed to be made
        /// entirely of these when the row has a single node, which never happens for
        /// them by construction (MapGenerator places them in rows of width >= 2).
        /// </summary>
        public static bool IsDetour(this NodeKind kind) =>
            kind == NodeKind.Elite || kind == NodeKind.Trace || kind == NodeKind.Carving;
    }
}
