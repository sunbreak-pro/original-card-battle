// Port of unity-project-kit/Assets/View/Depiction/Script/HandFan.cs
// Where each card of the hand rests. Pure arithmetic with no engine types, so the EditMode
// tests can hold it directly: the View lives in Assembly-CSharp, which a test asmdef cannot
// reference. This is layout, not a rule of the game; the numbers come from the View's inspector.
//
// Port notes:
// - Every C# `float` is a single: float parameters are rounded on entry, constants are float32 values
//   and every float operation is rounded with Math.fround; the angle math is double, as in the C#
//   (`degrees * Math.PI / 180.0`), and its Sin / Cos results are cast to float. V8's Math.sin / cos may
//   differ from .NET's in the last bit of the double; the cast to float hides it in all but the rarest case.
// - FanPlace is a readonly struct: a frozen object with X, Y and Degrees.
const f = Math.fround;

/** A resting place in the hand area: offset from its centre in pixels, and tilt in degrees. */
export class FanPlace {
  constructor(x, y, degrees) {
    this.X = f(x);
    this.Y = f(y);
    /** Counter-clockwise tilt. Cards left of the middle lean left (positive). */
    this.Degrees = f(degrees);
    Object.freeze(this);
  }
}

export const HandFan = {
  /** The largest hand the spacing still closes in for (HAND_LIMIT, battle_core_v4 §8). */
  MostCards: 8,

  /**
   * battle-visual-v1.md §4.8: neighbouring cards sit 160 px apart for five cards or fewer, then
   * 150 / 140 / 126 for six / seven / eight, so eight still fit the 1100 px hand band.
   * `upToFive` is the inspector's spacing for five or fewer; six to eight scale it by the same
   * shares, so 160 gives the table exactly. Past eight the spacing stays at eight's.
   */
  Spacing(count, upToFive) {
    upToFive = f(upToFive);
    if (count <= 5) return upToFive;
    // (150f / 160f) and the others are C# constants, folded in float.
    if (count === 6) return f(upToFive * f(150 / 160));
    if (count === 7) return f(upToFive * f(140 / 160));
    return f(upToFive * f(126 / 160));
  },

  /**
   * Resting place of card `index` of `count` in a shallow fan.
   * The centre of the middle card rises above the outermost cards' by `dropPixels` times the step
   * squared. A tilted card's lower corner dips by half its width times sin(tilt); the whole hand is
   * raised by the outermost card's dip, so that corner stays on or just above the hand's base line
   * (the screen edge is just below it; the lift left over is half the height times 1 - cos(tilt),
   * under 2 px for shallow fans). The raise is the same for every card: raising each card by its own
   * dip would lift the cards beside the middle above it once the tilt outgrows the drop. Assumes the
   * outermost tilt stays within 90 degrees.
   */
  Place(count, index, spacing, degreesPerCard, dropPixels, cardWidth) {
    spacing = f(spacing);
    degreesPerCard = f(degreesPerCard);
    dropPixels = f(dropPixels);
    cardWidth = f(cardWidth);
    const edge = f(f(count - 1) * 0.5);
    const step = f(f(index) - edge);
    const degrees = f(-step * degreesPerCard);
    const edgeCornerDip = f(f(f(Math.abs(Math.sin(f(edge * degreesPerCard) * Math.PI / 180.0))) * cardWidth) * 0.5);
    return new FanPlace(
      f(step * spacing),
      f(f(f(f(edge * edge) - f(step * step)) * dropPixels) + edgeCornerDip),
      degrees,
    );
  },
};

/**
 * battle-visual-v1 §4.8 (#242): where the hand's cards rest and rise on the 1920×1080 screen.
 * Coordinates are screen pixels, x from the left and y from the top, as the document gives them.
 */
export const HandSpec = {
  CardWidth: f(216),
  CardHeight: f(304),
  CentreX: f(960),

  /** The middle card's top: only its upper 184 px show above the screen's bottom. */
  MiddleTop: f(896),

  /** The k-th card from the middle sits k² times this much lower. */
  DropPerStep: f(3.2),

  /** A hovered card's top: its bottom then sits 16 px above the screen's bottom. */
  HoverTop: f(760),

  /**
   * The resting place of card `index` of `count`: its centre on the screen (X, Y from the top) and
   * its tilt (counter-clockwise, cards left of the middle lean left). The tilt turns the card about
   * the middle of its bottom edge, as §4.8 says.
   */
  Rest(count, index, spacing, degreesPerCard) {
    spacing = f(spacing);
    degreesPerCard = f(degreesPerCard);
    const step = f(f(index) - f(f(count - 1) * 0.5));
    const x = f(HandSpec.CentreX + f(step * spacing));
    const top = f(HandSpec.MiddleTop + f(f(step * step) * HandSpec.DropPerStep));
    const degrees = f(-step * degreesPerCard);
    const radians = degrees * Math.PI / 180.0;
    // CardHeight * 0.5f is a C# constant (152).
    const half = f(HandSpec.CardHeight * 0.5);
    return new FanPlace(
      f(x - f(half * f(Math.sin(radians)))),
      f(f(top + HandSpec.CardHeight) - f(half * f(Math.cos(radians)))),
      degrees,
    );
  },

  /** A hovered card's centre: upright, above its own place in the fan, top at HoverTop. */
  Hover(count, index, spacing) {
    spacing = f(spacing);
    const step = f(f(index) - f(f(count - 1) * 0.5));
    // HoverTop + CardHeight * 0.5f is a C# constant (912).
    return new FanPlace(f(HandSpec.CentreX + f(step * spacing)), f(HandSpec.HoverTop + f(HandSpec.CardHeight * 0.5)), 0);
  },

  /** The width the hand spans at rest, ignoring the tilt: (count − 1) × spacing + one card. */
  Width(count, spacing) {
    spacing = f(spacing);
    return count <= 0 ? f(0) : f(f(f(count - 1) * spacing) + HandSpec.CardWidth);
  },
};
