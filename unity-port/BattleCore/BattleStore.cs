using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Logic-layer driver for the pure battle core (the C# analogue of React's
    /// <c>useReducer</c>): holds the current <see cref="BattleState"/>, funnels every
    /// <see cref="BattleAction"/> through <see cref="BattleReducer.Reduce"/>, and
    /// notifies subscribers when the state actually changes. MonoBehaviour-free.
    /// </summary>
    public sealed class BattleStore
    {
        private readonly IRng _rng;
        private readonly List<Action<BattleState>> _subscribers = new List<Action<BattleState>>();

        public BattleState State { get; private set; }

        public BattleStore(IRng rng) : this(rng, new BattleInit()) { }

        /// <summary><paramref name="init"/> carries what exploration hands to the battle (max stamina, HP, floor, miasma, time limit, disclosure).</summary>
        public BattleStore(IRng rng, BattleInit init)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            State = BattleReducer.InitState(_rng, init ?? new BattleInit());
        }

        public void Dispatch(BattleAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            BattleState next = BattleReducer.Reduce(State, action, _rng);
            if (ReferenceEquals(next, State)) return; // reducer no-op
            State = next;
            NotifySubscribers();
        }

        public void PlayCard(string instanceId, int invest) => Dispatch(new PlayCardAction(instanceId, invest));

        public void EndTurn() => Dispatch(new EndTurnAction());

        public void Restart() => Dispatch(new RestartAction());

        /// <summary>Registers a listener and immediately emits the current state. Returns an unsubscribe delegate.</summary>
        public Action Subscribe(Action<BattleState> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            _subscribers.Add(listener);
            listener(State);
            return () => _subscribers.Remove(listener);
        }

        private void NotifySubscribers()
        {
            foreach (var listener in _subscribers.ToArray())
            {
                listener(State);
            }
        }

        public BattleViewModel ToViewModel() => BattleViewModel.From(State);

        public IReadOnlyList<CardView> DescribeHand() => ViewModel.DescribeHand(State);

        public string DistanceLabel() => ViewModel.DistanceLabel(State.DistanceIndex);

        public bool IsBattleOver() => ViewModel.IsBattleOver(State.Result);
    }
}
