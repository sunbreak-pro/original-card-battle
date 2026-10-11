// #455: the places of battle-visual-v1 §4.9's parts (Script/BottomRightLayout.cs) and the tally they
// draw (PlayedFrame). Runs under `dotnet test` (Depiction.Script.Tests.csproj) and in Unity's EditMode
// runner from the same file.
using NUnit.Framework;

namespace Depiction.Tests
{
    public class BottomRightLayoutTests
    {
        [Test]
        public void ThePlayedFrames_Are40Square_6Apart_24FromTheRight_AtY896()
        {
            Assert.That(PlayedLayout.Order, Is.EqualTo(new[] { CardKind.Attack, CardKind.Guard, CardKind.Skill, CardKind.Stance }));
            Assert.That(PlayedLayout.LeftOf(3) + PlayedLayout.Size, Is.EqualTo(1920f - 24f));
            Assert.That(PlayedLayout.LeftOf(1) - PlayedLayout.LeftOf(0), Is.EqualTo(46f));
            Assert.That(PlayedLayout.LeftOf(0), Is.EqualTo(1920f - 24f - 178f));
            Assert.That(PlayedLayout.Top, Is.EqualTo(896f));
            Assert.That(PlayedLayout.BadgeSize, Is.EqualTo(20f));
            Assert.That(PlayedLayout.BadgeText, Is.EqualTo(14));
            Assert.That(PlayedLayout.UnplayedAlpha, Is.EqualTo(0.55f));
        }

        [Test]
        public void ThePlayedFrames_EndAboveTheEndTurnPlate()
        {
            // §4.9 ターン終了: right 24, y 976, 224×72. The frames end at y 936 and the underline 6 px below.
            Assert.That(PlayedLayout.Top + PlayedLayout.Size + 6f, Is.LessThan(976f));
        }

        [Test]
        public void TheReserveHint_StandsAtX1560Y984_ClearOfTheEndTurnPlate()
        {
            Assert.That(ReserveHintLayout.Left, Is.EqualTo(1560f));
            Assert.That(ReserveHintLayout.Top, Is.EqualTo(984f));
            Assert.That(ReserveHintLayout.Height, Is.EqualTo(56f));
            Assert.That(ReserveHintLayout.ShieldSize, Is.EqualTo(30f));
            Assert.That(ReserveHintLayout.TextSize, Is.EqualTo(24));
            Assert.That(ReserveHintLayout.Left + ReserveHintLayout.Width, Is.LessThan(1920f - 24f - 224f), "the plate starts at x 1672");
        }

        [Test]
        public void ThePlayedTally_CountsEachAttribute_AndKeepsTheLast()
        {
            var played = new PlayedFrame { Shown = true };
            played.Add(CardKind.Attack);
            played.Add(CardKind.Attack);
            played.Add(CardKind.Guard);
            played.Add(CardKind.Move); // movement is no attribute (battle_core_v4 §2.1): not counted, not last
            Assert.That(played.CountOf(CardKind.Attack), Is.EqualTo(2));
            Assert.That(played.CountOf(CardKind.Guard), Is.EqualTo(1));
            Assert.That(played.CountOf(CardKind.Skill), Is.EqualTo(0));
            Assert.That(played.CountOf(CardKind.Move), Is.EqualTo(0));
            Assert.That(played.Last, Is.EqualTo(CardKind.Guard));
        }
    }
}
