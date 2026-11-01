using System.Linq;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Script.Tests
{
    /// <summary>The meta screens' computed times (title.js, hall.js, ending.js) and their breaths, against the analysis' numbers.</summary>
    public class MetaTimelinesTests
    {
        [Test]
        public void Chars_SplitsAsTheJsSpread_KeepingSurrogatePairs()
        {
            Assert.That(MetaTimelines.Chars("瘴気死"), Is.EqualTo(new[] { "瘴", "気", "死" }));
            Assert.That(MetaTimelines.Chars("第 7 層"), Is.EqualTo(new[] { "第", " ", "7", " ", "層" }));
            Assert.That(MetaTimelines.Chars("\U00020BB7a"), Is.EqualTo(new[] { "\U00020BB7", "a" }));
            Assert.That(MetaTimelines.Chars(""), Is.Empty);
            Assert.That(MetaTimelines.Chars(null), Is.Empty);
        }

        [Test]
        public void TheTitle_TaglineAndMenu()
        {
            Assert.That(MetaTimelines.TitleTaglineCharAt(0), Is.EqualTo(900));
            Assert.That(MetaTimelines.TitleTaglineCharAt(23), Is.EqualTo(2855), "the last of 24 letters");
            Assert.That(Enumerable.Range(0, 3).Select(MetaTimelines.TitleMenuItemAt), Is.EqualTo(new[] { 1800, 1910, 2020 }));
        }

        [Test]
        public void TheSeal_LandsAfterTheName()
        {
            Assert.That(MetaTimelines.SealAt(3), Is.EqualTo(1820), "a three-letter name");
            Assert.That(MetaTimelines.SealAt("灯守り"), Is.EqualTo(1820));
            Assert.That(MetaTimelines.HallNameCharAt(2), Is.EqualTo(1470));
            Assert.That(MetaTimelines.SealStampedAt(1820), Is.EqualTo(1990));
            Assert.That(Enumerable.Range(0, 3).Select(MetaTimelines.HallAttireAfterStamp), Is.EqualTo(new[] { 120, 240, 360 }));
            Assert.That(MetaTimelines.HallStartButtonAt(1820), Is.EqualTo(2320));
            Assert.That(MetaTimelines.HallKeysAt(1820), Is.EqualTo(2520));
        }

        [Test]
        public void TheHallColumns_RiseInTurn()
        {
            Assert.That(MetaTimelines.RiseInAt(380, 90, 2), Is.EqualTo(560));
            Assert.That(MetaTimelines.HallLivesAt(0), Is.EqualTo(380));
            Assert.That(MetaTimelines.HallLivesAt(3), Is.EqualTo(650));
            Assert.That(MetaTimelines.HallStrataAt(7), Is.EqualTo(900), "the last stratum after the heading");
            Assert.That(MetaTimelines.HallHeirAt(1), Is.EqualTo(1080));
        }

        [Test]
        public void TheEndingCard_T1T2T3()
        {
            // meta-a §4.2: 倒れた (3 letters), a 14-letter sentence, 3 lines → t1 1420, t2 2352, t3 3632, the button at 3932
            EndingTimeline t = MetaTimelines.Ending(3, 14, 3);
            Assert.That(new[] { t.T1, t.T2, t.T3, t.ButtonAt }, Is.EqualTo(new[] { 1420, 2352, 3632, 3932 }));
            Assert.That(MetaTimelines.EndingTitleCharAt(2), Is.EqualTo(1080));
            Assert.That(MetaTimelines.EndingSentenceCharAt(1420, 13), Is.EqualTo(1420 + 200 + 13 * 38));
            Assert.That(MetaTimelines.EndingLineAt(2352, 2), Is.EqualTo(2352 + 300 + 520));
        }

        [Test]
        public void TheEndingCard_ReadsTheView()
        {
            LifeStart s = Inheritance.StartLife(Inheritance.NewLineage(7UL), 1UL);
            Lineage l = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(7, 3, null, RunPhase.Fallen),
                new[] { new CardEntry("tsuki", 2) }, new InheritanceCore.Journal());
            EndingView view = Inheritance.EndingView(l);
            EndingTimeline t = MetaTimelines.Ending(view);
            int title = MetaTimelines.Chars(view.Title).Count;
            int sentence = MetaTimelines.Chars(view.Sentence).Count;
            Assert.That(title, Is.EqualTo(3));
            Assert.That(sentence, Is.EqualTo(view.Sentence.Length));
            Assert.That(t.T1, Is.EqualTo(800 + title * 140 + 200));
            Assert.That(t.T2, Is.EqualTo(t.T1 + 200 + sentence * 38 + 200));
            Assert.That(t.T3, Is.EqualTo(t.T2 + 300 + view.Lines.Count * 260 + 200));
        }

        [Test]
        public void TheEndingCard_OfAFallOnLayer7_IsTheJsTimes()
        {
            // node: endingView of a 倒れた life on layer 7 (title 3 letters, 「第 7 層の戦いで倒れた。」 13, three lines)
            // gives t1 1420, t2 2314, t3 3594 in ending.js (meta-a.md §4.2's 2352 / 3632 count the sentence as 14).
            LifeStart s = Inheritance.StartLife(Inheritance.NewLineage(7UL), 1UL);
            Lineage l = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(7, 3, null, RunPhase.Fallen),
                new[] { new CardEntry("tsuki", 2), new CardEntry("harai", 1) },
                InheritanceCore.Journal.WithPageCount(0, new[] { new System.Collections.Generic.KeyValuePair<string, int>("sabiyari", 1) }, ""));
            EndingView view = MetaLines.WithWaitingTrace(Inheritance.EndingView(l), l);
            Assert.That(view.Sentence, Is.EqualTo("第 7 層の戦いで倒れた。"));
            Assert.That(view.Lines.Count, Is.EqualTo(3));
            EndingTimeline t = MetaTimelines.Ending(view);
            Assert.That(new[] { t.T1, t.T2, t.T3, t.ButtonAt }, Is.EqualTo(new[] { 1420, 2314, 3594, 3894 }));
        }

        [Test]
        public void TheBreaths()
        {
            Assert.That(MetaTimelines.Breath(0, 4200), Is.EqualTo(0).Within(1e-12));
            Assert.That(MetaTimelines.Breath(2100, 4200), Is.EqualTo(1).Within(1e-12));
            Assert.That(MetaTimelines.Breath(0, 1900, 950), Is.EqualTo(1).Within(1e-12));

            Assert.That(MetaTimelines.TitleFootLight(0), Is.EqualTo(0.55).Within(1e-12));
            Assert.That(MetaTimelines.TitleFootLight(2100), Is.EqualTo(0.90).Within(1e-12));

            Assert.That(MetaTimelines.HallFlameOpacity(0, 0), Is.EqualTo(0.55).Within(1e-12));
            Assert.That(MetaTimelines.HallFlameOpacity(950, 0), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(MetaTimelines.HallFlameScale(950, 0), Is.EqualTo(1.10).Within(1e-12));
            Assert.That(MetaTimelines.HallFlameScale(0, 0), Is.EqualTo(0.85).Within(1e-12));
            // the i-th flame is 700 ms ahead: flame 1 at now 250 is flame 0 at 950
            Assert.That(MetaTimelines.HallFlameOpacity(250, 1), Is.EqualTo(MetaTimelines.HallFlameOpacity(950, 0)).Within(1e-12));

            Assert.That(MetaTimelines.EndingGlow(0, died: true), Is.EqualTo(0.35).Within(1e-12));
            Assert.That(MetaTimelines.EndingGlow(3100, died: true), Is.EqualTo(0.60).Within(1e-12));
            Assert.That(MetaTimelines.EndingGlow(0, died: false), Is.EqualTo(0.60).Within(1e-12));
            Assert.That(MetaTimelines.EndingGlow(1900, died: false), Is.EqualTo(0.85).Within(1e-12));
        }
    }
}
