using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class DragTraceTests
    {
        private static DragTraceFrame HeldFrame() => new DragTraceFrame
        {
            Frame = 1234,
            Time = 12.3456789,
            PointerX = 960.5f,
            PointerY = 300.25f,
            Card = new DragTraceCard
            {
                Id = "sw_breath",
                Held = true,
                X = -120.125f,
                Y = 48f,
                RotX = 0f,
                RotY = 0f,
                RotZ = 357.5f,
                ScaleX = 1f,
                ScaleY = 1f,
                ScaleZ = 1f,
                Alpha = 0.92f,
            },
            Receiver = DragReceiver.Enemy,
            ReachCells = new[] { 3, 4 },
            StaminaPay = 2,
        };

        [Test]
        public void AHeldFrame_IsOneLineWithEveryField()
        {
            Assert.That(HeldFrame().ToJson(), Is.EqualTo(
                "{\"frame\":1234,\"time\":12.345679,\"pointer\":[960.5,300.25]," +
                "\"card\":{\"id\":\"sw_breath\",\"held\":true,\"pos\":[-120.125,48],\"rot\":[0,0,357.5],\"scale\":[1,1,1],\"alpha\":0.92}," +
                "\"receiver\":\"enemy\",\"reach\":[3,4],\"pay\":2}"));
        }

        [Test]
        public void AFrameBeforeAnyCard_WritesNulls_AndNoReach()
        {
            var frame = new DragTraceFrame { Frame = 1, Time = 0.5 };

            Assert.That(frame.ToJson(), Is.EqualTo(
                "{\"frame\":1,\"time\":0.500000,\"pointer\":null,\"card\":null,\"receiver\":\"none\",\"reach\":[],\"pay\":null}"));
        }

        [Test]
        public void TheLine_NeverHoldsANewline()
        {
            DragTraceFrame frame = HeldFrame();
            frame.Card.Id = "odd\nid\t\"quoted\"\\";

            string json = frame.ToJson();

            Assert.That(json, Does.Not.Contain("\n"));
            Assert.That(json, Does.Contain("\"id\":\"odd\\nid\\t\\\"quoted\\\"\\\\\""));
        }

        [Test]
        public void TheReceiver_IsNamed_EnemySelfOrNone()
        {
            Assert.That(DragTraceFrame.ReceiverName(DragReceiver.Enemy), Is.EqualTo("enemy"));
            Assert.That(DragTraceFrame.ReceiverName(DragReceiver.Self), Is.EqualTo("self"));
            Assert.That(DragTraceFrame.ReceiverName(DragReceiver.None), Is.EqualTo("none"));
        }

        [Test]
        public void Numbers_UseADot_UnderACommaLocale()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string json = HeldFrame().ToJson();

                Assert.That(json, Does.Contain("\"time\":12.345679"));
                Assert.That(json, Does.Contain("\"alpha\":0.92"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Test]
        public void ANonFiniteNumber_IsWrittenAsNull()
        {
            DragTraceFrame frame = HeldFrame();
            frame.Card.Alpha = float.NaN;

            Assert.That(frame.ToJson(), Does.Contain("\"alpha\":null"));
        }

        [Test]
        public void ThePay_IsTheNumberAfterTheArrow_OnlyWhileShown()
        {
            Assert.That(DragTrace.ParsePay("▸ 2", true), Is.EqualTo(2));
            Assert.That(DragTrace.ParsePay("▸ 12", true), Is.EqualTo(12));
            Assert.That(DragTrace.ParsePay("▸ 2", false), Is.Null);
            Assert.That(DragTrace.ParsePay("", true), Is.Null);
            Assert.That(DragTrace.ParsePay("▸ ", true), Is.Null);
        }

        [Test]
        public void ACellsNumber_ComesFromItsName()
        {
            Assert.That(DragTrace.CellNumber("Cell1"), Is.EqualTo(1));
            Assert.That(DragTrace.CellNumber("Cell12"), Is.EqualTo(12));
            Assert.That(DragTrace.CellNumber("Cell"), Is.EqualTo(0));
            Assert.That(DragTrace.CellNumber("ReachFill"), Is.EqualTo(0));
            Assert.That(DragTrace.CellNumber(null), Is.EqualTo(0));
        }
    }
}
