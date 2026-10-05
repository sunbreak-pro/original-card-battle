// #349: the omen badge carries the kind's icon and, for attack and guard only, a number. Runs under
// `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class CoreOmenIconTests
    {
        private static Omen OmenFor(EnemyDef enemy, string id)
        {
            return new Omen(id, EnemyAi.LabelOf(enemy.Actions[id]));
        }

        [Test]
        public void AnAttackOmen_CarriesTheSword_AndTheCoresNumber()
        {
            Omen sweep = OmenFor(Enemies.PolearmWarped, "sweep");
            // 17 is a number no rule produces from the sweep: the writer copies the core's preview.
            OmenFrame frame = CoreText.OmenOf(sweep, Enemies.PolearmWarped, new OmenPreview(17, 9, true, false));

            Assert.Multiple(() =>
            {
                Assert.That(frame.Visible, Is.True);
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Attack));
                Assert.That(frame.KindLabel, Is.EqualTo("攻撃"));
                Assert.That(frame.ValueText, Is.EqualTo("17"));
                Assert.That(CoreText.OmenOf(sweep, Enemies.PolearmWarped).ValueText, Is.EqualTo("8"), "the face alone without a preview");
            });
        }

        [Test]
        public void AGuardOmen_CarriesTheShield_AndTheFaceGuard()
        {
            Omen guard = OmenFor(Enemies.PolearmWarped, "guard_up");
            OmenFrame frame = CoreText.OmenOf(guard, Enemies.PolearmWarped);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Guard));
                Assert.That(frame.KindLabel, Is.EqualTo("防御"));
                Assert.That(frame.ValueText, Is.EqualTo("3"));
                Assert.That(CoreText.OmenOf(guard, Enemies.PolearmWarped, new OmenPreview(0, 0, false, false)).ValueText,
                    Is.EqualTo("3"), "an attack preview does not touch a guard's number");
            });
        }

        [Test]
        public void ASkillOmen_CarriesTheStatusIcon_AndNoNumber()
        {
            OmenFrame frame = CoreText.OmenOf(OmenFor(Enemies.PackAlpha, "herd"), Enemies.PackAlpha);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Status));
                Assert.That(frame.KindLabel, Is.EqualTo("技"));
                Assert.That(frame.ValueText, Is.Empty);
            });
        }

        [Test]
        public void AMoveOmen_ReadsMove_AndShowsNoNumber_EvenWithAGuardFace()
        {
            // 踏み込み moves one cell and gains Guard 2, but its kind is 移動.
            OmenFrame frame = CoreText.OmenOf(OmenFor(Enemies.PolearmWarped, "step_forward"), Enemies.PolearmWarped);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Move));
                Assert.That(frame.KindLabel, Is.EqualTo("移動"));
                Assert.That(frame.ValueText, Is.Empty);
            });
        }

        [Test]
        public void AStanceOmen_CarriesTheFlag_AndNoNumber()
        {
            OmenFrame frame = CoreText.OmenOf(OmenFor(Enemies.PackAlpha, "hunt_stance"), Enemies.PackAlpha);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Stance));
                Assert.That(frame.KindLabel, Is.EqualTo("構え"));
                Assert.That(frame.ValueText, Is.Empty);
            });
        }

        [Test]
        public void ARestOmen_CarriesTheRestIcon_AndNoNumber()
        {
            OmenFrame frame = CoreText.OmenOf(new Omen(EnemyAi.RestActionId, EnemyAi.RestLabel), Enemies.PolearmWarped);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Visible, Is.True);
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Rest));
                Assert.That(frame.KindLabel, Is.EqualTo("休み"));
                Assert.That(frame.ValueText, Is.Empty);
            });
        }

        [Test]
        public void EveryOmenKind_HasAnIconAndAWord()
        {
            foreach (OmenKind kind in Enum.GetValues(typeof(OmenKind)))
            {
                Assert.That(CoreText.IconOf(kind), Is.Not.EqualTo(OmenIcon.None), kind.ToString());
                Assert.That(CoreText.OmenKindWord(kind), Is.Not.Empty, kind.ToString());
            }
            Assert.That(new[] { CoreText.IconOf(OmenKind.Attack), CoreText.IconOf(OmenKind.Guard), CoreText.IconOf(OmenKind.Skill) },
                Is.Unique, "attack, guard and the status kind are told apart by their icons");
        }
    }
}
