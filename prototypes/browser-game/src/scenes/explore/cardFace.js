// A card as the design system's OCB.Card draws it, from the face the ported bridge writes
// (CoreText.FaceOf → CardFaceText). Only the shape changes here: every word and number is the face's.
import { CoreText } from '../../bridge/depiction/CoreText.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';

const ATTR = { Attack: 'attack', Guard: 'defense', Skill: 'skill', Stance: 'stance', Move: 'skill' };
const VALUE_ICON = { Power: 'sword', Guard: 'shield', Heal: 'heal', Close: 'forward', Back: 'back', Push: 'forward', Pull: 'back', Draw: 'draw' };

/** The OCB.Card definition of a CardDef, through the bridge's face. */
export function cardDefOf(def) {
  const face = CoreText.FaceOf(def);
  const attr = ATTR[face.Kind] || 'skill';
  // CoreText.ReachText is empty for a card aimed at the one playing it: the card prints 自分.
  const reach = face.RequiredRangeGlyph ? [face.RequiredRangeGlyph, face.RequiredRangeGlyph] : 'self';
  const values = (face.Values || []).map((v) => ({ icon: VALUE_ICON[v.Kind] || 'plus', n: v.Number, word: v.Word }));
  const lines = face.TextLines || [];
  return {
    name: face.Name,
    attr,
    cost: face.PrintedCost != null ? face.PrintedCost : face.Cost,
    reach,
    values,
    desc: face.Lasting ? '' : lines.join(''),
    lines: face.Lasting ? lines : [],
    trait: face.Plain || (!face.TraitTop && !face.TraitBottom) ? null : { cond: face.TraitTop, eff: face.TraitBottom },
    hover: face.HoverLine || '',
  };
}

/** The OCB.Card definition of a card id; `book` (a MasteryBook) grows the card to this life's step. */
export function cardDefById(id, book) {
  let def = CardCatalog.ById(id);
  if (book && typeof book.Mastered === 'function') def = book.Mastered(def);
  return cardDefOf(def);
}

/** The card's name as this life knows it. */
export function cardName(id, book) {
  try {
    let def = CardCatalog.ById(id);
    if (book && typeof book.Mastered === 'function') def = book.Mastered(def);
    return def.Name;
  } catch {
    return id;
  }
}
