---
name: ui-ux-creator
description: UI/UX design skill for dark fantasy card battle games. Covers color palettes, typography, animations, card layouts, and psychological pressure design. Use for "create UI component", "design battle screen", "add animation", or visual design tasks.
---

# UI/UX Creator Skill

Design guidelines for dark fantasy roguelike card battle games.

## Design Philosophy

| Principle | Description |
|-----------|-------------|
| Clarity over Decoration | Information must be clear; decoration serves purpose |
| Pressure through Simplicity | Create psychological tension with minimal elements |
| Depth Visualization | Dungeon depth should be visually perceivable |
| Strategic Feedback | Instant feedback to support player decisions |

## Color System

```css
:root {
  /* Base Colors */
  --bg-surface: #1a1a1f;
  --bg-elevated: #25252e;
  --text-primary: #e8e8ea;
  --text-secondary: #a8a8b2;

  /* Depth Progression (darken as depth increases) */
  --depth-1-5: #2a2535;    /* Purple-dark */
  --depth-6-10: #252a35;   /* Blue-dark */
  --depth-11-15: #2a2525;  /* Red-dark */
  --depth-16-20: #1a1a1a;  /* Abyss */

  /* Functional Accents */
  --accent-danger: #d32f2f;    /* HP loss, danger */
  --accent-warning: #f57c00;   /* Caution, cost */
  --accent-success: #388e3c;   /* Heal, success */
  --accent-info: #1976d2;      /* Info, magic */

  /* Status Effects */
  --status-buff: #4caf50;
  --status-debuff: #e64a19;
  --status-neutral: #757575;

  /* Card Rarity */
  --rarity-common: #78909c;
  --rarity-rare: #5e35b1;
  --rarity-epic: #d84315;
  --rarity-legend: #f59e0b;

  /* Borders & Shadows */
  --border-subtle: rgba(255, 255, 255, 0.08);
  --border-emphasis: rgba(255, 255, 255, 0.16);
  --shadow-depth: 0 4px 12px rgba(0, 0, 0, 0.4);
}
```

## Typography

```css
:root {
  /* Font Families */
  --font-display: 'Cinzel', serif;        /* Titles, headers */
  --font-body: 'Noto Sans JP', sans-serif; /* UI, descriptions */
  --font-mono: 'SF Mono', monospace;       /* Numbers, HP, damage */

  /* Font Sizes */
  --text-xs: 0.75rem;   /* 12px - Supplementary */
  --text-sm: 0.875rem;  /* 14px - Card description */
  --text-base: 1rem;    /* 16px - Standard UI */
  --text-lg: 1.125rem;  /* 18px - Card name */
  --text-xl: 1.25rem;   /* 20px - Section header */
  --text-2xl: 1.5rem;   /* 24px - Large numbers */
  --text-3xl: 2rem;     /* 32px - Title */
}

/* Number Display - Always high contrast, tabular */
.number-display {
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  color: #ffffff;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.8);
}
```

## Card Component

**Structure:** Cost badge (top-left) → Image → Name + Description → Type label (bottom-right)

```css
.game-card {
  width: 140px;
  aspect-ratio: 2.5/3.5;
  border-radius: 8px;
  background: var(--bg-elevated);
  border: 2px solid var(--border-subtle);
  transition: transform 0.2s ease-out;
}

.game-card:hover {
  transform: translateY(-8px);
  box-shadow: 0 0 20px rgba(138, 43, 226, 0.3);
  border-color: rgba(138, 43, 226, 0.5);
}

.game-card.selected {
  box-shadow: 0 0 20px rgba(138, 43, 226, 0.6);
  border-color: rgba(138, 43, 226, 0.8);
  transform: translateY(-4px);
}

/* Hand Layout - Overlap cards, fan out on hover */
.card-hand {
  display: flex;
  justify-content: center;
  gap: -2rem;
}

.card-hand:hover .game-card {
  margin: 0 0.5rem;
}
```

## Battle Animations

**Principle:** Use animations only for state changes, user feedback, and attention direction.

```css
/* Damage Float */
@keyframes damage-float {
  0% { opacity: 1; transform: translateY(0) scale(0.8); }
  20% { transform: translateY(-10px) scale(1.2); }
  100% { opacity: 0; transform: translateY(-50px) scale(1); }
}

/* Card Play */
@keyframes card-play {
  0% { transform: translateY(0) scale(1); opacity: 1; }
  50% { transform: translateY(-20px) scale(1.05); }
  100% { transform: translateY(-100px) scale(0.8); opacity: 0; }
}

/* Critical Warning Pulse (use sparingly) */
@keyframes critical-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}

.critical-warning {
  animation: critical-pulse 1.5s ease-in-out infinite;
}
```

## Lives Display (Psychological Pressure)

Display remaining lives prominently with color progression:

| Lives | Color | Effect |
|-------|-------|--------|
| 7-10 | Green | Normal |
| 4-6 | Yellow | Warning |
| 1-3 | Red | Danger + pulse animation |

```css
.lives-display {
  font-family: var(--font-mono);
  font-size: var(--text-2xl);
}

.lives-danger {
  color: var(--accent-danger);
  animation: critical-pulse 1.5s ease-in-out infinite;
}
```

## Depth Indicator

Visual progression through dungeon depths:

```css
.depth-indicator {
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.1em;
}

/* Vignette effect - darker at deeper levels */
.dungeon-view[data-depth="deep"] {
  box-shadow: inset 0 0 120px rgba(0, 0, 0, 0.7);
}
```

## Status Effects Display

Always use icon + number. Never rely on color alone.

```css
.status-effect {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: var(--text-xs);
}

.status-buff { background: rgba(76, 175, 80, 0.2); }
.status-debuff { background: rgba(230, 74, 25, 0.2); }
```

## Accessibility Requirements

1. **Contrast:** Minimum 4.5:1 for text, 3:1 for large text
2. **Color Independence:** Always pair color with icon/text
3. **Focus States:** Visible outline for keyboard navigation
4. **Motion:** Keep animations under 3 seconds

```css
*:focus-visible {
  outline: 2px solid var(--accent-info);
  outline-offset: 2px;
}

/* Reduced motion preference */
@media (prefers-reduced-motion: reduce) {
  * { animation-duration: 0.01ms !important; }
}
```

## CSS Best Practices

1. **Units:** Use `vh/vw` for sizing, `px` only for borders
2. **Scoping:** Always scope class names: `.battle-screen .card { }` not `.card { }`
3. **Performance:** Use `transform` and `opacity` for animations (GPU-accelerated)
4. **Variables:** Use CSS custom properties for theming

```css
/* Good - GPU accelerated */
.smooth { transition: transform 0.3s, opacity 0.3s; }

/* Avoid - causes reflow */
.avoid { transition: width 0.3s, height 0.3s; }
```

## Implementation Checklist

- [ ] Use CSS variables for consistent theming
- [ ] Numbers in monospace font
- [ ] Color + icon/text for all status indicators
- [ ] Animations complete within 3 seconds
- [ ] Test at depth 1, 10, 20 for visual consistency
- [ ] Test with 1-2 lives remaining
- [ ] Keyboard navigation works
- [ ] 60fps performance maintained
