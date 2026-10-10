using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// One battle: the enemies fought at once, in phase order. One id for a single enemy, two for
    /// roster §9's pair (錆槍 in front, 灰弩 one cell behind).
    /// </summary>
    public sealed record Fight
    {
        public Fight(IReadOnlyList<string> enemyIds)
        {
            if (enemyIds == null) throw new ArgumentNullException(nameof(enemyIds));
            if (enemyIds.Count == 0) throw new ArgumentException("a fight needs at least one enemy", nameof(enemyIds));
            if (enemyIds.Any(string.IsNullOrEmpty)) throw new ArgumentException("an enemy id is empty", nameof(enemyIds));
            EnemyIds = new ReadOnlyCollection<string>(enemyIds.ToArray());
        }

        /// <summary>Enemy ids as BattleCore.Enemies knows them, in phase order.</summary>
        public IReadOnlyList<string> EnemyIds { get; }

        /// <summary>"polearm_warped+crossbow_hunter" — the same joiner as BattleCore's -chain switch.</summary>
        public override string ToString() => string.Join("+", EnemyIds);

        /// <summary>
        /// Value equality over the ids, in order. The compiler's record equality would compare the
        /// list by reference, so two fights with the same enemies would not be equal.
        /// </summary>
        public bool Equals(Fight? other) =>
            other is not null && EnemyIds.SequenceEqual(other.EnemyIds, StringComparer.Ordinal);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (string id in EnemyIds) hash.Add(id, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// What one combat node holds: a single fight, or two fought back to back with HP and stamina
    /// carried over (the 2-fight elite stand-in). The node costs one 刻限 either way.
    /// </summary>
    public sealed record Encounter
    {
        public Encounter(IReadOnlyList<Fight> fights, bool isStandIn)
        {
            if (fights == null) throw new ArgumentNullException(nameof(fights));
            if (fights.Count == 0) throw new ArgumentException("an encounter needs at least one fight", nameof(fights));
            if (fights.Any(f => f == null)) throw new ArgumentException("a fight is null", nameof(fights));
            Fights = new ReadOnlyCollection<Fight>(fights.ToArray());
            IsStandIn = isStandIn;
        }

        /// <summary>The fights in the order they are fought.</summary>
        public IReadOnlyList<Fight> Fights { get; }

        /// <summary>
        /// True when the enemy in this slot is not the one roster §9 puts in that column on this
        /// layer, and the table borrows it from the same layer instead
        /// (encounters_and_rewards_v4.md §2.2). That covers an empty §9 slot filled from another
        /// column, and a §9 elite moved up to an empty boss slot (門竜 on layer 4, 不退 on layer 5).
        /// Cards replaces these once §9 fills the slot.
        /// </summary>
        public bool IsStandIn { get; }

        /// <summary>Every enemy id of every fight, in order.</summary>
        public IEnumerable<string> EnemyIds => Fights.SelectMany(f => f.EnemyIds);

        /// <summary>"polearm_warped,shadow_hound" — fights joined the way the -chain switch joins them.</summary>
        public override string ToString() => string.Join(",", Fights);

        /// <summary>Value equality over the fights, in order, and the stand-in flag.</summary>
        public bool Equals(Encounter? other) =>
            other is not null && IsStandIn == other.IsStandIn && Fights.SequenceEqual(other.Fights);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var fight in Fights) hash.Add(fight);
            hash.Add(IsStandIn);
            return hash.ToHashCode();
        }
    }

    /// <summary>One layer's pools. Each pool is drawn from uniformly.</summary>
    public sealed class LayerEncounterTable
    {
        public LayerEncounterTable(int layer, IReadOnlyList<Encounter> normal, IReadOnlyList<Encounter> elite, IReadOnlyList<Encounter> boss)
        {
            if (layer < 1 || layer > SevenLayers.Count) throw new ArgumentOutOfRangeException(nameof(layer));
            Layer = layer;
            Normal = Freeze(normal, nameof(normal));
            Elite = Freeze(elite, nameof(elite));
            Boss = Freeze(boss, nameof(boss));
        }

        public int Layer { get; }

        /// <summary>通常戦闘. Also what a 死亡地点の痕跡 would replace, once #106 fills it.</summary>
        public IReadOnlyList<Encounter> Normal { get; }

        /// <summary>精鋭.</summary>
        public IReadOnlyList<Encounter> Elite { get; }

        /// <summary>階層ボス.</summary>
        public IReadOnlyList<Encounter> Boss { get; }

        /// <summary>The pool a node kind draws from, or an empty list for a kind that holds no fight.</summary>
        public IReadOnlyList<Encounter> PoolFor(NodeKind kind) => kind switch
        {
            NodeKind.Battle => Normal,
            NodeKind.Elite => Elite,
            NodeKind.Boss => Boss,
            _ => Array.Empty<Encounter>(),
        };

        private static IReadOnlyList<Encounter> Freeze(IReadOnlyList<Encounter> pool, string name)
        {
            if (pool == null) throw new ArgumentNullException(name);
            if (pool.Count == 0) throw new ArgumentException("a pool needs at least one encounter", name);
            return new ReadOnlyCollection<Encounter>(pool.ToArray());
        }
    }

    /// <summary>
    /// Who is fought on each combat node of each layer
    /// (`.claude/docs/danjeon_document/encounters_and_rewards_v4.md` §2, Issue #102).
    ///
    /// The enemies and the layers they may appear on come from `enemy_document/enemy_roster_v4.md`
    /// §9 (cards lane). This table never moves an enemy to a layer §9 does not put it on; where §9
    /// leaves an elite or boss slot empty, the slot borrows an enemy from the same layer and is
    /// flagged <see cref="Encounter.IsStandIn"/>. On layers 4 and 5 that borrowing moves a §9 elite
    /// into the boss slot. Enemy ids are plain strings that match BattleCore.Enemies (roster §1.8)
    /// — DungeonCore does not reference BattleCore (#97).
    /// </summary>
    public static class LayerEncounters
    {
        // roster §2: normals.
        private const string PolearmWarped = "polearm_warped";        // 錆槍の竜兵, layers 1〜2
        private const string ShadowHound = "shadow_hound";            // 瘴牙の走竜, layers 1〜2
        private const string RustedRevenant = "rusted_revenant";      // 瘴甲の竜兵, layers 2〜4
        private const string CrossbowHunter = "crossbow_hunter";      // 灰弩の竜兵, layers 2〜3
        private const string MistArcher = "mist_archer";              // 燐弓の竜兵, layers 4〜6
        private const string TwinBladeWarped = "twin_blade_warped";   // 燐刃の竜兵, layers 4〜7
        private const string PolearmCrystal = "polearm_crystal";      // 晶槍の竜兵, layers 3〜4

        // roster §3: elites.
        private const string ArmoredWarden = "armored_warden";        // 鉄壁の門竜, layers 4〜5
        private const string PackAlpha = "pack_alpha";                // 統牙の長竜, layers 5〜7
        private const string PolearmUnyielding = "polearm_unyielding"; // 不退の槍竜, layers 5〜6

        // roster §4〜§6: bosses.
        private const string MiasmaPriest = "miasma_priest";          // 大黒蛇 セルク, layer 3
        private const string AbyssAngler = "abyss_angler";            // 獄竜 ガルド, layer 6
        private const string DistortionRoot = "distortion_root";      // 歪みの根, layer 7

        private static readonly IReadOnlyList<LayerEncounterTable> Tables = Array.AsReadOnly(BuildTables());

        /// <summary>The seven tables, layer 1 first. Read-only: callers cannot swap a layer out.</summary>
        public static IReadOnlyList<LayerEncounterTable> All => Tables;

        public static LayerEncounterTable Of(int layer)
        {
            if (layer < 1 || layer > SevenLayers.Count)
                throw new ArgumentOutOfRangeException(nameof(layer), layer, $"the lair has {SevenLayers.Count} layers");
            return Tables[layer - 1];
        }

        /// <summary>
        /// The encounter behind a node of this kind on this layer, drawn uniformly from its pool
        /// with one rng draw. Null for kinds that hold no fight: rest, survey, event and carving.
        /// A trace is null as well until #106 decides what it holds.
        /// </summary>
        public static Encounter? Pick(NodeKind kind, int layer, IRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var pool = Of(layer).PoolFor(kind);
            if (pool.Count == 0) return null;
            return pool[rng.NextInt(0, pool.Count)];
        }

        private static LayerEncounterTable[] BuildTables() => new[]
        {
            // Layer 1. §9 has no elite and no boss here: the elite is the two normals back to back,
            // the boss the sturdier normal (瘴牙 80 HP over 錆槍 60).
            new LayerEncounterTable(1,
                Singles(PolearmWarped, ShadowHound),
                new[] { Chain(standIn: true, PolearmWarped, ShadowHound) },
                new[] { Single(ShadowHound, standIn: true) }),

            // Layer 2. The pair fills the elite slot: 141 HP over two cells, needing 7 cells — the
            // elite field of the layer. The boss is the sturdiest normal (瘴甲 87 HP).
            new LayerEncounterTable(2,
                Singles(PolearmWarped, ShadowHound, RustedRevenant, CrossbowHunter),
                new[] { Pair(standIn: true, PolearmWarped, CrossbowHunter) },
                new[] { Single(RustedRevenant, standIn: true) }),

            // Layer 3. The pair again in the elite slot; セルク is the real boss.
            new LayerEncounterTable(3,
                Singles(RustedRevenant, CrossbowHunter, PolearmCrystal),
                new[] { Pair(standIn: true, PolearmWarped, CrossbowHunter) },
                new[] { Single(MiasmaPriest, standIn: false) }),

            // Layer 4. §9's only elite (門竜) takes the boss slot, so the elite slot becomes two
            // different normals back to back. Every ordered pair is listed, so which one comes
            // second (with HP and stamina already spent) is as random as which two are drawn.
            new LayerEncounterTable(4,
                Singles(RustedRevenant, MistArcher, TwinBladeWarped, PolearmCrystal),
                DistinctChains(RustedRevenant, MistArcher, TwinBladeWarped, PolearmCrystal),
                new[] { Single(ArmoredWarden, standIn: true) }),

            // Layer 5. §9 lists three elites. 不退 — where the three spear dragoons end up — is the
            // boss, so a node's draw never depends on another node's.
            new LayerEncounterTable(5,
                Singles(MistArcher, TwinBladeWarped),
                new[] { Single(ArmoredWarden, standIn: false), Single(PackAlpha, standIn: false) },
                new[] { Single(PolearmUnyielding, standIn: true) }),

            new LayerEncounterTable(6,
                Singles(MistArcher, TwinBladeWarped),
                new[] { Single(PackAlpha, standIn: false), Single(PolearmUnyielding, standIn: false) },
                new[] { Single(AbyssAngler, standIn: false) }),

            new LayerEncounterTable(7,
                Singles(TwinBladeWarped),
                new[] { Single(PackAlpha, standIn: false) },
                new[] { Single(DistortionRoot, standIn: false) }),
        };

        private static Encounter Single(string id, bool standIn) =>
            new Encounter(new[] { new Fight(new[] { id }) }, standIn);

        /// <summary>Normal fights: never stand-ins, since §9 lists every normal per layer.</summary>
        private static Encounter[] Singles(params string[] ids) =>
            ids.Select(id => Single(id, standIn: false)).ToArray();

        /// <summary>Two enemies on the field at once.</summary>
        private static Encounter Pair(bool standIn, string front, string back) =>
            new Encounter(new[] { new Fight(new[] { front, back }) }, standIn);

        /// <summary>Single fights back to back, with no rest between them.</summary>
        private static Encounter Chain(bool standIn, params string[] ids) =>
            new Encounter(ids.Select(id => new Fight(new[] { id })).ToArray(), standIn);

        /// <summary>
        /// Every 2-fight chain of two different enemies, in both orders: n × (n − 1) chains, so one
        /// uniform draw picks the pair and the order alike.
        /// </summary>
        private static Encounter[] DistinctChains(params string[] ids)
        {
            var chains = new List<Encounter>();
            for (int i = 0; i < ids.Length; i++)
            {
                for (int j = 0; j < ids.Length; j++)
                {
                    if (i == j) continue;
                    chains.Add(Chain(standIn: true, ids[i], ids[j]));
                }
            }
            return chains.ToArray();
        }
    }
}
