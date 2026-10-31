// The words of a trace's gains (the browser trial's src/bridge/exploration/TraceGain.js:55 TraceGainText), byte for byte:
// every expected string below is what the JS printed for the same arguments. Kept apart from TraceGainTests, which
// Unity cannot compile yet (it names InheritanceCore's types), so the EditMode runner checks the words too. Runs under
// `dotnet test` (Exploration.Bridge.Tests) and in Unity's EditMode runner.
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class TraceGainTextTests
    {
        [Test]
        public void TraceGainText_IsTheJsWords()
        {
            // js: bridge/exploration/TraceGain.js:55 (each string printed by the JS with these arguments)
            Assert.That(TraceGainText.Capped(2), Is.EqualTo("3 段目の札がこの生にあるので 2 段目止まり"));
            Assert.That(TraceGainText.Now(1), Is.EqualTo("いまも 1 段目"));
            Assert.That(TraceGainText.Already(2), Is.EqualTo("すでに 2 段目"));
            Assert.That(TraceGainText.Up(0, 1), Is.Null);
            Assert.That(TraceGainText.Up(1, 3), Is.EqualTo("いま 1 段目 → 3 段目"));
            Assert.That(TraceGainText.RankUp(0, 2), Is.EqualTo("習熟 0 → 2 段目"));
            Assert.That(TraceGainText.DeckCap(45), Is.EqualTo("デッキが上限 45 枚"));
            Assert.That(TraceGainText.Copies(3), Is.EqualTo("デッキに同じ札が 3 枚"));
            Assert.That(TraceGainText.OwnedOnly(null, false), Is.EqualTo("所持カードにだけ入る"));
            Assert.That(TraceGainText.OwnedOnly("x", true), Is.EqualTo("所持カードにだけ入った（x）"));
            Assert.That(TraceGainText.OwnedOnly("", true), Is.EqualTo("所持カードにだけ入った"));
            Assert.That(TraceGainText.Joined, Is.EqualTo("デッキに 1 枚入った"));
            Assert.That(TraceGainText.Joins, Is.EqualTo("デッキに 1 枚入る"));
            Assert.That(TraceGainText.Nothing("y", false), Is.EqualTo("この生では何も増えない（y）"));
            Assert.That(TraceGainText.Nothing("y", true), Is.EqualTo("この生では何も増えなかった（y）"));
            Assert.That(TraceGainText.DeckFullLead(40), Is.EqualTo("デッキが上限（40 枚）なので、受け取った札はデッキに入らず、所持カードにだけ入る。"));
            Assert.That(TraceGainText.InTrace("突き", 3), Is.EqualTo("痕跡では 突き（3 段目）"));
            Assert.That(TraceGainText.PageUp(0, 2), Is.Null);
            Assert.That(TraceGainText.PageUp(1, 2), Is.EqualTo("いま 開示度 1 → 2"));
            Assert.That(TraceGainText.PageRaised(0, 1), Is.EqualTo("開示度 0 → 1"));
            Assert.That(TraceGainText.PageNow(1), Is.EqualTo("いまも 開示度 1 / 2"));
            Assert.That(TraceGainText.PageAlready(2), Is.EqualTo("すでに 開示度 2 / 2"));
            Assert.That(TraceGainText.PageInTrace(1), Is.EqualTo("痕跡では 開示度 1 / 2"));
            Assert.That(TraceGainText.ScribbleHeld, Is.EqualTo("すでに手記にある"));
        }

        [Test]
        public void TheCopiesNote_IsTheCoresCap()
        {
            // js: bridge/exploration/TraceGain.js:181 Copies(Constants.CopiesMax)
            Assert.That(TraceGainText.Copies(BattleCore.Constants.CopiesMax), Is.EqualTo("デッキに同じ札が 3 枚"));
        }
    }
}
