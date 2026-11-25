// Where a standing picture is laid over a figure (#772).
//
// The pictures are not cut alike: the enemies' are painted down to their bottom edge, but the swordsman's has 180 px of
// air over the head and about 100 px under the soles. Scaling a whole picture to the figure's height would leave the
// swordsman a fifth smaller and floating, so each picture names its own head top, sole row and body centre, and the
// soles are put on the figure's feet line (Koudai-san, 2026-11-25: the margins are absorbed by the soles, not cropped
// from the picture). A picture without marks is read as head at its top, soles at its bottom, body in its middle.
using System;
using System.Collections.Generic;

namespace Trial.Battle
{
    /// <summary>A picture's rect in the figure's own box: stage px, top-left origin, y down.</summary>
    public readonly struct ArtRect
    {
        public readonly float X, Y, W, H;

        public ArtRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

        public override string ToString() => "(" + X + ", " + Y + ", " + W + ", " + H + ")";
    }

    /// <summary>A picture's head top, sole row and body centre x, in the px of the picture they were measured on.</summary>
    public readonly struct ArtMarks
    {
        public readonly float Top, Sole, CenterX;
        /// <summary>The size of the picture the marks were measured on (0 = the marks are read on any size as they are).</summary>
        public readonly float PictureW, PictureH;

        public ArtMarks(float top, float sole, float centerX, float pictureW, float pictureH)
        {
            Top = top;
            Sole = sole;
            CenterX = centerX;
            PictureW = pictureW;
            PictureH = pictureH;
        }
    }

    public static class ArtStand
    {
        // Measured on the pictures in Assets/Art/Characters/<id>/chr_<id>_idle_stand.png. Kept with the measured size
        // so a picture imported smaller (a lower max size) still stands on its soles.
        private static readonly Dictionary<string, ArtMarks> Marks = new Dictionary<string, ArtMarks>(StringComparer.Ordinal)
        {
            ["swordsman"] = new ArtMarks(180f, 1434f, 274f, 640f, 1536f),
        };

        /// <summary>The marks of `id`'s picture at `pictureW` × `pictureH` px; a picture without marks spans its whole height.</summary>
        public static ArtMarks MarksOf(string id, float pictureW, float pictureH)
        {
            if (id != null && Marks.TryGetValue(id, out ArtMarks m) && m.PictureW > 0f && m.PictureH > 0f)
            {
                float sx = pictureW / m.PictureW, sy = pictureH / m.PictureH;
                return new ArtMarks(m.Top * sy, m.Sole * sy, m.CenterX * sx, pictureW, pictureH);
            }
            return new ArtMarks(0f, pictureH, pictureW / 2f, pictureW, pictureH);
        }

        /// <summary>
        /// The rect that lays `id`'s picture (`pictureW` × `pictureH` px) over a figure `height` px tall whose feet stand at
        /// `footX` of its box: head top to soles is `height`, the soles on the box's bottom edge, the body centre on footX.
        /// The picture keeps its aspect; what lies above the head or below the soles reaches out of the box.
        /// </summary>
        public static ArtRect Place(string id, float pictureW, float pictureH, float height, float footX)
        {
            ArtMarks m = MarksOf(id, pictureW, pictureH);
            float span = m.Sole - m.Top;
            // Marks that name no body (sole at or above the head) cannot be stood on: the whole picture is the body.
            if (!(span > 0f)) m = new ArtMarks(0f, pictureH, pictureW / 2f, pictureW, pictureH);
            span = m.Sole - m.Top;
            float k = span > 0f ? height / span : 0f;
            return new ArtRect(footX - m.CenterX * k, height - m.Sole * k, pictureW * k, pictureH * k);
        }
    }
}
