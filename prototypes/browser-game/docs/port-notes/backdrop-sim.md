# Port notes: Backdrop.Script + BattleCore.Sim (GreedyPlayer, Decks)

C# sources: `unity-port/unity-project-kit/Assets/View/Backdrop/Script/*.cs` (BackdropSpec, BackdropMotion, BackdropSwitch,
ExplorationBackdrop, MiasmaLook) and `unity-port/BattleCore.Sim/{GreedyPlayer,Decks}.cs`.
JS: `src/bridge/backdrop/{BackdropSpec,BackdropMotion,BackdropSwitch,ExplorationBackdrop,MiasmaLook}.js`, the helper
`src/bridge/backdrop/CrtPow.js` (no C# counterpart, see below), `src/sim/{GreedyPlayer,Decks}.js`.
Tests: `test/backdrop/*.test.mjs` (+ `_oracle.mjs`, `_sim.mjs` helpers, `CrtPow.fixture.json`).
Run with `node --test test/backdrop/*.test.mjs`. Node 22 does not expand a bare directory: `node --test test/backdrop/`
fails before running anything.

## How it was checked against the C#

- **Backdrop: all 33 cases of the oracle's `backdrop` suite match** (`oracle/out/backdrop.json`, rebuilt case by case in
  `test/backdrop/_oracle.mjs` from `oracle/src/Suites/Backdrop.cs`, serialized and diffed with the parity harness's own
  `test/parity/lib/serialize.mjs` + `compare.mjs`). That covers every layer 0..8 (For, SkyAt, HazeAlphaAt, BrightestAt,
  Luminance, ReachRedContrast, FlickerAlpha, the exploration backdrop, LowestContrast, PeakLuminance, Mix, Over), every
  miasma reading 0..100 step 5, the motion case (MoteAt, MoteAlpha, Crossfade, Hash incl. negative and int.MaxValue
  indices, Ticks, CornerDistance) and the switch script. Float members compare through `Math.fround`, doubles exactly.
- Every case of the C# test classes BackdropSpecTests, BackdropMotionTests, BackdropSwitchTests, ExplorationBackdropTests
  and MiasmaLookTests is ported (75 tests pass).
- **Sim:** the DeckTests and GreedyPlayer cases of `BattleCore.Sim.Tests/SimTests.cs` are ported, plus the oracle's
  `catalog` `table:Decks` case and a replay of the oracle's `battles` suite (seed 1 of every deck × setup, 98 battles)
  that checks every greedy decision (free step, card, target and the double value, in order). They skip until the
  battle core modules they load exist (see Blockers). The pure value terms (Value, the status worths, PositionBias
  per policy, HeldIds, constants) were run against stubbed core modules and pass.

## C# float (single precision) in the backdrop

The backdrop computes in `float`. Every float value is held as its exact float32 value, and **every float operation of
the C# is followed by `Math.fround`** (`f(f(a * b) + c)` …). A single float op computed in double and rounded once is
bit-identical to the CLR's float op (53 ≥ 2·24 + 2), and RyuJIT does not contract to FMA, so the results are exact.
Float parameters are rounded on entry (`y = f(y)`), as the C# caller's implicit conversion would; that matters because
the harness passes JS doubles like `0.52`. Mixed float/double expressions follow the C# promotion exactly, e.g.
`CornerDistance`'s `Math.Sqrt(aspect * aspect + 1.0)` rounds `aspect * aspect` to float first, and
`BreathPeriodMs` is float math then `Math.Round(double)` (banker's, `roundEven`). `Rise`/`Crossfade`/`Breath` are double
math cast to float once at the end. Constants: `const float` → `Math.fround(literal)`; `ShadeCurve` stays double.

## System.Math.Pow → `CrtPow.Pow` (glibc's pow)

`Luminance` (WCAG) uses `Math.Pow(x, 2.4)` and `MiasmaLook.ShadeAt` `Math.Pow(x, 1.4)`. .NET's `Math.Pow` calls the C
runtime's `pow`; the oracle runs on Linux x86-64 with glibc 2.39 on a CPU with FMA, i.e. glibc's `__pow_fma`.
V8's `Math.pow` is a different algorithm: it differs in the last bit for ~10% of the luminance inputs, which made 7 of
the 33 oracle cases fail (Luminance doubles, compared exactly). `CrtPow.js` ports glibc's `e_pow.c` (ARM
optimized-routines) line for line, with:

- the tables `__pow_log_data` / `__exp_data` read out of this machine's `libm.so.6` (hex uint64 literals);
- an exact FMA emulation (Dekker product + Boldo–Melquiond round-to-odd sum) for the explicit `__builtin_fma` calls
  and for every `a * b + c` GCC contracts under `-mfma` (single-use multiplies in the same basic block; the
  `specialcase` k<0 multiply is shared across blocks and so not fused — found empirically);
- all of `e_pow.c`'s special cases (zeros, infinities, NaN, negative x with integer y, subnormal x, tiny/huge y).

Checked bit for bit against glibc's `pow` (via Python's `math.pow`, same libm) on 320,000 inputs (luminance inputs,
x^1.4 for float x, wide random x/y) plus the special grid; `test/backdrop/CrtPow.test.mjs` pins 2,480 of them, 1,200
being inputs where V8 rounds differently. Signaling NaNs are not told apart (JS cannot). Notes:

- The Unity build on Windows/Mono/IL2CPP has its own `pow`; the oracle is the reference here. Nothing drawn or decided
  depends on the last bit: Luminance/Contrast only feed the WCAG checks, and `ShadeAt` casts the pow result to float.
- `Math.Sin` / `Math.Cos` (BackdropMotion, MiasmaLook.Breath) stay V8's: every result is cast to float (or summed into a
  float cast), so a last-bit difference from glibc almost never survives; all oracle cases match.
- If the integrator prefers, `CrtPow.js` can move to `src/shared/`; nothing else in the ported C# calls `Math.Pow`.

## Shapes and API decisions

- **Structs** `Rgba`, `SkyStop`, `Flicker`, `GaugeTick`, `MiasmaShade` are `defineRecord` (value equality like a C#
  struct); their constructors round float fields. `default(Rgba)` (fields never assigned, e.g. `Motes.None.Color`) is
  `new Rgba(0, 0, 0, 0)` — alpha 0, not the constructor default 1, as the oracle shows. `Flicker.Moves` is a getter.
- **Object initializers** (`Haze`, `Motes`, `LayerBackdrop`, `ExplorationBackdrop`): the constructor takes one plain
  object of members (`new Haze({ Shape, Color, X: 0.42 })`); unknown keys throw; float members are rounded. Later
  assignments should be float32 values.
- **Static classes** (`BackdropSpec`, `ExplorationBackdropSpec`, `MiasmaLook`, `BackdropMotion`, `Decks`) are frozen
  objects whose public static fields appear in C# declaration order (what the oracle's `Tables.Static` writes);
  private statics (`All`, `White`…, `VeilAlpha`, `Steps`, `LookSeed`) are module-level, not exported.
- **`BackdropMotion.MoteAt`** has two `out float` parameters → returns `{ x, y }`.
- **`MiasmaLook.ShadeAt`** overloads → one method; `ShadeAt(p)` when `ms` is undefined, else `ShadeAt(p, ms)`.
- **`BackdropSwitch.Change`** (nested enum) is exported as `Change` and hung on the class; `Shown` / `Leaving`
  (`private set`) are getters over `_shown` / `_leaving`.
- **`Rgba.Hex`**: `TrimStart('#')` (every leading `#`), `Substring` and `Convert.ToInt32(s, 16)` with .NET's range
  check and `FormatException` messages (a local `FormatException` class; cs.js has none).
- **`SimDeck.Rows`** tuples → `{ CardId, Copies }` objects (the oracle writes the tuple element names). `Count` and
  `HasStance` are getters. `Decks.Archetypes`, `InitialFortyIds`, `StanceSubstitutes` and the archetype rows are frozen
  (IReadOnlyList); the decks built by `CostOneOnly` / `Plain` / `WithoutStances` hold plain arrays.
- **`GreedyPlayer`**: loops in the C#'s order so the tie-breaks are the same (free step +1 before −1, first strictly best;
  hand order with the first copy of a kind standing for the rest; targets in `TargetsFor` order, i.e. `state.Living`
  ascending). Values are doubles summed in the C#'s left-to-right order. Constants are static fields on the class.
  `Choice` is a record. The constructor's `ArgumentOutOfRangeException` carries .NET's message
  `"0 (no cap) or more (Parameter 'maxPlays')"`, like the other ports (PARITY_SHAPES says the oracle cuts the suffix; this
  exception never reaches a parity case).
- `InOut`'s `PlayedThisTurn.Contains(BattleAttribute.Attack)` is an exact-value `includes`, as in C# (a combined flag
  value would not match; the core records the folded single attribute).

## C# quirks ported as they are

- `Decks.CostOneOnly`'s top-up loop can add a third or later copy if the catalog had very few cost-1 cards (the summary
  says "a second copy"); unchanged.
- `ReachRedContrast` / `LowestContrast` start from `double.MaxValue` (`Number.MAX_VALUE`) and return it for a backdrop
  with no steps (cannot happen).
