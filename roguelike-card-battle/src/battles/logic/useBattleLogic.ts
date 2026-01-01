import { useState, useRef, useReducer, useEffect, useCallback, createRef, useMemo } from "react";
import type { Card, Depth } from "../../cards/type/cardType";
import type { BuffDebuffMap } from "../../cards/type/baffType";
import type { Enemy } from "../../Character/data/EnemyData";
import { swordMaster } from "../../Character/data/PlayerData";

// 複数敵用の状態インターフェース
export interface EnemyBattleState {
    enemy: Enemy;
    hp: number;
    maxHp: number;
    ap: number;
    maxAp: number;
    guard: number;
    buffs: BuffDebuffMap;
    turnCount: number;
    ref: React.RefObject<HTMLDivElement | null>;
}
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
// import {
//     createMasteryStore,
//     incrementCardMastery,
//     applyMasteryToCards,
//     type MasteryStore,
// } from "../../cards/state/masteryManager";

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

// 敵の初期状態を作成するヘルパー関数
function createEnemyState(enemy: Enemy): EnemyBattleState {
    return {
        enemy,
        hp: enemy.maxHp,
        maxHp: enemy.maxHp,
        ap: enemy.maxAp,
        maxAp: enemy.maxAp,
        guard: enemy.startingGuard,
        buffs: new Map(),
        turnCount: 1,
        ref: createRef<HTMLDivElement>(),
    };
}

export const useBattleLogic = (depth: Depth, initialEnemies?: Enemy[]) => {
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
    const enemyRef = useRef<HTMLDivElement>(null); // 後方互換性のため維持
    const drawnCardsRef = useRef<Card[]>([]);

    // --- 複数敵データ State ---
    const [enemies, setEnemies] = useState<EnemyBattleState[]>(() => {
        if (initialEnemies && initialEnemies.length > 0) {
            return initialEnemies.map(createEnemyState);
        }
        // デフォルトで通常敵をランダム選択
        const { enemies: selectedEnemies } = selectRandomEnemy(depth, "normal");
        return selectedEnemies.map(createEnemyState);
    });

    // 現在のターゲット敵インデックス（0が最初の敵）
    // TODO: 複数敵ターゲティング実装時に使用
    // const [_targetEnemyIndex, _setTargetEnemyIndex] = useState(0);

    // --- 後方互換性のための派生値 ---
    const currentEnemy = enemies[0]?.enemy;
    const enemyHp = enemies[0]?.hp ?? 0;
    const enemyMaxHp = enemies[0]?.maxHp ?? 0;
    const enemyAp = enemies[0]?.ap ?? 0;
    const enemyMaxAp = enemies[0]?.maxAp ?? 0;
    const enemyGuard = enemies[0]?.guard ?? 0;
    const enemyBuffs = useMemo(() => {
        return enemies[0]?.buffs ?? new Map();
    }, [enemies]); const enemyTurnCount = enemies[0]?.turnCount ?? 1;

    // --- 基本ステータス State ---
    const [playerName] = useState("eires"); // プレイヤー名（将来のカスタマイズ用に維持）
    const [playerClassName] = useState(swordMaster.classGrade[0]); // プレイヤークラス名
    const [playerHp, setPlayerHp] = useState(100);
    const [playerMaxHp] = useState(100);
    const [playerAp, setPlayerAp] = useState(30); // AP（装備耐久値）
    const [playerMaxAp] = useState(30);
    const [playerGuard, setPlayerGuard] = useState(0); // Guard（一時的な防御）
    const [energy, setEnergy] = useState(3);
    const [maxEnergy] = useState(3);
    const [turn, setTurn] = useState(1);
    const [turnPhase, setTurnPhase] = useState<"player" | "enemy" | "transition">("player");

    // --- バフ State ---
    const [playerBuffs, setPlayerBuffs] = useState<BuffDebuffMap>(new Map());

    // --- Ver 4.0: 速度システム State ---
    const [playerSpeed, setPlayerSpeed] = useState(50); // プレイヤーの速度
    const [enemySpeed, setEnemySpeed] = useState(0); // 敵の速度
    const [turnOrder, setTurnOrder] = useState<"player" | "enemy">("player"); // ターン順序
    const [speedBonusPlayer, setSpeedBonusPlayer] = useState<SpeedBonus | null>(null); // プレイヤーの速度ボーナス
    const [speedBonusEnemy, setSpeedBonusEnemy] = useState<SpeedBonus | null>(null); // 敵の速度ボーナス

    // --- Ver 4.0: 敵エナジー管理 State ---
    const [enemyEnergy, setEnemyEnergy] = useState(0); // 敵の現在のエナジー（行動回数）

    // 敵の状態を更新するヘルパー関数
    const updateEnemy = useCallback((index: number, updater: (state: EnemyBattleState) => Partial<EnemyBattleState>) => {
        setEnemies(prev => prev.map((e, i) => i === index ? { ...e, ...updater(e) } : e));
    }, []);

    // 全ての敵の状態を更新するヘルパー関数（全体攻撃用）
    const updateAllEnemies = useCallback((updater: (state: EnemyBattleState) => Partial<EnemyBattleState>) => {
        setEnemies(prev => prev.map(e => ({ ...e, ...updater(e) })));
    }, []);

    // 後方互換性のためのセッター（最初の敵を更新）
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

    // --- Ver 4.0: 敵エナジー計算関数 ---
    /**
     * 敵の基本エナジーを計算
     */
    const calculateEnemyEnergy = useCallback((enemy: Enemy): number => {
        return enemy.baseEnemyEnergy;
    }, []);

    /**
     * 敵エナジーにバフ/デバフを適用
     * Ver 4.0では slow はエナジーに影響しない（速度のみ）
     */
    const applyEnemyEnergyModifiers = useCallback((
        baseEnergy: number,
        _buffs: BuffDebuffMap // 将来の拡張のために予約（現在未使用）
    ): number => {
        let energy = baseEnergy;

        // 将来的に energyRegen などのバフを追加可能
        // 現状では slow はエナジーに影響しない
        // _buffsパラメータは将来の拡張のために予約

        return Math.max(1, energy); // 最低1エナジー保証
    }, []);

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

        // === カード固有効果の処理 ===

        // guardAmount: カードに設定されたGuard付与量
        if (card.guardAmount && card.guardAmount > 0) {
            setPlayerGuard(g => g + card.guardAmount!);
            if (playerRef.current) showShieldEffect(playerRef.current, card.guardAmount);
        }

        // 剣気結界（sw_037）: Guard = 剣気×8
        if (card.cardTypeId === "sw_037") {
            const guardFromEnergy = swordEnergy.current * 8;
            setPlayerGuard(g => g + guardFromEnergy);
            if (playerRef.current) showShieldEffect(playerRef.current, guardFromEnergy);
        }

        // 不屈の闘志（sw_039）: Guard = 剣気×2
        if (card.cardTypeId === "sw_039") {
            const guardFromEnergy = swordEnergy.current * 2;
            setPlayerGuard(g => g + guardFromEnergy);
            if (playerRef.current) showShieldEffect(playerRef.current, guardFromEnergy);
        }

        // 剣気吸収（sw_040）: Guard = 剣気×3
        if (card.cardTypeId === "sw_040") {
            const guardFromEnergy = swordEnergy.current * 3;
            setPlayerGuard(g => g + guardFromEnergy);
            if (playerRef.current) showShieldEffect(playerRef.current, guardFromEnergy);
        }

        // healAmount: カードに設定された回復量
        if (card.healAmount && card.healAmount > 0) {
            const newHp = applyHeal(card.healAmount, playerHp, playerMaxHp);
            setPlayerHp(newHp);
            if (playerRef.current) showHealEffect(playerRef.current, card.healAmount);
        }

        // energyGain: エナジー回復
        if (card.energyGain && card.energyGain > 0) {
            setEnergy(e => Math.min(maxEnergy, e + card.energyGain!));
        }

        // drawCards: 手札追加
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

        // 効果適用: 回復（calculateCardEffectからの結果）
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

        // Ver 4.0: 出血ダメージ適用（カード使用毎）
        const bleedDamage = calculateBleedDamage(playerMaxHp, playerBuffs);
        if (bleedDamage > 0) {
            setPlayerHp(prev => Math.max(0, prev - bleedDamage));
            if (playerRef.current) {
                showDamageEffect(playerRef.current, bleedDamage, false);
            }
            await new Promise(r => setTimeout(r, 300)); // 出血ダメージ表示のディレイ
        }
    };

    // --- ロジック: プレイヤーのターン開始 ---
    const startPlayerTurn = useCallback(async () => {
        // Ver 4.0: 速度計算
        const calcPlayerSpeed = calculatePlayerSpeed(playerBuffs);
        const calcEnemySpeed = currentEnemy ? calculateEnemySpeed(currentEnemy, enemyBuffs) : 0;
        setPlayerSpeed(calcPlayerSpeed);
        setEnemySpeed(calcEnemySpeed);

        // ターン順序決定
        const order = determineTurnOrder(calcPlayerSpeed, calcEnemySpeed);
        setTurnOrder(order);

        // 速度ボーナス計算（プレイヤーが先攻の場合）
        if (order === "player") {
            const bonus = calculateSpeedBonus(calcPlayerSpeed, calcEnemySpeed);
            setSpeedBonusPlayer(bonus);
            setSpeedBonusEnemy(null);
        } else {
            setSpeedBonusPlayer(null);
        }

        // Ver 4.0: 敵エナジー計算
        if (currentEnemy) {
            const baseEnergy = calculateEnemyEnergy(currentEnemy);
            const modifiedEnergy = applyEnemyEnergyModifiers(baseEnergy, enemyBuffs);
            setEnemyEnergy(modifiedEnergy);
        }

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
    }, [playerBuffs, playerMaxHp, showMessage, maxEnergy, showHealEffect, showShieldEffect, setEnemyBuffs, currentEnemy, enemyBuffs, calculateEnemyEnergy, applyEnemyEnergyModifiers]);

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

        // Ver 4.0: 敵の複数行動システム
        // 敵エナジーに基づいて複数回行動を実行
        const checkBattleEnd = () => {
            return playerHp <= 0 || enemyHp <= 0;
        };

        const onExecuteAction = async (action: import("../../Character/data/EnemyData").EnemyAction) => {
            // Guardを付与する行動の場合
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
            const enemyAttackCard = enemyActionToCard(action);

            // ダメージ計算と適用（連続攻撃の場合は複数回）
            const hitCount = action.hitCount || 1;
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
            if (action.applyDebuffs && action.applyDebuffs.length > 0) {
                let newBuffs = playerBuffs;
                action.applyDebuffs.forEach(debuff => {
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

            // Ver 4.0: 出血ダメージ適用（敵の1行動実行後）
            const bleedDamage = calculateBleedDamage(enemyMaxHp, enemyBuffs);
            if (bleedDamage > 0) {
                setEnemyHp(h => Math.max(0, h - bleedDamage));
                if (enemyRef.current) {
                    showDamageEffect(enemyRef.current, bleedDamage, false);
                }
                await new Promise(r => setTimeout(r, 300));
            }
        };

        // 敵エナジー分の行動を実行
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
    ]);

    // --- ロジック: ターン終了 ---
    const handleEndTurn = () => {
        if (turnPhase !== "player") return;
        setTurnPhase("transition");

        // 持続ダメージ処理（プレイヤーターン終了時）
        const dotDamage = calculateEndTurnDamage(playerBuffs);
        if (dotDamage > 0) {
            setPlayerHp(h => Math.max(0, h - dotDamage));
            if (playerRef.current) {
                showDamageEffect(playerRef.current, dotDamage, false);
            }
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

    // --- 次の敵へのリセット関数（複数敵対応）---
    const resetForNextEnemy = useCallback((nextEnemy: Enemy | Enemy[]) => {
        // === 敵データのリセット ===
        const nextEnemies = Array.isArray(nextEnemy) ? nextEnemy : [nextEnemy];
        setEnemies(nextEnemies.map(createEnemyState));

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
    // useEffectではなく、HPに基づいて計算（複数敵対応：全ての敵のHPが0以下で勝利）
    const actualBattleResult = (() => {
        if (battleResult !== "ongoing") return battleResult;
        const allEnemiesDead = enemies.every(e => e.hp <= 0);
        if (allEnemiesDead) return "victory";
        if (playerHp <= 0) return "defeat";
        return "ongoing";
    })();

    // 生存している敵のリスト
    const aliveEnemies = enemies.filter(e => e.hp > 0);

    // Ver 4.0: 敵の次ターン行動プレビュー
    const nextEnemyActions = useMemo(() => {
        if (!currentEnemy || enemyHp <= 0) {
            return [];
        }
        return previewEnemyActions(currentEnemy, enemyHp, turn + 1);
    }, [currentEnemy, enemyHp, turn]);

    return {
        // Refs
        playerRef, enemyRef,
        // Enemy Data（後方互換性）
        currentEnemy,
        // 複数敵データ
        enemies,
        aliveEnemies,
        updateEnemy,
        updateAllEnemies,
        // Stats（後方互換性）
        playerName, playerClassName, playerHp, playerMaxHp, playerAp, playerMaxAp, playerGuard, playerBuffs,
        enemyHp, enemyMaxHp, enemyAp, enemyMaxAp, enemyGuard, enemyBuffs,
        energy, maxEnergy, turn, turnPhase,
        turnMessage, showTurnMessage,
        // Ver 4.0: Speed System
        playerSpeed, enemySpeed, turnOrder, speedBonusPlayer, speedBonusEnemy,
        // Ver 4.0: Enemy Energy
        enemyEnergy,
        // Ver 4.0: Next Enemy Actions Preview
        nextEnemyActions,
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