// Suite 6 (dungeon): maps, reducer walks, encounter picks and the exploration core's tables.
using DungeonContent;
using DungeonCore;
using Exploration;

namespace Oracle;

public static class DungeonSuite
{
    public const int MapSeeds = 20;
    public const int WalkSeeds = 3;
    public const int EncounterSeeds = 20;
    public const int WalkMaxOps = 60;

    public static int Run(string outDir)
    {
        using var file = new SuiteFile(outDir, "dungeon", "LayerMap", "LayerProfile", "LayerMapSpec");

        // ---- maps: every layer × seeds 1..20, and the two stock specs ----
        for (int layer = 1; layer <= SevenLayers.Count; layer++)
        {
            for (int seed = 1; seed <= MapSeeds; seed++)
            {
                int l = layer;
                ulong s = (ulong)seed;
                file.Add($"map:L{l}:s{seed}", new OrderedMap { ["Layer"] = l, ["Seed"] = s }, () =>
                {
                    var profile = SevenLayers.Of(l);
                    var map = profile.Map(s);
                    return new OrderedMap
                    {
                        ["Map"] = map,
                        ["Validate"] = Tables.Try(() => { MapValidator.Validate(map, profile.MapSpec); return "ok"; }),
                        ["Rows"] = Enumerable.Range(0, map.RowCount).Select(r => map.Row(r).Select(n => n.Id).ToList()).ToList(),
                        ["Neighbours"] = map.Nodes.Select(n => map.Neighbours(n.Id)).ToList(),
                        ["Predecessors"] = map.Nodes.Select(n => map.Predecessors(n.Id)).ToList(),
                    };
                });
            }
        }
        foreach (var (name, make) in new (string, Func<LayerMapSpec>)[] { ("Minimal", () => LayerMapSpec.Minimal(1)), ("Full", () => LayerMapSpec.Full(1)) })
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                ulong s = (ulong)seed;
                file.Add($"mapspec:{name}:s{seed}", new OrderedMap { ["Spec"] = name, ["Seed"] = s }, () =>
                {
                    var spec = make();
                    return new OrderedMap { ["Spec"] = spec, ["Map"] = Tables.Try(() => MapGenerator.Generate(spec, s)) };
                });
            }
        }

        // ---- reducer walks ----
        for (int layer = 1; layer <= SevenLayers.Count; layer++)
        {
            for (int seed = 1; seed <= WalkSeeds; seed++)
            {
                foreach (bool leftmost in new[] { true, false })
                {
                    var (ops, results) = Walk(layer, seed, leftmost);
                    string id = $"walk:L{layer}:s{seed}:{(leftmost ? "leftmost" : "rightmost")}";
                    var input = new OrderedMap { ["Layer"] = layer, ["Seed"] = seed, ["Policy"] = leftmost ? "leftmost" : "rightmost", ["Ops"] = ops };
                    file.Add(id, input, () => results);
                }
            }
        }

        // ---- encounter picks: layer × node kind × seeds 1..20 ----
        for (int layer = 1; layer <= SevenLayers.Count; layer++)
        {
            foreach (NodeKind kind in Enum.GetValues(typeof(NodeKind)))
            {
                int l = layer;
                var k = kind;
                file.Add($"encounter:L{l}:{k}", new OrderedMap { ["Layer"] = l, ["Kind"] = k, ["Seeds"] = EncounterSeeds }, () =>
                {
                    var picks = new List<object>();
                    for (int seed = 1; seed <= EncounterSeeds; seed++)
                    {
                        ulong s = (ulong)seed;
                        picks.Add(Tables.Try(() => LayerEncounters.Pick(k, l, new SeededRng(s))));
                    }
                    var cellsFor = new List<object>();
                    for (int sizes = 1; sizes <= 4; sizes++)
                    {
                        int z = sizes;
                        cellsFor.Add(Tables.Try(() => BattleField.CellsFor(l, k, z)));
                    }
                    return new OrderedMap
                    {
                        ["Picks"] = picks,
                        ["Pool"] = LayerEncounters.Of(l).PoolFor(k),
                        ["Cells"] = Tables.Try(() => BattleField.Cells(l, k)),
                        ["CellsFor1to4"] = cellsFor,
                        ["Token"] = k.ToToken(),
                        ["IsDetour"] = k.IsDetour(),
                        ["IsCombat"] = k.IsCombat(),
                    };
                });
            }
        }
        file.Add("table:LayerEncounters", new OrderedMap { ["Type"] = "LayerEncounters" }, () => new OrderedMap
        {
            ["All"] = LayerEncounters.All,
            ["OfOutOfRange"] = Tables.Try(() => LayerEncounters.Of(8)),
        });

        // ---- tables ----
        file.Add("table:Miasma", new OrderedMap { ["Type"] = "Miasma" }, MiasmaTable);
        file.Add("table:SevenLayers", new OrderedMap { ["Type"] = "SevenLayers" }, () => new OrderedMap
        {
            ["Static"] = Tables.Static(typeof(SevenLayers)),
            ["DisplayName"] = SevenLayers.All.Select(p => new List<string> { p.DisplayName(false), p.DisplayName(true) }).ToList(),
            ["BareMinimumMiasma0to3"] = Enumerable.Range(0, 4).Select(r => SevenLayers.BareMinimumMiasma(r)).ToList(),
            ["FullSpendMiasma0to3"] = Enumerable.Range(0, 4).Select(r => SevenLayers.FullSpendMiasma(r)).ToList(),
            ["OfOutOfRange"] = Tables.Try(() => SevenLayers.Of(0)),
        });
        file.Add("table:BattleField", new OrderedMap { ["Type"] = "BattleField" }, () => Tables.Static(typeof(BattleField)));
        file.Add("table:ExplorationReducer", new OrderedMap { ["Type"] = "ExplorationReducer" }, () => new OrderedMap
        {
            ["Static"] = Tables.Static(typeof(ExplorationReducer)),
            ["RestHeal0to100"] = Enumerable.Range(0, 101).Select(ExplorationReducer.RestHeal).ToList(),
            ["InterludeHeal0to100"] = Enumerable.Range(0, 101).Select(ExplorationReducer.InterludeHeal).ToList(),
        });
        file.Add("table:ItemCatalogue", new OrderedMap { ["Type"] = "ItemCatalogue" }, () =>
        {
            var effects = new OrderedMap();
            foreach (ItemEffect e in Enum.GetValues(typeof(ItemEffect))) effects.Add(e, new OrderedMap { ["Token"] = e.ToToken(), ["Side"] = e.Side() });
            return new OrderedMap
            {
                ["Tools"] = ItemCatalogue.Tools,
                ["Consumables"] = ItemCatalogue.Consumables,
                ["All"] = ItemCatalogue.All.Select(i => i.Id).ToList(),
                ["ToString"] = ItemCatalogue.All.Select(i => i.ToString()).ToList(),
                ["Effects"] = effects,
                ["UnknownTool"] = Tables.Try(() => ItemCatalogue.Tool("nope")),
                ["ConsumableAsTool"] = Tables.Try(() => ItemCatalogue.Tool("yakuso")),
            };
        });
        file.Add("table:Loadout", new OrderedMap { ["Type"] = "Loadout" }, LoadoutTable);
        file.Add("table:RunLoadout", new OrderedMap { ["Type"] = "RunLoadout" }, () =>
        {
            var trial = RunStart.TrialLoadout();
            return new OrderedMap
            {
                ["Static"] = Tables.Static(typeof(RunLoadout)),
                ["Trial"] = trial,
                ["SpendGauge"] = Tables.Try(() => { var next = trial.Spend(0, out int relief); return new OrderedMap { ["Loadout"] = next, ["Relief"] = relief }; }),
                ["SpendBad"] = Tables.Try(() => trial.Spend(5, out _)),
                ["TooManyTools"] = Tables.Try(() => new RunLoadout(new[] { ReliefItem.Density("a", 1), ReliefItem.Density("b", 1), ReliefItem.Density("c", 1), ReliefItem.Density("d", 1) })),
                ["GaugeTool"] = Tables.Try(() => new RunLoadout(new[] { ReliefItem.Gauge("a", 1) })),
                ["SurvivorSlots"] = new RunLoadout(new[] { ReliefItem.Density("a", 1), ReliefItem.Density("b", 2), ReliefItem.Density("c", 1), ReliefItem.Density("d", 1) }, null, RunLoadout.SurvivorBonusToolSlots),
            };
        });
        file.Add("table:Seeds", new OrderedMap { ["Type"] = "Seeds" }, () =>
        {
            var layerSeeds = new List<object>();
            var encounterSeeds = new List<object>();
            var rewardSeeds = new List<object>();
            foreach (ulong run in new ulong[] { 1, 2, 3, 20260921, ulong.MaxValue })
            {
                for (int layer = 1; layer <= SevenLayers.Count; layer++)
                {
                    layerSeeds.Add(ExplorationSession.LayerSeed(run, layer));
                    for (int node = 0; node < 13; node += 4)
                    {
                        encounterSeeds.Add(ExplorationSession.EncounterSeed(run, layer, node));
                        rewardSeeds.Add(Exploration.Bridge.Expedition.RewardSeed(run, layer, node));
                    }
                }
            }
            var draws = new List<object>();
            foreach (ulong seed in new ulong[] { 0, 1, 2, 12345, ulong.MaxValue })
            {
                var rng = new SeededRng(seed);
                var row = new List<object>();
                for (int i = 0; i < 4; i++) row.Add(rng.NextUInt64());
                for (int i = 0; i < 4; i++) row.Add(rng.NextDouble());
                for (int i = 0; i < 4; i++) row.Add(rng.NextInt(0, 7));
                for (int i = 0; i < 4; i++) row.Add(rng.NextChance(0.5));
                var list = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                rng.Shuffle(list);
                row.Add(list);
                draws.Add(row);
            }
            return new OrderedMap
            {
                ["Runs"] = new ulong[] { 1, 2, 3, 20260921, ulong.MaxValue },
                ["LayerSeed"] = layerSeeds,
                ["EncounterSeed"] = encounterSeeds,
                ["RewardSeed"] = rewardSeeds,
                ["DungeonRng"] = draws,
            };
        });
        return file.Count;
    }

    private static object MiasmaTable()
    {
        var eff = new List<object>();
        for (int d = 0; d <= 8; d++) for (int r = -1; r <= 3; r++) eff.Add(new[] { d, r, Miasma.EffectiveDensity(d, r) });
        var acc = new List<object>();
        foreach (int p in new[] { -5, 0, 7, 50, 95, 99, 100, 120 }) foreach (int e in new[] { -1, 0, 1, 3, 7 }) acc.Add(new[] { p, e, Miasma.Accumulate(p, e), Miasma.Relieve(p, e) });
        var pen = new List<object>();
        for (int p = -5; p <= 105; p++) pen.Add(new object[] { p, Miasma.MaxStaminaPenalty(p), Miasma.IsLethal(p) });
        var max = new List<object>();
        for (int t = -6; t <= 6; t++) for (int p = 0; p <= 100; p += 5) max.Add(new[] { t, p, Miasma.MaxStamina(t, p) });
        return new OrderedMap
        {
            ["Static"] = Tables.Static(typeof(Miasma)),
            ["EffectiveDensity"] = eff,
            ["AccumulateRelieve"] = acc,
            ["PenaltyLethal"] = pen,
            ["MaxStamina"] = max,
            ["NegativeDensity"] = Tables.Try(() => Miasma.EffectiveDensity(-1, 0)),
        };
    }

    private static object LoadoutTable()
    {
        var full = Loadout.Of(new[] { "bosho_no_men", "kokugen_no_suna", "toshin_no_kakera" }, new[] { "yakuso", "joka_no_ko", "wasuremizu" });
        var has = new OrderedMap();
        foreach (ItemEffect e in Enum.GetValues(typeof(ItemEffect))) has.Add(e, full.Has(e));
        return new OrderedMap
        {
            ["Static"] = Tables.Static(typeof(Loadout)),
            ["Empty"] = Loadout.Empty,
            ["Full"] = full,
            ["Has"] = has,
            ["TwoMasks"] = Loadout.Of(new[] { "bosho_no_men", "bosho_no_men" }),
            ["Survivor"] = Loadout.Of(new[] { "bosho_no_men", "kokugen_no_suna", "toshin_no_kakera", "ikitsugi_no_fue" }, null, Loadout.SurvivorToolSlots),
            ["TooManyTools"] = Tables.Try(() => Loadout.Of(new[] { "bosho_no_men", "kokugen_no_suna", "toshin_no_kakera", "ikitsugi_no_fue" })),
            ["TooManyConsumables"] = Tables.Try(() => Loadout.Of(Array.Empty<string>(), new[] { "yakuso", "yakuso", "yakuso", "yakuso" })),
            ["TakeIntoFree"] = Tables.Try(() => Loadout.Empty.Take("yakuso")),
            ["TakeFullNoSlot"] = Tables.Try(() => full.Take("shirube_no_ishi")),
            ["TakeFullSlot1"] = Tables.Try(() => full.Take("shirube_no_ishi", 1)),
            ["Use1"] = Tables.Try(() => { var next = full.Use(1, out var used); return new OrderedMap { ["Loadout"] = next, ["Used"] = used }; }),
            ["UseBad"] = Tables.Try(() => Loadout.Empty.Use(0, out _)),
            ["Leave"] = full.LeaveTheDungeon(),
        };
    }

    /// <summary>
    /// One scripted walk (docs/PARITY_SHAPES.md "dungeon walks"): enter the layer, then always step to the
    /// leftmost (or rightmost) reachable unresolved node; a rest is a 習熟訓練 the first time and a rest
    /// after; a combat node reports HP −10 / stamina −3; seed 2 carries the trial loadout and uses its
    /// first consumable on the third step; seed 3 starts at miasma 55 + 5 × layer. When no unresolved
    /// node touches the current one, or the phase changes, the walk tries the interlude, the descent to
    /// the next layer (same map seed) and ends the life. Errors are recorded and the state kept.
    /// </summary>
    public static (List<object> Ops, List<object> Results) Walk(int layer, int seed, bool leftmost)
    {
        var ops = new List<object>();
        var results = new List<object>();
        ExplorationState state = null;

        void Apply(OrderedMap op, Func<ExplorationState> f)
        {
            ops.Add(op);
            object outcome;
            try
            {
                state = f();
                outcome = state;
            }
            catch (Exception ex)
            {
                outcome = ex;
            }
            var r = new OrderedMap { ["State"] = outcome };
            if (state != null) r["CanStep"] = state.Map.Nodes.Select(n => ExplorationReducer.CanStep(state, n.Id)).ToList();
            results.Add(r);
        }

        var profile = SevenLayers.Of(layer);
        var loadout = seed == 2 ? RunStart.TrialLoadout() : RunLoadout.Empty;
        int miasma = seed == 3 ? 55 + 5 * layer : 0;
        Apply(new OrderedMap
        {
            ["Op"] = "Enter", ["Layer"] = layer, ["MapSeed"] = (ulong)seed, ["Loadout"] = seed == 2 ? "trial" : "empty",
            ["Hp"] = 50, ["MaxHp"] = 50, ["Stamina"] = 10, ["MiasmaPercent"] = miasma, ["TempMaxStaminaMod"] = 0,
        }, () => ExplorationReducer.Enter(profile, profile.Map((ulong)seed), loadout, 50, 50, 10, miasma, 0));

        // A step that is not allowed (the boss, from the entry) is refused.
        int boss = state.Map.BossId;
        Apply(new OrderedMap { ["Op"] = "Step", ["Node"] = boss, ["Choice"] = RestChoice.Rest }, () => ExplorationReducer.Step(state, boss));

        bool trained = false;
        int steps = 0;
        while (state.Phase == RunPhase.Exploring && ops.Count < WalkMaxOps)
        {
            var open = state.ReachableUnresolved().Select(state.Map.Node).ToList();
            if (open.Count == 0) break;
            var pick = leftmost
                ? open.OrderBy(n => n.Column).ThenBy(n => n.Id).First()
                : open.OrderByDescending(n => n.Column).ThenByDescending(n => n.Id).First();
            var choice = RestChoice.Rest;
            if (pick.Kind == NodeKind.Rest)
            {
                choice = trained ? RestChoice.Rest : RestChoice.Train;
                trained = true;
            }
            int node = pick.Id;
            var c = choice;
            Apply(new OrderedMap { ["Op"] = "Step", ["Node"] = node, ["Choice"] = c }, () => ExplorationReducer.Step(state, node, c));
            steps++;
            if (pick.Kind.IsCombat() && state.Phase != RunPhase.MiasmaDeath)
            {
                int hp = state.Hp - 10, stamina = state.Stamina - 3;
                Apply(new OrderedMap { ["Op"] = "ReportBattle", ["Hp"] = hp, ["Stamina"] = stamina }, () => ExplorationReducer.ReportBattle(state, hp, stamina));
            }
            if (seed == 2 && steps == 3)
            {
                Apply(new OrderedMap { ["Op"] = "UseConsumable", ["Index"] = 0 }, () => ExplorationReducer.UseConsumable(state, 0));
                Apply(new OrderedMap { ["Op"] = "UseConsumable", ["Index"] = 7 }, () => ExplorationReducer.UseConsumable(state, 7));
            }
            if (state.TrainingMarks > 0)
            {
                Apply(new OrderedMap { ["Op"] = "SpendTrainingMark" }, () => ExplorationReducer.SpendTrainingMark(state));
            }
        }

        // A revisit: step back to the entry when it touches the current node (no 刻限 spent).
        if (state.Phase == RunPhase.Exploring && state.Map.Neighbours(state.CurrentNodeId).Contains(state.Map.EntryId))
        {
            int entry = state.Map.EntryId;
            Apply(new OrderedMap { ["Op"] = "Step", ["Node"] = entry, ["Choice"] = RestChoice.Rest }, () => ExplorationReducer.Step(state, entry));
        }

        Apply(new OrderedMap { ["Op"] = "Interlude" }, () => ExplorationReducer.Interlude(state));
        if (state.Phase == RunPhase.Interlude && !state.Profile.IsLast)
        {
            int next = state.Profile.Layer + 1;
            Apply(new OrderedMap { ["Op"] = "Descend", ["Layer"] = next, ["MapSeed"] = (ulong)seed },
                () => ExplorationReducer.Descend(state, SevenLayers.Of(next), SevenLayers.Of(next).Map((ulong)seed)));
        }
        Apply(new OrderedMap { ["Op"] = "SpendTrainingMark" }, () => ExplorationReducer.SpendTrainingMark(state));
        Apply(new OrderedMap { ["Op"] = "EndLife" }, () => ExplorationReducer.EndLife(state));
        Apply(new OrderedMap { ["Op"] = "EndLife" }, () => ExplorationReducer.EndLife(state));
        return (ops, results);
    }
}
