using DungeonCore;

namespace DungeonCore.Tests
{
    public class BattleFieldTests
    {
        // seven_layers_v4.md §2.4: 通常戦闘 / 精鋭 / 階層ボス per layer.
        [TestCase(1, 6, 7, 8)]
        [TestCase(2, 6, 7, 8)]
        [TestCase(3, 6, 7, 8)]
        [TestCase(4, 7, 8, 8)]
        [TestCase(5, 7, 8, 8)]
        [TestCase(6, 7, 8, 8)]
        [TestCase(7, 8, 8, 8)]
        public void EachLayer_HandsTheTablesLine(int layer, int normal, int elite, int boss)
        {
            Assert.Multiple(() =>
            {
                Assert.That(BattleField.Cells(layer, NodeKind.Battle), Is.EqualTo(normal));
                Assert.That(BattleField.Cells(layer, NodeKind.Elite), Is.EqualTo(elite));
                Assert.That(BattleField.Cells(layer, NodeKind.Boss), Is.EqualTo(boss));
            });
        }

        [Test]
        public void ALineIsWidened_UntilEveryEnemyStands()
        {
            // One size-2 enemy needs 7 (5 + 2): wider than layer 1's 6.
            Assert.That(BattleField.CellsFor(1, NodeKind.Battle, 2), Is.EqualTo(7));
            // A size-1 enemy fits the 6 as it is.
            Assert.That(BattleField.CellsFor(1, NodeKind.Battle, 1), Is.EqualTo(6));
            // The pair of roster §9 (two size-1 enemies) on layer 2's elite line of 7.
            Assert.That(BattleField.CellsFor(2, NodeKind.Elite, 2), Is.EqualTo(7));
        }

        [Test]
        public void NoFightNeedingMoreThanEightCells_IsPutOnALine()
        {
            Assert.Throws<ArgumentException>(() => BattleField.CellsFor(7, NodeKind.Boss, 4));
        }

        [TestCase(NodeKind.Rest)]
        [TestCase(NodeKind.Survey)]
        [TestCase(NodeKind.Event)]
        [TestCase(NodeKind.Carving)]
        public void ANodeWithoutAFight_HasNoLine(NodeKind kind)
        {
            Assert.Throws<ArgumentException>(() => BattleField.Cells(1, kind));
        }

        [TestCase(0)]
        [TestCase(8)]
        public void ALayerOutsideTheLair_IsRefused(int layer)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleField.Cells(layer, NodeKind.Battle));
        }

        [Test]
        public void EveryFightOfTheEncounterTables_FitsItsLine()
        {
            // roster §0 「大きさ」: these four use two cells, every other enemy one.
            var sizeTwo = new HashSet<string> { "armored_warden", "miasma_priest", "abyss_angler", "distortion_root" };
            for (int layer = 1; layer <= SevenLayers.Count; layer++)
            {
                foreach (var kind in new[] { NodeKind.Battle, NodeKind.Elite, NodeKind.Boss })
                {
                    foreach (var encounter in LayerEncounters.Of(layer).PoolFor(kind))
                    {
                        foreach (var fight in encounter.Fights)
                        {
                            int sizes = fight.EnemyIds.Sum(id => sizeTwo.Contains(id) ? 2 : 1);
                            Assert.That(BattleField.CellsFor(layer, kind, sizes), Is.LessThanOrEqualTo(BattleField.MaxCells),
                                $"layer {layer} {kind}: {fight}");
                        }
                    }
                }
            }
        }
    }
}
