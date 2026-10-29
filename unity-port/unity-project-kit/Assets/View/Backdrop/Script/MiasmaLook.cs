// How the miasma shows on the exploration screen (battle-visual-v1 §2.5, #90): the gauge in the top band
// (dungeon_exploration_v4 §6.2: ten ticks, a line every 20%, the next step drawn faintly ahead) and the
// shade that closes in on the screen as the reading climbs. Pure functions of the reading and the time,
// so the same moment always draws the same picture and `dotnet test` can pin them.
//
// The colours follow world-v1 §3 rule 1 (the thicker the miasma, the deeper it leans to violet) and
// battle-visual-v1 §2.1 (the miasma icon is phosphor; amber stays the player's; 注意 always comes with △).
// The readings the rules hang on (death at 100, a max-stamina line every 20, no further drop past 80) are
// DungeonCore's Miasma; this assembly does not reference it, so they are restated here and the tests pin
// them to concept-v3 §6 and seven_layers_v4 §3.
namespace Backdrop
{
    /// <summary>What the faint share ahead of the gauge warns of.</summary>
    public enum MiasmaWarning
    {
        /// <summary>The next step only adds to the reading.</summary>
        None,
        /// <summary>The next step crosses one of the 20% lines, where max stamina drops.</summary>
        StepLine,
        /// <summary>The next step reaches 100% and ends the life.</summary>
        Death,
    }

    /// <summary>How one tick of the gauge is drawn. <see cref="Height"/> is a share of the gauge's height.</summary>
    public readonly struct GaugeTick
    {
        public readonly int Percent;
        public readonly bool IsStepLine;
        public readonly float Height;

        public GaugeTick(int percent, bool isStepLine, float height)
        {
            Percent = percent;
            IsStepLine = isStepLine;
            Height = height;
        }
    }

    /// <summary>
    /// How far the shade has closed in at one reading. Alphas are of <see cref="MiasmaLook.ShadeColor"/>
    /// except <see cref="SeepAlpha"/>, which is of <see cref="MiasmaLook.SeepColor"/>.
    /// </summary>
    public readonly struct MiasmaShade
    {
        /// <summary>0 at a clear 0% to 1 at 100%.</summary>
        public readonly float Strength;
        /// <summary>Laid evenly over the whole screen.</summary>
        public readonly float VeilAlpha;
        /// <summary>Added at the corners, falling to nothing at <see cref="InnerRadius"/>.</summary>
        public readonly float EdgeAlpha;
        /// <summary>Where the darkening of the edges starts, as a share of the half diagonal from the centre.</summary>
        public readonly float InnerRadius;
        /// <summary>The violet glow that seeps in at the corners, on the same falloff as the edges.</summary>
        public readonly float SeepAlpha;

        public MiasmaShade(float strength, float veilAlpha, float edgeAlpha, float innerRadius, float seepAlpha)
        {
            Strength = strength;
            VeilAlpha = veilAlpha;
            EdgeAlpha = edgeAlpha;
            InnerRadius = innerRadius;
            SeepAlpha = seepAlpha;
        }
    }

    public static class MiasmaLook
    {
        /// <summary>The reading that ends the life (concept-v3 §6).</summary>
        public const int DeathAt = 100;
        /// <summary>Max stamina drops by one at every multiple of this (concept-v3 §6).</summary>
        public const int StepPercent = 20;
        /// <summary>The last line that drops max stamina; past it the reading only counts down to death (seven_layers_v4 §3).</summary>
        public const int LastStepLine = 80;

        // ---- the gauge ----

        /// <summary>パネル（不透明） (§2.1): the gauge's empty track.</summary>
        public static readonly Rgba Track = Rgba.Hex("#141925");
        /// <summary>罫線 (§2.1): the gauge's frame.</summary>
        public static readonly Rgba Frame = Rgba.Bytes(157, 166, 186, 0.26f);
        /// <summary>燐光 (§2.1): the fill of a thin reading.</summary>
        public static readonly Rgba FillThin = Rgba.Hex("#b9a3ff");
        /// <summary>菫 (§2.1): the fill of a thick reading.</summary>
        public static readonly Rgba FillThick = Rgba.Hex("#8b6fd0");
        /// <summary>注意 (§2.1): the share ahead when the next step costs more than miasma. Always with △.</summary>
        public static readonly Rgba Caution = Rgba.Hex("#e6c35c");
        /// <summary>地 (§2.1): the ticks, drawn over the fill.</summary>
        public static readonly Rgba Tick = Rgba.Hex("#10141d", 0.9f);
        /// <summary>The fill and the track are told apart by colour alone, so they keep a contrast of 3 (WCAG 1.4.11).</summary>
        public const float FillMinContrast = 3f;

        /// <summary>The share ahead's alpha when it only warns of more miasma.</summary>
        public const float AheadAlpha = 0.4f;
        /// <summary>The share ahead's alpha when it crosses a line.</summary>
        public const float AheadStepAlpha = 0.7f;

        /// <summary>The mark the forecast label starts with when the share ahead is 注意 (§2.1: 注意 always with △).</summary>
        public const string CautionMark = "△ ";

        /// <summary>How long the fill takes to climb to a new reading.</summary>
        public const int RiseMs = 500;

        /// <summary>From this reading the fill and the shade breathe: only death is left ahead (seven_layers_v4 §3).</summary>
        public const int BreathFrom = LastStepLine;
        public const int BreathSlowMs = 2400;
        public const int BreathFastMs = 1200;
        /// <summary>How far the fill's alpha dips at the bottom of a breath.</summary>
        public const float BreathDepth = 0.25f;

        /// <summary>The fill's colour at <paramref name="percent"/>: phosphor when thin, deepening to violet as it thickens.</summary>
        public static Rgba FillAt(int percent)
        {
            return BackdropSpec.Mix(FillThin, FillThick, Clamp01(percent / (float)DeathAt));
        }

        /// <summary>What the share from <paramref name="percent"/> to <paramref name="nextPercent"/> warns of. Meant for the forecast of the next single step only: crossing a line is not always a drop in max stamina (a rest raises it), so do not use it for an action that raises max stamina.</summary>
        public static MiasmaWarning Warning(int percent, int nextPercent)
        {
            if (nextPercent <= percent) return MiasmaWarning.None;
            if (nextPercent >= DeathAt) return MiasmaWarning.Death;
            for (int line = StepPercent; line <= LastStepLine; line += StepPercent)
            {
                if (percent < line && nextPercent >= line) return MiasmaWarning.StepLine;
            }
            return MiasmaWarning.None;
        }

        /// <summary>The colour of the share ahead of the fill. Without a warning it is the fill it will become, faint.</summary>
        public static Rgba AheadColor(int percent, int nextPercent)
        {
            switch (Warning(percent, nextPercent))
            {
                case MiasmaWarning.Death:
                    return Caution;
                case MiasmaWarning.StepLine:
                    return Caution.WithAlpha(AheadStepAlpha);
                default:
                    return FillAt(nextPercent).WithAlpha(AheadAlpha);
            }
        }

        /// <summary>The mark to put before the forecast label: <see cref="CautionMark"/> when the share ahead is 注意.</summary>
        public static string ForecastMark(int percent, int nextPercent)
        {
            return Warning(percent, nextPercent) == MiasmaWarning.None ? "" : CautionMark;
        }

        /// <summary>The gauge's nine ticks between 0 and 100 (ten cells): full-height lines at 20, 40, 60, 80, short ones between.</summary>
        public static GaugeTick[] Ticks()
        {
            var ticks = new GaugeTick[9];
            for (int i = 1; i <= 9; i++)
            {
                int percent = i * 10;
                bool line = percent % StepPercent == 0;
                ticks[i - 1] = new GaugeTick(percent, line, line ? 1f : 0.4f);
            }
            return ticks;
        }

        /// <summary>
        /// The reading the fill shows <paramref name="elapsedMs"/> after it moved from <paramref name="from"/>
        /// to <paramref name="to"/>: eased over <see cref="RiseMs"/>. It goes down the same way (incense).
        /// </summary>
        public static float Rise(int from, int to, double elapsedMs)
        {
            if (elapsedMs <= 0) return from;
            if (elapsedMs >= RiseMs) return to;
            double t = elapsedMs / RiseMs;
            return (float)(from + (to - from) * (t * t * (3.0 - 2.0 * t)));
        }

        /// <summary>The period of the breath at <paramref name="percent"/>: slow at 80%, quickening to 99%. 0 below 80%.</summary>
        public static int BreathPeriodMs(float percent)
        {
            if (percent < BreathFrom) return 0;
            float t = Clamp01((percent - BreathFrom) / (DeathAt - 1 - BreathFrom));
            return (int)System.Math.Round(BreathSlowMs + (BreathFastMs - BreathSlowMs) * t);
        }

        /// <summary>The breath at <paramref name="ms"/>: 0 at rest, 1 at the bottom of a breath, on a cosine so it never jumps.</summary>
        public static float Breath(float percent, double ms)
        {
            int period = BreathPeriodMs(percent);
            if (period <= 0) return 0f;
            double phase = ms / period - System.Math.Floor(ms / period);
            return (float)(0.5 - 0.5 * System.Math.Cos(phase * 2.0 * System.Math.PI));
        }

        /// <summary>The share of the fill's alpha lit at <paramref name="ms"/>: 1 below 80%, dipping by <see cref="BreathDepth"/> from there.</summary>
        public static float FillAlpha(float percent, double ms)
        {
            return 1f - BreathDepth * Breath(percent, ms);
        }

        // ---- the shade over the screen ----

        /// <summary>紺 (world-v1 §6), darker than the darkest sky: the shade's colour.</summary>
        public static readonly Rgba ShadeColor = Rgba.Hex("#05060f");
        /// <summary>菫 (§2.1): the miasma's own glow seeping in at the corners.</summary>
        public static readonly Rgba SeepColor = Rgba.Hex("#8b6fd0");

        public const float ShadeVeilMax = 0.20f;
        public const float ShadeEdgeMax = 0.80f;
        public const float ShadeInnerClear = 0.85f;
        public const float ShadeInnerClosed = 0.40f;
        public const float ShadeSeepMax = 0.20f;
        /// <summary>How far the inner edge of the shade moves in and out at the bottom of a breath.</summary>
        public const float ShadeBreathRadius = 0.03f;
        /// <summary>The curve of the strength: above 1, so the first 20% is only a faint tint (about 0.1) and the shade closes in gradually (battle-visual-v1 §2.5).</summary>
        public const double ShadeCurve = 1.4;

        /// <summary>The shade at <paramref name="percent"/> (a reading easing up with <see cref="Rise"/> may fall between whole numbers).</summary>
        public static MiasmaShade ShadeAt(float percent)
        {
            float s = (float)System.Math.Pow(Clamp01(percent / DeathAt), ShadeCurve);
            return new MiasmaShade(
                s,
                ShadeVeilMax * s,
                ShadeEdgeMax * s,
                ShadeInnerClear + (ShadeInnerClosed - ShadeInnerClear) * s,
                ShadeSeepMax * s);
        }

        /// <summary>The shade at <paramref name="percent"/> at <paramref name="ms"/>: from 80% its inner edge breathes in step with the gauge.</summary>
        public static MiasmaShade ShadeAt(float percent, double ms)
        {
            MiasmaShade still = ShadeAt(percent);
            float breath = Breath(percent, ms);
            if (breath <= 0f) return still;
            return new MiasmaShade(still.Strength, still.VeilAlpha, still.EdgeAlpha,
                still.InnerRadius - ShadeBreathRadius * breath, still.SeepAlpha);
        }

        /// <summary>
        /// How far point (<paramref name="x"/>, <paramref name="y"/>) — shares of the screen, 0 at the top left —
        /// is from the centre, as a share of the half diagonal on a <paramref name="aspect"/> (width / height) screen.
        /// </summary>
        public static float CornerDistance(float x, float y, float aspect)
        {
            double dx = (x - 0.5) * aspect;
            double dy = y - 0.5;
            double half = System.Math.Sqrt(aspect * aspect + 1.0) / 2.0;
            return (float)(System.Math.Sqrt(dx * dx + dy * dy) / half);
        }

        /// <summary>How much of the edges' darkening reaches a point <paramref name="distance"/> from the centre (see <see cref="CornerDistance"/>).</summary>
        public static float EdgeFalloff(MiasmaShade shade, float distance)
        {
            if (distance <= shade.InnerRadius) return 0f;
            if (distance >= 1f) return 1f;
            float t = (distance - shade.InnerRadius) / (1f - shade.InnerRadius);
            return t * t * (3f - 2f * t);
        }

        /// <summary>The shade colour's alpha at a point <paramref name="distance"/> from the centre: the even veil, then the edges on top.</summary>
        public static float ShadeAlphaAt(MiasmaShade shade, float distance)
        {
            float edge = shade.EdgeAlpha * EdgeFalloff(shade, distance);
            return 1f - (1f - shade.VeilAlpha) * (1f - edge);
        }

        /// <summary><paramref name="under"/> as the shade leaves it at a point <paramref name="distance"/> from the centre: darkened, then the seep laid on.</summary>
        public static Rgba Shaded(Rgba under, MiasmaShade shade, float distance)
        {
            Rgba dark = BackdropSpec.Over(ShadeColor.WithAlpha(ShadeAlphaAt(shade, distance)), under);
            return BackdropSpec.Over(SeepColor.WithAlpha(shade.SeepAlpha * EdgeFalloff(shade, distance)), dark);
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            return v > 1f ? 1f : v;
        }
    }
}
