// The mode screen and the end screen of the demo (#191), built in code with UiKit on the demo
// canvas. They lay out words and buttons only: the enemies offered, the chain, the end screen's
// title, lines and labels all come from Depiction.Bridge.DemoSession.
#if UNITY_2021_2_OR_NEWER
using System;
using BattleCore;
using Depiction.Bridge;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    /// <summary>「1 体で区切る」 (one of the eleven, or random) or 「連戦」 (§12's three).</summary>
    public sealed class ModeSelectScreen
    {
        private readonly RectTransform _root;

        public ModeSelectScreen(RectTransform parent, Action<string> single, Action random, Action chain, Action back)
        {
            _root = UiKit.Box(parent, "ModeSelect", 0f, 0f, 1f, 1f);
            Image backdrop = UiKit.Fill(_root, "Back", BattleTheme.WithAlpha(BattleTheme.Ground, 0.97f));
            backdrop.raycastTarget = true;

            UiKit.Label(_root, "Title", 40, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(1200f, 60f), new Vector2(40f, -20f), DemoSession.ModeTitle);

            UiKit.Label(_root, "SingleHead", 30, TextAnchor.MiddleLeft, BattleTheme.Warm, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(1200f, 50f), new Vector2(40f, -100f), DemoSession.SingleHeading);
            RectTransform grid = UiKit.Box(_root, "Enemies", 0.02f, 0.36f, 0.98f, 0.84f);
            const int columns = 3;
            int count = Enemies.All.Count + 1;
            int rows = (count + columns - 1) / columns;
            for (int i = 0; i < count; i++)
            {
                float x0 = (i % columns) / (float)columns;
                float y1 = 1f - (i / columns) / (float)rows;
                var min = new Vector2(x0 + 0.004f, y1 - 1f / rows + 0.01f);
                var max = new Vector2(x0 + 1f / columns - 0.004f, y1 - 0.01f);
                if (i < Enemies.All.Count)
                {
                    EnemyDef enemy = Enemies.All[i];
                    UiKit.Button(grid, "Enemy" + i, DemoSession.EnemyLine(enemy), () => single(enemy.Id), BattleTheme.Panel, BattleTheme.Ink, 26, min, max);
                }
                else
                {
                    UiKit.Button(grid, "Random", DemoSession.RandomLabel, () => random(), BattleTheme.Panel, BattleTheme.Warm, 26, min, max);
                }
            }

            UiKit.Label(_root, "ChainHead", 30, TextAnchor.MiddleLeft, BattleTheme.Warm, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(1200f, 50f), new Vector2(40f, 250f), DemoSession.ChainHeading);
            UiKit.Button(_root, "Chain", DemoSession.ChainLine(), () => chain(), BattleTheme.Accent, BattleTheme.InkBlack, 28,
                new Vector2(0.02f, 0.13f), new Vector2(0.98f, 0.22f));
            UiKit.Button(_root, "Back", DemoSession.BackToDeckLabel, () => back(), BattleTheme.Panel, BattleTheme.Ink, 26,
                new Vector2(0.02f, 0.02f), new Vector2(0.3f, 0.09f));
        }

        public bool Visible
        {
            get { return _root.gameObject.activeSelf; }
            set { _root.gameObject.SetActive(value); }
        }
    }

    /// <summary>§12's result: who won, which battle, the HP left, what was played, and the ways on.</summary>
    public sealed class EndScreen
    {
        private readonly RectTransform _root;
        private readonly Text _title;
        private readonly Text _lines;
        private readonly RectTransform _buttons;
        private readonly Action<bool> _goOn;
        private readonly Action _again;
        private readonly Action _back;

        public EndScreen(RectTransform parent, Action<bool> goOn, Action again, Action back)
        {
            _goOn = goOn;
            _again = again;
            _back = back;
            _root = UiKit.Box(parent, "EndScreen", 0f, 0f, 1f, 1f);
            Image backdrop = UiKit.Fill(_root, "Back", BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.82f));
            backdrop.raycastTarget = true;
            RectTransform panel = UiKit.Panel(_root, "Panel", 0.22f, 0.12f, 0.78f, 0.88f).rectTransform;
            _title = UiKit.Text(UiKit.Box(panel, "Title", 0f, 0.82f, 1f, 1f), "Text", 48, TextAnchor.MiddleCenter, BattleTheme.Warm);
            _title.fontStyle = FontStyle.Bold;
            _lines = UiKit.Text(UiKit.Box(panel, "Lines", 0.05f, 0.34f, 0.95f, 0.82f), "Text", 28, TextAnchor.UpperLeft, BattleTheme.Ink);
            _buttons = UiKit.Box(panel, "Buttons", 0.05f, 0.03f, 0.95f, 0.32f);
            Visible = false;
        }

        public bool Visible
        {
            get { return _root.gameObject.activeSelf; }
            set { _root.gameObject.SetActive(value); }
        }

        public void Show(DemoEndScreen screen)
        {
            _title.text = screen.Title;
            _title.color = screen.Won ? BattleTheme.Warm : BattleTheme.Omen;
            _lines.text = string.Join("\n", screen.Lines);

            UiKit.ClearChildren(_buttons);
            if (screen.CanGoOn)
            {
                UiKit.Button(_buttons, "Rest", screen.RestLabel, () => _goOn(true), BattleTheme.Accent, BattleTheme.InkBlack, 26,
                    new Vector2(0f, 0.52f), new Vector2(0.49f, 1f));
                UiKit.Button(_buttons, "GoOn", screen.GoOnLabel, () => _goOn(false), BattleTheme.Panel, BattleTheme.Ink, 26,
                    new Vector2(0.51f, 0.52f), new Vector2(1f, 1f));
            }
            UiKit.Button(_buttons, "Again", screen.AgainLabel, () => _again(), BattleTheme.Panel, BattleTheme.Ink, 26,
                new Vector2(0f, 0f), new Vector2(0.49f, 0.44f));
            UiKit.Button(_buttons, "BackToDeck", screen.BackLabel, () => _back(), BattleTheme.Panel, BattleTheme.Ink, 26,
                new Vector2(0.51f, 0f), new Vector2(1f, 0.44f));
            Visible = true;
        }
    }
}
#endif
