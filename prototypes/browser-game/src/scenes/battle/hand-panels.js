// The panels around the hand: a port of CardPanels.cs (the hover line above a hovered card §7.2, the reason
// it cannot be played from here on its right §5.1, the right-click detail above it §7.4, the meaning of an
// underlined term §7.1), RefusalLineView.cs (§5.4: why a released card went back, 3.5 s) and the scene's
// guide line (DepictionPlayer.handGuide). Every word comes from the card's face or the source; these only
// place and print them. Looks: OCB.HoverLine, .ocb-reachpanel, .ocb-detail, .ocb-tip, OCB.RefusalLine.
import { h } from '../common/stage.js';
import { RefusalLineLayout } from '../../bridge/depictionScript/RefusalLineLayout.js';
import { buildDetail, wrapLine } from './hand-card.js';

// CardPanels' measures.
const HOVER_W = 420;          // §7.2: a 420 px tag above the card
const HOVER_GAP = 8;
/** The text's width inside the DS hover line: 420 − padding 14 × 2 − the lamp's 16 − the gap 10. */
const HOVER_TEXT_W = HOVER_W - 28 - 16 - 10;
const REACH = { w: 280, h: 76, gap: 12, rightLimit: 1620 };   // §5.1
const DETAIL = { w: 560, h: 280 };                              // §7.4
/**
 * Review 17 (§3.13): where the detail may stand over the field — the floor's row (FloorView: its top at y 560, its tiles
 * 40 tall) stays clear by DETAIL_GAP, as do the omen badges; its top no higher than under the corner buttons (y 20–76).
 */
const DETAIL_FLOOR = { top: 560, h: 40 };
const DETAIL_GAP = 4;
const DETAIL_MIN_TOP = 84;
const TERM_W = 340;                                             // §7.1
const EDGE = 16;              // ClampX: inside the screen with 16 px to spare
/**
 * How fast the guide line fades out and back. Review fix (scene choice): it steps aside while a hovered card's panels
 * (the hover line, the reach panel) or the detail stand in its band (y ≈ 757, where the lifted card's top, the hover
 * line and the detail land), as it already does for the refusal line — torn pieces of it showed between them. It
 * stays up under a held or selected card (Unity's RefreshPlayableLook and the design system's guideIdle).
 */
const GUIDE_FADE_MS = 120;
/** Review 19: the guide line keeps this far inside an open drawer's edge. */
const GUIDE_CLEAR = 16;

/** RefusalLineLayout.Top is a C# property; the port may keep it as a getter or a method. */
function refusalTop() {
  const t = RefusalLineLayout.Top;
  return typeof t === 'function' ? t.call(RefusalLineLayout) : t;
}

/** CardPanels.BreakAfter: the reach hint breaks after 「相手との間合いが」. */
function breakAfter(text, mark) {
  const at = text.indexOf(mark);
  return at < 0 ? [text] : [text.slice(0, at + 1), text.slice(at + mark.length)];
}

/**
 * Review fix (a two-line hover line over a card under a number panel covered the panel's bottom edge): the line
 * keeps PANEL_GAP off every number panel (env.geom.statusBoxes) where it can. Review 17: it always stands where
 * Unity's CardPanels.ShowHover puts it, HOVER_GAP over the raised card — moved down onto the card it covered the card's
 * top edge and the reach panel's corner (battle-visual-v1 §5.1 「パネルとは重なりません」), and set beside the card it
 * left the card altogether. At that height it slides sideways off the panels in its way, keeping the card's middle
 * under it, inside the screen; when no slide clears them, it stands, still over the card's middle, where it covers the
 * least of them.
 */
const PANEL_GAP = 4;
const CHIP_WEIGHT = 4;
function makeClearOfPanels(env) {
  return function clearOfPanels(left, top, height, card) {
    const panels = env.geom.statusBoxes ? env.geom.statusBoxes().map((p) => p.box) : [];
    const across = (l, p) => l < p.x + p.w + PANEL_GAP && l + HOVER_W > p.x - PANEL_GAP;
    const under = (p) => top < p.y + p.h + PANEL_GAP && top + height > p.y - PANEL_GAP;
    const inRow = panels.filter(under);
    const hits = (l) => inRow.filter((p) => across(l, p));
    const hit = hits(left);
    if (!hit.length) return { left, top, ok: true };
    // Sideways: the smaller slide that clears the panels in the way, if the line still covers the card's middle.
    const lo = Math.max(EDGE, card.x + card.w / 2 - HOVER_W);
    const hi = Math.min(1920 - EDGE - HOVER_W, card.x + card.w / 2);
    const toLeft = Math.min(...hit.map((p) => p.x - PANEL_GAP - HOVER_W));
    const toRight = Math.max(...hit.map((p) => p.x + p.w + PANEL_GAP));
    const options = [toLeft, toRight]
      .filter((l) => l >= lo && l <= hi && !hits(l).length)
      .sort((a, b) => Math.abs(a - left) - Math.abs(b - left));
    if (options.length) return { left: options[0], top, ok: true };
    // None clears: where, over the card's middle, it covers the least of the panels in its row — their status chips (the
    // panel's last row, left-aligned: StatusBarView's chipsW) counting CHIP_WEIGHT times as much as the rest.
    const span = (l, a, b) => Math.max(0, Math.min(l + HOVER_W, b) - Math.max(l, a));
    const cover = (l) => inRow.reduce((sum, p) => sum + span(l, p.x - PANEL_GAP, p.x + p.w + PANEL_GAP)
      + (p.chipsW ? CHIP_WEIGHT * span(l, p.x - PANEL_GAP, p.x + p.chipsW + PANEL_GAP) : 0), 0);
    const tries = [lo, hi, ...inRow.filter((p) => p.chipsW).map((p) => p.x + p.chipsW + PANEL_GAP)].filter((l) => l >= lo && l <= hi);
    let best = Math.max(lo, Math.min(hi, left));
    for (const l of tries) if (cover(l) < cover(best) - 0.5) best = l;
    return { left: best, top, ok: false };
  };
}

/**
 * Review fix (§7.4 / §7 ③ 全文と用語の定義: the glossary ended on 「詰める / 離れる：自分が動いて、相手との」, cut where the
 * box's whole lines ran out): a definition is shown whole or not at all, and where the box cannot hold the rest, the
 * last line that fits ends in 「…」 (scene text), so a cut never reads as the end of a sentence. The body text likewise.
 */
const ELLIPSIS = '…'; // scene text
/** The longest head of `node`'s text that, with the ellipsis, keeps `fits()`; '' when none does. */
function cutToFit(node, text, fits) {
  let lo = 0;
  let hi = text.length;
  node.data = text;
  if (fits()) return true;
  while (lo < hi) {
    const mid = Math.ceil((lo + hi) / 2);
    node.data = text.slice(0, mid).trimEnd() + ELLIPSIS;
    if (fits()) lo = mid; else hi = mid - 1;
  }
  node.data = lo > 0 ? text.slice(0, lo).trimEnd() + ELLIPSIS : '';
  return lo > 0;
}
/** The text node a row ends with (the definition after 「：」), made if missing. */
function tailText(el) {
  const last = el.lastChild;
  if (last && last.nodeType === 3) return last;
  const t = document.createTextNode('');
  el.appendChild(t);
  return t;
}
function fitDetail(el) {
  const body = el.querySelector('.ocbg-battle-detail-text');
  if (body && body.scrollHeight > body.clientHeight + 1) {
    const node = tailText(body);
    cutToFit(node, node.data, () => body.scrollHeight <= body.clientHeight + 1);
  }
  const box = el.querySelector('.ocbg-battle-detail-terms');
  if (!box) return;
  const limit = box.clientHeight;
  const rows = [...box.children];
  const fitsRow = (r) => r.offsetTop + r.offsetHeight <= limit + 0.5;
  for (let i = 0; i < rows.length; i++) {
    const r = rows[i];
    if (fitsRow(r)) continue;
    // This definition does not fit whole: it keeps the lines it has room for, ending in 「…」; with no room for a
    // line of it, the definition before it takes the 「…」.
    const node = tailText(r);
    const kept = r.offsetTop < limit && cutToFit(node, node.data, () => fitsRow(r));
    if (!kept) {
      r.remove();
      const prev = rows[i - 1];
      if (prev) { const pn = tailText(prev); cutToFit(pn, pn.data + ELLIPSIS, () => fitsRow(prev)); }
    }
    rows.slice(i + 1).forEach((x) => x.remove());
    break;
  }
}

function clampX(cx, width) {
  const half = width / 2;
  return Math.max(EDGE + half, Math.min(1920 - EDGE - half, cx));
}

export function createPanels(env) {
  const { OCB, layer, real, L } = env;
  const clearOfPanels = makeClearOfPanels(env);

  // ---- the guide line (handGuide): centre x 960, y ≈ 757, body 22 px, no panel ----------------------
  const guide = h('div', { class: 'ocbg-battle-guide' });
  guide.style.top = L.GUIDE.y + 'px';
  guide.style.height = L.GUIDE.h + 'px';
  layer.hud.appendChild(guide);
  let guideText = '';
  let guideAlpha = 1;
  let guideAway = false;

  // ---- the refusal line (RefusalLineView) ---------------------------------------------------------
  let refusalEl = null;
  /** Real ms the line has left to stand (it counts only while it shows), its alpha, its width, what to call at its end. */
  let refusalLeft = 0;
  let refusalAlpha = 1;
  let refusalW = 0;
  let refusalOnEnd = null;

  // ---- hover line / reach hint / detail / term (CardPanels) ---------------------------------------
  let hover = null;    // { el, text, lit, plain, height }
  let reach = null;    // { el, text }
  let detail = null;   // { el, id }
  let term = null;     // { el, mark }

  const guideSpan = h('span');
  guide.appendChild(guideSpan);
  let guideW = -1;
  function showGuideText() {
    const text = refusalEl ? '' : guideText;
    if (guideSpan.textContent !== text) { guideSpan.textContent = text; guideW = -1; }
  }
  /**
   * Review 19 (with the pile list open while 俊敏's choice waited, the guide's first character stood under the drawer):
   * the line keeps GUIDE_CLEAR inside the part of the screen an open drawer leaves — the pile list's (left) or the
   * journal's (right) — moving off its centre only as far as that needs.
   */
  function placeGuide() {
    const left = env.geom.drawerRight ? env.geom.drawerRight() : 0;
    const journal = env.geom.journalEdge ? env.geom.journalEdge() : null;
    if (!(left > 0) && journal == null) { if (guide.style.translate) guide.style.translate = ''; return; }
    if (guideW < 0) guideW = guideSpan.offsetWidth || 0;
    const lo = left > 0 ? left + GUIDE_CLEAR : 0;
    const hi = journal != null ? journal - GUIDE_CLEAR : 1920;
    const x0 = 960 - guideW / 2;
    const x1 = 960 + guideW / 2;
    const dx = x0 < lo ? lo - x0 : x1 > hi ? hi - x1 : 0;
    const t = Math.abs(dx) > 0.5 ? dx.toFixed(1) + 'px 0' : '';
    if (guide.style.translate !== t) guide.style.translate = t;
  }

  function hideHover() {
    if (hover) { hover.el.remove(); hover = null; }
    if (reach) { reach.el.remove(); reach = null; }
    api.hideTerm();
  }

  const api = {
    // ---- guide ----
    setGuide(text) { guideText = text || ''; showGuideText(); },
    /** Per frame: the guide fades out while a card is hovered, held or selected, and back after. */
    stepGuide(dtScene, away) {
      guideAway = !!away;
      const target = guideAway ? 0 : 1;
      const step = GUIDE_FADE_MS > 0 ? dtScene / GUIDE_FADE_MS : 1;
      guideAlpha = target > guideAlpha ? Math.min(target, guideAlpha + step) : Math.max(target, guideAlpha - step);
      guide.style.opacity = guideAlpha.toFixed(3);
      placeGuide();
    },

    /** A hovered card's panels or the detail are up (the guide steps aside for them). */
    get panelsUp() { return !!hover || !!reach || !!detail; },

    // ---- refusal (§5.4) ----
    get refusalShown() { return !!refusalEl; },
    /** The refusal line's box on the stage, or null when none is up. */
    refusalBox() {
      if (!refusalEl) return null;
      if (!refusalW) refusalW = refusalEl.offsetWidth || 900;
      return { x: RefusalLineLayout.CenterX - refusalW / 2, y: refusalTop(), w: refusalW, h: RefusalLineLayout.Height };
    },
    /**
     * Per frame (real ms): review 16 — while a raised card stands over the line (`away`), the line fades out (120 ms)
     * and its RefusalLineLayout.Seconds stop counting, so the card's name and values read and the reason comes back
     * when the card goes down. Otherwise the seconds run out and the line goes.
     */
    stepRefusal(dtReal, away) {
      if (!refusalEl) return;
      const step = GUIDE_FADE_MS > 0 ? dtReal / GUIDE_FADE_MS : 1;
      refusalAlpha = away ? Math.max(0, refusalAlpha - step) : Math.min(1, refusalAlpha + step);
      refusalEl.style.opacity = refusalAlpha >= 1 ? '' : refusalAlpha.toFixed(3);
      if (away) return;
      refusalLeft -= dtReal;
      if (refusalLeft > 0) return;
      const end = refusalOnEnd;
      api.hideRefusal();
      if (end) end();
    },
    /** Puts the reason up on one line above the hand for RefusalLineLayout.Seconds (real time); the guide steps aside. */
    showRefusal(text, onEnd) {
      api.hideRefusal();
      if (!text) return;
      refusalLeft = RefusalLineLayout.Seconds * 1000;
      refusalAlpha = 1;
      refusalW = 0;
      refusalOnEnd = onEnd || null;
      refusalEl = OCB.RefusalLine(text);
      refusalEl.classList.add('ocbg-battle-refusal');
      refusalEl.style.top = refusalTop() + 'px';
      refusalEl.style.left = RefusalLineLayout.CenterX + 'px';
      refusalEl.style.height = RefusalLineLayout.Height + 'px';
      refusalEl.style.padding = '0 ' + RefusalLineLayout.Pad + 'px';
      refusalEl.style.gap = RefusalLineLayout.DotGap + 'px';
      refusalEl.style.fontSize = RefusalLineLayout.TextSize + 'px';
      const dot = refusalEl.querySelector('.ocb-dot');
      if (dot) { dot.style.width = RefusalLineLayout.DotSize + 'px'; dot.style.height = RefusalLineLayout.DotSize + 'px'; }
      layer.float.appendChild(refusalEl);
      showGuideText();
    },
    hideRefusal() {
      refusalOnEnd = null;
      if (refusalEl) { refusalEl.remove(); refusalEl = null; }
      showGuideText();
    },

    // ---- hover (§7.2 / §5.1) ----
    /**
     * ShowHover: the hover line centred above `box` (the card's upright bounds now) and, when the script
     * says nobody is in reach (CardFace.ReachHint), the reason on its right (on its left past x 1620).
     */
    showHover(face, box, lit) {
      if (!face) { hideHover(); return; }
      const reachRight = box.x + box.w + REACH.gap + REACH.w <= REACH.rightLimit;
      if (face.HoverLine) {
        if (!hover || hover.text !== face.HoverLine || hover.lit !== lit || hover.plain !== !!face.Plain) {
          if (hover) hover.el.remove();
          const el = OCB.HoverLine(wrapLine(face.HoverLine, HOVER_TEXT_W, 18), lit);
          el.classList.add('ocbg-battle-hoverline');
          // The lamp-like dot is off on a plain card (CardPanels: _hoverDot.enabled = !face.Plain).
          const dot = el.querySelector('.ocb-lamp');
          if (dot && face.Plain) dot.style.visibility = 'hidden';
          layer.over.appendChild(el);
          hover = { el, text: face.HoverLine, lit, plain: !!face.Plain, height: el.offsetHeight || 48 };
        }
        const at = clearOfPanels(clampX(box.x + box.w / 2, HOVER_W) - HOVER_W / 2, box.y - HOVER_GAP - hover.height, hover.height, box);
        hover.el.style.left = at.left.toFixed(1) + 'px';
        hover.el.style.top = at.top.toFixed(1) + 'px';
      } else if (hover) { hover.el.remove(); hover = null; }

      if (face.ReachHint) {
        if (!reach || reach.text !== face.ReachHint) {
          if (reach) reach.el.remove();
          const parts = breakAfter(face.ReachHint, 'が ');
          const el = h('div', { class: 'ocb-reachpanel ocbg-battle-reachpanel' }, OCB.icon('range', 22, 'var(--ink)'),
            h('div', null, parts.map((t) => h('div', null, t))));
          layer.over.appendChild(el);
          reach = { el, text: face.ReachHint };
        }
        reach.el.style.left = (reachRight ? box.x + box.w + REACH.gap : box.x - REACH.gap - REACH.w).toFixed(1) + 'px';
        reach.el.style.top = box.y.toFixed(1) + 'px';
      } else if (reach) { reach.el.remove(); reach = null; }
    },
    hideHover,

    // ---- term (§7.1) ----
    /** ShowTerm: the meaning above the underlined word (`word`: its stage box). */
    showTerm(word, mark) {
      if (!word || !mark) { api.hideTerm(); return; }
      if (!term || term.mark !== mark) {
        api.hideTerm();
        const el = h('div', { class: 'ocb-tip ocbg-battle-termtip' },
          h('div', { class: 'ocb-tip-head' }, mark.Word || ''),
          h('p', null, mark.Meaning || ''));
        layer.over.appendChild(el);
        term = { el, mark, height: el.offsetHeight || 60 };
      }
      term.el.style.left = (clampX(word.x + word.w / 2, TERM_W) - TERM_W / 2).toFixed(1) + 'px';
      term.el.style.top = (word.y - HOVER_GAP - term.height).toFixed(1) + 'px';
    },
    hideTerm() {
      if (term) { term.el.remove(); term = null; }
    },

    // ---- detail (§7.4) ----
    get detailShown() { return !!detail; },
    get detailFor() { return detail ? detail.id : ''; },
    /** ToggleDetail: opens the detail of `face` straight above `box`, or closes it when it is already open for that card. */
    toggleDetail(face, box, extra) {
      if (detail && face && detail.id === face.Id) { api.hideDetail(); return; }
      api.showDetail(face, box, extra);
    },
    showDetail(face, box, extra) {
      api.hideDetail();
      if (!face || !face.Detail) return;
      const el = buildDetail(OCB, face, extra);
      layer.over.appendChild(el);
      // Review 19 (a repeat: the glossary still cut a definition short, now in 「…」): the detail grows to hold its whole
      // text and every definition (battle-visual-v1 §7 ③ 全文と用語の定義); only past DETAIL_MAX_H does a cut fall back.
      try { fitDetail(el); } catch (err) { /* a layout read that fails leaves the CSS's cut */ }
      detail = { el, id: face.Id, h: Math.max(DETAIL.h, el.offsetHeight || DETAIL.h) };
      // The detail stands where the hover line was.
      if (hover) { hover.el.remove(); hover = null; }
      api.hideTerm();
      api.placeDetail(box);
    },
    /**
     * §3.13: 「人型の胴は覆いますが、予兆・数値パネル・スタミナの札・手札は隠しません」 — the omen being A1's badge and its
     * aimed cells on the floor (§1.1). Unity's CardPanels places the detail straight above the card (HOVER_GAP over it),
     * which covers the floor's row and, over the left cards, the hero's number panel. Review 17 (raised to clear the
     * panels, y 330–610, it hid the gap number and the hero's cell): in order —
     *  1. straight above the card, when that covers no number panel, omen badge or floor cell (the floor's row is the
     *     line's whole width, so only at the screen's edges);
     *  2. over the floor: its bottom DETAIL_GAP over the floor's top, x on the card's middle, where it covers the
     *     figures' bodies only, when no omen badge stands in its way;
     *  3. the same height, slid sideways just clear of the badges in its way, inside the screen;
     *  4. with neither, Unity's place, raised to keep off the number panels (it then covers part of the floor's row).
     */
    placeDetail(box) {
      if (!detail || !box) return;
      const DH = detail.h || DETAIL.h;
      const left = clampX(box.x + box.w / 2, DETAIL.w) - DETAIL.w / 2;
      const unity = box.y - HOVER_GAP - DH;
      const panels = env.geom.statusBoxes ? env.geom.statusBoxes().map((p) => p.box) : [];
      // Review 19: 俊敏's pending choice is no less to keep clear than an omen badge (the detail hid its 「前へ」 mark).
      const badges = (env.geom.omenBoxes ? env.geom.omenBoxes() : []).concat(env.geom.freeStepBoxes ? env.geom.freeStepBoxes() : []);
      const floorRow = { x: 0, y: DETAIL_FLOOR.top, w: 1920, h: DETAIL_FLOOR.h };
      const hitsAny = (l, t, rects) => rects.filter((r) => r && l < r.x + r.w + DETAIL_GAP && l + DETAIL.w > r.x - DETAIL_GAP && t < r.y + r.h + DETAIL_GAP && t + DH > r.y - DETAIL_GAP);
      let at = null;
      if (!hitsAny(left, unity, panels.concat(badges, [floorRow])).length) at = { left, top: unity };
      const over = DETAIL_FLOOR.top - DETAIL_GAP - DH;
      if (!at && over >= DETAIL_MIN_TOP) {
        const inWay = hitsAny(left, over, badges);
        if (!inWay.length) at = { left, top: over };
        else {
          const lo = EDGE;
          const hi = 1920 - EDGE - DETAIL.w;
          const options = [Math.min(...inWay.map((b) => b.x)) - DETAIL_GAP - DETAIL.w, Math.max(...inWay.map((b) => b.x + b.w)) + DETAIL_GAP]
            .filter((l) => l >= lo && l <= hi && !hitsAny(l, over, badges).length)
            .sort((a, b) => Math.abs(a - left) - Math.abs(b - left));
          if (options.length) at = { left: options[0], top: over };
        }
      }
      if (!at) {
        let top = unity;
        for (const p of panels.concat(env.geom.freeStepBoxes ? env.geom.freeStepBoxes() : [])) {
          if (left < p.x + p.w + PANEL_GAP && left + DETAIL.w > p.x - PANEL_GAP && top + DH > p.y - PANEL_GAP) top = Math.min(top, p.y - PANEL_GAP - DH);
        }
        at = { left, top: Math.max(DETAIL_MIN_TOP, top) };
      }
      detail.el.style.left = at.left.toFixed(1) + 'px';
      detail.el.style.top = at.top.toFixed(1) + 'px';
    },
    hideDetail() {
      if (detail) { detail.el.remove(); detail = null; }
    },

    hideAll() {
      hideHover();
      api.hideDetail();
    },
    destroy() {
      api.hideAll();
      api.hideRefusal();
      guide.remove();
    },
  };
  return api;
}
