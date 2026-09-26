// The deck screen of the demo (#190): the eighty cards, a count per kind, the filters and presets,
// and 「戦闘へ」 once the deck passes §8. Built in code with UiKit on the demo canvas, so no scene or
// prefab changes. It decides nothing: which cards a filter keeps, what a page holds, every word
// about a card and whether the deck may fight all come from Depiction.Bridge.DeckBuilder.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using BattleCore;
using Depiction.Bridge;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class DeckSelectScreen
    {
        private const int Columns = 4;
        private const int Rows = 5;
        private const int PerPage = Columns * Rows;

        private readonly RectTransform _root;
        private readonly Action<DeckBuilder> _changed;
        private readonly Action<List<CardInstance>> _battle;
        private readonly DeckFilter _filter = new DeckFilter();
        private DeckBuilder _deck;
        private int _page;
        private CardDef _selected;
        private int _randomSeed = Environment.TickCount;

        private readonly RectTransform _filterRow;
        private readonly RectTransform _grid;
        private readonly Text _pageText;
        private readonly Text _detail;
        private readonly Text _status;
        private readonly Text _deckText;
        private readonly Button _toBattle;

        /// <param name="parent">The demo canvas.</param>
        /// <param name="deck">The deck to start from (the saved one, or the prototype).</param>
        /// <param name="changed">Called after every change, so the flow can save the deck.</param>
        /// <param name="battle">Called with the built deck when 「戦闘へ」 is pressed.</param>
        public DeckSelectScreen(RectTransform parent, DeckBuilder deck, Action<DeckBuilder> changed, Action<List<CardInstance>> battle)
        {
            _deck = deck ?? new DeckBuilder();
            _changed = changed;
            _battle = battle;

            _root = UiKit.Box(parent, "DeckSelect", 0f, 0f, 1f, 1f);
            Image back = UiKit.Fill(_root, "Back", BattleTheme.WithAlpha(BattleTheme.Ground, 0.97f));
            back.raycastTarget = true; // the battle underneath takes no clicks while the deck is built

            UiKit.Label(_root, "Title", 34, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(1400f, 60f), new Vector2(40f, -20f), DeckBuilder.Title);

            _filterRow = UiKit.Box(_root, "Filters", 0.02f, 0.86f, 0.62f, 0.91f);
            _grid = UiKit.Box(_root, "Cards", 0.02f, 0.13f, 0.62f, 0.85f);

            RectTransform nav = UiKit.Box(_root, "Pages", 0.02f, 0.04f, 0.62f, 0.11f);
            UiKit.Button(nav, "Prev", "前のページ", () => Turn(-1), BattleTheme.Panel, BattleTheme.Ink, 26, new Vector2(0f, 0f), new Vector2(0.25f, 1f));
            _pageText = UiKit.Text(UiKit.Box(nav, "PageText", 0.27f, 0f, 0.73f, 1f), "Text", 26, TextAnchor.MiddleCenter, BattleTheme.Ink2);
            UiKit.Button(nav, "Next", "次のページ", () => Turn(1), BattleTheme.Panel, BattleTheme.Ink, 26, new Vector2(0.75f, 0f), new Vector2(1f, 1f));

            RectTransform detailBox = UiKit.Panel(_root, "Detail", 0.64f, 0.52f, 0.98f, 0.91f).rectTransform;
            _detail = UiKit.Text(detailBox, "Text", 24, TextAnchor.UpperLeft, BattleTheme.Ink);

            RectTransform deckBox = UiKit.Panel(_root, "Deck", 0.64f, 0.17f, 0.98f, 0.50f).rectTransform;
            _status = UiKit.Text(UiKit.Box(deckBox, "Status", 0f, 0.82f, 1f, 1f), "Text", 24, TextAnchor.MiddleLeft, BattleTheme.Warm);
            _deckText = UiKit.Text(UiKit.Box(deckBox, "List", 0f, 0f, 1f, 0.82f), "Text", 20, TextAnchor.UpperLeft, BattleTheme.Ink);

            RectTransform presets = UiKit.Box(_root, "Presets", 0.64f, 0.10f, 0.98f, 0.155f);
            UiKit.Button(presets, "Prototype", "試作デッキ", () => Replace(DeckBuilder.Prototype()), BattleTheme.Panel, BattleTheme.Ink, 24, new Vector2(0f, 0f), new Vector2(0.32f, 1f));
            UiKit.Button(presets, "Random", "ランダム", () => Replace(DeckBuilder.Random(_randomSeed++)), BattleTheme.Panel, BattleTheme.Ink, 24, new Vector2(0.34f, 0f), new Vector2(0.66f, 1f));
            UiKit.Button(presets, "Clear", "空にする", () => Replace(new DeckBuilder()), BattleTheme.Panel, BattleTheme.Ink, 24, new Vector2(0.68f, 0f), new Vector2(1f, 1f));

            RectTransform go = UiKit.Box(_root, "Go", 0.64f, 0.02f, 0.98f, 0.085f);
            _toBattle = UiKit.Button(go, "ToBattle", "戦闘へ", () => _battle?.Invoke(_deck.Build()), BattleTheme.Accent, BattleTheme.InkBlack, 30, Vector2.zero, Vector2.one);

            Refresh();
        }

        public bool Visible
        {
            get { return _root.gameObject.activeSelf; }
            set { _root.gameObject.SetActive(value); }
        }

        // ---- actions ----

        private void Turn(int by)
        {
            _page += by;
            Refresh();
        }

        private void Replace(DeckBuilder deck)
        {
            _deck = deck;
            Changed();
        }

        private void Add(CardDef def)
        {
            _selected = def;
            if (_deck.Add(def.Id)) Changed();
            else Refresh();
        }

        private void Remove(CardDef def)
        {
            _selected = def;
            if (_deck.Remove(def.Id)) Changed();
            else Refresh();
        }

        private void Changed()
        {
            _changed?.Invoke(_deck);
            Refresh();
        }

        // ---- drawing ----

        private void Refresh()
        {
            DrawFilters();
            DeckPage page = _deck.Page(_filter, _page, PerPage);
            _page = page.Index;
            _pageText.text = page.Text;
            DrawCards(page);

            _detail.text = _selected != null ? string.Join("\n", DeckBuilder.DetailOf(_selected)) : "カードを押すと、効果の文が出ます。";
            _status.text = _deck.StatusText;
            _deckText.text = string.Join("　", _deck.DeckLines());
            _toBattle.interactable = _deck.IsValid;
        }

        private void DrawFilters()
        {
            UiKit.ClearChildren(_filterRow);
            var attributes = DeckBuilder.AttributeOptions;
            var costs = DeckBuilder.CostOptions;
            float width = 1f / (attributes.Count + costs.Count);
            int slot = 0;
            foreach (KeyValuePair<string, BattleAttribute> option in attributes)
            {
                BattleAttribute chosen = option.Value;
                bool on = _filter.Attribute == chosen;
                UiKit.Button(_filterRow, "Attr" + slot, option.Key, () => { _filter.Attribute = chosen; _page = 0; Refresh(); },
                    on ? BattleTheme.Accent : BattleTheme.Panel, on ? BattleTheme.InkBlack : BattleTheme.Ink, 22,
                    new Vector2(slot * width, 0f), new Vector2((slot + 1) * width - 0.004f, 1f));
                slot++;
            }
            foreach (KeyValuePair<string, int> option in costs)
            {
                int chosen = option.Value;
                bool on = _filter.Cost == chosen;
                UiKit.Button(_filterRow, "Cost" + slot, option.Key, () => { _filter.Cost = chosen; _page = 0; Refresh(); },
                    on ? BattleTheme.Warm : BattleTheme.Panel, on ? BattleTheme.InkBlack : BattleTheme.Ink, 22,
                    new Vector2(slot * width, 0f), new Vector2((slot + 1) * width - 0.004f, 1f));
                slot++;
            }
        }

        private void DrawCards(DeckPage page)
        {
            UiKit.ClearChildren(_grid);
            for (int i = 0; i < page.Cards.Count; i++)
            {
                CardDef def = page.Cards[i];
                int column = i % Columns;
                int row = i / Columns;
                float x0 = column / (float)Columns;
                float y1 = 1f - row / (float)Rows;
                RectTransform cell = UiKit.Box(_grid, "Card" + i, x0 + 0.003f, y1 - 1f / Rows + 0.006f, x0 + 1f / Columns - 0.003f, y1 - 0.006f);

                bool chosen = _selected != null && _selected.Id == def.Id;
                Color back = chosen ? BattleTheme.WithAlpha(BattleTheme.Accent, 0.35f) : BattleTheme.Panel;
                UiKit.Button(cell, "Select", DeckBuilder.LineOf(def), () => { _selected = def; Refresh(); }, back, BattleTheme.Ink, 20,
                    new Vector2(0f, 0.34f), new Vector2(1f, 1f));
                UiKit.Button(cell, "Minus", "−", () => Remove(def), BattleTheme.Panel, BattleTheme.Ink, 26, new Vector2(0f, 0f), new Vector2(0.3f, 0.3f));
                Text count = UiKit.Text(UiKit.Box(cell, "Count", 0.3f, 0f, 0.7f, 0.3f), "Text", 24, TextAnchor.MiddleCenter, BattleTheme.Warm, _deck.CountText(def.Id));
                count.fontStyle = FontStyle.Bold;
                Button plus = UiKit.Button(cell, "Plus", "＋", () => Add(def), BattleTheme.Panel, BattleTheme.Ink, 26, new Vector2(0.7f, 0f), new Vector2(1f, 0.3f));
                plus.interactable = _deck.CanAdd(def.Id);
            }
        }
    }
}
#endif
