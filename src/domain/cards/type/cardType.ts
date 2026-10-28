/**
 * Shim file for backward compatibility with immutable deck files.
 * deck.ts and deckReducter.ts are immutable and import from this location.
 */
export type { Card, Depth, CardTag, MasteryLevel, GemLevel } from '@/types/cardTypes';
export type { CardCharacterClass } from '@/types/characterTypes';
export type { CardBuffSpec } from '@/types/battleTypes';
export { MASTERY_THRESHOLDS, MASTERY_BONUSES } from '@/constants/cardConstants';
export { isCardForClass } from '../logic/cardUtils';
