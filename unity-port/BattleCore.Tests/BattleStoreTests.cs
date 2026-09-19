using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>Headless exercise of the Logic layer (BattleStore) and the View contract (IBattleView).</summary>
    public class BattleStoreTests
    {
        private sealed class FakeView : IBattleView
        {
            public readonly List<BattleViewModel> Renders = new List<BattleViewModel>();
            public void Render(BattleViewModel vm) => Renders.Add(vm);
        }

        private static BattleStore NewStore() => new BattleStore(new FixedRng(0));

        [Test]
        public void Subscribe_EmitsCurrentStateImmediately()
        {
            var store = NewStore();
            var seen = new List<BattleState>();
            store.Subscribe(seen.Add);
            Assert.That(seen.Count, Is.EqualTo(1));
            Assert.That(seen[0], Is.SameAs(store.State));
        }

        [Test]
        public void Dispatch_NoOpDoesNotNotify()
        {
            var store = NewStore();
            int calls = 0;
            store.Subscribe(_ => calls++);
            store.PlayCard("no-such-card", 1);
            store.PlayCard(store.State.Hand[0].InstanceId, 9);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void PlayCard_WithInvest_NotifiesAndCarriesEvents()
        {
            var store = NewStore();
            var view = new FakeView();
            store.Subscribe(state => view.Render(BattleViewModel.From(state)));
            var card = store.State.Hand.First(c => c.Def.MinInvest == 0 || store.State.PlayerStamina >= 1);
            store.PlayCard(card.InstanceId, card.Def.MinInvest);
            Assert.That(view.Renders.Count, Is.EqualTo(2));
            Assert.That(view.Renders[1].Events.OfType<CardPlayedEvent>().Single().Invest, Is.EqualTo(card.Def.MinInvest));
            Assert.That(view.Renders[1].Hand.Count, Is.EqualTo(2));
        }

        [Test]
        public void EndTurn_ProducesEnemyPhaseEventsInOrder()
        {
            var store = NewStore();
            store.EndTurn();
            var types = store.State.Events.Select(e => e.GetType()).ToList();
            int enemyPhase = types.IndexOf(typeof(EnemyPhaseStartedEvent));
            int omenDeclared = types.IndexOf(typeof(OmenDeclaredEvent));
            int turnStarted = types.IndexOf(typeof(TurnStartedEvent));
            Assert.That(enemyPhase, Is.GreaterThanOrEqualTo(0));
            Assert.That(omenDeclared, Is.GreaterThan(enemyPhase));
            Assert.That(turnStarted, Is.GreaterThan(omenDeclared));
            Assert.That(store.State.Turn, Is.EqualTo(2));
        }

        [Test]
        public void Unsubscribe_StopsNotifications()
        {
            var store = NewStore();
            int calls = 0;
            var unsubscribe = store.Subscribe(_ => calls++);
            unsubscribe();
            store.EndTurn();
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Init_FromExploration_IsVisibleInViewModel()
        {
            var store = new BattleStore(new FixedRng(0), new BattleInit(PlayerMaxStamina: 6, PlayerHp: 12, Floor: 5, MiasmaPercent: 80, TimeLimitLeft: 2, Disclosure: 2));
            var vm = store.ToViewModel();
            Assert.That(vm.PlayerMaxStamina, Is.EqualTo(6));
            Assert.That(vm.PlayerHp, Is.EqualTo(12));
            Assert.That(vm.Floor, Is.EqualTo(5));
            Assert.That(vm.MiasmaPenalty, Is.EqualTo(4));
            Assert.That(vm.TimeLimitLeft, Is.EqualTo(2));
            Assert.That(vm.Omen!.Name, Is.EqualTo("薙ぎ払い"));
            store.Restart();
            Assert.That(store.ToViewModel().PlayerMaxStamina, Is.EqualTo(6));
        }

        [Test]
        public void Constructor_RejectsNullRng()
        {
            Assert.Throws<ArgumentNullException>(() => new BattleStore(null!));
        }
    }
}
