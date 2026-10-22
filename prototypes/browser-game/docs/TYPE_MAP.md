# C# → JS file and type map

Every public/internal type, the C# file it lives in and the JS module that must export it under the same name. Total 23161 lines of C#.

| C# file | JS module | lines | exported types |
| --- | --- | --- | --- |
| `unity-port/BattleCore/Attributes.cs` | `src/core/battle/Attributes.js` | 62 | AttributeRule |
| `unity-port/BattleCore/BattleEvents.cs` | `src/core/battle/BattleEvents.js` | 255 | BattleEvent, TurnStarted, GuardCleared, StaminaRecovered, StatusTicked, Drawn, DeckReshuffled, OmenSet, PlanSet, PlanChanged, TreeSwitched, ReserveChecked, HandDiscarded, TurnEnded, CardPlayed, ActionExecuted, ActionWhiffed, Rested, StaminaSpent, TraitEvaluated, FaceResolved, DamageDealt, GuardGained, CellsMoved, WallHit, PushRefused, FreeStepTaken, MoveBlocked, StatusApplied, StaminaGained, StatusConsumed, StatusHpChanged, Healed, StaminaBroken, Reflected, StanceSet, StanceFired, StanceStopped, StanceResumed, CardExiled, EnemyDefeated, DefeatChecked, BattleEnded, MaxStaminaChanged, StatusCleared, StepResult |
| `unity-port/BattleCore/CardCatalog.Initial.cs` | `src/core/battle/CardCatalog.Initial.js` | 322 | CardCatalog |
| `unity-port/BattleCore/CardCatalog.Learned.cs` | `src/core/battle/CardCatalog.Learned.js` | 342 | CardCatalog |
| `unity-port/BattleCore/CardCatalog.cs` | `src/core/battle/CardCatalog.js` | 142 | CardCatalog, PrototypeDeck |
| `unity-port/BattleCore/Cards.cs` | `src/core/battle/Cards.js` | 275 | DrawResult, DeckValidation, Cards |
| `unity-port/BattleCore/Chain.cs` | `src/core/battle/Chain.js` | 141 | BattleTally, Chain |
| `unity-port/BattleCore/ChainOrder.cs` | `src/core/battle/ChainOrder.js` | 140 | ChainBattle, ChainOrder |
| `unity-port/BattleCore/Columns.cs` | `src/core/battle/Columns.js` | 96 | Columns |
| `unity-port/BattleCore/Combat.cs` | `src/core/battle/Combat.js` | 96 | Combat |
| `unity-port/BattleCore/Constants.cs` | `src/core/battle/Constants.js` | 212 | Constants |
| `unity-port/BattleCore/DeckRules.cs` | `src/core/battle/DeckRules.js` | 162 | Departure, DeckRules |
| `unity-port/BattleCore/Enemies.cs` | `src/core/battle/Enemies.js` | 1099 | Enemies |
| `unity-port/BattleCore/EnemyAi.cs` | `src/core/battle/EnemyAi.js` | 366 | EnemyAi |
| `unity-port/BattleCore/Field.cs` | `src/core/battle/Field.js` | 200 | Shift, Field |
| `unity-port/BattleCore/IRng.cs` | `src/core/battle/IRng.js` | 74 | IRng, SystemRng, SeededRng, FixedRng |
| `unity-port/BattleCore/Mastery.cs` | `src/core/battle/Mastery.js` | 623 | MasteryContext, MasteryKind, MasteryPlay, MasteryProfile, MasteryProfiles, Mastery, MasteryTally, CardMastery, RankUp, MasteryResult, MasteryBook |
| `unity-port/BattleCore/Statuses.cs` | `src/core/battle/Statuses.js` | 244 | StatusSet, Statuses |
| `unity-port/BattleCore/Traits.cs` | `src/core/battle/Traits.js` | 162 | TraitContext, TraitOutcome, Traits |
| `unity-port/BattleCore/TurnLoop.cs` | `src/core/battle/TurnLoop.js` | 2146 | BattleSetup, PlayPreview, OmenPreview, PlayRefusal, TurnLoop, FocusStep |
| `unity-port/BattleCore/Types.cs` | `src/core/battle/Types.js` | 1029 | BattleAttribute, FaceKind, Actor, GameResult, BattlePhase, TargetKind, OmenKind, StatusKind, StatusGrant, StatusDecay, GapBand, TraitCondition, TraitEffect, Trait, StanceHook, StanceWhen, StanceEntry, StanceDef, Reach, GapBoundary, Face, CardDef, CardInstance, OmenLabel, Omen, EnemyActionDef, PlayerTurnEnd, BattleHistory, AdaptationView, TreeSwitch, EnemyRank, EnemyDef, CombatantState, EnemyUnit, CarriedTools, BattleState, EnumTokens |
| `unity-port/DungeonCore/BattleField.cs` | `src/core/dungeon/BattleField.js` | 59 | BattleField |
| `unity-port/DungeonCore/ExplorationReducer.cs` | `src/core/dungeon/ExplorationReducer.js` | 267 | ExplorationRuleException, ExplorationReducer |
| `unity-port/DungeonCore/ExplorationState.cs` | `src/core/dungeon/ExplorationState.js` | 164 | RunPhase, RestChoice, ExplorationState |
| `unity-port/DungeonCore/IRng.cs` | `src/core/dungeon/IRng.js` | 96 | IRng, SeededRng, RngExtensions |
| `unity-port/DungeonCore/LayerEncounters.cs` | `src/core/dungeon/LayerEncounters.js` | 274 | Fight, Encounter, LayerEncounterTable, LayerEncounters |
| `unity-port/DungeonCore/LayerMap.cs` | `src/core/dungeon/LayerMap.js` | 113 | MapNode, LayerMap |
| `unity-port/DungeonCore/LayerMapSpec.cs` | `src/core/dungeon/LayerMapSpec.js` | 140 | LayerMapSpec |
| `unity-port/DungeonCore/MapGenerator.cs` | `src/core/dungeon/MapGenerator.js` | 168 | MapGenerator |
| `unity-port/DungeonCore/MapValidator.cs` | `src/core/dungeon/MapValidator.js` | 153 | MapValidationException, MapValidator |
| `unity-port/DungeonCore/Miasma.cs` | `src/core/dungeon/Miasma.js` | 75 | Miasma |
| `unity-port/DungeonCore/NodeKind.cs` | `src/core/dungeon/NodeKind.js` | 75 | NodeKind, NodeKindTokens |
| `unity-port/DungeonCore/RunLoadout.cs` | `src/core/dungeon/RunLoadout.js` | 91 | ReliefKind, ReliefItem, RunLoadout |
| `unity-port/DungeonCore/SevenLayers.cs` | `src/core/dungeon/SevenLayers.js` | 162 | LayerProfile, SevenLayers |
| `unity-port/DungeonContent/ItemCatalogue.cs` | `src/core/dungeonContent/ItemCatalogue.js` | 133 | ItemDef, ItemCatalogue |
| `unity-port/DungeonContent/ItemEffect.cs` | `src/core/dungeonContent/ItemEffect.js` | 104 | ItemEffect, EffectSide, ItemEffects |
| `unity-port/DungeonContent/Loadout.cs` | `src/core/dungeonContent/Loadout.js` | 126 | LoadoutRuleException, Loadout |
| `unity-port/unity-project-kit/Assets/View/Exploration/Bridge/BattleRewards.cs` | `src/bridge/exploration/BattleRewards.js` | 276 | BattleReward, CardPile, BattleRewards |
| `unity-port/unity-project-kit/Assets/View/Exploration/Bridge/EncounterBattle.cs` | `src/bridge/exploration/EncounterBattle.js` | 286 | BattleHandIn, BattleReturn, FoughtBattle, EncounterBattle |
| `unity-port/unity-project-kit/Assets/View/Exploration/Bridge/Expedition.cs` | `src/bridge/exploration/Expedition.js` | 328 | Expedition |
| `unity-port/unity-project-kit/Assets/View/Exploration/Bridge/UniqueCards.cs` | `src/bridge/exploration/UniqueCards.js` | 98 | UniqueCardReward, UniqueCards |
| `unity-port/unity-project-kit/Assets/View/Exploration/Script/EnemyIntel.cs` | `src/bridge/explorationScript/EnemyIntel.js` | 51 | NodeIntel, IEnemyIntel, NoEnemyIntel |
| `unity-port/unity-project-kit/Assets/View/Exploration/Script/ExplorationScreen.cs` | `src/bridge/explorationScript/ExplorationScreen.js` | 480 | NodeStanding, ScreenAction, CardKind, Overlay, StepForecast, StepOption, NodeCard, EdgeLine, MiasmaGauge, SlotCard, CardButton, ScreenCard, OverlayPage, ExplorationScreen |
| `unity-port/unity-project-kit/Assets/View/Exploration/Script/ExplorationScreenBuilder.cs` | `src/bridge/explorationScript/ExplorationScreenBuilder.js` | 522 | ExplorationScreenBuilder |
| `unity-port/unity-project-kit/Assets/View/Exploration/Script/ExplorationSession.cs` | `src/bridge/explorationScript/ExplorationSession.js` | 370 | RunStart, ExplorationSession |
| `unity-port/unity-project-kit/Assets/View/Exploration/Script/ExplorationText.cs` | `src/bridge/explorationScript/ExplorationText.js` | 82 | ExplorationText |
| `unity-port/unity-project-kit/Assets/View/Journal/Script/JournalBook.cs` | `src/bridge/journal/JournalBook.js` | 161 | JournalTab, JournalContext, JournalRow, JournalPage, JournalBook |
| `unity-port/unity-project-kit/Assets/View/Journal/Script/JournalTerms.cs` | `src/bridge/journal/JournalTerms.js` | 73 | JournalTerms |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/BattleLaunch.cs` | `src/bridge/depiction/BattleLaunch.js` | 167 | BattleLaunch |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CardFaceText.cs` | `src/bridge/depiction/CardFaceText.js` | 601 | CardFaceText |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CardSystems.cs` | `src/bridge/depiction/CardSystems.js` | 61 | CardSystems |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CoreBattleSource.cs` | `src/bridge/depiction/CoreBattleSource.js` | 221 | CoreBattleSource |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CoreScriptWriter.cs` | `src/bridge/depiction/CoreScriptWriter.js` | 1032 | CoreScriptWriter |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CoreText.cs` | `src/bridge/depiction/CoreText.js` | 540 | CoreText |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/DeckBuilder.cs` | `src/bridge/depiction/DeckBuilder.js` | 469 | DeckFilter, DeckPage, DeckBuilder |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/DemoEnding.cs` | `src/bridge/depiction/DemoEnding.js` | 84 | DemoEnding |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/DemoSession.cs` | `src/bridge/depiction/DemoSession.js` | 403 | DemoMode, DemoStage, DemoEndScreen, DemoSession |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/DepartureBuilder.cs` | `src/bridge/depiction/DepartureBuilder.js` | 286 | SupplyOption, DepartureBuilder |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/FloorCells.cs` | `src/bridge/depiction/FloorCells.js` | 95 | FloorCells |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/GrammarSamples.cs` | `src/bridge/depiction/GrammarSamples.js` | 240 | GrammarSample, GrammarSamples |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/PileCards.cs` | `src/bridge/depiction/PileCards.js` | 89 | PileCards |
| `unity-port/unity-project-kit/Assets/View/Depiction/Bridge/StatusChipText.cs` | `src/bridge/depiction/StatusChipText.js` | 193 | StatusChipText |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/BattleSpeed.cs` | `src/bridge/depictionScript/BattleSpeed.js` | 146 | BattleSpeedStep, BattleSpeed |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/BottomRightLayout.cs` | `src/bridge/depictionScript/BottomRightLayout.js` | 85 | PlayedLayout, ReserveHintLayout |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/CardTextFit.cs` | `src/bridge/depictionScript/CardTextFit.js` | 198 | TextFit, CardTextFit |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/CornerLayout.cs` | `src/bridge/depictionScript/CornerLayout.js` | 94 | ScreenBox, CornerLayout |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/DemoDeck.cs` | `src/bridge/depictionScript/DemoDeck.js` | 120 | TraitCondition, DemoCard, DemoDeck |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/DepictionTypes.cs` | `src/bridge/depictionScript/DepictionTypes.js` | 1017 | RangeSide, UnitSide, CardKind, OmenIcon, StatusGlyph, OmenStatus, OmenHit, CardAim, DropZone, CardFace, CardValueKind, CardValue, TermDefinition, TermRow, TermMark, CardDetail, ChipKind, StatusChip, UnitFrame, StanceRow, OmenFrame, CornerFrame, BattleOutcome, DepictionFrame, PileFrame, PileCard, PlayedFrame, FloorFrame, StrikeSystem, StrikeSystems, DepictionEventKind, CueKind, Cue, DepictionEvent, DepictionScript, PlayVerdict, DepictionText, IDepictionSource, DepictionRunner |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/EffectAudit.cs` | `src/bridge/depictionScript/EffectAudit.js` | 274 | Carries, Verdict, Basis, EffectAuditEntry, EffectFlow, EffectAudit |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/EffectGrammar.cs` | `src/bridge/depictionScript/EffectGrammar.js` | 239 | FxSystem, FxForm, FxMark, FxFaces, EffectGrammar |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/Effects.cs` | `src/bridge/depictionScript/Effects.js` | 776 | EffectId, EffectSpec, EffectCatalog, EffectSwitches, EffectStep, StatusBeat, EffectStrength, EffectPlan, EffectTrace, Entry |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/EnemyTurnShadeLayout.cs` | `src/bridge/depictionScript/EnemyTurnShadeLayout.js` | 20 | EnemyTurnShadeLayout |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/FigureMotion.cs` | `src/bridge/depictionScript/FigureMotion.js` | 245 | FigurePose, CharacterArt, FigureFacing, FigureFrame, EnemyMotionShape, DefeatShape |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/FloorSpec.cs` | `src/bridge/depictionScript/FloorSpec.js` | 59 | FloorSpec |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/HandFan.cs` | `src/bridge/depictionScript/HandFan.js` | 111 | FanPlace, HandFan, HandSpec |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/HpWickSpec.cs` | `src/bridge/depictionScript/HpWickSpec.js` | 79 | HpWickSpec |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/OmenDisclosure.cs` | `src/bridge/depictionScript/OmenDisclosure.js` | 70 | OmenDisclosure |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/PileLayout.cs` | `src/bridge/depictionScript/PileLayout.js` | 79 | PileKind, PileLayout |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/PileList.cs` | `src/bridge/depictionScript/PileList.js` | 46 | PileList |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/ReceiverDrop.cs` | `src/bridge/depictionScript/ReceiverDrop.js` | 37 | ReceiverDrop |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/RefusalLineLayout.cs` | `src/bridge/depictionScript/RefusalLineLayout.js` | 40 | RefusalLineLayout |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/StanceGlow.cs` | `src/bridge/depictionScript/StanceGlow.js` | 53 | StanceGlowChange, StanceGlow |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/StanceList.cs` | `src/bridge/depictionScript/StanceList.js` | 89 | StanceList |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/StatusPanelLayout.cs` | `src/bridge/depictionScript/StatusPanelLayout.js` | 135 | StatusPanelLayout |
| `unity-port/unity-project-kit/Assets/View/Depiction/Script/TurnSliceScript.cs` | `src/bridge/depictionScript/TurnSliceScript.js` | 163 | TurnSliceScript |
| `unity-port/unity-project-kit/Assets/View/Backdrop/Script/BackdropMotion.cs` | `src/bridge/backdrop/BackdropMotion.js` | 88 | BackdropMotion |
| `unity-port/unity-project-kit/Assets/View/Backdrop/Script/BackdropSpec.cs` | `src/bridge/backdrop/BackdropSpec.js` | 378 | Rgba, SkyStop, HazeShape, Flicker, Haze, Motes, LayerBackdrop, BackdropSpec |
| `unity-port/unity-project-kit/Assets/View/Backdrop/Script/BackdropSwitch.cs` | `src/bridge/backdrop/BackdropSwitch.js` | 53 | BackdropSwitch, Change |
| `unity-port/unity-project-kit/Assets/View/Backdrop/Script/ExplorationBackdrop.cs` | `src/bridge/backdrop/ExplorationBackdrop.js` | 81 | ExplorationBackdrop, ExplorationBackdropSpec |
| `unity-port/unity-project-kit/Assets/View/Backdrop/Script/MiasmaLook.cs` | `src/bridge/backdrop/MiasmaLook.js` | 276 | MiasmaWarning, GaugeTick, MiasmaShade, MiasmaLook |
| `unity-port/BattleCore.Sim/Decks.cs` | `src/sim/Decks.js` | 205 | SimDeck, Decks |
| `unity-port/BattleCore.Sim/GreedyPlayer.cs` | `src/sim/GreedyPlayer.js` | 309 | Positioning, Choice, GreedyPlayer |

## JS-only additions (not in the C#)

The multi-enemy extension of the depiction (`docs/port-notes/depiction-multi-enemy.md`) adds to
`src/bridge/depictionScript/DepictionTypes.js` two exports the C# file does not have — `FloorEnemy`
(one enemy on the floor: Unit, Hatch, Cell, Size, Gap, Down, AimCells, DenseCells, MoveCells) and
`EnemyHatch` (Count, AngleOf) — and members on C# types: `DepictionFrame.Enemies` / `Omens` /
`SwatchesShown`, `UnitFrame.Unit` / `Cell` / `Size` / `Gap` / `GapGlyph` / `Hatch`, `OmenFrame.Unit` /
`Hatch`, `FloorFrame.Enemies`, `Cue.Unit` / `UnitCellAfter` / `UnitGapAfter`, `DepictionEvent.Unit`,
`CardFace.ReachOn` / `TargetUnit` / `CostOn` / `StanceHintAfterPayOn` / `ReachFor()` / `CostFor()` /
`CostDropFor()` / `StanceHintAfterPayFor()`. `CoreBattleSource` gains `TargetFor(cardId)` and an
optional `target` on `Inspect` / `TryPlay` / `PreviewFor` / `RefusalText`; `CoreScriptWriter` gains the
static `TargetOf(state, id)`. The parity serializer reads the C# members only, so none of these reach
the oracle comparison.

## Names defined in more than one module

- `CardCatalog` is one `partial` class over three files. `CardCatalog.js` exports `CardCatalog`; `CardCatalog.Initial.js` and `CardCatalog.Learned.js` export what the main file merges (keep the C# member names, e.g. the static members declared in each part).
- `IRng` / `SeededRng` exist in both cores (separate on purpose). `core/battle/IRng.js` and `core/dungeon/IRng.js` each export their own; same SplitMix64 sequence.
- `TraitCondition` (BattleCore `Types.cs` vs Depiction `DemoDeck.cs`) and `CardKind` (Exploration `ExplorationScreen.cs` vs Depiction `DepictionTypes.cs`) are different types with the same name in different C# namespaces. Import the one from the namespace the C# file uses (`using` lines).
