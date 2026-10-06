// The provisional battle-speed switch (#348), built in code with UiKit on the battle canvas, the same
// shape as the demo's SurrenderButton. Where it sits is a stand-in: battle-visual-v1 §4 gives the
// top-right corner to the journal button (24 from the right, y 20, 56 x 56), and the demo's 「降参する」
// borrows that corner (y 16-72), so this sits just under it at y 84-140, x 1696-1896 on the
// 1920 x 1080 canvas. The final place and look are the design lane's to decide. The words come from
// BattleSpeed.Label; the button computes nothing.
#if UNITY_2021_2_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    /// <summary>One press moves the battle speed on: 1.0 → 1.25 → 1.5 → 1.0 times.</summary>
    public sealed class BattleSpeedButton
    {
        private readonly GameObject _button;
        private readonly Text _label;

        public BattleSpeedButton(RectTransform parent, Action cycle)
        {
            Button button = UiKit.Button(parent, "BattleSpeed", "", () => cycle(), BattleTheme.Panel, BattleTheme.Ink, 24,
                new Vector2(1f, 1f), new Vector2(1f, 1f));
            var rt = (RectTransform)button.transform;
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(200f, 56f);
            rt.anchoredPosition = new Vector2(-24f, -84f);
            _label = button.GetComponentInChildren<Text>();
            _button = button.gameObject;
        }

        /// <summary>Writes the speed in force on the button (BattleSpeed.Label).</summary>
        public void Show(string label)
        {
            if (_label) _label.text = label;
        }

        public bool Visible
        {
            get { return _button.activeSelf; }
            set { _button.SetActive(value); }
        }
    }
}
#endif
