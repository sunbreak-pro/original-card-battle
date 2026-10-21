// Shared by the backdrop tests: the C# oracle's `backdrop` suite (oracle/out/backdrop.json, written by
// oracle/src/Suites/Backdrop.cs) rebuilt with the JS port, case by case, and diffed with the parity
// harness's own serializer and comparer (test/parity/lib). Floats compare through Math.fround, doubles
// exactly, as the harness does.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { toOracleJson } from '../parity/lib/serialize.mjs';
import { expandRefs, firstDiff } from '../parity/lib/compare.mjs';

import { BackdropSpec, Rgba, Flicker } from '../../src/bridge/backdrop/BackdropSpec.js';
import { BackdropMotion } from '../../src/bridge/backdrop/BackdropMotion.js';
import { BackdropSwitch } from '../../src/bridge/backdrop/BackdropSwitch.js';
import { ExplorationBackdropSpec } from '../../src/bridge/backdrop/ExplorationBackdrop.js';
import { MiasmaLook } from '../../src/bridge/backdrop/MiasmaLook.js';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const suitePath = path.join(root, 'oracle', 'out', 'backdrop.json');
const shapesPath = path.join(root, 'oracle', 'out', 'shapes.json');

/** Why the oracle comparison cannot run, or null when it can. */
export const oracleMissing = fs.existsSync(suitePath) && fs.existsSync(shapesPath) ? null : 'oracle/out/backdrop.json not written';

let cache = null;
function load() {
  if (cache) return cache;
  const shapes = JSON.parse(fs.readFileSync(shapesPath, 'utf8'));
  const cases = JSON.parse(fs.readFileSync(suitePath, 'utf8'));
  cache = { shapes, cases: new Map(cases.map((c) => [c.id, c])) };
  return cache;
}

/** Every oracle case id, in file order. */
export function oracleIds() {
  return [...load().cases.keys()];
}

const Heights = [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.52, 0.54, 0.56, 0.6, 0.7, 0.8, 0.9, 1].map(Math.fround);
const Times = [0, 70, 140, 500, 1234.5, 3000, 7000, 12345];

/** Tables.Static(typeof(T)): the public static fields and properties in declaration order (functions left out). */
function Static(obj) {
  const m = {};
  for (const k of Object.keys(obj)) if (typeof obj[k] !== 'function') m[k] = obj[k];
  return m;
}

function table() {
  return {
    Static: Static(BackdropSpec),
    ExplorationStatic: Static(ExplorationBackdropSpec),
    MiasmaLookStatic: Static(MiasmaLook),
    MotionStatic: Static(BackdropMotion),
    Clamp: Array.from({ length: 11 }, (_, i) => BackdropSpec.Clamp(i - 1)),
    Hex: [Rgba.Hex('#ff1f4f'), Rgba.Hex('10141d', 0.5), Rgba.Bytes(157, 166, 186, 0.26)],
  };
}

function layer(l) {
  const b = BackdropSpec.For(l);
  const e = ExplorationBackdropSpec.For(l);
  return {
    For: b,
    SkyAt: Heights.map((y) => BackdropSpec.SkyAt(b, y)),
    HazeAlphaAt: b.Hazes.map((h) => Heights.map((y) => BackdropSpec.HazeAlphaAt(h, y))),
    BrightestAt: Heights.map((y) => BackdropSpec.BrightestAt(b, y)),
    Luminance: Heights.map((y) => BackdropSpec.Luminance(BackdropSpec.BrightestAt(b, y))),
    ReachRedContrast: BackdropSpec.ReachRedContrast(b),
    TileContrast: BackdropSpec.Contrast(BackdropSpec.ReachRed, b.Tile),
    FlickerAlpha: b.Hazes.map((h) => Times.map((ms) => BackdropMotion.FlickerAlpha(h.Flicker, ms))),
    Exploration: e,
    ExplorationBrightestAt: Heights.map((y) => ExplorationBackdropSpec.BrightestAt(e, y)),
    LowestContrastInk: ExplorationBackdropSpec.LowestContrast(e, ExplorationBackdropSpec.Ink),
    LowestContrastAmber: ExplorationBackdropSpec.LowestContrast(e, ExplorationBackdropSpec.Amber),
    PeakLuminance: ExplorationBackdropSpec.PeakLuminance(e),
    Mix: [0, 0.25, 0.5, 1].map((t) => BackdropSpec.Mix(b.Sky[0].Color, b.Tile, t)),
    Over: BackdropSpec.Over(b.Tile, b.Sky[0].Color),
  };
}

function miasma(p) {
  const next = [p, p + 1, p + 5, p + 10, p + 20, 100];
  const distances = [0, 0.3, 0.5, 0.8, 0.95, 1, 1.2].map(Math.fround);
  return {
    FillAt: MiasmaLook.FillAt(p),
    Next: next,
    Warning: next.map((n) => MiasmaLook.Warning(p, n)),
    AheadColor: next.map((n) => MiasmaLook.AheadColor(p, n)),
    ForecastMark: next.map((n) => MiasmaLook.ForecastMark(p, n)),
    BreathPeriodMs: MiasmaLook.BreathPeriodMs(p),
    Breath: Times.map((ms) => MiasmaLook.Breath(p, ms)),
    FillAlpha: Times.map((ms) => MiasmaLook.FillAlpha(p, ms)),
    ShadeAt: MiasmaLook.ShadeAt(p),
    ShadeAtTimes: Times.map((ms) => MiasmaLook.ShadeAt(p, ms)),
    Rise: Times.map((ms) => MiasmaLook.Rise(p, Math.min(100, p + 10), ms)),
    EdgeFalloff: distances.map((d) => MiasmaLook.EdgeFalloff(MiasmaLook.ShadeAt(p), d)),
    ShadeAlphaAt: distances.map((d) => MiasmaLook.ShadeAlphaAt(MiasmaLook.ShadeAt(p), d)),
    Shaded: [0, 0.5, 1].map((d) => MiasmaLook.Shaded(BackdropSpec.For(3).Tile, MiasmaLook.ShadeAt(p), d)),
  };
}

function motion() {
  const motes = BackdropSpec.For(7).Motes;
  const at = [];
  const alpha = [];
  for (let index = 0; index < 6; index++) {
    for (const ms of Times) {
      const { x, y } = BackdropMotion.MoteAt(motes, index, ms, 1920, 1080);
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
    Crossfade: [-10.0, 0, 100, 200, 399.9, 400, 1000].map((ms) => BackdropMotion.Crossfade(ms)),
    Hash: hash,
    Ticks: MiasmaLook.Ticks(),
    CornerDistance: [[0, 0], [0.5, 0.5], [1, 1], [0.25, 0.75]].map(([x, y]) => MiasmaLook.CornerDistance(x, y, Math.fround(16 / 9))),
    FlickerStill: BackdropMotion.FlickerAlpha(Flicker.Still, 1000),
    FlickerFlash: Times.map((ms) => BackdropMotion.FlickerAlpha(new Flicker(900, 0.6, true), ms)),
  };
}

function switchLog() {
  const sw = new BackdropSwitch();
  const log = [];
  for (const [l, now] of [[1, 0.0], [1, 50.0], [2, 100.0], [3, 200.0], [3, 300.0], [9, 1000.0]]) {
    const change = sw.Show(l, now);
    log.push({ Show: l, At: now, Change: change, Shown: sw.Shown, Leaving: sw.Leaving });
    for (const dt of [0.0, 150.0, 450.0]) {
      const t = sw.Tick(now + dt);
      log.push({ Tick: now + dt, T: t, Shown: sw.Shown, Leaving: sw.Leaving });
    }
  }
  return log;
}

/** The JS port's output for one oracle case id. */
export function compute(id) {
  if (id === 'table:BackdropSpec') return table();
  if (id.startsWith('layer:')) return layer(Number(id.slice(6)));
  if (id.startsWith('miasma:')) return miasma(Number(id.slice(7)));
  if (id === 'motion') return motion();
  if (id === 'switch') return switchLog();
  throw new Error(`unknown backdrop case ${id}`);
}

/** The first difference between the oracle's case and the port's, or null when they agree. */
export function diffCase(id) {
  const { shapes, cases } = load();
  const c = cases.get(id);
  if (!c) throw new Error(`no oracle case ${id}`);
  const oracle = expandRefs(c.output, c.defs);
  const js = toOracleJson(compute(id), shapes);
  return firstDiff(oracle, js, { shapes, floats: new Set(c.floats || []) });
}
