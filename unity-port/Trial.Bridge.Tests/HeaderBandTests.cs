// The shared header band (#1074): the four screens read one set of values, the band is as tall as the exploration map's
// top, the buttons stand in one place on every screen, the count and the bubbles sit where the canvas puts them, every
// text is 16 px or more, and amber stays on the stamina and the deck's count.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Depiction;
using NUnit.Framework;
using Trial.Geometry;

namespace Trial.Bridge.Tests
{
    public sealed class HeaderBandTests
    {
        private static readonly HeaderScreen[] Screens = { HeaderScreen.Base, HeaderScreen.Table, HeaderScreen.Explore, HeaderScreen.Battle };

        private static readonly TableBox Stage = new TableBox(0f, 0f, 1920f, 1080f);

        // Stand-ins for the measured words: the speed's 「速さ ×1.25」 is the widest.
        private const float SpeedW = 120f, OwnedCardsW = 106f, JournalW = 76f, SettingsW = 76f;

        private static HeaderContent ContentOf(HeaderScreen screen)
        {
            HeaderCalendar cal = HeaderCalendar.Of(86, false);
            switch (screen)
            {
                case HeaderScreen.Base: return HeaderContent.ForBase(cal, 50, 10, 20);
                case HeaderScreen.Table: return HeaderContent.ForTable(cal, 50, 10, 20);
                case HeaderScreen.Explore: return HeaderContent.ForExplore("第 1 層　燦光の樹海", cal, 38, 50, 8, 10, 20, true);
                default: return HeaderContent.ForBattle("第 1 層　燦光の樹海", 3, 1, 1, cal, 31, 50, 5, 10, 20, BattleSpeedStep.Normal);
            }
        }

        private static HeaderButtonRow RowOf(HeaderContent c) =>
            HeaderBand.ButtonRow(c.Speed.Shown ? SpeedW : 0f, OwnedCardsW, JournalW, SettingsW);

        [Test]
        public void EveryScreen_ReadsTheSameValues()
        {
            HeaderBandMetrics first = HeaderBand.MetricsOf(HeaderScreen.Base);
            foreach (HeaderScreen s in Screens)
            {
                HeaderBandMetrics m = HeaderBand.MetricsOf(s);
                Assert.That(m, Is.SameAs(first), s.ToString());
                Assert.That(m.Band, Is.EqualTo(new TableBox(0f, 0f, 1920f, 96f)), s.ToString());
                Assert.That(m.PlaceWidth, Is.EqualTo(HeaderPlaceWidth.Packed), s + ": packed to the left by default");
            }
            Assert.That(() => HeaderBand.MetricsOf((HeaderScreen)99), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void TheBand_IsAsTallAsTheExplorationMapsTop()
        {
            Assert.That(HeaderBand.H, Is.EqualTo(global::Exploration.StrataLayout.StageTop));
            Assert.That(HeaderBand.Band.Bottom, Is.EqualTo(global::Exploration.StrataLayout.StageTop));
        }

        [Test]
        public void TheButtons_StandInOnePlace_OnEveryScreen()
        {
            foreach (HeaderScreen s in Screens)
            {
                HeaderButtonRow row = RowOf(ContentOf(s));
                Assert.That(row.Box(HeaderButtonKind.Settings).Right, Is.EqualTo(1920f - 24f), s + ": the settings end at the padding");
                Assert.That(row.Box(HeaderButtonKind.Journal).X, Is.EqualTo(1896f - SettingsW - 8f - JournalW), s.ToString());
                Assert.That(row.Box(HeaderButtonKind.OwnedCards).X, Is.EqualTo(1896f - SettingsW - 8f - JournalW - 8f - OwnedCardsW), s.ToString());
                foreach (HeaderButtonKind k in new[] { HeaderButtonKind.OwnedCards, HeaderButtonKind.Journal, HeaderButtonKind.Settings })
                {
                    TableBox b = row.Box(k);
                    Assert.That(b.Y, Is.EqualTo(11.5f), s + " " + k + ": centred in the 95 px content box");
                    Assert.That(b.H, Is.EqualTo(72f));
                    Assert.That(b.Inside(HeaderBand.Band), Is.True, s + " " + k + " " + b);
                }
                Assert.That(row.Has(HeaderButtonKind.Speed), Is.EqualTo(s == HeaderScreen.Battle), s + ": the speed is the battle's only");
            }
        }

        [Test]
        public void TheBattlesSpeed_StandsLeftOfASeparator()
        {
            HeaderButtonRow row = RowOf(ContentOf(HeaderScreen.Battle));
            TableBox speed = row.Box(HeaderButtonKind.Speed), owned = row.Box(HeaderButtonKind.OwnedCards);
            Assert.That(row.Separator.HasValue, Is.True);
            TableBox sep = row.Separator.Value;
            Assert.That(sep.X - speed.Right, Is.EqualTo(8f + 8f), "the gap and the separator's margin");
            Assert.That(owned.X - sep.Right, Is.EqualTo(8f + 8f));
            Assert.That(sep.H, Is.EqualTo(48f));
            Assert.That(sep.Y + sep.H / 2f, Is.EqualTo(95f / 2f));
            Assert.That(row.Left, Is.EqualTo(speed.X));
            Assert.That(RowOf(ContentOf(HeaderScreen.Explore)).Separator.HasValue, Is.False);
            Assert.That(() => RowOf(ContentOf(HeaderScreen.Explore)).Box(HeaderButtonKind.Speed), Throws.ArgumentException);
        }

        [Test]
        public void AButton_IsSeventySixWideAtLeast()
        {
            Assert.That(HeaderBand.ButtonW(32f), Is.EqualTo(76f));
            Assert.That(HeaderBand.ButtonW(80f), Is.EqualTo(80f + 24f + 2f));
            Assert.That(HeaderBand.ButtonIconTop, Is.EqualTo(1f + 6f + (72f - 2f - 6f - 4f - 52f) / 2f));
            Assert.That(HeaderBand.ButtonLabelTop + 20f, Is.LessThanOrEqualTo(72f - 1f - 4f), "the word stays inside the padding");
        }

        [Test]
        public void TheLeftSide_IsPackedFromThePadding_TwentyEightApart()
        {
            HeaderLeftRow row = HeaderBand.LeftRow(300f, 150f, 110f, 100f);
            Assert.That(row.PlaceX, Is.EqualTo(24f));
            Assert.That(row.Divider.X, Is.EqualTo(24f + 300f + 28f));
            Assert.That(row.Divider.H, Is.EqualTo(40f));
            Assert.That(row.Divider.Y + row.Divider.H / 2f, Is.EqualTo(95f / 2f));
            Assert.That(row.CalendarX, Is.EqualTo(row.Divider.Right + 28f));
            Assert.That(row.HpX, Is.EqualTo(row.CalendarX + 150f + 28f));
            Assert.That(row.StaminaX, Is.EqualTo(row.HpX + 110f + 28f));
            Assert.That(row.Right, Is.EqualTo(row.StaminaX + 100f));
        }

        [Test]
        public void TheFixedPlace_KeepsTheCalendarInOnePlace()
        {
            float narrow = HeaderBand.PlaceW(180f, HeaderPlaceWidth.Fixed), wide = HeaderBand.PlaceW(520f, HeaderPlaceWidth.Fixed);
            Assert.That(HeaderBand.LeftRow(narrow, 150f, 110f, 100f).CalendarX, Is.EqualTo(HeaderBand.LeftRow(wide, 150f, 110f, 100f).CalendarX));
            Assert.That(HeaderBand.PlaceW(700f, HeaderPlaceWidth.Fixed), Is.EqualTo(700f), "a wider place keeps its width");
            Assert.That(HeaderBand.PlaceW(180f, HeaderPlaceWidth.Packed), Is.EqualTo(180f));
        }

        [Test]
        public void ThePlace_RunsItsPictureNameTagsAndChip_TwelveApart()
        {
            float[] xs = HeaderBand.PlaceXs(56f, new[] { 92f }, 110f);
            Assert.That(xs, Is.EqualTo(new[] { 0f, 28f + 12f, 28f + 12f + 56f + 12f, 28f + 12f + 56f + 12f + 92f + 12f }));
            Assert.That(HeaderBand.PlaceContentW(56f, new[] { 92f }, 110f), Is.EqualTo(xs[3] + 110f));
            Assert.That(HeaderBand.PlaceXs(56f, null, 0f), Is.EqualTo(new[] { 0f, 40f }));
            Assert.That(HeaderBand.PlaceContentW(56f, null, 0f), Is.EqualTo(96f));
            Assert.That(HeaderBand.TagW(64f), Is.EqualTo(64f + 22f));
            Assert.That(HeaderBand.TagH, Is.EqualTo(28f));
        }

        [Test]
        public void AFigure_RunsItsWordsOnOneLine()
        {
            HeaderFigure days = HeaderBand.DaysFigure(87);
            float[] xs = HeaderBand.FigureXs(26f, days, 40f, 39f, 20f);
            Assert.That(xs, Is.EqualTo(new[] { 0f, 36f, 36f + 40f + 4f, 36f + 40f + 4f + 39f + 4f, 36f + 40f + 4f + 39f + 4f + 20f }));
            Assert.That(HeaderBand.FigureW(26f, days, 40f, 39f, 20f), Is.EqualTo(xs[4]));

            float[] frozen = HeaderBand.FigureXs(26f, days, 40f, 39f, 20f, 128f);
            Assert.That(frozen[4], Is.EqualTo(xs[4] + 10f), "the note stands 10 px after 「日」");
            Assert.That(HeaderBand.FigureW(26f, days, 40f, 39f, 20f, 128f), Is.EqualTo(frozen[4] + 128f));

            HeaderFigure hp = HeaderBand.OutOf(38, 50);
            float[] h = HeaderBand.FigureXs(22f, hp, 0f, 29f, 40f);
            Assert.That(h[1], Is.EqualTo(h[2]), "no lead: the number starts after the picture");
            Assert.That(h[3], Is.EqualTo(h[2] + 29f + 5f), "a space before 「/」");
        }

        [Test]
        public void TheCount_SitsOnTheOwnedCardsCorner()
        {
            TableBox button = RowOf(ContentOf(HeaderScreen.Explore)).Box(HeaderButtonKind.OwnedCards);
            TableBox badge = HeaderBand.BadgeBox(button, 20f);
            Assert.That(badge.W, Is.EqualTo(32f));
            Assert.That(badge.Right, Is.EqualTo(button.Right + 6f));
            Assert.That(badge.Y, Is.EqualTo(button.Y - 8f));
            Assert.That(badge.H, Is.EqualTo(24f));
            Assert.That(badge.Inside(HeaderBand.Band), Is.True, "it stays on the band: " + badge);
            Assert.That(HeaderBand.BadgeBox(button, 9f).W, Is.EqualTo(30f), "30 wide at least");
        }

        [Test]
        public void ABubble_OpensUnderTheBand_OnTheStage()
        {
            foreach (HeaderScreen s in Screens)
            {
                HeaderContent c = ContentOf(s);
                HeaderButtonRow row = RowOf(c);
                foreach (HeaderButtonKind k in new[] { HeaderButtonKind.Speed, HeaderButtonKind.OwnedCards, HeaderButtonKind.Journal, HeaderButtonKind.Settings })
                {
                    if (!row.Has(k)) continue;
                    TableBox button = row.Box(k);
                    // the speed's line is the longest: about 30 letters of 16 px
                    TableBox tip = HeaderBand.TipBox(button, k == HeaderButtonKind.Speed ? 480f : 140f);
                    Assert.That(tip.Y, Is.GreaterThanOrEqualTo(HeaderBand.Band.Bottom), s + " " + k + ": under the band");
                    Assert.That(tip.Y, Is.EqualTo(96f + 8f));
                    Assert.That(tip.Inside(Stage), Is.True, s + " " + k + " " + tip);
                    Assert.That(tip.Right, Is.LessThanOrEqualTo(button.Right + 0.01f), s + " " + k + ": right edge on the button's");
                    Assert.That(tip.H, Is.EqualTo(22f + 16f + 2f));
                }
            }
            // a bubble wider than the room left of its button is held at the padding
            TableBox narrow = new TableBox(30f, 11.5f, 76f, 72f);
            Assert.That(HeaderBand.TipBox(narrow, 300f).X, Is.EqualTo(24f));
        }

        [Test]
        public void TheWords_AreComposedHere()
        {
            HeaderFigure days = HeaderBand.DaysFigure(87);
            Assert.That((days.Lead, days.Number, days.Tail), Is.EqualTo(("残り", "87", "日")));
            Assert.That(days.Text, Is.EqualTo("残り87日"));
            HeaderFigure hp = HeaderBand.OutOf(38, 50);
            Assert.That((hp.Lead, hp.Number, hp.Tail), Is.EqualTo(("", "38", "/ 50")));
            Assert.That(hp.Text, Is.EqualTo("38 / 50"));
            Assert.That(HeaderBand.OutOf(8, 12).Text, Is.EqualTo("8 / 12"), "the stamina's real maximum, a temporary rise included");
            Assert.That(HeaderBand.TurnTag(3), Is.EqualTo("ターン 3"));
            Assert.That(HeaderBand.FightTag(2, 3), Is.EqualTo("連戦 2 / 3"));
            Assert.That(HeaderBand.SpeedLabel(BattleSpeedStep.Normal), Is.EqualTo("速さ ×1.25"));
            Assert.That(HeaderBand.SpeedLabel(BattleSpeedStep.Slow), Is.EqualTo("速さ ×1.0"));
            Assert.That(HeaderBand.Count(20), Is.EqualTo("20"));
            Assert.That(HeaderBand.WordOf(HeaderButtonKind.OwnedCards), Is.EqualTo("所持カード"));
            Assert.That(HeaderBand.WordOf(HeaderButtonKind.Journal), Is.EqualTo("手記"));
            Assert.That(HeaderBand.WordOf(HeaderButtonKind.Settings), Is.EqualTo("設定"));
            Assert.That(HeaderBand.WordOf(HeaderButtonKind.Speed, "速さ ×1.5"), Is.EqualTo("速さ ×1.5"));
            Assert.That(HeaderBand.TipOf(HeaderButtonKind.Journal), Is.EqualTo("手記（J）"));
            Assert.That(HeaderBand.TipOf(HeaderButtonKind.OwnedCards), Is.EqualTo("所持カード（C）"));
            Assert.That(HeaderBand.TipOf(HeaderButtonKind.Settings), Is.EqualTo("設定（Esc）"));
            Assert.That(HeaderBand.TipOf(HeaderButtonKind.Speed), Is.EqualTo(BattleSpeed.Tooltip));
        }

        [Test]
        public void TheBattlesTags_AddTheRunOfFights_FromTwoFights()
        {
            Assert.That(HeaderBand.BattleTags(3, 1, 1), Is.EqualTo(new[] { "ターン 3" }));
            Assert.That(HeaderBand.BattleTags(3, 0, 0), Is.EqualTo(new[] { "ターン 3" }));
            Assert.That(HeaderBand.BattleTags(5, 2, 3), Is.EqualTo(new[] { "ターン 5", "連戦 2 / 3" }));
            Assert.That(HeaderBand.BattleTags(1, 1, 2), Is.EqualTo(new[] { "ターン 1", "連戦 1 / 2" }));
        }

        [Test]
        public void EveryIcon_StaysInsideItsBox()
        {
            var paths = typeof(HeaderBand.Icons).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral)
                .Select(f => (f.Name, D: (string)f.GetRawConstantValue())).ToList();
            Assert.That(paths.Count, Is.EqualTo(8));
            foreach ((string name, string d) in paths)
            {
                RectF b = PathData.Parse(d).ControlBounds();
                Assert.That(b.Left >= 0f && b.Top >= 0f && b.Right <= 24f && b.Bottom <= 24f, Is.True, name + " " + b);
            }
        }

        private static readonly Regex FontSize = new Regex(@"(?:font:\s*(?:\d+\s+)?(?:italic\s+)?([\d.]+)px|font-size:\s*([\d.]+)px)", RegexOptions.CultureInvariant);

        private static IEnumerable<(string Name, string Css)> Rules() =>
            typeof(HeaderBand.Css).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral)
                .Select(f => (f.Name, (string)f.GetRawConstantValue()));

        [Test]
        public void EveryText_IsSixteenPxOrMore_InTheBodyColourUnderTwentyFour()
        {
            int texts = 0;
            foreach ((string name, string css) in Rules())
            {
                foreach (Match m in FontSize.Matches(css))
                {
                    texts++;
                    float px = float.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, CultureInfo.InvariantCulture);
                    Assert.That(px, Is.GreaterThanOrEqualTo(16f), name);
                    // the count is dark on the amber pill, as the canvas draws it
                    if (px < 24f && name != nameof(HeaderBand.Css.BadgeText))
                        Assert.That(Regex.IsMatch(css, @"(^|;)\s*color\s*:"), Is.False, name + ": under 24 px the text keeps the body colour");
                }
            }
            Assert.That(texts, Is.EqualTo(9), "the band's texts were read");
        }

        [Test]
        public void Amber_StaysOnTheStaminaAndTheDecksCount()
        {
            string[] amber = Rules().Where(r => r.Css.Contains("--amber")).Select(r => r.Name).OrderBy(n => n).ToArray();
            Assert.That(amber, Is.EqualTo(new[] { "Badge", "StaminaPip" }));
        }
    }
}
