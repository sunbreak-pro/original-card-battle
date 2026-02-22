import { Application, extend } from "@pixi/react";
import { Container, Graphics } from "pixi.js";
import { useRef, type ReactNode } from "react";

// Register PixiJS components for @pixi/react JSX usage.
// Phase 1: Container + Graphics only. Phase 3 adds Sprite, Text.
extend({ Container, Graphics });

interface PixiStageProps {
  readonly children: ReactNode;
  readonly className?: string;
}

/**
 * Shared PixiJS stage wrapper.
 * Renders a transparent canvas overlay that auto-resizes to fill its parent.
 * pointer-events: none ensures DOM elements remain interactive.
 */
export function PixiStage({ children, className }: PixiStageProps): ReactNode {
  const containerRef = useRef<HTMLDivElement>(null);

  return (
    <div
      ref={containerRef}
      className={className}
      style={{
        position: "absolute",
        top: 0,
        left: 0,
        width: "100%",
        height: "100%",
        pointerEvents: "none",
        zIndex: 5,
      }}
    >
      <Application
        resizeTo={containerRef}
        backgroundAlpha={0}
        antialias
        resolution={window.devicePixelRatio || 1}
        autoDensity
      >
        {children}
      </Application>
    </div>
  );
}
