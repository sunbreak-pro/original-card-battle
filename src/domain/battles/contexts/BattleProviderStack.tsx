// BattleProviderStack (B1.5)
// Composes the three battle contexts from a single orchestrator instance
// The orchestrator remains the internal implementation; contexts provide focused slices

import React, { useMemo, type ReactNode } from "react";
import type { Depth } from '@/types/cardTypes';
import type { EnemyDefinition } from '@/types/characterTypes';
import type { InitialPlayerState } from "../managements/useBattleState";
import { useBattle } from "../managements/battleFlowManage";
import { PlayerBattleProvider } from "./PlayerBattleContext";
import { EnemyBattleProvider } from "./EnemyBattleContext";
import { BattleSessionProvider } from "./BattleSessionContext";
import type {
  PlayerBattleContextValue,
  EnemyBattleContextValue,
  BattleSessionContextValue,
} from "./battleContextTypes";

interface BattleProviderStackProps {
  depth: Depth;
  initialEnemies?: EnemyDefinition[];
  initialPlayerState?: InitialPlayerState;
  children: ReactNode;
}

/**
 * BattleProviderStack
 *
 * Wraps children with three battle contexts:
 * EnemyBattleProvider -> PlayerBattleProvider -> BattleSessionProvider -> children
 *
 * Internally uses the existing useBattleOrchestrator hook and slices its return
 * value into three focused context values.
 */
export const BattleProviderStack: React.FC<BattleProviderStackProps> = ({
  depth,
  initialEnemies,
  initialPlayerState,
  children,
}) => {
  const battle = useBattle(depth, initialEnemies, initialPlayerState);

  // Slice: Player battle context value
  const playerValue = useMemo<PlayerBattleContextValue>(
    () => ({
      playerName: battle.playerName,
      playerClass: battle.playerClass,
      playerRef: battle.playerRef,
      playerHp: battle.playerHp,
      playerMaxHp: battle.playerMaxHp,
      playerAp: battle.playerAp,
      playerMaxAp: battle.playerMaxAp,
      playerGuard: battle.playerGuard,
      playerBuffs: battle.playerBuffs,
      playerEnergy: battle.playerEnergy,
      maxEnergy: battle.maxEnergy,
      swordEnergy: battle.swordEnergy,
      hand: battle.hand,
      drawPile: battle.drawPile,
      discardPile: battle.discardPile,
      isNewCard: battle.isNewCard,
      getDiscardingCards: battle.getDiscardingCards,
      handleCardPlay: battle.handleCardPlay,
      openedPileType: battle.openedPileType,
      openDrawPile: battle.openDrawPile,
      openDiscardPile: battle.openDiscardPile,
      closePileModal: battle.closePileModal,
      playerBattleStats: {
        hp: battle.playerHp,
        maxHp: battle.playerMaxHp,
        ap: battle.playerAp,
        maxAp: battle.playerMaxAp,
        guard: battle.playerGuard,
        buffs: battle.playerBuffs,
        energy: battle.playerEnergy,
        maxEnergy: battle.maxEnergy,
        playerClass: battle.playerClass,
      },
    }),
    [
      battle.playerName,
      battle.playerClass,
      battle.playerRef,
      battle.playerHp,
      battle.playerMaxHp,
      battle.playerAp,
      battle.playerMaxAp,
      battle.playerGuard,
      battle.playerBuffs,
      battle.playerEnergy,
      battle.maxEnergy,
      battle.swordEnergy,
      battle.hand,
      battle.drawPile,
      battle.discardPile,
      battle.isNewCard,
      battle.getDiscardingCards,
      battle.handleCardPlay,
      battle.openedPileType,
      battle.openDrawPile,
      battle.openDiscardPile,
      battle.closePileModal,
    ]
  );

  // Slice: Enemy battle context value
  const enemyValue = useMemo<EnemyBattleContextValue>(
    () => ({
      currentEnemy: battle.aliveEnemies[0] ?? null,
      enemies: battle.aliveEnemies,
      aliveEnemies: battle.aliveEnemies,
      enemyHp: battle.enemyHp ?? 0,
      enemyMaxHp: battle.enemyMaxHp ?? 0,
      enemyAp: battle.enemyAp ?? 0,
      enemyMaxAp: battle.enemyMaxAp ?? 0,
      enemyGuard: battle.enemyGuard ?? 0,
      enemyBuffs: battle.enemyBuffs,
      enemyEnergy: battle.enemyEnergy ?? 0,
      nextEnemyActions: battle.nextEnemyActions ?? [],
      getTargetEnemyRef: battle.getTargetEnemyRef,
      updateEnemy: battle.updateEnemy,
      updateAllEnemies: battle.updateAllEnemies,
      enemyBattleStats: {
        hp: battle.enemyHp ?? 0,
        maxHp: battle.enemyMaxHp ?? 0,
        ap: battle.enemyAp ?? 0,
        maxAp: battle.enemyMaxAp ?? 0,
        guard: battle.enemyGuard ?? 0,
        buffs: battle.enemyBuffs,
        energy: battle.enemyEnergy ?? 0,
      },
    }),
    [
      battle.aliveEnemies,
      battle.enemyHp,
      battle.enemyMaxHp,
      battle.enemyAp,
      battle.enemyMaxAp,
      battle.enemyGuard,
      battle.enemyBuffs,
      battle.enemyEnergy,
      battle.nextEnemyActions,
      battle.getTargetEnemyRef,
      battle.updateEnemy,
      battle.updateAllEnemies,
    ]
  );

  // Slice: Battle session context value
  const sessionValue = useMemo<BattleSessionContextValue>(
    () => ({
      phaseCount: battle.phaseCount,
      isPlayerPhase: battle.isPlayerPhase,
      playerNowSpeed: battle.playerNowSpeed,
      enemyNowSpeed: battle.enemyNowSpeed,
      phaseQueue: battle.phaseQueue,
      currentPhaseIndex: battle.currentPhaseIndex,
      turnMessage: battle.turnMessage,
      showTurnMessage: battle.showTurnMessage,
      handleEndPhase: battle.handleEndPhase,
      initializeBattle: battle.initializeBattle,
      resetForNextEnemy: battle.resetForNextEnemy,
      battleResult: battle.battleResult as "ongoing" | "victory" | "defeat",
      battleStats: battle.battleStats,
    }),
    [
      battle.phaseCount,
      battle.isPlayerPhase,
      battle.playerNowSpeed,
      battle.enemyNowSpeed,
      battle.phaseQueue,
      battle.currentPhaseIndex,
      battle.turnMessage,
      battle.showTurnMessage,
      battle.handleEndPhase,
      battle.initializeBattle,
      battle.resetForNextEnemy,
      battle.battleResult,
      battle.battleStats,
    ]
  );

  return (
    <EnemyBattleProvider value={enemyValue}>
      <PlayerBattleProvider value={playerValue}>
        <BattleSessionProvider value={sessionValue}>
          {children}
        </BattleSessionProvider>
      </PlayerBattleProvider>
    </EnemyBattleProvider>
  );
};
