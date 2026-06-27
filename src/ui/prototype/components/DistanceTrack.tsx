import type { JSX } from "react";
import { RANGE_LABEL, RANGE_ORDER } from "../engine/constants";

export interface DistanceTrackProps {
  readonly distanceIndex: number;
}

export function DistanceTrack({
  distanceIndex,
}: DistanceTrackProps): JSX.Element {
  return (
    <div className="pb-distance-track" aria-label="間合い">
      {RANGE_ORDER.map((band, i) => (
        <div
          key={band}
          className={`pb-distance-node${i === distanceIndex ? " is-current" : ""}`}
        >
          <span className="pb-distance-label">{RANGE_LABEL[band]}</span>
          {i === distanceIndex && (
            <span className="pb-distance-marker" aria-hidden="true">
              ⚔
            </span>
          )}
        </div>
      ))}
    </div>
  );
}
