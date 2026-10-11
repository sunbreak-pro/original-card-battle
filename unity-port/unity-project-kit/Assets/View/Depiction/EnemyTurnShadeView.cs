// The enemy's turn below the throw line (battle-visual-v1 §5.5, #455): from y 790 down the screen
// sinks under a black gradient, clear at the top and 72% at the bottom edge, across the whole width.
// It lies over the hand, the stamina tag, the piles and the lower right; the speed switch at the top
// right stays as it is (§4.1). Whether it is the enemy's turn is the script's (DepictionFrame.EnemyTurn).
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class EnemyTurnShadeView
    {
        private readonly RectTransform _root;

        public EnemyTurnShadeView(RectTransform canvasRoot, int siblingIndex)
        {
            // Stretched along the bottom edge: the canvas's full width, 290 px tall (y 790 to 1080),
            // the way RefScreen keeps the lower parts on the bottom edge of a screen of another shape.
            _root = UiKit.Rect(canvasRoot, "EnemyTurnShade", new Vector2(0f, 0f), new Vector2(1f, 0f));
            _root.pivot = new Vector2(0.5f, 0f);
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = new Vector2(0f, EnemyTurnShadeLayout.Height);
            if (siblingIndex >= 0) _root.SetSiblingIndex(siblingIndex);
            CanvasGroup group = UiKit.Group(_root);
            group.blocksRaycasts = false;
            group.interactable = false;
            Sprite gradient = ProceduralArt.VerticalGradient(
                new Color(0f, 0f, 0f, EnemyTurnShadeLayout.TopAlpha),
                new Color(0f, 0f, 0f, EnemyTurnShadeLayout.BottomAlpha));
            UiKit.Image(_root, "Gradient", gradient, Color.white, Vector2.zero, Vector2.one);
            _root.gameObject.SetActive(false);
        }

        public bool Shown => _root && _root.gameObject.activeSelf;

        public void Show(bool on)
        {
            if (_root) _root.gameObject.SetActive(on);
        }
    }
}
#endif
