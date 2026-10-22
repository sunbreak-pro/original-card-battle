// Port of scenes/battle/layout.js: the stage-px constants of the battle screen that no ported layout script holds.
// In Unity they lived in the prefab or in a View's code; here they come from the design system's OCB.LAYOUT /
// layout.md (the copy of battle-visual-v1 §4) and from DepictionPlayer / DepictionFx. Everything a ported script does
// hold (HandSpec, FloorSpec, StatusPanelLayout, CornerLayout, PileLayout, PlayedLayout, ReserveHintLayout,
// RefusalLineLayout, EnemyTurnShadeLayout, StanceList, FigureFrame) is read from that script instead.
//
// Names convert mechanically (FEET_Y → L.FeetY, OMEN.overHead → L.Omen.OverHead); the JS name stands in each comment
// for grep. Engine-free; the View rules boundary holds (no `using BattleCore;`, no rule-class call).
using System;
using System.Collections.Generic;
using Depiction;

namespace Trial.Battle
{
    /// <summary>layout.js `L` (Object.freeze). Stage px are 1920×1080, origin top left, y down; times are nominal ms unless "real".</summary>
    public static class L
    {
        /// <summary>W.</summary>
        public const float W = 1920f; // js: scenes/battle/layout.js:11
        /// <summary>H.</summary>
        public const float H = 1080f; // js: scenes/battle/layout.js:12

        /// <summary>HORIZON_Y — layout.md: the horizon; below it the ground is dark (floor, hand and numbers stay on dark ground).</summary>
        public const float HorizonY = 556f; // js: scenes/battle/layout.js:15
        /// <summary>FEET_Y — FloorSpec top 560 + FeetBelowTop 10 (FloorView.FeetOf).</summary>
        public const float FeetY = 570f; // js: scenes/battle/layout.js:17
        /// <summary>CHEST — battle_ui_ux_v2 §0 / §5.0: numbers rise from the chest (65% of the height from the feet) …</summary>
        public const float Chest = 0.65f; // js: scenes/battle/layout.js:19
        /// <summary>HEAD — … toward the head (84%).</summary>
        public const float Head = 0.84f; // js: scenes/battle/layout.js:20

        /// <summary>OMEN — battle-visual §4.4 / OmenBadgeView: the badge 200×98, its bottom 14 px over the enemy's head (DepictionPlayer.OmenOverHead).</summary>
        public static class Omen // js: scenes/battle/layout.js:23
        {
            public const float W = 200f;
            public const float H = 98f;
            public const float OverHead = 14f;
        }

        /// <summary>RECEIVER — ReceiverView (#568): the invisible square a single-target card is released on, centred on the enemy's middle.</summary>
        public static class Receiver // js: scenes/battle/layout.js:25
        {
            public const float W = 340f;
            public const float H = 340f;
        }

        /// <summary>MARK_PAD — TargetMarkView: the hooks stand this far outside the figure.</summary>
        public static class MarkPad // js: scenes/battle/layout.js:27
        {
            public const float X = 46f;
            public const float Top = 22f;
            public const float Bottom = 12f;
            public const float Hook = 26f;
            public const float Width = 3f;
        }

        /// <summary>
        /// TAG_LIFT — DS motion.md 「予測値の札のぶん予兆の札を上へよける（`tag.lift`）」 (0〜80 px) and TargetFrame. Review 19: the tag
        /// stands on the frame's top edge, and the badge of an enemy showing it rises just far enough that its bottom keeps
        /// TAG_LIFT_GAP over the tag (at most TAG_LIFT); at rest every badge is 14 px over its head (battle-visual-v1 §4.4).
        /// </summary>
        public const float TagLift = 80f; // js: scenes/battle/layout.js:34
        /// <summary>TAG_LIFT_GAP.</summary>
        public const float TagLiftGap = 4f; // js: scenes/battle/layout.js:35
        /// <summary>PRED_TAG_H — TargetFrame / battle-visual-v1 §5.2: the prediction tag is 60 px tall.</summary>
        public const float PredTagH = 60f; // js: scenes/battle/layout.js:37
        /// <summary>STANCE_FLY_MS — battle_ui_ux_v2 §5.11 順 1 / 順 8: a stance card (an enemy's stance omen) flies to the end of the stance list over this (out).</summary>
        public const float StanceFlyMs = 300f; // js: scenes/battle/layout.js:39
        /// <summary>TRAIT_WORD_MS — §5.9 順 12: an enemy's fired trait's condition word swells in at the right end of its badge over this (out).</summary>
        public const float TraitWordMs = 150f; // js: scenes/battle/layout.js:41

        /// <summary>THROW — ThrowLineView / battle-visual §5.3: the amber dashed line, only while a self card is held.</summary>
        public static class Throw // js: scenes/battle/layout.js:43
        {
            public const float Y = 790f;
            public const float X0 = 400f;
            public const float X1 = 1520f;
            public const float BelowAlpha = 0.6f;
        }

        /// <summary>STAMINA — layout.md スタミナの札.</summary>
        public static class Stamina // js: scenes/battle/layout.js:45
        {
            public const float X = 32f;
            public const float Y = 846f;
            public const float W = 236f;
            public const float H = 84f;
        }

        /// <summary>END_TURN — layout.md ターン終了 (DepictionPlayer.StyleEndTurn: amber 2 px frame, amber 16%, 24 px label).</summary>
        public static class EndTurn // js: scenes/battle/layout.js:47
        {
            public const float X = 1672f;
            public const float Y = 976f;
            public const float W = 224f;
            public const float H = 72f;
        }

        /// <summary>BANNER — layout.md バナー: y 400, 132 tall (Unity's plate: 1920×120 centred 80 px over the middle).</summary>
        public static class Banner // js: scenes/battle/layout.js:49
        {
            public const float Y = 400f;
            public const float H = 132f;
        }

        /// <summary>GUIDE — layout.md 案内の行: centre x 960, y ≈ 757, body 22 px, no panel.</summary>
        public static class Guide // js: scenes/battle/layout.js:51
        {
            public const float Y = 743f;
            public const float H = 28f;
        }

        /// <summary>RESULT — DepictionPlayer.BuildResultCard: 520×220, 60 px over the middle, rises 40 px.</summary>
        public static class Result // js: scenes/battle/layout.js:53
        {
            public const float W = 520f;
            public const float H = 220f;
            public const float Dy = -60f;
            public const float Rise = 40f;
        }

        /// <summary>LUNGE_PX — DepictionPlayer.Lunge(…, 46f, …).</summary>
        public const float LungePx = 46f; // js: scenes/battle/layout.js:56
        /// <summary>RELEASE_RISE — PlayAccepted: a self card is tossed 140 px upward.</summary>
        public const float ReleaseRise = 140f; // js: scenes/battle/layout.js:58
        /// <summary>RELEASE_SCALE — CardRelease: the card shrinks to half.</summary>
        public const float ReleaseScale = 0.5f; // js: scenes/battle/layout.js:60
        /// <summary>SNAP_PULL — DepictionPlayer.SnapPull.</summary>
        public const float SnapPull = 0.35f; // js: scenes/battle/layout.js:62
        /// <summary>HELD_ALPHA — BeginHold: a held card stands at 92% opacity.</summary>
        public const float HeldAlpha = 0.92f; // js: scenes/battle/layout.js:64
        /// <summary>DRAG_THRESHOLD — battle_ui_ux_v2 §3.2: a drag starts 8 px from the press (stage px, not scaled by speed).</summary>
        public const float DragThreshold = 8f; // js: scenes/battle/layout.js:66
        /// <summary>LONG_PRESS_MS — §3.13: a long press opens the detail after 400 ms (real, not scaled).</summary>
        public const float LongPressMs = 400f; // js: scenes/battle/layout.js:68
        /// <summary>AUTO_WAIT_MS — AutoDrag: the wait before a suggested drag …</summary>
        public const float AutoWaitMs = 400f; // js: scenes/battle/layout.js:70
        /// <summary>AUTO_DRAG_MS — … and the drag's length (nominal ms, inout).</summary>
        public const float AutoDragMs = 450f; // js: scenes/battle/layout.js:71

        /// <summary>SHAKE — ReturnToHand's shake (UiTween.Shake(card, 8, 2, ms)).</summary>
        public static class Shake // js: scenes/battle/layout.js:73
        {
            public const float Px = 8f;
            public const int Cycles = 2;
        }

        /// <summary>GHOST — FlyGhostTo: the played card's ghost, 76×104, ink at 55%.</summary>
        public static class Ghost // js: scenes/battle/layout.js:75
        {
            public const float W = 76f;
            public const float H = 104f;
            public const float Alpha = 0.55f;
        }

        /// <summary>
        /// NUMBER_SIZE — a blow's HP number by intensity tier 1..4. Review fix: battle-visual-v1 §3.1 / §5.5 and the DS type
        /// scale (README 文字: 48 「HP の被弾」) give the HP loss 48 px; Unity's DepictionFx.NumberSize is [56, 72, 88, 108] (72 at
        /// the uniform tier 2 of battle_ui_ux_v2 §5.3). The tiers keep Unity's ratios around 48.
        /// </summary>
        public static readonly IReadOnlyList<float> NumberSize = Array.AsReadOnly(new[] { 37f, 48f, 59f, 72f }); // js: scenes/battle/layout.js:82
        /// <summary>GUARD_NUMBER_SIZE — battle-visual-v1 §3.1 / §5.5 and the DS type scale (34 「Guard の被弾」): the "Guard −n" float (DepictionPlayer: 44).</summary>
        public const float GuardNumberSize = 34f; // js: scenes/battle/layout.js:84
        /// <summary>PANEL_FOLLOW_RATE — DepictionPlayer.FollowPanels: how fast a panel closes on its place, per second.</summary>
        public const float PanelFollowRate = 14f; // js: scenes/battle/layout.js:86

        /// <summary>
        /// FF_SCALE — scene choices (Unity has no fast-forward; the design system's prototype collapsed every length to 1 ms).
        /// Review 16: at ×1000 every number of an action lived 2 frames (battle_ui_ux_v2 「早送りは動きだけを短縮し、数値と位置は同じです」);
        /// fast-forward runs the motion ×4 and the floating numbers and words at most ×1.5 the battle speed (clock.RunPaced),
        /// so a hurrying player still reads what hit them.
        /// </summary>
        public const float FfScale = 4f; // js: scenes/battle/layout.js:94
        /// <summary>FF_FLOAT_SCALE.</summary>
        public const float FfFloatScale = 1.5f; // js: scenes/battle/layout.js:95
        /// <summary>
        /// FF_MIN_RUN_MS — scene choice (review 19: taps of Space that came in an action's last moments, or in the pause after
        /// it, sped next to nothing): a one-press fast-forward that has run less than this many real ms when its action ends
        /// carries on to the end of the next one.
        /// </summary>
        public const float FfMinRunMs = 300f; // js: scenes/battle/layout.js:101
        /// <summary>SHORT_SCALE — BattleSpeed.HaltedTweenSpeed: 演出短縮 and a halted battle.</summary>
        public const float ShortScale = 1000f; // js: scenes/battle/layout.js:103
        /// <summary>
        /// RESULT_HOLD_MS — from the result card's rise to the app's next screen (real ms; a click skips it): Unity raises
        /// BattleFinished as the card starts to rise and DemoFlow waits EndScreenDelaySeconds = 1.2 (battle_ui_ux_v2 §6.2
        /// 「結果の札から終了画面までの 1.2 秒」).
        /// </summary>
        public const float ResultHoldMs = 1200f; // js: scenes/battle/layout.js:109
        /// <summary>
        /// RESULT_SKIP_AFTER_MS — scene choice (review 16: clicks made to hurry the enemy's turn skipped 負け 0.14 s after it
        /// appeared): a press skips the result only once the card has stood this many real ms after rising, and only after a
        /// pause this long (RESULT_SKIP_PAUSE_MS) since the last press (a mash of clicks never skips it; the 1.2 s then run out).
        /// </summary>
        public const float ResultSkipAfterMs = 400f; // js: scenes/battle/layout.js:115
        /// <summary>RESULT_SKIP_PAUSE_MS.</summary>
        public const float ResultSkipPauseMs = 350f; // js: scenes/battle/layout.js:116
        /// <summary>OMEN_BLINK_STAGGER_MS — battle_ui_ux_v2 §5.1 順 8: the standing omens blink once at the turn start, 120 apart in the enemies' action order.</summary>
        public const float OmenBlinkStaggerMs = 120f; // js: scenes/battle/layout.js:118
        /// <summary>BETWEEN_ENEMIES_MS — battle_ui_ux_v2 §3.11 / §5.13: the pause between two enemies' phases (none between an elite's two actions).</summary>
        public const float BetweenEnemiesMs = 600f; // js: scenes/battle/layout.js:120
        /// <summary>
        /// FF_SOUND_GAP_MS — scene choice (review fix): while fast-forward collapses every stagger, a sound repeated within this
        /// many real ms is dropped (6 discards and 6 draws fired in the same instant, louder than a hit).
        /// </summary>
        public const float FfSoundGapMs = 90f; // js: scenes/battle/layout.js:125
        /// <summary>
        /// END_TURN_MASH_MS — scene choice (review fix: clicks on the end-turn plate every 100 ms to hurry the enemy's turn ended
        /// the next turn as it opened): a press of the plate or X while the flow runs, or within this many real ms of such a
        /// press, is swallowed and slides the window on; the first press after a pause this long ends the turn.
        /// </summary>
        public const float EndTurnMashMs = 600f; // js: scenes/battle/layout.js:131
        /// <summary>
        /// END_TURN_OPEN_GUARD_MS — scene choice (review 16: clicks every 0.4 s still ended the next turn 0.36 s after it opened):
        /// a press of the plate or X within this many real ms of the turn opening is swallowed too.
        /// </summary>
        public const float EndTurnOpenGuardMs = 500f; // js: scenes/battle/layout.js:137
        /// <summary>
        /// END_TURN_MASH_RHYTHM — scene choice (review 17: presses every 0.65–0.8 s slipped between the two values above): the
        /// window a press opens follows the press rhythm — this factor times the interval since the press before it (at least
        /// END_TURN_MASH_MS, at most END_TURN_MASH_MAX_MS).
        /// </summary>
        public const float EndTurnMashRhythm = 1.3f; // js: scenes/battle/layout.js:144
        /// <summary>END_TURN_MASH_MAX_MS.</summary>
        public const float EndTurnMashMaxMs = 1500f; // js: scenes/battle/layout.js:145
        /// <summary>
        /// END_TURN_REFUSE_MS — scene choice (review 21: a swallowed press showed nothing): a press of the plate or X the mash
        /// window swallows in the player's own wait shakes the plate once over this (nominal ms).
        /// </summary>
        public const float EndTurnRefuseMs = 220f; // js: scenes/battle/layout.js:150
        /// <summary>
        /// FALL_FOOT_MS — battle_ui_ux_v2 §5.15 順 5: an enemy that falls while others stand — the foot light's beat after the
        /// 800 (nominal ms) …
        /// </summary>
        public const float FallFootMs = 300f; // js: scenes/battle/layout.js:155
        /// <summary>FALL_GREY_MS — … and the grey panel's.</summary>
        public const float FallGreyMs = 300f; // js: scenes/battle/layout.js:156
        /// <summary>MOVE_BUMP_MS — battle_ui_ux_v2 §3.14 / §5.7 順 5: a move that went nowhere — the figure leans that way and back over this (nominal ms).</summary>
        public const float MoveBumpMs = 40f; // js: scenes/battle/layout.js:158
        /// <summary>END_TURN_FLASH_MS — battle_ui_ux_v2 §3.5: the end-turn plate brightens once over this (nominal ms) when no card can be played.</summary>
        public const float EndTurnFlashMs = 600f; // js: scenes/battle/layout.js:160
        /// <summary>DOUBLE_CLICK_MS — the OS double-click interval (Windows' default): a press this soon after the click that played a card is its pair (real ms).</summary>
        public const float DoubleClickMs = 500f; // js: scenes/battle/layout.js:162
        /// <summary>HEARTBEAT_HP — sound.md: the heartbeat plays three times at the player's turn start while HP is under 15 (the vignette's line too).</summary>
        public const int HeartbeatHp = 15; // js: scenes/battle/layout.js:164
        /// <summary>VIGNETTE_PULSE_MS — battle_ui_ux_v2 §5.6 順 5: a landed blow under 15 HP pulses the vignette 0.5 → 0.25 over this.</summary>
        public const float VignettePulseMs = 200f; // js: scenes/battle/layout.js:166
        /// <summary>
        /// OPEN_FADE_MS — scene choice (review fix): until the opening frame is on the screen only the layer's backdrop shows
        /// (no half-built HUD while the fonts load); then the rest fades in over this many nominal ms (Ease.Out) …
        /// </summary>
        public const float OpenFadeMs = 240f; // js: scenes/battle/layout.js:171
        /// <summary>OPEN_FONT_WAIT_MS — … and the fonts the opening frame's text needs are waited for at most this many real ms.</summary>
        public const float OpenFontWaitMs = 1200f; // js: scenes/battle/layout.js:173
    }

    /// <summary>The functions layout.js exports beside `L`.</summary>
    public static class BattleLayout
    {
        /// <summary>DepictionFx.NumberFont: the font size of a number at intensity `tier` (1..4; outside clamps, as `tier | 0`).</summary>
        // js: scenes/battle/layout.js:177
        public static float NumberFont(int tier)
        {
            int i = Math.Max(1, Math.Min(4, tier)) - 1;
            return L.NumberSize[i];
        }

        /// <summary>
        /// The design system's hatch angle for enemy hatch `hatch` (EnemyHatch.AngleOf: 45 / −45 / 90). OCB.hatch draws a
        /// vertical bar rotated by its angle, so 90 comes out as horizontal stripes, and 0 falls back to 45 through its
        /// `angle || 45`. battle_ui_ux_v2 §1.4 (「3 体目 縦」) and battle-visual-v1 §4.2 want the third enemy's stripes upright:
        /// 90° is handed to the DS as 180 (the same upright bar). Every hatch of the scene goes through this: the floor's
        /// stripes, the omen badge's, the number panel's and the stance frames' swatches (review fix).
        /// </summary>
        // js: scenes/battle/layout.js:189
        public static int HatchAngle(int hatch)
        {
            int a = EnemyHatch.AngleOf(hatch);
            return a == 90 ? 180 : a;
        }

        /// <summary>
        /// The roster's name of an enemy (UnitFrame.ArtId is its roster id; Enemies.ById(id).Name), or "" when the id is not
        /// on the roster. Scene use only: the pile list's log with two or three enemies and the chain's switch banner
        /// (review 21). A roster lookup of a definition, not a rule (the View rules boundary lists no `Enemies`).
        /// </summary>
        // js: scenes/battle/layout.js:198
        public static string EnemyNameOf(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return "";
            try
            {
                BattleCore.EnemyDef def = BattleCore.Enemies.ById(artId);
                return def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>The enemy's hatch as a glyph (battle-visual-v1 §8 凡例 ／＼; the third enemy's upright stripes ｜): a log line's tag.</summary>
        // js: scenes/battle/layout.js:209
        public static string HatchGlyph(int hatch)
        {
            int a = EnemyHatch.AngleOf(hatch);
            return a == 90 ? "｜" : a < 0 ? "＼" : "／";
        }
    }
}
