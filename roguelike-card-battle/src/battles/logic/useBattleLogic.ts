import { useState, useRef, useReducer, useEffect, useCallback } from "react";
import type { Card, Depth } from "../../cards/type/cardType";
import type { BuffDebuffMap } from "../../cards/type/baffType";
import type { Enemy } from "../../Character/data/EnemyData";
import {
    type SwordEnergyState,
    createInitialSwordEnergy,
    addSwordEnergy,
    consumeSwordEnergy,
    consumeAllSwordEnergy,
    getSwordEnergyEffects,
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
    determineEnemyAction,
    enemyActionToCard,
    selectRandomEnemy,
} from "../../battles/logic/enemyAI";
import { deckReducer } from "../decks/deckReducter";
import { createInitialDeck, drawCards } from "../../battles/decks/deck";
import { SWORDSMAN_CARDS_ARRAY } from "../../cards/data/SwordsmanCards";
import { useCardAnimation } from "../battleUI/animations/useCardAnimation";
import { useTurnTransition } from "../battleUI/animations/useTurnTransition";

// 初期デッキ設定（剣士用カード）
const INITIAL_DECK_COUNTS: Record<string, number> = {
    sw_001: 4,  // Swift Slash (0コスト、剣気+1)
    sw_003: 3,  // Combo Strike (1コスト、3連撃、剣気+1)
    sw_007: 3,  // Slash (1コスト、基本攻撃、剣気+1)
    sw_013: 2,  // Sword Energy Focus (剣気+4)
    sw_027: 2,  // Sword Energy Release (剣気消費技)
    sw_037: 2,  // Sword Energy Barrier (防御)
    sw_038: 2,  // Counter Stance (Guard+剣気)
    sw_014: 2,  // Meditation (剣気+HP回復)
};

export const useBattleLogic = (depth: Depth, initialEnemy?: Enemy) => {
    // --- アニメーションフック ---
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
    const enemyRef = useRef<HTMLDivElement>(null);
    const drawnCardsRef = useRef<Card[]>([]);

    // --- 敵データ State ---
    const [currentEnemy, setCurrentEnemy] = useState<Enemy>(() => {
        if (initialEnemy) {
            return initialEnemy;
        }
        // デフォルトで通常敵をランダム選択
        const { enemies } = selectRandomEnemy(depth, "normal");
        return enemies[0];
    });
    const [enemyTurnCount, setEnemyTurnCount] = useState(1);

    // --- 基本ステータス State ---
    const [playerHp, setPlayerHp] = useState(80);
    const [playerMaxHp] = useState(100);
    const [playerAp, setPlayerAp] = useState(30); // AP（装備耐久値）
    const [playerMaxAp] = useState(30);
    const [playerGuard, setPlayerGuard] = useState(0); // Guard（一時的な防御）
    const [enemyHp, setEnemyHp] = useState(currentEnemy.maxHp);
    const [enemyMaxHp, setEnemyMaxHp] = useState(currentEnemy.maxHp);
    const [enemyAp, setEnemyAp] = useState(currentEnemy.maxAp); // 敵のAP
    const [enemyMaxAp, setEnemyMaxAp] = useState(currentEnemy.maxAp);
    const [enemyGuard, setEnemyGuard] = useState(currentEnemy.startingGuard); // 敵のGuard
    const [energy, setEnergy] = useState(3);
    const [maxEnergy] = useState(3);
    const [turn, setTurn] = useState(1);
    const [turnPhase, setTurnPhase] = useState<"player" | "enemy" | "transition">("player");

    // --- バフ State ---
    const [playerBuffs, setPlayerBuffs] = useState<BuffDebuffMap>(new Map());
    const [enemyBuffs, setEnemyBuffs] = useState<BuffDebuffMap>(new Map());

    // --- 剣気システム State ---
    const [swordEnergy, setSwordEnergy] = useState<SwordEnergyState>(createInitialSwordEnergy());

    // --- デッキ State (Reducer) ---
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
    // --- アニメーショントリガー State ---
    const [isShuffling, setIsShuffling] = useState(false);
    const [isDrawingAnimation, setIsDrawingAnimation] = useState(false);

    // --- モーダル管理 State ---
    const [openedPileType, setOpenedPileType] = useState<"draw" | "discard" | null>(null);

    // --- 勝敗判定 State ---
    const [battleResult, setBattleResult] = useState<"ongoing" | "victory" | "defeat">("ongoing");

    // --- 戦闘統計 State ---
    const [battleStats, setBattleStats] = useState({
        damageDealt: 0,
        damageTaken: 0,
    });

    // --- ロジック: カードプレイ ---
    const handleCardPlay = async (card: Card, cardElement?: HTMLElement) => {
        if (!canPlayCard(card, energy, turnPhase === "player")) return;

        setEnergy(e => e - card.cost);
        const effect = calculateCardEffect(card);

        // アニメーション
        if (cardElement) {
            const isPlayerTarget = effect.shieldGain || effect.hpGain || effect.playerBuffs?.length;
            const target = isPlayerTarget ? playerRef.current : enemyRef.current;
            if (target) await playCardWithAnimation(cardElement, target, () => { });
        }

        // === 剣気システム処理 ===
        let swordEnergyDamageBonus = 0;
        let consumedSwordEnergy = 0;

        // 剣気消費技の場合
        if (card.swordEnergyConsume !== undefined) {
            if (card.swordEnergyConsume === 0) {
                // 全消費
                const result = consumeAllSwordEnergy(swordEnergy);
                consumedSwordEnergy = result.consumed;
                setSwordEnergy(result.newState);
            } else {
                // 指定量消費
                const result = consumeSwordEnergy(swordEnergy, card.swordEnergyConsume);
                consumedSwordEnergy = result.consumed;
                setSwordEnergy(result.newState);
            }

            // 剣気消費技のダメージボーナス
            if (card.swordEnergyMultiplier) {
                swordEnergyDamageBonus = calculateSwordEnergyConsumeDamage(
                    0, consumedSwordEnergy, card.swordEnergyMultiplier
                );
            }
        }
        // 剣気蓄積の場合
        else if (card.swordEnergyGain) {
            setSwordEnergy(prev => addSwordEnergy(prev, card.swordEnergyGain!));
        }

        // 剣気によるダメージボーナス（物理攻撃時）
        if (card.category === "physical" && card.swordEnergyConsume === undefined) {
            const effects = getSwordEnergyEffects(swordEnergy.current);
            swordEnergyDamageBonus = effects.damageBonus;
        }

        // 効果適用: ダメージ（新しいダメージ計算システムを使用）
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

            // 剣気ダメージボーナスを追加したカードを作成
            const cardWithSwordEnergy = {
                ...card,
                baseDamage: (card.baseDamage || 0) + swordEnergyDamageBonus,
            };

            const damageResult = calculateDamage(playerChar, enemyChar, cardWithSwordEnergy);
            const allocation = applyDamageAllocation(enemyChar, damageResult.finalDamage);

            // ダメージ適用
            setEnemyGuard(g => Math.max(0, g - allocation.guardDamage));
            setEnemyAp(a => Math.max(0, a - allocation.apDamage));
            setEnemyHp(h => Math.max(0, h - allocation.hpDamage));

            if (enemyRef.current) {
                showDamageEffect(enemyRef.current, damageResult.finalDamage, damageResult.isCritical);
            }

            // 吸血回復
            if (damageResult.lifestealAmount > 0) {
                const newHp = applyHeal(damageResult.lifestealAmount, playerHp, playerMaxHp);
                setPlayerHp(newHp);
                if (playerRef.current) showHealEffect(playerRef.current, damageResult.lifestealAmount);
            }

            // 棘の鎧ダメージ
            if (damageResult.thornsDamage > 0) {
                setPlayerHp(h => Math.max(0, h - damageResult.thornsDamage));
                if (playerRef.current) showDamageEffect(playerRef.current, damageResult.thornsDamage, false);
            }

            // 統計追跡
            setBattleStats(stats => ({
                ...stats,
                damageDealt: stats.damageDealt + damageResult.finalDamage,
            }));
        }
        // 効果適用: Guard（旧Shieldから変更）
        if (effect.shieldGain) {
            setPlayerGuard(g => g + effect.shieldGain!);
            if (playerRef.current) showShieldEffect(playerRef.current, effect.shieldGain);
        }
        // 効果適用: 回復
        if (effect.hpGain) {
            const newHp = applyHeal(effect.hpGain, playerHp, playerMaxHp);
            setPlayerHp(newHp);
            if (playerRef.current) showHealEffect(playerRef.current, effect.hpGain);
        }
        // 効果適用: バフ
        if (effect.enemyDebuffs?.length) {
            let newBuffs = enemyBuffs;
            effect.enemyDebuffs.forEach(b => {
                newBuffs = addOrUpdateBuffDebuff(newBuffs, b.type, b.stacks, b.duration, b.value, false);
            });
            setEnemyBuffs(newBuffs);
        }
        if (effect.playerBuffs?.length) {
            let newBuffs = playerBuffs;
            effect.playerBuffs.forEach(b => {
                newBuffs = addOrUpdateBuffDebuff(newBuffs, b.type, b.stacks, b.duration, b.value, false);
            });
            setPlayerBuffs(newBuffs);
        }

        // カード熟練度を上昇させて、デッキに反映
        const updatedCard = incrementUseCount(card);
        dispatch({ type: "CARD_PLAY", card: updatedCard });
    };

    // --- ロジック: プレイヤーのターン開始 ---
    const startPlayerTurn = useCallback(async () => {
        await new Promise<void>(r => showMessage("プレイヤーのターン", 2500, r));

        // 1. Guardの消滅
        setPlayerGuard(0);

        // 2. バフ/デバフの持続時間減少
        setPlayerBuffs(b => decreaseBuffDebuffDuration(b));
        setEnemyBuffs(b => decreaseBuffDebuffDuration(b));

        // 3. 回復・Guard再生
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
            setEnergy(0); // エナジーを0に設定
            return;
        } else {
            // 4. エナジー再生処理
            const energyModifier = calculateEnergyModifier(playerBuffs);
            const totalEnergy = Math.max(0, maxEnergy + energyModifier);
            setEnergy(totalEnergy);
        }


        // 5. 自動浄化処理
        if (playerBuffs.has("cleanse")) {
            const cleanse = playerBuffs.get("cleanse")!;
            const cleanseCount = cleanse.value * cleanse.stacks;
            setPlayerBuffs(b => removeDebuffs(b, cleanseCount));
        }


        setTurnPhase("player");
        setTurn(t => t + 1);
        const currentDeck = deckStateRef.current;

        // ドロー計算（drawPowerバフ考慮）
        const drawModifier = calculateDrawModifier(playerBuffs);
        const totalDrawCount = 5 + drawModifier;
        const { drawnCards, newDrawPile, newDiscardPile } = drawCards(
            totalDrawCount,
            currentDeck.drawPile,
            currentDeck.discardPile
        );
        drawnCardsRef.current = drawnCards;
        dispatch({ type: "SET_PILES", newDrawPile, newDiscardPile });

        // シャッフル判定にも最新のRefの値を使用
        if (newDiscardPile.length === 0 && currentDeck.discardPile.length > 0) {
            setIsShuffling(true);
        } else if (drawnCards.length > 0) {
            setIsDrawingAnimation(true);
        }
    }, [playerBuffs, playerMaxHp, showMessage, maxEnergy, showHealEffect, showShieldEffect]);

    // --- ロジック: 敵のターン実行 ---
    const executeEnemyTurn = useCallback(async () => {
        // 1. Guardの消滅と自動Guard付与
        setEnemyGuard(currentEnemy.startingGuard);

        // 2. 回復・Guard再生処理
        const { hp, shield } = calculateStartTurnHealing(enemyBuffs);
        if (hp > 0) {
            setEnemyHp(h => applyHeal(hp, h, enemyMaxHp));
            await new Promise(r => setTimeout(r, 500));
        }
        if (shield > 0) {
            setEnemyGuard(g => g + shield);
            await new Promise(r => setTimeout(r, 500));
        }

        // 3. 行動不可チェック
        if (!canAct(enemyBuffs)) {
            // このターンは行動不可
            await new Promise(r => setTimeout(r, 1500));
            setEnemyTurnCount(c => c + 1);
            startPlayerTurn();
            return;
        }

        await new Promise(r => setTimeout(r, 800));

        // 4. 敵のAIによる行動決定
        const enemyAction = determineEnemyAction(currentEnemy, enemyHp, enemyTurnCount);

        // Guardを付与する行動の場合
        if (enemyAction.guardGain && enemyAction.guardGain > 0) {
            setEnemyGuard(g => g + enemyAction.guardGain!);
            await new Promise(r => setTimeout(r, 800));
            setEnemyTurnCount(c => c + 1);
            startPlayerTurn();
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
        const enemyAttackCard = enemyActionToCard(enemyAction);

        // ダメージ計算と適用（連続攻撃の場合は複数回）
        const hitCount = enemyAction.hitCount || 1;
        for (let i = 0; i < hitCount; i++) {
            const damageResult = calculateDamage(enemyChar, playerChar, enemyAttackCard);
            const allocation = applyDamageAllocation(playerChar, damageResult.finalDamage);

            // ダメージ適用
            setPlayerGuard(g => Math.max(0, g - allocation.guardDamage));
            setPlayerAp(a => Math.max(0, a - allocation.apDamage));
            setPlayerHp(h => Math.max(0, h - allocation.hpDamage));

            if (playerRef.current) {
                showDamageEffect(playerRef.current, damageResult.finalDamage, false);
            }

            // 反撃ダメージ
            if (damageResult.reflectDamage > 0) {
                setEnemyHp(h => Math.max(0, h - damageResult.reflectDamage));
                if (enemyRef.current) showDamageEffect(enemyRef.current, damageResult.reflectDamage, false);
            }

            // 統計追跡
            setBattleStats(stats => ({
                ...stats,
                damageTaken: stats.damageTaken + damageResult.finalDamage,
            }));

            if (i < hitCount - 1) {
                await new Promise(r => setTimeout(r, 500));
            }
        }

        // デバフ付与
        if (enemyAction.applyDebuffs && enemyAction.applyDebuffs.length > 0) {
            let newBuffs = playerBuffs;
            enemyAction.applyDebuffs.forEach(debuff => {
                newBuffs = addOrUpdateBuffDebuff(
                    newBuffs,
                    debuff.type,
                    debuff.stacks,
                    debuff.duration,
                    debuff.value,
                    debuff.isPermanent
                );
            });
            setPlayerBuffs(newBuffs);
        }

        await new Promise(r => setTimeout(r, 800));

        // 5. ターン終了時の持続ダメージ（毒、火傷など）
        const dotDamage = calculateEndTurnDamage(enemyBuffs);
        if (dotDamage > 0) {
            setEnemyHp(h => Math.max(0, h - dotDamage));
            if (enemyRef.current) showDamageEffect(enemyRef.current, dotDamage, false);
            await new Promise(r => setTimeout(r, 500));
        }

        // ターンカウント増加
        setEnemyTurnCount(c => c + 1);

        // プレイヤーのターンへ
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
        playerHp,
        playerMaxHp,
        playerAp,
        playerMaxAp,
        playerGuard,
        playerBuffs,
        // depth,
        startPlayerTurn,
        showDamageEffect,
    ]);

    // --- ロジック: ターン終了 ---
    const handleEndTurn = () => {
        if (turnPhase !== "player") return;
        setTurnPhase("transition");

        // 持続ダメージ処理（プレイヤーターン終了時）
        const dotDamage = calculateEndTurnDamage(playerBuffs);
        if (dotDamage > 0) {
            setPlayerHp(h => Math.max(0, h - dotDamage));
            // TODO: ダメージエフェクトを表示
        }

        // Momentumバフのスタック増加
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

    // --- useEffect: アニメーション制御 ---
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

    // --- 次の敵へのリセット関数 ---
    const resetForNextEnemy = useCallback((nextEnemy: Enemy) => {
        // === 敵データのリセット ===
        setCurrentEnemy(nextEnemy);
        setEnemyHp(nextEnemy.maxHp);
        setEnemyMaxHp(nextEnemy.maxHp);
        setEnemyAp(nextEnemy.maxAp);
        setEnemyMaxAp(nextEnemy.maxAp);
        setEnemyGuard(nextEnemy.startingGuard);
        setEnemyBuffs(new Map());
        setEnemyTurnCount(1);

        // === プレイヤーのバトル状態リセット（HP/APは維持）===
        setPlayerGuard(0); // Guardはリセット
        setPlayerBuffs(new Map()); // バフ/デバフをクリア
        setEnergy(maxEnergy); // エナジーを全回復
        setSwordEnergy(createInitialSwordEnergy()); // 剣気をリセット

        // === デッキのリセット（全カードを山札に戻して5枚ドロー）===
        const allCards = [
            ...deckStateRef.current.hand,
            ...deckStateRef.current.drawPile,
            ...deckStateRef.current.discardPile,
        ];
        // シャッフル
        const shuffledDeck = [...allCards].sort(() => Math.random() - 0.5);
        const newHand = shuffledDeck.slice(0, 5);
        const newDrawPile = shuffledDeck.slice(5);
        dispatch({ type: "RESET_DECK", hand: newHand, drawPile: newDrawPile, discardPile: [] });

        // === ターンと統計のリセット ===
        setTurn(1);
        setTurnPhase("player");
        setBattleStats({ damageDealt: 0, damageTaken: 0 });

        // === バトル結果をリセット ===
        setBattleResult("ongoing");
    }, [maxEnergy]);

    // --- 勝敗判定 (derived state) ---
    // useEffectではなく、HPに基づいて計算
    const actualBattleResult = (() => {
        if (battleResult !== "ongoing") return battleResult;
        if (enemyHp <= 0) return "victory";
        if (playerHp <= 0) return "defeat";
        return "ongoing";
    })();

    return {
        // Refs
        playerRef, enemyRef,
        // Enemy Data
        currentEnemy,
        // Stats
        playerHp, playerMaxHp, playerAp, playerMaxAp, playerGuard, playerBuffs,
        enemyHp, enemyMaxHp, enemyAp, enemyMaxAp, enemyGuard, enemyBuffs,
        energy, maxEnergy, turn, turnPhase,
        turnMessage, showTurnMessage,
        // Sword Energy
        swordEnergy,
        // Deck
        hand: deckState.hand,
        drawPile: deckState.drawPile,
        discardPile: deckState.discardPile,
        // Animations / Helpers
        isNewCard, getDiscardingCards,
        // Actions
        handleCardPlay, handleEndTurn,
        resetForNextEnemy,
        onDepthChange: () => { },//仮
        // モーダル管理
        openedPileType,
        openDrawPile,
        openDiscardPile,
        closePileModal,
        // 勝敗判定 & 統計
        battleResult: actualBattleResult,
        battleStats,
    };
};