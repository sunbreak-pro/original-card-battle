import { useState, useRef, useReducer, useEffect, useCallback } from "react";
import type { Card, Depth } from "../../cards/type/cardType";
import type { BuffDebuffMap } from "../../cards/type/baffType";
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
} from "../../cards/state/buff";
import {
    applyDamage,
    applyHeal,
    applyShield,
    determineEnemyAction,
} from "../../battles/logic/battle";
import { deckReducer } from "../decks/deckReducter";
import { createInitialDeck, drawCards } from "../../battles/decks/deck";
import { SAMPLE_CARDS } from "../../cards/data/CardData";
import { useCardAnimation } from "../battleUI/animations/useCardAnimation";
import { useTurnTransition } from "../battleUI/animations/useTurnTransition";

// 初期デッキ設定
const INITIAL_DECK_COUNTS = {
    phy_001: 4, phy_002: 3, phy_003: 3,
    mag_001: 2, def_001: 4, def_002: 2, heal_001: 2,
};

export const useBattleLogic = (depth: Depth) => {
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

    // --- 基本ステータス State ---
    const [playerHp, setPlayerHp] = useState(80);
    const [playerMaxHp] = useState(100);
    const [playerShield, setPlayerShield] = useState(15);
    const [enemyHp, setEnemyHp] = useState(45);
    const [enemyMaxHp] = useState(60);
    const [enemyShield, setEnemyShield] = useState(10);
    const [energy, setEnergy] = useState(3);
    const [maxEnergy] = useState(3);
    const [turn, setTurn] = useState(1);
    const [turnPhase, setTurnPhase] = useState<"player" | "enemy" | "transition">("player");

    // --- バフ State ---
    const [playerBuffs, setPlayerBuffs] = useState<BuffDebuffMap>(new Map());
    const [enemyBuffs, setEnemyBuffs] = useState<BuffDebuffMap>(new Map());

    // --- デッキ State (Reducer) ---
    const initialDeckState = (() => {
        const initialDeck = createInitialDeck(INITIAL_DECK_COUNTS, SAMPLE_CARDS);
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
        const effect = calculateCardEffect(card, depth);

        // アニメーション
        if (cardElement) {
            const isPlayerTarget = effect.shieldGain || effect.hpGain || effect.playerBuffs?.length;
            const target = isPlayerTarget ? playerRef.current : enemyRef.current;
            if (target) await playCardWithAnimation(cardElement, target, () => { });
        }

        // 効果適用: ダメージ
        if (effect.damageToEnemy) {
            const { newHp, newShield } = applyDamage(effect.damageToEnemy, enemyHp, enemyShield);
            setEnemyHp(newHp);
            setEnemyShield(newShield);
            if (enemyRef.current) showDamageEffect(enemyRef.current, effect.damageToEnemy, false);

            // 統計追跡
            setBattleStats(stats => ({
                ...stats,
                damageDealt: stats.damageDealt + effect.damageToEnemy!,
            }));
        }
        // 効果適用: シールド
        if (effect.shieldGain) {
            const newShield = applyShield(effect.shieldGain, playerShield);
            setPlayerShield(newShield);
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

        // 回復・シールド
        const { hp, shield } = calculateStartTurnHealing(playerBuffs);
        if (hp > 0) {
            setPlayerHp(h => applyHeal(hp, h, playerMaxHp));
            if (playerRef.current) showHealEffect(playerRef.current, hp);
            await new Promise(r => setTimeout(r, 500));
        }
        if (shield > 0) {
            setPlayerShield(s => applyShield(shield, s));
            if (playerRef.current) showShieldEffect(playerRef.current, shield);
            await new Promise(r => setTimeout(r, 500));
        }

        setTurnPhase("player");
        setTurn(t => t + 1);
        setEnergy(maxEnergy);
        const currentDeck = deckStateRef.current;

        // ドロー計算
        const { drawnCards, newDrawPile, newDiscardPile } = drawCards(
            5,
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
    }, [playerBuffs, playerHp, playerMaxHp, playerShield, showMessage, maxEnergy, showHealEffect, showShieldEffect]);

    // --- ロジック: 敵のターン実行 ---
    const executeEnemyTurn = useCallback(async () => {
        // 毒などのダメージ
        const dmg = calculateEndTurnDamage(enemyBuffs);
        if (dmg > 0) {
            const { newHp, newShield } = applyDamage(dmg, enemyHp, enemyShield);
            setEnemyHp(newHp);
            setEnemyShield(newShield);
            if (enemyRef.current) showDamageEffect(enemyRef.current, dmg, false);
            await new Promise(r => setTimeout(r, 300));
        }
        setEnemyBuffs(b => decreaseBuffDebuffDuration(b));
        await new Promise(r => setTimeout(r, 800));

        // 攻撃
        const damage = determineEnemyAction();
        const { newHp, newShield } = applyDamage(damage, playerHp, playerShield);
        setPlayerHp(newHp);
        setPlayerShield(newShield);
        if (playerRef.current) showDamageEffect(playerRef.current, damage, false);

        // 統計追跡
        setBattleStats(stats => ({
            ...stats,
            damageTaken: stats.damageTaken + damage,
        }));

        await new Promise(r => setTimeout(r, 800));

        // プレイヤーのターンへ
        startPlayerTurn();
    }, [
        // 依存配列を修正
        enemyBuffs,
        enemyHp,
        enemyShield,
        playerHp,
        playerShield,
        startPlayerTurn,
        showDamageEffect // 追加
    ]);

    // --- ロジック: ターン終了 ---
    const handleEndTurn = () => {
        if (turnPhase !== "player") return;
        setTurnPhase("transition");

        const dmg = calculateEndTurnDamage(playerBuffs);
        if (dmg > 0) {
            // 演出省略
        }
        setPlayerBuffs(b => decreaseBuffDebuffDuration(b));

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

    // --- 勝敗判定 (HPの監視) ---
    useEffect(() => {
        if (battleResult !== "ongoing") return;

        if (enemyHp <= 0) {
            setBattleResult("victory");
        } else if (playerHp <= 0) {
            setBattleResult("defeat");
        }
    }, [enemyHp, playerHp, battleResult]);

    return {
        // Refs
        playerRef, enemyRef,
        // Stats
        playerHp, playerMaxHp, playerShield, playerBuffs,
        enemyHp, enemyMaxHp, enemyShield, enemyBuffs,
        energy, maxEnergy, turn, turnPhase,
        turnMessage, showTurnMessage,
        // Deck
        hand: deckState.hand,
        drawPile: deckState.drawPile,
        discardPile: deckState.discardPile,
        // Animations / Helpers
        isNewCard, getDiscardingCards,
        // Actions
        handleCardPlay, handleEndTurn,
        onDepthChange: () => { },//仮
        // モーダル管理
        openedPileType,
        openDrawPile,
        openDiscardPile,
        closePileModal,
        // 勝敗判定 & 統計
        battleResult,
        battleStats,
    };
};