// DepictionPlayer's flying cards: the draw and the discard, then how long each effect holds the flow (#585).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public partial class DepictionPlayer
    {
        /// <summary>
        /// Rebuilds the hand from the settled faces, and flies the <paramref name="drawn"/> newest cards
        /// (the right end of the fan) in from the draw pile one stagger apart (EffectId.CardDraw).
        /// A draw that ran the pile out (<paramref name="rebuildAfter"/> ≥ 0, battle_ui_ux_v2 §3.14 /
        /// §5.1 順 6, #455) slides the discard pile onto the draw pile once the cards left on the old pile
        /// have gone, and the rest follow it (EffectPlan.DrawCardStartMs). The flow is held for the plan's
        /// PileRebuild and CardDraw waits in turn, and the draw ends on their sum.
        /// </summary>
        private IEnumerator DrawHand(List<CardFace> faces, int drawn, int rebuildAfter)
        {
            bool rebuild = drawn > 0 && rebuildAfter >= 0;
            if (rebuild && !(_effects.IsOn(EffectId.PileRebuild) && _effects.IsOn(EffectId.CardDraw)))
            {
                // With either beat off there is no draw to slide into: the slide (or its skip) comes first.
                yield return HoldBeat(EffectId.PileRebuild, SlideDiscardOntoDraw(), null);
                rebuild = false;
            }
            BuildHand(faces);
            int count = Mathf.Clamp(drawn, 0, _hand.Count);
            if (count == 0) yield break;
            if (!rebuild)
            {
                yield return HoldBeat(EffectId.CardDraw, FlyDrawn(_hand.Count - count, Cue.Unchanged), null, count);
                yield break;
            }
            bool flown = false;
            StartCoroutine(Then(FlyDrawn(_hand.Count - count, rebuildAfter), () => flown = true));
            yield return HoldBeat(EffectId.PileRebuild, Nothing(), null);
            yield return HoldBeat(EffectId.CardDraw, Until(() => flown), null, count);
        }

        private IEnumerator FlyDrawn(int first, int rebuildAfter)
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
            float clock = 0f;
            for (int i = 0; i < flying.Count; i++)
            {
                if (i == rebuildAfter)
                {
                    // The old pile is empty: the discard pile slides onto it, and the next card leaves the new one.
                    float slideAt = EffectPlan.RebuildStartMs(rebuildAfter, _effects);
                    yield return UiTween.Wait(slideAt - clock);
                    clock = slideAt;
                    StartCoroutine(SlideDiscardOntoDraw());
                }
                float startAt = EffectPlan.DrawCardStartMs(i, rebuildAfter, _effects);
                yield return UiTween.Wait(startAt - clock);
                clock = startAt;
                StartCoroutine(FlyCard(flying[i].card, from, flying[i].home, ms, 0f, 1f, Ease.Out));
            }
            yield return UiTween.Wait(ms);
        }

        private static IEnumerator Then(IEnumerator play, System.Action done)
        {
            yield return play;
            done();
        }

        private static IEnumerator Nothing()
        {
            yield break;
        }

        private static IEnumerator Until(System.Func<bool> done)
        {
            while (!done()) yield return null;
        }

        /// <summary>Turn end: the whole hand flies to the discard pile one stagger apart (EffectId.HandDiscard).</summary>
        private IEnumerator DiscardHand()
        {
            int count = _hand.Count;
            if (count > 0) yield return HoldBeat(EffectId.HandDiscard, FlyDiscarded(), null, count);
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
        /// The played card's ghost drifts from where the card vanished onto the pile the core put it in
        /// (DepictionEvent.PlayedTo): the discard pile (EffectId.CardToDiscard) at once, or for a stance
        /// card the exile pile, 220 ms in on the beat 600 counted from the release's start
        /// (EffectId.CardToExile, battle_ui_ux_v2 §5.11 順 4, #455). The release has already run its
        /// CardRelease length, so only the rest of the 600 is waited, and nothing is drawn while it is.
        /// It runs beside the event's beats and holds nothing up.
        /// </summary>
        private IEnumerator GhostToPile(Vector2 from, PileKind pile)
        {
            bool exiled = pile == PileKind.Exile;
            if (exiled) yield return UiTween.Wait(Mathf.Max(0f, EffectCatalog.CardToExileDelayMs - _effects.Ms(EffectId.CardRelease)));
            yield return Effect(exiled ? EffectId.CardToExile : EffectId.CardToDiscard, FlyGhostTo(from, pile), null);
        }

        private IEnumerator FlyGhostTo(Vector2 from, PileKind pile)
        {
            bool exiled = pile == PileKind.Exile;
            if (!fxLayer) yield break;
            Vector2 to;
            if (exiled && _piles != null && _piles.Shown) to = PointOnFx(_piles.MiddleOf(PileKind.Exile));
            else if (!exiled && discardPoint) to = DepictionFx.PointOn(fxLayer, discardPoint);
            else yield break;
            EffectId id = exiled ? EffectId.CardToExile : EffectId.CardToDiscard;
            Color tint = BattleTheme.WithAlpha(BattleTheme.Ink, 0.55f);
            Image ghost = UiKit.Sprite(fxLayer, exiled ? "ExileGhost" : "DiscardGhost", ProceduralArt.White, tint, Half, Half, new Vector2(76f, 104f), from);
            ghost.raycastTarget = false;
            RectTransform rt = ghost.rectTransform;
            yield return UiTween.Run(_effects.Ms(id), exiled ? Ease.In : Ease.InOut, t =>
            {
                if (!rt) return;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                ghost.color = BattleTheme.WithAlpha(tint, tint.a * (1f - t));
            });
            if (ghost) Destroy(ghost.gameObject);
        }

        /// <summary>A world point as an offset from the fx layer's centre, as DepictionFx.PointOn gives a transform's.</summary>
        private Vector2 PointOnFx(Vector3 world)
        {
            Vector2 local = fxLayer.InverseTransformPoint(world);
            return local - fxLayer.rect.center;
        }

        /// <summary>
        /// battle_ui_ux_v2 §3.14 山札切れ (#455): the discard pile slides onto the draw pile in 200 ms (out)
        /// in the middle of the draw, once the old pile is empty. A card back of the piles' 64×84 crosses
        /// from one to the other; the piles' own thickness follows with the event's settled frame. The
        /// exile pile does not move.
        /// </summary>
        private IEnumerator SlideDiscardOntoDraw()
        {
            if (!fxLayer || _piles == null || !_piles.Shown) yield break;
            Vector2 from = PointOnFx(_piles.MiddleOf(PileKind.Discard));
            Vector2 to = PointOnFx(_piles.MiddleOf(PileKind.Draw));
            RectTransform sheet = UiKit.Point(fxLayer, "RebuildSheet", Half, Half, new Vector2(PileLayout.Width, PileLayout.Height), from);
            VisualArt.Panel(sheet, "Back", BattleTheme.CardGround, 5);
            VisualArt.Ring(sheet, "Edge", BattleTheme.Steel, 5, 1);
            yield return UiTween.Run(_effects.Ms(EffectId.PileRebuild), Ease.Out, t =>
            {
                if (sheet) sheet.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            });
            if (sheet) Destroy(sheet.gameObject);
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

        // ---- the plan's waits (#495) ------------------------------------------------------------

        /// <summary>The waits EffectPlan lists for the event playing now, taken in order by <see cref="HoldBeat"/>.</summary>
        private readonly Queue<EffectStep> _planned = new Queue<EffectStep>();
        /// <summary>The event <see cref="_planned"/> belongs to, or null between events.</summary>
        private DepictionEvent _plannedEvent;

        /// <summary>
        /// An event starts: the flow will be held at exactly the steps EffectPlan.StepsOf lists for it,
        /// the same steps the EditMode tests add up against the 2.0 s cap.
        /// </summary>
        private void BeginPlan(DepictionEvent ev)
        {
            _planned.Clear();
            _plannedEvent = ev;
            foreach (EffectStep step in EffectPlan.StepsOf(ev)) _planned.Enqueue(step);
        }

        /// <summary>An event has played its beats. A planned wait the screen never held means the two have drifted apart.</summary>
        private void EndPlan()
        {
            if (_plannedEvent != null && _planned.Count > 0)
            {
                var left = new List<string>();
                foreach (EffectStep step in _planned) left.Add(step.Id + " x" + step.Count);
                Debug.LogError("[Depiction] event " + _plannedEvent.Order + " " + _plannedEvent.Title
                    + ": EffectPlan lists waits the screen did not hold: " + string.Join(", ", left));
            }
            _planned.Clear();
            _plannedEvent = null;
        }

        /// <summary>The plan's next step, which must be the beat the screen is about to wait for.</summary>
        private EffectStep TakePlanned(EffectId id, int count)
        {
            if (_plannedEvent == null) return new EffectStep(id, count); // a beat outside any event: nothing to hold it to
            string where = "[Depiction] event " + _plannedEvent.Order + " " + _plannedEvent.Title + ": ";
            if (_planned.Count == 0 || _planned.Peek().Id != id)
            {
                Debug.LogError(where + "the screen waits for " + id + " where EffectPlan lists "
                    + (_planned.Count > 0 ? _planned.Peek().Id.ToString() : "nothing more"));
                return new EffectStep(id, count);
            }
            EffectStep planned = _planned.Dequeue();
            if (planned.Count != count)
            {
                Debug.LogError(where + id + " plays " + count + " items where EffectPlan lists " + planned.Count);
            }
            return planned;
        }

        /// <summary>
        /// Plays one beat the event waits for (#495). The flow is held for the plan's wait for this step
        /// (EffectPlan.WaitMs: the number EffectPlan.BlockingMs adds up for the tests) while
        /// <paramref name="play"/> runs beside it. The animation is built from the same catalog length;
        /// when it runs a frame or so past the wait (UiTween ends on the first frame after its length),
        /// the flow lets it finish, so nothing on screen is cut short. A step the plan gives a lead
        /// (EffectStep.LeadMs, §5.4, #499) holds the flow for the lead only and plays on beside what
        /// follows. Off, the beat settles at once and holds nothing up, like <see cref="Effect"/>.
        /// <paramref name="played"/> runs once the beat has played out (or settled), lead or not.
        /// Beats that run beside the flow keep using Effect.
        /// </summary>
        private IEnumerator HoldBeat(EffectId id, IEnumerator play, System.Action settle, int count = 1, System.Action played = null)
        {
            EffectStep step = TakePlanned(id, count);
            if (!_effects.IsOn(id))
            {
                settle?.Invoke();
                Trace?.Skip(id, CurrentOrder);
                played?.Invoke();
                yield break;
            }
            int handle = Trace != null ? Trace.Begin(id, CurrentOrder, step.Count) : -1;
            if (step.LeadMs.HasValue)
            {
                StartCoroutine(PlayOn(play, handle, played));
                yield return UiTween.Wait(EffectPlan.WaitMs(step, _effects));
                yield break;
            }
            bool waited = false;
            StartCoroutine(WaitThen(EffectPlan.WaitMs(step, _effects), () => waited = true));
            yield return play;
            while (!waited) yield return null;
            if (handle >= 0) Trace.End(handle);
            played?.Invoke();
        }

        /// <summary>The rest of a beat with a lead, beside the flow: it plays out, then is timed and handed on.</summary>
        private IEnumerator PlayOn(IEnumerator play, int handle, System.Action played)
        {
            yield return play;
            if (handle >= 0) Trace.End(handle);
            played?.Invoke();
        }

        private static IEnumerator WaitThen(float ms, System.Action done)
        {
            yield return UiTween.Wait(ms);
            done();
        }

        /// <summary>The settled event in the log, beside what the plan says it should take at the speed in force.</summary>
        private void LogSettled(DepictionEvent ev, float seconds)
        {
            float planned = EffectPlan.BlockingMs(ev, _effects, _speed);
            Debug.Log("[Depiction] event " + ev.Order + " " + ev.Title + " settled in " + seconds.ToString("0.00")
                + " s (EffectPlan " + planned.ToString("0") + " ms)");
        }
    }
}
#endif
