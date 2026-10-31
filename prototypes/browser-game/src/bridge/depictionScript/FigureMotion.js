// Port of unity-project-kit/Assets/View/Depiction/Script/FigureMotion.cs
// The figure's art and the shapes its body takes (#288). Pure C#, like the rest of Script/: the View
// asks these which picture to wear, which way the body faces and how far a move stretches it, so
// `dotnet test` holds the same numbers the screen plays.
//
// Port notes:
// - Every C# `float` is a single: float parameters are rounded on entry, constants are their float32
//   values and every float operation is rounded with Math.fround, as .NET computes in single precision.
// - The value tuples `(float X, float Y)` are plain objects `{ X, Y }`.
import { ArgumentException, ArgumentOutOfRangeException, defineEnum } from '../../shared/cs.js';
import { StrikeSystem, UnitSide } from './DepictionTypes.js';

const f = Math.fround;

/** new ArgumentOutOfRangeException(paramName, actualValue, message) with .NET's message. */
function argRangeValue(name, value, message) {
  const e = new ArgumentOutOfRangeException(`${message} (Parameter '${name}')\nActual value was ${value}.`);
  e.ParamName = name;
  e.ActualValue = value;
  return e;
}

/** new ArgumentException(message, paramName) with .NET's message. */
function argException(message, name) {
  const e = new ArgumentException(`${message} (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/**
 * The four pictures a figure can wear. Only Idle is required; a pose with no picture shows the idle
 * one, and the motion alone carries the moment.
 * battle_ui_ux_v2 §6.4 lists the same four. 崩れ (the stagger of §5.14 順 2) has no picture of its
 * own (#309): the script carries no beat for the moment of a break, so the stagger is motion only.
 * A collapse pose comes with that beat.
 */
export const FigurePose = defineEnum(['Idle', 'Act', 'Hit', 'Down']);

function Require(id) {
  if (id == null || id.length === 0) throw argException('An art id is required.', 'id');
  return id;
}

/**
 * Where a figure's art lives in the Unity project (asset-intake.md §4):
 * Assets/Art/Characters/<id>/chr_<id>_<category>_<label>.png.
 * The Editor fills the figure's shelf from these paths; the script only names the id.
 */
export const CharacterArt = {
  Root: 'Assets/Art/Characters',

  Folder(id) {
    return CharacterArt.Root + '/' + Require(id);
  },

  /** The file a pose is read from. FigurePose.Act is a prefix: the first act_* picture is taken. */
  FileName(id, pose) {
    Require(id);
    switch (pose) {
      case FigurePose.Idle: return 'chr_' + id + '_idle_stand.png';
      case FigurePose.Act: return 'chr_' + id + '_act_';
      case FigurePose.Hit: return 'chr_' + id + '_react_hit.png';
      case FigurePose.Down: return 'chr_' + id + '_react_down.png';
      default: throw argRangeValue('pose', pose, 'No file is named for this pose.');
    }
  },

  PathOf(id, pose) {
    return CharacterArt.Folder(id) + '/' + CharacterArt.FileName(id, pose);
  },
};

/**
 * Which way a figure's body faces, as its x scale at rest (#288). Art is drawn the way it faces on
 * screen — the enemy to the left, the player to the right — and is never mirrored. The placeholder
 * silhouette (ProceduralArt) is drawn facing right, so only the enemy's placeholder is mirrored.
 */
export const FigureFacing = {
  HomeScaleX(side, wearsArt) {
    return !wearsArt && side === UnitSide.Enemy ? f(-1) : f(1);
  },

  /**
   * The body's scale while it moves: the move's stretch times the scale at rest, so a mirrored
   * body stays mirrored through the move and is back at rest when `bell` is 0. Returns { X, Y }.
   */
  During(homeScaleX, system, bell) {
    const { X: x, Y: y } = EnemyMotionShape.Stretch(system, bell);
    return { X: f(f(homeScaleX) * x), Y: y };
  },
};

/**
 * How wide a figure's frame is for the art it wears (#302). The body is drawn with preserveAspect
 * in a frame of fixed height whose bottom edge is the feet; art wider than the frame's rest shape
 * would be shrunk to fit the width, and Unity hands the lost height out half above, half below,
 * so the figure would float off the floor line. The frame is widened instead, to the art's own
 * shape at the frame's height; art no wider than the rest shape (the 640 x 1536 pictures in a
 * 210 x 450 frame) keeps the rest frame, so it looks exactly as before.
 */
export const FigureFrame = {
  /**
   * The frame's width for one picture of `artWidth` x `artHeight` pixels: height x artWidth / artHeight,
   * never narrower than `restWidth`. A picture with no size (or no picture) keeps the rest width.
   */
  Width(restWidth, height, artWidth, artHeight) {
    restWidth = f(restWidth);
    height = f(height);
    artWidth = f(artWidth);
    artHeight = f(artHeight);
    if (artWidth <= 0 || artHeight <= 0 || height <= 0) return restWidth;
    return Math.max(restWidth, f(f(height * artWidth) / artHeight));
  },

  /**
   * How high above the frame's bottom edge a preserveAspect picture's feet are drawn, with the
   * body's pivot at the centre (Unity's Image splits the unused height evenly). Zero means the
   * picture stands on the frame's bottom edge, which is the floor line.
   */
  Lift(frameWidth, frameHeight, artWidth, artHeight) {
    frameWidth = f(frameWidth);
    frameHeight = f(frameHeight);
    artWidth = f(artWidth);
    artHeight = f(artHeight);
    if (artWidth <= 0 || artHeight <= 0 || frameWidth <= 0 || frameHeight <= 0) return f(0);
    const drawnHeight = Math.min(frameHeight, f(f(frameWidth * artHeight) / artWidth));
    return f(f(frameHeight - drawnHeight) / 2);
  },

  // ---- battle-visual-v1 §4.3 人型 (#455) ----------------------------------------------------

  /**
   * §4.3: a figure stands 226 px tall on the 1920×1080 screen, feet on y 570 — about 75% of the
   * 300 px battle_ui_ux_v2 drew it at. The Unity frame is 450 tall, so it is drawn at 226 / 450.
   */
  StandardHeight: f(226),

  /** §4.3: an elite that takes two cells stands 264 px tall. */
  LargeHeight: f(264),

  /** §4.3 足元の灯り: an ellipse under the feet, 60 px tall and twice as wide as the figure. */
  FootLightHeight: f(60),

  FootLightWidthRatio: f(2),

  /** §4.3 シルエット: the silhouette's edge light is 2 px wide on the screen. */
  EdgeLightWidth: f(2),

  /**
   * How far the edge light is drawn out from the silhouette, in the body's own units, under a
   * figure drawn at `figureScale`: the 2 px counted back out of the scale, so it stays 2 px on the
   * screen at 226 and at 264.
   */
  EdgeLightOffset(figureScale) {
    return f(FigureFrame.EdgeLightWidth * FigureFrame.Counter(figureScale));
  },

  /** How tall a figure that takes `cells` cells stands on the screen. */
  HeightFor(cells) {
    return cells >= 2 ? FigureFrame.LargeHeight : FigureFrame.StandardHeight;
  },

  /** The scale that draws a frame `frameHeight` tall at `height` screen pixels. A frame with no height is drawn as it is. */
  ScaleFor(height, frameHeight) {
    height = f(height);
    frameHeight = f(frameHeight);
    if (height <= 0 || frameHeight <= 0) return f(1);
    return f(height / frameHeight);
  },

  /**
   * The scale a child takes so its own sizes stay in screen pixels under a parent drawn at
   * `parentScale` (the target brackets, the foot light's height). A parent with no usable scale
   * leaves the child as it is.
   */
  Counter(parentScale) {
    const scale = Math.abs(f(parentScale));
    return scale < f(0.01) ? f(1) : f(1 / scale);
  },
};

/**
 * How the enemy's body moves into its action (EffectId.EnemyMotion), by system, so the four actions
 * read apart on one picture: 払 swings across, 突 drives straight in, 打 shoves with its weight,
 * 盾 raises the haft; a step just steps. `bell` runs 0 → 1 → 0 over the move.
 */
export const EnemyMotionShape = {
  /** How far the figure travels toward the other side, in px at the end of the move. */
  Reach(system) {
    switch (system) {
      case StrikeSystem.Thrust: return f(90);
      case StrikeSystem.Sweep: return f(40);
      case StrikeSystem.Strike: return f(60);
      case StrikeSystem.Shield: return f(0);
      default: return f(70);
    }
  },

  /** The stretch of the body, as factors on its scale at rest (1, 1 = unchanged). Returns { X, Y }. */
  Stretch(system, bell) {
    bell = f(bell);
    switch (system) {
      case StrikeSystem.Sweep: return { X: f(1 + f(f(0.08) * bell)), Y: f(1) };
      case StrikeSystem.Thrust: return { X: f(1 + f(f(0.14) * bell)), Y: f(1 - f(f(0.05) * bell)) };
      case StrikeSystem.Strike: return { X: f(1 + f(f(0.12) * bell)), Y: f(1 - f(f(0.08) * bell)) };
      case StrikeSystem.Shield: return { X: f(1), Y: f(1 + f(f(0.14) * bell)) };
      default: return { X: f(1), Y: f(1) };
    }
  },
};

function Fall(t) {
  const u = Math.min(f(1), Math.max(f(0), f(t / DefeatShape.FadeFrom)));
  return f(u * u); // eases in: the body gives way, then drops
}

/**
 * The fall (EffectId.Defeat, battle_ui_ux_v2 §2.2 の 6): the figure tilts back from the other side
 * and sinks while it darkens, then fades away. `t` runs 0 → 1 over the whole length.
 */
export const DefeatShape = {
  /** Where the fade starts: the tilt and the sink take the time before it. */
  FadeFrom: f(0.6),

  TiltDegrees: f(24),
  SinkPixels: f(40),

  /** How dark the body ends: its colour is multiplied by this. */
  DarkTo: f(0.35),

  /**
   * The tilt in degrees about the feet. The enemy faces left and falls back to the right
   * (clockwise, negative); the player the other way.
   */
  Tilt(side, t) {
    t = f(t);
    const away = side === UnitSide.Enemy ? f(-1) : f(1);
    return f(f(away * DefeatShape.TiltDegrees) * Fall(t));
  },

  Sink(t) {
    return f(-DefeatShape.SinkPixels * Fall(f(t)));
  },

  Brightness(t) {
    // (1f - DarkTo) is a C# constant: fround(1 - fround(0.35)).
    return f(1 - f(f(1 - DefeatShape.DarkTo) * Fall(f(t))));
  },

  Alpha(t) {
    t = f(t);
    if (t <= DefeatShape.FadeFrom) return f(1);
    // (1f - FadeFrom) is a C# constant: fround(1 - fround(0.6)).
    return Math.max(f(0), f(1 - f(f(t - DefeatShape.FadeFrom) / f(1 - DefeatShape.FadeFrom))));
  },
};
