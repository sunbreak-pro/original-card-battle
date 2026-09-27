// The exploration screen (dungeon_exploration_v4.md §6), built in code with UiKit on its own canvas.
// It computes no rule: every number, label, warning and button comes from the ExplorationScreen that
// Exploration.Script built from the core. The only arithmetic here is layout — placing a node's
// (x, y) fraction inside the map, and drawing the link between two placed nodes.
//
// The look is a stand-in: grey squares with one character for the kind, shades of grey and a single
// accent colour (§6.10). The design lane and #90 own the real look; replacing it touches this file
// and ExplorationPalette only.
#if UNITY_2021_2_OR_NEWER
using System;
using DungeonCore;
using Journal.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Exploration.View
{
    public sealed class ExplorationScreenView
    {
        // The map is drawn in a fixed-size area so a node's fraction maps to the same place on any screen.
        private const float MapWidth = 1060f;
        private const float MapHeight = 700f;
        private const float NodeSize = 116f;
        private const float LinkThickness = 6f;

        private readonly Action<int, RestChoice> _step;
        private readonly Action<ScreenAction> _act;
        private readonly Action<int> _useConsumable;

        private readonly Text _layerTitle;
        private readonly Text _timeLabel;
        private readonly RectTransform _timePips;
        private readonly Text _miasmaLabel;
        private readonly Text _miasmaForecast;
        private readonly RectTransform _gauge;
        private readonly Text _hpLabel;
        private readonly Text _staminaLabel;
        private readonly Text _maxStaminaLabel;
        private readonly Text _marksLabel;
        private readonly RectTransform _mapPanel;
        private readonly RectTransform _mapArea;
        private readonly RectTransform _links;
        private readonly RectTransform _nodes;
        private readonly Text _hint;
        private readonly Text _detailTitle;
        private readonly Text _detailBody;
        private readonly RectTransform _detailButtons;
        private readonly Text _toolsLabel;
        private readonly RectTransform _slots;
        private readonly Button _endLife;
        private readonly RectTransform _cardLayer;
        private readonly JournalDrawerView _journal;

        private ExplorationScreen _screen;
        private int _pinned = -1;
        private int _hovered = -1;
        private int _lastCurrent = -1;

        /// <param name="canvas">A full-screen rect on a top-level canvas.</param>
        /// <param name="step">Walk to a node with a choice (the choice only matters on a 休息 node).</param>
        /// <param name="act">A card button or 「この生を終える」.</param>
        /// <param name="useConsumable">Burn the consumable in a slot.</param>
        public ExplorationScreenView(RectTransform canvas, Action<int, RestChoice> step, Action<ScreenAction> act, Action<int> useConsumable)
        {
            _step = step;
            _act = act;
            _useConsumable = useConsumable;

            RectTransform root = UiKit.Box(canvas, "Exploration", 0f, 0f, 1f, 1f);
            UiKit.Fill(root, "Ground", ExplorationPalette.Ground);

            // Sibling order is draw order. The map and the detail go first, then the card layer that
            // covers them; the band, the carried items and 「この生を終える」 sit above a card so the
            // gauge stays readable and an item can still be used at the interlude. Whether those
            // buttons work is the screen's CanUse / CanEndLife, not the layering.

            // ---- the map ----
            _mapPanel = UiKit.Box(root, "Map", 0.015f, 0.11f, 0.665f, 0.89f);
            UiKit.Fill(_mapPanel, "Back", ExplorationPalette.Panel);
            _mapArea = UiKit.Point(_mapPanel, "Area", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(MapWidth, MapHeight), Vector2.zero);
            _links = UiKit.Rect(_mapArea, "Links", Vector2.zero, Vector2.one);
            _nodes = UiKit.Rect(_mapArea, "Nodes", Vector2.zero, Vector2.one);
            _hint = UiKit.Text(UiKit.Box(root, "Hint", 0.21f, 0.02f, 0.665f, 0.10f), "Text", 20, TextAnchor.MiddleLeft, ExplorationPalette.InkSoft);

            // ---- the node detail ----
            RectTransform detail = UiKit.Box(root, "Detail", 0.68f, 0.36f, 0.985f, 0.89f);
            UiKit.Fill(detail, "Back", ExplorationPalette.Panel);
            _detailTitle = UiKit.Text(UiKit.Box(detail, "Title", 0f, 0.88f, 1f, 1f), "Text", 28, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _detailBody = UiKit.Text(UiKit.Box(detail, "Body", 0f, 0.16f, 1f, 0.88f), "Text", 20, TextAnchor.UpperLeft, ExplorationPalette.Ink);
            _detailButtons = UiKit.Box(detail, "Buttons", 0.03f, 0.03f, 0.97f, 0.14f);

            // ---- the card layer ----
            _cardLayer = UiKit.Box(root, "Cards", 0f, 0f, 1f, 1f);

            // ---- top band: layer, 刻限, 瘴気, HP / stamina, journal ----
            RectTransform band = UiKit.Box(root, "Band", 0f, 0.905f, 1f, 1f);
            UiKit.Fill(band, "Back", ExplorationPalette.Band);
            _layerTitle = UiKit.Text(UiKit.Box(band, "Layer", 0.01f, 0f, 0.24f, 1f), "Text", 30, TextAnchor.MiddleLeft, ExplorationPalette.Ink);

            RectTransform time = UiKit.Box(band, "Time", 0.25f, 0f, 0.37f, 1f);
            _timeLabel = UiKit.Text(UiKit.Box(time, "Label", 0f, 0.45f, 1f, 1f), "Text", 24, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _timePips = UiKit.Box(time, "Pips", 0.04f, 0.14f, 0.96f, 0.40f);

            RectTransform miasma = UiKit.Box(band, "Miasma", 0.38f, 0f, 0.63f, 1f);
            _miasmaLabel = UiKit.Text(UiKit.Box(miasma, "Label", 0f, 0.45f, 0.4f, 1f), "Text", 24, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _miasmaForecast = UiKit.Text(UiKit.Box(miasma, "Forecast", 0.4f, 0.45f, 1f, 1f), "Text", 18, TextAnchor.MiddleRight, ExplorationPalette.InkSoft);
            _gauge = UiKit.Box(miasma, "Gauge", 0.02f, 0.14f, 0.98f, 0.40f);

            // Three short lines: HP and the training marks, stamina, and how max stamina is made up.
            RectTransform body = UiKit.Box(band, "Body", 0.64f, 0f, 0.89f, 1f);
            _hpLabel = UiKit.Text(UiKit.Box(body, "Hp", 0f, 0.64f, 0.5f, 1f), "Text", 20, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _marksLabel = UiKit.Text(UiKit.Box(body, "Marks", 0.5f, 0.64f, 1f, 1f), "Text", 16, TextAnchor.MiddleRight, ExplorationPalette.InkSoft);
            _staminaLabel = UiKit.Text(UiKit.Box(body, "Stamina", 0f, 0.32f, 1f, 0.66f), "Text", 20, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _maxStaminaLabel = UiKit.Text(UiKit.Box(body, "MaxStamina", 0f, 0f, 1f, 0.34f), "Text", 16, TextAnchor.MiddleLeft, ExplorationPalette.InkSoft);

            UiKit.Button(band, "Journal", ExplorationText.JournalButton, ToggleJournal, ExplorationPalette.Button, ExplorationPalette.Ink, 22,
                new Vector2(0.90f, 0.18f), new Vector2(0.99f, 0.82f));

            // ---- what is carried ----
            RectTransform carried = UiKit.Box(root, "Carried", 0.68f, 0.11f, 0.985f, 0.34f);
            UiKit.Fill(carried, "Back", ExplorationPalette.Panel);
            _toolsLabel = UiKit.Text(UiKit.Box(carried, "Tools", 0f, 0.62f, 1f, 1f), "Text", 20, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _slots = UiKit.Box(carried, "Slots", 0.03f, 0.08f, 0.97f, 0.58f);

            // ---- the survivor route (§6.6) ----
            _endLife = UiKit.Button(root, "EndLife", ExplorationText.EndLifeButton, () => _act(ScreenAction.AskEndLife),
                ExplorationPalette.Button, ExplorationPalette.Ink, 24, new Vector2(0.015f, 0.02f), new Vector2(0.2f, 0.095f));

            // The journal may be read at any time, so it opens above everything.
            _journal = new JournalDrawerView(root);
        }

        public bool JournalOpen => _journal.IsOpen;

        public void ToggleJournal() => _journal.Toggle();

        public void Render(ExplorationScreen screen)
        {
            _screen = screen;
            if (screen.CurrentNodeId != _lastCurrent)
            {
                // After a step the detail follows the player to where they now stand.
                _lastCurrent = screen.CurrentNodeId;
                _pinned = screen.CurrentNodeId;
            }
            if (screen.Node(_pinned) == null) _pinned = screen.CurrentNodeId;
            _hovered = -1;

            _layerTitle.text = screen.LayerTitle;
            _timeLabel.text = screen.TimeLabel;
            DrawPips(screen.TimeLeft, screen.TimeLimit);
            _miasmaLabel.text = screen.Miasma.Label;
            _miasmaForecast.text = screen.Miasma.ForecastLabel;
            DrawGauge(screen.Miasma);
            _hpLabel.text = screen.HpLabel;
            _staminaLabel.text = screen.StaminaLabel;
            _maxStaminaLabel.text = screen.MaxStaminaLabel;
            _marksLabel.text = screen.MarksLabel;

            DrawMap(screen);
            _hint.text = screen.Hint;
            DrawDetail();
            DrawCarried(screen);
            _endLife.gameObject.SetActive(screen.ShowsEndLife);
            _endLife.interactable = screen.CanEndLife;
            DrawCard(screen.Card);
            _journal.Show(screen.Journal);
        }

        // ---- top band ----

        private void DrawPips(int left, int limit)
        {
            UiKit.ClearChildren(_timePips);
            if (limit <= 0) return;
            float width = 1f / limit;
            for (int i = 0; i < limit; i++)
            {
                Image pip = UiKit.Image(_timePips, "Pip" + i, ProceduralArt.White,
                    i < left ? ExplorationPalette.Accent : ExplorationPalette.Empty,
                    new Vector2(i * width, 0f), new Vector2((i + 1) * width, 1f));
                pip.rectTransform.offsetMin = new Vector2(2f, 0f);
                pip.rectTransform.offsetMax = new Vector2(-2f, 0f);
            }
        }

        private void DrawGauge(MiasmaGauge gauge)
        {
            UiKit.ClearChildren(_gauge);
            UiKit.Fill(_gauge, "Back", ExplorationPalette.Empty);
            UiKit.Image(_gauge, "Next", ProceduralArt.White, ExplorationPalette.AccentGhost,
                Vector2.zero, new Vector2(Mathf.Clamp01(gauge.NextFill), 1f));
            UiKit.Image(_gauge, "Fill", ProceduralArt.White, ExplorationPalette.Accent,
                Vector2.zero, new Vector2(Mathf.Clamp01(gauge.Fill), 1f));
            foreach (int line in gauge.StepLines)
            {
                float at = line / 100f;
                Image mark = UiKit.Image(_gauge, "Line" + line, ProceduralArt.White, ExplorationPalette.Line,
                    new Vector2(at, 0f), new Vector2(at, 1f));
                mark.rectTransform.offsetMin = new Vector2(-1f, 0f);
                mark.rectTransform.offsetMax = new Vector2(1f, 0f);
            }
        }

        // ---- the map ----

        private static Vector2 Place(NodeCard node) =>
            new Vector2((node.X - 0.5f) * MapWidth, (node.Y - 0.5f) * MapHeight);

        private void DrawMap(ExplorationScreen screen)
        {
            FitMap();
            UiKit.ClearChildren(_links);
            UiKit.ClearChildren(_nodes);

            foreach (EdgeLine edge in screen.Edges)
            {
                Vector2 from = Place(screen.Node(edge.From));
                Vector2 to = Place(screen.Node(edge.To));
                Vector2 span = to - from;
                Image link = UiKit.Sprite(_links, "Link" + edge.From + "-" + edge.To, ProceduralArt.White,
                    edge.Lit ? ExplorationPalette.Accent : ExplorationPalette.Link,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(span.magnitude, edge.Lit ? LinkThickness : LinkThickness * 0.5f), (from + to) * 0.5f);
                link.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            }

            foreach (NodeCard node in screen.Nodes) DrawNode(node, screen.MapIsLive);
        }

        /// <summary>
        /// Shrinks the fixed-size map area to its panel when the screen is wider than 16:9 and the
        /// scaler leaves the panel shorter than the map. Never enlarges it.
        /// </summary>
        private void FitMap()
        {
            Canvas.ForceUpdateCanvases();
            Rect panel = _mapPanel.rect;
            float scale = Mathf.Min(1f, Mathf.Min(panel.width / (MapWidth + NodeSize), panel.height / (MapHeight + NodeSize)));
            _mapArea.localScale = new Vector3(scale, scale, 1f);
        }

        private void DrawNode(NodeCard node, bool live)
        {
            RectTransform rt = UiKit.Point(_nodes, "Node" + node.Id, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(NodeSize, NodeSize), Place(node));
            Image face = rt.gameObject.AddComponent<Image>();
            face.sprite = ProceduralArt.White;
            face.color = ExplorationPalette.NodeFill(node.Standing);
            face.raycastTarget = true;

            float frame = ExplorationPalette.FrameThickness(node.Standing);
            if (frame > 0f) UiKit.Frame(rt, ExplorationPalette.FrameColour(node.Standing), frame);

            Color ink = node.Standing == NodeStanding.Far || node.Standing == NodeStanding.Resolved
                ? ExplorationPalette.InkSoft
                : ExplorationPalette.InkOnNode;
            UiKit.Text(UiKit.Box(rt, "Glyph", 0f, 0.3f, 1f, 1f), "Text", 46, TextAnchor.MiddleCenter, ink, node.Glyph);
            // 死亡地点の痕跡 is the longest label; it may run a little past the square rather than wrap.
            Text kind = UiKit.Text(UiKit.Box(rt, "Kind", 0f, 0f, 1f, 0.34f), "Text", 14, TextAnchor.MiddleCenter, ink, node.KindLabel);
            kind.horizontalOverflow = HorizontalWrapMode.Overflow;

            int id = node.Id;
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.interactable = live;
            button.onClick.AddListener(() => Pin(id));
            UiKit.OnPointer(rt.gameObject, EventTriggerType.PointerEnter, _ => Hover(id));
            UiKit.OnPointer(rt.gameObject, EventTriggerType.PointerExit, _ => Hover(-1));
        }

        private void Pin(int id)
        {
            _pinned = id;
            DrawDetail();
        }

        private void Hover(int id)
        {
            _hovered = id;
            DrawDetail();
        }

        // ---- the node detail ----

        private void DrawDetail()
        {
            UiKit.ClearChildren(_detailButtons);
            if (_screen == null) return;

            // Hovering reads a node; only the pinned (clicked) one gets buttons.
            bool hovering = _hovered >= 0 && _hovered != _pinned;
            NodeCard node = _screen.Node(hovering ? _hovered : _pinned);
            if (node == null)
            {
                _detailTitle.text = "";
                _detailBody.text = "";
                return;
            }

            _detailTitle.text = node.Title;
            _detailBody.text = string.Join("\n", node.Detail);

            if (hovering || node.Options.Count == 0) return;
            float width = 1f / node.Options.Count;
            for (int i = 0; i < node.Options.Count; i++)
            {
                StepOption option = node.Options[i];
                int id = node.Id;
                RestChoice choice = option.Choice;
                UiKit.Button(_detailButtons, "Option" + i, option.Label, () => _step(id, choice),
                    option.NeedsConfirm ? ExplorationPalette.Warning : ExplorationPalette.Accent, ExplorationPalette.InkOnAccent, 24,
                    new Vector2(i * width, 0f), new Vector2((i + 1) * width - 0.02f, 1f));
            }
        }

        // ---- what is carried ----

        private void DrawCarried(ExplorationScreen screen)
        {
            _toolsLabel.text = screen.ToolsLabel;
            UiKit.ClearChildren(_slots);
            float width = 1f / Math.Max(1, screen.Consumables.Count);
            for (int i = 0; i < screen.Consumables.Count; i++)
            {
                SlotCard slot = screen.Consumables[i];
                int index = slot.Index;
                Button button = UiKit.Button(_slots, "Slot" + i, slot.Name, () => _useConsumable(index),
                    ExplorationPalette.Button, ExplorationPalette.Ink, 20,
                    new Vector2(i * width, 0f), new Vector2((i + 1) * width - 0.02f, 1f));
                button.interactable = slot.CanUse;
                string note = slot.Note;
                UiKit.OnPointer(button.gameObject, EventTriggerType.PointerEnter, _ => _hint.text = note);
                UiKit.OnPointer(button.gameObject, EventTriggerType.PointerExit, _ => _hint.text = _screen != null ? _screen.Hint : "");
            }
        }

        // ---- the card over the map ----

        private void DrawCard(ScreenCard card)
        {
            UiKit.ClearChildren(_cardLayer);
            if (!card.IsShown) return;

            Image dim = UiKit.Fill(_cardLayer, "Dim", ExplorationPalette.Dim);
            dim.raycastTarget = true;

            RectTransform plate = UiKit.Box(_cardLayer, "Card", 0.07f, 0.24f, 0.61f, 0.76f);
            UiKit.Fill(plate, "Back", ExplorationPalette.Band);
            UiKit.Frame(plate, ExplorationPalette.Accent, 3f);
            UiKit.Text(UiKit.Box(plate, "Title", 0.04f, 0.82f, 0.96f, 0.97f), "Text", 36, TextAnchor.MiddleLeft, ExplorationPalette.Ink, card.Title);
            UiKit.Text(UiKit.Box(plate, "Lines", 0.04f, 0.24f, 0.96f, 0.80f), "Text", 22, TextAnchor.UpperLeft, ExplorationPalette.Ink,
                string.Join("\n", card.Lines));

            RectTransform buttons = UiKit.Box(plate, "Buttons", 0.04f, 0.05f, 0.96f, 0.19f);
            float width = 1f / Math.Max(1, card.Buttons.Count);
            for (int i = 0; i < card.Buttons.Count; i++)
            {
                CardButton button = card.Buttons[i];
                ScreenAction action = button.Action;
                UiKit.Button(buttons, "Button" + i, button.Label, () => _act(action),
                    i == 0 ? ExplorationPalette.Accent : ExplorationPalette.Button,
                    i == 0 ? ExplorationPalette.InkOnAccent : ExplorationPalette.Ink, 24,
                    new Vector2(i * width, 0f), new Vector2((i + 1) * width - 0.02f, 1f));
            }
        }
    }
}
#endif
