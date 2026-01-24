/**
 * Battle Orchestrator Hook
 *
 * Main hook that orchestrates all battle-related hooks:
 * - useBattleState: HP/AP/Guard/Buff state management
 * - useBattlePhase: Phase queue and turn management
 * - useCardExecution: Card effect execution
 * - useEnemyAI: Enemy action determination and execution
 * - useClassAbility: Class-specific ability management
 *
 * This hook provides the same interface as the original useBattle hook
 * for backward compatibility while using the new modular structure internally.
 */

import { useState, useRef, useReducer, useEffect, useCallback, useMemo } from "react";
import type { Card, Depth } from "../../cards/type/cardType";
import type { EnemyDefinition } from "../../characters/type/enemyType";
import type { PhaseQueue } from "../type/phaseType";
import { createInitialSwordEnergy } from "../../characters/type/classAbilityTypes";

// Deck management (IMMUTABLE ZONE - DO NOT MODIFY)
import { deckReducer } from "../../cards/decks/deckReducter";
import { createInitialDeck, shuffleArray } from "../../cards/decks/deck";
import { SWORDSMAN_CARDS_ARRAY } from "../../cards/data/SwordmanCards";
import { INITIAL_DECK_COUNTS } from "../data/initialDeckConfig";

// Card mastery management
import { applyMasteryToCards } from "../../cards/state/masteryManager";

// Animation hooks
import { useCardAnimation } from "../../../ui/commonHtml/useCardAnimation";
import { useTurnTransition } from "../../../ui/animations/usePhaseTransition";

// Battle hooks
import { useBattlePhase } from "./useBattlePhase";
import { useCharacterPhaseExecution } from "./executeCharacterManage";
import { useBattleState, type InitialPlayerState } from "./useBattleState";
import { useCardExecution, type CardAnimationHandlers, type CardExecutionSetters, type DeckDispatch } from "./useCardExecution";
import { useEnemyAI } from "./useEnemyAI";
import { useSwordEnergy } from "./useClassAbility";

// Player data
import { Swordman_Status } from "../../characters/player/data/PlayerData";

// Phase execution
import { calculatePlayerPhaseEnd } from "../execution/playerPhaseExecution";

// Enemy action preview
import { previewEnemyActions } from "../../characters/enemy/logic/enemyActionExecution";

// Enemy state helper
import { createEnemyStateFromDefinition } from "../logic/enemyStateLogic";

// ============================================================================
// Main Hook
// ============================================================================

/**
 * Battle orchestrator hook
 *
 * This is the main entry point for battle state and logic.
 * It composes all the modular battle hooks and provides a unified interface.
 *
 * @param depth - Current dungeon depth for enemy selection
 * @param initialEnemies - Optional initial enemies (overrides random selection)
 * @param initialPlayerState - Optional initial player state (HP/AP from PlayerContext)
 */
export const useBattleOrchestrator = (
  depth: Depth,
  initialEnemies?: EnemyDefinition[],
  initialPlayerState?: InitialPlayerState
) => {
  // ========================================================================
  // Animation Hooks
  // ========================================================================

  const {
    drawCardsWithAnimation,
    discardCardsWithAnimation,
    playCardWithAnimation,
    showDamageEffect,
    showHealEffect,
    showShieldEffect,
    isNewCard,
    getDiscardingCards,
  } = useCardAnimation();

  const { turnMessage, showTurnMessage, showMessage } = useTurnTransition();

  // ========================================================================
  // Phase Management Hook
  // ========================================================================

  const phaseState = useBattlePhase(Swordman_Status.speed);

  // ========================================================================
  // Character Phase Execution Hook
  // ========================================================================

  const {
    executePlayerPhase: executePlayerPhaseImpl,
    executeEnemyPhase: executeEnemyPhaseImpl,
  } = useCharacterPhaseExecution();

  // ========================================================================
  // Refs
  // ========================================================================

  const playerRef = useRef<HTMLDivElement>(null);
  const drawnCardsRef = useRef<Card[]>([]);

  // ========================================================================
  // Battle State Hook
  // ========================================================================

  const battleState = useBattleState(depth, initialEnemies, phaseState.playerSpeed, initialPlayerState);

  const {
    // Player state
    playerState,
    setPlayerHp,
    setPlayerAp,
    setPlayerGuard,
    setPlayerEnergy,
    setPlayerBuffs,

    // Enemy state
    enemies,
    currentEnemy,
    setEnemies,
    setEnemyHp,
    setEnemyAp,
    setEnemyGuard,
    setEnemyBuffs,
    updateEnemyByUpdater,
    updateAllEnemies,

    // Derived values
    aliveEnemies,
    isPlayerAlive,
    areAllEnemiesDead,

    // Legacy derived values
    enemyHp,
    enemyMaxHp,
    enemyAp,
    enemyMaxAp,
    enemyGuard,
    enemyBuffs,
    enemyEnergy,
    setEnemyEnergy,

    // BattleStats objects
    playerBattleStats,
    enemyBattleStats,
  } = battleState;

  // ========================================================================
  // Class Ability Hook (Sword Energy)
  // ========================================================================

  const swordEnergyHook = useSwordEnergy();
  const { abilityState: swordEnergy, setAbilityState: setSwordEnergy } = swordEnergyHook;

  // ========================================================================
  // Deck State
  // ========================================================================

  const initialDeckState = useMemo(() => {
    let initialDeck = createInitialDeck(INITIAL_DECK_COUNTS, SWORDSMAN_CARDS_ARRAY);

    // Apply mastery from saved store if available
    if (initialPlayerState?.cardMasteryStore && initialPlayerState.cardMasteryStore.size > 0) {
      initialDeck = applyMasteryToCards(initialDeck, initialPlayerState.cardMasteryStore);
    }

    return { hand: [], drawPile: initialDeck, discardPile: [] };
  }, [initialPlayerState?.cardMasteryStore]);

  const [deckState, dispatch] = useReducer(deckReducer, initialDeckState);
  const deckStateRef = useRef(deckState);

  useEffect(() => {
    deckStateRef.current = deckState;
  }, [deckState]);

  const [isShuffling, setIsShuffling] = useState(false);
  const [isDrawingAnimation, setIsDrawingAnimation] = useState(false);

  // ========================================================================
  // Modal State
  // ========================================================================

  const [openedPileType, setOpenedPileType] = useState<"draw" | "discard" | null>(null);

  // ========================================================================
  // Battle Result State
  // ========================================================================

  const [battleResult, setBattleResult] = useState<"ongoing" | "victory" | "defeat">("ongoing");

  // ========================================================================
  // Battle Statistics
  // ========================================================================

  const [battleStats, setBattleStats] = useState({
    damageDealt: 0,
    damageTaken: 0,
  });

  // ========================================================================
  // Helper Functions
  // ========================================================================

  const getTargetEnemyRef = useCallback(() => {
    const aliveEnemy = enemies.find((e) => e.hp > 0);
    return aliveEnemy?.ref.current ?? null;
  }, [enemies]);

  // Legacy updateEnemy for backward compatibility
  const updateEnemy = useCallback(
    (index: number, updater: (state: typeof enemies[0]) => Partial<typeof enemies[0]>) => {
      updateEnemyByUpdater(index, updater);
    },
    [updateEnemyByUpdater]
  );

  // ========================================================================
  // Card Execution Hook
  // ========================================================================

  const animationHandlers: CardAnimationHandlers = useMemo(
    () => ({
      playCardWithAnimation,
      showDamageEffect,
      showHealEffect,
      showShieldEffect,
      drawCardsWithAnimation,
    }),
    [playCardWithAnimation, showDamageEffect, showHealEffect, showShieldEffect, drawCardsWithAnimation]
  );

  const cardSetters: CardExecutionSetters = useMemo(
    () => ({
      setPlayerEnergy,
      setPlayerGuard,
      setPlayerHp,
      setPlayerBuffs,
      setEnemyHp,
      setEnemyAp,
      setEnemyGuard,
      setEnemyBuffs,
      setSwordEnergy,
      setBattleStats,
    }),
    [
      setPlayerEnergy,
      setPlayerGuard,
      setPlayerHp,
      setPlayerBuffs,
      setEnemyHp,
      setEnemyAp,
      setEnemyGuard,
      setEnemyBuffs,
      setSwordEnergy,
    ]
  );

  const cardExecution = useCardExecution(
    playerBattleStats,
    enemyBattleStats,
    playerState.energy,
    playerState.maxEnergy,
    playerState.hp,
    playerState.maxHp,
    playerState.buffs,
    enemyBuffs,
    swordEnergy,
    phaseState.isPlayerPhase,
    playerRef,
    getTargetEnemyRef,
    deckStateRef,
    cardSetters,
    animationHandlers,
    dispatch as DeckDispatch
  );

  // ========================================================================
  // Enemy AI Hook (Available for future use)
  // ========================================================================

  // Note: enemyAI hook is initialized but currently the enemy phase execution
  // is handled by executeCharacterManage. This will be refactored in a future update.
  useEnemyAI();

  // ========================================================================
  // Card Play Handler
  // ========================================================================

  const handleCardPlay = useCallback(
    async (card: Card, cardElement?: HTMLElement) => {
      await cardExecution.executeCard(card, cardElement);
    },
    [cardExecution]
  );

  // ========================================================================
  // Phase Execution
  // ========================================================================

  const executePlayerPhase = useCallback(async () => {
    await executePlayerPhaseImpl({
      playerBuffs: playerState.buffs,
      playerMaxHp: playerState.maxHp,
      maxEnergy: playerState.maxEnergy,
      deckStateRef,
      drawnCardsRef,
      playerRef,
      setPlayerGuard,
      setPlayerEnergy,
      setPlayerBuffs,
      setPlayerHp,
      setIsShuffling,
      setIsDrawingAnimation,
      showMessage,
      showHealEffect,
      showShieldEffect,
      phaseState: {
        setPlayerPhaseActive: phaseState.setPlayerPhaseActive,
        setEnemyPhaseActive: phaseState.setEnemyPhaseActive,
        incrementPhaseCount: phaseState.incrementPhaseCount,
        clearActivePhase: phaseState.clearActivePhase,
      },
      dispatch,
    });
  }, [
    executePlayerPhaseImpl,
    playerState.buffs,
    playerState.maxHp,
    playerState.maxEnergy,
    setPlayerGuard,
    setPlayerEnergy,
    setPlayerBuffs,
    setPlayerHp,
    showMessage,
    showHealEffect,
    showShieldEffect,
    phaseState,
  ]);

  const executeEnemyPhase = useCallback(async () => {
    await executeEnemyPhaseImpl({
      currentEnemy: currentEnemy!,
      enemyBuffs,
      enemyHp,
      enemyMaxHp,
      enemyEnergy,
      playerHp: playerState.hp,
      playerBuffs: playerState.buffs,
      enemies,
      enemyStats: enemyBattleStats,
      playerStats: playerBattleStats,
      playerRef,
      setEnemyGuard,
      setEnemyEnergy,
      setEnemyBuffs,
      setEnemyHp,
      setPlayerGuard,
      setPlayerAp,
      setPlayerHp,
      setPlayerBuffs,
      setBattleStats,
      showMessage,
      showDamageEffect,
      getTargetEnemyRef,
      phaseState: {
        setPlayerPhaseActive: phaseState.setPlayerPhaseActive,
        setEnemyPhaseActive: phaseState.setEnemyPhaseActive,
        incrementPhaseCount: phaseState.incrementPhaseCount,
        clearActivePhase: phaseState.clearActivePhase,
      },
    });
  }, [
    executeEnemyPhaseImpl,
    currentEnemy,
    enemyBuffs,
    enemyHp,
    enemyMaxHp,
    enemyEnergy,
    playerState.hp,
    playerState.buffs,
    enemies,
    enemyBattleStats,
    playerBattleStats,
    setEnemyGuard,
    setEnemyEnergy,
    setEnemyBuffs,
    setEnemyHp,
    setPlayerGuard,
    setPlayerAp,
    setPlayerHp,
    setPlayerBuffs,
    showMessage,
    showDamageEffect,
    getTargetEnemyRef,
    phaseState,
  ]);

  // ========================================================================
  // Battle Flow Control
  // ========================================================================

  const executeNextPhaseRef = useRef<(queue: PhaseQueue, index: number) => Promise<void>>(
    async () => { }
  );

  const executeNextPhaseImpl = useCallback(
    async (queue: PhaseQueue, index: number) => {
      // Check battle end
      if (areAllEnemiesDead || !isPlayerAlive) {
        return;
      }

      if (index >= queue.phases.length) {
        // All phases complete - generate new queue and start new round
        const newQueue = phaseState.generatePhaseQueueFromSpeeds(
          playerState.buffs,
          currentEnemy ?? null,
          enemyBuffs
        );
        await executeNextPhaseRef.current(newQueue, 0);
        return;
      }

      // Update current phase index for UI display
      phaseState.setPhaseIndex(index);

      const currentActor = queue.phases[index];

      if (currentActor === "player") {
        await executePlayerPhase();
        // Player phase waits for handleEndPhase to advance
      } else {
        await executeEnemyPhase();
        // Enemy phase auto-advances
        await executeNextPhaseRef.current(queue, index + 1);
      }
    },
    [
      areAllEnemiesDead,
      isPlayerAlive,
      executePlayerPhase,
      executeEnemyPhase,
      phaseState,
      playerState.buffs,
      currentEnemy,
      enemyBuffs,
    ]
  );

  useEffect(() => {
    executeNextPhaseRef.current = executeNextPhaseImpl;
  }, [executeNextPhaseImpl]);

  const executeNextPhase = useCallback(async (queue: PhaseQueue, index: number) => {
    await executeNextPhaseRef.current(queue, index);
  }, []);

  const initializeBattle = useCallback(async () => {
    const queue = phaseState.generatePhaseQueueFromSpeeds(
      playerState.buffs,
      currentEnemy ?? null,
      enemyBuffs
    );
    await executeNextPhase(queue, 0);
  }, [playerState.buffs, currentEnemy, enemyBuffs, phaseState, executeNextPhase]);

  const handleEndPhase = useCallback(() => {
    if (!phaseState.isPlayerPhase) return;

    // Clear phase state
    phaseState.clearActivePhase();

    // Calculate phase end effects
    const phaseEndResult = calculatePlayerPhaseEnd({ playerBuffs: playerState.buffs });

    // Apply DoT damage
    if (phaseEndResult.dotDamage > 0) {
      setPlayerHp((h) => Math.max(0, h - phaseEndResult.dotDamage));
      if (playerRef.current) {
        showDamageEffect(playerRef.current, phaseEndResult.dotDamage, false);
      }
    }

    // Update buffs (momentum stacking)
    setPlayerBuffs(phaseEndResult.newBuffs);

    // Discard hand and advance to next phase
    const cardsToDiscard = [...deckState.hand];
    const nextPhaseIndex = phaseState.currentPhaseIndex + 1;
    discardCardsWithAnimation(cardsToDiscard, 250, () => {
      dispatch({ type: "END_TURN", cardsToDiscard });

      // Advance to next phase in queue
      if (phaseState.phaseQueue) {
        executeNextPhase(phaseState.phaseQueue, nextPhaseIndex);
      }
    });
  }, [
    phaseState,
    playerState.buffs,
    showDamageEffect,
    setPlayerHp,
    setPlayerBuffs,
    deckState.hand,
    discardCardsWithAnimation,
    executeNextPhase,
  ]);

  // ========================================================================
  // Auto-start Battle
  // ========================================================================

  const battleInitializedRef = useRef(false);

  useEffect(() => {
    if (!battleInitializedRef.current && currentEnemy) {
      battleInitializedRef.current = true;
      initializeBattle();
    }
  }, [currentEnemy, initializeBattle]);

  // ========================================================================
  // Animation Effects
  // ========================================================================

  useEffect(() => {
    if (isShuffling) {
      showMessage("山札が尽きました...デッキをシャッフルします", 1500, () => {
        setTimeout(() => {
          setIsShuffling(false);
          setIsDrawingAnimation(true);
        }, 1000);
      });
    }
  }, [isShuffling, showMessage]);

  useEffect(() => {
    if (isDrawingAnimation && drawnCardsRef.current.length > 0) {
      drawCardsWithAnimation(
        drawnCardsRef.current,
        (cards) => {
          dispatch({ type: "ADD_TO_HAND", cards });
          drawnCardsRef.current = [];
          setIsDrawingAnimation(false);
        },
        250
      );
    }
  }, [isDrawingAnimation, drawCardsWithAnimation]);

  // ========================================================================
  // Modal Controls
  // ========================================================================

  const openDrawPile = useCallback(() => setOpenedPileType("draw"), []);
  const openDiscardPile = useCallback(() => setOpenedPileType("discard"), []);
  const closePileModal = useCallback(() => setOpenedPileType(null), []);

  // ========================================================================
  // Reset for Next Enemy
  // ========================================================================

  const resetForNextEnemy = useCallback(
    (nextEnemy: EnemyDefinition | EnemyDefinition[]) => {
      const nextEnemies = Array.isArray(nextEnemy) ? nextEnemy : [nextEnemy];
      setEnemies(nextEnemies.map((def) => createEnemyStateFromDefinition(def)));

      setPlayerGuard(0);
      setPlayerBuffs(new Map());
      setPlayerEnergy(playerState.maxEnergy);
      setSwordEnergy(createInitialSwordEnergy());

      // Collect all cards and shuffle
      const allCards = [
        ...deckStateRef.current.hand,
        ...deckStateRef.current.drawPile,
        ...deckStateRef.current.discardPile,
      ];
      const shuffledDeck = shuffleArray(allCards);
      dispatch({ type: "RESET_DECK", hand: [], drawPile: shuffledDeck, discardPile: [] });

      // Reset phase state
      phaseState.resetPhaseState();
      setBattleStats({ damageDealt: 0, damageTaken: 0 });
      setBattleResult("ongoing");

      // Allow battle to reinitialize
      battleInitializedRef.current = false;
    },
    [
      setEnemies,
      setPlayerGuard,
      setPlayerBuffs,
      setPlayerEnergy,
      setSwordEnergy,
      playerState.maxEnergy,
      phaseState,
    ]
  );

  // ========================================================================
  // Battle Result
  // ========================================================================

  const actualBattleResult = useMemo(() => {
    if (battleResult !== "ongoing") return battleResult;
    if (areAllEnemiesDead) return "victory";
    if (!isPlayerAlive) return "defeat";
    return "ongoing";
  }, [battleResult, areAllEnemiesDead, isPlayerAlive]);

  // ========================================================================
  // Next Enemy Actions Preview
  // ========================================================================

  const nextEnemyActions = useMemo(() => {
    if (!currentEnemy || enemyHp <= 0) {
      return [];
    }
    return previewEnemyActions(currentEnemy, enemyHp, enemyMaxHp, phaseState.phaseCount + 1);
  }, [currentEnemy, enemyHp, phaseState.phaseCount]);

  // ========================================================================
  // Return - Backward Compatible Interface
  // ========================================================================

  return {
    // Refs
    playerRef,
    getTargetEnemyRef,

    // Enemy state
    currentEnemy,
    enemies,
    aliveEnemies,
    updateEnemy,
    updateAllEnemies,

    // Player state
    playerName: playerState.name,
    playerClass: playerState.playerClass,
    playerHp: playerState.hp,
    playerMaxHp: playerState.maxHp,
    playerAp: playerState.ap,
    playerMaxAp: playerState.maxAp,
    playerGuard: playerState.guard,
    playerBuffs: playerState.buffs,

    // Enemy derived state
    enemyHp,
    enemyMaxHp,
    enemyAp,
    enemyMaxAp,
    enemyGuard,
    enemyBuffs,

    // Phase state
    playerEnergy: playerState.energy,
    cardEnergy: playerState.energy, // Alias for backward compatibility
    maxEnergy: playerState.maxEnergy,
    phaseCount: phaseState.phaseCount,
    isPlayerPhase: phaseState.isPlayerPhase,
    turnMessage,
    showTurnMessage,
    playerNowSpeed: phaseState.playerSpeed,
    enemyNowSpeed: phaseState.enemySpeed,
    phaseQueue: phaseState.phaseQueue,
    currentPhaseIndex: phaseState.currentPhaseIndex,
    enemyEnergy,
    nextEnemyActions,

    // Sword Energy
    swordEnergy,

    // Deck state
    hand: deckState.hand,
    drawPile: deckState.drawPile,
    discardPile: deckState.discardPile,

    // Animation helpers
    isNewCard,
    getDiscardingCards,

    // Actions
    handleCardPlay,
    handleEndPhase,
    resetForNextEnemy,
    initializeBattle,

    // Deprecated: kept for backward compatibility
    startBattleRound: initializeBattle,
    onDepthChange: () => { },

    // Modal controls
    openedPileType,
    openDrawPile,
    openDiscardPile,
    closePileModal,

    // Battle result
    battleResult: actualBattleResult,
    battleStats,
  };
};
