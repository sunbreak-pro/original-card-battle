// 継承の間 (CAMP_FACILITIES_DESIGN.md §3.1, concept-v3 §8): what the earlier lives hand on, read off
// Inheritance.hallView(lineage) — the past lives with their 装束 and how and where each ended, every
// untaken trace on a column of the seven layers, the survivor and its bonus — and the next heir's
// 襲名. Nothing here takes a trace (R-12); 「出立の支度へ」 starts the life through Inheritance.startLife.
import { h, place, pushKeys } from '../common/stage.js';
import { hallView, startLife, InheritanceText, LifeEnding, CandidateKind } from '../../core/inheritance/Inheritance.js';
import { SevenLayers } from '../../core/dungeon/SevenLayers.js';
import { BackdropSpec, LayerBackdrop, SkyStop, Haze, HazeShape, Flicker, Motes, Rgba } from '../../bridge/backdrop/BackdropSpec.js';
import { JournalBook } from '../../bridge/journal/JournalBook.js';
import { MetaText } from './text.js';
import { sceneRoot, soundOf, sceneClock, styler, ocb, icon, token, hexRgb, rgbaCss, riseIn } from './util.js';
import { createBackdrop } from './backdrop.js';

const DEATHS = [LifeEnding.Fallen, LifeEnding.MiasmaDeath];

/** The lamp-lit room, built from the same backdrop types as a layer (warm: it is the player's room). */
export function hallBackdropSpec(el, strength = 1) {
  const rgba = (name, fb, a) => { const [r, g, b] = hexRgb(token(el, name, fb)); return Rgba.Bytes(r, g, b, a); };
  const ground = rgba('--ground', '#10141d', 1);
  return new LayerBackdrop({
    Layer: 0,
    Name: InheritanceText.HallTitle,
    Sky: [new SkyStop(0, BackdropSpec.Mix(ground, rgba('--amber', '#e8a84c', 1), 0.06)), new SkyStop(0.55, ground), new SkyStop(1, BackdropSpec.Mix(ground, new Rgba(0, 0, 0, 1), 0.45))],
    Hazes: [
      new Haze({ Shape: HazeShape.Radial, Color: rgba('--amber', '#e8a84c', 0.16 * strength), X: 0.5, Y: 0.02, Size: 1.25, Flicker: new Flicker(4200, 0.28) }),
      new Haze({ Shape: HazeShape.Radial, Color: rgba('--wick', '#ffd89a', 0.07 * strength), X: 0.86, Y: 0.30, Size: 0.7, Flicker: new Flicker(5300, 0.35) }),
    ],
    Motes: new Motes({ Count: 26, Color: rgba('--wick', '#ffd89a', 0.22 * strength), SizePx: 3, SpeedX: 3, SpeedY: -5, Twinkle: 0.6 }),
  });
}

function endingIcon(ending) {
  switch (ending) {
    case LifeEnding.MiasmaDeath: return icon('miasma', 22, 'var(--phosphor)');
    case LifeEnding.Fallen: return icon('person', 22, 'var(--steel)');
    case LifeEnding.Completed: return icon('star', 22, 'var(--amber)');
    default: return icon('person', 22, 'var(--amber)');
  }
}

function kindIcon(kind) {
  switch (kind) {
    case CandidateKind.Card: return icon('star', 18, 'var(--amber)');
    case CandidateKind.EnemyPage: return icon('journal', 18, 'var(--ink)');
    case CandidateKind.DungeonPage: return icon('stairs', 18, 'var(--ink)');
    default: return icon('journal', 18, 'var(--ink)');
  }
}

/** A stratum's sky as CSS, read off the bridge's per-layer stops. */
function skyCss(layer) {
  const spec = BackdropSpec.For(layer);
  return `linear-gradient(180deg, ${spec.Sky.map((s) => `${rgbaCss(s.Color)} ${Math.round(s.At * 100)}%`).join(', ')})`;
}

/**
 * A past life's trace line on its card. hallView says 「痕跡：第 n 代 …が受け取った」 once a trace is
 * collected, but visiting collects a trace even with 「何も受け取らない」 (INHERITANCE.md R-10, 試作で決めたこと
 * 5) and the lineage keeps who collected it, not what was picked: the hall says 「…が回収した」. Any other
 * line (untaken, none, or one the inheritance module words differently) stands as hallView gives it.
 */
export function pastTraceLine(lineage, life) {
  const rec = lineage.Lives.find((l) => l.Generation === life.Generation);
  const gen = rec ? rec.CollectedBy : null;
  if (gen === null || gen === undefined) return life.TraceLine;
  const by = lineage.Lives.find((l) => l.Generation === gen) ?? lineage.Current;
  const name = by ? by.Name : '';
  return life.TraceLine === InheritanceText.TraceStatus.Taken(gen, name) ? MetaText.TraceCollected(gen, name) : life.TraceLine;
}

/**
 * mountHall(host, ctx) → { destroy() }
 * ctx: { stage, audio, settings, lineage, seed?, onDepart(lifeStart), onSettings(), onJournal?, onTitle? }
 * onDepart gets Inheritance.startLife(lineage, seed); null when the lineage already has a life in
 * progress (nothing to start: the router resumes that life).
 */
export function mountHall(host, ctx) {
  const O = ocb();
  const sound = soundOf(ctx);
  const lineage = ctx.lineage;
  const view = hallView(lineage);
  const root = sceneRoot(host, 'hall');
  let alive = true;
  const backdrop = createBackdrop(root, hallBackdropSpec(root), { vignette: 0.5 });
  const clock = sceneClock((dt, now) => { backdrop.paint(now); flames(now); });
  backdrop.alpha = 0;

  // ---- header ----
  const title = place(h('h1', { class: 'ocbg-meta-hall-title' }, view.Title), 72, 36);
  const lead = place(h('p', { class: 'ocbg-meta-hall-lead' }, view.Lead), 74, 110);
  root.appendChild(title);
  root.appendChild(lead);
  // Top right (CAMP_FACILITIES_DESIGN.md §3.5 / §3.3): 設定 (Esc) and, when the router gives one, the journal (J),
  // laid from the right edge leftwards as on 出立; then, when the router gives the way back, 「タイトルへ」.
  // The lineage is saved the moment a life closes or a trace is taken (tier1 R1-16), so leaving the hall
  // keeps everything: the title's つづきから comes back here with the same next heir (INHERITANCE.md R-02).
  const corner = [];
  let cornerX = 1848;
  if (ctx.onSettings) {
    cornerX -= 120;
    const gear = place(h('button', { class: 'ocbg-meta-chip-btn', type: 'button', onclick: () => { if (!busy) { sound.play('uiClick'); ctx.onSettings(); } } },
      h('span', null, MetaText.Settings), h('small', null, 'Esc')), cornerX, 44, 120, 52);
    root.appendChild(gear);
    corner.push(gear);
    cornerX -= 16;
  }
  if (ctx.onJournal) {
    cornerX -= 56;
    const book = place(h('button', { class: 'ocbg-meta-chip-btn is-square', type: 'button', title: JournalBook.Heading, 'aria-label': JournalBook.Heading,
      onclick: () => { if (!busy) ctx.onJournal(); } }, icon('journal', 30, 'var(--ink)')), cornerX, 44, 56, 52);
    root.appendChild(book);
    corner.push(book);
    cornerX -= 16;
  }
  if (ctx.onTitle) {
    cornerX -= 168;
    const toTitle = place(h('button', { class: 'ocbg-meta-chip-btn ocbg-meta-hall-totitle', type: 'button', onclick: () => leaveToTitle() },
      icon('back', 20, 'var(--ink)'), h('span', null, MetaText.BackToTitle)), cornerX, 44, 168, 52);
    root.appendChild(toTitle);
    corner.push(toTitle);
  }

  // ---- past lives (newest first) ----
  const traceLineOf = (life) => pastTraceLine(lineage, life);
  const livesHead = place(h('h2', { class: 'ocbg-meta-hall-h2' }, view.PastLivesHeading), 72, 184);
  const lives = place(h('div', { class: 'ocbg-meta-hall-lives' }), 72, 236, 440, 486);
  const lifeCards = view.PastLives.map((life) => {
    const lived = !DEATHS.includes(life.Ending);
    return h('article', { class: 'ocbg-meta-hall-life' + (lived ? ' is-alive' : '') },
      h('header', null,
        h('span', { class: 'ocbg-meta-gen' }, life.GenerationLabel),
        h('b', { class: 'ocbg-meta-hall-lifename' }, life.Name),
        h('span', { class: 'ocbg-meta-ending' + (lived ? ' is-alive' : '') }, endingIcon(life.Ending), life.EndingLabel)),
      h('p', { class: 'ocbg-meta-hall-line' }, life.EndingLine),
      h('p', { class: 'ocbg-meta-hall-place' }, icon('stairs', 18, 'var(--ink)'), life.PlaceLine),
      h('footer', null,
        h('span', { class: 'ocbg-meta-tier' }, InheritanceText.AttireHeading + '　' + life.TierName),
        h('span', null, traceLineOf(life))));
  });
  if (!lifeCards.length) lives.appendChild(h('p', { class: 'ocbg-meta-hall-none' }, view.Empty || InheritanceText.HallEmpty));
  lifeCards.forEach((c) => lives.appendChild(c));
  root.appendChild(livesHead);
  root.appendChild(lives);
  // More lives than fit: the list fades at the edge it continues past (wheel or drag to read on).
  const edge = () => {
    const more = lives.scrollHeight - lives.clientHeight > 2;
    lives.classList.toggle('is-more-below', more && lives.scrollTop + lives.clientHeight < lives.scrollHeight - 2);
    lives.classList.toggle('is-more-above', more && lives.scrollTop > 2);
  };
  lives.addEventListener('scroll', edge);
  edge();
  // The rules of traces (R-12: the hall only shows them), under the lives.
  const rules = place(h('ul', { class: 'ocbg-meta-hall-rules' }, ...view.TraceRules.map((r) => h('li', null, r))), 72, 742, 440, 244);
  root.appendChild(rules);

  // ---- the seven layers, with where each life ended and the traces still lying there ----
  const traceHead = place(h('h2', { class: 'ocbg-meta-hall-h2' }, view.TraceHeading), 552, 184);
  root.appendChild(traceHead);
  const column = place(h('div', { class: 'ocbg-meta-hall-column' }), 552, 236, 360, 750);
  const strata = [];
  for (let layer = 1; layer <= SevenLayers.Count; layer++) {
    const profile = SevenLayers.Of(layer);
    const s = h('div', { class: 'ocbg-meta-stratum', style: { background: skyCss(layer) } },
      h('div', { class: 'ocbg-meta-stratum-veil' }),
      h('div', { class: 'ocbg-meta-stratum-label' },
        h('span', { class: 'ocbg-meta-stratum-n' }, String(layer)),
        h('span', null, profile.DisplayName(lineage.Player.CarvingTaken))));
    const marks = h('div', { class: 'ocbg-meta-stratum-marks' });
    s.appendChild(marks);
    s.marks = marks;
    place(s, 0, (layer - 1) * 108, 360, 102);
    column.appendChild(s);
    strata.push(s);
  }
  // Each closed life marks the layer it ended on (the lineage keeps the layer; hallView prints the line).
  const flameEls = [];
  lineage.Lives.forEach((life) => {
    const s = strata[life.Layer - 1];
    if (!s) return;
    const lived = !DEATHS.includes(life.Ending);
    const traceIndex = life.TraceId ? view.Traces.findIndex((t) => t.Id === life.TraceId) : -1;
    const untaken = traceIndex >= 0;
    // the mark's hover line says whose life it was, how it ended and where its trace stands (the three looks below)
    const past = view.PastLives.find((p) => p.Generation === life.Generation) || null;
    const mark = h('button', {
      class: 'ocbg-meta-mark' + (lived ? ' is-alive' : '') + (untaken ? ' is-trace' : '') + (life.CollectedBy !== null ? ' is-taken' : ''),
      type: 'button',
      title: past ? MetaText.MarkTitle(past.Line, past.EndingLabel, pastTraceLine(lineage, past)) : InheritanceText.HeirLine(life.Generation, life.Name),
      onclick: () => { if (untaken) select(traceIndex, true); },
    }, h('span', { class: 'ocbg-meta-mark-n' }, String(life.Generation)));
    if (untaken) { const f = h('i', { class: 'ocbg-meta-flame' }); mark.appendChild(f); flameEls.push(f); }
    s.marks.appendChild(mark);
    // The whole stratum answers the pointer for the trace lying there (the mark is small).
    if (untaken && !s.traceIndex) {
      s.traceIndex = traceIndex + 1;
      s.classList.add('is-live');
      s.addEventListener('click', (e) => { if (!mark.contains(e.target)) select(traceIndex, true); });
    }
  });
  root.appendChild(column);
  // What the marks' looks mean, under the column (review: 「2」 dashed, 「3」 ringed and 「4」 glowing had no key): only
  // the looks the column shows.
  const looks = [
    ['is-trace', MetaText.MarkUntaken, lineage.Lives.some((l) => l.TraceId && view.Traces.some((t) => t.Id === l.TraceId))],
    ['is-taken', MetaText.MarkTaken, lineage.Lives.some((l) => l.CollectedBy !== null)],
    ['is-alive', MetaText.MarkAlive, lineage.Lives.some((l) => !DEATHS.includes(l.Ending))],
  ].filter((x) => x[2]);
  if (looks.length) {
    root.appendChild(place(h('div', { class: 'ocbg-meta-mark-key' },
      looks.map(([cls, word]) => h('span', null, h('i', { class: 'ocbg-meta-mark is-key ' + cls }), word))), 552, 1000, 360, 40));
  }

  // ---- the selected trace ----
  const detail = place(h('section', { class: 'ocbg-meta-hall-trace' }), 936, 236, 452, 750);
  root.appendChild(detail);
  let sel = view.Traces.length ? 0 : -1;
  let traceBody = null;
  let keys = null;
  function paintKeys() {
    if (keys) keys.textContent = MetaText.HallKeysFor(view.Traces.length > 1, traceScrolls());
  }

  function drawTrace() {
    detail.innerHTML = '';
    strata.forEach((s) => s.classList.remove('is-sel'));
    const body = h('div', { class: 'ocbg-meta-hall-trace-body' });
    if (sel < 0) {
      body.classList.add('is-empty');
      body.appendChild(h('i', { class: 'ocbg-meta-wick-off' }));
      body.appendChild(h('p', { class: 'ocbg-meta-hall-none' }, view.NoTraces));
    } else {
      const t = view.Traces[sel];
      strata[t.Layer - 1].classList.add('is-sel');
      const head = h('header', null,
        h('div', { class: 'ocbg-meta-hall-trace-top' },
          h('span', { class: 'ocbg-meta-ending' + (t.Cleared ? ' is-alive' : '') }, endingIcon(lineage.Traces.find((x) => x.Id === t.Id)?.Ending), t.EndingLabel),
          view.Traces.length > 1 ? h('span', { class: 'ocbg-meta-pager' }, `${sel + 1} / ${view.Traces.length}`) : null),
        h('h3', null, t.HeirLine),
        h('p', { class: 'ocbg-meta-hall-place' }, icon('stairs', 18, 'var(--ink)'), t.PlaceLine),
        h('p', { class: 'ocbg-meta-hall-appears' + (t.Visible ? '' : ' is-waiting') }, h('i', { class: 'ocbg-meta-dot' }), t.AppearsLine));
      body.appendChild(head);
      if (t.Note) body.appendChild(h('p', { class: 'ocbg-meta-hall-note' }, t.Note));
      body.appendChild(h('p', { class: 'ocbg-meta-hall-count' }, t.CountLine));
      const list = h('ul', { class: 'ocbg-meta-cands' });
      t.Candidates.forEach((c) => list.appendChild(h('li', { class: 'is-' + c.Kind.toLowerCase() + (c.Kind === CandidateKind.Scribble ? ' is-long' : '') },
        h('span', { class: 'ocbg-meta-cand-kind' }, kindIcon(c.Kind), c.KindLabel),
        h('b', null, c.Name),
        h('span', { class: 'ocbg-meta-cand-detail' }, c.Detail))));
      body.appendChild(list);
      if (t.Withheld.length) {
        body.appendChild(h('h4', null, InheritanceText.WithheldHeading));
        const wl = h('ul', { class: 'ocbg-meta-withheld' });
        t.Withheld.forEach((w) => wl.appendChild(h('li', null, h('b', null, w.Name), h('span', null, w.Detail))));
        body.appendChild(wl);
      }
    }
    detail.appendChild(body);
    traceBody = body;
    // A trace with more candidates than fit: the list fades at the edge it continues past, like the
    // lives list (R-12: every candidate is shown; the wheel, a drag or ↑↓ / PgUp PgDn read on).
    body.addEventListener('scroll', traceEdge);
    traceEdge();
    paintKeys();
  }
  function traceEdge() {
    const b = traceBody;
    if (!b) return;
    const more = b.scrollHeight - b.clientHeight > 2;
    b.classList.toggle('is-more-below', more && b.scrollTop + b.clientHeight < b.scrollHeight - 2);
    b.classList.toggle('is-more-above', more && b.scrollTop > 2);
  }
  const traceScrolls = () => !!traceBody && traceBody.scrollHeight - traceBody.clientHeight > 2;
  function scrollTrace(by) {
    if (!traceScrolls()) return false;
    const before = traceBody.scrollTop;
    traceBody.scrollTop = Math.max(0, Math.min(traceBody.scrollHeight - traceBody.clientHeight, before + by));
    traceEdge();
    return traceBody.scrollTop !== before;
  }
  function select(i, byPointer) {
    if (i === sel || i < 0 || i >= view.Traces.length) return;
    sel = i;
    sound.play(byPointer ? 'uiClick' : 'nodeHover');
    drawTrace();
    const p = styler(detail.firstChild, { o: 0, y: 8 });
    clock.tween(p, { o: 1, y: 0 }, 220, 'out');
  }
  drawTrace();

  // ---- the next heir ----
  const heir = view.NextHeir;
  const heirHead = place(h('h2', { class: 'ocbg-meta-hall-h2' }, view.NextHeirHeading), 1428, 184);
  root.appendChild(heirHead);
  const heirBox = place(h('section', { class: 'ocbg-meta-hall-heir' }), 1428, 236, 420, 696);
  const gen = h('div', { class: 'ocbg-meta-heir-gen' }, heir.GenerationLabel, h('span', null, InheritanceText.RoleName));
  const nameEl = h('div', { class: 'ocbg-meta-heir-name' });
  const nameChars = [...heir.Name].map((ch) => { const c = h('span', null, ch); nameEl.appendChild(c); return c; });
  const seal = h('div', { class: 'ocbg-meta-seal' }, ...MetaText.NameSeal.map((c) => h('span', null, c)));
  const nameRow = h('div', { class: 'ocbg-meta-heir-namerow' }, nameEl, seal);
  heirBox.appendChild(gen);
  heirBox.appendChild(nameRow);

  const attire = h('div', { class: 'ocbg-meta-heir-attire' },
    h('h4', null, InheritanceText.AttireHeading),
    h('div', { class: 'ocbg-meta-heir-tier' }, heir.TierName),
    h('p', { class: 'ocbg-meta-heir-reason' }, heir.TierReason),
    h('ul', null, ...heir.Attire.map((a) => h('li', null,
      h('i', { class: 'ocbg-meta-swatch' + (a.Color ? '' : ' is-none'), style: a.Color ? { background: a.Color } : null }),
      h('span', { class: 'ocbg-meta-part' }, a.Part),
      h('span', null, a.Text)))));
  heirBox.appendChild(attire);
  const fig = h('div', { class: 'ocbg-meta-heir-figure' });
  const foot = O.FootLight(180, false);
  place(foot, -20, 196);
  fig.appendChild(foot);
  fig.appendChild(O.Figure('swordsman'));
  heirBox.appendChild(fig);

  if (view.Survivor) {
    const s = view.Survivor;
    heirBox.appendChild(h('div', { class: 'ocbg-meta-heir-survivor' },
      h('h4', null, view.SurvivorHeading),
      h('p', { class: 'ocbg-meta-heir-survivor-line' }, s.Line),
      h('ul', null, ...s.Lines.map((l) => h('li', null, icon('plus', 18, 'var(--amber)'), l))),
      s.DurationLine ? h('p', { class: 'ocbg-meta-heir-duration' }, s.DurationLine) : null));
  } else if (view.SurvivorSpent) {
    // The life that used the bonus has closed with no new survivor: the block says the bonus ended (R-14).
    const s = view.SurvivorSpent;
    heirBox.appendChild(h('div', { class: 'ocbg-meta-heir-survivor is-spent' },
      h('h4', null, view.SurvivorHeading),
      h('p', { class: 'ocbg-meta-heir-survivor-line' }, s.Line),
      h('ul', null, ...s.Lines.map((l) => h('li', null, l)))));
  }
  root.appendChild(heirBox);

  const startBtn = place(h('button', { class: 'ocbg-meta-primary', type: 'button', onclick: () => depart() },
    h('span', null, view.StartButton), h('small', null, 'Enter')), 1428, 948, 420, 76);
  root.appendChild(startBtn);
  keys = place(h('div', { class: 'ocbg-meta-keys' }), 72, 1028);
  root.appendChild(keys);
  paintKeys();

  // ---- arrival and the naming ----
  const fade = styler(place(h('div', { class: 'ocbg-meta-fade' }), 0, 0, 1920, 1080), { o: 1 });
  root.appendChild(fade.el);
  let busy = false;
  const lamp = { a: 0 };
  Object.defineProperty(lamp, 'v', { get: () => lamp.a, set: (v) => { lamp.a = v; backdrop.alpha = v; } });
  sound.play('hallArrive', 80);
  clock.tween(fade, { o: 0 }, 700, 'out');
  clock.tween(lamp, { v: 1 }, 1600, 'out', 150);
  for (const el of [title, lead]) { const p = styler(el, { o: 0, y: 10 }); clock.tween(p, { o: 1, y: 0 }, 600, 'out', 250); }
  riseIn(clock, [livesHead, ...lifeCards, rules], { delay: 380, step: 90 });
  riseIn(clock, [traceHead, ...strata], { delay: 480, step: 60, dy: 10 });
  const pd = styler(detail, { o: 0, x: 16 });
  clock.tween(pd, { o: 1, x: 0 }, 500, 'out', 900);
  riseIn(clock, [heirHead, gen], { delay: 1000, step: 80 });
  const attireIn = [attire, fig, ...heirBox.querySelectorAll('.ocbg-meta-heir-survivor')];
  attireIn.forEach((el) => { styler(el, { o: 0 }); });
  nameChars.forEach((c, i) => {
    const p = styler(c, { o: 0, y: 14 });
    clock.tween(p, { o: 1, y: 0 }, 420, 'out', 1250 + i * 110);
  });
  const sealAt = 1250 + nameChars.length * 110 + 240;
  const ps = styler(seal, { o: 0, s: 1.9, r: -14 });
  clock.wait(sealAt).then(() => {
    if (!alive) return;
    clock.tween(ps, { o: 1, s: 1, r: -4 }, 170, 'in').then(() => {
      if (!alive) return;
      sound.play('heirNamed');
      seal.classList.add('is-stamped');
      attireIn.forEach((el, i) => { const p = styler(el, { o: 0, y: 10 }); clock.tween(p, { o: 1, y: 0 }, 520, 'out', 120 + i * 120); });
    });
  });
  const pb = styler(startBtn, { o: 0, y: 12 });
  clock.tween(pb, { o: 1, y: 0 }, 520, 'out', sealAt + 500);
  const pk = styler(keys, { o: 0 });
  clock.tween(pk, { o: 1 }, 600, 'out', sealAt + 700);
  corner.forEach((el) => { const pg = styler(el, { o: 0 }); clock.tween(pg, { o: 1 }, 500, 'out', 600).then(() => { el.style.transform = ''; }); });

  function flames(now) {
    for (let i = 0; i < flameEls.length; i++) {
      const k = 0.5 - 0.5 * Math.cos(((now + i * 700) / 1900) * 2 * Math.PI);
      flameEls[i].style.opacity = String(0.55 + 0.45 * k);
      flameEls[i].style.transform = `scale(${0.85 + 0.25 * k})`;
    }
  }

  async function leaveToTitle() {
    if (busy || !ctx.onTitle) return;
    busy = true;
    sound.play('uiCancel');
    clock.tween(lamp, { v: 0.35 }, 600, 'inout');
    await clock.tween(fade, { o: 1 }, 500, 'in', 100);
    if (alive) ctx.onTitle();
  }

  async function depart() {
    if (busy) return;
    busy = true;
    sound.play('uiConfirm');
    const lifeStart = lineage.Current ? null : startLife(lineage, ctx.seed ?? null);
    clock.tween(lamp, { v: 0.35 }, 700, 'inout');
    await clock.tween(fade, { o: 1 }, 650, 'in', 150);
    if (alive && ctx.onDepart) ctx.onDepart(lifeStart);
  }

  const popKeys = pushKeys((e) => {
    if (busy) return false;
    // ←→ go from trace to trace; ↑↓ and PgUp PgDn read the selected trace's list when it runs past the panel.
    if (e.key === 'ArrowRight') { if (view.Traces.length) select((sel + 1) % view.Traces.length); return true; }
    if (e.key === 'ArrowLeft') { if (view.Traces.length) select((sel - 1 + view.Traces.length) % view.Traces.length); return true; }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { scrollTrace(e.key === 'ArrowDown' ? 84 : -84); return true; }
    if (e.key === 'PageDown' || e.key === 'PageUp') {
      const page = traceBody ? Math.max(84, traceBody.clientHeight - 84) : 0;
      scrollTrace(e.key === 'PageDown' ? page : -page);
      return true;
    }
    if (e.key === 'Enter') { depart(); return true; }
    // a held key repeats: only a fresh press opens an overlay (the overlays close on a fresh press only, too)
    if (e.key === 'Escape' && ctx.onSettings) { if (!e.repeat) { sound.play('uiClick'); ctx.onSettings(); } return true; }
    if ((e.key === 'j' || e.key === 'J') && ctx.onJournal) { if (!e.repeat) ctx.onJournal(); return true; }
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
