# Port notes: exploration bridge, exploration script, journal

Files (C# → JS):

| C# (`unity-project-kit/Assets/View/…`) | JS (`src/bridge/…`) |
| --- | --- |
| `Exploration/Bridge/BattleRewards.cs` | `exploration/BattleRewards.js` (BattleReward, CardPile, BattleRewards) |
| `Exploration/Bridge/EncounterBattle.cs` | `exploration/EncounterBattle.js` (BattleHandIn, BattleReturn, FoughtBattle, EncounterBattle) |
| `Exploration/Bridge/Expedition.cs` | `exploration/Expedition.js` (Expedition) |
| `Exploration/Bridge/UniqueCards.cs` | `exploration/UniqueCards.js` (UniqueCardReward, UniqueCards) |
| `Exploration/Script/EnemyIntel.cs` | `explorationScript/EnemyIntel.js` (NodeIntel, IEnemyIntel, NoEnemyIntel) |
| `Exploration/Script/ExplorationScreen.cs` | `explorationScript/ExplorationScreen.js` (NodeStanding, ScreenAction, CardKind, Overlay, StepForecast, StepOption, NodeCard, EdgeLine, MiasmaGauge, SlotCard, CardButton, ScreenCard, OverlayPage, ExplorationScreen) |
| `Exploration/Script/ExplorationScreenBuilder.cs` | `explorationScript/ExplorationScreenBuilder.js` (ExplorationScreenBuilder) |
| `Exploration/Script/ExplorationSession.cs` | `explorationScript/ExplorationSession.js` (RunStart, ExplorationSession) |
| `Exploration/Script/ExplorationText.cs` | `explorationScript/ExplorationText.js` (ExplorationText) |
| `Journal/Script/JournalBook.cs` | `journal/JournalBook.js` (JournalTab, JournalContext, JournalRow, JournalPage, JournalBook) |
| `Journal/Script/JournalTerms.cs` | `journal/JournalTerms.js` (JournalTerms) |

Tests: `test/exploration/*.test.mjs` (one per file) plus three helper modules: `test/exploration/_explore.mjs` (ExplorationScreenTests' internal helpers), `test/exploration/_battle.mjs` (ExpeditionTests' private helpers) and `test/exploration/_needs.mjs` (skips a test while a module it needs is missing). Run with `node --test test/exploration/*.test.mjs`. Under Node 22, `node --test test/exploration/` fails: it reads a directory argument as a file name.

## Test status and verification

- `node --test test/exploration/*.test.mjs`: 127 tests. 74 pass and 53 skip; none fail.
- Every skipped test reads `needs src/core/battle/CardCatalog.js` (and, for some, `Cards` / `DeckRules` / `Mastery` / `Statuses` / `Chain` / `Enemies`, plus `depictionScript/DepictionTypes.js` for the one test that drives a real battle). The four Bridge test files load their modules through `test/exploration/_needs.mjs`. A missing module turns into `{ skip: 'needs <module>' }`; any other load error still fails. The tests run by themselves once those modules land, with no edit.
- **Checked against the C# itself.** A throwaway console app in the scratchpad (`tools/minioracle`) compiles the original C# sources by link. All build output stays in the scratchpad, and nothing is written under `unity-port`. Its output was diffed line for line against a JS twin (`tools/dump*.mjs`):
  - 18 scripted lives through the exploration screen (35,123 lines): every frame's labels, node cards and float positions, options with their forecasts and warnings, edges, slots, cards over the map, journal pages, the session flags and the overlays. They cover all 12 card kinds, layers 1〜7, the carving, the pushed-out and survivor endings, 瘴気死 on a step and on a descent, falls, a new life, consumables, training marks and Esc. **Byte-identical.**
  - The rewards (540 `BattleRewards.Roll` cases over every layer × node kind × pool encounter × owned set × seed, `DrawConsumable` sequences, `CardPile.Of/Deal`), the terms page body, and the seed arithmetic, including the ulong wrap and `unchecked((int))`. **Byte-identical.** These ran with a stand-in for the card catalogue that carries the C#'s real ids.
  - Six whole expeditions (753 lines, 78 battles, all 7 layers, 2-fight chains, pairs, a loss, boss lessons, an elite offer, 39 picks, 7 training marks), with every fight's end stubbed. **Byte-identical** on everything the bridge computes: hand-ins, launches, carries, returns, journal levels, rewards, the offer, learn and train, and the deck and owned growth. These ran with stand-ins for the unported battle modules. The real battle state, mastery ranks and mastered card names were left out of this comparison.

## Strings

Every string literal in the 11 C# files was checked against the JS: 316 literal fragments, split at the interpolation holes, all found verbatim. The full-width punctuation, `〜`, `−` (U+2212), `→`, `／` and `　` (U+3000) are byte-identical. The checking script is in the scratchpad (`tools/strings.py`, outside the game tree).

## Representation choices (not 1:1, behaviour unchanged)

- **ulong seeds are BigInt.** `RunStart.Seed`, `ExplorationSession.RunSeed`, `LayerSeed`, `EncounterSeed` and `Expedition.RewardSeed` take and return BigInt, wrapped to 64 bits (`BigInt.asUintN(64, …)`), so `unchecked` ulong arithmetic matches. `RunStart.Seed` and the `runSeed` arguments also accept a whole Number, which is converted. `RunSeed + 1UL` wraps `ulong.MaxValue` to 0, as in C#. `BattleHandIn.Seed` is `unchecked((int)EncounterSeed(...))`, ported as `Number(BigInt.asIntN(32, …))`. The next fight's seed `unchecked(Seed + Index * 7919)` is `(… ) | 0`.
- **Saving:** `JSON.stringify` throws on BigInt. A save that keeps `RunSeed` / `RunStart.Seed` must write it as a string (`String(seed)`) and read it back with `BigInt(text)`. `toParityJson` in cs.js already writes BigInt as a decimal string.
- **Object initializers.** `new RunStart { Seed = 3, Hp = 41 }` becomes `new RunStart({ Seed: 3n, Hp: 41 })`: the constructor sets the C# defaults, then `Object.assign`s the init object. `EncounterBattle.NextLaunch` builds `new BattleLaunch()` and assigns the fields in the C# initializer's order.
- **Optional and named parameters are positional.** `new Expedition(start, deck, battleToolIds, talent, intel, journal)`, `new JournalPage(id, tab, title, rows, disclosure)`, `new JournalRow(label, text, needsDisclosure)`.
- **Overloads.** `EncounterBattle.Finish(source)` and `Finish(finished, history)` are one method that branches on the argument count. Fewer than two arguments selects the `CoreBattleSource` overload, so `Finish(null)` throws ArgumentNullException('source') and `Finish(state, null)` throws ArgumentNullException('history'), as in C#.
- **Tuple.** `BattleRewards.NormalChances` returns `{ Card, Consumable, Page }`.
- **Collections.** `Expedition.Owned` is a `Set` (HashSet). `_journal`, `BattleHandIn.Disclosure` and `BattleReturn.Disclosure` are `Map`s, in C# insertion order. `UniqueCards.For`, `CardPile.Deal` and `BattleRewards.Roll` take `owned` as either a Set or an array (IReadOnlyCollection). The `journal` argument of `new Expedition` may be a Map or a plain object, which is read as a Map.
- **Static readonly fields that read another module are lazy.** `Expedition.InitialKinds` is a cached static getter. `UniqueCards`' boss and elite tables and the builder's `CardsPage` / `SettingsPage` are built on first use. This keeps module evaluation order irrelevant; the values are the same objects on every read, as in C#.
- **Private members keep their C# names** (`Begin`, `Apply`, `Arrived`, `Descended`, `CoversMap`, `Take`, `SyncLife`, `BeginLife`, `BattleTools`). Private nested `BattleRewards.Draw` is a module-level class. Private static helpers of static classes are module-level functions.
- **Extension methods** are called as their static classes: `NodeKindTokens.IsCombat(kind)`, `RngExtensions.NextInt / NextChance / Shuffle(rng, …)`. This matches the DungeonCore port.
- **C# float.** `NodeCard.X` / `Y` are computed in single precision as C# does: `X = fround(fround(Column + 0.5) / rowWidth)`, `Y = fround(1 - fround(Row / (RowCount - 1)))`, or 0.5. `MiasmaGauge.Fill` / `NextFill` are `fround(Percent / 100)`. **Parity caveat:** the oracle writes a C# `float` as its shortest single-precision text (for example `0.16666667`). The JS holds the same float32 value as a double (`0.1666666716337204`). The harness should compare these as float32 (`Math.fround(oracle) === js`), or the oracle should widen them to double before writing.
- **Exceptions** carry .NET's message format: `… (Parameter 'x')`, plus `\nActual value was v.` for ArgumentOutOfRangeException(param, value, msg). Each module builds them inline with cs.js's exception classes (no shared helper outside cs.js).
- **toJSON for parity.** Every data class (all classes except the mutable `Expedition`, `EncounterBattle`, `ExplorationSession` and `FoughtBattle`) has a `toJSON()` that writes `$type` first, then the C# public properties in declaration order. It also writes the simple computed properties, because System.Text.Json writes every public getter: `StepForecast.IsLethal`, `StepOption.NeedsConfirm`, `MiasmaGauge.Fill/NextFill`, `ScreenCard.IsShown/IsConfirmation`, `ExplorationScreen.MapIsLive`, `JournalPage.DisclosureLabel/Body`, `JournalBook.ClosingHint/First`, `BattleReward.IsEmpty`, `UniqueCardReward.IsEmpty` and `BattleReturn.Died`. If the oracle's PARITY_SHAPES list leaves any of these out, delete them from that `toJSON`. Constants (`JournalRow.Hidden`, `JournalBook.Heading`, …) are static and are not written.
- `IEnemyIntel` is exported as an empty frozen object; an intel source is any object with `ForNode(node, layer, surveyed)` and `Pages(layer, surveyed)`.
- `ExplorationText.ItemNote` ports `Regex.Replace` with a global JS regex, `/（[^（）]*\.md[^（）]*）/g`, and `string.Trim()` as `.trim()`. The two trims differ only on U+FEFF and U+0085, which no catalogue note contains.

## Quirks kept as in the C#

- `Expedition.Return` draws the reward seed from `Session.PendingBattleNode` *before* `Session.ReportBattle` clears it. That is intended, but it means `Return` after `Abandon` and a manual `ReportBattle` would read node −1. This is the same as C#.
- `Expedition.Learn` clears the offer list in place (`List.Clear()`). A reference obtained earlier from `Offer` empties too. `Return` replaces the list with a new one.
- `BattleRewards.Roll` for a layer-7 `NodeKind.Battle` calls `Page(100)`, which consumes no draw (`NextChance(1.0)` returns before drawing). Then `NextChance(0.5)` decides the card. The draw order matches C# exactly.
- The builder's `ContentLines` takes the combat branch before the per-kind switch, as C#'s `case NodeKind kind when kind.IsCombat():` does.
- `MaxStaminaLabel` falls back to `最大 {max}` when the 3..14 clamp breaks the sum.

No C# bugs were found in these files.

## Dependencies (other agents' modules, imported by C# name)

- DungeonCore: `ExplorationState` (RunPhase, RestChoice), `ExplorationReducer`, `Miasma`, `NodeKind` (NodeKind, NodeKindTokens), `RunLoadout` (ReliefItem, RunLoadout), `SevenLayers`, `IRng` (SeededRng, RngExtensions), `LayerEncounters` (LayerEncounters, Encounter, Fight in tests), `BattleField`. All present; the Script tests pass against them.
- DungeonContent: `ItemCatalogue`, `ItemEffect`.
- BattleCore: `CardCatalog` (CardCatalog, PrototypeDeck), `Cards`, `DeckRules` (DeckRules, Departure), `Enemies`, `Mastery` (MasteryBook, MasteryTally; MasteryPlay and MasteryContext in tests), `Types` (CardInstance, Actor, GameResult), `BattleEvents` (CardPlayed), `Chain`.
  - Assumed: `MasteryTally.Of(plays)` takes an array of MasteryPlay (the C# overload `Of(IEnumerable<MasteryPlay>)`), and `MasteryBook.AfterBattle(state)` takes a BattleState.
  - Assumed: `DeckRules.CanAdd(deck, id, owned)` accepts a `Set` for `owned`.
  - Assumed: `Chain.Carry` returns `{ Hp, Stamina }`.
- Depiction bridge: `BattleLaunch` (fields; `CreateSource()`), and through it `CoreBattleSource` (`State`, `History`; the test driver uses `TryPlay(...)` → `{ verdict }`). The test `Drive` helper imports `DepictionText` and `PlayVerdict` from `src/bridge/depictionScript/DepictionTypes.js`.
