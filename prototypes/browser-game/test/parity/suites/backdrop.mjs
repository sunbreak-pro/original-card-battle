// Mirrors oracle/src/Suites/Backdrop.cs. Float arguments are given as Math.fround of the C# literal.
import { arr, readStatic, outXY, range } from '../lib/cs.mjs';

export const imports = [
  'src/bridge/backdrop/BackdropSpec.js',
  'src/bridge/backdrop/ExplorationBackdrop.js',
  'src/bridge/backdrop/MiasmaLook.js',
  'src/bridge/backdrop/BackdropMotion.js',
  'src/bridge/backdrop/BackdropSwitch.js',
];

/** Presentation values: doubles may differ by a few ulps (.NET vs V8 Math.Pow / Sin / Cos). */
export const ulps = 4;

const f = Math.fround;
const HEIGHTS = [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.52, 0.54, 0.56, 0.6, 0.7, 0.8, 0.9, 1].map(f);
const TIMES = [0, 70, 140, 500, 1234.5, 3000, 7000, 12345];
const DISTANCES = [0, 0.3, 0.5, 0.8, 0.95, 1, 1.2].map(f);

export function run(c, { use, expected }) {
  const { BackdropSpec, Rgba, Flicker } = use('src/bridge/backdrop/BackdropSpec.js', 'BackdropSpec', 'Rgba', 'Flicker');
  const { ExplorationBackdropSpec } = use('src/bridge/backdrop/ExplorationBackdrop.js', 'ExplorationBackdropSpec');
  const { MiasmaLook } = use('src/bridge/backdrop/MiasmaLook.js', 'MiasmaLook');
  const { BackdropMotion } = use('src/bridge/backdrop/BackdropMotion.js', 'BackdropMotion');
  const env = { BackdropSpec, Rgba, Flicker, ExplorationBackdropSpec, MiasmaLook, BackdropMotion };

  if (c.id === 'table:BackdropSpec') {
    const ex = expected || {};
    return {
      Static: readStatic(BackdropSpec, Object.keys(ex.Static || {})),
      ExplorationStatic: readStatic(ExplorationBackdropSpec, Object.keys(ex.ExplorationStatic || {})),
      MiasmaLookStatic: readStatic(MiasmaLook, Object.keys(ex.MiasmaLookStatic || {})),
      MotionStatic: readStatic(BackdropMotion, Object.keys(ex.MotionStatic || {})),
      Clamp: range(-1, 11).map((l) => BackdropSpec.Clamp(l)),
      Hex: [Rgba.Hex('#ff1f4f'), Rgba.Hex('10141d', f(0.5)), Rgba.Bytes(157, 166, 186, f(0.26))],
    };
  }
  if (c.id.startsWith('layer:')) return layer(c.input.Layer, env);
  if (c.id.startsWith('miasma:')) return miasma(c.input.Percent, env);
  if (c.id === 'motion') return motion(env);
  if (c.id === 'switch') {
    const { BackdropSwitch } = use('src/bridge/backdrop/BackdropSwitch.js', 'BackdropSwitch');
    return switchLog(BackdropSwitch);
  }
  throw new Error(`harness: unknown backdrop case ${c.id}`);
}

function layer(l, { BackdropSpec, ExplorationBackdropSpec, BackdropMotion }) {
  const b = BackdropSpec.For(l);
  const e = ExplorationBackdropSpec.For(l);
  const hazes = arr(b.Hazes);
  return {
    For: b,
    SkyAt: HEIGHTS.map((y) => BackdropSpec.SkyAt(b, y)),
    HazeAlphaAt: hazes.map((h) => HEIGHTS.map((y) => BackdropSpec.HazeAlphaAt(h, y))),
    BrightestAt: HEIGHTS.map((y) => BackdropSpec.BrightestAt(b, y)),
    Luminance: HEIGHTS.map((y) => BackdropSpec.Luminance(BackdropSpec.BrightestAt(b, y))),
    ReachRedContrast: BackdropSpec.ReachRedContrast(b),
    TileContrast: BackdropSpec.Contrast(BackdropSpec.ReachRed, b.Tile),
    FlickerAlpha: hazes.map((h) => TIMES.map((ms) => BackdropMotion.FlickerAlpha(h.Flicker, ms))),
    Exploration: e,
    ExplorationBrightestAt: HEIGHTS.map((y) => ExplorationBackdropSpec.BrightestAt(e, y)),
    LowestContrastInk: ExplorationBackdropSpec.LowestContrast(e, ExplorationBackdropSpec.Ink),
    LowestContrastAmber: ExplorationBackdropSpec.LowestContrast(e, ExplorationBackdropSpec.Amber),
    PeakLuminance: ExplorationBackdropSpec.PeakLuminance(e),
    Mix: [0, 0.25, 0.5, 1].map((t) => BackdropSpec.Mix(arr(b.Sky)[0].Color, b.Tile, f(t))),
    Over: BackdropSpec.Over(b.Tile, arr(b.Sky)[0].Color),
  };
}

function miasma(p, { BackdropSpec, MiasmaLook }) {
  const next = [p, p + 1, p + 5, p + 10, p + 20, 100];
  const shade = MiasmaLook.ShadeAt(p);
  return {
    FillAt: MiasmaLook.FillAt(p),
    Next: next,
    Warning: next.map((n) => MiasmaLook.Warning(p, n)),
    AheadColor: next.map((n) => MiasmaLook.AheadColor(p, n)),
    ForecastMark: next.map((n) => MiasmaLook.ForecastMark(p, n)),
    BreathPeriodMs: MiasmaLook.BreathPeriodMs(p),
    Breath: TIMES.map((ms) => MiasmaLook.Breath(p, ms)),
    FillAlpha: TIMES.map((ms) => MiasmaLook.FillAlpha(p, ms)),
    ShadeAt: shade,
    ShadeAtTimes: TIMES.map((ms) => MiasmaLook.ShadeAt(p, ms)),
    Rise: TIMES.map((ms) => MiasmaLook.Rise(p, Math.min(100, p + 10), ms)),
    EdgeFalloff: DISTANCES.map((d) => MiasmaLook.EdgeFalloff(MiasmaLook.ShadeAt(p), d)),
    ShadeAlphaAt: DISTANCES.map((d) => MiasmaLook.ShadeAlphaAt(MiasmaLook.ShadeAt(p), d)),
    Shaded: [0, 0.5, 1].map((d) => MiasmaLook.Shaded(BackdropSpec.For(3).Tile, MiasmaLook.ShadeAt(p), f(d))),
  };
}

function motion({ BackdropSpec, Flicker, MiasmaLook, BackdropMotion }) {
  const motes = BackdropSpec.For(7).Motes;
  const at = [];
  const alpha = [];
  for (let index = 0; index < 6; index++) {
    for (const ms of TIMES) {
      const [x, y] = outXY(BackdropMotion.MoteAt(motes, index, ms, 1920, 1080));
      at.push({ X: x, Y: y });
      alpha.push(BackdropMotion.MoteAlpha(motes, index, ms));
    }
  }
  const hash = [];
  for (const index of [0, 1, 2, 7, 100, -3, 2147483647]) for (const salt of [0, 1, 5]) hash.push(BackdropMotion.Hash(index, salt));
  return {
    Motes: motes,
    MoteAt: at,
    MoteAlpha: alpha,
    Crossfade: [-10, 0, 100, 200, 399.9, 400, 1000].map((ms) => BackdropMotion.Crossfade(ms)),
    Hash: hash,
    Ticks: MiasmaLook.Ticks(),
    CornerDistance: [[0, 0], [0.5, 0.5], [1, 1], [0.25, 0.75]].map(([x, y]) => MiasmaLook.CornerDistance(f(x), f(y), f(16 / 9))),
    FlickerStill: BackdropMotion.FlickerAlpha(Flicker.Still, 1000),
    FlickerFlash: TIMES.map((ms) => BackdropMotion.FlickerAlpha(new Flicker(900, f(0.6), true), ms)),
  };
}

function switchLog(BackdropSwitch) {
  const sw = new BackdropSwitch();
  const log = [];
  for (const [layer, now] of [[1, 0], [1, 50], [2, 100], [3, 200], [3, 300], [9, 1000]]) {
    const change = sw.Show(layer, now);
    log.push({ Show: layer, At: now, Change: change, Shown: sw.Shown, Leaving: sw.Leaving });
    for (const dt of [0, 150, 450]) {
      const t = sw.Tick(now + dt);
      log.push({ Tick: now + dt, T: t, Shown: sw.Shown, Leaving: sw.Leaving });
    }
  }
  return log;
}
