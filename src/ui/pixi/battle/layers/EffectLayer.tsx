import { useCallback, useEffect, useRef, useState } from "react";
import type { Graphics as PixiGraphics } from "pixi.js";
import type { BattlePixiProps } from "../../types/pixiTypes";

interface TestParticle {
  x: number;
  y: number;
  color: number;
  /** 1 → 0 over PARTICLE_LIFETIME_MS, drives alpha + scale. */
  alpha: number;
}

const PARTICLE_LIFETIME_MS = 900;
const PARTICLE_RADIUS = 14;

/**
 * Effect layer. Phase 1 only owns a single self-fading test particle, drawn
 * via `<pixiGraphics>` and animated with `requestAnimationFrame` (no particle
 * library — `@pixi/particle-emitter` is dead on v8, plan §0B-3).
 *
 * The particle is emitted once on mount to prove the GPU overlay paints. Real
 * effect routing (damage/heal/shield) and wiring into `useCardAnimation` is
 * Phase 2 territory and intentionally not done here.
 */
export function EffectLayer(props: BattlePixiProps) {
  const [particle, setParticle] = useState<TestParticle | null>(null);
  const rafRef = useRef<number | null>(null);
  const emittedRef = useRef(false);

  const emit = useCallback((x: number, y: number, color: number) => {
    setParticle({ x, y, color, alpha: 1 });
  }, []);

  // Emit one test particle on first mount (Phase 1 acceptance proof).
  useEffect(() => {
    if (emittedRef.current) return;
    emittedRef.current = true;
    // Centred-ish within the .battle-field overlay region.
    emit(160, 120, 0x66ccff);
  }, [emit]);

  // rAF fade-out loop, scoped to the active particle lifetime.
  useEffect(() => {
    if (!particle) return;
    const start = performance.now();

    const tick = (now: number) => {
      const elapsed = now - start;
      const remaining = 1 - elapsed / PARTICLE_LIFETIME_MS;
      if (remaining <= 0) {
        setParticle(null);
        rafRef.current = null;
        return;
      }
      setParticle((prev) => (prev ? { ...prev, alpha: remaining } : prev));
      rafRef.current = requestAnimationFrame(tick);
    };

    rafRef.current = requestAnimationFrame(tick);
    return () => {
      if (rafRef.current !== null) {
        cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
    };
    // Re-run only when a new particle is emitted, not on every alpha update
    // (alpha changes are intentionally excluded to avoid restarting the loop).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [particle?.x, particle?.y, particle?.color]);

  const draw = useCallback(
    (g: PixiGraphics) => {
      g.clear();
      if (!particle) return;
      g.circle(particle.x, particle.y, PARTICLE_RADIUS * particle.alpha);
      g.fill({ color: particle.color, alpha: particle.alpha });
    },
    [particle],
  );

  // `props` is consumed to keep the Phase 2 effect-routing surface stable
  // (BattleCanvas forwards battle state here). Referenced as a no-op for now.
  void props;

  if (!particle) return <pixiContainer />;
  return (
    <pixiContainer>
      <pixiGraphics draw={draw} />
    </pixiContainer>
  );
}
