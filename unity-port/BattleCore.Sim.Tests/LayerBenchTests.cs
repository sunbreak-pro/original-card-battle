using System.Text.Json;
using BattleCore;
using BattleCore.Sim;
using DungeonCore;

namespace BattleCore.Sim.Tests;

/// <summary>
/// #457: the layer measurement (encounters_and_rewards_v4 §6) fights what the layer tables say, on
/// the right line and stamina, and writes seven layers × six types of M1 / M2 / M3, the same for the
/// same seed.
/// </summary>
public class LayerBenchTests
{
    private static string Ids(Encounter encounter) => string.Join(",", encounter.Fights.Select(f => f.ToString()));

    [Test]
    public void TheLayers_AreTheSevenOfTheDungeonTable_WithItsNormalsAndBoss()
    {
        Assert.That(LayerBench.Layers.Select(l => l.Layer), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }));
        foreach (var spec in LayerBench.Layers)
        {
            var table = LayerEncounters.Of(spec.Layer);
            Assert.That(spec.NormalIds, Is.EqualTo(table.Normal.Select(Ids)), $"layer {spec.Layer} normals");
            Assert.That(table.Boss.Select(Ids), Is.EqualTo(new[] { spec.BossId }), $"layer {spec.Layer} boss");
        }
    }

    [Test]
    public void TheChain_IsANormal_ThenAnElite_ThenTheBoss_OfTheSameLayer()
    {
        foreach (var spec in LayerBench.Layers)
        {
            var table = LayerEncounters.Of(spec.Layer);
            var fights = spec.ChainFights;
            Assert.That(ChainOrder.Parse(spec.ChainSpec).All(b => b.Layer == spec.Layer), Is.True, $"layer {spec.Layer} chain layers");
            Assert.That(fights[0].Kind, Is.EqualTo(NodeFight.Normal));
            Assert.That(table.Normal.Select(Ids), Does.Contain(fights[0].Name), $"layer {spec.Layer} entry");
            Assert.That(fights[^1].Kind, Is.EqualTo(NodeFight.Boss));
            Assert.That(fights[^1].Name, Is.EqualTo(spec.BossId), $"layer {spec.Layer} boss");
            string elite = string.Join(",", fights.Where(f => f.Kind == NodeFight.Elite).Select(f => f.Name));
            Assert.That(table.Elite.Select(Ids), Does.Contain(elite), $"layer {spec.Layer} elite");
            Assert.That(fights.Count, Is.EqualTo(spec.Layer == 1 || spec.Layer == 4 ? 4 : 3), $"layer {spec.Layer} length");
        }
    }

    [Test]
    public void TheLines_AreSection2_3s_AndHoldEveryFight()
    {
        Assert.That(LayerBench.Layers.Select(l => l.NormalCells), Is.EqualTo(new[] { 6, 6, 6, 7, 7, 7, 8 }));
        Assert.That(LayerBench.Layers.Select(l => l.EliteCells), Is.EqualTo(new[] { 7, 7, 7, 8, 8, 8, 8 }));
        Assert.That(LayerBench.Layers.Select(l => l.BossCells), Is.EqualTo(new[] { 8, 8, 8, 8, 8, 8, 8 }));
        foreach (var spec in LayerBench.Layers)
        foreach (var fight in spec.NormalFights.Append(spec.BossFight).Concat(spec.ChainFights))
        {
            Assert.That(fight.FieldCells, Is.GreaterThanOrEqualTo(Runner.FieldCellsFor(fight.Enemies)), $"layer {spec.Layer} {fight.Name}");
        }
    }

    [Test]
    public void TheLines_AreTheDungeonsBattleField_ForEveryLayerAndKind()
    {
        foreach (var spec in LayerBench.Layers)
        {
            Assert.That(spec.CellsFor(NodeFight.Normal), Is.EqualTo(BattleField.Cells(spec.Layer, NodeKind.Battle)), $"layer {spec.Layer} normal");
            Assert.That(spec.CellsFor(NodeFight.Elite), Is.EqualTo(BattleField.Cells(spec.Layer, NodeKind.Elite)), $"layer {spec.Layer} elite");
            Assert.That(spec.CellsFor(NodeFight.Boss), Is.EqualTo(BattleField.Cells(spec.Layer, NodeKind.Boss)), $"layer {spec.Layer} boss");
        }
    }

    [Test]
    public void TheMaxStamina_IsSection6s_EntryForNormals_BeforeTheBossForTheRest()
    {
        Assert.That(LayerBench.Layers.Select(l => l.NormalFights[0].MaxStamina), Is.EqualTo(new[] { 10, 10, 10, 9, 9, 8, 7 }));
        Assert.That(LayerBench.Layers.Select(l => l.BossFight.MaxStamina), Is.EqualTo(new[] { 10, 10, 9, 9, 8, 7, 6 }));
        foreach (var spec in LayerBench.Layers)
        foreach (var fight in spec.ChainFights)
        {
            Assert.That(fight.MiasmaPercent, Is.EqualTo(fight.Kind == NodeFight.Normal ? spec.EntryMiasma : spec.BossMiasma), $"layer {spec.Layer} {fight.Name}");
        }
    }

    [Test]
    public void ASetup_PutsBothEnemiesOfAPair_OnTheLayersLine()
    {
        var spec = LayerBench.Layers[1];
        var pair = spec.ChainFights[1];
        var setup = LayerBench.Setup(Decks.Archetypes[0], pair, 30, 4);
        Assert.That(setup.Enemy.Id, Is.EqualTo("polearm_warped"));
        Assert.That(setup.MoreEnemies!.Select(e => e.Id), Is.EqualTo(new[] { "crossbow_hunter" }));
        Assert.That(setup.FieldCells, Is.EqualTo(7));
        Assert.That(setup.PlayerMaxStamina, Is.EqualTo(10));
        Assert.That(setup.StartHp, Is.EqualTo(30));
        Assert.That(setup.PlayerStartStamina, Is.EqualTo(4));
    }

    [Test]
    public void TheStartHp_IsTheShareOfTheMax_RoundedAwayFromZero()
    {
        Assert.That(new LayerOptions().StartHp, Is.EqualTo(Constants.PlayerMaxHp));
        Assert.That(new LayerOptions(StartHpPercent: 50).StartHp, Is.EqualTo(25));
        Assert.That(new LayerOptions(StartHpPercent: 1).StartHp, Is.EqualTo(1));
    }

    [Test]
    public void TheJson_HasARowPerLayerAndType_WithM1M2M3_AndReplaysForTheSameSeed()
    {
        string Json(int threads) => LayerBench.Json(new LayerOptions(Seed: 3, Runs: 1, Threads: threads), LayerBench.Run(new LayerOptions(Seed: 3, Runs: 1, Threads: threads)));

        string once = Json(1);
        Assert.That(Json(4), Is.EqualTo(once));

        using var doc = JsonDocument.Parse(once);
        var root = doc.RootElement;
        Assert.That(root.GetProperty("format").GetString(), Is.EqualTo(LayerBench.Format));
        Assert.That(root.GetProperty("layers").GetArrayLength(), Is.EqualTo(7));
        var rows = root.GetProperty("rows").EnumerateArray().ToList();
        Assert.That(rows.Count, Is.EqualTo(7 * Decks.Archetypes.Count));
        foreach (var spec in LayerBench.Layers)
        {
            var mine = rows.Where(r => r.GetProperty("layer").GetInt32() == spec.Layer).ToList();
            Assert.That(mine.Select(r => r.GetProperty("deck").GetString()), Is.EqualTo(Decks.Archetypes.Select(d => d.Id)), $"layer {spec.Layer}");
            foreach (var row in mine)
            {
                foreach (string m in new[] { "m1", "m2Normal", "m2Boss", "m3" })
                {
                    Assert.That(row.GetProperty(m).ValueKind, Is.EqualTo(JsonValueKind.Number), $"layer {spec.Layer} {m}");
                }
                Assert.That(row.GetProperty("normalRuns").GetInt32(), Is.EqualTo(spec.NormalIds.Count));
                Assert.That(row.GetProperty("bossRuns").GetInt32(), Is.EqualTo(1));
                Assert.That(row.GetProperty("chainRuns").GetInt32(), Is.EqualTo(1));
            }
        }
    }

    [Test]
    public void ARow_ReadsM1M2M3_FromItsRecords()
    {
        BattleRecord Rec(Outcome outcome, int turns) =>
            new BattleRecord("d", "e", EnemyRank.Normal, outcome, turns, Array.Empty<int>(), 0, 0, 0, 0, 0, 0, 0);
        var row = new LayerRow(1, "d",
            new[] { Rec(Outcome.Won, 4), Rec(Outcome.Lost, 6), Rec(Outcome.Won, 8), Rec(Outcome.Stalled, 60) },
            new[] { Rec(Outcome.Won, 12), Rec(Outcome.Lost, 14), Rec(Outcome.Won, 20) },
            new[] { Outcome.Won, Outcome.Lost, Outcome.Lost, Outcome.Won, Outcome.Stalled });
        Assert.That(row.M1, Is.EqualTo(50));
        Assert.That(row.M2Normal, Is.EqualTo(7));
        Assert.That(row.M2Boss, Is.EqualTo(14));
        Assert.That(row.M3, Is.EqualTo(40));
    }

    [Test]
    public void Layers_IsParsed_WithItsHp_AndRefusesWhatDoesNotGoWithIt()
    {
        Assert.That(Program.TryParse(new[] { "--layers", "--seed", "4", "--runs", "9", "--layer-hp", "80" }, out var options, out _, out _, out _, out _), Is.True);
        Assert.That(options.Layers, Is.EqualTo(new LayerOptions(4, 9, 80, 0, 0)));
        Assert.That(Program.TryParse(new[] { "--layers" }, out var plain, out _, out _, out _, out _), Is.True);
        Assert.That(plain.Layers!.StartHpPercent, Is.EqualTo(100));
        Assert.That(Program.TryParse(Array.Empty<string>(), out var bench, out _, out _, out _, out _), Is.True);
        Assert.That(bench.Layers, Is.Null);

        Assert.That(Program.TryParse(new[] { "--layer-hp", "80" }, out _, out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParse(new[] { "--layers", "--layer-hp", "0" }, out _, out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParse(new[] { "--layers", "--layer-hp", "101" }, out _, out _, out _, out _, out _), Is.False);
        Assert.That(Program.TryParse(new[] { "--layers", "--only", "first3" }, out _, out _, out _, out _, out _), Is.False);
    }
}
