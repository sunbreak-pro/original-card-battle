import { useState, useCallback } from 'react';
import {
  attemptEscape,
  calculateEscapeChance,
} from '@/domain/battles/logic/escapeLogic';

type EscapePhase = 'attempting' | 'success' | 'failure' | null;

interface UseEscapeLogicParams {
  playerSpeed: number;
  avgEnemySpeed: number;
  isBossBattle: boolean;
  onEscapeSuccess: () => void;
  onEscapeFailure: () => void;
}

interface UseEscapeLogicReturn {
  escapePhase: EscapePhase;
  escapeChance: number;
  handleEscape: () => void;
}

/**
 * Hook for managing escape logic in battle
 */
export function useEscapeLogic({
  playerSpeed,
  avgEnemySpeed,
  isBossBattle,
  onEscapeSuccess,
  onEscapeFailure,
}: UseEscapeLogicParams): UseEscapeLogicReturn {
  const [escapePhase, setEscapePhase] = useState<EscapePhase>(null);

  const escapeChance = calculateEscapeChance(playerSpeed, avgEnemySpeed);

  const handleEscape = useCallback(() => {
    if (isBossBattle || escapePhase !== null) return;

    setEscapePhase('attempting');

    const success = attemptEscape(playerSpeed, avgEnemySpeed);

    setTimeout(() => {
      if (success) {
        setEscapePhase('success');
        setTimeout(() => {
          setEscapePhase(null);
          onEscapeSuccess();
        }, 1500);
      } else {
        setEscapePhase('failure');
        setTimeout(() => {
          setEscapePhase(null);
          onEscapeFailure();
        }, 1500);
      }
    }, 1200);
  }, [
    isBossBattle,
    escapePhase,
    playerSpeed,
    avgEnemySpeed,
    onEscapeSuccess,
    onEscapeFailure,
  ]);

  return {
    escapePhase,
    escapeChance,
    handleEscape,
  };
}
