/**
 * Elemental Chain Hook for Mage class
 *
 * Wraps the ElementalSystem to provide React-friendly state management.
 * Follows the same pattern as useSwordEnergy.
 */

import { useState, useCallback } from "react";
import type { Card } from '@/types/cardTypes';
import type { ElementalState } from '@/types/characterTypes';
import type { DamageModifier } from "../../characters/classAbility/classAbilitySystem";
import { ElementalSystem } from "../../characters/player/logic/elementalSystem";
import type { ClassAbilityUI, UseClassAbilityReturn } from "./useClassAbility";

// ============================================================================
// Elemental Chain Level Helpers
// ============================================================================

function getElementalLevel(state: ElementalState): string {
  if (state.resonanceLevel === 2) return "MAX";
  if (state.resonanceLevel === 1) return "MID";
  return "NONE";
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useElementalChain(): UseClassAbilityReturn<ElementalState> {
  const [abilityState, setAbilityState] = useState<ElementalState>(
    ElementalSystem.initialize()
  );

  const onCardPlayed = useCallback((card: Card) => {
    setAbilityState((prev) => ElementalSystem.onCardPlay(prev, card));
  }, []);

  const onTurnStart = useCallback(() => {
    setAbilityState((prev) => ElementalSystem.onTurnStart(prev));
  }, []);

  const onTurnEnd = useCallback(() => {
    setAbilityState((prev) => ElementalSystem.onTurnEnd(prev));
  }, []);

  const getDamageModifier = useCallback(
    (card?: Card): DamageModifier => {
      return ElementalSystem.getDamageModifier(abilityState, card);
    },
    [abilityState]
  );

  const canPerformAction = useCallback(
    (actionId: string): boolean => {
      return ElementalSystem.canPerformAction(abilityState, actionId);
    },
    [abilityState]
  );

  const getAbilityUI = useCallback((): ClassAbilityUI => {
    return {
      label: "共鳴",
      current: abilityState.resonanceLevel,
      max: 2,
      level: getElementalLevel(abilityState),
      description: ElementalSystem.getStateDescription(abilityState),
    };
  }, [abilityState]);

  const resetAbility = useCallback(() => {
    setAbilityState(ElementalSystem.initialize());
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
