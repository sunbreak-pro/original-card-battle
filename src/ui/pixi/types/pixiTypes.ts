/**
 * Minimal subset of the DOM-side battle state forwarded into the PixiJS
 * overlay. Phase 1 only needs scalar values for the test particle / future
 * effect routing — richer state (buffs, deck, etc.) is deferred to Phase 2.
 */
export interface BattlePixiProps {
  playerHp: number;
  playerMaxHp: number;
  enemyHp: number;
  enemyMaxHp: number;
  isPlayerPhase: boolean;
  phaseCount: number;
}
