# Port notes: several enemies in the depiction (JS extension)

**This is not a port.** The C# depiction writer (`Bridge/CoreScriptWriter.cs`) models the first enemy
only, and throws `KeyNotFoundException` as soon as a second enemy's omen is shown (see
`depiction-writer.md`, "Bugs / oddities"). The battle core (`TurnLoop`) has handled up to three enemies
since #47 (battle_core_v4 §7.4 / §22.4), and the exploration hands in two on the elite nodes of layers
2〜3 (`LayerEncounters`: roster §9's pair, `polearm_warped` in front, `crossbow_hunter` behind, 7 cells).
This extension makes those battles play end to end through `CoreBattleSource`, and adds what a scene
needs to draw two or three enemies the way `art_document/battle-visual-v1.md` describes.

Files (all additive; every C# member keeps its meaning):

| JS | what changed |
| --- | --- |
| `src/bridge/depiction/CoreScriptWriter.js` | one `EnemyTrack` per enemy (the C#'s `_enemy` / `_omen` / `_plan` / held 構え / fall, kept per enemy); events read their enemy from `BattleEvent.Unit`; the frame carries every enemy; each card face carries its reach, cost and 構えの予告 per enemy; `TargetOf` (the unnamed receiver) |
| `src/bridge/depiction/CoreBattleSource.js` | `TargetFor(cardId)`; optional `target` on `Inspect` / `TryPlay` / `PreviewFor` / `RefusalText` (whose numbers are then that enemy's); the unattended run's flee rule reads the nearest standing enemy |
| `src/bridge/depictionScript/DepictionTypes.js` | new members below, new `FloorEnemy` class, new `EnemyHatch` |
| `src/bridge/depiction/BattleLaunch.js` | comment only (`MoreEnemyIds` now plays) |
| `test/depiction/MultiEnemy.test.mjs`, `_singleEnemyRuns.mjs`, `MultiEnemy.single.golden.json` | tests (below) |
| `test/parity/deviations.mjs`, `test/parity/run.mjs` | the documented parity deviation (below) |

## One enemy: nothing changes

In a one-enemy battle every member the C# writes comes out exactly as before, value for value. This is
pinned by a snapshot taken from the code before the extension (`MultiEnemy.single.golden.json`: 127
one-enemy runs — every roster enemy × seeds, journal levels 0/1, narrow and wide lines, carried HP and
stamina, the tools, a turn limit, and the writer fed GreedyPlayer battles — every screen event, every
frame and every answer the source gives at a wait, for every verdict). The new members are additions:
a one-enemy frame has `Enemies = [Enemy]`, `Omens = [Omen]`, units and cues name unit 0 or −1.

## The new members

### DepictionFrame

| member | meaning |
| --- | --- |
| `Enemies` (UnitFrame[]) | every enemy, in the core's enemy order (`BattleState.Enemies`: definition order = phase order = the line front to back as the battle starts, §7.4). `Enemies[k].Unit === k` for the whole battle; a fallen enemy stays, `Down`. |
| `Omens` (OmenFrame[]) | each enemy's omen badge, same order (`Omens[k].Unit === k`), each read against **its own** enemy's actions — the C# bug fixed. A fallen enemy's is hidden (`Visible` and `PlanVisible` false). |
| `SwatchesShown` (bool) | the battle has two or more enemies: draw the hatch swatches (§4.2 / §4.4 / §4.5 / §4.6). False with one. It stays true for the whole battle. |
| `Enemy`, `Omen` (C#) | **the focus enemy**: the first one standing in the line — the lowest cell among the enemies the core still has standing. `Frame.Enemy === Frame.Enemies[focus]`, `Frame.Omen === Frame.Omens[focus]` (the same objects). An enemy that fell in the move being written, its Defeat beat still to play, is passed over (the core has already freed its cells, and the player may stand past them); when it is the last enemy (the battle is won) it is that one; once every fall is written, the one that fell last. Always unit 0 with one enemy. |

### UnitFrame (enemy frames; the player's keeps the defaults)

| member | meaning |
| --- | --- |
| `Unit` | the enemy's index in the core's enemy list, the `Unit` every BattleEvent and Cue carries; −1 on the player's frame |
| `Cell`, `Size` | where it stands (lowest cell, as FloorFrame counts) and its width; 0 on the player's |
| `Gap`, `GapGlyph` | its own N to the player (§7.2) and the glyph the floor prints ("2"); −1 / "" once it has fallen in the core (also while its Defeat beat is still to play), and on the player's |
| `Hatch` | its hatch: 0 / 1 / 2 = 45° / −45° / 90° (`EnemyHatch.AngleOf`), equal to `Unit`; −1 on the player's |

### OmenFrame

`Unit` (whose omen) and `Hatch` (the swatch on the plate's first row).

### FloorFrame and FloorEnemy

`FloorFrame.Enemies` (FloorEnemy[]), in the order of `DepictionFrame.Enemies`: `Unit`, `Hatch`,
`Cell`, `Size`, `Gap`, `Down`, and the cells **its own** shown omen reaches (`AimCells`), meets the
boundary of (`DenseCells`) and would move it to (`MoveCells`) — each read exactly as the C# reads the
one enemy (`FloorCells.AimOf` / `DenseOf` / `MoveOf`, on the board as shown). The single-valued C#
members (`EnemyCell`, `EnemySize`, `Gap`, `AimCells`, `DenseCells`, `MoveCells`) are the focus enemy's.

### DepictionEvent

`Unit`: which enemy the event is about — the one whose phase an `EnemyAction` beat belongs to (its
`敵の番`, its action, its second action, its step, its second blow, its 予定変更, the wall it shoves the
player into), whose next omen a `NextOmen` decides, who falls in a `Defeat`, and the one a `PlayCard`
aimed at one enemy was played on. −1 for `TurnStart`, `TurnEnd`, and a card aimed at nobody or at
every enemy in reach.

### Cue

| member | meaning |
| --- | --- |
| `Unit` | which enemy the cue is about, −1 when it concerns the player alone. `Slash`, `GuardBlock`, `Hit` (a blow, a wall, a 見切り return) carry the enemy of the exchange, the one struck or the one striking. `GuardReset`, `GuardGain`, `StatusChange`, `HpChange`, `TraitFire` carry their holder when it is an enemy. `OmenShow`, `EnemyWindup`, `SideBonusMiss`, `Defeat` carry their enemy. `RangeSwitch` carries the enemy that moved (by itself or pushed), −1 when the player moved. |
| `UnitCellAfter`, `UnitGapAfter` | `RangeSwitch` with `Unit` ≥ 0: that enemy's cell and N after the move. The C#'s `PlayerCellAfter` / `EnemyCellAfter` / `GapAfter` / `RangeAfter` / `RangeGlyphAfter` are the focus enemy's. |

### CardFace

| member | meaning |
| --- | --- |
| `ReachOn` (bool[]) | per enemy slot: the card can be aimed at that enemy now (it stands and its N is inside the card's reach — `Reach.Contains(BattleState.GapTo(k))`, the core's own test). Empty for a card aimed at nobody. `ReachFor(k)` reads it. |
| `TargetUnit` | the enemy the card goes to when released on the receiver without the screen naming one (`CoreBattleSource.TargetFor`, below); −1 for a card that does not aim at one enemy. `Cost`, `CostDrop`, `StanceHintAfterPay`, the lamp fallback and the preview on the face are read against it. |
| `CostOn` (int[]) | per enemy slot: the cost of the card played on that enemy now — the core's (`TurnLoop.Preview(...).Cost` on it). A trait read on the gap makes it differ between enemies: 石礫 (`stone_throw`, cost 2, 間合い 2 以上 → コスト −1) costs 2 on an enemy at N 1 and 1 on one at N 2. A fallen slot holds `Cost`. Empty for a card that does not aim at one enemy (its cost does not depend on a target). `CostFor(k)` reads it (`Cost` when the slot has none), `CostDropFor(k)` is `CostDrop` read on it (the "▼n" while the card frames enemy k). |
| `StanceHintAfterPayOn` (string[]) | per enemy slot: `StanceHintAfterPay` as it reads once `CostOn[k]` is paid (§4.9 / §5.3 構えの予告). Same length as `CostOn`. `StanceHintAfterPayFor(k)` reads it (`StanceHintAfterPay` when the slot has none). |

The C#'s per-enemy arrays keep their meaning: `LitOn[k]` / `LitFor(k)` and `HpLossOn[k]` /
`HpLossFor(k)` are indexed by the same enemy slot `k`. `TraitLit` stays "any enemy in reach satisfies
the trait" (§6.3), and `ReachHint` stays "no standing enemy in reach" (`CoreText.ReachesNobody`): that
is §5.1's 出せない札のパネル beside a hovered card, which with two or more enemies shows only when no
enemy is in reach.

At rest the face is read on `TargetUnit`, and `TargetUnit` is the cheapest receiver (below), so the
cost circle shows the lowest cost the card has on any enemy in reach — the same "any enemy" reading
as the lamp (§6.3 決定 6). A lit コスト −1 lamp therefore never sits over an undiscounted circle, and the
cost on the face is what a drop that names no enemy pays. While the card frames enemy k, every number
is re-read on k: `CostFor(k)` / `CostDropFor(k)` / `LitFor(k)` / `HpLossFor(k)` / `StanceHintAfterPayFor(k)`
and `source.PreviewFor(id, k)` (§6.3 「枠で囲んだ敵で判定し直します。予測値もその敵で出します」).

### EnemyHatch

`EnemyHatch.Count` (3) and `EnemyHatch.AngleOf(h)`: 45 / −45 / 90 degrees, as battle-visual-v1 §3.2 lists them.

## Targets

The core never picks a single-target card's enemy: `TurnLoop.PlayCard(state, id, rng, target)` takes
it from whoever releases the card ("受け皿で 1 体", §7.4); an "all" card hits everyone in reach and reads
the nearest of them; a self card reads `BattleState.Nearest`; a target that does not stand is read as
`Nearest`. The C# source passes no target, so the core reads 0 — the front enemy, also once it has
fallen, after which every single-target card is refused (`NoSuchTarget`) and a two-enemy battle could
not be won.

Here:

- `TryPlay(cardId, zone, target?)`, `Inspect(cardId, target?)`, `PreviewFor(cardId, target?)` and
  `RefusalText(cardId, verdict, target?)` take the enemy the card is released on / framed over (its
  `Unit`). The recorder (`src/app/recorder.js`) logs and replays the extra argument as it is.
- Without one, the card goes to `TargetFor(cardId)` (= `CoreScriptWriter.TargetOf(state, id)`): of the
  standing enemies it can be played on now (`TurnLoop.CanPlay` = None), the one it costs least on
  (`TurnLoop.Preview(...).Cost`), the nearest (lowest cell) of those on a tie; else the same among the
  ones inside its reach (`TurnLoop.Preview(...).InReach`); else `Nearest`; 0 when none stands. The cost
  differs between enemies only through a trait read on the gap (石礫), so for every other card this is
  the nearest such enemy. Cost comes first so that the face at rest agrees with its lamp (above).
  Every number on the card's face (`Cost` and the rest) and `PreviewFor` use exactly this target, and
  so does the `PlayCard` the core runs (the played event's `PreviewText` sums what landed on it).
  With one enemy it is always 0 — the C#'s.
- A named target that is not a standing enemy answers `NotWaiting` (the core's `NoSuchTarget` has no
  verdict; `PlayVerdict` is pinned to the C#'s members). The screen offers no receiver on a fallen enemy.
- `RefusalText(id, verdict, target?)` is battle-visual-v1 §5.4's 出せなかった理由 line (the C#'s
  `DepictionPlayer.ShowRefusal` → `RefusalLineView`), shown after a released card went back to the
  hand. Its numbers are those of the enemy the card was released on — `target`, else `face.TargetUnit`,
  the one an unnamed drop goes to: `NotEnoughStamina` quotes `face.CostFor(target)` (石礫 released on
  the enemy at N 1 needs 2, not the 1 it costs on the one at N 2), `OutOfRange` quotes
  `Enemies[target].GapGlyph` (`Player.RangeGlyph` for a card aimed at nobody or an enemy that does
  not stand). `OutOfRange` is written whenever it is asked for, also while another enemy is inside
  the card's reach: a card dropped on the enemy behind while only the one in front is in reach comes
  back with "「猪突猛進」は間合い 1〜3 の相手にしか出せません。いまは 4 です". (§5.1's "どの敵にも届かない
  ときだけ" is the hover panel's rule, `CardFace.ReachHint`, not this line's.) One enemy: exactly the
  C#'s text, whatever the verdict.
- The unattended run (`SuggestedCardId`, used by `EncounterBattle.StartNext(true)`): a card is payable
  on the enemy `TargetFor` sends it to, and the flee rule (#192) reads N to the nearest standing enemy
  (the C# reads unit 0's, also after it fell).

## Beats in a battle of several enemies

`EndTurn` plays each standing enemy's phase in turn, as the core runs them (§7.4 "並びの順に 1 体ずつ"):

```
ターン終了 (-1) → 敵の番 (0) → <action beats of 0> → 次の予兆 (0) → 敵の番 (1) → … → 次の予兆 (1)
```

- An enemy's held 構え (the Guard of its reserve, which the C# plays at the head of the move's last
  event) plays at the head of **its own** next omen. With one enemy that is the same event and place.
- **Falls.** `EnemyDefeated` (an enemy falls with others standing) and `BattleEnded` won (the last one)
  queue the fallen enemies; each falls as a `Defeat` beat of its own (`Unit` = that enemy) right after the
  beat it fell in — before the next beat opens, so its figure, omen and floor cells are gone before the
  next enemy acts. With one enemy this is the C#'s one fall at the end of the move. Event `Order` stays
  1, 2, 3 … in play order. A lost battle writes no fall for an enemy that fell with the player.
- Until its `Defeat` beat an enemy whose fall is pending shows HP 0, still standing (#288: the figure
  stays up until the fall plays); its `Gap` is already −1 and it is not the focus.
- The battle is won (`Outcome` Won) only when the last enemy falls; after one falls the fight goes on
  and the cards hit the rest (§7.4, §22.4).

## How the scene should draw it (battle-visual-v1)

Everything below is read from the frame / cues; the scene computes nothing.

- **Figures and number panels**: one figure per `Frame.Enemies[k]` that is not `Down`, standing on
  `Cell` (width `Size`), art `ArtId`; its number panel (§4.5) under it with `Hp` / `Guard` / `Stamina`,
  `ReserveLikely`, `Elite`, `Statuses`. Place by `Cell`, never by list position: enemies may pass each
  other (§7.1).
- **Swatches** (`SwatchesShown`): a 16 px swatch at the left of each enemy's HP row (§4.5), a 22 px one
  at the right end of its omen plate's first row (§4.4), one at the left of its stance frames (§4.6) —
  red stripes at `EnemyHatch.AngleOf(Hatch)`, the same direction as its floor cells.
- **Floor** (§4.2): for each `Frame.Floor.Enemies[k]` not `Down`, hatch `AimCells` at its angle (where two
  enemies' cells overlap the directions cross), `DenseCells` at 6 px, `MoveCells` as the violet dashed
  frame. The amber reach glow and draw order are as for one enemy.
- **The one N** (§4.2 間合いの数字): at rest `Floor.Gap` / `Player.RangeGlyph` (the nearest standing
  enemy, the focus); while a held card frames enemy k, `Frame.Enemies[k].GapGlyph`, bracketing the empty
  cells up to `Enemies[k].Cell`.
- **Omen plates** (§4.4): one per `Frame.Omens[k]` above enemy k's figure. During an enemy's phase the
  acting plate glows: the `EnemyAction` beat's `Unit`.
- **A card at rest** (§6): the cost circle from `face.Cost` / `face.CostDrop`, the lamp from
  `face.TraitLit`, the 出せない札のパネル beside a hovered card from `face.ReachHint` (empty while any
  enemy is in reach, §5.1).
- **Holding a card** (§5.2): amber corners around enemy k when `face.ReachFor(k)`, steel ones otherwise;
  the predicted value over the framed enemy from `source.PreviewFor(id, k)`; the HP wick on its bar from
  `face.HpLossFor(k)`; the lamp re-lit from `face.LitFor(k)` (§6.3); the cost circle from
  `face.CostFor(k)` / `face.CostDropFor(k)`, the stamina's 「▸ n」 from `face.CostFor(k)`, and the
  構えの予告 from `face.StanceHintAfterPayFor(k)`. While no enemy is framed, the face's own values
  (`Cost`, `StanceHintAfterPay`: the `TargetUnit` ones). Released over enemy k:
  `source.TryPlay(id, DropZone.Receiver, k)`; when it is refused, the §5.4 line
  `source.RefusalText(id, verdict, k)`. A drop that names no enemy goes to `face.TargetUnit`.
- **Cues**: play each on the enemy its `Unit` names (`Slash` from / onto that figure, `Defeat` on that
  figure, `OmenShow` on that plate, `RangeSwitch` walks that figure to `UnitCellAfter`). After a
  `Defeat` beat that enemy's figure, panel, omen, stance frames and floor cells are gone (`Down`).
- **Not in the spec**: where the second and third number panels and stance-frame stacks go when two
  enemies stand on neighbouring cells (§4.5 gives "under the figure, ≤ cell width − 10"; §4.6 gives one
  enemy stack at the right edge). That is the scene's / design lane's call.

A scene written against the one-enemy shape keeps running: it shows the focus enemy (`Frame.Enemy`)
and its omen, and switches to the next one when the front one falls. It should read
`LitFor(frame.Enemy.Unit)` / `HpLossFor(frame.Enemy.Unit)` instead of slot 0, and it cannot aim at an
enemy that is not the default target.

### Hand-off: the battle scene's files are behind this extension (not changed here)

`docs/BATTLE_SCENE.md` and `src/scenes/battle/player.js` belong to the battle scene's architect
(`docs/CONVENTIONS.md`: no edits to another agent's files), so this extension leaves them as they are.
Two places there still describe the writer before it, and following them now draws an enemy twice:

- `BATTLE_SCENE.md` §3 "**One enemy in the frame.**" says the script depicts `Enemies[0]` only and folds
  every enemy event into that one model, and tells the scene to draw the enemies behind from
  `source.State.Enemies.slice(1)` through `field.setExtras`. `player.js` `updateExtras()` does exactly
  that. Since `Frame.Enemy` is now the focus enemy, it becomes unit 1 once unit 0 falls, and
  `State.Enemies.slice(1)` still holds unit 1: in the roster pair the crossbow is drawn twice on cell 7
  (seed 1, unattended: from event 21, `Defeat` of unit 0, on). The extras should be every enemy but the
  focus one (`Frame.Enemy.Unit`, the index in `State.Enemies`); the smallest change to `updateExtras()`
  is its first line of the list:

  ```js
  const focus = frameNow().Enemy.Unit; // the enemy the frame already draws
  const list = st.Enemies.filter((u, k) => k !== focus).map((u) => ({ /* unchanged */ }));
  ```

  Better still, draw every `Frame.Enemies[k]` with its own beats (the section above), which retires
  the extras path and its reads of the core state.
- `BATTLE_SCENE.md` §8.2's first bullet (the pair throws `KeyNotFoundException` 'bolt' and the scene goes
  `halted`) is no longer true of the port: the pair plays to its end (the C# still throws). The member
  lists of §3 do not name the members above.

## The deviation from the C#, and the parity harness

The C# oracle records the C# writer's exception for the two-enemy cases. The port writes those battles
instead, so two oracle cases differ by design. They are listed in `test/parity/deviations.mjs`, with
the reason; `node test/parity/run.mjs` counts them as "documented deviations" (not failures) and prints
each with its reason on every run, `--quiet` included. Acceptance is narrow:

- `scripts / writer:charge_release|polearm_warped+crossbow_hunter|1`: the opening frame and every
  replayed call must still equal the oracle's, every step where the C# threw must be written (an event
  list), and at least one such step must exist. The events themselves are not compared (the C# models
  one enemy).
- `scripts / source:charge_release|polearm_warped+crossbow_hunter|1`: the oracle holds the exception
  alone (the C# throws on the first `AdvanceAuto`); the port must have played the unattended run to its
  end.

Anything else that differs in those cases still fails, and an entry whose case matches the oracle again
is reported as stale. The C# itself is unchanged (still throws).

## Tests

`test/depiction/MultiEnemy.test.mjs`:

- one-enemy runs equal the snapshot on every C# member, and the added members are exactly the ones above.
  The snapshot pins the whole one-enemy pipeline (rules included): after a deliberate change to the
  one-enemy depiction or to the battle rules, re-write it with `node test/depiction/_singleEnemyRuns.mjs --write`
  from the code *before* that change, then apply the change and check the diff is the intended one;
- the roster pair × seeds 1..40 and every two- and three-enemy line the roster can make (13 × 13 − 16
  pairs, 9³ threes of one-cell enemies, on the narrowest line that holds them) run unattended to the end
  without an exception. The broad sweep runs seeds 1..2 by default; `MULTI_ENEMY_SEEDS=40` runs every
  line × seeds 1..40 (35 280 battles, several minutes). After every event: numbering, `Unit` on events
  and cues, the per-enemy frame invariants (units, hatches, floor entries, the focus, the single-valued
  members, a fallen enemy shows nothing, nobody shares a cell, the N shown is never negative). Once a
  move has played out: each enemy's HP, Guard, stamina, max stamina, statuses, stances, cell, N, omen
  (kind, icon, reach — against its own actions), plan, aimed / dense / move cells equal the core state.
  At each wait: each card's `TargetUnit` is `TargetFor`, its cost is the core's on that target, the
  default `Inspect` / `PreviewFor` equal the explicit ones, `ReachOn` is the core's reach per enemy. At
  the end: Won only with every enemy down and fallen once;
- a named target is the one the card hits, also where the default would go elsewhere; a card released
  on an enemy out of its reach is refused with that enemy's N, also while another enemy is in reach
  (§5.4), while the hover panel (`ReachHint`) stays empty then (§5.1);
- 石礫 on the pair from cell 4 (N 1 and 2): `CostOn` [2, 1], the face at rest on the cheaper enemy
  (cost 1 ▼1 under a lit lamp), each enemy's cost, ▼ and 構えの予告 while framed, and the refusal on
  the dearer enemy quoting its own cost; and the pair × seeds 1..40 with the prototype deck's 盾打ち
  swapped for 石礫 ×2, where at each wait every card's `CostFor(k)` / `CostDropFor(k)` /
  `StanceHintAfterPayFor(k)` and the refusal texts on each enemy are the core's, and the default target
  is the cheapest receiver (the sweep's `checkWait` asserts the same in every battle);
- the exploration path: an `Expedition` walked (battles stubbed as won) to a layer 2 and a layer 3 elite
  node, which `LayerEncounters` fills with the pair on 7 cells, fought for real through
  `EncounterBattle.StartNext(true)` with the same checks, finished and returned (seeds 1..4 each).

## Open points

- One journal level (開示度) for the battle: `EncounterBattle` hands in the lowest of the enemies'
  (C# behaviour); a per-enemy level would need `EncounterBattle` / `BattleLaunch` to carry one per enemy.
- The corner's floor and miasma stay the C#'s placeholders (`depiction-writer.md`).
- The C# depiction has no multi-enemy design yet; if Unity adds one, this extension should follow it.
