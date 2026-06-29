// DOM overlay — hand card list.
// Receives pre-derived CardView[] from viewModel so no game logic lives here.

import type { JSX } from "react";
import type { CardView } from "@/ui/battle-lab/core/viewModel";

interface HandOverlayProps {
  readonly hand: readonly CardView[];
  readonly isOver: boolean;
  readonly onPlay: (instanceId: string) => void;
}

export function HandOverlay({
  hand,
  isOver,
  onPlay,
}: HandOverlayProps): JSX.Element {
  return (
    <div className="pb-hand" aria-label="手札">
      {hand.map((card) => {
        const playable = card.playable && !isOver;
        return (
          <button
            key={card.instanceId}
            type="button"
            className={`pb-card pb-card--${card.type}${playable ? "" : " is-disabled"}`}
            disabled={!playable}
            onClick={() => onPlay(card.instanceId)}
          >
            <div className="pb-card-head">
              <span className="pb-card-name">{card.name}</span>
              <span className="pb-card-cost">気力 {card.cost}</span>
            </div>

            <div className="pb-card-body">
              {card.effectiveRangeLabel && (
                <span className="pb-card-range">
                  有効間合い {card.effectiveRangeLabel}
                </span>
              )}
              {card.damage && (
                <span className="pb-card-damage">{card.damage.text}</span>
              )}
              {card.type === "guard" && card.guard > 0 && (
                <span className="pb-card-effect">ガード +{card.guard}</span>
              )}
              {card.shiftLabel && (
                <span className="pb-card-shift">{card.shiftLabel}</span>
              )}
            </div>

            <div className="pb-card-desc">{card.description}</div>
            {card.disabledReason && (
              <div className="pb-card-reason">{card.disabledReason}</div>
            )}
          </button>
        );
      })}
    </div>
  );
}
