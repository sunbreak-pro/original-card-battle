# Port notes: TurnLoop.cs and EnemyAi.cs

Files: `src/core/battle/TurnLoop.js` (from `unity-port/BattleCore/TurnLoop.cs`, 2146 lines) and
`src/core/battle/EnemyAi.js` (from `unity-port/BattleCore/EnemyAi.cs`, 366 lines).
Tests: `test/battle/TurnLoop.test.mjs`, `test/battle/EnemyAi.test.mjs`, helpers in
`test/battle/_turnloop_fixtures.mjs` (a port of `BattleCore.Tests/Fixtures.cs`).

## Shape of the JS

- `TurnLoop` is a frozen object holding exactly the 13 C# `public static` methods (Start,
  BeginPlayerTurn, CanPlay, CostNow, Preview, PreviewOmen, OmenBoundaryCells, OmenMoveCell,
  PreviewRecovery, PlayCard, CanTakeFreeStep, TakeFreeStep, EndTurn). Every private method is a
  module-level function with its C# name (Resolve, OpenTurnFor, EnemyPhase, Execute, MoveSelf,
  ApplyStatus, Ward, DecideNextOmen, ...), so a line-by-line review against the C# works. Note the
  private `Set(state, actor, unit, value)` shadows the global `Set` inside TurnLoop.js only; the module
  never uses the global.
- C# `out` parameters of private methods return objects: `OnFall` → `{ state, over }`, `Execute` →
  `{ state, fell }`, `Ward` → `{ state, warded, wardedCap }`. Private tuples `(int Raw, int Damage)`
  return `{ Raw, Damage }`.
- `BattleSetup`, `PlayPreview`, `OmenPreview`, `FocusStep` are records with the same named-argument
  convention as `Types.js`: a trailing plain object of parameter names is read as C# named arguments
  (`new BattleSetup(enemy, deck, 6, { StartGap: 2, MoreEnemies: [...] })`). Other agents already call
  BattleSetup this way. BattleSetup's Constants defaults (`StartGap`, `PlayerStartCell`, `PlayerMaxHp`,
  `PlayerMaxStamina`) are function defaults, evaluated at construction, so no Constants value is read
  at module-evaluation time across an import cycle. `BattleSetup.SliceFieldCells` is a static field (6).
- `PlayRefusal` is `defineEnum` (member-name strings), so the C# message
  `Card "x" cannot be played: OutOfReach.` comes out byte-identical.
- `EnemyAi` is a plain object. `RestLabel` (C# `static readonly`) is a lazily-built cached getter: one
  instance, but `OmenLabel` from Types.js is not touched at module-evaluation time.
- The C# extension method `OmenLabel.ToText(this OmenLabel label)` (declared in EnemyAi.cs) is
  `EnemyAi.ToText(label)`. `gap.ToBand()` is `EnumTokens.ToBand(gap)` (Types.js). `Reach.ToText()`
  stays an instance method of Reach.
- Event init-only properties (`new X(...) { Unit = i }`, `PushRefused.ByStance`,
  `StatusApplied.Dropped / Warded`) are set with the record's `with` (`U(event, unit)` helper), which
  BattleEvents.js supports. Events the C# builds without `{ Unit = ... }` are left at BattleEvents'
  default 0, exactly as in C#.
- Exceptions carry the .NET messages: `ArgumentNullException` → `Value cannot be null. (Parameter 'x')`;
  `ArgumentException(msg, name)` → `msg (Parameter 'name')`; `ArgumentOutOfRangeException(name, value,
  msg)` → `msg (Parameter 'name')\nActual value was v.` (Linux newline, as the oracle runs on Linux).
  `KeyNotFoundException` for an unknown action id: `The given key 'id' was not present in the dictionary.`
- `def.Actions` (`IReadOnlyDictionary`) is read as a `Map` (`get` / `values()`), per CONVENTIONS;
  insertion order is the C# enumeration order (Enemies.js builds it in declaration order).

## C# semantics carried over deliberately

- `List.Remove(card)` (hand) removes the first element equal by record equality: `removeFirst` uses
  `csEquals` (reference equality short-circuits first).
- `List<int>.Sort` (Reached, PushOrder) is unstable in .NET; the keys are the standing enemies' cells,
  which are always distinct (one enemy per cell), so a stable JS sort gives the same order.
- `Half(value)` is `(value + 1) / 2` in int arithmetic → `idiv`.
- `Math.Sign` → `Math.sign` (only ever on non-zero ints here).
- `foreach` enumerations of lists that are replaced during the loop (`self.StanceList` in
  StanceAtTurnEnd / OpenTurnFor, `state.Player.StanceList` in MoveSelf / MoveBySwitch, `state.Living`
  in BeginPlayerTurn) iterate the list as it was when the loop started, as C# does.
- `RevisePlan` compares the planned and the new omen with C# record equality (`csEquals`).
- `string.Equals(active?.Id, enemy.ActiveSwitch, Ordinal)`: null and null are equal; `undefined` is
  normalised to `null` before comparing.
- The TraitContext the enemy reads leaves `HandAfterPlay` and `OpponentOmen` at their defaults
  (int.MaxValue / null): `undefined` is passed so the record default applies.

## C# quirks ported as they are (not bugs fixed)

- `OpenTurnFor`'s enemy 俊敏 step reads the omen through `EnemyAi.ActionToExecute(def, omen, stamina)`
  with no active switch (the 4th argument is left null), unlike EnemyPhase, which passes the switch the
  omen was decided on. Under a switch with an override of the same id, the free-step direction is read
  off the base action's reach. Kept.
- `StanceHolds` in `OpenTurnFor` reads `state` from before the turn-start reset (so
  `MovedThisTurn` for a turn-start stance sees last turn's Moved flag). Kept.
- `PreviewOmen` does not clear the player's `FreeStep` in its step-8 copy while `PreviewRecovery` does;
  neither reads it afterwards, so it changes nothing. Kept as written.
- In `EnemyPhase`, when an enemy falls to its own 見切り return while other enemies stand, the phase
  returns without `CheckDefeat` (no DefeatChecked event for that unit). Kept.
- In `Resolve`, when the player falls mid-attack the remaining faces are skipped but `PlayCard` still
  records the play (RecordPlayed, history, discard / exile) before CheckDefeat. Kept.

## Not 1:1 (and why)

- Nothing: every branch, constant, event, event order and RNG call order follows the C#. The only RNG
  calls are `Cards.Shuffle` in Start and `Cards.Draw` (one card at a time) in DrawCards; previews resolve
  on a copy with `new SeededRng(0)`, exactly as the C#. C# tuples are read in the object form the sibling
  ports return (`Combat.ApplyGuard` → `{ Damage, GuardAfter, Absorbed }`, `Cards.DiscardHand` →
  `{ Hand, DiscardPile }`).

## Verified against the C# (2026-10-20)

A reference runner outside the game tree (`scratchpad/turnloop-ref/`: `Program.cs` compiles
`unity-port/BattleCore/*.cs` by link under dotnet 10; `run.mjs` is its JS twin) plays a deterministic
policy (every playable card left to right, every target; the 俊敏 step toward / away by the gap) and logs
every event (all fields, init-only ones included), the full state after every step (both sides, piles,
tools, history with mastery contexts) and, at every decision point, `CanPlay` / `CostNow` / `Preview` for
every hand card × target, `PreviewOmen` / `OmenBoundaryCells` / `OmenMoveCell` per enemy and
`PreviewRecovery`.

- Roster: every enemy of `Enemies.All` × prototype deck × seeds 1-3 on 6 cells, the 80-card deck on 8
  cells at gap 3 and gap 1, every boss with all three tools and with carried HP / stamina, a pair, two
  trios and a refused 4-cell line: 75 battles, 44,885 lines.
- Custom bosses (same definitions in both languages) for the paths the roster reaches rarely: switches
  with overrides, EnterMove, Sway, Cleanse, StopStance, capped boss words under the ward (1 and 2),
  PullBeyond, NotWhileFoeHas / NotAtFoeCap, OnHit (OncePerTurn, status on the attacker) and PushImmune
  stances, 見切り, 俊敏 on an enemy, two actions a phase, a size-2 enemy, 2- and 3-enemy lines: 14 battles,
  12,764 lines.

Both logs from the JS port are byte-identical to the C# ones.

## Tests ported

From `BattleCore.Tests`: TurnLoopTests (all, including the pinned three turns under seed 20260921 and
the replay test), MultiEnemyTests (the TurnLoop ones), TwoEnemyBattleTests, OmenPlanTests,
PreviewTests, SwiftAndFocusTests, FloorReadTests, StanceStopTests (three TurnLoop ones),
DepartureToolTests, BossStatusTests / BossSwitchTests / PriestAwayTests (the cases that drive TurnLoop or
EnemyAi), PolearmTests (EnemyAi half), EnemyTreeReachTests (the full tree walk over the roster), plus
fixture-only cases for every EnemyAi member, the .NET exception messages, the BattleSetup getters and
CostNow with a コストの割引 stance.

The test files load the battle modules dynamically (`loadDeps` in `test/battle/_turnloop_fixtures.mjs`):
while a sibling module is missing, a suite reports `# SKIP needs X.js` instead of failing to load; any
other load error is re-thrown. With the whole core present: 228 tests (174 in TurnLoop.test.mjs, 54 in
EnemyAi.test.mjs), all passing.
