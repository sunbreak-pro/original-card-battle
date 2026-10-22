# battle-views: hand / field / hud, from the browser game to Unity (Trial.*)

Analyst report for the architect and the implementers. Area: the battle scene's three view modules.

- JS root (`$G`): `scratchpad/proto/prototypes/browser-game/src/scenes/battle/` (absolute:
  `C:\Users\user\AppData\Local\Temp\claude\C--Users-user-orca-original-card-battle\1ab86662-d6cc-4831-aa8c-76f9592e0ca6\scratchpad\proto\prototypes\browser-game\src\scenes\battle\`).
- Contract: `browser-game/docs/BATTLE_SCENE.md` §2 (env, layers, clocks, geom), §6a–§6c (module APIs), §7 (scene texts).
- DS (`$DS`): `browser-game/vendor/ds/bundle.css` (component looks), `bundle.js` (OCB components, icons, figures), `tokens.css`.
- Unity today (`$U`): `trial/unity-port/unity-project-kit/Assets/View/Depiction/*.cs`, `Assets/View/Backdrop/`.
- Pure C# to reuse as is (do not re-port): `Depiction/Script/*` (HandFan/HandSpec, FloorSpec, ReceiverDrop, RefusalLineLayout,
  EnemyTurnShadeLayout, StanceGlow, StatusPanelLayout, StanceList, PileLayout, PileList, PlayedLayout, ReserveHintLayout,
  CornerLayout, HpWickSpec, FigureMotion (FigureFrame, EnemyMotionShape, FigureFacing, DefeatShape), Effects (EffectCatalog,
  EffectSwitches, EffectPlan), EffectGrammar, BattleSpeed, CardTextFit, DepictionTypes), `Backdrop/Script/*` (BackdropSpec,
  BackdropMotion), `Depiction/Bridge/*` (CoreText, CardFaceText, PileCards …).

**Who wins.** Look and feel (every px, colour, ms, easing, stacking, interaction) = the JS. Rules, numbers, words = the C#
cores and the ported Script/Bridge helpers. The existing Unity views (`CardView`, `FigureView`, `DepictionFx` …) are
reference only; the new `Trial` stack does not reuse them (decision 1), only their pure C# helpers.

---

## 0. Cross-cutting conventions for all three modules

### 0.1 Coordinates

- Stage = 1920×1080, origin top-left, y down (JS). In uGUI every Trial piece is a RectTransform with anchor = pivot =
  (0,1) on its layer, and `anchoredPosition = (x, -y)`. Recommend one helper `Stage.Place(rt, x, y, w, h)` (architect's
  env) so the implementers write the JS numbers unchanged.
- CSS `rotate(θdeg)` is clockwise on screen. Unity `localEulerAngles.z = -θ`. HandSpec's `Degrees` are already CCW
  (Unity sign): JS writes `rot = -p.Degrees`, so in Unity use `z = p.Degrees` directly.
- `transform-origin` must be reproduced with the RectTransform pivot (or a parent "origin" transform). Places that rotate
  or scale about a non-centre point: figure root/body (origin = feet point `(cx, height)`), guard badge (`14px 50%`),
  stamina number (`0 50%`), follow-up badge (`100% 0`), shield halves (`50% 90%`), ghost card (`0 0`), stance-fly ghost
  (`0 0`).

### 0.2 Layers and z-order (BATTLE_SCENE §2.1; `battle.css:28-39`)

One RectTransform per layer, full 1920×1080, `raycastTarget=false` by default, sibling order bottom → top:

| layer | owner here | contents |
| --- | --- | --- |
| backdrop (0) | field | sky, earth, hazes, shafts, motes, horizon |
| field (10) | field | floor tiles + gap chip, foot lights, figures. **This whole layer slides** (arena shift + arena shake) |
| fx (15) | field + hand | strikes, bursts, rings, crest, sigil, shield, impacts; the hand's target hooks + light |
| panels (20) | hud | status panels, omen badges (+plans), stance lists |
| marks (30) | hand + field | throw line; 俊敏 choice marks |
| hud (40) | hud + hand | corner, speed, journal, stamina tag, piles, played, reserve hint, end turn; the hand's guide line |
| shade (50) | field | enemy-turn shade, low-HP vignette |
| hand (60) | hand | the fan |
| held (70) | hand | held / returning / leaving cards |
| float (80) | field / hand / hud | floating numbers, prediction tags, refusal line, banners, ghosts, omen flight |
| over (90) | hud / hand | tooltips, hover line, reach panel, term tip, card detail, pile drawer, end scrim, result, chain banner, loss ink |

Within a layer, CSS `z-index` becomes sibling order (re-sorted when z changes) or nested sub-Canvases with
`overrideSorting`. Recommend: hand cards re-sorted every frame (cheap: ≤ 8 + leaving), figures re-sorted when their z
changes; floats always appended last.

### 0.3 Tokens (`$DS/tokens.css`, dusk theme is the only one the scene uses; no `data-theme` is ever set)

| token | value | token | value |
| --- | --- | --- | --- |
| ground | #10141d | panel | rgba(16,20,30,.88) |
| panel-solid | #141925 | card | #171c28 |
| ink | #ecebf2 | ink-muted | #a9adbd |
| rule | rgba(157,166,186,.26) | steel (= miss) | #6d7787 |
| amber | #e8a84c | wick | #ffd89a |
| crimson | #c2483f | hp-text | #e36a5e |
| violet | #8b6fd0 | reach-red | #ff1f4f |
| phosphor | #b9a3ff | guard | #8fc4d8 |
| boss | #d08cf0 | washi / washi-ink | #f0e6d0 / #1d1a16 |
| figure-self | #2b2f3a | figure-enemy | #1b1722 |
| keyline | rgba(16,20,30,0) (invisible in dusk: skip every keyline stroke) | flood (scene) | #06090c |

Shadows: `--shadow-card` 0 6px 14px rgba(0,0,0,.45); `--shadow-lamp` 0 0 0 2px #ffd89a, 0 0 20px 5px rgba(255,216,154,.5);
`--shadow-threat` 0 0 8px rgba(255,31,79,.3); `--shadow-reach` 0 0 22px rgba(232,168,76,.5); `--shadow-omen-active`
0 0 30px rgba(139,111,208,.6); `--shadow-tip` 0 8px 18px rgba(0,0,0,.45).
Radii: panel 6, plate 5, tag 4, chip-foe 3, chip-self 15. Strokes: rule 1, frame 2, icon 2.2, bold 3. Opacities:
unpayable .6, held .92, idle-hooks .75, unplayed .55, enemy-turn .4, guard-zero .7.
`tok(name, a)` in JS = `color-mix(in srgb, var(--x) a%, transparent)` = the token with alpha × a. `color-mix(A p%, B q%)`
with p+q < 100 = mix A:B in ratio p:q, alpha (p+q)/100 (used once, see §1.1 mods).

Fonts (TMP dynamic SDF from `Assets/View/Resources/Fonts`): display = Shippori Mincho Bold (CSS 700 and 800 both drawn
with the Bold file: no ExtraBold TTF exists), body = Zen Kaku Gothic New Regular/Bold, mono = IBM Plex Mono SemiBold
(600) / Bold (700). Plex Mono has no Japanese: give every mono asset a fallback to Zen Kaku (▼, ×, −, ▸, …).
`letter-spacing` (em) → TMP `characterSpacing` (TMP units are 1/100 em: `.02em` → 2, `.08em` → 8, `.12em` → 12).
DS base `line-height: 1.375` (`.ocb-root`) wherever no line height is given.

### 0.4 Time

Every animation runs on the scene clock (`env.clock`) with the nominal ms (EffectCatalog, ×1.25 speed). Eases
(`stage.js:80`, same as Unity `UiTween.Apply`): `out` = 1−(1−t)³, `in` = t³, `inout` = cubic in-out, `linear`, `smooth`
= t²(3−2t). `clock.run(ms, ease, fn(easedT))` calls fn every frame and once with 1; `ms = 0` (switched off) = end state at
once. `clock.runPaced` = run, but under fast-forward at most `L.FF_FLOAT_SCALE` (1.5) × battle speed (floats only).
Input timings (8 px drag threshold, 400 ms long press, 3.5 s refusal line, arena slide 220 ms, stance high-place
switch 150 ms, hover sound gap 50 ms) run on `env.real` (unscaled). Hover weights use `clock.paused ? dtReal : dtScene`.
Backdrop motion uses real time. The JS uses no CSS transitions in the scene (`battle.css:47-52` switches the DS ones
off) — the Unity port must not add any tween that ignores the scene clock.

In the tables below "ms" names an EffectCatalog id when it is one: CardDraw 260 (stagger 60), HandFan 120, CardHover
90, CardGrab 90, ReceiverShow 150, ThrowLineShow 150, ReceiverSnap 80, CardRelease 160, CardToDiscard 200,
CardToExile 220 (starts `CardToExileDelayMs 600 − CardRelease 160` after release), PileRebuild 200, CardReturn 200,
RefusalShake 160, HandDiscard 240 (stagger 60), UnpayableDim 120, AttackLunge 100, EnemyLunge 100, StrikeShape 120,
HitStop 30, HitStopStrong 90, HitFlash 260, TargetRecoil 260, DamageNumber 1000 (the JS float itself is 600),
HpDrain 300, HpTrail 500, ScreenShake 180, AttackReturn 120, EnemyMotion 220, OmenSpend 200, EnemyReturn 140,
GuardBlock 320, GuardNumber 1000, GuardBreak 200, GuardGain 260, OmenShow 240, TraitFire 200, StaminaChange 220,
StanceCue 460, FloorMove 160/cell, GapRecount 150, SideBonusMiss 260, GrammarMark 300, TraitAccent 150,
BreathHeave 400, StanceGlow 300, StatusApply 200, StatusStack 150, StatusTick 120, StatusVanish 150, OmenBlink 250,
OmenExecute 200, PushMark 1000, TurnBanner 300, EnemyTurnBanner 400, StaminaRecover 80/pip, ReservePips 250,
ResultCard 400, Defeat 800 (`Script/Effects.cs:518-590`).

### 0.5 Module shape in C#

Each JS factory `createX(env, opts) → api` becomes `public sealed class X` with `static X Create(BattleEnv env, XOptions o)`
and PascalCase methods with the same names and argument order. A JS Promise becomes the architect's awaitable beat
handle (call it `Beat`; it must never complete after `Destroy`, BATTLE_SCENE §2.2). Every method must accept `ms = 0`
and settle at once. Every module registers its `env.geom` getters in `Create` and only reads the others lazily.

---

## 1. Hand (`hand.js` 1229, `hand-card.js` 401, `hand-marks.js` 282, `hand-panels.js` 419, `hand.css` 64)

### 1.1 Visual elements

#### A. The card face (`hand-card.js:254-318` buildCard, DS `bundle.css:15-52`, scene `hand.css:11-20,54-64`)

216×304, all positions from the card's top-left. Built once per face key (`faceKey`, `hand-card.js:352`); the lamp, the
cost circle, the dim sheet and the status marks change in place.

| part | geometry | look |
| --- | --- | --- |
| body | 216×304, radius 6 | fill `card` #171c28; shadow 0 6 14 black .45 (offset y +6 down, blur 14) |
| attribute bands | left band x0 w8 full height; top band y0 h5 full width; both clipped by the card's 6 px corner | colour `--attr`: attack violet, defense(Guard) guard, skill amber, stance crimson, move/other steel |
| edge | 2 px border, radius 6, over everything but the dim? (order: clip, cost, icon, reach, name, values, desc, trait, **dim**, **edge**) — the edge is drawn over the dim sheet | `--attr`; lit → wick |
| lit glow | outside the card | `--shadow-card` + `--shadow-lamp`: a solid 2 px wick ring just outside the edge (spread 2, blur 0) and a 20 px blur / 5 px spread glow of wick at .5 |
| cost circle | (16,11) 44×44 circle | 2 px amber ring, fill black .45, number 700 28 mono amber centred; lowered (`CostDrop>0`): fill amber, number in `ground` |
| ▼n tag | centred on (58,53) (cost box (42,42), `hand.css:18`), h 18, padding 0 4 | fill panel-solid, 1 px amber, radius 3, text 400 14/16 body ink, nowrap. Text `"▼"+drop` |
| attribute icon | (68,21) 24 px | DS icon: attack `sword` phosphor, Guard `shield` guard, stance `flag` hp-text, skill/move `star` (skill amber, move ink) |
| reach tag | right 12 (x ends at 204), top 11, h 32, padding 0 8, gap 4 | fill panel-solid, 1 px steel, radius 4, text 700 20 mono ink; content `range` icon 18 + `RequiredRangeGlyph`; self card: `person` icon 22 + `自分` 400 14 body; none: no tag (`reachPart`, `hand-card.js:140`) |
| name | (16,60) w 188 h 28 | 700 22/28 display, letter-spacing .02em, nowrap, ink; size from `CardTextFit.Name(name, 22, 14, 188, 28, measure(700 display, pitch 28/22))`; text = fit's text (no wrap) |
| values | (16,94), flex row, gap 14, ≤ 2 columns | column = grid [24 px icon][auto]: icon 24 in value colour vertically centred on row 1 (40 px), number 700 36/40 mono value colour, word 400 16/18 body ink-muted under the number (col 2, margin-top −2). Value icon/colour (`hand-card.js:40-59`): Power sword phosphor; Guard shield guard; Heal heal hp-text; Close/Push forward ink; Back/Pull back ink; else draw ink. A v4.2 `ValueText` alone → one column (Power or Guard by Kind) |
| status marks | one value: a column right after it (flex item, margin-left −4, align-self centre, gap 2); two values: absolute right 6, top 92, right-aligned, z over | each mark: inline row h 20, padding 0 3, gap 2, radius 4, fill rgba(67,70,81,.88) (= mix card 70 % + ink 18 %), outline 1 px ink .25; text 600 16/20 mono ink (`→` / `×1.5` / `−3`) + status icon 16 ink (`focus` / `empower` / `intimidate`) (`modsOf`, `hand-card.js:173`). Hidden when none |
| description | (16,156) w 188 h 66 (3 lines); lasting face: (16,94) h 88 (4 lines) | body 400, ink; size and line breaks from `CardTextFit.Description(rows, 16, 14, 188, boxH, measure(400 body, pitch 22/16))`; line height = size × 22/16; `white-space: pre`, overflow hidden; a lasting row's leading `CardTextFit.LastingMark` (∞) in hp-text |
| trait box | (12,235) 197×62 (left 12, right 7, bottom 7) | fill black .34, 1 px rule, radius 4, rows vertically centred; lamp slot 26 wide at the left (lamp centre (26,266)); row 1 400 16/22 body, row 2 700 16/22, nowrap, ink; each row shrunk by `CardTextFit.Name(row, 16, 14, 161, 22, …)` (plain: width 175). Plain (`face.Plain`): dashed 1 px steel border, no lamp slot, padding-left 12, both rows 400 ink-muted. v4.2 `TraitText` → row 1 only |
| lamp | 10 px steel circle; lit 16 px wick + glow 0 0 10 rgba(255,216,154,.8) | lit = `card.lit` (`setLit`: only when the face has a non-plain trait box) |
| terms | per `CardFace.Terms` mark on a trait row: span underlined | dotted underline 2 px ink-muted, 4 px under the baseline (`hand.css:20`) |
| dim sheet | 216×304 radius 6, black | opacity `dimA` (0 … `--opacity-unpayable` .6) |

Released-card trait word (`traitSignal`, `hand.js:504`; `hand.css:54`): a tag child of the trait box, left 0, bottom
6 px above the box's top: padding 2 10, fill panel-solid, 2 px wick border, radius 4, text 700 20/26 body ink, nowrap,
glow 0 0 12 amber .45. It leaves with the card.

#### B. Fan, hover, held (`hand.js:115-228`, `hand.css:7`)

- Card wrapper 216×304, pivot centre. Pose `{x, y, rot, s, a}` (centre px, CSS degrees, scale, alpha) + `shake` (px,
  added to x).
- Rest place: `HandSpec.Rest(n, i, HandFan.Spacing(n, 160), 2.5)` → centre (X, Y), `rot = −Degrees`. Spacing 160 for ≤ 5,
  150 / 140 / 126 for 6 / 7 / 8+. Middle card top y 896; card k steps out sits k²·3.2 lower; tilt 2.5°/step about the
  bottom-middle.
- Hover place: `HandSpec.Hover(n, i, spacing)` → centre (same x, 912), upright (top at 760). Size never changes.
- Fan pose = lerp(rest, hover, smooth(clamp(hw))) for x/y, `rot·(1−w)`.
- Z (`render`, `hand.js:207`): 10+i; hovered 250; selected 300; settling (hw > .01) 200 + round(hw·10); returning 400;
  leaving 450. Leaving/returning/held cards are re-parented to the `held` layer; back to `hand` when they land.
- Held card: alpha `L.HELD_ALPHA` .92, scale 1, upright over CardGrab (90, out), keeps the grabbed offset; drawn on
  the `held` layer.
- Dim: per card, black sheet alpha → .6 (or 0) over UnpayableDim 120 linear (`setDim`, `hand.js:829`).
- Battle over: the whole hand layer → opacity .35 over `ms` (out) (`fadeAll`).
- Cursor: `grab` while a card is hovered and the hand is interactable, `grabbing` while held (`hand.js:797`). Optional in
  Unity (needs runtime cursor textures).

#### C. Hold decorations (`hand-marks.js`)

- Target frame per framed figure (`boxOf`, `hand-marks.js:72`): x = figure.x − width/2 − left, w = width + left + right,
  top = figure.top − 22, bottom = feetY + 12. left = right = 46, except toward a standing neighbour enemy: min(46,
  max(2, room/2 − 6)) where room = gap between the two bodies (`FRAME_APART 12`, `OUT_MIN 2`). Follows the figure every
  frame (rebuild when the rounded w/h change).
- Hooks (DS `TargetFrame`, `bundle.js:507`): four L-corners, legs 26 px, stroke 3 px (steel frame 2 px), square caps,
  colour wick (reachable) or steel; reachable hooks wear `drop-shadow(0 0 10px rgba(232,168,76,.35))`. Several enemies,
  the focused steel frame: stroke ink-muted 3 px (`hand.css:26`). The keyline under-stroke is invisible (skip).
- Frame light (reachable only; several enemies: only on the frame in focus): a box 10 px outside the frame on every
  side, `glowGradient(amber, .35)` = radial closest-side, alpha .35·(1−r)² sampled at r = 0, .25, .5, .75, 0 at 1.
  Drawn first, under the hooks. Layer: `fx`.
- Frame opacity: fade `a` 0→1 over ReceiverShow 150 (single) / ThrowLineShow 150 (self), out. One frame: a·(hot ? 1 :
  .75). Several: focused a·(hot ? 1 : .9), others a·.6.
- Prediction tag (DS `PredTag`, `bundle.css:147`): layer `float`, centred on (frame centre x, frame top y); h 60,
  padding 0 14, gap 8, fill panel-solid, 2 px wick border, radius 6, text 700 48 mono wick, nowrap; icon 32 by
  `previewKind(face)` (`hand-card.js:68`: self card Guard→Heal→Draw first; enemy card Power; else Values[0]; none:
  Guard/Power by Kind) in that value's colour. Shown only on a reachable frame with a non-empty preview. Several
  enemies: a tag on every reached frame; frames not in focus: `grayscale(.85) brightness(.62)`, opacity a (focused:
  same as the frame). 重撃 lit on this enemy (`heavyLitOn`): append a 24×24 mark: dashed amber circle r 8 stroke 2
  (dash 3 3) + ink slash 4,20→20,4 width 2.4 round caps.
- Throw line (self cards; DS `.ocb-throwline`): layer `marks`, x 400 → 1520, centred on y 790 (top 788.5), 3 px tall,
  dashes amber 9 on / 6 off; opacity a·(hot ? 1 : .6).
- The receiver is invisible (340×340 over the enemy's home: `geom.receiver(s)`, field's).

#### D. Panels around the cards (`hand-panels.js`)

| piece | where | look |
| --- | --- | --- |
| guide line | layer hud, full width, top 743, h 28, centred x 960; slides to stay 16 px inside an open drawer (pile list left edge `geom.drawerRight()`, journal `geom.journalEdge()`) | 400 22/28 body ink, nowrap, text-shadow 0 1 3 ground + 0 0 8 ground; text = `refreshPlayable`'s guide, blank while a refusal line shows; alpha steps toward 0 (away) or 1 at dt/120 ms (scene dt) |
| refusal line | layer float, top 836 (`RefusalLineLayout.Top` = 884 − 48), h 48, centred on x 960 | DS `.ocb-refusal`: padding 0 16, gap 10, fill panel-solid, 1 px steel, radius 6, 400 20/28 body ink nowrap; amber dot 16. Stands 3.5 s real time, counting only while shown; fades to 0 at dtReal/120 while a raised card (hw > .3, not the one whose press caused it) overlaps it, back after |
| hover line | layer over; above the raised card: left = clampX(card centre, 420) − 210 (16 px from the edges), top = card box top − 8 − height; then slid sideways to keep 4 px off every number panel (`makeClearOfPanels`) | DS `.ocb-hoverline`: w 420, min-h 48, padding 10 14, gap 10, fill panel-solid, 1 px steel, radius 6, 400 18/26 body; lamp 10 steel / 16 wick + glow when the card is lit, hidden for a plain card; text pre-broken by `wrapLine(text, 366, 18)` (`hand-card.js:325`) |
| reach panel | layer over; right of the card (x = box right + 12) unless that passes x 1620, then left (box.x − 12 − 280); top = box top | DS `.ocb-reachpanel`: 280×76, padding 12 16, gap 12, fill panel-solid, 1 px **dashed** steel, radius 6, 400 18/26; `range` icon 22 ink; text split after `が ` into two lines |
| term tip | layer over; centred over the underlined word, clamped 16 px, bottom 8 px over the word | DS `.ocb-tip`: w 340, padding 10 12 12, fill panel-solid, 1 px steel, radius 6, shadow-tip; head 700 18/24 body (margin-bottom 6) = `mark.Word`; p 400 16/22 = `mark.Meaning` |
| card detail | layer over; placement §1.3 | DS `.ocb-detail`: w 560, min-h 280, max-h 600, padding 16, gap 16, row; fill panel-solid, 2 px steel, radius 6, shadow-tip. Left: 173×243 slot with the card face at scale .8 (origin top-left). Right column (gap 6): h3 700 24/32 display (`Detail.Name`); meta 400 16/22 ink-muted (`KindLine`); body 400 16/22 ink (max-h 264, cut with 「…」); 「今」 rows (margin-top 8, gap 2, 16/22 ink, head span min-w 72 + 8 margin, ink-muted): 離す先 / 今の判定 / 予測値 (`nowPart`, `hand-card.js:387`); terms box at the bottom (margin-top auto): padding 8 10 + 8 transparent bottom, fill black .35, radius 4, 16/22, rows `<b>Word</b>：Meaning`, max-h 330, whole definitions or a 「…」 cut (`fitDetail`, `hand-panels.js:117`) |

### 1.2 Hand animations

| what | property | from → to | ms / ease | notes |
| --- | --- | --- | --- | --- |
| hover lift | hover weight hw | moveTowards 0↔1, step dt/90 | CardHover 90, linear weight, place eased smoothstep | targets 1: hovered, selected, pressed, detail-open card |
| long-press return | hw | → 0 at dt/200 | 200 (scene dt) | card stays down until the pointer leaves it |
| grab | rot | tilt → 0 | CardGrab 90 out | alpha .92 at once |
| snap pull | snapW | moveTowards toward 1 (hot single) or 0, step dt/80 | ReceiverSnap 80 | pos = lerp(pointer pos, receiver centre, .35·smooth(snapW)) |
| zone fade | frames / throw line alpha | 0 → 1 | ReceiverShow / ThrowLineShow 150 out | |
| draw | pose from pile middle (64,990) s .3 a 0 rot 0 → rest s 1 a 1 | | CardDraw 260 out, card k starts at `EffectPlan.DrawCardStartMs(k, rebuildAfter)` = k·60 (+ PileRebuild 200 for k ≥ rebuildAfter) | sound `draw` per card at its start; `onRebuild` at `RebuildStartMs` = rebuildAfter·60 |
| discard all | → discard middle (150,990), rot 0, s .3, a 0 | | HandDiscard 240 in, card i starts at 60·i | sound `discard` per card; then clear |
| release (single) | → receiver centre (`to`), s .5, a 0 | | CardRelease 160 in | `L.RELEASE_SCALE` .5 |
| release (self) | → y − 140, s .5, a 0 | | CardRelease 160 in | |
| release (stance) | → stance slot centre (`geom.stanceSlot('Player')`), rot 0, s 76/216 (.352), a stays 1 | | 300 (`L.STANCE_FLY_MS`) out | flow waits CardRelease 160 only; card kept until its ghost leaves for the exile pile |
| lamp swell | lamp scale | 1 → 1.4 → 1 (1+.4·sin πt) | 150 out | `o.traitWord` also adds the word tag |
| settle | rest of the fan → new rest | | HandFan 120 out | |
| return to hand | pose → rest (x,y,rot,s 1) | | CardReturn 200 out | then a = 1, lamp back to the face's |
| refusal shake | x offset | sin(t·2π·2)·8·(1−t) | RefusalShake 160 linear | only when `shake` |
| dim | sheet alpha | ↔ .6 | UnpayableDim 120 linear | token-guarded per card |
| auto drag | pos | lerp from → to | 450 inout (`L.AUTO_DRAG_MS`) | self: to.y = 790 − (from.y − 790)·.35 |
| fade all | hand layer opacity | current → .35 | ms out | |
| guide fade | alpha | step dt/120 | 120 (scene dt) | |
| refusal fade | alpha | step dtReal/120 | 120 real | |

Sounds the hand plays (`env.audio.play`): `hover` (not again within 50 real ms), `grab` (hold / select / re-take an
auto-selected card), `frameIn` / `frameOut` (zone hot on/off), `noReach` (instead of frameIn over a single card with a
ReachHint), `cancel` (Esc, selection dropped, unplayed slide home), `refusal` + `refusal` at +70 ms (refused and shaken),
`releaseAttack` / `releaseDefense` / `releaseStance` / `releaseSkill` by `face.Kind` (release), `draw`, `discard`.

### 1.3 Contract (BATTLE_SCENE §6a; `hand.js:844-1221`) and what Unity's views do differently

Options: `onRelease(cardId, zone, unit?)`, `onHover(face|null)`, `onHoldStart(face, unit)`, `onHoldEnd(face)`,
`onTarget(face, unit)`, `preview(cardId, unit) → string`, `restingUnit() → int`.

| method | behaviour (JS wins) | Unity today |
| --- | --- | --- |
| `SameHand(faces)` | same ids in the same order | same (DepictionPlayer) |
| `Build(faces)` | clear, new cards (born under the current dim sheet with no fade), `Layout()` | BuildHand from prefab |
| `Bind(faces)` | same ids: rebind each face in place (new face only when `faceKey` changed; lamp/cost/mods re-applied) | rebinds whole CardView |
| `Layout()` | every free card home at once (mode fan, hw 0, shake 0, back to the hand layer), hover dropped | LayoutHand |
| `Clear()`, `Count()`, `Faces()` | | |
| `SetInteractable(on)` | off: let go a held card, drop selection / press / hover, hide panels and refusal | similar |
| `RefreshPlayable(dimOf, guide)` | dim fades per card (skips the held card and the one being handed over); guide text | Unity dims on any refusal and lights all while busy; JS rule: player computes `dimOf` |
| `SetStatusMarks({focus, empower, intimidate})` | marks on the faces (§1.1 A) | none |
| `IsHolding`, `HeldFace` (held or selected), `HoveredFace` | | |
| `FlyDrawn(first, rebuildAfter, {from, onRebuild})` | §1.2 | Unity FlyDrawn: same timing, no scale from .3 |
| `DiscardAll(to)` | §1.2 | FlyDiscarded |
| `Release(cardId, ev, to, o)` → `{Done, VanishedAt(), Face, Keep}` | removes from the list at once, others keep their places until `Settle`; release sound; trait swell + word; stance card flies to the slot and is kept | Unity tosses every self card up (no stance slot flight, no trait word) |
| `GhostCard(face)` | a fresh card face for the field's ghost | Unity flies a plain sheet |
| `DropCard(cardId)` | destroy a leaving card | |
| `Settle(ms)` | §1.2 | SettleHand |
| `ReturnToHand(cardId, shake)` | §1.2 + sounds | same shake 8 px ×2 |
| `LetGo()` | held card home at once, decorations gone; drops selection and press | LetGo |
| `ShowRefusal(text)` / `HideRefusal()` | §1.1 D; remembers the card whose press caused it | RefusalLineView: no step-aside, fixed 3.5 s |
| `Select(i, {auto})`, `Confirm()`, `Cancel()` | click/keyboard path (§1.4) | none |
| `AutoDrag(cardId, zone, unit)` | hold → move 450 inout → hot → release through the same path | AutoDrag |
| additions | `HasSelection`, `DetailOpen`, `FramedUnit`, `SelectedIndex`, `CycleTarget(dir)` (Tab), `ToggleSelectedDetail` (I), `FadeAll(ms)`, `SetBlocked(on, hover, onTry)`, `SetGrabLock(fn)`, `TaggedUnits()`, `DebugState()`, `Destroy` | none |
| geom | registers `geom.handCard(cardId) → {x,y}` (cards and leaving cards) | |

Unity-side look differences to drop: legacy `Text` with LegacyRuntime font; MinFontSize 12 (JS 14); lit = Glow sprite 28 px
out at wick .5 + 2 px ring (JS: `--shadow-lamp`); hover line min-height 44 and a 12 px dot at x 20 (JS DS hover line);
detail fixed 280 tall, closed when the hover moves, placed straight above (JS keeps it until Esc/click/right click, grows
to 600, places it per §1.4); prediction tag parented to the figure (JS: float layer + badge lift); one enemy only.

### 1.4 Interaction details (JS is the spec)

- **Pointer → stage px.** One pointer state per frame from the scene (Input System `Pointer.current`), converted
  through the letterboxed canvas to stage px. The hand hit-tests cards by math, not by uGUI raycasts:
  `CardUnder(p)` (`hand.js:244`): none when the pointer is over another module's piece (`IsForeign`) or
  `geom.coversPointer(p)` (open pile drawer / tooltip); the selected card first (its rest OR hover rect), then the
  hovered one (rest OR hover rect), then right-to-left by rest rects. A rect test is a point-in-rotated-rect
  (`inRect`, 216×304 about the centre).
- **IsForeign (`hand.js:587`)**: the top uGUI raycast hit belongs to layers hud / over / panels (buttons, chips,
  drawer) — except (a) an enemy's panel while a card is selected (`partOfEnemyAt`), (b) the journal veil outside the
  journal drawer (`geom.journalCovers`). In Unity: `EventSystem.RaycastAll` once per pointer event, check the hit's
  layer.
- **Can hover**: interactable, not blocked (or blocked with hover allowed: the journal), nothing held, no press, no
  selection, not busy (a card returning), not destroyed. **Can grab**: interactable, not blocked, no grab lock, nothing
  held, not busy, detail not locking.
- **Press** (left button): closes a locked detail first (a press on the scene's own ground then does nothing more). If
  the hand is blocked by the pile list or the journal → `onTry` (refusal line); with a grab lock (俊敏) → lock callback.
  Else a press on a card arms `down {card, x, y, token}` and starts a 400 ms **real** timer: still pressed and not
  dragging → `longRest = card`, hover panels hidden, card sinks to rest over 200 ms (scene), then the detail opens.
  A press elsewhere arms `bgDown`.
- **Drag threshold**: 8 stage px from the press (real, not scaled) → `BeginHold` as if the pointer had left from the
  press point (offset kept). A release ≥ 8 px from the press with no move in between is also a drag. A long press that
  then moves 8 px becomes a drag (the detail closes).
- **BeginHold** (`hand.js:401`): ends a selection, drops hover/panels, settles other cards (hw 0), card → held layer,
  alpha .92, `grab` sound, upright over 90, `lift`: lamp/cost read for the framed enemy (`CardFace.LitFor(u)`,
  `CostFor(u)`/`CostDropFor(u)`), marks shown with `preview(cardId, unit)`, `onHoldStart(face, unit)`.
  Base enemy = `restingUnit()` if it still stands, else `CardFace.TargetUnit`, else 0.
- **Every frame while held** (`updateHold`): card pos = pointer + offset; `ZoneAt` with the card's AABB (rotated card
  bounds) and the pointer: single → the receiver under the pointer, else the first whose `ReceiverDrop.Takes(cardBox,
  receiver, px, py)` (≥ 50 % of the card on it, or the pointer inside); self → `p.y < 790` (AboveThrowLine). Framed
  unit change → `marks.SetTarget(u)`, lamp/cost re-read, `onTarget`. Hot change → `frameIn`/`frameOut`/`noReach`. Snap
  pull as §1.2.
- **Release**: zone measured where the pointer carries the card (before snap) → `onRelease(cardId, zone, unit)`; the
  player calls TryPlay; refused → `ReturnToHand(shake = verdict ≠ WrongZone && zone ≠ None)`. Nobody took it → slide
  home without sound.
- **Click path** (`hand.js:513-561, 702-728`): a click on a card selects it (lifted to the hover place, hold
  decorations shown, does not follow the pointer; `grab` sound). Click the selected card again → drop it (`cancel`
  sound) unless it was auto-selected (then it becomes the player's: `grab`). A click (moved < 8 px) on an enemy's
  receiver, its target hooks, its omen badge or number panel (any enemy: `partOfEnemyAt`) or above the throw line for
  a self card plays it there (`confirmOn(unit)`); anywhere else drops the selection. While selected, no other card lifts
  on hover; the frame under the pointer becomes the framed one unless a Tab focus holds (until the pointer moves 8 px).
- **Keys** (scene routes): 1–9 `Select(i)`; Enter `Confirm()` plays on `FramedUnit()`; Tab/Shift+Tab `CycleTarget(±1)`
  (receivers sorted left→right, wraps; marks hot); I `ToggleSelectedDetail()`; Esc `Cancel()` (held → home with
  `cancel`; else selection; else detail).
- **Right click** on a card (interactable, not busy, nothing held) toggles its detail; elsewhere closes it. The browser
  menu is suppressed (Unity: nothing to suppress).
- **Detail lock**: while a detail opened by right click / long press is up, cards lift on hover but none is taken or
  selected; any click closes it. The selected card's own detail (I) locks nothing. Detail placement (`placeDetail`,
  `hand-panels.js:369`): (1) straight above the card (8 px gap, centred, 16 px from edges) if it covers no number
  panel, badge, 俊敏 mark or the floor row (y 560–600 full width); (2) else bottom 4 px over the floor top (top = 556 −
  height) at the card's centre if no badge/mark is in the way and top ≥ 84; (3) same height slid just clear of the
  badges in the way; (4) else Unity's place raised to clear the number panels (top ≥ 84). Height = whole content (280 …
  600).
- **Hover line clear of panels** (`hand-panels.js:59`): at its Unity place, if it overlaps a number panel (4 px gap) in
  its row: try the smaller slide (left of the panels / right of them) keeping the card's centre under the line and 16 px
  from the screen edges; else the x over the card's middle that covers the least panel area, chips counted ×4
  (`statusBoxes()[].box.chipsW`).
- **Guide step-aside**: alpha → 0 while a hovered card's hover line / reach panel / detail is up, or a card is
  selected; stays under a held card.
- **Refusal ownership**: `ShowRefusal` records the pressed / hovered / selected card; that card does not push the line
  aside.
- **Lost release**: window focus lost, app hidden, or a mouse move with no button down while holding/pressing → drop the
  press, send a held card home (`Cancel`). Unity: `Application.focusChanged`, and check `Mouse.current.leftButton` each
  frame.
- **Blocked** (`SetBlocked(on, hover, onTry)`): journal (hover allowed, real-time hover), pile list (press/digit →
  `onTry` refusal), settings modal (nothing). **Grab lock** (`SetGrabLock(fn)`): 俊敏's choice; hover still lifts.
- Several enemies: one receiver per standing enemy (`geom.receivers()`, cut at the midpoint between neighbours); every
  standing enemy framed (amber where `face.ReachFor(u)`), each reached frame with its own `preview(cardId, u)` tag; the
  framed one in focus.

---

## 2. Field (`field.js` 1062, `field/art.js` 238, `backdrop.js` 193, `floor.js` 451, `freestep.js` 222, `fx.js` 811, `field.css` 113)

### 2.1 Backdrop (`field/backdrop.js`, `field.css:7-15`; data `Backdrop/Script/BackdropSpec.cs:166-265`)

Children of layer `backdrop`, bottom → top (order matters: the earth is under the hazes):

1. **Sky**: full 1920×1080 vertical gradient through `spec.Sky` stops (`At` 0 top → 1 bottom), sRGB.
2. **Earth** (DS addition): x 0, y 556, 1920×524, vertical gradient ground #10141d at α .80 → α .92.
3. **Hazes**, each `spec.Hazes[i]`, alpha × `BackdropMotion.FlickerAlpha(haze.Flicker, realMs)` (rewrite only when it
   moves ≥ .004):
   - TopGlow: full screen, colour at the top → 0 at `Size`·1080.
   - Band: transparent at (Y − Size/2), colour at Y, transparent at (Y + Size/2) (fractions of 1080).
   - Radial: square `Size·1080` centred on (X·1920, Y·1080), radial closest-side colour → 0 (round).
   - Shafts: one 1920×1080 surface: `Count` shafts, shaft i centred at x = X·1920 + (i − (Count−1)/2)·0.18·1920, y 540,
     width `Size·1920`, height 1.6·1080, rotated +Angle° (clockwise on screen; Unity z = −Angle), horizontal profile
     0 → colour at 25 % → colour at 75 % → 0; then the whole surface masked by a vertical fade 1 at y 0 → 0 at y 560.
     Only its alpha animates. (Unity's BackdropLayers has no 560 px fade: JS wins.)
4. **Motes**: `Count` round specks `SizePx`, radial closest-side colour → colour×.6 alpha at 60 % → 0; centre at
   `BackdropMotion.MoteAt(m, i, realMs, 1920, 1080)`, alpha `MoteAlpha(m, i, realMs)`.
5. **Horizon**: y 555, 1920×1, ink at .22.

演出短縮 (`settings.shortFx`): everything stands still — hazes at `FlickerAlpha(f, f.Flash ? 140 : PeriodMs/4)`, motes at
time 0. Battle speed / fast-forward do not touch the backdrop.

Per-layer values (BackdropSpec, verbatim):

| layer | sky stops | hazes | motes (count, colour, px, speed x/y px/s, twinkle) | tile |
| --- | --- | --- | --- | --- |
| 1 燦光の樹海 | 0 #d6e8a4, .4 #7cab4a, 1 #22391c | Shafts white .28, X .42, Size .05, Count 4, Angle 8, flicker 6000/.25; TopGlow #f4f8dc .30 Size .5 | 24, #f4f8dc .35, 4, 6/10, .5 | rgba(10,18,12,.82) |
| 2 ■■の秘跡 | 0 #b8a684, .5 #8a7a5e, 1 #4a4034 | Shafts white .25, X .3, Size .06, Count 3, Angle 18, 7000/.2; Radial #b9a3ff .12, X .5 Y .95 Size .5, 4200/.3 | 20, #e8dcc0 .25, 3, 14/4, .2 | rgba(16,13,10,.82) |
| 3 大黒蛇の住処 | 0 #1e1a3a, 1 #0b0a18 | Radial #b9a3ff .10, X .78 Y .25 Size .6, 3200/.4; TopGlow #8b6fd0 .07 Size .6 | 12, #b9a3ff .35, 3, −3/−6, .7 | rgba(12,14,28,.78) |
| 4 輝晶の間 | 0 #e4e6f2, .5 #b7bbd6, 1 #7d82a8 | Shafts white .35, X .5, Size .05, Count 5, Angle 0, 5000/.2; TopGlow white .60 Size .45 | 20, white .5, 3, 0/3, .9 | rgba(14,16,28,.82) |
| 5 血染めの死海 | 0 #1a0e12, 1 #070507 | Band rgb(140,30,40) .28, Y .4 Size .7, 8000/.25 | 18, #efe6dc .2, 3, 8/−4, .3 | rgba(12,8,10,.80) |
| 6 獄雷峡 | 0 #120b1c, 1 #040208 | Shafts #8b6fd0 .22, X .62, Size .012, Count 1, Angle −12, flash 5200/1; TopGlow #8b6fd0 .05 Size .5 | none | rgba(8,6,12,.84) |
| 7 果てなき世界 | 0 #080a1a, 1 #020309 | Radial #8b6fd0 .22, X .5 Y .9 Size .9, 7000/.3 | 30, #8b6fd0 .3, 4, 0/−12, .5 | rgba(6,6,14,.84) |

Flicker: breath = 1 − depth·(.5 − .5 cos(2π·phase)); flash = lit in the first 140 ms of the period, 1 − depth·(lit/140),
else 1 − depth. Mote: x = wrap(hash(i,1)·W + speedX·s + 12·sin(.6·s + hash(i,3)·2π)), y = wrap(hash(i,2)·H + speedY·s);
twinkle beat 1.5 + 2.5·hash(i,4) s.

### 2.2 Floor (`field/floor.js`, `field.css:17-33`, DS `bundle.css:70-79`)

Root in layer `field` (slides with the arena). Floor top y 560, tiles 40 tall.

- **Tile c** (1-based, `FloorSpec`): x = `TileLeft(n, c)` = 300 + (c−1)·1320/n + 3, w = `TileWidth(n)` = 1320/n − 6,
  y 560, h 40, radius 6, fill = layer tile colour (table above), 1 px rule border. Children in order:
  1. reach fill: inset 0, radius 6, vertical gradient amber .55 → amber .18, box-shadow 0 0 22 amber .5; alpha `reachA`.
  2. threat box (inset 0) holding: the move frame (inset −1, radius 6, 2 px **dashed** violet) and/or the threat frame
     (inset −1, radius 6, 2 px solid reach-red, shadow 0 0 8 reach-red .3, clipped inside) with one hatch per threat:
     bars 3 px wide every 11 px (6 px on dense / boundary cells), reach-red, at the threat's angle, filling the frame's
     inside (see §2.6 hatch).
  3. reach top line: top 3 px, left/right 4 px inset, amber, radius 3 3 0 0; alpha `reachA`.
- Threat angle: one enemy and no hatch → 45 (`／`); several enemies or a hatch → `hatchAngle(EnemyHatch)` (45 / −45 /
  180 for "90 = upright", `layout.js:189`). Two enemies on one cell: two hatches crossing.
- Move frame on a cell another enemy aims at (`move-over`): drawn in front of the red frame, inset 4, with a 2 px
  panel-solid keyline on both sides (`field.css:24`).
- `focusAim(unit)`: while one enemy's badge is hovered (2+ enemies) the other enemies' hatches / move frames at .2.
- Unread enemies (`Disclosure < 1`): no aimed / dense / move cells.
- **Gap chip** (DS `DistanceChip` parts): box top 563, h 34, centred over the gap span (`FloorSpec.GapSpan`), width =
  100 at gap ≤ 0 else max(100, span); chip: h 34, padding 0 10, gap 6, fill panel-solid, 1 px steel, radius 6, 700 28
  mono ink, `range` icon 22 ink, number min-w 18 centred; at gap 0 the chip itself is 100 wide. Brackets (when gap > 0
  and (broken span or span > chip width + 8)): left arm from x 8 to the middle, right arm from the middle to w − 8, each
  a 1 px horizontal line at y 17 (ink .55) with a 17 px end tick (1 px, ink .55) at its outer end (top 9). z over the
  figures (sibling last). Broken span (another standing enemy between the hero and the counted enemy): brackets over the
  whole span, chip slid (`chipDx`) to the run of empty cells nearest the counted enemy (`gapPlace`, `floor.js:143`).
  Hidden once the outcome is decided.

Floor animations:

| what | value | ms / ease |
| --- | --- | --- |
| reach light in | reachA 0 → 1 per lit cell | 150 out |
| reach light out | → 0 | 120 linear |
| aim reveal (omen shown) | newly aimed cell k: clip-from-left 100 % → 0 after 30·k | 30 linear per cell |
| aim fade (stripes with nothing left) | threat box alpha → 0, then cleared | 120 linear |
| retire all (battle over) | all threat boxes alpha 1 → 0 | ms `in` |
| gap recount | box left/width, chip dx: from → to (eased); number: t < .5 old number up 14 px fading out, t ≥ .5 new number from +14 px fading in; brackets hidden until the end | GapRecount 150 out |
| wall edge flash | bar at x 300 or 1620 (−5), y 480, 10×120, radius 5, vertical gradient transparent → ink .55 → ink, glow 0 0 14 spread 4 ink .7: alpha 1 → .7 | ms (80) out, then .7 → 0 over 160 in |

### 2.3 Figures (`field.js:126-371`, `field/art.js`, DS FIGURES `bundle.js:583-639`)

- Silhouette per side: player `swordsman`; enemy by `ArtId` in VARIANTS (`art.js:87-140`), else `warden` (size ≥ 2) or
  `spearman`. Def boxes (w×h, body width, cx): swordsman 140×226 / 100 / 62; spearman 170×226 / 110 / 104; hound
  230×218 / 180 / 110; warden 220×264 / 180 / 120; SERPENT_DEF 220×234 / 184 / 124; root 220×264 / 170 / 110.
  Variants: polearm_warped = spearman; rusted_revenant + plates; crossbow_hunter / mist_archer / twin_blade_warped drop
  the spear (parts 7, 8) and add their weapons; polearm_crystal drops part 8, adds a crystal head; shadow_hound = hound;
  pack_alpha + mane; polearm_unyielding + shield (+ details); armored_warden = warden; miasma_priest / abyss_angler =
  serpent paths (+ lure); distortion_root own paths. Paths use M/L/H/V/C/S/Q/A/Z, absolute and relative, implicit
  repeats (`art.js` and `bundle.js` path strings — copy verbatim).
- Height `FigureFrame.HeightFor(size)` 226 / 264; k = height/def.h; body width = body·k, art width svgW = w·k, cx·k.
- Placement: feet y 570 (`L.FEET_Y`), x = `FloorSpec.FeetX(cells, cell, size)` (no nudge). Root transform
  translate(x + shake − cx, 570 − height + y) rotate(rootRot) about the feet point (cx, height).
- Children (bottom → top): foot light, stance glow, body { art, glowBody, flash }.
  - **Art**: each path filled `figure-self` #2b2f3a / `figure-enemy` #1b1722; edge light = 4 copies of the whole
    silhouette in the rim colour offset (±2, ±2) **screen** px drawn under the fill (player amber .28, enemy violet
    .35; overlapping copies compound); variant `details` stroked 2 screen px in the rim colour over the fill. Enemies are
    drawn facing left as authored (no mirroring; `FigureFacing.HomeScaleX(Enemy, wearsArt: true)` = 1).
  - **Foot light** (DS FootLight): ellipse width 2 × body, height 60, centred on (cx, feet line), radial closest-side
    amber .55 (player) / violet .35 (enemy) → 0.
  - **Stance glow** (halo): x cx − .7·body, w 1.4·body, y −.04·h, h 1.06·h; `glowGradient(crimson, .25)`; alpha glowA·alpha.
  - **glowBody** (body light): the silhouette filled with a radial gradient centred (cx, .4·h from the top), r .46·h,
    stops (0, 1) (.35, .7) (.7, .22) (1, 0) of crimson, **screen** blend, alpha glowA·alpha·.25.
  - **Flash**: the silhouette filled with the role colour (white → ink, omen → violet), alpha flashA.
  - Body filters: brightness `bright` (fall); several enemies: nearest wears `drop-shadow(0 0 1.5px rgba(139,111,208,.85))`
    on its art, each one further back `brightness(1 + .5·rank)` (1.5, 2.0).
- Paint order: hero z 9; enemies by distance to the hero, nearest 8, then 7, 6; the acting enemy (until home) 8; a
  walking enemy (2+ enemies) 2.
- Down: hidden whole. A chain's next enemy: every enemy figure's alpha `enemiesAppear` (`AppearEnemies`).
- Arena slide (`setShift(dir)`, `field.js:299-330`): layer `field` translateX(shake + shift.x). Wanted = max(15 % of
  1920 = 288, what keeps every standing figure's art 12 px inside the part a 30 % drawer leaves), limited so no figure's
  art leaves the screen on the other side; eased over 220 ms on **real** time with ease-out-quad 1−(1−t)²; re-evaluated
  every frame while a drawer is open.

Figure beats (all on the scene clock; "own(channel)": a newer run on the same channel stops the older one touching
the figure):

| method | motion | ms / ease |
| --- | --- | --- |
| `Lunge(side, px, ms, unit)` | pose Act; x += px toward the other side (enemy: limited to `roomToward` − 8 px before an ally's body) | AttackLunge / EnemyLunge 100 out |
| `ReturnHome(side, ms, unit)` | x → homeX, y → 0, then pose Idle | ms inout |
| `EnemyMotion(system, ms, unit)` | bell = sin(πt); x = from + reach·dir·t (reach = `EnemyMotionShape.Reach`: Thrust 90, Sweep 40, Strike 60, Shield 0, else 70, limited by `roomToward`); Shield: y = −16·bell; scale = `FigureFacing.During(1, system, bell)` (Sweep x 1+.08b; Thrust x 1+.14b y 1−.05b; Strike x 1+.12b y 1−.08b; Shield y 1+.14b); rot: Sweep dir·lerp(−18, 14, t)·bell, Strike dir·10·bell (CSS degrees, dir = −1 for the enemy); stays where it ended | EnemyMotion 220 out |
| `Recoil(side, strong, ms, unit)` | pose Hit; body rot = lean·sin(πt), lean 7° (12 strong), enemy +, player −; shake x = sin(t·2π·3)·amp·(1−t), amp 10 (20 strong) | TargetRecoil 260: rot out, shake linear |
| `Flash(side, role, ms, unit)` | flash overlay alpha 1 → 0 | HitFlash 260 out |
| `Heave(side, ms, unit)` | body y-scale 1 + .05·sin(πt) | BreathHeave 400 inout |
| `Fall(side, ms, unit, {keepFoot})` | rest body; rootRot = −`DefeatShape.Tilt(side, t)` (24° away from the other side), y −= `Sink` (40), alpha `DefeatShape.Alpha` (fade from 60 %), brightness → .35; then down (keepFoot: alpha 0, foot light kept) | Defeat 800 linear |
| `FadeFoot(side, ms, unit)` | foot alpha 1 → 0, then down | 300 (`L.FALL_FOOT_MS`) linear |
| `DimFeet(ms)` | every standing foot light alpha and scaleX 1 → 0 | 600 in |
| `Bump(side, dir, ms, unit)` | shake = d·12·sin(πt) | 40 (`L.MOVE_BUMP_MS`) inout |
| `ShakeArena(ms)` | field layer x = sin(t·2π·3)·6·(1−t) | ScreenShake 180 linear |
| `MoveOnFloor(cue, cells, ms)` | hero to `PlayerCellAfter`, moved enemy (`Cue.Unit` + `UnitCellAfter`; one enemy: `EnemyCellAfter`) to its cell; `walking` while moving (the panels ride along) | 160·cells; out when `Pushed`, else inout |
| `SnapOnFloor(cue)` | end state at once | |
| `SetStanceGlow / FadeStanceGlow(side, on, ms, unit)` | glowA → 0/1 | StanceGlow 300 out |
| `AppearEnemies(ms, from)` | enemies' alpha from → 1 | 400 out |

### 2.4 Fx catalogue (`field/fx.js`, `field.css:82-113`)

All in layer `fx` unless noted; every piece is centre-anchored at a stage point; "fade(ms)" = alpha → 0 with `in`,
then destroyed. Tier index = clamp(tier, 1, 4) (default 2). `SLASH_LENGTH` [300, 380, 460, 560], `SLASH_THICKNESS`
[8, 12, 16, 22], `MARK_SCALE` [.6, 1, 1.3, 1.7]. Colour names: the player's blow `ink`, an enemy's `violet`; `tok`
alpha as written. `IMPACT_IN` 36: the point of contact = chest x − dir·36 (dir +1 when the blow travels right).
Reach of a blow (`reachOf`): near = max(40, min(room toward the striker, natural)); far = max(40, min(room past the
body, half body + 48)) (`blowRoom`: 8 px short of any other standing figure but the striker).

| shape | geometry | colours | timing |
| --- | --- | --- | --- |
| **斬 Slash** (default system) | arc of a circle: R = max(60, min(natural·.42, near/.85, far/.85)); box (2R + 4t) × (1.6R + 4t) centred on the chest; from (cx − dir·.85R, cy − .38R) to (cx + dir·.85R, cy + .62R), SVG arc radius = .9 × chord, sweep toward dir; three strokes along it: glow width 2.6t at colour .35, core max(3, .9t) with a gradient along the chord (colour α 0 at the start → .95 at 35 % → 1 at 80 % → .2 at the end), edge max(1.5, .3t) ink .9; round caps; drop-shadow 0 0 8 colour .7 | colour | revealed along the path (dash offset 1000 → 0) over StrikeShape 120 out; then fade 200 |
| **突 Thrust** | a horizontal bar from x0 = chest − dir·near to x1 = chest + dir·far, height 3·th (th = max(4, .45·SLASH_THICKNESS)); core band th tall, gradient from the striker side colour 1 → .95 at 70 % → 0; glow band 3th tall colour .35 → .2 at 60 % → 0; drop-shadow 0 0 6 colour .8 | colour | scaleX 0 → 1 from the striker side over max(1, .6·ms) linear; then **impact** at the contact point (70 px, ms − line + 80) and fade 180 |
| impact | 70 px disc, radial ink 1 → colour .9 at 35 % → 0 | | scale .4 → 1.2, alpha 1 − t², `out` |
| **払 Sweep** | band x0 → x1 (near/far with natural .7·length), height 2.4t, rotated 6° (−6 when the blow goes right: CSS `rotate(tilt)`), vertical gradient: 0 → colour .55 (30 %) → core .95 (47 %) → ink .95 (50 %) → core .95 (53 %) → colour .55 (70 %) → 0, horizontal alpha mask 0 at the striker end → 1 at 22 %; drop-shadow 0 0 8 colour .75. Front: 14 × 1.6·th soft dot (ink → core .8 at 45 % → 0) riding the leading edge. Motes: 7 dots 5 px (core / ink .95), spawned as t passes k/7 at the front, each flies (−dir·(18 + 6(k%3)), −10 − 4(k%2)) px over 320 out, fading | colour; core = phosphor for an enemy's (violet) blow, else the colour | scaleX 0 → 1 over ms out; then front fade 120, band fade 200 |
| **打 Strike** (ripple) | ring at the contact point, size .5 × (.7·length), even stroke max(3, size·.04), colour .9, glow 6; five spikes (len (.7·length)/3, 5 px tall, radius 3, gradient colour .95 → 0) at angles −50, −25, 0, 25, 50 (mirrored 180 − a for a leftward blow) | colour | ring scale .3 → 1; spikes translate .15 → .45 of ring size, scaleX 0 → 1; over ms out; then ring fade 180, spikes 140 |
| **盾 Ring** (Mark, FxForm.Ring) | ellipse 110s × 230s at chest + 70·facing, stroke max(2, 4s) guard .95 with glow 7s guard .75; plus a burst (guard, 160s) | guard | scale (.6 → 1, 0 → 1) over ms out; fade 220. Rides with the figure (`trackX`) |
| **型 Crest** | at the feet: outer ellipse 280s × 72s, stroke max(1.5, 3s), glow 6s, filled with radial (warm ? amber : violet) .22 → 0; inner ellipse 170s × 44s, stroke max(1.25, 2s), glow 4s; outer colour amber .95 (player) / violet .95; inner wick .85 / phosphor .85; with light: glow 200s × 320s centred 140s above the feet, `glowGradient(amber/violet, .35)` | warm only on the player's side | outer scale .3 → 1, inner .6 → 1 over ms out; then all fade 600. Rides with the figure |
| **気 Sigil** | diamond 40s (square rotated 45°) from (head.x, head.y − 60) to the chest, eased `in` (t³); fill linear 135° ink .95 → phosphor .98 at 38 % → violet .95; glow 0 0 10s phosphor .85 + inset 2 px ink .55; a glow 3.2·size behind (`glowGradient(phosphor, .55)`); three trail copies (sizes .9, .78, .66 × size, phosphor .5/.36/.22) lagging .07/.14/.21 | | ms linear (position eased in); then burst (phosphor, 140s) at the chest; diamond fade 150, glow 220, trail 120 |
| **Trait accent** | at the contact point: core 96 px radial ink .9 → core colour .75 at 30 % → 0; 8 rays 46 × 4 px radius 2 at 22.5° + k·45°, gradient ray colour 0 → .95 (45 %) → ink .9 (70 %) → 0 | player: core amber, rays wick; enemy: phosphor / violet | core scale .5 → 1.35; rays translate 10 → 36 px, scaleX .5 → 1.35; over TraitAccent 150 out; then core fade 140, rays 120 |
| **Burst** | size px square, `glowGradient(colour, .8)` | role → token | scale .4 → 1.5, alpha 1 − t over 320 out (fire and forget) |
| **Shield block** | 100×114 at the chest + 40 px toward the blow; shield path `M3 2 H61 V31 C61 50 46 63 32 70 C18 63 3 50 3 31 Z` in 64×72 scaled; fill vertical guard .34 → .06; stroke guard .95 2.5 px (non-scaling), round joins; inner keyline ink .35 1 px at scale .84 about (32,36); drop-shadow 0 0 8 guard .7; burst (guard, 220). Broken: two halves clipped left/right of the middle + crack `M33 3 L29 17 L36 29 L30 43 L35 56 L32 69` ink .9 2 px | guard | pop: scale 1.2 → .8 → 1 over min(240, ms) out (crack fades in over its second half); wait the rest; not broken: fade 220; broken: halves move (∓16, 12) and rotate ∓14° about (50 %, 90 %) while alpha 1 → 0 over 220 in |
| **Ghost** (layer float) | discard/exile: 76×104 ink .55 sheet fading 1 − t; with `card`: that card face scaled 76/216 from its top-left, alpha .9·(1 − t²); sheet (rebuild): 64×84 card back (fill card, 1 px steel, radius 5) | | ms: CardToDiscard 200 inout / CardToExile 220 in / PileRebuild 200 out |
| **Low-HP vignette** (layer shade) | full screen radial centred on (960,540), ellipse radii 72 % of 1920 and 66 % of 1080, transparent to 58 %, crimson .70 at 86 %, crimson at 100 %; bright sky (top sky stop luminance > .4: layers 1, 2, 4): ellipse 60 % × 58 %, transparent to 66 %, crimson .70 at 86 %, mix(crimson 75 %, flood #06090c) at 100 % | | rest .25 (bright ×2.6 = .65) faded over 200 out; pulse to .5 (bright 1) then back to rest over 200 out |
| **Enemy-turn shade** (layer shade) | x 0, y 790, 1920×290, vertical black α 0 → .72 | | on/off, no fade |

**Floating text** (`floatText`, `fx.js:507-705`, layer float, DS `.ocb-float`):

- Dress (`dress`): a number alone, or "cause number" (`出血 −3`, `Guard −3`), takes an icon by role; `plain` keeps the text
  as a word. Kinds: struck / whiff → miss; number with role ink / omen / white → hp (heart icon, hp-text); number + heal →
  hp (heal icon, hp-text); guard → guard (shield icon fill guard .2, guard); warm → amber word; ink2 → ink-muted;
  omen word → phosphor; else ink.
- Tag: padding 2 10 (word / miss: 0 10), gap 6, radius 6, fill black .8, nowrap, centred on its point
  (`translate(-50%,-50%)`; `o.anchor` left/right puts that edge on the point). Number: 700 mono at `fontSize`, line height
  round(1.1·size), role colour; word: body font; miss: body font, ink, line-through 3 px steel. Icon size
  round(.62·size). Cause (`small`, before the number) and side label (after, margin-left 6): 400 16 body ink.
- Sizes: HP blows `numberFont(tier)` = [37, 48, 59, 72]; `Guard −n` 34 (`L.GUARD_NUMBER_SIZE`); words 36; whiff 40;
  heal/damage-over-time 40; StanceCue 48.
- Motion: 600 ms `out` on `clock.runPaced`; scale 1.35 → 1 over the first 18 % of eased t; alpha 1 until t .6 then
  linear to 0; rises `rise` px × t (default 43 = .19 × 226; the player passes chest→head distance; a whiff rises −50 =
  sinks); `followY`: rides at another float's height; `track`: x follows the figure.
- Placement (`placeFloat`): under any omen badge over its path (`avoid`, `avoidGap` 8); under every float still on
  screen (alpha > .15) whose swept box it would cross (6 px gap, 4 px x margin); if that takes it below `lowest`
  (feet line − margin) it stands beside them at its own height (toward the figure's back first), 16 px from the stage
  edges; else stacks anyway. Needs the tag's laid-out width/height before it moves.

### 2.5 俊敏's choice (`field/freestep.js`, `field.css:35-41`), layer marks

- Marks (only the ways the source opens + 留まる): arrows 64×40 centred over cell (playerCell ± 1), top = 512 + rise; stay
  64×28 on the player's own cell, top 572. Plate: fill panel-solid, 1 px steel, radius 6, centred content; icons amber:
  `forward` / `back` DS icons at 30, stay icon 24 (path `M12 3.5a2.6 2.6 0 110 5.2a2.6 2.6 0 010-5.2zM12 8.7v6.6M8.6 11.6l3.4-1.6 3.4 1.6M9.4 19.6l2.6-4.3 2.6 4.3M3.5 21h17`,
  stroke 2.2 round). Focus: 2 px amber border, fill mix(amber 16 %, panel-solid), glow 0 0 12 amber .4.
- Tip: line tip (padding 8 12, fill panel-solid, 1 px steel, radius 6, shadow-tip, 400 16/22 body ink, nowrap) centred
  8 px over the hovered (else focused) mark with its word 後ろへ / 留まる / 前へ.
- Show: alpha 0 → 1 and rise 8 → 0 over 150 out; hide: alpha → 0 over 120 in. Positions follow `geom.cellBox` (slides
  with the arena). Pointer enter → `onFocus(dir)`, click → `onPick(dir)` (uGUI handlers).

### 2.6 Contract (BATTLE_SCENE §6b; `field.js:503-993`) and Unity differences

Factory `Create(env, {layer, nodeKind, onFreeStepPick, onFreeStepFocus})`. Methods: `ApplyFrame(frame, {first})`,
`ShowGapTo(unit)`, `EnemyUnits()`, `SetExtras()` (no-op), `DrawAim(frame, unit, now)`, `ShowReach(cells)`,
`HideReach()`, `RetireAim(ms)`, `RetireAimOf(unit)`, `FocusAim(unit)`, `SetShift(dir)`, `ShiftX`, `NudgeOf()` (0),
`SetLowHp(on)`, `PulseLowHp(ms)`, `AppearEnemies(ms, from)`, `SetEnemyTurnShade(on)`, `SetPose`, `SetDown`,
`EnemyAway(unit)`, `SetEnemyHomeCell(cell, size, unit)`, `SnapHome`, `StanceGlowShown`, `SetStanceGlow`,
`FadeStanceGlow`, the beats of §2.3, `ShowFreeStep(playerCell, ways)`, `FocusFreeStep(dir)`, `HideFreeStep(ms)`,
`FreeStepShown`, `FreeStepFocus`, `FreeStepDirs()`, `FreeStepCentre(dir)`, `RecountGap(...)`, `SetGap(...)`,
`Strike(side, {byPlayer, system, intensity, ms, unit, source})`, `WallHit(side, dir, ms, unit)`,
`TraitAccent(side, ms, unit)`, `Mark(side, {fx, intensity, ms, crestLight, unit})`, `Burst(at, role, size)`,
`FloatText(at, text, role, size, o)`, `ShieldBlock(side, amountText, ms, unit, {broken})`, `ShieldAt(side, unit)`,
`Ghost(from, to, {kind, ms, ease, card})`, `Destroy`.

`ApplyFrame`: floor bind (unread from `Omens[].Disclosure`), dress figures (silhouette, height), Down, player at its cell
at once, enemies' homes set (moved there only when `first`), stance glows snapped when `first`, gap hidden once decided,
shade from `EnemyTurn`.

geom registered: `figure(side, unit) → {x, feetY, height, width, homeX, top, artX0, artX1, chest, head, unit, down,
walking}` (x includes the arena slide; chest = 65 % up, head = 84 % up), `receiver(unit)` (340×340 centred on the body's
middle over its home, 24 px inside the screen), `receivers()` (standing enemies left→right, cut at midpoints),
`freeStepBoxes()`, `cellBox(cell)` (+ slide).

Unity differences (JS wins): Unity figures wear one ProceduralArt silhouette (+ spear) with an `Outline` component and a
range tag 近/遠 at the feet (JS: per-roster DS silhouettes, 4-copy edge light, no range tag); stance glow = Glow sprite
behind (JS adds the screened body light); flash tints the body (JS: overlay silhouette); no paint order / depth looks;
Unity fx are straight bars (Streak at ±38°), a solid disc ripple, two rings for the trait accent, a flat white sigil, a
150×170 92 % shield slab with the number; floats are bare bold text with a 2 px shadow rising 96 px over 1000 ms; floor
reach switches at once; no aim reveal/fade, no broken-span chip, no move-over, no wall edge; backdrop has no earth,
horizon or shaft fade (and crossfades layers over 400 ms, which the battle does not need).

---

## 3. HUD (`hud.js` 697, `hud/status.js` 578, `omen.js` 350, `stances.js` 278, `stamina.js` 108, `piles.js` 234, `bottom.js` 184, `chrome.js` 427, `util.js` 169, `hud.css` 195)

### 3.1 Number panels (`hud/status.js`, `hud.css:5-46`, `StatusPanelLayout`)

- Box: top 614, left from `follow`, width 300 (player) / `EnemyPanelWidth(cells, size)` (210, 272 for 2 cells, never
  more than size·cellWidth − 10), height `StatusPanelLayout.Height(chipRows)` = 78, + 8 + rows·30 + (rows−1)·5 with chips.
  Layer panels.
- Look: fill panel (rgba(16,20,30,.88)); solid #141925 when 2+ enemies (`setSolid`); radius 6; edge 1 px rule; elite
  2 px phosphor; rest focus 2 px amber + glow 0 0 10 amber .45; down (fallen, 2+ enemies): opacity .55, grayscale 1,
  bar fill steel.
- HP row (mid y 20): optional hatch swatch 16 at (10,12) (2+ enemies; shifts the row right by 22); heart icon 20
  crimson (filled) at (10 + shift, 10); number at (36 + shift, 5) w 44 h 30, 700 24/30 mono ink tabular; bar at
  (86 + shift, 13) h 14, w = width − 10 − 86 − shift (min 8), 1 px rule border, radius 3, fill ground .8 under: trail
  (steel .85), fill (crimson), wick stripes (HpWickSpec: 3 px wick, 3 px gap, 135°/`／`, from `Span(hp, max, loss)`),
  ticks: a mark every 10 HP (count floor((max−1)/10)), 2 px ground .72 (1 px when 10 HP < 8 px).
- Guard row (top 40, h 28): shield icon 28 (Guard > 0: guard stroke + guard fill .2; 0: steel dashed 2.4 2.4) + number
  min-w 48, 700 24/28 mono guard (0: opacity .7, ink-muted). Gap 6. Pivot for pop: (14, 50 %).
- Enemy stamina group (right 10, top 40, h 28, gap 6, only when `ShowStamina`): dot 16 ink-muted, number min-w 30 700
  24/28 mono ink right-aligned, 構えの盾 24 (solid guard fill .2 when `ReserveLikely`, dashed guard otherwise).
  Narrow (< 210): gap 3, dot/icon 12, shield 20, no min widths.
- Chips (left 10, top 78, width = w − 20, wrap, gap 5): DS `.ocb-chip`: h 30, padding 0 8 0 6, gap 4, fill black .35,
  1 px ink-muted, radius 3 (OnFoe); OnSelf/Lasting: border steel radius 15; Boss: 3 px double boss; hover: border ink,
  fill white .1. Content: known status icon 18 ink + label (400 16 body) + stacks (600 mono); lasting: name + infinity 18
  hp-text + `×n`. Order newest-left (stamps); max 4 (enemy) / 6 (player), else the last slot is `+n`. Stances never
  appear as chips (they are the stance list's).
- Chip tip (single hud tip, layer over): w 340, DS `.ocb-tip`; head: icon 20 + title + small group (400 14 ink); rows
  `効き方` / `減り方` (head span 700 ink + 10 px) or, for `+n`, `chipText：Effect` per hidden chip; left = clamp(chip
  left, 16, 1564), bottom 38 px above the chip's bottom. Right click pins it (until a click, Esc or another right click).

Panel animations:

| method | motion | ms / ease |
| --- | --- | --- |
| `PopGuard(n, ms)` | guard badge scale 1.2 → .8 → 1 (`popScale` of eased t) | GuardGain 260 out |
| `CrackGuard(ms)` | shield brightness 2.4 → 1 (out) + badge shake x = sin(t·2π·2)·7·(1−t) (linear) | GuardBreak 200 |
| `AnimateHp(hp, ms)` | number at once; fill → new width | HpDrain 300 out |
| `AnimateTrail(ms)` | trail → fill | HpTrail 500 inout |
| `PlayStatus` Apply | chip scale .3 → 1.2 (t < .6) → 1 | StatusApply 200 out |
| Stack | pop | 150 |
| Tick | scale 1 − .18·sin(πt) | 120 out |
| Vanish | opacity 1 → 0, scale 1 → .7, then removed | 150 linear |
| `GreyOut(ms)` | opacity 1 → .55, grayscale 0 → 1, then down | 300 linear |
| `LightPips(st, max, perPip)` | waits perPip, +1 per step (player: stamina tag pip lights; enemy: number pops) | StaminaRecover 80/pip |
| `PopPips(ms)` | number pop | StaminaChange 220 |

### 3.2 Omen badge and plan (`hud/omen.js`, `hud.css:60-89,189-191`, DS `.ocb-omen`)

- Wrap 200×98 (z 2; walking 1; trait word up 4). Badge: fill panel-solid, 2 px violet, radius 6, padding 8 12, column
  space-between. Active (enemy acting on it): border phosphor + glow 0 0 30 violet .6. Rest (`OmenIcon.Rest`): 2 px
  dashed steel.
- Row 1 (h 52, gap 8, padding-right 26, clipped): icon 34 by `OmenIcon` (Attack sword phosphor, Guard shield guard,
  Status star ink, Move back ink — `forward` when the move goes right, Stance flag ink, Rest rest steel, Seal seal ink,
  None: 34 px blank) + value 700 48/52 mono (phosphor attack / guard / ink), or ≤ 2 statuses at 28 (glyph icons ink;
  a boss word = 16 px body ink in a 3 px double boss frame, radius 3, letter-spacing −.04em).
- Swatch (hatch swatch 22, 2 px reach-red, radius 3, bars 2 px every 6) at right 12, top 14: shown with the hit mark, or
  (2+ enemies) when the omen hatches cells; never for a move or rest, never when Disclosure < 1.
- Row 2 (h 26, space-between, 400 16/22 body): kind label (ink); hit mark 30×26 radius 4: lands → 2 px phosphor +
  person icon 18 phosphor; misses → 2 px dashed steel + person steel + a 2 px steel slash rotated −24°. v4.2 side glyph
  (64×28, 2 px violet, radius 4, 600 20 mono) when no hit mark; struck: dashed steel, .8, slash.
- Tail ▼: 16×9 triangle under the badge (top 96), in violet (active phosphor, rest steel), x = enemy head x − badge
  left, clamped 14…186.
- Plan (`ReserveOmen`): 170×66 at (15, −74) (8 px above the badge), padding 4 10 5, fill panel-solid, radius 6, dashed
  2 px ink-muted frame at .62, contents at .62. Row 1 h 32 gap 8: icon 24 + value 700 28/32 mono (or statuses 20); row 2
  h 20, 16/20 body: kind + tag `PlanTag` (400 14/16, 1 px dashed ink-muted, radius 3, padding 0 4; changed: solid ink).
- Trait word: tag inside the badge's right end (right 8, vertically centred), padding 4 10, fill panel-solid, 2 px
  phosphor, radius 4, 400 16/22 body ink, outer 3 px panel-solid ring.

| method | motion | ms / ease |
| --- | --- | --- |
| `PopIn(ms)` | drop −24 → 0 and alpha 0 → 1 (badge and plan) | OmenShow 240 out |
| `Blink(ms)` | badge alpha × lerp(1, .25, sin πt) | OmenBlink 250 inout (120 apart per enemy) |
| `Flare(ms)` | wrap scale 1 + .18 sin πt; kind word brightness 1.6 → 1 | OmenExecute 200 out |
| `Reveal(ms)` | scale 1 + .12 sin πt; badge brightness 1.5 → 1 | OmenShow out |
| `TraitWord(word, ms)` | scale .6 → 1, alpha 0 → 1; hold 900; alpha → 0 over 200 in | 150 out |
| `FadeOut / FadeOutAll(ms)` | badge (and plan) alpha → 0 | OmenSpend 200 in |
| `StrikeSide(ms)` | struck class + shake 6 px ×2 | SideBonusMiss 260 linear |
| `PromotePlan(ms)` | plan translates down 74, alpha 1 → 0 while the badge (redrawn as the plan) 0 → 1 | 150 out |
| `SetActing(on)`, `HideNow`, `HideAllNow`, `StrikeSideNow`, `SetMoveDir`, `SetSwatchShown`, `SetTail`, `SetBehind`, `Place(left, top)`, `Box()`, `Show`, `Visible` | | |

### 3.3 Stance lists (`hud/stances.js`, DS `.ocb-stance`)

- Frames 250×52 at x 24 (player) / 1646 (enemy), top = 470 − i·58 (i = 0 newest), ≤ 3 frames, the oldest folded into
  `+n` on top. Look: padding 0 12, gap 8, fill panel, 2 px hp-text (player) / violet (enemy), radius 6, 700 20/26
  display; [swatch 16 (enemy, 2+ enemies)] + flag 24 (hp-text / phosphor) + name (ellipsis) + `×n` (600 16 mono, w 40,
  right). Empty: no fill, 2 px dashed steel, steel flag only. Hover: fill rgba(40,44,58,.92) + tip (340, beside: player
  right +12, enemy left −12−340, top clamp(top − 10, 16, 1064 − h)); right click pins.
- New stance ring: box-shadow 0 0 0 4 px (hp-text / violet) α .9 → 0 over 300 linear on the newest frame.
- High place (`keepClear(arts)` from `follow`): when a standing figure's art comes within 4 px of the list's rest box
  → the list moves to the top bar: player at (corner right + 12, 20), enemy at (speed button or gear x − 12 − 250, 20),
  one frame (newest) + `+n` (600 16 mono, margin-left auto); fade in 0 → 1 over 150 ms real (ease-out-quad). Tip under it.
- geom: `stanceBox(side)`, `stanceSlot(side)` (slot 0 or the high place's frame).

### 3.4 Stamina tag (`hud/stamina.js`, DS `.ocb-stamina`)

(32, 846) 236×84, padding 10 12, fill card, 1 px steel, radius 5. Row 1 h 36 gap 8: dot 18 amber; number 700 34/36 mono
amber (pivot 0 50 %); `▸ n` 700 24 mono wick, right, visible only while a card is held (`ShowCost`). Pips (margin-top 8,
h 20, gap 4): one per max point, 16 px circles: held amber fill; paying 3 px amber ring; next-recovery 2 px dashed amber;
empty 2 px steel ring; divider 2×20 ink .7 after `StanceDivider` pips.
`GlowReserve(ms)`: pips before the divider: scale 1 + .25 sin πt, glow 0 0 (10 + 6a) (4a) amber α a, a = .9 sin πt
(ReservePips 250 out). `LightPip(i)`: scale 1.35 → 1, ring 0 0 0 (8t) wick α .9(1 − t) over 320 out.
`PopNumber(ms)`: pop.

### 3.5 Piles and the list drawer (`hud/piles.js`, DS `.ocb-pile`, `.ocb-drawer*`)

- Piles at x 32 + k·86, y 948, 64×84: sheets behind (`PileLayout.SheetsBehind`: 0 for ≤ 1, then 1 per 5 cards, max 3) at
  top −4·i: fill card, 1 px steel, radius 5; face: same, icon 28 (`draw` / `discard` / `exile`, ink; empty: steel, no
  fill, dashed border); hover: face border ink + caption tip (line tip h 22, padding 0 6, no shadow, line 20) centred on
  the pile, top 1006, text `PileList.TabLabel`. No count is written. Click → toggle the list.
- Drawer (layer over): x 0, 576×1080 (30 %), fill panel-solid, right border 1 px rule, shadow-tip; slides in from
  −576 over 220 out on the scene clock; closes at once. Head: wrap row, align end, gap 12, padding 20 16 0 16, bottom
  1 px rule: tabs 山札 / 捨札 / 除外 / ログ全文 (700 18 body, padding 12 16, ink at .72, on: 1 + 2 px amber underline; click
  `uiClick`), close button `閉じる（Esc）` (margin 0 0 8 auto, padding 10 14, 400 18 body ink, fill card, 1 px rule, radius 6,
  hover border ink), hint row `閉じるとプレイできます` (full width, right, 16/22 ink, margin-bottom 10). Body (scroll,
  padding 8 16 24): rows 40 tall, padding 0 12, gap 10, bottom 1 px rule, 400 18 body: kind band 6×24 radius 2 (attack
  violet, guard guard, skill amber, stance crimson, unknown steel), name, `in / deck` (600 mono, right). Empty: `なし`
  (padding 16 12, 18/1.4). Log: lines 16/26 ink pre-wrap; `—` lines bold with 8 px above; empty
  `ログ全文は、まだ中身がありません。`; scrolled to the end.
- geom: `pileMiddle(kind)` = (left + 32, 990), `coversPointer(pt)` (x < 576·slide), `drawerRight()` = 576·slide.

### 3.6 Lower right (`hud/bottom.js`)

- Played attributes: 40×40 at x 1718 + i·46, y 896 (`PlayedLayout`); DS panel (fill panel, 1 px rule, radius 6), opacity
  .55 (1 when played); icon 24 (attack sword phosphor, guard shield guard, skill star amber, stance flag hp-text; steel
  when not played); count badge 20 circle at (+28, −8): fill panel-solid, 1 px border in the attribute frame colour,
  700 14/18 mono ink; last-played underline: left 4 right 4, 7 px below, 2 px, frame colour. Follow-up badge (`追撃`):
  right edge at 1712, y 896, min 40×40, padding 0 6, panel, text 700 24 mono phosphor `+n`; in: scale .6 → 1 (origin
  right-top) over 150 out; out: alpha → 0 over 150 linear.
- Reserve hint: (1560, 984) 98×56, panel, 1 px dashed guard, radius 6, padding 0 12, gap 8: shield 30 (guard fill .2) +
  `+3` 700 24 mono guard. Tip above right-aligned: `構え：…。いまターンを終えると付く`.
- End turn: (1672, 976) 224×72, 2 px amber, radius 6, fill amber .16 (hover .28), 700 24/28 body, letter-spacing .08em,
  ink, `ターン終了`. Enemy turn / over: opacity .4 (over: steel border, no fill, no click). `Pop(ms)`: pop (StanceCue's
  200). `Flash(ms)`: brightness 1 + .6 sin πt, glow 0 0 (24k) amber α .7k (k = sin πt) over 600 out. `Refuse(ms)`:
  brightness .75 + shake 6 px ×2 over 220 linear.

### 3.7 Chrome (`hud/chrome.js`)

- Corner (24, 20) h 44: DS `.ocb-corner` panel, padding 0 16, gap 18, 600 20 mono ink: `hourglass` 22 + turn,
  `stairs` 22 + layer (env.layerNo), chain dots (12 px circles, 2 px ink-muted border, done = filled ink, `i < index`),
  `miasma` 22 phosphor + `n%`. Each item has a hover tip 8 px under the corner (CornerTips texts).
- Speed switch (1708, 20) 120×56: panel, padding-left 12, gap 8, 600 20 mono; `speed1..3` icon 24 + `BattleSpeed.Label`;
  hover: border ink + white .1 wash; tooltip `BattleSpeed.Tooltip` under, right-aligned (`CornerLayout.SpeedTooltip`).
- Journal button (1840, 20) 56×56: panel, `journal` icon 30 ink; hover border ink, fill rgba(40,44,58,.92); off: .4, no
  click. Gear (1640, 20) only with `settingsInBattle`.
- Banner (layer float): y 400, 1920×132, horizontal gradient rgba(10,12,18,0) → .8 (28 %) → .8 (72 %) → 0; 800 64/72
  display, letter-spacing .12em, ink, centred; two 2 px rules from 22 % to 78 % at 10 px from the top and the bottom,
  amber (enemy: violet) α .7. Alpha: t < .25 → t/.25, t > .75 → (1 − t)/.25, linear, over TurnBanner 300 /
  EnemyTurnBanner 400; then removed.
- End scrim (layer over, under the result): ground .62, alpha 0 → 1 over `settleEnd(ms)` while fallen parts fade.
- Result (layer over): DS `.ocb-end` (full screen, rgba(8,10,14,.62); loss: flood .4) + paper (washi #f0e6d0, ink
  #1d1a16, radius 4, shadow 0 18 40 black .5). Win: 720 wide, padding 48 56 40, centred, h2 800 64/72 display
  letter-spacing .12em (`勝ち`), lines 400 20/32; in: alpha min(1, 2t), scale .94 → 1 over ResultCard 400 out. Loss:
  922 wide, min-h 626, padding 48 72 40, column gap 14: title 800 48/60 display .08em centred; lines 400 22/32 centred;
  bars (margin 6 64, gap 8; head 18/24 at .8; each row grid 96 / 1fr / 48, gap 14, 400 20/28, value 600 22/28 mono
  right; track 16 tall radius 3 washi-ink .12; fill radius 3 attribute colour); stats lines; button `手記へ` (700 22,
  letter-spacing .08em, 2 px washi-ink border, radius 6, padding 16 36, margin-top 18, focus outline 3 px amber offset 3).
  Loss in: paper translateY 80 → 0 over ms out; items from the top 120 apart starting at ms/2, each alpha 0 → 1 over 200
  out; bars width 0 → value over 400 out starting at ms/2 + 240.
- Chain banner (layer over): full screen ground .86; band centred, padding 28 96, 2 px top/bottom rules ink .4; `n 戦目`
  800 64/72 display .12em; name 700 28/36 display .06em. Panel alpha 0 → 1 and y +12 → 0 over 300 out, hold 700, board
  alpha → 0 over 400 in (`onOut` at its start).
- Loss ink (layer over): a 1920×1240 fill (transparent → flood .94 over its first 160 px) rising from top 1080 to −160
  over 800 in; then `力尽きた` (700 64/80 display, centred, top 560) alpha 0 → 1 and letter-spacing .06 → .40 em over 300
  out; `LossWordOut(ms)` fades the word (in).
- Omen flight (`FlyOmenToStances(unit, ms)`, layer float): a copy of the badge plate (not its plan) from the badge box
  to the enemy stance slot, scale 1 → min(250/200, 52/98) (origin top-left) over 300 out, alpha 1 until .7 then → 0;
  the badge hidden at once.

### 3.8 `follow(o)` (`hud.js:273-423`), every frame from the player

- Panels: one enemy → `StatusPanelLayout.Place(playerX, 300, enemyX, w)`; several → `spreadRow` (each centred on its
  figure, kept in [lo, hi], pushed apart to 8 px, 24 passes). lo = max(24, drawerRight + 24 when the pile list is open),
  hi = min(1896, journalEdge − 24). Fallen grey panels: first try the row as it stood (kept if every standing panel's
  figure x stays ≥ min(24, w/4) inside its panel); else each fallen panel stands under its cell or slides ≤ 24 px to keep
  4 px off, or yields (hidden). Easing toward targets: share = 1 − exp(−14 · TweenSpeed · dtReal/1000); snap when asked,
  on the first frame, when the widths change, or when the clock scale > 50; a walking figure carries its panel (only
  the starting offset eases away).
- Badges: centred on the enemy x, clamped 24 px inside [badgeLo, badgeHi]; several spread apart, each at most 75 px
  (150 with a drawer or the journal open) from its home, eased with share 1 − exp(−3 · speed · dt/200); bottom 14 px
  over the head (2+ enemies: over the tallest head, one row); lift for a prediction tag: need = clamp(bottom − (tagTop −
  4), 0, 80) with tagTop = figureTop − 22 − 30, eased 1 − exp(−3 · speed · dt/120); tail at the head x; walking → behind.
- Stance lists: `keepClear` with every standing figure's art box.

### 3.9 Contract (BATTLE_SCENE §6c; `hud.js:546-663`) and Unity differences

`ApplyFrame(frame, {first, keepOmen})`, `Follow(o)`, `Status(side, unit)` → panel API (`SetGuard`, `PopGuard`,
`CrackGuard`, `SetHp`, `AnimateHp`, `AnimateTrail`, `SnapTrail`, `SetHpKeepingTrail`, `ShowHpLoss`, `SetStatus`,
`PlayStatus`, `SetStamina`, `LightPips`, `PopPips`, `GreyOut`, `SetFocus`, `SetYielded`, `Box`, `Hp`, `Shown`),
`Omen` (forwarder to the frame's enemy), `OmenOf(unit)`, `SyncOmenLook(frame, unit)`, `EnemyUnits()`,
`Stamina.ShowCost(n)` / `GlowReserve(ms)`, `ReserveHint(text)`, `SetEnemyTurn(on)`, `EndTurn.Pop/Flash/Refuse`,
`Banner(text, side, ms)`, `ResultCard(outcome, ms, info, {onButton})`, `ResultCardNow`, `ChainBanner(title, name, scale,
onOut)`, `LossWordOut(ms)`, `SettleEnd(ms, outcome)`, `SettleEndNow`, `RetireOmens(ms)`, `LiftOmen(units)`,
`SetRestFocus(unit)`, `SetLog(lines)`, `FlyOmenToStances(unit, ms)`, `LossInk(word, inkMs, wordMs)`, `Speed(label,
chevrons)`, `SetSpeedVisible(on)`, `ClosePinned()`, `DrawerOpen()`, `CloseDrawer()`, `Destroy`. Options: `chain`,
`onEndTurn`, `onSpeed`, `onJournal`, `onSettings`, `settingsInBattle`, `onDrawer(open)`, `onOmenHover(unit)`.
geom registered: `pileMiddle`, `omenBox(unit)`, `omenShown(unit)`, `omenBoxes()`, `endTurnBox()`, `statusBox(side, unit)`,
`statusBoxes()` (computed from layout numbers, never from layout reads — it is asked every frame), `stanceBox`,
`stanceSlot`, `coversPointer`, `drawerRight`.

Unity differences (JS wins): Unity omen `PopIn` scales .6 → 1 (JS drops 24 px and fades), no ▼ tail, swatch only with the
hit mark, move icon always →, trait line floated over the head (JS: word inside the badge), no plan promotion; chips
append in arrival order (JS newest-left); no rest focus, no grey-out, no yield logic; stance list has no high place,
ring or pinned tip; pile list is a stand-in with no slide, kind bands, log tab or closing hint; corner fills the dot of
the fight in progress (JS fills only won fights) and has no tooltips; banner is a 1920×120 plate 80 px over the middle
(JS: DS band at y 400, 132 tall, with rules); result is a 520×220 box (JS: DS end paper / loss screen); end-turn has no
flash/refuse; no loss ink, chain banner, end scrim or omen flight.

---

## 4. Proposed C# split (Assets/View/Trial/Scenes/Battle/, namespace `Trial.Scenes.Battle`)

One C# file per JS module, same function names in PascalCase. No unit shares a file. Line counts are estimates for C#
(no CSS: every look is built in code, so a module grows ~1.2–1.8× its JS + CSS).

| unit | files | ≈ lines | kit / DS needs | depends on |
| --- | --- | --- | --- | --- |
| **U1 Hand** | `Hand.cs` (hand.js), `HandMarks.cs` (hand-marks.js) | 1500 + 450 | rounded rect, path stroke (hooks, square caps), radial glow, soft drop-shadow on strokes, dashed line (throw line 9/6), colour filter (grayscale/brightness for `is-other` tags), TMP mono text, DS icons | U2 (`HandCard.BuildCard`, `HandPanels.Create` APIs), env/clock/geom/input (architect), geom of U3 (figure, receivers) and U5 (omenBox(es), statusBox(es), stanceSlot, coversPointer, drawerRight) |
| **U2 HandFace** | `HandCard.cs` (hand-card.js: face, `ModsOf`, `PreviewKind`, `ValueIcon/Color`, `WrapLine`, `FaceKey`, `BuildDetail`, `NowPart`), `HandPanels.cs` (hand-panels.js: guide, refusal line, hover line, reach panel, term tip, detail + `FitDetail`, `PlaceDetail`, `ClearOfPanels`) | 800 + 750 | rounded rect fill/border/dashed border, box shadow (card shadow, lamp ring + glow, tip shadow), clip of the attribute bands to the 6 px corner, circle ring, TMP text (display/body/mono, letter-spacing, dotted underline over a char range, rich colour for ∞), text measure for CardTextFit, ellipsis cutting, DS icons | DS card skeleton if the DS unit owns `.ocb-card` (explore/meta use `OCB.Card` too); env geom for placement (statusBoxes, omenBoxes, freeStepBoxes, drawerRight, journalEdge) |
| **U3 Field** | `Field.cs` (field.js), `Field/Art.cs` (art.js: `Tok`, `GlowGradient`, `ShapeToken`, VARIANTS, `SilhouetteOf`, `FigureDef`, `DrawFigure`, SHIELD_PATH) | 1350 + 450 | SVG path fill (arcs, relative, smooth curves), 4-offset edge light, path filled with a radial gradient (body light) + screen blend, flat-colour overlay of a path, radial glow (foot light, stance glow), colour filter (brightness, drop-shadow outline for the front enemy), full-screen radial vignette (ellipse, multi-stop), vertical gradient (shade) | U4 (`Floor`, `Backdrop`, `FreeStep` APIs), U6 (`Fx` API), DS FIGURES data |
| **U4 Floor + Backdrop + FreeStep** | `Field/Floor.cs`, `Field/Backdrop.cs`, `Field/FreeStep.cs` | 650 + 350 + 300 | rounded tile fill/border, hatch clipped to a rounded rect with a left-to-right reveal, dashed frame, multi-stop vertical/horizontal gradients, box shadow (threat, reach), rotated gradient quads (shafts) with a vertical alpha mask, soft dots (motes), radial ellipse, TMP mono, DS icons + `range` | BackdropSpec/BackdropMotion/FloorSpec (pure C#) |
| **U6 Fx** | `Field/Fx.cs` (fx.js: strikes, marks, sigil, accent, burst, floats + placement, shield, ghost) | 1250 | path stroke with trim (arc reveal) + gradient along the path + glow, gradient bars with axis mask, ellipse rings with glow, radial glows, rotated quads, polygon clip into halves, float tags (rounded rect black .8 + icon + TMP with line-through) | clock (`RunPaced`), DS icons; U2's card face for the ghost (passed in as a GameObject) |
| **U5 Hud core** | `Hud.cs` (hud.js: ApplyFrame, Follow, SpreadRow, FallenPlace, LiftOmen, omen forwarder, SettleEnd, RetireOmens, FlyOmenToStances, geom), `Hud/Util.cs` (util.js: `Pop`, `Shake`, `ScaleTo`, `PopScale`, `OMEN_ICON`, `GLYPH_ICON`, `KIND_LOOK`, `PILE_ICON`, `StatusIconOf`, `ShieldIcon`, `CreateTip` (one tip + pin), `LineTip`) | 950 + 250 | TMP, rounded rect, DS icons; clone-able badge visual for the flight | U7, U8, U9 (creates them; their APIs below), StatusPanelLayout, BattleSpeed |
| **U7 Panels** | `Hud/Status.cs` (status.js), `Hud/Stamina.cs` (stamina.js) | 850 + 220 | rounded rect + border (1/2 px, double 3 px for boss chips), pill radius 15, wrap layout of chips (measure TMP widths), stripes (HP wick 135°), ticks, hatch swatch, filled/dashed DS icons, colour filter (grayscale, brightness), circle, dashed circle ring, box-shadow glow ring | `Hud/Util.cs` (U5) |
| **U8 Omen + Stances** | `Hud/Omen.cs` (omen.js), `Hud/Stances.cs` (stances.js) | 600 + 450 | rounded rect, dashed frame, glow shadow, triangle, hatch swatch, TMP (display 20, mono 48), strike line, brightness filter, ellipsis | `Hud/Util.cs` (U5) |
| **U9 Chrome + Piles + Bottom** | `Hud/Chrome.cs` (corner, speed, journal, gear, banner, result, chain banner, loss ink), `Hud/Piles.cs` (piles + drawer + log), `Hud/Bottom.cs` (played, reserve hint, end turn) | 700 + 450 + 320 | rounded rect, horizontal gradient with stops (banner), scrim, washi paper with shadow, progress bars, scroll view (RectMask2D + ScrollRect), tab underline, TMP with animated letter-spacing, circles, brightness filter, DS icons | `Hud/Util.cs` (U5); PileList, PileLayout, PlayedLayout, ReserveHintLayout, CornerLayout, JournalBook.ClosingHint, CardCatalog/CoreText (row band colour) |

Interfaces to freeze first (so the units can run in parallel): `HandCard.BuildCard(face) → CardFaceView {Root, SetLit,
SetDim, SetCost, SetMods, Lit, Terms[]}`; `HandPanels.Create(env) → {SetGuide, StepGuide, PanelsUp, RefusalShown,
RefusalBox, StepRefusal, ShowRefusal, HideRefusal, ShowHover, HideHover, ShowTerm, HideTerm, DetailShown, DetailFor,
ToggleDetail, ShowDetail, PlaceDetail, HideDetail, HideAll, Destroy}`; `HandMarks.Create(env) → {Shown, Hot, Show(face,
previewOf, target), SetTarget, TaggedUnits, SetHot, Hide, BoxOf(side, unit), Update, Destroy}`; `Floor.Create(env, tile)`
/ `Backdrop.Create(env, layer)` / `FreeStep.Create(env, opts)` as in floor.js / backdrop.js / freestep.js; `Fx.Create(env)
→ {Strike, Mark, TraitAccent, Burst, FloatText, ShieldBlock, Ghost, Clear}`; the hud parts' factories as in hud.js:111-171.

Build order: Kit + DS + scene env first (other areas); then U2, U4, U6, U7, U8, U9 and `Hud/Util.cs` in parallel; U1, U3,
U5 as soon as their dependencies' signatures exist (they can stub them).

---

## 5. Notes for the architect

- **Input**: one scene-level pointer pump (stage px, buttons, right button, focus lost) feeding the hand each frame; uGUI
  EventSystem only for the hud's buttons/chips/tabs, the freestep marks and the drawer. The hand must be able to ask
  "is the top raycast hit in hud / over / panels?" (`IsForeign`).
- **Every frame order** (JS): clock tick → hand onFrame (hover, hold, panels, guide, refusal) → field onFrame (arena
  slide, render) → player `followFigures` (hud.follow, floor reach). Keep geometry reads lazy and cheap (`statusBoxes`
  from numbers only).
- **Performance**: keep per-frame changes to transforms and alpha (CanvasGroup / vertex alpha); avoid mesh rebuilds per
  frame except where the JS animates shape (slash trim, hatch reveal clip, sweep motes, gradients that grow). Put
  `float`, `fx`, `hand`/`held` on their own sub-Canvases so a float does not rebuild the hud.
- **Text measure parity**: CardTextFit receives a measure delegate; implement it with the same TMP font asset at the same
  size (width of the widest line, height = lines × size × pitch).
- **Sounds**: the hand plays its own interaction sounds (§1.2); the hud plays `uiClick` for the pile toggle, the drawer
  tabs, the drawer's close button and the gear (the speed switch's click sound is the player's); the field plays none.

---

## 6. Kit and DS needs, consolidated

Vector kit (uGUI Graphic subclasses, geometry engine-free and dotnet-testable):

1. Rounded rect: fill, per-corner radius, AA feather; border 1/2/3 px solid; dashed border (DS `--dash` 3 px; Chrome
   draws a 2 px dashed border with dashes about 3× the width — check against screenshots); double border (3 px double =
   1 px line, 1 px gap, 1 px line: boss chips, boss words); inset/outset offsets (`inset: -1px`, `inset: 4px`).
2. Box shadow: offset, blur, spread, colour, radius; spread-only ring (`0 0 0 Npx`); several on one box (card +
   lamp). Glow around a circle (lamp, pips).
3. Linear gradient rect: N stops, any angle; a second, separable alpha mask along the other axis (sweep band, shafts
   with the 560 px fade); repeating stripes (HP wick 3/3 at 135°, throw-line dashes 9/6).
4. Radial gradient: ellipse "closest-side" in a box, N stops; the `glowGradient` (alpha·(1−r)² at r 0/.25/.5/.75, 0 at 1)
   preset; ellipse radii in % of the box (vignette).
5. SVG path: parser for M/L/H/V/C/S/Q/T/A/Z, absolute + relative, implicit repeats; fill (concave, holes not needed;
   nonzero per path); stroke with width (screen-px "non-scaling" option), round/square caps, round/miter joins,
   dasharray (2.4 2.4 dashed icons, 3 3 dashed circle), trim / dash offset (slash reveal), gradient along a path;
   halo / drop-shadow approximation (a wider, feathered copy).
6. Path-masked fills: a path filled with a radial gradient (stance body light) and a path in flat colour (flash
   overlay); either dense tessellation with per-vertex colour or a stencil mask graphic.
7. Hatch: parallel bars (width, gap, angle) clipped to a (rounded) rect, with a clip-from-left reveal fraction; swatch
   preset (22 or 16 px, 2 px reach-red, radius 3, bars 2/6).
8. Ellipse ring at real size (even stroke) with glow; circle; triangle (▼); polygon clip by a half-plane (shield halves).
9. Colour filter for a subtree: opacity, brightness (×), grayscale (0…1). Used by: fallen panel, `is-other` prediction
   tags, back enemies, fall darkening, crack / flare / reveal / end-turn flash brightness.
10. Blend materials: screen (body light) — additive at reduced alpha is the fallback.
11. TMP text factory: font roles (display Bold, body Regular/Bold, mono SemiBold/Bold with Japanese fallback), size and
    line height in px, letter-spacing in em (animated for 力尽きた), nowrap / pre / ellipsis, rich colour for one char,
    dotted underline and 3 px line-through for a character range (bounds from `textInfo`), underlay shadow (guide line
    0 1 3 ground + 0 0 8 ground), tabular numbers, Japanese line breaking (kinsoku) where the browser wraps (detail body,
    terms, log).
12. Clipping: RectMask2D for the drawer body (scroll) and row-1 overflow in the omen badge; the card's attribute bands
    drawn as shapes already matching the rounded corner.

DS port (shared with explore/meta; owner outside this area): token colours (§0.3), the 24×24 icon paths
(`$DS/bundle.js:64-101`; `heart` and `pip` filled, the rest stroked 2.2 round), `FIGURES` (`bundle.js:583-639`),
`STATUS` name → icon id (`bundle.js:168-179`), hatch/swatch, and the `.ocb-card` skeleton if explore/meta need it too.

---

## 7. Risks

1. CSS effects with no uGUI equivalent: blur box-shadows, `drop-shadow` on strokes, `filter: grayscale/brightness`,
   `mix-blend-mode: screen`, `clip-path`, `mask-image`. Each needs a kit feature (§6) or a visible compromise.
2. Text metrics: TMP vs the browser change CardTextFit's chosen sizes and breaks and the hover line's `WrapLine`; the
   detail's auto height and the float placement depend on laid-out sizes (measure synchronously after setting text).
   Dynamic SDF atlases must contain the glyphs before measuring.
3. SVG path robustness: the DS silhouettes and icons use arcs, smooth curves and implicit repeats; ear-clipping of
   self-touching parts can fail; the 4-offset edge light needs alpha compounding as in the browser.
4. Per-frame cost: floats, hand cards (≈ 40 graphics each), figures and hooks change every frame; mesh rebuilds or
   full-canvas rebatching would drop frames. Use transforms/alpha and sub-canvases.
5. Input parity: the hand's own math hit-test vs uGUI raycasts on buttons and chips; the "foreign" test, lost-release
   handling and the 8 px threshold must match or drags misfire (several review rounds in the JS fixed exactly this).
6. Promise semantics in C#: beats return awaitables that never complete after destroy; many JS calls are
   fire-and-forget beside the flow — the C# handles must not throw when the objects are gone.
7. Multi-enemy: the JS reads writer-extension fields that the C# Depiction types do not have (§8). Until they exist,
   only the one-enemy path can run against the C# source.
8. Fonts: no Shippori Mincho ExtraBold (800) file; banners, the result title and the chain banner will be drawn at 700.

---

## 8. Rule gaps (JS-only data the views read; the C# writer/source does not provide them today)

Checked against `$U/Depiction/Script/DepictionTypes.cs` (it has `CardFace.LitFor(enemy)` and `HpLossFor(enemy)` only):

- `DepictionFrame.Enemies`, `Omens`, `SwatchesShown`; `UnitFrame.Unit`, `Hatch`, `Cell`, `Size`, `Gap`; `OmenFrame.Unit`,
  `Hatch`, `Disclosure`; `FloorFrame.Enemies[]` (`Unit`, `Hatch`, `AimCells`, `DenseCells`, `MoveCells`, `Cell`, `Size`,
  `Gap`, `Down`); `Cue.Unit`, `Cue.UnitCellAfter` (docs/port-notes/depiction-multi-enemy.md).
- `CardFace.ReachOn` / `ReachFor(u)`, `TargetUnit`, `CostOn` / `CostFor(u)` / `CostDropFor(u)`, `StanceHintAfterPayFor(u)`.
- Source extensions used through the player: `Inspect/PreviewFor/RefusalText/TryPlay(…, target)`, `CanTakeFreeStep(dir)`,
  `TakeFreeStep(dir)` (the core has `TurnLoop.CanTakeFreeStep/TakeFreeStep`; `CoreBattleSource` does not expose them),
  the `FreeStep` event kind.
- Constants the views print: `Constants.EmpowerMult` (1.5), `IntimidatePenalty` (3), `HeavyBlowPower` (6),
  `FragileMult` (1.5) — present in `BattleCore/Constants.cs`.
