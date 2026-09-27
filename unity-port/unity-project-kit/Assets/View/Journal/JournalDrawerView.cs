// The journal drawer: a panel from the right, 30% wide, that reads a JournalBook (dungeon_exploration_v4.md
// §6.7, battle_ui_ux_v2.md 手記ドロワー). It decides nothing: which tabs open, which rows read 「？」 and
// every word come from Journal.Script. Built in code with UiKit, so no scene or prefab changes.
//
// The look is a stand-in — flat grey paper, the built-in font, no slide. The paper colour, the fonts
// and the 220 ms slide are the design lane's to set; they replace this file's colours and nothing else.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Journal.View
{
    public sealed class JournalDrawerView
    {
        // Stand-in colours only (see the header).
        private static readonly Color Paper = new Color(0.86f, 0.85f, 0.82f, 1f);
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color InkSoft = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color Tab = new Color(0.74f, 0.73f, 0.70f, 1f);
        private static readonly Color TabOn = new Color(0.97f, 0.96f, 0.93f, 1f);

        private readonly RectTransform _root;
        private readonly RectTransform _tabs;
        private readonly Text _title;
        private readonly Text _disclosure;
        private readonly Text _body;
        private readonly Text _hint;

        private JournalBook _book;
        private string _pageId;

        /// <param name="parent">A full-screen rect on the screen's canvas. The drawer takes its right 30%.</param>
        public JournalDrawerView(RectTransform parent)
        {
            _root = UiKit.Box(parent, "JournalDrawer", 0.70f, 0f, 1f, 1f);
            Image back = UiKit.Fill(_root, "Paper", Paper);
            back.raycastTarget = true;

            RectTransform head = UiKit.Box(_root, "Head", 0f, 0.92f, 1f, 1f);
            UiKit.Text(UiKit.Box(head, "Name", 0.03f, 0f, 0.4f, 1f), "Text", 30, TextAnchor.MiddleLeft, Ink, JournalBook.Heading);
            _hint = UiKit.Text(UiKit.Box(head, "Hint", 0.4f, 0f, 0.76f, 1f), "Text", 18, TextAnchor.MiddleRight, InkSoft);
            UiKit.Button(head, "Close", JournalBook.CloseLabel, Close, Tab, Ink, 20, new Vector2(0.78f, 0.15f), new Vector2(0.98f, 0.85f));

            _tabs = UiKit.Box(_root, "Tabs", 0.02f, 0.85f, 0.98f, 0.91f);
            _title = UiKit.Text(UiKit.Box(_root, "Title", 0.03f, 0.78f, 0.75f, 0.84f), "Text", 28, TextAnchor.MiddleLeft, Ink);
            _disclosure = UiKit.Text(UiKit.Box(_root, "Disclosure", 0.75f, 0.78f, 0.97f, 0.84f), "Text", 20, TextAnchor.MiddleRight, InkSoft);
            _body = UiKit.Text(UiKit.Box(_root, "Body", 0.03f, 0.03f, 0.97f, 0.77f), "Text", 22, TextAnchor.UpperLeft, Ink);

            _root.gameObject.SetActive(false);
        }

        public bool IsOpen => _root.gameObject.activeSelf;

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Redraw();
        }

        public void Close() => _root.gameObject.SetActive(false);

        /// <summary>Hands the drawer this frame's pages. The open page stays open if it still may.</summary>
        public void Show(JournalBook book)
        {
            _book = book;
            if (IsOpen) Redraw();
        }

        private void Redraw()
        {
            UiKit.ClearChildren(_tabs);
            if (_book == null) return;

            JournalPage page = _book.Find(_pageId);
            _pageId = page?.Id;
            _hint.text = _book.ClosingHint;

            IReadOnlyList<JournalPage> pages = _book.Pages;
            float width = pages.Count == 0 ? 1f : 1f / pages.Count;
            for (int i = 0; i < pages.Count; i++)
            {
                JournalPage tab = pages[i];
                string id = tab.Id;
                Button button = UiKit.Button(_tabs, "Tab" + i, JournalBook.TabText(tab), () => { _pageId = id; Redraw(); },
                    tab.Id == _pageId ? TabOn : Tab, Ink, 18,
                    new Vector2(i * width, 0f), new Vector2((i + 1) * width - 0.01f, 1f));
                button.interactable = _book.CanOpen(tab);
            }

            _title.text = page?.Title ?? "";
            _disclosure.text = page?.DisclosureLabel ?? "";
            _body.text = page?.Body ?? "";
        }
    }
}
#endif
