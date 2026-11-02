// DepictionPlayer's flow: the end-turn click, the automatic events, one event, one cue, and the debug gates (#585).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Depiction.View
{
    public partial class DepictionPlayer
    {
        /// <summary>
        /// Reads a click on the end-turn plate. The plate is a plain rect in the prefab and the prefab
        /// belongs to the Unity project, not to this repo, so the click is read here instead of being
        /// wired to a Button component that would have to be added there.
        /// </summary>
        private void UpdateEndTurn()
        {
            if (_source == null || _busy || _dragging != null || !endTurn) return;
            if (_halted || !_source.CanEndTurn) return;
            if (!TryPointerPress(out Vector2 pointer)) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(endTurn, pointer, null)) return;
            StartCoroutine(PlayThenContinue(_source.EndTurn()));
        }

        /// <summary>Plays one event the player asked for, then lets the automatic ones follow.</summary>
        private IEnumerator PlayThenContinue(DepictionEvent ev)
        {
            _busy = true;
            SetHandInteractable(false);
            yield return PlayEvent(ev);
            _busy = false;
            yield return UiTween.Wait(EffectFlow.AfterPlayerEventMs);
            yield return RunAutomaticEvents();
        }

        [ContextMenu("Debug Step")]
        public void DebugStep()
        {
            _stepRequested = true;
        }

        // ---- flow -------------------------------------------------------------------------

        private IEnumerator RunAutomaticEvents()
        {
            _busy = true;
            while (!_halted && !_source.Finished && !_source.WaitingForPlayer)
            {
                DepictionEvent ev = _source.AdvanceAuto();
                yield return PlayEvent(ev);
                yield return UiTween.Wait(EffectFlow.AfterAutoEventMs);
            }
            _busy = false;
            if (_halted) yield break; // given up (#203): nothing follows, and no result card
            if (_source.Finished)
            {
                Finished = true;
                RefreshPlayableLook(); // the guide line goes quiet; the outcome is the result card's and the end screen's (#191)
                Debug.Log("[Depiction] finished. seconds per event: " + string.Join(" / ", EventSeconds.ConvertAll(s => s.ToString("0.00"))));
                if (logEffectTrace) Debug.Log("[Depiction] effects:\n" + Trace.ToCsv());
                BattleOutcome outcome = _source.Frame.Outcome;
                if (outcome != BattleOutcome.Ongoing)
                {
                    StartCoroutine(Effect(EffectId.ResultCard, ShowResult(outcome, _effects.Ms(EffectId.ResultCard)), () => ShowResultNow(outcome)));
                }
                ShowSpeedButton(false); // like the demo's 「降参する」, the switch is for the battle only
                BattleFinished?.Invoke();
                yield break;
            }
            SetHandInteractable(true);
            if (autoPlayDrags) StartCoroutine(AutoDrag());
        }

        private IEnumerator PlayEvent(DepictionEvent ev)
        {
            float startedAt = Time.unscaledTime;
            BeginPlan(ev);
            // §4.4 敵の番 (#455): the acting omen's light lasts through the enemy's beats and goes out
            // with the first event that is not one of them (the next omen, the turn start, a fall), or
            // with the beat of another enemy.
            if (ev.Kind != DepictionEventKind.EnemyAction) SetActingExcept(-1);
            else if (SeveralEnemies) SetActingExcept(ev.Unit);
            // §5.5 (#455): the screen comes back up as the event that ends the enemy's turn starts (the
            // turn start's banner), not after it has played; it sinks when the turn end's frame lands.
            if (ev.After != null && !ev.After.EnemyTurn) ShowEnemyTurn(false);
            if (ev.Kind == DepictionEventKind.TurnStart)
            {
                yield return HoldBeat(EffectId.TurnBanner, Banner(DepictionText.YourTurn, BattleTheme.Accent, _effects.Ms(EffectId.TurnBanner)), null);
                // An omen already standing since the enemy's phase blinks once; a new one drops in with its cue.
                for (int k = 0; k < _enemyCount; k++)
                {
                    int unit = k;
                    if (!EnemyScreen.OmenOf(ev.After, k).Visible) continue;
                    if (ev.Cues.Exists(c => c.Kind == CueKind.OmenShow && EnemyScreen.IndexOf(c, _enemyCount) == unit)) continue;
                    StartCoroutine(Effect(EffectId.OmenBlink, _enemies[k].Badge.Blink(_effects.Ms(EffectId.OmenBlink)), null));
                }
            }
            foreach (Cue cue in ev.Cues)
            {
                yield return PlayCue(ev, cue);
            }
            if (ev.Kind == DepictionEventKind.TurnEnd)
            {
                yield return HoldBeat(EffectId.EnemyTurnBanner, Banner(DepictionText.EnemyTurn, BattleTheme.Omen, _effects.Ms(EffectId.EnemyTurnBanner)), null);
            }
            // §4.2 (#242): with a floor, home is the enemy's cell after the event, so the step back
            // below also carries it onto a cell it was moved to.
            if (_floor != null && _floor.Shown && ev.After != null && ev.After.Floor.Cells > 0) SetEnemyHomes(ev.After);
            for (int k = 0; k < _enemyCount; k++)
            {
                EnemyParts foe = _enemies[k];
                if (!foe.Figure) continue;
                // With others standing a fallen enemy's figure is gone, and has nowhere to step back to.
                if (SeveralEnemies && ev.After != null && EnemyScreen.UnitOf(ev.After, k).Down) continue;
                // An enemy whose blow was all taken by Guard (or who only guarded) is still leaning in:
                // it steps back beside the next event instead of holding this one up.
                if (foe.Figure.Rect.anchoredPosition != foe.Home)
                {
                    StartCoroutine(Effect(EffectId.EnemyReturn, ReturnHome(foe.Figure, foe.Home, _effects.Ms(EffectId.EnemyReturn)), () => SnapHome(foe.Figure, foe.Home)));
                }
                // A move that ends where it began (盾 reaches 0) never returns, and a blow Guard took part of
                // (or one a move follows, #499) skips the player's return: neither may keep its action
                // picture into the next event.
                else foe.Figure.SetPose(FigurePose.Idle);
            }
            if (playerFigure) playerFigure.SetPose(FigurePose.Idle);
            EndPlan();
            FadeSpentStripes(ev.After);
            ApplyFrame(ev.After);
            float seconds = Time.unscaledTime - startedAt;
            EventSeconds.Add(seconds);
            LogSettled(ev, seconds);
            yield return Gate(ev.Order + "-end");
        }

        private IEnumerator PlayCue(DepictionEvent ev, Cue cue)
        {
            // #589: an enemy's end of the beat is the enemy the cue names.
            EnemyParts foe = EnemyOf(cue);
            OmenBadgeView badge = foe.Badge;
            FigureView target = cue.Target == UnitSide.Player ? playerFigure : foe.Figure;
            StatusBarView status = cue.Target == UnitSide.Player ? playerStatus : foe.Status;
            Vector2 chest = DepictionFx.PointOn(fxLayer, target.chest);
            Vector2 head = DepictionFx.PointOn(fxLayer, target.head);
            // §5.0: a number's centre goes from the chest to the head of this figure, whatever its height,
            // so it stops under the omen badge. Labels start just under the head.
            Vector2 numberAt = DepictionFx.PointOn(fxLayer, target.transform) + new Vector2(0f, FloatNumberSpec.StartAbove(target.Height));
            float numberRise = FloatNumberSpec.RiseBy(target.Height);
            Vector2 labelAt = head + new Vector2(0f, -30f);
            PlayMark(ev, cue, target, chest, head);

            switch (cue.Kind)
            {
                case CueKind.GuardReset:
                    status.SetGuard(cue.GuardAfter);
                    break;

                case CueKind.StaminaChange:
                    if (cue.Amount > 0) yield return RecoverStamina(cue, status);
                    else SpendStamina(cue, status);
                    break;

                case CueKind.StatusChange:
                    yield return ChangeStatus(cue, status);
                    break;

                case CueKind.DrawHand:
                    // §3.14 (#455): a draw that ran the pile out slides the discard pile onto it mid-draw.
                    yield return DrawHand(ev.After.Hand, cue.Amount, cue.RebuildAfter);
                    break;

                case CueKind.OmenShow:
                    BindBadge(badge, ev.After, EnemyScreen.IndexOf(cue, _enemyCount));
                    DrawOmenStripes(ev.After);
                    yield return HoldBeat(EffectId.OmenShow, badge.PopIn(_effects.Ms(EffectId.OmenShow)), () => badge.Rect.localScale = Vector3.one);
                    break;

                case CueKind.TraitFire:
                    yield return HoldBeat(EffectId.TraitFire, FireTrait(labelAt, cue.Text), null);
                    break;

                case CueKind.Slash:
                    yield return Attack(ev, cue, target, foe.Figure, status, chest);
                    break;

                case CueKind.GuardGain:
                    yield return HoldBeat(EffectId.GuardGain, GainGuard(cue, status, chest, numberAt, numberRise), () => status.SetGuard(cue.GuardAfter));
                    break;

                case CueKind.RangeSwitch:
                    yield return MoveBeat(ev, cue, target, labelAt);
                    break;

                case CueKind.StanceCue:
                    yield return StanceBeat(ev, cue, status, chest, numberAt, numberRise);
                    break;

                case CueKind.DiscardHand:
                    yield return DiscardHand();
                    break;

                case CueKind.EnemyWindup:
                    yield return WindupBeat(cue, target, badge);
                    break;

                case CueKind.Defeat:
                    yield return DefeatBeat(ev, cue, target, status, badge);
                    break;

                case CueKind.SideBonusMiss:
                    yield return HoldBeat(EffectId.SideBonusMiss, StrikeOffBonus(cue, badge), badge.StrikeSideNow);
                    break;

                case CueKind.GuardBlock:
                    yield return GuardBlockBeat(ev, cue, target, status, chest);
                    break;

                case CueKind.HpChange:
                    yield return HpChangeBeat(ev, cue, target, status);
                    break;

                case CueKind.Hit:
                    yield return HitBeat(ev, cue, target, status, foe);
                    break;
            }
        }

        // ---- debug gate ---------------------------------------------------------------------

        private IEnumerator Gate(string gateName)
        {
            bool capture = !string.IsNullOrEmpty(debugCaptureDir);
            if (!debugStepMode && !capture) yield break;
            DebugGate = gateName; // set first: ApplyTweenSpeed leaves the 0 alone while a gate holds
            UiTween.Speed = 0f; // freezes every running tween so the frame can be captured
            if (capture)
            {
                System.IO.Directory.CreateDirectory(debugCaptureDir);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(debugCaptureDir, gateName + ".png"));
                for (int i = 0; i < 3; i++) yield return null;
            }
            _stepRequested = false;
            while (debugStepMode && !_stepRequested) yield return null;
            DebugGate = "";
            ApplyTweenSpeed(); // a speed picked while frozen takes effect here
        }
    }
}
#endif
