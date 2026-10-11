// The figure's art and the shapes its body takes (#288). Pure C#, like the rest of Script/: the View
// asks these which picture to wear, which way the body faces and how far a move stretches it, so
// `dotnet test` holds the same numbers the screen plays.
using System;

namespace Depiction
{
    /// <summary>
    /// The four pictures a figure can wear. Only <see cref="Idle"/> is required; a pose with no picture
    /// shows the idle one, and the motion alone carries the moment.
    /// battle_ui_ux_v2 §6.4 lists the same four. 崩れ (the stagger of §5.14 順 2) has no picture of its
    /// own (#309): the script carries no beat for the moment of a break, so the stagger is motion only.
    /// A collapse pose comes with that beat.
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
    /// How wide a figure's frame is for the art it wears (#302). The body is drawn with preserveAspect
    /// in a frame of fixed height whose bottom edge is the feet; art wider than the frame's rest shape
    /// would be shrunk to fit the width, and Unity hands the lost height out half above, half below,
    /// so the figure would float off the floor line. The frame is widened instead, to the art's own
    /// shape at the frame's height; art no wider than the rest shape (the 640 x 1536 pictures in a
    /// 210 x 450 frame) keeps the rest frame, so it looks exactly as before.
    /// </summary>
    public static class FigureFrame
    {
        /// <summary>
        /// The frame's width for one picture of <paramref name="artWidth"/> x <paramref name="artHeight"/>
        /// pixels: <c>height x artWidth / artHeight</c>, never narrower than <paramref name="restWidth"/>.
        /// A picture with no size (or no picture) keeps the rest width.
        /// </summary>
        public static float Width(float restWidth, float height, float artWidth, float artHeight)
        {
            if (artWidth <= 0f || artHeight <= 0f || height <= 0f) return restWidth;
            return Math.Max(restWidth, height * artWidth / artHeight);
        }

        /// <summary>
        /// How high above the frame's bottom edge a preserveAspect picture's feet are drawn, with the
        /// body's pivot at the centre (Unity's Image splits the unused height evenly). Zero means the
        /// picture stands on the frame's bottom edge, which is the floor line.
        /// </summary>
        public static float Lift(float frameWidth, float frameHeight, float artWidth, float artHeight)
        {
            if (artWidth <= 0f || artHeight <= 0f || frameWidth <= 0f || frameHeight <= 0f) return 0f;
            float drawnHeight = Math.Min(frameHeight, frameWidth * artHeight / artWidth);
            return (frameHeight - drawnHeight) / 2f;
        }

        // ---- battle-visual-v1 §4.3 人型 (#455) ----------------------------------------------------

        /// <summary>
        /// §4.3: a figure stands 226 px tall on the 1920×1080 screen, feet on y 570 — about 75% of the
        /// 300 px battle_ui_ux_v2 drew it at. The Unity frame is 450 tall, so it is drawn at 226 / 450.
        /// </summary>
        public const float StandardHeight = 226f;

        /// <summary>§4.3: an elite that takes two cells stands 264 px tall.</summary>
        public const float LargeHeight = 264f;

        /// <summary>§4.3 足元の灯り: an ellipse under the feet, 60 px tall and twice as wide as the figure.</summary>
        public const float FootLightHeight = 60f;

        public const float FootLightWidthRatio = 2f;

        /// <summary>How tall a figure that takes <paramref name="cells"/> cells stands on the screen.</summary>
        public static float HeightFor(int cells)
        {
            return cells >= 2 ? LargeHeight : StandardHeight;
        }

        /// <summary>
        /// The scale that draws a frame <paramref name="frameHeight"/> tall at <paramref name="height"/>
        /// screen pixels. A frame with no height is drawn as it is.
        /// </summary>
        public static float ScaleFor(float height, float frameHeight)
        {
            if (height <= 0f || frameHeight <= 0f) return 1f;
            return height / frameHeight;
        }

        /// <summary>
        /// The scale a child takes so its own sizes stay in screen pixels under a parent drawn at
        /// <paramref name="parentScale"/> (the target brackets, the foot light's height). A parent
        /// with no usable scale leaves the child as it is.
        /// </summary>
        public static float Counter(float parentScale)
        {
            float scale = Math.Abs(parentScale);
            return scale < 0.01f ? 1f : 1f / scale;
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
