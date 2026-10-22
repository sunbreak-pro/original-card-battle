// Port of unity-port/unity-project-kit/Assets/View/Backdrop/Script/BackdropSpec.cs
//
// The battle backdrop of each layer (battle-visual-v1 §2.4, #89): the sky gradient, the haze laid over
// it, the light that breathes in the haze, the motes that drift through it, and the floor tile drawn
// on top. Pure C#, so `dotnet test` holds the values the screen draws.
//
// Only the backdrop changes with the layer; the UI keeps its colours (world-v1 §6). Layers 4 and 5
// are the values §2.4 fixed before #89; 2, 3, 6 and 7 are #89's and 1 is #609's, read off world-v1 §4
// (the light source, the brightness and the main colours of each layer). The art for a layer (a far view and a
// ground, #89's second half) goes between the sky and the haze, so everything here stays when it lands.
//
// C# `float` (single precision): every float value is held as its exact float32 value (Math.fround), and
// every float operation of the C# is rounded with Math.fround right after it, so the numbers are
// bit-identical to the C#'s (a + b, a * b, a / b on two floats rounded once to float is what the CLR
// does; FMA contraction is not done by RyuJIT). Float parameters are rounded on entry, as the C#
// caller's implicit conversion to float would. double arithmetic (Luminance, Contrast) stays double.
//
// Structs (Rgba, SkyStop, Flicker) are immutable records (value equality, like a C# struct's Equals).
// `default(Rgba)` is `new Rgba(0, 0, 0, 0)` (every channel 0, alpha 0 too — not the constructor's
// default alpha 1). Classes with C# object initializers (Haze, Motes, LayerBackdrop) take one plain
// object of the members to set: `new Haze { Shape = ..., X = 0.42f }` → `new Haze({ Shape: ..., X: 0.42 })`.
// Their float members are rounded to float32 by the constructor; assign float32 values (Math.fround)
// if you set one later.
import { defineRecord, defineEnum, ArgumentOutOfRangeException } from '../../shared/cs.js';
import { Pow } from './CrtPow.js';

const f = Math.fround;

/** .NET FormatException (not in cs.js; only thrown by Rgba.Hex on a malformed colour). */
class FormatException extends Error {
  constructor(m) {
    super(m);
    this.name = 'FormatException';
  }
}

/**
 * Sets the members of an object-initializer `init` on `target`, rounding the float members to float32.
 * A key the type does not have throws (catches a typo that C# would not compile).
 */
function initialize(target, init, floats) {
  if (init == null) return;
  for (const key of Object.keys(init)) {
    if (!Object.prototype.hasOwnProperty.call(target, key)) {
      throw new Error(`${target.constructor.name} has no member '${key}'`);
    }
    const v = init[key];
    if (v === undefined) continue;
    target[key] = floats.includes(key) ? f(v) : v;
  }
}

// ---------------------------------------------------------------- Rgba

/** A colour in sRGB, 0〜1 per channel, with its alpha. The mockups blend in sRGB, so this does too. */
export class Rgba extends defineRecord('Rgba', ['R', 'G', 'B', ['A', 1]]) {
  constructor(r, g, b, a = 1) {
    super(f(r), f(g), f(b), f(a));
  }

  /** "#rrggbb" with the alpha given apart (the CSS rgba(...) of §2.4). */
  static Hex(hex, alpha = 1) {
    const h = TrimStart(hex, '#');
    return new Rgba(Channel(h, 0), Channel(h, 2), Channel(h, 4), alpha);
  }

  /** The CSS rgba(r, g, b, a) form §2.4 writes the floor tiles in. */
  static Bytes(r, g, b, alpha = 1) {
    return new Rgba(f(r / 255), f(g / 255), f(b / 255), alpha);
  }

  WithAlpha(alpha) {
    return new Rgba(this.R, this.G, this.B, alpha);
  }
}

/** string.TrimStart(char): every leading `ch` removed. */
function TrimStart(s, ch) {
  let i = 0;
  while (i < s.length && s[i] === ch) i++;
  return s.substring(i);
}

/** private static float Channel(string hex, int at) — Convert.ToInt32(hex.Substring(at, 2), 16) / 255f. */
function Channel(hex, at) {
  return f(ToInt32Hex(Substring(hex, at, 2)) / 255);
}

/** string.Substring(startIndex, length) with .NET's range checks. */
function Substring(s, start, length) {
  if (start > s.length) {
    throw new ArgumentOutOfRangeException("startIndex cannot be larger than length of string. (Parameter 'startIndex')");
  }
  if (start + length > s.length) {
    throw new ArgumentOutOfRangeException("Index and length must refer to a location within the string. (Parameter 'length')");
  }
  return s.substr(start, length);
}

/** Convert.ToInt32(s, 16) for the two-character pieces Channel reads ("0x" prefix allowed, as .NET does). */
function ToInt32Hex(s) {
  let body = s;
  if (body.length >= 2 && body[0] === '0' && (body[1] === 'x' || body[1] === 'X')) body = body.substring(2);
  const digit = (c) => {
    const d = '0123456789abcdef'.indexOf(c.toLowerCase());
    return c.length === 1 && d >= 0 ? d : -1;
  };
  if (body.length === 0 || digit(body[0]) < 0) throw new FormatException('Could not find any recognizable digits.');
  let n = 0;
  for (let i = 0; i < body.length; i++) {
    const d = digit(body[i]);
    if (d < 0) throw new FormatException('Additional non-parsable characters are at the end of the string.');
    n = n * 16 + d;
  }
  return n;
}

/** default(Rgba): every field 0. */
function DefaultRgba() {
  return new Rgba(0, 0, 0, 0);
}

// ---------------------------------------------------------------- SkyStop

/** One stop of the sky gradient. At runs 0 at the top of the screen to 1 at the bottom. */
export class SkyStop extends defineRecord('SkyStop', ['At', 'Color']) {
  constructor(at, color) {
    super(f(at), color);
  }
}

// ---------------------------------------------------------------- HazeShape

export const HazeShape = defineEnum([
  /** Light from above: Haze.Color at the top, gone at Haze.Size of the height. */
  'TopGlow',
  /** A level band centred on Haze.Y, Haze.Size of the height across, soft at both edges. */
  'Band',
  /** Haze.Count shafts of light, Haze.Size of the width each, leaning Haze.Angle degrees from upright. */
  'Shafts',
  /** A soft round glow at (Haze.X, Haze.Y), Haze.Size of the height across. */
  'Radial',
]);

// ---------------------------------------------------------------- Flicker

/**
 * How a light breathes: its alpha falls by up to Depth of itself and comes back once
 * every PeriodMs. Flash makes it a short flash at the start of the
 * period instead (the lightning of layer 6), dark the rest of the time.
 */
export class Flicker extends defineRecord('Flicker', ['PeriodMs', 'Depth', ['Flash', false]]) {
  constructor(periodMs, depth, flash = false) {
    super(periodMs, f(depth), flash);
  }

  get Moves() {
    return this.PeriodMs > 0 && this.Depth > 0;
  }
}
/** public static readonly Flicker Still = new Flicker(0, 0f); */
Flicker.Still = new Flicker(0, 0);

// ---------------------------------------------------------------- Haze

/** One layer of haze over the sky. Positions are fractions of the screen, 0 at the top left. */
export class Haze {
  constructor(init) {
    this.Shape = HazeShape.TopGlow;
    this.Color = DefaultRgba();
    this.X = f(0.5);
    this.Y = f(0.5);
    this.Size = 1;
    this.Count = 1;
    this.Angle = 0;
    this.Flicker = Flicker.Still;
    initialize(this, init, ['X', 'Y', 'Size', 'Angle']);
  }
}

// ---------------------------------------------------------------- Motes

/**
 * The specks that drift through the haze: dust in the light, sand, crystal sparks, salt, miasma.
 * Speeds are in reference pixels (1920×1080) per second, + to the right and downward.
 */
export class Motes {
  constructor(init) {
    this.Count = 0;
    this.Color = DefaultRgba();
    this.SizePx = 4;
    this.SpeedX = 0;
    this.SpeedY = 0;
    /** How far each speck's alpha dips as it twinkles, 0 (steady) to 1. */
    this.Twinkle = 0;
    initialize(this, init, ['SizePx', 'SpeedX', 'SpeedY', 'Twinkle']);
  }
}
/** public static readonly Motes None = new Motes(); */
Motes.None = new Motes();

// ---------------------------------------------------------------- LayerBackdrop

/** Everything one layer draws behind the battle. */
export class LayerBackdrop {
  constructor(init) {
    this.Layer = 0;
    /** The layer's name (world-v1 §4). Layer 2 keeps its masked name. */
    this.Name = null;
    this.Sky = null;
    this.Hazes = null;
    this.Motes = Motes.None;
    /** 床のタイル: the fill of a floor cell, drawn by the floor over the backdrop. */
    this.Tile = DefaultRgba();
    initialize(this, init, []);
  }
}

// ---------------------------------------------------------------- BackdropSpec

/** The layers of the dragon's lair (world-v1 §4). */
const Layers = 7;
/** battle_ui_ux_v2 §4.3: a new layer fades in over the old one in the 400 ms of the switch banner. */
const CrossfadeMs = 400;
/** The floor's band on the 1920×1080 screen (battle-visual-v1 §4.2: top y 560, cells 40 px tall). */
const FloorTop = f(560 / 1080);
const FloorBottom = f(600 / 1080);
/**
 * The red of the cells an enemy's action reaches (battle-visual-v1 §2.1). It is drawn at 100%
 * over the floor tile, and §2.3 holds it to a contrast of 3 there on every layer.
 */
const ReachRed = Rgba.Hex('#ff1f4f');
const ReachRedMinContrast = 3;

const White = new Rgba(1, 1, 1);
const Sunlight = Rgba.Hex('#f4f8dc'); // the pale yellow-green sun of layer 1
const Phosphor = Rgba.Hex('#b9a3ff'); // 燐光 (world-v1 §6)
const Violet = Rgba.Hex('#8b6fd0'); // 菫 (world-v1 §6)

const All = [
  // 1 燦光の樹海, bright (#609): young grass under the sun falling through the hole in the roof, in
  // near-upright shafts. The sun is pale yellow-green and white, never amber (world-v1 §3 rule 4), and no crystal grows here (rule 5).
  new LayerBackdrop({
    Layer: 1,
    Name: '燦光の樹海',
    Sky: [new SkyStop(0, Rgba.Hex('#d6e8a4')), new SkyStop(0.4, Rgba.Hex('#7cab4a')), new SkyStop(1, Rgba.Hex('#22391c'))],
    Hazes: [
      new Haze({ Shape: HazeShape.Shafts, Color: White.WithAlpha(0.28), X: 0.42, Size: 0.05, Count: 4, Angle: 8, Flicker: new Flicker(6000, 0.25) }),
      new Haze({ Shape: HazeShape.TopGlow, Color: Sunlight.WithAlpha(0.30), Size: 0.5 }),
    ],
    Motes: new Motes({ Count: 24, Color: Sunlight.WithAlpha(0.35), SizePx: 4, SpeedX: 6, SpeedY: 10, Twinkle: 0.5 }),
    Tile: Rgba.Bytes(10, 18, 12, 0.82),
  }),
  // 2 ■■の秘跡: sand and white stone in a slanting light; the first violet crystals sprout at the foot of the pillars.
  new LayerBackdrop({
    Layer: 2,
    Name: '■■の秘跡',
    Sky: [new SkyStop(0, Rgba.Hex('#b8a684')), new SkyStop(0.5, Rgba.Hex('#8a7a5e')), new SkyStop(1, Rgba.Hex('#4a4034'))],
    Hazes: [
      new Haze({ Shape: HazeShape.Shafts, Color: White.WithAlpha(0.25), X: 0.3, Size: 0.06, Count: 3, Angle: 18, Flicker: new Flicker(7000, 0.2) }),
      new Haze({ Shape: HazeShape.Radial, Color: Phosphor.WithAlpha(0.12), X: 0.5, Y: 0.95, Size: 0.5, Flicker: new Flicker(4200, 0.3) }),
    ],
    Motes: new Motes({ Count: 20, Color: Rgba.Hex('#e8dcc0', 0.25), SizePx: 3, SpeedX: 14, SpeedY: 4, Twinkle: 0.2 }),
    Tile: Rgba.Bytes(16, 13, 10, 0.82),
  }),
  // 3 大黒蛇の住処: the one dark upper layer; the crystals on the serpent's back are its only light.
  new LayerBackdrop({
    Layer: 3,
    Name: '大黒蛇の住処',
    Sky: [new SkyStop(0, Rgba.Hex('#1e1a3a')), new SkyStop(1, Rgba.Hex('#0b0a18'))],
    Hazes: [
      new Haze({ Shape: HazeShape.Radial, Color: Phosphor.WithAlpha(0.10), X: 0.78, Y: 0.25, Size: 0.6, Flicker: new Flicker(3200, 0.4) }),
      new Haze({ Shape: HazeShape.TopGlow, Color: Violet.WithAlpha(0.07), Size: 0.6 }),
    ],
    Motes: new Motes({ Count: 12, Color: Phosphor.WithAlpha(0.35), SizePx: 3, SpeedX: -3, SpeedY: -6, Twinkle: 0.7 }),
    Tile: Rgba.Bytes(12, 14, 28, 0.78),
  }),
  // 4 輝晶の間, the brightest: #e4e6f2 → #b7bbd6 → #7d82a8, upright light streaks (white 35%), white 60% from above.
  new LayerBackdrop({
    Layer: 4,
    Name: '輝晶の間',
    Sky: [new SkyStop(0, Rgba.Hex('#e4e6f2')), new SkyStop(0.5, Rgba.Hex('#b7bbd6')), new SkyStop(1, Rgba.Hex('#7d82a8'))],
    Hazes: [
      new Haze({ Shape: HazeShape.Shafts, Color: White.WithAlpha(0.35), X: 0.5, Size: 0.05, Count: 5, Angle: 0, Flicker: new Flicker(5000, 0.2) }),
      new Haze({ Shape: HazeShape.TopGlow, Color: White.WithAlpha(0.60), Size: 0.45 }),
    ],
    Motes: new Motes({ Count: 20, Color: White.WithAlpha(0.5), SizePx: 3, SpeedX: 0, SpeedY: 3, Twinkle: 0.9 }),
    Tile: Rgba.Bytes(14, 16, 28, 0.82),
  }),
  // 5 血染めの死海: the red haze (rgba(140,30,40,.28)) over #1a0e12 → #070507; salt drifts white off the sea bed.
  new LayerBackdrop({
    Layer: 5,
    Name: '血染めの死海',
    Sky: [new SkyStop(0, Rgba.Hex('#1a0e12')), new SkyStop(1, Rgba.Hex('#070507'))],
    Hazes: [
      new Haze({ Shape: HazeShape.Band, Color: Rgba.Bytes(140, 30, 40, 0.28), Y: 0.4, Size: 0.7, Flicker: new Flicker(8000, 0.25) }),
    ],
    Motes: new Motes({ Count: 18, Color: Rgba.Hex('#efe6dc', 0.2), SizePx: 3, SpeedX: 8, SpeedY: -4, Twinkle: 0.3 }),
    Tile: Rgba.Bytes(12, 8, 10, 0.80),
  }),
  // 6 獄雷峡: dark; the black lightning shows only by its violet edge, so the light is a rare flash, not a glow (world-v1 §4.1).
  new LayerBackdrop({
    Layer: 6,
    Name: '獄雷峡',
    Sky: [new SkyStop(0, Rgba.Hex('#120b1c')), new SkyStop(1, Rgba.Hex('#040208'))],
    Hazes: [
      new Haze({ Shape: HazeShape.Shafts, Color: Violet.WithAlpha(0.22), X: 0.62, Size: 0.012, Count: 1, Angle: -12, Flicker: new Flicker(5200, 1, true) }),
      new Haze({ Shape: HazeShape.TopGlow, Color: Violet.WithAlpha(0.05), Size: 0.5 }),
    ],
    Motes: Motes.None,
    Tile: Rgba.Bytes(8, 6, 12, 0.84),
  }),
  // 7 果てなき世界, the darkest: a hollow made of miasma, lit by the miasma itself; the spring at its centre glows violet.
  new LayerBackdrop({
    Layer: 7,
    Name: '果てなき世界',
    Sky: [new SkyStop(0, Rgba.Hex('#080a1a')), new SkyStop(1, Rgba.Hex('#020309'))],
    Hazes: [
      new Haze({ Shape: HazeShape.Radial, Color: Violet.WithAlpha(0.22), X: 0.5, Y: 0.9, Size: 0.9, Flicker: new Flicker(7000, 0.3) }),
    ],
    Motes: new Motes({ Count: 30, Color: Violet.WithAlpha(0.3), SizePx: 4, SpeedX: 0, SpeedY: -12, Twinkle: 0.5 }),
    Tile: Rgba.Bytes(6, 6, 14, 0.84),
  }),
];

/** private static double Linear(float c) — System.Math.Pow is CrtPow.Pow (glibc's pow, as the oracle rounds it). */
function Linear(c) {
  return c <= 0.04045 ? c / 12.92 : Pow((c + 0.055) / 1.055, 2.4);
}

/**
 * Static class. Public static members in C# declaration order (fields, then methods); the private
 * ones (White, Sunlight, Phosphor, Violet, All, Linear) are module-level.
 */
export const BackdropSpec = Object.freeze({
  Layers,
  CrossfadeMs,
  FloorTop,
  FloorBottom,
  ReachRed,
  ReachRedMinContrast,

  /** The backdrop of `layer`; anything outside 1〜7 is held to the nearest end. */
  For(layer) {
    return All[BackdropSpec.Clamp(layer) - 1];
  },

  Clamp(layer) {
    if (layer < 1) return 1;
    return layer > Layers ? Layers : layer;
  },

  /** The sky's colour at `y` (0 top, 1 bottom), straight between its stops. */
  SkyAt(backdrop, y) {
    y = f(y);
    const sky = backdrop.Sky;
    if (y <= sky[0].At) return sky[0].Color;
    for (let i = 1; i < sky.length; i++) {
      if (y > sky[i].At) continue;
      const t = f(f(y - sky[i - 1].At) / f(sky[i].At - sky[i - 1].At));
      return BackdropSpec.Mix(sky[i - 1].Color, sky[i].Color, t);
    }
    return sky[sky.length - 1].Color;
  },

  /**
   * The most a haze can lay on the point at height `y` (0 top, 1 bottom) in any
   * column, at the top of its breath: a shaft or a radial glow is taken where it is densest across.
   */
  HazeAlphaAt(haze, y) {
    y = f(y);
    const a = haze.Color.A;
    switch (haze.Shape) {
      case HazeShape.TopGlow:
        return y >= haze.Size ? 0 : f(a * f(1 - f(y / haze.Size)));
      case HazeShape.Band: {
        const half = f(haze.Size / 2);
        const d = Math.abs(f(y - haze.Y));
        return d >= half ? 0 : f(a * f(1 - f(d / half)));
      }
      case HazeShape.Shafts:
        return a;
      case HazeShape.Radial: {
        const r = f(haze.Size / 2);
        const d = Math.abs(f(y - haze.Y));
        return d >= r ? 0 : f(a * f(1 - f(d / r)));
      }
      default:
        return 0;
    }
  },

  /** The brightest the backdrop gets at height `y`: the sky with every haze laid on at its densest. */
  BrightestAt(backdrop, y) {
    y = f(y);
    let under = BackdropSpec.SkyAt(backdrop, y);
    for (const haze of backdrop.Hazes) under = BackdropSpec.Over(haze.Color.WithAlpha(BackdropSpec.HazeAlphaAt(haze, y)), under);
    return under;
  },

  /** `top` laid over the opaque `under` at the top's alpha (sRGB, as the mockups do). */
  Over(top, under) {
    const a = top.A;
    const rest = f(1 - a);
    return new Rgba(
      f(f(top.R * a) + f(under.R * rest)),
      f(f(top.G * a) + f(under.G * rest)),
      f(f(top.B * a) + f(under.B * rest)),
    );
  },

  Mix(a, b, t) {
    t = f(t);
    return new Rgba(
      f(a.R + f(f(b.R - a.R) * t)),
      f(a.G + f(f(b.G - a.G) * t)),
      f(a.B + f(f(b.B - a.B) * t)),
      f(a.A + f(f(b.A - a.A) * t)),
    );
  },

  /** WCAG 2 relative luminance. */
  Luminance(c) {
    return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
  },

  /** WCAG 2 contrast ratio, 1〜21. */
  Contrast(a, b) {
    const la = BackdropSpec.Luminance(a);
    const lb = BackdropSpec.Luminance(b);
    return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
  },

  /**
   * The lowest contrast the reach red gets on `backdrop`'s floor tile, over the
   * floor's band from top to bottom, with every haze at its densest (battle-visual-v1 §2.3).
   */
  ReachRedContrast(backdrop) {
    let lowest = Number.MAX_VALUE; // double.MaxValue
    for (let step = 0; step <= 8; step++) {
      // FloorTop + (FloorBottom - FloorTop) * step / 8f, all in float.
      const y = f(FloorTop + f(f(f(FloorBottom - FloorTop) * step) / 8));
      const tile = BackdropSpec.Over(backdrop.Tile, BackdropSpec.BrightestAt(backdrop, y));
      lowest = Math.min(lowest, BackdropSpec.Contrast(ReachRed, tile));
    }
    return lowest;
  },
});
