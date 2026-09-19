using System;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class DepictionScriptTests
    {
        [Test]
        public void SevenEventsFlowThroughInOrder()
        {
            var runner = new DepictionRunner(TurnSliceScript.Build());

            Assert.That(runner.AdvanceAuto().Order, Is.EqualTo(1));
            Assert.That(runner.Frame.Player.Stamina, Is.EqualTo(9));
            Assert.That(runner.Frame.Hand.Count, Is.EqualTo(5));
            Assert.That(runner.Frame.Omen.KindLabel + runner.Frame.Omen.SideGlyph + runner.Frame.Omen.ValueText, Is.EqualTo("攻撃近13"));

            Play(runner, TurnSliceScript.Kesagiri, DropZone.Receiver, 2);
            Assert.That(runner.Frame.Enemy.Hp, Is.EqualTo(51));
            Assert.That(runner.Frame.Player.Stamina, Is.EqualTo(8));

            Play(runner, TurnSliceScript.Daijodan, DropZone.Receiver, 3);
            Assert.That(runner.Frame.Enemy.Hp, Is.EqualTo(38));
            Assert.That(runner.Frame.Player.Stamina, Is.EqualTo(6));

            Play(runner, TurnSliceScript.Ushirotobi, DropZone.AboveThrowLine, 4);
            Assert.That(runner.Frame.Player.Range, Is.EqualTo(RangeSide.Far));
            Assert.That(runner.Frame.Player.Guard, Is.EqualTo(7));
            Assert.That(runner.Frame.Player.Stamina, Is.EqualTo(5));

            Assert.That(runner.AdvanceAuto().Order, Is.EqualTo(5));
            Assert.That(runner.Frame.Player.Guard, Is.EqualTo(10));
            Assert.That(runner.Frame.Hand, Is.Empty);

            Assert.That(runner.AdvanceAuto().Order, Is.EqualTo(6));
            Assert.That(runner.Frame.Player.Hp, Is.EqualTo(47));
            Assert.That(runner.Frame.Player.Guard, Is.EqualTo(0));

            Assert.That(runner.AdvanceAuto().Order, Is.EqualTo(7));
            Assert.That(runner.Frame.Omen.KindLabel, Is.EqualTo("防御"));
            Assert.That(runner.Finished, Is.True);
        }

        [Test]
        public void DragEventsDoNotAdvanceAutomatically()
        {
            var runner = new DepictionRunner(TurnSliceScript.Build());
            runner.AdvanceAuto();

            Assert.That(runner.WaitingForDrag, Is.True);
            Assert.Throws<InvalidOperationException>(() => runner.AdvanceAuto());
        }

        [Test]
        public void WrongCardReturnsToHand()
        {
            var runner = new DepictionRunner(TurnSliceScript.Build());
            runner.AdvanceAuto();

            PlayVerdict verdict = runner.TryPlay(TurnSliceScript.Daijodan, DropZone.Receiver, out DepictionEvent played);

            Assert.That(verdict, Is.EqualTo(PlayVerdict.WrongCard));
            Assert.That(played, Is.Null);
            Assert.That(runner.Index, Is.EqualTo(1));
        }

        [Test]
        public void SingleTargetCardReleasedOffTheReceiverReturnsToHand()
        {
            var runner = new DepictionRunner(TurnSliceScript.Build());
            runner.AdvanceAuto();

            Assert.That(runner.TryPlay(TurnSliceScript.Kesagiri, DropZone.None, out _), Is.EqualTo(PlayVerdict.WrongZone));
            Assert.That(runner.TryPlay(TurnSliceScript.Kesagiri, DropZone.AboveThrowLine, out _), Is.EqualTo(PlayVerdict.WrongZone));
            Assert.That(runner.Index, Is.EqualTo(1));
        }

        [Test]
        public void SelfCardNeedsTheThrowLine()
        {
            var runner = new DepictionRunner(TurnSliceScript.Build());
            runner.AdvanceAuto();
            Play(runner, TurnSliceScript.Kesagiri, DropZone.Receiver, 2);
            Play(runner, TurnSliceScript.Daijodan, DropZone.Receiver, 3);

            Assert.That(runner.TryPlay(TurnSliceScript.Ushirotobi, DropZone.Receiver, out _), Is.EqualTo(PlayVerdict.WrongZone));
            Assert.That(runner.TryPlay(TurnSliceScript.Ushirotobi, DropZone.AboveThrowLine, out _), Is.EqualTo(PlayVerdict.Accepted));
        }

        [Test]
        public void EveryCueCarriesItsSettledValueSoTheViewNeverComputes()
        {
            DepictionScript script = TurnSliceScript.Build();
            foreach (DepictionEvent ev in script.Events)
            {
                Assert.That(ev.After, Is.Not.Null, "event " + ev.Order);
                foreach (Cue cue in ev.Cues)
                {
                    Assert.That(cue.Intensity, Is.InRange(1, 4), "event " + ev.Order + " " + cue.Kind);
                    if (cue.Kind == CueKind.Hit) Assert.That(cue.HpAfter, Is.Not.EqualTo(Cue.Unchanged));
                    if (cue.Kind == CueKind.GuardGain || cue.Kind == CueKind.GuardBlock || cue.Kind == CueKind.StanceCue)
                        Assert.That(cue.GuardAfter, Is.Not.EqualTo(Cue.Unchanged));
                    if (cue.Kind == CueKind.StaminaChange) Assert.That(cue.StaminaAfter, Is.Not.EqualTo(Cue.Unchanged));
                }
            }
        }

        private static void Play(DepictionRunner runner, string cardId, DropZone zone, int expectedOrder)
        {
            PlayVerdict verdict = runner.TryPlay(cardId, zone, out DepictionEvent played);
            Assert.That(verdict, Is.EqualTo(PlayVerdict.Accepted));
            Assert.That(played.Order, Is.EqualTo(expectedOrder));
        }
    }
}
