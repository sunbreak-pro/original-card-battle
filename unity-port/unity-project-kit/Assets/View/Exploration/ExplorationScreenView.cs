// The exploration screen (dungeon_exploration_v4.md §6), built in code with UiKit on its own canvas.
// It computes no rule: every number, label, warning and button comes from the ExplorationScreen that
// Exploration.Script built from the core. The only arithmetic here is layout — placing a node's
// (x, y) fraction inside the map, and drawing the link between two placed nodes.
//
// The look is a stand-in: grey squares with one character for the kind, shades of grey and a single
// accent colour (§6.10). The design lane and #90 own the real look; replacing it touches this file
// and ExplorationPalette only. The backdrop of each layer with its veil, the 瘴気 gauge and the shade
// that closes in on the screen are already the real look (battle-visual-v1 §2.5, #614 and #613): the
// backdrop is Backdrop.View.BackdropLayers, the veil is Backdrop.ExplorationBackdropSpec's, and every
// colour, alpha and timing of the gauge and the shade is Backdrop.MiasmaLook's.
//
// #626: the pick screen (an elite's two cards, the card pile's three, a 習熟訓練 mark's card) is drawn
// from the CardPick Exploration.Bridge built, each card the battle's own CardView bound to its face.
// #717: the choosing screen at a 死亡地点の痕跡 is drawn the same way from the HeritagePick.
// #719: the swap screen for a consumable found with the slots full, from the ConsumableSwap.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Linq;
using Backdrop;
using Backdrop.View;
using Depiction;
using Depiction.View;
using DungeonCore;
using Exploration.Bridge;
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
        // The pick screen lays its cards out at most this many to a row, under a caption this tall.
        private const int PickColumns = 6;
        private const float PickCaption = 40f;
        private const float PickGap = 24f;
        // The choosing screen at a trace (#717), in fractions of its plate: the foot holds the count and the
        // confirm button, a row of pages takes HeritagePageRow, and with cards above, the pages stop at
        // HeritagePagesWithCards. A chosen card wears a frame this thick.
        private const int HeritagePageColumns = 3;
        private const float HeritageTop = 0.80f;
        private const float HeritageFoot = 0.12f;
        private const float HeritagePageRow = 0.085f;
        private const float HeritagePagesWithCards = 0.26f;
        private const float HeritageMark = 6f;
        // The shade is drawn into a texture this small and stretched over the screen: it is a smooth falloff.
        private const int ShadeWidth = 64;
        private const int ShadeHeight = 36;

        private readonly Action<int, RestChoice> _step;
        private readonly Action<ScreenAction> _act;
        private readonly Action<int> _useConsumable;
        private readonly Action<Overlay> _toggle;
        private readonly Action<string> _pickCard;
        private readonly Action<string> _toggleLegacy;
        private readonly Action _confirmLegacy;
        private readonly Action<int> _swapFound;
        private readonly Action _discardFound;

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
        private readonly Button _cardsButton;
        private readonly Button _settingsButton;
        private readonly RectTransform _pickLayer;
        private readonly RectTransform _swapLayer;
        private readonly RectTransform _heritageLayer;
        private readonly RectTransform _overlayLayer;
        private readonly JournalDrawerView _journal;

        // The backdrop behind the map and the veil over it (battle-visual-v1 §2.5, #614). The veil's alpha
        // differs by layer, so it crossfades with the backdrop: Tick moves it from _veilFrom to _veilTo.
        private readonly RectTransform _backdropHost;
        private readonly Image _veil;
        private BackdropLayers _backdrop;
        private Color _veilFrom;
        private Color _veilTo;
        private double _veilSince;

        // The 瘴気 gauge (§6.2) and the shade (battle-visual-v1 §2.5), moved every frame by Tick.
        private readonly RectTransform _root;
        private Image _gaugeAhead;
        private Image _gaugeFill;
        private int _gaugeFrom;
        private int _gaugeTo = -1;
        private int _gaugeNext;
        private double _gaugeSince;
        // The breath follows the settled reading, not the one easing up, so a rise only changes its pace.
        private readonly MiasmaBreath _breath = new MiasmaBreath();
        private Image _shade;
        private Image _seep;
        private Color32[] _shadePixels;
        private Color32[] _seepPixels;
        private float _shadeAspect;
        private float _shadeRadius = -1f;
        private float _shadeStrength = -1f;

        private ExplorationScreen _screen;
        private int _pinned = -1;
        private int _hovered = -1;
        private int _lastCurrent = -1;

        /// <param name="canvas">A full-screen rect on a top-level canvas.</param>
        /// <param name="step">Walk to a node with a choice (the choice only matters on a 休息 node).</param>
        /// <param name="act">A card button or 「この生を終える」.</param>
        /// <param name="useConsumable">Burn the consumable in a slot.</param>
        /// <param name="toggle">A band button or a close button: open or close that screen.</param>
        /// <param name="pickCard">A card pressed on the pick screen (#626), by its id.</param>
        /// <param name="toggleLegacy">A choice pressed on the choosing screen at a trace (#717), by its key.</param>
        /// <param name="confirmLegacy">The choosing screen's confirm button.</param>
        /// <param name="swapFound">A slot pressed on the swap screen (#719), by its index.</param>
        /// <param name="discardFound">The swap screen's 「諦める」.</param>
        public ExplorationScreenView(RectTransform canvas, Action<int, RestChoice> step, Action<ScreenAction> act, Action<int> useConsumable,
            Action<Overlay> toggle, Action<string> pickCard, Action<string> toggleLegacy, Action confirmLegacy, Action<int> swapFound, Action discardFound)
        {
            _step = step;
            _act = act;
            _useConsumable = useConsumable;
            _toggle = toggle;
            _pickCard = pickCard;
            _toggleLegacy = toggleLegacy;
            _confirmLegacy = confirmLegacy;
            _swapFound = swapFound;
            _discardFound = discardFound;

            RectTransform root = UiKit.Box(canvas, "Exploration", 0f, 0f, 1f, 1f);
            _root = root;
            UiKit.Fill(root, "Ground", ExplorationPalette.Ground);

            // Back to front: the layer's backdrop, the veil of the ground colour over it, the miasma's shade,
            // then everything the screen is for (battle-visual-v1 §2.5). The backdrop is made by the first
            // Render, once the screen says which layer it is on.
            _backdropHost = UiKit.Box(root, "Backdrop", 0f, 0f, 1f, 1f);
            _veil = UiKit.Fill(root, "Veil", Color.clear);
            BuildShade(root);

            // Sibling order is draw order. The map and the detail go first, then the card layer that
            // covers them; the band, the carried items and 「この生を終える」 sit above a card so the
            // gauge stays readable and an item can still be used at the interlude. Whether those
            // buttons work is the screen's CanUse / CanEndLife, not the layering. 所持カード and
            // 設定・セーブ cover all of that but the band, whose buttons switch between them.

            // ---- the map ----
            // The map, its links and its labels are drawn straight on the veiled backdrop, not on a panel
            // (battle-visual-v1 §2.5), over the shade and under the band and the right column.
            _mapPanel = UiKit.Box(root, "Map", 0.015f, 0.11f, 0.665f, 0.89f);
            _mapArea = UiKit.Point(_mapPanel, "Area", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(MapWidth, MapHeight), Vector2.zero);
            _links = UiKit.Rect(_mapArea, "Links", Vector2.zero, Vector2.one);
            _nodes = UiKit.Rect(_mapArea, "Nodes", Vector2.zero, Vector2.one);
            // The hint sits on the backdrop, so it takes the body colour the veil was sized for (§2.5).
            _hint = UiKit.Text(UiKit.Box(root, "Hint", 0.21f, 0.02f, 0.665f, 0.10f), "Text", 20, TextAnchor.MiddleLeft, ExplorationPalette.Ink);

            // ---- the node detail ----
            RectTransform detail = UiKit.Box(root, "Detail", 0.68f, 0.36f, 0.985f, 0.89f);
            UiKit.Fill(detail, "Back", ExplorationPalette.Panel);
            _detailTitle = UiKit.Text(UiKit.Box(detail, "Title", 0f, 0.88f, 1f, 1f), "Text", 28, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _detailBody = UiKit.Text(UiKit.Box(detail, "Body", 0f, 0.16f, 1f, 0.88f), "Text", 20, TextAnchor.UpperLeft, ExplorationPalette.Ink);
            _detailButtons = UiKit.Box(detail, "Buttons", 0.03f, 0.03f, 0.97f, 0.14f);

            // ---- the card layer ----
            _cardLayer = UiKit.Box(root, "Cards", 0f, 0f, 1f, 1f);

            // ---- top band: layer, 刻限, 瘴気, HP / stamina, then journal / cards / settings ----
            RectTransform band = UiKit.Box(root, "Band", 0f, 0.905f, 1f, 1f);
            UiKit.Fill(band, "Back", ExplorationPalette.Band);
            _layerTitle = UiKit.Text(UiKit.Box(band, "Layer", 0.01f, 0f, 0.20f, 1f), "Text", 30, TextAnchor.MiddleLeft, ExplorationPalette.Ink);

            RectTransform time = UiKit.Box(band, "Time", 0.21f, 0f, 0.31f, 1f);
            _timeLabel = UiKit.Text(UiKit.Box(time, "Label", 0f, 0.45f, 1f, 1f), "Text", 24, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _timePips = UiKit.Box(time, "Pips", 0.04f, 0.14f, 0.96f, 0.40f);

            RectTransform miasma = UiKit.Box(band, "Miasma", 0.32f, 0f, 0.53f, 1f);
            _miasmaLabel = UiKit.Text(UiKit.Box(miasma, "Label", 0f, 0.45f, 0.4f, 1f), "Text", 24, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _miasmaForecast = UiKit.Text(UiKit.Box(miasma, "Forecast", 0.4f, 0.45f, 1f, 1f), "Text", 18, TextAnchor.MiddleRight, ExplorationPalette.InkSoft);
            _gauge = UiKit.Box(miasma, "Gauge", 0.02f, 0.14f, 0.98f, 0.40f);
            BuildGauge();

            // Three short lines: HP and the training marks, stamina, and how max stamina is made up.
            RectTransform body = UiKit.Box(band, "Body", 0.54f, 0f, 0.77f, 1f);
            _hpLabel = UiKit.Text(UiKit.Box(body, "Hp", 0f, 0.64f, 0.5f, 1f), "Text", 20, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _marksLabel = UiKit.Text(UiKit.Box(body, "Marks", 0.5f, 0.64f, 1f, 1f), "Text", 16, TextAnchor.MiddleRight, ExplorationPalette.InkSoft);
            _staminaLabel = UiKit.Text(UiKit.Box(body, "Stamina", 0f, 0.32f, 1f, 0.66f), "Text", 20, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _maxStaminaLabel = UiKit.Text(UiKit.Box(body, "MaxStamina", 0f, 0f, 1f, 0.34f), "Text", 16, TextAnchor.MiddleLeft, ExplorationPalette.InkSoft);

            // Left to right: journal, cards, and the gear at the very end, away from 「この生を終える」 (§6.2).
            UiKit.Button(band, "Journal", ExplorationText.JournalButton, () => _toggle(Overlay.Journal),
                ExplorationPalette.Button, ExplorationPalette.Ink, 18, new Vector2(0.78f, 0.18f), new Vector2(0.845f, 0.82f));
            _cardsButton = UiKit.Button(band, "Cards", ExplorationText.CardsButton, () => _toggle(Overlay.Cards),
                ExplorationPalette.Button, ExplorationPalette.Ink, 18, new Vector2(0.85f, 0.18f), new Vector2(0.925f, 0.82f));
            _settingsButton = UiKit.Button(band, "Settings", ExplorationText.SettingsButton, () => _toggle(Overlay.Settings),
                ExplorationPalette.Button, ExplorationPalette.Ink, 18, new Vector2(0.93f, 0.18f), new Vector2(0.99f, 0.82f));

            // ---- what is carried ----
            RectTransform carried = UiKit.Box(root, "Carried", 0.68f, 0.11f, 0.985f, 0.34f);
            UiKit.Fill(carried, "Back", ExplorationPalette.Panel);
            _toolsLabel = UiKit.Text(UiKit.Box(carried, "Tools", 0f, 0.62f, 1f, 1f), "Text", 20, TextAnchor.MiddleLeft, ExplorationPalette.Ink);
            _slots = UiKit.Box(carried, "Slots", 0.03f, 0.08f, 0.97f, 0.58f);

            // ---- the survivor route (§6.6) ----
            _endLife = UiKit.Button(root, "EndLife", ExplorationText.EndLifeButton, () => _act(ScreenAction.AskEndLife),
                ExplorationPalette.Button, ExplorationPalette.Ink, 24, new Vector2(0.015f, 0.02f), new Vector2(0.2f, 0.095f));

            // ---- the pick screen (#626): over the map, the card and the carried items; under 所持カード
            // so the cards owned can be read before one is picked ----
            _pickLayer = UiKit.Box(root, "Pick", 0f, 0f, 1f, 1f);

            // ---- the swap screen (#719): ConsumableSwaps shows it only once no card waits ----
            _swapLayer = UiKit.Box(root, "Swap", 0f, 0f, 1f, 1f);

            // ---- the choosing screen at a trace (#717): over the pick screen, so a card offer that waits
            // too comes after it ----
            _heritageLayer = UiKit.Box(root, "Heritage", 0f, 0f, 1f, 1f);

            // ---- 所持カード / 設定・セーブ, under the band so its buttons stay pressable ----
            _overlayLayer = UiKit.Box(root, "Overlay", 0f, 0f, 1f, 1f);
            band.SetAsLastSibling();

            // The journal may be read at any time, so it opens above everything.
            _journal = new JournalDrawerView(root, () => _toggle(Overlay.Journal));
        }

        public void Render(ExplorationScreen screen)
        {
            _screen = screen;
            DrawBackdrop(screen.Layer);
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
            SetGauge(screen.Miasma);
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

            _cardsButton.interactable = screen.CanOpenCards;
            _settingsButton.interactable = screen.CanOpenSettings;
            DrawOverlay(screen);
            _journal.Show(screen.Journal);
            bool journal = screen.OpenOverlay == Overlay.Journal;
            if (journal && !_journal.IsOpen) _journal.Open();
            else if (!journal && _journal.IsOpen) _journal.Close();
        }

        // ---- the backdrop and its veil (battle-visual-v1 §2.5, #614) ----

        /// <summary>
        /// Lays the backdrop of <paramref name="layer"/> behind the map. A new layer crossfades over
        /// BackdropSpec.CrossfadeMs, the veil's alpha with it, so the map never jumps between two veils.
        /// </summary>
        private void DrawBackdrop(int layer)
        {
            int at = BackdropSpec.Clamp(layer);
            Color veil = ExplorationPalette.Of(ExplorationBackdropSpec.For(at).Veil);
            if (_backdrop == null)
            {
                // The backdrop sizes its hazes from the area's rect, which a new canvas has not laid out yet.
                Canvas.ForceUpdateCanvases();
                _backdrop = BackdropLayers.Create(_backdropHost, at);
                _veilFrom = _veilTo = veil;
                _veil.color = veil;
                return;
            }
            if (at == _backdrop.Layer) return;
            _veilFrom = _veil.color;
            _veilTo = veil;
            _veilSince = NowMs;
            _backdrop.Show(at);
        }

        private void FadeVeil()
        {
            if (_backdrop == null) return;
            _veil.color = Color.Lerp(_veilFrom, _veilTo, BackdropMotion.Crossfade(NowMs - _veilSince));
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

        // ---- the 瘴気 gauge and the shade (battle-visual-v1 §2.5, #613) ----

        private static double NowMs => Time.unscaledTimeAsDouble * 1000.0;

        /// <summary>Moves the gauge's rise and breath and the shade to this frame. Call once a frame.</summary>
        public void Tick()
        {
            FadeVeil();
            if (_gaugeTo < 0) return;
            double now = NowMs;
            float shown = MiasmaLook.Rise(_gaugeFrom, _gaugeTo, now - _gaugeSince);
            float fill = Mathf.Clamp01(shown / MiasmaLook.DeathAt);
            float next = Mathf.Clamp01(_gaugeNext / (float)MiasmaLook.DeathAt);
            float breath = _breath.Advance(now, _gaugeTo);

            Color colour = ExplorationPalette.Of(MiasmaLook.FillAt(Mathf.RoundToInt(shown)));
            colour.a *= MiasmaLook.FillAlphaOf(breath);
            _gaugeFill.color = colour;
            _gaugeFill.rectTransform.anchorMax = new Vector2(fill, 1f);
            _gaugeAhead.rectTransform.anchorMin = new Vector2(fill, 0f);
            _gaugeAhead.rectTransform.anchorMax = new Vector2(Mathf.Max(fill, next), 1f);

            DrawShade(MiasmaLook.ShadeBreathing(shown, breath));
        }

        /// <summary>The track, the fill, the share ahead and the nine ticks, made once; Tick moves them.</summary>
        private void BuildGauge()
        {
            UiKit.Fill(_gauge, "Track", ExplorationPalette.Of(MiasmaLook.Track));
            _gaugeAhead = UiKit.Image(_gauge, "Ahead", ProceduralArt.White, Color.clear, Vector2.zero, new Vector2(0f, 1f));
            _gaugeFill = UiKit.Image(_gauge, "Fill", ProceduralArt.White, Color.clear, Vector2.zero, new Vector2(0f, 1f));
            Color tickColour = ExplorationPalette.Of(MiasmaLook.Tick);
            foreach (GaugeTick tick in MiasmaLook.Ticks())
            {
                // Full height at the 20% lines, 4/10 between, standing on the track's floor.
                float at = tick.Percent / (float)MiasmaLook.DeathAt;
                Image mark = UiKit.Image(_gauge, "Tick" + tick.Percent, ProceduralArt.White, tickColour,
                    new Vector2(at, 0f), new Vector2(at, tick.Height));
                mark.rectTransform.offsetMin = new Vector2(-1f, 0f);
                mark.rectTransform.offsetMax = new Vector2(1f, 0f);
            }
            UiKit.Frame(_gauge, ExplorationPalette.Of(MiasmaLook.Frame), 1f);
        }

        /// <summary>Takes a new reading: the fill climbs (or falls) to it over MiasmaLook.RiseMs from where it is drawn now.</summary>
        private void SetGauge(MiasmaGauge gauge)
        {
            double now = NowMs;
            if (_gaugeTo < 0)
            {
                _gaugeFrom = gauge.Percent;
                _gaugeTo = gauge.Percent;
                _gaugeSince = now - MiasmaLook.RiseMs;
            }
            else if (gauge.Percent != _gaugeTo)
            {
                _gaugeFrom = Mathf.RoundToInt(MiasmaLook.Rise(_gaugeFrom, _gaugeTo, now - _gaugeSince));
                _gaugeTo = gauge.Percent;
                _gaugeSince = now;
            }
            _gaugeNext = gauge.NextPercent;
            _gaugeAhead.color = ExplorationPalette.Of(MiasmaLook.AheadColor(gauge.Percent, gauge.NextPercent));
            Tick();
        }

        /// <summary>
        /// The shade over the whole screen: the dark (MiasmaLook.ShadeColor) and the violet seep on top,
        /// each a small texture stretched over the screen. Their pixels are redrawn only when the shade moves.
        /// </summary>
        private void BuildShade(RectTransform root)
        {
            _shadePixels = new Color32[ShadeWidth * ShadeHeight];
            _seepPixels = new Color32[ShadeWidth * ShadeHeight];
            _shade = UiKit.Image(root, "MiasmaShade", ShadeSprite(), Color.white, Vector2.zero, Vector2.one);
            _seep = UiKit.Image(root, "MiasmaSeep", ShadeSprite(), Color.white, Vector2.zero, Vector2.one);
        }

        private static Sprite ShadeSprite()
        {
            var texture = new Texture2D(ShadeWidth, ShadeHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            return Sprite.Create(texture, new Rect(0, 0, ShadeWidth, ShadeHeight), new Vector2(0.5f, 0.5f), 100f);
        }

        private void DrawShade(MiasmaShade shade)
        {
            Rect area = _root.rect;
            float aspect = area.height > 0f ? area.width / area.height : 16f / 9f;
            if (Mathf.Abs(aspect - _shadeAspect) < 0.001f
                && Mathf.Abs(shade.InnerRadius - _shadeRadius) < 0.0005f
                && Mathf.Abs(shade.Strength - _shadeStrength) < 0.0005f) return;
            _shadeAspect = aspect;
            _shadeRadius = shade.InnerRadius;
            _shadeStrength = shade.Strength;

            Color32 dark = ExplorationPalette.Of(MiasmaLook.ShadeColor);
            Color32 seep = ExplorationPalette.Of(MiasmaLook.SeepColor);
            for (int y = 0; y < ShadeHeight; y++)
            {
                for (int x = 0; x < ShadeWidth; x++)
                {
                    float distance = MiasmaLook.CornerDistance((x + 0.5f) / ShadeWidth, (y + 0.5f) / ShadeHeight, aspect);
                    int i = y * ShadeWidth + x;
                    dark.a = (byte)Mathf.RoundToInt(255f * MiasmaLook.ShadeAlphaAt(shade, distance));
                    seep.a = (byte)Mathf.RoundToInt(255f * shade.SeepAlpha * MiasmaLook.EdgeFalloff(shade, distance));
                    _shadePixels[i] = dark;
                    _seepPixels[i] = seep;
                }
            }
            Upload(_shade, _shadePixels);
            Upload(_seep, _seepPixels);
        }

        private static void Upload(Image image, Color32[] pixels)
        {
            Texture2D texture = image.sprite.texture;
            texture.SetPixels32(pixels);
            texture.Apply();
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

        // ---- the pick screen (#626) ----

        /// <summary>Shows the pick screen, or clears it when <paramref name="pick"/> is not shown.</summary>
        public void ShowPick(CardPick pick)
        {
            UiKit.ClearChildren(_pickLayer);
            if (pick == null || !pick.IsShown) return;

            RectTransform plate = Plate(_pickLayer, pick.Title, pick.Prompt);
            RectTransform area = UiKit.Box(plate, "Cards", 0.02f, 0.03f, 0.98f, 0.80f);
            LayOutCards(area, pick.Choices.Count, (i, scale, at, step) =>
            {
                PickChoice choice = pick.Choices[i];
                string id = choice.CardId;
                DrawCardCell(area, "Choice_" + id, choice.Face, choice.Caption, scale, at, step, () => _pickCard(id));
            });
        }

        // ---- the choosing screen at a 死亡地点の痕跡 (#717, dungeon_exploration_v4.md §7.7) ----

        /// <summary>
        /// Shows the choosing screen, or clears it when <paramref name="pick"/> is not shown: the cards on
        /// top with their face at the step they are handed on with, each page as its one line under them,
        /// and the count and the confirm button at the foot. What is chosen is marked; pressing a choice
        /// again takes it back off.
        /// </summary>
        public void ShowHeritage(HeritagePick pick)
        {
            UiKit.ClearChildren(_heritageLayer);
            if (pick == null || !pick.IsShown) return;

            RectTransform plate = Plate(_heritageLayer, pick.Title, pick.Prompt);
            var cards = pick.Choices.Where(c => c.Kind == LegacyKind.Card).ToList();
            var pages = pick.Choices.Where(c => c.Kind == LegacyKind.Page).ToList();

            int rows = (pages.Count + HeritagePageColumns - 1) / HeritagePageColumns;
            float pagesHeight = Mathf.Min(rows * HeritagePageRow, cards.Count > 0 ? HeritagePagesWithCards : HeritageTop - HeritageFoot);
            float pagesTop = HeritageFoot + pagesHeight;
            if (cards.Count > 0)
            {
                RectTransform area = UiKit.Box(plate, "Cards", 0.02f, pagesTop, 0.98f, HeritageTop);
                LayOutCards(area, cards.Count, (i, scale, at, step) =>
                {
                    HeritageChoice choice = cards[i];
                    string key = choice.Key;
                    CardView card = DrawCardCell(area, "Choice_" + key, choice.Face, choice.Caption, scale, at, step, () => _toggleLegacy(key));
                    if (choice.Selected) UiKit.Frame(card.Rect, ExplorationPalette.Accent, HeritageMark);
                });
            }
            if (pages.Count > 0)
            {
                RectTransform lines = UiKit.Box(plate, "Pages", 0.02f, HeritageFoot, 0.98f, pagesTop);
                float width = 1f / HeritagePageColumns;
                float height = 1f / rows;
                for (int i = 0; i < pages.Count; i++)
                {
                    HeritageChoice choice = pages[i];
                    string key = choice.Key;
                    int row = i / HeritagePageColumns;
                    int column = i % HeritagePageColumns;
                    UiKit.Button(lines, "Choice_" + key, choice.Title + "\n" + choice.Caption, () => _toggleLegacy(key),
                        choice.Selected ? ExplorationPalette.Accent : ExplorationPalette.Button,
                        choice.Selected ? ExplorationPalette.InkOnAccent : ExplorationPalette.Ink, 18,
                        new Vector2(column * width, 1f - (row + 1) * height + 0.01f), new Vector2((column + 1) * width - 0.01f, 1f - row * height));
                }
            }

            UiKit.Text(UiKit.Box(plate, "Count", 0.03f, 0.02f, 0.68f, HeritageFoot - 0.01f), "Text", 24, TextAnchor.MiddleRight, ExplorationPalette.Ink, pick.CountLine);
            UiKit.Button(plate, "Confirm", pick.ConfirmLabel, () => _confirmLegacy(), ExplorationPalette.Accent, ExplorationPalette.InkOnAccent, 26,
                new Vector2(0.70f, 0.02f), new Vector2(0.97f, HeritageFoot - 0.01f));
        }

        // ---- the swap screen (#719, tools_and_consumables_v4.md §3) ----

        /// <summary>
        /// Shows the swap screen, or clears it when <paramref name="swap"/> is not shown: the item found, a
        /// button for each slot with what it holds, and 「諦める」.
        /// </summary>
        public void ShowSwap(ConsumableSwap swap)
        {
            UiKit.ClearChildren(_swapLayer);
            if (swap == null || !swap.IsShown) return;

            RectTransform plate = Plate(_swapLayer, swap.Title, swap.Prompt);
            UiKit.Text(UiKit.Box(plate, "Item", 0.03f, 0.66f, 0.97f, 0.80f), "Text", 26, TextAnchor.MiddleLeft, ExplorationPalette.Ink, swap.ItemLine);

            RectTransform slots = UiKit.Box(plate, "Slots", 0.03f, 0.28f, 0.97f, 0.62f);
            float width = 1f / Math.Max(1, swap.Slots.Count);
            for (int i = 0; i < swap.Slots.Count; i++)
            {
                SwapSlot slot = swap.Slots[i];
                int index = slot.Index;
                string label = slot.Note.Length == 0 ? slot.Label : slot.Label + "\n" + slot.Note;
                UiKit.Button(slots, "Slot" + i, label, () => _swapFound(index), ExplorationPalette.Accent, ExplorationPalette.InkOnAccent, 20,
                    new Vector2(i * width, 0f), new Vector2((i + 1) * width - 0.01f, 1f));
            }

            UiKit.Button(plate, "Discard", swap.DiscardLabel, () => _discardFound(), ExplorationPalette.Button, ExplorationPalette.Ink, 24,
                new Vector2(0.35f, 0.07f), new Vector2(0.65f, 0.20f));
        }

        // ---- what the pick screens share ----

        /// <summary>The dim that stops the presses reaching the map, and the plate with the title and the prompt.</summary>
        private static RectTransform Plate(RectTransform layer, string title, string prompt)
        {
            Image dim = UiKit.Fill(layer, "Dim", ExplorationPalette.Dim);
            dim.raycastTarget = true;

            RectTransform plate = UiKit.Box(layer, "Plate", 0.04f, 0.04f, 0.96f, 0.89f);
            UiKit.Fill(plate, "Back", ExplorationPalette.Band);
            UiKit.Frame(plate, ExplorationPalette.Accent, 3f);
            UiKit.Text(UiKit.Box(plate, "Title", 0.03f, 0.89f, 0.97f, 0.98f), "Text", 36, TextAnchor.MiddleLeft, ExplorationPalette.Ink, title);
            UiKit.Text(UiKit.Box(plate, "Prompt", 0.03f, 0.82f, 0.97f, 0.89f), "Text", 22, TextAnchor.MiddleLeft, ExplorationPalette.InkSoft, prompt);
            return plate;
        }

        /// <summary>Lays <paramref name="count"/> cells out in centred rows, scaled to fit the area. Each cell is drawn by <paramref name="draw"/>: its index, scale, place and size.</summary>
        private static void LayOutCards(RectTransform area, int count, Action<int, float, Vector2, Vector2> draw)
        {
            Canvas.ForceUpdateCanvases();
            Rect room = area.rect;
            int columns = Math.Max(1, Math.Min(PickColumns, count));
            int rows = Math.Max(1, (count + columns - 1) / columns);
            Vector2 cell = new Vector2(CardView.Size.x + PickGap, CardView.Size.y + PickCaption + PickGap);
            float scale = Mathf.Min(1f, Mathf.Min(room.width / (columns * cell.x), room.height / (rows * cell.y)));
            Vector2 step = cell * scale;

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int inRow = Math.Min(columns, count - row * columns);
                float x = ((i % columns) - (inRow - 1) * 0.5f) * step.x;
                float y = ((rows - 1) * 0.5f - row) * step.y;
                draw(i, scale, new Vector2(x, y), step);
            }
        }

        private static CardView DrawCardCell(RectTransform area, string name, CardFace face, string captionText, float scale, Vector2 at, Vector2 step, Action press)
        {
            RectTransform cell = UiKit.Point(area, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), step, at);
            CardView card = NewCard(cell);
            RectTransform rect = card.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one * scale;
            card.Bind(face);
            card.SetDimmed(false);
            card.Interactable = false;

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.background;
            button.onClick.AddListener(() => press());

            if (captionText.Length == 0) return card;
            RectTransform caption = UiKit.Point(cell, "Caption", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(step.x, PickCaption * scale), new Vector2(0f, PickGap * 0.5f * scale));
            UiKit.Text(caption, "Text", 20, TextAnchor.MiddleCenter, ExplorationPalette.Ink, captionText);
            return card;
        }

        /// <summary>
        /// A hand card made in code, as the battle's prefab would make it: the scene has no Card prefab, so
        /// the ground the press lands on is made here and CardView lays out the rest (CardView.EnsureParts).
        /// </summary>
        private static CardView NewCard(RectTransform parent)
        {
            var go = new GameObject("Card", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<CardView>();
            card.group = go.AddComponent<CanvasGroup>();
            card.background = UiKit.Image((RectTransform)go.transform, "Background", ProceduralArt.White, Color.white, Vector2.zero, Vector2.one);
            card.background.raycastTarget = true;
            go.SetActive(true);
            return card;
        }

        // ---- 所持カード / 設定・セーブ ----

        private void DrawOverlay(ExplorationScreen screen)
        {
            UiKit.ClearChildren(_overlayLayer);
            Overlay open = screen.OpenOverlay;
            OverlayPage page = open == Overlay.Cards ? screen.Cards : open == Overlay.Settings ? screen.Settings : null;
            if (page == null) return;

            Image dim = UiKit.Fill(_overlayLayer, "Dim", ExplorationPalette.Dim);
            dim.raycastTarget = true;

            RectTransform plate = UiKit.Box(_overlayLayer, "Plate", 0.22f, 0.14f, 0.78f, 0.86f);
            UiKit.Fill(plate, "Back", ExplorationPalette.Band);
            UiKit.Frame(plate, ExplorationPalette.Accent, 3f);
            UiKit.Text(UiKit.Box(plate, "Title", 0.04f, 0.86f, 0.70f, 0.97f), "Text", 34, TextAnchor.MiddleLeft, ExplorationPalette.Ink, page.Title);
            UiKit.Button(plate, "Close", page.CloseLabel, () => _toggle(open), ExplorationPalette.Button, ExplorationPalette.Ink, 20,
                new Vector2(0.72f, 0.875f), new Vector2(0.96f, 0.955f));
            UiKit.Text(UiKit.Box(plate, "Lines", 0.04f, 0.05f, 0.96f, 0.83f), "Text", 22, TextAnchor.UpperLeft, ExplorationPalette.Ink,
                string.Join("\n", page.Lines));
        }
    }
}
#endif
