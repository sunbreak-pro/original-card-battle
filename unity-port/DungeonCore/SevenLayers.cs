using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// One layer's numbers. The values come from
    /// `.claude/docs/danjeon_document/seven_layers_v4.md` §2 and §4 (Issue #130).
    /// </summary>
    public sealed class LayerProfile
    {
        public LayerProfile(int layer, string name, string? trueName, int density, int timeLimit, LayerMapSpec mapSpec)
        {
            if (layer < 1) throw new ArgumentOutOfRangeException(nameof(layer));
            if (density < 0) throw new ArgumentOutOfRangeException(nameof(density));
            if (timeLimit < 1) throw new ArgumentOutOfRangeException(nameof(timeLimit));
            if (mapSpec == null) throw new ArgumentNullException(nameof(mapSpec));
            if (mapSpec.Layer != layer)
                throw new ArgumentException($"layer {layer} was handed the spec of layer {mapSpec.Layer}", nameof(mapSpec));
            if (timeLimit >= mapSpec.NodeCount)
                throw new ArgumentException(
                    $"layer {layer} has {timeLimit} 刻限 for {mapSpec.NodeCount} nodes; a layer must always keep " +
                    "at least one node out of reach (seven_layers_v4.md §2.2)", nameof(timeLimit));

            Layer = layer;
            Name = name;
            TrueName = trueName;
            Density = density;
            TimeLimit = timeLimit;
            MapSpec = mapSpec;
        }

        public int Layer { get; }

        /// <summary>What the layer is called before anything is learned about it.</summary>
        public string Name { get; }

        /// <summary>The name behind the blanks, if the layer hides one (world-v1.md §5.1).</summary>
        public string? TrueName { get; }

        /// <summary>瘴気 added per 刻限 spent in this layer.</summary>
        public int Density { get; }

        /// <summary>刻限 for this layer. Never carried over to the next (dungeon_exploration_v4.md §3.1).</summary>
        public int TimeLimit { get; }

        public LayerMapSpec MapSpec { get; }

        /// <summary>刻限 to reach the boss without detouring.</summary>
        public int ShortestPathCost => MapSpec.ShortestPathCost;

        public bool IsLast => Layer == SevenLayers.Count;

        /// <summary>
        /// The name to print. A layer with a hidden name shows the blanks until the clue is
        /// picked up, and its true name afterwards (world-v1.md §5.1).
        /// </summary>
        public string DisplayName(bool clueTaken) =>
            clueTaken && TrueName != null ? TrueName : Name;

        /// <summary>This layer's map for the given seed.</summary>
        public LayerMap Map(ulong seed) => MapGenerator.Generate(MapSpec, seed);
    }

    /// <summary>
    /// The seven layers of the dragon's lair, with the density and 刻限 redrawn from five
    /// floors to seven (Issue #130). Names and order come from `vision/world-v1.md` §4.
    ///
    /// Running straight down costs 89% of the gauge, which leaves max stamina 6 at 歪みの根 —
    /// the condition `battle_core_v4.md` §13 already sets a win-rate target for.
    /// </summary>
    public static class SevenLayers
    {
        public const int Count = 7;

        /// <summary>The layer that hides its name until the carving is found.</summary>
        public const int SacramentLayer = 2;

        /// <summary>The layer 歪みの根 and, once unlocked, 根の裂け目 sit on.</summary>
        public const int RootLayer = 7;

        private static readonly LayerProfile[] Profiles = BuildProfiles();

        public static IReadOnlyList<LayerProfile> All => Profiles;

        public static LayerProfile Of(int layer)
        {
            if (layer < 1 || layer > Count)
                throw new ArgumentOutOfRangeException(nameof(layer), layer, $"the lair has {Count} layers");
            return Profiles[layer - 1];
        }

        /// <summary>瘴気 a run picks up walking straight down every layer, with no relief.</summary>
        public static int BareMinimumMiasma(int densityRelief = 0) =>
            Profiles.Sum(p => Miasma.EffectiveDensity(p.Density, densityRelief) * p.ShortestPathCost);

        /// <summary>瘴気 a run picks up spending every 刻限 of every layer.</summary>
        public static int FullSpendMiasma(int densityRelief = 0) =>
            Profiles.Sum(p => Miasma.EffectiveDensity(p.Density, densityRelief) * p.TimeLimit);

        private static LayerProfile[] BuildProfiles() => new[]
        {
            new LayerProfile(1, "燦光の樹海", null, density: 1, timeLimit: 10,
                Wide(1, battles: 3, events: 2, rests: 2, surveys: 1)),

            // Layer two hides its name. One battle is traded for the carving that reveals it.
            new LayerProfile(2, "■■の秘跡", "竜神の秘跡", density: 1, timeLimit: 10,
                Wide(2, battles: 3, events: 2, rests: 2, surveys: 1).With(NodeKind.Carving, 1)),

            new LayerProfile(3, "大黒蛇の住処", null, density: 2, timeLimit: 10,
                Wide(3, battles: 3, events: 2, rests: 2, surveys: 1)),

            // From here down the layers narrow: 刻限 falls as density rises, so the deep
            // layers are meant to be run through rather than combed (seven_layers_v4.md §2.2).
            new LayerProfile(4, "輝晶の間", null, density: 3, timeLimit: 7,
                Narrow(4, battles: 2, rests: 2, surveys: 1)),

            new LayerProfile(5, "血染めの死海", null, density: 4, timeLimit: 7,
                Narrow(5, battles: 2, rests: 2, surveys: 1)),

            new LayerProfile(6, "獄雷峡", null, density: 5, timeLimit: 6,
                new LayerMapSpec(6, new[] { 1, 3, 2, 1 }, new Dictionary<NodeKind, int>
                {
                    [NodeKind.Battle] = 2,
                    [NodeKind.Elite] = 1,
                    [NodeKind.Rest] = 1,
                    [NodeKind.Survey] = 1,
                })),

            // The void. Density jumps a step because the layer is made of 瘴気 (world-v1.md §4).
            new LayerProfile(7, "果てなき世界", null, density: 7, timeLimit: 4,
                new LayerMapSpec(7, new[] { 1, 3, 1 }, new Dictionary<NodeKind, int>
                {
                    [NodeKind.Battle] = 1,
                    [NodeKind.Elite] = 1,
                    [NodeKind.Rest] = 1,
                })),
        };

        /// <summary>Eleven nodes over five rows — the full-sized layer of dungeon_exploration_v4 §3.1.</summary>
        private static LayerMapSpec Wide(int layer, int battles, int events, int rests, int surveys) =>
            new LayerMapSpec(layer, new[] { 1, 3, 3, 3, 1 }, new Dictionary<NodeKind, int>
            {
                [NodeKind.Battle] = battles,
                [NodeKind.Elite] = 1,
                [NodeKind.Rest] = rests,
                [NodeKind.Survey] = surveys,
                [NodeKind.Event] = events,
            });

        /// <summary>Eight nodes over four rows. Events drop out: there is no 刻限 left for them.</summary>
        private static LayerMapSpec Narrow(int layer, int battles, int rests, int surveys) =>
            new LayerMapSpec(layer, new[] { 1, 3, 3, 1 }, new Dictionary<NodeKind, int>
            {
                [NodeKind.Battle] = battles,
                [NodeKind.Elite] = 1,
                [NodeKind.Rest] = rests,
                [NodeKind.Survey] = surveys,
            });
    }
}
