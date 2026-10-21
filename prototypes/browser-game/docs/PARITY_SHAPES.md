# Parity shapes: what the oracle writes

Companion to `docs/PARITY.md`. The C# oracle (`oracle/`, run `dotnet run -c Release --project oracle -- --out oracle/out --shapes-doc docs/PARITY_SHAPES.md`) writes one file per suite to `oracle/out/<suite>.json`, plus `oracle/out/shapes.json` (the per-type member lists below, machine-readable; the harness reads it) and `oracle/out/summary.txt`. The harness is `test/parity/run.mjs`.

## Case files

Each suite file is a JSON array, one case per line:

```
{ "id": "...", "input": {...}, "output": ..., "defs": [ ... ]?, "floats": [ "output.X[*]" ]? }
```

- **`defs` / `$ref`** (size): per case, every object of the suite's *interned* types is written once into `defs` and referenced as `{ "$ref": n }` wherever it appears (structurally equal objects share one `n`; a def may itself hold `$ref`s). The harness expands them before diffing, so the JS side never produces `$ref`. Interned types per suite are listed under each suite below.
- **`floats`**: normalized paths (array indices as `[*]`) of values that were a C# `float` but not inside a float-typed member (raw float results a suite stored). Float-typed members of typed objects are listed per type in `shapes.json` (`floats`). At a float position the harness compares `Math.fround(js) === Math.fround(oracle)`; everywhere else numbers compare exactly, except in `backdrop` (presentation values only), where a double may differ by up to 4 ulps because .NET's `Math.Pow` / `Sin` / `Cos` and V8's differ in the last bit. No battle or dungeon rule uses those functions (only `Backdrop/Script` and `HandFan`).

## Value rules (both sides)

| C# | JSON |
| --- | --- |
| record / class / struct of the game's namespaces (`BattleCore`, `DungeonCore`, `DungeonContent`, `Depiction`, `Exploration`, `Journal`, `Backdrop`, `BattleCore.Sim`) | `{ "$type": "<C# type name, no namespace>", ...members }` |
| plain enum | member name string |
| `[Flags]` enum (`BattleAttribute`, `MasteryContext`) | integer |
| `Dictionary` / `IReadOnlyDictionary` / `SortedDictionary` | object, keys `String(key)` (enum keys by name), enumeration order |
| `HashSet`, `List`, arrays, any other `IEnumerable` | array in enumeration order |
| value tuple | object with the tuple element names (`[TupleElementNames]`, e.g. `SimDeck.Rows` → `{ "CardId", "Copies" }`), else `Item1`… |
| `int` and smaller | number |
| `long` / `ulong` | number when |v| ≤ 2^53, else decimal string (e.g. `ExplorationSession.LayerSeed`). JS: a BigInt is written the same way. |
| `double` | number (round-trip); `NaN` / `Infinity` / `-Infinity` as strings |
| `float` | the float's exact double value (compared through `Math.fround`, see above) |
| `null`, `T?` without value | `null` |
| delegate (`TreeSwitch.Holds`) | member left out |
| exception | `{ "$error": "<type name>", "Message": "<constructor message>" }`. .NET's ` (Parameter 'x')` and `Actual value was …` suffixes are cut, so the message is the string the C# passed. A .NET dictionary indexer miss is `KeyNotFoundException` / `The given key 'k' was not present in the dictionary.` |

### Members of a typed object

- **Records** (C# `record`): every auto-property (positional parameters and `init` properties such as `BattleEvent.Unit`), plus computed properties whose type is a primitive / enum / string (e.g. `CardDef.Cost`, `CardDef.Attribute`, `BattleState.Gap`, `BattleState.Nearest`, `EnemyUnit.Alive`). Computed properties of an object / list type (`CardDef.AllTraits`, `BattleState.Living`, `Face.ReachOrDefault`, `CombatantState.StanceList`…) are left out: they are derived from members that are written.
- **Classes and structs**: every public instance property (computed ones too — `Loadout.Tools => _tools` exposes private state) and every public instance field.
- Never: static members, indexers, delegates. A getter that throws is left out (the JS serializer does the same).
- **Custom shapes**: `StatusSet` → `{ "$type": "StatusSet", "Stacks": { "<StatusKind>": n } }` in `Kinds` order (read with `Kinds` and `Stacks(kind)`). `LayerMap` → its public properties plus `Successors: { "<nodeId>": [ids] }` (every node, `Nodes` order, via `Successors(id)`) and `Fingerprint` (`Fingerprint()`).
- The JS serializer (`test/parity/run.mjs`, `toOracleJson`) reads, for an object whose constructor has `$name` (or a class name) found in `shapes.json`, exactly the listed members by name (`obj[member]`), so computed C# properties must be **getters** on the JS class, not methods. An object of a type not in `shapes.json` is written with its own enumerable fields.

## Suites

Battles use `BattleCore.Sim` `GreedyPlayer(Positioning.Neutral, maxPlays: 0)` and `Runner.Fight`'s loop, written out in `oracle/src/BattleRun.cs`:

```
rng = new SeededRng(seed); state = TurnLoop.Start(setup, rng)                       → step "Start"
while state.Result == Ongoing && state.Turn < 60:
  BeginPlayerTurn(state, rng)                                                        → step; stop if not Ongoing   [decision point]
  d = greedy.ChooseFreeStep(state); if d != 0: TakeFreeStep(state, d)                → step "TakeFreeStep" Direction  [decision point if PlayerAction]
  played = 0; while Ongoing: c = greedy.Choose(state, played); if null break
      PlayCard(state, c.InstanceId, rng, c.Target)                                   → step InstanceId Target Value(=c.Value)  [decision point if PlayerAction]
  if not Ongoing break; EndTurn(state, rng)                                          → step "EndTurn"
```

Setup (Runner.Setup with every argument): `new BattleSetup(enemies[0], deck.Build(), FieldCells, Constants.StartGap, Constants.PlayerStartCell, Constants.PlayerMaxHp, Constants.BaseMaxStamina, more (enemies[1..] or null), null, null, 0, 0, 0)` with `FieldCells = max(BattleSetup.SliceFieldCells, PlayerStartCell + StartGap + Σ enemy.Size)`.

Decks: `Decks.Archetypes` (6, in order) then `Decks.Initial()`. Enemy setups: `Enemies.All` alone (13, roster order), then every multi-enemy fight in `LayerEncounters.All` (one: `polearm_warped+crossbow_hunter`). Battle id: `<deckId>|<enemy ids joined by +>|<seed>`.

| Suite | Cases | Case ids / input | Output | Interned |
| --- | --- | --- | --- | --- |
| `catalog` | 99 | `card:<id>` `{Id}`; `enemy:<id>` `{Id}`; `table:<Name>` `{Type}` for Constants, Columns, Statuses, PrototypeDeck, MasteryProfiles, Decks | card: `{Def, Profile, Ranks: [Mastery.Master(def, 1..3, profile)], IsStanceCard, StanceStandsAlone, MultiHitCarriesNoFoeStatus, HasMovement, IsMovementCard, IsOpponentDirected, GapHolds0to5}`; enemy: `{Def, ValidateEnemyAction: {actionId: errors}}`; table: a static class's public consts / static fields / static properties in declaration order (+ extras per table) | — |
| `battles` | 490 | 7 decks × 14 setups × seeds 1..5; input `{Deck, Enemies, Seed, FieldCells, TurnLimit: 60, Positioning: "Neutral", MaxPlays: 0}` | `{Steps: [{Call, InstanceId?, Target?, Direction?, Value?, Result: StepResult}], Result, Turns}` (`Error` if the C# threw) | CardDef, EnemyDef, CardInstance, EnemyActionDef, Omen, StatusSet, Trait, StanceDef, BattleHistory, PlayerTurnEnd, MasteryPlay, CombatantState, EnemyUnit, StanceEntry, CarriedTools, every BattleEvent |
| `previews` | 28 | first 2 battles per setup (decks 0 and 1, seed 1); input = battle input + `Calls` (the recorded calls, replayed instead of the greedy) | per decision point `{Step (index into Calls), Cards: [hand card × target 0..Enemies.Count-1: {InstanceId, Target, CanPlay, CostNow, Preview}], Omens: [unit: {Unit, PreviewOmen, OmenBoundaryCells, OmenMoveCell}], PreviewRecovery, CanTakeFreeStep, GreedyValue (GreedyPlayer.Value), GreedyIncoming, Texts: [hand card: {InstanceId, ReachesNobody, Lamps: {Result, PerEnemy}, HpLoss}]}` | as battles |
| `texts` | 568 | `card:<id>` / `card:<id>@<rank>` `{Card, Rank}` (rank = `Mastery.Master` step); `status:<Kind>`; `chips:<name>` `{Statuses, Stances}`; `action:<enemy>:<Actions or Switch:id>:<action>`; `omen:<enemy>:<n>` `{Enemy, Omen, Preview}` (every distinct omen / plan shown at the decision points of the previews battles) | card: `CoreText` FaceOf / KindOf / KindWord / SystemOf / ReachHint / ReachText / Describe / ValueOf / FacePower / TraitLines / StanceText, per trait TraitLine / ConditionWord / EffectWord + `CardFaceText` Condition / Effect / ConditionClause / EffectClause, and `CardFaceText` Values / TextLines / LastingLines / TraitRows / HoverLine / Detail / KindLine / TermsOf / TermMarks / AttributeWord, `CoreText.BoundRefusal(name)`; status (stacks 1, 2, 3, 5): `StatusChipText` Chip / Effect / KindOf / GroupWord / Decay, `CoreText` GlyphOf / MoveBlockedLine, ToToken / ToLabel; chips: `StatusChipText.Chips`, `StanceChips`, `CoreText.Chips(set, list)`; action: SystemOf / Describe / ValueOf / FacePower / TraitLine / StanceText and per OmenKind IconOf(kind, action) / OmenStatusesOf / OmenValueOf(kind, action, null); omen: `OmenOf(omen, enemy, preview)`, `OmenOf(omen, enemy)`, OmenKindWord, ReachText | CardDef, EnemyDef |
| `scripts` | 28 | `writer:<battle>` (first deck, seed 1, per setup; input = battle input + `Calls`); `source:<battle>` (input + `SuggestCards: true, StopAfterTurns: 30`) | writer: `new CoreScriptWriter(setup.Enemy) {ChainIndex 1, ChainTotal 1, Disclosure 2}`; `{Opening: writer.Opening(start state), Steps: [{call..., Events: writer.Write(step events, step state)}]}` (steps after Start). source: `new CoreBattleSource(setup, seed, suggestCards: true, stopAfterTurns: 30)`, loop until Finished: waiting → `{Guide, CanEndTurn, Suggested, Hand: [{Id, Inspect, PreviewFor, RefusalText(id, Inspect), WrongZoneText: RefusalText(id, WrongZone)}]}` then TryPlay(suggested, RequiredZone(face.Aim)) or EndTurn(); else AdvanceAuto(); `{Opening, Moves: [{..., Action, Verdict?, Event}], Finished, Result, Turn, HistoryCount}` | CardDef, EnemyDef, CardFace, CardDetail, UnitFrame, OmenFrame, PileFrame, FloorFrame, CornerFrame, PlayedFrame, StatusChip, TermMark, CardValue, TermDefinition, PileCard, DepictionFrame |
| `dungeon` | 257 | `map:L<layer>:s<seed>` (7 × 20) `{Layer, Seed}`; `mapspec:<Minimal/Full>:s<1..5>`; `walk:L<layer>:s<1..3>:<leftmost/rightmost>` `{Layer, Seed, Policy, Ops}`; `encounter:L<layer>:<NodeKind>` `{Layer, Kind, Seeds: 20}`; `table:<Name>` for LayerEncounters, Miasma, SevenLayers, BattleField, ExplorationReducer, ItemCatalogue, Loadout, RunLoadout, Seeds | map: `{Map: SevenLayers.Of(l).Map(seed), Validate: "ok" or error, Rows, Neighbours, Predecessors}`; walk: one result per op `{State: ExplorationState or error, CanStep: [CanStep(state, id) per node]}`; encounter: `{Picks: [LayerEncounters.Pick(kind, layer, new SeededRng(seed)) for seed 1..20], Pool, Cells, CellsFor1to4, Token, IsDetour, IsCombat}` | LayerMap, LayerProfile, LayerMapSpec |
| `exploration` | 3 | `expedition:s<1..3>` `{Seed, Loadout ("empty"/"trial"), BattleToolIds, Talent, LastLayer: 3, StubHpLoss: 10, MaxMoves: 400}` | `{Moves: [{Op, Args?, Result, Screen: session.Screen(), Expedition: summary}]}` | LayerMap, LayerProfile, CardDef, EnemyDef, CardInstance, MasteryProfile |
| `backdrop` | 33 | `table:BackdropSpec`; `layer:<0..8>`; `miasma:<0..100 step 5>`; `motion`; `switch` | BackdropSpec / ExplorationBackdropSpec / MiasmaLook / BackdropMotion / BackdropSwitch values (see `oracle/src/Suites/Backdrop.cs` for the exact argument lists: heights `0, .1, .2, .3, .4, .5, .52, .54, .56, .6, .7, .8, .9, 1`, times `0, 70, 140, 500, 1234.5, 3000, 7000, 12345` ms) | — |

### Dungeon walk ops

`input.Ops` is the exact list of calls; the harness applies them in order to one `ExplorationState` (an op that throws leaves the state as it was and its result is the error):

| Op | Call |
| --- | --- |
| `Enter {Layer, MapSeed, Loadout ("empty" → `RunLoadout.Empty`, "trial" → `RunStart.TrialLoadout()`), Hp, MaxHp, Stamina, MiasmaPercent, TempMaxStaminaMod}` | `ExplorationReducer.Enter(SevenLayers.Of(Layer), SevenLayers.Of(Layer).Map(MapSeed), loadout, Hp, MaxHp, Stamina, MiasmaPercent, TempMaxStaminaMod)` |
| `Step {Node, Choice}` | `ExplorationReducer.Step(state, Node, Choice)` |
| `ReportBattle {Hp, Stamina}` | `ExplorationReducer.ReportBattle(state, Hp, Stamina)` |
| `UseConsumable {Index}` | `ExplorationReducer.UseConsumable(state, Index)` |
| `SpendTrainingMark` / `Interlude` / `EndLife` | the reducer call of that name |
| `Descend {Layer, MapSeed}` | `ExplorationReducer.Descend(state, SevenLayers.Of(Layer), SevenLayers.Of(Layer).Map(MapSeed))` |

The policy that produced them (for reading only): leftmost / rightmost reachable unresolved node (column, then id); a rest node trains the first time and rests after; a combat node reports HP − 10 / stamina − 3; seed 2 carries the trial loadout and uses consumable 0 (then a bad index 7) on the third step; seed 3 starts at miasma 55 + 5 × layer; then a revisit of the entry, the interlude, the descent, a spare training mark and two `EndLife`s.

### Exploration moves

`Expedition(new RunStart { Seed, Loadout }, deck: null, battleToolIds, talent)`; then `Toggle(Journal)` × 2 and `Escape()` × 2; then until the life is over or the layer passes `LastLayer` (or 400 moves): pending battle → `StartEncounter()` (Result: `HandIn`), per fight `NextLaunch()` then `StartNext()` and `Finish(stub, source.History)` with `stub = source.State with { Result = Won, Phase = Finished, Player = Player with { Hp = max(1, Hp − 10) } }` (Result: `{Launch, StartState, Opening, HpAfter, StaminaAfter, Index, Count, Won}`), then `Return()` and `Learn(Offer[0])` when something is offered; pending step → `Act(ConfirmStep)`; confirming descent → `Act(ConfirmDescend)`; LayerCleared / PushedOut → `Act(GoToInterlude)`; Interlude → `Act(Descend)` (stop at layer 3's interlude); training marks → `Train(Deck[0].Def.Id)`; the trial loadout's first consumable once miasma > 0 → `UseConsumable(0)`; no unresolved neighbour → `Act(AskEndLife)`, `Act(ConfirmEndLife)`; else `Step(leftmost node, Train for the first rest else Rest)`. Each move records `session.Screen()` and the expedition summary `{Life, RunSeed, State, CarvingTaken, Surveyed, PendingBattleNode, PendingEncounter, PendingStepNode, ConfirmingEndLife, ConfirmingDescend, InterludeHealed, OpenOverlay, MapIsLive, CanEndLife, Deck (instance ids), Owned, Offer, Consumables, Pages, Mastery, Disclosure {enemyId: DisclosureOf}}`.

### Scope and size

`battles` is 34 MB at seeds 1..5, under the 40 MB cap, so no seeds were dropped. To stay under it, the battle decks are the six archetypes plus `Decks.Initial()`; the other `Decks` decks (`cost_one`, `initial_forty`, `plain`, the no-stance twins) are not fought (they are in `catalog` `table:Decks`). `--seeds N` changes the battle seeds; `--only a,b` runs some suites (shapes.json is merged, never shrunk).

## Harness (`test/parity/run.mjs`)

`node test/parity/run.mjs [suite ...] [--first N] [--case <substring>] [--show N] [--oracle <dir>] [--quiet]`. Exit 1 when a case differs or a runner throws where the C# did not; skipped cases alone do not fail.

- One runner per suite in `test/parity/suites/<suite>.mjs`, a line-by-line mirror of `oracle/src/Suites/<Suite>.cs`, written against the C# names at the TYPE_MAP paths. `lib/battle.mjs` holds the battle loop and the call replay, `lib/serialize.mjs` the JSON form (`toOracleJson`), `lib/compare.mjs` `$ref` expansion and the diff, `lib/modules.mjs` the module loader.
- A case whose module is missing, fails to load, or lacks an export is **skipped** and counted under the missing module; a suite with every case skipped prints `skipped: missing <module>`.
- `battles` plays the port's own greedy player (`src/sim/GreedyPlayer.js`). `previews` and `scripts` replay the oracle's recorded calls (`input.Calls`), so a greedy difference does not hide a preview or script difference.
- Port conventions the runners accept: `out` parameters as `{ ok, value }` (ok = C# return, value = out) or `{ value, <outName> }` (e.g. `Loadout.Use` → `{ value, used }`, `RunLoadout.Spend` → `{ value, gaugeRelief }`), arrays `[ret, out]`; `MoteAt`'s two outs as `{ x, y }` / `{ X, Y }` / `[x, y]`. `ulong` arguments (map / run / RNG seeds) are passed as BigInt; `int` seeds as numbers. Error messages: a JS message that copies .NET's full text (` (Parameter 'x')`, `\nActual value was v.`) is cut back like the oracle's, so either spelling matches. C# `ToString()` overrides are read as `ToString()` when the port has it, else `toString()`.
- Static tables (`table:*` cases) read the port's static members by the names the oracle listed.
- `test/parity/report.json` holds every failure `{ id, path, oracle, js }` (values truncated) and the module load status.

## Known C# behaviour the port must reproduce

- `scripts`: the `polearm_warped+crossbow_hunter` pair throws `KeyNotFoundException` ("The given key 'bolt' was not present in the dictionary.") in both the writer and the source: `CoreScriptWriter.FloorOf` → `FloorCells.DenseOf(AsShown(after), 0)` → `TurnLoop.OmenBoundaryCells` → `EnemyAi.ActionOf` looks the second enemy's omen up in the first enemy's action table. A faithful port throws the same (`KeyNotFoundException` from `src/shared/cs.js` with that message where C# indexes a `Dictionary`).
- Expected errors are part of the oracle output and must match: e.g. `catalog` `table:Columns` `CostOf0to5` (`Columns.CostOf(0)` / `(5)` throw `ArgumentOutOfRangeException("Column must be 1..4.")`), the dungeon walks' refused steps (`ExplorationRuleException`), `table:Loadout` / `table:RunLoadout` rule errors, `BattleField.CellsFor` with too many enemy cells.

## Per-type members (generated from `oracle/out/shapes.json`)

<!-- BEGIN GENERATED SHAPES -->

| Type | Kind | Members (in order) | Computed (getters the JS side must expose) | Float members |
| --- | --- | --- | --- | --- |
| `ActionExecuted` (BattleCore) | record | Actor, Unit, Action, GapBefore |  |  |
| `ActionWhiffed` (BattleCore) | record | Actor, Unit, SourceId, Gap, Reach |  |  |
| `BattleEnded` (BattleCore) | record | Actor, Unit, Result |  |  |
| `BattleHandIn` (Exploration.Bridge) | class | Layer, Kind, Encounter, FieldCells, Hp, MaxHp, Stamina, MaxStamina, MiasmaPercent, Disclosure, Deck, Tools, Seed |  |  |
| `BattleHistory` (BattleCore) | record | PlayedCards, PlayerTurns, HookSnagTurns, InflictionTurns, MasteryPlayList |  |  |
| `BattleLaunch` (Depiction.Bridge) | class | EnemyId, Seed, FieldCells, StartGap, Disclosure, AutoPlay, StopAfterTurns, Deck, MoreEnemyIds, PlayerMaxHp, MaxStamina, StartHp, StartStamina, PlayerStartCell, StartStaminaBonus, FirstHitGuard, BossWordWard, ChainIndex, ChainTotal |  |  |
| `BattleReturn` (Exploration.Bridge) | class | Won, Died, Hp, Stamina, Ticks, RankUps, Disclosure, Observed, Learned, Offered, Reward | Died |  |
| `BattleReward` (Exploration.Bridge) | class | CardChoices, Consumables, Pages, IsEmpty | IsEmpty |  |
| `BattleState` (BattleCore) | record | Turn, FieldCells, Player, Enemies, Hand, DrawPile, DiscardPile, Result, Phase, ExilePile, PlayerHistory, PlayerTools, Gap, Nearest | Gap, Nearest |  |
| `CardButton` (Exploration) | class | Label, Action |  |  |
| `CardDef` (BattleCore) | record | Id, Name, Attributes, Column, Face, Trait, Targets, Description, ExtraTrait, FixedCost, Cost, Attribute | Cost, Attribute |  |
| `CardDetail` (Depiction) | class | Name, KindLine, Body, Terms |  |  |
| `CardExiled` (BattleCore) | record | Actor, Unit, Card |  |  |
| `CardFace` (Depiction) | class | Id, Name, Cost, Kind, Aim, Affects, TypeLabel, Description, ValueText, TraitText, TraitLit, RequiredRange, RequiredRangeGlyph, ReachHint, Values, TextLines, Lasting, Plain, TraitTop, TraitBottom, PrintedCost, LitOn, HpLossOn, HoverLine, ReachCells, Detail, StanceHintAfterPay, Terms, CostDrop | CostDrop |  |
| `CardInstance` (BattleCore) | record | InstanceId, Def |  |  |
| `CardMastery` (BattleCore) | record | CardId, Ticks, Rank |  |  |
| `CardPlayed` (BattleCore) | record | Actor, Unit, Card, GapBefore |  |  |
| `CardValue` (Depiction) | class | Kind, Number, Word |  |  |
| `CarriedTools` (BattleCore) | record | ScaleGuard, BossWordWard, Warded |  |  |
| `CellsMoved` (BattleCore) | record | Actor, Unit, From, To, Pushed |  |  |
| `CombatantState` (BattleCore) | record | Hp, MaxHp, Stamina, MaxStamina, Guard, Cell, Size, Statuses, NextTurnRecoveryBonus, Stances, FollowUp, Played, Moved, FreeStep, LastMove, FarCell | FarCell |  |
| `CornerFrame` (Depiction) | class | Turn, Floor, ChainIndex, ChainTotal, MiasmaPercent |  |  |
| `Cue` (Depiction) | class | Kind, Target, Source, Amount, Text, Intensity, HpAfter, GuardAfter, StaminaAfter, StaminaMax, RangeAfter, RangeGlyphAfter, StacksAfter, RebuildAfter, System, Fx, TraitAccent, Pushed, PlayerCellAfter, EnemyCellAfter, GapAfter, CellsMoved |  |  |
| `DamageDealt` (BattleCore) | record | Actor, Unit, Target, Raw, Absorbed, Damage, TargetGuardAfter, TargetHpAfter |  |  |
| `DeckReshuffled` (BattleCore) | record | Actor, Unit, DrawPileCount |  |  |
| `DeckValidation` (BattleCore) | record | Ok, Errors |  |  |
| `DefeatChecked` (BattleCore) | record | Actor, Unit, Result |  |  |
| `Departure` (BattleCore) | record | Deck, Tools, Consumables, StartCellsBack, TalentCardId, StartStaminaBonus, FirstHitGuard, BossWordWard, PlayerStartCell, StartGap | PlayerStartCell, StartGap |  |
| `DepictionEvent` (Depiction) | class | Order, Kind, Title, CardId, Aim, PreviewText, PlayedTo, Systems, Cues, After, WaitsForDrag | WaitsForDrag |  |
| `DepictionFrame` (Depiction) | class | Outcome, Corner, Player, Enemy, Omen, Hand, StanceHint, StanceDivider, Floor, Piles, StanceListsShown, Played, EnemyTurn |  |  |
| `Drawn` (BattleCore) | record | Actor, Unit, Card, HandCountAfter |  |  |
| `EdgeLine` (Exploration) | class | From, To, Lit |  |  |
| `Encounter` (DungeonCore) | record | Fights, IsStandIn |  |  |
| `EnemyActionDef` (BattleCore) | record | Id, Name, Attributes, Column, Face, Trait, Targets, Description, Omen, NotWhileFoeHas, PullBeyond, NotAtFoeCap, Cost, Attribute | Cost, Attribute |  |
| `EnemyDef` (BattleCore) | record | Id, Name, MaxHp, MaxStamina, Recovery, Size, BranchAtGapZero, BranchAtGapOneToTwo, BranchAtGapThreePlus, Actions, Rank, ActionsPerPhase, Switches |  |  |
| `EnemyDefeated` (BattleCore) | record | Actor, Unit |  |  |
| `EnemyUnit` (BattleCore) | record | Def, Body, Omen, SpentStances, Plan, ActiveSwitch, Executed, HeldSwitches, Alive | Alive |  |
| `ExplorationBackdrop` (Backdrop) | class | Layer, Place, Veil |  |  |
| `ExplorationScreen` (Exploration) | class | LayerTitle, TimeLabel, TimeLeft, TimeLimit, Miasma, HpLabel, StaminaLabel, MaxStaminaLabel, MarksLabel, Nodes, Edges, CurrentNodeId, Hint, ToolsLabel, Consumables, CanEndLife, ShowsEndLife, Card, Journal, OpenOverlay, CanOpenCards, CanOpenSettings, Cards, Settings, MapIsLive | MapIsLive |  |
| `ExplorationState` (DungeonCore) | class | Profile, Map, CurrentNodeId, Resolved, TimeLeft, MiasmaPercent, Hp, MaxHp, Stamina, TempMaxStaminaMod, TrainingMarks, Loadout, Phase, EffectiveDensity, MaxStamina, IsOver | EffectiveDensity, MaxStamina, IsOver |  |
| `Face` (BattleCore) | record | Power, Guard, Move, Push, Draw, StaminaGain, Reach, Statuses, Heal, Break, Stance, Hits, StopStance, GivesFoeStatus | GivesFoeStatus |  |
| `FaceResolved` (BattleCore) | record | Actor, Unit, SourceId, Face |  |  |
| `Fight` (DungeonCore) | record | EnemyIds |  |  |
| `Flicker` (Backdrop) | struct | PeriodMs, Depth, Flash, Moves | Moves | Depth |
| `FloorFrame` (Depiction) | class | Cells, PlayerCell, PlayerSize, EnemyCell, EnemySize, Gap, AimCells, DenseCells, MoveCells |  |  |
| `GaugeTick` (Backdrop) | struct | Percent, IsStepLine, Height |  | Height |
| `GuardCleared` (BattleCore) | record | Actor, Unit, Cleared |  |  |
| `GuardGained` (BattleCore) | record | Actor, Unit, Amount, GuardAfter |  |  |
| `HandDiscarded` (BattleCore) | record | Actor, Unit, Count |  |  |
| `Haze` (Backdrop) | class | Shape, Color, X, Y, Size, Count, Angle, Flicker |  | X, Y, Size, Angle |
| `ItemDef` (DungeonContent) | class | Id, Name, Reading, Effect, Amount, Note, Side | Side |  |
| `JournalBook` (Journal) | class | Pages, Context, ClosingHint, First | ClosingHint, First |  |
| `JournalPage` (Journal) | class | Id, Tab, Title, Rows, Disclosure, DisclosureLabel, Body | DisclosureLabel, Body |  |
| `JournalRow` (Journal) | class | Label, Text, NeedsDisclosure |  |  |
| `LayerBackdrop` (Backdrop) | class | Layer, Name, Sky, Hazes, Motes, Tile |  |  |
| `LayerEncounterTable` (DungeonCore) | class | Layer, Normal, Elite, Boss |  |  |
| `LayerMap` (DungeonCore) | class (custom: LayerMap) | Layer, Seed, Nodes, RowCount, EntryId, BossId, NodeCount, ShortestPathCost, Successors, Fingerprint | NodeCount, ShortestPathCost, Successors, Fingerprint |  |
| `LayerMapSpec` (DungeonCore) | class | Layer, RowWidths, Quotas, ExtraEdgeChance, NodeCount, MiddleNodeCount, RowCount, ShortestPathCost, FullSweepCost | RowCount, ShortestPathCost, FullSweepCost |  |
| `LayerProfile` (DungeonCore) | class | Layer, Name, TrueName, Density, TimeLimit, MapSpec, ShortestPathCost, IsLast | ShortestPathCost, IsLast |  |
| `Loadout` (DungeonContent) | class | Tools, Consumables, ToolSlotCount, MiasmaDensityRelief, TimeLimitBonus | Tools, Consumables, MiasmaDensityRelief, TimeLimitBonus |  |
| `MapNode` (DungeonCore) | record | Id, Row, Column, Kind |  |  |
| `MasteryBook` (BattleCore) | record | Talent, Records, Profiles, TopRankHolder | TopRankHolder |  |
| `MasteryPlay` (BattleCore) | record | CardId, Contexts, Ticks | Ticks |  |
| `MasteryProfile` (BattleCore) | record | CardId, Kind, RankNames, RankTraits, GapContext |  |  |
| `MasteryTally` (BattleCore) | record | Ticks |  |  |
| `MaxStaminaChanged` (BattleCore) | record | Actor, Unit, Amount, MaxAfter, StaminaAfter |  |  |
| `MiasmaGauge` (Exploration) | class | Percent, NextPercent, Fill, NextFill, Label, ForecastLabel, StepLines | Fill, NextFill | Fill, NextFill |
| `MiasmaShade` (Backdrop) | struct | Strength, VeilAlpha, EdgeAlpha, InnerRadius, SeepAlpha |  | Strength, VeilAlpha, EdgeAlpha, InnerRadius, SeepAlpha |
| `Motes` (Backdrop) | class | Count, Color, SizePx, SpeedX, SpeedY, Twinkle |  | SizePx, SpeedX, SpeedY, Twinkle |
| `MoveBlocked` (BattleCore) | record | Actor, Unit, By |  |  |
| `NodeCard` (Exploration) | class | Id, Row, Column, Kind, X, Y, Glyph, KindLabel, Standing, Lines, Options, Note, Title, Detail |  | X, Y |
| `Omen` (BattleCore) | record | ActionId, Label |  |  |
| `OmenFrame` (Depiction) | class | Visible, Icon, KindLabel, SideGlyph, ValueText, Hit, Statuses, Disclosure, PlanVisible, PlanIcon, PlanKindLabel, PlanValueText, PlanStatuses, PlanSideGlyph, PlanChanged, PlanTag | PlanTag |  |
| `OmenLabel` (BattleCore) | record | Kind, Reach |  |  |
| `OmenPreview` (BattleCore) | record | RawPower, Damage, Lands, Rests |  |  |
| `OmenSet` (BattleCore) | record | Actor, Unit, Omen, Decided |  |  |
| `OmenStatus` (Depiction) | class | Label, Glyph |  |  |
| `OverlayPage` (Exploration) | class | Title, Lines, CloseLabel |  |  |
| `PileCard` (Depiction) | class | Name, InPile, InDeck, RowText | RowText |  |
| `PileFrame` (Depiction) | class | Shown, Draw, Discard, Exile, DrawCards, DiscardCards, ExileCards |  |  |
| `PlanChanged` (BattleCore) | record | Actor, Unit, Was, Now |  |  |
| `PlanSet` (BattleCore) | record | Actor, Unit, Plan, Decided |  |  |
| `PlayPreview` (BattleCore) | record | TraitHolds, Attacks, RawPower, Damage, GuardGain, InReach, Cost |  |  |
| `PlayedFrame` (Depiction) | class | Shown, Attack, Guard, Skill, Stance, Last, FollowUp |  |  |
| `PlayerTurnEnd` (BattleCore) | record | Guard, Gap, Moved, Cell, Played, LastMove |  |  |
| `Reach` (BattleCore) | record | Min, Max |  |  |
| `Reflected` (BattleCore) | record | Actor, Unit, Target, Raw, Absorbed, Damage, TargetGuardAfter, TargetHpAfter |  |  |
| `ReliefItem` (DungeonCore) | record | Id, Kind, Amount |  |  |
| `ReserveChecked` (BattleCore) | record | Actor, Unit, StaminaLeft, GuardGained, GuardAfter |  |  |
| `Rested` (BattleCore) | record | Actor, Unit, Declared |  |  |
| `Rgba` (Backdrop) | struct | R, G, B, A |  | R, G, B, A |
| `RunLoadout` (DungeonCore) | class | Tools, ToolSlotCount, Consumables, DensityRelief | Consumables, DensityRelief |  |
| `ScreenCard` (Exploration) | class | Kind, Title, Lines, Buttons, IsShown, IsConfirmation | IsShown, IsConfirmation |  |
| `SimDeck` (BattleCore.Sim) | record | Id, Name, Rows, Basis, Count, HasStance | Count, HasStance |  |
| `SkyStop` (Backdrop) | struct | At, Color |  | At |
| `SlotCard` (Exploration) | class | Index, Name, Note, CanUse |  |  |
| `StaminaBroken` (BattleCore) | record | Actor, Unit, Target, Amount, StaminaAfter |  |  |
| `StaminaGained` (BattleCore) | record | Actor, Unit, Amount, StaminaAfter |  |  |
| `StaminaRecovered` (BattleCore) | record | Actor, Unit, Amount, StaminaAfter, MaxStamina |  |  |
| `StaminaSpent` (BattleCore) | record | Actor, Unit, Amount, StaminaAfter |  |  |
| `StanceDef` (BattleCore) | record | Hook, When, Threshold, Guard, Recovery, NextRecovery, Power, Stamina, Status, StatusStacks, Break, OncePerTurn, Attribute |  |  |
| `StanceEntry` (BattleCore) | record | Def, Source, ReactedTurn, StoppedThrough |  |  |
| `StanceFired` (BattleCore) | record | Actor, Unit, SourceId, Hook |  |  |
| `StanceResumed` (BattleCore) | record | Actor, Unit, SourceId, Index |  |  |
| `StanceRow` (Depiction) | class | Name, Count |  |  |
| `StanceSet` (BattleCore) | record | Actor, Unit, SourceId, Name, Stance |  |  |
| `StanceStopped` (BattleCore) | record | Actor, Unit, SourceId, Index, Through |  |  |
| `StatusApplied` (BattleCore) | record | Actor, Unit, Target, Kind, Stacks, StacksAfter, Refused, Dropped, Warded, Landed | Landed |  |
| `StatusChip` (Depiction) | class | Label, Stacks, Kind, Title, Group, Effect, Decay |  |  |
| `StatusCleared` (BattleCore) | record | Actor, Unit, Kind, Stacks |  |  |
| `StatusConsumed` (BattleCore) | record | Actor, Unit, Kind, StacksAfter |  |  |
| `StatusGrant` (BattleCore) | record | Kind, Stacks, OnSelf, Cap |  |  |
| `StatusHpChanged` (BattleCore) | record | Actor, Unit, Kind, Amount, HpAfter |  |  |
| `StatusSet` (BattleCore) | class (custom: StatusSet) | Stacks | Stacks |  |
| `StatusTicked` (BattleCore) | record | Actor, Unit, Kind, StacksAfter |  |  |
| `StepForecast` (Exploration) | class | TimeBefore, TimeAfter, MiasmaBefore, MiasmaAfter, MaxStaminaBefore, MaxStaminaAfter, StaminaBefore, StaminaAfter, HpBefore, HpAfter, PhaseAfter, Lines, Warnings, IsLethal | IsLethal |  |
| `StepOption` (Exploration) | class | Label, Choice, Forecast, NeedsConfirm | NeedsConfirm |  |
| `StepResult` (BattleCore) | record | State, Events |  |  |
| `TermDefinition` (Depiction) | class | Word, Meaning |  |  |
| `TermMark` (Depiction) | class | Row, Start, Length, Word, Meaning |  |  |
| `Trait` (BattleCore) | record | Condition, Effect, Amount, Threshold, Attribute, Omen, Watch, Grant |  |  |
| `TraitEvaluated` (BattleCore) | record | Actor, Unit, SourceId, Trait, Outcome |  |  |
| `TraitOutcome` (BattleCore) | record | Triggered, PowerBonus, GuardBonus, NextTurnRecoveryBonus, StaminaGain, Draw, Grants, CostDown, Convert, FollowUp, BreakBonus |  |  |
| `TreeSwitch` (BattleCore) | record | Id, BranchAtGapZero, BranchAtGapOneToTwo, BranchAtGapThreePlus, Overrides, EnterMove, Sway, Cleanse |  |  |
| `TreeSwitched` (BattleCore) | record | Actor, Unit, From, To |  |  |
| `TurnEnded` (BattleCore) | record | Actor, Unit, Turn |  |  |
| `TurnStarted` (BattleCore) | record | Actor, Unit, Turn |  |  |
| `UnitFrame` (Depiction) | class | Hp, HpMax, Guard, ShowStamina, Stamina, StaminaMax, NextRecovery, ReserveLikely, Elite, HasRange, Range, RangeGlyph, Statuses, ArtId, Down, Stances |  |  |
| `WallHit` (BattleCore) | record | Actor, Unit, BlockedCells, Raw, Absorbed, Damage, GuardAfter, HpAfter |  |  |

<!-- END GENERATED SHAPES -->
