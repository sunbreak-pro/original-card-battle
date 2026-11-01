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

        // ---- §3.9 (#589): a receiver per standing enemy ------------------------------------------------

        /// <summary>Two receivers over neighbours on 8 cells (165 px apart), so they overlap by 175.</summary>
        private static readonly ScreenBox[] TwoReceivers =
        {
            new ScreenBox(1000f, 170f, 340f, 340f),
            new ScreenBox(1165f, 170f, 340f, 340f),
        };

        [TestCase(1100f, 0)] // inside the first only
        [TestCase(1200f, 0)] // inside both, nearer the first's middle (1170)
        [TestCase(1300f, 1)] // inside both, nearer the second's middle (1335)
        [TestCase(1400f, 1)] // inside the second only
        public void ThePointer_PicksTheReceiverWhoseMiddleIsNearest(float pointerX, int expected)
        {
            Assert.That(ReceiverDrop.Pick(CardAt(0f, 0f), TwoReceivers, pointerX, 300f), Is.EqualTo(expected));
        }

        [Test]
        public void WithThePointerOutside_TheCardGoesWhereMostOfItLies()
        {
            // The pointer is below both; the card lies wholly on the second and half on the first.
            ScreenBox card = CardAt(1232f, 180f);
            Assert.That(ReceiverDrop.Pick(card, TwoReceivers, 1300f, 600f), Is.EqualTo(1));
            // Mostly off to the left, half on the first and not on the second.
            ScreenBox left = CardAt(892f, 180f);
            Assert.That(ReceiverDrop.Pick(left, TwoReceivers, 900f, 600f), Is.EqualTo(0));
        }

        [Test]
        public void OffEveryReceiver_NoEnemyIsPicked()
        {
            Assert.That(ReceiverDrop.Pick(CardAt(100f, 180f), TwoReceivers, 100f, 600f), Is.EqualTo(-1));
            Assert.That(ReceiverDrop.Pick(CardAt(1000f, 180f), new ScreenBox[0], 1100f, 300f), Is.EqualTo(-1));
        }

        [Test]
        public void TheFallen_OffTheList_CannotBePicked()
        {
            // The first fell: only the second's receiver is offered, so a pointer over the first's place takes none.
            var standing = new[] { TwoReceivers[1] };
            Assert.That(ReceiverDrop.Pick(CardAt(0f, 0f), standing, 1100f, 300f), Is.EqualTo(-1));
            Assert.That(ReceiverDrop.Pick(CardAt(0f, 0f), standing, 1300f, 300f), Is.EqualTo(0));
        }
    }
}
