// DepictionPlayer's hand: a frame on the screen, the hand's places, hover and its panels, the guide line (#585).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public partial class DepictionPlayer
    {
        // ---- frame -> screen ------------------------------------------------------------------

        /// <summary>
        /// #606: hands the script's layer to the backdrop (battle-visual-v1 §2.4), so it no longer has to
        /// read the corner info's text back. BackdropView attaches itself after the scene loads (#89), so the
        /// first frames may come before it: it is looked for again until it is there. Show ignores the
        /// layer it already shows, so every frame may call it.
        /// </summary>
        private void ShowBackdropLayer(int layer)
        {
            if (layer < 1 || !backdrop) return;
            if (!_backdropView) _backdropView = FindAnyObjectByType<Backdrop.View.BackdropView>();
            if (_backdropView) _backdropView.Show(layer);
        }

        /// <summary>Enemy <paramref name="k"/>'s omen in <paramref name="frame"/>, with its swatch (§4.4, #589).</summary>
        private static void BindBadge(OmenBadgeView badge, DepictionFrame frame, int k)
        {
            badge.Bind(EnemyScreen.OmenOf(frame, k), EnemySwatch.OnBadge(frame, k), EnemySwatch.DirectionOf(frame, k));
        }

        private void ApplyFrame(DepictionFrame frame)
        {
            if (cornerInfo) cornerInfo.Bind(frame.Corner);
            ShowBackdropLayer(frame.Corner.Floor);
            EnsureEnemies(EnemyScreen.CountOf(frame));
            // #288: the art is the script's to name; the figure looks it up and keeps it while the id stays.
            playerFigure.ShowArt(frame.Player.ArtId);
            playerFigure.SetDown(frame.Player.Down);
            // §4.3 (#455): 226 px tall, 264 for an elite on two cells (the script's sizes).
            playerFigure.StandTall(FigureFrame.HeightFor(frame.Floor.PlayerSize));
            for (int k = 0; k < _enemyCount; k++)
            {
                UnitFrame unit = EnemyScreen.UnitOf(frame, k);
                _enemies[k].Figure.ShowArt(unit.ArtId);
                _enemies[k].Figure.SetDown(unit.Down);
                _enemies[k].Figure.StandTall(FigureFrame.HeightFor(EnemyScreen.SizeOf(frame, k)));
            }
            _staminaTag?.SetDivider(frame.StanceDivider); // before the bind below shows the stamina
            _staminaTag?.SetNextRecovery(frame.Player.NextRecovery); // §4.7 (#455): the dashed dots
            ApplyPiles(frame.Piles);
            _stanceLists?.Bind(frame); // §4.6 (#455)
            ApplyStanceGlows(frame); // §5.11 順 2 (#501): the body's light while the list holds a stance
            playerStatus.Bind(frame.Player);
            playerFigure.SetRange(frame.Player.HasRange, frame.Player.RangeGlyph);
            for (int k = 0; k < _enemyCount; k++)
            {
                UnitFrame unit = EnemyScreen.UnitOf(frame, k);
                // §2.2 の 6 (#589): the panel of an enemy that fell while others stood stays, grey. One that
                // stands takes back its colours before the bind, so this frame's lie over the last battle's.
                bool fallen = SeveralEnemies && unit.Down;
                if (!fallen) _enemies[k].Status.SetFallen(false);
                _enemies[k].Status.SetSwatch(EnemySwatch.OnPanel(frame, k), EnemySwatch.DirectionOf(frame, k)); // §4.5 (#589)
                _enemies[k].Status.Bind(unit);
                if (fallen) _enemies[k].Status.SetFallen(true);
                _enemies[k].Figure.SetRange(unit.HasRange, unit.RangeGlyph);
            }
            SetPlayerHome(playerFigure);
            ApplyFloor(frame); // §4.2: the figures stand on their cells (#577: every source draws the floor)
            for (int k = 0; k < _enemyCount; k++) BindBadge(_enemies[k].Badge, frame, k);
            if (SeveralEnemies) DrawNearerInFront(frame);
            bool hasHint = !string.IsNullOrEmpty(frame.StanceHint);
            if (stanceHintText) stanceHintText.text = frame.StanceHint;
            if (stanceHintIcon) stanceHintIcon.enabled = hasHint;
            _frameStanceHint = frame.StanceHint ?? "";
            ShowReserveHint(_dragging); // §4.9 (#455)
            _played?.Bind(frame.Played, this);
            ShowEnemyTurn(frame.EnemyTurn); // §5.5 (#455): sinks once the turn end has played
            if (!SameHand(frame.Hand)) BuildHand(frame.Hand);
            else for (int i = 0; i < _hand.Count; i++) _hand[i].Bind(frame.Hand[i]);
            LayoutHand();
            ShowHpLoss(_dragging); // §4.5 (#455): the bar was bound again, and so was the held card's face
        }

        private bool SameHand(List<CardFace> faces)
        {
            if (faces.Count != _hand.Count) return false;
            for (int i = 0; i < faces.Count; i++)
            {
                if (_hand[i].CardId != faces[i].Id) return false;
            }
            return true;
        }

        private void BuildHand(List<CardFace> faces)
        {
            ClearHand();
            foreach (CardFace face in faces)
            {
                CardView card = Instantiate(cardPrefab, handArea);
                card.Bind(face);
                card.SetDimmed(false);
                card.DragBegan += OnDragBegan;
                card.DragMoved += OnDragMoved;
                card.DragEnded += OnDragEnded;
                _hand.Add(card);
            }
            LayoutHand();
        }

        private void ClearHand()
        {
            foreach (CardView card in _hand)
            {
                if (card) Destroy(card.gameObject);
            }
            _hand.Clear();
        }

        private void LayoutHand()
        {
            _hovered = null;
            _hoverWeight.Clear();
            for (int i = 0; i < _hand.Count; i++)
            {
                PlaceAtHome(_hand[i], i);
            }
            RestoreCardOrder();
        }

        /// <summary>Cards draw left to right, after anything else placed under the hand area by hand.</summary>
        private void RestoreCardOrder()
        {
            int first = Mathf.Max(0, handArea.childCount - _hand.Count);
            for (int i = 0; i < _hand.Count; i++)
            {
                if (_hand[i]) _hand[i].transform.SetSiblingIndex(first + i);
            }
        }

        /// <summary>
        /// Resting place of the i-th card (battle-visual-v1 §4.8, #242): centred on x 960, the middle
        /// card's top at y 896 so only its upper 184 px show, the k-th from the middle k² × 3.2 px lower,
        /// tilted 2.5° a step about the middle of its bottom edge. Worked out on the 1920×1080 screen and
        /// put into the hand area's space, so where the hand area stands in the scene no longer matters.
        /// </summary>
        private void HomeOf(int index, out Vector2 position, out float degrees)
        {
            FanPlace rest = HandSpec.Rest(_hand.Count, index, HandFan.Spacing(_hand.Count, cardSpacing), fanDegreesPerCard);
            position = HandPoint(new Vector2(rest.X, rest.Y));
            degrees = rest.Degrees;
        }

        /// <summary>§4.8 ホバー: upright, in front, raised until its bottom is 16 px above the screen's (top at y 760).</summary>
        private Vector2 HoverPlace(int index)
        {
            FanPlace hover = HandSpec.Hover(_hand.Count, index, HandFan.Spacing(_hand.Count, cardSpacing));
            return HandPoint(new Vector2(hover.X, hover.Y));
        }

        /// <summary>
        /// A point given on the 1920×1080 screen (x from the left, y from the top) as an anchored position
        /// under the hand area. x is kept from the screen's centre and y from its bottom, so a screen of
        /// another shape keeps the hand on its bottom edge.
        /// </summary>
        private Vector2 HandPoint(Vector2 screen)
        {
            Canvas canvas = handArea ? handArea.GetComponentInParent<Canvas>() : null;
            if (!canvas) return new Vector2(screen.x - BattleTheme.RefWidth * 0.5f, BattleTheme.RefHeight * 0.5f - screen.y);
            var root = (RectTransform)canvas.rootCanvas.transform;
            Rect area = root.rect;
            var local = new Vector2(area.center.x + (screen.x - BattleTheme.RefWidth * 0.5f), area.yMin + (BattleTheme.RefHeight - screen.y));
            Vector3 inHand = handArea.InverseTransformPoint(root.TransformPoint(local));
            return new Vector2(inHand.x, inHand.y);
        }

        private void PlaceAtHome(CardView card, int index)
        {
            HomeOf(index, out Vector2 position, out float degrees);
            card.Rect.anchoredPosition = position;
            card.Rect.localScale = Vector3.one;
            card.Rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        // ---- hover ----------------------------------------------------------------------------

        /// <summary>
        /// Lifts the card under the pointer and brings it to the front. A card starts its hover from its
        /// resting place, so it does not flicker when the pointer sits on its bottom edge; once hovered,
        /// its raised place counts as well (<see cref="HoveredRectContains"/>).
        /// </summary>
        private void UpdateHover()
        {
            if (_source == null) return;
            if (_busy || _dragging != null)
            {
                // Tweens own the cards now; they end in LayoutHand, which also drops the hover.
                _hovered = null;
                _hoverWeight.Clear();
                HideHoverPanels();
                return;
            }

            CardView over = CardUnderPointer();
            if (over != _hovered)
            {
                RestoreCardOrder();
                if (over) over.transform.SetAsLastSibling();
                _hovered = over;
                if (_panels != null) _panels.HideDetail(); // the detail belongs to the card it was opened on
                // EffectId.CardHover: timed from the moment a card starts to rise until it is fully up.
                _hoverHandle = -1;
                if (over && Trace != null)
                {
                    if (_effects.IsOn(EffectId.CardHover)) _hoverHandle = Trace.Begin(EffectId.CardHover, CurrentOrder);
                    else Trace.Skip(EffectId.CardHover, CurrentOrder);
                }
            }

            float hoverMs = _effects.Ms(EffectId.CardHover);
            // The battle speed (#348), like every tween. Input timing is not scaled (§6.2 rule 1).
            float step = hoverMs > 0f ? Time.unscaledDeltaTime * FrameMotionSpeed * 1000f / hoverMs : 1f;
            for (int i = 0; i < _hand.Count; i++)
            {
                CardView card = _hand[i];
                if (!card) continue;
                _hoverWeight.TryGetValue(card, out float weight);
                float next = Mathf.MoveTowards(weight, card == over ? 1f : 0f, step);
                if (next == weight && weight == 0f) continue;
                _hoverWeight[card] = next;
                if (card == over && next >= 1f && _hoverHandle >= 0)
                {
                    Trace.End(_hoverHandle);
                    _hoverHandle = -1;
                }
                float eased = next * next * (3f - 2f * next);
                HomeOf(i, out Vector2 home, out float degrees);
                // §4.8 / §6.2: upright and raised to the hover height; the size never changes.
                card.Rect.anchoredPosition = Vector2.Lerp(home, HoverPlace(i), eased);
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(degrees, 0f, eased));
                card.Rect.localScale = Vector3.one;
            }
            UpdateHoverPanels(over);
        }

        // ---- the panels around a hovered card (battle-visual-v1 §5.1 / §7.2 / §7.4, #242) ----

        private CardPanels _panels;

        private CardPanels Panels
        {
            get
            {
                if (_panels != null || !handArea) return _panels;
                Canvas canvas = handArea.GetComponentInParent<Canvas>();
                if (canvas) _panels = new CardPanels((RectTransform)canvas.rootCanvas.transform);
                return _panels;
            }
        }

        /// <summary>
        /// Follows the hovered card with its hover line and, when the script says nobody is in its reach,
        /// the reason on its right. A right click on it opens the detail (§7.4); moving off it closes it.
        /// </summary>
        private void UpdateHoverPanels(CardView over)
        {
            CardPanels panels = Panels;
            if (panels == null) return;
            if (!over)
            {
                HideHoverPanels();
                return;
            }
            if (TryRightPress()) panels.ToggleDetail(over, cardPrefab);
            if (panels.DetailShown) panels.HideHover();
            else
            {
                panels.ShowHover(over);
                // §7.1 (#455): a pointer on an underlined term shows what it means, above the word.
                if (TryPointer(out Vector2 pointer) && over.TryTermAt(pointer, out TermMark term, out RectTransform word)) panels.ShowTerm(word, term);
                else panels.HideTerm();
            }
        }

        private void HideHoverPanels()
        {
            if (_panels == null) return;
            _panels.HideHover();
            _panels.HideDetail();
        }

        /// <summary>True on the frame the right button went down (§7.4 右クリック).</summary>
        private static bool TryRightPress()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }

        /// <summary>True on the frame Esc went down.</summary>
        private static bool TryEscape()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private CardView CardUnderPointer()
        {
            if (!TryPointer(out Vector2 pointer)) return null;
            // A card under the open pile list (#455) is not hovered through it.
            if (_pileList != null && _pileList.Covers(pointer)) return null;
            // The hovered card is drawn in front, so it wins where two cards overlap.
            // It is also caught on its raised part (§4.8: up to 136 px above its resting place), so moving
            // onto what the hover lifted keeps the hover, and the right click (§7.4) works there too.
            if (_hovered && _hovered.Interactable && HoveredRectContains(_hovered, _hand.IndexOf(_hovered), pointer)) return _hovered;
            for (int i = _hand.Count - 1; i >= 0; i--)
            {
                CardView card = _hand[i];
                if (card && card.Interactable && RestingRectContains(card, i, pointer)) return card;
            }
            return null;
        }

        /// <summary>
        /// True when the pointer is over the card as it would lie at rest: at its fan place, tilted,
        /// unlifted and unscaled. Undoing only the lift would leave the hover scale in the test, and
        /// the rim that scale adds would flicker the hover on and off.
        /// </summary>
        private bool RestingRectContains(CardView card, int index, Vector2 pointer)
        {
            if (index < 0) return false;
            HomeOf(index, out Vector2 home, out float degrees);
            return RectAtContains(card, home, degrees, pointer);
        }

        /// <summary>
        /// The hovered card's test: its resting place or its raised one (<see cref="HoverPlace"/>,
        /// upright). Testing only the resting place dropped the hover once the pointer followed the
        /// card up onto the part the hover lifts (§4.8: 136 px).
        /// </summary>
        private bool HoveredRectContains(CardView card, int index, Vector2 pointer)
        {
            if (index < 0) return false;
            return RestingRectContains(card, index, pointer) || RectAtContains(card, HoverPlace(index), 0f, pointer);
        }

        /// <summary>True when the pointer is over the card as it would stand at <paramref name="place"/>, tilted <paramref name="degrees"/>, unscaled.</summary>
        private bool RectAtContains(CardView card, Vector2 place, float degrees, Vector2 pointer)
        {
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(handArea, pointer, null, out Vector3 world)) return false;
            // anchoredPosition and localPosition differ by a constant, so the localPosition at that place
            // is the current one moved by how far the card sits from it.
            Vector3 atLocal = card.Rect.localPosition + (Vector3)(place - card.Rect.anchoredPosition);
            Vector3 inHand = handArea.InverseTransformPoint(world);
            Vector3 inCard = Quaternion.Inverse(Quaternion.Euler(0f, 0f, degrees)) * (inHand - atLocal);
            return card.Rect.rect.Contains(inCard);
        }

        /// <summary>True on the frame the pointer was pressed, with where it was pressed.</summary>
        private static bool TryPointerPress(out Vector2 position)
        {
            position = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
            position = pointer.position.ReadValue();
            return true;
#else
            if (!Input.GetMouseButtonDown(0)) return false;
            position = Input.mousePosition;
            return true;
#endif
        }

        private static bool TryPointer(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            position = pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            bool present = pointer != null;
#else
            position = Input.mousePosition;
            bool present = Input.mousePresent;
#endif
            // A pointer that left the game view keeps reporting its last position; that is not a hover.
            return present && new Rect(0f, 0f, Screen.width, Screen.height).Contains(position);
        }

        private void SetHandInteractable(bool on)
        {
            foreach (CardView card in _hand)
            {
                card.Interactable = on;
            }
            if (!on && _refusal != null)
            {
                StopCoroutine(_refusal);
                _refusal = null;
                _refusalLine?.Hide();
            }
            RefreshPlayableLook();
        }

        // ---- guide ----------------------------------------------------------------------------

        /// <summary>
        /// Dims every card the source would refuse right now and prints the source's guide line.
        /// Which cards those are, and what the line says, are the source's answer; the View shows it.
        /// </summary>
        private void RefreshPlayableLook()
        {
            bool waiting = _source != null && !_busy && _source.WaitingForPlayer
                && _hand.Exists(c => c && c.Interactable);
            foreach (CardView card in _hand)
            {
                if (!card || card == _dragging) continue;
                bool dim = waiting && _source.Inspect(card.CardId) != PlayVerdict.Accepted;
                if (dim == card.Dimmed) continue;
                CardView dimmed = card;
                StartCoroutine(Effect(EffectId.UnpayableDim, dimmed.FadeDimmed(dim, _effects.Ms(EffectId.UnpayableDim)), () => dimmed.SetDimmed(dim)));
            }
            if (_refusal != null || !handGuide) return;
            handGuide.color = BattleTheme.Ink;
            handGuide.text = _source != null && (waiting || _source.Finished) ? _source.GuideText : "";
        }

        /// <summary>Says why a released card went back to the hand, then returns to the guide line.</summary>
        private void ShowRefusal(CardView card, PlayVerdict verdict, int target)
        {
            if ((!handGuide && _refusalLine == null) || _source == null) return;
            string text = target >= 0 && TargetedSource != null
                ? TargetedSource.RefusalText(card.CardId, verdict, target)
                : _source.RefusalText(card.CardId, verdict);
            if (string.IsNullOrEmpty(text)) return;
            if (_refusal != null) StopCoroutine(_refusal);
            _refusal = StartCoroutine(Refusal(text));
        }

        /// <summary>
        /// battle-visual-v1 §5.4 (#455): the reason stands on its own line above the hand for
        /// <see cref="refusalSeconds"/> (3.5 s), and the guide line steps aside meanwhile. A scene
        /// without the canvas the line is made on keeps the old guide-line text.
        /// </summary>
        private IEnumerator Refusal(string text)
        {
            if (_refusalLine != null)
            {
                _refusalLine.Show(text);
                if (handGuide) handGuide.text = "";
            }
            else
            {
                handGuide.color = BattleTheme.Omen;
                handGuide.text = text;
            }
            yield return new WaitForSecondsRealtime(refusalSeconds);
            _refusal = null;
            _refusalLine?.Hide();
            RefreshPlayableLook();
        }
    }
}
#endif
