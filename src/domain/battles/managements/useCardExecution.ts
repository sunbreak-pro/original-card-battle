/**
 * Card Execution Hook
 *
 * Handles card effect execution including:
 * - Damage calculation and application
 * - Guard/Shield effects
 * - Healing effects
 * - Sword energy processing
 * - Buff/Debuff application
 * - Card draw effects
 *
 * This hook is designed to be composed with useBattleState and useSwordEnergy.
 */

import { useCallback, type RefObject } from "react";
import type { Card } from '@/types/cardTypes';
import type { BuffDebuffMap, BuffDebuffState } from '@/types/battleTypes';
import type { BattleStats } from '@/types/characterTypes';
import type { DeckState } from "../../cards/decks/deckReducter";
import type {
  CardExecutionResult,
  CardEffectPreview,
} from '@/types/battleTypes';
import { createDefaultExecutionResult } from '../logic/cardExecutionLogic';
import type { SwordEnergyState } from '@/types/characterTypes';

// Card logic
import { calculateCardEffect, canPlayCard as canPlayCardCheck, incrementUseCount } from "../../cards/state/card";

// Damage calculation
import { calculateDamage, applyDamageAllocation } from "../calculators/damageCalculation";

// Buff/Debuff logic
import { addOrUpdateBuffDebuff } from "../logic/buffLogic";
import { applyHeal } from "../logic/battleLogic";

// Bleed damage
import { calculateBleedDamage } from "../logic/bleedDamage";

// Sword energy
import {
  addSwordEnergy,
  consumeSwordEnergy,
  consumeAllSwordEnergy,
  getSwordEnergyBleedChance,
} from "../../characters/player/logic/swordEnergySystem";

import { SWORD_ENERGY_BLEED_DURATION, SWORD_ENERGY_BLEED_STACKS } from "@/constants";

// Deck management
import { drawCards } from "../../cards/decks/deck";

// ============================================================================
// Types
// ============================================================================

/**
 * Animation handlers passed from parent
 */
export interface CardAnimationHandlers {
  playCardWithAnimation: (
    cardElement: HTMLElement,
    targetElement: HTMLElement,
    callback: () => void
  ) => Promise<void>;
  showDamageEffect: (target: HTMLElement, amount: number, isCritical: boolean) => void;
  showHealEffect: (target: HTMLElement, amount: number) => void;
  showShieldEffect: (target: HTMLElement, amount: number) => void;
  drawCardsWithAnimation: (
    cards: Card[],
    callback: (cards: Card[]) => void,
    delay: number
  ) => void;
}

/**
 * State setters for card execution
 */
export interface CardExecutionSetters {
  setPlayerEnergy: (updater: number | ((prev: number) => number)) => void;
  setPlayerGuard: (updater: number | ((prev: number) => number)) => void;
  setPlayerHp: (updater: number | ((prev: number) => number)) => void;
  setPlayerBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;
  setEnemyHp: (updater: number | ((prev: number) => number)) => void;
  setEnemyAp: (updater: number | ((prev: number) => number)) => void;
  setEnemyGuard: (updater: number | ((prev: number) => number)) => void;
  setEnemyBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;
  setSwordEnergy: React.Dispatch<React.SetStateAction<SwordEnergyState>>;
  setBattleStats: (
    updater: (prev: { damageDealt: number; damageTaken: number }) => {
      damageDealt: number;
      damageTaken: number;
    }
  ) => void;
}

/**
 * Deck dispatch action for card play
 */
export interface DeckDispatch {
  (action:
    | { type: "CARD_PLAY"; card: Card }
    | { type: "SET_PILES"; newDrawPile: Card[]; newDiscardPile: Card[] }
    | { type: "ADD_TO_HAND"; cards: Card[] }
  ): void;
}

/**
 * Return type for useCardExecution hook
 */
export interface UseCardExecutionReturn {
  /**
   * Execute a card's effects
   */
  executeCard: (
    card: Card,
    cardElement?: HTMLElement
  ) => Promise<CardExecutionResult>;

  /**
   * Check if a card can be played
   */
  canPlayCard: (card: Card) => boolean;

  /**
   * Get preview of card effects
   */
  getCardEffectPreview: (card: Card) => CardEffectPreview;
}

// ============================================================================
// Hook Implementation
// ============================================================================

/**
 * Card execution hook
 *
 * @param playerStats - Current player battle stats
 * @param enemyStats - Current enemy battle stats
 * @param playerEnergy - Current player energy
 * @param maxEnergy - Maximum player energy
 * @param playerHp - Current player HP
 * @param playerMaxHp - Maximum player HP
 * @param playerBuffs - Current player buffs
 * @param enemyBuffs - Current enemy buffs
 * @param swordEnergy - Current sword energy state
 * @param isPlayerPhase - Whether it's currently player's phase
 * @param playerRef - Ref to player element for animations
 * @param getTargetEnemyRef - Function to get enemy element ref
 * @param deckStateRef - Ref to current deck state
 * @param setters - State setter functions
 * @param animations - Animation handler functions
 * @param dispatch - Deck reducer dispatch
 */
export function useCardExecution(
  playerStats: BattleStats,
  enemyStats: BattleStats,
  playerEnergy: number,
  maxEnergy: number,
  playerHp: number,
  playerMaxHp: number,
  playerBuffs: BuffDebuffMap,
  enemyBuffs: BuffDebuffMap,
  swordEnergy: SwordEnergyState,
  isPlayerPhase: boolean,
  playerRef: RefObject<HTMLDivElement | null>,
  getTargetEnemyRef: () => HTMLElement | null,
  deckStateRef: RefObject<DeckState>,
  setters: CardExecutionSetters,
  animations: CardAnimationHandlers,
  dispatch: DeckDispatch
): UseCardExecutionReturn {
  // ========================================================================
  // Check if card can be played
  // ========================================================================

  const canPlayCard = useCallback(
    (card: Card): boolean => {
      return canPlayCardCheck(card, playerEnergy, isPlayerPhase);
    },
    [playerEnergy, isPlayerPhase]
  );

  // ========================================================================
  // Get card effect preview
  // ========================================================================

  const getCardEffectPreview = useCallback(
    (card: Card): CardEffectPreview => {
      const effect = calculateCardEffect(card);

      let canPlay = true;
      let cannotPlayReason: string | undefined;

      if (!isPlayerPhase) {
        canPlay = false;
        cannotPlayReason = "Not player phase";
      } else if (playerEnergy < card.cost) {
        canPlay = false;
        cannotPlayReason = "Not enough energy";
      }

      // Estimate damage
      let estimatedDamage = 0;
      if (effect.damageToEnemy) {
        // Apply sword energy flat bonus to base damage
        const swordEnergyFlatBonus = swordEnergy.current;
        const cardWithBonus = swordEnergyFlatBonus > 0 && card.baseDamage !== undefined
          ? { ...card, baseDamage: card.baseDamage + swordEnergyFlatBonus }
          : card;
        const damageResult = calculateDamage(playerStats, enemyStats, cardWithBonus);
        estimatedDamage = damageResult.finalDamage;
      }

      return {
        estimatedDamage,
        guardGain: effect.shieldGain || card.guardAmount || 0,
        healAmount: effect.hpGain || card.healAmount || 0,
        energyCost: card.cost,
        canPlay,
        cannotPlayReason,
        swordEnergyGain: card.swordEnergyGain || 0,
        swordEnergyConsume:
          card.swordEnergyConsume !== undefined
            ? card.swordEnergyConsume === 0
              ? swordEnergy.current
              : card.swordEnergyConsume
            : 0,
        cardDraw: card.drawCards || 0,
        playerBuffs: effect.playerBuffs?.map((b) => b.name) || [],
        enemyDebuffs: effect.enemyDebuffs?.map((b) => b.name) || [],
      };
    },
    [playerEnergy, isPlayerPhase, swordEnergy, playerStats, enemyStats]
  );

  // ========================================================================
  // Execute card
  // ========================================================================

  const executeCard = useCallback(
    async (card: Card, cardElement?: HTMLElement): Promise<CardExecutionResult> => {
      const result = createDefaultExecutionResult();

      // Check if card can be played
      if (!canPlayCard(card)) {
        return result;
      }

      result.success = true;
      result.energyConsumed = card.cost;

      // Consume energy
      setters.setPlayerEnergy((e) => e - card.cost);

      const effect = calculateCardEffect(card);

      // Animation
      if (cardElement) {
        const isPlayerTarget =
          effect.shieldGain || effect.hpGain || effect.playerBuffs?.length;
        const target = isPlayerTarget
          ? playerRef.current
          : getTargetEnemyRef();
        if (target) {
          await animations.playCardWithAnimation(cardElement, target, () => {});
        }
      }

      // ====================================================================
      // Sword Energy Processing
      // ====================================================================

      if (card.swordEnergyConsume !== undefined) {
        if (card.swordEnergyConsume === 0) {
          // Consume all sword energy
          setters.setSwordEnergy((prev) => {
            const swordResult = consumeAllSwordEnergy(prev);
            result.swordEnergyConsumed = swordResult.consumed;
            result.swordEnergyChange = -swordResult.consumed;
            return swordResult.newState;
          });
        } else {
          // Consume specific amount
          setters.setSwordEnergy((prev) => {
            const swordResult = consumeSwordEnergy(prev, card.swordEnergyConsume!);
            result.swordEnergyConsumed = swordResult.consumed;
            result.swordEnergyChange = -swordResult.consumed;
            return swordResult.newState;
          });
        }
      } else if (card.swordEnergyGain) {
        // Gain sword energy
        setters.setSwordEnergy((prev) => {
          const newState = addSwordEnergy(prev, card.swordEnergyGain!);
          result.swordEnergyChange = card.swordEnergyGain!;
          return newState;
        });
      }

      // ====================================================================
      // Damage Application
      // ====================================================================

      if (effect.damageToEnemy) {
        // Apply sword energy flat bonus to base damage
        const swordEnergyFlatBonus = swordEnergy.current;
        const cardWithBonus = swordEnergyFlatBonus > 0 && card.baseDamage !== undefined
          ? { ...card, baseDamage: card.baseDamage + swordEnergyFlatBonus }
          : card;
        const damageResult = calculateDamage(
          playerStats,
          enemyStats,
          cardWithBonus
        );
        const allocation = applyDamageAllocation(
          enemyStats,
          damageResult.finalDamage
        );

        result.damageDealt = damageResult.finalDamage;
        result.isCritical = damageResult.isCritical;

        setters.setEnemyGuard((g) => Math.max(0, g - allocation.guardDamage));
        setters.setEnemyAp((a) => Math.max(0, a - allocation.apDamage));
        setters.setEnemyHp((h) => Math.max(0, h - allocation.hpDamage));

        const enemyTarget = getTargetEnemyRef();
        if (enemyTarget) {
          animations.showDamageEffect(
            enemyTarget,
            damageResult.finalDamage,
            damageResult.isCritical
          );
        }

        // Lifesteal
        if (damageResult.lifestealAmount > 0) {
          const newHp = applyHeal(
            damageResult.lifestealAmount,
            playerHp,
            playerMaxHp
          );
          setters.setPlayerHp(newHp);
          result.lifestealAmount = damageResult.lifestealAmount;
          if (playerRef.current) {
            animations.showHealEffect(
              playerRef.current,
              damageResult.lifestealAmount
            );
          }
        }

        // Reflect damage
        if (damageResult.reflectDamage > 0) {
          setters.setPlayerHp((h) => Math.max(0, h - damageResult.reflectDamage));
          result.reflectDamage = damageResult.reflectDamage;
          if (playerRef.current) {
            animations.showDamageEffect(
              playerRef.current,
              damageResult.reflectDamage,
              false
            );
          }
        }

        // Update battle stats
        setters.setBattleStats((stats) => ({
          ...stats,
          damageDealt: stats.damageDealt + damageResult.finalDamage,
        }));

        // Auto-bleed from sword energy (swordsman attack cards only)
        if (card.characterClass === "swordsman" && card.tags.includes("attack")) {
          const bleedChance = getSwordEnergyBleedChance(swordEnergy);
          if (bleedChance > 0 && Math.random() < bleedChance) {
            const newEnemyBuffs = addOrUpdateBuffDebuff(
              enemyBuffs,
              "bleed",
              SWORD_ENERGY_BLEED_DURATION,
              3,
              SWORD_ENERGY_BLEED_STACKS,
              false,
              undefined,
              'player'
            );
            setters.setEnemyBuffs(newEnemyBuffs);
          }
        }
      }

      // ====================================================================
      // Guard Application
      // ====================================================================

      if (effect.shieldGain) {
        setters.setPlayerGuard((g) => g + effect.shieldGain!);
        result.guardGained += effect.shieldGain;
        if (playerRef.current) {
          animations.showShieldEffect(playerRef.current, effect.shieldGain);
        }
      }

      if (card.guardAmount && card.guardAmount > 0) {
        setters.setPlayerGuard((g) => g + card.guardAmount!);
        result.guardGained += card.guardAmount;
        if (playerRef.current) {
          animations.showShieldEffect(playerRef.current, card.guardAmount);
        }
      }

      // Special card handling for sword energy guard
      if (card.cardTypeId === "sw_037") {
        const guardFromEnergy = swordEnergy.current * 8;
        setters.setPlayerGuard((g) => g + guardFromEnergy);
        result.guardGained += guardFromEnergy;
        if (playerRef.current) {
          animations.showShieldEffect(playerRef.current, guardFromEnergy);
        }
      }
      if (card.cardTypeId === "sw_039" || card.cardTypeId === "sw_040") {
        const guardFromEnergy = swordEnergy.current * 2;
        setters.setPlayerGuard((g) => g + guardFromEnergy);
        result.guardGained += guardFromEnergy;
        if (playerRef.current) {
          animations.showShieldEffect(playerRef.current, guardFromEnergy);
        }
      }

      // ====================================================================
      // Heal Application
      // ====================================================================

      if (card.healAmount && card.healAmount > 0) {
        const newHp = applyHeal(card.healAmount, playerHp, playerMaxHp);
        setters.setPlayerHp(newHp);
        result.healingDone += card.healAmount;
        if (playerRef.current) {
          animations.showHealEffect(playerRef.current, card.healAmount);
        }
      }

      if (effect.hpGain) {
        const newHp = applyHeal(effect.hpGain, playerHp, playerMaxHp);
        setters.setPlayerHp(newHp);
        result.healingDone += effect.hpGain;
        if (playerRef.current) {
          animations.showHealEffect(playerRef.current, effect.hpGain);
        }
      }

      // ====================================================================
      // Energy Gain
      // ====================================================================

      if (card.energyGain && card.energyGain > 0) {
        setters.setPlayerEnergy((e) => Math.min(maxEnergy, e + card.energyGain!));
      }

      // ====================================================================
      // Card Draw
      // ====================================================================

      if (card.drawCards && card.drawCards > 0) {
        const currentDeck = deckStateRef.current;
        const { drawnCards: newCards, newDrawPile, newDiscardPile } = drawCards(
          card.drawCards,
          currentDeck.drawPile,
          currentDeck.discardPile
        );
        if (newCards.length > 0) {
          dispatch({ type: "SET_PILES", newDrawPile, newDiscardPile });
          result.cardsDrawn = newCards.length;
          animations.drawCardsWithAnimation(
            newCards,
            (cards) => {
              dispatch({ type: "ADD_TO_HAND", cards });
            },
            150
          );
        }
      }

      // ====================================================================
      // Debuffs to Enemy
      // ====================================================================

      if (effect.enemyDebuffs?.length) {
        let newBuffs = enemyBuffs;
        effect.enemyDebuffs.forEach((b) => {
          // Player is applying debuffs to enemy
          newBuffs = addOrUpdateBuffDebuff(
            newBuffs,
            b.name,
            b.duration,
            b.value,
            b.stacks,
            false,
            undefined,
            'player'
          );
          result.debuffsApplied.push(b as BuffDebuffState);
        });
        setters.setEnemyBuffs(newBuffs);
      }

      // ====================================================================
      // Buffs to Player
      // ====================================================================

      if (effect.playerBuffs?.length) {
        let newBuffs = playerBuffs;
        effect.playerBuffs.forEach((b) => {
          // Player is applying buffs to self
          newBuffs = addOrUpdateBuffDebuff(
            newBuffs,
            b.name,
            b.duration,
            b.value,
            b.stacks,
            false,
            undefined,
            'player'
          );
          result.buffsApplied.push(b as BuffDebuffState);
        });
        setters.setPlayerBuffs(newBuffs);
      }

      // ====================================================================
      // Update Card and Discard
      // ====================================================================

      const updatedCard = incrementUseCount(card);
      dispatch({ type: "CARD_PLAY", card: updatedCard });

      // ====================================================================
      // Bleed Damage
      // ====================================================================

      const bleedDamage = calculateBleedDamage(playerMaxHp, playerBuffs);
      if (bleedDamage > 0) {
        setters.setPlayerHp((prev) => Math.max(0, prev - bleedDamage));
        result.bleedDamage = bleedDamage;
        if (playerRef.current) {
          animations.showDamageEffect(playerRef.current, bleedDamage, false);
        }
        await new Promise((r) => setTimeout(r, 300));
      }

      return result;
    },
    [
      canPlayCard,
      playerStats,
      enemyStats,
      playerHp,
      playerMaxHp,
      playerBuffs,
      enemyBuffs,
      swordEnergy,
      maxEnergy,
      playerRef,
      getTargetEnemyRef,
      deckStateRef,
      setters,
      animations,
      dispatch,
    ]
  );

  // ========================================================================
  // Return
  // ========================================================================

  return {
    executeCard,
    canPlayCard,
    getCardEffectPreview,
  };
}
