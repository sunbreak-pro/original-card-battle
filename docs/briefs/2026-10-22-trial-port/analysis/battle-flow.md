# battle-flow — the battle scene's core in Unity (BattleScene / BattlePlayer / BattleClock / BattleLayout)

Analyst report for the Trial port. Read-only analysis; nothing outside this file was changed.

Path shorthands used below:

- `$J` = `scratchpad/proto/prototypes/browser-game/src/scenes/battle/` (the approved JS scene)
- `$DOC` = `scratchpad/proto/prototypes/browser-game/docs/BATTLE_SCENE.md` (the module contract, read in full)
- `$UP` = `trial/unity-port/unity-project-kit/Assets/View/Depiction/DepictionPlayer.cs` (the Unity original)
- `$US` = `trial/unity-port/unity-project-kit/Assets/View/Depiction/Script/` (pure C#, engine-free)
- `$UB` = `trial/unity-port/unity-project-kit/Assets/View/Depiction/Bridge/` (pure C#, engine-free)
- `$UT` = `trial/unity-port/unity-project-kit/Assets/View/UiTween.cs`

Winner rule used throughout: **the JS wins for the view (look, feel, timing, flow order); the C# wins for rules** (verdicts, numbers, texts the source/writer produce).

---

## 0. Summary of the porting decisions in this area

1. `player.js` (2329 lines) is the approved, newer `DepictionPlayer`. Port it 1:1 as `Trial/Battle/BattlePlayer.cs`, keeping every function name (PascalCase) and every comment's review tag, so a reviewer can put the two files side by side. Do **not** start from `DepictionPlayer.cs`: ~55 behaviour differences (§1.9) all go the JS way.
2. The player, the clock, the layout constants, the module contracts and a tiny promise type (`Beat`) go into one **engine-free** assembly `Trial.Battle.Flow` (`noEngineReferences: true`, references `Depiction.Script`, `Depiction.Bridge`, `BattleCore`), so the whole flow (plan waits, mash window, fast-forward arming, held two-blow actions, result holds) can be tested by `dotnet test` with fake hand/field/hud modules, exactly like the existing Script/Bridge assemblies are.
3. Async pattern: a custom task-like `Beat` (JS `Promise<void>`) with `[AsyncMethodBuilder]`, so `async function playEvent(ev) { … await holdBeat(…) … }` becomes `async Beat PlayEvent(DepictionEvent ev) { … await HoldBeat(…) … }` line for line. Continuations are posted to a microtask-like queue drained by the scene in a fixed order (§4.2). No Unity coroutines, no `Task`, no `Awaitable`, no `UiTween.Speed` in the trial stack.
4. One scaled scene clock instance (`BattleClock`, port of `clock.js`) plus a real clock; never the static `UiTween.Speed` (Battle.unity keeps using that).
5. Layers = 11 full-stage children of one 1920×1080 stage `RectTransform`, each a nested `Canvas` (hierarchy order = the CSS z-index order) with a `CanvasGroup`; only interactive layers get a `GraphicRaycaster`.
6. Input = Input System **polling** in the scene driver (as `DepictionPlayer` already does, `$UP:1938-2039`), dispatched to the same handler functions the JS registers on `window`/root, in the browser's order; the DOM "target" checks (`isControl`, `isForeign`) become one `EventSystem.RaycastAll` lookup of the topmost hit's layer.
7. All stage coordinates stay **y-down stage px** in every C# module (as JS); conversion to uGUI happens in one placement helper at the leaf. This keeps every JS number copyable.

---

## 1. The player: flow, states, beats, sounds, speed, FF, chain, result, free step, auto play

### 1.1 Input states (`$J/player.js:224, 360-364`; `$DOC §4.1`)

| state | set at (`player.js`) | hand interactable | end turn | fast-forward |
| --- | --- | --- | --- | --- |
| `opening` | initial (224) | no | press only marks the mash window (1892) | no |
| `auto` | `runAutomaticEvents` 853 | no (`setHandInteractable(false)` 854) | swallowed + opens mash window (1901) | yes (`canFastForward` 2261) |
| `idle` | 900 (after the loop), 1655 (after a refused card returns) | yes (902) | yes if `canEndTurn` 1878-1880, subject to the open guard / mash cap (1906-1912) | no |
| `resolving` | `playAccepted` 1586, `returnToHand` 1650, `playFreeStep` 1823, `playThenContinue` 1929 | no | swallowed (as auto) | yes |
| `over` | `finish` 2012 | no | no | no |
| `halted` | `sourceFailed` 1951 | no | no | no |

`player.onState(fn)` (2195) notifies; the scene uses it to disarm FF and to run the Enter auto-select (`BattleScene.js:459-487`).
Unity has only `_busy` and `_dragging` (`$UP:117-119`), no states, no listeners.

### 1.2 The flow, function by function (JS lines; Unity counterpart)

- **start** `player.js:2209-2218` — `snapPanels = true`; `field.setStanceGlow(Player, StanceGlow.IsOn(frame.Player))` (enemies are snapped by `field.applyFrame(…, {first:true})`); `applyFrame(frame, true)`; `field.snapHome(Enemy, unit)` for every enemy; `showSpeed()`; then `chainOpening()` (1999-2008) and only then `runAutomaticEvents()`. Errors are caught and logged. Unity: `Start` `$UP:335-385` / `Restart` 202-253 (no chain banner, no opening fade).
- **runAutomaticEvents** `player.js:851-905` — `busy=true; setState('auto'); setHandInteractable(false); updateFloorReach()`. Loop while alive && !halted && (a fetched `next` exists || (!Finished && !WaitingForPlayer)):
  1. take `next` or `ctx.source.AdvanceAuto()` (try/catch → `sourceFailed`);
  2. several enemies: `startsOtherEnemy(ev)` → `clock.wait(max(0, L.BETWEEN_ENEMIES_MS 600 − EffectFlow.AfterAutoEventMs 350))` (865-868, 907-919);
  3. `await playEvent(ev)`;
  4. if `heldEnd` (the event's last cue was the enemy's Hit on the player, 1313-1317): peek `next = AdvanceAuto()`; if `continuesHeld(next)` (same unit, title starts with held title + `（`, 921-928) → `carried = heldEnd; heldEnd = null; afterWait(CONTINUE_PAUSE_MS 80); eventEnded(); continue` (no 350 wait); else `await playHeldEnd()` (937-949: flushes the side label, `OmenSpend` fadeOut, `EnemyReturn` returnHome);
  5. `afterWait(EffectFlow.AfterAutoEventMs = 350)` (sets `afterWaiting`), `eventEnded()` (eventEnds++, listeners).
  After the loop: `busy=false`; if Finished → `finish()`; else turn-open bookkeeping (`turnOpenedAt` when a TurnStart played, `askedAt`), `setState('idle')`, `openFreeStep()`, `setHandInteractable(true)`, `refreshPlayable()`, `scheduleAuto()`.
  Unity `$UP:727-755`: AdvanceAuto → PlayEvent → Wait(350); no peeking, no held actions, no between-enemy pause, no error path, result straight from here.
- **playEvent** `player.js:951-1016` — `beginPlan(ev)` (443-451: plan queue = `EffectPlan.StepsOf(ev)`, sound budget 2, essential count, `pendingSide = null`, multiplier labels, log lines); carried side label; `enemyPhase` bookkeeping (955-957); acting light off unless the same enemy acts again (959-962); `showEnemyTurn(false)` when `ev.After` is not the enemy's turn (964); **TurnStart**: reset `playedThisTurn`, `turnStarted`, `restingGapUnit`; heartbeat ×3 (`sfx('heartbeat', k*900)`, k=0..2) when 0 < HP < 15; `holdBeat(TurnBanner, hud.banner(DepictionText.YourTurn, 'player', 300))` (965-974); cues one by one (`playCue`), the omen blink after the TurnStart's DrawHand cue (977-984); **TurnEnd**: `sfx('foeTurn')`, `showEnemyTurn(true)` (if Ongoing) then `holdBeat(EnemyTurnBanner, hud.banner(DepictionText.EnemyTurn, 'enemy', 400))` (985-991); per enemy: `setEnemyHomeCell` from `ev.After`, skip held enemies, `returnHome` (beside, `EnemyReturn` 140) or `setPose(Idle)` (993-1006); player `Idle`; held side label; `flushSide()`; `endPlan()`; `applyFrame(ev.After)`; carried → `playHeldEnd()`.
  Unity `$UP:757-807`.
- **holdBeat** `player.js:645-662` = `$UP:1643-1666` (identical logic: `takePlanned` → off: settle+played; `LeadMs` → play beside, wait the lead; else wait `EffectPlan.WaitMs` and the play, whichever is longer, then `played`). `effect` 409-415 = `$UP:1568-1579` minus the trace. `takePlanned`/`endPlan` 619-638 = `$UP:1600-1630` (`console.error` = `Debug.LogError`).
- **applyFrame** `player.js:672-694` — `hud.applyFrame(frame, {first, keepOmen: held unit})`, `field.applyFrame(frame, {first})`, `showEnemyTurn(enemyTurnOf(frame))`, stance glows (not on first), `hand.setStatusMarks(statusMarksOf(frame.Player))`, `hand.sameHand ? bind : build`, `hand.layout()`, `frameStanceHint`, `showReserveHint()`, `showHpLoss()`, `showLowHp(hp)`. Unity `$UP:1692-1728` does the same content in one class.
- **per frame** `player.js:2129-2133` → `followFigures()` (799-829: follow x = figure's x while `walking`, else `homeX`; `hud.follow({playerX, playerWalking, enemyX, enemyTop = FEET_Y − height, cells, enemySize, units[], snap})`) and `updateFloorReach()` (780-787). Unity `$UP:403-412, 541-605, 653-663`.
- **release** `onRelease(cardId, zone, unit)` `player.js:1668-1688` → `TryPlay` (target only with ≥2 enemies, 1663-1665) → accepted: `acceptedPlays++`, `playAccepted` (1584-1631); refused: `showRefusal` (1690-1697) + `returnToHand(cardId, shake = verdict ≠ WrongZone && zone ≠ None)` (1647-1660). Unity `$UP:2372-2398`.
- **playAccepted** `player.js:1584-1631` — `busy`, `resolving`, hand off, `playedThisTurn++`, `eventFace`, result tally, `beginPlan`, **budget = 1** (the hand's release sound is the action's), `to = receiverCentre(unit)` for Single else null, the fired trait's condition word for the card, `holdBeat(CardRelease, hand.release(…).done, played → released(…))`, bump bookkeeping, each cue (+ the 40 ms bump where a move went nowhere / was cut short), player Idle, `flushSide`, `endPlan`, `applyFrame`, `afterWait(EffectFlow.AfterPlayerEventMs = 250)`, `eventEnded`, `runAutomaticEvents`. Unity `$UP:2400-2437`.
- **released** `player.js:1637-1645` — ghost from the receiver centre (Single) or where the card vanished; a stance card (`rel.keep`) stays at the stance slot until its exile ghost leaves; `effect(HandFan, hand.settle(120), hand.layout)`. Unity `$UP:2440-2449`.
- **requestEndTurn** `player.js:1886-1925` + **playThenContinue** 1927-1938 (§1.6 below). Unity `$UP:699-717`.
- **finish** `player.js:2010-2081` (§1.7). Unity `$UP:738-751`.

### 1.3 Beat map: cue → calls → plan steps → sounds

Nominal ms are `EffectCatalog` (`$US/Effects.cs:271-346`, written at ×1.25). `HB` = `holdBeat` (holds the flow), `E` = beside the flow. `T` = cue target, `S = hud.status(T, unit)`, `F = field`. Every float starts at `chest` (65 % of figure height from the feet, `L.CHEST`) and rises `chest.y − head.y` (to 84 %, `L.HEAD`) over 600 ms ease-out, stops 8 px under any omen badge on its path (`FLOAT_UNDER_BADGE`, player.js:37) and 16 px over the feet line (`lowest = 570 − 16 = 554`, 43, 432-433), rides with its figure (`track`, 436-441), and may carry a 16 px side label.

| cue | JS calls (player.js lines) | plan steps (EffectPlan) | sounds (player) | Unity (`$UP` lines) and what differs |
| --- | --- | --- | --- | --- |
| any, `EffectGrammar.MarkOf = Shape` | `E(GrammarMark 300, F.mark(T,{fx, intensity, ms, crestLight, unit}))`; Form lights the stance glow from `ev.After` (1059-1065, 841-847) | none | `stance` on a Form TraitFire (1114) | 821-830, same |
| `ChestHeave` | `E(BreathHeave 400, F.heave(T, ms, unit))` (1066-1068) | none | – | 831-833 (y-scale 1→1.05→1, InOut) same |
| `GuardReset` | `S.setGuard(GuardAfter)` (1074-1076) | none | – | 838-840 same |
| `StaminaChange` +n | `HB(StaminaRecover 80×n stagger 80, S.lightPips(after, max, 80), settle setStamina, count n)` (1079-1083) | StaminaRecover×n | `pip` ×n at k·80 ms (player side only, 1080) | 843-849 same, no sound |
| `StaminaChange` −n | `S.setStamina`; `E(StaminaChange 220, S.popPips(ms))` (1084-1087) | none | – | 851-855 (Pop 1.2→0.8→1 on pipRow) same |
| `StatusChange` | `beat = StatusBeat.Of(cue)`; `HB(beat, S.playStatus(text, stacks, beat, ms), settle setStatus)` (1090-1095) | StatusApply 200 / Stack 150 / Tick 120 / Vanish 150 | `seal` (budget, incidental) when StatusApply with `Fx == Spirit` (1092) | 858-863 same, no sound |
| `DrawHand` | `drawHand(After.Hand, Amount, RebuildAfter)` (1097-1099, 1503-1520): optional `HB(PileRebuild 200, slideDiscardOntoDraw)`; `hand.build(faces)`; `HB(CardDraw 260 + 60·(n−1), hand.flyDrawn(first, rebuildAfter, {from: pileMiddle(Draw), onRebuild}), count n)` | [PileRebuild] CardDraw×n | `rebuild` (1498, not budgeted); the hand plays `draw` per card | 865-868, 1390-1442 same timing (`EffectPlan.DrawCardStartMs` / `RebuildStartMs`) |
| `OmenShow` | `hud.syncOmenLook(After, unit)`; `badge.bind(omenFrameOf(After, unit))`; `E(OmenShow, F.drawAim(After, unit))` (aimed cells drawn left→right 30 ms a cell); `HB(OmenShow 240, badge.popIn(240))` — drop 24 px + fade in (1101-1111) | OmenShow | `omenDrop` (1106, not budgeted) | 870-873: `omenBadge.PopIn` scales 0.6→1, no floor drawing, no sound |
| `TraitFire` 構え・name (stance set) | `HB(TraitFire 200, wait 200 + (enemy) hud.flyOmenToStances(unit, L.STANCE_FLY_MS 300))` (1116-1125) | TraitFire | `stance` if Form | 875-877 → `FireTrait` 1205-1210 (burst Warm 260 + warm 40 px line rising 70) |
| `TraitFire` fired trait of the card / the enemy's action | `pendingSide = {text: traitOf().side, kind: guard/hp/none}`; `HB(TraitFire, wait 200 + (enemy) badge.traitWord(condition, L.TRAIT_WORD_MS 150))` (1126-1137, 1404-1421) — the label goes onto the next number (`takeSide`, 554-562) or rises alone at the event's end (`flushSide` 607-617, 32 px, warm/ink) unless a SideBonusMiss whiffed | TraitFire | – | same as above (Unity floats the raw line over the head) |
| `TraitFire` other (構え停止・/構え復帰・, MoveBlocked line) | `HB(TraitFire, fireTrait)`: burst 260 (not for blocked) + `F.floatText(chest, text, ink2 (blocked) / warm (player) / ink (enemy), 36, plain)` + wait 200 (1138, 1386-1396) | TraitFire | – | as above |
| `Slash` | `attack()` (1438-1481): `HB(AttackLunge|EnemyLunge 100, F.lunge(src, 46, 100, su))` (skipped for the 2nd blow of a carried action: plan step taken, pose Act, 1451-1454); `E(TraitAccent 150)` if `cue.TraitAccent`; enemy blow: system hit sound (essential); `HB(StrikeShape 120, F.strike(T, {byPlayer, system, intensity, ms, unit, source}))`; `HB(HitStop 30 | HitStopStrong 90, wait)`; strong → `E(ScreenShake 180)`; HP unchanged → (player: hit sound) return; else player hit sound, `F.burst(chest, omen, 200 + 60·intensity)`, float `−` + `hpLostOf` (`ink`, `numberFont(intensity)`, side = trait label + multiplier), `E(HitFlash 260, F.flash(T,'white'))`, `E(TargetRecoil 260)`, `drainHp`, then `HB(AttackReturn 120)` unless `EffectPlan.MoveFollows` | Lunge, StrikeShape, HitStop[Strong], [HpDrain(+lead 80 when a move follows), AttackReturn] | `hitSlash/hitThrust/hitBlunt` by `cue.System` (Sweep → hitSlash), essential | 992-1033: number = bare `Amount` at 56/72/88/108, no sign, no clamp, no side label; same beats |
| `GuardGain` | `HB(GuardGain 260, burst(chest, guard, 220 + 40·int) + float '+n' guard numberFont + S.popGuard(after, 260))` (1146-1153) | GuardGain | `guardUp` (incidental) | 883-885, 1140-1145 same minus side label/sound |
| `RangeSwitch` on the floor | pushed → `E(PushMark 1000, float word on the moved figure)` where word = 「引き寄せ」 if the moved figure ends nearer the mover else `DepictionText.Pushed` (1157-1160, 1366-1384); `HB(FloorMove 160·cells, F.moveOnFloor(cue, cells, 160·cells), settle snapOnFloor, count cells)`; `HB(GapRecount 150, F.recountGap(recountTo().gap, PlayerCellAfter, playerSize, recountTo().cell, 150))` (1161-1168) | FloorMove×cells, GapRecount | – | 887-907: 「押し出し」 always, at the player's head − 30, does not ride; recount to `cue.GapAfter` |
| `RangeSwitch` no floor | `HB(RangeSwitch 320, wait)`, `HB(RangeTagFlip 150, wait)` (1169-1173) | RangeSwitch, RangeTagFlip | – | Unity moves to slots (dead path: the core always writes a floor) |
| `StanceCue` | `HB(ReservePips 250, hud.stamina.glowReserve(250) | wait)`; `HB(StanceCue 460, endTurn.pop(200) → burst(chest, guard, 300) + float cue.Text guard 48 + S.popGuard(after, 260))`; then `hud.reserveHint('')` (1176-1189) | ReservePips, StanceCue | `breath` then `reserve` (incidental) | 909-914, 1147-1163 same minus the hint clear |
| `DiscardHand` | `HB(HandDiscard 240 + 60·(n−1), hand.discardAll(pileMiddle(Discard)), count n)`; `hand.clear()` (1191-1193, 1523-1527) | HandDiscard×n | the hand plays `discard` per card | 916-918, 1461-1481 same |
| `EnemyWindup` | hand off; acting light moves to this unit; `badge.promotePlan(150)` (an elite's 2nd action); `badge.setActing(true)`; `E(OmenExecute 200, badge.flare)`; `HB(EnemyMotion 220, F.enemyMotion(system, 220, unit), settle pose Act)` (1195-1208) | EnemyMotion | – | 920-928 same minus promotePlan |
| `Defeat` | enemy: `E(OmenSpend 200, badge.fadeOutAll)` + `F.retireAimOf(unit)`; alone → `HB(Defeat 800, F.fall(T, 800, unit), settle setDown)`; others stand → `HB(Defeat, F.fall(…,{keepFoot}))` → `F.fadeFoot(T, 300)` → `S.greyOut(300)` (held, not planned) (1210-1236) | Defeat | – | 930-940: fall only |
| `SideBonusMiss` | `HB(SideBonusMiss 260, float cue.Text 'whiff' 40 {struck, rise −50, lowest 554} at omen box centre + 64 px down + badge.strikeSide(260), settle strikeSideNow)` (1238-1245) | SideBonusMiss | – | 942-944, 1212-1217 same place |
| `GuardBlock` | `S.setGuard(after)`; sound if no Hit follows; after==0 → `E(GuardBreak 200, S.crackGuard)` else `S.popGuard(after, 260)`; tag 「Guard −n」 34 px guard (`L.GUARD_NUMBER_SIZE`) at chest ± 12 px anchored when an HP number follows, centred alone; wall → `wallBeat`; `HB(GuardBlock 320, F.shieldBlock(T, n, 320, unit, {broken}))` (1247-1270) | GuardBlock | `guardBreak` / `hitGuard` (essential) when no Hit follows | 946-955: tag at chest + 40 − 60 px x, 44 px; slab shield |
| `HpChange` | `E(DamageNumber, float Text + ' ' + (+n / −n typographic minus), heal / omen, 40)`; `drainHp` (1272-1279) | HpDrain | – | 957-965: ASCII sign, Accent/Omen |
| `Hit` | float `−` + hpLost (`omen`, numberFont, side) — beside the Guard tag 24 px out on the figure's back riding the tag's y (`hpBesideTag` 1337-1345) or on the chest; `E(HitFlash, white)`; `E(TargetRecoil)`; strong → shake; `drainHp`; player target: if last cue of an EnemyAction → take OmenSpend/EnemyReturn steps without waiting and set `heldEnd` (1313-1317) else `HB(OmenSpend 200, fadeOut)`, `HB(EnemyReturn 140, returnHome)` (1281-1325) | HpDrain(+lead), [OmenSpend, EnemyReturn] | `hitBody` (or `hitBlunt` for a wall) on the player, essential | 967-986: violet flash (`BattleTheme.Omen`), returns after every hit |
| `drainHp` | player HP drop under 15 → `pulseLowHp` (vignette 0.5→0.25 over 200); `HB(HpDrain 300, S.animateHp, settle setHpKeepingTrail, played → E(HpTrail 500, S.animateTrail, snapTrail))` (1484-1489) | HpDrain | – | 1042-1046 same minus vignette |
| idle with nothing playable | `E(endTurn.flash(L.END_TURN_FLASH_MS 600))` once per wait (1733-1748) | – | – | none |
| TurnStart / TurnEnd banners | see 1.2 | TurnBanner 300 / EnemyTurnBanner 400 | `heartbeat` ×3 / `foeTurn` | 767-783 |

Multipliers (`player.js:127-136, 563-602`): each blow (Slash + following GuardBlock/Hit on the same target and unit) is matched to the event's StatusChange spending 脆化 on the struck side or 強化 on the striker (nearest before, else after); the number the blow shows (Hit > GuardBlock > Slash with settled HP) carries 「×1.5 脆化」/「×1.5 強化」 after any trait label. Factors: `BattleCore.Constants.FragileMult` / `EmpowerMult` (C# `double` — format with `CultureInfo.InvariantCulture`).

Walls (`player.js:87-94, 539-548, 1432-1434`): an EnemyAction titled `壁に当たる` (C# writer `$UB/CoreScriptWriter.cs:206-213` already writes it) → the player's blow cues are the wall's (`dir −1`); in a PlayCard event, a GuardBlock/Hit on an enemy after its own push `RangeSwitch` is the wall's (`dir +1`, or −1 if the face has a Pull value). `E(HitFlash, F.wallHit(T, dir, 80, unit))`; side label 「壁」; sound `hitBlunt` for `hitBody`.

Move bump (`player.js:1550-1582, 1605-1619`): a card with 詰める/離れる value whose event moved nobody (and no MoveBlocked line) leans `F.bump(Player, dir, 40)` after the stamina cue; a move cut short leans after its last own move.

### 1.4 Sounds and the per-event budget

- `beginPlan`: `budget = 2`, `essentialLeft = min(2, essentialSoundsOf(ev))` (`player.js:446-447, 383-394`): essential = each Slash, each Hit on the player, each GuardBlock not followed by a Hit on the same target. `sfxBudget(name, essential)` (375-381): essential consumes `essentialLeft`; incidental plays only on a slot essentials leave. PlayCard: `budget = 1` (1594-1595).
- Budgeted: `hitSlash/hitThrust/hitBlunt` (by system, `HIT_SOUND` 204-209), `hitBody`, `hitGuard`, `guardBreak` (essential); `guardUp`, `seal`, `stance`, `breath`, `reserve` (incidental).
- Unbudgeted (`sfx`): `heartbeat` ×3 (0/900/1800 ms delay), `foeTurn`, `omenDrop`, `pip` (k·80), `rebuild`, `uiClick` (speed, free-step pick), `refusal` (drawer/journal/free-step refusals), `hover` (free-step focus change).
- The hand plays `hover, grab, frameIn, frameOut, noReach, cancel, refusal×2, releaseAttack/Defense/Skill/Stance, draw, discard`; the hud plays `uiClick`; the field nothing; the journal its own open/close (`$DOC §4.4`).
- Scene wrapper `safeAudio` (`BattleScene.js:33-47`): never throws; while FF is on, a same-name sound within `L.FF_SOUND_GAP_MS = 90` real ms of the last is dropped.
- Unity plays no sounds in `DepictionPlayer`. C# port: `IBattleAudio.Play(string name, float delayMs = 0, float mult = 1)` (the audio analyst's synth), wrapped by `SafeAudio` in `BattleEnv`.

### 1.5 Speed, 演出短縮, fast-forward, halt, pause (`clock.js`, `BattleScene.js:272-318, 416-451`, `layout.js:88-103`)

Scene time scale, recomputed every frame (`clock.js:49-56`):

```
scale = paused ? 0
      : TweenSpeed(step) × (halted || settings.shortFx ? L.SHORT_SCALE 1000 : ff ? L.FF_SCALE 4 : 1)
TweenSpeed = Multiplier/1.25 → ×1.0: 0.8, ×1.25: 1, ×1.5: 1.2   ($US/BattleSpeed.cs:59)
pacedScale (floating numbers/words only) = paused ? 0
      : (!ff || halted || shortFx) ? scale : min(scale, TweenSpeed × L.FF_FLOAT_SCALE 1.5)   (clock.js:58-62)
```

- **Speed** (`player.js:2113-2125`): `settings.speed` 1 | 1.25 | 1.5 ↔ `BattleSpeedStep`; `cycleSpeed` = `BattleSpeed.Next`, `sfx('uiClick')`, `hud.speed(Label, Chevrons)`, `ctx.onSettingsChange(settings)`. Read live by the clock. Unity: `SetSpeed` writes `UiTween.Speed` + `PlayerPrefs "Depiction.BattleSpeed"` (`$UP:284-292`). Trial: keep the JS semantics; persist through the trial's settings store (not the Depiction PlayerPrefs key).
- **演出短縮** (`settings.shortFx`): ×1000 on all scene tweens/waits; input thresholds unchanged. Unity has no switch (only `HaltedTweenSpeed` on 降参).
- **Fast-forward** (scene only; Unity has none — `$UT:2-4,20` mention ×8 but nothing sets it): `setFastForward((ffHeld || pointerFF || ffArmed) && !overlayUp && player.canFastForward())` (`BattleScene.js:296-298`). A fresh Space press or a left press on the arena arms a one-press FF: `ffUntil = max(ffUntil, eventEnds + (afterWaiting ? 2 : 1))` (299-305); on each `onEventEnd`, if armed and `eventEnds ≥ ffUntil`: if it ran < `L.FF_MIN_RUN_MS 300` real ms → extend by one event, else disarm (312-318); state idle/over/halted disarms (459-462). Held Space / held button keep it on while held. The floats run on `runPaced` so they stay readable (≤ ×1.5 speed).
- **Halt**: `clock.setHalted` exists (`clock.js:169`) but nothing calls it; `player.halt()` (2302-2307) only stops input. `sourceFailed` → `halted` state + `ctx.onError`. The trial has no 降参. Unity `Halt()` (`$UP:265-278`) speeds leftovers ×1000 — keep `SetHalted` in the C# clock for parity, unused by the trial.
- **Pause**: the journal or the app's settings overlay holds the scene clock (`scale 0`) — `syncPause` (`BattleScene.js:193-208`); the hand's hover then runs on real dt (`hand.js:761`); the result hold counts only unpaused real time (`holdUnpaused`, `player.js:1958-1967`, 50 ms polls). The pile drawer does **not** pause.
- Per-frame motions (hover weights, snap pull) use scene dt in JS (so FF/short speed them); Unity uses `TweenSpeed` only (`$UP:301, 1867, 2328`). JS wins.
- **dt cap**: the stage frame clock caps a frame's dt at 50 ms (`scenes/common/stage.js:112`); Unity's UiTween is uncapped (`$UT:47, 60`). JS wins (a hitch slows the beat instead of skipping it).

### 1.6 End turn and the mash window (`player.js:1878-1938`, `BattleScene.js:156, 400-404`)

`requestEndTurn({queue, press})`:
1. `queue` while a refused card slides home → `endTurnQueued = true` (taken by `returnToHand` 1658).
2. `press`: `gap = now − lastPressAt`; `rhythm = gap ≤ 1500 ? gap : 0`; `win = clamp(rhythm × 1.3, 600, 1500)` (`L.END_TURN_MASH_RHYTHM/MASH_MS/MASH_MAX_MS`). Flow running (`auto|resolving|opening`) → `mashUntil = now + win`, swallowed. In the player's wait: guarded if `now − turnOpenedAt < 500` (`END_TURN_OPEN_GUARD_MS`) or (`now < mashUntil` and `now − askedAt < 1500`) → `mashUntil = min(askedAt + 1500, max(mashUntil, now + win))`, `hud.endTurn.refuse(220)` (6 px ×2 shake, dimmed), swallowed.
3. `canEndTurn` (idle, !busy, !holding, no overlay, `source.CanEndTurn`) → close free step, reset the Tab gap focus, `hand.cancel()`, `EndTurn()` (try/catch), `playThenContinue` (busy, resolving, `playEvent`, `afterWait(250)`, `eventEnded`, `runAutomaticEvents`).
Unity: `UpdateEndTurn` (`$UP:699-706`) — a press inside the plate rect when not busy/dragging/halted; no X key, no queue, no mash, no refuse feedback.

### 1.7 Chain switch, finish, result (`player.js:1957-2081`, `$DOC §4.6`)

- **Chain opening** (1999-2008): only when `ctx.chain.total > 1 && index > 1`: `field.appearEnemies(0, 0)` (enemies hidden), `hud.chainBanner('n 戦目', names, scale = TurnBanner on ? 1 : 0, onOut → field.appearEnemies(400 out, 0))` — the board 300 in / 700 hold / 400 out. Unity cuts straight in.
- **finish** (2010-2081), in order: `closeFreeStep(0)`; `over`; hand off; `refreshPlayable()` (guide quiet); `hud.setSpeedVisible(false)`; close the pile drawer; `E(OmenSpend 200, hud.retireOmens)` + `E(OmenSpend, field.retireAim)`; a loss without `Player.Down` → `await E(Defeat 800, field.fall(Player))`; loss with Defeat on: `field.dimFeet(600)` → `hud.lossInk('力尽きた', 800, 300)` → `clock.wait(1000)` (off: all at once); `E(ResultCard 400, hud.settleEnd(400, outcome) + hand.fadeAll(400))`; loss: `E(ResultCard, hud.lossWordOut(400))`; `await E(ResultCard, hud.resultCard(outcome, win ? 400 : 400 (LOSS.paperMs), info, {onButton}))`; `resultUpAt = real.now`; win (or `ctx.auto`) → `holdUnpaused(L.RESULT_HOLD_MS 1200)` real, loss → waits for 「手記へ」; either races `resultSkip`; then `ctx.onFinished()` exactly once.
- **skipResult** (2293-2300): counts only after the card has stood `RESULT_SKIP_AFTER_MS 400` and after a pause of `RESULT_SKIP_PAUSE_MS 350` since the last press (each press slides the pause).
- **resultInfo** (1974-1992): win lines 「{names}を倒した。」, 「ターン n ・ 残りの HP h / m」, chain 「i / n 戦目」; loss: title 「i 戦目で倒れた」 / 「第 L 層で倒れた」, 「残 HP h / m」, bars (攻撃 violet / 防御 guard / スキル amber incl. Move / スタンス crimson) from the scene's own tally, stats 「特性の発動 n 回」「n ターン戦った」, button 「手記へ」.
- Unity: `ShowResult` 520×220 black 80 % plate, 72 px bold 勝ち/負け rising 40 px over 400 Out (`$UP:1241-1275`), `BattleFinished` raised at the start (`$UP:750`); `DemoFlow` waits `EndScreenDelaySeconds = 1.2` realtime (`DemoFlow.cs:25, 171-205`).

### 1.8 俊敏's free step (`player.js:1750-1876`, `$DOC §4.8`) — JS extension of the source

- `openFreeStep()` at the turn's first idle: asks `CanTakeFreeStep()` once per turn (`freeStepTurn`), then `CanTakeFreeStep(−1/+1)`; `hand.setGrabLock(refuseWhileChoosing)`; `field.showFreeStep(PlayerCell, ways)`.
- Guide: 「俊敏で 1 マス動けます。矢印か「留まる」を選んでください（← / → と Enter）」; a card pressed → `refusal` + 「先に、動く先の矢印か「留まる」を選んでください」.
- `pickFreeStep(dir)` (1804-1818): 0 = stay (asks nothing); ±1 → `uiClick`, `closeFreeStep`, `TakeFreeStep(dir)` → `playFreeStep(ev)` (1821-1845: beginPlan, cues, no-cue → grey 「動けず」 float (`PushMark`, ink2 36), idle pose, flushSide, endPlan, applyFrame, afterWait 250, eventEnded, runAutomaticEvents).
- Keys: ← always picks back, → forward, a closed way falls to 留まる; Esc moves the focus to 留まる; Enter chooses the focus (1860-1876, 2318-2322).
- Event kind `'FreeStep'` is outside the C# enum; `EffectPlan.StepsOf` yields only the cues' own steps for it (no CardRelease, no banner) — safe with a new enum member (§5).

### 1.9 Every difference from `DepictionPlayer.cs` (JS wins unless stated)

| # | topic | JS (approved) | Unity original |
| --- | --- | --- | --- |
| D1 | opening | layers except backdrop/over hidden until fonts load (race 1200 real), then fade in 240 out (`BattleScene.js:489-509`, `layout.js:171-173`) | snap (`$UP:335-385`) |
| D2 | chain switch banner | `player.js:1999-2008, 2217` | none |
| D3 | source error → `halted` + `ctx.onError` | `player.js:1945-1953` (+ try/catch at 861, 875, 1677, 1815, 1922) | uncaught |
| D4 | explicit states, `onState`, `onEventEnd`, `eventEnds`, `afterWaiting` | `player.js:360-364, 302-317, 2193-2204` | `_busy` only |
| D5 | dim sheet: only `Inspect ∉ {Accepted, OutOfRange}` while waiting; **whole hand dark** while the flow runs | `player.js:1707-1731` | dims any refusal, lights all while busy (`$UP:2062-2077`) |
| D6 | 600 ms between two enemies' phases | `player.js:865-868, 907-919` | n/a (one enemy) |
| D7 | two-blow / step-back actions kept as one (`heldEnd`/`carried`, 80 ms) | `player.js:146, 322-330, 871-887, 921-949, 1009-1015, 1313-1317, 1451-1454` | returns after every Hit (`$UP:978-984`) |
| D8 | enemy-turn shade from the 敵の番 banner, held to the result (`enemyPhase`) | `player.js:296, 666-670, 955-957, 985-991` | shades when the TurnEnd frame lands (`$UP:766, 1723`) |
| D9 | heartbeat ×3 at HP < 15; Tab focus reset | `player.js:965-974` | none |
| D10 | omen blink after the turn start's draw, 120 apart | `player.js:975-984, 1022-1036` | right after the banner (`$UP:771-774`) |
| D11 | banner look (DS band y 400, 132 tall) | `hud.banner`, `layout.js:49` | 1920×120 black 55 % plate +80 px, 56 px bold (`$UP:1222-1238`); same 25/50/25 linear curve |
| D12 | float path chest→head, 600 ms, under badges, ≥16 px over feet, rides with figure, side label | `player.js:1044-1054`, `$DOC §4.3` | chest+40 rising 96 over 1000 (`$UP:817-818`, `DepictionFx.cs:214-240`) |
| D13 | number sizes 37/48/59/72 | `layout.js:82, 177-180` | 56/72/88/108 (`DepictionFx.cs:17`) |
| D14 | HP number `−` + HP actually lost (kill clamp) | `player.js:1290, 1423-1429, 1473` | bare Amount (`$UP:970, 1020`) |
| D15 | every hit flashes white | `player.js:1303` | violet on Hit (`$UP:971`) |
| D16 | 「Guard −n」 34 px, paired with the HP number 24 px apart | `player.js:52, 1255-1267, 1292-1300, 1337-1345` | 44 px at −60 px (`$UP:952`) |
| D17 | shield 100×114 rim, crack on break | field (`$DOC §6b`) | 150×170 slab with number (`DepictionFx.ShieldBlock`) |
| D18 | TraitFire: stance flies, trait label on the number, badge word; grey MoveBlocked | `player.js:1113-1140, 1386-1421` | one warm line over the head (`$UP:1205-1210`) |
| D19 | ×1.5 脆化/強化 labels | `player.js:127-136, 563-602` | none |
| D20 | wall blows (edge flash, 打 ripple, 「壁」) | `player.js:87-94, 539-548, 1432-1434` | none |
| D21 | 引き寄せ / 押し出し on the moved figure | `player.js:137, 1366-1384` | 押し出し at the player's head (`$UP:890`) |
| D22 | recount to the Tab-chosen enemy | `player.js:1353-1360` | `cue.GapAfter` (`$UP:896-898`) |
| D23 | StanceCue clears the 構えの予告 at once; reserve hint off on the enemy's turn | `player.js:1188, 734-739, 790-795` | until the frame (`$UP:471-476`) |
| D24 | elite's plan promoted (150) on its 2nd action | `player.js:1201-1203` | none |
| D25 | fall with others standing: 800 → foot 300 → grey 300; aim cells retired with the badge | `player.js:1210-1236` | fall only (`$UP:930-940`) |
| D26 | OmenShow: aim cells drawn 30 ms a cell; drop 24 px + fade; `omenDrop` | `player.js:1101-1111` | scale 0.6→1 (`$UP:870-873`) |
| D27 | low-HP vignette 0.25 + pulse 0.5→0.25/200 | `player.js:705-717, 1486` | none |
| D28 | PlayCard sound budget 1; trait word leaves with the card; stance card flies to the stance slot (300 out) and stays; move bump 40 ms; tally | `player.js:1584-1631` | `$UP:2400-2437` |
| D29 | ghost = the card itself shrunk to 76 px, 90 % fading late | `player.js:1530-1540` | ink 55 % 76×104 plate (`$UP:1508-1528`) |
| D30 | wrong-zone text 「「name」は敵の上で離すと出せます」 for single-target | `player.js:85, 1694` | source text (`$UP:2080-2087`) |
| D31 | refused card: `resolving` state; end turn queued until it is home | `player.js:1647-1660` | `$UP:2477-2499` |
| D32 | end turn: plate + X, mash window, open guard, cap, refuse shake | `player.js:1886-1925`, `BattleScene.js:400-404` | plate click only (`$UP:699-706`) |
| D33 | plate flashes once when nothing is playable | `player.js:1733-1748` | none |
| D34 | speed persisted via app settings (`onSettingsChange`) | `player.js:2119-2125` | PlayerPrefs (`$UP:290-291`) |
| D35 | fast-forward (Space / arena click, ×4, floats ≤ ×1.5, one-press to event end, 300 min, 90 ms sound gap) | `BattleScene.js:277-318, 416-451`, `clock.js:49-62` | none |
| D36 | 演出短縮 ×1000 | `clock.js:53` | none (halt only) |
| D37 | pause under journal/settings, hover on real time | `BattleScene.js:186-208`, `clock.js:51, 171` | none |
| D38 | finish sequence (retire omens/aim, hero falls, loss ink, settle, end paper) | `player.js:2010-2070` | 520×220 card (`$UP:738-751, 1241-1275`) |
| D39 | `onFinished` after the card rose + 1200 real (pause-aware); loss waits for 「手記へ」; skip rules | `player.js:2072-2081, 2293-2300` | event at card start + DemoFlow 1.2 s (`DemoFlow.cs:25, 200-202`) |
| D40 | 俊敏's free step | `player.js:1750-1876` | none |
| D41 | several enemies (Cue.Unit, per-unit figures/panels/badges, `TryPlay(…, target)`) | throughout (`unitOf` 274) | one enemy |
| D42 | event log for the pile list's ログ全文 tab (≤ 400 lines) | `player.js:148-168, 460-531` | none |
| D43 | auto play via `ctx.auto.choose / freeStep`, wait 400 **real** | `player.js:2085-2109` | `AutoDrag` with a scaled 400 wait, `SuggestedCardId`, `autoEndTurn` (`$UP:2505-2530`) |
| D44 | Tab at rest moves the floor's N between enemies (`restingGapUnit`) | `player.js:2227-2247, 746-768` | none |
| D45 | Enter twice: confirm, then confirm the auto-selected next card | `BattleScene.js:319-326, 385-394, 459-486` | none |
| D46 | panels follow the walking x during MoveOnFloor | `player.js:799-829` | home x (`$UP:558`) |
| D47 | held preview for a blow = `HpLossFor(k)` (after Guard) | `player.js:2167-2178` | raw `PreviewFor` (`$UP:2257-2260`) |
| D48 | cost / 構えの予告 / HP wick per framed enemy (`CostFor(k)` …) | `player.js:719-768, 2140-2189` | `Face.Cost`, `HpLossFor(0)` (`$UP:2168-2170, 483-487`) |
| D49 | Esc chain: cancel → pinned tip → drawer → journal → free-step focus → (settings on key **release**, opt-in) | `BattleScene.js:358-371, 407-413` | Esc closes the pile list only (`$UP:405-406`) |
| D50 | settings in battle: the game opts in (`main.js:456-457`: `settingsInBattle: true`) → gear + Esc's last step | `BattleScene.js:133-138, 249-264` | n/a |
| D51 | dt capped at 50 ms | `stage.js:112` | uncapped |
| D52 | ease `smooth` (`t²(3−2t)`) exists | `stage.js:85` | UiTween has Linear/In/Out/InOut only |
| D53 | hover/snap weights advance on scene dt (FF/short affect them) | `hand.js:761-779` | `TweenSpeed` × unscaled dt (`$UP:1867, 2328`) |
| D54 | diagnostics | EffectPlan mismatch → `console.error` only | + `EffectTrace`, `EventSeconds`, debug gates/captures (`$UP:1281-1297, 1682-1688, 2540-2557`). Keep the mismatch log (tests assert none); the rest is optional |

Same in both (port as is): `holdBeat`/`effect`/plan semantics, all catalog lengths, the `AfterAutoEventMs 350` / `AfterPlayerEventMs 250` after-waits (`$US/EffectAudit.cs:88-91`), refusal shake rule, draw/discard staggers and rebuild timing, CardRelease 160 In scale 1→0.5 alpha 1→0, self card 140 px up, exile delay 600 − 160, lunge 46 px Out, EnemyMotion shapes, recoil 12/7° ×3 shakes 20/10 px, arena shake 6 px ×3, heave 1.05, panel follow `1 − e^(−14·dt·speed)`.

---

## 2. env, layers, clocks, effect switches, geometry registry in Unity

### 2.1 env (`BattleScene.js:54-98`, `$DOC §2`)

Split the JS `env` into an engine-free core the player sees and a Unity view env the modules see:

```csharp
// Trial.Battle.Flow (engine-free)
public class BattleEnv
{
    public BattleClock Clock;          // scaled scene clock
    public RealClock Real;             // unscaled (input timings)
    public BattleFx Fx;                // EffectSwitches facade
    public BattleGeom Geom;            // registry, filled by the modules
    public SafeAudio Audio;            // Play(name, delayMs = 0, mult = 1), never throws, FF de-dupe
    public BattleSettings Settings;    // { float Speed; bool ShortFx } shared with the app, read live
    public int LayerNo;                // 1..7, clamped
    public NodeKind NodeKind;          // Battle | Elite | Boss
    public float? MiasmaPercent;
    public ILog Log;                   // Error(string), Exception(Exception) → Debug.LogError/LogException in Unity
}
// Trial.Battle (Unity)
public sealed class BattleViewEnv : BattleEnv
{
    public RectTransform Root;         // the 1920×1080 scene root (y-down stage px helpers below)
    public BattleLayers Layer;         // Backdrop, Field, Fx, Panels, Marks, Hud, Shade, Hand, Held, Float, Over
    public Func<Vector2, StagePoint> ToStage;   // screen px → stage px (y down)
    public UiTheme Theme; public UiFonts Fonts; // from the kit
}
```

`OCB` (the DS bundle) has no runtime counterpart; its looks come from the kit (other analysts).

### 2.2 Layers and z-order (`BattleScene.js:26`, `battle.css:28-39`)

| order | layer | CSS z | owner | raycaster | notes |
| --- | --- | --- | --- | --- | --- |
| 0 | Backdrop | 0 | field | no | always visible from the start |
| 1 | Field | 10 | field | no | figures; arena shake / drawer slide transform the figures' parent |
| 2 | Fx | 15 | field (+hand hooks) | no | |
| 3 | Panels | 20 | hud | **yes** | status chips, stance frames, badges (hover/right click) |
| 4 | Marks | 30 | hand / field | **yes** | 俊敏's marks are clickable |
| 5 | Hud | 40 | hud (+hand guide) | **yes** | buttons, piles, end-turn plate |
| 6 | Shade | 50 | field | no | |
| 7 | Hand | 60 | hand | no | hit-tested geometrically by the hand |
| 8 | Held | 70 | hand | no | |
| 9 | Float | 80 | field/hand/hud | no | |
| 10 | Over | 90 | hud/hand/scene | **yes** | tooltips, detail, drawer, result card, journal host |

Recommendation: under the stage root create the 11 layers as **siblings in this order**; each gets `RectTransform` stretched to 1920×1080 (anchor top-left, pivot (0,1)), a nested `Canvas` with `overrideSorting = false` (draws in hierarchy order inside the root canvas, isolates per-frame mesh rebuilds per layer — every layer animates every frame), and a `CanvasGroup` (for the opening fade: `alpha 0` + `blocksRaycasts false` stands for `visibility: hidden`). Add `GraphicRaycaster` only to Panels, Marks, Hud, Over. Every Graphic defaults to `raycastTarget = false`; interactive pieces opt in (the JS `.ocbg-battle-hit` / `pointer-events: auto` rule, `battle.css:41-42`). The root itself: `RectMask2D` (= `overflow: hidden`) and a full-size ground fill (`--ground #10141d`).
The JS `opening` set (`BattleScene.js:495`) = every layer but Backdrop and Over.

Alternative considered: one canvas with sibling order only — rejected for rebuild cost (a moving float or hover tween would dirty the whole canvas each frame). `overrideSorting = true` with explicit sortingOrders — rejected: it couples the battle's orders to the app's other canvases.

### 2.3 Clocks (`clock.js` → `BattleClock.cs`)

```csharp
public enum Ease { Out, In, InOut, Linear, Smooth }      // stage.js:80-86 (cubic; smooth = t*t*(3-2t))
public static class Eases { public static float Apply(Ease e, float t); }

public sealed class BattleClock
{
    public BattleClock(BattleSettings settings, ILog log);
    public double Now { get; }                 // scene time, nominal ms
    public float Scale { get; }                // the factor this frame (read-only)
    public bool FastForward { get; }
    public bool Paused { get; }
    // clock.tween(obj, to, ms, ease, delay): numeric channels of a target; replaces running tweens on the same (target, channel)
    public Beat Tween(ITweenTarget target, TweenTo to, float ms, Ease ease = Ease.Out, float delay = 0f);
    public Beat Run(float ms, Ease ease, Action<float, float> fn);       // fn(eased, raw) each frame, fn(1,1) last; ms 0 → once, done
    public Beat RunPaced(float ms, Ease ease, Action<float, float> fn);  // elapsed advances at pacedScale
    public Beat Wait(float ms);                                          // 0 → Beat.Done
    public Action OnFrame(Action<float, float> fn);                      // fn(dtScene, dtReal); returns unsubscribe
    public void SetFastForward(bool on); public void SetHalted(bool on); public void SetPaused(bool on);
    internal void Step(float dtRealMs);       // called by BattleClocks.Step
    internal void Destroy();
}
public sealed class RealClock
{
    public double Now { get; }                 // ms, uncapped
    public Beat Wait(float ms);                // input timings, refusal 3.5 s, result hold
    public Beat Run(float ms, Ease ease, Action<float> fn);   // addition: the JS modules do `real.wait(16).then(step)` loops for real-time fades (stances.js:221-226, field.js:320-327)
}
public sealed class BattleClocks { public BattleClock Clock; public RealClock Real; public void Step(float dtRealMs); public void Destroy(); }
public static class BattleClockSteps { public static BattleSpeedStep StepOfSpeed(float speed); public static float SpeedOfStep(BattleSpeedStep s); } // clock.js:17-27
public interface ITweenTarget { float Get(int channel); void Set(int channel, float value); }   // tween is only used on card poses (hand.js:480, 965, 986, 1020, 1024, 1049): channels X, Y, Rot, S, A
```

Step order, mirroring the browser (`stage.js:110-128` then `clock.js:65-111`, then microtasks):

1. `Real.Now += dtReal` (uncapped); resolve due real waits (post their continuations).
2. `dt = min(50, dtReal)`; `scale = computeScale()`; `sceneNow += dt × scale`; advance tweens (start values captured on their first active frame, `delay` honoured), runs, timers, paced runs — each list taken whole and items added meanwhile kept (`clock.js:71-106`); collect `done`.
3. Resolve `done` (post), then call every `OnFrame(dtScene, dt)` subscriber (player follow/reach, hand frame loop, hud follow, field motes …).
4. `BeatLoop.Drain()` (all posted continuations, until empty).

After `Destroy()`: every pending `Tween/Run/Wait` never resolves, new ones return `Beat.Never`, lists and subscribers are cleared, and posted continuations belonging to the battle are dropped (the player also checks `_alive` after every await, as JS).

The driver feeds `Time.unscaledDeltaTime × 1000` (not scaled by `Time.timeScale`). Never touch `UiTween.Speed`.

### 2.4 Effect switches (`BattleScene.js:66-80`)

```csharp
public sealed class BattleFx
{
    public BattleFx(EffectSwitches switches);   // EffectSwitches.AllOn() — the scene has no Inspector
    public EffectSwitches Switches { get; }
    public bool On(EffectId id) => Switches.IsOn(id);
    public float Ms(EffectId id) => Switches.Ms(id);
    public float Stagger(EffectId id) => Switches.StaggerMs(id);
    public float Wait(EffectId id, int count = 1) => Switches.WaitMs(id, count);
}
```

The JS uses string ids (`env.fx.ms('CardHover')`); C# uses `EffectId` directly. Reuse `$US/Effects.cs` unchanged. A dev toggle may build `EffectSwitches.WithOff(names, out unknown)` like `$UP:340`.

### 2.5 Geometry registry (`$DOC §2.4`)

Delegate slots filled by the owning module in its constructor and read lazily by the others (null slot → default answer, as `env.geom.x ? … : []` in JS). All values in stage px, y down.

```csharp
public readonly struct StagePoint { public readonly float X, Y; }
public readonly struct StageBox { public readonly float X, Y, W, H; }
public sealed class FigureGeom
{   // field.js:1014-1026
    public float X, FeetY, Height, Width, HomeX, Top, ArtX0, ArtX1;
    public StagePoint Chest, Head; public int Unit; public bool Down, Walking;
}
public sealed class BattleGeom
{
    // field
    public Func<UnitSide, int, FigureGeom> Figure;          // (side, unit = -1)
    public Func<int, ScreenBox?> Receiver;                  // 340×340 over an enemy's home (ScreenBox from $US/CornerLayout.cs:11)
    public Func<IReadOnlyList<(int unit, ScreenBox box)>> Receivers;
    public Func<int, StageBox?> CellBox;
    public Func<IReadOnlyList<(StageBox box, int dir)>> FreeStepBoxes;
    // hud
    public Func<PileKind, StagePoint> PileMiddle;
    public Func<int, StageBox?> OmenBox;                    // (unit = -1)
    public Func<IReadOnlyList<StageBox>> OmenBoxes;
    public Func<int, bool> OmenShown;
    public Func<StageBox> EndTurnBox;
    public Func<UnitSide, int, StageBox?> StatusBox;        // computed from StatusPanelLayout, never from the hierarchy (read every frame)
    public Func<IReadOnlyList<(UnitSide side, int unit, StageBox box)>> StatusBoxes;
    public Func<UnitSide, StageBox?> StanceBox;
    public Func<UnitSide, StageBox?> StanceSlot;
    public Func<StagePoint, bool> CoversPointer;
    public Func<float> DrawerRight;
    // hand
    public Func<string, StagePoint?> HandCard;
    // scene
    public Func<StagePoint, bool> JournalCovers;            // x ≥ 1920 × 0.7 while the journal is open
    public Func<float?> JournalEdge;                        // 1344 while open
}
```

Unit convention: JS `undefined` (one enemy / "the leader") ↔ C# `-1` (`BattleUnits.None`); JS `unitOf(x)` (`player.js:274`) returns `undefined` for `Unit < 0`. Keep that mapping in one helper; never pass `0` where JS passes `undefined` (several places branch on it: `isHeld` 930, `list` 995, `targetFor` 1663).

### 2.6 Stage px placement

Use one helper in the kit: `StagePx.Place(RectTransform rt, float x, float y, float w, float h)` → anchors (0,1), pivot as needed, `anchoredPosition = (x, -y)`. All JS geometry, layout numbers and `env.geom` values stay y-down. The `$DOC §0` note "Unity's fx offsets are y-up" then only matters when reading `$UP`/`DepictionFx.cs` numbers.

---

## 3. Scene-level keys and pointer → Input System / EventSystem

### 3.1 Per-frame dispatch order (one `BattleSceneDriver : MonoBehaviour` on the root, `[DefaultExecutionOrder(100)]` so it runs after the `EventSystem`)

1. `BeatLoop.Drain()` (continuations of hud button clicks the EventSystem dispatched this frame).
2. Keyboard: build key events from `Keyboard.current` (`wasPressedThisFrame` / `wasReleasedThisFrame` / `isPressed`) and call the scene key handler (port of the `pushKeys` closure, `BattleScene.js:344-406`) and `OnKeyUp` (407-413); drain after each.
3. Pointer: read `Mouse.current` (fallback `Pointer.current`): position → stage px; then, in browser order, **down**: hud pin handler (capture phase, `hud.js:461`) → scene `OnPointerDown` (root, `BattleScene.js:436-447`) → hand `OnDown` (`hand.js:604-642`); **move** (if moved): hand `OnMove`, hud `OnPointerMove`; **up**: hand `OnUp` (683-728) → scene `OnPointerUp` (448-451); **right button** → hand `OnContext` (740-747), hud `OnPinContext`; drain after each.
4. `clocks.Step(Time.unscaledDeltaTime × 1000)` (includes OnFrame and a final drain).

`OnApplicationFocus(false)` → `onBlur` of the scene (`clearFF`, `escToSettings = false`, 453) and of the hand (`dropLostHold`, 658-664); `OnApplicationPause(true)` → the same (the `visibilitychange` path). `OnDestroy` → `Destroy()`.

A press and a release in the same frame: handle the press first, then the release (both flags can be true). Multiple clicks in one frame are collapsed by polling — acceptable (the JS mash rules already absorb rapid presses).

### 3.2 Keys (`BattleScene.js:331-413`, `$DOC §4.5`)

`sceneKey` filter: ignore when Ctrl/Alt/Meta(Win/Cmd) is held. JS reads `e.key`, so Shift+digit (`!`…) is not a scene key while Shift+letter still is: ignore digits with Shift held, keep letters.

| JS key | Input System | in `idle` | in `auto`/`resolving` | repeat |
| --- | --- | --- | --- | --- |
| `1`–`9` | `digit1Key…digit9Key`, `numpad1Key…numpad9Key` | `autoSelect = null; hand.Select(i-1)` | nothing reaches the source (`hand.Select` refuses when not interactable) | ignored |
| Enter | `enterKey`, `numpadEnterKey` | free step pending → `ConfirmFreeStep()`; else `hand.Confirm()` and remember `autoSelect {index, turn, plays}` | `autoSelect.confirm = true` (the 2nd Enter of a pair) | swallowed |
| Tab / Shift+Tab | `tabKey` + `shiftKey.isPressed` | `hand.CycleTarget(±1)` else (nothing held/selected) `player.CycleGapFocus(±1)` | – | swallowed |
| I | `iKey` | `hand.ToggleSelectedDetail()` | – | ignored |
| J | `jKey` | toggle journal | toggle journal | ignored |
| X | `xKey` | `player.RequestEndTurn({queue, press})` | same (swallowed by the mash window) | ignored |
| Esc | `escapeKey` press; **release** for settings | `hand.Cancel()` → `hud.ClosePinned()` → `hud.CloseDrawer()` → close journal → free step: `StayFreeStepFocus()` → else arm `escToSettings`; on release, with `ctx.SettingsInBattle` (the game passes true) → `OpenSettings()` | same order | ignored |
| Space | `spaceKey` press → `ArmFF()`; `isPressed` → `ffHeld` | – | FF (one press / held) | press only |
| ← / → | `leftArrowKey` / `rightArrowKey` | only while 俊敏's choice waits: `MoveFreeStepFocus(∓1)` | – | allowed (idempotent) |
| over state | Enter / Space / Esc fresh | `skipResult()`; Esc first closes an open drawer | | |

Any scene key arriving means no modal is up → `settingsClosed()` (346-347). While the journal is open, only J and Esc are the scene's (373).

### 3.3 Pointer (`BattleScene.js:416-458`, `hand.js:578-755`, `$DOC §4.5, §6a`)

| JS | Unity |
| --- | --- |
| `env.toStage(e)` | `RectTransformUtility.ScreenPointToLocalPointInRectangle(stageRoot, screen, cam, out p)` → `(p.x, -p.y)` with the stage pivot top-left |
| `e.target` inside `layer.hud/over/panels` (`isControl` 422-427, `isForeign` 587-593) | `EventSystem.current.RaycastAll(pointerEventData)`; take the first result; its layer = the layer root it `IsChildOf`; a `BattleHitTag` component may refine (`EndTurnPlate` counts as arena; `JournalVeil` outside the drawer is not foreign) |
| root `pointerdown` (FF arm, result skip, `settingsClosed`) | `leftButton.wasPressedThisFrame` (any button for `skipResult`/`settingsClosed`; FF only on the left button and not on a control) |
| double click guard: a press within `L.DOUBLE_CLICK_MS 500` real of a press/release made while asked does not FF (434-447) | same, on `Real.Now` |
| `window pointerup` | `leftButton.wasReleasedThisFrame` |
| `pointermove` with `buttons === 0` while holding (lost release) → `dropLostHold` (668-670) | `!leftButton.isPressed` while the hand holds or has a press under way |
| `contextmenu` (Windows browsers fire it on the right-button **release**) | `rightButton.wasReleasedThisFrame` (the JS feel on Windows; `$UP:1938-1946` used the press — either works, release matches the approved build) |
| `pointerleave` root | pointer outside the stage rect / game view (`$UP:2027-2039` pattern) |
| `setPointerCapture` | not needed (polling is global) |
| hover | geometric, every frame from the pointer position (the hand's `cardUnder`), not `IPointerEnterHandler`; hud tooltips may use `IPointerEnterHandler/ExitHandler` |
| hud buttons (`click`) | `IPointerClickHandler` on raycast-target Graphics in Hud/Over/Panels/Marks; runs inside `EventSystem.Update` before the driver (no flow depends on the browser's click-after-up order: verified for the plate, piles, speed, journal, gear, result button, free-step marks) |

Thresholds (all `Real`, unscaled): drag `L.DRAG_THRESHOLD = 8` **stage px** (`Math.hypot` on stage coordinates, so 5.3 screen px at 1280×720) — do not use `EventSystem.pixelDragThreshold` and do not use `IDragHandler` (the hand's own state machine decides, as `hand.js:666-694`); long press `L.LONG_PRESS_MS = 400` real, then the card returns over `LONG_PRESS_RETURN_MS = 200` on the **scene** clock before the detail opens (`hand.js:50, 626-638`); refusal line 3.5 s real; result skip 400 / 350 real; double click 500 real.

`EnsureEventSystem` with `InputSystemUIInputModule` as `$UP:2559-2567`.

---

## 4. The C# design

### 4.1 Files and assemblies

```
Assets/View/Trial/Battle/Flow/            asmdef Trial.Battle.Flow   (noEngineReferences: true; refs Depiction.Script, Depiction.Bridge, BattleCore)
  Beat.cs            Beat, BeatSource, BeatLoop, BeatAwaiter, BeatMethodBuilder           (new; JS Promise)
  BattleClock.cs     port of clock.js (+ Ease, Eases, RealClock, BattleClocks, StepOfSpeed/SpeedOfStep)
  BattleLayout.cs    port of layout.js: static class L (constants) + BattleLayout.NumberFont/HatchAngle/EnemyNameOf/HatchGlyph
  BattleEnv.cs       BattleEnv, BattleFx, BattleGeom, geometry structs, SafeAudio, BattleSettings, BattleCtx, ILog
  BattleContracts.cs IHand, IField, IHud (+ IStatusPanel, IOmenBadge, IOmenFacade, IHudStamina, IHudEndTurn) and option/info types
  BattlePlayer.cs    port of player.js (one file; JS line refs kept in comments)
Assets/View/Trial/Battle/                 asmdef Trial.Battle         (Unity; refs Trial.Battle.Flow, Unity.InputSystem, Unity.TextMeshPro, kit)
  BattleScene.cs     port of BattleScene.js: MountBattleScene(host, ctx) → BattleSceneHandle; BattleSceneDriver; BattleLayers; overlays; keys; pointer
  (BattleHand.cs / BattleField.cs / BattleHud.cs + their private files: other analysts)
unity-port/Trial.Battle.Flow/*.csproj + Trial.Battle.Flow.Tests/   (netstandard2.1, C# 9, like Depiction.Script.csproj)
```

Naming: functions keep the JS names in PascalCase (`runAutomaticEvents` → `RunAutomaticEvents`); `L` constants convert mechanically (`FEET_Y` → `L.FeetY`, `OMEN.overHead` → `L.Omen.OverHead`, `END_TURN_MASH_MS` → `L.EndTurnMashMs`), with the JS name in a comment for grep.

### 4.2 The async pattern: `Beat` (justification and shape)

Requirements taken from the JS: (a) `await` inside long sequential flows with early `return` on `!alive`; (b) fire-and-forget beats beside the flow; (c) fan-out — one beat awaited by several (`flying` in `drawHand` 1517-1519, `rel.done`), `Promise.all` (`finish` 2051), `Promise.race` (2076); (d) never-resolving promises after destroy; (e) deterministic ordering: continuations run after the clock's frame step and its OnFrame callbacks, and after each input handler (browser microtasks); (f) testable without the Editor.

Options rejected:
- **Unity coroutines** (`$UP`'s pattern): need a MonoBehaviour, cannot return values, no multi-await/All/Race, timing tied to Unity's frame and the static `UiTween.Speed`, `StopAllCoroutines` granularity, PlayMode-only tests.
- **`Task` / `async Task`**: Unity's `SynchronizationContext` posts every continuation to the next frame (+1 frame per `await`: a 30-await event drifts ~0.5 s and shifts every beat relative to the plan); allocations; unobserved exceptions.
- **Unity 6 `Awaitable`**: pooled, **may be awaited only once** (breaks fan-out), engine-bound (no `dotnet test`), resumes on Unity's schedule, not our scaled clock.

Chosen: a ~250-line task-like type, the same technique UniTask uses (custom `AsyncMethodBuilder`, works under IL2CPP), with no third-party package:

```csharp
namespace Trial.Battle
{
    [AsyncMethodBuilder(typeof(BeatMethodBuilder))]
    public sealed class Beat : System.Collections.IEnumerator   // IEnumerator only so a coroutine could `yield return` it
    {
        public static Beat Done { get; }          // resolved (Promise.resolve())
        public static Beat Never();               // never resolves (a destroyed clock's answer)
        public bool IsDone { get; }
        public Exception Fault { get; }           // a rejected promise; `await` rethrows it
        public Beat Then(Action next);            // .then(() => …): resolves after next ran
        public Beat Then(Func<Beat> next);        // .then(() => promise)
        public static Beat All(params Beat[] beats);   // Promise.all
        public static Beat Any(params Beat[] beats);   // Promise.race
        public BeatAwaiter GetAwaiter();
    }
    public sealed class BeatSource { public Beat Beat { get; } public void Resolve(); public void Fail(Exception e); } // new Promise(res => …)
    public struct BeatAwaiter : INotifyCompletion
    {
        public bool IsCompleted => false;         // always posts, like JS: `await` of a settled promise still yields to the microtask queue
        public void OnCompleted(Action continuation);   // queued on BeatLoop when the Beat settles
        public void GetResult();                  // rethrows Fault
    }
    public static class BeatLoop                  // the microtask queue (main thread only)
    {
        public static void Post(Action a);
        public static void Drain();               // runs until empty; called by the clock step and after every input handler
        public static Action<Exception> Unhandled;  // a faulted `async Beat` nobody awaited → ILog
    }
    public struct BeatMethodBuilder { /* Create, Start, Task, SetResult, SetException, AwaitOnCompleted, AwaitUnsafeOnCompleted, SetStateMachine */ }
}
```

`Beat<T>` is not needed: no awaited JS function in this area returns a value (`hand.release` returns an object synchronously with a `Done` beat inside).

The port then reads like the JS:

```csharp
// player.js:645-662
private async Beat HoldBeat(EffectId id, Func<Beat> play, Action settle, int count = 1, Action played = null)
{
    EffectStep step = TakePlanned(id, count);
    if (!_switches.IsOn(id)) { settle?.Invoke(); played?.Invoke(); return; }
    float waitMs = EffectPlan.WaitMs(step, _switches);
    if (step.LeadMs.HasValue)
    {
        Safe(play).Then(() => { if (_alive) played?.Invoke(); });
        await _clock.Wait(waitMs);
        return;
    }
    Beat waited = _clock.Wait(waitMs);
    await Safe(play);
    await waited;
    played?.Invoke();
}
// player.js:398-406: Safe(play) catches a synchronous throw and a faulted beat, logs, returns a settled Beat.
```

Thunks stay thunks (`Func<Beat>`), so `play` starts only when `HoldBeat` decides, as in JS (`$DOC §4.2`: "`play` is a **thunk**").

### 4.3 `BattleScene.cs` (port of `BattleScene.js`)

```csharp
public static class BattleScene
{
    public const float JournalShare = 0.3f;                    // BattleScene.js:23
    public static readonly string[] Layers = { "backdrop", "field", "fx", "panels", "marks", "hud", "shade", "hand", "held", "float", "over" };
    public static (RectTransform root, BattleLayers layer, BattleViewEnv env, BattleClocks clocks) CreateBattleEnv(RectTransform host, BattleCtx ctx); // module dev scenes reuse it
    public static BattleSceneHandle MountBattleScene(RectTransform host, BattleCtx ctx);
}
public sealed class BattleSceneHandle
{
    public RectTransform Root; public BattleViewEnv Env; public BattlePlayer Player; public (IHand Hand, IField Field, IHud Hud) Modules;
    public void Refresh();          // the app closed its settings overlay (speed may have changed)
    public void Pause(bool on);     // the app covers the scene
    public void Destroy();
}
public sealed class BattleCtx       // engine-free (host types as object where the app owns them)
{
    public ITrialSource Source;  public IBattleAudio Audio;  public BattleSettings Settings;
    public int Layer; public string LayerTitle; public NodeKind NodeKind; public (int Index, int Total)? Chain;
    public float? MiasmaPercent; public Func<string, int> DisclosureOf;
    public Func<object, JournalOpenOpts, IDisposable> OpenJournal;   // (host RectTransform, { EnemyIds, DisclosureOf, OnClose })
    public Action OnSettings; public bool SettingsInBattle; public Action<BattleSettings> OnSettingsChange;
    public Action<Exception> OnError; public Action OnFinished; public IBattleAuto Auto;   // dev / tests
}
public interface IBattleAuto { CardChoice Choose(ITrialSource source, int playedThisTurn); int FreeStep(ITrialSource source); } // CardChoice { string Id; int Unit = -1 }
```

Inside `MountBattleScene` keep the JS closures as private methods of a `BattleSceneState` object: `SettingsInBattle`, `Late` (forwarding hand callbacks to the player made last), `OverlayUp`, `SyncPause`, `UpdateShift`, `CloseJournal`, `ToggleJournal`, `OpenSettings`, `SettingsClosed`, FF (`UpdateFF`, `ArmFF`, `ClearFF`, `ffHeld`, `pointerFF`, `ffArmed`, `ffUntil`, `ffArmedAt`), auto-select (`autoSelect`, `TurnNow`), `SceneKey`, the key handler, `OnKeyUp`, `IsControl`, `OnPointerDown/Up`, `OnBlur`, `OnVisibility`, the opening (`go()` with a font prewarm instead of `fontsFor`, raced with `Real.Wait(L.OpenFontWaitMs 1200)`, then `player.Start()`, layers visible, `Clock.Run(240, Out, alpha)`), and `Destroy` in the JS order (`BattleScene.js:530-548`). The auto-select's `Promise.resolve().then(…)` (467) becomes `BeatLoop.Post(…)`.

### 4.4 `BattlePlayer.cs` (port of `player.js`)

```csharp
public sealed class BattlePlayer
{
    public BattlePlayer(BattleEnv env, IHand hand, IField field, IHud hud, BattleCtx ctx);   // createPlayer(env, view, ctx)
    public BattlePlayerState State { get; }       // Opening | Auto | Idle | Resolving | Over | Halted
    public bool Busy { get; }
    public Action OnState(Action<BattlePlayerState> fn);   // returns unsubscribe
    public Action OnEventEnd(Action fn);
    public int EventEnds { get; } public bool AfterWaiting { get; }
    public IReadOnlyList<string> LogLines();
    public HandCallbacks HandCallbacks { get; }   // OnRelease(id, zone, unit), OnHover(face), OnHoldStart(face, unit), OnHoldEnd(face), Preview(id, unit) → string, OnTarget(face, unit)
    public Beat Start();
    public bool RequestEndTurn(EndTurnRequest o = default);   // { Queue, Press }
    public bool CanEndTurn();
    public bool Returning { get; }
    public bool CycleGapFocus(int dir);
    public int RestingUnit { get; }
    public void CycleSpeed(); public void ShowSpeed();
    public IReadOnlyList<string> EnemyIds();
    public bool IsPlayable(string id);
    public int AcceptedPlays { get; }
    public bool CanFastForward();
    public void SetOverlayCheck(Func<bool> fn); public void SetDrawerCheck(Func<bool> fn); public void SetJournalCheck(Func<bool> fn);
    public void RefuseWhileDrawer(); public void RefuseWhileJournal();
    public void OverlaysChanged(bool drawer, bool journal);
    public void SkipResult();
    public void Halt();
    public void RefreshPlayable();
    public bool FreeStepPending { get; } public FreeStepWays? FreeStepWaysNow { get; }
    public bool PickFreeStep(int dir); public void FocusFreeStep(int dir); public bool MoveFreeStepFocus(int key);
    public bool StayFreeStepFocus(); public bool ConfirmFreeStep();
    public int PlanErrors { get; }                // count of EffectPlan mismatches (tests assert 0)
    public void Destroy();
}
```

Private members port 1:1 (`SetHandInteractable`, `Sfx`, `SfxBudget`, `EssentialSoundsOf`, `Safe`, `Effect`, `FloatEffect`, `BadgeBoxes`, `TrackOf`, `BeginPlan`, `LogEvent`, `LogWho`, `StanceCount`, `WallOf`, `TakeSide`, `MultLabelsOf`, `FlushSide`, `EndPlan`, `TakePlanned`, `HoldBeat`, `EnemyTurnOf`, `ApplyFrame`, `StatusMarksOf`, `ShowLowHp`, `PulseLowHp`, `HeldSlot`, `ShowHeldCost`, `ShowReserveHint`, `ShowHpLoss`, `LiftOmenForHeld`, `UpdateFloorReach`, `ShowEnemyTurn`, `FollowFigures`, `ApplyStanceGlow`, `LightStanceGlowWithCrest`, `RunAutomaticEvents`, `StartsOtherEnemy`, `ContinuesHeld`, `IsHeld`, `PlayHeldEnd`, `PlayEvent`, `BlinkStanding`, `PlayCue`, `HpBesideTag`, `RecountTo`, `PushedFloat`, `MovedWord`, `FireTrait`, `TraitOf`, `HpLostOf`, `WallBeat`, `Attack`, `DrainHp`, `PlayerSize`, `SlideDiscardOntoDraw`, `DrawHand`, `DiscardHand`, `GhostToPile`, `ReceiverCentre`, `StuckMoveOf`, `CutShortOf`, `PlayAccepted`, `Released`, `ReturnToHand`, `TargetFor`, `OnRelease`, `ShowRefusal`, `FlashIfNothingPlayable`, `OpenFreeStep`, `CloseFreeStep`, `RefuseWhileChoosing`, `ShowCauseRefusal`, `ClearRefusal`, `PlayFreeStep`, `PlayThenContinue`, `SourceFailed`, `HoldUnpaused`, `ResultInfo`, `ChainOpening`, `Finish`, `ScheduleAuto`). Scene texts (`FREE_STEP_*`, `DRAWER_GUIDE`, `JOURNAL_GUIDE`, `wrongZoneOnEnemy`, `WALL_*`, `CONDITION_NAMES`, `NAMED_EFFECTS`, `MULT_LABEL`, `PULLED_WORD`, `MINUS '−'`, `LOG`, `RESULT_TEXT`, `RESULT_BARS`, `LOSS`, `LOSS_WORD`, `HIT_SOUND`, `CONTINUE_PAUSE_MS 80`, `CHAIN_APPEAR_MS 400`, `LOG_MAX 400`, `FLOAT_UNDER_BADGE 8`, `FLOAT_OVER_FEET 16`, `HP_BESIDE_GUARD_GAP 24`, `STAGE_EDGE 16`) become `private const`/`static readonly` at the top with `// scene text` marks. Regexes (`/^構え/`, `^Guard\+`, trait kinds 1414) port to `System.Text.RegularExpressions` or `StartsWith`.

Every state-changing call goes to `_ctx.Source` each time (never a cached reference) so the save recorder that wraps it sees every call (`player.js:7-9`).

### 4.5 Module contracts (every method of `$DOC §6` plus the additions the player/scene call)

Common types: `StagePoint`, `StageBox`, `FigureGeom` (§2.5); `int unit = -1` = JS `undefined`; `FxRole { Ink, Ink2, Omen, Guard, Warm, Heal, Whiff, White }`; `BannerSide { Player, Enemy }`; `GhostKind { Discard, Exile, Sheet }`; `object` stands for a view handle the engine-free side only passes through (`GhostCard` → `Ghost`, journal host). `ms = 0` means "put the end state at once and resolve" for every animation method (`$DOC §5`).

```csharp
public sealed class HandOpts
{
    public Action<string, DropZone, int> OnRelease;  public Action<CardFace> OnHover;
    public Action<CardFace, int> OnHoldStart;        public Action<CardFace> OnHoldEnd;
    public Action<CardFace, int> OnTarget;           public Func<string, int, string> Preview;
    public Func<int> RestingUnit;
}
public interface IHand
{
    // frame
    bool SameHand(IReadOnlyList<CardFace> faces);
    void Build(IReadOnlyList<CardFace> faces);
    void Bind(IReadOnlyList<CardFace> faces);
    void Layout();
    void Clear();
    int Count();
    IReadOnlyList<CardFace> Faces();
    // state set by the player
    void SetInteractable(bool on);
    void RefreshPlayable(Func<string, bool> dimOf, string guide);
    void SetStatusMarks(StatusMarks marks);              // { Focus, Empower, Intimidate }
    bool IsHolding();
    CardFace HeldFace();                                 // held, else selected, else null
    CardFace HoveredFace();
    bool HasSelection();
    int SelectedIndex();
    bool DetailOpen();
    int FramedUnit();                                    // −1 for none
    IReadOnlyList<int> TaggedUnits();
    HandDebugState DebugState();
    // beats
    Beat FlyDrawn(int first, int rebuildAfter, FlyDrawnOpts o);    // { StagePoint From; Func<Beat> OnRebuild }
    Beat DiscardAll(StagePoint to);
    ReleaseResult Release(string cardId, DepictionEvent ev, StagePoint? to, ReleaseOpts o);  // o.TraitWord
    object GhostCard(CardFace face);                     // a fresh card view for the played card's ghost (field.Ghost's Card)
    void DropCard(string cardId);
    Beat Settle(float ms);
    Beat ReturnToHand(string cardId, bool shake);
    void LetGo();
    void ShowRefusal(string text);
    void HideRefusal();
    Beat FadeAll(float ms);
    // input paths
    bool Select(int index, SelectOpts o = default);      // o.Auto: the scene's own pick after an Enter play
    bool Confirm();
    bool Cancel();
    bool CycleTarget(int dir);
    void ToggleSelectedDetail();
    Beat AutoDrag(string cardId, DropZone zone, int unit = -1);
    void SetBlocked(bool on, bool hover, Action onTry);
    void SetGrabLock(Action onTry);                      // null clears
    // raw input routed by the scene (§3.1)
    void OnPointerDown(BattlePointer p); void OnPointerMove(BattlePointer p); void OnPointerUp(BattlePointer p);
    void OnPointerCancel(); void OnContext(BattlePointer p); void OnLeave(); void OnBlur();
    void Destroy();
}
public sealed class ReleaseResult { public Beat Done; public Func<StagePoint> VanishedAt; public CardFace Face; public bool Keep; }

public sealed class FieldOpts { public int Layer; public NodeKind NodeKind; public Action<int> OnFreeStepPick; public Action<int> OnFreeStepFocus; }
public interface IField
{
    // frame
    void ApplyFrame(DepictionFrame frame, bool first);
    void SetExtras(IReadOnlyList<object> list);          // kept for the contract; draws nothing
    void ShowGapTo(int unit);                            // −1: the frame's own
    IReadOnlyList<int> EnemyUnits();
    void ShowReach(IReadOnlyList<int> cells); void HideReach();
    Beat DrawAim(DepictionFrame frame, int unit, bool now = false);
    void FocusAim(int unit);
    Beat RetireAim(float ms); void RetireAimOf(int unit);
    void SetEnemyTurnShade(bool on);
    void SetShift(int dir); float ShiftX { get; }
    float NudgeOf(UnitSide side, int unit = -1);
    void SetLowHp(bool on); Beat PulseLowHp(float ms);
    Beat AppearEnemies(float ms, float from = 0f);
    // figure state
    void SetPose(UnitSide side, FigurePose pose, int unit = -1);
    void SetDown(UnitSide side, bool down, int unit = -1);
    bool EnemyAway(int unit = -1);
    void SetEnemyHomeCell(int cell, int size, int unit = -1);
    void SnapHome(UnitSide side, int unit = -1);
    bool StanceGlowShown(UnitSide side, int unit = -1);
    void SetStanceGlow(UnitSide side, bool on, int unit = -1);
    Beat FadeStanceGlow(UnitSide side, bool on, float ms, int unit = -1);
    // beats
    Beat Lunge(UnitSide side, float px, float ms, int unit = -1);
    Beat ReturnHome(UnitSide side, float ms, int unit = -1);
    Beat EnemyMotion(StrikeSystem system, float ms, int unit = -1);
    Beat Recoil(UnitSide side, bool strong, float ms, int unit = -1);
    Beat Flash(UnitSide side, FxRole role, float ms, int unit = -1);
    Beat Heave(UnitSide side, float ms, int unit = -1);
    Beat Fall(UnitSide side, float ms, int unit = -1, bool keepFoot = false);
    Beat FadeFoot(UnitSide side, float ms, int unit = -1);
    Beat DimFeet(float ms);
    Beat Bump(UnitSide side, int dir, float ms, int unit = -1);
    Beat ShakeArena(float ms);
    Beat MoveOnFloor(Cue cue, int cells, float ms);
    void SnapOnFloor(Cue cue);
    Beat RecountGap(int gap, int playerCell, int playerSize, int enemyCell, float ms);
    void SetGap(int gap, int playerCell, int playerSize, int enemyCell);
    // 俊敏
    void ShowFreeStep(int playerCell, FreeStepWays ways);
    int? FocusFreeStep(int dir);  Beat HideFreeStep(float ms);
    bool FreeStepShown { get; }   int? FreeStepFocus { get; }
    IReadOnlyList<int> FreeStepDirs();  StagePoint? FreeStepCentre(int dir);
    // fx
    Beat Strike(UnitSide side, StrikeOpts o);            // { ByPlayer, System, Intensity, Ms, Unit, Source }
    Beat WallHit(UnitSide side, int dir, float ms, int unit = -1);
    Beat TraitAccent(UnitSide side, float ms, int unit = -1);
    Beat Mark(UnitSide side, MarkOpts o);                // { Fx, Intensity, Ms, CrestLight, Unit }
    void Burst(StagePoint at, FxRole role, float size);
    Beat FloatText(StagePoint at, string text, FxRole role, float fontSize, FloatOpts o = null);
    Beat ShieldBlock(UnitSide side, string amountText, float ms, int unit = -1, bool broken = false);
    StagePoint? ShieldAt(UnitSide side, int unit = -1);
    Beat Ghost(StagePoint from, StagePoint to, GhostOpts o);   // { Kind, Ms, Ease, object Card }
    void Destroy();
}
public sealed class FloatOpts
{
    public float? Rise; public bool Struck; public bool Plain; public string Side;
    public Func<float?> Track; public Func<IReadOnlyList<StageBox>> Avoid; public float AvoidGap; public float? Lowest;
    public string Anchor;                                // "left" | "right" | null
    public Func<float?> FollowY; public float FollowRise;
    public Action<FloatSpan> OnPlaced;                   // { X0, X1, Y, Rise, Func<float?> YNow }
}

public sealed class HudOpts
{
    public (int Index, int Total) Chain; public NodeKind NodeKind;
    public Action OnEndTurn, OnSpeed, OnJournal, OnSettings; public bool SettingsInBattle;
    public Action<bool> OnDrawer; public Action<int> OnOmenHover;
}
public interface IHud
{
    void ApplyFrame(DepictionFrame frame, bool first, int keepOmen = -1);
    void Follow(FollowArgs a);       // { PlayerX, PlayerWalking, EnemyX, EnemyTop, Cells, EnemySize, Units[{Unit, X, Top, Size, Down, Walking}], Snap }
    IStatusPanel Status(UnitSide side, int unit = -1);
    IOmenFacade Omen { get; }        // hud.omen: Bind / PopIn / Blink / Flare / SetActing(false = every badge) / …
    IOmenBadge OmenOf(int unit = -1);
    void SyncOmenLook(DepictionFrame frame, int unit);
    IReadOnlyList<int> EnemyUnits();
    IHudStamina Stamina { get; }     // ShowCost(int n); Beat GlowReserve(float ms)
    void ReserveHint(string text);
    void SetEnemyTurn(bool on);
    IHudEndTurn EndTurn { get; }     // Beat Pop(float ms); Beat Flash(float ms); Beat Refuse(float ms)
    Beat Banner(string text, BannerSide side, float ms);
    Beat ResultCard(BattleOutcome outcome, float ms, ResultInfo info, Action onButton);
    void ResultCardNow(BattleOutcome outcome, ResultInfo info, Action onButton);
    Beat ChainBanner(string title, string name, float scale, Action onOut);
    Beat LossInk(string word, float inkMs, float wordMs);
    Beat LossWordOut(float ms);
    Beat SettleEnd(float ms, BattleOutcome outcome); void SettleEndNow(BattleOutcome outcome);
    Beat RetireOmens(float ms);
    void LiftOmen(IReadOnlyList<int> units);
    void SetRestFocus(int unit);     // −1 clears
    void SetLog(IReadOnlyList<string> lines);
    Beat FlyOmenToStances(int unit, float ms);
    void Speed(string label, int chevrons);
    void SetSpeedVisible(bool on);
    bool ClosePinned();
    bool DrawerOpen();
    void CloseDrawer();
    void OnPinDown(BattlePointer p); void OnPinContext(BattlePointer p); void OnPointerMove(BattlePointer p);  // routed by the scene
    void Destroy();
}
public interface IStatusPanel
{
    int Hp { get; } bool Shown { get; } StageBox Box();
    void SetGuard(int n); Beat PopGuard(int n, float ms = 260f); Beat CrackGuard(float ms);
    void SetHp(int hp); Beat AnimateHp(int hp, float ms); Beat AnimateTrail(float ms); void SnapTrail(); void SetHpKeepingTrail(int hp);
    void ShowHpLoss(int n);
    void SetStatus(string label, int stacks); Beat PlayStatus(string label, int stacks, EffectId beat, float ms);
    void SetStamina(int stamina, int max); Beat LightPips(int stamina, int max, float perPipMs); Beat PopPips(float ms);
    Beat GreyOut(float ms); void SetDown(bool on); void SetFocus(bool on);
}
public interface IOmenBadge
{
    void Bind(OmenFrame o); Beat PopIn(float ms); Beat Blink(float ms); Beat Flare(float ms); void SetActing(bool on);
    Beat FadeOut(float ms); void HideNow(); Beat FadeOutAll(float ms); void HideAllNow();
    Beat StrikeSide(float ms); void StrikeSideNow(); Beat PromotePlan(float ms); Beat TraitWord(string word, float ms);
    StageBox Box(); bool Visible { get; }
    void SetSwatchShown(bool on, bool aimed); void SetTail(float dx); void SetBehind(bool on); void SetMoveDir(int dir);
}
public interface IOmenFacade { void Bind(OmenFrame o); Beat PopIn(float ms); Beat Blink(float ms); Beat Flare(float ms); void SetActing(bool on); }
public sealed class ResultInfo { public string Title; public IReadOnlyList<string> Lines; public string BarsLabel; public IReadOnlyList<ResultBar> Bars; public IReadOnlyList<string> Stats; public string Button; }
public readonly struct ResultBar { public readonly string Label; public readonly UiColorRole Color; public readonly int Value; }
```

The modules never call `ctx.Source`, never call each other, and expose nothing else to each other than `env.Geom` and the callbacks (`$DOC §1, §5`).

### 4.6 Tests (`dotnet test`, engine-free)

- `BeatTests`: ordering (continuations after OnFrame; `await Beat.Done` still yields), All/Any, faults, `Never`, destroy drops queued work.
- `BattleClockTests`: scale table (speed × FF × short × pause), `RunPaced` cap, 50 ms dt cap, tween replacement by channel, run/wait created inside a callback survive (`clock.js:71-106`, review 19).
- `BattlePlayerFlowTests` with fake `IHand/IField/IHud` whose beats resolve after the given ms on the clock, driving a real `CoreBattleSource` (one enemy) with the greedy chooser: zero `PlanErrors`; each event's scene-time length = `EffectPlan.BlockingMs` + after-waits; two-blow actions take one lunge and one return; mash window table (presses every 0.1 / 0.4 / 0.65-0.8 / 0.7-1.4 s from the reviews); one-press FF ends at the event end (+300 ms rule); result hold 1200 unpaused; loss waits for the button.
- Later, the JS oracle replays (`dev/battle-replays/replays.json`) can feed the same tests.

---

## 5. Reuse and bridge additions

### 5.1 Reused as is (no change)

`$US`: `BattleSpeed` (Multiplier/TweenSpeed/Next/Label/Chevrons/Tooltip/Save/Load), `Effects` (EffectId, EffectSpec, EffectCatalog, EffectSwitches, EffectStep, StatusBeat, EffectStrength, EffectPlan, EffectTrace), `EffectAudit` (EffectFlow 350/250), `EffectGrammar` (MarkOf, FxMark, FxSystem, FxForm), `StanceGlow`, `FigureMotion` (FigurePose, FigureFacing, EnemyMotionShape, DefeatShape, FigureFrame), `HandFan` (HandFan, HandSpec, FanPlace), `ReceiverDrop`, `RefusalLineLayout`, `CardTextFit`, `FloorSpec`, `StatusPanelLayout`, `CornerLayout` (incl. `ScreenBox`), `PileLayout`, `PileList`, `StanceList`, `HpWickSpec`, `OmenDisclosure`, `EnemyTurnShadeLayout`, `BottomRightLayout`, `DepictionText`; the JS ports of all of these carry no JS-only additions (checked: only `DepictionTypes.js` among Script files has extensions).
`$UB`: `CoreText` (MoveBlockedLine, ConditionWord, TraitLine, StanceStoppedLine/ResumedLine …), `CardFaceText`, `StatusChipText`, `PileCards`, `FloorCells`, `CardSystems`, `CoreScriptWriter`'s one-enemy path (it already writes 「壁に当たる」, 「…（2 撃目）」, 「…（詰める）」/「（下がる）」 — `$UB/CoreScriptWriter.cs:206-275`).
`BattleCore`: `Constants.HeavyBlowPower` (6), `FragileMult`/`EmpowerMult` (1.5, double), `StatusKind.ToLabel`, `Enemies.ById(id).Name`, `TurnLoop.CanTakeFreeStep/TakeFreeStep` (`TurnLoop.cs:751-771`). `BattleCore.Sim/GreedyPlayer` for auto play (dev; must be reachable from Unity).
Not reused: `DepictionPlayer.cs` and its views (Battle.unity keeps them), `UiTween` (static speed), `DepictionFx`, `DemoFlow`, `TurnSliceScript`, `DragTrace*`.

### 5.2 JS-side additions the bridge needs (the rules analyst ports them as opt-in)

1. `DepictionTypes` multi-enemy members (`DepictionTypes.js:195-260, 408-450, 528-560, 590-625, 745-800, 925-1000`): `CardFace.ReachOn/TargetUnit/CostOn/StanceHintAfterPayOn` + `ReachFor/CostFor/CostDropFor/StanceHintAfterPayFor`; `UnitFrame.Unit/Cell/Size/Gap/GapGlyph/Hatch`; `EnemyHatch` (Count 3, AngleOf 45/−45/90); `OmenFrame.Unit/Hatch`; `DepictionFrame.Enemies/Omens/SwatchesShown`; `FloorFrame.Enemies` + `FloorEnemy`; `Cue.Unit/UnitCellAfter/UnitGapAfter`; `DepictionEvent.Unit`. Defaults keep today's C# behaviour.
2. `DepictionEventKind.FreeStep` appended at the end of the enum (JS uses the string `'FreeStep'`, `CoreBattleSource.js:57-61`); `EffectPlan.StepsOf` needs no change for it.
3. A source interface for the trial (keep `IDepictionSource` intact for `DepictionRunner`): `ITrialSource : IDepictionSource` with `Inspect(id, target)`, `TryPlay(id, zone, target, out ev)`, `PreviewFor(id, target)`, `RefusalText(id, verdict, target)`, `TargetFor(id)`, `CanTakeFreeStep()`, `CanTakeFreeStep(dir)`, `TakeFreeStep(dir)`, `State`, `AutoCardId`, nearest-gap `SuggestedCardId` (`CoreBattleSource.js:128-320`).
4. `CoreScriptWriter` multi-enemy writing (`CoreScriptWriter.js` extension blocks; the C# one throws `KeyNotFoundException` with two enemies, `$DOC §8.2`).
5. `BattleLaunch` / `DeckBuilder` / `DepartureBuilder` JS extensions (outside this area; flagged by grep).
6. The save recorder wraps the source (app analyst): every call must reach `ctx.Source` live.

---

## 6. Proposed implementation units

| unit | files | ~lines | depends on |
| --- | --- | --- | --- |
| U1 Flow assembly + Beat | `Trial/Battle/Flow/Beat.cs`, asmdef, `unity-port/Trial.Battle.Flow*.csproj`, `BeatTests` | 250 + 150 tests | – |
| U2 BattleClock | `BattleClock.cs`, `BattleClockTests` | 300 + 200 | U1 |
| U3 BattleLayout | `BattleLayout.cs` | 220 | bridge multi-enemy `EnemyHatch` (U-rules) |
| U4 Env + contracts | `BattleEnv.cs`, `BattleContracts.cs` | 650 | U1, U2, bridge types |
| U5 BattlePlayer | `BattlePlayer.cs` | 2400 | U1-U4, bridge additions 5.2 (1)-(3) |
| U6 BattleScene + driver + input | `Trial/Battle/BattleScene.cs` (+ `BattleSceneDriver`, `BattleLayers`, `BattleInput`) | 850 | U4, U5, kit (stage root, raycast helper, fonts), hand/field/hud modules |
| U7 Flow tests with fakes | `Trial.Battle.Flow.Tests/BattlePlayerFlowTests.cs`, `Fakes/*.cs` | 700 | U5 |
| U8 Bootstrap scene hook | the trial bootstrap's battle mount (router) | 80 | U6, app analyst |

Interim path: U5 can land against one enemy (C# writer as is) with the free step behind `ITrialSource`'s capability check (`CanTakeFreeStep` absent → no choice, as the JS `typeof s.CanTakeFreeStep !== 'function'` guard, `player.js:1759`).

---

## 7. Risks

- **Continuation order**: if `Beat` continuations ran synchronously on completion (or on the next Unity frame), the flow would interleave differently with `OnFrame` (follow/hover) and with input than the approved JS. Mitigation: always post to `BeatLoop`, drain at the fixed points of §2.3/§3.1, and test it.
- **`undefined` vs −1** for units: several JS branches distinguish "one enemy" from "unit 0" (`player.js:930, 995-1006, 1663`). One helper, unit tests with one and two enemies.
- **Input polling vs DOM events**: press+release in one frame, lost key repeats, Shift+digit, IME, the click-after-up order; covered in §3, but each needs a PlayMode check.
- **DOM target checks → raycasts**: every interactive Graphic must opt in to `raycastTarget`, everything else off; a stray raycast target over the arena would block FF clicks, over the hand would make the hand "foreign".
- **EventSystem vs driver order**: fix with `[DefaultExecutionOrder]`; otherwise a hud click's continuation could run a frame late.
- **Static `UiTween.Speed` / PlayerPrefs speed key** shared with Battle.unity: the trial must use neither.
- **Size of `BattlePlayer.cs`** (~2400 lines) and its review cost; keep the JS line references and review tags in comments, port in the JS order.
- **TMP text widths**: `hpBesideTag` estimates widths from font size (0.62/0.6 factors, `player.js:1338`); TMP metrics differ slightly — keep the estimate (layout must not read the hierarchy every frame, `$DOC §2.4 statusBox`).
- **Font prewarm**: TMP dynamic SDF atlases fill on first use and can hitch mid-beat; the opening wait should add the hand/pile names' characters up front (replaces `fontsFor`, `BattleScene.js:108-128`).
- **Exceptions inside `async Beat`**: must be logged (`BeatLoop.Unhandled`) or they vanish; `Safe()` wraps every `play` thunk as in JS.
- **IL2CPP**: custom `AsyncMethodBuilder` is supported (UniTask relies on it); keep the builder `public` and preserved.
- **Bridge parity**: the C# writer and the JS writer must split events identically for the held-action logic (title prefix match `（`); verify against the oracle replays for all 13 enemies.
