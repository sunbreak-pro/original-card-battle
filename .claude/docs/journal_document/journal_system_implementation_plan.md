# Journal System Design Document V3.0

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2026-02-05 | V3.0 | Complete rewrite. Replaced incorrect Lives System content with proper Journal specifications. |

---

## 1. Overview

The Journal (手記) is the player's personal notebook, accessible from the header UI at any time. It provides deck building, encyclopedia, notes, and settings functionality.

> **Note:** The Journal is NOT a BaseCamp facility. It is a header UI element accessible from any screen.

### 1.1 Design Philosophy

| Principle | Description |
|-----------|-------------|
| Always Accessible | One-click access from header, regardless of location |
| Information Hub | Centralized access to player knowledge and configuration |
| Non-Intrusive | Overlay UI that doesn't interrupt gameplay flow |
| Context-Aware | Some features restricted during dungeon exploration |

### 1.2 Access Method

```
┌────────────────────────────────────────────────────────┐
│  Gold: 1250  Souls: 650  Lives: ❤️❤️❤️     📔 [Journal]  │
└────────────────────────────────────────────────────────┘
                                              ↑
                              Click to open Journal overlay
```

---

## 2. Page Structure

```
Journal (手記)
├── Chapter 1「戦術」 — Tactics (Deck Building)
├── Chapter 2「記憶」 — Memories (Encyclopedia)
├── Chapter 3「思考」 — Thoughts (Player Notes)
└── 奥付「設定」     — Colophon (Settings & Save/Load)
```

---

## 3. Chapter Details

### 3.1 Chapter 1: Tactics (戦術) — Deck Building

**Purpose:** View and edit the player's card deck.

**Features:**
- View current deck composition
- Add/remove cards from deck
- Rearrange card order
- View card statistics (mastery level, usage count)
- Filter by class, cost, card type

**Dungeon Restrictions:**
- **VIEW ONLY** — Cannot modify deck during exploration
- Can still browse and plan future changes

**UI Layout:**
```
┌─────────────────────────────────────────────────────────┐
│  Chapter 1: Tactics                      [Deck: 30/40]  │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  [Current Deck]              [Available Cards]          │
│  ┌──────────────┐            ┌──────────────┐          │
│  │ Card 1       │            │ Card A       │          │
│  │ Card 2       │            │ Card B       │          │
│  │ Card 3       │  ←→        │ Card C       │          │
│  │ ...          │            │ ...          │          │
│  └──────────────┘            └──────────────┘          │
│                                                         │
│  [Filter: All ▼] [Sort: Cost ▼]                        │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

### 3.2 Chapter 2: Memories (記憶) — Encyclopedia

**Purpose:** Record and browse discovered information.

**Categories:**
| Category | Content |
|----------|---------|
| Cards | Discovered card details, effects, mastery info |
| Equipment | Found equipment stats, effects, rarity info |
| Monsters | Encountered enemy stats, patterns, weaknesses |
| Events | Discovered dungeon events and outcomes |

**Features:**
- Auto-updates when new content is discovered
- Mastery progress tracking for cards
- Kill count and drop rates for monsters
- Completion percentage per category

**Dungeon Access:**
- **FULL ACCESS** — Can browse encyclopedia at any time
- Real-time updates when discovering new content

**UI Layout:**
```
┌─────────────────────────────────────────────────────────┐
│  Chapter 2: Memories                   [Progress: 45%]  │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  [Cards] [Equipment] [Monsters] [Events]                │
│   ════                                                  │
│                                                         │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐          │
│  │ Card 1 │ │ Card 2 │ │ Card 3 │ │ ???    │          │
│  │ ★★★☆☆ │ │ ★★☆☆☆ │ │ ★☆☆☆☆ │ │        │          │
│  └────────┘ └────────┘ └────────┘ └────────┘          │
│                                                         │
│  Total Discovered: 127/280                              │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

### 3.3 Chapter 3: Thoughts (思考) — Player Notes

**Purpose:** Free-form note-taking area for player strategies and observations.

**Features:**
- Multiple note pages
- Simple text editor
- Auto-save functionality
- Note categories/tags (optional)

**Dungeon Access:**
- **FULL ACCESS** — Can read and write notes at any time

**Use Cases:**
- Boss attack pattern notes
- Build theorycrafting
- Run planning
- Reminders for next exploration

---

### 3.4 Colophon: Settings (設定) — Save/Load & Configuration

**Purpose:** Game settings and save management.

**Settings Categories:**
| Category | Options |
|----------|---------|
| Audio | BGM volume, SFX volume, Voice volume |
| Display | Screen size, Animation speed, Text speed |
| Gameplay | Auto-advance, Confirm prompts, Tutorial hints |
| Accessibility | High contrast, Large text, Button hold duration |

**Save/Load Features:**
- Manual save slots (3-5 slots)
- Auto-save toggle
- Export/Import save data (future)

**Dungeon Restrictions:**
- **SAVE:** Limited (can save progress snapshot)
- **LOAD:** Disabled during exploration (prevents save scumming)

---

## 4. Technical Implementation

### 4.1 Data Structure

```typescript
// src/types/journalTypes.ts

export interface JournalState {
  // Current active page
  activePage: JournalPage;

  // Encyclopedia data
  encyclopedia: EncyclopediaData;

  // Player notes
  notes: PlayerNote[];

  // Settings
  settings: GameSettings;
}

export type JournalPage =
  | 'tactics'
  | 'memories'
  | 'thoughts'
  | 'settings';

export interface EncyclopediaData {
  discoveredCards: Set<string>;
  discoveredEquipment: Set<string>;
  discoveredMonsters: Set<string>;
  discoveredEvents: Set<string>;

  // Detailed stats
  cardMastery: Map<string, MasteryInfo>;
  monsterKills: Map<string, number>;
}

export interface PlayerNote {
  id: string;
  title: string;
  content: string;
  createdAt: Date;
  updatedAt: Date;
  tags?: string[];
}

export interface GameSettings {
  audio: {
    bgmVolume: number;  // 0-100
    sfxVolume: number;
    voiceVolume: number;
  };
  display: {
    animationSpeed: 'slow' | 'normal' | 'fast';
    textSpeed: 'slow' | 'normal' | 'fast';
  };
  gameplay: {
    autoAdvance: boolean;
    confirmPrompts: boolean;
    tutorialHints: boolean;
  };
}
```

### 4.2 Context Integration

```typescript
// JournalContext manages Journal state
// Access via useJournal() hook

interface JournalContextValue {
  journalState: JournalState;

  // Navigation
  openJournal: () => void;
  closeJournal: () => void;
  setActivePage: (page: JournalPage) => void;

  // Encyclopedia
  discoverCard: (cardId: string) => void;
  discoverEquipment: (equipmentId: string) => void;
  discoverMonster: (monsterId: string) => void;
  discoverEvent: (eventId: string) => void;

  // Notes
  addNote: (note: Omit<PlayerNote, 'id' | 'createdAt' | 'updatedAt'>) => void;
  updateNote: (id: string, content: string) => void;
  deleteNote: (id: string) => void;

  // Settings
  updateSettings: (settings: Partial<GameSettings>) => void;

  // Save/Load
  saveGame: (slot: number) => void;
  loadGame: (slot: number) => void;
}
```

### 4.3 Dungeon Restrictions Logic

```typescript
// Determine feature availability based on current screen
function getJournalRestrictions(currentScreen: GameScreen): JournalRestrictions {
  const isDungeon = currentScreen === 'dungeon' || currentScreen === 'battle';

  return {
    tactics: {
      viewDeck: true,       // Always allowed
      editDeck: !isDungeon, // Only at BaseCamp
    },
    memories: {
      browse: true,         // Always allowed
      autoUpdate: true,     // Always allowed
    },
    thoughts: {
      read: true,           // Always allowed
      write: true,          // Always allowed
    },
    settings: {
      changeSettings: true, // Always allowed
      save: true,           // Always allowed (snapshot)
      load: !isDungeon,     // Only at BaseCamp
    },
  };
}
```

---

## 5. UI Components

### 5.1 Component Hierarchy

```
JournalOverlay
├── JournalHeader (close button, page tabs)
├── JournalContent
│   ├── TacticsPage
│   │   ├── DeckEditor
│   │   └── CardBrowser
│   ├── MemoriesPage
│   │   ├── CategoryTabs
│   │   └── EncyclopediaGrid
│   ├── ThoughtsPage
│   │   ├── NoteList
│   │   └── NoteEditor
│   └── SettingsPage
│       ├── SettingsCategories
│       └── SaveLoadPanel
└── JournalFooter (navigation hints)
```

### 5.2 Visual Style

| Element | Style |
|---------|-------|
| Background | Parchment texture, dark edges |
| Typography | Serif font, hand-written feel |
| Icons | Ink-sketch style |
| Animations | Page-turn effect, ink spreading |

---

## 6. Implementation Priority

### Phase 1: Core Structure
- [ ] JournalContext and state management
- [ ] JournalOverlay base component
- [ ] Page navigation
- [ ] Basic Tactics page (deck view only)

### Phase 2: Encyclopedia
- [ ] Encyclopedia data structure
- [ ] Auto-discovery system
- [ ] Category browsing UI
- [ ] Mastery display

### Phase 3: Notes & Settings
- [ ] Note CRUD operations
- [ ] Settings UI
- [ ] Save/Load functionality
- [ ] Dungeon restrictions

### Phase 4: Polish
- [ ] Visual effects
- [ ] Animations
- [ ] Sound effects
- [ ] Accessibility features

---

## 7. Reference Documents

```
camp_facilities_design.md Section 4
├── Journal System overview
└── Dungeon restrictions

game_design_master.md Section 4.1.7
├── Journal role definition
└── Page structure specification
```
