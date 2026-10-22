// The end of a life (dungeon_exploration_v4.md §6.9, concept-v3 §8): a sheet of washi laid over the
// place it ended, reading Inheritance.endingView — the end sentence, what stays behind (the trace and
// its candidates, the cards the shallow rule kept out) or what passes on (the whole journal, the
// survivor's bonus), the next heir — and 「継承の間へ」.
import { h, place, pushKeys } from '../common/stage.js';
import { endingView, traceForLayer, LifeEnding, InheritanceText, InheritanceRules } from '../../core/inheritance/Inheritance.js';
import { SevenLayers } from '../../core/dungeon/SevenLayers.js';
import { ExplorationBackdropSpec } from '../../bridge/backdrop/ExplorationBackdrop.js';
import { sceneRoot, soundOf, sceneClock, styler, icon, applyWashi } from './util.js';
import { createBackdrop, layerBackdrop } from './backdrop.js';
import { MetaText } from './text.js';

const DEATHS = [LifeEnding.Fallen, LifeEnding.MiasmaDeath];

/**
 * A layer shows one trace, the oldest untaken one (INHERITANCE.md 試作で決めたこと 4, NodeKind.Trace),
 * so a life that ends on a layer where an older trace still lies leaves a trace that waits: the next
 * heir to visit gets the older one. endingView says 「次の踏破者が第 n 層を訪れると…受け取れる」 for every
 * death; here that line becomes the wait, as the hall says it (traceView: Visible / Waits), naming the
 * trace that stands first (Inheritance.traceForLayer). A cleared life's card trace gets the same line.
 * Nothing changes when the trace is the one that shows, or once endingView prints the wait itself.
 */
export function withWaitingTrace(view, lineage) {
  const life = lineage && lineage.Lives.length ? lineage.Lives[lineage.Lives.length - 1] : null;
  if (!life || !life.TraceId || lineage.Current || !lineage.Traces.some((t) => t.Id === life.TraceId)) return view;
  const first = traceForLayer(lineage, life.Layer);
  if (!first || first.Id === life.TraceId) return view;
  const ahead = lineage.Traces.filter((t) => t.Layer === life.Layer && t.Id !== life.TraceId);
  const wait = ahead.length > 1 ? MetaText.TraceWaitsMany(life.Layer, ahead.length) : MetaText.TraceWaits(life.Layer, first.Generation, first.Name);
  const visit = InheritanceText.TraceVisit(life.Layer, InheritanceRules.LegacyPicks);
  let lines;
  if (view.Lines.includes(visit)) lines = view.Lines.map((l) => (l === visit ? wait : l));
  else if (view.Ending === LifeEnding.Completed) {
    const left = InheritanceText.ClearedTraceLeft(InheritanceText.LayerTitle(life.Layer, SevenLayers.Of(life.Layer).DisplayName(lineage.Player.CarvingTaken)));
    const at = view.Lines.indexOf(left);
    if (at < 0) return view;
    lines = [...view.Lines.slice(0, at + 1), wait, ...view.Lines.slice(at + 1)];
  } else return view;
  return { ...view, Lines: lines };
}

/**
 * mountEnding(host, ctx) → { destroy() }
 * ctx: { stage, audio, ending (Inheritance.endingView(lineage)), lineage? (for the place's backdrop;
 *        a lineage passed as `ending` is read through endingView), layer?, onHall() }
 */
export function mountEnding(host, ctx) {
  const sound = soundOf(ctx);
  const lineage = ctx.lineage || (ctx.ending && ctx.ending.Lives ? ctx.ending : null);
  const view = withWaitingTrace(ctx.ending && ctx.ending.Sentence ? ctx.ending : endingView(lineage), lineage);
  const lastLife = lineage && lineage.Lives.length ? lineage.Lives[lineage.Lives.length - 1] : null;
  const layer = ctx.layer || (lastLife ? lastLife.Layer : 7);
  const died = DEATHS.includes(view.Ending);
  const root = sceneRoot(host, 'ending' + (died ? ' is-death' : ' is-alive'));
  let alive = true;

  // The place the life ended, under its exploration veil and a little more night.
  const place0 = ExplorationBackdropSpec.For(layer);
  const backdrop = createBackdrop(root, layerBackdrop(layer), { veil: place0.Veil, vignette: 0.75, moteSpeed: died ? 0.35 : 0.8 });
  const clock = sceneClock((dt, now) => { backdrop.paint(now); glowBreath(now); });

  const glow = place(h('div', { class: 'ocbg-meta-ending-glow' }), 960 - 620, 540 - 470, 1240, 940);
  root.appendChild(glow);

  // ---- where it ended ----
  const placeName = InheritanceText.LayerTitle(layer, SevenLayers.Of(layer).DisplayName(!!(lineage && lineage.Player.CarvingTaken)));
  const caption = place(h('div', { class: 'ocbg-meta-ending-place' }, h('i'), h('span', null, placeName), h('i')), 0, 64, 1920);
  root.appendChild(caption);

  // ---- the sheet ----
  const sheet = place(h('article', { class: 'ocbg-meta-washi ocbg-meta-ending-sheet' }), 960 - 520, 120, 1040);
  applyWashi(sheet);
  const mark = h('div', { class: 'ocbg-meta-ending-mark' }, icon(view.Ending === LifeEnding.MiasmaDeath ? 'miasma' : view.Ending === LifeEnding.Completed ? 'star' : 'person', 40, 'var(--washi-ink)'));
  const titleEl = h('h1', null);
  const titleChars = [...view.Title].map((c) => { const s = h('span', null, c); titleEl.appendChild(s); return s; });
  const heirLine = h('p', { class: 'ocbg-meta-ending-heir' }, view.HeirLine);
  const sentence = h('p', { class: 'ocbg-meta-ending-sentence' });
  const sentenceChars = [...view.Sentence].map((c) => { const s = h('span', null, c); sentence.appendChild(s); return s; });
  const rule = h('i', { class: 'ocbg-meta-ending-rule' });
  const lines = h('ul', { class: 'ocbg-meta-ending-lines' });
  const lineEls = view.Lines.map((l) => { const li = h('li', null, h('i', { class: 'ocbg-meta-ending-dot' }), h('span', null, l)); lines.appendChild(li); return li; });
  const next = h('p', { class: 'ocbg-meta-ending-next' }, view.NextLine);
  const button = h('button', { class: 'ocbg-meta-washi-btn', type: 'button', onclick: () => toHall() }, h('span', null, view.Button), h('small', null, 'Enter'));
  sheet.appendChild(h('header', null, mark, titleEl));
  sheet.appendChild(heirLine);
  sheet.appendChild(sentence);
  sheet.appendChild(rule);
  sheet.appendChild(lines);
  sheet.appendChild(h('footer', null, next, button));
  root.appendChild(sheet);
  // The sheet takes the height of what it says, centred on the screen.
  sheet.style.top = `${Math.max(40, Math.round((1080 - sheet.offsetHeight) / 2))}px`;

  // ---- motion: the sheet is laid down, the words are written, what stays appears line by line ----
  const fade = styler(place(h('div', { class: 'ocbg-meta-fade' }), 0, 0, 1920, 1080), { o: 1 });
  root.appendChild(fade.el);
  clock.tween(fade, { o: 0 }, 900, 'out');
  const pc = styler(caption, { o: 0 });
  clock.tween(pc, { o: 1 }, 1400, 'out', 200);
  const ps = styler(sheet, { o: 0, y: 26, s: 0.985, r: died ? -0.8 : 0.6 });
  clock.tween(ps, { o: 1, y: 0, s: 1, r: died ? -0.4 : 0.3 }, 900, 'out', 300);
  if (view.Ending === LifeEnding.Fallen) sound.play('lifeEndDeath', 300);
  else if (!died) sound.play('survived', 300);
  const pm = styler(mark, { o: 0, s: 0.8 });
  clock.tween(pm, { o: 1, s: 1 }, 700, 'out', 700);
  titleChars.forEach((c, i) => { const p = styler(c, { o: 0, y: 10 }); clock.tween(p, { o: 1, y: 0 }, 520, 'out', 800 + i * 140); });
  const t1 = 800 + titleChars.length * 140 + 200;
  const ph = styler(heirLine, { o: 0 });
  clock.tween(ph, { o: 1 }, 600, 'out', t1);
  sentenceChars.forEach((c, i) => { const p = styler(c, { o: 0 }); clock.tween(p, { o: 1 }, 260, 'out', t1 + 200 + i * 38); });
  const t2 = t1 + 200 + sentenceChars.length * 38 + 200;
  const pr = styler(rule, { o: 0, s: 0.2 });
  rule.style.transformOrigin = '0 50%';
  clock.tween(pr, { o: 1, s: 1 }, 700, 'out', t2);
  lineEls.forEach((li, i) => { const p = styler(li, { o: 0, x: -10 }); clock.tween(p, { o: 1, x: 0 }, 480, 'out', t2 + 300 + i * 260); });
  const t3 = t2 + 300 + lineEls.length * 260 + 200;
  const pn = styler(next, { o: 0 });
  clock.tween(pn, { o: 1 }, 600, 'out', t3);
  const pb = styler(button, { o: 0, y: 8 });
  clock.tween(pb, { o: 1, y: 0 }, 500, 'out', t3 + 300);

  // A death leaves a cold breath of the place; a life closed alive keeps the warm light of the heirs.
  function glowBreath(now) {
    const k = 0.5 - 0.5 * Math.cos((now / (died ? 6200 : 3800)) * 2 * Math.PI);
    glow.style.opacity = String((died ? 0.35 : 0.6) + 0.25 * k);
  }

  // The first key shows everything at once; the next one goes on to the hall.
  let busy = false;
  let shown = false;
  let live = clock;
  function showAll() {
    shown = true;
    live.stop();
    for (const el of [caption, sheet, mark, ...titleChars, heirLine, ...sentenceChars, rule, ...lineEls, next, button]) { el.style.opacity = '1'; el.style.transform = ''; }
    fade.el.style.opacity = '0';
    live = sceneClock((dt, now) => { backdrop.paint(now); glowBreath(now); });
  }
  async function toHall() {
    if (busy) return;
    busy = true;
    sound.play('uiConfirm');
    const pf = styler(fade.el, { o: 0 });
    const pz = styler(sheet, { o: 1 });
    live.tween(pz, { o: 0, y: -16 }, 420, 'in');
    await live.tween(pf, { o: 1 }, 600, 'in', 150);
    if (alive && ctx.onHall) ctx.onHall();
  }
  // A press anywhere while the words are still being written lays the whole sheet down at once.
  root.addEventListener('pointerdown', (e) => {
    if (busy || shown || Number(button.style.opacity) >= 1 || button.contains(e.target)) return;
    showAll();
  });
  const popKeys = pushKeys((e) => {
    if (busy) return false;
    if (e.key === 'Enter' || e.key === ' ' || e.key === 'Escape') {
      if (!shown && Number(button.style.opacity) < 1) { showAll(); return true; }
      if (e.key !== 'Escape') { toHall(); return true; }
    }
    return false;
  });

  return {
    destroy() {
      alive = false;
      popKeys();
      live.stop();
      clock.stop();
      root.remove();
    },
  };
}
