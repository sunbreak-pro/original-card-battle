// dotnet-only: where a standing picture is laid over a figure (ArtStand, #772).
using NUnit.Framework;
using Trial.Battle;

namespace Trial.Battle.Flow.Tests
{
    public class ArtStandTests
    {
        private const float Eps = 0.01f;

        [Test]
        public void APictureWithoutMarksSpansTheFigureFromTopToBottom()
        {
            ArtRect r = ArtStand.Place("spearman_x", 400f, 800f, 200f, 60f);
            Assert.That(r.W, Is.EqualTo(100f).Within(Eps));
            Assert.That(r.H, Is.EqualTo(200f).Within(Eps));
            Assert.That(r.X, Is.EqualTo(10f).Within(Eps), "the middle of the picture on the feet");
            Assert.That(r.Y, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void ANullIdIsReadAsAPictureWithoutMarks()
        {
            ArtMarks m = ArtStand.MarksOf(null, 300f, 600f);
            Assert.That(m.Top, Is.EqualTo(0f));
            Assert.That(m.Sole, Is.EqualTo(600f));
            Assert.That(m.CenterX, Is.EqualTo(150f));
        }

        [Test]
        public void TheSwordsmanStandsOnHisSolesWithHisHeadAtTheFigureTop()
        {
            // The player's figure as measured in play mode: 226 tall, feet at 61.6 of its box.
            ArtRect r = ArtStand.Place("swordsman", 640f, 1536f, 226f, 61.6f);
            float k = 226f / (1434f - 180f);
            Assert.That(r.W, Is.EqualTo(640f * k).Within(Eps));
            Assert.That(r.H, Is.EqualTo(1536f * k).Within(Eps));
            Assert.That(r.X, Is.EqualTo(61.6f - 274f * k).Within(Eps));
            Assert.That(r.Y, Is.EqualTo(226f - 1434f * k).Within(Eps));
            Assert.That(r.Y + 180f * k, Is.EqualTo(0f).Within(Eps), "the head top on the figure's top");
            Assert.That(r.Y + 1434f * k, Is.EqualTo(226f).Within(Eps), "the soles on the feet line");
            Assert.That(r.W, Is.EqualTo(115.34f).Within(0.05f));
            Assert.That(r.H, Is.EqualTo(276.82f).Within(0.05f));
            Assert.That(r.Y, Is.EqualTo(-32.44f).Within(0.05f));
        }

        [Test]
        public void TheSwordsmanImportedSmallerStandsTheSame()
        {
            ArtRect full = ArtStand.Place("swordsman", 640f, 1536f, 226f, 61.6f);
            ArtRect half = ArtStand.Place("swordsman", 320f, 768f, 226f, 61.6f);
            Assert.That(half.X, Is.EqualTo(full.X).Within(Eps));
            Assert.That(half.Y, Is.EqualTo(full.Y).Within(Eps));
            Assert.That(half.W, Is.EqualTo(full.W).Within(Eps));
            Assert.That(half.H, Is.EqualTo(full.H).Within(Eps));
        }

        [Test]
        public void AnEmptyPictureLaysNothing()
        {
            ArtRect r = ArtStand.Place("spearman_x", 0f, 0f, 200f, 60f);
            Assert.That(r.W, Is.EqualTo(0f));
            Assert.That(r.H, Is.EqualTo(0f));
        }
    }
}
