/**
 * Summon System Hook for Summoner class
 *
 * Wraps the SummonSystem to provide React-friendly state management.
 * Follows the same pattern as useSwordEnergy and useElementalChain.
 */

import { useState, useCallback } from "react";
import type { Card } from '@/types/cardTypes';
import type { SummonState } from '@/types/characterTypes';
import type { DamageModifier } from "../../characters/classAbility/classAbilitySystem";
import { SummonSystem } from "../../characters/player/logic/summonSystem";
import type { ClassAbilityUI, UseClassAbilityReturn } from "./useClassAbility";

// ============================================================================
// Summon Level Helpers
// ============================================================================

function getSummonLevel(state: SummonState): string {
  if (state.activeSummons.length >= state.summonSlots) return "MAX";
  if (state.activeSummons.length >= 2) return "HIGH";
  if (state.activeSummons.length >= 1) return "MID";
  return "NONE";
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useSummonSystem(): UseClassAbilityReturn<SummonState> {
  const [abilityState, setAbilityState] = useState<SummonState>(
    SummonSystem.initialize()
  );

  const onCardPlayed = useCallback((card: Card) => {
    setAbilityState((prev) => SummonSystem.onCardPlay(prev, card));
  }, []);

  const onTurnStart = useCallback(() => {
    setAbilityState((prev) => SummonSystem.onTurnStart(prev));
  }, []);

  const onTurnEnd = useCallback(() => {
    setAbilityState((prev) => SummonSystem.onTurnEnd(prev));
  }, []);

  const getDamageModifier = useCallback(
    (card?: Card): DamageModifier => {
      return SummonSystem.getDamageModifier(abilityState, card);
    },
    [abilityState]
  );

  const canPerformAction = useCallback(
    (actionId: string): boolean => {
      return SummonSystem.canPerformAction(abilityState, actionId);
    },
    [abilityState]
  );

  const getAbilityUI = useCallback((): ClassAbilityUI => {
    return {
      label: "召喚",
      current: abilityState.activeSummons.length,
      max: abilityState.summonSlots,
      level: getSummonLevel(abilityState),
      description: SummonSystem.getStateDescription(abilityState),
    };
  }, [abilityState]);

  const resetAbility = useCallback(() => {
    setAbilityState(SummonSystem.initialize());
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
