using System.Linq;
using NUnit.Framework;

namespace Trial.Script.Tests
{
    /// <summary>The title's menu rules (title.js:52-105, :151-160): items, what opens, the first cursor, ↑↓, choosing.</summary>
    public class TitleMenuTests
    {
        [Test]
        public void TheItems_AreNewContinueSettings_InThatOrder()
        {
            var menu = new TitleMenu(hasSave: true, unreadable: false, hasSettings: true);
            Assert.That(menu.Items.Select(i => i.Key), Is.EqualTo(new[] { "new", "continue", "settings" }));
            Assert.That(menu.Items.Select(i => i.Label), Is.EqualTo(new[] { MetaText.NewGame, MetaText.Continue, MetaText.Settings }));
            Assert.That(menu.Items.All(i => i.Enabled), Is.True);
            Assert.That(menu.Items.All(i => i.Note == null), Is.True);
        }

        [Test]
        public void WithoutASave_ContinueIsShut_AndSaysWhy()
        {
            var none = new TitleMenu(hasSave: false, unreadable: false, hasSettings: true);
            Assert.That(none.Items[1].Enabled, Is.False);
            Assert.That(none.Items[1].Note, Is.EqualTo(MetaText.NoSave));
            var unreadable = new TitleMenu(hasSave: false, unreadable: true, hasSettings: true);
            Assert.That(unreadable.Items[1].Note, Is.EqualTo(MetaText.Unreadable));
            Assert.That(unreadable.Items[0].Note, Is.Null, "only つづきから carries the note");
        }

        [Test]
        public void SettingsOpens_OnlyWithAnOpener()
        {
            Assert.That(new TitleMenu(false, false, hasSettings: false).Items[2].Enabled, Is.False);
            Assert.That(new TitleMenu(false, false, hasSettings: true).Items[2].Enabled, Is.True);
        }

        [Test]
        public void TheCursorStarts_OnContinueWithASave_ElseOnNew()
        {
            Assert.That(new TitleMenu(true, false, true).Cur, Is.EqualTo(1));
            Assert.That(new TitleMenu(false, false, true).Cur, Is.EqualTo(0));
            Assert.That(new TitleMenu(false, true, true).Cur, Is.EqualTo(0), "an unreadable save is no save to continue");
            Assert.That(new TitleMenu(true, false, true).Lit, Is.EqualTo(1));
        }

        [Test]
        public void Move_SkipsShutItems_AndGoesRoundTheEnds()
        {
            var menu = new TitleMenu(hasSave: false, unreadable: false, hasSettings: true);
            Assert.That(menu.Move(1), Is.EqualTo("uiClick"));
            Assert.That(menu.Cur, Is.EqualTo(2), "つづきから is shut: 設定");
            Assert.That(menu.Move(1), Is.EqualTo("uiClick"));
            Assert.That(menu.Cur, Is.EqualTo(0), "round the end");
            Assert.That(menu.Move(-1), Is.EqualTo("uiClick"));
            Assert.That(menu.Cur, Is.EqualTo(2), "round the top");
            Assert.That(menu.Lit, Is.EqualTo(2));
        }

        [Test]
        public void Move_WithOneOpenItem_StaysSilent()
        {
            var menu = new TitleMenu(hasSave: false, unreadable: false, hasSettings: false);
            Assert.That(menu.Move(1), Is.Null);
            Assert.That(menu.Move(-1), Is.Null);
            Assert.That(menu.Cur, Is.EqualTo(0));
        }

        [Test]
        public void Focus_ByPointerHovers_ByKeyClicks_AndIgnoresTheSameOrAShutItem()
        {
            var menu = new TitleMenu(hasSave: false, unreadable: false, hasSettings: true);
            Assert.That(menu.Focus(0, true), Is.Null, "already there");
            Assert.That(menu.Focus(1, true), Is.Null, "shut");
            Assert.That(menu.Cur, Is.EqualTo(0));
            Assert.That(menu.Focus(2, true), Is.EqualTo("nodeHover"));
            Assert.That(menu.Focus(0, false), Is.EqualTo("uiClick"));
        }

        [Test]
        public void Choose_New_LeavesWithoutASave_AndAsksOverOne()
        {
            Assert.That(new TitleMenu(false, false, true).Choose(0), Is.EqualTo(TitleChoice.LeaveNew));
            Assert.That(new TitleMenu(true, false, true).Choose(0), Is.EqualTo(TitleChoice.AskNew));
            Assert.That(new TitleMenu(false, true, true).Choose(0), Is.EqualTo(TitleChoice.AskNew), "an unreadable save asks too");
        }

        [Test]
        public void Choose_ContinueAndSettings()
        {
            var menu = new TitleMenu(true, false, true);
            Assert.That(menu.Choose(1), Is.EqualTo(TitleChoice.LeaveContinue));
            Assert.That(menu.Choose(2), Is.EqualTo(TitleChoice.Settings));
        }

        [Test]
        public void Choose_NothingWhileLeaving_OrForAShutItem()
        {
            var menu = new TitleMenu(false, false, false);
            Assert.That(menu.Choose(1), Is.EqualTo(TitleChoice.None));
            Assert.That(menu.Choose(2), Is.EqualTo(TitleChoice.None));
            menu.Busy = true;
            Assert.That(menu.Choose(0), Is.EqualTo(TitleChoice.None));
        }

        [Test]
        public void Choose_MovesTheCursor_ButNotTheLitRow_AsTheJsDoes()
        {
            var menu = new TitleMenu(true, false, true);
            Assert.That(menu.Choose(2), Is.EqualTo(TitleChoice.Settings));
            Assert.That(menu.Cur, Is.EqualTo(2));
            Assert.That(menu.Lit, Is.EqualTo(1));
        }

        [Test]
        public void Keys_AsTheTitleReadsThem()
        {
            Assert.That(TitleMenu.KeyOf("ArrowUp"), Is.EqualTo(TitleKey.Up));
            Assert.That(TitleMenu.KeyOf("w"), Is.EqualTo(TitleKey.Up));
            Assert.That(TitleMenu.KeyOf("ArrowDown"), Is.EqualTo(TitleKey.Down));
            Assert.That(TitleMenu.KeyOf("s"), Is.EqualTo(TitleKey.Down));
            Assert.That(TitleMenu.KeyOf("Enter"), Is.EqualTo(TitleKey.Choose));
            Assert.That(TitleMenu.KeyOf(" "), Is.EqualTo(TitleKey.Choose));
            Assert.That(TitleMenu.KeyOf("Escape"), Is.EqualTo(TitleKey.Settings));
            Assert.That(TitleMenu.KeyOf("W"), Is.EqualTo(TitleKey.None), "the JS compares the lower-case key only");
            Assert.That(TitleMenu.KeyOf("j"), Is.EqualTo(TitleKey.None));
            Assert.That(TitleMenu.KeyOf(null), Is.EqualTo(TitleKey.None));
        }
    }
}
