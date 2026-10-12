// #501: two pieces of battle_ui_ux_v2 the screen did not play yet. §5.11 順 2: the body keeps a light
// (StanceGlow, α 0.25) for as long as its stance list holds a stance. §5.13 順 1: at turn end with 3 or
// more stamina left, pips 1-3 glow for 250 ms before the shield and "+3" (EffectId.ReservePips), and the
// wait is one of the plan's steps, so the 2.0 s the tests hold is the wait the screen holds. Runs under
// `dotnet test` (Depiction.Script.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class StanceGlowTests
    {
        private static UnitFrame Holding(params string[] placed) => new UnitFrame { Stances = StanceList.Rows(placed) };

        // ---- §5.11 順 2: the light kept while a stance is held ----

        [Test]
        public void TheLight_IsOffWhileTheListIsEmpty()
        {
            Assert.That(StanceGlow.IsOn(new UnitFrame()), Is.False);
            Assert.That(StanceGlow.IsOn(Holding()), Is.False);
            Assert.That(StanceGlow.IsOn(new UnitFrame { Stances = null }), Is.False, "a source with no list draws no light");
            Assert.That(StanceGlow.IsOn(null), Is.False);
        }

        [Test]
        public void TheLight_IsOnWhileTheListHoldsAStance()
        {
            Assert.That(StanceGlow.IsOn(Holding("岩の構え")), Is.True);
            Assert.That(StanceGlow.IsOn(Holding("岩の構え", "水の構え", "岩の構え")), Is.True);
        }

        [Test]
        public void TheLight_ComesOnWithTheFirstStance_StaysUntilTheListEmpties_AndGoesOutThen()
        {
            // The frames a unit goes through: nothing, a stance put down, a second, a repeat, a turn
            // later, and the list emptied (a new battle's opening frame).
            var frames = new List<(UnitFrame Frame, StanceGlowChange Expected, bool OnAfter)>
            {
                (Holding(), StanceGlowChange.Keep, false),
                (Holding("岩の構え"), StanceGlowChange.Light, true),
                (Holding("岩の構え", "水の構え"), StanceGlowChange.Keep, true),
                (Holding("岩の構え", "水の構え", "岩の構え"), StanceGlowChange.Keep, true),
                (Holding("岩の構え", "水の構え", "岩の構え"), StanceGlowChange.Keep, true),
                (Holding(), StanceGlowChange.Fade, false),
                (Holding(), StanceGlowChange.Keep, false),
            };
            bool shown = false;
            for (int i = 0; i < frames.Count; i++)
            {
                StanceGlowChange change = StanceGlow.Change(shown, frames[i].Frame);
                Assert.That(change, Is.EqualTo(frames[i].Expected), "frame " + i);
                if (change != StanceGlowChange.Keep) shown = change == StanceGlowChange.Light;
                Assert.That(shown, Is.EqualTo(frames[i].OnAfter), "frame " + i);
                Assert.That(shown, Is.EqualTo(StanceGlow.IsOn(frames[i].Frame)), "the light follows the list, frame " + i);
            }
        }

        [Test]
        public void ASecondStance_AddsNoSecondLight()
        {
            // §5.11 順 2: 2 つ目からは光を重ねず、紋だけを出し直す.
            Assert.That(StanceGlow.Change(true, Holding("岩の構え", "水の構え")), Is.EqualTo(StanceGlowChange.Keep));
        }

        [Test]
        public void TheLight_IsAQuarterStrong_AndFadesBesideTheFlowIn300Ms()
        {
            Assert.That(StanceGlow.Alpha, Is.EqualTo(0.25f));
            EffectSpec spec = EffectCatalog.Of(EffectId.StanceGlow);
            Assert.That(spec.Ms, Is.EqualTo(300f), "§5.11 順 2 300 out");
            Assert.That(spec.Blocking, Is.False, "the light never lengthens an event");
        }

        // ---- §5.13 順 1: 構え's pips ----

        private static DepictionEvent TurnEndWithReserve() => new DepictionEvent
        {
            Kind = DepictionEventKind.TurnEnd,
            After = new DepictionFrame(),
            Cues =
            {
                new Cue { Kind = CueKind.StanceCue, Target = UnitSide.Player, Amount = 3, Text = "+3", GuardAfter = 3 },
                new Cue { Kind = CueKind.DiscardHand, Amount = 5 },
            },
        };

        [Test]
        public void TheReserveBeat_GlowsThePipsFirst_ThenTheShieldAndThePlus3()
        {
            var steps = EffectPlan.StepsOf(TurnEndWithReserve()).Select(s => s.Id).ToList();
            Assert.That(steps, Has.Member(EffectId.ReservePips));
            int pips = steps.IndexOf(EffectId.ReservePips);
            Assert.That(steps[pips + 1], Is.EqualTo(EffectId.StanceCue), "順 1 the pips, then 順 2 the shield and +3");
        }

        [Test]
        public void ThePipGlow_IsWaitedFor_250Ms()
        {
            EffectSpec spec = EffectCatalog.Of(EffectId.ReservePips);
            Assert.That(spec.Ms, Is.EqualTo(250f), "§5.13 順 1 250");
            Assert.That(spec.StaggerMs, Is.EqualTo(0f), "the three pips glow together");
            Assert.That(spec.Blocking, Is.True);
            var on = EffectSwitches.AllOn();
            var pipsOff = EffectSwitches.AllOn();
            pipsOff.Set(EffectId.ReservePips, false);
            Assert.That(EffectPlan.BlockingMs(TurnEndWithReserve(), on) - EffectPlan.BlockingMs(TurnEndWithReserve(), pipsOff), Is.EqualTo(250f));
        }

        [Test]
        public void NoReserve_NoPipGlow()
        {
            // §5.13: under 3 left there is no 構え cue, so no glow and no wait.
            var ev = new DepictionEvent { Kind = DepictionEventKind.TurnEnd, After = new DepictionFrame(), Cues = { new Cue { Kind = CueKind.DiscardHand, Amount = 5 } } };
            Assert.That(EffectPlan.StepsOf(ev).Select(s => s.Id), Has.No.Member(EffectId.ReservePips));
        }

        [Test]
        public void TheTurnEnd_WithThePipGlowAndAFullHandThrownAway_StillFitsTwoSeconds()
        {
            float ms = EffectPlan.BlockingMs(TurnEndWithReserve(), EffectSwitches.AllOn());
            // 250 (pips) + 460 (shield, +3) + 240 + 4 × 60 (five cards thrown away) + 400 (the enemy's banner).
            Assert.That(ms, Is.EqualTo(250f + 460f + 480f + 400f));
            Assert.That(ms, Is.LessThan(BattleSpeed.EventCapMs));

            DepictionEvent sliceEnd = TurnSliceScript.Build().Events.Single(e => e.Kind == DepictionEventKind.TurnEnd);
            Assert.That(EffectPlan.StepsOf(sliceEnd).Select(s => s.Id), Has.Member(EffectId.ReservePips), "the fixed slice keeps 3 and glows");
            Assert.That(EffectPlan.BlockingMs(sliceEnd, EffectSwitches.AllOn()), Is.LessThan(BattleSpeed.EventCapMs));
        }
    }
}
