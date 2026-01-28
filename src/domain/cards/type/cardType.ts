/**
 * Shim file for backward compatibility with immutable deck files.
 * deck.ts and deckReducter.ts are immutable and import from this location.
 */
export type { Card, Depth, Rarity, CardCategory, CardTag, MasteryLevel, GemLevel, DepthCurveType } from '@/types/cardTypes';
export type { CardCharacterClass } from '@/types/characterTypes';
export type { CardBuffSpec } from '@/types/battleTypes';
export { MAGIC_MULTIPLIERS, MASTERY_THRESHOLDS, MASTERY_BONUSES, CARD_CATEGORY_NAMES, RARITY_COLORS } from '@/constants/cardConstants';
export { isCardForClass, isClassSpecificCard, filterCardsByClass } from '../logic/cardUtils';
