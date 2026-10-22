// dotnet-only tests of BattleLayout (port of scenes/battle/layout.js): every constant of `L` against the JS value (a
// double entry: the table below is copied from layout.js, and the C# names are converted from the JS ones mechanically),
// and the four exported helpers.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Depiction;
using NUnit.Framework;

namespace Trial.Battle.Flow.Tests
{
    public class BattleLayoutTests
    {
        /// <summary>layout.js `L`, as the JS writes it (scenes/battle/layout.js:10-174). Arrays are checked on their own.</summary>
        private static readonly (string Js, double Value)[] JsL =
        {
            ("W", 1920), ("H", 1080),
            ("HORIZON_Y", 556), ("FEET_Y", 570), ("CHEST", 0.65), ("HEAD", 0.84),
            ("OMEN.w", 200), ("OMEN.h", 98), ("OMEN.overHead", 14),
            ("RECEIVER.w", 340), ("RECEIVER.h", 340),
            ("MARK_PAD.x", 46), ("MARK_PAD.top", 22), ("MARK_PAD.bottom", 12), ("MARK_PAD.hook", 26), ("MARK_PAD.width", 3),
            ("TAG_LIFT", 80), ("TAG_LIFT_GAP", 4), ("PRED_TAG_H", 60), ("STANCE_FLY_MS", 300), ("TRAIT_WORD_MS", 150),
            ("THROW.y", 790), ("THROW.x0", 400), ("THROW.x1", 1520), ("THROW.belowAlpha", 0.6),
            ("STAMINA.x", 32), ("STAMINA.y", 846), ("STAMINA.w", 236), ("STAMINA.h", 84),
            ("END_TURN.x", 1672), ("END_TURN.y", 976), ("END_TURN.w", 224), ("END_TURN.h", 72),
            ("BANNER.y", 400), ("BANNER.h", 132),
            ("GUIDE.y", 743), ("GUIDE.h", 28),
            ("RESULT.w", 520), ("RESULT.h", 220), ("RESULT.dy", -60), ("RESULT.rise", 40),
            ("LUNGE_PX", 46), ("RELEASE_RISE", 140), ("RELEASE_SCALE", 0.5), ("SNAP_PULL", 0.35), ("HELD_ALPHA", 0.92),
            ("DRAG_THRESHOLD", 8), ("LONG_PRESS_MS", 400), ("AUTO_WAIT_MS", 400), ("AUTO_DRAG_MS", 450),
            ("SHAKE.px", 8), ("SHAKE.cycles", 2),
            ("GHOST.w", 76), ("GHOST.h", 104), ("GHOST.alpha", 0.55),
            ("GUARD_NUMBER_SIZE", 34), ("PANEL_FOLLOW_RATE", 14),
            ("FF_SCALE", 4), ("FF_FLOAT_SCALE", 1.5), ("FF_MIN_RUN_MS", 300), ("SHORT_SCALE", 1000),
            ("RESULT_HOLD_MS", 1200), ("RESULT_SKIP_AFTER_MS", 400), ("RESULT_SKIP_PAUSE_MS", 350),
            ("OMEN_BLINK_STAGGER_MS", 120), ("BETWEEN_ENEMIES_MS", 600), ("FF_SOUND_GAP_MS", 90),
            ("END_TURN_MASH_MS", 600), ("END_TURN_OPEN_GUARD_MS", 500), ("END_TURN_MASH_RHYTHM", 1.3), ("END_TURN_MASH_MAX_MS", 1500),
            ("END_TURN_REFUSE_MS", 220), ("FALL_FOOT_MS", 300), ("FALL_GREY_MS", 300), ("MOVE_BUMP_MS", 40),
            ("END_TURN_FLASH_MS", 600), ("DOUBLE_CLICK_MS", 500), ("HEARTBEAT_HP", 15), ("VIGNETTE_PULSE_MS", 200),
            ("OPEN_FADE_MS", 240), ("OPEN_FONT_WAIT_MS", 1200),
        };

        /// <summary>FEET_Y → FeetY (the port's mechanical rule).</summary>
        private static string Pascal(string snake) =>
            string.Concat(snake.Split('_').Select(w => w.Length == 0 ? "" : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));

        /// <summary>overHead → OverHead, x0 → X0.</summary>
        private static string Member(string camel) => char.ToUpperInvariant(camel[0]) + camel.Substring(1);

        private static FieldInfo FieldOf(string js)
        {
            string[] parts = js.Split('.');
            Type owner = typeof(L);
            string name = Pascal(parts[0]);
            if (parts.Length == 2)
            {
                owner = typeof(L).GetNestedType(name, BindingFlags.Public);
                Assert.That(owner, Is.Not.Null, "L." + name + " (" + js + ")");
                name = Member(parts[1]);
            }
            FieldInfo f = owner.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(f, Is.Not.Null, owner.Name + "." + name + " (" + js + ")");
            return f;
        }

        [Test]
        public void EveryConstantOfL_HoldsTheJsValue()
        {
            foreach (var (js, value) in JsL)
            {
                object got = FieldOf(js).GetValue(null);
                Assert.That(Convert.ToDouble(got, CultureInfo.InvariantCulture), Is.EqualTo(value).Within(1e-6), js);
            }
        }

        [Test]
        public void L_HoldsNothingTheJsDoesNot()
        {
            var expected = new HashSet<string>(JsL.Select(e => FieldOf(e.Js).DeclaringType.Name + "." + FieldOf(e.Js).Name)) { "L.NumberSize" };
            var actual = new HashSet<string>();
            foreach (Type t in new[] { typeof(L) }.Concat(typeof(L).GetNestedTypes(BindingFlags.Public)))
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                    actual.Add(t.Name + "." + f.Name);
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test]
        public void NumberSize_IsTheFourTiers()
        {
            Assert.That(L.NumberSize, Is.EqualTo(new[] { 37f, 48f, 59f, 72f }));
            Assert.That(L.NumberSize, Is.Not.InstanceOf<float[]>(), "read-only, as Object.freeze");
        }

        [Test]
        public void ShortScale_IsBattleSpeedsHaltedTweenSpeed()
        {
            Assert.That(L.ShortScale, Is.EqualTo(BattleSpeed.HaltedTweenSpeed));
        }

        // ---- the helpers --------------------------------------------------------------------------

        [TestCase(-3, 37f)]
        [TestCase(0, 37f)]
        [TestCase(1, 37f)]
        [TestCase(2, 48f)]
        [TestCase(3, 59f)]
        [TestCase(4, 72f)]
        [TestCase(9, 72f)]
        public void NumberFont_ClampsTheTierToOneToFour(int tier, float size)
        {
            Assert.That(BattleLayout.NumberFont(tier), Is.EqualTo(size));
        }

        [Test]
        public void NumberFont_AtTheUniformIntensity_IsTheDsHpLoss48()
        {
            Assert.That(BattleLayout.NumberFont(EffectStrength.Uniform), Is.EqualTo(48f));
        }

        [TestCase(0, 45)]
        [TestCase(1, -45)]
        [TestCase(2, 180)]
        [TestCase(-1, 45)]
        [TestCase(3, 45)]
        public void HatchAngle_HandsTheThirdEnemysUprightStripesAs180(int hatch, int angle)
        {
            Assert.That(BattleLayout.HatchAngle(hatch), Is.EqualTo(angle));
        }

        [TestCase(0, "／")]
        [TestCase(1, "＼")]
        [TestCase(2, "｜")]
        [TestCase(-1, "／")]
        [TestCase(7, "／")]
        public void HatchGlyph_IsTheLegendsGlyph(int hatch, string glyph)
        {
            Assert.That(BattleLayout.HatchGlyph(hatch), Is.EqualTo(glyph));
        }

        [Test]
        public void HatchAngleAndGlyph_ReadTheOneEnemyHatchOfDepictionScript()
        {
            // layout.js imports EnemyHatch from DepictionTypes.js: one source for the angles.
            for (int h = -3; h <= EnemyHatch.Count + 2; h++)
            {
                int a = EnemyHatch.AngleOf(h);
                Assert.That(BattleLayout.HatchAngle(h), Is.EqualTo(a == 90 ? 180 : a), h.ToString());
                Assert.That(BattleLayout.HatchGlyph(h), Is.EqualTo(a == 90 ? "｜" : a < 0 ? "＼" : "／"), h.ToString());
            }
        }

        [Test]
        public void EnemyNameOf_IsTheRostersName()
        {
            foreach (BattleCore.EnemyDef def in BattleCore.Enemies.All)
                Assert.That(BattleLayout.EnemyNameOf(def.Id), Is.EqualTo(def.Name), def.Id);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("no_such_enemy")]
        public void EnemyNameOf_IsEmpty_ForNoIdOrAnUnknownOne(string id)
        {
            Assert.That(BattleLayout.EnemyNameOf(id), Is.EqualTo(""));
        }
    }
}
