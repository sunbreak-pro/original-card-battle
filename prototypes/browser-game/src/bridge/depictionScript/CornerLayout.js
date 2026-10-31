// Port of unity-project-kit/Assets/View/Depiction/Script/CornerLayout.cs
// Where the small buttons in the top corners of the battle screen sit (battle-visual-v1 §4.1, #389).
// Pure arithmetic with no engine types, like HandFan, so the EditMode tests can hold it directly:
// the View lives in Assembly-CSharp, which a test asmdef cannot reference. Every box is in pixels on
// the 1920 x 1080 reference canvas, measured from the top-left corner with y growing downwards, the
// way battle-visual-v1 §4 writes its coordinates. The View turns a box into an anchoredPosition.
//
// Port notes:
// - ScreenBox is a readonly struct: a frozen object whose four members are float32 values
//   (Math.fround), with Right / Bottom / FromRight as getters (each a rounded float sum).
// - ScreenBox.ToString() writes each float as .NET's float.ToString() does (shortest round-trip,
//   "E+XX" past 9 integer digits or below 1E-04); toString() is the same.
import { ArgumentOutOfRangeException } from '../../shared/cs.js';

const f = Math.fround;

/** new ArgumentOutOfRangeException(paramName, message) with .NET's message. */
function argRangeMessage(name, message) {
  const e = new ArgumentOutOfRangeException(`${message} (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/**
 * .NET's float.ToString() (invariant culture, .NET Core 3.0+): the shortest digits that read back as
 * the same float, in fixed notation unless the decimal exponent is past 9 digits or below 1E-04.
 */
function floatToString(v) {
  v = f(v);
  if (Number.isNaN(v)) return 'NaN';
  if (v === Infinity) return 'Infinity';
  if (v === -Infinity) return '-Infinity';
  if (v === 0) return Object.is(v, -0) ? '-0' : '0';
  const sign = v < 0 ? '-' : '';
  const a = Math.abs(v);
  let p = 1;
  while (p < 9 && f(Number(a.toPrecision(p))) !== a) p++;
  const exp = Number(a.toPrecision(p)).toExponential(p - 1); // "d.ddde+x"
  const [mant, e] = exp.split('e');
  const digits = mant.replace('.', '').replace(/0+$/, '') || '0';
  const scale = Number(e) + 1; // the number of integer digits
  const maxDigits = Math.max(digits.length, 9);
  if (scale > maxDigits || scale < -3) {
    const x = scale - 1;
    const ex = (x < 0 ? '-' : '+') + String(Math.abs(x)).padStart(2, '0');
    return sign + digits[0] + (digits.length > 1 ? '.' + digits.slice(1) : '') + 'E' + ex;
  }
  if (scale <= 0) return sign + '0.' + '0'.repeat(-scale) + digits;
  if (scale >= digits.length) return sign + digits + '0'.repeat(scale - digits.length);
  return sign + digits.slice(0, scale) + '.' + digits.slice(scale);
}

/** A box on the 1920 x 1080 canvas: left and top edges, width and height, y counted downwards. */
export class ScreenBox {
  constructor(x, y, width, height) {
    if (f(width) < 0 || f(height) < 0) throw argRangeMessage('width', 'A box has no negative size.');
    this.X = f(x);
    this.Y = f(y);
    this.Width = f(width);
    this.Height = f(height);
    Object.freeze(this);
  }

  get Right() {
    return f(this.X + this.Width);
  }

  get Bottom() {
    return f(this.Y + this.Height);
  }

  /** True when the two boxes share some area. Boxes that only touch along an edge do not overlap. */
  Overlaps(other) {
    return this.X < other.Right && other.X < this.Right && this.Y < other.Bottom && other.Y < this.Bottom;
  }

  /** The offset of the box's top-right corner from the canvas's top-right corner (anchor and pivot (1, 1)). */
  get FromRight() {
    return f(CornerLayout.CanvasWidth - this.Right);
  }

  ToString() {
    return '(' + floatToString(this.X) + ', ' + floatToString(this.Y) + ', ' + floatToString(this.Width) + ' x ' + floatToString(this.Height) + ')';
  }

  toString() {
    return this.ToString();
  }
}

const CanvasWidth = f(1920);
const CornerInfoTop = f(20);
const CornerInfoHeight = f(44);
const JournalButton = new ScreenBox(f(f(CanvasWidth - 24) - 56), 20, 56, 56);
const JournalGap = f(12);
const SpeedButton = new ScreenBox(f(f(JournalButton.X - JournalGap) - 120), 20, 120, 56);

/** The top corners of the battle screen (battle-visual-v1 §4.1). */
export const CornerLayout = {
  CanvasWidth,
  CanvasHeight: f(1080),

  /** The corner info's top and height (x 24, y 20, height 44). Its width follows its contents. */
  CornerInfoTop,
  CornerInfoHeight,
  CornerInfoBottom: f(CornerInfoTop + CornerInfoHeight),

  /** The journal button: 24 from the right, y 20, 56 x 56. The View does not build it yet. */
  JournalButton,

  /** The space between the journal button and the speed switch. */
  JournalGap,

  /** The battle-speed switch: 92 from the right (12 left of the journal button), y 20, 120 x 56. */
  SpeedButton,

  /** The switch's side padding, the icon's size and the space between the icon and the number. */
  SpeedPadding: f(12),
  SpeedIconSize: f(24),
  SpeedIconGap: f(8),

  /** The width left for the number ("×1.25") between the icon and the right padding: 64 px. */
  get SpeedNumberWidth() {
    const c = CornerLayout;
    return f(f(f(f(c.SpeedButton.Width - c.SpeedPadding) - c.SpeedIconSize) - c.SpeedIconGap) - c.SpeedPadding);
  },

  /** How far under the switch its hover tooltip starts. */
  SpeedTooltipGap: f(8),

  /** The tooltip's padding: 8 above and below, 12 left and right. */
  SpeedTooltipPadY: f(8),
  SpeedTooltipPadX: f(12),

  /**
   * The hover tooltip of the switch for a line `textWidth` x `textHeight` px: 8 under the switch,
   * its right edge on the switch's right edge, padded 8 / 12.
   */
  SpeedTooltip(textWidth, textHeight) {
    const c = CornerLayout;
    const width = f(f(textWidth) + f(2 * c.SpeedTooltipPadX));
    const height = f(f(textHeight) + f(2 * c.SpeedTooltipPadY));
    return new ScreenBox(f(c.SpeedButton.Right - width), f(c.SpeedButton.Bottom + c.SpeedTooltipGap), width, height);
  },

  /**
   * The demo's 「降参する」 (#203): top left, 12 under the corner info, 200 x 56. The top right
   * belongs to the journal button and the speed switch, and the switch's tooltip opens
   * leftwards under them; the demo's canvas draws above the battle's, so a button there would
   * hide the tooltip. Far from the switch, a run of presses on the speed cannot end the battle.
   */
  DemoSurrender: new ScreenBox(24, f(f(CornerInfoTop + CornerInfoHeight) + 12), 200, 56),
};
