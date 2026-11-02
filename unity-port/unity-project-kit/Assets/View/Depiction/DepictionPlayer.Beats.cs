// DepictionPlayer's beats: what one cue draws on the screen (#585).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public partial class DepictionPlayer
    {
        // ---- the longer cues (#585): PlayCue hands each one here --------------------------------------

        /// <summary>
        /// §5.3 (#57): a beat of a play draws its system's shape beside it; a blow draws its own in Attack,
        /// and 息 is the pips lighting, with the chest rising only on a heal.
        /// </summary>
        private void PlayMark(DepictionEvent ev, Cue cue, FigureView target, Vector2 chest, Vector2 head)
        {
            switch (EffectGrammar.MarkOf(cue))
            {
                case FxMark.Shape:
                    // §5.11 順 2 (#501): read before the light below changes. A unit that already keeps a
                    // stance light gets the crest alone, with no second light over its body.
                    bool crestLight = StanceGlow.CrestCarriesLight(target.StanceGlowShown);
                    StartCoroutine(Effect(EffectId.GrammarMark, DepictionFx.Mark(this, fxLayer, chest, head, DepictionFx.PointOn(fxLayer, target.transform),
                        target.side == UnitSide.Player ? 1f : -1f, cue.Fx.Value, cue.Intensity, _effects.Ms(EffectId.GrammarMark), crestLight), null));
                    LightStanceGlowWithCrest(ev, cue, target); // §5.11 順 2 (#501)
                    break;
                case FxMark.ChestHeave:
                    StartCoroutine(Effect(EffectId.BreathHeave, Heave(target, _effects.Ms(EffectId.BreathHeave)), null));
                    break;
            }
        }

        /// <summary>A gain lights its pips one by one and is waited for.</summary>
        private IEnumerator RecoverStamina(Cue cue, StatusBarView status)
        {
            return HoldBeat(EffectId.StaminaRecover,
                status.LightPips(cue.StaminaAfter, cue.StaminaMax, _effects.Ms(EffectId.StaminaRecover)),
                () => status.SetStamina(cue.StaminaAfter, cue.StaminaMax), cue.Amount);
        }

        /// <summary>A spend pops beside the card's own beats.</summary>
        private void SpendStamina(Cue cue, StatusBarView status)
        {
            status.SetStamina(cue.StaminaAfter, cue.StaminaMax);
            if (status.pipRow && _effects.IsOn(EffectId.StaminaChange)) StartCoroutine(UiTween.Pop(status.pipRow, _effects.Ms(EffectId.StaminaChange)));
        }

        private IEnumerator ChangeStatus(Cue cue, StatusBarView status)
        {
            EffectId beat = StatusBeat.Of(cue);
            return HoldBeat(beat, status.PlayStatus(cue.Text, cue.StacksAfter, beat, _effects.Ms(beat)), () => status.SetStatus(cue.Text, cue.StacksAfter));
        }

        private IEnumerator MoveBeat(DepictionEvent ev, Cue cue, FigureView target, Vector2 labelAt)
        {
            // Moved by the other side (the polearm's shove): a knock-back, and the muted label says so.
            if (cue.Pushed) FloatEffect(EffectId.PushMark, labelAt, DepictionText.Pushed, BattleTheme.Ink2, 36);
            // §5.7 順 1〜2 (#586): the walk, 160 a cell, then the gap number counted again on arrival.
            // Every source draws the floor since the near / far slots went (#577).
            int cells = EffectPlan.CellsOf(cue);
            if (!OnFloor(cue))
            {
                // A script bug: the frame shows no floor to walk on. The plan's waits are still held.
                Debug.LogError("[Depiction] event " + ev.Order + " " + ev.Title + ": a move with no floor shown");
                yield return HoldBeat(EffectId.FloorMove, UiTween.Wait(_effects.WaitMs(EffectId.FloorMove, cells)), null, cells);
                yield return HoldBeat(EffectId.GapRecount, UiTween.Wait(_effects.Ms(EffectId.GapRecount)), null);
                yield break;
            }
            yield return HoldBeat(EffectId.FloorMove, MoveOnFloor(target, cue, cells), () => SnapOnFloor(target, cue), cells);
            yield return HoldBeat(EffectId.GapRecount,
                _floor.RecountGap(cue.GapAfter, cue.PlayerCellAfter, PlayerSize, cue.EnemyCellAfter, _effects.Ms(EffectId.GapRecount)),
                () => _floor.SetGap(cue.GapAfter, cue.PlayerCellAfter, PlayerSize, cue.EnemyCellAfter));
        }

        private IEnumerator StanceBeat(DepictionEvent ev, Cue cue, StatusBarView status, Vector2 chest, Vector2 numberAt, float numberRise)
        {
            // §5.13 順 1 (#501): pips 1-3 glow first (構え's 息), then the shield and "+3" (順 2).
            yield return HoldBeat(EffectId.ReservePips, GlowReservePips(cue.Target, _effects.Ms(EffectId.ReservePips)), null);
            yield return HoldBeat(EffectId.StanceCue, Reserve(cue, status, chest, numberAt, numberRise), () => status.SetGuard(cue.GuardAfter));
            yield return Gate(ev.Order + "-impact");
        }

        private IEnumerator WindupBeat(Cue cue, FigureView target, OmenBadgeView badge)
        {
            SetHandInteractable(false);
            // The moment the enemy does what it foretold: the omen lights up (§4.4 敵の番) and
            // flares beside its move.
            badge.SetActing(true);
            StartCoroutine(Effect(EffectId.OmenExecute, badge.Flare(_effects.Ms(EffectId.OmenExecute)), null));
            yield return HoldBeat(EffectId.EnemyMotion, EnemyMotion(target, cue.System, _effects.Ms(EffectId.EnemyMotion)),
                () => target.SetPose(FigurePose.Act));
        }

        private IEnumerator DefeatBeat(DepictionEvent ev, Cue cue, FigureView target, StatusBarView status, OmenBadgeView badge)
        {
            // #288: the fallen enemy's omen goes with it (the frame after the beat keeps it hidden).
            // #349: and so does an elite's plan, which a plain FadeOut leaves up.
            if (cue.Target == UnitSide.Enemy && badge)
            {
                StartCoroutine(Effect(EffectId.OmenSpend, badge.FadeOutAll(_effects.Ms(EffectId.OmenSpend)), badge.HideAllNow));
            }
            // §5.15 順 5 (#589): one enemy falls while others stand. After the fall's 800 the foot
            // light goes out (300) and the panel turns grey (300). The event's plan lists the 800 only:
            // EffectPlan is shared with the trial, and its screen holds these two past the plan as well.
            bool othersStand = cue.Target == UnitSide.Enemy && SeveralEnemies && EnemyScreen.OthersStand(ev.After, EnemyScreen.IndexOf(cue, _enemyCount));
            yield return HoldBeat(EffectId.Defeat, target.Fall(_effects.Ms(EffectId.Defeat), othersStand), () => target.SetDown(true));
            if (!othersStand) yield break;
            if (_effects.IsOn(EffectId.Defeat))
            {
                yield return target.FadeFoot(DefeatShape.FootLightMs);
                yield return status.GreyOut(DefeatShape.PanelGreyMs);
            }
            else status.SetFallen(true);
        }

        private IEnumerator GuardBlockBeat(DepictionEvent ev, Cue cue, FigureView target, StatusBarView status, Vector2 chest)
        {
            // The Guard's number is its own colour, and a light stands in for the sound (#76).
            status.SetGuard(cue.GuardAfter);
            if (cue.GuardAfter == 0) StartCoroutine(Effect(EffectId.GuardBreak, status.CrackGuard(_effects.Ms(EffectId.GuardBreak)), null));
            else if (status.guardBadge) StartCoroutine(UiTween.Pop(status.guardBadge, 260f));
            FloatHit(EffectId.GuardNumber, ev, cue, target, FloatNumberKind.GuardLoss, "");
            yield return HoldBeat(EffectId.GuardBlock, DepictionFx.ShieldBlock(this, fxLayer, chest, cue.Amount.ToString(), () => Gate(ev.Order + "-block")), null);
        }

        private IEnumerator HpChangeBeat(DepictionEvent ev, Cue cue, FigureView target, StatusBarView status)
        {
            // 出血 / 再生 / a heal / a 見切り return (#188): the number with its cause beside it
            // (battle_ui_ux_v2 §5.0 脇ラベル), then the bar.
            FloatHit(EffectId.DamageNumber, ev, cue, target, FloatNumberSpec.OfHpChange(cue.Amount), cue.Text);
            yield return DrainHp(status, cue.HpAfter);
        }

        private IEnumerator HitBeat(DepictionEvent ev, Cue cue, FigureView target, StatusBarView status, EnemyParts foe)
        {
            OmenBadgeView badge = foe.Badge;
            bool strong = EffectStrength.IsStrong(cue.Intensity);
            FloatHit(EffectId.DamageNumber, ev, cue, target, FloatNumberKind.HpLoss, cue.SideLabel);
            StartCoroutine(Effect(EffectId.HitFlash, target.Flash(BattleTheme.Omen, _effects.Ms(EffectId.HitFlash)), null));
            StartCoroutine(Effect(EffectId.TargetRecoil, Recoil(target, strong), null));
            if (strong) StartCoroutine(Effect(EffectId.ScreenShake, ShakeArena(), null));
            yield return DrainHp(status, cue.HpAfter);
            yield return Gate(ev.Order + "-impact");
            // The player's blow Guard took part of, with a move right after it (#499): as in Attack.
            if (cue.Target == UnitSide.Enemy && EffectPlan.MoveFollows(ev, cue) && playerFigure) playerFigure.SetPose(FigurePose.Idle);
            if (cue.Target == UnitSide.Player)
            {
                // The blow that lands on the player is the enemy's own: its omen is spent and
                // it draws back. A blow the player lands does neither.
                yield return HoldBeat(EffectId.OmenSpend, badge.FadeOut(_effects.Ms(EffectId.OmenSpend)), badge.HideNow);
                // §5.4 順 2 (#587): the enemy's own move next (退き撃ち) carries it on from the
                // lunge; the omen fades beside the move (the plan gives OmenSpend no wait), and
                // the figure walks in its standing pose, as the return would have left it.
                if (EffectPlan.EnemyMoveFollows(ev, cue))
                {
                    if (foe.Figure) foe.Figure.SetPose(FigurePose.Idle);
                }
                else
                {
                    yield return HoldBeat(EffectId.EnemyReturn, ReturnHome(foe.Figure, foe.Home, _effects.Ms(EffectId.EnemyReturn)), () => SnapHome(foe.Figure, foe.Home));
                }
            }
        }

        // ---- beats --------------------------------------------------------------------------

        private IEnumerator Attack(DepictionEvent ev, Cue cue, FigureView target, FigureView enemy, StatusBarView status, Vector2 chest)
        {
            // The script must name the attacker. A missing one is a script bug: say so, then fall back
            // to the opposite of the target so the beat still plays (EffectPlan.AttackerOf, which the plan reads too).
            if (!cue.Source.HasValue) Debug.LogError("[Depiction] event " + ev.Order + ": Slash cue has no Source");
            UnitSide source = EffectPlan.AttackerOf(cue);
            bool byPlayer = source == UnitSide.Player;
            bool strong = EffectStrength.IsStrong(cue.Intensity);
            FigureView attacker = byPlayer ? playerFigure : enemy;
            Vector2 attackerHome = attacker.Rect.anchoredPosition;
            // #288: both sides step in before the blow. The enemy draws back with EnemyReturn once the
            // blow has landed, or beside the next event when Guard took all of it.
            EffectId lunge = byPlayer ? EffectId.AttackLunge : EffectId.EnemyLunge;
            yield return HoldBeat(lunge, Lunge(attacker, 46f, _effects.Ms(lunge)), () => attacker.SetPose(FigurePose.Act));
            Color streak = byPlayer ? BattleTheme.Ink : BattleTheme.Omen;
            // §5.9 J2 (#57): a fired trait lays its short cue over the blow; the tier stays as written.
            if (cue.TraitAccent) StartCoroutine(Effect(EffectId.TraitAccent, DepictionFx.TraitAccent(this, fxLayer, chest, _effects.Ms(EffectId.TraitAccent)), null));
            yield return HoldBeat(EffectId.StrikeShape,
                DepictionFx.Strike(this, fxLayer, chest, streak, cue.Intensity, !byPlayer, cue.System, _effects.Ms(EffectId.StrikeShape)), null);
            EffectId stop = strong ? EffectId.HitStopStrong : EffectId.HitStop;
            yield return HoldBeat(stop, UiTween.Wait(_effects.Ms(stop)), null);
            if (strong) StartCoroutine(Effect(EffectId.ScreenShake, ShakeArena(), null));

            // A slash with no settled HP is the swing alone: the GuardBlock / Hit cues that follow
            // carry the numbers. That is every enemy slash, and any slash Guard takes a bite out of.
            if (cue.HpAfter == Cue.Unchanged) yield break;

            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Omen, 200f + 60f * cue.Intensity);
            FloatHit(EffectId.DamageNumber, ev, cue, target, FloatNumberKind.HpLoss, cue.SideLabel);
            StartCoroutine(Effect(EffectId.HitFlash, target.Flash(BattleTheme.White, _effects.Ms(EffectId.HitFlash)), null));
            StartCoroutine(Effect(EffectId.TargetRecoil, Recoil(target, strong), null));
            yield return DrainHp(status, cue.HpAfter);
            yield return Gate(ev.Order + "-impact");
            // §5.4 順 2 / §5.5 順 8 (#499): a move right after the blow carries the player on from the
            // lunge (MoveOnFloor starts where the figure stands), so there is no step back first; the
            // figure walks in its standing pose, as the return would have left it.
            if (byPlayer && !EffectPlan.MoveFollows(ev, cue))
            {
                yield return HoldBeat(EffectId.AttackReturn, ReturnHome(attacker, attackerHome, _effects.Ms(EffectId.AttackReturn)), () => SnapHome(attacker, attackerHome));
            }
            else if (byPlayer) attacker.SetPose(FigurePose.Idle);
        }

        // ---- attack and defence beats (#76) -----------------------------------------------------

        /// <summary>
        /// The bar drops (blocking), then the grey band follows it down beside the flow. A move right
        /// after the blow starts 80 ms into the drop (EffectPlan.MoveFollows, #499); the band still
        /// waits for the bar to land, so it follows the settled width.
        /// </summary>
        private IEnumerator DrainHp(StatusBarView status, int hp)
        {
            yield return HoldBeat(EffectId.HpDrain, status.AnimateHp(hp, _effects.Ms(EffectId.HpDrain)), () => status.SetHpKeepingTrail(hp),
                played: () => StartCoroutine(Effect(EffectId.HpTrail, status.AnimateTrail(_effects.Ms(EffectId.HpTrail)), status.SnapTrail)));
        }

        /// <summary>The struck figure leans away from the blow and shakes; a strong blow does both harder.</summary>
        private IEnumerator Recoil(FigureView figure, bool strong)
        {
            float ms = _effects.Ms(EffectId.TargetRecoil);
            RectTransform body = figure.body ? figure.body.rectTransform : null;
            // The player faces right and reels to the left; the enemy the other way round.
            float lean = (strong ? 12f : 7f) * (figure.side == UnitSide.Player ? 1f : -1f);
            figure.SetPose(FigurePose.Hit);
            StartCoroutine(UiTween.Shake(figure.Rect, strong ? 20f : 10f, 3, ms));
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (body) body.localRotation = Quaternion.Euler(0f, 0f, lean * Mathf.Sin(t * Mathf.PI));
            });
            if (body) body.localRotation = Quaternion.identity;
            if (figure) figure.SetPose(FigurePose.Idle);
        }

        /// <summary>§5.3 息 on a heal (#57): the body rises a little and settles once, beside the HP beat.</summary>
        private IEnumerator Heave(FigureView figure, float ms)
        {
            RectTransform body = figure.body ? figure.body.rectTransform : null;
            if (!body) yield break;
            Vector3 home = figure.BodyHomeScale;
            yield return UiTween.Run(ms, Ease.InOut, t =>
            {
                if (body) body.localScale = new Vector3(home.x, home.y * (1f + 0.05f * Mathf.Sin(t * Mathf.PI)), home.z);
            });
            if (body) body.localScale = home;
        }

        /// <summary>A strong blow shakes the whole arena (the parent of the figures).</summary>
        private IEnumerator ShakeArena()
        {
            RectTransform arena = playerFigure ? playerFigure.Rect.parent as RectTransform : null;
            if (!arena) yield break;
            yield return UiTween.Shake(arena, 6f, 3, _effects.Ms(EffectId.ScreenShake));
        }

        /// <summary>
        /// The enemy moves into its action, shaped by its system so the slice's four actions read apart
        /// on one picture: 払 swings across, 突 drives straight in, 打 shoves with its weight, 盾 raises
        /// the haft; a step just steps (EnemyMotionShape). The stretch is taken on the body's scale at
        /// rest, so a mirrored silhouette stays facing the player (#288). The lean stays until the enemy returns.
        /// </summary>
        private IEnumerator EnemyMotion(FigureView figure, StrikeSystem system, float ms)
        {
            RectTransform body = figure.body ? figure.body.rectTransform : null;
            float toward = figure.side == UnitSide.Player ? 1f : -1f;
            Vector2 from = figure.Rect.anchoredPosition;
            float reach = EnemyMotionShape.Reach(system);
            float homeX = figure.BodyHomeScale.x;
            figure.SetPose(FigurePose.Act);
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!figure) return;
                float bell = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
                float rise = system == StrikeSystem.Shield ? 16f * bell : 0f;
                figure.Rect.anchoredPosition = from + new Vector2(reach * toward * t, rise);
                if (!body) return;
                (float x, float y) = FigureFacing.During(homeX, system, bell);
                body.localScale = new Vector3(x, y, 1f);
                switch (system)
                {
                    case StrikeSystem.Sweep: // wound back, then across
                        body.localRotation = Quaternion.Euler(0f, 0f, -toward * Mathf.Lerp(-18f, 14f, t) * bell);
                        break;
                    case StrikeSystem.Strike: // leaning its weight into the push
                        body.localRotation = Quaternion.Euler(0f, 0f, -toward * 10f * bell);
                        break;
                }
            });
            if (body)
            {
                body.localRotation = Quaternion.identity;
                body.localScale = figure.BodyHomeScale;
            }
        }

        private static IEnumerator ReturnHome(FigureView figure, Vector2 home, float ms)
        {
            if (!figure) yield break;
            yield return UiTween.Move(figure.Rect, figure.Rect.anchoredPosition, home, ms, Ease.InOut);
            if (figure) figure.SetPose(FigurePose.Idle);
        }

        private static void SnapHome(FigureView figure, Vector2 home)
        {
            if (!figure) return;
            figure.Rect.anchoredPosition = home;
            figure.SetPose(FigurePose.Idle);
        }

        private IEnumerator GainGuard(Cue cue, StatusBarView status, Vector2 chest, Vector2 numberAt, float numberRise)
        {
            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Guard, 220f + 40f * cue.Intensity);
            DepictionFx.FloatText(this, fxLayer, numberAt, "+" + cue.Amount, BattleTheme.Guard, FloatNumberSpec.GainFontPx(cue.Intensity), rise: numberRise);
            yield return status.PopGuard(cue.GuardAfter, _effects.Ms(EffectId.GuardGain));
        }

        private IEnumerator Reserve(Cue cue, StatusBarView status, Vector2 chest, Vector2 numberAt, float numberRise)
        {
            if (endTurn) yield return UiTween.Pop(endTurn, 200f);
            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Guard, 300f);
            DepictionFx.FloatText(this, fxLayer, numberAt, cue.Text, BattleTheme.Guard, 48, rise: numberRise);
            yield return status.PopGuard(cue.GuardAfter);
        }

        /// <summary>
        /// §5.13 順 1 (#501): the player's pips 1-3 on the stamina tag glow. An enemy's stamina is a number
        /// on its panel with no pips (§4.5), and its 構え plays as a GuardGain, so for it the beat only holds.
        /// </summary>
        private IEnumerator GlowReservePips(UnitSide side, float ms)
        {
            if (side == UnitSide.Player && _staminaTag != null) yield return _staminaTag.GlowReserve(ms);
            else yield return UiTween.Wait(ms);
        }

        // ---- §5.11 順 2 (#501): the light a unit keeps while it holds a stance -------------------

        /// <summary>
        /// Brings each figure's stance light to what <paramref name="frame"/> says: on while the unit's
        /// stance list holds one (StanceGlow), faded in or out beside the flow. A light already lit with
        /// the 型 crest (<see cref="LightStanceGlowWithCrest"/>) is kept.
        /// </summary>
        private void ApplyStanceGlows(DepictionFrame frame)
        {
            ApplyStanceGlow(playerFigure, frame.Player);
            for (int k = 0; k < _enemyCount; k++) ApplyStanceGlow(_enemies[k].Figure, EnemyScreen.UnitOf(frame, k));
        }

        private void ApplyStanceGlow(FigureView figure, UnitFrame unit)
        {
            if (!figure) return;
            StanceGlowChange change = StanceGlow.Change(figure.StanceGlowShown, unit);
            if (change == StanceGlowChange.Keep) return;
            bool on = change == StanceGlowChange.Light;
            StartCoroutine(Effect(EffectId.StanceGlow, figure.FadeStanceGlow(on, _effects.Ms(EffectId.StanceGlow)), () => figure.SetStanceGlow(on)));
        }

        /// <summary>A new battle: each light is as the first frame says at once, with no fade.</summary>
        private void SnapStanceGlows(DepictionFrame frame)
        {
            if (playerFigure) playerFigure.SetStanceGlow(StanceGlow.IsOn(frame.Player));
            for (int k = 0; k < _enemyCount; k++)
            {
                if (_enemies[k].Figure) _enemies[k].Figure.SetStanceGlow(StanceGlow.IsOn(EnemyScreen.UnitOf(frame, k)));
            }
        }

        /// <summary>
        /// §5.11 順 2: the light opens with the 型 crest (足元に紋、身体に薄い光), so a 型 beat on a unit whose
        /// list holds a stance once the event has played lights it there, not when the frame lands.
        /// </summary>
        private void LightStanceGlowWithCrest(DepictionEvent ev, Cue cue, FigureView target)
        {
            if (!target || cue.Fx != FxSystem.Form || ev.After == null) return;
            UnitFrame unit = cue.Target == UnitSide.Player ? ev.After.Player : EnemyScreen.UnitOf(ev.After, EnemyScreen.IndexOf(cue, _enemyCount));
            ApplyStanceGlow(target, unit);
        }

        private IEnumerator FireTrait(Vector2 labelAt, string text)
        {
            DepictionFx.Burst(this, fxLayer, labelAt, BattleTheme.Warm, 260f);
            DepictionFx.FloatText(this, fxLayer, labelAt, text, BattleTheme.Warm, 40, rise: 70f);
            yield return UiTween.Wait(_effects.Ms(EffectId.TraitFire));
        }

        private IEnumerator StrikeOffBonus(Cue cue, OmenBadgeView badge)
        {
            DepictionFx.FloatText(this, fxLayer, DepictionFx.PointOn(fxLayer, badge.transform) + new Vector2(0f, -64f),
                cue.Text, BattleTheme.Whiff, 40, struck: true, rise: -50f);
            yield return badge.StrikeSide(_effects.Ms(EffectId.SideBonusMiss));
        }

        // ---- status and turn beats (#77) -------------------------------------------------------

        /// <summary>A band across the arena with one line on it, faded in and out over <paramref name="ms"/>.</summary>
        private IEnumerator Banner(string text, Color color, float ms)
        {
            if (!fxLayer) yield break;
            Image plate = UiKit.Sprite(fxLayer, "Banner", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.55f),
                Half, Half, new Vector2(1920f, 120f), new Vector2(0f, 80f));
            plate.raycastTarget = false;
            Text label = UiKit.Text(plate.rectTransform, "Text", 64, TextAnchor.MiddleCenter, color, text);
            BattleFonts.Use(label, BattleFonts.Banner);
            label.raycastTarget = false;
            CanvasGroup group = UiKit.Group(plate.rectTransform);
            group.alpha = 0f;
            yield return UiTween.Run(ms, Ease.Linear, t =>
            {
                if (group) group.alpha = t < 0.25f ? t / 0.25f : t > 0.75f ? (1f - t) / 0.25f : 1f;
            });
            if (plate) Destroy(plate.gameObject);
        }

        /// <summary>The one card at the end: 勝ち or 負け rises into place and stays (EffectId.ResultCard).</summary>
        private IEnumerator ShowResult(BattleOutcome outcome, float ms)
        {
            CanvasGroup group = BuildResultCard(outcome);
            if (!group) yield break;
            var rt = (RectTransform)group.transform;
            Vector2 at = rt.anchoredPosition;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!rt) return;
                group.alpha = t;
                rt.anchoredPosition = at + new Vector2(0f, -40f * (1f - t));
            });
        }

        private void ShowResultNow(BattleOutcome outcome)
        {
            CanvasGroup group = BuildResultCard(outcome);
            if (group) group.alpha = 1f;
        }

        private CanvasGroup BuildResultCard(BattleOutcome outcome)
        {
            if (!fxLayer || _resultCard) return null;
            Image plate = UiKit.Sprite(fxLayer, "ResultCard", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.8f),
                Half, Half, new Vector2(520f, 220f), new Vector2(0f, 60f));
            plate.raycastTarget = false;
            Color color = outcome == BattleOutcome.Won ? BattleTheme.Warm : BattleTheme.Omen;
            Text label = UiKit.Text(plate.rectTransform, "Outcome", 72, TextAnchor.MiddleCenter, color, DepictionText.OutcomeText(outcome));
            BattleFonts.Use(label, BattleFonts.Banner);
            label.raycastTarget = false;
            _resultCard = plate.gameObject;
            CanvasGroup group = UiKit.Group(plate.rectTransform);
            group.alpha = 0f;
            return group;
        }

        /// <summary>
        /// A rising number, as an effect: it runs on its own for its length, and the trace notes it,
        /// or notes that it was switched off.
        /// </summary>
        private void FloatEffect(EffectId id, Vector2 at, string text, Color color, int fontSize)
        {
            if (!_effects.IsOn(id))
            {
                Trace?.Skip(id, CurrentOrder);
                return;
            }
            if (Trace != null) StartCoroutine(TraceFor(id, _effects.Ms(id)));
            DepictionFx.FloatText(this, fxLayer, at, text, color, fontSize);
        }

        /// <summary>
        /// battle-visual-v1 §5.5 / battle_ui_ux_v2 §5.0, §5.6 順 2〜3 (#455): a hit's number on its plate,
        /// rising from the chest to the head of <paramref name="target"/>. The amount is the cue's; the
        /// pair (Guard left, HP right, 24 px apart) is read off the order the script wrote the cues in.
        /// </summary>
        private void FloatHit(EffectId id, DepictionEvent ev, Cue cue, FigureView target, FloatNumberKind kind, string label)
        {
            if (!_effects.IsOn(id))
            {
                Trace?.Skip(id, CurrentOrder);
                return;
            }
            if (Trace != null) StartCoroutine(TraceFor(id, _effects.Ms(id)));
            int index = ev.Cues.IndexOf(cue);
            bool paired = kind == FloatNumberKind.GuardLoss
                ? FloatNumberSpec.GuardPairsWithHp(ev.Cues, index)
                : FloatNumberSpec.HpPairsWithGuard(ev.Cues, index);
            DepictionFx.FloatPlate(this, fxLayer, DepictionFx.PointOn(fxLayer, target.transform), target.Height, paired,
                kind, cue.Amount, label, _effects.Ms(id));
        }

        private IEnumerator TraceFor(EffectId id, float ms)
        {
            int handle = Trace.Begin(id, CurrentOrder);
            yield return UiTween.Wait(ms);
            Trace.End(handle);
        }

        /// <summary>Steps toward the opponent by <paramref name="pixels"/> (negative steps back).</summary>
        private IEnumerator Lunge(FigureView figure, float pixels, float ms)
        {
            float direction = figure.side == UnitSide.Player ? 1f : -1f;
            Vector2 from = figure.Rect.anchoredPosition;
            Vector2 to = from + new Vector2(pixels * direction, 0f);
            figure.SetPose(FigurePose.Act);
            yield return UiTween.Move(figure.Rect, from, to, ms, Ease.Out);
        }

        /// <summary>
        /// The walk's end state at once, on the floor: both figures on their cells. The gap number is
        /// the recount's to settle (EffectId.GapRecount, #586).
        /// </summary>
        private void SnapOnFloor(FigureView figure, Cue cue)
        {
            figure.transform.position = _floor.FeetOf(cue.PlayerCellAfter, PlayerSize);
            SetPlayerHome(figure);
            int moved = EnemyScreen.MovedUnit(cue, _enemyCount, out int cell);
            if (moved < 0) return;
            EnemyParts foe = EnemyAt(moved);
            if (!foe.Figure) return;
            foe.Home = AnchoredAt(foe.Figure.Rect, _floor.FeetOf(cell, EnemySizeOf(moved)));
            SnapHome(foe.Figure, foe.Home);
        }

        /// <summary>
        /// The move is drawn on the floor: one is shown and the cue says where both stand after it
        /// (EffectPlan.MovesOnFloor). Every source draws the floor (#577), so this only fails on a script bug.
        /// </summary>
        private bool OnFloor(Cue cue)
        {
            return cue != null && _floor != null && _floor.Shown && EffectPlan.MovesOnFloor(cue);
        }

        private int PlayerSize => _source != null && _source.Frame != null ? Mathf.Max(1, _source.Frame.Floor.PlayerSize) : 1;

        /// <summary>How many cells enemy <paramref name="k"/> takes (the script's), at least one.</summary>
        private int EnemySizeOf(int k) => FrameNow != null ? EnemyScreen.SizeOf(FrameNow, k) : 1;

        /// <summary>
        /// §4.2 (#242) / §5.7 順 1 (#586): a move on the floor — the player walks to its new cell, the
        /// enemy that moved (a push, a pull, its own step; EnemyScreen.MovedUnit) slides to its own, 160 ms
        /// a cell (EffectId.FloorMove). The gap number is counted again after it (EffectId.GapRecount).
        /// </summary>
        private IEnumerator MoveOnFloor(FigureView figure, Cue cue, int cells)
        {
            Vector3 from = figure.transform.position;
            Vector3 to = _floor.FeetOf(cue.PlayerCellAfter, PlayerSize);
            int moved = EnemyScreen.MovedUnit(cue, _enemyCount, out int cell);
            EnemyParts foe = moved >= 0 ? EnemyAt(moved) : null;
            FigureView walker = foe != null ? foe.Figure : null;
            Vector2 enemyFrom = walker ? walker.Rect.anchoredPosition : Vector2.zero;
            Vector2 enemyTo = walker ? AnchoredAt(walker.Rect, _floor.FeetOf(cell, EnemySizeOf(moved))) : enemyFrom;
            bool enemyMoves = walker && (enemyTo - foe.Home).sqrMagnitude > 0.25f;
            // §5.4 順 2 (#587): the enemy's own move after its blow starts 80 ms after the number, while
            // the struck player still reels (TargetRecoil); a player whose cell stays is left to it.
            bool playerStays = enemyMoves && !cue.Pushed && _playerHomeSet && to == _playerHome;
            if (enemyMoves) foe.Home = enemyTo;
            SetPlayerHome(figure, to); // the panels set off for the new cells as the figures do
            // 160 ms a cell (EffectSpec.TotalMs of FloorMove); a push starts fast and settles.
            yield return UiTween.Run(_effects.WaitMs(EffectId.FloorMove, cells), cue.Pushed ? Ease.Out : Ease.InOut, t =>
            {
                if (figure && !playerStays) figure.transform.position = Vector3.LerpUnclamped(from, to, t);
                if (enemyMoves && walker) walker.Rect.anchoredPosition = Vector2.LerpUnclamped(enemyFrom, enemyTo, t);
            });
        }
    }
}
#endif
