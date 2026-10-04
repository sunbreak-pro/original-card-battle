// The end of a demo battle that cannot strand the player (#297): a tally or an end screen that
// throws is reported, and a fallback end screen still offers to choose again.
using System;
using System.Collections.Generic;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class DemoEndingTests
    {
        private static List<CardInstance> Deck()
        {
            return DeckBuilder.Random(9).Build();
        }

        private static void AssertOffersToChooseAgain(DemoEndScreen screen)
        {
            Assert.Multiple(() =>
            {
                Assert.That(screen.CanChooseEnemy, Is.True, "the mode screen is offered");
                Assert.That(screen.ChooseEnemyLabel, Does.Contain("選び直す"));
                Assert.That(screen.BackLabel, Is.EqualTo("デッキ選択へ戻る"));
                Assert.That(screen.CanAgain, Is.False, "「もう一度」 would step a run left half-done");
                Assert.That(screen.CanGoOn, Is.False, "nor would the chain's rest");
                Assert.That(screen.Title, Is.Not.Empty);
                Assert.That(screen.Lines, Is.Not.Empty);
            });
        }

        [Test]
        public void AFinishThatThrows_IsReported_AndTheFallbackOffersToChooseAgain()
        {
            var session = DemoSession.Single(Deck(), "polearm_warped", 1);
            CoreBattleSource source = session.StartBattle();
            var reported = new List<Exception>();

            // The battle is still going, so DemoSession.Finish throws.
            bool tallied = DemoEnding.Tally(() => session.Finish(source), reported.Add);
            DemoEndScreen screen = DemoEnding.Screen(tallied, session.EndScreen, reported.Add);

            Assert.That(tallied, Is.False);
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported[0], Is.InstanceOf<InvalidOperationException>());
            AssertOffersToChooseAgain(screen);
        }

        [Test]
        public void ASurrenderThatThrows_IsReported_AndTheFallbackOffersToChooseAgain()
        {
            var session = DemoSession.Single(Deck(), "polearm_warped", 1);
            session.StartBattle();
            var reported = new List<Exception>();

            bool tallied = DemoEnding.Tally(() => session.Surrender(null), reported.Add);
            DemoEndScreen screen = DemoEnding.Screen(tallied, session.EndScreen, reported.Add);

            Assert.That(tallied, Is.False);
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported[0], Is.InstanceOf<ArgumentNullException>());
            AssertOffersToChooseAgain(screen);
        }

        [Test]
        public void AnEndScreenThatThrows_IsReported_AndTheFallbackOffersToChooseAgain()
        {
            // Nothing tallied yet, so DemoSession.EndScreen throws.
            var session = DemoSession.Single(Deck(), "polearm_warped", 1);
            var reported = new List<Exception>();

            DemoEndScreen screen = DemoEnding.Screen(true, session.EndScreen, reported.Add);

            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported[0], Is.InstanceOf<InvalidOperationException>());
            AssertOffersToChooseAgain(screen);
        }

        [Test]
        public void AnEndScreenThatComesOutEmpty_FallsBack()
        {
            var reported = new List<Exception>();
            AssertOffersToChooseAgain(DemoEnding.Screen(true, () => null, reported.Add));
            AssertOffersToChooseAgain(DemoEnding.Screen(true, null, reported.Add));
            Assert.That(reported, Is.Empty);
        }

        [Test]
        public void AReportThatThrows_DoesNotTakeTheFallbackAway()
        {
            Action<Exception> broken = e => throw new InvalidOperationException("the log is broken");

            bool tallied = DemoEnding.Tally(() => throw new InvalidOperationException("the tally"), broken);
            DemoEndScreen screen = DemoEnding.Screen(true, () => throw new InvalidOperationException("the words"), broken);

            Assert.That(tallied, Is.False);
            AssertOffersToChooseAgain(screen);
        }

        [Test]
        public void AnEndingThatGoesThrough_ShowsTheSessionsOwnEndScreen()
        {
            var session = DemoSession.Single(Deck(), "polearm_warped", 1);
            CoreBattleSource source = session.StartBattle();
            var reported = new List<Exception>();

            bool tallied = DemoEnding.Tally(() => session.Surrender(source), reported.Add);
            DemoEndScreen screen = DemoEnding.Screen(tallied, session.EndScreen, reported.Add);

            Assert.That(tallied, Is.True);
            Assert.That(reported, Is.Empty);
            Assert.Multiple(() =>
            {
                Assert.That(screen.Title, Does.Contain("降参しました"));
                Assert.That(screen.CanAgain, Is.True);
                Assert.That(screen.AgainLabel, Is.EqualTo("もう一度"));
                Assert.That(screen.ChooseEnemyLabel, Is.EqualTo("敵を選び直す"));
            });
        }
    }
}
