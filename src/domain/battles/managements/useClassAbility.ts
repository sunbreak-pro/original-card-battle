/**
 * Class Ability Hook
 *
 * Generic hook for managing character class abilities:
 * - Swordsman: Sword Energy (剣気)
 * - Mage: Elemental Chain (属性連鎖) - Future
 * - Summoner: Summon System (召喚) - Future
 *
 * This hook wraps the ClassAbilitySystem interface and provides
 * React-friendly state management and event handling.
 */

import { useState, useCallback } from "react";
import type { Card } from '@/types/cardTypes';
import type {
  ClassAbilityState,
  SwordEnergyState,
} from '@/types/characterTypes';
import type {
  ClassAbilitySystem,
  DamageModifier,
} from "../../characters/classAbility/classAbilitySystem";
import { DEFAULT_DAMAGE_MODIFIER } from "../../characters/classAbility/classAbilitySystem";

// Sword Energy System
import {
  SwordEnergySystem,
  createInitialSwordEnergy,
} from "../../characters/player/logic/swordEnergySystem";

// Elemental Chain System
import { useElementalChain } from "./useElementalChain";

// Summon System
import { useSummonSystem } from "./useSummonSystem";

// ============================================================================
// Types
// ============================================================================

/**
 * UI representation of class ability state
 */
export interface ClassAbilityUI {
  /** Display label (e.g., "剣気") */
  label: string;
  /** Current value */
  current: number;
  /** Maximum value */
  max: number;
  /** Level indicator (e.g., "MAX", "HIGH", "MID", "LOW") */
  level: string;
  /** Description of current state */
  description: string;
}

/**
 * Return type for useClassAbility hook
 */
export interface UseClassAbilityReturn<T extends ClassAbilityState> {
  /** Current ability state */
  abilityState: T;

  /** Set ability state directly */
  setAbilityState: React.Dispatch<React.SetStateAction<T>>;

  /** Handle card play event */
  onCardPlayed: (card: Card) => void;

  /** Handle turn start event */
  onTurnStart: () => void;

  /** Handle turn end event */
  onTurnEnd: () => void;

  /** Get damage modifier for current state */
  getDamageModifier: (card?: Card) => DamageModifier;

  /** Check if an action can be performed */
  canPerformAction: (actionId: string) => boolean;

  /** Get UI representation of current state */
  getAbilityUI: () => ClassAbilityUI;

  /** Reset ability to initial state */
  resetAbility: () => void;
}

// ============================================================================
// Sword Energy Level Helpers
// ============================================================================

function getSwordEnergyLevel(current: number, max: number): string {
  if (current >= max) return "MAX";
  if (current >= 8) return "HIGH";
  if (current >= 5) return "MID";
  if (current >= 3) return "LOW";
  return "NONE";
}

function getSwordEnergyLabel(): string {
  return "剣気";
}

// ============================================================================
// Hook Implementation - Sword Energy
// ============================================================================

/**
 * Sword Energy ability hook for Swordsman class
 */
export function useSwordEnergy(): UseClassAbilityReturn<SwordEnergyState> {
  const [abilityState, setAbilityState] = useState<SwordEnergyState>(
    createInitialSwordEnergy()
  );

  const onCardPlayed = useCallback((card: Card) => {
    setAbilityState((prev) => SwordEnergySystem.onCardPlay(prev, card));
  }, []);

  const onTurnStart = useCallback(() => {
    setAbilityState((prev) => SwordEnergySystem.onTurnStart(prev));
  }, []);

  const onTurnEnd = useCallback(() => {
    setAbilityState((prev) => SwordEnergySystem.onTurnEnd(prev));
  }, []);

  const getDamageModifier = useCallback(
    (card?: Card): DamageModifier => {
      return SwordEnergySystem.getDamageModifier(abilityState, card);
    },
    [abilityState]
  );

  const canPerformAction = useCallback(
    (actionId: string): boolean => {
      return SwordEnergySystem.canPerformAction(abilityState, actionId);
    },
    [abilityState]
  );

  const getAbilityUI = useCallback((): ClassAbilityUI => {
    return {
      label: getSwordEnergyLabel(),
      current: abilityState.current,
      max: abilityState.max,
      level: getSwordEnergyLevel(abilityState.current, abilityState.max),
      description: SwordEnergySystem.getStateDescription(abilityState),
    };
  }, [abilityState]);

  const resetAbility = useCallback(() => {
    setAbilityState(createInitialSwordEnergy());
  }, []);

  return {
    abilityState,
    setAbilityState,
    onCardPlayed,
    onTurnStart,
    onTurnEnd,
    getDamageModifier,
    canPerformAction,
    getAbilityUI,
    resetAbility,
  };
}

// ============================================================================
// Generic Hook Implementation
// ============================================================================

/**
 * Generic class ability hook
 *
 * Use this hook when you need to work with any class ability system.
 * For specific classes, prefer the typed hooks (useSwordEnergy, etc.)
 *
 * @param system - The class ability system implementation
 * @param getLabel - Function to get the display label
 * @param getLevel - Function to get the level indicator
 */
export function useClassAbility<T extends ClassAbilityState>(
  system: ClassAbilitySystem<T>,
  getLabel: () => string,
  getLevel: (state: T) => string
): UseClassAbilityReturn<T> {
  const [abilityState, setAbilityState] = useState<T>(system.initialize());

  const onCardPlayed = useCallback(
    (card: Card) => {
      setAbilityState((prev) => system.onCardPlay(prev, card));
    },
    [system]
  );

  const onTurnStart = useCallback(() => {
    setAbilityState((prev) => system.onTurnStart(prev));
  }, [system]);

  const onTurnEnd = useCallback(() => {
    setAbilityState((prev) => system.onTurnEnd(prev));
  }, [system]);

  const getDamageModifier = useCallback(
    (card?: Card): DamageModifier => {
      return system.getDamageModifier(abilityState, card);
    },
    [system, abilityState]
  );

  const canPerformAction = useCallback(
    (actionId: string): boolean => {
      return system.canPerformAction(abilityState, actionId);
    },
    [system, abilityState]
  );

  const getAbilityUI = useCallback((): ClassAbilityUI => {
    // Type guard for states with current/max
    const state = abilityState as unknown as { current?: number; max?: number };
    return {
      label: getLabel(),
      current: state.current ?? 0,
      max: state.max ?? 0,
      level: getLevel(abilityState),
      description: system.getStateDescription(abilityState),
    };
  }, [abilityState, getLabel, getLevel, system]);

  const resetAbility = useCallback(() => {
    setAbilityState(system.initialize());
  }, [system]);

  return {
    abilityState,
    setAbilityState,
    onCardPlayed,
    onTurnStart,
    onTurnEnd,
    getDamageModifier,
    canPerformAction,
    getAbilityUI,
    resetAbility,
  };
}

// ============================================================================
// Factory Function
// ============================================================================

/**
 * Create appropriate class ability hook based on character class
 *
 * Note: Currently only Swordsman is fully implemented.
 * Mage and Summoner hooks will be added when those systems are implemented.
 */
export function createClassAbilityHook(
  characterClass: "swordsman" | "mage" | "summoner"
): () => UseClassAbilityReturn<ClassAbilityState> {
  switch (characterClass) {
    case "swordsman":
      return useSwordEnergy as () => UseClassAbilityReturn<ClassAbilityState>;
    case "mage":
      return useElementalChain as () => UseClassAbilityReturn<ClassAbilityState>;
    case "summoner":
      return useSummonSystem as () => UseClassAbilityReturn<ClassAbilityState>;
    default:
      return useSwordEnergy as () => UseClassAbilityReturn<ClassAbilityState>;
  }
}

// ============================================================================
// Re-exports
// ============================================================================

export { DEFAULT_DAMAGE_MODIFIER };
export type { DamageModifier };
