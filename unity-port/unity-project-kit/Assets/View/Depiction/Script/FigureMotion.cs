// The figure's art and the shapes its body takes (#288). Pure C#, like the rest of Script/: the View
// asks these which picture to wear, which way the body faces and how far a move stretches it, so
// `dotnet test` holds the same numbers the screen plays.
using System;

namespace Depiction
{
    /// <summary>
    /// The four pictures a figure can wear. Only <see cref="Idle"/> is required; a pose with no picture
    /// shows the idle one, and the motion alone carries the moment.
    /// </summary>
    public enum FigurePose
    {
        Idle,
        Act,
        Hit,
        Down,
    }

    /// <summary>
    /// Where a figure's art lives in the Unity project (asset-intake.md §4):
    /// Assets/Art/Characters/&lt;id&gt;/chr_&lt;id&gt;_&lt;category&gt;_&lt;label&gt;.png.
    /// The Editor fills the figure's shelf from these paths; the script only names the id.
    /// </summary>
    public static class CharacterArt
    {
        public const string Root = "Assets/Art/Characters";

        public static string Folder(string id) => Root + "/" + Require(id);

        /// <summary>The file a pose is read from. <see cref="FigurePose.Act"/> is a prefix: the first act_* picture is taken.</summary>
        public static string FileName(string id, FigurePose pose)
        {
            Require(id);
            switch (pose)
            {
                case FigurePose.Idle: return "chr_" + id + "_idle_stand.png";
                case FigurePose.Act: return "chr_" + id + "_act_";
                case FigurePose.Hit: return "chr_" + id + "_react_hit.png";
                case FigurePose.Down: return "chr_" + id + "_react_down.png";
                default: throw new ArgumentOutOfRangeException(nameof(pose), pose, "No file is named for this pose.");
            }
        }

        public static string PathOf(string id, FigurePose pose) => Folder(id) + "/" + FileName(id, pose);

        private static string Require(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("An art id is required.", nameof(id));
            return id;
        }
    }

    /// <summary>
    /// Which way a figure's body faces, as its x scale at rest (#288). Art is drawn the way it faces on
    /// screen — the enemy to the left, the player to the right — and is never mirrored. The placeholder
    /// silhouette (ProceduralArt) is drawn facing right, so only the enemy's placeholder is mirrored.
    /// </summary>
    public static class FigureFacing
    {
        public static float HomeScaleX(UnitSide side, bool wearsArt)
        {
            return !wearsArt && side == UnitSide.Enemy ? -1f : 1f;
        }

        /// <summary>
        /// The body's scale while it moves: the move's stretch times the scale at rest, so a mirrored
        /// body stays mirrored through the move and is back at rest when <paramref name="bell"/> is 0.
        /// </summary>
        public static (float X, float Y) During(float homeScaleX, StrikeSystem system, float bell)
        {
            (float x, float y) = EnemyMotionShape.Stretch(system, bell);
            return (homeScaleX * x, y);
        }
    }

    /// <summary>
    /// How the enemy's body moves into its action (EffectId.EnemyMotion), by system, so the four actions
    /// read apart on one picture: 払 swings across, 突 drives straight in, 打 shoves with its weight,
    /// 盾 raises the haft; a step just steps. <c>bell</c> runs 0 → 1 → 0 over the move.
    /// </summary>
    public static class EnemyMotionShape
    {
        /// <summary>How far the figure travels toward the other side, in px at the end of the move.</summary>
        public static float Reach(StrikeSystem system)
        {
            switch (system)
            {
                case StrikeSystem.Thrust: return 90f;
                case StrikeSystem.Sweep: return 40f;
                case StrikeSystem.Strike: return 60f;
                case StrikeSystem.Shield: return 0f;
                default: return 70f;
            }
        }

        /// <summary>The stretch of the body, as factors on its scale at rest (1, 1 = unchanged).</summary>
        public static (float X, float Y) Stretch(StrikeSystem system, float bell)
        {
            switch (system)
            {
                case StrikeSystem.Sweep: return (1f + 0.08f * bell, 1f);
                case StrikeSystem.Thrust: return (1f + 0.14f * bell, 1f - 0.05f * bell);
                case StrikeSystem.Strike: return (1f + 0.12f * bell, 1f - 0.08f * bell);
                case StrikeSystem.Shield: return (1f, 1f + 0.14f * bell);
                default: return (1f, 1f);
            }
        }
    }

    /// <summary>
    /// The fall (EffectId.Defeat, battle_ui_ux_v2 §2.2 の 6): the figure tilts back from the other side
    /// and sinks while it darkens, then fades away. <c>t</c> runs 0 → 1 over the whole length.
    /// </summary>
    public static class DefeatShape
    {
        /// <summary>Where the fade starts: the tilt and the sink take the time before it.</summary>
        public const float FadeFrom = 0.6f;

        public const float TiltDegrees = 24f;
        public const float SinkPixels = 40f;

        /// <summary>How dark the body ends: its colour is multiplied by this.</summary>
        public const float DarkTo = 0.35f;

        /// <summary>
        /// The tilt in degrees about the feet. The enemy faces left and falls back to the right
        /// (clockwise, negative); the player the other way.
        /// </summary>
        public static float Tilt(UnitSide side, float t)
        {
            float away = side == UnitSide.Enemy ? -1f : 1f;
            return away * TiltDegrees * Fall(t);
        }

        public static float Sink(float t) => -SinkPixels * Fall(t);

        public static float Brightness(float t) => 1f - (1f - DarkTo) * Fall(t);

        public static float Alpha(float t)
        {
            if (t <= FadeFrom) return 1f;
            return Math.Max(0f, 1f - (t - FadeFrom) / (1f - FadeFrom));
        }

        private static float Fall(float t)
        {
            float u = Math.Min(1f, Math.Max(0f, t / FadeFrom));
            return u * u; // eases in: the body gives way, then drops
        }
    }
}
