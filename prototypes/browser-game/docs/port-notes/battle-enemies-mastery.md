# Port notes: BattleCore Enemies + Mastery

Files: `unity-port/BattleCore/Enemies.cs`, `unity-port/BattleCore/Mastery.cs`
→ `src/core/battle/Enemies.js`, `src/core/battle/Mastery.js`.
Tests: `test/battle/Enemies.test.mjs` (140), `test/battle/Mastery.test.mjs` (47), all passing.

Ported test sources: `EnemyRosterTests` (all cases, incl. every enemy fought to the end over 6 seeds),
`EnemyTreeReachTests` (the whole tree walk), the roster half of `BossSwitchTests` (every adaptation /
stage condition on a hand-built `AdaptationView`, the reshaped trees and overrides, and the in-play cases:
breaking staff, ガルド stage 2 step, root sway, root_sk cleanse), `MasteryTests` (all), and
`MasteryProfilesTests` (all, including the §4.6 table test, which reads the canon read-only from
`.claude/docs/card_document/swordsman_cards_v4.md` and is skipped when
that file is not there, as the C# test is in Unity).

Parity against the C# oracle (`node test/parity/run.mjs`): `catalog` 99/99 (every EnemyDef with its
switches, overrides and `Cards.ValidateEnemyAction`; every card's `Mastery.Master` steps 1-3;
`GapHolds` 0..5; the `MasteryProfiles` table) and `battles` 490/490 (all thirteen enemies, the boss
switches and `BattleHistory.MasteryPlays`) pass. Running the harness rewrites
`test/parity/report.json` (its normal output).

## Shape of the exports

- `Enemies` is a frozen object. `PolearmWarpedId` is a plain string; the thirteen EnemyDefs,
  `MultiHitRedesignPending` and `All` are getters over a roster **built once on first read** (all at
  once, in C# declaration order), the way a C# type initializer runs every static field initializer
  before the first member is used. Nothing from another module is read at module-evaluation time, so
  the `TurnLoop ↔ Enemies` and `Types ↔ Statuses` cycles are safe. `CardCatalog.js` does the same.
- `EnemyDef.Actions` is a `Map` (insertion order = roster order). `All` is a frozen array.
  `MultiHitRedesignPending` is a `Set` (C# `IReadOnlyCollection<string>` over a `HashSet`).
- `TreeSwitch.Holds` is a JS function `(AdaptationView) => boolean`. A stage's own switch gets
  `(view) => stage.Holds(view)` (the C# method group, bound).
- `MasteryProfiles.Observe` / `All` are getters built together on first read (so `Find('observe') ===
  Observe`, as the test pins). `Mastery.MovementGuardByRank` / `MovementExtraByRank` are frozen arrays.
- `MasteryContext` is `[Flags]` → `defineFlags` (integers, as the oracle writes them); `MasteryKind`
  is `defineEnum`.
- `MasteryTally.Ticks` and `MasteryBook.Records` are `Map`s. `Rerank` builds `Records` in ordinal id
  order, as the C# `OrderBy(..., StringComparer.Ordinal)` does, so the oracle JSON matches key order.

## Not 1:1 (and why)

- **Records with C# named arguments.** Mastery's records use the same trailing-plain-object
  convention as `Types.js` (`new MasteryProfile(id, kind, { RankNames: [...] })`). The local
  `record()` helper is a copy of `Types.js`'s (that one is not exported).
- **Private records** `Tree`, `Adaptation`, `Stage` are module-private. `Tree` is a `defineRecord`
  (it needs `with` for `tree with { One = ... }`); `Adaptation` / `Stage` are plain classes whose C#
  named parameters are a trailing options object: `new Adaptation(Id, Holds, Reshape, Overrides = null,
  { Cleanse, Retouch })`, `new Stage(Id, Crossed, Reshape, Overrides, { EnterMove, Sway, Without })`.
  `Build(...)` and `Layered(...)` take their C# named parameters the same way.
- **Overloads** are dispatched on the argument:
  - `MasteryProfiles.Find(cardId)` / `Find(profiles, cardId)` by the argument count;
  - `MasteryTally.Of(BattleState)` / `Of(IEnumerable<MasteryPlay>)`: an iterable (array, Set,
    generator) is the play list, anything else is a state (`null` → `ArgumentNullException('state')`);
  - `MasteryBook.AfterBattle(BattleState)` / `AfterBattle(MasteryTally)`: `instanceof MasteryTally`.
- `Loosened(card, condition, threshold:, attribute:, omen:)` → `Loosened(card, condition, { threshold,
  attribute, omen })`.
- The C# private members of `MasteryBook` (`Find`, `TickTable`, `Rerank`) are ordinary methods on the
  class (JS has no `private` on a record copied with `with`); nothing outside calls them.
- `SlowStacks` (`private const int = Constants.StatusApplyDefault`) is a function, so `Constants` is
  read when the roster is built, not at module load.
- Exception messages follow .NET's `Message` (`ArgumentNullException`: `Value cannot be null.
  (Parameter 'x')`; `ArgumentOutOfRangeException(param, value, msg)`: `msg (Parameter 'x')\nActual
  value was v.`), which the parity harness cuts back the way the oracle does.
- `MasteryTally.TicksOf(null)` throws `ArgumentNullException('key')`, as `Dictionary.TryGetValue(null)`
  does in C#.

## C# quirks kept as they are

- `Tree.All(params string[] ids)` hands **one array** to all three bands, so an adaptation built with it
  (turtle, snap, root_a, root_sk) has `BranchAtGapZero === BranchAtGapOneToTwo === BranchAtGapThreePlus`.
  Harmless (nothing mutates branches); kept for fidelity.
- `Drop` uses C# `dropped |= id == each` (no short-circuit); ported as `dropped = dropped || ...`,
  which is equivalent since the comparison has no side effect.
- `EverySecondInLastTurn` uses C# integer division (`idiv`).
- `Mastery.ContextsOf` reads the **canon** profile (`MasteryProfiles.Find(def.Id)`) for the 間合い
  context, not the `MasteryBook`'s own `Profiles`: a book begun with custom profiles has their
  `GapContext` ignored during battle. Ported as is (TurnLoop calls it this way).
- `Mastery.ContextsOf` computes `context.StaminaBefore - paid` in C# `int`; `StaminaBefore` defaults to
  `int.MaxValue`, which would wrap only for a negative `paid`. Costs are never negative, so JS (no
  wrap) gives the same result.
- `Mastery.GapHolds` reads only the first 間合い trait of a card (the C# comment says so); kept.
- C# record equality on `MasteryBook` compares `Records` / `Profiles` by reference; `csEquals` is
  structural. Nothing in the game compares books.

## For other agents

- `src/app/life.js:83` calls `MasteryBook.FromRecords` (no such member in C#, so `undefined`) and then
  falls back to `this.Mastery.With({ Records: records })`. The record copy method is lower-case
  `with` (`src/shared/cs.js`), so that line throws `TypeError` when an heir inherits cards. It should be
  `this.Mastery.with({ Records: records })` (the Map `Inh.mergeMastery` returns is the right shape).
  Not changed here: `life.js` is the integrator's file.
