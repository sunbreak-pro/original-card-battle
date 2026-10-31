// The flow tests' rig: a real CoreBattleSource (wrapped in a recorder that notes every call and the scene time of every
// event it hands out), the fake hand / field / hud, a recording sound and log, a greedy chooser for ctx.Auto, and a
// driver that steps the battle clock 1 ms a frame (scene time = real time at ×1.25, so every wait lands exactly).
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;

namespace Trial.Battle.Flow.Tests.Fakes
{
    /// <summary>ctx.audio: every play, with the real time and the player's event count it came in.</summary>
    public sealed class FakeAudio : IBattleAudio
    {
        public Func<int> EventIndex = () => 0;
        public readonly List<(string Name, float Delay, int Event)> Plays = new List<(string, float, int)>();

        public void Play(string name, float delayMs, float? mult) => Plays.Add((name, delayMs, EventIndex()));
    }

    /// <summary>env.Log: every console.error.</summary>
    public sealed class FakeLog : IBattleLog
    {
        public readonly List<string> Errors = new List<string>();

        public void Error(string message, Exception error = null) => Errors.Add((message ?? "") + (error != null ? " " + error : ""));
    }

    /// <summary>
    /// The save recorder's place (ctx.source): forwards every call to the battle and notes it — the state-changing calls
    /// with the scene time and the event they returned.
    /// </summary>
    public sealed class RecordingSource : ITrialSource
    {
        private readonly ITrialSource _inner;
        private readonly Func<float> _now;

        public RecordingSource(ITrialSource inner, Func<float> now)
        {
            _inner = inner;
            _now = now;
        }

        public ITrialSource Inner => _inner;

        /// <summary>Every event handed out, in order, with the call that took it and the scene time.</summary>
        public readonly List<(DepictionEvent Ev, string Call, float At)> Events = new List<(DepictionEvent, string, float)>();

        /// <summary>Every state-changing call ("AdvanceAuto", "EndTurn", "TryPlay:id:zone:target", "TakeFreeStep:dir").</summary>
        public readonly List<string> Calls = new List<string>();

        /// <summary>When set, the next AdvanceAuto throws this (the bridge-bug path).</summary>
        public Exception FailNext;

        private DepictionEvent Note(DepictionEvent ev, string call)
        {
            if (ev != null) Events.Add((ev, call, _now()));
            return ev;
        }

        public DepictionFrame Frame => _inner.Frame;
        public bool Finished => _inner.Finished;
        public bool WaitingForPlayer => _inner.WaitingForPlayer;
        public bool CanEndTurn => _inner.CanEndTurn;
        public string SuggestedCardId => _inner.SuggestedCardId;
        public string GuideText => _inner.GuideText;
        public BattleState State => _inner.State;
        public IReadOnlyList<BattleEvent> History => _inner.History;
        public string AutoCardId => _inner.AutoCardId;

        public DepictionEvent AdvanceAuto()
        {
            Calls.Add("AdvanceAuto");
            if (FailNext != null)
            {
                Exception e = FailNext;
                FailNext = null;
                throw e;
            }
            return Note(_inner.AdvanceAuto(), "AdvanceAuto");
        }

        public DepictionEvent EndTurn()
        {
            Calls.Add("EndTurn");
            return Note(_inner.EndTurn(), "EndTurn");
        }

        public PlayVerdict Inspect(string cardId) => _inner.Inspect(cardId);
        public PlayVerdict Inspect(string cardId, int target) => _inner.Inspect(cardId, target);

        public PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)
        {
            Calls.Add("TryPlay:" + cardId + ":" + zone);
            PlayVerdict v = _inner.TryPlay(cardId, zone, out played);
            Note(played, "TryPlay");
            return v;
        }

        public PlayVerdict TryPlay(string cardId, DropZone zone, int target, out DepictionEvent played)
        {
            Calls.Add("TryPlay:" + cardId + ":" + zone + ":" + target);
            PlayVerdict v = _inner.TryPlay(cardId, zone, target, out played);
            Note(played, "TryPlay");
            return v;
        }

        public string PreviewFor(string cardId) => _inner.PreviewFor(cardId);
        public string PreviewFor(string cardId, int target) => _inner.PreviewFor(cardId, target);
        public string RefusalText(string cardId, PlayVerdict verdict) => _inner.RefusalText(cardId, verdict);
        public string RefusalText(string cardId, PlayVerdict verdict, int target) => _inner.RefusalText(cardId, verdict, target);
        public int TargetFor(string cardId) => _inner.TargetFor(cardId);
        public bool CanTakeFreeStep(int? dir = null) => _inner.CanTakeFreeStep(dir);

        public DepictionEvent TakeFreeStep(int dir)
        {
            Calls.Add("TakeFreeStep:" + dir);
            return Note(_inner.TakeFreeStep(dir), "TakeFreeStep");
        }
    }

    /// <summary>
    /// The dev page's chooser (the ported GreedyPlayer's place): of every card the core lets the player play now on every
    /// standing enemy, the one whose preview takes most HP, then has most power, then gives most Guard (the earlier card
    /// and enemy on a tie), at most MaxPlays a turn; ends the turn when none. 俊敏's step: forward when open, else back.
    /// </summary>
    public static class GreedyAuto
    {
        public static BattleAuto Make(int maxPlays = 4, bool endTurnsOnly = false, int freeStep = 1) => new BattleAuto
        {
            Choose = (source, played) => endTurnsOnly ? null : Choose((ITrialSource)source, played, maxPlays),
            FreeStep = source =>
            {
                var t = (ITrialSource)source;
                if (freeStep != 0 && t.CanTakeFreeStep(freeStep)) return freeStep;
                if (freeStep != 0 && t.CanTakeFreeStep(-freeStep)) return -freeStep;
                return 0;
            },
        };

        public static BattleAutoPick Choose(ITrialSource source, int played, int maxPlays)
        {
            if (played >= maxPlays) return null;
            BattleState state = source.State;
            bool multi = state.Enemies.Count > 1;
            BattleAutoPick best = null;
            long bestScore = long.MinValue;
            foreach (CardInstance card in state.Hand)
            {
                foreach (int target in state.Living)
                {
                    if (TurnLoop.CanPlay(state, card.InstanceId, target) != PlayRefusal.None) continue;
                    PlayPreview preview = TurnLoop.Preview(state, card.InstanceId, target);
                    long score = preview == null ? 0 : preview.Damage * 10000L + preview.RawPower * 100L + preview.GuardGain;
                    if (best == null || score > bestScore)
                    {
                        best = new BattleAutoPick(card.InstanceId, multi ? target : -1);
                        bestScore = score;
                    }
                }
            }
            return best;
        }
    }

    /// <summary>The scene's part the tests need: env, the three fakes, the player, and a frame driver.</summary>
    public sealed class Rig
    {
        public readonly BattleEnv Env;
        public readonly FakeHand Hand;
        public readonly FakeField Field;
        public readonly FakeHud Hud;
        public readonly FakeAudio Audio = new FakeAudio();
        public readonly FakeLog Log = new FakeLog();
        public readonly BattleCtx Ctx;
        public readonly RecordingSource Source;
        public readonly BattlePlayer Player;
        public int Finished;
        public readonly List<Exception> SourceErrors = new List<Exception>();
        public readonly List<(int Index, float At)> EventEnds = new List<(int, float)>();

        /// <param name="source">The battle: a trial source is wrapped in a RecordingSource (Source); any other (a fixed script) is handed as it is.</param>
        public Rig(IDepictionSource source, BattleAuto auto = null, BattleChain chain = null, BattleSettings settings = null)
        {
            Env = new BattleEnv(settings ?? new BattleSettings(), Audio, 1, BattleNodeKind.Battle, null, Log);
            Hand = new FakeHand(Env);
            Field = new FakeField(Env);
            Hud = new FakeHud(Env);
            Source = source is ITrialSource trial ? new RecordingSource(trial, () => Env.Clock.Now) : null;
            Ctx = new BattleCtx
            {
                Source = (IDepictionSource)Source ?? source,
                Chain = chain,
                Auto = auto,
                OnFinished = () => Finished++,
                OnError = e => SourceErrors.Add(e),
            };
            Player = new BattlePlayer(Env, Hand, Field, Hud, Ctx);
            Hand.Opts = Player.HandCallbacks;
            Audio.EventIndex = () => Player.EventEnds;
            Player.OnEventEnd(() => EventEnds.Add((Player.EventEnds, Env.Clock.Now)));
        }

        public static CoreBattleSource Launch(string enemyId, int seed, int cells = 6, IReadOnlyList<string> more = null, int? startHp = null,
            List<CardInstance> deck = null, int chainIndex = 1, int chainTotal = 1, int startGap = BattleLaunch.DefaultStartGap)
        {
            return new BattleLaunch
            {
                EnemyId = enemyId, Seed = seed, FieldCells = cells, MoreEnemyIds = more != null ? more.ToList() : null, StartHp = startHp,
                Deck = deck, ChainIndex = chainIndex, ChainTotal = chainTotal, StartGap = startGap,
            }.CreateSource();
        }

        /// <summary>Starts the player and drains what the start posted.</summary>
        public void Start()
        {
            Player.Start();
            Microtasks.Drain();
        }

        /// <summary>Steps the clock `ms` frames of 1 ms.</summary>
        public void Run(float ms, float dt = 1f)
        {
            for (float t = 0; t < ms; t += dt) Env.Clock.Step(dt);
        }

        /// <summary>Steps 1 ms frames until `until` holds (true) or `maxMs` of real time ran out (false).</summary>
        public bool RunUntil(Func<bool> until, float maxMs = 3_600_000f, float dt = 1f)
        {
            float start = Env.Real.Now;
            while (!until())
            {
                if (Env.Real.Now - start > maxMs) return false;
                Env.Clock.Step(dt);
            }
            return true;
        }

        /// <summary>Calls an input path (the scene's handler) and drains, as the scene does after every input.</summary>
        public T Input<T>(Func<T> call)
        {
            T r = call();
            Microtasks.Drain();
            return r;
        }

        public void Input(Action call)
        {
            call();
            Microtasks.Drain();
        }
    }
}
