// Whether a held single-target card is over the receiver (battle_ui_ux_v2 §3.2, #568). Pure arithmetic
// with no engine types, like CornerLayout, so the EditMode tests can hold it directly. The View hands in
// the boxes on the 1920 x 1080 reference canvas (ScreenBox, y counted downwards): the card where the
// pointer carries it, before the snap draws it in, and the receiver's invisible square. The hot light,
// the snap's pull and the release all ask this one function, so what lights is what plays.
using System;
using System.Collections.Generic;

namespace Depiction
{
    public static class ReceiverDrop
    {
        /// <summary>The share of the card's area that must lie on the receiver: half or more.</summary>
        public const float TakeShare = 0.5f;

        /// <summary>The share of <paramref name="card"/>'s area that lies inside <paramref name="receiver"/>, from 0 to 1. A card with no area has none.</summary>
        public static float Share(ScreenBox card, ScreenBox receiver)
        {
            float area = card.Width * card.Height;
            if (area <= 0f) return 0f;
            float width = Math.Min(card.Right, receiver.Right) - Math.Max(card.X, receiver.X);
            float height = Math.Min(card.Bottom, receiver.Bottom) - Math.Max(card.Y, receiver.Y);
            if (width <= 0f || height <= 0f) return 0f;
            return Math.Min(1f, width * height / area);
        }

        /// <summary>
        /// True when the receiver takes the card: half or more of the card lies on it, or the pointer is
        /// inside it (the drop that worked before #568 still works). The pointer counts on the left and
        /// top edges and not on the right and bottom ones, as Unity's Rect.Contains does.
        /// </summary>
        public static bool Takes(ScreenBox card, ScreenBox receiver, float pointerX, float pointerY)
        {
            if (Share(card, receiver) >= TakeShare) return true;
            return Inside(receiver, pointerX, pointerY);
        }

        /// <summary>
        /// battle_ui_ux_v2 §3.9 (#589): which of several receivers takes the card, or −1. Neighbours' receivers
        /// overlap (a receiver is wider than a cell), so the choice stays one: among those the pointer is
        /// inside, the one whose middle is nearest it; with the pointer inside none, the one holding the most
        /// of the card, if that is half or more. The first given wins a tie.
        /// </summary>
        public static int Pick(ScreenBox card, IReadOnlyList<ScreenBox> receivers, float pointerX, float pointerY)
        {
            if (receivers == null) throw new ArgumentNullException(nameof(receivers));
            int nearest = -1;
            float nearestBy = float.MaxValue;
            int fullest = -1;
            float fullestBy = 0f;
            for (int i = 0; i < receivers.Count; i++)
            {
                ScreenBox receiver = receivers[i];
                if (Inside(receiver, pointerX, pointerY))
                {
                    float by = Math.Abs(receiver.X + receiver.Width * 0.5f - pointerX);
                    if (by < nearestBy)
                    {
                        nearest = i;
                        nearestBy = by;
                    }
                }
                float share = Share(card, receiver);
                if (share >= TakeShare && share > fullestBy)
                {
                    fullest = i;
                    fullestBy = share;
                }
            }
            return nearest >= 0 ? nearest : fullest;
        }

        /// <summary>The pointer counts on the left and top edges and not on the right and bottom ones, as Unity's Rect.Contains does.</summary>
        private static bool Inside(ScreenBox receiver, float pointerX, float pointerY)
        {
            return pointerX >= receiver.X && pointerX < receiver.Right && pointerY >= receiver.Y && pointerY < receiver.Bottom;
        }
    }
}
