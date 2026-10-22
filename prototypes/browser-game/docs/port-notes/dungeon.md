# Port notes: DungeonCore + DungeonContent

C# sources: `unity-port/DungeonCore/*.cs` (all but `IsExternalInit.cs`) and `unity-port/DungeonContent/*.cs`.
JS: `src/core/dungeon/` and `src/core/dungeonContent/`, one module per C# file at the TYPE_MAP path, every TYPE_MAP name exported.

## How it was checked against the C#

- `scratch-csharp/dungeon/` is a console app that compiles the DungeonCore and DungeonContent sources **by link** (`<Compile Include=… LinkBase=…>`), not by `ProjectReference`, so the build writes nothing under `unity-port/`. It prints the fixtures in `test/dungeon/fixtures/`:
  `dotnet run -c Release --project scratch-csharp/dungeon -- test/dungeon/fixtures`
  - `maps.json`: fingerprints, neighbours and predecessors of every layer × 27 seeds (0..19, 42, the five `ExplorationSession.LayerSeed(1..5, layer)` 64-bit seeds, 2^64−1), plus the minimal spec, a full spec with a trace, and an always-fork spec. All match.
  - `walks.json`: 24 whole lives played by a seeded policy (enter, step, rest or train, battle reports, consumables, training marks, interlude, descend, end of life). Every `RunPhase` is reached. The JS replays them line for line.
  - `encounters.json`: the seven tables and four 120-pick sequences. `rng.json`: SplitMix64 vectors (u64, doubles, NextInt, NextChance, Shuffle). `specs.json`: quota order after `With`. `errors.json`: the exact exception type and message of 70 failure paths.
- Tests: `node --test "test/dungeon/*.test.mjs"`. Node 22.22 does not expand a bare directory, so `node --test test/dungeon/` fails before running anything (it treats the directory as a module path). The glob form works.

## Decisions (not 1:1 in form, the same in behaviour)

- **ulong is a BigInt.** `SeededRng`, `MapGenerator.Generate`, `LayerProfile.Map` and `LayerMap` take a number or a BigInt and wrap it to 64 bits like an unchecked cast (`toUlong` in `_errors.js`). `LayerMap.Seed` is always a BigInt. Parity: `toParityJson` writes a BigInt as a string, while System.Text.Json writes a `ulong` as a JSON number. The harness has to compare `Seed` as a string or BigInt, because `JSON.parse` loses precision above 2^53.
- **Extension methods** become static objects, called with the receiver first: `RngExtensions.NextInt(rng, a, b)`, `RngExtensions.NextChance(rng, p)`, `RngExtensions.Shuffle(rng, list)`, `NodeKindTokens.ToToken(kind)` / `.IsDetour` / `.IsCombat`, `ItemEffects.Side(effect)` / `.ToToken(effect)`.
- **`IRng`** is an interface. It is exported as a frozen marker object with `IRng.is(x)`, which is true when `x` has a `NextDouble()` method.
- **`MapGenerator.Generate` overloads** are merged into one function. When the second argument passes `IRng.is`, it is the `(spec, rng, seed = 0)` overload. Otherwise it is `(spec, ulong seed)`.
- **`out` parameters** become returned objects. `RunLoadout.Spend(index)` returns `{ value, gaugeRelief }` and `Loadout.Use(slot)` returns `{ value, used }`, where `value` is the C# return value.
- **`ExplorationState.With(named: …)`** becomes `With({ profile, map, currentNodeId, resolved, timeLeft, miasmaPercent, hp, maxHp, stamina, tempMaxStaminaMod, trainingMarks, loadout, phase })`. A missing or null field keeps the current value, like the C# `??`, so `0` is still applied.
- **`Resolved`** is a `Set`. `HasResolved` also accepts an array, to support restoring from a save.
- **Dictionary order.** `IReadOnlyDictionary<NodeKind,int>` becomes a `Map`. `LayerMapSpec.With` is the only place where the C# removes keys and then adds them. A .NET (and Mono) `Dictionary` reuses the most recently freed entry slot, while a JS `Map` appends. `With` therefore emulates the entry table and free list (`DictionaryEntries`), and `specs.json` pins the result, e.g. `Minimal().With(Battle, 0)` keeps `Rest,Survey,Battle,Elite`. The constructors also accept a plain object, read in key order.
- **Enum order.** `Quotas.OrderBy(p => p.Key)` in `AssignKinds` sorts by declaration index (`enumIndex`). That order decides the shuffle input and the RNG order.
- **Exceptions carry the .NET 10 `Message` text**, including `" (Parameter 'x')"` and `"\nActual value was v."`, built by the extra helper module `src/core/dungeon/_errors.js`. This helper has no C# counterpart and is not in TYPE_MAP. DungeonContent imports it too. The exception types come from `cs.js`. `MapValidationException`, `ExplorationRuleException` and `LoadoutRuleException` are `Error` subclasses whose `name` is set. The newline is `\n` (Linux .NET). Unity on Windows would print `\r\n`.
- **`Enumerable.Single` / `ToDictionary` / missing-key** failures in the `LayerMap` constructor reproduce the BCL messages.
- **Records.** `MapNode` and `ReliefItem` use `defineRecord`, with `ToString()`/`toString()` in the compiler's record format. `Fight` and `Encounter` validate before `super()`, freeze their lists (C# `ReadOnlyCollection`) and override `Equals`/`equals` with the C# value equality. `GetHashCode()` is deterministic and consistent with equality, but its values are not .NET's, because .NET's `HashCode` is seeded per process. `Encounter.EnemyIds` is a getter that returns a new array, where C# has a lazy `IEnumerable`.
- **Static tables** (`SevenLayers` profiles, `LayerEncounters` tables) are built on first access, like a C# static initializer, so module evaluation order never matters. `LayerEncounters.All` and the pools are frozen (C# `Array.AsReadOnly` / `ReadOnlyCollection`). `SevenLayers.All`, `ItemCatalogue.Tools` and `ItemCatalogue.Consumables` are plain arrays, as in C#. `ItemCatalogue.All` returns a new concatenation on every read.
- **`RunLoadout.Empty` / `Loadout.Empty`** are non-writable static properties (C# get-only). `Loadout`'s constructor is private in C#. In JS it is public and documented as private.
- **`string.IsNullOrWhiteSpace`** uses `char.IsWhiteSpace`'s set: U+0085 counts as white space and U+FEFF does not, unlike JS `\s`.
- **Parity JSON.** Every non-record class has a `toJSON()` that emits `$type` first and then all public instance properties in declaration order, computed getters included (the System.Text.Json default):
  - `LayerMap`: Layer, Seed, Nodes, RowCount, EntryId, BossId, NodeCount, ShortestPathCost
  - `LayerMapSpec`: Layer, RowWidths, Quotas, ExtraEdgeChance, NodeCount, MiddleNodeCount, RowCount, ShortestPathCost, FullSweepCost
  - `LayerProfile`: Layer, Name, TrueName, Density, TimeLimit, MapSpec, ShortestPathCost, IsLast
  - `ExplorationState`: Profile, Map, CurrentNodeId, Resolved, TimeLeft, MiasmaPercent, Hp, MaxHp, Stamina, TempMaxStaminaMod, TrainingMarks, Loadout, Phase, EffectiveDensity, MaxStamina, IsOver
  - `RunLoadout`: Tools, ToolSlotCount, Consumables, DensityRelief
  - `LayerEncounterTable`: Layer, Normal, Elite, Boss
  - `Encounter` (record): Fights, IsStandIn, EnemyIds
  - `ItemDef`: Id, Name, Reading, Effect, Amount, Note, Side
  - `Loadout`: Tools, Consumables, ToolSlotCount, MiasmaDensityRelief, TimeLimitBonus

  If the oracle's serializer leaves out computed properties, drop them here to match (or the other way round). `docs/PARITY_SHAPES.md` did not exist when this was written.
- **int overflow is not emulated.** All values are small. One exception: `NextInt` computes its span and result with `| 0`. LINQ `Sum`'s checked overflow is not emulated either.

## C# quirks ported as they are

- `LayerMapSpec.With(NodeKind.Battle, n)` hands back the original battle count. It sets Battle to `n`, then sets it to `n - (n - current)`. So `With(Battle, x)` is a no-op, and when `x` is 0 the removed Battle key is re-added in its old slot.
- `MapGenerator.Generate(spec, seed)` checks `spec` for null twice, once in each C# overload. This is harmless.
- `AssignKinds` would index past `eligible` / `plainBag` if a spec let too many detours through. `LayerMapSpec` rules that out. The JS throws `ArgumentOutOfRangeException` there instead of reading `undefined`.
- `ExplorationReducer.ReportBattle` keeps the current phase unless HP reaches 0. A report after the layer boss therefore stays `LayerCleared` or `Completed`.
- `BattleField.Cells` with a kind that holds no fight prints the enum name (`"a Rest node holds no fight"`). For an undefined value it prints the raw value (`"a 99 node holds no fight"`), the same as C# `(NodeKind)99`.
