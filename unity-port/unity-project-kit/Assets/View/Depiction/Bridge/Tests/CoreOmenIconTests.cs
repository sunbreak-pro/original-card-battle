// #349: the omen badge carries the kind's icon and, for attack and guard only, a number. Runs under
// `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System;
using System.Linq;
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
                Assert.That(CoreText.OmenOf(sweep, Enemies.PolearmWarped).ValueText, Is.EqualTo("9"), "the face alone without a preview");
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
        public void ASkillThatPutsAStatusOnThePlayer_CarriesTheStamp_AndTheStatusInPlaceOfANumber()
        {
            // battle-visual-v1 §4.4 状態付与 (#455): 追い立てる pushes and puts 鈍足 on the player.
            OmenFrame frame = CoreText.OmenOf(OmenFor(Enemies.PackAlpha, "herd"), Enemies.PackAlpha);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Seal));
                Assert.That(frame.KindLabel, Is.EqualTo("技"));
                Assert.That(frame.ValueText, Is.Empty);
                Assert.That(frame.Statuses.Select(s => s.Label), Is.EqualTo(new[] { "鈍足" }));
                Assert.That(frame.Statuses.Select(s => s.Glyph), Is.EqualTo(new[] { StatusGlyph.Slow }));
            });
        }

        [Test]
        public void ASkillThatPutsNoStatusOnThePlayer_CarriesTheStar_AndNothingInPlaceOfANumber()
        {
            // 遠吠え puts 強化 on the hound itself: a 技, but not a 状態付与.
            OmenFrame frame = CoreText.OmenOf(OmenFor(Enemies.PackAlpha, "howl"), Enemies.PackAlpha);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Status));
                Assert.That(frame.KindLabel, Is.EqualTo("技"));
                Assert.That(frame.ValueText, Is.Empty);
                Assert.That(frame.Statuses, Is.Empty);
            });
        }

        [Test]
        public void AStatusWithNoIcon_IsCarriedByName_AndTheSelfStatusIsLeftOut()
        {
            // 縛りの言葉 puts 呪縛 (no 付録 A icon: the badge writes the name) and 威圧 on the player.
            OmenFrame frame = CoreText.OmenOf(OmenFor(Enemies.MiasmaPriest, "binding_word"), Enemies.MiasmaPriest);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Seal));
                Assert.That(frame.Statuses.Select(s => s.Label), Is.EqualTo(new[] { StatusKind.Binding.ToLabel(), StatusKind.Intimidate.ToLabel() }));
                Assert.That(frame.Statuses.Select(s => s.Glyph), Is.EqualTo(new[] { StatusGlyph.None, StatusGlyph.Intimidate }));
            });
        }

        [Test]
        public void AnAttackThatAlsoPutsAStatus_KeepsTheSword_AndItsNumber()
        {
            // 盾打ち strikes and puts 鈍足: its kind is 攻撃, so the sword and the power, no stamp.
            OmenFrame frame = CoreText.OmenOf(OmenFor(Enemies.ArmoredWarden, "shield_bash"), Enemies.ArmoredWarden);

            Assert.Multiple(() =>
            {
                Assert.That(frame.Icon, Is.EqualTo(OmenIcon.Attack));
                Assert.That(frame.ValueText, Is.Not.Empty);
                Assert.That(frame.Statuses, Is.Empty);
            });
        }

        [Test]
        public void AtMostTwoStatuses_AreCarried()
        {
            EnemyActionDef many = new EnemyActionDef("many", "many", BattleAttribute.Skill, 1,
                new Face(Reach: Reach.Only(0), Statuses: new[]
                {
                    new StatusGrant(StatusKind.Bleed, 1), new StatusGrant(StatusKind.Regen, 1, OnSelf: true),
                    new StatusGrant(StatusKind.Slow, 1), new StatusGrant(StatusKind.Fatigue, 1),
                }));
            Assert.That(CoreText.OmenStatusesOf(OmenKind.Skill, many).Select(s => s.Glyph),
                Is.EqualTo(new[] { StatusGlyph.Bleed, StatusGlyph.Slow }), "the self status is skipped; the third foe status is not shown");
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
