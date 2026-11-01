// Section 1: title → 継承の間 (first heir) → デッキを組む → 出立の支度 → set out (the layer-1 map assembling).
// Fresh storage. Ends with checkpoint ckpt-departed.json (the life in progress, nothing fought yet).
import {
  launch, sleep, shot, frames, startCast, saveFrames, mountedAt, pageNow, hoverSel, clickSel, centerOf,
  setIndexFile, dumpStorage, saveCheckpoint, writeJSON, S, marks,
} from './lib.mjs';

setIndexFile('meta');
const LINEAGE_SEED = 789591033;
const { browser, page, errors } = await launch({ navigate: false });
const info = {};

// ---------------------------------------------------------------- title
{
  const cast = await startCast(page);
  await page.goto((await import('./lib.mjs')).GAME + '?debug');
  const t0 = await mountedAt(page, 'ocbg-meta-title');
  await sleep(4400);
  const got = await cast.stop();
  saveFrames(got, t0, '101-title-intro', [0, 300, 800, 1500, 2500, 4000], { screen: 'title', state: 'intro animation, fresh storage', how: 'load dev.html?debug; time from the title root attaching (after fonts.ready)' });
}
await sleep(400);
await shot(page, '102-title-idle', 'title', 'idle, はじめから focused (no save)', 'fresh load, 5 s');
await hoverSel(page, '.ocbg-meta-title-item', 0);
await sleep(400);
await shot(page, '103-title-hover-new', 'title', 'pointer over はじめから', 'mouse.move to item 0');
await hoverSel(page, '.ocbg-meta-title-item', 1);
await sleep(400);
await shot(page, '104-title-hover-continue-disabled', 'title', 'pointer over つづきから (disabled, 保存された生は無い)', 'mouse.move to item 1');
await hoverSel(page, '.ocbg-meta-title-item', 2);
await sleep(400);
await shot(page, '105-title-hover-settings', 'title', 'pointer over 設定 (focus moved)', 'mouse.move to item 2');
await page.mouse.move(1500, 1000, { steps: 3 });
await page.keyboard.press('ArrowUp');
await sleep(400);
await shot(page, '106-title-key-focus-new', 'title', 'keyboard focus back on はじめから (↑ from 設定)', 'ArrowUp');
// settings over the title
await page.keyboard.press('Escape');
await sleep(700);
await shot(page, '107-title-settings', 'settings', 'settings overlay over the title', 'Esc on the title');
await page.keyboard.press('Escape');
await sleep(600);

// ---------------------------------------------------------------- hall (first heir)
{
  const before = await pageNow(page);
  const cast = await startCast(page);
  // The lineage seed is (Date.now() ^ Math.random()·2^31): pinned for this click so every run gets the same lineage
  // (seed 789591033: heir ケイト, layer 1 with 情報収集 / 精鋭 / イベント / 休息). Nothing else in the game reads either.
  await page.evaluate((seed) => { const d = Date.now, r = Math.random; Date.now = () => seed; Math.random = () => 0; window.__restoreTime = () => { Date.now = d; Math.random = r; }; }, LINEAGE_SEED);
  await clickSel(page, '.ocbg-meta-title-item', 0);
  const leaveAt = await pageNow(page);
  const t0 = await mountedAt(page, 'ocbg-meta-hall', before);
  await page.evaluate(() => window.__restoreTime());
  await sleep(4400);
  const got = await cast.stop();
  saveFrames(got, leaveAt, '108-title-leave', [0, 300, 700, 1000], { screen: 'title', state: 'leaving (はじめから clicked: menu slides out, fade to black)', how: 'click はじめから; time from the click' });
  saveFrames(got, t0, '201-hall-first-enter', [0, 300, 1000, 2000, 4000], { screen: 'hall (継承の間)', state: 'first heir, entering (lamp up, rows rise, name written, seal stamped)', how: 'title → はじめから; time from the hall root attaching' });
}
await sleep(500);
await shot(page, '202-hall-first-idle', 'hall (継承の間)', 'first heir, settled (no past lives, no traces)', 'after the entrance, ~5 s');
await hoverSel(page, '.ocbg-meta-stratum', 2);
await sleep(400);
await shot(page, '203-hall-first-hover-stratum3', 'hall (継承の間)', 'pointer over layer card 3 (no trace there)', 'mouse.move to the 3rd stratum');
await hoverSel(page, '.ocbg-meta-hall-heir ~ .ocbg-meta-primary, .ocbg-meta-hall .ocbg-meta-primary');
await sleep(400);
await shot(page, '204-hall-first-hover-start', 'hall (継承の間)', 'pointer over 出立の支度へ', 'mouse.move to the start button');
// settings from the hall (保存してタイトルへ)
await page.mouse.move(1000, 600, { steps: 3 });
await page.keyboard.press('Escape');
await sleep(700);
await shot(page, '205-hall-settings', 'settings', 'settings over the hall (lineage save: 保存してタイトルへ)', 'Esc in the hall');
await page.keyboard.press('Escape');
await sleep(600);

// ---------------------------------------------------------------- departure: deck
{
  const before = await pageNow(page);
  const cast = await startCast(page);
  await clickSel(page, '.ocbg-meta-hall .ocbg-meta-primary');
  const t0 = await mountedAt(page, 'ocbg-meta-departure', before);
  await sleep(1600);
  const got = await cast.stop();
  saveFrames(got, t0, '301-deck-enter', [0, 150, 300, 600, 1000], { screen: 'deck (デッキを組む)', state: 'entering (cards rise in)', how: 'hall → 出立の支度へ; time from the departure root attaching' });
}
await sleep(400);
await page.mouse.move(1900, 1070);
await shot(page, '302-deck-default', 'deck (デッキを組む)', 'default: 試作デッキ (20), first card selected, page 1, 全て / 全コスト', 'just entered');
info.filters = await page.evaluate(() => [...document.querySelectorAll('.ocbg-meta-filter')].map((b) => b.textContent));
// attribute filter: the 2nd tab (first attribute after 全て)
await clickSel(page, '.ocbg-meta-filter', 1);
await sleep(500);
await page.mouse.move(1900, 1070);
await shot(page, '303-deck-filter-attr', 'deck (デッキを組む)', `filter tab 「${info.filters[1]}」 selected`, 'click the 2nd filter tab');
const costIdx = info.filters.findIndex((t) => /1/.test(t) && /コスト/.test(t));
if (costIdx > 0) {
  await clickSel(page, '.ocbg-meta-filter', costIdx);
  await sleep(500);
  await page.mouse.move(1900, 1070);
  await shot(page, '304-deck-filter-attr-cost', 'deck (デッキを組む)', `filter 「${info.filters[1]}」 + 「${info.filters[costIdx]}」`, 'click a cost filter too');
}
await clickSel(page, '.ocbg-meta-filter', 0);
const allCost = info.filters.findIndex((t) => t.includes('全コスト'));
if (allCost > 0) await clickSel(page, '.ocbg-meta-filter', allCost);
await sleep(500);
// hover a card (no click)
await hoverSel(page, '.ocbg-meta-cell-face', 8);
await sleep(400);
await shot(page, '305-deck-hover-card', 'deck (デッキを組む)', 'pointer over card 9 (hover look), detail still shows the selected card', 'mouse.move onto the 9th card');
await clickSel(page, '.ocbg-meta-cell-face', 8);
await sleep(500);
await shot(page, '306-deck-card-selected-detail', 'deck (デッキを組む)', 'card 9 selected: detail panel (face, lines, terms), deck list chip marked', 'click the 9th card');
await page.mouse.move(1900, 1070);
await page.keyboard.press('ArrowRight');
await sleep(400);
await shot(page, '307-deck-key-cursor', 'deck (デッキを組む)', 'keyboard cursor moved to card 10 (→)', 'ArrowRight');
// page 2
{
  const cast = await startCast(page);
  await sleep(200);
  const t0 = Date.now();
  await clickSel(page, '.ocbg-meta-pagenav .ocbg-meta-nav', 1);
  await sleep(800);
  const got = await cast.stop();
  saveFrames(got, t0, '308-deck-page2-turn', [0, 100, 200, 400], { screen: 'deck (デッキを組む)', state: 'turning to page 2 (cards slide in from the right)', how: 'click 次のページ' });
}
await page.mouse.move(1900, 1070);
await sleep(300);
await shot(page, '309-deck-page2', 'deck (デッキを組む)', 'page 2', 'after 次のページ');
await clickSel(page, '.ocbg-meta-pagenav .ocbg-meta-nav', 0);
await sleep(600);
// counter states: clear, then a few cards (under 20)
await clickSel(page, '.ocbg-meta-presets .ocbg-meta-btn', 2);
await sleep(600);
await page.mouse.move(1900, 1070);
await shot(page, '310-deck-cleared-0', 'deck (デッキを組む)', 'deck cleared: 0 cards (クリア), 準備へ disabled', 'click クリア');
for (let i = 0; i < 6; i++) { await clickSel(page, '.ocbg-meta-cell .ocbg-meta-step[aria-label="＋"]', i); await sleep(120); }
await clickSel(page, '.ocbg-meta-cell .ocbg-meta-step[aria-label="＋"]', 0); await sleep(120);
await clickSel(page, '.ocbg-meta-cell .ocbg-meta-step[aria-label="＋"]', 1); await sleep(400);
await page.mouse.move(1900, 1070);
await sleep(300);
await shot(page, '311-deck-under20', 'deck (デッキを組む)', 'deck under 20 (8 cards): gauge short of the 20 mark, status says how many more', 'clear, then ＋ on cards 1–6 and again on 1–2');
// fill to the cap (40)
info.fill = [];
for (let pageNo = 0; pageNo < 6; pageNo++) {
  for (let pass = 0; pass < 3; pass++) {
    const n = await page.evaluate(() => document.querySelectorAll('.ocbg-meta-cell').length);
    for (let i = 0; i < n; i++) {
      const can = await page.evaluate((i) => { const b = document.querySelectorAll('.ocbg-meta-cell .ocbg-meta-step[aria-label="＋"]')[i]; return b && !b.disabled; }, i);
      if (!can) continue;
      await clickSel(page, '.ocbg-meta-cell .ocbg-meta-step[aria-label="＋"]', i);
      await sleep(60);
    }
  }
  const total = await page.evaluate(() => document.querySelector('.ocbg-meta-total').textContent);
  info.fill.push(total);
  const full = await page.evaluate(() => [...document.querySelectorAll('.ocbg-meta-cell .ocbg-meta-step[aria-label="＋"]')].every((b) => b.disabled));
  if (full) break;
  const next = await page.evaluate(() => !document.querySelectorAll('.ocbg-meta-pagenav .ocbg-meta-nav')[1].disabled);
  if (!next) break;
  await clickSel(page, '.ocbg-meta-pagenav .ocbg-meta-nav', 1);
  await sleep(500);
}
await sleep(500);
await page.mouse.move(1900, 1070);
await shot(page, '312-deck-full', 'deck (デッキを組む)', 'deck at the cap (40): every ＋ disabled, gauge full', 'clicked ＋ until every ＋ was disabled');
// a refused ＋ (shake) on a full deck
{
  const cast = await startCast(page);
  await sleep(200);
  const t0 = Date.now();
  await page.evaluate(() => { const b = document.querySelectorAll('.ocbg-meta-cell .ocbg-meta-step[aria-label="＋"]')[0]; b.disabled = false; });
  await clickSel(page, '.ocbg-meta-cell .ocbg-meta-step[aria-label="＋"]', 0);
  await sleep(400);
  const got = await cast.stop();
  saveFrames(got, t0, '313-deck-full-refuse-plus', [0, 60, 120], { screen: 'deck (デッキを組む)', state: 'refused ＋ on a full deck (the ＋ shakes)', how: 're-enabled the disabled ＋ in the DOM and clicked it (the builder refuses)' });
}
// back to the prototype deck (valid)
await clickSel(page, '.ocbg-meta-presets .ocbg-meta-btn', 0);
await sleep(800);
await page.mouse.move(1900, 1070);
await shot(page, '314-deck-prototype-valid', 'deck (デッキを組む)', '試作デッキ again (valid), 準備へ enabled', 'click 試作デッキ');
// deck list chips: hover / click a chip
await clickSel(page, '.ocbg-meta-deckchip', 3);
await sleep(500);
await shot(page, '315-deck-list-chip', 'deck (デッキを組む)', 'a deck-list chip clicked (its card picked and marked)', 'click the 4th chip of the deck list');
// the life's deck: 試作デッキ + 水の構え + 岩の構え (so the battles have a stance to show)
for (let k = 0; k < 6; k++) {
  const prev = await page.evaluate(() => !document.querySelectorAll('.ocbg-meta-pagenav .ocbg-meta-nav')[0].disabled);
  if (!prev) break;
  await clickSel(page, '.ocbg-meta-pagenav .ocbg-meta-nav', 0);
  await sleep(450);
}
for (const name of ['水の構え', '岩の構え']) {
  await page.evaluate((name) => { const f = [...document.querySelectorAll('.ocbg-meta-cell-face')].find((b) => b.getAttribute('aria-label') === name); f.closest('.ocbg-meta-cell').setAttribute('data-ref', 'pick'); }, name);
  await clickSel(page, '[data-ref="pick"] .ocbg-meta-step[aria-label="＋"]');
  await page.evaluate(() => document.querySelector('[data-ref="pick"]').removeAttribute('data-ref'));
  await sleep(250);
}
await page.mouse.move(1900, 1070);
await sleep(400);
await shot(page, '316-deck-for-this-life', 'deck (デッキを組む)', 'the deck this life sets out with: 試作デッキ + 水の構え + 岩の構え (22)', '＋ on 水の構え and 岩の構え');

// ---------------------------------------------------------------- departure: supplies
{
  const cast = await startCast(page);
  await sleep(200);
  const t0 = Date.now();
  await clickSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 0);
  await sleep(900);
  const got = await cast.stop();
  saveFrames(got, t0, '401-supplies-enter', [0, 150, 300, 500], { screen: 'supplies (出立の支度)', state: 'step change deck → supplies (slide)', how: 'click 準備へ' });
}
await page.mouse.move(1900, 1070);
await sleep(300);
await shot(page, '402-supplies-empty', 'supplies (出立の支度)', 'empty: no talent, no tool, no consumable', 'after 準備へ');
await shot(page, '403-supplies-start-diagram-default', 'supplies (出立の支度)', 'start-position diagram (crop of the info panel), no tool', 'clip x1236 y576 w636 h324', { clip: { x: 1236, y: 576, width: 636, height: 324 } });
info.talents = await page.evaluate(() => [...document.querySelectorAll('.ocbg-meta-talent')].map((b) => b.textContent));
info.tools = await page.evaluate(() => [...document.querySelectorAll('.ocbg-meta-supply')].map((c) => c.querySelector('b').textContent));
await clickSel(page, '.ocbg-meta-talent', 0);
await sleep(300);
// a tool and a consumable (the first of each grid)
const supplyCount = await page.evaluate(() => [...document.querySelectorAll('.ocbg-meta-supgrid')].map((g) => g.querySelectorAll('.ocbg-meta-supply').length));
info.supplyCount = supplyCount;
await page.evaluate(() => {
  const grids = document.querySelectorAll('.ocbg-meta-supgrid');
  grids[0].querySelectorAll('.ocbg-meta-supply')[0].setAttribute('data-ref', 'tool0');
  grids[1].querySelectorAll('.ocbg-meta-supply')[0].setAttribute('data-ref', 'cons0');
});
await clickSel(page, '[data-ref="tool0"] .ocbg-meta-step[aria-label="＋"]');
await sleep(300);
await clickSel(page, '[data-ref="cons0"] .ocbg-meta-step[aria-label="＋"]');
await sleep(500);
await page.mouse.move(1900, 1070);
await sleep(300);
await shot(page, '404-supplies-talent-tool-consumable', 'supplies (出立の支度)', `talent 「${info.talents[0]}」, tool 「${info.tools[0]}」, consumable 「${info.tools[supplyCount[0]]}」 chosen; 出立する enabled`, 'click talent 1, ＋ on tool 1, ＋ on consumable 1');
await shot(page, '405-supplies-start-diagram-chosen', 'supplies (出立の支度)', 'start-position diagram after the choice (crop)', 'clip x1236 y576 w636 h324', { clip: { x: 1236, y: 576, width: 636, height: 324 } });
// 間合いの履 moves the start (cell 2 → 1, gap 3 → 4): the diagram changes
const supplyRef = async (name, ref) => page.evaluate(([name, ref]) => {
  const c = [...document.querySelectorAll('.ocbg-meta-supply')].find((x) => x.querySelector('b').textContent === name);
  document.querySelectorAll(`[data-ref="${ref}"]`).forEach((e) => e.removeAttribute('data-ref'));
  if (c) c.setAttribute('data-ref', ref);
  return !!c;
}, [name, ref]);
if (await supplyRef('間合いの履', 'boots')) {
  await clickSel(page, '[data-ref="boots"] .ocbg-meta-step[aria-label="＋"]');
  await page.mouse.move(1900, 1070);
  await sleep(400);
  await shot(page, '406-supplies-start-diagram-moved', 'supplies (出立の支度)', 'start-position diagram with 間合いの履 (start cell 1, gap 4) (crop)', 'click ＋ on 間合いの履; clip of the info panel', { clip: { x: 1236, y: 576, width: 636, height: 324 } });
  await shot(page, '406b-supplies-with-boots', 'supplies (出立の支度)', 'two tools (防瘴の面, 間合いの履), the start diagram moved', 'full screen of the same moment');
  await clickSel(page, '[data-ref="boots"] .ocbg-meta-step[aria-label="−"]');
  await sleep(300);
}
// this life's supplies: + 写しの帳 (tool: the survey asks which enemy) and 薬草 (consumable)
if (await supplyRef('写しの帳', 'sand')) await clickSel(page, '[data-ref="sand"] .ocbg-meta-step[aria-label="＋"]');
await sleep(200);
if (await supplyRef('薬草', 'herb')) await clickSel(page, '[data-ref="herb"] .ocbg-meta-step[aria-label="＋"]');
await page.mouse.move(1900, 1070);
await sleep(400);
await shot(page, '407-supplies-final', 'supplies (出立の支度)', 'this life\'s supplies: talent 突き, tools 防瘴の面 + 写しの帳, consumables 浄化の香 + 薬草', 'state used for this life');
// a slot plate hovered (its × removes it)
await hoverSel(page, '.ocbg-meta-slot.is-full', 0);
await sleep(300);
await shot(page, '407b-supplies-hover-slot', 'supplies (出立の支度)', 'pointer over a filled tool slot (× to remove)', 'mouse.move onto the first filled slot');
await hoverSel(page, '.ocbg-meta-dep-page .ocbg-meta-primary', 1).catch(() => {});
await sleep(300);
await shot(page, '408-supplies-hover-setout', 'supplies (出立の支度)', 'pointer over 出立する', 'mouse.move to 出立する');

// ---------------------------------------------------------------- set out: the layer-1 map
{
  const before = await pageNow(page);
  const cast = await startCast(page);
  const c = await centerOf(page, '.ocbg-meta-primary:not([disabled])', 1) || await centerOf(page, '.ocbg-meta-primary', 1);
  await page.mouse.click(c[0], c[1]);
  const t0 = await mountedAt(page, 'ocbg-explore', before);
  await mountedAt(page, 'ocbg-battle', before, 20000).catch(() => null);
  await sleep(3500);
  const got = await cast.stop();
  saveFrames(got, t0, '501-explore-layer1-enter', [0, 300, 700, 1000, 1500, 2000], { screen: 'exploration', state: 'layer 1 right after departure (map assembling under the layer banner)', how: 'click 出立する; time from the explore root attaching' });
  const banners = await marks(page, 'ocbg-explore-banner.is-on', before);
  info.exploreBanners = banners;
  const enc = banners.find((x) => !/層/.test(x.text));
  if (enc) saveFrames(got, enc.t, '502-explore-encounter-banner', [0, 150, 300, 600, 1000], { screen: 'exploration', state: `encounter banner before the first battle (「${enc.text}」)`, how: 'the start node is a battle; time from the banner switching on' });
  const b = await marks(page, 'ocbg-battle', before);
  if (b[0]) saveFrames(got, b[0].t, '601-battle-open', [0, 200, 400, 700, 1000, 1500, 2500], { screen: 'battle', state: 'battle opening (backdrop first, then HUD / field fade in, then あなたの番)', how: 'first battle of layer 1 after setting out; time from the battle root attaching' });
  const tb = await marks(page, 'ocbg-battle-banner', before);
  info.battleBanners = tb;
  if (tb[0]) saveFrames(got, tb[0].t, '602-battle-turn1-banner', [0, 150, 300, 600, 1000], { screen: 'battle', state: `turn start banner 「${tb[0].text}」, turn 1`, how: 'time from the banner element attaching' });
}
info.afterSetOut = await page.evaluate(() => ({ battle: !!document.querySelector('.ocbg-battle'), explore: !!document.querySelector('.ocbg-explore'), pending: window.__ocb.live && window.__ocb.live.raw.Session.HasPendingBattle }));
saveCheckpoint('departed', await dumpStorage(page));
info.errors = errors;
writeJSON('info-meta.json', info);
console.log(JSON.stringify(info, null, 1));
await browser.close();
