# Development Skills

This directory contains specialized skill guides for developing different aspects of the card battle game. Each skill provides step-by-step instructions, patterns, and code templates.

## Available Skills

### Content Creation

| Skill | Command | Description |
|-------|---------|-------------|
| **card-creator** | Add new cards | Card data creation, type compliance, deck registration |
| **enemy-creator** | Add new enemies | Enemy definitions, AI patterns, depth-specific data |
| **character-class-creator** | Add new classes | Class data, initial decks, class-specific mechanics |

### System Development

| Skill | Command | Description |
|-------|---------|-------------|
| **battle-system** | Battle features | Buff/debuff, damage calculation, phase execution |
| **camp-facility** | Camp features | Shop, Guild, Blacksmith, Sanctuary, Library |
| **dungeon-system** | Dungeon features | Map generation, node types, return system |

### UI/UX

| Skill | Command | Description |
|-------|---------|-------------|
| **ui-ux-creator** | UI components | Color palettes, typography, animations, layouts |

### Research & Debugging

| Skill | Command | Description |
|-------|---------|-------------|
| **design-research** | Search specs | Quick reference for game design documents |
| **debugging-error-prevention** | Prevent bugs | Error boundaries, type safety, async handling |
| **debugging-active** | Debug issues | State inspection, diagnostic trees, root cause analysis |

### Utilities

| Skill | Command | Description |
|-------|---------|-------------|
| **memory-keeper** | Record info | Store design decisions, bug info, development tips |

## Usage

Skills are automatically triggered by Claude Code when relevant requests are made. Examples:

- "Add a new fire mage card" → `card-creator`
- "Create a boss enemy for depth 5" → `enemy-creator`
- "Debug why damage calculation is wrong" → `debugging-active`
- "Check the card derivation specs" → `design-research`

## Skill File Structure

Each skill directory contains:
- `SKILL.md` - Main skill definition with instructions, patterns, and templates

## Adding New Skills

1. Create a new directory under `.claude/skills/`
2. Add a `SKILL.md` file following the existing pattern
3. Register the skill in the system if needed
