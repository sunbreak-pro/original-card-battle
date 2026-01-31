import { useState, useRef, useEffect, useMemo, useCallback } from "react";
import type { Depth, Card } from "@/types/cardTypes";
import {
  useBattle,
  type InitialPlayerState,
} from "../../domain/battles/managements/battleFlowManage";
import { selectRandomEnemy } from "../../domain/characters/enemy/logic/enemyAI";
import { CardComponent } from "../cardHtml/CardComponent";
import { BattlingCardPileModal } from "../cardHtml/CardModalDisplay";
import { TurnOrderIndicator } from "./TurnOrderIndicator";
import EnemyFrame from "./EnemyFrame";
import PlayerFrame from "./PlayerFrame";
import VictoryScreen from "./VictoryScreen";
import DefeatScreen from "./DefeatScreen";
import UseItemModal from "./UseItemModal";
import { ElementalResonanceDisplay } from "./ElementalResonanceDisplay";
import "../css/battle/BattleScreen.css";
import { DEPTH_BACKGROUND_IMAGES } from "../../constants/uiConstants";
import type { Item } from "@/types/itemTypes";
import { neutralTheme } from "../../domain/dungeon/depth/deptManager";
import { usePlayer } from "../../contexts/PlayerContext";
import { useResources } from "../../contexts/ResourceContext";
import { useGameState } from "../../contexts/GameStateContext";
import { handlePlayerDeathWithDetails } from "../../domain/battles/logic/deathHandler";
import { saveManager } from "../../domain/save/logic/saveManager";
import { getInitialDeckCounts } from "../../constants/data/battles/initialDeckConfig";
import type { EncounterSize } from "@/types/characterTypes";
import {
  gainSoulFromEnemy,
  getSoulValue,
  calculateMagicStoneDrops,
  type EnemyType,
} from "../../domain/camps/logic/soulSystem";
import {
  executeItemEffect,
  applyBuffsToMap,
  clearDebuffsFromMap,
} from "../../domain/battles/logic/itemEffectExecutor";
import {
  attemptEscape,
  calculateEscapeChance,
} from "../../domain/battles/logic/escapeLogic";

/**
 * Collect mastery from all cards in deck and merge with existing store
 */
function collectMasteryFromDeck(
  cards: Card[],
  existingStore: Map<string, number>,
): Map<string, number> {
  const newStore = new Map(existingStore);
  cards.forEach((card) => {
    const cardTypeId = card.cardTypeId;
    const currentCount = newStore.get(cardTypeId) ?? 0;
    // Use the higher value between existing and card's useCount
    if (card.useCount > currentCount) {
      newStore.set(cardTypeId, card.useCount);
    }
  });
  return newStore;
}

const BattleScreen = ({
  depth,
  onReturnToCamp,
  onWin,
  onLose,
}: {
  depth: Depth;
  onDepthChange: (d: Depth) => void;
  onReturnToCamp?: () => void;
  onWin?: () => void;
  onLose?: () => void;
}) => {
  const theme = neutralTheme;
  const {
    playerData,
    updatePlayerData,
    runtimeState,
    updateRuntimeState,
    decreaseLives,
    isGameOver,
    setDifficulty,
    deckCards,
    applyEquipmentDurabilityDamage,
  } = usePlayer();
  const { addMagicStones } = useResources();
  const { navigateTo, gameState } = useGameState();

  // Get encounter size from battle config (default to "single")
  const encounterSize: EncounterSize =
    gameState.battleConfig?.enemyType || "single";
  // Map encounter size to EnemyType for soul/reward system
  const enemyType: EnemyType =
    encounterSize === "double"
      ? "double"
      : encounterSize === "three"
        ? "three"
        : encounterSize === "boss"
          ? "boss"
          : "single";

  // 遭遇カウント管理
  const [encounterCount, setEncounterCount] = useState(0);
  const deathHandledRef = useRef(false);
  const [soulsTransferred, setSoulsTransferred] = useState(0);

  // Use Item Modal state
  const [isItemModalOpen, setIsItemModalOpen] = useState(false);
  const [itemUsedThisPhase, setItemUsedThisPhase] = useState(false);

  // Escape state
  const [escapePhase, setEscapePhase] = useState<
    "attempting" | "success" | "failure" | null
  >(null);

  // Create initial player state from runtime state
  const initialPlayerState = useMemo<InitialPlayerState>(
    () => ({
      currentHp: runtimeState.currentHp,
      currentAp: runtimeState.currentAp,
      maxHp: playerData.persistent.baseMaxHp,
      maxAp: playerData.persistent.baseMaxAp,
      name: playerData.persistent.name,
      playerClass: playerData.persistent.playerClass,
      classGrade: playerData.persistent.classGrade,
      speed: playerData.persistent.baseSpeed,
      cardActEnergy: playerData.persistent.cardActEnergy,
      cardMasteryStore: runtimeState.cardMasteryStore,
      deckConfig: deckCards.length > 0
        ? deckCards.reduce<Record<string, number>>((acc, card) => {
            acc[card.cardTypeId] = (acc[card.cardTypeId] ?? 0) + 1;
            return acc;
          }, {})
        : getInitialDeckCounts(playerData.persistent.playerClass),
    }),
    [
      runtimeState.currentHp,
      runtimeState.currentAp,
      runtimeState.cardMasteryStore,
      playerData.persistent,
      deckCards,
    ],
  );

  const {
    playerRef,
    aliveEnemies,
    playerName,
    playerClass,
    playerHp,
    playerMaxHp,
    playerAp,
    playerMaxAp,
    playerGuard,
    playerBuffs,
    cardEnergy,
    maxEnergy,
    swordEnergy,
    phaseCount,
    turnMessage,
    showTurnMessage,
    hand,
    drawPile,
    discardPile,
    isNewCard,
    getDiscardingCards,
    handleCardPlay,
    handleEndPhase,
    resetForNextEnemy,
    openedPileType,
    openDrawPile,
    openDiscardPile,
    closePileModal,
    battleResult,
    battleStats,
    currentPhaseIndex,
    expandedPhaseEntries,
    selectedTargetIndex,
    setSelectedTargetIndex,
    isPlayerPhase,
    setPlayerBuffs,
    elementalState,
  } = useBattle(depth, undefined, initialPlayerState, encounterSize, {
    onApDamage: applyEquipmentDurabilityDamage,
  });

  // Handle player death penalty when defeated
  // Side effect (updatePlayerData, decreaseLives) must run in useEffect, not during render
  useEffect(() => {
    if (battleResult === "defeat" && !deathHandledRef.current) {
      const result = handlePlayerDeathWithDetails(playerData);
      updatePlayerData(result.updates);
      // eslint-disable-next-line react-hooks/set-state-in-effect -- one-time init guarded by ref
      setSoulsTransferred(result.soulsTransferred);
      decreaseLives();
      deathHandledRef.current = true;
    }
  }, [battleResult, playerData, updatePlayerData, decreaseLives]);

  // Reset itemUsedThisPhase when phase changes (render-time setState pattern)
  const [prevPhaseIndex, setPrevPhaseIndex] = useState(currentPhaseIndex);
  if (currentPhaseIndex !== prevPhaseIndex) {
    setPrevPhaseIndex(currentPhaseIndex);
    setItemUsedThisPhase(false);
  }

  const handleContinueToNextBattle = () => {
    const nextEncounter = encounterCount + 1;
    setEncounterCount(nextEncounter);

    let nextEncounterSize: EncounterSize = "single";
    if (nextEncounter === 7) {
      nextEncounterSize = "boss";
    } else if (nextEncounter % 3 === 0) {
      nextEncounterSize = "three";
    } else if (nextEncounter % 2 === 0) {
      nextEncounterSize = "double";
    }
    const { enemies: nextEnemies } = selectRandomEnemy(
      depth,
      nextEncounterSize,
    );
    resetForNextEnemy(nextEnemies);
  };

  // Handle using an item in battle
  const handleUseItem = useCallback(
    (item: Item) => {
      // Prevent using multiple items per phase
      if (itemUsedThisPhase) {
        console.log("Item already used this phase");
        return;
      }

      // Execute item effect
      const result = executeItemEffect(
        item,
        playerHp,
        playerMaxHp,
        playerBuffs,
        cardEnergy,
        maxEnergy,
      );

      if (!result.success) {
        console.log(`Item use failed: ${result.message}`);
        return;
      }

      // Apply effects to player state
      // Note: We need to update HP through the runtime state for persistence
      if (result.hpChange && result.hpChange > 0) {
        const newHp = Math.min(playerHp + result.hpChange, playerMaxHp);
        updateRuntimeState({ currentHp: newHp });
      }

      // Apply buffs if any
      if (result.buffsApplied && result.buffsApplied.length > 0) {
        setPlayerBuffs((prev) => applyBuffsToMap(prev, result.buffsApplied!));
      }

      // Clear debuffs if requested
      if (result.debuffsCleared) {
        setPlayerBuffs((prev) => clearDebuffsFromMap(prev));
      }

      // Remove or decrement item from inventory
      const itemIndex = playerData.inventory.inventory.items.findIndex(
        (i) => i.id === item.id,
      );

      if (itemIndex !== -1) {
        const items = [...playerData.inventory.inventory.items];
        const targetItem = items[itemIndex];

        if (
          targetItem.stackable &&
          targetItem.stackCount &&
          targetItem.stackCount > 1
        ) {
          // Decrement stack count
          items[itemIndex] = {
            ...targetItem,
            stackCount: targetItem.stackCount - 1,
          };
        } else {
          // Remove item entirely
          items.splice(itemIndex, 1);
        }

        // Update player inventory
        updatePlayerData({
          inventory: {
            ...playerData.inventory,
            inventory: {
              ...playerData.inventory.inventory,
              items,
              currentCapacity: items.length,
            },
          },
        });
      }

      // Mark item as used this phase
      setItemUsedThisPhase(true);

      console.log(`Used item: ${item.name} - ${result.message}`);
    },
    [
      itemUsedThisPhase,
      playerHp,
      playerMaxHp,
      playerBuffs,
      cardEnergy,
      maxEnergy,
      playerData.inventory,
      updateRuntimeState,
      updatePlayerData,
      setPlayerBuffs,
    ],
  );

  // Escape logic
  const isBossBattle = enemyType === "boss";
  const avgEnemySpeed =
    aliveEnemies.length > 0
      ? aliveEnemies.reduce((sum, e) => sum + e.definition.baseSpeed, 0) /
        aliveEnemies.length
      : 10;
  const escapeChance = calculateEscapeChance(
    playerData.persistent.baseSpeed,
    avgEnemySpeed,
  );

  const handleEscape = useCallback(() => {
    if (isBossBattle || escapePhase !== null) return;

    setEscapePhase("attempting");

    const success = attemptEscape(
      playerData.persistent.baseSpeed,
      avgEnemySpeed,
    );

    setTimeout(() => {
      if (success) {
        setEscapePhase("success");
        setTimeout(() => {
          setEscapePhase(null);
          if (onWin) {
            navigateTo("dungeon_map");
          } else if (onReturnToCamp) {
            onReturnToCamp();
          }
        }, 1500);
      } else {
        setEscapePhase("failure");
        setTimeout(() => {
          setEscapePhase(null);
          handleEndPhase();
        }, 1500);
      }
    }, 1200);
  }, [
    isBossBattle,
    escapePhase,
    playerData.persistent.baseSpeed,
    avgEnemySpeed,
    onWin,
    onReturnToCamp,
    navigateTo,
    handleEndPhase,
  ]);

  // Get inventory items for the modal
  const inventoryItems = playerData.inventory.inventory.items;

  // Victory reward calculations (computed regardless, only used when victory)
  const soulRemnants = getSoulValue(enemyType);
  const magicStones = calculateMagicStoneDrops(enemyType);

  const handleVictoryContinue = () => {
    const allCards = [...hand, ...drawPile, ...discardPile];
    const updatedMastery = collectMasteryFromDeck(
      allCards,
      runtimeState.cardMasteryStore,
    );

    const soulResult = gainSoulFromEnemy(
      playerData.progression.sanctuaryProgress,
      enemyType,
      false,
    );

    updatePlayerData({
      progression: {
        ...playerData.progression,
        sanctuaryProgress: soulResult.newProgress,
      },
    });

    addMagicStones(magicStones, false);

    updateRuntimeState({
      currentHp: playerHp,
      currentAp: playerAp,
      cardMasteryStore: updatedMastery,
    });

    if (onWin) {
      onWin();
    } else {
      handleContinueToNextBattle();
    }
  };

  // Defeat handler
  const gameOver = battleResult === "defeat" ? isGameOver() : false;

  const handleDefeatAction = () => {
    if (gameOver) {
      saveManager.deleteSave();
      setDifficulty("normal");
      navigateTo("character_select");
    } else if (onLose) {
      onLose();
    } else if (onReturnToCamp) {
      onReturnToCamp();
    }
  };

  return (
    <div
      className="battle-screen"
      style={{
        backgroundImage: `url('${DEPTH_BACKGROUND_IMAGES[depth] ?? DEPTH_BACKGROUND_IMAGES[4]}')`,
      }}
    >
      {showTurnMessage && (
        <div className="turn-message-slide">
          <div className="turn-message-text">{turnMessage}</div>
        </div>
      )}
      {escapePhase && (
        <div className={`turn-message-slide escape-${escapePhase}`}>
          <div className="turn-message-text">
            {escapePhase === "attempting" && "逃走を試みる..."}
            {escapePhase === "success" && "うまく逃げ出した！"}
            {escapePhase === "failure" && "逃走失敗！"}
          </div>
        </div>
      )}
      <div className="battle-header">
        <div className="depth-info">
          {depth}-{encounterCount === 6 ? "BOSS" : encounterCount + 1} | Phase{" "}
          {phaseCount}
        </div>
        <TurnOrderIndicator
          expandedEntries={expandedPhaseEntries}
          currentPhaseIndex={currentPhaseIndex}
          enemyCount={aliveEnemies.length}
        />
      </div>

      {/* Class Ability Display */}
      {playerClass === "swordsman" && (
        <div className="class-ability-header">
          <div className="sword-energy-display">
            <div className="sword-energy-label">剣気:</div>
            <div className="sword-energy-bar-container">
              <div className="sword-energy-bar">
                <div
                  className={`sword-energy-fill ${
                    swordEnergy.current >= 10
                      ? "level-max"
                      : swordEnergy.current >= 8
                        ? "level-high"
                        : swordEnergy.current >= 5
                          ? "level-mid"
                          : ""
                  }`}
                  style={{
                    width: `${(swordEnergy.current / swordEnergy.max) * 100}%`,
                  }}
                />
                <span className="sword-energy-text">
                  {swordEnergy.current}/{swordEnergy.max}
                </span>
              </div>
            </div>
            <div className="sword-energy-effects">
              <span
                className={`effect-badge crit ${swordEnergy.current > 0 ? "active" : "inactive"}`}
              >
                physical damage: + {swordEnergy.current}
              </span>
              <span
                className={`effect-badge pierce ${swordEnergy.current >= 8 ? "active" : "inactive"}`}
              >
                {swordEnergy.current >= 8 ? "✓" : "○"} 貫通+30%
              </span>
              <span
                className={`effect-badge max ${swordEnergy.current >= 10 ? "active" : "inactive"}`}
              >
                {swordEnergy.current >= 10 ? "✓" : "○"} MAX
              </span>
            </div>
          </div>
        </div>
      )}
      {playerClass === "mage" && (
        <div className="class-ability-header">
          <ElementalResonanceDisplay elementalState={elementalState} />
        </div>
      )}

      <div className="battle-field">
        <EnemyFrame
          enemies={aliveEnemies.map((e) => ({
            definition: e.definition,
            hp: e.hp,
            maxHp: e.maxHp,
            ap: e.ap,
            maxAp: e.maxAp,
            guard: e.guard,
            actEnergy: e.energy,
            buffDebuffs: e.buffDebuffs,
            turnCount: phaseCount,
          }))}
          enemyRefs={aliveEnemies.map((e) => e.ref)}
          theme={theme}
          selectedTargetIndex={selectedTargetIndex}
          onSelectTarget={setSelectedTargetIndex}
          isPlayerPhase={isPlayerPhase}
        />
        <PlayerFrame
          playerName={playerName}
          playerClass={playerClass}
          playerRef={playerRef}
          playerHp={playerHp}
          playerMaxHp={playerMaxHp}
          playerAp={playerAp}
          playerMaxAp={playerMaxAp}
          playerGuard={playerGuard}
          playerBuffs={playerBuffs}
          cardEnergy={cardEnergy}
          maxEnergy={maxEnergy}
          theme={theme}
        />
      </div>
      <div className="hand-container">
        {/* Left Section: Draw/Discard Piles */}
        <div className="pile-section">
          <div
            className="pile-icon draw"
            title="Draw Pile"
            onClick={openDrawPile}
          >
            <div className="pile-visual">🎴</div>
            <div className="pile-count">山札: {drawPile.length}</div>
          </div>
          <div
            className="pile-icon discard"
            title="Discard Pile"
            onClick={openDiscardPile}
          >
            <div className="pile-visual">🗑️</div>
            <div className="pile-count">捨て札: {discardPile.length}</div>
          </div>
        </div>

        {/* Center Section: Cards */}
        <div className="card-container">
          {hand.map((card, index) => {
            const isDrawing = isNewCard(card.id);
            const isDiscarding = getDiscardingCards().some(
              (c) => c.id === card.id,
            );

            const totalCards = hand.length;
            const offset = index - (totalCards - 1) / 2;
            const translateY = Math.abs(offset) * 1.5 - 1.8;
            const rotation = offset * 4.2;

            return (
              <div
                key={card.id}
                className={`card-wrapper ${isDrawing ? "drawing" : ""} ${
                  isDiscarding ? "discarding" : ""
                }`}
                style={
                  {
                    "--rot": `${rotation}deg`,
                    "--y": `${translateY * 0.5}vh`,
                    animationDelay: isDrawing
                      ? `${index * 0.1}s`
                      : isDiscarding
                        ? `${index * 0.05}s`
                        : "0s",
                  } as React.CSSProperties
                }
                onClick={(e) => handleCardPlay(card, e.currentTarget)}
              >
                <CardComponent
                  card={card}
                  depth={depth}
                  isPlayable={card.cost <= cardEnergy && !isDiscarding}
                />
              </div>
            );
          })}
        </div>

        {/* Right Section: Action Buttons */}
        <div className="button-frame">
          <button className="action-btn end-phase-btn" onClick={handleEndPhase}>
            End Phase
          </button>
          <button
            className="action-btn use-item-btn"
            onClick={() => setIsItemModalOpen(true)}
          >
            Use Item
          </button>
          <button
            className="action-btn flee-btn"
            onClick={handleEscape}
            disabled={isBossBattle}
            title={
              isBossBattle
                ? "ボス戦では逃走不可"
                : `逃走確率: ${Math.round(escapeChance * 100)}%`
            }
          >
            {isBossBattle ? "逃走不可" : "逃走"}
          </button>
        </div>
      </div>
      <BattlingCardPileModal
        isOpen={openedPileType !== null}
        onClose={closePileModal}
        title={openedPileType === "draw" ? "山札" : "捨て札"}
        cards={openedPileType === "draw" ? drawPile : discardPile}
        depth={depth}
      />
      <UseItemModal
        isOpen={isItemModalOpen}
        onClose={() => setIsItemModalOpen(false)}
        items={inventoryItems}
        onUseItem={handleUseItem}
        itemUsedThisPhase={itemUsedThisPhase}
      />

      {/* Victory/Defeat overlays */}
      {battleResult === "victory" && (
        <VictoryScreen
          onContinue={handleVictoryContinue}
          rewards={{ soulRemnants, magicStones, cards: [] }}
          battleStats={{
            phaseCount: phaseCount,
            damageDealt: battleStats.damageDealt,
            damageTaken: battleStats.damageTaken,
          }}
          enemyType={enemyType}
        />
      )}
      {battleResult === "defeat" && (
        <DefeatScreen
          onRetry={() => window.location.reload()}
          onReturnToCamp={handleDefeatAction}
          battleStats={{
            turnCount: phaseCount,
            damageDealt: battleStats.damageDealt,
            damageTaken: battleStats.damageTaken,
          }}
          remainingLives={runtimeState.lives.currentLives}
          maxLives={runtimeState.lives.maxLives}
          soulsTransferred={soulsTransferred}
          isGameOver={gameOver}
        />
      )}
    </div>
  );
};

export default BattleScreen;
