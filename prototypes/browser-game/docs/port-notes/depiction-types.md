# Port notes: depiction vocabulary and effect timings (Script)

Files (C# under `unity-port/unity-project-kit/Assets/View/Depiction/Script/`):

| C# | JS | exports |
| --- | --- | --- |
| `DepictionTypes.cs` | `src/bridge/depictionScript/DepictionTypes.js` | RangeSide, UnitSide, CardKind, OmenIcon, StatusGlyph, OmenStatus, OmenHit, CardAim, DropZone, CardFace, CardValueKind, CardValue, TermDefinition, TermRow, TermMark, CardDetail, ChipKind, StatusChip, UnitFrame, StanceRow, OmenFrame, CornerFrame, BattleOutcome, DepictionFrame, PileFrame, PileCard, PlayedFrame, FloorFrame, StrikeSystem, StrikeSystems, DepictionEventKind, CueKind, Cue, DepictionEvent, DepictionScript, PlayVerdict, DepictionText, IDepictionSource, DepictionRunner |
| `Effects.cs` | `src/bridge/depictionScript/Effects.js` | EffectId, EffectSpec, EffectCatalog, EffectSwitches, EffectStep, StatusBeat, EffectStrength, EffectPlan, EffectTrace, Entry (also `EffectTrace.Entry`) |

Tests: `test/depiction/DepictionTypes.test.mjs`, `test/depiction/Effects.test.mjs`.

## DepictionTypes.js

- **Mutable classes, not records.** Every C# `sealed class` here is a JS class whose constructor sets the
  C# field initializers. Fields without an initializer take C#'s default: an enum field is its
  **first member** (`default(enum)` is 0: `CardFace.Kind` = `Attack`, `Cue.Kind` = `GuardReset`,
  `Cue.Target` = `Player`, `Cue.RangeAfter` / `UnitFrame.Range` = `Near`, `DepictionEvent.Kind` =
  `TurnStart`, `TermMark.Row` = `TraitTop`, `CardValue.Kind` = `Power`); an uninitialized `string` is
  `null` (`CardFace.Id`, `CardFace.Name`, `StatusChip.Label`, `CardFace.StanceHintAfterPay`); every
  `T?` and object reference is `null` (`Affects`, `RequiredRange`, `Detail`, `PlayedFrame.Last`,
  `Cue.Source`, `Cue.Fx`, `DepictionEvent.After`, `DepictionScript.Opening`). Lists are fresh `[]`.
- **Object initializers**: `new X()` as other ports do (`Object.assign(new Cue(), {...})`), or
  `new X({ ...members })`, which applies the members after the defaults (the C# object initializer).
- **Computed properties are getters** (the parity serializer reads them by name): `CardFace.CostDrop`,
  `OmenFrame.PlanTag`, `PileCard.RowText`, `DepictionEvent.WaitsForDrag`, and the runner's
  `Finished`, `Next`, `WaitingForPlayer`, `CanEndTurn`, `SuggestedCardId`, `GuideText`.
  `DepictionRunner.Index` / `Frame` (`{ get; private set; }`) are plain fields.
- `Cue.Unchanged` is a static field (-1). `DepictionText` is a plain object with the constants and
  methods of the static class.
- `OmenFrame.Disclosure` defaults to `OmenDisclosure.Full` and `DepictionEvent.PlayedTo` to
  `PileKind.Discard`; both are read in the constructor only, so the import cycles with
  `OmenDisclosure.js` / `PileLayout.js` / `EffectGrammar.js` are safe.
- **`out` parameter**: `DepictionRunner.TryPlay(cardId, zone)` returns
  `{ ok: verdict, value: played, verdict, played }`, the same shape as `CoreBattleSource.TryPlay`.
- `IDepictionSource` is a C# interface: exported as a frozen object `{ $members: [...] }` with a JSDoc
  typedef of its members. Nothing checks it at run time.
- **String concatenation of null**: C# writes `null` as `""` in `+`. Kept where a null can reach a
  text: `PileCard.RowText` (a null `Name`), `DepictionText.WrongZoneText`, the runner's `GuideText`
  and `RefusalText` (a face whose `Name` is null).
- Exceptions keep .NET's messages (`argNull` / `argRangeValue` as in CoreBattleSource):
  `PileFrame.CardsOf` on an unknown pile throws `ArgumentOutOfRangeException` "Specified argument was
  out of the range of valid values. (Parameter 'pile')\nActual value was X."; the runner throws
  `InvalidOperationException` "The script has already finished." / "Event n waits for a drag." /
  "A fixed script ends its own turn; the player cannot.", and `ArgumentNullException` for a null script.

## Effects.js

- **Floats**: every C# `float` is a single. `EffectSpec` stores `Math.fround(ms)`; `TotalMs`,
  `RebuildStartMs`, `DrawCardStartMs` and `BlockingMs`' running sum round each operation with
  `Math.fround` (.NET does single-precision arithmetic). `EffectStep.LeadMs` is `fround`ed when set.
  All written lengths are small integers, so in play the values are the plain sums.
- The catalog's 60 entries (lengths, staggers, blocking flags and the Japanese `Source` strings) were
  generated from the C# file by a script, so the strings are byte-identical. It is built lazily on
  the first `EffectCatalog.Of` / `All` (C#: a static readonly field). `All` returns a fresh array in
  the order written (= the enum's order).
- `EffectSwitches.WithOff(names)` (C#: `out List<string> unknown`) returns
  `{ ok: switches, value: unknown, switches, unknown }`. Names are trimmed with **C#'s `Trim()`**
  (`char.IsWhiteSpace`: trims U+0085, keeps U+FEFF — JS `trim()` does the opposite) and matched
  case-sensitively against the enum's names only ("7" is not a name).
- `EffectPlan.BlockingMs(ev, switches, speed)`: C#'s two overloads in one; with `speed` it is
  `BattleSpeed.WallMs(BlockingMs(ev, switches), speed)`. Without it, `switches` is checked for null
  before `ev`, as in C#.
- `EffectPlan.AddSteps` is the module-private function `EffectPlan_AddSteps`; `EffectCatalog.Build` is
  `EffectCatalog_Build`.
- `EffectTrace.Entry` is a nested class: exported as `Entry` and attached as `EffectTrace.Entry`.
  `Entry.Id` defaults to `CardDraw` (default(EffectId)). `EffectTrace.Speed` is a plain field
  (C# get/set auto-property) starting at `BattleSpeed.Default`.
- **`EffectTrace.ToCsv` number formats** (`"0.000"` / `"0"`, invariant culture) follow .NET exactly
  (checked against .NET 10 in this container): the value is first written to 15 significant digits
  for a double, 7 for a float (`NominalMs`), **ties to even** (Dragon4), then rounded half away from
  zero to the format's places; a negative value or -0 keeps its "-" even when it rounds to zero
  ("-0.000"); NaN / Infinity / -Infinity print as such. `_netCustomFixed` is exported for the test
  only (not a C# member).

## C# behaviours ported as they are (not bugs fixed)

- `StatusBeat.Of`: a StatusChange cue whose `StacksAfter` was left `Cue.Unchanged` (-1) counts as
  `StatusVanish` (`StacksAfter <= 0`).
- `EffectSpec.TotalMs(count)` answers 0 for any `count <= 0`; `RebuildStartMs` / `DrawCardStartMs`
  clamp negative indexes to 0.
- `EffectTrace.End` on a clock that went backwards writes a negative measured length ("-2000").
- `DepictionText.Find` / `WrongZoneText` throw on a null hand / face (C#: NullReferenceException,
  JS: TypeError).

## Verification

- Ported test cases: DepictionScriptTests.cs (all), EffectTests.cs (all but the PileList one, which
  belongs to PileList), plus the defaults, getters, texts and refusals of the runner.
- Each test file runs on the real modules when the siblings exist, else on the module with the other
  agents' imports swapped for stubs holding C# constants (`OmenDisclosure.Full = 2`, `PileKind`,
  `BattleSpeed.Default` with `WallMs` at the default speed only). The cases that need
  `TurnSliceScript.js`, `BattleSpeed.js` (factors) or `EffectGrammar`'s words are skipped, naming the
  module, until it exists.
- Differential check against the C# (scratch only, `scratchpad/effdiff/`: a console project that
  compiles `Depiction/Script/**/*.cs` directly, no ProjectReference): 3000 random events (every
  `DepictionEventKind`, 0〜5 random cues of every `CueKind` with random amounts, sources, cells,
  rebuilds) → `StepsOf` (ids, counts, leads, exception types and messages), `BlockingMs` all on /
  all off / one off / at the three speeds; `DrawCardStartMs` / `RebuildStartMs` over a grid;
  `TotalMs(-1..8)` for every effect; an `EffectTrace` CSV with odd clock values. 0 mismatches.
