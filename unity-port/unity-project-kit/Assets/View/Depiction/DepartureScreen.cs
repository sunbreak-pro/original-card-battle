// The departure screen of the demo (#58), between the deck screen and the mode screen: the three
// tool slots and the three consumable slots, the start a tool moves back, and the talent card.
// Built in code with UiKit on the demo canvas like the deck screen, so no scene or prefab changes.
// It decides nothing: what fits a slot, where the battle starts, which cards may be the talent,
// every word, and whether the run may set out come from Depiction.Bridge.DepartureBuilder, whose
// rules are BattleCore.DeckRules'.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using BattleCore;
using Depiction.Bridge;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class DepartureScreen
    {
        private const int TalentColumns = 3;
        private const int TalentRows = 6;
        private const int TalentPerPage = TalentColumns * TalentRows;

        // The supply rows on the left, as shares of the screen's height.
        private const float RowHeight = 0.056f;
        private const float ToolsTop = 0.85f;
        private const float ConsumablesTop = 0.34f;

        private readonly RectTransform _root;
        private readonly DepartureBuilder _builder;
        private readonly Action<Departure> _setOut;
        private DeckBuilder _deck = new DeckBuilder();
        private int _talentPage;

        private readonly RectTransform _tools;
        private readonly RectTransform _consumables;
        private readonly RectTransform _talentGrid;
        private readonly Text _talentPageText;
        private readonly Text _info;
        private readonly Text _status;
        private readonly Button _setOutButton;

        /// <param name="parent">The demo canvas.</param>
        /// <param name="builder">What the player picks; kept across visits, so the slots and the talent stay chosen.</param>
        /// <param name="setOut">Called with the departure when 「出立する」 is pressed.</param>
        /// <param name="back">「デッキに戻る」.</param>
        public DepartureScreen(RectTransform parent, DepartureBuilder builder, Action<Departure> setOut, Action back)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _setOut = setOut;

            _root = UiKit.Box(parent, "Departure", 0f, 0f, 1f, 1f);
            // Opaque like the deck screen (#211): the battle underneath neither shows nor takes clicks.
            Image backdrop = UiKit.Fill(_root, "Back", BattleTheme.Ground);
            backdrop.raycastTarget = true;

            UiKit.Label(_root, "Title", 34, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(1600f, 60f), new Vector2(40f, -20f), DepartureBuilder.Title);

            Heading("ToolHead", DepartureBuilder.ToolHeading, ToolsTop + 0.005f);
            _tools = UiKit.Box(_root, "Tools", 0.02f, ToolsTop - RowHeight * _builder.ToolOptions.Count, 0.60f, ToolsTop);
            Heading("ConsumableHead", DepartureBuilder.ConsumableHeading, ConsumablesTop + 0.005f);
            _consumables = UiKit.Box(_root, "Consumables", 0.02f, ConsumablesTop - RowHeight * _builder.ConsumableOptions.Count, 0.60f, ConsumablesTop);

            UiKit.Text(UiKit.Box(_root, "TalentHead", 0.62f, 0.855f, 0.98f, 0.905f), "Text", 24, TextAnchor.MiddleLeft, BattleTheme.Warm,
                DepartureBuilder.TalentHeading);
            _talentGrid = UiKit.Box(_root, "Talent", 0.62f, 0.50f, 0.98f, 0.85f);
            RectTransform nav = UiKit.Box(_root, "TalentPages", 0.62f, 0.44f, 0.98f, 0.49f);
            UiKit.Button(nav, "Prev", "前のページ", () => TurnTalent(-1), BattleTheme.Panel, BattleTheme.Ink, 22, new Vector2(0f, 0f), new Vector2(0.3f, 1f));
            _talentPageText = UiKit.Text(UiKit.Box(nav, "PageText", 0.32f, 0f, 0.68f, 1f), "Text", 22, TextAnchor.MiddleCenter, BattleTheme.Ink2);
            UiKit.Button(nav, "Next", "次のページ", () => TurnTalent(1), BattleTheme.Panel, BattleTheme.Ink, 22, new Vector2(0.7f, 0f), new Vector2(1f, 1f));

            RectTransform infoBox = UiKit.Panel(_root, "Info", 0.62f, 0.14f, 0.98f, 0.42f).rectTransform;
            _info = UiKit.Text(UiKit.Box(infoBox, "Lines", 0.03f, 0.22f, 0.97f, 0.97f), "Text", 22, TextAnchor.UpperLeft, BattleTheme.Ink);
            _status = UiKit.Text(UiKit.Box(infoBox, "Status", 0.03f, 0.02f, 0.97f, 0.2f), "Text", 22, TextAnchor.MiddleLeft, BattleTheme.Warm);

            RectTransform go = UiKit.Box(_root, "Go", 0.62f, 0.02f, 0.98f, 0.11f);
            UiKit.Button(go, "BackToDeck", DepartureBuilder.BackLabel, () => back(), BattleTheme.Panel, BattleTheme.Ink, 26,
                new Vector2(0f, 0f), new Vector2(0.38f, 1f));
            _setOutButton = UiKit.Button(go, "SetOut", DepartureBuilder.SetOutLabel, SetOut, BattleTheme.Accent, BattleTheme.InkBlack, 30,
                new Vector2(0.40f, 0f), new Vector2(1f, 1f));

            Visible = false;
        }

        public bool Visible
        {
            get { return _root.gameObject.activeSelf; }
            set { _root.gameObject.SetActive(value); }
        }

        /// <summary>Opens the screen for the deck the deck screen just handed over.</summary>
        public void Show(DeckBuilder deck)
        {
            _deck = deck ?? new DeckBuilder();
            Refresh();
            Visible = true;
        }

        // ---- actions ----

        private void SetOut()
        {
            if (!_builder.CanSetOut(_deck)) return;
            _setOut?.Invoke(_builder.Build(_deck));
        }

        private void TurnTalent(int by)
        {
            _talentPage += by;
            Refresh();
        }

        // ---- drawing ----

        private void Refresh()
        {
            DrawSupplies(_tools, _builder.ToolOptions, _builder.ToolCount, _builder.CanAddTool,
                id => _builder.AddTool(id), id => _builder.RemoveTool(id));
            DrawSupplies(_consumables, _builder.ConsumableOptions, _builder.ConsumableCount, _builder.CanAddConsumable,
                id => _builder.AddConsumable(id), id => _builder.RemoveConsumable(id));
            DrawTalent();

            _info.text = string.Join("\n", new[]
            {
                _builder.TalentLine(_deck),
                _builder.StartLine,
                _builder.ToolsLine,
                _builder.ConsumablesLine,
                "デッキ: " + _deck.Total + " 枚",
            });
            _status.text = _builder.StatusText(_deck);
            _setOutButton.interactable = _builder.CanSetOut(_deck);
        }

        private void Heading(string name, string text, float bottom)
        {
            UiKit.Text(UiKit.Box(_root, name, 0.02f, bottom, 0.60f, bottom + 0.045f), "Text", 24, TextAnchor.MiddleLeft, BattleTheme.Warm, text);
        }

        /// <summary>One row per option: its line, then − count ＋.</summary>
        private void DrawSupplies(RectTransform box, IReadOnlyList<SupplyOption> options, Func<string, int> count,
            Func<string, bool> canAdd, Func<string, bool> add, Func<string, bool> remove)
        {
            UiKit.ClearChildren(box);
            float share = 1f / Math.Max(1, options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                SupplyOption option = options[i];
                string id = option.Id;
                float y1 = 1f - i * share;
                RectTransform row = UiKit.Box(box, "Row" + i, 0f, y1 - share + 0.004f, 1f, y1 - 0.004f);
                Color ink = option.WorksInBattle ? BattleTheme.Ink : BattleTheme.Ink2;
                UiKit.Text(UiKit.Box(row, "Line", 0f, 0f, 0.78f, 1f), "Text", 18, TextAnchor.MiddleLeft, ink, DepartureBuilder.LineOf(option));
                UiKit.Button(row, "Minus", "−", () => { remove(id); Refresh(); }, BattleTheme.Panel, BattleTheme.Ink, 24,
                    new Vector2(0.79f, 0.08f), new Vector2(0.85f, 0.92f));
                int n = count(id);
                Text shown = UiKit.Text(UiKit.Box(row, "Count", 0.85f, 0f, 0.92f, 1f), "Text", 22, TextAnchor.MiddleCenter, BattleTheme.Warm,
                    n == 0 ? "" : "×" + n);
                shown.fontStyle = FontStyle.Bold;
                Button plus = UiKit.Button(row, "Plus", "＋", () => { add(id); Refresh(); }, BattleTheme.Panel, BattleTheme.Ink, 24,
                    new Vector2(0.92f, 0.08f), new Vector2(0.98f, 0.92f));
                plus.interactable = canAdd(id);
            }
        }

        private void DrawTalent()
        {
            UiKit.ClearChildren(_talentGrid);
            DeckPage page = DepartureBuilder.TalentPage(_deck, _talentPage, TalentPerPage);
            _talentPage = page.Index;
            _talentPageText.text = page.Text;
            string chosen = _builder.TalentIn(_deck);
            for (int i = 0; i < page.Cards.Count; i++)
            {
                CardDef def = page.Cards[i];
                int column = i % TalentColumns;
                int row = i / TalentColumns;
                float x0 = column / (float)TalentColumns;
                float y1 = 1f - row / (float)TalentRows;
                bool on = def.Id == chosen;
                UiKit.Button(_talentGrid, "Talent" + i, def.Name, () => { _builder.ChooseTalent(_deck, def.Id); Refresh(); },
                    on ? BattleTheme.Accent : BattleTheme.Panel, on ? BattleTheme.InkBlack : BattleTheme.Ink, 22,
                    new Vector2(x0 + 0.004f, y1 - 1f / TalentRows + 0.008f), new Vector2(x0 + 1f / TalentColumns - 0.004f, y1 - 0.008f));
            }
        }
    }
}
#endif
