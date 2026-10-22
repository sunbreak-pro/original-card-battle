#!/usr/bin/env node
// Golden data for Trial.Audio.Synth (the C# port of the browser trial's sound, Audio.js + the design system's Sound).
//
// Loads the JS sources read-only — `vendor/ds/bundle.js` (OCB.Sound, SOUND_DEFAULTS) and
// `src/scenes/audio/Audio.js` (createAudio, the meta presets, the ambience) — into a recording WebAudio mock at
// 48 kHz and writes, per preset, the graph-call log of three plays and the render length, plus the data tables (labels
// and numbers, byte for byte), the noise buffers and room impulses (head, tail, energy, FNV-1a of the Float32 bytes),
// the LCG / FNV seeds, the bus setup, a few plays with options, and one minute of every ambience. The C# tests replay
// the same calls and compare.
//
// Usage (no packages; Node 20+):
//   node unity-port/tools/audio-golden.mjs <browser-game folder> [out.json] [--chromium]
//   default out: unity-port/Trial.Audio.Synth.Tests/golden/audio-golden.json
//
// --chromium also renders every preset (and the surface steps and miasma lines) in headless Chromium's
// OfflineAudioContext at 48 kHz through the game's real graph (master 1, effects 1, reverb 0.18, the compressor; each
// sound played 0.5 s in, so the compressor has settled) and stores the max momentary loudness (BS.1770, 400 ms windows
// every 100 ms), the peak and a 20 ms RMS envelope. It needs playwright-core (PLAYWRIGHT_CORE, or the copy under the
// global @playwright/mcp) and a Chromium (CHROMIUM_PATH, or Playwright's headless shell). Without the flag, an existing
// "chromium" section of the output is kept as it is.
//
// Log entries (numbers rounded to 1e-6, as Audio.test.mjs does):
//   [id, "new"]                       a node is created (id = Kind+n, n counted from the first node of the segment;
//                                     nodes made before the segment carry their bus name: fx, fxWet, out, wet, …)
//   [id, "type", "bandpass"]          filter / oscillator type
//   [id, "buffer", "white"] / [id, "loop", true]
//   [id, "connect", dst]              dst = a node id, or "<node id>.<param>" for a param
//   [id, "disconnect"]
//   [id, param, "value", v] / [id, param, "set" | "lin" | "exp", v, t] / [id, param, "target", v, t, tc] / [id, param, "cancel", t]
//   [id, "start", t, offset] / [id, "stop", t]
import { existsSync, readFileSync, writeFileSync, mkdirSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import vm from 'node:vm';

const HERE = dirname(fileURLToPath(import.meta.url));
const argv = process.argv.slice(2);
const withChromium = argv.includes('--chromium');
const args = argv.filter((a) => !a.startsWith('--'));
if (!args[0]) {
  console.error('usage: node unity-port/tools/audio-golden.mjs <browser-game folder> [out.json] [--chromium]');
  process.exit(2);
}
const GAME = resolve(args[0]);
const OUT = resolve(args[1] || resolve(HERE, '../Trial.Audio.Synth.Tests/golden/audio-golden.json'));
const BUNDLE = resolve(GAME, 'vendor/ds/bundle.js');
const AUDIO = resolve(GAME, 'src/scenes/audio/Audio.js');
const SR = 48000;

const R = (x) => (typeof x === 'number' ? Math.round(x * 1e6) / 1e6 : x);

// ---------------------------------------------------------------------------------------------------
// A recording WebAudio mock (after Audio.test.mjs makeMock), logging node creation, types, buffers and
// connections as well as every param call. It throws where browsers throw.
// ---------------------------------------------------------------------------------------------------
function makeMock() {
  const shared = { contexts: [] };
  class Param {
    constructor(ctx, owner, name, value) { this.ctx = ctx; this.owner = owner; this.name = name; this._v = value; }
    get value() { return this._v; }
    set value(v) { this.check(v); this._v = v; this.log('value', v); }
    check(v) { if (typeof v !== 'number' || !Number.isFinite(v)) throw new TypeError(`${this.owner.id}.${this.name}: non-finite ${v}`); }
    time(t) { if (typeof t !== 'number' || !Number.isFinite(t) || t < 0) throw new RangeError(`${this.owner.id}.${this.name}: bad time ${t}`); }
    log(m, ...a) { this.ctx.log.push([this.owner.id, this.name, m, ...a.map(R)]); }
    setValueAtTime(v, t) { this.check(v); this.time(t); this._v = v; this.log('set', v, t); return this; }
    linearRampToValueAtTime(v, t) { this.check(v); this.time(t); this._v = v; this.log('lin', v, t); return this; }
    exponentialRampToValueAtTime(v, t) {
      this.check(v); this.time(t);
      if (v === 0) throw new RangeError(`${this.owner.id}.${this.name}: exponential ramp to 0`);
      this._v = v; this.log('exp', v, t); return this;
    }
    setTargetAtTime(v, t, tc) { this.check(v); this.time(t); this._v = v; this.log('target', v, t, tc); return this; }
    cancelScheduledValues(t) { this.time(t); this.log('cancel', t); return this; }
  }
  class Node {
    constructor(ctx, kind) {
      this.ctx = ctx; this.kind = kind; this.index = ctx.nodes.length; this.id = kind + '#' + this.index;
      ctx.nodes.push(this); this.outs = []; ctx.log.push([this.id, 'new']);
    }
    connect(dst) {
      if (!(dst instanceof Node) && !(dst instanceof Param)) throw new TypeError(`${this.id}.connect: not a node or param`);
      if (dst.ctx !== this.ctx) throw new Error('connect across contexts');
      this.outs.push(dst);
      this.ctx.log.push([this.id, 'connect', dst instanceof Param ? dst.owner.id + '.' + dst.name : dst.id]);
      return dst;
    }
    disconnect() { this.outs = []; this.ctx.log.push([this.id, 'disconnect']); }
  }
  function typed(node, initial, allowed) {
    let type = initial;
    Object.defineProperty(node, 'type', {
      get: () => type,
      set: (v) => { if (!allowed.includes(v)) throw new TypeError('bad type ' + v); type = v; node.ctx.log.push([node.id, 'type', v]); },
    });
  }
  class Source extends Node {
    constructor(ctx, kind) { super(ctx, kind); this.started = null; this.stopped = null; }
    start(t = 0, offset = 0) {
      if (this.started != null) throw new Error('InvalidStateError: start twice');
      if (!Number.isFinite(t) || t < 0) throw new RangeError('bad start ' + t);
      if (!Number.isFinite(offset) || offset < 0) throw new RangeError('bad offset ' + offset);
      if (this.kind === 'BufferSource' && !this.buffer) throw new Error('buffer source without buffer');
      this.started = t; this.ctx.started.push(this); this.ctx.log.push([this.id, 'start', R(t), R(offset)]);
    }
    stop(t = 0) {
      if (this.started == null) throw new Error('InvalidStateError: stop before start');
      if (!Number.isFinite(t) || t < 0) throw new RangeError('bad stop ' + t);
      this.stopped = t; this.ctx.log.push([this.id, 'stop', R(t)]);
    }
  }
  class Buffer {
    constructor(ctx, ch, len, sr) {
      if (!(len > 0) || !(sr > 0)) throw new RangeError('bad buffer');
      this.numberOfChannels = ch; this.length = len; this.sampleRate = sr; this.duration = len / sr;
      this.data = Array.from({ length: ch }, () => new Float32Array(len));
      this.name = 'buffer#' + ctx.buffers.length; ctx.buffers.push(this);
    }
    getChannelData(i) { return this.data[i]; }
  }
  class MockAudioContext {
    constructor() {
      this.sampleRate = SR; this.currentTime = 0; this.state = 'running';
      this.nodes = []; this.buffers = []; this.started = []; this.log = [];
      this.destination = new Node(this, 'Destination');
      shared.contexts.push(this);
    }
    createGain() { const n = new Node(this, 'Gain'); n.gain = new Param(this, n, 'gain', 1); return n; }
    createBiquadFilter() {
      const n = new Node(this, 'Biquad');
      typed(n, 'lowpass', ['lowpass', 'highpass', 'bandpass', 'lowshelf', 'highshelf', 'peaking', 'notch', 'allpass']);
      n.frequency = new Param(this, n, 'frequency', 350); n.Q = new Param(this, n, 'Q', 1); n.gain = new Param(this, n, 'gain', 0);
      return n;
    }
    createOscillator() {
      const n = new Source(this, 'Osc');
      typed(n, 'sine', ['sine', 'square', 'sawtooth', 'triangle']);
      n.frequency = new Param(this, n, 'frequency', 440); n.detune = new Param(this, n, 'detune', 0);
      return n;
    }
    createBufferSource() {
      const n = new Source(this, 'BufferSource');
      let buffer = null, loop = false;
      Object.defineProperty(n, 'buffer', { get: () => buffer, set: (b) => { buffer = b; this.log.push([n.id, 'buffer', b ? b.name : null]); } });
      Object.defineProperty(n, 'loop', { get: () => loop, set: (v) => { loop = v; this.log.push([n.id, 'loop', v]); } });
      n.playbackRate = new Param(this, n, 'playbackRate', 1);
      return n;
    }
    createBuffer(ch, len, sr) { return new Buffer(this, ch, len, sr); }
    createConvolver() {
      const n = new Node(this, 'Convolver');
      let buffer = null;
      Object.defineProperty(n, 'buffer', { get: () => buffer, set: (b) => { buffer = b; this.log.push([n.id, 'buffer', b ? b.name : null]); } });
      return n;
    }
    createDynamicsCompressor() {
      const n = new Node(this, 'Compressor');
      for (const [k, v] of [['threshold', -24], ['ratio', 12], ['knee', 30], ['attack', 0.003], ['release', 0.25]]) n[k] = new Param(this, n, k, v);
      return n;
    }
    createStereoPanner() { const n = new Node(this, 'Panner'); n.pan = new Param(this, n, 'pan', 0); return n; }
    resume() { if (this.state !== 'closed') this.state = 'running'; return Promise.resolve(); }
    suspend() { if (this.state !== 'closed') this.state = 'suspended'; return Promise.resolve(); }
    close() { this.state = 'closed'; return Promise.resolve(); }
  }
  return { MockAudioContext, shared };
}

// ---------------------------------------------------------------------------------------------------
// Loading
// ---------------------------------------------------------------------------------------------------
const bundleText = readFileSync(BUNDLE, 'utf8');
const audioText = readFileSync(AUDIO, 'utf8');
const M = await import(pathToFileURL(AUDIO).href);

function loadBundle(AudioContextClass) {
  const win = {};
  if (AudioContextClass) win.AudioContext = AudioContextClass;
  vm.runInNewContext(bundleText, { window: win, console });
  return win.OCB;
}

/** hashSeed is not exported: take its source text from Audio.js (read-only) and evaluate it alone. */
const hashSeed = (() => {
  const m = /function hashSeed\(str\) \{[\s\S]*?\n\}/.exec(audioText);
  if (!m) throw new Error('hashSeed not found in Audio.js');
  return new Function(m[0] + '\nreturn hashSeed;')();
})();

/** A fresh, unlocked createAudio over two mock contexts (the DS one is made first, by base.unlock()). */
function freshAudio() {
  const { MockAudioContext, shared } = makeMock();
  const OCB = loadBundle(MockAudioContext);
  const a = M.createAudio(OCB, {}, { AudioContext: MockAudioContext });
  if (!a.unlock()) throw new Error('unlock failed');
  const meta = a._ctx;
  const ds = shared.contexts.find((c) => c !== meta);
  if (!ds || shared.contexts.length !== 2) throw new Error('expected two contexts');
  nameBuffers(meta, ['room', 'cave', 'white', 'pink', 'brown']);
  nameBuffers(ds, ['room', 'dsNoise']);
  return { a, OCB, meta, ds, busNames: { meta: metaBuses(meta), ds: dsBuses(ds) } };
}

function nameBuffers(ctx, names) {
  if (ctx.buffers.length !== names.length) throw new Error(`expected ${names.length} buffers, got ${ctx.buffers.length}`);
  ctx.buffers.forEach((b, i) => { b.name = names[i]; });
}

// The buses made in ensure(), found by their wiring (not by creation index), named as in Audio.js / bundle.js.
const outsTo = (ctx, kind, target) => ctx.nodes.filter((n) => n.kind === kind && n.outs.includes(target));
function wetChain(ctx, target) {
  // Gain → Biquad → Convolver → target
  const conv = outsTo(ctx, 'Convolver', target);
  if (conv.length !== 1) throw new Error('convolver into ' + target.id);
  const lp = outsTo(ctx, 'Biquad', conv[0]);
  if (lp.length !== 1) throw new Error('low-pass into ' + conv[0].id);
  const g = outsTo(ctx, 'Gain', lp[0]);
  if (g.length !== 1) throw new Error('wet gain into ' + lp[0].id);
  return { gain: g[0], lp: lp[0], conv: conv[0] };
}
function metaBuses(ctx) {
  const comp = ctx.nodes.find((n) => n.kind === 'Compressor');
  const [master] = outsTo(ctx, 'Gain', comp);
  const intoMaster = outsTo(ctx, 'Gain', master); // fx, ambDuck in creation order
  const [fx, ambDuck] = intoMaster;
  const [amb] = outsTo(ctx, 'Gain', ambDuck);
  const fxw = wetChain(ctx, fx), ambw = wetChain(ctx, amb);
  return {
    [ctx.destination.id]: 'destination', [comp.id]: 'comp', [master.id]: 'master', [fx.id]: 'fx', [ambDuck.id]: 'ambDuck', [amb.id]: 'amb',
    [fxw.gain.id]: 'fxWet', [fxw.lp.id]: 'fxWetLp', [fxw.conv.id]: 'fxRoom', [ambw.gain.id]: 'ambWet', [ambw.lp.id]: 'ambWetLp', [ambw.conv.id]: 'cave',
  };
}
function dsBuses(ctx) {
  const comp = ctx.nodes.find((n) => n.kind === 'Compressor');
  const [out] = outsTo(ctx, 'Gain', comp);
  const w = wetChain(ctx, out);
  return { [ctx.destination.id]: 'destination', [comp.id]: 'comp', [out.id]: 'out', [w.gain.id]: 'wet', [w.lp.id]: 'wetLp', [w.conv.id]: 'room' };
}

/** A log segment: everything one scenario logged on one context, with node ids relative to its first new node. */
function segment(ctx, buses) {
  const logStart = ctx.log.length, nodeBase = ctx.nodes.length, startedBase = ctx.started.length;
  const mapId = (id) => {
    const m = /^(\w+)#(\d+)$/.exec(id);
    if (!m) return id;
    const k = Number(m[2]);
    if (k >= nodeBase) return m[1] + '+' + (k - nodeBase);
    return buses[id] || id;
  };
  const mapTarget = (s) => {
    const dot = s.indexOf('.');
    return dot < 0 ? mapId(s) : mapId(s.slice(0, dot)) + s.slice(dot);
  };
  return {
    log() {
      return ctx.log.slice(logStart).map((e) => {
        const out = e.slice();
        out[0] = mapId(out[0]);
        if (out[1] === 'connect') out[2] = mapTarget(out[2]);
        return out;
      });
    },
    nodes() { return ctx.nodes.length - nodeBase; },
    started() { return ctx.started.slice(startedBase); },
  };
}

/** The latest stop() among the sources one play started, relative to the play's time. */
function playEnd(startedSinceBefore, now) {
  let end = 0;
  for (const s of startedSinceBefore) if (s.stopped != null) end = Math.max(end, s.stopped - now);
  return end;
}

// ---------------------------------------------------------------------------------------------------
// Scenarios
// ---------------------------------------------------------------------------------------------------
const PLAY_TIMES = [0, 1, 2];

/** Three plays of one preset in a fresh instance, at currentTime 0, 1 and 2 s. */
function presetScenario(name, opts) {
  const f = freshAudio();
  const isBattle = f.a.battleNames.includes(name);
  const ctx = isBattle ? f.ds : f.meta;
  const seg = segment(ctx, isBattle ? f.busNames.ds : f.busNames.meta);
  const plays = [];
  for (const now of PLAY_TIMES) {
    ctx.currentTime = now;
    const before = ctx.started.length;
    const ok = opts ? f.a.play(name, 0, undefined, { ...opts }) : f.a.play(name);
    const end = playEnd(ctx.started.slice(before), now);
    plays.push({ now, ok, end: R(end), frames: Math.ceil(end * SR - 1e-7) + Math.floor(SR * 1.4) });
  }
  const out = { kind: isBattle ? 'ds' : 'meta', name, options: opts || null, plays, nodes: seg.nodes(), log: seg.log() };
  f.a.destroy();
  return out;
}

/** Calls with delays, mults, pans, pitchMul and the step plan, each in a fresh instance. */
function callScenario(label, calls) {
  const f = freshAudio();
  const metaSeg = segment(f.meta, f.busNames.meta), dsSeg = segment(f.ds, f.busNames.ds);
  const results = [];
  for (const c of calls) {
    f.meta.currentTime = c.now; f.ds.currentTime = c.now;
    let r;
    if (c.fn === 'play') r = f.a.play(...c.args);
    else if (c.fn === 'stepTo') r = f.a.stepTo(...c.args);
    else if (c.fn === 'miasmaLine') r = f.a.miasmaLine(...c.args);
    else throw new Error(c.fn);
    results.push(r);
  }
  const out = { label, calls, results, meta: { nodes: metaSeg.nodes(), log: metaSeg.log() }, ds: { nodes: dsSeg.nodes(), log: dsSeg.log() } };
  f.a.destroy();
  return out;
}

/** One place's ambience: setAmbience(id, 20) after unlock, then 60 s of scheduler ticks every 0.25 s. */
function ambienceScenario(id) {
  const f = freshAudio();
  const ctx = f.meta;
  const seg = segment(ctx, f.busNames.meta);
  const ok = f.a.setAmbience(id, 20);
  for (let t = 0; t < 60; t += 0.25) { ctx.currentTime += 0.25; f.a._tick(); }
  const layers = f.a._layers;
  const out = { id, fadeMs: 20, seconds: 60, tick: 0.25, ok, layers, nodes: seg.nodes(), log: seg.log() };
  f.a.destroy();
  return out;
}

// ---------------------------------------------------------------------------------------------------
// Buffers and seeds
// ---------------------------------------------------------------------------------------------------
function fnv32(f32) {
  const bytes = new Uint8Array(f32.buffer, f32.byteOffset, f32.byteLength);
  let h = 2166136261;
  for (let i = 0; i < bytes.length; i++) { h ^= bytes[i]; h = Math.imul(h, 16777619) >>> 0; }
  return h;
}
function describeBuffer(b) {
  const channels = [];
  for (let c = 0; c < b.numberOfChannels; c++) {
    const d = b.getChannelData(c);
    let energy = 0, peak = 0;
    for (let i = 0; i < d.length; i++) { energy += d[i] * d[i]; if (Math.abs(d[i]) > peak) peak = Math.abs(d[i]); }
    channels.push({ head: Array.from(d.slice(0, 64)), tail: Array.from(d.slice(d.length - 64)), energy, peak, fnv: fnv32(d) });
  }
  return { length: b.length, sampleRate: b.sampleRate, channels };
}

/** A module constant Audio.js does not export (`const NAME = <JSON literal>;`), read from its source text. */
function constantOf(name) {
  const m = new RegExp(`^const ${name} = ([^;]+);`, 'm').exec(audioText);
  if (!m) throw new Error(name + ' not found in Audio.js');
  return JSON.parse(m[1]);
}

/**
 * The data tables as the JS holds them (labels, numbers, key order), so the C# tables can be compared field by field:
 * the design system's TIMBRES / SOUND_DEFAULTS, Audio.js's META_TIMBRES / META_GROUPS / META_SOUND_DEFAULTS / SURFACES /
 * AMBIENCE_DEFS / SOUND_SETTINGS_DEFAULTS and its private constants, the app's DEFAULT_SETTINGS (src/app/save.js), and the
 * names the dev page plays (the regexes of test/audio/Audio.test.mjs:213-218).
 */
async function dataTables(OCB) {
  const save = await import(pathToFileURL(resolve(GAME, 'src/app/save.js')).href);
  const html = readFileSync(resolve(GAME, 'dev/audio.html'), 'utf8');
  const devPlays = new Set();
  for (const m of html.matchAll(/\b(?:play|has)\(\s*['"]([A-Za-z0-9]+)['"]/g)) devPlays.add(m[1]);
  for (const m of html.matchAll(/\bresult:\s*['"]([A-Za-z0-9]+)['"]/g)) devPlays.add(m[1]);
  const devAmbiences = new Set();
  for (const m of html.matchAll(/\bsetAmbience\(\s*['"]([A-Za-z0-9]+)['"]/g)) devAmbiences.add(m[1]);
  return {
    dsTimbres: OCB.TIMBRES,
    dsSounds: OCB.SOUND_DEFAULTS,
    metaTimbres: M.META_TIMBRES,
    metaGroups: M.META_GROUPS,
    metaSounds: M.META_SOUND_DEFAULTS,
    surfaces: M.SURFACES,
    ambienceIds: M.AMBIENCE_IDS,
    ambienceBuilderIds: M.AMBIENCE_BUILDER_IDS,
    ambienceDefs: M.AMBIENCE_DEFS,
    soundSettingsDefaults: M.SOUND_SETTINGS_DEFAULTS,
    appSettingsDefaults: save.DEFAULT_SETTINGS,
    constants: Object.fromEntries(['MIASMA_LINE_PITCH', 'RESULT_AFTER_STEP_MS', 'LOOKAHEAD_S', 'TICK_MS', 'NOISE_SECONDS'].map((k) => [k, constantOf(k)])),
    devPage: { plays: [...devPlays], ambiences: [...devAmbiences] },
  };
}

function seeds() {
  const lcg = {};
  for (const seed of [0, 1, 7, 11, 13, 23, 37, 2147483646, 2147483647, 123456789.7, -5]) {
    const r = M.lcg(seed);
    lcg[String(seed)] = Array.from({ length: 16 }, () => r());
  }
  const hash = {};
  for (const id of [...M.AMBIENCE_IDS, '', 'a', '継承の間']) hash[id] = hashSeed(id);
  const layerRnd = {};
  for (const id of M.AMBIENCE_IDS) { const r = M.lcg(hashSeed(id)); layerRnd[id] = Array.from({ length: 8 }, () => r()); }
  return { lcg, hashSeed: hash, layerRnd };
}

// ---------------------------------------------------------------------------------------------------
// Run
// ---------------------------------------------------------------------------------------------------
const probe = freshAudio();
const battleNames = probe.a.battleNames.slice();
const metaNames = probe.a.metaNames.slice();
const data = await dataTables(probe.OCB);
const buffers = {};
for (const b of probe.meta.buffers) buffers[b.name] = describeBuffer(b);
buffers.dsNoise = describeBuffer(probe.ds.buffers.find((b) => b.name === 'dsNoise'));
const dsRoom = describeBuffer(probe.ds.buffers.find((b) => b.name === 'room'));
if (JSON.stringify(dsRoom) !== JSON.stringify(buffers.room)) throw new Error('the DS room and the meta fx room differ');
const setup = {
  meta: segmentAll(probe.meta, probe.busNames.meta),
  ds: segmentAll(probe.ds, probe.busNames.ds),
};
probe.a.destroy();

function segmentAll(ctx, buses) {
  return ctx.log.map((e) => {
    const out = e.slice();
    out[0] = buses[out[0]] || out[0];
    if (out[1] === 'connect') { const [n, p] = out[2].split('.'); out[2] = (buses[n] || n) + (p ? '.' + p : ''); }
    // buffers are named after ensure() made them; the log caught their creation-order names
    if (out[1] === 'buffer') { const m = /^buffer#(\d+)$/.exec(out[2]); if (m) out[2] = ctx.buffers[Number(m[1])].name; }
    return out;
  });
}

const presets = [];
for (const name of battleNames) presets.push(presetScenario(name));
for (const name of metaNames) presets.push(presetScenario(name));

const surfaces = [];
for (const s of Object.keys(M.SURFACES)) for (const name of ['nodeStep', 'nodeReturn']) surfaces.push({ surface: s, ...presetScenario(name, { surface: s }) });

const calls = [
  callScenario('options', [
    { now: 0, fn: 'play', args: ['descend', 120, 0.5, { pan: -0.3 }] },
    { now: 1, fn: 'play', args: ['hitSlash', 150, 0.5] },
    { now: 2, fn: 'play', args: ['grab', 0, 0] },
    { now: 3, fn: 'play', args: ['uiClick', -40, 2] },
    { now: 4, fn: 'play', args: ['nodeHover', 0, undefined, { pitchMul: 0.5 }] },
    { now: 5, fn: 'play', args: ['nodeStep', 0, undefined, { pan: 0.4, surface: 'glass' }] },
  ]),
  callScenario('steps', [
    { now: 0, fn: 'stepTo', args: [{}] },
    { now: 1, fn: 'stepTo', args: [{ fresh: false }] },
    { now: 2, fn: 'stepTo', args: [{ line: 40 }] },
    { now: 6, fn: 'stepTo', args: [{ miasmaDeath: true, line: 20 }] },
    { now: 11, fn: 'stepTo', args: [{ result: 'infoGained' }] },
  ]),
  callScenario('miasmaLine', [
    { now: 0, fn: 'miasmaLine', args: [20] },
    { now: 3, fn: 'miasmaLine', args: [40, 100] },
    { now: 6, fn: 'miasmaLine', args: [60] },
    { now: 9, fn: 'miasmaLine', args: [80] },
    { now: 12, fn: 'miasmaLine', args: [100] },
  ]),
  // Times are whole render quanta (128 frames at 48 kHz), as a context's currentTime always is.
  callScenario('gaps', [
    { now: 0, fn: 'play', args: ['hover'] },
    { now: 0.024, fn: 'play', args: ['hover'] },
    { now: 0.064, fn: 'play', args: ['hover'] },
    { now: 0.08, fn: 'play', args: ['hover'] },
    { now: 0.104, fn: 'play', args: ['nodeHover'] },
    { now: 0.16, fn: 'play', args: ['nodeHover'] },
    { now: 0.168, fn: 'play', args: ['nodeHover'] },
    { now: 0.2, fn: 'play', args: ['uiClick'] },
    { now: 0.232, fn: 'play', args: ['uiClick'] },
    { now: 0.248, fn: 'play', args: ['uiClick'] },
  ]),
];

const ambience = M.AMBIENCE_IDS.map(ambienceScenario);

// ---------------------------------------------------------------------------------------------------
// Chromium reference renders (optional)
// ---------------------------------------------------------------------------------------------------
/** The renders: every preset, nodeStep / nodeReturn on every surface, the miasma lines, a panned and delayed call. */
function chromiumCases() {
  const cases = [];
  for (const name of [...battleNames, ...metaNames]) cases.push({ key: name, name });
  for (const s of Object.keys(M.SURFACES)) for (const name of ['nodeStep', 'nodeReturn']) cases.push({ key: name + ':' + s, name, opts: { surface: s } });
  for (const line of [40, 60, 80]) cases.push({ key: 'miasmaLine:' + line, name: 'miasmaLine', line });
  cases.push({ key: 'descend:pan', name: 'descend', mult: 0.5, opts: { pan: -0.3 } });
  return cases;
}

async function renderInChromium() {
  const require = createRequire(import.meta.url);
  const pwPath = process.env.PLAYWRIGHT_CORE || resolve(process.env.APPDATA || '', 'npm/node_modules/@playwright/mcp/node_modules/playwright-core');
  const { chromium } = require(pwPath);
  let exe = process.env.CHROMIUM_PATH;
  if (!exe) {
    const root = resolve(process.env.LOCALAPPDATA || '', 'ms-playwright');
    const shell = existsSync(root) ? readdirSync(root).filter((d) => d.startsWith('chromium_headless_shell-')).sort().pop() : null;
    if (shell) exe = resolve(root, shell, 'chrome-headless-shell-win64/chrome-headless-shell.exe');
  }
  const browser = await chromium.launch(exe ? { executablePath: exe } : {});
  const page = await browser.newPage();
  await page.setContent('<html><body></body></html>');
  await page.addScriptTag({ content: 'window.__made = []; class OAC extends OfflineAudioContext { constructor() { super(2, 48000 * 7, 48000); window.__made.push(this); } } window.AudioContext = OAC;' });
  await page.addScriptTag({ content: bundleText });
  await page.addScriptTag({ content: audioText.replace(/^export /gm, '') + '\nwindow.__createAudio = createAudio;' });
  const version = await browser.version();
  const out = { browser: version, sampleRate: SR, startSeconds: 0.5, blockMs: 20, settings: { master: 1, effects: 1, reverb: 0.18 }, renders: {} };
  for (const c of chromiumCases()) {
    out.renders[c.key] = await page.evaluate(async (c) => {
      window.__made = [];
      const a = window.__createAudio(window.OCB, { master: 1, effects: 1, ambience: 0.6, reverb: 0.18 }, { AudioContext: window.AudioContext });
      a.unlock();
      const [ds, meta] = window.__made; // the design system's context is made first (base.unlock())
      const battle = Object.keys(window.OCB.SOUND_DEFAULTS).includes(c.name);
      if (c.line) a.miasmaLine(c.line, 500);
      else a.play(c.name, 500, c.mult, c.opts);
      const buf = await (battle ? ds : meta).startRendering();
      const L = buf.getChannelData(0), R = buf.getChannelData(1), n = L.length;
      const pb = [1.53512485958697, -2.69169618940638, 1.19839281085285], pa = [1, -1.69065929318241, 0.73248077421585];
      const rb = [1, -2, 1], ra = [1, -1.99004745483398, 0.99007225036621];
      const cum = new Float64Array(n + 1);
      for (const x of [L, R]) {
        let x1 = 0, x2 = 0, y1 = 0, y2 = 0, u1 = 0, u2 = 0;
        for (let i = 0; i < n; i++) {
          const v = x[i];
          const y = pb[0] * v + pb[1] * x1 + pb[2] * x2 - pa[1] * y1 - pa[2] * y2;
          const u = rb[0] * y + rb[1] * y1 + rb[2] * y2 - ra[1] * u1 - ra[2] * u2;
          x2 = x1; x1 = v; y2 = y1; y1 = y; u2 = u1; u1 = u;
          cum[i + 1] += u * u;
        }
      }
      for (let i = 0; i < n; i++) cum[i + 1] += cum[i];
      let lufs = -Infinity, peak = 0;
      for (let s = 0; s + 19200 <= n; s += 4800) lufs = Math.max(lufs, -0.691 + 10 * Math.log10((cum[s + 19200] - cum[s]) / 19200));
      for (let i = 0; i < n; i++) peak = Math.max(peak, Math.abs(L[i]), Math.abs(R[i]));
      const blocks = [];
      for (let s = 0; s + 960 <= n; s += 960) {
        let e = 0;
        for (let i = s; i < s + 960; i++) e += L[i] * L[i] + R[i] * R[i];
        const db = 10 * Math.log10(e / (2 * 960));
        blocks.push(db > -120 ? Math.round(db * 100) / 100 : null);
      }
      while (blocks.length && blocks[blocks.length - 1] === null) blocks.pop();
      return { name: c.name, opts: c.opts || null, mult: c.mult ?? null, line: c.line ?? null, lufs: Math.round(lufs * 1000) / 1000, peakDb: Math.round(20 * Math.log10(peak) * 1000) / 1000, blocks };
    }, c);
    process.stdout.write('.');
  }
  process.stdout.write('\n');
  await browser.close();
  return out;
}

let chromiumSection = null;
if (withChromium) chromiumSection = await renderInChromium();
else if (existsSync(OUT)) {
  try { chromiumSection = JSON.parse(readFileSync(OUT, 'utf8')).chromium || null; } catch (e) { chromiumSection = null; }
}

const md5 = (s) => createHash('md5').update(s).digest('hex');
const golden = {
  generator: 'unity-port/tools/audio-golden.mjs',
  source: { bundle: 'vendor/ds/bundle.js', bundleMd5: md5(bundleText), audio: 'src/scenes/audio/Audio.js', audioMd5: md5(audioText) },
  sampleRate: SR,
  battleNames,
  metaNames,
  data,
  seeds: seeds(),
  buffers,
  setup,
  presets,
  surfaces,
  calls,
  ambience,
  ...(chromiumSection ? { chromium: chromiumSection } : {}),
};

// One log entry per line so a diff reads like the call sequence.
function pretty(v, indent = '') {
  const inner = indent + ' ';
  if (Array.isArray(v)) {
    if (v.every((x) => x === null || typeof x !== 'object')) return JSON.stringify(v);
    if (v.every((x) => Array.isArray(x) && x.every((y) => y === null || typeof y !== 'object'))) {
      return '[\n' + v.map((x) => inner + JSON.stringify(x)).join(',\n') + '\n' + indent + ']';
    }
    return '[\n' + v.map((x) => inner + pretty(x, inner)).join(',\n') + '\n' + indent + ']';
  }
  if (v && typeof v === 'object') {
    const keys = Object.keys(v);
    if (!keys.length) return '{}';
    return '{\n' + keys.map((k) => inner + JSON.stringify(k) + ': ' + pretty(v[k], inner)).join(',\n') + '\n' + indent + '}';
  }
  return v === undefined ? 'null' : JSON.stringify(v);
}

mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, pretty(golden) + '\n');
const count = (xs) => xs.reduce((n, x) => n + x.log.length, 0);
console.log(`wrote ${OUT}`);
console.log(`  ${presets.length} presets (${count(presets)} log entries), ${surfaces.length} surface steps, ${calls.length} call scenarios, ${ambience.length} ambiences (${count(ambience)} entries)`);
console.log(chromiumSection ? `  chromium: ${Object.keys(chromiumSection.renders).length} renders (${chromiumSection.browser})${withChromium ? '' : ', kept from the previous file'}` : '  chromium: none (run with --chromium)');
