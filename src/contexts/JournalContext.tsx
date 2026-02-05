/**
 * Journal Context
 *
 * Provides state management for the Journal (手記) overlay system.
 * Manages journal open/close state, current page, notes, and discovery tracking.
 */

import {
  createContext,
  useContext,
  useState,
  useCallback,
  useEffect,
  type ReactNode,
} from "react";
import type {
  JournalPage,
  MemoriesCategory,
  JournalNote,
  DiscoveryState,
} from "@/types/journalTypes";
import {
  DEFAULT_DISCOVERY_STATE,
  DEFAULT_JOURNAL_STATE,
} from "@/types/journalTypes";

// Storage keys
const STORAGE_KEYS = {
  NOTES: "journal_notes",
  DISCOVERY: "journal_discovery",
} as const;

/**
 * Context value interface
 */
interface JournalContextValue {
  // State
  isOpen: boolean;
  currentPage: JournalPage;
  memoriesCategory: MemoriesCategory;
  notes: JournalNote[];
  discovery: DiscoveryState;

  // Journal open/close
  openJournal: (page?: JournalPage) => void;
  closeJournal: () => void;

  // Page navigation
  setPage: (page: JournalPage) => void;
  setMemoriesCategory: (category: MemoriesCategory) => void;

  // Notes management (Phase 4)
  addNote: (content: string) => void;
  updateNote: (id: string, content: string) => void;
  deleteNote: (id: string) => void;

  // Discovery tracking (Phase 2)
  discoverCard: (cardTypeId: string) => void;
  discoverEnemy: (enemyId: string) => void;
  discoverEquipment: (equipmentId: string) => void;
  discoverEvent: (eventId: string) => void;
  isCardDiscovered: (cardTypeId: string) => boolean;
  isEnemyDiscovered: (enemyId: string) => boolean;
  isEquipmentDiscovered: (equipmentId: string) => boolean;
  isEventDiscovered: (eventId: string) => boolean;
}

// Create the context
const JournalContext = createContext<JournalContextValue | null>(null);

/**
 * Load notes from localStorage
 */
function loadNotes(): JournalNote[] {
  try {
    const stored = localStorage.getItem(STORAGE_KEYS.NOTES);
    if (stored) {
      const parsed = JSON.parse(stored);
      if (Array.isArray(parsed)) {
        return parsed;
      }
    }
  } catch (error) {
    console.error("Failed to load journal notes:", error);
  }
  return [];
}

/**
 * Load discovery state from localStorage
 */
function loadDiscovery(): DiscoveryState {
  try {
    const stored = localStorage.getItem(STORAGE_KEYS.DISCOVERY);
    if (stored) {
      const parsed = JSON.parse(stored);
      return {
        cards: Array.isArray(parsed.cards) ? parsed.cards : [],
        enemies: Array.isArray(parsed.enemies) ? parsed.enemies : [],
        equipment: Array.isArray(parsed.equipment) ? parsed.equipment : [],
        events: Array.isArray(parsed.events) ? parsed.events : [],
      };
    }
  } catch (error) {
    console.error("Failed to load discovery state:", error);
  }
  return DEFAULT_DISCOVERY_STATE;
}

/**
 * Generate unique ID for notes
 */
function generateNoteId(): string {
  return `note_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * Provider props
 */
interface JournalProviderProps {
  children: ReactNode;
}

/**
 * JournalProvider component
 */
export function JournalProvider({ children }: JournalProviderProps) {
  // UI state (not persisted)
  const [isOpen, setIsOpen] = useState(DEFAULT_JOURNAL_STATE.isOpen);
  const [currentPage, setCurrentPage] = useState<JournalPage>(
    DEFAULT_JOURNAL_STATE.currentPage
  );
  const [memoriesCategory, setMemoriesCategoryState] =
    useState<MemoriesCategory>(DEFAULT_JOURNAL_STATE.memoriesCategory);

  // Persisted state
  const [notes, setNotes] = useState<JournalNote[]>(loadNotes);
  const [discovery, setDiscovery] = useState<DiscoveryState>(loadDiscovery);

  // Persist notes to localStorage
  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEYS.NOTES, JSON.stringify(notes));
    } catch (error) {
      console.error("Failed to save journal notes:", error);
    }
  }, [notes]);

  // Persist discovery to localStorage
  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEYS.DISCOVERY, JSON.stringify(discovery));
    } catch (error) {
      console.error("Failed to save discovery state:", error);
    }
  }, [discovery]);

  // Journal open/close
  const openJournal = useCallback((page?: JournalPage) => {
    setIsOpen(true);
    if (page) {
      setCurrentPage(page);
    }
  }, []);

  const closeJournal = useCallback(() => {
    setIsOpen(false);
  }, []);

  // Page navigation
  const setPage = useCallback((page: JournalPage) => {
    setCurrentPage(page);
  }, []);

  const setMemoriesCategory = useCallback((category: MemoriesCategory) => {
    setMemoriesCategoryState(category);
  }, []);

  // Notes management
  const addNote = useCallback((content: string) => {
    const now = new Date().toISOString();
    const newNote: JournalNote = {
      id: generateNoteId(),
      content,
      createdAt: now,
      updatedAt: now,
    };
    setNotes((prev) => [newNote, ...prev]);
  }, []);

  const updateNote = useCallback((id: string, content: string) => {
    setNotes((prev) =>
      prev.map((note) =>
        note.id === id
          ? { ...note, content, updatedAt: new Date().toISOString() }
          : note
      )
    );
  }, []);

  const deleteNote = useCallback((id: string) => {
    setNotes((prev) => prev.filter((note) => note.id !== id));
  }, []);

  // Discovery tracking
  const discoverCard = useCallback((cardTypeId: string) => {
    setDiscovery((prev) => {
      if (prev.cards.includes(cardTypeId)) return prev;
      return { ...prev, cards: [...prev.cards, cardTypeId] };
    });
  }, []);

  const discoverEnemy = useCallback((enemyId: string) => {
    setDiscovery((prev) => {
      if (prev.enemies.includes(enemyId)) return prev;
      return { ...prev, enemies: [...prev.enemies, enemyId] };
    });
  }, []);

  const discoverEquipment = useCallback((equipmentId: string) => {
    setDiscovery((prev) => {
      if (prev.equipment.includes(equipmentId)) return prev;
      return { ...prev, equipment: [...prev.equipment, equipmentId] };
    });
  }, []);

  const discoverEvent = useCallback((eventId: string) => {
    setDiscovery((prev) => {
      if (prev.events.includes(eventId)) return prev;
      return { ...prev, events: [...prev.events, eventId] };
    });
  }, []);

  // Discovery checks
  const isCardDiscovered = useCallback(
    (cardTypeId: string) => discovery.cards.includes(cardTypeId),
    [discovery.cards]
  );

  const isEnemyDiscovered = useCallback(
    (enemyId: string) => discovery.enemies.includes(enemyId),
    [discovery.enemies]
  );

  const isEquipmentDiscovered = useCallback(
    (equipmentId: string) => discovery.equipment.includes(equipmentId),
    [discovery.equipment]
  );

  const isEventDiscovered = useCallback(
    (eventId: string) => discovery.events.includes(eventId),
    [discovery.events]
  );

  const value: JournalContextValue = {
    isOpen,
    currentPage,
    memoriesCategory,
    notes,
    discovery,
    openJournal,
    closeJournal,
    setPage,
    setMemoriesCategory,
    addNote,
    updateNote,
    deleteNote,
    discoverCard,
    discoverEnemy,
    discoverEquipment,
    discoverEvent,
    isCardDiscovered,
    isEnemyDiscovered,
    isEquipmentDiscovered,
    isEventDiscovered,
  };

  return (
    <JournalContext.Provider value={value}>{children}</JournalContext.Provider>
  );
}

/**
 * Hook to use journal context
 */
// eslint-disable-next-line react-refresh/only-export-components
export function useJournal(): JournalContextValue {
  const context = useContext(JournalContext);
  if (!context) {
    throw new Error("useJournal must be used within a JournalProvider");
  }
  return context;
}

export default JournalContext;
