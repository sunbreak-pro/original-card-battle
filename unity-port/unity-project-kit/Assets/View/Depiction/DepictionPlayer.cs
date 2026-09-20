// Plays whatever an IDepictionSource hands it: the live turn the player fights by default, or the
// fixed script kept for filming. Some events wait for the player; the rest run on their own. This
// class computes no rule: every number, label, verdict and line of text it shows comes from the
// source (DepictionFrame / DepictionEvent / Cue / PlayVerdict). Positions come from the scene
// objects at run time, so prefab instances can be moved in the Editor freely.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Depiction.View
{
    public class DepictionPlayer : MonoBehaviour
    {
        [Header("Prefabs")]
        public CardView cardPrefab;

        [Header("Scene instances")]
        public Image backdrop;
        public CornerInfoView cornerInfo;
        public FigureView playerFigure;
        public FigureView enemyFigure;
        public RectTransform playerNearSlot;
        public RectTransform playerFarSlot;
        public StatusBarView playerStatus;
        public StatusBarView enemyStatus;
        public OmenBadgeView omenBadge;
        public ReceiverView receiver;
        public ThrowLineView throwLine;
        public RectTransform handArea;
        public RectTransform deckPoint;
        public RectTransform discardPoint;
        public RectTransform endTurn;
        public Image stanceHintIcon;
        public Text stanceHintText;
        public RectTransform fxLayer;
        [Tooltip("One line above the hand: the card the script plays next, or why a released card came back.")]
        public Text handGuide;

        [Header("Hand layout")]
        [Tooltip("Distance between neighbouring cards. Below the card width (190) the cards overlap like a spread bundle.")]
        public float cardSpacing = 160f;
        [Tooltip("The hand is a shallow fan: each step away from the middle tilts a card by this many degrees...")]
        [Range(0f, 15f)] // HandFan assumes the outermost tilt stays within 90 degrees
        public float fanDegreesPerCard = 2.5f;
        [Tooltip("...and the middle rises above the outermost cards by this many pixels times the step squared.")]
        public float fanDropPixels = 4f;

        [Header("Hover")]
        [Tooltip("A card under the pointer rises by this much and comes to the front. Its fan angle is kept.")]
        public float hoverLiftPixels = 28f;
        public float hoverScale = 1.05f;
        public float hoverSeconds = 0.09f;

        [Header("Guide")]
        [Tooltip("How long the reason a card came back stays on the guide line.")]
        public float refusalSeconds = 3.5f;

        [Header("Mode")]
        [Tooltip("Filming only: replays TurnSliceScript in its written order, refusing any other card. "
                 + "Off by default, so the hand is free and the fight is played for real.")]
        public bool scriptedPlayback;

        [Header("Debug")]
        [Tooltip("Performs the source's suggested drags by itself. Only a fixed script suggests one.")]
        public bool autoPlayDrags;
        [Tooltip("Freezes at named gates (hover / impact / end) until DebugStep() is called. For screenshots.")]
        public bool debugStepMode;
        [Tooltip("Folder (absolute, or relative to the project) that receives one PNG per gate. Empty = no capture.")]
        public string debugCaptureDir = "";

        /// <summary>Name of the gate the playback is frozen at ("3-impact"), or empty.</summary>
        public string DebugGate { get; private set; } = "";
        public bool Finished { get; private set; }
        /// <summary>Seconds each event took from its start (or from the confirmed drop) to its settled frame.</summary>
        public readonly List<float> EventSeconds = new List<float>();

        private IDepictionSource _source;
        private readonly List<CardView> _hand = new List<CardView>();
        private bool _busy;
        private bool _stepRequested;
        private CardView _dragging;
        private Vector3 _dragHome;
        private Vector3 _grabOffset;
        private CardView _hovered;
        private readonly Dictionary<CardView, float> _hoverWeight = new Dictionary<CardView, float>();
        private Coroutine _refusal;

        private void Start()
        {
            // Keep playing while the Editor is unfocused (captures and remote-driven checks rely on it).
            Application.runInBackground = true;
            EnsureEventSystem();
            UiTween.Speed = 1f; // a debug gate may have left it at 0 when domain reload is off
            if (backdrop && backdrop.sprite == null)
            {
                BattleTheme.FloorPalette palette = BattleTheme.Floor(1);
                backdrop.sprite = ProceduralArt.VerticalGradient(palette.Top, palette.Bottom);
                backdrop.color = Color.white;
            }
            if (stanceHintIcon && stanceHintIcon.sprite == null) stanceHintIcon.sprite = ProceduralArt.Shield;

            _source = scriptedPlayback
                ? (IDepictionSource)new DepictionRunner(TurnSliceScript.Build())
                : new LiveTurn();
            ApplyFrame(_source.Frame);
            StartCoroutine(RunAutomaticEvents());
        }

        private void OnDisable()
        {
            // A debug gate freezes every tween through the shared UiTween.Speed. Never leave it frozen
            // for the next scene (View v1.1 uses the same static when domain reload is off).
            UiTween.Speed = 1f;
            // Stopping the component also stops the refusal coroutine; do not leave the guide line stuck.
            _refusal = null;
        }

        private void Update()
        {
            UpdateHover();
            UpdateEndTurn();
        }

        /// <summary>
        /// Reads a click on the end-turn plate. The plate is a plain rect in the prefab and the prefab
        /// belongs to the Unity project, not to this repo, so the click is read here instead of being
        /// wired to a Button component that would have to be added there.
        /// </summary>
        private void UpdateEndTurn()
        {
            if (_source == null || _busy || _dragging != null || !endTurn) return;
            if (!_source.CanEndTurn) return;
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
            yield return UiTween.Wait(250f);
            yield return RunAutomaticEvents();
        }

        public void DebugStep()
        {
            _stepRequested = true;
        }

        // ---- flow -------------------------------------------------------------------------

        private IEnumerator RunAutomaticEvents()
        {
            _busy = true;
            while (!_source.Finished && !_source.WaitingForPlayer)
            {
                DepictionEvent ev = _source.AdvanceAuto();
                yield return PlayEvent(ev);
                yield return UiTween.Wait(350f);
            }
            _busy = false;
            if (_source.Finished)
            {
                Finished = true;
                RefreshPlayableLook(); // the guide line carries the outcome
                Debug.Log("[Depiction] finished. seconds per event: " + string.Join(" / ", EventSeconds.ConvertAll(s => s.ToString("0.00"))));
                yield break;
            }
            SetHandInteractable(true);
            if (autoPlayDrags) StartCoroutine(AutoDrag());
        }

        private IEnumerator PlayEvent(DepictionEvent ev)
        {
            float startedAt = Time.unscaledTime;
            foreach (Cue cue in ev.Cues)
            {
                yield return PlayCue(ev, cue);
            }
            ApplyFrame(ev.After);
            float seconds = Time.unscaledTime - startedAt;
            EventSeconds.Add(seconds);
            Debug.Log("[Depiction] event " + ev.Order + " " + ev.Title + " settled in " + seconds.ToString("0.00") + " s");
            yield return Gate(ev.Order + "-end");
        }

        private IEnumerator PlayCue(DepictionEvent ev, Cue cue)
        {
            FigureView target = cue.Target == UnitSide.Player ? playerFigure : enemyFigure;
            StatusBarView status = cue.Target == UnitSide.Player ? playerStatus : enemyStatus;
            Vector2 chest = DepictionFx.PointOn(fxLayer, target.chest);
            Vector2 head = DepictionFx.PointOn(fxLayer, target.head);
            // Numbers start at the chest and labels just under the head, so neither climbs into the
            // omen badge or off the top edge.
            Vector2 numberAt = chest + new Vector2(0f, 40f);
            Vector2 labelAt = head + new Vector2(0f, -30f);

            switch (cue.Kind)
            {
                case CueKind.GuardReset:
                    status.SetGuard(cue.GuardAfter);
                    break;

                case CueKind.StaminaChange:
                    status.SetStamina(cue.StaminaAfter, cue.StaminaMax);
                    if (status.pipRow) StartCoroutine(UiTween.Pop(status.pipRow, 220f));
                    if (cue.Amount > 0) yield return UiTween.Wait(220f);
                    break;

                case CueKind.DrawHand:
                    yield return DrawHand(ev.After.Hand);
                    break;

                case CueKind.OmenShow:
                    omenBadge.Bind(ev.After.Omen);
                    yield return omenBadge.PopIn(240f);
                    break;

                case CueKind.TraitFire:
                    DepictionFx.Burst(this, fxLayer, labelAt, BattleTheme.Warm, 260f);
                    DepictionFx.FloatText(this, fxLayer, labelAt, cue.Text, BattleTheme.Warm, 40, rise: 70f);
                    yield return UiTween.Wait(260f);
                    break;

                case CueKind.Slash:
                    yield return Attack(ev, cue, target, status, chest);
                    break;

                case CueKind.GuardGain:
                    DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Guard, 220f + 40f * cue.Intensity);
                    DepictionFx.FloatText(this, fxLayer, numberAt, "+" + cue.Amount, BattleTheme.Guard, DepictionFx.NumberFont(cue.Intensity));
                    yield return status.PopGuard(cue.GuardAfter);
                    break;

                case CueKind.RangeSwitch:
                    yield return SwitchRange(target, cue.RangeAfter, cue.RangeGlyphAfter);
                    break;

                case CueKind.StanceCue:
                    if (endTurn) yield return UiTween.Pop(endTurn, 200f);
                    DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Guard, 300f);
                    DepictionFx.FloatText(this, fxLayer, numberAt, cue.Text, BattleTheme.Guard, 48);
                    yield return status.PopGuard(cue.GuardAfter);
                    yield return Gate(ev.Order + "-impact");
                    break;

                case CueKind.DiscardHand:
                    yield return DiscardHand();
                    break;

                case CueKind.EnemyWindup:
                    SetHandInteractable(false);
                    yield return Lunge(target, 70f, 220f);
                    break;

                case CueKind.SideBonusMiss:
                    DepictionFx.FloatText(this, fxLayer, DepictionFx.PointOn(fxLayer, omenBadge.transform) + new Vector2(0f, -64f),
                        cue.Text, BattleTheme.Whiff, 40, struck: true, rise: -50f);
                    yield return omenBadge.StrikeSide(260f);
                    break;

                case CueKind.GuardBlock:
                    status.SetGuard(cue.GuardAfter);
                    if (status.guardBadge) StartCoroutine(UiTween.Pop(status.guardBadge, 260f));
                    yield return DepictionFx.ShieldBlock(this, fxLayer, chest, cue.Amount.ToString(), () => Gate(ev.Order + "-block"));
                    break;

                case CueKind.Hit:
                    DepictionFx.FloatText(this, fxLayer, numberAt, cue.Amount.ToString(), BattleTheme.Omen, DepictionFx.NumberFont(cue.Intensity));
                    StartCoroutine(DepictionFx.Flash(target.body, BattleTheme.Omen, 260f));
                    StartCoroutine(UiTween.Shake(target.Rect, DepictionFx.Shake(cue.Intensity), 3, 260f));
                    yield return status.AnimateHp(cue.HpAfter, 300f);
                    yield return Gate(ev.Order + "-impact");
                    if (cue.Target == UnitSide.Player)
                    {
                        // The blow that lands on the player is the enemy's own: its omen is spent and
                        // it draws back. A blow the player lands does neither.
                        yield return omenBadge.FadeOut(200f);
                        yield return Lunge(enemyFigure, -70f, 140f);
                    }
                    break;
            }
        }

        // ---- beats --------------------------------------------------------------------------

        private IEnumerator Attack(DepictionEvent ev, Cue cue, FigureView target, StatusBarView status, Vector2 chest)
        {
            // The script must name the attacker. A missing one is a script bug: say so, then fall back
            // to the opposite of the target so the beat still plays.
            if (!cue.Source.HasValue) Debug.LogError("[Depiction] event " + ev.Order + ": Slash cue has no Source");
            UnitSide source = cue.Source ?? (cue.Target == UnitSide.Enemy ? UnitSide.Player : UnitSide.Enemy);
            bool byPlayer = source == UnitSide.Player;
            FigureView attacker = byPlayer ? playerFigure : enemyFigure;
            if (byPlayer) yield return Lunge(attacker, 46f, 100f);
            Color streak = byPlayer ? BattleTheme.Ink : BattleTheme.Omen;
            yield return DepictionFx.Slash(this, fxLayer, chest, streak, cue.Intensity, towardLeft: !byPlayer);
            yield return UiTween.Wait(DepictionFx.HitStop(cue.Intensity));

            // A slash with no settled HP is the swing alone: the GuardBlock / Hit cues that follow
            // carry the numbers. That is every enemy slash, and any slash Guard takes a bite out of.
            if (cue.HpAfter == Cue.Unchanged) yield break;

            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Omen, 200f + 60f * cue.Intensity);
            DepictionFx.FloatText(this, fxLayer, chest + new Vector2(0f, 40f), cue.Amount.ToString(), BattleTheme.Ink, DepictionFx.NumberFont(cue.Intensity));
            StartCoroutine(DepictionFx.Flash(target.body, BattleTheme.White, 220f));
            StartCoroutine(UiTween.Shake(target.Rect, DepictionFx.Shake(cue.Intensity), 3, 260f));
            yield return status.AnimateHp(cue.HpAfter, 300f);
            yield return Gate(ev.Order + "-impact");
            yield return Lunge(attacker, -46f, 120f);
        }

        /// <summary>Steps toward the opponent by <paramref name="pixels"/> (negative steps back).</summary>
        private IEnumerator Lunge(FigureView figure, float pixels, float ms)
        {
            float direction = figure.side == UnitSide.Player ? 1f : -1f;
            Vector2 from = figure.Rect.anchoredPosition;
            Vector2 to = from + new Vector2(pixels * direction, 0f);
            yield return UiTween.Move(figure.Rect, from, to, ms, Ease.Out);
        }

        private IEnumerator SwitchRange(FigureView figure, RangeSide range, string glyph)
        {
            RectTransform slot = range == RangeSide.Near ? playerNearSlot : playerFarSlot;
            Vector3 from = figure.transform.position;
            Vector3 to = slot ? slot.position : from;
            // 320 ms to move, then the tag flips in 150 ms (battle_ui_ux_v2 §10.6).
            yield return UiTween.Run(320f, Ease.InOut, t => { if (figure) figure.transform.position = Vector3.LerpUnclamped(from, to, t); });
            yield return figure.FlipRangeTag(glyph, 150f);
        }

        private IEnumerator DrawHand(List<CardFace> faces)
        {
            BuildHand(faces);
            Vector3 from = deckPoint ? deckPoint.position : handArea.position;
            var homes = new List<Vector3>();
            foreach (CardView card in _hand)
            {
                homes.Add(card.transform.position);
                card.transform.position = from;
                card.group.alpha = 0f;
            }
            for (int i = 0; i < _hand.Count; i++)
            {
                StartCoroutine(FlyCard(_hand[i], from, homes[i], 260f, 0f, 1f));
                yield return UiTween.Wait(60f);
            }
            yield return UiTween.Wait(260f);
        }

        private IEnumerator DiscardHand()
        {
            Vector3 to = discardPoint ? discardPoint.position : handArea.position;
            foreach (CardView card in _hand)
            {
                StartCoroutine(FlyCard(card, card.transform.position, to, 240f, 1f, 0f));
                yield return UiTween.Wait(60f);
            }
            yield return UiTween.Wait(240f);
            ClearHand();
        }

        private static IEnumerator FlyCard(CardView card, Vector3 from, Vector3 to, float ms, float alphaFrom, float alphaTo)
        {
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, to, t);
                card.group.alpha = Mathf.Lerp(alphaFrom, alphaTo, t);
            });
        }

        // ---- frame -> screen ------------------------------------------------------------------

        private void ApplyFrame(DepictionFrame frame)
        {
            if (cornerInfo) cornerInfo.Bind(frame.Corner);
            playerStatus.Bind(frame.Player);
            enemyStatus.Bind(frame.Enemy);
            playerFigure.SetRange(frame.Player.HasRange, frame.Player.RangeGlyph);
            enemyFigure.SetRange(frame.Enemy.HasRange, frame.Enemy.RangeGlyph);
            RectTransform slot = frame.Player.Range == RangeSide.Near ? playerNearSlot : playerFarSlot;
            if (slot) playerFigure.transform.position = slot.position;
            omenBadge.Bind(frame.Omen);
            bool hasHint = !string.IsNullOrEmpty(frame.StanceHint);
            if (stanceHintText) stanceHintText.text = frame.StanceHint;
            if (stanceHintIcon) stanceHintIcon.enabled = hasHint;
            if (!SameHand(frame.Hand)) BuildHand(frame.Hand);
            else for (int i = 0; i < _hand.Count; i++) _hand[i].Bind(frame.Hand[i]);
            LayoutHand();
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

        /// <summary>Resting place of the i-th card in a shallow fan (see <see cref="HandFan.Place"/>).</summary>
        private void HomeOf(int index, out Vector2 position, out float degrees)
        {
            FanPlace place = HandFan.Place(_hand.Count, index, cardSpacing, fanDegreesPerCard, fanDropPixels, cardPrefab.Rect.rect.width);
            position = new Vector2(place.X, place.Y);
            degrees = place.Degrees;
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
        /// Lifts the card under the pointer and brings it to the front. The test runs against the
        /// card's resting place, not the lifted one, so the card does not flicker when the pointer
        /// sits on its bottom edge.
        /// </summary>
        private void UpdateHover()
        {
            if (_source == null) return;
            if (_busy || _dragging != null)
            {
                // Tweens own the cards now; they end in LayoutHand, which also drops the hover.
                _hovered = null;
                _hoverWeight.Clear();
                return;
            }

            CardView over = CardUnderPointer();
            if (over != _hovered)
            {
                RestoreCardOrder();
                if (over) over.transform.SetAsLastSibling();
                _hovered = over;
            }

            float step = hoverSeconds > 0f ? Time.unscaledDeltaTime / hoverSeconds : 1f;
            for (int i = 0; i < _hand.Count; i++)
            {
                CardView card = _hand[i];
                if (!card) continue;
                _hoverWeight.TryGetValue(card, out float weight);
                float next = Mathf.MoveTowards(weight, card == over ? 1f : 0f, step);
                if (next == weight && weight == 0f) continue;
                _hoverWeight[card] = next;
                float eased = next * next * (3f - 2f * next);
                HomeOf(i, out Vector2 home, out _);
                card.Rect.anchoredPosition = home + new Vector2(0f, hoverLiftPixels * eased);
                float scale = Mathf.Lerp(1f, hoverScale, eased);
                card.Rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private CardView CardUnderPointer()
        {
            if (!TryPointer(out Vector2 pointer)) return null;
            // The hovered card is drawn in front, so it wins where two cards overlap.
            if (_hovered && _hovered.Interactable && RestingRectContains(_hovered, _hand.IndexOf(_hovered), pointer)) return _hovered;
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
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(handArea, pointer, null, out Vector3 world)) return false;
            HomeOf(index, out Vector2 home, out float degrees);
            // anchoredPosition and localPosition differ by a constant, so the resting localPosition is
            // the current one moved by how far the card sits from home.
            Vector3 restLocal = card.Rect.localPosition + (Vector3)(home - card.Rect.anchoredPosition);
            Vector3 inHand = handArea.InverseTransformPoint(world);
            Vector3 inCard = Quaternion.Inverse(Quaternion.Euler(0f, 0f, degrees)) * (inHand - restLocal);
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
                card.SetDimmed(waiting && _source.Inspect(card.CardId) != PlayVerdict.Accepted);
            }
            if (_refusal != null || !handGuide) return;
            handGuide.color = BattleTheme.Ink;
            handGuide.text = _source != null && (waiting || _source.Finished) ? _source.GuideText : "";
        }

        /// <summary>Says why a released card went back to the hand, then returns to the guide line.</summary>
        private void ShowRefusal(CardView card, PlayVerdict verdict)
        {
            if (!handGuide || _source == null) return;
            string text = _source.RefusalText(card.CardId, verdict);
            if (string.IsNullOrEmpty(text)) return;
            if (_refusal != null) StopCoroutine(_refusal);
            _refusal = StartCoroutine(Refusal(text));
        }

        private IEnumerator Refusal(string text)
        {
            handGuide.color = BattleTheme.Omen;
            handGuide.text = text;
            yield return new WaitForSecondsRealtime(refusalSeconds);
            _refusal = null;
            RefreshPlayableLook();
        }

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
                && RestingRectContains(_hovered, _hand.IndexOf(_hovered), e.position)) held = _hovered;
            _dragSource = card;
            BeginHold(held);
            _grabOffset = held.transform.position - PointerWorld(e);
        }

        private void OnDragMoved(CardView card, PointerEventData e)
        {
            if (_dragging == null || card != _dragSource) return;
            _dragging.transform.position = PointerWorld(e) + _grabOffset;
            UpdateHot(_dragging, e.position);
        }

        private void OnDragEnded(CardView card, PointerEventData e)
        {
            if (_dragging == null || card != _dragSource) return;
            _dragSource = null;
            CardView held = _dragging;
            Release(held, ZoneAt(held, e.position));
        }

        private void BeginHold(CardView card)
        {
            _dragging = card;
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
            card.Rect.localRotation = Quaternion.identity; // a held card is upright
            card.Rect.localScale = new Vector3(1.08f, 1.08f, 1f);
            card.group.alpha = 0.92f;
            if (card.Face.Aim == CardAim.Single)
            {
                receiver.Show(PreviewFor(card));
            }
            else
            {
                throwLine.Show(PreviewFor(card));
                ShowTargetMark(card);
            }
        }

        /// <summary>The throw line says where to release; the mark says who the card lands on.</summary>
        private void ShowTargetMark(CardView card)
        {
            if (!card.Face.Affects.HasValue)
            {
                Debug.LogError("[Depiction] card " + card.CardId + " is a throw-line card but the script gave no Affects");
                return;
            }
            FigureView figure = card.Face.Affects.Value == UnitSide.Player ? playerFigure : enemyFigure;
            if (!figure.targetMark)
            {
                Debug.LogError("[Depiction] " + figure.name + " has no targetMark; run Tools > Depiction > Upgrade Prefabs");
                return;
            }
            figure.targetMark.Show(CardView.KindColor(card.Face.Kind));
        }

        private void HideTargetMarks()
        {
            if (playerFigure.targetMark) playerFigure.targetMark.Hide();
            if (enemyFigure.targetMark) enemyFigure.targetMark.Hide();
        }

        private void SetZoneHot(CardView card, bool hot)
        {
            if (card.Face.Aim == CardAim.Single)
            {
                receiver.SetHot(hot);
                return;
            }
            throwLine.SetHot(hot);
            if (playerFigure.targetMark) playerFigure.targetMark.SetHot(hot);
            if (enemyFigure.targetMark) enemyFigure.targetMark.SetHot(hot);
        }

        /// <summary>The one predicted value comes from the source; a card it would refuse shows none.</summary>
        private string PreviewFor(CardView card)
        {
            return _source == null ? "" : _source.PreviewFor(card.CardId);
        }

        private DropZone ZoneAt(CardView card, Vector2 screenPoint)
        {
            if (card.Face.Aim == CardAim.Single) return receiver.Contains(screenPoint, null) ? DropZone.Receiver : DropZone.None;
            return throwLine.IsAbove(screenPoint, null) ? DropZone.AboveThrowLine : DropZone.None;
        }

        private void UpdateHot(CardView card, Vector2 screenPoint)
        {
            SetZoneHot(card, ZoneAt(card, screenPoint) != DropZone.None);
        }

        private void Release(CardView card, DropZone zone)
        {
            _dragging = null;
            receiver.Hide();
            throwLine.Hide();
            HideTargetMarks();
            PlayVerdict verdict = _source.TryPlay(card.CardId, zone, out DepictionEvent played);
            if (verdict == PlayVerdict.Accepted) StartCoroutine(PlayAccepted(card, played));
            else
            {
                ShowRefusal(card, verdict);
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
            _hand.Remove(card);
            Vector3 from = card.transform.position;
            Vector3 to = ev.Aim == CardAim.Single
                ? receiver.transform.position
                : from + new Vector3(0f, 140f * handArea.lossyScale.y, 0f); // a self card is tossed upward
            yield return UiTween.Run(160f, Ease.In, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, to, t);
                float s = Mathf.Lerp(1.08f, 0.5f, t);
                card.Rect.localScale = new Vector3(s, s, 1f);
                card.group.alpha = 1f - t;
            });
            Destroy(card.gameObject);
            StartCoroutine(SettleHand(180f));

            foreach (Cue cue in ev.Cues)
            {
                yield return PlayCue(ev, cue);
            }
            ApplyFrame(ev.After);
            float seconds = Time.unscaledTime - startedAt;
            EventSeconds.Add(seconds);
            Debug.Log("[Depiction] event " + ev.Order + " " + ev.Title + " settled in " + seconds.ToString("0.00") + " s");
            yield return Gate(ev.Order + "-end");
            yield return UiTween.Wait(250f);
            yield return RunAutomaticEvents();
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
            yield return UiTween.Run(180f, Ease.Out, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, _dragHome, t);
                float s = Mathf.Lerp(1.08f, 1f, t);
                card.Rect.localScale = new Vector3(s, s, 1f);
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(0f, homeDegrees, t));
            });
            if (card) card.group.alpha = 1f;
            if (shake && card) yield return UiTween.Shake(card.Rect, 8f, 2, 160f);
            LayoutHand();
            _busy = false;
            RefreshPlayableLook();
        }

        /// <summary>
        /// Debug only: performs the drag the source suggests, so a slice can be captured without a
        /// mouse. Only a fixed script suggests one; a live turn leaves the choice to the player.
        /// </summary>
        private IEnumerator AutoDrag()
        {
            yield return UiTween.Wait(400f);
            string suggested = _source.SuggestedCardId;
            if (string.IsNullOrEmpty(suggested)) yield break;
            CardView card = _hand.Find(c => c && c.CardId == suggested);
            if (card == null) yield break;
            CardAim aim = card.Face.Aim;
            BeginHold(card);
            Vector3 from = card.transform.position;
            Vector3 to = aim == CardAim.Single
                ? receiver.transform.position
                : new Vector3(from.x, throwLine.transform.position.y + (throwLine.transform.position.y - from.y) * 0.35f, from.z);
            yield return UiTween.Run(450f, Ease.InOut, t => { if (card) card.transform.position = Vector3.LerpUnclamped(from, to, t); });
            SetZoneHot(card, true);
            yield return Gate((EventSeconds.Count + 1) + "-hover");
            Release(card, DepictionText.RequiredZone(aim));
        }

        private Vector3 PointerWorld(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(handArea, e.position, e.pressEventCamera, out Vector3 world);
            return world;
        }

        // ---- debug gate ---------------------------------------------------------------------

        private IEnumerator Gate(string gateName)
        {
            bool capture = !string.IsNullOrEmpty(debugCaptureDir);
            if (!debugStepMode && !capture) yield break;
            float speed = UiTween.Speed;
            UiTween.Speed = 0f; // freezes every running tween so the frame can be captured
            DebugGate = gateName;
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
            UiTween.Speed = speed;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
#if ENABLE_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }
    }
}
#endif
