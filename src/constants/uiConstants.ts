/**
 * UI Constants
 *
 * Centralized UI-related constants including asset paths.
 */

// ============================================================
// Generated Icon Assets
// ============================================================

/** Header image asset paths */
export const HEADER_ICONS = {
  heart: "../../public/assets/images/icons/heart-icon.png",
  gold: "../../public/assets/images/icons/gold-icon.png",
  stoneSmall: "../../public/assets/images/icons/magic-stone-small.png",
  stoneMedium: "../../public/assets/images/icons/magic-stone-medium.png",
  stoneLarge: "../../public/assets/images/icons/magic-stone-large.png",
  stoneHuge: "../../public/assets/images/icons/magic-stone-huge.png",
};

export const ELEMENT_MAGIC_ICONS = {
  fire: "../../public/assets/images/elements/element-fire.png",
  ice: "../../public/assets/images/elements/element-ice.png",
  lightning: "../../public/assets/images/elements/element-lightning.png",
  dark: "../../public/assets/images/elements/element-dark.png",
  light: "../../public/assets/images/elements/element-light.png",
};
export const ELEMENT_PHYSICAL_ICONS = {
  slash: "../../public/assets/images/elements/element-slash.png",
  impact: "../../public/assets/images/elements/element-impact.png",
  guard: "../../public/assets/images/elements/element-guard.png",
};

// ============================================================
// Background Image Assets
// ============================================================

export const DEPTH_BACKGROUND_IMAGES = {
  1: "../../public/assets/images/depth-backgrounds/depth_1_background.png",
  2: "../../public/assets/images/depth-backgrounds/depth_2_background.png",
  3: "../../public/assets/images/depth-backgrounds/depth_3_background.png",
  4: "../../public/assets/images/depth-backgrounds/depth_4_background.png",
  5: "../../public/assets/images/depth-backgrounds/depth_5_background.png",
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
