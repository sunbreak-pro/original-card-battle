// What the shared header band (#1074) shows on each screen: the place and its tags, the figures, and which button is
// pressable or lit (the battle's owned cards take no press, the table's stay lit, the exploration's follow whether the
// cards may open now).
using Depiction;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    public sealed class HeaderContentTests
    {
        private static readonly HeaderCalendar Running = HeaderCalendar.Of(87, false);

        [Test]
        public void TheBase_ShowsTheBaseAtFullStrength_AndOpensTheTable()
        {
            HeaderContent c = HeaderContent.ForBase(Running, 50, 10, 20);
            Assert.That(c.Screen, Is.EqualTo(HeaderScreen.Base));
            Assert.That(c.PlaceName, Is.EqualTo("拠点"));
            Assert.That(c.PlaceArt, Is.EqualTo(HeaderPlaceArt.Base));
            Assert.That(c.Tags, Is.Empty);
            Assert.That(c.BackChip, Is.False);
            Assert.That(c.HpFigure.Text, Is.EqualTo("50 / 50"));
            Assert.That(c.StaminaFigure.Text, Is.EqualTo("10 / 10"));
            Assert.That(c.Days.Text, Is.EqualTo("残り87日"));
            Assert.That(c.DeckBadge, Is.EqualTo("20"));
            Assert.That(c.OwnedCards, Is.EqualTo(HeaderButtonState.Ready));
            Assert.That(c.OwnedCards.Pressable, Is.True);
            Assert.That(c.Speed.Shown, Is.False);
        }

        [Test]
        public void TheTable_KeepsTheOwnedCardsLit_AndOffersTheWayBack()
        {
            HeaderContent c = HeaderContent.ForTable(Running, 50, 10, 24);
            Assert.That(c.Screen, Is.EqualTo(HeaderScreen.Table));
            Assert.That(c.PlaceName, Is.EqualTo("拠点"));
            Assert.That(c.Tags, Is.EqualTo(new[] { "支度の卓" }));
            Assert.That(c.BackChip, Is.True);
            Assert.That(c.OwnedCards.Lit, Is.True);
            Assert.That(c.OwnedCards.Enabled, Is.True, "lit, not dimmed");
            Assert.That(c.OwnedCards.Pressable, Is.False, "the table is the screen it opens");
            Assert.That(c.DeckBadge, Is.EqualTo("24"));
            Assert.That(c.HpFigure.Text, Is.EqualTo("50 / 50"));
        }

        [Test]
        public void TheExploration_OpensTheOwnedCards_OnlyWhileTheyMayOpen()
        {
            HeaderContent open = HeaderContent.ForExplore("第 1 層　燦光の樹海", Running, 38, 50, 8, 10, 20, canOpenCards: true);
            Assert.That(open.PlaceName, Is.EqualTo("第 1 層　燦光の樹海"));
            Assert.That(open.PlaceArt, Is.EqualTo(HeaderPlaceArt.Layer));
            Assert.That(open.Tags, Is.Empty);
            Assert.That(open.OwnedCards.Pressable, Is.True);
            Assert.That(open.HpFigure.Text, Is.EqualTo("38 / 50"));
            Assert.That(open.StaminaFigure.Text, Is.EqualTo("8 / 10"));

            HeaderContent shut = HeaderContent.ForExplore("第 1 層　燦光の樹海", Running, 38, 50, 8, 10, 20, canOpenCards: false, canOpenSettings: false);
            Assert.That(shut.OwnedCards, Is.EqualTo(HeaderButtonState.Off));
            Assert.That(shut.OwnedCards.Shown, Is.True, "it stays where it is, dimmed");
            Assert.That(shut.OwnedCards.Pressable, Is.False);
            Assert.That(shut.Settings.Pressable, Is.False);
            Assert.That(shut.Journal.Pressable, Is.True);
        }

        [Test]
        public void TheBattle_ShowsTheCountButTakesNoPressOnTheOwnedCards()
        {
            HeaderContent c = HeaderContent.ForBattle("第 1 層　燦光の樹海", 3, 2, 3, Running, 31, 50, 5, 10, 20, BattleSpeedStep.Fast);
            Assert.That(c.Screen, Is.EqualTo(HeaderScreen.Battle));
            Assert.That(c.Tags, Is.EqualTo(new[] { "ターン 3", "連戦 2 / 3" }));
            Assert.That(c.OwnedCards.Shown, Is.True);
            Assert.That(c.OwnedCards.Enabled, Is.False);
            Assert.That(c.OwnedCards.Pressable, Is.False);
            Assert.That(c.DeckBadge, Is.EqualTo("20"));
            Assert.That(c.Speed, Is.EqualTo(HeaderButtonState.Ready));
            Assert.That(c.SpeedText, Is.EqualTo("速さ ×1.5"));
            Assert.That(c.Settings.Pressable, Is.True);
            Assert.That(c.Journal.Pressable, Is.True);

            HeaderContent noSettings = HeaderContent.ForBattle("第 1 層　燦光の樹海", 1, 1, 1, Running, 50, 50, 10, 10, 20, BattleSpeedStep.Normal, canOpenSettings: false);
            Assert.That(noSettings.Settings, Is.EqualTo(HeaderButtonState.Off), "it stays in its place, dimmed");
            Assert.That(noSettings.Tags, Is.EqualTo(new[] { "ターン 1" }));
        }

        [Test]
        public void TheOwnedCardsRule_ByScreen()
        {
            Assert.That(HeaderContent.OwnedCardsState(HeaderScreen.Base, false), Is.EqualTo(HeaderButtonState.Ready));
            Assert.That(HeaderContent.OwnedCardsState(HeaderScreen.Table, false), Is.EqualTo(HeaderButtonState.On));
            Assert.That(HeaderContent.OwnedCardsState(HeaderScreen.Explore, true), Is.EqualTo(HeaderButtonState.Ready));
            Assert.That(HeaderContent.OwnedCardsState(HeaderScreen.Explore, false), Is.EqualTo(HeaderButtonState.Off));
            Assert.That(HeaderContent.OwnedCardsState(HeaderScreen.Battle, true), Is.EqualTo(HeaderButtonState.Off));
        }

        [Test]
        public void TheJournal_IsPressableOnEveryScreen_AndTheSpeedStandsInBattleOnly()
        {
            HeaderContent[] all =
            {
                HeaderContent.ForBase(Running, 50, 10, 20),
                HeaderContent.ForTable(Running, 50, 10, 20),
                HeaderContent.ForExplore("第 2 層　■■の秘跡", Running, 30, 50, 6, 10, 22, true),
                HeaderContent.ForBattle("第 2 層　■■の秘跡", 1, 1, 1, Running, 30, 50, 6, 10, 22, BattleSpeedStep.Normal),
            };
            foreach (HeaderContent c in all)
            {
                Assert.That(c.Journal.Pressable, Is.True, c.Screen.ToString());
                Assert.That(c.Speed.Shown, Is.EqualTo(c.Screen == HeaderScreen.Battle), c.Screen.ToString());
                Assert.That(c.StateOf(HeaderButtonKind.Journal), Is.EqualTo(c.Journal));
            }
        }

        [Test]
        public void AButtonState_CanBeSwappedByKind()
        {
            HeaderContent c = HeaderContent.ForExplore("第 1 層　燦光の樹海", Running, 38, 50, 8, 10, 20, true);
            c.SetState(HeaderButtonKind.OwnedCards, HeaderButtonState.Off);
            Assert.That(c.OwnedCards, Is.EqualTo(HeaderButtonState.Off));
            c.SetState(HeaderButtonKind.Speed, HeaderButtonState.Ready);
            Assert.That(c.StateOf(HeaderButtonKind.Speed).Shown, Is.True);
            Assert.That(HeaderButtonState.Hidden.Pressable, Is.False);
            Assert.That(HeaderButtonState.On.ToString(), Is.EqualTo("lit"));
        }

        [Test]
        public void AFrozenCalendar_CarriesAShortNote()
        {
            HeaderCalendar frozen = HeaderCalendar.Of(41, true);
            Assert.That(frozen.ShownNote, Is.EqualTo("暦は止まっている"));
            Assert.That(HeaderCalendar.Of(41, true, "竜王が戻るまで 41 日").ShownNote, Is.EqualTo("竜王が戻るまで 41 日"));
            Assert.That(HeaderCalendar.Of(41, false, "竜王が戻るまで 41 日").ShownNote, Is.EqualTo(""), "a running calendar shows no note");
            Assert.That(HeaderContent.ForBase(frozen, 50, 10, 20).Calendar.Frozen, Is.True);
            Assert.That(HeaderContent.ForBase(null, 50, 10, 20).Days.Number, Is.EqualTo("0"));
        }
    }
}
