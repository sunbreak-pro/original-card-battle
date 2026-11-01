# Area report: app — router, save/load, settings, life setup, names, intel, boot

Analyst scope: `src/app/*.js` (main, life, save, recorder, names, intel), `src/scenes/common/stage.js` (+ `common.css`),
`src/scenes/meta/index.js` + `settings.js`, `docs/ARCH.md`, compared with the Unity flow code
(`DemoFlow.cs`, `DemoScreens.cs`, `BattleBootstrap.cs`, `ExplorationBootstrap.cs`, `Depiction/Bridge/DemoSession.cs`,
`BattleLaunch.cs`, `Exploration/Bridge/Expedition.cs`, `EncounterBattle.cs`).

Path shorthands used below:

- `JS/` = `C:\Users\user\AppData\Local\Temp\claude\C--Users-user-orca-original-card-battle\1ab86662-d6cc-4831-aa8c-76f9592e0ca6\scratchpad\proto\prototypes\browser-game\`
- `KIT/` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port\unity-project-kit\Assets\View\`
- `PORT/` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port\`
- `UPROJ/` = `C:\Users\user\Unity\RPG-by-card\`

Who wins: **JS wins for flow, view, timing, keys and save behaviour; C# wins for rules.** Every place the app relies on a
JS-only rule extension is listed in §5.11 (rule gaps); shared infrastructure the app needs is §5.12 (kit needs).

---

## 0. What the area is, in one screen

`main.js` (524 lines) is the whole shell: one Stage, one audio, the settings, the lineage save, and a router between six
screens (title / hall / departure / explore / battle / ending) and three overlays (settings, owned cards, journal).
Scenes never navigate; they call back into the router. Rules live in the ported cores, the inheritance module
(`src/core/inheritance/Inheritance.js`, JS-only) and `life.js` (the seam between one Expedition and the lineage).

The save is **save-by-replay**: a life is its start (plain data, `life.js makeStart`) plus the ordered list of
state-changing calls the screens made on `Expedition` (E), `ExplorationSession` (S), `EncounterBattle` (B) and each
`CoreBattleSource` (C) (`recorder.js`). Loading rebuilds the Expedition and replays the calls; a fight in progress
comes back exactly where it stood (hand, piles, RNG, script position). Writes are stamped `{rev, tab}` so a second
instance never overwrites a newer save.

Unity today has nothing equivalent: `DemoFlow` is a deck → departure → mode → battle → end-screen demo with the deck in
`PlayerPrefs`, `ExplorationBootstrap` loads `Battle.unity` additively per fight and keeps everything in memory (no save).
The trial needs a new router (`Trial/`), a pure save/replay assembly, and opt-in ports of the JS rule extensions.

---

## 1. Screen flow and every transition

### 1.1 Boot (JS `src/app/main.js:19-35`, `:512-524`)

| Step | JS | Unity (proposed) |
| --- | --- | --- |
| 1 | `createStage(app)` (`stage.js:47-77`): 1920×1080 box, `scale = min(w/1920, h/1080)`, centred, letterbox `--ground #10141d` (`common.css:2-5`). | `Stage.Create()` in `TrialBoot.Awake`: Screen-Space-Overlay canvas, CanvasScaler `ScaleWithScreenSize`, ref 1920×1080, **`screenMatchMode = Expand`** (gives exactly `min(w/1920,h/1080)`), full-canvas background Image `#10141d`, centred 1920×1080 `StageRoot` with `RectMask2D` (CSS `overflow:hidden`). Camera clear colour `#10141d`. |
| 2 | `settings = {...DEFAULT_SETTINGS, ...loadSettings()}` (`save.js:10-17,132-141`). | `TrialSettings.Load(store)`. |
| 3 | `audio = createAudio(OCB, settings, {onChange: s => {assign; writeSettings}})`; `audio.bindUnlock(document)` (browser autoplay gate). | `TrialAudio.Create(settings, onChange)`; no unlock needed (start ambience at once). |
| 4 | `opened = openSave()` → `save`, `unreadable`, `stamp`; `TAB = random base36 8 + Date.now() base36 last 4`; `storageOk = storageWorks()` (probe key). | `SaveKeeper.Open(store)`; `Tab = Guid.NewGuid().ToString("N")[..12]`; `StorageWorks()` = write+delete `probe.tmp`. |
| 5 | wait `document.fonts.ready` or 1500 ms (`main.js:523-524`), then `boot()` → `start()` → `toTitle()`. `claude.hot.snapshot(() => persist())` on hot update. | Create the TMP dynamic SDF font assets synchronously in `Awake` (fonts analyst), then `toTitle()` on the first `Start`. The hot-snapshot persist becomes `OnApplicationQuit` / `OnApplicationPause(true)` / `OnDestroy` flush. |

`?autobattle` and `?debug` (`main.js:404-407`) become Inspector fields (§5.2).

### 1.2 Router primitives (`main.js:53-119`)

- `show(mount, ctx, cls)` (`:60-68`): `closeOverlay()` → old `scene.destroy()` → remove every `.ocbg-scene` node →
  `host = stage.layer(cls)` → `scene = mount(host, {stage, audio, settings, ...ctx})`. **No router-level crossfade**: the
  swap is instant; every screen fades itself out to `--ground` and the next fades in from `--ground` (§1.4).
  - JS quirk: `closeOverlay()` calls `scene.refresh()` on the *outgoing* scene before destroying it. C# should close the
    overlay without refreshing the scene being replaced (no visible difference).
- `openOverlay(kind, extra)` (`:73-100`): one overlay at a time (`closeOverlay()` first); host = `stage.layer('ocbg-overlay-host')`
  with `z-index 500`; base ctx `{stage, audio, settings, onClose: closeOverlay}`; hides the app's own title notices
  (`sceneNotices(false)`, `:56-58`).
  - `settings`: `mountSettings(host, {...base, canSaveQuit: !!live, onSaveQuit: saveQuit, onChange: s => {assign; writeSettings; audio.applySettings(settings)}, ...extra})`.
    `extra` overrides (Object.assign order), so `LINEAGE_SAVE = {canSaveQuit:true, saveQuitKind:'lineage'}` (`:71`) turns
    the button on in hall/departure. With `!storageOk` it removes stage-level notices and adds
    `noticeLine(NO_STORAGE_LINES.join(''), 540, max(16, panel.offsetTop − 76), 840)` inside the overlay host (`:86-93`).
  - `cards`: `mountOwnedCards(host, {...base, expedition: live.raw})`.
  - `journal`: `mountJournal(host, {...base, ...journalCtx('explore')})`.
- `closeOverlay()` (`:101-108`): `overlay.destroy()`, remove host, `sceneNotices(true)`, `scene.refresh()`.
- `journalCtx(mode)` (`:110-119`): `{expedition: live.raw, lineage, mode, memo: live.memo, scribbles: live.scribbles, onMemo: t => {live.memo = t; persist()}}`.

Layering on the stage root (CSS z-index): scene layers (`.ocbg-scene`, auto) < overlay host (500) < stage notices
(`.ocbg-notice` 880) < confirm card (`.ocbg-confirm` 900). Notices and confirm cards are appended to `stage.root`
directly, **not** as `.ocbg-scene`, so they survive `show()` (e.g. `resumeLife`'s failure card is asked over the new
title). The title's own notice sits inside the title root at z 40, under the title's fade (z 50), so it fades with it.

Unity: fixed child containers of `StageRoot`, in sibling order: `Scenes`, `Overlays`, `Notices`, `Confirms`
(sibling order = draw order). Use nested `Canvas{overrideSorting}` only if a scene needs its own sorting.

### 1.3 Flow graph

```
boot ─▶ Title ──はじめから (no save)── leave 1100ms ─────────────▶ Hall
          │ ├─はじめから (save/unreadable) ─ confirm ─ok─ cut ─────▶ Hall
          │ ├─つづきから ─ leave 1100ms ─┬─ save.life ─▶ resumeLife ─▶ beginLife ─┬─ open fight ─▶ resumeFight ─▶ Battle
          │ │                           │                                       └─────────────▶ Explore
          │ │                           └─ lineage only ─────────────────────────▶ Hall
          │ └─設定 / Esc ─▶ [settings overlay, no save button]
Hall ──出立 (Enter) ── lamp 700 + fade 650@150 ──▶ Departure ──出立する── fade 520 ──▶ beginLife ─▶ Explore
  │  └─タイトルへ ── lamp 600 + fade 500@100 ──▶ Title          └─← 継承の間へ / Esc-less back ─ fade 520 ─▶ Hall
  └─設定 / Esc ─▶ [settings overlay, 「保存してタイトルへ」]       └─設定 / Esc ─▶ [settings overlay, 「保存してタイトルへ」]
Explore ──battle node── encounter banner 1200 ──▶ runEncounter ──▶ Battle (one scene per fight) ──▶ … ──▶ Explore (fade-in 420)
  │  ├─J / C / Esc ─▶ [journal / owned cards / settings 「保存して閉じる」]
  │  ├─trace node ─▶ inheritance.take(picks)  (saved at once)
  │  ├─carving read ─▶ onCarving (saved at once)
  │  └─NewLife button on the end card ─▶ endLife ─ cut ─▶ Ending ── Enter ── sheet 420 + fade 600@150 ──▶ Hall
Battle ──Esc (on release) / gear ─▶ [settings 「保存して閉じる」 kind 'battle'] ── 保存して閉じる ─▶ Title
any overlay ── 保存して閉じる / 保存してタイトルへ ─▶ saveQuit ─ cut ─▶ Title
```

### 1.4 Transition table (what is called, what is passed, animation, ambience, save)

"cut" = instant swap; the incoming screen starts fully covered by its own `--ground` fade layer, so a cut reads as a cut
to black followed by that screen's fade-in. Easing names are `stage.js:80-86` (`out` = cubic out `1-(1-t)^3`,
`in` = `t^3`, `inout` = cubic in-out, `linear`, `smooth` = smoothstep). `x@y` = x ms with y ms delay.

| From → To | Trigger (JS) | Router call (main.js) | Passed | Outgoing animation | Incoming animation | Ambience | Saved |
| --- | --- | --- | --- | --- | --- | --- | --- |
| boot → Title | fonts ready | `toTitle` `:213-257` | `hasSave = !!save.lineage`, `unreadable`, `onNew`, `onContinue`, `onSettings` | — | title entrance (below) | `setAmbience('title', 1200)` | — |
| Title → Hall (new, nothing stored) | はじめから | `onNew` → `fresh()` `:225` | new lineage `Inh.newLineage((Date.now() ^ rand*0x7fffffff) >>> 0)`, life null | `title.leave` `title.js:108-120`: menu o0,x−24 360 in; name o0 420 in; tagline o0 700 in; figure o0,y+26,s.92 900 in; fade→1 900 inout@200 (total 1100) | hall entrance | `setAmbience('hall', 1500)` | `persist(true)` (forced) |
| Title → Hall (save or unreadable stored) | はじめから (`title.js:56-60`: with `hasSave` or `unreadable` the title plays no leave and calls `onNew` at once) | `confirmCard` 「新しい系譜を始める」 body 「これまでの生と痕跡の記録は消えます。」 (lineage with lives or a life) or the unreadable body (§1.5), ok はじめから (danger), cancel やめる → `fresh()` `:226-241`; a stored lineage with no lives and no life gets no question | as above | none (cut; the confirm is over the live title) | hall entrance | hall 1500 | `persist(true)` |
| Title → resume | つづきから | `onContinue` `:243`: `syncIdle(); save.life ? resumeLife() : save.lineage ? toHall() : toTitle()` | — | `title.leave` 1100 | explore / battle / hall entrance | per target | `persist()` in `beginLife` |
| Hall → Title | タイトルへ chip | `onTitle: toTitle` | — | `hall.leaveToTitle` `hall.js:389-396`: uiCancel; lamp→.35 600 inout; fade→1 500 in@100 | title entrance | title 1200 | — |
| Hall → Departure | 出立 button / Enter | `onDepart(lifeStart)` → `toDeparture` `:267,274-298` | `lifeStart = Inh.startLife(lineage)` (hall computes it; router recomputes if hall passed null) | `hall.depart` `hall.js:398-406`: uiConfirm; lamp→.35 700 inout; fade→1 650 in@150 | departure entrance: fade 1→0 500 out then removed; cells o0,y18→ 380 out @150+i·28 (`departure.js:748-754`) | (stays hall) | — (the draft is the departure's own key) |
| Departure → Hall | ← 継承の間へ | `onBack: toHall` | — | `departure.leave` fade→1 520 in (`:729-735`) | hall entrance | hall 1500 (no-op, same id) | — |
| Departure → Explore | 出立する / Enter | `onSetOut(choice)` `:286-296` → `beginLife(start, [], '', null)` | `choice = {deck, deckBuilder, deckSave, battleToolIds, consumableIds, loadout, talent, departure, limits}` | `departure.leave` 520 in | explore entrance (layer assembles + `layerBanner` resolves at 1500, banner shown 1700; ×0.35 with 演出短縮) | explore sets `ambience(layer)` itself (default fade 1800) | `save.lineage = lifeStart.Lineage; save.life = {before: Inh.toJSON(oldLineage)}; save.deckSave = choice.deckSave` then `persist()` in `beginLife` |
| Explore → Battle | battle node resolved | scene `battleFlow` (`ExploreScene.js:1556-1585`) → `ctx.onBattle()` → `runEncounter()` `:429-466` | per fight: `{source, layer, layerTitle, miasmaPercent, nodeKind, chain:{index: battle.Index+1, total: battle.Count}, disclosureOf, openJournal, settingsInBattle: true, onSettings, onSettingsChange, onError, onFinished}` | `encounterBanner` (`:1459-1492`): map `filter: brightness(.55)` (`explore.css:70`), banner on, resolves at 1200 (banner leaves +300) | cut; battle shows only its backdrop until fonts ≤1200 ms real, then all other layers fade in `OPEN_FADE_MS 240` nominal, `out` (`BattleScene.js:495-509`, `layout.js:171-173`) | `duckAmbience(0.4)` (`BATTLE_DUCK`, `:423`) | every call `persistSoon`; decision → `persist()` |
| Battle → next fight | `onFinished` | loop in `runEncounter` | next `battle.StartNext(AUTOBATTLE)` | result card, hold `RESULT_HOLD_MS 1200` (win) / wait for a press (loss) (`player.js:2060-2081`) | cut; battle opening as above | ducked | as above |
| Battle → Explore | last `onFinished` | `.then(() => { persist(); toExploreBack(); })` `:390` | — | result hold | explore root `is-returning`: opacity 0→1 **420 ms ease-out** (`explore.css:10`), no layer banner; scene calls `Expedition.Return()` itself (`returnFlow`, `:1588-1598`) | `duckAmbience(1)` | `persist()`, then `Return` is logged + `persist()` |
| Explore → Ending | NewLife button on the life-end card | `onLifeEnd` → `endLife` `:485-500` | `ending: Inh.endingView(lineage)`, `lineage`, `onHall: toHall` | none (cut) | ending entrance (below) | `setAmbience('hall', 2000)` | `recordEnd`, `life = null`, `persist()` **before** the ending mounts |
| Ending → Hall | Enter / Space / button (after all is shown) | `onHall: toHall` | — | `ending.toHall` (`ending.js:139-148`): uiConfirm; sheet o→0,y−16 420 in; fade→1 600 in@150 | hall entrance | hall 1500 | (already saved) |
| any → Title (save & quit) | 保存して閉じる / 保存してタイトルへ | `saveQuit` `:504-509`: `persist(); closeOverlay(); duckAmbience(1); toTitle()` | — | none (cut) | title entrance | title 1200 | `persist()` |
| Title → Battle (resumed) | つづきから with an open fight | `resumeFight(rec.wrapSource(open.source))` `:329-333` | the replayed source | title.leave 1100 | battle opening | `setAmbience(layer, 1200)` then duck .4 | — |

Entrance animations the router relies on (owned by the meta analyst, listed so the router timing is complete):

- **Title** (`title.js:122-149`): fade layer o1→0 **1600 inout**; figure o0,y12→ 1400 out@500; name o0,y8→ 900 out@900;
  rule o0,s.4 (origin left-centre)→ 1100 out@1000; tagline chars o0,y−10→ 700 out@(900+i·85); menu rows o0,x−16→
  500 out@(1800+i·110); key line o0→ 700 out@2400; foot light `0.55 + 0.35·(0.5−0.5cos(2π·now/4200))`.
- **Hall** (`hall.js:343-379`): sound `hallArrive`@80; fade 1→0 **700 out**; backdrop alpha 0→1 1600 out@150; title/lead
  o0,y10→ 600 out@250; `riseIn` lives (delay 380, step 90, dy 18, 420 out); traces (delay 480, step 60, dy 10); detail
  o0,x16→ 500 out@900; heir head (delay 1000, step 80); name chars o0,y14→ 420 out@(1250+i·110); seal at
  `sealAt = 1250 + n·110 + 240`: o0,s1.9,r−14 → o1,s1,r−4 **170 in**, then `heirNamed` + attire rows 520 out@(120+i·120);
  start button o0,y12→ 520 out@(sealAt+500); keys 600@(sealAt+700); corner chips 500 out@600.
- **Ending** (`ending.js:94-120`): fade 1→0 **900 out**; caption 1400 out@200; sheet o0,y26,s.985,r(−0.8 death / +0.6
  alive) → o1,y0,s1,r(−0.4 / +0.3) 900 out@300; sound `lifeEndDeath` (Fallen) or `survived` @300; mark o0,s.8→ 700
  out@700; title chars 520 out@(800+i·140); `t1 = 800+n·140+200`; heir line 600@t1; sentence chars 260 out@(t1+200+i·38);
  `t2 = t1+200+m·38+200`; rule s.2→1 700@t2; lines x−10 480 out@(t2+300+i·260); `t3 = t2+300+k·260+200`; next line
  600@t3; button y8 500@(t3+300). First key (Enter/Space/Esc) or a press anywhere shows everything at once (`showAll`).
- **Overlays**: settings shade 0→1 200 out, panel o0,s.97,y10 → 220 out; close shade 160 in, panel o0,s.98,y6 160 in
  (`settings.js:160-173`). Journal drawer x+DRAWER_W→0 220 out, shade 220 out; close 180 in (`journal.js:309-324`).
  Owned cards shade 200 out, panel o0,y14 → 260 out; close 160/170 in (`owned.js:199-212`).
- **Confirm card**: appears and disappears instantly (no tween), no sound (`stage.js:183-213`).
- **Notice line**: appears instantly; with `ms > 0` it gets `.is-leaving` at `ms − 600` (opacity transition 0.6 s) and is
  removed at `ms` (`stage.js:152-159`, `common.css:18-19`).

Ambience calls are router-owned except the explore scene's per-layer `ambience(layer)` (`ExploreScene.js:193`, default
fade 1800 ms, `Audio.js:739-751`). `duckAmbience(f, fadeMs = 600)` ramps with time constant `fadeMs/3000` s
(`Audio.js:811-814`).

### 1.5 Error, recovery and multi-instance paths

| Case | JS | Behaviour |
| --- | --- | --- |
| Fight in progress no longer replays | `beginLife` `:305-326` | `replayLife` throws → `trimUnfinished(log)`; if nothing was trimmed rethrow, else warn and replay the trimmed log (that fight starts over). |
| Life does not replay at all | `resumeLife` `:337-358` | `live = null; kept = keepBrokenSave(); toTitle();` confirm **「生を再開できません」** body `保存された生を読み込めませんでした。` + (kept ? `記録は消さずに控えとして残しています。` : '') + `あきらめると、この生は無かったことになり、出立の前から出直します。（<msg ≤80>）`, ok **あきらめる** (danger), cancel **タイトルへ**. ok (and `save.life` unchanged) → `save.lineage = Inh.fromJSON(l.before)`, `save.life = null`, `persist()`, `toHall()`. |
| Battle scene threw | `battleFailed` `:471-483` (scene's `ctx.onError`) | guarded by `failing`; `live.log = trimUnfinished(live.log)`; `persist()`; confirm **「戦闘を続けられません」** `この戦闘をはじめからやり直します。（msg）` ok **やり直す** cancel **タイトルへ** → `resumeLife()` or `toTitle()`. |
| Stored save unreadable at boot | `openSave` `save.js:78-93` | `keepBrokenSave()` copies the text to `ocb.save.v1.broken`; the text stays under the main key; title notice `UNREADABLE_LINES(kept)` (`main.js:48-49`) and つづきから disabled with `記録を読み込めない`; はじめから asks 「新しい系譜を始める」 `読み込めなかった記録に、新しい系譜を上書きします。` + (kept ? `控えは残ります。` : `控えを残せなかったため、記録は失われます。`) + reason. |
| Storage blocked | `storageWorks()` | title notice `NO_STORAGE_LINES` (`この環境では保存できません。` / `ページを閉じると、ここまでの記録は失われます。`) and the same line over the settings panel. |
| A write fails after working | `persist` `:136-141` | once: `storageOk = false`, stage notice `SAVE_FAILED_TEXT` at `(560, 104, w 800)` for 8000 ms. |
| Another instance wrote the save | `inSync` / `markStale` / `reloadStored` `:157-186` | before every write the stored stamp must equal ours; otherwise `stale = true`, next tick `reloadStored(true)`: cancel every open confirm card, `openSave()`, `live = null`, `failing = false`, `duckAmbience(1)`, `toTitle()`, notice `STALE_TEXT` (`別のタブでこの記録が先に進みました。最新の記録を読み込み直しました。`) at `(560, 104, 800)` for 10000 ms. On the title the `storage` event (`:197-210`) silently re-reads (`syncIdle`) and redraws only if "has a save" changed. |

Notice positions: title notice `noticeLine(lines.join('\n'), 152, 840 − max(0, lines.length − 2)·25, 560)`, z 40, inserted
before the title's fade (`main.js:246-256`).

### 1.6 Unity flow code vs JS — which wins

| Topic | Unity today | JS (wins) |
| --- | --- | --- |
| Screen host | `DemoFlow` builds its own overlay canvas (sortingOrder 100) over `Battle.unity`; `ExplorationBootstrap` loads `Battle.unity` **additively** per fight, hides its own roots, waits, unloads (`ExplorationBootstrap.cs:123-188`). | One stage; the battle is a scene layer mounted by the router like any other screen. No additive scene loads, no `BattleBootstrap.HandIn` static. |
| Canvas scaling | `ScaleWithScreenSize` + `matchWidthOrHeight 0.5` (`DemoFlow.cs:269-282`, `ExplorationBootstrap.cs:233-243`): not letterboxed. | Uniform `min` scale, letterboxed with `#10141d` → `Expand`. |
| End of a fight | View waits for `player.Finished`, then `WaitForSeconds(resultSeconds = 2)`, then `battle.TryFinish(source)` (`ExplorationBootstrap.cs:47,156-168`); DemoFlow waits `EndScreenDelaySeconds 1.2` (`DemoFlow.cs:25`). | `EncounterBattle.Finish` is called **the moment the core decides** (settleWhenDecided) and saved; the battle scene holds its result card 1200 ms (win) / until a press (loss), then `onFinished`. The rule call (`Finish`) is the C#'s; only its timing changes. |
| Keys on the map | `Update` polls J / C / Esc `wasPressedThisFrame` and toggles `Session.Toggle(...)` / `Session.Escape()` (`ExplorationBootstrap.cs:76-82,245-260`). | Key stack with fresh-press rules; overlays are the app's (session `Toggle` is not used by the browser scene). |
| Saved data | `PlayerPrefs` `Depiction.Demo.Deck` (deck) and `Depiction.BattleSpeed` (speed) (`DemoFlow.cs:22`, `DepictionPlayer.cs:106`). Nothing else survives. | Four keys (§2.1). The trial must **not** touch the two PlayerPrefs keys, so `Battle.unity` keeps its own speed and deck. |
| Mode / end screens | `ModeSelectScreen`, `EndScreen`, `SurrenderButton` (`DemoScreens.cs`), `DemoSession` (chain of three). | Not in the trial: an encounter's fights are `EncounterBattle`; results and life end are explore / ending screens. |

Reused as-is from Unity (pure C#): `Expedition`, `EncounterBattle`, `BattleHandIn`, `BattleReturn`, `BattleLaunch`,
`CoreBattleSource`, `DeckBuilder`, `DepartureBuilder`, `BattleSpeed`. They need the opt-in extensions in §5.11.

---

## 2. Save by replay

### 2.1 Storage keys (JS `save.js:5-8`, `departure.js:62`)

| JS key | Contents | Unity file (proposed, under `persistentDataPath/<saveFolder>/`) |
| --- | --- | --- |
| `ocb.save.v1` | the save (§2.2) | `save.v1.json` |
| `ocb.save.v1.broken` | verbatim copy of a save that did not load / replay | `save.v1.broken.json` |
| `ocb.settings.v1` | settings (§3) | `settings.v1.json` |
| `ocb.departure.draft.v1` | 出立's half-built deck `{id: "<lineageSeed>|<generation>|<lifeSeed>", deck: DeckBuilder.Save(), tools: [...], consumables: [...], talent}`, written on every change, cleared on 出立する (`departure.js:52-90,148-151`) | `departure-draft.v1.json` (the departure scene owns the content; the app owns the store) |
| `ocb.probe` | storage probe | `probe.tmp` |

`persistentDataPath` on Windows = `%USERPROFILE%\AppData\LocalLow\DefaultCompany\RPG-by-card\` (company/product from
`UPROJ/ProjectSettings/ProjectSettings.asset:15-16`). Default `<saveFolder>` = `trial`.

### 2.2 The save root and the stamp (`save.js:45-103`)

```json
{"v":1,"rev":12,"tab":"k3j9x0a2lq1b",
 "lineage": { ...Inheritance.toJSON(lineage)... },
 "life": null | { "start": {...}, "log": [...], "memo": "…", "scribbles": [...], "before": { ...lineage JSON... } },
 "deckSave": null | "thrust:2,kesa_cut:3,…"}
```

- The head `{"v":1,"rev":N,"tab":"…"` is always first so the stamp is read **without parsing**:
  `STAMP_RE = /^\{"v":1,"rev":(\d+),"tab":"([^"\\]*)"/`. `stampOf(text)`: null when nothing stored, `{rev:0, tab:''}` for
  a text without a stamp. `sameStamp(a,b)`: both null, or equal `rev` and `tab`.
- Every write: `next = {rev: max(stamp.rev, force ? stored.rev : 0) + 1, tab: TAB}` (`main.js:132-133`); on success the
  page keeps `next` as its stamp.
- `writeSave` refuses a save without lineage. `tab` has `"` and `\` stripped.
- JSON uses a replacer/reviver for BigInt (`{$big}`), Map (`{$map}`), Set (`{$set}`) (`save.js:30-43`). In practice
  seeds are already decimal **strings** (lineage `Seed`, life `Seed`, start `seed`), and every logged argument is plain
  data, so C# needs no tagging. (The C# reader may accept `{"$big":"…"}` as a string for safety.)
- `lineage` is the JS-only inheritance module's plain JSON: `{Version:1, Seed:"<u64>", Lives:[LifeRecord], Current:
  LifeRecord|null, Traces:[Trace], Survivor:null|{Generation, Name, BonusLivesLeft, BonusLives, Journal}, Player:
  {CarvingTaken, RootDefeated}}` (`Inheritance.js:550-560,1118-1221`; `fromJSON` validates and throws
  `InheritanceRuleException('bad save: …')`).
- `life.before` is the lineage as it stood before 出立 (for 「あきらめる」).
- `deckSave` is the last set-out deck string, handed to 出立 as `savedDeck`.

### 2.3 The life's start (`life.js:30-71`)

```json
{"v":1,"generation":3,"name":"…","seed":"12345678901234567890","toolSlots":3,"deckMax":40,
 "survivorBonus":false,"traceLayers":[2,5],"carvingKnown":false,
 "journal":{"Disclosure":{"polearm_warped":1},"Pages":[{"Id":"page-1","Title":"…","Text?":"…"}],"Memo":"","Scribbles":[{"Generation":2,"Name":"…","Text":"…"}]},
 "choice":{"deck":[["thrust-1","thrust"],…],"battleToolIds":["ikitsugi_no_fue"],
           "loadout":{"tools":[["bosho_no_men","Density",1],…],"consumables":[["yakuso","Heal",20],…],"slots":3,"trial":true},
           "talent":null,"toolIds":[],"consumableIds":["yakuso"]}}
```

- `deck` keeps instance ids and order exactly (they drive the battle shuffles).
- `journal` is `Inh.heirJournal(lifeStart.Journal, lifeStart.JournalFrom)` (a survivor's memo becomes that survivor's
  走り書き; the heir starts with an empty memo).
- `loadout` is `runLoadoutToData(toRunLoadout(choice.loadout, toolSlots))` (`Loadouts.js:19-45`): rows `[id, ReliefKind, amount]`.
- `choice.toolIds` is always `[]` today (the departure passes `consumableIds` only).

`lifeSetup(start)` (`life.js:178-215`) rebuilds the Expedition:

1. `life = {traceTaken: false}`; `mapFor(profile, seed)`: `spec = profile.MapSpec`; carving known → `spec.With(Carving, 0)`;
   `!traceTaken && traceLayers.has(layer)` → `spec.With(Trace, 1)`; `MapGenerator.Generate(spec, seed)`.
2. `RunStart({Seed: BigInt(start.seed), Loadout: runLoadoutFromData(choice.loadout), MapFor})`.
3. `intel = new JournalIntel()`; `limits = DeckLimits.From({DeckMax, ToolSlots})`; options `{trial: true, limits: limits.IsDefault ? null : limits}`.
4. `new Expedition(runStart, deckFromData(choice.deck), battleToolIds, talent, intel, journal.Disclosure, options)`; `intel.bind(expedition)`.
5. Inherited pages: `expedition.InheritedPages = pages`; if > 0, `_pages += pages` and `BeginLife` is patched to add them
   again when a life restarts.
6. `Inherit` is patched to set `life.traceTaken = true` first (so the later layers of the same life draw no trace node).

### 2.4 The log (`recorder.js`)

Entry: `[tag, method, args]` or `[tag, method, [], n]` (a run of `n ≥ 2` identical **no-argument** calls on the same tag
collapses into one entry: `push` `:47-54`; replay expands it). Tags: `E` Expedition, `S` its ExplorationSession, `B` the
EncounterBattle, `C` the current CoreBattleSource. A wrapped source passed back as an argument
(`EncounterBattle.Finish(source)`) is logged as the string `"$source"` (`:56-59`).

Recording rules (`wrap` `:23-44`):

- Calls run on the real object (`this` = raw), so the objects' own internal calls are never logged (e.g. `Return` →
  `Session.ReportBattle`, the session's `Keeper.TakeChest/TakeSurvey`, `Expedition.Learn` → `Take`).
- A call is logged **after it returns**; a throwing call changed nothing and is not logged.
- Getters are never logged (`Deck`, `Offer`, `PendingResult`, `AutoCardId`, `Frame`, …).
- `READ_ONLY` (`:12-16`) is never logged: `Screen, CanOpen, DisclosureOf, HandIn, PreviewFor, Inspect, RefusalText,
  HasPlayableCard, toJSON, equals, constructor, NextLaunch, TryDescend, MapOf, SyncLife, CanUseSlot, EventAt,
  CanTeleport, TargetFor, CanTakeFreeStep, ShownGapGlyph`. Everything else is logged (note `EnemyName` is not in the
  list; scenes must not call it through the recorder).
- Children: `E.StartEncounter()` and the `E.Battle` / `E.Session` properties return wrapped objects; `B.StartNext()`
  returns a wrapped source; `onCall(tag, method)` runs after every logged call (the app's `persistSoon`).

Calls the shipped game actually logs (the method set the C# recorder and replayer must support):

| Tag | Method | Args (JSON) | Who calls it |
| --- | --- | --- | --- |
| E | `StartEncounter` | `[]` | router `runEncounter` |
| E | `Return` | `[]` | explore `returnFlow` |
| E | `Abandon` | `[]` | explore `battleFlow` fallback |
| E | `Learn` | `[cardId]` | explore result card |
| E | `Train` | `[cardId]` | explore training card |
| E | `Take` | `[cardId]` | (only if a scene calls it) |
| E | `CloseResult` | `[]` | explore result card (trial) |
| E | `ShowTaken` | `[card object]` | explore trace receipt (trial) |
| E | `CloseTaken` | `[]` | explore trace receipt |
| E | `Inherit` | `[taken object]` (`Inheritance.takeTrace(...).Taken`, plain JSON: `{TraceId, Cards:[{CardId, Rank, …}], Journal:{Disclosure, Pages, Scribbles}}`) | router `inheritance.take` |
| S | `Step` | `[nodeId, RestChoice]` | explore |
| S | `Jump` | `[nodeId, RestChoice]` | explore (標の石, trial) |
| S | `UseConsumable` | `[slot]` | explore |
| S | `Act` | `[ScreenAction|TrialAction]` or `[action, arg]` (`arg` = slot int for `TakeFound`, enemy id for `ChooseSurvey`) | explore |
| S | `OpenTraining`, `PutOffTraining`, `Escape`, `Toggle` | `[]` / `[Overlay]` | explore |
| B | `StartNext` | `[suggestCards: bool]` (`true` only with autobattle) | router |
| B | `Finish` | `["$source"]` | settleWhenDecided |
| C | `AdvanceAuto` | `[]` (run-length) | battle player, autobattle |
| C | `Next` | `[]` (run-length; autobattle only) | autobattle |
| C | `EndTurn` | `[]` | battle player |
| C | `TryPlay` | `[cardId, DropZone]` or `[cardId, DropZone, target]` | battle player |
| C | `TakeFreeStep` | `[±1]` | battle player |

Enums are logged by their **name strings** (JS enums are `defineEnum` name strings, `shared/cs.js:44-49`), e.g. `"Rest"`,
`"ConfirmStep"`, `"AcceptEvent"`.

### 2.5 Replay (`replayLife` `:88-106`)

```
kept = copy of log; battle = source = null; fighting = false
for each [tag, m, args, n] in kept, repeat n (default 1) times:
    target = E → expedition | S → expedition.Session | B → battle | C → source
    if no target or no method m → throw "replay: cannot call <tag>.<m>"
    r = target[m](...args with "$source" → source)
    E.StartEncounter → battle = r, fighting = false
    E.Abandon / E.Return → fighting = false
    B.StartNext → source = r, fighting = true
    B.Finish → fighting = false
open = fighting && battle && source && expedition.Battle === battle ? {battle, source} : null
return {kept, open}
```

- **Resume mid-battle** (tier1 R1-16, #462): the log holds every `C` call up to the last saved one, so the replayed
  source stands exactly there — hand, piles, HP, stamina, Guard, statuses, omens, turn, phase, **RNG state** and the
  **script position** (each played script event is one `AdvanceAuto`). The router wraps it into the *same* log
  (`recordExpedition(expedition, res.kept, persistSoon)`, then `rec.wrapSource(open.source)`) and hands it to
  `runEncounter(resumed)`. The battle scene starts from `source.Frame` (the frame after the last event the screen had
  dequeued) and goes on with `AdvanceAuto` / waits for the player. The event that was animating at the moment of the
  cut is treated as played (its `After` frame shows), not replayed.
- **Fight decided but not finished in the save** (crash between the deciding call and `B.Finish`): `settleWhenDecided`
  sees `State.Result != Ongoing` on construction and finishes at once (`:150-152`).
- **Multi-fight encounter reloaded after its first fight**: `expedition.Battle` is still the encounter; the router's
  `runEncounter` resumes with `ex.Battle` and `StartNext` for the next fight (EXTENSIONS X-2).
- `trimUnfinished(log)` (`:161-172`): drops everything from the last `B.StartNext` that has no `B.Finish` after it. Used
  only when a fight cannot go on (replay threw on it, or the battle scene failed); a plain reload keeps the fight.
- `settleWhenDecided(battle, source, onSettled)` (`:126-154`): returns `{source: view, finish(), settled}`; after **every**
  call on `view`, if not done and the core decided (`source.State.Result !== Ongoing`), call `battle.Finish(source)`
  (logged `B.Finish ["$source"]` right after the deciding `C.*`) and `onSettled()` (= `persist()`); `finish()` is a no-op
  afterwards (`fight.finish()` after `onFinished` / autobattle).

### 2.6 When the save is written

| Moment | JS | Write |
| --- | --- | --- |
| はじめから | `fresh()` `main.js:225` | `persist(true)` (forced over a stale/unreadable save) |
| hall with no lineage (defensive) | `toHall` `:263` | `persist()` |
| 出立する | `onSetOut` + `beginLife` `:286-296,323` | `persist()` (lineage with `Current`, life `{start, log:[], memo:'', scribbles, before}`, `deckSave`) |
| every logged call | `recordExpedition(..., persistSoon)` `:148-153,318` | coalesced: one write per event-loop turn (`queueMicrotask`) |
| a fight decided | `settleWhenDecided(..., persist)` `:438` | immediate |
| after an encounter's last fight | `runEncounter().then(persist)` `:332,390` | immediate |
| explore actions | scene `ctx.onPersist()` (`ExploreScene.js:195`, e.g. after `Return`, `CloseResult`, `Act`) | immediate (redundant with persistSoon; keeps the step-before-walk rule X-8) |
| trace taken | `inheritance.take` `:370-388` | lineage (`takeTrace`) **and** the `E.Inherit` entry in **one** write |
| carving read | `onCarving` `:395` | `noteCarving` + write |
| memo typed | journal debounce **400 ms** + flush on close (`journal.js:233-263`) → `onMemo` → `persist()` | immediate |
| life ends | `endLife` `:485-500` | `recordEnd`, `life = null`, write before the ending mounts |
| あきらめる | `resumeLife` `:350-356` | lineage := `before`, life null, write |
| battle scene failed | `battleFailed` `:476-477` | trimmed log, write |
| 保存して閉じる / 保存してタイトルへ | `saveQuit` `:504-509` | write |
| settings changed | `onChange` everywhere | settings key only |
| 出立 draft changed | `departure.js:148-151` | draft key only |

`persist()` composes `save.life = {start, log: live.log.slice(), memo, scribbles, before: save.life.before}` from `live`
whenever a life is live (`main.js:131`).

### 2.7 Settings persistence (`save.js:132-147`)

`writeSettings` stores exactly the keys of `DEFAULT_SETTINGS` (`speed, shortFx, master, effects, ambience, muted`);
`loadSettings` keeps a stored key only when its `typeof` matches the default's. `reverb` (audio-only, default 0.18,
`Audio.js:115`) is **not** persisted.

### 2.8 C# design (pure, engine-free, testable with `dotnet test`)

**Assembly `Trial.App`** (`KIT/Trial/App/`, asmdef `noEngineReferences: true`, mirrored by `PORT/Trial.App/Trial.App.csproj`
like `PORT/Exploration.Bridge/Exploration.Bridge.csproj`: netstandard2.1, C# 9, `<Compile Include="..\unity-project-kit\Assets\View\Trial\App\*.cs" />`;
tests in `PORT/Trial.App.Tests/` NUnit net10.0). References: BattleCore, DungeonCore, DungeonContent, Exploration.Script,
Exploration.Bridge, Depiction.Script, Depiction.Bridge, Journal.Script, and the new inheritance assembly.

1. **JSON** (`Json.cs`): `JsonValue` (Null / Bool / Number / String / Array / Object), objects keep **insertion order**
   (`List<KeyValuePair<string, JsonValue>>` + lookup), numbers kept as `long` when integral else `double`
   (`ToString("R", InvariantCulture)`), compact writer identical to `JSON.stringify` (escape `"` `\` and U+0000–U+001F
   as `\b \f \n \r \t` or `\u00XX`, non-ASCII written raw in UTF-8 without BOM, lone surrogates `\udXXX`), strict reader
   (throws `JsonException` with position). No `System.Text.Json` (not in Unity), no `JsonUtility` (no dictionaries, no
   ordered objects, no polymorphism). ~420 lines.
2. **Store** (`KeyValueStore.cs`): `interface IKeyValueStore { string Read(string key); bool Write(string key, string text);
   void Remove(string key); }`; `FileKeyValueStore(string folder)` maps the JS keys to the file names in §2.1;
   `MemoryKeyValueStore` for tests and for the `noStorage` switch. **Atomic write**: write `<file>.tmp` with
   `FileStream(..., FileOptions.WriteThrough)` + `Flush(true)`, then `File.Replace(tmp, file, file + ".bak", true)` when
   the target exists, else `File.Move(tmp, file)`; on `IOException` (AV lock) retry once after 50 ms, then fall back to
   `File.Copy(tmp, file, true)` + delete tmp. Every access in try/catch returning null/false, as `save.js:19-27`.
   `ReadHead(key, 128)` reads only the first bytes for the stamp. ~170 lines.
3. **Save** (`Save.cs`, port of `save.js`, same names): `SaveKey`, `DefaultSettings`, `StampOf(text)`, `StoredStamp()`,
   `SameStamp(a, b)`, `OpenSave() → (TrialSave save, Unreadable unreadable, Stamp stamp)`, `WriteSave(save, stamp)`,
   `StorageWorks()`, `KeepBrokenSave()`, `ReadBrokenSave()`, `LoadSettings()`, `WriteSettings(settings)`. The writer
   emits the head `v, rev, tab` first, then `lineage, life, deckSave`, so the regex holds. ~280 lines.
4. **Save data** (`SaveData.cs`): `TrialSave {Lineage, LifeSave Life, string DeckSave}`, `LifeSave {LifeStart Start,
   LifeLog Log, string Memo, List<Scribble> Scribbles, JsonValue Before}`, `LifeStart` (the shape of §2.3) with
   `ToJson()` / `FromJson(JsonValue)` that throw on a bad shape. Seeds are `ulong` parsed from decimal strings
   (`ulong.Parse(s, NumberStyles.None, InvariantCulture)`); lineage and `before` stay `JsonValue` here and go through the
   inheritance module's `FromJson` (which validates, as `Inheritance.fromJSON` does). ~260 lines.
5. **Recorder** (`Recorder.cs`, port of `recorder.js`). C# has no Proxy, so recording is a **command-side facade**;
   reads stay on the raw objects:
   - `LogEntry {string Tag; string Method; JsonValue[] Args; int Count}` and `LifeLog` (`List<LogEntry>` + `Push` with
     the run-length rule + `ToJson` / `FromJson`).
   - `RecordExpedition(Expedition raw, LifeLog log, Action<string,string> onCall) → RecordedExpedition`.
     `RecordedExpedition` exposes `Raw`, `Log`, `Session` (a `RecordedSession`), `Battle` (a `RecordedBattle` wrapping
     `Raw.Battle`, cached per raw instance), `WrapSource(CoreBattleSource)`, and one method per logged E call of §2.4.
     `RecordedSession`, `RecordedBattle` (`StartNext(bool)`, `Finish(RecordedSource)`, read-throughs `Over, Lost, Won,
     Index, Count, HandIn, Fought, HpAfter, StaminaAfter`), `RecordedSource` (implements the trial battle source
     interface the battle analyst defines — at least `IDepictionSource` plus the JS extensions — forwarding reads and
     logging `AdvanceAuto, Next, EndTurn, TryPlay, TakeFreeStep`).
   - Each logged method: `var r = raw.M(args); log.Push(tag, "M", encodedArgs); onCall?.Invoke(tag, "M"); AfterCall?.Invoke(); return r;`
     — nothing logged when `raw.M` throws.
   - `ReadOnly` (the JS list) is a `static readonly HashSet<string>`; a reflection test
     (`EveryPublicMutatorOfTheFourTypes_IsRecordedOrReadOnly`) walks the public methods of `Expedition`,
     `ExplorationSession`, `EncounterBattle`, `CoreBattleSource` and fails on any method that is neither in `ReadOnly`
     nor has a recorder method of the same name. This replaces the Proxy's "everything is recorded by default".
   - `Replay.ReplayLife(Expedition ex, IReadOnlyList<LogEntry> log) → ReplayResult {LifeLog Kept; OpenFight Open}`: a
     `switch ($"{tag}.{method}")` decoding the args (enum names via `Enum.Parse` with an explicit
     `ScreenAction`-then-`TrialAction` lookup for `S.Act`); unknown → `ReplayException("replay: cannot call X.Y")`.
     `C.Next` replays as `AdvanceAuto` (JS `Next` is only logged when an event is pending, where `AdvanceAuto` is the
     same call) unless the battle analyst exposes `Next`.
   - `SettleWhenDecided(RecordedBattle, RecordedSource, Action onSettled) → Fight {RecordedSource Source; bool Finish(); bool Settled}`
     subscribes to `source.AfterCall` (and checks once on construction).
   - `TrimUnfinished(log)`.
   ~520 lines.
6. **SaveKeeper** (`SaveKeeper.cs`, the "one save, several tabs" half of `main.js:121-210`): holds `Save`, `Unreadable`,
   `Stamp`, `Stale`, `StorageOk`, `Tab`; `Persist(LiveLife live, bool force)`, `PersistSoon()` (sets dirty),
   `FlushIfDirty()`, `InSync()`, `MarkStale()` (raises `BecameStale` once), `SyncIdle()`, `Reopen()`. Pure, so the
   stamp tests of `test/app/stamp.test.mjs` port 1:1. ~200 lines.

**Flush policy (Unity side).** `PersistSoon` sets a dirty flag; `TrialBoot.LateUpdate` calls `FlushIfDirty()` — one
write per frame at most, the frame being Unity's "event-loop turn". Hard writes (`Persist()`) stay synchronous at the
moments of §2.6. Also flush in `OnApplicationPause(true)`, `OnApplicationFocus(false)`, `OnApplicationQuit` and
`TrialBoot.OnDestroy` (play-mode exit). Writes are synchronous on the main thread; measure with a long life (the JS
life test prints entries/bytes per life) and move only the file write to a worker (sequence-numbered, last wins) if a
write exceeds ~2 ms.

**Multi-instance.** There is no `storage` event; keep the stamp check before every write (it also protects an Editor
play session and a built player sharing `persistentDataPath`) and `SyncIdle` on title clicks. No `FileSystemWatcher`.

**Cross-check with the browser.** Because the log uses the same tags, method names and enum names, a browser save can be
replayed by the C# replayer. Add a node script to the browser-game repo (`tools/dump-saves.mjs`) that walks a few
seeded lives with the JS recorder (deaths, a survivor, a taken trace, a mid-fight cut, multi-fight encounters) and
writes `{save, snapshot}` fixtures (`toParityJson` of `Session.State`, `Mastery`, `Deck`, `Owned`, journal, `Pages`,
and for the open fight `State` + `Frame`). `PORT/Trial.App.Tests/Fixtures/js-saves/*.json` replayed in C# must land on
the same snapshots: this is the oracle for every JS rule extension the trial ports.

Tests to port 1:1 (`JS/test/app/`): `life.test.mjs` (lives die, traces taken, every life replays exactly; unfinished
fight comes back; survivor bonus), `settle.test.mjs` (death saved at decision; win kept during result card; fight in
progress resumes to the same end for seeds 11/5/8 at cuts 7/23/2; decided-on-load finishes at once; whole lives replay
from every save; `storageWorks` / `keepBrokenSave`; `openSave` three cases), `stamp.test.mjs`, `survivor.test.mjs`,
`inherit-gains.test.mjs` (X-11 / R-27 parts that touch `life.js`).

---

## 3. Settings

### 3.1 Options (`save.js:10-17`, `settings.js`, `Audio.js:115-144`)

| Key | Values / range | Default | UI (settings.js) | Read by |
| --- | --- | --- | --- | --- |
| `speed` | 1 / 1.25 / 1.5 (`BattleSpeedStep` Slow / Normal / Fast; stored as the number) | **1.25** (`BattleSpeed.Default`) | segmented row 「戦闘の速さ」, labels `×1.0 / ×1.25 / ×1.5` with `speed1/2/3` chevron icons 22 px (`:80-81`) | battle clock `BattleSpeed.TweenSpeed(step)` = multiplier / 1.25 (0.8 / 1 / 1.2) (`battle/clock.js:52-61`); HUD switch label and `player.cycleSpeed` (writes `settings.speed`, calls `ctx.onSettingsChange` → `writeSettings`) (`player.js:2114-2124`, `hud.js:169,358`). Not read outside battles. |
| `shortFx` | bool | false | 「演出短縮」 入れる / 切る + note 「演出短縮を入れると、速さに関わらず演出が縮みます。」 | battle clock `× L.SHORT_SCALE`, no fast-forward (`clock.js:53,60,153`); battle backdrop still (`field/backdrop.js:86`); explore `dur(ms) = round(ms × 0.35)` (`ExploreScene.js:187-188`, also on reduced motion). |
| `master` | 0..1, step 0.05 (`round(v·20)/20`) | 0.8 | slider 「全体」 | audio master gain (`Audio.setMaster`) |
| `effects` | 0..1, step 0.05 | **0.9** (app default wins over the audio module's 1.0) | slider 「効果音」 | audio fx gain and the battle synth (`master × effects`) |
| `ambience` | 0..1, step 0.05 | 0.6 | slider 「環境音」 | ambience gain |
| `muted` | bool | false | 「消音」 消す / 鳴らす | audio mute (suspends the graph 400 ms after muting) |
| (`reverb`) | 0..1 | 0.18 | none | audio only, never persisted |

Reduced motion (`prefers-reduced-motion`) is an OS query, not a setting: tweens of meta screens finish in 1 ms
(`util.js:93-99`), CSS animations 1 ms (`common.css:20`), explore uses the short durations. Unity has no such query: make
it a `TrialBoot` Inspector switch (`reducedMotion`, default off).

### 3.2 How a change flows

- Panel change → `emit()` (`settings.js:49-55`) → `ctx.onChange({...ctx.settings, master, effects, ambience, muted,
  speed: Multiplier(step), speedStep, shortFx[, reverb]})` → router: `Object.assign(settings, next); writeSettings(settings);
  audio.applySettings(settings)` (`main.js:84`).
- Slider moves call the audio directly (`audio.setMaster` …) → audio `changed()` → the audio's `onChange` listener the
  router registered at boot → `Object.assign(settings, s); writeSettings(settings)` (`main.js:23`). The panel also
  subscribes to `audio.onChange` to repaint when something else changes the audio.
- The battle's speed switch writes `settings.speed` itself and calls `onSettingsChange` (`main.js:458`). The settings
  object is **one shared instance** (scenes keep a reference and read it live), so a change made in the overlay reaches
  the running battle; `closeOverlay → scene.refresh()` makes the battle repaint its switch (`BattleScene.js:520-524`).

### 3.3 The panel (for completeness; the meta analyst owns the look)

`settings.js:24-198`: shade full stage (click closes), panel at x 540, w 840, top recentred to
`max(24, round((1080 − height)/2))`. Rows in order: speed, shortFx (+note), heading 「音の大きさ」, master, effects,
ambience, mute, footer (note + save button when `onSaveQuit && canSaveQuit !== false`, then 「閉じる」 + `Esc` tag), key
line `↑↓ 項目　←→ 変える　Esc 閉じる`. Focus starts on row 0. Save button label/note by kind (`text.js:105-109`):
`'battle'` (inBattle) → 「保存して閉じる」 + 「戦闘の途中を保存して閉じます。つづきからで、この場面に戻ります。」; `'lineage'` →
「保存してタイトルへ」 + 「ここまでの記録は保存されています。つづきからで、ここへ戻ります。」; `'life'` → 「保存して閉じる」 +
「今の状態を保存してゲームを閉じます。生は終わりません。」. Where it is offered: title — none (`canSaveQuit = !!live` = false);
hall / departure — `'lineage'`; explore — `'life'`; battle — `'battle'`.

### 3.4 C#

`TrialSettings` (pure, in `Save.cs`): fields `Speed` (`BattleSpeedStep`), `ShortFx`, `Master`, `Effects`, `Ambience`,
`Muted`, plus non-persisted `Reverb = 0.18f`; `event Action Changed`. File JSON keeps the JS shape
(`{"speed":1.25,"shortFx":false,"master":0.8,"effects":0.9,"ambience":0.6,"muted":false}`); read `speed` with the JS
`stepOf` rule (`settings.js:10-15`: exact step, else the step whose multiplier is within 1e-6, else Default). Numbers
with `InvariantCulture`. The trial never reads or writes `PlayerPrefs` `Depiction.BattleSpeed`.

---

## 4. Keyboard and pointer, shared across scenes

### 4.1 The key stack (`stage.js:133-145`)

One global `keydown` listener; handlers pushed with `pushKeys(handler)` (returns the pop function). The **top** handler
(last pushed) gets the key first; returning `true` swallows it (`preventDefault`, stops); `false` passes it down. Scenes
push on mount and pop on destroy, so an overlay mounted after a scene is above it. `keyup` is not stacked: the battle
listens on `window` directly (`BattleScene.js:407-414`). `modalSwallow(e)` (`util.js:117-119`): an overlay swallows
printable keys (`key.length === 1`), arrows, Enter, Tab, PageUp, PageDown, and lets the rest (F-keys, modifiers) through.

### 4.2 Key map

| Where | Keys |
| --- | --- |
| Title (`title.js:151-160`) | ↑ / `w` up, ↓ / `s` down (repeats move), Enter / Space choose, Esc (fresh) → settings. Ignored while leaving or while a confirm card is open. |
| Hall (`hall.js:408-424`) | ← / → trace, ↑ / ↓ scroll ±84 px, PgUp / PgDn scroll `clientHeight − 84`, Enter 出立, Esc (fresh) → settings, J (fresh) → journal **only if the router gives `onJournal` — main.js does not**. |
| Departure (`departure.js:779-803`) | Tab marks "keyboard focus"; Esc (fresh) → settings (→ back without `onSettings`); J likewise not wired; Enter on a Tab-focused button goes to that button. Deck step: Shift+↑↓ scroll list, PgUp/PgDn page, arrows move the card cursor (↑↓ by `Columns`), `+ = ;` add, `- _ Backspace Delete` remove, Enter → supplies (refusal sound when invalid). Supplies step: ← / PgUp, → / PgDn talent page, Enter 出立する. |
| Explore (`ExploreScene.js:2285-2324`) | with Ctrl/Meta/Alt → pass; repeats of J / C / Esc swallowed; J → journal (also while busy unless a battle is pending); C → owned cards; Esc → stop aiming 標の石 if aiming and nothing covers the map, else settings; 1–3 consumables; Enter / Space at the interlude → first button; arrows move the node selection; Enter / Space step (Shift+Enter second option). A Tab-focused button takes Enter / Space first (X-8). Plates and confirmations push their own handlers (Esc = their 「やめる」). |
| Battle (`BattleScene.js:331-414`) | scene keys: Esc, Enter, Tab, Space, 1–9, J, I, X (+ ← / → only while 俊敏's choice waits). Esc backs out one step (held card → pinned detail → pile list → journal → free-step focus) and otherwise **opens settings on key release**; 1–9 select, Enter play, Tab / Shift+Tab target, I detail, X end turn, Space held = fast-forward (first press arms one-action FF), J journal. After the end: Enter / Space / Esc skip the result (fresh press). A scene key arriving proves no modal overlay is up (`settingsClosed()`). |
| Ending (`ending.js:150-161`) | first Enter / Space / Esc (or press anywhere) shows everything; then Enter / Space → hall. |
| Settings overlay (`settings.js:175-186`) | Esc (fresh) close; ↑ / ↓ focus (wraps, `nodeHover`); ← / → change (`uiClick`); Enter / Space activate (or step right); rest `modalSwallow`. |
| Journal overlay (`journal.js:333-343`) | Esc / J (fresh) close; ← / → / Tab page; ↑ / ↓ scroll ±60; rest `modalSwallow`. In the memo textarea every key stays in the textarea; Esc blurs and closes. |
| Owned cards (`owned.js:214-225`) | Esc / C (fresh) close; PgUp / PgDn page; arrows move (↑↓ by `Columns`); rest `modalSwallow`. |
| Confirm card (`stage.js:199-209`) | Esc (fresh) → cancel; Enter → OK unless a Tab-focused card button exists (then that button); Space only presses a Tab-focused button; repeats and Ctrl/Meta/Alt+Enter ignored; **other keys return false and fall through** (screens below check "a confirm is open" themselves: title `document.querySelector('.ocbg-confirm')`, explore `confirmOpen`). |

### 4.3 Held-key rules

- Every opener and closer (Esc, J, C, the title / hall / departure Esc) acts only on a **fresh press**
  (`!e.repeat`) and swallows repeats, so a held key never opens-closes-opens an overlay (ARCH "A held key opens or
  closes an overlay once").
- The battle opens settings on Esc **release** (`escToSettings`), so the repeats of a held Esc are spent while nothing
  is open.
- Arrows, ±, PgUp/PgDn repeat (cursor moves, slider steps of 0.05).
- Battle: Space held keeps fast-forward on (`ffHeld`); its repeats only keep it; a repeated Enter / Tab is swallowed,
  other repeats pass.

### 4.4 Overlay stacking and Esc

At most one app overlay (settings / owned / journal) at a time (`openOverlay` closes the previous). Over them: notices
(no input) and confirm cards. The battle's journal is **not** an app overlay: the battle mounts it into its own `over`
layer through `ctx.openJournal` (journal mode `'battle'`), and the scene clock pauses while the journal or the app's
settings is up (`BattleScene.js:185-270`). Esc order everywhere: the innermost thing closes first (confirm → plate /
drawer / held card / journal → settings opens), and the settings panel's Esc closes it.

### 4.5 Tab focus rule (`stage.js:161-174`, `departure.js:769-777`, EXTENSIONS X-8)

Only focus that **Tab** put on a button counts: a capture-phase `keydown` remembers whether the last key was Tab,
`focusin` records the element when it was, and any `pointerdown` clears it. A Tab-focused button takes Enter / Space
before the screen's own Enter (a focused 「やめる」 never confirms). Focus ring: `outline: 2px solid --wick #ffd89a,
offset 2px` (`common.css:8`; ink on washi in explore).

### 4.6 Pointer conventions

- Stage coordinates: `stage.toStage(e) = ((clientX − rect.left)/scale, (clientY − rect.top)/scale)` (`stage.js:66-69`),
  origin top-left, y down. Unity: `Stage.ToStage(Vector2 screenPos)` via
  `RectTransformUtility.ScreenPointToLocalPointInRectangle(StageRoot, pos, null, out local)` then `(local.x + 960, 540 − local.y)`.
- Shades of settings and journal close on click; the owned-cards shade and the confirm shade do not.
- Hover (`mouseenter`) gives focus + `nodeHover` sound on the title menu; CSS `:hover` styles with 0.14 s transitions.
- Slider: `pointerdown` sets the value from x and captures; `pointermove` follows; `pointerup` releases.
- Wheel scroll in the hall (lives, trace list) and the departure (deck list).
- Battle: `pointerdown` on the arena arms fast-forward; `window pointerup` releases; a press within 500 ms of the
  click that played a card does not fast-forward (`L.DOUBLE_CLICK_MS`).

### 4.7 Focus / blur

- Battle: `window blur` → clear fast-forward, `escToSettings = false`; `visibilitychange` hidden → clear fast-forward
  (`BattleScene.js:452-458`). No synthetic keyup.
- Explore: `focusin` / `focusout` for the Tab rule.
- Audio: unlock on the first gesture (browser only).
- Hot update: `claude.hot.snapshot` → `persist()`.

### 4.8 Unity implementation (Input System only: `activeInputHandler: 1`, `UPROJ/ProjectSettings/ProjectSettings.asset:690`)

- **`KeyStack`** (`Trial/Scenes/Common/KeyStack.cs`): `Push(Func<KeyPress, bool>) → Action pop`;
  `event Action<KeyPress> KeyUp`; `event Action Blurred`; `Pump()` called once per frame from `TrialBoot.Update` (before
  scene updates). `KeyPress {string Key; bool Repeat; bool Shift, Ctrl, Alt, Meta}` where `Key` is the **JS `e.key`
  string**, so every handler ports with the same comparisons: letters lower-case, upper-case with Shift (`'j'`/`'J'`),
  `'1'..'9'` (numpad digits too), `' '`, `'Enter'` (NumpadEnter too), `'Escape'`, `'Tab'`, `'Backspace'`, `'Delete'`,
  `'ArrowUp'`/`Down`/`Left`/`Right`, `'PageUp'`, `'PageDown'`, `'Home'`, `'End'`, `'+'` (Shift+Equals, NumpadPlus,
  Shift+Semicolon for JIS), `'='`, `';'`, `'-'` (Minus, NumpadMinus), `'_'` (Shift+Minus), `'Shift'`/`'Control'`/`'Alt'`
  for the modifier keys themselves (the battle ignores them), `'F1'..'F12'`. Map from `UnityEngine.InputSystem.Key`
  with a fixed table (positional, US layout) — the handlers already accept every alias they need.
- **Repeat synthesis**: Input System gives no OS auto-repeat for non-text keys. Repeat the most recently pressed
  non-modifier key while held: first repeat after 500 ms, then every 33 ms (Windows defaults), `Repeat = true`.
  Releasing that key stops it; pressing another key moves the repeat to it.
- **Fresh press** = `wasPressedThisFrame`; `KeyUp` = `wasReleasedThisFrame`. Process presses in the order the frame
  saw them (stable order by the key table is acceptable; true order is not recoverable per frame).
- **Blur**: `TrialBoot.OnApplicationFocus(false)` and `OnApplicationPause(true)` → `KeyStack.Blurred` (battle clears FF
  and `escToSettings`) and drop all held / repeat state **without** synthetic keyups (a synthetic Esc keyup would open
  settings in battle).
- **EventSystem**: one, created by `TrialBoot` with `InputSystemUIInputModule`; set **`eventSystem.sendNavigationEvents =
  false`** and every `Selectable.navigation = None`, so Enter / Space / Esc / arrows never click or move uGUI selection
  behind the key stack's back. Pointer, click, scroll stay with uGUI handlers (`IPointerClickHandler`,
  `IPointerEnterHandler`, `IDragHandler`, `IScrollHandler`).
- **Text field guard**: while a `TMP_InputField` is focused (the journal memo), `Pump` dispatches only `'Escape'` (the
  journal handles it: blur + close) and nothing else, mirroring the textarea's `stopPropagation`. Japanese IME must be
  verified (`Keyboard.current.SetIMEEnabled(true)` while the field is focused; see risks).
- **`TabFocus`** (`Trial/Scenes/Common/TabFocus.cs`): scopes (the top modal: confirm card, explore plate, departure)
  register their buttons in order; Tab / Shift+Tab cycles and draws the `--wick` 2 px ring (offset 2); a pointer press
  (`Pointer.current.press.wasPressedThisFrame`) clears it; Enter / Space press the Tab-focused button first.
- **Static state**: `UPROJ/ProjectSettings/EditorSettings.asset:29-30` has Enter Play Mode Options on with domain reload
  **disabled**. Every static (key stack, open confirm cards, the departure's in-memory draft, cached fonts) needs a
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` reset.

---

## 5. Unity design

### 5.1 Layout

```
KIT/Trial/
  App/                      Trial.App.asmdef (noEngineReferences)  — save.js, recorder.js, life.js, names.js, intel.js + JSON + store + SaveKeeper
  Boot/                     Trial.Boot.asmdef (engine)             — TrialBoot.cs, Main.cs (router: the rest of main.js)
  Scenes/Common/            Trial.Common.asmdef (engine)           — stage.js: Stage.cs, Clock.cs, KeyStack.cs, ConfirmCard.cs, NoticeLine.cs, TabFocus.cs
  Scenes/Meta|Explore|Battle|Audio/                                 — other analysts
UPROJ/Assets/Scenes/Trial.unity                                     — one GameObject with TrialBoot (camera optional: Overlay canvas)
PORT/Trial.App/Trial.App.csproj, PORT/Trial.App.Tests/              — dotnet
```

Namespaces: `Trial.App`, `Trial.Boot`, `Trial.Common`, `Trial.Meta`, … Function names are the JS ones in PascalCase
(`toTitle` → `ToTitle`, `persistSoon` → `PersistSoon`, `replayLife` → `ReplayLife`).

### 5.2 `TrialBoot` (MonoBehaviour, `[DefaultExecutionOrder(-200)]`)

Inspector fields:

| Field | Default | JS equivalent / purpose |
| --- | --- | --- |
| `autobattle` | false | `?autobattle`: every fight fought unattended with `source.AutoCardId` (no battle scene mounted; `autoFight` `main.js:409-420`, guard 20000 steps, a throwing call falls back to `AdvanceAuto`) |
| `debug` | false | `?debug`: exposes `TrialBoot.Debug {Save, Live, Fight, Settings, Audio, Stage}` and enables context-menu commands (Dump save JSON to the console, Open save folder, Delete save, Force stale) |
| `saveFolder` | `"trial"` | folder under `persistentDataPath` (tests and captures use another) |
| `noStorage` | false | simulate blocked storage (`MemoryKeyValueStore`, `storageOk = false`) |
| `lineageSeed` | 0 | 0 = JS behaviour (time ^ random); else fixed seed for はじめから |
| `reducedMotion` | false | `prefers-reduced-motion` |
| `vSync` | true | `QualitySettings.vSyncCount = 1` (rAF pacing); else `targetFrameRate = 60` |

Responsibilities: build EventSystem + canvas + `Stage`; fonts; `TrialSettings.Load`; `TrialAudio`; `SaveKeeper.Open`;
create `Main` and call `Main.Start()` (→ `ToTitle`). `Update`: `KeyStack.Pump()`. `LateUpdate`:
`SaveKeeper.FlushIfDirty()`. `OnApplicationFocus(false)` / `OnApplicationPause(true)`: `KeyStack.Blur()` + flush.
`OnApplicationQuit` / `OnDestroy`: flush, destroy the scene, stop audio.

### 5.3 `Stage` (port of `stage.js:33-77`)

`Stage.Create(Transform parent) → Stage {RectTransform Root, Scenes, Overlays, Notices, Confirms; Vector2 ToStage(Vector2 screen);
RectTransform Layer(string name, RectTransform under = Scenes)}` plus `Place(RectTransform, x, y, w?, h?)`: anchors
(0,1)/(0,1), pivot (0,1), `anchoredPosition = (x, −y)`, `sizeDelta = (w, h)` (auto size when null is the UI kit's
job). Canvas: `ScreenSpaceOverlay`, scaler `ScaleWithScreenSize` 1920×1080 `Expand`, `pixelPerfect` off.

### 5.4 `Main` (port of `main.js`; plain C# class owned by `TrialBoot`)

Same functions and order as JS: `Show`, `OpenOverlay`, `CloseOverlay`, `JournalCtx`, `SceneNotices`, `ToTitle`, `ToHall`,
`ToDeparture`, `BeginLife`, `ResumeFight`, `ResumeLife`, `ToExplore`, `ToExploreBack`, `AutoFight`, `RunEncounter`,
`BattleFailed`, `EndLife`, `SaveQuit`, `ReloadStored`, `Start`. Data checks (`InSync`, `Persist`, stamps) delegate to
`SaveKeeper`. `live` becomes `LiveLife {RecordedExpedition Expedition; Expedition Raw; LifeLog Log; LifeStart Start;
LifeFlags Life; string Memo; List<Scribble> Scribbles}`. All texts of `main.js:46-51` and the three confirm cards go into
`AppText` (pure, in `Trial.App`).

Async: JS promises (`await new Promise(resolve => show(battle, {onFinished: resolve}))`, `confirmCard().then`) map to
`System.Threading.Tasks.Task` + `TaskCompletionSource` (supports several awaiters, `WhenAll` / `WhenAny` for
`Promise.all` / `race`); Unity's `UnitySynchronizationContext` resumes continuations on the main thread. Rules: no
`async void` except top-level entry points wrapped in try/catch that log; after every `await` check `stale` / `live` /
`alive` exactly where the JS does (`main.js:332,390`).

### 5.5 Scene contract

```csharp
public interface IScene { void Destroy(); void Refresh(); }          // Refresh may be empty (JS refresh?())
public interface IOverlay : IScene { void Close(); }                  // settings / owned / journal also return close()
public class SceneCtx { public Stage Stage; public ITrialAudio Audio; public TrialSettings Settings; }
// mount functions keep the JS shape: static IScene MountTitle(RectTransform host, TitleCtx ctx)
```

Context classes the router fills (field names = JS keys in PascalCase):

- `TitleCtx {HasSave, Unreadable, OnNew, OnContinue, OnSettings}`
- `HallCtx {Lineage, Seed?, OnDepart(LifeStart), OnSettings, OnJournal?, OnTitle}` (router passes no `OnJournal`, as JS)
- `DepartureCtx {LifeStart, Owned (InitialKinds ∪ PrototypeDeck.Kinds ids), SavedDeck, OnBack, OnSettings, OnSetOut(DepartureChoice)}`
- `ExploreCtx {Expedition (RecordedExpedition; the scene reads `.Raw`), Inheritance {TraceView(layer), Scribbles(), Take(picks)}, OnBattle() → Task, OnLifeEnd(ending), OnOverlay(kind), OnSaveQuit, OnPersist, OnCarving}`
- `BattleCtx {Source (settled recorded source), Layer, LayerTitle, MiasmaPercent, NodeKind, Chain {Index, Total}, DisclosureOf, OpenJournal(host, opts) → IOverlay, SettingsInBattle = true, OnSettings, OnSettingsChange, OnError, OnFinished}`
- `EndingCtx {Ending (endingView), Lineage, OnHall}`
- `SettingsCtx {CanSaveQuit, SaveQuitKind, InBattle, OnSaveQuit, OnChange, OnClose}`, `OwnedCtx {Expedition, OnClose}`,
  `JournalCtx {Expedition, Lineage, Mode, Memo, Scribbles, OnMemo, OnClose, EnemyIds?, DisclosureOf?}`

### 5.6 Overlay manager

Inside `Main`: one `overlay` field; `OpenOverlay(kind, extra)` creates a host under `Stage.Overlays`; while one is open
the title's app notices are hidden; `CloseOverlay()` destroys it and refreshes the scene. The battle's journal is the
battle's own (mounted via `ctx.OpenJournal` into its `over` layer). `ConfirmCard.Open(Stage, opts) → Task<bool>` lives
under `Stage.Confirms`, survives `Show()`, and `ConfirmCard.CancelAll()` answers every open card with false
(`reloadStored`). `ConfirmCard.AnyOpen` replaces `document.querySelector('.ocbg-confirm')`.

### 5.7 `Clock` (port of `stage.js:79-131`; may be merged with the animation analyst's tween engine)

`Clock(Action<float dt, float now> onFrame)`: `Now` (ms, real time from `Time.unscaledTimeAsDouble`), `Tween(ITweenable
obj, (string key, float to)[] to, float ms, Ease ease, float delay = 0) → Task`, `Wait(ms) → Task`, `Stop()` (resolves
every pending task). Semantics to keep: `from` is sampled when the delay ends; a new tween on the same object removes
the overlapping keys from older tweens and resolves an older tween left with no keys; `p = ms > 0 ? min(1, (now − t0)/ms) : 1`;
`dt` passed to `onFrame` clamped to 50 ms. Reduced motion: `ms = min(ms, 1)`, delay 0 (`util.js:93-99`).
`Ease`: `Out(t) = 1 − (1 − t)^3`, `In(t) = t^3`, `InOut(t) = t < .5 ? 4t^3 : 1 − (−2t + 2)^3 / 2`, `Linear`,
`Smooth(t) = t^2 (3 − 2t)`. `Styler` (`util.js:34-48`): `o` → `CanvasGroup.alpha`, `x`/`y` → offset (y down), `s` →
uniform scale, `r` → degrees (CSS clockwise → Unity `−r`); pivot must match the CSS transform-origin (default centre;
`'0 50%'` for the title and ending rules).

### 5.8 Confirm card and notice (exact look, `common.css:6-19`, tokens from `vendor/ds/tokens.css`)

- Confirm shade: full stage, `rgba(5,6,10,.62)`, z 900, card centred. Card: width 760, padding 32 / 36 / 28, background
  `--panel-solid #141925`, border 1 px `--steel #6d7787`, radius 6, shadow `0 8px 18px rgba(0,0,0,.45)`. Title:
  Shippori Mincho 700 30 px / 40 px, margin-bottom 12. Body: Zen Kaku Gothic New 400 20 px / 32 px. Button row:
  right-aligned, gap 16, margin-top 28, order [cancel, OK]. Button: Zen Kaku Gothic New 700 22 px, letter-spacing .06 em,
  `--ink #ecebf2`, padding 16 / 28, background `#141925`, border 1 px `#6d7787`, radius 6; hover border `--ink` and
  background `rgba(40,44,58,.92)`; primary border 2 px `--amber #e8a84c`, background `rgba(232,168,76,.16)` (hover .28);
  danger border `--hp-text #e36a5e`, background `rgba(194,72,63,.18)`. Default labels OK 「進む」, cancel 「やめる」.
- Notice: z 880, padding 12 / 20, Zen Kaku Gothic New 400 20 px / 30 px, `--ink`, background `#141925`, border 1 px
  `--rule rgba(157,166,186,.26)` with a **3 px left border `--caution #e6c35c`**, radius 6, shadow-tip, multi-line
  (`pre-line`), no pointer input; leaving: opacity → 0 over 600 ms (linear CSS transition) starting at `ms − 600`.

### 5.9 C# files

| File | Assembly | Ports | Responsibility | ≈ lines |
| --- | --- | --- | --- | --- |
| `App/Json.cs` | Trial.App | — | ordered JSON DOM, reader, writer (JSON.stringify-compatible) | 420 |
| `App/KeyValueStore.cs` | Trial.App | `save.js:19-27` | key-value store interface, file store with atomic write, memory store | 170 |
| `App/Save.cs` | Trial.App | `save.js` | keys, stamps, open / write / probe / broken copy, settings load / write, `TrialSettings` | 300 |
| `App/SaveData.cs` | Trial.App | `life.js` data shapes | `TrialSave`, `LifeSave`, `LifeStart`, `Choice` ↔ JSON | 260 |
| `App/Recorder.cs` | Trial.App | `recorder.js` | log, recorded facades, replay, settle, trim, read-only list | 520 |
| `App/SaveKeeper.cs` | Trial.App | `main.js:121-210` | persist / persistSoon / inSync / stale / syncIdle | 200 |
| `App/AppText.cs` | Trial.App | `main.js:46-51` + confirm texts | the app's own words | 60 |
| `App/Life.cs` | Trial.App | `life.js` | `ChoiceToData`, `MakeStart`, `StartPages`, `LifeScribbles`, `AddScribbles`, `LifeSetup` (+ `LifeFlags`) | 330 |
| `App/Names.cs` | Trial.App | `names.js` | `LifeJournal`, `CardName`, `EnemyName`, `EndOptions` | 80 |
| `App/Intel.cs` | Trial.App (or Exploration.Script as an opt-in class) | `intel.js` | `JournalIntel : IEnemyIntel` (`SurveyTarget`, `Arrived`, `SurveyedEnemy`, `LevelOf`, `ForNode`, `Pages`) | 210 |
| `Boot/TrialBoot.cs` | Trial.Boot | `main.js:19-35,404-407,512-524` | Inspector switches, build, frame pump, lifecycle flushes | 240 |
| `Boot/Main.cs` | Trial.Boot | `main.js` | router, overlays, encounter loop, autobattle, error paths | 460 |
| `Scenes/Common/Stage.cs` | Trial.Common | `stage.js:4-77` | canvas, letterbox, layers, `Place`, `ToStage` | 180 |
| `Scenes/Common/Clock.cs` | Trial.Common | `stage.js:79-131`, `util.js:34-99` | tween clock, easings, `Styler`, reduced motion | 230 |
| `Scenes/Common/KeyStack.cs` | Trial.Common | `stage.js:133-145`, `util.js:117-119` | key stack, JS key names, repeat synthesis, keyup, blur, text-field guard, `ModalSwallow` | 270 |
| `Scenes/Common/TabFocus.cs` | Trial.Common | `stage.js:161-174` | Tab-only focus ring and Enter / Space routing | 100 |
| `Scenes/Common/ConfirmCard.cs` | Trial.Common | `stage.js:176-213` | confirm card (`Task<bool>`), `CancelAll`, `AnyOpen` | 160 |
| `Scenes/Common/NoticeLine.cs` | Trial.Common | `stage.js:147-159` | one-line notice with timed leave | 70 |
| `PORT/Trial.App.Tests/*` | dotnet | `test/app/*.mjs` + new | JSON, store, stamps, open / broken, recorder coverage (reflection), replay, settle, resume, life, survivor, JS-save parity fixtures | 1100 |
| `Assets/Scenes/Trial.unity` + PlayMode smoke test | Unity | — | boot → title → はじめから → hall → departure → explore (autobattle) → ending → hall; save → restart → つづきから | 150 |

### 5.10 Implementation units

1. **app-save-core** (pure, dotnet-tested): `Json.cs`, `KeyValueStore.cs`, `Save.cs`, `SaveData.cs`, `Recorder.cs`,
   `SaveKeeper.cs`, `AppText.cs` + tests (JSON, store, stamp, openSave, broken copy, run-length, recorder coverage,
   replay, settle, trim). Compiles against today's C# for the methods that exist; the JS-extension method names are added
   to the replay switch as the other units land them. ≈ 2000 lines with tests.
2. **app-life-seam** (pure, dotnet-tested): `Life.cs`, `Names.cs`, `Intel.cs` + ports of `life.test`, `settle.test`,
   `survivor.test`, the life parts of `inherit-gains.test`, and the JS-save parity fixtures (with the node dump script in
   the browser-game repo). Depends on unit 1, the C# inheritance module, and the opt-in rule extensions of §5.11. ≈ 1300 lines.
3. **app-shell** (Unity): `TrialBoot.cs`, `Main.cs`, `Stage.cs`, `Clock.cs`, `KeyStack.cs`, `TabFocus.cs`,
   `ConfirmCard.cs`, `NoticeLine.cs`, asmdefs, `Trial.unity`, PlayMode smoke test. Can start with placeholder scenes
   (a coloured panel per screen with the transition fades) and swap in the real mounts as the scene units land. Depends on
   units 1–2, the UI kit (rounded rect, border, shadow, TMP text) and the audio port. ≈ 1700 lines.

### 5.11 Rule gaps the app depends on (JS-only today; port to C# as opt-in, default = today's C#)

- **Inheritance module** (`src/core/inheritance/Inheritance.js`, 1265 lines; C# has none, #106): `newLineage`,
  `startLife`, `traceNodeView`, `takeTrace`, `noteCarving`, `recordEnd`, `placeFromState`, `endingView`, `hallView`,
  `heirJournal`, `mergeMastery`, `toJSON` / `fromJSON` (validating). The save format depends on its JSON shape.
- **Expedition** (`Exploration.Bridge`): options `{trial, limits, carvingKnown}`; `Inherit(taken)` with
  `InheritedPages`, `InheritedTraceId`, `InheritedLayer`; a start-pages option that `BeginLife` re-applies (replaces the
  JS monkey patch, `life.js:205-211`); an inherit hook (replaces `life.traceTaken` patch, `life.js:212-213`); `Take`
  returning bool; `PendingResult` / `CloseResult`, `ShowTaken` / `PendingTaken` / `CloseTaken` (with JSON round-trip for
  the card), `TakeChest`, `TakeSurvey`, `Train`.
- **RunStart / ExplorationSession** (`Exploration.Script`): `RunStart.MapFor(profile, seed)`, `RunStart.Trial`,
  `RunStart.CarvingKnown`; `Act(action, arg)` with `TrialAction`; `Jump`; trial `UseConsumable` (aiming);
  `OpenTraining` / `PutOffTraining`; `Escape` stopping the aim; an **arrival hook** so `JournalIntel.Arrived` runs inside
  the logged call (today `Arrived` is private, `ExplorationSession.cs:356`) — e.g. an optional interface the session
  calls when `Intel` implements it.
- **CoreBattleSource** (`Depiction.Bridge`): `AutoCardId`, `TryPlay(cardId, zone, target)`, `TakeFreeStep(dir)` /
  `CanTakeFreeStep`, `TargetFor`, target-aware `Inspect` / `PreviewFor` / `RefusalText`; `Next` public or replayed as
  `AdvanceAuto`.
- **DeckLimits** (survivor 45-card cap, 4 tool slots) and `BattleHandIn.Limits` / `BattleLaunch.Limits`.
- **EnemyPages** (journal extension) for `JournalIntel.ForNode` / `Pages`.
- **Timing, not rule**: `EncounterBattle.Finish` at the decision (JS) instead of `TryFinish` after the scene (Unity view).

### 5.12 Kit needs (shared infrastructure the app needs from other units)

- Stage canvas with `Expand` scaler, 1920×1080 `StageRoot` with `RectMask2D`, ordered layer containers.
- TMP dynamic SDF font assets ready before the title (Shippori Mincho, Zen Kaku Gothic New, IBM Plex Mono).
- Vector UI kit: rounded rect fill + 1/2 px border + one-side accent border (notice), soft shadow (`0 8 18 .45`),
  full-stage shade.
- Tween clock + `Styler` with pivot handling (if another unit owns the tween engine, `Clock.cs` adopts it).
- `ITrialAudio`: `Play`, `SetAmbience(id, fadeMs)`, `DuckAmbience(factor, fadeMs = 600)`, `ApplySettings`, `Settings`,
  `OnChange`, `SetMaster/SetEffects/SetAmbienceVolume/SetMuted`.
- Input: `KeyStack` (JS key names, repeat synthesis, keyup, blur), `TabFocus`, EventSystem with navigation events off,
  IME-capable text field for the journal memo.
- Static-state reset hooks (domain reload is disabled in the project).

### 5.13 Risks

- **Disabled domain reload** (`EditorSettings.asset:29-30`): statics (key stack, confirm list, draft, caches) survive
  between play sessions unless reset with `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`.
- **Replay determinism** depends on every JS rule extension being ported bit-identically (float32 rounding as the JS
  `Math.fround` ports do, ordinal string ordering, and iteration order: JS `Map` / object iteration is insertion order,
  C# `Dictionary` enumeration order is not guaranteed after removals). Any rule that iterates a dictionary must sort or
  use an ordered structure; the JS-save parity fixtures catch drift.
- **Japanese IME** in the journal memo with the Input System only (no legacy `Input.compositionString`): verify
  `TMP_InputField` composition early; fallback is a small custom field on `Keyboard.onTextInput` /
  `onIMECompositionChange`.
- **Key repeat** is synthesised (500 / 33 ms) and press order within one frame is not recoverable; both differ slightly
  from the browser.
- **Linear colour space** (`ProjectSettings.asset:49`): alpha blending of shades and translucent panels happens in linear
  space and looks different from CSS sRGB blending (`rgba(5,6,10,.62)` etc.); the UI kit must decide a compensation.
- **Write cost**: the whole save is rewritten per frame while a fight plays; measure on a long life before shipping; a
  worker-thread write is the fallback. File locks by antivirus can make `File.Replace` fail; the retry / copy fallback
  covers it.
- **Async pitfalls**: exceptions in fire-and-forget tasks vanish unless wrapped; a scene destroyed while awaiting must
  check `alive`, as the JS does.
- **One write for a take**: the lineage change (`takeTrace`) and the `E.Inherit` log entry must land in the same write,
  or a crash in between lets a trace be taken twice (INHERITANCE R-21).
- `runInBackground: 0`: an unfocused player stops updating; tweens jump to their end on return (as a background browser
  tab does). The battle's real-time holds then complete at once; acceptable, but note it in playtests.
