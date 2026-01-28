/**
 * UI Constants
 *
 * Centralized UI-related constants including asset paths.
 */

// ============================================================
// Pencil-generated Image Assets
// ============================================================

/** Header image asset paths */
export const HEADER_IMAGES = {
  heart: "/assets/pencil/images/generated-1769314219918.png",
  gold: "/assets/pencil/images/gold-icon.png",
  stoneSmall: "/assets/pencil/images/magic-stone-small.png",
  stoneMedium: "/assets/pencil/images/magic-stone-medium.png",
  stoneLarge: "/assets/pencil/images/magic-stone-large.png",
  stoneHuge: "/assets/pencil/images/magic-stone-huge.png",
};

// ============================================================
// Battle UI Constants
// ============================================================

/** Guard bar maximum display value */
export const GUARD_BAR_DISPLAY_MAX = 30;

// ============================================================
// Animation Constants
// ============================================================

export const CARD_ANIMATION = {
  DRAW_INTERVAL: 150,
  DRAW_DURATION: 800,
  DRAW_START_OFFSET_X: 100,
  DRAW_START_OFFSET_Y: 150,
  DRAW_INITIAL_SCALE: 0.1,
  DRAW_INITIAL_ROTATION: 20,
  DISCARD_INTERVAL: 100,
  DISCARD_DURATION: 500,
  DISCARD_END_OFFSET_X: 100,
  DISCARD_END_OFFSET_Y: 150,
  DISCARD_ROTATION: 25,
  DISCARD_PARTICLE_COUNT: 10,
  DISCARD_PARTICLE_OPACITY: 0.6,
  DISCARD_PARTICLE_SIZE: 3,
  DISCARD_PARTICLE_SPREAD: 50,
  PLAY_DURATION: 400,
  PLAY_FINAL_SCALE: 0.5,
  PLAY_FINAL_ROTATION: 360,
  PLAY_TRAIL_INTERVAL: 50,
  PLAY_PARTICLE_COUNT: 30,
  PLAY_PARTICLE_OPACITY: 0.8,
  PLAY_PARTICLE_SIZE: 5,
  PLAY_PARTICLE_SPREAD: 150,
  PLAY_Z_INDEX: 200,
  DRAW_Z_INDEX: 100,
} as const;

export const DAMAGE_ANIMATION = {
  CRIT_SHAKE_AMPLITUDE: 30,
  NORMAL_SHAKE_AMPLITUDE: 19,
  CRIT_PARTICLE_COUNT: 90,
  NORMAL_PARTICLE_COUNT: 20,
  CRIT_PARTICLE_SIZE: 6,
  NORMAL_PARTICLE_SIZE: 4,
  CRIT_PARTICLE_SPREAD: 200,
  NORMAL_PARTICLE_SPREAD: 100,
  SHAKE_DURATION: 300,
} as const;

export const HEAL_ANIMATION = {
  PARTICLE_COUNT: 20,
  PARTICLE_INTERVAL: 30,
  PARTICLE_DURATION: 1000,
  PARTICLE_SIZE: 8,
  PARTICLE_Y_OFFSET: 50,
  PARTICLE_FINAL_Y: 100,
  GLOW_BLUR: 10,
} as const;

export const SHIELD_ANIMATION = {
  TEXT_FONT_SIZE: 28,
  RING_INITIAL_SIZE: 20,
  RING_BORDER: 3,
  RING_GLOW_BLUR: 20,
  RING_DURATION: 800,
  RING_FINAL_SIZE: 200,
} as const;

export const ENERGY_ANIMATION = {
  CONSUMPTION_TIMEOUT: 600,
} as const;
