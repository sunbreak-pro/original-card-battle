/**
 * Journal System Types
 *
 * Type definitions for the Journal (手記) overlay system.
 */

/**
 * Available pages in the journal
 */
export type JournalPage = "tactics" | "memories" | "thoughts" | "settings";

/**
 * Categories within the memories (encyclopedia) page
 */
export type MemoriesCategory = "cards" | "enemies" | "equipment" | "events";

/**
 * A single note entry in the thoughts page
 */
export interface JournalNote {
  id: string;
  content: string;
  createdAt: string;
  updatedAt: string;
}

/**
 * Discovery tracking state for encyclopedia entries
 */
export interface DiscoveryState {
  cards: string[]; // Discovered card type IDs
  enemies: string[]; // Discovered enemy IDs
  equipment: string[]; // Discovered equipment IDs
  events: string[]; // Discovered event IDs
}

/**
 * Complete journal state
 */
export interface JournalState {
  isOpen: boolean;
  currentPage: JournalPage;
  memoriesCategory: MemoriesCategory;
  notes: JournalNote[];
  discovery: DiscoveryState;
}

/**
 * Default discovery state
 */
export const DEFAULT_DISCOVERY_STATE: DiscoveryState = {
  cards: [],
  enemies: [],
  equipment: [],
  events: [],
};

/**
 * Default journal state
 */
export const DEFAULT_JOURNAL_STATE: JournalState = {
  isOpen: false,
  currentPage: "tactics",
  memoriesCategory: "cards",
  notes: [],
  discovery: DEFAULT_DISCOVERY_STATE,
};
