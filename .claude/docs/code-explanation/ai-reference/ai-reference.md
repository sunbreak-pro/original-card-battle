# AI Reference Document

## Section 1: File Registry

| Path | Lines | System | Role |
|------|-------|--------|------|
| `src/App.tsx` | 119 | state | Provider nesting, screen routing |
| `src/contexts/GameStateContext.tsx` | 140 | state | Screen navigation, battle config, depth |
| `src/contexts/ResourceContext.tsx` | 363 | economy | Gold dual-pool, magic stones, exploration limit |
| `src/contexts/PlayerContext.tsx` | ~675 | state | Player stats, runtime battle state, deck, equipment AP |
| `src/contexts/InventoryContext.tsx` | 774 | inventory | Item add/remove/move/equip operations |
| `src/contexts/GuildContext.tsx` | 240 | state | Rumors, quests (standalone, not in main hierarchy) |
| `src/ui/dungeonHtml/DungeonRunContext.tsx` | 198 | dungeon | Dungeon run state, floor progression |
| `src/domain/battles/managements/useBattleOrchestrator.ts` | ~870 | battle | Main orchestrator — composes all hooks |
| `src/domain/battles/managements/useBattleState.ts` | 620 | battle | Player/enemy/target state management |
| `src/domain/battles/managements/useBattlePhase.ts` | 212 | battle | Phase queue, speed, turn order |
| `src/domain/battles/managements/executeCharacterManage.ts` | 200 | battle | Player/enemy phase execution bridge |
| `src/domain/battles/managements/battleFlowManage.ts` | — | battle | Battle flow utilities |
| `src/domain/battles/managements/useCardExecution.ts` | ~748 | card | Core card execution hook |
| `src/domain/battles/managements/useClassAbility.ts` | 280 | character | React hooks: useSwordEnergy, useClassAbility, factory |
| `src/domain/battles/managements/useElementalChain.ts` | ~86 | character | React hook wrapper for ElementalSystem |
| `src/domain/battles/managements/useEnemyAI.ts` | 300 | battle | React hook wrapping AI + execution + preview |
| `src/domain/battles/managements/damageManage.ts` | 107 | battle | Damage calculation wrappers for card/enemy |
| `src/domain/battles/execution/playerPhaseExecution.ts` | 150 | battle | Pure functions: player phase start/end |
| `src/domain/battles/execution/enemyPhaseExecution.ts` | 248 | battle | Pure functions: enemy phase start/end + attack |
| `src/domain/battles/calculators/damageCalculation.ts` | 87 | battle | Core damage formula + allocation |
| `src/domain/battles/calculators/buffCalculation.ts` | 167 | battle | Buff effect multipliers, healing, DoT, utility |
| `src/domain/battles/calculators/speedCalculation.ts` | 80 | battle | Speed formulas + mean-reversion randomness |
| `src/domain/battles/calculators/phaseCalculation.ts` | 167 | battle | Phase queue generation from speed diff |
| `src/domain/battles/logic/buffLogic.ts` | 153 | battle | Buff CRUD operations + duration management |
| `src/domain/battles/logic/bleedDamage.ts` | 17 | battle | Bleed damage formula |
| `src/domain/battles/logic/phaseLogic.ts` | 156 | battle | Phase transition helpers (partially deprecated) |
| `src/domain/battles/logic/cardExecutionLogic.ts` | 30 | card | Factory for default CardExecutionResult |
| `src/domain/battles/logic/itemEffectExecutor.ts` | 293 | inventory | Consumable item effect execution in battle |
| `src/domain/battles/logic/enemyStateLogic.ts` | 41 | battle | createEnemyStateFromDefinition factory |
| `src/domain/cards/decks/deck.ts` | 107 | card | **IMMUTABLE** — createInitialDeck, getCardDataByClass |
| `src/domain/cards/decks/deckReducer.ts` | ~67 | card | **IMMUTABLE** — deckReducer (draw/discard/shuffle) |
| `src/domain/cards/state/card.ts` | 83 | card | Effective power, mastery calc, canPlay, calculateCardEffect |
| `src/domain/cards/state/cardPlayLogic.ts` | 58 | card | Duplicate of calculateCardEffect + canPlayCard |
| `src/domain/cards/state/CardHandle.ts` | 101 | card | Sword energy processing helpers for card play |
| `src/domain/cards/state/masteryManager.ts` | 60 | card | MasteryStore (Map), increment/sync mastery |
| `src/domain/cards/logic/cardDerivation.ts` | 134 | card | Card evolution unlock checks based on mastery |
| `src/domain/cards/logic/cardUtils.ts` | 32 | card | Class-based card filtering utilities |
| `src/domain/characters/player/classAbility/classAbilitySystem.ts` | ~137 | character | Interface definition, DamageModifier type, helpers |
| `src/domain/characters/player/logic/swordEnergySystem.ts` | ~261 | character | Swordsman: energy gauge, bleed chance |
| `src/domain/characters/player/logic/elementalSystem.ts` | ~250 | character | Mage: resonance chain, element effects, field buffs |
| `src/domain/characters/player/logic/title.ts` | ~23 | character | Title/grade strings by card type count |
| `src/domain/characters/logic/playerUtils.ts` | 96 | character | Player stat helpers (getInitialPlayerState) |
| `src/domain/characters/logic/characterUtils.ts` | 34 | character | createEmptyBuffDebuffMap, shared utils |
| `src/domain/characters/logic/classAbilityUtils.ts` | 89 | character | Factory functions, initial state creators |
| `src/domain/characters/logic/enemyUtils.ts` | 28 | character | generateEnemyInstanceId helper |
| `src/domain/characters/enemy/logic/enemyAI.ts` | 126 | battle | Core AI: determineEnemyAction, selectRandomEnemy |
| `src/domain/characters/enemy/logic/enemyActionExecution.ts` | 115 | battle | Action execution loop with energy, fallback |
| `src/domain/camps/logic/shopLogic.ts` | 142 | economy | Purchase, pack opening, stone exchange |
| `src/domain/camps/logic/blacksmithLogic.ts` | 466 | economy | Level/quality upgrade, repair, dismantle |
| `src/domain/camps/logic/sanctuaryLogic.ts` | 335 | economy | Node unlock, effect aggregation, stat boosting |
| `src/domain/camps/logic/blacksmithUtils.ts` | 65 | economy | Equipment validation helpers |
| `src/domain/item_equipment/logic/generateItem.ts` | 69 | inventory | Item factory: consumable and equipment generation |
| `src/domain/item_equipment/logic/itemUtils.ts` | 19 | inventory | calculateMagicStoneValue helper |
| `src/domain/item_equipment/logic/equipmentStats.ts` | 205 | inventory | AP calculation, stat bonuses from equipped items |
| `src/domain/dungeon/depth/deptManager.ts` | 41 | dungeon | Depth names, neutral theme constants |
| `src/domain/dungeon/logic/dungeonLogic.ts` | 362 | dungeon | Map generation, node selection, progression |
| `src/domain/dungeon/logic/nodeEventLogic.ts` | 243 | dungeon | Rest/treasure/event node processing |
| `src/domain/save/logic/saveManager.ts` | 249 | state | localStorage save/load/export/import/migrate |
| `src/types/characterTypes.ts` | 344 | types | PlayerData, BattleStats, class ability types |
| `src/types/battleTypes.ts` | 247 | types | BuffDebuffState, DamageResult, PhaseQueue |
| `src/types/cardTypes.ts` | 139 | types | Card, CardCategory, MasteryLevel, GemLevel |
| `src/types/itemTypes.ts` | 176 | types | Item, MagicStones, EquipmentData |
| `src/types/campTypes.ts` | 510 | types | Shop, blacksmith, sanctuary, storage types |
| `src/types/dungeonTypes.ts` | 88 | types | DungeonRun, DungeonFloor, DungeonNode |
| `src/types/saveTypes.ts` | 74 | types | SaveData, PlayerSaveData, ResourceSaveData |
| `src/types/index.ts` | 100 | types | Barrel re-export of all types |
| `src/constants/battleConstants.ts` | 97 | constants | All numeric battle constants |
| `src/constants/cardConstants.ts` | 139 | constants | MASTERY_THRESHOLDS, MASTERY_BONUSES, MAGIC_ELEMENTS |
| `src/constants/characterConstants.ts` | 126 | constants | SWORD_ENERGY_MAX, RESONANCE_MULTIPLIER, etc. |
| `src/constants/itemConstants.ts` | 45 | constants | Magic stone values, rarity sell/buy prices |
| `src/constants/campConstants.ts` | 126 | constants | Blacksmith modifiers, sanctuary constants |
| `src/constants/saveConstants.ts` | 12 | constants | SAVE_VERSION, SAVE_KEY |
| `src/constants/uiConstants.ts` | — | constants | Asset paths, player images |
| `src/constants/data/battles/buffData.ts` | 111 | data | BUFF_EFFECTS definitions |
| `src/constants/data/cards/swordsmanCards.ts` | ~594 | data | 41 Swordsman cards (sw_001-sw_041) |
| `src/constants/data/cards/mageCards.ts` | ~706 | data | 40 Mage cards (mg_001-mg_040) |
| `src/constants/data/characters/PlayerData.tsx` | 82 | data | Base stats per class |
| `src/constants/data/characters/CharacterClassData.ts` | 226 | data | Class display info, descriptions |
| `src/constants/data/characters/enemy/enemyDepth1.ts` | ~200 | data | Depth 1 enemies |
| `src/constants/data/characters/enemy/enemyDepth2.ts` | ~200 | data | Depth 2 enemies |
| `src/constants/data/characters/enemy/enemyDepth3.ts` | ~200 | data | Depth 3 enemies |
| `src/constants/data/characters/enemy/enemyDepth4.ts` | ~200 | data | Depth 4 enemies |
| `src/constants/data/characters/enemy/enemyDepth5.ts` | ~200 | data | Depth 5 enemies |
| `src/constants/data/camps/ShopData.ts` | 244 | data | Shop listings, equipment packs, daily rotation |
| `src/constants/data/camps/BlacksmithData.ts` | 191 | data | Upgrade costs, quality rates |
| `src/constants/data/camps/SanctuaryData.ts` | 608 | data | 25 skill nodes, tier/category layout |
| `src/constants/data/items/ConsumableItemData.ts` | 332 | data | All consumable item definitions |
| `src/constants/data/items/EquipmentData.ts` | 103 | data | Equipment base stats, slot definitions |
| `src/constants/data/items/TestItemsData.ts` | 273 | data | Test items for development |

## Section 2: Dependency Matrix

### 2A: Import Graph (file → its imports)

```
App.tsx → [GameStateContext, ResourceContext, PlayerContext, InventoryContext, DungeonRunContext]
GameStateContext → [saveManager, saveConstants]
ResourceContext → [itemUtils, campTypes]
PlayerContext → [ResourceContext, PlayerData, CharacterClassData, classAbilityUtils, equipmentStats]
InventoryContext → [PlayerContext]
GuildContext → [campTypes]
DungeonRunContext → [dungeonLogic, dungeonTypes]
useBattleOrchestrator → [useBattleState, useBattlePhase, executeCharacterManage, useCardExecution, useClassAbility, useElementalChain, useEnemyAI, deck.ts, cardConstants]
useBattleState → [PlayerData, characterUtils, enemyStateLogic, battleConstants]
useBattlePhase → [speedCalculation, phaseCalculation]
useCardExecution → [card.ts, damageCalculation, buffLogic, bleedDamage, swordEnergySystem, cardExecutionLogic, deck.ts, classAbilitySystem]
useClassAbility → [swordEnergySystem, useElementalChain, classAbilitySystem]
useElementalChain → [elementalSystem]
useEnemyAI → [enemyAI, enemyActionExecution, enemyPhaseExecution, bleedDamage]
damageCalculation → [buffCalculation, battleConstants]
buffCalculation → [buffData, battleConstants]
buffLogic → [buffData]
playerPhaseExecution → [buffCalculation, buffLogic, deck.ts]
enemyPhaseExecution → [buffCalculation, buffLogic, damageCalculation, bleedDamage, enemyAI]
enemyAI → [enemyDepth1-5, characterTypes]
enemyActionExecution → [enemyAI]
enemyStateLogic → [enemyUtils, characterUtils, battleConstants]
swordEnergySystem → [classAbilitySystem, classAbilityUtils, battleConstants]
elementalSystem → [classAbilitySystem, classAbilityUtils, characterConstants, cardConstants]
classAbilityUtils → [characterTypes]
card.ts → [cardTypes]
cardPlayLogic → [cardTypes]
masteryManager → [cardConstants]
cardDerivation → [cardTypes]
itemEffectExecutor → [ConsumableItemData, buffLogic, buffData]
shopLogic → [generateItem, itemConstants, ShopData, campTypes]
blacksmithLogic → [campConstants, blacksmithUtils, BlacksmithData, campTypes]
sanctuaryLogic → [campConstants, SanctuaryData, campTypes]
generateItem → [ConsumableItemData, EquipmentData]
equipmentStats → [campConstants, itemTypes]
dungeonLogic → [dungeonTypes]
nodeEventLogic → [itemTypes, generateItem]
saveManager → [saveTypes, saveConstants]
```

### 2B: Consumer Graph (file → who imports it)

```
GameStateContext → [App.tsx]
ResourceContext → [App.tsx, PlayerContext]
PlayerContext → [App.tsx, InventoryContext]
InventoryContext → [App.tsx]
DungeonRunContext → [App.tsx, BattleScreen]
useBattleOrchestrator → [BattleScreen]
useBattleState → [useBattleOrchestrator]
useBattlePhase → [useBattleOrchestrator]
useCardExecution → [useBattleOrchestrator]
useClassAbility → [useBattleOrchestrator]
useElementalChain → [useBattleOrchestrator, useClassAbility]
useEnemyAI → [useBattleOrchestrator]
damageCalculation → [useCardExecution, enemyPhaseExecution]
buffCalculation → [damageCalculation, playerPhaseExecution, enemyPhaseExecution]
buffLogic → [useCardExecution, playerPhaseExecution, enemyPhaseExecution, itemEffectExecutor]
buffData → [buffCalculation, buffLogic, itemEffectExecutor]
bleedDamage → [useCardExecution, enemyPhaseExecution, useEnemyAI]
deck.ts → [useBattleOrchestrator, useCardExecution, playerPhaseExecution] (IMMUTABLE)
deckReducer → [useBattleOrchestrator] (IMMUTABLE)
card.ts → [useCardExecution]
classAbilitySystem → [swordEnergySystem, elementalSystem, useClassAbility, useCardExecution]
classAbilityUtils → [swordEnergySystem, elementalSystem, PlayerContext]
swordEnergySystem → [useClassAbility]
elementalSystem → [useElementalChain]
enemyAI → [enemyActionExecution, enemyPhaseExecution, useEnemyAI]
enemyActionExecution → [useEnemyAI]
enemyPhaseExecution → [useEnemyAI, executeCharacterManage]
enemyStateLogic → [useBattleState]
characterUtils → [enemyStateLogic, useBattleState]
battleConstants → [damageCalculation, buffCalculation, swordEnergySystem, enemyStateLogic, useBattleState]
characterConstants → [elementalSystem]
cardConstants → [elementalSystem, masteryManager, useBattleOrchestrator]
campConstants → [blacksmithLogic, sanctuaryLogic, equipmentStats]
saveManager → [GameStateContext]
saveConstants → [GameStateContext, saveManager]
ConsumableItemData → [generateItem, itemEffectExecutor, shopLogic]
EquipmentData → [generateItem]
ShopData → [shopLogic]
BlacksmithData → [blacksmithLogic]
SanctuaryData → [sanctuaryLogic]
swordsmanCards → [deck.ts]
enemyDepth1-5 → [enemyAI]
generateItem → [nodeEventLogic, shopLogic]
equipmentStats → [PlayerContext]
shopLogic → [ShopScreen UI]
blacksmithLogic → [BlacksmithScreen UI]
sanctuaryLogic → [SanctuaryScreen UI]
dungeonLogic → [DungeonRunContext]
nodeEventLogic → [DungeonMap UI]
```

## Section 3: Context Provider Chain

```
GameStateProvider                     ← No context deps; reads saveManager at init
│ PROVIDES: currentScreen, battleMode, depth, navigateTo, startBattle, returnToCamp
│
└─ SettingsProvider                   ← Settings (volume, etc.)
   └─ ToastProvider                   ← Toast notifications
      └─ ResourceProvider             ← No context deps; standalone
         │ PROVIDES: gold (baseCamp/exploration), magicStones, explorationLimit
         │           addGold, useGold, transferExplorationToBaseCamp, resetExploration
         │
         └─ PlayerProvider            ← READS ResourceContext (useResources)
            │ PROVIDES: playerData, runtimeBattleState, deckCards, equipmentAP
            │           updatePlayerData, lives, souls
            │
            └─ InventoryProvider      ← READS PlayerContext (usePlayer)
               │ PROVIDES: add/remove/move items, equip/unequip
               │ DELEGATES TO PlayerContext: all ops via updatePlayerData()
               │
               └─ DungeonRunProvider  ← No context deps; standalone
                  │ PROVIDES: dungeonRun, initializeRun, selectNode, completeNode, advanceFloor
                  │
                  └─ AppContent

GuildContext (STANDALONE — not in hierarchy, local to Guild facility component)
  PROVIDES: rumors, quests, activateRumor, acceptQuest, claimReward
```

## Section 4: Vulnerability Index

| ID | Severity | Location | System | Type | Summary |
|----|----------|----------|--------|------|---------|
| V-ORCH-01 | HIGH | `useBattleOrchestrator.ts:550-608` | battle | stale-closure | executeNextPhaseRef update gap between recreate and useEffect |
| V-ORCH-02 | MEDIUM | `useBattleOrchestrator.ts:562` | battle | stale-closure | Phase queue expansion uses stale enemies array |
| V-ORCH-03 | MEDIUM | `useBattleOrchestrator.ts:648` | battle | stale-closure | handleEndPhase reads stale phaseState.currentPhaseIndex |
| V-ORCH-04 | MEDIUM | `useBattlePhase.ts:82-111` | battle | race-condition | generatePhaseQueueFromSpeeds double-call uses same random state |
| V-ORCH-05 | LOW | `useBattleOrchestrator.ts` | battle | quality | 882-line file violates single-responsibility |
| V-ORCH-06 | LOW | `useBattleOrchestrator.ts:405` | battle | quality | useEnemyAI return value discarded |
| V-ORCH-07 | LOW | `useBattleOrchestrator.ts:800-882` | battle | quality | Legacy compatibility surface (deprecated aliases) |
| V-ORCH-08 | LOW | `useBattlePhase.ts` | battle | quality | phaseCount vs phaseIndex naming confusion |
| V-ORCH-09 | LOW | `expandPhaseEntriesForMultipleEnemies` | battle | quality | Multi-enemy support bolted on, limits extensibility |
| V-DMG-01 | MEDIUM | `playerPhaseExecution.ts:71-74` | battle | logic-error | Healing uses pre-duration-decrease buffs (inconsistent with DoT) |
| V-DMG-02 | LOW | `buffCalculation.ts:93-98` | battle | logic-error | Curse + overCurse multiplicative stacking reduces to 10% |
| V-DMG-03 | MEDIUM | `damageCalculation.ts:52-57` | battle | logic-error | Guard bleed-through only when guard fully absorbs + AP=0 |
| V-DMG-04 | HIGH | `buffCalculation.ts:135-137` | battle | logic-error | canAct only checks stun; freeze/stagger not enforced |
| V-DMG-05 | MEDIUM | `buffCalculation.ts:162-167` | battle | logic-error | immunityBuff return semantics are confusing/inverted |
| V-DMG-06 | MEDIUM | `bleedDamage.ts` | battle | logic-error | Bleed damage does not multiply by stacks |
| V-DMG-07 | LOW | `buffCalculation.ts:9-62` | battle | quality | Attack/defense buffs are hardcoded if-chains |
| V-DMG-08 | LOW | `damageCalculation.ts` | battle | quality | No damage type system (physical/magical/elemental) |
| V-DMG-09 | LOW | `buffLogic.ts:94-110` | battle | quality | Deprecated decreaseBuffDebuffDuration still exists |
| V-DMG-10 | MEDIUM | `buffCalculation.ts:120-124` | battle | logic-error | Burn/poison DoT ignores stacks |
| V-DMG-11 | LOW | `buffCalculation.ts` | battle | stub | Weakness/prostoration defined but unused in calculations |
| V-CARD-01 | MEDIUM | `useCardExecution.ts:320` | card | stale-closure | Sword energy bonus uses pre-consumption value |
| V-CARD-02 | HIGH | `useCardExecution.ts:429-461` | card | logic-error | Double guard application (effect.shieldGain + card.guardAmount) |
| V-CARD-03 | MEDIUM | `useCardExecution.ts:446-461` | card | logic-error | Hardcoded card IDs for sword energy guard (sw_037/039/040) |
| V-CARD-04 | MEDIUM | `useCardExecution.ts:468-483` | card | stale-closure | Heal uses stale HP value (multiple setPlayerHp clobber) |
| V-CARD-05 | MEDIUM | `card.ts:52-82, cardPlayLogic.ts:14-43` | card | quality | Duplicate calculateCardEffect functions in two files |
| V-CARD-06 | LOW | `itemEffectExecutor.ts:172-178` | card | logic-error | Item damage ignores defense/allocation |
| V-CARD-07 | MEDIUM | `useCardExecution.ts:410-421` | card | stale-closure | Auto-bleed uses stale enemy buffs (last setEnemyBuffs wins) |
| V-CARD-08 | LOW | `card.ts:58-68` | card | quality | calculateCardEffect only handles 3 of 6 categories |
| V-CARD-09 | FIXED | — | card | — | Deck management now class-aware (useDeckManage.ts removed) |
| V-CARD-10 | LOW | `cardPlayLogic.ts` | card | quality | Complete dead-code duplicate file |
| V-CARD-11 | LOW | `CardHandle.ts:49` | card | quality | processSwordEnergyConsumption returns unused damageBonus |
| V-CARD-12 | LOW | `deck.ts:3-7` | card | quality | Module-level mutable counter never resets |
| V-CARD-13 | LOW | `deckReducter.ts` | card | logic-error | Empty draw+discard pile edge case (IMMUTABLE) |
| V-CARD-14 | LOW | `swordsmanCards.ts:10-11` | card | logic-error | Card id equals cardTypeId — duplicates share same id |
| V-CARD-15 | FIXED | — | card | — | Filename renamed to swordsmanCards.ts |
| V-CARD-16 | LOW | card data files | card | quality | No runtime validation on card definitions |
| V-CARD-18 | LOW | `masteryManager.ts` | card | logic-error | getMasteryBonus returns decimal; callers must add 1.0 |
| V-CLASS-01 | MEDIUM | `useCardExecution.ts:320, swordEnergySystem.ts:111` | character | stale-closure | Sword energy flat bonus applied before consumption |
| V-CLASS-02 | MEDIUM | `elementalSystem.ts:47-57` | character | logic-error | Non-magic cards reset resonance chain |
| V-CLASS-03 | HIGH | `elementalSystem.ts:170-218, useCardExecution.ts:326-329` | character | stub | Resonance effects (burn/freeze/stun) never applied |
| V-CLASS-04 | MEDIUM | `classAbilitySystem.ts:22-30` | character | stub | DamageModifier.critBonus and penetration never used |
| V-CLASS-05 | LOW | `classAbilitySystem.ts:119-129` | character | logic-error | combineDamageModifiers multiplies percent (not additive) |
| V-CLASS-07 | LOW | `characterConstants.ts:93-124` | character | quality | 8 empty resonance effect entries |
| V-CLASS-08 | LOW | `useClassAbility/useElementalChain` | character | quality | Two identical hook wrappers (~120 lines duplication) |
| V-CLASS-10 | LOW | `classAbilityUtils.ts:15, characterConstants.ts:30` | character | quality | SWORD_ENERGY_MAX defined in two places |
| V-CLASS-11 | LOW | `classAbilitySystem.ts:35, characterConstants.ts:37` | character | quality | DEFAULT_DAMAGE_MODIFIER defined in two places |
| V-CLASS-12 | LOW | `swordEnergySystem.ts:110` | character | logic-error | _card param unused — bonus applies to all cards |
| V-CLASS-13 | MEDIUM | `elementalSystem.ts:48-77` | character | logic-error | card.element array inconsistent find vs includes check |
| V-CS-01 | HIGH | `PlayerContext.tsx:531-667` | state | race-condition | Dual resource state sync — stale getTotalGold drift |
| V-CS-02 | MEDIUM | `ResourceContext.tsx:117-145` | state | race-condition | useGold balance check outside setResources updater |
| V-CS-03 | MEDIUM | `ResourceContext.tsx:224-241` | state | race-condition | useExplorationPoint returns stale success value |
| V-CS-04 | HIGH | `PlayerContext.tsx:772` | state | logic-error | Player ID regenerates on every useMemo recomputation |
| V-CS-05 | LOW | `PlayerContext.tsx` | state | quality | ~675-line file handles 6+ concerns |
| V-CS-06 | HIGH | `saveManager.ts:193-232` | state | data-loss | Save misses inventory items and equipmentInventory |
| V-CS-07 | MEDIUM | `saveManager.ts:172-186` | state | stub | migrate() is a stub — format changes corrupt saves |
| V-CS-08 | LOW | `ResourceContext.tsx:67-81, PlayerContext.tsx:228-244` | state | quality | Hardcoded test values in production init |
| V-CS-09 | LOW | `campTypes.ts:237` | state | logic-error | Quest expiry uses Date objects (won't survive JSON) |
| V-CS-10 | LOW | `src/ui/dungeonHtml/DungeonRunContext.tsx` | state | quality | Context in UI directory breaks convention |
| V-CS-11 | HIGH | `InventoryContext.tsx:48` | inventory | stale-closure | Rapid operations read stale playerData snapshot |
| V-CS-12 | LOW | `useBattleState.ts:460-494` | battle | quality | Battle state dual API surface (targeted + legacy setters) |
| V-EC-01 | MEDIUM | `shopLogic.ts:83-124` | economy | logic-error | Stone exchange overpayment (no change given) |
| V-EC-02 | LOW | `ShopData.ts:33-55` | economy | logic-error | Equipment packs skip uncommon rarity |
| V-EC-03 | LOW | `blacksmithLogic.ts:224-285` | economy | logic-error | Quality upgrade consumes resources on failure (by design?) |
| V-EC-04 | LOW | `sanctuaryLogic.ts:125-201` | economy | quality | Sanctuary effects recalculated on every call |
| V-EC-05 | LOW | `BlacksmithData.ts:106-135` | economy | logic-error | Dismantle returns % of sellPrice (very low for common) |
| V-EC-06 | MEDIUM | `saveTypes.ts:29-32` | economy | data-loss | Save excludes exploration resources (lost on reload) |
| V-EC-07 | LOW | `BlacksmithData.ts:12-22` | economy | logic-error | Common and Uncommon share identical upgrade costs |
| V-EC-08 | MEDIUM | `ShopData.ts:185-223` | economy | logic-error | Daily rotation seed not persisted |
| V-EC-09 | LOW | systemic | economy | quality | No endgame gold sink |
| V-EC-10 | LOW | `sanctuaryLogic.ts:94-120` | economy | logic-error | explorationLimitBonus field not updated by unlockNode |
| V-EC-11 | LOW | `ResourceContext.tsx:67-81` | economy | quality | Hardcoded initial resource test values |
| V-EC-12 | MEDIUM | `ResourceContext.tsx:267-309` | economy | logic-error | Transfer multiplier floors per-tier (small quantities lost) |
| V-PLR-01 | HIGH | `PlayerContext.tsx:772` | character | logic-error | Player ID = Date.now() changes on every recomputation |
| V-PLR-02 | LOW | `swordEnergySystem.ts:110` | character | logic-error | _card unused — sword energy bonus applies to all cards |
| V-PLR-04 | MEDIUM | `elementalSystem.ts:48-77` | character | logic-error | Elemental resonance uses card.element inconsistently |
| V-PLR-05 | LOW | `PlayerContext.tsx` | character | quality | ~675-line PlayerContext handles 6+ concerns |
| V-PLR-06 | LOW | `title.ts:1-23` | character | stub | Title functions disconnected from gameplay |
| V-PLR-07 | FIXED | — | character | — | Filename renamed to title.ts |
| V-ENM-01 | MEDIUM | `enemyAI.ts:23` | battle | logic-error | _remainingEnergy ignored in AI selection |
| V-ENM-02 | MEDIUM | `enemyActionExecution.ts:81-114` | battle | logic-error | Preview uses different random results than execution |
| V-ENM-03 | LOW | `enemyAI.ts:71` | battle | logic-error | enemyAction always sets element to ["physics"] |
| V-ENM-04 | LOW | `useEnemyAI.ts:233-241` | battle | stale-closure | Bleed calculated with stale enemy HP/buffs |
| V-ENM-05 | LOW | `useEnemyAI.ts:133` | battle | quality | useEnemyAI is stateless wrapper |
| V-ENM-06 | LOW | `enemyPhaseExecution.ts:141-150, useEnemyAI.ts:177-179` | battle | quality | Duplicate guard-only check |
| V-ENM-07 | MEDIUM | `enemyActionExecution.ts:65-77` | battle | logic-error | Fallback action hardcoded 5 damage (doesn't scale with depth) |
| V-INV-01 | HIGH | `InventoryContext.tsx:48` | inventory | stale-closure | Stale playerData in rapid operations |
| V-INV-02 | MEDIUM | `InventoryContext.tsx:211-216` | inventory | logic-error | equipItem only removes from storage (not equipmentInventory) |
| V-INV-03 | LOW | `InventoryContext.tsx:441-519` | inventory | quality | Duplicate move directions (equipment_to_storage = equipSlotItem_to_storage) |
| V-INV-04 | LOW | `InventoryContext.tsx:56-73` | inventory | logic-error | Capacity tracked manually instead of items.length |
| V-INV-05 | LOW | `TestItemsData.ts` | inventory | quality | Test items loaded in production build |
| V-INV-06 | LOW | systemic | inventory | quality | No item stacking for consumables |
| V-INV-07 | MEDIUM | `equipmentStats.ts` | inventory | stub | Equipment durability never decremented in battle |
| V-DNG-01 | LOW | `dungeonLogic.ts:47-71` | dungeon | logic-error | Map generation uses uncontrolled Math.random |
| V-DNG-02 | LOW | `DungeonRunContext.tsx:122-133` | dungeon | logic-error | Floor advancement doesn't escalate difficulty |
| V-DNG-03 | MEDIUM | `nodeEventLogic.ts:194` | dungeon | logic-error | Trap damage uses negative hpRestore (consumer must check) |
| V-DNG-04 | LOW | `src/ui/dungeonHtml/DungeonRunContext.tsx` | dungeon | quality | Context in UI directory |
| V-DNG-05 | LOW | `dungeonLogic.ts:90-126` | dungeon | logic-error | Node connections can create unreachable nodes |
| V-DNG-06 | LOW | `deptManager.ts` | dungeon | quality | Filename typo: "deptManager" (should be "depthManager") |
| V-DNG-07 | LOW | `DungeonRunContext.tsx:94-118` | dungeon | logic-error | encounterCount inflated by non-battle nodes |

## Section 5: Stub/Incomplete Systems

| System | Status | Type Location | Implementation Location | Missing |
|--------|--------|---------------|------------------------|---------|
| Equipment durability | PARTIAL | `itemTypes.ts` (durability, maxDurability) | `equipmentStats.ts` (stat calc exists) | No degradation during battle; repair exists but nothing to repair |
| Title system | DISCONNECTED | `title.ts` (functions exist) | None | cardTypeCount not tracked; functions possibly never called |
| Save migration | STUB | `saveTypes.ts` (version field) | `saveManager.ts:172-186` | migrate() stamps version only; no actual migration logic |
| Critical hit | STUB | `classAbilitySystem.ts` (critBonus) | None | critBonus set by mage at Lv2 but never read in damage pipeline |
| Penetration | STUB | `classAbilitySystem.ts` (penetration) | None | Field exists in DamageModifier, never set > 0 |
| Freeze/stagger | PARTIAL | `buffData.ts` (defined as debuffs) | None | Exist in buff catalog but canAct() only checks stun |
| Weakness/prostoration | PARTIAL | `buffData.ts` (defined) | None | Defined in BUFF_EFFECTS but not checked in damage calc |
| Daily shop rotation | PARTIAL | `ShopData.ts` (generateDailyEquipmentInventory) | None visible | dayCount tracking mechanism not implemented |
| useEnemyAI state | STUB | `useEnemyAI.ts` | useBattleOrchestrator:405 | Hook called but return value discarded |
| Resonance effects | STUB | `elementalSystem.ts:170-218` | None | getResonanceEffects function exists but never called in battle |

## Section 6: Immutable Files

| File | Reason |
|------|--------|
| `src/domain/cards/decks/deck.ts` | Marked IMMUTABLE in CLAUDE.md — createInitialDeck, drawCards, shuffleArray |
| `src/domain/cards/decks/deckReducer.ts` | Marked IMMUTABLE in CLAUDE.md — deckReducer state machine |

## Section 7: Known Duplications

| Item | Location A | Location B | Risk |
|------|-----------|-----------|------|
| `SWORD_ENERGY_MAX` (=10) | `classAbilityUtils.ts:15` | `characterConstants.ts:30` | Value drift |
| `DEFAULT_DAMAGE_MODIFIER` | `classAbilitySystem.ts:35-40` | `characterConstants.ts:37-42` | Value drift |
| `calculateCardEffect` | `card.ts:52-82` | `cardPlayLogic.ts:14-43` | Behavior divergence |
| `canPlayCard` | `card.ts` | `cardPlayLogic.ts` | Behavior divergence |
| Guard-only action check | `enemyPhaseExecution.ts:141-150` | `useEnemyAI.ts:177-179` | Logic divergence |
| `equipment_to_storage` move | `InventoryContext.tsx:441-483` | `InventoryContext.tsx:484-519` (equipSlotItem_to_storage) | API confusion |
| Targeted vs legacy enemy setters | `useBattleState.ts:460-494` | Same file | Unnecessary API surface |
| Healing pre-tick pattern | `playerPhaseExecution.ts:71-74` | `enemyPhaseExecution.ts:88-91` | Consistent bug in both |

## Section 8: Critical Data Flow Paths

### Card Play Pipeline
```
Player clicks card → canPlayCard(cost, energy, isPlayerPhase)
→ setPlayerEnergy(e - cost) → calculateCardEffect(card)
→ swordEnergy consume/gain → adjustedBase = baseDamage + swordEnergyFlat
→ elementalMod = getDamageModifier(card) → adjustedBase *= percentMultiplier
→ FOR each hit: calculateDamage(player, enemy, card) → applyDamageAllocation(enemy, damage)
→ lifesteal/reflect → guard application → heal application → energy gain
→ card draw → debuffs to enemy → buffs to player → card discard + mastery increment
→ bleed damage check
```

### Damage Calculation Pipeline
```
calculateDamage(attacker, defender, card)
→ baseDmg = card.baseDamage
→ atkMultiplier = 1.0 + atkUpMinor + atkUpMajor + momentum*stacks - atkDown
→ finalAtk = floor(baseDmg * atkMultiplier)
→ vulnMod = 1.0 + defDown (mitigated by tenacity)
→ reductionMod = 1.0 - defUp
→ incomingDmg = floor(finalAtk * vulnMod * reductionMod)
→ reflect = floor(incoming * 0.30), lifesteal = floor(incoming * 0.30)
→ applyDamageAllocation: Guard → AP → HP (bleed-through if guard absorbs + AP=0)
```

### Enemy Turn Pipeline
```
executeEnemyPhaseForIndex(enemyIndex)
→ calculateEnemyPhaseStart: tick buffs, heal, guard reset, energy reset, stun check
→ IF canAct: executeEnemyActions(energy loop):
  → WHILE energy > 0: determineEnemyAction(weighted random) → action
  → IF cost > remaining: getFallbackAction(5dmg)
  → onExecuteAction(action) → calculateEnemyAttackDamage → applyDamageAllocation to player
  → apply debuffs to player (appliedBy: 'enemy')
  → 800ms delay between actions
→ calculateEnemyPhaseEnd: DoT damage to enemy
→ executeNextPhase(queue, index + 1)
```

### Resource Spend Pipeline
```
Shop: resolveShopListing(typeId) → canAfford(price) → useGold(amount)
  → useGold deducts baseCamp first, then exploration
  → generateConsumableFromData(typeId) → add to inventory

Blacksmith: getLevelUpgradeCost(item) → gold check + magic stone value check
  → calculateStonesToConsume(stones, target) → smallest-first consumption
  → performLevelUpgrade(item) → stats *= nextMod / currentMod

Sanctuary: canUnlockNode(node, progress) → totalSouls >= cost
  → totalSouls -= cost → unlockedNodes.push(id) → recalculate effects
```

### Save/Load Pipeline
```
SAVE: PlayerContext + ResourceContext + InventoryContext → SaveData
  → { player stats, baseCamp gold/stones, storage items, equipment slots, sanctuary }
  → JSON.stringify → localStorage.setItem("roguelike_card_save")
  NOT SAVED: inventory items, equipmentInventory, exploration resources, guild, dungeon run

LOAD: localStorage.getItem → JSON.parse → version check ("1.0.0")
  → migrate() stub → return { success, data }
  → GameStateContext reads hasSave() at init → route to "camp" or "character_select"
```

### Dungeon Progression Pipeline
```
initializeRun(depth) → generateFloorMap(depth, config)
→ Row 0 = battle, Last row = boss, Middle = weighted random
→ generateConnections (forward-only, index mapping + 50% diagonal)
→ selectNode(nodeId) → mark current, skip others
→ Battle node: startBattle(config) → battle → completeCurrentNode(result)
  → victory: mark completed, unlock connected nodes
  → defeat: isActive = false
→ Boss victory: floor complete → advanceToNextFloor() → new map (same depth)
→ Dungeon exit:
  → Survive: transferExplorationToBaseCamp(0.6-1.0 multiplier)
  → Death: resetExplorationResources (exploration lost, baseCamp kept)
```
