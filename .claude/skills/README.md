# Development Skills

This directory contains specialized skill guides for developing different aspects of the card battle game. Each skill provides step-by-step instructions, patterns, and code templates.

## Available Skills

### Content Creation

| Skill                       | Command         | Description                                            |
| --------------------------- | --------------- | ------------------------------------------------------ |
| **card-creator**            | Add new cards   | Card data creation, type compliance, deck registration |
| **enemy-creator**           | Add new enemies | Enemy definitions, AI patterns, depth-specific data    |
| **character-class-creator** | Add new classes | Class data, initial decks, class-specific mechanics    |

### UI/UX

| Skill                          | Command                       | Description                                                                                                                                   |
| ------------------------------ | ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| **ui-ux-creator**              | UI components                 | Color palettes, typography, animations, layouts                                                                                               |
| **visual-production-pipeline** | Character art / UI production | Tools and prerequisites survey, one character or one UI screen through the pipeline, report → Artifact → life-editor note (tag `card-battle`) |

## Usage

Skills are automatically triggered by Claude Code when relevant requests are made. Examples:

- "Add a new fire mage card" → `card-creator`
- "Create a boss enemy for depth 5" → `enemy-creator`

## Skill File Structure

Each skill directory contains:

- `SKILL.md` - Main skill definition with instructions, patterns, and templates

## Adding New Skills

1. Create a new directory under `.claude/skills/`
2. Add a `SKILL.md` file following the existing pattern
3. Register the skill in the system if needed
