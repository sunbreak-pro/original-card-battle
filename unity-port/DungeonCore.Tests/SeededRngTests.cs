using DungeonCore;

namespace DungeonCore.Tests
{
    public class SeededRngTests
    {
        [Test]
        public void SplitMix64_MatchesTheReferenceVectors()
        {
            // SplitMix64 from seed 0. Pinned so a refactor cannot silently move every map that
            // was ever generated, and so Unity's Mono and .NET agree on the numbers.
            var rng = new SeededRng(0UL);
            Assert.That(rng.NextUInt64(), Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x06C45D188009454FUL));
        }

        [Test]
        public void NextDouble_StaysInRange()
        {
            var rng = new SeededRng(12345);
            for (int i = 0; i < 2000; i++)
            {
                double value = rng.NextDouble();
                Assert.That(value, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(value, Is.LessThan(1.0));
            }
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new SeededRng(99UL);
            var b = new SeededRng(99UL);
            for (int i = 0; i < 50; i++) Assert.That(b.NextUInt64(), Is.EqualTo(a.NextUInt64()));
        }

        [Test]
        public void NextInt_NeverLeavesTheRange_EvenWhenTheSourceReturnsOne()
        {
            var degenerate = new FixedRng(1.0);
            Assert.That(degenerate.NextInt(0, 3), Is.EqualTo(2));

            var rng = new SeededRng(7);
            for (int i = 0; i < 2000; i++)
            {
                Assert.That(rng.NextInt(5, 9), Is.InRange(5, 8));
            }
        }

        [Test]
        public void NextInt_EmptyRangeReturnsTheMinimum()
        {
            Assert.That(new SeededRng(1).NextInt(4, 4), Is.EqualTo(4));
        }

        [Test]
        public void Shuffle_IsAPermutation()
        {
            var items = Enumerable.Range(0, 20).ToList();
            new SeededRng(3).Shuffle(items);
            Assert.That(items.OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, 20)));
        }

        [Test]
        public void Shuffle_DependsOnTheSeed()
        {
            var a = Enumerable.Range(0, 20).ToList();
            var b = Enumerable.Range(0, 20).ToList();
            new SeededRng(3).Shuffle(a);
            new SeededRng(4).Shuffle(b);
            Assert.That(a, Is.Not.EqualTo(b));
        }
    }
}
