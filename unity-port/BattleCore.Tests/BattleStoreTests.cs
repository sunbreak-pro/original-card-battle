using System.Collections.Generic;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// Headless tests for the Logic-layer <see cref="BattleStore"/> and the
    /// <see cref="IBattleView"/> render contract. All expected values use
    /// <see cref="FixedRng"/>(0) and are anchored to the same golden trace the
    /// parity fixture proves (step 0 = INIT, step 1 = PLAY_CARD thrust-1,
    /// step 2 = END_TURN).
    /// </summary>
    public class BattleStoreTests
    {
        private static BattleStore NewFixedStore() => new BattleStore(new FixedRng(0));

        [Test]
        public void Constructor_RunsInitState()
        {
            var store = NewFixedStore();

            Assert.That(store.State.Turn, Is.EqualTo(1));
            Assert.That(store.State.PlayerHp, Is.EqualTo(30));
            Assert.That(store.State.EnemyHp, Is.EqualTo(38));
            Assert.That(store.State.Result, Is.EqualTo(GameResult.Ongoing));
            Assert.That(store.State.Hand, Is.Not.Empty);
        }

        [Test]
        public void PlayCard_Thrust1_DealsFiveDamage()
        {
            var store = NewFixedStore();
            Assert.That(HandInstanceIds(store), Does.Contain("thrust-1"), "golden trace expects thrust-1 in the opening hand");

            store.PlayCard("thrust-1");

            Assert.That(store.State.EnemyHp, Is.EqualTo(33)); // 38 -> 33, matches parity step 1
            Assert.That(store.State.Result, Is.EqualTo(GameResult.Ongoing));
        }

        [Test]
        public void EndTurn_AdvancesTurnAndResolvesEnemy()
        {
            var store = NewFixedStore();
            store.PlayCard("thrust-1");

            store.EndTurn();

            Assert.That(store.State.Turn, Is.EqualTo(2));   // matches parity step 2
            Assert.That(store.State.PlayerHp, Is.EqualTo(22));
        }

        [Test]
        public void Dispatch_NoOp_DoesNotNotifySubscribers()
        {
            var store = NewFixedStore();
            int notifications = 0;
            store.Subscribe(_ => notifications++); // immediate emit => 1

            store.PlayCard("no-such-card"); // reducer returns same ref => no notify

            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_EmitsImmediately_AndOnEveryRealChange()
        {
            var store = NewFixedStore();
            int notifications = 0;
            var unsubscribe = store.Subscribe(_ => notifications++); // => 1

            store.PlayCard("thrust-1"); // => 2
            store.EndTurn();            // => 3

            Assert.That(notifications, Is.EqualTo(3));

            unsubscribe();
            store.EndTurn(); // no longer listening
            Assert.That(notifications, Is.EqualTo(3));
        }

        [Test]
        public void Restart_ResetsToInitialState()
        {
            var store = NewFixedStore();
            store.PlayCard("thrust-1");
            store.EndTurn();

            store.Restart();

            Assert.That(store.State.Turn, Is.EqualTo(1));
            Assert.That(store.State.EnemyHp, Is.EqualTo(38));
            Assert.That(store.State.Result, Is.EqualTo(GameResult.Ongoing));
        }

        [Test]
        public void View_RendersOnSubscribeAndOnChange()
        {
            var store = NewFixedStore();
            var view = new FakeView();
            store.Subscribe(state => view.Render(BattleViewModel.From(state)));

            Assert.That(view.RenderCount, Is.EqualTo(1), "immediate emit renders once");
            Assert.That(view.Last!.Turn, Is.EqualTo(1));
            Assert.That(view.Last!.EnemyHp, Is.EqualTo(38));
            Assert.That(view.Last!.Hand, Is.Not.Empty);
            Assert.That(view.Last!.DistanceLabel, Is.Not.Empty);

            store.PlayCard("thrust-1");

            Assert.That(view.RenderCount, Is.EqualTo(2));
            Assert.That(view.Last!.EnemyHp, Is.EqualTo(33));
        }

        [Test]
        public void ToViewModel_MatchesViewModelHelpers()
        {
            var store = NewFixedStore();
            var vm = store.ToViewModel();

            Assert.That(vm.DistanceLabel, Is.EqualTo(store.DistanceLabel()));
            Assert.That(vm.BattleOver, Is.EqualTo(store.IsBattleOver()));
            Assert.That(vm.Hand.Count, Is.EqualTo(store.DescribeHand().Count));
        }

        private static IEnumerable<string> HandInstanceIds(BattleStore store)
        {
            var ids = new List<string>();
            foreach (var card in store.State.Hand) ids.Add(card.InstanceId);
            return ids;
        }

        private sealed class FakeView : IBattleView
        {
            public int RenderCount { get; private set; }
            public BattleViewModel? Last { get; private set; }

            public void Render(BattleViewModel vm)
            {
                RenderCount++;
                Last = vm;
            }
        }
    }
}
