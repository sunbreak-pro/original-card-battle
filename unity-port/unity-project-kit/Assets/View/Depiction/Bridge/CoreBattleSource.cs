// The battle the v4.2 core runs, offered to the screen as an IDepictionSource. Pure C#:
// BattleCore + Depiction.Script, no UnityEngine.
//
// This is the third source beside LiveTurn (the §18 draft rules written by hand) and
// DepictionRunner (the fixed script kept for filming). It holds no rule of its own: TurnLoop moves
// the battle, CoreScriptWriter turns what happened into screen events, and this class only decides
// which of the two the screen is waiting for. It does not read DemoDeck.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public sealed class CoreBattleSource : IDepictionSource
    {
        private readonly IRng _rng;
        private readonly CoreScriptWriter _writer;
        private readonly Queue<DepictionEvent> _pending = new Queue<DepictionEvent>();
        private readonly bool _suggestCards;
        private readonly int _stopAfterTurns;
        private BattleState _state;

        /// <param name="setup">Who fights whom, with which deck.</param>
        /// <param name="seed">Fixes every shuffle, so the same seed replays the same battle here and under `dotnet test`.</param>
        /// <param name="suggestCards">
        /// Unattended runs only (captures, the PlayMode test): suggest the leftmost card that can be
        /// paid for, and nothing once none can. A played battle leaves the choice to the player.
        /// </param>
        /// <param name="stopAfterTurns">Unattended runs only: report Finished once this many turns have closed on their next omen. 0 = fight to the end.</param>
        public CoreBattleSource(BattleSetup setup, int seed, bool suggestCards = false, int stopAfterTurns = 0)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (stopAfterTurns < 0) throw new ArgumentOutOfRangeException(nameof(stopAfterTurns), stopAfterTurns, "0 or more.");
            _suggestCards = suggestCards;
            _stopAfterTurns = stopAfterTurns;
            _rng = new SeededRng(seed);
            _writer = new CoreScriptWriter(setup.Enemy);
            _state = TurnLoop.Start(setup, _rng).State;
            Frame = _writer.Opening(_state);
        }

        /// <summary>The slice: the polearm against the ten-kind prototype deck.</summary>
        public static CoreBattleSource Slice(int seed)
        {
            return new CoreBattleSource(BattleSetup.Slice(), seed);
        }

        /// <summary>The core state behind the screen. For tests and tools; the View never reads it.</summary>
        public BattleState State => _state;

        public DepictionFrame Frame { get; private set; }

        public bool Finished => _pending.Count == 0 && (_state.Result != GameResult.Ongoing || ReachedTurnLimit);

        public bool WaitingForPlayer =>
            _pending.Count == 0 && _state.Result == GameResult.Ongoing && _state.Phase == BattlePhase.PlayerAction;

        /// <summary>The turn limit is only looked at between turns, so a limited run still ends on a next omen.</summary>
        private bool ReachedTurnLimit =>
            _stopAfterTurns > 0 && _state.Phase == BattlePhase.AwaitingTurnStart && _state.Turn >= _stopAfterTurns;

        public bool CanEndTurn => WaitingForPlayer;

        /// <summary>A played battle has no opinion about which card to drag; an unattended run takes the leftmost payable one.</summary>
        public string SuggestedCardId
        {
            get
            {
                if (!_suggestCards || !WaitingForPlayer) return "";
                foreach (CardInstance card in _state.Hand)
                {
                    if (TurnLoop.CanPlay(_state, card.InstanceId) == PlayRefusal.None) return card.InstanceId;
                }
                return "";
            }
        }

        public string GuideText
        {
            get
            {
                if (_pending.Count > 0) return "";
                if (_state.Result == GameResult.Won) return "敵を討ち取りました";
                if (_state.Result == GameResult.Lost) return "力尽きました";
                if (ReachedTurnLimit) return "";
                if (!WaitingForPlayer) return "";
                return HasPlayableCard()
                    ? "出したい札を離してください。手が済んだら「ターン終了」"
                    : "出せる札がありません。「ターン終了」を押してください";
            }
        }

        public DepictionEvent AdvanceAuto()
        {
            if (_pending.Count == 0)
            {
                if (Finished) throw new InvalidOperationException("The battle is over.");
                if (_state.Phase != BattlePhase.AwaitingTurnStart) throw new InvalidOperationException("The turn waits for the player.");
                Take(TurnLoop.BeginPlayerTurn(_state, _rng));
            }
            return Next();
        }

        public DepictionEvent EndTurn()
        {
            if (!CanEndTurn) throw new InvalidOperationException("The turn cannot be ended right now.");
            Take(TurnLoop.EndTurn(_state, _rng));
            return Next();
        }

        public PlayVerdict Inspect(string cardId)
        {
            if (!WaitingForPlayer) return PlayVerdict.NotWaiting;
            switch (TurnLoop.CanPlay(_state, cardId))
            {
                case PlayRefusal.None: return PlayVerdict.Accepted;
                case PlayRefusal.NotInHand: return PlayVerdict.NotInHand;
                case PlayRefusal.NotEnoughStamina: return PlayVerdict.NotEnoughStamina;
                default: return PlayVerdict.NotWaiting;
            }
        }

        public PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)
        {
            played = null;
            PlayVerdict verdict = Inspect(cardId);
            if (verdict != PlayVerdict.Accepted) return verdict;

            CardFace face = DepictionText.Find(Frame.Hand, cardId);
            if (face == null) return PlayVerdict.NotInHand;
            if (zone != DepictionText.RequiredZone(face.Aim)) return PlayVerdict.WrongZone;

            Take(TurnLoop.PlayCard(_state, cardId, _rng));
            played = Next();
            return PlayVerdict.Accepted;
        }

        /// <summary>
        /// The one number shown while a card is held. Provisional shape (the final one is #30): the
        /// damage that would get past the enemy's Guard, or the Guard a card without an attack gives.
        /// </summary>
        public string PreviewFor(string cardId)
        {
            if (Inspect(cardId) != PlayVerdict.Accepted) return "";
            PlayPreview preview = TurnLoop.Preview(_state, cardId);
            if (preview == null) return "";
            if (preview.Attacks) return preview.Damage.ToString();
            return preview.GuardGain > 0 ? preview.GuardGain.ToString() : "";
        }

        public string RefusalText(string cardId, PlayVerdict verdict)
        {
            CardFace face = DepictionText.Find(Frame.Hand, cardId);
            if (face == null) return "";
            switch (verdict)
            {
                case PlayVerdict.NotEnoughStamina:
                    return "「" + face.Name + "」はスタミナ " + face.Cost + " が要ります。残りは " + Frame.Player.Stamina + " です";
                case PlayVerdict.WrongZone:
                    return "「" + face.Name + "」は" + DepictionText.ZoneName(face.Aim) + "で離すと出せます";
                default:
                    return "";
            }
        }

        private void Take(StepResult step)
        {
            _state = step.State;
            foreach (DepictionEvent ev in _writer.Write(step.Events, _state)) _pending.Enqueue(ev);
        }

        private DepictionEvent Next()
        {
            DepictionEvent ev = _pending.Dequeue();
            Frame = ev.After;
            return ev;
        }

        private bool HasPlayableCard()
        {
            foreach (CardInstance card in _state.Hand)
            {
                if (TurnLoop.CanPlay(_state, card.InstanceId) == PlayRefusal.None) return true;
            }
            return false;
        }
    }
}
