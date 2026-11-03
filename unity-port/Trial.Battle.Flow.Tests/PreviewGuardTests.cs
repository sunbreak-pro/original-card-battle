// dotnet-only (#820): the detail's 「予測値」 row and the split it reads — the power before the enemy's Guard and the Guard —
// so a fired bonus that a Guard of the same size swallows no longer reads as the printed value going through untouched.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using NUnit.Framework;
using Trial.Battle.Flow.Tests.Fakes;

namespace Trial.Battle.Flow.Tests
{
    public class PreviewGuardTests
    {
        private List<Exception> _unhandled;

        [SetUp]
        public void SetUp()
        {
            Microtasks.Reset();
            _unhandled = new List<Exception>();
            Microtasks.Unhandled = e => _unhandled.Add(e);
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(_unhandled, Is.Empty, "no exception may reach Microtasks.Unhandled");
            Microtasks.Reset();
        }

        private static readonly string[] NoMods = new string[0];

        [Test]
        public void AHeavyBlowTheGuardSwallows_ShowsThePowerTheGuardAndThePrintedValue()
        {
            // 棘の一射 into an attack omen: 8 + 重撃 6 = 14 against Guard 6 — 8 gets through, the printed 8 too.
            var split = new GuardSplit { Raw = "14", Guard = 6 };
            Assert.That(BattlePlayer.DetailText.Predicted("8", split, "8", NoMods, lit: true), Is.EqualTo("8（威力 14 − Guard 6・表の値 8・特性）"));
        }

        [Test]
        public void APlainBlowIntoGuard_ShowsThePowerAndTheGuard_AndNoPrintedValueWhenThePowerIsIt()
        {
            var split = new GuardSplit { Raw = "8", Guard = 6 };
            Assert.That(BattlePlayer.DetailText.Predicted("2", split, "8", NoMods, lit: false), Is.EqualTo("2（威力 8 − Guard 6）"));
        }

        [Test]
        public void WithoutAGuard_TheRowIsAsBefore()
        {
            Assert.That(BattlePlayer.DetailText.Predicted("8", null, "8", NoMods, lit: false), Is.EqualTo("8"));
            Assert.That(BattlePlayer.DetailText.Predicted("14", null, "8", new[] { "強化 ×1.5" }, lit: true), Is.EqualTo("14（表の値 8・強化 ×1.5・特性）"));
            Assert.That(BattlePlayer.DetailText.Predicted("9", null, null, NoMods, lit: false), Is.EqualTo("9"), "a card with no value");
            Assert.That(BattlePlayer.DetailText.Predicted("", new GuardSplit { Raw = "8", Guard = 6 }, "8", NoMods, lit: false), Is.Empty, "no prediction, no row");
        }

        [Test]
        public void AGuardThatTakesNothing_AddsNothing()
        {
            // A Guard of 0, or a power the Guard did not change, leaves the row as the HP number alone.
            Assert.That(BattlePlayer.DetailText.Predicted("8", new GuardSplit { Raw = "8", Guard = 0 }, "8", NoMods, lit: false), Is.EqualTo("8"));
            Assert.That(BattlePlayer.DetailText.Predicted("8", new GuardSplit { Raw = "8", Guard = 3 }, "8", NoMods, lit: false), Is.EqualTo("8"));
        }

        [Test]
        public void PreviewGuard_IsTheSourcesPowerAndTheFramesGuard_OnlyWhileTheEnemyHoldsGuard()
        {
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, 20260921));
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle), Is.True);

            bool seenGuard = false;
            for (int turn = 0; turn < 10 && !seenGuard; turn++)
            {
                int guard = rig.Source.Frame.Enemies[0].Unit.Guard;
                foreach (CardFace face in rig.Source.Frame.Hand.Where(f => f.Aim == CardAim.Single && f.HpLossOn.Count > 0
                    && f.Values.Any(v => v.Kind == CardValueKind.Power)))
                {
                    string raw = rig.Source.PreviewFor(face.Id, 0);
                    GuardSplit split = rig.Player.HandCallbacks.PreviewGuard(face.Id, 0);
                    if (guard == 0 || string.IsNullOrEmpty(raw))
                    {
                        Assert.That(split, Is.Null, face.Id + " with no Guard to split");
                        continue;
                    }
                    Assert.That(split, Is.Not.Null, face.Id);
                    Assert.That(split.Raw, Is.EqualTo(raw), face.Id + ": the power before the Guard is the source's");
                    Assert.That(split.Guard, Is.EqualTo(guard), face.Id + ": the Guard is the one on the enemy's panel");
                    seenGuard = true;
                }
                if (seenGuard) break;
                rig.Run(2000);
                Assert.That(rig.Input(() => rig.Player.RequestEndTurn()), Is.True);
                Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle || rig.Player.State == BattlePlayerState.Over), Is.True);
                if (rig.Player.State == BattlePlayerState.Over) break;
            }
            Assert.That(seenGuard, Is.True, "the polearm holds its 構え Guard on some turn with a blow in hand");
        }
    }
}
