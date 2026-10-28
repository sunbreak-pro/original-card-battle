using BattleCore;
using DungeonCore;
using BattleRng = BattleCore.SeededRng;
using DungeonRng = DungeonCore.SeededRng;

namespace BattleCore.Sim.Tests;

/// <summary>
/// #594: the battle core and the exploration core do not reference each other (#97), so each writes its
/// own copy of the numbers a life carries across the two: the max stamina and its bounds, the rest
/// between layers, the seeded generator and each layer's 瘴気 density. They agree today. This is the one
/// place that sees both, so a value changed on one side only fails here instead of making the same
/// character's max stamina or rest differ between the map and the battle (#99).
/// It lives in BattleCore.Sim.Tests for the same reason as EncounterTableTests: the project is dotnet-only.
/// </summary>
public class CoreAgreementTests
{
    [Test]
    public void TheMaxStaminaBaseAndBounds_AreTheSameOnBothSides()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Miasma.BaseMaxStamina, Is.EqualTo(Constants.BaseMaxStamina));
            Assert.That(Miasma.MaxStaminaFloor, Is.EqualTo(Constants.MaxStaminaFloor));
            Assert.That(Miasma.MaxStaminaCeil, Is.EqualTo(Constants.MaxStaminaCeil));
        });
    }

    [Test]
    public void TheMaxStaminaAtEveryGauge_IsTheSameOnBothSides()
    {
        for (int percent = 0; percent <= 99; percent++)
        {
            Assert.That(Miasma.MaxStamina(0, percent), Is.EqualTo(Chain.MaxStaminaAt(percent)), $"瘴気 {percent}%");
        }
    }

    [Test]
    public void TheRestBetweenLayers_GivesBackTheSameHp()
    {
        Assert.That(ExplorationReducer.InterludeHpPercent, Is.EqualTo(Constants.ChainRestHpPercent));
        // 45 and 55 land on a half (13.5, 16.5), so the two sides must also round the same way.
        foreach (int maxHp in new[] { 50, 45, 55, 1, 99 })
        {
            Assert.That(ExplorationReducer.InterludeHeal(maxHp), Is.EqualTo(Chain.Rest(0, maxHp, 10).Hp), $"max HP {maxHp}");
        }
    }

    [Test]
    public void TheSeededGenerators_DrawTheSameSequenceFromTheSameSeed()
    {
        foreach (int seed in new[] { 0, 1, 12345, int.MaxValue, -1 })
        {
            var battle = new BattleRng(seed);
            var dungeon = new DungeonRng(unchecked((ulong)seed));
            for (int i = 0; i < 64; i++)
            {
                Assert.That(dungeon.NextDouble(), Is.EqualTo(battle.NextDouble()), $"seed {seed}, draw {i}");
            }
        }
    }

    [Test]
    public void EachLayersDensity_IsTheSameOnBothSides()
    {
        Assert.That(SevenLayers.All.Select(layer => layer.Density), Is.EqualTo(ChainOrder.Densities));
    }

    /// <summary>
    /// The exploration core's battle-width table copies the line's bounds and the cells before the first
    /// enemy from the battle core (#99). A change on the battle side only would hand a too-short line.
    /// </summary>
    [Test]
    public void TheBattleFieldBoundsAndOpeningCells_AreTheSameOnBothSides()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BattleField.MinCells, Is.EqualTo(Constants.FieldCellsMin));
            Assert.That(BattleField.MaxCells, Is.EqualTo(Constants.FieldCellsMax));
            Assert.That(BattleField.CellsBeforeEnemies, Is.EqualTo(Constants.PlayerStartCell + Constants.StartGap));
        });
    }

    /// <summary>
    /// Field.EnemyStartCell shrinks the opening gap without an error when the line is too short, so every
    /// line the exploration hands across must hold the full gap.
    /// </summary>
    [Test]
    public void EveryLineTheExplorationHands_KeepsTheFullOpeningGap()
    {
        int expected = Constants.PlayerStartCell + 1 + Constants.StartGap;
        int checkedCount = 0;
        for (int layer = 1; layer <= 7; layer++)
        {
            foreach (var kind in new[] { NodeKind.Battle, NodeKind.Elite, NodeKind.Boss })
            {
                for (int size = 1; size <= 3; size++)
                {
                    int cells;
                    try
                    {
                        cells = BattleField.CellsFor(layer, kind, size);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                    int start = Field.EnemyStartCell(cells, Constants.PlayerStartCell, 1, size, Constants.StartGap);
                    Assert.That(start, Is.EqualTo(expected), $"layer {layer}, {kind}, enemy size {size}, {cells} cells");
                    checkedCount++;
                }
            }
        }
        Assert.That(checkedCount, Is.GreaterThan(0));
    }
}
