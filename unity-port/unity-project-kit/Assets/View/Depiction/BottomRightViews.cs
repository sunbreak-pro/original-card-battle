// The lower right of the battle screen (battle-visual-v1 §4.9, #455), made at run time on the canvas
// like the piles, since the scene belongs to the Unity project:
//
// - PlayedAttributesView: four 40×40 frames, 6 px apart, 24 px in from the right at y 896, for
//   攻撃・防御・スキル・スタンス. An attribute played this turn lights its icon in the attribute's
//   colour and carries a 20 px count badge at its top right; one not played is steel at 55%. The
//   attribute of the card played last is underlined (battle_ui_ux_v2 §1.1 B6).
// - ReserveHintView: the 構えの予告 at x 1560, y 984, 56 tall — a Guard-coloured 1 px dashed frame,
//   a 30 px shield and "+3" at 24 px.
//
// Both draw what the script hands them (DepictionFrame.Played, DepictionFrame.StanceHint and a held
// card's CardFace.StanceHintAfterPay); neither counts nor judges.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class PlayedAttributesView
    {
        private sealed class Slot
        {
            public CardKind Kind;
            public CanvasGroup Group;
            public Image Icon;
            public RectTransform Badge;
            public Image BadgeEdge;
            public Text BadgeText;
            public Image Underline;
        }

        private readonly RectTransform _root;
        private readonly List<Slot> _slots = new List<Slot>();

        public PlayedAttributesView(RectTransform canvasRoot, int siblingIndex)
        {
            UiKit.EnsureFont();
            _root = UiKit.Rect(canvasRoot, "PlayedAttributes", Vector2.zero, Vector2.one);
            if (siblingIndex >= 0) _root.SetSiblingIndex(siblingIndex);
            CanvasGroup group = UiKit.Group(_root);
            group.blocksRaycasts = false;
            group.interactable = false;
            for (int i = 0; i < PlayedLayout.Order.Length; i++) _slots.Add(MakeSlot(canvasRoot, i));
            _root.gameObject.SetActive(false);
        }

        public void Bind(PlayedFrame played)
        {
            bool shown = played != null && played.Shown;
            _root.gameObject.SetActive(shown);
            if (!shown) return;
            foreach (Slot slot in _slots)
            {
                int count = played.CountOf(slot.Kind);
                bool lit = count > 0;
                slot.Group.alpha = lit ? 1f : PlayedLayout.UnplayedAlpha;
                slot.Icon.color = lit ? BattleTheme.KindInk(slot.Kind) : BattleTheme.Steel;
                slot.Badge.gameObject.SetActive(lit);
                slot.BadgeEdge.color = BattleTheme.KindFrame(slot.Kind);
                slot.BadgeText.text = count.ToString();
                slot.Underline.enabled = lit && played.Last == slot.Kind;
            }
        }

        private Slot MakeSlot(RectTransform canvasRoot, int index)
        {
            var slot = new Slot { Kind = PlayedLayout.Order[index] };
            var size = new Vector2(PlayedLayout.Size, PlayedLayout.Size);
            RectTransform root = UiKit.Point(_root, "Played" + slot.Kind, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size, Vector2.zero);
            RefScreen.Place(root, canvasRoot, new Vector2(PlayedLayout.LeftOf(index), PlayedLayout.Top), size);
            slot.Group = UiKit.Group(root);
            // パネル (88%), 罫線 1 px, 6 px corners (§3.2).
            VisualArt.Panel(root, "Back", BattleTheme.Panel, 6);
            VisualArt.Ring(root, "Edge", BattleTheme.Line, 6, 1);
            slot.Icon = UiKit.Sprite(root, "Icon", VisualArt.KindIcon(slot.Kind), BattleTheme.Steel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(PlayedLayout.IconSize, PlayedLayout.IconSize), Vector2.zero);
            slot.Icon.preserveAspect = true;

            // 回数の札: a 20 px circle on the top-right corner, the number at 14 px in body ink (§2.3: under 24 px).
            var badgeSize = new Vector2(PlayedLayout.BadgeSize, PlayedLayout.BadgeSize);
            slot.Badge = UiKit.Point(root, "Count", new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), badgeSize, new Vector2(-2f, -2f));
            UiKit.Sprite(slot.Badge, "Fill", ProceduralArt.Circle, BattleTheme.PanelOpaque, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), badgeSize, Vector2.zero);
            slot.BadgeEdge = UiKit.Sprite(slot.Badge, "Edge", VisualArt.RoundedRing(Mathf.RoundToInt(PlayedLayout.BadgeSize * 0.5f), 1), BattleTheme.Line,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), badgeSize, Vector2.zero);
            slot.BadgeEdge.type = Image.Type.Sliced;
            slot.BadgeText = UiKit.Label(slot.Badge, "Text", PlayedLayout.BadgeText, TextAnchor.MiddleCenter, BattleTheme.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                badgeSize, Vector2.zero);
            slot.BadgeText.fontStyle = FontStyle.Bold;
            slot.BadgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            slot.Badge.gameObject.SetActive(false);

            // B6 直前のカードに下線: 2 px (§3.2 枠) in the attribute's colour, 4 px under the frame.
            slot.Underline = UiKit.Sprite(root, "Last", ProceduralArt.White, BattleTheme.KindFrame(slot.Kind), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(PlayedLayout.Size - 8f, 2f), new Vector2(0f, -4f));
            slot.Underline.enabled = false;
            return slot;
        }
    }

    public sealed class ReserveHintView
    {
        private readonly RectTransform _root;
        private readonly Text _text;

        public ReserveHintView(RectTransform canvasRoot, int siblingIndex)
        {
            UiKit.EnsureFont();
            var size = new Vector2(ReserveHintLayout.Width, ReserveHintLayout.Height);
            _root = UiKit.Point(canvasRoot, "ReserveHint", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size, Vector2.zero);
            if (siblingIndex >= 0) _root.SetSiblingIndex(siblingIndex);
            RefScreen.Place(_root, canvasRoot, new Vector2(ReserveHintLayout.Left, ReserveHintLayout.Top), size);
            CanvasGroup group = UiKit.Group(_root);
            group.blocksRaycasts = false;
            group.interactable = false;
            // §2.1: numbers stand on an 88% panel; §4.9: a Guard-coloured 1 px dashed frame.
            VisualArt.Panel(_root, "Back", BattleTheme.Panel, 6);
            VisualArt.DashedFrame(_root, BattleTheme.Guard, 1f);
            Image shield = UiKit.Sprite(_root, "Shield", ProceduralArt.Shield, BattleTheme.Guard, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ReserveHintLayout.ShieldSize, ReserveHintLayout.ShieldSize), new Vector2(ReserveHintLayout.Pad, 0f));
            shield.preserveAspect = true;
            _text = UiKit.Label(_root, "Text", ReserveHintLayout.TextSize, TextAnchor.MiddleLeft, BattleTheme.Guard, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ReserveHintLayout.TextWidth, ReserveHintLayout.Height),
                new Vector2(ReserveHintLayout.Pad + ReserveHintLayout.ShieldSize + ReserveHintLayout.TextGap, 0f));
            _text.fontStyle = FontStyle.Bold;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _root.gameObject.SetActive(false);
        }

        /// <summary>Shows "+3" with its shield, or nothing for "" (構え would not hold, or it is the enemy's turn).</summary>
        public void Show(string hint)
        {
            bool on = !string.IsNullOrEmpty(hint);
            _root.gameObject.SetActive(on);
            if (on) _text.text = hint;
        }
    }
}
#endif
