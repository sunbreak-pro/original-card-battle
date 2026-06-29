// PixiJS bakeoff — top-level container.
//
// Layout composition (see BattleField.tsx for the Pixi side):
//   Pixi <canvas> (BattleField): distance track, combatant HP/stamina bars,
//                                damage-number pop FX
//   DOM overlay:                 hand cards, end-turn button, battle log,
//                                result modal
//
// BattleField is loaded via React.lazy so <Application> never participates
// in StrictMode's synchronous double-render (issue #602).

import type { JSX } from "react";
import { lazy, Suspense, useReducer } from "react";
import { battleReducer, initState } from "@/ui/battle-lab/core/battleReducer";
import { describeHand, isBattleOver } from "@/ui/battle-lab/core/viewModel";
import { HandOverlay } from "./components/HandOverlay";
import { LogOverlay } from "./components/LogOverlay";
import { ResultOverlay } from "./components/ResultOverlay";
import "./pixi-bakeoff.css";

const BattleField = lazy(() =>
  import("./components/BattleField").then((m) => ({ default: m.BattleField })),
);

export function PixiBattle(): JSX.Element {
  const [state, dispatch] = useReducer(battleReducer, undefined, initState);
  const isOver = isBattleOver(state.result);
  const hand = describeHand(state);

  return (
    <div className="pixi-bakeoff">
      <header className="pb-header">
        <h1 className="pb-title">間合い × スタミナ（PixiJS版）</h1>
        <span className="pb-turn">ターン {state.turn}</span>
      </header>

      {/* Pixi canvas: distance track, HP/stamina bars, damage-pop FX */}
      <Suspense fallback={<div className="pb-field-host" />}>
        <BattleField state={state} />
      </Suspense>

      {/* DOM: hand cards */}
      <HandOverlay
        hand={hand}
        isOver={isOver}
        onPlay={(instanceId) => dispatch({ type: "PLAY_CARD", instanceId })}
      />

      {/* DOM: end-turn control */}
      <div className="pb-controls">
        <button
          type="button"
          className="pb-end-turn"
          disabled={isOver}
          onClick={() => dispatch({ type: "END_TURN" })}
        >
          ターン終了
        </button>
      </div>

      {/* DOM: battle log */}
      <LogOverlay log={state.log} />

      {/* DOM: result modal */}
      {isOver && (
        <ResultOverlay
          result={state.result}
          onRestart={() => dispatch({ type: "RESTART" })}
        />
      )}
    </div>
  );
}
