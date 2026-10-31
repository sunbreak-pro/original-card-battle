// Port of unity-project-kit/Assets/View/Depiction/Script/CardTextFit.cs
// How a card's name and trait line fit their boxes (#210). Pure arithmetic over a measure the View
// hands in (Unity's Text.preferredWidth / preferredHeight with the prefab's font), so the EditMode
// tests can hold the choice with a stand-in measure. The boxes and the largest size are the prefab's.
// The description rows fit the same way, wrapping only once shrinking alone runs out (#567).
//
// Port notes:
// - `TextMeasure` is C#'s `delegate void TextMeasure(string text, int size, out float width, out float height)`.
//   In JS the measure is `(text, size) => ({ width, height })` (the two out parameters as an object);
//   `[width, height]` and `{ Width, Height }` are read too. The measured values are C# floats, so they
//   are rounded to float32 (Math.fround) as the out parameters would be, and so are the box's width and height.
// - `TextFit` is a readonly struct: a frozen object with Text, Size and Fits.
// - C#'s string.Trim / TrimStart / TrimEnd (char.IsWhiteSpace) are reproduced exactly, not JS's trim().
// - String concatenation of a null row writes "" (C#), not "null".
const f = Math.fround;

/** C# char.IsWhiteSpace for one UTF-16 code unit. */
function isCsWhiteSpace(c) {
  const u = c.charCodeAt(0);
  return (u >= 0x09 && u <= 0x0d) || u === 0x20 || u === 0x85 || u === 0xa0 || u === 0x1680
    || (u >= 0x2000 && u <= 0x200a) || u === 0x2028 || u === 0x2029 || u === 0x202f || u === 0x205f || u === 0x3000;
}
function csTrimStart(s) {
  let a = 0;
  while (a < s.length && isCsWhiteSpace(s[a])) a++;
  return s.substring(a);
}
function csTrimEnd(s) {
  let b = s.length;
  while (b > 0 && isCsWhiteSpace(s[b - 1])) b--;
  return s.substring(0, b);
}
function csTrim(s) {
  return csTrimEnd(csTrimStart(s));
}

/** Calls a TextMeasure: the printed size of `text` at font size `size`, one line per "\n", as float32 { width, height }. */
function measureOf(measure, text, size) {
  const r = measure(text, size);
  let w;
  let h;
  if (Array.isArray(r)) {
    w = r[0];
    h = r[1];
  } else {
    w = r.width !== undefined ? r.width : r.Width;
    h = r.height !== undefined ? r.height : r.Height;
  }
  return { width: f(w), height: f(h) };
}

/**
 * The printed size of `text` at font size `size`, one line per "\n" (C# delegate TextMeasure with two
 * out floats). JS: `(text, size) => ({ width, height })`. Nothing checks it at run time.
 * @typedef {(text: string, size: number) => ({ width: number, height: number } | [number, number])} TextMeasure
 */

/** What to print and how large. Text carries "\n" where the line breaks. */
export class TextFit {
  constructor(text, size, fits) {
    this.Text = text;
    this.Size = size;
    /** False when even the smallest size overflows the box; the View then wraps and clips as a last guard. */
    this.Fits = fits;
    Object.freeze(this);
  }
}

function IsKatakana(c) {
  const u = c.charCodeAt(0);
  return u >= 0x30a1 && u <= 0x30fa; // 'ァ' .. 'ヺ'
}

// "+12", "×2", "Guard": a run of half-width letters, digits and signs stays on one line.
function IsWordChar(c) {
  const u = c.charCodeAt(0);
  const asciiLetterOrDigit = (u >= 0x30 && u <= 0x39) || (u >= 0x41 && u <= 0x5a) || (u >= 0x61 && u <= 0x7a);
  return (u < 0x80 && asciiLetterOrDigit) || c === '+' || c === '-' || c === '−' || c === '×' || c === '＋' || c === '－';
}

function WidthOf(text, size, measure) {
  return measureOf(measure, text, size).width;
}

function Largest(text, maxSize, minSize, width, height, measure) {
  minSize = Math.min(minSize, maxSize);
  width = f(width);
  height = f(height);
  for (let size = maxSize; size >= minSize; size--) {
    const m = measureOf(measure, text, size);
    if (m.width <= width && m.height <= height) return new TextFit(text, size, true);
  }
  return new TextFit(text, minSize, false);
}

export const CardTextFit = {
  /** CoreText.TraitLines joins two traits with this; one trait has one space, between its condition and its effect. */
  TraitJoin: ' ／ ',

  /**
   * The name on one line, at the largest size from `maxSize` down to `minSize` that stays inside the
   * box. A name that wraps lands on the type line.
   */
  Name(name, maxSize, minSize, width, height, measure) {
    return Largest(name, maxSize, minSize, width, height, measure);
  },

  /**
   * The trait line on one line, or on two when two lines print larger: two traits one per line
   * (背水の陣), one trait broken between its condition and its effect. A tie keeps one line.
   */
  Trait(trait, maxSize, minSize, width, height, measure) {
    const one = Largest(trait, maxSize, minSize, width, height, measure);
    const broken = CardTextFit.TwoLines(trait);
    if (broken == null || (one.Fits && one.Size >= maxSize)) return one;
    const two = Largest(broken, maxSize, minSize, width, height, measure);
    const twoIsBetter = two.Fits !== one.Fits ? two.Fits : two.Size > one.Size;
    return twoIsBetter ? two : one;
  },

  /** The trait line broken in two, or null when it has nowhere to break. */
  TwoLines(trait) {
    if (trait == null || trait.length === 0) return null;
    const join = trait.indexOf(CardTextFit.TraitJoin);
    if (join >= 0) return trait.substring(0, join) + '\n' + trait.substring(join + CardTextFit.TraitJoin.length);
    const space = trait.indexOf(' ');
    return space >= 0 ? trait.substring(0, space) + '\n' + trait.substring(space + 1) : null;
  },

  // ---- the description (#567) ---------------------------------------------------------------

  /** Characters that never open a line (行頭禁則): closing marks, punctuation, small kana, the long vowel. */
  NoLineStart: '、。，．,.：；:;！？!?）)」』】〕］]｝}〉》・ー～…％%ぁぃぅぇぉっゃゅょァィゥェォッャュョ々',

  /** Characters that never close a line (行末禁則): opening marks. */
  NoLineEnd: '（(「『【〔［[｛{〈《',

  /** The mark at the head of a lasting row (§6.1 スタンスの札); the View colours it. */
  LastingMark: '∞',

  /** Whether the face prints lasting rows, which start higher and take four lines. */
  PrintsLasting(face) {
    return face.Lasting && face.TextLines != null && face.TextLines.length > 0;
  },

  /**
   * The rows the description prints before fitting: CardFace.TextLines (each lasting row behind
   * LastingMark), or for a v4.2 face one row per sentence of its description.
   */
  DescriptionRows(face) {
    const rows = [];
    if (face.TextLines != null && face.TextLines.length > 0) {
      const lasting = CardTextFit.PrintsLasting(face);
      for (const row of face.TextLines) rows.push(lasting ? CardTextFit.LastingMark + ' ' + (row == null ? '' : row) : row);
      return rows;
    }
    for (const sentence of (face.Description == null ? '' : face.Description).split('。').join('。\n').split('\n')) {
      if (csTrim(sentence).length > 0) rows.push(sentence);
    }
    return rows;
  },

  /**
   * The description rows inside the box, one row per line where they fit. First the largest size
   * from `maxSize` down to `minSize` that holds every row on one line; when even `minSize` overflows,
   * the rows are wrapped (no punctuation at a line start, no number or katakana word cut in two) at
   * the largest size whose lines stay inside the box; when that also overflows, the lines past the
   * box's foot are dropped and TextFit.Fits is false.
   */
  Description(rows, maxSize, minSize, width, height, measure) {
    minSize = Math.min(minSize, maxSize);
    if (rows == null || rows.length === 0) return new TextFit('', maxSize, true);
    height = f(height);
    const shrunk = Largest(rows.join('\n'), maxSize, minSize, width, height, measure);
    if (shrunk.Fits) return shrunk;
    for (let size = maxSize; size >= minSize; size--) {
      const wrapped = CardTextFit.Wrap(rows, size, width, measure).join('\n');
      const h = measureOf(measure, wrapped, size).height;
      if (h <= height) return new TextFit(wrapped, size, true);
    }
    const lines = CardTextFit.Wrap(rows, minSize, width, measure);
    while (lines.length > 1) {
      const h = measureOf(measure, lines.join('\n'), minSize).height;
      if (h <= height) break;
      lines.splice(lines.length - 1, 1);
    }
    return new TextFit(lines.join('\n'), minSize, false);
  },

  /** Each row broken into lines no wider than `width` at `size`. */
  Wrap(rows, size, width, measure) {
    width = f(width);
    const lines = [];
    for (const row of rows) {
      let rest = csTrim(row == null ? '' : row);
      while (true) {
        if (rest.length <= 1 || WidthOf(rest, size, measure) <= width) {
          lines.push(rest);
          break;
        }
        let cut = 1;
        while (cut < rest.length && WidthOf(rest.substring(0, cut + 1), size, measure) <= width) cut++;
        let at = cut;
        while (at > 0 && !CardTextFit.CanBreakBefore(rest, at)) at--;
        if (at === 0) at = cut; // nowhere allowed: cut where the width runs out
        lines.push(csTrimEnd(rest.substring(0, at)));
        rest = csTrimStart(rest.substring(at));
      }
    }
    return lines;
  },

  /** Whether a line may break between text[at - 1] and text[at]. */
  CanBreakBefore(text, at) {
    if (at <= 0 || at >= text.length) return false;
    const before = text[at - 1];
    const after = text[at];
    if (CardTextFit.NoLineStart.indexOf(after) >= 0 || CardTextFit.NoLineEnd.indexOf(before) >= 0) return false;
    if (IsKatakana(before) && IsKatakana(after)) return false; // アタック, スタミナ stay whole
    return !(IsWordChar(before) && IsWordChar(after));
  },
};
