using NUnit.Framework;

namespace Depiction.Tests
{
    public class ReceiverDropTests
    {
        // The receiver as the prefab builds it (340 x 340) and a hand card (216 x 304), on the 1920 x 1080 canvas.
        private static readonly ScreenBox Receiver = new ScreenBox(1190f, 170f, 340f, 340f);

        private static ScreenBox CardAt(float x, float y) => new ScreenBox(x, y, 216f, 304f);

        [Test]
        public void ACardWhollyOnTheReceiver_SharesAllOfItsArea()
        {
            Assert.That(ReceiverDrop.Share(CardAt(1200f, 180f), Receiver), Is.EqualTo(1f));
            Assert.That(ReceiverDrop.Takes(CardAt(1200f, 180f), Receiver, 1000f, 100f), Is.True);
        }

        [Test]
        public void ExactlyHalfTheCard_OnTheReceiver_IsTaken_WithThePointerOutside()
        {
            // 108 of the card's 216 px lie inside, the whole height does.
            ScreenBox card = CardAt(1190f - 108f, 188f);

            Assert.That(ReceiverDrop.Share(card, Receiver), Is.EqualTo(0.5f));
            Assert.That(ReceiverDrop.Takes(card, Receiver, 1000f, 300f), Is.True);
        }

        [Test]
        public void LessThanHalf_WithThePointerOutside_IsNotTaken()
        {
            ScreenBox card = CardAt(1190f - 109f, 188f);

            Assert.That(ReceiverDrop.Share(card, Receiver), Is.LessThan(0.5f));
            Assert.That(ReceiverDrop.Takes(card, Receiver, 1000f, 300f), Is.False);
        }

        [Test]
        public void ThePointerInside_IsTaken_EvenWithLittleOverlap()
        {
            // Held by its right edge: 16 px of the card lie inside, the pointer does too.
            ScreenBox card = CardAt(1190f - 200f, 188f);

            Assert.That(ReceiverDrop.Share(card, Receiver), Is.LessThan(0.1f));
            Assert.That(ReceiverDrop.Takes(card, Receiver, 1195f, 300f), Is.True);
        }

        [Test]
        public void NoOverlap_WithThePointerOutside_IsNotTaken()
        {
            ScreenBox card = CardAt(800f, 188f);

            Assert.That(ReceiverDrop.Share(card, Receiver), Is.EqualTo(0f));
            Assert.That(ReceiverDrop.Takes(card, Receiver, 900f, 300f), Is.False);
        }

        [Test]
        public void ACardTouchingTheEdge_SharesNothing_AndThePointerCountsOnTheLeftAndTopEdgesOnly()
        {
            Assert.That(ReceiverDrop.Share(CardAt(1190f - 216f, 188f), Receiver), Is.EqualTo(0f));
            ScreenBox away = CardAt(0f, 0f);
            Assert.That(ReceiverDrop.Takes(away, Receiver, 1190f, 170f), Is.True);
            Assert.That(ReceiverDrop.Takes(away, Receiver, 1530f, 300f), Is.False);
            Assert.That(ReceiverDrop.Takes(away, Receiver, 1300f, 510f), Is.False);
        }
    }
}
