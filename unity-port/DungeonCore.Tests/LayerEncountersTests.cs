using DungeonCore;

namespace DungeonCore.Tests
{
    public class LayerEncountersTests
    {
        // enemy_roster_v4.md §2〜§6 headers: the layers each enemy is placed on.
        private static readonly Dictionary<string, (int From, int To)> RosterLayers = new()
        {
            ["polearm_warped"] = (1, 2),
            ["shadow_hound"] = (1, 2),
            ["rusted_revenant"] = (2, 4),
            ["crossbow_hunter"] = (2, 3),
            ["mist_archer"] = (4, 6),
            ["twin_blade_warped"] = (4, 7),
            ["polearm_crystal"] = (3, 4),
            ["armored_warden"] = (4, 5),
            ["pack_alpha"] = (5, 7),
            ["polearm_unyielding"] = (5, 6),
            ["miasma_priest"] = (3, 3),
            ["abyss_angler"] = (6, 6),
            ["distortion_root"] = (7, 7),
        };

        // enemy_roster_v4.md §0 「大きさ」, as BattleCore.Enemies sets it.
        private static readonly Dictionary<string, int> Sizes = new()
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

        private const string ThePair = "polearm_warped+crossbow_hunter";
        private static readonly int[] PairLayers = { 2, 3 };

        // enemy_roster_v4.md §9, one row per layer, one array per column.
        private static readonly string[][] SectionNineNormal =
        {
            new[] { "polearm_warped", "shadow_hound" },
            new[] { "polearm_warped", "shadow_hound", "rusted_revenant", "crossbow_hunter" },
            new[] { "rusted_revenant", "crossbow_hunter", "polearm_crystal" },
            new[] { "rusted_revenant", "mist_archer", "twin_blade_warped", "polearm_crystal" },
            new[] { "mist_archer", "twin_blade_warped" },
            new[] { "mist_archer", "twin_blade_warped" },
            new[] { "twin_blade_warped" },
        };

        private static readonly string[][] SectionNinePair =
        {
            Array.Empty<string>(),
            new[] { ThePair },
            new[] { ThePair },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
        };

        private static readonly string[][] SectionNineElite =
        {
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "armored_warden" },
            new[] { "armored_warden", "pack_alpha", "polearm_unyielding" },
            new[] { "pack_alpha", "polearm_unyielding" },
            new[] { "pack_alpha" },
        };

        private static readonly string[][] SectionNineBoss =
        {
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "miasma_priest" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "abyss_angler" },
            new[] { "distortion_root" },
        };

        // seven_layers_v4.md §2.4: field cells per node kind.
        private static readonly int[] NormalCells = { 6, 6, 6, 7, 7, 7, 8 };
        private const int BossCells = 8;

        // BattleCore Constants.PlayerStartCell (2) + Constants.StartGap (3), as ChainBattle.FieldCells adds them.
        private const int CellsBeforeTheEnemies = 5;

        private static readonly NodeKind[] CombatKinds = { NodeKind.Battle, NodeKind.Elite, NodeKind.Boss };

        [Test]
        public void ThereIsOneTablePerLayer()
        {
            Assert.That(LayerEncounters.All.Count, Is.EqualTo(SevenLayers.Count));
            for (int layer = 1; layer <= SevenLayers.Count; layer++)
            {
                Assert.That(LayerEncounters.Of(layer).Layer, Is.EqualTo(layer));
            }
        }

        [Test]
        public void AskingForALayerOutsideTheLairThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LayerEncounters.Of(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayerEncounters.Of(8));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayerEncounters.Pick(NodeKind.Battle, 0, new SeededRng(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayerEncounters.Pick(NodeKind.Battle, 8, new SeededRng(1)));
        }

        [Test]
        public void TheTableMatchesTheDesignDoc()
        {
            // encounters_and_rewards_v4.md §2.1. "," = fought back to back, "+" = on the field together.
            var expected = new[]
            {
                ("polearm_warped | shadow_hound", "polearm_warped,shadow_hound", "shadow_hound"),
                ("polearm_warped | shadow_hound | rusted_revenant | crossbow_hunter", ThePair, "rusted_revenant"),
                ("rusted_revenant | crossbow_hunter | polearm_crystal", ThePair, "miasma_priest"),
                (
                    "rusted_revenant | mist_archer | twin_blade_warped | polearm_crystal",
                    "rusted_revenant,mist_archer | rusted_revenant,twin_blade_warped | rusted_revenant,polearm_crystal | " +
                    "mist_archer,rusted_revenant | mist_archer,twin_blade_warped | mist_archer,polearm_crystal | " +
                    "twin_blade_warped,rusted_revenant | twin_blade_warped,mist_archer | twin_blade_warped,polearm_crystal | " +
                    "polearm_crystal,rusted_revenant | polearm_crystal,mist_archer | polearm_crystal,twin_blade_warped",
                    "armored_warden"
                ),
                ("mist_archer | twin_blade_warped", "armored_warden | pack_alpha", "polearm_unyielding"),
                ("mist_archer | twin_blade_warped", "pack_alpha | polearm_unyielding", "abyss_angler"),
                ("twin_blade_warped", "pack_alpha", "distortion_root"),
            };

            for (int layer = 1; layer <= SevenLayers.Count; layer++)
            {
                var table = LayerEncounters.Of(layer);
                var (normal, elite, boss) = expected[layer - 1];
                Assert.That(Render(table.Normal), Is.EqualTo(normal), $"layer {layer} normal");
                Assert.That(Render(table.Elite), Is.EqualTo(elite), $"layer {layer} elite");
                Assert.That(Render(table.Boss), Is.EqualTo(boss), $"layer {layer} boss");
            }
        }

        [Test]
        public void EveryCombatPoolHoldsSomething()
        {
            foreach (var table in LayerEncounters.All)
            {
                foreach (var kind in CombatKinds)
                {
                    Assert.That(table.PoolFor(kind), Is.Not.Empty, $"layer {table.Layer} {kind}");
                }
            }
        }

        [Test]
        public void NormalFightsAreNeverStandIns()
        {
            // §9 lists the normals of every layer, so nothing in the normal pools is borrowed.
            foreach (var table in LayerEncounters.All)
            {
                Assert.That(table.Normal.All(e => !e.IsStandIn), Is.True, $"layer {table.Layer}");
                Assert.That(table.Normal.All(e => e.Fights.Count == 1 && e.Fights[0].EnemyIds.Count == 1), Is.True,
                    $"layer {table.Layer}: a normal node is one enemy");
            }
        }

        [Test]
        public void BossesAreRealOnlyOnLayersThreeSixAndSeven()
        {
            // §9 places a boss on layers 3, 6 and 7 only; the rest borrow one (§2.2).
            var realBossLayers = new[] { 3, 6, 7 };
            foreach (var table in LayerEncounters.All)
            {
                bool real = realBossLayers.Contains(table.Layer);
                Assert.That(table.Boss.All(e => e.IsStandIn == !real), Is.True, $"layer {table.Layer}");
            }
        }

        [Test]
        public void ElitesAreRealOnlyOnLayersFiveToSeven()
        {
            // Layers 1〜3 have no §9 elite; layer 4's only one (門竜) is moved up to the empty boss
            // slot, so its elite slot is a stand-in too.
            foreach (var table in LayerEncounters.All)
            {
                bool real = table.Layer >= 5;
                Assert.That(table.Elite.All(e => e.IsStandIn == !real), Is.True, $"layer {table.Layer}");
            }
        }

        [Test]
        public void ThePairAppearsOnlyInTheEliteSlotOfLayersTwoAndThree()
        {
            foreach (var table in LayerEncounters.All)
            {
                foreach (var kind in CombatKinds)
                {
                    foreach (var encounter in table.PoolFor(kind))
                    {
                        foreach (var fight in encounter.Fights.Where(f => f.EnemyIds.Count > 1))
                        {
                            Assert.That(fight.ToString(), Is.EqualTo(ThePair), $"layer {table.Layer} {kind}");
                            Assert.That(kind, Is.EqualTo(NodeKind.Elite), $"layer {table.Layer}");
                            Assert.That(PairLayers, Does.Contain(table.Layer), $"layer {table.Layer}");
                        }
                    }
                }
            }

            foreach (int layer in PairLayers)
            {
                Assert.That(LayerEncounters.Of(layer).Elite.Any(e => e.ToString() == ThePair), Is.True, $"layer {layer}");
            }
        }

        [Test]
        public void EveryEnemyIsOnARosterLayer()
        {
            // The table never redefines layers: each lone enemy sits inside its roster §2〜§6 range.
            // The pair's 錆槍 on layer 3 is §9's pair column; the test below checks pairs as a whole.
            foreach (var table in LayerEncounters.All)
            {
                foreach (var kind in CombatKinds)
                {
                    foreach (var fight in table.PoolFor(kind).SelectMany(e => e.Fights))
                    {
                        foreach (string id in fight.EnemyIds)
                        {
                            Assert.That(RosterLayers.ContainsKey(id), Is.True, $"unknown enemy id {id}");
                        }

                        if (fight.EnemyIds.Count > 1) continue;

                        string lone = fight.EnemyIds[0];
                        var (from, to) = RosterLayers[lone];
                        Assert.That(table.Layer, Is.InRange(from, to), $"{lone} on layer {table.Layer}");
                    }
                }
            }
        }

        [Test]
        public void EveryEnemyComesFromTheRightSectionNineColumn()
        {
            // A real entry comes from §9's column for its slot: normals from 通常, real elites from
            // 精鋭, real bosses from ボス. A stand-in may borrow from any column of its layer.
            foreach (var table in LayerEncounters.All)
            {
                int row = table.Layer - 1;
                var anyColumn = SectionNineNormal[row]
                    .Concat(SectionNinePair[row])
                    .Concat(SectionNineElite[row])
                    .Concat(SectionNineBoss[row])
                    .ToArray();

                foreach (var kind in CombatKinds)
                {
                    string[] ownColumn = kind switch
                    {
                        NodeKind.Battle => SectionNineNormal[row],
                        NodeKind.Elite => SectionNineElite[row],
                        _ => SectionNineBoss[row],
                    };

                    foreach (var encounter in table.PoolFor(kind))
                    {
                        var allowed = encounter.IsStandIn ? anyColumn : ownColumn;
                        foreach (var fight in encounter.Fights)
                        {
                            Assert.That(allowed, Does.Contain(fight.ToString()),
                                $"layer {table.Layer} {kind}: {fight} (stand-in: {encounter.IsStandIn})");
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryRosterEnemyIsUsedSomewhere()
        {
            var used = LayerEncounters.All
                .SelectMany(t => CombatKinds.SelectMany(t.PoolFor))
                .SelectMany(e => e.EnemyIds)
                .ToHashSet();
            Assert.That(used, Is.EquivalentTo(RosterLayers.Keys));
        }

        [Test]
        public void EveryFightFitsTheFieldOfItsNode()
        {
            // seven_layers_v4.md §2.4: a fight needs 5 + the enemies' summed size cells, and the
            // node's field must never be narrower than that.
            foreach (var table in LayerEncounters.All)
            {
                foreach (var kind in CombatKinds)
                {
                    int cells = FieldCells(kind, table.Layer);
                    foreach (var fight in table.PoolFor(kind).SelectMany(e => e.Fights))
                    {
                        int needed = CellsBeforeTheEnemies + fight.EnemyIds.Sum(id => Sizes[id]);
                        Assert.That(needed, Is.LessThanOrEqualTo(cells), $"layer {table.Layer} {kind}: {fight}");
                    }
                }
            }
        }

        [Test]
        public void NodesWithoutAFightReturnNull()
        {
            var rng = new SeededRng(5);
            foreach (var kind in new[] { NodeKind.Rest, NodeKind.Survey, NodeKind.Event, NodeKind.Carving, NodeKind.Trace })
            {
                for (int layer = 1; layer <= SevenLayers.Count; layer++)
                {
                    Assert.That(LayerEncounters.Pick(kind, layer, rng), Is.Null, $"layer {layer} {kind}");
                }
            }
        }

        [Test]
        public void PickDrawsFromTheNodesPool()
        {
            var rng = new SeededRng(11);
            for (int layer = 1; layer <= SevenLayers.Count; layer++)
            {
                var table = LayerEncounters.Of(layer);
                foreach (var kind in CombatKinds)
                {
                    for (int i = 0; i < 50; i++)
                    {
                        var picked = LayerEncounters.Pick(kind, layer, rng);
                        Assert.That(picked, Is.Not.Null);
                        Assert.That(table.PoolFor(kind), Does.Contain(picked), $"layer {layer} {kind}");
                    }
                }
            }
        }

        [Test]
        public void PickReachesEveryEntryOfAPool()
        {
            // Layer 4's elite pool is the largest (twelve ordered chains); a uniform draw should hit them all.
            var rng = new SeededRng(21);
            var pool = LayerEncounters.Of(4).Elite;
            var seen = new HashSet<Encounter>();
            for (int i = 0; i < 300; i++) seen.Add(LayerEncounters.Pick(NodeKind.Elite, 4, rng)!);
            Assert.That(seen.Count, Is.EqualTo(pool.Count));
        }

        [Test]
        public void PickCoversBothEndsOfThePool()
        {
            var pool = LayerEncounters.Of(2).Normal;
            Assert.That(LayerEncounters.Pick(NodeKind.Battle, 2, new FixedRng(0.0)), Is.SameAs(pool[0]));
            Assert.That(LayerEncounters.Pick(NodeKind.Battle, 2, new FixedRng(1.0)), Is.SameAs(pool[pool.Count - 1]));
        }

        [Test]
        public void LayerFourChainsPutEveryEnemyFirstAndSecondEquallyOften()
        {
            // The chain carries HP and stamina over, so who is fought second matters; the pool
            // lists both orders of every pair.
            var chains = LayerEncounters.Of(4).Elite;
            var first = chains.GroupBy(e => e.Fights[0].ToString()).ToDictionary(g => g.Key, g => g.Count());
            var second = chains.GroupBy(e => e.Fights[1].ToString()).ToDictionary(g => g.Key, g => g.Count());
            Assert.That(chains.Count, Is.EqualTo(12));
            Assert.That(first.Values, Is.All.EqualTo(3));
            Assert.That(second.Values, Is.All.EqualTo(3));
            Assert.That(chains.All(e => e.Fights[0].ToString() != e.Fights[1].ToString()), Is.True);
        }

        [Test]
        public void PickDrawsOnceForAFightAndNeverForTheRest()
        {
            // Map generation may share this rng: an extra or missing draw would shift every later one.
            for (int layer = 1; layer <= SevenLayers.Count; layer++)
            {
                foreach (var kind in CombatKinds)
                {
                    var rng = new CountingRng(new SeededRng(7));
                    LayerEncounters.Pick(kind, layer, rng);
                    Assert.That(rng.Draws, Is.EqualTo(1), $"layer {layer} {kind}");
                }

                foreach (var kind in new[] { NodeKind.Rest, NodeKind.Survey, NodeKind.Event, NodeKind.Carving, NodeKind.Trace })
                {
                    var rng = new CountingRng(new SeededRng(7));
                    LayerEncounters.Pick(kind, layer, rng);
                    Assert.That(rng.Draws, Is.EqualTo(0), $"layer {layer} {kind}");
                }
            }
        }

        [Test]
        public void EncountersCompareByValue()
        {
            var a = new Encounter(new[] { new Fight(new[] { "polearm_warped", "crossbow_hunter" }) }, isStandIn: true);
            var b = new Encounter(new[] { new Fight(new[] { "polearm_warped", "crossbow_hunter" }) }, isStandIn: true);
            var hound = new Fight(new[] { "shadow_hound" });
            var otherHound = new Fight(new[] { "shadow_hound" });
            Assert.That(hound, Is.EqualTo(otherHound));
            Assert.That(hound.GetHashCode(), Is.EqualTo(otherHound.GetHashCode()));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));

            var reversed = new Encounter(new[] { new Fight(new[] { "crossbow_hunter", "polearm_warped" }) }, isStandIn: true);
            var real = new Encounter(new[] { new Fight(new[] { "polearm_warped", "crossbow_hunter" }) }, isStandIn: false);
            Assert.That(a, Is.Not.EqualTo(reversed));
            Assert.That(a, Is.Not.EqualTo(real));

            // The layer-3 boss drawn from the table equals one built from scratch.
            var serk = new Encounter(new[] { new Fight(new[] { "miasma_priest" }) }, isStandIn: false);
            Assert.That(LayerEncounters.Pick(NodeKind.Boss, 3, new SeededRng(1)), Is.EqualTo(serk));
        }

        [Test]
        public void TheTablesCannotBeReplacedFromOutside()
        {
            var all = (IList<LayerEncounterTable>)LayerEncounters.All;
            Assert.Throws<NotSupportedException>(() => all[0] = LayerEncounters.Of(2));
            Assert.That(LayerEncounters.All, Is.Not.InstanceOf<LayerEncounterTable[]>());
        }

        [Test]
        public void SameSeed_SamePicks()
        {
            var first = PickSequence(42UL);
            var second = PickSequence(42UL);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentPicks()
        {
            Assert.That(PickSequence(42UL), Is.Not.EqualTo(PickSequence(43UL)));
        }

        [Test]
        public void PickNeedsAnRng()
        {
            Assert.Throws<ArgumentNullException>(() => LayerEncounters.Pick(NodeKind.Battle, 1, null!));
        }

        [Test]
        public void EncountersRejectEmptyShapes()
        {
            Assert.Throws<ArgumentException>(() => new Fight(Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new Fight(new[] { "" }));
            Assert.Throws<ArgumentException>(() => new Encounter(Array.Empty<Fight>(), isStandIn: false));
        }

        private static int FieldCells(NodeKind kind, int layer) => kind switch
        {
            NodeKind.Battle => NormalCells[layer - 1],
            NodeKind.Elite => Math.Min(NormalCells[layer - 1] + 1, BossCells),
            NodeKind.Boss => BossCells,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        private static string Render(IReadOnlyList<Encounter> pool) => string.Join(" | ", pool.Select(e => e.ToString()));

        /// <summary>Counts how often the wrapped rng is drawn from.</summary>
        private sealed class CountingRng : IRng
        {
            private readonly IRng _inner;

            public CountingRng(IRng inner)
            {
                _inner = inner;
            }

            public int Draws { get; private set; }

            public double NextDouble()
            {
                Draws++;
                return _inner.NextDouble();
            }
        }

        /// <summary>Over 100 draws across every layer and combat kind, rendered so sequences compare by value.</summary>
        private static List<string> PickSequence(ulong seed)
        {
            var rng = new SeededRng(seed);
            var picks = new List<string>();
            for (int i = 0; i < 120; i++)
            {
                int layer = 1 + i % SevenLayers.Count;
                var kind = CombatKinds[i % CombatKinds.Length];
                picks.Add(LayerEncounters.Pick(kind, layer, rng)!.ToString());
            }
            return picks;
        }
    }
}
