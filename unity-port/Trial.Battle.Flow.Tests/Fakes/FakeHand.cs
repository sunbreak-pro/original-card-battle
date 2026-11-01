// A stand-in for the hand module (hand.js, IHand) for the player's flow tests: it keeps the cards the player builds and
// binds, and every beat resolves after the nominal ms the player hands it (or the hand's own catalog ms) on env.Clock,
// as the real module's do. It records what the player asked of it.
using System;
using System.Collections.Generic;
using System.Linq;
using Depiction;

namespace Trial.Battle.Flow.Tests.Fakes
{
    public sealed class FakeHand : IHand
    {
        private readonly BattleEnv _env;
        private readonly EffectSwitches _switches = EffectSwitches.AllOn();
        private List<CardFace> _cards = new List<CardFace>();

        public FakeHand(BattleEnv env)
        {
            _env = env;
            env.Geom.HandCard = id =>
            {
                int i = _cards.FindIndex(c => c.Id == id);
                return i < 0 ? (StagePoint?)null : new StagePoint(560 + 120 * i, 960);
            };
        }

        /// <summary>The scene's forwarding of the hand's callbacks (BattleScene.js `late`): the player's HandCallbacks.</summary>
        public HandOpts Opts;

        public bool Interactable;
        public string Guide = "";
        public readonly List<string> Refusals = new List<string>();
        public int RefusalsHidden;
        public Action GrabLock;
        public readonly List<(string Id, bool Shake)> Returns = new List<(string, bool)>();
        public readonly List<string> Released = new List<string>();
        public readonly List<string> Dropped = new List<string>();
        /// <summary>Every DropCard with its scene time.</summary>
        public readonly List<(string Id, float At)> DropTimes = new List<(string, float)>();
        public readonly List<(string Id, DropZone Zone, int Unit)> AutoDrags = new List<(string, DropZone, int)>();
        public readonly List<string> ReleaseTraitWords = new List<string>();
        public int Cancels;
        public int LetGos;
        public bool Holding;
        public Func<string, bool> DimOf;
        public StatusMarks Marks;
        public int Builds;
        public int Binds;

        public IReadOnlyList<CardFace> Cards => _cards;

        public bool SameHand(IReadOnlyList<CardFace> faces)
        {
            if (faces == null || faces.Count != _cards.Count) return false;
            for (int i = 0; i < faces.Count; i++)
                if (faces[i].Id != _cards[i].Id) return false;
            return true;
        }

        public void Build(IReadOnlyList<CardFace> faces)
        {
            Builds++;
            _cards = faces != null ? faces.ToList() : new List<CardFace>();
        }

        public void Bind(IReadOnlyList<CardFace> faces)
        {
            Binds++;
            _cards = faces != null ? faces.ToList() : new List<CardFace>();
        }

        public void Layout() { }

        public void Clear() => _cards.Clear();

        public int Count() => _cards.Count;

        public IReadOnlyList<CardFace> Faces() => _cards.ToList();

        public void SetInteractable(bool on) => Interactable = on;

        public void RefreshPlayable(Func<string, bool> dimOf, string guide)
        {
            DimOf = dimOf;
            Guide = guide ?? "";
        }

        public void SetStatusMarks(StatusMarks marks) => Marks = marks;

        public bool IsHolding() => Holding;

        public CardFace HeldFace() => null;

        public CardFace HoveredFace() => null;

        public HandDebugState DebugState() => new HandDebugState { Interactable = Interactable, Held = Holding, Modes = Array.Empty<string>() };

        public bool HasSelection() => false;

        public bool DetailOpen() => false;

        public int FramedUnit() => -1;

        public IReadOnlyList<int> TaggedUnits() => Array.Empty<int>();

        /// <summary>Cards [first..] fly in one stagger apart; a draw that ran the pile out calls OnRebuild at RebuildStartMs.</summary>
        public Promise FlyDrawn(int first, int rebuildAfter, FlyDrawnOpts o)
        {
            int n = Math.Max(0, _cards.Count - first);
            if (n == 0) return Promise.Resolved();
            if (rebuildAfter >= 0 && o != null && o.OnRebuild != null)
            {
                Action onRebuild = o.OnRebuild;
                float at = EffectPlan.RebuildStartMs(rebuildAfter, _switches);
                (at > 0 ? _env.Clock.Wait(at) : Promise.Resolved()).Then(() => onRebuild()).Forget();
            }
            float total = EffectPlan.DrawCardStartMs(n - 1, rebuildAfter, _switches) + _switches.Ms(EffectId.CardDraw);
            return _env.Clock.Wait(total);
        }

        public Promise DiscardAll(StagePoint to)
        {
            int n = _cards.Count;
            if (n == 0) return Promise.Resolved();
            return _env.Clock.Wait(_switches.WaitMs(EffectId.HandDiscard, n)).Then(() => _cards.Clear());
        }

        public ReleaseResult Release(string cardId, DepictionEvent ev, StagePoint? to, ReleaseOpts o)
        {
            Released.Add(cardId);
            ReleaseTraitWords.Add(o != null ? o.TraitWord : "");
            CardFace face = _cards.Find(c => c.Id == cardId);
            _cards.RemoveAll(c => c.Id == cardId);
            StagePoint at = to ?? new StagePoint(960, 760);
            return new ReleaseResult
            {
                Done = _env.Clock.Wait(_switches.Ms(EffectId.CardRelease)),
                VanishedAt = () => at,
                Face = face,
                Keep = face != null && face.Kind == CardKind.Stance,
            };
        }

        public object GhostCard(CardFace face) => face;

        public void DropCard(string cardId)
        {
            Dropped.Add(cardId);
            DropTimes.Add((cardId, _env.Clock.Now));
        }

        public Promise Settle(float ms) => _env.Clock.Wait(ms);

        public Promise ReturnToHand(string cardId, bool shake)
        {
            Returns.Add((cardId, shake));
            return _env.Clock.Wait(_switches.Ms(EffectId.CardReturn) + (shake ? _switches.Ms(EffectId.RefusalShake) : 0f));
        }

        public void LetGo()
        {
            LetGos++;
            Holding = false;
        }

        public void SetBlocked(bool on, bool hover, Action onTry) { }

        public void SetGrabLock(Action onTry) => GrabLock = onTry;

        public void ShowRefusal(string text) => Refusals.Add(text);

        public void HideRefusal() => RefusalsHidden++;

        public Promise FadeAll(float ms) => _env.Clock.Wait(ms);

        public bool Select(int index, SelectOpts o = null) => false;

        public bool Confirm() => false;

        public int SelectedIndex() => -1;

        public bool CycleTarget(int dir) => false;

        public bool ToggleSelectedDetail() => false;

        public bool Cancel()
        {
            Cancels++;
            return false;
        }

        /// <summary>AutoDrag: held, carried L.AutoDragMs onto its zone, released through the same path (onHoldStart → onHoldEnd → onRelease).</summary>
        public Promise AutoDrag(string cardId, DropZone zone, int unit = -1)
        {
            AutoDrags.Add((cardId, zone, unit));
            CardFace face = _cards.Find(c => c.Id == cardId);
            Opts?.OnHoldStart?.Invoke(face, unit);
            Holding = true;
            return _env.Clock.Wait(L.AutoDragMs).Then(() =>
            {
                Holding = false;
                Opts?.OnHoldEnd?.Invoke(face);
                Opts?.OnRelease?.Invoke(cardId, zone, unit);
            });
        }

        public void OnPointerDown(BattlePointer p) { }
        public void OnPointerMove(BattlePointer p) { }
        public void OnPointerUp(BattlePointer p) { }
        public void OnPointerCancel() { }
        public void OnContext(BattlePointer p) { }
        public void OnLeave() { }
        public void OnBlur() { }
        public void Destroy() { }
    }
}
