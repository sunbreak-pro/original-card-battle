// DepictionPlayer's drag: holding a card, the targets it lights, releasing it, and the unattended drags (#585).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Depiction.Bridge;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Depiction.View
{
    public partial class DepictionPlayer
    {
        // ---- drag ---------------------------------------------------------------------------

        // The EventSystem hands the drag to whatever it hits, but a lifted card leaves the strip at the
        // bottom of its resting place where the right neighbour is hit instead. The hover decides which
        // card is picked up, so the card that looks lifted is the one that moves; the move and end events
        // then follow the held card, whichever card the EventSystem keeps reporting.
        private CardView _dragSource;

        private void OnDragBegan(CardView card, PointerEventData e)
        {
            if (_busy || _dragging != null) return;
            CardView held = card;
            if (_hovered && _hovered != card && _hovered.Interactable
                && HoveredRectContains(_hovered, _hand.IndexOf(_hovered), e.position)) held = _hovered;
            _dragSource = card;
            BeginHold(held);
            _dragPointerWorld = PointerWorld(e);
            _grabOffset = held.transform.position - _dragPointerWorld;
        }

        private void OnDragMoved(CardView card, PointerEventData e)
        {
            if (_dragging == null || card != _dragSource) return;
            _dragPointerWorld = PointerWorld(e);
            UpdateHot(_dragging, e.position);
            _dragging.transform.position = DragPosition();
        }

        private void OnDragEnded(CardView card, PointerEventData e)
        {
            if (_dragging == null || card != _dragSource) return;
            _dragSource = null;
            _dragPointerWorld = PointerWorld(e);
            CardView held = _dragging;
            Release(held, ZoneAt(held, e.position));
        }

        private void BeginHold(CardView card)
        {
            _dragging = card;
            _pickedUnit = -1;
            _aimedUnit = card.Face.TargetUnit;
            // The card may be lifted by the hover; it must come back to its resting place, not to there.
            Vector3 held = card.transform.position;
            PlaceAtHome(card, _hand.IndexOf(card));
            _dragHome = card.transform.position;
            card.transform.position = held;
            // The hover stops updating while a card is held, so settle any card it left half lifted.
            foreach (CardView other in _hand)
            {
                if (other && other != card) PlaceAtHome(other, _hand.IndexOf(other));
            }
            _hovered = null;
            _hoverWeight.Clear();
            card.transform.SetAsLastSibling();
            card.group.alpha = 0.92f;
            HideHoverPanels();
            // §4.7 / §5.2: the stamina it would pay turns to rings, and "▸ n" shows.
            _staminaTag?.ShowCost(HeldCost(card));
            ShowReserveHint(card); // §4.9 / §5.3 (#455): the 構えの予告 after this card is paid for
            ShowHpLoss(card); // §4.5 / §5.2 (#455): the wick stripe on the enemy's HP bar
            // §6.3: a card aimed at one enemy is judged again on the enemy it frames. The verdict is the
            // script's (CardFace.LitFor); the View only shows it.
            if (card.Face.Aim == CardAim.Single) card.ShowLamp(card.Face.LitFor(EnemyScreen.FramedUnit(card.Face, _pickedUnit)));
            // EffectId.CardGrab: a held card stands upright. §0.1 decision 7: its size and shadow stay.
            Quaternion tilt = card.Rect.localRotation;
            Vector3 scaleFrom = card.Rect.localScale;
            Vector3 heldScale = Vector3.one;
            StartCoroutine(Effect(EffectId.CardGrab,
                UiTween.Run(_effects.Ms(EffectId.CardGrab), Ease.Out, t =>
                {
                    if (!card || _dragging != card) return;
                    card.Rect.localRotation = Quaternion.Slerp(tilt, Quaternion.identity, t);
                    card.Rect.localScale = Vector3.LerpUnclamped(scaleFrom, heldScale, t);
                }),
                () =>
                {
                    card.Rect.localRotation = Quaternion.identity;
                    card.Rect.localScale = heldScale;
                }));
            // The zone takes the drop from the first frame; the fade only changes how it looks.
            if (_zoneFade != null) StopCoroutine(_zoneFade);
            if (card.Face.Aim == CardAim.Single)
            {
                KeepReceiverOnAimed();
                receiver.Show(PreviewFor(card));
                _zoneFade = StartCoroutine(Effect(EffectId.ReceiverShow, receiver.FadeIn(_effects.Ms(EffectId.ReceiverShow)), null));
            }
            else
            {
                throwLine.Show(PreviewFor(card));
                _zoneFade = StartCoroutine(Effect(EffectId.ThrowLineShow, throwLine.FadeIn(_effects.Ms(EffectId.ThrowLineShow)), null));
            }
            ShowTargetMark(card);
        }

        /// <summary>
        /// battle-visual-v1 §5.2 / §5.3 (#242): the four hooks around the figure the card lands on, with
        /// the one predicted value on the top edge. A card aimed at one enemy frames the enemy (steel and
        /// no number when the script says nobody is in reach: CardFace.ReachHint); a self card frames
        /// the figure the script names (CardFace.Affects). With several enemies a card aimed at one frames
        /// every standing one (<see cref="ShowTargetMarks"/>), and the one it lands on without aiming is
        /// the focus enemy.
        /// </summary>
        private void ShowTargetMark(CardView card)
        {
            if (card.Face.Aim == CardAim.Single && SeveralEnemies)
            {
                ShowTargetMarks(card);
                return;
            }
            FigureView figure;
            bool reaches = true;
            if (card.Face.Aim == CardAim.Single)
            {
                figure = enemyFigure;
                reaches = EnemyScreen.Reaches(card.Face, 0, _enemyCount);
            }
            else if (!card.Face.Affects.HasValue)
            {
                Debug.LogError("[Depiction] card " + card.CardId + " is a throw-line card but the script gave no Affects");
                return;
            }
            else figure = card.Face.Affects.Value == UnitSide.Player ? playerFigure : EnemyAt(EnemyScreen.FocusOf(_source.Frame)).Figure;
            if (!figure || !figure.targetMark)
            {
                Debug.LogError("[Depiction] " + (figure ? figure.name : "a figure") + " has no targetMark; run Tools > Depiction > Upgrade Prefabs");
                return;
            }
            CardValueKind kind = ValueKindOf(card);
            figure.targetMark.Show(reaches, BattleTheme.Wick, PreviewFor(card), VisualArt.ValueIcon(kind), VisualArt.ValueColor(kind));
        }

        private static CardValueKind ValueKindOf(CardView card)
        {
            return card.Face.Values != null && card.Face.Values.Count > 0
                ? card.Face.Values[0].Kind
                : card.Face.Kind == CardKind.Guard ? CardValueKind.Guard : CardValueKind.Power;
        }

        /// <summary>
        /// battle_ui_ux_v2 §2.2 の 5 / §3.9 (#589): every standing enemy's figure gets its frame. One in the
        /// card's reach shows the value on that enemy (the source's, read per enemy); one out of it shows
        /// steel hooks and no number (CardFace.ReachOn). A fallen enemy shows none: its figure is gone.
        /// </summary>
        private void ShowTargetMarks(CardView card)
        {
            CardValueKind kind = ValueKindOf(card);
            for (int k = 0; k < _enemyCount; k++)
            {
                TargetMarkView mark = _enemies[k].Figure ? _enemies[k].Figure.targetMark : null;
                if (!mark)
                {
                    Debug.LogError("[Depiction] enemy " + k + "'s figure has no targetMark; run Tools > Depiction > Upgrade Prefabs");
                    continue;
                }
                if (!EnemyScreen.Stands(_source.Frame, k))
                {
                    mark.Hide();
                    continue;
                }
                bool reaches = EnemyScreen.Reaches(card.Face, k, _enemyCount);
                mark.Show(reaches, BattleTheme.Wick, reaches ? PreviewOn(card, k) : "", VisualArt.ValueIcon(kind), VisualArt.ValueColor(kind));
            }
        }

        private void HideTargetMarks()
        {
            if (playerFigure && playerFigure.targetMark) playerFigure.targetMark.Hide();
            foreach (EnemyParts foe in _enemies)
            {
                if (foe.Figure && foe.Figure.targetMark) foe.Figure.targetMark.Hide();
            }
        }

        private void SetZoneHot(CardView card, bool hot)
        {
            if (card.Face.Aim == CardAim.Single)
            {
                receiver.SetHot(hot);
                // §5.15 順 1: only the enemy the card is over answers.
                int framed = EnemyScreen.FramedUnit(card.Face, _pickedUnit);
                for (int k = 0; k < _enemyCount; k++)
                {
                    TargetMarkView mark = _enemies[k].Figure ? _enemies[k].Figure.targetMark : null;
                    if (mark) mark.SetHot(hot && (!SeveralEnemies || k == framed));
                }
                return;
            }
            throwLine.SetHot(hot);
            if (playerFigure.targetMark) playerFigure.targetMark.SetHot(hot);
            for (int k = 0; k < _enemyCount; k++)
            {
                TargetMarkView mark = _enemies[k].Figure ? _enemies[k].Figure.targetMark : null;
                if (mark) mark.SetHot(hot);
            }
        }

        /// <summary>The stamina a held card would pay: on the enemy it frames, with several (CardFace.CostFor).</summary>
        private int HeldCost(CardView card)
        {
            return SeveralEnemies ? card.Face.CostFor(EnemyScreen.FramedUnit(card.Face, _pickedUnit)) : card.Face.Cost;
        }

        /// <summary>
        /// §6.3 (#589): the held card has come over another enemy, or off the last one. The numbers read on the
        /// enemy it frames follow: the lamp, the stamina it would pay and the 構えの予告.
        /// </summary>
        private void PickEnemy(CardView card, int unit)
        {
            if (unit == _pickedUnit) return;
            _pickedUnit = unit;
            if (unit >= 0) _aimedUnit = unit;
            card.ShowLamp(card.Face.LitFor(EnemyScreen.FramedUnit(card.Face, unit)));
            _staminaTag?.ShowCost(HeldCost(card));
            ShowReserveHint(card);
        }

        /// <summary>The release zone goes over the enemy the held card is aimed at at once, for what reads its place this frame.</summary>
        private void KeepReceiverOnAimed()
        {
            RectTransform root = RefScreen.Root(receiver.transform);
            if (SeveralEnemies && root) KeepX(receiver.Rect, root, EnemyHomeOnScreen(root, ReceiverEnemy()).x);
        }

        /// <summary>The one predicted value comes from the source; a card it would refuse shows none.</summary>
        private string PreviewFor(CardView card)
        {
            return _source == null ? "" : _source.PreviewFor(card.CardId);
        }

        /// <summary>The source that answers for a named enemy (CoreBattleSource), or null for one that keeps one enemy.</summary>
        private ITrialSource TargetedSource => _source as ITrialSource;

        /// <summary>The predicted value read on enemy <paramref name="unit"/>.</summary>
        private string PreviewOn(CardView card, int unit)
        {
            return TargetedSource != null ? TargetedSource.PreviewFor(card.CardId, unit) : PreviewFor(card);
        }

        private DropZone ZoneAt(CardView card, Vector2 screenPoint)
        {
            if (card.Face.Aim == CardAim.Single) return ReceiverTakes(card, screenPoint) ? DropZone.Receiver : DropZone.None;
            return throwLine.IsAbove(screenPoint, null) ? DropZone.AboveThrowLine : DropZone.None;
        }

        /// <summary>
        /// #568: the receiver takes a single-target card when half of the card lies on it or the pointer
        /// is inside it (ReceiverDrop.Takes). The card is measured where the pointer carries it, before
        /// the snap draws it in, so the pull cannot hold a card that has already left.
        /// </summary>
        private bool ReceiverTakes(CardView card, Vector2 screenPoint)
        {
            RectTransform root = RefScreen.Root(receiver.transform);
            if (!root) return receiver.Contains(screenPoint, null);
            ScreenBox dish = BoxOn(root, receiver.Rect, Vector3.zero);
            ScreenBox held = BoxOn(root, card.Rect, _dragPointerWorld + _grabOffset - card.transform.position);
            Vector2 pointer = RefScreen.Of(root, _dragPointerWorld);
            if (!SeveralEnemies) return ReceiverDrop.Takes(held, dish, pointer.x, pointer.y);
            // §3.9 (#589): one receiver per standing enemy, as tall as the scene's and over that enemy's home.
            var units = new List<int>();
            var dishes = new List<ScreenBox>();
            for (int k = 0; k < _enemyCount; k++)
            {
                if (!EnemyScreen.Stands(_source.Frame, k)) continue;
                units.Add(k);
                dishes.Add(new ScreenBox(EnemyHomeOnScreen(root, _enemies[k]).x - dish.Width * 0.5f, dish.Y, dish.Width, dish.Height));
            }
            int picked = ReceiverDrop.Pick(held, dishes, pointer.x, pointer.y);
            PickEnemy(card, picked >= 0 ? units[picked] : -1);
            return picked >= 0;
        }

        /// <summary>The box <paramref name="rt"/> covers on the reference screen once moved by <paramref name="shift"/> (world units); a tilted card counts its whole outline.</summary>
        private static ScreenBox BoxOn(RectTransform root, RectTransform rt, Vector3 shift)
        {
            rt.GetWorldCorners(Corners);
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            foreach (Vector3 corner in Corners)
            {
                Vector2 at = RefScreen.Of(root, corner + shift);
                left = Mathf.Min(left, at.x);
                right = Mathf.Max(right, at.x);
                top = Mathf.Min(top, at.y);
                bottom = Mathf.Max(bottom, at.y);
            }
            return new ScreenBox(left, top, right - left, bottom - top);
        }

        private void UpdateHot(CardView card, Vector2 screenPoint)
        {
            _zoneHot = ZoneAt(card, screenPoint) != DropZone.None;
            SetZoneHot(card, _zoneHot);
        }

        /// <summary>Where the held card sits: under the pointer, drawn toward the dish by the snap weight.</summary>
        private Vector3 DragPosition()
        {
            Vector3 free = _dragPointerWorld + _grabOffset;
            if (_snapWeight <= 0f || !receiver) return free;
            float eased = _snapWeight * _snapWeight * (3f - 2f * _snapWeight);
            return Vector3.Lerp(free, receiver.transform.position, SnapPull * eased);
        }

        /// <summary>
        /// EffectId.ReceiverSnap: while a single-target card is over the dish, the pull grows over the
        /// effect's length, and lets go the same way. Switched off, it takes hold at once.
        /// </summary>
        private void UpdateSnap()
        {
            if (_dragging == null)
            {
                _snapWeight = 0f;
                return;
            }
            float target = _dragging.Face.Aim == CardAim.Single && _zoneHot ? 1f : 0f;
            float ms = _effects.Ms(EffectId.ReceiverSnap);
            // The battle speed (#348), like every tween.
            float next = ms > 0f ? Mathf.MoveTowards(_snapWeight, target, Time.unscaledDeltaTime * FrameMotionSpeed * 1000f / ms) : target;
            if (next == _snapWeight) return;
            if (_snapWeight == 0f && next > 0f && Trace != null)
            {
                if (_effects.IsOn(EffectId.ReceiverSnap)) _snapHandle = Trace.Begin(EffectId.ReceiverSnap, CurrentOrder);
                else Trace.Skip(EffectId.ReceiverSnap, CurrentOrder);
            }
            if (next >= 1f && _snapHandle >= 0)
            {
                Trace.End(_snapHandle);
                _snapHandle = -1;
            }
            _snapWeight = next;
            _dragging.transform.position = DragPosition();
        }

        /// <summary>Halt: a held card goes back to its place unplayed, and the drop zone goes.</summary>
        private void LetGo()
        {
            CardView held = _dragging;
            _dragging = null;
            _dragSource = null;
            _pickedUnit = -1;
            _zoneHot = false;
            _snapWeight = 0f;
            _snapHandle = -1;
            if (_zoneFade != null)
            {
                StopCoroutine(_zoneFade);
                _zoneFade = null;
            }
            receiver.Hide();
            throwLine.Hide();
            HideTargetMarks();
            _staminaTag?.ShowCost(0);
            ShowReserveHint(null);
            ShowHpLoss(null);
            if (held)
            {
                held.group.alpha = 1f;
                held.ShowLamp(held.Face.TraitLit);
            }
            LayoutHand();
        }

        private void Release(CardView card, DropZone zone)
        {
            // §3.9 (#589): the enemy it was released over; −1 lets the source send it where its face was read.
            int target = _pickedUnit;
            _pickedUnit = -1;
            _dragging = null;
            _zoneHot = false;
            _snapWeight = 0f;
            _snapHandle = -1;
            if (_zoneFade != null)
            {
                StopCoroutine(_zoneFade);
                _zoneFade = null;
            }
            receiver.Hide();
            throwLine.Hide();
            HideTargetMarks();
            _staminaTag?.ShowCost(0);
            ShowReserveHint(null);
            ShowHpLoss(null);
            DepictionEvent played;
            PlayVerdict verdict = target >= 0 && TargetedSource != null
                ? TargetedSource.TryPlay(card.CardId, zone, target, out played)
                : _source.TryPlay(card.CardId, zone, out played);
            if (verdict == PlayVerdict.Accepted) StartCoroutine(PlayAccepted(card, played));
            else
            {
                ShowRefusal(card, verdict, target);
                // A card released on a zone it belongs to was refused by a rule: shake it. One released
                // on the wrong zone just slides home.
                StartCoroutine(ReturnToHand(card, shake: verdict != PlayVerdict.WrongZone && zone != DropZone.None));
            }
        }

        private IEnumerator PlayAccepted(CardView card, DepictionEvent ev)
        {
            _busy = true;
            SetHandInteractable(false);
            float startedAt = Time.unscaledTime;
            BeginPlan(ev);
            _hand.Remove(card);
            Vector3 from = card.transform.position;
            Vector3 to = ev.Aim == CardAim.Single
                ? receiver.transform.position
                : from + new Vector3(0f, 140f * handArea.lossyScale.y, 0f); // a self card is tossed upward
            // §5.4 順 0 (#499): a trait that fires before the first face shows as the card is released,
            // so the plan gives the release no wait of its own and the card flies off beside the cue.
            yield return HoldBeat(EffectId.CardRelease, UiTween.Run(_effects.Ms(EffectId.CardRelease), Ease.In, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, to, t);
                float s = Mathf.Lerp(1f, 0.5f, t);
                card.Rect.localScale = new Vector3(s, s, 1f);
                card.group.alpha = 1f - t;
            }), null, played: () => Released(card, ev.PlayedTo));

            foreach (Cue cue in ev.Cues)
            {
                yield return PlayCue(ev, cue);
            }
            // As in PlayEvent: a blow with no return (Guard took part of it, or a move followed it)
            // may not keep its action picture into the next event.
            if (playerFigure) playerFigure.SetPose(FigurePose.Idle);
            EndPlan();
            FadeSpentStripes(ev.After);
            ApplyFrame(ev.After);
            float seconds = Time.unscaledTime - startedAt;
            EventSeconds.Add(seconds);
            LogSettled(ev, seconds);
            yield return Gate(ev.Order + "-end");
            yield return UiTween.Wait(EffectFlow.AfterPlayerEventMs);
            yield return RunAutomaticEvents();
        }

        /// <summary>The released card is gone: a ghost flies to the pile the core put it in and the hand closes up.</summary>
        private void Released(CardView card, PileKind playedTo)
        {
            if (!card) return;
            Vector2 vanishedAt = card.Face.Aim == CardAim.Single && receiver
                ? DepictionFx.PointOn(fxLayer, receiver.transform)
                : DepictionFx.PointOn(fxLayer, card.transform);
            Destroy(card.gameObject);
            StartCoroutine(GhostToPile(vanishedAt, playedTo));
            StartCoroutine(Effect(EffectId.HandFan, SettleHand(_effects.Ms(EffectId.HandFan)), LayoutHand));
        }

        /// <summary>
        /// Slides the remaining cards to their new fan places. Each start value is kept with its card,
        /// not by position in the list, so a hand that changes mid-tween cannot pair a card with
        /// another card's start; a card that left the hand is skipped.
        /// </summary>
        private IEnumerator SettleHand(float ms)
        {
            var starts = new List<(CardView card, Vector2 position, float degrees)>();
            foreach (CardView card in _hand)
            {
                if (card) starts.Add((card, card.Rect.anchoredPosition, card.Rect.localEulerAngles.z));
            }
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                foreach ((CardView card, Vector2 position, float degrees) start in starts)
                {
                    int index = start.card ? _hand.IndexOf(start.card) : -1;
                    if (index < 0) continue;
                    HomeOf(index, out Vector2 home, out float homeDegrees);
                    start.card.Rect.anchoredPosition = Vector2.Lerp(start.position, home, t);
                    start.card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(start.degrees, homeDegrees, t));
                    start.card.Rect.localScale = Vector3.one;
                }
            });
        }

        private IEnumerator ReturnToHand(CardView card, bool shake)
        {
            _busy = true;
            Vector3 from = card.transform.position;
            HomeOf(_hand.IndexOf(card), out _, out float homeDegrees);
            Vector3 home = _dragHome;
            yield return Effect(EffectId.CardReturn, UiTween.Run(_effects.Ms(EffectId.CardReturn), Ease.Out, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, home, t);
                card.Rect.localScale = Vector3.one;
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(0f, homeDegrees, t));
            }), null);
            if (card)
            {
                card.group.alpha = 1f;
                card.ShowLamp(card.Face.TraitLit);
            }
            if (shake && card) yield return Effect(EffectId.RefusalShake, UiTween.Shake(card.Rect, 8f, 2, _effects.Ms(EffectId.RefusalShake)), null);
            LayoutHand();
            _busy = false;
            RefreshPlayableLook();
        }

        /// <summary>
        /// Debug only: performs the drag the source suggests, so a slice can be captured without a
        /// mouse. A fixed script suggests its next card, and a core battle started with auto play the leftmost one it can pay for.
        /// </summary>
        private IEnumerator AutoDrag()
        {
            yield return UiTween.Wait(400f);
            if (_halted) yield break;
            string suggested = _source.SuggestedCardId;
            if (string.IsNullOrEmpty(suggested))
            {
                // Nothing left to drag. An unattended run ends the turn the way a click on the plate would.
                if (autoEndTurn && !_busy && _source.CanEndTurn) StartCoroutine(PlayThenContinue(_source.EndTurn()));
                yield break;
            }
            CardView card = _hand.Find(c => c && c.CardId == suggested);
            if (card == null) yield break;
            CardAim aim = card.Face.Aim;
            BeginHold(card);
            Vector3 from = card.transform.position;
            Vector3 to = aim == CardAim.Single
                ? receiver.transform.position
                : new Vector3(from.x, throwLine.transform.position.y + (throwLine.transform.position.y - from.y) * 0.35f, from.z);
            yield return UiTween.Run(450f, Ease.InOut, t => { if (card && !_halted) card.transform.position = Vector3.LerpUnclamped(from, to, t); });
            if (_halted) yield break; // Halt let the card go back to the fan mid-flight
            SetZoneHot(card, true);
            yield return Gate((EventSeconds.Count + 1) + "-hover");
            if (_halted) yield break; // Halt let the card go
            Release(card, DepictionText.RequiredZone(aim));
        }

        private Vector3 PointerWorld(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(handArea, e.position, e.pressEventCamera, out Vector3 world);
            return world;
        }
    }
}
#endif
