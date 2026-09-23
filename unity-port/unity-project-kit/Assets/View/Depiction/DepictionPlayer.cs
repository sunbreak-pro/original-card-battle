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

        [Header("Guide")]
        [Tooltip("How long the reason a card came back stays on the guide line.")]
        public float refusalSeconds = 3.5f;

        [Header("Effects (#75-#77)")]
        [Tooltip("Effects to switch off, by id (EffectId: CardDraw, HandFan, CardHover, CardGrab, ReceiverShow, "
                 + "ThrowLineShow, ReceiverSnap, CardRelease, CardToDiscard, CardReturn, RefusalShake, HandDiscard, "
                 + "UnpayableDim). A switched-off effect settles at once and the battle goes on.")]
        public string[] effectsOff = new string[0];
        [Tooltip("Writes every effect that played (id, event, start, measured and nominal ms) to the log once the source finishes. For #78.")]
        public bool logEffectTrace;

        [Header("Mode")]
        [Tooltip("Filming only: replays TurnSliceScript in its written order, refusing any other card. "
                 + "Off by default, so the hand is free and the fight is played for real.")]
        public bool scriptedPlayback;

        [Header("Debug")]
        [Tooltip("Performs the source's suggested drags by itself. A fixed script suggests its next card; "
                 + "a core battle started with auto play suggests the leftmost card it can pay for.")]
        public bool autoPlayDrags;
        [Tooltip("With Auto Play Drags: ends the turn by itself once the source suggests no card. For unattended runs.")]
        public bool autoEndTurn;
        [Tooltip("Freezes at named gates (hover / impact / end) until DebugStep() is called. For screenshots.")]
        public bool debugStepMode;
        [Tooltip("Folder (absolute, or relative to the project) that receives one PNG per gate. Empty = no capture.")]
        public string debugCaptureDir = "";

        /// <summary>Name of the gate the playback is frozen at ("3-impact"), or empty.</summary>
        public string DebugGate { get; private set; } = "";
        public bool Finished { get; private set; }
        /// <summary>Seconds each event took from its start (or from the confirmed drop) to its settled frame.</summary>
        public readonly List<float> EventSeconds = new List<float>();
        /// <summary>Every effect that played or was switched off, with its measured length (#78 reads it).</summary>
        public EffectTrace Trace { get; private set; }
        /// <summary>Which effects play. Built from <see cref="effectsOff"/> in Start.</summary>
        public EffectSwitches Effects => _effects;

        private IDepictionSource _source;
        private IDepictionSource _givenSource;
        private readonly List<CardView> _hand = new List<CardView>();
        private bool _busy;
        private bool _stepRequested;
        private CardView _dragging;
        private Vector3 _dragHome;
        private Vector3 _grabOffset;
        private CardView _hovered;
        private readonly Dictionary<CardView, float> _hoverWeight = new Dictionary<CardView, float>();
        private Coroutine _refusal;
        private EffectSwitches _effects = EffectSwitches.AllOn();
        private Coroutine _zoneFade;
        private int _hoverHandle = -1;
        private Vector3 _dragPointerWorld;
        private bool _zoneHot;
        private float _snapWeight;
        private int _snapHandle = -1;

        /// <summary>How far a held card is drawn toward the dish once it is over it (EffectId.ReceiverSnap).</summary>
        private const float SnapPull = 0.35f;
        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
        /// <summary>Where the enemy figure stands between its moves; every motion returns it here.</summary>
        private Vector2 _enemyHome;
        private GameObject _resultCard;

        /// <summary>
        /// Hands the player the source to play instead of the two it can build by itself. Call it
        /// from Awake (BattleBootstrap does): Start reads it once.
        /// </summary>
        public void UseSource(IDepictionSource source)
        {
            _givenSource = source;
        }

        private void Start()
        {
            // Keep playing while the Editor is unfocused (captures and remote-driven checks rely on it).
            Application.runInBackground = true;
            EnsureEventSystem();
            _effects = EffectSwitches.WithOff(effectsOff, out List<string> unknownEffects);
            foreach (string name in unknownEffects) Debug.LogWarning("[Depiction] effectsOff: no effect is called \"" + name + "\"");
            Trace = new EffectTrace(() => Time.unscaledTimeAsDouble);
            UiTween.Speed = 1f; // a debug gate may have left it at 0 when domain reload is off
            if (backdrop && backdrop.sprite == null)
            {
                BattleTheme.FloorPalette palette = BattleTheme.Floor(1);
                backdrop.sprite = ProceduralArt.VerticalGradient(palette.Top, palette.Bottom);
                backdrop.color = Color.white;
            }
            if (stanceHintIcon && stanceHintIcon.sprite == null) stanceHintIcon.sprite = ProceduralArt.Shield;

            if (_givenSource != null) _source = _givenSource;
            else if (scriptedPlayback) _source = new DepictionRunner(TurnSliceScript.Build());
            else _source = new LiveTurn();
            ApplyFrame(_source.Frame);
            if (enemyFigure) _enemyHome = enemyFigure.Rect.anchoredPosition;
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
            UpdateSnap();
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
                if (logEffectTrace) Debug.Log("[Depiction] effects:\n" + Trace.ToCsv());
                BattleOutcome outcome = _source.Frame.Outcome;
                if (outcome != BattleOutcome.Ongoing)
                {
                    StartCoroutine(Effect(EffectId.ResultCard, ShowResult(outcome, _effects.Ms(EffectId.ResultCard)), () => ShowResultNow(outcome)));
                }
                yield break;
            }
            SetHandInteractable(true);
            if (autoPlayDrags) StartCoroutine(AutoDrag());
        }

        private IEnumerator PlayEvent(DepictionEvent ev)
        {
            float startedAt = Time.unscaledTime;
            if (ev.Kind == DepictionEventKind.TurnStart)
            {
                yield return Effect(EffectId.TurnBanner, Banner(DepictionText.YourTurn, BattleTheme.Accent, _effects.Ms(EffectId.TurnBanner)), null);
                // An omen already standing since the enemy's phase blinks once; a new one drops in with its cue.
                if (ev.After.Omen.Visible && !ev.Cues.Exists(c => c.Kind == CueKind.OmenShow))
                {
                    StartCoroutine(Effect(EffectId.OmenBlink, omenBadge.Blink(_effects.Ms(EffectId.OmenBlink)), null));
                }
            }
            foreach (Cue cue in ev.Cues)
            {
                yield return PlayCue(ev, cue);
            }
            if (ev.Kind == DepictionEventKind.TurnEnd)
            {
                yield return Effect(EffectId.EnemyTurnBanner, Banner(DepictionText.EnemyTurn, BattleTheme.Omen, _effects.Ms(EffectId.EnemyTurnBanner)), null);
            }
            // An enemy whose blow was all taken by Guard (or who only guarded) is still leaning in:
            // it steps back beside the next event instead of holding this one up.
            if (enemyFigure && enemyFigure.Rect.anchoredPosition != _enemyHome)
            {
                StartCoroutine(Effect(EffectId.EnemyReturn, ReturnHome(enemyFigure, _enemyHome, _effects.Ms(EffectId.EnemyReturn)), () => SnapHome(enemyFigure, _enemyHome)));
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
                    if (cue.Amount > 0)
                    {
                        // A gain lights its pips one by one and is waited for.
                        yield return Effect(EffectId.StaminaRecover,
                            status.LightPips(cue.StaminaAfter, cue.StaminaMax, _effects.Ms(EffectId.StaminaRecover)),
                            () => status.SetStamina(cue.StaminaAfter, cue.StaminaMax), cue.Amount);
                    }
                    else
                    {
                        // A spend pops beside the card's own beats.
                        status.SetStamina(cue.StaminaAfter, cue.StaminaMax);
                        if (status.pipRow && _effects.IsOn(EffectId.StaminaChange)) StartCoroutine(UiTween.Pop(status.pipRow, _effects.Ms(EffectId.StaminaChange)));
                    }
                    break;

                case CueKind.StatusChange:
                {
                    EffectId beat = StatusBeat.Of(cue);
                    yield return Effect(beat, status.PlayStatus(cue.Text, cue.StacksAfter, beat, _effects.Ms(beat)), () => status.SetStatus(cue.Text, cue.StacksAfter));
                    break;
                }

                case CueKind.DrawHand:
                    yield return DrawHand(ev.After.Hand, cue.Amount);
                    break;

                case CueKind.OmenShow:
                    omenBadge.Bind(ev.After.Omen);
                    yield return Effect(EffectId.OmenShow, omenBadge.PopIn(_effects.Ms(EffectId.OmenShow)), () => omenBadge.Rect.localScale = Vector3.one);
                    break;

                case CueKind.TraitFire:
                    yield return Effect(EffectId.TraitFire, FireTrait(labelAt, cue.Text), null);
                    break;

                case CueKind.Slash:
                    yield return Attack(ev, cue, target, status, chest);
                    break;

                case CueKind.GuardGain:
                    yield return Effect(EffectId.GuardGain, GainGuard(cue, status, chest, numberAt), () => status.SetGuard(cue.GuardAfter));
                    break;

                case CueKind.RangeSwitch:
                    // Moved by the other side (the polearm's shove): a knock-back, and the muted label says so.
                    if (cue.Pushed) FloatEffect(EffectId.PushMark, labelAt, DepictionText.Pushed, BattleTheme.Ink2, 36);
                    yield return Effect(EffectId.RangeSwitch, SwitchRange(target, cue.RangeAfter, cue.RangeGlyphAfter, cue.Pushed),
                        () => SnapRange(target, cue.RangeAfter, cue.RangeGlyphAfter));
                    break;

                case CueKind.StanceCue:
                    yield return Effect(EffectId.StanceCue, Reserve(cue, status, chest, numberAt), () => status.SetGuard(cue.GuardAfter));
                    yield return Gate(ev.Order + "-impact");
                    break;

                case CueKind.DiscardHand:
                    yield return DiscardHand();
                    break;

                case CueKind.EnemyWindup:
                    SetHandInteractable(false);
                    // The moment the enemy does what it foretold: the omen flares beside its move.
                    StartCoroutine(Effect(EffectId.OmenExecute, omenBadge.Flare(_effects.Ms(EffectId.OmenExecute)), null));
                    yield return Effect(EffectId.EnemyMotion, EnemyMotion(target, cue.System, _effects.Ms(EffectId.EnemyMotion)), null);
                    break;

                case CueKind.SideBonusMiss:
                    yield return Effect(EffectId.SideBonusMiss, StrikeOffBonus(cue), omenBadge.StrikeSideNow);
                    break;

                case CueKind.GuardBlock:
                {
                    // The Guard's number is its own colour, and a light stands in for the sound (#76).
                    status.SetGuard(cue.GuardAfter);
                    if (cue.GuardAfter == 0) StartCoroutine(Effect(EffectId.GuardBreak, status.CrackGuard(_effects.Ms(EffectId.GuardBreak)), null));
                    else if (status.guardBadge) StartCoroutine(UiTween.Pop(status.guardBadge, 260f));
                    FloatEffect(EffectId.GuardNumber, numberAt + new Vector2(-60f, 0f), "Guard −" + cue.Amount, BattleTheme.Guard, 44);
                    yield return Effect(EffectId.GuardBlock, DepictionFx.ShieldBlock(this, fxLayer, chest, cue.Amount.ToString(), () => Gate(ev.Order + "-block")), null);
                    break;
                }

                case CueKind.HpChange:
                {
                    // 出血 / 再生 / a heal / a 見切り return (#188): the cause and the number, then the bar.
                    string sign = cue.Amount > 0 ? "+" : "";
                    FloatEffect(EffectId.DamageNumber, numberAt, cue.Text + " " + sign + cue.Amount,
                        cue.Amount > 0 ? BattleTheme.Accent : BattleTheme.Omen, 40);
                    yield return DrainHp(status, cue.HpAfter);
                    break;
                }

                case CueKind.Hit:
                {
                    bool strong = EffectStrength.IsStrong(cue.Intensity);
                    FloatEffect(EffectId.DamageNumber, numberAt, cue.Amount.ToString(), BattleTheme.Omen, DepictionFx.NumberFont(cue.Intensity));
                    StartCoroutine(Effect(EffectId.HitFlash, DepictionFx.Flash(target.body, BattleTheme.Omen, _effects.Ms(EffectId.HitFlash)), null));
                    StartCoroutine(Effect(EffectId.TargetRecoil, Recoil(target, strong), null));
                    if (strong) StartCoroutine(Effect(EffectId.ScreenShake, ShakeArena(), null));
                    yield return DrainHp(status, cue.HpAfter);
                    yield return Gate(ev.Order + "-impact");
                    if (cue.Target == UnitSide.Player)
                    {
                        // The blow that lands on the player is the enemy's own: its omen is spent and
                        // it draws back. A blow the player lands does neither.
                        yield return Effect(EffectId.OmenSpend, omenBadge.FadeOut(_effects.Ms(EffectId.OmenSpend)), omenBadge.HideNow);
                        yield return Effect(EffectId.EnemyReturn, ReturnHome(enemyFigure, _enemyHome, _effects.Ms(EffectId.EnemyReturn)), () => SnapHome(enemyFigure, _enemyHome));
                    }
                    break;
                }
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
            bool strong = EffectStrength.IsStrong(cue.Intensity);
            FigureView attacker = byPlayer ? playerFigure : enemyFigure;
            Vector2 attackerHome = attacker.Rect.anchoredPosition;
            if (byPlayer) yield return Effect(EffectId.AttackLunge, Lunge(attacker, 46f, _effects.Ms(EffectId.AttackLunge)), null);
            Color streak = byPlayer ? BattleTheme.Ink : BattleTheme.Omen;
            yield return Effect(EffectId.StrikeShape,
                DepictionFx.Strike(this, fxLayer, chest, streak, cue.Intensity, !byPlayer, cue.System, _effects.Ms(EffectId.StrikeShape)), null);
            EffectId stop = strong ? EffectId.HitStopStrong : EffectId.HitStop;
            yield return Effect(stop, UiTween.Wait(_effects.Ms(stop)), null);
            if (strong) StartCoroutine(Effect(EffectId.ScreenShake, ShakeArena(), null));

            // A slash with no settled HP is the swing alone: the GuardBlock / Hit cues that follow
            // carry the numbers. That is every enemy slash, and any slash Guard takes a bite out of.
            if (cue.HpAfter == Cue.Unchanged) yield break;

            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Omen, 200f + 60f * cue.Intensity);
            FloatEffect(EffectId.DamageNumber, chest + new Vector2(0f, 40f), cue.Amount.ToString(), BattleTheme.Ink, DepictionFx.NumberFont(cue.Intensity));
            StartCoroutine(Effect(EffectId.HitFlash, DepictionFx.Flash(target.body, BattleTheme.White, _effects.Ms(EffectId.HitFlash)), null));
            StartCoroutine(Effect(EffectId.TargetRecoil, Recoil(target, strong), null));
            yield return DrainHp(status, cue.HpAfter);
            yield return Gate(ev.Order + "-impact");
            if (byPlayer)
            {
                yield return Effect(EffectId.AttackReturn, ReturnHome(attacker, attackerHome, _effects.Ms(EffectId.AttackReturn)), () => SnapHome(attacker, attackerHome));
            }
        }

        // ---- attack and defence beats (#76) -----------------------------------------------------

        /// <summary>The bar drops (blocking), then the grey band follows it down beside the flow.</summary>
        private IEnumerator DrainHp(StatusBarView status, int hp)
        {
            yield return Effect(EffectId.HpDrain, status.AnimateHp(hp, _effects.Ms(EffectId.HpDrain)), () => status.SetHpKeepingTrail(hp));
            StartCoroutine(Effect(EffectId.HpTrail, status.AnimateTrail(_effects.Ms(EffectId.HpTrail)), status.SnapTrail));
        }

        /// <summary>The struck figure leans away from the blow and shakes; a strong blow does both harder.</summary>
        private IEnumerator Recoil(FigureView figure, bool strong)
        {
            float ms = _effects.Ms(EffectId.TargetRecoil);
            RectTransform body = figure.body ? figure.body.rectTransform : null;
            // The player faces right and reels to the left; the enemy the other way round.
            float lean = (strong ? 12f : 7f) * (figure.side == UnitSide.Player ? 1f : -1f);
            StartCoroutine(UiTween.Shake(figure.Rect, strong ? 20f : 10f, 3, ms));
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (body) body.localRotation = Quaternion.Euler(0f, 0f, lean * Mathf.Sin(t * Mathf.PI));
            });
            if (body) body.localRotation = Quaternion.identity;
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
        /// with the placeholder art alone: 払 swings across, 突 drives straight in, 打 shoves with its
        /// weight, 盾 raises the haft; a step just steps. The lean stays until the enemy returns.
        /// </summary>
        private IEnumerator EnemyMotion(FigureView figure, StrikeSystem system, float ms)
        {
            RectTransform body = figure.body ? figure.body.rectTransform : null;
            float toward = figure.side == UnitSide.Player ? 1f : -1f;
            Vector2 from = figure.Rect.anchoredPosition;
            float reach = system == StrikeSystem.Thrust ? 90f
                : system == StrikeSystem.Sweep ? 40f
                : system == StrikeSystem.Strike ? 60f
                : system == StrikeSystem.Shield ? 0f
                : 70f;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!figure) return;
                float bell = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
                float rise = system == StrikeSystem.Shield ? 16f * bell : 0f;
                figure.Rect.anchoredPosition = from + new Vector2(reach * toward * t, rise);
                if (!body) return;
                switch (system)
                {
                    case StrikeSystem.Sweep: // wound back, then across
                        body.localRotation = Quaternion.Euler(0f, 0f, -toward * Mathf.Lerp(-18f, 14f, t) * bell);
                        body.localScale = new Vector3(1f + 0.08f * bell, 1f, 1f);
                        break;
                    case StrikeSystem.Thrust: // the whole body stretched along the line
                        body.localScale = new Vector3(1f + 0.14f * bell, 1f - 0.05f * bell, 1f);
                        break;
                    case StrikeSystem.Strike: // leaning its weight into the push
                        body.localRotation = Quaternion.Euler(0f, 0f, -toward * 10f * bell);
                        body.localScale = new Vector3(1f + 0.12f * bell, 1f - 0.08f * bell, 1f);
                        break;
                    case StrikeSystem.Shield: // the haft stood up
                        body.localScale = new Vector3(1f, 1f + 0.14f * bell, 1f);
                        break;
                }
            });
            if (body)
            {
                body.localRotation = Quaternion.identity;
                body.localScale = Vector3.one;
            }
        }

        private static IEnumerator ReturnHome(FigureView figure, Vector2 home, float ms)
        {
            if (!figure) yield break;
            yield return UiTween.Move(figure.Rect, figure.Rect.anchoredPosition, home, ms, Ease.InOut);
        }

        private static void SnapHome(FigureView figure, Vector2 home)
        {
            if (figure) figure.Rect.anchoredPosition = home;
        }

        private IEnumerator GainGuard(Cue cue, StatusBarView status, Vector2 chest, Vector2 numberAt)
        {
            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Guard, 220f + 40f * cue.Intensity);
            DepictionFx.FloatText(this, fxLayer, numberAt, "+" + cue.Amount, BattleTheme.Guard, DepictionFx.NumberFont(cue.Intensity));
            yield return status.PopGuard(cue.GuardAfter, _effects.Ms(EffectId.GuardGain));
        }

        private IEnumerator Reserve(Cue cue, StatusBarView status, Vector2 chest, Vector2 numberAt)
        {
            if (endTurn) yield return UiTween.Pop(endTurn, 200f);
            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Guard, 300f);
            DepictionFx.FloatText(this, fxLayer, numberAt, cue.Text, BattleTheme.Guard, 48);
            yield return status.PopGuard(cue.GuardAfter);
        }

        private IEnumerator FireTrait(Vector2 labelAt, string text)
        {
            DepictionFx.Burst(this, fxLayer, labelAt, BattleTheme.Warm, 260f);
            DepictionFx.FloatText(this, fxLayer, labelAt, text, BattleTheme.Warm, 40, rise: 70f);
            yield return UiTween.Wait(_effects.Ms(EffectId.TraitFire));
        }

        private IEnumerator StrikeOffBonus(Cue cue)
        {
            DepictionFx.FloatText(this, fxLayer, DepictionFx.PointOn(fxLayer, omenBadge.transform) + new Vector2(0f, -64f),
                cue.Text, BattleTheme.Whiff, 40, struck: true, rise: -50f);
            yield return omenBadge.StrikeSide(_effects.Ms(EffectId.SideBonusMiss));
        }

        // ---- status and turn beats (#77) -------------------------------------------------------

        /// <summary>A band across the arena with one line on it, faded in and out over <paramref name="ms"/>.</summary>
        private IEnumerator Banner(string text, Color color, float ms)
        {
            if (!fxLayer) yield break;
            Image plate = UiKit.Sprite(fxLayer, "Banner", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.55f),
                Half, Half, new Vector2(1920f, 120f), new Vector2(0f, 80f));
            plate.raycastTarget = false;
            Text label = UiKit.Text(plate.rectTransform, "Text", 56, TextAnchor.MiddleCenter, color, text);
            label.fontStyle = FontStyle.Bold;
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
            label.fontStyle = FontStyle.Bold;
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
            yield return UiTween.Move(figure.Rect, from, to, ms, Ease.Out);
        }

        private void SnapRange(FigureView figure, RangeSide range, string glyph)
        {
            RectTransform slot = range == RangeSide.Near ? playerNearSlot : playerFarSlot;
            if (slot) figure.transform.position = slot.position;
            figure.SetRange(true, glyph);
        }

        private IEnumerator SwitchRange(FigureView figure, RangeSide range, string glyph, bool pushed)
        {
            RectTransform slot = range == RangeSide.Near ? playerNearSlot : playerFarSlot;
            Vector3 from = figure.transform.position;
            Vector3 to = slot ? slot.position : from;
            // 320 ms to move, then the tag flips in 150 ms (battle_ui_ux_v2 §10.6). A push is a knock-back:
            // it starts fast and settles, where a step of one's own eases in and out.
            yield return UiTween.Run(320f, pushed ? Ease.Out : Ease.InOut, t => { if (figure) figure.transform.position = Vector3.LerpUnclamped(from, to, t); });
            yield return figure.FlipRangeTag(glyph, 150f);
        }

        /// <summary>
        /// Rebuilds the hand from the settled faces, and flies the <paramref name="drawn"/> newest cards
        /// (the right end of the fan) in from the draw pile one stagger apart (EffectId.CardDraw).
        /// </summary>
        private IEnumerator DrawHand(List<CardFace> faces, int drawn)
        {
            BuildHand(faces);
            int count = Mathf.Clamp(drawn, 0, _hand.Count);
            if (count == 0) yield break;
            yield return Effect(EffectId.CardDraw, FlyDrawn(_hand.Count - count), null, count);
        }

        private IEnumerator FlyDrawn(int first)
        {
            Vector3 from = deckPoint ? deckPoint.position : handArea.position;
            var flying = new List<(CardView card, Vector3 home)>();
            for (int i = first; i < _hand.Count; i++)
            {
                CardView card = _hand[i];
                flying.Add((card, card.transform.position));
                card.transform.position = from;
                card.group.alpha = 0f;
            }
            float ms = _effects.Ms(EffectId.CardDraw);
            float stagger = _effects.StaggerMs(EffectId.CardDraw);
            for (int i = 0; i < flying.Count; i++)
            {
                StartCoroutine(FlyCard(flying[i].card, from, flying[i].home, ms, 0f, 1f, Ease.Out));
                if (i < flying.Count - 1) yield return UiTween.Wait(stagger);
            }
            yield return UiTween.Wait(ms);
        }

        /// <summary>Turn end: the whole hand flies to the discard pile one stagger apart (EffectId.HandDiscard).</summary>
        private IEnumerator DiscardHand()
        {
            int count = _hand.Count;
            if (count > 0) yield return Effect(EffectId.HandDiscard, FlyDiscarded(), null, count);
            ClearHand();
        }

        private IEnumerator FlyDiscarded()
        {
            Vector3 to = discardPoint ? discardPoint.position : handArea.position;
            float ms = _effects.Ms(EffectId.HandDiscard);
            float stagger = _effects.StaggerMs(EffectId.HandDiscard);
            var cards = new List<CardView>(_hand);
            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                if (card) StartCoroutine(FlyCard(card, card.transform.position, to, ms, 1f, 0f, Ease.In));
                if (i < cards.Count - 1) yield return UiTween.Wait(stagger);
            }
            yield return UiTween.Wait(ms);
        }

        private static IEnumerator FlyCard(CardView card, Vector3 from, Vector3 to, float ms, float alphaFrom, float alphaTo, Ease ease)
        {
            yield return UiTween.Run(ms, ease, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, to, t);
                card.group.alpha = Mathf.Lerp(alphaFrom, alphaTo, t);
            });
        }

        /// <summary>
        /// The played card's ghost drifts from where the card vanished onto the discard pile
        /// (EffectId.CardToDiscard). It runs beside the event's beats and holds nothing up.
        /// </summary>
        private IEnumerator FlyGhostToDiscard(Vector2 from)
        {
            if (!fxLayer || !discardPoint) yield break;
            Vector2 to = DepictionFx.PointOn(fxLayer, discardPoint);
            Color tint = BattleTheme.WithAlpha(BattleTheme.Ink, 0.55f);
            Image ghost = UiKit.Sprite(fxLayer, "DiscardGhost", ProceduralArt.White, tint, Half, Half, new Vector2(76f, 104f), from);
            ghost.raycastTarget = false;
            RectTransform rt = ghost.rectTransform;
            yield return UiTween.Run(_effects.Ms(EffectId.CardToDiscard), Ease.InOut, t =>
            {
                if (!rt) return;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                ghost.color = BattleTheme.WithAlpha(tint, tint.a * (1f - t));
            });
            if (ghost) Destroy(ghost.gameObject);
        }

        // ---- effects (#75-#77) -----------------------------------------------------------------

        /// <summary>The event the running effect belongs to: the one playing, or the next one while the player acts.</summary>
        private int CurrentOrder => EventSeconds.Count + 1;

        /// <summary>
        /// Plays one effect by id. When it is on, <paramref name="play"/> runs and the trace times it.
        /// When it is off, <paramref name="settle"/> puts the end state on screen at once, the trace
        /// records the skip, and the caller goes on without waiting.
        /// </summary>
        private IEnumerator Effect(EffectId id, IEnumerator play, System.Action settle, int count = 1)
        {
            if (!_effects.IsOn(id))
            {
                settle?.Invoke();
                Trace?.Skip(id, CurrentOrder);
                yield break;
            }
            int handle = Trace != null ? Trace.Begin(id, CurrentOrder, count) : -1;
            yield return play;
            if (handle >= 0) Trace.End(handle);
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
                // EffectId.CardHover: timed from the moment a card starts to rise until it is fully up.
                _hoverHandle = -1;
                if (over && Trace != null)
                {
                    if (_effects.IsOn(EffectId.CardHover)) _hoverHandle = Trace.Begin(EffectId.CardHover, CurrentOrder);
                    else Trace.Skip(EffectId.CardHover, CurrentOrder);
                }
            }

            float hoverMs = _effects.Ms(EffectId.CardHover);
            float step = hoverMs > 0f ? Time.unscaledDeltaTime * 1000f / hoverMs : 1f;
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
            card.group.alpha = 0.92f;
            // EffectId.CardGrab: a held card stands upright and grows to 1.08.
            Quaternion tilt = card.Rect.localRotation;
            Vector3 scaleFrom = card.Rect.localScale;
            Vector3 heldScale = new Vector3(1.08f, 1.08f, 1f);
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
                receiver.Show(PreviewFor(card));
                _zoneFade = StartCoroutine(Effect(EffectId.ReceiverShow, receiver.FadeIn(_effects.Ms(EffectId.ReceiverShow)), null));
            }
            else
            {
                throwLine.Show(PreviewFor(card));
                _zoneFade = StartCoroutine(Effect(EffectId.ThrowLineShow, throwLine.FadeIn(_effects.Ms(EffectId.ThrowLineShow)), null));
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
            float next = ms > 0f ? Mathf.MoveTowards(_snapWeight, target, Time.unscaledDeltaTime * 1000f / ms) : target;
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

        private void Release(CardView card, DropZone zone)
        {
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
            yield return Effect(EffectId.CardRelease, UiTween.Run(_effects.Ms(EffectId.CardRelease), Ease.In, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, to, t);
                float s = Mathf.Lerp(1.08f, 0.5f, t);
                card.Rect.localScale = new Vector3(s, s, 1f);
                card.group.alpha = 1f - t;
            }), null);
            Vector2 vanishedAt = card.Face.Aim == CardAim.Single && receiver
                ? DepictionFx.PointOn(fxLayer, receiver.transform)
                : DepictionFx.PointOn(fxLayer, card.transform);
            Destroy(card.gameObject);
            StartCoroutine(Effect(EffectId.CardToDiscard, FlyGhostToDiscard(vanishedAt), null));
            StartCoroutine(Effect(EffectId.HandFan, SettleHand(_effects.Ms(EffectId.HandFan)), LayoutHand));

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
            Vector3 home = _dragHome;
            yield return Effect(EffectId.CardReturn, UiTween.Run(_effects.Ms(EffectId.CardReturn), Ease.Out, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, home, t);
                float s = Mathf.Lerp(1.08f, 1f, t);
                card.Rect.localScale = new Vector3(s, s, 1f);
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(0f, homeDegrees, t));
            }), null);
            if (card) card.group.alpha = 1f;
            if (shake && card) yield return Effect(EffectId.RefusalShake, UiTween.Shake(card.Rect, 8f, 2, _effects.Ms(EffectId.RefusalShake)), null);
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
