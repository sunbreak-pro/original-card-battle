# Area: audio — every sound and the ambience, ported to Unity (Trial.Audio)

Paths: `JS/` = `C:\Users\user\AppData\Local\Temp\claude\C--Users-user-orca-original-card-battle\1ab86662-d6cc-4831-aa8c-76f9592e0ca6\scratchpad\proto\prototypes\browser-game\`, `OCB` = `…\scratchpad\artifact\ocb.js` (byte-identical to `JS/vendor/ds/bundle.js` lines 662-841, checked by md5 of the `TIMBRES … OCB.Sound = Sound` block), `KIT/` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port\unity-project-kit\Assets\View\`, `UP/` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port\`.

Who wins: sound carries no game rules, so **the JS wins everywhere in this area** (graphs, numbers, timing, levels). The only C#-side facts are the values the call sites read from the cores (miasma forecast, node kind, card kind, strike system). Deviations proposed below are marked **[DEVIATION]** with the reason.

Unity today has no sound at all: no `AudioSource`, `AudioClip` or `OnAudioFilterRead` anywhere under `KIT/` (grep). `C:\Users\user\Unity\RPG-by-card\ProjectSettings\AudioManager.asset`: volume 1, Default Speaker Mode 2 (stereo), DSP buffer 1024, real voices 32, virtual voices 512, sample rate 0 (= device). Only `SampleScene.unity` has an `AudioListener`.

---

## 0. What the area contains

| Source | Lines | What it is |
| --- | --- | --- |
| `OCB:662-841` | 180 | The design system's battle synth `Sound()`: 16 timbres (`TIMBRES` 666-670), 29 presets (`SOUND_DEFAULTS` 671-701), its own `AudioContext`, compressor, 1.4 s damp room, 1 s white noise. `OCB.SoundPalette` (2922-2937) is a DS demo page, not used by the game. |
| `JS/src/scenes/audio/Audio.js` | 964 | `createAudio()`: wraps `OCB.Sound()` (battle names go to it unchanged) and adds 27 meta presets (`META_SOUND_DEFAULTS` 55-83) synthesised by 24 labelled meta timbres (`SYN` 342-576, 26 functions), 8 footstep surfaces (`SURFACES` 86-95), 10 ambiences (`AMBIENCE_DEFS` 101-112, `AMBIENCE_BUILDERS` 856-961), settings (115, 696-828), the step plan (166-170). Second `AudioContext`. |
| `JS/docs/AUDIO.md` | 141 | Rules, API, call-site table, per-event rationale, ambience table with LUFS, surfaces, measured levels. Two statements are stale versus the code (see §5.6). |
| `JS/dev/audio.html` | 275 | Dev page: settings sliders (reverb 0..0.6), mute, defaults, ambience buttons with fade slider (0-5000 ms, step 100, default 1800), stop, duck toggle (0.45 / 600 ms), a 7-node step strip, 10 step cases, one button per meta event by group, battle presets, a 40 s run-through (`at(...)` list 251-271). |
| `JS/test/audio/Audio.test.mjs` | 356 | Strict WebAudio mock (23-122, sampleRate 8000, logs every param call), 7 tests (import without window; wraps the real bundle; every name exists; every event and ambience against the mock with no warnings and no leaked sources; determinism of the whole call log; settings; crossfade). |

Call sites are in §4.

---

## 1. The audio graph (both contexts)

### 1.1 Battle synth context (`OCB:707-730`)

```
voice nodes ──dest(g)──► out (Gain = master×effects) ──► comp ──► destination
             └─────────► wet (Gain = reverb 0.18) ─► LP(2400, Q = 1, the WebAudio default) ─► Convolver(room 1.4 s) ─► out
comp = DynamicsCompressor(threshold −18 dB, ratio 4; knee 30, attack 0.003, release 0.25 = WebAudio defaults)
```

- Noise: ONE mono buffer, 1 s at the context rate, seed 11: `sd = (sd*16807) % 2147483647; nd[j] = (sd/2147483647)*2-1` (no normalisation, no loop fade) (`OCB:726-728`).
- Room impulse: stereo, `len = floor(sr*1.4)`, seed 7 carried from channel 0 into channel 1: `s = (s*16807) % 2147483647; ir[ch][i] = ((s/2147483647)*2-1) * (1 - i/len)^3.2` (`OCB:716-721`). Convolver `normalize` is left at its default **true** (see §3.3).
- `S.setMaster(v)` / `S.setReverb(v)` set `.value` immediately (no ramp) (`OCB:733-734`). Battle master = `master × effects` (Audio.js `applyBase` 699).

### 1.2 Meta + ambience context (`Audio.js:281-306`)

```
meta voice ──send(g,o)──[StereoPanner(o.pan) if o.pan]──► fx (Gain = effects) ──► master (Gain = master | 0 when muted) ──► comp ──► destination
                                                     └──► fxWet (Gain = reverb) ─► LP(2400, Q 0.7) ─► Convolver(room 1.4 s, power 3.2, seed 7) ─► fx
ambience layer L: beds/drones/voices ─► L.mix (1) ─► L.gain (fade 0→def.level) ─► amb (Gain = ambience) ─► ambDuck (Gain = duck) ─► master
                                                     L.gain ─► sendRoom (Gain = def.room) ─► ambWet (1) ─► LP(1800, Q 0.7) ─► Convolver(cave 2.6 s, power 2.4, seed 13) ─► amb
comp = DynamicsCompressor(threshold −18, ratio 4)
```

- `makeRoom(seconds, power, seed)` (267-280) is the same formula as the DS room. The meta fx room is therefore identical to the battle room except the low-pass Q (0.7 here, 1 in the DS).
- Noise buffers (`makeNoise` 242-266), created once in `ensure()`: `white` seed 11, `pink` seed 23, `brown` seed 37. `n = floor(sr*4)`, `fade = floor(sr*0.25)`, raw length `n + fade`, generator `lcg(seed)` (173-179): `s = (abs(floor(seed)) % 2147483646) + 1; next: s = (s*16807) % 2147483647; return (s-1)/2147483646`, `w = rnd()*2-1`.
  - white: `x = w`.
  - pink (Paul Kellet, refined): `b0=.99886*b0+w*.0555179; b1=.99332*b1+w*.0750759; b2=.969*b2+w*.153852; b3=.8665*b3+w*.3104856; b4=.55*b4+w*.5329522; b5=-.7616*b5-w*.016898; x=b0+b1+b2+b3+b4+b5+b6+w*.5362; b6=w*.115926`.
  - brown: `last = (last + .02*w)/1.02; x = last`.
  - `raw[i] = x` (stored as Float32), `peak = max |x|` (computed on the double x), `k = 0.9/peak`; output `d[i] = (i < fade ? raw[i]*(i/fade) + raw[n+i]*(1-i/fade) : raw[i]) * k` — a seamless 4 s loop peaking at 0.9.
- A timer `setInterval(tick, 250)` runs the ambience scheduler (303).
- `ensure()` is called by `unlock()` and by every `play()`; nothing exists before the first user gesture.

### 1.3 Shared primitives (exact semantics; the C# port must keep them)

Meta (`Audio.js:309-338`):

| Name | Definition |
| --- | --- |
| `G(v)` | GainNode, `gain.value = v ?? 1`. Before its first scheduled event a param holds this intrinsic value, so `G(0)` is silent until its envelope starts. |
| `F(type, f, q)` | BiquadFilter, `frequency.value = f`, `Q.value = q ?? 0.7`. |
| `O(type, f, t, dur)` | Oscillator, `frequency.setValueAtTime(f, t)`, `start(t)`, `stop(t + dur + 0.05)`. |
| `N(t, dur, color='white')` | BufferSource on the 4 s noise, `loop = true`, `start(t, (noiseTurn++ * 0.137) % 3.5)`, `stop(t + dur + 0.05)`. `noiseTurn` is one global counter shared by every burst (meta and ambience). |
| `pk(v)` | `max(0.0002, v)`. |
| `env(g, t, a, peak, dur)` | `set(1e-4, t)`; `exp → max(2e-4, peak) @ t+a`; `exp → 1e-4 @ t + max(a+0.01, dur)`. |
| `swellEnv(g, t, a, peak, dur)` | `set(1e-4, t)`; `lin → max(2e-4, peak) @ t+a`; `exp → 1e-4 @ t + max(a+0.02, dur)`. |
| `send(node, o)` | `o.dst` set → connect to it only (ambience). Else, `o.pan` set → `StereoPanner(pan)`; connect to `fx` and `fxWet`. |

DS (`OCB:735-743`): `dest(g)` = connect to `out` and `wet`; `env` identical; `noiseSrc(t, dur)` = loop the 1 s noise from `(turn * 0.137) % 0.8` (`turn` = the play counter, so every burst of one play shares one offset); `osc` = `O`; `filt(type, f, q)` sets `Q = q || 0.7`. Inside a DS timbre `p_tone()` returns the playing preset's `tone` (`curTone`, set in `S.play`).

WebAudio semantics the port must reproduce: a `setValueAtTime` event holds until the next event; a ramp starts from the previous event's time and value; `exponentialRampToValueAtTime` is geometric between them; `setTargetAtTime(v, T0, τ)` is `v + (V0 - v)·e^{-(t-T0)/τ}` with `V0` the param's current computed value; `cancelScheduledValues(t)` drops every event at or after `t`. Connections into a param add to its intrinsic value (LFOs). Mono into a stereo node up-mixes as L = R = mono (no −3 dB); `StereoPanner` on mono input is equal-power `x = (pan+1)/2, gL = cos(xπ/2), gR = sin(xπ/2)` (so pan 0.12 gives L 0.6374, R 0.7705: a panned voice is about 3 dB under an unpanned one).

---

## 2. Every named sound event

Columns: vol (0..1, before effects/master), pitch Hz, dur ms, tone (0..1). "end" = the latest `stop()` of the voice in seconds (render length before the 1.4 s room tail) — used by the length tests in §8.

### 2.1 The 29 battle presets (DS, `OCB:671-701`)

| Preset | Timbre | vol | pitch | dur | tone | Wobble | end s |
| --- | --- | --- | --- | --- | --- | --- | --- |
| hover 札にホバー | paper | 0.05 | 3400 | 26 | 0.5 | ±3 % | 0.076 |
| grab 札を掴む | paper | 0.16 | 1500 | 80 | 0.4 | ±3 % | 0.13 |
| frameIn 枠に入る | wood | 0.11 | 980 | 42 | 0.5 | ±3 % | 0.092 |
| frameOut 枠から出る | wood | 0.06 | 560 | 40 | 0.3 | ±3 % | 0.09 |
| noReach 届かない | thud | 0.07 | 150 | 70 | 0.2 | — | 0.12 |
| releaseAttack | swish | 0.18 | 900 | 150 | 0.5 | — | 0.20 |
| hitSlash | slash | 0.30 | 2600 | 190 | 0.6 | — | 0.24 |
| hitThrust | thrust | 0.30 | 1900 | 140 | 0.6 | — | 0.19 |
| hitBlunt | blunt | 0.32 | 120 | 170 | 0.4 | — | 0.22 |
| releaseDefense | paper | 0.14 | 1100 | 90 | 0.3 | ±3 % | 0.14 |
| guardUp 盾の輪 | metal | 0.16 | 1240 | 340 | 0.45 | — | 0.39 |
| releaseSkill | paper | 0.14 | 1300 | 90 | 0.4 | ±3 % | 0.14 |
| seal 状態が付く | seal | 0.22 | 150 | 140 | 0.5 | — | 0.148 |
| releaseStance | paper | 0.12 | 900 | 110 | 0.3 | ±3 % | 0.16 |
| stance 型が立つ | string | 0.20 | 98 | 700 | 0.4 | — | 0.75 |
| cancel | slide | 0.10 | 700 | 120 | 0.2 | — | 0.17 |
| refusal | wood | 0.09 | 300 | 34 | 0.2 | ±3 % | 0.084 |
| draw (per card) | paper | 0.07 | 2200 | 60 | 0.5 | ±3 % | 0.11 |
| discard (per card) | paper | 0.06 | 1600 | 55 | 0.4 | ±3 % | 0.105 |
| rebuild | slide | 0.12 | 900 | 220 | 0.4 | — | 0.27 |
| pip 回復の粒 | chime | 0.06 | 1180 | 260 | 0.3 | — | 0.31 |
| breath 構え・吸う | breath | 0.12 | 900 | 360 | 0.4 | — | 0.41 |
| reserve 構え・盾 | metal | 0.14 | 1240 | 300 | 0.4 | — | 0.35 |
| foeTurn | string | 0.18 | 73 | 900 | 0.3 | — | 0.95 |
| omenDrop | string | 0.14 | 73 | 700 | 0.3 | — | 0.75 |
| hitGuard | metal | 0.24 | 620 | 420 | 0.6 | — | 0.47 |
| guardBreak | crack | 0.24 | 700 | 300 | 0.6 | — | 0.35 |
| hitBody | body | 0.30 | 90 | 220 | 0.4 | — | 0.27 |
| heartbeat | heart | 0.16 | 55 | 160 | 0.3 | — | 0.37 |

DS timbres (`OCB:744-822`). Notation: `N(t,d)` = DS noise burst, `O(type,f,t,d)` oscillator, `BP/LP/HP(f, Q)` biquads (Q 0.7 unless given), `env(a, peak, d)` from the voice's t, `→out` = `dest`. Every frequency sweep is a param event on the filter/oscillator.

| Timbre | Graph |
| --- | --- |
| paper(t,p,d,v) | `N(t,d) → BP(p, 0.8+2·tone) → HP(500) → g env(0.004, v, d) →out` |
| slide | `N(t,d) → BP(p, 0.6)` [freq `set 1.3p@t`, `exp → 0.7p@t+d`] `→ g` [`set 1e-4@t`, `lin → v@t+0.3d`, `exp → 1e-4@t+d`] `→out` |
| wood | `O(tri, p, t, d)` [`exp → 0.82p@t+d`] `→ g env(0.002, v, d) →out`; `N(t, 0.02) → HP(2500) → g2 env(0.001, 0.35v, 0.018) →out` |
| thud | `O(sine, p, t, d)` [`exp → 0.55p@t+d`] `→ g env(0.003, v, d) →out` |
| swish | `N(t,d) → BP(0.5p, 1.4)` [`set 0.45p@t`, `exp → 1.7p@t+d`] `→ g` [`set 1e-4@t`, `exp → v@t+0.6d`, `exp → 1e-4@t+d`] `→out` |
| slash | `N(t,d) → BP(p, 2.2)` [`set p@t`, `exp → 0.28p@t+0.8d`] `→ g env(0.003, v, d) →out`; `thud(t+0.01, 120, 0.14, 0.7v)` |
| thrust | `N(t,d) → BP(p, 3) → g env(0.002, 0.8v, 0.6d) →out`; `thud(t, 170, d, v)` |
| blunt | `thud(t, p, d, v)`; `N(t, 0.08) → LP(900) → g env(0.002, 0.6v, 0.08) →out` |
| metal | partials `[ratio, amp, durFactor]` = `[1,1,1] [2.76,.45,.7] [5.4,.25,.45] [8.93,.12,.3]`: `O(sine, p·ratio, t, d) → g env(0.002, v·amp·(ratio>1 ? 0.6+tone : 1), d·durFactor) →out`; `N(t, 0.02) → HP(4000) → g2 env(0.001, 0.3v, 0.02) →out` |
| crack | `metal(t, p, 0.6d, 0.7v)`; `N(t, d) → HP(1800) → g env@t+0.02 (0.002, 0.8v, 0.5d) →out` |
| seal | `thud(t, p, 0.7d, v)`; `wood(t+0.012, 1500, 0.025, 0.5v)` |
| string | `O(saw, p, t, d) + O(tri, 1.003p, t, d) → LP(10p, 1.2)` [`set 10p@t`, `exp → 1.6p@t+0.7d`] `→ g env(0.006, v, d) →out` |
| chime | `O(sine, p, t, d) → g env(0.006, v, d)`; `O(sine, 2.01p, t, 0.5d) → g2 env(0.004, 0.25v, 0.5d)`; both `→out` |
| breath | `N(t,d) → BP(p, 0.7)` [`set 0.7p@t`, `lin → 1.2p@t+d`] `→ LP(2200) → g` [`set 1e-4@t`, `lin → v@t+0.75d`, `exp → 1e-4@t+d`] `→out` |
| body | `thud(t, p, d, v)`; `N(t, 0.5d) → LP(420) → g env(0.003, 0.8v, 0.5d) →out` |
| heart | `thud(t, p, d, v)`; `thud(t+0.16, 0.9p, d, 0.7v)` |

`S.play(name, delayMs, mult)` (`OCB:826-837`): returns silently if muted, unknown, or not unlocked; **hover** is throttled to one per 50 ms of context time (`lastAt.hover && now - lastAt.hover < 0.05`); `turn++`; `t = now + delayMs/1000`; wobble `((turn % 3) - 1) * 0.03` only for timbres `paper` and `wood`; `vol × (mult || 1)` (note `||`: a mult of 0 plays at 1; no caller passes 0).

### 2.2 The 27 meta presets (`Audio.js:55-83`)

Groups (`META_GROUPS` 49): explore 探索, reward 報酬, life 生と継承, ui 操作.

| Preset | Group | Timbre | vol | pitch | dur | tone | gap | end s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| nodeStep 新しいノードへ進む | explore | step | 0.22 | 110 | 260 | 0.5 | — | 0.318 (glass 0.35) |
| nodeReturn 踏んだノードへ戻る | explore | step | 0.11 | 120 | 180 | 0.3 | — | 0.238 (glass 0.35) |
| nodeHover ノードにホバー | explore | paper | 0.035 | 2600 | 22 | 0.4 | 60 ms | 0.072 |
| journalOpen 手記を開く | explore | page | 0.12 | 1500 | 280 | 0.5 | — | 0.33 |
| journalClose 手記を閉じる | explore | close | 0.10 | 1000 | 200 | 0.3 | — | 0.32 |
| rest 休む | explore | exhale | 0.15 | 760 | 1200 | 0.3 | — | 1.25 |
| train 訓練する | explore | drill | 0.16 | 900 | 170 | 0.5 | — | 0.3264 |
| infoGained 情報を得る | explore | quill | 0.13 | 2300 | 460 | 0.4 | — | 0.4793 |
| eventResolve イベントが決まる | explore | settle | 0.12 | 210 | 520 | 0.4 | — | 0.59 |
| traceTake 痕跡を拾う | explore | ember | 0.085 | 523.25 | 1700 | 0.4 | — | 1.75 |
| consumableUse 消耗品を使う | explore | pouch | 0.12 | 1200 | 260 | 0.4 | — | 0.31 |
| miasmaLine 瘴気 20% の線 | explore | swell | 0.11 | 55 | 2400 | 0.3 | — | 2.45 |
| miasmaDeath 瘴気 100% | explore | petrify | 0.14 | 49 | 3400 | 0.3 | — | 3.45 |
| layerCleared 層の主を倒した | explore | toll | 0.12 | 98 | 3600 | 0.3 | — | 3.65 |
| interlude 階層間の休憩 | explore | hum | 0.05 | 110 | 2600 | 0.3 | — | 2.65 |
| descend 次の層へ降りる | explore | fall | 0.12 | 98 | 2400 | 0.35 | — | 2.45 |
| rewardReveal 報酬が並ぶ | reward | slide | 0.11 | 1000 | 260 | 0.4 | — | 0.31 |
| rewardPick 報酬を選ぶ | reward | paper | 0.15 | 1250 | 90 | 0.4 | — | 0.14 |
| cardAdded デッキに札が入る | reward | tuck | 0.12 | 1100 | 220 | 0.4 | — | 0.215 |
| lifeEndDeath 倒れて生が終わる | life | expire | 0.15 | 55 | 4400 | 0.25 | — | 4.45 |
| survived 生きたまま生を閉じる | life | relief | 0.12 | 440 | 2200 | 0.35 | — | 2.294 |
| hallArrive 継承の間に着く | life | kindle | 0.12 | 2400 | 1500 | 0.4 | — | 1.55 |
| heirNamed 襲名 | life | nameSeal | 0.18 | 98 | 1500 | 0.4 | — | 1.55 |
| titleEnter タイトルから入る | life | bow | 0.075 | 73.42 | 2800 | 0.3 | — | 2.85 |
| uiClick ボタンを押す | ui | wood | 0.07 | 760 | 30 | 0.4 | 40 ms | 0.08 |
| uiConfirm 決める | ui | seal | 0.10 | 170 | 110 | 0.4 | — | 0.127 |
| uiCancel やめる・戻る | ui | slide | 0.08 | 620 | 110 | 0.2 | — | 0.16 |

Counts: explore 16 + reward 3 + life 5 + ui 3 = 27 presets; `META_TIMBRES` (21-46) labels 24 timbres; `SYN` has 26 functions (those 24 plus `thud` and `swish`, used inside others). With the 29 battle presets that is 56 named sounds. Each preset also carries `label`, `spec`, `role` (動作 / 結果 / 動作＋結果 / —) as UI text for the dev page.

Meta timbres (`Audio.js:342-576`). Same notation; `N(...)` is the meta noise (§1.3), `→send` = `send(g, o)`; nested calls pass `tone` and `o` through.

| Timbre | Graph |
| --- | --- |
| paper | as DS, Q `0.8+2·tone`, meta noise |
| slide | as DS but `lin → pk(v)` |
| wood | as DS (noise `N(t, 0.02)`) |
| thud | as DS |
| seal | `thud(t, p, 0.7d, v)`; `wood(t+0.012, 1500, 0.025, 0.5v)` |
| swish | as DS but `exp → max(2e-4, v)` |
| step (surface s, §2.3) | `thud(t, p·s.thud, 0.55d, v·s.heel)`; `N(t+0.008, d, pink) → F(s.type, s.f·(0.7+0.6·tone), s.q) → g env@t+0.008 (0.006, v·s.scuff, d) →send`; if `s.crackle`: for i = 0..2, `at = t+0.016+0.027i`: `N(at, 0.012) → BP(1.3·s.f, 2) → env@at (0.001, v·s.crackle·(1-0.25i), 0.012)`; if `s.wet`: `N(t+0.02, 0.035) → BP(1100, 3) → env@t+0.02 (0.002, v·s.wet, 0.035)`; if `s.ring`: `O(sine, 1900, t, 0.3) → env@t+0.004 (0.003, v·s.ring, 0.28)` |
| page | `N(t,d) → BP(0.6p, 0.9+tone)` [`set 0.6p@t`, `exp → 1.5p@t+0.7d`] `→ am → g`; `am = G(0.6)` with `O(sine, 19, t, d) → G(0.4) → am.gain` (gain 0.6±0.4); `g` [`set 1e-4@t`, `lin → pk(v)@t+0.35d`, `exp → 1e-4@t+d`] `→send` |
| close | `N(t,d) → BP(1.2p, 0.7)` [`set 1.2p@t`, `exp → 0.5p@t+0.8d`] `→ g` [`set 1e-4@t`, `lin → pk(0.7v)@t+0.25d`, `exp → 1e-4@t+0.85d`] `→send`; `thud(t+0.75d, 140, 0.12, 0.55v)` |
| quill | `s = d/3`; for i = 0..2, `at = t + 0.9·s·i`: `N(at, s) → BP(p(1.08-0.07i), 2.5+2·tone)` [`set p(1.08-0.07i)@at`, `lin → p(0.9-0.07i)@at+0.8s`] `→ g swell@at (0.3s, v(1-0.18i), 0.85s) →send` |
| exhale | `N(t,d,pink) → BP(1.25p, 0.7)` [`set 1.25p@t`, `exp → 0.55p@t+d`] `→ LP(1400+1200·tone) → g swell(0.18d, v, d) →send` |
| drill | `swish(t,p,d,v)`; `k = t+0.92d`: `wood(k, 300, 0.06, 0.8v)`; `thud(k, 140, 0.12, 0.6v)` |
| settle | `thud(t, p, 0.5d, v)`; `N(t+0.02, d, pink) → LP(500+500·tone) → g swell@t+0.02 (0.04, 0.3v, d) →send` |
| ember | `lp = LP(p(2.5+2·tone), 0.5) → G(1) →send`; partials `[m, a, dd]` = `[1,1,1] [2,.28,.6] [3,.1,.4]`: `O(sine, p·m, t, d·dd) → g env(0.02, v·a, d·dd) → lp`; `N(t, 0.6d, pink) → LP(600) → g2 swell(0.25d, 0.25v, 0.6d) →send` |
| pouch | `N(t, 0.13) → BP(p, 0.7) → am → g`; `am = G(0.6)` + `O(sine, 28, t, 0.13) → G(0.4) → am.gain`; `g swell(0.03, 0.7v, 0.13) →send`; `thud(t+min(0.12, 0.5d), 170, max(0.08, d-0.12), 0.6v)` |
| swell | sum `g` ← `O(sine,p)·0.45 + O(sine,1.012p)·0.45 + O(tri,2.02p)·0.6 + O(sine,3.01p)·0.12` (all `t, d`) `+ N(t,d,brown) → LP(180+300·tone, 0.8) → G(0.6)`; `g swell(0.55d, v, d) →send` |
| petrify | `lp = LP(2200+1200·tone, 0.7) → G(1) →send`; 9 ticks, `at` starts at `t` and after tick i grows by `0.32·0.78^i` (offsets 0, .32, .5696, .7643, .9161, 1.0345, 1.1269, 1.1989, 1.2551 s): `N(at, 0.012) → BP(1700+350·(i%3), 3) → env@at (0.001, 0.22v(0.6+0.05i), 0.012) → lp`; then the same 4 oscillators as swell `+ N(t,d,brown) → LP(220, 0.8) → G(0.7)` into `g` [`set 1e-4@t`, `lin → v@t+0.62d`, `lin → 1e-4@t+0.66d`] `→send` (the swell stops short) |
| toll | `lp = LP(p(5+6·tone), 0.5) → G(1) →send`; partials `[m, a, dd]` = `[.5,.5,1] [1,1,.8] [1.19,.45,.55] [1.5,.3,.45] [2,.35,.35] [2.66,.16,.25]`: `O(sine, p·m, t, d·dd) → g env(0.004, v·a, d·dd) → lp`; `N(t, 0.05, pink) → LP(900) → env(0.002, 0.3v, 0.05) →send` |
| hum | `lp = LP(p(4+6·tone), 0.5)`; `[m,a]` = `[1,1] [1.5,.55] [2,.25]`: `O(sine, p·m, t, d) → G(a) → lp`; `lp → g swell(0.4d, v, d) →send` |
| fall | `lp = LP(10p(0.6+tone), 1.0)` [`set` same `@t`, `exp → 2.2p@t+0.85d`]; `[type,f,a]` = `[saw,p,1] [tri,1.004p,1] [sine,0.5p,0.6]`: `O(type,f,t,d)` [`exp → 0.42f@t+0.9d`] `→ G(a) → lp`; `lp → g env(0.05, v, d) →send`; `N(t,d,brown) → LP(160) → gn swell(0.3d, 0.4v, d) →send` |
| tuck | `slide(t, p, min(0.15,d), 0.8v)`; `wood(t+min(0.13, 0.6d), 0.48p, 0.035, 0.7v)` |
| expire | `thud(t, p, 0.18, 0.9v)`; `thud(t+0.24, 0.9p, 0.2, 0.6v)`; `t2 = t+0.7`, `d2 = max(0.5, d-0.7)`: `lp = LP(9p(0.6+tone), 0.6)`; `O(tri, 2p, t2, d2)`, `O(sine, p, t2, d2)` each [`exp → 0.7f@t2+d2`] `→ lp → g swell@t2 (0.6, 0.8v, d2) →send` |
| relief | `exhale(t, 700, 0.6d, v)`; `ember(t+0.42d, p, 0.6d, 0.8v)` |
| kindle | `N(t, 0.09) → BP(p, 1.4)` [`set 0.7p@t`, `exp → 1.3p@t+0.07`] `→ env(0.003, 0.7v, 0.08) →send`; `t2 = t+0.11`, `d2 = max(0.3, d-0.11)`: `N(t2,d2,pink) → LP(520+300·tone) → swell@t2 (0.25, 0.8v, d2) →send`; `O(sine,110,t2,d2) → gw`, `O(sine,165,t2,d2) → G(0.5) → gw`; `gw swell@t2 (0.4·d2, 0.25v, d2) →send` |
| nameSeal | `seal(t, 120, 0.16, v)`; `t2 = t+0.22`, `d2 = max(0.4, d-0.22)`: `lp = LP(10p, 1.1)` [`set 10p@t2`, `exp → 1.8p@t2+0.7d2`]; `O(saw,p,t2,d2) + O(tri,1.003p,t2,d2) → lp → g env@t2 (0.02, 0.6v, d2) →send` |
| bow | `lp = LP(3p, 1.1)` [`set 3p@t`, `lin → p(5+6·tone)@t+0.4d`, `exp → 2.5p@t+d`]; `O(saw,p) + O(saw,1.005p) + O(tri,0.5p)→G(0.5)` (all `t,d`) `→ lp → g` [`set 1e-4@t`, `lin → pk(v)@t+0.35d`, `lin → pk(0.8v)@t+0.6d`, `exp → 1e-4@t+d`] `→send`; `N(t,d,pink) → BP(9p, 1.2) → gn swell(0.35d, 0.12v, 0.9d) →send` |

### 2.3 Footstep surfaces (`SURFACES`, `Audio.js:86-95`)

| Surface | Places | thud | heel | filter | f Hz | Q | scuff | extra |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| stone 石 | title, hall, interlude | 1.0 | 0.8 | bandpass | 900 | 0.8 | 0.45 | — |
| litter 落ち葉 | layer1 | 0.85 | 0.6 | bandpass | 2600 | 0.6 | 0.55 | crackle 0.45 |
| sand 石畳の砂 | layer2 | 1.05 | 0.7 | highpass | 2800 | 0.7 (dB) | 0.5 | — |
| damp 湿った岩 | layer3 | 0.9 | 0.9 | lowpass | 650 | 1.2 (dB) | 0.5 | wet 0.4 |
| glass 結晶の床 | layer4 | 1.25 | 0.7 | bandpass | 1500 | 1.0 | 0.3 | ring 0.16 |
| salt 塩の殻 | layer5 | 1.0 | 0.7 | bandpass | 1900 | 1.3 | 0.5 | crackle 0.65 |
| gravel 砂利 | layer6 | 0.95 | 0.8 | bandpass | 1300 | 0.9 | 0.6 | crackle 0.35 |
| void 床が無い | layer7 | 0.65 | 0.6 | lowpass | 320 | 0.7 (dB) | 0.3 | — |

The current surface is set by `setAmbience` (`surface = def.surface`; `null` keeps the last one).

### 2.4 Play rules (meta `play`, `Audio.js:713-737`)

1. Refused (returns `false`): muted, destroyed, unknown name. Battle names (`battleNames` = keys of `OCB.SOUND_DEFAULTS`) go to the DS `base.play(name, delayMs, mult)` unchanged and return `unlocked` (no gap, no pan, no pitchMul).
2. Meta names: refused before unlock; **gap** (`pr.gap` ms) measured on the context clock from the last *accepted* call of the same name (`lastAt[name]` = call time, not start time). A refused call does not advance any counter.
3. `turn++` (meta counter, separate from the DS `turn`).
4. `step` timbre: `stepTurn++`; `pan = stepTurn % 2 ? 0.12 : -0.12` (first step +0.12 = right) unless the caller gave one; `surface` = current place's unless given.
5. Wobble `((turn % 3) - 1) * 0.03` for timbres `paper`, `wood`, `step` (by the preset's timbre, so `uiConfirm`'s inner wood does not wobble).
6. `t = now + max(0, delayMs)/1000`; `pitch = pr.pitch × (1+wob) × (o.pitchMul || 1)`; `vol = pr.vol × (mult ?? 1)`; `dur = pr.dur/1000`.

Pitch, filters and durations are all inside the synthesis: **a pitch change is not a playback-rate change** (fixed-frequency filters such as `paper`'s HP 500 and the step surfaces stay put). The Unity port must render pitch variants, never use `AudioSource.pitch`.

### 2.5 The two-sounds budget

- **Explore step** — `stepPlan({fresh, line, miasmaDeath, result})` (`Audio.js:166-170`): action = `fresh ? nodeStep : nodeReturn`; result = `miasmaDeath ? 'miasmaDeath' : line ? 'miasmaLine' : result || null`. `stepTo` (797-803) plays the action now and the result at `RESULT_AFTER_STEP_MS = 280`; `miasmaLine` goes through `miasmaLine(line, 280)`.
- **Miasma line** — `miasmaLine(pct, delayMs)` (805-808): `i = miasmaLineIndex(pct)` = `round(pct/20) - 1` clamped 0..3 (non-finite → 0); plays `miasmaLine` with `mult = 1 + 0.1i` and `pitchMul = [1, 0.891, 0.794, 0.707][i]` (a whole tone lower and 10 % louder per line).
- **Battle** — per depiction event (`JS/src/scenes/battle/player.js:230-238, 375-394, 443-451, 1594-1595`): `beginPlan` sets `budget = 2`, `essentialLeft = min(2, essentialSoundsOf(ev))` where essential = each `Slash` cue, each `Hit` cue on the player, each `GuardBlock` not immediately followed by a `Hit` on the same target. `sfxBudget(name, essential)`: `budget <= 0` → drop; essential → `essentialLeft = max(0, essentialLeft-1)`; incidental with `budget - essentialLeft <= 0` → drop; else `budget--` and play. A card play (`playAccepted`) sets `budget = 1` (the hand's release sound is the action) and clamps `essentialLeft`. Essential names: `hitSlash/hitThrust/hitBlunt` (by `HIT_SOUND`: Slash→hitSlash, Thrust→hitThrust, Sweep→hitSlash, Strike→hitBlunt, 204-209), `hitBody` (or `hitBlunt` for a wall hit on the player), `hitGuard`, `guardBreak`. Incidental: `guardUp`, `seal`, `stance`, `breath`, `reserve`. Outside the budget: `heartbeat`, `foeTurn`, `omenDrop`, `pip`, `rebuild`, `refusal`, `uiClick`, `hover`, and all hand sounds.
- **Composite timbres** have at most two parts (drill, pouch, tuck, expire, relief, kindle, nameSeal, close, petrify).

### 2.6 Throttles outside the synth

| Where | Rule |
| --- | --- |
| DS `S.play` | `hover` at most once per 50 ms (context clock) |
| `hand.js:47, 293` | `hover` only if ≥ `HOVER_SOUND_GAP_MS = 50` real ms since the last |
| `BattleScene.js:33-47` `safeAudio` | never throws; while fast-forward is held, the same name within `L.FF_SOUND_GAP_MS = 90` real ms (`layout.js:125`) of the last (measured at `now + delayMs`) is dropped |
| meta presets | `nodeHover` gap 60 ms, `uiClick` gap 40 ms |
| `util.js:13-21` `soundOf(ctx)` | the meta scenes' wrapper; never throws |

Sound delays are always **real-time** ms added to the audio clock (`ctx.currentTime`), never the scaled battle clock. Keep that in Unity (`AudioSettings.dspTime`).

---

## 3. Ambience and rooms

### 3.1 Places (`AMBIENCE_DEFS`, `Audio.js:101-112`)

| id | place | surface | level | room send | measured (AUDIO.md, LUFS at master 1, ambience 1) |
| --- | --- | --- | --- | --- | --- |
| title | 住処の口 | stone | 0.5 | 0.5 | −36 |
| hall | 継承の間 | stone | 0.7 | 0.3 | −37 |
| interlude | 階層間 | stone | 0.64 | 0.45 | −36.5 |
| layer1 | 燦光の樹海 | litter | 1.0 | 0.25 | −36.5 |
| layer2 | ■■の秘跡 | sand | 0.87 | 0.55 | −36 |
| layer3 | 大黒蛇の住処 | damp | 0.5 | 0.7 | −34 |
| layer4 | 輝晶の間 | glass | 1.2 | 0.6 | −37.5 |
| layer5 | 血染めの死海 | salt | 0.81 | 0.3 | −36 |
| layer6 | 獄雷峡 | gravel | 0.86 | 0.5 | −36 |
| layer7 | 果てなき世界 | void | 0.57 | 0.8 | −34.5 |

`ambienceId(id)` (147-154): `null`, `''`, `'none'` → `null` (silence); a number or digit string 1..7 → `'layerN'` (other numbers → invalid); a known id → itself; anything else → invalid (`setAmbience` returns `false`).

### 3.2 How a layer is built and driven (`Audio.js:579-693`)

- `makeLayer(id)`: `L.gain = G(0.0001) → amb`; `L.mix = G(1) → L.gain`; `L.gain → sendRoom = G(def.room) → ambWet`; `L.rnd = lcg(hashSeed(id))` with FNV-1a `h = 2166136261; h ^= charCode; h = imul(h, 16777619) >>> 0`. Voices route with `o = { dst: L.mix }` (no fx, no pan).
- Toolkit `K` (594-651), all into `L.mix`:
  - `bed(color, {type='lowpass', f, q=0.7, gain, pan=0, fLfo, gLfo})`: looping BufferSource on the color's 4 s buffer → `F(type, f, q)` → `G(gain)` → mix; each `[rate, depth]` in `fLfo`/`gLfo` is a sine `O` at `rate` Hz started at `now` → `G(depth)` → the filter's `frequency` / the gain's `gain` (added to the intrinsic value). Started at `start(now, (L.nodes.length * 0.61) % 3.5)` where `L.nodes` already holds this bed's source and its LFO oscillators (order: source, fLfo oscillators, gLfo oscillators, then start). No builder uses `pan`.
  - `drone(type, f, gain, {lp, gLfo})`: `O` (kept) `→ [LP(lp, 0.7)] → G(gain)` → mix, gain LFOs as above, started at `now`, runs until the layer is killed.
  - `every(lo, hi, fn)`: sparse voice; first `next = now + 0.5·lo + 0.5·(hi-lo)·rnd()` (consumed at build time, in builder order).
  - `tone(t, f, v, a, d, type='sine', lp=0)`: `O(type, f, t, a+d) → [LP(lp, 0.7)] → g swellEnv(t, a, v, a+d)`.
  - `tick(t, f, q, v, d)`: `N(t, d) white → BP(f, q) → g env(t, 0.001, v, d)`.
  - `drip(t, f, v)`: `O(sine, f, t, 0.12)` [`exp → 0.45f@t+0.06`] `→ g env(t, 0.002, v, 0.09)`.
  - `thud(t, f, v, d)` = meta `thud(t, f, d, v, tone 0.3, {dst})`.
  - `creak(t, f, v)`: `O(saw, 44, t, 0.45)` [`exp → 27@t+0.42`] `→ BP(f, 5) → g swellEnv(t, 0.06, v, 0.42)`.
  - `scrape(t, f, v, d)`: `N(t, d, pink) → BP(f, 1.6)` [`set 0.8f@t`, `lin → 1.15f@t+d`] `→ g` [`set 1e-4@t`, `lin → v@t+0.5d`, `lin → 1e-4@t+d`].
  - `rumble(t, v, f, a, d)`: `N(t, a+d, brown) → LP(1.6f, 0.9)` [`set 1.6f@t`, `exp → 0.7f@t+a+d`] `→ g swellEnv(t, a, v, a+d)`.
  - `glide(t, f0, f1, v, d)`: `O(sine, f0, t, d)` [`exp → f1@t+d`] `→ g`; `O(tri, 2f0, t, d)` [`exp → 2f1@t+d`] `→ G(0.2) → g`; `g swellEnv(t, 0.4d, v, d)`.
- Builders (856-961), exact values. Beds: `color type f Q gain {fLfo [Hz ±depth]} {gLfo}`; drones: `type f gain`; sparse: `every lo–hi s: voice`. `r()` = `L.rnd`, and arguments evaluate left to right (the C# must call `r()` in the same order, including `i*(…r())` when `i = 0`).

| Place | Beds | Drones | Sparse |
| --- | --- | --- | --- |
| title | pink LP 380 Q0.6 g0.07 fLfo [0.071 ±160][0.023 ±90] gLfo [0.031 ±0.025]; brown LP 140 g0.08 | sine 73.42 g0.016 gLfo [0.05 ±0.006]; sine 110.3 g0.006 | 14–22: `tone([146.83,110,130.81][k%3], 0.010, a1.6, d4.5, triangle, lp600)` |
| hall | brown LP 240 g0.06; pink BP 820 Q0.7 g0.006 gLfo [0.83 ±0.002][2.3 ±0.0015] | sine 73.42 0.012; sine 110 0.0065 gLfo [0.037 ±0.002]; sine 146.83 0.003 | 16–30: `creak(190, 0.010)` |
| interlude | pink LP 520 g0.05 gLfo [0.12 ±0.022] | sine 55 0.014; sine 82.41 0.008 gLfo [0.045 ±0.003] | 9–15: `tone([220,293.66,246.94,329.63][k%4], 0.006, 1.1, 3.6)` |
| layer1 | pink LP 900 g0.03 fLfo [0.043 ±320]; white BP 3400 Q0.5 g0.010 gLfo [0.052 ±0.006][0.13 ±0.003] | sine 110 0.003 | 2.5–7: n = 1+k%3 ticks, `tick(t + i(0.07+0.05r()), 2200+1400r(), 2.5, 0.005, 0.018)`; 10–18: `tone([659.26,783.99,880][k%3], 0.0035, 0.5, 2.6)` |
| layer2 | pink BP 340 Q6 g0.05 fLfo [0.029 ±38] gLfo [0.047 ±0.018]; pink BP 512 Q8 g0.025 fLfo [0.041 ±30] gLfo [0.061 ±0.012]; white BP 6500 Q0.6 g0.0018 gLfo [0.09 ±0.0018] | sine 110 0.007; sine 110.55 0.007; sine 165 0.003 | 14–26: `tone([1567.98,1396.91][k%2], 0.0022, 0.02, 1.8, sine, lp3000)`; 20–34: `thud(t, 68, 0.03, 0.5)` + `tick(t+0.03, 1800, 0.8, 0.004, 0.25)` |
| layer3 | brown LP 190 g0.10 gLfo [0.09 ±0.05] | saw 41.2 0.018 lp150; sine 82.41 0.005 | 3–9: `drip(t, 1100+700r(), 0.012)`; 18–32: `scrape(t, 380, 0.012, 2.6)` |
| layer4 | white BP 6200 Q0.8 g0.0025 | sine 523.25 / 524.1 / 784 / 785.3 / 1046.5, each 0.0016 with gLfo [0.07+0.03i ±0.0014] (i = 0..4, built first); sine 220 0.004; sine 329.63 0.0025 | 7–13: `tone([783.99,880,1174.66,1318.51][k%4], 0.004, 1.5, 4.2)` |
| layer5 | pink LP 650 g0.05 gLfo [0.035 ±0.022][0.11 ±0.008]; white BP 620 Q14 g0.02 fLfo [0.021 ±70] gLfo [0.058 ±0.012] | sine 49 0.012; sine 73.42 0.004 | 5–12: n = 3+k%4 ticks, `tick(t + i(0.03+0.06r()), 2600+900r(), 2, 0.0035, 0.008)`; 22–36: `glide(t, 65.41, 51.91, 0.010, 3.2)` |
| layer6 | pink BP 260 Q1.2 g0.07 gLfo [0.05 ±0.03][0.017 ±0.015]; white BP 1800 Q3 g0.004 gLfo [0.07 ±0.003] | sine 46.25 0.016; saw 61.74 0.006 lp140 | 13–26: `tick(t, 2400, 1, 0.003, 0.09)`; `rumble(t+0.25+0.6r(), 0.07, 180, 0.4+0.5r(), 3.2+1.8r())` |
| layer7 | brown LP 120 g0.09 gLfo [0.023 ±0.03]; pink BP 400 Q1.5 g0.03 fLfo [0.06 ±240] (built after the drones) | sine 55 0.012; sine 55.35 0.012; sine 82.6 0.004; saw 27.5 0.01 lp90; sine 164.81 0.004 gLfo [0.031 ±0.003]; sine 165.3 0.004 | 16–28: `f = [196,174.61,155.56][k%3]`, `glide(t, f, 0.94f, 0.009, 3.4)` |

Build order inside each builder is the source order (it fixes node counts, hence the bed start offsets, and the `rnd()` sequence).

- Scheduler `tick()` (679-693), every `TICK_MS = 250` real ms and once at every `startLayer`: `now = currentTime`, `horizon = now + LOOKAHEAD_S (1.5)`; for each non-stopping layer, for each sparse entry in order: if `next < now - 1` then `next = now + 0.05` (no burst after a suspend); `while (next < horizon) { fn(max(next, now + 0.02), k, rnd); k++; next += lo + (hi-lo)·rnd(); }`. A voice that throws is skipped (`errors++`).
- `startLayer(id, fadeMs)` (655-662): `f = max(0.05, fadeMs/1000)`; `L.gain: set(1e-4, now)`, `lin → def.level @ now+f`.
- `stopLayer(L, fadeMs)` (670-677): `cancel(now)`, `set(current value, now)`, `lin → 0 @ now+f`; `killLayer` after `fadeMs + 150` ms stops every kept node and disconnects `L.gain`.
- `setAmbience(id, fadeMs = 1800)` (739-751): invalid → `false`; `surface` follows; same id with a live layer → no restart; before unlock it only records the id; else stop every layer (crossfade, all old layers fade over the same `fade`) and start the new one. `null` fades everything out.
- `unlock()` (774-782): starts the pending ambience with a **2500 ms** fade.
- `duckAmbience(factor, fadeMs = 600)` (811-814): `duck = clamp01(factor)`; `ambDuck.gain: cancel(now)`, `setTarget(duck, now, τ = max(0.01, fadeMs/3000))` (τ 0.2 s by default). Not persisted.

### 3.3 Rooms and the impulse

| Room | Used by | Length | Envelope power | Seed | Pre-filter | Send |
| --- | --- | --- | --- | --- | --- | --- |
| DS damp room | battle presets | 1.4 s | 3.2 | 7 | LP 2400 Q **1** (dB) | `wet` gain = reverb (0.18) |
| meta fx room | meta presets | 1.4 s | 3.2 | 7 | LP 2400 Q 0.7 (dB) | `fxWet` gain = reverb (0.18) |
| ambience cave | ambience | 2.6 s | 2.4 | 13 | LP 1800 Q 0.7 (dB) | per layer `def.room`, post-fade |

Impulse: `len = floor(sr·seconds)`, stereo, `s = seed`, for channel 0 then 1 (the state carries over), for each i: `s = (s·16807) mod 2147483647; ir[ch][i] = ((s/2147483647)·2 − 1)·(1 − i/len)^power`.

**ConvolverNode normalisation is on by default and must be ported** (WebAudio spec `calculateNormalizationScale`): `power = sqrt(Σ_ch Σ_i ir² / (channels·len))`, `power = max(power, 0.000125)` (and non-finite → 0.000125), `scale = (1/power) · 0.00125 · (44100/sr)` (× 0.5 only for 4-channel IRs). The IR is multiplied by `scale`. For the 1.4 s room at 48 kHz the scale is ≈ 0.0054; without it the reverb would be ≈ 45 dB too loud. A mono input with a stereo IR gives `L = in*irL, R = in*irR`; a stereo input gives `L = inL*irL, R = inR*irR` (mono voices into the wet bus are up-mixed to L = R first, so both cases are the same formula).

### 3.4 Compressor (both contexts)

`DynamicsCompressor`, threshold −18 dB, ratio 4, knee 30 dB, attack 3 ms, release 250 ms (WebAudio defaults except threshold and ratio). It matters for two things: the automatic **makeup gain** of the spec, `(1/curve(1.0))^0.6` ≈ 1.135 (+1.1 dB on everything; Chromium knee `y = thr + (1 − e^{−k(x−thr)})/k` with `k ≈ 0.588` found by `KAtSlope(1/ratio)` at `threshold + knee` = +12 dB), and the soft limiting of the few loud peaks (AUDIO.md: `guardBreak` peaks at +1.8 dBFS at master 1 without it).

---

## 4. Call sites (who plays what, and when)

Every scene receives `ctx.audio` (the `createAudio` object). Unity scene ports keep the same names and calls; the table is the checklist for the other areas.

| Area / file | Event → sound |
| --- | --- |
| `JS/src/app/main.js:22-24` | `createAudio(OCB, settings, { onChange: persist })`, `bindUnlock(document)` |
| `main.js:216` `toTitle` | `setAmbience('title', 1200)` |
| `main.js:264` `toHall` | `setAmbience('hall', 1500)` |
| `main.js:331` `resumeFight` | `setAmbience(layer, 1200)` |
| `main.js:423, 433, 465` `runEncounter` | `duckAmbience(BATTLE_DUCK = 0.4)` before the fights, `duckAmbience(1)` after |
| `main.js:183, 507` stale reload / save-quit | `duckAmbience(1)` |
| `main.js:497` life ended | `setAmbience('hall', 2000)` |
| `main.js:84` settings overlay | `onChange` → persist + `audio.applySettings(settings)` |
| `ExploreScene.js:837` every render | `setAmbience(interlude ? 'interlude' : layer)` (default 1800 ms) when it changes |
| `ExploreScene.js:550` | node hover (id changes) → `nodeHover` |
| `:565` / `:592` | node pinned → `uiClick`; keyboard pin → `nodeHover` |
| `:1054` | a trial card opens → `rewardReveal` (Swap) else `uiClick` |
| `:1068, 1614` | result plate closes: learned → `cardAdded`; declined offer → `uiCancel` |
| `:1079, 1698, 1788, 2058` | a card / pick / training row chosen → `rewardPick` |
| `:1133` | event result shows → `rewardReveal` (with offer) else `eventResolve`, at +80 ms |
| `:1141, 1157` | confirm card opens `uiClick`; cancelled `uiCancel` |
| `:1173-1176` | trial action: TakeFound `consumableUse`, ChooseSurvey `infoGained`, AcceptEvent `eventResolve`, other `uiCancel` |
| `:1213` | GoToInterlude → `interlude` |
| `:1226` | ConfirmEndLife → `uiConfirm` |
| `:1270-1278` `afterMove` | `stepTo({ fresh, line, miasmaDeath: fc.IsLethal, result })`, `line = MaxStaminaPenalty(fc.MiasmaAfter)·StepPercent` only when max stamina drops and not lethal; `result` = Rest node → `train`/`rest` by choice, Survey/Carving → `infoGained`, Event → `eventResolve` (`STEP_RESULT` 70), none for a walked node; a jump also plays `consumableUse` |
| `:1397-1398` | consumable: aiming toggled → `uiClick` / `uiCancel`, used → `consumableUse` |
| `:1405, 1410` `afterDescend` | `descend`; miasma death after it → `miasmaDeath` at +600 |
| `:1620` | reward closed in LayerCleared → `layerCleared` at +200 |
| `:1725` | battle result with cards → `rewardReveal` at +120 |
| `:1759, 1779-1781, 1800` | trace take → `traceTake` (picks) else `uiCancel`; leave asks `uiClick` / `uiCancel`; cursor `nodeHover` |
| `:2010, 2018` | train done → `uiConfirm`; later → `uiCancel` |
| `:2198-2209` | overlay: journal → `journalOpen` … `journalClose`; other overlays `uiClick` |
| `:2297` | Esc stops aiming → `uiCancel` |
| `meta/title.js:58, 64, 91, 111-112, 158` | menu focus: pointer `nodeHover`, keys `uiClick`; leave: `titleEnter` (when entering) + `uiConfirm` together; settings `uiClick` |
| `meta/hall.js:101, 283, 349, 370, 392, 401, 421` | gear `uiClick`; trace select (pointer `uiClick`, keys `nodeHover`); arrival `hallArrive` at +80; seal stamped `heirNamed` (when the 170 ms seal tween ends at `1250 + 110·nameChars + 240` ms); to title `uiCancel`; depart `uiConfirm` |
| `meta/departure.js` | filters `uiClick` (215, 223); card hover / page / scroll `nodeHover` (253, 293, 373, 579, 581, 798-799); choose `uiClick` (320, 427); deck replaced `rewardReveal` (460); add `cardAdded` / full `refusal` (466, 472); remove `uiCancel` (478, 533, 567); supply add `consumableUse` / refused `refusal` (536-537); talent `uiConfirm` (594); to supplies `uiConfirm` (664); back `uiCancel` (681, 699); set out `uiConfirm` (711); settings `uiClick` (726); Enter on an invalid deck `refusal` (796, 800) |
| `meta/ending.js:102-103, 142` | Fallen → `lifeEndDeath` at +300; survived (not died) → `survived` at +300; miasma death silent; to hall `uiConfirm` |
| `meta/journal.js:211, 314, 320` | page turn `nodeHover`; mount `journalOpen`; close `journalClose` |
| `meta/owned.js:96, 172, 181, 193, 203, 208` | card hover `nodeHover`; select `uiClick`; page `nodeHover`; mount `journalOpen`; close `journalClose` |
| `meta/settings.js:62, 75, 107, 140, 164, 169, 180-181` | segment change / slider step `uiClick` (after applying the new volume); save-quit `uiConfirm`; open `uiClick`; close `uiCancel`; ↑↓ focus `nodeHover` |
| `battle/hand.js` | `hover` (293), `frameIn` / `noReach` (Single aim + `face.ReachHint`) / `frameOut` (384-385), `grab` (414, 520, 711), `refusal` + `refusal` at +70 (shake) or `cancel` (471), `cancel` (530), `draw` per card at its stagger start (961), `discard` per card (985), release by kind: Attack `releaseAttack`, Guard `releaseDefense`, Stance `releaseStance`, else `releaseSkill` (39, 1010) |
| `battle/player.js` | `heartbeat` ×3 at 0/900/1800 ms on TurnStart when 0 < HP < 15 (972); `foeTurn` on TurnEnd (986); `pip` per stamina point at `k·ms(StaminaRecover)` for the player (1080); `seal` on StatusApply with Spirit fx (1092, budget); `omenDrop` (1106); `stance` on TraitFire with Form fx (1114, budget); `guardUp` (1147, budget); `breath` then `reserve` (1178-1180, budget); `hitGuard`/`guardBreak` (1252, essential); `hitBody`/`hitBlunt` on the player (1283, essential); system hit (1458 enemy at the lunge; 1466/1469 player after the hit stop, essential); `rebuild` (1498); `refusal` (1785, 2272, 2278); `uiClick` (1807 free step, 2122 speed); `hover` on free-step focus (1851) |
| `battle/hud.js:150`, `hud/chrome.js:230`, `hud/piles.js:130, 168` | `uiClick` |

---

## 5. Settings, volume and mute

### 5.1 Values

- Module defaults `SOUND_SETTINGS_DEFAULTS = { master 0.8, effects 1.0, ambience 0.6, reverb 0.18, muted false }` (`Audio.js:115`).
- **The game's defaults are the app's** (`JS/src/app/save.js:10-17`): `{ speed 1.25, shortFx false, master 0.8, effects 0.9, ambience 0.6, muted false }`, stored under `ocb.settings.v1`. `loadSettings` keeps only keys of `DEFAULT_SETTINGS` (`save.js:138`), so **reverb is never persisted** and is always 0.18 in the game. The Unity port uses effects 0.9.
- `normalizeSettings` (134-144): each volume clamped to 0..1, non-finite → default; `muted` only if boolean.
- Settings panel (`meta/settings.js:85-128`): sliders Master / Effects / Ambience round to 0.05 (`Math.round(v·20)/20`), show 0..100; ← → step ±0.05; mute is a segmented row.

### 5.2 How each volume acts

| Setting | Effect | Ramp |
| --- | --- | --- |
| master | meta `master` gain; DS `out` = master × effects | meta: `cancel; setTarget(v, now, 0.03)`; DS: immediate |
| effects | meta `fx` gain; DS `out` = master × effects | τ 0.03 / immediate |
| ambience | `amb` gain | τ 0.03 |
| reverb | meta `fxWet`; DS `wet` | τ 0.03 / immediate |
| muted | meta `master → 0` (τ 0.03), and 400 ms later `ctx.suspend()` (time stops; resumed on unmute); `play()` returns false; DS `base.muted` refuses new battle sounds but its graph keeps running | — |

`onChange(fn)` listeners receive the snapshot after every setter; `applySettings(partial)` merges, normalises, ramps everything and re-applies mute. `toggleMute()` returns the new state.

### 5.3 Levels to hit (acceptance; AUDIO.md:119-130, measured offline with node-web-audio-api, BS.1770 K-weighted max momentary loudness, master 1)

- Battle reference: hits −26 to −29 LUFS; `foeTurn`, `stance` ≈ −20.5.
- Big moments: `descend` −19.4, `miasmaDeath` −20.4, `lifeEndDeath` −19.7, `layerCleared` −21, `heirNamed` −22.6, `miasmaLine` −22.7, `titleEnter` −23.5.
- Results: `traceTake` −24.7, `survived` −25.3, `interlude` −26, `eventResolve` −31.
- Map: `nodeStep` −35, `nodeReturn` −42, `nodeHover` −61; UI −38 to −48.
- Ambience: per place in §3.1 (−34 to −37.5). Above 4 kHz under 3 % of the power except layer 1 (14 %).

### 5.4 Stale doc versus code (the code wins)

1. AUDIO.md:33 says `duckAmbience(0.5)` in battle; the code uses `BATTLE_DUCK = 0.4` (`main.js:423`).
2. AUDIO.md:83 says a rest node's step has no result; the code passes `rest` / `train` as the step's result (`ExploreScene.js:1273`).
3. AUDIO.md:23 default effects 1.0; the game starts at 0.9 (§5.1).

### 5.5 Doubled sounds found in the JS

- Explore `J` / 手記: `ExploreScene.js:2198` plays `journalOpen` and the mounted journal plays it again (`journal.js:314`); the same for `journalClose` (2205/2209 and 320). The two bursts have different noise offsets, so the open is about 3 dB louder in explore than in battle (battle fixed this, `BattleScene.js:227`, BATTLE_SCENE.md §4.4).
- Explore 手持ちの札 overlay: `uiClick` (2198) then `owned.js:203` `journalOpen` (two different sounds, allowed).
- Explore settings overlay: `uiClick` twice (2198, `settings.js:164`); the second falls inside `uiClick`'s 40 ms gap and is dropped, so only one sounds.

**[DEVIATION, recommended]** In Unity, the explore scene plays nothing for the journal overlay (the journal plays its own, as in battle). If the architect prefers strict JS parity, keep both calls; the port supports either.

---

## 6. Proposed C# design

### 6.1 Principles

1. **One engine-free synthesis library, `Trial.Audio.Synth`** (netstandard2.1, `noEngineReferences: true`), that is a small WebAudio subset with the same API shape (`CreateGain`, `CreateBiquadFilter`, `CreateOscillator`, `CreateBufferSource`, `CreateStereoPanner`, `CreateConvolver`, `AudioParam.SetValueAtTime/…`, `Connect`, `Start/Stop`). The DS and meta timbres and the ambience builders are then **line-by-line ports** of the JS (same order of node creation and param calls), which makes them diffable and lets a test compare the C# graph-call log with a golden log produced from the real JS.
2. **One-shots are pre-rendered** (dry + room tail baked at the current reverb) into stereo float PCM, then `AudioClip.Create` + `SetData`, and played by an `AudioSource` pool with `PlayScheduled(dspTime + delay)`. The reverb is a convolution and must have zero latency against the hit, which an offline render gives for free.
3. **The ambience runs live** in the same engine on the audio thread (`OnAudioFilterRead`): it is endless, its LFO rates do not divide each other and its sparse voices come from a seeded schedule, so a loop would repeat audibly or cost tens of MB per place. The cave convolution runs with a 4096-frame partition; the dry ambience is delayed by the same 4096 frames, so the wet/dry alignment is exact (the whole ambience lags 85 ms, which nothing depends on).
4. **The master stage sits on the AudioListener** (`OnAudioFilterRead` on the listener's GameObject processes the whole mix): master gain (τ 30 ms, 0 when muted) then the compressor port.
5. Pitch variants (wobble, `pitchMul`) and `mult` are rendered, never `AudioSource.pitch`.

### 6.2 `Trial.Audio.Synth` — files (KIT `View/Trial/Audio/Synth/`, namespace `Trial.Audio.Synth`)

| File | ~Lines | Contents (JS source) |
| --- | --- | --- |
| `Rng.cs` | 60 | `Lcg` (`lcg`, Audio.js:173), `ParkMiller.Step(ref long s)` (raw `s*16807 % 2147483647` for rooms and the DS noise), `HashSeed(string)` (FNV-1a, 181-188). Use `long` arithmetic. |
| `AudioBuffers.cs` | 140 | `MakeNoise(sr, seed, NoiseColor)` (242-266, Float32 storage, peak on the double), `MakeDsNoise(sr)` (OCB:726-728), `MakeRoom(sr, seconds, power, seed)` (267-280 / OCB:716-721), `ConvolverNormalizationScale(ir, sr)` (§3.3). |
| `AudioParam.cs` | 220 | Intrinsic `Value`; event list (set / linear / exponential / target / cancel) with spec semantics (§1.3); `ComputedValue` (last rendered sample, used by `setTarget` V0 and by `stopLayer`); a-rate fill of a 128-frame quantum; summed modulation inputs from nodes; `IsConstant` fast path. Exponential ramps with a zero or sign-changing start hold the start value (spec). |
| `AudioNodes.cs` | 240 | `AudioNode` (inputs, per-quantum cached output, channel count 1 or 2, up-mix mono→stereo L = R), `GainNode`, `StereoPannerNode` (equal-power, mono and stereo input formulas of the spec), `DestinationNode`, `ScheduledSourceNode` base (start/stop on the first frame ≥ the time). |
| `BiquadFilterNode.cs` | 150 | Spec formulas: `w0 = 2πf/sr` (f clamped to [0, sr/2]); lowpass/highpass `α = sin w0 / (2·10^{Q/20})` (**Q in dB**); bandpass `α = sin w0 / (2Q)`; LP `b = [(1−c)/2, 1−c, (1−c)/2]`, HP `b = [(1+c)/2, −(1+c), (1+c)/2]`, BP `b = [α, 0, −α]`, `a = [1+α, −2c, 1−α]`, normalised by a0. Coefficients per sample while `frequency`/`Q` are automated or modulated, cached otherwise. Per-channel state (stereo for the wet buses). Default `Q = 1` (the DS wet low-pass relies on it). |
| `OscillatorNode.cs` | 110 | `sine` = `Math.Sin` on a double phase starting at 0; `triangle`/`sawtooth` read band-limited tables; a-rate `frequency`. |
| `WaveTables.cs` | 140 | Chromium-like PeriodicWave: table size 4096 (sr ≤ 88.2 kHz), 3 ranges per octave (36 ranges), lowest fundamental `(sr/2)/2048`, partials per range `2048·2^{−range/3}`, coefficients sawtooth `b_n = 2/(πn)·(−1)^{n+1}`, triangle `b_n = 8 sin(πn/2)/(πn)²`, built by inverse FFT, scaled by `1/max|table_0|`; read with linear interpolation inside a table and a blend between the two adjacent ranges (`pitchRange = 1 + cents/400`). |
| `BufferSourceNode.cs` | 80 | Loop over the whole buffer, `Start(t, offsetSeconds)`, `Stop(t)`, mono. |
| `Fft.cs` | 130 | Iterative radix-2 complex FFT with precomputed twiddles, real-signal helpers. |
| `ConvolverNode.cs` | 170 | Stereo IR, spec normalisation, uniformly partitioned overlap-add with block `B` (4096 default), frequency-domain delay line, mono or stereo input; `LatencyFrames = B`. Also `static float[][] ConvolveOffline(float[][] input, float[][] ir)` that trims the latency, used by the renderer. |
| `AudioContextLite.cs` | 180 | `SampleRate`, `CurrentTime`, render quantum 128, `Create*`, `Destination`, `Render(float[] interleaved, int frames)`; voice groups (`BeginVoice/EndVoice`) whose terminal connections are dropped `0.2 s` after their last stop, so live graphs do not grow; optional `ICallLog` (records `[nodeId, param, method, args rounded 1e-6]` in the same format as the JS mock, `Audio.test.mjs:35`). |
| `DynamicsCompressor.cs` | 140 | Static curve (threshold −18, knee 30, ratio 4; `KAtSlope` with 15 geometric bisection steps between 0.1 and 10000, `SlopeAt` with `x·1.001`), makeup `(1/curve(1))^0.6`, per-sample detector `max(|L|,|R|)` → target gain `curve(x)/x`, attack τ 3 ms / release τ 250 ms on the gain, 6 ms look-ahead delay; `Process(float[] interleaved, int frames, int channels)`. A simplification of Chromium's kernel (no adaptive release zones); target error under 0.5 dB on the presets. |
| `SoundData.cs` | 230 | `DsTimbres` labels, `DsSoundDefaults` (29), `MetaTimbres` (24), `MetaGroups`, `MetaSoundDefaults` (27, incl. `Gap`), `Surfaces` (8), `AmbienceDefs` (10), `SoundSettingsDefaults`, `GameSettingsDefaults` (effects 0.9), constants `MiasmaLinePitch {1, .891, .794, .707}`, `ResultAfterStepMs 280`, `LookaheadS 1.5`, `TickS 0.25`, `NoiseSeconds 4`, `BattleDuck 0.4`. A `Preset` record `{ Label, Group, Spec, Role, Timbre, Vol, Pitch, Dur, Tone, Gap }`. 29 + 27 presets. |
| `DsSound.cs` | 200 | Port of `OCB:744-822` (`Paper`, `Slide`, `Wood`, `Thud`, `Swish`, `Slash`, `Thrust`, `Blunt`, `Metal`, `Crack`, `Seal`, `String`, `Chime`, `Breath`, `Body`, `Heart`) with `Env`, `NoiseSrc(t, dur)` (offset from the play's `turn`), `Osc`, `Filt`, `Dest`. |
| `MetaSynth.cs` | 360 | Port of `Audio.js:309-576`: `G`, `F`, `O`, `N` (global `noiseTurn`), `Pk`, `Env`, `SwellEnv`, `Send`, and the 26 timbre functions (`Paper`…`Bow`, incl. `Thud`, `Swish`) with the same argument order `(t, p, d, v, tone, o)`. `o` = `VoiceRouting { AudioNode Dst; double? Pan; string Surface; double PitchMul }`. |
| `SoundRules.cs` | 160 | `NormalizeSettings`, `AmbienceId(object)` → `(bool ok, string id)`, `MiasmaLineIndex`, `StepPlan(fresh, line, miasmaDeath, result)`, `Wobble(turn)`, `StepPan(stepTurn)`, `GapGate` (per-name last time), `SoundBudget` (`Begin(essential, budget = 2)`, `BeginCardPlay()`, `TryPlay(bool essential)` — the battle rule of §2.5, pure), `VariantKey` (§6.4). |
| `PresetRenderer.cs` | 170 | `RenderedSound Render(SoundRequest r, int sampleRate)`: fresh context, voice at t = 0 into a dry stereo bus, `wet = Conv(LP(dry))` with the battle or meta room, `out = dry + reverb·wet`; `Frames = ceil(end·sr) + roomLen`; reports `Peak`, `End`. `static double VoiceEnd(...)` for the table in §2. |
| `AmbienceBuilders.cs` | 230 | Port of `Audio.js:594-651` (toolkit `K`: `Bed`, `Drone`, `Every`, `Tone`, `Tick`, `Drip`, `Thud`, `Creak`, `Scrape`, `Rumble`, `Glide`) and `856-961` (10 builders). |
| `AmbienceEngine.cs` | 270 | The live ambience graph of §1.2 (`amb`, `ambDuck`, `ambWet → LP 1800 → cave`, dry delay = cave latency), layers (`MakeLayer`, `StartLayer`, `StopLayer`, `KillLayer` at `fade + 150 ms` of engine time), the scheduler (`Tick` every 0.25 s of engine time and at `StartLayer`, horizon 1.5 s, burst guard), `SetAmbience(id, fadeMs = 1800)`, `Duck(factor, fadeMs = 600)`, `SetVolume(v)` (τ 0.03), `Freeze(bool)` (the suspend), `Surface`, and `Render(float[] data, int frames, int channels)` that drains a command queue first (thread-safe; never touches Unity). Deterministic when driven offline. |

Total ≈ 3,200 lines of library.

### 6.3 Unity side — files (KIT `View/Trial/Audio/`, namespace `Trial.Audio`, in the Trial view assembly)

| File | ~Lines | Contents |
| --- | --- | --- |
| `TrialAudio.cs` | 330 | Port of the `createAudio` object (`Audio.js:754-849`), PascalCase: `Play(name, delayMs = 0, double? mult = null, PlayOptions o = null) → bool`, `Has`, `StepTo(StepInput) → string[]`, `MiasmaLine(pct, delayMs)`, `SetAmbience(object id, double fadeMs = 1800) → bool`, `DuckAmbience(factor, fadeMs = 600)`, `SetMaster/SetEffects/SetAmbienceVolume/SetReverb/SetMuted/ToggleMute/ApplySettings`, `Settings`, `OnChange(Action<SoundSettings>) → IDisposable`, `Unlock()`, `Destroy()`, `Presets`, `MetaNames`, `BattleNames`, `Surface`, `Ambience`. Keeps the DS `turn`, meta `turn`, `stepTurn` and gap times (real clock `Time.realtimeSinceStartupAsDouble`). Implements the battle area's `IBattleAudio` (`Play(name, delayMs, mult)`), so `SafeAudio` wraps it. |
| `SfxClipCache.cs` | 200 | `VariantKey → AudioClip`; a background queue (`Task.Run` → `PresetRenderer.Render`) filled at `Unlock` in priority order (ui and title → hall → stone steps → explore → reward → battle → the long ones); the main thread drains finished renders in `Update` (`AudioClip.Create(key, frames, 2, sr, false)` + `SetData`, at most 4 per frame); a missing variant is rendered synchronously on the main thread (≤ 150 ms for the longest); on a surface change the 12 step variants of the new surface are queued at the top; `SetReverb` re-renders everything (old clips keep playing until replaced); `Dispose` destroys the clips. |
| `SfxPlayer.cs` | 200 | `AudioSource` pool of 24 (2D, `spatialBlend 0`, `playOnAwake false`, `loop false`, `priority 64`); `Play(AudioClip, double dspTime)` with `PlayScheduled`; when all are busy, steals the one that started earliest; `volume = effects`, smoothed toward the target in `Update` with τ 30 ms. |
| `AmbienceHost.cs` | 110 | `MonoBehaviour` + `AudioSource` (a 1-frame silent looping clip, `spatialBlend 0`, `priority 0`, `Play()` at start); `OnAudioFilterRead(float[] data, int channels)` → `AmbienceEngine.Render` (renders stereo, folds to mono or writes L/R into the first two channels for other layouts); commands go through the engine's queue. Rebuilds the engine on `AudioSettings.OnAudioConfigurationChanged`. |
| `MasterBus.cs` | 110 | On the `AudioListener`'s GameObject (adds a listener if the bootstrap has none): `OnAudioFilterRead` → master gain (one-pole τ 30 ms toward `muted ? 0 : master`) → `DynamicsCompressor.Process`. `volatile` targets written from the main thread. |
| `Editor/TrialAudioWindow.cs` | 260 | Port of `dev/audio.html` for koudai-san to listen in Play Mode: sliders (master, effects, ambience, reverb 0..0.6), mute, defaults, ambience buttons with the fade slider (0-5000, step 100, default 1800), stop, duck toggle (0.45, 600 ms), the 7-node step strip (戦 調 事 休 痕 戦 主; miasma starts at 12 %, +9 % per new node, lines 20/40/60/80), the 10 step cases, one button per meta event by group and per battle preset, and the 40 s run-through (the `at(ms, label, fn)` list of `audio.html:251-271`, verbatim). Plus "Export WAVs": writes every preset render to `RPG-by-card/Builds/AudioRenders/*.wav` (16-bit stereo) for side-by-side listening against the browser. |
| `Tests/PlayMode/TrialAudioSmokeTests.cs` | 90 | Creates the listener, `TrialAudio`, plays every preset and every ambience, waits 1 s each, asserts no exception, the master filter ran, the pool never exceeded 24, mute stops new plays. |

Total ≈ 1,300 lines.

### 6.4 Variants and the clip cache

`VariantKey = (name, w, surface, pan, mult, pitchMul, reverb)` where `w ∈ {0, 1, 2}` is the wobble class `turn % 3` for timbres that wobble (paper, wood, step) and 1 otherwise. A cached render uses `turn = w` for the DS noise offset `(w·0.137) % 0.8` and `noiseTurn = 16·w` as the meta burst counter start.

- **[DEVIATION]** In the browser every play reads a different noise segment; the cache reuses three. The bursts are white/pink noise, statistically identical, so this is inaudible; the wobble alternation (the audible part) is exact.
- Boot set: battle 10 wobbling presets × 3 + 19 = 49; meta `nodeHover`, `rewardPick`, `uiClick` × 3 = 9, `nodeStep`/`nodeReturn` × 3 wobbles × 2 pans for the current surface = 12, `miasmaLine` × 4 lines = 4, the other 21 × 1 → 95 clips, ≈ 195 s of stereo float at 48 kHz ≈ 75 MB (if Unity keeps created clips as float). Rendering cost ≈ 4 GFLOP, a few seconds on a worker thread in the Mono Editor, under a second in IL2CPP. Every further surface adds 12 clips.

### 6.5 Threading and timing

- Main thread: `TrialAudio`, `SfxClipCache` (clip creation), `SfxPlayer`. Background: renders only (pure library, no shared state except the input request).
- Audio thread: `AmbienceHost.OnAudioFilterRead` and `MasterBus.OnAudioFilterRead`. The engine allocates only when a sparse voice is built (a few small objects every few seconds); everything per sample uses preallocated buffers.
- `Play` computes `at = AudioSettings.dspTime + delayMs/1000` once per call, so a step and its result stay exactly 280 ms apart. Gaps and the hover throttles use the real clock.
- Mute: master target 0 (τ 30 ms), `Play` returns false, 400 ms later `AmbienceEngine.Freeze(true)` (time stops, output zeros); unmute unfreezes. **[DEVIATION]** In the browser a battle sound already ringing at the moment of mute keeps ringing (the DS has no mute on its graph); in Unity the master stage silences it. The intent of mute wins.
- **[DEVIATION]** The browser has two compressors (battle context, meta+ambience context); Unity has one on the summed mix. The ambience sits about 13 dB under the hits, so the shared detector changes the hits by well under 0.5 dB.
- Unity has no autoplay policy: the bootstrap calls `Unlock()` at start, and the router's first `SetAmbience('title', 1200)` fades the title in over 1.2 s (the browser waits for the first click and then uses 2.5 s). `Unlock()` keeps the JS rule: a pending place starts with a 2500 ms fade.

### 6.6 Tests

dotnet projects (pattern of `UP/UiArt.Script*.csproj`): `UP/Trial.Audio.Synth/Trial.Audio.Synth.csproj` (netstandard2.1, LangVersion 9.0, links `..\unity-project-kit\Assets\View\Trial\Audio\Synth\*.cs`), `UP/Trial.Audio.Synth.Tests/Trial.Audio.Synth.Tests.csproj` (net10.0, NUnit 4.3.2, links `…\Synth\Tests\*.cs`, copies `…\Synth\Tests\golden\*.json`), both added to `UP/UnityCorePort.slnx`. Unity asmdefs `Trial.Audio.Synth` (`noEngineReferences: true`) and `Trial.Audio.Synth.Tests` (Editor, `UNITY_INCLUDE_TESTS`, nunit) like `KIT/UiArt/`.

| Test file | ~Lines | Checks |
| --- | --- | --- |
| `RngAndBuffersTests.cs` | 120 | LCG sequences and `HashSeed("layer3")` equal the JS; the first and last 64 samples and the energy of the white/pink/brown buffers, the DS noise and the three IRs equal the golden (1e-7); the room normalisation scale. |
| `GraphPrimitiveTests.cs` | 190 | Param automation values at chosen frames (set, linear, exponential, target, cancel + set of the current value); biquad magnitude at f0 versus the formula for each type (Q in dB for LP/HP); band-limited saw has no partial above Nyquist and peaks at 1; buffer loop with offset; panner gains 0.6374 / 0.7705 at pan 0.12; a unit impulse through the convolver returns `scale·IR`; compressor static gain at 0 dBFS ≈ 0.8096·1.135. |
| `PresetRenderTests.cs` | 170 | Every one of the 56 presets renders at 48 kHz: `Frames == ceil(end·sr) + floor(1.4·sr)` with `end` from §2; no NaN; peak in (0, 1.6); two renders are identical; the wobble variants differ; a delayed start is silent before its time; each surface changes `nodeStep`. Loudness (K-weighted max momentary, master 1, effects 1, reverb 0.18, compressor on) within ±1.5 dB of §5.3 (`Loudness.cs` ~90 lines: BS.1770 48 kHz pre-filter `b = [1.53512485958697, −2.69169618940638, 1.19839281085285]`, `a = [1, −1.69065929318241, 0.73248077421585]`; RLB `b = [1, −2, 1]`, `a = [1, −1.99004745483398, 0.99007225036621]`; 400 ms windows every 100 ms; `−0.691 + 10·log10(ΣL² + ΣR²)/n`). |
| `GraphParityTests.cs` | 130 | dotnet-only (`#if !UNITY_5_3_OR_NEWER`, uses System.Text.Json): for every preset (and every surface for the steps), the C# `ICallLog` of one play at t = 0 equals the golden JS log (node ids renumbered from the first node created by that play; numbers rounded 1e-6). Ambience: for every place, `SetAmbience(id, 20)` then 60 s of `Tick` at 0.25 s steps equals the golden. |
| `AmbienceTests.cs` | 170 | Every place builds, fires every sparse entry at least once in 60 s, crossfades (two layers during the fade, one after `fade + 150 ms`), the same id does not restart, `null` fades to silence, the surface follows, same calls give the same PCM hash, freeze stops time; loudness per place (60 s render) within ±1.5 dB of §3.1, marked `[Category("Slow")]`. |
| `SoundRulesTests.cs` | 90 | `NormalizeSettings` cases of `Audio.test.mjs:304-305`, `AmbienceId`, `MiasmaLineIndex`, `StepPlan` priority table, `SoundBudget` (essential first, incidental only on a free slot, card play budget 1), gaps 40 / 60 ms. |

Golden generator `UP/tools/audio-golden.mjs` (~160 lines, Node, no packages): loads `JS/src/scenes/audio/Audio.js` and `JS/vendor/ds/bundle.js` (paths as arguments, read-only), injects a recording mock derived from `Audio.test.mjs:23-122` at **sampleRate 48000**, unlocks, plays each preset with fresh counters (`turn`, `noiseTurn` and `stepTurn` set to the cached variant's values), dumps the per-play logs, the noise buffers' and IRs' first/last 64 samples and energies, and the ambience logs, to `KIT/Trial/Audio/Synth/Tests/golden/audio-golden.json`. **Run it once while the browser-game copy exists (it lives in a session scratchpad) and commit the JSON.**

---

## 7. Implementation units

1. **audio-synth** — `Trial.Audio.Synth` engine, data, DS + meta timbres, rules, renderer, compressor, the dotnet projects, the golden tool, and the tests except the ambience ones (≈ 2,700 + 800 test/tool lines).
2. **audio-ambience-unity** — `AmbienceBuilders.cs`, `AmbienceEngine.cs` + `AmbienceTests.cs`, and the Unity side (`TrialAudio`, `SfxClipCache`, `SfxPlayer`, `AmbienceHost`, `MasterBus`, the editor window, the PlayMode smoke test) (≈ 500 + 1,300 + 260 test lines). Depends on unit 1 and on the bootstrap providing a camera with an `AudioListener` and on the app router calling `SetAmbience` / `DuckAmbience`.

---

## 8. What the other areas need from audio (interfaces)

- `TrialAudio` instance in every scene context (`ctx.Audio`), plus a never-throwing wrapper (`SafeAudio` in battle, `SoundOf(ctx)` in meta) that swallows exceptions.
- Router calls of §4 (`main.js` rows), with the same fade values; `DuckAmbience(0.4)` around fights and `DuckAmbience(1)` after, on save-quit and on a stale reload.
- Settings: the router persists `{ master, effects, ambience, muted }` (with `speed`, `shortFx`) and calls `ApplySettings` on change; the settings panel calls `SetMaster/SetEffects/SetAmbienceVolume/SetMuted` and subscribes to `OnChange`.
- The battle player keeps the budget (`SoundBudget` + its own `EssentialSoundsOf(ev)` over the depiction cues); the hand keeps its 50 ms hover throttle; the scene keeps the 90 ms fast-forward de-dupe.
- The explore scene passes `StepTo` the forecast values (`fc.MaxStaminaAfter/Before`, `fc.IsLethal`, `fc.MiasmaAfter`, `Miasma.MaxStaminaPenalty`, `Miasma.StepPercent` exist in C#) and the node kind/choice; the trial-only action names (`TrialAction`, `TrialCardKind.Swap`, `LifeEnding.Fallen`) have no C# counterpart yet (grep under `UP/`: 0 hits) and come from the explore/meta ports.

---

## 9. Risks

- **Audio-thread C# cost and GC** in the Mono Editor (live ambience: per-sample biquad coefficients for LFO-swept beds, three wavetable oscillators, cave convolution ≈ 60 complex MACs per frame at B 4096). Measure with the Profiler's Audio module; if it is heavy, raise B to 8192 or render the cave at half rate.
- **Clip memory and pre-render time** (≈ 95 clips, ≈ 75 MB if stored as float; seconds of background work in Mono). Fallback: render one-shots live in the ambience engine too (the library supports it), which needs a low-latency head partition for the room.
- **No ears in the loop**: implementers cannot listen. Correctness rests on the golden graph logs, the DSP unit tests and the loudness table; koudai-san listens through the editor window and the WAV export against `dev/audio.html`.
- The golden tool depends on the browser-game copy in a temporary scratchpad; generate the JSON early.
- Easy-to-miss WebAudio details: lowpass/highpass Q in dB, the DS wet low-pass Q = 1 default, convolver normalisation (×44100/sr), exponential-ramp anchoring to the previous event, `setTarget` starting from the computed value, equal-power panning −3 dB at centre, Float32 rounding of noise buffers.
- Oscillator tables approximate Chromium's (node-web-audio-api, used for the AUDIO.md levels, has its own); timbre differences of sawtooth/triangle are small and lowpassed in every use.
- The compressor is a simplification; levels may differ by up to ~0.5 dB on the loudest peaks.
- Unity real voices are 32 (`AudioManager.asset`); the pool of 24 plus the ambience source stays under it, but dense battle turns (6 draws + 6 discards with 1.4 s tails) will steal the oldest voices.
- Sample rate: renders follow `AudioSettings.outputSampleRate`; noise buffers and normalisation depend on it, tests pin 48 kHz.
- Open decision for the architect: the doubled journal open/close in explore (§5.5).
