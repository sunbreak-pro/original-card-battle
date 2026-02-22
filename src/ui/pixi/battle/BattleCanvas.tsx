import type { ReactNode } from "react";
import { PixiStage } from "../core/PixiStage";
import { BackgroundLayer } from "./layers/BackgroundLayer";
import { CharacterLayer } from "./layers/CharacterLayer";
import { EffectLayer } from "./layers/EffectLayer";
import type { BattlePixiState, PixiEffectCommand } from "../types/pixiTypes";

interface BattleCanvasProps {
  readonly battleState: BattlePixiState;
  readonly effectQueue: readonly PixiEffectCommand[];
  readonly onEffectComplete?: (index: number) => void;
}

/**
 * PixiJS canvas overlay for battle screen.
 * Renders Background, Character, and Effect layers as transparent overlay.
 * Phase 1: Only EffectLayer has content (test particles).
 */
export function BattleCanvas({
  battleState: _battleState,
  effectQueue,
  onEffectComplete,
}: BattleCanvasProps): ReactNode {
  // battleState is available for Phase 2+ effect triggers
  return (
    <PixiStage className="battle-canvas">
      <BackgroundLayer />
      <CharacterLayer />
      <EffectLayer
        effectQueue={effectQueue}
        onEffectComplete={onEffectComplete}
      />
    </PixiStage>
  );
}
