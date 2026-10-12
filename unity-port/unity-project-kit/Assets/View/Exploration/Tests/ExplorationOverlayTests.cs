// What the top band's three buttons open (dungeon_exploration_v4.md §6.2): the journal, 所持カード and
// 設定・セーブ, one at a time, with J, C and Esc doing the same, and Esc closing things in its order.
using System.Linq;
using DungeonCore;
using Exploration;
using Journal;
using NUnit.Framework;
using static Exploration.Tests.ExplorationScreenTests;

namespace Exploration.Tests
{
    public class ExplorationOverlayTests
    {
        [Test]
        public void NothingIsOpenAtFirst()
        {
            var screen = Explorable(new ExplorationSession()).Screen();

            Assert.That(screen.OpenOverlay, Is.EqualTo(Overlay.None));
            Assert.That(screen.CanOpenCards, Is.True);
            Assert.That(screen.CanOpenSettings, Is.True);
        }

        [Test]
        public void EachButtonOpensItsScreenAndClosesIt()
        {
            var session = Explorable(new ExplorationSession());

            foreach (var overlay in new[] { Overlay.Journal, Overlay.Cards, Overlay.Settings })
            {
                Assert.That(session.Toggle(overlay), Is.True);
                Assert.That(session.Screen().OpenOverlay, Is.EqualTo(overlay));
                Assert.That(session.Toggle(overlay), Is.True);
                Assert.That(session.Screen().OpenOverlay, Is.EqualTo(Overlay.None));
            }
            Assert.That(session.Toggle(Overlay.None), Is.False);
        }

        [Test]
        public void OpeningOneScreenClosesTheOther()
        {
            var session = Explorable(new ExplorationSession());

            session.Toggle(Overlay.Journal);
            session.Toggle(Overlay.Cards);
            Assert.That(session.OpenOverlay, Is.EqualTo(Overlay.Cards));
            session.Toggle(Overlay.Settings);
            Assert.That(session.OpenOverlay, Is.EqualTo(Overlay.Settings));
        }

        [Test]
        public void CardsAndSettingsCoverTheMapButTheJournalDoesNot()
        {
            var session = Explorable(new ExplorationSession());
            int target = session.State.ReachableUnresolved().First();

            session.Toggle(Overlay.Journal);
            Assert.That(session.MapIsLive, Is.True, "the drawer sits beside the map");
            Assert.That(session.Screen().Node(target).Options, Is.Not.Empty);

            foreach (var overlay in new[] { Overlay.Cards, Overlay.Settings })
            {
                session.Toggle(overlay);
                Assert.That(session.MapIsLive, Is.False);
                Assert.That(session.Screen().Node(target).Options, Is.Empty);
                Assert.That(session.Step(target), Is.False, "no step is taken under " + overlay);
            }
        }

        [Test]
        public void TheStandInBattleShutsCardsAndSettingsButNotTheJournal()
        {
            // The entry is a battle, so a life opens on the stand-in battle card (§6.8).
            var session = new ExplorationSession();
            var screen = session.Screen();
            Assert.That(screen.Card.Kind, Is.EqualTo(CardKind.Battle));

            Assert.That(screen.CanOpenCards, Is.False);
            Assert.That(screen.CanOpenSettings, Is.False);
            Assert.That(session.Toggle(Overlay.Cards), Is.False);
            Assert.That(session.Toggle(Overlay.Settings), Is.False);
            Assert.That(session.Escape(), Is.False, "Esc opens no settings in battle");
            Assert.That(session.Toggle(Overlay.Journal), Is.True);

            session.Act(ScreenAction.WinBattle);
            Assert.That(session.Screen().CanOpenCards, Is.True);
            Assert.That(session.Screen().CanOpenSettings, Is.True);
        }

        [Test]
        public void EscAnswersAConfirmationFirst()
        {
            var session = Explorable(new ExplorationSession(new RunStart { MiasmaPercent = 98 }));
            int target = session.State.ReachableUnresolved().First();
            session.Toggle(Overlay.Journal);
            session.Step(target);
            Assert.That(session.Screen().Card.IsConfirmation, Is.True);

            Assert.That(session.Escape(), Is.True);
            Assert.That(session.Screen().Card.IsShown, Is.False, "「やめる」");
            Assert.That(session.State.CurrentNodeId, Is.Not.EqualTo(target));
            Assert.That(session.OpenOverlay, Is.EqualTo(Overlay.Journal), "the drawer waits for the next Esc");

            Assert.That(session.Escape(), Is.True);
            Assert.That(session.OpenOverlay, Is.EqualTo(Overlay.None));
        }

        [Test]
        public void EscClosesTheJournalOrTheCardsBeforeItOpensSettings()
        {
            var session = Explorable(new ExplorationSession());

            foreach (var overlay in new[] { Overlay.Journal, Overlay.Cards })
            {
                session.Toggle(overlay);
                Assert.That(session.Escape(), Is.True);
                Assert.That(session.OpenOverlay, Is.EqualTo(Overlay.None), overlay + " closes, settings stay shut");
            }

            Assert.That(session.Escape(), Is.True);
            Assert.That(session.OpenOverlay, Is.EqualTo(Overlay.Settings));
            Assert.That(session.Escape(), Is.True);
            Assert.That(session.OpenOverlay, Is.EqualTo(Overlay.None));
        }

        [Test]
        public void TheEndLifeConfirmationIsAnsweredByEscToo()
        {
            var session = Explorable(new ExplorationSession());
            session.Act(ScreenAction.AskEndLife);
            Assert.That(session.Screen().Card.Kind, Is.EqualTo(CardKind.ConfirmEndLife));

            Assert.That(session.Escape(), Is.True);
            Assert.That(session.ConfirmingEndLife, Is.False);
            Assert.That(session.State.Phase, Is.EqualTo(RunPhase.Exploring), "the life goes on");
        }

        [Test]
        public void TheInterludeKeepsAllThreeButtons()
        {
            // §6.5: the band's buttons sit over the interlude card as they sit over the map.
            var session = new ExplorationSession();
            WalkToBoss(session);
            session.Act(ScreenAction.GoToInterlude);
            var screen = session.Screen();
            Assert.That(screen.Card.Kind, Is.EqualTo(CardKind.Interlude));

            Assert.That(screen.CanOpenCards, Is.True);
            Assert.That(screen.CanOpenSettings, Is.True);
            Assert.That(session.Escape(), Is.True);
            Assert.That(session.OpenOverlay, Is.EqualTo(Overlay.Settings), "the interlude is no confirmation");
        }

        [Test]
        public void TheCardsAndSettingsScreensSayWhatTheyAreAndHowToClose()
        {
            var screen = Explorable(new ExplorationSession()).Screen();

            Assert.That(screen.Cards.Title, Does.StartWith("所持カード"));
            Assert.That(screen.Cards.CloseLabel, Is.EqualTo("閉じる（C）"));
            Assert.That(screen.Settings.Title, Does.StartWith("設定・セーブ"));
            Assert.That(screen.Settings.Lines, Has.Some.StartWith("保存してゲームを閉じる"));
            Assert.That(screen.Settings.CloseLabel, Is.EqualTo("閉じる（Esc）"));
        }

        [Test]
        public void TheJournalEndsOnThePageOfTerms()
        {
            var book = Explorable(new ExplorationSession()).Screen().Journal;

            Assert.That(book.Pages.Last().Tab, Is.EqualTo(JournalTab.Terms));
            Assert.That(book.CanOpen(book.Pages.Last()), Is.True);
        }
    }
}
