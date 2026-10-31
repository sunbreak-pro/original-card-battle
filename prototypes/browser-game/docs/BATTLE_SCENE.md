# The battle scene — module split and internal contracts

The battle scene reproduces Unity's battle screen: `unity-port/unity-project-kit/Assets/View/Depiction/DepictionPlayer.cs`
(the playback core) and its views (`CardView`, `FigureView`, `FloorView`, `StatusBarView`, `OmenBadgeView`, ...).
The pure C# behind them is already ported under `src/bridge/depiction/` and `src/bridge/depictionScript/`.
This document is the contract between the scene's core (`player.js`, owned by the architect) and the three
view modules (`hand.js`, `field.js`, `hud.js`), so that three agents can build the modules in parallel
without talking to each other.

Paths below are relative to `game/` (`$G`). `$U` is `unity-port/unity-project-kit/Assets/View/Depiction`.

## 0. Ground rules (all modules)

- **No rule, no number.** Every number, label, verdict and line of text comes from the frame / event / cue
  (`DepictionTypes.js`), from a ported text helper (`CoreText`, `CardFaceText`, `StatusChipText`, `PileList`,
  `BattleSpeed`, `DepictionText`), or from `ctx.source`'s read-only answers (`PreviewFor`, `Inspect`,
  `RefusalText`, `GuideText`). Where a Unity view hard-codes a string, port it and mark the line with
  `// scene text` (example: `PileList.CloseLabel` is ported; a hard-coded `"Guard −"` in `DepictionPlayer`
  is scene text).
- **Layout numbers** come from the ported layout scripts (`HandSpec`, `FloorSpec`, `StatusPanelLayout`,
  `CornerLayout`, `PileLayout`, `PlayedLayout`, `ReserveHintLayout`, `RefusalLineLayout`,
  `EnemyTurnShadeLayout`, `StanceList`, `FigureFrame`, `HpWickSpec`) or, where Unity has them only in a
  prefab, from `src/scenes/battle/layout.js` (`L`), which collects the design system's `OCB.LAYOUT` /
  `layout.md` values. Do not invent coordinates; add a named constant to `layout.js` only through the
  architect (or put it at the top of your module with its source in a comment).
- **Looks** come from the design system: `window.OCB` components (`Card`, `OmenPlate`, `StatusPanel`,
  `StanceSlot`, `StaminaPlate`, `Pile`, `FloorTile`, `DistanceChip`, `Figure`, `FootLight`, `TargetFrame`,
  `PredTag`, `HoverLine`, `ReachPanel`, `RefusalLine`, `Banner`, `FloatNum`, `CornerInfo`, `SpeedToggle`,
  `JournalButton`, `PlayedAttrs`, `GuardPreview`, `EndTurnButton`, `icon`, `hatch`, `swatch`, `STATUS`,
  `ATTR`, `FIGURES`) and `bundle.css`. The interaction feel of `build/src/50-game.js` (hover lift, hold and
  release, drop zones, prediction tag, reach light, refusal line, sequences) is the approved feel: reuse its
  code and numbers wherever the Unity view does not say otherwise.
- **CSS**: class prefix `ocbg-battle-`; every selector starts with `.ocbg-battle` (the scene root). Tokens
  only (`var(--…)`), except per-layer backdrop colours, which come from `BackdropSpec` as data. Colour by
  role; text under 24 px is `var(--ink)`; `--reach-red` only for floor hatching and swatches; warm colours
  (`--amber`, `--wick`) only on the player's side; no emoji. The DS classes (`.ocb-…`) work inside the
  scene because the stage root carries `ocb-root`.
- **Coordinates**: 1920×1080 stage px, origin top left, y down. Unity's fx offsets are y-up: `chest + (0, 40)`
  in Unity is `chest.y − 40` here.
- **Time**: every animation runs on `env.clock` (the scaled scene clock, §2.2) with the **nominal** ms of the
  effect (`EffectCatalog`, written at ×1.25). Never use `setTimeout`, `setInterval`, CSS transitions or
  `element.animate` with your own durations for anything the battle speed / 演出短縮 / fast-forward must
  stretch. Input timings (refusal hold 3.5 s, long-press 400 ms, drag threshold 8 px) use `env.real`.
  The design system's own CSS transitions (`.ocb-bar-fill`, `.ocb-omen`, `.ocb-card-dim` …) run on real time;
  `battle.css` switches them off inside the scene — do not reintroduce CSS transitions for timed things.
- **Readability at 1280×720**: nothing under 16 px except the five 14 px places of the DS README.
- **Ownership**: architect: `BattleScene.js`, `player.js`, `clock.js`, `layout.js`, `battle.css`,
  `dev/battle.js`. Hand agent: `hand.js`, `hand.css`, `dev/battle-hand*.js`. Field agent: `field.js`,
  `field.css`, `dev/battle-field*.js`. HUD agent: `hud.js`, `hud.css`, `dev/battle-hud*.js`. A module may
  add private helper files named `hand-*.js` / `field-*.js` / `hud-*.js`. Never edit another module's files;
  if you need something from another module, it goes through `env.geom` (§2.4) or a callback, and you
  write it in your report.

## 1. Files and the runtime picture

```
src/scenes/battle/
  BattleScene.js   mountBattleScene(host, ctx) → { destroy }       the mount: root, layers, env, modules, keys, overlays
  player.js        createPlayer(env, view, ctx) → player           port of DepictionPlayer: flow, plan waits, cue beats, input states
  clock.js         createBattleClock(settings) → { clock, real }   the scaled scene clock (UiTween.Speed) on stage.createClock
  layout.js        L                                               stage-px constants the ported scripts do not hold
  battle.css       scene root, layers, z-order (+ the baseline's minimal looks)
  hand.js / hand.css     cards in hand, hover, hold and release, drop zones, marks, prediction, refusal line
  field.js / field.css   backdrop, floor, figures, fx (strikes, numbers, ghosts), enemy-turn shade;
                         field/freestep.js: 俊敏's choice marks (§4.8)
  hud.js / hud.css       panels, omen badge, stance lists, stamina tag, corner, speed, piles + list, played,
                         reserve hint, journal / end-turn buttons, banners, result card
dev/battle.js      dev page: enemy × layer picker, auto play
```

```
ctx.source (CoreBattleSource, wrapped by the save recorder)
     │ Frame / AdvanceAuto / EndTurn / TryPlay / Inspect / PreviewFor / RefusalText / GuideText
     ▼
 player.js ── applyFrame(frame) ──▶ field.applyFrame / hud.applyFrame / hand (build|bind)
     │  ── beat calls (lunge, strike, popGuard, banner, flyDrawn …) ──▶ modules (Promise per beat)
     ◀── input callbacks (onRelease, onEndTurn, onSpeed, onJournal, onPile …) ── modules
```

`BattleScene.js` builds `env`, creates the three modules with `createHand(env, opts)`,
`createField(env, opts)`, `createHud(env, opts)`, then `createPlayer(env, { hand, field, hud }, ctx)` and
`player.start()`. The modules never call `ctx.source` state-changing methods and never call each other
directly: geometry goes through `env.geom`, input goes up through callbacks to the player.

## 2. The shared environment `env`

```js
env = {
  OCB,            // window.OCB
  root,           // the scene root element: <div class="ocbg-battle">
  layer,          // { backdrop, field, fx, panels, marks, hud, shade, hand, held, float, over }  (§2.1)
  clock,          // scaled scene clock (§2.2)
  real,           // unscaled clock (§2.2)
  fx,             // effect switches facade (§2.3)
  geom,           // geometry registry (§2.4)
  audio,          // { play(name, delayMs?, mult?) } — safe wrapper over ctx.audio (no-op when missing)
  settings,       // ctx.settings (read speed / shortFx live; only the player writes speed)
  stage,          // ctx.stage (toStage(e) → stage px)
  toStage(e),     // shorthand for stage.toStage
  layerNo,        // ctx.layer (1..7)
  nodeKind,       // 'Battle' | 'Elite' | 'Boss'
  miasmaPercent,  // ctx.miasmaPercent when the app gives it, else null (the writer's corner always says 0)
  L,              // layout constants (layout.js)
}
```

### 2.1 Layers and z-order

All layers are absolutely positioned 1920×1080 boxes inside the root, `pointer-events: none` by default;
a module sets `pointer-events: auto` on its own interactive pieces. Order (bottom → top), from
`ds/project/layout.md` 「重なりの順」:

| layer | z | owner | contents |
| --- | --- | --- | --- |
| `backdrop` | 0 | field | per-layer sky / haze / motes (`BackdropSpec`), horizon at y 556, dark earth below |
| `field` | 10 | field | floor tiles, gap chip, foot lights, figures, extra (behind) enemies |
| `fx` | 15 | field (+hand's hooks) | strike shapes, bursts, grammar marks, shield block, ghosts, rebuild sheet; the target hooks (hand-marks.js: the mark is the figure's in Unity, so the omen badge and the panels stand over it) |
| `panels` | 20 | hud | status panels, omen badge (+ plan), stance lists |
| `marks` | 30 | hand / field | throw line, (invisible) receiver, debug boxes; 俊敏's choice marks (field/freestep.js, §4.8) |
| `hud` | 40 | hud (+hand's guide line) | corner, speed, journal, stamina tag, piles, played, reserve hint, end turn; hand's guide line |
| `shade` | 50 | field | enemy-turn shade below y 790 (`EnemyTurnShadeLayout`) |
| `hand` | 60 | hand | the fan |
| `held` | 70 | hand | the held card |
| `float` | 80 | field / hand / hud | prediction tag (hand), floating numbers (field), banners (hud), refusal line (hand) |
| `over` | 90 | hud / hand / scene | tooltips, card detail, pile drawer, result card, journal overlay |

### 2.2 Clocks (`clock.js`)

`env.clock` is the scene's `UiTween`: one frame loop (from `stage.createClock`) whose time runs at
`scale = BattleSpeed.TweenSpeed(step)` (×1.0 → 0.8, ×1.25 → 1, ×1.5 → 1.2), times `L.FF_SCALE` (4) while
fast-forward is held (review 16: at ×1000 every number of an action lived 2 frames; battle_ui_ux_v2 「早送りは動きだけを短縮し、
数値と位置は同じです」), and `L.SHORT_SCALE` (1000, Unity's `HaltedTweenSpeed`) when `settings.shortFx` is on
or the battle is halted. The scale is read every frame, so a speed change takes effect at once on tweens
and waits already running (as `UiTween.Speed` does).

```js
clock.now                         // scene time in nominal ms
clock.tween(obj, to, ms, ease?, delay?) → Promise   // numeric props of obj, like stage.createClock's tween
clock.run(ms, ease, fn) → Promise // UiTween.Run: fn(easedT) every frame (and once with 1 at the end)
clock.runPaced(ms, ease, fn)      // as run(), but under fast-forward at most L.FF_FLOAT_SCALE (1.5) × the speed: the
                                  // floating numbers and words (fx.floatText), which never hold the flow, stay readable
clock.wait(ms) → Promise          // UiTween.Wait
clock.onFrame(fn) → unsubscribe   // fn(dtScene, dtReal) every frame (hover weights, follow, motes)
clock.setFastForward(on)          // the player's
clock.scale                       // current factor (read-only)
real.wait(ms) / real.now          // unscaled (WaitForSecondsRealtime, input timings)
```

Eases are `stage.EASE` names: `'out' | 'in' | 'inout' | 'linear' | 'smooth'` (Unity's `Ease.Out/In/InOut/Linear`
are the same cubic set). A tween, run or wait started from inside another's callback is kept (review 19: a 払's motes, started
inside its own run, were dropped by the frame's filter and stayed on the screen for good). After the scene is destroyed, pending promises from `tween/run/wait` (and `real.wait`) never
resolve: a destroyed scene's flow stops where it stands and never reaches the source again. Do not put cleanup
that must run after destroy behind such a promise; do it in `destroy()`.

### 2.3 Effect switches (`env.fx`)

A facade over the ported `EffectSwitches` (everything on; the scene has no Inspector):
`fx.on(id)`, `fx.ms(id)` (one item, 0 when off), `fx.stagger(id)`, `fx.wait(id, count)`, and `fx.switches` (the
`EffectSwitches` instance itself, for `EffectPlan.DrawCardStartMs` / `RebuildStartMs` / `WaitMs`). Ids are `EffectId`
values (strings). A module that animates a beat uses the `ms` it is handed by the player; a module that
animates something of its own (hover, grab, dim, snap, receiver fade) takes its length from `env.fx.ms(id)`.

### 2.4 Geometry registry (`env.geom`)

Cross-module positions, in stage px, read live (they follow motion). Each module registers its getters in
its factory; the others call them lazily (never at construction time).

| getter | owner | returns |
| --- | --- | --- |
| `geom.figure(side)` | field | `{ x, feetY, height, width, homeX, chest:{x,y}, head:{x,y}, top, walking, artX0, artX1 }` of `'Player'` / `'Enemy'` now (x includes lunges and walks; `homeX` is its place between motions — already the new cell during a floor walk, so `walking` says to follow `x` then; `width` is the body width the target hooks frame; `artX0`–`artX1` the art's own box across, wider for a tail or a spear — review 21, the stance lists keep clear of it) |
| `geom.receiver()` | field | `{ X, Y, Width, Height }` — the invisible 340×340 receiver over the enemy's home (a `ScreenBox`-shaped object, for `ReceiverDrop.Takes`) |
| `geom.cellBox(cell)` | field | `{ x, y, w, h }` of a floor tile |
| `geom.pileMiddle(kind)` | hud | `{ x, y }` of the draw / discard / exile pile (`PileKind`) |
| `geom.omenBox()` | hud | `{ x, y, w, h }` of the omen badge (for the struck bonus text) |
| `geom.endTurnBox()` | hud | `{ x, y, w, h }` |
| `geom.handCard(cardId)` | hand | `{ x, y }` centre of that card now, or `null` |
| `geom.coversPointer(pt)` | hud | `true` while an open pile drawer / tooltip covers `pt` (the hand then does not hover through it) |
| `geom.omenBox(unit)` / `geom.omenBoxes()` / `geom.omenShown(unit)` | hud | an enemy's badge box; every standing enemy's; whether that badge shows now |
| `geom.statusBox(side, unit)` / `geom.statusBoxes()` | hud | a number panel's box `{x, y, w, h}` (null when hidden); every shown panel `{ side, unit, box }` (a click on an enemy's panel plays a selected card on it; the hover line keeps off the panels). Asked every frame, so it is computed from the layout numbers (`StatusPanelLayout.Height`), never read from the DOM (a layout read per frame slowed the scene enough that a drag's press became a long press) |
| `geom.stanceBox(side)` | hud | the stance list's box over every frame it shows, at its rest place or its high place (§6c; null when the lists are hidden) |
| `geom.journalCovers(pt)` | scene | the journal's drawer (right 30 %) covers `pt` while it is open (outside it the cards can be hovered, §2.4) |
| `geom.journalEdge()` / `geom.drawerRight()` | scene / hud | the journal drawer's left edge while it is open (else null) / the pile list drawer's right edge as it slides (0 closed): the panels, badges and the guide line keep inside what they leave (review 19) |
| `geom.stanceSlot(side)` | hud | the slot at the end of `side`'s stance list (y 470, or the high place's one frame), where a stance just put down goes (§5.11 順 1; null while the lists are hidden) |
| `geom.freeStepBoxes()` | field | 俊敏's choice marks as they stand, `[{x, y, w, h, dir}]` ([] while none shows): the card detail keeps off them |

## 3. The script vocabulary (what the player hands you)

`src/bridge/depictionScript/DepictionTypes.js` (port of `$U/Script/DepictionTypes.cs`). Enum values are their
member-name strings (`UnitSide.Player === 'Player'`). Read the C# for the field comments; the summary:

- `DepictionFrame` — one settled screen: `Outcome`, `Corner {Turn, Floor, ChainIndex, ChainTotal, MiasmaPercent}`,
  `Player` / `Enemy` (`UnitFrame`: `Hp, HpMax, Guard, ShowStamina, Stamina, StaminaMax, NextRecovery,
  ReserveLikely, Elite, HasRange, Range, RangeGlyph, Statuses[StatusChip], ArtId, Down, Stances[StanceRow]`),
  `Omen` (`OmenFrame`: `Visible, Icon, KindLabel, SideGlyph, ValueText, Hit, Statuses[OmenStatus], Disclosure,
  PlanVisible, PlanIcon, PlanKindLabel, PlanValueText, PlanStatuses, PlanSideGlyph, PlanChanged, PlanTag`),
  `Hand[CardFace]`, `StanceHint`, `StanceDivider`, `Floor` (`FloorFrame`: `Cells, PlayerCell, PlayerSize,
  EnemyCell, EnemySize, Gap, AimCells, DenseCells, MoveCells`), `Piles` (`PileFrame`: `Shown, Draw, Discard,
  Exile, DrawCards, DiscardCards, ExileCards`, `CardsOf(kind)`), `StanceListsShown`, `Played` (`PlayedFrame`:
  `Shown, Attack, Guard, Skill, Stance, Last, FollowUp`), `EnemyTurn`.
- `CardFace` — `Id, Name, Cost, PrintedCost, CostDrop, Kind, Aim, Affects, TypeLabel, Description, ValueText,
  TraitText, TraitLit, RequiredRange, RequiredRangeGlyph, ReachHint, Values[CardValue{Kind, Number, Word}],
  TextLines, Lasting, Plain, TraitTop, TraitBottom, LitOn, HpLossOn, HoverLine, ReachCells, Detail
  (CardDetail{Name, KindLine, Body, Terms}), StanceHintAfterPay, Terms[TermMark]`, `LitFor(i)`, `HpLossFor(i)`.
- `DepictionEvent` — `Order, Kind (TurnStart|PlayCard|TurnEnd|EnemyAction|NextOmen|Defeat), Title, CardId,
  Aim, PreviewText, PlayedTo (PileKind), Systems[FxSystem], Cues[Cue], After (DepictionFrame)`.
- `Cue` — `Kind, Target, Source, Amount, Text, Intensity, HpAfter, GuardAfter, StaminaAfter, StaminaMax,
  RangeAfter, RangeGlyphAfter, StacksAfter, RebuildAfter, System (StrikeSystem), Fx (FxSystem|null),
  TraitAccent, Pushed, PlayerCellAfter, EnemyCellAfter, GapAfter, CellsMoved`. `Cue.Unchanged === -1`.
- `PlayVerdict`, `DropZone`, `CardAim`, `DepictionText` (`YourTurn` あなたの番, `EnemyTurn` 敵の番,
  `OutcomeText(o)` 勝ち / 負け, `Pushed` 押し出し, `RequiredZone(aim)`, `WrongZoneText`, `Find`).

**Several enemies.** Unity's script depicts `Enemies[0]` only. The JS writer's documented extension
(`docs/port-notes/depiction-multi-enemy.md`: `Frame.Enemies`, `Frame.Omens`, `FloorFrame.Enemies`, `Cue.Unit`,
`CardFace.ReachOn` / `TargetUnit`, the optional target of `Inspect` / `TryPlay` / `PreviewFor` / `RefusalText`) writes
2–3 enemies; the scene draws every one with its own figure, panel and omen badge, and plays each cue on the enemy its
`Cue.Unit` names. With one enemy every call is the one Unity makes. (`field.setExtras` is kept for the contract and
draws nothing.)

## 4. The player (`player.js`) — port of `DepictionPlayer`

### 4.1 Input states

| state | meaning | hand interactable | end turn | fast-forward |
| --- | --- | --- | --- | --- |
| `opening` | before the first automatic run | no | no | no |
| `auto` | `RunAutomaticEvents`: turn start, the enemy's turn, next omen, falls (busy) | no | no (a press is swallowed: the mash window, §4.5) | Space or a click: one press to the end of the event playing, held while held |
| `idle` | `source.WaitingForPlayer`; sub-states hovering / holding live in the hand (`hand.isHolding()`); 俊敏's choice (`player.freeStepPending`, §4.8: the cards lift on hover but none is taken) | yes | yes (not while holding, not while an overlay is open, not in the mash window; it lets 俊敏's step go) | no |
| `resolving` | a player-asked event plays (`PlayAccepted`, the end turn's `PlayThenContinue`) or a refused card returns | no | no (swallowed, as in `auto`) | as in `auto` |
| `over` | `source.Finished`: result card, then `ctx.onFinished()` once | no | no | no |
| `halted` | the source threw (a bridge / core bug): input and events stop, `console.error`, then `ctx.onError(err)` if the app gave one | no | no | no |

`player.state` is readable; `player.onState(fn)` notifies changes (the dev page and auto play use it).

### 4.2 Flow (1:1 with `DepictionPlayer`)

- `start()` = `Start`/`Restart`: `SnapStanceGlows(frame)`, `applyFrame(frame, {first:true})`, snap the enemy home,
  then `runAutomaticEvents()`.
- `runAutomaticEvents()`: `while (!Finished && !WaitingForPlayer) { ev = ctx.source.AdvanceAuto(); await playEvent(ev); await clock.wait(EffectFlow.AfterAutoEventMs) }`;
  then either the result card (`over`) or `idle` (+ `hand.setInteractable(true)`, `refreshPlayable()`).
- `playEvent(ev)` = `PlayEvent`: `BeginPlan(ev)` (the queue of `EffectPlan.StepsOf(ev)`), omen acting light off,
  shade up when `ev.After.EnemyTurn` is false, the turn banner, the omen blink, each cue (`playCue`), the enemy-turn
  banner, enemy home update + return, poses idle, `EndPlan()`, `applyFrame(ev.After)`.
- `holdBeat(id, play, settle, count = 1, played)` = `HoldBeat`: takes the plan's next step (logs a mismatch with
  `console.error`, as Unity's `Debug.LogError` — never throws), waits `EffectPlan.WaitMs(step, switches)` while
  `play()` runs; a step with `LeadMs` holds the lead only and lets the beat play on. `play` is a **thunk**
  returning a Promise. `effect(id, play, settle)` runs a beat beside the flow (`Effect`).
- Release: the hand calls `opts.onRelease(cardId, zone)`. The player calls `ctx.source.TryPlay(cardId, zone)`
  (returns `{ verdict, played }`). Accepted → `playAccepted(cardId, ev)`: `BeginPlan`, `hand.release(cardId, ev, receiverCentre | null)`
  under `holdBeat(CardRelease)` (its `played` callback flies the ghost to `ev.PlayedTo` and settles the fan),
  every cue, `applyFrame(ev.After)`, `wait(AfterPlayerEventMs)`, `runAutomaticEvents()`. Refused →
  `hand.showRefusal(ctx.source.RefusalText(cardId, verdict))` and `hand.returnToHand(cardId, shake)` with
  `shake = verdict !== WrongZone && zone !== None` (Unity's rule).
- End turn: the hud's plate or X (§4.5; Enter never ends the turn, Space only fast-forwards) → `requestEndTurn({ queue, press })`
  → `PlayThenContinue(ctx.source.EndTurn())`. A press (`press: true`) while the flow runs, or inside the window the press
  before it opened, is swallowed and opens a window of its own (review fixes: clicks every 100 ms, then every 0.4 s, to hurry
  the enemy's turn ended the next turn 106 / 360 ms after it opened). Review 17 (presses every 0.65–0.8 s slipped between a
  fixed 600 ms window and the turn's 500 ms guard and ended the next turn 0.6–1.1 s after it opened): the window follows the
  press rhythm — `L.END_TURN_MASH_RHYTHM` (1.3) × the interval since the press before, at least `L.END_TURN_MASH_MS` (600),
  at most `L.END_TURN_MASH_MAX_MS` (1500) real ms — so presses that keep a rhythm under 1.5 s are swallowed however long they
  go on while the flow runs. A press in a turn's first `L.END_TURN_OPEN_GUARD_MS` (500 real ms after the player is asked again
  following a turn start) is swallowed too; the first press after a pause ends the turn. Review 21 (presses every 0.7–1.4 s after
  the turn opened slid the window on for good and the turn could not be ended, with no feedback): in the player's own wait a
  press may slide the window on, but never past `L.END_TURN_MASH_MAX_MS` (1500) after the player was last asked (any wait, a
  turn start or a play) — from then on every press counts — and a swallowed press shakes the plate once
  (`hud.endTurn.refuse(L.END_TURN_REFUSE_MS)`, 6 px ×2, dimmed for that moment; measured with presses every 0.7–1.4 s: the turn
  ends 1.6–2.0 s after it opened). The automatic player's
  `requestEndTurn()` carries no `press`. `player.onEventEnd(fn)` is called after every screen event has played with its
  after-wait (the scene's one-press fast-forward ends there); `player.eventEnds` counts them and `player.afterWaiting` says an
  event's after-wait runs now (review 19: a press in that pause carries on to the end of the next event; §4.5).
- An enemy action the writer splits into events of its own (`…（2 撃目）`, `…（詰める）` / `…（下がる）`; review 21, battle_ui_ux_v2 §5.12
  「踏み込みと止めは 1 回」): when an `EnemyAction` event's last cue is the enemy's `Hit` on the player, its `OmenSpend` and
  `EnemyReturn` beats are held (`heldEnd`; the plan's steps are taken, no wait) and the next event is fetched before the
  after-wait. One that goes on with it (same `Cue.Unit`, title = the first event's title + 「（」) plays after
  `CONTINUE_PAUSE_MS` (80, 「チップの着地の 80 ms 後に 2 撃目」) from where the enemy stands — its lunge is not taken again —, its
  side label (退き撃ち's 「+3 温存」) waits for its own number, and the badge keeps the omen it acts on (`hud.applyFrame(…, { keepOmen })`);
  anything else first plays the held end: the label alone if no number took it, the badge spent, the step home. twin_blade's
  二段斬り: one step in, the badge lit through both blows, one step back, ~2.0 s (was ~2.8).
- `applyFrame(frame)` = `ApplyFrame`: `hud.applyFrame(frame)`, `field.applyFrame(frame)`, the stance glows
  (`StanceGlow.Change` → `field.fadeStanceGlow` beside the flow, `ApplyStanceGlows`), then the hand:
  `hand.sameHand(frame.Hand) ? hand.bind(frame.Hand) : hand.build(frame.Hand)`, `hand.layout()`; then
  `hud.reserveHint(held ? held.StanceHintAfterPayFor(k) ?? frame.StanceHint : frame.StanceHint)` and
  `hud.status('Enemy', k).showHpLoss(held ? held.HpLossFor(k) : 0)`, `k` the enemy slot the held single-target card frames
  (port notes 「Holding a card」; the stamina's 「▸ n」 is `CostFor(k)` and the card's circle `CostFor(k)` / `CostDropFor(k)` too —
  review 16: they kept the face's own cost over a dearer enemy, and the release charged more than the card said). The hand is
  rebuilt only when the ids differ.
- `refreshPlayable()` = `RefreshPlayableLook` with battle-visual-v1's sheet (review fix; Unity dims on any refusal and lights the
  whole hand while busy): `hand.refreshPlayable(dimOf, guide)` where, while the player waits, `dimOf(id)` is
  `Inspect(id) ∉ { Accepted, OutOfRange }` — the sheet is for stamina (and 呪縛), not for reach, which the hover's panel, the
  steel hooks and the unlit floor say (§5.1 「カードに黒い板は重ねません（板はスタミナが足りないときだけです）」) — and while the
  flow runs (`auto` / `resolving` / over) every card is dark (§6.2 敵の番, battle_ui_ux_v2 §3.5 「敵フェーズ中・演出中 →
  手札全体を暗くし」); a card built mid-flow (the turn start's draw) comes in under that sheet, and the card being played is
  skipped. `guide = waiting || Finished ? ctx.source.GuideText : ''` (the refusal line, when up, keeps the guide quiet).
- Every frame: `followFigures()` (= `FollowFigures`/`FollowPanels`): `hud.follow({ playerX, enemyX, enemyTop, cells, enemySize, units, snap })`
  from `geom.figure(...)` home x — its current x while it walks on the floor (`geom.figure().walking`; review fix: MoveOnFloor
  sets the home to the new cell as the walk starts, and the panel raced up to 170 px ahead of the hero) — and the frame's floor; the floor reach light (`UpdateFloorReach`): the face hovered
  (not busy) or held, aimed `Single`, lights `face.ReachCells` via `field.showReach` / `field.hideReach`.

### 4.3 Cue → calls (the beat map)

`T` = the cue's target side (`'Player' | 'Enemy'`), `S = hud.status(T)`, `F = field`, `chest/head = geom.figure(T).chest/head`,
`numberAt = labelAt = chest` and every float rises `chest.y − head.y` (19 % of the figure's height) in 600 ms (ease-out):
battle_ui_ux_v2 §5.0 and the DS Notices 「胸（65%）から頭（84%）へ、600 ms」 (review 19; Unity starts 40 px over the chest, rises 96 px
over 1000 ms, and on the hero and a 2-cell elite carried the number past the head). A float may carry a side label after its number
(`o.side`, 16 px body ink, §5.0 「主数字 + 脇ラベル」): a fired trait's 「+5 間合い2以上」 or the wall's 「壁」.

| cue / event | calls (in order; `HB` = holdBeat, `E` = effect beside the flow) |
| --- | --- |
| any with `EffectGrammar.MarkOf(cue) === 'Shape'` | `E(GrammarMark, F.mark(T, {fx: cue.Fx, intensity, ms, crestLight}))`; a 型 (`Form`) cue lights the stance glow from `ev.After` |
| `... === 'ChestHeave'` | `E(BreathHeave, F.heave(T, ms))` |
| `GuardReset` | `S.setGuard(cue.GuardAfter)` |
| `StaminaChange` (gain) | `HB(StaminaRecover, S.lightPips(after, max, ms), settle S.setStamina, count = Amount)`; sound `pip` per pip |
| `StaminaChange` (spend) | `S.setStamina(after, max)`; `E(StaminaChange, S.popPips(ms))` |
| `StatusChange` | `beat = StatusBeat.Of(cue)`; `HB(beat, S.playStatus(cue.Text, cue.StacksAfter, beat, ms), settle S.setStatus)` |
| `DrawHand` | `drawHand(ev.After.Hand, Amount, RebuildAfter)`: maybe `HB(PileRebuild, F.ghost(discard→draw sheet))`, `hand.build(faces)`, `HB(CardDraw, hand.flyDrawn(first, rebuildAfter, …), count)` |
| `OmenShow` | `hud.syncOmenLook(ev.After, unit)` (its swatch and move arrow as the event's frame leaves them), `hud.omen.bind(ev.After.Omen)`; `E(OmenShow, F.drawAim(ev.After, unit))` — the floor takes that enemy's cells from the frame now (review 21: the first omen of a 2–3 enemy opening drew every enemy's stripes, the next enemy's 240 ms before its badge; the others keep what they showed until their own omen) and the newly aimed ones are drawn left to right, 30 ms a cell (§5.8 提示; scene addition); `HB(OmenShow, hud.omen.popIn(ms))` — the badge (and its plan) drops 24 px into place and fades in (§5.8 「頭上へ降りる」, §5.16 順 1; Unity scales 0.6 → 1); sound `omenDrop` |
| `TraitFire` | review 19: a stance set (`構え・name`) — `HB(TraitFire, wait)`, the player's card having flown to the stance list (`hand.release`), an enemy's omen badge flying to the enemy's list beside it (`hud.flyOmenToStances(unit, L.STANCE_FLY_MS)`, §5.11 順 8); a fired trait of the card played / the enemy's action (§5.9 順 1 / 順 12) — no line over the head: the card's lamp swelled 1.4× and its condition word left with it (`hand.release(…, { traitWord })`), an enemy's condition word swells inside its badge's right end (`omen.traitWord(word, L.TRAIT_WORD_MS)`; review 21: it hung half outside the badge and the next badge painted over it — inside, over the swatch and the hit mark for its moment, the badge raised over its neighbours), and the next number takes the side label 「effect condition」 — the condition by its one-word name (review 21, §5.9 順 1: 「間合い」「温存」「連動」「予兆」, 「出血中」 for 相手出血, not the lamp line's 「間合い0」「残4」), a named effect as itself (「+6 重撃」 with the core's `HeavyBlowPower`, 「転換」, 「追撃」) (a Guard bonus the next Guard gain, any other the next HP number; with none, it rises alone as a word at the event's end, unless the blow whiffed); anything else (構え停止・ / 構え復帰・, a stopped move) — `HB(TraitFire, burst + F.floatText(labelAt, cue.Text, role, 36) + wait)` |
| `Slash` | `attack(ev, cue)`: `HB(AttackLunge|EnemyLunge, F.lunge(attacker, 46, ms))` (not again for the second event of a held enemy action, §4.2), `E(TraitAccent)` if `cue.TraitAccent`, `HB(StrikeShape, F.strike(T, …))`, `HB(HitStop|HitStopStrong, wait)`, shake when strong; if `HpAfter !== Unchanged`: burst, `F.floatText(numberAt, '−' + hpLost, 'ink', NumberFont(intensity), { side })` — signed (review 21, §5.0 「−18」, the DS's floatAt('hp', '−' + n); Unity writes the bare amount) — `side` the trait's label and then the blow's multiplier (below) — `hpLost` the HP the blow took (its Amount, but on a killing blow the HP that was left: the tag's and the wick's number, §5.0; review 19), `E(HitFlash, F.flash(T,'white'))`, `E(TargetRecoil, F.recoil(T, strong))`, `drainHp`, then `HB(AttackReturn, F.returnHome('Player'))` unless a move follows |
| `GuardGain` | `HB(GuardGain, burst guard + F.floatText(numberAt, '+n', 'guard', NumberFont, { side }) + S.popGuard(after, ms))` — `side` a Guard trait's 「+3 温存」, also when the trait fired in the first event of a held action (§4.2); sound `guardUp` |
| a blow's multiplier | review 21 (battle_ui_ux_v2 §5.0 「−12 ×1.5 脆化」, §5.6 順 7, §5.10 強化 「×1.5 強化」; Unity has none): at `beginPlan` each blow (a `Slash` and the `GuardBlock` / `Hit` on the same target after it) is matched with the event's `StatusChange` that spends 脆化 on the side struck or 強化 on the striker — the nearest blow of that pair, the one before it, else the one after (the writer spends 強化 after its blow, and opens a second blow's event where its 脆化 is spent). The number that blow shows (its `Hit`, else its `GuardBlock`, else its `Slash`) carries 「×1.5 脆化」 / 「×1.5 強化」 after any trait label (one per blow, 脆化 first; the factors are the core's `FragileMult` / `EmpowerMult`) |
| `RangeSwitch` on the floor | (review 19) the recount counts to the enemy the floor's N is read to — after a Tab at rest, that enemy's `Gap` / `Cell` from the event's frame when this is the event's last move (`recountTo`; the writer's `GapAfter` is the nearest's); `cue.Pushed` → `E(PushMark, F.floatText(labelAt of the side moved, word, 'ink2', 36, { track }))` — `word` 「引き寄せ」 (scene text) when the cells move the figure nearer the one who moved it, else `DepictionText.Pushed` (review 21, §5.7 順 6; the cue carries no way) (the enemy's head when a push moved an enemy, `Cue.Unit`); `HB(FloorMove, F.moveOnFloor(cue, cells, ms), settle F.snapOnFloor(cue), count = cells)`; `HB(GapRecount, F.recountGap(...))` |
| `StanceCue` | `HB(ReservePips, hud.stamina.glowReserve(ms) or wait)`; `HB(StanceCue, hud.endTurn.pop(200) + burst + F.floatText(numberAt, cue.Text, 'guard', 48) + S.popGuard(after))`; then the 構えの予告 goes (`hud.reserveHint('')`: it has been granted; battle-visual-v1 §4.9, review 19); sounds `breath` → `reserve` |
| `DiscardHand` | `HB(HandDiscard, hand.discardAll(pileMiddle(Discard)), count = hand.count())`; sound `discard` per card |
| `EnemyWindup` | hand off; an elite's / boss's second action (its first omen spent, its plan standing): `omenOf(unit).promotePlan(150)` beside the motion — the plan drops into the lower slot and turns solid (battle_ui_ux_v2 §3.10, scene addition; review 16: with no reach glyph — `PlanSideGlyph` is the v4.2 reach text — and, as the frame carries no hit mark or aimed cells for a plan, no hit mark, swatch or stripes: §8.2); `setActing(true)` on that badge; `E(OmenExecute, hud.omen.flare(ms))`; `HB(EnemyMotion, F.enemyMotion(cue.System, ms), settle pose Act)` |
| `Defeat` | enemy: `E(OmenSpend, hud.omen.fadeOutAll(ms))` and `F.retireAimOf(unit)` — its aimed / dense / move cells leave the floor with the badge (§5.8's 120 ms fade; review 17: they stayed striped through the whole fall); `HB(Defeat, F.fall(T, ms), settle F.setDown(T, true))`. Review 17, battle_ui_ux_v2 §5.15 順 5 (scene addition; Unity draws one enemy): when other enemies stand, `F.fall(T, ms, unit, { keepFoot: true })`, then `F.fadeFoot(T, L.FALL_FOOT_MS)` (the foot light goes, the cell empties, 300) and `S.greyOut(L.FALL_GREY_MS)` (the panel turns grey, 300), holding the flow — 800 → 300 → 300 |
| `SideBonusMiss` | `HB(SideBonusMiss, F.floatText(omenBox − 64, cue.Text, 'whiff', 40, {struck, rise:-50}) + hud.omen.strikeSide(ms), settle strikeSideNow)` |
| `GuardBlock` | `S.setGuard(after)`; `after == 0` → `E(GuardBreak, S.crackGuard(ms))` else pop; `E(GuardNumber, F.floatText(at, 'Guard −n', 'guard', L.GUARD_NUMBER_SIZE))` (34 px, battle-visual-v1 §3.1 / §5.5; Unity 44) on the numbers' own path (`numberAt`, the chest, §5.0): when an HP number follows, anchored 12 px to the blow's side of the chest (the pair `Guard −n` · 24 px · HP centred on the chest, the tag over the shield, the number on the figure's back), alone centred (review 17: over the old opaque shield the pair rose from 113 px over it, ~145 px above every other blow's number, and its 「beside the shield」 fallback put the tag on the next enemy's head); `HB(GuardBlock, F.shieldBlock(T, n, ms, unit, { broken: after == 0 }))`; sounds `hitGuard` / `guardBreak` |
| `HpChange` | `E(DamageNumber, F.floatText(numberAt, Text + ' ' + signed, Amount>0 ? 'heal' : 'omen', 40))` — `+n` / `−n` (the typographic minus); `drainHp` |
| `Hit` | `F.floatText(numberAt, '−' + hpLost, 'omen', NumberFont, { side })` — after a Guard block on the same figure beside its "Guard −n" tag, 24 px further out on the figure's back, riding at the tag's height as the tag rises (`followY`; §5.6 順 3 「順 2 と横に 24 px 並ぶ」 — review 16: a number with its own rise trailed the earlier tag), or on the tag's other side when the stage has no room there — `E(HitFlash, F.flash(T,'white'))` (battle_ui_ux_v2 §5.5 順 3 「相手が白く光る」; review 19 — Unity's `BattleTheme.Omen` turned a guarded hit's figure flat violet), `E(TargetRecoil)`, shake if strong, `drainHp`; on the player: `HB(OmenSpend, hud.omen.fadeOut)`, `HB(EnemyReturn, F.returnHome('Enemy'))` — held when it is the last cue of the enemy's event (§4.2); sound `hitBody` |
| a wall's `GuardBlock` / `Hit` | review 19, battle_ui_ux_v2 §5.7 順 7 (the writer marks no wall cue): the cues of the event `CoreScriptWriter.OpenIfBoundary` opens for a player shoved into the end of the line (title 「壁に当たる」), and a blow on an enemy after its push in the player's card event (§5.4: the card's blow comes first). `E(F.wallHit(T, dir, 80))` — the floor's end lights 80 ms (`floor.edgeFlash`), or the figure in the way flashes white, and the pushed figure takes 打's ripple from that side; the number reads 「n 壁」 (side label), on the Guard tag when Guard took it all; sound `hitBlunt` for `hitBody` |
| idle, nothing playable | once per wait in which `Inspect` accepts no card of the hand, `E(hud.endTurn.flash(L.END_TURN_FLASH_MS))`: the plate brightens once (battle_ui_ux_v2 §3.5 「ターン終了ボタンが 1 回だけ明るくなる」; scene addition, Unity changes only the guide) |
| `drainHp(S, hp)` | `HB(HpDrain, S.animateHp(hp, ms), settle S.setHpKeepingTrail(hp), played → E(HpTrail, S.animateTrail(ms), S.snapTrail))`; a blow that lowers the player's HP under 15 pulses the low-HP vignette (`F.pulseLowHp(200)`, §5.6 順 5) |
| `TurnStart` event | `HB(TurnBanner, hud.banner(DepictionText.YourTurn, 'player', ms))`; after its `DrawHand` cue (or its cues, when it draws nothing) `E(OmenBlink, badge.blink(ms))` on every badge already standing, `L.OMEN_BLINK_STAGGER_MS` (120) apart in the enemies' action order (§5.1 順 8 「順 6 の後」 — review 16; Unity blinks right after the banner; `Frame.Omens` is in that order); heartbeat sound ×3 when the player's HP < 15 (sound.md: the turn start only) |
| `TurnEnd` event (after its cues) | the enemy's turn shows as its banner comes in — `showEnemyTurn(true)`: the shade below y 790, the plate at 40 %, the 構えの予告 off (battle-visual-v1 §4.9 / §5.5; review 19 — Unity shades only once the TurnEnd frame lands after the banner); `HB(EnemyTurnBanner, hud.banner(DepictionText.EnemyTurn, 'enemy', ms))`; sound `foeTurn` |
| result | a loss first (battle_ui_ux_v2 §5.19 順 1〜4; review 19, scene addition — Unity goes from the hero's fall straight to 負け): `field.dimFeet(600)` (the foot lights shrink out), `hud.lossInk('力尽きた', 800, 300)` (the ink (`--flood`, `#06090c`) floods up from the bottom, then 「力尽きた」 in the display face at 64 px, its letters drawing apart), `clock.wait(1000)`; then (review 21) `hud.resultCard(outcome, ms, info, { onButton })` — the design system's end paper: a win's (勝ち, whom it beat, the turns and the HP kept, the chain's fight; scaling 0.94 → 1 over the ResultCard ms, §5.18 順 2) or a loss's result screen (§2.5, 922×626, rising 400 out as 「力尽きた」 gives way, `hud.lossWordOut`), §4.6; a win goes on after `L.RESULT_HOLD_MS`, a loss waits for 「手記へ」 (or a click / Enter / Space / Esc, `skipResult`; the dev page's `ctx.auto` holds and goes on); then `ctx.onFinished()` once |

Every float `playCue` raises over a figure carries `track` (the x of that figure now): it rides along with the figure
(a shove carries its number and 「押し出し」 with it). A float that is a word or a line (a trait's cue, 「構え・…」, 「空振り」,
「押し出し」) stands on the same black 80 % tag as the numbers, fitted close (review fix: bare text with a 2 px shadow read at
1.1–1.9 : 1 over the bright skies of layers 1, 2 and 4; battle-visual-v1 §2.1); numbers keep the DS FloatNum tag with its icon.

The low-HP vignette (battle_ui_ux_v2 §5.6 順 5; scene addition, Unity has none): while the player's HP is under 15 a crimson
vignette lines the screen's edge at α 0.25 (`field.setLowHp`, from every applied frame), and each blow that lowers it pulses it
0.5 → 0.25 over 200 ms. The spec's low heartbeat with it is not played: sound.md keeps the heartbeat to the turn start (3 times,
「常には鳴らしません」), and a blow's two sounds already fill the action's budget of two.
A `TraitFire` whose text is a `CoreText.MoveBlockedLine` (呪縛 / 鉤爪 stopped a move) rises grey (`ink2`) with no light
(battle_ui_ux_v2 §5.7 順 9 / 11).

Colour roles passed to `floatText`/`burst`/`flash` (`'ink' | 'ink2' | 'omen' | 'guard' | 'warm' | 'heal' | 'whiff' | 'white'`)
map Unity's `BattleTheme` names; the field module maps them to tokens (`omen` → `--phosphor`/`--violet`
(enemy side), `guard` → `--guard`, `warm` → `--amber` (player side only; on an enemy target use `--ink`), `heal` → `--hp-text`,
`whiff` → `--miss`, `ink2` → `--ink-muted`).

### 4.4 Sounds — who plays what

The two-sounds rule (`ds/project/sound.md`) is per action. **The player** plays the result sounds of beats
(`hitSlash/hitThrust/hitBlunt` by `cue.System`, `hitGuard`, `guardBreak`, `hitBody`, `guardUp`, `seal` on a status
put on the opponent, `stance` on a 型 cue, `breath`+`reserve`, `foeTurn`, `omenDrop`, `pip`, `rebuild`,
`heartbeat`) through a per-event budget of two. The budget is not first come, first served: at `beginPlan` the player
counts the event's essential sounds (a blow's system hit, the body's hit on the player, a Guard's block or break —
the action and its result, sound.md row 26), and an incidental one (`guardUp`, `seal`, `stance`, `breath`, `reserve`)
plays only on a slot those leave (review fix: an enemy's own `guardUp` took the deciding blow's `hitBody`). The
journal plays its own `journalOpen` / `journalClose` (docs/AUDIO.md); the scene plays none. **The hand** plays its own interaction sounds (`hover`, `grab`,
`frameIn`, `frameOut`, `noReach`, `cancel`, `refusal` ×2, `releaseAttack/Defense/Skill/Stance` by
`face.Kind`, `draw` per card in `flyDrawn`, `discard` per card in `discardAll`). **The hud** plays `uiClick`
for its buttons. **The field** plays nothing.

### 4.5 Keys and pointer (scene level, `BattleScene.js` → player / modules)

| key | in `idle` | in `auto` / `resolving` |
| --- | --- | --- |
| `1`–`9` | `hand.select(i)` — the click path (§6a) | — |
| `Enter` | with a selection: `hand.confirm()` on the frame the screen shows as focused; nothing without one (§3.4: Enter never ends the turn) | an Enter that comes while the play it confirmed resolves is kept, and confirms the card the scene selects next (battle_ui_ux_v2 §3 入力の端 「選択中に Enter を 2 度押す: 1 度目で確定、2 度目は自動選択された次の札の確定」; review 21) |
| `Space` | — | fast-forward: a press runs to the end of the event playing now (§3.4 「次の 1 手の終わりまで」) — pressed in an event's after-wait, to the end of the next event, and one that has run under `L.FF_MIN_RUN_MS` (300 real ms) when its event ends runs on to the end of the next (review 19: taps in the 350 ms pause after an enemy action were spent on the pause) — held it stays on while held |
| `X` | end turn (battle_ui_ux_v2 §3.4) | swallowed (the mash window, §4.2) |
| `Tab` / `Shift+Tab` | with a single-target selection: the focus to the next / previous standing enemy's frame; at rest (two or more enemies): the floor's N to the next enemy, and a selection then starts framed on it | — |
| `I` | the selected card's detail | — |
| `J` | journal: `ctx.openJournal(layer.over, { enemyIds, onClose })` | same |
| `Esc` | `hand.cancel()` → else a chip's / stance's pinned detail (`hud.closePinned()`, review 21) → else `hud.closeDrawer()` → else close journal → else, while 俊敏's choice waits, the focus to 留まる (`player.stayFreeStepFocus()`, review 21); nothing more (§3.4; review 19: the settings cannot be opened in a battle, battle-visual-v1 §4.1). With `ctx.settingsInBattle` (an app's opt-in) the old last step comes back: `ctx.onSettings()` **on the key's release**, a held Esc's repeats doing nothing | same order |
| `←` / `→` | only while 俊敏's choice waits (§4.8): ← picks back, → picks forward (§3.4 「← / → で 1 マス後ろ / 前を選びます」; review 19 — they walked a focus along back / 留まる / forward). Review 21 (a repeat: after the pointer focused 前へ, → went to 留まる and Enter stayed): the key of a way always picks that way, pressed again it keeps it; only the key of a way not open falls to 留まる, and Esc takes the focus to 留まる. Otherwise the keys are not the scene's (§3.4: 「それ以外で ← / → は使いません」) | — |
| `Enter` (俊敏) | while the choice waits: chooses the focused mark (nothing with no focus) | — |

Pointer: a click (or holding the primary button) anywhere outside the hand during `auto` / `resolving` fast-forwards as Space does
— one press to the end of the event, held while held (the design system's flow bar did the same with a click). The end-turn plate
counts as the arena there (it has nothing to do while the flow runs; §3.5 「早送りだけ受ける」), and its press also marks the mash
window. A press within `L.DOUBLE_CLICK_MS` (500, the OS double-click time) of a press or release made while the player was asked
does not fast-forward (review 16: the second click of a double-click that played a selected card on its target armed the
one-press fast-forward, and the player's own strike, hit-stop and number vanished). Review 19 (a repeat finding): battle-visual-v1
§4.1 「設定・セーブの画面は戦闘中に開けない」 — the speed switch is the battle's one setting — so the scene draws no settings gear and
Esc does not open the settings. `src/app/main.js` still passes `ctx.onSettings` (`openOverlay('settings', { inBattle: true })`); the
scene calls it only when the app also passes `ctx.settingsInBattle: true`, which brings the gear (12 px left of the speed switch,
tooltip 設定（Esc）) and Esc's last step back. The game passes it (2026-10-21): since tier1 R1-16 (2026-10-12, #462) a fight is
saved move by move and comes back where it was closed, so the settings offer 「保存して閉じる」 in a fight too — §4.1's reason
(「戦闘は保存されない」) no longer holds; the two documents disagree and the conflict is for main to settle. The scene's default
(no flag: no gear) is unchanged. The app's own QA that clicks `.ocbg-battle-settingsbtn` (`dev/app/saveload2/a6-settings.mjs`,
`a14-sizes.mjs`, `dev/app/saveload3/b8-battle-settings.mjs`, `b11-misc.mjs`) needs that flag, or to drop the step:
the app's call (one line in `hud.js` and one in `BattleScene.js`), not the scene's.

While the journal alone is up the scene clock holds still and no card is pressed, grabbed or played, but the cards
still lift on hover outside the journal's drawer, on real time (battle_ui_ux_v2 §2.4 「開いている間はカードをホバーでき
るがプレイできません」): `hand.setBlocked(on, hover)`. The settings' modal holds everything. The pile list (review fix,
§3.5 「手記ドロワー / 一覧ドロワー / 詳細ポップが開いている → 掴めない。ホバーで持ち上がるだけ」; Unity lets the cards be
taken under it) blocks the cards the same way without holding the clock or the turn (the plate and X still end it), drops a
selection as it opens, and says 「閉じるとプレイできます」 beside its close button (`JournalBook.ClosingHint`, the journal's own line).
Review 16 (a dragged card was refused with no feedback while the guide still asked for a release): a press on a card or its digit
plays `refusal` and puts 「一覧を閉じるとプレイできます（Esc）」 (scene text) on the refusal line. Review 21 (the open list said
「close it to play」 three times at once): the guide line is quiet while the list is open — its close button and 「閉じるとプレイ
できます」 beside it say it (§3.5) —, and a refusal line that names the list, the journal or 俊敏's choice goes as soon as that is
closed or chosen (`player.overlaysChanged`, `clearRefusal`; it stood 3 s more and asked for what was done) (`hand.setBlocked(on, hover, onTry)` → `player.refuseWhileDrawer()`). Review 17 (with the journal open a
pressed card silently did nothing and the guide still asked for a release): the journal the same way — the guide reads
「手記を閉じるとプレイできます（Esc）」 (scene text) and a press on a card plays `refusal` with that line (`player.refuseWhileJournal()`,
`player.setJournalCheck`). The guide's order: the journal, then 俊敏's choice (the pile list does not stop it, §4.8), then the pile list (quiet).

### 4.6 Speed, 演出短縮, chain, result

- Speed: `settings.speed` 1 | 1.25 | 1.5 ↔ `BattleSpeedStep` Slow | Normal | Fast. The hud's button calls
  `opts.onSpeed()`; the player sets `settings.speed` to `BattleSpeed.Next`, plays `uiClick`, calls
  `ctx.onSettingsChange?.(settings)` if the app gave one, and `hud.speed(BattleSpeed.Label, BattleSpeed.Chevrons)`.
  The clock reads the new speed on its next frame.
- 演出短縮: `settings.shortFx` → the clock's scale ×1000 (all tweens and waits ≈ 1 ms; input thresholds unchanged).
- Chain: `ctx.chain = { index, total }` (else the frame's `Corner.ChainIndex / ChainTotal`); the corner shows
  dots — one 12 px circle per fight, unboxed like the other items (battle-visual-v1 §4.1). Review 19: 「済んだ戦いを塗る」 — only the
  fights already won are filled (`i < index`, the app's `index` being the 1-based fight under way), so the first of three shows ○○○
  and a dot fills as the next fight comes in (§5.18 順 7); Unity's `CornerInfoView` fills the one being fought too (`i ≤ ChainIndex`).
- Optional ctx extras the app may give: `onSettingsChange(settings)` (called after the speed button changed
  `settings.speed` in place, so the app can persist it), `onError(err)` (the source threw; see `halted`), `auto` (§4.7).
- Chain switch (review 21; battle_ui_ux_v2 §2.5 切替バナー / §5.18 順 6〜7, Unity cuts straight to the next fight): a chain's second
  fight and on opens on the dark board with 「n 戦目」 and the enemy's name (the roster's, by `UnitFrame.ArtId`) at the middle
  (`hud.chainBanner`, 300 in, 700 held, 400 out), the enemies coming out of the haze as it goes (`field.appearEnemies(400)`), and
  only then the turn start; the corner's dot has filled for the fight won. Off with the TurnBanner effect switch.
- Result (review 21: a 520×220 box with one word read as a debug overlay, and a loss put 「負け」 and 「力尽きた」 up together): the
  design system's end paper (bundle.css `.ocb-end` / `.ocb-end-paper`: the board under the ground's 62 % scrim, washi, washi ink).
  A win: 720 wide, `DepictionText.OutcomeText` (勝ち) at 64, 「{names}を倒した。」, 「ターン n ・ 残りの HP a / b」, the chain's
  「i / n 戦目」. A loss: battle_ui_ux_v2 §2.5's result screen, 922×626 at x 26〜74 %, y 21〜79 %, over the ink, 「力尽きた」 fading as
  it rises — 「i 戦目で倒れた」 (or 「第 L 階層で倒れた」 outside a chain), 「残 HP 0 / 50」, the cards played by attribute as four bars in
  the attribute colours (the scene's count of the plays it showed; a move card counts as a skill), 「特性の発動 n 回」, 「n ターン戦った」
  (this fight's: the scene knows no other fight of the chain, so not §2.5's 平均), and 「手記へ」. Its items come in 120 apart, the bars
  over 400. After a win has risen, the player waits `L.RESULT_HOLD_MS` real time and calls `ctx.onFinished()` exactly once; a loss
  waits for 「手記へ」 (§5.19 順 7) — or a click, Enter, Space or Esc — unless the dev page's `ctx.auto` plays. A click or a fresh Enter / Space / Esc skips the hold
  only once the card has stood `L.RESULT_SKIP_AFTER_MS` (400) and after a pause of `L.RESULT_SKIP_PAUSE_MS` (350) since the last
  such press (review 16: clicks made to hurry the enemy's turn skipped 負け 0.14 s after it appeared; a mash never skips it).

### 4.7 Auto play (dev / tests)

`ctx.auto` (optional): `{ choose(source) → cardId | '', freeStep?(source) → −1 | 0 | 1 }`. In `idle` the player waits
400 ms (`AutoDrag`'s wait); while 俊敏's choice waits it takes `freeStep`'s way (staying without one) through
`pickFreeStep`, else either `hand.autoDrag(cardId, DepictionText.RequiredZone(face.Aim))` — the same hold → move →
release path a mouse takes — or, for `''`, `requestEndTurn()`. The dev page's chooser runs the ported `GreedyPlayer`
on `source.State` (a read for tools only; `ChooseFreeStep` for the step).

### 4.8 俊敏's free step (JS extension of the source; scene addition)

battle_core_v4.md §5 (#48): a turn that starts with 俊敏 opens one free cell, forward or back, until the first card is
played or the turn ends. The core has it (`TurnLoop.CanTakeFreeStep` / `TakeFreeStep`); the C# `CoreBattleSource` does
not, so Unity lets the step go. `src/bridge/depiction/CoreBattleSource.js` adds, as a documented JS extension:

- `CanTakeFreeStep(dir?)` (read-only): the battle waits for the player and the core holds the step open; with `dir`
  (+1 / −1) also that the line has a cell that way (`Field.Move` changes the cell). 呪縛 / 鈍足 / 鉤爪 do not close a
  way: the core takes the step and answers `MoveBlocked` (§3.8: the arrows show under 呪縛 too).
- `TakeFreeStep(dir)`: the core's step, its events written by the same writer (`Apply` per core event, `Close`), as one
  screen event of kind `CoreBattleSource.FreeStepKind` (`'FreeStep'`, outside the C# enum: `PlayCard` would wait for a
  card's release, `TurnStart` for its banner), title `俊敏`; returned as `EndTurn` returns its first event. Its plan is
  the cues' own: `FloorMove ×1` + `GapRecount`, or a `TraitFire` with the `MoveBlocked` line, or nothing (鈍足 to 0 cells).
  The recorder logs the call with its plain number. Tests: `test/depiction/FreeStep.test.mjs`.

The scene (battle_ui_ux_v2 §3.8 / §5.1 順 7 / §5.7 順 10–11), `player.js` + `field/freestep.js`:

- **When**: at the turn's first `idle` the player asks `CanTakeFreeStep()` once (the step only opens at the turn
  start; the answer is logged by the recorder, so it is asked once a turn), then `CanTakeFreeStep(±1)`.
- **Marks** (150 ms out): icons only (review 19: §1.5 keeps a resting screen's fixed labels to 10 characters, at most 2 each, and
  battle-visual-v1 §11 lists 俊敏の矢印 and 留まる as icons) — a 64×40 plate with 付録 A's 前へ / 後ろへ in amber over each open way's
  cell, its bottom 8 px over the floor so the gap number on the cells stays readable, and on the player's own tile a 64×28 plate
  with a stand-in line icon for 留まる (a figure on its cell; #92 draws the real ones). The mark's word (後ろへ / 留まる / 前へ) stands
  on a tip over the hovered or focused mark only. The focus is the amber edge (as every focus of the battle).
- **Input**: a click on a mark chooses it; ← picks back and → forward, again pressed it keeps that way (review 21; the key of a way
  not open picks 留まる, Esc takes the focus to 留まる), Enter chooses; the pointer over a mark focuses it. The guide line reads
  「俊敏で 1 マス動けます。矢印か「留まる」を選んでください（← / → と Enter）」; with a drawer open it keeps 16 px inside what the drawer
  leaves (review 19: its first character stood under the pile list).
- **Refusal**: no card is taken until a mark is chosen (§3.8 選ぶまで手札は掴めません): `hand.setGrabLock(fn)` — a press
  on a card or its digit plays `refusal` and puts 「先に、動く先の矢印か「留まる」を選んでください」 on the refusal line;
  hovering still lifts the cards (「引いた手札を見てから選べます」).
- **Choice**: 留まる asks nothing of the source (the first card or the turn's end lets the step go); a way is
  `ctx.source.TakeFreeStep(dir)` and its event plays as a move: the walk (160 ms a cell) and the recount, or the grey
  `MoveBlocked` line; an event with no cue (鈍足 took the cell to 0) floats a grey 「動けず」 (scene text, §5.7 順 9).
  X / the plate end the turn as usual and let the step go. No footstep sound: the battle's sound set has none.
- **The pile list** (review 17: opened while the choice waited, it hid the 「後ろへ」 mark and its guide replaced the step's): the
  choice stays the thing to make — a click on a mark takes it with the list open, the guide keeps 俊敏's line, a card pressed says
  the choice's refusal, and the arena's slide (`field.setShift`) brings the marks out from under the drawer with the hero
  (`freeStep.span`).
- Dev: `dev/out/battle.html?swift=<n>` puts 俊敏 n on the player before turn 1 (no card of the catalog puts it on the
  player yet); `window.__battle.chooseStep()` is the greedy player's way.

## 5. Common module conventions

- Factory: `createX(env, opts) → api`. The api object always has `destroy()`.
- Every animation method returns a Promise and runs on `env.clock` with the **nominal** ms it is given.
  `ms = 0` (an effect switched off) means: put the end state at once and resolve.
- `applyFrame(frame, { first })` must be able to rebuild everything the module shows from the frame alone
  (Unity: "The View can rebuild everything it shows from a frame alone"). `first` = a battle's opening frame:
  no fades (snap stance glows, snap panels).
- Never read `ctx.source`. Values only come through the arguments.
- Missing data must not throw: a frame field that is `null`/empty draws nothing.
- Each module ships `dev/battle-<module>.js` (built with `node tools/devbuild.mjs`) that mounts the module
  alone on the stage with a hand-made frame, and screenshots at 1920×1080 and 1280×720.

## 6. Module contracts

### 6a. `hand.js` — cards in hand (agent: hand)

Unity: `CardView`, `CardPanels`, `ReceiverView`, `ThrowLineView`, `TargetMarkView`, `RefusalLineView`, and the hover /
drag / fan / guide parts of `DepictionPlayer` (`LayoutHand`, `HomeOf`, `HoverPlace`, `UpdateHover`,
`UpdateHoverPanels`, `CardUnderPointer`, `BeginHold`, `ShowTargetMark`, `SetZoneHot`, `ZoneAt`, `ReceiverTakes`,
`UpdateHot`, `DragPosition`, `UpdateSnap`, `LetGo`, `Release` (visual half), `FlyDrawn`, `FlyDiscarded`,
`FlyCard`, `SettleHand`, `ReturnToHand`, `RefreshPlayableLook` (look), `ShowRefusal`/`Refusal`, `AutoDrag`).
Ported helpers: `HandSpec`, `HandFan`, `ReceiverDrop`, `RefusalLineLayout`, `CardTextFit`, `EffectPlan.DrawCardStartMs` /
`RebuildStartMs`, `DepictionText`. DS: `OCB.Card`, `CardDetail`, `HoverLine`, `ReachPanel`, `TargetFrame`,
`PredTag`, `RefusalLine`, `FEEL_DEFS` numbers.

```js
createHand(env, {
  onRelease(cardId, zone),   // a held card was let go: zone = DropZone (None | Receiver | AboveThrowLine)
  onHover(face | null),      // the hovered face changed (the player lights the floor reach)
  onHoldStart(face),         // a card was picked up (the player: stamina pay preview, reserve hint, HP wick stripe)
  onHoldEnd(face),           // the hold ended (release, cancel, letGo) — before onRelease
  preview(cardId) → string,  // ctx.source.PreviewFor (the one predicted value; '' for a refused card)
}) → {
  // frame
  sameHand(faces) → bool                  // same ids in the same order
  build(faces)                            // BuildHand: new card elements at their fan places (HandSpec.Rest)
  bind(faces)                             // same ids: rebind each face in place (cost, lamp, values, hover line)
  layout()                                // LayoutHand: every card to its place at once, hover dropped
  clear()
  count() → number
  faces() → CardFace[]
  // state set by the player
  setInteractable(on)                     // off: no hover, no grab (busy / enemy turn / overlay); a held card is let go
  refreshPlayable(dimOf, guide)           // dims cards with dimOf(id) (UnpayableDim, env.fx.ms), prints the guide line
  setStatusMarks({ focus, empower, intimidate })   // addition (review 21): the player's 集中 / 強化 / 威圧 — their marks beside the values (below)
  isHolding() → bool; heldFace() → CardFace | null; hoveredFace() → CardFace | null
  // beats (Promise)
  flyDrawn(first, rebuildAfter, { from, onRebuild })   // cards [first..] fly in from `from`, EffectPlan.DrawCardStartMs; onRebuild() at RebuildStartMs; sound 'draw' per card
  discardAll(to)                          // FlyDiscarded (HandDiscard ms + stagger, ease in) then clear; sound 'discard' per card
  release(cardId, ev, to, o) → { done: Promise, vanishedAt: () => {x,y}, face, keep }   // PlayAccepted's CardRelease: removes the card from the hand list at once, flies it to `to` (Single: receiver centre) or 140 px up (Self), scale 1 → 0.5, alpha 1 → 0, ease in; release sound by face.Kind.
                                          // Review 19: a stance card flies to the stance list's slot (geom.stanceSlot) over L.STANCE_FLY_MS (out), shrinking to its ghost's 76 px, and stays (`keep`) until its ghost leaves for the exile (§5.11 順 1 / 順 4); `o.traitWord` swells the lamp 1.4× and stands the condition word on the card (§5.9 順 1)
  ghostCard(face) → Element | null        // addition: a fresh DS card element of `face` for the played card's ghost (field.ghost's `card`)
  dropCard(cardId)                        // Released: destroy the element
  settle(ms)                              // SettleHand: the rest slide to their new places
  returnToHand(cardId, shake) → Promise   // ReturnToHand: CardReturn then RefusalShake; sound 'cancel' or 'refusal' ×2
  letGo()                                 // Halt / Esc while holding: back unplayed, decorations gone
  showRefusal(text)                       // RefusalLineView: the line at RefusalLineLayout for 3.5 s (env.real), the guide steps aside
  hideRefusal()
  // input paths
  select(index, o) → bool; confirm() → bool; cancel() → bool   // the keyboard / click path (battle_ui_ux_v2 §3.4); `o.auto`: the scene's own pick after an Enter play — a click on it keeps it as the player's selection (review 19: the click dropped it, and the next click on the enemy did nothing)
  autoDrag(cardId, zone) → Promise        // AutoDrag: hold, move 450 ms (inout), set hot, release through the same path
  destroy()
}
```

- Hold decorations (hand owns them, all in `layer.marks` / `layer.float`): for `Aim === 'Single'` the invisible
  receiver (`geom.receiver()`, `ReceiverDrop.Takes` for hot and for the drop) and the target hooks around the
  enemy (`TargetMarkView`: steel and no number when `face.ReachHint` is not empty); for `Self` the throw line at
  y 790 (x 400–1520, `ThrowLineView.IsAbove`) and hooks around `face.Affects`'s figure. The prediction tag shows
  `opts.preview(cardId)` with the icon of `face.Values[0].Kind` (or Guard / Power by `face.Kind`) at the top edge of
  the hooks, drawn in `layer.float` (battle-visual-v1 §5.2, DS TargetFrame); the badge over that frame rises just clear of it (DS
  `tag.lift`, hud.js; review 19 — review 17's tag pushed under the badge stood on the enemy's head and hid the badge's ▼, A1).  `face.LitFor(k)` relights the lamp while held, and the
  cost circle reads `face.CostFor(k)` / `CostDropFor(k)` (`k` the enemy it frames; review 16). For a blow on an enemy the
  player's `preview` gives the damage that reaches its HP, after its Guard — `face.HpLossFor(k)`, the HP wick's number —
  once `PreviewFor` says there is a value (battle_ui_ux_v2 §1.1 A6 / §3.2 「相手の Guard を引いた後の値」, §5.0; review 16:
  `PreviewFor`, like the C#, gives the raw power, so over Guard 5 the tag read 17 while the wick showed 12); any other card's
  tag is `PreviewFor`'s.
- Hover: `HandSpec.Hover` place (top y 760, upright), weight moves linearly over `CardHover` ms and is eased with
  smoothstep (Unity), the hover line (`face.HoverLine`, lamp `face.TraitLit`) above it, `face.ReachHint` panel on its
  right, right click toggles the detail (`face.Detail`), `face.Terms` underline + meaning on hover (§7.1).
- The detail (battle_ui_ux_v2 §3.13 / §3.5; review fix — Unity's `UpdateHover` closed it as the hover moved, so a long press
  showed nothing): a right click or a 400 ms press opens it over its card. Where (§3.13 「人型の胴は覆いますが、予兆・数値パネル・
  スタミナの札・手札は隠しません」, the omen being A1's badge and its aimed cells on the floor; review 17 — raised to clear the panels to
  y 330–610 it hid the gap number and the hero's cell): straight above the card as Unity's CardPanels has it, when that covers no
  number panel, badge or floor cell (only at the screen's edges); else over the floor (its bottom 4 px over the floor's top, y
  276–556) on the card's middle, where it covers the figures' bodies only; else at that height slid just clear of the omen badges
  in its way; else Unity's place raised off the panels. A long press is spent: the card goes back
  to its rest place over 200 ms and then the detail opens (§3.2 / §3.13); the card stays at rest until the pointer has left it.
  A right-click detail keeps its card raised. The detail stays up until Esc, a
  click (anywhere in the scene: on a card it only closes it) or another right click; while it is up the hand is locked — the
  cards lift on hover, none is taken or selected. A press that has opened it and then moves 8 px still becomes a drag (the
  detail closes): a player who pauses before dragging gets the drag, and a drag whose moves arrive late is not lost to the
  timer; a release 8 px or more from its press with no move reported in between is a drag too. The selected card's own detail
  (`I`) locks nothing. The detail holds its whole text and every definition (battle-visual-v1 §7 ③ 全文と用語の定義; review 19, a repeat):
  it grows from the DS's 280 to what they need (at most 600, past which the 「…」 cut falls back), and its place is worked out with
  that height. 俊敏's pending marks (`geom.freeStepBoxes`) are kept clear like the omen badges (review 19: it hid the 「前へ」 mark).
- Drag: 8 px threshold (`env.real`, unscaled), the grabbed point's offset kept, upright over `CardGrab` ms (out),
  alpha 0.92, snap pull 0.35 toward the receiver over `ReceiverSnap` ms (smoothstep). Size and shadow never change
  (decision 7).
- Card faces: map `CardFace` onto `OCB.Card` (`Kind` → `attack|defense|skill|stance`; cost circle `Cost` with
  `PrintedCost` and `CostDrop` ▼n; `Values` columns; `TextLines` (∞ rows when `Lasting`); `TraitTop` / `TraitBottom`
  box, dashed when `Plain`; reach tag from `RequiredRangeGlyph`, 自分 for `Aim === 'Self'`). Long text per
  `CardTextFit`.
- The guide line (`handGuide`): centre x 960, y ≈ 757, 22 px `--ink`, no panel; text from `refreshPlayable`'s
  `guide`. The refusal line replaces it for 3.5 s (`RefusalLineLayout`). It fades out (120 ms) while a hovered card's
  panels (hover line, reach panel) or the detail stand in its band, and while a card is selected (review 16: the selected
  card stands at the hover place across the band, and the drag wording is not the click path's), and back after (review fix:
  torn pieces of it showed between them; Unity's 11-hover-card-trait shows the same collision); it stays up under a held card.
  The refusal line (layer.float) steps aside the same way while a raised card (hovered, pressed, selected) stands over it, and
  its 3.5 s count only while it shows (review 16: it covered the name and the values of the next card hovered); the card whose
  own press brought it up (俊敏's choice, the pile list) keeps it.
- A window blur, a hidden page or a mouse move with no button down (the release was lost) sends a held card home unplayed and
  drops a press under way (review 16: after an alt-tab the card stayed glued to the pointer and played on the next click).
- Additions: `opts.restingUnit()` (the enemy a Tab at rest chose, −1 for none: a lifted single-target card is framed on it
  until the pointer or Tab names another, battle_ui_ux_v2 §1.4 「Tab か対象の枠で移る」), `debugState()` (dev / tests: what decides whether a press takes a card), `setGrabLock(fn | null)` (俊敏's
  choice: no card is taken, a press or a digit calls `fn`; hover still lifts), `setBlocked(on, hover)` (an overlay: no press or grab; `hover` lets the cards lift behind the journal, on
  real time while the scene clock holds). The hooks are drawn in `layer.fx` (under the omen badge and the panels, as
  Unity's `TargetMarkView` is the figure's); the prediction tag in `layer.float`. On the click path an enemy's omen
  badge and number panel count as that enemy (a click there plays the selection on it). The hover line always stands 8 px over
  the raised card, as Unity's CardPanels.ShowHover has it (review 17: moved down onto the card it covered the card's top edge and
  the reach panel's corner — battle-visual-v1 §5.1 「パネルとは重なりません」 — and set beside the card it left the card). At that
  height it keeps 4 px off every number panel by sliding sideways while the card's middle stays under it; when no slide clears
  (review 16: over the leftmost cards under the hero's panel), it stands, still over the card's middle, where it covers the least of
  the panels, their status chips counting four times (`StatusBarView`'s chips are left-aligned; `box().chipsW`). The right-click detail holds whole lines only (text 4, terms 3: CardPanels'
  `VerticalWrapMode.Truncate`).
- Keyboard / click path: `select(i)` lifts card i to the hover place and shows its hold decorations without
  following the pointer; a click on the target hooks (or above the line for `Self`) or `confirm()` releases to
  `DepictionText.RequiredZone(face.Aim)`; `cancel()` drops the selection. `confirm()` plays on the frame the screen shows as
  focused (`framedUnit()`): the one Tab or the pointer last framed, which stays framed — its light, the HP wick, the floor's
  N — when the pointer moves off (review fix: Enter then went to `CardFace.TargetUnit` while another enemy stood framed).
- Several enemies, the frames (battle_ui_ux_v2 §3.9; review fix): the focused frame stands full, its steel hooks (a card that
  cannot reach it) brighter and 3 px (`.is-focus`); the other frames step back, their hooks at 60 %, their prediction tags
  greyed and darkened but opaque (`.is-other`), so the focus reads even when it is an enemy out of reach.
- Status marks on the faces (battle_ui_ux_v2 §3.5 集中 / 威圧, §5.10 強化; review 21 — the cards printed the plain values while
  the tag, the wick and the hit counted the status): while the player holds one, each card it acts on carries its icon with
  「→」 (集中, every card with a value), 「×1.5」 (強化, a card with an attack value) or 「−3」 (威圧, attack or Guard) beside its
  values (`hand-card.js modsOf`; the factors are the core's `Constants`). One value: the column stands after it; two values: under
  the second, inside the face. The printed number stays the writer's (§8.2).
- The detail's 「今」 rows (§3.13; review 21): under the text, 離す先 (対象の枠 / 投げ上げ線 from `CardFace.Aim`), for a card with a
  trait 今の判定 (its lamp), and 予測値 with its make-up (the face's printed value, the status marks, 特性) — scene text. 習熟の段、
  探索の面の印、入手先 are not in `CardFace` (§8.2).
- The selected card's panels (review 21: a card chosen by a digit or a click showed neither its hover line nor its reach panel):
  the hover line and the reach panel follow the selected card while nothing is hovered or held.
- Several frames side by side (review 21: the trio's frames overlapped by 4 px and their tags stood on each other): a frame's
  sides are trimmed toward its neighbours' receivers so that 12 px stay between two frames, each keeping at least 2 px outside its
  own figure (`hand-marks.js` `FRAME_APART`, `OUT_MIN`).
- 重撃's cut of the recovery (§5.9 順 9; review 21): a held card whose heavy-blow trait is lit for the framed enemy adds the
  recovery-cut mark to its prediction tag.

### 6b. `field.js` — the arena (agent: field)

Unity: `FigureView`, `FloorView`, `DepictionFx`, `EnemyTurnShadeView`, `VisualArt` (shapes), and the figure /
floor parts of `DepictionPlayer` (`ApplyFloor`, `FollowFigures` positions, `Lunge`, `ReturnHome`, `SnapHome`,
`EnemyMotion`, `Recoil`, `Heave`, `ShakeArena`, `MoveOnFloor`, `SnapOnFloor`, `FlyGhostTo`, `SlideDiscardOntoDraw`,
the stance-glow parts). Ported helpers: `FloorSpec`, `FloorCells` (data only), `FigureFrame`, `FigureFacing`,
`EnemyMotionShape`, `DefeatShape`, `FigurePose`, `StanceGlow`, `EffectGrammar` (`LookId`, forms), `EnemyTurnShadeLayout`,
`BackdropSpec` / `BackdropMotion`. DS: `FloorTile`, `DistanceChip`, `Figure`, `FootLight`, `FloatNum`, `hatch`, `FIGURES`.

```js
createField(env, { layer, nodeKind }) → {
  // frame
  applyFrame(frame, { first })   // art ids / Down / heights (FigureFrame.HeightFor(size)); floor (tiles for Cells, AimCells red hatch,
                                 // DenseCells dense hatch, MoveCells violet dashed frame, the gap chip over the empty cells);
                                 // player figure on FeetOf(PlayerCell, PlayerSize) at once; the enemy's HOME set to its cell
                                 // (moved there now only when first); stance glows snapped when first (later changes are
                                 // the player's: it calls fadeStanceGlow / setStanceGlow by StanceGlow.Change); the enemy-turn shade (frame.EnemyTurn)
  setExtras(list)                // scene addition: [{ id, name, cell, size, hp, hpMax, down }] for Enemies[1..]
  showReach(cells) / hideReach() // amber reach light on the floor cells (battle-visual §4.2): each cell fades in over 150 ms (out)
                                 // and out over 120 ms (linear), battle_ui_ux_v2 §5.2 (review 16; Unity switches it at once)
  drawAim(frame, unit, now)      // addition (§5.8 提示): the floor takes frame's cells now; newly aimed cells drawn left to right, 30 ms a cell
  setEnemyTurnShade(on)          // EnemyTurnShadeLayout: gradient 0 → 0.72 black below y 790
  // figure state
  setPose(side, pose); setDown(side, down)
  enemyAway() → bool             // the enemy is not on its home
  setEnemyHomeCell(cell, size)   // PlayEvent: home = the enemy's cell after the event
  snapHome(side)                 // to its home at once, pose Idle
  stanceGlowShown(side) → bool; setStanceGlow(side, on); fadeStanceGlow(side, on, ms)
  // beats (Promise, nominal ms)
  lunge(side, px, ms)            // Lunge: Act pose, px toward the other side (ease out); the player's home is unchanged; an enemy's
                                 // stops 8 px short of an ally's body in its way (roomToward, as enemyMotion's reach)
  returnHome(side, ms)           // back to its home (inout), then Idle
  enemyMotion(system, ms)        // EnemyMotion: reach by EnemyMotionShape.Reach, stretch FigureFacing.During, Sweep / Strike lean, Shield rise 16
  recoil(side, strong, ms)       // Hit pose, lean (12 / 7°) away from the blow, shake 20 / 10 px ×3
  flash(side, role, ms)          // the body blinks white ('white') or the enemy colour ('omen')
  heave(side, ms)                // 息 on a heal: y-scale 1 → 1.05 → 1 (inout)
  fall(side, ms, unit, { keepFoot })   // DefeatShape: tilt, sink 40, darken to 0.35, fade from 60%; foot light out — with keepFoot
                                 // (an enemy falling while others stand) the foot light stays and the figure is not put down …
  fadeFoot(side, ms, unit)       // … addition (§5.15 順 5): the foot light goes over ms (linear), then the figure is down (the cell empties)
  bump(side, dir, ms, unit)      // addition (§3.14 / §5.7 順 5): a move that went nowhere — the figure leans 12 px that way and back
  shakeArena(ms)                 // the figures' layer shakes 6 px ×3
  moveOnFloor(cue, cells, ms)    // MoveOnFloor: player → FeetOf(cue.PlayerCellAfter), enemy → cue.EnemyCellAfter when it moved; out when Pushed else inout.
                                 // Several enemies: only Cue.Unit with its UnitCellAfter moves an enemy (review 16: after a kill
                                 // the C#'s EnemyCellAfter is the next enemy's cell, and the dead one slid onto it)
  snapOnFloor(cue)
  recountGap(gap, playerCell, playerSize, enemyCell, ms) / setGap(...)   // the gap chip: old number up and out, new in from below
  // fx (layer.fx / layer.float), Promise unless noted
  strike(side, { byPlayer, system, intensity, ms, unit, source })   // DepictionFx.Strike: Slash arc / Thrust line / Sweep band / Strike ripple across `side`'s chest
                                 // review 19: the shape reaches no further than 8 px short of another standing figure but its striker
                                 // (`blowRoom`), and 48 px past the struck body (fx.js BLOW_PAST) — a 突 on the front enemy ran through the
                                 // next one to the third one's head, and a 払 250 px past the hero
  wallHit(side, dir, ms, unit)   // addition (§5.7 順 7, review 19): the edge `side` was pushed into lights (the floor's end, or the figure in
                                 // the way flashes white) and the figure takes 打's ripple from that side
  dimFeet(ms)                    // addition (§5.19 順 1, review 19): every standing figure's foot light shrinks out (in)
  traitAccent(side, ms)
  mark(side, { fx, intensity, ms, crestLight })       // DepictionFx.Mark: 盾 ring, 型 crest at the feet (+ body light when crestLight), 気 sigil, 息 pips;
                                                      // the ring and the crest ride with the figure while they show (review 17)
  burst(at, role, size)          // fire-and-forget radial flash at stage point `at`
  floatText(at, text, role, fontSize, { struck, rise = 43, side })   // DepictionFx.FloatText: rises `rise` px over 600 ms (§5.0; Unity 1000), fades in its last 40 %; `side` a 16 px label after the number; fire-and-forget, returns its Promise
  shieldBlock(side, amountText, ms, unit, { broken })  // DepictionFx.ShieldBlock: the shield snaps up 40 px toward the blow from the chest — review 17:
                                                      // a 100×114 rim of light over a see-through face, no number (the "Guard −n" tag says it);
                                                      // `broken` (Guard went to 0): a crack, and its halves part as it fades
  ghost(from, to, { kind: 'discard' | 'exile' | 'sheet', ms, ease, card })   // FlyGhostTo (76×104 ink 55% fading; with `card`, that card element shrunk to 76 wide, 90 % fading late) / SlideDiscardOntoDraw (a 64×84 card back)
  retireAim(ms)                  // addition: the battle is over — the aimed / dense / move cells fade with the omen badges and stay clear
  retireAimOf(unit)              // addition (review 17): enemy `unit` falls — its aimed / dense / move cells go now, with its badge;
                                 // review 20: the last enemy's too (floor.retireUnit(unit, solo) — the floor's bind no longer
                                 // brings a lone fallen enemy's stripes back for the rest of the fall)
  setLowHp(on); pulseLowHp(ms)   // addition: the low-HP vignette (§4.3) rests at 0.25 while on; a landed blow pulses it 0.5 → 0.25
                                 // (review 21: over a bright sky ×2.6 and the deep form, see below)
  appearEnemies(ms, from = 0) → Promise   // addition (§5.18 順 7, review 21): every enemy figure from `from` to full over ms (out) —
                                 // the chain's next enemy coming out of the haze
  destroy()
}
```

- Figures: `ArtId` names the art; until art exists, pick a DS silhouette (`FIGURES`) by the enemy's family
  (a table in field.js, scene choice); the player is `swordsman`. Height: `FigureFrame.HeightFor(size)`
  (226, 264 for a 2-cell enemy); feet at y 570 (`FloorSpec` top 560 + `FeetBelowTop` 10); x = `FloorSpec.FeetX`.
  Enemy silhouettes face left (`FigureFacing.HomeScaleX`). Foot light: width 2 × body, height 60, amber for the
  player, violet for enemies. Elite: phosphor edge light; boss: double frame is the hud's (panel), the field may add
  the `--boss` rim.
- The floor: `FloorSpec` (x 300–1620, y 560, 40 tall, 3 px inset), tiles filled with `LayerBackdrop.Tile`; the
  hatching angle per enemy (45° for the front enemy); 6 px stripes on `DenseCells`; `MoveCells` violet dashed; the
  gap chip (`OCB.DistanceChip`, y 563, `FloorSpec.GapSpan`, `TagAtZero` 100 wide at gap 0). Stripes that go with nothing in
  their place (the omen acted) fade over 120 ms (battle_ui_ux_v2 §5.8 「実行の後、斜線は 120 で消える」; review 16). One enemy's move
  destination on a cell another enemy aims at (review 17: the DS tile draws the red threat frame over the violet dashed one at the
  same rect, and the destination vanished): the dashed frame stands in front, 4 px inside the red one, on a dark keyline
  (`.ocbg-battle-move-over`).
- The light shafts (layers 1, 2, 4) are painted once into a canvas, the 560 px fade baked in, and only its opacity moves
  (review 16: a full-screen mask over rotated gradients, its opacity rewritten every frame, ran those layers at 15–20 fps in
  headless Chromium); a haze's opacity is written only when it moved 0.004.
- Paint order (battle_ui_ux_v2 §2.2.1 / §5.7; review fix: the figures painted in creation order, so the farthest enemy
  covered the nearer ones): the hero in front (z 9); the enemies by their distance to the hero, the nearest in front (8, 7, 6);
  an enemy walking on the floor behind every other enemy until it arrives (2); the gap number over them all (z 20). Review 16:
  the enemy acting (`enemyMotion` / its lunge, until it is home again) stands in front of the other enemies (8), and its
  reach stops 8 px short of an ally's body in its way (`roomToward`; a back enemy's 90 px thrust and 46 px lunge landed its
  body on the enemy in front, and the two read as one silhouette while it acted).
- Depth with two or three enemies (scene choice, review fix: three bodies on adjacent cells fused into one dark clump): the
  nearest enemy's silhouette wears a sharper violet edge (`drop-shadow` 1.5 px, violet 85 %), each enemy further back is drawn
  a step lighter (`brightness` 1.5, 2.0), opaque — a see-through body turned into a ghost over the bright layers.
- 気 (`mark` with a Sigil form; a look call, Unity's flat White sprite read as a debug marker): the diamond is lit
  (phosphor → violet with a bright edge and a phosphor glow), carries the crest's soft Glow behind it and leaves three fading
  copies along its fall.
- Backdrop for `ctx.layer`: `BackdropSpec.For(layer)` — sky stops, hazes (`HazeShape`), motes moved with
  `BackdropMotion` on `env.real` (they do not speed up), the horizon at y 556 and the dark earth below (DS `dusk`
  values for the earth), so the floor, hand and numbers stay on dark ground (DS README 「背景のテーマ」).
- The enemy-turn shade lives in `layer.shade` (over the hud, under the hand).
- The drawer slide (`setShift(dir)`): battle_ui_ux_v2 §2.3 / §2.4's 15 % (288 px), read every frame. Review 19 (the trio on 8
  cells: the journal cut off the third enemy's badge, body and panel, and the slide was 62–203 px): the stance lists no longer hold it
  back, and the floor may come within 12 px of the screen's edge. Review 21 (the third enemy still stood half under the journal's
  edge): the slide is at least 15 % and as much more as brings every standing figure's art (`geom.figure().artX0 / artX1`)
  `DRAWER_KEEP` (12) inside what the drawer (`DRAWER_SHARE`, 30 %) leaves. One limit is kept (scene choice): no standing figure's
  art leaves the screen on the other side. A stance list a figure would come under moves to its high place (hud, §6c). The panels
  and the badges keep 24 px inside what the drawer leaves (hud.js, `geom.journalEdge` / `drawerRight`), the badges then sliding up to
  150 px apart. Figures stand on their cells' middle (battle_ui_ux_v2 §2.1; review 19 — the earlier `standX` nudge moved the hound
  24 px into the enemy beside it): a silhouette wider than its cell may reach behind a stance list's frame. `field.nudgeOf` stays (0)
  for `dev/battle-read.js`.
- 俊敏's 「留まる」 plate: 104×28 on the player's tile from 2 px under the feet to the tile's bottom (review fix: the 40 px plate's
  top edge cut across the feet and the foot light; raised to the arrows' line it would cover the legs, under the tile the hero's
  panel).
- 俊敏's choice (§4.8): `showFreeStep(playerCell, ways)`, `focusFreeStep(dir)`, `hideFreeStep(ms)`, `freeStepShown`,
  `freeStepFocus`, `freeStepDirs()`, `freeStepCentre(dir)`; `opts.onFreeStepPick(dir)` / `onFreeStepFocus(dir)`.
- Once a frame's `Outcome` is decided the gap number is put away (it measured to a fallen enemy). At gap 0 the plate
  itself is `FloorSpec.TagAtZero` (100) wide. Several enemies: when another standing enemy stands between the player
  and the enemy the number is read to, the tag stands over the run of empty cells in the span nearest the chosen enemy (no
  brackets; review 17: with the front enemy on the cell just before the chosen one it fell to the border between the two
  enemies' feet and read as the distance between them), or, with no empty cell in the span, on its near edge; a Tab choice holds for its turn only (`restingGapUnit` resets at the turn's end). An
  enemy whose fall is still to play (HP 0, its `FloorEnemy.Gap` −1: the core has freed its cells) counts as empty there, as N
  counts it (review 16: the tag broke onto the next enemy's feet for the whole fall). Review 19 (a Tab at rest moved the N to the
  back enemy over the same empty cells): over a span another enemy breaks, the brackets run the whole span the number counts — from
  the hero's cells to the chosen enemy's — and the plate sits over the empty cells in it; the chosen enemy's number panel wears the
  amber focus edge while nothing is held (`hud.setRestFocus`).
- The blows' looks (review 19, a look call over Unity's prototype bars): 斬 an arc of blade light — a glow under a bright core with
  thin ends, revealed along the arc from high on the striker's side to low past the chest; 突 one line of light from the striker's
  side, its tail fading, and a point of impact flaring where it meets the body; 払 a wide band with soft edges, a bright core line,
  a lit leading edge and a few motes thrown behind it. The trait accent (§5.9 J2) is a burst of light with eight short rays at the
  point of contact (Unity's two concentric rings read as a gun sight with the 突 line through them).
- The stance's body light (§5.11 順 2 「身体に薄い光」, `StanceGlow`; review 21 — review 19's silhouette filled flat crimson at
  α 0.25 read as an enraged tint and drowned the hero's amber edge): a radial light inside the silhouette (`BODY_LIGHT`: centred
  60 % up the figure, full at its middle, gone at 46 % of its height), screened over the art (`mix-blend-mode: screen`), so the
  middle of the body glows and its edge keeps the figure's own rim; the soft glow behind stays as its halo. Strength `StanceGlow.Alpha`.
- The low-HP vignette over a bright sky (§5.6 順 5; review 21 — on layer 1 the crimson at α 0.25 over the pale meadow read as a
  beige stain): where `BackdropSpec`'s top sky stop is brighter than relative luminance 0.4 (layers 1, 2, 4) the vignette takes
  its deep form (`.is-bright`: a narrower band, crimson darkened toward the flood at the edge) at 2.6× (peak 1). A look call.
- 開示度 0 (battle_ui_ux_v2 §7; review 21): an enemy whose omen's `Disclosure` is under 1 has no aimed cells drawn on the floor
  (`unreadOf(frame)` → `floor.bind(…, { unread })`); the writer still writes them (§8.2).
- The bodies of 蛇体 enemies (review 21, a look call — the 瘴気の司祭 and the 深淵の釣り手 read as the same stand-in, and the
  歪んだ根 as a bush): `field/art.js` draws a coiled serpent (`SERPENT_DEF`, 220×234) for the priest and the angler, and a
  root mass with its own outline for the 歪んだ根.
- 打 (`strike` with `StrikeSystem.Strike`): a thin ring snapping open where the blow meets the body (the chest's edge
  toward the striker) and five short spikes thrown the way it travels (a look call: Unity's solid disc over the chest
  read as a bubble swallowing the figure).
- The rings (review 17, a look call; Unity stretches one 32 px ring sprite, and the port's SVG stretched with it — the 型 crest
  26 px thick at its ends and 6 at its top, the 盾 ring a lozenge, the trait accent a target): every ring is an ellipse drawn at
  its real size with an even line and a soft light (`drop-shadow`) — 盾 4 px, the crest 3 / 2 px over a faint light on the ground
  inside it, the trait accent 2.5 / 2 px, 打 4 % of its size; the timing is Unity's.
- The blocked blow's shield (review 17, a look call: Unity's prototype ShieldBlock is a flat 150×170 slab at 92 % with the
  number on it, and it hid the struck figure — whole, on a blow Guard stopped): 100×114, a Guard-coloured rim with a soft glow
  over a see-through face, so the figure shows through; no number (the "Guard −n" tag beside it says it); a broken Guard draws a
  crack and parts the halves as it fades. Its pop / wait / fade are Unity's.

### 6c. `hud.js` — panels and chrome (agent: hud)

Unity: `StatusBarView`, `OmenBadgeView`, `OmenIconArt`, `StanceListView`, `StaminaTagView`, `PilesView`,
`PileListView`, `BottomRightViews` (`PlayedAttributesView`, `ReserveHintView`), `CornerInfoView`,
`BattleSpeedButton`, the end-turn plate (`StyleEndTurn`, `ShowEnemyTurn`), `Banner`, `ShowResult` /
`BuildResultCard`, and the panel half of `FollowFigures` / `FollowPanels` / `KeepX` / `KeepBottom`. Ported helpers:
`StatusPanelLayout`, `HpWickSpec`, `StatusChipText` (already applied by the writer: chips carry their texts),
`OmenDisclosure` (already applied: the frame is blanked), `StanceList`, `PileLayout`, `PileList`,
`PlayedLayout`, `ReserveHintLayout`, `CornerLayout`, `BattleSpeed`, `EnemyTurnShadeLayout.EndTurnAlpha`.
DS: `StatusPanel`, `StatusChip`/`StatusTip`, `OmenPlate`, `ReserveOmen`, `StanceSlot`/`StanceTip`, `StaminaPlate`,
`Pile`, `PileDrawer`, `PlayedAttrs`, `GuardPreview`, `CornerInfo`, `SpeedToggle`, `JournalButton`,
`EndTurnButton`, `Banner`, `icon`.

```js
createHud(env, {
  chain: { index, total },
  onEndTurn(),            // the plate was clicked (the player decides whether it counts)
  onSpeed(),              // the speed button was clicked
  onJournal(),            // the 手記 button
  onSettings(),           // the gear's click — only with settingsInBattle (Esc is the scene's)
  settingsInBattle,       // review 19/20: the gear only when the app opts in (battle-visual-v1 §4.1: no settings in a battle)
}) → {
  applyFrame(frame, { first })   // corner (Turn; Floor = env.layerNo, the exploration's; chain dots ●○ as CornerInfoView; miasma %),
                                 // both panels (UnitFrame: HP + bar + ticks, Guard (dashed at 0), enemy stamina dot + number +
                                 // the 構えの盾 solid/dashed by ReserveLikely, Elite phosphor frame, chips with tooltips),
                                 // the omen badge (OmenFrame incl. plan / 予定 / 予定変更, Disclosure, Hit mark, Statuses, boss words),
                                 // stance lists (StanceListsShown, StanceList rows / folding / dashed empty frame),
                                 // stamina tag (Stamina, StaminaMax, StanceDivider, NextRecovery dashed dots),
                                 // piles (PileFrame counts → thickness, PileLayout) and the open list's rows (PileList),
                                 // played attributes (PlayedFrame, Last underline, FollowUp badge), the end-turn plate alpha
                                 // (EnemyTurn → 0.4) — the reserve hint is set by the player (reserveHint below)
  follow({ playerX, enemyX, enemyTop, cells, enemySize, units, snap })   // per frame: panels at y 614 under their figures
                                 // (StatusPanelLayout.Place, eased 1 − e^(−14·dt·speed)), widths (EnemyPanelWidth), the omen badge's
                                 // middle on enemyX (24 px inside the edges), its bottom 14 px over enemyTop (+ its lift, below)
  status(side) → {               // 'Player' | 'Enemy'; the player's stamina routes to the stamina tag
    setGuard(n); popGuard(n, ms = 260); crackGuard(ms)
    setHp(hp); animateHp(hp, ms); animateTrail(ms); snapTrail(); setHpKeepingTrail(hp)
    showHpLoss(n)                // the wick stripes (HpWickSpec.Span) — enemy panel only
    setStatus(label, stacks); playStatus(label, stacks, beatId, ms)   // StatusApply / StatusStack / StatusTick / StatusVanish
    setStamina(st, max); lightPips(st, max, perPipMs); popPips(ms)
    greyOut(ms)                  // addition (§5.15 順 5, review 17): a fallen enemy's panel turns grey over ms, then setDown
  }
  omen: { bind(omenFrame); popIn(ms); blink(ms); flare(ms); setActing(on); fadeOut(ms); hideNow();
          fadeOutAll(ms); hideAllNow(); strikeSide(ms); strikeSideNow() }
  stamina: { showCost(n); glowReserve(ms) }      // "▸ n" and the pay rings while a card is held; pips 1–3 glow (構え's 息)
  reserveHint(text)               // ReserveHintView at x 1560 y 984; '' hides it
  setEnemyTurn(on)                // ShowEnemyTurn's plate half: the end-turn plate at EnemyTurnShadeLayout.EndTurnAlpha (0.4)
  endTurn: { pop(ms); flash(ms); refuse(ms) }   // StanceCue's 200 ms pop; idle flash; refuse (review 21): the plate dims and shakes
                                 // (6 px, twice; L.END_TURN_REFUSE_MS 220) when the mash cap (§4.2) turns a press away
  banner(text, side, ms) → Promise   // a band at y 400 (DS .ocb-banner), fade in 25% / hold / out 25% (Unity's curve); side 'player' | 'enemy'
  resultCard(outcome, ms, info, { onButton }) → Promise; resultCardNow(outcome, info, { onButton })
                                 // review 21: the DS end paper (.ocb-end / .ocb-end-paper) in layer.over — a win's 720-wide paper
                                 // (勝ち + info.lines) scaling 0.94 → 1 at the middle; a loss's result screen (§2.5, 922×626 at
                                 // x 26〜74 %, y 21〜79 %; info.title, lines, bars, stats, button 「手記へ」 → onButton), rising
                                 // from below over the ink; items in from the top 120 apart, bars growing over 400
  chainBanner(title, name, scale, onOut) → Promise   // addition (§2.5 切替バナー / §5.18 順 6〜7, review 21): the dark board, 「n 戦目」
                                 // and the enemy's name (300 in, hold, 400 out); onOut() as the board comes up
  lossWordOut(ms) → Promise       // addition (review 21): 「力尽きた」 fades as the result screen rises over it
  closePinned() → bool            // addition (review 21): a right-click-pinned chip / stance detail closes (Esc)
  speed(label, chevrons)          // BattleSpeedButton.Show; tooltip BattleSpeed.Tooltip
  setSpeedVisible(on)
  drawerOpen() → bool; closeDrawer()
  liftOmen(units)                 // the hand's taggedUnits(): those badges rise just clear of the prediction tag (below)
  setRestFocus(unit)              // addition (review 20): the enemy panel a Tab rests on gets an amber edge (status.setFocus); −1 clears
  setLog(lines)                   // addition (review 19): the pile list's 4th tab, ログ全文 — the player's event log, newest last
  flyOmenToStances(unit, ms) → Promise   // addition (§5.11 順 3, review 19): an enemy's 構え — a copy of its badge (the
                                 // `.ocbg-battle-omen` plate alone, not its plan) flies to the end of the enemy stance list
                                 // (geom.stanceSlot('Enemy')) over ms (L.STANCE_FLY_MS 300), the badge hidden behind it
  lossInk(word, inkMs, wordMs) → Promise   // addition (§5.19 順 2〜3, review 19): the ink floods up and the word draws apart
  destroy()
}
```

- Pile list drawer: a click on a pile toggles the list for that pile (`PileListView.Toggle`), tabs 山札 / 捨札 /
  除外 (`PileList.TabLabel`), rows `PileCard.RowText`, empty `PileList.Empty`, close `PileList.CloseLabel` with
  `JournalBook.ClosingHint` (「閉じるとプレイできます」, §4.5) beside it, width share 30 % (or the DS's recommended C variant);
  the rows refresh with every frame (`Show(piles)`). While it is open the panels keep 24 px right of its edge
  (`piles.drawerRight()`; review fix: it covered the hero's heart and cut its panel). Review 20: the badges keep that edge too,
  and the journal's left edge (`geom.journalEdge()`, the scene's) bounds panels and badges on the right — the row spreads
  between the two edges, with each badge's slide doubled (150) while a drawer or the journal is open, so beside the journal the
  badges no longer stack on each other or go under the book. Each row carries the card kind's band on its left (`bandOf(name)`:
  `CardCatalog` by name → `CoreText.KindOf` → the hand's kind colours; review 19 — the rows were plain text). A fourth tab,
  ログ全文 (`setLog`), lists the event log (§7), `ログ全文は、まだ中身がありません。` when empty.
- Several enemies, the row (battle-visual-v1 §4.5 「誰の数かを位置だけで示します」; review fix): a fallen enemy's grey panel takes
  no part in the row's spread; it stands under its emptied cell and gives the place up (`status.setYielded`, hidden,
  `data-yielded`) only while a standing enemy's or the hero's panel actually stands over its cell — an enemy that stepped into
  the fallen one's cell no longer has its panel pushed onto the next figure. Review 16 (with the 10 px row gap counted, a
  neighbour that merely touched it hid it, so a defeated enemy on adjacent cells left no trace): it slides up to 24 px to keep
  4 px off its neighbours, and yields only when that is not enough. Review 17 (one enemy fell while another stood: the hero's
  300 px panel slid back under the hero into the fallen hound's place, and the grey panel was gone for the rest of the fight with
  nobody on its cell): the row is first laid out with the grey panels in it, as it stood before the fall, and that layout is kept
  while every standing panel still has its figure 24 px (a quarter of a narrow panel) inside its edges; only otherwise does the
  rule above apply. Review 20 (two enemies whose heads stood at different heights: the badges sat on two rows): with two or more
  enemies the badges share one row, set by the tallest head (`rowTop`), and each ▼ reaches down to its own head.
- Several enemies, the badges (battle_ui_ux_v2 §2.2.2 / §5.8): badges that would overlap slide apart, at most 75 px each,
  eased over 200 ms (out-like), and a ▼ under each (`omen.setTail(dx)`, in the frame's colour) stays on its enemy's head; the
  hatch swatch stands on a badge whose omen hatches cells on the floor (`omen.setSwatchShown(on, aimed)`, `FloorEnemy.AimCells`).
  Review 16: the DS OmenPlate 「移動の行動に赤の色見本を付けないでください」 (`angle` null for an action that aims at no cell) and
  battle-visual-v1 §2.3 keep the red to the cells an action reaches, so a move or a rest carries none — this overrides the port
  notes' 「a 22 px one at the right end of its omen plate's first row」 for every plate (a conflict between the port note and the
  DS; the DS's rule is followed). One enemy: the swatch with the hit mark (OmenBadgeView). Review 17: every badge carries its ▼,
  one enemy's too (battle_ui_ux_v2 §5.8 「札は敵の頭上に置き、▼ で敵に結びます」, §5.16 「1 手目（▼）が下」; Unity draws none). A
  walking enemy's badge passes behind its neighbours' until it arrives (`omen.setBehind`; review 17: it rode over the swatch and
  the hit mark of the enemy it passed), as its figure does (§5.7).
- The badge and the prediction tag (review 16, a repeat finding; review 20 replaces review 17's placement): the tag (60 tall)
  stands on the frame's top edge (hand-marks.js `tagY` = the box's top; battle_ui_ux_v2 §3.2 「枠の上辺」), and a badge it would
  touch rises just clear of it: `hud.liftOmen(units)` (the hand's `taggedUnits()`) marks those badges, and `follow` lifts each
  by `clamp(badgeBottom − (tagTop − L.TAG_LIFT_GAP 4), 0, L.TAG_LIFT 80)`, eased, then lets it back down when the tag goes. At
  rest every badge is 14 px over its head, as Unity (`FollowFigures` / `KeepBottom`, `OmenOverHead` 14) and battle-visual-v1 §4.4
  have it; the DS's `tag.lift` knob (motion.md) is `L.TAG_LIFT`, now the most a badge may rise (review 17 had pushed the tag down
  under the badge instead, between the top hooks — off the frame edge the spec names, and over the figure's head).
- A trait's word on a badge (`omen.traitWord(word, ms)`, §5.9 順 1 / 順 12 for the enemy; review 19): a small tag with the
  condition's name (「間合い」, not the lamp's 「間合い0」; review 21), faded in over `L.TRAIT_WORD_MS` (150), held 900 ms, out over
  200. Review 21 (it hung half outside the badge's right edge, and with three enemies the next badge painted over it): it stands
  inside the badge's right end, over the swatch and the hit mark for its moment, and the badge stands over its neighbours while it
  shows (`.is-trait`).
- Omen comes in (`omen.popIn(ms)`, review 16): the badge — and its plan, the two rows together — drops 24 px into place and
  fades in over the OmenShow ms (battle_ui_ux_v2 §5.8 「頭上へ降りる」 260 out, §5.16 順 1); Unity scales it 0.6 → 1.
- The end-turn plate's one brightening (`endTurn.flash(ms)`, §4.3 「idle, nothing playable」): brightness up to 1.6 and an amber
  light, once, off at the enemy's turn and at the result.
- A stance frame's hover shows its lasting chip's panel (`UnitFrame.Statuses`, the enemies' too: 「構え・狩りの構え」 with
  永続 / 効き方 / 減り方); with no chip it shows the name and ×n (review 16 — the finding's repro hovered the enemy list's
  zero-size container, not a frame; the chip was there).
- The journal button goes off with the end-turn plate at the result (`setOff`, 40 %, no click of its own: a click there moves
  the result on; review fix: it stayed lit and a click on it skipped the result).
- Omen badge: 200×98 `panel-solid`, violet 2 px frame; icon by `OmenIcon` (`OmenIconArt`), value at 48 in phosphor
  (attack) / guard; the swatch of the floor hatching; the hit mark (`OmenHit`: lands phosphor solid / misses steel
  dashed struck); `Statuses` at 28 px for a Seal omen; boss words (`StatusGlyph.None`) as the name in the boss
  colour's double frame; plan badge beside it (`ReserveOmen`, tag `PlanTag`), dashed. `Disclosure` < 2 draws no numbers
  (the writer has already blanked them) — draw what is there.
- Additions: the settings gear only when the app opts in (`opts.settingsInBattle`; review 19 — battle-visual-v1 §4.1 「戦闘中は
  設定を開けません」 overrides the brief's 「Esc / gear」, so the scene draws none by default) — `createSettingsButton`, 56×56 in
  the journal button's look, 12 px left of the speed switch, tooltip `ExplorationText.SettingsButton` 設定（Esc）; hidden with
  the speed switch once the battle is over;
  `omen.setMoveDir(dir)` (a move omen's arrow points the way its destination cells lie, `FloorFrame.MoveCells` against
  the enemy's cell; ← when unknown) and `omen.promotePlan(ms)` (§4.3 `EnemyWindup`); the stamina dots are full 18 / 16 px
  circles (StaminaTagView / StatusBarView; the 付録 A pip icon drew them at half size); a pile's name is a caption on the
  hovered pile under its icon; 負け is written in `--phosphor` (the enemy side's text colour: Unity's `BattleTheme.Omen`
  is violet, which the DS keeps off text) inside the violet frame.
- HP ticks (review 19): a tick every 10 HP on every panel (battle-visual-v1 §4.5, battle_ui_ux_v2 §1.1 B1 「目盛は 10 刻み」,
  StatusBarView.RenderTicks); where they would stand closer than 8 px they are drawn 1 px wide (`is-thin`) rather than thinned to
  every 20 HP (the 37 px bars of 8 cells did that, and nothing said so).
- Chain dots (review 20): CornerInfoView's dots fill the battles already won (`i < index`), the current one hollow —
  ○○○ at the first of three (the dot of the battle being fought was drawn full).
- The result (review 21 — the 520×220 box over the heads held one word and read as a debug overlay; a loss showed 負け and
  力尽きた together): the DS end screen. A win: the scrim, the paper at the middle with 勝ち, whom it beat (`Enemies.ById(id).Name`
  by the figures' art ids), 「ターン n ・ 残りの HP h / m」, and in a chain 「i / n 戦目」; it goes on after `L.RESULT_HOLD_MS`. A
  loss: after the ink and 「力尽きた」, the result screen rises (400) as the word fades — 「第 n 階層で倒れた」 (or 「i 戦目で倒れた」
  in a chain), 残 HP, 出した札 by attribute as four bars (攻撃 / 防御 / スキル / スタンス in the attribute colours; a move card counts
  as スキル), 特性の発動 n 回, n ターン戦った, and 「手記へ」, which (or a click, Enter, Space, Esc after the skip pause) calls
  `onFinished`; the loss waits for it (the dev auto play excepted). The counts are the scene's own (each accepted play's `Kind`,
  each fired trait).
- The stance lists' high place (review 21; a look call): a list that a standing figure's art comes under — a slid arena beside a
  drawer, a figure on the line's last cell — moves to the top bar beside its side's corner chrome (the player's 12 px right of the
  corner information, the enemy's 12 px left of the speed switch or the gear), shows one frame there (the newest) with 「+n」 for
  the rest (its hover lists them all), and comes back to y 470 once the place is clear (`stances.keepClear(arts)`, called by
  `follow`). The switch fades over 150 ms on real time (it runs while the journal holds the scene clock).
- Pinned details (battle_ui_ux_v2 §3.13; review 21): a right click on a status chip or a stance frame pins its detail; it stays
  until a click anywhere, a right click elsewhere (which pins that one instead) or Esc (`closePinned`). Hover still shows it as
  before (battle-visual-v1 §7.3 — the two documents differ: hover is kept and the pin added, see §8.2).
- The chips' order (review 21; StatusBarView appends and the writer's chips come in StatusKind order): newest on the left —
  each word keeps the stamp of the last change of its stacks — so the oldest fold into 「+n」; the opening frame keeps the frame's
  order.
- The hatch swatch at 開示度 0 (review 21): hidden — the floor draws no cells for it.
- The chain switch (§5.18 順 6〜7): see `chainBanner` above; the player plays it before the first turn when `ctx.chain.index > 1`.
- Status chips: `ChipKind` decides the corner (`OnFoe` square 3 px, `OnSelf` round 15 px, `Lasting` ∞, `Boss` double
  frame); hover shows `Title`, `Group`, `Effect`, `Decay`. At most 4 on the enemy panel, 6 on the player's, then `+n`.

## 7. Scene texts (strings the scene writes itself)

| text | where | source |
| --- | --- | --- |
| `Guard −n` | GuardNumber float | `DepictionPlayer` (scene text) |
| `一覧を閉じるとプレイできます（Esc）` / `手記を閉じるとプレイできます（Esc）` | guide / refusal line while the pile list / the journal is open | scene text (battle_ui_ux_v2 §3.5; review 16 / 17) |
| `「name」は敵の上で離すと出せます` | refusal line, a single-target card let go off its target | scene text (battle_ui_ux_v2 §3.14; replaces `DepictionText.WrongZoneText`'s 「敵の受け皿の上」, a receiver the screen no longer draws; review 17) |
| `俊敏で 1 マス動けます…`, `先に、動く先の矢印か…`, `動けず`, `留まる` / `前へ` / `後ろへ` | 俊敏's choice | scene text (battle_ui_ux_v2 §3.8, §5.7 順 9) |
| `ターン終了` | end-turn plate | the Unity prefab / DS `EndTurnButton` |
| speed labels, tooltip | speed button | `BattleSpeed.Label`, `BattleSpeed.Tooltip` (ported) |
| `閉じる（Esc）`, tabs, `なし` | pile drawer | `PileList` (ported) |
| `ログ全文`, `ログ全文は、まだ中身がありません。` | the pile list's log tab and its empty line | scene text (review 19) |
| log lines (`— ターン n —`, `— 敵の番 —`, `自分: title`, `敵: title` — with two or three enemies the enemy's name (review 21) —, `　特性: …` (the condition's name), `　[who ]状態: label n` / `消えた`, `　スタンス: …`, `　山札を作り直した`, `　side HP −n` / `Guard −n` / `壁 −n`, `　sideが倒れた`) | the log tab | scene text around the events' own titles and lines (player.js `LOG`, at most 400 lines) |
| `力尽きた` | the loss ink (§5.19 順 3) | scene text (battle_ui_ux_v2 §5.19) |
| `壁` | the 16 px label after a wall blow's number (`3 壁`) | scene text (battle_ui_ux_v2 §5.7 「壁」; review 19) |
| a trait's effect + condition (`+3 残4`) | the 16 px label after the number its trait changed | `CoreText.TraitLine`'s two halves (the bridge's), the word Guard dropped |
| `前へ` / `留まる` / `後ろへ` | 俊敏's marks — now icons, the words in a tip on hover / focus | scene text (review 19: the words sat on the floor) |
| `間合い` `温存` `連動` `予兆` `〜中`; `+6 重撃` `転換` `追撃` | a fired trait's condition word and named effect (side label, badge, card) | scene text over `CoreText.ConditionWord`'s head (review 21, §5.9 順 1); 重撃's 6 is `Constants.HeavyBlowPower` |
| `×1.5 脆化` / `×1.5 強化` | after a blow's number | scene text, `EnumTokens.ToLabel` + `Constants.FragileMult` / `EmpowerMult` (review 21, §5.0) |
| `−n` | the HP loss float's sign | scene text (review 21, §5.0) |
| `引き寄せ` | the muted word over a figure pulled nearer | scene text (review 21, §5.7 順 6); a push keeps `DepictionText.Pushed` |
| `→` / `×1.5` / `−3` with the status icon | beside the card values under 集中 / 強化 / 威圧 | scene text + `Constants` (review 21, §3.5 / §5.10) |
| `離す先`, `対象の枠（敵の上で離す）`, `投げ上げ線（線より上で離す）`, `今の判定`, `条件が成り立っています（ランプ点灯）` / `条件は成り立っていません`, `予測値`, `（表の値 n・…）` | the detail's 「今」 rows | scene text (review 21, §3.13) |
| `+n` | the stance list's high place, the frames not shown | scene text (review 21) |
| `name を倒した。`, `ターン n ・ 残りの HP h / m`, `i / n 戦目` | the win paper | scene text around the frame's numbers and `Enemies.ById(id).Name` (review 21) |
| `第 n 階層で倒れた` / `i 戦目で倒れた`, `残 HP h / m`, `出した札`, `攻撃` `防御` `スキル` `スタンス`, `特性の発動 n 回`, `n ターン戦った`, `手記へ` | the loss's result screen | scene text (review 21, battle_ui_ux_v2 §2.5 / §5.19 順 5〜7) |
| `n 戦目` + the enemy's name | the chain's switch banner | scene text (review 21, §2.5 / §5.18 順 6) |

Everything else (banners, result, guide, refusals, chip / omen / card texts) comes from the ported bridge. The corner's chain
is `CornerInfoView`'s dots (review 17; the brief's 「i / n」 is gone).

## 8. Testing

- `node tools/devbuild.mjs dev/battle.js` → `dev/out/battle.html`; the picker builds a fight with `BattleLaunch`
  (every enemy of `Enemies.All`, the two-enemy pair of `LayerEncounters`, layer 1–7) and mounts the scene with a
  stub `ctx`. `?enemy=<id>&layer=<n>&seed=<n>&auto=1&speed=1.5&short=1` start it directly.
- `node tools/shot.mjs dev/out/battle.html dev/shots/<prefix> --steps <steps.mjs>`; `S(x, y)` maps stage px to the
  page. The page exposes `window.__battle = { scene, player, source, state() }` for steps.
- A module's own page (`dev/battle-hand.js`, `dev/battle-field.js`, `dev/battle-hud.js`) mounts that module alone with
  `dev/battle-kit.js`: `devStage()`, `makeSource({ enemy, more, seed, cells, disclosure, chainIndex, chainTotal })`
  (BattleLaunch, as Unity's BattleBootstrap), `stepTo(source, until)` (takes automatic events / ends turns without a
  screen and returns the events, so you can feed real `DepictionEvent`s and cues to your beat methods), and
  `mountAlone(stage, createX, opts, ctx)` (the scene's own env via `createBattleEnv`, with stand-in geometry for the
  other modules). Screenshot steps go in `dev/battle-<module>-steps/`.
- The scene's own steps: `dev/battle-steps/manual.mjs` (start, hover, hold over the enemy, play, enemy turn) and
  `dev/battle-steps/auto.mjs` (open with `?auto=1`: start, mid, result).
- No console errors. Read your screenshots at 1920×1080 and at 1280×720 (`page.setViewportSize`).

### 8.1 Dev build, stand-ins and replays

- `node dev/battle-build.mjs dev/<page>.js` builds like `tools/devbuild.mjs`, plus a resolver: an import of a
  `src/` module that does not exist yet resolves to its stand-in under `dev/battle-shim/` (same path below
  `src/`), or to a module that throws "not ported yet" when evaluated. It prints which stand-ins it used; once
  the real port lands the real file wins. The stand-ins are dev-only copies of the C# (`Script/` layout files);
  never import `dev/` from `src/`.
- `dev/battle.js?replay=1` (and automatically when `BattleLaunch` / `Enemies` / `GreedyPlayer` cannot load)
  replays the C# `CoreBattleSource`'s own runs from the oracle (`oracle/out/scripts.json` → `node
  dev/battle-replays/extract.mjs` → `dev/battle-replays/replays.json`, read by `dev/battle-replay.js`): Unity's
  frames, events, cues, guide lines, previews and refusal texts, for all 13 enemies. With `auto=1` the scene plays
  each recorded battle to its result card through the hand's own drag path. Any mismatch between the scene's
  beats and `EffectPlan` shows up as a `console.error` ("EffectPlan lists waits the screen did not hold").
- `dev/battle-contract.js` mounts the three modules alone and checks every member this document lists
  (`node dev/battle-build.mjs dev/battle-contract.js`, then `node tools/shot.mjs dev/out/battle-contract.html dev/shots/contract
  --steps dev/battle-steps/contract.mjs`: prints `contract {"missing":[]}`). Run it after replacing a module.
- Steps in `dev/battle-steps/`: `manual.mjs`, `auto.mjs`, `keys.mjs` (digit, speed, pile list, journal, Esc, Space +
  held fast-forward, 1280×720), `finish.mjs` (to the end, prints the outcome), `chain.mjs`, `destroy.mjs`, `peek.mjs`.
- `dev/battle.js` (the playtest's page) also takes `ids=<id,id[,id]>` (the enemies `pair=1` fights; a line too short for
  them is widened, and the bar offers only the widths that hold them), `realJournal=1`, `realSettings=1` and
  `swift=<n>` (俊敏 on the player from turn 1), and (review 16) `swap=<from>:<to>[,…]` — the prototype deck with every copy
  of a card kind replaced (e.g. `swap=thrust:rock_stance,feint:water_stance` for the player's stance path, §5.11 and the
  stacked ×2; `swap=thrust:stone_throw&gap=1` with `pair=1` for a cost that differs between two enemies) — and `gap=<n>`
  (`BattleLaunch.StartGap`). `node dev/battle-autoplay.mjs` plays every enemy, the 2-enemy pair (7 and
  8 cells) and the 3-enemy line (8 cells) through real input, takes 俊敏's step by click and by keys where a battle
  runs with `swift`, and checks the screen against the source after every action (`dev/battle-read.js`, which also
  checks the free-step marks against `CanTakeFreeStep`). Review 16: the free step's locked card is read on the press (the
  line's 3.5 s are real time, and a slow drag on a loaded page outlasted them — a false failure); with the pile list open a
  card is pressed and the drawer's refusal line is checked; X / the plate wait out the turn's first 0.5 s. Review 21: at the end it waits
  for the result paper and keeps its text (`paper` in the report); a loss's result screen is read, shot, and 「手記へ」 is pressed
  as a player would (`onFinished` must not come before it).
- `dev/battle-read.js` (review 15): the floor's N at rest follows a Tab's choice (`player.restingUnit`); a fallen enemy's panel
  may be hidden while it gives its place up (`data-yielded`); the dark cards at a wait are the ones `Inspect` refuses for cost
  or 呪縛 (not `OutOfRange`). Review 17: a selected or held card that frames nobody (a self card, `framedUnit()` −1) keeps the
  Tab's N, so the check falls back to `player.restingUnit` (it reported a false gap mismatch after Tab and a self card's digit).
- Review 13's steps (`dev/battle-review13/`): `freestep.mjs`, `drawers.mjs`, `hoverline.mjs`, `chrome.mjs`,
  `movearrow.mjs`, `tabgap.mjs`, `second.mjs`, `capture.mjs` (CAP = push | strike | word | float2), `jsound.mjs`.
- Review 19/20's steps (`dev/battle-review20/`, shared helpers in `lib.mjs`): `probe-geo.mjs`, `drawers.mjs` (badges / panels
  beside the journal and the pile list), `freestep.mjs` (icons, tips, ←/→ keys), `tabtag.mjs` (rest focus, tag on the top
  edge, badge lift), `row.mjs` (one badge row), `beats.mjs`, `wall.mjs`, `kill.mjs`, `cutshort.mjs`, `recount.mjs`,
  `fftap.mjs`, `autoselect.mjs`, `stance.mjs`, `timeline.mjs`, `detail.mjs`, `loss.mjs`, `fall.mjs`, `misc.mjs`, `trait.mjs`,
  `esc.mjs` (no gear, Esc calls nothing), `esc-optin.mjs` (`?settingsInBattle=1`), `log.mjs`. The autoplay's chrome pass checks that there is no gear and that Esc with nothing open calls nothing
  (`?settingsInBattle=1`: the gear, and Esc reaching `onSettings`).

### 8.2 Known issues of the ported bridge (not the scene's)

- An elite's or boss's second action (review 16): `OmenFrame` carries no hit mark for the plan (`PlanSideGlyph` is the v4.2
  reach text) and the floor no cells for it, so the promoted badge shows neither the hit mark nor the swatch and the floor no
  stripes during that action (battle-visual-v1 §4.4, battle_ui_ux_v2 §5.16 順 4 「狙うマスも 1 手目の後の位置から数え直す」); the
  writer would have to write the re-read plan's `Hit` and aimed cells (Unity draws nothing there either).
- A move stopped at an edge or by another figure (battle_ui_ux_v2 §5.7 順 5, §3.14: a 40 ms bump that way): the core writes no
  event for a move that went nowhere (`TurnLoop` returns at `shift.To === shift.From`), and a move cut short reads in the
  script like one 鈍足 shortened (`CellsMoved` is all there is), so the scene cannot tell — it needs a cue from the writer.
  Review 17 (scene addition): a played card whose face carries a 詰める / 離れる value (`CardValueKind.Close` / `Back`) and whose
  event moves no one on the floor and has no 呪縛 / 鉤爪 line leans the hero `L.MOVE_BUMP_MS` (40) toward that way and back
  (`field.bump`) where the move would have played — the 0-cell case. Review 20 (scene addition): a move cut short is read the
  same way — the face's 詰める / 離れる n against the cells the hero's own `RangeSwitch` cues moved, and the bump plays after the
  last of them when it moved fewer and ended against something (the gap 0 for 詰める, the line's first cell for 離れる). With 鈍足
  taking the move to 0 the same bump plays where §5.7 順 9 wants the grey 「動けず」 (the script does not tell the two apart).
- A blow into the wall (battle_ui_ux_v2 §5.7, review 19): the script carries no wall flag, so the scene reads it — an enemy's
  walk into the edge is the writer's own `EnemyAction` event titled 「壁に当たる」 (`CoreScriptWriter.OpenIfBoundary`), a push
  into the wall a blow on the pushed enemy after its `RangeSwitch` in a played card's event. A 「壁」 label of the source would
  replace the title match.
- An enemy's 構え omen (review 19/20): the omen's `ValueText` keeps the value of the plan it was written for (「無防備 +3」 on a
  turn whose 構え is already set); `CoreScriptWriter` writes it from the plan, the scene draws what it is handed.
- 威圧 on the player (battle_ui_ux_v2 §3.5: 「カードの値と予測値が 3 下がり、値の横に威圧のアイコンと組で「−3」」; review 17):
  `CardFace.Values` (written by `CoreScriptWriter`, as the C#) print the card's own values, while `PreviewFor`, `HpLossFor` and the
  hit include the −3 — 袈裟斬り prints 15 where the tag, the wick and the hit say 12. The scene shows the faces it is handed (it
  must not take 3 off itself); the writer would have to write the lowered value and a 威圧 mark on the face (Unity is the same).
- No afterimage (残像 α 0.35) while a move or push card is held (battle_ui_ux_v2 §3.1 / §3.2 / §3.8 / §5.7): the View must
  not count cells, and neither `CardFace` nor `TurnLoop.Preview` carries the cell a 前へ / 後ろへ card takes the player to, or
  the cell (and wall) a 押す / 引く card sends an enemy to. It needs a documented source extension like the free step's — e.g.
  `CardFace.MoveTo` / `PushTo { unit, cell, wall }` written by `CoreScriptWriter` from the core's own resolution — which is
  outside the scene's files (Unity has none either).

- The C# `CoreScriptWriter` throws `KeyNotFoundException` on a two-enemy fight (it looks the back enemy's action up
  in the front enemy's definition); the JS writer's multi-enemy extension replaces that path. A pair needs a line of
  7+ cells (`TurnLoop.Validate`: "Enemy 1 does not fit on the line"); the dev bar offers only the widths that hold it.
- At 開示度 0 the writer still writes the cells the enemy aims at (`FloorCells.AimOf` ignores the level), while
  battle_ui_ux_v2 §7 shows them from 開示度 1. Unity draws them; review 21: the scene now leaves them off the floor for an
  enemy whose omen's `Disclosure` is under 1 (§6b), and hides the badge's swatch with them. The writer should blank them
  (`OmenDisclosure` / `CoreScriptWriter.FloorOf`).
- `CoreScriptWriter` writes `Corner.Floor = 1` and `MiasmaPercent = 0`; the corner shows `ctx.layer` instead and
  reads `ctx.miasmaPercent` when the app gives it.
- Review 21, what the script does not carry (the scene shows what it is handed; each needs the writer or the source):
  - The faces' values under 集中 / 強化 / 威圧 (§3.5 「面の値と予測値を…出し」): `CardFace.Values` stay the printed values. The
    scene marks the faces (§6a) and the prediction tag carries the shifted value; the writer would have to write the shifted
    numbers.
  - A boss's second phase and its adaptation (battle_ui_ux_v2 §5.17 第二相 / 適応): nothing in the frame or the cues says a
    phase changed or what it adapted to, so the scene plays no phase banner and marks no adaptation.
  - An enemy's 構え by its clause (「構え・狩りの構え」's effect on the stance tip): the enemy's lasting chip carries
    `StatusChipText`'s fallback wording, not the stance's own clause (a `CoreText` / writer question).
  - An elite's plan (the second action): no hit mark and no cells for it (`OmenFrame`, as the first item above).
  - The detail's 習熟の段と伸びる先, 探索の面の印 and 入手先 (§3.13): not in `CardFace`.
- Review 21, the two documents differ on a status chip's detail: battle-visual-v1 §7.3 shows it on hover, battle_ui_ux_v2
  §3.13 on a right click. The scene keeps the hover and adds the right-click pin (§6c); one of the two should be changed.

## 9. The baseline (what ships now)

`hand.js`, `field.js` and `hud.js` ship as **baselines**: every method of §6 exists with the right signature and
timing, and the battle plays end to end (cards clickable and draggable onto the enemy / above the line, figures,
floor and HP visible, omen, piles, end turn, banners, result card). Their looks are minimal. Each module agent
replaces its file (and its CSS) wholesale, keeping the factory signature and every method; adding methods is
fine (tell the architect); removing or renaming one is not.
