// Port of unity-project-kit/Assets/View/Depiction/Script/ReceiverDrop.cs
// Whether a held single-target card is over the receiver (battle_ui_ux_v2 §3.2, #568). Pure arithmetic
// with no engine types, like CornerLayout, so the EditMode tests can hold it directly. The View hands in
// the boxes on the 1920 x 1080 reference canvas (ScreenBox, y counted downwards): the card where the
// pointer carries it, before the snap draws it in, and the receiver's invisible square. The hot light,
// the snap's pull and the release all ask this one function, so what lights is what plays.
//
// Port note: every C# `float` is a single (Math.fround on each float operation; the pointer's
// coordinates are rounded on entry, as the C# float parameters would be).
const f = Math.fround;

export const ReceiverDrop = {
  /** The share of the card's area that must lie on the receiver: half or more. */
  TakeShare: f(0.5),

  /** The share of `card`'s area that lies inside `receiver`, from 0 to 1. A card with no area has none. (ScreenBox, ScreenBox) */
  Share(card, receiver) {
    const area = f(card.Width * card.Height);
    if (area <= 0) return f(0);
    const width = f(Math.min(card.Right, receiver.Right) - Math.max(card.X, receiver.X));
    const height = f(Math.min(card.Bottom, receiver.Bottom) - Math.max(card.Y, receiver.Y));
    if (width <= 0 || height <= 0) return f(0);
    return Math.min(f(1), f(f(width * height) / area));
  },

  /**
   * True when the receiver takes the card: half or more of the card lies on it, or the pointer is
   * inside it (the drop that worked before #568 still works). The pointer counts on the left and
   * top edges and not on the right and bottom ones, as Unity's Rect.Contains does.
   */
  Takes(card, receiver, pointerX, pointerY) {
    pointerX = f(pointerX);
    pointerY = f(pointerY);
    if (ReceiverDrop.Share(card, receiver) >= ReceiverDrop.TakeShare) return true;
    return pointerX >= receiver.X && pointerX < receiver.Right && pointerY >= receiver.Y && pointerY < receiver.Bottom;
  },
};
