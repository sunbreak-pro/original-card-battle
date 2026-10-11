using BattleCore;
using DungeonCore;

namespace BattleCore.Sim.Tests;

/// <summary>
/// #456: the exploration side's encounter table (DungeonCore.LayerEncounters) holds enemy ids as plain
/// strings, since DungeonCore does not reference BattleCore (#97). This is the one place that sees both,
/// so a renamed id or a resized enemy in BattleCore.Enemies fails here instead of at the first battle.
/// It lives in BattleCore.Sim.Tests because that project is dotnet-only: unity-sync-plan.mjs does not
/// copy it into Unity, so no asmdef has to learn the extra reference.
/// </summary>
public class EncounterTableTests
{
    // enemy_roster_v4.md §0 「大きさ」. Pinned here rather than read from BattleCore.Enemies, so a size
    // changed there without the roster changing fails the field check below.
    private static readonly Dictionary<string, int> RosterSizes = new()
    {
        ["polearm_warped"] = 1,
        ["shadow_hound"] = 1,
        ["rusted_revenant"] = 1,
        ["crossbow_hunter"] = 1,
        ["mist_archer"] = 1,
        ["twin_blade_warped"] = 1,
        ["polearm_crystal"] = 1,
        ["armored_warden"] = 2,
        ["pack_alpha"] = 1,
        ["polearm_unyielding"] = 1,
        ["miasma_priest"] = 2,
        ["abyss_angler"] = 2,
        ["distortion_root"] = 2,
    };

    // The cells in front of the enemies: the player's start cell 2 plus START_GAP 3 (battle_core_v4).
    private const int CellsBeforeTheEnemies = 5;

    /// <summary>Every fight of every pool of every layer, with where it sits for the failure message.</summary>
    private static IEnumerable<(string Where, int Layer, Fight Fight)> AllFights()
    {
        foreach (var table in LayerEncounters.All)
        {
            var pools = new (string Name, IReadOnlyList<Encounter> Pool)[]
            {
                ("normal", table.Normal),
                ("elite", table.Elite),
                ("boss", table.Boss),
            };
            foreach (var (name, pool) in pools)
            {
                foreach (var encounter in pool)
                {
                    foreach (var fight in encounter.Fights)
                        yield return ($"layer {table.Layer} {name} {encounter}", table.Layer, fight);
                }
            }
        }
    }

    [Test]
    public void EveryEnemyIdInTheTable_ResolvesInBattleCore()
    {
        var unknown = new List<string>();
        foreach (var (where, _, fight) in AllFights())
        {
            foreach (var id in fight.EnemyIds)
            {
                try
                {
                    Enemies.ById(id);
                }
                catch (KeyNotFoundException)
                {
                    unknown.Add($"{where}: {id}");
                }
            }
        }
        Assert.That(unknown, Is.Empty, "ids in DungeonCore.LayerEncounters that BattleCore.Enemies does not know");
    }

    [Test]
    public void TheTableUsesEveryRosterEnemy_AndNoOther()
    {
        var used = AllFights().SelectMany(f => f.Fight.EnemyIds).Distinct().OrderBy(id => id, StringComparer.Ordinal);
        Assert.That(used, Is.EqualTo(RosterSizes.Keys.OrderBy(id => id, StringComparer.Ordinal)));
    }

    [Test]
    public void EachFight_IsFoughtOnFiveCellsPlusTheEnemiesSizes()
    {
        foreach (var (where, layer, fight) in AllFights())
        {
            int expected = CellsBeforeTheEnemies + fight.EnemyIds.Sum(id => RosterSizes[id]);
            var battle = new ChainBattle(fight.EnemyIds, layer);
            Assert.That(battle.FieldCells, Is.EqualTo(expected), where);
        }
    }
}
