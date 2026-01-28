import { useState, useRef, useMemo, useCallback } from "react";
import type { Depth, Card } from "../../domain/cards/type/cardType";
import {
  useBattle,
  type InitialPlayerState,
} from "../../domain/battles/managements/battleFlowManage";
import { selectRandomEnemy } from "../../domain/characters/enemy/logic/enemyAI";
import { CardComponent } from "../cardHtml/CardComponent";
import { BattlingCardPileModal } from "../cardHtml/CardModalDisplay";
import { TurnOrderIndicator } from "./TurnOrderIndicator";
import EnemyFrame from "./EnemyFrame";
import PlayerFrame from "./playerFrame";
import VictoryScreen from "./VictoryScreen";
import DefeatScreen from "./DefeatScreen";
import UseItemModal from "./UseItemModal";
import "../css/battle/BattleScreen.css";
import type { Item } from "../../domain/item_equipment/type/ItemTypes";
import { neutralTheme } from "../../domain/dungeon/depth/deptManager";
import { usePlayer } from "../../contexts/PlayerContext";
import { useResources } from "../../contexts/ResourceContext";
import { useGameState } from "../../contexts/GameStateContext";
import { handlePlayerDeathWithDetails } from "../../domain/battles/logic/deathHandler";
import { saveManager } from "../../domain/save/logic/saveManager";
import { getInitialDeckCounts } from "../../domain/battles/data/initialDeckConfig";
import {
  gainSoulFromEnemy,
  getSoulValue,
  calculateMagicStoneDrops,
  type EnemyType,
} from "../../domain/camps/logic/soulSystem";
import { executeItemEffect } from "../../domain/battles/logic/itemEffectExecutor";

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
  } = usePlayer();
  const { addMagicStones } = useResources();
  const { navigateTo, gameState } = useGameState();

  // Get enemy type from battle config (default to "normal")
  const enemyType: EnemyType = gameState.battleConfig?.enemyType || "normal";

  // 遭遇カウント管理
  const [encounterCount, setEncounterCount] = useState(0);
  const deathHandledRef = useRef(false);
  const soulsTransferredRef = useRef(0);

  // Use Item Modal state
  const [isItemModalOpen, setIsItemModalOpen] = useState(false);
  const [itemUsedThisPhase, setItemUsedThisPhase] = useState(false);

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
      deckConfig: getInitialDeckCounts(playerData.persistent.playerClass),
    }),
    [
      runtimeState.currentHp,
      runtimeState.currentAp,
      runtimeState.cardMasteryStore,
      playerData.persistent,
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
    phaseQueue,
    currentPhaseIndex,
  } = useBattle(depth, undefined, initialPlayerState);

  // Handle player death penalty when defeated
  // Runs once when defeat is detected; uses ref to avoid re-render
  if (battleResult === "defeat" && !deathHandledRef.current) {
    const result = handlePlayerDeathWithDetails(playerData);
    updatePlayerData(result.updates);
    soulsTransferredRef.current = result.soulsTransferred;
    decreaseLives();
    deathHandledRef.current = true;
  }

  // Reset itemUsedThisPhase when phase changes (render-time setState pattern)
  const prevPhaseIndexRef = useRef(currentPhaseIndex);
  if (currentPhaseIndex !== prevPhaseIndexRef.current) {
    prevPhaseIndexRef.current = currentPhaseIndex;
    setItemUsedThisPhase(false);
  }

  const handleContinueToNextBattle = () => {
    const nextEncounter = encounterCount + 1;
    setEncounterCount(nextEncounter);

    let encounterType: "normal" | "group" | "boss" = "normal";
    if (nextEncounter === 7) {
      encounterType = "boss";
    } else if (nextEncounter % 3 === 0) {
      encounterType = "group";
    }
    const { enemies: nextEnemies } = selectRandomEnemy(depth, encounterType);
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
        // Buffs would need to be applied through battle state
        // This is a simplified version - full implementation would
        // need to integrate with useBattle's buff system
        console.log("Buffs applied:", result.buffsApplied);
      }

      // Clear debuffs if requested
      if (result.debuffsCleared) {
        console.log("Debuffs cleared");
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
    ],
  );

  // Get inventory items for the modal
  const inventoryItems = playerData.inventory.inventory.items;

  if (battleResult === "victory") {
    // Calculate rewards based on enemy type
    const soulRemnants = getSoulValue(enemyType);
    const magicStones = calculateMagicStoneDrops(enemyType);

    // If onWin callback is provided (dungeon mode), wrap it to save HP/AP and mastery first
    const handleVictoryContinue = () => {
      // Collect mastery from all cards in deck
      const allCards = [...hand, ...drawPile, ...discardPile];
      const updatedMastery = collectMasteryFromDeck(
        allCards,
        runtimeState.cardMasteryStore,
      );

      // Gain souls from enemy defeat (updates sanctuary progress)
      const soulResult = gainSoulFromEnemy(
        playerData.progression.sanctuaryProgress,
        enemyType,
        false, // isReturnBattle
      );

      // Update player data with new sanctuary progress
      updatePlayerData({
        progression: {
          ...playerData.progression,
          sanctuaryProgress: soulResult.newProgress,
        },
      });

      // Add magic stones to exploration resources
      addMagicStones(magicStones, false); // false = add to exploration

      // Save current HP/AP and card mastery to runtime state
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

    return (
      <VictoryScreen
        onContinue={handleVictoryContinue}
        rewards={{
          soulRemnants,
          magicStones,
          cards: [],
        }}
        battleStats={{
          phaseCount: phaseCount,
          damageDealt: battleStats.damageDealt,
          damageTaken: battleStats.damageTaken,
        }}
        enemyType={enemyType}
      />
    );
  }

  if (battleResult === "defeat") {
    const gameOver = isGameOver();

    // Handle "Return to Camp" or "New Game" depending on game over state
    const handleDefeatAction = () => {
      if (gameOver) {
        // Game Over: Delete save data, reset difficulty, and go to character select
        saveManager.deleteSave();
        setDifficulty("normal"); // Reset to default difficulty
        navigateTo("character_select");
      } else if (onLose) {
        onLose();
      } else if (onReturnToCamp) {
        onReturnToCamp();
      }
    };

    return (
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
        soulsTransferred={soulsTransferredRef.current}
        isGameOver={gameOver}
      />
    );
  }

  return (
    <div className="battle-screen">
      {showTurnMessage && (
        <div className="turn-message-slide">
          <div className="turn-message-text">{turnMessage}</div>
        </div>
      )}
      <div className="battle-header">
        <div className="depth-info">
          {depth}-{encounterCount === 6 ? "BOSS" : encounterCount + 1} | Phase{" "}
          {phaseCount}
        </div>
        <TurnOrderIndicator
          phaseQueue={phaseQueue}
          currentPhaseIndex={currentPhaseIndex}
        />
      </div>

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
          swordEnergy={swordEnergy}
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
            onClick={() => console.log("Fleeing Combat: 未実装")}
          >
            Fleeing Combat
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
    </div>
  );
};

export default BattleScreen;
