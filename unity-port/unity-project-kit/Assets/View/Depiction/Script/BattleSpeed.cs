// The battle speed the player picks (#348): 1.0, 1.25 or 1.5 times. Pure C#, like the rest of
// Script/, so `dotnet test` holds the same factors the screen plays.
//
// Every length in EffectCatalog and EffectFlow is the length at 1.25 times (today's pace, the
// default). At a multiplier k an effect takes "written ms x 1.25 / k": 1.0 times is slower than
// today, 1.5 times faster. The View never rescales a length: it hands UiTween the written ms and
// sets UiTween.Speed to TweenSpeed, and UiTween.Run and UiTween.Wait both divide by that speed. So
// one number stretches the tweens, the effect waits and the gaps between events alike.
using System;

namespace Depiction
{
    /// <summary>The three steps of the battle speed (battle_ui_ux_v2 §5.0).</summary>
    public enum BattleSpeedStep
    {
        /// <summary>1.0 times: the written lengths stretched by 1.25.</summary>
        Slow,

        /// <summary>1.25 times: today's pace and the default. The written lengths as they are.</summary>
        Normal,

        /// <summary>1.5 times: the written lengths times 1.25 / 1.5.</summary>
        Fast,
    }

    /// <summary>
    /// The one place the battle speed's factors live. The View reads <see cref="TweenSpeed(BattleSpeedStep, bool)"/>
    /// into UiTween.Speed and shows <see cref="Label"/>, <see cref="Chevrons"/> and <see cref="Tooltip"/>; the measurement reads <see cref="WallMs"/> and
    /// <see cref="EventCapMsAt"/>.
    /// </summary>
    public static class BattleSpeed
    {
        public const BattleSpeedStep Default = BattleSpeedStep.Normal;

        /// <summary>The multiplier the lengths in EffectCatalog and EffectFlow are written at.</summary>
        public const float BaseMultiplier = 1.25f;

        /// <summary>battle_ui_ux_v2 §5.0: one action within 2.0 s.</summary>
        public const float EventCapMs = 2000f;

        /// <summary>UiTween's reduced-motion factor: what is left of a halted event settles within a few frames.</summary>
        public const float HaltedTweenSpeed = 1000f;

        public static readonly BattleSpeedStep[] Steps = { BattleSpeedStep.Slow, BattleSpeedStep.Normal, BattleSpeedStep.Fast };

        /// <summary>The factor the player picks: 1.0, 1.25 or 1.5.</summary>
        public static float Multiplier(BattleSpeedStep step)
        {
            switch (step)
            {
                case BattleSpeedStep.Slow: return 1f;
                case BattleSpeedStep.Normal: return 1.25f;
                case BattleSpeedStep.Fast: return 1.5f;
                default: throw new ArgumentOutOfRangeException(nameof(step), step, "No multiplier is written for this speed.");
            }
        }

        /// <summary>What UiTween.Speed is set to: 0.8, 1 or 1.2. The default plays the written lengths unchanged.</summary>
        public static float TweenSpeed(BattleSpeedStep step) => Multiplier(step) / BaseMultiplier;

        /// <summary>Like <see cref="TweenSpeed(BattleSpeedStep)"/>, but a halted battle (降参) settles at reduced-motion speed whatever was picked.</summary>
        public static float TweenSpeed(BattleSpeedStep step, bool halted) => halted ? HaltedTweenSpeed : TweenSpeed(step);

        /// <summary>How long a length written at 1.25 times really lasts at <paramref name="step"/>.</summary>
        public static float WallMs(float nominalMs, BattleSpeedStep step) => nominalMs * BaseMultiplier / Multiplier(step);

        /// <summary>
        /// The cap one action is held to at this speed: 2.0 s at 1.25 and 1.5 times, and the same
        /// 2.0 s stretched by the slow step's factor (2.5 s) at 1.0 times (battle_ui_ux_v2 §5.0).
        /// </summary>
        public static float EventCapMsAt(BattleSpeedStep step) => Multiplier(step) >= BaseMultiplier ? EventCapMs : WallMs(EventCapMs, step);

        /// <summary>The switch's next step: 1.0 → 1.25 → 1.5 → 1.0.</summary>
        public static BattleSpeedStep Next(BattleSpeedStep step)
        {
            switch (step)
            {
                case BattleSpeedStep.Slow: return BattleSpeedStep.Normal;
                case BattleSpeedStep.Normal: return BattleSpeedStep.Fast;
                case BattleSpeedStep.Fast: return BattleSpeedStep.Slow;
                default: throw new ArgumentOutOfRangeException(nameof(step), step, "No next speed is written for this speed.");
            }
        }

        /// <summary>
        /// The number on the switch (battle-visual-v1 §4.1): "×1.0", "×1.25" or "×1.5", with no word.
        /// The View shows it as it is, next to <see cref="Chevrons"/> chevrons.
        /// </summary>
        public static string Label(BattleSpeedStep step)
        {
            switch (step)
            {
                case BattleSpeedStep.Slow: return "×1.0";
                case BattleSpeedStep.Normal: return "×1.25";
                case BattleSpeedStep.Fast: return "×1.5";
                default: throw new ArgumentOutOfRangeException(nameof(step), step, "No label is written for this speed.");
            }
        }

        /// <summary>
        /// How many right-pointing chevrons the switch's icon stacks (battle-visual-v1 §4.1 and
        /// 付録 A 「速さ」): one at 1.0 times, two at 1.25, three at 1.5.
        /// </summary>
        public static int Chevrons(BattleSpeedStep step)
        {
            switch (step)
            {
                case BattleSpeedStep.Slow: return 1;
                case BattleSpeedStep.Normal: return 2;
                case BattleSpeedStep.Fast: return 3;
                default: throw new ArgumentOutOfRangeException(nameof(step), step, "No icon is written for this speed.");
            }
        }

        /// <summary>The line the switch shows under itself while hovered (battle-visual-v1 §4.1).</summary>
        public const string Tooltip = "戦闘の速さ。押すたびに ×1.0 → ×1.25 → ×1.5 と巡ります";

        /// <summary>What the screen stores (PlayerPrefs): fixed strings, so no culture's decimal mark gets in.</summary>
        public static string Save(BattleSpeedStep step)
        {
            switch (step)
            {
                case BattleSpeedStep.Slow: return "1.0";
                case BattleSpeedStep.Normal: return "1.25";
                case BattleSpeedStep.Fast: return "1.5";
                default: throw new ArgumentOutOfRangeException(nameof(step), step, "No saved form is written for this speed.");
            }
        }

        /// <summary>
        /// The speed the screen stored, read back. Nothing stored, or anything this build does not
        /// write, is the default (1.25 times).
        /// </summary>
        public static BattleSpeedStep Load(bool has, string saved)
        {
            if (!has || saved == null) return Default;
            string text = saved.Trim();
            foreach (BattleSpeedStep step in Steps)
            {
                if (string.Equals(Save(step), text, StringComparison.Ordinal)) return step;
            }
            return Default;
        }
    }
}
