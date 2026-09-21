using DungeonCore;

namespace DungeonCore.Tests
{
    public class GoldenMapTests
    {
        /// <summary>
        /// The whole minimal layer for one seed, written out. Pinning it catches a refactor that
        /// reorders the draws: the map would still be valid and still be deterministic, but every
        /// seed would name a different map than it did before.
        /// </summary>
        private const string MinimalLayerSeedOne =
            "L1|0:0,0,battle->1.2;1:1,0,battle->3;2:1,1,survey->4;3:2,0,rest->5;4:2,1,elite->5;5:3,0,boss->;";

        [Test]
        public void TheMinimalLayerAtSeedOneIsPinned()
        {
            Assert.That(MapGenerator.Generate(LayerMapSpec.Minimal(1), 1UL).Fingerprint(),
                Is.EqualTo(MinimalLayerSeedOne));
        }

        [Test]
        public void ThePinnedMapIsTheShapeTheDesignDocDraws()
        {
            // dungeon_exploration_v4.md §4.1: entry battle, a two-way fork, a second two-way
            // fork, boss. Four rows, six nodes, both forks avoidable.
            var map = MapGenerator.Generate(LayerMapSpec.Minimal(1), 1UL);
            Assert.That(map.Successors(map.EntryId).Count, Is.EqualTo(2));
            Assert.That(map.RowCount, Is.EqualTo(4));
            Assert.That(map.NodeCount, Is.EqualTo(6));
            Assert.That(map.Row(1).Select(n => n.Kind), Is.EquivalentTo(new[] { NodeKind.Battle, NodeKind.Survey }));
            Assert.That(map.Row(2).Select(n => n.Kind), Is.EquivalentTo(new[] { NodeKind.Rest, NodeKind.Elite }));
        }
    }
}
