// easing functions
export const Easing = {
  linear: (t: number) => t,
  easeInQuad: (t: number) => t * t,
  easeOutQuad: (t: number) => t * (2 - t),
  easeInOutQuad: (t: number) => (t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t),
  easeInCubic: (t: number) => t * t * t,
  easeOutCubic: (t: number) => --t * t * t + 1,
  easeInOutCubic: (t: number) =>
    t < 0.5 ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1,
  easeInQuart: (t: number) => t * t * t * t,
  easeOutQuart: (t: number) => 1 - --t * t * t * t,
  easeInOutQuart: (t: number) =>
    t < 0.5 ? 8 * t * t * t * t : 1 - 8 * --t * t * t * t,
  easeInElastic: (t: number) => {
    const c4 = (2 * Math.PI) / 3;
    return t === 0
      ? 0
      : t === 1
        ? 1
        : -Math.pow(2, 10 * t - 10) * Math.sin((t * 10 - 10.75) * c4);
  },
  easeOutElastic: (t: number) => {
    const c4 = (2 * Math.PI) / 3;
    return t === 0
      ? 0
      : t === 1
        ? 1
        : Math.pow(2, -10 * t) * Math.sin((t * 10 - 0.75) * c4) + 1;
  },
  easeOutBounce: (t: number) => {
    const n1 = 7.5625;
    const d1 = 2.75;
    if (t < 1 / d1) {
      return n1 * t * t;
    } else if (t < 2 / d1) {
      return n1 * (t -= 1.5 / d1) * t + 0.75;
    } else if (t < 2.5 / d1) {
      return n1 * (t -= 2.25 / d1) * t + 0.9375;
    } else {
      return n1 * (t -= 2.625 / d1) * t + 0.984375;
    }
  },
  easeInBack: (t: number) => {
    const c1 = 1.70158;
    const c3 = c1 + 1;
    return c3 * t * t * t - c1 * t * t;
  },
  easeOutBack: (t: number) => {
    const c1 = 1.70158;
    const c3 = c1 + 1;
    return 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2);
  },
};

export interface AnimationProps {
  element: HTMLElement;
  duration: number;
  easing?: (t: number) => number;
  from?: Partial<CSSStyleDeclaration>;
  to: Partial<CSSStyleDeclaration>;
  onProgress?: (progress: number) => void;
  onComplete?: () => void;
}

type CSSWritableProps = {
  [K in keyof CSSStyleDeclaration]:
  CSSStyleDeclaration[K] extends string ? K : never;
}[keyof CSSStyleDeclaration];

export const animate = ({
  element,
  duration,
  easing = Easing.easeOutCubic,
  from = {},
  to,
  onProgress,
  onComplete,
}: AnimationProps) => {
  const startTime = performance.now();

  let rafId: number;
  let cancelled = false;

  const step = (currentTime: number) => {
    if (cancelled) return;

    const elapsed = currentTime - startTime;
    const progress = Math.min(elapsed / duration, 1);
    const easedProgress = easing(progress);

    Object.entries(to).forEach(([key, value]) => {
      const styleKey = key as CSSWritableProps;

      const fromValue = from[styleKey] as string | undefined;
      const interpolated = interpolateValue(
        fromValue || "0",
        value as string,
        easedProgress
      );

      element.style.setProperty(String(styleKey), interpolated);
    });

    onProgress?.(easedProgress);

    if (progress < 1) {
      rafId = requestAnimationFrame(step);
    } else {
      onComplete?.();
    }
  };

  rafId = requestAnimationFrame(step);

  return () => {
    cancelled = true;
    cancelAnimationFrame(rafId);
  };
};

const interpolateValue = (
  from: string,
  to: string,
  progress: number
): string => {
  const fromMatch = from.match(/^(-?\d+\.?\d*)(.*)$/);
  const toMatch = to.match(/^(-?\d+\.?\d*)(.*)$/);

  if (fromMatch && toMatch) {
    const fromNum = parseFloat(fromMatch[1]);
    const toNum = parseFloat(toMatch[1]);
    const unit = toMatch[2];

    const interpolated = fromNum + (toNum - fromNum) * progress;
    return `${interpolated}${unit}`;
  }

  return to;
};
export const animateSequence = async (
  animations: (() => Promise<void>)[]
): Promise<void> => {
  for (const animation of animations) {
    await animation();
  }
};

export const animateParallel = async (
  animations: (() => Promise<void>)[]
): Promise<void> => {
  await Promise.all(animations.map((anim) => anim()));
};

export const animateAsync = (props: AnimationProps): Promise<void> => {
  return new Promise((resolve) => {
    animate({
      ...props,
      onComplete: () => {
        props.onComplete?.();
        resolve();
      },
    });
  });
};

export interface ParticleOptions {
  container: HTMLElement;
  x: number;
  y: number;
  count?: number;
  color?: string;
  size?: number;
  duration?: number;
  spread?: number;
  gravity?: number;
}

export const createParticles = ({
  container,
  x,
  y,
  count = 20,
  color = "#fff",
  size = 4,
  duration = 1000,
  spread = 100,
  gravity = 0.5,
}: ParticleOptions): void => {
  for (let i = 0; i < count; i++) {
    const particle = document.createElement("div");
    particle.style.position = "absolute";
    particle.style.left = `${x}px`;
    particle.style.top = `${y}px`;
    particle.style.width = `${size}px`;
    particle.style.height = `${size}px`;
    particle.style.backgroundColor = color;
    particle.style.borderRadius = "50%";
    particle.style.pointerEvents = "none";
    particle.style.zIndex = "9999";
    particle.style.boxShadow = `0 0 ${size * 2}px ${color}`;

    container.appendChild(particle);

    const angle = (Math.PI * 2 * i) / count;
    const velocity = Math.random() * spread + spread / 2;
    const vx = Math.cos(angle) * velocity;
    const vy = Math.sin(angle) * velocity - spread / 2;

    let currentVy = vy;
    const startTime = performance.now();

    const animateParticle = (currentTime: number) => {
      const elapsed = currentTime - startTime;
      const progress = elapsed / duration;

      if (progress >= 1) {
        particle.remove();
        return;
      }

      currentVy += gravity;

      const newX = x + vx * elapsed * 0.05;
      const newY = y + currentVy * elapsed * 0.05;
      const opacity = 1 - progress;

      particle.style.left = `${newX}px`;
      particle.style.top = `${newY}px`;
      particle.style.opacity = `${opacity}`;

      requestAnimationFrame(animateParticle);
    };

    requestAnimationFrame(animateParticle);
  }
};

export interface DamageTextOptions {
  container: HTMLElement;
  x: number;
  y: number;
  value: number;
  color?: string;
  fontSize?: number;
  duration?: number;
  isCritical?: boolean;
}

export const showDamageText = ({
  container,
  x,
  y,
  value,
  color = "#ff4444",
  fontSize = 32,
  duration = 1500,
  isCritical = false,
}: DamageTextOptions): void => {
  const text = document.createElement("div");
  text.textContent = isCritical ? `${value}!!!` : `${value}`;
  text.style.position = "absolute";
  text.style.left = `${x}px`;
  text.style.top = `${y}px`;
  text.style.fontSize = `${fontSize}px`;
  text.style.fontWeight = "bold";
  text.style.color = color;
  text.style.pointerEvents = "none";
  text.style.zIndex = "9999";
  text.style.textShadow = `0 0 10px ${color}, 0 0 20px ${color}`;
  text.style.fontFamily = "monospace";
  text.style.userSelect = "none";
  text.style.transform = "translate(-50%, -50%)";

  if (isCritical) {
    text.style.fontSize = `${fontSize * 1.5}px`;
    text.style.animation = "shake 0.3s";
  }

  container.appendChild(text);

  const startTime = performance.now();
  const startY = y;

  const animateText = (currentTime: number) => {
    const elapsed = currentTime - startTime;
    const progress = elapsed / duration;

    if (progress >= 1) {
      text.remove();
      return;
    }

    const newY = startY - 100 * progress;
    const opacity = 1 - progress;
    const scale = 1 + progress * 0.5;

    text.style.top = `${newY}px`;
    text.style.opacity = `${opacity}`;
    text.style.transform = `translate(-50%, -50%) scale(${scale})`;

    requestAnimationFrame(animateText);
  };

  requestAnimationFrame(animateText);
};

export const shakeElement = (
  element: HTMLElement,
  intensity: number = 10,
  duration: number = 300
): void => {
  const originalTransform = element.style.transform || "";
  const startTime = performance.now();

  const shake = (currentTime: number) => {
    const elapsed = currentTime - startTime;
    const progress = elapsed / duration;

    if (progress >= 1) {
      element.style.transform = originalTransform;
      return;
    }

    const x = (Math.random() - 0.5) * intensity * (1 - progress);
    const y = (Math.random() - 0.5) * intensity * (1 - progress);

    element.style.transform = `${originalTransform} translate(${x}px, ${y}px)`;

    requestAnimationFrame(shake);
  };

  requestAnimationFrame(shake);
};

export const glowPulse = (
  element: HTMLElement,
  color: string,
  duration: number = 1000,
  intensity: number = 20
): void => {
  const startTime = performance.now();

  const pulse = (currentTime: number) => {
    const elapsed = currentTime - startTime;
    const progress = (elapsed % duration) / duration;

    const glow = Math.sin(progress * Math.PI * 2) * intensity;
    element.style.boxShadow = `0 0 ${glow}px ${color}, 0 0 ${glow * 2}px ${color}`;

    requestAnimationFrame(pulse);
  };

  requestAnimationFrame(pulse);
};
