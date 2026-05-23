import { Application, extend } from "@pixi/react";
import { Container, Graphics, Sprite, Text } from "pixi.js";
import type { ReactNode, RefObject } from "react";
import { usePixiEventGuard } from "./usePixiApp";

// v8 requirement: register the pixi classes we use as JSX intrinsics
// (<pixiContainer>, <pixiGraphics>, ...). `<Stage>` was removed in v8.
extend({ Container, Graphics, Sprite, Text });

interface PixiStageProps {
  /** The `.battle-field` host element the canvas should resize to. */
  resizeTo: RefObject<HTMLElement | null>;
  children: ReactNode;
}

/**
 * Internal child rendered *inside* `<Application>` so it can read the app via
 * the reconciler context and apply the pointer-event guard exactly once.
 */
function StageEventGuard(): null {
  usePixiEventGuard();
  return null;
}

/**
 * Shared `<Application>` wrapper for every Pixi overlay.
 *
 * - `backgroundAlpha={0}` keeps the canvas transparent so the DOM battle
 *   field shows through (hybrid DOM + GPU rendering).
 * - `resolution` + `autoDensity` must be paired to avoid hi-DPI coord drift.
 * - `preference="webgl"`: WebGPU is flagged production-not-recommended by
 *   PixiJS v8 (plan §0B-2).
 */
export function PixiStage({ resizeTo, children }: PixiStageProps) {
  return (
    <Application
      resizeTo={resizeTo}
      backgroundAlpha={0}
      antialias
      resolution={window.devicePixelRatio}
      autoDensity
      preference="webgl"
    >
      <StageEventGuard />
      {children}
    </Application>
  );
}
