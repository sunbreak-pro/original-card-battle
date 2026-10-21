# Port notes: depiction layout and presentation specs (Script)

Files (C# under `unity-port/unity-project-kit/Assets/View/Depiction/Script/`, JS under `src/bridge/depictionScript/`):

| C# | JS exports | Test |
| --- | --- | --- |
| `BattleSpeed.cs` | BattleSpeedStep, BattleSpeed | `test/depiction/BattleSpeed.test.mjs` |
| `BottomRightLayout.cs` | PlayedLayout, ReserveHintLayout | `BottomRightLayout.test.mjs` |
| `CardTextFit.cs` | TextFit, CardTextFit (TextMeasure is a JSDoc typedef) | `CardTextFit.test.mjs` |
| `CornerLayout.cs` | ScreenBox, CornerLayout | `CornerLayout.test.mjs` |
| `DemoDeck.cs` | TraitCondition (Depiction's), DemoCard, DemoDeck | `DemoDeck.test.mjs` |
| `EnemyTurnShadeLayout.cs` | EnemyTurnShadeLayout | `EnemyTurnShadeLayout.test.mjs` |
| `FigureMotion.cs` | FigurePose, CharacterArt, FigureFacing, FigureFrame, EnemyMotionShape, DefeatShape | `FigureMotion.test.mjs` |
| `FloorSpec.cs` | FloorSpec | `FloorSpec.test.mjs` |
| `HandFan.cs` | FanPlace, HandFan, HandSpec | `HandFan.test.mjs` (HandFanTests + HandSpecTests) |
| `HpWickSpec.cs` | HpWickSpec | `HpWickSpec.test.mjs` |
| `OmenDisclosure.cs` | OmenDisclosure | `OmenDisclosure.test.mjs` |
| `PileLayout.cs` | PileKind, PileLayout | `PileLayout.test.mjs` |
| `PileList.cs` | PileList | `PileList.test.mjs` |
| `ReceiverDrop.cs` | ReceiverDrop | `ReceiverDrop.test.mjs` |
| `RefusalLineLayout.cs` | RefusalLineLayout | `RefusalLineLayout.test.mjs` |
| `StanceGlow.cs` | StanceGlowChange, StanceGlow | `StanceGlow.test.mjs` (+ StanceGlowReadoutTests on the real writer) |
| `StanceList.cs` | StanceList | `StanceList.test.mjs` |
| `StatusPanelLayout.cs` | StatusPanelLayout | `StatusPanelLayout.test.mjs` |
| `TurnSliceScript.cs` | TurnSliceScript | `TurnSliceScript.test.mjs` |

## Conventions used here

- **Floats are float32.** Every C# `float` constant is held as `Math.fround(literal)`; float parameters are rounded on
  entry; every float operation is followed by `Math.fround` (an int operand is converted to float first), as .NET
  computes in single precision. C# constant expressions (`Pad + DotSize + DotGap`, `1f - DarkTo`, `150f / 160f`) are
  folded the same way. Double math stays double where the C# writes it (`HandFan`'s angle, `HpWickSpec.Sqrt2`), and
  `(float)Math.Sin(...)` casts with fround. So values read like the C#'s: `EnemyTurnShadeLayout.BottomAlpha` is
  `0.7200000286102295`, `BattleSpeed.TweenSpeed(Slow)` is `0.800000011920929`, `PileList.WidthShare` `0.30000001192092896`
  (fine for CSS and for `Math.round`). Tests compare with `Math.fround(x)` where NUnit wrote `0.55f`.
- **Static classes** are plain objects; C# static properties are getters (`PlayedLayout.SpanWidth`,
  `FollowUpRight`, `ReserveHintLayout.Width`, `CornerLayout.SpeedNumberWidth`, `EnemyTurnShadeLayout.Height`,
  `RefusalLineLayout.Top`, `HpWickSpec.TileSize` / `TilePeriod`, `PileLayout.SpanWidth`, `DemoDeck.All`).
- **Readonly structs** (`ScreenBox`, `FanPlace`, `TextFit`) are frozen objects. `ScreenBox` has getters `Right`, `Bottom`,
  `FromRight`, the method `Overlaps(other)`, and `ToString()` (= `toString()`) writing floats as .NET's
  `float.ToString()` does (shortest round-trip, `E+XX` past 9 integer digits or below 1E-04, `-0`, `NaN`).
- **`out` parameters** (the shapes the scenes already read):
  - `FloorSpec.GapSpan(cells, playerCell, playerSize, enemyCell)` → `{ from, to }`
  - `HpWickSpec.Span(hp, hpMax, loss)` → `{ ok, from, to }` (`ok` is the C# bool)
  - `StatusPanelLayout.Place(playerX, playerWidth, enemyX, enemyWidth)` → `{ playerLeft, enemyLeft }`
  - `StatusPanelLayout.OnFloor(cells, playerCell, playerSize, enemyCell, enemySize, height)` → `{ player, enemy }` (ScreenBox)
  - value tuples `(float X, float Y)` (`FigureFacing.During`, `EnemyMotionShape.Stretch`) → `{ X, Y }`
- **`TextMeasure`** (C#: `delegate void TextMeasure(string text, int size, out float width, out float height)`) is a JS
  function `(text, size) => ({ width, height })`; `[width, height]` and `{ Width, Height }` are read too. The measured
  values and the box's width / height are rounded to float32, as the C# out floats and parameters are.
- **Overloads**: `BattleSpeed.TweenSpeed(step)` and `TweenSpeed(step, halted)` are one function.
- **Exceptions** carry .NET's full messages (`"No multiplier is written for this speed. (Parameter 'step')\nActual value was 7."`,
  `"An art id is required. (Parameter 'id')"`, `"Value cannot be null. (Parameter 'omen')"`,
  `"A box has no negative size. (Parameter 'width')"`, `"A panel has no negative width. (Parameter 'playerWidth')"`,
  `"Specified argument was out of the range of valid values. (Parameter 'index')"`), using the `cs.js` exception types.
- **Import cycles**: `OmenDisclosure.js` and `PileLayout.js` import nothing from `DepictionTypes.js` (its constructors read
  `OmenDisclosure.Full` and `PileKind.Discard`). `DemoDeck.All` is a lazy getter (built once, on first read: the C#
  static readonly list). `TurnSliceScript.Kesagiri` etc. are DemoDeck's strings.
- **C# strings**: `BattleSpeed.Load` and `CardTextFit` trim with C#'s `Trim` / `TrimStart` / `TrimEnd`
  (`char.IsWhiteSpace`: U+0085 is trimmed, U+FEFF is not; JS `trim()` does the opposite). `string + null` writes `""`
  (`CardTextFit.DescriptionRows` on a null lasting row gives `"∞ "`). `IsWordChar`'s `char.IsLetterOrDigit` under 0x80
  is `[A-Za-z0-9]`. Every literal is byte-identical to the C# (checked by script: every string and char literal of the 19 files).
- **Enums**: `PileLayout.LeftOf` takes `(int)pile`: a PileKind name, or a number (or .NET's number string for an
  undefined value, "5") as the int the C# would cast.

## C# behaviours ported as they are

- `DefeatShape.Tilt(Enemy, 0)` is `-0` (−24 × 0), in the C# too.
- `CardTextFit.Wrap` keeps a one-character rest on its own line even when it is wider than the box, and cuts where the
  width runs out when no break is allowed (`at == 0`). A measure that is not monotonic can make the cut land past the
  real overflow, as in the C#.
- `StatusPanelLayout.Place` can still overlap the panels when the two do not fit side by side between the edges (the C#
  says "as long as both fit"); `Clamp` returns `min` when `max < min`.
- `HpWickSpec.Alpha` computes `x - y` in int32 (`| 0`), as C# int arithmetic wraps.
- `HandFan.Place` assumes the outermost tilt stays within 90 degrees (no guard).
- `OmenDisclosure.Apply` empties the frame's own status lists in place (`List.Clear()`), so a list shared with another
  frame is emptied there too.
- `PileLayout.CountOf(null, anything)` answers 0 before looking at the pile.

## Verification

- Differential check against the real C# (scratch only: `scratchpad/layoutdiff/`, a console project that compiles
  `Depiction/Script/**/*.cs` and dumps every public member over input grids, and `diff.mjs`, which replays them on the
  port): **121,818 cases, all equal bit for bit** (each float compared as its exact value, `-0` and NaN included;
  exception types and messages included). Grids: every BattleSpeed member × steps (and an undefined one) × lengths, Load
  with odd white space; CardTextFit Name / Trait / Description / Wrap / CanBreakBefore / TwoLines / DescriptionRows over
  33 texts × sizes × boxes with a float stand-in measure; ScreenBox / SpeedTooltip / ToString over odd floats; every
  FigureMotion function over bells, scales and t; FloorSpec over cells −1..11; HandFan.Place / HandSpec.Rest / Hover /
  Width; HpWickSpec.Alpha over a 61 × 61 grid and Span; OmenDisclosure.Apply at −3..5; PileLayout; ReceiverDrop over
  card / receiver / pointer grids; StatusPanelLayout.Place over 3,000 inputs and OnFloor over every standing; the whole
  TurnSliceScript.Build() script and the DemoDeck faces field by field.
- `node --test` on the 19 test files: 234 tests, all pass (the C# EditMode tests for these files ported in full, plus
  CardFaceFitTests.cs on the eighty real cards and StanceGlowReadoutTests.cs on the real CoreScriptWriter). The whole
  `test/depiction/*.test.mjs` folder: 711 pass. Parity `scripts` suite: 28 / 28.
