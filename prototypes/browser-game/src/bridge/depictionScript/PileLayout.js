// Port of unity-project-kit/Assets/View/Depiction/Script/PileLayout.cs
// Where the three piles stand and how thick each looks (battle-visual-v1 §4.7, #455). Pure C#, so
// `dotnet test` holds the same numbers the screen draws.
//
// Port notes:
// - This module imports nothing from DepictionTypes (DepictionEvent's constructor reads
//   PileKind.Discard, so the two would form a cycle); CountOf reads any PileFrame it is handed.
// - C# `float` constants are float32 values (Math.fround), and each float operation is rounded.
// - `(int)pile` is the enum's declaration index (enumIndex).
import { ArgumentOutOfRangeException, defineEnum, enumIndex, idiv } from '../../shared/cs.js';

const f = Math.fround;

/** new ArgumentOutOfRangeException(paramName, actualValue, null) with .NET's message. */
function argRangeValue(name, value) {
  const e = new ArgumentOutOfRangeException(
    `Specified argument was out of the range of valid values. (Parameter '${name}')\nActual value was ${value}.`,
  );
  e.ParamName = name;
  e.ActualValue = value;
  return e;
}

/** The three piles, left to right. */
export const PileKind = defineEnum(['Draw', 'Discard', 'Exile']);

/**
 * `(int)pile`: the declaration index of a PileKind name. C# can cast any int to the enum, so a
 * number (or the number string .NET writes for an undefined value, "5") is taken as that int.
 */
function pileIndex(pile) {
  if (typeof pile === 'number') return pile;
  const i = PileKind.$order.indexOf(pile);
  if (i >= 0) return i;
  if (typeof pile === 'string' && /^-?\d+$/.test(pile)) return Number(pile);
  return enumIndex(PileKind, pile);
}

export const PileLayout = {
  /** §4.7 束 3 つ: from x 32, y 948, each 64×84, 22 px apart. */
  Left: f(32),
  Top: f(948),
  Width: f(64),
  Height: f(84),
  Gap: f(22),

  /** §4.7: the pile's icon is 28 px. */
  IconSize: f(28),

  /** §4.7: each sheet behind the front one shows 4 px more of the pile's thickness. */
  SheetStep: f(4),

  /**
   * At most this many sheets behind the front one (12 px). The stamina tag above ends at y 930,
   * 18 px over the piles, so a thicker stack would reach it.
   */
  MaxSheetsBehind: 3,

  /** One more sheet behind the front one for every this many cards past the first. */
  CardsPerSheet: 5,

  /** The left edge of `pile` on the 1920×1080 screen. */
  LeftOf(pile) {
    // (Width + Gap) is a C# constant (86).
    return f(PileLayout.Left + f(f(pileIndex(pile)) * f(PileLayout.Width + PileLayout.Gap)));
  },

  /** The three together span the stamina tag's 236 px, as §4.7 lines them up under it. */
  get SpanWidth() {
    return f(f(3 * PileLayout.Width) + f(2 * PileLayout.Gap));
  },

  /**
   * How many sheets lie behind the front card of a pile of `count` cards: none for 0 or 1, then one
   * more for every 5 cards (2〜6 → 1, 7〜11 → 2, 12 and more → 3).
   * §4.7 says the thickness follows the count 4 px at a time without saying how many cards a
   * step holds; this is the battle lane's reading.
   */
  SheetsBehind(count) {
    if (count <= 1) return 0;
    return Math.min(PileLayout.MaxSheetsBehind, 1 + idiv(count - 2, PileLayout.CardsPerSheet));
  },

  /** A pile with no card is drawn as an empty slot: a steel dashed frame, no sheet (§3.2 破線 for 空き). */
  IsEmpty(count) {
    return count <= 0;
  },

  /** The number of cards in `pile` on `piles`. */
  CountOf(piles, pile) {
    if (piles == null) return 0;
    switch (pile) {
      case PileKind.Draw: return piles.Draw;
      case PileKind.Discard: return piles.Discard;
      case PileKind.Exile: return piles.Exile;
      default: throw argRangeValue('pile', pile);
    }
  },
};
