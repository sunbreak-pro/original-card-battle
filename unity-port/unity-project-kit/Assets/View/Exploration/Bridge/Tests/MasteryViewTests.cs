// X-15 (the browser trial's test/ext/last-round.test.mjs:26-56): a card's name at each 段 and its 刻み toward the next
// one, as the exploration's rank-up lines and training card read them (Exploration.Bridge.MasteryView). Runs under
// `dotnet test` (Exploration.Bridge.Tests) and in Unity's EditMode runner.
using System.Collections.Generic;
using BattleCore;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class MasteryViewTests
    {
        /// <summary>A book after <c>n</c> training marks on each card id given (js: last-round.test.mjs:20).</summary>
        private static MasteryBook Trained(params (string Id, int Marks)[] marks)
        {
            MasteryBook book = MasteryBook.Begin(null);
            foreach (var (id, n) in marks)
            {
                for (int i = 0; i < n; i++) book = book.Train(id).Book;
            }
            return book;
        }

        [Test]
        public void X15_NameAtRank_TheCataloguesNameAtZero_TheProfilesNameAtEachDan()
        {
            // js: test/ext/last-round.test.mjs:26
            MasteryBook book = MasteryBook.Begin(null);
            Assert.That(MasteryView.NameAtRank(book, "thrust", 0), Is.EqualTo(CardCatalog.ById("thrust").Name));
            Assert.That(MasteryView.NameAtRank(book, "thrust", 1), Is.EqualTo("鋭い突き"));
            Assert.That(MasteryView.NameAtRank(book, "thrust", 2), Is.EqualTo("穿ち突き"));
            Assert.That(MasteryView.NameAtRank(book, "no_such_card", 1), Is.EqualTo("no_such_card"));
        }

        [Test]
        public void X15_NameAtRank_ACardThatNeverGrows_KeepsItsName()
        {
            // js: bridge/exploration/MasteryView.js:13 (no profile: the catalogue's name at every 段)
            MasteryBook book = MasteryBook.Begin(null);
            string still = null;
            foreach (CardDef def in CardCatalog.All)
            {
                if (book.ProfileOf(def.Id) == null)
                {
                    still = def.Id;
                    break;
                }
            }
            Assert.That(still, Is.Not.Null, "some card has no profile");
            Assert.That(MasteryView.NameAtRank(book, still, 2), Is.EqualTo(CardCatalog.ById(still).Name));
            Assert.That(MasteryView.NameAtRank(null, "thrust", 2), Is.EqualTo(CardCatalog.ById("thrust").Name), "no book: no profile to read");
        }

        [Test]
        public void X15_MasteryProgress_TheTicksTowardTheNextDan_AndTheNamesAroundIt()
        {
            // js: test/ext/last-round.test.mjs:34
            IReadOnlyList<int> thresholds = Constants.MasteryThresholds;
            int t1 = thresholds[0];
            int t2 = thresholds[1];
            MasteryProgressInfo fresh = MasteryView.MasteryProgress(MasteryBook.Begin(null), "thrust");
            Assert.That(new object[] { fresh.Rank, fresh.Ticks, fresh.Need, fresh.Grows, fresh.Top, fresh.Capped },
                Is.EqualTo(new object[] { 0, 0, t1, true, false, false }));
            Assert.That(fresh.Name, Is.EqualTo(fresh.BaseName));
            Assert.That(fresh.NextName, Is.EqualTo("鋭い突き"));
            MasteryProgressInfo one = MasteryView.MasteryProgress(Trained(("thrust", t1 + 1)), "thrust");
            Assert.That(new object[] { one.Rank, one.Ticks, one.Need }, Is.EqualTo(new object[] { 1, t1 + 1, t2 }));
            Assert.That(one.Name, Is.EqualTo("鋭い突き"));
            Assert.That(one.BaseName, Is.EqualTo(CardCatalog.ById("thrust").Name));
            Assert.That(one.NextName, Is.EqualTo("穿ち突き"));
        }

        [Test]
        public void X15_MasteryProgress_NoNextDanAtTheTop_NorAtTwoWhileAnotherCardHoldsTheThird()
        {
            // js: test/ext/last-round.test.mjs:47
            int top = Constants.MasteryThresholds[2];
            MasteryBook book = Trained(("thrust", top), ("kesa_cut", top));
            string holder = book.TopRankHolder;
            string other = holder == "thrust" ? "kesa_cut" : "thrust";
            MasteryProgressInfo held = MasteryView.MasteryProgress(book, holder);
            Assert.That(held.Rank, Is.EqualTo(3));
            Assert.That(held.Top, Is.True);
            Assert.That(held.Need, Is.Null);
            Assert.That(held.NextName, Is.Null);
            MasteryProgressInfo stopped = MasteryView.MasteryProgress(book, other);
            Assert.That(stopped.Rank, Is.EqualTo(2));
            Assert.That(stopped.Capped, Is.True);
            Assert.That(stopped.Need, Is.Null);
            Assert.That(stopped.NextName, Is.Null);
        }

        [Test]
        public void X15_MasteryProgress_ACardWithNoProfile_DoesNotGrow()
        {
            // js: bridge/exploration/MasteryView.js:31-37 (Grows false, Need null)
            MasteryBook book = MasteryBook.Begin(null);
            foreach (CardDef def in CardCatalog.All)
            {
                if (book.ProfileOf(def.Id) != null) continue;
                MasteryProgressInfo flat = MasteryView.MasteryProgress(book, def.Id);
                Assert.That(flat.Grows, Is.False);
                Assert.That(flat.Need, Is.Null);
                Assert.That(flat.NextName, Is.Null);
                Assert.That(flat.Capped, Is.False);
                return;
            }
            Assert.Fail("every card has a profile");
        }
    }
}
