// The hand module's card face: a port of unity-port/unity-project-kit/Assets/View/Depiction/CardView.cs
// (battle-visual-v1 §6, 案 C, 216×304) drawn with the design system's card (bundle.css `.ocb-card` and its
// parts, `OCB.icon`). It binds a CardFace and prints what the script wrote: no rule, no number of its own.
// The name and the description fit their boxes with the ported CardTextFit (#210 / #567), measured with
// the page's own fonts on a canvas (Unity measures with Text.preferredWidth / preferredHeight).
//
// Also the right-click detail (CardPanels.ShowDetail, §7.4): the card at 80% beside its name, kind line,
// full text and terms.
import { h } from '../common/stage.js';
import { CardKind, CardAim, CardValueKind, TermRow } from '../../bridge/depictionScript/DepictionTypes.js';
import { CardTextFit } from '../../bridge/depictionScript/CardTextFit.js';
import { Constants } from '../../core/battle/Constants.js';

// ---- CardView's §6.1 measures (px from the card's top-left) ---------------------------------------
export const CARD_W = 216;
export const CARD_H = 304;
const NAME_SIZE = 22;
const NAME_BOX = { w: CARD_W - 16 - 12, h: 28 };          // NameAt (16, 60), right 12, height 28
const TEXT_SIZE = 16;
const TEXT_LINE = 22;                                      // CardView.TextLine
const TEXT_AT = 156;                                       // TextAt.y
const VALUES_AT = 94;                                      // ValuesAt.y (the lasting rows start here)
const TEXT_W = CARD_W - 16 - 12;                           // 188
/**
 * CardView.MinFontSize is 12; the scene stops at 14 (undecided.md 5, 案 B「14 px で止める」), so a card's text keeps
 * to the readable sizes at 1280×720 and a row that would go smaller wraps instead (CardTextFit's own next step).
 */
const MIN_FONT = 14;
/** Trait rows (§6.1 特性の箱, 161 px of text at 16): Unity lets a long row run over; the scene shrinks it, but not under 14. */
const TRAIT_TEXT_W = CARD_W - 12 - 7 - (8 + 16 + 8) - 4;
const TRAIT_MIN_FONT = MIN_FONT;

/** BattleTheme.KindFrame / KindInk through the DS's attribute classes (.ocb-card.is-…). A move card keeps the card's steel. */
const KIND_CLASS = { [CardKind.Attack]: 'is-attack', [CardKind.Guard]: 'is-defense', [CardKind.Skill]: 'is-skill', [CardKind.Stance]: 'is-stance' };
const KIND_INK = { [CardKind.Attack]: 'var(--phosphor)', [CardKind.Guard]: 'var(--guard)', [CardKind.Skill]: 'var(--amber)', [CardKind.Stance]: 'var(--hp-text)' };
/** VisualArt.KindIcon: sword, shield, flag; a skill (and a move) the star. */
const KIND_ICON = { [CardKind.Attack]: 'sword', [CardKind.Guard]: 'shield', [CardKind.Stance]: 'flag' };

/** VisualArt.ValueIcon / ValueColor (§8). */
export function valueIcon(kind) {
  switch (kind) {
    case CardValueKind.Power: return 'sword';
    case CardValueKind.Guard: return 'shield';
    case CardValueKind.Heal: return 'heal';
    case CardValueKind.Close:
    case CardValueKind.Push: return 'forward';
    case CardValueKind.Back:
    case CardValueKind.Pull: return 'back';
    default: return 'draw';
  }
}
export function valueColor(kind) {
  switch (kind) {
    case CardValueKind.Power: return 'var(--phosphor)';
    case CardValueKind.Guard: return 'var(--guard)';
    case CardValueKind.Heal: return 'var(--hp-text)';
    default: return 'var(--ink)';
  }
}

/**
 * The icon beside the predicted number. Unity (DepictionPlayer.ShowTargetMark) takes Values[0], which on a card
 * that moves first (足捌き・後: ← 1, Guard 12) puts the move's arrow beside the Guard. The scene picks the value
 * the number is (battle_ui_ux_v2 §3.3: on a self card Guard → 回復 → ドロー, on an enemy the damage), and keeps
 * Unity's choice for anything else.
 */
const SELF_ORDER = [CardValueKind.Guard, CardValueKind.Heal, CardValueKind.Draw];
export function previewKind(face) {
  const values = face.Values || [];
  if (face.Aim === CardAim.Self) {
    for (const k of SELF_ORDER) if (values.some((v) => v.Kind === k)) return k;
    // A stance's number is the Guard its trait puts up (岩の構え: 置く時 予兆が攻撃 → Guard +3).
    if (values.length === 0) return CardValueKind.Guard;
  } else if (values.some((v) => v.Kind === CardValueKind.Power)) return CardValueKind.Power;
  if (values.length > 0) return values[0].Kind;
  return face.Kind === CardKind.Guard ? CardValueKind.Guard : CardValueKind.Power;
}

// ---- measuring (Unity's Text.preferredWidth / preferredHeight with the font in use) -----------------
let canvasCtx = null;
const widthCache = new Map();
function fontVar(name, fallback) {
  try {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name);
    return v && v.trim() ? v.trim() : fallback;
  } catch (err) { return fallback; }
}
function textWidth(text, font) {
  const key = font + '\u0000' + text;
  const hit = widthCache.get(key);
  if (hit != null) return hit;
  if (!canvasCtx) canvasCtx = document.createElement('canvas').getContext('2d');
  canvasCtx.font = font;
  const w = canvasCtx.measureText(text).width;
  // A width measured before the web fonts arrived would be the fallback's; keep it only once they are in.
  if (!document.fonts || document.fonts.status === 'loaded') widthCache.set(key, w);
  return w;
}
function lines(text) { return String(text == null ? '' : text).split('\n'); }
/** CardTextFit's TextMeasure for one face: the widest line, and the height the given line pitch gives. */
function measureWith(weight, family, pitch) {
  return (text, size) => {
    const ls = lines(text);
    let w = 0;
    for (const l of ls) w = Math.max(w, textWidth(l, weight + ' ' + size + 'px ' + family));
    return { width: w, height: ls.length * size * pitch };
  };
}
let measures = null;
function fonts() {
  if (!measures) {
    const display = fontVar('--font-display', 'serif');
    const body = fontVar('--font-body', 'sans-serif');
    measures = {
      // The name is one line in its 28 px box (22 px over a 28 px pitch).
      name: measureWith(700, display, 28 / 22),
      // CardView: lineSpacing = TextLine / (TextSize × 1.15), so a line is size × 22 / 16 at any size.
      desc: measureWith(400, body, TEXT_LINE / TEXT_SIZE),
      traitTop: measureWith(400, body, 22 / 16),
      traitBottom: measureWith(700, body, 22 / 16),
    };
  }
  return measures;
}

// ---- the face -----------------------------------------------------------------------------------

function costPart(OCB, face) {
  const cost = h('div', { class: 'ocb-cost' });
  fillCost(cost, face.Cost, face.CostDrop || 0);
  return cost;
}
/** CardView.BindCost: the number, filled and marked "▼n" when a discount took a cost off. */
function fillCost(el, cost, drop) {
  el.textContent = String(cost);
  el.classList.toggle('is-down', drop > 0);
  if (drop > 0) el.appendChild(h('span', { class: 'ocb-down' }, '▼' + drop)); // scene text (CardView "▼" + drop)
}

function reachPart(OCB, face) {
  // CardView.BindReach: the number tag (⊢⊣ 1〜2), the self mark (人型 + 自分), or nothing.
  const glyph = face.RequiredRangeGlyph || '';
  const aims = face.Aim === CardAim.Single || glyph !== '';
  if (aims && glyph !== '') return h('div', { class: 'ocb-reach' }, OCB.icon('range', 18, 'var(--ink)'), glyph);
  if (face.Aim === CardAim.Self) return h('div', { class: 'ocb-reach' }, OCB.icon('person', 22, 'var(--ink)'), h('small', null, '自分')); // scene text (CardView "自分")
  return null;
}

function valuesPart(OCB, face) {
  let values = face.Values;
  // A v4.2 source writes one value string only: it stands as the one column (CardView.BindValues).
  if ((!values || values.length === 0) && face.ValueText && !face.Lasting) {
    values = [{ Kind: face.Kind === CardKind.Guard ? CardValueKind.Guard : CardValueKind.Power, Number: face.ValueText, Word: '' }];
  }
  if (!values || values.length === 0) return null;
  const box = h('div', { class: 'ocb-values' });
  values.slice(0, 2).forEach((v) => { // CardFaceLimits.Values
    const ink = valueColor(v.Kind);
    box.appendChild(h('div', { class: 'ocb-val', style: { '--val-ink': ink } }, OCB.icon(valueIcon(v.Kind), 24, ink), h('b', null, String(v.Number)), h('small', null, v.Word || '')));
  });
  return box;
}

/**
 * battle_ui_ux_v2 §3.5 集中 / 威圧 and §5.10 強化 (review 21): while the player holds one of them the cards it acts on carry
 * its mark beside their values — 「→」 with 集中's icon on every card with a value (its next card resolves one column right),
 * 「×1.5」 with 強化's on a card with an attack value, 「−3」 with 威圧's on a card with an attack or a Guard value. Which
 * card takes which mark is read off the face's own value kinds; the factors are the core's (Constants). The number the face
 * prints stays the writer's: the shifted value of 集中 / 威圧 (§3.5 「面の値…を…出し」) is not in CardFace (docs/BATTLE_SCENE.md
 * §8.2), so the mark says what will apply and the prediction tag shows the value.
 * `marks` = { focus, empower, intimidate } (the player's chips). Returns [{ icon, text, word }].
 */
export function modsOf(face, marks) {
  if (!face || !marks) return [];
  const values = face.Values || [];
  if (!values.length) return [];
  const has = (k) => values.some((v) => v.Kind === k);
  const out = [];
  if (marks.focus) out.push({ icon: 'focus', text: '→', word: '集中' }); // scene text (§3.5 「→」)
  if (marks.empower && has(CardValueKind.Power)) out.push({ icon: 'empower', text: '×' + Constants.EmpowerMult, word: '強化' });
  if (marks.intimidate && (has(CardValueKind.Power) || has(CardValueKind.Guard))) out.push({ icon: 'intimidate', text: '−' + Constants.IntimidatePenalty, word: '威圧' });
  return out;
}
/** The marks of `modsOf` as one small column after the values (ink 16 px, each with its status icon at 16). */
function fillMods(OCB, box, mods) {
  box.innerHTML = '';
  box.style.display = mods.length ? '' : 'none';
  for (const m of mods) {
    const tag = h('span', { class: 'ocbg-battle-mod', title: m.word }, h('b', null, m.text));
    try { tag.appendChild(OCB.icon(m.icon, 16, 'var(--ink)')); } catch (err) { /* an icon the DS lacks: the text alone */ }
    box.appendChild(tag);
  }
}

function descriptionPart(OCB, face) {
  // §6.1 スタンスの札: the lasting rows start where the value columns would, and take four lines.
  const lasting = CardTextFit.PrintsLasting(face);
  const boxH = TEXT_LINE * (lasting ? 4 : 3);
  const rows = CardTextFit.DescriptionRows(face);
  const fit = CardTextFit.Description(rows, TEXT_SIZE, MIN_FONT, TEXT_W, boxH, fonts().desc);
  const pitch = fit.Size * TEXT_LINE / TEXT_SIZE;
  const el = h('div', { class: 'ocb-desc ocbg-battle-desc', style: { top: (lasting ? VALUES_AT : TEXT_AT) + 'px', height: boxH + 'px', 'font-size': fit.Size + 'px', 'line-height': pitch + 'px' } });
  if (!fit.Text) return el;
  for (const line of lines(fit.Text)) {
    // The ∞ at the head of a lasting row takes the HP colour (CardView's rich text), printed as the very
    // character the fit measured, so a row is never wider than CardTextFit allowed.
    if (lasting && line.startsWith(CardTextFit.LastingMark)) {
      el.appendChild(h('div', { class: 'ocbg-battle-desc-line' }, h('span', { class: 'ocbg-battle-lasting' }, CardTextFit.LastingMark), line.slice(CardTextFit.LastingMark.length)));
    } else {
      el.appendChild(h('div', { class: 'ocbg-battle-desc-line' }, line));
    }
  }
  return el;
}

/** One trait row, with each term the script marked on it (CardFace.Terms) in a dotted underline (§7.1). */
function traitRow(text, row, face, bold, plain) {
  text = text || '';
  const fit = CardTextFit.Name(text, TEXT_SIZE, TRAIT_MIN_FONT, plain ? TRAIT_TEXT_W + 14 : TRAIT_TEXT_W, 22, bold ? fonts().traitBottom : fonts().traitTop);
  const p = h('p', { style: fit.Size !== TEXT_SIZE ? { 'font-size': fit.Size + 'px' } : null });
  const marks = (face.Terms || []).filter((m) => m.Row === row && m.Start >= 0 && m.Length > 0 && m.Start + m.Length <= text.length)
    .sort((a, b) => a.Start - b.Start);
  let at = 0;
  for (const m of marks) {
    if (m.Start < at) continue;
    if (m.Start > at) p.appendChild(document.createTextNode(text.slice(at, m.Start)));
    const term = h('span', { class: 'ocb-term ocbg-battle-term' }, text.slice(m.Start, m.Start + m.Length));
    term.__mark = m;
    p.appendChild(term);
    at = m.Start + m.Length;
  }
  if (at < text.length) p.appendChild(document.createTextNode(text.slice(at)));
  return p;
}

function traitPart(face) {
  let top = face.TraitTop;
  let bottom = face.TraitBottom;
  // A v4.2 source writes the trait as one line (CardView.BindTrait).
  if (!top && !bottom && face.TraitText) { top = face.TraitText; bottom = ''; }
  if (!top && !bottom) return null;
  const plain = !!face.Plain;
  // §6.1 素直な札: a dashed frame, no lamp, both rows in the note colour (the DS's .is-plain).
  const box = h('div', { class: 'ocb-trait' + (plain ? ' is-plain' : '') });
  if (!plain) box.appendChild(h('div', { class: 'ocb-lamp-slot' }, h('i', { class: 'ocb-lamp' })));
  box.appendChild(h('div', { class: 'ocbg-battle-trait-rows' }, traitRow(top, TermRow.TraitTop, face, false, plain), traitRow(bottom, TermRow.TraitBottom, face, true, plain)));
  return box;
}

/**
 * Builds one card face (CardView.EnsureParts + Bind). Returns `{ el, setLit(lit), setDim(alpha), lit, terms }`;
 * the lamp and the dark sheet change in place, everything else is a new face.
 */
export function buildCard(OCB, face) {
  const el = h('div', { class: 'ocb-card ocbg-battle-face ' + (KIND_CLASS[face.Kind] || 'is-move') });
  el.appendChild(h('div', { class: 'ocb-card-clip' }));
  el.appendChild(costPart(OCB, face));
  const kindIcon = OCB.icon(KIND_ICON[face.Kind] || 'star', 24, KIND_INK[face.Kind] || 'var(--ink)');
  kindIcon.classList.add('ocb-attr-icon');
  el.appendChild(kindIcon);
  const reach = reachPart(OCB, face);
  if (reach) el.appendChild(reach);
  const nameFit = CardTextFit.Name(face.Name || '', NAME_SIZE, MIN_FONT, NAME_BOX.w, NAME_BOX.h, fonts().name);
  el.appendChild(h('div', { class: 'ocb-card-name', style: nameFit.Size !== NAME_SIZE ? { 'font-size': nameFit.Size + 'px' } : null }, nameFit.Text));
  const values = valuesPart(OCB, face);
  if (values) el.appendChild(values);
  // §3.5 / §5.10 (review 21): the status marks stand beside the value (setMods) — right after a lone value column, where the
  // fan leaves it in sight; with two columns at the value row's right end, inside the card's edge.
  const twoValues = values && values.children.length > 1;
  const modsBox = values ? h('div', { class: 'ocbg-battle-mods' + (twoValues ? ' is-end' : ''), style: { display: 'none' } }) : null;
  if (modsBox) (twoValues ? el : values).appendChild(modsBox);
  let modsKey = '';
  el.appendChild(descriptionPart(OCB, face));
  const trait = traitPart(face);
  if (trait) el.appendChild(trait);
  const dim = h('div', { class: 'ocb-card-dim' });
  el.appendChild(dim);
  el.appendChild(h('div', { class: 'ocb-card-edge' }));
  const lamp = el.querySelector('.ocb-lamp');
  const costEl = el.querySelector('.ocb-cost');
  let costShown = face.Cost + '/' + (face.CostDrop || 0);
  // CardView.ShowLamp: a plain card and a card with no trait box never light.
  const hasLamp = !!trait && !face.Plain;
  const card = {
    el,
    lit: false,
    terms: Array.from(el.querySelectorAll('.ocbg-battle-term')),
    /**
     * Several enemies (port notes 「Holding a card」, review fix): while the card frames enemy k the circle reads
     * CardFace.CostFor(k) / CostDropFor(k); at rest the face's own Cost / CostDrop.
     */
    setCost(cost, drop) {
      const key = cost + '/' + (drop || 0);
      if (key === costShown || !costEl) return;
      costShown = key;
      fillCost(costEl, cost, drop || 0);
    },
    setLit(on) {
      card.lit = !!on && hasLamp;
      el.classList.toggle('is-lit', card.lit);
      if (lamp) lamp.classList.toggle('is-on', card.lit);
    },
    setDim(alpha) {
      dim.style.opacity = String(Math.max(0, Math.min(1, alpha)));
    },
    /** The status marks beside the values (modsOf), redrawn only when they change. */
    setMods(mods) {
      if (!modsBox) return;
      const key = (mods || []).map((m) => m.icon).join(',');
      if (key === modsKey) return;
      modsKey = key;
      fillMods(OCB, modsBox, mods || []);
    },
  };
  card.setLit(!!face.TraitLit);
  card.setDim(0);
  return card;
}

/**
 * The hover line's text broken where CardTextFit allows a break (no punctuation at a line start, no number or
 * katakana word cut in two), for its 18 px body text in the line's width (scene choice: Unity's UI Text breaks
 * only at spaces, the browser anywhere, and both split 「（12 に込み）」).
 */
export function wrapLine(text, width, size) {
  if (!text) return '';
  const m = measureWith(400, fontVar('--font-body', 'sans-serif'), 1.375);
  const fits = (t) => m(t, size).width <= width;
  const out = [];
  let rest = text;
  while (rest && !fits(rest)) {
    // Prefer a phrase edge outside brackets: after a space or 、。, or before an opening bracket — the last one whose line fits.
    let at = -1;
    let depth = 0;
    for (let i = 1; i < rest.length; i++) {
      const prev = rest[i - 1];
      if (prev === '（' || prev === '「') depth++;
      else if ((prev === '）' || prev === '」') && depth > 0) depth--;
      const edge = rest[i] === '（' || rest[i] === '「' || (depth === 0 && (prev === ' ' || prev === '、' || prev === '。'));
      if (!edge || !CardTextFit.CanBreakBefore(rest, i)) continue;
      if (fits(rest.slice(0, i).trimEnd())) at = i; else break;
    }
    if (at <= 0) return out.concat(CardTextFit.Wrap([rest], size, width, m)).join('\n');
    out.push(rest.slice(0, at).trimEnd());
    rest = rest.slice(at).trimStart();
  }
  if (rest) out.push(rest);
  return out.join('\n');
}

/** A key that changes whenever anything the face prints changes (CardView.Bind is called again). */
export function faceKey(f) {
  if (!f) return '';
  const vals = (f.Values || []).map((v) => v.Kind + ':' + v.Number + ':' + v.Word).join(',');
  const terms = (f.Terms || []).map((t) => t.Row + ':' + t.Start + ':' + t.Length).join(',');
  return [f.Id, f.Name, f.Cost, f.PrintedCost, f.Kind, f.Aim, f.RequiredRangeGlyph, f.ValueText, vals, (f.TextLines || []).join('|'), f.Description,
    f.Lasting, f.Plain, f.TraitTop, f.TraitBottom, f.TraitText, terms].join('/');
}

/**
 * CardPanels.ShowDetail (§7.4): 560×280, the card at 80% on the left (DS .ocb-detail), name 24, kind
 * line 16, the full text, and the terms in a black 35% box along the bottom.
 */
export function buildDetail(OCB, face, extra) {
  const d = face.Detail;
  const mini = buildCard(OCB, face);
  if (extra && extra.mods) mini.setMods(extra.mods);
  const terms = (d.Terms || []).map((t) => h('div', null, h('b', null, t.Word), '：' + t.Meaning)); // scene text (CardPanels: Word + "：" + Meaning)
  return h('div', { class: 'ocb-detail ocbg-battle-detail' },
    h('div', { class: 'ocb-detail-card' }, mini.el),
    h('div', { class: 'ocb-detail-body' },
      h('h3', null, d.Name || face.Name || ''),
      h('div', { class: 'ocb-meta' }, d.KindLine || ''),
      h('div', { class: 'ocb-full ocbg-battle-detail-text' }, d.Body || ''),
      nowPart(face, extra),
      terms.length ? h('div', { class: 'ocb-detail-terms ocbg-battle-detail-terms' }, terms) : null));
}

/**
 * battle_ui_ux_v2 §3.13 (review 21: the detail held the card text and the terms only): what the card does now — 離す先
 * (対象の枠 / 投げ上げ線, from CardFace.Aim) and, for a card with a trait, 今の判定 (its lamp) with 予測値の内訳 (the source's
 * prediction against the face's printed value, and the status marks that apply). scene text. 習熟の段と伸びる先, 探索の面の印
 * and 入手先 are not in the script (CardFace carries none of them; docs/BATTLE_SCENE.md §8.2).
 * `extra` = { lit, preview, mods }.
 */
const ZONE_WORDS = { [CardAim.Single]: '対象の枠（敵の上で離す）', [CardAim.Self]: '投げ上げ線（線より上で離す）' };
function nowPart(face, extra) {
  const rows = [];
  const zone = ZONE_WORDS[face.Aim];
  if (zone) rows.push(['離す先', zone]);
  const hasTrait = !face.Plain && !!(face.TraitTop || face.TraitText);
  if (hasTrait && extra) rows.push(['今の判定', extra.lit ? '条件が成り立っています（ランプ点灯）' : '条件は成り立っていません']);
  const printed = (face.Values || [])[0];
  if (extra && extra.preview) {
    const parts = [extra.preview];
    if (printed && String(printed.Number) !== String(extra.preview)) parts.push('（表の値 ' + printed.Number + (extra.mods && extra.mods.length ? '・' + extra.mods.map((m) => m.word + ' ' + m.text).join('・') : '') + (extra.lit ? '・特性' : '') + '）');
    rows.push(['予測値', parts.join('')]);
  }
  if (!rows.length) return null;
  return h('div', { class: 'ocbg-battle-detail-now' }, rows.map(([head, text]) => h('p', null, h('span', null, head), text)));
}
