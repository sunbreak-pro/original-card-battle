Here is the English translation of the **Dungeon Exploration UI Design Document v2.1**.

To help you visualize the structural concepts and UI layouts described in this design, I have inserted specific image tags where diagrams would be most instructive.

---

# Dungeon Exploration UI Design Document v2.1

## Revision History

| Version | Date       | Changes                                                                |
| ------- | ---------- | ---------------------------------------------------------------------- |
| v2.0    | -          | Initial creation                                                       |
| v2.1    | 2026-01-10 | Resolved contradictions, finalized specifications, unified terminology |

---

## 1. Overview

### 1.1 Concept

**"The Descent"** - Expression of psychological pressure via an hourglass-shaped map.

- **Visual experience of being cornered:** The deeper you dive, the narrower the path becomes, creating mental pressure.
- **Strategic judgment:** The **Exploration Count Limit** forces players to decide "what must be achieved in this run."
- **Tension of return:** The risk of returning creates a tense decision on "when to turn back."

### 1.2 Terminology Definition

| Term        | Definition                         | Notes                                |
| ----------- | ---------------------------------- | ------------------------------------ |
| Node / Room | A single point on the map          | Used interchangeably                 |
| Depth       | Dungeon layer (1-5)                | Also referred to as "Shindo" (Depth) |
| Exploration | From dungeon entry to return/death | 1 Exploration consumes 1 count       |

### 1.3 Exploration Count System

| Element               | Specification                                                        |
| --------------------- | -------------------------------------------------------------------- |
| Initial / Max Value   | **10 Counts** (Same value)                                           |
| Consumption Timing    | Consumed the moment the dungeon is entered                           |
| Survival/Death Impact | **Consumed in either case** (Decreases even if you return)           |
| Recovery Method       | **None** (No means of recovery exists)                               |
| Reaching 0 Counts     | **Game Over**                                                        |
| Post-Game Over        | Restart with partial carry-over (Unlocked elements, Library records) |
| UI Position           | Header Top-Right "Exploration: 7/10"                                 |

**Strategic Significance:**

- Reaching deep layers requires multiple exploration attempts (Exact counts to be determined in balance adjustments).
- Failure (Death/Retreat) consumes a count, making reckless charges risky.
- Requires planning: "I'll farm the shallow layers for upgrades this time," vs "Next time I will aim for Depth 5."

> **Note:** The specific number of explorations required to reach deep layers will be decided during the difficulty adjustment phase. Undecided at present.

---

## 2. Map Structure Specifications

### 2.1 Concrete Hourglass Design

**Map Width and Rows per Depth**

| Depth | Theme   | Columns | Rows | Total Nodes (Approx) | Notes                             |
| ----- | ------- | ------- | ---- | -------------------- | --------------------------------- |
| 1     | Decay   | 3-4     | 7    | 21-28                | Intro, moderate choices           |
| 2     | Madness | 4-5     | 7    | 28-35                | Choices begin to increase         |
| 3     | Chaos   | **5-6** | 7    | 35-42                | **Max Width**, highest freedom    |
| 4     | Void    | 3-4     | 7    | 21-28                | Rapid convergence, choices vanish |
| 5     | Abyss   | 1-2     | 7    | 7-14                 | Almost linear, no escape          |

**Player Choices:**

- Average 5-7 node selections per layer (Base is 7 rows = 7 selections).
- Horizontal movement is possible; taking detours can result in 8-10 selections.
- Shortest route takes about 5 nodes to reach the next layer.

**Visual Image of Map Shape:**

```text
      [Base Camp]          ← Base (Safe Zone)
          │
    ┌───┼───┐
   [ Depth 1 ]            ← 3-4 Cols (Narrow)
    │ │ │ │
  ┌─┴─┼─┼─┴─┐
 [ Depth 2 ]              ← 4-5 Cols (Expanding)
  │ │ │ │ │
 ┌┴─┼─┼─┼─┴┐
[ Depth 3 ]               ← 5-6 Cols (Max Width)
 │ │ │ │ │
  └─┬─┼─┬─┘
   [ Depth 4 ]            ← 3-4 Cols (Rapid Shrink)
    │ │ │
     └┬┘
   [ Depth 5 ]            ← 1-2 Cols (Linear)
      │
   [Boss / Abyss]

```

### 2.2 Node Connection Rules

**Vertical (Descending):**

- Each node connects to **1-3 nodes** in the row below.
- Depth 5 has minimal connections (1-2 nodes).

**Horizontal (Detour):**

- Can move to **adjacent nodes** within the same row (Depth 1-4 only).
- Horizontal movement counts as a node selection (no extra cost).
- Events/Combats occur normally on horizontally visited nodes.
- Horizontal movement is disabled in Depth 5 (due to linear path).

**Visual Representation:**

- Vertical Route: Thick solid line.
- Horizontal Route: Thin dotted line (Suggesting a "detour").

### 2.3 Node Appearance Ratios

**Ratio of Enemy/Event/Rest per Layer**

| Depth | Combat | Event | Rest | Notes                             |
| ----- | ------ | ----- | ---- | --------------------------------- |
| 1     | 50%    | 30%   | 20%  | Base. High rest for easier intro  |
| 2     | 55%    | 30%   | 15%  | Combat increases slightly         |
| 3     | 60%    | 30%   | 10%  | Combat becomes dominant           |
| 4     | 65%    | 30%   | 5%   | Almost no rest, harsh environment |
| 5     | 70%    | 25%   | 5%   | Combat focused, fewer events      |

**Event Content Breakdown:**

- **30-40% of Event Nodes contain Rest elements.**
- Rest Choice: Select either **AP Recovery** or **HP Recovery**.
- Example: "Found a cave. Rest? (Recover HP or Recover AP)"

- Alternative rewards for not choosing rest are not implemented at this stage.

**Placement Logic:**

- At least one "Non-Combat Node" per row (prevents complete dead ends).
- At least one Rest Node in the row immediately preceding a Boss (Preparation opportunity).

### 2.4 Map Generation Constraints

```typescript
interface MapGenerationConstraints {
  // Basic Constraints
  minNonCombatPerRow: 1; // Min 1 non-combat node per row
  maxConsecutiveCombat: 3; // Max consecutive combat nodes
  bossPreparationGuarantee: true; // Guaranteed rest before Boss

  // Special Nodes
  hiddenRoomPossibility: boolean; // Flag for hidden room generation
  shortcutPaths: number; // Number of shortcut routes (Depth 3 only)

  // Isolated Node Validation
  validateConnectivity: true; // Verify/fix unreachable or dead-end nodes
}
```

---

## 3. Node Information Disclosure Rules

### 3.1 Disclosure Stages

| State                     | Combat Node                         | Event Node                        | Rest Node            |
| ------------------------- | ----------------------------------- | --------------------------------- | -------------------- |
| **Far (Unconnected)**     | Sword Icon                          | Event Icon (Suggests Risk/Reward) | Campfire Icon        |
| **Adjacent (Selectable)** | Sword Icon                          | Event Icon                        | Campfire Icon        |
| **Adjacent + Hover**      | Enemy Name, Threat, Drop Prediction | Event Name (If Known)             | Est. Recovery Amount |

### 3.2 Enemy Threat Level System

| Threat | Display Name | Corresponding Enemy Category           |
| ------ | ------------ | -------------------------------------- |
| 1      | Minion (Low) | Normal Enemy (Weak)                    |
| 2      | Minion (Mid) | Normal Enemy (Strong)                  |
| 3      | Elite        | Elite Enemy                            |
| 4      | Floor Boss   | Floor Boss of each Depth               |
| 5      | Abyss Boss   | Depth 5 Boss (Guardian, True Evil God) |

> **Note:** Threat classification is an initial definition; details will be adjusted in `overroll_enemy_design.md`.

### 3.3 Definition of "Known" Events

**Conditions for an event to become Known:**

1. Event encountered at least once in past explorations.
2. Data is **saved across all explorations** (retained after reset/game over).
3. All events are "?" on the very first play.

**Display of Known Events:**

- Icon: Suggests event type (Risk High/Low, Reward High/Low).
- Hover: Displays Event Name only (Choice details hidden).

**Display of First-Time Events:**

- Icon: "?"
- Hover: "Unknown Event"

> **Note:** Scout items/skills are under consideration. Implementation priority is low.

---

## 4. UI/UX Layout (PC Only)

### 4.1 Screen Composition (3 Columns + Dynamic Right Panel)

```text
+-------------------------------------------------------------------+
| [Depth 3: Chaos] [Teleport Stone: 2] [Progress Bar] [Return][Menu]|
+-------------------------------------------------------------------+
|                    |                                |             |
|  Left Panel (20%)  |   Center Map (60%)             | Right Panel |
|  ===============   |   ====================         | (20%)       |
|  [Current Status]  |                                | *Visible    |
|  HP: 250/300       |      ○ ─ ○ ─ ●               | on Click    |
|  AP: 80/100        |      │   │   │                | only        |
|                    |   ┌──┴──┬──┴──┐              |             |
|  [Buff/Debuff]     |   │[E] │[?]│[R]│             |             |
|  - Madness Lv2     |   └─────┴─────┘              |             |
|  - Atk +10%        |      │   │   │                |             |
|                    |      ○   ○   ○                |             |
|  [Deck Status]     |                                |             |
|  Cards: 25         |                                |             |
|  Hand Max: 7       |                                |             |
|                    |                                |             |
|  [🎒 Inventory]    |                                |             |
|  └─ Modal Display  |                                |             |
+-------------------------------------------------------------------+
| Exploration: 7/10 | Current Layer: Depth 3 / 5                    |
+-------------------------------------------------------------------+

```

**Header Items:**

- Depth: Current layer number and theme name.
- Teleport Stone: Quantity held only (Type omitted).
- Progress Indicator: Visually displays distance from current location to Base.
- Return Button / Menu Button.

**Gold Display:**

- Gold is not constantly displayed inside the dungeon.
- Displayed via animation only when Gold is acquired: `[Existing Gold] + [Amount Acquired]`.

### 4.2 Right Panel Details (On Node Click)

**Trigger:** Clicking a selectable node on the map.

**Combat Node Example:**

```text
┌──────────────────┐
│ [Goblin Group]   │ ← Enemy Name
│                  │
│ Type: Combat     │
│ Threat: ★★☆      │ ← Level 2 (Minion/Mid)
│                  │
│ Drop Prediction: │
│ - Magic Stone (S)│
│ - Card x1        │
│                  │
│ [Start Battle]   │ ← Action Button
└──────────────────┘

```

**Clicking a different node:**

- Right panel content switches immediately.
- Previous node info is hidden.
- Only the "Currently Selected Node" is highlighted.

**Closing the Panel:**

- Click outside (Map area).
- Press ESC key.
- "x" button on the top right of the panel.

### 4.3 Return Button Placement

**Position:** Header Top-Right (Next to Menu).
**Always Visible:** Yes (Clickable at any depth).

**Visual Design:**

- Normal: `[← Return]` (Grayish, subtle).
- Hover: `[← Return]` (Orangeish, conspicuous).
- Warning: `[⚠ Return]` (Reddish, when Exploration Count is 3 or less).

---

## 5. Return System UI

### 5.1 Return Modal Display

**Trigger:** Click Return Button.

**Modal Window:**

```text
┌─────────────────────────────────────┐
│          Confirm Return Route       │
├─────────────────────────────────────┤
│                                     │
│ To Base: 18 Nodes                   │
│                                     │
│ [Risk Prediction]                   │
│ Est. Combat: 5 ~ 7 battles          │
│ Est. Consumption:                   │
│  - HP: -120 ~ -180 (40-60%)         │
│  - AP: -50 ~ -80                    │
│  - Death Risk: Medium               │
│                                     │
│ * Use a Teleport Stone for safe     │
│   return. (Held: 2)                 │
│                                     │
├─────────────────────────────────────┤
│ [Show Route]      [Use Stone]       │
│ [Return on Foot]  [Cancel]          │
└─────────────────────────────────────┘

```

### 5.2 Detailed Route Display

**On clicking "Show Route":**

- Modal closes.
- **Planned Return Route** is highlighted in yellow on the map.
- "Encounter Rate" (%) is displayed on each node.

**Visual Example:**

```text
  [Base]
    ↑ 10%
   ○ ← Low Risk
    ↑ 25%
   ○
    ↑ 45%
   ● ← Current Loc (High Risk)

```

### 5.3 Enemy Spawn Rules During Return

| Item            | Specification                   |
| --------------- | ------------------------------- |
| Enemy Strength  | **Normal** (No weakening)       |
| Rewards         | **Normal**                      |
| Card Mastery    | **Increases normally**          |
| Elite Enemies   | **Do not respawn**              |
| Bosses          | **Do not respawn**              |
| Enemy Type      | Same type as defeated enemies   |
| Encounter Check | Performed individually per room |

### 5.4 Process After Starting Return

**Return on Foot:**

1. Automatically move backward along the route one node at a time.
2. "Encounter Check" at each node.
3. Player control only occurs when combat starts (Transition to normal battle screen).
4. Auto-return resumes after battle.
5. Exploration ends upon reaching Base.

**Use Teleport Stone:**

- Immediate return to Base.
- No combat, no consumption.
- Consumes 1 Teleport Stone.
- Reward reduction applies (depending on stone type).

---

## 6. Theme Expression by Layer (Simplified)

### 6.1 Visual Implementation

Eliminate excessive effects; differentiate via color tone, background, and icon design.

| Depth | Background Color | Saturation     | Node Icon                       | Connection Line        | Notes                        |
| ----- | ---------------- | -------------- | ------------------------------- | ---------------------- | ---------------------------- |
| 1     | Brown tones      | Med            | Normal size, slightly worn      | Solid, cracked texture | Decay image                  |
| 2     | Purple tones     | High           | Slightly irregular shapes       | Solid, slightly wavy   | Suggestion of Madness        |
| 3     | Dark Red/Violet  | Very High      | Mixed designs                   | Complex mesh/web       | Chaos, visual pressure       |
| 4     | Grey/White       | Very Low       | Semi-transparent, thin outlines | Dotted, fading         | Void, loss of info           |
| 5     | Pure Black       | 0 (Monochrome) | White points only               | Thin white lines       | Abyss, simple & intimidating |

### 6.2 Dynamic Change Effects

**Progress Indicator:**

- Displayed in Header.
- Visually shows distance from current location to Base.
- Bar decreases as you go deeper.

**Exploration Count Sync:**

- 3 or less remaining: Header turns pale red.
- 1 remaining: Entire header turns red, warning icon blinks.

---

## 7. Implementation Priority

### 7.1 MVP (Minimum Viable Product)

**Phase 1: Core Functions** (Highest Priority)

- [ ] Hourglass map generation logic (Cols/Rows for Depth 1-5).
- [ ] Node placement algorithm (Combat/Event/Rest ratios).
- [ ] Map constraints application (Max consecutive combat, guaranteed rest before boss).
- [ ] Node connection rules (Vertical/Horizontal).
- [ ] Basic UI (3-column layout, right panel on click).
- [ ] Node info disclosure (Threat level system).
- [ ] Return modal and route calculation.
- [ ] Color tone changes by layer (Background only).
- [ ] Inventory modal integration.

**Phase 2: Strategy Enhancement** (Secondary)

- [ ] Known event system (Record past encounters).
- [ ] Detailed route display function.
- [ ] Encounter rate calculation and display.
- [ ] Progress indicator.

**Phase 3: UX Improvements** (Later)

- [ ] Exploration count sync UI (Header warning colors).
- [ ] Detailed design differences in node icons.
- [ ] Smooth scrolling/transition animations.
- [ ] Gold acquisition animation.

### 7.2 Deferred (Future Implementation)

- Tutorial flow (Integrated during full tutorial implementation).
- Scout items/skills (Under consideration, low priority).
- Complex visual effects (Pulsing lines, blinking, etc.).
- Mobile support.

---

## 8. Technical Specification Notes

### 8.1 Proposed Data Structure

**Map Node:**

```typescript
interface MapNode {
  id: string;
  depth: 1 | 2 | 3 | 4 | 5;
  column: number; // 0-5 (Column position)
  row: number; // 0-6 (Row position)
  type: "combat" | "event" | "rest";
  content: CombatData | EventData | RestData;
  isKnown: boolean; // Event known flag
  connections: string[]; // Connected Node ID array
  threatLevel?: 1 | 2 | 3 | 4 | 5; // Combat nodes only
}

interface CombatData {
  enemyId: string;
  enemyName: string;
  threatLevel: 1 | 2 | 3 | 4 | 5;
  expectedDrops: DropItem[];
}

interface EventData {
  eventId: string;
  eventName: string;
  riskLevel: "low" | "medium" | "high";
  rewardLevel: "low" | "medium" | "high";
  hasRestOption: boolean;
}

interface RestData {
  recoveryType: "hp" | "ap" | "choice";
  estimatedRecovery: number;
}
```

**Exploration State:**

```typescript
interface ExplorationState {
  currentNodeId: string;
  visitedNodes: string[];
  remainingExplorations: number; // Available explorations (Init 10)
  currentDepth: 1 | 2 | 3 | 4 | 5;
  playerStatus: PlayerStatus;
  knownEvents: string[]; // Saved across all explorations
}
```

### 8.2 Map Generation Algorithm Overview

1. Determine number of columns based on rows (7) for each Depth.
2. Randomly assign types (Combat/Event/Rest) to nodes based on ratios.
3. **Constraint Check**:

- Max consecutive combat nodes <= 3?
- At least 1 non-combat node per row?
- Rest node exists in row before Boss?

4. Generate vertical connections (Connect to 1-3 nodes in lower row).
5. Add horizontal connections (Routes to adjacent nodes).
6. Validate and fix isolated nodes (Unreachable/Dead ends).
