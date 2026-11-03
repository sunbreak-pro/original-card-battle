// The floor's stripes in time (battle_ui_ux_v2 §5.8, battle-visual-v1 の色の表, #766): on the omen's drop
// the aimed cells are drawn in from left to right, each one 30 ms after the cell before it, and after the
// enemy's action they are gone in 120 ms. Script/StripeSweep.cs says which cells, EffectPlan when. Runs
// under `dotnet test` (Depiction.Script.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class StripeSweepTests
    {
        private static DepictionFrame One(int[] aim, bool visible = true, int cells = 8)
        {
            var enemy = new EnemyFrame
            {
                AimCells = new List<int>(aim),
                Omen = new OmenFrame { Visible = visible, Disclosure = OmenDisclosure.Full },
            };
            return new DepictionFrame
            {
                Enemies = new List<EnemyFrame> { enemy },
                Enemy = enemy.Unit,
                Omen = enemy.Omen,
                Floor = new FloorFrame { Cells = cells, AimCells = enemy.AimCells },
            };
        }

        private static EnemyFrame Aiming(int direction, int[] aim, bool visible = true)
        {
            return new EnemyFrame
            {
                StripeDirection = direction,
                Swatch = true,
                AimCells = new List<int>(aim),
                Omen = new OmenFrame { Visible = visible, Disclosure = OmenDisclosure.Full },
            };
        }

        private static DepictionFrame Several(params EnemyFrame[] enemies)
        {
            return new DepictionFrame
            {
                Enemies = new List<EnemyFrame>(enemies),
                Enemy = enemies[0].Unit,
                Omen = enemies[0].Omen,
                Floor = new FloorFrame { Cells = 8 },
            };
        }

        // ---- when ----

        [Test]
        public void EachCellStarts30MsAfterTheCellBeforeIt()
        {
            var on = EffectSwitches.AllOn();
            float[] starts = Enumerable.Range(0, 5).Select(order => EffectPlan.StripeDrawStartMs(order, on)).ToArray();
            Assert.That(starts, Is.EqualTo(new[] { 0f, 30f, 60f, 90f, 120f }));
            for (int k = 1; k < starts.Length; k++) Assert.That(starts[k] - starts[k - 1], Is.EqualTo(30f), "§5.8 1 マス 30 ずつ");
        }

        [Test]
        public void TheLastCellIsDrawnByThe30MsAfterItsStart()
        {
            var on = EffectSwitches.AllOn();
            Assert.That(EffectPlan.StripeDrawMs(4, on), Is.EqualTo(EffectPlan.StripeDrawStartMs(3, on) + EffectCatalog.Of(EffectId.StripeDraw).Ms));
            Assert.That(EffectPlan.StripeDrawMs(4, on), Is.EqualTo(120f));
            Assert.That(EffectPlan.StripeDrawMs(0, on), Is.EqualTo(0f), "no cell aimed, nothing drawn");
        }

        [Test]
        public void TheStripesAreGone120MsAfterTheAction()
        {
            Assert.That(EffectPlan.StripeFadeMs(EffectSwitches.AllOn()), Is.EqualTo(120f));
        }

        [Test]
        public void NeitherHoldsTheFlowUp()
        {
            Assert.That(EffectCatalog.Of(EffectId.StripeDraw).Blocking, Is.False, "it runs beside the omen's drop");
            Assert.That(EffectCatalog.Of(EffectId.StripeFade).Blocking, Is.False);
            var ev = new DepictionEvent { Kind = DepictionEventKind.EnemyAction, Cues = new List<Cue> { new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy } } };
            Assert.That(EffectPlan.StepsOf(ev).Select(s => s.Id), Has.None.EqualTo(EffectId.StripeDraw).And.None.EqualTo(EffectId.StripeFade));
        }

        [Test]
        public void SwitchedOff_TheStripesSettleAtOnce()
        {
            var off = EffectSwitches.AllOn();
            off.Set(EffectId.StripeDraw, false);
            off.Set(EffectId.StripeFade, false);
            Assert.That(EffectPlan.StripeDrawStartMs(3, off), Is.EqualTo(0f));
            Assert.That(EffectPlan.StripeDrawMs(4, off), Is.EqualTo(0f));
            Assert.That(EffectPlan.StripeFadeMs(off), Is.EqualTo(0f));
        }

        [Test]
        public void ANegativeOrder_StartsWithTheFirstCell()
        {
            Assert.That(EffectPlan.StripeDrawStartMs(-1, EffectSwitches.AllOn()), Is.EqualTo(0f));
        }

        [Test]
        public void EachCell_IsDrawnOverIts30Ms_AfterItsStart()
        {
            var on = EffectSwitches.AllOn();
            Assert.That(EffectPlan.StripeDrawShare(1, 29f, on), Is.EqualTo(0f), "the second cell waits for the first");
            Assert.That(EffectPlan.StripeDrawShare(1, 45f, on), Is.EqualTo(0.5f));
            Assert.That(EffectPlan.StripeDrawShare(1, 60f, on), Is.EqualTo(1f));
            Assert.That(EffectPlan.StripeDrawShare(0, 500f, on), Is.EqualTo(1f));
            var off = EffectSwitches.AllOn();
            off.Set(EffectId.StripeDraw, false);
            Assert.That(EffectPlan.StripeDrawShare(4, 0f, off), Is.EqualTo(1f), "off settles at once");
        }

        // ---- which cells ----

        [Test]
        public void AShownOmen_DrawsItsCellsInFromTheLeft()
        {
            DepictionFrame before = One(new[] { 3, 4, 5 }, visible: false);
            DepictionFrame after = One(new[] { 5, 3, 4 });
            Assert.That(StripeSweep.DrawnIn(before, after), Is.EqualTo(new[] { 3, 4, 5 }), "left to right, whatever order the aim lists");
            Assert.That(StripeSweep.FadedOut(before, after), Is.Empty);
        }

        [Test]
        public void ASpentOmen_FadesAllItsCells()
        {
            DepictionFrame before = One(new[] { 2, 3 });
            DepictionFrame after = One(new[] { 2, 3 }, visible: false);
            Assert.That(StripeSweep.FadedOut(before, after), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(StripeSweep.DrawnIn(before, after), Is.Empty);
        }

        [Test]
        public void AnAimThatOnlyShifts_KeepsTheCellsItStillHolds()
        {
            // §5.8 敵が動いた / 押された: the cells it keeps are not drawn again.
            DepictionFrame before = One(new[] { 3, 4 });
            DepictionFrame after = One(new[] { 4, 5 });
            Assert.That(StripeSweep.DrawnIn(before, after), Is.EqualTo(new[] { 5 }));
            Assert.That(StripeSweep.FadedOut(before, after), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void TheSameFrame_SweepsNothing()
        {
            DepictionFrame frame = One(new[] { 3, 4 });
            Assert.That(StripeSweep.DrawnIn(frame, frame), Is.Empty);
            Assert.That(StripeSweep.FadedOut(frame, frame), Is.Empty);
        }

        [Test]
        public void TheFirstFrame_OrANewFloor_SettlesAtOnce()
        {
            DepictionFrame after = One(new[] { 3, 4 });
            Assert.That(StripeSweep.DrawnIn((DepictionFrame)null, after), Is.Empty);
            Assert.That(StripeSweep.FadedOut((DepictionFrame)null, after), Is.Empty);
            DepictionFrame otherFloor = One(new[] { 1 }, visible: false, cells: 6);
            Assert.That(StripeSweep.DrawnIn(otherFloor, after), Is.Empty);
        }

        [Test]
        public void TwoEnemies_ACellOneOfThemStillAims_IsNotSwept()
        {
            // The first enemy's omen is spent; the second's still stands on cell 4.
            DepictionFrame before = Several(Aiming(0, new[] { 3, 4 }), Aiming(1, new[] { 4, 5 }));
            DepictionFrame after = Several(Aiming(0, new[] { 3, 4 }, visible: false), Aiming(1, new[] { 4, 5 }));
            Assert.That(StripeSweep.FadedOut(before, after), Is.EqualTo(new[] { 3 }));
            Assert.That(StripeSweep.DrawnIn(before, after), Is.Empty);
        }

        [Test]
        public void AnOmenShownInTheEventThatSpendsTheOld_DrawsOnlyNewCells_AndTheOldOnesStillFade()
        {
            // The screen asks with what its cells hold: 2 and 3 from the old omen.
            var holding = new List<bool> { false, true, true, false, false, false, false, false };
            DepictionFrame after = One(new[] { 3, 4 });
            List<int> drawn = StripeSweep.DrawnIn(holding, after);
            Assert.That(drawn, Is.EqualTo(new[] { 4 }), "cell 3 keeps its stripes");
            foreach (int cell in drawn) holding[cell - 1] = true; // what the draw-in leaves the cells holding
            Assert.That(StripeSweep.FadedOut(holding, after), Is.EqualTo(new[] { 2 }), "at the event's end the old omen's own cell fades");
        }

        [Test]
        public void Holding_ReadsEachCellOfTheFloor()
        {
            Assert.That(StripeSweep.Holding(One(new[] { 2, 4 }, cells: 5)), Is.EqualTo(new[] { false, true, false, true, false }));
            Assert.That(StripeSweep.Holding(One(new[] { 2, 4 }, visible: false, cells: 5)), Is.EqualTo(new[] { false, false, false, false, false }));
            Assert.That(StripeSweep.DrawnIn(new List<bool> { false, false }, One(new[] { 1 })), Is.Empty, "a list of another length is another floor");
        }

        [Test]
        public void TwoEnemies_TheNewOmensCells_AreDrawnInLeftToRight()
        {
            DepictionFrame before = Several(Aiming(0, new[] { 6 }), Aiming(1, new[] { 2 }, visible: false));
            DepictionFrame after = Several(Aiming(0, new[] { 6 }), Aiming(1, new[] { 2, 3, 6 }));
            Assert.That(StripeSweep.DrawnIn(before, after), Is.EqualTo(new[] { 2, 3 }), "cell 6 keeps its stripes and gains a direction at once");
        }
    }
}
