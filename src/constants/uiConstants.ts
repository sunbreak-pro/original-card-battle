/**
 * UI Constants
 *
 * Centralized UI-related constants including asset paths.
 */

import type { ElementType, CharacterClass } from '@/types/characterTypes';

// ============================================================
// Player Character Image Assets
// ============================================================

export const PLAYER_CHARACTER_IMAGES: Record<CharacterClass, string> = {
  swordsman: "/assets/images/player-character/Swordman.png",
  mage: "/assets/images/player-character/Mage.png",
  summoner: "/assets/images/player-character/Summoner.png",
};

// ============================================================
// Generated Icon Assets
// ============================================================

/** Header image asset paths */
export const HEADER_ICONS = {
  heart: "/assets/images/icons/heart-icon.png",
  gold: "/assets/images/icons/gold-icon.png",
  stoneSmall: "/assets/images/icons/magic-stone-small.png",
  stoneMedium: "/assets/images/icons/magic-stone-medium.png",
  stoneLarge: "/assets/images/icons/magic-stone-large.png",
  stoneHuge: "/assets/images/icons/magic-stone-huge.png",
};

export const ELEMENT_MAGIC_ICONS: Partial<Record<ElementType, string>> = {
  fire: "/assets/images/elements/element-fire.png",
  ice: "/assets/images/elements/element-ice.png",
  lightning: "/assets/images/elements/element-lightning.png",
  dark: "/assets/images/elements/element-dark.png",
  light: "/assets/images/elements/element-light.png",
};


export const ELEMENT_UTILITY_ICONS: Partial<Record<ElementType, string>> = {
  physics: "/assets/images/elements/element-physics.png",
  buff: "/assets/images/elements/element-buff.png",
  guard: "/assets/images/elements/element-guard.png",
  heal: "/assets/images/elements/element-heal.png",
};

// ============================================================
// Background Image Assets
// ============================================================

export const DEPTH_BACKGROUND_IMAGES: Record<number, string> = {
  1: "/assets/images/depth-backgrounds/depth_1_background.png",
  2: "/assets/images/depth-backgrounds/depth_2_background.png",
  3: "/assets/images/depth-backgrounds/depth_3_background.png",
  4: "/assets/images/depth-backgrounds/depth_4_background.png",
  5: "/assets/images/depth-backgrounds/depth_4_background.png",
};

// ============================================================
// Element Icon Helper
// ============================================================

/** Get element icon path by ElementType. Returns undefined for elements without icons (summon, etc.) */
export function getElementIcon(element: ElementType): string | undefined {
  return ELEMENT_MAGIC_ICONS[element] ?? ELEMENT_UTILITY_ICONS[element];
}

// ============================================================
// Deck Limit Constants
// ============================================================

/** Maximum copies of a single card in a deck */
export const MAX_CARD_COPIES = 3;

/** Minimum deck size to start exploration */
export const MIN_DECK_SIZE = 15;

/** Maximum deck size */
export const MAX_DECK_SIZE = 30;

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
  TEXT_DURATION: 3000,
  CRIT_COLOR: "#ffd700",
  NORMAL_COLOR: "#ff4444",
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
