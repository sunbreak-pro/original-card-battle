// 手記のドロワー (dungeon_exploration_v4.md §6.7, battle-visual-v1.md §7.5, CAMP_FACILITIES_DESIGN.md
// §3.3): a sheet of washi from the right, 30% of the screen wide, reading a Journal.Script JournalBook
// the way Unity's JournalDrawerView does — which tabs open where (in battle only the enemies' pages
// and the terms), which rows read 「？」, every word. The tabs run 敵ごと / ダンジョン / メモ / 用語
// (battle_ui_ux_v2.md §2.4, dungeon_exploration_v4.md §6.7). In battle 「敵ごと」 is one tab per enemy
// being fought, named after it (§2.4 「敵のタブは戦っている敵ごとに 1 つ出し」, §4.7 「1 体戦は 4、2 体戦は 5、
// 3 体戦は 6」; JournalBook.TabText). While exploring the journal holds every enemy met (up to 13), so the
// pages sit under one 「敵」 tab with a row of per-enemy tabs below it (試作で決めたこと, EXTENSIONS.md).
// J or Esc closes it.
import { h, place, pushKeys } from '../common/stage.js';
import { JournalBook, JournalContext, JournalPage, JournalRow, JournalTab } from '../../bridge/journal/JournalBook.js';
import { JournalTerms } from '../../bridge/journal/JournalTerms.js';
import { EnemyPages } from '../../bridge/journal/EnemyPages.js';
import { ExplorationScreenBuilder } from '../../bridge/explorationScript/ExplorationScreenBuilder.js';
import { Enemies } from '../../core/battle/Enemies.js';
import { InheritanceText } from '../../core/inheritance/Inheritance.js';
import { MetaText } from './text.js';
import { sceneRoot, soundOf, sceneClock, styler, icon, modalSwallow, applyWashi } from './util.js';

const TAB_ORDER = [JournalTab.Enemy, JournalTab.Dungeon, JournalTab.Memo, JournalTab.Terms];
const DRAWER_W = 576; // 30% of 1920

/** The exploration screen's own book (ExplorationScreenBuilder: ExplorationScreen.Journal) for a running life. */
function expeditionPages(ex) {
  try {
    const session = ex && ex.Session;
    if (!session || !session.State) return [];
    const screen = ExplorationScreenBuilder.Build(session);
    return screen && screen.Journal ? screen.Journal.Pages.slice() : [];
  } catch (e) { return []; }
}

/** The enemies of the fight on screen, as { Id, Name } (the battle hands their ids). */
function enemiesOf(ctx) {
  const list = (ctx.enemies || []).slice();
  for (const id of ctx.enemyIds || []) {
    if (list.some((e) => (e.Id ?? e.id) === id)) continue;
    let name = id;
    try { name = Enemies.ById(id).Name; } catch (e) { /* unknown id: its id */ }
    list.push({ Id: id, Name: name });
  }
  return list;
}

/**
 * The book the drawer reads: ctx.book, or ctx.pages, or the running life's (ctx.expedition, read
 * through ExplorationScreenBuilder like the map's drawer); plus a page per enemy of the fight on
 * screen (ctx.enemies / ctx.enemyIds, 開示度 from ctx.disclosureOf, e.g. Expedition.DisclosureOf) and
 * the page of terms when none was handed in. In battle the fight's enemies come first and are the
 * only enemy pages (the others are not this fight's), in the fight's order (battle_ui_ux_v2.md §2.4
 * 「敵のタブは戦っている敵ごとに 1 つ」). Each foe's page is its roster page at disclosureOf's 開示度 — in
 * battle always, so the drawer reads the value the omens read; elsewhere only when the book has none
 * (EnemyPages.Page: 名前と HP 最大だけ at 0, battle_ui_ux_v2.md §7).
 */
export function bookOf(ctx) {
  const context = ctx.mode === 'battle' ? JournalContext.Battle
    : ctx.mode === 'explore' ? JournalContext.Exploration
      : (ctx.book ? ctx.book.Context : JournalContext.Exploration);
  let pages = (ctx.book ? ctx.book.Pages : ctx.pages ? ctx.pages : expeditionPages(ctx.expedition)).slice();
  const foes = enemiesOf(ctx);
  const foeIds = foes.map((e) => EnemyPages.PageId(e.Id ?? e.id));
  if (context === JournalContext.Battle && foes.length) {
    const ids = new Set(foeIds);
    pages = pages.filter((p) => p.Tab !== JournalTab.Enemy || ids.has(p.Id));
  }
  const disclosureOf = ctx.disclosureOf || (ctx.expedition ? (id) => ctx.expedition.DisclosureOf(id) : null);
  // In battle the fight's pages are drawn at the 開示度 the battle reads (disclosureOf: the journal's, the
  // same value its omens take — concept-v3 §5), never at another screen's.
  const redraw = context === JournalContext.Battle && disclosureOf != null;
  for (const e of foes) {
    const id = e.Id ?? e.id;
    const at = pages.findIndex((p) => p.Id === EnemyPages.PageId(id));
    if (at >= 0 && !redraw) continue;
    const level = Math.max(0, Math.min(JournalPage.MaxDisclosure, disclosureOf ? disclosureOf(id) : 0));
    const made = e.Rows ? new JournalPage(EnemyPages.PageId(id), JournalTab.Enemy, e.Name ?? e.name ?? id, e.Rows, level)
      : EnemyPages.Has(id) ? EnemyPages.Page(id, level)
        : new JournalPage(EnemyPages.PageId(id), JournalTab.Enemy, e.Name ?? e.name ?? id, [new JournalRow('', MetaText.EnemyPagePending)], level);
    if (at >= 0) pages[at] = made; else pages.push(made);
  }
  if (context === JournalContext.Battle && foes.length) {
    const at = (p) => foeIds.indexOf(p.Id);
    pages = pages.filter((p) => p.Tab !== JournalTab.Enemy).concat(pages.filter((p) => p.Tab === JournalTab.Enemy).sort((a, b) => at(a) - at(b)));
  }
  if (!pages.some((p) => p.Tab === JournalTab.Terms)) pages.push(JournalTerms.Page());
  pages = pages.map(withStanceTerm);
  const rare = rarePagesPage(ctx.expedition);
  if (rare && !pages.some((p) => p.Id === rare.Id)) {
    // after the layer's own pages on the ダンジョン tab (the layer first: it is the page the tab opens on)
    const last = pages.reduce((at, p, i) => (p.Tab === JournalTab.Dungeon ? i : at), -1);
    pages.splice(last >= 0 ? last + 1 : pages.length, 0, rare);
  }
  const ordered = TAB_ORDER.flatMap((tab) => pages.filter((p) => p.Tab === tab));
  return new JournalBook(ordered, context);
}

/**
 * JS extension (docs/EXTENSIONS.md X-17): the 用語 page with 「スタンス」 under 「構え」. The ported page (JournalTerms, the
 * C#'s) defines 構え only as the end-of-turn Guard rule, while most stance cards are named 「〜の構え」: the row says the
 * attribute apart from the rule. Added here, where the drawer shows the page, so the ported page stays the C#'s.
 */
export function withStanceTerm(page) {
  if (!page || page.Tab !== JournalTab.Terms || page.Rows.some((r) => r.Label === MetaText.StanceTerm)) return page;
  const at = page.Rows.findIndex((r) => r.Label === '構え');
  if (at < 0) return page;
  const rows = page.Rows.slice();
  rows.splice(at + 1, 0, new JournalRow(MetaText.StanceTerm, MetaText.StanceTermText));
  return new JournalPage(page.Id, page.Tab, page.Title, rows, page.Disclosure);
}

/**
 * The ダンジョン tab's 希少な頁 page while the life holds any (Expedition.Pages): how many were handed down — a
 * survivor's whole journal, the pages a trace gave (Expedition.InheritedPages) — and how many this life found
 * (INHERITANCE.md R-27: inherited pages are not 拾った物). What a page holds is #107's: until then the page says
 * plainly that the pages do nothing and that an empty page is no legacy pick (試作で決めたこと 9). Null without pages.
 */
export function rarePagesPage(ex) {
  let total = 0;
  let inherited = 0;
  try {
    total = ex ? Number(ex.Pages) || 0 : 0;
    inherited = ex ? Math.max(0, Math.min(total, Number(ex.InheritedPages) || 0)) : 0;
  } catch (e) { return null; }
  if (total <= 0) return null;
  const rows = [];
  if (inherited > 0) rows.push(new JournalRow(MetaText.RarePagesInherited, MetaText.RarePagesCount(inherited)));
  if (total - inherited > 0) rows.push(new JournalRow(MetaText.RarePagesFound, MetaText.RarePagesCount(total - inherited)));
  rows.push(new JournalRow('', MetaText.RarePagesPending));
  rows.push(new JournalRow('', MetaText.RarePagesFate));
  return new JournalPage('rare-pages', JournalTab.Dungeon, MetaText.RarePagesTitle, rows);
}

/** In battle every enemy being fought is a tab of its own (battle_ui_ux_v2.md §2.4, §4.7). */
function enemyTabs(book) {
  return book.Context === JournalContext.Battle;
}

/**
 * The drawer's tab row for a book with `page` open, left to right: 敵ごと / ダンジョン / メモ / 用語, as
 * { label, open, on, page, enemy }. In battle 敵ごと is one tab per enemy page, labelled with the enemy's
 * name (JournalBook.TabText) — 4 tabs for one foe, 5 for two, 6 for three; while exploring it is the one
 * 「敵」 tab (JournalBook.TabLabel), the enemies on the row below it. A tab whose pages may not be opened
 * here (the battle's ダンジョン and メモ) is shown shut.
 */
export function tabsOf(book, page) {
  const pagesOf = (tab) => book.Pages.filter((p) => p.Tab === tab);
  const out = [];
  for (const tab of TAB_ORDER) {
    if (tab === JournalTab.Enemy && enemyTabs(book)) {
      for (const p of pagesOf(tab)) out.push({ label: JournalBook.TabText(p), open: book.CanOpen(p), on: !!page && page.Id === p.Id, page: p, enemy: true });
      continue;
    }
    const first = pagesOf(tab).find((p) => book.CanOpen(p)) ?? null;
    out.push({ label: JournalBook.TabLabel(tab), open: first != null, on: !!page && page.Tab === tab, page: first, enemy: false });
  }
  return out;
}

/**
 * mountJournal(host, ctx) → { destroy(), close() }
 * ctx: { stage, audio, mode: 'explore' | 'battle', book? (JournalBook), pages? (JournalPage[]),
 *        expedition? (the running life: its book when none is handed in), enemies? ([{ Id, Name, Rows? }]),
 *        enemyIds? (the fight's enemies), disclosureOf?(enemyId), pageId?, memo? (the player's own
 *        text), scribbles? ([{ Generation, Name, Text }] inherited), onMemo?(text), onClose() }
 * With onMemo the メモ page is a sheet the player writes on (dungeon_exploration_v4.md §6.7: プレイヤーの
 * 自由記述); the game never reads it. With memo or onMemo every enemy page ends on 「自分のメモ」, the same
 * text read only (battle_ui_ux_v2.md §2.4; in battle with 「戦闘中は書けない」 at its end, the メモ tab
 * being shut there — EnemyPages.WithMemo).
 */
export function mountJournal(host, ctx) {
  const sound = soundOf(ctx);
  const root = sceneRoot(host, 'journal');
  const clock = sceneClock();
  const book = bookOf(ctx);
  let alive = true;
  let closing = false;
  let page = (ctx.pageId ? book.Find(ctx.pageId) : book.First) || null;

  const shade = place(h('div', { class: 'ocbg-meta-shade is-light', onclick: () => close() }), 0, 0, 1920, 1080);
  root.appendChild(shade);
  const drawer = place(h('aside', { class: 'ocbg-meta-washi ocbg-meta-drawer', role: 'dialog', 'aria-label': JournalBook.Heading }), 1920 - DRAWER_W, 0, DRAWER_W, 1080);
  applyWashi(drawer);
  root.appendChild(drawer);

  const head = h('header', { class: 'ocbg-meta-drawer-head' },
    h('h2', null, icon('journal', 30, 'var(--washi-ink)'), JournalBook.Heading),
    book.ClosingHint ? h('span', { class: 'ocbg-meta-drawer-hint' }, book.ClosingHint) : null,
    h('button', { class: 'ocbg-meta-drawer-close', type: 'button', onclick: () => close() }, JournalBook.CloseLabel));
  drawer.appendChild(head);
  const tabs = h('nav', { class: 'ocbg-meta-drawer-tabs' });
  const subtabs = h('nav', { class: 'ocbg-meta-drawer-sub' });
  const titleRow = h('div', { class: 'ocbg-meta-drawer-titlerow' });
  const body = h('div', { class: 'ocbg-meta-drawer-body' });
  drawer.appendChild(tabs);
  drawer.appendChild(subtabs);
  drawer.appendChild(titleRow);
  drawer.appendChild(body);
  drawer.appendChild(h('footer', { class: 'ocbg-meta-drawer-keys' }, MetaText.JournalKeys));

  const pagesOf = (tab) => book.Pages.filter((p) => p.Tab === tab);
  const perEnemy = enemyTabs(book);
  /** A page's label on the row below its tab: the enemy's name, or a dungeon page's title (the layer's short: 「第 N 層」). */
  const subLabel = (p) => {
    if (p.Tab !== JournalTab.Dungeon) return JournalBook.TabText(p);
    const m = /^dungeon-(\d+)$/.exec(p.Id);
    return m ? MetaText.LayerShort(Number(m[1])) : p.Title;
  };

  function go(next, quiet) {
    if (!next || !book.CanOpen(next) || (page && next.Id === page.Id)) return;
    page = next;
    if (!quiet) sound.play('nodeHover');
    draw(true);
  }

  function rowsOf(p) {
    const list = h('div', { class: 'ocbg-meta-rows' });
    let mode = 'stack';
    for (const row of p.Rows) {
      if (row.Label.length > 0 && row.Text.length === 0) {
        list.appendChild(h('h4', { class: 'ocbg-meta-rows-h' }, row.Label));
        mode = p.Tab === JournalTab.Terms && row.Label === JournalTerms.TermsHeading ? 'pair' : 'stack';
        continue;
      }
      const hidden = p.IsHidden(row);
      const text = hidden ? h('span', { class: 'ocbg-meta-hidden', title: JournalRow.Hidden }, JournalRow.Hidden) : h('span', null, p.TextOf(row));
      if (row.Label.length === 0) list.appendChild(h('p', { class: 'ocbg-meta-rows-p' }, text));
      else list.appendChild(h('div', { class: 'ocbg-meta-rows-' + mode }, h('b', null, row.Label), text));
    }
    return list;
  }

  // ---- メモ: the scribbles handed down, then the player's own sheet ----
  let memoText = String(ctx.memo || '');
  let memoTimer = null;
  const flushMemo = () => { if (memoTimer) { clearTimeout(memoTimer); memoTimer = null; } if (ctx.onMemo) { try { ctx.onMemo(memoText); } catch (e) { /* the router's */ } } };
  const writesMemo = (p) => p.Tab === JournalTab.Memo && (!!ctx.onMemo || ctx.memo != null || (ctx.scribbles && ctx.scribbles.length > 0));
  // The router keeps the memo: then each enemy page shows it as its 自分のメモ row.
  const showsMemo = !!ctx.onMemo || ctx.memo != null;
  function memoOf(p) {
    const list = h('div', { class: 'ocbg-meta-rows ocbg-meta-memo' });
    const scribbles = ctx.scribbles || [];
    if (scribbles.length) {
      list.appendChild(h('h4', { class: 'ocbg-meta-rows-h' }, MetaText.MemoInherited));
      for (const sc of scribbles) {
        list.appendChild(h('div', { class: 'ocbg-meta-rows-stack is-scribble' },
          h('b', null, InheritanceText.ScribbleName(sc.Generation, sc.Name)), h('span', null, sc.Text)));
      }
      list.appendChild(h('h4', { class: 'ocbg-meta-rows-h' }, MetaText.MemoMine));
    }
    const empty = (p.Rows.find((r) => r.Label.length === 0) || {}).Text || '';
    if (ctx.onMemo) {
      const area = h('textarea', { class: 'ocbg-meta-memo-sheet', rows: '10', spellcheck: 'false', placeholder: empty, 'aria-label': JournalBook.TabLabel(JournalTab.Memo) });
      area.value = memoText;
      area.addEventListener('input', () => {
        memoText = area.value;
        if (memoTimer) clearTimeout(memoTimer);
        memoTimer = setTimeout(flushMemo, 400);
      });
      // Typing stays in the sheet: the screens' keys (J, arrows, Enter) never see it. Esc closes the drawer.
      area.addEventListener('keydown', (e) => {
        e.stopPropagation();
        if (e.key === 'Escape') { e.preventDefault(); if (!e.repeat) { area.blur(); close(); } }
      });
      list.appendChild(area);
    } else {
      list.appendChild(h('p', { class: 'ocbg-meta-rows-p' }, memoText || empty));
    }
    return list;
  }

  function draw(animate) {
    tabs.innerHTML = '';
    const entries = tabsOf(book, page);
    tabs.style.gridTemplateColumns = `repeat(${Math.max(1, entries.length)}, minmax(0, 1fr))`;
    // 5 and 6 tabs (2 and 3 foes, §4.7) set smaller so a five-letter name keeps one line.
    tabs.classList.toggle('is-n5', entries.length === 5);
    tabs.classList.toggle('is-n6', entries.length >= 6);
    for (const t of entries) {
      const b = h('button', { type: 'button', class: 'ocbg-meta-drawer-tab' + (t.enemy ? ' is-enemy' : '') + (t.on ? ' is-on' : '') + (t.open ? '' : ' is-shut'),
        disabled: t.open ? null : 'disabled', 'aria-disabled': t.open ? 'false' : 'true', title: t.label,
        onclick: () => go(t.page) }, t.label);
      tabs.appendChild(b);
    }
    subtabs.innerHTML = '';
    // A tab holding several pages shows them on a row below it: the enemies met while exploring, and the ダンジョン
    // tab's pages (the layer, the carving once read, the 希少な頁) — ←→ walks them too.
    const tabPages = page && (page.Tab === JournalTab.Enemy ? !perEnemy : page.Tab === JournalTab.Dungeon) ? pagesOf(page.Tab) : [];
    subtabs.style.display = tabPages.length > 1 ? '' : 'none';
    for (const p of tabPages.length > 1 ? tabPages : []) {
      subtabs.appendChild(h('button', { type: 'button', class: 'ocbg-meta-drawer-subtab' + (page && p.Id === page.Id ? ' is-on' : ''),
        disabled: book.CanOpen(p) ? null : 'disabled', onclick: () => go(p) }, subLabel(p)));
    }
    titleRow.innerHTML = '';
    body.innerHTML = '';
    if (!page) return;
    titleRow.appendChild(h('h3', null, page.Title));
    if (page.DisclosureLabel) titleRow.appendChild(h('span', { class: 'ocbg-meta-disclosure' }, ...disclosureDots(page), page.DisclosureLabel));
    body.appendChild(writesMemo(page) ? memoOf(page) : rowsOf(showsMemo ? EnemyPages.WithMemo(page, memoText, book.Context) : page));
    body.scrollTop = 0;
    if (animate) { const s = styler(body.firstChild, { o: 0, y: 8 }); clock.tween(s, { o: 1, y: 0 }, 200, 'out'); }
  }
  function disclosureDots(p) {
    const dots = [];
    for (let i = 1; i <= JournalPage.MaxDisclosure; i++) dots.push(h('i', { class: i <= p.Disclosure ? 'is-on' : '' }));
    return [h('span', { class: 'ocbg-meta-disclosure-dots' }, ...dots)];
  }
  draw(false);

  // ---- open / close: the sheet slides in from the right (220 ms), back out on close ----
  const pd = styler(drawer, { x: DRAWER_W });
  const ps = styler(shade, { o: 0 });
  clock.tween(pd, { x: 0 }, 220, 'out');
  clock.tween(ps, { o: 1 }, 220, 'out');
  sound.play('journalOpen');

  async function close() {
    if (closing) return;
    closing = true;
    flushMemo();
    sound.play('journalClose');
    clock.tween(ps, { o: 0 }, 180, 'in');
    await clock.tween(pd, { x: DRAWER_W }, 180, 'in');
    if (alive && ctx.onClose) ctx.onClose();
  }

  // ←→ walks the pages that may be opened here, in tab order.
  function step(by) {
    const open = book.Pages.filter((p) => book.CanOpen(p));
    if (!open.length) return;
    const i = Math.max(0, open.findIndex((p) => page && p.Id === page.Id));
    go(open[(i + by + open.length) % open.length]);
  }
  const popKeys = pushKeys((e) => {
    if (closing) return true;
    // Only a fresh press closes: a held J or Esc repeats, and the screen under the drawer would open it again on the
    // next repeat (its openers ignore repeats too).
    if (e.key === 'Escape' || e.key === 'j' || e.key === 'J') { if (!e.repeat) close(); return true; }
    if (e.key === 'ArrowLeft') { step(-1); return true; }
    if (e.key === 'ArrowRight' || e.key === 'Tab') { step(1); return true; }
    if (e.key === 'ArrowDown') { body.scrollTop += 60; return true; }
    if (e.key === 'ArrowUp') { body.scrollTop -= 60; return true; }
    return modalSwallow(e);
  });

  return {
    close,
    destroy() {
      alive = false;
      if (memoTimer) flushMemo();
      popKeys();
      clock.stop();
      root.remove();
    },
  };
}
