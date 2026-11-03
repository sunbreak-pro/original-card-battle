// Where a layer's painted backdrop is looked for and how a screen lays it (battle-visual-v1 §2.4, #858). The old battle
// screen (BackdropLayers) and the trial's battle, exploration and meta screens all read the art from this one path, so a
// picture dropped into Resources reaches every screen at once. Pure C#, so `dotnet test` holds the rules the views follow.
namespace Backdrop
{
    /// <summary>The part of a picture a screen shows, in fractions of the picture with v from its bottom (as uGUI's uvRect).</summary>
    public readonly struct ArtCrop
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;

        public ArtCrop(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        /// <summary>The whole picture.</summary>
        public static ArtCrop Whole => new ArtCrop(0f, 0f, 1f, 1f);
    }

    public static class BackdropArt
    {
        /// <summary>The art of layer n is the Sprite at Resources/Backdrops/layer-n (n = 1〜7).</summary>
        public const string ResourcePrefix = "Backdrops/layer-";

        /// <summary>Whether <paramref name="layer"/> has a place for art: the seven layers do; the hall's room (layer 0) is not a layer.</summary>
        public static bool HasSlot(int layer) => layer >= 1 && layer <= BackdropSpec.Layers;

        /// <summary>The Resources path of <paramref name="layer"/>'s art, or null for a layer without a slot (never clamped: a wrong layer shows no art rather than another layer's).</summary>
        public static string ResourcePath(int layer) =>
            HasSlot(layer) ? ResourcePrefix + layer.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

        /// <summary>
        /// Whether the code-drawn far view (BackdropScenery) is drawn: only while the layer has no art. The art is one opaque
        /// picture of the far view and the ground, so drawing both would stack two far views.
        /// </summary>
        public static bool DrawsFarView(bool hasArt) => !hasArt;

        /// <summary>
        /// The crop that fills a <paramref name="boxW"/> × <paramref name="boxH"/> box with an
        /// <paramref name="artW"/> × <paramref name="artH"/> picture without stretching it (CSS object-fit: cover, centred):
        /// a picture wider than the box loses its sides, a taller one its top and bottom. A 1920×1080 picture fills the
        /// stage whole; the exploration's sky strip shows its middle band. A size that is not positive shows the whole picture.
        /// </summary>
        public static ArtCrop Cover(float artW, float artH, float boxW, float boxH)
        {
            if (!(artW > 0f) || !(artH > 0f) || !(boxW > 0f) || !(boxH > 0f)) return ArtCrop.Whole;
            float art = artW / artH;
            float box = boxW / boxH;
            if (art > box)
            {
                float w = box / art;
                return new ArtCrop((1f - w) / 2f, 0f, w, 1f);
            }
            if (art < box)
            {
                float h = art / box;
                return new ArtCrop(0f, (1f - h) / 2f, 1f, h);
            }
            return ArtCrop.Whole;
        }
    }
}
