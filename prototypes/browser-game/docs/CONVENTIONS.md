# Porting conventions (C# → JavaScript)

The game's rules are the C# code in `unity-port/` (the C# is the source of truth for battle and exploration). We port it to JavaScript **faithfully**, so the browser game plays exactly like the Unity build. A C# oracle replays the same scenarios and a parity harness diffs the two (see `docs/PARITY.md`). Anything that changes behaviour is a bug.

Workspace root: `.` (below: `$G`). C# root: `unity-port` (below: `$U`).

## Module layout

- ES modules (ES2022), no dependencies, run under Node 22 (`node --test`) and bundled by esbuild into one classic script for the browser.
- **One JS module per C# file**, at the path given in `docs/TYPE_MAP.md`. Export every public/internal type of that file under **the same name**. Import other types from the module TYPE_MAP lists for them (relative path, `.js` extension). Circular imports between our modules are fine as long as nothing is read at module-evaluation time across the cycle (wrap catalog building in functions / lazy getters if needed).
- First line of every module: `// Port of unity-port/<path>.cs` (or `unity-project-kit/...`).
- The shared helpers are in `src/shared/cs.js` — use them; do not re-implement them.

## Names

- Keep C# identifiers **exactly**: type names, static method names, property / field names, enum member names, record parameter names (PascalCase stays PascalCase: `state.Player.Hp`, `TurnLoop.PlayCard(...)`). Private fields `_x` stay `_x`. Locals are free.
- Reason: 1:1 review against the C# and JSON parity with the oracle (which serializes with the C# member names).

## Types

| C# | JS |
| --- | --- |
| `record` / `sealed record` / `record struct` | `class X extends defineRecord('X', ['A', 'B', ['C', dflt]]) { getters / methods }`. Never mutate after construction; copy with `.with({...})` exactly where C# uses `with`. |
| record inheritance (`abstract record BattleEvent(Actor Actor)`, `sealed record TurnStarted(Actor Actor, int Turn) : BattleEvent(Actor)`) | base: `defineRecord('BattleEvent', ['Actor'])`; derived: `defineRecord('TurnStarted', ['Turn'], BattleEvent)` — the derived constructor takes the base fields first, then its own (`new TurnStarted(actor, turn)`). |
| class with mutable state | JS class, same members. |
| static class | `export const X = { Member(...) {...}, CONST: 3 }` or a class with static members. |
| plain enum | `defineEnum(['A', 'B', ...])` → values are the member-name strings. `(int)e` → `enumIndex(E, e)`; `(E)i` → `enumAt(E, i)`. If the C# assigns explicit values to a plain enum, use a numeric map instead and say so in a comment. |
| `[Flags]` enum | `defineFlags({ None: 0, Attack: 1, ... })` → numbers; port bit operations exactly. |
| `interface` | nothing to export at runtime unless code checks it; export a JSDoc typedef comment or an empty object with the same name if something imports it. |
| `List<T>`, `IReadOnlyList<T>`, arrays | JS arrays (`.Count` → `.length`). |
| `Dictionary<K,V>` | `Map` (C# enumeration order = insertion order when nothing was removed; if the C# removes then enumerates, note it). `HashSet` → `Set`. |
| `null`, `T?` | `null`. `default(int)` → 0, `default(bool)` → false, `default(string)` → null. |
| tuples `(int A, int B)` | plain object `{ A, B }` (or `{ Item1, Item2 }` when unnamed). |
| `out` parameters | return an object `{ ok, value }` (document it); keep the same method name. |

## Semantics that differ between C# and JS

- Integer division `a / b` on ints → `idiv(a, b)`. `%` is the same.
- `Math.Round(x)` → `roundEven(x)` (C# default is banker's rounding). `MidpointRounding.AwayFromZero` → `roundAway`. `(int)someDouble` → `Math.trunc`. `Math.Clamp` → `clamp`.
- LINQ `OrderBy`/`ThenBy`(`Descending`) are stable → `stableSortBy(arr, f1, {f: f2, desc: true})`. String keys: ids are ASCII, compare ordinally (`compareOrdinal`); flag any non-ASCII sort.
- `Max`/`Min`/`First` on empty sequences throw in C# — use the helpers so behaviour matches.
- String text must be **byte-identical** (Japanese full-width punctuation, spaces, `〜`, `−` vs `-`). Interpolations → template literals.
- `foreach` over a collection that is modified in the loop throws in C#; reproduce only if the C# relies on it (it should not).
- RNG: `IRng.NextDouble()`; `SeededRng` is SplitMix64 on `ulong` — port with BigInt exactly (see `core/battle/IRng.js`). Every call order must match the C# so seeded runs replay identically.
- Exceptions: `throw new InvalidOperationException(msg)` etc. from `cs.js`, same messages.

## Tests

Each porting agent writes `test/<area>/<File>.test.mjs` (node:test + node:assert/strict) for the files it ports. Port representative cases from the matching C# test project in `$U/*Tests/` (at least the cases that pin numbers: costs, damage, ranges, tree choices, map shapes, texts). Run `node --test test/` before finishing; all of yours must pass.

## Don'ts

- Do not "improve", simplify, or re-balance anything. Port bugs too (note them in `docs/PORT_NOTES.md` under your file's heading).
- Do not edit files owned by another agent (see the work split in your prompt). If you need a symbol another agent owns, import it by its C# name from the module TYPE_MAP gives — it will exist.
- Do not touch `/home/user/original-card-battle` (read-only source), and do not publish anything.
