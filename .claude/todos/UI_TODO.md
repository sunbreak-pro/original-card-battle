# BaseCamp Universal Header Component - Implementation Plan

## Overview

Create a reusable header component that displays player status and resources across all basecamp facility screens. The header will dynamically adapt its content based on the current facility context.

## User Requirements

### Layout Structure:

2-column layout with independent title row
Title row at top (facility name + subtitle)
Below title: Left column (player status) | Right column (resources)

### Resource Display:

- Magic stones: Show all 4 rarities horizontally (Small: X | Medium: X | Large: X | Huge: X)
- Gold: Display total basecamp gold
- Souls: Display when relevant (Sanctuary context)

### Player Status Display:

- Player name (if set, otherwise show default)
- Character class + grade (e.g., "Swordsman [E]")
- Exploration runs remaining (e.g., "3/10 runs")
- Character level
- Context-specific progress:

-- In Sanctuary: Show skill tree progress (X/Y nodes unlocked)
-- During dungeon: Show current depth (Depth X/5)

## Design Decisions

### Layout Choice

### Selected: 2-column + independent title row

Maintains existing pattern from Sanctuary/Shop
Clear visual hierarchy
Sufficient space for all information

Component Approach
Create CampHeader component with:

Props interface for flexible configuration
Facility-aware theming (purple/gold/orange accent colors)
Conditional sections (show/hide based on context)

## Architecture

### File Structure

roguelike-card-battle/src/ui/campsUI/
├── common/
│ ├── CampHeader.tsx # Main header component (NEW)
│ ├── CampHeader.css # Header styles (NEW)
│ └── types.ts # Header prop types (NEW)

### Component Interface

typescript// types.ts
export interface CampHeaderProps {
// Facility info
title: string;
subtitle?: string;
themeColor: 'purple' | 'gold' | 'orange' | 'green';

// Display toggles
showPlayerStatus?: boolean;
showResources?: boolean;

// Context-specific content
contextInfo?: {
type: 'sanctuary' | 'depth' | 'none';
data?: {
// For sanctuary: unlocked/total nodes
sanctuaryProgress?: { unlocked: number; total: number };
// For dungeon: current depth
currentDepth?: number;
};
};
}

### Data Access

### From Contexts:

usePlayer() → Player data (name, class, grade, level, souls, gold, magic stones, exploration limit)
useGameState() → Current screen, depth
Direct access to player.sanctuaryProgress for Sanctuary-specific stats

No new Context needed - all data already available

## Implementation Steps

### Phase 1: Component Structure

Create roguelike-card-battle/src/ui/campsUI/common/ directory
Create types.ts with CampHeaderProps interface
Create CampHeader.tsx skeleton with:

Props destructuring
usePlayer/useGameState hooks
Basic JSX structure (title row + 2 columns)

### Phase 2: Left Column - Player Status

Implement player name display (with fallback to "Adventurer")
Add class + grade display (${playerClass} [${classGrade}])
Add level display
Add exploration runs counter (${current}/${max} runs)
Add context-specific progress:

Sanctuary: ${unlockedCount}/${totalNodes} nodes
Depth: Depth ${current}/5

### Phase 3: Right Column - Resources

Create magic stones display section:

4 inline elements for Small/Medium/Large/Huge
Format: S: X | M: X | L: X | H: X
Use simple emoji icons (💎 prefix)

Add gold display (💰 ${baseCampGold} G)
Conditionally show souls (Sanctuary only)

### Phase 4: Styling (CSS)

1. Create CampHeader.css based on existing Sanctuary pattern
2. Define theme color CSS variables:

css --theme-purple: rgba(168, 85, 247, 0.95);
--theme-gold: rgba(212, 175, 55, 0.95);
--theme-orange: rgba(255, 107, 53, 0.95);
--theme-green: rgba(74, 222, 128, 0.95);

3. Layout styles:

Title row: 4.5vh font, gap: 1vh
Columns: display: flex, justify-content: space-between
Resource/status items: gap: 3vw

4. Apply viewport units consistently:

Font sizes: 1.8vh - 4.5vh
Spacing: 2vh, 3vw
Borders: 2px solid rgba(theme-color, 0.3)

### Phase 5: Integration (Temporary Demo)

Update BaseCamp.tsx to use <CampHeader>:

tsx <CampHeader
     title="Base Camp"
     subtitle="Prepare for your next journey"
     themeColor="purple"
     showPlayerStatus={true}
     showResources={true}
   />

Create simple test integration in one existing facility (e.g., Storage or Guild) to verify:

Data binding works correctly
Theme colors apply properly
Layout is responsive

## CSS Design Patterns

### Grid Structure

css.camp-header {
display: grid;
grid-template-rows: auto auto;
gap: 2vh;
padding-bottom: 3vh;
border-bottom: 2px solid rgba(var(--theme-color), 0.3);
}

.header-title-row {
display: flex;
flex-direction: column;
gap: 1vh;
}

.header-columns {
display: flex;
justify-content: space-between;
gap: 5vw;
}

### Theme Color Application

tsx// In component
const themeColors = {
purple: 'rgba(168, 85, 247, 0.95)',
gold: 'rgba(212, 175, 55, 0.95)',
orange: 'rgba(255, 107, 53, 0.95)',
green: 'rgba(74, 222, 128, 0.95)',
};

<div
  className="camp-header"
  style={{ '--theme-color': themeColors[themeColor] }}
>

## Component Usage Examples

### Sanctuary

tsx<CampHeader
title="Sanctuary"
subtitle="Unlock eternal blessings with Soul Remnants"
themeColor="purple"
contextInfo={{
    type: 'sanctuary',
    data: { sanctuaryProgress: { unlocked: 12, total: 50 } }
  }}
/>

### Shop

tsx<CampHeader
  title="Merchant's Exchange"
  subtitle="Trade goods and treasures"
  themeColor="gold"
/>

### During Dungeon (Future)

tsx<CampHeader
title="Dungeon"
subtitle="Depths of corruption"
themeColor="purple"
contextInfo={{
    type: 'depth',
    data: { currentDepth: 3 }
  }}
/>

## Critical Files to Modify/Create

New Files:

roguelike-card-battle/src/ui/campsUI/common/CampHeader.tsx
roguelike-card-battle/src/ui/campsUI/common/CampHeader.css
roguelike-card-battle/src/ui/campsUI/common/types.ts

### Files to Update (for demo integration):

roguelike-card-battle/src/ui/campsUI/BaseCamp.tsx

### Reference Files (do NOT modify yet):

roguelike-card-battle/src/ui/campsUI/Sanctuary/Sanctuary.tsx (existing header pattern)
roguelike-card-battle/src/ui/campsUI/Shop/Shop.tsx (existing header pattern)
roguelike-card-battle/src/domain/camps/contexts/PlayerContext.tsx (data source)
roguelike-card-battle/src/domain/camps/contexts/GameStateContext.tsx (navigation state)

## Verification Plan

After implementation, verify:

1. Visual Check:

Load BaseCamp screen
Confirm header displays with correct data
Check theme color is applied (purple border)

2. Data Binding:

Player name shows (or "Adventurer" fallback)
Class shows "Swordsman [E]" format
Level displays correctly
Exploration runs show "current/max"
Gold displays basecamp value
Magic stones show all 4 rarities

3. Responsive Layout:

Test on different viewport sizes
Ensure vh/vw units scale properly
No overflow or text clipping

4. Context Switching (Future):

When integrated in Sanctuary, verify progress nodes show
When integrated in dungeon, verify depth shows

## Notes

This is a temporary/demo implementation - full integration across all facilities will come later
Use viewport units (vh/vw) consistently per project standards
Keep component stateless - all data from props or hooks
Follow existing Sanctuary pattern for visual consistency
No time estimates - focus on incremental progress
Component should be theme-agnostic with color prop injection
