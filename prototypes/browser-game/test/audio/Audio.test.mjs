// Tests for src/scenes/audio/Audio.js: imports under Node, wraps OCB.Sound, every name used exists,
// every event and ambience runs against a strict WebAudio mock, determinism, settings.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import vm from 'node:vm';

const HERE = dirname(fileURLToPath(import.meta.url));
const GAME = resolve(HERE, '../..');
const SRC = resolve(GAME, 'src/scenes/audio/Audio.js');
const BUNDLE = resolve(GAME, 'vendor/ds/bundle.js');
const DEV = resolve(GAME, 'dev/audio.html');
const DOC = resolve(GAME, 'docs/AUDIO.md');

const M = await import(SRC);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---------------------------------------------------------------------------------------------------
// A strict WebAudio mock: throws where browsers throw, records warnings for silent mistakes, logs calls.
// ---------------------------------------------------------------------------------------------------
function makeMock() {
  const shared = { contexts: [] };
  class Param {
    constructor(ctx, owner, name, value, range) { this.ctx = ctx; this.owner = owner; this.name = name; this._v = value; this.range = range; this.events = []; }
    get value() { return this._v; }
    set value(v) { this.check(v, 'value'); this._v = v; this.log('value', v); }
    check(v, what) { if (typeof v !== 'number' || !Number.isFinite(v)) throw new TypeError(`${this.owner.kind}.${this.name}.${what}: non-finite ${v}`); this.inRange(v); }
    inRange(v) {
      if (this.range && (v < this.range[0] || v > this.range[1])) this.ctx.warn(`${this.owner.kind}.${this.name} out of range: ${v}`);
    }
    time(t, what) { if (typeof t !== 'number' || !Number.isFinite(t) || t < 0) throw new RangeError(`${this.owner.kind}.${this.name}.${what}: bad time ${t}`); }
    last() { return this.events.length ? this.events[this.events.length - 1].v : this._v; }
    log(m, ...a) { this.ctx.log.push([this.owner.id, this.name, m, ...a.map((x) => (typeof x === 'number' ? Math.round(x * 1e6) / 1e6 : x))]); }
    setValueAtTime(v, t) { this.check(v, 'setValueAtTime'); this.time(t, 'setValueAtTime'); this.events.push({ v, t }); this._v = v; this.log('set', v, t); return this; }
    linearRampToValueAtTime(v, t) { this.check(v, 'linearRamp'); this.time(t, 'linearRamp'); this.events.push({ v, t }); this._v = v; this.log('lin', v, t); return this; }
    exponentialRampToValueAtTime(v, t) {
      this.check(v, 'expRamp'); this.time(t, 'expRamp');
      if (v === 0) throw new RangeError(`${this.owner.kind}.${this.name}: exponential ramp to 0`);
      const prev = this.last();
      if (prev === 0 || Math.sign(prev) !== Math.sign(v)) this.ctx.warn(`${this.owner.kind}.${this.name}: exponential ramp from ${prev} to ${v} has no effect`);
      this.events.push({ v, t }); this._v = v; this.log('exp', v, t); return this;
    }
    setTargetAtTime(v, t, tc) {
      this.check(v, 'setTarget'); this.time(t, 'setTarget');
      if (typeof tc !== 'number' || !Number.isFinite(tc) || tc < 0) throw new RangeError('bad timeConstant ' + tc);
      this.events.push({ v, t }); this._v = v; this.log('target', v, t, tc); return this;
    }
    cancelScheduledValues(t) { this.time(t, 'cancel'); this.events = this.events.filter((e) => e.t < t); this.log('cancel', t); return this; }
  }
  class Node {
    constructor(ctx, kind) { this.ctx = ctx; this.kind = kind; this.id = kind + '#' + ctx.nodes.length; ctx.nodes.push(this); this.outs = []; }
    connect(dst) {
      if (!(dst instanceof Node) && !(dst instanceof Param)) throw new TypeError(`${this.kind}.connect: not a node or param`);
      if (dst.ctx !== this.ctx) throw new Error('connect across contexts');
      this.outs.push(dst); return dst;
    }
    disconnect() { this.outs = []; }
  }
  class Source extends Node {
    constructor(ctx, kind) { super(ctx, kind); this.started = null; this.stopped = null; }
    start(t = 0, offset = 0) {
      if (this.started != null) throw new Error('InvalidStateError: start twice');
      if (!Number.isFinite(t) || t < 0) throw new RangeError('bad start ' + t);
      if (!Number.isFinite(offset) || offset < 0) throw new RangeError('bad offset ' + offset);
      if (this.kind === 'BufferSource' && !this.buffer) throw new Error('buffer source without buffer');
      if (this.kind === 'BufferSource' && offset >= this.buffer.duration) this.ctx.warn('offset past the buffer');
      this.started = t; this.ctx.started.push(this); this.ctx.log.push([this.id, 'start', t, offset]);
    }
    stop(t = 0) {
      if (this.started == null) throw new Error('InvalidStateError: stop before start');
      if (!Number.isFinite(t) || t < 0) throw new RangeError('bad stop ' + t);
      if (t && t < this.started) this.ctx.warn('stop before start time');
      this.stopped = t; this.ctx.log.push([this.id, 'stop', t]);
    }
  }
  class Buffer {
    constructor(ch, len, sr) {
      if (!(len > 0) || !(sr > 0)) throw new RangeError('bad buffer');
      this.numberOfChannels = ch; this.length = len; this.sampleRate = sr; this.duration = len / sr;
      this.data = Array.from({ length: ch }, () => new Float32Array(len));
    }
    getChannelData(i) { return this.data[i]; }
  }
  class MockAudioContext {
    constructor() {
      this.sampleRate = 8000; this.currentTime = 0; this.state = 'running';
      this.nodes = []; this.started = []; this.warnings = []; this.log = [];
      this.destination = new Node(this, 'Destination');
      shared.contexts.push(this);
    }
    warn(w) { this.warnings.push(w); }
    get nyq() { return this.sampleRate / 2; }
    createGain() { const n = new Node(this, 'Gain'); n.gain = new Param(this, n, 'gain', 1); return n; }
    createBiquadFilter() {
      const n = new Node(this, 'Biquad'); let type = 'lowpass';
      Object.defineProperty(n, 'type', { get: () => type, set: (v) => { if (!['lowpass', 'highpass', 'bandpass', 'lowshelf', 'highshelf', 'peaking', 'notch', 'allpass'].includes(v)) throw new TypeError('bad filter type ' + v); type = v; } });
      n.frequency = new Param(this, n, 'frequency', 350, [1, 24000]); n.Q = new Param(this, n, 'Q', 1, [0.0001, 1000]); n.gain = new Param(this, n, 'gain', 0);
      return n;
    }
    createOscillator() {
      const n = new Source(this, 'Osc'); let type = 'sine';
      Object.defineProperty(n, 'type', { get: () => type, set: (v) => { if (!['sine', 'square', 'sawtooth', 'triangle'].includes(v)) throw new TypeError('bad osc type ' + v); type = v; } });
      n.frequency = new Param(this, n, 'frequency', 440, [0.001, 24000]); n.detune = new Param(this, n, 'detune', 0);
      return n;
    }
    createBufferSource() { const n = new Source(this, 'BufferSource'); n.buffer = null; n.loop = false; n.playbackRate = new Param(this, n, 'playbackRate', 1); return n; }
    createBuffer(ch, len, sr) { return new Buffer(ch, len, sr); }
    createConvolver() { const n = new Node(this, 'Convolver'); n.buffer = null; return n; }
    createDynamicsCompressor() {
      const n = new Node(this, 'Compressor');
      for (const [k, v] of [['threshold', -24], ['ratio', 12], ['knee', 30], ['attack', 0.003], ['release', 0.25]]) n[k] = new Param(this, n, k, v);
      return n;
    }
    createStereoPanner() { const n = new Node(this, 'Panner'); n.pan = new Param(this, n, 'pan', 0, [-1, 1]); return n; }
    resume() { if (this.state !== 'closed') this.state = 'running'; return Promise.resolve(); }
    suspend() { if (this.state !== 'closed') this.state = 'suspended'; return Promise.resolve(); }
    close() { this.state = 'closed'; return Promise.resolve(); }
  }
  return { MockAudioContext, shared };
}

function loadBundle(AudioContextClass) {
  const win = {};
  if (AudioContextClass) win.AudioContext = AudioContextClass;
  vm.runInNewContext(readFileSync(BUNDLE, 'utf8'), { window: win, console });
  return win.OCB;
}

/** Advance a mock context's clock in small steps, running the ambience scheduler. */
function advance(audio, ctx, seconds, step = 0.25) {
  for (let t = 0; t < seconds; t += step) { ctx.currentTime += step; audio._tick(); }
}

// ---------------------------------------------------------------------------------------------------

test('imports under Node without window or AudioContext, and stays silent', () => {
  assert.equal(typeof globalThis.window, 'undefined');
  assert.equal(typeof globalThis.AudioContext, 'undefined');
  const a = M.createAudio();
  for (const k of ['play', 'unlock', 'setMaster', 'setReverb', 'reset', 'setAmbience', 'setEffects', 'setAmbienceVolume', 'setMuted', 'stepTo', 'miasmaLine', 'onChange', 'destroy']) {
    assert.equal(typeof a[k], 'function', k);
  }
  assert.equal(a.play('nodeStep'), false, 'silent before unlock');
  assert.equal(a.unlock(), false, 'no AudioContext: unlock reports false');
  assert.equal(a.play('nodeStep'), false);
  assert.equal(a.setAmbience(1), true);
  assert.equal(a.ambience, 'layer1');
  assert.equal(a.surface, 'litter');
  assert.equal(a.setAmbience('nowhere'), false);
  assert.equal(a.ambience, 'layer1');
  assert.equal(a.setAmbience(null), true);
  assert.equal(a.ambience, null);
  assert.deepEqual(a.battleNames, []);
  a.destroy();
});

test('wraps the real OCB.Sound and keeps its battle presets and API', () => {
  const OCB = loadBundle();
  const a = M.createAudio(OCB);
  const battle = Object.keys(OCB.SOUND_DEFAULTS);
  assert.equal(battle.length, 29);
  assert.deepEqual([...a.battleNames].sort(), [...battle].sort());
  for (const k of battle) assert.deepEqual(a.presets[k], OCB.SOUND_DEFAULTS[k], k);
  for (const k of a.metaNames) assert.ok(a.presets[k], 'meta preset merged: ' + k);
  for (const k of a.metaNames) assert.ok(!battle.includes(k), 'meta name collides with a battle name: ' + k);
  assert.equal(a.presets, a.base.presets, 'one live presets map');
  for (const k of ['play', 'unlock', 'setMaster', 'setReverb', 'reset']) assert.equal(typeof a.base[k], 'function');
  assert.equal(typeof a.muted, 'boolean');
  a.presets.nodeStep.vol = 0.5; a.presets.hitSlash.vol = 0.5;
  a.reset();
  assert.equal(a.presets.nodeStep.vol, M.META_SOUND_DEFAULTS.nodeStep.vol);
  assert.equal(a.presets.hitSlash.vol, OCB.SOUND_DEFAULTS.hitSlash.vol);
  // Assigning a saved map keeps its tuned values and fills only what is missing.
  const saved = JSON.parse(JSON.stringify(a.presets)); saved.nodeStep.vol = 0.3; delete saved.uiClick;
  a.presets = saved;
  assert.equal(a.presets.nodeStep.vol, 0.3);
  assert.ok(a.presets.uiClick);
  // effects volume scales the battle synth's master
  a.setMaster(0.5); a.setEffects(0.5);
  assert.equal(a.base.master, 0.25);
  a.setMuted(true);
  assert.equal(a.base.muted, true);
  a.destroy();
});

test('every preset name, timbre, surface and ambience used exists', () => {
  const a = M.createAudio(loadBundle());
  const timbres = new Set(a.timbres);
  for (const [k, p] of Object.entries(M.META_SOUND_DEFAULTS)) {
    assert.ok(timbres.has(p.timbre), `${k}: timbre ${p.timbre} not implemented`);
    assert.ok(M.META_TIMBRES[p.timbre], `${k}: timbre ${p.timbre} has no label`);
    assert.ok(M.META_GROUPS[p.group], `${k}: group ${p.group}`);
    for (const f of ['vol', 'pitch', 'dur', 'tone']) assert.ok(Number.isFinite(p[f]) && p[f] > 0, `${k}.${f}`);
    assert.ok(p.vol <= 0.3, `${k}: louder than the loudest battle hit`);
    assert.ok(p.label && p.role && p.spec, k);
  }
  for (const t of Object.keys(M.META_TIMBRES)) assert.ok(timbres.has(t), `labelled timbre ${t} is not implemented`);
  assert.deepEqual(M.AMBIENCE_IDS, ['title', 'hall', 'interlude', 'layer1', 'layer2', 'layer3', 'layer4', 'layer5', 'layer6', 'layer7']);
  assert.deepEqual([...M.AMBIENCE_BUILDER_IDS].sort(), [...M.AMBIENCE_IDS].sort());
  for (const [id, d] of Object.entries(M.AMBIENCE_DEFS)) assert.ok(M.SURFACES[d.surface], `${id}: surface ${d.surface}`);
  for (let n = 1; n <= 7; n++) assert.equal(M.ambienceId(n), 'layer' + n);
  // stepPlan only ever names presets that exist, and never more than two
  for (const fresh of [true, false]) for (const line of [0, 20, 40, 60, 80]) for (const miasmaDeath of [false, true]) for (const result of [null, 'infoGained', 'eventResolve', 'traceTake']) {
    const plan = M.stepPlan({ fresh, line, miasmaDeath, result });
    assert.ok(plan.length >= 1 && plan.length <= 2);
    for (const n of plan) assert.ok(a.has(n), n);
  }
  // names used by the dev page
  const html = readFileSync(DEV, 'utf8');
  const used = new Set();
  for (const m of html.matchAll(/\b(?:play|has)\(\s*['"]([A-Za-z0-9]+)['"]/g)) used.add(m[1]);
  for (const m of html.matchAll(/\bresult:\s*['"]([A-Za-z0-9]+)['"]/g)) used.add(m[1]);
  assert.ok(used.size > 5, 'dev page names found');
  for (const n of used) assert.ok(a.has(n), 'dev page plays unknown preset ' + n);
  const amb = new Set();
  for (const m of html.matchAll(/\bsetAmbience\(\s*['"]([A-Za-z0-9]+)['"]/g)) amb.add(m[1]);
  for (const n of amb) assert.notEqual(M.ambienceId(n), undefined, 'dev page uses unknown ambience ' + n);
  // the doc's tables cover every event and ambience, and name nothing else
  const doc = readFileSync(DOC, 'utf8');
  const rows = doc.split('\n').filter((l) => l.startsWith('|'));
  const named = new Set();
  for (const r of rows) { const m = r.match(/^\|\s*[^|]*?`([A-Za-z0-9]+)`/); if (m) named.add(m[1]); }
  for (const k of a.metaNames) assert.ok(named.has(k), 'AUDIO.md misses event ' + k);
  for (const k of M.AMBIENCE_IDS) assert.ok(named.has(k), 'AUDIO.md misses ambience ' + k);
  for (const k of named) assert.ok(a.has(k) || M.AMBIENCE_DEFS[k] || M.SURFACES[k] || M.META_TIMBRES[k], 'AUDIO.md names unknown ' + k);
  a.destroy();
});

test('every event and every ambience runs against a strict WebAudio mock', async () => {
  const { MockAudioContext, shared } = makeMock();
  const OCB = loadBundle(MockAudioContext);
  const a = M.createAudio(OCB, { ambience: 0.5 }, { AudioContext: MockAudioContext });
  a.setAmbience('title');
  assert.equal(a.unlock(), true);
  assert.equal(shared.contexts.length, 2, 'own context + the battle synth context');
  const ctx = a._ctx, bctx = shared.contexts.find((c) => c !== ctx);
  assert.equal(a._layers.length, 1, 'pending ambience starts on unlock');
  for (const name of a.metaNames) {
    ctx.currentTime += 0.5;
    assert.equal(a.play(name), true, name);
  }
  for (const name of a.battleNames) {
    assert.equal(a.play(name), true, name);
  }
  assert.ok(bctx.started.length > 29, 'battle synth rendered into its own context');
  for (const s of [{}, { fresh: false }, { line: 40 }, { miasmaDeath: true }, { result: 'infoGained' }]) { ctx.currentTime += 0.5; a.stepTo(s); }
  for (const l of [20, 40, 60, 80]) assert.equal(a.miasmaLine(l), true);
  a.play('descend', 120, 0.5, { pan: -0.3 });
  // each ambience: let it run for a minute; every layer must schedule at least one sparse event
  for (const id of M.AMBIENCE_IDS) {
    assert.equal(a.setAmbience(id, 20), true);
    advance(a, ctx, 60);
    const live = a._layers.filter((L) => !L.stopping);
    assert.equal(live.length, 1, id);
    assert.equal(live[0].id, id);
    assert.ok(live[0].nodes > 0, id + ': beds and drones');
    assert.ok(live[0].sparse.length > 0 && live[0].sparse.every((k) => k > 0), id + ': sparse events fired ' + live[0].sparse);
    await sleep(200);
    assert.equal(a._layers.length, 1, id + ': the previous layer was released after its fade');
  }
  assert.equal(a._errors, 0, 'no voice threw');
  assert.deepEqual(ctx.warnings, [], 'own context warnings');
  assert.deepEqual(bctx.warnings, [], 'battle context warnings');
  // every started source is stopped once the ambience is cleared
  a.setAmbience(null, 20);
  await sleep(220);
  assert.equal(a._layers.length, 0);
  const unstopped = ctx.started.filter((s) => s.stopped == null);
  assert.deepEqual(unstopped.map((s) => s.id), [], 'sources left running');
  // the scheduler never produced NaN times
  for (const e of ctx.log) for (const x of e) if (typeof x === 'number') assert.ok(Number.isFinite(x), JSON.stringify(e));
  a.destroy();
  assert.equal(ctx.state, 'closed');
});

test('is deterministic: the same calls give the same WebAudio calls', () => {
  function run() {
    const { MockAudioContext } = makeMock();
    const a = M.createAudio(undefined, {}, { AudioContext: MockAudioContext });
    a.unlock();
    const ctx = a._ctx;
    a.setAmbience(3, 500);
    for (let i = 0; i < 6; i++) { ctx.currentTime += 0.4; a.stepTo({ fresh: i % 3 !== 2, line: i === 4 ? 40 : 0 }); }
    for (const n of ['nodeHover', 'journalOpen', 'traceTake', 'heirNamed', 'miasmaDeath']) { ctx.currentTime += 0.3; a.play(n); }
    advance(a, ctx, 45);
    a.setAmbience(6, 500);
    advance(a, ctx, 45);
    const log = JSON.stringify(ctx.log);
    const sample = Array.from(ctx.started.find((s) => s.kind === 'BufferSource').buffer.getChannelData(0).slice(0, 64));
    a.destroy();
    return { log, sample };
  }
  const r1 = run(), r2 = run();
  assert.ok(r1.log.length > 10000);
  assert.equal(r1.log, r2.log);
  assert.deepEqual(r1.sample, r2.sample);
  const src = readFileSync(SRC, 'utf8');
  assert.ok(!/Math\.random/.test(src), 'no Math.random in Audio.js');
});

test('settings: volumes, mute, persistence callbacks', async () => {
  assert.deepEqual(M.normalizeSettings(null), M.SOUND_SETTINGS_DEFAULTS);
  assert.deepEqual(M.normalizeSettings({ master: 3, effects: -1, ambience: 'x', reverb: 0.3, muted: 1 }), { master: 1, effects: 0, ambience: M.SOUND_SETTINGS_DEFAULTS.ambience, reverb: 0.3, muted: false });
  const { MockAudioContext } = makeMock();
  const seen = [];
  const a = M.createAudio(undefined, { master: 0.6, ambience: 0.4, muted: false }, { AudioContext: MockAudioContext, onChange: (s) => seen.push(s) });
  assert.deepEqual(a.settings, { master: 0.6, effects: 1, ambience: 0.4, reverb: 0.18, muted: false });
  a.unlock();
  const ctx = a._ctx;
  const off = a.onChange((s) => seen.push({ second: s.ambience }));
  a.setAmbienceVolume(0.2);
  assert.deepEqual(seen.slice(-2), [{ master: 0.6, effects: 1, ambience: 0.2, reverb: 0.18, muted: false }, { second: 0.2 }]);
  off();
  a.setEffects(0.5);
  assert.equal(seen.length, 3);
  a.setMuted(true);
  assert.equal(a.muted, true);
  assert.equal(a.play('uiClick'), false, 'muted: no sound');
  const masterGain = ctx.nodes.find((n) => n.kind === 'Gain' && n.outs.some((o) => o.kind === 'Compressor'));
  assert.equal(masterGain.gain.events.at(-1).v, 0, 'master fades to 0');
  await sleep(450);
  assert.equal(ctx.state, 'suspended', 'a muted graph stops rendering');
  a.muted = false;
  assert.equal(ctx.state, 'running');
  assert.equal(masterGain.gain.events.at(-1).v, 0.6);
  assert.equal(a.play('uiClick'), true);
  a.applySettings({ master: 0.9, muted: true });
  assert.deepEqual(a.settings, { master: 0.9, effects: 0.5, ambience: 0.2, reverb: 0.18, muted: true });
  assert.equal(a.toggleMute(), false);
  // hover is throttled; clicks within 40 ms are merged
  assert.equal(a.play('nodeHover'), true);
  assert.equal(a.play('nodeHover'), false);
  ctx.currentTime += 0.1;
  assert.equal(a.play('nodeHover'), true);
  a.destroy();
});

test('crossfade: the old place fades while the new one rises; footsteps follow the place', async () => {
  const { MockAudioContext } = makeMock();
  const a = M.createAudio(undefined, {}, { AudioContext: MockAudioContext });
  a.unlock();
  a.setAmbience('layer1', 30);
  assert.equal(a.surface, 'litter');
  assert.equal(a.setAmbience('layer1'), true);
  assert.equal(a._layers.length, 1, 'same place: no restart');
  a.setAmbience('layer2', 30);
  assert.equal(a.surface, 'sand');
  assert.deepEqual(a._layers.map((L) => [L.id, L.stopping]), [['layer1', true], ['layer2', false]]);
  await sleep(250);
  assert.deepEqual(a._layers.map((L) => L.id), ['layer2']);
  a.duckAmbience(0.4, 300);
  a.destroy();
  assert.equal(a.play('nodeStep'), false, 'destroyed: silent');
});
