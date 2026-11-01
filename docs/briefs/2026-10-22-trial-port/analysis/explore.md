# Area: explore — the exploration scene, ported to Unity (Trial.Explore)

Paths below: `JS/` = `…/scratchpad/proto/prototypes/browser-game/`, `KIT/` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port\unity-project-kit\Assets\View\`. The built artifact (`…/scratchpad/artifact/pretty/game.js:11913`) has the same constants as `JS/src/scenes/explore/ExploreScene.js`, so the source is the reference.

Rule of thumb for every row below: **the JS look and feel wins for the view; the C# wins for rules.** The scene computes nothing: every number and line comes from the bridge (`ExplorationScreenBuilder`, `Expedition`, `Inheritance`, `TraceGain`, `MasteryView`) or the scene text table (`text.js`).

---

## 0. What the area is

| JS file | Lines | What it is | C# target (proposal) |
| --- | --- | --- | --- |
| `JS/src/scenes/explore/ExploreScene.js` | 2356 | `mountExploreScene(host, ctx)`: the whole scene (band, map, walker, detail, carried, cards over the map, flows, interlude, keys) | `KIT/Trial/Explore/ExploreScene*.cs` (partial class, 6 files) |
| `explore.css` | 436 | every look / transition / keyframe | folded into the C# files that build each element + `ExploreStyle.cs` (constants) |
| `backdrop.js` | 241 | place canvas (sky, haze, motes, 400 ms crossfade), veil, interlude lull, miasma shade, `skyStrip` | `ExploreBackdrop.cs` |
| `icons.js` | 75 | 24×24 SVG path icons (node kinds, band, items) | `ExploreIcons.cs` (path strings + `Ico(...)`) |
| `text.js` | 104 | `SceneText` (scene-only Japanese lines) | `ExploreText.cs` |
| `cardFace.js` | 46 | `cardDefOf / cardDefById / cardName` → OCB.Card def | `ExploreCardFace.cs` (or the shared card-face helper from the battle unit) |

Bridge it reads (C# already exists for most; trial extensions are JS-only — see §6): `JS/src/bridge/explorationScript/*` (= `KIT/Exploration/Script/*`), `JS/src/bridge/exploration/*` (= `KIT/Exploration/Bridge/*` + JS-only `TraceGain.js`, `MasteryView.js`, `Loadouts.js`), `JS/src/bridge/backdrop/ExplorationBackdrop.js` + `MiasmaLook.js` + `BackdropSpec/Motion/Switch.js` (= `KIT/Backdrop/Script/*`, 1:1, nothing JS-only).

---

## 1. Stage layout (1920×1080 stage px)

Constants `ExploreScene.js:47-56`:

```
AREA     = { cx: 652, cy: 548, w: 1060, h: 680 }   // node centres: x = 652 + (X-0.5)*1060, y = 548 + (0.5-Y)*680
NODE     = 88                                     // node square
WALKER_UP = NODE/2 + 6 = 50                       // walker stands on the node's top edge
WALKER_BOX = 52
EDGE_INSET = NODE/2 + 8 = 52                      // edge line starts/ends 52 px from node centres
DETAIL   = { x: 1304, y: 112, w: 592, h: 604 }
CARRIED  = { x: 1304, y: 728, w: 592, h: 232 }    // grows upward; DETAIL's height = carriedTop - 12 - 112
MAP_CENTER = { x: 652, y: 540 }                   // plates centre here
```

Node centres span x 122…1182, y 208 (Y=1, entry row on top) … 888 (Y=0, boss row at the bottom). `NodeCard.X/Y` are C# floats from the builder (`ExplorationScreenBuilder.cs`, unchanged).

Draw order (DOM order + z-index, `ExploreScene.js:249-281`, `explore.css`). In uGUI make these sibling order, back to front:

| # | Element | Rect | z | Notes |
| --- | --- | --- | --- | --- |
| 1 | root `.ocbg-explore` | full | – | background `--ground` #10141d; class `is-returning` fades whole root 0→1, 420 ms ease-out |
| 2 | backdrop host | full | 0 | place canvas → veil → lull → shade (§5.2) |
| 3 | map layer | full | 2 | pool, edges, nodes, selection frame, walker (z3 inside); pointer-events only on nodes; `transition: opacity 500ms ease-in-out, filter 400ms ease-in-out` |
| 4 | detail panel | 1304,112 592×604 | 3 | `transition: opacity 400ms ease-in-out` |
| 5 | banner | x24 w1256 (y per kind) | 4 | |
| 6 | interlude view (`lullview`) | full | 4 | after banner in DOM (above it) |
| 7 | card layer (dim + plate) | full | 5 | dim covers 2–6 but not 8–11 |
| 8 | carried panel | 1304,728 592×232 | 6 | stays above the dim |
| 9 | 「この生を終える」 | 24,980 288×68 | 6 | |
| 10 | hint | 336,980 w≥724 h68 | 6 | |
| 11 | key legend | 1068,976 828×76 | 6 | right-aligned |
| 12 | band | 0,0 1920×96 | 7 | |
| 13 | fallback overlays | full | 8 | dev only (§5.17) |
| 14 | confirm card | full | 900 | `confirmCard` from `common/stage.js` |
| – | app overlay host (journal / cards / settings) | full | 500 (stage layer above the scene) | owned by the meta scenes |

---

## 2. Tokens, colours, fonts used by this scene

Tokens (`JS/vendor/ds/tokens.css`, dusk theme). Resolve once into a `Tokens` static (shared kit):

| Token | Value | Token | Value |
| --- | --- | --- | --- |
| ground | #10141d | panel | rgba(16,20,30,.88) |
| panel-solid | #141925 | card | #171c28 |
| ink | #ecebf2 | rule | rgba(157,166,186,.26) |
| steel | #6d7787 | amber | #e8a84c |
| wick | #ffd89a | crimson | #c2483f |
| hp-text | #e36a5e | violet | #8b6fd0 |
| phosphor | #b9a3ff | caution | #e6c35c |
| boss | #d08cf0 | washi | #f0e6d0 |
| washi-ink | #1d1a16 | keyline | rgba(16,20,30,0) |
| shadow-card | 0 6px 14px rgba(0,0,0,.45) | shadow-reach | 0 0 22px rgba(232,168,76,.5) |
| shadow-tip | 0 8px 18px rgba(0,0,0,.45) | shadow-lamp | 0 0 0 2px #ffd89a, 0 0 20px 5px rgba(255,216,154,.5) |
| radius-panel 6 · radius-tag 4 · radius-chip-self 15 | | | |

`color-mix(in srgb, X p%, transparent)` = X at alpha p. `color-mix(in srgb, A p%, B)` = lerp(B, A, p), opaque. Precomputed mixes the scene uses: amber 24% on panel-solid ≈ (71,59,46); amber 36% ≈ (96,77,51); amber 12% ≈ (45,42,42); amber 55% + ink ≈ (234,198,151) (edge flow stroke).

Fonts (`build.mjs:15` loads Shippori Mincho 400/700/800, Zen Kaku Gothic New 400/700, IBM Plex Mono 400/600/700):
- display = Shippori Mincho. **800 is used** (banner `strong`, plate `h2`, interlude `h2`, choice-tile `strong`, swap `strong`). `KIT/Resources/Fonts` has only `ShipporiMincho-Bold.ttf` → add `ShipporiMincho-ExtraBold.ttf` (OFL) or accept Bold.
- body = Zen Kaku Gothic New 400 / 700 (both present).
- numbers = IBM Plex Mono 600 (`.ocbg-explore-num`, `kbd`) → `IBMPlexMono-SemiBold.ttf` (present).
- CSS `letter-spacing: Nem` → TMP `characterSpacing = N*100` (TMP units are 1/100 em). CSS `font: 700 22px/28px` → size 22, line height 28 px (kit helper converts px line height to TMP `lineSpacing`).
- **`rich(text)`** (`ExploreScene.js:88`): digit runs in Plex Mono 600 (letter-spacing 0); runs of `■` drawn in ink at 34% alpha (washi-ink 38% on washi), letter-spacing .06em. In the layer title and the interlude layer name the digits stay in the display font 700 (`explore.css:30, 364`). Kit: `Rich(string, onWashi)` → TMP rich string with `<font="PlexMono-SemiBold">` / `<color=#…>` tags (runtime font assets must be registered with `MaterialReferenceManager` so `<font>` resolves).

Buttons (`common.css:6-12`, `.ocbg-btn`): 700 22/1 body, letter-spacing .06em, ink on panel-solid, 1px steel border, radius 6, padding 16 28. Hover: border ink, bg rgba(40,44,58,.92). `is-primary`: 2px amber border, bg rgba(232,168,76,.16), hover .28. `is-danger`: border hp-text, bg rgba(194,72,63,.18). Disabled: opacity .4. Focus ring (Tab only): 2px wick outline, offset 2; on washi and on the interlude plate 3px washi-ink, offset 3 (`explore.css:56`). On washi plates: buttons bg panel-solid; primary bg amber 24% on panel-solid, hover 36% (`explore.css:235-237`).

`kbd` (`explore.css:11`): inline box min 24×24, padding 0 6, 600 16/1 mono, ink, 1px steel border, radius 4, bg panel-solid.

Icons (`icons.js`): 24×24 viewBox, stroke 2.2, round caps/joins, colour by role. `heart` and `pip` are filled unless `{ outline: true }`. Options: `fill`, `fillOpacity`, `dashed` (2.4 2.4), `width`. Unknown names fall back to `pouch`. Paths contain `a` (arc) commands → the kit's SVG path parser must support M/L/H/V/C/S/Q/A/Z, absolute and relative. Node kinds use the NodeKind name as the icon key (`Battle`, `Elite`, `Boss`, `Rest`, `Survey`, `Event`, `Trace`, `Carving`). Items: `joka_no_ko`, `yakuso`, `koi_hoshiniku`, `wasuremizu`, `shirube_no_ishi`, else `pouch`. The training rows also use the design system's battle icons (`OCB.icon(ATTR.icon)`: sword / shield / star / flag) → shared with the battle unit.

---

## 3. Time base and easings

- One frame clock per scene (`createClock`, `stage.js:87`): drives backdrop drawing, roll labels, the gauge and the walker. While the app's battle scene is up (`inBattle`) the clock skips the backdrop (`ExploreScene.js:400`).
- `dur(ms)` (`ExploreScene.js:188`) = `round(ms × 0.35)` when 演出短縮 (`settings.shortFx`) or reduced motion is on, else ms. **Only JS timers and JS-driven tweens use `dur`**; CSS transitions and keyframes keep their length (reduced motion clamps every CSS animation to 1 ms, `common.css:20`). Port exactly that split: `Dur()` on the scene's waits/tweens listed as "dur(…)" below, CSS ports untouched by 演出短縮.
- JS easings (`stage.js:80`): `out` = 1−(1−t)³; `inout` = cubic in-out; `smooth` = t²(3−2t).
- CSS easings: `ease` (default, no function given) = cubic-bezier(.25,.1,.25,1); `ease-out` = (0,0,.58,1); `ease-in` = (.42,0,1,1); `ease-in-out` = (.42,0,.58,1); "decel" = cubic-bezier(.22,.61,.36,1); "std" = cubic-bezier(.4,0,.2,1). Kit needs a cubic-bezier evaluator.

---

## 4. Shared elements in detail

### 4.1 RollLabel (`ExploreScene.js:112-159`)
A label whose numbers roll when only the numbers change: split at `(\d+)`; if the non-digit parts match the old text, the digits tween from old to new over **520 ms, EASE.out, rounded each frame**, and the label gets class `is-rolled` → keyframe `ocbg-explore-roll` 900 ms ease-out: text-shadow 0 → `0 0 12px ink@70%` at 25% → 0 (`explore.css:33-34`). Used for 刻限, 瘴気, HP, スタミナ, 最大スタミナ, 訓練の刻み. No animation on the first render or when `animate` is false. In TMP: glow = underlay/glow material property tween (needs a per-label material instance) or a blurred duplicate.

### 4.2 Focus and keys (`ExploreScene.js:197-246`, `stage.js:133-213`)
- Key stack: the top handler gets keys first; returning true swallows. Each plate pushes its own handler; confirm cards push theirs.
- A button that **Tab** moved the focus to takes Enter/Space before the screen's own keys; focus set by a mouse press does not count (`lastKeyTab` / `tabFocus`). Under a confirmation only its own buttons answer. The legend names the focused button for Enter (§5.7).
- Held keys: `j`, `c`, `Esc` act on a fresh press only (`e.repeat` ignored); arrows repeat (OS repeat). Confirm cards ignore repeats.
- Kit needs: key stack on the Input System with OS-like repeat and an `IsRepeat` flag; Tab traversal over the scene's `Selectable`s in visual order; a focus ring graphic.

---

## 5. Every state and panel

### 5.1 Lifecycle (`ExploreScene.js:181-196, 2326-2356`)
- `mountExploreScene(host, ctx)` → `{ destroy(), refresh() }`. ctx: `expedition` (the recorder-wrapped Expedition), `inheritance { traceView(layer, nodeId), take(picks), scribbles() }`, `onBattle(expedition) → Promise`, `onLifeEnd(ending)`, `onOverlay(kind, info)`, `onSaveQuit()`, `onPersist()`, `onCarving()`, `audio`, `settings`.
- Start: `returning = expedition.Battle?.Over` (the app re-mounts the scene after the battle scene). Returning: root class `is-returning` (fade 420 ms ease-out), no layer banner. Otherwise `render({ enter: true })` (map assembles, §5.4) then `await layerBanner()` when `Phase == Exploring || HasPendingBattle`, then `proceed()`.
- The app **destroys** this scene to show a battle and mounts a fresh one afterwards (`main.js:390, 398`). Keep that contract in Unity (cheap: everything is built in code).
- `destroy()`: answer an open confirmation with cancel, pop keys, stop the clock, destroy the backdrop.
- `refresh()`: `render(); proceed();`.

### 5.2 Backdrop (`backdrop.js`, `explore.css:15-23`)
Stack, back to front:
1. **Place** (canvas at half resolution 960×540, scaled): sky = vertical gradient of `BackdropSpec.For(layer).Sky` stops; hazes, each alpha × `BackdropMotion.FlickerAlpha(haze.Flicker, now)`:
   - `TopGlow` / `Band`: vertical gradient, 21 stops of `BackdropSpec.HazeAlphaAt(haze, y)` on `haze.Color` at alpha 1.
   - `Radial`: radial gradient at (X·W, Y·H), radius `Size·H/2`, colour → transparent.
   - `Shafts`: `Count` shafts, `0.18·W` apart, centred on `X·W`, each `Size·W` wide and `1.6·H` tall, rotated **+Angle° clockwise** about (x, H/2); horizontal gradient 0 → lit at 25% … 75% → 0.
   - Motes: `Count` soft dots (radial colour → 0), **diameter `SizePx × 2.2` stage px**, alpha `Color.A × BackdropMotion.MoteAlpha`, position `BackdropMotion.MoteAt(motes, i, now, 1920, 1080)`.
   - Layer change: `BackdropSwitch.Show(layer, now)`; leaving slot drawn at 1, shown slot at `sw.Tick(now)` (400 ms smoothstep, `BackdropMotion.Crossfade`); a third layer mid-fade drops the oldest.
2. **Veil**: full rect, colour `ExplorationBackdropSpec.For(layer).Veil` = #10141d at alpha [.72, .68, .35, .70, .35, .35, .35] for layers 1…7; `transition: background-color 400ms ease-in-out`.
3. **Lull** (interlude only): `radial-gradient(60% 55% at 34% 88%, amber@14%, transparent 70%)` over `linear-gradient(ground@62% → ground@40%)` top to bottom; opacity 0↔1, 900 ms ease-in-out.
4. **Miasma shade**: 240×135 pixel buffer scaled to full screen. Per pixel `d = MiasmaLook.CornerDistance((x+.5)/240, (y+.5)/135, 1920/1080)`; `sh = MiasmaLook.ShadeAt(shownPercent, now)`; `a1 = ShadeAlphaAt(sh, d)`, `a2 = sh.SeepAlpha × EdgeFalloff(sh, d)`, `out = a2 + a1(1−a2)`, colour `(Seep·a2 + Shade·a1(1−a2))/out`. Redrawn only when `(Strength, InnerRadius)` changes. `shownPercent = MiasmaLook.Rise(from, to, now − at)` (500 ms smoothstep), set by the band (§5.3).
   - Unity: a runtime `Texture2D` 240×135 (RGBA32, bilinear) on a `RawImage` reproduces it exactly; it is generated, not baked. A ring mesh with vertex colours is the alternative.

`skyStrip(layer, 534, 300)` (`backdrop.js:198`): the interlude's look at the next layer. Same sky; radial hazes with radius `Size·ht/2`; shafts `max(4, Size·w)` wide, 2·ht tall, gradient 0 → full at 50% → 0 (no plateau); top-glow/band with 11 stops; then the next layer's veil at 0.6× its alpha. No motes, no flicker.

Unity's `KIT/Backdrop/BackdropLayers.cs` draws the same place (same shaft plateau, radial falloff, spacing 0.18, 1.6·H) **except the mote size** (it uses `SizePx`; explore uses `SizePx × 2.2`) and it may load `Resources/Backdrops/layer-n` art (none exists in RPG-by-card). It serves Battle.unity and must stay untouched, so the trial gets its own painter (`ExploreBackdrop.cs`, or a shared trial painter with a mote-scale parameter).

### 5.3 The band (`ExploreScene.js:285-323, 752-791`; `explore.css:26-65`)
0,0 1920×96, bg `panel`, bottom 1px `rule`, shadow `0 6px 18px ground@60%`, flex row, gap 24, padding 0 24, items centred vertically. Left to right (x positions from the flex): 

| Block | x, width | Content |
| --- | --- | --- |
| Layer | 24, 360 | `stairs` 28 ink + layer title (display 700 28/36, ls .04em, one line, ellipsis; digits display font). Text from `screen.LayerTitle` (「第 2 層　■■の秘跡」), redrawn only when it changes. |
| 刻限 | 408, 210 | row: `hourglass` 22 ink + TimeLabel (body 700 22/28, RollLabel). Pips below: margin-top 12, gap 6, each 14×14, radius 3, 2px steel border; lit (`i < TimeLeft`): amber fill+border, glow `0 0 8px amber@45%`; `transition: bg/border/shadow 420ms ease-out`. `TimeLimit > 10`: gap 4, 12×12. Trial badge `TimeBonusLabel` (「刻限の砂 +1」) absolutely under the block (top 100%+3): padding 0 8, body 700 16/18, 1px amber border, radius 15, bg amber@16%; hidden when empty. |
| 瘴気 | 642, 380 | row: `miasma` 24 phosphor + Label (RollLabel, 700 22/28) + forecast (margin-left auto, 400 18/24; `caution` 18 icon first when `MiasmaLook.ForecastMark(Percent, NextPercent)` ≠ ""). Gauge 380×18, margin-top 10, 1px border `MiasmaLook.Frame`, radius 4, clipped, bg `MiasmaLook.Track`. Inside: **ahead** bar (width `NextPercent%`, colour `MiasmaLook.AheadColor`; when `Warning ≠ None`: opacity pulse 1↔.45, 1.6 s ease-in-out infinite) → **fill** (width = `Rise(from,to,now−at)` %, colour `FillAt(shown)` with alpha `FillAlpha(to, now)` = breathing from 80%) → **ticks** (`MiasmaLook.Ticks()`: 9 ticks at 10…90%, 2px wide centred, height 100% at 20/40/60/80 else 40%, bottom-aligned, colour `MiasmaLook.Tick`). Trial `ReliefLabel` (「防瘴の面 濃さ 3 → 2」) absolutely under (top 100%+1), 400 17/18; hidden when empty. |
| Body | 1046, 460 | grid 186px + 1fr, rows 30 / 26, gaps 16 / 6: (1,1) `heart` 22 crimson + HpLabel; (1,2) `pip` 20 amber (filled) + StaminaLabel; (2,1) **訓練の刻み button** (MarksLabel; 400 16/22, padding 0 10, margin-left −10, radius 15, transparent border; enabled: amber border, bg amber@12%, hover @24%; disabled: plain text, no pointer); (2,2) MaxStaminaLabel 400 16/22. All four labels are RollLabels. |
| Buttons | right edge 1896, gap 8 | 手記（J）, 所持カード（C）, 設定（Esc）: each 72 tall, padding 6 12 4, column, gap 4, icon 28 ink (`journal` / `cards` / `gear`) over label 400 16/20; panel-solid bg, 1px steel, radius 6; hover (enabled): border ink, translateY(−2) (90 ms `ease`); disabled opacity .4. `cards` disabled = `!screen.CanOpenCards`, `settings` = `!screen.CanOpenSettings`. |

Gauge/backdrop sync (`drawBand`): when `g.Percent` changes, `from = Rise(oldFrom, oldTo, now−at)` (or the new value on first render / no animation), `to = Percent`, `at = now`; `backdrop.SetMiasma(Percent, now, instant)` the same way.

### 5.4 The map (`ExploreScene.js:446-595, 1302-1383`; `explore.css:68-112`)

**Nodes** (`buildMap`): each node is an 88×88 box at its centre.
- Lift wrapper (face + label): hover → `translateY(−6px) scale(1.04)`, 90 ms std easing; opacity transition 300 ms. Hover lifts any node, live or not.
- Face: panel-solid fill, 2px steel border, radius 6, `shadow-card`; icon `ico(kind, 44, colour)` centred; transitions border/shadow/bg 300 ms. Icon colour: Elite violet, Boss `--boss`, Trace amber, others ink; any **Far** node steel.
- Label: `KindLabel` centred, top 96 (8 px under the face), body 700 16/20 ink, no wrap, text-shadow `0 0 6px ground, 0 1px 2px ground` (TMP underlay in ground colour).
- Standing styles (`NodeStanding`):
  - Current: 3px amber border, shadows `shadow-card + shadow-reach`.
  - Reachable: amber border (2px) + **beckon** 2.4 s ease-in-out infinite: shadow `card + 0 0 0 0 amber@0` ↔ `card + 0 0 18px 2px amber@38%`.
  - Revisit: 2px border ink@60%.
  - Resolved: lift opacity .6; 1px `rule` border.
  - Far: 2px **dashed** steel border, fill panel-solid@72%, no shadow.
  - Boss (any standing): extra outline 2px `--boss` at offset 4 (dashed when Far).
  - `is-jump` (a 標の石 is aimed and the node has a jump option): 2px dashed amber, `shadow-reach`, beckon.
  - `is-live` (MapIsLive && !busy): pointer cursor only.
- Arrival ripple (`is-arrived`, after each step, class removed after 900 ms): a ring inset −3 (94×94), 2px amber, radius 6: 820 ms decel, opacity .9 → 0, scale 1 → 1.55.
- Mouse: enter → `onHover(id, true)` (+ `nodeHover` sound when the hovered id changes), leave → clear; click → `onNodeClick`: ignored unless map live and nothing busy/plated; a click on the already pinned node with options takes option 0; else pins it (`uiClick`).

**Edges** (`buildMap`, `updateMap`): one per `screen.Edges` entry, a line from `a + (b−a)·k` to `b − (b−a)·k`, `k = 52/len`.
- Base: stroke steel 2.5, round caps, dash `3 9`; transitions stroke/width 300 ms.
- Walked (both ends Current/Revisit/Resolved): ink@55%, solid.
- Lit (`edge.Lit && MapIsLive && !busy && !Aiming`): amber 4, solid, glow `drop-shadow(0 0 6px amber@55%)`; plus a **flow** line on top: stroke amber 55% + ink, width 4, round caps, dash `2 22`, opacity .95, dash offset 24 → 0 every 1.2 s linear (forever); it runs from the Current end toward the other end.
- The edge being walked gets the **trail** (§walk).

**Selection frame**: `OCB.TargetFrame(132, 146, { hook: 20, width: 3, glow: 12 })` = four corner hooks of 20 px, square caps, wick #ffd89a stroke 3 over a keyline stroke 7 (transparent in this theme), glow `drop-shadow(0 0 12px rgba(232,168,76,.35))`. Position `translate(x − 66, y − 44 − 18)` over the pinned node (so it frames node and label). `transition: transform 160ms decel, opacity 160ms`; hidden (opacity 0) when nothing is pinned and while `is-busy`.

**Pool**: 520×400 ellipse under everything, radial (closest-side) amber@16% → amber@5% at 55% → transparent, centred on the walker's node centre (no perch offset).

**Walker**: 52×52 box, z above nodes. Glow: 100×100 circle (inset −24), radial amber@42% → transparent, **breathe** 2.6 s ease-in-out infinite: opacity .75, scale .94 ↔ opacity 1, scale 1.06. Disc: 40×40 circle (inset 6), panel-solid, 3px amber border, shadows `0 2px 6px ground@80%` + `shadow-reach`, `person` icon 24 ink. Rest position: centre `(x, y − 50·perch − hop)` with perch 1. `is-landed` → disc keyframe 320 ms decel: scale (1.18, .84) → 60% (.96, 1.05) → 1. Walker opacity transition 240 ms ease-in-out (used by the leap).

**Enter animation** (map built with `enter`: new life, new layer, first mount unless returning): node `i` fades from opacity 0, translateY(14) to rest (opacity 420 ms ease-out, transform 420 ms decel) after `dur(200 + row·90)`; edge draws on (dash offset L → 0 over `dur(520)` decel) after `dur(260 + fromRow·90)`.

**Engaging dim**: during the encounter banner the map layer gets `filter: brightness(.55)` (400 ms ease-in-out); removed after the battle returns. Unity: multiply every map Graphic's `canvasRenderer` colour by .55 (works for TMP too) with a 400 ms tween.

**Interlude**: map, detail and banner fade to opacity 0 (`is-interlude`), nodes stop taking input.

**Selection by keys** (`moveSelection`, `ExploreScene.js:571`): from the pinned node (or current), among nodes with `along = v·dir > 4`, score `along + 2.2·|cross|`, ×0.6 when the node has options; pick the lowest; play `nodeHover`.

**Step** (`takeOption` → `afterMove`, `ExploreScene.js:1241-1300`):
1. `session.Step(id, choice)` (or `session.Jump(id, choice)` for an aimed 標の石). If `session.HasPendingStep` (瘴気死 ahead) → store `stepInfo`, render (the confirmation opens, §5.11).
2. `afterMove`: persist; on a fresh Carving call `ctx.onCarving()`; `busy = true`; sound `audio.stepTo({ fresh, line, miasmaDeath, result })` where `line = MaxStaminaPenalty(fc.MiasmaAfter) × 20` when max stamina drops and the step is not lethal, `result` = `train`/`rest` on a fresh Rest, `infoGained` on Survey/Carving, `eventResolve` on Event; a jump also plays `consumableUse`.
3. **Walk** (`walk`, `ExploreScene.js:1322`): `ms = dur(min(820, 420 + len·0.9))`; `e = EASE.inout(t)`; perch = smooth(1 − t/.24) for t < .24, smooth((t − .76)/.24) for t > .76, else 0; hop = sin(πt)·10; walker at `lerp(a, b, e)`. Trail: a line from `a + (b−a)k` to `b − (b−a)k` with `k = min(.45, 52/len)`, amber 5, round caps, glow `drop-shadow(0 0 7px amber@60%)`, revealed from the start: `along = clamp((e·len − k·len)/max(1, len − 2k·len))`. At t ≥ .7 call `render()` once (the band's numbers roll as the walker lands). At t = 1: `is-landed`, trail fades (opacity transition 480 ms ease-out) and is removed after `dur(520)`.
4. **Leap** (標の石, `leap`): walker opacity → 0 (240 ms); after `dur(320)` place it on the target, render, fade back in, `is-landed`; resolve after `dur(260)`.
5. Arrival: ripple on the node; `busy = false`; queue the trace card (fresh Trace, or one still undecided), training (non-trial), carving → `layerBanner()` after `dur(300)`, fresh Survey → note banner with `ExplorationScreenBuilder.SurveyLine(session)`; persist; render; `proceed()` (§5.18).

### 5.5 Node detail panel (`drawDetail`, `ExploreScene.js:600-659`; `explore.css:134-178, 254-255`)
Shows the hovered node (read only, no options) or the pinned node (with options unless busy). Redrawn only when (node id, hovering, busy, plate open) or the screen object changes. Panel: flex column, gap 16, padding 24 28, bg `panel`, 1px rule, radius 6, clipped.
- **Watermark**: the node kind icon at 260 px, ink, stroke width 1.2, absolute right −36 top 150, opacity .05, behind the content.
- **Head**: grid 72 | 1fr | auto, gap 16, centred. Token 72×72 (panel-solid, 2px steel, radius 6; amber border for Current/Reachable; dashed for Far; boss outline 2px at offset 3) with icon 40 in the node colour. Titles: `h2` KindLabel (display 700 32/40, ls .04em), `p` Note (400 18/26). Pill (top-aligned): `SceneText.Standing` word (今いる / 進める / 戻れる / 踏んだ / 遠い), padding 3 12, 700 16/22, 1px steel, radius 15; Current amber border + bg amber@22%, Reachable amber border, Revisit ink border, Far dashed.
- **Lines** (`node.Lines`): column gap 8, padding-top 14, top border rule. Each line split at the first 「: 」 when it sits at index 1…8 → grid 92 | 1fr (gap 12, baseline): key 700 16/24 ls .04em, value 400 20/28 (`rich`). No key → full width.
- **Options** (pinned, not busy): pinned to the bottom (`margin-top: auto`), grid gap 14, two columns when there are two. Option box: column gap 6, padding 12 14 14, bg panel-solid@85%, 1px rule, radius 6; lethal (`NeedsConfirm`) border caution; no forecast (「移る」) → no box. Inside: `【Label】` 700 18/24; one row per forecast line (gap 10, 400 18/26, no wrap) with icon 20 by its first word (刻限 `hourglass` ink, 瘴気 `miasma` phosphor, 最大スタミナ `pip` amber outline, スタミナ `pip` amber, HP `heart` crimson, else a 20 px blank); warnings: row gap 10, margin-top 2, padding 8 10, 400 17/24, bg caution@10%, 3px caution left border, radius 0 4 4 0, `caution` icon 22. Foot: button `ocbg-btn is-primary` (+`is-danger` when lethal), full width, padding 14 18, font 24, ls .12em, label = option label → `takeOption`.
- **Far**: not hovering, map live, not current and no lines → `ExplorationText.FarNode` (400 18/26, top border).
- **Fit**: if the content overflows, add `is-tight`, then `is-tighter` (smaller fonts/paddings, `explore.css:168-178`) until it fits. Unity: measure preferred height after layout and step down.

### 5.6 Carried panel (`drawCarried`, `fitCarried`, `ExploreScene.js:662-741`; `explore.css:181-210`)
1304,728 592×232, column gap 12, padding 16 24, bg panel, 1px rule, radius 6. **Grows upward** when its content needs more height (bottom edge stays at 960); the detail panel above loses the same height (`DETAIL.h = top − 12 − 112`).
- Tools, C# mode: `tool` icon 22 + `ToolsLabel` (400 20/26, no wrap).
- Tools, trial mode (`screen.ToolCards`): chips wrap (gap 6 8): chip padding 2 10, 1px amber border, radius 15, bg amber@10%, name 700 16/22 + effect 400 17 (`Effect` or `Side`); battle tools (`Side == 戦闘`) steel border, no fill. Hover → hint shows `SceneText.ToolNote(name, side, note)`.
- Slots: grid of 3, gap 12. Slot button: C# mode 112 tall; trial auto, min 120, padding-top 12, gap 2. Column centred, padding 8, panel-solid, 2px steel, radius 6, 700 18/22. `kbd` 1/2/3 at left 8 top 8 (22 tall; .5 alpha when empty). Icon `itemIcon(id)` 40 (trial 34). Name. Trial: forecast lines (one per line, 400 17/22, centred, no wrap) or the reason (16, wraps). 「使う」/「やめる」 (aimed) at right 10 top 8 (400 16/22) when usable or aimed. Usable: amber border, hover translateY(−4) and bg amber 12% on panel-solid (90 ms / 120 ms). Empty: dashed border, 400. Disabled non-empty: opacity .55. Aimed 標の石: wick border, `shadow-lamp`, opacity pulse 1.6 s. **Held** (a card that waits on the player is up, `plate.kind ≠ 'screen'`): slots opacity .5, steel border, no input, use label hidden. Hover → hint `SceneText.SlotHint(note, extra)`. Click → `useConsumable(i)`.
- Found row (C# mode consumables + pages found this life): 「拾った物」 key + chips (1px rule, radius 15, padding 1 10, icon 18, name, ×n). Inherited pages row 「受け継いだ物」 apart (`expedition.InheritedPages`).

### 5.7 「この生を終える」, hint, key legend
- End-life: `ocbg-btn`, 24,980 288×68, font 22, ls .08em. Hidden when `!screen.ShowsEndLife`. Disabled when `!CanEndLife || busy || confirmOpen || a waiting card`. Click → `onCardAction(AskEndLife)`.
- Hint: 336,980, h 68, flex centred left, 400 20/30, text-shadow `0 0 8px ground, 0 1px 2px ground`, balanced wrap. Text = `hintOverride` (tool/slot hover) or `screen.Hint` (empty under a waiting card). Width = `max(724, 1896 − legendWidth − 28 − 336)` so 標の石's long hint stays on one line.
- Legend (`drawLegend`): right-aligned, 1068,976 828×76, gap 20, items `kbd…kbd label` (gap 6), 400 16/22, text-shadow `0 0 6px ground`, no input. Contents:
  - Tab-focused button: 「Enter <its label>」 first, and the screen's own Enter/Space items are left out.
  - A plate's own `legend` list when a plate is up.
  - Map live: 「←→↑↓ 選ぶ」, 「Enter <option 0>」, 「Shift Enter <option 1>」, 「Esc やめる」 while aiming. Interlude: 「Enter <first button>」.
  - 「1–3 消耗品」 when a slot can be used and no waiting card is up.

### 5.8 Banners (`ExploreScene.js:1417-1492`; `explore.css:115-131`)
Box x24 w1256, column centred, gap 4, no input; bg horizontal gradient transparent → ground@92% (18%) → ground@92% (82%) → transparent; two 2px rules (left 24%, right 24%) at top 10 and bottom 10, amber @ .7 (violet for the encounter). Show: opacity 0 / translateY(8) → 1 / 0, 420 ms ease-out (both ways). A newer banner owns the element (`bannerSeq`); an older timer never hides it.
- **Layer** (`layerBanner`): y 392 h 200. `small` = part of LayerTitle before 「　」 (display 700 26/32, ls .2em); `strong` = the rest (display 800 64/76, ls .14em). Hides after `dur(1700)`; the promise resolves at `dur(1500)`.
- **Note** (`noteBanner(kicker, line, icon)`): y 412 h 160. `small` = icon 26 ink + kicker (gap 10); `strong` 40/52, ls .06em, max-width 1120, centred. Hides after `dur(2800)`. Used for 情報収集 (`SurveyLine`, icon Survey) and 訓練した (icon Rest).
- **Encounter** (`encounterBanner`): y 362 h 260, gap 6. Node-kind icon 56 in its colour; `strong` KindLabel 56/64; `em` enemy names (400 24/32, ls .04em) only when one enemy is at 開示度 ≥ 1, names via `EnemyPages.EncounterName` (「 + 」 within a fight, 「 → 」 between fights, 「？」 below 開示度 1); `small` 「n 連戦」 when several fights. Root gets `is-engaging` (map brightness .55). Hides and resolves after `dur(1200)`; `is-encounter` removed `dur(300)` later.

### 5.9 Plates over the map (`openPlate` / `closePlate` / `fitPlate`, `ExploreScene.js:846-887`; `explore.css:218-255`)
- Dim: full screen, ground@64%, takes clicks; fade in 260 ms ease-out; out 220 ms ease-in.
- Plate: flex column, gap 14, padding 36 44 32, radius 6, `shadow-tip`; **in** 300 ms decel (opacity 0, translateY 18 → 1, 0); **out** 220 ms ease-in (→ opacity 0, translateY 10); removed after `dur(220)`.
- Position: x = round(652 − w/2); y = given or after layout `max(120, round(540 − h/2))`.
- Typography: `h2` display 800 44/54 ls .06em; lines `p` 400 22/34, 6 below; kicker row gap 10, display 700 18/24 ls .08em; buttons row at the bottom (padding-top 10), right-aligned, gap 16, each min-width 220.
- Skins: **washi** (`is-washi`) bg #f0e6d0, text washi-ink, 1px washi-ink@22%, shadows `inset 0 0 90px washi-ink@14%` + `shadow-tip`. **solid** (`is-solid`, training) panel-solid, 1px steel. **end** (§5.10).
- While a plate other than the layer-break card is up (`kind ≠ 'screen'`), the carried panel is held, the marks button and 「この生を終える」 are disabled, and the hint is empty.
- Kinds: `'screen'` (layer-break / end / stand-in battle), `'choice'`, `'result'`, `'trace'`, `'train'`.

### 5.10 Screen cards (`drawScreenCard`, `ExploreScene.js:916-959`)
Drawn from `screen.Card` when no flow runs. Key Enter = first button. Legend 「Enter <first button>」.
- **LayerCleared** (階層ボスを倒した) / **PushedOut** (刻限が尽きた): washi, width 900, `is-break`: kicker icon `Boss` (cleared) or `hourglass` (pushed out) 28 washi-ink + LayerTitle; h2 52/64; lines with a top border (washi-ink@25%, padding-top 12); button 「階層間の休憩へ」 (padding-top 26). Slots stay usable under it.
- **MiasmaDeath / Fallen / Completed / Survived**: `is-end`, width 980, panel-solid, 1px steel, centred text, padding-top 44, bg `radial-gradient(80% 70% at 50% 0%, violet@14%, transparent 70%)`; mark circle 76×76 (1px rule) with icon 40: `miasma` phosphor / `heart` crimson outline / `stairs` amber; h2 64/76 ls .2em; lines padding 14 40 6 with a top rule; button centred (padding-top 22). Survived/Completed: amber@12% radial and border amber 50% + steel. Fallen: 3px crimson top border. Button 「新しい生で潜り直す」 → `ctx.onLifeEnd(lifeEnd(session, screen))` (the app's ending card, `lifeEnd` at `ExploreScene.js:165`).
- **Battle** (stand-in, only when there is no battle scene: `battleFallback`): generic washi card with 勝ったことにする / 倒れたことにする.

### 5.11 Confirmations (`openConfirm`, `ExploreScene.js:1137`; `stage.js:183`; `common.css:13-17`)
For `ConfirmStep`, `ConfirmDescend` (danger) and `ConfirmEndLife` (not danger). Shade full screen rgba(5,6,10,.62), z 900, centred card 760 wide, padding 32 36 28, `ocb-solid` (panel-solid, 1px steel, radius 6) + `shadow-tip`, in-animation 240 ms decel (`explore.css:398`). h3 display 700 30/40; body 400 20/32, lines joined with newlines; row right-aligned gap 16 margin-top 28: cancel (`Buttons[1]`, 「やめる」) then ok (`Buttons[0]`, primary, danger). Enter = ok, Esc = cancel, fresh presses only, a Tab-focused button wins. Sound `uiClick` on open; cancel → `session.Act(Cancel)`, `uiCancel`; ok → `onCardAction(Buttons[0].Action)`.

### 5.12 Trial choice cards (`drawChoiceCard`, `ExploreScene.js:974-1055`; `explore.css:404-433`)
Keys: 1…n pick, Esc = 「諦める」 when present. Legend lists them. Sound on open `uiClick` (Swap: `rewardReveal`).
- **EventChoice** (受ける / 受けない), width 1000: kicker `Event` 28 + 「イベント」 + sub (LayerTitle; margin-left 10, padding-left 12, left border washi-ink@30%, weight 400); h2 event name; lead (first line) 700 24/34; other lines with a top border (padding 8 0 4, 20/32). Tiles: grid 2 columns, gap 20: button min-height 150, padding 20 24 18 64, panel-solid, 2px steel (first tile amber), radius 6, ink; hover translateY(−4), bg amber 12% on panel-solid; `kbd` at 20,22; label display 800 32/40 ls .08em; note lines (split at 「　」) column gap 4, 400 18/26, each with a line icon (HP heart crimson 20, 最大スタミナ pip outline 18, スタミナ pip 18, 瘴気 miasma 20, カード seal 20, 頁 page 20, 消耗品 pouch 20, else a dot).
- **Swap** (消耗品の枠が埋まっている), width 1180: kicker `pouch` + 「拾った物」; h2; lead; body grid 300 | 1fr, gap 28. Found tile: column centred, gap 10, padding 18 18 20, 2px amber border, bg amber@10%, label 「拾った物」 display 700 16/22 ls .08em, icon 56, name display 800 30/38, note 17/24. Side: label 「持っている物」 + slots grid 3, gap 14: button column gap 8, padding 16 12 14, panel-solid, 2px steel; hover translateY(−4), amber border; kbd at 10,10; icon 40; name 700 22/28; note 15/21; chip 「入れ替える」 at the bottom (padding 4 14, 700 16/22, amber border, radius 15). Button 「諦める」 (Esc).
- **SurveyChoice** (写しの帳), width 900: kicker `Survey` + 「情報収集」; h2; lines; rows column gap 10: button padding 14 18, gap 14, panel-solid, 2px steel, 400 22/28; hover amber border, translateX(4); `kbd`, `Battle` icon 22 violet, label (bold).
- Answer → `session.Act(action, arg)`; persist; close; sound `consumableUse` (TakeFound) / `infoGained` (ChooseSurvey, then note banner with `SurveyLine`) / `eventResolve` (AcceptEvent) / `uiCancel`; render; `proceed()`.

### 5.13 Battle result, reward and unique-card pick (`resultFlow`, `ExploreScene.js:1602-1727`; `explore.css:257-298`)
Input: `expedition.PendingResult` (trial: `{ Back, Raised, PileSpent, Benched, DeckMax }`) or `{ Back }` from `Expedition.Return()`. Offer = `expedition.Offer` (an elite's unique cards or the card pile's three), `fits = expedition.OfferFits`.
- Plate washi `is-result`, width 1180, or 840 `is-compact` when no card to show. Keys: ←/→ select (wraps), 1…n select, Enter = learn the chosen card (or close when no offer), Esc = skip. Legend 「←→ 選ぶ / Enter この札を覚える / Esc 受け取らない」 or 「Enter 地図へ戻る」.
- Head: row space-between, padding-bottom 14, bottom border washi-ink@25%: h2 「戦いに勝った」/「戦いに敗れた」 (no wrap); stat chips (padding 6 14, 700 22/28, ink on panel-solid, radius 6): `heart` 22 + HpLabel, `pip` 20 + StaminaLabel.
- Body: grid 300 | 1fr, gap 36. Side (column gap 16, padding-right 28, right border washi-ink@22%; compact: two-column grid, gaps 40/14, no border): groups with `h4` (display 700 18/24 ls .06em) and rows `p` (400 18/26, icon 18 washi-ink): 覚えた札 (only when the pick shows on the right), Benched note, 習熟が上がった札 (`pip` outline 16 + 「突き → 鋭い突き」 + 「0 → 1 段目」 kept on one line, `MasteryView.nameAtRank`), 手記に書き足した敵 (`journal` icon, 「name　開示度 n / 2」, only `Raised` in trial), 拾った物 (item icons; pages + 「頁の中身はまだ書かれていない…」; PileSpent note; else 「拾った物はない」 at .7). Notes: 400 16/23.
- Main (column gap 18): h3 「1 枚を選んで覚える」 or 「覚えた札」 (display 700 26/34 ls .06em); card row centred, gap 32, padding 18 0 44. Each card 216×304 (`OCB.Card(cardDefById(id, mastery), { fit: 'b' })`), **deal** keyframe 460 ms decel with delay `dur(120 + i·110)`: opacity 0, translateY(36), rotate(−4°) → rest. Pickable: hover/chosen translateY(−10) (120 ms std); frame `TargetFrame(236, 324, hook 24, width 3)` at −10,−10, opacity 0 → 1 when chosen (140 ms); `kbd` i+1 centred 38 below (amber border when chosen). Click select, double-click learn. OfferBenched note under the row when a card would not fit.
- Buttons: 「受け取らない」 + 「この札を覚える」 (disabled until a card is chosen), or 「地図へ戻る」.
- Sounds: `rewardReveal` at +120 ms when cards show; select `rewardPick`; learn `cardAdded` (if `Learn` true) else `uiCancel`; after closing, `layerCleared` at +200 ms when the phase is LayerCleared.
- Close: `Learn(id)` (if chosen) → `CloseResult()` → persist → close plate → render → resolve after `dur(240)`.

**Event result** (`eventResultFlow`, `ExploreScene.js:1058`): same plate, width 1180 with a chest's card pick or 760 compact; kicker `Event` 26 + 「イベント」 + LayerTitle; h2 = outcome name + stat chips; side = outcome lines with line icons (22/32) and the pages note; pick as above. Sound `rewardReveal` or `eventResolve` at +80 ms.

### 5.14 Trace (痕跡) flow (`traceFlow`, `takenFlow`, `ExploreScene.js:1729-1982`; `explore.css:300-328`)
Opened on a fresh Trace step, or whenever the heir stands on an undecided trace (`traceWaiting()`, reload-safe). Never when the life is over.
- Plate washi `is-trace`, **fixed 1100×760** (x 102, y 160). Kicker `Trace` 28 + `view.PlaceLine`; h2 `view.Title`; heir line (display 700 22/30, margin-top −6); note (400 18/26); lead (`view.Lead` or `view.Refusal`); deck-full warning (`caution` 20 + `TraceGainText.DeckFullLead(DeckMax)`, 700) when the deck is full and the trace holds cards.
- List (fills the rest, scrolls, top border washi-ink@25%): candidate rows are buttons, grid 28 | 150 | 1fr | auto, gap 14, min-height 52, padding 6 8, bottom border washi-ink@15%, 400 18/24: check circle 22 (2px washi-ink@55%; picked: amber border + amber dot radial 60%/66%), kind tag (padding 1 10, 700 16/22, 1px washi-ink@40%, radius 4), name 700 20/26, detail, gain notes (column 3 to end, 400 16/22, one per line; 「nothing」 rows bold). Hover bg washi-ink@6%; cursor row bg washi-ink@8% + 3px amber inset on the left; locked (3 picked) .5. Withheld cards under `h4` (display 700 18/24, margin 14 0 4): rows grid 28 | 1fr | auto, .7, dashed check. Scroll cue: the list fades at the edge it continues past (mask 82%, or both edges 14%/86%). Unity: an edge gradient in the washi colour over the list, or nested RectMask2D softness.
- Rows show `traceGains(expedition, view.Candidates, picks, { scribbles })` (name/detail as this heir would hold them, notes), refreshed on every toggle.
- Footer: pick count (`pickState(picks).Line`, 700 24/30, pushed left), 「何も受け取らない」 (or 閉じる), 「受け取る」 (disabled until one pick).
- Keys: ↑↓ cursor over candidates then withheld rows (`nodeHover`), PageUp/PageDown scroll 80%, Home/End, Space toggle (`rewardPick`; max `InheritanceRules.LegacyPicks`), Enter take (when picks), Esc leave. Leave asks first: confirm 「何も受け取らずに去りますか」 / body `TraceLeaveBody(n)` / ok `view.LeaveButton` / cancel 「選び直す」 / danger.
- Take: `ctx.inheritance.take(picks)` → sound `traceTake` (or `uiCancel`); a 受け取った遺産 card is written (`takenCard`) and kept by `expedition.ShowTaken(card)`; close; after `dur(240)` `takenFlow`.
- **Taken card** (`takenFlow`): washi compact, width 840; kicker `Trace` + PlaceLine; h2 「受け取った遺産」; group `h4` HeirLine + rows (grid 22 | 150 | 1fr | auto: icon `seal`/`journal`/`page` 18, kind tag, name 700 20/26, detail; gain notes margin-left 34, 400 16/22); 「選ばなかった n 件は失われた」. Enter/Esc → `expedition.CloseTaken()`, persist, close.

### 5.15 Training (習熟訓練) (`trainingFlow`, `trainBeforeDescend`, `ExploreScene.js:1989-2135`; `explore.css:330-348`)
Opened after a rest's 「訓練する」 (trial: `session.TrainingWaiting`), from the band's 訓練の刻み button (`session.OpenTraining()`), or before 次の層へ降りる with marks left (one card per mark).
- Plate `is-solid`, **fixed 1060×780** (x 122, y 150). Kicker `Rest` 26 ink + MarksLabel (ink); h2 「訓練する札を選ぶ」 / 「降りる前に訓練する」; lead 400 20/28 body.
- Body grid 1fr | 216, gap 40. List (max-height 470, scrolls, top rule): one row per card kind in the deck: grid 28 | 1fr | 48 | 132 | 96, gap 14, min-height 46, padding 4 12, bottom rule, 400 18/24: attribute icon 24 (design-system battle icon in its ATTR ink), name display 700 20/26, ×copies, 刻み text (right; quiet 16 when no next 段), rank chip (padding 1 10, 700 16/22, amber border radius 15; 0 段: steel, 400). Hover bg ink@6%; chosen bg amber@16% + 3px amber inset. Scroll cue as the trace list.
- Preview: 216×304 dashed steel box (radius 6) with the chosen card (fade 160 ms ease-out); under it (top 318, width 236) lines 400 18/26: 元の名, 訓練すると 刻み n / m (・k 段目へ), 次の名, or the capped/top/flat note (`MasteryView.masteryProgress`). The mouse previews a row only on a real pointer move; leaving the list shows the chosen (or first) card.
- Buttons: 「あとで」 (or 「やめる」 before descend), 「刻みを捨てて降りる」 (danger, descend only), 「訓練する」 / 「訓練して降りる」 (disabled until chosen).
- Keys: arrows select (wrap; `rewardPick`), Enter train, Shift+Enter discard-and-descend, Esc later.
- Train: `expedition.Train(id)` → `uiConfirm`; note banner 「訓練した」 with `SceneText.RankUp(...)` or `Trained(name, ticks, need)` (icon Rest). Later: `uiCancel`, `session.PutOffTraining()`.

### 5.16 Interlude (階層間の休憩) (`drawInterlude`, `ExploreScene.js:2141-2191`; `explore.css:350-371`)
When `Phase == Interlude`: root `is-interlude` (map/detail/banner fade out), backdrop lull on, ambience `interlude`. Under a confirmation the rest card stays (drawn from `ExplorationScreenBuilder.InterludeCard(session)`).
- Rest plate (washi): x 96, width 1112, y = max(120, round(540 − h/2)); column gap 18, padding 44 56 40; shadows `inset 0 0 110px washi-ink@14%` + `shadow-tip`; in 600 ms decel. Kicker `Rest` 28 + LayerTitle; h2 display 800 60/72 ls .16em (「階層間の休憩」); rest lines 0–2 (column gap 14, padding 22 0, top and bottom borders washi-ink@25%): row gap 16, 400 26/36, icons 28 (heart crimson, pip amber, miasma phosphor). Buttons (margin-top 12, right-aligned, gap 16, min-width 260, panel-solid; 次の層へ降りる primary with `stairs` 24).
- Next-layer panel: 1304,112 width 592 auto height, column gap 16, padding 24 28, ink on panel, 1px rule, radius 6; in 700 ms decel after 120 ms. Kicker `stairs` 26 + 「次の層」; window 536×302 (1px rule, radius 4, clipped) with `skyStrip(next, 534, 300)` drifting (9 s ease-in-out infinite alternate: scale 1.02 → scale 1.08 translateY(−6)) under an inset vignette `0 0 40px ground@70%`; layer name h3 (display 700 32/42) and its numbers (400 22/34) split at 「: 」; 「！ 」 lines as warnings (caution style, 18/26); other lines 400 20/30.
- Keys: Enter/Space = first button. Slots and the marks button stay usable.

### 5.17 Overlays (journal, owned cards, settings) (`openOverlay`, `ExploreScene.js:2194-2267`)
The scene calls `ctx.onOverlay(kind, { book: screen.Journal, page, screen })`; the app mounts the meta overlay (`main.js:73`: `Meta.mountJournal` / `mountOwnedCards` / `mountSettings`) on a stage layer at z 500, which pushes its own keys. `main.js`'s `openOverlay` returns nothing, so the scene does not wait (it clears `overlayOpen` and renders at once). Sounds: `journalOpen` (journal) or `uiClick`. Gates: cards needs `screen.CanOpenCards`, settings `screen.CanOpenSettings`. The C# `session.Toggle(Overlay)` / `OpenOverlay` is not used by the trial scene. The fallback overlays (`fallbackOverlay`, a 576-wide washi drawer sliding in 220 ms decel / out 200 ms ease-in, and a 1060×780 page at 430,150) are a dev path for a missing `onOverlay`: low priority.

### 5.18 Flow controller (`proceed`, `ExploreScene.js:1495-1598`)
Loop (≤ 8 rounds) while alive:
1. `expedition.Battle?.Over` → `returnFlow`: `Return()`, persist, remove `is-engaging`, render; C# mode shows `resultFlow({ Back })` unless died.
2. `session.HasPendingBattle` and the encounter is not over → `battleFlow`: busy, render, `encounterBanner()`, `ctx.onBattle(expedition)` (the app replaces the scene). No `onBattle` → stand-in card. A battle that did not finish → `expedition.Abandon()`, stand-in card.
3. Trial `expedition.PendingResult` not yet shown by this mount → `resultFlow` (battle) or `eventResultFlow` (event).
4. `expedition.PendingTaken` not yet shown → `takenFlow`.
5. A trace waits → `traceFlow`.
6. Training waits → `trainingFlow`.
Then render.

`onCardAction(action, arg)` (`ExploreScene.js:1166-1238`): trial actions (§5.12); `NewLife` → `ctx.onLifeEnd`; `AskEndLife`; `Descend` with marks left → `trainBeforeDescend` first; `GoToInterlude` → `interlude` sound; `Descend`/`ConfirmDescend` → `afterDescend` (`descend` sound, `render({ enter: true })`, a death on entry plays `miasmaDeath` at +600 ms, else `layerBanner` then `proceed`); `ConfirmStep` → `afterMove` of the stored step; `ConfirmEndLife` → `uiConfirm`; `WinBattle`/`FallInBattle` → `proceed`.

`useConsumable(i)` (`ExploreScene.js:1385`): refused while busy, confirming, or under a waiting card; `session.UseConsumable(i)`; sound `uiClick` (start aiming) / `uiCancel` (stop aiming) / `consumableUse`; persist; render.

### 5.19 Keys (scene handler, `ExploreScene.js:2285-2324`)

| Key | When | Does |
| --- | --- | --- |
| J (fresh) | not (pending battle and busy) | journal overlay |
| C (fresh) | not busy | owned cards |
| Esc (fresh) | aiming, no plate, no confirm | `session.Escape()` (stops aiming), `uiCancel` |
| Esc (fresh) | otherwise, not busy | settings |
| 1 / 2 / 3 | not confirming; no plate or the layer-break card | the slot, only if CanUse or aimed |
| Enter / Space | interlude, no plate | first card button |
| ←→↑↓ | map live | move the selection |
| Enter / Space (+Shift) | map live | pinned node's option 0 (Shift: option 1) |
| Tab, Enter/Space | anywhere | focus traversal; a Tab-focused button presses first |
| plate keys | per plate | §5.10–5.15 |

### 5.20 Sounds (names are `Audio.js` presets; see `docs/AUDIO.md`)

| Moment | Call |
| --- | --- |
| hover id changes, arrow selection, trace cursor | `nodeHover` |
| pin a node, open a confirm / choice card, start aiming, non-journal overlay | `uiClick` |
| every step | `audio.stepTo({ fresh, line, miasmaDeath, result })` (step + at most one result) |
| 標の石 jump, consumable used, TakeFound | `consumableUse` |
| cancel, skip, stop aim, leave, put off training | `uiCancel` |
| ConfirmEndLife, training done | `uiConfirm` |
| GoToInterlude | `interlude` |
| descend / descend into death | `descend` / `miasmaDeath` (+600 ms) |
| boss result card closed | `layerCleared` (+200 ms) |
| result cards / swap card | `rewardReveal` (+120 / +80 ms) |
| select a card or trace row | `rewardPick` |
| learn a card | `cardAdded` |
| take a trace | `traceTake` |
| ChooseSurvey / AcceptEvent / event result without pick | `infoGained` / `eventResolve` |
| journal open | `journalOpen` (close is the meta drawer's) |
| ambience | `setAmbience(layer)` or `'interlude'` when the phase changes |

---

## 6. Bridge and session calls

### 6.1 The C# already has (`KIT/Exploration/Script`, `KIT/Exploration/Bridge`, byte-identical per `docs/port-notes/exploration-bridge.md`)
- `ExplorationSession`: `State`, `Screen()`, `Step(nodeId, choice)`, `UseConsumable(i)`, `SpendTrainingMark()`, `Act(ScreenAction)`, `ReportBattle`, `CanOpen`, `Toggle`, `Escape()`, `TryDescend()`, `HasPendingBattle`, `HasPendingStep`, `IsIdle`, `MapIsLive`, `CanEndLife`, `CanUseConsumables`, `ConfirmingDescend`, `PendingBattleNode`, `PendingEncounter`, `RunSeed`, `Life`, `CarvingTaken`, `Surveyed`, `InterludeHealed`, `LayerSeed`, `EncounterSeed`, `RunStart.TrialLoadout()`.
- `ExplorationScreenBuilder.Build`, `Forecast(before, nodeId, choice)`; `ExplorationScreen` with `MarksLabel`, `NodeCard`, `EdgeLine`, `MiasmaGauge`, `SlotCard(Index, Name, Note, CanUse)`, `CardButton(Label, Action)`, `ScreenCard`, `OverlayPage`, enums `NodeStanding`, `ScreenAction`, `CardKind`, `Overlay`.
- `ExplorationText`: `Glyph`, `KindLabel`, `Threat`, `LayerTitle`, `ItemName`, `ItemNote`, `EmptySlot`, `JournalButton`, `CardsButton`, `SettingsButton`, `EndLifeButton`, `FarNode`, `UnknownEnemy`, `Change`, `IsCombat`.
- `Expedition`: `Session`, `Deck`, `Owned`, `Mastery`, `Tools`, `Offer`, `Consumables`, `Pages`, `Battle`, `DisclosureOf`, `HandIn`, `StartEncounter`, `Return`, `RewardSeed`, `Abandon`, `Learn`, `Train`, `InitialKinds`. `BattleReturn` (`Won`, `Died`, `Learned`, `Offered`, `RankUps`, `Disclosure`, `Reward.Consumables/Pages`). `EncounterBattle.Over`.
- `Backdrop.Script`: `BackdropSpec`, `BackdropMotion`, `BackdropSwitch`, `ExplorationBackdropSpec`, `MiasmaLook` (all members the scene uses).
- `Journal.Script`: `JournalBook`, `JournalPage.MaxDisclosure`, `JournalTerms`. BattleCore: `Enemies.ById(id).Name`, `MasteryBook.RankOf/TicksOf/ProfileOf/TopRankHolder/Mastered`, `Mastery.Master`, `CardCatalog.ById`. Depiction.Bridge: `CoreText.FaceOf` (card face).

### 6.2 JS-only (the rules analyst ports them, opt-in; default = today's C#)
Session (`ExplorationSession.js`, trial mode, `RunStart.Trial`, `RunStart.CarvingKnown`, `RunStart.MapFor`):
- read: `Trial`, `IsAiming`, `AimingSlot`, `PendingEvent`, `PendingFound`, `HasPendingEvent/Found/Survey`, `SurveyChoices`, `SurveyTarget`, `SurveyPicked`, `TrainingWaiting`, `Events`, `EventState`, `InterludeState`, `Keeper`, `CanUseSlot(i)`, `EventAt(nodeId)`, `MapOf`, `EventSeed`, `LayerEnemies`; `IsIdle` widened by the trial waits.
- write: `Jump(nodeId, choice)`, `Act(TrialAction, arg)` (`AcceptEvent`, `DeclineEvent`, `TakeFound(slot)`, `LeaveFound`, `ChooseSurvey(enemyId)`), `UseConsumable` (aiming, refuse no-op items), `OpenTraining()`, `PutOffTraining()`, `Act(Cancel)`/`Escape()`/`AskEndLife` also stop aiming, `Receive`, `TurnDownFind`, `ResolveEvent`, `ApplyJump`, `CarvingKnownAt`.
Builder / screen: `ExplorationScreenBuilder.SurveyLine(session)`, `InterludeCard(session)` (public), trial `Forecast(before, id, choice, jumpSlot, event)`, `ChangeLines`; screen fields `ToolCards` (`ToolCard`), `TimeBonusLabel`, `ReliefLabel`, `Aiming`, `Events` (`EventOutcome`); `SlotCard.Forecast/Reason/Aiming/Id`; `CardButton.Arg/Note`; `StepOption.Jump`; `ScreenCard.IsChoice`; enums `TrialAction`, `TrialCardKind`; trial lines in `ContentLines` (rest gain 「・上限まで」, survey line, event lines, trace collected), journal rows 出来事 / carving page / density floor; ~50 `ExplorationText` members (`AimHint`, `JumpNote`, `JumpLabel`, `JumpRestLabel`, `JumpTrainLabel`, `NoEffectNow`, `NoJumpNow`, `ToolEffect`, `SideBattle`, `SideExploration`, `PlayerNote`, `Relief`, `ReliefFloor`, `TimeBonus`, `InterludeShare`, `CappedMark`, event / swap / survey / carving texts…). `TrialItems.js`.
Expedition (`Expedition.js`, options `{ trial, limits, carvingKnown }`): `Trial`, `PendingResult`, `CloseResult()`, `OfferFits`, `DeckMax`, `ShowTaken(card)`, `PendingTaken`, `CloseTaken()`, `TakeChest`, `TakeSurvey`, `EnemyName`, `Events`, `FoundPages`, `InheritedPages`, `InheritedTraceId`, `InheritedLayer`, `Take` returning bool, `Inherit(taken)` (added by `src/app/life.js`).
Other JS-only modules the scene reads: `TraceGain.js` (`traceGains`, `deckIsFull`, `TraceGainText`), `MasteryView.js` (`nameAtRank`, `masteryProgress`), `core/inheritance/Inheritance.js` (`pickState`, `traceNodeView` via `ctx.inheritance`), `bridge/journal/EnemyPages.js` (`EncounterName`), `app/intel.js` (`JournalIntel`, `SurveyTarget`), core `LayerEvents.js`, reducer `CanTeleport`, `Teleport`, `ResolveEvent`, `TakeConsumable`, `InterludePercent`, `InterludeHealOf`, `SpringHeal`, `MealCost`, state `Trial`, `TimeBonus`, `TimeLimit` with bonus, `ReliefKind` members after `Density`/`Gauge`, `RunLoadout.HasFreeSlot`/`ChoosesSurvey`/trial flag.

### 6.3 Which calls change state (go through the app's recorder, then `ctx.Persist()`)
`Session.Step`, `Jump`, `UseConsumable`, `Act(action[, arg])`, `Escape`, `OpenTraining`, `PutOffTraining`; `Expedition.Return`, `Learn`, `Train`, `CloseResult`, `ShowTaken`, `CloseTaken`, `Abandon`; `ctx.inheritance.take`; `ctx.onCarving`. Everything else the scene reads is read-only (`recorder.js:12` lists `Screen`, `CanOpen`, `DisclosureOf`, `TryDescend`, `CanUseSlot`, `EventAt`… as not logged). In C#, give the scene an interface the app implements with its recorder instead of a JS proxy.

---

## 7. Unity's existing exploration view: what is there, what is reusable

| File | What it has | Verdict |
| --- | --- | --- |
| `KIT/Exploration/ExplorationScreenView.cs` (413) | Stand-in look: grey squares 116 px with one glyph, map 1060×700, legacy `UnityEngine.UI.Text`, rebuilds every child on each `Render`, no walker, no animation, no hover detail beyond text, one generic card plate | Not reusable for the look. Keep untouched. Its structure (pin/hover, map fit) is trivial to redo. |
| `KIT/Exploration/ExplorationBootstrap.cs` (273) | Builds canvas (ScaleWithScreenSize 1920×1080, match .5) and EventSystem (InputSystemUIInputModule); J/C/Esc via `Keyboard.current`; fights by loading `Battle.unity` additively and hiding its own scene | Battle hand-off not reusable (the trial battle is a Trial view on the same canvas). `EnsureEventSystem` / canvas set-up are patterns for the trial bootstrap. |
| `KIT/Exploration/ExplorationPalette.cs` (55) | Grey stand-in colours | Replaced by tokens. |
| `KIT/Journal/JournalDrawerView.cs` (144) | Stand-in drawer: right 30%, flat grey, legacy Text, ScrollRect body | Replaced by the meta journal port (meta analyst). |
| `KIT/Backdrop/BackdropLayers.cs` (221) | Runtime-procedural sky, haze, shafts, motes with the 400 ms crossfade | Same place as explore's painter except mote size (×2.2 in JS explore). Serves Battle.unity, so do not change it. Port `backdrop.js` instead, or use a shared trial painter with a mote-scale parameter. |
| `KIT/UiKit.cs`, `UiTween.cs`, `ProceduralArt.cs` | Legacy Text helpers, coroutine tweens with 4 easings and a global speed, procedural sprites | Not enough for the trial (no TMP, no cubic-bezier, no keyframes, no per-scene clock). Font file names in `UiKit` are useful. |
| `KIT/Exploration/Script/*`, `Bridge/*`, `Backdrop/Script/*`, `Journal/Script/*` | Pure C# view-models, verified byte-identical with the JS | **Reuse directly.** Trial extensions go into these as opt-in (§6.2). |

Assembly note: `Exploration.Script.asmdef` and friends are `noEngineReferences: true`. The trial view needs references to `Exploration.Script`, `Exploration.Bridge`, `Backdrop.Script`, `Journal.Script`, `Depiction.Bridge`, `DungeonCore`, `DungeonContent`, `BattleCore`, plus `Unity.ugui` (TMP inside uGUI 2.x) and `Unity.InputSystem`. The JS-only modules (Inheritance, TraceGain, MasteryView, EnemyPages, JournalIntel) need homes in engine-free assemblies too.

---

## 8. C# file split (`KIT/Trial/Explore/`, namespace `Trial.Explore`)

`ExploreScene.js` becomes one `partial class ExploreScene` over six files that mirror its section headers, with the JS function names in PascalCase (`buildMap` → `BuildMap`, `drawDetail` → `DrawDetail`…), so a reviewer can diff section by section. **Unit A writes `ExploreScene.State.cs` first** (all fields and the helper signatures listed below); B–F can start against it in parallel and never edit each other's files.

| Unit | Files | ~Lines | Contents | Depends on |
| --- | --- | --- | --- | --- |
| A. Scene core | `ExploreScene.State.cs`, `ExploreScene.cs`, `ExploreStyle.cs` | 1100 | `Mount(host, ctx)`/`Destroy`/`Refresh`; ctx interface (`IExploreContext`: Expedition commands, Inheritance, OnBattle, OnLifeEnd, OnOverlay, OnPersist, OnCarving, Audio, Settings); layer tree and z-order (§1); fields (`screen`, `pinned`, `hovered`, `busy`, `plate`, `confirmOpen`, `flowRunning`, `pendingTrace`, `pendingTraining`, `battleFallback`, `walkerAnim`, gauge from/to/at/next, `firstRender`, `hintOverride`, `inBattle`, `shownResult`, `traceOffered`, `shownTaken`, node/edge maps); clock tick; `Dur`; `Render`; `RollLabel`; `Rich`; `Put`/`SplitRow`; Tab focus + `KeysWithFocus` + `Ask`; `DrawLegend`/`FitHint`/`DrawHint`; scene key handler (§5.19); `OpenOverlay` (+ optional fallback); `LifeEnd`; `ExploreStyle` = sizes, colours, easings from §1–§3 as constants | Kit (all), D, E (types only) |
| B. Map | `ExploreScene.Map.cs`, `ExploreNodeView.cs`, `ExploreEdgeView.cs` | 1200 | `NodePos`, `SetWalker`, `KindColor`/`NodeIconColor`, `BuildMap` (enter animation), `UpdateMap`, `OnHover`, `OnNodeClick`, `MoveSelection`, `TakeOption`, `AfterMove`, `TraceWaiting`, `Walk`, `Leap`, selection frame, pool, walker (glow breathe, landed squash), trail, edge flow, arrival ripple, engaging dim | A, kit vector + tween |
| C. Panels | `ExploreScene.Band.cs`, `ExploreScene.Detail.cs`, `ExploreScene.Carried.cs` | 1100 | `DrawBand`, `PaintGauge`, pips, gauge ticks, bonus/relief labels, band buttons, marks button, `SyncMarks`, `SyncEndLife`; `DrawDetail` (tight fit); `DrawCarried`, `FitCarried`, slots, tool chips, found rows; `UseConsumable` | A, D (icons), kit |
| D. Leaf data and backdrop | `ExploreText.cs` (text.js), `ExploreIcons.cs` (icons.js), `ExploreCardFace.cs` (cardFace.js), `ExploreBackdrop.cs` (backdrop.js incl. `SkyStrip`, shade texture) | 750 | Pure ports; the backdrop uses kit gradient/radial/shaft/soft-dot graphics and `Backdrop.Script` | kit vector, Backdrop.Script |
| E. Cards over the map | `ExploreScene.Plates.cs` | 1300 | `ClosePlate`/`OpenPlate`/`FitPlate`, `CardButtons`, `DrawScreenCard` (break / end / stand-in), `LineIcon`, `DrawChoiceCard` (event / swap / survey), `OpenConfirm`, `OnCardAction`, banners (`LayerBanner`, `NoteBanner`, `EncounterBanner`), `Proceed`, `BattleFlow`, `ReturnFlow`, `AfterDescend`, `DrawInterlude` | A, B (for `AfterMove`), D |
| F. Result flows | `ExploreScene.Results.cs` | 1400 | `ResultFlow` (reward + unique pick), `EventResultFlow`, `TraceFlow` (+ `MoreCue`, `KeepInView`, `HeldScribbles`), `TakenCard`, `TakenFlow`, `TrainingFlow`, `TrainBeforeDescend` | A, D, shared card face, rules ports (TraceGain, MasteryView, Inheritance) |

Tests (EditMode / dotnet where engine-free): `ExploreText` strings byte-identical to `text.js`; icon paths parse; `ExploreStyle` positions (node centre, walker perch, edge inset, plate x/y) against the JS formulas; `MoveSelection` scoring on a fixed map.

### Kit needs (shared, not this area's files)
1. Vector Graphics: rounded rect fill + border (1/2/3 px, per-corner radius for the gauge clip), **dashed border**, outer outline at an offset (boss), soft box-shadow (outer and **inset**, with spread), radial and linear gradients (multi-stop, elliptical radial at a position), line with dash pattern + offset + round/square caps + **reveal 0…1** + glow underlay, SVG path stroke/fill parser (arcs), corner-hooks target frame, soft dot, rotated shaft quad.
2. TMP: runtime SDF fonts (Shippori Mincho 700 **and 800**, Zen Kaku Gothic New 400/700, IBM Plex Mono 600/700) registered for `<font>` tags; px line-height helper; letter-spacing helper (em → 1/100 em); underlay/glow presets for text-shadow; ellipsis; per-label material instance for the roll glow.
3. Scene clock with ms tweens, cubic-bezier easings, CSS-like keyframe tracks (loops, alternate, delay, fill both), `Dur()` for 演出短縮 and a reduced-motion switch.
4. Layout: stack/grid with gap and padding (or uGUI LayoutGroups + ContentSizeFitter + an immediate rebuild before measuring), text preferred-size measuring.
5. Input: key stack on the Input System with OS-like repeat and `IsRepeat`; Tab traversal and a Tab-only focus ring; pointer enter/exit/click/double-click; wheel scrolling; "real pointer move" detection.
6. Buttons: `ocbg-btn` variants (default / primary / danger / disabled / hover / focus, washi variant), `kbd` chip, pill/chip.
7. Brightness multiplier for a subtree (`canvasRenderer.SetColor`), CanvasGroup fades.
8. Scroll list with edge-fade cue in the background colour and `KeepInView`.
9. Shared card face (`OCB.Card` 216×304, fit 'b') and design-system battle icons (from the battle unit).
10. Confirm card (`confirmCard`) shared with the other scenes.
11. Audio facade: `Play(name, delayMs)`, `StepTo(...)`, `SetAmbience(id)`.

---

## 9. Risks and open points
- **Rules dependency is large.** The trial screen is only reachable with trial mode on; almost every card in §5.12–5.15 needs a JS-only rule port (§6.2). Without them the scene runs in C# mode (stand-in texts, no events, no aiming, no pending results), which looks like the JS only for the map, band and battle result.
- **Inheritance is entirely JS-only** (#106 has no C#). The trace card and the taken card wait on that port and on the app's lineage store.
- **Save by replay**: the JS scene relies on a recording proxy. The C# needs an explicit command interface in the app, or 「つづきから」 parity breaks (pending results, taken card, training waiting are all designed for replay).
- **Shippori Mincho ExtraBold** is missing from Resources; without it the 800-weight headings fall back to Bold.
- **CSS filter/drop-shadow/masks** (edge glows, `brightness(.55)`, list fades) have no uGUI equivalent; the kit stand-ins (glow underlay lines, renderer colour, background-colour fades) must be checked side by side with the artifact.
- **Text metrics**: TMP line height and kerning differ from the browser; the detail panel's tight-fit steps and the hint width depend on measured widths, so expect small layout differences to tune.
- **Rebuild cost**: the JS rebuilds the detail, carried panel and plates on change; doing the same in uGUI each render is fine for these sizes, but the map must be built once per layer and only restyled (`UpdateMap`), as the JS does.
- `main.js`'s `openOverlay` returns nothing, so the scene does not block on overlays; the Unity router should keep that (the overlay owns its keys).
- `dur()` only scales JS timers; porting CSS keyframes through the same `Dur()` would make 演出短縮 faster than the JS.
