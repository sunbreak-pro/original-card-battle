/**
 * Battle Flow Management Hook
 * Main hook that orchestrates battle state and flow.
 * Uses separated modules for phase management, deck management, and execution logic.
 */
import { useState, useRef, useReducer, useEffect, useCallback, useMemo } from "react";
import type { Card, Depth } from "../../cards/type/cardType";
import type { BuffDebuffMap } from "../type/baffType";
import type { Enemy } from "../../characters/type/enemyType";
import type { EnemyBattleState } from "../type/battleStateType";
import type { Player } from "../../characters/type/playerTypes";
import type { PhaseQueue } from "../type/phaseType";
import { Swordman_Status } from "../../characters/player/data/PlayerData";
import {
    type SwordEnergyState,
    createInitialSwordEnergy,
    addSwordEnergy,
    consumeSwordEnergy,
    consumeAllSwordEnergy,
    calculateSwordEnergyConsumeDamage,
} from "../../characters/player/logic/swordEnergySystem";

// Card logic
import { calculateCardEffect, canPlayCard, incrementUseCount } from "../../cards/state/card";

// Buff/Debuff logic
import { addOrUpdateBuffDebuff } from "../logic/buffLogic";
import { applyHeal } from "../logic/battleLogic";

// Damage calculation
import { calculateDamage, applyDamageAllocation } from "../caluculaters/damageCalculation";

// Enemy AI
import { selectRandomEnemy } from "../../characters/enemy/logic/enemyAI";
import { previewEnemyActions } from "../../characters/enemy/logic/enemyActionExecution";

// Bleed damage
import { calculateBleedDamage } from "../logic/bleedDamage";

// Deck management (IMMUTABLE ZONE)
import { deckReducer } from "../../cards/decks/deckReducter";
import { createInitialDeck, drawCards, shuffleArray } from "../../cards/decks/deck";
import { SWORDSMAN_CARDS_ARRAY } from "../../cards/data/SwordmanCards";
import { INITIAL_DECK_COUNTS } from "../data/initialDeckConfig";

// Animation hooks
import { useCardAnimation } from "../../../ui/commonUI/useCardAnimation";
import { useTurnTransition } from "../../../ui/commonUI/animations/usePhaseTransition";

import { useBattlePhase } from "./useBattlePhase";
import { useCharacterPhaseExecution } from "./executeCharacterManage";

// Execution logic
import {
    calculatePlayerPhaseEnd,
} from "../execution/playerPhaseExecution";

// Enemy state
import { createEnemyState } from "../logic/enemyStateLogic";

// Re-export for backward compatibility
export { createEnemyState } from "../logic/enemyStateLogic";

// ============================================================================
// Main Hook
// ============================================================================

export const useBattle = (depth: Depth, initialEnemies?: Enemy[]) => {
    // --- Animation hooks ---
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

    // --- Phase management hook ---
    const phaseState = useBattlePhase(Swordman_Status.speed);

    // --- Character phase execution hook ---
    const { executePlayerPhase: executePlayerPhaseImpl, executeEnemyPhase: executeEnemyPhaseImpl } = useCharacterPhaseExecution();

    // --- Refs ---
    const playerRef = useRef<HTMLDivElement>(null);
    const drawnCardsRef = useRef<Card[]>([]);

    // --- Enemy state ---
    const [enemies, setEnemies] = useState<EnemyBattleState[]>(() => {
        if (initialEnemies && initialEnemies.length > 0) {
            return initialEnemies.map(createEnemyState);
        }
        const { enemies: selectedEnemies } = selectRandomEnemy(depth, "normal");
        return selectedEnemies.map(createEnemyState);
    });

    const getTargetEnemyRef = useCallback(() => {
        const aliveEnemy = enemies.find(e => e.hp > 0);
        return aliveEnemy?.ref.current ?? null;
    }, [enemies]);

    // --- Derived enemy values ---
    const currentEnemy = enemies[0]?.enemy;
    const enemyHp = enemies[0]?.hp ?? 0;
    const enemyMaxHp = enemies[0]?.enemy.maxHp ?? 0;
    const enemyAp = enemies[0]?.ap ?? 0;
    const enemyMaxAp = enemies[0]?.enemy.maxAp ?? 0;
    const enemyGuard = enemies[0]?.guard ?? 0;
    const actEnergy = enemies[0]?.energy ?? 1;
    const enemyBuffs = useMemo(() => enemies[0]?.buffs ?? new Map(), [enemies]);

    const [enemyEnergy, setEnemyEnergy] = useState(actEnergy);

    // --- Player state ---
    const [playerClass] = useState<Player["playerClass"]>(Swordman_Status.playerClass);
    const [playerName] = useState<Player["name"]>("エイレス");
    const [classGrade] = useState<Player["classGrade"]>(Swordman_Status.classGrade);
    const [playerHp, setPlayerHp] = useState(Swordman_Status.hp);
    const [playerMaxHp] = useState(Swordman_Status.maxHp);
    const [playerAp, setPlayerAp] = useState(Swordman_Status.ap);
    const [playerMaxAp] = useState(Swordman_Status.maxAp);
    const [playerGuard, setPlayerGuard] = useState(0);
    const [playerEnergy, setPlayerEnergy] = useState(Swordman_Status.cardActEnergy);
    const [maxEnergy] = useState(Swordman_Status.cardActEnergy);
    const [playerBuffs, setPlayerBuffs] = useState<BuffDebuffMap>(new Map());

    // --- Sword Energy state ---
    const [swordEnergy, setSwordEnergy] = useState<SwordEnergyState>(createInitialSwordEnergy());

    // --- Deck state ---
    // Initial deck with empty hand - cards are drawn in first player phase
    const initialDeckState = (() => {
        const initialDeck = createInitialDeck(INITIAL_DECK_COUNTS, SWORDSMAN_CARDS_ARRAY);
        return { hand: [], drawPile: initialDeck, discardPile: [] };
    })();
    const [deckState, dispatch] = useReducer(deckReducer, initialDeckState);
    const deckStateRef = useRef(deckState);
    useEffect(() => {
        deckStateRef.current = deckState;
    }, [deckState]);
    const [isShuffling, setIsShuffling] = useState(false);
    const [isDrawingAnimation, setIsDrawingAnimation] = useState(false);

    // --- Modal state ---
    const [openedPileType, setOpenedPileType] = useState<"draw" | "discard" | null>(null);

    // --- Battle result state ---
    const [battleResult, setBattleResult] = useState<"ongoing" | "victory" | "defeat">("ongoing");

    // --- Battle statistics ---
    const [battleStats, setBattleStats] = useState({
        damageDealt: 0,
        damageTaken: 0,
    });

    // --- Enemy update helpers ---
    const updateEnemy = useCallback((index: number, updater: (state: EnemyBattleState) => Partial<EnemyBattleState>) => {
        setEnemies(prev => prev.map((e, i) => i === index ? { ...e, ...updater(e) } : e));
    }, []);

    const updateAllEnemies = useCallback((updater: (state: EnemyBattleState) => Partial<EnemyBattleState>) => {
        setEnemies(prev => prev.map(e => ({ ...e, ...updater(e) })));
    }, []);

    // Backward compatibility setters
    const setEnemyHp = useCallback((updater: number | ((prev: number) => number)) => {
        updateEnemy(0, (e) => ({ hp: typeof updater === 'function' ? updater(e.hp) : updater }));
    }, [updateEnemy]);

    const setEnemyAp = useCallback((updater: number | ((prev: number) => number)) => {
        updateEnemy(0, (e) => ({ ap: typeof updater === 'function' ? updater(e.ap) : updater }));
    }, [updateEnemy]);

    const setEnemyGuard = useCallback((updater: number | ((prev: number) => number)) => {
        updateEnemy(0, (e) => ({ guard: typeof updater === 'function' ? updater(e.guard) : updater }));
    }, [updateEnemy]);

    const setEnemyBuffs = useCallback((updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => {
        updateEnemy(0, (e) => ({ buffs: typeof updater === 'function' ? updater(e.buffs) : updater }));
    }, [updateEnemy]);

    // --- Character objects for calculations (memoized to prevent unnecessary re-renders) ---
    const playerChar = useMemo<Player>(() => ({
        playerClass,
        classGrade,
        name: playerName,
        hp: playerHp,
        maxHp: playerMaxHp,
        ap: playerAp,
        maxAp: playerMaxAp,
        guard: playerGuard,
        buffDebuffs: playerBuffs,
        level: 1,
        speed: phaseState.playerSpeed,
        cardActEnergy: 3,
        gold: 0,
        deck: SWORDSMAN_CARDS_ARRAY,
    }), [playerClass, classGrade, playerName, playerHp, playerMaxHp, playerAp, playerMaxAp, playerGuard, playerBuffs, phaseState.playerSpeed]);

    const enemyChar = useMemo<Enemy>(() => currentEnemy ? {
        id: currentEnemy.id,
        name: currentEnemy.name,
        description: currentEnemy.description,
        speed: currentEnemy.speed,
        actEnergy: currentEnemy.actEnergy,
        startingGuard: currentEnemy.startingGuard,
        aiPatterns: currentEnemy.aiPatterns,
        hp: enemyHp,
        maxHp: enemyMaxHp,
        ap: enemyAp,
        maxAp: enemyMaxAp,
        guard: enemyGuard,
        buffDebuffs: enemyBuffs,
    } : {} as Enemy, [currentEnemy, enemyHp, enemyMaxHp, enemyAp, enemyMaxAp, enemyGuard, enemyBuffs]);

    // ========================================================================
    // Card Play Logic
    // ========================================================================

    const handleCardPlay = async (card: Card, cardElement?: HTMLElement) => {
        if (!canPlayCard(card, playerEnergy, phaseState.isPlayerPhase)) return;

        setPlayerEnergy(e => e - card.cost);
        const effect = calculateCardEffect(card);

        // Animation
        if (cardElement) {
            const isPlayerTarget = effect.shieldGain || effect.hpGain || effect.playerBuffs?.length;
            const target = isPlayerTarget ? playerRef.current : getTargetEnemyRef();
            if (target) await playCardWithAnimation(cardElement, target, () => { });
        }

        // Sword Energy processing
        let swordEnergyDamageBonus = 0;
        let consumedSwordEnergy = 0;

        if (card.swordEnergyConsume !== undefined) {
            if (card.swordEnergyConsume === 0) {
                const result = consumeAllSwordEnergy(swordEnergy);
                consumedSwordEnergy = result.consumed;
                setSwordEnergy(result.newState);
            } else {
                const result = consumeSwordEnergy(swordEnergy, card.swordEnergyConsume);
                consumedSwordEnergy = result.consumed;
                setSwordEnergy(result.newState);
            }

            if (card.swordEnergyMultiplier) {
                swordEnergyDamageBonus = calculateSwordEnergyConsumeDamage(
                    0, consumedSwordEnergy, card.swordEnergyMultiplier
                );
            }
        } else if (card.swordEnergyGain) {
            setSwordEnergy(prev => addSwordEnergy(prev, card.swordEnergyGain!));
        }

        // Apply damage
        if (effect.damageToEnemy) {
            const cardWithSwordEnergy = {
                ...card,
                baseDamage: (card.baseDamage || 0) + swordEnergyDamageBonus,
            };

            const damageResult = calculateDamage(playerChar, enemyChar, cardWithSwordEnergy);
            const allocation = applyDamageAllocation(enemyChar, damageResult.finalDamage);

            setEnemyGuard(g => Math.max(0, g - allocation.guardDamage));
            setEnemyAp(a => Math.max(0, a - allocation.apDamage));
            setEnemyHp(h => Math.max(0, h - allocation.hpDamage));

            const enemyTarget = getTargetEnemyRef();
            if (enemyTarget) {
                showDamageEffect(enemyTarget, damageResult.finalDamage, damageResult.isCritical);
            }

            if (damageResult.lifestealAmount > 0) {
                const newHp = applyHeal(damageResult.lifestealAmount, playerHp, playerMaxHp);
                setPlayerHp(newHp);
                if (playerRef.current) showHealEffect(playerRef.current, damageResult.lifestealAmount);
            }

            if (damageResult.reflectDamage > 0) {
                setPlayerHp(h => Math.max(0, h - damageResult.reflectDamage));
                if (playerRef.current) showDamageEffect(playerRef.current, damageResult.reflectDamage, false);
            }

            setBattleStats(stats => ({
                ...stats,
                damageDealt: stats.damageDealt + damageResult.finalDamage,
            }));
        }

        // Apply guard
        if (effect.shieldGain) {
            setPlayerGuard(g => g + effect.shieldGain!);
            if (playerRef.current) showShieldEffect(playerRef.current, effect.shieldGain);
        }

        if (card.guardAmount && card.guardAmount > 0) {
            setPlayerGuard(g => g + card.guardAmount!);
            if (playerRef.current) showShieldEffect(playerRef.current, card.guardAmount);
        }

        // Special card handling for sword energy guard
        if (card.cardTypeId === "sw_037") {
            const guardFromEnergy = swordEnergy.current * 8;
            setPlayerGuard(g => g + guardFromEnergy);
            if (playerRef.current) showShieldEffect(playerRef.current, guardFromEnergy);
        }
        if (card.cardTypeId === "sw_039" || card.cardTypeId === "sw_040") {
            const guardFromEnergy = swordEnergy.current * 2;
            setPlayerGuard(g => g + guardFromEnergy);
            if (playerRef.current) showShieldEffect(playerRef.current, guardFromEnergy);
        }

        // Apply heal
        if (card.healAmount && card.healAmount > 0) {
            const newHp = applyHeal(card.healAmount, playerHp, playerMaxHp);
            setPlayerHp(newHp);
            if (playerRef.current) showHealEffect(playerRef.current, card.healAmount);
        }

        // Apply energy gain
        if (card.energyGain && card.energyGain > 0) {
            setPlayerEnergy(e => Math.min(maxEnergy, e + card.energyGain!));
        }

        // Draw cards effect
        if (card.drawCards && card.drawCards > 0) {
            const currentDeck = deckStateRef.current;
            const { drawnCards: newCards, newDrawPile, newDiscardPile } = drawCards(
                card.drawCards,
                currentDeck.drawPile,
                currentDeck.discardPile
            );
            if (newCards.length > 0) {
                dispatch({ type: "SET_PILES", newDrawPile, newDiscardPile });
                drawCardsWithAnimation(newCards, (cards) => {
                    dispatch({ type: "ADD_TO_HAND", cards });
                }, 150);
            }
        }

        if (effect.hpGain) {
            const newHp = applyHeal(effect.hpGain, playerHp, playerMaxHp);
            setPlayerHp(newHp);
            if (playerRef.current) showHealEffect(playerRef.current, effect.hpGain);
        }

        // Apply debuffs to enemy
        if (effect.enemyDebuffs?.length) {
            let newBuffs = enemyBuffs;
            effect.enemyDebuffs.forEach(b => {
                newBuffs = addOrUpdateBuffDebuff(newBuffs, b.name, b.duration, b.value, b.stacks, false);
            });
            setEnemyBuffs(newBuffs);
        }

        // Apply buffs to player
        if (effect.playerBuffs?.length) {
            let newBuffs = playerBuffs;
            effect.playerBuffs.forEach(b => {
                newBuffs = addOrUpdateBuffDebuff(newBuffs, b.name, b.duration, b.value, b.stacks, false);
            });
            setPlayerBuffs(newBuffs);
        }

        // Update card and discard
        const updatedCard = incrementUseCount(card);
        dispatch({ type: "CARD_PLAY", card: updatedCard });

        // Bleed damage
        const bleedDamage = calculateBleedDamage(playerMaxHp, playerBuffs);
        if (bleedDamage > 0) {
            setPlayerHp(prev => Math.max(0, prev - bleedDamage));
            if (playerRef.current) {
                showDamageEffect(playerRef.current, bleedDamage, false);
            }
            await new Promise(r => setTimeout(r, 300));
        }
    };

    // ========================================================================
    // Phase Execution (using executeCharacterManage hook)
    // ========================================================================

    /**
     * Execute player phase via context
     */
    const executePlayerPhase = useCallback(async () => {
        await executePlayerPhaseImpl({
            playerBuffs,
            playerMaxHp,
            maxEnergy,
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
        playerBuffs,
        playerMaxHp,
        maxEnergy,
        showMessage,
        showHealEffect,
        showShieldEffect,
        phaseState,
    ]);

    /**
     * Execute enemy phase via context
     */
    const executeEnemyPhase = useCallback(async () => {
        await executeEnemyPhaseImpl({
            currentEnemy,
            enemyBuffs,
            enemyHp,
            enemyMaxHp,
            enemyEnergy,
            playerHp,
            playerBuffs,
            enemies,
            enemyChar,
            playerChar,
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
        playerHp,
        playerBuffs,
        enemies,
        enemyChar,
        playerChar,
        showMessage,
        showDamageEffect,
        getTargetEnemyRef,
        phaseState,
        setEnemyGuard,
        setEnemyHp,
        setEnemyBuffs,
    ]);

    // ========================================================================
    // Battle Flow Control
    // ========================================================================

    // Use ref to allow recursive calls and avoid circular dependency
    const executeNextPhaseRef = useRef<(queue: PhaseQueue, index: number) => Promise<void>>(
        async () => { /* placeholder */ }
    );

    /**
     * Execute the next phase in the queue (implementation)
     */
    const executeNextPhaseImpl = useCallback(async (queue: PhaseQueue, index: number) => {
        // Check battle end
        const allEnemiesDead = enemies.every(e => e.hp <= 0);
        if (allEnemiesDead || playerHp <= 0) {
            return;
        }

        if (index >= queue.phases.length) {
            // All phases complete - generate new queue and start new round
            const newQueue = phaseState.generatePhaseQueueFromSpeeds(
                playerBuffs,
                currentEnemy,
                enemyBuffs
            );
            // Start first phase of new round via ref
            await executeNextPhaseRef.current(newQueue, 0);
            return;
        }

        const currentActor = queue.phases[index];

        if (currentActor === "player") {
            await executePlayerPhase();
            // Player phase waits for handleEndPhase to advance
        } else {
            await executeEnemyPhase();
            // Enemy phase auto-advances via ref
            await executeNextPhaseRef.current(queue, index + 1);
        }
    }, [enemies, playerHp, executePlayerPhase, executeEnemyPhase, phaseState, playerBuffs, currentEnemy, enemyBuffs]);

    // Keep ref in sync with latest implementation
    useEffect(() => {
        executeNextPhaseRef.current = executeNextPhaseImpl;
    }, [executeNextPhaseImpl]);

    /**
     * Execute the next phase (public API using ref)
     */
    const executeNextPhase = useCallback(async (queue: PhaseQueue, index: number) => {
        await executeNextPhaseRef.current(queue, index);
    }, []);

    /**
     * Initialize battle and start first phase
     */
    const initializeBattle = useCallback(async () => {
        // Generate phase queue
        const queue = phaseState.generatePhaseQueueFromSpeeds(
            playerBuffs,
            currentEnemy,
            enemyBuffs
        );

        // Start first phase
        await executeNextPhase(queue, 0);
    }, [playerBuffs, currentEnemy, enemyBuffs, phaseState, executeNextPhase]);

    /**
     * Handle end of player phase - advances to next phase in queue
     */
    const handleEndPhase = useCallback(() => {
        if (!phaseState.isPlayerPhase) return;

        // Clear phase state
        phaseState.clearActivePhase();

        // Calculate phase end effects
        const phaseEndResult = calculatePlayerPhaseEnd({ playerBuffs });

        // Apply DoT damage
        if (phaseEndResult.dotDamage > 0) {
            setPlayerHp(h => Math.max(0, h - phaseEndResult.dotDamage));
            if (playerRef.current) {
                showDamageEffect(playerRef.current, phaseEndResult.dotDamage, false);
            }
        }

        // Update buffs (momentum stacking)
        setPlayerBuffs(phaseEndResult.newBuffs);

        // Discard hand and advance to next phase
        const cardsToDiscard = [...deckState.hand];
        discardCardsWithAnimation(cardsToDiscard, 250, () => {
            dispatch({ type: "END_TURN", cardsToDiscard });

            // Advance to next phase in queue
            if (phaseState.phaseQueue) {
                phaseState.advancePhaseIndex();
                executeNextPhase(phaseState.phaseQueue, phaseState.currentPhaseIndex + 1);
            }
        });
    }, [phaseState, playerBuffs, showDamageEffect, deckState.hand, discardCardsWithAnimation, executeNextPhase]);

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
            drawCardsWithAnimation(drawnCardsRef.current, (cards) => {
                dispatch({ type: "ADD_TO_HAND", cards });
                drawnCardsRef.current = [];
                setIsDrawingAnimation(false);
            }, 250);
        }
    }, [isDrawingAnimation, drawCardsWithAnimation]);

    // ========================================================================
    // Modal Controls
    // ========================================================================

    const openDrawPile = () => setOpenedPileType("draw");
    const openDiscardPile = () => setOpenedPileType("discard");
    const closePileModal = () => setOpenedPileType(null);

    // ========================================================================
    // Reset for Next Enemy
    // ========================================================================

    const resetForNextEnemy = useCallback((nextEnemy: Enemy | Enemy[]) => {
        const nextEnemies = Array.isArray(nextEnemy) ? nextEnemy : [nextEnemy];
        setEnemies(nextEnemies.map(createEnemyState));

        setPlayerGuard(0);
        setPlayerBuffs(new Map());
        setPlayerEnergy(maxEnergy);
        setSwordEnergy(createInitialSwordEnergy());

        // Collect all cards and shuffle - hand will be drawn in first player phase
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
    }, [maxEnergy, phaseState]);

    // ========================================================================
    // Battle Result
    // ========================================================================

    const actualBattleResult = (() => {
        if (battleResult !== "ongoing") return battleResult;
        const allEnemiesDead = enemies.every(e => e.hp <= 0);
        if (allEnemiesDead) return "victory";
        if (playerHp <= 0) return "defeat";
        return "ongoing";
    })();

    const aliveEnemies = enemies.filter(e => e.hp > 0);

    const nextEnemyActions = useMemo(() => {
        if (!currentEnemy || enemyHp <= 0) {
            return [];
        }
        return previewEnemyActions(currentEnemy, enemyHp, phaseState.phaseCount + 1);
    }, [currentEnemy, enemyHp, phaseState.phaseCount]);

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
        playerName,
        playerClass,
        playerHp,
        playerMaxHp,
        playerAp,
        playerMaxAp,
        playerGuard,
        playerBuffs,

        // Enemy derived state
        enemyHp,
        enemyMaxHp,
        enemyAp,
        enemyMaxAp,
        enemyGuard,
        enemyBuffs,

        // Phase state
        playerEnergy,
        cardEnergy: playerEnergy, // Alias for backward compatibility
        maxEnergy,
        phaseCount: phaseState.phaseCount,
        isPlayerPhase: phaseState.isPlayerPhase,
        turnMessage,
        showTurnMessage,
        playerNowSpeed: phaseState.playerSpeed,
        enemyNowSpeed: phaseState.enemySpeed,
        phaseQueue: phaseState.phaseQueue,
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
