# Port notes: BattleCore cards, deck rules, traits, statuses, chain

Files: `Cards.cs`, `CardCatalog.cs`, `CardCatalog.Initial.cs`, `CardCatalog.Learned.cs`, `DeckRules.cs`, `Traits.cs`,
`Statuses.cs`, `Chain.cs`, `ChainOrder.cs`
→ `src/core/battle/{Cards,CardCatalog,CardCatalog.Initial,CardCatalog.Learned,DeckRules,Traits,Statuses,Chain,ChainOrder}.js`.

Tests: `test/battle/{Cards,CardCatalog,CardCatalog.Initial,CardCatalog.Learned,DeckRules,Traits,Statuses,Chain,ChainOrder}.test.mjs`,
helper `test/battle/_battle_cards.mjs`, C# dump `test/battle/BattleCards.fixture.json`.

## How to use these modules from other code

### CardCatalog (one C# `partial` class over three files)

- `import { CardCatalog, PrototypeDeck } from './CardCatalog.js'` is the catalog everyone uses: the eighty cards
  under their C# names (`CardCatalog.Thrust` … `CardCatalog.LastStand`), `CardCatalog.All` (canon order, #1 first)
  and `CardCatalog.ById(id)`.
- `CardCatalog.Initial.js` / `CardCatalog.Learned.js` each export their part as `CardCatalog` (a frozen object with
  the 40 static members that C# file declares). Only the merged object should be imported by game code.
- The trait / status shorthands (`AtMost`, `AtLeast`, `Reserve`, `When`, `Combo`, `OmenIs`, `FoeHas`, `SelfHas`, `Foe`,
  `Self`) are `private static` in C#. They are exported from `CardCatalog.js` (hoisted `function` declarations) only so
  the two parts can call them; they are not CardCatalog members.
- Cycle safety: `CardCatalog.js` ↔ parts is an import cycle. The merged object's card members, `All` and
  `PrototypeDeck.Kinds` are lazy getters (All / Kinds are built once on first read and cached), so any of the three
  modules can be the first one loaded. (This is also the C# rule: `All` is assigned in the static constructor, after
  every field initializer of every part has run.) Checked by loading each of my modules as the entry.
- `CardCatalog.All`, `PrototypeDeck.Kinds`, each part object, the merged object, and every `Face.Statuses` list of
  the catalog are frozen (C# `static readonly` / `IReadOnlyList`). Mutating them throws a TypeError instead of
  corrupting the shared catalog for the rest of the run.

### Tuples, overloads, named arguments

| C# | JS |
| --- | --- |
| `var (hand, discard) = Cards.DiscardHand(h, d)` | `const { Hand, DiscardPile } = Cards.DiscardHand(h, d)` |
| `var (hp, st) = Chain.Carry(state)` / `Chain.Rest(...)` | `{ Hp, Stamina }` |
| `StatusSet.Of((StatusKind.Slow, 2), (k, n))` | `StatusSet.Of({ Kind, Stacks }, ...)`; `[kind, n]` and `{ Item1, Item2 }` are accepted too, and so is one array of tuples (C# `StatusSet.Of(words.ToArray())`). A lone `[kind, n]` (string, number) is read as one tuple. |
| `set.Add(kind, 2, kindLimit: 6)` | `set.Add(kind, 2, 6)` or `set.Add(kind, 2, { kindLimit: 6 })` / `set.Add(kind, { kindLimit: 6 })`; null / undefined is no limit |
| `new TraitContext(Gap: 2, Moved: true)` | `new TraitContext({ Gap: 2, Moved: true })` (same named-argument rule as Types.js records: a trailing plain object of parameter names; a typo throws) |
| `new TraitOutcome(true, PowerBonus: 3)` | `new TraitOutcome(true, { PowerBonus: 3 })` |
| `new Departure(deck, tools, cons, 0, talent, StartStaminaBonus: 3)` | `new Departure(deck, tools, cons, 0, talent, { StartStaminaBonus: 3 })` |
| `departure.SetupFor(enemy, cells, playerStartHp: 20)` | `departure.SetupFor(enemy, cells, 20)` (positional, C# defaults null) |
| `Cards.MultiHitCarriesNoFoeStatus(CardDef)` / `(EnemyActionDef)` / `(BattleAttribute, Face, IReadOnlyList<Trait>)` | one method: 2+ arguments (or a number first) is the three-argument overload; an `EnemyActionDef` is the action overload; anything else is a CardDef (a lone null throws on `def`, the CardDef overload's parameter) |
| `IReadOnlyCollection<string> ownedKinds` | any iterable of ids (array or Set) |
| `IEnumerable<BattleEvent> events` (Chain.Tally) | any iterable |

- `BattleTally.Attributes` is a `Map<BattleAttribute(number), int>`; `BattleTally.Order` is `[1, 2, 4, 8]`
  (Attack, Guard, Skill, Stance as their `[Flags]` numbers, literal so nothing is read from Types.js at load).
- `ChainBattle.ToString()` / `StatusSet.ToString()` keep the C# name; `toString()` aliases them for template literals.
- `StatusSet.Equals(other)` / `equals(other)` / `GetHashCode()` (C# unchecked int arithmetic, `Math.imul` / `| 0`;
  pinned against dotnet values). cs.js `csEquals` also compares two sets by their `_stacks` Map entries, which is the
  same rule, so records holding a StatusSet (CombatantState) compare as in C#.
- `StatusSet.toJSON()` is the parity custom shape `{ $type: 'StatusSet', Stacks: { <kind>: n } }` in `Kinds` order.
- `StatusSet.Kinds` is a getter (C# property) and returns a new array on every read (C# `.ToList()`).

### Private C# members

`StatusSet.Clamp` / `ToDictionary`, `Traits.Apply`, `ChainOrder.Single` are module-local functions (not exported,
not static members). The C# `private StatusSet(...)` constructor is a plain JS constructor; only Statuses.js builds
sets with it.

## Exactness notes (not 1:1 in form, identical in behaviour)

- **Catalog data**: the 80 `CardDef`s were converted mechanically from the C# source (a converter script under the
  scratchpad, not in the game tree) and then checked three ways: every one of the 80 against the C# oracle's dump
  (`oracle/out/catalog.json`, `toOracleJson` of the parity harness: all fields including Description, Reach,
  Statuses, Stance, Trait / Grant, Cost and the folded Attribute) — 0 differences, canon order identical; 15 + 12 + 15
  cards pinned by hand from the C# text in the tests; and the canon tallies of `CardCatalogTests` (attribute patterns,
  costs, band table, trait / effect counts, pairs, reaches, …).
- **C# `grant:`-only shorthand calls** (`FoeHas(StatusKind.Bleed, TraitEffect.Status, grant: Foe(...))`) pass the
  default amount `0` positionally in JS.
- **Cards.Validate** ordering: `GroupBy` keeps first-seen groups, then `OrderBy(..., StringComparer.Ordinal)`; JS sorts
  with cs.js `compareOrdinal` (UTF-16 code-unit order, the same as .NET ordinal, also for non-ASCII). Pinned with a deck
  that hits every message at once with ids in mixed case (`Beta`, `_under`, `alpha`, `Dup-0` vs `dup-0`).
- **ChainOrder.Parse** uses .NET `String.Trim()` exactly: .NET strips U+0085 and does not strip U+FEFF, the other way
  round from JS `trim()` (pinned with both). `int.TryParse(s, NumberStyles.None, InvariantCulture)` is ASCII digits only,
  no sign / spaces, value ≤ int.MaxValue (`"03"` → 3, `"+3"`, `" 3"`, `""`, `"99999999999"` → refused), pinned.
- **Chain.Rest**: `maxHp * ChainRestHpPercent` is an unchecked C# int product (`Math.imul`), then `/ 100.0`,
  `Math.Round(AwayFromZero)` (cs.js `roundAway`), `(int)`; `hp + gain` wraps like C# int (`| 0`). Pinned over 42 rows.
- **Chain.MaxStaminaAt** integer division → `idiv`.
- **Exceptions** carry .NET's full `Message` (`(Parameter 'x')`, `\nActual value was v.`), same types, pinned against
  dotnet (e.g. `Unknown status. (Parameter 'kind')\nActual value was 99.`). `ArgumentNullException(param, message)` is
  `"<message> (Parameter '<param>')"` (DeckRules' `Tools is null.`).
- **StatusSet** enumeration order: a JS Map. C# enumerates its Dictionary only in `TickTurnStart` / `Equals`, where
  the order does not show, and lists words through `Kinds` (sorted by enum index), so the difference after a removal
  (Dictionary slot reuse) is never observable.

## C# quirks ported as they are

- `ChainOrder.Parse`: a battle with no `@layer` takes the layer of the one before (the variable outlives the loop
  body). A failed `TryParse` sets the layer to 0 before throwing (unobservable). `"@3"` and `"polearm_warped+"` reach
  `Enemies.ById("")` and throw `KeyNotFoundException` (`Unknown enemy id "".`), not an ArgumentException. `"Nine"`
  (capitalised) is not the nine: it is parsed as an enemy id and refused. Spaces around a layer number
  (`"x @ 3"`) are refused because the number part is not trimmed.
- `Chain.Total`: EnemyId is `""`, Result / HpLeft / MaxHp are the last battle's.
- `DeckRules.Validate` with a null `Deck` throws `ArgumentNullException` on `deck` (from ValidateDeck), not on
  `departure`.
- `Cards.Shuffle` keeps the `if (j > i) j = i;` guard (FixedRng(0.9999999) relies on it never mattering; pinned).
- No C# bug found in these nine files.

## Equality caveat (as in battle-foundation.md)

C# record equality compares `IReadOnlyList` / `IReadOnlyDictionary` members by reference; cs.js `csEquals`
compares them element-wise. That affects only `DrawResult`, `DeckValidation`, `Departure`, `BattleTally`,
`ChainBattle`, `TraitContext.Played` and `TraitOutcome.Grants` equality, which nothing in BattleCore compares.
`TraitOutcome.None` returned by Traits.Evaluate is the shared instance, so `=== TraitOutcome.None` works as the C#
`Is.EqualTo(TraitOutcome.None)` does for it.

## Verification

- `scratchpad/work/cards-oracle` (C# console compiling `$U/BattleCore/*.cs` by link; output under the scratchpad only)
  wrote `test/battle/BattleCards.fixture.json`: shuffles under SystemRng (seeded .NET Random) / SeededRng / FixedRng,
  a 192-row draw matrix (draw × discard × hand × count), every Validate / ValidateEnemyAction / DeckRules message in
  order, CanAdd answers, Departure.SetupFor's BattleSetup, every trait condition (45 + 4 traits × 5 boards), every
  effect (met / unmet / with a grant), 背水の陣 over 16 boards, 12 StatusSets (kinds, stacks, hash, text),
  RecoveryLoss over 192 rows, StayedTwoTurns, Rest (42 rows), MaxStaminaAt (−5..105), Chain.Total, ChainOrder's
  default / nine / densities and 26 Parse specs, and the exceptions. Every value matches.
- All 80 catalog cards match the C# oracle dump field for field (CardCatalog.test.mjs, runs whenever
  `oracle/out/catalog.json` exists).
- With Statuses.js in place, the foundation's tests that were skipping on it (Types / Combat / Field / Attributes /
  Columns / Constants / BattleEvents / IRng / EnemyAi) all run and pass.
- In a throw-away mirror with minimal stand-ins for Mastery.js and Enemies.js (scratchpad only), all 135 of my tests
  run and pass (the oracle cross-check skips there only because the mirror has no oracle dump).

## Test status / blockers

In the game tree: 135 tests, 108 pass, 27 skip, 0 fail. The skips are waiting on other agents' modules:

- `DeckRules.test.mjs` (16) — DeckRules.js imports `BattleSetup` from TurnLoop.js, and TurnLoop.js imports
  `Mastery.js` and `Enemies.js`: the module cannot load until both exist (`needs Mastery.js`).
- `ChainOrder.test.mjs` (5) — ChainOrder.js imports `Enemies.js` (and TurnLoop.js) (`needs Enemies.js`).
- `CardCatalog.test.mjs` (2: every card played; random decks fight the polearm) and `Chain.test.mjs` (4: carried
  starts, the next battle, the tally of a fought battle) — they drive TurnLoop.

Rerun when Mastery.js and Enemies.js land:
`node --test test/battle/{Cards,CardCatalog,CardCatalog.Initial,CardCatalog.Learned,DeckRules,Traits,Statuses,Chain,ChainOrder}.test.mjs`.
