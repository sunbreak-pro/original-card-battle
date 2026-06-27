import type { JSX } from "react";
import type { GameResult } from "../engine/types";

export interface ResultOverlayProps {
  readonly result: GameResult;
  readonly onRestart: () => void;
}

export function ResultOverlay({
  result,
  onRestart,
}: ResultOverlayProps): JSX.Element {
  const won = result === "won";
  return (
    <div className="pb-result-overlay" role="dialog" aria-modal="true">
      <div className="pb-result-box">
        <div className={`pb-result-title${won ? " is-won" : " is-lost"}`}>
          {won ? "勝利" : "敗北"}
        </div>
        <button type="button" className="pb-restart" onClick={onRestart}>
          もう一度
        </button>
      </div>
    </div>
  );
}
