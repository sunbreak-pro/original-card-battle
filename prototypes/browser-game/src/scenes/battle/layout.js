// Stage-px constants of the battle screen that no ported layout script holds: in Unity they live in the
// prefab or in a View's code; here they come from the design system's OCB.LAYOUT / layout.md (the
// copy of battle-visual-v1 §4) and from DepictionPlayer / DepictionFx. Everything a ported script does
// hold (HandSpec, FloorSpec, StatusPanelLayout, CornerLayout, PileLayout, PlayedLayout, ReserveHintLayout,
// RefusalLineLayout, EnemyTurnShadeLayout, StanceList, FigureFrame) is read from that script instead.
// Owned by the architect; module agents ask before adding a value.
import { EnemyHatch } from '../../bridge/depictionScript/DepictionTypes.js';
import { Enemies } from '../../core/battle/Enemies.js';

export const L = Object.freeze({
  W: 1920,
  H: 1080,

  /** layout.md: the horizon; below it the ground is dark (floor, hand and numbers stay on dark ground). */
  HORIZON_Y: 556,
  /** FloorSpec top 560 + FeetBelowTop 10 (FloorView.FeetOf). */
  FEET_Y: 570,
  /** battle_ui_ux_v2 §0 / §5.0: numbers rise from the chest (65% of the height from the feet) toward the head (84%). */
  CHEST: 0.65,
  HEAD: 0.84,

  /** battle-visual §4.4 / OmenBadgeView: the badge 200×98, its bottom 14 px over the enemy's head (DepictionPlayer.OmenOverHead). */
  OMEN: Object.freeze({ w: 200, h: 98, overHead: 14 }),
  /** ReceiverView (#568): the invisible square a single-target card is released on, centred on the enemy's middle. */
  RECEIVER: Object.freeze({ w: 340, h: 340 }),
  /** TargetMarkView: the hooks stand this far outside the figure. */
  MARK_PAD: Object.freeze({ x: 46, top: 22, bottom: 12, hook: 26, width: 3 }),
  /**
   * DS motion.md 「予測値の札のぶん予兆の札を上へよける（`tag.lift`）」 (0〜80 px) and TargetFrame (「予測値の札は枠の上辺の中央」;
   * 「予兆の札を上へずらす案」). Review 19 (the tag pushed down under the badge stood on the enemy's head and hid the badge's ▼):
   * the tag stands on the frame's top edge, and the badge of an enemy showing it rises just far enough that its bottom keeps
   * TAG_LIFT_GAP over the tag (at most TAG_LIFT); at rest every badge is 14 px over its head (battle-visual-v1 §4.4).
   */
  TAG_LIFT: 80,
  TAG_LIFT_GAP: 4,
  /** TargetFrame / battle-visual-v1 §5.2: the prediction tag is 60 px tall. */
  PRED_TAG_H: 60,
  /** battle_ui_ux_v2 §5.11 順 1 / 順 8: a stance card (an enemy's stance omen) flies to the end of the stance list over this (out). */
  STANCE_FLY_MS: 300,
  /** §5.9 順 12: an enemy's fired trait's condition word swells in at the right end of its badge over this (out). */
  TRAIT_WORD_MS: 150,
  /** ThrowLineView / battle-visual §5.3: the amber dashed line, only while a self card is held. */
  THROW: Object.freeze({ y: 790, x0: 400, x1: 1520, belowAlpha: 0.6 }),
  /** layout.md スタミナの札. */
  STAMINA: Object.freeze({ x: 32, y: 846, w: 236, h: 84 }),
  /** layout.md ターン終了 (DepictionPlayer.StyleEndTurn: amber 2 px frame, amber 16%, 24 px label). */
  END_TURN: Object.freeze({ x: 1672, y: 976, w: 224, h: 72 }),
  /** layout.md バナー: y 400, 132 tall (Unity's plate: 1920×120 centred 80 px over the middle). */
  BANNER: Object.freeze({ y: 400, h: 132 }),
  /** layout.md 案内の行: centre x 960, y ≈ 757, body 22 px, no panel. */
  GUIDE: Object.freeze({ y: 743, h: 28 }),
  /** DepictionPlayer.BuildResultCard: 520×220, 60 px over the middle, rises 40 px. */
  RESULT: Object.freeze({ w: 520, h: 220, dy: -60, rise: 40 }),

  /** DepictionPlayer.Lunge(…, 46f, …). */
  LUNGE_PX: 46,
  /** PlayAccepted: a self card is tossed 140 px upward. */
  RELEASE_RISE: 140,
  /** CardRelease: the card shrinks to half. */
  RELEASE_SCALE: 0.5,
  /** DepictionPlayer.SnapPull. */
  SNAP_PULL: 0.35,
  /** BeginHold: a held card stands at 92% opacity. */
  HELD_ALPHA: 0.92,
  /** battle_ui_ux_v2 §3.2: a drag starts 8 px from the press (not scaled by speed). */
  DRAG_THRESHOLD: 8,
  /** §3.13: a long press opens the detail after 400 ms (not scaled). */
  LONG_PRESS_MS: 400,
  /** AutoDrag: the wait before a suggested drag, and the drag's length (nominal ms, inout). */
  AUTO_WAIT_MS: 400,
  AUTO_DRAG_MS: 450,
  /** ReturnToHand's shake (UiTween.Shake(card, 8, 2, ms)). */
  SHAKE: Object.freeze({ px: 8, cycles: 2 }),
  /** FlyGhostTo: the played card's ghost, 76×104, ink at 55%. */
  GHOST: Object.freeze({ w: 76, h: 104, alpha: 0.55 }),

  /**
   * A blow's HP number by intensity tier 1..4. Review fix: battle-visual-v1 §3.1 / §5.5 and the DS type scale (README
   * 文字: 48 「HP の被弾」) give the HP loss 48 px; Unity's DepictionFx.NumberSize is [56, 72, 88, 108] (72 at the uniform
   * tier 2 of battle_ui_ux_v2 §5.3). The tiers keep Unity's ratios around 48.
   */
  NUMBER_SIZE: Object.freeze([37, 48, 59, 72]),
  /** battle-visual-v1 §3.1 / §5.5 and the DS type scale (34 「Guard の被弾」): the "Guard −n" float (DepictionPlayer: 44). */
  GUARD_NUMBER_SIZE: 34,
  /** DepictionPlayer.FollowPanels: how fast a panel closes on its place, per second. */
  PANEL_FOLLOW_RATE: 14,

  /**
   * Scene choices (Unity has no fast-forward; the design system's prototype collapsed every length to 1 ms). Review 16:
   * at ×1000 every number of an action lived 2 frames (battle_ui_ux_v2 「早送りは動きだけを短縮し、数値と位置は同じです」);
   * fast-forward runs the motion ×4 and the floating numbers and words at most ×1.5 the battle speed (clock.runPaced),
   * so a hurrying player still reads what hit them.
   */
  FF_SCALE: 4,
  FF_FLOAT_SCALE: 1.5,
  /**
   * Scene choice (review 19: taps of Space that came in an action's last moments, or in the pause after it, sped next to
   * nothing): a one-press fast-forward that has run less than this many real ms when its action ends carries on to the
   * end of the next one.
   */
  FF_MIN_RUN_MS: 300,
  /** BattleSpeed.HaltedTweenSpeed: 演出短縮 and a halted battle. */
  SHORT_SCALE: 1000,
  /**
   * From the result card's rise to the app's next screen (real ms; a click skips it): Unity raises
   * BattleFinished as the card starts to rise and DemoFlow waits EndScreenDelaySeconds = 1.2
   * (battle_ui_ux_v2 §6.2 「結果の札から終了画面までの 1.2 秒」).
   */
  RESULT_HOLD_MS: 1200,
  /**
   * Scene choice (review 16: clicks made to hurry the enemy's turn skipped 負け 0.14 s after it appeared): a press skips
   * the result only once the card has stood this many real ms after rising, and only after a pause this long since the
   * last press (a mash of clicks never skips it; the 1.2 s then run out).
   */
  RESULT_SKIP_AFTER_MS: 400,
  RESULT_SKIP_PAUSE_MS: 350,
  /** battle_ui_ux_v2 §5.1 順 8: the standing omens blink once at the turn start, 120 apart in the enemies' action order. */
  OMEN_BLINK_STAGGER_MS: 120,
  /** battle_ui_ux_v2 §3.11 / §5.13: the pause between two enemies' phases (none between an elite's two actions). */
  BETWEEN_ENEMIES_MS: 600,
  /**
   * Scene choice (review fix): while fast-forward collapses every stagger, a sound repeated within this many
   * real ms is dropped (6 discards and 6 draws fired in the same instant, louder than a hit).
   */
  FF_SOUND_GAP_MS: 90,
  /**
   * Scene choice (review fix: clicks on the end-turn plate every 100 ms to hurry the enemy's turn ended the next turn
   * as it opened): a press of the plate or X while the flow runs, or within this many real ms of such a press, is
   * swallowed and slides the window on; the first press after a pause this long ends the turn.
   */
  END_TURN_MASH_MS: 600,
  /**
   * Scene choice (review 16: clicks every 0.4 s still ended the next turn 0.36 s after it opened, as the 350 ms window
   * caught only presses closer together than that): a press of the plate or X within this many real ms of the turn
   * opening (the player is asked again after a turn start) is swallowed too.
   */
  END_TURN_OPEN_GUARD_MS: 500,
  /**
   * Scene choice (review 17: presses every 0.65–0.8 s slipped between the two values above and still ended the next turn
   * 0.6–1.1 s after it opened): the window a press of the plate or X opens follows the press rhythm — this factor times
   * the interval since the press before it (at least END_TURN_MASH_MS, at most END_TURN_MASH_MAX_MS) — so a press that
   * keeps a rhythm under 1.5 s is swallowed however long it goes on, and the first one after a pause counts.
   */
  END_TURN_MASH_RHYTHM: 1.3,
  END_TURN_MASH_MAX_MS: 1500,
  /**
   * Scene choice (review 21: a swallowed press showed nothing, and the plate looked broken): a press of the plate or X the
   * mash window swallows in the player's own wait shakes the plate once over this (nominal ms).
   */
  END_TURN_REFUSE_MS: 220,
  /**
   * battle_ui_ux_v2 §5.15 順 5 「倒れ（800）→ 人型が薄れて消え、足元の灯りが消えてマスが空く（300）→ 数値パネルが灰に（300）」:
   * an enemy that falls while others stand — the foot light and the grey panel's beats after the 800 (nominal ms).
   */
  FALL_FOOT_MS: 300,
  FALL_GREY_MS: 300,
  /** battle_ui_ux_v2 §3.14 / §5.7 順 5: a move that went nowhere — the figure leans that way and back over this (nominal ms). */
  MOVE_BUMP_MS: 40,
  /** battle_ui_ux_v2 §3.5: the end-turn plate brightens once over this (nominal ms) when no card can be played. */
  END_TURN_FLASH_MS: 600,
  /** The OS double-click interval (Windows' default): a press this soon after the click that played a card is its pair. */
  DOUBLE_CLICK_MS: 500,
  /** sound.md: the heartbeat plays three times at the player's turn start while HP is under 15 (the vignette's line too). */
  HEARTBEAT_HP: 15,
  /** battle_ui_ux_v2 §5.6 順 5: a landed blow under 15 HP pulses the vignette 0.5 → 0.25 over this. */
  VIGNETTE_PULSE_MS: 200,
  /**
   * Scene choice (review fix): until the opening frame is on the screen only the layer's backdrop shows (no
   * half-built HUD while the fonts load); then the rest fades in over this many nominal ms (Ease.Out) …
   */
  OPEN_FADE_MS: 240,
  /** … and the fonts the opening frame's text needs are waited for at most this many real ms. */
  OPEN_FONT_WAIT_MS: 1200,
});

/** DepictionFx.NumberFont: the font size of a number at intensity `tier` (1..4). */
export function numberFont(tier) {
  const i = Math.max(1, Math.min(4, tier | 0)) - 1;
  return L.NUMBER_SIZE[i];
}

/**
 * The design system's hatch angle for enemy hatch `hatch` (EnemyHatch.AngleOf: 45 / −45 / 90). OCB.hatch draws a
 * vertical bar rotated by its angle, so 90 comes out as horizontal stripes, and 0 falls back to 45 through its
 * `angle || 45`. battle_ui_ux_v2 §1.4 (「3 体目 縦」) and battle-visual-v1 §4.2 want the third enemy's stripes
 * upright: 90° is handed to the DS as 180 (the same upright bar). Every hatch of the scene goes through this:
 * the floor's stripes, the omen badge's, the number panel's and the stance frames' swatches (review fix).
 */
export function hatchAngle(hatch) {
  const a = EnemyHatch.AngleOf(hatch | 0);
  return a === 90 ? 180 : a;
}

/**
 * The roster's name of an enemy (UnitFrame.ArtId is its roster id; Enemies.ById(id).Name), or '' when the id is not on
 * the roster. Scene use only: the pile list's log with two or three enemies and the chain's switch banner (review 21).
 */
export function enemyNameOf(artId) {
  if (!artId) return '';
  try {
    const def = Enemies.ById(artId);
    return def && def.Name ? def.Name : '';
  } catch (err) {
    return '';
  }
}

/** The enemy's hatch as a glyph (battle-visual-v1 §8 凡例 ／＼; the third enemy's upright stripes ｜): a log line's tag. */
export function hatchGlyph(hatch) {
  const a = EnemyHatch.AngleOf(hatch | 0);
  return a === 90 ? '｜' : a < 0 ? '＼' : '／';
}
