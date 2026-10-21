// The three piles and their lists: ports of PilesView and PileListView (battle-visual-v1 §4.7, battle_ui_ux_v2
// §1.1 C4 / §3.13, #455), in the design system's pile and drawer (未決の見た目 1, 案 A) looks.
//
// Piles: draw, discard and exile, each a 64×84 card back with its 付録 A icon at 28 px, 22 px apart from
// x 32, y 948 (PileLayout). Thickness is sheets laid 4 px apart behind the front one (PileLayout.SheetsBehind);
// an empty pile is a steel dashed slot. No number is written (B9: the piles read by their thickness).
// A click on a pile opens its list; the same pile again closes it.
//
// The list: a drawer from the left (the journal drawer's mirror image) taking 30 % of the screen
// (PileList.WidthShare), with a tab per pile and one row per kind of card, "突き 2 / 3" (PileCard: how many
// the pile holds / how many the deck holds), in the order given. The rows follow every frame.
import { h } from '../../common/stage.js';
import { PileKind, PileLayout } from '../../../bridge/depictionScript/PileLayout.js';
import { PileList } from '../../../bridge/depictionScript/PileList.js';
import { JournalBook, JournalContext } from '../../../bridge/journal/JournalBook.js';
import { CardKind } from '../../../bridge/depictionScript/DepictionTypes.js';
import { CoreText } from '../../../bridge/depiction/CoreText.js';
import { CardCatalog } from '../../../core/battle/CardCatalog.js';
import { PILE_ICON, tok, lineTip } from './util.js';

/**
 * DS PileDrawer (案 A, the 576 px drawer this list is; review 19 — the rows were bare text): each 40 px row carries a
 * 6×24 band in its card's attribute colour at its left, so the list scans by attribute. The row (PileCard) names the
 * card only; its attribute is the catalogue's (CardCatalog by name → CoreText.KindOf, as the writer colours a face),
 * read for its colour alone. A name the catalogue lacks gets the steel band.
 */
const KIND_BAND = { [CardKind.Attack]: 'var(--violet)', [CardKind.Guard]: 'var(--guard)', [CardKind.Skill]: 'var(--amber)', [CardKind.Stance]: 'var(--crimson)' };
let kindByName = null;
function bandOf(name) {
  if (!kindByName) {
    kindByName = new Map();
    try { for (const def of CardCatalog.All) if (def && def.Name && !kindByName.has(def.Name)) kindByName.set(def.Name, CoreText.KindOf(def.Attribute)); } catch (err) { /* colour only */ }
  }
  return KIND_BAND[kindByName.get(name)] || 'var(--steel)';
}
/**
 * battle_ui_ux_v2 §1.1 C4 / DS PileDrawer 案 A (review 19: the ログ tab was missing, as in Unity's PileListView): the fourth
 * tab, ログ全文, holds the battle's log (player.js builds it from the script's own words). scene text: the DS's tab word and
 * its empty line.
 */
const LOG_KIND = 'Log';
const LOG_TAB = 'ログ全文';
const LOG_EMPTY = 'ログ全文は、まだ中身がありません。';

/**
 * battle_ui_ux_v2 §3.5 (review fix): with the list open the cards lift on hover but are not taken, and 「閉じるとプレイ
 * できます」 stands beside the close button — the journal drawer's own closing line (JournalBook.ClosingHint, battle).
 */
const CLOSING_HINT = new JournalBook([], JournalContext.Battle).ClosingHint;

const KINDS = [PileKind.Draw, PileKind.Discard, PileKind.Exile];
/** DS 未決 1 案 A: the drawer slides in from the left over 220 ms (ease-out). */
const DRAWER_OPEN_MS = 220;
/** DS 案 A: a row is 40 px tall. */
const ROW_HEIGHT = 40;
/**
 * The pile's hover line names the pile only (PileList.TabLabel): no count, as battle_ui_ux_v2 §1.1 B9
 * (「枚数の文は出さない」) and PilesView ("No number is written") say — review fix. Scene choice (Unity shows no
 * pile tooltip): it is a small caption on the hovered pile itself, under its icon, inside its 64×84 face — over the
 * pile it covered the stamina tag's pips, under it it fell inside the screen's 24 px margin, and beside it (the
 * piles stand 22 px apart) it lay over the next pile. 16 px in ink on the opaque panel, 22 tall, 4 px over the
 * pile's bottom edge, so the 28 px icon (its middle on the face's) stays clear.
 */
const CAPTION_H = 22;
const CAPTION_BOTTOM = 4;

export function createPiles(env, opts = {}) {
  const { OCB, layer, clock, L } = env;
  const hosts = {};
  let frame = null;
  let shown = false;

  for (const kind of KINDS) {
    const host = h('div', { class: 'ocbg-battle-pile ocbg-battle-hit ocb-pile', role: 'button', 'data-pile': kind });
    host.style.left = PileLayout.LeftOf(kind) + 'px';
    host.style.top = PileLayout.Top + 'px';
    host.style.width = PileLayout.Width + 'px';
    host.style.height = PileLayout.Height + 'px';
    host.addEventListener('click', () => { if (opts.onToggle) opts.onToggle(kind); });
    const self = {};
    host.addEventListener('mouseenter', () => {
      if (!opts.tip || !shown) return;
      const cap = lineTip(PileList.TabLabel(kind));
      cap.classList.add('ocbg-battle-pilecap');
      opts.tip.show(cap, (n) => {
        n.style.left = PileLayout.LeftOf(kind) + PileLayout.Width / 2 - n.offsetWidth / 2 + 'px';
        n.style.top = PileLayout.Top + PileLayout.Height - CAPTION_BOTTOM - CAPTION_H + 'px';
      }, self);
    });
    host.addEventListener('mouseleave', () => { if (opts.tip) opts.tip.hide(self); });
    layer.hud.appendChild(host);
    hosts[kind] = { el: host, key: '', count: 0 };
  }

  /** PilesView.Bind: the sheets behind, the empty slot, the icon. */
  function drawPile(kind, count) {
    const p = hosts[kind];
    p.count = count;
    p.el.dataset.count = String(count); // what the pile stands for (read by the dev pages' screen check)
    const behind = PileLayout.SheetsBehind(count);
    const empty = PileLayout.IsEmpty(count);
    const key = behind + '/' + empty;
    if (key === p.key) return;
    p.key = key;
    p.el.innerHTML = '';
    p.el.classList.toggle('is-empty', empty);
    for (let i = behind; i >= 1; i--) {
      const sheet = h('i', { class: 'ocb-pile-sheet' });
      sheet.style.top = -PileLayout.SheetStep * i + 'px';
      p.el.appendChild(sheet);
    }
    p.el.appendChild(h('div', { class: 'ocb-pile-face' }, OCB.icon(PILE_ICON[kind], PileLayout.IconSize, empty ? tok('steel') : tok('ink'))));
  }

  // ---- the drawer ---------------------------------------------------------------------------
  const width = Math.round(L.W * PileList.WidthShare);
  let drawer = null;
  let openKind = PileKind.Draw;
  let slide = 0;

  let logLines = [];
  function drawDrawer() {
    if (!drawer) return;
    drawer.body.innerHTML = '';
    drawer.tabs.innerHTML = '';
    for (const kind of KINDS.concat([LOG_KIND])) {
      const tab = h('div', { class: 'ocb-drawer-tab ocbg-battle-drawer-tab' + (kind === openKind ? ' is-on' : ''), role: 'button' }, kind === LOG_KIND ? LOG_TAB : PileList.TabLabel(kind));
      tab.addEventListener('click', () => {
        if (kind === openKind) return;
        env.audio.play('uiClick');
        openKind = kind;
        drawDrawer();
      });
      drawer.tabs.appendChild(tab);
    }
    if (openKind === LOG_KIND) {
      if (!logLines.length) { drawer.body.appendChild(h('p', { class: 'ocbg-battle-drawer-empty' }, LOG_EMPTY)); return; }
      const list = h('div', { class: 'ocbg-battle-drawer-log' }, logLines.map((line) => h('div', { class: 'ocbg-battle-log-line' + (/^—/.test(line) ? ' is-head' : '') }, line)));
      drawer.body.appendChild(list);
      drawer.body.scrollTop = drawer.body.scrollHeight;
      return;
    }
    const rows = frame && frame.Piles && frame.Piles.CardsOf ? frame.Piles.CardsOf(openKind) : [];
    if (!rows || rows.length === 0) {
      drawer.body.appendChild(h('p', { class: 'ocbg-battle-drawer-empty' }, PileList.Empty));
      return;
    }
    for (const r of rows) {
      // PileCard.RowText ("突き 2 / 3"), laid out as the DS row: the attribute band, the name, the two counts at the right.
      const band = h('i', { class: 'ocbg-battle-drawer-band' });
      const row = h('div', { class: 'ocb-drawer-row ocbg-battle-drawer-row', title: r.RowText }, band,
        h('span', { class: 'ocbg-battle-drawer-name' }, r.Name || ''),
        h('span', { class: 'ocb-mono' }, r.InPile + ' / ' + r.InDeck));
      row.style.setProperty('--attr', bandOf(r.Name));
      drawer.body.appendChild(row);
    }
  }

  function placeDrawer() {
    if (drawer) drawer.el.style.translate = (-width * (1 - slide)).toFixed(1) + 'px 0';
  }

  function open(kind) {
    openKind = kind;
    if (!drawer) {
      const tabs = h('div', { class: 'ocb-drawer-tabs' });
      const close = h('div', { class: 'ocbg-battle-drawer-close', role: 'button' }, PileList.CloseLabel);
      close.addEventListener('click', () => { env.audio.play('uiClick'); api.close(); });
      const hint = CLOSING_HINT ? h('div', { class: 'ocbg-battle-drawer-hint' }, CLOSING_HINT) : null;
      const head = h('div', { class: 'ocbg-battle-drawer-head' }, tabs, hint, close);
      const body = h('div', { class: 'ocbg-battle-drawer-body' });
      const el = h('div', { class: 'ocb-drawer ocbg-battle-drawer ocbg-battle-hit' }, head, body);
      el.style.width = width + 'px';
      // the cards and the piles under the drawer take no click through it
      el.addEventListener('pointerdown', (e) => e.stopPropagation());
      layer.over.appendChild(el);
      drawer = { el, tabs, body };
      slide = 0;
      placeDrawer();
      const token = (drawer.token = {});
      clock.run(DRAWER_OPEN_MS, 'out', (t) => { if (drawer && drawer.token === token) { slide = t; placeDrawer(); } });
      if (opts.onDrawer) opts.onDrawer(true);
    }
    drawDrawer();
  }

  const api = {
    /** The frame's piles: thickness, and the open list follows (PileListView.Show). */
    bind(f) {
      frame = f;
      const piles = f ? f.Piles : null;
      shown = !!(piles && piles.Shown);
      for (const kind of KINDS) {
        hosts[kind].el.style.display = shown ? '' : 'none';
        if (shown) drawPile(kind, PileLayout.CountOf(piles, kind));
      }
      if (!shown) api.close();
      else if (drawer) drawDrawer();
    },
    /** The log tab's lines (player.js); an open log tab follows. */
    setLog(lines) {
      logLines = (lines || []).slice();
      if (drawer && openKind === LOG_KIND) drawDrawer();
    },
    /** PileListView.Toggle: opens that pile's list, or closes the drawer when that list is open already. */
    toggle(kind) {
      if (drawer && openKind === kind) { api.close(); return; }
      open(kind);
    },
    close() {
      if (!drawer) return;
      drawer.el.remove();
      drawer = null;
      if (opts.onDrawer) opts.onDrawer(false);
    },
    get open() { return !!drawer; },
    /** The drawer's right edge on the stage now (0 while closed; it grows as the drawer slides in). */
    drawerRight() { return drawer ? width * slide : 0; },
    /** PileListView.Covers: the drawer is open over this stage point. */
    covers(pt) { return !!drawer && pt && pt.x >= 0 && pt.x < width * slide && pt.y >= 0 && pt.y <= L.H; },
    /** PilesView.MiddleOf. */
    middle(kind) {
      return { x: PileLayout.LeftOf(kind) + PileLayout.Width / 2, y: PileLayout.Top + PileLayout.Height / 2 };
    },
    get shown() { return shown; },
    rowHeight: ROW_HEIGHT,
    destroy() {
      api.close();
      for (const kind of KINDS) hosts[kind].el.remove();
    },
  };
  return api;
}

