/**
 * Battle Orchestrator Hook
 *
 * Main hook that orchestrates all battle-related hooks:
 * - useBattleState: HP/AP/Guard/Buff state management
 * - useBattlePhase: Phase queue and turn management
 * - useCardExecution: Card effect execution
 * - executeCharacterManage: Enemy/player phase execution
 * - useClassAbility: Class-specific ability management
 *
 * This hook provides the same interface as the original useBattle hook
 * for backward compatibility while using the new modular structure internally.
 */

import { useState, useRef, useReducer, useEffect, useCallback, useMemo } from "react";
import type { Card, Depth } from '@/types/cardTypes';
import type { EnemyDefinition, CharacterClass, EncounterSize } from '@/types/characterTypes';
import type { PhaseQueue, BuffDebuffMap } from '@/types/battleTypes';
import { createInitialSwordEnergy } from '../../characters/player/classAbility/classAbilityUtils';

// Deck management (IMMUTABLE ZONE - DO NOT MODIFY)
import { deckReducer } from "../../cards/decks/deckReducter";
import { createInitialDeck, shuffleArray } from "../../cards/decks/deck";
import { SWORDSMAN_CARDS_ARRAY } from "@/constants/data/cards/swordsmanCards";
import { MAGE_CARDS_ARRAY } from "@/constants/data/cards/mageCards";
import { SUMMONER_CARDS_ARRAY } from "@/constants/data/cards/summonerCards";
import { getInitialDeckCounts } from "@/constants/data/battles/initialDeckConfig";

// Card mastery management
import { applyMasteryToCards } from "../../cards/state/masteryManager";

// Animation hooks
import { useCardAnimation } from "../../../ui/html/componentsHtml/useCardAnimation";
import { useTurnTransition } from "../../../ui/animations/usePhaseTransition";

// Battle hooks
import { useBattlePhase } from "./useBattlePhase";
import { useCharacterPhaseExecution } from "./executeCharacterManage";
import { useBattleState, type InitialPlayerState } from "./useBattleState";
import { useCardExecution, type CardAnimationHandlers, type CardExecutionSetters, type DeckDispatch } from "./useCardExecution";

import { useSwordEnergy } from "./useClassAbility";
import { useElementalChain } from "./useElementalChain";


// Phase execution
import { calculatePlayerPhaseEnd } from "../execution/playerPhaseExecution";

// Enemy action preview
import { previewEnemyActions } from "../../characters/enemy/logic/enemyActionExecution";

// Enemy state helper
import { createEnemyStateFromDefinition } from "../logic/enemyStateLogic";

// Resonance effects (Mage)
import { getResonanceEffects } from "../../characters/player/logic/elementalSystem";

// Phase queue helpers (extracted for modularity)
import { expandPhaseEntriesForMultipleEnemies, buildEnemyBattleStats } from "../helpers/phaseQueueHelpers";

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Get card data array for a specific character class
 * @param classType - The character class
 * @returns Array of card definitions for that class
 */
function getCardDataByClass(classType: CharacterClass): Card[] {
  switch (classType) {
    case "swordsman":
      return SWORDSMAN_CARDS_ARRAY;
    case "mage":
      return MAGE_CARDS_ARRAY;
    case "summoner":
      return SUMMONER_CARDS_ARRAY;
    default:
      return SWORDSMAN_CARDS_ARRAY;
  }
}

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
export interface BattleOrchestratorOptions {
  /** Callback when AP absorbs damage — distributes to equipment durability */
  onApDamage?: (apDamage: number) => { currentAp: number; maxAp: number };
}

export const useBattleOrchestrator = (
  depth: Depth,
  initialEnemies?: EnemyDefinition[],
  initialPlayerState?: InitialPlayerState,
  encounterType: EncounterSize = "single",
  options?: BattleOrchestratorOptions,
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

  // Use player speed from initial state, fallback to default
  const playerSpeed = initialPlayerState?.speed ?? 50;
  const phaseState = useBattlePhase(playerSpeed);

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

  const battleState = useBattleState(depth, initialEnemies, phaseState.playerSpeed, initialPlayerState, encounterType);

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

    // Target selection
    selectedTargetIndex,
    setSelectedTargetIndex,

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

    // BattleStats objects
    playerBattleStats,
    enemyBattleStats,
  } = battleState;

  // ========================================================================
  // Class Ability Hook (Sword Energy)
  // ========================================================================

  const swordEnergyHook = useSwordEnergy();
  const { abilityState: swordEnergy, setAbilityState: setSwordEnergy } = swordEnergyHook;

  // Elemental Chain (Mage) - called unconditionally per React hooks rules
  const elementalChainHook = useElementalChain();
  const { abilityState: elementalState } = elementalChainHook;

  // ========================================================================
  // Deck State
  // ========================================================================

  const initialDeckState = useMemo(() => {
    // Use deck config from player state, falling back to default swordsman deck
    const deckCounts = initialPlayerState?.deckConfig
      ?? getInitialDeckCounts(initialPlayerState?.playerClass ?? "swordsman");

    // Get card data for the player's class
    const cardData = getCardDataByClass(initialPlayerState?.playerClass ?? "swordsman");

    let initialDeck = createInitialDeck(deckCounts, cardData);

    // Apply mastery from saved store if available
    if (initialPlayerState?.cardMasteryStore && initialPlayerState.cardMasteryStore.size > 0) {
      initialDeck = applyMasteryToCards(initialDeck, initialPlayerState.cardMasteryStore);
    }

    return { hand: [], drawPile: initialDeck, discardPile: [] };
  }, [initialPlayerState]);

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
    // Return the ref of the selected target enemy
    const selectedEnemy = battleState.selectedEnemy;
    if (selectedEnemy && selectedEnemy.hp > 0) {
      return selectedEnemy.ref.current;
    }
    // Fallback: first alive enemy
    const aliveEnemy = enemies.find((e) => e.hp > 0);
    return aliveEnemy?.ref.current ?? null;
  }, [enemies, battleState.selectedEnemy]);

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
      getElementalDamageModifier: elementalChainHook.getDamageModifier,
      getResonanceEffects: initialPlayerState?.playerClass === "mage"
        ? (card?: Card) => {
          const state = elementalChainHook.abilityState;
          if (!state.lastElement || state.resonanceLevel === 0) return null;
          if (card && !card.element.includes(state.lastElement)) return null;
          return getResonanceEffects(state.lastElement, state.resonanceLevel);
        }
        : undefined,
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
      elementalChainHook.getDamageModifier,
      elementalChainHook.abilityState,
      initialPlayerState?.playerClass,
    ]
  );

  const cardExecution = useCardExecution(
    playerBattleStats,
    enemyBattleStats,
    playerState.energy,
    playerState.maxEnergy,
    playerState.maxHp,
    playerState.buffs,
    swordEnergy,
    phaseState.isPlayerPhase,
    playerRef,
    getTargetEnemyRef,
    deckStateRef,
    cardSetters,
    animationHandlers,
    dispatch as DeckDispatch
  );

  // Note: Enemy phase execution is handled by executeCharacterManage.

  // ========================================================================
  // Card Play Handler
  // ========================================================================

  const handleCardPlay = useCallback(
    async (card: Card, cardElement?: HTMLElement) => {
      await cardExecution.executeCard(card, cardElement);
      elementalChainHook.onCardPlayed(card);
    },
    [cardExecution, elementalChainHook]
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

  /**
   * Execute enemy phase for a specific enemy index.
   * Each alive enemy gets its own turn in multi-enemy battles.
   */
  const executeEnemyPhaseForIndex = useCallback(async (enemyIndex: number) => {
    const enemy = enemies[enemyIndex];
    if (!enemy || enemy.hp <= 0) return; // Skip dead enemies

    const enemyDef = enemy.definition;
    const eStats = buildEnemyBattleStats(enemy);

    // Build index-scoped setters
    const scopedSetEnemyGuard = (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(enemyIndex, (e) => ({
        guard: typeof updater === "function" ? updater(e.guard) : updater,
      }));
    };
    const scopedSetEnemyHp = (updater: number | ((prev: number) => number)) => {
      updateEnemyByUpdater(enemyIndex, (e) => ({
        hp: typeof updater === "function" ? updater(e.hp) : updater,
      }));
    };
    const scopedSetEnemyBuffs = (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => {
      updateEnemyByUpdater(enemyIndex, (e) => ({
        buffDebuffs: typeof updater === "function" ? updater(e.buffDebuffs) : updater,
      }));
    };
    const scopedSetEnemyEnergy = (value: number) => {
      updateEnemyByUpdater(enemyIndex, () => ({ energy: value }));
    };
    const getEnemyRef = () => enemy.ref.current;

    await executeEnemyPhaseImpl({
      currentEnemy: enemyDef,
      enemyBuffs: enemy.buffDebuffs,
      enemyHp: enemy.hp,
      enemyMaxHp: enemy.maxHp,
      enemyEnergy: enemy.energy,
      playerHp: playerState.hp,
      playerBuffs: playerState.buffs,
      enemies,
      enemyIndex,
      enemyStats: eStats,
      playerStats: playerBattleStats,
      playerRef,
      setEnemyGuard: scopedSetEnemyGuard,
      setEnemyEnergy: scopedSetEnemyEnergy,
      setEnemyBuffs: scopedSetEnemyBuffs,
      setEnemyHp: scopedSetEnemyHp,
      setPlayerGuard,
      setPlayerAp,
      setPlayerHp,
      setPlayerBuffs,
      setBattleStats,
      showMessage,
      showDamageEffect,
      getTargetEnemyRef: getEnemyRef,
      onApDamage: options?.onApDamage,
      phaseState: {
        setPlayerPhaseActive: phaseState.setPlayerPhaseActive,
        setEnemyPhaseActive: phaseState.setEnemyPhaseActive,
        incrementPhaseCount: phaseState.incrementPhaseCount,
        clearActivePhase: phaseState.clearActivePhase,
      },
    });
  }, [
    executeEnemyPhaseImpl,
    enemies,
    updateEnemyByUpdater,
    playerState.hp,
    playerState.buffs,
    playerBattleStats,
    setPlayerGuard,
    setPlayerAp,
    setPlayerHp,
    setPlayerBuffs,
    showMessage,
    showDamageEffect,
    phaseState,
    options?.onApDamage,
  ]);


  // ========================================================================
  // Battle Flow Control
  // ========================================================================

  const executeNextPhaseRef = useRef<(queue: PhaseQueue, index: number) => Promise<void>>(
    async () => { }
  );

  // Ref to track latest enemies for use in async phase execution
  const enemiesRef = useRef(enemies);
  useEffect(() => {
    enemiesRef.current = enemies;
  }, [enemies]);

  // Ref to track latest phase index for use in animation callbacks
  const phaseIndexRef = useRef(phaseState.currentPhaseIndex);
  useEffect(() => {
    phaseIndexRef.current = phaseState.currentPhaseIndex;
  }, [phaseState.currentPhaseIndex]);

  const executeNextPhaseImpl = useCallback(
    async (queue: PhaseQueue, index: number) => {
      // Check battle end
      if (areAllEnemiesDead || !isPlayerAlive) {
        return;
      }

      // Use expanded entries for multi-enemy support (use ref to avoid stale closure)
      const expandedEntries = expandPhaseEntriesForMultipleEnemies(queue, enemiesRef.current);

      if (index >= expandedEntries.length) {
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

      const currentEntry = expandedEntries[index];

      if (currentEntry.actor === "player") {
        await executePlayerPhase();
        // Player phase waits for handleEndPhase to advance
      } else {
        const enemyIdx = currentEntry.enemyIndex ?? 0;
        // Skip dead enemies (use ref to avoid stale closure)
        if (enemiesRef.current[enemyIdx] && enemiesRef.current[enemyIdx].hp > 0) {
          await executeEnemyPhaseForIndex(enemyIdx);
        }
        // Enemy phase auto-advances
        await executeNextPhaseRef.current(queue, index + 1);
      }
    },
    [
      areAllEnemiesDead,
      isPlayerAlive,
      executePlayerPhase,
      executeEnemyPhaseForIndex,
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

  const battleInitializedRef = useRef(false);

  const initializeBattle = useCallback(async () => {
    if (battleInitializedRef.current) return;
    battleInitializedRef.current = true;

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

    // Reset elemental chain at end of player phase
    elementalChainHook.onTurnEnd();

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
    const nextPhaseIndex = phaseIndexRef.current + 1;
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
    elementalChainHook,
  ]);

  // ========================================================================
  // Auto-start Battle
  // ========================================================================

  useEffect(() => {
    if (currentEnemy) {
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
      elementalChainHook.resetAbility();

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
      elementalChainHook,
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

  const expandedPhaseEntries = useMemo(() => {
    if (!phaseState.phaseQueue) return [];
    return expandPhaseEntriesForMultipleEnemies(phaseState.phaseQueue, enemies);
  }, [phaseState.phaseQueue, enemies]);

  const nextEnemyActions = useMemo(() => {
    if (!currentEnemy || enemyHp <= 0) {
      return [];
    }
    return previewEnemyActions(currentEnemy, enemyHp, enemyMaxHp, phaseState.phaseCount + 1);
  }, [currentEnemy, enemyHp, enemyMaxHp, phaseState.phaseCount]);

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

    // Target selection
    selectedTargetIndex,
    setSelectedTargetIndex,

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
    maxEnergy: playerState.maxEnergy,
    phaseCount: phaseState.phaseCount,
    isPlayerPhase: phaseState.isPlayerPhase,
    turnMessage,
    showTurnMessage,
    playerNowSpeed: phaseState.playerSpeed,
    enemyNowSpeed: phaseState.enemySpeed,
    phaseQueue: phaseState.phaseQueue,
    currentPhaseIndex: phaseState.currentPhaseIndex,
    expandedPhaseEntries,
    enemyEnergy,
    nextEnemyActions,

    // Sword Energy
    swordEnergy,

    // Elemental Chain (Mage)
    elementalState,

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

    // Modal controls
    openedPileType,
    openDrawPile,
    openDiscardPile,
    closePileModal,

    // Battle result
    battleResult: actualBattleResult,
    battleStats,

    // State setters (for item effects, etc.)
    setPlayerBuffs,
  };
};
