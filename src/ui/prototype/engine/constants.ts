// Tuning constants — the single source of truth for prototype numbers.

import type { RangeBand } from "./types";

export const MAX_STAMINA = 20;

/** Stamina regained at turn start, keyed by the current range band. */
export const STAMINA_RECOVERY: Readonly<Record<RangeBand, number>> = {
  close: 1,
  mid: 2,
  far: 3,
};

/** At or above this stamina, attacks deal full damage. Below it, damage decays. */
export const FATIGUE_THRESHOLD = 8;
export const FATIGUE_FLOOR_MULT = 0.4;

/** Damage multiplier keyed by |distance - effectiveRange| (clamped to last). */
export const RANGE_MULT = [1.0, 0.5, 0.15] as const;

/** Distance bands ordered by index: close=0, mid=1, far=2. */
export const RANGE_ORDER = [
  "close",
  "mid",
  "far",
] as const satisfies readonly RangeBand[];
export const MID_INDEX = 1;

/** Japanese labels for UI/log rendering. */
export const RANGE_LABEL: Readonly<Record<RangeBand, string>> = {
  close: "近",
  mid: "中",
  far: "遠",
};

export const PLAYER_MAX_HP = 30;
export const ENEMY_MAX_HP = 38;
export const HAND_SIZE = 3;
export const INITIAL_DISTANCE_INDEX = MID_INDEX;
