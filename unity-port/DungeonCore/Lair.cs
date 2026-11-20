using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// One layer of one region (seven_layers_v4.md §3.2 and §7, Issue #888). The calendar replaced the per-layer 刻限
    /// and 瘴気 density, so a layer is its place, its names, its map and the backdrop that draws it.
    /// </summary>
    public sealed class LayerProfile
    {
        public LayerProfile(int layer, Region region, string name, string? trueName, int backdropLayer, LayerMapSpec mapSpec)
        {
            if (layer < 1 || layer > Lair.LayerCount)
                throw new ArgumentOutOfRangeException(nameof(layer), layer, $"the lair has {Lair.LayerCount} layers");
            if (layer < Lair.FirstRegionLayer && region != Region.Common)
                throw new ArgumentException($"layer {layer} is shared by every region, not {region}'s", nameof(region));
            if (layer >= Lair.FirstRegionLayer && !region.HasKing())
                throw new ArgumentException($"layer {layer} belongs to one of the three regions, not {region}", nameof(region));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("a layer needs a name", nameof(name));
            if (backdropLayer < 1 || backdropLayer > Lair.BackdropCount)
                throw new ArgumentOutOfRangeException(nameof(backdropLayer), backdropLayer, $"there are {Lair.BackdropCount} backdrops");
            if (mapSpec == null) throw new ArgumentNullException(nameof(mapSpec));
            if (mapSpec.Layer != layer)
                throw new ArgumentException($"layer {layer} was handed the spec of layer {mapSpec.Layer}", nameof(mapSpec));

            Layer = layer;
            Region = region;
            Name = name;
            TrueName = trueName;
            BackdropLayer = backdropLayer;
            MapSpec = mapSpec;
        }

        /// <summary>Depth, 1..7. The three regions share the numbers 3..7.</summary>
        public int Layer { get; }

        /// <summary>Common on layers 1 and 2; the region the layer belongs to below that.</summary>
        public Region Region { get; }

        /// <summary>
        /// What the layer is called before anything is learned about it. A layer main has not named yet reads
        /// 「竜の領域 第 4 層」 (§3.4).
        /// </summary>
        public string Name { get; }

        /// <summary>The name behind the blanks, if the layer hides one (world-v1.md §5.1).</summary>
        public string? TrueName { get; }

        /// <summary>
        /// Which of the seven backdrops (1..7) draws this layer. A layer with no art of its own borrows the art of
        /// the nearest named layer of its region (§3.2, §3.4).
        /// </summary>
        public int BackdropLayer { get; }

        public LayerMapSpec MapSpec { get; }

        /// <summary>Days to reach the boss without detouring: one per row.</summary>
        public int ShortestPathCost => MapSpec.ShortestPathCost;

        /// <summary>The region's king room: the last layer a life walks (§3.1).</summary>
        public bool IsKingLayer => Layer == Lair.KingLayer;

        /// <summary>The deepest layer of the route. The same as <see cref="IsKingLayer"/>.</summary>
        public bool IsLast => IsKingLayer;

        /// <summary>
        /// The name to print. A layer with a hidden name shows the blanks until the clue is picked up, and its true
        /// name afterwards (world-v1.md §5.1).
        /// </summary>
        public string DisplayName(bool clueTaken) =>
            clueTaken && TrueName != null ? TrueName : Name;

        /// <summary>This layer's map for the given seed.</summary>
        public LayerMap Map(ulong seed) => MapGenerator.Generate(MapSpec, seed);

        public override string ToString() => $"{Region.ToToken()} {Layer} {Name}";
    }

    /// <summary>
    /// The lair of the 80% build (seven_layers_v4.md §3): two shared layers, then three regions of four layers and
    /// each region's king room. Layer 8, 果てなき世界, is not built in the 80% version (§9).
    ///
    /// Every region is as long as the others: 5 + 5 + 5 + 4 + 4 + 4 + 3 = 30 days from the surface to the king when
    /// no detour is taken (§3.2).
    /// </summary>
    public static class Lair
    {
        /// <summary>Layers a route walks, from the surface to the king room.</summary>
        public const int LayerCount = 7;

        /// <summary>The layer of the three king rooms.</summary>
        public const int KingLayer = 7;

        /// <summary>The layer that hides its name until the carving is found.</summary>
        public const int SacramentLayer = 2;

        /// <summary>The first layer that belongs to a region. Layers above it are shared.</summary>
        public const int FirstRegionLayer = 3;

        /// <summary>The backdrops drawn so far, indexed 1..7 (Backdrop's own numbering).</summary>
        public const int BackdropCount = 7;

        /// <summary>The three regions, in the order the interlude of layer 2 offers them.</summary>
        public static IReadOnlyList<Region> Regions { get; } =
            new ReadOnlyCollection<Region>(new[] { Region.Dragon, Region.Parasite, Region.Ghost });

        private static readonly LayerProfile[] Shared = BuildShared();

        private static readonly IReadOnlyDictionary<Region, LayerProfile[]> ByRegion = BuildRegions();

        private static readonly IReadOnlyList<LayerProfile> AllProfiles = new ReadOnlyCollection<LayerProfile>(
            Shared.Concat(Regions.SelectMany(r => ByRegion[r])).ToArray());

        /// <summary>Every distinct layer: the two shared ones, then each region's five (17 in all).</summary>
        public static IReadOnlyList<LayerProfile> Profiles => AllProfiles;

        /// <summary>
        /// One layer. Layers 1 and 2 are shared and ignore <paramref name="region"/>; layers 3..7 need one of the three
        /// regions.
        /// </summary>
        public static LayerProfile Of(int layer, Region region = Region.Common)
        {
            if (layer < 1 || layer > LayerCount)
                throw new ArgumentOutOfRangeException(nameof(layer), layer, $"the lair has {LayerCount} layers");
            if (layer < FirstRegionLayer) return Shared[layer - 1];
            if (!region.HasKing())
                throw new ArgumentException($"layer {layer} belongs to a region; name one of {string.Join(" / ", Regions)}", nameof(region));
            return ByRegion[region][layer - FirstRegionLayer];
        }

        /// <summary>The seven layers a life walks into <paramref name="region"/>, from layer 1 to its king room.</summary>
        public static IReadOnlyList<LayerProfile> Route(Region region)
        {
            if (!region.HasKing())
                throw new ArgumentException($"a route ends in a king room; {region} has none", nameof(region));
            return new ReadOnlyCollection<LayerProfile>(Shared.Concat(ByRegion[region]).ToArray());
        }

        /// <summary>
        /// A layer's name as the screen prints it: the profile's <see cref="LayerProfile.DisplayName"/> on a shared
        /// layer or when the region is known, else 「第 N 層」.
        /// </summary>
        public static string LayerLabel(int layer, bool clueTaken, Region? region = null)
        {
            if (layer < 1 || layer > LayerCount)
                throw new ArgumentOutOfRangeException(nameof(layer), layer, $"the lair has {LayerCount} layers");
            if (layer < FirstRegionLayer) return Of(layer).DisplayName(clueTaken);
            if (region.HasValue && region.Value.HasKing()) return Of(layer, region.Value).DisplayName(clueTaken);
            return $"第 {layer} 層";
        }

        private static LayerProfile[] BuildShared() => new[]
        {
            new LayerProfile(1, Region.Common, "燦光の樹海", null, 1,
                Wide(1, battles: 3, events: 2, rests: 2, surveys: 1)),

            // Layer two hides its name. One battle is traded for the carving that reveals it (§8). The true name is
            // 龍神, the dragon god, not 竜神: 竜神 gave way to the dragon kings (world-v1.md §5.3).
            new LayerProfile(2, Region.Common, "■■の秘跡", "龍神の秘跡", 2,
                Wide(2, battles: 3, events: 2, rests: 2, surveys: 1).With(NodeKind.Carving, 1)),
        };

        // §3.2's table. Every region has the same node mix (§7); the regions differ by enemies and backdrops. A
        // layer main has not named yet is called after its region (§3.4) and borrows the backdrop of the nearest
        // named layer of the same region. The parasite region has no art yet (design draws it in #891): its layers
        // borrow the dragon region's two backdrops at the same depths until then.
        private static IReadOnlyDictionary<Region, LayerProfile[]> BuildRegions() => new Dictionary<Region, LayerProfile[]>
        {
            [Region.Dragon] = new[]
            {
                Third(Region.Dragon, "大黒蛇の住処", backdrop: 3),
                Deep(4, Region.Dragon, null, backdrop: 3),
                Deep(5, Region.Dragon, null, backdrop: 6),
                Deep(6, Region.Dragon, "獄雷峡", backdrop: 6),
                King(Region.Dragon, "竜王の間"),
            },
            [Region.Parasite] = new[]
            {
                Third(Region.Parasite, "蔓絡みの獣窟", backdrop: 3),
                Deep(4, Region.Parasite, null, backdrop: 3),
                Deep(5, Region.Parasite, null, backdrop: 6),
                Deep(6, Region.Parasite, "血染めの腐林", backdrop: 6),
                King(Region.Parasite, "爛れた世界樹の間"),
            },
            [Region.Ghost] = new[]
            {
                Third(Region.Ghost, null, backdrop: 4),
                Deep(4, Region.Ghost, "輝晶の間", backdrop: 4),
                Deep(5, Region.Ghost, "暗き死海", backdrop: 5),
                Deep(6, Region.Ghost, null, backdrop: 5),
                King(Region.Ghost, "死霊王の間"),
            },
        };

        private static LayerProfile Third(Region region, string? name, int backdrop) =>
            new LayerProfile(3, region, name ?? Unnamed(3, region), null, backdrop,
                Wide(3, battles: 3, events: 2, rests: 2, surveys: 1));

        // Layers 4〜6 narrow and hold no event: the deep layers are run through rather than combed (§7). Each still
        // offers two nodes that move max stamina, both rests (§7.1, R2-4).
        private static LayerProfile Deep(int layer, Region region, string? name, int backdrop) =>
            new LayerProfile(layer, region, name ?? Unnamed(layer, region), null, backdrop,
                Narrow(layer, battles: 2, rests: 2, surveys: 1));

        // The king room. Its middle row takes an event rather than a second rest: two rests side by side would let a
        // life rest twice right before the king (§7.1). Every king room is drawn on the void's backdrop until design
        // paints the three rooms (#891).
        private static LayerProfile King(Region region, string name) =>
            new LayerProfile(KingLayer, region, name, null, 7,
                new LayerMapSpec(KingLayer, new[] { 1, 4, 1 }, new Dictionary<NodeKind, int>
                {
                    [NodeKind.Battle] = 1,
                    [NodeKind.Elite] = 1,
                    [NodeKind.Rest] = 1,
                    [NodeKind.Event] = 1,
                }));

        private static string Unnamed(int layer, Region region) => $"{region.Label()}の領域 第 {layer} 層";

        /// <summary>Eleven nodes over five rows: the full-sized layer of dungeon_exploration_v4 §3.1.</summary>
        private static LayerMapSpec Wide(int layer, int battles, int events, int rests, int surveys) =>
            new LayerMapSpec(layer, new[] { 1, 3, 3, 3, 1 }, new Dictionary<NodeKind, int>
            {
                [NodeKind.Battle] = battles,
                [NodeKind.Elite] = 1,
                [NodeKind.Rest] = rests,
                [NodeKind.Survey] = surveys,
                [NodeKind.Event] = events,
            });

        /// <summary>Eight nodes over four rows, with no event.</summary>
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
