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
                    if (cue.Kind == CueKind.StaminaChange)
                    {
                        Assert.That(cue.StaminaAfter, Is.Not.EqualTo(Cue.Unchanged));
                        Assert.That(cue.StaminaMax, Is.Not.EqualTo(Cue.Unchanged));
                    }
                }
            }
        }

        [Test]
        public void EverySlashNamesItsAttacker()
        {
            int slashes = 0;
            foreach (DepictionEvent ev in TurnSliceScript.Build().Events)
            {
                foreach (Cue cue in ev.Cues)
                {
                    if (cue.Kind != CueKind.Slash) continue;
                    slashes += 1;
                    Assert.That(cue.Source.HasValue, Is.True, "event " + ev.Order + " slash has no Source");
                    Assert.That(cue.Source.Value, Is.Not.EqualTo(cue.Target), "event " + ev.Order);
                }
            }
            Assert.That(slashes, Is.EqualTo(3));
        }

        [Test]
        public void EveryUnitWithARangeSideCarriesItsGlyph()
        {
            DepictionScript script = TurnSliceScript.Build();
            AssertRangeGlyphs(script.Opening, "opening");
            foreach (DepictionEvent ev in script.Events)
            {
                AssertRangeGlyphs(ev.After, "event " + ev.Order);
                foreach (Cue cue in ev.Cues)
                {
                    if (cue.Kind == CueKind.RangeSwitch) Assert.That(cue.RangeGlyphAfter, Is.Not.Empty, "event " + ev.Order);
                }
            }

            Assert.That(script.Opening.Player.RangeGlyph, Is.EqualTo("近"));
            Cue rangeSwitch = script.Events[3].Cues.Find(c => c.Kind == CueKind.RangeSwitch);
            Assert.That(rangeSwitch, Is.Not.Null, "event 4 has no RangeSwitch cue");
            Assert.That(rangeSwitch.RangeAfter, Is.EqualTo(RangeSide.Far));
            Assert.That(rangeSwitch.RangeGlyphAfter, Is.EqualTo("遠"));
            Assert.That(script.Events[3].After.Player.RangeGlyph, Is.EqualTo("遠"));
        }

        [Test]
        public void EveryThrowLineCardSaysWhoseFiguresItLandsOn()
        {
            DepictionScript script = TurnSliceScript.Build();
            int throwLineCards = AssertAffects(script.Opening, "opening");
            foreach (DepictionEvent ev in script.Events)
            {
                throwLineCards += AssertAffects(ev.After, "event " + ev.Order);
            }
            Assert.That(throwLineCards, Is.GreaterThan(0));
        }

        [Test]
        public void EveryHandCardPrintsItsTypeAndWhatItDoes()
        {
            DepictionScript script = TurnSliceScript.Build();
            var frames = new System.Collections.Generic.List<DepictionFrame> { script.Opening };
            foreach (DepictionEvent ev in script.Events) frames.Add(ev.After);
            int faces = 0;
            foreach (DepictionFrame frame in frames)
            {
                foreach (CardFace face in frame.Hand)
                {
                    faces += 1;
                    Assert.That(face.TypeLabel, Is.Not.Empty, face.Id + " has no TypeLabel");
                    Assert.That(face.Description, Is.Not.Empty, face.Id + " has no Description");
                    // The card prints one sentence per line in a box three lines tall and about nine
                    // full-width characters wide (half-width characters count as half).
                    string[] sentences = face.Description.TrimEnd('。').Split('。');
                    Assert.That(sentences.Length, Is.LessThanOrEqualTo(3), face.Id + " has more than three sentences");
                    foreach (string sentence in sentences)
                    {
                        Assert.That(Width(sentence + "。"), Is.LessThanOrEqualTo(9.5), face.Id + ": " + sentence);
                    }
                    if (!string.IsNullOrEmpty(face.ValueText))
                        Assert.That(face.Description, Does.Contain(face.ValueText), face.Id + " describes a number other than its value");
                }
            }
            Assert.That(faces, Is.GreaterThan(0));
        }

        private static double Width(string text)
        {
            double width = 0;
            foreach (char c in text) width += c < 0x2000 ? 0.5 : 1.0;
            return width;
        }

        /// <returns>How many throw-line cards the frame's hand holds.</returns>
        private static int AssertAffects(DepictionFrame frame, string where)
        {
            int throwLineCards = 0;
            foreach (CardFace face in frame.Hand)
            {
                if (face.Aim != CardAim.Self) continue;
                throwLineCards += 1;
                Assert.That(face.Affects.HasValue, Is.True, where + " " + face.Id + " has no Affects");
            }
            return throwLineCards;
        }

        private static void AssertRangeGlyphs(DepictionFrame frame, string where)
        {
            foreach (UnitFrame unit in new[] { frame.Player, frame.Enemy })
            {
                if (!unit.HasRange) continue;
                // The glyph and the side are written separately; they must never disagree.
                Assert.That(unit.RangeGlyph, Is.EqualTo(unit.Range == RangeSide.Near ? "近" : "遠"), where);
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
