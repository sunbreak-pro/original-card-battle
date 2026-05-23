import { lazy, Suspense, useEffect, useRef, useState } from "react";
import type { BattlePixiProps } from "../types/pixiTypes";

// Step 0-A (plan §2.3): the whole app is wrapped in <StrictMode> (main.tsx).
// @pixi/react v8 crashes under StrictMode's double mount because it grabs a
// stale WebGL context (open issue pixijs/pixi-react#602). We keep global
// StrictMode and instead isolate the Pixi subtree:
//
//  1. The <Application> tree is code-split via React.lazy so it is never part
//     of the synchronous StrictMode double-render.
//  2. A useRef/useState mount gate (`ready`) defers rendering <PixiStage>
//     until *after* the first committed mount effect. StrictMode simulates an
//     unmount+remount before the first real commit; by only flipping `ready`
//     in a post-commit effect (and never back), the <Application> mounts a
//     single time against a fresh WebGL context.
const PixiStage = lazy(() =>
  import("../core/PixiStage").then((m) => ({ default: m.PixiStage })),
);
const BackgroundLayer = lazy(() =>
  import("./layers/BackgroundLayer").then((m) => ({
    default: m.BackgroundLayer,
  })),
);
const CharacterLayer = lazy(() =>
  import("./layers/CharacterLayer").then((m) => ({
    default: m.CharacterLayer,
  })),
);
const EffectLayer = lazy(() =>
  import("./layers/EffectLayer").then((m) => ({ default: m.EffectLayer })),
);

export function BattleCanvas(props: BattlePixiProps) {
  const hostRef = useRef<HTMLDivElement>(null);
  const mountedRef = useRef(false);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    // Runs only on the real (post-StrictMode-simulation) commit. The ref
    // guard makes this idempotent even if the effect is re-invoked.
    if (mountedRef.current) return;
    mountedRef.current = true;
    // Deliberate one-shot post-commit gate (Step 0-A / issue #602): the
    // <Application> must mount exactly once, *after* StrictMode's simulated
    // unmount+remount. The ref makes this idempotent and it never re-runs,
    // so the cascading-render concern this rule guards against does not apply.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setReady(true);
  }, []);

  return (
    <div ref={hostRef} className="battle-pixi-host" aria-hidden>
      {ready && (
        <Suspense fallback={null}>
          <PixiStage resizeTo={hostRef}>
            <pixiContainer>
              <BackgroundLayer />
              <CharacterLayer />
              <EffectLayer {...props} />
            </pixiContainer>
          </PixiStage>
        </Suspense>
      )}
    </div>
  );
}
