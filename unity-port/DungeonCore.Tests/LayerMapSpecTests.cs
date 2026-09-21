using DungeonCore;

namespace DungeonCore.Tests
{
    public class LayerMapSpecTests
    {
        [Test]
        public void ASpecWithoutASingleEntryRowIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new LayerMapSpec(
                1,
                new[] { 2, 2, 1 },
                new Dictionary<NodeKind, int> { [NodeKind.Battle] = 3 }));
        }

        [Test]
        public void ASpecWhoseQuotasDoNotFillTheMapIsRejected()
        {
            var error = Assert.Throws<ArgumentException>(() => new LayerMapSpec(
                1,
                new[] { 1, 2, 2, 1 },
                new Dictionary<NodeKind, int> { [NodeKind.Battle] = 2 }));
            Assert.That(error!.Message, Does.Contain("middle nodes"));
        }

        [Test]
        public void ASpecThatWouldForceADetourIsRejected()
        {
            // Two elites in a two-wide row would leave no way past them.
            var error = Assert.Throws<ArgumentException>(() => new LayerMapSpec(
                1,
                new[] { 1, 2, 1 },
                new Dictionary<NodeKind, int> { [NodeKind.Elite] = 2 }));
            Assert.That(error!.Message, Does.Contain("avoidable"));
        }

        [Test]
        public void TheBossCannotBeAskedForByQuota()
        {
            Assert.Throws<ArgumentException>(() => new LayerMapSpec(
                1,
                new[] { 1, 2, 2, 1 },
                new Dictionary<NodeKind, int> { [NodeKind.Boss] = 1, [NodeKind.Battle] = 3 }));
        }

        [Test]
        public void WithTradesABattleForAnotherKind()
        {
            var full = LayerMapSpec.Full(2);
            var spec = full.With(NodeKind.Carving, 1);
            Assert.That(spec.Quotas[NodeKind.Carving], Is.EqualTo(1));
            Assert.That(spec.Quotas[NodeKind.Battle], Is.EqualTo(full.Quotas[NodeKind.Battle] - 1));
            Assert.That(spec.NodeCount, Is.EqualTo(full.NodeCount));
        }

        [Test]
        public void WithRefusesToTradeMoreBattlesThanThereAre()
        {
            Assert.Throws<ArgumentException>(() => LayerMapSpec.Minimal().With(NodeKind.Trace, 3));
        }

        [Test]
        public void TheCarvingCanBePlacedAndIsStillAvoidable()
        {
            // world-v1.md §5: the layer-two carving names 竜神の秘跡. It is a detour, so a run
            // that spends no 刻限 on it never learns the name.
            var spec = LayerMapSpec.Full(2).With(NodeKind.Carving, 1);
            var map = MapGenerator.Generate(spec, 41UL);
            Assert.That(map.CountOf(NodeKind.Carving), Is.EqualTo(1));

            var carving = map.Nodes.Single(n => n.Kind == NodeKind.Carving);
            Assert.That(carving.Row, Is.Not.Zero);
            Assert.That(carving.Row, Is.Not.EqualTo(map.RowCount - 1));
            Assert.That(map.Row(carving.Row).Any(n => !n.Kind.IsDetour()), Is.True);
        }
    }
}
