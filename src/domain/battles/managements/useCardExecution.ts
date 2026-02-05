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

import { useCallback, useEffect, useRef, type RefObject } from "react";
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
import type { DamageModifier } from "../../characters/player/classAbility/classAbilitySystem";
import type { ResonanceEffectConfig } from '@/types/characterTypes';

// Card logic
import { calculateCardEffect, canPlayCard as canPlayCardCheck, incrementUseCount } from "../../cards/state/card";
import { calculateSwordEnergyGuard } from "../../cards/state/cardPlayLogic";

// Damage calculation
import { calculateDamage, applyDamageAllocation } from "../calculators/damageCalculation";

// Buff/Debuff logic
import { addOrUpdateBuffDebuff, removeNDebuffs } from "../logic/buffLogic";

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
 * Enemy state for AoE card execution
 */
export interface EnemyStateForAoE {
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  buffDebuffs: BuffDebuffMap;
  ref: React.RefObject<HTMLDivElement | null>;
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
  getElementalDamageModifier?: (card?: Card) => DamageModifier;
  getResonanceEffects?: (card?: Card) => ResonanceEffectConfig | null;
  /** Get all alive enemies for AoE cards */
  getAliveEnemies?: () => EnemyStateForAoE[];
  /** Update enemy by index for AoE cards */
  updateEnemyByIndex?: (
    index: number,
    updater: (state: EnemyStateForAoE) => Partial<EnemyStateForAoE>
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
 * @param playerMaxHp - Maximum player HP
 * @param playerBuffs - Current player buffs (synced via ref for async safety)
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
  playerMaxHp: number,
  playerBuffs: BuffDebuffMap,
  swordEnergy: SwordEnergyState,
  isPlayerPhase: boolean,
  playerRef: RefObject<HTMLDivElement | null>,
  getTargetEnemyRef: () => HTMLElement | null,
  deckStateRef: RefObject<DeckState>,
  setters: CardExecutionSetters,
  animations: CardAnimationHandlers,
  dispatch: DeckDispatch
): UseCardExecutionReturn {
  // Ref to track latest playerBuffs for use in async callbacks
  const playerBuffsRef = useRef(playerBuffs);
  useEffect(() => {
    playerBuffsRef.current = playerBuffs;
  }, [playerBuffs]);

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
        let adjustedBase = card.baseDamage !== undefined
          ? card.baseDamage + swordEnergyFlatBonus
          : undefined;

        // Apply elemental resonance multiplier
        const elementalMod = setters.getElementalDamageModifier?.(card);
        if (elementalMod && adjustedBase !== undefined) {
          adjustedBase = Math.round(adjustedBase * elementalMod.percentMultiplier);
        }

        const cardWithBonus = adjustedBase !== undefined && adjustedBase !== card.baseDamage
          ? { ...card, baseDamage: adjustedBase }
          : card;
        const damageResult = calculateDamage(playerStats, enemyStats, cardWithBonus);
        const hitCount = card.hitCount || 1;
        estimatedDamage = damageResult.finalDamage * hitCount;
      }

      return {
        estimatedDamage,
        guardGain: (effect.shieldGain ?? 0) + (card.guardAmount ?? 0),
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
    [playerEnergy, isPlayerPhase, swordEnergy, playerStats, enemyStats, setters]
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
          await animations.playCardWithAnimation(cardElement, target, () => { });
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
        // Sword energy flat bonus uses the pre-consumption snapshot (swordEnergy from hook params).
        // This is intentional: the bonus reflects energy at time of card play, not after consumption.
        const swordEnergyFlatBonus = swordEnergy.current;
        let adjustedBaseDamage = card.baseDamage !== undefined
          ? card.baseDamage + swordEnergyFlatBonus
          : undefined;

        // Apply elemental resonance multiplier (Mage class ability)
        const elementalMod = setters.getElementalDamageModifier?.(card);
        if (elementalMod && adjustedBaseDamage !== undefined) {
          adjustedBaseDamage = Math.round(adjustedBaseDamage * elementalMod.percentMultiplier);
        }

        const cardWithBonus = adjustedBaseDamage !== undefined && adjustedBaseDamage !== card.baseDamage
          ? { ...card, baseDamage: adjustedBaseDamage }
          : card;

        const hitCount = card.hitCount || 1;

        // AoE (targetAll) card: apply damage to all alive enemies
        if (card.targetAll && setters.getAliveEnemies && setters.updateEnemyByIndex) {
          const aliveEnemies = setters.getAliveEnemies();
          for (let enemyIdx = 0; enemyIdx < aliveEnemies.length; enemyIdx++) {
            const enemy = aliveEnemies[enemyIdx];
            if (enemy.hp <= 0) continue; // Skip dead enemies

            // Track running totals for multi-hit damage allocation (V-EXEC-03)
            let runningGuard = enemy.guard;
            let runningAp = enemy.ap;
            let runningHp = enemy.hp;

            for (let hit = 0; hit < hitCount; hit++) {
              // Create local stats snapshot for this hit using running totals
              const enemyStatsForCalc: BattleStats = {
                hp: runningHp,
                maxHp: enemy.maxHp,
                ap: runningAp,
                maxAp: enemy.maxAp,
                guard: runningGuard,
                speed: 0,
                buffDebuffs: enemy.buffDebuffs,
              };

              const damageResult = calculateDamage(
                playerStats,
                enemyStatsForCalc,
                cardWithBonus,
                elementalMod?.critBonus ?? 0,
                elementalMod?.penetration ?? 0,
              );
              const allocation = applyDamageAllocation(
                enemyStatsForCalc,
                damageResult.finalDamage
              );

              result.damageDealt += damageResult.finalDamage;
              if (damageResult.isCritical) result.isCritical = true;

              // Update running totals for next hit calculation
              runningGuard = Math.max(0, runningGuard - allocation.guardDamage);
              runningAp = Math.max(0, runningAp - allocation.apDamage);
              runningHp = Math.max(0, runningHp - allocation.hpDamage);

              // Update enemy state by index
              setters.updateEnemyByIndex(enemyIdx, (e) => ({
                guard: Math.max(0, e.guard - allocation.guardDamage),
                ap: Math.max(0, e.ap - allocation.apDamage),
                hp: Math.max(0, e.hp - allocation.hpDamage),
              }));

              // Show damage effect on this enemy
              if (enemy.ref.current) {
                animations.showDamageEffect(
                  enemy.ref.current,
                  damageResult.finalDamage,
                  damageResult.isCritical
                );
              }

              // Update battle stats
              setters.setBattleStats((stats) => ({
                ...stats,
                damageDealt: stats.damageDealt + damageResult.finalDamage,
              }));

              // Stop attacking if enemy is dead (V-EXEC-01 pattern)
              if (runningHp <= 0) {
                break;
              }

              // Delay between hits (not after last hit)
              if (hit < hitCount - 1) {
                await new Promise((r) => setTimeout(r, 300));
              }
            }
            // Small delay between enemies for visual clarity
            if (enemyIdx < aliveEnemies.length - 1) {
              await new Promise((r) => setTimeout(r, 150));
            }
          }
        } else {
          // Single target: track running totals for multi-hit damage allocation (V-EXEC-03)
          // This ensures hit 2 correctly targets remaining guard/AP after hit 1
          let runningGuard = enemyStats.guard;
          let runningAp = enemyStats.ap;
          let runningHp = enemyStats.hp;

          for (let hit = 0; hit < hitCount; hit++) {
            // Create local stats snapshot for this hit using running totals
            const localEnemyStats: BattleStats = {
              ...enemyStats,
              guard: runningGuard,
              ap: runningAp,
              hp: runningHp,
            };

            const damageResult = calculateDamage(
              playerStats,
              localEnemyStats,
              cardWithBonus,
              elementalMod?.critBonus ?? 0,
              elementalMod?.penetration ?? 0,
            );
            const allocation = applyDamageAllocation(
              localEnemyStats,
              damageResult.finalDamage
            );

            result.damageDealt += damageResult.finalDamage;
            if (damageResult.isCritical) result.isCritical = true;

            // Update running totals for next hit calculation
            runningGuard = Math.max(0, runningGuard - allocation.guardDamage);
            runningAp = Math.max(0, runningAp - allocation.apDamage);
            runningHp = Math.max(0, runningHp - allocation.hpDamage);

            // Apply to React state
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
              setters.setPlayerHp(prev => Math.min(prev + damageResult.lifestealAmount, playerMaxHp));
              result.lifestealAmount += damageResult.lifestealAmount;
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
              result.reflectDamage += damageResult.reflectDamage;
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

            // Stop attacking if enemy is dead (V-EXEC-01 pattern)
            if (runningHp <= 0) {
              break;
            }

            // Delay between hits (not after last hit)
            if (hit < hitCount - 1) {
              await new Promise((r) => setTimeout(r, 500));
            }
          }
        }

        // Auto-bleed from sword energy (swordsman attack cards only) — applied once after all hits
        if (card.characterClass === "swordsman" && card.tags.includes("attack")) {
          const bleedChance = getSwordEnergyBleedChance(swordEnergy);
          if (bleedChance > 0 && Math.random() < bleedChance) {
            setters.setEnemyBuffs(prevBuffs =>
              addOrUpdateBuffDebuff(
                prevBuffs,
                "bleed",
                SWORD_ENERGY_BLEED_DURATION,
                3,
                SWORD_ENERGY_BLEED_STACKS,
                false,
                undefined,
                'player'
              )
            );
          }
        }
      }

      // ====================================================================
      // Resonance Effects (Mage class ability)
      // ====================================================================

      const resonanceEffects = setters.getResonanceEffects?.(card);
      if (resonanceEffects) {
        // Burn → apply to enemy
        if (resonanceEffects.burn) {
          setters.setEnemyBuffs(prev =>
            addOrUpdateBuffDebuff(prev, "burn", resonanceEffects.burn!.duration, 3, resonanceEffects.burn!.stacks, false, undefined, 'player')
          );
        }
        // Freeze → apply to enemy
        if (resonanceEffects.freeze) {
          setters.setEnemyBuffs(prev =>
            addOrUpdateBuffDebuff(prev, "freeze", resonanceEffects.freeze!.duration, 100, 1, false, undefined, 'player')
          );
        }
        // Stun → apply to enemy
        if (resonanceEffects.stun) {
          setters.setEnemyBuffs(prev =>
            addOrUpdateBuffDebuff(prev, "stun", resonanceEffects.stun!.duration, 100, 1, false, undefined, 'player')
          );
        }
        // Weakness → apply to enemy
        if (resonanceEffects.weakness) {
          setters.setEnemyBuffs(prev =>
            addOrUpdateBuffDebuff(prev, "weakness", resonanceEffects.weakness!.duration, 20, 1, false, undefined, 'player')
          );
        }
        // Lifesteal → heal player based on % of damage dealt
        if (resonanceEffects.lifesteal && result.damageDealt > 0) {
          const healAmount = Math.floor(result.damageDealt * (resonanceEffects.lifesteal / 100));
          if (healAmount > 0) {
            setters.setPlayerHp(prev => Math.min(prev + healAmount, playerMaxHp));
            if (playerRef.current) {
              animations.showHealEffect(playerRef.current, healAmount);
            }
          }
        }
        // Cleanse → remove N debuffs from player
        if (resonanceEffects.cleanse && resonanceEffects.cleanse > 0) {
          setters.setPlayerBuffs(prev => removeNDebuffs(prev, resonanceEffects.cleanse!));
        }
        // Heal → heal player flat amount
        if (resonanceEffects.heal && resonanceEffects.heal > 0) {
          setters.setPlayerHp(prev => Math.min(prev + resonanceEffects.heal!, playerMaxHp));
          if (playerRef.current) {
            animations.showHealEffect(playerRef.current, resonanceEffects.heal);
          }
        }
        // Field buff → apply to player
        if (resonanceEffects.fieldBuff) {
          setters.setPlayerBuffs(prev =>
            addOrUpdateBuffDebuff(prev, resonanceEffects.fieldBuff!, 3, 50, 1, false, undefined, 'player')
          );
        }
      }

      // ====================================================================
      // Guard Application
      // ====================================================================

      const totalGuard = (effect.shieldGain ?? 0) + (card.guardAmount ?? 0);
      if (totalGuard > 0) {
        setters.setPlayerGuard((g) => g + totalGuard);
        result.guardGained += totalGuard;
        if (playerRef.current) {
          animations.showShieldEffect(playerRef.current, totalGuard);
        }
      }

      // Sword energy to guard conversion (data-driven via card.convertEnergyToGuard)
      const guardFromEnergy = calculateSwordEnergyGuard(card, swordEnergy.current);
      if (guardFromEnergy > 0) {
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
        setters.setPlayerHp(prev => Math.min(prev + card.healAmount!, playerMaxHp));
        result.healingDone += card.healAmount;
        if (playerRef.current) {
          animations.showHealEffect(playerRef.current, card.healAmount);
        }
      }

      if (effect.hpGain) {
        setters.setPlayerHp(prev => Math.min(prev + effect.hpGain!, playerMaxHp));
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
        setters.setEnemyBuffs(prevBuffs => {
          let newBuffs = prevBuffs;
          effect.enemyDebuffs!.forEach((b) => {
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
          return newBuffs;
        });
      }

      // ====================================================================
      // Buffs to Player
      // ====================================================================

      if (effect.playerBuffs?.length) {
        setters.setPlayerBuffs(prevBuffs => {
          let newBuffs = prevBuffs;
          effect.playerBuffs!.forEach((b) => {
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
          return newBuffs;
        });
      }

      // ====================================================================
      // Update Card and Discard
      // ====================================================================

      const updatedCard = incrementUseCount(card);
      dispatch({ type: "CARD_PLAY", card: updatedCard });

      // ====================================================================
      // Bleed Damage
      // ====================================================================

      const bleedDamage = calculateBleedDamage(playerMaxHp, playerBuffsRef.current);
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
      playerMaxHp,
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
