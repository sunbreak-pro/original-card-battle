// 出立 (CAMP_FACILITIES_DESIGN.md §3.2): the battle deck first, then the tools, the consumables and the
// talent — Unity's DeckSelectScreen (#190) and DepartureScreen (#58) as two steps of one screen. It
// decides nothing: what a filter keeps, what a page holds, whether a card may go in, every word about
// a card and whether the life may set out come from Depiction.Bridge.DeckBuilder / DepartureBuilder
// (whose rules are BattleCore.DeckRules'); the supplies are DungeonContent's catalogue
// (DepartureSupplies). The deck limits shown are the life's (Inheritance.startLife: 生存者ボーナス).
//
// JS extension (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」): both builders are made with the life's
// DeckLimits, read from ctx.lifeStart (DeckMin / DeckMax / ToolSlots / ConsumableSlots), so a survivor's
// heir fills a 4th tool slot and a deck up to 45 (concept-v3.md §8.2 / §14 生存者ボーナス). A first life
// gets the C#'s 20〜40 and 3 + 3. The limits go out in the onSetOut payload for the exploration.
import { h, place, pushKeys } from '../common/stage.js';
import { DeckBuilder, DeckFilter } from '../../bridge/depiction/DeckBuilder.js';
import { DepartureBuilder } from '../../bridge/depiction/DepartureBuilder.js';
import { BattleAttribute } from '../../core/battle/Types.js';
import { DeckLimits } from '../../core/battle/DeckRules.js';
import { Expedition } from '../../bridge/exploration/Expedition.js';
import { ItemCatalogue } from '../../core/dungeonContent/ItemCatalogue.js';
import { EffectSide } from '../../core/dungeonContent/ItemEffect.js';
import { InheritanceText } from '../../core/inheritance/Inheritance.js';
import { JournalBook } from '../../bridge/journal/JournalBook.js';
import { DepartureSupplies } from './DepartureSupplies.js';
import { MetaText } from './text.js';
import { sceneRoot, soundOf, sceneClock, styler, ocb, icon, rollNumber } from './util.js';
import { createBackdrop } from './backdrop.js';
import { hallBackdropSpec } from './hall.js';
import { faceOf, scaledCard, cardEl, termRows } from './cardFace.js';

// DeckSelectScreen: 6 columns × 3 rows, the card at 72% above its − ×n ＋ row.
const Columns = 6;
const Rows = 3;
const PerPage = Columns * Rows;
const FaceScale = 0.72;
// DepartureScreen: the talent grid, 3 × 6.
const TalentColumns = 3;
const TalentRows = 6;
const TalentPerPage = TalentColumns * TalentRows;

// The attribute's icon and its role colour (battle-visual-v1 §2.2), keyed by BattleAttribute.
const ATTR_ICON = { [BattleAttribute.Attack]: 'sword', [BattleAttribute.Guard]: 'shield', [BattleAttribute.Skill]: 'star', [BattleAttribute.Stance]: 'flag' };
const ATTR_INK = { [BattleAttribute.Attack]: 'var(--phosphor)', [BattleAttribute.Guard]: 'var(--guard)', [BattleAttribute.Skill]: 'var(--amber)', [BattleAttribute.Stance]: 'var(--hp-text)' };

/** A design-document citation inside an item's note (「（seven_layers_v4.md §3.2）」): not for the player. */
const DOC_CITATION = /（[^（）]*\.md[^（）]*）/g;
/**
 * A sentence carrying a design-status remark (「防御は Guard だけ（アーマーは凍結）」: what the design
 * has frozen, CLAUDE.md 凍結): the sentence goes with its remark, since it only explains the design.
 */
const DESIGN_STATUS = /。?[^。（）]*（[^（）]*凍結[^（）]*）/g;
const playerNote = (note) => String(note || '').replace(DOC_CITATION, '').replace(DESIGN_STATUS, '').trim();

/**
 * 出立's draft: the deck being built and the supplies picked, not yet set out. It is kept while the heir
 * steps back into 継承の間 and comes again, over a reload, and over closing the game and opening it again
 * (出立 has no 保存して閉じる, so the draft is the only way a half-built deck outlives the page), so
 * 「← 継承の間へ」 or a closed tab throws nothing away. Only the life it was made for reads it back (the
 * lineage seed, the generation and the life's seed, which the hall's startLife gives again on every
 * visit); 出立する clears it, since the router then keeps the deck itself (ctx.savedDeck). An unsent
 * choice, so a per-viewer convenience: memory first, localStorage behind it (sessionStorage, where an
 * older build kept it, is still read), every access guarded (storage may be missing or throw).
 */
const DRAFT_KEY = 'ocb.departure.draft.v1';
let draftInMemory = null;
function draftIdOf(lifeStart) {
  if (!lifeStart || !lifeStart.Life) return null;
  const lineageSeed = lifeStart.Lineage ? lifeStart.Lineage.Seed : '';
  return [lineageSeed, lifeStart.Life.Generation, lifeStart.Seed].map((x) => String(x ?? '')).join('|');
}
function storedDraft(storage, id) {
  try {
    const text = window[storage].getItem(DRAFT_KEY);
    const d = text ? JSON.parse(text) : null;
    if (d && d.id === id && typeof d.deck === 'string') return d;
  } catch (e) { /* storage unavailable */ }
  return null;
}
function readDraft(id) {
  if (draftInMemory && draftInMemory.id === id) return draftInMemory;
  return storedDraft('localStorage', id) || storedDraft('sessionStorage', id);
}
function writeDraft(d) {
  draftInMemory = d;
  try { window.localStorage.setItem(DRAFT_KEY, JSON.stringify(d)); } catch (e) { /* storage unavailable */ }
}
function clearDraft() {
  draftInMemory = null;
  for (const storage of ['localStorage', 'sessionStorage']) {
    try { window[storage].removeItem(DRAFT_KEY); } catch (e) { /* storage unavailable */ }
  }
}

/**
 * mountDeparture(host, ctx) → { destroy() }
 * ctx: { stage, audio, settings, lifeStart (Inheritance.startLife: its DeckMin / DeckMax / ToolSlots /
 *        ConsumableSlots are the limits), owned (card ids; missing or empty: the forty every character
 *        owns, Expedition.InitialKinds; either way the prototype deck's kinds join them,
 *        DeckBuilder.WithPrototypeKinds; null owns the catalogue), savedDeck? (DeckBuilder.Save string),
 *        seed?, onSetOut({ deck, deckBuilder, deckSave, battleToolIds, consumableIds, loadout, talent,
 *        departure, limits }), onBack(), onSettings()?, onJournal()? }
 * With onSettings, Esc opens the settings (CAMP_FACILITIES_DESIGN.md §3.5); without it Esc goes back.
 * A draft of the same life (see readDraft) wins over savedDeck: the screen reopens as it was left.
 */
export function mountDeparture(host, ctx) {
  const O = ocb();
  const sound = soundOf(ctx);
  const lifeStart = ctx.lifeStart;
  // The kinds the heir may build from: the router's (or the initial forty), plus the prototype deck's
  // (Expedition.BeginLife owns its start deck's kinds anyway), so 「試作デッキ」 opens at its full 20.
  const owned = ctx.owned === null ? null
    : DeckBuilder.WithPrototypeKinds(ctx.owned && [...ctx.owned].length ? [...ctx.owned] : Expedition.InitialKinds);
  // The life's limits (DeckLimits.From reads the four numbers; a missing one is the C#'s).
  const limits = DeckLimits.From(lifeStart);
  const deckMin = limits.DeckMin;
  const deckCap = limits.DeckMax;
  const root = sceneRoot(host, 'departure');
  const backdrop = createBackdrop(root, hallBackdropSpec(root, 0.5), { vignette: 0.6 });
  const clock = sceneClock((dt, now) => backdrop.paint(now));
  let alive = true;
  let busy = false;

  // ---- state (the bridge's builders) ----
  // A deck on the owned kinds with the life's limits (DeckBuilder.WithOwned keeps a deck over the cap).
  const adopt = (src) => src.WithOwned(owned);
  let notice = '';
  // The notice says why the deck opened over the cap; it goes once the deck is back under it.
  let noticeOverCap = false;
  let deck;
  const draftId = draftIdOf(lifeStart);
  const draft = draftId ? readDraft(draftId) : null;
  // A saved deck (or a draft) past the life's cap — a survivor's 45 after the bonus ended — opens with
  // every card and the reason (DeckBuilder.ReadOverCap); any other broken string, as the C# reads it.
  const read = draft ? DeckBuilder.ReadOverCap(draft.deck, limits)
    : ctx.savedDeck != null ? DeckBuilder.ReadOverCap(ctx.savedDeck, limits) : null;
  if (read) {
    deck = adopt(read.ok);
    notice = read.value;
    noticeOverCap = deck.Total > limits.DeckMax;
  } else {
    deck = adopt(DeckBuilder.Prototype(limits));
  }
  const supplies = DepartureSupplies.NewBuilder(limits);
  if (draft) {
    for (const id of draft.tools || []) supplies.AddTool(id);
    for (const id of draft.consumables || []) supplies.AddConsumable(id);
    if (draft.talent) supplies.ChooseTalent(deck, draft.talent);
  }
  let settingOut = false;
  function keepDraft() {
    if (!draftId || settingOut) return;
    writeDraft({ id: draftId, deck: deck.Save(), tools: [...supplies.Tools], consumables: [...supplies.Consumables], talent: supplies.Talent });
  }
  const filter = new DeckFilter();
  let page = 0;
  let talentPage = 0;
  let selected = null;
  let step = 'deck';
  let randomSeed = Number((BigInt(lifeStart && lifeStart.Seed != null ? lifeStart.Seed : (ctx.seed ?? 1)) & 0x3fffffffn)) || 1;
  const faces = new Map();
  const faceFor = (def) => { if (!faces.has(def.Id)) faces.set(def.Id, faceOf(def)); return faces.get(def.Id); };
  const isOwned = (id) => owned === null || owned.includes(id);
  // What the life's limits raise (the bridge's RaisedLine under DepartureBuilder.BonusLabel), beside a
  // step's title; nothing on a life without the survivor's bonus. Not a document rule: the documents show
  // the bonus in 継承の間 (CAMP_FACILITIES_DESIGN.md §3.1, tier1-core R1-14) and §3.2 出立 is silent;
  // docs/EXTENSIONS.md 「出立の枠」 試作で決めたこと 6 gives the reason (the raised numbers are used here).
  function bonusChip(line) {
    if (!line) return null;
    return h('span', { class: 'ocbg-meta-bonus' }, h('i', { class: 'ocbg-meta-bonus-dot' }),
      h('b', null, DepartureBuilder.BonusLabel), h('span', null, line));
  }

  // ---- header ----
  const backBtn = place(h('button', { class: 'ocbg-meta-back', type: 'button', onclick: () => back() },
    icon('back', 22, 'var(--ink)'), h('span', null, InheritanceText.ToHallButton)), 64, 24, null, 52);
  root.appendChild(backBtn);
  // Top right: 設定 (Esc) and the journal (J) when the router gives them (CAMP_FACILITIES_DESIGN.md §3.3 / §3.5).
  // Same order as 継承の間: the journal, then 設定 at the right edge.
  const corner = [];
  let cornerX = 1872;
  if (ctx.onSettings) {
    cornerX -= 120;
    corner.push(place(h('button', { class: 'ocbg-meta-chip-btn', type: 'button', onclick: () => openSettings() },
      h('span', null, MetaText.Settings), h('small', null, 'Esc')), cornerX, 24, 120, 52));
    cornerX -= 16;
  }
  if (ctx.onJournal) {
    cornerX -= 56;
    corner.push(place(h('button', { class: 'ocbg-meta-chip-btn is-square', type: 'button', title: JournalBook.Heading, 'aria-label': JournalBook.Heading,
      onclick: () => { if (!busy) ctx.onJournal(); } }, icon('journal', 30, 'var(--ink)')), cornerX, 24, 56, 52));
  }
  if (corner.length) cornerX -= 24;
  corner.forEach((el) => root.appendChild(el));
  const stepper = place(h('div', { class: 'ocbg-meta-stepper' }), 1236, 24, cornerX - 1236, 52);
  const stepDeck = h('span', { class: 'is-on' }, h('b', null, '1'), MetaText.StepDeck);
  const stepSup = h('span', null, h('b', null, '2'), MetaText.StepSupplies);
  stepper.appendChild(stepDeck);
  stepper.appendChild(h('i'));
  stepper.appendChild(stepSup);
  root.appendChild(stepper);

  // =================================================================================== deck step
  const deckPage = place(h('div', { class: 'ocbg-meta-dep-page' }), 0, 0, 1920, 1080);
  root.appendChild(deckPage);
  deckPage.appendChild(place(h('div', { class: 'ocbg-meta-dep-head' },
    h('h1', { class: 'ocbg-meta-dep-title' }, deck.Title), bonusChip(deck.RaisedLine)), 64, 88, 1160));
  const noticeEl = place(h('p', { class: 'ocbg-meta-dep-notice' }, notice), 1236, 92, 636);
  deckPage.appendChild(noticeEl);
  // the deck step's keys end on Esc like the supplies step's (Esc opens the settings here, else goes back)
  const deckKeys = place(h('p', { class: 'ocbg-meta-keys is-right' }, MetaText.DeckKeys + '　' + (ctx.onSettings ? MetaText.EscSettings : MetaText.EscBack)), 1236, 100, 636);
  deckKeys.style.display = notice ? 'none' : '';
  deckPage.appendChild(deckKeys);

  // filters (DeckBuilder.ShownAttributeOptions — the battle's words, X-17 — / CostOptions)
  const filters = place(h('div', { class: 'ocbg-meta-filters' }), 64, 146, 1140, 50);
  const attrBtns = DeckBuilder.ShownAttributeOptions.map((opt) => {
    const b = h('button', { type: 'button', class: 'ocbg-meta-filter', onclick: () => { filter.Attribute = opt.Value; page = 0; sound.play('uiClick'); drawFilters(); drawGrid(0); } },
      ATTR_ICON[opt.Value] ? icon(ATTR_ICON[opt.Value], 20, ATTR_INK[opt.Value]) : null, h('span', null, opt.Key));
    b.opt = opt;
    filters.appendChild(b);
    return b;
  });
  filters.appendChild(h('i', { class: 'ocbg-meta-filter-gap' }));
  const costBtns = DeckBuilder.CostOptions.map((opt) => {
    const b = h('button', { type: 'button', class: 'ocbg-meta-filter is-cost', onclick: () => { filter.Cost = opt.Value; page = 0; sound.play('uiClick'); drawFilters(); drawGrid(0); } },
      opt.Value > 0 ? h('i', { class: 'ocbg-meta-costdot' }, String(opt.Value)) : null, h('span', null, opt.Key));
    b.opt = opt;
    filters.appendChild(b);
    return b;
  });
  deckPage.appendChild(filters);
  function drawFilters() {
    attrBtns.forEach((b) => b.classList.toggle('is-on', filter.Attribute === b.opt.Value));
    costBtns.forEach((b) => b.classList.toggle('is-on', filter.Cost === b.opt.Value));
  }

  // the card grid
  const grid = place(h('div', { class: 'ocbg-meta-grid' }), 64, 214, 1140, 800);
  deckPage.appendChild(grid);
  let cells = [];
  function drawGrid(dir) {
    const p = deck.Page(filter, page, PerPage);
    page = p.Index;
    pageText.textContent = p.Text;
    prevBtn.disabled = p.Index === 0;
    nextBtn.disabled = p.Index >= p.Count - 1;
    grid.innerHTML = '';
    cells = p.Cards.map((def, i) => {
      const col = i % Columns, row = Math.floor(i / Columns);
      const face = faceFor(def);
      const card = scaledCard(face, FaceScale);
      card.classList.add('ocbg-meta-grid-card');
      const cell = place(h('div', { class: 'ocbg-meta-cell' + (isOwned(def.Id) ? '' : ' is-unowned') }), col * 190, row * 266, 182, 262);
      const press = h('button', { class: 'ocbg-meta-cell-face', type: 'button', 'aria-label': def.Name,
        onclick: () => choose(def), oncontextmenu: (e) => { e.preventDefault(); choose(def); }, onmouseenter: () => sound.play('nodeHover') }, card);
      cell.appendChild(press);
      if (!isOwned(def.Id)) cell.appendChild(h('span', { class: 'ocbg-meta-unowned' }, MetaText.NotOwned));
      const minus = h('button', { class: 'ocbg-meta-step', type: 'button', 'aria-label': '−', onclick: () => remove(def, cell) }, '−');
      const count = h('b', { class: 'ocbg-meta-count' });
      const plus = h('button', { class: 'ocbg-meta-step', type: 'button', 'aria-label': '＋', onclick: () => add(def, cell) }, '＋');
      cell.appendChild(h('div', { class: 'ocbg-meta-cell-row' }, minus, count, plus));
      cell.def = def; cell.count = count; cell.plus = plus; cell.minus = minus;
      grid.appendChild(cell);
      return cell;
    });
    refreshCells();
    if (dir) {
      cells.forEach((c, i) => {
        const s = styler(c, { o: 0, x: 36 * dir });
        clock.tween(s, { o: 1, x: 0 }, 240, 'out', (i % Columns) * 18).then(() => { c.style.transform = ''; });
      });
    }
  }
  function refreshCells() {
    for (const c of cells) {
      const text = deck.CountText(c.def.Id);
      c.count.textContent = text;
      c.classList.toggle('is-in', text !== '');
      c.plus.disabled = !deck.CanAdd(c.def.Id);
      c.minus.disabled = deck.CountOf(c.def.Id) === 0;
      c.classList.toggle('is-sel', selected !== null && selected.Id === c.def.Id);
    }
  }

  // page nav (DeckSelectScreen: 前のページ / n / m ページ（k 種）/ 次のページ)
  const prevBtn = h('button', { class: 'ocbg-meta-nav', type: 'button', onclick: () => turn(-1) }, icon('back', 20, 'var(--ink)'), MetaText.PrevPage);
  const pageText = h('span', { class: 'ocbg-meta-pagetext' });
  const nextBtn = h('button', { class: 'ocbg-meta-nav', type: 'button', onclick: () => turn(1) }, MetaText.NextPage, icon('forward', 20, 'var(--ink)'));
  deckPage.appendChild(place(h('div', { class: 'ocbg-meta-pagenav' }, prevBtn, pageText, nextBtn), 64, 1016, 1140, 48));
  function turn(by) {
    const before = page;
    page += by;
    const p = deck.Page(filter, page, PerPage);
    if (p.Index === before) { page = before; return; }
    sound.play('nodeHover');
    drawGrid(by);
  }

  // detail (DeckBuilder.DetailOf + the face's terms)
  const detail = place(h('section', { class: 'ocbg-meta-detail' }), 1236, 146, 636, 452);
  deckPage.appendChild(detail);
  function drawDetail() {
    detail.innerHTML = '';
    if (!selected) { detail.appendChild(h('p', { class: 'ocbg-meta-detail-hint' }, MetaText.PickCardHint)); return; }
    const face = faceFor(selected);
    const lines = DeckBuilder.ShownDetailOf(selected);
    const big = cardEl(face);
    place(big, 16, 16);
    detail.appendChild(big);
    const text = place(h('div', { class: 'ocbg-meta-detail-text' }), 252, 16, 368);
    text.appendChild(h('h3', null, lines[0]));
    lines.slice(1).forEach((l, i) => text.appendChild(h('p', { class: i === lines.length - 2 && l.startsWith('「') ? 'is-flavour' : '' }, l)));
    detail.appendChild(text);
    const terms = termRows(face);
    if (terms.length) detail.appendChild(place(h('div', { class: 'ocbg-meta-terms' }, ...terms), 16, 334, 604));
    const s = styler(big, { o: 0, y: 10 });
    clock.tween(s, { o: 1, y: 0 }, 200, 'out');
  }
  function choose(def) {
    if (selected && selected.Id === def.Id) return;
    selected = def;
    sound.play('uiClick');
    drawDetail();
    refreshCells();
    markChip();
  }

  // the count: how many, the life's 20〜40 (45 with the survivor's bonus), DeckBuilder.StatusText
  const countBox = place(h('section', { class: 'ocbg-meta-countbox' }), 1236, 614, 636, 150);
  const totalEl = h('b', { class: 'ocbg-meta-total' });
  const gaugeFill = h('i', { class: 'ocbg-meta-gauge-fill' });
  const gauge = h('div', { class: 'ocbg-meta-gauge' }, gaugeFill);
  const minPct = (100 * deckMin) / deckCap;
  gauge.appendChild(h('i', { class: 'ocbg-meta-gauge-min', style: { left: `${minPct}%` } }));
  const statusEl = h('p', { class: 'ocbg-meta-status' });
  countBox.appendChild(h('div', { class: 'ocbg-meta-count-row' },
    h('div', { class: 'ocbg-meta-total-wrap' }, totalEl, h('span', null, MetaText.CardsUnit)),
    h('div', { class: 'ocbg-meta-gauge-wrap' }, gauge,
      h('div', { class: 'ocbg-meta-gauge-labels' },
        h('span', { style: { left: `${minPct}%` } }, String(deckMin)),
        h('span', { style: { left: '100%' } }, String(deckCap))))));
  countBox.appendChild(statusEl);
  deckPage.appendChild(countBox);
  const total = rollNumber(clock, totalEl, deck.Total);

  // The deck as listed (DeckBuilder.DeckLines), one chip a kind. Three whole rows show; a longer deck
  // scrolls by whole rows (wheel, or Shift+↑↓), fades at the edge it continues past and says so in its
  // heading, like 継承の間's lists. A chip picks its card and turns the grid to it (so its − is at hand);
  // the picked card's chip is marked and kept in view, so the cursor keys reach the list too.
  const listKinds = h('small', { class: 'ocbg-meta-decklist-kinds' });
  const listMore = h('span', { class: 'ocbg-meta-decklist-more' });
  const listBox = place(h('section', { class: 'ocbg-meta-decklist' },
    h('header', null, h('h4', null, MetaText.DeckListHeading), listKinds, listMore)), 1236, 776, 636, 128);
  const listBody = h('div', { class: 'ocbg-meta-decklist-body' });
  listBox.appendChild(listBody);
  deckPage.appendChild(listBox);
  listBody.addEventListener('scroll', listEdge, { passive: true });
  function listEdge() {
    const room = listBody.scrollHeight - listBody.clientHeight;
    const more = listBody.clientHeight > 0 && room > 2;
    const below = more && listBody.scrollTop < room - 2;
    listBody.classList.toggle('is-more-below', below);
    listBody.classList.toggle('is-more-above', more && listBody.scrollTop > 2);
    listMore.classList.toggle('is-up', more && !below);
    listMore.textContent = more ? MetaText.DeckListMore : '';
  }
  /** One row of chips (a chip's height and the row gap, as meta.css lays them out). */
  function listRow() {
    const first = listBody.firstElementChild;
    return first ? first.offsetHeight + 6 : 30;
  }
  function scrollList(rows) {
    const before = listBody.scrollTop;
    listBody.scrollTop = Math.max(0, Math.min(listBody.scrollHeight - listBody.clientHeight, before + rows * listRow()));
    if (listBody.scrollTop !== before) sound.play('nodeHover');
    listEdge();
  }
  function drawList() {
    const lines = deck.DeckLines();
    // DeckLines is the deck's kinds in canon order, as Build lays them out (copies side by side).
    const kinds = [];
    for (const card of deck.Build()) if (kinds[kinds.length - 1] !== card.Def) kinds.push(card.Def);
    const top = listBody.scrollTop;
    listBody.innerHTML = '';
    lines.forEach((line, i) => {
      const def = kinds[i];
      const chip = h('button', { type: 'button', class: 'ocbg-meta-deckchip', tabindex: '-1', 'data-id': def ? def.Id : null,
        onclick: () => { if (def && !busy) reveal(def); } }, line);
      listBody.appendChild(chip);
    });
    listKinds.textContent = lines.length ? MetaText.OwnedCount(lines.length) : '';
    listBody.scrollTop = top;
  }
  /** Marks the picked card's chip and scrolls it into view; then the edges and the cue follow the list. */
  function markChip() {
    let on = null;
    for (const chip of listBody.children) {
      const is = !!selected && chip.getAttribute('data-id') === selected.Id;
      chip.classList.toggle('is-sel', is);
      if (is) on = chip;
    }
    if (on && listBody.clientHeight > 0) {
      const top = on.offsetTop;
      const bottom = top + on.offsetHeight;
      if (top < listBody.scrollTop) listBody.scrollTop = top;
      else if (bottom > listBody.scrollTop + listBody.clientHeight) listBody.scrollTop = bottom - listBody.clientHeight;
    }
    listEdge();
  }
  /** A chip pressed: its card is picked and the grid turns to the page that holds it. */
  function reveal(def) {
    let kept = deck.Filter(filter);
    let at = kept.findIndex((d) => d.Id === def.Id);
    if (at < 0) {
      // The filter hides it: back to 全て / 全コスト, as the deck list shows every kind.
      filter.Attribute = BattleAttribute.None;
      filter.Cost = 0;
      drawFilters();
      kept = deck.Filter(filter);
      at = kept.findIndex((d) => d.Id === def.Id);
      page = -1;
    }
    const target = Math.max(0, Math.floor(at / PerPage));
    if (target !== page) {
      const dir = page < 0 ? 0 : target > page ? 1 : -1;
      page = target;
      drawGrid(dir);
    }
    if (selected && selected.Id === def.Id) { sound.play('uiClick'); refreshCells(); markChip(); return; }
    choose(def);
  }

  // presets
  const presetRow = place(h('div', { class: 'ocbg-meta-presets' },
    h('button', { class: 'ocbg-meta-btn', type: 'button', onclick: () => replace(adopt(DeckBuilder.Prototype(limits))) }, MetaText.Prototype),
    h('button', { class: 'ocbg-meta-btn', type: 'button', onclick: () => replace(randomDeck()) }, MetaText.Random),
    h('button', { class: 'ocbg-meta-btn', type: 'button', onclick: () => replace(new DeckBuilder(owned, limits)) }, MetaText.Clear)), 1236, 916, 636, 50);
  deckPage.appendChild(presetRow);
  const toSupplies = place(h('button', { class: 'ocbg-meta-primary', type: 'button', onclick: () => showSupplies() },
    h('span', null, MetaText.ToSupplies), h('small', null, 'Enter')), 1236, 984, 636, 72);
  deckPage.appendChild(toSupplies);

  /**
   * 「ランダム」: DeckBuilder.Random holds §8 and its closers, but it draws from the whole catalogue.
   * For a character that owns fewer kinds, its picks go through Add on a builder of the owned kinds
   * (which refuses the rest) and further seeded decks top it up to the same size.
   */
  function randomDeck() {
    const b = new DeckBuilder(owned, limits);
    for (let tries = 0; tries < 16 && b.Total < DeckBuilder.RandomDefaultSize; tries++) {
      const r = DeckBuilder.Random(randomSeed++, DeckBuilder.RandomDefaultSize, limits);
      for (const card of r.Build()) {
        if (b.Total >= DeckBuilder.RandomDefaultSize) break;
        b.Add(card.Def.Id);
      }
    }
    return b;
  }
  function replace(next) {
    deck = next;
    setNotice('');
    sound.play('rewardReveal');
    refreshDeck();
    cells.forEach((c, i) => { const s = styler(c.count, { o: 0, s: 1.4 }); clock.tween(s, { o: 1, s: 1 }, 260, 'out', i * 12); });
  }
  function add(def, cell) {
    if (deck.Add(def.Id)) {
      sound.play('cardAdded');
      if (!selected || selected.Id !== def.Id) { selected = def; drawDetail(); }
      refreshDeck();
      const s = styler(cell.count, { s: 1.5 });
      clock.tween(s, { s: 1 }, 240, 'out');
    } else {
      sound.play('refusal');
      shake(cell.plus);
    }
  }
  function remove(def, cell) {
    if (deck.Remove(def.Id)) {
      sound.play('uiCancel');
      refreshDeck();
      const s = styler(cell.count, { s: 0.7 });
      clock.tween(s, { s: 1 }, 200, 'out');
    }
  }
  function shake(el) {
    const s = styler(el);
    const seq = [6, -6, 4, -4, 0];
    seq.reduce((p, x) => p.then(() => clock.tween(s, { x }, 32, 'linear')), Promise.resolve()).then(() => { el.style.transform = ''; });
  }
  function refreshDeck() {
    refreshCells();
    total.to(deck.Total);
    gaugeFill.style.width = `${Math.min(100, (100 * deck.Total) / deckCap)}%`;
    gauge.classList.toggle('is-ok', deck.IsValid);
    statusEl.textContent = deck.StatusText;
    statusEl.classList.toggle('is-ok', deck.IsValid);
    drawList();
    markChip();
    if (noticeOverCap && deck.Total <= deckCap) setNotice('');
    toSupplies.disabled = !deck.IsValid;
    keepDraft();
  }
  function setNotice(text) {
    notice = text;
    noticeOverCap = false;
    noticeEl.textContent = text;
    deckKeys.style.display = text ? 'none' : '';
  }

  // =================================================================================== supplies step
  const supPage = place(h('div', { class: 'ocbg-meta-dep-page' }), 0, 0, 1920, 1080);
  supPage.style.display = 'none';
  root.appendChild(supPage);
  supPage.appendChild(place(h('div', { class: 'ocbg-meta-dep-head' },
    h('h1', { class: 'ocbg-meta-dep-title' }, supplies.Title), bonusChip(supplies.RaisedLine)), 64, 88, 1160));
  supPage.appendChild(place(h('p', { class: 'ocbg-meta-keys is-right' }, MetaText.SupplyKeys + '　' + (ctx.onSettings ? MetaText.EscSettings : MetaText.EscBack)), 1236, 100, 636));

  supPage.appendChild(place(h('h2', { class: 'ocbg-meta-dep-h2' }, supplies.ToolHeading), 64, 146, 1140));
  const toolSlotRow = place(h('div', { class: 'ocbg-meta-slots' }), 64, 184, 1140, 64);
  supPage.appendChild(toolSlotRow);
  const toolGrid = place(h('div', { class: 'ocbg-meta-supgrid' }), 64, 262, 1140, 360);
  supPage.appendChild(toolGrid);
  supPage.appendChild(place(h('h2', { class: 'ocbg-meta-dep-h2' }, supplies.ConsumableHeading), 64, 636, 1140));
  const consSlotRow = place(h('div', { class: 'ocbg-meta-slots' }), 64, 674, 1140, 64);
  supPage.appendChild(consSlotRow);
  const consGrid = place(h('div', { class: 'ocbg-meta-supgrid' }), 64, 752, 1140, 270);
  supPage.appendChild(consGrid);

  function supplyCard(option, kind) {
    const item = kind === 'tool' ? ItemCatalogue.Tool(option.Id) : ItemCatalogue.Consumable(option.Id);
    const battle = item.Side === EffectSide.Battle;
    const count = h('b', { class: 'ocbg-meta-count' });
    const minus = h('button', { class: 'ocbg-meta-step', type: 'button', 'aria-label': '−', onclick: () => {
      if (kind === 'tool' ? supplies.RemoveTool(option.Id) : supplies.RemoveConsumable(option.Id)) { sound.play('uiCancel'); refreshSupplies(); }
    } }, '−');
    const plus = h('button', { class: 'ocbg-meta-step', type: 'button', 'aria-label': '＋', onclick: () => {
      if (kind === 'tool' ? supplies.AddTool(option.Id) : supplies.AddConsumable(option.Id)) { sound.play('consumableUse'); refreshSupplies(); pulse(card); }
      else { sound.play('refusal'); shake(plus); }
    } }, '＋');
    const card = h('div', { class: 'ocbg-meta-supply' + (battle ? ' is-battle' : '') },
      h('div', { class: 'ocbg-meta-supply-text' },
        h('div', { class: 'ocbg-meta-supply-head' },
          h('b', null, option.Name), h('small', null, item.Reading),
          h('span', { class: 'ocbg-meta-side' }, icon(battle ? 'range' : 'stairs', 16, 'var(--ink)'), battle ? MetaText.SideBattle : MetaText.SideExplore)),
        h('p', null, playerNote(option.Note))),
      h('div', { class: 'ocbg-meta-cell-row' }, minus, count, plus));
    card.option = option; card.count = count; card.plus = plus; card.minus = minus; card.kind = kind;
    return card;
  }
  function pulse(el) {
    el.classList.remove('is-pulse');
    void el.offsetWidth;
    el.classList.add('is-pulse');
  }
  const toolCards = supplies.ToolOptions.map((o) => supplyCard(o, 'tool'));
  const consCards = supplies.ConsumableOptions.map((o) => supplyCard(o, 'consumable'));
  toolCards.forEach((c, i) => toolGrid.appendChild(place(c, (i % 2) * 580, Math.floor(i / 2) * 90, 560, 82)));
  consCards.forEach((c, i) => consGrid.appendChild(place(c, (i % 2) * 580, Math.floor(i / 2) * 90, 560, 82)));

  function drawSlots(row, ids, count, options, removeFn, isBonus) {
    row.innerHTML = '';
    for (let i = 0; i < count; i++) {
      const id = ids[i];
      const opt = id ? options.find((o) => o.Id === id) : null;
      const bonus = isBonus ? isBonus(i) : false;
      const plate = h('button', { type: 'button', class: 'ocbg-meta-slot' + (opt ? ' is-full' : '') + (bonus ? ' is-bonus' : ''), disabled: opt ? null : 'disabled',
        title: bonus ? DepartureBuilder.BonusLabel : null,
        onclick: () => { if (opt && removeFn(id)) { sound.play('uiCancel'); refreshSupplies(); } } },
      h('span', { class: 'ocbg-meta-slot-n' }, String(i + 1)),
      h('b', null, opt ? opt.Name : MetaText.EmptySlot),
      opt ? h('i', { class: 'ocbg-meta-slot-x' }, icon('exile', 16, 'var(--ink)')) : null);
      row.appendChild(place(plate, i * 224, 0, 212, 64));
    }
  }

  // talent (DepartureBuilder.TalentPage) and the info panel
  supPage.appendChild(place(h('h2', { class: 'ocbg-meta-dep-h2 is-wrap' }, DepartureBuilder.TalentHeading), 1236, 146, 636));
  const talentGrid = place(h('div', { class: 'ocbg-meta-talents' }), 1236, 212, 636, 300);
  supPage.appendChild(talentGrid);
  const tPrev = h('button', { class: 'ocbg-meta-nav is-small', type: 'button', onclick: () => { talentPage--; sound.play('nodeHover'); drawTalent(); } }, icon('back', 18, 'var(--ink)'), MetaText.PrevPage);
  const tText = h('span', { class: 'ocbg-meta-pagetext' });
  const tNext = h('button', { class: 'ocbg-meta-nav is-small', type: 'button', onclick: () => { talentPage++; sound.play('nodeHover'); drawTalent(); } }, MetaText.NextPage, icon('forward', 18, 'var(--ink)'));
  supPage.appendChild(place(h('div', { class: 'ocbg-meta-pagenav' }, tPrev, tText, tNext), 1236, 520, 636, 40));
  function drawTalent() {
    talentGrid.innerHTML = '';
    const p = DepartureBuilder.TalentPage(deck, talentPage, TalentPerPage);
    talentPage = p.Index;
    tText.textContent = p.Text;
    tPrev.disabled = p.Index === 0;
    tNext.disabled = p.Index >= p.Count - 1;
    const chosen = supplies.TalentIn(deck);
    p.Cards.forEach((def, i) => {
      const on = def.Id === chosen;
      const b = h('button', { type: 'button', class: 'ocbg-meta-talent' + (on ? ' is-on' : ''),
        onclick: () => { if (supplies.ChooseTalent(deck, def.Id)) { sound.play('uiConfirm'); refreshSupplies(); } } },
      icon(ATTR_ICON[def.Attribute] || 'star', 18, on ? 'var(--ground)' : (ATTR_INK[def.Attribute] || 'var(--ink)')), h('span', null, def.Name));
      talentGrid.appendChild(place(b, (i % TalentColumns) * 214, Math.floor(i / TalentColumns) * 50, 204, 42));
    });
  }

  const info = place(h('section', { class: 'ocbg-meta-info' }), 1236, 576, 636, 324);
  supPage.appendChild(info);
  const lane = h('div', { class: 'ocbg-meta-lane' });
  const infoLines = h('ul', { class: 'ocbg-meta-info-lines' });
  info.appendChild(h('h4', null, MetaText.StartHeading));
  info.appendChild(lane);
  info.appendChild(infoLines);
  const supStatus = place(h('p', { class: 'ocbg-meta-status' }), 1236, 912, 636);
  supPage.appendChild(supStatus);
  const backToDeck = place(h('button', { class: 'ocbg-meta-btn', type: 'button', onclick: () => showDeck() }, DepartureBuilder.BackLabel), 1236, 972, 236, 72);
  const setOutBtn = place(h('button', { class: 'ocbg-meta-primary', type: 'button', onclick: () => setOut() },
    h('span', null, DepartureBuilder.SetOutLabel), h('small', null, 'Enter')), 1488, 972, 384, 72);
  supPage.appendChild(backToDeck);
  supPage.appendChild(setOutBtn);

  // The start a tool moves back (DepartureBuilder.PlayerStartCell / StartGap), drawn on six cells.
  function drawLane() {
    lane.innerHTML = '';
    const cellW = 88;
    const pc = supplies.PlayerStartCell;
    const gap = supplies.StartGap;
    const foe = pc + gap + 1;
    const cellsN = Math.max(6, foe);
    for (let i = 1; i <= cellsN; i++) {
      const t = O.FloorTile(cellW - 6);
      place(t, (i - 1) * cellW + 3, 58);
      if (i === pc) { t.reach.style.opacity = 0.65; t.reachLine.style.opacity = 1; }
      lane.appendChild(t);
    }
    const me = place(h('div', { class: 'ocbg-meta-lane-unit' }, icon('person', 34, 'var(--amber)')), (pc - 1) * cellW, 12, cellW, 44);
    const them = place(h('div', { class: 'ocbg-meta-lane-unit' }, icon('person', 34, 'var(--violet)')), (foe - 1) * cellW, 12, cellW, 44);
    lane.appendChild(me);
    lane.appendChild(them);
    const chip = O.DistanceChip(gap, gap * cellW);
    place(chip, pc * cellW, 61);
    lane.appendChild(chip);
  }

  function refreshSupplies() {
    for (const c of toolCards.concat(consCards)) {
      const n = c.kind === 'tool' ? supplies.ToolCount(c.option.Id) : supplies.ConsumableCount(c.option.Id);
      c.count.textContent = n === 0 ? '' : '×' + n;
      c.classList.toggle('is-in', n > 0);
      c.plus.disabled = !(c.kind === 'tool' ? supplies.CanAddTool(c.option.Id) : supplies.CanAddConsumable(c.option.Id));
      c.minus.disabled = n === 0;
    }
    drawSlots(toolSlotRow, supplies.Tools, supplies.Limits.ToolSlots, supplies.ToolOptions, (id) => supplies.RemoveTool(id), (i) => supplies.IsBonusToolSlot(i));
    drawSlots(consSlotRow, supplies.Consumables, supplies.Limits.ConsumableSlots, supplies.ConsumableOptions, (id) => supplies.RemoveConsumable(id));
    drawTalent();
    drawLane();
    infoLines.innerHTML = '';
    [supplies.TalentLine(deck), supplies.StartLine, supplies.ToolsLine, supplies.ConsumablesLine, MetaText.DeckCount(deck.Total)]
      .forEach((l) => infoLines.appendChild(h('li', null, l)));
    supStatus.textContent = supplies.StatusText(deck);
    const ok = supplies.CanSetOut(deck);
    supStatus.classList.toggle('is-ok', ok);
    setOutBtn.disabled = !ok;
    keepDraft();
  }

  // =================================================================================== steps
  async function showSupplies() {
    if (busy || !deck.IsValid || step !== 'deck') return;
    busy = true;
    sound.play('uiConfirm');
    step = 'supplies';
    talentPage = 0;
    refreshSupplies();
    stepDeck.classList.remove('is-on'); stepSup.classList.add('is-on');
    backBtn.lastChild.textContent = DepartureBuilder.BackLabel;
    const a = styler(deckPage), b = styler(supPage, { o: 0, x: 80 });
    supPage.style.display = '';
    clock.tween(a, { o: 0, x: -80 }, 260, 'in');
    await clock.tween(b, { o: 1, x: 0 }, 340, 'out', 140);
    deckPage.style.display = 'none';
    supPage.style.transform = '';
    busy = false;
  }
  async function showDeck() {
    if (busy || step !== 'supplies') return;
    busy = true;
    sound.play('uiCancel');
    step = 'deck';
    refreshDeck();
    drawGrid(0);
    stepSup.classList.remove('is-on'); stepDeck.classList.add('is-on');
    backBtn.lastChild.textContent = InheritanceText.ToHallButton;
    const a = styler(supPage), b = styler(deckPage, { o: 0, x: -80 });
    deckPage.style.display = '';
    markChip();   // the list is measured now that the page shows again
    clock.tween(a, { o: 0, x: 80 }, 260, 'in');
    await clock.tween(b, { o: 1, x: 0 }, 340, 'out', 140);
    supPage.style.display = 'none';
    deckPage.style.transform = '';
    busy = false;
  }
  function back() {
    if (step === 'supplies') { showDeck(); return; }
    if (busy) return;
    sound.play('uiCancel');
    leave(() => ctx.onBack && ctx.onBack());
  }
  function setOut() {
    if (busy || !supplies.CanSetOut(deck)) return;
    const departure = supplies.Build(deck);
    // The tools that act inside a battle go to the battles (Expedition's battleToolIds); the whole
    // loadout, with the life's slot count, goes to the exploration (#104) — the router reads it with
    // toRunLoadout(loadout, limits.ToolSlots) (DepartureSupplies.Carry).
    const { battleToolIds, consumableIds: consumables, loadout } = DepartureSupplies.Carry(departure, limits);
    settingOut = true;
    clearDraft();   // the life sets out with this deck; the router keeps it from here (ctx.savedDeck)
    sound.play('uiConfirm');
    leave(() => ctx.onSetOut && ctx.onSetOut({
      deck: departure.Deck,
      deckBuilder: deck,
      deckSave: deck.Save(),
      battleToolIds,
      consumableIds: consumables,
      loadout,
      talent: departure.TalentCardId,
      departure,
      limits,
    }));
  }
  function openSettings() {
    if (busy || !ctx.onSettings) return;
    sound.play('uiClick');
    ctx.onSettings();
  }
  async function leave(cb) {
    busy = true;
    const fade = styler(place(h('div', { class: 'ocbg-meta-fade' }), 0, 0, 1920, 1080), { o: 0 });
    root.appendChild(fade.el);
    await clock.tween(fade, { o: 1 }, 520, 'in');
    if (alive) cb();
  }

  // =================================================================================== first frame
  // The header goes over both pages (they fill the stage), so its buttons take the pointer: 「← 継承の間へ」, the
  // stepper and the corner (設定, 手記), all under the entrance fade that follows.
  root.appendChild(backBtn);
  root.appendChild(stepper);
  corner.forEach((el) => root.appendChild(el));
  drawFilters();
  drawGrid(0);
  selected = cells.length ? cells[0].def : null;
  refreshDeck();
  drawDetail();
  const enter = styler(place(h('div', { class: 'ocbg-meta-fade' }), 0, 0, 1920, 1080), { o: 1 });
  root.appendChild(enter.el);
  clock.tween(enter, { o: 0 }, 500, 'out').then(() => enter.el.remove());
  cells.forEach((c, i) => {
    const s = styler(c, { o: 0, y: 18 });
    clock.tween(s, { o: 1, y: 0 }, 380, 'out', 150 + i * 28).then(() => { c.style.transform = ''; });
  });

  // ---- keyboard: a cursor over the page's cards (the selected card), + / − for its count ----
  function cursorIndex() { return selected ? cells.findIndex((c) => c.def.Id === selected.Id) : -1; }
  function moveCursor(by, rowStep) {
    const i = cursorIndex();
    if (i < 0) { if (cells.length) choose(cells[0].def); return; }
    const j = i + by;
    if (rowStep) { if (j >= 0 && j < cells.length) choose(cells[j].def); return; }
    if (j >= 0 && j < cells.length) { choose(cells[j].def); return; }
    const before = page;
    turn(by < 0 ? -1 : 1);
    if (page !== before && cells.length) choose(by < 0 ? cells[cells.length - 1].def : cells[0].def);
  }
  function cursorCell() { const i = cursorIndex(); return i >= 0 ? cells[i] : null; }
  // A button reached with Tab takes Enter itself (the browser presses it); a button the pointer last
  // pressed keeps focus too, but Enter then means the screen's own Enter. (:focus-visible cannot tell
  // them apart: any key press makes the focused button match it.)
  let tabbed = false;
  root.addEventListener('pointerdown', () => { tabbed = false; }, true);
  const keyboardFocus = () => {
    const a = document.activeElement;
    return tabbed && !!(a && a !== document.body && root.contains(a) && !a.disabled);
  };

  const popKeys = pushKeys((e) => {
    if (e.key === 'Tab') { tabbed = true; return false; }
    if (busy) return false;
    // a held key repeats: only a fresh press opens an overlay or goes back (the overlays close on a fresh press only)
    if (e.key === 'Escape') { if (!e.repeat) { if (ctx.onSettings) openSettings(); else back(); } return true; }
    if ((e.key === 'j' || e.key === 'J') && ctx.onJournal) { if (!e.repeat) ctx.onJournal(); return true; }
    if (e.key === 'Enter' && keyboardFocus()) return false;
    if (step === 'deck') {
      if (e.shiftKey && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) { scrollList(e.key === 'ArrowDown' ? 1 : -1); return true; }
      if (e.key === 'PageUp') { turn(-1); return true; }
      if (e.key === 'PageDown') { turn(1); return true; }
      if (e.key === 'ArrowLeft') { moveCursor(-1); return true; }
      if (e.key === 'ArrowRight') { moveCursor(1); return true; }
      if (e.key === 'ArrowUp') { moveCursor(-Columns, true); return true; }
      if (e.key === 'ArrowDown') { moveCursor(Columns, true); return true; }
      if (e.key === '+' || e.key === '=' || e.key === ';') { const c = cursorCell(); if (c) add(c.def, c); return true; }
      if (e.key === '-' || e.key === '_' || e.key === 'Backspace' || e.key === 'Delete') { const c = cursorCell(); if (c) remove(c.def, c); return true; }
      if (e.key === 'Enter') { if (deck.IsValid) showSupplies(); else sound.play('refusal'); return true; }
    } else {
      if (e.key === 'ArrowLeft' || e.key === 'PageUp') { if (!tPrev.disabled) { talentPage--; sound.play('nodeHover'); drawTalent(); } return true; }
      if (e.key === 'ArrowRight' || e.key === 'PageDown') { if (!tNext.disabled) { talentPage++; sound.play('nodeHover'); drawTalent(); } return true; }
      if (e.key === 'Enter') { if (supplies.CanSetOut(deck)) setOut(); else sound.play('refusal'); return true; }
    }
    return false;
  });

  return {
    destroy() {
      alive = false;
      popKeys();
      clock.stop();
      root.remove();
    },
  };
}

