import { useEffect } from "react";
import { useApplication } from "@pixi/react";

/**
 * Typed re-export of @pixi/react's `useApplication`. Must be called from a
 * component rendered *inside* `<Application>` (it reads the app off the
 * reconciler context).
 */
export { useApplication };

/**
 * Single source of truth for the Pixi overlay's pointer-event policy.
 *
 * Rationale (plan §2.6 / §0B-5): CSS `pointer-events:none` on the canvas is
 * not enough — @pixi/react v8 still attaches a global `pointermove` listener
 * during the document capture phase, which can swallow hover events meant for
 * the underlying DOM battle UI. Phase 1 has zero hover-driven Pixi behaviour,
 * so we hard-disable the renderer's `move` feature here. Phase 2 (hover FX)
 * should flip this to per-display-object `eventMode` rather than re-enabling
 * the global feature.
 *
 * This is intentionally the ONLY place this flag is set, so the plan's
 * "decide in one location" requirement is satisfied.
 */
export function usePixiEventGuard(): void {
  const { app } = useApplication();

  useEffect(() => {
    // `app.renderer` is undefined until the renderer has initialised; the
    // effect re-runs when the app instance changes, so guard defensively.
    const renderer = app?.renderer;
    if (!renderer) return;
    // Intentional imperative PixiJS renderer configuration (not React state):
    // `events.features.move` is the documented v8 API to disable the global
    // pointermove listener. The immutability lint rule treats any write
    // reachable from a hook value as accidental mutation, which does not
    // apply to configuring an external imperative subsystem.
    // eslint-disable-next-line react-hooks/immutability
    renderer.events.features.move = false;
  }, [app]);
}
