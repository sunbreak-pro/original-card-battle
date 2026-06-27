import type { JSX } from "react";
import type { PrototypeCard } from "../engine/types";
import {
  computeAttackDamage,
  rangeMultiplier,
  staminaDamageMultiplier,
} from "../engine/combat";
import { RANGE_LABEL } from "../engine/constants";

export interface HandViewProps {
  readonly hand: readonly PrototypeCard[];
  readonly playerStamina: number;
  readonly distanceIndex: number;
  readonly disabled: boolean;
  readonly onPlay: (instanceId: string) => void;
}

export function HandView({
  hand,
  playerStamina,
  distanceIndex,
  disabled,
  onPlay,
}: HandViewProps): JSX.Element {
  return (
    <div className="pb-hand" aria-label="手札">
      {hand.map((card) => {
        const affordable = playerStamina >= card.cost;
        const playable = affordable && !disabled;
        const reason = !affordable ? `気力不足（必要 ${card.cost}）` : "";

        // Predicted damage at the current range and stamina, so the player can
        // read the compatibility/fatigue gradient before committing.
        const effRange = card.effectiveRange;
        let damageLine: string | null = null;
        if (card.type === "attack" && effRange) {
          const predicted = computeAttackDamage(
            card.basePower,
            effRange,
            playerStamina,
            distanceIndex,
          );
          const tags: string[] = [];
          if (rangeMultiplier(distanceIndex, effRange) < 1)
            tags.push("間合い不適");
          if (staminaDamageMultiplier(playerStamina) < 1) tags.push("疲労");
          const note = tags.length > 0 ? tags.join("・") : "最適";
          damageLine = `威力 ${card.basePower} → ${predicted}（${note}）`;
        }

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
              {effRange && (
                <span className="pb-card-range">
                  有効間合い {RANGE_LABEL[effRange]}
                </span>
              )}
              {damageLine && (
                <span className="pb-card-damage">{damageLine}</span>
              )}
              {card.type === "guard" && (
                <span className="pb-card-effect">ガード +{card.guard}</span>
              )}
              {card.shift !== 0 && (
                <span className="pb-card-shift">
                  {card.shift < 0 ? "間合い −1（詰める）" : "間合い +1（退く）"}
                </span>
              )}
            </div>

            <div className="pb-card-desc">{card.description}</div>
            {reason && <div className="pb-card-reason">{reason}</div>}
          </button>
        );
      })}
    </div>
  );
}
