# Port notes: the demo flow and the effect grammar

Files (C# → JS):

| C# (`unity-project-kit/Assets/View/Depiction/…`) | JS | exports |
| --- | --- | --- |
| `Bridge/DemoSession.cs` | `src/bridge/depiction/DemoSession.js` | DemoMode, DemoStage, DemoEndScreen, DemoSession |
| `Bridge/DemoEnding.cs` | `src/bridge/depiction/DemoEnding.js` | DemoEnding |
| `Bridge/GrammarSamples.cs` | `src/bridge/depiction/GrammarSamples.js` | GrammarSample, GrammarSamples |
| `Script/EffectGrammar.cs` | `src/bridge/depictionScript/EffectGrammar.js` | FxSystem, FxForm, FxMark, FxFaces, EffectGrammar |
| `Script/EffectAudit.cs` | `src/bridge/depictionScript/EffectAudit.js` | Carries, Verdict, Basis, EffectAuditEntry, EffectFlow, EffectAudit |

Tests: `test/depiction/{DemoSession,DemoEnding,GrammarSamples,EffectGrammar,EffectAudit}.test.mjs`, helper
`test/depiction/_demoIsolate.mjs`, golden `test/depiction/demo.golden.json`.

## Tests: two layers and a golden

- **Real layer**: the C# test cases (DemoSessionTests, DemoEndingTests, GrammarSamplesTests,
  EffectGrammarRuleTests, EffectAuditTests, the EffectFlow lines of BattleSpeedTests, the DemoSession cases of
  JournalDisclosureHandInTests and UnknownValueTests) on the real modules. Loaded dynamically: while a module
  another agent ports is missing, these cases skip with `needs <module>`.
- **Isolated layer**: each file's own logic with the imports of other agents' modules swapped for small stubs
  (`importIsolated` rewrites the import specifiers to `data:` URLs). The stubs hold names, member lists and
  the few numbers the tested functions read (copied from the C#), never a rule the tests then check. This is
  what proves the port's branches, strings and numbers while the rest of the game is still being ported.
- **Golden** (`demo.golden.json`): written by a scratch C# program (`scratchpad/demo-oracle/Program.cs`,
  compiles the unity-port sources directly, no ProjectReference, so nothing is built under unity-port):
  - the nine grammar samples (FileName / Label byte for byte), `For(system)` for each system, and for every
    card of `CardCatalog.All` from gaps 0〜4: played or not, trait fired, title, what `WatchOf` finds;
  - `RandomEnemyId` for 260 (seed, pick) pairs including `int.MaxValue` / `int.MinValue`; `AverageOf` for 11
    turn lists; every enemy's `EnemyLine` and `FieldCellsFor` (default and a tool's start); `ChainLine`;
    two breakdowns;
  - **36 scripted demo runs** fought on the core (singles over six enemies × three seeds, chains with rests,
    a two-enemy chain from `run = 1`, surrenders, a Departure with tools and a journal): each battle's start
    state (HP, stamina, cell, gap, line, the whole shuffled draw pile, the corner, the disclosure), tally,
    stage and end screen, then `Again` and the first start once more.
  The isolated GrammarSamples layer drives `Build()` with stub plays made from the golden rows and must find
  the same nine samples; the isolated DemoSession layer checks the picks, averages, ChainLine and breakdowns
  against the golden.

Run: `node --test test/depiction/*.test.mjs` (Node 22 does not expand a bare directory argument; the task's
`node --test test/depiction/` makes node try to load the directory as a module).

## Decisions and quirks (not 1:1)

- **Overloads.** `DemoSession.Single` / `Chain` take either a deck (array → `Departure.Plain`) or a
  `Departure`; a `null` goes the `Departure` way (C# would pick by the null's static type; both throw
  `ArgumentNullException`). `FieldCellsFor(enemy)` and `FieldCellsFor(enemy, playerStartCell, startGap)` are
  one function (the 1-argument form when both extra arguments are `undefined`).
- **The private constructor** of DemoSession is a plain JS constructor; build runs with `Single` / `Chain`.
- **Get-only / private-set properties** (`Mode`, `Departure`, `Order`, `Index`, `Stage`, `Surrendered`,
  `Tallies`, `Current`, `AllWon`) are getters over `#private` fields, so a scene cannot set them.
  `Tallies` hands out the live list, and `Again()` empties that same list (`List.Clear()`), as in C#.
- **`unchecked` int arithmetic**: `RunSeed` and the per-start seed wrap like C# (`(a + Math.imul(b, k)) | 0`).
- **`(int Hp, int Stamina)` tuples** (`RestAfter`, and what `Chain.Carry` / `Chain.Rest` return) are
  `{ Hp, Stamina }` objects.
- **`AverageOf`**: `Math.Round(x, 1, AwayFromZero).ToString("0.#", Invariant)` → `roundAway(x, 1)` then
  `toFixed(1)` with a trailing `.0` cut. The .NET rounding (`ModF`, `|fraction| >= 0.5`) and `roundAway`
  agree on every value; pinned by the golden.
- **Object initializers** (`new DemoEndScreen { Won = won }`, `new GrammarSample { … }`, `new FxFaces { … }`)
  are constructors taking an optional `init` object applied after the field defaults.
- **`DemoEnding.Screen(tallied, build, report)`**: C# callers pass the method group `session.EndScreen`;
  in JS pass `() => session.EndScreen()` (a bare `session.EndScreen` loses `this`).
- **`catch (Exception)`** → `catch (e)`: JS catches anything thrown; C# catches every exception too.
- **`CoreBattleSource.TryPlay(id, zone, out played)`** is `{ ok, value }` in the port (CoreBattleSource's
  convention); GrammarSamples.PlayFirstCard reads `ok` as the verdict and `value` as the event.
- **`BattleSetup` named arguments** (`StartGap: startGap, PlayerStartHp: StartHp`) use TurnLoop.js's trailing
  plain-object form.
- **GrammarSamples constants** `DeckCopies` (`Constants.HandDraw * 2`) and `StartHp`
  (`Constants.PlayerMaxHp / 2`, integer division) are getters, so Constants is read at use, not at module load.
  `All` is cached like the C# `_all ?? (_all = Build())`: a `Build()` that throws leaves it unset, and the next
  read tries again. The list is frozen (`AsReadOnly`). `GrammarSample`'s defaults are C#'s
  (`System = Slash`, `Watch = EffectId.CardDraw`, the enums' first members).
- **`Index.ToString("00")`**: zero-padded to two digits, a negative keeps its sign before the digits (`-03`),
  three digits stay three.
- **EffectGrammar** imports `CueKind` / `StrikeSystem` from DepictionTypes.js and only reads them inside
  functions, so the cycle DepictionTypes ↔ EffectGrammar is safe. `ToStrike` / `FromStrike` / `MarkOf` return
  the enum strings; `FromStrike` returns `null` for a step (C# `FxSystem?`). `FormOf` / `Glyph` throw
  `ArgumentOutOfRangeException` with .NET's message for a value outside the enum.
- **EffectAudit**: the 60 rows are the C# rows verbatim (checked string for string against the C# source).
  `Entries` is built lazily on first use (C# static initializer) so EffectId is not read at module load;
  `All` returns the values in insertion order (= EffectId order). `Dictionary.Add` on a repeated key throws
  `ArgumentException` like .NET. `EffectAuditEntry` is frozen (get-only properties).
- **EffectFlow**: `const float`s; `FrameMs` is `Math.fround(1000 / 60)` (C# `1000f / 60f`, one float
  division) = 16.666666030883789. `AfterPlayerEventMsAt` / `AfterAutoEventMsAt` delegate to
  `BattleSpeed.WallMs`.

## C# behaviour kept as is (worth knowing, not bugs fixed)

- `DemoSession.Breakdown` reads only the four single attributes of `BattleTally.Order`: a tally whose
  `Attributes` held a combined key (`Guard | Attack`) would not count it (golden: `"技 1"`). `Chain.Tally`
  only writes folded single attributes, so it cannot happen in a real run.
- The comment on `RandomEnemyId` says "one of the eleven"; the roster holds thirteen and the pick covers all
  thirteen (`Enemies.All.Count`).
- `DemoEnding.Screen(true, build, report)` treats a `build` returning `null` / `undefined` as the fallback
  without reporting anything.
