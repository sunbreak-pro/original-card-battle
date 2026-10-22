# kit: the design system and the visual primitives, from the browser game to Unity (Trial.*)

Analyst report for the architect and the implementers. Area: the design system (DS) and every drawing primitive the
scenes need (tokens, components, vector shapes, text, stage, motion).

Paths used below:

- `$G` = `C:\Users\user\AppData\Local\Temp\claude\C--Users-user-orca-original-card-battle\1ab86662-d6cc-4831-aa8c-76f9592e0ca6\scratchpad\proto\prototypes\browser-game`
- `$DS` = `$G\vendor\ds` (`tokens.css`, `bundle.css`, `bundle.js`; byte-identical to the artifact's `tokens.css`, `ocb.css`, `ocb.js`, checked with diff)
- `$KIT` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port\unity-project-kit\Assets\View`
- `$PORT` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port`
- `$UP` = `C:\Users\user\Unity\RPG-by-card` (Unity 6000.5.5f1, URP 2D, uGUI 2.5, TMP essentials imported, Input System 1.19)

**Who wins.** The look and feel (px, colour, ms, easing, stacking) is the JS/DS. Rules and words are the C# cores. The
existing Unity view helpers (`$KIT\UiKit.cs`, `UiTween.cs`, `ProceduralArt.cs`, `BattleTheme.cs`) are reference only:
they live in Assembly-CSharp, so an asmdef'd `Trial.*` assembly cannot reference them anyway. Where they agree with
the DS (BattleTheme colours = tokens; `UiTween.Apply` = the JS cubic ease set) the values are simply re-declared.
Where they differ the DS wins (ProceduralArt's raster sprites → vector meshes; UiKit's "800 drawn as 700" → a real
ExtraBold file; UiTween's coroutine → the scene clock).

---

## 0. Summary of decisions proposed here

1. Two assemblies. `Trial.Geometry` (engine-free, netstandard2.1, C# 9, dotnet-tested) holds every number-crunching
   part: colours and `color-mix`, eases and cubic-bezier, CSS keyframe sampling, SVG path parsing/flattening,
   triangulation, stroking (caps, joins, dashes, soft profiles), rounded boxes with borders and shadows, gradients,
   hatching, polygon clipping, the flex solver, CSS value parsing, text-metric math, and the data tables (icons,
   figures, tokens). `Trial.Kit` (Unity) turns those meshes into uGUI `Graphic`s, configures TMP, builds the stage,
   runs the clock and the input helpers, and hosts the DS port (`Ocb.*`).
2. All shapes are vertex-coloured meshes on the default UI material (one material, one white texture, batches well).
   Anti-aliasing is a 1-screen-pixel alpha fringe; blur is approximated with erf-profile rings; gradients are exact
   for linear bands (polygons split at stop isolines) and dense tessellation for radial ones. No texture files.
3. Text is TMP dynamic SDF, eight font assets (Shippori Mincho 400/700/800, Zen Kaku Gothic New 400/700, IBM Plex
   Mono 400/600/700). Three TTFs must be added to `fetch-fonts.mjs`: `ShipporiMincho-Regular.ttf`,
   `ShipporiMincho-ExtraBold.ttf`, `IBMPlexMono-Regular.ttf`.
4. A small CSS-value parser lets implementers paste the CSS strings verbatim (`Css.Box("background: var(--panel); border: 1px solid var(--rule); border-radius: 6px")`),
   so the C# can be diffed against `bundle.css` / scene CSS line by line.
5. The project is in **Linear colour space** (`$UP\ProjectSettings\ProjectSettings.asset:49 m_ActiveColorSpace: 1`).
   uGUI then blends in linear space and every translucent panel, scrim, shadow and AA edge comes out lighter than in
   the browser. The kit renders the Trial canvas into a gamma-space render target (§4.12). This needs a spike first.
6. Kit units: K1 geometry paths/meshes, K2 geometry CSS box/paint/layout, K3 Unity runtime (stage, graphics, text,
   motion, input, `Trial/Common/Stage.cs`), K4 DS port (`Ocb.*`). See §5.

---

## 1. Tokens

Source: `$DS\tokens.css` (5 lines; line 2 = `:root, [data-theme="dusk"]`, line 3 = wakakusa-a, line 4 = wakakusa-b,
line 5 = sizes and fonts). The game always runs the **dusk** theme (`$G\build.mjs:51` `<div id="app" data-theme="dusk">`;
no JS ever changes `data-theme`). The per-layer sky / haze / floor colours come from the ported `BackdropSpec`, not
from the themes. The wakakusa values are listed for completeness; nothing in the trial reads them.

### 1.1 Colours (dusk)

| token | value | Unity `BattleTheme` today | notes |
| --- | --- | --- | --- |
| `--ground` | `#10141d` | `Ground` | stage letterbox and scene grounds |
| `--panel` | `rgba(16,20,30,.88)` | `Panel` (#10141e .88) | same colour |
| `--panel-solid` | `#141925` | `PanelOpaque` | |
| `--card` | `#171c28` | `CardGround` | |
| `--ink` | `#ecebf2` | `Ink` | |
| `--ink-muted` | `#a9adbd` | `Ink2` | |
| `--rule` | `rgba(157,166,186,.26)` | `Line` | |
| `--steel` | `#6d7787` | `Steel` | |
| `--amber` | `#e8a84c` | `Amber` | |
| `--wick` | `#ffd89a` | `Wick` | |
| `--crimson` | `#c2483f` | `Scarlet` | |
| `--hp-text` | `#e36a5e` | `HpInk` | |
| `--violet` | `#8b6fd0` | `Violet` | |
| `--reach-red` | `#ff1f4f` | `AimRed` | floor hatching and swatches only |
| `--phosphor` | `#b9a3ff` | `Phosphor` | |
| `--guard` | `#8fc4d8` | `Guard` | |
| `--miss` | `var(--steel)` = `#6d7787` | `Whiff` | |
| `--caution` | `#e6c35c` | `Caution` | |
| `--boss` | `#d08cf0` | `Boss` | |
| `--washi` | `#f0e6d0` | `Paper` | |
| `--washi-ink` | `#1d1a16` | `PaperInk` | |
| `--figure-self` | `#2b2f3a` | — | |
| `--figure-enemy` | `#1b1722` | — | |
| `--flood` (not a token) | fallback `#06090c` | `InkBlack` | used as `var(--flood, #06090c)` in field.css:80, hud.css:160/186; never set |

### 1.2 Per-theme values (only these differ)

| token | dusk | wakakusa-a | wakakusa-b |
| --- | --- | --- | --- |
| `--sky-top` | `#1d3340` | `#f4f2df` | `#e3ebd3` |
| `--sky-bottom` | `#182b37` | `#dde9cb` | `#c3d6ad` |
| `--meadow-top` | `#182b37` | `#b9d394` | `#97b878` |
| `--meadow-bottom` | `#152530` | `#86a862` | `#64844c` |
| `--earth-top` | `#13212b` | `#5f7d45` | `#4a663a` |
| `--earth-bottom` | `#0f1a22` | `#10170d` | `#0c130b` |
| `--floor-tile` | `rgba(10,16,22,.72)` | `rgba(14,20,12,.82)` | `rgba(12,18,12,.80)` |
| `--sun` | `rgba(255,255,255,.10)` | `rgba(255,255,255,.55)` | `rgba(255,255,255,.35)` |
| `--haze` | `rgba(255,255,255,.05)` | `rgba(255,255,255,.14)` | `rgba(255,255,255,.10)` |
| `--keyline` | `rgba(16,20,30,0)` | `rgba(16,20,30,.85)` | `rgba(16,20,30,.85)` |

Consequence: in dusk `--keyline` is fully transparent, so the TargetFrame's keyline stroke, the throw line's
`box-shadow: 0 0 0 2px var(--keyline)` and the throwline drop-shadow draw nothing. Keep them in code (theme-driven)
but skip zero-alpha geometry.

### 1.3 Sizes, strokes, opacities, screen (`tokens.css:5`)

| group | tokens |
| --- | --- |
| space | `--space-4/6/8/12/16/24/32/48` = 4/6/8/12/16/24/32/48 px |
| radius | `--radius-panel` 6, `--radius-plate` 5, `--radius-tag` 4, `--radius-chip-foe` 3, `--radius-chip-self` 15, `--radius-pip` 50% |
| stroke | `--stroke-rule` 1, `--stroke-frame` 2, `--stroke-icon` 2.2, `--stroke-bold` 3, `--dash` 3, `--hatch-gap` 11 |
| opacity | `--opacity-unpayable` .6, `--opacity-held` .92, `--opacity-idle-hooks` .75, `--opacity-unplayed` .55, `--opacity-enemy-turn` .4, `--opacity-guard-zero` .7 |
| screen | `--screen-w` 1920, `--screen-h` 1080, `--card-w` 216, `--card-h` 304, `--cell-h` 40, `--figure-h` 226 |

### 1.4 Shadow tokens (parsed)

| token | layers (x, y, blur, spread, colour) |
| --- | --- |
| `--shadow-card` | (0, 6, 14, 0, rgba(0,0,0,.45)) |
| `--shadow-lamp` | (0, 0, 0, 2, #ffd89a), (0, 0, 20, 5, rgba(255,216,154,.5)) |
| `--shadow-threat` | (0, 0, 8, 0, rgba(255,31,79,.3)) |
| `--shadow-reach` | (0, 0, 22, 0, rgba(232,168,76,.5)) |
| `--shadow-omen-active` | (0, 0, 30, 0, rgba(139,111,208,.6)) |
| `--shadow-tip` | (0, 8, 18, 0, rgba(0,0,0,.45)) |

### 1.5 Font tokens

`--font-display` = Shippori Mincho; `--font-body` = Zen Kaku Gothic New; `--font-mono` = IBM Plex Mono (the system
fallbacks in the stacks are irrelevant in Unity). Weights actually used (every `font:` shorthand in `$G\src` and
`$DS`, counted with grep; `font-weight:` declarations checked by hand):

| family | weight | sizes used (px) |
| --- | --- | --- |
| display | 400 | 18, 20, 34, 46 (title tagline, ending sentence, memo sheet, flavour text, scribbles) |
| display | 700 | 15–36 (15, 16, 17, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36), 64 |
| display | 800 | 30, 32, 36, 44, 48, 56, 60, 64, 72 (banners, end paper h2, plates, hall title, heir name, seal, ending h1, journal h2, chain banner, result title) |
| body | 400 | 14, 16, 17, 18, 20, 22, 24, 26 |
| body | 700 | 16, 18, 20, 22, 24 |
| mono | 400 | 14, 16 (attr count badge, button key hints `small`) |
| mono | 600 | 16, 18, 20, 22, 24 (corner, speed, chips' numbers, stance ×n, explore numbers, kbd) |
| mono | 700 | 16, 20, 22, 24, 26, 28, 34, 36, 48, 64 |

### 1.6 DS layout constants (`$DS\bundle.js:1310-1320`, `OCB.LAYOUT`)

`W 1920, H 1080, floorX0 300, floorX1 1620, floorY 560, cellH 40, cellGap 3, footY 570, omenGap 14, omenW 200,
omenH 98, panelY 614, panelSelfW 300, panelFoeW 210, edge 24, stanceY 470, stanceW 250, stanceH 52, stanceGap 6,
stanceMax 3, distY 563, throwX0 400, throwX1 1520, stamina {32, 846}, piles {x 32, y 948, w 64, h 84, gap 22},
attrs {1718, 896}, guardPre {1560, 984}, endTurn {1672, 976}, corner {24, 20}, journal {1840, 20}, speed {1708, 20},
hand {cx 960}, refusalBottom 884, bannerY 400`. The battle scene reads them through `$G\src\scenes\battle\layout.js`;
port them once as `DsLayout` (engine-free constants) so `layout.cs` can quote them.

### 1.7 C# design

```csharp
namespace Trial.Geometry
{
    public enum DsTheme { Dusk, WakakusaA, WakakusaB }

    public static class DsTokens
    {
        // colours (dusk = :root); Rgba is straight-alpha sRGB, 0..1
        public static readonly Rgba Ground = Rgba.Hex(0x10141d);
        public static readonly Rgba Panel = Rgba.Bytes(16, 20, 30, .88f);
        public static readonly Rgba PanelSolid = Rgba.Hex(0x141925);
        // ... one field per row of §1.1, PascalCase of the token name (InkMuted, HpText, ReachRed, WashiInk, FigureSelf ...)
        public static readonly Rgba Flood = Rgba.Hex(0x06090c);       // var(--flood, #06090c)

        public static Rgba SkyTop(DsTheme t);   // ... the ten themed tokens of §1.2 as functions of the theme
        public static Rgba Keyline(DsTheme t);

        // sizes
        public const float Space4 = 4, Space6 = 6, Space8 = 8, Space12 = 12, Space16 = 16, Space24 = 24, Space32 = 32, Space48 = 48;
        public const float RadiusPanel = 6, RadiusPlate = 5, RadiusTag = 4, RadiusChipFoe = 3, RadiusChipSelf = 15;
        public const float StrokeRule = 1, StrokeFrame = 2, StrokeIcon = 2.2f, StrokeBold = 3, Dash = 3, HatchGap = 11;
        public const float OpacityUnpayable = .6f, OpacityHeld = .92f, OpacityIdleHooks = .75f, OpacityUnplayed = .55f,
                           OpacityEnemyTurn = .4f, OpacityGuardZero = .7f;
        public const float ScreenW = 1920, ScreenH = 1080, CardW = 216, CardH = 304, CellH = 40, FigureH = 226;

        // shadows (§1.4) as immutable arrays of BoxShadow
        public static readonly BoxShadow[] ShadowCard, ShadowLamp, ShadowThreat, ShadowReach, ShadowOmenActive, ShadowTip;

        /// <summary>var(--name) lookup for the CSS parser: colours, sizes (as px), shadows. Null when unknown.</summary>
        public static bool TryResolve(string name, DsTheme theme, out CssValue value);
    }

    public static class DsLayout { /* §1.6 as consts / small structs */ }
}
```

The Unity side adds `Rgba.ToColor()` / `ToColor32()` extension methods in `Trial.Kit` (no UnityEngine in Geometry).

---

## 2. The DS components

### 2.0 Which components the game actually uses

Counted with grep over `$G\src` (`OCB.x` calls and `ocb-` class names). The game mounts the stage root with class
`ocb-root` (`$G\src\scenes\common\stage.js:49`), so every `.ocb-root .ocb-*` rule applies inside every scene.

| component (bundle.js line) | used by the game | how |
| --- | --- | --- |
| `icon` (113) + `ICONS` (64-101) | yes, 32 call sites | everywhere |
| `hatch` (132), `swatch` (141) | yes | floor threat tiles, omen badge, status panel |
| `STATUS` (168), `ATTR` (182) | yes | chip names → icons (`hud/util.js:91`), training rows |
| `Card` (197) + `fitCardText` (257) | yes: explore (fit `'b'`), meta (`cardFace.js`) | the battle hand builds its own face from the same DS classes (`battle/hand-card.js`) |
| `CardDetail` (288) | classes only (`.ocb-detail*`) | `battle/hand-card.js`, `hand-panels.js` |
| `OmenPlate` (313), `ReserveOmen` (333) | classes only (`.ocb-omen`, `.ocb-omen-row1/2`, `.ocb-hitmark`, `.ocb-plan`) | `battle/hud/omen.js` rebuilds them |
| `StatusChip` (343), `StatusTip` (350) | classes (`.ocb-chip`, `.ocb-tip`, `.ocb-tip-head`) | `battle/hud/status.js` |
| `StatusPanel` (361) | no (the hud draws `.ocbg-battle-stat`) | — |
| `StanceSlot` (411) | yes (`OCB.StanceSlot`, `hud/stances.js:180`) | |
| `StanceTip` (421) | classes | |
| `StaminaPlate` (432) | classes (`.ocb-stamina`, `.ocb-st-row1`, `.ocb-st-pay`, `.ocb-pips`, `.ocb-pip`, `.ocb-pip-div`) | `hud/stamina.js` |
| `Pile` (456) | classes (`.ocb-pile*`) | `hud/piles.js` |
| `FloorTile` (469) | yes (`field/floor.js:95`) | |
| `DistanceChip` (487) | classes (`.ocb-dist`, `.ocb-dist-chip`) | `field/floor.js` |
| `TargetFrame` (507), `PredTag` (520) | yes (`hand-marks.js:148`, `:112`; explore selection and pick frames) | |
| `HoverLine` (528), `RefusalLine` (537) | yes (`hand-panels.js:288`, `:258`) | |
| `ReachPanel` (531) | class `.ocb-reachpanel` | |
| `Banner` (538) | yes (`hud/chrome.js:249`) | |
| `FloatNum` (539) | class `.ocb-float` | `field/fx.js` |
| `CornerInfo` (548), `SpeedToggle` (554) | classes (`.ocb-corner`, `.ocb-panel`) | `hud/chrome.js` |
| `JournalButton` (559) | yes (`hud/chrome.js:161`); the settings button copies its classes | |
| `PlayedAttrs` (560), `GuardPreview` (571), `EndTurnButton` (572) | classes (`.ocb-attr`, `.ocb-count`, `.ocb-endturn`) | `hud/bottom.js` |
| `FIGURES` (583-639), `Figure` (641) | data yes; `Figure` itself only in meta title (`title.js:27`) | battle uses `field/art.js drawFigure` (4 offset rim copies) |
| `FootLight` (656) | yes (`field.js:216`, meta title, hall) | |
| `.ocb-drawer*` classes | yes (`hud/piles.js`) | |
| `.ocb-end`, `.ocb-end-paper` | yes (battle result card) | |
| `Sound`, `SOUND_DEFAULTS`, `TIMBRES` | yes | audio area, not the kit |
| `FEEL_DEFS` (850), `SCRIPT`/`Core` (956/1043), `mountBattle` (1323), `Lab` (2387) | no | DS prototype only |
| `PileDrawer` (2591), `PursuitBadge` (2646), `DRAGONS` (2664), `Gallery` (2695), `FoeBadges` (2825), `Stage` (2837), `HandFan` (2855), `FloorLane` (2874), `Notices` (2899), `TurnControls` (2909), `SoundPalette` (2923) | no | DS showcase/gallery only; do not port |
| `.ocb-game .ocb-sky/.ocb-meadow/.ocb-earth/.ocb-sun/.ocb-haze/.ocb-mote/.ocb-enemy-shade/.ocb-flow/.ocb-debug` (`bundle.css:218-239`) and `@keyframes ocb-breathe/ocb-drift` | no | DS prototype's backdrop; the game paints `BackdropSpec` |
| `.ocb-lab*`, `.ocb-gallery`, `.ocb-scaled` (`bundle.css:242-283`) | no | |

So the kit ports: the tokens, `icon`/`hatch`/`swatch`, the data tables, every `.ocb-root .ocb-*` rule as a named
style, and the component builders the game calls (`Card`, `CardDetail`, `StanceSlot`, `FloorTile`, `TargetFrame`,
`PredTag`, `HoverLine`, `ReachPanel`, `RefusalLine`, `Banner`, `FloatNum`, `JournalButton`, `Figure`, `FootLight`).
`OmenPlate`, `ReserveOmen`, `StatusChip`, `StatusTip`, `StatusPanel`, `StanceTip`, `StaminaPlate`, `Pile`,
`DistanceChip`, `CornerInfo`, `SpeedToggle`, `PlayedAttrs`, `GuardPreview`, `EndTurnButton` are ported too (small,
and the scene ports rebuild them from the same parts), but their exact DOM is the scene's.

### 2.1 Shared rules (`bundle.css:3-12`)

- `.ocb-root`: `box-sizing: border-box` on everything; font body; colour ink; `line-height: 1.375`; `p { margin: 0 }`.
- `.ocb-mono`: font mono, tabular nums. `.ocb-disp`: font display.
- `.ocb-icon`: inline-block, `flex: none`, `overflow: visible`, `vertical-align: middle`.
- `.ocb-panel`: bg `--panel`, `1px solid --rule`, radius 6. `.ocb-solid`: bg `--panel-solid`, `1px solid --steel`, radius 6.

### 2.2 Icon (`bundle.js:63-128`)

`icon(name, size = 24, color = 'currentColor', { fill, fillOpacity, dashed, width })` → `<svg viewBox="0 0 24 24"
width=size height=size>` with one `<path>`, `stroke-linecap: round`, `stroke-linejoin: round`.

- `FILLED = { heart, pip }` (110): fill = colour, no stroke (opts ignored).
- Otherwise: `fill = opts.fill || none` (+ `fill-opacity` when given), `stroke = colour`, `stroke-width = opts.width || 2.2`
  (in viewBox units, so 2.2 × size/24 px), `opts.dashed` → `stroke-dasharray: 2.4 2.4` (viewBox units; every dash gets
  round caps, so a dashed icon reads as nearly solid beads with 0.2-unit gaps: replicate, do not "fix").
- Unknown name → `plus`.
- Explore uses its own `ico()` (`$G\src\scenes\explore\icons.js:47-71`) with the same rules plus `opts.outline` (draw
  heart/pip stroked) and fallback `pouch`.

DS ICONS (`bundle.js:64-101`, 24×24, verbatim; label in `ICON_LABELS` 102-109):

```
sword      M20 4l-1.5 5.5L9 19l-4-4 9.5-9.5zM6.5 13.5l4 4M3 21l3.5-3.5
shield     M12 3l7 3v5c0 5-3 8.5-7 10-4-1.5-7-5-7-10V6z
heart      M12 21s-8-5-8-11a4.5 4.5 0 018-2.8A4.5 4.5 0 0120 10c0 6-8 11-8 11z            (filled)
heal       M12 6v12M6 12h12
forward    M4 12h14M13 6l6 6-6 6
back       M20 12H6M11 6l-6 6 6 6
range      M3 12h18M3 7v10M21 7v10
infinity   M18.2 8c5.1 0 5.1 8 0 8-5.1 0-7.1-8-12.7-8-4.6 0-4.6 8 0 8 5.6 0 7.6-8 12.7-8z
pip        M12 6a6 6 0 110 12a6 6 0 010-12z                                              (filled)
draw       M8 3h9v14H8zM5 7v14h9
discard    M12 4v10M7 9l5 5 5-5M4 20h16
exile      M6 6l12 12M18 6L6 18
flag       M6 21V3M6 4h12l-3 4.5L18 13H6
star       M12 3l2 7 7 2-7 2-2 7-2-7-7-2 7-2z
hourglass  M7 3h10M7 21h10M8 3c0 5 8 6 8 9s-8 4-8 9M16 3c0 5-8 6-8 9s8 4 8 9
stairs     M3 20h5v-5h5v-5h5V5h3
miasma     M3 9c3-3 6 3 9 0s6 3 9 0M3 15c3-3 6 3 9 0s6 3 9 0
journal    M4 5c3-1 6-1 8 1v14c-2-2-5-2-8-1zM20 5c-3-1-6-1-8 1v14c2-2 5-2 8-1z
person     M12 4a3.5 3.5 0 110 7a3.5 3.5 0 010-7zM5 21c0-4 3-7 7-7s7 3 7 7
target     M4 20L20 4M4 12l8-8M12 20l8-8
seal       M9 3h6v6H9zM9 9l-3 4h12l-3-4M4 13h16v4H4zM6 21h12
bleed      M12 3c4 5 6 8 6 11a6 6 0 01-12 0c0-3 2-6 6-11z
brittle    M12 3l-2 6 4 3-3 4 2 5M5 5l4 4M19 6l-4 3
slow       M6 9l6 5 6-5M6 4l6 5 6-5M4 20h16
intimidate M2 12s4-6 10-6 10 6 10 6-4 6-10 6S2 12 2 12zM12 9v6
fatigue    M3 8h15v8H3zM18 11h3v2M6 11h3v2H6z
empower    M6 13l6-6 6 6M6 19l6-6 6 6
focus      M12 4a8 8 0 110 16a8 8 0 010-16zM12 10a2 2 0 110 4a2 2 0 010-4z
insight    M3 12h9M8 7l5 5-5 5M17 4v16
agile      M4 8h9a3 3 0 10-3-3M4 16h12a3 3 0 11-3 3M4 12h6
regen      M12 21V11M12 11c0-4 3-7 8-7 0 5-3 7-8 7zM12 15c0-3-2-5-6-5 0 3 2 5 6 5z
plus       M5 12h14M12 5v14
rest       M5 12h14
speed1     M9 6l6 6-6 6
speed2     M6 6l6 6-6 6M12 6l6 6-6 6
speed3     M4 6l5 6-5 6M10 6l5 6-5 6M16 6l5 6-5 6
```

Explore icons (`$G\src\scenes\explore\icons.js:8-41`; the ones already in the DS set are identical strings:
`Battle`=sword, `cards`=draw, `journal`, `hourglass`, `stairs`, `miasma`, `heart`, `pip`, `person`, `seal`):

```
Elite           M4 4l11 11M13.2 17.2l4-4M16.5 16.5L20 20M20 4L9 15M6.8 13.2l4 4M7.5 16.5L4 20
Boss            M12 21c-3.9 0-7-3.1-7-7V9.5L3 4l5 3h8l5-3-2 5.5V14c0 3.9-3.1 7-7 7zM9 12.5h.01M15 12.5h.01M10 17h4
Rest            M12 3c2.6 3 4 5.2 4 7.6a4 4 0 01-8 0C8 8.2 9.4 6 12 3zM4 21l16-4M20 21L4 17
Survey          M10.5 4a6.5 6.5 0 110 13a6.5 6.5 0 010-13zM15.3 15.3L20.5 20.5M7.5 10.5h6
Event           M12 3l9 9-9 9-9-9zM10 9.6a2 2 0 113 1.7c-.7.5-1 1-1 1.9M12 16.2h.01
Trace           M6 21V11a6 6 0 0112 0v10M3.5 21h17M12 10c1.3 1.5 2 2.6 2 3.8a2 2 0 01-4 0c0-1.2.7-2.3 2-3.8z
Carving         M6 3h12v18H6zM9 8c2-1.6 4 1.6 6 0M9 12.5c2-1.6 4 1.6 6 0M9 17h6
gear            M12 8.6a3.4 3.4 0 110 6.8a3.4 3.4 0 010-6.8zM12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1
tool            M14.5 4a5 5 0 00-4.6 6.9L4 16.8 7.2 20l5.9-5.9A5 5 0 0019.9 9.5l-3 3-3.4-.9-.9-3.4 3-3z
page            M6 3h9l4 4v14H6zM15 3v4h4M9 12h7M9 16h5
caution         M12 4L21 20H3zM12 10v4.5M12 17.4h.01
joka_no_ko      M7 21h10M8.5 21c0-2.2 1.4-3.5 3.5-3.5s3.5 1.3 3.5 3.5M12 17.5V15M12 12c-2-1.6 2-3 0-4.6S12 4.6 12 3
yakuso          M12 21V10M12 10c0-4 3-6.5 7-6.5 0 4-3 6.5-7 6.5zM12 15c0-3-2.2-5-6-5 0 3 2.2 5 6 5z
koi_hoshiniku   M15 3.5a5.5 5.5 0 015.5 5.5c0 3.8-3.6 6.2-7.6 6.2L9.8 18.3 7.5 18 7.2 15.7l3-3C10.2 8 11.6 3.5 15 3.5zM5.6 17.2a1.6 1.6 0 11-2 2
wasuremizu      M9.5 3h5M10.5 3v5.5L5.4 17.4A2.4 2.4 0 007.5 21h9a2.4 2.4 0 002.1-3.6L13.5 8.5V3M7.6 15h8.8
shirube_no_ishi M5 15.5L8 7l7-2.5 5 6.5-2 8.5H8zM9.5 13l3-2 3 2
pouch           M8 7h8l3 4v6a4 4 0 01-4 4H9a4 4 0 01-4-4v-6zM9 7L8 4h8l-1 3
```

Other scene-owned icon paths that use the same style (put them in the same data file so the parser tests cover them):

```
stay   (battle/field/freestep.js:46)  M12 3.5a2.6 2.6 0 110 5.2a2.6 2.6 0 010-5.2zM12 8.7v6.6M8.6 11.6l3.4-1.6 3.4 1.6M9.4 19.6l2.6-4.3 2.6 4.3M3.5 21h17
gear   (battle/hud/chrome.js:198)     same string as explore gear
cut mark (battle/hand-marks.js:126-141): <circle cx 12 cy 12 r 8 fill none stroke amber 2 dasharray "3 3"> + path M4 20L20 4 ink 2.4 round
shield (battle/field/art.js:238, 64×72 box)  M3 2 H61 V31 C61 50 46 63 32 70 C18 63 3 50 3 31 Z
shield crack (battle/field/fx.js:55)  M33 3 L29 17 L36 29 L30 43 L35 56 L32 69
```

Parser notes from these strings: implicit repeats (`L90 80 86 112 96 150`), packed arc flags (`a3.5 3.5 0 110 7`
= rx 3.5, ry 3.5, rot 0, large 1, sweep 1, x 0, y 7), numbers without separators (`-1.5 5.5L9`, `.7.5` = .7 then .5,
`1.4-1.1`), `h.01` dots (a 0.01 segment with round caps must render as a full dot of the stroke width), relative and
absolute S/s and Q (figures), uppercase and lowercase A.

### 2.3 Hatch and swatch (`bundle.js:130-148`)

`hatch(w, h, angle = 45, gap = 11, color = --reach-red, width = 3)` = an SVG `<pattern width=gap height=gap
patternUnits=userSpaceOnUse patternTransform="rotate(angle)">` holding `<rect x=0 y=0 width=width height=gap>`, filling
a `w×h` rect. Semantics: a point q (svg px, origin = the svg's top-left) is painted when
`frac((q.x·cosθ + q.y·sinθ) / gap) · gap < width` (θ = angle, rotation clockwise on screen). Phase 0 is at the svg's
top-left. Several hatches on one tile overlay (cross-hatch: one svg per threat).

`swatch(angle, size = 22)` = `<span>` size×size, `border: 2px solid --reach-red`, `border-radius: 3px`, `overflow: hidden`,
holding `hatch(size, size, angle, gap 6, reach-red, width 2)` (display block, so it starts at the padding box, inside
the border, and is clipped by the padding box = (size−4)² with inner radius 1). Sizes used: 22 (omen), 16 (status
panel, stance slot).

### 2.4 Card (`bundle.js:192-241`, `bundle.css:14-52`)

216×304, radius 6, bg `--card`, `box-shadow: --shadow-card`. Attribute colours (`--attr` frame, `--attr-ink` value ink):
attack violet/phosphor, defense guard/guard, skill amber/amber, stance crimson/hp-text, none steel/ink.

Child order (paint order): `.ocb-card-clip` (::before left bar, ::after top bar) → cost → attr icon → reach tag →
name → values → desc → trait box → `.ocb-card-dim` → `.ocb-card-edge`.

| part | box | look |
| --- | --- | --- |
| clip::before | left 0, top 0, bottom 0, width 8 | `--attr`, clipped by the card's 6 px corners (shape = rect with TL/BL radius 6) |
| clip::after | left 0, top 0, right 0, height 5 | `--attr`, clipped by the TL/TR 6 px corners (height 5 < radius 6: the strip's ends follow the arcs) |
| edge | inset 0 | `2px solid --attr`, radius 6; `.is-lit` → `--wick` |
| lit card | | `box-shadow: --shadow-card, --shadow-lamp` (spread-2 wick ring + 20 px/5 px glow) |
| cost | (16, 11) 44×44 circle | `2px solid --amber`, bg `rgba(0,0,0,.45)`, mono 700 28/1 amber, centred; `.is-down`: bg amber, text `--ground` |
| cost ▼n | DS: centre-bottom, `bottom −9`; game: `left 42, top 42, translate(−50%,−50%)` (`hand.css:18`) | height 18, padding 0 4, radius 3, bg panel-solid, `1px solid amber`, body 400 14/16 ink, nowrap |
| attr icon | (68, 21), 24 px | colour `--attr-ink` (sword/shield/star/flag) |
| reach tag | right 12, top 11, height 32 | padding 0 8, flex centre gap 4, bg panel-solid, `1px solid --steel`, radius 4, mono 700 20/1 ink, nowrap; icon `range` 18 or `person` 22; `small` body 400 14/1 → **17 px in the game** (`common.css:23`) |
| name | left 16, top 60, right 12 | display 700 22/28, letter-spacing .02em, nowrap (game: overflow hidden) |
| values | left 16, top 94, flex gap 14 | each `.ocb-val`: grid `24px auto`, col-gap 4, centred; icon 24 (colour = value ink); `b` mono 700 36/40 in `--val-ink`; `small` (row 2, col 2) body 400 16/18 ink-muted, margin-top −2 |
| desc | left 16, top 156, 188×66 (stance: top 94, height 88) | body 400 16/22 ink, `line-break: strict`; stance rows `.ocb-inf`: flex gap 6 centre height 22 nowrap, icon `infinity` 18 hp-text |
| trait box | left 12, right 7, bottom 7, height 62 | bg `rgba(0,0,0,.34)`, `1px solid --rule`, radius 4, flex centre, padding-right 4; lamp slot 26 wide; `p` body 400 16/22 nowrap; second `p` 700 |
| plain trait | | `border-style: dashed; border-color: steel; color: ink-muted; padding-left 12`; text 「素直な札」 / 「特性なし（+2 込み）」 (400) |
| lamp | 10×10 circle steel; `.is-on` 16×16 wick + `box-shadow 0 0 10px rgba(255,216,154,.8)` | |
| dim | inset 0, radius 6, black, opacity 0; `.is-unpayable` → .6 | |
| term | `text-decoration: underline dotted --ink-muted 2px; text-underline-offset: 4px` | |

Long description fit (`fitCardText`, 257-284), modes from `st.fit`: `'unity'` nowrap overflow visible; `'a'` one line,
shrink 16→13 px until it fits 188, then ellipsis; `'b'` (explore uses it) shrink 16→13 until the wrapped height
fits 66 (88 for a stance), line height `round(size × 1.375)`, then clamp to `floor(H / lh)` lines; `'c'` split at the
first 「、」, each part shrunk on its own, ellipsis. Measurement: hidden twin at body font, `line-break: strict`,
`overflow-wrap: anywhere`. Meta's card (`$G\src\scenes\meta\cardFace.js:52-56`) instead sets `white-space: pre-line`
and, when rows > 3 or a row > 11 chars, `.is-small` = 14/19 (`meta.css:390-394`).

The battle hand (`battle/hand-card.js`) composes the same parts itself and fits name and description with the ported
`CardTextFit` (min font 14). So the C# `Ocb.Card` must expose its parts as separate builders (`CostPart`, `ReachPart`,
`ValuesPart`, `DescPart`, `TraitPart`, `Dim`, `Edge`) and a `TextMeasure` delegate (§4.9).

States/animations of the card itself: none in CSS (`.ocb-card-dim` has no transition in the DS; the game's hand
tweens dim/lit on the scene clock).

### 2.5 CardDetail (`bundle.js:287-298`, `bundle.css:182-190`)

560×280 (game: `height: auto; min-height 280; max-height 600`, `hand.css:43`), padding 16, flex gap 16, bg panel-solid,
`2px solid --steel`, radius 6, `--shadow-tip`. Card holder 173×243 with the card at `scale(.8)`, origin 0 0. Body:
column gap 6; `h3` display 700 24/32; `.ocb-meta` body 400 16/22 ink-muted (「攻撃 ・ 敵 1 体 ・ 間合い 1〜2」 /
「… ・ 自分」); `.ocb-full` body 400 16/22; `.ocb-detail-terms` margin-top auto, padding 8 10, bg `rgba(0,0,0,.35)`,
radius 4, body 400 16/22, `b` margin-right 6.

### 2.6 OmenPlate and ReserveOmen (`bundle.js:300-338`, `bundle.css:54-68`)

OmenPlate 200×98, bg panel-solid, `2px solid --violet`, radius 6, padding 8 12, column, space-between,
`transition: box-shadow .12s` (switched off in the battle, `battle.css:49-52`). `.is-active`: border phosphor +
`--shadow-omen-active` (0 0 30px violet .6). `.is-rest`: `2px dashed --steel`.

- Row 1: flex centre gap 8, height 52: kind icon 34 (colour by kind: 攻撃 sword phosphor, 防御 shield guard, 移動 back
  ink, 構え flag ink, 技 star ink, 状態 seal ink, 休み rest steel), value `b` mono 700 48/52 in the kind colour, status
  icons 28 ink, swatch 22 at `margin-left: auto` (not for 休み).
- Row 2: flex centre space-between, height 26, body 400 16/22: kind word (状態 → 「技」) + hit mark: 30×26, radius 4;
  hit = `2px solid phosphor` + person 18 phosphor; miss = `2px dashed --miss` + person 18 miss + ::after a 2 px miss
  line, left 2, right 2, top 50%, `rotate(-24deg)`.

ReserveOmen 170×66, opacity .62, `2px dashed --ink-muted`, bg panel-solid, radius 6, padding 6 10, flex centre gap 8:
icon 24, `b` mono 700 28/1, kind (16 px), `.ocb-plan` 「予定」 abs right 6 bottom 6, body 400 14/16, `1px dashed ink-muted`,
radius 3, padding 0 4. The game's plan (`hud.css:81-89`) differs: 170×66, padding 4 10 5, ::before dashed frame at
`--plan-alpha` (.62), rows 32/20 px; port the game's.

### 2.7 StatusChip, StatusTip, StatusPanel (`bundle.js:340-406`, `bundle.css:85-114`)

StatusChip: height 30, padding 0 8 0 6, inline-flex centre gap 4, bg `rgba(0,0,0,.35)`, `1px solid --ink-muted`,
radius 3 (foe-applied) / self-applied: border steel, radius 15; boss: `3px double --boss`; hover (`:hover` or
`.is-hover`): border ink, bg `rgba(255,255,255,.1)`; body 400 16/1, nowrap; icon 18 ink, name, count in mono 600.

STATUS table (168-179; `self` decides the chip shape; `how`/`decay` are tooltip texts):
bleed 出血 foe · brittle 脆化 foe · slow 鈍足 foe · intimidate 威圧 foe · fatigue 疲労 foe · empower 強化 self ·
focus 集中 self · insight 見切り self · agile 俊敏 self · regen 再生 self (texts verbatim in the source; copy them).

StatusTip: width 340, padding 10 12 12, bg panel-solid, `1px solid --steel`, radius 6, `--shadow-tip`. Head: flex
centre gap 6, body 700 18/24, margin-bottom 6: icon 20, 「name n」, `small` margin-left auto, body 400 14/18 ink-muted
(「自分に付ける系」/「相手に付ける系」). Rows `p` body 400 16/22, label `span` ink-muted margin-right 8 (「効き方」「減り方」).
The battle overrides (`hud.css:50-54`): all ink, label 700 with margin-right 10, rows 4 px apart.

StatusPanel (not used by the game, listed for completeness): `.ocb-panel .ocb-stat`, width 300 self / 210 foe,
padding 8 10 10, column gap 8, `.is-elite` border phosphor. HP row h28 gap 6: [swatch 16] heart 20 crimson, number
mono 700 24/28 min-width 40 right-aligned, bar `flex:1` h14 radius 3 bg `rgba(0,0,0,.5)` `1px solid rule` overflow
hidden: fill crimson (`transition: width .3s ease-out`), loss = `repeating-linear-gradient(135deg, wick 0 2px,
transparent 2px 5px)` between hp−loss and hp, ticks every 10 HP: 2 px `rgba(0,0,0,.55)` at `calc(t% − 1px)`.
Guard row h30 gap 6: shield 28 (guard > 0: guard stroke + guard fill .2; 0: steel dashed) + mono 700 24/28 guard
(0: row opacity .7, number ink-muted). Foe extra at margin-left auto: pip 16 ink-muted, mono 700 24/28 ink, shield 24
(reserve: filled .2 / dashed guard). Chips: flex wrap gap 5; foe max 4, self max 6 (overflow → 「+n」 chip).

### 2.8 StanceSlot, StanceTip (`bundle.js:408-427`, `bundle.css:116-121`)

250×52, padding 0 12, flex centre gap 8, bg `--panel`, `2px solid --hp-text`, radius 6, display 700 20/26.
`.is-foe`: border violet; flag icon colour phosphor (foe) / hp-text (self). `.is-empty`: no bg, `2px dashed steel`,
only a flag 24 steel. Optional swatch 16. Name span (game: nowrap, ellipsis); `.ocb-x` 「×n」 margin-left auto, width
40, right-aligned, mono 600 16/1; `more` → 「+n」 in place of the name. `.is-hover`: bg `rgba(40,44,58,.92)`.
StanceTip: the tip with flag 20 hp-text, 「name ×n」, small 「永続」, rows 効き方 (how + 「（重ねた n つぶん効く）」) and
減り方 「減らない（永続）」.

### 2.9 StaminaPlate and Pile (`bundle.js:429-464`, `bundle.css:123-142`)

StaminaPlate 236×84, padding 10 12, bg `--card`, `1px solid --steel`, radius 5. Row 1 flex centre gap 8 height 36:
pip icon 18 amber, number mono 700 34/36 amber, pay 「▸ n」 margin-left auto, mono 700 24/1 wick, opacity 0 → 1 when
shown. Pips: flex centre gap 4 height 20 margin-top 8; each 16×16 circle: `is-have` amber fill, `is-pay` `3px solid
amber` ring, `is-next` `2px dashed amber` ring, `is-empty` `2px solid steel` ring; a divider after index 2: 2×20
`rgba(236,235,242,.7)`. `.is-lit` → `@keyframes ocb-pip-lit .32s ease-out` (scale 1.35→1, ring shadow 0 0 0 0
wick .9 → 0 0 0 8px wick 0). The game's stamina (`hud/stamina.js:92-102`) runs the same shape on its clock.

Pile 64×84, pointer. Sheets: `min(3, ceil(count/3))` layers, drawn from the farthest (i = layers … 1) at `top: −4i`,
opacity `1 − .18 i`, each 64×84 radius 5 bg card `1px solid steel`. Face inset 0, flex centre, radius 5, bg card,
`1px solid steel`, icon 28 (draw/discard/exile) ink, or steel when empty; empty face: no bg, dashed border. Hover:
face border ink. Names 山札 / 捨札 / 除外.

### 2.10 FloorTile and DistanceChip (`bundle.js:466-502`, `bundle.css:70-79`)

FloorTile: height 40, width w, radius 6, bg `--floor-tile` (game: `BackdropSpec` tile colour, `floor.js:99`),
`1px solid --rule`. Children in order:

1. `.ocb-tile-reach` inset 0, radius 6, `linear-gradient(rgba(232,168,76,.55), rgba(232,168,76,.18))` (top→bottom),
   `--shadow-reach`, opacity 0 (turned on by the caller).
2. Threat host (inset 0): `.ocb-tile-move` (inset −1, radius 6, `2px dashed --violet`) and/or `.ocb-tile-threat`
   (inset −1, radius 6, `2px solid --reach-red`, `--shadow-threat`, overflow hidden) holding one hatch per threat:
   `hatch(w, 40, angle, boundary ? 6 : 11)`, i.e. bars 3 px, gap 11 (6 on a boundary cell). Effective hatch window
   = the threat box's padding box = the tile inset by 1 px, inner radius 4; pattern origin at tile + (1, 1).
   Game override for a move over a threat: `inset 4px` + `box-shadow: 0 0 0 2px panel-solid, inset 0 0 0 2px panel-solid` (`field.css:24`).
3. `.ocb-tile-reach-line` top bar 3 px, radius `3px 3px 0 0`, amber, opacity 0 (game: left/right 4, `field.css:21`).

DistanceChip(n, span): width `max(100, span)`, height 34, flex centre. With `span > 0`, bracket lines in
`rgba(236,235,242,.55)`: horizontals at top 17 from left 8 to 50% and from 50% to right 8 (1 px), end ticks at left 8 /
right 8, top 9, 1×17. Chip: z 1, height 34, padding 0 10, flex centre gap 6, bg panel-solid, `1px solid steel`,
radius 6, mono 700 28/1; icon `range` 22 ink. The game draws its own arms (`field.css:26-33`).

### 2.11 TargetFrame, PredTag, throw line (`bundle.js:504-523`, `bundle.css:144-148`)

TargetFrame(w, h, { hook = 26, width = 3, reachable, glow = 10, glowAlpha = .35 }): an SVG `w×h`, overflow visible,
path of four corner hooks:
`M0 L V0 H L  M(w−L) 0 H w V L  M w (h−L) V h H (w−L)  M L h H0 V(h−L)` (L = hook), `stroke-linecap: square`, no fill.
Two strokes: keyline (`--keyline`, width sw + 4, below; invisible in dusk) and the colour stroke (`--wick`, sw; when
`reachable === false`: `--steel`, width 2). Reachable frames get `filter: drop-shadow(0 0 glow px rgba(232,168,76, glowAlpha))`.
Callers: battle hooks (`hand-marks.js:148`, with the hand's HOOK/HOOK_W/GLOW/GLOW_ALPHA constants), explore node
selection (`ExploreScene.js:259`, hook 20, width 3, glow 12) and pick frames (236×324, hook 24, width 3).

PredTag(icon, n, iconColor): `.ocb-pred` abs, height 60, padding 0 14, flex centre gap 8, bg panel-solid,
`2px solid --wick`, radius 6, mono 700 48/1 wick, nowrap, `translate(−50%, −50%)` (centred on its point); icon 32
(default phosphor).

Throw line `.ocb-throwline`: height 3, `repeating-linear-gradient(90deg, amber 0 9px, transparent 9px 15px)` (9 on,
6 off; measured), `filter: drop-shadow(0 0 0 --keyline)`. The game's `.ocbg-battle-throw` adds `box-shadow: 0 0 0 2px --keyline`.

### 2.12 Notices (`bundle.js:525-543`, `bundle.css:150-166`)

| piece | look |
| --- | --- |
| HoverLine | width 420, min-height 48, padding 10 14, flex centre gap 10, bg panel-solid, `1px solid steel`, radius 6, body 400 18/26; lamp first (10 steel or lit 16 wick + glow) |
| ReachPanel | 280×76, padding 12 16, flex centre gap 12, bg panel-solid, `1px dashed --miss`, radius 6, body 400 18/26; icon `range` 22 ink; the text splits after 「間合いが」 into two lines |
| RefusalLine | height 48, padding 0 20 0 16, inline-flex centre gap 12, bg panel-solid, `1px solid steel`, radius 6, body 400 20/28, nowrap; dot 16×16 amber circle |
| Banner | abs left 0 right 0, height 132, flex centre; bg `linear-gradient(90deg, rgba(10,12,18,0), rgba(10,12,18,.8) 28%, rgba(10,12,18,.8) 72%, rgba(10,12,18,0))`; display 800 64/72, letter-spacing .12em; ::before/::after: left 22%, right 22%, height 2, amber (`.is-foe`: violet), opacity .7, at top 10 / bottom 10; pointer-events none |
| FloatNum | abs, flex centre gap 6, padding 2 10, radius 6, bg `rgba(0,0,0,.8)`, nowrap, `translate(−50%,−50%)`; `b` mono 700 48/52 hp-text; guard: 34/40 guard; miss: body 400 24/32 ink with `line-through 2px --miss` (game: 3px, `field.css:108`); `small` body 400 16/20 ink; icon: guard shield 30 (fill .2), heal 34 hp-text, damage heart 36 hp-text, miss none |
| guide (`.ocb-game .ocb-guide`) | top 743, height 28, flex centre gap 10, body 400 22/28 ink, `text-shadow: 0 1px 3px rgba(0,0,0,.7)`, `transition: opacity .2s`; the battle's own guide (`hand.css:49`) uses `text-shadow: 0 1px 3px --ground, 0 0 8px --ground` |

### 2.13 HUD (`bundle.js:545-573`, `bundle.css:168-181`)

| piece | look |
| --- | --- |
| CornerInfo `.ocb-panel.ocb-corner` | height 44, padding 0 16, inline-flex centre gap 18, mono 600 20/1, nowrap; spans gap 6: hourglass 22 ink + turn, stairs 22 ink + floor, miasma 22 phosphor + 「n%」 |
| SpeedToggle `.ocb-panel.ocb-speed` | 120×56, padding 0 12, flex centre gap 8, mono 600 20/1, pointer; icon speed1/2/3 24 ink + 「×1.0 / ×1.25 / ×1.5」; hover: border ink, bg `rgba(40,44,58,.92)` |
| JournalButton `.ocb-panel.ocb-btn-sq` | 56×56, centred journal icon 30 ink; hover as above |
| PlayedAttrs | flex gap 6; each `.ocb-panel.ocb-attr` 40×40 centred, opacity .55 (on: 1), icon 24 (attr ink or steel); count badge abs right −8 top −8, 20×20 circle, bg panel-solid, `1px solid ink-muted`, mono 400 14/18 centred (game: 700 ink, `hud.css:129`) |
| GuardPreview | height 56, padding 0 14, inline-flex centre gap 8, `1px dashed --guard`, radius 6, bg `rgba(16,20,30,.6)`, mono 700 24/1 guard; shield 30 (fill .2) + 「+n」 |
| EndTurnButton | 224×72, flex centre, `2px solid amber`, radius 6, bg `rgba(232,168,76,.16)`, body 700 24/28, letter-spacing .08em, ink; hover bg .28; `.is-off` opacity .4 |

### 2.14 Drawer, end paper (`bundle.css:192-206`)

Drawer (classes used by `hud/piles.js`): `.ocb-drawer` abs column; tabs flex gap 4; tab body 700 18/1 ink-muted,
padding 10 14, `border-bottom: 2px solid transparent`; `.is-on` ink + amber underline. Row: flex centre gap 10,
height 40, padding 0 12, `border-bottom: 1px solid rule`, body 400 18/1; `.ocb-mono` margin-left auto, 600; `i` bar
6×24, radius 2, bg `--attr`. (The battle's drawer frame, tabs and close button are in `hud.css:105-124`.)

End: `.ocb-end` abs inset 0, flex centre, bg `rgba(8,10,14,.62)`. Paper: width 720, padding 48 56 40, bg washi,
colour washi-ink, radius 4, `box-shadow: 0 18px 40px rgba(0,0,0,.5)`, centred text; `h2` display 800 64/72,
letter-spacing .12em, margin-bottom 8; `p` body 400 20/32; `button` margin-top 28, body 700 22/1, letter-spacing .08em,
washi-ink, no bg, `2px solid washi-ink`, radius 6, padding 16 36.

### 2.15 Figure and FootLight (`bundle.js:575-659`)

`poly(x1,y1,x2,y2,w)` = a bar of width w from (x1,y1) to (x2,y2): with `n = (−dy, dx)/len · w/2`, the closed quad
`(p1+n) (p2+n) (p2−n) (p1−n)`. FIGURES (viewBox w×h, feet on the bottom edge, `body` = width the target hooks frame,
`cx` = feet centre x, `side`):

```
swordsman  w 140 h 226 body 100 cx 62  side self
  M62 6c8 0 14 6 14 14s-6 14-14 14-14-6-14-14 6-14 14-14z
  M50 12C42 16 39 26 43 34L50 26Z
  M56 32h12v10H56z
  M40 46C46 40 56 38 62 38S80 40 86 46L90 80 86 112 96 150H28L38 112 34 80Z
  M80 48L94 70 100 94 92 98 86 74 74 56Z
  M44 50L40 76 60 94 94 90 94 99 56 104 32 80 36 52Z
  poly(100, 92, 136, 22, 5)
  poly(91, 87, 109, 97, 4)
  poly(93, 106, 100, 92, 4)
  M66 148H80L92 188 104 222 90 224 78 192Z
  M34 148H50L46 186 36 222H22L32 186Z
  M88 220H110L112 226H86ZM18 220H40V226H16Z
spearman   w 170 h 226 body 110 cx 104 side foe
  M120 8C110 0 96 2 90 10L70 16Q63 21 68 27L92 31 96 39 118 41C127 32 129 16 120 8Z
  M110 9L132 1 121 15Z
  M100 36L118 38 123 55 96 57Z
  M88 55C96 48 124 48 132 56L137 92 128 128H92L86 92Z
  M126 116C148 128 161 160 167 204L160 207C150 172 138 148 120 130Z
  M92 60L74 86 61 104 68 110 82 93 99 70Z
  M128 62L121 92 104 99 106 106 126 99 137 70Z
  poly(20, 164, 152, 30, 5)
  M30 148L4 180 38 158Z
  M92 124H108L100 160 108 196 96 222 72 226 71 219 89 214 92 197 84 160Z
  M112 124H128L124 162 132 198 124 222 102 226 101 219 117 214 116 198 108 160Z
hound      w 230 h 218 body 180 cx 110 side foe
  M40 70C46 54 64 48 78 54L94 66 88 82 60 86 34 84Z
  M30 70L6 64 10 76 30 82Z
  M72 52L84 36 88 58Z
  M80 70C110 60 160 62 190 76L204 104 186 132 120 134 86 120 76 96Z
  M196 84C214 92 226 118 228 150L220 152C216 126 206 106 190 98Z
  M88 116L76 160 70 216 84 218 92 162 104 124Z
  M112 122L114 170 104 216 118 218 128 170 128 124Z
  M160 124L170 168 160 216 174 218 186 166 178 122Z
  M182 118L196 164 192 216 206 218 210 162 196 114Z
serpent    w 220 h 234 body 160 cx 96 side foe
  M58 8C72 0 92 4 98 18L102 34 84 42 62 38 42 30C40 20 46 12 58 8Z
  M44 22L18 26 22 34 44 32Z
  M80 36C96 40 104 60 100 84 96 110 82 132 86 156L60 160C54 132 70 108 74 84 78 64 72 48 68 40Z
  M60 150C40 166 34 196 56 214 84 234 150 234 186 218 214 204 216 178 196 166 172 152 130 160 110 176L120 196C138 186 164 180 176 188 188 196 180 208 160 212 128 220 92 218 72 206 58 196 62 176 78 166Z
  M108 178C124 196 156 200 184 194L190 206C160 216 118 212 98 192Z
warden     w 220 h 264 body 180 cx 120 side foe
  M136 10C124 0 106 4 100 14L80 22Q72 28 78 34L104 38 108 48 132 50C142 38 144 20 136 10Z
  M126 10L150 0 138 18Z
  M108 46H138L144 66 102 68Z
  M86 66C100 56 150 56 166 68L176 118 164 168H92L80 118Z
  M160 160C186 174 204 210 212 252L202 256C192 218 176 192 152 178Z
  M30 70H86L92 92 88 190 58 206 28 190 24 92Z
  M96 160H120L114 214 122 258 92 264 88 256 102 250 98 214Z
  M136 160H160L160 214 170 256 140 264 136 256 150 250 140 214Z
```

`Figure(kind, { fill, rim, scaleTo })`: svg w×h (with `scaleTo`: height scaleTo, width `w·scaleTo/h`), overflow
visible; a rim group (`fill = stroke = rim`, stroke-width 4, round joins) under a fill group. Default fill
`--figure-self` / `--figure-enemy`; rim `rgba(232,168,76,.28)` self / `rgba(139,111,208,.35)` foe. **The game's battle
replaces the rim stroke** with four copies of the fill shape offset by (±2, ±2) screen px in the rim colour, alphas
compounding where they overlap (`field/art.js:162-235`, `drawFigure`; roster variants and the boss serpent/root are
also there, `art.js:76-140`). The kit's `PathGraphic` must allow several groups with per-group translate, fill, stroke
and a radial-gradient fill (the stance body light, `field.js:110`
`{ color crimson, at .6, r .46, stops [[0,1],[.35,.7],[.7,.22],[1,0]] }`).

FootLight(w, foe): abs, height 60, width w, `border-radius: 50%` (an ellipse), bg
`radial-gradient(closest-side, foe ? rgba(139,111,208,.35) : rgba(232,168,76,.55), rgba(0,0,0,0))`.

### 2.16 common.css and stage.js (owned by this area)

`$G\src\scenes\common\common.css`:

| rule | look |
| --- | --- |
| `.ocbg-viewport` (3) | fixed, inset = safe-area insets, bg ground, overflow hidden |
| `.ocbg-stage` (4) | 1920×1080, origin 0 0, overflow hidden, body font, ink |
| `.ocbg-scene` (5) | abs inset 0, overflow hidden |
| `.ocbg-btn` (6-12) | body 700 22/1, letter-spacing .06em, ink, bg panel-solid, `1px solid steel`, radius 6, padding 16 28; hover: border ink, bg `rgba(40,44,58,.92)`; `:focus-visible`: outline 2px wick, offset 2; `.is-primary`: `2px solid amber`, bg amber .16, hover .28; `.is-danger`: border hp-text, bg `rgba(194,72,63,.18)`; `[disabled]` opacity .4 |
| `.ocbg-confirm` (13-17) | abs inset 0, z 900, flex centre, scrim `rgba(5,6,10,.62)`; card (`.ocb-solid`) width 760, padding 32 36 28, `--shadow-tip`; `h3` display 700 30/40, margin-bottom 12; `p` body 400 20/32; row flex end gap 16 margin-top 28 |
| `.ocbg-notice` (18-19) | abs z 880, padding 12 20, body 400 20/30 ink, bg panel-solid, `1px solid rule` + `border-left: 3px solid caution`, radius 6, `--shadow-tip`, `white-space: pre-line`, `transition: opacity .6s`, `.is-leaving` opacity 0 |
| reduced motion (20) | every animation/transition 1 ms (no OS signal in Unity; the settings' 演出短縮 is the switch) |
| card `自分` tag (23) | `.ocbg-stage .ocb-card .ocb-reach small { font-size: 17px }` |

`stage.js` API (port 1:1 into `Trial/Common/Stage.cs`): `W = 1920`, `H = 1080`; `h(tag, props, ...kids)`;
`place(el, x, y, w, h)`; `createStage(container)` → `{ root, viewport, scale, offX, offY, fit(), toStage(e), layer(cls) }`
(scale = `min(w/1920, h/1080)`, centred); `EASE` = `out 1−(1−t)³`, `in t³`, `inout` (cubic in-out), `linear`,
`smooth t²(3−2t)`; `createClock(onFrame)` → `{ now, tween(obj, to, ms, ease, delay), wait(ms), stop() }` (a new tween
of the same obj drops the same keys from older tweens; `from` is read when the delay ends; frame dt clamped to 50 ms);
`pushKeys(handler)` key stack (top first, `true` swallows); `noticeLine(text, x, y, w, ms)` (fade starts at ms − 600);
`confirmCard(host, { title, body, ok = 「進む」, cancel = 「やめる」, danger })` → Promise<bool> with the Tab-focus rule
(a Tab-focused button takes Enter/Space; Esc cancels; repeats ignored).

---

## 3. CSS feature census

Counts are `grep -F` line hits over `$G\src\scenes` (CSS and JS) plus `$DS\bundle.css`; "JS" marks values written by
script (`el.style…`). Rough by nature; the point is which features need kit support.

### 3.1 Table

| feature | count | typical values / where | kit answer |
| --- | --- | --- | --- |
| `linear-gradient` | 49 | vertical 2–3 stop fades (`transparent → x`), 90° bands (banner `0/28/72/100%`), 135° sigil, 180° 5-stop sweep (fx.js:244), mask fades | `LinearGradient` paint, band-split meshing |
| `repeating-linear-gradient` | 3 | HP loss 135° wick 0 2 / transparent 2 5; throw line 90° amber 0 9 / 9 15; memo sheet ruled lines (36 px period, 1 px line at 35) | `Stripes` |
| `radial-gradient` | 21 | `closest-side` glows (2–5 stops), `ellipse 72% 66% at 50% 50%` vignette, `80% 70% at 50% 0%` plate tints, `60% 55% at 34% 88%` lull, `ellipse at x y` washi (farthest-corner) | `RadialGradient` paint, dense meshing |
| `conic-gradient` | 0 | | — |
| `box-shadow` | 68 (8 inset) | tokens; `0 0 Npx` glows 8–22 px; spread rings `0 0 0 2/3/8/14/22px`; `inset 3px 0 0` accent bar; `inset 0 0 90/110/60/40px` paper vignettes; `-18px 0 40px` drawer; `0 34px 80px` ending sheet; multiple layers | `BoxShadow` rings (§4.4) |
| `text-shadow` | 14 | ground halos `0 0 6px`, `0 0 8px`, `0 1px 2px`, `0 1px 3px`; glows `0 0 12/18/22/30/34px` colour 30–70 %; `0 2px 12/14px` | TMP underlay ≤ 4 px, `TextGlow` above (§4.9) |
| `border … dashed` | 54 | 1 px and 2 px; pips, plan, empty slots, far nodes | dashed ring (measured pattern §3.2) |
| `dotted` | 6 | journal rows `1px dotted`, term underline | dotted |
| `double` | 4 | `3px double --boss` (boss chip, boss word) | double ring = 1/1/1 |
| `border-radius` | 185 | 6 (panel), 5, 4, 3, 2, 50 %, 12, 15, 16, 22; `0 4px 4px 0`, `4px 4px 0 0`, `3px 3px 0 0`; elliptic 50 % on non-squares | per-corner elliptic radii |
| per-side borders | ~15 | `border-left: 3px` accents (notice, hall life, warn), `border-top: 3px crimson`, tab `border-bottom: 2/3px` | per-side widths/colours |
| `outline` | 20 | focus rings 2 px wick offset 2/3, 3 px washi-ink offset 3, 2 px amber offset −2; boss nodes 2 px boss offset 3/4 (dashed when far) | outline follows the radius (measured) |
| `opacity` | 214 | states .3–.95, transitions | CanvasGroup (§6 risk 2) |
| `transform` rotate/scale/translate | 20/36/78 | centring `translate(−50%,−50%)`, hover lifts −1…−10 px, `scale(1.04)`, `scaleX(−1)`, rotate −24° / 45° / −4°; JS `style.scale` / `style.translate` separate properties | `UiNode` transform channels |
| `skew` | 0 | | — |
| `filter` | 9 CSS + ~12 JS | `brightness(.82/.55/.75)`, animated brightness 1.5–2.4 → 1 (flashes), `grayscale(.85) brightness(.62)`, `grayscale(1)`, animated grayscale; `drop-shadow(0 0 6–12px colour)` on SVG strokes and fx | `ColorFilter`; `SoftOutline` |
| `blur()` | 0 | (the one grep hit is `textarea.blur()`) | — |
| `backdrop-filter` | 0 | | — |
| `mix-blend-mode` | 1 | `screen` on the stance body light (`field.css:55`) | screen material |
| `clip-path` | 3 (JS) | `inset(0 x% 0 0)` hatch wipe (`floor.js:299-301`), shield halves `inset(0 50% 0 0)` (`fx.js:737-739`) | geometry clip / RectMask2D |
| `mask-image` | 13 | vertical list fades: `180deg, ground 82/84/86%, transparent`, `0deg` same, both ends `transparent, ground 12/14/24%, ground 86/88%, transparent`; wakakusa-b sun (DS, unused) | `FadeMask` |
| `writing-mode` | 1 | `vertical-rl` title tagline 46 px display 400, letter-spacing .22em (`meta.css:48`) | `VerticalText` |
| `letter-spacing` | 89 | .02/.04/.06/.08/.1/.12/.14/.16/.2/.22/.24/.3/.32/.34em, 0, −.04em | TMP `characterSpacing = em × 100` |
| `line-height` | 24 + every `font:` shorthand | px values and ratios 1, 1.08–1.7 | `TextMath.LineSpacing` |
| `::before` / `::after` | 16 / 17 | banner lines, ripple ring, hit-mark slash, plan dashed frame, title mark dot, h2 underline, bullets, focus bar, status dot, ▼▲ glyphs, inset vignette, gap arms, enlarged slider hit area | explicit child nodes |
| `@keyframes` | 20 | §3.3 | `CssKeyframes` |
| `animation` | 36 | | `CssAnimator` |
| `transition` | 63 | §3.4 | `Transitions` |
| `color-mix(in srgb …)` | 157 | `X p%, transparent` (= X at alpha p) and two-colour mixes, some with a translucent second colour | `CssColor.Mix` (spec premultiplied) |
| `text-overflow: ellipsis` | 6 | | TMP `Ellipsis` |
| `-webkit-line-clamp` | 1 | supply text 2 lines | TMP `maxVisibleLines` + ellipsis |
| `white-space` pre / pre-line / nowrap | many | | TMP wrapping off / `\n` kept |
| `text-wrap: balance / pretty`, `word-break: auto-phrase / keep-all` | 6 | meta paragraphs, hall lines, explore hint, journal tabs | `Wrap.Balance`, phrase `<nobr>` heuristic (§6 gap) |
| `text-decoration` | 4 | dotted underline 2 px offset 4; line-through 2/3 px | `TextDecor` |
| `cursor` | 71 | pointer / grab / grabbing / default | optional cursors (§6 gap) |
| scrollbars | meta: 8 px, steel thumb radius 4; decklist none + `scroll-snap-type: y mandatory` | | `ScrollArea` |

### 3.2 Facts measured in Chrome 155 (headless, `--force-device-scale-factor=1`)

Probe pages and screenshots: `C:\Users\user\AppData\Local\Temp\claude\C--Users-user-orca-original-card-battle\1ab86662-d6cc-4831-aa8c-76f9592e0ca6\scratchpad\kitprobe\probe*.html|png`;
the measuring scripts sit one folder up (`measure_probe.py`, `m2.py`, `m3.py`, `m4.py`, `fontmetrics.py`). Use these numbers rather than the CSS specs where they differ.

- **Dashed borders**: 1 px → dash 3, gap 2; 2 px → dash 6–7, gap 3–4 (period 10). So dash = 3w, gap = 2w, with Chrome
  stretching the gaps so a side starts and ends with a dash at the corners. Rounded dashed borders follow the arc with
  the same pattern. A 16 px circle with a 2 px dashed border shows 4 dashes.
- **Dotted**: 1 px → 1 on, 1 off (square dots). Dotted underline 2 px → 2×2 square dots every 4 px.
- **Double 3 px**: 1 px line, 1 px gap, 1 px line.
- **Mixed side widths/colours** (`border-left: 3px red; others 1px green`): the corner pixels split along the diagonal.
- **Outline** with `border-radius: 6`, width 2, offset 4: the outline is rounded, outer radius 12 (= radius + offset + width).
- **Repeating gradients**: 135° `wick 0 2px, transparent 2px 5px` has its band phase 0 at the box's top-left corner;
  90° `amber 0 9px, transparent 9px 15px` = 9 on, 6 off from the left edge.
- **Blur**: `box-shadow` blur b → Gaussian σ = b/2 (profile matched erfc within 2/255). `text-shadow` b → σ = b/2.
  **`filter: drop-shadow(0 0 b)` → σ = b** (not b/2): a 20 px drop-shadow falls off like σ = 20.
- **Line-through 2 px** on 24/32 text sits 16–17 px below the line box top; underline offset 4 px lands at the line box
  bottom for 16/22 text (font underline position + 4).

### 3.3 Every `@keyframes`

Per-segment easing: CSS applies the timing function to each keyframe interval, not to the whole run.

| name (file:line) | animates | used by (duration, easing, other) |
| --- | --- | --- |
| `ocb-pip-lit` (bundle.css:136) | box-shadow `0 0 0 0 wick .9` → `0 0 0 8px wick 0`; scale 1.35 → 1 | `.ocb-pip.is-lit` .32s ease-out (DS; the game re-runs it on its clock) |
| `ocb-breathe` (227) | opacity 1 → .68 → 1 | DS backdrop only (unused by the game) |
| `ocb-drift` (229) | translate 0 → (--dx, --dy), opacity 0 → 1 (15 %) → 1 (85 %) → 0 | DS motes only (unused) |
| `ocbg-explore-fade` (explore.css:221) | opacity 0 → 1 | `.is-returning` 420ms ease-out both; dim 260ms ease-out both; train preview card 160ms ease-out both; overlay shade 200ms ease-out both |
| `ocbg-explore-fadeout` (222) | opacity 1 → 0 | dim `.is-leaving` 220ms ease-in both |
| `ocbg-explore-roll` (34) | text-shadow 0 → `0 0 12px ink 70%` (25 %) → 0 | rolled labels 900ms ease-out |
| `ocbg-explore-pulse` (46) | opacity 1 → .45 → 1 | gauge-ahead `.is-warn` 1.6s ease-in-out infinite; aiming slot 1.6s ease-in-out infinite |
| `ocbg-explore-flow` (82) | stroke-dashoffset 24 → 0 | lit edge flow (dash `2 22`, width 4, round) 1.2s linear infinite |
| `ocbg-explore-beckon` (92) | box-shadow `card, 0 0 0 0 amber 0%` ↔ (50 %) `card, 0 0 18px 2px amber 38%` | reachable / jump node face 2.4s ease-in-out infinite |
| `ocbg-explore-ripple` (102) | opacity .9 → 0, scale 1 → 1.55 | node face ::after (inset −3, 2 px amber, radius 6) 820ms cubic-bezier(.22,.61,.36,1) |
| `ocbg-explore-breathe` (109) | opacity .75 → 1 → .75, scale .94 → 1.06 → .94 | walker glow 2.6s ease-in-out infinite |
| `ocbg-explore-land` (112) | scale (1.18, .84) → 60 % (.96, 1.05) → none | walker disc 320ms cubic-bezier(.22,.61,.36,1) |
| `ocbg-explore-plate-in` (225) | opacity 0, translateY 18 → 1, none | plate 300ms; interlude plate 600ms; interlude next 700ms delay 120ms; overlay page 260ms; confirm card 240ms; all cubic-bezier(.22,.61,.36,1) both |
| `ocbg-explore-plate-out` (226) | opacity 1 → 0, translateY 0 → 10 | plate `.is-leaving` 220ms ease-in both; overlay page 200ms ease-in both |
| `ocbg-explore-deal` (291) | opacity 0, translateY 36, rotate −4° → 1, none | reward pick cards 460ms cubic-bezier(.22,.61,.36,1) both, delay `120 + i·110` ms (`ExploreScene.js:1116, 1710`) |
| `ocbg-explore-window` (361) | scale 1.02 → 1.08, translateY 0 → −6 | interlude window canvas 9s ease-in-out infinite alternate |
| `ocbg-explore-drawer` (378) | translateX 100 % → 0 | journal drawer 220ms cubic-bezier(.22,.61,.36,1) both |
| `ocbg-explore-drawer-out` (379) | translateX 0 → 100 % | 200ms ease-in both |
| `ocbg-meta-stamp` (meta.css:129) | box-shadow `0 0 0 0 amber 70%` → `0 0 0 22px amber 0%` | seal `.is-stamped` .7s ease-out |
| `ocbg-meta-pulse` (248) | box-shadow `0 0 0 0 amber 60%` → `0 0 0 14px amber 0%` | supply `.is-pulse` .5s ease-out |

Also a WAAPI-style animation exists only in the unused DS `PileDrawer` (cubic-bezier(.33,1,.68,1)).

### 3.4 Transitions

All CSS transitions, deduplicated (`grep -o "transition: …"`): `border-color/background .12s–.14s` (buttons, filters,
steps, slots, chips); `transform 90ms` (+ border-color 120ms, background-color 120ms) for hover lifts;
`transform 90ms cubic-bezier(.4,0,.2,1), opacity 300ms` (node lift); `transform 160ms cubic-bezier(.22,.61,.36,1),
opacity 160ms` (selection frame); `transform 120ms cubic-bezier(.4,0,.2,1)` (pick); `opacity 140ms/.14s/.2s/.22s/.24s/
240ms/400ms/480ms ease-out/.6s/900ms ease-in-out`; `opacity 500ms ease-in-out, filter 400ms ease-in-out` (map
engaging); `stroke 300ms, stroke-width 300ms` (map edges); `background-color 400ms ease-in-out` (veil);
`background-color/border-color/box-shadow 420ms ease-out` (pips); `width .26s ease-out, background .2s` (gauge);
`width .3s ease-out` (DS bar fill); `letter-spacing .2s, text-shadow .2s` (title menu item); `transform .22s`
(title mark); `box-shadow .2s, border-color .2s` (stratum); `box-shadow .12s` (DS omen). No timing function = CSS
`ease` = cubic-bezier(.25,.1,.25,1). JS-set transitions: edge draw-in `stroke-dashoffset dur(520)ms
cubic-bezier(.22,.61,.36,1)` and node entry `opacity dur(420)ms ease-out, transform dur(420)ms cubic-bezier(.22,.61,.36,1)`
(`ExploreScene.js:475, 495`). Inside the battle no CSS transition runs (`battle.css:47-52`, `field.css:4`, `hud.css:3`).

### 3.5 SVG and canvas created in JS

- SVG elements: `svg` (viewBox, width/height, overflow visible), `path` (icons, figures, fx streaks, frames, gear,
  stay), `g` (fill/stroke groups, `transform="translate(dx dy)"`), `defs`, `linearGradient` (`gradientUnits=userSpaceOnUse`
  x1..y2, or objectBoundingBox 0,0→0,1) + `stop` (offset, stop-color with alpha), `radialGradient` (userSpaceOnUse cx cy r)
  + `stop` (stop-opacity), `pattern` (hatch, `patternTransform=rotate`), `rect`, `ellipse` (fx ring), `line` (map
  edges), `circle` (cut mark), `title` (ignore). Attributes: `stroke-dasharray`, animated `stroke-dashoffset`,
  `stroke-linecap` round/square, `stroke-linejoin` round, `vector-effect: non-scaling-stroke` (fx shield, `fx.js:726-747`),
  `fill-opacity`, `transform="translate(32 36) scale(0.84) translate(-32 -36)"`.
- Gradient strokes: the slash core uses a userSpaceOnUse linear gradient along the swing (`fx.js:208-217`: stops 0 α0,
  .35 α.95, .8 α1, 1 α.2), over a glow stroke (×2.6 width, α.35) and under an ink edge (×.3 width, α.9).
- Canvas 2D (10 contexts): battle light shafts (rotated linear gradient across each shaft, then `destination-in` with a
  vertical fade to 560 px, `field/backdrop.js:161-185`); explore and meta backdrops (sky linear, haze radial, shafts,
  vignette radial with an inner radius `createRadialGradient(W/2,H/2,H·.35, W/2,H/2,W·.62)`, `meta/backdrop.js:129`);
  the 32 px mote sprite (radial); the washi fibre tile (360×360, 26 radial blobs α .012–.032 and 260 quadratic strokes
  0.5–1.4 px α .04–.11, round caps, tiled with wrap-around, seed 48271 Park-Miller, `meta/util.js:126-171`); text
  measurement (`hand-card.js:88-98`).

### 3.6 Font vertical metrics (read from the TTFs in `$UP\Assets\View\Resources\Fonts`)

| file | unitsPerEm | hhea asc/desc/gap | OS/2 win asc/desc | USE_TYPO | underline pos/thick |
| --- | --- | --- | --- | --- | --- |
| ShipporiMincho-Bold | 1000 | 1160 / −288 / 0 | 1160 / 288 | 0 | −75 / 50 |
| ZenKakuGothicNew-Regular/Bold | 1000 | 1160 / −288 / 0 | 1160 / 288 | 0 | −133 / 20 |
| IBMPlexMono-SemiBold/Bold | 1000 | 1025 / −275 / 0 | 1025 / 275 | 1 | −112/88, −100/100 |

hhea equals the win metrics, so every browser lays these fonts out with ascent 1.160 em / descent .288 em
(content area 1.448 em) for Shippori and Zen Kaku, and 1.025 / .275 (1.300 em) for Plex Mono. TMP's `faceInfo` reads
the same hhea values. Formulas in §4.9.

---

## 4. The Kit API

### 4.1 Assemblies, folders, sync

| assembly | sources | asmdef | references |
| --- | --- | --- | --- |
| `Trial.Geometry` | `$PORT\Trial.Geometry\*.cs` (+ `Data\*.cs`), csproj `$PORT\Trial.Geometry\Trial.Geometry.csproj` (netstandard2.1, `LangVersion 9.0`, `Nullable enable`, like `BattleCore.csproj`) | `$KIT\Trial\Geometry\Trial.Geometry.asmdef` (`noEngineReferences: true`, `autoReferenced: true`) | none |
| `Trial.Geometry.Tests` | `$PORT\Trial.Geometry.Tests\*.cs`, csproj net10.0 + NUnit 4.3.2 (copy `BattleCore.Tests.csproj`) | dotnet only (no Unity copy needed) | Trial.Geometry |
| `Trial.Kit` | `$KIT\Trial\Kit\**\*.cs` | `$KIT\Trial\Kit\Trial.Kit.asmdef` | Trial.Geometry, Unity.TextMeshPro, UnityEngine.UI, Unity.InputSystem |
| `Trial.Common` (or part of the app assembly) | `$KIT\Trial\Common\Stage.cs`, `CommonStyles.cs` | per the architect | Trial.Kit |

Tooling the architect has to extend (outside the kit's own files):

- `$PORT\tools\unity-sync-plan.mjs`: copy `$PORT\Trial.Geometry\**\*.cs` → `Assets/View/Trial/Geometry/` (like
  BattleCore → Assets/Core), and `$KIT\Assets\View\Trial\**\*.{cs,asmdef}` → `Assets/View/Trial/` (like Depiction);
  add both to `OWNED_ROOTS`.
- `$PORT\UnityCorePort.slnx`: add the two projects.
- `$PORT\tools\fetch-fonts.mjs`: add the three TTFs (§4.13) with their SHA-256 from the pinned google/fonts commit.

### 4.2 Trial.Geometry (engine-free) — classes and signatures

```csharp
namespace Trial.Geometry
{
    // ---- math -----------------------------------------------------------------------------------
    public readonly struct Vec2 { public readonly float X, Y; /* + - * / Dot Cross Len Norm Perp Lerp */ }
    public readonly struct RectF { public readonly float X, Y, W, H; /* y-down stage px: X,Y = top-left */ }
    public readonly struct Affine            // 2×3, y-down
    {
        public static Affine Translate(float x, float y), Scale(float sx, float sy), RotateCss(float deg); // CSS/SVG rotate: clockwise on screen
        public static Affine ViewBox(RectF viewBox, float w, float h);   // preserveAspectRatio xMidYMid meet
        public Affine Then(Affine next); public Vec2 Apply(Vec2 p); public float MeanScale { get; }
    }

    // ---- colour ---------------------------------------------------------------------------------
    public readonly struct Rgba               // straight alpha, sRGB channels 0..1
    {
        public static Rgba Hex(int rrggbb, float a = 1), Bytes(int r, int g, int b, float a = 1);
        public Rgba WithAlpha(float a); public Rgba MulAlpha(float k);
        public static Rgba LerpPremul(Rgba a, Rgba b, float t);          // CSS gradient interpolation
    }
    public static class CssColor
    {
        public static Rgba Parse(string css, DsTheme theme = DsTheme.Dusk); // #rgb #rrggbb rgb() rgba() transparent var(--x[, fb]) color-mix(in srgb, A p%?, B q%?)
        public static Rgba Mix(Rgba a, float pa, Rgba b, float pb);       // CSS Color 5: premultiplied, normalise p, alpha × (pa+pb) when < 100%
    }
    public readonly struct ColorFilter         // CSS brightness(k) grayscale(g), sRGB, Rec.709 weights
    {
        public readonly float Brightness, Grayscale;
        public Rgba Apply(Rgba c); public ColorFilter Then(ColorFilter inner);
    }

    // ---- easing and keyframes -----------------------------------------------------------------------
    public delegate float EaseFn(float t);
    public static class Ease
    {
        public static readonly EaseFn Out, In, InOut, Linear, Smooth;      // stage.js EASE (= UiTween.Apply)
        public static readonly EaseFn Css /* ease */, CssIn, CssOut, CssInOut;
        public static readonly EaseFn Decel /* (.22,.61,.36,1) */, Standard /* (.4,0,.2,1) */;
        public static EaseFn CubicBezier(float x1, float y1, float x2, float y2); // Newton + bisection, as Chromium's UnitBezier
        public static EaseFn Parse(string name);                           // "out", "ease-out", "cubic-bezier(...)"
    }
    public sealed class CssKeyframes      // name, ordered stops; each stop sets a subset of AnimProps
    {
        public static CssKeyframes Get(string name);                       // the 20 of §3.3 as data
        public AnimProps Sample(float progress, EaseFn segmentEase);       // per-segment easing, missing props interpolate between the stops that set them
    }
    public struct AnimProps { public float? Opacity, Tx, Ty, Sx, Sy, Rot, ShadowSpread, ShadowBlur, ShadowAlpha, TextGlowBlur, TextGlowAlpha, DashOffset; }
    public sealed class CssAnimation       // duration, delay, iterations (∞), direction, fill
    {
        public static CssAnimation Parse(string shorthand);                // "ocbg-explore-plate-in 300ms cubic-bezier(.22,.61,.36,1) both"
        public bool TrySample(float tMs, out AnimProps props);             // false = not applied (before delay without backwards fill, after end without forwards)
    }

    // ---- paths ------------------------------------------------------------------------------------
    public sealed class PathData { public static PathData Parse(string d); public IReadOnlyList<SubPath> SubPaths { get; } }
    public static class Flatten
    {
        /// <summary>Lines, cubics, quads, arcs (SVG F.6.5/F.6.6 incl. radius correction) → polylines; tolerance in OUTPUT px after `m`.</summary>
        public static List<Polyline> Run(PathData path, Affine m, float tolerancePx = .2f);
    }
    public sealed class Polyline { public List<Vec2> Points; public bool Closed; public float Length { get; } }
    public static class Triangulate
    {
        public static void Fill(Polyline ring, MeshBuilder mb, Paint paint);   // ear clipping, CW/CCW agnostic, dedupes points
        public static bool IsSimple(Polyline ring);                            // used by tests over every shipped path
    }
    public enum Cap { Butt, Round, Square } public enum Join { Miter, Round, Bevel }
    public sealed class StrokeStyle
    {
        public float Width; public Cap Cap; public Join Join = Join.Miter; public float MiterLimit = 4;
        public float[] Dash; public float DashOffset; public bool NonScaling; // NonScaling: Width is in output px
    }
    public static class Stroker
    {
        public static void Stroke(Polyline line, StrokeStyle s, Affine m, float feather, MeshBuilder mb, Paint paint);
        public static List<Polyline> Dash(Polyline line, float[] pattern, float offset);  // pattern restarts per subpath; caps per dash
        /// <summary>A soft band: alpha(d) by `profile` across the line (d = signed distance from the centre line).</summary>
        public static void Soft(Polyline line, Func<float, float> profile, float halfExtent, Affine m, MeshBuilder mb, Rgba color);
    }

    // ---- meshes -----------------------------------------------------------------------------------
    public abstract class Paint { public abstract Rgba At(Vec2 p); public virtual IEnumerable<IsoLine> Bands(RectF bounds) => null; }
    public sealed class SolidPaint : Paint { }
    public sealed class LinearGradient : Paint                       // CSS + SVG semantics
    {
        public static LinearGradient Css(float angleDeg, RectF box, params GradientStop[] stops); // gradient line length |W sinθ|+|H cosθ|
        public static LinearGradient Points(Vec2 p0, Vec2 p1, params GradientStop[] stops);       // SVG userSpaceOnUse
        public bool Repeating;
    }
    public sealed class RadialGradient : Paint                       // ellipse; closest-side / farthest-corner / explicit rx ry; optional inner radius (canvas)
    {
        public static RadialGradient Css(string sizeAndPosition, RectF box, params GradientStop[] stops); // "closest-side", "ellipse 72% 66% at 50% 50%", "80% 70% at 50% 0%", "ellipse at 30% 18%"
    }
    public readonly struct GradientStop { public readonly Rgba Color; public readonly float Pos; public readonly bool PosIsPx; }

    public sealed class MeshBuilder
    {
        public List<Vec2> Pos; public List<Rgba> Col; public List<int> Idx;
        public int AddVertex(Vec2 p, Rgba c); public void AddTri(int a, int b, int c); public void Clear();
        public void SplitByLines(IEnumerable<IsoLine> lines);      // exact linear gradients: split along stop isolines
        public void Refine(float maxEdgePx);                       // radial paints, gradient strokes
        public void ClipConvex(IReadOnlyList<Vec2> convexPolygon); // Sutherland–Hodgman per triangle
        public void SubtractConvex(IReadOnlyList<Vec2> convexPolygon); // outer box-shadow: keep only outside the box
        public void ApplyAlphaRamp(bool vertical, IReadOnlyList<(float at, float alpha)> ramp); // mask-image fades (splits at kinks)
        public void ApplyFilter(ColorFilter f); public void Transform(Affine m);
    }

    // ---- shapes, boxes ------------------------------------------------------------------------------
    public readonly struct Radii { /* TL, TR, BR, BL, each (rx, ry); CSS clamping: scale all by min(side / sum) when they overflow */ }
    public static class Shapes
    {
        public static Polyline RoundRect(RectF r, Radii radii, float tolPx = .2f);
        public static Polyline Ellipse(RectF r, float tolPx = .2f);
        /// <summary>Convex fill with an AA fringe: solid core shrunk by feather/2, alpha-0 rim grown by feather/2.</summary>
        public static void FillConvexAA(Polyline convex, Paint paint, float feather, MeshBuilder mb);
        public static void Ring(Polyline outer, Polyline inner, Paint paint, float feather, MeshBuilder mb); // solid borders
    }
    public sealed class BoxStyle
    {
        public List<Paint> Backgrounds;          // CSS order: first = top; paint last first
        public BorderSide Top, Right, Bottom, Left;    // width, style (None/Solid/Dashed/Dotted/Double), colour
        public Radii Radius;
        public List<BoxShadow> Shadows;          // outer and inset, CSS order
        public Outline Outline;                  // width, style, colour, offset (rounded: radius + offset)
    }
    public readonly struct BoxShadow { public readonly float X, Y, Blur, Spread; public readonly Rgba Color; public readonly bool Inset; }
    public static class BoxMesh
    {
        /// <summary>Paint order: outer shadows (last listed first), backgrounds (bottom first, under the border), inset shadows, border, outline.</summary>
        public static void Build(RectF borderBox, BoxStyle s, float feather, MeshBuilder mb);
    }
    public static class Hatch
    {
        /// <summary>SVG pattern semantics (§2.3): bars `width` every `gap` at `angleDeg`, phase at `origin`, clipped to `clip` (convex).</summary>
        public static void Bars(IReadOnlyList<Vec2> clip, Vec2 origin, float angleDeg, float gap, float width, Rgba color, float feather, MeshBuilder mb);
        /// <summary>CSS repeating-linear-gradient with hard stops: bands [from, to) every `period` along the CSS angle, phase at the box's gradient start.</summary>
        public static void Stripes(RectF box, IReadOnlyList<Vec2> clip, float cssAngleDeg, float from, float to, float period, Rgba color, float feather, MeshBuilder mb);
    }
    public static class Blur { public static float Erf(float x); public static float Erfc(float x); public static float EdgeAlpha(float distOutside, float sigma); }

    // ---- text math, layout, CSS parsing -------------------------------------------------------------------
    public enum Family { Display, Body, Mono }
    public readonly struct FontSpec { public readonly Family Family; public readonly int Weight; public readonly float Size, LineHeightPx; }
    public static class TextMath
    {
        public static FontSpec ParseFont(string css);                   // "700 22px/28px var(--font-display)", "400 18px/1.4 var(--font-body)"
        public static float TmpCharacterSpacing(float em) => em * 100f;
        public static float TmpLineSpacing(float pitchPx, float sizePx, float ascentEm, float descentEm, float lineGapEm);
        public static float CssBaselineFromTop(float lineHeightPx, float sizePx, float ascentEm, float descentEm); // half-leading model
        public static Vec2 VerticalPunctuationOffset(char c, float sizePx); // 、。 → upper right; ー 「」 （） → rotate or vertical forms
    }
    public sealed class FlexSpec { public bool Column, Wrap; public float Gap, RowGap; public Insets Padding; public Align AlignItems; public Justify Justify; public float? Width, Height; }
    public struct FlexItem { public float W, H, Grow, MarginL, MarginR, MarginT, MarginB, Baseline; public bool AutoMarginStart, AutoMarginEnd; public Align? Self; }
    public static class Flex { public static FlexResult Solve(FlexSpec spec, IReadOnlyList<FlexItem> items); }      // rects + content size
    public static class Grid { public static GridResult Solve(IReadOnlyList<Track> cols, float colGap, float rowGap, IReadOnlyList<GridItem> items); } // px / fr / auto / minmax(0,1fr)

    public static class Css   // declaration parser for the subset the DS and the scenes use; results cached by string
    {
        public static BoxStyle Box(string declarations, DsTheme theme = DsTheme.Dusk);
        public static TextStyleData Text(string declarations, DsTheme theme = DsTheme.Dusk);
        public static IReadOnlyList<BoxShadow> Shadows(string css);  public static Paint Background(string css, RectF box);
        public static IReadOnlyList<TransitionSpec> Transitions(string css);
    }
}
```

Data files (engine-free, so the dotnet tests parse every string): `Data/DsIcons.cs` (ICONS, ICON_LABELS, FILLED),
`Data/ExploreIcons.cs` (`EXPLORE_ICONS`), `Data/ExtraIcons.cs` (stay, gear, cut mark, shield, crack),
`Data/DsFigures.cs` (FIGURES + `Poly`). The battle's `art.js` VARIANTS stay with the battle field port, one file per JS module.

Tests to write (dotnet): every icon/figure path parses, flattens and (for fills) `IsSimple`; arc endpoint/centre
conversion against known values; dash lengths along a polyline; erf against a table; `CssColor.Mix` for
`color-mix(in srgb, var(--card) 70%, var(--ink) 18%)` (alpha .88) and `… var(--amber) 16%, transparent` (amber α .16);
cubic-bezier against Chromium values at t = .25/.5/.75; keyframe per-segment easing; gradient line length for 135°;
hatch phase at the origin; box-shadow alpha at d = 0 equals .5 × colour alpha (σ = b/2); flex with `margin-left: auto`.

### 4.3 Anti-aliasing

- Feather `f` = one screen pixel in stage units = `1 / (stageScale × nodeScale)`, where `stageScale` is the canvas
  scale factor (`TrialStage.Scale`) and `nodeScale` the accumulated `localScale` from the stage to the graphic (a
  `.72` card, a `.8` detail card, `.5` thumbnails). Rebuild on stage resize; animated scale changes do not rebuild.
- Convex fills (rounded rects, ellipses, circles): a solid core inset by `f/2` plus a rim strip to `+f/2` whose outer
  vertices carry the same RGB at alpha 0 (vertex-normal offsets; corners are already many vertices).
- Strokes: the band is `w + f` wide with the outer `f` fading out. A stroke thinner than `f` keeps width `f` and
  multiplies its alpha by `w / f` (coverage). The same rule draws 1 px borders at 1280×720 (stage scale .667: 1 px = .67
  screen px) the way the browser's rasteriser does.
- Concave fills (figures, filled icons): triangulated core + an outer fringe stroke along the outline (width `f`, alpha
  1 → 0 outward). Overlap between adjacent figure parts is fine (opaque fill; rim alphas compound as in the browser).
- Hatch bars and stripes get a fringe on their long edges only; the clipped ends sit under the 2 px red border.
- Text AA is TMP's SDF.

### 4.4 Box-shadow, glows and blur

- Outer `box-shadow: x y b s c`: shape = the border box grown by `s` (corner radius per the spec: `r + s` for
  `r ≥ s`; for `r < s` use `s·(1 + (r/s − 1)³)`, and 0 stays 0), shifted by (x, y), blurred with σ = b/2 (measured).
  Mesh: concentric rounded-rect contours at signed distances `d ∈ [−3σ, +3σ]` (8–12 rings; denser near 0) with alpha
  `c.a · ½·erfc(d / (σ√2))`, plus the inner core at full alpha. Then `SubtractConvex(borderBox)`: CSS paints an outer
  shadow only outside the border box, which matters under translucent panels. A blur of 0 is a solid ring
  (spread rings `0 0 0 2px`). Corners: the ring model is the usual approximation (the true 2-D blur of a corner is
  slightly darker inside the arc); acceptable.
- Inset shadows: contours inward from the padding box: `inset 0 0 0 2px c` = a 2 px inner ring; `inset 3px 0 0 c` = a
  3 px bar on the left inner edge (the hall/trace rows' cursor); blurred insets (paper vignettes 40–110 px) = rings
  inward with `½·erfc(…)` alpha, clipped to the padding box.
- Several shadows: paint the last listed first.
- `filter: drop-shadow(0 0 b c)` on strokes/paths: σ = b (measured). For a stroke of width w the blurred profile is
  `c.a · [Φ((d + w/2)/σ) − Φ((d − w/2)/σ)]` (Φ = normal CDF). Draw it with `Stroker.Soft` under the stroke. For filled
  shapes (shield, sweep) use `Stroker.Soft` along the outline with the outward half of the profile of a large width.
  Drop-shadow on an element whose own shape is a rect (fx streak/sweep divs) = a box-shadow with σ = b and no clipping.
- Glow sprites: `glowGradient` (`field/art.js:25-28`) = closest-side radial with stops α·(1−r)² at r = 0, .25, .5, .75
  and transparent at 1: use exactly those stops (the JS look wins over ProceduralArt's continuous (1−r)²).

### 4.5 Hatching and clipping

- Bars are generated as long quads covering the clip polygon's bounds, then each quad is clipped with
  `ClipConvex(clip)` (a rounded rect polygon is convex, so one Sutherland–Hodgman pass is exact).
- Floor threat: clip = rounded rect at tile + (1,1), size (w − 2) × 38, radius 4 (the threat box's padding box);
  origin = tile + (1,1); bars 3 px, gap 11 or 6, angle from `EnemyHatch` (45 / −45 / 90). The wipe reveal
  (`clip-path: inset(0 x% 0 0)`) intersects the clip with the half-plane `x ≤ left + (1−x%)·w` before clipping
  (two convex polygons intersect to a convex polygon).
- Swatch: clip = (size − 4)² square at (2, 2), radius 1; bars 2 px, gap 6.
- HP loss stripes: `Stripes(box, clip = bar inner rounded rect, 135°, 0, 2, 5, wick)`.
- Card attribute bars: `ClipConvex(cardRoundRect)` on the 8 px and 5 px rects.
- Shield halves (`inset(0 50% 0 0)` / `inset(0 0 0 50%)`): clip the shield mesh by a half-plane; the parting motion
  moves the two graphics.

### 4.6 SVG paths: parse → flatten → triangulate → stroke

- Tokeniser: commands `MmLlHhVvCcSsQqTtAaZz`; numbers with optional sign, leading dot, exponent; a second dot starts a
  new number; after `A/a` the large-arc and sweep flags are single characters `0`/`1` and may be glued to the next
  number. Implicit repeats: extra pairs after `M` are `L`, after `m` are `l`. `Z` returns to the subpath start; a
  command after `Z` without `M` starts at that point.
- Smooth curves: `S/s` reflect the previous cubic's second control point (or use the current point), `T/t` the
  previous quad's control point.
- Arcs: SVG implementation notes F.6.5 (endpoint → centre) with F.6.6 radius correction (scale radii up when the
  endpoints do not fit) and the zero-radius → line rule; flatten at angle step `2·acos(1 − tol/r)` with r in output px.
- Cubic/quad flattening: recursive subdivision until the control-polygon flatness ≤ tolerance (0.2 px after transform).
- Transforms: viewBox → element box (`Affine.ViewBox`, meet, centred), then group transforms. `NonScaling` stroke
  widths are applied after the transform.
- Fill rule: every `<path>` is filled per subpath (no shipped fill has holes; tests assert `IsSimple`). Two subpaths in
  one `d` that overlap would fill once in SVG; none of the shipped fills do. If a future path fails `IsSimple`, vendor a
  winding-rule tessellator (LibTessDotNet, SGI Free Software License B) rather than extending ear clipping.
- Stroking: per segment quads; joins (miter with limit 4 → bevel fallback, round fans, bevel); caps (butt, square =
  extend by w/2, round fans); dashes split the polyline by arc length first (pattern restarts per subpath, offset may be
  negative or animated), then each dash is capped; a zero-length piece with round caps is a dot of diameter w.
- Gradient strokes: stroke mesh → `Refine(6 px)` → per-vertex `paint.At`.

### 4.7 The CSS-like box

`Box` = one `BoxGraphic` per element that has a background, border, shadow or outline. Its mesh is built by
`BoxMesh.Build` in CSS paint order. Background is painted under the border (`background-clip: border-box`), so a
translucent border shows the background through it, as in the browser. Children are separate GameObjects drawn
after it (hierarchy order). Content box = border box − border − padding (`BoxModel.Content(rect, style, padding)`).

Borders:

- Solid uniform: one ring (outer = border box, inner = inset by w with radius `max(0, r − w)` per axis).
- Per-side widths/colours: four trapezoids split along the corner diagonals (with radii, split each corner arc at the
  angle `atan2(wTop, wLeft)`).
- Dashed: stroke the ring's centre line per side with dash 3w / gap 2w, stretching gaps so each side starts and ends
  on a dash; around rounded corners continue along the arc (measured §3.2). Dotted: 1 px squares 1 on / 1 off;
  2 px and wider: w × w dots, w gap.
- Double (3 px): two 1 px rings, 1 px apart.
- Outline: a ring outside the border box at `offset`, radius `r + offset`; negative offset draws inside.
- `::before` / `::after`: explicit child nodes (before = first child, after = last child).

Overflow: `overflow: hidden` on a rectangle = `RectMask2D`; with a radius, clip the child meshes by the box's rounded
rect (`ClipConvex`) instead of a stencil mask (stencils have no AA).

### 4.8 Flex and grid helpers

The DS and the scenes lay out with flex rows (icon + text + `margin-left: auto` tail), wrapping chip rows, columns
with gaps, `flex: 1` fillers and a few grids (`grid-template-columns: 24px auto`, `186px 1fr`, `repeat(3, minmax(0, 1fr))`,
`28px 150px 1fr auto`). `Flex.Solve` covers: direction, gap/row-gap, padding, `align-items` (start/centre/end/stretch/
baseline), `justify-content` (start/centre/end/space-between), wrap with `align-content: flex-start`, `flex-grow`,
auto margins, fixed sizes; no shrink beyond `min-width: 0` truncation (the text then ellipsises). `Grid.Solve` covers
fixed px, `fr`, `auto` (max content), `minmax(0, 1fr)`, column/row gaps, column spans. The Unity adapter
`FlexBox.Layout(RectTransform container, FlexSpec)` measures children (fixed sizes from `UiNode`, text from
`TextMeasure`) and writes their positions; it runs when content changes, never per frame. Do not use Unity
`LayoutGroup`s (different semantics, rebuild costs).

### 4.9 Text (TMP)

Font assets (`Trial.Kit.TrialFonts`):

```csharp
public static class TrialFonts
{
    public static TMP_FontAsset Get(Family family, int weight);    // Display 400/700/800, Body 400/700, Mono 400/600/700
    public static void Prewarm(string characters);                  // TryAddCharacters on every asset that will draw them
    public static string Tag(Family family, int weight);             // "Mono-600" for <font="Mono-600"> rich text
}
```

- Created on first use with `TMP_FontAsset.CreateFontAsset(font, samplingPointSize: 64, atlasPadding: 16,
  GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true)` from
  `Resources.Load<Font>("Fonts/<file>")` (the TTF importers have `includeFontData: 1`, checked in
  `$UP\Assets\View\Resources\Fonts\ZenKakuGothicNew-Regular.ttf.meta`). Name each asset (`"Mono-600"`) and register it
  with `MaterialReferenceManager.AddFontAsset` so `<font="…">` tags resolve (explore's digit runs need it).
- Fallback chains: Mono-w → Body (same or nearest weight) for Japanese; Display-w → Body-w; Body-w → Display-700.
- Cache in statics but test with Unity's `!asset` null check, and reset in
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`: domain reload is disabled
  (`$UP\ProjectSettings\EditorSettings.asset:29-30`), and runtime-created assets can be destroyed when Play Mode stops
  (the same trap `ProceduralArt.Cached` documents).
- TMP essentials are imported in `$UP\Assets\TextMesh Pro` (shaders, `TMP Settings.asset`, Japanese kinsoku lists in
  `LineBreaking Leading/Following Characters.txt`). Default font asset stays LiberationSans; our assets are set per label.

`TextStyle` (from `TextMath.ParseFont` + colour/align/wrap) and the label factory:

```csharp
public sealed class TextStyle
{
    public Family Family; public int Weight; public float Size; public float LineHeight;   // px (CSS ratio × size resolved by the parser)
    public float LetterSpacingEm; public Rgba Color; public TextAlignmentOptions Align;
    public WhiteSpace WhiteSpace;     // Normal, NoWrap, Pre, PreLine
    public TextOverflow Overflow;     // Visible, Clip, Ellipsis
    public int LineClamp;             // 0 = none
    public TextShadow[] Shadows;      // CSS order
    public static TextStyle Css(string declarations);   // "font: 700 22px/28px var(--font-display); letter-spacing: .02em; color: var(--ink)"
}
public static class Label
{
    public static TextMeshProUGUI Make(RectTransform parent, string text, TextStyle style, string name = null);
    public static void Apply(TMP_Text t, TextStyle style);
}
```

Mapping rules:

- Size: `fontSize = px` (TMP units = canvas units = stage px). `enableAutoSizing = false`, `richText` only where needed.
- Letter-spacing: `characterSpacing = em × 100` (TMP spacing is in 1/100 em). Like Chrome, it is added after every
  character including the last, so centred text shifts by half a spacing in both. Verify in the gallery (§4.14):
  width("あいう", .1em) − width("あいう", 0) = 3 × .1 × size.
- Line height: TMP's line pitch = `(ascender − descender) × scale + lineSpacing × size / 100` (lineGap 0 for these
  fonts). For a CSS pitch L: `lineSpacing = (L / size − (asc − desc)) × 100`, with asc/desc in em from the asset's
  `faceInfo` (1.160/.288 Shippori and Zen Kaku → content 1.448 em; Plex 1.025/.275 → 1.300 em). Example: body 16/22 →
  −7.3; display 22/28 → −17.5; mono 48/52 → −21.7.
- Vertical placement: CSS puts the first baseline at `top + (L − content)/2 + ascent`; TMP at `top + ascent`. Shift the
  label's rect by `(L − content)/2` (negative for tight line heights: 16/22 body → −0.58 px) via the top margin
  (`TextMath.CssBaselineFromTop`). For one-line labels whose box is the line box, `VerticalAlignmentOptions.Top` +
  that margin matches the browser; avoid TMP `Midline`.
- `white-space: nowrap` → `textWrappingMode = NoWrap`; `pre`/`pre-line` → keep `\n` (TMP keeps them); normal →
  `Normal`; `overflow: hidden` + `text-overflow: ellipsis` → `overflowMode = Ellipsis`; clamp → `maxVisibleLines`.
- Japanese breaking: TMP's kinsoku lists (strict enough for `line-break: strict`). `text-wrap: balance` →
  `Wrap.Balance(TMP_Text)`: find the narrowest width that keeps the line count (binary search on `GetPreferredValues`).
  `word-break: auto-phrase` has no exact rule (Chrome uses a phrase model); `Wrap.JaPhrases(string)` wraps simple
  bunsetsu (break after 、。 and after particles は が を に で と の へ も, before kanji runs) in `<nobr>`; treat it as a
  heuristic and review screens by eye.
- `text-shadow` → two paths:
  1. blur ≤ 3 px (`0 1px 2px`, `0 1px 3px`): TMP underlay on a material preset shared per (font asset, shadow)
     (`TextShadows.Material(asset, shadow)`): offset and softness converted from px with
     `u = px × samplingPointSize / (size × gradientScale)` (gradientScale = padding + 1), then calibrated in the gallery.
     Only one underlay per material: with two shadows keep the larger.
  2. blur > 3 px (`0 0 6–8px ground` halos, `0 0 12–34px` glows): `TextGlow` graphic behind the label: one soft
     rounded rect per visible glyph (from `textInfo.characterInfo` bounds) with σ = blur/2 and alpha scaled by ~.6 so
     the sum of neighbours matches; animatable (the roll keyframe, title menu's `text-shadow .2s`).
- `text-decoration`: `TextDecor.Underline(t, start, length, thickness 2, offset 4, dotted)` and `LineThrough(t, …, 2|3)`
  draw `VectorGraphic`s from `textInfo` (underline y = baseline + |underlinePos| × size + offset; line-through at the
  font's strikethrough offset). TMP's own underline/strikethrough have the font's thickness and are not used.
- Vertical text (`VerticalText.Make(parent, string, style, pitchPx)`): one TMP per character (the title animates each
  character anyway), stacked at `pitch = size × 1 + letter-spacing` (46 × 1.22 = 56.12 px), columns right to left with
  the gap (40 px, second column `margin-top: 96`). 、。 move to the upper right of their cell
  (`TextMath.VerticalPunctuationOffset`), ー and brackets rotate −90°.
- Measuring (`TextMeasure`): `Width(text, style)` / `Height(text, style, width)` via `TMP_Text.GetPreferredValues` on a
  hidden measuring label per style, after `Prewarm`. This feeds the ported `CardTextFit` and the DS `fitCardText`
  modes; Unity and the browser fonts differ slightly, so the measurement must be TMP's own (what is measured is
  what is drawn).
- Mono digits: Plex Mono is monospaced; `font-variant-numeric: tabular-nums` needs nothing more.

### 4.10 Stage (`TrialStage`)

```csharp
public sealed class TrialStage : MonoBehaviour
{
    public const float W = 1920, H = 1080;
    public static TrialStage Create(string name = "TrialStage");   // Canvas + scaler + letterbox + root + EventSystem (InputSystemUIInputModule)
    public RectTransform Root { get; }                             // 1920×1080, anchors centre, pivot (0,1), RectMask2D
    public float Scale { get; }                                    // canvas scale factor = min(screenW/1920, screenH/1080)
    public event Action Resized;
    public RectTransform Layer(string name, int order);            // full-stage child; nested Canvas for rebuild isolation
    public Vector2 ToStage(Vector2 screenPos);                     // stage px, y down
    public Vector2 ToScreen(Vector2 stagePos);
}
public static class Ui        // h()/place() equivalents
{
    public static RectTransform Div(RectTransform parent, string name);
    public static RectTransform Place(RectTransform rt, float x, float y, float? w = null, float? h = null); // anchor = pivot = (0,1), anchoredPosition = (x, −y)
    public static void Clear(RectTransform parent);                  // detach then Destroy (as UiKit.ClearChildren)
}
```

- Canvas: Screen Space – Overlay (or Camera with the gamma target, §4.12), `CanvasScaler.ScaleWithScreenSize`,
  reference 1920×1080, `screenMatchMode = Expand` (scale = min, as `stage.js:56`). A full-canvas `--ground` image behind
  the 1920×1080 root makes the letterbox; the camera clear colour is `--ground` too.
- Coordinates: every Trial element uses anchor = pivot = (0, 1) and `anchoredPosition = (x, −y)`.
- CSS `transform-origin` → pivot; `UiNode.SetOrigin(ox, oy)` moves the pivot and compensates `anchoredPosition`
  so the layout box stays put. `rotate(θ)` → `localEulerAngles.z = −θ`. `translate(−50%, −50%)` → pivot (.5, .5).
- `toStage`: `RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, cam, out p)`; stage = `(p.x, −p.y)`.
- Layers: each stage layer is a child with its own nested `Canvas` (and `GraphicRaycaster` where it takes input), so a
  hand card's rebuild does not rebatch the backdrop. Z within a layer = sibling order; `UiNode.Z` re-sorts siblings
  stably (CSS z-index + DOM order).
- Pointer events: raycast targets are opt-in (`raycastTarget = false` by default on every kit graphic and label);
  `pointer-events: none` layers simply have no targets.

`UiNode` (one per element that moves): `X, Y, W, H` (stage px), transform channels `Translate (x, y)`,
`Scale (sx, sy)`, `Rotate`, `Origin`, `Opacity` (lazy `CanvasGroup`), `Filter` (`ColorFilter` pushed to every kit
graphic and label below), `Visible` (`display: none` → `SetActive(false)`), `PointerEvents`. CSS's separate
`translate` / `scale` properties and the `transform` property compose into one TRS (translations add, scales
multiply, rotations add), which covers every use in the JS. `Styler` (meta's `{ o, x, y, s, r }`) is a thin view over
`UiNode` for tweens.

### 4.11 Clock, eases, animations, transitions

```csharp
public sealed class TrialClock            // port of stage.js createClock (+ run/onFrame of the battle clock)
{
    public float Now { get; }                                       // ms of scene time
    public Func<float> Scale = () => 1f;                            // the battle clock plugs BattleSpeed / fast-forward here
    public Awaitable Tween(object target, string key, Func<float> get, Action<float> set, float to, float ms, EaseFn ease = null, float delay = 0);
    public Awaitable Run(float ms, EaseFn ease, Action<float, float> fn);   // fn(eased, raw); fn(1, 1) last
    public Awaitable Wait(float ms);
    public IDisposable OnFrame(Action<float, float> fn);            // (dtScene, dtReal)
    public void Tick(float dtRealMs);                               // driven from TrialStage.Update with Time.unscaledDeltaTime, dt clamped to 50 ms
    public void Stop();                                             // pending awaitables never complete (JS semantics)
}
```

- `Tween` with the same `(target, key)` replaces the running one (JS drops the keys from the older tween); `from` is
  read when the delay ends. Awaitables come from Unity 6 `AwaitableCompletionSource` and complete inside `Tick`, so
  continuations run in the same frame as a JS promise's microtask would. A tween/run/wait started from inside another
  callback is kept (battle clock review-19 rule). `Flow.All(params Awaitable[])` replaces `Promise.all`; never await
  one Awaitable twice.
- `CssAnimator.Play(UiNode node, CssAnimation anim, CssKeyframes frames)` samples per frame and writes opacity,
  transform channels, box-shadow parameters (rebuilds the box mesh), text-glow and dash offset. Infinite animations
  (beckon, breathe, pulse, flow, window) run on the scene clock's real time.
- `Transitions.Set(node, "border-color .14s, background .14s")` + `node.SetState(":hover", true)`: on a state change
  each listed property tweens from its current value to the state's value with the CSS easing. Inside the battle no
  transitions are declared (the JS turns them off).

### 4.12 Colour space and blending (needs a spike)

The project is Linear. uGUI converts vertex colours to linear and blends in linear space, while CSS blends in sRGB.
Effect on the browser look: `rgba(5,6,10,.62)` over a 50 % grey gives sRGB ≈ .20 in Chrome (.38 × .5 + .62 × .02)
but ≈ .32 in a linear blend (.38 × lin(.5) + .62 × lin(.02), re-encoded); every scrim, shadow, glow, translucent panel and AA fringe comes out lighter/thicker. Plan:

1. Render the Trial canvas with a dedicated camera (Screen Space – Camera) into a `RenderTexture` with
   `RenderTextureReadWrite.Linear` (no sRGB conversion on write), same size as the screen.
2. Make the UI shaders output the raw (gamma) vertex colours: `canvas.vertexColorAlwaysGammaSpace = true` keeps the
   CPU from converting; the UI/TMP shaders then convert only when `_UIVertexColorAlwaysGammaSpace` is set
   (`$UP\Assets\TextMesh Pro\Shaders\TMP_SDF-Mobile.shader:157-159`), so set that property to 0 on the kit's
   materials (or use copies of UI-Default / TMP_SDF-Mobile without the conversion block).
3. Present the RT full-screen with a blit that converts gamma → linear (`pow(c, 2.2)` or the exact sRGB curve) before
   the sRGB back buffer re-encodes it. Raycasts keep working when the RT matches the screen size (recreate on resize).

Spike acceptance: the probe above (`.62` scrim over grey, a 1 px `rgba(157,166,186,.26)` border on `#141925`, a
`0 8px 18px rgba(0,0,0,.45)` shadow) within 2/255 of a Chrome screenshot. Fallback A: switch the whole project to
Gamma (affects Battle.unity and the URP 2D lights; Koudai-san decides). Fallback B: stay linear and accept lighter
translucency.

`mix-blend-mode: screen` (stance body light) needs a material with `Blend OneMinusDstColor One` (colour premultiplied
by alpha in the shader); in the gamma target it matches the browser. It breaks batching for that graphic only.

### 4.13 Fonts to fetch

`fetch-fonts.mjs` (`$PORT\tools\fetch-fonts.mjs:25-34`) has ShipporiMincho-Bold, ZenKakuGothicNew-Regular/Bold,
IBMPlexMono-SemiBold/Bold. Missing weights the game uses:

| file (google/fonts ofl/) | why |
| --- | --- |
| `shipporimincho/ShipporiMincho-ExtraBold.ttf` | display 800: banners, end paper, plates, hall title, heir name, seal, ending h1, journal h2, choice tiles, chain banner, result title (§1.5) |
| `shipporimincho/ShipporiMincho-Regular.ttf` | display 400: title tagline (46 px vertical), ending sentence 34 px, memo sheet 20 px, flavour text and scribbles 18 px |
| `ibmplexmono/IBMPlexMono-Regular.ttf` | mono 400: played-attr count (14 px, game overrides to 700), button key hints `small` 16 px (meta primary / chip / washi buttons) |

Each Shippori file is about 8.5 MB (the Bold is 8,563,788 bytes), so the fetch keeps them out of git as today. Faux
bold from 700 (TMP `boldStyle` dilation) is the fallback if a file is refused, but it thickens strokes evenly and
does not match the 800 design.

### 4.14 DS port shape and a gallery for review

- `$KIT\Trial\Kit\Ds\` holds `static partial class Ocb` split by `bundle.js` section, same names in PascalCase:
  `Ocb.Icons.cs` (Icon, Hatch, Swatch), `Ocb.Data.cs` (STATUS, ATTR, OMEN_KIND, PILE_ICON, PILE_NAME),
  `Ocb.Card.cs` (Card + parts, FitCardText, MeasureBox, CardDetail), `Ocb.Omen.cs`, `Ocb.Status.cs`, `Ocb.Stance.cs`,
  `Ocb.Stamina.cs` (StaminaPlate, Pile), `Ocb.Floor.cs`, `Ocb.Marks.cs` (TargetFrame, PredTag), `Ocb.Notices.cs`,
  `Ocb.Hud.cs`, `Ocb.Figures.cs` (Figure, FootLight), and `Ocb.Styles.cs` = every `.ocb-root .ocb-*` rule of
  `bundle.css` as a named `BoxStyle`/`TextStyle` built with `Css.Box("…")` from the CSS text copied verbatim, one
  constant per rule, in file order (so a reviewer can diff them).
- Builders return a small handle (`OcbElement`: root `RectTransform`, `UiNode`, named parts, `Update(d)` where the JS
  has `el.update`) instead of DOM nodes; states (`is-lit`, `is-unpayable`, `is-hover`, `is-off` …) are bools on the
  handle that re-apply the style variants.
- `KitGallery` (a bootstrap scene component, dev only): draws every DS component and every primitive at known
  positions on the 1920×1080 stage, next to which the reviewer puts a Chrome screenshot of the same layout (a JS
  `dev/kit-gallery.html` built from the DS gives the reference). This is how §4.9's calibrations and §4.12's spike
  are checked.

---

## 5. Implementation units

| unit | files | ≈ lines | depends on |
| --- | --- | --- | --- |
| K1 Geometry: paths and meshes | `$PORT\Trial.Geometry\`: `Vec2.cs`, `RectF.cs`, `Affine.cs`, `Rgba.cs`, `CssColor.cs`, `ColorFilter.cs`, `Ease.cs`, `PathData.cs`, `Flatten.cs`, `Triangulate.cs`, `Stroke.cs`, `MeshBuilder.cs`, `Paint.cs` (Solid), `Blur.cs`, `Data\DsIcons.cs`, `Data\ExploreIcons.cs`, `Data\ExtraIcons.cs`, `Data\DsFigures.cs`; csproj; `$PORT\Trial.Geometry.Tests\` (parser, flatten, stroke/dash, triangulate over every shipped path, colour mix, eases) | 2900 + 900 tests | — |
| K2 Geometry: CSS box, paints, layout | `Tokens.cs` (DsTokens, DsTheme, DsLayout), `Gradients.cs`, `Shapes.cs`, `Box.cs` (BoxStyle, borders incl. dashed/dotted/double/per-side, outline, BoxShadow outer/inset), `Hatch.cs`, `Keyframes.cs` (CssKeyframes ×20, CssAnimation), `TextMath.cs`, `Flex.cs`, `Grid.cs`, `CssParse.cs`; tests (gradient line length, shadow profile, dash distribution, hatch phase, flex/grid, parser round trips) | 2400 + 800 tests | K1 |
| K3 Kit runtime (Unity) | `$KIT\Trial\Kit\`: `Trial.Kit.asmdef`, `TrialStage.cs`, `Render\GammaTarget.cs`, `Render\Materials.cs`, `Render\PaintTexture.cs`, `Ui.cs`, `UiNode.cs`, `Graphics\VectorGraphic.cs`, `BoxGraphic.cs`, `PathGraphic.cs`, `HatchGraphic.cs`, `GradientGraphic.cs`, `Text\TrialFonts.cs`, `TextStyle.cs`, `Label.cs`, `TextShadows.cs`, `TextGlow.cs`, `TextDecor.cs`, `VerticalText.cs`, `TextMeasure.cs`, `Wrap.cs`, `Layout\FlexBox.cs`, `Motion\TrialClock.cs`, `Motion\Tweens.cs` (Pop, Shake, ScaleTo, Styler), `Motion\CssAnimator.cs`, `Motion\Transitions.cs`, `Input\Hit.cs`, `Input\KeyStack.cs`, `Input\Focus.cs`, `Masks\FadeMask.cs`, `Masks\ScrollArea.cs`, `Dev\KitGallery.cs`; plus `$KIT\Trial\Common\Stage.cs` (stage.js port) and `CommonStyles.cs` (common.css) | 3300 | K1, K2 (text, stage, clock and input can start on K1's `Rgba`/`Ease` alone) |
| K4 DS port | `$KIT\Trial\Kit\Ds\Ocb.*.cs` (§4.14) | 1600 | K3 |

Order: K1 → (K2 ∥ K3-text/stage/clock/input) → K3-graphics → K4. The §4.12 gamma spike belongs at the start of K3,
because its answer changes the canvas setup every later unit builds on. Tooling edits (sync plan, slnx, fetch-fonts)
go to the architect's integration step.

---

## 6. Risks and gaps

Risks:

1. **Linear colour space** (§4.12): translucency, shadows and AA differ from the browser until the gamma target works.
2. **Group opacity**: CSS `opacity` composites the subtree first; `CanvasGroup` multiplies each graphic, so an opaque
   card face at opacity .4 (departure unowned cards, `meta.css:178`) shows its background through its text. Fix where
   visible with an offscreen capture (`GroupOpacity`: render the subtree once to an RT and show it at the alpha),
   otherwise accept for short fades.
3. **Text metrics**: TMP SDF vs Chrome's hinted text; line positions can differ by about 1 px; CardTextFit picks can
   change; `auto-phrase` and `balance` are heuristics; the vertical tagline's punctuation needs hand offsets.
4. **Large text glows** exceed the SDF padding and are approximated per glyph; ground halos on small text (6–8 px)
   come out as soft blobs rather than glyph-shaped halos.
5. **Japanese dynamic atlases**: first-use glyph rasterisation hitches and atlas growth (Alpha8 2048² = 4 MB per page);
   prewarm per scene from the bridge's text tables.
6. **Mesh rebuild cost**: animated box-shadows (beckon, stamp, pulse), hover borders and per-frame floats rebuild
   meshes; use transforms/alpha where possible and nested canvases per layer.
7. **Ear clipping** relies on simple polygons; a non-simple future path needs a real tessellator.
8. **Disabled domain reload**: every static cache (fonts, materials, RTs, key stack) needs a Unity null check and a
   `SubsystemRegistration` reset.
9. **Assembly boundary**: `Trial.*` asmdefs cannot see Assembly-CSharp (`UiKit`, `BattleTheme`, `ProceduralArt`,
   `UiTween`); nothing in the kit may depend on them.
10. **Dashed-border distribution** is Chrome-internal; ours follows the measured 3w/2w rule with gap stretching and may
    differ by a pixel per dash.

Gaps (nothing in the JS/DS or the C# decides them):

- `word-break: auto-phrase` (Chrome's phrase segmenter) and `text-wrap: balance/pretty` have no portable rule.
- `--keyline` is transparent in dusk: the keyline strokes exist in code but never show; keep them or drop them?
- The wakakusa themes and the DS backdrop/motes are unused by the game; port only the token values?
- `cursor: grab/grabbing/pointer`: the browser changes the cursor; Unity keeps the OS arrow unless the kit draws
  cursors (no art exists).
- `prefers-reduced-motion` has no Unity signal; only the 演出短縮 setting remains.
- `--flood` is not a token (fallback `#06090c` everywhere); it equals `BattleTheme.InkBlack`.
- The DS fixes 14 px for the card's 「自分」 tag and the game prints 17 px (`common.css:21-23`): the game wins.
- Mono 400 is used only at 14/16 px in key hints and one badge; fetching IBMPlexMono-Regular is cheap (≈140 KB), but
  the architect may choose 600 instead.
