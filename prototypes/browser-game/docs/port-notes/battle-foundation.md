# Port notes: BattleCore foundation

Files: `IRng.cs`, `Attributes.cs`, `Types.cs`, `Constants.cs`, `Columns.cs`, `Combat.cs`, `Field.cs`, `BattleEvents.cs`
→ `src/core/battle/{IRng,Attributes,Types,Constants,Columns,Combat,Field,BattleEvents}.js`.
Tests: `test/battle/{IRng,Attributes,Types,Constants,Columns,Combat,Field,BattleEvents}.test.mjs`
(+ `IRng.fixture.json`, `Combat.fixture.json`, both written by the C# code under dotnet 10 in
`scratch-csharp/foundation`, which compiles `$U/BattleCore/*.cs` by link without writing under `$U`).

## How to use these types from other modules

### Records: positional, or C# named arguments

Every record in `Types.js` (and every event in `BattleEvents.js`) is `defineRecord` plus one extra: **a trailing
plain object whose keys are all parameter names is read as C# named arguments**. Omitted parameters get their
C# default.

| C# | JS |
| --- | --- |
| `new Face(Power: 7, Reach: new Reach(0, 2))` | `new Face({ Power: 7, Reach: new Reach(0, 2) })` |
| `new Trait(TraitCondition.GapAtMost, effect, amount, Threshold: gap)` | `new Trait(TraitCondition.GapAtMost, effect, amount, { Threshold: gap })` |
| `new StatusGrant(StatusKind.Regen, 1, OnSelf: true)` | `new StatusGrant(StatusKind.Regen, 1, { OnSelf: true })` |
| `x with { A = 1 }` | `x.with({ A: 1 })` |

- A plain object that mixes known and unknown keys throws (`unknown named argument(s) ...`), to catch typos.
- A plain object with **no** known key is treated as an ordinary positional value (so nothing changes for a
  field that legitimately holds a plain object). `Map`s, arrays, records and functions are never plain objects.
- `named[k] === undefined` means "not given": the default stays.
- `TurnLoop.js` (BattleSetup) adopted the same convention.

### Events: init-only properties

`BattleEvent.Unit`, `PushRefused.ByStance`, `StatusApplied.Dropped` / `Warded` are C# `{ get; init; }`
properties outside the positional constructor. In JS they are own fields set to their C# default (0 / false)
by the constructor, included in `equals` (C# record equality includes them) and in `toJSON`.

| C# | JS |
| --- | --- |
| `new DamageDealt(a, t, raw, abs, dmg, g, hp) { Unit = unit }` | `new DamageDealt(a, t, raw, abs, dmg, g, hp, { Unit: unit })` or `new DamageDealt(...).with({ Unit: unit })` |
| `new StatusApplied(...) { Unit = u, Dropped = d, Warded = w }` | `new StatusApplied(..., { Unit: u, Dropped: d, Warded: w })` |
| `ev with { Unit = u }` | `ev.with({ Unit: u })` |
| `case DamageDealt d:` / `OfType<DamageDealt>()` | `ev instanceof DamageDealt` / `events.filter((e) => e instanceof DamageDealt)` |

Derived constructors take Actor first, then their own fields: `new TurnStarted(Actor.Player, 1)`.
`EnemyDefeated` has only Actor. `StepResult(State, Events)` is a plain record, not a `BattleEvent`.

`toJSON` key order is `$type`, Actor, own positional fields, Unit, own init props. C# `ToString()` prints the
base property Unit right after Actor; if the parity diff is order-sensitive, that is the place to look.
Computed getters (`StatusApplied.Landed`, `CardDef.Cost` / `Attribute` / `AllTraits`, `Face.ReachOrDefault` /
`StatusList` / `GivesFoeStatus`, `BattleState.Gap` / `Living` / `Nearest` / `Tools` / `History` ..., `EnemyUnit.Alive` ...)
are **not** in `toJSON` (defineRecord serializes only stored fields). If the oracle's serializer includes
read-only computed properties (System.Text.Json does by default), the parity harness must add them per
`PARITY_SHAPES.md` or the oracle must skip them.

### Overloads

- `BattleState.WithEnemy(unit, body)` / `WithEnemy(body)`: one JS method; a non-number first argument is the
  one-enemy shorthand (`WithEnemy(0, body)`).
- `Field.Validate(fieldCells, playerCell, playerSize, enemyCell, enemySize)` /
  `Field.Validate(fieldCells, playerCell, playerSize, enemies)`: one JS method; a number 4th argument is the
  first overload. The tuples `(int Cell, int Size)` are `{ Cell, Size }`; `{ Item1, Item2 }` and `[cell, size]`
  are accepted too (C# erases tuple names, callers may build either).
- `SystemRng(int seed)` / `SystemRng(Random random)`: a number is a seed; anything else must have `NextDouble()`.
- C# extension methods `gap.ToBand()`, `kind.ToToken()`, `kind.ToLabel()` are `EnumTokens.ToBand(gap)` etc.
- Static methods with C# default parameters keep them positional (`Combat.ComputeRawPower(face, traitBonus = 0,
  followUp = 0, conversion = 0, blowMult = 1.0)`): `ComputeRawPower(p, blowMult: 1.5)` is
  `ComputeRawPower(p, 0, 0, 0, 1.5)` (or pass `undefined` for skipped defaults). `Field.Move(state, who, cells, unit = 0)`.
- `Combat.ApplyGuard` returns the tuple as `{ Damage, GuardAfter, Absorbed }`.

### Enums

- `BattleAttribute` is `[Flags]` → `defineFlags({ None: 0, Attack: 1, Guard: 2, Skill: 4, Stance: 8 })`;
  `x.HasFlag(f)` → `hasFlag(x, f)` from cs.js.
- All the others are plain `defineEnum` in C# declaration order (`StatusKind` 0..15, ...), so `(int)k` is
  `enumIndex(StatusKind, k)` (StatusSet.Kinds orders by it).
- `Constants.FaceOrder` holds the FaceKind member strings directly (see "Import cycles").

## Import cycles (read this if a module fails with "Cannot access X before initialization")

- `Constants.js` is a **leaf** (imports nothing), so every module may read it at evaluation time. That is
  why `FaceOrder` is the literal `['Attack', 'Move', 'Guard', 'Skill', 'Stance']` instead of reading
  `FaceKind` from Types.js — the values are identical (a defineEnum value is its name).
- `BattleEvents.js` imports only cs.js.
- `Types.js` reads only cs.js and Constants at evaluation time. It imports `Columns` (leaf-ish), `AttributeRule`,
  `Field`, `Combat` and `StatusSet` (Statuses.js) but uses them only inside getters / methods.
  `Attributes.js` and `Field.js` import Types.js back and read it only inside functions; `Combat.js` imports
  Statuses.js and reads it only inside `CellsAfterSlow`.
- Consequence for others: if `Types.js` is the first module of a cycle to load, Statuses.js (and anything it
  imports) is evaluated **before** Types.js's body. Such a module must not read `StatusKind` etc. at its
  own top level (e.g. a `const TABLE = { [StatusKind.Slow]: ... }`); read it lazily (inside functions).

## C# quirks ported as they are

- **SeededRng**: SplitMix64 on `ulong` → BigInt masked to 64 bits; `unchecked((ulong)seed)` sign-extends a
  negative int seed (`-1` → `0xFFFF_FFFF_FFFF_FFFF`, `BigInt.asUintN(64, ...)`). `NextDouble` is
  `(z >> 11) * 2^-53`, exact in both. Pinned: first 5 values for seeds 1 and 42 (and 0, −1, int.MinValue,
  int.MaxValue), plus 200 values × 20 seeds from dotnet (`IRng.fixture.json`), all bit-identical.
- **SystemRng(seed)**: not a stub. `new Random(seed)` in .NET (and Unity's Mono) is the documented seeded
  Knuth subtractive generator ("Net5CompatSeedImpl"); it is ported in `IRng.js` (`CompatRandom`, private)
  with C# int wraparound (`| 0`) everywhere — needed for seeds near int.MinValue / int.MaxValue, where
  `MSEED - |seed|` is negative and intermediate subtractions overflow in C#. Seed int.MinValue behaves like
  int.MaxValue (`Math.Abs` special case), as in .NET. 4000 values × 20 seeds match dotnet. The
  `SystemRng(Random)` overload takes any `{ NextDouble() }` (e.g. `{ NextDouble: Math.random }` for an
  unseeded game). C# tests that use `new SystemRng(seed)` (CardsTests, DeckRulesTests) therefore replay exactly.
- **Seeds are C# `int`**: JS seeds go through `seed | 0` (a C# `(int)` cast), so non-integers truncate and
  values past 32 bits wrap.
- **Exceptions**: thrown with the cs.js classes and the exact .NET `Message` text, including the
  `(Parameter 'x')` suffix and `\nActual value was v.` line that `ArgumentOutOfRangeException(param, value, msg)`
  appends (`Environment.NewLine` is `\n` here; Unity on Windows would use `\r\n`). `ArgumentNullException(param)`
  → `Value cannot be null. (Parameter 'x')`. Enum values out of range print as given (a JS string or number),
  where .NET prints the integer.
- **Combat.ComputeRawPower** rounds AwayFromZero (cs.js `roundAway`), then `(int)` (truncate), then floors at 0.
  2000 random rows with multipliers 0.5 / 1 / 1.5 match dotnet (`Combat.fixture.json`). `-0` never escapes.
- **Field.MoveEnemy** walks `for (cell = limit; cell != from; cell -= step)`. If `limit` were ever on the far
  side of `from` this would not terminate (in C# too); it cannot happen while nobody overtakes (an enemy's
  Cell is always ≥ the player's FarCell + 1 and ≤ the line's end), so it is ported as is.
- **Reach.Default / CarriedTools.None** are static singletons (`Reach.Default === Reach.Default`).
  `Face.StatusList`, `BattleHistory.Cards` etc. return one shared **frozen** empty array for null (C#
  `Array.Empty<T>()`); a caller that tries to push onto it throws instead of silently corrupting it.
- `BattleState.History` returns a **new** `BattleHistory()` each call when `PlayerHistory` is null, as the C# does.
- `AdaptationView.HasHeld` / `TreeSwitch.Override` compare with `===` (C# `StringComparison.Ordinal`).
- `TreeSwitch.Holds` is a JS function `(AdaptationView) => boolean` stored as a field (C# `Func<,>`);
  call it as `sw.Holds(view)`.
- `EnemyDef.Actions` is a `Map<string, EnemyActionDef>` (C# `IReadOnlyDictionary`).

## Equality caveat (cs.js, not changed here)

`csEquals` compares arrays element-wise. C# record equality compares `IReadOnlyList` members by
**reference** (List<T> has no value equality). Nothing in BattleCore compares records that hold lists
(checked: no `==` / `Equals` on BattleState / CombatantState / BattleHistory), so the difference is only
visible in tests. Also note JS `indexOf` / `includes` on arrays of records use `===`, while C#
`List.IndexOf` / `Contains` use record (value) equality.

## Not 1:1

- `IRng` is an interface: exported as an empty frozen object so `import { IRng }` resolves.
- `BattleEvent` is `abstract` in C#; JS does not stop `new BattleEvent(...)`.
- Records are not frozen (defineRecord does not freeze); treat them as immutable as C# does.
- `ConstantsTableTests` reads the canon from `.claude/docs/battle_document/battle_core_v4.md`
  (read-only) and is skipped when that file is absent, as the C# test is ignored in Unity.

## Verification

- `scratch-csharp/foundation` (compiles `$U/BattleCore/*.cs` by link, output under scratch only) dumped, by
  reflection, every record of Types.cs / BattleEvents.cs / Field.cs (68 types: positional parameters in order,
  C# defaults, init-only properties, getters, methods, base type) and every static class (Constants, Columns,
  Combat, Field, AttributeRule, EnumTokens: every public field value and method name). The JS exports match
  all of them with no extra members.
- Differential runs against the C#: 3000 random `Field.Move` calls on lines of 1..3 enemies (with
  `Nearest` / `Living`), 1500 random `Field.Validate` setups (exception type and message), 500
  `EnemyStartCell` rows, 3000 random faces through the four `AttributeRule` functions and `GivesFoeStatus`,
  2000 `ComputeRawPower` rows, 8000 RNG values: 0 mismatches. Subsets are kept as `test/battle/*.fixture.json`.
- Every module loads cleanly when imported first (no TDZ across the Types ↔ Attributes / Field / Combat cycle).

## Test status / blocker

`Types.js` and `Combat.js` import `Statuses.js` (StatusSet.Empty, Statuses.MoveReduction) exactly as the C#
does. `Statuses.js` (another agent's) did not exist when this was written, so the tests load their modules
through `test/battle/_foundation_deps.mjs` and skip with `needs Statuses.js` instead of failing to load:
in the game tree 33 pass / 100 skip. Run against a throw-away mirror with a minimal StatusSet stub, all
132 run and pass (1 more, the TurnLoop cross-check in Field.test.mjs, skips with `needs TurnLoop.js + Cards.js`
until those load). Once `Statuses.js` lands, rerun
`node --test test/battle/{IRng,Attributes,Types,Constants,Columns,Combat,Field,BattleEvents}.test.mjs`.
The `CellsAfterSlow` row builds its StatusSet with `StatusSet.Of({ Kind, Stacks })` and falls back to
`StatusSet.Of([kind, stacks])`; adjust if the Statuses port chose another tuple shape.
