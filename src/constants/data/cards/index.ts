/**
 * Card Data Index
 *
 * Central export for all card definitions.
 */

export { SWORDSMAN_CARDS } from './swordsmanCards';
export { MAGE_CARDS } from './mageCards';
export { COMMON_CARDS } from './commonCards';
export {
  SWORDSMAN_DERIVATIONS,
  MAGE_DERIVATIONS,
  getDerivationsForClass,
  getAllDerivations,
} from './cardDerivationRegistry';
export type { DerivationEntry } from './cardDerivationRegistry';
