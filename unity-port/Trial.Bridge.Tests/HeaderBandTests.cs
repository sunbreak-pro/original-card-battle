// The shared header band (#1074): the four screens stand their buttons in one order, the band is as tall as the
// exploration map's top, the buttons stand in one place on every screen, the count and the bubbles sit where the canvas
// puts them, every text is 16 px or more, and amber stays on the stamina and the deck's count.
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
        public void EveryScreen_StandsItsButtonsInOneOrder_WithItsOwnStates()
        {
            HeaderButtonState hidden = HeaderButtonState.Hidden, ready = HeaderButtonState.Ready, off = HeaderButtonState.Off, on = HeaderButtonState.On;
            var expected = new Dictionary<HeaderScreen, HeaderButtonState[]>
            {
                // speed, owned cards, journal, settings
                [HeaderScreen.Base] = new[] { hidden, ready, ready, ready },
                [HeaderScreen.Table] = new[] { hidden, on, ready, ready },
                [HeaderScreen.Explore] = new[] { hidden, ready, ready, ready },
                [HeaderScreen.Battle] = new[] { ready, off, ready, ready },
            };
            HeaderButtonKind[] kinds = { HeaderButtonKind.Speed, HeaderButtonKind.OwnedCards, HeaderButtonKind.Journal, HeaderButtonKind.Settings };
            foreach (HeaderScreen s in Screens)
            {
                HeaderContent c = ContentOf(s);
                Assert.That(c.Screen, Is.EqualTo(s));
                Assert.That(kinds.Select(c.StateOf).ToArray(), Is.EqualTo(expected[s]), s + ": each button's state");
                HeaderButtonRow row = RowOf(c);
                HeaderButtonKind[] leftToRight = kinds.Where(k => c.StateOf(k).Shown && row.Has(k)).OrderBy(k => row.Box(k).X).ToArray();
                HeaderButtonKind[] want = s == HeaderScreen.Battle ? kinds : kinds.Skip(1).ToArray();
                Assert.That(leftToRight, Is.EqualTo(want), s + ": the buttons stand in one order");
            }
            Assert.That(HeaderBand.PlaceWidth, Is.EqualTo(HeaderPlaceWidth.Packed), "packed to the left by default");
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
        public void WithoutACalendar_HpFollowsTheRule()
        {
            HeaderLeftRow with = HeaderBand.LeftRow(300f, 150f, 110f, 100f);
            HeaderLeftRow without = HeaderBand.LeftRow(300f, 150f, 110f, 100f, withCalendar: false);
            Assert.That(with.HasCalendar, Is.True);
            Assert.That(without.HasCalendar, Is.False);
            Assert.That(without.Divider.X, Is.EqualTo(with.Divider.X));
            Assert.That(without.HpX, Is.EqualTo(without.Divider.Right + 28f), "no slot and no gap is left where the calendar stood");
            Assert.That(without.StaminaX, Is.EqualTo(without.HpX + 110f + 28f));
            Assert.That(without.Right, Is.EqualTo(with.Right - 150f - 28f));
        }

        [Test]
        public void ALongNote_FitsOnlyWithTheClearanceBeforeTheButtons()
        {
            Assert.That(HeaderBand.ButtonsClearance, Is.EqualTo(28f), "the band's own gap between groups");
            HeaderButtonRow buttons = HeaderBand.ButtonRow(SpeedW, OwnedCardsW, JournalW, SettingsW);
            // a row whose right edge lands exactly on the buttons' left edge less the clearance fits; a px more does not
            float edge = buttons.Left - HeaderBand.ButtonsClearance;
            HeaderLeftRow probe = HeaderBand.LeftRow(300f, 150f, 110f, 100f);
            float calendarW = 150f + (edge - probe.Right);
            HeaderLeftRow atEdge = HeaderBand.LeftRow(300f, calendarW, 110f, 100f);
            Assert.That(atEdge.Right, Is.EqualTo(edge).Within(1e-3f));
            Assert.That(HeaderBand.Fits(atEdge, buttons.Left), Is.True);
            Assert.That(HeaderBand.Fits(HeaderBand.LeftRow(300f, calendarW + 1f, 110f, 100f), buttons.Left), Is.False);
            Assert.That(HeaderBand.Fits(probe, buttons.Left), Is.True, "a short row fits");
            Assert.That(HeaderBand.Fits(null, buttons.Left), Is.False);
        }

        [Test]
        public void TheShortNote_KeepsTheDays_AndDropsOnlyTheLongWords()
        {
            HeaderCalendar longNote = HeaderCalendar.Of(41, true, "竜王が戻るまで 12 日。暦は止まっている");
            HeaderCalendar shortNote = longNote.Shortened();
            Assert.That((shortNote.DaysLeft, shortNote.Frozen, shortNote.ShownNote), Is.EqualTo((41, true, HeaderBand.FrozenNote)));
            Assert.That(longNote.ShownNote, Is.EqualTo("竜王が戻るまで 12 日。暦は止まっている"), "the calendar handed in keeps its long note");
            Assert.That(HeaderCalendar.Of(41, false).Shortened().ShownNote, Is.EqualTo(""), "a running calendar has no note either way");
        }

        [Test]
        public void EveryTabStop_IsNamedByItsKind()
        {
            Assert.That(HeaderBand.StopOf(HeaderButtonKind.Speed), Is.EqualTo(HeaderStop.Speed));
            Assert.That(HeaderBand.StopOf(HeaderButtonKind.OwnedCards), Is.EqualTo(HeaderStop.OwnedCards));
            Assert.That(HeaderBand.StopOf(HeaderButtonKind.Journal), Is.EqualTo(HeaderStop.Journal));
            Assert.That(HeaderBand.StopOf(HeaderButtonKind.Settings), Is.EqualTo(HeaderStop.Settings));
            Assert.That(HeaderBand.StopWord(HeaderStop.Back), Is.EqualTo("拠点へ戻る"));
            Assert.That(HeaderBand.StopWord(HeaderStop.Speed), Is.EqualTo("速さ"));
            Assert.That(HeaderBand.StopWord(HeaderStop.OwnedCards), Is.EqualTo("所持カード"));
            Assert.That(HeaderBand.StopWord(HeaderStop.Journal), Is.EqualTo("手記"));
            Assert.That(HeaderBand.StopWord(HeaderStop.Settings), Is.EqualTo("設定"));
            foreach (HeaderButtonKind kind in System.Enum.GetValues(typeof(HeaderButtonKind)))
                Assert.That(HeaderBand.StopWord(HeaderBand.StopOf(kind)), Is.EqualTo(HeaderBand.WordOf(kind)), kind.ToString());
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
            Assert.That(HeaderBand.TipOf(HeaderScreen.Battle, HeaderButtonKind.Speed), Is.EqualTo(BattleSpeed.Tooltip));
        }

        [Test]
        public void TheOwnedCardsBubble_NamesTheCKey_OnTheExplorationOnly()
        {
            Assert.That(HeaderBand.TipOf(HeaderScreen.Explore, HeaderButtonKind.OwnedCards), Is.EqualTo("所持カード（C）"), "C opens them on the map");
            foreach (HeaderScreen s in new[] { HeaderScreen.Base, HeaderScreen.Table, HeaderScreen.Battle })
            {
                string tip = HeaderBand.TipOf(s, HeaderButtonKind.OwnedCards);
                Assert.That(tip, Is.EqualTo("所持カード"), s + ": no C key here");
                Assert.That(tip, Does.Not.Contain("C"), s.ToString());
            }
            foreach (HeaderScreen s in Screens)
            {
                Assert.That(HeaderBand.TipOf(s, HeaderButtonKind.Journal), Is.EqualTo("手記（J）"), s + ": J opens the journal everywhere");
                Assert.That(HeaderBand.TipOf(s, HeaderButtonKind.Settings), Is.EqualTo("設定（Esc）"), s + ": Esc opens the settings everywhere");
            }
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
