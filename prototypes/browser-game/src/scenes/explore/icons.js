// Line icons for the exploration scene, drawn in the design system's style (24×24 box, 2.2 px
// stroke, round caps and joins; battle-visual-v1 appendix A). The battle set (OCB.ICONS) has no node
// kinds, items or gear, so they are drawn here. The colour always comes from a role token.

const SVGNS = 'http://www.w3.org/2000/svg';

/** Paths for the exploration screen. Where the battle set already has a fitting shape it is reused. */
export const EXPLORE_ICONS = {
  // node kinds (dungeon_exploration_v4 §6.10: 戦・精・主・休・調・事・痕・彫)
  Battle: 'M20 4l-1.5 5.5L9 19l-4-4 9.5-9.5zM6.5 13.5l4 4M3 21l3.5-3.5',
  Elite: 'M4 4l11 11M13.2 17.2l4-4M16.5 16.5L20 20M20 4L9 15M6.8 13.2l4 4M7.5 16.5L4 20',
  Boss: 'M12 21c-3.9 0-7-3.1-7-7V9.5L3 4l5 3h8l5-3-2 5.5V14c0 3.9-3.1 7-7 7zM9 12.5h.01M15 12.5h.01M10 17h4',
  Rest: 'M12 3c2.6 3 4 5.2 4 7.6a4 4 0 01-8 0C8 8.2 9.4 6 12 3zM4 21l16-4M20 21L4 17',
  Survey: 'M10.5 4a6.5 6.5 0 110 13a6.5 6.5 0 010-13zM15.3 15.3L20.5 20.5M7.5 10.5h6',
  Event: 'M12 3l9 9-9 9-9-9zM10 9.6a2 2 0 113 1.7c-.7.5-1 1-1 1.9M12 16.2h.01',
  Trace: 'M6 21V11a6 6 0 0112 0v10M3.5 21h17M12 10c1.3 1.5 2 2.6 2 3.8a2 2 0 01-4 0c0-1.2.7-2.3 2-3.8z',
  Carving: 'M6 3h12v18H6zM9 8c2-1.6 4 1.6 6 0M9 12.5c2-1.6 4 1.6 6 0M9 17h6',
  // band and tools
  gear: 'M12 8.6a3.4 3.4 0 110 6.8a3.4 3.4 0 010-6.8zM12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1',
  cards: 'M8 3h9v14H8zM5 7v14h9',
  journal: 'M4 5c3-1 6-1 8 1v14c-2-2-5-2-8-1zM20 5c-3-1-6-1-8 1v14c2-2 5-2 8-1z',
  hourglass: 'M7 3h10M7 21h10M8 3c0 5 8 6 8 9s-8 4-8 9M16 3c0 5-8 6-8 9s8 4 8 9',
  stairs: 'M3 20h5v-5h5v-5h5V5h3',
  miasma: 'M3 9c3-3 6 3 9 0s6 3 9 0M3 15c3-3 6 3 9 0s6 3 9 0',
  heart: 'M12 21s-8-5-8-11a4.5 4.5 0 018-2.8A4.5 4.5 0 0120 10c0 6-8 11-8 11z',
  pip: 'M12 6a6 6 0 110 12a6 6 0 010-12z',
  person: 'M12 4a3.5 3.5 0 110 7a3.5 3.5 0 010-7zM5 21c0-4 3-7 7-7s7 3 7 7',
  seal: 'M9 3h6v6H9zM9 9l-3 4h12l-3-4M4 13h16v4H4zM6 21h12',
  tool: 'M14.5 4a5 5 0 00-4.6 6.9L4 16.8 7.2 20l5.9-5.9A5 5 0 0019.9 9.5l-3 3-3.4-.9-.9-3.4 3-3z',
  page: 'M6 3h9l4 4v14H6zM15 3v4h4M9 12h7M9 16h5',
  caution: 'M12 4L21 20H3zM12 10v4.5M12 17.4h.01',
  // consumables (DungeonContent ItemCatalogue ids)
  joka_no_ko: 'M7 21h10M8.5 21c0-2.2 1.4-3.5 3.5-3.5s3.5 1.3 3.5 3.5M12 17.5V15M12 12c-2-1.6 2-3 0-4.6S12 4.6 12 3',
  yakuso: 'M12 21V10M12 10c0-4 3-6.5 7-6.5 0 4-3 6.5-7 6.5zM12 15c0-3-2.2-5-6-5 0 3 2.2 5 6 5z',
  koi_hoshiniku: 'M15 3.5a5.5 5.5 0 015.5 5.5c0 3.8-3.6 6.2-7.6 6.2L9.8 18.3 7.5 18 7.2 15.7l3-3C10.2 8 11.6 3.5 15 3.5zM5.6 17.2a1.6 1.6 0 11-2 2',
  wasuremizu: 'M9.5 3h5M10.5 3v5.5L5.4 17.4A2.4 2.4 0 007.5 21h9a2.4 2.4 0 002.1-3.6L13.5 8.5V3M7.6 15h8.8',
  shirube_no_ishi: 'M5 15.5L8 7l7-2.5 5 6.5-2 8.5H8zM9.5 13l3-2 3 2',
  pouch: 'M8 7h8l3 4v6a4 4 0 01-4 4H9a4 4 0 01-4-4v-6zM9 7L8 4h8l-1 3',
};

const FILLED = { heart: 1, pip: 1 };

/**
 * ico(name, size, color, { fill, fillOpacity, dashed, width }) — an inline SVG in the design
 * system's icon style. `name` is a key of EXPLORE_ICONS.
 */
export function ico(name, size = 24, color = 'currentColor', opts = {}) {
  const s = document.createElementNS(SVGNS, 'svg');
  s.setAttribute('viewBox', '0 0 24 24');
  s.setAttribute('width', size);
  s.setAttribute('height', size);
  s.setAttribute('class', 'ocb-icon');
  s.setAttribute('aria-hidden', 'true');
  const p = document.createElementNS(SVGNS, 'path');
  p.setAttribute('d', EXPLORE_ICONS[name] || EXPLORE_ICONS.pouch);
  p.setAttribute('stroke-linecap', 'round');
  p.setAttribute('stroke-linejoin', 'round');
  if (FILLED[name] && !opts.outline) {
    p.setAttribute('fill', color);
    p.setAttribute('stroke', 'none');
  } else {
    p.setAttribute('fill', opts.fill || 'none');
    if (opts.fill && opts.fillOpacity != null) p.setAttribute('fill-opacity', opts.fillOpacity);
    p.setAttribute('stroke', color);
    p.setAttribute('stroke-width', opts.width || 2.2);
    if (opts.dashed) p.setAttribute('stroke-dasharray', '2.4 2.4');
  }
  s.appendChild(p);
  return s;
}

/** The icon of a consumable or tool id, falling back to the pouch. */
export function itemIcon(id) {
  return EXPLORE_ICONS[id] ? id : 'pouch';
}
