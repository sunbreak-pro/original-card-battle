// The meta screens (docs/ARCH.md): title, 継承の間, 出立, the ending card, and the three overlays
// opened from outside the route (設定・セーブ, 所持カード, 手記). Each mount takes a 1920×1080 host
// inside the shared Stage and returns { destroy() }; the overlays also return close().
import './meta.css';

export { mountTitle } from './title.js';
export { mountHall } from './hall.js';
export { mountDeparture } from './departure.js';
export { mountEnding } from './ending.js';
export { mountSettings } from './settings.js';
export { mountOwnedCards } from './owned.js';
export { mountJournal } from './journal.js';
export { MetaText } from './text.js';
