// The effects' ids, lengths and switches (Script/Effects.cs, #75). The View animates for the
// catalog's lengths and asks the switches whether to animate at all, so what is held here is what
// the screen plays. Seeing the effects themselves is a capture in the Editor (visual-inspect).
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class EffectTests
    {
        // ---- The catalog ----

        [Test]
        public void EveryEffect_HasALength()
        {
            foreach (EffectId id in Enum.GetValues(typeof(EffectId)))
            {
                EffectSpec spec = EffectCatalog.Of(id);
                Assert.That(spec.Id, Is.EqualTo(id));
                Assert.That(spec.Ms, Is.GreaterThan(0f), id + " plays for some time when it is on");
                Assert.That(spec.StaggerMs, Is.GreaterThanOrEqualTo(0f), id.ToString());
                Assert.That(spec.Source, Is.Not.Empty, id + " says where its length comes from");
            }
            Assert.That(EffectCatalog.All.Count, Is.EqualTo(Enum.GetValues(typeof(EffectId)).Length));
        }

        // battle_ui_ux_v2 §3.2 unless the issue chose the length (#75).
        [TestCase(EffectId.CardDraw, 260f, 60f, true)]
        [TestCase(EffectId.HandFan, 120f, 0f, false)]
        [TestCase(EffectId.CardHover, 90f, 0f, false)]
        [TestCase(EffectId.CardGrab, 90f, 0f, false)]
        [TestCase(EffectId.ReceiverShow, 150f, 0f, false)]
        [TestCase(EffectId.ThrowLineShow, 150f, 0f, false)]
        [TestCase(EffectId.ReceiverSnap, 80f, 0f, false)]
        [TestCase(EffectId.CardRelease, 160f, 0f, true)]
        [TestCase(EffectId.CardToDiscard, 200f, 0f, false)]
        [TestCase(EffectId.CardReturn, 200f, 0f, false)]
        [TestCase(EffectId.RefusalShake, 160f, 0f, false)]
        [TestCase(EffectId.HandDiscard, 240f, 60f, true)]
        [TestCase(EffectId.UnpayableDim, 120f, 0f, false)]
        public void TheCardMotion_PlaysForTheLengthsTheCanonWrites(EffectId id, float ms, float stagger, bool blocking)
        {
            EffectSpec spec = EffectCatalog.Of(id);
            Assert.That(spec.Ms, Is.EqualTo(ms));
            Assert.That(spec.StaggerMs, Is.EqualTo(stagger));
            Assert.That(spec.Blocking, Is.EqualTo(blocking));
        }

        [Test]
        public void APerCardEffect_StartsEachNextCardOneStaggerLater()
        {
            EffectSpec draw = EffectCatalog.Of(EffectId.CardDraw);
            Assert.That(draw.TotalMs(5), Is.EqualTo(260f + 4 * 60f), "five cards: the last leaves after four staggers");
            Assert.That(draw.TotalMs(1), Is.EqualTo(260f));
            Assert.That(draw.TotalMs(0), Is.EqualTo(0f), "nothing drawn, nothing waited for");
            Assert.That(EffectCatalog.Of(EffectId.HandDiscard).TotalMs(8), Is.EqualTo(240f + 7 * 60f));
        }

        // ---- Switches ----

        [Test]
        public void EveryEffect_IsOnUntilSwitchedOff_AndOffWaitsForNothing()
        {
            var switches = EffectSwitches.AllOn();
            foreach (EffectId id in Enum.GetValues(typeof(EffectId)))
            {
                Assert.That(switches.IsOn(id), Is.True, id.ToString());
                Assert.That(switches.WaitMs(id, 3), Is.EqualTo(EffectCatalog.Of(id).TotalMs(3)), id.ToString());

                switches.Set(id, false);
                Assert.That(switches.IsOn(id), Is.False, id.ToString());
                Assert.That(switches.Ms(id), Is.EqualTo(0f), id + ": off animates for 0 ms");
                Assert.That(switches.StaggerMs(id), Is.EqualTo(0f), id.ToString());
                Assert.That(switches.WaitMs(id, 3), Is.EqualTo(0f), id + ": off holds nothing up");

                switches.Set(id, true);
                Assert.That(switches.IsOn(id), Is.True, id + " can be switched back on");
            }
        }

        [Test]
        public void TheInspectorList_SwitchesOffByName_AndHandsBackWhatItDoesNotKnow()
        {
            var switches = EffectSwitches.WithOff(new[] { "CardDraw", " HandDiscard ", "", "CardDrw", "7", null }, out List<string> unknown);

            Assert.That(switches.IsOn(EffectId.CardDraw), Is.False);
            Assert.That(switches.IsOn(EffectId.HandDiscard), Is.False, "surrounding spaces are forgiven");
            Assert.That(switches.IsOn(EffectId.CardRelease), Is.True, "\"7\" is not a name, so nothing at 7 is switched off");
            Assert.That(unknown, Is.EqualTo(new[] { "CardDrw", "7" }));

            Assert.That(EffectSwitches.WithOff(null, out List<string> none).IsOn(EffectId.CardDraw), Is.True);
            Assert.That(none, Is.Empty);
        }

        // ---- What an event waits for ----

        [Test]
        public void TheFixedSlice_WaitsForTheDrawsTheReleasesAndTheDiscard()
        {
            List<DepictionEvent> events = TurnSliceScript.Build().Events;
            var all = EffectSwitches.AllOn();

            DepictionEvent turnStart = events[0];
            Assert.That(Steps(turnStart), Is.EqualTo(new[] { (EffectId.CardDraw, 5) }));
            Assert.That(EffectPlan.BlockingMs(turnStart, all), Is.EqualTo(500f));

            foreach (DepictionEvent play in events.Where(e => e.Kind == DepictionEventKind.PlayCard))
            {
                Assert.That(Steps(play), Is.EqualTo(new[] { (EffectId.CardRelease, 1) }), "event " + play.Order);
                Assert.That(EffectPlan.BlockingMs(play, all), Is.EqualTo(160f));
            }

            DepictionEvent turnEnd = events.Single(e => e.Kind == DepictionEventKind.TurnEnd);
            Assert.That(Steps(turnEnd), Is.EqualTo(new[] { (EffectId.HandDiscard, 2) }));
            Assert.That(EffectPlan.BlockingMs(turnEnd, all), Is.EqualTo(300f));
        }

        [Test]
        public void SwitchingAnyEffectOff_NeverLengthensAnEvent_AndAllOff_TheTurnStillPlaysToTheEnd()
        {
            List<DepictionEvent> events = LiveTurnEvents();
            Assert.That(events.Count, Is.GreaterThan(3));

            foreach (EffectId id in Enum.GetValues(typeof(EffectId)))
            {
                var oneOff = EffectSwitches.AllOn();
                oneOff.Set(id, false);
                foreach (DepictionEvent ev in events)
                {
                    float on = EffectPlan.BlockingMs(ev, EffectSwitches.AllOn());
                    float off = EffectPlan.BlockingMs(ev, oneOff);
                    Assert.That(off, Is.InRange(0f, on), id + " off, event " + ev.Order);
                }
            }

            // With every effect off nothing is waited for, and every event still hands over its settled
            // frame: that frame is what the View shows, so the turn goes on to its end.
            var allOff = EffectSwitches.AllOff();
            foreach (DepictionEvent ev in events)
            {
                Assert.That(EffectPlan.BlockingMs(ev, allOff), Is.EqualTo(0f), "event " + ev.Order);
                Assert.That(ev.After, Is.Not.Null, "event " + ev.Order);
            }
        }

        [Test]
        public void TheCardMotion_OfEveryEvent_FitsInsideTheTwoSecondBudget()
        {
            var all = EffectSwitches.AllOn();
            foreach (DepictionEvent ev in TurnSliceScript.Build().Events.Concat(LiveTurnEvents()))
            {
                Assert.That(EffectPlan.BlockingMs(ev, all), Is.LessThan(2000f), "event " + ev.Order + " " + ev.Title);
            }
        }

        // ---- The trace #78 reads ----

        [Test]
        public void TheTrace_TimesWhatPlayed_AndMarksWhatWasSwitchedOff()
        {
            double now = 10.0;
            var trace = new EffectTrace(() => now);

            int draw = trace.Begin(EffectId.CardDraw, eventOrder: 1, count: 5);
            now += 0.52;
            trace.End(draw);
            trace.End(draw); // a second End changes nothing
            trace.Skip(EffectId.CardRelease, eventOrder: 2);
            int open = trace.Begin(EffectId.HandFan, eventOrder: 2);

            Assert.That(trace.Entries, Has.Count.EqualTo(3));
            EffectTrace.Entry first = trace.Entries[0];
            Assert.That(first.Id, Is.EqualTo(EffectId.CardDraw));
            Assert.That(first.EventOrder, Is.EqualTo(1));
            Assert.That(first.Seconds, Is.EqualTo(0.52).Within(1e-9));
            Assert.That(first.NominalMs, Is.EqualTo(500f));
            Assert.That(first.Finished, Is.True);
            Assert.That(trace.Entries[1].Skipped, Is.True);
            Assert.That(trace.Entries[1].Seconds, Is.EqualTo(0.0));
            Assert.That(trace.Entries[open].Finished, Is.False, "still playing");

            string[] lines = trace.ToCsv().TrimEnd('\n').Split('\n');
            Assert.That(lines[0], Is.EqualTo("effect,event,start_s,measured_ms,nominal_ms,skipped"));
            Assert.That(lines[1], Is.EqualTo("CardDraw,1,10.000,520,500,0"));
            Assert.That(lines[2], Does.StartWith("CardRelease,2,").And.EndsWith(",1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => trace.End(9));
        }

        // ---- Helpers ----

        private static (EffectId, int)[] Steps(DepictionEvent ev) =>
            EffectPlan.StepsOf(ev).Select(s => (s.Id, s.Count)).ToArray();

        /// <summary>One live turn with nobody at the mouse: the turn start, the turn end, the enemy.</summary>
        private static List<DepictionEvent> LiveTurnEvents()
        {
            var live = new LiveTurn();
            var events = new List<DepictionEvent>();
            while (!live.Finished && !live.WaitingForPlayer) events.Add(live.AdvanceAuto());
            string first = live.Frame.Hand.Select(c => c.Id).FirstOrDefault(id => live.Inspect(id) == PlayVerdict.Accepted);
            if (first != null)
            {
                CardFace face = DepictionText.Find(live.Frame.Hand, first);
                Assert.That(live.TryPlay(first, DepictionText.RequiredZone(face.Aim), out DepictionEvent played), Is.EqualTo(PlayVerdict.Accepted));
                events.Add(played);
            }
            events.Add(live.EndTurn());
            while (!live.Finished && !live.WaitingForPlayer) events.Add(live.AdvanceAuto());
            return events;
        }
    }
}
