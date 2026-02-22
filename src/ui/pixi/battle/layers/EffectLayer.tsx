import { useEffect, useRef, type ReactNode } from "react";
import type { Container } from "pixi.js";
import { PIXI_EFFECT_TYPE } from "../../types/pixiTypes";
import type { PixiEffectCommand } from "../../types/pixiTypes";
import { spawnTestParticles } from "../../shared/particles/ParticlePresets";

interface EffectLayerProps {
  readonly effectQueue: readonly PixiEffectCommand[];
  readonly onEffectComplete?: (index: number) => void;
}

/**
 * Effect layer for particle and visual effects.
 * Phase 1: Renders test particles only.
 * Phase 2: Damage numbers, heal effects, shield effects, etc.
 */
export function EffectLayer({
  effectQueue,
  onEffectComplete,
}: EffectLayerProps): ReactNode {
  const containerRef = useRef<Container>(null);
  const processedRef = useRef(0);

  // Process new effect commands
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    // Process only new commands since last render
    for (let i = processedRef.current; i < effectQueue.length; i++) {
      const effect = effectQueue[i];
      if (effect.type === PIXI_EFFECT_TYPE.TEST_PARTICLE) {
        const idx = i;
        spawnTestParticles(
          container,
          effect.x,
          effect.y,
          effect.color ?? "#ffffff",
          () => onEffectComplete?.(idx),
        );
      }
    }
    processedRef.current = effectQueue.length;
  }, [effectQueue, onEffectComplete]);

  // Reset counter when queue is cleared
  useEffect(() => {
    if (effectQueue.length === 0) {
      processedRef.current = 0;
    }
  }, [effectQueue.length]);

  return <pixiContainer ref={containerRef} label="effect-layer" />;
}
