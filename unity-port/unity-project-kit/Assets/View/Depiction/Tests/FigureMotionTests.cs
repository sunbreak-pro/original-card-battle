// #288: which picture a figure wears, which way its body faces, and the shapes of the enemy's move
// and of the fall. The View takes every one of these numbers from FigureMotion.cs.
using System;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class FigureMotionTests
    {
        private static readonly StrikeSystem[] Systems = (StrikeSystem[])Enum.GetValues(typeof(StrikeSystem));

        // ---- the art's paths ----

        [Test]
        public void TheArt_IsFiledUnderTheEnemysId_AsTheIntakeRulesNameIt()
        {
            Assert.That(CharacterArt.PathOf("polearm_warped", FigurePose.Idle),
                Is.EqualTo("Assets/Art/Characters/polearm_warped/chr_polearm_warped_idle_stand.png"));
            Assert.That(CharacterArt.PathOf("polearm_warped", FigurePose.Hit),
                Is.EqualTo("Assets/Art/Characters/polearm_warped/chr_polearm_warped_react_hit.png"));
            Assert.That(CharacterArt.PathOf("polearm_warped", FigurePose.Down),
                Is.EqualTo("Assets/Art/Characters/polearm_warped/chr_polearm_warped_react_down.png"));
            Assert.That(CharacterArt.FileName("polearm_warped", FigurePose.Act), Is.EqualTo("chr_polearm_warped_act_"), "any act_* picture");
            Assert.Throws<ArgumentException>(() => CharacterArt.Folder(""));
        }

        // ---- facing ----

        [Test]
        public void Art_IsNeverMirrored_OnlyTheEnemysPlaceholderIs()
        {
            Assert.That(FigureFacing.HomeScaleX(UnitSide.Enemy, wearsArt: true), Is.EqualTo(1f), "enemy art is drawn facing left");
            Assert.That(FigureFacing.HomeScaleX(UnitSide.Player, wearsArt: true), Is.EqualTo(1f), "player art is drawn facing right");
            Assert.That(FigureFacing.HomeScaleX(UnitSide.Enemy, wearsArt: false), Is.EqualTo(-1f), "the silhouette is drawn facing right");
            Assert.That(FigureFacing.HomeScaleX(UnitSide.Player, wearsArt: false), Is.EqualTo(1f));
        }

        [Test]
        public void TheEnemysMove_KeepsItsFacing_ThroughTheMove_AndEndsBackAtRest()
        {
            // The bug #288 names: the move set the scale to plain positive numbers and ended at
            // Vector3.one, so a mirrored enemy turned its back after every action.
            foreach (float home in new[] { -1f, 1f })
            foreach (StrikeSystem system in Systems)
            {
                for (int i = 0; i <= 20; i++)
                {
                    float bell = (float)Math.Sin(i / 20.0 * Math.PI);
                    (float x, float y) = FigureFacing.During(home, system, bell);
                    Assert.That(Math.Sign(x), Is.EqualTo(Math.Sign(home)), system + " at step " + i);
                    Assert.That(y, Is.GreaterThan(0f), system + " at step " + i);
                }
                (float endX, float endY) = FigureFacing.During(home, system, 0f);
                Assert.That(endX, Is.EqualTo(home), system + ": back at rest");
                Assert.That(endY, Is.EqualTo(1f), system + ": back at rest");
            }
        }

        [Test]
        public void TheFourActions_ReadApart_ByHowFarTheyReach()
        {
            Assert.That(EnemyMotionShape.Reach(StrikeSystem.Thrust), Is.GreaterThan(EnemyMotionShape.Reach(StrikeSystem.Strike)));
            Assert.That(EnemyMotionShape.Reach(StrikeSystem.Strike), Is.GreaterThan(EnemyMotionShape.Reach(StrikeSystem.Sweep)));
            Assert.That(EnemyMotionShape.Reach(StrikeSystem.Shield), Is.EqualTo(0f), "a guard stands its ground");
        }

        // ---- the fall ----

        [Test]
        public void TheFall_TiltsAwayFromTheOtherSide_Sinks_Darkens_ThenFadesOut()
        {
            Assert.That(DefeatShape.Tilt(UnitSide.Enemy, 0f), Is.EqualTo(0f));
            Assert.That(DefeatShape.Tilt(UnitSide.Enemy, 1f), Is.LessThan(0f), "the enemy falls back to the right");
            Assert.That(DefeatShape.Tilt(UnitSide.Player, 1f), Is.GreaterThan(0f), "the player falls back to the left");
            Assert.That(DefeatShape.Sink(1f), Is.LessThan(0f));
            Assert.That(DefeatShape.Brightness(0f), Is.EqualTo(1f));
            Assert.That(DefeatShape.Brightness(1f), Is.EqualTo(DefeatShape.DarkTo).Within(1e-5f));

            Assert.That(DefeatShape.Alpha(0f), Is.EqualTo(1f));
            Assert.That(DefeatShape.Alpha(DefeatShape.FadeFrom), Is.EqualTo(1f), "it falls first, then fades");
            Assert.That(DefeatShape.Alpha(1f), Is.EqualTo(0f), "gone at the end, so the cell is empty");

            float last = 2f;
            for (int i = 0; i <= 20; i++)
            {
                float alpha = DefeatShape.Alpha(i / 20f);
                Assert.That(alpha, Is.LessThanOrEqualTo(last));
                last = alpha;
            }
        }
    }
}
