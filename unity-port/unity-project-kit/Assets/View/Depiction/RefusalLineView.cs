// The reason a released card went back to the hand (battle-visual-v1 §5.4, #455): one line above the
// hand, 48 px tall, on an opaque panel with a steel frame, an amber 16 px dot on its left and the text
// at 20 px in body ink. The words are the source's (IDepictionSource.RefusalText); this only places
// them. Made at run time on the canvas, like the other #455 parts.
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class RefusalLineView
    {
        private readonly RectTransform _canvas;
        private readonly RectTransform _root;
        private readonly Text _text;

        public RefusalLineView(RectTransform canvasRoot, int siblingIndex)
        {
            UiKit.EnsureFont();
            _canvas = canvasRoot;
            var size = new Vector2(RefusalLineLayout.WidthFor(0f), RefusalLineLayout.Height);
            _root = UiKit.Point(canvasRoot, "RefusalLine", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size, Vector2.zero);
            if (siblingIndex >= 0) _root.SetSiblingIndex(siblingIndex);
            CanvasGroup group = UiKit.Group(_root);
            group.blocksRaycasts = false;
            group.interactable = false;
            // 不透明パネル, 鈍鋼の枠 (1 px), 6 px corners.
            VisualArt.Panel(_root, "Back", BattleTheme.PanelOpaque, 6);
            VisualArt.Ring(_root, "Edge", BattleTheme.Steel, 6, 1);
            UiKit.Sprite(_root, "Dot", ProceduralArt.Circle, BattleTheme.Amber, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(RefusalLineLayout.DotSize, RefusalLineLayout.DotSize), new Vector2(RefusalLineLayout.Pad, 0f));
            _text = UiKit.Label(_root, "Text", RefusalLineLayout.TextSize, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, RefusalLineLayout.Height), new Vector2(RefusalLineLayout.Pad + RefusalLineLayout.DotSize + RefusalLineLayout.DotGap, 0f));
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _root.gameObject.SetActive(false);
        }

        public bool Shown => _root && _root.gameObject.activeSelf;

        /// <summary>Puts <paramref name="text"/> up on one line, sized to it and centred over the hand.</summary>
        public void Show(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }
            _root.gameObject.SetActive(true);
            _text.text = text;
            float width = _text.preferredWidth;
            _text.rectTransform.sizeDelta = new Vector2(width, RefusalLineLayout.Height);
            var size = new Vector2(RefusalLineLayout.WidthFor(width), RefusalLineLayout.Height);
            RefScreen.Place(_root, _canvas, new Vector2(RefusalLineLayout.LeftFor(width), RefusalLineLayout.Top), size);
        }

        public void Hide()
        {
            if (_root) _root.gameObject.SetActive(false); // the canvas may already be gone (OnDisable at teardown)
        }
    }
}
#endif
