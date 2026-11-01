using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Trial.Async.Tests
{
    public class PromiseTests
    {
        private List<Exception> _unhandled;

        [SetUp]
        public void SetUp()
        {
            Microtasks.Reset();
            _unhandled = new List<Exception>();
            Microtasks.Unhandled = e => _unhandled.Add(e);
        }

        [Test]
        public void AsyncMethod_RunsSynchronouslyUntilItsFirstAwait()
        {
            var log = new List<string>();
            var gate = new PromiseSource();
            async Promise Flow()
            {
                log.Add("a");
                await gate.Promise;
                log.Add("b");
            }

            Promise p = Flow();
            log.Add("after call");
            Assert.That(log, Is.EqualTo(new[] { "a", "after call" }));
            gate.Resolve();
            Assert.That(log.Count, Is.EqualTo(2), "a continuation never runs inside the call that settles");
            Microtasks.Drain();
            Assert.That(log, Is.EqualTo(new[] { "a", "after call", "b" }));
            Assert.That(p.IsResolved, Is.True);
        }

        [Test]
        public void AwaitOfASettledPromise_StillYieldsToTheQueue()
        {
            var log = new List<string>();
            async Promise Flow()
            {
                log.Add("before");
                await Promise.Resolved();
                log.Add("after");
            }

            Flow();
            log.Add("sync end");
            Microtasks.Drain();
            Assert.That(log, Is.EqualTo(new[] { "before", "sync end", "after" }));
        }

        [Test]
        public void APromise_CanBeAwaitedBySeveralFlows_BeforeAndAfterItSettles()
        {
            var src = new PromiseSource();
            int count = 0;
            async Promise Waiter() { await src.Promise; count++; await src.Promise; count++; }

            Waiter();
            Waiter();
            src.Resolve();
            Microtasks.Drain();
            Assert.That(count, Is.EqualTo(4));
        }

        [Test]
        public void ContinuationsRun_InTheOrderTheirPromisesSettled()
        {
            var a = new PromiseSource();
            var b = new PromiseSource();
            var log = new List<string>();
            async Promise On(Promise p, string name) { await p; log.Add(name); }

            On(a.Promise, "a");
            On(b.Promise, "b");
            b.Resolve();
            a.Resolve();
            Microtasks.Drain();
            Assert.That(log, Is.EqualTo(new[] { "b", "a" }));
        }

        [Test]
        public void ValuePromise_GivesItsValue()
        {
            var src = new PromiseSource<int>();
            int got = 0;
            async Promise<int> Twice() => 2 * await src.Promise;
            async Promise Read() { got = await Twice(); }

            Read();
            src.Resolve(21);
            Microtasks.Drain();
            Assert.That(got, Is.EqualTo(42));
        }

        [Test]
        public void Rejection_RethrowsAtAwait_AndIsNotReportedWhenObserved()
        {
            Exception caught = null;
            async Promise Fails() { await Promise.Resolved(); throw new InvalidOperationException("boom"); }
            async Promise Catches()
            {
                try { await Fails(); }
                catch (InvalidOperationException e) { caught = e; }
            }

            Catches();
            Microtasks.Drain();
            Assert.That(caught, Is.Not.Null);
            Assert.That(caught.Message, Is.EqualTo("boom"));
            Assert.That(_unhandled, Is.Empty);
        }

        [Test]
        public void AnUnobservedRejection_GoesToUnhandled()
        {
            async Promise Fails() { await Promise.Resolved(); throw new InvalidOperationException("lost"); }

            Fails();
            Microtasks.Drain();
            Assert.That(_unhandled.Count, Is.EqualTo(1));
            Assert.That(_unhandled[0].Message, Is.EqualTo("lost"));
        }

        [Test]
        public void All_WaitsForEvery_AndRace_ForTheFirst()
        {
            var a = new PromiseSource();
            var b = new PromiseSource();
            Promise all = Promise.All(a.Promise, b.Promise, null);
            Promise race = Promise.Race(a.Promise, b.Promise);
            b.Resolve();
            Microtasks.Drain();
            Assert.That(all.IsPending, Is.True);
            Assert.That(race.IsResolved, Is.True);
            a.Resolve();
            Microtasks.Drain();
            Assert.That(all.IsResolved, Is.True);
            Assert.That(Promise.All().IsResolved, Is.True);
        }

        [Test]
        public void Then_RunsAfterTheResolution_AndPassesARejectionThrough()
        {
            var src = new PromiseSource();
            var log = new List<string>();
            Promise p = src.Promise.Then(() => log.Add("then"));
            src.Resolve();
            Assert.That(log, Is.Empty);
            Microtasks.Drain();
            Assert.That(log, Is.EqualTo(new[] { "then" }));
            Assert.That(p.IsResolved, Is.True);

            Promise r = Promise.Rejected(new Exception("x")).Then(() => log.Add("never"));
            Exception handled = null;
            r.Catch(e => handled = e);
            Microtasks.Drain();
            Assert.That(handled?.Message, Is.EqualTo("x"));
            Assert.That(log, Is.EqualTo(new[] { "then" }));
        }

        [Test]
        public void Safe_ReportsAThrowAndResolves()
        {
            Promise p = Promise.Safe(() => throw new Exception("sync"));
            Promise q = Promise.Safe(() => Promise.Rejected(new Exception("async")));
            Microtasks.Drain();
            Assert.That(p.IsResolved && q.IsResolved, Is.True);
            Assert.That(_unhandled.ConvertAll(e => e.Message), Is.EquivalentTo(new[] { "sync", "async" }));
        }

        [Test]
        public void ANeverSettlingPromise_NeverContinues()
        {
            bool ran = false;
            async Promise Flow() { await Promise.Never(); ran = true; }
            Flow();
            Microtasks.Drain();
            Assert.That(ran, Is.False);
        }
    }
}
