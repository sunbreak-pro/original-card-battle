# Port notes: depiction writer (Bridge)

Files (C# under `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/`):

| C# | JS |
| --- | --- |
| `CoreScriptWriter.cs` | `src/bridge/depiction/CoreScriptWriter.js` |
| `CoreBattleSource.cs` | `src/bridge/depiction/CoreBattleSource.js` |
| `BattleLaunch.cs` | `src/bridge/depiction/BattleLaunch.js` |
| `FloorCells.cs` | `src/bridge/depiction/FloorCells.js` |
| `PileCards.cs` | `src/bridge/depiction/PileCards.js` |
| `CardSystems.cs` | `src/bridge/depiction/CardSystems.js` |

Tests: `test/depiction/{CoreScriptWriter,CoreBattleSource,BattleLaunch,FloorCells,PileCards,CardSystems}.test.mjs`
(ported from `Bridge/Tests/CoreScriptWriterTests.cs`, `CoreBattleSourceTests.cs`, `BattleLaunchTests.cs`,
`FloorCellsTests.cs`, `PileReadoutTests.cs`, and the CardSystems cases of `EffectGrammarTests.cs`).

## Parity check

An ad-hoc replay of all 28 cases of the C# oracle's `scripts` suite (`writer:*` = CoreScriptWriter fed
the recorded greedy battles, `source:*` = CoreBattleSource run unattended for 30 turns with every
Inspect / PreviewFor / RefusalText / GuideText asked at each wait) compared every member the oracle
wrote against the JS objects: **28 / 28 cases identical** (0 differences), the two-enemy exception
included. (Script: `scratchpad/scripts-compare.mjs <gameRoot> oracle/out/scripts.json`; the
official harness is `test/parity/run.mjs`.)

## API shapes the scenes rely on

- `CoreBattleSource` implements `IDepictionSource` with the C# member names: `Frame`, `Finished`,
  `WaitingForPlayer`, `CanEndTurn`, `SuggestedCardId`, `GuideText` (getters / a field), and
  `AdvanceAuto()`, `EndTurn()`, `Inspect(id)`, `TryPlay(id, zone)`, `PreviewFor(id)`,
  `RefusalText(id, verdict)`. Also `State`, `History`, static `Slice(seed)`, static `FleeGap`.
- `TryPlay` has a C# `out DepictionEvent played`. Per CONVENTIONS it returns `{ ok, value }`:
  **`ok` is the `PlayVerdict`** (not a bool) and `value` the played event (null unless `Accepted`).
  The same two are also on the object as `verdict` and `played`.
- `Frame` is a plain field (C# `{ get; private set; }`); it is replaced by `ev.After` as each
  event is taken, so `source.Frame === lastEvent.After`.
- `BattleLaunch` keeps the C# public fields with the same defaults. JS extra: `new BattleLaunch(init)`
  assigns `init`'s own properties after the defaults (the C# object initializer
  `new BattleLaunch { EnemyId = "..." }`). `SliceFieldCells`, `DefaultStartGap`, `DefaultDisclosure`
  are static getters (C# consts that read other modules), `DefaultSeed` / `LowestDisclosure` static fields.
- Script objects (`DepictionEvent`, `Cue`, `DepictionFrame`, `UnitFrame`, `OmenFrame`, `FloorFrame`,
  `PileFrame`, `PileCard`, `PlayedFrame`, `CornerFrame`) are built from the DepictionTypes classes:
  `new X()` then the initializer's members assigned in the C# order, so every default the
  DepictionTypes port gives (e.g. `Cue.HpAfter = Cue.Unchanged`) stays where the C# leaves it.

## Translation choices

- **Overloads.** `CardSystems.Of(CardDef)` / `Of(EnemyActionDef)` are one function that dispatches on
  `instanceof EnemyActionDef` (C# overload resolution is by static type; every caller passes one or
  the other). Both return `[]` for null.
- **Lazy statics.** `CardSystems.Table` (C# `static readonly ... = Build()`) is a getter that builds the
  Map on first use, and `PileCards._catalogOrder` stays lazy as in the C#. Neither reads `CardCatalog`
  at module evaluation, so import cycles cannot see it half-built. Table rows are frozen arrays
  (C# `.AsReadOnly()`).
- **`SortedDictionary<StatusKind, int>`** (the writer's per-unit statuses) is a `Map` enumerated in
  `StatusKind` declaration order (`enumIndex`), which is the C# key order (no explicit values).
- **Record equality.** `Equals(a, b)` on omens and `List<CardInstance>.Remove` use `csEquals`.
  C# record equality compares list-typed members by reference while `csEquals` compares them
  element-wise; it only matters for records holding lists (CardDef via Face.Statuses), and the hand
  removal always receives the very instance the hand holds (TurnLoop.PlayCard), so no observable
  difference.
- **Pattern switches.** `OpenIfBoundary` is an `if` chain in the C# case order, each guard included
  (a failed guard falls through to the next case, as in C#). `Apply`'s switch has one case per event
  type, so an `else if` chain matches the same case.
- **Exceptions.** Same exception types and .NET message texts: `ArgumentNullException`
  ("Value cannot be null. (Parameter 'x')"), `ArgumentOutOfRangeException`
  ("msg (Parameter 'x')\nActual value was v."), `InvalidOperationException` with the C# messages,
  `KeyNotFoundException` for the dictionary indexer in `ShownOmenPower`. `Queue.Dequeue` on an empty
  queue cannot happen through the public API; `Next()` throws InvalidOperationException('Queue empty.') if it did.
- `BattleLaunch.EnemyById` catches `KeyNotFoundException` (by class or by `name`) and rethrows the
  C# `ArgumentException`; a null id prints as `""` in the message, as C# string concatenation does.
- `(int)EffectPlan.BlockingMs(...)` in the tests → `Math.trunc`.

## Bugs / oddities ported as they are

- **Two-enemy battles throw (C# bug; reproduced until the JS multi-enemy extension).** *Now handled in
  JS:* `depiction-multi-enemy.md` keeps every enemy, so the port writes these battles; the two oracle
  cases below are documented parity deviations (`test/parity/deviations.mjs`). What follows is the C#'s
  behaviour, which the oracle still records. The writer keeps one omen (`_omen`) and
  takes every `OmenSet`, whichever unit it is about. With a second enemy (roster §9's pair
  `polearm_warped` + `crossbow_hunter`, which the exploration hands in for layers 2〜3), the last
  omen set is unit 1's (`bolt`); `Snapshot → FloorOf → AsShown` puts it on unit 0 and
  `TurnLoop.OmenBoundaryCells` looks `bolt` up in the polearm's actions →
  `KeyNotFoundException: The given key 'bolt' was not present in the dictionary.` thrown out of
  `CoreScriptWriter.Write`, so out of `CoreBattleSource.AdvanceAuto / EndTurn / TryPlay` (the core
  state has already advanced; nothing is queued). The C# oracle (`oracle/out/scripts.json`,
  cases `*|polearm_warped+crossbow_hunter|1`) shows the same exception at the same steps, and the
  JS matches it message for message, including the writer's later events (their `Order` numbers
  carry the events written before each throw). The Unity build has the same crash; the JS port no
  longer throws.

- `CoreScriptWriter.Opening` does not reset `_order`, `_omenPreview`, `_omenPreviewFor`,
  `_enemyActed`, `_enemyReserveHeld` or `_enemyFalls`; a writer reused for a second battle keeps
  numbering from the first. Ported as is (the C# makes one writer per battle).
- `CoreScriptWriter.Snapshot` always writes `Corner.Floor = 1` and `MiasmaPercent = 0` (the
  exploration's floor and miasma are not handed to the battle yet, #99). Ported as is; a scene
  that wants the layer / miasma must take them from the exploration side.
- Only the first enemy (`Enemies[0]`) is modelled by the C# writer (`_enemy`), even in a two-enemy
  battle (`MoreEnemyIds`): events about unit 1+ still move the one enemy model (e.g. `Unit(actor)`
  ignores `e.Unit`), and `EnemyDefeated` only marks the fall for `Unit == 0`. The JS port models every
  enemy instead (`depiction-multi-enemy.md`); with one enemy its output is the C#'s, member for member.
- `PileCards.Rows` takes `deck` as `IReadOnlyDictionary`; the JS accepts a `Map` (what `Fill` builds)
  or a plain object.
