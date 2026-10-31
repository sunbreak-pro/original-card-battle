# Parity: C# oracle vs JS port

The JS port must replay exactly what the C# does. A C# console app (the oracle) runs fixed scenarios and writes JSON; a JS harness runs the same scenarios on the port and diffs.

## Oracle (C#)

- Project: `$G/oracle/Oracle.csproj` (net10.0, console). References `$U/BattleCore`, `$U/DungeonCore`, `$U/DungeonContent`, `$U/Exploration.Bridge`, `$U/Exploration.Script`, `$U/Depiction.Bridge`, `$U/Depiction.Script`, `$U/Backdrop.Script` projects by `ProjectReference`; links `$U/BattleCore.Sim/GreedyPlayer.cs` and `Decks.cs` with `<Compile Include=... Link=...>` (the Sim project is an exe). `dotnet` 10 is installed at `/usr/bin/dotnet`. `$U` is read-only: never write inside it (build output goes to `$G/oracle/bin`, `$G/oracle/obj`; if a referenced project writes its own bin/obj under `$U`, that is acceptable build output but do not commit or edit sources).
- Run: `dotnet run -c Release --project $G/oracle -- --out $G/oracle/out` writes one JSON file per suite.
- JSON shape: member names exactly as in C# (PascalCase). Every object of a record/class type gets `"$type": "<C# type name>"` first. Plain enums as their member-name string; `[Flags]` enums as their integer value. `IReadOnlyList`/arrays → arrays; `Dictionary<K,V>` → object with `String(key)` keys in enumeration order; `double` written with round-trip precision. Do not serialize `[JsonIgnore]`-like computed caches; serialize public properties and public fields of records/classes (read-only computed properties too, if they are simple getters — keep a list in `$G/docs/PARITY_SHAPES.md` of what is included per type so the JS side matches).
- Suites (each a file `out/<suite>.json`, an array of cases `{ id, input, output }`):
  1. `catalog` — every `CardDef` from `CardCatalog` (initial + learned, all mastery steps if the API exposes them), every enemy def from `Enemies`, the `Constants` tables.
  2. `battles` — `GreedyPlayer` (Sim) battles: decks from `Decks` × every enemy in the roster (normal, elite, boss; also two-enemy setups if `Decks`/`LayerEncounters` define them) × seeds 1..5. Record for every step: the call made (`Start`, `BeginPlayerTurn`, `PlayCard(id,target)`, `TakeFreeStep(dir)`, `EndTurn`), the full `StepResult` (events + state). Cap at 60 turns.
  3. `previews` — at each player decision point of the first 2 battles per enemy: `CanPlay`, `CostNow`, `Preview` for every hand card × target, `PreviewOmen` per enemy, `OmenBoundaryCells`, `OmenMoveCell`, `PreviewRecovery`.
  4. `texts` — `CoreText`, `CardFaceText`, `StatusChipText` output for every card (and every mastery step where applicable), every status, every enemy omen in the battles above.
  5. `scripts` — `CoreScriptWriter` / `CoreBattleSource` depiction scripts for the first battle per enemy (seed 1).
  6. `dungeon` — `MapGenerator` maps for layers 1..7 × seeds 1..20 (+ `MapValidator` result); `ExplorationReducer` scripted walks per layer (always take the leftmost open node; then the rightmost; include rest/info/event/consumable uses, pushed-out and miasma-death cases); `LayerEncounters` picks per layer × node kind × seed; `Miasma` table; `SevenLayers` table; `ItemCatalogue` and `Loadout` defaults.
  7. `exploration` — `Expedition` runs: a full expedition with stubbed battle outcomes (win with HP−10 each fight) through layers 1..3 with seed 1..3, recording each screen's `ExplorationScreenBuilder` output and every `BattleRewards`/`UniqueCards` result; `JournalBook` pages at each step.
  8. `backdrop` — `BackdropSpec`, `ExplorationBackdrop`, `MiasmaLook` values for every layer and miasma 0..100 step 5.
- Determinism: only `SeededRng`. If an API uses `SystemRng`/`System.Random`, replace it in the oracle by injecting `SeededRng` (the JS cannot reproduce `System.Random`).

## Harness (JS)

- `$G/test/parity/run.mjs` (`node test/parity/run.mjs [suite] [--first N]`): for each case, rebuild the input with the JS port (same API, same seeds, same greedy policy from `src/sim/GreedyPlayer.js`), convert with `toParityJson` (src/shared/cs.js; `$type` from the record's `$name`), and deep-diff against the oracle output. Print per suite: cases, passed, and for failures the first differing JSON path with both values (truncated). Exit code 1 if anything differs.
- `$G/test/parity/report.json` gets the full machine-readable result for the fix loop.
