using System.Text.Encodings.Web;
using System.Text.Json;

namespace BattleCore.Sim;

/// <summary>The kind of combat node a battle stands for (encounters_and_rewards_v4 §2.3 / §6).</summary>
public enum NodeFight
{
    Normal,
    Elite,
    Boss,
}

/// <summary>
/// One fight of a measurement: the enemies fought at once (in phase order), the line, the max stamina,
/// and whether HP and stamina come in from the fight before (the layer chain carries them, with no
/// rest between) or the fight starts at <see cref="LayerOptions.StartHpPercent"/>.
/// </summary>
public sealed record LayerFight(NodeFight Kind, IReadOnlyList<EnemyDef> Enemies, int FieldCells, int MiasmaPercent)
{
    /// <summary>§6: max stamina falls by 1 per full 20% of 瘴気 and stops at 6 (<see cref="Chain.MaxStaminaAt"/>).</summary>
    public int MaxStamina => Chain.MaxStaminaAt(MiasmaPercent);

    public string Name => string.Join("+", Enemies.Select(e => e.Id));
}

/// <summary>
/// One layer's conditions, from encounters_and_rewards_v4: the normals of §2.1 (等確率, so each is
/// measured on its own), the boss, the layer chain of §3, the line of §2.3 and the 瘴気 of §6
/// (normals fight at the layer's entry, elites and the boss at the value before the boss), with the
/// targets of §6's table.
/// </summary>
public sealed record LayerSpec(
    int Layer,
    IReadOnlyList<string> NormalIds,
    string BossId,
    string ChainSpec,
    IReadOnlyList<NodeFight> ChainKinds,
    int NormalCells,
    int EliteCells,
    int BossCells,
    int EntryMiasma,
    int BossMiasma,
    int M1Target,
    int M3Target,
    (int Min, int Max) M2NormalTarget,
    (int Min, int Max) M2BossTarget)
{
    public int CellsFor(NodeFight kind) => kind switch
    {
        NodeFight.Normal => NormalCells,
        NodeFight.Elite => EliteCells,
        _ => BossCells,
    };

    /// <summary>§6: normal fights at the entry's 瘴気; elites and the boss, to be strict, at the value before the boss.</summary>
    public int MiasmaFor(NodeFight kind) => kind == NodeFight.Normal ? EntryMiasma : BossMiasma;

    public LayerFight Fight(NodeFight kind, IReadOnlyList<string> enemyIds) =>
        new LayerFight(kind, enemyIds.Select(Enemies.ById).ToList(), CellsFor(kind), MiasmaFor(kind));

    /// <summary>M1's fights: each normal of the layer alone.</summary>
    public IReadOnlyList<LayerFight> NormalFights => NormalIds.Select(id => Fight(NodeFight.Normal, new[] { id })).ToList();

    /// <summary>M2's boss fight, from full conditions (not carried).</summary>
    public LayerFight BossFight => Fight(NodeFight.Boss, new[] { BossId });

    /// <summary>M3's fights: §3's <c>-chain</c> order, each battle with the line and 瘴気 of its node kind.</summary>
    public IReadOnlyList<LayerFight> ChainFights
    {
        get
        {
            var order = ChainOrder.Parse(ChainSpec);
            if (order.Count != ChainKinds.Count)
            {
                throw new InvalidOperationException($"layer {Layer}: the chain has {order.Count} battles but {ChainKinds.Count} kinds.");
            }
            return order.Select((b, i) => Fight(ChainKinds[i], b.EnemyIds)).ToList();
        }
    }
}

/// <summary>What the layer measurement runs: the seed, the runs per fight, the HP the player starts from, the threads and the play cap.</summary>
public sealed record LayerOptions(
    int Seed = BenchOptions.DefaultSeed,
    int Runs = BenchOptions.DefaultRuns,
    int StartHpPercent = LayerOptions.DefaultStartHpPercent,
    int Threads = 0,
    int MaxPlays = 0)
{
    /// <summary>§6: 「HP は 100% から始めます」.</summary>
    public const int DefaultStartHpPercent = 100;

    /// <summary>The HP a fight that carries nothing starts from: the percentage of the max, rounded away from zero, at least 1.</summary>
    public int StartHp => Math.Max(1, (int)Math.Round(Constants.PlayerMaxHp * StartHpPercent / 100.0, MidpointRounding.AwayFromZero));
}

/// <summary>One deck on one layer: M1 over the normals, M2 over the normals and over the boss, M3 over the layer chain.</summary>
public sealed record LayerRow(
    int Layer,
    string DeckId,
    IReadOnlyList<BattleRecord> Normals,
    IReadOnlyList<BattleRecord> Bosses,
    IReadOnlyList<Outcome> Chains)
{
    public double M1 => Criteria.WinRate(Normals);

    public double M2Normal => Criteria.Median(Normals.Select(r => r.Turns));

    public double M2Boss => Criteria.Median(Bosses.Select(r => r.Turns));

    public double M3 => Chains.Count == 0 ? 0 : 100.0 * Chains.Count(o => o == Outcome.Won) / Chains.Count;
}

/// <summary>
/// #457: the difficulty curve of encounters_and_rewards_v4 §6, layer by layer and deck type by deck
/// type — M1 the normal fights' win rate, M2 the median turns (normal fights; the boss), M3 the
/// layer chain's win rate. The tables are §2.1 / §2.3 / §3 / §6 as written; BattleCore.Sim.Tests
/// checks them against DungeonCore.LayerEncounters so the two cannot drift apart.
/// </summary>
public static class LayerBench
{
    public const string Format = "battlecore-sim-layers/1";

    private static readonly (int, int) NormalTurns = (6, 10);
    private static readonly (int, int) RealBossTurns = (12, 18);
    private static readonly (int, int) EliteStandInBossTurns = (10, 14);

    private static readonly NodeFight[] Three = { NodeFight.Normal, NodeFight.Elite, NodeFight.Boss };

    /// <summary>Layers 1 and 4: the elite is two fights back to back, so the chain is four (§3).</summary>
    private static readonly NodeFight[] Four = { NodeFight.Normal, NodeFight.Elite, NodeFight.Elite, NodeFight.Boss };

    public static IReadOnlyList<LayerSpec> Layers { get; } = new[]
    {
        new LayerSpec(1, new[] { "polearm_warped", "shadow_hound" }, "shadow_hound",
            "polearm_warped@1,polearm_warped@1,shadow_hound@1,shadow_hound@1", Four,
            6, 7, 8, 1, 5, 90, 75, NormalTurns, NormalTurns),
        new LayerSpec(2, new[] { "polearm_warped", "shadow_hound", "rusted_revenant", "crossbow_hunter" }, "rusted_revenant",
            "polearm_warped@2,polearm_warped+crossbow_hunter@2,rusted_revenant@2", Three,
            6, 7, 8, 6, 10, 85, 75, NormalTurns, NormalTurns),
        new LayerSpec(3, new[] { "rusted_revenant", "crossbow_hunter", "polearm_crystal" }, "miasma_priest",
            "rusted_revenant@3,polearm_warped+crossbow_hunter@3,miasma_priest@3", Three,
            6, 7, 8, 12, 20, 80, 65, NormalTurns, RealBossTurns),
        new LayerSpec(4, new[] { "rusted_revenant", "mist_archer", "twin_blade_warped", "polearm_crystal" }, "armored_warden",
            "rusted_revenant@4,twin_blade_warped@4,polearm_crystal@4,armored_warden@4", Four,
            7, 8, 8, 23, 32, 75, 55, NormalTurns, EliteStandInBossTurns),
        new LayerSpec(5, new[] { "mist_archer", "twin_blade_warped" }, "polearm_unyielding",
            "mist_archer@5,pack_alpha@5,polearm_unyielding@5", Three,
            7, 8, 8, 36, 48, 65, 55, NormalTurns, EliteStandInBossTurns),
        new LayerSpec(6, new[] { "mist_archer", "twin_blade_warped" }, "abyss_angler",
            "mist_archer@6,pack_alpha@6,abyss_angler@6", Three,
            7, 8, 8, 53, 68, 55, 45, NormalTurns, RealBossTurns),
        new LayerSpec(7, new[] { "twin_blade_warped" }, "distortion_root",
            "twin_blade_warped@7,pack_alpha@7,distortion_root@7", Three,
            8, 8, 8, 75, 89, 50, 40, NormalTurns, RealBossTurns),
    };

    /// <summary>The setup of one fight: the first enemy and the rest in phase order, the line, the max stamina, and the HP and stamina it starts from.</summary>
    public static BattleSetup Setup(SimDeck deck, LayerFight fight, int? startHp, int? startStamina)
    {
        if (fight.FieldCells < Runner.FieldCellsFor(fight.Enemies))
        {
            throw new InvalidOperationException($"{fight.Name} needs {Runner.FieldCellsFor(fight.Enemies)} cells, more than the {fight.FieldCells} given.");
        }
        return new BattleSetup(
            fight.Enemies[0], deck.Build(), fight.FieldCells,
            PlayerMaxStamina: fight.MaxStamina,
            MoreEnemies: fight.Enemies.Count > 1 ? fight.Enemies.Skip(1).ToList() : null,
            PlayerStartHp: startHp,
            PlayerStartStamina: startStamina);
    }

    /// <summary>
    /// The layer chain (§3): the first fight from <see cref="LayerOptions.StartHp"/> and full stamina,
    /// each next one with the HP and stamina the last left (<see cref="Chain.Carry"/>; no rest). Won
    /// when every fight is won; otherwise the outcome of the fight that ended it.
    /// </summary>
    public static Outcome FightChain(SimDeck deck, IReadOnlyList<LayerFight> fights, GreedyPlayer player, int startHp, int seed)
    {
        int? hp = startHp;
        int? stamina = null;
        for (int i = 0; i < fights.Count; i++)
        {
            var (record, final) = Runner.Fight(deck, Setup(deck, fights[i], hp, stamina), player, unchecked(seed + i * 7919));
            if (!record.Won) return record.Outcome;
            (hp, stamina) = Chain.Carry(final);
        }
        return Outcome.Won;
    }

    /// <summary>Every layer × the six types; the same seed gives the same rows whatever the thread count.</summary>
    public static IReadOnlyList<LayerRow> Run(LayerOptions options)
    {
        var player = new GreedyPlayer(Positioning.Neutral, options.MaxPlays);
        var decks = Decks.Archetypes;
        int startHp = options.StartHp;

        var battles = new List<(int Layer, string DeckId, NodeFight Kind, Func<BattleRecord> Job)>();
        var chains = new List<(int Layer, string DeckId, Func<Outcome> Job)>();
        foreach (var layer in Layers)
        {
            var normals = layer.NormalFights;
            var boss = layer.BossFight;
            var order = layer.ChainFights;
            foreach (var deck in decks)
            {
                foreach (var fight in normals)
                for (int run = 0; run < options.Runs; run++)
                {
                    int seed = Runner.SeedFor(options.Seed, "layer-normal", $"{layer.Layer}:{fight.Name}", run);
                    battles.Add((layer.Layer, deck.Id, NodeFight.Normal, () => Runner.Fight(deck, Setup(deck, fight, startHp, null), player, seed).Record));
                }
                for (int run = 0; run < options.Runs; run++)
                {
                    int seed = Runner.SeedFor(options.Seed, "layer-boss", $"{layer.Layer}:{boss.Name}", run);
                    battles.Add((layer.Layer, deck.Id, NodeFight.Boss, () => Runner.Fight(deck, Setup(deck, boss, startHp, null), player, seed).Record));
                }
                for (int run = 0; run < options.Runs; run++)
                {
                    int seed = Runner.SeedFor(options.Seed, "layer-chain", layer.Layer.ToString(System.Globalization.CultureInfo.InvariantCulture), run);
                    chains.Add((layer.Layer, deck.Id, () => FightChain(deck, order, player, startHp, seed)));
                }
            }
        }

        var battleOut = Bench.RunAll(battles.Select(b => b.Job).ToList(), options.Threads);
        var chainOut = Bench.RunAll(chains.Select(c => c.Job).ToList(), options.Threads);

        var rows = new List<LayerRow>();
        foreach (var layer in Layers)
        foreach (var deck in decks)
        {
            bool Mine(int l, string d) => l == layer.Layer && d == deck.Id;
            rows.Add(new LayerRow(
                layer.Layer, deck.Id,
                battles.Select((b, i) => (b, i)).Where(x => Mine(x.b.Layer, x.b.DeckId) && x.b.Kind == NodeFight.Normal).Select(x => battleOut[x.i]).ToList(),
                battles.Select((b, i) => (b, i)).Where(x => Mine(x.b.Layer, x.b.DeckId) && x.b.Kind == NodeFight.Boss).Select(x => battleOut[x.i]).ToList(),
                chains.Select((c, i) => (c, i)).Where(x => Mine(x.c.Layer, x.c.DeckId)).Select(x => chainOut[x.i]).ToList()));
        }
        return rows;
    }

    /// <summary>The measurement as JSON: the conditions of each layer, then one row per layer × type with M1 / M2 / M3.</summary>
    public static string Json(LayerOptions options, IReadOnlyList<LayerRow> rows)
    {
        var doc = new
        {
            format = Format,
            seed = options.Seed,
            runs = options.Runs,
            startHpPercent = options.StartHpPercent,
            startHp = options.StartHp,
            turnLimit = Runner.TurnLimit,
            maxPlays = options.MaxPlays > 0 ? options.MaxPlays : (int?)null,
            policy = "greedy, Neutral positioning (GreedyPlayer)",
            layers = Layers.Select(l => new
            {
                layer = l.Layer,
                normals = l.NormalIds,
                boss = l.BossId,
                chain = l.ChainSpec,
                chainKinds = l.ChainKinds.Select(k => k.ToString()),
                fieldCells = new { normal = l.NormalCells, elite = l.EliteCells, boss = l.BossCells },
                entryMiasma = l.EntryMiasma,
                entryMaxStamina = Chain.MaxStaminaAt(l.EntryMiasma),
                bossMiasma = l.BossMiasma,
                bossMaxStamina = Chain.MaxStaminaAt(l.BossMiasma),
                targets = new
                {
                    m1 = l.M1Target,
                    m2Normal = new { min = l.M2NormalTarget.Min, max = l.M2NormalTarget.Max },
                    m2Boss = new { min = l.M2BossTarget.Min, max = l.M2BossTarget.Max },
                    m3 = l.M3Target,
                },
            }),
            rows = rows.Select(r =>
            {
                var spec = Layers[r.Layer - 1];
                return new
                {
                    layer = r.Layer,
                    deck = r.DeckId,
                    m1 = R(r.M1),
                    m2Normal = R(r.M2Normal),
                    m2Boss = R(r.M2Boss),
                    m3 = R(r.M3),
                    m1Met = r.M1 >= spec.M1Target,
                    m2NormalMet = r.M2Normal >= spec.M2NormalTarget.Min && r.M2Normal <= spec.M2NormalTarget.Max,
                    m2BossMet = r.M2Boss >= spec.M2BossTarget.Min && r.M2Boss <= spec.M2BossTarget.Max,
                    m3Met = r.M3 >= spec.M3Target,
                    normalRuns = r.Normals.Count,
                    normalStalls = r.Normals.Count(b => b.Outcome == Outcome.Stalled),
                    bossRuns = r.Bosses.Count,
                    bossStalls = r.Bosses.Count(b => b.Outcome == Outcome.Stalled),
                    chainRuns = r.Chains.Count,
                    chainStalls = r.Chains.Count(o => o == Outcome.Stalled),
                };
            }),
        };
        var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        return JsonSerializer.Serialize(doc, json) + "\n";
    }

    private static double R(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
