// The game's sound. Wraps the design system's battle synth (OCB.Sound) and adds the exploration and
// meta events and a quiet ambience per place, all synthesised with WebAudio (no audio files).
//
// Rules carried over from the design system ("音", ds/project/sound.md; battle_ui_ux_v2.md §5.20):
//  - One named preset per event: { label, timbre, vol, pitch (Hz), dur (ms), tone (0-1 brightness) }.
//  - At most two sounds per action: the action sound and the result sound. A composite timbre has at
//    most two parts, and stepTo() keeps a single result after the step.
//  - Dark, quiet, damp. No fanfare, no bright electronic beeps. The only high sounds are paper and
//    small lights.
//  - No randomness. Repeats alternate by a counter; noise buffers and ambience timing come from a
//    seeded LCG, so every run sounds the same.
// Ambience: one very quiet procedural loop per place: the seven layers of world-v1.md §4, the title,
// the hall of heirs (継承の間) and the interlude (階層間の休憩). It starts only after unlock() (a user
// gesture), crossfades on setAmbience(), and obeys mute, master and its own volume.

// ---------------------------------------------------------------------------------------------------
// Data
// ---------------------------------------------------------------------------------------------------

/** Timbres of the meta synth (the battle timbres stay in OCB.TIMBRES). Labels are UI text. */
export const META_TIMBRES = {
  paper: '紙（擦れ）',
  slide: '紙の束（滑り）',
  wood: '木の小さな音',
  seal: '印（判）',
  step: '足音（層ごとの地面）',
  page: '頁をめくる',
  close: '本を閉じる',
  quill: '筆の掠れ',
  exhale: '息を吐く',
  drill: '素振りと打ち込み',
  settle: '置いて収まる',
  ember: '灯の温み',
  pouch: '袋を開けて使う',
  swell: '低いうねり',
  petrify: '体が固まる',
  toll: '湿った鐘',
  hum: '低い和音',
  fall: '落ちる低い弦',
  tuck: '札を差し込む',
  expire: '最後の鼓動',
  relief: '安堵の息と灯',
  kindle: '灯をともす',
  nameSeal: '名を押す（判と弦）',
  bow: '擦った低い弦'
};

/** Groups of the meta events, in display order. */
export const META_GROUPS = { explore: '探索', reward: '報酬', life: '生と継承', ui: '操作' };

/**
 * Default presets of the exploration and meta events. Same shape as OCB.SOUND_DEFAULTS, plus `group`
 * and an optional `gap` (minimum ms between two plays of the same event).
 */
export const META_SOUND_DEFAULTS = {
  nodeStep:      { label: '新しいノードへ進む', group: 'explore', spec: '指定なし', role: '動作', timbre: 'step', vol: 0.22, pitch: 110, dur: 260, tone: 0.5 },
  nodeReturn:    { label: '踏んだノードへ戻る', group: 'explore', spec: '指定なし', role: '動作', timbre: 'step', vol: 0.11, pitch: 120, dur: 180, tone: 0.3 },
  nodeHover:     { label: 'ノードにホバー', group: 'explore', spec: '指定なし', role: '—', timbre: 'paper', vol: 0.035, pitch: 2600, dur: 22, tone: 0.4, gap: 60 },
  journalOpen:   { label: '手記を開く', group: 'explore', spec: '指定なし', role: '—', timbre: 'page', vol: 0.12, pitch: 1500, dur: 280, tone: 0.5 },
  journalClose:  { label: '手記を閉じる', group: 'explore', spec: '指定なし', role: '—', timbre: 'close', vol: 0.10, pitch: 1000, dur: 200, tone: 0.3 },
  rest:          { label: '休む', group: 'explore', spec: '指定なし', role: '結果', timbre: 'exhale', vol: 0.15, pitch: 760, dur: 1200, tone: 0.3 },
  train:         { label: '訓練する', group: 'explore', spec: '指定なし', role: '動作＋結果', timbre: 'drill', vol: 0.16, pitch: 900, dur: 170, tone: 0.5 },
  infoGained:    { label: '情報を得る', group: 'explore', spec: '指定なし', role: '結果', timbre: 'quill', vol: 0.13, pitch: 2300, dur: 460, tone: 0.4 },
  eventResolve:  { label: 'イベントが決まる', group: 'explore', spec: '指定なし', role: '結果', timbre: 'settle', vol: 0.12, pitch: 210, dur: 520, tone: 0.4 },
  traceTake:     { label: '痕跡を拾う（遺産）', group: 'explore', spec: '灯芯の 1 点', role: '結果', timbre: 'ember', vol: 0.085, pitch: 523.25, dur: 1700, tone: 0.4 },
  consumableUse: { label: '消耗品を使う', group: 'explore', spec: '指定なし', role: '動作＋結果', timbre: 'pouch', vol: 0.12, pitch: 1200, dur: 260, tone: 0.4 },
  miasmaLine:    { label: '瘴気が 20% の線を越える', group: 'explore', spec: '低いうねり', role: '結果', timbre: 'swell', vol: 0.11, pitch: 55, dur: 2400, tone: 0.3 },
  miasmaDeath:   { label: '瘴気が 100% に達する', group: 'explore', spec: '指定なし', role: '結果', timbre: 'petrify', vol: 0.14, pitch: 49, dur: 3400, tone: 0.3 },
  layerCleared:  { label: '層の主を倒した', group: 'explore', spec: 'ファンファーレなし', role: '結果', timbre: 'toll', vol: 0.12, pitch: 98, dur: 3600, tone: 0.3 },
  interlude:     { label: '階層間の休憩に入る', group: 'explore', spec: '指定なし', role: '—', timbre: 'hum', vol: 0.05, pitch: 110, dur: 2600, tone: 0.3 },
  descend:       { label: '次の層へ降りる', group: 'explore', spec: '低い弦（落ちる）', role: '動作', timbre: 'fall', vol: 0.12, pitch: 98, dur: 2400, tone: 0.35 },
  rewardReveal:  { label: '報酬が並ぶ', group: 'reward', spec: '指定なし', role: '—', timbre: 'slide', vol: 0.11, pitch: 1000, dur: 260, tone: 0.4 },
  rewardPick:    { label: '報酬を選ぶ', group: 'reward', spec: '指定なし', role: '動作', timbre: 'paper', vol: 0.15, pitch: 1250, dur: 90, tone: 0.4 },
  cardAdded:     { label: 'デッキに札が入る', group: 'reward', spec: '指定なし', role: '結果', timbre: 'tuck', vol: 0.12, pitch: 1100, dur: 220, tone: 0.4 },
  lifeEndDeath:  { label: '倒れて生が終わる', group: 'life', spec: '指定なし', role: '—', timbre: 'expire', vol: 0.15, pitch: 55, dur: 4400, tone: 0.25 },
  survived:      { label: '生きたまま生を閉じる', group: 'life', spec: '指定なし', role: '—', timbre: 'relief', vol: 0.12, pitch: 440, dur: 2200, tone: 0.35 },
  hallArrive:    { label: '継承の間に着く', group: 'life', spec: '琥珀の灯り', role: '—', timbre: 'kindle', vol: 0.12, pitch: 2400, dur: 1500, tone: 0.4 },
  heirNamed:     { label: '襲名', group: 'life', spec: '指定なし', role: '動作＋結果', timbre: 'nameSeal', vol: 0.18, pitch: 98, dur: 1500, tone: 0.4 },
  titleEnter:    { label: 'タイトルから入る', group: 'life', spec: '指定なし', role: '—', timbre: 'bow', vol: 0.075, pitch: 73.42, dur: 2800, tone: 0.3 },
  uiClick:       { label: 'ボタンを押す', group: 'ui', spec: '指定なし', role: '—', timbre: 'wood', vol: 0.07, pitch: 760, dur: 30, tone: 0.4, gap: 40 },
  uiConfirm:     { label: '決める', group: 'ui', spec: '指定なし', role: '—', timbre: 'seal', vol: 0.10, pitch: 170, dur: 110, tone: 0.4 },
  uiCancel:      { label: 'やめる・戻る', group: 'ui', spec: '指定なし', role: '—', timbre: 'slide', vol: 0.08, pitch: 620, dur: 110, tone: 0.2 }
};

/** The ground under the explorer's feet, per place (used by the `step` timbre). */
export const SURFACES = {
  stone:  { label: '石', thud: 1.0, heel: 0.8, type: 'bandpass', f: 900, q: 0.8, scuff: 0.45 },
  litter: { label: '落ち葉', thud: 0.85, heel: 0.6, type: 'bandpass', f: 2600, q: 0.6, scuff: 0.55, crackle: 0.45 },
  sand:   { label: '石畳の砂', thud: 1.05, heel: 0.7, type: 'highpass', f: 2800, q: 0.7, scuff: 0.5 },
  damp:   { label: '湿った岩', thud: 0.9, heel: 0.9, type: 'lowpass', f: 650, q: 1.2, scuff: 0.5, wet: 0.4 },
  glass:  { label: '結晶の床', thud: 1.25, heel: 0.7, type: 'bandpass', f: 1500, q: 1.0, scuff: 0.3, ring: 0.16 },
  salt:   { label: '塩の殻', thud: 1.0, heel: 0.7, type: 'bandpass', f: 1900, q: 1.3, scuff: 0.5, crackle: 0.65 },
  gravel: { label: '砂利', thud: 0.95, heel: 0.8, type: 'bandpass', f: 1300, q: 0.9, scuff: 0.6, crackle: 0.35 },
  void:   { label: '床が無い', thud: 0.65, heel: 0.6, type: 'lowpass', f: 320, q: 0.7, scuff: 0.3 }
};

/**
 * The places that have an ambience. `level` is the layer's peak gain (before the ambience volume),
 * `room` the send into the damp cave reverb, `surface` the ground for footsteps.
 */
export const AMBIENCE_DEFS = {
  title:     { label: 'タイトル', place: '住処の口', surface: 'stone', level: 0.5, room: 0.5, desc: '住処の口へ吸い込まれる風、低い D の響き、まれに遠い弦' },
  hall:      { label: '継承の間', place: '継承の間', surface: 'stone', level: 0.7, room: 0.3, desc: '動かない部屋の空気、灯の炎の息、温かい五度、まれに梁の軋み' },
  interlude: { label: '階層間の休憩', place: '階層間', surface: 'stone', level: 0.64, room: 0.45, desc: 'ゆっくり呼吸する空気、低い A と E、まれに柔らかい音' },
  layer1:    { label: '第 1 層', place: '燦光の樹海', surface: 'litter', level: 1.0, room: 0.25, desc: '天井の大穴から入る明るい空気、風に揺れる葉、小枝、光の粒' },
  layer2:    { label: '第 2 層', place: '■■の秘跡', surface: 'sand', level: 0.87, room: 0.55, desc: '柱列を抜ける風の共鳴、流れる砂、石の低い唸り、芽を出す結晶' },
  layer3:    { label: '第 3 層', place: '大黒蛇の住処', surface: 'damp', level: 0.5, room: 0.7, desc: '大蛇の呼吸に合わせて膨らむ低い空気、湿った雫、とぐろが壁を擦る' },
  layer4:    { label: '第 4 層', place: '輝晶の間', surface: 'glass', level: 1.2, room: 0.6, desc: '結晶のかすかな唸りの重なり、光の薄い擦れ、ゆっくり膨らむ結晶の音' },
  layer5:    { label: '第 5 層', place: '血染めの死海', surface: 'salt', level: 0.81, room: 0.3, desc: '干上がった海床の乾いた風、肋骨を抜ける笛のような風、塩の爆ぜ、遠い唸り' },
  layer6:    { label: '第 6 層', place: '獄雷峡', surface: 'gravel', level: 0.86, room: 0.5, desc: '谷を下る風、崖を走る獄雷の爆ぜと遠い雷鳴、黒く低い響き' },
  layer7:    { label: '第 7 層', place: '果てなき世界', surface: 'void', level: 0.57, room: 0.8, desc: '空洞の圧、うなりを持つ低い二音、湧き口の上下、沈んでいく瘴気の息' }
};
export const AMBIENCE_IDS = Object.keys(AMBIENCE_DEFS);

export const SOUND_SETTINGS_DEFAULTS = { master: 0.8, effects: 1.0, ambience: 0.6, reverb: 0.18, muted: false };

const MIASMA_LINE_PITCH = [1, 0.891, 0.794, 0.707]; // 20 / 40 / 60 / 80 %: a whole tone lower per line
const RESULT_AFTER_STEP_MS = 280;
const LOOKAHEAD_S = 1.5;
const TICK_MS = 250;
const NOISE_SECONDS = 4;

// ---------------------------------------------------------------------------------------------------
// Pure helpers (exported for tests and callers)
// ---------------------------------------------------------------------------------------------------

function clamp01(v, dflt) {
  const n = Number(v);
  if (!Number.isFinite(n)) return dflt;
  return n < 0 ? 0 : n > 1 ? 1 : n;
}

/** Normalise a (possibly partial or stale) persisted settings object. */
export function normalizeSettings(s) {
  const d = SOUND_SETTINGS_DEFAULTS;
  s = s && typeof s === 'object' ? s : {};
  return {
    master: clamp01(s.master, d.master),
    effects: clamp01(s.effects, d.effects),
    ambience: clamp01(s.ambience, d.ambience),
    reverb: clamp01(s.reverb, d.reverb),
    muted: typeof s.muted === 'boolean' ? s.muted : d.muted
  };
}

/** 1..7 / '3' / 'layer3' / 'title' → an ambience id; null, '' or 'none' → null; anything else → undefined. */
export function ambienceId(id) {
  if (id == null || id === '' || id === 'none') return null;
  if (typeof id === 'number' || /^\d+$/.test(String(id))) {
    const n = Number(id);
    return n >= 1 && n <= 7 ? 'layer' + n : undefined;
  }
  return Object.prototype.hasOwnProperty.call(AMBIENCE_DEFS, id) ? id : undefined;
}

/** Index (0..3) of a 20% miasma line: 20 → 0, 40 → 1, 60 → 2, 80 or more → 3. */
export function miasmaLineIndex(linePct) {
  const i = Math.round(Number(linePct) / 20) - 1;
  return i < 0 || !Number.isFinite(i) ? 0 : i > 3 ? 3 : i;
}

/**
 * Which sounds a step plays, by the two-sounds rule: the step (action) and at most one result,
 * by priority miasma death > miasma line > the node's own result.
 */
export function stepPlan({ fresh = true, line = 0, miasmaDeath = false, result = null } = {}) {
  const action = fresh ? 'nodeStep' : 'nodeReturn';
  const res = miasmaDeath ? 'miasmaDeath' : line ? 'miasmaLine' : result || null;
  return res ? [action, res] : [action];
}

/** Park-Miller LCG in [0, 1). The same seed gives the same sequence. */
export function lcg(seed) {
  let s = (Math.abs(Math.floor(seed)) % 2147483646) + 1;
  return function next() {
    s = (s * 16807) % 2147483647;
    return (s - 1) / 2147483646;
  };
}

function hashSeed(str) {
  let h = 2166136261;
  for (let i = 0; i < str.length; i++) {
    h ^= str.charCodeAt(i);
    h = Math.imul(h, 16777619) >>> 0;
  }
  return h;
}

function clonePresets(src) {
  const out = {};
  for (const k in src) out[k] = { ...src[k] };
  return out;
}

/** A stand-in for OCB.Sound when the design system bundle is not loaded (Node, tests). */
function stubBase(OCB) {
  const S = {
    muted: false, master: 0.8, reverb: 0.18,
    presets: OCB && OCB.SOUND_DEFAULTS ? clonePresets(OCB.SOUND_DEFAULTS) : {}
  };
  S.unlock = function () {};
  S.play = function () {};
  S.setMaster = function (v) { S.master = v; };
  S.setReverb = function (v) { S.reverb = v; };
  S.reset = function () { S.presets = OCB && OCB.SOUND_DEFAULTS ? clonePresets(OCB.SOUND_DEFAULTS) : {}; };
  return S;
}

// ---------------------------------------------------------------------------------------------------
// createAudio
// ---------------------------------------------------------------------------------------------------

/**
 * @param {object} [OCB] the design system namespace (window.OCB). Without it the battle presets are silent.
 * @param {object} [settings] persisted { master, effects, ambience, reverb, muted } (all 0..1 except muted).
 * @param {object} [opts] { AudioContext, onChange(settings) }. AudioContext defaults to the global one.
 */
export function createAudio(OCB, settings, opts) {
  opts = opts || {};
  const base = OCB && typeof OCB.Sound === 'function' ? OCB.Sound() : stubBase(OCB);
  const battleNames = new Set(Object.keys((OCB && OCB.SOUND_DEFAULTS) || base.presets || {}));
  const state = normalizeSettings(settings);
  const listeners = [];
  if (typeof opts.onChange === 'function') listeners.push(opts.onChange);

  function mergeMeta(onlyMissing) {
    for (const k in META_SOUND_DEFAULTS) {
      if (battleNames.has(k) || (onlyMissing && base.presets[k])) continue;
      base.presets[k] = { ...META_SOUND_DEFAULTS[k] };
    }
  }
  mergeMeta();

  let ctx = null, master = null, fx = null, fxWet = null, amb = null, ambDuck = null, ambWet = null;
  let bufs = null, unlocked = false, destroyed = false, turn = 0, stepTurn = 0, noiseTurn = 0;
  let ambId = null, surface = 'stone', duck = 1, timer = null, suspendTimer = null, errors = 0;
  const layers = [];
  const lastAt = {};

  // ------------------------------------------------------------------ graph
  function makeNoise(seed, color) {
    const sr = ctx.sampleRate, n = Math.floor(sr * NOISE_SECONDS), fade = Math.floor(sr * 0.25);
    const raw = new Float32Array(n + fade), rnd = lcg(seed);
    let b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, last = 0, peak = 0;
    for (let i = 0; i < raw.length; i++) {
      const w = rnd() * 2 - 1;
      let x = w;
      if (color === 'pink') { // Paul Kellet's refined filter
        b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.969 * b2 + w * 0.153852;
        b3 = 0.8665 * b3 + w * 0.3104856; b4 = 0.55 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.016898;
        x = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362; b6 = w * 0.115926;
      } else if (color === 'brown') {
        last = (last + 0.02 * w) / 1.02; x = last;
      }
      raw[i] = x;
      if (Math.abs(x) > peak) peak = Math.abs(x);
    }
    const buf = ctx.createBuffer(1, n, sr), d = buf.getChannelData(0), k = peak > 0 ? 0.9 / peak : 1;
    // Crossfade the tail into the head so the loop has no seam (brown noise would click otherwise).
    for (let i = 0; i < n; i++) {
      const v = i < fade ? raw[i] * (i / fade) + raw[n + i] * (1 - i / fade) : raw[i];
      d[i] = v * k;
    }
    return buf;
  }
  function makeRoom(seconds, power, seed) {
    const len = Math.floor(ctx.sampleRate * seconds), ir = ctx.createBuffer(2, len, ctx.sampleRate);
    let s = seed;
    for (let ch = 0; ch < 2; ch++) {
      const dd = ir.getChannelData(ch);
      for (let i = 0; i < len; i++) {
        s = (s * 16807) % 2147483647;
        dd[i] = ((s / 2147483647) * 2 - 1) * Math.pow(1 - i / len, power);
      }
    }
    const conv = ctx.createConvolver();
    conv.buffer = ir;
    return conv;
  }
  function ensure() {
    if (destroyed) return false;
    if (ctx) {
      if (ctx.state === 'suspended' && !state.muted && ctx.resume) ctx.resume().catch(() => {});
      return true;
    }
    const AC = opts.AudioContext || globalThis.AudioContext || globalThis.webkitAudioContext;
    if (!AC) return false;
    try { ctx = new AC(); } catch (e) { ctx = null; return false; }
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -18; comp.ratio.value = 4;
    master = G(state.muted ? 0 : state.master);
    master.connect(comp); comp.connect(ctx.destination);
    fx = G(state.effects); fx.connect(master);
    ambDuck = G(duck); ambDuck.connect(master);
    amb = G(state.ambience); amb.connect(ambDuck);
    // Effects get the same short damp room as the battle synth; the ambience gets a longer, darker cave.
    fxWet = G(state.reverb);
    const lp = F('lowpass', 2400); fxWet.connect(lp); lp.connect(makeRoom(1.4, 3.2, 7)).connect(fx);
    ambWet = G(1);
    const lp2 = F('lowpass', 1800); ambWet.connect(lp2); lp2.connect(makeRoom(2.6, 2.4, 13)).connect(amb);
    bufs = { white: makeNoise(11, 'white'), pink: makeNoise(23, 'pink'), brown: makeNoise(37, 'brown') };
    timer = setInterval(tick, TICK_MS);
    if (timer && timer.unref) timer.unref();
    return true;
  }

  // ------------------------------------------------------------------ primitives
  function G(v) { const g = ctx.createGain(); g.gain.value = v == null ? 1 : v; return g; }
  function F(type, f, q) { const b = ctx.createBiquadFilter(); b.type = type; b.frequency.value = f; b.Q.value = q == null ? 0.7 : q; return b; }
  function O(type, f, t, dur) {
    const o = ctx.createOscillator(); o.type = type; o.frequency.setValueAtTime(f, t);
    o.start(t); o.stop(t + dur + 0.05); return o;
  }
  function N(t, dur, color) {
    // Each burst starts at a different point of the buffer (a counter, not a random number).
    const s = ctx.createBufferSource(); s.buffer = bufs[color || 'white']; s.loop = true;
    s.start(t, (noiseTurn++ * 0.137) % (NOISE_SECONDS - 0.5)); s.stop(t + dur + 0.05); return s;
  }
  function pk(v) { return Math.max(0.0002, v); }
  function env(g, t, a, peak, dur) {
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(Math.max(0.0002, peak), t + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t + Math.max(a + 0.01, dur));
  }
  /** Rise linearly to `peak` at t+a, then decay exponentially to silence at t+dur. */
  function swellEnv(g, t, a, peak, dur) {
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(Math.max(0.0002, peak), t + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t + Math.max(a + 0.02, dur));
  }
  /** Connect a voice's last node: into an ambience layer (o.dst) or to the effects bus, dry and wet. */
  function send(node, o) {
    if (o && o.dst) { node.connect(o.dst); return; }
    let n = node;
    if (o && o.pan && ctx.createStereoPanner) { const p = ctx.createStereoPanner(); p.pan.value = o.pan; node.connect(p); n = p; }
    n.connect(fx); n.connect(fxWet);
  }

  // ------------------------------------------------------------------ meta timbres
  // Each: (t, pitch Hz, dur s, vol, tone 0..1, o) with o = { pan, dst, surface, pitchMul }.
  const SYN = {
    // Ports of the battle synth's paper / slide / wood / thud / seal / swish, so meta and battle match.
    paper(t, p, d, v, tone, o) {
      const n = N(t, d), bp = F('bandpass', p, 0.8 + tone * 2), hp = F('highpass', 500), g = G(0);
      n.connect(bp); bp.connect(hp); hp.connect(g); env(g, t, 0.004, v, d); send(g, o);
    },
    slide(t, p, d, v, tone, o) {
      const n = N(t, d), bp = F('bandpass', p, 0.6), g = G(0);
      bp.frequency.setValueAtTime(p * 1.3, t); bp.frequency.exponentialRampToValueAtTime(p * 0.7, t + d);
      n.connect(bp); bp.connect(g);
      g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(pk(v), t + d * 0.3); g.gain.exponentialRampToValueAtTime(0.0001, t + d);
      send(g, o);
    },
    wood(t, p, d, v, tone, o) {
      const x = O('triangle', p, t, d), g = G(0);
      x.frequency.exponentialRampToValueAtTime(p * 0.82, t + d);
      x.connect(g); env(g, t, 0.002, v, d); send(g, o);
      const n = N(t, 0.02), hp = F('highpass', 2500), g2 = G(0); n.connect(hp); hp.connect(g2); env(g2, t, 0.001, v * 0.35, 0.018); send(g2, o);
    },
    thud(t, p, d, v, tone, o) {
      const x = O('sine', p, t, d), g = G(0);
      x.frequency.exponentialRampToValueAtTime(p * 0.55, t + d);
      x.connect(g); env(g, t, 0.003, v, d); send(g, o);
    },
    seal(t, p, d, v, tone, o) {
      SYN.thud(t, p, d * 0.7, v, tone, o);
      SYN.wood(t + 0.012, 1500, 0.025, v * 0.5, tone, o);
    },
    swish(t, p, d, v, tone, o) {
      const n = N(t, d), bp = F('bandpass', p * 0.5, 1.4), g = G(0);
      bp.frequency.setValueAtTime(p * 0.45, t); bp.frequency.exponentialRampToValueAtTime(p * 1.7, t + d);
      n.connect(bp); bp.connect(g);
      g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(Math.max(0.0002, v), t + d * 0.6); g.gain.exponentialRampToValueAtTime(0.0001, t + d);
      send(g, o);
    },

    // A footstep: a damp heel thud and a scuff of the place's ground (SURFACES).
    step(t, p, d, v, tone, o) {
      const s = SURFACES[o.surface] || SURFACES.stone;
      SYN.thud(t, p * s.thud, d * 0.55, v * s.heel, tone, o);
      const n = N(t + 0.008, d, 'pink'), f = F(s.type, s.f * (0.7 + tone * 0.6), s.q), g = G(0);
      n.connect(f); f.connect(g); env(g, t + 0.008, 0.006, v * s.scuff, d); send(g, o);
      if (s.crackle) {
        for (let i = 0; i < 3; i++) {
          const at = t + 0.016 + i * 0.027, c = N(at, 0.012), b = F('bandpass', s.f * 1.3, 2), gc = G(0);
          c.connect(b); b.connect(gc); env(gc, at, 0.001, v * s.crackle * (1 - i * 0.25), 0.012); send(gc, o);
        }
      }
      if (s.wet) {
        const w = N(t + 0.02, 0.035), b = F('bandpass', 1100, 3), gw = G(0);
        w.connect(b); b.connect(gw); env(gw, t + 0.02, 0.002, v * s.wet, 0.035); send(gw, o);
      }
      if (s.ring) {
        const r = O('sine', 1900, t, 0.3), gr = G(0);
        r.connect(gr); env(gr, t + 0.004, 0.003, v * s.ring, 0.28); send(gr, o);
      }
    },
    // A page lifts and turns: noise sweeping up with a flutter of the paper edge.
    page(t, p, d, v, tone, o) {
      const n = N(t, d), bp = F('bandpass', p * 0.6, 0.9 + tone), am = G(0.6), g = G(0);
      bp.frequency.setValueAtTime(p * 0.6, t); bp.frequency.exponentialRampToValueAtTime(p * 1.5, t + d * 0.7);
      const lfo = O('sine', 19, t, d), depth = G(0.4); lfo.connect(depth); depth.connect(am.gain);
      n.connect(bp); bp.connect(am); am.connect(g);
      g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(pk(v), t + d * 0.35); g.gain.exponentialRampToValueAtTime(0.0001, t + d);
      send(g, o);
    },
    // The book shuts: the pages slide down (action) and the covers meet softly (result).
    close(t, p, d, v, tone, o) {
      const n = N(t, d), bp = F('bandpass', p * 1.2, 0.7), g = G(0);
      bp.frequency.setValueAtTime(p * 1.2, t); bp.frequency.exponentialRampToValueAtTime(p * 0.5, t + d * 0.8);
      n.connect(bp); bp.connect(g);
      g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(pk(v * 0.7), t + d * 0.25); g.gain.exponentialRampToValueAtTime(0.0001, t + d * 0.85);
      send(g, o);
      SYN.thud(t + d * 0.75, 140, 0.12, v * 0.55, tone, o);
    },
    // Three short strokes of a dry brush: a line written into the journal.
    quill(t, p, d, v, tone, o) {
      const s = d / 3;
      for (let i = 0; i < 3; i++) {
        const at = t + i * s * 0.9, n = N(at, s), b = F('bandpass', p * (1.08 - i * 0.07), 2.5 + tone * 2), g = G(0);
        b.frequency.setValueAtTime(p * (1.08 - i * 0.07), at); b.frequency.linearRampToValueAtTime(p * (0.9 - i * 0.07), at + s * 0.8);
        n.connect(b); b.connect(g); swellEnv(g, at, s * 0.3, v * (1 - i * 0.18), s * 0.85); send(g, o);
      }
    },
    // A long breath out: noise falling in pitch.
    exhale(t, p, d, v, tone, o) {
      const n = N(t, d, 'pink'), b = F('bandpass', p * 1.25, 0.7), l = F('lowpass', 1400 + tone * 1200), g = G(0);
      b.frequency.setValueAtTime(p * 1.25, t); b.frequency.exponentialRampToValueAtTime(p * 0.55, t + d);
      n.connect(b); b.connect(l); l.connect(g); swellEnv(g, t, d * 0.18, v, d); send(g, o);
    },
    // Training: a practice swing (action) and the knock on the post (result).
    drill(t, p, d, v, tone, o) {
      SYN.swish(t, p, d, v, tone, o);
      const k = t + d * 0.92;
      SYN.wood(k, 300, 0.06, v * 0.8, tone, o);
      SYN.thud(k, 140, 0.12, v * 0.6, tone, o);
    },
    // Something is set down and the dust settles: one low thunk with a soft tail.
    settle(t, p, d, v, tone, o) {
      SYN.thud(t, p, d * 0.5, v, tone, o);
      const n = N(t + 0.02, d, 'pink'), l = F('lowpass', 500 + tone * 500), g = G(0);
      n.connect(l); l.connect(g); swellEnv(g, t + 0.02, 0.04, v * 0.3, d); send(g, o);
    },
    // A warm, low-lit glow (the wick, 灯芯): a soft chime with an octave and a breath of flame under it.
    ember(t, p, d, v, tone, o) {
      const lp = F('lowpass', p * (2.5 + tone * 2), 0.5), out = G(1);
      lp.connect(out); send(out, o);
      [[1, 1, 1], [2, 0.28, 0.6], [3, 0.1, 0.4]].forEach(([m, a, dd]) => {
        const x = O('sine', p * m, t, d * dd), g = G(0); x.connect(g); env(g, t, 0.02, v * a, d * dd); g.connect(lp);
      });
      const n = N(t, d * 0.6, 'pink'), l = F('lowpass', 600), g2 = G(0);
      n.connect(l); l.connect(g2); swellEnv(g2, t, d * 0.25, v * 0.25, d * 0.6); send(g2, o);
    },
    // A cloth pouch opens (action) and the thing is taken (result).
    pouch(t, p, d, v, tone, o) {
      const n = N(t, 0.13), b = F('bandpass', p, 0.7), am = G(0.6), g = G(0);
      const lfo = O('sine', 28, t, 0.13), depth = G(0.4); lfo.connect(depth); depth.connect(am.gain);
      n.connect(b); b.connect(am); am.connect(g); swellEnv(g, t, 0.03, v * 0.7, 0.13); send(g, o);
      SYN.thud(t + Math.min(0.12, d * 0.5), 170, Math.max(0.08, d - 0.12), v * 0.6, tone, o);
    },
    // A low swell (the miasma rises past a line): two beating low sines, an octave above and a dark rumble.
    swell(t, p, d, v, tone, o) {
      const g = G(0);
      [['sine', p, 0.45], ['sine', p * 1.012, 0.45], ['triangle', p * 2.02, 0.6], ['sine', p * 3.01, 0.12]].forEach(([type, f, a]) => {
        const x = O(type, f, t, d), ga = G(a); x.connect(ga); ga.connect(g);
      });
      const nb = N(t, d, 'brown'), nl = F('lowpass', 180 + tone * 300, 0.8), gn = G(0.6);
      nb.connect(nl); nl.connect(gn); gn.connect(g);
      swellEnv(g, t, d * 0.55, v, d); send(g, o);
    },
    // The body hardens: muffled crystal ticks closing up (action), under a low swell that stops short (result).
    petrify(t, p, d, v, tone, o) {
      const lp = F('lowpass', 2200 + tone * 1200, 0.7), gl = G(1);
      lp.connect(gl); send(gl, o);
      let at = t;
      for (let i = 0; i < 9; i++) {
        const n = N(at, 0.012), b = F('bandpass', 1700 + (i % 3) * 350, 3), g = G(0);
        n.connect(b); b.connect(g); env(g, at, 0.001, v * 0.22 * (0.6 + i * 0.05), 0.012); g.connect(lp);
        at += 0.32 * Math.pow(0.78, i);
      }
      const g = G(0);
      [['sine', p, 0.45], ['sine', p * 1.012, 0.45], ['triangle', p * 2.02, 0.6], ['sine', p * 3.01, 0.12]].forEach(([type, f, a]) => {
        const x = O(type, f, t, d), ga = G(a); x.connect(ga); ga.connect(g);
      });
      const nb = N(t, d, 'brown'), nl = F('lowpass', 220, 0.8), gn = G(0.7);
      nb.connect(nl); nl.connect(gn); gn.connect(g);
      g.gain.setValueAtTime(0.0001, t);
      g.gain.linearRampToValueAtTime(v, t + d * 0.62);
      g.gain.linearRampToValueAtTime(0.0001, t + d * 0.66);
      send(g, o);
    },
    // A damp low bell, struck once: inharmonic partials under a low lid.
    toll(t, p, d, v, tone, o) {
      const lp = F('lowpass', p * (5 + tone * 6), 0.5), out = G(1);
      lp.connect(out); send(out, o);
      [[0.5, 0.5, 1], [1, 1, 0.8], [1.19, 0.45, 0.55], [1.5, 0.3, 0.45], [2, 0.35, 0.35], [2.66, 0.16, 0.25]].forEach(([m, a, dd]) => {
        const x = O('sine', p * m, t, d * dd), g = G(0); x.connect(g); env(g, t, 0.004, v * a, d * dd); g.connect(lp);
      });
      const n = N(t, 0.05, 'pink'), nl = F('lowpass', 900), ng = G(0);
      n.connect(nl); nl.connect(ng); env(ng, t, 0.002, v * 0.3, 0.05); send(ng, o);
    },
    // A low warm chord (root, fifth, octave) that rises slowly and fades.
    hum(t, p, d, v, tone, o) {
      const lp = F('lowpass', p * (4 + tone * 6), 0.5), g = G(0);
      [[1, 1], [1.5, 0.55], [2, 0.25]].forEach(([m, a]) => { const x = O('sine', p * m, t, d), gg = G(a); x.connect(gg); gg.connect(lp); });
      lp.connect(g); swellEnv(g, t, d * 0.4, v, d); send(g, o);
    },
    // A deep string falling: the pitch and the lid sink together, with a low rumble of depth.
    fall(t, p, d, v, tone, o) {
      const lp = F('lowpass', p * 10 * (0.6 + tone), 1.0), g = G(0);
      lp.frequency.setValueAtTime(p * 10 * (0.6 + tone), t); lp.frequency.exponentialRampToValueAtTime(p * 2.2, t + d * 0.85);
      [['sawtooth', p, 1], ['triangle', p * 1.004, 1], ['sine', p * 0.5, 0.6]].forEach(([type, f, a]) => {
        const x = O(type, f, t, d), ga = G(a);
        x.frequency.exponentialRampToValueAtTime(f * 0.42, t + d * 0.9);
        x.connect(ga); ga.connect(lp);
      });
      lp.connect(g); env(g, t, 0.05, v, d); send(g, o);
      const n = N(t, d, 'brown'), nl = F('lowpass', 160), gn = G(0);
      n.connect(nl); nl.connect(gn); swellEnv(gn, t, d * 0.3, v * 0.4, d); send(gn, o);
    },
    // A card slides into the deck (action) and the deck is squared with a tap (result).
    tuck(t, p, d, v, tone, o) {
      SYN.slide(t, p, Math.min(0.15, d), v * 0.8, tone, o);
      SYN.wood(t + Math.min(0.13, d * 0.6), p * 0.48, 0.035, v * 0.7, tone, o);
    },
    // The last heartbeat, slower than the battle's (action), then a low tone sinking into silence (result).
    expire(t, p, d, v, tone, o) {
      SYN.thud(t, p, 0.18, v * 0.9, tone, o);
      SYN.thud(t + 0.24, p * 0.9, 0.2, v * 0.6, tone, o);
      const t2 = t + 0.7, d2 = Math.max(0.5, d - 0.7);
      const lp = F('lowpass', p * 9 * (0.6 + tone), 0.6), g = G(0);
      [['triangle', p * 2], ['sine', p]].forEach(([type, f]) => {
        const x = O(type, f, t2, d2); x.frequency.exponentialRampToValueAtTime(f * 0.7, t2 + d2); x.connect(lp);
      });
      lp.connect(g); swellEnv(g, t2, 0.6, v * 0.8, d2); send(g, o);
    },
    // Relief: a long breath out (action) and the wick's small warm glow (result).
    relief(t, p, d, v, tone, o) {
      SYN.exhale(t, 700, d * 0.6, v, tone, o);
      SYN.ember(t + d * 0.42, p, d * 0.6, v * 0.8, tone, o);
    },
    // A lamp is lit: the strike (action), then the flame catches with a warm low glow (result).
    kindle(t, p, d, v, tone, o) {
      const n = N(t, 0.09), b = F('bandpass', p, 1.4), g = G(0);
      b.frequency.setValueAtTime(p * 0.7, t); b.frequency.exponentialRampToValueAtTime(p * 1.3, t + 0.07);
      n.connect(b); b.connect(g); env(g, t, 0.003, v * 0.7, 0.08); send(g, o);
      const t2 = t + 0.11, d2 = Math.max(0.3, d - 0.11);
      const f = N(t2, d2, 'pink'), fl = F('lowpass', 520 + tone * 300, 0.7), gf = G(0);
      f.connect(fl); fl.connect(gf); swellEnv(gf, t2, 0.25, v * 0.8, d2); send(gf, o);
      const w = O('sine', 110, t2, d2), w2 = O('sine', 165, t2, d2), g5 = G(0.5), gw = G(0);
      w.connect(gw); w2.connect(g5); g5.connect(gw); swellEnv(gw, t2, d2 * 0.4, v * 0.25, d2); send(gw, o);
    },
    // 襲名: the name is stamped with a heavy seal (action) and a low string answers (result).
    nameSeal(t, p, d, v, tone, o) {
      SYN.seal(t, 120, 0.16, v, tone, o);
      const t2 = t + 0.22, d2 = Math.max(0.4, d - 0.22);
      const lp = F('lowpass', p * 10, 1.1), g = G(0);
      lp.frequency.setValueAtTime(p * 10, t2); lp.frequency.exponentialRampToValueAtTime(p * 1.8, t2 + d2 * 0.7);
      const a = O('sawtooth', p, t2, d2), b = O('triangle', p * 1.003, t2, d2);
      a.connect(lp); b.connect(lp); lp.connect(g); env(g, t2, 0.02, v * 0.6, d2); send(g, o);
    },
    // A bowed low string: a slow attack, the lid opening and closing, and a breath of bow hair.
    bow(t, p, d, v, tone, o) {
      const lp = F('lowpass', p * 3, 1.1), g = G(0);
      lp.frequency.setValueAtTime(p * 3, t); lp.frequency.linearRampToValueAtTime(p * (5 + tone * 6), t + d * 0.4);
      lp.frequency.exponentialRampToValueAtTime(p * 2.5, t + d);
      const a = O('sawtooth', p, t, d), b = O('sawtooth', p * 1.005, t, d), c = O('triangle', p * 0.5, t, d), gc = G(0.5);
      a.connect(lp); b.connect(lp); c.connect(gc); gc.connect(lp); lp.connect(g);
      g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(pk(v), t + d * 0.35);
      g.gain.linearRampToValueAtTime(pk(v * 0.8), t + d * 0.6); g.gain.exponentialRampToValueAtTime(0.0001, t + d);
      send(g, o);
      const n = N(t, d, 'pink'), nb = F('bandpass', p * 9, 1.2), gn = G(0);
      n.connect(nb); nb.connect(gn); swellEnv(gn, t, d * 0.35, v * 0.12, d * 0.9); send(gn, o);
    }
  };

  // ------------------------------------------------------------------ ambience
  function makeLayer(id) {
    const def = AMBIENCE_DEFS[id], now = ctx.currentTime;
    const L = { id, def, nodes: [], sparse: [], rnd: lcg(hashSeed(id)), stopping: false, timer: null };
    L.gain = G(0.0001); L.gain.connect(amb);
    L.mix = G(1); L.mix.connect(L.gain);
    const sendRoom = G(def.room); L.gain.connect(sendRoom); sendRoom.connect(ambWet);
    const keep = (n) => { L.nodes.push(n); return n; };
    const lfo = (param, list) => {
      for (const [rate, depth] of list || []) {
        const x = keep(ctx.createOscillator()); x.type = 'sine'; x.frequency.value = rate;
        const dg = G(depth); x.connect(dg); dg.connect(param); x.start(now);
      }
    };
    const o = { dst: L.mix };
    // K: the toolkit an ambience builds with. Beds and drones run until the layer stops; voices are one-shots.
    const K = {
      bed(color, { type = 'lowpass', f, q = 0.7, gain, pan = 0, fLfo, gLfo }) {
        const s = keep(ctx.createBufferSource()); s.buffer = bufs[color]; s.loop = true;
        const fl = F(type, f, q), g = G(gain);
        s.connect(fl); fl.connect(g);
        let last = g;
        if (pan && ctx.createStereoPanner) { const p = ctx.createStereoPanner(); p.pan.value = pan; g.connect(p); last = p; }
        last.connect(L.mix);
        lfo(fl.frequency, fLfo); lfo(g.gain, gLfo);
        s.start(now, (L.nodes.length * 0.61) % (NOISE_SECONDS - 0.5));
      },
      drone(type, f, gain, { lp, gLfo } = {}) {
        const x = keep(ctx.createOscillator()); x.type = type; x.frequency.value = f;
        const g = G(gain);
        if (lp) { const fl = F('lowpass', lp, 0.7); x.connect(fl); fl.connect(g); } else x.connect(g);
        g.connect(L.mix); lfo(g.gain, gLfo); x.start(now);
      },
      every(lo, hi, fn) {
        L.sparse.push({ lo, hi, fn, k: 0, next: now + lo * 0.5 + (hi - lo) * 0.5 * L.rnd() });
      },
      // one-shot voices
      tone(t, f, v, a, d, type = 'sine', lp = 0) {
        const x = O(type, f, t, a + d), g = G(0);
        if (lp) { const fl = F('lowpass', lp, 0.7); x.connect(fl); fl.connect(g); } else x.connect(g);
        swellEnv(g, t, a, v, a + d); g.connect(L.mix);
      },
      tick(t, f, q, v, d) {
        const n = N(t, d), b = F('bandpass', f, q), g = G(0);
        n.connect(b); b.connect(g); env(g, t, 0.001, v, d); g.connect(L.mix);
      },
      drip(t, f, v) {
        const x = O('sine', f, t, 0.12), g = G(0);
        x.frequency.exponentialRampToValueAtTime(f * 0.45, t + 0.06); x.connect(g); env(g, t, 0.002, v, 0.09); g.connect(L.mix);
      },
      thud(t, f, v, d) { SYN.thud(t, f, d, v, 0.3, o); },
      creak(t, f, v) { // a beam settles: a slowing pulse train through a narrow band
        const x = O('sawtooth', 44, t, 0.45), b = F('bandpass', f, 5), g = G(0);
        x.frequency.exponentialRampToValueAtTime(27, t + 0.42);
        x.connect(b); b.connect(g); swellEnv(g, t, 0.06, v, 0.42); g.connect(L.mix);
      },
      scrape(t, f, v, d) { // something heavy slides along stone
        const n = N(t, d, 'pink'), b = F('bandpass', f, 1.6), g = G(0);
        b.frequency.setValueAtTime(f * 0.8, t); b.frequency.linearRampToValueAtTime(f * 1.15, t + d);
        n.connect(b); b.connect(g);
        g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(v, t + d * 0.5); g.gain.linearRampToValueAtTime(0.0001, t + d);
        g.connect(L.mix);
      },
      rumble(t, v, f, a, d) { // distant thunder: dark noise that rolls and darkens
        const n = N(t, a + d, 'brown'), b = F('lowpass', f * 1.6, 0.9), g = G(0);
        b.frequency.setValueAtTime(f * 1.6, t); b.frequency.exponentialRampToValueAtTime(f * 0.7, t + a + d);
        n.connect(b); b.connect(g); swellEnv(g, t, a, v, a + d); g.connect(L.mix);
      },
      glide(t, f0, f1, v, d) { // a slow sliding tone (a far groan, a falling breath)
        const x = O('sine', f0, t, d), x2 = O('triangle', f0 * 2, t, d), g2 = G(0.2), g = G(0);
        x.frequency.exponentialRampToValueAtTime(f1, t + d); x2.frequency.exponentialRampToValueAtTime(f1 * 2, t + d);
        x.connect(g); x2.connect(g2); g2.connect(g); swellEnv(g, t, d * 0.4, v, d); g.connect(L.mix);
      }
    };
    AMBIENCE_BUILDERS[id](K, L.rnd);
    return L;
  }
  function startLayer(id, fadeMs) {
    const L = makeLayer(id), now = ctx.currentTime, f = Math.max(0.05, fadeMs / 1000);
    L.gain.gain.setValueAtTime(0.0001, now);
    L.gain.gain.linearRampToValueAtTime(L.def.level, now + f);
    layers.push(L);
    tick();
    return L;
  }
  function killLayer(L) {
    clearTimeout(L.timer);
    for (const n of L.nodes) { try { n.stop(); } catch (e) { /* already stopped */ } }
    try { L.gain.disconnect(); } catch (e) { /* ignore */ }
    const i = layers.indexOf(L);
    if (i >= 0) layers.splice(i, 1);
  }
  function stopLayer(L, fadeMs) {
    if (L.stopping) return;
    L.stopping = true;
    const now = ctx.currentTime, f = Math.max(0.05, fadeMs / 1000), p = L.gain.gain;
    const cur = Number.isFinite(p.value) ? p.value : L.def.level;
    p.cancelScheduledValues(now); p.setValueAtTime(cur, now); p.linearRampToValueAtTime(0, now + f);
    L.timer = setTimeout(() => killLayer(L), fadeMs + 150);
  }
  /** Schedule the sparse ambience events that fall inside the lookahead window. */
  function tick() {
    if (!ctx) return;
    const now = ctx.currentTime, horizon = now + LOOKAHEAD_S;
    for (const L of layers) {
      if (L.stopping) continue;
      for (const s of L.sparse) {
        if (s.next < now - 1) s.next = now + 0.05; // after a long suspend, do not burst
        while (s.next < horizon) {
          try { s.fn(Math.max(s.next, now + 0.02), s.k, L.rnd); } catch (e) { errors++; /* skip a bad voice */ }
          s.k++;
          s.next += s.lo + (s.hi - s.lo) * L.rnd();
        }
      }
    }
  }

  // ------------------------------------------------------------------ settings
  function snapshot() { return { master: state.master, effects: state.effects, ambience: state.ambience, reverb: state.reverb, muted: state.muted }; }
  function changed() { const s = snapshot(); for (const fn of listeners.slice()) { try { fn(s); } catch (e) { /* ignore */ } } }
  function ramp(param, v) { if (!ctx) return; const now = ctx.currentTime; param.cancelScheduledValues(now); param.setTargetAtTime(v, now, 0.03); }
  function applyBase() { base.muted = state.muted; base.setMaster(state.master * state.effects); base.setReverb(state.reverb); }
  function applyMute() {
    base.muted = state.muted;
    if (!ctx) return;
    ramp(master.gain, state.muted ? 0 : state.master);
    clearTimeout(suspendTimer);
    if (state.muted) {
      // Stop rendering a silent graph after the fade, to save the battery; resume on unmute.
      suspendTimer = setTimeout(() => { if (state.muted && ctx && ctx.state === 'running' && ctx.suspend) ctx.suspend().catch(() => {}); }, 400);
    } else if (ctx.state === 'suspended' && unlocked && ctx.resume) ctx.resume().catch(() => {});
  }
  applyBase();

  // ------------------------------------------------------------------ play
  function play(name, delayMs, mult, o) {
    if (state.muted || destroyed) return false;
    const pr = base.presets[name];
    if (!pr) return false;
    if (battleNames.has(name)) { base.play(name, delayMs, mult); return unlocked; }
    if (!unlocked || !ensure()) return false;
    const now = ctx.currentTime;
    if (pr.gap && lastAt[name] != null && now - lastAt[name] < pr.gap / 1000) return false;
    lastAt[name] = now;
    turn++;
    const syn = SYN[pr.timbre];
    if (!syn) return false;
    o = { ...(o || {}) };
    if (pr.timbre === 'step') {
      stepTurn++;
      if (o.pan == null) o.pan = stepTurn % 2 ? 0.12 : -0.12; // left, right, left ...
      if (!o.surface) o.surface = surface;
    }
    // Alternate the pitch by ±3% so repeats do not stack into one tone (as the battle synth does).
    const wob = pr.timbre === 'paper' || pr.timbre === 'wood' || pr.timbre === 'step' ? ((turn % 3) - 1) * 0.03 : 0;
    const t = now + Math.max(0, delayMs || 0) / 1000;
    const pitch = pr.pitch * (1 + wob) * (o.pitchMul || 1);
    try { syn(t, pitch, pr.dur / 1000, pr.vol * (mult == null ? 1 : mult), pr.tone, o); } catch (e) { errors++; return false; }
    return true;
  }

  function setAmbience(id, fadeMs) {
    const nid = ambienceId(id);
    if (nid === undefined) return false;
    const fade = fadeMs == null ? 1800 : Math.max(0, fadeMs);
    surface = nid ? AMBIENCE_DEFS[nid].surface : surface;
    const live = layers.find((L) => !L.stopping);
    if (nid === ambId && (live || !ctx)) return true;
    ambId = nid;
    if (!unlocked || !ctx) return true; // starts on unlock()
    for (const L of layers.slice()) stopLayer(L, fade);
    if (nid) startLayer(nid, fade);
    return true;
  }

  // ------------------------------------------------------------------ the object
  const audio = {
    base,
    get presets() { return base.presets; },
    set presets(v) { base.presets = v; mergeMeta(true); },
    get muted() { return state.muted; },
    set muted(v) { audio.setMuted(v); },
    get master() { return state.master; },
    get effects() { return state.effects; },
    get ambienceVolume() { return state.ambience; },
    get reverb() { return state.reverb; },
    get unlocked() { return unlocked; },
    get ambience() { return ambId; },
    get surface() { return surface; },
    get settings() { return snapshot(); },
    battleNames: [...battleNames],
    metaNames: Object.keys(META_SOUND_DEFAULTS),
    /** The timbres the meta synth implements. */
    timbres: Object.keys(SYN),

    /** Call from a user gesture. Creates the audio graph and starts the pending ambience. */
    unlock() {
      if (destroyed) return false;
      unlocked = true;
      try { base.unlock(); } catch (e) { /* the battle synth stays silent */ }
      applyBase();
      if (!ensure()) return false;
      if (ambId && !layers.some((L) => !L.stopping)) startLayer(ambId, 2500);
      return true;
    },
    /** Unlock on the first pointer or key press on `target` (default: document). Returns an unbind function. */
    bindUnlock(target) {
      target = target || globalThis.document;
      if (!target || !target.addEventListener) return () => {};
      const evs = ['pointerdown', 'keydown', 'touchend'];
      const once = () => { audio.unlock(); off(); };
      const off = () => evs.forEach((e) => target.removeEventListener(e, once, true));
      evs.forEach((e) => target.addEventListener(e, once, true));
      return off;
    },
    /** play(name, delayMs, mult[, { pitchMul, pan, surface }]). Battle names go to OCB.Sound. */
    play,
    has(name) { return !!base.presets[name]; },
    /** A step on the map, with at most one result by the two-sounds rule. Returns the planned names. */
    stepTo(step) {
      const plan = stepPlan(step);
      play(plan[0]);
      if (plan[1] === 'miasmaLine') audio.miasmaLine(step.line, RESULT_AFTER_STEP_MS);
      else if (plan[1]) play(plan[1], RESULT_AFTER_STEP_MS);
      return plan;
    },
    /** The low swell for a 20% line (20/40/60/80): a whole tone lower and a little louder per line. */
    miasmaLine(linePct, delayMs) {
      const i = miasmaLineIndex(linePct);
      return play('miasmaLine', delayMs || 0, 1 + i * 0.1, { pitchMul: MIASMA_LINE_PITCH[i] });
    },
    setAmbience,
    /** Lower the ambience while something needs the room (not persisted). factor 0..1. */
    duckAmbience(factor, fadeMs) {
      duck = clamp01(factor, 1);
      if (ambDuck && ctx) { const now = ctx.currentTime; ambDuck.gain.cancelScheduledValues(now); ambDuck.gain.setTargetAtTime(duck, now, Math.max(0.01, (fadeMs == null ? 600 : fadeMs) / 3000)); }
    },
    setMaster(v) { state.master = clamp01(v, state.master); base.setMaster(state.master * state.effects); if (!state.muted && master) ramp(master.gain, state.master); changed(); },
    setEffects(v) { state.effects = clamp01(v, state.effects); base.setMaster(state.master * state.effects); if (fx) ramp(fx.gain, state.effects); changed(); },
    setAmbienceVolume(v) { state.ambience = clamp01(v, state.ambience); if (amb) ramp(amb.gain, state.ambience); changed(); },
    setReverb(v) { state.reverb = clamp01(v, state.reverb); base.setReverb(state.reverb); if (fxWet) ramp(fxWet.gain, state.reverb); changed(); },
    setMuted(v) { state.muted = !!v; applyMute(); changed(); },
    toggleMute() { audio.setMuted(!state.muted); return state.muted; },
    applySettings(s) {
      const n = normalizeSettings({ ...snapshot(), ...(s || {}) });
      Object.assign(state, n);
      applyBase();
      if (ctx) { ramp(fx.gain, n.effects); ramp(amb.gain, n.ambience); ramp(fxWet.gain, n.reverb); }
      applyMute();
      changed();
    },
    /** Subscribe to settings changes (to persist them). Returns an unsubscribe function. */
    onChange(fn) {
      listeners.push(fn);
      return () => { const i = listeners.indexOf(fn); if (i >= 0) listeners.splice(i, 1); };
    },
    reset() { base.reset(); mergeMeta(); },
    destroy() {
      destroyed = true;
      clearInterval(timer); clearTimeout(suspendTimer);
      for (const L of layers.slice()) killLayer(L);
      if (ctx && ctx.close) ctx.close().catch(() => {});
      ctx = null;
    },
    /** For tests: run one scheduler pass now. */
    _tick: tick,
    get _ctx() { return ctx; },
    /** For tests: how many voices threw (a bad preset or ambience voice is skipped, not fatal). */
    get _errors() { return errors; },
    get _layers() { return layers.map((L) => ({ id: L.id, stopping: L.stopping, nodes: L.nodes.length, sparse: L.sparse.map((s) => s.k) })); }
  };
  return audio;
}

// ---------------------------------------------------------------------------------------------------
// The ambiences (world-v1.md §4). Gains are tiny on purpose: the ambience sits under everything.
// ---------------------------------------------------------------------------------------------------

const AMBIENCE_BUILDERS = {
  // Title — the mouth of the den: wind drawn inward, a low D, now and then a far string.
  title(K) {
    K.bed('pink', { type: 'lowpass', f: 380, q: 0.6, gain: 0.07, fLfo: [[0.071, 160], [0.023, 90]], gLfo: [[0.031, 0.025]] });
    K.bed('brown', { type: 'lowpass', f: 140, gain: 0.08 });
    K.drone('sine', 73.42, 0.016, { gLfo: [[0.05, 0.006]] });
    K.drone('sine', 110.3, 0.006);
    K.every(14, 22, (t, k) => K.tone(t, [146.83, 110, 130.81][k % 3], 0.010, 1.6, 4.5, 'triangle', 600));
  },
  // 継承の間 — a still room with a lamp: no wind, the flame breathing, a warm fifth, a beam settling.
  hall(K) {
    K.bed('brown', { type: 'lowpass', f: 240, gain: 0.06 });
    K.bed('pink', { type: 'bandpass', f: 820, q: 0.7, gain: 0.006, gLfo: [[0.83, 0.002], [2.3, 0.0015]] });
    K.drone('sine', 73.42, 0.012);
    K.drone('sine', 110, 0.0065, { gLfo: [[0.037, 0.002]] });
    K.drone('sine', 146.83, 0.003);
    K.every(16, 30, (t) => K.creak(t, 190, 0.010));
  },
  // 階層間の休憩 — a pocket of rest: slow breathing air, a low A and E, a soft tone now and then.
  interlude(K) {
    K.bed('pink', { type: 'lowpass', f: 520, gain: 0.05, gLfo: [[0.12, 0.022]] });
    K.drone('sine', 55, 0.014);
    K.drone('sine', 82.41, 0.008, { gLfo: [[0.045, 0.003]] });
    K.every(9, 15, (t, k) => K.tone(t, [220, 293.66, 246.94, 329.63][k % 4], 0.006, 1.1, 3.6));
  },
  // 1 燦光の樹海 — sunlit sunken forest: open air through the hole in the roof, leaves in slow gusts,
  // twigs and leaf litter, a few soft motes of light. Almost no drone: the only layer lit by the sky.
  layer1(K) {
    K.bed('pink', { type: 'lowpass', f: 900, gain: 0.03, fLfo: [[0.043, 320]] });
    K.bed('white', { type: 'bandpass', f: 3400, q: 0.5, gain: 0.010, gLfo: [[0.052, 0.006], [0.13, 0.003]] });
    K.drone('sine', 110, 0.003);
    K.every(2.5, 7, (t, k, r) => {
      const n = 1 + (k % 3);
      for (let i = 0; i < n; i++) K.tick(t + i * (0.07 + 0.05 * r()), 2200 + 1400 * r(), 2.5, 0.005, 0.018);
    });
    K.every(10, 18, (t, k) => K.tone(t, [659.26, 783.99, 880][k % 3], 0.0035, 0.5, 2.6));
  },
  // 2 ■■の秘跡 — the dragon god's old sanctuary: wind resonating in the pillar rows, drifting sand,
  // a faint hum of stone, the first violet crystals barely ringing, a far stone settling.
  layer2(K) {
    K.bed('pink', { type: 'bandpass', f: 340, q: 6, gain: 0.05, fLfo: [[0.029, 38]], gLfo: [[0.047, 0.018]] });
    K.bed('pink', { type: 'bandpass', f: 512, q: 8, gain: 0.025, fLfo: [[0.041, 30]], gLfo: [[0.061, 0.012]] });
    K.bed('white', { type: 'bandpass', f: 6500, q: 0.6, gain: 0.0018, gLfo: [[0.09, 0.0018]] });
    K.drone('sine', 110, 0.007);
    K.drone('sine', 110.55, 0.007);
    K.drone('sine', 165, 0.003);
    K.every(14, 26, (t, k) => K.tone(t, [1567.98, 1396.91][k % 2], 0.0022, 0.02, 1.8, 'sine', 3000));
    K.every(20, 34, (t) => { K.thud(t, 68, 0.03, 0.5); K.tick(t + 0.03, 1800, 0.8, 0.004, 0.25); });
  },
  // 3 大黒蛇の住処 — the dark, damp den: low air swelling with the serpent's breath (about 11 s),
  // a very low drone, water drops, the coils shifting against the wall.
  layer3(K) {
    K.bed('brown', { type: 'lowpass', f: 190, gain: 0.10, gLfo: [[0.09, 0.05]] });
    K.drone('sawtooth', 41.2, 0.018, { lp: 150 });
    K.drone('sine', 82.41, 0.005);
    K.every(3, 9, (t, k, r) => K.drip(t, 1100 + 700 * r(), 0.012));
    K.every(18, 32, (t) => K.scrape(t, 380, 0.012, 2.6));
  },
  // 4 輝晶の間 — the crystal hall: a haze of slowly beating glass tones in the middle register,
  // a thin hiss of light, crystals that swell and fade. Bright to the eye, still quiet to the ear.
  layer4(K) {
    [523.25, 524.1, 784, 785.3, 1046.5].forEach((f, i) => K.drone('sine', f, 0.0016, { gLfo: [[0.07 + i * 0.03, 0.0014]] }));
    K.bed('white', { type: 'bandpass', f: 6200, q: 0.8, gain: 0.0025 });
    K.drone('sine', 220, 0.004);
    K.drone('sine', 329.63, 0.0025);
    K.every(7, 13, (t, k) => K.tone(t, [783.99, 880, 1174.66, 1318.51][k % 4], 0.004, 1.5, 4.2));
  },
  // 5 血染めの死海 — the dried red sea bed: dry gusts, wind whistling through the giant ribs,
  // the salt crust crackling, something huge groaning far away.
  layer5(K) {
    K.bed('pink', { type: 'lowpass', f: 650, gain: 0.05, gLfo: [[0.035, 0.022], [0.11, 0.008]] });
    K.bed('white', { type: 'bandpass', f: 620, q: 14, gain: 0.02, fLfo: [[0.021, 70]], gLfo: [[0.058, 0.012]] });
    K.drone('sine', 49, 0.012);
    K.drone('sine', 73.42, 0.004);
    K.every(5, 12, (t, k, r) => {
      const n = 3 + (k % 4);
      for (let i = 0; i < n; i++) K.tick(t + i * (0.03 + 0.06 * r()), 2600 + 900 * r(), 2, 0.0035, 0.008);
    });
    K.every(22, 36, (t) => K.glide(t, 65.41, 51.91, 0.010, 3.2));
  },
  // 6 獄雷峡 — the gorge of black lightning: wind down the gorge and its edge on the cliffs, a dark low
  // drone, and now and then the lightning crackling along a cliff with thunder rolling far off.
  layer6(K) {
    K.bed('pink', { type: 'bandpass', f: 260, q: 1.2, gain: 0.07, gLfo: [[0.05, 0.03], [0.017, 0.015]] });
    K.bed('white', { type: 'bandpass', f: 1800, q: 3, gain: 0.004, gLfo: [[0.07, 0.003]] });
    K.drone('sine', 46.25, 0.016);
    K.drone('sawtooth', 61.74, 0.006, { lp: 140 });
    K.every(13, 26, (t, k, r) => {
      K.tick(t, 2400, 1, 0.003, 0.09);
      K.rumble(t + 0.25 + 0.6 * r(), 0.07, 180, 0.4 + 0.5 * r(), 3.2 + 1.8 * r());
    });
  },
  // 7 果てなき世界 — the void of miasma: pressure, two low tones beating against each other, the spring
  // welling up and down, the miasma exhaling in slow falling tones. The darkest place.
  layer7(K) {
    K.bed('brown', { type: 'lowpass', f: 120, gain: 0.09, gLfo: [[0.023, 0.03]] });
    K.drone('sine', 55, 0.012);
    K.drone('sine', 55.35, 0.012);
    K.drone('sine', 82.6, 0.004);
    K.drone('sawtooth', 27.5, 0.01, { lp: 90 });
    K.drone('sine', 164.81, 0.004, { gLfo: [[0.031, 0.003]] });
    K.drone('sine', 165.3, 0.004);
    K.bed('pink', { type: 'bandpass', f: 400, q: 1.5, gain: 0.03, fLfo: [[0.06, 240]] });
    K.every(16, 28, (t, k) => { const f = [196, 174.61, 155.56][k % 3]; K.glide(t, f, f * 0.94, 0.009, 3.4); });
  }
};

/** For tests: every ambience id has a builder. */
export const AMBIENCE_BUILDER_IDS = Object.keys(AMBIENCE_BUILDERS);
