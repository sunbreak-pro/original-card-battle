# The game's sound

`src/scenes/audio/Audio.js` holds all of the game's sound. It wraps the design system's battle synth (`OCB.Sound`, 29 battle presets, unchanged) and adds the exploration and meta events plus a quiet ambience for every place. Everything is synthesised with WebAudio. There are no audio files.

Sources: the design system's sound rules (`ds/project/sound.md`, from `battle_ui_ux_v2.md` §5.20), the seven layers and their look (`world-v1.md` §1, §3, §4, §8), the exploration screen (`dungeon_exploration_v4.md` §6), and the consumables in `DungeonContent/ItemCatalogue.cs`.

## Rules

- **One named preset per event.** Each preset has the same shape as `OCB.SOUND_DEFAULTS`: `{ label, spec, role, timbre, vol, pitch, dur, tone }`. The meta presets also carry `group`, and an optional `gap` (the shortest time in ms between two plays of the same event). Battle and meta presets share one live map, `audio.presets`.
- **At most two sounds per action**: the action sound, then the result sound. A composite timbre has at most two parts (for example `drill` = the swing, then the knock). `stepTo()` plays the step and at most one result.
- **Dark, quiet, damp.** There are no fanfares and no bright electronic beeps. The only high sounds are paper and small lights, the same as in battle. Meta sounds go through the same short damp room as the battle synth. The ambience has its own longer, darker cave.
- **No randomness.** Repeated paper, wood and step sounds alternate their pitch by ±3% using a counter. Steps also alternate left and right. Noise buffers and the timing of ambience events come from a seeded Park-Miller LCG, so a given sequence of calls always produces the same WebAudio calls. A test checks this.
- **Nothing sounds before a user gesture.** `unlock()` creates the audio graph and starts the pending ambience. `bindUnlock(document)` does this on the first pointer or key press.

## API

`createAudio(OCB, settings, opts?)` returns an object that keeps the whole `OCB.Sound` API and adds the rest:

- Kept from `OCB.Sound`: `play(name, delayMs, mult)`, `unlock()`, `muted` (read/write), `setMaster(v)`, `setReverb(v)`, `presets`, `reset()`. `audio.base` is the wrapped `OCB.Sound` object. Battle names go to it unchanged.
- `play(name, delayMs, mult, { pitchMul, pan, surface })` plays a meta event. It returns `false` when it stays silent (muted, before unlock, unknown name, or inside its `gap`).
- `stepTo({ fresh, line, miasmaDeath, result })` plays one step on the map, following the two-sounds rule (see below). It returns the planned names. `miasmaLine(pct, delayMs)` plays the swell for one 20% line.
- `setAmbience(id, fadeMs = 1800)` crossfades to `title`, `hall`, `interlude`, `layer1`..`layer7` (or `1`..`7`). `null` fades to silence. The footstep surface follows the place. `duckAmbience(factor, fadeMs)` lowers the ambience temporarily (for example during battle) and is not persisted.
- Settings: `setEffects(v)`, `setAmbienceVolume(v)`, `setMuted(b)`, `toggleMute()`, `applySettings(partial)`, `settings` (a snapshot), and `onChange(fn)`, which returns an unsubscribe function. The caller persists the snapshot `{ master, effects, ambience, reverb, muted }`, all 0..1. `normalizeSettings()` cleans a stale or partial blob. The defaults are master 0.8, effects 1.0, ambience 0.6, reverb 0.18, not muted.
- Volumes: master scales everything. Effects scales the battle and meta sounds (the battle synth's master is set to master × effects). Ambience scales only the ambience. Mute fades the master to 0, and 400 ms later suspends the meta/ambience context to save battery.
- `destroy()` stops the ambience and closes the context.

In Node there is no `window` or `AudioContext`. The module still imports, and every call is a silent no-op. `opts.AudioContext` lets tests inject a mock.

## Who calls what

| Scene | Calls |
| --- | --- |
| app / router | `createAudio(window.OCB, save.settings, { onChange: persist })`, `bindUnlock(document)`, `setAmbience()` on every scene change (title → `title`, 継承の間 and 出立 → `hall`, map and battle of layer N → `N`, interlude → `interlude`), `duckAmbience(0.5)` while a battle is mounted |
| title, 継承の間 | `titleEnter`, `hallArrive`, `heirNamed`, `uiClick` / `uiConfirm` / `uiCancel` |
| explore | `nodeHover`, `stepTo()` for every step, `journalOpen` / `journalClose` (J, also in battle), `rest` / `train`, `consumableUse`, `layerCleared`, `interlude`, `descend`, `miasmaDeath` (through `stepTo`) |
| battle | the 29 battle presets, unchanged (`ds/project/sound.md`) |
| reward | `rewardReveal`, `rewardPick`, `cardAdded` |
| ending card | `lifeEndDeath` (fell in battle), `survived` (「この生を終える」, or the root destroyed). Miasma death already sounded on the map, so its ending card is silent |

## Events

`vol` is 0..1 before the effects volume, `pitch` is in Hz, `dur` in ms. Timbres marked *port* are the battle synth's own, copied so that meta and battle paper/wood/seal sound the same.

| Event | Timbre (vol / pitch / dur) | Why |
| --- | --- | --- |
| 新しいノードへ進む `nodeStep` | `step`: heel thud + scuff of the place's ground (0.22 / 110 / 260) | A step costs 刻限, so it is a real footstep: the loudest sound on the map apart from the results. It is still quieter than a battle hit. The ground changes with the layer (see Surfaces), so you can hear where you are walking. Left and right alternate. |
| 踏んだノードへ戻る `nodeReturn` | `step`, half as loud and shorter (0.11 / 120 / 180) | A walked node costs no 刻限 and adds no miasma (§6.3 「戻れる」). The softer step makes that difference audible. |
| ノードにホバー `nodeHover` | `paper` *port* (0.035 / 2600 / 22), gap 60 ms | The map is read like paper. This is quieter than the battle's card hover because the cursor crosses many nodes. Throttled the same way as the battle hover. |
| 手記を開く `journalOpen` | `page`: noise sweeping up, with a flutter of the page edge (0.12 / 1500 / 280) | A book opening, not a UI whoosh. It uses the same drawer in battle (§6.7). |
| 手記を閉じる `journalClose` | `close`: pages slide down, then the covers meet softly (0.10 / 1000 / 200) | It differs from opening in both direction and colour, the same way the battle's 取り消し differs from 離す. |
| 休む `rest` | `exhale`: a long breath out, falling (0.15 / 760 / 1200) | Rest restores HP and stamina. The battle's 構え is a breath in; resting is the breath out. There is no chime, because the HP pips have their own small light. |
| 訓練する `train` | `drill`: a practice swing (action), then the knock on a wooden post (result) (0.16 / 900 / 170) | Training is a choice that costs the rest. It is physical and short, and fits in two parts. |
| 情報を得る `infoGained` | `quill`: three strokes of a dry brush (0.13 / 2300 / 460) | Information raises an enemy's 開示度 in the journal. The sound is a line being written, not a reward. |
| イベントが決まる `eventResolve` | `settle`: one low thunk, and the dust settling (0.12 / 210 / 520) | The events (泉, 重い宝箱, 特殊な食事) can be good or bad. A neutral "it is done" sound does not judge them. |
| 痕跡を拾う（遺産） `traceTake` | `ember`: a warm low chime with an octave, and a breath of flame (0.085 / 523 / 1700) | world-v1 §6: 灯芯 is "the single most important highlight (習得・遺産・決着)". Taking a predecessor's legacy is that moment. Warm, because amber belongs to the player's side. |
| 消耗品を使う `consumableUse` | `pouch`: a cloth pouch opens (action), a soft thump as it is used (result) (0.12 / 1200 / 260) | It covers all five consumables (香, 薬草, 干し肉, 忘れ水, 標の石) without pretending to be any one of them. |
| 瘴気が 20% の線を越える `miasmaLine` | `swell`: two beating low sines, an octave and a twelfth, dark rumble (0.11 / 55 / 2400) | §6.4: crossing a line lowers max stamina. It is a slow warning swell, not an alarm. Each line is a whole tone lower and a little louder (40% → 0.89×, 60% → 0.79×, 80% → 0.71×), so the danger deepens. |
| 瘴気が 100% に達する `miasmaDeath` | `petrify`: muffled crystal ticks closing up (action), under a low swell that stops short (result) (0.14 / 49 / 3400) | world-v1 §1: miasma builds up in the body until it hardens and cannot move. The sound hardens and then stops. Silence and the room tail are the end. |
| 層の主を倒した `layerCleared` | `toll`: a damp low bell, struck once (0.12 / 98 / 3600) | No fanfare. One solemn low strike marks the end of a layer. It is low and quiet enough to sit under the battle's last hit. |
| 階層間の休憩に入る `interlude` | `hum`: a low warm root, fifth and octave rising and fading (0.05 / 110 / 2600) | §6.5: the interlude is the only safe pocket, with no miasma. A warm chord, very quiet, with no melody. |
| 次の層へ降りる `descend` | `fall`: a deep string whose pitch and filter sink together, with a low rumble (0.12 / 98 / 2400) | "A deep string falling": going down into the den. The same family as the battle's 低い弦 (敵の番・予兆), so it reads as the den's voice. |
| 報酬が並ぶ `rewardReveal` | `slide` *port* (0.11 / 1000 / 260) | The cards are laid out as a bundle sliding, as in the battle's 山札の作り直し. It is one sound, not one per card. |
| 報酬を選ぶ `rewardPick` | `paper` *port* (0.15 / 1250 / 90) | The same gesture as 札を掴む in battle. |
| デッキに札が入る `cardAdded` | `tuck`: the card slides in (action), the deck is squared with a tap (result) (0.12 / 1100 / 220) | The result of the pick, and the end of the reward. |
| 倒れて生が終わる `lifeEndDeath` | `expire`: one last heartbeat, slower than the battle's (action), then a low tone sinking into silence (result) (0.15 / 55 / 4400) | The battle's HP<15 heartbeat comes back one last time and stops. Growth is lost (concept-v3), so the tone falls and does not resolve. |
| 生きたまま生を閉じる `survived` | `relief`: a long breath out (action), then the wick's warm glow (result) (0.12 / 440 / 2200) | 「生きたまま、この生を閉じた」: relief, and the amber of the player's side that carries on to the next heir. |
| 継承の間に着く `hallArrive` | `kindle`: the strike of a match (action), then the flame catches with a warm low glow (result) (0.12 / 2400 / 1500) | world-v1 §6: amber is 継承の間 and the lamp is where the player is. Arriving at the hall lights it. |
| 襲名 `heirNamed` | `nameSeal`: a heavy seal stamped (action), and a low string answers (result) (0.18 / 98 / 1500) | 襲名 is taking a name, so it is stamped like a 判. The string is the line of heirs going on. It uses the battle's seal and string family. |
| タイトルから入る `titleEnter` | `bow`: a bowed low D, slow attack, with a breath of bow hair (0.075 / 73.4 / 2800) | It sounds like the mouth of the den drawing you in. It is a single bowed note, not a sting or a fanfare, and it sits on the title ambience's D. |
| ボタンを押す `uiClick` | `wood` *port* (0.07 / 760 / 30), gap 40 ms | The same small wood as the battle's 枠に入る, but softer. |
| 決める `uiConfirm` | `seal` *port* (0.10 / 170 / 110) | A decision is "pressing the seal". It is lighter than 襲名. |
| やめる・戻る `uiCancel` | `slide` *port*, falling (0.08 / 620 / 110) | The same family as the battle's 取り消して手札に戻る: something slides back. |

## A step and its result

| Situation | Action (0 ms) | Result (+280 ms) | Left out |
| --- | --- | --- | --- |
| new node, nothing else | `nodeStep` | — | — |
| walked node | `nodeReturn` | — | it costs nothing, so it has no result |
| new node with a result (情報 / 事 / 痕) | `nodeStep` | `infoGained` / `eventResolve` / `traceTake` | — |
| the step crosses a 20% line | `nodeStep` | `miasmaLine` (pitch by line) | the node's own result: the warning matters more |
| the step reaches 100% | `nodeStep` | `miasmaDeath` | everything else |
| rest node | `nodeStep` | — | 休む / 訓練する is a second action with its own sound |

The priority is miasma death, then the miasma line, then the node's result. `stepPlan()` is the pure version that tests check.

## Ambience

Every ambience is a set of filtered noise beds (white, pink, brown, 4 s seamless loops) and quiet drones. Their filters and gains are slowly modulated by LFOs at rates that do not divide each other. On top of that are sparse one-shot voices at seeded, irregular intervals. Every layer is distinct and follows world-v1 §4 (light source, brightness, colour) and §3 (light peaks at layer 4, then darkens). The values are levels at master 1, ambience 1. At the default settings everything is about 6 dB quieter.

| Place | What you hear | Why | Level (LUFS) |
| --- | --- | --- | --- |
| `title` 住処の口 | wind drawn into the den's mouth, a low D, now and then a far soft string | world-v1 §1: the den's mouth opens onto the surface, and you go down from there | −36 |
| `hall` 継承の間 | still room air, a lamp flame breathing, a warm D–A–D fifth, an old beam creaking | The amber room of the heirs, indoors and safe. It is the only place with a warm fifth | −37 |
| `interlude` 階層間 | slowly breathing air (8 s), a low A and E, sparse soft tones | The safe pocket between layers: rest, and no miasma | −36.5 |
| `layer1` 燦光の樹海 | open bright air, leaves in slow gusts, twigs and leaf litter, a few soft motes of light; almost no drone | The only layer lit by the sky (陽光, 明るい, 若草). It has the most high air, but no birds: this is a den, not a forest | −36.5 |
| `layer2` ■■の秘跡 | wind resonating in the pillar rows (two narrow bands), drifting sand, a faint beating hum of stone, a rare violet crystal barely ringing, a far stone settling | The old sanctuary of stone pillars and sand (斜光, 明るい). The crystals first appear here, so the first glassy sound appears here too | −36 |
| `layer3` 大黒蛇の住処 | low damp air that swells with the serpent's breath (11 s), a very low drone, water drops, the coils shifting against the wall | The only dark upper layer (薄暗い, 藍紫), the great serpent coiled in the wall. It is the dampest and has the longest reverb send | −34 |
| `layer4` 輝晶の間 | a haze of slowly beating glass tones in the middle register, a thin hiss of light, crystals that swell and fade | The brightest layer (結晶の全方向, 白と淡菫). It is bright to the eye but still quiet to the ear: the shimmer stays at 0.5–1 kHz so it never becomes a beep | −37.5 |
| `layer5` 血染めの死海 | dry gusts, wind whistling through the giant ribs, salt crust crackling, something huge groaning far away | The dried red sea bed with its rib gates and salt (散乱光, 中). No water: it is dry | −36 |
| `layer6` 獄雷峡 | wind down the gorge and its edge on the cliffs, a dark low drone, black lightning crackling along a cliff and thunder rolling far off | The gorge where the black thunder runs (暗い, 黒紫). The thunder is distant and dark, never a crack overhead | −36 |
| `layer7` 果てなき世界 | pressure of the void, two low tones beating, a faint high-low hum, the spring welling up and down, the miasma exhaling in slow falling tones | No floor, ceiling or walls, only miasma (最も暗い, 紺と菫). It is the deepest and lowest, with the most reverb | −34.5 |

### Surfaces

The `nodeStep` and `nodeReturn` scuff follows the current place.

| Surface | Places | Sound |
| --- | --- | --- |
| `stone` 石 | title, hall, interlude | a mid band of scuff |
| `litter` 落ち葉 | 1 | higher scuff with three small crackles |
| `sand` 石畳の砂 | 2 | a high hiss of grit |
| `damp` 湿った岩 | 3 | dull scuff with a small wet splat |
| `glass` 結晶の床 | 4 | a firm heel with a faint glass ring |
| `salt` 塩の殻 | 5 | crunching crust (crackles) |
| `gravel` 砂利 | 6 | gravel |
| `void` 床が無い | 7 | almost no scuff, only a muffled low pulse |

## Levels

These were measured offline (node-web-audio-api, max momentary loudness with BS.1770 K-weighting, master 1). The battle synth is the reference. Its hits are −26 to −29 LUFS and its low strings (`foeTurn`, `stance`) are about −20.5.

- The big moments are about as loud as the battle's low strings: `descend` −19.4, `miasmaDeath` −20.4, `lifeEndDeath` −19.7, `layerCleared` −21, `heirNamed` −22.6, `miasmaLine` −22.7, `titleEnter` −23.5.
- The results sit at about hit level: `traceTake` −24.7, `survived` −25.3, `interlude` −26, `eventResolve` −31.
- Steps and the map are under the hits: `nodeStep` −35, `nodeReturn` −42, `nodeHover` −61. UI is −38 to −48.
- The ambience is −34 to −38, which is about 13 dB under a battle hit at the default settings.
- The high-frequency share (power above 4 kHz) of every ambience is under 3%, except layer 1's leaves (14%).
- On a laptop speaker (simulated by a 200 Hz high-pass), the low events lose 15–20 dB. The octave and twelfth partials keep `miasmaLine`, `miasmaDeath` and `layerCleared` near −40, which is about the level of a battle hit on the same speaker.

The same measurement showed that the design system's own `guardBreak` peaks at +1.8 dBFS at master 1 (−0.1 at the default 0.8). That is not changed here; it belongs to the design system.

## Tests and the dev page

- `test/audio/Audio.test.mjs` (`node --test test/audio/Audio.test.mjs`) checks the following. The module imports under Node without `window`. It wraps the real `bundle.js` `OCB.Sound` and keeps its 29 presets and API. Every timbre, surface, ambience and name used by `stepPlan`, by the dev page and by this document exists. Every event and every ambience runs against a strict WebAudio mock with no throws, no warnings (such as an exponential ramp from 0, or a filter out of range) and no leaked sources. Two runs produce identical WebAudio calls. There is no `Math.random`. Settings, mute (suspend and resume), throttling and crossfades work.
- `dev/audio.html` has a button for every event, battle preset and ambience, plus a hover/step strip, the step-with-result cases, settings persisted to `localStorage`, and a 40-second run-through. ES modules do not load from `file://`, so serve the folder that holds `game/` and `ds/` (for example `python3 -m http.server`) and open `/game/dev/audio.html`.

## Limits

- The battle synth makes its own `AudioContext` inside `OCB.Sound`, and the meta/ambience graph makes a second one. The two share no clock (each schedules from its own "now"), which is inaudible here. They are mixed by the browser.
- Mute suspends only the meta/ambience context. The battle context stays running but silent (`OCB.Sound` has no suspend).
- Low fundamentals (41–55 Hz) are felt on headphones and lost on small speakers by design. The upper partials carry the meaning there.
