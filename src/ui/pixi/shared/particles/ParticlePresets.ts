import { Container, Graphics } from "pixi.js";

/**
 * Particle configuration preset.
 */
export interface ParticlePreset {
  readonly count: number;
  readonly size: number;
  readonly spread: number;
  readonly duration: number;
  readonly gravity: number;
}

const TEST_PARTICLE_PRESET: ParticlePreset = {
  count: 20,
  size: 4,
  spread: 100,
  duration: 1000,
  gravity: 0.5,
};

/**
 * Parse CSS hex color string to PixiJS numeric color.
 */
export function parseColor(cssColor: string): number {
  if (cssColor.startsWith("#")) {
    return parseInt(cssColor.slice(1), 16);
  }
  return 0xffffff;
}

/**
 * Spawn test particles at a position.
 * Uses PixiJS v8 native Graphics (no particle-emitter dependency).
 * Particles radiate outward with gravity and fade out.
 */
export function spawnTestParticles(
  parent: Container,
  x: number,
  y: number,
  cssColor: string,
  onComplete?: () => void,
): void {
  const color = parseColor(cssColor);
  const { count, size, spread, duration, gravity } = TEST_PARTICLE_PRESET;
  let completedCount = 0;

  for (let i = 0; i < count; i++) {
    const particle = new Graphics();
    particle.circle(0, 0, size);
    particle.fill({ color, alpha: 1 });

    particle.x = x;
    particle.y = y;
    parent.addChild(particle);

    const angle = (Math.PI * 2 * i) / count;
    const speed = (Math.random() * 0.5 + 0.5) * spread;
    const vx = Math.cos(angle) * speed;
    const vy = Math.sin(angle) * speed - spread * 0.3;

    const startTime = performance.now();

    const animate = (): void => {
      const elapsed = performance.now() - startTime;
      const progress = Math.min(elapsed / duration, 1);

      if (progress >= 1) {
        parent.removeChild(particle);
        particle.destroy();
        completedCount++;
        if (completedCount >= count) {
          onComplete?.();
        }
        return;
      }

      // Position: velocity * normalized time + gravity
      const t = elapsed / 1000;
      particle.x = x + vx * t;
      particle.y = y + vy * t + 0.5 * gravity * spread * t * t;
      particle.alpha = 1 - progress;
      particle.scale.set(1 - progress * 0.5);

      requestAnimationFrame(animate);
    };

    // Stagger particle start slightly
    setTimeout(() => requestAnimationFrame(animate), i * 10);
  }
}
