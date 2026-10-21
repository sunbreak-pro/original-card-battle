// 所持カード (CAMP_FACILITIES_DESIGN.md §3.4): the cards this character owns as this life has grown
// them — the current name (MasteryBook.Mastered), the 習熟 step and the ticks toward the next one
// (the book's record and its profile's thresholds), the 永続能力 a stance card carries (CardFaceText's
// lasting rows) — with a mark on the cards in the battle deck. Read only. Built from the deck screen's
// parts; C or Esc closes it.
import { h, place, pushKeys } from '../common/stage.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { Mastery, MasteryProfiles } from '../../core/battle/Mastery.js';
import { DeckBuilder } from '../../bridge/depiction/DeckBuilder.js';
import { MetaText } from './text.js';
import { sceneRoot, soundOf, sceneClock, styler, icon, modalSwallow } from './util.js';
import { faceOf, scaledCard, cardEl } from './cardFace.js';

const Columns = 6;
const Rows = 3;
const PerPage = Columns * Rows;
const FaceScale = 0.68;

/** Card ids in the battle deck, from a DeckBuilder, a list of CardInstances or a list of ids. */
function deckIds(deck) {
  if (!deck) return () => false;
  if (typeof deck.CountOf === 'function') return (id) => deck.CountOf(id) > 0;
  const ids = new Set((deck || []).map((c) => (typeof c === 'string' ? c : (c.Def ? c.Def.Id : c.Id))));
  return (id) => ids.has(id);
}

/**
 * mountOwnedCards(host, ctx) → { destroy(), close() }
 * ctx: { stage, audio, owned (card ids), book? (MasteryBook; none: every card at step 0), deck?
 *        (DeckBuilder | CardInstance[] | ids: the battle deck to mark), expedition? (the running
 *        Expedition: owned, book and deck are read off it when not given), onClose() }
 */
export function mountOwnedCards(host, ctx) {
  const sound = soundOf(ctx);
  const root = sceneRoot(host, 'owned');
  const clock = sceneClock();
  const ex = ctx.expedition || null;
  const book = ctx.book || (ex && ex.Mastery) || null;
  const inDeck = deckIds(ctx.deck || (ex ? ex.Deck : null));
  const ownedSet = new Set(ctx.owned || (ex ? [...ex.Owned] : []));
  const cards = CardCatalog.All.filter((def) => ownedSet.has(def.Id));
  let alive = true;
  let closing = false;
  let page = 0;
  let selected = cards[0] || null;

  const rankOf = (id) => (book ? book.RankOf(id) : 0);
  const ticksOf = (id) => (book ? book.TicksOf(id) : 0);
  const profileOf = (id) => (book ? book.ProfileOf(id) : MasteryProfiles.Find(id));
  const grown = (def) => (book ? book.Mastered(def) : def);
  // Another card already holds the life's one 3 段 (MasteryBook.TopRankHolder).
  const topHolderOther = (id) => {
    const holder = book && book.TopRankHolder !== undefined ? book.TopRankHolder : null;
    return holder != null && holder !== id;
  };
  const faces = new Map();
  const faceFor = (def) => { const g = grown(def); if (!faces.has(def.Id)) faces.set(def.Id, faceOf(g)); return faces.get(def.Id); };

  const shade = place(h('div', { class: 'ocbg-meta-shade is-deep', onclick: () => close() }), 0, 0, 1920, 1080);
  root.appendChild(shade);
  const panel = place(h('section', { class: 'ocbg-meta-panel ocbg-meta-owned-panel', role: 'dialog', 'aria-label': MetaText.OwnedTitle }), 40, 36, 1840, 1008);
  root.appendChild(panel);
  panel.appendChild(h('header', null,
    h('h2', null, MetaText.OwnedTitle), h('span', { class: 'ocbg-meta-owned-count' }, MetaText.OwnedCount(cards.length)),
    h('span', { class: 'ocbg-meta-keys is-in-panel' }, MetaText.OwnedKeys),
    h('button', { class: 'ocbg-meta-x', type: 'button', onclick: () => close(), 'aria-label': MetaText.Close }, icon('exile', 22, 'var(--ink)'))));

  const grid = place(h('div', { class: 'ocbg-meta-grid' }), 28, 92, 1100, 830);
  panel.appendChild(grid);
  const prevBtn = h('button', { class: 'ocbg-meta-nav', type: 'button', onclick: () => turn(-1) }, icon('back', 20, 'var(--ink)'), MetaText.PrevPage);
  const pageText = h('span', { class: 'ocbg-meta-pagetext' });
  const nextBtn = h('button', { class: 'ocbg-meta-nav', type: 'button', onclick: () => turn(1) }, MetaText.NextPage, icon('forward', 20, 'var(--ink)'));
  panel.appendChild(place(h('div', { class: 'ocbg-meta-pagenav' }, prevBtn, pageText, nextBtn), 28, 940, 1100, 48));
  const detail = place(h('section', { class: 'ocbg-meta-detail is-owned' }), 1160, 92, 652, 896);
  panel.appendChild(detail);

  function pips(rank, size) {
    const el = h('span', { class: 'ocbg-meta-pips' + (size ? ' is-' + size : '') });
    for (let i = 1; i <= Mastery.TopRank; i++) el.appendChild(h('i', { class: i <= rank ? 'is-on' : '' }));
    return el;
  }

  let cells = [];
  function drawGrid(dir) {
    const pages = Math.max(1, Math.ceil(cards.length / PerPage));
    page = Math.max(0, Math.min(pages - 1, page));
    pageText.textContent = `${page + 1} / ${pages}`;
    prevBtn.disabled = page === 0;
    nextBtn.disabled = page >= pages - 1;
    grid.innerHTML = '';
    cells = cards.slice(page * PerPage, page * PerPage + PerPage).map((def, i) => {
      const col = i % Columns, row = Math.floor(i / Columns);
      const face = faceFor(def);
      const rank = rankOf(def.Id);
      const cell = place(h('button', { class: 'ocbg-meta-cell is-owned' + (inDeck(def.Id) ? ' is-in' : ''), type: 'button',
        onclick: () => select(def), onmouseenter: () => sound.play('nodeHover') }), col * 184, row * 274, 176, 262);
      cell.appendChild(scaledCard(face, FaceScale));
      cell.appendChild(h('div', { class: 'ocbg-meta-owned-row' },
        pips(rank), h('span', null, MetaText.RankLine(rank)),
        inDeck(def.Id) ? h('span', { class: 'ocbg-meta-indeck', title: MetaText.InDeck }, icon('draw', 18, 'var(--amber)')) : null));
      cell.def = def;
      grid.appendChild(cell);
      return cell;
    });
    paintSel();
    if (dir) cells.forEach((c, i) => { const s = styler(c, { o: 0, x: 30 * dir }); clock.tween(s, { o: 1, x: 0 }, 220, 'out', (i % Columns) * 16).then(() => { c.style.transform = ''; }); });
  }
  function paintSel() { cells.forEach((c) => c.classList.toggle('is-sel', selected && c.def.Id === selected.Id)); }

  function drawDetail() {
    detail.innerHTML = '';
    if (!selected) { detail.appendChild(h('p', { class: 'ocbg-meta-detail-hint' }, MetaText.OwnedNone)); return; }
    const def = selected;
    const g = grown(def);
    const face = faceFor(def);
    const rank = rankOf(def.Id);
    const ticks = ticksOf(def.Id);
    const profile = profileOf(def.Id);
    const big = cardEl(face);
    place(big, 20, 20);
    detail.appendChild(big);
    const head = place(h('div', { class: 'ocbg-meta-detail-text' }), 260, 20, 372);
    head.appendChild(h('h3', null, g.Name));
    if (g.Name !== def.Name) head.appendChild(h('p', null, MetaText.BaseName(def.Name)));
    if (inDeck(def.Id)) head.appendChild(h('p', { class: 'ocbg-meta-indeck-line' }, icon('draw', 18, 'var(--amber)'), MetaText.InDeck));
    detail.appendChild(head);

    // 習熟: the step, and the ticks toward the next one against the profile's threshold.
    const below = place(h('div', { class: 'ocbg-meta-owned-below' }), 20, 344, 612, 532);
    detail.appendChild(below);
    const m = h('div', { class: 'ocbg-meta-mastery' });
    m.appendChild(h('h4', null, MetaText.Mastery));
    m.appendChild(h('div', { class: 'ocbg-meta-mastery-rank' }, pips(rank, 'big'), h('b', null, MetaText.RankLine(rank))));
    if (!profile) {
      m.appendChild(h('p', null, MetaText.NoGrowth));
    } else if (rank >= Mastery.TopRank) {
      m.appendChild(h('p', null, MetaText.TopRank));
    } else if (rank === Mastery.TopRank - 1 && topHolderOther(def.Id)) {
      // One card of a life holds 3 段 (concept-v3 §14 1 つの生の段数; MasteryBook.Rerank stops the rest
      // at 2). The ticks keep counting but buy no step, so the bar and 次の名 would read as stuck.
      const holder = CardCatalog.All.find((d) => d.Id === book.TopRankHolder);
      const holderName = holder ? grown(holder).Name : book.TopRankHolder;
      m.appendChild(h('p', { class: 'ocbg-meta-mastery-capped' }, MetaText.TopTaken(holderName)));
      m.appendChild(h('p', { class: 'ocbg-meta-mastery-ticks' }, MetaText.TicksSoFar(ticks)));
    } else {
      const need = profile.Thresholds[rank];
      const bar = h('div', { class: 'ocbg-meta-ticks' }, h('i', { style: { width: `${Math.min(100, (100 * ticks) / need)}%` } }));
      m.appendChild(h('div', { class: 'ocbg-meta-mastery-next' }, h('span', null, MetaText.NextRank), bar, h('b', null, MetaText.TicksLine(ticks, need))));
      const nextName = profile.NameAt(rank + 1);
      if (nextName) m.appendChild(h('p', null, MetaText.NextName(nextName)));
    }
    below.appendChild(m);

    // 永続能力: a stance card's lasting rows (CardFaceText.LastingLines), or none.
    const lasting = h('div', { class: 'ocbg-meta-lasting' });
    lasting.appendChild(h('h4', null, MetaText.Lasting));
    if (face.Lasting && face.TextLines.length) face.TextLines.forEach((l) => lasting.appendChild(h('p', null, icon('infinity', 18, 'var(--hp-text)'), l)));
    else lasting.appendChild(h('p', null, MetaText.NoLasting));
    below.appendChild(lasting);

    const lines = DeckBuilder.ShownDetailOf(g);
    const more = h('div', { class: 'ocbg-meta-owned-lines' });
    lines.slice(1).forEach((l) => more.appendChild(h('p', { class: l.startsWith('「') ? 'is-flavour' : '' }, l)));
    below.appendChild(more);
    const s = styler(big, { o: 0, y: 8 });
    clock.tween(s, { o: 1, y: 0 }, 200, 'out');
  }

  function select(def) {
    if (selected && selected.Id === def.Id) return;
    selected = def;
    sound.play('uiClick');
    paintSel();
    drawDetail();
  }
  function turn(by) {
    const pages = Math.max(1, Math.ceil(cards.length / PerPage));
    const next = Math.max(0, Math.min(pages - 1, page + by));
    if (next === page) return;
    page = next;
    sound.play('nodeHover');
    drawGrid(by);
  }
  // The keyboard walks the whole list (a page turns under the cursor); ↑↓ move a row within the page.
  function moveSel(by, rowStep) {
    if (!cards.length) return;
    const i = selected ? cards.findIndex((d) => d.Id === selected.Id) : -1;
    let j = i < 0 ? 0 : i + by;
    if (rowStep && (Math.floor(j / PerPage) !== Math.floor(i / PerPage) || j < 0 || j >= cards.length)) return;
    j = Math.max(0, Math.min(cards.length - 1, j));
    if (j === i) return;
    const p = Math.floor(j / PerPage);
    if (p !== page) { const by2 = p > page ? 1 : -1; page = p; sound.play('nodeHover'); drawGrid(by2); }
    select(cards[j]);
  }

  drawGrid(0);
  drawDetail();
  const ps = styler(panel, { o: 0, y: 14 });
  const pd = styler(shade, { o: 0 });
  clock.tween(pd, { o: 1 }, 200, 'out');
  clock.tween(ps, { o: 1, y: 0 }, 260, 'out');
  sound.play('journalOpen');

  async function close() {
    if (closing) return;
    closing = true;
    sound.play('journalClose');
    clock.tween(pd, { o: 0 }, 160, 'in');
    await clock.tween(ps, { o: 0, y: 10 }, 170, 'in');
    if (alive && ctx.onClose) ctx.onClose();
  }

  const popKeys = pushKeys((e) => {
    if (closing) return true;
    // only a fresh press closes (a held C or Esc repeats; the screen under it would reopen it)
    if (e.key === 'Escape' || e.key === 'c' || e.key === 'C') { if (!e.repeat) close(); return true; }
    if (e.key === 'PageUp') { turn(-1); return true; }
    if (e.key === 'PageDown') { turn(1); return true; }
    if (e.key === 'ArrowLeft') { moveSel(-1); return true; }
    if (e.key === 'ArrowRight') { moveSel(1); return true; }
    if (e.key === 'ArrowUp') { moveSel(-Columns, true); return true; }
    if (e.key === 'ArrowDown') { moveSel(Columns, true); return true; }
    return modalSwallow(e);
  });

  return {
    close,
    destroy() {
      alive = false;
      popKeys();
      clock.stop();
      root.remove();
    },
  };
}
