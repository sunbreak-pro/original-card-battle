import { useState, useRef, useReducer, useEffect, useCallback, createRef, useMemo } from "react";
import type { Card, Depth } from "../../cards/type/cardType";
import type { BuffDebuffMap } from "../../cards/type/baffType";
import type { Enemy } from "../../Character/data/EnemyData";
import {
    type Player,
    Swordman_Status,
} from "../../Character/data/PlayerData";
import {
    type SwordEnergyState,
    createInitialSwordEnergy,
    addSwordEnergy,
    consumeSwordEnergy,
    consumeAllSwordEnergy,
    calculateSwordEnergyConsumeDamage,
} from "../../Character/abilities/SwordEnergy";
import {
    calculateCardEffect,
    canPlayCard,
    incrementUseCount,
} from "../../cards/state/card";
import {
    calculateEndTurnDamage,
    calculateStartTurnHealing,
    decreaseBuffDebuffDuration,
    addOrUpdateBuffDebuff,
    canAct,
    calculateEnergyModifier,
    calculateDrawModifier,
    removeDebuffs,
} from "../../cards/state/buff";
import {
    applyHeal,
} from "../../battles/logic/battle";
import {
    calculateDamage,
    applyDamageAllocation,
    type Character,
} from "../../battles/logic/damageCalculation";
import {
    // determineEnemyAction, 将来実装予定
    enemyAction,
    selectRandomEnemy,
} from "../../battles/logic/enemyAI";
import {
    calculatePlayerSpeed,
    calculateEnemySpeed,
    determineTurnOrder,
    calculateSpeedBonus,
    type SpeedBonus,
} from "./speedCalculation";
import { calculateBleedDamage } from "./bleedDamage";
import { executeEnemyActions, previewEnemyActions } from "./enemyActionExecution";
import { deckReducer } from "../decks/deckReducter";
import { createInitialDeck, drawCards } from "../../battles/decks/deck";
import { SWORDSMAN_CARDS_ARRAY } from "../../cards/data/SwordsmanCards";
import { useCardAnimation } from "../battleUI/animations/useCardAnimation";
import { useTurnTransition } from "../battleUI/animations/useTurnTransition";

export interface EnemyBattleState {
    enemy: Enemy;
    hp: number;
    ap: number;
    guard: number;
    energy: number;
    speed: number;
    buffs: BuffDebuffMap;
    turnCount: number;
    ref: React.RefObject<HTMLDivElement | null>;
}

export interface PlayerBattleState {
    player: Player;
    hp: number;
    ap: number;
    guard: number;
    energy: number;
    speed: number;
    buffs: BuffDebuffMap;
    ref: React.RefObject<HTMLDivElement | null>;
}
const INITIAL_DECK_COUNTS: Record<string, number> = {
    sw_001: 4,  // Swift Slash 
    sw_003: 3,  // Combo Strike 
    sw_007: 3,  // Slash 
    sw_013: 2,  // Sword Energy Focus 
    sw_027: 2,  // Sword Energy Release 
    sw_037: 2,  // Sword Energy Barrier 
    sw_038: 2,  // Counter Stance
    sw_014: 2,  // Meditation
};

//initialize enemy helper func
function createEnemyState(enemy: Enemy): EnemyBattleState {
    return {
        enemy,
        hp: enemy.maxHp,
        ap: enemy.maxAp,
        guard: enemy.startingGuard,
        energy: enemy.actEnergy,
        speed: enemy.speed,
        buffs: new Map(),
        turnCount: 1,
        ref: createRef<HTMLDivElement>(),
    };
}

export const useBattleLogic = (depth: Depth, initialEnemies?: Enemy[]) => {
    // --- animation hooks ---
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

    // --- Refs ---
    const playerRef = useRef<HTMLDivElement>(null);
    const drawnCardsRef = useRef<Card[]>([]);

    // --- Multiple enemies data State ---
    const [enemies, setEnemies] = useState<EnemyBattleState[]>(() => {
        if (initialEnemies && initialEnemies.length > 0) {
            return initialEnemies.map(createEnemyState);
        }
        // default enemy selection based on depth
        const { enemies: selectedEnemies } = selectRandomEnemy(depth, "normal");
        return selectedEnemies.map(createEnemyState);
    });
    // Get the current target enemy's ref (for animation)
    const getTargetEnemyRef = useCallback(() => {
        // Return the ref of the first alive enemy
        const aliveEnemy = enemies.find(e => e.hp > 0);
        return aliveEnemy?.ref.current ?? null;
    }, [enemies]);
    // TODO: Used when implementing multiple enemy targeting
    // const [_targetEnemyIndex, _setTargetEnemyIndex] = useState(0);

    // --- Derived values for backward compatibility ---
    const currentEnemy = enemies[0]?.enemy;
    const enemyHp = enemies[0]?.hp ?? 0;
    const enemyMaxHp = enemies[0]?.enemy.maxHp ?? 0;
    const enemyAp = enemies[0]?.ap ?? 0;
    const enemyMaxAp = enemies[0]?.enemy.maxAp ?? 0;
    const enemyGuard = enemies[0]?.guard ?? 0;
    const enemySpeed = enemies[0]?.enemy.speed ?? 0;
    const actEnergy = enemies[0]?.energy ?? 1;
    const enemyBuffs = useMemo(() => {
        return enemies[0]?.buffs ?? new Map();
    }, [enemies]); const enemyTurnCount = enemies[0]?.turnCount ?? 1;
    const [enemyEnergy, setEnemyEnergy] = useState(actEnergy); // Enemy's current energy (action count)
    // --- basic State ---
    const [playerName] = useState("eires"); //(maintained for future customization
    const [playerClassName] = useState(Swordman_Status.classGrade);
    const [playerHp, setPlayerHp] = useState(Swordman_Status.hp);
    const [playerMaxHp] = useState(Swordman_Status.maxHp);
    const [playerAp, setPlayerAp] = useState(Swordman_Status.ap);
    const [playerMaxAp] = useState(Swordman_Status.maxAp);
    const [playerGuard, setPlayerGuard] = useState(0);
    const [cardEnergy, setEnergy] = useState(Swordman_Status.initialEnergy);
    const [maxEnergy] = useState(3);
    const [turn, setTurn] = useState(1);
    const [turnPhase, setTurnPhase] = useState<"player" | "enemy" | "transition">("player");
    const [playerBuffs, setPlayerBuffs] = useState<BuffDebuffMap>(new Map());
    const [playerNowSpeed, setPlayerSpeed] = useState(Swordman_Status.speed);
    const [enemyNowSpeed, setEnemySpeed] = useState(enemySpeed);
    const [turnOrder, setTurnOrder] = useState<"player" | "enemy">("player");
    const [speedBonusPlayer, setSpeedBonusPlayer] = useState<SpeedBonus | null>(null);
    const [speedBonusEnemy, setSpeedBonusEnemy] = useState<SpeedBonus | null>(null);


    // Update a specific enemy's state helper function
    const updateEnemy = useCallback((index: number, updater: (state: EnemyBattleState) => Partial<EnemyBattleState>) => {
        setEnemies(previous => previous.map((e, i) => i === index ? { ...e, ...updater(e) } : e));
    }, []);

    // Update all enemies' state helper function (for area attacks)
    const updateAllEnemies = useCallback((updater: (state: EnemyBattleState) => Partial<EnemyBattleState>) => {
        setEnemies(prev => prev.map(e => ({ ...e, ...updater(e) })));
    }, []);

    // Backward compatibility setters (update the first enemy)
    const setEnemyHp = useCallback((updater: number | ((prev: number) => number)) => {
        updateEnemy(0, (e) => ({
            hp: typeof updater === 'function' ? updater(e.hp) : updater
        }));
    }, [updateEnemy]);

    const setEnemyAp = useCallback((updater: number | ((prev: number) => number)) => {
        updateEnemy(0, (e) => ({
            ap: typeof updater === 'function' ? updater(e.ap) : updater
        }));
    }, [updateEnemy]);

    const setEnemyGuard = useCallback((updater: number | ((prev: number) => number)) => {
        updateEnemy(0, (e) => ({
            guard: typeof updater === 'function' ? updater(e.guard) : updater
        }));
    }, [updateEnemy]);

    const setEnemyBuffs = useCallback((updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => {
        updateEnemy(0, (e) => ({
            buffs: typeof updater === 'function' ? updater(e.buffs) : updater
        }));
    }, [updateEnemy]);


    const setEnemyTurnCount = useCallback((updater: number | ((prev: number) => number)) => {
        updateEnemy(0, (e) => ({
            turnCount: typeof updater === 'function' ? updater(e.turnCount) : updater
        }));
    }, [updateEnemy]);
    const [swordEnergy, setSwordEnergy] = useState<SwordEnergyState>(createInitialSwordEnergy());
    const initialDeckState = (() => {
        const initialDeck = createInitialDeck(INITIAL_DECK_COUNTS, SWORDSMAN_CARDS_ARRAY);
        const { drawnCards, newDrawPile, newDiscardPile } = drawCards(5, initialDeck, []);
        return { hand: drawnCards, drawPile: newDrawPile, discardPile: newDiscardPile };
    })();
    const [deckState, dispatch] = useReducer(deckReducer, initialDeckState);
    const deckStateRef = useRef(deckState);
    useEffect(() => {
        deckStateRef.current = deckState;
    }, [deckState]);
    const [isShuffling, setIsShuffling] = useState(false);
    const [isDrawingAnimation, setIsDrawingAnimation] = useState(false);

    // --- modal manage State ---
    const [openedPileType, setOpenedPileType] = useState<"draw" | "discard" | null>(null);

    // --- battle result State ---
    const [battleResult, setBattleResult] = useState<"ongoing" | "victory" | "defeat">("ongoing");

    // --- battle statistics State ---
    const [battleStats, setBattleStats] = useState({
        damageDealt: 0,
        damageTaken: 0,
    });
    /**
     * Apply buffs/debuffs to enemy energy
     * In Ver 4.0, slow does not affect energy (only speed)
     */
    const applyEnemyEnergyModifiers = useCallback((
        baseEnergy: number,
        _buffs: BuffDebuffMap // Reserved for future expansion (currently unused)
    ): number => {
        let energy = baseEnergy;

        // Future buffs like energyRegen can be added here
        // Currently, slow does not affect energy
        // The _buffs parameter is reserved for future expansion

        return Math.max(1, energy); // Ensure at least 1 energy
    }, []);

    // --- Logic: Card Play ---
    const handleCardPlay = async (card: Card, cardElement?: HTMLElement) => {
        if (!canPlayCard(card, cardEnergy, turnPhase === "player")) return;

        setEnergy(e => e - card.cost);
        const effect = calculateCardEffect(card);

        // animations
        if (cardElement) {
            const isPlayerTarget = effect.shieldGain || effect.hpGain || effect.playerBuffs?.length;
            const target = isPlayerTarget ? playerRef.current : getTargetEnemyRef();
            if (target) await playCardWithAnimation(cardElement, target, () => { });
        }

        // === Sword Energy System Processing ===
        let swordEnergyDamageBonus = 0;
        let consumedSwordEnergy = 0;

        // In case of sword energy consumption skills
        if (card.swordEnergyConsume !== undefined) {
            if (card.swordEnergyConsume === 0) {
                // Consume all
                const result = consumeAllSwordEnergy(swordEnergy);
                consumedSwordEnergy = result.consumed;
                setSwordEnergy(result.newState);
            } else {
                // Consume specified amount
                const result = consumeSwordEnergy(swordEnergy, card.swordEnergyConsume);
                consumedSwordEnergy = result.consumed;
                setSwordEnergy(result.newState);
            }

            // Damage bonus for sword energy consumption skills
            if (card.swordEnergyMultiplier) {
                swordEnergyDamageBonus = calculateSwordEnergyConsumeDamage(
                    0, consumedSwordEnergy, card.swordEnergyMultiplier
                );
            }
        }
        else if (card.swordEnergyGain) {
            setSwordEnergy(prev => addSwordEnergy(prev, card.swordEnergyGain!));
        }

        // Apply effects: Damage
        if (effect.damageToEnemy) {
            const playerChar: Character = {
                hp: playerHp,
                maxHp: playerMaxHp,
                ap: playerAp,
                maxAp: playerMaxAp,
                guard: playerGuard,
                buffDebuffs: playerBuffs,
            };

            const enemyChar: Character = {
                hp: enemyHp,
                maxHp: enemyMaxHp,
                ap: enemyAp,
                maxAp: enemyMaxAp,
                guard: enemyGuard,
                buffDebuffs: enemyBuffs,
            };

            // Create a card with sword energy damage bonus added
            const cardWithSwordEnergy = {
                ...card,
                baseDamage: (card.baseDamage || 0) + swordEnergyDamageBonus,
            };

            const damageResult = calculateDamage(playerChar, enemyChar, cardWithSwordEnergy);
            const allocation = applyDamageAllocation(enemyChar, damageResult.finalDamage);

            // apply damage to enemy
            setEnemyGuard(g => Math.max(0, g - allocation.guardDamage));
            setEnemyAp(a => Math.max(0, a - allocation.apDamage));
            setEnemyHp(h => Math.max(0, h - allocation.hpDamage));

            const enemyTarget = getTargetEnemyRef();
            if (enemyTarget) {
                showDamageEffect(enemyTarget, damageResult.finalDamage, damageResult.isCritical);
            }

            // lifeSteal
            if (damageResult.lifestealAmount > 0) {
                const newHp = applyHeal(damageResult.lifestealAmount, playerHp, playerMaxHp);
                setPlayerHp(newHp);
                if (playerRef.current) showHealEffect(playerRef.current, damageResult.lifestealAmount);
            }

            // 棘の鎧ダメージ
            if (damageResult.reflectDamage > 0) {
                setPlayerHp(h => Math.max(0, h - damageResult.reflectDamage));
                if (playerRef.current) showDamageEffect(playerRef.current, damageResult.reflectDamage, false);
            }

            // 統計追跡
            setBattleStats(stats => ({
                ...stats,
                damageDealt: stats.damageDealt + damageResult.finalDamage,
            }));
        }
        // Apply effects: Guard
        if (effect.shieldGain) {
            setPlayerGuard(g => g + effect.shieldGain!);
            if (playerRef.current) showShieldEffect(playerRef.current, effect.shieldGain);
        }


        if (card.guardAmount && card.guardAmount > 0) {
            setPlayerGuard(g => g + card.guardAmount!);
            if (playerRef.current) showShieldEffect(playerRef.current, card.guardAmount);
        }

        if (card.cardTypeId === "sw_037") {
            const guardFromEnergy = swordEnergy.current * 8;
            setPlayerGuard(g => g + guardFromEnergy);
            if (playerRef.current) showShieldEffect(playerRef.current, guardFromEnergy);
        }

        if (card.cardTypeId === "sw_039") {
            const guardFromEnergy = swordEnergy.current * 2;
            setPlayerGuard(g => g + guardFromEnergy);
            if (playerRef.current) showShieldEffect(playerRef.current, guardFromEnergy);
        }

        if (card.cardTypeId === "sw_040") {
            const guardFromEnergy = swordEnergy.current * 2;
            setPlayerGuard(g => g + guardFromEnergy);
            if (playerRef.current) showShieldEffect(playerRef.current, guardFromEnergy);
        }


        if (card.healAmount && card.healAmount > 0) {
            const newHp = applyHeal(card.healAmount, playerHp, playerMaxHp);
            setPlayerHp(newHp);
            if (playerRef.current) showHealEffect(playerRef.current, card.healAmount);
        }

        if (card.energyGain && card.energyGain > 0) {
            setEnergy(e => Math.min(maxEnergy, e + card.energyGain!));
        }

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

        if (effect.enemyDebuffs?.length) {
            let newBuffs = enemyBuffs;
            effect.enemyDebuffs.forEach(b => {
                newBuffs = addOrUpdateBuffDebuff(newBuffs, b.name, b.duration, b.value, b.stacks, false);
            });
            setEnemyBuffs(newBuffs);
        }
        if (effect.playerBuffs?.length) {
            let newBuffs = playerBuffs;
            effect.playerBuffs.forEach(b => {
                newBuffs = addOrUpdateBuffDebuff(newBuffs, b.name, b.duration, b.value, b.stacks, false);
            });
            setPlayerBuffs(newBuffs);
        }

        const updatedCard = incrementUseCount(card);
        dispatch({ type: "CARD_PLAY", card: updatedCard });

        const bleedDamage = calculateBleedDamage(playerMaxHp, playerBuffs);
        if (bleedDamage > 0) {
            setPlayerHp(prev => Math.max(0, prev - bleedDamage));
            if (playerRef.current) {
                showDamageEffect(playerRef.current, bleedDamage, false);
            }
            await new Promise(r => setTimeout(r, 300));
        }
    };

    const startPlayerTurn = useCallback(async () => {
        const calcPlayerSpeed = calculatePlayerSpeed(playerBuffs);
        const calcEnemySpeed = currentEnemy ? calculateEnemySpeed(currentEnemy, enemyBuffs) : 0;
        setPlayerSpeed(calcPlayerSpeed);
        setEnemySpeed(calcEnemySpeed);

        const order = determineTurnOrder(calcPlayerSpeed, calcEnemySpeed);
        setTurnOrder(order);

        if (order === "player") {
            const bonus = calculateSpeedBonus(calcPlayerSpeed, calcEnemySpeed);
            setSpeedBonusPlayer(bonus);
            setSpeedBonusEnemy(null);
        } else {
            setSpeedBonusPlayer(null);
        }
        if (currentEnemy) {
            const modifiedEnergy = applyEnemyEnergyModifiers(currentEnemy.actEnergy, enemyBuffs);
            setEnemyEnergy(modifiedEnergy);
        }

        await new Promise<void>(r => showMessage("プレイヤーのターン", 2500, r));
        setPlayerGuard(0);
        setPlayerBuffs(b => decreaseBuffDebuffDuration(b));
        setEnemyBuffs(b => decreaseBuffDebuffDuration(b));

        const { hp, shield } = calculateStartTurnHealing(playerBuffs);
        if (hp > 0) {
            setPlayerHp(h => applyHeal(hp, h, playerMaxHp));
            if (playerRef.current) showHealEffect(playerRef.current, hp);
            await new Promise(r => setTimeout(r, 500));
        }
        if (shield > 0) {
            setPlayerGuard(g => g + shield);
            if (playerRef.current) showShieldEffect(playerRef.current, shield);
            await new Promise(r => setTimeout(r, 500));
        }

        if (!canAct(playerBuffs)) {
            await new Promise(r => setTimeout(r, 1500));
            setEnergy(0);
            return;
        } else {
            const energyModifier = calculateEnergyModifier(playerBuffs);
            const totalEnergy = Math.max(0, maxEnergy + energyModifier);
            setEnergy(totalEnergy);
        }
        if (playerBuffs.has("cleanse")) {
            const cleanse = playerBuffs.get("cleanse")!;
            const cleanseCount = cleanse.value * cleanse.stacks;
            setPlayerBuffs(b => removeDebuffs(b, cleanseCount));
        }

        setTurnPhase("player");
        setTurn(t => t + 1);
        const currentDeck = deckStateRef.current;

        const drawModifier = calculateDrawModifier(playerBuffs);
        const totalDrawCount = 5 + drawModifier;
        const { drawnCards, newDrawPile, newDiscardPile } = drawCards(
            totalDrawCount,
            currentDeck.drawPile,
            currentDeck.discardPile
        );
        drawnCardsRef.current = drawnCards;
        dispatch({ type: "SET_PILES", newDrawPile, newDiscardPile });

        if (newDiscardPile.length === 0 && currentDeck.discardPile.length > 0) {
            setIsShuffling(true);
        } else if (drawnCards.length > 0) {
            setIsDrawingAnimation(true);
        }
    }, [playerBuffs, playerMaxHp, showMessage, maxEnergy, showHealEffect, showShieldEffect, setEnemyBuffs, currentEnemy, enemyBuffs, applyEnemyEnergyModifiers]);

    const executeEnemyTurn = useCallback(async () => {
        setEnemyGuard(currentEnemy.startingGuard);
        const { hp, shield } = calculateStartTurnHealing(enemyBuffs);
        if (hp > 0) {
            setEnemyHp(h => applyHeal(hp, h, enemyMaxHp));
            await new Promise(r => setTimeout(r, 500));
        }
        if (shield > 0) {
            setEnemyGuard(g => g + shield);
            await new Promise(r => setTimeout(r, 500));
        }

        if (!canAct(enemyBuffs)) {
            await new Promise(r => setTimeout(r, 1500));
            setEnemyTurnCount(c => c + 1);
            startPlayerTurn();
            return;
        }

        await new Promise(r => setTimeout(r, 800));

        const checkBattleEnd = () => {
            return playerHp <= 0 || enemyHp <= 0;
        };

        const onExecuteAction = async (action: import("../../Character/data/EnemyData").EnemyAction) => {
            // If the action grants Guard, apply it and return early
            if (action.guardGain && action.guardGain > 0) {
                setEnemyGuard(g => g + action.guardGain!);
                return;
            }

            const playerChar: Character = {
                hp: playerHp,
                maxHp: playerMaxHp,
                ap: playerAp,
                maxAp: playerMaxAp,
                guard: playerGuard,
                buffDebuffs: playerBuffs,
            };

            const enemyChar: Character = {
                hp: enemyHp,
                maxHp: enemyMaxHp,
                ap: enemyAp,
                maxAp: enemyMaxAp,
                guard: enemyGuard,
                buffDebuffs: enemyBuffs,
            };

            // 敵の行動をCard形式に変換
            const enemyAttackCard = enemyAction(action);

            // ダメージ計算と適用（連続攻撃の場合は複数回）
            const hitCount = action.hitCount || 1;
            for (let i = 0; i < hitCount; i++) {
                const damageResult = calculateDamage(enemyChar, playerChar, enemyAttackCard);
                const allocation = applyDamageAllocation(playerChar, damageResult.finalDamage);

                setPlayerGuard(g => Math.max(0, g - allocation.guardDamage));
                setPlayerAp(a => Math.max(0, a - allocation.apDamage));
                setPlayerHp(h => Math.max(0, h - allocation.hpDamage));

                if (playerRef.current) {
                    showDamageEffect(playerRef.current, damageResult.finalDamage, false);
                }

                if (damageResult.reflectDamage > 0) {
                    setEnemyHp(h => Math.max(0, h - damageResult.reflectDamage));
                    const reflectTarget = getTargetEnemyRef();
                    if (reflectTarget) showDamageEffect(reflectTarget, damageResult.reflectDamage, false);
                }

                setBattleStats(stats => ({
                    ...stats,
                    damageTaken: stats.damageTaken + damageResult.finalDamage,
                }));

                if (i < hitCount - 1) {
                    await new Promise(r => setTimeout(r, 500));
                }
            }

            if (action.applyDebuffs && action.applyDebuffs.length > 0) {
                let newBuffs = playerBuffs;
                action.applyDebuffs.forEach(debuff => {
                    newBuffs = addOrUpdateBuffDebuff(
                        newBuffs,
                        debuff.name,
                        debuff.stacks,
                        debuff.duration,
                        debuff.value,
                        debuff.isPermanent,
                        debuff.source,
                    );
                });
                setPlayerBuffs(newBuffs);
            }
            const bleedDamage = calculateBleedDamage(enemyMaxHp, enemyBuffs);
            if (bleedDamage > 0) {
                setEnemyHp(h => Math.max(0, h - bleedDamage));
                const bleedTarget = getTargetEnemyRef();
                if (bleedTarget) {
                    showDamageEffect(bleedTarget, bleedDamage, false);
                }
                await new Promise(r => setTimeout(r, 300));
            }
        };
        await executeEnemyActions(
            currentEnemy,
            enemyHp,
            enemyMaxHp,
            enemyTurnCount,
            enemyEnergy,
            onExecuteAction,
            checkBattleEnd
        );

        await new Promise(r => setTimeout(r, 800));
        const dotDamage = calculateEndTurnDamage(enemyBuffs);
        if (dotDamage > 0) {
            setEnemyHp(h => Math.max(0, h - dotDamage));
            const dotTarget = getTargetEnemyRef();
            if (dotTarget) showDamageEffect(dotTarget, dotDamage, false);
            await new Promise(r => setTimeout(r, 500));
        }
        setEnemyTurnCount(c => c + 1);

        startPlayerTurn();
    }, [
        currentEnemy,
        enemyBuffs,
        enemyHp,
        enemyMaxHp,
        enemyAp,
        enemyMaxAp,
        enemyGuard,
        enemyTurnCount,
        enemyEnergy,
        playerHp,
        playerMaxHp,
        playerAp,
        playerMaxAp,
        playerGuard,
        playerBuffs,
        startPlayerTurn,
        showDamageEffect,
        setEnemyHp,
        setEnemyGuard,
        setEnemyTurnCount,
        getTargetEnemyRef,
    ]);

    const handleEndTurn = () => {
        if (turnPhase !== "player") return;
        setTurnPhase("transition");

        const dotDamage = calculateEndTurnDamage(playerBuffs);
        if (dotDamage > 0) {
            setPlayerHp(h => Math.max(0, h - dotDamage));
            if (playerRef.current) {
                showDamageEffect(playerRef.current, dotDamage, false);
            }
        }

        if (playerBuffs.has("momentum")) {
            setPlayerBuffs(buffs => {
                const newBuffs = new Map(buffs);
                const momentum = newBuffs.get("momentum")!;
                newBuffs.set("momentum", { ...momentum, stacks: momentum.stacks + 1 });
                return newBuffs;
            });
        }

        const cardsToDiscard = [...deckState.hand];
        discardCardsWithAnimation(cardsToDiscard, 250, () => {
            dispatch({ type: "END_TURN", cardsToDiscard });
            showMessage("敵のターン", 2500, executeEnemyTurn);
        });
    };

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

    const openDrawPile = () => setOpenedPileType("draw");
    const openDiscardPile = () => setOpenedPileType("discard");
    const closePileModal = () => setOpenedPileType(null);

    const resetForNextEnemy = useCallback((nextEnemy: Enemy | Enemy[]) => {
        const nextEnemies = Array.isArray(nextEnemy) ? nextEnemy : [nextEnemy];
        setEnemies(nextEnemies.map(createEnemyState));

        setPlayerGuard(0);
        setPlayerBuffs(new Map());
        setEnergy(maxEnergy);
        setSwordEnergy(createInitialSwordEnergy());

        const allCards = [
            ...deckStateRef.current.hand,
            ...deckStateRef.current.drawPile,
            ...deckStateRef.current.discardPile,
        ];
        const shuffledDeck = [...allCards].sort(() => Math.random() - 0.5);
        const newHand = shuffledDeck.slice(0, 5);
        const newDrawPile = shuffledDeck.slice(5);
        dispatch({ type: "RESET_DECK", hand: newHand, drawPile: newDrawPile, discardPile: [] });

        setTurn(1);
        setTurnPhase("player");
        setBattleStats({ damageDealt: 0, damageTaken: 0 });

        setBattleResult("ongoing");
    }, [maxEnergy]);

    // Calculated based on HP instead of useEffect (supports multiple enemies: victory if all enemies have HP <= 0)
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
        return previewEnemyActions(currentEnemy, enemyHp, turn + 1);
    }, [currentEnemy, enemyHp, turn]);

    return {
        playerRef,
        getTargetEnemyRef,
        currentEnemy, enemies, aliveEnemies,
        updateEnemy, updateAllEnemies,
        playerName, playerClassName, playerHp, playerMaxHp, playerAp, playerMaxAp, playerGuard, playerBuffs,
        enemyHp, enemyMaxHp, enemyAp, enemyMaxAp, enemyGuard, enemyBuffs,
        cardEnergy, maxEnergy, turn, turnPhase,
        turnMessage, showTurnMessage,
        playerNowSpeed, enemyNowSpeed, turnOrder, speedBonusPlayer, speedBonusEnemy,
        enemyEnergy, nextEnemyActions,
        swordEnergy,
        hand: deckState.hand, drawPile: deckState.drawPile, discardPile: deckState.discardPile,
        isNewCard, getDiscardingCards, handleCardPlay, handleEndTurn, resetForNextEnemy,
        onDepthChange: () => { },
        openedPileType, openDrawPile, openDiscardPile, closePileModal,
        battleResult: actualBattleResult,
        battleStats,
    };
};