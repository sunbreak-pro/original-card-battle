// A card face as the design system draws it (OCB.Card, 216×304), from the face the bridge writes
// (CoreText.FaceOf → CardFace, filled by CardFaceText). Like Unity's CardView.Bind, it only maps
// fields: the kind to the frame colour, each value column to its icon, the rows, the trait box.
// Nothing here judges a card.
import { h } from '../common/stage.js';
import { CoreText } from '../../bridge/depiction/CoreText.js';
import { ocb } from './util.js';

/** CardKind (DepictionTypes) → the design system's attribute key. */
const ATTR_OF = { Attack: 'attack', Guard: 'defense', Skill: 'skill', Stance: 'stance', Move: 'skill' };

/** CardValueKind → icon (battle-visual-v1 付録 A): 詰める / 押す point forward, 離れる / 引く back. */
const VALUE_ICON = { Power: 'sword', Guard: 'shield', Heal: 'heal', Close: 'forward', Back: 'back', Push: 'forward', Pull: 'back', Draw: 'draw' };

/** The face of a catalog card (CardDef) as the deck screen shows it. */
export function faceOf(def) {
  return CoreText.FaceOf(def);
}

/**
 * The rows the description prints, as Unity's CardTextFit.DescriptionRows picks them: the face's
 * TextLines when it has any (a stance's lasting rows among them), else one row per sentence of its
 * Description.
 */
export function descriptionRows(face) {
  if (face.TextLines && face.TextLines.length > 0) return face.TextLines.slice();
  return String(face.Description || '').replace(/。/g, '。\n').split('\n').filter((r) => r.trim().length > 0);
}

/** CardFace → the def object OCB.Card reads. */
export function ocbDef(face) {
  const attr = ATTR_OF[face.Kind] || 'skill';
  const reach = face.RequiredRangeGlyph ? [face.RequiredRangeGlyph, face.RequiredRangeGlyph] : 'self';
  return {
    name: face.Name,
    attr,
    cost: face.PrintedCost || face.Cost,
    reach,
    values: (face.Values || []).map((v) => ({ icon: VALUE_ICON[v.Kind] || 'plus', n: v.Number, word: v.Word })),
    desc: face.Lasting ? '' : descriptionRows(face).join('\n'),
    lines: face.Lasting ? descriptionRows(face) : [],
    trait: face.Plain ? null : { cond: face.TraitTop, eff: face.TraitBottom },
  };
}

/**
 * One card element at full size (216×304). opts: { lit, unpayable }. The plain card keeps the
 * bridge's own two rows (CardFaceText.PlainTop / PlainBottom) in the dashed box.
 */
export function cardEl(face, opts = {}) {
  const O = ocb();
  const el = O.Card(ocbDef(face), { cost: face.Cost, lamp: !!opts.lit, unpayable: !!opts.unpayable });
  // Undecided look 5, recommendation B (ds undecided.md): rows that would run past the 188 px box at
  // 16 px wrap at 14 px instead of spilling out of the card.
  const rows = descriptionRows(face);
  if (rows.length > 3 || rows.some((r) => r.length > 11)) el.querySelector('.ocb-desc')?.classList.add('is-small');
  const box = el.querySelectorAll('.ocb-trait p');
  if (face.Plain) {
    if (box[0]) box[0].textContent = face.TraitTop;
    if (box[1]) box[1].textContent = face.TraitBottom;
  } else {
    // battle-visual-v1 §7.1: the glossary words the trait box prints get a dotted underline and their
    // meaning on hover (CardFace.Terms: row, start, length, word, meaning — all the bridge's).
    underline(box[0], face.TraitTop, (face.Terms || []).filter((m) => m.Row === 'TraitTop'));
    underline(box[1], face.TraitBottom, (face.Terms || []).filter((m) => m.Row === 'TraitBottom'));
  }
  el.dataset.card = face.Id;
  return el;
}

function underline(p, text, marks) {
  if (!p || !marks.length || p.textContent !== text) return;
  p.textContent = '';
  let at = 0;
  for (const m of marks.slice().sort((a, b) => a.Start - b.Start)) {
    if (m.Start < at) continue;
    if (m.Start > at) p.appendChild(document.createTextNode(text.slice(at, m.Start)));
    p.appendChild(h('span', { class: 'ocb-term', title: `${m.Word}：${m.Meaning}` }, text.slice(m.Start, m.Start + m.Length)));
    at = m.Start + m.Length;
  }
  if (at < text.length) p.appendChild(document.createTextNode(text.slice(at)));
}

/** A card scaled into a holder of its scaled size (the deck grid uses 0.72, like DeckSelectScreen). */
export function scaledCard(face, scale, opts = {}) {
  const holder = h('div', { class: 'ocbg-meta-cardslot', style: { width: `${Math.round(216 * scale)}px`, height: `${Math.round(304 * scale)}px` } });
  const el = cardEl(face, opts);
  el.style.transform = `scale(${scale})`;
  el.style.transformOrigin = '0 0';
  el.style.position = 'absolute';
  el.style.left = '0';
  el.style.top = '0';
  holder.appendChild(el);
  return holder;
}

/** The right-click detail's terms (CardDetail.Terms) as 「語：意味」 rows. */
export function termRows(face) {
  const terms = (face.Detail && face.Detail.Terms) || [];
  return terms.map((t) => h('div', { class: 'ocbg-meta-term' }, h('b', null, t.Word), h('span', null, t.Meaning)));
}
